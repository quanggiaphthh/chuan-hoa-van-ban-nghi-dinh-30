using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Adapters;

/// <summary>Derived observation at one real top-level body block, not legal compliance.</summary>
public sealed record PartyScopedEvidenceObservation(
    string CandidateId, int SourceOrder, string SourcePart, string SourcePath,
    string? ParagraphId, string TargetKey, ValidationStatus Status, string Reason,
    bool RequiresHumanReview = true);

public sealed record PartyScopedEvidenceSnapshot(
    PatchPolicy DocumentPatchPolicy, bool RequiresHumanReview,
    IReadOnlyList<PartyScopedEvidenceObservation> Observations,
    IReadOnlyList<string> ObservedTargetKeys, int EvidenceRecordCount);

/// <summary>
/// Reuses V23 document session. Independent source blocks receive distinct
/// internal keys; all observations remain review-only, never eligible for patch.
/// Inline nested OOXML completeness and appendix ownership are NOT established.
/// </summary>
public sealed class PartySourceScopedEvidenceSession
{
    private static readonly Regex CandidateId = new(
        "^[a-z][a-z0-9_-]{0,63}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex BodyPath = new(
        @"^/w:document/w:body/\*\[[1-9][0-9]*\]$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly DocumentModel _document;
    private readonly BodyBlockReference[] _sourceAtOpen;
    private readonly PartyEvidenceSession _session;
    private readonly List<PartyScopedEvidenceObservation> _history = new();
    private readonly object _gate = new();

    private PartySourceScopedEvidenceSession(DocumentModel document, DateOnly date)
    {
        _document = document;
        _sourceAtOpen = document.BodyBlocks.ToArray();
        _session = PartyEvidenceSession.Open(document, date);
    }

    public static PartySourceScopedEvidenceSession Open(DocumentModel document, DateOnly date)
    {
        ArgumentNullException.ThrowIfNull(document);
        return new(document, date);
    }

    public PartyScopedEvidenceObservation ObserveWholeBodyParagraph(
        string candidateId, BodyBlockReference source, ParagraphModel paragraph,
        ObservedRunProperty property)
    {
        if (candidateId is null || !CandidateId.IsMatch(candidateId))
            throw new ArgumentException("Invalid lower-case candidate ID.", nameof(candidateId));
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(paragraph);
        lock (_gate)
        {
            PartyScopedEvidenceObservation Deny(string reason)
            {
                var denied = new PartyScopedEvidenceObservation(candidateId,
                    source.Order, source.SourcePart, source.SourcePath, source.ModelId,
                    "", ValidationStatus.NOT_EVALUATED, reason);
                _history.Add(denied);
                return denied;
            }
            if (!ValidManifest()) return Deny("SOURCE_BLOCK_MANIFEST_UNVERIFIED");
            if (!_sourceAtOpen.Any(b => ReferenceEquals(b, source))
                || source.Kind != "paragraph" || source.ElementName != "p"
                || source.ModelId is null
                || !_document.Paragraphs.Any(p => ReferenceEquals(p, paragraph)
                    && string.Equals(p.Id, source.ModelId, StringComparison.Ordinal)))
                return Deny("SOURCE_BLOCK_OWNERSHIP_NOT_CONFIRMED");

            var scope = string.Join("\u001f", candidateId, source.SourcePart,
                source.SourcePath, source.ModelId,
                source.Order.ToString(System.Globalization.CultureInfo.InvariantCulture));
            var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(scope)))
                .ToLowerInvariant();
            var scopedCandidateId = "s" + digest[..40];
            var runIds = paragraph.Runs.Select(run => run.Id).ToArray();
            var result = _session.ObserveRun(paragraph, scopedCandidateId, runIds, property);
            var observation = new PartyScopedEvidenceObservation(candidateId, source.Order,
                source.SourcePart, source.SourcePath, source.ModelId,
                result.TargetKey, result.Status, result.Reason);
            _history.Add(observation);
            return observation;
        }
    }

    /// <summary>
    /// V25 opt-in strict source-coverage path: inspect the preflighted original
    /// OOXML before making any derived observation. Unknown inline structures
    /// cannot enter the uncertain-value evidence ledger.
    /// </summary>
    public PartyScopedEvidenceObservation ObserveSourceCoveredWholeBodyParagraph(
        Nd30.DocumentEngine.Safety.SafeDocxSnapshot snapshot,
        string candidateId, BodyBlockReference source, ParagraphModel paragraph,
        ObservedRunProperty property)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(paragraph);
        lock (_gate)
        {
            // Reuse the V24 public validation path for candidate ID syntax.
            // Do not store any observation on invalid input.
            if (candidateId is null || !CandidateId.IsMatch(candidateId))
                throw new ArgumentException("Invalid lower-case candidate ID.", nameof(candidateId));

            // Do not trust a matching source path unless the original object
            // and the complete ordered manifest remain owned by this session.
            if (!ValidManifest()
                || !_sourceAtOpen.Any(b => ReferenceEquals(b, source)))
            {
                var denied = new PartyScopedEvidenceObservation(
                    candidateId, source.Order, source.SourcePart, source.SourcePath,
                    source.ModelId, "", ValidationStatus.NOT_EVALUATED,
                    "SOURCE_BLOCK_MANIFEST_UNVERIFIED");
                _history.Add(denied);
                return denied;
            }
            var coverage = new PartyParagraphSourceCoverageProbe().Observe(
                snapshot, _document, source, paragraph);
            if (coverage.Outcome != ObservationOutcome.Consistent)
            {
                var denied = new PartyScopedEvidenceObservation(
                    candidateId, source.Order, source.SourcePart, source.SourcePath,
                    source.ModelId, "", ValidationStatus.NOT_EVALUATED, coverage.Reason);
                _history.Add(denied);
                return denied;
            }
            return ObserveWholeBodyParagraph(candidateId, source, paragraph, property);
        }
    }

    public PartyScopedEvidenceSnapshot Snapshot()
    {
        lock (_gate)
        {
            var s = _session.Snapshot();
            return new(PatchPolicy.PROHIBITED, true,
                Array.AsReadOnly(_history.ToArray()),
                Array.AsReadOnly(s.ObservedTargetKeys.ToArray()), s.EvidenceRecordCount);
        }
    }

    private bool ValidManifest()
    {
        if (_sourceAtOpen.Length == 0 || _document.BodyBlocks.Count != _sourceAtOpen.Length)
            return false;
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < _sourceAtOpen.Length; i++)
        {
            var b = _sourceAtOpen[i];
            if (!ReferenceEquals(b, _document.BodyBlocks[i]) || b.Order != i + 1
                || b.SourcePart != "word/document.xml"
                || !BodyPath.IsMatch(b.SourcePath) || !seenPaths.Add(b.SourcePath))
                return false;
            if (b.ModelId is not null && !seenIds.Add(b.ModelId)) return false;
            if (b.Kind == "paragraph")
            {
                if (b.ElementName != "p" || b.ModelId is null
                    || !_document.Paragraphs.Any(p => p.Id == b.ModelId)) return false;
            }
            else if (b.Kind == "table")
            {
                if (b.ElementName != "tbl" || b.ModelId is null
                    || !_document.Tables.Any(t => t.Id == b.ModelId)) return false;
            }
            else if (b.Kind != "unclassified" || b.ModelId is not null) return false;
        }
        return true;
    }
}
