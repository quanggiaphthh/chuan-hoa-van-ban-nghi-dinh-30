using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Parsing;
using Nd30.DocumentEngine.Model;
using Nd30.SemanticDetector.Detection;
using Xunit;

namespace Nd30.SemanticDetector.Tests;

public sealed class PartyAppendixSourcePartitionerTests
{
    private static W.Paragraph P(string text) => new(new W.Run(new W.Text(text)));
    private static W.Table T(string text) => new(
        new W.TableProperties(),
        new W.TableGrid(new W.GridColumn { Width = "2400" }),
        new W.TableRow(new W.TableCell(P(text))));

    private static DocumentModel Document(params OpenXmlElement[] children)
    {
        var file = Path.Combine(Path.GetTempPath(), $"party-v26-{Guid.NewGuid():N}.docx");
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
            var result = new DocxParser().Parse(file);
            Assert.NotNull(result.Document);
            return result.Document!;
        }
        finally { if (File.Exists(file)) File.Delete(file); }
    }

    private static DocumentModel TwoAppendices() => Document(
        P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("QUYẾT ĐỊNH"),
        P("Điều 1. Nội dung chính"), T("Bảng thuộc phần thân"),
        P("Nơi nhận:"), P("TM. BAN THƯỜNG VỤ"),
        P("PHỤ LỤC I"), P("Danh mục thứ nhất"),
        T("Bảng thứ nhất"), P("PHỤ LỤC II"),
        T("Bảng thứ hai"), P("Dữ liệu cuối"));

    [Fact]
    public void Two_appendices_partition_all_source_blocks_once_and_keep_tables_unclassified()
    {
        var d = TwoAppendices();
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.True(x.SourcePartitionComplete);
        Assert.True(x.RequiresHumanReview);
        Assert.Equal(2, x.Appendices.Count);
        Assert.Equal(new[] { 7, 10 }, x.Appendices.Select(a => a.HeadingSourceOrder));
        Assert.Equal(6, x.BeforeAppendices.Count);
        Assert.Equal(new[] { 7, 8, 9 }, x.Appendices[0].SourceEntries.Select(b => b.Source.Order));
        Assert.Equal(new[] { 10, 11, 12 }, x.Appendices[1].SourceEntries.Select(b => b.Source.Order));
        Assert.Equal(PartyAppendixBlockDisposition.TableUnclassified, x.Appendices[0].SourceEntries[2].Disposition);
        Assert.Equal(PartyAppendixBlockDisposition.TableUnclassified, x.Appendices[1].SourceEntries[1].Disposition);
        Assert.Equal(new[] { 1, 2, 3, 4, 5, 6 },
            x.BeforeAppendices.Select(b => b.Order));
        var source = x.BeforeAppendices.Select(b => b.SourcePath)
            .Concat(x.Appendices.SelectMany(a => a.SourceEntries.Select(e => e.Source.SourcePath))).ToArray();
        Assert.Equal(d.BodyBlocks.Select(b => b.SourcePath), source);
        Assert.Equal(d.BodyBlocks.Count, source.Distinct().Count());
        Assert.Contains(x.Diagnostics, a => a.StartsWith("UNCLASSIFIED_APPENDIX_SOURCE:", StringComparison.Ordinal));
    }

    [Fact]
    public void Unsupported_sdt_inside_appendix_is_preserved_as_unknown_source_block()
    {
        var d = Document(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("QUYẾT ĐỊNH"),
            P("Điều 1. Nội dung"), P("Nơi nhận:"),
            P("PHỤ LỤC 1"),
            new W.SdtBlock(new W.SdtProperties(),
                new W.SdtContentBlock(P("Không tự nhận là nội dung"))),
            P("Dòng cuối"));
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.True(x.SourcePartitionComplete);
        var appendix = Assert.Single(x.Appendices);
        Assert.Equal(new[] { 5, 6, 7 }, appendix.SourceEntries.Select(s => s.Source.Order));
        Assert.Equal(PartyAppendixBlockDisposition.UnsupportedContainer,
            appendix.SourceEntries[1].Disposition);
        Assert.Contains(x.Diagnostics, s => s.StartsWith("UNCLASSIFIED_APPENDIX_SOURCE:", StringComparison.Ordinal));
    }

    [Fact]
    public void Repeated_headings_refuse_ambiguous_partition_instead_of_merging_them()
    {
        var d = Document(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("QUYẾT ĐỊNH"),
            P("Điều 1. Nội dung"), P("Nơi nhận:"),
            P("PHỤ LỤC 1"), P("A"), P("PHỤ LỤC 1"), P("B"));
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.False(x.SourcePartitionComplete);
        Assert.Empty(x.Appendices);
        Assert.Contains("AMBIGUOUS_REPEATED_APPENDIX_HEADING", x.Diagnostics);
    }

    [Fact]
    public void Appendix_heading_inside_table_refuses_silent_source_swallowing()
    {
        var d = Document(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("QUYẾT ĐỊNH"),
            P("Điều 1. Nội dung"), P("Nơi nhận:"),
            P("PHỤ LỤC 1"), P("Danh sách"),
            T("PHỤ LỤC 2"), P("Dòng từ bảng thứ hai"));
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.False(x.SourcePartitionComplete);
        Assert.Empty(x.Appendices);
        Assert.Contains("APPENDIX_HEADING_INSIDE_TABLE_UNRESOLVED", x.Diagnostics);
    }

    [Fact]
    public void Missing_body_or_end_does_not_invent_appendix_ownership()
    {
        var d = Document(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("THÔNG BÁO"),
            P("Điều 1. Nội dung"), P("PHỤ LỤC 1"), P("Danh sách"));
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.False(x.SourcePartitionComplete);
        Assert.Empty(x.Appendices);
        Assert.Contains("BODY_OR_END_BOUNDARY_NOT_CONFIRMED", x.Diagnostics);
    }

    [Fact]
    public void Duplicated_model_owner_or_bad_order_is_rejected()
    {
        var d = TwoAppendices();
        var duplicate = d with { BodyBlocks = d.BodyBlocks.Select((b, i) =>
            i == 1 ? b with { ModelId = "p1" } : b).ToArray() };
        var reordered = d with { BodyBlocks = d.BodyBlocks.Select((b, i) =>
            i == 1 ? b with { Order = 17 } : b).ToArray() };
        foreach (var input in new[] { duplicate, reordered })
        {
            var x = new PartyAppendixSourcePartitioner().Project(input);
            Assert.False(x.SourcePartitionComplete);
            Assert.Empty(x.Appendices);
            Assert.Contains("APPENDIX_SOURCE_MANIFEST_UNVERIFIED", x.Diagnostics);
        }
    }

    [Fact]
    public void Non_appendix_document_keeps_all_source_blocks_outside_appendices()
    {
        var d = Document(P("ĐẢNG CỘNG SẢN VIỆT NAM"), P("QUYẾT ĐỊNH"),
            P("Điều 1. Nội dung"), T("Bảng đính kèm chính"),
            P("Nơi nhận:"), P("TM. BAN THƯỜNG VỤ"));
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.True(x.SourcePartitionComplete);
        Assert.Empty(x.Appendices);
        Assert.Equal(d.BodyBlocks.Select(b => b.SourcePath),
            x.BeforeAppendices.Select(b => b.SourcePath));
        Assert.Contains("NO_EXPLICIT_APPENDIX_BOUNDARIES", x.Diagnostics);
    }

    [Fact]
    public void Table_in_body_remains_before_appendices_and_never_moves_to_appendix()
    {
        var d = TwoAppendices();
        var x = new PartyAppendixSourcePartitioner().Project(d);
        Assert.Equal("table", x.BeforeAppendices[3].Kind);
        Assert.Equal(4, x.BeforeAppendices[3].Order);
        Assert.DoesNotContain(x.Appendices.SelectMany(a => a.SourceEntries),
            e => e.Source.Order == 4);
        Assert.Equal("Nơi nhận:", string.Concat(d.Paragraphs[3].Runs.Select(r => r.Text)));
    }
    [Fact]
    public void Orphan_paragraph_in_model_is_rejected_even_when_manifest_looks_valid()
    {
        var d = TwoAppendices();
        var orphan = d.Paragraphs[0] with { Id = "p999" };
        var corrupted = d with { Paragraphs = d.Paragraphs.Append(orphan).ToArray() };
        var x = new PartyAppendixSourcePartitioner().Project(corrupted);
        Assert.False(x.SourcePartitionComplete);
        Assert.Empty(x.Appendices);
        Assert.Contains("APPENDIX_SOURCE_MANIFEST_UNVERIFIED", x.Diagnostics);
    }

    [Fact]
    public void Orphan_table_in_model_is_rejected_even_when_manifest_looks_valid()
    {
        var d = TwoAppendices();
        var orphan = d.Tables[0] with { Id = "t999" };
        var corrupted = d with { Tables = d.Tables.Append(orphan).ToArray() };
        var x = new PartyAppendixSourcePartitioner().Project(corrupted);
        Assert.False(x.SourcePartitionComplete);
        Assert.Empty(x.Appendices);
        Assert.Contains("APPENDIX_SOURCE_MANIFEST_UNVERIFIED", x.Diagnostics);
    }

}
