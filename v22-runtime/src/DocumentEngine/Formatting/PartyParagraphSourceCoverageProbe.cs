using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Safety;

namespace Nd30.DocumentEngine.Formatting;

/// <summary>
/// Conservative snapshot-to-model run completeness probe. Uses the existing
/// preflighted snapshot and Open XML SDK; does not parse, normalize, mutate,
/// or give authority to any Party rule.
/// </summary>
public sealed record PartyParagraphSourceCoverage(
    ObservationOutcome Outcome, string Reason, int XmlDirectRunCount,
    int ModelRunCount)
{
    public bool RequiresHumanReview => true;
}

/// <summary>
/// For directly owned body paragraphs only. A paragraph containing w:hyperlink,
/// w:sdt, w:fldSimple, tracking wrappers, bookmarks, breaks, drawings or any
/// unrecognized inline child cannot be certified as fully represented by the
/// legacy direct-run model. We fail closed rather than flattening those children.
/// </summary>
public sealed class PartyParagraphSourceCoverageProbe
{
    public PartyParagraphSourceCoverage Observe(
        SafeDocxSnapshot snapshot, DocumentModel document,
        BodyBlockReference source, ParagraphModel paragraph)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(paragraph);

        PartyParagraphSourceCoverage Unknown(string reason, int count = 0) =>
            new(ObservationOutcome.NotEvaluated, reason, count, paragraph.Runs.Count);

        if (source.Kind != "paragraph" || source.ElementName != "p"
            || source.SourcePart != "word/document.xml" || source.ModelId is null
            || !document.BodyBlocks.Any(b => ReferenceEquals(b, source))
            || !document.Paragraphs.Any(p => ReferenceEquals(p, paragraph)
                && string.Equals(p.Id, source.ModelId, StringComparison.Ordinal)))
            return Unknown("SOURCE_BLOCK_OWNERSHIP_NOT_CONFIRMED");

        if (source.Order < 1 || source.SourcePath !=
            $"/w:document/w:body/*[{source.Order}]")
            return Unknown("SOURCE_BLOCK_PATH_NOT_CONFIRMED");

        try
        {
            snapshot.EnsureUnchanged();
            using var package = WordprocessingDocument.Open(
                snapshot.StagedPath, false,
                new OpenSettings {
                    MaxCharactersInPart = PackageSafetyLimits.MaximumXmlCharactersPerPart
                });
            var body = package.MainDocumentPart?.Document.Body;
            if (body is null || source.Order > body.ChildElements.Count
                || body.ChildElements[source.Order - 1] is not W.Paragraph xmlParagraph)
                return Unknown("SOURCE_BODY_PARAGRAPH_NOT_FOUND");

            // P0-C currently models only w:r that are direct w:p children.
            // Do not silently ignore hyperlink/SDT/field/redline/other wrappers.
            if (xmlParagraph.ChildElements.Any(child =>
                    child is not W.ParagraphProperties && child is not W.Run))
                return Unknown("UNSUPPORTED_NESTED_OR_INLINE_CONTAINER");

            var directRuns = xmlParagraph.Elements<W.Run>().ToArray();
            if (directRuns.Length == 0)
                return Unknown("NO_SUPPORTED_DIRECT_TEXT_RUNS");

            if (directRuns.Any(run => run.ChildElements.Any(child =>
                    child is not W.RunProperties && child is not W.Text)))
                return Unknown("UNSUPPORTED_RUN_CONTENT", directRuns.Length);

            if (directRuns.Any(run => !run.Elements<W.Text>().Any()
                    || string.IsNullOrEmpty(run.InnerText))
                || paragraph.Runs.Any(run => run.Fields.Count > 0))
                return Unknown("NON_TEXT_OR_FIELD_RUN_REQUIRES_REVIEW", directRuns.Length);

            if (directRuns.Length != paragraph.Runs.Count
                || !directRuns.Select(run => run.InnerText)
                    .SequenceEqual(paragraph.Runs.Select(run => run.Text), StringComparer.Ordinal)
                || !paragraph.Runs.Select((run, index) => run.Id ==
                    $"{paragraph.Id}.r{index + 1}").All(x => x))
                return Unknown("MODEL_DIRECT_RUN_COVERAGE_MISMATCH", directRuns.Length);

            // Verify the declared paragraph style too. This is a structure
            // coverage gate, NOT a check that inherited styles or legal rules
            // are compliant.
            if (!string.Equals(xmlParagraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value,
                    paragraph.StyleId, StringComparison.Ordinal))
                return Unknown("MODEL_PARAGRAPH_STYLE_MISMATCH", directRuns.Length);

            snapshot.EnsureUnchanged();
            return new(ObservationOutcome.Consistent,
                "DIRECT_INLINE_SOURCE_RUNS_CONFIRMED",
                directRuns.Length, paragraph.Runs.Count);
        }
        catch (Exception)
        {
            // The source snapshot was disposed/modified, malformed or not
            // accessible. Do not manufacture a positive observation.
            return Unknown("SOURCE_SNAPSHOT_UNAVAILABLE");
        }
    }
}
