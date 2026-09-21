using Nd30.DocumentEngine.Model;

namespace Nd30.LegalValidator.Model;

public enum ValidationStatus { PASS, FAIL, NOT_APPLICABLE, NOT_EVALUATED, NEEDS_REVIEW }
public enum PatchEligibility { NOT_NEEDED, ELIGIBLE, SUGGEST_ONLY, PROHIBITED, UNKNOWN }
public enum EvidenceAuthority { Derived, Authoritative }

public sealed record RuleTarget(string ObjectType, string? Role, string? Property)
{
    public string Key => string.Join(".", new[] { ObjectType, Role, Property }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!));
}

public sealed record RuleSource(string SourceId, string Locator);

public sealed record EvidenceRecord(
    string Kind,
    EvidenceAuthority Authority,
    string Reference,
    double? Confidence = null,
    bool RequiresReview = false,
    IReadOnlyDictionary<string,string>? Attributes = null);

public sealed record RuleDefinition(
    string Id,
    string Regime,
    string Domain,
    string Maturity,
    RuleSource Source,
    DateOnly? EffectiveFrom,
    DateOnly? EffectiveTo,
    RuleTarget Target,
    IReadOnlyDictionary<string,object?> Constraint,
    IReadOnlyDictionary<string,object?>? Applicability,
    string Severity,
    string AutofixPolicy,
    IReadOnlyList<string> RequiresCapabilities);

public sealed record ValidationResult(
    string RuleId,
    ValidationStatus Status,
    string Severity,
    RuleSource Source,
    RuleTarget Target,
    string Expected,
    string Observed,
    IReadOnlyList<EvidenceRecord> Evidence,
    IReadOnlyList<string> RequiredCapabilities,
    IReadOnlyList<string> MissingCapabilities,
    string Applicability,
    double? Confidence,
    PatchEligibility PatchEligibility,
    string Reason);

public sealed record ValidationReport(
    IReadOnlyList<ValidationResult> Results,
    int ApplicableRules,
    int EvaluatedRules,
    double EvaluatedCoveragePercent,
    int PassCount,
    int FailCount,
    int NeedsReviewCount,
    int NotEvaluatedCount,
    int NotApplicableCount,
    bool FullComplianceClaimAllowed,
    string ComplianceStatement);

public sealed record ConstraintEvaluation(ValidationStatus Status, string Reason, string Expected, string Observed);
