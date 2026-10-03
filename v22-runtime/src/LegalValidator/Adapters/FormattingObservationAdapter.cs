using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Profiles;
using Nd30.SemanticDetector.Model;

namespace Nd30.LegalValidator.Adapters;

/// <summary>
/// One actionable formatting target discovered in the DOCX: a paragraph or
/// section property that the profile can enforce, with the value currently
/// present so a later apply can bind an exact expected-before value.
/// </summary>
public sealed record MutableTarget(
    string ParagraphId,
    string Property,
    string? CurrentValue,
    string Unit,
    string LabelVi,
    string RuleId,
    bool DirectOnly);

/// <summary>
/// A scoped validation finding. Deliberately never carries a whole-document
/// compliance verdict: the subset only covers rules the DOCX can actually
/// evidence.
/// </summary>
public sealed record ProfileFinding(
    string RuleId,
    string TargetKey,
    string Property,
    string Status,
    string Severity,
    string Expected,
    string Observed,
    string PatchEligibility,
    string Reason);

/// <summary>Result of running the real rule engine over the profile's scoped subset.</summary>
public sealed record ProfileValidationOutcome(
    string DocumentTypeKey,
    string DocumentTypeLabel,
    bool DocumentTypeConfirmed,
    IReadOnlyList<string> RuleSubset,
    IReadOnlyList<ProfileFinding> Findings,
    int ApplicableRules,
    int EvaluatedRules,
    int FailCount,
    int NotEvaluatedCount,
    int NeedsReviewCount,
    double EvaluatedCoveragePercent,
    bool FullComplianceClaimAllowed);

/// <summary>
/// Builds DOCX-derived observations for the profile-scoped rule subset.
///
/// The existing <see cref="SemanticValidationContextBuilder"/> observes one
/// representative run per semantic component. That is enough for a single
/// heading, but the body of an administrative document is many paragraphs whose
/// direct formatting may disagree. This adapter therefore:
///  - observes a component property only when every relevant run agrees, and
///    marks it uncertain otherwise, so a mixed document yields NEEDS_REVIEW
///    instead of a false PASS or a fabricated FAIL;
///  - resolves inherited values through <see cref="EffectiveFormattingResolver"/>
///    so a rule about the effective font is judged on what Word actually renders;
///  - adds the document- and section-level observations the profile needs but the
///    component adapter never emitted.
/// </summary>
public static class FormattingObservationAdapter
{
    private const double TwipsPerMillimetre = 1440d / 25.4d;
    private const double TwipsPerCentimetre = 1440d / 2.54d;

    public static ValidationContext BuildContext(
        DocumentModel document,
        SemanticDocumentModel semantic,
        ProfileDocumentType documentType,
        FormattingProfile profile,
        DateOnly evaluationDate)
    {
        var context = new ValidationContext { EvaluationDate = evaluationDate, DocumentPatchPolicy = document.Safety.PatchPolicy };
        foreach (var capability in profile.GrantedCapabilities) context.AddCapability(capability);
        context.SetField("document.type", documentType.DocumentType);

        // Reuse the proven component observations, then extend them. A null value
        // is never forwarded: an undeclared property is unknown, not evidence of
        // a non-conforming value, and observing it would turn "not stated" into a
        // false FAIL.
        var componentContext = SemanticValidationContextBuilder.Build(document, semantic, evaluationDate);
        foreach (var key in componentContext.ObservedValues.Keys)
        {
            // The component adapter publishes a single `line_spacing` value that
            // folds a line multiple and an absolute point value into one field.
            // That quantity is ambiguous, so it is dropped here and re-published
            // under `line_spacing_lines` / `line_spacing_pt`, decided by the DOCX
            // line rule.
            if (key.EndsWith(".line_spacing", StringComparison.Ordinal)) continue;
            // Role formatting is re-derived below by full-run consensus. Copying the
            // component builder's single representative run would leave a false PASS behind
            // whenever that consensus turns out to be unknown.
            if (IsRoleFormattingKey(key)) continue;
            var value = componentContext.ObservedValues[key];
            // An absent or blank property is unknown, not evidence of a
            // non-conforming value; observing it would turn "not stated" into a
            // false FAIL.
            if (value is null) continue;
            if (value is string text && string.IsNullOrWhiteSpace(text)) continue;
            context.Observe(key, value,
                componentContext.Evidence.TryGetValue(key, out var records) && records.Count > 0 ? records[0] : null);
        }
        foreach (var key in componentContext.UncertainTargets) context.MarkUncertain(key);
        // `document.type` is the profile-selected type, not the detector's suggestion.
        // The component builder publishes its own value, which would otherwise
        // overwrite the selected type and make type-specific applicability run under
        // the wrong regime. It is skipped here and set again after the merge.
        foreach (var (key, value) in componentContext.Fields)
        {
            if (string.Equals(key, "document.type", StringComparison.Ordinal)) continue;
            context.SetField(key, value);
        }
        // Re-assert the selected type last so nothing in the merge can displace it.
        context.SetField("document.type", documentType.DocumentType);

        ObserveRoleFormattingConsensus(document, semantic, context);
        ObserveParagraphTargets(document, context);
        // Must follow ObserveParagraphTargets: it reads the per-paragraph line multiples.
        ObserveCompositeLineConsensus(semantic, context);
        ObserveBodyParagraph(document, semantic, context);
        ObserveDocumentDefaults(document, context);
        ObserveSections(document, context);
        return context;
    }

