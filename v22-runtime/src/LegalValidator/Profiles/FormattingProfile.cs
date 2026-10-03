using System.Security.Cryptography;
using System.Text;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Model;

namespace Nd30.LegalValidator.Profiles;

/// <summary>
/// One formatting property the company profile can actually enforce and mutate.
/// Every target is bound to an authoritative rule id, an observation key that the
/// DOCX adapter really produces, and a canonical unit. Nothing is claimed here
/// that the adapter cannot observe or the executor cannot write.
/// </summary>
public sealed record ProfileMutationTarget(
    string Property,
    string ObservationKey,
    string Unit,
    string LabelVi,
    string Scope,
    string ConstraintKind,
    string RuleId);

/// <summary>
/// One document type covered by profile v1. `AbbreviationRuleId` is scoped per
/// type on purpose: the release pack ships every <c>ND30.PL3.I.ABBR.*</c> rule
/// with a null applicability, so an unscoped run would fail all 30 of them on
/// every document.
/// </summary>
public sealed record ProfileDocumentType(
    string TypeKey,
    string LabelVi,
    string DocumentType,
    string? AbbreviationRuleId,
    bool HasTypeHeading);

/// <summary>
/// Versioned company formatting profile. The digest is computed over the whole
/// canonical definition so an inspection can be bound to the exact profile and
/// rule subset that produced it, and a later apply can refuse to act on a
/// proposal issued under a different profile.
/// </summary>
public sealed class FormattingProfile
{
    public string Id { get; }
    public string Version { get; }
    public string RulePackId { get; }
    public string Digest { get; }
    public IReadOnlyList<ProfileDocumentType> DocumentTypes { get; }
    public IReadOnlyList<ProfileMutationTarget> MutationTargets { get; }
    public IReadOnlyList<string> GrantedCapabilities { get; }

    private readonly IReadOnlyList<string> _baseRuleIds;

    internal FormattingProfile(
        string id,
        string version,
        string rulePackId,
        IReadOnlyList<ProfileDocumentType> documentTypes,
        IReadOnlyList<ProfileMutationTarget> mutationTargets,
        IReadOnlyList<string> grantedCapabilities,
        IReadOnlyList<string> baseRuleIds,
        IReadOnlyList<string> ruleContentDigest)
    {
        Id = id;
        Version = version;
        RulePackId = rulePackId;
        DocumentTypes = documentTypes;
        MutationTargets = mutationTargets;
        GrantedCapabilities = grantedCapabilities;
        _baseRuleIds = baseRuleIds;
        RuleContentDigest = ruleContentDigest.Count > 0
            ? string.Join(",", ruleContentDigest)
            : ComputeRuleContentDigest([], rulePackId);
        Digest = ComputeDigest(id, version, rulePackId, documentTypes, mutationTargets, grantedCapabilities, baseRuleIds, RuleContentDigest);
    }

    /// <summary>
    /// Per-rule content digests for every rule the profile can put in scope.
    ///
    /// The profile digest must change when the *content* of a selected rule
    /// changes, not only when its ID list changes. Otherwise a corrected
    /// constraint or a changed applicability condition would leave a proposal
    /// issued under the old rule looking current, and an apply could act on a
    /// stale recommendation.
    /// </summary>
    public string RuleContentDigest { get; }

    public IReadOnlyList<string> BaseRuleIds => _baseRuleIds;

    /// <summary>Resolve the full scoped rule subset for one detected document type.</summary>
    public IReadOnlyList<string> ResolveRuleSubset(RuleCatalog catalog, string documentTypeKey)
    {
        var type = DocumentTypes.FirstOrDefault(x => string.Equals(x.TypeKey, documentTypeKey, StringComparison.OrdinalIgnoreCase));
        var ids = new List<string>(_baseRuleIds);
        if (type?.AbbreviationRuleId is { } abbreviation
            && catalog.ReleaseRuleIds.Contains(abbreviation))
            ids.Add(abbreviation);
        // Preserve catalog order and never evaluate a rule outside the release pack.
        var wanted = ids.ToHashSet(StringComparer.Ordinal);
        return catalog.Rules
            .Where(rule => wanted.Contains(rule.Id) && catalog.ReleaseRuleIds.Contains(rule.Id))
            .Select(rule => rule.Id)
            .ToList();
    }

