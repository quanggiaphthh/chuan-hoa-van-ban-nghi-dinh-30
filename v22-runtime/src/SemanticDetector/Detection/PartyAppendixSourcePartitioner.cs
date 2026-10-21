using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Model;

namespace Nd30.SemanticDetector.Detection;

/// <summary>
/// These are candidate source spans, not legally verified appendix ownership.
/// A table or an unsupported body container remains unclassified even when its
/// source order lies between two visible appendix headings.
/// </summary>
public enum PartyAppendixBlockDisposition
{
    ParagraphCandidate,
    TableUnclassified,
    UnsupportedContainer
}

public sealed record PartyAppendixSourceEntry(
    BodyBlockReference Source, PartyAppendixBlockDisposition Disposition);

public sealed record PartyAppendixSourceCandidate(
    int Ordinal, string Heading, int HeadingSourceOrder, string HeadingSourcePath,
    IReadOnlyList<PartyAppendixSourceEntry> SourceEntries,
    bool RequiresHumanReview = true);

public sealed record PartyAppendixSourcePartition(
    IReadOnlyList<BodyBlockReference> BeforeAppendices,
    IReadOnlyList<PartyAppendixSourceCandidate> Appendices,
    IReadOnlyList<string> Diagnostics,
    bool SourcePartitionComplete,
    bool RequiresHumanReview = true);