    /// <summary>
    /// Per-paragraph observations over every paragraph in the body.
    ///
    /// These exist so a later apply can bind an exact expected-before value to a
    /// specific paragraph, including headings and signature blocks. They are not
    /// role-scoped and are never rolled up into a role-level key.
    /// </summary>
    /// <summary>
    /// The role key a component's formatting is published under.
    ///
    /// The detector's RecipientList component is the lexical label paragraph ("Nơi nhánh" and
    /// similar), not the recipient list itself. Publishing its formatting as list formatting made a
    /// label of one size be judged against list rules written for the entries, so a correct label
    /// could fail the list rule. It is therefore published under the label role, and the list role
    /// is left unobserved until a bounded detector change can collect the real entries. No
    /// following paragraph is guessed at here.
    /// </summary>
    private static string PublishedRoleKey(SemanticRole role) =>
        role == SemanticRole.RecipientList ? "recipient_list_label" : SnakeRole(role);

    /// <summary>
    /// Roles whose formatting this adapter re-derives by consensus instead of trusting a single
    /// representative run.
    /// </summary>
    private static readonly SemanticRole[] ConsensusRoles =
    {
        SemanticRole.NationalHeader, SemanticRole.NationalMotto,
        SemanticRole.IssuingAuthorityParent, SemanticRole.IssuingAuthority,
        SemanticRole.IssuePlaceAndDate, SemanticRole.DocumentTypeHeading,
        SemanticRole.SubjectNamedDocument, SemanticRole.SubjectOfficialLetter,
        SemanticRole.LegalBasisBlock, SemanticRole.Addressee, SemanticRole.RecipientList,
    };

    private static bool IsRoleFormattingKey(string key)
    {
        const string prefix = "semantic_component.";
        if (!key.StartsWith(prefix, StringComparison.Ordinal)) return false;
        var rest = key[prefix.Length..];
        var separator = rest.IndexOf('.');
        if (separator <= 0) return false;
        var role = rest[..separator];
        var property = rest[(separator + 1)..];
        // Compare the snake_case role name. Comparing the PascalCase enum name never matched, so
        // the component builder's representative run was copied in and could leave a false PASS.
        if (!ConsensusRoles.Any(candidate =>
                string.Equals(SnakeRole(candidate), role, StringComparison.OrdinalIgnoreCase)))
            return false;
        // "uppercase" is deliberately absent: EffectiveFormatting exposes no uppercase, so it
        // cannot be re-derived here, and skipping it would silently drop a real observation.
        return property is "font_size_pt" or "bold" or "italic" or "alignment";
    }

    /// <summary>
    /// Every paragraph in the package, whatever its source kind: top-level body, table cells,
    /// headers and footers. Looked up by exact paragraph id so a role paragraph is found wherever
    /// the document actually put it.
    /// </summary>
    private static IReadOnlyDictionary<string, ParagraphModel> AllParagraphsById(DocumentModel document)
    {
        var map = new Dictionary<string, ParagraphModel>(StringComparer.Ordinal);
        foreach (var paragraph in document.Paragraphs) map[paragraph.Id] = paragraph;
        foreach (var table in document.Tables)
            foreach (var row in table.Rows)
                foreach (var cell in row.Cells)
                    foreach (var paragraph in cell.Paragraphs) map[paragraph.Id] = paragraph;
        foreach (var header in document.Headers)
            foreach (var paragraph in header.Paragraphs) map[paragraph.Id] = paragraph;
        foreach (var footer in document.Footers)
            foreach (var paragraph in footer.Paragraphs) map[paragraph.Id] = paragraph;
        return map;
    }

