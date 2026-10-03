using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Processing;
using Nd30.LegalValidator.Profiles;
using Xunit;

namespace Nd30.LegalValidator.Tests;

/// <summary>
/// Serving-flow regressions for the profile formatting apply path, against a real
/// DOCX through <see cref="DocumentProcessorService"/>. These cover the boundary
/// the executor cannot see: the mandatory inspection binding, the exact
/// expected-before value, the rule-scoped target restriction, and the non-noop
/// output revalidation.
/// </summary>
public sealed class FormattingApplyServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fmt-svc-" + Guid.NewGuid().ToString("N"));
    public FormattingApplyServiceTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { /* best effort */ } }

    private string P(string name) => Path.Combine(_directory, name);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "release", "admin-nd30-verified-rc-v20.yaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Verified release pack not found.");
    }

    private static DocumentProcessorService Service()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        return new DocumentProcessorService(catalog, new DateOnly(2026, 9, 19));
    }

    private static byte[] Bytes(string path) => System.IO.File.ReadAllBytes(path);
    private static string Sha(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static string RealNamedDecision() =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", "02-named-decision.docx"));

    private static FormattingApplyRequest Request(byte[] source, DocumentInspectionResult inspection, string property, string targetId, string? expectedBefore, string desiredAfter)
    {
        var target = inspection.MutableTargets!.First(t => t.Property == property && t.TargetId == targetId);
        return new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, property, targetId,
            expectedBefore ?? target.CurrentValue, desiredAfter,
            inspection.Binding.Id, inspection.Binding.Digest, target.RuleId);
    }

    // ---------- happy path ----------

    [Fact]
    public async Task Non_noop_apply_produces_a_new_document_and_keeps_the_source_unchanged()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var before = target.CurrentValue;

        var result = await service.ApplyFormattingAsync(
            new MemoryStream(source, writable: false),
            Request(source, inspection, "section.margin_right_mm", target.TargetId, before, "17"),
            CancellationToken.None);

        Assert.True(result.Reopened);
        Assert.True(result.Revalidated);
        Assert.True(result.SourceUnchanged);
        Assert.Equal(Sha(source), result.SourceSha256);
        Assert.Equal("17", result.After);
        Assert.Equal(before, result.Before);
        Assert.NotEqual(result.SourceSha256, result.OutputSha256);
        Assert.NotEmpty(result.OutputBytes);
        // The bytes on disk are never what the caller handed in.
        Assert.NotEqual(source, result.OutputBytes);

        // Revalidating the produced document with the same profile sees the change.
        var revalidated = await service.InspectAsync(new MemoryStream(result.OutputBytes, writable: false), CancellationToken.None);
        Assert.Equal(result.OutputSha256, revalidated.SourceSha256);
        Assert.Equal("17", revalidated.MutableTargets!.First(t => t.Property == "section.margin_right_mm").CurrentValue);
        // Unrelated section properties survive.
        Assert.Equal("20", revalidated.MutableTargets!.First(t => t.Property == "section.margin_top_mm").CurrentValue);
        Assert.Equal("30", revalidated.MutableTargets!.First(t => t.Property == "section.margin_left_mm").CurrentValue);
        Assert.Equal("A4", revalidated.MutableTargets!.First(t => t.Property == "section.page_size").CurrentValue);
        Assert.False(revalidated.FullComplianceClaimAllowed);
    }

    [Fact]
    public async Task Apply_is_bound_to_the_document_type_it_was_inspected_as()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        Assert.Equal("quyet_dinh", inspection.Binding!.DocumentTypeKey);

        var result = await service.ApplyFormattingAsync(
            new MemoryStream(source, writable: false),
            Request(source, inspection, "section.margin_right_mm", "s1", "15", "17"),
            CancellationToken.None);

        // The applied artifact is reported against the same profile binding.
        Assert.Equal(inspection.Binding.Id, result.Binding.Id);
        Assert.Equal(inspection.Binding.Digest, result.Binding.Digest);
        Assert.Equal("ND30.PL1.I.GENERAL.MARGIN_RIGHT", result.RuleId);
    }

    // ---------- mandatory bindings ----------

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-digest")]
    public async Task Apply_without_a_well_formed_profile_binding_is_refused(string? digest)
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var request = new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, "section.margin_right_mm", target.TargetId, "15", "17",
            inspection.Binding!.Id, digest!, target.RuleId);

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));

        Assert.Equal("PROFILE_CHANGED", error.Code);
    }

    [Fact]
    public async Task Apply_under_a_stale_profile_digest_is_refused()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var request = new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, "section.margin_right_mm", target.TargetId, "15", "17",
            inspection.Binding!.Id, new string('a', 64), target.RuleId);

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));

        Assert.Equal("PROFILE_CHANGED", error.Code);
    }

    [Fact]
    public async Task Apply_without_an_exact_expected_before_value_is_refused()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var request = new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, "section.margin_right_mm", target.TargetId, string.Empty, "17",
            inspection.Binding!.Id, inspection.Binding.Digest, target.RuleId);

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));

        Assert.Equal("PRECONDITION_FAILED", error.Code);
    }

    [Fact]
    public async Task Apply_with_a_wrong_expected_before_value_is_refused()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false),
                Request(source, inspection, "section.margin_right_mm", target.TargetId, "18", "17"), CancellationToken.None));

        Assert.Equal("PRECONDITION_FAILED", error.Code);
    }

    [Fact]
    public async Task Apply_of_a_stale_source_digest_is_refused()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var request = Request(source, inspection, "section.margin_right_mm", target.TargetId, "15", "17") with
        {
            SourceSha256 = new string('b', 64),
        };

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));

        Assert.Equal("STALE_DOCUMENT", error.Code);
    }

    // ---------- rule-scoped targets ----------

    /// <summary>
    /// A body rule must not be applicable to a type heading or a signature block
    /// merely because those paragraphs also carry direct formatting.
    /// </summary>
    [Fact]
    public async Task A_body_rule_is_not_offered_against_a_structurally_classified_paragraph()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);

        var parser = new Nd30.DocumentEngine.Parsing.DocxParser();
        var parsed = parser.Parse(RealNamedDecision());
        Assert.NotNull(parsed.Document);
        var semantic = new Nd30.SemanticDetector.Detection.AdministrativeSemanticDetector().Detect(parsed.Document!);

        var structuralIds = semantic.Components
            .Where(c => c.Role is Nd30.SemanticDetector.Model.SemanticRole.DocumentTypeHeading
                or Nd30.SemanticDetector.Model.SemanticRole.SubjectNamedDocument
                or Nd30.SemanticDetector.Model.SemanticRole.LegalBasisBlock
                or Nd30.SemanticDetector.Model.SemanticRole.SignerName
                or Nd30.SemanticDetector.Model.SemanticRole.IssuingAuthority)
            .Select(c => c.Evidence.ParagraphId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(structuralIds);

        // No paragraph-scoped body target may point at a structural paragraph.
        var paragraphScoped = inspection.MutableTargets!.Where(t => !t.Property.StartsWith("section.", StringComparison.Ordinal) && !t.Property.StartsWith("document.", StringComparison.Ordinal)).ToArray();
        foreach (var target in paragraphScoped)
            Assert.DoesNotContain(target.TargetId, structuralIds);

        // And asking for one is refused at the serving boundary, whether or not the
        // body property was observable in this document.
        var heading = structuralIds.First();
        var request = new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, "run.font_size_pt", heading, "14", "13",
            inspection.Binding!.Id, inspection.Binding.Digest, "ND30.PL1.I.II.6E.BODY_SIZE");
        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));
        Assert.Equal("TARGET_NOT_FOUND", error.Code);
    }

    // ---------- desired value legality ----------

    [Theory]
    [InlineData("40")]   // above the rule's 15-20 mm range
    [InlineData("5")]    // below the rule's range
    [InlineData("abc")]  // not numeric at all
    public async Task A_desired_value_the_bound_rule_rejects_is_refused(string desiredAfter)
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false),
                Request(source, inspection, "section.margin_right_mm", target.TargetId, "15", desiredAfter), CancellationToken.None));

        Assert.Equal("UNSUPPORTED_OPERATION", error.Code);
    }

    [Fact]
    public async Task A_desired_value_inside_the_rule_range_is_accepted()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");

        var result = await service.ApplyFormattingAsync(
            new MemoryStream(source, writable: false),
            Request(source, inspection, "section.margin_right_mm", target.TargetId, "15", "18.5"), CancellationToken.None);

        Assert.Equal("18.5", result.After);
    }

    [Fact]
    public async Task A_property_outside_the_profile_is_refused()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var request = new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, "paragraph.line_spacing_points", "p1", "14", "20",
            inspection.Binding!.Id, inspection.Binding.Digest, "ND30.PL1.I.II.6E.BODY_SIZE");

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));

        Assert.Equal("UNSUPPORTED_PROPERTY", error.Code);
    }

    [Fact]
    public async Task A_property_paired_with_the_wrong_rule_is_refused()
    {
        var service = Service();
        var source = Bytes(RealNamedDecision());
        var inspection = await service.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var request = new FormattingApplyRequest(
            inspection.SourceSha256, inspection.Binding!.DocumentTypeKey, "section.margin_right_mm", "s1", "15", "17",
            inspection.Binding!.Id, inspection.Binding.Digest, "ND30.PL1.I.II.6E.BODY_SIZE");

        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            service.ApplyFormattingAsync(new MemoryStream(source, writable: false), request, CancellationToken.None));

        Assert.Equal("PROFILE_TUPLE_MISMATCH", error.Code);
    }
}