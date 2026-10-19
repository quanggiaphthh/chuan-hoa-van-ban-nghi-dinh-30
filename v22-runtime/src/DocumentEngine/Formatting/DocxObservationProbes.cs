using Nd30.DocumentEngine.Model;

namespace Nd30.DocumentEngine.Formatting;

// These read-only probes are NOT legal validators. No Party rule is imported or activated.
public enum ObservationOutcome { Consistent, Different, NotEvaluated }
public enum ObservedRunProperty { FontSizePt, Bold, Italic }
public enum DeclaredPrintSurface { Unknown, SingleSided, DuplexFront, DuplexBack }

public sealed record RunSpanObservation(
    ObservationOutcome Outcome,
    string Reason,
    IReadOnlyList<string> RunIds,
    double? NumericValue,
    bool? BooleanValue,
    IReadOnlyList<string> Provenances)
{
    public bool RequiresReview => true;
}

public sealed record MarginTarget(double TopMm, double BottomMm, double LeftMm, double RightMm);
public sealed record MarginPrintEvidence(
    DeclaredPrintSurface Surface,
    bool OrientationIndependentlyConfirmed,
    bool PrintIntentIndependentlyConfirmed,
    bool SourceSectionConfirmed);

public sealed record MarginObservation(
    ObservationOutcome Outcome,
    string Reason,
    IReadOnlyDictionary<string, double> MeasuredMm)
{
    public bool RequiresReview => true;
}

/// <summary>
/// Measures every run of an explicitly selected WHOLE paragraph. A semantic
/// component may occupy less than a paragraph, so a caller must not interpret
/// Consistent as proof of a complete component's formatting or legal compliance.
/// </summary>
public sealed class RunSpanObservationProbe
{
    private readonly EffectiveFormattingResolver _resolver = new();

    public RunSpanObservation Observe(
        DocumentModel document,
        ParagraphModel paragraph,
        IReadOnlyList<string> runIds,
        ObservedRunProperty property)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(runIds);

        RunSpanObservation Unknown(string code) =>
            new(ObservationOutcome.NotEvaluated, code, runIds.ToArray(), null, null, []);

        // The selected paragraph must belong to this parsed DOCX; otherwise a
        // caller could accidentally measure an unrelated or reconstructed block.
        var belongsToDocument = document.Paragraphs.Any(p => ReferenceEquals(p, paragraph))
            || document.Tables.SelectMany(t => t.Rows).SelectMany(r => r.Cells)
                .SelectMany(c => c.Paragraphs).Any(p => ReferenceEquals(p, paragraph))
            || document.Headers.SelectMany(h => h.Paragraphs)
                .Any(p => ReferenceEquals(p, paragraph))
            || document.Footers.SelectMany(f => f.Paragraphs)
                .Any(p => ReferenceEquals(p, paragraph));
        if (!belongsToDocument)
            return Unknown("PARAGRAPH_PROVENANCE_NOT_CONFIRMED");

        // Require complete, source-ordered paragraph coverage. A selection of one
        // convenient run must never certify a paragraph containing different runs.
        if (paragraph.Runs.Count == 0
            || runIds.Count != paragraph.Runs.Count
            || !runIds.SequenceEqual(paragraph.Runs.Select(r => r.Id), StringComparer.Ordinal)
            || runIds.Distinct(StringComparer.Ordinal).Count() != runIds.Count)
            return Unknown("INCOMPLETE_OR_UNORDERED_RUN_SPAN");

        double? firstNumeric = null;
        bool? firstBoolean = null;
        var provenances = new List<string>();
        var mixed = false;

