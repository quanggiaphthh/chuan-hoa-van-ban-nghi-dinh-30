using System.Buffers.Binary;
using System.IO.Compression;
using System.Text;
using System.Xml;
using Nd30.DocumentEngine.Model;

namespace Nd30.DocumentEngine.Safety;

/// <summary>
/// Fail-closed package inspection. The central directory count is checked before
/// ZipArchive is constructed, and every entry is drained through a byte-counted
/// stream before an Open XML parser may read the staged package.
/// </summary>
public static class PackageSafetyPreflight
{
    private const int EndRecordLength = 22;
    private const int MaximumZipCommentLength = ushort.MaxValue;
    private const uint EndRecordSignature = 0x06054b50;
    private const uint CentralDirectoryHeaderSignature = 0x02014b50;
    private static readonly StringComparer PartComparer = StringComparer.OrdinalIgnoreCase;

    public static PackageSafetyState Inspect(string path, CancellationToken cancellationToken = default) =>
        Inspect(path, PackageSafetyBudget.Default, cancellationToken);

    internal static PackageSafetyState Inspect(string path, PackageSafetyBudget budget, CancellationToken cancellationToken = default)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var info = new FileInfo(path);
            if (!info.Exists) return Rejected("MALFORMED_DOCX", "The DOCX package could not be read.");
            if (info.Length > budget.MaximumCompressedInputBytes) return Rejected("INPUT_TOO_LARGE", "The DOCX input exceeds the allowed size.");
            if (info.Length < EndRecordLength) return Rejected("MALFORMED_DOCX", "The DOCX package is malformed.");

