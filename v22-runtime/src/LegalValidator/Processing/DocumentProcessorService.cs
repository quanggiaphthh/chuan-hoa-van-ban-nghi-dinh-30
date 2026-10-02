using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Package;
using Nd30.DocumentEngine.Parsing;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Mutation;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;

namespace Nd30.LegalValidator.Processing;

public sealed record ParagraphAlignmentInspection(string ParagraphId, string Text, string? DirectAlignment, string? IssueId);
public sealed record DocumentInspectionResult(
    string SourceSha256,
    string Profile,
    bool SafeToMutate,
    string PackagePolicy,
    IReadOnlyList<ParagraphAlignmentInspection> Paragraphs,
    bool ParagraphsTruncated,
    IReadOnlyList<Diagnostic> Diagnostics);
public sealed record DocumentApplyResult(
    string SourceSha256,
    string OutputSha256,
    string ParagraphId,
    string Before,
    string After,
    byte[] OutputBytes,
    bool Reopened,
    bool Revalidated,
    bool SourceUnchanged,
    string Property);

/// <summary>
/// Minimal stream boundary for the extraction spike. It exposes one existing,
/// source-tested mutation family and makes no legal-compliance assertion.
/// </summary>
public sealed class DocumentProcessorService
{
    private static readonly XNamespace WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public const string Profile = "generic-direct-alignment-spike";
    public const long MaximumOutputBytes = PackageSafetyLimits.MaximumCompressedInputBytes;
    public static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _processingTimeout;
    private const int MaximumInspectionParagraphs = 250;
    private const int MaximumDisplayedParagraphCharacters = 500;

    public DocumentProcessorService() : this(ProcessingTimeout) { }

    internal DocumentProcessorService(TimeSpan processingTimeout)
    {
        if (processingTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(processingTimeout));
        _processingTimeout = processingTimeout;
    }

