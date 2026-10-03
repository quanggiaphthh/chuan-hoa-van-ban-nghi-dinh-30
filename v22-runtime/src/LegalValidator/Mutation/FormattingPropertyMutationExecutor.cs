using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Package;
using Nd30.DocumentEngine.Safety;
using DocumentFormat.OpenXml;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Profiles;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;

namespace Nd30.LegalValidator.Mutation;

/// <summary>Which part of the OOXML tree a profile property lives on.</summary>
public enum FormattingTargetKind { Paragraph, Section }

/// <summary>
/// An exact, confirmed formatting change: one property, one target, an exact
/// expected-before value and an exact desired-after value, bound to the document
/// state, the rule identity and the profile/rule content that were in force when
/// the proposal was produced by the serving flow.
///
/// <see cref="MutationIntentId"/> is derived from all of that content via
/// <see cref="ComputeId"/>. It is therefore verifiable: changing any field without
/// recomputing the id is detected, which is what stops a tampered desired value
/// from riding an existing authorization artifact.
/// </summary>
public sealed record FormattingMutationIntent(
    string MutationIntentId,
    string ProposalId,
    string RuleId,
    string Property,
    string TargetId,
    FormattingTargetKind Kind,
    string Unit,
    string? ExpectedBefore,
    string DesiredAfter,
    DocumentStateBinding StateBinding,
    string ProfileDigest,
    string RuleContentDigest)
{
    /// <summary>Canonical content identity. Deterministic and covers every bound field.</summary>
    public static string ComputeId(
        string proposalId,
        string ruleId,
        string property,
        string targetId,
        FormattingTargetKind kind,
        string unit,
        string? expectedBefore,
        string desiredAfter,
        DocumentStateBinding stateBinding,
        string profileDigest,
        string ruleContentDigest) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("|",
            "formatting-intent",
            proposalId,
            ruleId,
            property,
            targetId,
            kind.ToString(),
            unit,
            expectedBefore ?? "-",
            desiredAfter,
            stateBinding.ExpectedDocument.Algorithm,
            stateBinding.ExpectedDocument.Digest,
            stateBinding.ExpectedRule.RuleId,
            stateBinding.ExpectedRule.SourceId,
            stateBinding.ExpectedRule.SourceLocator,
            profileDigest,
            ruleContentDigest)))).ToLowerInvariant()[..24];

    /// <summary>True only for a 64-character lowercase hex SHA-256 digest.</summary>
    public static bool IsWellFormedDigest(string? value) =>
        value is { Length: 64 } && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}

public sealed record FormattingAuthorization(
    string AuthorizationId,
    string ProposalId,
    string MutationIntentId,
    string Scope = "single-exact-formatting-mutation");

public enum FormattingAuthorizationDecision
{
    AUTHORIZED, MISSING_AUTHORIZATION, MALFORMED_AUTHORIZATION, PROPOSAL_MISMATCH,
    DOCUMENT_STATE_MISMATCH, STALE_STATE, PROPOSAL_NOT_ELIGIBLE, SAFETY_BLOCKED, UNSUPPORTED_PROPERTY,
    INTENT_MISMATCH, PROFILE_TUPLE_MISMATCH,
}

public sealed record FormattingAuthorizationResult(
    FormattingAuthorizationDecision Decision,
    bool Authorized,
    RemediationProposal? Proposal,
    string Reason);

/// <summary>
/// An authorized exact formatting mutation, produced only by
/// <see cref="FormattingAuthorizationBoundary"/>. The executor accepts nothing
/// else, so a bare intent cannot reach a write.
/// </summary>
public sealed record AuthorizedFormattingMutationRequest(
    string RequestId,
    string IdempotencyKey,
    string ProposalId,
    string RuleId,
    DocumentStateBinding StateBinding,
    AuthorizationArtifact Authorization,
    FormattingMutationIntent Intent);

/// <summary>
/// Authorization boundary for profile formatting mutations.
///
/// It reuses the same authority primitives as the existing alignment path \u2014
/// <see cref="RemediationProposal"/>, <see cref="AuthorizationArtifact"/>,
/// <see cref="DocumentStateBinding"/> and <see cref="StaleStateVerifier"/> \u2014 and
/// keeps the same prohibitions: safety can never be overridden, a proposal must
/// be ELIGIBLE and bound to the exact document state, and the artifact must match
/// the proposal and the exact intent it claims to authorize.
/// </summary>
public static class FormattingAuthorizationBoundary
{
    public const string RequestScope = "single-exact-formatting-mutation";