/// <summary>
/// Adds an additive appendices[]-style projection on top of the already-tested
/// PartySourceOrderSegmenter and P0-C ordered source references.
/// One source block is assigned to exactly one positional bucket; no parsed
/// table, unclassified container, body or signature block is discarded.
/// </summary>
public sealed class PartyAppendixSourcePartitioner
{
    private static readonly Regex AppendixHeading = new(
        @"^PHỤ\s+LỤC(?:\s+(?:[IVXLCDM]+|\d+))?$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex Space = new(@"\s+", RegexOptions.Compiled);

    public PartyAppendixSourcePartition Project(DocumentModel document)
    {
        ArgumentNullException.ThrowIfNull(document);
        PartyAppendixSourcePartition Refuse(string reason) =>
            new(Array.Empty<BodyBlockReference>(), Array.Empty<PartyAppendixSourceCandidate>(),
                Array.AsReadOnly(new[] { reason }), false);

        var blocks = document.BodyBlocks;
        if (blocks.Count == 0) return Refuse("ORDERED_BODY_BLOCKS_UNAVAILABLE");
        if (!ValidSourceManifest(document)) return Refuse("APPENDIX_SOURCE_MANIFEST_UNVERIFIED");

        PartySegmentationResult segmentation;
        try { segmentation = new PartySourceOrderSegmenter().Segment(document); }
        catch (Exception) { return Refuse("PARTY_SEGMENTATION_UNAVAILABLE"); }

        if (segmentation.Blocks.Count != blocks.Count
            || segmentation.Blocks.Where((candidate, i) =>
                !ReferenceEquals(candidate.Source, blocks[i])).Any())
            return Refuse("APPENDIX_SEGMENTATION_SOURCE_MISMATCH");

        var paragraphs = document.Paragraphs.ToDictionary(p => p.Id, StringComparer.Ordinal);
        var tables = document.Tables.ToDictionary(t => t.Id, StringComparer.Ordinal);
        // A table cell may itself contain an appendix heading; since P0-C
        // cannot establish its owning semantic region, do not silently absorb it.
        foreach (var block in blocks.Where(b => b.Kind == "table"))
        {
            if (!tables.TryGetValue(block.ModelId!, out var table))
                return Refuse("APPENDIX_TABLE_SOURCE_UNRESOLVED");
            if (table.Rows.SelectMany(row => row.Cells)
                .SelectMany(cell => cell.Paragraphs)
                .Any(p => AppendixHeading.IsMatch(Canon(p.Runs))))
                return Refuse("APPENDIX_HEADING_INSIDE_TABLE_UNRESOLVED");
        }

        if (!segmentation.Blocks.Any(b => b.Region == PartyCandidateRegion.Body)
            || !segmentation.Blocks.Any(b => b.Region == PartyCandidateRegion.End))
            return Refuse("BODY_OR_END_BOUNDARY_NOT_CONFIRMED");

        if (segmentation.Diagnostics.Contains("REPEATED_APPENDIX_HEADING_REQUIRES_REVIEW"))
            return Refuse("AMBIGUOUS_REPEATED_APPENDIX_HEADING");

        var anchors = segmentation.AppendixBoundaries;
        var diagnostics = new List<string> { "APPENDIX_SOURCE_CANDIDATES_REQUIRE_HUMAN_REVIEW" };
        if (anchors.Count == 0)
        {
            diagnostics.Add("NO_EXPLICIT_APPENDIX_BOUNDARIES");
            return new(Array.AsReadOnly(blocks.ToArray()),
                Array.Empty<PartyAppendixSourceCandidate>(),
                Array.AsReadOnly(diagnostics.ToArray()), true);
        }

        var firstEndOrder = segmentation.Blocks
            .Where(b => b.Region == PartyCandidateRegion.End)
            .Min(b => b.Source.Order);
        var lastAnchorOrder = firstEndOrder;
        for (var anchorIndex = 0; anchorIndex < anchors.Count; anchorIndex++)
        {
            var anchor = anchors[anchorIndex];
            if (anchor.Ordinal != anchorIndex + 1
                || anchor.SourceOrder <= lastAnchorOrder
                || anchor.SourceOrder > blocks.Count)
                return Refuse("APPENDIX_ANCHOR_ORDER_INVALID");
            var heading = blocks[anchor.SourceOrder - 1];
            if (heading.Kind != "paragraph" || heading.ModelId is null
                || heading.SourcePath != anchor.SourcePath
                || !paragraphs.TryGetValue(heading.ModelId, out var p)
                || !AppendixHeading.IsMatch(Canon(p.Runs))
                || !string.Equals(Canon(p.Runs), Space.Replace(anchor.Heading.Trim(), " "),
                    StringComparison.OrdinalIgnoreCase)
                || segmentation.Blocks[anchor.SourceOrder - 1].Region
                    != PartyCandidateRegion.Appendix)
                return Refuse("APPENDIX_ANCHOR_PROVENANCE_NOT_CONFIRMED");
            lastAnchorOrder = anchor.SourceOrder;
        }

        var first = anchors[0].SourceOrder - 1;
        if (segmentation.Blocks.Take(first).Any(b => b.Region == PartyCandidateRegion.Appendix))
            return Refuse("APPENDIX_OVERLAPS_PRECEDING_DOCUMENT");

        var groups = new List<PartyAppendixSourceCandidate>();
        var assigned = new HashSet<int>();
        for (var i = 0; i < anchors.Count; i++)
        {
            var start = anchors[i].SourceOrder - 1;
            var end = i + 1 < anchors.Count ? anchors[i + 1].SourceOrder - 1 : blocks.Count;
            var entries = new List<PartyAppendixSourceEntry>();
            for (var j = start; j < end; j++)
            {
                var block = blocks[j];
                if (!assigned.Add(block.Order))
                    return Refuse("APPENDIX_SOURCE_DUPLICATE_OWNER");
                var disposition = block.Kind switch
                {
                    "paragraph" => PartyAppendixBlockDisposition.ParagraphCandidate,
                    "table" => PartyAppendixBlockDisposition.TableUnclassified,
                    _ => PartyAppendixBlockDisposition.UnsupportedContainer
                };
                if (disposition == PartyAppendixBlockDisposition.ParagraphCandidate
                    && segmentation.Blocks[j].Region != PartyCandidateRegion.Appendix)
                    return Refuse("APPENDIX_PARAGRAPH_REGION_CONFLICT");
                if (disposition != PartyAppendixBlockDisposition.ParagraphCandidate)
                {
                    if (segmentation.Blocks[j].Region != PartyCandidateRegion.Unclassified)
                        return Refuse("APPENDIX_UNKNOWN_CONTAINER_REGION_CONFLICT");
                    diagnostics.Add($"UNCLASSIFIED_APPENDIX_SOURCE:{block.SourcePath}");
                }
                entries.Add(new(block, disposition));
            }
            groups.Add(new(anchors[i].Ordinal, anchors[i].Heading, anchors[i].SourceOrder,
                anchors[i].SourcePath, Array.AsReadOnly(entries.ToArray())));
        }

        var pre = blocks.Take(first).ToArray();
        var all = pre.Select(b => b.Order).Concat(assigned).ToArray();
        if (all.Length != blocks.Count || all.Distinct().Count() != blocks.Count)
            return Refuse("APPENDIX_PARTITION_NOT_EXHAUSTIVE_OR_OVERLAPPING");
        return new(Array.AsReadOnly(pre), Array.AsReadOnly(groups.ToArray()),
            Array.AsReadOnly(diagnostics.Distinct().ToArray()), true);
    }

    private static string Canon(IEnumerable<RunModel> runs) =>
        Space.Replace(string.Concat(runs.Select(r => r.Text)).Trim(), " ").ToUpperInvariant();

    private static bool ValidSourceManifest(DocumentModel document)
    {
        var paths = new HashSet<string>(StringComparer.Ordinal);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < document.BodyBlocks.Count; i++)
        {
            var b = document.BodyBlocks[i];
            if (b.Order != i + 1 || b.SourcePart != "word/document.xml"
                || b.SourcePath != $"/w:document/w:body/*[{i + 1}]"
                || !paths.Add(b.SourcePath))
                return false;
            if (b.ModelId is not null && !ids.Add(b.ModelId)) return false;
            if (b.Kind == "paragraph")
            {
                if (b.ElementName != "p" || b.ModelId is null
                    || document.Paragraphs.Count(p => p.Id == b.ModelId) != 1)
                    return false;
            }
            else if (b.Kind == "table")
            {
                if (b.ElementName != "tbl" || b.ModelId is null
                    || document.Tables.Count(t => t.Id == b.ModelId) != 1)
                    return false;
            }
            else if (b.Kind != "unclassified" || b.ModelId is not null)
                return false;
        }
        // A manifest can look internally consistent yet silently omit a parsed
        // top-level paragraph/table. Require reverse coverage of both model lists.
        var representedParagraphs = document.BodyBlocks.Where(b => b.Kind == "paragraph")
            .Select(b => b.ModelId!).ToHashSet(StringComparer.Ordinal);
        var representedTables = document.BodyBlocks.Where(b => b.Kind == "table")
            .Select(b => b.ModelId!).ToHashSet(StringComparer.Ordinal);
        if (representedParagraphs.Count != document.Paragraphs.Count
            || document.Paragraphs.Any(p => !representedParagraphs.Contains(p.Id))
            || representedTables.Count != document.Tables.Count
            || document.Tables.Any(t => !representedTables.Contains(t.Id)))
            return false;
        return true;
    }
}
