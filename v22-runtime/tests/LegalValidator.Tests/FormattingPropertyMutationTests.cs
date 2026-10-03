using System.Globalization;
using DocumentFormat.OpenXml.Packaging;
using System.Security.Cryptography;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Catalog;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Mutation;
using Nd30.LegalValidator.Profiles;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
using Xunit;

namespace Nd30.LegalValidator.Tests;

/// <summary>
/// Targeted regressions for the profile formatting mutation path:
///  1. the executor accepts only a trusted payload and re-derives every current
///     fact from the canonical catalog/profile and a fresh read of the source;
///  2. the complex-script size is never written, because the confirmed intent
///     binds only the Latin size;
///  3. body aggregation uses the detector's body paragraphs only.
/// </summary>
public sealed class FormattingPropertyMutationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "fmt-" + Guid.NewGuid().ToString("N"));
    public FormattingPropertyMutationTests() => Directory.CreateDirectory(_directory);
    public void Dispose() { try { Directory.Delete(_directory, true); } catch { /* best effort */ } }

    private const string FontRule = "ND30.PL1.I.II.6E.BODY_SIZE";
    private const string MarginRule = "ND30.PL1.I.GENERAL.MARGIN_RIGHT";

    private string P(string name) => Path.Combine(_directory, name);

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "release", "admin-nd30-verified-rc-v20.yaml"))) return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Verified release pack not found.");
    }

    private static (RuleCatalog Catalog, FormattingProfile Profile, FormattingPropertyMutationExecutor Executor) Engine()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        return (catalog, profile, new FormattingPropertyMutationExecutor(catalog, profile));
    }

    private static string StableId(string value) =>
        Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value))).ToLowerInvariant()[..24];

    // ---------- fixtures ----------

    private static W.Run SizedRun(string text, string halfPoints, string? complexHalfPoints = null)
    {
        var runProperties = new W.RunProperties(
            new W.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
            new W.FontSize { Val = halfPoints });

        // Appended only when it exists. Passing a null element into the params constructor is
        // what produced CS8604, and OpenXmlElement has no meaningful null instance; the SDK
        // inserts a known child at its schema-correct position, so szCs still follows sz.
        if (complexHalfPoints is not null)
            runProperties.AppendChild(new W.FontSizeComplexScript { Val = complexHalfPoints });

        return new W.Run(runProperties, new W.Text(text));
    }

    private static W.SectionProperties Section(string rightTwips = "850") =>
        new(new W.PageSize { Width = 11906U, Height = 16838U },
            new W.PageMargin { Top = 1134, Right = uint.Parse(rightTwips, CultureInfo.InvariantCulture), Bottom = 1134, Left = 1701U });

    /// <summary>Latin 14pt, complex-script 18pt on the body paragraph.</summary>
    private static string DifferingComplexSizeDocx(string path) =>
        Build(path, new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }, new W.Indentation { FirstLine = "709" }),
            SizedRun("Body paragraph", "28", "36")),
        new W.Paragraph(new W.Run(new W.Text("Heading"))),
        new W.Paragraph(SizedRun("Unrelated", "28", "36")));

    /// <summary>Every run declares the same Latin and complex-script size.</summary>
    private static string UniformSizeDocx(string path, string halfPoints = "28", string complex = "28") =>
        Build(path, new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }, new W.Indentation { FirstLine = "709" }),
            SizedRun("Body paragraph", halfPoints, complex)),
        new W.Paragraph(SizedRun("Unrelated", halfPoints, complex)));

    /// <summary>One run declares a direct size, the other inherits it.</summary>
    private static string MixedDirectSizeDocx(string path) =>
        Build(path, new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }),
            SizedRun("Sized run", "28", "28"),
            new W.Run(new W.RunProperties(new W.RunFonts { Ascii = "Times New Roman" }), new W.Text("Inherited run"))));

    /// <summary>Latin size declared, no complex-script size anywhere.</summary>
    private static string NoComplexSizeDocx(string path) =>
        Build(path, new W.Paragraph(
            new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Both }),
            SizedRun("Latin only", "28")));

    private static string Build(string path, params W.Paragraph[] paragraphs)
    {
        var body = new W.Body();
        foreach (var paragraph in paragraphs) body.Append(paragraph);
        body.Append(Section());
        using var document = WordprocessingDocument.Create(path, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);
        var main = document.AddMainDocumentPart();
        main.Document = new W.Document(body);
        main.Document.Save();
        return path;
    }

    // ---------- trusted payload helpers ----------

    private sealed record Prepared(FormattingPropertyMutationExecutor Executor, TrustedFormattingAuthorization Trusted, FormattingMutationIntent Intent);

    private static Prepared Prepare(string fixture, string property, string targetId, string? expectedBefore, string desiredAfter, string ruleId, FormattingTargetKind kind = FormattingTargetKind.Paragraph, string? profileDigestOverride = null, string? ruleDigestOverride = null)
    {
        var (catalog, profile, executor) = Engine();
        var liveRule = catalog.Rules.First(rule => rule.Id == ruleId);
        var identity = DocumentIdentityService.FromFile(fixture);
        var stateBinding = new DocumentStateBinding(identity, new RuleIdentity(liveRule.Id, liveRule.Source.SourceId, liveRule.Source.Locator));
        var profileTarget = profile.FindMutationTarget(property);
        var unit = profileTarget?.Unit ?? string.Empty;
        var proposalId = StableId("proposal|" + property + "|" + targetId + "|" + expectedBefore + "|" + desiredAfter);
        var profileDigest = profileDigestOverride ?? profile.Digest;
        var ruleDigest = ruleDigestOverride ?? executor.CurrentRuleContentDigest(liveRule.Id);
        var intentId = FormattingMutationIntent.ComputeId(
            proposalId, liveRule.Id, property, targetId, kind, unit, expectedBefore, desiredAfter, stateBinding, profileDigest, ruleDigest);
        var intent = new FormattingMutationIntent(intentId, proposalId, liveRule.Id, property, targetId, kind, unit,
            expectedBefore, desiredAfter, stateBinding, profileDigest, ruleDigest);
        var proposal = new RemediationProposal(proposalId, liveRule.Id, StableId("finding|" + proposalId), [StableId("evidence")],
            targetId, property, expectedBefore ?? "unset", desiredAfter, RemediationDecision.ELIGIBLE, true, "confirmed", stateBinding);
        var artifact = AuthorizationArtifact.Issue(proposal, "confirmed-gateway") with
        {
            Scope = FormattingAuthorizationBoundary.RequestScope,
            MutationIntentId = intentId,
        };
        return new Prepared(executor, executor.Package(intent, artifact, StableId("key"), "req-1"), intent);
    }

    // ---------- 1. authorized payload only, current state re-derived ----------

    [Fact]
    public void Executor_does_not_expose_a_bare_intent_entry_point()
    {
        var execute = typeof(FormattingPropertyMutationExecutor).GetMethods()
            .Single(method => method.Name == nameof(FormattingPropertyMutationExecutor.Execute));
        Assert.Equal(typeof(TrustedFormattingAuthorization), execute.GetParameters()[2].ParameterType);
        Assert.DoesNotContain(typeof(FormattingPropertyMutationExecutor).GetMethods(),
            method => method.Name == nameof(FormattingPropertyMutationExecutor.Execute)
                && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(FormattingMutationIntent)));
    }

    [Fact]
    public void Executor_requires_the_canonical_catalog_and_profile()
    {
        Assert.Throws<ArgumentNullException>(() => new FormattingPropertyMutationExecutor(null!, FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()))));
        Assert.Throws<ArgumentNullException>(() => new FormattingPropertyMutationExecutor(RuleCatalog.LoadVerifiedRelease(FindRoot()), null!));
    }

    [Fact]
    public void A_proposal_issued_under_a_different_profile_is_rejected_by_the_current_profile_digest()
    {
        var fixture = UniformSizeDocx(P("profile.docx"));
        var stale = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule, profileDigestOverride: new string('a', 64));

        var result = stale.Executor.Execute(fixture, P("profile-out.docx"), stale.Trusted);

        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.PROFILE_CHANGED, result.Outcome);
        Assert.False(File.Exists(P("profile-out.docx")));
    }

    /// <summary>
    /// Packaging cannot launder a stale proposal: the trusted payload carries no
    /// digest fields, so re-packaging a proposal that predates a profile change
    /// still leaves it bound to the old digest and therefore still rejected.
    /// </summary>
    [Fact]
    public void Repackaging_a_stale_proposal_cannot_refresh_its_profile_binding()
    {
        var fixture = UniformSizeDocx(P("repack.docx"));
        var stale = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule, profileDigestOverride: new string('a', 64));

        var repackaged = stale.Executor.Package(stale.Intent, stale.Trusted.Authorization, StableId("key"), "req-2");

        Assert.Equal(FormattingMutationOutcome.PROFILE_CHANGED,
            stale.Executor.Execute(fixture, P("repack-out.docx"), repackaged).Outcome);
        Assert.DoesNotContain(typeof(TrustedFormattingAuthorization).GetProperties(),
            property => property.Name.EndsWith("Digest", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("", "run.font_size_pt")]
    [InlineData("not-a-digest", "run.font_size_pt")]
    [InlineData("2D582FF2E00630CC141B0A85F3AAC9A2F32E6BDFEFD166110E726D4393ACEDC6", "run.font_size_pt")]
    public void An_absent_or_malformed_profile_binding_is_rejected(string digest, string property)
    {
        var fixture = UniformSizeDocx(P("bind-" + digest.Length + ".docx"));
        var prepared = Prepare(fixture, property, "p1", "14", "13", FontRule, profileDigestOverride: digest);

        var result = prepared.Executor.Execute(fixture, P("bind-out.docx"), prepared.Trusted);

        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.MISSING_BINDING, result.Outcome);
        Assert.False(File.Exists(P("bind-out.docx")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc123")]
    public void An_absent_or_malformed_rule_content_binding_is_rejected(string digest)
    {
        var fixture = UniformSizeDocx(P("rbind-" + digest.Length + ".docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule, ruleDigestOverride: digest);

        var result = prepared.Executor.Execute(fixture, P("rbind-out.docx"), prepared.Trusted);

        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.MISSING_BINDING, result.Outcome);
    }

    /// <summary>
    /// The intent identity is derived from its content. Tampering a bound field
    /// while keeping the identity and the matching artifact must be rejected,
    /// otherwise a desired value inside the writer's storage range but outside the
    /// rule could be applied.
    /// </summary>
    [Theory]
    [InlineData("desired")]
    [InlineData("target")]
    [InlineData("before")]
    [InlineData("unit")]
    public void Tampering_a_bound_intent_field_under_the_same_identity_is_rejected(string field)
    {
        var fixture = UniformSizeDocx(P("tamper-" + field + ".docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        var intent = prepared.Intent;

        FormattingMutationIntent tampered = field switch
        {
            // A size the writer would happily store but the rule forbids.
            "desired" => intent with { DesiredAfter = "100" },
            "target" => intent with { TargetId = "p2" },
            "before" => intent with { ExpectedBefore = "18" },
            _ => intent with { Unit = "cm" },
        };

        // Identity and artifact are unchanged: only the content moved.
        var result = prepared.Executor.Execute(fixture, P("tamper-out.docx"), prepared.Trusted with { Intent = tampered });

        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.INTENT_CONTENT_MISMATCH, result.Outcome);
        Assert.False(File.Exists(P("tamper-out.docx")));
    }

    [Fact]
    public void Intent_identity_is_deterministic_and_content_sensitive()
    {
        var (_, profile, executor) = Engine();
        var (_, _, baseIntent) = Prepare(UniformSizeDocx(P("ident.docx")), "run.font_size_pt", "p1", "14", "13", FontRule);
        var digest = executor.CurrentRuleContentDigest(FontRule);

        var again = FormattingMutationIntent.ComputeId(baseIntent.ProposalId, baseIntent.RuleId, baseIntent.Property,
            baseIntent.TargetId, baseIntent.Kind, baseIntent.Unit, baseIntent.ExpectedBefore, baseIntent.DesiredAfter,
            baseIntent.StateBinding, profile.Digest, digest);
        Assert.Equal(baseIntent.MutationIntentId, again);

        var changed = FormattingMutationIntent.ComputeId(baseIntent.ProposalId, baseIntent.RuleId, baseIntent.Property,
            baseIntent.TargetId, baseIntent.Kind, baseIntent.Unit, baseIntent.ExpectedBefore, "13.5",
            baseIntent.StateBinding, profile.Digest, digest);
        Assert.NotEqual(baseIntent.MutationIntentId, changed);
    }

    [Fact]
    public void A_rule_edited_under_the_same_id_is_rejected_by_the_current_rule_content_digest()
    {
        var fixture = UniformSizeDocx(P("rule.docx"));
        var (_, profile, _) = Engine();
        var currentRuleDigest = FormattingProfile.ComputeRuleContentDigest(RuleCatalog.LoadVerifiedRelease(FindRoot()), FontRule);
        Assert.False(string.IsNullOrEmpty(currentRuleDigest));
        var stale = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule, ruleDigestOverride: new string('b', 64));

        var result = stale.Executor.Execute(fixture, P("rule-out.docx"), stale.Trusted);

        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.STALE_RULE_CONTENT, result.Outcome);
        Assert.False(File.Exists(P("rule-out.docx")));
        // The profile digest was unchanged, so this rejection is specifically
        // about rule content.
        Assert.Equal(profile.Digest, stale.Intent.ProfileDigest);
    }

    [Fact]
    public void A_property_paired_with_a_rule_it_is_not_defined_for_is_rejected()
    {
        var fixture = UniformSizeDocx(P("tuple.docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", MarginRule);
        var result = prepared.Executor.Execute(fixture, P("tuple-out.docx"), prepared.Trusted);
        Assert.False(result.Success);
        Assert.True(result.Outcome is FormattingMutationOutcome.PROFILE_TUPLE_MISMATCH or FormattingMutationOutcome.AUTHORIZATION_REJECTED);
        Assert.False(File.Exists(P("tuple-out.docx")));
    }

    [Fact]
    public void A_missing_or_wrongly_scoped_authorization_is_rejected()
    {
        var fixture = UniformSizeDocx(P("auth.docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        var withoutArtifact = prepared.Trusted with { Authorization = null! };
        Assert.Equal(FormattingMutationOutcome.AUTHORIZATION_REJECTED,
            prepared.Executor.Execute(fixture, P("auth-a.docx"), withoutArtifact).Outcome);

        var wrongScope = prepared.Trusted with { Authorization = prepared.Trusted.Authorization with { Scope = "other-scope" } };
        Assert.Equal(FormattingMutationOutcome.AUTHORIZATION_REJECTED,
            prepared.Executor.Execute(fixture, P("auth-b.docx"), wrongScope).Outcome);

        var wrongIntent = prepared.Trusted with { Authorization = prepared.Trusted.Authorization with { MutationIntentId = "not-the-intent" } };
        Assert.Equal(FormattingMutationOutcome.AUTHORIZATION_REJECTED,
            prepared.Executor.Execute(fixture, P("auth-c.docx"), wrongIntent).Outcome);
    }

    [Fact]
    public void A_stale_document_state_is_rejected_before_any_write()
    {
        var fixture = UniformSizeDocx(P("stale.docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        File.AppendAllText(fixture, "x");

        var result = prepared.Executor.Execute(fixture, P("stale-out.docx"), prepared.Trusted);

        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.STALE_DOCUMENT, result.Outcome);
        Assert.False(File.Exists(P("stale-out.docx")));
    }

    [Fact]
    public void An_authorized_font_size_change_writes_only_the_confirmed_property()
    {
        var fixture = UniformSizeDocx(P("font.docx"));
        var before = File.ReadAllBytes(fixture);
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        var output = P("font-out.docx");

        var result = prepared.Executor.Execute(fixture, output, prepared.Trusted);

        Assert.True(result.Success, result.Reason);
        Assert.Equal("13", result.After);
        Assert.True(result.Verification!.SourceUnchanged);
        Assert.Equal(before, File.ReadAllBytes(fixture));

        using var reopened = WordprocessingDocument.Open(output, false);
        var paragraphs = reopened.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ToList();
        Assert.Equal("26", paragraphs[0].Descendants<W.FontSize>().Single().Val!.Value);
        Assert.Equal("28", paragraphs[1].Descendants<W.FontSize>().Single().Val!.Value);
        // Alignment and indent were outside the intent and must be untouched.
        Assert.Equal(W.JustificationValues.Both, paragraphs[0].ParagraphProperties!.GetFirstChild<W.Justification>()!.Val!.Value);
        Assert.Equal("709", paragraphs[0].ParagraphProperties!.GetFirstChild<W.Indentation>()!.FirstLine!.Value);
    }

    // ---------- 2. complex-script size is out of scope ----------

    [Fact]
    public void A_differing_complex_script_size_is_preserved_in_the_output()
    {
        // Latin 14pt (sz=28) with complex-script 18pt (szCs=36). The owner confirmed
        // only 14 -> 13pt for the Latin size.
        var fixture = DifferingComplexSizeDocx(P("cs.docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        var output = P("cs-out.docx");

        var result = prepared.Executor.Execute(fixture, output, prepared.Trusted);

        Assert.True(result.Success, result.Reason);
        using var reopened = WordprocessingDocument.Open(output, false);
        var body = reopened.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().First();
        Assert.Equal("26", body.Descendants<W.FontSize>().Single().Val!.Value);
        // The complex-script size must survive untouched at 18pt.
        Assert.Equal("36", body.Descendants<W.FontSizeComplexScript>().Single().Val!.Value);
    }

    [Fact]
    public void An_absent_complex_script_size_is_not_created_in_the_output()
    {
        var fixture = NoComplexSizeDocx(P("nocs.docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        var output = P("nocs-out.docx");

        var result = prepared.Executor.Execute(fixture, output, prepared.Trusted);

        Assert.True(result.Success, result.Reason);
        using var reopened = WordprocessingDocument.Open(output, false);
        var body = reopened.MainDocumentPart!.Document.Body!;
        Assert.Equal("26", body.Descendants<W.FontSize>().Single().Val!.Value);
        // Not written, because it was never confirmed.
        Assert.Empty(body.Descendants<W.FontSizeComplexScript>());
    }

    [Fact]
    public void Font_size_change_is_refused_when_a_run_has_no_direct_size()
    {
        var fixture = MixedDirectSizeDocx(P("mixed.docx"));
        var prepared = Prepare(fixture, "run.font_size_pt", "p1", "14", "13", FontRule);
        var output = P("mixed-out.docx");

        var result = prepared.Executor.Execute(fixture, output, prepared.Trusted);

        // A partially direct paragraph has no single direct size to confirm against.
        Assert.False(result.Success);
        Assert.Equal(FormattingMutationOutcome.PROVENANCE_NOT_DIRECT, result.Outcome);
        Assert.False(File.Exists(output));
        using var source = WordprocessingDocument.Open(fixture, false);
        // The inherited run must not have been given a direct size.
        Assert.Single(source.MainDocumentPart!.Document.Body!.Descendants<W.FontSize>());
    }

    // ---------- 3. body aggregation scope ----------

    [Fact]
    public void Body_rollup_ignores_structurally_classified_paragraphs()
    {
        var fixture = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", "02-named-decision.docx"));
        var parser = new Nd30.DocumentEngine.Parsing.DocxParser();
        var detector = new Nd30.SemanticDetector.Detection.AdministrativeSemanticDetector();
        var (_, profile, _) = Engine();
        var documentType = profile.FindDocumentType("quyet_dinh")!;

        var parsed = parser.Parse(fixture);
        Assert.NotNull(parsed.Document);
        var semantic = detector.Detect(parsed.Document!);
        var context = Nd30.LegalValidator.Adapters.FormattingObservationAdapter.BuildContext(
            parsed.Document!, semantic, documentType, profile, new DateOnly(2026, 9, 19));

        var structuralRoles = new[]
        {
            Nd30.SemanticDetector.Model.SemanticRole.DocumentTypeHeading,
            Nd30.SemanticDetector.Model.SemanticRole.SubjectNamedDocument,
            Nd30.SemanticDetector.Model.SemanticRole.IssuingAuthority,
            Nd30.SemanticDetector.Model.SemanticRole.IssuingAuthorityParent,
            Nd30.SemanticDetector.Model.SemanticRole.LegalBasisBlock,
        };
        var structuralIds = semantic.Components.Where(component => structuralRoles.Contains(component.Role))
            .Select(component => component.Evidence.ParagraphId).ToHashSet(StringComparer.Ordinal);
        var bodyIds = semantic.Components
            .Where(component => component.Role == Nd30.SemanticDetector.Model.SemanticRole.Body)
            .Select(component => component.Evidence.ParagraphId).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(structuralIds);
        Assert.NotEmpty(bodyIds);
        Assert.Empty(bodyIds.Intersect(structuralIds, StringComparer.Ordinal));

        // Give the type heading a deliberately different size and alignment; the
        // body role values must not move, proving headings do not feed the body roll-up.
        var headingId = semantic.Components.First(component => component.Role == Nd30.SemanticDetector.Model.SemanticRole.DocumentTypeHeading).Evidence.ParagraphId;
        var index = int.Parse(headingId.AsSpan(1), CultureInfo.InvariantCulture);
        var variant = P("heading-variant.docx");
        File.Copy(fixture, variant, overwrite: true);
        using (var document = WordprocessingDocument.Open(variant, true))
        {
            var heading = document.MainDocumentPart!.Document.Body!.Elements<W.Paragraph>().ElementAt(index - 1);
            heading.ParagraphProperties ??= new W.ParagraphProperties();
            heading.ParagraphProperties.RemoveAllChildren<W.Justification>();
            heading.ParagraphProperties.Append(new W.Justification { Val = W.JustificationValues.Center });
            foreach (var run in heading.Descendants<W.Run>())
            {
                run.RunProperties ??= new W.RunProperties();
                run.RunProperties.RemoveAllChildren<W.FontSize>();
                run.RunProperties.Append(new W.FontSize { Val = "48" });
            }
            document.MainDocumentPart.Document.Save();
        }
        var variantParsed = parser.Parse(variant);
        Assert.NotNull(variantParsed.Document);
        var variantContext = Nd30.LegalValidator.Adapters.FormattingObservationAdapter.BuildContext(
            variantParsed.Document!, detector.Detect(variantParsed.Document!), documentType, profile, new DateOnly(2026, 9, 19));

        foreach (var key in new[] { "semantic_component.body.font_size_pt", "semantic_component.body.alignment", "semantic_component.body.italic" })
        {
            Assert.Equal(
                context.ObservedValues.TryGetValue(key, out var before) ? Convert.ToString(before, CultureInfo.InvariantCulture) : null,
                variantContext.ObservedValues.TryGetValue(key, out var after) ? Convert.ToString(after, CultureInfo.InvariantCulture) : null);
            Assert.Equal(context.UncertainTargets.Contains(key), variantContext.UncertainTargets.Contains(key));
        }

        // Per-paragraph target observations still cover the whole document so an
        // apply can bind an exact expected-before value to any paragraph.
        foreach (var structuralId in structuralIds)
            Assert.Contains(context.ObservedValues.Keys, key => key.StartsWith($"paragraph.{structuralId}.", StringComparison.Ordinal));
    }
}