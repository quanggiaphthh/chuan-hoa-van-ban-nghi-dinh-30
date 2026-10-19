using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Package;
using Nd30.DocumentEngine.Parsing;
using Xunit;

namespace Nd30.DocumentEngine.Tests;

public sealed class DocxObservationProbesTests
{
    private static string MakeDocument(bool inheritedSize, bool overrideSecondSize,
        bool overrideSecondBold, bool landscape, bool omitMargins = false)
    {
        var file = Path.Combine(Path.GetTempPath(), $"docx-observation-{Guid.NewGuid():N}.docx");
        using (var package = WordprocessingDocument.Create(file, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            if (inheritedSize)
            {
                var styles = main.AddNewPart<StyleDefinitionsPart>();
                styles.Styles = new W.Styles(
                    new W.Style(new W.StyleRunProperties(
                        new W.FontSize { Val = "28" }, new W.Bold()))
                    { StyleId = "Base", Type = W.StyleValues.Paragraph },
                    new W.Style(new W.BasedOn { Val = "Base" })
                    { StyleId = "Derived", Type = W.StyleValues.Paragraph });
                styles.Styles.Save();
            }

            var first = new W.Run(new W.Text("Tiêu đề "));
            var secondProps = new W.RunProperties();
            if (overrideSecondSize) secondProps.Append(new W.FontSize { Val = "32" });
            if (overrideSecondBold) secondProps.Append(new W.Bold { Val = false });
            var second = new W.Run(secondProps, new W.Text("văn bản"));
            var paragraph = inheritedSize
                ? new W.Paragraph(new W.ParagraphProperties(
                    new W.ParagraphStyleId { Val = "Derived" }), first, second)
                : new W.Paragraph(first, second);

            var properties = new W.SectionProperties(
                new W.PageSize
                {
                    Width = landscape ? 15840u : 12240u,
                    Height = landscape ? 12240u : 15840u,
                    Orient = landscape ? W.PageOrientationValues.Landscape : W.PageOrientationValues.Portrait
                });
            if (!omitMargins)
            {
                properties.Append(new W.PageMargin
                {
                    Top = 1134,
                    Bottom = 1134,
                    Left = 1701,
                    Right = 850
                });
            }
            main.Document = new W.Document(new W.Body(paragraph, properties));
            main.Document.Save();
        }
        return file;
    }

    private static DocumentModel Parse(string file)
    {
        var parsed = new DocxParser().Parse(file);
        Assert.NotNull(parsed.Document);
        return parsed.Document!;
    }

    [Fact]
    public void Real_docx_inherited_styles_are_resolved_for_every_run()
    {
        var file = MakeDocument(true, false, false, false);
        try
        {
            var before = PackagePreserver.Sha256(file);
            var doc = Parse(file);
            var p = Assert.Single(doc.Paragraphs);
            var runIds = p.Runs.Select(r => r.Id).ToArray();
            var probe = new RunSpanObservationProbe();

            var size = probe.Observe(doc, p, runIds, ObservedRunProperty.FontSizePt);
            Assert.Equal(ObservationOutcome.Consistent, size.Outcome);
            Assert.Equal(14d, size.NumericValue);
            Assert.Equal(2, size.Provenances.Count);
            Assert.All(size.Provenances, src => Assert.Contains("style", src));

            var bold = probe.Observe(doc, p, runIds, ObservedRunProperty.Bold);
            Assert.Equal(ObservationOutcome.Consistent, bold.Outcome);
            Assert.True(bold.BooleanValue);
            Assert.True(bold.RequiresReview);
            Assert.Equal(before, PackagePreserver.Sha256(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Real_docx_mixed_run_size_and_bold_are_not_uniform()
    {
        var file = MakeDocument(true, true, true, false);
        try
        {
            var doc = Parse(file);
            var p = Assert.Single(doc.Paragraphs);
            var ids = p.Runs.Select(r => r.Id).ToArray();
            var probe = new RunSpanObservationProbe();

            var size = probe.Observe(doc, p, ids, ObservedRunProperty.FontSizePt);
            var bold = probe.Observe(doc, p, ids, ObservedRunProperty.Bold);
            Assert.Equal(ObservationOutcome.Different, size.Outcome);
            Assert.Equal(ObservationOutcome.Different, bold.Outcome);
            Assert.Equal("MIXED_EFFECTIVE_RUN_FORMATTING", size.Reason);
            Assert.Null(size.NumericValue);
            Assert.Null(bold.BooleanValue);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Partial_missing_or_reordered_run_evidence_is_not_evaluated()
    {
        var file = MakeDocument(true, true, false, false);
        try
        {
            var doc = Parse(file);
            var p = Assert.Single(doc.Paragraphs);
            var ids = p.Runs.Select(r => r.Id).ToArray();
            var probe = new RunSpanObservationProbe();
            foreach (var partial in new[]
            {
                new[] { ids[0] },
                new[] { ids[1], ids[0] },
                new[] { ids[0], ids[0] },
                new[] { ids[0], "p1.unknown" }
            })
            {
                var result = probe.Observe(doc, p, partial, ObservedRunProperty.FontSizePt);
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.True(result.RequiresReview);
            }
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Missing_effective_font_size_is_not_evaluated()
    {
        var file = MakeDocument(false, false, false, false);
        try
        {
            var doc = Parse(file);
            var p = Assert.Single(doc.Paragraphs);
            var result = new RunSpanObservationProbe().Observe(
                doc, p, p.Runs.Select(r => r.Id).ToArray(), ObservedRunProperty.FontSizePt);
            Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
            Assert.Equal("MISSING_EFFECTIVE_FONT_SIZE", result.Reason);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Style_cycles_from_existing_fixture_are_not_evaluated()
    {
        var file = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,
            "../../../../../fixtures/docx/19-style-cycle.docx"));
        var doc = Parse(file);
        var p = doc.Paragraphs[0];
        var result = new RunSpanObservationProbe().Observe(
            doc, p, p.Runs.Select(r => r.Id).ToArray(), ObservedRunProperty.FontSizePt);
        Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
        Assert.Equal("STYLE_INHERITANCE_CYCLE", result.Reason);
    }

    [Fact]
    public void Real_portrait_docx_reports_only_candidate_margin_metadata()
    {
        var file = MakeDocument(true, false, false, false);
        try
        {
            var before = PackagePreserver.Sha256(file);
            var doc = Parse(file);
            var section = Assert.Single(doc.Sections);
            var target = new MarginTarget(20, 20, 30, 15);
            var context = new MarginPrintEvidence(
                DeclaredPrintSurface.SingleSided, true, true, true);
            var result = SectionMarginObservationProbe.Observe(section, target, context);
            Assert.Equal(ObservationOutcome.Consistent, result.Outcome);
            Assert.True(result.RequiresReview);
            Assert.InRange(result.MeasuredMm["top_mm"], 19.95, 20.05);
            Assert.InRange(result.MeasuredMm["right_mm"], 14.95, 15.05);
            Assert.Equal(before, PackagePreserver.Sha256(file));

            var wrong = SectionMarginObservationProbe.Observe(
                section, new MarginTarget(20, 20, 15, 30), context);
            Assert.Equal(ObservationOutcome.Different, wrong.Outcome);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Duplex_back_and_unconfirmed_print_intent_are_never_misreported()
    {
        var file = MakeDocument(true, false, false, false);
        try
        {
            var section = Assert.Single(Parse(file).Sections);
            var target = new MarginTarget(20, 20, 30, 15);
            foreach (var surface in new[] { DeclaredPrintSurface.Unknown, DeclaredPrintSurface.DuplexBack })
            {
                var result = SectionMarginObservationProbe.Observe(
                    section, target, new MarginPrintEvidence(surface, true, true, true));
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.Empty(result.MeasuredMm);
            }
            var missing = SectionMarginObservationProbe.Observe(
                section, target, new MarginPrintEvidence(DeclaredPrintSurface.DuplexFront, true, false, true));
            Assert.Equal(ObservationOutcome.NotEvaluated, missing.Outcome);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Real_landscape_or_missing_margin_docx_is_not_evaluated()
    {
        foreach (var isLandscape in new[] { true, false })
        {
            var file = MakeDocument(true, false, false, isLandscape, omitMargins: !isLandscape);
            try
            {
                var section = Assert.Single(Parse(file).Sections);
                var result = SectionMarginObservationProbe.Observe(
                    section, new MarginTarget(20, 20, 30, 15),
                    new MarginPrintEvidence(DeclaredPrintSurface.SingleSided, true, true, true));
                Assert.Equal(ObservationOutcome.NotEvaluated, result.Outcome);
                Assert.Equal(isLandscape ? "NON_PORTRAIT_SECTION" : "INCOMPLETE_SECTION_MARGIN_METADATA", result.Reason);
            }
            finally { File.Delete(file); }
        }
    }
}
