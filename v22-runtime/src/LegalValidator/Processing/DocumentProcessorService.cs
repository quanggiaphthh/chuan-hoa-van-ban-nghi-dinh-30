using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Package;
using Nd30.DocumentEngine.Parsing;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Mutation;
using Nd30.LegalValidator.Profiles;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
using Nd30.SemanticDetector.Detection;
using Nd30.SemanticDetector.Model;

namespace Nd30.LegalValidator.Processing;

public sealed record ParagraphAlignmentInspection(string ParagraphId, string Text, string? DirectAlignment, string? IssueId);

/// <summary>Everything an inspection binds to so a later apply can refuse a proposal made under a different profile.</summary>
public sealed record ProfileBinding(
    string Id,
    string Version,
    string Digest,
    string RulePackId,
    string DocumentTypeKey,
    string DocumentTypeLabelVi,
    bool DocumentTypeConfirmed,
    /// <summary>
    /// Which body of law the rule pack belongs to. This profile is the
    /// administrative (ND30) scope; a party-committee (HD05) document is a
    /// different scope and is not covered by these rules.
    /// </summary>
    string ScopeId = "ND30_ADMIN",
    string ScopeLabelVi = "V\u0103n b\u1ea1n h\u00e0nh ch\u00ednh",
    /// <summary>What the detector suggested. Kept separate from the bound type.</summary>
    string? DetectedDocumentTypeKey = null,
    /// <summary>Whether the bound type was chosen explicitly by the owner.</summary>
    bool OwnerConfirmed = false);

public sealed record ProfileFindingView(
    string RuleId,
    string TargetKey,
    string Property,
    string Status,
    string Severity,
    string Expected,
    string Observed,
    string PatchEligibility,
    string PropertyLabelVi,
    string Unit,
    string? TargetId);

public sealed record MutableTargetView(
    string TargetId,
    string Property,
    string? CurrentValue,
    string Unit,
    string PropertyLabelVi,
    string RuleId,
    bool DirectOnly);

/// <summary>
/// A document type the current profile version actually supports, so the caller
/// can confirm or correct a detected type without guessing. This is a projection
/// of the versioned profile, not a new detection source.
/// </summary>
public sealed record SupportedDocumentTypeView(
    string TypeKey,
    string LabelVi,
    bool HasTypeHeading);

public sealed record DocumentInspectionResult(
    string SourceSha256,
    string Profile,
    bool SafeToMutate,
    string PackagePolicy,
    IReadOnlyList<ParagraphAlignmentInspection> Paragraphs,
    bool ParagraphsTruncated,
    IReadOnlyList<Diagnostic> Diagnostics,
    ProfileBinding? Binding = null,
    IReadOnlyList<ProfileFindingView>? Findings = null,
    IReadOnlyList<MutableTargetView>? MutableTargets = null,
    int RuleSubsetSize = 0,
    int ApplicableRules = 0,
    int EvaluatedRules = 0,
    int FailCount = 0,
    int NeedsReviewCount = 0,
    int NotEvaluatedCount = 0,
    double EvaluatedCoveragePercent = 0,
    bool FullComplianceClaimAllowed = false,
    string? ScopeStatement = null,
    IReadOnlyList<string>? SupportedProperties = null,
    IReadOnlyList<SupportedDocumentTypeView>? SupportedDocumentTypes = null);

public sealed record DocumentApplyResult(
    string SourceSha256,
    string OutputSha256,
    string ParagraphId,
    string Before,
    string After,
    byte[] OutputBytes,
    bool Reopened,
    bool Revalidated,
    bool SourceUnchanged,
    string Property,
    string RuleId = "TECHNICAL.USER_CONFIRMED_ALIGNMENT",
    string ProfileId = "",
    string ProfileVersion = "",
    string ProfileDigest = "");

/// <summary>A confirmed, exact formatting change requested against a named profile property.</summary>
public sealed record FormattingApplyRequest(
    string SourceSha256,
    /// <summary>Document type the owner confirmed. Re-resolved and compared on every apply.</summary>
    string DocumentTypeKey,
    string Property,
    string TargetId,
    string? ExpectedBefore,
    string DesiredAfter,
    string? ProfileId,
    string? ProfileDigest,
    string? RuleId);

public sealed record DocumentFormattingApplyResult(
    string SourceSha256,
    string OutputSha256,
    string DocumentTypeKey,
    string Property,
    string TargetId,
    string? Before,
    string After,
    string Unit,
    string PropertyLabelVi,
    string RuleId,
    byte[] OutputBytes,
    bool Reopened,
    bool Revalidated,
    bool SourceUnchanged,
    string Outcome,
    string Reason,
    ProfileBinding Binding);

/// <summary>
/// Serving boundary for the company formatting profile. It runs the real rule
/// engine over a profile-scoped rule subset and returns the profile binding,
/// findings and mutable targets an inspection needs. The rule subset is limited
/// to rules the DOCX can genuinely evidence, so management, records, workflow
/// and Party-document rules never contribute a FAIL here.
/// </summary>
public sealed class DocumentProcessorService
{
    private static readonly XNamespace WordprocessingNamespace = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    public const string Profile = "company-formatting-profile-v1";
    public const long MaximumOutputBytes = PackageSafetyLimits.MaximumCompressedInputBytes;
    public static readonly TimeSpan ProcessingTimeout = TimeSpan.FromSeconds(30);
    private readonly TimeSpan _processingTimeout;
    private readonly RuleCatalog? _catalog;
    private readonly FormattingProfile? _profile;
    private readonly DateOnly _evaluationDate;
    private const int MaximumInspectionParagraphs = 250;
    private const int MaximumDisplayedParagraphCharacters = 500;
    private const int MaximumReportedFindings = 200;
    private const int MaximumReportedTargets = 2000;

    public DocumentProcessorService() : this(ProcessingTimeout) { }

    public DocumentProcessorService(string legalRoot, DateOnly? evaluationDate = null)
        : this(ProcessingTimeout, RuleCatalog.LoadVerifiedRelease(legalRoot), evaluationDate) { }

    /// <summary>Serve with an already-loaded catalog so startup can validate the pack once.</summary>
    public DocumentProcessorService(RuleCatalog catalog, DateOnly? evaluationDate = null)
        : this(ProcessingTimeout, catalog, evaluationDate) { }

    internal DocumentProcessorService(TimeSpan processingTimeout)
        : this(processingTimeout, null, null) { }

    private DocumentProcessorService(TimeSpan processingTimeout, RuleCatalog? catalog, DateOnly? evaluationDate)
    {
        if (processingTimeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(processingTimeout));
        _processingTimeout = processingTimeout;
        _catalog = catalog;
        _profile = catalog is null ? null : FormattingProfileV1.Load(catalog);
        _evaluationDate = evaluationDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
    }

    /// <summary>True when the verified release catalog and company profile are loaded.</summary>
    public bool HasProfile => _catalog is not null && _profile is not null;

    public FormattingProfile? CurrentProfile => _profile;

    public async Task<DocumentInspectionResult> InspectAsync(Stream input, CancellationToken cancellationToken)
        => await InspectAsync(input, null, cancellationToken);

