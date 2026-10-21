using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Parsing;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Model;
using Xunit;

namespace Nd30.LegalValidator.Tests;

public sealed class PartyOoxmlSourceCoverageTests
{
    private static string Create(params W.OpenXmlElement[] elements)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hd05-v25-{Guid.NewGuid():N}.docx");
        using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var body = new W.Body();
            var paragraph = new W.Paragraph();
            foreach (var e in elements) paragraph.Append(e);
            body.Append(paragraph);
            body.Append(new W.SectionProperties());
            main.Document = new W.Document(body);
            main.Document.Save();
        }
        return path;
    }

    private static W.Run TextRun(string content) =>
        new(new W.RunProperties(new W.FontSize { Val = "28" }), new W.Text(content));

    private static (SafeDocxSnapshot snapshot, DocumentModel model) ParseFile(string path)
    {
        using var input = File.OpenRead(path);
        var snapshot = SafeDocxSnapshot.CreateAsync(input).GetAwaiter().GetResult();
        var parsed = new DocxParser().Parse(snapshot);
        Assert.NotNull(parsed.Document);
        return (snapshot, parsed.Document!);
    }

    [Fact]
    public void Plain_direct_runs_are_confirmed_without_docx_modification()
    {
        var path = Create(TextRun("ĐẢNG "), TextRun("CỘNG SẢN VIỆT NAM"));
        try
        {
            var before = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)));
            var (snapshot, model) = ParseFile(path);
            using (snapshot)
            {
                var coverage = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, model, model.BodyBlocks[0], model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.Consistent, coverage.Outcome);
                Assert.Equal("DIRECT_INLINE_SOURCE_RUNS_CONFIRMED", coverage.Reason);
                Assert.Equal(2, coverage.XmlDirectRunCount);
                Assert.True(coverage.RequiresHumanReview);
                var ledger = PartySourceScopedEvidenceSession.Open(model, new DateOnly(2026, 10, 21));
                var accepted = ledger.ObserveSourceCoveredWholeBodyParagraph(snapshot,
                    "heading", model.BodyBlocks[0], model.Paragraphs[0],
                    ObservedRunProperty.FontSizePt);
                Assert.Equal(ValidationStatus.NEEDS_REVIEW, accepted.Status);
                Assert.Equal(1, ledger.Snapshot().EvidenceRecordCount);
                Assert.Equal(PatchPolicy.PROHIBITED, ledger.Snapshot().DocumentPatchPolicy);
                Assert.Equal(before, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))));
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Hyperlink_nested_run_is_not_silently_ignored()
    {
        var path = Create(TextRun("A"), new W.Hyperlink(TextRun("18pt inside hyperlink")));
        try
        {
            var (snapshot, model) = ParseFile(path);
            using (snapshot)
            {
                Assert.Single(model.Paragraphs[0].Runs); // Existing P0-C only sees direct run.
                var check = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, model, model.BodyBlocks[0], model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.NotEvaluated, check.Outcome);
                Assert.Equal("UNSUPPORTED_NESTED_OR_INLINE_CONTAINER", check.Reason);
                var ledger = PartySourceScopedEvidenceSession.Open(model, new DateOnly(2026, 10, 21));
                var denied = ledger.ObserveSourceCoveredWholeBodyParagraph(snapshot,
                    "font", model.BodyBlocks[0], model.Paragraphs[0],
                    ObservedRunProperty.FontSizePt);
                Assert.Equal(ValidationStatus.NOT_EVALUATED, denied.Status);
                Assert.Equal("UNSUPPORTED_NESTED_OR_INLINE_CONTAINER", denied.Reason);
                Assert.Empty(ledger.Snapshot().ObservedTargetKeys);
                Assert.Equal(0, ledger.Snapshot().EvidenceRecordCount);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Content_control_nested_run_is_not_silently_ignored()
    {
        var path = Create(TextRun("A"), new W.SdtRun(
            new W.SdtProperties(), new W.SdtContentRun(TextRun("inside SDT"))));
        try
        {
            var (snapshot, model) = ParseFile(path);
            using (snapshot)
            {
                var result = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, model, model.BodyBlocks[0], model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.Equal("UNSUPPORTED_NESTED_OR_INLINE_CONTAINER", result.Reason);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Simple_field_is_not_dropped_from_source_coverage()
    {
        var path = Create(TextRun("A"), new W.SimpleField(TextRun("PAGE")) { Instruction = "PAGE" });
        try
        {
            var (snapshot, model) = ParseFile(path);
            using (snapshot)
            {
                var result = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, model, model.BodyBlocks[0], model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.Equal("UNSUPPORTED_NESTED_OR_INLINE_CONTAINER", result.Reason);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Unsupported_break_content_fails_closed()
    {
        var path = Create(TextRun("A"), new W.Run(new W.Break()));
        try
        {
            var (snapshot, model) = ParseFile(path);
            using (snapshot)
            {
                var result = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, model, model.BodyBlocks[0], model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.Equal("UNSUPPORTED_RUN_CONTENT", result.Reason);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Missing_or_foreign_source_ownership_fails_closed()
    {
        var path = Create(TextRun("A"));
        try
        {
            var (snapshot, model) = ParseFile(path);
            using (snapshot)
            {
                var fake = model.BodyBlocks[0] with { };
                var result = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, model, fake, model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.Equal("SOURCE_BLOCK_OWNERSHIP_NOT_CONFIRMED", result.Reason);
                var wrong = model with { Paragraphs = [] };
                var mismatch = new PartyParagraphSourceCoverageProbe().Observe(
                    snapshot, wrong, model.BodyBlocks[0], model.Paragraphs[0]);
                Assert.Equal(ObservationOutcome.NotEvaluated, mismatch.Outcome);
            }
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Read_only_snapshot_must_remain_available()
    {
        var path = Create(TextRun("A"));
        try
        {
            var (snapshot, model) = ParseFile(path);
            snapshot.Dispose();
            var result = new PartyParagraphSourceCoverageProbe().Observe(
                snapshot, model, model.BodyBlocks[0], model.Paragraphs[0]);
            Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
            Assert.Equal("SOURCE_SNAPSHOT_UNAVAILABLE", result.Reason);
        }
        finally { File.Delete(path); }
    }
}