    public ProfileDocumentType? FindDocumentType(string key) =>
        DocumentTypes.FirstOrDefault(x => string.Equals(x.TypeKey, key, StringComparison.OrdinalIgnoreCase));

    public ProfileMutationTarget? FindMutationTarget(string property) =>
        MutationTargets.FirstOrDefault(x => string.Equals(x.Property, property, StringComparison.OrdinalIgnoreCase));

    public bool IsKnownDocumentType(string key) => FindDocumentType(key) is not null;

    private static string ComputeDigest(
        string id,
        string version,
        string rulePackId,
        IReadOnlyList<ProfileDocumentType> documentTypes,
        IReadOnlyList<ProfileMutationTarget> mutationTargets,
        IReadOnlyList<string> grantedCapabilities,
        IReadOnlyList<string> baseRuleIds,
        string ruleContentDigest)
    {
        var canonical = new StringBuilder();
        canonical.Append("profile=").Append(id).Append('\n');
        canonical.Append("version=").Append(version).Append('\n');
        canonical.Append("rule_pack=").Append(rulePackId).Append('\n');
        foreach (var type in documentTypes.OrderBy(x => x.TypeKey, StringComparer.Ordinal))
            canonical.Append("type=").Append(type.TypeKey).Append('|').Append(type.DocumentType).Append('|')
                .Append(type.AbbreviationRuleId ?? "-").Append('|').Append(type.HasTypeHeading ? "1" : "0").Append('\n');
        foreach (var target in mutationTargets.OrderBy(x => x.Property, StringComparer.Ordinal))
            canonical.Append("target=").Append(target.Property).Append('|').Append(target.ObservationKey).Append('|')
                .Append(target.Unit).Append('|').Append(target.Scope).Append('|').Append(target.ConstraintKind).Append('|')
                .Append(target.RuleId).Append('\n');
        foreach (var capability in grantedCapabilities.OrderBy(x => x, StringComparer.Ordinal))
            canonical.Append("capability=").Append(capability).Append('\n');
        foreach (var rule in baseRuleIds.OrderBy(x => x, StringComparer.Ordinal))
            canonical.Append("rule=").Append(rule).Append('\n');
        canonical.Append("rule_content=").Append(ruleContentDigest).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    /// <summary>
    /// Digest of the normalised content of each rule. Every field that can change
    /// a verdict is included: target, constraint, applicability, temporality,
    /// source locator, maturity, severity, autofix policy and required
    /// capabilities.
    /// </summary>
    public static IReadOnlyList<string> ComputeRuleContentDigests(RuleCatalog catalog, IEnumerable<string> ruleIds)
    {
        var wanted = ruleIds.ToHashSet(StringComparer.Ordinal);
        return catalog.Rules
            .Where(rule => wanted.Contains(rule.Id) && catalog.ReleaseRuleIds.Contains(rule.Id))
            .OrderBy(rule => rule.Id, StringComparer.Ordinal)
            .Select(rule => $"{rule.Id}={HashRuleContent(rule)}")
            .ToList();
    }

    /// <summary>Bare 64-character content digest of a single release-pack rule.</summary>
    public static string ComputeRuleContentDigest(RuleCatalog catalog, string ruleId) =>
        catalog.Rules.FirstOrDefault(rule => rule.Id == ruleId && catalog.ReleaseRuleIds.Contains(ruleId)) is { } rule
            ? HashRuleContent(rule)
            : string.Empty;

    private static string ComputeRuleContentDigest(IEnumerable<string> digests, string rulePackId)
    {
        var canonical = new StringBuilder();
        canonical.Append("pack=").Append(rulePackId).Append('\n');
        foreach (var digest in digests.OrderBy(x => x, StringComparer.Ordinal)) canonical.Append(digest).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static string HashRuleContent(RuleDefinition rule)
    {
        var canonical = new StringBuilder();
        canonical.Append("id=").Append(rule.Id).Append('\n');
        canonical.Append("regime=").Append(rule.Regime).Append('\n');
        canonical.Append("domain=").Append(rule.Domain).Append('\n');
        canonical.Append("maturity=").Append(rule.Maturity).Append('\n');
        canonical.Append("source_id=").Append(rule.Source.SourceId).Append('\n');
        canonical.Append("source_locator=").Append(rule.Source.Locator).Append('\n');
        canonical.Append("effective_from=").Append(rule.EffectiveFrom?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "-").Append('\n');
        canonical.Append("effective_to=").Append(rule.EffectiveTo?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) ?? "-").Append('\n');
        canonical.Append("target=").Append(rule.Target.ObjectType).Append('|').Append(rule.Target.Role ?? "-").Append('|').Append(rule.Target.Property ?? "-").Append('\n');
        canonical.Append("constraint=").Append(CanonicalValue(rule.Constraint)).Append('\n');
        canonical.Append("applicability=").Append(rule.Applicability is null ? "-" : CanonicalValue(rule.Applicability)).Append('\n');
        canonical.Append("severity=").Append(rule.Severity).Append('\n');
        canonical.Append("autofix=").Append(rule.AutofixPolicy).Append('\n');
        foreach (var capability in rule.RequiresCapabilities.OrderBy(x => x, StringComparer.Ordinal))
            canonical.Append("capability=").Append(capability).Append('\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    /// <summary>Stable textual form of a nested rule value so key order cannot change the digest.</summary>
    private static string CanonicalValue(object? value)
    {
        var map = value as IReadOnlyDictionary<string, object?>;
        if (map is not null)
            return "{" + string.Join(",", map
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => entry.Key + ":" + CanonicalValue(entry.Value))) + "}";
        if (value is System.Collections.IEnumerable sequence and not string)
            return "[" + string.Join(",", sequence.Cast<object?>().Select(CanonicalValue)) + "]";
        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
    }
}

/// <summary>
/// Company formatting profile v1 for the five priority administrative document
/// types. Internal values that are still provisional are marked in
/// <see cref="ProfileDocumentType"/> comments and must be confirmed before the
/// profile is treated as the approved company standard.
/// </summary>
public static class FormattingProfileV1
{
    public const string Id = "HOATIEU-MIENBAC-ADMIN-V1";
    public const string Version = "1.0.0-provisional";
    public const string RulePackId = "ADMIN-ND30-VERIFIED-RC-V20";

    public static readonly string CongVanKey = "cong_van";

    /// <summary>
    /// Capabilities the DOCX adapter can genuinely honour. Anything requiring a
    /// workflow log, records system, signature verification or layout analysis is
    /// deliberately absent so those rules stay NOT_EVALUATED instead of failing.
    /// </summary>
    public static readonly IReadOnlyList<string> GrantedCapabilities = new[]
    {
        "semantic_component_detection",
        "document_type_classification",
        "effective_run_style",
        "effective_paragraph_style",
        "section_properties",
        "document_encoding",
    };

/// <summary>
/// Observation properties the adapter really emits. A rule is only in scope
/// when its target property appears here; this is what keeps management,
/// records, workflow and Party-document rules out of a formatter run.
///
/// Content properties (<c>text</c>, <c>present</c>) are deliberately excluded:
/// a formatter cannot rewrite document wording, so a content rule is a review
/// concern and must not be reported as a formatting failure.
/// </summary>
public static readonly IReadOnlySet<string> ObservableProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "uppercase", "prefix",
        "font_size_pt", "bold", "italic", "alignment",
        "line_spacing", "line_spacing_pt", "first_line_indent_cm", "paragraph_spacing_pt",
        "effective_font_family", "effective_font_color",
        "page_size", "orientation",
        "margin_top_mm", "margin_right_mm", "margin_bottom_mm", "margin_left_mm",
    };

    /// <summary>
/// Object types a formatter run may look at. `document_metadata` is included
/// only for type-scoped rules such as the document-type abbreviation; its other
/// rules stay out because their target properties are never observed.
/// </summary>
/// <summary>
/// Properties a rule in scope may target but the adapter does not observe yet.
/// The rule still enters the subset and evaluates as NOT_EVALUATED with a
/// declared missing observation, which is honest; it must never be invented.
/// `document_type_abbreviation` is here because ND30 special-cases the
/// abbreviation form (for example a two-word type is not simply its initials),
/// so deriving it mechanically would produce a wrong verdict.
/// </summary>
public static readonly IReadOnlySet<string> PendingObservationProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "document_type_abbreviation",
    };

public static readonly IReadOnlyList<string> ScopedObjectTypes = new[] { "semantic_component", "document", "section", "document_metadata" };

