using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Adapters;

/// <summary>
/// One observation projected into the validation evidence container, NOT a
/// legal rule result. Status never becomes PASS or FAIL in this adapter.
/// </summary>
public sealed record PartyCandidateObservation(
    string TargetKey,
    ValidationStatus Status,
    string Reason,
    bool RequiresHumanReview = true);

/// <summary>
/// Read-only bridge from V17 DOCX probes to an isolated Party candidate
/// ValidationContext. No Party release, normativity or authority is inferred.
/// </summary>
public static class PartyObservationContextBridge
{
    private static readonly Regex CandidateId = new(
        "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private const string EvidenceKind = "party_docx_derived_observation_unverified";

    public static ValidationContext Create(DocumentModel document, DateOnly evaluationDate)
    {
        ArgumentNullException.ThrowIfNull(document);
        // More conservative than the input package: Party candidate observations
        // must never turn into an eligible document mutation.
        return new ValidationContext
        {
            EvaluationDate = evaluationDate,
            DocumentPatchPolicy = PatchPolicy.PROHIBITED
        };
    }

    public static PartyCandidateObservation ProjectRun(
        ValidationContext context,
        DocumentModel document,
        ParagraphModel paragraph,
        string candidateId,
        IReadOnlyList<string> orderedRunIds,
        ObservedRunProperty property)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(paragraph);
        ArgumentNullException.ThrowIfNull(orderedRunIds);
        var suffix = property switch
        {
            ObservedRunProperty.FontSizePt => "font_size_pt",
            ObservedRunProperty.Bold => "bold",
            ObservedRunProperty.Italic => "italic",
            _ => throw new ArgumentOutOfRangeException(nameof(property))
        };
        var key = Key(candidateId, suffix);
        context.DocumentPatchPolicy = PatchPolicy.PROHIBITED;

        if (context.ObservedValues.ContainsKey(key) || context.UncertainTargets.Contains(key))
            return new(key, ValidationStatus.NOT_EVALUATED, "DUPLICATE_CANDIDATE_TARGET");

        var observation = new RunSpanObservationProbe()
            .Observe(document, paragraph, orderedRunIds, property);

        if (observation.Outcome == ObservationOutcome.NotEvaluated)
            return new(key, ValidationStatus.NOT_EVALUATED, observation.Reason);

        object? value = property == ObservedRunProperty.FontSizePt
            ? observation.NumericValue
            : observation.BooleanValue;
        if (observation.Outcome == ObservationOutcome.Different)
            value = "non_uniform_effective_run_values";

        // Derived evidence is not authoritative Party field ownership. Marking
        // uncertainty is deliberate even when all paragraph runs agree.
        var evidence = new EvidenceRecord(
            EvidenceKind, EvidenceAuthority.Derived,
            paragraph.Id, null, true,
            new Dictionary<string, string>
            {
                ["candidate_id"] = candidateId,
                ["source_scope"] = "complete_paragraph_runs_only",
                ["observation_reason"] = observation.Reason,
                ["observed_property"] = suffix
            });
        context.Observe(key, value, evidence).MarkUncertain(key);
        return new(key, ValidationStatus.NEEDS_REVIEW, observation.Reason);
    }

    public static PartyCandidateObservation ProjectMargins(
        ValidationContext context,
        DocumentModel document,
        SectionModel section,
        string candidateId,
        MarginTarget target,
        MarginPrintEvidence printEvidence)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(section);
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(printEvidence);
        var key = Key(candidateId, "margins_mm");
        context.DocumentPatchPolicy = PatchPolicy.PROHIBITED;

        if (context.ObservedValues.ContainsKey(key) || context.UncertainTargets.Contains(key))
            return new(key, ValidationStatus.NOT_EVALUATED, "DUPLICATE_CANDIDATE_TARGET");

        // A section ID alone is not unique or sufficient provenance: require
        // the actual section instance from this parsed document.
        if (!document.Sections.Any(s => ReferenceEquals(s, section)))
            return new(key, ValidationStatus.NOT_EVALUATED, "SECTION_PROVENANCE_NOT_CONFIRMED");

        var observation = SectionMarginObservationProbe.Observe(
            section, target, printEvidence);
        if (observation.Outcome == ObservationOutcome.NotEvaluated)
            return new(key, ValidationStatus.NOT_EVALUATED, observation.Reason);

        context.Observe(key, observation.MeasuredMm, new EvidenceRecord(
            EvidenceKind, EvidenceAuthority.Derived,
            section.Id, null, true,
            new Dictionary<string, string>
            {
                ["candidate_id"] = candidateId,
                ["source_scope"] = "section_metadata_only",
                ["observation_reason"] = observation.Reason
            })).MarkUncertain(key);
        return new(key, ValidationStatus.NEEDS_REVIEW, observation.Reason);
    }

    private static string Key(string candidateId, string property)
    {
        if (candidateId is null || !CandidateId.IsMatch(candidateId))
            throw new ArgumentException(
                "Candidate ID must be one lowercase identifier, not a field path.",
                nameof(candidateId));
        return $"party_candidate.{candidateId}.{property}";
    }
}
