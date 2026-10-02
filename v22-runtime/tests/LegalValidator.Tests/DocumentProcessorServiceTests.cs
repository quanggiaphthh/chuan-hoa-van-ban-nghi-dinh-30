using DocumentFormat.OpenXml.Packaging;
using System.Text;
using Nd30.DocumentEngine.Parsing;
using Nd30.DocumentEngine.Package;
using Nd30.LegalValidator.Processing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Xunit;

namespace Nd30.LegalValidator.Tests;

public sealed class DocumentProcessorServiceTests
{
    [Fact]
    public async Task Golden_direct_alignment_fixture_reopens_after_the_single_supported_mutation()
    {
        var source = await File.ReadAllBytesAsync(Fixture("23-direct-alignment.docx"));
        var original = source.ToArray();
        var sourceDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)).ToLowerInvariant();
        var processor = new DocumentProcessorService();

        var inspection = await processor.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        var target = Assert.Single(inspection.Paragraphs, paragraph => paragraph.ParagraphId == "p1");
        Assert.True(inspection.SafeToMutate);
        Assert.Equal("LEFT", target.DirectAlignment);

        var applied = await processor.ApplyAlignmentAsync(new MemoryStream(source, writable: false),
            sourceDigest, target.ParagraphId, "LEFT", "CENTER", CancellationToken.None);
        Assert.Equal(original, source);
        Assert.True(applied.Reopened);
        Assert.True(applied.Revalidated);
        Assert.True(applied.SourceUnchanged);
        Assert.NotEqual(sourceDigest, applied.OutputSha256);

        var reopened = await processor.InspectAsync(new MemoryStream(applied.OutputBytes, writable: false), CancellationToken.None);
        Assert.Equal("CENTER", Assert.Single(reopened.Paragraphs).DirectAlignment);
    }

    [Fact]
    public async Task Inspection_and_confirmed_alignment_produce_a_new_reopenable_artifact()
    {
        var source = CreateDocx();
        var original = source.ToArray();
        var sourceDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)).ToLowerInvariant();
        var processor = new DocumentProcessorService();

        var inspection = await processor.InspectAsync(new MemoryStream(source, writable: false), CancellationToken.None);
        Assert.Equal(sourceDigest, inspection.SourceSha256);
        Assert.True(inspection.SafeToMutate);
        Assert.Contains(inspection.Paragraphs, paragraph => paragraph.ParagraphId == "p1" && paragraph.DirectAlignment == "LEFT");

        var applied = await processor.ApplyAlignmentAsync(new MemoryStream(source, writable: false),
            sourceDigest, "p1", "LEFT", "CENTER", CancellationToken.None);
        Assert.Equal("p1", applied.ParagraphId);
        Assert.Equal("LEFT", applied.Before);
        Assert.Equal("CENTER", applied.After);
        Assert.NotEqual(sourceDigest, applied.OutputSha256);
        Assert.True(applied.Reopened);
        Assert.True(applied.Revalidated);
        Assert.True(applied.SourceUnchanged);
        Assert.Equal(original, source);

        var folder = Path.Combine(Path.GetTempPath(), "processor-fidelity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var sourcePath = Path.Combine(folder, "source.docx");
            var outputPath = Path.Combine(folder, "output.docx");
            await File.WriteAllBytesAsync(sourcePath, source);
            await File.WriteAllBytesAsync(outputPath, applied.OutputBytes);
            var beforeParts = PackagePreserver.PartSha256Inventory(sourcePath);
            var afterParts = PackagePreserver.PartSha256Inventory(outputPath);
            Assert.Equal(beforeParts.Keys, afterParts.Keys);
            Assert.NotEqual(beforeParts["word/document.xml"], afterParts["word/document.xml"]);
            foreach (var part in beforeParts.Keys.Where(part => !part.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase)))
                Assert.Equal(beforeParts[part], afterParts[part]);
        }
        finally { try { Directory.Delete(folder, recursive: true); } catch { } }

        await using var reopenedSnapshot = await Nd30.DocumentEngine.Safety.SafeDocxSnapshot.CreateAsync(new MemoryStream(applied.OutputBytes), CancellationToken.None);
        var reopened = new DocxParser().Parse(reopenedSnapshot);
        Assert.NotNull(reopened.Document);
        Assert.Equal("center", reopened.Document!.Paragraphs[0].DirectFormatting.Alignment, ignoreCase: true);
        Assert.Contains("Protected unrelated content", reopened.Document.Paragraphs[1].Runs[0].Text);
        Assert.Single(reopened.Document.Tables);
        Assert.Equal("Table content", reopened.Document.Tables[0].Rows[0].Cells[0].Paragraphs[0].Runs[0].Text);
        Assert.Single(reopened.Document.Sections);
        Assert.Equal((int?)1440, reopened.Document.Sections[0].MarginTopTwips);
    }

    [Fact]
    public async Task Rejects_stale_source_and_non_direct_alignment_without_an_output()
    {
        var source = CreateDocx();
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)).ToLowerInvariant();
        var processor = new DocumentProcessorService();

        var stale = await Assert.ThrowsAsync<DocumentProcessorException>(() => processor.ApplyAlignmentAsync(
            new MemoryStream(source, writable: false), new string('a', 64), "p1", "LEFT", "CENTER", CancellationToken.None));
        Assert.Equal("STALE_DOCUMENT", stale.Code);

        var inheritedOnly = CreateDocx(withDirectAlignment: false);
        var inheritedDigest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(inheritedOnly)).ToLowerInvariant();
        var nonDirect = await Assert.ThrowsAsync<DocumentProcessorException>(() => processor.ApplyAlignmentAsync(
            new MemoryStream(inheritedOnly, writable: false), inheritedDigest, "p1", "LEFT", "CENTER", CancellationToken.None));
        Assert.Equal("PROVENANCE_NOT_DIRECT", nonDirect.Code);
        Assert.Equal(digest, Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(source)).ToLowerInvariant());
    }

    [Fact]
    public async Task Rejects_unsafe_packages_before_returning_inspection()
    {
        var processor = new DocumentProcessorService();
        var error = await Assert.ThrowsAsync<DocumentProcessorException>(() => processor.InspectAsync(
            new MemoryStream(Encoding.UTF8.GetBytes("not a docx")), CancellationToken.None));
        Assert.Equal("MALFORMED_DOCX", error.Code);
        Assert.DoesNotContain(Path.GetTempPath(), error.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Deadline_and_caller_cancellation_return_distinct_safe_errors()
    {
        var timed = new DocumentProcessorService(TimeSpan.FromMilliseconds(25));
        var timeout = await Assert.ThrowsAsync<DocumentProcessorException>(() => timed.InspectAsync(new BlockingReadStream(), CancellationToken.None));
        Assert.Equal("PROCESSING_TIMEOUT", timeout.Code);
        Assert.DoesNotContain("/", timeout.SafeMessage, StringComparison.Ordinal);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await Assert.ThrowsAsync<DocumentProcessorException>(() => new DocumentProcessorService().InspectAsync(new MemoryStream(), cancellation.Token));
        Assert.Equal("PROCESSING_CANCELLED", cancelled.Code);
    }

    [Fact]
    public async Task Oversized_generated_output_is_rejected_before_materialization()
    {
        var path = Path.Combine(Path.GetTempPath(), "processor-output-limit-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                output.SetLength(DocumentProcessorService.MaximumOutputBytes + 1);
            var method = typeof(DocumentProcessorService).GetMethod("ReadBoundedOutputAsync",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            Assert.NotNull(method);
            var read = Assert.IsType<Task<byte[]>>(method.Invoke(null, [path, CancellationToken.None]));
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(async () => { await read; });
            Assert.Equal("OUTPUT_TOO_LARGE", error.Code);
            Assert.DoesNotContain(path, error.SafeMessage, StringComparison.OrdinalIgnoreCase);
        }
        finally { File.Delete(path); }
    }

    private static byte[] CreateDocx(bool withDirectAlignment = true)
    {
        using var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            var body = new W.Body();
            var targetProperties = new W.ParagraphProperties();
            if (withDirectAlignment) targetProperties.Append(new W.Justification { Val = W.JustificationValues.Left });
            body.Append(new W.Paragraph(targetProperties, new W.Run(new W.Text("Target paragraph"))));
            body.Append(new W.Paragraph(new W.Run(new W.Text("Protected unrelated content"))));
            body.Append(new W.Table(new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("Table content")))))));
            body.Append(new W.SectionProperties(new W.PageSize { Width = 12240, Height = 15840 },
                new W.PageMargin { Top = 1440, Right = 1440, Bottom = 1440, Left = 1440 }));
            main.Document = new W.Document(body);
            main.Document.Save();
        }
        return stream.ToArray();
    }

    private static string Fixture(string fileName) => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/docx", fileName));

    private sealed class BlockingReadStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() => throw new NotSupportedException();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) => WaitAsync(cancellationToken);
        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) => WaitAsync(cancellationToken).AsTask();
        private static async ValueTask<int> WaitAsync(CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
