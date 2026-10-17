using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Parsing;
using Xunit;

namespace Nd30.DocumentEngine.Tests;

public sealed class OrderedBodyBlockTests
{
    private static W.Paragraph Paragraph(string text) =>
        new(new W.Run(new W.Text(text)));

    private static W.Table Table(string cellText) =>
        new(
            new W.TableProperties(),
            new W.TableGrid(new W.GridColumn { Width = "2400" }),
            new W.TableRow(new W.TableCell(Paragraph(cellText)))
        );

    [Fact]
    public void Mixed_paragraph_table_body_keeps_source_order_and_single_ownership()
    {
        var path = Path.Combine(Path.GetTempPath(), $"p0c-ordered-{Guid.NewGuid():N}.docx");
        try
        {
            using (var package = WordprocessingDocument.Create(path, WordprocessingDocumentType.Document))
            {
                var main = package.AddMainDocumentPart();
                main.Document = new W.Document(
                    new W.Body(
                        Paragraph("before"),
                        Table("cell one"),
                        Paragraph("middle"),
                        Table("cell two"),
                        Paragraph("after"),
                        new W.SectionProperties()
                    )
                );
                main.Document.Save();
            }

            var parsed = new DocxParser().Parse(path);
            Assert.NotNull(parsed.Document);
            var document = parsed.Document!;
            var blocks = document.BodyBlocks;

            Assert.Equal(new[] { "paragraph", "table", "paragraph", "table", "paragraph" }, blocks.Select(b => b.Kind));
            Assert.Equal(new[] { "p1", "t1", "p2", "t2", "p3" }, blocks.Select(b => b.ModelId));
            Assert.Equal(new[] { 1, 2, 3, 4, 5 }, blocks.Select(b => b.Order));
            Assert.Equal(new[] { "p", "tbl", "p", "tbl", "p" }, blocks.Select(b => b.ElementName));
            Assert.Equal(5, blocks.Count); // Final w:sectPr is layout metadata, not a content block.
            Assert.Equal(3, document.Paragraphs.Count);
            Assert.Equal(2, document.Tables.Count);

            Assert.All(blocks, b => Assert.Equal("word/document.xml", b.SourcePart));
            Assert.Equal(
                Enumerable.Range(1, 5).Select(i => $"/w:document/w:body/*[{i}]"),
                blocks.Select(b => b.SourcePath));
            Assert.Equal(new[] { "before", "middle", "after" }, document.Paragraphs.Select(p => p.Runs[0].Text));
            Assert.Equal("cell one", document.Tables[0].Rows[0].Cells[0].Paragraphs[0].Runs[0].Text);
            Assert.Equal("cell two", document.Tables[1].Rows[0].Cells[0].Paragraphs[0].Runs[0].Text);
            Assert.DoesNotContain(blocks, b => b.ModelId?.Contains(".c", StringComparison.Ordinal) == true);
            Assert.Equal(blocks.Count, blocks.Select(b => b.ModelId).Distinct().Count());
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Existing_table_fixture_retains_paragraph_and_table_ids()
    {
        var path = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../fixtures/docx/07-table.docx"));
        var parsed = new DocxParser().Parse(path);
        Assert.NotNull(parsed.Document);
        var document = parsed.Document!;

        Assert.Equal(document.Paragraphs.Count, document.BodyBlocks.Count(b => b.Kind == "paragraph"));
        Assert.Equal(document.Tables.Count, document.BodyBlocks.Count(b => b.Kind == "table"));
        Assert.All(document.BodyBlocks.Where(b => b.Kind == "paragraph"),
            b => Assert.Contains(document.Paragraphs, p => p.Id == b.ModelId));
        Assert.All(document.BodyBlocks.Where(b => b.Kind == "table"),
            b => Assert.Contains(document.Tables, t => t.Id == b.ModelId));
    }
}
