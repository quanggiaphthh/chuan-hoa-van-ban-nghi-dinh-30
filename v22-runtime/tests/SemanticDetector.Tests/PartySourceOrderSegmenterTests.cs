using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Parsing;
using Nd30.SemanticDetector.Detection;
using Xunit;

namespace Nd30.SemanticDetector.Tests;

public sealed class PartySourceOrderSegmenterTests
{
    private static W.Paragraph P(string text) => new(new W.Run(new W.Text(text)));

    private static W.Table T(string text) =>
        new(new W.TableProperties(),
            new W.TableGrid(new W.GridColumn { Width = "2400" }),
            new W.TableRow(new W.TableCell(P(text))));

    private static DocumentModel D(params OpenXmlElement[] children)
    {
        var file = Path.Combine(Path.GetTempPath(), $"party-segment-{Guid.NewGuid():N}.docx");
        try
        {
            using (var package = WordprocessingDocument.Create(file, WordprocessingDocumentType.Document))
            {
                var main = package.AddMainDocumentPart();
                var body = new W.Body();
                foreach (var child in children) body.Append(child);
                body.Append(new W.SectionProperties());
                main.Document = new W.Document(body);
                main.Document.Save();
            }

            var parsed = new DocxParser().Parse(file);
            Assert.NotNull(parsed.Document);
            return parsed.Document!;
        }
        finally
        {
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Fact]
    public void Explicit_party_boundaries_preserve_owner_and_unclassified_tables()
    {
        var d = D(
            P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("BAN CHẤP HÀNH ĐẢNG BỘ"),
            P("Số: 01-QĐ/TU"),
            P("QUYẾT ĐỊNH"),
            P("Về một nội dung"),
            P("Căn cứ Điều lệ Đảng;"),
            P("Điều 1. Quy định trách nhiệm"),
            T("Bảng trong nội dung không được tự gán vùng"),
            P("Điều 2. Tổ chức thực hiện"),
            P("Nơi nhận:"),
            P("T/M BAN THƯỜNG VỤ"),
            P("PHỤ LỤC 1"),
            P("Danh sách kèm theo"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.Equal(d.BodyBlocks.Count, x.Blocks.Count);
        Assert.True(x.RequiresReview); // Candidate-only, even where explicit anchors exist.
        Assert.Equal(new[]
        {
            PartyCandidateRegion.Front, PartyCandidateRegion.Front, PartyCandidateRegion.Front,
            PartyCandidateRegion.Front, PartyCandidateRegion.Front,
            PartyCandidateRegion.Body, PartyCandidateRegion.Body,
            PartyCandidateRegion.Unclassified, PartyCandidateRegion.Body,
            PartyCandidateRegion.End, PartyCandidateRegion.End,
            PartyCandidateRegion.Appendix, PartyCandidateRegion.Appendix
        }, x.Blocks.Select(b => b.Region));
        Assert.Equal(d.BodyBlocks.Select(b => b.SourcePath), x.Blocks.Select(b => b.Source.SourcePath));
        Assert.Single(x.Blocks, b => b.Source.Kind == "table");
        Assert.All(x.Blocks.Where(b => b.Source.Kind == "table"),
            b => Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
        Assert.Equal(x.Blocks.Count, x.Blocks.Select(b => b.Source.SourcePath).Distinct().Count());
    }

    [Fact]
    public void Table_cell_party_heading_is_evidence_but_table_stays_unclassified()
    {
        var d = D(
            T("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("QUYẾT ĐỊNH"),
            P("Điều 1. Quy định nội dung"),
            P("Nơi nhận:"),
            P("PHỤ LỤC 1"),
            P("Danh sách"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.True(x.RequiresReview);
        Assert.Equal(new[]
        {
            PartyCandidateRegion.Unclassified, PartyCandidateRegion.Front,
            PartyCandidateRegion.Body, PartyCandidateRegion.End,
            PartyCandidateRegion.Appendix, PartyCandidateRegion.Appendix
        }, x.Blocks.Select(b => b.Region));
        Assert.Contains("PARTY_HEADING_IN_TABLE_UNCLASSIFIED", x.Diagnostics);
        Assert.Single(x.AppendixBoundaries);
        Assert.Equal(5, x.AppendixBoundaries[0].SourceOrder);
        Assert.Equal("table", x.Blocks[0].Source.Kind);
        Assert.Equal(d.BodyBlocks.Select(b => b.SourcePath), x.Blocks.Select(b => b.Source.SourcePath));
    }

    [Fact]
    public void Table_heading_without_end_anchor_cannot_claim_table_ownership()
    {
        var d = D(T("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("QUYẾT ĐỊNH"), P("Điều 1. Nội dung"), P("PHỤ LỤC 1"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.True(x.RequiresReview);
        Assert.All(x.Blocks, b => Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
        Assert.Contains("END_START_NOT_CONFIRMED", x.Diagnostics);
        Assert.Empty(x.AppendixBoundaries);
    }

    [Fact]
    public void Conflicting_state_and_party_headers_require_review()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM"),
            P("Điều 1. Nội dung"), P("Nơi nhận:"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.Contains("CONFLICTING_STATE_AND_PARTY_HEADINGS", x.Diagnostics);
        Assert.All(x.Blocks, b => Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
    }

    [Fact]
    public void Multiple_appendices_have_separate_ordered_boundaries_without_claiming_tables()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("QUYẾT ĐỊNH"), P("Điều 1. Nội dung"),
            P("Nơi nhận:"), P("TM. BAN THƯỜNG VỤ"),
            P("PHỤ LỤC 1"), P("Danh sách số một"),
            P("PHỤ LỤC 2"), T("Bảng kèm phụ lục hai"), P("Danh sách số hai"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.True(x.RequiresReview);
        Assert.Equal(new[] { 1, 2 }, x.AppendixBoundaries.Select(a => a.Ordinal));
        Assert.Equal(new[] { 6, 8 }, x.AppendixBoundaries.Select(a => a.SourceOrder));
        Assert.Equal(new[] { "PHỤ LỤC 1", "PHỤ LỤC 2" }, x.AppendixBoundaries.Select(a => a.Heading));
        Assert.Equal(PartyCandidateRegion.End, x.Blocks[4].Region);
        Assert.Equal(PartyCandidateRegion.Appendix, x.Blocks[5].Region);
        Assert.Equal(PartyCandidateRegion.Appendix, x.Blocks[7].Region);
        Assert.Equal(PartyCandidateRegion.Unclassified, x.Blocks[8].Region);
        Assert.Single(x.SigningBoundaries);
        Assert.Equal(5, x.SigningBoundaries[0].SourceOrder);
        Assert.DoesNotContain("REPEATED_APPENDIX_HEADING_REQUIRES_REVIEW", x.Diagnostics);
    }

    [Fact]
    public void Repeated_appendix_heading_is_not_silently_trusted()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("QUYẾT ĐỊNH"), P("Điều 1. Nội dung"),
            P("Nơi nhận:"), P("PHỤ LỤC 1"), P("Bảng A"),
            P("PHỤ LỤC 1"), P("Bảng B"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.Equal(2, x.AppendixBoundaries.Count);
        Assert.Contains("REPEATED_APPENDIX_HEADING_REQUIRES_REVIEW", x.Diagnostics);
        Assert.True(x.RequiresReview);
    }

    [Fact]
    public void Multiple_signature_prefixes_are_evidence_not_signer_identity()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("QUYẾT ĐỊNH"), P("Điều 1. Nội dung"),
            P("K/T CHÁNH VĂN PHÒNG"), P("TM. BAN THƯỜNG VỤ"),
            P("PHỤ LỤC I"), P("Danh sách"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.Equal(new[] { 4, 5 }, x.SigningBoundaries.Select(b => b.SourceOrder));
        Assert.All(x.SigningBoundaries, b => Assert.False(string.IsNullOrWhiteSpace(b.SourcePath)));
        Assert.Contains("MULTIPLE_SIGNING_PREFIXES_REQUIRE_REVIEW", x.Diagnostics);
        Assert.Single(x.AppendixBoundaries);
        Assert.All(x.Blocks.Skip(3).Take(2), b => Assert.Equal(PartyCandidateRegion.End, b.Region));
        Assert.Equal(PartyCandidateRegion.Appendix, x.Blocks[5].Region);
        Assert.True(x.RequiresReview);
    }

    [Fact]
    public void State_headings_must_not_be_misclassified_as_party()
    {
        var d = D(
            P("CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM"),
            P("Độc lập - Tự do - Hạnh phúc"),
            P("QUYẾT ĐỊNH"),
            P("Điều 1. Nội dung"),
            P("Nơi nhận:"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.True(x.RequiresReview);
        Assert.All(x.Blocks, b => Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
        Assert.Contains("PARTY_HEADING_MISSING_AMBIGUOUS_OR_MISPLACED", x.Diagnostics);
    }

    [Fact]
    public void Missing_end_boundary_cannot_silently_swallow_signature_or_appendix()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("THÔNG BÁO"), P("Căn cứ công việc thực tế"),
            P("Nội dung"), P("PHỤ LỤC 1"), P("Danh sách"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.True(x.RequiresReview);
        Assert.Equal(PartyCandidateRegion.Front, x.Blocks[0].Region);
        Assert.All(x.Blocks.Skip(1),
            b => Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
        Assert.Contains("END_START_NOT_CONFIRMED", x.Diagnostics);
    }

    [Fact]
    public void Missing_body_start_must_not_invent_body_region()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("BÁO CÁO"),
            P("Nội dung không có mốc phân vùng"), P("Nơi nhận:"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.True(x.RequiresReview);
        Assert.DoesNotContain(x.Blocks, b => b.Region == PartyCandidateRegion.Body);
        Assert.Contains("BODY_START_NOT_CONFIRMED", x.Diagnostics);
    }

    [Fact]
    public void Invalid_or_unavailable_body_provenance_is_fail_closed()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("Căn cứ quy định"),
            P("Nơi nhận:"));
        var segmenter = new PartySourceOrderSegmenter();
        var empty = segmenter.Segment(d with { BodyBlocks = [] });
        Assert.Empty(empty.Blocks);
        Assert.Contains("ORDERED_BODY_BLOCKS_UNAVAILABLE", empty.Diagnostics);

        var wrong = d with
        {
            BodyBlocks = d.BodyBlocks
                .Select((b, i) => i == 1 ? b with { Order = 999 } : b)
                .ToArray()
        };
        var result = segmenter.Segment(wrong);
        Assert.True(result.RequiresReview);
        Assert.All(result.Blocks,
            b => Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
        Assert.Contains("INVALID_SOURCE_ORDER_PROVENANCE", result.Diagnostics);
    }

    [Fact]
    public void Duplicated_model_owner_or_source_path_requires_manual_review()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"),
            P("Căn cứ quy định"), P("Nơi nhận:"));

        var duplicateModel = d with
        {
            BodyBlocks = d.BodyBlocks.Select((b, i) =>
                i == 1 ? b with { ModelId = "p1" } : b).ToArray()
        };
        var duplicatePath = d with
        {
            BodyBlocks = d.BodyBlocks.Select((b, i) =>
                i == 1 ? b with { SourcePath = d.BodyBlocks[0].SourcePath } : b).ToArray()
        };
        foreach (var input in new[] { duplicateModel, duplicatePath })
        {
            var result = new PartySourceOrderSegmenter().Segment(input);
            Assert.True(result.RequiresReview);
            Assert.Contains("DUPLICATE_BODY_BLOCK_PROVENANCE", result.Diagnostics);
            Assert.All(result.Blocks, b =>
                Assert.Equal(PartyCandidateRegion.Unclassified, b.Region));
        }
    }

    [Fact]
    public void Unsupported_container_remains_unclassified_and_requires_review()
    {
        var d = D(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("QUYẾT ĐỊNH"),
            P("Điều 1. Nội dung"),
            new W.SdtBlock(new W.SdtProperties(),
                new W.SdtContentBlock(P("Chữ nằm trong SDT không được tự nhận là nội dung"))),
            P("Nơi nhận:"));
        var x = new PartySourceOrderSegmenter().Segment(d);
        Assert.Equal(new[]
        {
            PartyCandidateRegion.Front, PartyCandidateRegion.Front,
            PartyCandidateRegion.Body, PartyCandidateRegion.Unclassified,
            PartyCandidateRegion.End
        }, x.Blocks.Select(b => b.Region));
        Assert.Equal("unclassified", x.Blocks[3].Source.Kind);
        Assert.Contains(x.Diagnostics, s => s.StartsWith("UNCLASSIFIED_BODY_CONTAINER:", StringComparison.Ordinal));
    }
}