    /// <summary>
    /// Mutation targets this build can actually apply.
    ///
    /// <c>document.effective_font_family</c> is deliberately NOT listed. Its observation
    /// is the resolved (inherited) family of top-level body paragraphs only: it excludes
    /// table cells, headers and footers, and cannot distinguish a direct font from an
    /// inherited one. There is no document-scope mutation kind, so an apply would be
    /// routed to the section resolver and could never execute, and the webapp rejects the
    /// empty document-scope target id outright. Advertising it therefore offered a change
    /// the server always refused. The ND30 font-family rule is still evaluated and still
    /// reported as a finding; only the apply offer is withdrawn.
    /// </summary>
    public static readonly IReadOnlyList<ProfileMutationTarget> MutationTargets = new[]
    {
        new ProfileMutationTarget("paragraph.alignment", "semantic_component.body.alignment", "", "C\u0103n l\u1ec1 \u0111o\u1ea1n", "paragraph", "alignment", "ND30.PL1.I.II.6E.BODY_JUSTIFIED"),
        new ProfileMutationTarget("paragraph.first_line_indent_cm", "semantic_component.body.first_line_indent_cm", "cm", "Th\u1ee5t l\u1ec1 \u0111\u1ea7u d\u00f2ng", "paragraph", "length_cm", "ND30.PL1.I.II.6E.FIRST_LINE_INDENT"),
        new ProfileMutationTarget("paragraph.spacing_after_pt", "semantic_component.body.paragraph_spacing_pt", "pt", "Kho\u1ea3ng c\u00e1ch sau \u0111o\u1ea1n", "paragraph", "length_pt", "ND30.PL1.I.II.6E.PARAGRAPH_GAP_MIN"),
        new ProfileMutationTarget("paragraph.line_spacing_lines", "semantic_component.body.line_spacing", "d\u00f2ng", "Gi\u00e3n d\u00f2ng theo b\u1ed9i s\u1ed1 d\u00f2ng", "paragraph", "line_spacing_lines", "ND30.PL1.I.II.6E.BODY_LINE_SPACING_RANGE"),
        new ProfileMutationTarget("run.font_size_pt", "semantic_component.body.font_size_pt", "pt", "C\u1ee1 ch\u1eef \u0111o\u1ea1n", "paragraph", "length_pt", "ND30.PL1.I.II.6E.BODY_SIZE"),
        new ProfileMutationTarget("section.page_size", "section.page_size", "", "Kh\u1ed5 gi\u1ea5y", "section", "page_size", "ND30.PL1.I.GENERAL.PAGE_SIZE_A4"),
        new ProfileMutationTarget("section.orientation", "section.orientation", "", "H\u01b0\u1edbng trang", "section", "orientation", "ND30.PL1.I.GENERAL.ORIENTATION_PORTRAIT_DEFAULT"),
        new ProfileMutationTarget("section.margin_top_mm", "section.margin_top_mm", "mm", "L\u1ec1 tr\u00ean", "section", "length_mm", "ND30.PL1.I.GENERAL.MARGIN_TOP"),
        new ProfileMutationTarget("section.margin_right_mm", "section.margin_right_mm", "mm", "L\u1ec1 ph\u1ea3i", "section", "length_mm", "ND30.PL1.I.GENERAL.MARGIN_RIGHT"),
        new ProfileMutationTarget("section.margin_bottom_mm", "section.margin_bottom_mm", "mm", "L\u1ec1 d\u01b0\u1edbi", "section", "length_mm", "ND30.PL1.I.GENERAL.MARGIN_BOTTOM"),
        new ProfileMutationTarget("section.margin_left_mm", "section.margin_left_mm", "mm", "L\u1ec1 tr\u00e1i", "section", "length_mm", "ND30.PL1.I.GENERAL.MARGIN_LEFT"),
    };