    private static string? ConsensusValue(
        EffectiveFormatting formatting, string property, EffectiveFormattingResolver resolver)
    {
        var value = property switch
        {
            "font_size_pt" => formatting.FontSizePt.Value is { } size ? Format(Round(size, 2)) : null,
            "bold" => formatting.Bold.Value is { } bold ? (bold ? "true" : "false") : null,
            "italic" => formatting.Italic.Value is { } italic ? (italic ? "true" : "false") : null,
            // The catalog states alignment in lower case ("center"), so normalize only this role
            // boundary. The mutable target vocabulary stays upper case and is untouched.
            "alignment" => CanonicalAlignment(formatting.Alignment.Value)?.ToLowerInvariant(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Re-derives role formatting from every relevant run of every paragraph the detector assigned
    /// to that role, rather than from one representative run.
    ///
    /// Outcomes, none of them invented:
    /// all runs known and identical -> observed;
    /// any run unknown, values differ, or the role evidence is ambiguous -> uncertain (NEEDS_REVIEW);
    /// no run known at all -> nothing published (NOT_EVALUATED).
    /// Any representative value copied in from the component builder has already been skipped, so an
    /// unknown consensus cannot leave a stale false PASS behind.
    /// </summary>
    /// <summary>
    /// Rules that target a block spanning two roles. Each entry pairs the published key with every
    /// role that must contribute to it.
    /// </summary>
    private static readonly (string Key, SemanticRole[] Roles)[] CompositeLineBlocks =
    {
        ("semantic_component.national_header_motto_block.line_spacing",
            new[] { SemanticRole.NationalHeader, SemanticRole.NationalMotto }),
        ("semantic_component.issuer_block.line_spacing",
            new[] { SemanticRole.IssuingAuthorityParent, SemanticRole.IssuingAuthority }),
    };

    /// <summary>
    /// Publishes the line multiple for a composite block drawn from two roles.
    ///
    /// Unit-safe by construction: it reads only the per-paragraph `line_spacing_lines` observation,
    /// which exists solely for `lineRule="auto"`, so an absolute `exact`/`atLeast` spacing can never
    /// become a line verdict. Outcomes match the rest of the adapter: a required role that is absent,
    /// a paragraph with no line multiple, or disagreeing multiples all become uncertain
    /// (NEEDS_REVIEW); no line multiple anywhere publishes nothing (NOT_EVALUATED); a single agreed
    /// multiple is observed.
    /// </summary>
    private static void ObserveCompositeLineConsensus(SemanticDocumentModel semantic, ValidationContext context)
    {
        var evidence = new EvidenceRecord("effective_paragraph_style", EvidenceAuthority.Derived, "role-consensus", null, false);

        foreach (var (key, roles) in CompositeLineBlocks)
        {
            var ids = new List<string>();
            var unusable = false;
            var contributing = new List<SemanticComponentCandidate>();

            foreach (var role in roles)
            {
                var roleCandidates = semantic.Components
                    .Where(component => component.Role == role)
                    .ToArray();

                // The role must actually be present.
                if (roleCandidates.Length == 0) { unusable = true; break; }

                // Every candidate for the role must carry a usable paragraph id. Filtering blank ids
                // away let a role with one usable and one unidentified candidate count as satisfied
                // and publish a partial consensus.
                if (roleCandidates.Any(component => string.IsNullOrWhiteSpace(component.Evidence.ParagraphId)))
                {
                    unusable = true;
                    break;
                }

                contributing.AddRange(roleCandidates);
                ids.AddRange(roleCandidates
                    .Select(component => component.Evidence.ParagraphId)
                    .Distinct(StringComparer.Ordinal));
            }

            // The block cannot be formed from incomplete evidence, so it is unknown, not partial.
            if (unusable)
            {
                context.MarkUncertain(key);
                continue;
            }

            // The same uncertainty signals the single-role consensus honours apply here: two
            // uncertain role labels that happen to agree must not become a block verdict.
            var candidateIds = contributing.Select(component => component.Id).ToHashSet(StringComparer.Ordinal);
            var ambiguous = contributing.Any(component => component.Status == ClassificationStatus.INFERRED_LOW)
                || contributing.Any(component => component.CompetingCandidateIds.Count > 0)
                || semantic.Ambiguities.Any(ambiguity =>
                    ambiguity.CandidateIds.Any(id => candidateIds.Contains(id)));

            if (ambiguous)
            {
                context.MarkUncertain(key);
                continue;
            }

            var stated = ids
                .Select(id => context.ObservedValues.TryGetValue("paragraph." + id + ".line_spacing_lines", out var value)
                    && value is not null
                        ? Format(value)
                        : null)
                .ToArray();

            ObserveSectionConsensus(context, key, stated, ParseLineMultiple, evidence);
        }
    }

    private static object ParseLineMultiple(string formatted) =>
        double.Parse(formatted, System.Globalization.CultureInfo.InvariantCulture);

    private static void ObserveRoleFormattingConsensus(
        DocumentModel document, SemanticDocumentModel semantic, ValidationContext context)
    {
        var paragraphs = AllParagraphsById(document);
        var resolver = new EffectiveFormattingResolver();
        var evidence = new EvidenceRecord("effective_run_style", EvidenceAuthority.Derived, "role-consensus", null, false);

        foreach (var role in ConsensusRoles)
        {
            var components = semantic.Components
                .Where(component => component.Role == role)
                .ToArray();
            if (components.Length == 0) continue;

            var key2 = "semantic_component." + PublishedRoleKey(role) + ".";

            // Competing evidence or a low-confidence claim is ambiguous even when the formatting
            // happens to agree, so it is reported as needing review rather than as a fact.
            // Ambiguity is a real signal, not merely "more than one paragraph of this role":
            // several legal-basis paragraphs are normal and must still reach consensus. Only a
            // low-confidence classification, a competing candidate, or an explicitly declared
            // ambiguity naming this candidate forces review.
            var candidateIds = components.Select(component => component.Id).ToHashSet(StringComparer.Ordinal);
            var ambiguous = components.Any(component => component.Status == ClassificationStatus.INFERRED_LOW)
                || components.Any(component => component.CompetingCandidateIds.Count > 0)
                || semantic.Ambiguities.Any(ambiguity =>
                    ambiguity.CandidateIds.Any(id => candidateIds.Contains(id)));

            var ids = components
                .Select(component => component.Evidence.ParagraphId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Distinct(StringComparer.Ordinal)
                .ToArray();

            foreach (var property in new[] { "font_size_pt", "bold", "italic", "alignment" })
            {
                var key = key2 + property;
                var known = new List<string>();
                var unknownRuns = 0;

                foreach (var id in ids)
                {
                    if (!paragraphs.TryGetValue(id, out var paragraph)) { unknownRuns++; continue; }
                    if (paragraph.Runs.Count == 0) { unknownRuns++; continue; }
                    foreach (var run in paragraph.Runs)
                    {
                        string? value;
                        try { value = ConsensusValue(resolver.Resolve(document, paragraph, run), property, resolver); }
                        catch (StyleInheritanceCycleException) { unknownRuns++; continue; }
                        if (value is null) unknownRuns++;
                        else known.Add(value);
                    }
                }

                if (known.Count == 0)
                {
                    // Nothing is known, so publish nothing and leave no stale value behind.
                    context.ObservedValues.Remove(key);
                    context.ObservedValues.Remove("semantic_component." + SnakeRole(role) + "." + property);
                    continue;
                }
                if (ambiguous || unknownRuns > 0 || known.Distinct(StringComparer.Ordinal).Count() != 1)
                {
                    context.MarkUncertain(key);
                    continue;
                }
                context.Observe(key, known[0], evidence);
            }
        }
    }

    private static void ObserveParagraphTargets(DocumentModel document, ValidationContext context)
    {
        var paragraphs = document.Paragraphs.Where(p => p.Runs.Count > 0).ToArray();
        if (paragraphs.Length == 0) return;
        var resolver = new EffectiveFormattingResolver();
        var evidence = new EvidenceRecord("effective_run_style", EvidenceAuthority.Derived, "paragraph-target", null, false);

        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.alignment",
            (f, r) => CanonicalAlignment(f.Alignment.Value) ?? f.Alignment.Value);
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.font_size_pt",
            (f, r) => f.FontSizePt.Value);
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.italic",
            (f, r) => f.Italic.Value);
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.first_line_indent_cm",
            (f, r) => f.FirstLineIndentPt.Value is { } pt ? Round(pt / 72d * 2.54d, 2) : (object?)null);
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.paragraph_spacing_pt",
            (f, r) => f.SpaceAfterPt.Value is { } pt ? Round(pt, 2) : (object?)null);
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.line_spacing_lines",
            (f, r) => LineSpacingLines(f.LineSpacing.Value));
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.line_spacing_pt",
            (f, r) => LineSpacingPoints(f.LineSpacing.Value));
        AddAggregated(paragraphs, resolver, document, context, evidence,
            p => $"paragraph.{p.Id}.font_family",
            (f, r) => f.FontFamily.Value is { Length: > 0 } family ? family : null);
    }

    /// <summary>
    /// Role-scoped body observations, aggregated over the paragraphs the semantic
    /// detector actually classified as body.
    ///
    /// Aggregating every paragraph would fold headings, the national header, the
    /// issuing authority, the addressee list and the signature block into the body
    /// role, so a legitimately different heading would make the body value look
    /// mixed and turn a compliant document into a false NEEDS_REVIEW. The detector
    /// already excludes structurally classified paragraphs when it emits its body
    /// candidates, so those component paragraph ids are the authoritative body set.
    /// </summary>
    private static void ObserveBodyParagraph(DocumentModel document, SemanticDocumentModel semantic, ValidationContext context)
    {
        var bodyParagraphIds = semantic.Components
            .Where(component => component.Role == SemanticRole.Body)
            .Select(component => component.Evidence.ParagraphId)
            .ToHashSet(StringComparer.Ordinal);
        if (bodyParagraphIds.Count == 0) return;

        // Roll the per-paragraph observations of exactly those paragraphs up to the
        // role-level keys the release-pack rules target.
        foreach (var suffix in new[] { "alignment", "font_size_pt", "italic", "first_line_indent_cm", "paragraph_spacing_pt", "line_spacing_lines", "line_spacing_pt", "font_family" })
            RollUpBody(context, bodyParagraphIds, suffix, "semantic_component.body." + suffix);

        // The verified ND30 line-spacing rule targets the bare `line_spacing` property, not
        // `line_spacing_lines`, and its declared unit is "line" (a 1.0-1.5 multiple). The
        // component adapter's `line_spacing` was deliberately dropped above because it folds a
        // line multiple and an absolute point value into one ambiguous field, and the rollup
        // above only publishes the disambiguated `_lines` / `_pt` keys. That left
        // ND30.PL1.I.II.6E.BODY_LINE_SPACING_RANGE with no producer at all, so it evaluated as
        // NOT_EVALUATED for every document and never reported. Republish it here from
        // `line_spacing_lines`, which carries a value only for `lineRule="auto"`, so the
        // published quantity stays unambiguous. An exact/atLeast body stays unobserved and the
        // rollup marks the role uncertain rather than forcing a value.
        RollUpBody(context, bodyParagraphIds, "line_spacing_lines", "semantic_component.body.line_spacing");

        // The release-pack body rules state alignment in lower case ("justify") and compare with
        // ordinal equality, while the canonical mutable-target vocabulary is upper case
        // ("JUSTIFY"). Publishing the upper-case form made a fully justified body fail the rule.
        // Normalize only at this role observation boundary; the mutable target contract keeps its
        // upper-case values untouched.
        if (context.ObservedValues.TryGetValue("semantic_component.body.alignment", out var bodyAlignment)
            && bodyAlignment is string alignmentWord)
        {
            context.Observe("semantic_component.body.alignment", alignmentWord.ToLowerInvariant());
        }
    }

    /// <summary>
    /// Line spacing is observed in exactly one unit, decided by the DOCX line rule.
    ///
    /// A multiple-of-lines rule (for example ND30's 1.0\u20131.5 line range) is only
    /// comparable with <c>lineRule="auto"</c>. An <c>exact</c>/<c>atLeast</c>
    /// spacing is an absolute point value and is a different quantity, so it is
    /// observed under its own key and never folded into the line multiple. A
    /// paragraph that mixes both forms cannot be reduced to one number, so its
    /// own keys stay unobserved rather than being forced to a wrong value.
    /// </summary>
    private static object? LineSpacingLines(LineSpacingModel? spacing) =>
        spacing?.Rule == LineSpacingRule.Auto && spacing.Lines is { } lines ? Round(lines, 4) : null;

    private static object? LineSpacingPoints(LineSpacingModel? spacing) =>
        spacing?.Rule is LineSpacingRule.Exact or LineSpacingRule.AtLeast && spacing.Points is { } points
            ? Round(points, 2)
            : null;

    /// <summary>
    /// Round to the precision the rules are expressed at. Word stores lengths in
    /// twips, so an exact millimetre requirement can sit a fraction under its
    /// bound after conversion; rounding here keeps a compliant document from
    /// failing on a conversion artefact.
    /// </summary>
    private static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);

    private static void AddAggregated(
        IReadOnlyList<ParagraphModel> paragraphs,
        EffectiveFormattingResolver resolver,
        DocumentModel document,
        ValidationContext context,
        EvidenceRecord evidence,
        Func<ParagraphModel, string> keySelector,
        Func<EffectiveFormatting, RunModel?, object?> projector)
    {
        foreach (var paragraph in paragraphs)
        {
            try
            {
                var values = paragraph.Runs
                    .Select(run => projector(resolver.Resolve(document, paragraph, run), run))
                    .Where(value => value is not null && (value is not string s || !string.IsNullOrWhiteSpace(s)))
                    .ToArray();
                if (values.Length == 0) continue;
                var key = keySelector(paragraph);
                if (values.Select(value => Format(value!)).Distinct(StringComparer.Ordinal).Count() == 1)
                    context.Observe(key, values[0], evidence);
                else
                {
                    // Mixed runs in one paragraph cannot be judged as a single value.
                    context.MarkUncertain(key);
                }
            }
            catch (StyleInheritanceCycleException)
            {
                context.MarkUncertain(keySelector(paragraph));
            }
        }
    }

    private static void RollUp(ValidationContext context, string suffix, int paragraphCount, string roleKey)
    {
        var keys = context.ObservedValues.Keys
            .Where(key => key.StartsWith("paragraph.", StringComparison.Ordinal) && key.EndsWith("." + suffix, StringComparison.Ordinal))
            .ToArray();
        if (keys.Length == 0) return;
        if (keys.Length < paragraphCount)
        {
            // At least one paragraph could not be observed, so the whole body value is unknown.
            context.MarkUncertain(roleKey);
            return;
        }
        var distinct = keys.Select(key => Format(context.ObservedValues[key]!)).Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 1) context.Observe(roleKey, context.ObservedValues[keys[0]]);
        else context.MarkUncertain(roleKey);
    }

    /// <summary>
    /// Roll up exactly the detector-identified body paragraphs. A body paragraph
    /// with no observation for the property keeps the role value uncertain rather
    /// than being silently skipped.
    /// </summary>
    private static void RollUpBody(ValidationContext context, IReadOnlySet<string> bodyParagraphIds, string suffix, string roleKey)
    {
        var observed = new Dictionary<string, object>(StringComparer.Ordinal);
        var missing = false;
        foreach (var paragraphId in bodyParagraphIds)
        {
            var key = $"paragraph.{paragraphId}.{suffix}";
            if (context.ObservedValues.TryGetValue(key, out var value) && value is not null)
            {
                observed[key] = value;
                continue;
            }
            missing = true;
        }
        if (observed.Count == 0) return;
        if (missing)
        {
            context.MarkUncertain(roleKey);
            return;
        }
        var distinct = observed.Values.Select(Format).Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length == 1) context.Observe(roleKey, observed.Values.First());
        else context.MarkUncertain(roleKey);
    }

    /// <summary>Document-level effective font, judged over every body run.</summary>
    private static void ObserveDocumentDefaults(DocumentModel document, ValidationContext context)
    {
        var resolver = new EffectiveFormattingResolver();
        var evidence = new EvidenceRecord("effective_run_style", EvidenceAuthority.Derived, "document", null, false);
        var families = new List<string>();
        var colors = new List<string>();
        foreach (var paragraph in document.Paragraphs)
        {
            foreach (var run in paragraph.Runs)
            {
                try
                {
                    var resolved = resolver.Resolve(document, paragraph, run);
                    if (resolved.FontFamily.Value is { Length: > 0 } family) families.Add(family);
                    // Count every run: an unknown colour must be recorded as unknown so the
                    // completeness check below can see it, not silently dropped.
                    colors.Add(resolved.Color.Value is { Length: > 0 } stated ? stated : string.Empty);
                }
                catch (StyleInheritanceCycleException)
                {
                    // The run's effective values are unknown, so it counts as an unknown run
                    // rather than being dropped; otherwise known black plus a cycle falsely passed.
                    context.MarkUncertain("document.effective_font_family");
                    colors.Add(string.Empty);
                }
            }
        }
        ObserveConsensus(context, "document.effective_font_family", families, evidence);

        // ND30 states the colour as the word "black", while the DOCX states an explicit sRGB hex,
        // and the rule compares with ordinal equality. Projection has to be complete across runs:
        //   every run explicitly black            -> "black", rule passes;
        //   every run known and at least one not  -> that real non-black value, rule fails;
        //   every run unknown (auto or theme)     -> nothing published, rule not evaluated;
        //   some known and some unknown           -> uncertain, rule needs review.
        // An automatic or theme-derived colour is never reported as a resolved black.
        var knownColors = new List<string>();
        var unknownRuns = 0;
        foreach (var color in colors)
        {
            var resolved = ResolveExplicitColor(color);
            if (resolved is null) unknownRuns++;
            else knownColors.Add(resolved);
        }

        if (colors.Count > 0 && unknownRuns == colors.Count) { /* nothing known: publish nothing */ }
        else if (unknownRuns > 0) context.MarkUncertain("document.effective_font_color");
        else if (knownColors.Count > 0)
        {
            var allBlack = knownColors.All(value => string.Equals(value, "black", StringComparison.Ordinal));
            context.Observe(
                "document.effective_font_color",
                allBlack ? "black" : knownColors.First(value => !string.Equals(value, "black", StringComparison.Ordinal)),
                evidence);
        }
    }

    /// <summary>
    /// Maps an explicitly written OOXML colour onto the rule vocabulary, or returns null when the
    /// colour is not actually known. Automatic and theme-derived colours are unresolved: they are
    /// never resolved to a value, least of all to black.
    /// </summary>
    private static string? ResolveExplicitColor(string color)
    {
        var trimmed = color.Trim();
        if (trimmed.Length == 0) return null;
        if (trimmed.Equals("auto", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.StartsWith("theme", StringComparison.OrdinalIgnoreCase)) return null;
        if (trimmed.Length == 6 && trimmed.All(Uri.IsHexDigit))
            return trimmed.Equals("000000", StringComparison.OrdinalIgnoreCase) ? "black" : trimmed.ToUpperInvariant();
        // Any other vocabulary (a named colour such as "red") is already explicit.
        return trimmed;
    }

    private static void ObserveConsensus(ValidationContext context, string key, List<string> values, EvidenceRecord evidence)
    {
        if (values.Count == 0) return;
        var distinct = values.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (distinct.Length == 1) context.Observe(key, distinct[0], evidence);
        else context.MarkUncertain(key);
    }

    /// <summary>Section-level page setup, converted to the units the rules require.</summary>
    private static void ObserveSections(DocumentModel document, ValidationContext context)
    {
        var evidence = new EvidenceRecord("section_properties", EvidenceAuthority.Derived, "sections", null, false);
        var orientation = new List<string>();
        var pageSize = new List<string>();
        foreach (var section in document.Sections)
        {
            var suffix = $".{section.Id}";
            context.Observe("section.orientation" + suffix, NormalizeOrientation(section.Orientation), evidence);
            orientation.Add(NormalizeOrientation(section.Orientation));
            context.Observe("section.page_size" + suffix, ResolvePageSize(section), evidence);
            pageSize.Add(ResolvePageSize(section));
            ObserveTwips(context, "section.margin_top_mm" + suffix, section.MarginTopTwips, evidence);
            ObserveTwips(context, "section.margin_right_mm" + suffix, section.MarginRightTwips, evidence);
            ObserveTwips(context, "section.margin_bottom_mm" + suffix, section.MarginBottomTwips, evidence);
            ObserveTwips(context, "section.margin_left_mm" + suffix, section.MarginLeftTwips, evidence);
        }
        if (document.Sections.Count == 0) return;

        var marginNames = new[] { "margin_top_mm", "margin_right_mm", "margin_bottom_mm", "margin_left_mm" };

        // The verified ND30 section rules target the bare `section.page_size`,
        // `section.orientation` and `section.margin_*_mm` keys, while the per-section
        // observations above are published under `section.<name>.<sectionId>`. Nothing published
        // the bare form, so those six rules had no producer and evaluated as NOT_EVALUATED for
        // every document, including the right-margin rule the synthetic pilot applies.
        //
        // Both the `.all` form and the bare form are published under the same consensus rule:
        // every section must state the property and they must all agree. A document whose
        // sections disagree, or where only some state it, is uncertain (NEEDS_REVIEW) rather than
        // silently reduced to the one known section.
        // Orientation and page size follow the same consensus as the margins, and the `.all` form is
        // kept for compatibility. Neither ever publishes the literal "mixed" or "unknown": those
        // would be read by an ordinal-equality rule as a genuine non-conforming value.
        ObserveStringConsensus(context, "section.orientation.all", orientation.Select(KnownOrNull).ToArray(), evidence);
        ObserveStringConsensus(context, "section.orientation", orientation.Select(KnownOrNull).ToArray(), evidence);
        ObserveStringConsensus(context, "section.page_size.all", pageSize.Select(KnownOrNull).ToArray(), evidence);
        ObserveStringConsensus(context, "section.page_size", pageSize.Select(KnownOrNull).ToArray(), evidence);

        foreach (var name in marginNames)
        {
            var stated = document.Sections
                .Select(section => Format(Twips(section.MarginTopTwips, section.MarginRightTwips, section.MarginBottomTwips, section.MarginLeftTwips, name)))
                .ToArray();

            ObserveSectionConsensus(context, "section." + name + ".all", stated, ParseMillimetres, evidence);
            ObserveSectionConsensus(context, "section." + name, stated, ParseMillimetres, evidence);
        }
    }

    private static object ParseMillimetres(string formatted) =>
        double.Parse(formatted, System.Globalization.CultureInfo.InvariantCulture);

    private static void ObserveTwips(ValidationContext context, string key, int? twips, EvidenceRecord evidence)
    {
        // Convert directly. This used to delegate to Twips(twips, null, null, null, null), whose
        // `which` switch only recognises the four margin names and therefore returned null for a
        // null `which` on every call, so no per-section margin was ever published.
        if (twips is null) return;
        context.Observe(key, Round(twips.Value / TwipsPerMillimetre, 1), evidence);
    }

    /// <summary>
    /// Publish a section-scoped property only when every section states it and they all agree.
    ///
    /// Three distinct outcomes, none of them invented:
    /// no section states it -> no observation at all, so the rule reports NOT_EVALUATED;
    /// some state it and some do not, or they disagree -> uncertain, so the rule reports
    /// NEEDS_REVIEW; all state the same value -> that value is observed.
    /// </summary>
    private static void ObserveSectionConsensus(
        ValidationContext context, string key, IReadOnlyList<string?> stated, Func<string, object> parse, EvidenceRecord evidence)
    {
        // A missing value can arrive as null or as a blank formatted string; both mean the
        // document did not state the property and neither may be parsed as a number.
        var values = stated.Select(value => string.IsNullOrWhiteSpace(value) ? null : value!.Trim()).ToArray();
        if (values.Length == 0 || values.All(value => value is null)) return;
        if (values.Any(value => value is null))
        {
            context.MarkUncertain(key);
            return;
        }
        var distinct = values.Select(value => value!).Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length != 1)
        {
            context.MarkUncertain(key);
            return;
        }
        context.Observe(key, parse(distinct[0]), evidence);
    }

    /// <summary>
    /// Word consensus for a whole-document string property such as page size or orientation.
    ///
    /// Every contributing value must be known and identical, otherwise the property is unknown.
    /// A sentinel such as "unknown" or "mixed" is never published, because the release-pack rules
    /// compare with ordinal equality and would read that sentinel as a real, non-conforming value.
    /// </summary>
    private static void ObserveStringConsensus(
        ValidationContext context, string key, IReadOnlyList<string?> stated, EvidenceRecord evidence)
    {
        ObserveSectionConsensus(
            context, key, stated,
            value => value,
            evidence);
    }

    private static string? KnownOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) || string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;

    private static object? Twips(int? top, int? right, int? bottom, int? left, string? which)
    {
        int? raw = which switch
        {
            "margin_top_mm" => top,
            "margin_right_mm" => right,
            "margin_bottom_mm" => bottom,
            "margin_left_mm" => left,
            _ => null,
        };
        return raw is null ? null : Round(raw.Value / TwipsPerMillimetre, 1);
    }

    private static string NormalizeOrientation(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "landscape" => "landscape",
        "portrait" or "" => "portrait",
        _ => "unknown",
    };

    /// <summary>A4 is 11906 x 16838 twips. Only an exact match is claimed.</summary>
    private static string ResolvePageSize(SectionModel section)
    {
        if (section.PageWidthTwips is not { } width || section.PageHeightTwips is not { } height) return "unknown";
        var longSide = Math.Max(width, height);
        var shortSide = Math.Min(width, height);
        if (Math.Abs(longSide - 16838) <= 8 && Math.Abs(shortSide - 11906) <= 8) return "A4";
        if (Math.Abs(longSide - 15840) <= 8 && Math.Abs(shortSide - 12240) <= 8) return "Letter";
        return $"custom:{width}x{height}";
    }

    public static string? CanonicalAlignment(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "LEFT" => "LEFT", "CENTER" => "CENTER", "RIGHT" => "RIGHT",
        "BOTH" or "JUSTIFY" or "JUSTIFIED" => "JUSTIFY",
        _ => null,
    };

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        bool b => b ? "true" : "false",
        double d => d.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
        _ => value.ToString() ?? string.Empty,
    };

    /// <summary>
    /// Enumerate the profile properties this DOCX exposes for mutation.
    ///
    /// Paragraph-scoped targets are restricted to the paragraphs the semantic
    /// detector assigned to the role the rule actually targets. A rule about the
    /// body (for example the body font size) must therefore never be offered
    /// against a type heading, the issuing authority or the signature block, even
    /// though those paragraphs also carry direct formatting.
    ///
    /// Section and document scope carry no paragraph id. A paragraph target also
    /// requires the property to be present as direct formatting, because the
    /// executor refuses to write through style inheritance.
    /// </summary>
    public static IReadOnlyList<MutableTarget> EnumerateMutableTargets(DocumentModel document, SemanticDocumentModel semantic, FormattingProfile profile)
    {
        var resolver = new EffectiveFormattingResolver();
        var bodyParagraphIds = semantic.Components
            .Where(component => component.Role == SemanticRole.Body)
            .Select(component => component.Evidence.ParagraphId)
            .ToHashSet(StringComparer.Ordinal);
        var roleParagraphIds = semantic.Components
            .GroupBy(component => SnakeRole(component.Role))
            .ToDictionary(
                group => group.Key,
                group => group.Select(component => component.Evidence.ParagraphId).ToHashSet(StringComparer.Ordinal),
                StringComparer.OrdinalIgnoreCase);

        var targets = new List<MutableTarget>();
        foreach (var target in profile.MutationTargets)
        {
            switch (target.Scope)
            {
                case "document":
                {
                    var families = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var paragraph in document.Paragraphs)
                        foreach (var run in paragraph.Runs)
                        {
                            try
                            {
                                if (resolver.Resolve(document, paragraph, run).FontFamily.Value is { Length: > 0 } family) families.Add(family);
                            }
                            catch (StyleInheritanceCycleException) { return targets; }
                        }
                    targets.Add(new MutableTarget(string.Empty, target.Property,
                        families.Count == 1 ? families.First() : null, target.Unit, target.LabelVi, target.RuleId, false));
                    break;
                }
                case "section":
                {
                    var section = document.Sections.FirstOrDefault();
                    if (section is null) break;
                    var observed = document.Sections.Count == 1 ? ObservedValueForSection(target.Property, section) : null;
                    targets.Add(new MutableTarget(section.Id, target.Property, observed, target.Unit, target.LabelVi, target.RuleId, false));
                    break;
                }
                default:
                {
                    // The rule's observation key names the semantic role it governs.
                    var role = RoleFromObservationKey(target.ObservationKey);
                    var allowed = role is null
                        ? null as IReadOnlySet<string>
                        : role.Equals("body", StringComparison.OrdinalIgnoreCase)
                            ? bodyParagraphIds
                            : roleParagraphIds.TryGetValue(role, out var ids) ? ids : new HashSet<string>(StringComparer.Ordinal);

                    foreach (var paragraph in document.Paragraphs)
                    {
                        if (allowed is not null && !allowed.Contains(paragraph.Id)) continue;
                        if (paragraph.Runs.Count == 0) continue;
                        var direct = DirectValue(paragraph, target.Property);
                        if (direct is null) continue;
                        targets.Add(new MutableTarget(paragraph.Id, target.Property, direct, target.Unit, target.LabelVi, target.RuleId, true));
                    }
                    break;
                }
            }
        }
        return targets;
    }

    /// <summary>
    /// Extract the semantic role a rule governs from its observation key, for
    /// example <c>semantic_component.body.font_size_pt</c> -> <c>body</c>.
    /// Returns null when the key is not role-scoped.
    /// </summary>
    internal static string? RoleFromObservationKey(string observationKey)
    {
        const string prefix = "semantic_component.";
        if (!observationKey.StartsWith(prefix, StringComparison.Ordinal)) return null;
        var rest = observationKey[prefix.Length..];
        var lastDot = rest.LastIndexOf('.');
        return lastDot <= 0 ? null : rest[..lastDot];
    }

    private static string SnakeRole(SemanticRole role) =>
        System.Text.RegularExpressions.Regex.Replace(role.ToString(), "([a-z0-9])([A-Z])", "$1_$2").ToLowerInvariant();

    private static string? ObservedValueForSection(string property, SectionModel section) => property switch
    {
        "section.page_size" => ResolvePageSize(section),
        "section.orientation" => NormalizeOrientation(section.Orientation),
        "section.margin_top_mm" => Format(section.MarginTopTwips is { } t ? Round(t / TwipsPerMillimetre, 1) : (object?)null),
        "section.margin_right_mm" => Format(section.MarginRightTwips is { } r ? Round(r / TwipsPerMillimetre, 1) : (object?)null),
        "section.margin_bottom_mm" => Format(section.MarginBottomTwips is { } b ? Round(b / TwipsPerMillimetre, 1) : (object?)null),
        "section.margin_left_mm" => Format(section.MarginLeftTwips is { } l ? Round(l / TwipsPerMillimetre, 1) : (object?)null),
        _ => null,
    };

    /// <summary>
    /// Direct-only reading used to decide whether a paragraph property can be
    /// written. The executor never creates a direct property that was absent,
    /// because that would silently outrank the author's style.
    /// </summary>
    private static string? DirectValue(ParagraphModel paragraph, string property) => property switch
    {
        "paragraph.alignment" => CanonicalAlignment(paragraph.DirectFormatting.Alignment),
        "paragraph.first_line_indent_cm" => paragraph.DirectFormatting.FirstLineIndentPt is { } pt ? Format(Round(pt / 72d * 2.54d, 2)) : null,
        "paragraph.spacing_after_pt" => paragraph.DirectFormatting.SpaceAfterPt is { } pt ? Format(Round(pt, 2)) : null,
        "paragraph.line_spacing_lines" => paragraph.DirectFormatting.LineSpacing is { Rule: LineSpacingRule.Auto } auto && auto.Lines is { } l ? Format(Round(l, 4)) : null,
        "run.font_size_pt" => DistinctRunFontSize(paragraph),
        _ => null,
    };

    private static string? DistinctRunFontSize(ParagraphModel paragraph)
    {
        var sizes = paragraph.Runs
            .Select(run => run.DirectFormatting.FontSizePt)
            .Where(size => size is not null)
            .Select(size => size!.Value)
            .Distinct()
            .ToArray();
        return sizes.Length == 1 ? Format(sizes[0]) : null;
    }
}