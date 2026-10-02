namespace Nd30.SemanticDetector.Model;

public enum SemanticRole
{
    IssuingAuthorityParent,
    IssuingAuthority,
    NationalHeader,
    NationalMotto,
    DocumentNumber,
    DocumentNotation,
    IssuePlaceAndDate,
    DocumentTypeHeading,
    SubjectNamedDocument,
    SubjectOfficialLetter,
    LegalBasisBlock,
    Body,
    PartHeading,
    ChapterHeading,
    SectionHeading,
    SubsectionHeading,
    ArticleHeading,
    Clause,
    Point,
    Addressee,
    RecipientList,
    RetentionLine,
    SigningAuthorityPrefix,
    SignerTitle,
    SignerName,
    AppendixNumber,
    AppendixTitle,
    AppendixReference,
    CopyFormHeading,
    CopyAuthority,
    CopyCertificationSignature,
    ClassificationMark,
    UrgencyMark,
    CirculationInstruction,
    DrafterCode,
    IssuedCopyCount,
    ContactInformation
}

public enum ClassificationMethod { Deterministic, Template, Heuristic, Human }
public enum ClassificationStatus { CONFIRMED, INFERRED_HIGH, INFERRED_LOW, UNKNOWN }
public enum AdministrativeDocumentClass { OfficialLetter, NamedAdministrativeDocument, Appendix, Copy, Unknown }

public sealed record SemanticSourceEvidence(
    string SourceKind,
    string ParagraphId,
    IReadOnlyList<string> RunIds,
    int Order,
    string Text,
    IReadOnlyList<string> Reasons);

public sealed record SemanticComponentCandidate(
    string Id,
    SemanticRole Role,
    string Text,
    double Confidence,
    ClassificationMethod Method,
    ClassificationStatus Status,
    SemanticSourceEvidence Evidence,
    IReadOnlyList<string> CompetingCandidateIds);

public sealed record SemanticAmbiguity(
    string Code,
    string Message,
    IReadOnlyList<string> CandidateIds);

public sealed record DocumentClassificationResult(
    AdministrativeDocumentClass DocumentClass,
    string? SpecificType,
    double Confidence,
    ClassificationStatus Status,
    bool NeedsReview,
    IReadOnlyList<string> EvidenceComponentIds,
    IReadOnlyList<string> CompetingTypes);

public sealed record TemplateMatchResult(
    string? TemplateId,
    double Confidence,
    ClassificationStatus Status,
    bool NeedsReview,
    IReadOnlyList<string> EvidenceComponentIds,
    IReadOnlyList<string> CandidateTemplateIds);

public sealed record SemanticDocumentModel(
    string DocumentId,
    DocumentClassificationResult Classification,
    TemplateMatchResult TemplateMatch,
    IReadOnlyList<SemanticComponentCandidate> Components,
    IReadOnlyList<SemanticAmbiguity> Ambiguities,
    bool RequiresReview,
    IReadOnlyList<string> Diagnostics);