    /// <summary>
    /// The five priority types. `HasTypeHeading` is false only for the official
    /// letter, which by ND30 carries no document-type heading.
    /// </summary>
    public static readonly IReadOnlyList<ProfileDocumentType> DocumentTypes = new[]
    {
        new ProfileDocumentType(CongVanKey, "C\u00f4ng v\u0103n", "official_letter", null, false),
        new ProfileDocumentType("quyet_dinh", "Quy\u1ebft \u0111\u1ecbnh", "quyet_dinh", "ND30.PL3.I.ABBR.QUYET_DINH", true),
        new ProfileDocumentType("bao_cao", "B\u00e1o c\u00e1o", "report", "ND30.PL3.I.ABBR.BAO_CAO", true),
        new ProfileDocumentType("ke_hoach", "K\u1ebf ho\u1ea1ch", "ke_hoach", "ND30.PL3.I.ABBR.KE_HOACH", true),
        new ProfileDocumentType("to_trinh", "T\u1edd tr\u00ecnh", "submission", "ND30.PL3.I.ABBR.TO_TRINH", true),
    };

/// <summary>
/// Resolve the generic (type-independent) rule subset against the loaded
/// catalog.
///
/// Selection is driven by evidence and business scope only:
///  - the target must be a formatting object type the adapter can read;
///  - the target property must be observable from the DOCX;
///  - every required capability must be honestly granted.
///
/// It deliberately does NOT filter on the autofix policy. Whether the executor
/// can currently repair a finding is a mutation-eligibility question, not a
/// validation-scope question; dropping review-only rules would hide real findings
/// and would make the formatter look compliant by shrinking its own coverage.
/// </summary>
public static IReadOnlyList<string> ResolveBaseRuleIds(RuleCatalog catalog)
{
    var ids = new List<string>();
    foreach (var rule in catalog.Rules)
    {
        if (!catalog.ReleaseRuleIds.Contains(rule.Id)) continue;
        if (!ScopedObjectTypes.Contains(rule.Target.ObjectType, StringComparer.OrdinalIgnoreCase)) continue;
        if (string.IsNullOrWhiteSpace(rule.Target.Property)) continue;
        if (!ObservableProperties.Contains(rule.Target.Property!)) continue;
        if (rule.RequiresCapabilities.Any(capability => !GrantedCapabilities.Contains(capability, StringComparer.Ordinal))) continue;
        ids.Add(rule.Id);
    }
    return ids;
}

    /// <summary>
/// Load the profile and bind it to the verified catalog. The digest covers the
/// profile values and the full content of every rule that can enter scope, so an
/// inspection can be bound to the exact profile and rule set that produced it.
/// </summary>
public static FormattingProfile Load(RuleCatalog catalog)
{
    var baseRuleIds = ResolveBaseRuleIds(catalog);
    var allCandidateIds = baseRuleIds
        .Concat(DocumentTypes.Where(type => type.AbbreviationRuleId is not null).Select(type => type.AbbreviationRuleId!))
        .ToHashSet(StringComparer.Ordinal);
    return new FormattingProfile(
        Id,
        Version,
        RulePackId,
        DocumentTypes,
        MutationTargets,
        GrantedCapabilities,
        baseRuleIds,
        FormattingProfile.ComputeRuleContentDigests(catalog, allCandidateIds));
}
}