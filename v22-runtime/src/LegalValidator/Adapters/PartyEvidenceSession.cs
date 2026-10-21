using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Adapters;

/// <summary>
/// Single-document, review-only observation facade.
/// No mutable context or document is exposed. Legacy bridge calls are also
/// protected by their own document/session binding guard.
/// </summary>
public sealed class PartyEvidenceSession
{
    private readonly DocumentModel _document;
    private readonly ValidationContext _context;
    private readonly object _gate = new();
    private readonly List<PartyCandidateObservation> _results = new();

    private PartyEvidenceSession(DocumentModel document, DateOnly evaluationDate)
    {
        _document = document;
        _context = PartyObservationContextBridge.Create(document, evaluationDate);
    }

    public static PartyEvidenceSession Open(DocumentModel document, DateOnly evaluationDate)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new PartyEvidenceSession(document, evaluationDate);
    }

    public PartyCandidateObservation ObserveRun(
        ParagraphModel paragraph,
        string candidateId,
        IReadOnlyList<string> orderedRunIds,
        ObservedRunProperty property)
    {
        lock (_gate)
        {
            var outcome = PartyObservationContextBridge.ProjectRun(
                _context, _document, paragraph, candidateId, orderedRunIds, property);
            _results.Add(outcome);
            return outcome;
        }
    }

    public PartyCandidateObservation ObserveMargins(
        SectionModel section,
        string candidateId,
        MarginTarget target,
        MarginPrintEvidence printEvidence)
    {
        lock (_gate)
        {
            var outcome = PartyObservationContextBridge.ProjectMargins(
                _context, _document, section, candidateId, target, printEvidence);
            _results.Add(outcome);
            return outcome;
        }
    }

    /// <summary>
    /// Stable, diagnostic-only copy. No mutable ValidationContext, source
    /// document, evidence dictionary, or patch capability escapes this API.
    /// </summary>
    public PartyEvidenceSessionSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new PartyEvidenceSessionSnapshot(
                _context.EvaluationDate,
                PatchPolicy.PROHIBITED,
                _results.ToList().AsReadOnly(),
                _context.ObservedValues.Keys.OrderBy(x => x, StringComparer.Ordinal)
                    .ToList().AsReadOnly(),
                _context.Evidence.Values.Sum(items => items.Count),
                true);
        }
    }
}

public sealed record PartyEvidenceSessionSnapshot(
    DateOnly EvaluationDate,
    PatchPolicy DocumentPatchPolicy,
    IReadOnlyList<PartyCandidateObservation> Results,
    IReadOnlyList<string> ObservedTargetKeys,
    int EvidenceRecordCount,
    bool RequiresHumanReview);
