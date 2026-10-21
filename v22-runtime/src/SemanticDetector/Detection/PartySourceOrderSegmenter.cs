using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Model;

namespace Nd30.SemanticDetector.Detection;

// Candidate-only regions. No canonical rule, Party authority or mutation capability is implied.
public enum PartyCandidateRegion { Front, Body, End, Appendix, Unclassified }

public sealed record PartyBlockCandidate(
    BodyBlockReference Source,
    PartyCandidateRegion Region,
    string Text);

public sealed record PartySegmentationResult(
    IReadOnlyList<PartyBlockCandidate> Blocks,
    bool RequiresReview,
    IReadOnlyList<string> Diagnostics);

// Separate from AdministrativeSemanticDetector: it must never change the ND30 default path.
public sealed class PartySourceOrderSegmenter
{
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex BodyStart = new(
        @"^\s*(?:-\s*)?(?:Căn\s+cứ\b|Điều\s+\d+[\.:])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex EndStart = new(
        @"^\s*(?:Nơi\s+nhận\s*:|T\s*/\s*M\.|TM\.|KT\.|TL\.|TUQ\.|Q\.)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex AppendixStart = new(
        @"^\s*PHỤ\s+LỤC(?:\s+(?:[IVXLCDM]+|\d+))?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public PartySegmentationResult Segment(DocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var diagnostics = new List<string>();
        var paragraphs = document.Paragraphs.ToDictionary(p => p.Id, StringComparer.Ordinal);
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

        // Explicit Party regime and exact Party title are required; do not infer Party
        // documents from ordinary administrative headings or document-type keywords.
        var headingIndices = projected.Select((b, i) => (b, i))
            .Where(x => x.b.Source.Kind == "paragraph"
                && Canon(x.b.Text) == "ĐẢNG CỘNG SẢN VIỆT NAM")
            .Select(x => x.i).ToArray();

        if (headingIndices.Length != 1 || headingIndices[0] > 11)
            return Unresolved("PARTY_HEADING_MISSING_AMBIGUOUS_OR_MISPLACED");

        var heading = headingIndices[0];
        var body = Find(projected, heading + 1, b => BodyStart.IsMatch(b.Text));
        var end = body >= 0
            ? Find(projected, body + 1, b => EndStart.IsMatch(b.Text))
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

        var appendix = Find(projected, end + 1, b => AppendixStart.IsMatch(b.Text));
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
        return new(projected, true, diagnostics);
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