    /// <summary>
    /// Inspect under an owner-confirmed document type.
    ///
    /// When <paramref name="confirmedDocumentTypeKey"/> is supplied it is validated
    /// against this profile version and the whole scoped evaluation (rule subset,
    /// findings, mutable targets, statement) is rebuilt for that type. Nothing is
    /// relabelled from the detector's type.
    /// </summary>
    public async Task<DocumentInspectionResult> InspectAsync(Stream input, string? confirmedDocumentTypeKey, CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_processingTimeout);
        try
        {
            await using var snapshot = await SafeDocxSnapshot.CreateAsync(input, deadline.Token).ConfigureAwait(false);
            var parsed = new DocxParser().Parse(snapshot, deadline.Token);
            if (parsed.Document is null)
            {
                var diagnostic = parsed.Diagnostics.FirstOrDefault();
                throw new DocumentProcessorException(diagnostic?.Code ?? "MALFORMED_DOCX", diagnostic?.Message ?? "The DOCX package could not be parsed.");
            }
            var document = parsed.Document;
            var safeToMutate = document.Safety.PatchPolicy == PatchPolicy.NORMAL && document.Safety.UnsupportedFeatures.Count == 0;
            var paragraphs = document.Paragraphs.Take(MaximumInspectionParagraphs).Select(paragraph =>
            {
                var alignment = CanonicalAlignment(paragraph.DirectFormatting.Alignment);
                var text = string.Concat(paragraph.Runs.Select(run => run.Text));
                var issueId = safeToMutate && alignment is not null
                    ? StableId(string.Join("|", snapshot.Sha256, paragraph.Id, "paragraph.alignment", alignment))
                    : null;
                return new ParagraphAlignmentInspection(paragraph.Id, Clip(text), alignment, issueId);
            }).ToArray();
            var validation = EvaluateProfile(document, safeToMutate, confirmedDocumentTypeKey);
            snapshot.EnsureUnchanged();
            return new DocumentInspectionResult(snapshot.Sha256, Profile, safeToMutate,
                document.Safety.PatchPolicy.ToString(), paragraphs,
                document.Paragraphs.Count > paragraphs.Length,
                parsed.Diagnostics.Take(32).ToArray(),
                validation.Binding,
                validation.Findings,
                validation.MutableTargets,
                validation.RuleSubsetSize,
                validation.ApplicableRules,
                validation.EvaluatedRules,
                validation.FailCount,
                validation.NeedsReviewCount,
                validation.NotEvaluatedCount,
                validation.EvaluatedCoveragePercent,
                validation.FullComplianceClaimAllowed,
                validation.ScopeStatement,
                _profile?.MutationTargets.Select(x => x.Property).ToArray(),
                // Projected from the loaded profile version so the caller can
                // confirm or correct the detected type using only types this
                // profile actually supports.
                _profile?.DocumentTypes
                    .Select(x => new SupportedDocumentTypeView(x.TypeKey, x.LabelVi, x.HasTypeHeading))
                    .ToArray());
        }
        catch (DocumentPackageException ex) { throw Translate(ex, cancellationToken, deadline.Token); }
        catch (OperationCanceledException) { throw TimeoutOrCancellation(cancellationToken, deadline.Token); }
        catch (DocumentProcessorException) { throw; }
        catch (Exception) { throw new DocumentProcessorException("PROCESSING_FAILED", "DOCX processing failed safely."); }
    }

    private sealed record ProfileEvaluation(
        ProfileBinding? Binding,
        IReadOnlyList<ProfileFindingView>? Findings,
        IReadOnlyList<MutableTargetView>? MutableTargets,
        int RuleSubsetSize,
        int ApplicableRules,
        int EvaluatedRules,
        int FailCount,
        int NeedsReviewCount,
        int NotEvaluatedCount,
        double EvaluatedCoveragePercent,
        bool FullComplianceClaimAllowed,
        string? ScopeStatement);

    private static readonly ProfileEvaluation EmptyEvaluation = new(
        null, null, null, 0, 0, 0, 0, 0, 0, 0, false, null);

    /// <summary>
    /// Run the real validator. Any failure here degrades to a scoped statement
    /// with no findings rather than a fabricated pass.
    /// </summary>
    private ProfileEvaluation EvaluateProfile(DocumentModel document, bool safeToMutate, string? confirmedDocumentTypeKey = null)
    {
        if (_catalog is null || _profile is null) return EmptyEvaluation;
        try
        {
            var semantic = new AdministrativeSemanticDetector().Detect(document);
            var detectedTypeKey = DetectTypeKey(semantic);
            // The owner's confirmed type wins, but only if this profile supports it.
            var ownerConfirmed = !string.IsNullOrWhiteSpace(confirmedDocumentTypeKey);
            var typeKey = ResolveConfirmedTypeKey(semantic, confirmedDocumentTypeKey, document);
            var documentType = _profile.FindDocumentType(typeKey);
            if (documentType is null)
            {
                return new ProfileEvaluation(
                    new ProfileBinding(_profile.Id, _profile.Version, _profile.Digest, _profile.RulePackId, typeKey, "Ch\u01b0a x\u00e1c \u0111\u1ecbnh lo\u1ea1i v\u0103n b\u1ea3n", false),
                    [], [], 0, 0, 0, 0, 0, 0, 0, false,
                    "Kh\u00f4ng x\u00e1c \u0111\u1ecbnh \u0111\u01b0\u1ee3c lo\u1ea1i v\u0103n b\u1ea3n thu\u1ed9c h\u1ed3 s\u01a1 \u0111\u1ecbnh d\u1ea1ng, n\u00ean ch\u01b0a ki\u1ec3m tra \u0111\u01b0\u1ee3c quy t\u1eafc n\u00e0o.");
            }

            var subset = _profile.ResolveRuleSubset(_catalog, typeKey);
            var context = FormattingObservationAdapter.BuildContext(document, semantic, documentType, _profile, _evaluationDate);
            if (!safeToMutate) context.DocumentPatchPolicy = PatchPolicy.AUDIT_ONLY;
            var report = new ValidationEngine(_catalog).Validate(context, subset);

            // Only offer targets whose rule is actually in the selected type's scope,
            // so the UI can never present an out-of-scope property as editable.
            var scopedRuleIds = subset.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var targets = FormattingObservationAdapter
                .EnumerateMutableTargets(document, semantic, _profile)
                .Where(target => scopedRuleIds.Contains(target.RuleId))
                .Take(MaximumReportedTargets)
                .Select(target => new MutableTargetView(
                    target.ParagraphId,
                    target.Property,
                    target.CurrentValue,
                    target.Unit,
                    target.LabelVi,
                    target.RuleId,
                    target.DirectOnly))
                .ToArray();

            var findings = report.Results
                .Where(result => result.Status is ValidationStatus.FAIL or ValidationStatus.NEEDS_REVIEW)
                .Take(MaximumReportedFindings)
                .Select(result => new ProfileFindingView(
                    result.RuleId,
                    result.Target.Key,
                    result.Target.Property ?? string.Empty,
                    result.Status.ToString(),
                    result.Severity,
                    result.Expected,
                    result.Observed,
                    // A finding is only auto-appliable when this profile build actually
                    // offers a mutation target for its property. The ND30 font-family
                    // rule is still evaluated and reported, but no safe apply exists, so
                    // claiming ELIGIBLE would invite a change the server always refuses.
                    AppliesToAMutationTarget(result) ? result.PatchEligibility.ToString() : "SUGGEST_ONLY",
                    LabelForProperty(result.Target.Property),
                    UnitForProperty(result.Target.Property),
                    TargetIdForProperty(result.Target.Property)))
                .ToArray();

            var statement =
                $"Ki\u1ec3m tra theo h\u1ed3 s\u01a1 {_profile.Id} phi\u00ean b\u1ea3n {_profile.Version}: \u0111\u00e3 \u00e1p d\u1ee5ng {subset.Count} quy t\u1eafc ph\u00f9 h\u1ee3p v\u1edbi lo\u1ea1i \"{documentType.LabelVi}\". " +
                $"Trong \u0111\u00f3 \u0111\u00e1nh gi\u00e1 \u0111\u01b0\u1ee3c {report.EvaluatedRules}/{report.ApplicableRules} quy t\u1eafc \u00e1p d\u1ee5ng, kh\u00f4ng \u0111\u00e1nh gi\u00e1 \u0111\u01b0\u1ee3c {report.NotEvaluatedCount}. " +
                "K\u1ebft lu\u1eadn n\u00e0y ch\u1ec9 n\u1eb1m trong ph\u1ea1m vi c\u00e1c quy t\u1eafc ki\u1ec3m tra \u0111\u01b0\u1ee3c t\u1eeb n\u1ed9i dung t\u1ec7p, kh\u00f4ng ph\u1ea3i k\u1ebft lu\u1eadn tu\u00e2n th\u1ee7 to\u00e0n di\u1ec7n.";
            if (report.FullComplianceClaimAllowed)
                statement += " Kh\u00f4ng c\u00f3 sai l\u1ec7ch n\u00e0o trong ph\u1ea1m vi \u0111\u00e3 ki\u1ec3m tra.";

            return new ProfileEvaluation(
                new ProfileBinding(_profile.Id, _profile.Version, _profile.Digest, _profile.RulePackId, typeKey, documentType.LabelVi,
                    !string.Equals(typeKey, detectedTypeKey, StringComparison.OrdinalIgnoreCase)
                        || (!semantic.Classification.NeedsReview && semantic.Classification.Status is ClassificationStatus.CONFIRMED or ClassificationStatus.INFERRED_HIGH),
                    "ND30_ADMIN",
                    "Văn bản hành chính",
                    detectedTypeKey,
                    ownerConfirmed),
                findings,
                targets,
                subset.Count,
                report.ApplicableRules,
                report.EvaluatedRules,
                report.FailCount,
                report.NeedsReviewCount,
                report.NotEvaluatedCount,
                report.EvaluatedCoveragePercent,
                report.FullComplianceClaimAllowed,
                statement);
        }
        catch (DocumentProcessorException)
        {
            // A contract violation such as an unsupported confirmed document type must
            // be refused, not degraded into an "unknown" binding that would still report
            // a profile binding while silently evaluating nothing.
            throw;
        }
        catch (Exception)
        {
            return new ProfileEvaluation(
                new ProfileBinding(_profile.Id, _profile.Version, _profile.Digest, _profile.RulePackId, "unknown", "Ch\u01b0a x\u00e1c \u0111\u1ecbnh lo\u1ea1i v\u0103n b\u1ea3n", false),
                [], [], 0, 0, 0, 0, 0, 0, 0, false,
                "Kh\u00f4ng ho\u00e0n t\u1ea5t \u0111\u01b0\u1ee3c b\u01b0\u1edbc ki\u1ec3m tra theo h\u1ed3 s\u01a1 \u0111\u1ecbnh d\u1ea1ng. T\u1ec7p g\u1ed1c kh\u00f4ng b\u1ecb thay \u0111\u1ed5i.");
        }
    }

    /// <summary>
    /// True when this profile build offers a mutation target bound to the finding's rule,
    /// i.e. an apply could actually be executed for it.
    ///
    /// Binding is by <see cref="ProfileMutationTarget.RuleId"/> against
    /// <see cref="ValidationResult.RuleId"/>: that tuple is declared by the profile and is the
    /// authoritative link. Property names are deliberately not compared, because a rule targets a
    /// bare property (<c>paragraph_spacing_pt</c>) while a mutation target is scope-qualified
    /// (<c>paragraph.spacing_after_pt</c>), so string comparison silently matched nothing.
    /// A rule with no declared target, such as the ND30 font-family rule, stays fail-closed.
    /// </summary>
    private bool AppliesToAMutationTarget(ValidationResult result) =>
        _profile is not null
        && _profile.MutationTargets.Any(target =>
            string.Equals(target.RuleId, result.RuleId, StringComparison.Ordinal));

    /// <summary>
    /// Map the semantic detector classification onto a profile document type key.
    /// </summary>
    internal static string DetectTypeKey(SemanticDocumentModel semantic) => semantic.Classification.DocumentClass switch
    {
        AdministrativeDocumentClass.OfficialLetter => FormattingProfileV1.CongVanKey,
        AdministrativeDocumentClass.NamedAdministrativeDocument => semantic.Classification.SpecificType ?? "unknown",
        AdministrativeDocumentClass.Appendix => "phu_luc",
        AdministrativeDocumentClass.Copy => "ban_sao",
        _ => "unknown",
    };

    /// <summary>
    /// Resolve the type a run must be evaluated under.
    ///
    /// The detector only ever *suggests* a type. When the owner explicitly
    /// confirmed one, that choice is honoured, but only after proving it is a type
    /// this loaded profile version actually supports. An unsupported key is
    /// refused rather than silently falling back to the detected type, so a
    /// tampered or out-of-scope request can never be relabelled into the profile.
    /// </summary>
    /// <summary>
    /// Positive evidence that a document belongs to the Party (HD05) regime rather
    /// than the administrative (ND30) regime.
    ///
    /// This is a fail-closed domain check, not a classifier: it only looks at the
    /// issuing-authority and heading region, so an administrative document that
    /// merely mentions the Party in its body is not blocked. Any positive signal
    /// refuses the administrative profile outright.
    /// </summary>
    /// <summary>
    /// Positive evidence that a document is issued by a Party (HD05) body rather
    /// than an administrative (ND30) one.
    ///
    /// This reads only the issuing-authority evidence the semantic detector
    /// publishes, which includes issuer text carried by a table or a page header.
    /// It deliberately ignores the document-type heading: a Party decision carries
    /// a formal QUYET DINH heading, so the heading alone is never Party evidence.
    /// It also deliberately does not scan raw body paragraphs, because an ordinary
    /// ND30 document may legitimately mention the Party or a trade union in its
    /// subject or body text.
    /// </summary>
    private static bool HasPartyRegimeEvidence(SemanticDocumentModel semantic, DocumentModel? document)
    {
        // Party-specific issuer phrases only. Generic bodies such as "Ban Chap hanh
        // Cong doan" and a bare "BCH" are excluded on purpose: they are not Party
        // regime evidence and must not reject a legitimate administrative document.
        var partyIssuerPhrases = new[]
        {
            "\u0110\u1ea3ng",                                  // Dang
            "\u0110\u1ea3ng C\u1ed9ng s\u1ea3n Vi\u1ec7t Nam",  // Dang Cong san Viet Nam
            "C\u1ed9ng s\u1ea3n Vi\u1ec7t Nam",                // Cong san Viet Nam
            "Ban Ch\u1ea5p h\u00e0nh \u0110\u1ea3ng",           // Ban Chap hanh Dang
            "BCH \u0110\u1ea3ng",                              // BCH Dang
            "Trung \u01b0\u01a1ng \u0110\u1ea3ng",             // Trung uong Dang
            "Ban Ch\u1ea5p h\u00e0nh Trung \u01b0\u01a1ng \u0110\u1ea3ng",
            "HD05",
        };
        var issuingRoles = new[]
        {
            SemanticRole.IssuingAuthority,
            SemanticRole.IssuingAuthorityParent,
            SemanticRole.SigningAuthorityPrefix,
            SemanticRole.CopyAuthority,
        };
        bool Matches(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && partyIssuerPhrases.Any(phrase => value.Contains(phrase, StringComparison.OrdinalIgnoreCase));

        foreach (var component in semantic.Components)
        {
            if (!issuingRoles.Contains(component.Role)) continue;
            if (Matches(component.Text)) return true;
        }

        // The detector flattens tables and body paragraphs only, so an issuer banner
        // living in the Word page header is invisible to the semantic components
        // above. The parsed header model is therefore consulted directly and
        // narrowly: header/top-banner text only, never body text.
        if (document is null) return false;
        foreach (var header in document.Headers)
        {
            foreach (var paragraph in header.Paragraphs)
            {
                if (Matches(string.Concat(paragraph.Runs.Select(run => run.Text)))) return true;
            }
        }
        return false;
    }

    private string ResolveConfirmedTypeKey(SemanticDocumentModel semantic, string? confirmedDocumentTypeKey, DocumentModel? document = null)
    {
        var detected = DetectTypeKey(semantic);
        // The ND30 administrative profile must never be applied to a Party (HD05)
        // document. This is checked before any type decision, including an explicit
        // confirmation, and fails closed on any positive Party signal.
        if (HasPartyRegimeEvidence(semantic, document))
            throw new DocumentProcessorException("UNSUPPORTED_DOCUMENT_TYPE",
                "This document shows Party (HD05) issuing evidence, which is outside the administrative formatting profile.");
        if (string.IsNullOrWhiteSpace(confirmedDocumentTypeKey)) return detected;
        // A confirmation may correct which supported administrative type applies.
        // It must never override evidence that the document is not administrative
        // administrative-scope material at all (appendix, copy, or an unclassified
        // document that may belong to another body's rules such as a party document).
        var documentClass = semantic.Classification.DocumentClass;
        var overridable = documentClass is AdministrativeDocumentClass.OfficialLetter
            or AdministrativeDocumentClass.NamedAdministrativeDocument;
        if (!overridable)
            throw new DocumentProcessorException("UNSUPPORTED_DOCUMENT_TYPE",
                "This document is not recognised as an administrative document of the supported types, so no administrative document type can be confirmed for it.");
        var trimmed = confirmedDocumentTypeKey.Trim();
        if (_profile?.FindDocumentType(trimmed) is null)
            throw new DocumentProcessorException("UNSUPPORTED_DOCUMENT_TYPE",
                "The confirmed document type is not covered by the company formatting profile.");
        return trimmed;
    }


    private static string LabelForProperty(string? property) =>
        FormattingProfileV1.MutationTargets.FirstOrDefault(x => string.Equals(x.ObservationKey, property, StringComparison.OrdinalIgnoreCase))?.LabelVi
        ?? (property is { Length: > 0 } ? property : "Thu\u1ed9c t\u00ednh \u0111\u1ecbnh d\u1ea1ng");

    private static string UnitForProperty(string? property) =>
        FormattingProfileV1.MutationTargets.FirstOrDefault(x => string.Equals(x.ObservationKey, property, StringComparison.OrdinalIgnoreCase))?.Unit ?? string.Empty;

    private static string? TargetIdForProperty(string? property) =>
        string.Equals(property, "document_type_abbreviation", StringComparison.OrdinalIgnoreCase) ? null : null;

    public async Task<DocumentApplyResult> ApplyAlignmentAsync(
        Stream input,
        string expectedSourceSha256,
        string paragraphId,
        string expectedBefore,
        string desiredAfter,
        CancellationToken cancellationToken,
        string? expectedProfileDigest = null,
        string? expectedProfileId = null,
        string? expectedRuleId = null)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_processingTimeout);
        string? outputPath = null;
        try
        {
            await using var source = await SafeDocxSnapshot.CreateAsync(input, deadline.Token).ConfigureAwait(false);
            if (!string.Equals(source.Sha256, expectedSourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("STALE_DOCUMENT", "The DOCX source changed after inspection.");
            if (source.Safety.PatchPolicy != PatchPolicy.NORMAL || source.Safety.UnsupportedFeatures.Count != 0)
                throw new DocumentProcessorException("PACKAGE_NOT_MUTABLE", "The DOCX package is not eligible for this mutation.");

            var parsed = new DocxParser().Parse(source, deadline.Token);
            if (parsed.Document is null)
                throw new DocumentProcessorException("MALFORMED_DOCX", "The DOCX package could not be parsed.");
            if (!TryIndex(paragraphId, out var index) || index > parsed.Document.Paragraphs.Count)
                throw new DocumentProcessorException("TARGET_NOT_FOUND", "The selected paragraph is no longer available.");
            if (!TryAlignment(expectedBefore, out var before) || !TryAlignment(desiredAfter, out var after) || before == after)
                throw new DocumentProcessorException("UNSUPPORTED_OPERATION", "The requested direct alignment change is not supported.");

            var paragraph = parsed.Document.Paragraphs[index - 1];
            var actualBefore = CanonicalAlignment(paragraph.DirectFormatting.Alignment);
            if (actualBefore is null)
                throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "Only an explicit direct paragraph alignment can be changed.");
            if (!string.Equals(actualBefore, before.ToString(), StringComparison.Ordinal))
                throw new DocumentProcessorException("PRECONDITION_FAILED", "The selected paragraph no longer has the inspected alignment.");

            var originalOtherParts = PackagePreserver.PartSha256Inventory(source.StagedPath, deadline.Token)
                .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var originalDocumentStructure = DocumentStructureFingerprint(source.StagedPath, paragraphId);

            var documentIdentity = DocumentIdentityService.FromFile(source.StagedPath);
            // A proposal is only actionable under the exact profile that produced
            // it. A profile change between inspect and apply must fail closed
            // instead of applying a stale recommendation.
            if (!string.IsNullOrWhiteSpace(expectedProfileDigest) && _profile is not null
                && !string.Equals(expectedProfileDigest, _profile.Digest, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("PROFILE_CHANGED", "The formatting profile changed after this document was inspected.");
            if (!string.IsNullOrWhiteSpace(expectedProfileId) && _profile is not null
                && !string.Equals(expectedProfileId, _profile.Id, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("PROFILE_CHANGED", "The formatting profile changed after this document was inspected.");
            var ruleId = string.IsNullOrWhiteSpace(expectedRuleId) ? "TECHNICAL.USER_CONFIRMED_ALIGNMENT" : expectedRuleId!;
            var ruleIdentity = new RuleIdentity(ruleId, "USER_CONFIRMATION", "explicit direct formatting operation");
            var binding = new DocumentStateBinding(documentIdentity, ruleIdentity);
            var findingReference = StableId(string.Join("|", documentIdentity.Digest, paragraph.Id, before, after));
            var proposalId = StableId(string.Join("|", "proposal", findingReference));
            var evidence = new[] { StableId(string.Join("|", "confirmed-operation", findingReference)) };
            var proposal = new RemediationProposal(proposalId, ruleIdentity.RuleId, findingReference, evidence,
                paragraph.Id, "paragraph_alignment", before.ToString(), after.ToString(),
                RemediationDecision.ELIGIBLE, true, "Exact user-confirmed technical operation.", binding);
            var target = new ParagraphTargetLocator("/word/document.xml", "paragraph", paragraph.Id,
                $"body/p[{index}]", ParagraphAlignmentMutationExecutor.Anchor(string.Concat(paragraph.Runs.Select(run => run.Text))),
                "paragraph.alignment");
            var intent = MutationIntent.Create(proposal, target, before, after);
            var authorization = AuthorizationArtifact.Issue(proposal, intent, "agent-workspace-confirmation-gateway");
            var authorized = AuthorizationBoundary.Authorize(proposal, source.Safety, authorization,
                documentIdentity, ruleIdentity, intent);
            if (!authorized.Authorized || authorized.Request is null)
                throw new DocumentProcessorException("AUTHORIZATION_REJECTED", "The exact alignment operation was not authorized.");

            outputPath = Path.Combine(Path.GetDirectoryName(source.StagedPath)!, "output.docx");
            var result = new ParagraphAlignmentMutationExecutor().Execute(source.StagedPath, outputPath, authorized.Request, deadline.Token);
            if (!result.Success)
            {
                if (result.Outcome == MutationOutcome.PROCESSING_CANCELLED)
                    throw TimeoutOrCancellation(cancellationToken, deadline.Token);
                throw new DocumentProcessorException(ToErrorCode(result.Outcome), SafeMutationMessage(result.Outcome));
            }

            source.EnsureUnchanged();
            var bytes = await ReadBoundedOutputAsync(outputPath, deadline.Token).ConfigureAwait(false);
            await using var reopenedSnapshot = await SafeDocxSnapshot.CreateAsync(new MemoryStream(bytes, writable: false), deadline.Token).ConfigureAwait(false);
            var reopened = new DocxParser().Parse(reopenedSnapshot, deadline.Token);
            if (reopened.Document is null || reopenedSnapshot.Safety.PatchPolicy != PatchPolicy.NORMAL)
                throw new DocumentProcessorException("OUTPUT_INTEGRITY_FAILED", "The generated DOCX did not pass canonical revalidation.");
            var outputOtherParts = PackagePreserver.PartSha256Inventory(outputPath, deadline.Token)
                .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (!originalOtherParts.SequenceEqual(outputOtherParts) ||
                DocumentStructureFingerprint(outputPath, paragraphId) != originalDocumentStructure)
                throw new DocumentProcessorException("OUTPUT_INTEGRITY_FAILED", "Unrelated DOCX package structures changed during processing.");
            var actualAfter = index <= reopened.Document.Paragraphs.Count
                ? CanonicalAlignment(reopened.Document.Paragraphs[index - 1].DirectFormatting.Alignment)
                : null;
            if (!string.Equals(actualAfter, after.ToString(), StringComparison.Ordinal))
                throw new DocumentProcessorException("POSTCONDITION_FAILED", "The requested alignment was not present after reopening.");
            source.EnsureUnchanged();
            return new DocumentApplyResult(source.Sha256, reopenedSnapshot.Sha256, paragraphId,
                before.ToString(), after.ToString(), bytes, true, true, true, "paragraph.alignment",
                ruleId, _profile?.Id ?? "", _profile?.Version ?? "", _profile?.Digest ?? "");
        }
        catch (DocumentPackageException ex) { throw Translate(ex, cancellationToken, deadline.Token); }
        catch (OperationCanceledException) { throw TimeoutOrCancellation(cancellationToken, deadline.Token); }
        catch (DocumentProcessorException) { throw; }
        catch (Exception) { throw new DocumentProcessorException("PROCESSING_FAILED", "DOCX processing failed safely."); }
        finally
        {
            if (outputPath is not null)
            {
                try { if (File.Exists(outputPath)) File.Delete(outputPath); }
                catch (IOException) { throw new DocumentProcessorException("TEMP_CLEANUP_FAILED", "Temporary DOCX output could not be removed."); }
                catch (UnauthorizedAccessException) { throw new DocumentProcessorException("TEMP_CLEANUP_FAILED", "Temporary DOCX output could not be removed."); }
            }
        }
    }

    /// <summary>
    /// Apply one confirmed profile formatting property.
    ///
    /// The proposal is re-resolved against the live document and the live profile
    /// on every call: the target must still exist, its current value must still
    /// equal the value the owner confirmed, the rule must still be in the scoped
    /// subset and still carry an autofix policy that permits an automatic fix, and
    /// the profile digest must match the one the inspection was issued under. Only
    /// then is an authorization artifact issued and handed to the surgical
    /// executor, and the output is re-inspected with the same profile afterwards.
    /// </summary>
    public async Task<DocumentFormattingApplyResult> ApplyFormattingAsync(
        Stream input,
        FormattingApplyRequest request,
        CancellationToken cancellationToken)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(_processingTimeout);
        string? outputPath = null;
        try
        {
            if (_catalog is null || _profile is null)
                throw new DocumentProcessorException("PROFILE_NOT_CONFIGURED", "The verified rule catalog and formatting profile are not loaded.");

            // The inspection binding is mandatory. An apply without it cannot prove
            // which profile and rule content the owner's confirmation was made under.
            if (!FormattingMutationIntent.IsWellFormedDigest(request.ProfileDigest))
                throw new DocumentProcessorException("PROFILE_CHANGED", "The confirmed operation carries no formatting profile binding.");
            if (string.IsNullOrWhiteSpace(request.ExpectedBefore))
                throw new DocumentProcessorException("PRECONDITION_FAILED", "The confirmed operation carries no expected current value.");

            await using var source = await SafeDocxSnapshot.CreateAsync(input, deadline.Token).ConfigureAwait(false);
            if (!string.Equals(source.Sha256, request.SourceSha256, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("STALE_DOCUMENT", "The DOCX source changed after inspection.");
            if (source.Safety.PatchPolicy != PatchPolicy.NORMAL || source.Safety.UnsupportedFeatures.Count != 0)
                throw new DocumentProcessorException("PACKAGE_NOT_MUTABLE", "The DOCX package is not eligible for this mutation.");

            if (!string.Equals(request.ProfileDigest, _profile.Digest, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("PROFILE_CHANGED", "The formatting profile changed after this document was inspected.");
            if (!string.IsNullOrWhiteSpace(request.ProfileId)
                && !string.Equals(request.ProfileId, _profile.Id, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("PROFILE_CHANGED", "The formatting profile changed after this document was inspected.");

            var profileTarget = _profile.FindMutationTarget(request.Property)
                ?? throw new DocumentProcessorException("UNSUPPORTED_PROPERTY", "The requested formatting property is not part of the company formatting profile.");
            if (!string.IsNullOrWhiteSpace(request.RuleId) && !string.Equals(request.RuleId, profileTarget.RuleId, StringComparison.Ordinal))
                throw new DocumentProcessorException("PROFILE_TUPLE_MISMATCH", "The requested property and rule are not a declared profile pair.");

            var liveRule = _catalog.Rules.FirstOrDefault(rule => rule.Id == profileTarget.RuleId)
                ?? throw new DocumentProcessorException("STALE_RULE_CONTENT", "The bound rule is not present in the verified release catalog.");
            if (liveRule.AutofixPolicy is not ("SAFE" or "GUARDED"))
                throw new DocumentProcessorException("PROPOSAL_NOT_ELIGIBLE", "The bound rule does not permit an automatic fix.");

var parsed = new DocxParser().Parse(source, deadline.Token);
            if (parsed.Document is null)
                throw new DocumentProcessorException("MALFORMED_DOCX", "The DOCX package could not be parsed.");

            // Re-run the real semantic detection and validation context for this exact
            // document. Document type, scoped rule subset, capabilities, applicability
            // fields and evidence all come from this live run, not from the request.
            var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document);
            // The confirmed type must be supported by this profile version, and the
            // rule, target and property must still be in that type's live scope. The
            // detector only suggests; it can never override the explicit choice, and
            // an unsupported or tampered key is refused outright.
            var documentTypeKey = ResolveConfirmedTypeKey(semantic, request.DocumentTypeKey, parsed.Document);
            if (string.IsNullOrWhiteSpace(request.DocumentTypeKey))
                throw new DocumentProcessorException("DOCUMENT_TYPE_REQUIRED", "A confirmed document type is required.");
            var documentType = _profile.FindDocumentType(documentTypeKey)
                ?? throw new DocumentProcessorException("UNSUPPORTED_DOCUMENT_TYPE", "This document type is not covered by the company formatting profile.");
            if (!_profile.ResolveRuleSubset(_catalog, documentTypeKey).Contains(profileTarget.RuleId))
                throw new DocumentProcessorException("UNSUPPORTED_PROPERTY", "The bound rule is not in scope for this document type.");
            var liveContext = FormattingObservationAdapter.BuildContext(parsed.Document, semantic, documentType, _profile, _evaluationDate);

            // Re-resolve the target from the live document and the live semantic
            // roles. A body rule can therefore never be applied to a heading or a
            // signature block just because that paragraph also has direct formatting.
            var liveTarget = FormattingObservationAdapter
                .EnumerateMutableTargets(parsed.Document, semantic, _profile)
                .FirstOrDefault(target => string.Equals(target.Property, request.Property, StringComparison.Ordinal)
                    && string.Equals(target.ParagraphId, request.TargetId, StringComparison.Ordinal))
                ?? throw new DocumentProcessorException("TARGET_NOT_FOUND", "The selected formatting target is no longer available.");
            if (liveTarget.CurrentValue is null)
                throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "The selected target does not declare this property as direct formatting.");
            if (!string.Equals(liveTarget.CurrentValue, request.ExpectedBefore, StringComparison.Ordinal))
                throw new DocumentProcessorException("PRECONDITION_FAILED", "The selected target no longer has the inspected value.");
            if (string.Equals(liveTarget.CurrentValue, request.DesiredAfter, StringComparison.Ordinal))
                throw new DocumentProcessorException("UNSUPPORTED_OPERATION", "The requested change does not alter the current value.");

            // The desired value is judged by the real evaluator against the real
            // context. Only the observed value for this one target key is replaced,
            // so every real capability, applicability field, evidence record and
            // uncertainty marker still applies.
            liveContext.Observe(liveRule.Target.Key, Coerce(request.DesiredAfter));
            liveContext.UncertainTargets.Remove(liveRule.Target.Key);
            var desiredEvaluation = new RuleEvaluator().Evaluate(liveRule, liveContext);
            if (desiredEvaluation.Status != ValidationStatus.PASS)
                throw new DocumentProcessorException("UNSUPPORTED_OPERATION", "The requested value does not satisfy the bound formatting rule.");

            var documentIdentity = DocumentIdentityService.FromFile(source.StagedPath);
            var ruleIdentity = new RuleIdentity(liveRule.Id, liveRule.Source.SourceId, liveRule.Source.Locator);
            var stateBinding = new DocumentStateBinding(documentIdentity, ruleIdentity);
            var proposalId = StableId(string.Join("|", "formatting-proposal", documentIdentity.Digest, request.Property, request.TargetId, request.ExpectedBefore, request.DesiredAfter));
            var ruleContentDigest = new FormattingPropertyMutationExecutor(_catalog, _profile).CurrentRuleContentDigest(liveRule.Id);
            var kind = FormattingPropertyMutationExecutor.IsParagraphProperty(request.Property)
                ? FormattingTargetKind.Paragraph
                : FormattingTargetKind.Section;
            var intentId = FormattingMutationIntent.ComputeId(
                proposalId, liveRule.Id, request.Property, request.TargetId, kind, profileTarget.Unit,
                request.ExpectedBefore, request.DesiredAfter, stateBinding, _profile.Digest, ruleContentDigest);

            var proposal = new RemediationProposal(proposalId, liveRule.Id, StableId(string.Join("|", "finding", proposalId)),
                [StableId(string.Join("|", "mutable-target", documentIdentity.Digest, request.Property, request.TargetId))],
                request.TargetId, request.Property, request.ExpectedBefore ?? "unset", request.DesiredAfter,
                RemediationDecision.ELIGIBLE, true, "Exact user-confirmed formatting operation.", stateBinding);
            var artifact = AuthorizationArtifact.Issue(proposal, "document-processor-serving-flow") with
            {
                Scope = FormattingAuthorizationBoundary.RequestScope,
                MutationIntentId = intentId,
            };

            var intent = new FormattingMutationIntent(intentId, proposalId, liveRule.Id, request.Property,
                request.TargetId, kind, profileTarget.Unit, request.ExpectedBefore, request.DesiredAfter, stateBinding,
                _profile.Digest, ruleContentDigest);

            var executor = new FormattingPropertyMutationExecutor(_catalog, _profile);
            var trusted = executor.Package(intent, artifact, intentId, "serving-flow");

            outputPath = Path.Combine(Path.GetDirectoryName(source.StagedPath)!, "formatted-output.docx");
            var mutation = executor.Execute(source.StagedPath, outputPath, trusted, deadline.Token);
            if (!mutation.Success)
            {
                if (mutation.Outcome == FormattingMutationOutcome.PROCESSING_CANCELLED)
                    throw TimeoutOrCancellation(cancellationToken, deadline.Token);
                // Reasons are fixed internal literals; the code drives client wording.
                throw new DocumentProcessorException(
                    ToFormattingErrorCode(mutation.Outcome),
                    string.IsNullOrWhiteSpace(mutation.Reason) ? SafeFormattingMessage(mutation.Outcome) : mutation.Reason);
            }

            source.EnsureUnchanged();
            var bytes = await ReadBoundedOutputAsync(outputPath, deadline.Token).ConfigureAwait(false);

            // Revalidate the produced document with the same profile. The output is
            // its own document: its digest must match what the mutation produced,
            // not what the input was.
            // Re-inspect the produced artifact under the SAME confirmed type, otherwise
            // the verification would fall back to the detector's type and either fail a
            // valid confirmed-type mutation or check the wrong rule scope.
            var applied = await InspectAsync(new MemoryStream(bytes, writable: false), request.DocumentTypeKey, deadline.Token).ConfigureAwait(false);
            if (mutation.OutputDocument is null
                || !string.Equals(applied.SourceSha256, mutation.OutputDocument.Digest, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("OUTPUT_INTEGRITY_FAILED", "The generated DOCX did not match the artifact that was produced.");

            var appliedTarget = applied.MutableTargets?.FirstOrDefault(target =>
                string.Equals(target.Property, request.Property, StringComparison.Ordinal)
                && string.Equals(target.TargetId, request.TargetId, StringComparison.Ordinal));
            if (appliedTarget is null || !string.Equals(appliedTarget.CurrentValue, request.DesiredAfter, StringComparison.Ordinal))
                throw new DocumentProcessorException("POSTCONDITION_FAILED", "The requested change was not present after reopening the generated document.");

            // Input invariance is proven by the executor, which re-hashes the source
            // immediately before publishing the output.
            if (applied.Binding is null
                || !string.Equals(applied.Binding.Id, _profile.Id, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(applied.Binding.Digest, _profile.Digest, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(applied.Binding.DocumentTypeKey, documentType.TypeKey, StringComparison.OrdinalIgnoreCase))
                throw new DocumentProcessorException("OUTPUT_INTEGRITY_FAILED", "The generated DOCX was not re-inspected under the confirmed profile.");
            if (mutation.Verification is not { SourceUnchanged: true })
                throw new DocumentProcessorException("SOURCE_IMMUTABILITY_VIOLATION", "The source DOCX changed during processing.");
            source.EnsureUnchanged();

            return new DocumentFormattingApplyResult(
                source.Sha256, applied.SourceSha256, documentType.TypeKey, request.Property, request.TargetId,
                liveTarget.CurrentValue, request.DesiredAfter, profileTarget.Unit, profileTarget.LabelVi, liveRule.Id,
                bytes, true, true, true, mutation.Outcome.ToString(), mutation.Reason,
                applied.Binding ?? new ProfileBinding(_profile.Id, _profile.Version, _profile.Digest, _profile.RulePackId, "unknown", "Ch\u01b0a x\u00e1c \u0111\u1ecbnh lo\u1ea1i v\u0103n b\u1ea3n", false));
        }
        catch (DocumentPackageException ex) { throw Translate(ex, cancellationToken, deadline.Token); }
        catch (OperationCanceledException) { throw TimeoutOrCancellation(cancellationToken, deadline.Token); }
        catch (DocumentProcessorException) { throw; }
        catch (Exception) { throw new DocumentProcessorException("PROCESSING_FAILED", "DOCX processing failed safely."); }
        finally
        {
            if (outputPath is not null)
            {
                try { if (File.Exists(outputPath)) File.Delete(outputPath); }
                catch (IOException) { throw new DocumentProcessorException("TEMP_CLEANUP_FAILED", "Temporary DOCX output could not be removed."); }
                catch (UnauthorizedAccessException) { throw new DocumentProcessorException("TEMP_CLEANUP_FAILED", "Temporary DOCX output could not be removed."); }
            }
        }
    }

        /// <summary>Convert a textual value to the numeric form a numeric constraint needs.</summary>
    private static object? Coerce(string value) =>
        double.TryParse(value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var number)
            ? number
            : value;
    private static string ToFormattingErrorCode(FormattingMutationOutcome outcome) => outcome switch
    {
        FormattingMutationOutcome.STALE_DOCUMENT => "STALE_DOCUMENT",
        FormattingMutationOutcome.AUTHORIZATION_REJECTED => "AUTHORIZATION_REJECTED",
        FormattingMutationOutcome.PACKAGE_NOT_MUTABLE => "PACKAGE_NOT_MUTABLE",
        FormattingMutationOutcome.TARGET_NOT_FOUND => "TARGET_NOT_FOUND",
        FormattingMutationOutcome.TARGET_AMBIGUOUS => "TARGET_AMBIGUOUS",
        FormattingMutationOutcome.PROVENANCE_NOT_DIRECT => "PROVENANCE_NOT_DIRECT",
        FormattingMutationOutcome.PRECONDITION_FAILED => "PRECONDITION_FAILED",
        FormattingMutationOutcome.PROFILE_CHANGED => "PROFILE_CHANGED",
        FormattingMutationOutcome.STALE_RULE_CONTENT => "STALE_RULE_CONTENT",
        FormattingMutationOutcome.PROFILE_TUPLE_MISMATCH => "PROFILE_TUPLE_MISMATCH",
        FormattingMutationOutcome.UNSUPPORTED_PROPERTY => "UNSUPPORTED_PROPERTY",
        FormattingMutationOutcome.UNSUPPORTED_OPERATION => "UNSUPPORTED_OPERATION",
        FormattingMutationOutcome.POSTCONDITION_FAILED => "POSTCONDITION_FAILED",
        FormattingMutationOutcome.OUTPUT_INTEGRITY_FAILED => "OUTPUT_INTEGRITY_FAILED",
        FormattingMutationOutcome.SOURCE_IMMUTABILITY_VIOLATION => "SOURCE_IMMUTABILITY_VIOLATION",
        FormattingMutationOutcome.OUTPUT_PATH_INVALID => "OUTPUT_PATH_INVALID",
        FormattingMutationOutcome.OUTPUT_ALREADY_EXISTS => "OUTPUT_ALREADY_EXISTS",
        FormattingMutationOutcome.OUTPUT_TOO_LARGE => "OUTPUT_TOO_LARGE",
        FormattingMutationOutcome.PROCESSING_CANCELLED => "PROCESSING_CANCELLED",
        FormattingMutationOutcome.MISSING_BINDING => "PROFILE_CHANGED",
        FormattingMutationOutcome.INTENT_CONTENT_MISMATCH => "AUTHORIZATION_REJECTED",
        _ => "PROCESSING_FAILED",
    };

    private static string SafeFormattingMessage(FormattingMutationOutcome outcome) => outcome switch
    {
        FormattingMutationOutcome.STALE_DOCUMENT => "The DOCX source changed after inspection.",
        FormattingMutationOutcome.AUTHORIZATION_REJECTED => "The exact formatting change was not authorized.",
        FormattingMutationOutcome.PACKAGE_NOT_MUTABLE => "The DOCX package is not eligible for this mutation.",
        FormattingMutationOutcome.TARGET_NOT_FOUND => "The selected formatting target is no longer available.",
        FormattingMutationOutcome.PROVENANCE_NOT_DIRECT => "Only direct formatting already declared by the document can be changed.",
        FormattingMutationOutcome.PRECONDITION_FAILED => "The selected target no longer has the inspected value.",
        FormattingMutationOutcome.PROFILE_CHANGED => "The formatting profile changed after this document was inspected.",
        FormattingMutationOutcome.STALE_RULE_CONTENT => "The bound rule changed after this document was inspected.",
        FormattingMutationOutcome.PROFILE_TUPLE_MISMATCH => "The requested property and rule are not a declared profile pair.",
        FormattingMutationOutcome.UNSUPPORTED_PROPERTY => "The requested formatting property is not part of the company formatting profile.",
        FormattingMutationOutcome.OUTPUT_INTEGRITY_FAILED => "The generated DOCX failed output integrity checks.",
        FormattingMutationOutcome.SOURCE_IMMUTABILITY_VIOLATION => "The source DOCX changed during processing.",
        _ => "The confirmed formatting change could not be completed.",
    };

    private static async Task<byte[]> ReadBoundedOutputAsync(string path, CancellationToken cancellationToken)
    {
        await using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 64 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (input.Length > MaximumOutputBytes) throw new DocumentProcessorException("OUTPUT_TOO_LARGE", "The generated DOCX exceeds the allowed output size.");
        using var output = new MemoryStream((int)input.Length);
        var buffer = new byte[64 * 1024];
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
            if (read == 0) break;
            if (output.Length + read > MaximumOutputBytes) throw new DocumentProcessorException("OUTPUT_TOO_LARGE", "The generated DOCX exceeds the allowed output size.");
            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }
        return output.ToArray();
    }

    private static string DocumentStructureFingerprint(string path, string targetParagraphId)
    {
        using var archive = ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml") ??
            throw new DocumentProcessorException("MALFORMED_DOCX", "The DOCX document part is missing.");
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = PackageSafetyLimits.MaximumXmlCharactersPerPart,
            MaxCharactersFromEntities = PackageSafetyLimits.MaximumXmlCharactersFromEntities,
            CloseInput = false,
        };
        using var part = entry.Open();
        using var reader = XmlReader.Create(part, settings);
        var document = XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        var body = document.Root?.Element(WordprocessingNamespace + "body");
        var paragraphs = body?.Elements(WordprocessingNamespace + "p").ToList() ?? [];
        if (!TryIndex(targetParagraphId, out var index) || index > paragraphs.Count)
            throw new DocumentProcessorException("TARGET_NOT_FOUND", "The selected paragraph is no longer available.");

        var target = paragraphs[index - 1];
        var paragraphProperties = target.Elements(WordprocessingNamespace + "pPr").ToList();
        if (paragraphProperties.Count != 1)
            throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "The selected paragraph has ambiguous direct properties.");
        var directAlignments = paragraphProperties[0].Elements(WordprocessingNamespace + "jc").ToList();
        if (directAlignments.Count != 1)
            throw new DocumentProcessorException("PROVENANCE_NOT_DIRECT", "The selected paragraph has ambiguous direct alignment.");
        directAlignments[0].Remove();

        var canonical = new StringBuilder();
        if (document.Root is not null) AppendCanonicalXml(document.Root, canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void AppendCanonicalXml(XElement element, StringBuilder target)
    {
        target.Append('E');
        AppendCanonicalToken(element.Name.NamespaceName, target);
        AppendCanonicalToken(element.Name.LocalName, target);
        foreach (var attribute in element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration)
            .OrderBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal)
            .ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal))
        {
            target.Append('A');
            AppendCanonicalToken(attribute.Name.NamespaceName, target);
            AppendCanonicalToken(attribute.Name.LocalName, target);
            AppendCanonicalToken(attribute.Value, target);
        }
        target.Append('>');
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement child:
                    AppendCanonicalXml(child, target);
                    break;
                case XText text:
                    target.Append('T');
                    AppendCanonicalToken(text.Value, target);
                    break;
                case XComment comment:
                    target.Append('C');
                    AppendCanonicalToken(comment.Value, target);
                    break;
                case XProcessingInstruction instruction:
                    target.Append('P');
                    AppendCanonicalToken(instruction.Target, target);
                    AppendCanonicalToken(instruction.Data, target);
                    break;
            }
        }
        target.Append("/E");
        AppendCanonicalToken(element.Name.NamespaceName, target);
        AppendCanonicalToken(element.Name.LocalName, target);
    }

    private static void AppendCanonicalToken(string value, StringBuilder target) =>
        target.Append(value.Length).Append(':').Append(value);

    private static string? CanonicalAlignment(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "LEFT" => "LEFT", "CENTER" => "CENTER", "RIGHT" => "RIGHT", "BOTH" or "JUSTIFY" => "JUSTIFY", _ => null,
    };

    private static bool TryAlignment(string value, out ParagraphAlignmentValue alignment) =>
        Enum.TryParse(value, ignoreCase: true, out alignment) && Enum.IsDefined(alignment);

    private static bool TryIndex(string paragraphId, out int index)
    {
        index = 0;
        return paragraphId.Length is > 1 and <= 10 && paragraphId[0] == 'p' &&
            int.TryParse(paragraphId.AsSpan(1), out index) && index > 0;
    }

    private static string Clip(string text) => text.Length <= MaximumDisplayedParagraphCharacters ? text : text[..MaximumDisplayedParagraphCharacters];
    private static string StableId(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];
    private static string ToErrorCode(MutationOutcome outcome) => outcome switch
    {
        MutationOutcome.STALE_DOCUMENT => "STALE_DOCUMENT",
        MutationOutcome.AUTHORIZATION_REJECTED => "AUTHORIZATION_REJECTED",
        MutationOutcome.PACKAGE_NOT_MUTABLE => "PACKAGE_NOT_MUTABLE",
        MutationOutcome.TARGET_NOT_FOUND => "TARGET_NOT_FOUND",
        MutationOutcome.TARGET_AMBIGUOUS => "TARGET_AMBIGUOUS",
        MutationOutcome.PROVENANCE_NOT_DIRECT => "PROVENANCE_NOT_DIRECT",
        MutationOutcome.PRECONDITION_FAILED => "PRECONDITION_FAILED",
        MutationOutcome.UNSUPPORTED_OPERATION => "UNSUPPORTED_OPERATION",
        MutationOutcome.OUTPUT_PATH_INVALID => "OUTPUT_PATH_INVALID",
        MutationOutcome.OUTPUT_ALREADY_EXISTS => "OUTPUT_ALREADY_EXISTS",
        MutationOutcome.OUTPUT_TOO_LARGE => "OUTPUT_TOO_LARGE",
        MutationOutcome.OUTPUT_INTEGRITY_FAILED => "OUTPUT_INTEGRITY_FAILED",
        MutationOutcome.SOURCE_IMMUTABILITY_VIOLATION => "SOURCE_IMMUTABILITY_VIOLATION",
        MutationOutcome.PROCESSING_CANCELLED => "PROCESSING_CANCELLED",
        _ => "PROCESSING_FAILED",
    };
    private static string SafeMutationMessage(MutationOutcome outcome) => outcome switch
    {
        MutationOutcome.STALE_DOCUMENT => "The DOCX source changed after inspection.",
        MutationOutcome.AUTHORIZATION_REJECTED => "The exact alignment operation was not authorized.",
        MutationOutcome.PACKAGE_NOT_MUTABLE => "The DOCX package is not eligible for this mutation.",
        MutationOutcome.TARGET_NOT_FOUND or MutationOutcome.TARGET_AMBIGUOUS => "The selected paragraph is not uniquely available.",
        MutationOutcome.PROVENANCE_NOT_DIRECT => "Only explicit direct paragraph alignment can be changed.",
        MutationOutcome.PRECONDITION_FAILED => "The selected paragraph no longer has the inspected alignment.",
        MutationOutcome.OUTPUT_INTEGRITY_FAILED => "The generated DOCX failed output integrity checks.",
        MutationOutcome.SOURCE_IMMUTABILITY_VIOLATION => "The source DOCX changed during processing.",
        _ => "The confirmed alignment operation could not be completed.",
    };

    private static DocumentProcessorException Translate(DocumentPackageException error, CancellationToken caller, CancellationToken deadline) =>
        error.Code == "PROCESSING_CANCELLED" ? TimeoutOrCancellation(caller, deadline) : new(error.Code, error.SafeMessage);

    private static DocumentProcessorException TimeoutOrCancellation(CancellationToken caller, CancellationToken deadline) => caller.IsCancellationRequested
        ? new("PROCESSING_CANCELLED", "DOCX processing was cancelled.")
        : deadline.IsCancellationRequested
            ? new("PROCESSING_TIMEOUT", "DOCX processing exceeded its time limit.")
            : new("PROCESSING_CANCELLED", "DOCX processing was cancelled.");
}

public sealed class DocumentProcessorException(string code, string safeMessage) : Exception(safeMessage)
{
    public string Code { get; } = code;
    public string SafeMessage { get; } = safeMessage;
}