            using var source = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.SequentialScan);
            var entryCountFromDirectory = ReadEntryCountBeforeArchive(source, info.Length, budget, cancellationToken);
            if (entryCountFromDirectory > budget.MaximumEntryCount) return Rejected("ARCHIVE_ENTRY_LIMIT", "The DOCX package contains too many parts.");

            source.Position = 0;
            using var archive = new ZipArchive(source, ZipArchiveMode.Read, leaveOpen: false);
            if (archive.Entries.Count != entryCountFromDirectory) return Rejected("UNSAFE_ARCHIVE", "The DOCX package has an ambiguous archive directory.");

            var entriesByName = new Dictionary<string, ZipArchiveEntry>(PartComparer);
            long declaredTotal = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var normalizedName = NormalizeAndValidateEntryName(entry.FullName);
                if (!entriesByName.TryAdd(normalizedName, entry)) return Rejected("UNSAFE_ARCHIVE", "The DOCX package contains duplicate or ambiguous part names.");
                if (entry.Length < 0 || entry.CompressedLength < 0) return Rejected("UNSAFE_ARCHIVE", "The DOCX package contains invalid part sizes.");
                if (entry.Length > budget.MaximumSingleEntryUncompressedBytes) return Rejected("ARCHIVE_ENTRY_TOO_LARGE", "A DOCX part exceeds the allowed size.");
                declaredTotal = checked(declaredTotal + entry.Length);
                if (declaredTotal > budget.MaximumTotalUncompressedBytes) return Rejected("ARCHIVE_EXPANSION_LIMIT", "The DOCX package expands beyond the allowed size.");
                if (ExceedsRatio(entry.Length, entry.CompressedLength, budget.MaximumEntryCompressionRatio))
                    return Rejected("ARCHIVE_EXPANSION_LIMIT", "A DOCX part exceeds the allowed expansion ratio.");
            }

            if (!entriesByName.ContainsKey("[Content_Types].xml") ||
                !entriesByName.ContainsKey("_rels/.rels") ||
                !entriesByName.ContainsKey("word/document.xml"))
                return Rejected("MALFORMED_DOCX", "Required DOCX package parts are missing.");

            var contentTypes = ReadContentTypes(entriesByName["[Content_Types].xml"], budget, cancellationToken);

            var macros = entriesByName.ContainsKey("word/vbaProject.bin");
            var ole = entriesByName.Keys.Any(n => n.StartsWith("word/embeddings/", StringComparison.OrdinalIgnoreCase));
            var signature = entriesByName.Keys.Any(n => n.StartsWith("_xmlsignatures/", StringComparison.OrdinalIgnoreCase));
            var trackedChanges = false;
            var protectedDocument = false;
            var externalRelationships = false;
            var unsupported = new HashSet<string>(StringComparer.Ordinal);
            if (entriesByName.Keys.Any(n => n.EndsWith("encryptedPackage", StringComparison.OrdinalIgnoreCase))) unsupported.Add("encrypted-package");
            if (entriesByName.Keys.Any(n => n.Contains("activeX", StringComparison.OrdinalIgnoreCase))) unsupported.Add("activeX-control");
            if (entriesByName.Keys.Any(n => n.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) && n.Contains("customXml", StringComparison.OrdinalIgnoreCase))) unsupported.Add("custom-xml-relationships");

            long actualTotal = 0;
            long actualXmlTotal = 0;
            foreach (var entry in archive.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var isXml = entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase) ||
                    contentTypes.IsXmlPart(entry.FullName);
                using var raw = entry.Open();
                using var counted = new BoundedEntryReadStream(raw, entry.Length, budget.MaximumSingleEntryUncompressedBytes,
                    isXml ? budget.MaximumTotalXmlBytes - actualXmlTotal : long.MaxValue,
                    () => cancellationToken.ThrowIfCancellationRequested(), count =>
                    {
                        actualTotal = checked(actualTotal + count);
                        if (actualTotal > budget.MaximumTotalUncompressedBytes) throw new PreflightFailure("ARCHIVE_EXPANSION_LIMIT", "The DOCX package expands beyond the allowed size.");
                        if (isXml)
                        {
                            actualXmlTotal = checked(actualXmlTotal + count);
                            if (actualXmlTotal > budget.MaximumTotalXmlBytes) throw new PreflightFailure("UNSAFE_XML", "DOCX XML exceeds the allowed processing budget.");
                        }
                    });

                if (isXml)
                {
                    var flags = ScanXml(counted, budget, cancellationToken);
                    if (entry.FullName.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                    {
                        trackedChanges |= flags.ContainsAny("ins", "del", "moveFrom", "moveTo");
                        if (flags.Contains("altChunk")) unsupported.Add("altChunk");
                    }
                    if (entry.FullName.Equals("word/settings.xml", StringComparison.OrdinalIgnoreCase) && flags.Contains("documentProtection")) protectedDocument = true;
                    if (entry.FullName.EndsWith(".rels", StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var relation in flags.Relationships)
                        {
                            if (relation.TargetMode.Equals("External", StringComparison.OrdinalIgnoreCase)) externalRelationships = true;
                            if (relation.Type.EndsWith("/attachedTemplate", StringComparison.OrdinalIgnoreCase)) unsupported.Add("attached-template");
                            if (relation.Type.EndsWith("/package", StringComparison.OrdinalIgnoreCase)) unsupported.Add("embedded-package");
                            if (relation.Type.EndsWith("/oleObject", StringComparison.OrdinalIgnoreCase) && relation.TargetMode.Equals("External", StringComparison.OrdinalIgnoreCase)) unsupported.Add("linked-ole");
                            if (relation.Type.EndsWith("/control", StringComparison.OrdinalIgnoreCase)) unsupported.Add("activex-control");
                        }
                    }
                }
                else
                {
                    Drain(counted, cancellationToken);
                }

                if (counted.BytesRead != entry.Length) return Rejected("MALFORMED_DOCX", "A DOCX part did not match its declared size.");
                if (ExceedsRatio(counted.BytesRead, entry.CompressedLength, budget.MaximumEntryCompressionRatio))
                    return Rejected("ARCHIVE_EXPANSION_LIMIT", "A DOCX part exceeds the allowed expansion ratio.");
            }

            var diagnostics = new List<Diagnostic>();
            foreach (var feature in unsupported.OrderBy(x => x, StringComparer.Ordinal))
                diagnostics.Add(new("UNSUPPORTED_FEATURE", $"Unsupported mutation-risk feature detected: {feature}."));
            if (signature) diagnostics.Add(new("SIGNED_PACKAGE", "OPC signature structure detected; cryptographic validity is not verified."));
            if (macros) diagnostics.Add(new("MACRO_PRESENT", "Macro project is preserved but never executed."));
            if (ole) diagnostics.Add(new("OLE_PRESENT", "Embedded/OLE content is preserved but never executed."));
            if (trackedChanges) diagnostics.Add(new("TRACKED_CHANGES_PRESENT", "Tracked changes are preserved; automatic mutation is audit-only."));
            if (externalRelationships) diagnostics.Add(new("EXTERNAL_RELATIONSHIP", "External relationships require guarded handling."));
            if (protectedDocument) diagnostics.Add(new("DOCUMENT_PROTECTED", "Document protection was detected."));

            var policy = unsupported.Count > 0 ? PatchPolicy.PROHIBITED :
                signature || protectedDocument || macros || ole || trackedChanges ? PatchPolicy.AUDIT_ONLY :
                externalRelationships ? PatchPolicy.GUARDED : PatchPolicy.NORMAL;
            return new(true, signature, protectedDocument, macros, trackedChanges, ole, externalRelationships,
                unsupported.OrderBy(x => x, StringComparer.Ordinal).ToArray(), policy, diagnostics);
        }
        catch (OperationCanceledException)
        {
            return Rejected("PROCESSING_CANCELLED", "DOCX processing was cancelled.");
        }
        catch (PreflightFailure failure)
        {
            return Rejected(failure.Code, failure.SafeMessage);
        }
        catch (XmlException exception) when (IsProhibitedDtd(exception))
        {
            return Rejected("UNSAFE_XML", "DOCX XML uses a prohibited DTD or entity declaration.");
        }
        catch (XmlException)
        {
            return Rejected("MALFORMED_DOCX", "A DOCX XML part is malformed.");
        }
        catch (Exception)
        {
            return Rejected("MALFORMED_DOCX", "The DOCX package is malformed or unreadable.");
        }
    }

    internal static PackageSafetyBudget DefaultBudget => PackageSafetyBudget.Default;

    private static PackageSafetyState Rejected(string code, string message) =>
        new(false, false, false, false, false, false, false, [code.ToLowerInvariant()], PatchPolicy.PROHIBITED, [new Diagnostic(code, message)]);

    private static bool ExceedsRatio(long expanded, long compressed, double maximumRatio) =>
        expanded > 0 && (compressed == 0 || expanded / (double)compressed > maximumRatio);

    private static long ReadEntryCountBeforeArchive(FileStream source, long fileLength, PackageSafetyBudget budget, CancellationToken cancellationToken)
    {
        var tailLength = (int)Math.Min(fileLength, EndRecordLength + MaximumZipCommentLength);
        var tail = new byte[tailLength];
        source.Position = fileLength - tailLength;
        source.ReadExactly(tail);
        var firstCandidate = Math.Max(0, tail.Length - (EndRecordLength + MaximumZipCommentLength));
        for (var i = tail.Length - EndRecordLength; i >= firstCandidate; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i, 4)) != EndRecordSignature) continue;
            var commentLength = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 20, 2));
            if (i + EndRecordLength + commentLength != tail.Length) continue;
            var disk = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 4, 2));
            var centralDisk = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 6, 2));
            var diskEntries = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 8, 2));
            var totalEntries = BinaryPrimitives.ReadUInt16LittleEndian(tail.AsSpan(i + 10, 2));
            var centralSize = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i + 12, 4));
            var centralOffset = BinaryPrimitives.ReadUInt32LittleEndian(tail.AsSpan(i + 16, 4));
            if (disk != 0 || centralDisk != 0 || diskEntries != totalEntries || totalEntries == ushort.MaxValue || centralSize == uint.MaxValue || centralOffset == uint.MaxValue)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "Multi-disk and unbounded ZIP64 packages are not supported.");
            if (totalEntries > budget.MaximumEntryCount)
                throw new PreflightFailure("ARCHIVE_ENTRY_LIMIT", "The DOCX package contains too many parts.");
            var eocdOffset = fileLength - tailLength + i;
            if ((long)centralOffset + centralSize > eocdOffset)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "The ZIP central directory has invalid bounds.");
            var actualEntryCount = ValidateCentralDirectory(source, centralOffset, centralSize, eocdOffset, budget, cancellationToken);
            if (actualEntryCount != totalEntries)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "The ZIP central directory has an ambiguous entry count.");
            return actualEntryCount;
        }
        throw new PreflightFailure("MALFORMED_DOCX", "The DOCX package has no valid ZIP directory.");
    }

    private static long ValidateCentralDirectory(
        FileStream source,
        uint centralOffset,
        uint centralSize,
        long endRecordOffset,
        PackageSafetyBudget budget,
        CancellationToken cancellationToken)
    {
        var position = (long)centralOffset;
        var end = checked(position + centralSize);
        if (end != endRecordOffset)
            throw new PreflightFailure("UNSAFE_ARCHIVE", "The ZIP central directory has an ambiguous boundary.");

        var header = new byte[46];
        long count = 0;
        long declaredTotal = 0;
        while (position < end)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (end - position < header.Length)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "The ZIP central directory is truncated.");
            source.Position = position;
            source.ReadExactly(header);
            if (BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(0, 4)) != CentralDirectoryHeaderSignature)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "The ZIP central directory contains an invalid record.");

            var compressedBytes = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(20, 4));
            var uncompressedBytes = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(24, 4));
            var nameBytes = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(28, 2));
            var extraBytes = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(30, 2));
            var commentBytes = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(32, 2));
            var diskNumber = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(34, 2));
            var localHeaderOffset = BinaryPrimitives.ReadUInt32LittleEndian(header.AsSpan(42, 4));
            if (nameBytes == 0 || diskNumber != 0 || compressedBytes == uint.MaxValue ||
                uncompressedBytes == uint.MaxValue || localHeaderOffset == uint.MaxValue)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "ZIP64 or multi-disk entry records are not supported.");

            count++;
            if (count > budget.MaximumEntryCount)
                throw new PreflightFailure("ARCHIVE_ENTRY_LIMIT", "The DOCX package contains too many parts.");
            if (uncompressedBytes > budget.MaximumSingleEntryUncompressedBytes)
                throw new PreflightFailure("ARCHIVE_ENTRY_TOO_LARGE", "A DOCX part exceeds the allowed size.");
            declaredTotal = checked(declaredTotal + uncompressedBytes);
            if (declaredTotal > budget.MaximumTotalUncompressedBytes)
                throw new PreflightFailure("ARCHIVE_EXPANSION_LIMIT", "The DOCX package expands beyond the allowed size.");
            if (ExceedsRatio(uncompressedBytes, compressedBytes, budget.MaximumEntryCompressionRatio))
                throw new PreflightFailure("ARCHIVE_EXPANSION_LIMIT", "A DOCX part exceeds the allowed expansion ratio.");

            var recordLength = (long)header.Length + nameBytes + extraBytes + commentBytes;
            if (recordLength > end - position)
                throw new PreflightFailure("UNSAFE_ARCHIVE", "The ZIP central directory contains an invalid record length.");
            position += recordLength;
        }

        return count;
    }

    private static string NormalizeAndValidateEntryName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 1024 || name[0] == '/' || name.Contains('\\') || name.Any(char.IsControl))
            throw new PreflightFailure("UNSAFE_ARCHIVE_PATH", "The DOCX package contains an unsafe part name.");
        string normalized;
        try { normalized = name.Normalize(NormalizationForm.FormC); }
        catch (ArgumentException) { throw new PreflightFailure("UNSAFE_ARCHIVE_PATH", "The DOCX package contains an invalid part name."); }
        if (!string.Equals(name, normalized, StringComparison.Ordinal))
            throw new PreflightFailure("UNSAFE_ARCHIVE_PATH", "The DOCX package contains a non-canonical part name.");
        var segments = normalized.Split('/');
        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (segment.Length == 0 && i == segments.Length - 1 && normalized.EndsWith('/')) continue;
            if (segment.Length == 0 || segment is "." or ".." || (i == 0 && segment.Length >= 2 && char.IsAsciiLetter(segment[0]) && segment[1] == ':'))
                throw new PreflightFailure("UNSAFE_ARCHIVE_PATH", "The DOCX package contains an unsafe part name.");
        }
        return normalized;
    }

    private static XmlFlags ScanXml(Stream stream, PackageSafetyBudget budget, CancellationToken cancellationToken)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var relationships = new List<RelationshipFlag>();
        var declarations = new List<ContentTypeDeclaration>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = budget.MaximumXmlCharactersPerPart,
            MaxCharactersFromEntities = PackageSafetyLimits.MaximumXmlCharactersFromEntities,
            IgnoreComments = false,
            IgnoreWhitespace = false,
            CloseInput = false,
            Async = false,
        };
        using var reader = XmlReader.Create(stream, settings);
        long nodes = 0;
        while (reader.Read())
        {
            cancellationToken.ThrowIfCancellationRequested();
            nodes++;
            if (nodes > budget.MaximumXmlNodesPerPart || reader.Depth > budget.MaximumXmlDepth)
                throw new PreflightFailure("UNSAFE_XML", "DOCX XML exceeds the allowed structure budget.");
            if (reader.NodeType == XmlNodeType.DocumentType)
                throw new PreflightFailure("UNSAFE_XML", "DOCX XML may not declare a DTD.");
            if (reader.NodeType == XmlNodeType.Element)
            {
                names.Add(reader.LocalName);
                if (reader.LocalName == "Relationship")
                    relationships.Add(new RelationshipFlag(reader.GetAttribute("Type") ?? string.Empty, reader.GetAttribute("TargetMode") ?? string.Empty));
                if (reader.LocalName is "Default" or "Override")
                    declarations.Add(new ContentTypeDeclaration(reader.LocalName,
                        reader.GetAttribute("Extension") ?? string.Empty,
                        reader.GetAttribute("PartName") ?? string.Empty,
                        reader.GetAttribute("ContentType") ?? string.Empty));
            }
        }
        return new XmlFlags(names, relationships, declarations);
    }

    private static ContentTypeMap ReadContentTypes(ZipArchiveEntry entry, PackageSafetyBudget budget, CancellationToken cancellationToken)
    {
        using var raw = entry.Open();
        using var counted = new BoundedEntryReadStream(raw, entry.Length, budget.MaximumSingleEntryUncompressedBytes,
            budget.MaximumXmlCharactersPerPart, () => cancellationToken.ThrowIfCancellationRequested(), _ => { });
        var flags = ScanXml(counted, budget, cancellationToken);
        if (counted.BytesRead != entry.Length)
            throw new PreflightFailure("MALFORMED_DOCX", "The content-type part did not match its declared size.");

        var defaults = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var overrides = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var declaration in flags.ContentTypes)
        {
            if (string.IsNullOrWhiteSpace(declaration.ContentType) || declaration.ContentType.Length > 256)
                throw new PreflightFailure("MALFORMED_DOCX", "The DOCX package has an invalid content-type declaration.");
            if (declaration.Kind == "Default")
            {
                if (string.IsNullOrWhiteSpace(declaration.Extension) || declaration.Extension.Length > 128 ||
                    declaration.Extension.Any(character => character is '/' or '\\' or '.' || char.IsControl(character)) ||
                    !defaults.TryAdd(declaration.Extension, declaration.ContentType))
                    throw new PreflightFailure("UNSAFE_ARCHIVE", "The DOCX package has an ambiguous default content type.");
            }
            else
            {
                if (declaration.PartName.Length < 2 || declaration.PartName[0] != '/')
                    throw new PreflightFailure("MALFORMED_DOCX", "The DOCX package has an invalid part content type.");
                var partName = declaration.PartName[1..];
                _ = NormalizeAndValidateEntryName(partName);
                if (!overrides.TryAdd(partName, declaration.ContentType))
                    throw new PreflightFailure("UNSAFE_ARCHIVE", "The DOCX package has an ambiguous part content type.");
            }
        }
        return new ContentTypeMap(defaults, overrides);
    }

    private static bool IsProhibitedDtd(XmlException exception) =>
        exception.Message.Contains("DTD is prohibited", StringComparison.OrdinalIgnoreCase) ||
        exception.Message.Contains("DTD processing is prohibited", StringComparison.OrdinalIgnoreCase);

    private static void Drain(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[64 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (stream.Read(buffer, 0, buffer.Length) == 0) return;
        }
    }

    private sealed record XmlFlags(HashSet<string> Names, IReadOnlyList<RelationshipFlag> Relationships, IReadOnlyList<ContentTypeDeclaration> ContentTypes)
    {
        public bool Contains(string value) => Names.Contains(value);
        public bool ContainsAny(params string[] values) => values.Any(Names.Contains);
    }

    private sealed record RelationshipFlag(string Type, string TargetMode);
    private sealed record ContentTypeDeclaration(string Kind, string Extension, string PartName, string ContentType);

    private sealed record ContentTypeMap(IReadOnlyDictionary<string, string> Defaults, IReadOnlyDictionary<string, string> Overrides)
    {
        public bool IsXmlPart(string name)
        {
            var contentType = Overrides.GetValueOrDefault(name);
            if (contentType is null)
            {
                var extensionStart = name.LastIndexOf('.');
                if (extensionStart < 0 || extensionStart == name.Length - 1) return false;
                Defaults.TryGetValue(name[(extensionStart + 1)..], out contentType);
            }
            return contentType is not null && (contentType.Equals("application/xml", StringComparison.OrdinalIgnoreCase) ||
                contentType.Equals("text/xml", StringComparison.OrdinalIgnoreCase) || contentType.EndsWith("+xml", StringComparison.OrdinalIgnoreCase));
        }
    }

    private sealed class PreflightFailure(string code, string safeMessage) : Exception
    {
        public string Code { get; } = code;
        public string SafeMessage { get; } = safeMessage;
    }

    private sealed class BoundedEntryReadStream(
        Stream inner,
        long expectedLength,
        long maximumEntryBytes,
        long maximumXmlRemainder,
        Action checkCancellation,
        Action<int> onRead) : Stream
    {
        public long BytesRead { get; private set; }
        public override bool CanRead => inner.CanRead;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override int Read(byte[] buffer, int offset, int count)
        {
            checkCancellation();
            var read = inner.Read(buffer, offset, count);
            Account(read);
            return read;
        }
        public override int Read(Span<byte> buffer)
        {
            checkCancellation();
            var read = inner.Read(buffer);
            Account(read);
            return read;
        }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            checkCancellation();
            return ReadAndAccountAsync(buffer, cancellationToken);
        }
        private async ValueTask<int> ReadAndAccountAsync(Memory<byte> buffer, CancellationToken cancellationToken)
        {
            var read = await inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            Account(read);
            return read;
        }
        private void Account(int read)
        {
            if (read <= 0) return;
            BytesRead = checked(BytesRead + read);
            if (BytesRead > maximumEntryBytes) throw new PreflightFailure("ARCHIVE_ENTRY_TOO_LARGE", "A DOCX part exceeds the allowed size.");
            if (BytesRead > expectedLength) throw new PreflightFailure("UNSAFE_ARCHIVE", "A DOCX part exceeded its declared size.");
            if (BytesRead > maximumXmlRemainder) throw new PreflightFailure("UNSAFE_XML", "DOCX XML exceeds the allowed processing budget.");
            onRead(read);
        }
        protected override void Dispose(bool disposing) { if (disposing) inner.Dispose(); base.Dispose(disposing); }
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
