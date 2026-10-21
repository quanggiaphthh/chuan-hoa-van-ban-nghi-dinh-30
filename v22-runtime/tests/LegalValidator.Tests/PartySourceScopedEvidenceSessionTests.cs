using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Parsing;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Model;
using Xunit;

namespace Nd30.LegalValidator.Tests;

public sealed class PartySourceScopedEvidenceSessionTests
{
    private static string Docx(params string[] texts)
    {
        var path = Path.Combine(Path.GetTempPath(), $"hd05-v24-{Guid.NewGuid():N}.docx");
        using (var doc = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
        {
            var part = doc.AddMainDocumentPart();
            var body = new W.Body();
            foreach (var text in texts)
                body.Append(new W.Paragraph(new W.Run(
                    new W.RunProperties(new W.FontSize { Val = "28" }), new W.Text(text))));
            body.Append(new W.SectionProperties());
            part.Document = new W.Document(body);
            part.Document.Save();
        }
        return path;
    }

    private static DocumentModel Parse(string path)
    {
        var result = new DocxParser().Parse(path);
        Assert.NotNull(result.Document);
        return result.Document!;
    }

    [Fact]
    public void One_rule_at_two_body_sources_has_two_distinct_derived_keys()
    {
        var path = Docx("ĐẢNG CỘNG SẢN VIỆT NAM", "Căn cứ");
        try
        {
            var d = Parse(path);
            var s = PartySourceScopedEvidenceSession.Open(d, new DateOnly(2026, 10, 21));
            var a = s.ObserveWholeBodyParagraph("bodyfont", d.BodyBlocks[0], d.Paragraphs[0], ObservedRunProperty.FontSizePt);
            var b = s.ObserveWholeBodyParagraph("bodyfont", d.BodyBlocks[1], d.Paragraphs[1], ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, a.Status);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, b.Status);
            Assert.NotEqual(a.TargetKey, b.TargetKey);
            Assert.Equal("/w:document/w:body/*[2]", b.SourcePath);
            Assert.Equal(2, s.Snapshot().ObservedTargetKeys.Count);
            Assert.Equal(2, s.Snapshot().EvidenceRecordCount);
            Assert.True(s.Snapshot().RequiresHumanReview);
            Assert.Equal(PatchPolicy.PROHIBITED, s.Snapshot().DocumentPatchPolicy);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Same_rule_same_source_same_property_is_blocked_without_overwrite()
    {
        var path = Docx("A");
        try
        {
            var d = Parse(path);
            var s = PartySourceScopedEvidenceSession.Open(d, new DateOnly(2026, 10, 21));
            var a = s.ObserveWholeBodyParagraph("same", d.BodyBlocks[0], d.Paragraphs[0], ObservedRunProperty.FontSizePt);
            var b = s.ObserveWholeBodyParagraph("same", d.BodyBlocks[0], d.Paragraphs[0], ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, a.Status);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, b.Status);
            Assert.Equal("DUPLICATE_CANDIDATE_TARGET", b.Reason);
            Assert.Equal(1, s.Snapshot().EvidenceRecordCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Foreign_document_source_or_paragraph_is_never_owned()
    {
        var first = Docx("A");
        var second = Docx("B");
        try
        {
            var a = Parse(first);
            var b = Parse(second);
            var s = PartySourceScopedEvidenceSession.Open(a, new DateOnly(2026, 10, 21));
            var x = s.ObserveWholeBodyParagraph("foreign", b.BodyBlocks[0], b.Paragraphs[0], ObservedRunProperty.FontSizePt);
            var y = s.ObserveWholeBodyParagraph("foreign", a.BodyBlocks[0], b.Paragraphs[0], ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, x.Status);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, y.Status);
            Assert.Equal("SOURCE_BLOCK_OWNERSHIP_NOT_CONFIRMED", y.Reason);
            Assert.Empty(s.Snapshot().ObservedTargetKeys);
        }
        finally { File.Delete(first); File.Delete(second); }
    }

    [Fact]
    public void Cloned_source_reference_cannot_impersonate_real_block()
    {
        var path = Docx("A");
        try
        {
            var d = Parse(path);
            var s = PartySourceScopedEvidenceSession.Open(d, new DateOnly(2026, 10, 21));
            var forged = d.BodyBlocks[0] with { };
            var result = s.ObserveWholeBodyParagraph("forged", forged, d.Paragraphs[0], ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, result.Status);
            Assert.Equal("SOURCE_BLOCK_OWNERSHIP_NOT_CONFIRMED", result.Reason);
            Assert.Empty(s.Snapshot().ObservedTargetKeys);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Missing_source_manifest_or_wrong_paragraph_fails_closed()
    {
        var path = Docx("A", "B");
        try
        {
            var d = Parse(path);
            var s = PartySourceScopedEvidenceSession.Open(d, new DateOnly(2026, 10, 21));
            var wrong = s.ObserveWholeBodyParagraph("wrong", d.BodyBlocks[0], d.Paragraphs[1], ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, wrong.Status);
            var missing = d with { BodyBlocks = [] };
            var other = PartySourceScopedEvidenceSession.Open(missing, new DateOnly(2026, 10, 21));
            var unknown = other.ObserveWholeBodyParagraph("missing", d.BodyBlocks[0], missing.Paragraphs[0], ObservedRunProperty.FontSizePt);
            Assert.Equal("SOURCE_BLOCK_MANIFEST_UNVERIFIED", unknown.Reason);
            Assert.Empty(other.Snapshot().ObservedTargetKeys);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Malformed_candidate_id_cannot_inject_field_path()
    {
        var path = Docx("A");
        try
        {
            var d = Parse(path);
            var s = PartySourceScopedEvidenceSession.Open(d, new DateOnly(2026, 10, 21));
            Assert.Throws<ArgumentException>(() => s.ObserveWholeBodyParagraph(
                "party_candidate.injected", d.BodyBlocks[0], d.Paragraphs[0], ObservedRunProperty.FontSizePt));
            Assert.Empty(s.Snapshot().Observations);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Snapshot_is_immutable_and_prior_copy_does_not_change()
    {
        var path = Docx("A");
        try
        {
            var d = Parse(path);
            var s = PartySourceScopedEvidenceSession.Open(d, new DateOnly(2026, 10, 21));
            var before = s.Snapshot();
            s.ObserveWholeBodyParagraph("one", d.BodyBlocks[0], d.Paragraphs[0], ObservedRunProperty.FontSizePt);
            Assert.Empty(before.Observations);
            var after = s.Snapshot();
            Assert.Single(after.Observations);
            Assert.Throws<NotSupportedException>(() =>
                ((IList<PartyScopedEvidenceObservation>)after.Observations).Clear());
        }
        finally { File.Delete(path); }
    }
}