    public static FormattingAuthorizationResult Authorize(
        RemediationProposal proposal,
        PackageSafetyState safety,
        AuthorizationArtifact? artifact,
        DocumentIdentity actualDocument,
        RuleIdentity actualRule,
        FormattingMutationIntent intent,
        ProfileMutationTarget? liveTarget = null)
    {
        // The profile owns the exact property -> rule tuple. An intent may not pair
        // a profile property with a rule it was never defined against. When the
        // caller supplies the live profile definition, that is what is checked.
        var profileTarget = liveTarget
            ?? FormattingProfileV1.MutationTargets.FirstOrDefault(target => string.Equals(target.Property, intent.Property, StringComparison.OrdinalIgnoreCase));
        if (profileTarget is null)
            return new(FormattingAuthorizationDecision.UNSUPPORTED_PROPERTY, false, proposal, "Property is not part of the company formatting profile.");
        if (!string.Equals(profileTarget.RuleId, intent.RuleId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.PROFILE_TUPLE_MISMATCH, false, proposal, "Property and rule are not a declared profile pair.");
        // The intent must also match the live definition's scope and unit, so a
        // changed profile cannot silently reinterpret a confirmed request.
        if (!string.Equals(profileTarget.Scope, intent.Kind == FormattingTargetKind.Section ? "section" : "paragraph", StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.PROFILE_TUPLE_MISMATCH, false, proposal, "Target scope does not match the current profile definition.");

        if (artifact is null)
            return new(FormattingAuthorizationDecision.MISSING_AUTHORIZATION, false, proposal, "Authorization artifact required.");
        if (!IsHex(artifact.AuthorizationId) || artifact.AuthorizationId.Length != 24)
            return new(FormattingAuthorizationDecision.MALFORMED_AUTHORIZATION, false, proposal, "Authorization artifact is malformed.");
        if (!string.Equals(artifact.Scope, RequestScope, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.MALFORMED_AUTHORIZATION, false, proposal, "Authorization scope does not cover a single exact formatting mutation.");
        if (!string.Equals(artifact.ProposalId, proposal.ProposalId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.PROPOSAL_MISMATCH, false, proposal, "Authorization does not match the proposal.");
        if (!string.Equals(artifact.MutationIntentId, intent.MutationIntentId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.INTENT_MISMATCH, false, proposal, "Authorization does not match the exact intent.");
        if (proposal.Decision != RemediationDecision.ELIGIBLE || !proposal.Executable || proposal.StateBinding is null)
            return new(FormattingAuthorizationDecision.PROPOSAL_NOT_ELIGIBLE, false, proposal, "Proposal is not eligible for execution.");
        if (!string.Equals(proposal.RuleId, intent.RuleId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.PROPOSAL_MISMATCH, false, proposal, "Proposal rule does not match the intended rule.");
        if (proposal.TargetId is null || !string.Equals(proposal.TargetId, intent.TargetId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.PROPOSAL_MISMATCH, false, proposal, "Proposal target does not match the intended target.");
        // Safety is never overridable by an authorization artifact.
        if (!safety.IsReadable
            || safety.IsSigned || safety.IsProtected
            || safety.PatchPolicy is PatchPolicy.AUDIT_ONLY or PatchPolicy.PROHIBITED
            || safety.UnsupportedFeatures.Count > 0)
            return new(FormattingAuthorizationDecision.SAFETY_BLOCKED, false, proposal, "Package safety forbids mutation.");
        if (!string.Equals(proposal.StateBinding.ExpectedDocument.Algorithm, intent.StateBinding.ExpectedDocument.Algorithm, StringComparison.Ordinal)
            || !string.Equals(proposal.StateBinding.ExpectedDocument.Digest, intent.StateBinding.ExpectedDocument.Digest, StringComparison.Ordinal)
            || !string.Equals(proposal.StateBinding.ExpectedRule.RuleId, intent.StateBinding.ExpectedRule.RuleId, StringComparison.Ordinal)
            || !string.Equals(proposal.StateBinding.ExpectedRule.SourceId, intent.StateBinding.ExpectedRule.SourceId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.DOCUMENT_STATE_MISMATCH, false, proposal, "Proposal and intent are bound to different document or rule state.");
        if (!string.Equals(proposal.StateBinding.ExpectedDocument.Digest, actualDocument.Digest, StringComparison.Ordinal)
            || !string.Equals(proposal.StateBinding.ExpectedDocument.Algorithm, actualDocument.Algorithm, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.DOCUMENT_STATE_MISMATCH, false, proposal, "Proposal was bound to a different document state.");
        if (!string.Equals(actualRule.RuleId, intent.RuleId, StringComparison.Ordinal))
            return new(FormattingAuthorizationDecision.PROPOSAL_MISMATCH, false, proposal, "Actual rule identity does not match the intended rule.");
        var stale = StaleStateVerifier.Verify(intent.StateBinding, actualDocument, actualRule);
        if (stale.Decision is not StaleStateDecision.MATCH)
            return new(FormattingAuthorizationDecision.STALE_STATE, false, proposal, "Document state or rule identity is stale.");
        return new(FormattingAuthorizationDecision.AUTHORIZED, true, proposal, "Exact formatting mutation authorized.");
}

private static string StableId(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    private static bool IsHex(string value)
    {
        foreach (var character in value)
            if (!Uri.IsHexDigit(character)) return false;
        return true;
    }
}

public enum FormattingMutationOutcome
{
    SUCCESS, STALE_DOCUMENT, AUTHORIZATION_REJECTED, PACKAGE_NOT_MUTABLE, TARGET_NOT_FOUND,
    TARGET_AMBIGUOUS, PROVENANCE_NOT_DIRECT, PRECONDITION_FAILED, UNSUPPORTED_OPERATION,
    UNSUPPORTED_PROPERTY, PROFILE_CHANGED, STALE_RULE_CONTENT, PROFILE_TUPLE_MISMATCH,
    MISSING_BINDING, INTENT_CONTENT_MISMATCH,
    INVALID_VALUE, OUTPUT_PATH_INVALID, OUTPUT_ALREADY_EXISTS, WRITE_FAILED,
    POSTCONDITION_FAILED, OUTPUT_INTEGRITY_FAILED, SOURCE_IMMUTABILITY_VIOLATION,
    OUTPUT_TOO_LARGE, PROCESSING_CANCELLED,
}

public sealed record FormattingMutationVerification(
    bool Reopened, bool IntegrityValid, bool TargetVerified, bool PackageSafetyNormal,
    bool SourceUnchanged, bool OutputChanged);

public sealed record FormattingMutationResult(
    FormattingMutationOutcome Outcome,
    bool Success,
    string Reason,
    string Property,
    string TargetId,
    string? Before,
    string? After,
    string RuleId,
    string MutationIntentId,
    DocumentIdentity InputDocument,
    DocumentIdentity? OutputDocument,
    FormattingMutationVerification? Verification);

/// <summary>
/// A trusted authorization payload handed to the executor by the serving flow.
///
/// It is a *claim*, not authority, and it deliberately carries no digest fields:
/// those live on <see cref="FormattingMutationIntent"/> and are produced by the
/// serving flow from its own inspection. Packaging here cannot refresh or launder
/// them, and every current fact the executor needs \u2014 the live profile definition,
/// the live rule content, the live document identity \u2014 is re-derived from the
/// canonical catalog, the canonical profile and a fresh read of the source.
/// </summary>
public sealed record TrustedFormattingAuthorization(
    FormattingMutationIntent Intent,
    AuthorizationArtifact Authorization,
    string ProposalId,
    string IdempotencyKey,
    string RequestId);

/// <summary>
/// Surgical executor for the profile's mandatory formatting families.
///
/// It only accepts a <see cref="TrustedFormattingAuthorization"/> payload and
/// re-validates it against the canonical catalog and profile it was constructed
/// with, so a request issued under an older profile or an edited rule is rejected
/// by comparing against the *current* definition rather than a copy of the
/// expected one.
///
/// It writes exactly one OOXML property, never flattens runs, never touches
/// styles, numbering, tables, headers, footers, sections beyond the single
/// targeted <c>sectPr</c>, or signatures, and never writes a property the owner
/// did not confirm. Everything else must remain byte-identical, which is proved
/// by a canonical XML fingerprint taken with the mutated element removed, and the
/// source is re-hashed immediately before the output is published.
/// </summary>
public sealed class FormattingPropertyMutationExecutor
{
    private static readonly XNamespace Wns = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const double TwipsPerMillimetre = 1440d / 25.4d;
    private const double TwipsPerCentimetre = 1440d / 2.54d;
    private readonly RuleCatalog _catalog;
    private readonly FormattingProfile _profile;

    public FormattingPropertyMutationExecutor(RuleCatalog catalog, FormattingProfile profile)
    {
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        _profile = profile ?? throw new ArgumentNullException(nameof(profile));
    }

    /// <summary>
    /// Package an already server-owned intent and authorization artifact.
    ///
    /// This stamps nothing: it cannot refresh a profile digest or a rule content
    /// digest, so packaging a proposal that is stale under the current profile
    /// leaves it stale.
    /// </summary>
    public TrustedFormattingAuthorization Package(
        FormattingMutationIntent intent,
        AuthorizationArtifact authorization,
        string idempotencyKey,
        string requestId)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(authorization);
        return new(intent, authorization, intent.ProposalId, idempotencyKey, requestId);
    }

    /// <summary>Current digest of the bound rule as it stands in the catalog right now.</summary>
    public string CurrentRuleContentDigest(string ruleId) =>
        FormattingProfile.ComputeRuleContentDigest(_catalog, ruleId);

    /// <summary>Digest of the rule bound to a profile property, as it stands right now.</summary>
    public string CurrentRuleContentDigestFor(string property) =>
        CurrentRuleContentDigest(_profile.FindMutationTarget(property)?.RuleId ?? string.Empty);

    private RuleDefinition CurrentRuleOrThrow(string ruleId) =>
        _catalog.Rules.FirstOrDefault(rule => string.Equals(rule.Id, ruleId, StringComparison.Ordinal))
        ?? throw new InvalidOperationException("Bound rule is not present in the verified release catalog.");

    public FormattingMutationResult Execute(
        string source,
        string output,
        TrustedFormattingAuthorization trusted,
        CancellationToken cancellationToken = default)
    {
        var intent = trusted.Intent;
        DocumentIdentity input;
        try { input = DocumentIdentityService.FromFile(source); }
        catch (Exception error) { return Fail(FormattingMutationOutcome.WRITE_FAILED, error.Message, intent, input: new("SHA-256", new string('0', 64))); }

        var inputDigest = input.Digest;
        string? temporary = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            // --- Re-derive every current fact from canonical sources. ---
            // Both digests are mandatory and must be well formed. An absent or
            // malformed digest is a rejection, never a skipped check.
            if (!FormattingMutationIntent.IsWellFormedDigest(intent.ProfileDigest))
                return Fail(FormattingMutationOutcome.MISSING_BINDING, "The proposal carries no well-formed formatting profile binding.", intent, input);
            if (!FormattingMutationIntent.IsWellFormedDigest(intent.RuleContentDigest))
                return Fail(FormattingMutationOutcome.MISSING_BINDING, "The proposal carries no well-formed rule content binding.", intent, input);

            if (!string.Equals(intent.ProfileDigest, _profile.Digest, StringComparison.Ordinal))
                return Fail(FormattingMutationOutcome.PROFILE_CHANGED, "The formatting profile changed after this proposal was produced.", intent, input);

            ProfileMutationTarget liveTarget;
            RuleDefinition liveRule;
            try
            {
                liveTarget = _profile.FindMutationTarget(intent.Property)
                    ?? throw new InvalidDataException("Property is not part of the current profile.");
                liveRule = CurrentRuleOrThrow(intent.RuleId);
            }
            catch (InvalidDataException error)
            {
                return Fail(FormattingMutationOutcome.UNSUPPORTED_PROPERTY, error.Message, intent, input);
            }

            // A rule edited under the same ID must invalidate the proposal.
            var currentRuleDigest = CurrentRuleContentDigest(liveRule.Id);
            if (!string.Equals(intent.RuleContentDigest, currentRuleDigest, StringComparison.Ordinal))
                return Fail(FormattingMutationOutcome.STALE_RULE_CONTENT, "The bound rule content changed after this proposal was produced.", intent, input);

            // The intent's tuple must still match the live profile definition.
            if (!string.Equals(liveTarget.RuleId, intent.RuleId, StringComparison.Ordinal))
                return Fail(FormattingMutationOutcome.PROFILE_TUPLE_MISMATCH, "Property and rule are not a declared pair in the current profile.", intent, input);
            if (!string.Equals(liveTarget.Unit, intent.Unit, StringComparison.Ordinal))
                return Fail(FormattingMutationOutcome.INTENT_CONTENT_MISMATCH, "The proposal unit does not match the current profile definition.", intent, input);

            // The identity must be recomputable from the exact content. A tampered
            // target, before/after value, unit or digest keeps a stale id and is
            // rejected here even when an artifact carries that same id.
            var recomputed = FormattingMutationIntent.ComputeId(
                intent.ProposalId, intent.RuleId, intent.Property, intent.TargetId, intent.Kind,
                intent.Unit, intent.ExpectedBefore, intent.DesiredAfter, intent.StateBinding,
                intent.ProfileDigest, intent.RuleContentDigest);
            if (!string.Equals(recomputed, intent.MutationIntentId, StringComparison.Ordinal))
                return Fail(FormattingMutationOutcome.INTENT_CONTENT_MISMATCH, "The confirmed intent identity does not match its content.", intent, input);

            // Current document state versus the state the proposal was bound to.
            if (!string.Equals(intent.StateBinding.ExpectedDocument.Algorithm, input.Algorithm, StringComparison.Ordinal)
                || !string.Equals(intent.StateBinding.ExpectedDocument.Digest, inputDigest, StringComparison.Ordinal))
                return Fail(FormattingMutationOutcome.STALE_DOCUMENT, "Stale document state.", intent, input);

            // Current rule identity, taken from the catalog rather than the request.
            var actualRule = new RuleIdentity(liveRule.Id, liveRule.Source.SourceId, liveRule.Source.Locator);

            if (trusted.Authorization is null)
                return Fail(FormattingMutationOutcome.AUTHORIZATION_REJECTED, "Authorization artifact required.", intent, input);

            var safety = PackageSafetyPreflight.Inspect(source, cancellationToken);
            var authorized = FormattingAuthorizationBoundary.Authorize(
                BuildProposalForReview(trusted, liveTarget, liveRule, intent), safety, trusted.Authorization, input, actualRule, intent, liveTarget);
            if (!authorized.Authorized)
                return Fail(FormattingMutationOutcome.AUTHORIZATION_REJECTED, authorized.Reason, intent, input);

            if (!safety.IsReadable || safety.PatchPolicy != PatchPolicy.NORMAL
                || safety.IsSigned || safety.IsProtected || safety.UnsupportedFeatures.Count > 0)
                return Fail(FormattingMutationOutcome.PACKAGE_NOT_MUTABLE, "Only NORMAL packages can be mutated.", intent, input);

            var sourcePath = Path.GetFullPath(source);
            var outputPath = Path.GetFullPath(output);
            if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
                return Fail(FormattingMutationOutcome.OUTPUT_PATH_INVALID, "Output equals source.", intent, input);
            if (File.Exists(outputPath))
                return Fail(FormattingMutationOutcome.OUTPUT_ALREADY_EXISTS, "Output already exists.", intent, input);
            var directory = Path.GetDirectoryName(outputPath)!;
            Directory.CreateDirectory(directory);
            temporary = Path.Combine(directory, $".{Path.GetFileName(outputPath)}.{Guid.NewGuid():N}.tmp");

            PackagePreserver.CopyWithoutMutation(source, temporary, cancellationToken);
            if (!string.Equals(DocumentIdentityService.FromFile(temporary).Digest, inputDigest, StringComparison.Ordinal))
                return Clean(FormattingMutationOutcome.STALE_DOCUMENT, "Input snapshot changed after preflight.", intent, input, temporary);

            if (!IsParagraphProperty(intent.Property))
            {
                // Section scope lives in word/document.xml sectPr; nothing else
                // in the package may change.
                var otherParts = PackagePreserver.PartSha256Inventory(temporary, cancellationToken)
                    .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                var beforeSection = ReadSectionValue(temporary, intent);
                if (beforeSection is null)
                    return Clean(FormattingMutationOutcome.PROVENANCE_NOT_DIRECT, "The section property is not declared directly.", intent, input, temporary);
                if (!string.Equals(beforeSection, intent.ExpectedBefore, StringComparison.Ordinal))
                    return Clean(FormattingMutationOutcome.PRECONDITION_FAILED, "The section value changed after inspection.", intent, input, temporary);

                var originalFingerprint = StructureFingerprint(temporary, intent);
                using (var document = WordprocessingDocument.Open(temporary, true))
                {
var sections = BodySections(document);
                    if (sections.Count != 1)
                        throw new InvalidDataException("Target drift.");
                    // BodySections already yields the resolved section properties.
                    WriteSectionValue(sections[0], intent);
                    document.MainDocumentPart!.Document.Save();
                }
                return Publish(source, inputDigest, temporary, outputPath, intent, input, otherParts, null, originalFingerprint, beforeSection, cancellationToken);
            }

            var index = ParagraphIndex(intent.TargetId);
            if (index <= 0) return Fail(FormattingMutationOutcome.TARGET_NOT_FOUND, "The selected paragraph is not available.", intent, input);

            var unrelatedText = OtherParagraphText(temporary, index);
            var beforeParagraph = ReadParagraphValue(temporary, intent, index);
            if (beforeParagraph is null)
                return Clean(FormattingMutationOutcome.PROVENANCE_NOT_DIRECT, "The paragraph property is not declared directly.", intent, input, temporary);
            if (!string.Equals(beforeParagraph, intent.ExpectedBefore, StringComparison.Ordinal))
                return Clean(FormattingMutationOutcome.PRECONDITION_FAILED, "The paragraph value changed after inspection.", intent, input, temporary);

            var otherPartsParagraph = PackagePreserver.PartSha256Inventory(temporary, cancellationToken)
                .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var paragraphFingerprint = StructureFingerprint(temporary, intent);

            using (var document = WordprocessingDocument.Open(temporary, true))
            {
                var paragraphs = document.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ToList();
                if (index > paragraphs.Count) throw new InvalidDataException("Target drift.");
                var properties = paragraphs[index - 1].ParagraphProperties
                    ?? throw new InvalidDataException("Paragraph properties missing.");
                WriteParagraphValue(properties, intent);
                document.MainDocumentPart.Document.Save();
            }
            return Publish(source, inputDigest, temporary, outputPath, intent, input, otherPartsParagraph, (unrelatedText, index), paragraphFingerprint, beforeParagraph, cancellationToken);
        }
catch (OperationCanceledException) { return Clean(FormattingMutationOutcome.PROCESSING_CANCELLED, "Mutation cancelled.", intent, input, temporary); }
        catch (DocumentPackageException error) { return Clean(ErrorCodeFor(error.Code), error.SafeMessage, intent, input, temporary); }
        // These carry our own fixed literals: no path, no document text, no provider detail.
        catch (InvalidDataException error) { return Clean(FormattingMutationOutcome.PROVENANCE_NOT_DIRECT, error.Message, intent, input, temporary); }
        catch (UnauthorizedAccessException) { return Clean(FormattingMutationOutcome.WRITE_FAILED, "The formatting change could not be written safely.", intent, input, temporary); }
        catch (IOException) { return Clean(FormattingMutationOutcome.WRITE_FAILED, "The formatting change could not be written safely.", intent, input, temporary); }
        catch (Exception) { return Clean(FormattingMutationOutcome.WRITE_FAILED, "The formatting change could not be completed.", intent, input, temporary); }
    }

private FormattingMutationResult Publish(
        string source,
        string inputDigest,
        string temporary,
        string outputPath,
        FormattingMutationIntent intent,
        DocumentIdentity input,
        KeyValuePair<string, string>[] otherParts,
        (string Text, int Index)? unrelated,
        string originalFingerprint,
        string before,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(temporary).Length > PackageSafetyLimits.MaximumCompressedInputBytes)
            return Clean(FormattingMutationOutcome.OUTPUT_TOO_LARGE, "The generated DOCX exceeds the allowed size.", intent, input, temporary);

        var postWriteSafety = PackageSafetyPreflight.Inspect(temporary, cancellationToken);
        if (!postWriteSafety.IsReadable || postWriteSafety.PatchPolicy != PatchPolicy.NORMAL)
            return Clean(FormattingMutationOutcome.OUTPUT_INTEGRITY_FAILED, "The generated DOCX failed safety checks.", intent, input, temporary);

        // Reopen and confirm the exact property, then prove nothing else moved.
        string? after;
        using (var document = WordprocessingDocument.Open(temporary, false))
        {
            after = IsParagraphProperty(intent.Property)
                ? ReadParagraphValue(temporary, intent, ParagraphIndex(intent.TargetId))
                : ReadSectionValue(temporary, intent);
        }
        if (!string.Equals(after, intent.DesiredAfter, StringComparison.Ordinal))
            return Clean(FormattingMutationOutcome.POSTCONDITION_FAILED, "The requested change was not present after reopening.", intent, input, temporary);

        if (!otherParts.SequenceEqual(PackagePreserver.PartSha256Inventory(temporary, cancellationToken)
                .Where(part => !part.Key.Equals("word/document.xml", StringComparison.OrdinalIgnoreCase))))
            return Clean(FormattingMutationOutcome.OUTPUT_INTEGRITY_FAILED, "Unrelated DOCX package parts changed.", intent, input, temporary);
        if (!string.Equals(StructureFingerprint(temporary, intent), originalFingerprint, StringComparison.Ordinal))
            return Clean(FormattingMutationOutcome.OUTPUT_INTEGRITY_FAILED, "Unrelated DOCX structures changed.", intent, input, temporary);
        if (unrelated is { } value && !string.Equals(OtherParagraphText(temporary, value.Index), value.Text, StringComparison.Ordinal))
            return Clean(FormattingMutationOutcome.OUTPUT_INTEGRITY_FAILED, "Unrelated paragraph text changed.", intent, input, temporary);

var output = DocumentIdentityService.FromFile(temporary);

        // Re-hash the source immediately before publishing. The SourceUnchanged
        // claim must rest on this final read, not on the digest taken at the start
        // of the flow.
        if (!string.Equals(DocumentIdentityService.FromFile(source).Digest, inputDigest, StringComparison.Ordinal))
            return Clean(FormattingMutationOutcome.SOURCE_IMMUTABILITY_VIOLATION, "The source DOCX changed during processing.", intent, input, temporary);

        File.Move(temporary, outputPath);
        // No cleanup reference is cleared here: after the move the temporary file no longer
        // exists and `temporary` is never read again on this path, so there is nothing to null
        // out. Assigning null to the non-nullable `Publish` parameter only produced CS8600.
        return new(FormattingMutationOutcome.SUCCESS, true, "Applied.", intent.Property, intent.TargetId, before, after,
            intent.RuleId, intent.MutationIntentId, input, output,
            new(true, true, true, true, true, !string.Equals(output.Digest, input.Digest, StringComparison.Ordinal)));
    }

    /// <summary>
    /// Rebuild the proposal for review from the live rule definition and the
    /// trusted payload.
    ///
    /// Eligibility is not invented here: it is taken from the rule's own autofix
    /// policy in the verified catalog, and the executor additionally refuses any
    /// rule whose policy would not permit an automatic fix. A rule that is
    /// review-only therefore never reaches a write.
    /// </summary>
    private static RemediationProposal BuildProposalForReview(
        TrustedFormattingAuthorization trusted,
        ProfileMutationTarget liveTarget,
        RuleDefinition liveRule,
        FormattingMutationIntent intent)
    {
        var fixable = liveRule.AutofixPolicy is "SAFE" or "GUARDED";
        return new RemediationProposal(
            trusted.ProposalId,
            liveRule.Id,
            StableId(string.Join("|", "formatting-finding", trusted.ProposalId)),
            [trusted.Authorization.AuthorizationId],
            intent.TargetId,
            liveTarget.Property,
            intent.ExpectedBefore ?? "unset",
            intent.DesiredAfter,
            fixable ? RemediationDecision.ELIGIBLE : RemediationDecision.SUGGESTION_ONLY,
            fixable,
            "Confirmed formatting operation re-checked against the live rule definition.",
            intent.StateBinding);
    }

    private static string StableId(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    /// <summary>
    /// Paragraph-scoped properties. The canonical profile name for line spacing is
    /// <c>paragraph.line_spacing_lines</c>; the shorter legacy spelling is still
    /// accepted so older callers keep working. Both must be recognised here,
    /// otherwise a line-spacing target would be routed to the section resolver and
    /// silently written nowhere.
    /// </summary>
    public static bool IsParagraphProperty(string property) => property switch
    {
        "paragraph.alignment" or "paragraph.first_line_indent_cm" or "paragraph.spacing_after_pt"
            or "paragraph.line_spacing" or "paragraph.line_spacing_lines" or "run.font_size_pt" => true,
        _ => false,
    };

    private static int ParagraphIndex(string targetId) =>
        targetId.Length is > 1 and <= 10 && targetId[0] == 'p'
        && int.TryParse(targetId.AsSpan(1), out var value) && value > 0 ? value : 0;

/// <summary>
    /// Section properties in document order. Word stores a section either as a
    /// direct child of <c>w:body</c> or, for the final section, inside the last
    /// paragraph's <c>w:pPr</c>. Both forms are real, so both must resolve to the
    /// same target the parser reported.
    /// </summary>
    private static List<W.SectionProperties> BodySections(WordprocessingDocument document)
    {
        var body = document.MainDocumentPart!.Document.Body!;
        var sections = new List<W.SectionProperties>();
        foreach (var element in body.ChildElements)
        {
            if (element is W.Paragraph paragraph)
            {
                var inner = paragraph.ParagraphProperties?.GetFirstChild<W.SectionProperties>();
                if (inner is not null) sections.Add(inner);
                continue;
            }
            if (element is W.SectionProperties direct) sections.Add(direct);
        }
        return sections;
    }

    /// <summary>Read the current direct value in the unit the profile declares.</summary>
    public static string? ReadParagraphValue(string path, FormattingMutationIntent intent, int index)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var paragraphs = document.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ToList();
        if (index <= 0 || index > paragraphs.Count) return null;
        var properties = paragraphs[index - 1].ParagraphProperties;
        if (properties is null) return null;
        return intent.Property switch
        {
            "paragraph.alignment" => properties.GetFirstChild<W.Justification>()?.Val?.Value.ToString().ToUpperInvariant(),
            "paragraph.first_line_indent_cm" => ReadCentimetres(properties.GetFirstChild<W.Indentation>()?.FirstLine?.Value),
            "paragraph.spacing_after_pt" => ReadPoints(properties.GetFirstChild<W.SpacingBetweenLines>()?.After?.Value),
            "paragraph.line_spacing_lines" => ReadLineSpacingLines(properties.GetFirstChild<W.SpacingBetweenLines>()),
            "run.font_size_pt" => ReadRunFontSize(paragraphs[index - 1]),
            _ => null,
        };
    }

    public static string? ReadSectionValue(string path, FormattingMutationIntent intent)
    {
using (var document = WordprocessingDocument.Open(path, false))
        {
            var properties = BodySections(document).SingleOrDefault();
            if (properties is null) return null;
            return intent.Property switch
            {
                "section.page_size" => properties.GetFirstChild<W.PageSize>() is { } size ? PageSizeName(size) : null,
                "section.orientation" => NormalizeOrientation(properties.GetFirstChild<W.PageSize>()?.Orient?.Value.ToString()),
                "section.margin_top_mm" => ReadMargin(properties.GetFirstChild<W.PageMargin>()?.Top?.Value),
                "section.margin_right_mm" => ReadMargin(properties.GetFirstChild<W.PageMargin>()?.Right?.Value),
                "section.margin_bottom_mm" => ReadMargin(properties.GetFirstChild<W.PageMargin>()?.Bottom?.Value),
                "section.margin_left_mm" => ReadMargin(properties.GetFirstChild<W.PageMargin>()?.Left?.Value),
                _ => null,
            };
        }
    }

private static string? ReadMargin(long? twips) =>
        twips is { } value ? Number(Round(value / TwipsPerMillimetre, 1)) : null;

    private static string? ReadCentimetres(string? twips) =>
        uint.TryParse(twips, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Number(Round(value / TwipsPerCentimetre, 2))
            : null;

    private static string? ReadPoints(string? twips) =>
        int.TryParse(twips, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? Number(Round(value / 20d, 2))
            : null;

    /// <summary>
    /// Only a multiple-of-lines spacing is readable as lines. An exact/at-least
    /// spacing is an absolute point value and is deliberately not converted, so it
    /// can never be compared against a line-count range.
    /// </summary>
    private static string? ReadLineSpacingLines(W.SpacingBetweenLines? spacing)
    {
        if (spacing?.Line?.Value is not { Length: > 0 } lineText) return null;
        if (!int.TryParse(lineText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var line)) return null;
        var rule = spacing.LineRule?.Value;
        if (rule is { } value && value != W.LineSpacingRuleValues.Auto) return null;
        return Number(Round(line / 240d, 4));
    }

    /// <summary>
    /// Font size is a run property, so it is only readable when every run in the
    /// paragraph already declares the same direct value.
    ///
    /// A run without a direct size is inherited, not 0 and not "the others' value".
    /// Dropping such a run and treating the remainder as uniform would report a
    /// size the paragraph does not actually have, so this returns null instead and
    /// the caller fails closed.
    /// </summary>
    private static string? ReadRunFontSize(W.Paragraph paragraph)
    {
        var runs = paragraph.Descendants<W.Run>().ToList();
        if (runs.Count == 0) return null;
        var sizes = new List<uint>();
        foreach (var run in runs)
        {
            var declared = run.RunProperties?.FontSize?.Val?.Value;
            if (string.IsNullOrEmpty(declared)) return null;
            if (!uint.TryParse(declared, NumberStyles.Integer, CultureInfo.InvariantCulture, out var halfPoints)) return null;
            sizes.Add(halfPoints);
        }
        // Half-points are the DOCX storage unit; the profile works in points.
        return sizes.Distinct().Count() == 1 ? Number(Round(sizes[0] / 2d, 2)) : null;
    }

    private static void WriteParagraphValue(W.ParagraphProperties properties, FormattingMutationIntent intent)
    {
        switch (intent.Property)
        {
            case "paragraph.alignment":
            {
                var justification = properties.GetFirstChild<W.Justification>()
                    ?? throw new InvalidDataException("Direct alignment missing.");
                justification.Val = intent.DesiredAfter switch
                {
                    "LEFT" => W.JustificationValues.Left,
                    "CENTER" => W.JustificationValues.Center,
                    "RIGHT" => W.JustificationValues.Right,
                    "JUSTIFY" => W.JustificationValues.Both,
                    _ => throw new InvalidDataException("Unsupported alignment."),
                };
                return;
            }
            case "paragraph.first_line_indent_cm":
            {
                if (!double.TryParse(intent.DesiredAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var cm))
                    throw new InvalidDataException("Unsupported indent.");
                var indentation = properties.GetFirstChild<W.Indentation>()
                    ?? throw new InvalidDataException("Direct indentation missing.");
                indentation.FirstLine = ((int)Math.Round(cm * TwipsPerCentimetre, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                return;
            }
            case "paragraph.spacing_after_pt":
            {
                if (!double.TryParse(intent.DesiredAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var pt))
                    throw new InvalidDataException("Unsupported spacing.");
                var spacing = properties.GetFirstChild<W.SpacingBetweenLines>()
                    ?? throw new InvalidDataException("Direct spacing missing.");
                spacing.After = ((int)Math.Round(pt * 20d, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                return;
            }
            case "paragraph.line_spacing":
            case "paragraph.line_spacing_lines":
            {
                if (!double.TryParse(intent.DesiredAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var lines))
                    throw new InvalidDataException("Unsupported line spacing.");
                var spacing = properties.GetFirstChild<W.SpacingBetweenLines>()
                    ?? throw new InvalidDataException("Direct spacing missing.");
                spacing.LineRule = W.LineSpacingRuleValues.Auto;
                spacing.Line = ((int)Math.Round(lines * 240d, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
                return;
            }
case "run.font_size_pt":
            {
                if (!double.TryParse(intent.DesiredAfter, NumberStyles.Float, CultureInfo.InvariantCulture, out var points))
                    throw new InvalidDataException("Unsupported font size.");
                var halfPoints = (int)Math.Round(points * 2d, MidpointRounding.AwayFromZero);
                if (halfPoints is < 2 or > 400) throw new InvalidDataException("Font size out of range.");
                var runs = properties.Parent!.Descendants<W.Run>().ToList();
                if (runs.Count == 0) throw new InvalidDataException("Paragraph has no runs.");
                foreach (var run in runs)
                {
                    var runProperties = run.RunProperties ?? throw new InvalidDataException("Direct run properties missing.");
                    // Scope is exactly the Latin size the owner confirmed. A run with
                    // no direct w:sz inherits its size, and creating one would
                    // silently outrank the author's style, so this fails closed.
                    if (string.IsNullOrEmpty(runProperties.FontSize?.Val?.Value))
                        throw new InvalidDataException("A run has no direct font size.");
                    runProperties.FontSize = new W.FontSize { Val = halfPoints.ToString(CultureInfo.InvariantCulture) };
                    // w:szCs is deliberately NOT written. The confirmed intent binds
                    // only the Latin size, so a document whose complex-script size
// differs (for example w:sz=28 with w:szCs=36) must keep its
                    // complex-script value untouched.
                }
                return;
            }
            default: throw new InvalidDataException("Unsupported property.");
        }
    }

    private static void WriteSectionValue(W.SectionProperties properties, FormattingMutationIntent intent)
    {
        switch (intent.Property)
        {
            case "section.page_size":
            {
                var size = properties.GetFirstChild<W.PageSize>() ?? throw new InvalidDataException("Direct page size missing.");
                switch (intent.DesiredAfter)
                {
                    case "A4": size.Width = 11906; size.Height = 16838; break;
                    default: throw new InvalidDataException("Unsupported page size.");
                }
                return;
            }
            case "section.orientation":
            {
                var size = properties.GetFirstChild<W.PageSize>() ?? throw new InvalidDataException("Direct page size missing.");
                size.Orient = intent.DesiredAfter switch
                {
                    "portrait" => W.PageOrientationValues.Portrait,
                    "landscape" => W.PageOrientationValues.Landscape,
                    _ => throw new InvalidDataException("Unsupported orientation."),
                };
                return;
            }
            case "section.margin_top_mm":
            case "section.margin_right_mm":
            case "section.margin_bottom_mm":
            case "section.margin_left_mm":
            {
                var margin = properties.GetFirstChild<W.PageMargin>() ?? throw new InvalidDataException("Direct margins missing.");
                var twips = (int)Math.Round(Twips(double.Parse(intent.DesiredAfter, CultureInfo.InvariantCulture)), MidpointRounding.AwayFromZero);
                if (twips < 0 || twips > 31680) throw new InvalidDataException("Margin out of range.");
                switch (intent.Property)
                {
                    case "section.margin_top_mm": margin.Top = twips; break;
                    case "section.margin_right_mm": margin.Right = (uint)twips; break;
                    case "section.margin_bottom_mm": margin.Bottom = twips; break;
                    default: margin.Left = (uint)twips; break;
                }
                return;
            }
            default: throw new InvalidDataException("Unsupported property.");
        }
    }

    /// <summary>
    /// Canonical fingerprint of <c>word/document.xml</c> with exactly the mutated
    /// element removed. If this differs before and after, anything other than the
    /// one intended property moved.
    /// </summary>
    private static string StructureFingerprint(string path, FormattingMutationIntent intent)
    {
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/document.xml")
            ?? throw new DocumentPackageException("MALFORMED_DOCX", "The DOCX document part is missing.");
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
        var body = document.Root?.Element(Wns + "body");
        if (body is null) return string.Empty;

        if (IsParagraphProperty(intent.Property))
        {
            var index = ParagraphIndex(intent.TargetId);
            var paragraph = body.Elements(Wns + "p").ElementAtOrDefault(index - 1);
            var properties = paragraph?.Element(Wns + "pPr");
            if (properties is null) return string.Empty;
            switch (intent.Property)
            {
                case "paragraph.alignment": properties.Elements(Wns + "jc").Remove(); break;
                case "paragraph.first_line_indent_cm":
                {
                    foreach (var indentation in properties.Elements(Wns + "ind"))
                        indentation.Attribute(Wns + "firstLine")?.Remove();
                    break;
                }
                case "paragraph.spacing_after_pt":
                case "paragraph.line_spacing":
                case "paragraph.line_spacing_lines":
                {
                    foreach (var spacing in properties.Elements(Wns + "spacing"))
                    {
                        if (intent.Property == "paragraph.spacing_after_pt") spacing.Attribute(Wns + "after")?.Remove();
                        else { spacing.Attribute(Wns + "line")?.Remove(); spacing.Attribute(Wns + "lineRule")?.Remove(); }
                    }
                    break;
                }
case "run.font_size_pt":
                {
                    // Remove only the Latin size. Leaving w:szCs in the fingerprint
                    // means any change to the complex-script size is detected as an
                    // unrelated-structure change and rejected.
                    foreach (var size in paragraph!.Descendants().Where(node => node.Name == Wns + "sz").ToList())
                        size.Remove();
                    break;
                }
            }
        }
else
        {
            // The same two storage forms Word uses, resolved identically to the
            // read and write paths.
            XElement? section = null;
            foreach (var element in body.Elements())
            {
                if (element.Name == Wns + "sectPr") { section = element; continue; }
                if (element.Name != Wns + "p") continue;
                var inner = element.Element(Wns + "pPr")?.Element(Wns + "sectPr");
                if (inner is not null) section = inner;
            }
            if (section is null) return string.Empty;
            switch (intent.Property)
            {
                case "section.page_size":
                case "section.orientation":
                {
                    foreach (var size in section.Elements(Wns + "pgSz"))
                    {
                        if (intent.Property == "section.page_size") { size.Attribute(Wns + "w")?.Remove(); size.Attribute(Wns + "h")?.Remove(); }
                        else size.Attribute(Wns + "orient")?.Remove();
                    }
                    break;
                }
                default:
                {
                    var attribute = intent.Property switch
                    {
                        "section.margin_top_mm" => "top",
                        "section.margin_right_mm" => "right",
                        "section.margin_bottom_mm" => "bottom",
                        _ => "left",
                    };
                    foreach (var margin in section.Elements(Wns + "pgMar")) margin.Attribute(Wns + attribute)?.Remove();
                    break;
                }
            }
        }

        var canonical = new StringBuilder();
        AppendCanonical(document.Root!, canonical);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()))).ToLowerInvariant();
    }

    private static void AppendCanonical(XElement element, StringBuilder target)
    {
        target.Append('E');
        Token(element.Name.NamespaceName, target);
        Token(element.Name.LocalName, target);
        foreach (var attribute in element.Attributes()
            .Where(attribute => !attribute.IsNamespaceDeclaration)
            .OrderBy(attribute => attribute.Name.NamespaceName, StringComparer.Ordinal)
            .ThenBy(attribute => attribute.Name.LocalName, StringComparer.Ordinal))
        {
            target.Append('A');
            Token(attribute.Name.NamespaceName, target);
            Token(attribute.Name.LocalName, target);
            Token(attribute.Value, target);
        }
        target.Append('>');
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XElement child: AppendCanonical(child, target); break;
                case XText text: target.Append('T'); Token(text.Value, target); break;
                case XComment comment: target.Append('C'); Token(comment.Value, target); break;
                case XProcessingInstruction instruction: target.Append('P'); Token(instruction.Target, target); Token(instruction.Data, target); break;
            }
        }
        target.Append("/E");
        Token(element.Name.NamespaceName, target);
        Token(element.Name.LocalName, target);
    }

    private static void Token(string value, StringBuilder target) => target.Append(value.Length).Append(':').Append(value);

    /// <summary>Concatenated text of every body paragraph except the target, used to prove content preservation.</summary>
    private static string OtherParagraphText(string path, int targetIndex)
    {
        using var document = WordprocessingDocument.Open(path, false);
        var paragraphs = document.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ToList();
        return string.Join("|", paragraphs.Where((_, position) => position + 1 != targetIndex).Select(paragraph => paragraph.InnerText));
    }

    private static string PageSizeName(W.PageSize size)
    {
        if (size.Width?.Value is not { } width || size.Height?.Value is not { } height) return "unknown";
        var longSide = Math.Max(width, height);
        var shortSide = Math.Min(width, height);
        if (Math.Abs(longSide - 16838) <= 8 && Math.Abs(shortSide - 11906) <= 8) return "A4";
        return $"custom:{width}x{height}";
    }

    private static string NormalizeOrientation(string? value) => (value ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "landscape" => "landscape",
        _ => "portrait",
    };

    private static double Round(double value, int digits) => Math.Round(value, digits, MidpointRounding.AwayFromZero);
    private static double Twips(double millimetres) => millimetres * TwipsPerMillimetre;
    private static string Number(double value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static FormattingMutationOutcome ErrorCodeFor(string code) => code switch
    {
        "TEMP_CLEANUP_FAILED" => FormattingMutationOutcome.WRITE_FAILED,
        "PACKAGE_NOT_MUTABLE" or "UNSAFE_ARCHIVE" or "UNSAFE_XML" or "MALFORMED_DOCX" => FormattingMutationOutcome.PACKAGE_NOT_MUTABLE,
        "TARGET_NOT_FOUND" => FormattingMutationOutcome.TARGET_NOT_FOUND,
        "TARGET_AMBIGUOUS" => FormattingMutationOutcome.TARGET_AMBIGUOUS,
        "PRECONDITION_FAILED" => FormattingMutationOutcome.PRECONDITION_FAILED,
        "PROVENANCE_NOT_DIRECT" => FormattingMutationOutcome.PROVENANCE_NOT_DIRECT,
        "OUTPUT_PATH_INVALID" => FormattingMutationOutcome.OUTPUT_PATH_INVALID,
        "OUTPUT_ALREADY_EXISTS" => FormattingMutationOutcome.OUTPUT_ALREADY_EXISTS,
        "OUTPUT_TOO_LARGE" or "INPUT_TOO_LARGE" => FormattingMutationOutcome.OUTPUT_TOO_LARGE,
        _ => FormattingMutationOutcome.WRITE_FAILED,
    };

    private static FormattingMutationResult Fail(FormattingMutationOutcome outcome, string reason, FormattingMutationIntent intent, DocumentIdentity input) =>
        new(outcome, false, reason, intent.Property, intent.TargetId, intent.ExpectedBefore, null,
            intent.RuleId, intent.MutationIntentId, input, null, null);

    private static FormattingMutationResult Clean(FormattingMutationOutcome outcome, string reason, FormattingMutationIntent intent, DocumentIdentity input, string? temporary)
    {
        if (temporary is not null)
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { /* Best effort; the caller surfaces the original failure. */ }
        }
        return Fail(outcome, reason, intent, input);
    }
}