    public async Task<DocumentInspectionResult> InspectAsync(Stream input, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_processingTimeout);
        try
        {
            await using var snapshot = await SafeDocxSnapshot.CreateAsync(input, deadline.Token).ConfigureAwait(false);
            var parsed = new DocxParser().Parse(snapshot, deadline.Token);
            if (parsed.Document is null)
            {
                var diagnostic = parsed.Diagnostics.FirstOrDefault();
                throw new DocumentProcessorException(diagnostic?.Code ?? "MALFORMED_DOCX", diagnostic?.Message ?? "The DOCX package could not be parsed.");
            }
            var document = parsed.Document;
            var safeToMutate = document.Safety.PatchPolicy == PatchPolicy.NORMAL && document.Safety.UnsupportedFeatures.Count == 0;
            var paragraphs = document.Paragraphs.Take(MaximumInspectionParagraphs).Select(paragraph =>
            {
                var alignment = CanonicalAlignment(paragraph.DirectFormatting.Alignment);
                var text = string.Concat(paragraph.Runs.Select(run => run.Text));
                var issueId = safeToMutate && alignment is not null
                    ? StableId(string.Join("|", snapshot.Sha256, paragraph.Id, "paragraph.alignment", alignment))
                    : null;
                return new ParagraphAlignmentInspection(paragraph.Id, Clip(text), alignment, issueId);
            }).ToArray();
            snapshot.EnsureUnchanged();
            return new DocumentInspectionResult(snapshot.Sha256, Profile, safeToMutate,
                document.Safety.PatchPolicy.ToString(), paragraphs,
                document.Paragraphs.Count > paragraphs.Length,
                parsed.Diagnostics.Take(32).ToArray());
        }
        catch (DocumentPackageException ex) { throw Translate(ex, cancellationToken, deadline.Token); }
        catch (OperationCanceledException) { throw TimeoutOrCancellation(cancellationToken, deadline.Token); }
        catch (DocumentProcessorException) { throw; }
        catch (Exception) { throw new DocumentProcessorException("PROCESSING_FAILED", "DOCX processing failed safely."); }
    }

    public async Task<DocumentApplyResult> ApplyAlignmentAsync(
        Stream input,
        string expectedSourceSha256,
        string paragraphId,
        string expectedBefore,
        string desiredAfter,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_processingTimeout);
        string? outputPath = null;
        try
        {
            await using var source = await SafeDocxSnapshot.CreateAsync(input, deadline.Token).ConfigureAwait(false);
            if (!string.Equals(source.Sha256, expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("STALE_DOCUMENT", "The DOCX source changed after inspection.");
            if (source.Safety.PatchPolicy != PatchPolicy.NORMAL || source.Safety.UnsupportedFeatures.Count != 0)
                throw new DocumentProcessorException("PACKAGE_NOT_MUTABLE", "The DOCX package is not eligible for this mutation.");

            var parsed = new DocxParser().Parse(source, deadline.Token);
            if (parsed.Document is null)
                throw new DocumentProcessorException("MALFORMED_DOCX", "The DOCX package could not be parsed.");
            if (!TryIndex(paragraphId, out var index) || index > parsed.Document.Paragraphs.Count)
                throw new DocumentProcessorException("TARGET_NOT_FOUND", "The selected paragraph is no longer available.");
            if (!TryAlignment(expectedBefore, out var before) || !TryAlignment(desiredAfter, out var after) || before == after)
                throw new DocumentProcessorException("UNSUPPORTED_OPERATION", "The requested direct alignment change is not supported.");

            var paragraph = parsed.Document.Paragraphs[index - 1];
            var actualBefore = CanonicalAlignment(paragraph.DirectFormatting.Alignment);
            if (actualBefore is null)
                throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "Only an explicit direct paragraph alignment can be changed.");
            if (!string.Equals(actualBefore, before.ToString(), StringComparison.Ordinal))
                throw new DocumentProcessorException("PRECONDITION_FAILED", "The selected paragraph no longer has the inspected alignment.");

            var originalOtherParts = PackagePreserver.PartSha256Inventory(source.StagedPath, deadline.Token)
                .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var originalDocumentStructure = DocumentStructureFingerprint(source.StagedPath, paragraphId);

            var documentIdentity = DocumentIdentityService.FromFile(source.StagedPath);
            var ruleIdentity = new RuleIdentity("TECHNICAL.USER_CONFIRMED_ALIGNMENT", "USER_CONFIRMATION", "explicit direct formatting operation");
            var binding = new DocumentStateBinding(documentIdentity, ruleIdentity);
            var findingReference = StableId(string.Join("|", documentIdentity.Digest, paragraph.Id, before, after));
            var proposalId = StableId(string.Join("|", "proposal", findingReference));
            var evidence = new[] { StableId(string.Join("|", "confirmed-operation", findingReference)) };
            var proposal = new RemediationProposal(proposalId, ruleIdentity.RuleId, findingReference, evidence,
                paragraph.Id, "paragraph_alignment", before.ToString(), after.ToString(),
                RemediationDecision.ELIGIBLE, true, "Exact user-confirmed technical operation.", binding);
            var target = new ParagraphTargetLocator("/word/document.xml", "paragraph", paragraph.Id,
                $"body/p[{index}]", ParagraphAlignmentMutationExecutor.Anchor(string.Concat(paragraph.Runs.Select(run => run.Text))),
                "paragraph.alignment");
            var intent = MutationIntent.Create(proposal, target, before, after);
            var authorization = AuthorizationArtifact.Issue(proposal, intent, "agent-workspace-confirmation-gateway");
            var authorized = AuthorizationBoundary.Authorize(proposal, source.Safety, authorization,
                documentIdentity, ruleIdentity, intent);
            if (!authorized.Authorized || authorized.Request is null)
                throw new DocumentProcessorException("AUTHORIZATION_REJECTED", "The exact alignment operation was not authorized.");

            outputPath = Path.Combine(Path.GetDirectoryName(source.StagedPath)!, "output.docx");
            var result = new ParagraphAlignmentMutationExecutor().Execute(source.StagedPath, outputPath, authorized.Request, deadline.Token);
            if (!result.Success)
            {
                if (result.Outcome == MutationOutcome.PROCESSING_CANCELLED)
                    throw TimeoutOrCancellation(cancellationToken, deadline.Token);
                throw new DocumentProcessorException(ToErrorCode(result.Outcome), SafeMutationMessage(result.Outcome));
            }

            source.EnsureUnchanged();
            var bytes = await ReadBoundedOutputAsync(outputPath, deadline.Token).ConfigureAwait(false);
            await using var reopenedSnapshot = await SafeDocxSnapshot.CreateAsync(new MemoryStream(bytes, writable: false), deadline.Token).ConfigureAwait(false);
            var reopened = new DocxParser().Parse(reopenedSnapshot, deadline.Token);
            if (reopened.Document is null || reopenedSnapshot.Safety.PatchPolicy != PatchPolicy.NORMAL)
                throw new DocumentProcessorException("OUTPUT_INTEGRITY_FAILED", "The generated DOCX did not pass canonical revalidation.");
            var outputOtherParts = PackagePreserver.PartSha256Inventory(outputPath, deadline.Token)
                .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (!originalOtherParts.SequenceEqual(outputOtherParts) ||
                DocumentStructureFingerprint(outputPath, paragraphId) != originalDocumentStructure)
                throw new DocumentProcessorException("OUTPUT_INTEGRITY_FAILED", "Unrelated DOCX package structures changed during processing.");
            var actualAfter = index <= reopened.Document.Paragraphs.Count
                ? CanonicalAlignment(reopened.Document.Paragraphs[index - 1].DirectFormatting.Alignment)
                : null;
            if (!string.Equals(actualAfter, after.ToString(), StringComparison.Ordinal))
                throw new DocumentProcessorException("POSTCONDITION_FAILED", "The requested alignment was not present after reopening.");
            source.EnsureUnchanged();
            return new DocumentApplyResult(source.Sha256, reopenedSnapshot.Sha256, paragraphId,
                before.ToString(), after.ToString(), bytes, true, true, true, "paragraph.alignment");
        }
        catch (DocumentPackageException ex) { throw Translate(ex, cancellationToken, deadline.Token); }
        catch (OperationCanceledException) { throw TimeoutOrCancellation(cancellationToken, deadline.Token); }
        catch (DocumentProcessorException) { throw; }
        catch (Exception) { throw new DocumentProcessorException("PROCESSING_FAILED", "DOCX processing failed safely."); }
        finally
        {
            if (outputPath is not null)
            {
                try { if (File.Exists(outputPath)) File.Delete(outputPath); }
                catch (IOException) { throw new DocumentProcessorException("TEMP_CLEANUP_FAILED", "Temporary DOCX output could not be removed."); }
                catch (UnauthorizedAccessException) { throw new DocumentProcessorException("TEMP_CLEANUP_FAILED", "Temporary DOCX output could not be removed."); }
            }
        }
    }

    private static async Task<byte[]> ReadBoundedOutputAsync(string path, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length > MaximumOutputBytes) throw new DocumentProcessorException("OUTPUT_TOO_LARGE", "The generated DOCX exceeds the allowed output size.");
        using var output = new MemoryStream((int)input.Length);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > MaximumOutputBytes) throw new DocumentProcessorException("OUTPUT_TOO_LARGE", "The generated DOCX exceeds the allowed output size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return output.ToArray();
    }

    private static string DocumentStructureFingerprint(string path, string targetParagraphId)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ??
            throw new DocumentProcessorException("MALFORMED_DOCX", "The DOCX document part is missing.");
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = PackageSafetyLimits.MaximumXmlCharactersPerPart,
            MaxCharactersFromEntities = PackageSafetyLimits.MaximumXmlCharactersFromEntities,
            CloseInput = false,
        };
        using var part = entry.Open();
        using var reader = XmlReader.Create(part, settings);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var body = document.Root?.Element(WordprocessingNamespace + "body");
        var paragraphs = body?.Elements(WordprocessingNamespace + "p").ToList() ?? [];
        if (!TryIndex(targetParagraphId, out var index) || index > paragraphs.Count)
            throw new DocumentProcessorException("TARGET_NOT_FOUND", "The selected paragraph is no longer available.");

        var target = paragraphs[index - 1];
        var paragraphProperties = target.Elements(WordprocessingNamespace + "pPr").ToList();
        if (paragraphProperties.Count != 1)
            throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "The selected paragraph has ambiguous direct properties.");
        var directAlignments = paragraphProperties[0].Elements(WordprocessingNamespace + "jc").ToList();
        if (directAlignments.Count != 1)
            throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "The selected paragraph has ambiguous direct alignment.");
        directAlignments[0].Remove();

        var canonical = new StringBuilder();
        if (document.Root is not null) AppendCanonicalXml(document.Root, canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void AppendCanonicalXml(XElement element, StringBuilder target)
    {
        target.Append('E');
        AppendCanonicalToken(element.Name.NamespaceName, target);
        AppendCanonicalToken(element.Name.LocalName, target);
        foreach (var attribute in element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration)
            .OrderBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal)
            .ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal))
        {
            target.Append('A');
            AppendCanonicalToken(attribute.Name.NamespaceName, target);
            AppendCanonicalToken(attribute.Name.LocalName, target);
            AppendCanonicalToken(attribute.Value, target);
        }
        target.Append('>');
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    AppendCanonicalXml(child, target);
                    break;
                case XText text:
                    target.Append('T');
                    AppendCanonicalToken(text.Value, target);
                    break;
                case XComment comment:
                    target.Append('C');
                    AppendCanonicalToken(comment.Value, target);
                    break;
                case XProcessingInstruction instruction:
                    target.Append('P');
                    AppendCanonicalToken(instruction.Target, target);
                    AppendCanonicalToken(instruction.Data, target);
                    break;
            }
        }
        target.Append("/E");
        AppendCanonicalToken(element.Name.NamespaceName, target);
        AppendCanonicalToken(element.Name.LocalName, target);
    }

    private static void AppendCanonicalToken(string value, StringBuilder target) =>
        target.Append(value.Length).Append(':').Append(value);

    private static string? CanonicalAlignment(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "LEFT" => "LEFT", "CENTER" => "CENTER", "RIGHT" => "RIGHT", "BOTH" or "JUSTIFY" => "JUSTIFY", _ => null,
    };

    private static bool TryAlignment(string value, out ParagraphAlignmentValue alignment) =>
        Enum.TryParse(value, ignoreCase: true, out alignment) && Enum.IsDefined(alignment);

    private static bool TryIndex(string paragraphId, out int index)
    {
        index = 0;
        return paragraphId.Length is > 1 and <= 10 && paragraphId[0] == 'p' &&
            int.TryParse(paragraphId.AsSpan(1), out index) && index > 0;
    }

    private static string Clip(string text) => text.Length <= MaximumDisplayedParagraphCharacters ? text : text[..MaximumDisplayedParagraphCharacters];
    private static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
    private static string ToErrorCode(MutationOutcome outcome) => outcome switch
    {
        MutationOutcome.STALE_DOCUMENT => "STALE_DOCUMENT",
        MutationOutcome.AUTHORIZATION_REJECTED => "AUTHORIZATION_REJECTED",
        MutationOutcome.PACKAGE_NOT_MUTABLE => "PACKAGE_NOT_MUTABLE",
        MutationOutcome.TARGET_NOT_FOUND => "TARGET_NOT_FOUND",
        MutationOutcome.TARGET_AMBIGUOUS => "TARGET_AMBIGUOUS",
        MutationOutcome.PROVENANCE_NOT_DIRECT => "PROVENANCE_NOT_DIRECT",
        MutationOutcome.PRECONDITION_FAILED => "PRECONDITION_FAILED",
        MutationOutcome.UNSUPPORTED_OPERATION => "UNSUPPORTED_OPERATION",
        MutationOutcome.OUTPUT_PATH_INVALID => "OUTPUT_PATH_INVALID",
        MutationOutcome.OUTPUT_ALREADY_EXISTS => "OUTPUT_ALREADY_EXISTS",
        MutationOutcome.OUTPUT_TOO_LARGE => "OUTPUT_TOO_LARGE",
        MutationOutcome.OUTPUT_INTEGRITY_FAILED => "OUTPUT_INTEGRITY_FAILED",
        MutationOutcome.SOURCE_IMMUTABILITY_VIOLATION => "SOURCE_IMMUTABILITY_VIOLATION",
        MutationOutcome.PROCESSING_CANCELLED => "PROCESSING_CANCELLED",
        _ => "PROCESSING_FAILED",
    };
    private static string SafeMutationMessage(MutationOutcome outcome) => outcome switch
    {
        MutationOutcome.STALE_DOCUMENT => "The DOCX source changed after inspection.",
        MutationOutcome.AUTHORIZATION_REJECTED => "The exact alignment operation was not authorized.",
        MutationOutcome.PACKAGE_NOT_MUTABLE => "The DOCX package is not eligible for this mutation.",
        MutationOutcome.TARGET_NOT_FOUND or MutationOutcome.TARGET_AMBIGUOUS => "The selected paragraph is not uniquely available.",
        MutationOutcome.PROVENANCE_NOT_DIRECT => "Only explicit direct paragraph alignment can be changed.",
        MutationOutcome.PRECONDITION_FAILED => "The selected paragraph no longer has the inspected alignment.",
        MutationOutcome.OUTPUT_INTEGRITY_FAILED => "The generated DOCX failed output integrity checks.",
        MutationOutcome.SOURCE_IMMUTABILITY_VIOLATION => "The source DOCX changed during processing.",
        _ => "The confirmed alignment operation could not be completed.",
    };

    private static DocumentProcessorException Translate(DocumentPackageException error, CancellationToken caller, CancellationToken deadline) =>
        error.Code == "PROCESSING_CANCELLED" ? TimeoutOrCancellation(caller, deadline) : new(error.Code, error.SafeMessage);

    private static DocumentProcessorException TimeoutOrCancellation(CancellationToken caller, CancellationToken deadline) => caller.IsCancellationRequested
        ? new("PROCESSING_CANCELLED", "DOCX processing was cancelled.")
        : deadline.IsCancellationRequested
            ? new("PROCESSING_TIMEOUT", "DOCX processing exceeded its time limit.")
            : new("PROCESSING_CANCELLED", "DOCX processing was cancelled.");
}

public sealed class DocumentProcessorException(string code, string safeMessage) : Exception(safeMessage)
{
    public string Code { get; } = code;
    public string SafeMessage { get; } = safeMessage;
}
