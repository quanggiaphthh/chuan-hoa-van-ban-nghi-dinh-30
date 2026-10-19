using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Model;

namespace Nd30.SemanticDetector.Detection;

// Candidate-only regions. No canonical rule, Party authority or mutation capability is implied.
public enum PartyCandidateRegion { Front, Body, End, Appendix, Unclassified }

public sealed record PartyBlockCandidate(
    BodyBlockReference Source,
    PartyCandidateRegion Region,
    string Text);

public sealed record PartyAppendixBoundary(int Ordinal, int SourceOrder, string SourcePath, string Heading);
public sealed record PartySigningBoundary(int SourceOrder, string SourcePath, string Prefix);

public sealed record PartySegmentationResult(
    IReadOnlyList<PartyBlockCandidate> Blocks,
    bool RequiresReview,
    IReadOnlyList<string> Diagnostics)
{
    // Evidence-only metadata; no appendix content or signer identity is asserted.
    public IReadOnlyList<PartyAppendixBoundary> AppendixBoundaries { get; init; } = [];
    public IReadOnlyList<PartySigningBoundary> SigningBoundaries { get; init; } = [];
}

// Separate from AdministrativeSemanticDetector: it must never change the ND30 default path.
public sealed class PartySourceOrderSegmenter
{
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BodyStart = new(
        @"^\s*(?:-\s*)?(?:Căn\s+cứ\b|Điều\s+\d+[\.:])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RecipientStart = new(
        @"^\s*Nơi\s+nhận\s*:", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex SigningPrefix = new(
        @"^\s*(?:T\s*/\s*M\.?|TM\.|K\s*/\s*T\.?|KT\.|TL\.|TUQ\.|Q\.)(?:\s|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AppendixStart = new(
        @"^\s*PHỤ\s+LỤC(?:\s+(?:[IVXLCDM]+|\d+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PartySegmentationResult Segment(DocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var diagnostics = new List<string>();
        var paragraphs = document.Paragraphs.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var tables = document.Tables.ToDictionary(t => t.Id, StringComparer.Ordinal);
        var blocks = document.BodyBlocks;
        if (blocks.Count == 0)
            return new([], true, ["ORDERED_BODY_BLOCKS_UNAVAILABLE"]);

        var validOrder = blocks.Select((b, i) => b.Order == i + 1
                && b.SourcePart == "word/document.xml"
                && b.SourcePath.StartsWith("/w:document/w:body/*[", StringComparison.Ordinal)
                && (b.Kind == "paragraph" || b.Kind == "table" || b.Kind == "unclassified"))
            .All(x => x);
        if (!validOrder)
            diagnostics.Add("INVALID_SOURCE_ORDER_PROVENANCE");

        var projected = blocks.Select(b =>
        {
            if (b.Kind != "paragraph" || b.ModelId is null
                || !paragraphs.TryGetValue(b.ModelId, out var paragraph))
                return new PartyBlockCandidate(b, PartyCandidateRegion.Unclassified, "");
            return new PartyBlockCandidate(
                b, PartyCandidateRegion.Unclassified,
                string.Concat(paragraph.Runs.Select(r => r.Text)).Trim());
        }).ToArray();

        PartySegmentationResult Unresolved(string reason)
        {
            diagnostics.Add(reason);
            return new(projected, true, diagnostics);
        }

        if (!validOrder)
            return Unresolved("NO_STRUCTURAL_CLASSIFICATION_WITH_INVALID_ORDER");
        // Each direct source child may be owned once only, even when supplied by
        // a future adapter rather than the canonical DocxParser.
        var ids = blocks.Where(b => b.ModelId is not null).Select(b => b.ModelId!).ToArray();
        if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Length
            || blocks.Select(b => b.SourcePath).Distinct(StringComparer.Ordinal).Count() != blocks.Count)
            return Unresolved("DUPLICATE_BODY_BLOCK_PROVENANCE");
        if (blocks.Any(b => b.Kind == "paragraph"
                && (b.ModelId is null || !paragraphs.ContainsKey(b.ModelId))))
            return Unresolved("UNRESOLVED_PARAGRAPH_REFERENCE");

        // A table-cell heading is evidence only: the table remains unclassified
        // because a single cell cannot prove the role of every other cell.
        int HeadingCount(PartyBlockCandidate b, string title)
        {
            if (b.Source.Kind == "paragraph")
                return Canon(b.Text) == title ? 1 : 0;
            if (b.Source.Kind != "table" || b.Source.ModelId is null
                || !tables.TryGetValue(b.Source.ModelId, out var table))
                return 0;
            return table.Rows.SelectMany(r => r.Cells)
                .SelectMany(c => c.Paragraphs)
                .Count(p => Canon(string.Concat(p.Runs.Select(r => r.Text))) == title);
        }

        const string partyTitle = "ĐẢNG CỘNG SẢN VIỆT NAM";
        const string stateTitle = "CỘNG HÒA XÃ HỘI CHỦ NGHĨA VIỆT NAM";
        var matches = projected.Select((b, i) => (index: i, count: HeadingCount(b, partyTitle)))
            .Where(x => x.count > 0).ToArray();
        if (matches.Length != 1 || matches[0].count != 1 || matches[0].index > 11)
            return Unresolved("PARTY_HEADING_MISSING_AMBIGUOUS_OR_MISPLACED");
        if (projected.Take(Math.Min(12, projected.Length)).Any(b => HeadingCount(b, stateTitle) > 0))
            return Unresolved("CONFLICTING_STATE_AND_PARTY_HEADINGS");

        var heading = matches[0].index;
        if (projected[heading].Source.Kind == "table")
            diagnostics.Add("PARTY_HEADING_IN_TABLE_UNCLASSIFIED");
        var body = Find(projected, heading + 1, b => BodyStart.IsMatch(b.Text));
        var end = body >= 0
            ? Find(projected, body + 1, b =>
                RecipientStart.IsMatch(b.Text) || SigningPrefix.IsMatch(b.Text))
            : -1;
        if (body < 0 || end < 0)
        {
            diagnostics.Add(body < 0 ? "BODY_START_NOT_CONFIRMED" : "END_START_NOT_CONFIRMED");
            // Only the exact Party title is identified; no unknown signature or
            // appendix can be accidentally given ownership by the body fallback.
            projected[heading] = projected[heading] with { Region = PartyCandidateRegion.Front };
            diagnostics.Add("PARTY_CANDIDATES_REQUIRE_HUMAN_REVIEW");
            return new(projected, true, diagnostics);
        }

        var appendixIndices = Enumerable.Range(end + 1, projected.Length - end - 1)
            .Where(i => projected[i].Source.Kind == "paragraph"
                && AppendixStart.IsMatch(projected[i].Text)).ToArray();
        var appendices = appendixIndices.Select((index, n) =>
            new PartyAppendixBoundary(n + 1, projected[index].Source.Order,
                projected[index].Source.SourcePath, projected[index].Text)).ToArray();
        if (appendices.Select(a => Canon(a.Heading)).Distinct(StringComparer.Ordinal).Count() < appendices.Length)
            diagnostics.Add("REPEATED_APPENDIX_HEADING_REQUIRES_REVIEW");

        var signatures = Enumerable.Range(end, projected.Length - end)
            .Where(i => projected[i].Source.Kind == "paragraph"
                && SigningPrefix.IsMatch(projected[i].Text))
            .Select(i => new PartySigningBoundary(projected[i].Source.Order,
                projected[i].Source.SourcePath, SigningPrefix.Match(projected[i].Text).Value.Trim()))
            .ToArray();
        if (signatures.Length > 1)
            diagnostics.Add("MULTIPLE_SIGNING_PREFIXES_REQUIRE_REVIEW");

        var appendix = appendixIndices.Length > 0 ? appendixIndices[0] : -1;
        for (var i = 0; i < projected.Length; i++)
        {
            var b = projected[i];
            if (b.Source.Kind != "paragraph")
            {
                diagnostics.Add($"UNCLASSIFIED_BODY_CONTAINER:{b.Source.SourcePath}");
                continue;
            }
            var region = i < body
                ? PartyCandidateRegion.Front
                : i < end
                    ? PartyCandidateRegion.Body
                    : appendix < 0 || i < appendix
                        ? PartyCandidateRegion.End
                        : PartyCandidateRegion.Appendix;
            projected[i] = b with { Region = region };
        }

        diagnostics.Add("PARTY_CANDIDATES_REQUIRE_HUMAN_REVIEW");
        return new(projected, true, diagnostics)
        {
            AppendixBoundaries = appendices,
            SigningBoundaries = signatures
        };
    }

    private static int Find(PartyBlockCandidate[] blocks, int from,
        Func<PartyBlockCandidate, bool> matches)
    {
        for (var i = from; i < blocks.Length; i++)
            if (blocks[i].Source.Kind == "paragraph" && matches(blocks[i]))
                return i;
        return -1;
    }

    private static string Canon(string text) =>
        Space.Replace(text.Trim(), " ").ToUpperInvariant();
}