        foreach (var run in paragraph.Runs)
        {
            if (string.IsNullOrEmpty(run.Text) || run.Fields.Count > 0)
                return Unknown("NON_TEXT_OR_FIELD_RUN_REQUIRES_REVIEW");

            EffectiveFormatting effective;
            try
            {
                effective = _resolver.Resolve(document, paragraph, run);
            }
            catch (StyleInheritanceCycleException)
            {
                return Unknown("STYLE_INHERITANCE_CYCLE");
            }

            switch (property)
            {
                case ObservedRunProperty.FontSizePt:
                {
                    var value = effective.FontSizePt.Value;
                    if (value is null) return Unknown("MISSING_EFFECTIVE_FONT_SIZE");
                    provenances.Add(effective.FontSizePt.Provenance);
                    if (firstNumeric is null) firstNumeric = value;
                    else if (Math.Abs(firstNumeric.Value - value.Value) > 0.000001) mixed = true;
                    break;
                }
                case ObservedRunProperty.Bold:
                case ObservedRunProperty.Italic:
                {
                    var effectiveBool = property == ObservedRunProperty.Bold
                        ? effective.Bold
                        : effective.Italic;
                    if (effectiveBool.Value is null) return Unknown("MISSING_EFFECTIVE_BOOLEAN_PROPERTY");
                    provenances.Add(effectiveBool.Provenance);
                    if (firstBoolean is null) firstBoolean = effectiveBool.Value;
                    else if (firstBoolean.Value != effectiveBool.Value.Value) mixed = true;
                    break;
                }
                default:
                    return Unknown("UNSUPPORTED_OBSERVED_RUN_PROPERTY");
            }
        }

        return new(
            mixed ? ObservationOutcome.Different : ObservationOutcome.Consistent,
            mixed ? "MIXED_EFFECTIVE_RUN_FORMATTING" : "UNIFORM_PARAGRAPH_RUN_EVIDENCE_ONLY",
            runIds.ToArray(),
            mixed ? null : firstNumeric,
            mixed ? null : firstBoolean,
            provenances);
    }
}

/// <summary>
/// Compares section metadata to a caller-provided margin target (never embeds
/// HD05 or ND30 authority). DOCX section properties do not establish physical
/// duplex page parity, so back-side claims deliberately remain NotEvaluated.
/// </summary>
public static class SectionMarginObservationProbe
{
    private const double TwipsPerMillimeter = 1440d / 25.4d;
    // Rounding tolerance for OpenXML integer twips, NOT a legal tolerance.
    private const double EncodingToleranceMm = 0.05d;

    public static MarginObservation Observe(
        SectionModel section,
        MarginTarget target,
        MarginPrintEvidence context)
    {
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(context);

        MarginObservation Unknown(string reason) =>
            new(ObservationOutcome.NotEvaluated, reason, new Dictionary<string, double>());

        if (!context.SourceSectionConfirmed
            || !context.OrientationIndependentlyConfirmed
            || !context.PrintIntentIndependentlyConfirmed)
            return Unknown("SECTION_OR_PRINT_CONTEXT_UNCONFIRMED");
        if (!string.Equals(section.Orientation, "portrait", StringComparison.OrdinalIgnoreCase))
            return Unknown("NON_PORTRAIT_SECTION");
        if (context.Surface == DeclaredPrintSurface.Unknown)
            return Unknown("PRINT_SURFACE_UNKNOWN");
        if (context.Surface == DeclaredPrintSurface.DuplexBack)
            return Unknown("DUPLEX_BACK_PHYSICAL_PAGE_PARITY_NOT_RENDER_VERIFIED");
        if (section.MarginTopTwips is null || section.MarginBottomTwips is null
            || section.MarginLeftTwips is null || section.MarginRightTwips is null)
            return Unknown("INCOMPLETE_SECTION_MARGIN_METADATA");

        var measured = new Dictionary<string, double>
        {
            ["top_mm"] = section.MarginTopTwips.Value / TwipsPerMillimeter,
            ["bottom_mm"] = section.MarginBottomTwips.Value / TwipsPerMillimeter,
            ["left_mm"] = section.MarginLeftTwips.Value / TwipsPerMillimeter,
            ["right_mm"] = section.MarginRightTwips.Value / TwipsPerMillimeter
        };
        var match = Math.Abs(measured["top_mm"] - target.TopMm) <= EncodingToleranceMm
            && Math.Abs(measured["bottom_mm"] - target.BottomMm) <= EncodingToleranceMm
            && Math.Abs(measured["left_mm"] - target.LeftMm) <= EncodingToleranceMm
            && Math.Abs(measured["right_mm"] - target.RightMm) <= EncodingToleranceMm;
        return new(match ? ObservationOutcome.Consistent : ObservationOutcome.Different,
            match ? "SECTION_METADATA_MATCHES_CANDIDATE_ONLY" : "SECTION_METADATA_DIFFERS_FROM_CANDIDATE",
            measured);
    }
}
