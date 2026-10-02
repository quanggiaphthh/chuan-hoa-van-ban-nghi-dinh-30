using System.IO.Compression;
using System.Buffers.Binary;
using System.Text;
using Nd30.DocumentEngine.Package;
using Nd30.DocumentEngine.Safety;
using Xunit;

namespace Nd30.DocumentEngine.Tests;

public sealed class PackageSafetyPreflightTests
{
    private const string ContentTypes = "<?xml version=\"1.0\"?><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/></Types>";
    private const string RootRelationships = "<?xml version=\"1.0\"?><Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"word/document.xml\"/></Relationships>";

    [Fact]
    public void Accepts_minimal_and_normal_docx()
    {
        using var temp = new TempDirectory();
        var minimal = CreateDocx(temp.File("minimal.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        var normal = Fixture("01-simple.docx");

        Assert.True(PackageSafetyPreflight.Inspect(minimal).IsReadable);
        Assert.True(PackageSafetyPreflight.Inspect(normal).IsReadable);
    }

    [Fact]
    public void Rejects_arbitrary_zip_disguised_as_docx_before_document_parsing()
    {
        using var temp = new TempDirectory();
        var path = temp.File("renamed.docx");
        using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            Add(archive, "ordinary.txt", "This is a ZIP, not an OOXML document.");
        }

        AssertRejected(PackageSafetyPreflight.Inspect(path), "MALFORMED_DOCX");
    }

    [Fact]
    public void Rejects_oversized_compressed_input_before_opening_archive()
    {
        using var temp = new TempDirectory();
        var path = temp.File("large.zip");
        using (var stream = new FileStream(path, FileMode.CreateNew)) stream.SetLength(2048);
        var state = PackageSafetyPreflight.Inspect(path, Budget(maxCompressedInputBytes: 1024));
        AssertRejected(state, "INPUT_TOO_LARGE");
    }

    [Fact]
    public void Rejects_archive_expansion_and_oversized_entry()
    {
        using var temp = new TempDirectory();
        var highRatio = CreateDocx(temp.File("ratio.docx"), $"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p>{new string('A', 80_000)}</w:p></w:body></w:document>");
        AssertRejected(PackageSafetyPreflight.Inspect(highRatio, Budget(maxCompressionRatio: 8)), "ARCHIVE_EXPANSION_LIMIT");

        var entry = CreateDocx(temp.File("entry.docx"), $"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p>{new string('x', 2_000)}</w:p></w:body></w:document>");
        AssertRejected(PackageSafetyPreflight.Inspect(entry, Budget(maxEntryUncompressedBytes: 1_000)), "ARCHIVE_ENTRY_TOO_LARGE");
    }

    [Fact]
    public void Rejects_total_expansion_and_entry_count()
    {
        using var temp = new TempDirectory();
        var total = CreateDocx(temp.File("total.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>", extraEntries: ["extra-a.bin", "extra-b.bin"]);
        AssertRejected(PackageSafetyPreflight.Inspect(total, Budget(maxTotalUncompressedBytes: 128)), "ARCHIVE_EXPANSION_LIMIT");
        AssertRejected(PackageSafetyPreflight.Inspect(total, Budget(maxEntryCount: 4)), "ARCHIVE_ENTRY_LIMIT");
    }

    [Fact]
    public void Rejects_actual_central_directory_entry_count_even_when_eocd_count_is_spoofed_low()
    {
        using var temp = new TempDirectory();
        var extras = Enumerable.Range(0, PackageSafetyLimits.MaximumEntryCount)
            .Select(index => $"extra-{index}.bin").ToArray();
        var path = CreateDocx(temp.File("spoofed-count.docx"),
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>",
            extraEntries: extras);
        SpoofEndRecordCount(path, 1);

        AssertRejected(PackageSafetyPreflight.Inspect(path), "ARCHIVE_ENTRY_LIMIT");
    }

    [Theory]
    [InlineData("../escape.bin")]
    [InlineData("/absolute.bin")]
    [InlineData("C:/absolute.bin")]
    [InlineData("folder\\escape.bin")]
    public void Rejects_unsafe_archive_paths(string entryName)
    {
        using var temp = new TempDirectory();
        var path = CreateDocx(temp.File("unsafe-path.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>", extraEntries: [entryName]);
        AssertRejected(PackageSafetyPreflight.Inspect(path), "UNSAFE_ARCHIVE_PATH");
    }

    [Fact]
    public void Rejects_duplicate_normalized_entry_names()
    {
        using var temp = new TempDirectory();
        var path = CreateDocx(temp.File("duplicate.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>", duplicateDocument: true);
        AssertRejected(PackageSafetyPreflight.Inspect(path), "UNSAFE_ARCHIVE");
    }

    [Fact]
    public void Rejects_malformed_zip_and_malformed_xml_without_raw_exception_details()
    {
        using var temp = new TempDirectory();
        var zip = temp.File("malformed.zip");
        File.WriteAllText(zip, "not a zip");
        var malformedXml = CreateDocx(temp.File("malformed-xml.docx"), "<w:document><w:body></w:document>");

        var zipState = PackageSafetyPreflight.Inspect(zip);
        var xmlState = PackageSafetyPreflight.Inspect(malformedXml);
        AssertRejected(zipState, "MALFORMED_DOCX");
        AssertRejected(xmlState, "MALFORMED_DOCX");
        Assert.DoesNotContain(zipState.Diagnostics, d => d.Message.Contains("/", StringComparison.Ordinal));
        Assert.DoesNotContain(xmlState.Diagnostics, d => d.Message.Contains("Line ", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Prohibits_dtd_and_xxe_before_external_resolution()
    {
        using var temp = new TempDirectory();
        var secret = temp.File("secret.txt");
        File.WriteAllText(secret, "must-not-be-read");
        var escaped = System.Security.SecurityElement.Escape(secret) ?? secret;
        var xml = $"<!DOCTYPE w:document [<!ENTITY xxe SYSTEM \"file://{escaped}\">]><w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body><w:p>&xxe;</w:p></w:body></w:document>";
        var path = CreateDocx(temp.File("xxe.docx"), xml);

        AssertRejected(PackageSafetyPreflight.Inspect(path), "UNSAFE_XML");
        Assert.Equal("must-not-be-read", File.ReadAllText(secret));
    }

    [Fact]
    public void Scans_xml_parts_declared_with_a_non_xml_filename_extension()
    {
        using var temp = new TempDirectory();
        var path = CreateDocx(temp.File("hidden-xml.docx"),
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>",
            extraEntries: ["word/payload.data"],
            contentTypeOverrides: "<Override PartName=\"/word/payload.data\" ContentType=\"application/xml\"/>",
            extraEntryContents: new Dictionary<string, string> { ["word/payload.data"] = "<!DOCTYPE a><a/>" });

        AssertRejected(PackageSafetyPreflight.Inspect(path), "UNSAFE_XML");
    }

    [Fact]
    public void Rejects_oversized_xml_and_excessive_depth()
    {
        using var temp = new TempDirectory();
        var large = CreateDocx(temp.File("large-xml.docx"), $"<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>{new string(' ', 2_000)}</w:body></w:document>");
        var deep = CreateDocx(temp.File("deep-xml.docx"), "<a>" + string.Concat(Enumerable.Repeat("<b>", 12)) + "x" + string.Concat(Enumerable.Repeat("</b>", 12)) + "</a>");

        AssertRejected(PackageSafetyPreflight.Inspect(large, Budget(maxXmlPartCharacters: 256)), "UNSAFE_XML");
        AssertRejected(PackageSafetyPreflight.Inspect(deep, Budget(maxXmlDepth: 8)), "UNSAFE_XML");
    }

    [Fact]
    public void Rejects_aggregate_xml_budget_across_individually_bounded_parts()
    {
        using var temp = new TempDirectory();
        var path = CreateDocx(temp.File("aggregate-xml.docx"),
            "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");

        AssertRejected(PackageSafetyPreflight.Inspect(path, Budget(maxTotalXmlBytes: 480)), "UNSAFE_XML");
    }

    [Fact]
    public void Rejects_excessive_xml_nodes_without_excessive_depth()
    {
        using var temp = new TempDirectory();
        var xml = "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body>" +
            string.Concat(Enumerable.Repeat("<w:p/>", 100)) + "</w:body></w:document>";
        var path = CreateDocx(temp.File("many-nodes.docx"), xml);

        AssertRejected(PackageSafetyPreflight.Inspect(path, Budget(maxXmlNodesPerPart: 64)), "UNSAFE_XML");
    }

    [Fact]
    public void Cancellation_fails_closed_with_stable_code()
    {
        using var temp = new TempDirectory();
        var path = CreateDocx(temp.File("cancel.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        AssertRejected(PackageSafetyPreflight.Inspect(path, PackageSafetyPreflight.DefaultBudget, cancellation.Token), "PROCESSING_CANCELLED");
    }

    [Fact]
    public void Part_hash_inventory_observes_cancellation()
    {
        using var temp = new TempDirectory();
        var path = CreateDocx(temp.File("cancel-inventory.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => PackagePreserver.PartSha256Inventory(path, cancellation.Token));
    }

    [Fact]
    public async Task Snapshot_cleanup_runs_after_success_and_failure()
    {
        using var temp = new TempDirectory();
        var valid = CreateDocx(temp.File("valid.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        var stagingRoot = temp.File("snapshots");
        Directory.CreateDirectory(stagingRoot);
        string stagedPath;
        await using (var input = File.OpenRead(valid))
        await using (var snapshot = await SafeDocxSnapshot.CreateAsync(input, CancellationToken.None, stagingRoot))
        {
            Assert.True(snapshot.Safety.IsReadable);
            stagedPath = snapshot.StagedPath;
            Assert.True(File.Exists(stagedPath));
        }
        Assert.False(File.Exists(stagedPath));

        var invalid = temp.File("invalid.docx");
        File.WriteAllText(invalid, "not a zip");
        await using var invalidInput = File.OpenRead(invalid);
        await Assert.ThrowsAsync<DocumentPackageException>(() => SafeDocxSnapshot.CreateAsync(invalidInput, CancellationToken.None, stagingRoot));
        Assert.Empty(Directory.GetFileSystemEntries(stagingRoot));
    }

    [Fact]
    public async Task Snapshot_reports_cleanup_failure_without_leaking_internal_path()
    {
        using var temp = new TempDirectory();
        var valid = CreateDocx(temp.File("valid.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        var stagingRoot = temp.File("cleanup-failure");
        Directory.CreateDirectory(stagingRoot);
        await using var input = File.OpenRead(valid);
        var snapshot = await SafeDocxSnapshot.CreateAsync(input, CancellationToken.None, stagingRoot,
            PackageSafetyPreflight.DefaultBudget, _ => false);
        var stagedDirectory = Path.GetDirectoryName(snapshot.StagedPath)!;

        var error = Assert.Throws<DocumentPackageException>(() => snapshot.Dispose());

        Assert.Equal("TEMP_CLEANUP_FAILED", error.Code);
        Assert.DoesNotContain(stagingRoot, error.SafeMessage, StringComparison.OrdinalIgnoreCase);
        Directory.Delete(stagedDirectory, recursive: true);
    }

    [Fact]
    public void Copy_without_mutation_rejects_overwrite_and_keeps_source_unchanged()
    {
        using var temp = new TempDirectory();
        var source = CreateDocx(temp.File("source.docx"), "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\"><w:body/></w:document>");
        var original = File.ReadAllBytes(source);
        var output = temp.File("output.docx");

        PackagePreserver.CopyWithoutMutation(source, output);

        Assert.Equal(original, File.ReadAllBytes(source));
        Assert.Equal(original, File.ReadAllBytes(output));
        var overwrite = Assert.Throws<DocumentPackageException>(() => PackagePreserver.CopyWithoutMutation(source, output));
        Assert.Equal("OUTPUT_ALREADY_EXISTS", overwrite.Code);
        var samePath = Assert.Throws<DocumentPackageException>(() => PackagePreserver.CopyWithoutMutation(source, source));
        Assert.Equal("OUTPUT_PATH_INVALID", samePath.Code);
    }

    private static void AssertRejected(PackageSafetyState state, string code)
    {
        Assert.False(state.IsReadable);
        Assert.Equal(PatchPolicy.PROHIBITED, state.PatchPolicy);
        Assert.Contains(state.Diagnostics, diagnostic => diagnostic.Code == code);
    }

    private static PackageSafetyBudget Budget(
        long? maxCompressedInputBytes = null,
        long? maxTotalUncompressedBytes = null,
        long? maxEntryUncompressedBytes = null,
        int? maxEntryCount = null,
        double? maxCompressionRatio = null,
        long? maxXmlPartCharacters = null,
        int? maxXmlDepth = null,
        long? maxXmlNodesPerPart = null,
        long? maxTotalXmlBytes = null) => new(
            maxCompressedInputBytes ?? 4 * 1024 * 1024,
            maxTotalUncompressedBytes ?? 4 * 1024 * 1024,
            maxEntryUncompressedBytes ?? 2 * 1024 * 1024,
            maxEntryCount ?? 64,
            maxCompressionRatio ?? 200,
            maxXmlPartCharacters ?? 2 * 1024 * 1024,
            maxXmlDepth ?? 64,
            maxXmlNodesPerPart ?? 250_000,
            maxTotalXmlBytes ?? 16 * 1024 * 1024);

    private static string Fixture(string fileName) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/docx", fileName));

    private static string CreateDocx(string path, string documentXml, string[]? extraEntries = null, bool duplicateDocument = false,
        string? contentTypeOverrides = null, IReadOnlyDictionary<string, string>? extraEntryContents = null)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false))
        {
            var types = contentTypeOverrides is null ? ContentTypes : ContentTypes.Replace("</Types>", contentTypeOverrides + "</Types>", StringComparison.Ordinal);
            Add(archive, "[Content_Types].xml", types);
            Add(archive, "_rels/.rels", RootRelationships);
            Add(archive, "word/document.xml", documentXml);
            if (duplicateDocument) Add(archive, "WORD/DOCUMENT.XML", documentXml);
            foreach (var name in extraEntries ?? []) Add(archive, name, extraEntryContents?.GetValueOrDefault(name) ?? "x");
        }
        return path;
    }

    private static void Add(ZipArchive archive, string name, string text)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(text);
    }

    private static void SpoofEndRecordCount(string path, ushort count)
    {
        var bytes = File.ReadAllBytes(path);
        var signature = new byte[] { 0x50, 0x4b, 0x05, 0x06 };
        var offset = bytes.AsSpan().LastIndexOf(signature);
        Assert.True(offset >= 0, "The synthetic ZIP must contain an end record.");
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 8, 2), count);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset + 10, 2), count);
        File.WriteAllBytes(path, bytes);
    }

    private sealed class TempDirectory : IDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), "docx-safety-test-" + Guid.NewGuid().ToString("N"));
        public TempDirectory() => Directory.CreateDirectory(_path);
        public string File(string name) => Path.Combine(_path, name);
        public void Dispose() { try { Directory.Delete(_path, recursive: true); } catch { } }
    }
}
