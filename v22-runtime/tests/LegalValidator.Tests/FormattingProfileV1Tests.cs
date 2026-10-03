using System.Globalization;
using System.Security.Cryptography;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Catalog;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Mutation;
using Nd30.LegalValidator.Processing;
using Nd30.LegalValidator.Profiles;
using Nd30.SemanticDetector.Detection;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Parsing;
using Xunit;

namespace Nd30.LegalValidator.Tests;

/// <summary>
/// Proves the company formatting profile v1 is actually wired into the serving
/// flow: a real DOCX is inspected, the real verified release pack is loaded, the
/// real rule engine runs over the profile-scoped subset, and the result is bound
/// to a profile digest that a later apply must still match.
/// </summary>
public sealed class FormattingProfileV1Tests
{
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

        /// <summary>Reads word/header1.xml straight out of the DOCX package.</summary>
    private static string ReadHeaderPartXml(string fileName)
    {
        var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", fileName));
        using var archive = System.IO.Compression.ZipFile.OpenRead(path);
        var entry = archive.GetEntry("word/header1.xml");
        Assert.NotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        return reader.ReadToEnd();
    }

private static string SemanticFixture(string fileName) =>
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", fileName));

    private static DocumentProcessorService Processor() => new(RuleCatalog.LoadVerifiedRelease(FindRoot()), new DateOnly(2026, 9, 19));

    [Fact]
    public void Profile_covers_exactly_the_five_priority_document_types()
    {
        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        Assert.Equal(FormattingProfileV1.Id, profile.Id);
        Assert.Equal(FormattingProfileV1.Version, profile.Version);
        Assert.Equal(FormattingProfileV1.RulePackId, profile.RulePackId);
        Assert.Equal(64, profile.Digest.Length);

        Assert.Equal(
            new[] { "bao_cao", "cong_van", "ke_hoach", "quyet_dinh", "to_trinh" },
            profile.DocumentTypes.Select(type => type.TypeKey).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        // The official letter carries no document-type heading, and therefore no
        // type-abbreviation rule.
        Assert.Null(profile.FindDocumentType(FormattingProfileV1.CongVanKey)!.AbbreviationRuleId);
        Assert.False(profile.FindDocumentType(FormattingProfileV1.CongVanKey)!.HasTypeHeading);
        foreach (var type in profile.DocumentTypes.Where(x => x.TypeKey != FormattingProfileV1.CongVanKey))
            Assert.StartsWith("ND30.PL3.I.ABBR.", type.AbbreviationRuleId, StringComparison.Ordinal);
    }

    /// <summary>
    /// The profile digest must bind to the actual content of the rules it can
    /// use. A rule whose ID stays the same but whose constraint or applicability
    /// changed must invalidate a proposal issued under the old content.
    /// </summary>
    [Fact]
    public void Profile_digest_binds_to_rule_content_not_only_to_rule_ids()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var baseRuleIds = profile.BaseRuleIds.ToArray();

        // Same IDs, same profile values: the digest is stable across loads.
        Assert.Equal(profile.Digest, FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot())).Digest);

        // Same IDs but one rule's constraint changed -> the binding must change.
        var mutated = MutateRule(catalog, "ND30.PL1.I.II.6E.BODY_SIZE", rule => rule with
        {
            Constraint = new Dictionary<string, object?> { ["type"] = "equals", ["value"] = 13 },
        });
        var changedConstraint = new FormattingProfile(
            FormattingProfileV1.Id, FormattingProfileV1.Version, FormattingProfileV1.RulePackId,
            profile.DocumentTypes, profile.MutationTargets, profile.GrantedCapabilities, baseRuleIds,
            FormattingProfile.ComputeRuleContentDigests(mutated, baseRuleIds));
        Assert.NotEqual(profile.RuleContentDigest, changedConstraint.RuleContentDigest);
        Assert.NotEqual(profile.Digest, changedConstraint.Digest);

        // Same IDs but a rule's applicability changed -> the binding must change.
        var mutatedApplicability = MutateRule(catalog, "ND30.PL1.I.II.6E.BODY_JUSTIFIED", rule => rule with
        {
            Applicability = new Dictionary<string, object?>
            {
                ["all"] = new List<object?> { new Dictionary<string, object?> { ["field"] = "document.type", ["operator"] = "equals", ["value"] = "report" } },
            },
        });
        var changedApplicability = new FormattingProfile(
            FormattingProfileV1.Id, FormattingProfileV1.Version, FormattingProfileV1.RulePackId,
            profile.DocumentTypes, profile.MutationTargets, profile.GrantedCapabilities, baseRuleIds,
            FormattingProfile.ComputeRuleContentDigests(mutatedApplicability, baseRuleIds));
        Assert.NotEqual(profile.Digest, changedApplicability.Digest);

        // A profile value change also rebinds.
        var changedValues = new FormattingProfile(
            FormattingProfileV1.Id, FormattingProfileV1.Version, FormattingProfileV1.RulePackId,
            profile.DocumentTypes,
            profile.MutationTargets.Append(new ProfileMutationTarget(
                "paragraph.space_before_pt", "semantic_component.body.paragraph_spacing_pt", "pt",
                "Kho\u1ea3ng c\u00e1ch tr\u01b0\u1edbc \u0111o\u1ea1n", "paragraph", "length_pt", "ND30.PL1.I.II.6E.PARAGRAPH_GAP_MIN")).ToArray(),
            profile.GrantedCapabilities, baseRuleIds, profile.RuleContentDigest.Split(',').ToArray());
        Assert.NotEqual(profile.Digest, changedValues.Digest);
    }

    /// <summary>Apply an apply function to one release-pack rule, returning a mutated catalog view.</summary>
    private static RuleCatalog MutateRule(RuleCatalog catalog, string ruleId, Func<RuleDefinition, RuleDefinition> mutate)
    {
        var rules = catalog.Rules.Select(rule => string.Equals(rule.Id, ruleId, StringComparison.Ordinal) ? mutate(rule) : rule).ToList();
        return RuleCatalog.WithRules(catalog, rules);
    }

    /// <summary>
    /// Validation scope must not be narrowed by whether the formatter can repair
    /// a finding. Selecting only SAFE/GUARDED rules would hide review-only rules
    /// and would make the formatter look compliant by shrinking its own coverage.
    /// </summary>
    [Fact]
    public void Rule_selection_is_independent_of_mutation_eligibility()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var subset = profile.ResolveRuleSubset(catalog, "bao_cao").ToHashSet(StringComparer.Ordinal);

        var excludedPolicies = catalog.Rules
            .Where(rule => FormattingProfileV1.ScopedObjectTypes.Contains(rule.Target.ObjectType, StringComparer.OrdinalIgnoreCase)
                        && FormattingProfileV1.ObservableProperties.Contains(rule.Target.Property ?? string.Empty)
                        && rule.RequiresCapabilities.All(capability => FormattingProfileV1.GrantedCapabilities.Contains(capability)))
            .Where(rule => !subset.Contains(rule.Id))
            .Select(rule => rule.AutofixPolicy)
            .Distinct()
            .ToArray();
        Assert.DoesNotContain("SAFE", excludedPolicies);
        Assert.DoesNotContain("GUARDED", excludedPolicies);

        // Direct proof the gate is gone: flipping an in-scope rule's autofix policy
        // to PROHIBITED must not remove it from the validation subset.
        var baseRuleIds = profile.BaseRuleIds.ToArray();
        var target = baseRuleIds[0];
        var downgraded = RuleCatalog.WithRules(catalog, catalog.Rules
            .Select(rule => string.Equals(rule.Id, target, StringComparison.Ordinal)
                ? rule with { AutofixPolicy = "PROHIBITED" }
                : rule)
            .ToList());
        Assert.Contains(target, FormattingProfileV1.ResolveBaseRuleIds(downgraded));
    }

    /// <summary>
    /// The release pack ships all 30 <c>ND30.PL3.I.ABBR.*</c> rules with a null
    /// applicability, so an unscoped run would fail every one of them on every
    /// document. The profile must scope the abbreviation rule to the detected type.
    /// </summary>
    [Fact]
    public void Rule_subset_scopes_the_document_type_abbreviation_rule_to_the_detected_type()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);

        var unscoped = catalog.Rules
            .Where(rule => rule.Target.ObjectType == "document_metadata" && rule.Target.Property == "document_type_abbreviation")
            .ToArray();
        Assert.Equal(30, unscoped.Length);
        Assert.All(unscoped, rule => Assert.Null(rule.Applicability));

        foreach (var documentType in profile.DocumentTypes)
        {
            var subset = profile.ResolveRuleSubset(catalog, documentType.TypeKey);
            var abbreviationRules = subset
                .Where(id => id.StartsWith("ND30.PL3.I.ABBR.", StringComparison.Ordinal))
                .ToArray();
            if (documentType.AbbreviationRuleId is null)
                Assert.Empty(abbreviationRules);
            else
                Assert.Equal(new[] { documentType.AbbreviationRuleId }, abbreviationRules);
        }
    }

    /// <summary>
    /// Management, records, workflow, electronic-signature and Party-document
    /// rules can never be evidenced from a DOCX, so they must not enter a
    /// formatter run at all.
    /// </summary>
    [Fact]
    public void Rule_subset_excludes_evidence_that_a_docx_cannot_carry()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var subset = profile.ResolveRuleSubset(catalog, "bao_cao").ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(subset);
        Assert.True(subset.Count < catalog.Rules.Count / 5, $"subset {subset.Count} of {catalog.Rules.Count}");

        foreach (var rule in catalog.Rules.Where(rule => subset.Contains(rule.Id)))
        {
            Assert.Contains(rule.Target.ObjectType, FormattingProfileV1.ScopedObjectTypes);
            Assert.True(
                FormattingProfileV1.ObservableProperties.Contains(rule.Target.Property!)
                || FormattingProfileV1.PendingObservationProperties.Contains(rule.Target.Property!),
                $"{rule.Id} targets {rule.Target.Property} which the profile neither observes nor declares pending.");
            Assert.DoesNotContain(rule.RequiresCapabilities, capability => !FormattingProfileV1.GrantedCapabilities.Contains(capability));
            Assert.Contains(rule.AutofixPolicy, new[] { "SAFE", "GUARDED" });
            Assert.Equal("administrative", rule.Regime);
        }

        // A property the profile has not declared must never reach a formatter run.
        Assert.Empty(catalog.Rules
            .Where(rule => subset.Contains(rule.Id))
            .Where(rule => !FormattingProfileV1.ObservableProperties.Contains(rule.Target.Property ?? string.Empty)
                        && !FormattingProfileV1.PendingObservationProperties.Contains(rule.Target.Property ?? string.Empty))
            .Select(rule => rule.Id));

        var excludedDomains = catalog.Rules
            .Where(rule => !subset.Contains(rule.Id))
            .Select(rule => rule.Domain)
            .Distinct()
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        Assert.Contains("records_management", excludedDomains);
        Assert.Contains("document_workflow", excludedDomains);
        Assert.Contains("electronic_signature", excludedDomains);
    }

    [Fact]
    public async Task Official_letter_inspection_runs_the_real_validator_and_reports_scoped_coverage()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("01-official-letter.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);

        Assert.NotNull(inspection.Binding);
        Assert.Equal(FormattingProfileV1.CongVanKey, inspection.Binding!.DocumentTypeKey);
        Assert.Equal("C\u00f4ng v\u0103n", inspection.Binding.DocumentTypeLabelVi);
        Assert.Equal(64, inspection.Binding.Digest.Length);
        Assert.Equal(FormattingProfileV1.RulePackId, inspection.Binding.RulePackId);

        // The real engine ran: the subset is populated and the report carries
        // scoped counts rather than a whole-document verdict.
        Assert.True(inspection.RuleSubsetSize > 0);
        Assert.True(inspection.ApplicableRules > 0);
        Assert.True(inspection.EvaluatedRules > 0);
        Assert.False(inspection.FullComplianceClaimAllowed);
        Assert.Contains("h\u1ed3 s\u01a1", inspection.ScopeStatement!, StringComparison.Ordinal);
        Assert.Contains(FormattingProfileV1.Id, inspection.ScopeStatement!, StringComparison.Ordinal);

        // Every reported finding is a real rule from the release pack, not a
        // management or records rule the DOCX cannot evidence.
        Assert.NotNull(inspection.Findings);
        foreach (var finding in inspection.Findings!)
        {
            Assert.StartsWith("ND30.", finding.RuleId, StringComparison.Ordinal);
            Assert.Contains(finding.Status, new[] { "FAIL", "NEEDS_REVIEW" });
            Assert.False(string.IsNullOrWhiteSpace(finding.PropertyLabelVi));
        }
    }

    [Fact]
    public async Task Named_decision_inspection_reports_body_alignment_and_font_findings_from_real_observations()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);

        Assert.NotNull(inspection.Binding);
        Assert.Equal("quyet_dinh", inspection.Binding!.DocumentTypeKey);
        Assert.Equal("Quy\u1ebft \u0111\u1ecbnh", inspection.Binding.DocumentTypeLabelVi);
        Assert.True(inspection.RuleSubsetSize > 0);

        // The subset must actually contain the body/paragraph rules the profile
        // declares, otherwise the profile would claim a property it never checks.
        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        var subset = profile.ResolveRuleSubset(RuleCatalog.LoadVerifiedRelease(FindRoot()), "quyet_dinh");
        Assert.Contains("ND30.PL1.I.II.6E.BODY_JUSTIFIED", subset);
        Assert.Contains("ND30.PL1.I.II.6E.BODY_SIZE", subset);
        Assert.Contains("ND30.PL1.I.II.6E.FIRST_LINE_INDENT", subset);
        Assert.Contains("ND30.PL1.I.II.6E.PARAGRAPH_GAP_MIN", subset);
        Assert.Contains("ND30.PL1.I.GENERAL.PAGE_SIZE_A4", subset);
        Assert.Contains("ND30.PL3.I.ABBR.QUYET_DINH", subset);
    }

    [Fact]
    public async Task Inspection_exposes_mutable_targets_only_where_direct_formatting_is_present()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);

        Assert.NotNull(inspection.MutableTargets);
        Assert.NotEmpty(inspection.MutableTargets!);
        Assert.All(inspection.MutableTargets!, target =>
        {
            Assert.False(string.IsNullOrWhiteSpace(target.Property));
            Assert.False(string.IsNullOrWhiteSpace(target.RuleId));
            Assert.StartsWith("ND30.", target.RuleId, StringComparison.Ordinal);
            // The webapp rejects a targetId shorter than one character, so every advertised
            // target must carry a usable id, not just the paragraph-scoped ones.
            Assert.False(string.IsNullOrWhiteSpace(target.TargetId));
            // Section and document scope targets are not paragraph-scoped.
            if (target.DirectOnly) Assert.Matches("^p[1-9][0-9]*$", target.TargetId);
        });

        // A paragraph-scoped target must only be offered where a direct value was
        // observed, because the executor refuses to create absent direct formatting.
        foreach (var target in inspection.MutableTargets.Where(t => t.DirectOnly))
            Assert.NotNull(target.CurrentValue);
    }

    [Fact]
    public async Task Inspection_of_an_unsupported_package_is_audit_only_and_still_reports_scope()
    {
        var bytes = await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/docx", "10-tracked-changes.docx")));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);

        Assert.False(inspection.SafeToMutate);
        Assert.Equal("AUDIT_ONLY", inspection.PackagePolicy);
        // No mutation target may be offered for a package that must not be edited.
        Assert.True(inspection.MutableTargets is null or { Count: 0 });
        // Findings remain visible: the document can still be reviewed.
        Assert.NotNull(inspection.Findings);
    }

    [Fact]
    public async Task Apply_refuses_a_proposal_issued_under_a_different_profile()
    {
        var bytes = await File.ReadAllBytesAsync(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/docx", "23-direct-alignment.docx")));
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var processor = Processor();

        var rejection = await Assert.ThrowsAsync<DocumentProcessorException>(() => processor.ApplyAlignmentAsync(
            new MemoryStream(bytes, writable: false), digest, "p1", "LEFT", "CENTER", CancellationToken.None,
            expectedProfileDigest: new string('a', 64)));
        Assert.Equal("PROFILE_CHANGED", rejection.Code);

        var rejectionById = await Assert.ThrowsAsync<DocumentProcessorException>(() => processor.ApplyAlignmentAsync(
            new MemoryStream(bytes, writable: false), digest, "p1", "LEFT", "CENTER", CancellationToken.None,
            expectedProfileId: "SOME-OTHER-PROFILE"));
        Assert.Equal("PROFILE_CHANGED", rejectionById.Code);

        // The matching profile still applies and records its own binding.
        var applied = await processor.ApplyAlignmentAsync(
            new MemoryStream(bytes, writable: false), digest, "p1", "LEFT", "CENTER", CancellationToken.None,
            expectedProfileDigest: processor.CurrentProfile!.Digest,
            expectedProfileId: processor.CurrentProfile.Id,
            expectedRuleId: "ND30.PL1.I.II.6E.BODY_JUSTIFIED");
        Assert.True(applied.Reopened);
        Assert.Equal(FormattingProfileV1.Id, applied.ProfileId);
        Assert.Equal(FormattingProfileV1.Version, applied.ProfileVersion);
        Assert.Equal(processor.CurrentProfile.Digest, applied.ProfileDigest);
        Assert.Equal("ND30.PL1.I.II.6E.BODY_JUSTIFIED", applied.RuleId);
    }

    /// <summary>
    /// Line spacing must be observed in exactly one unit, chosen by the DOCX line
    /// rule. A multiple-of-lines rule (ND30 requires 1.0-1.5 lines) can never be
    /// compared with an exact/at-least point value.
    /// </summary>
    [Fact]
    public void Line_spacing_is_observed_in_lines_only_for_a_multiple_of_lines_rule()
    {
        var parsed = new DocxParser().Parse(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/docx", "20-line-spacing.docx")));
        Assert.NotNull(parsed.Document);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var context = Nd30.LegalValidator.Adapters.FormattingObservationAdapter.BuildContext(
            parsed.Document!, semantic, profile.FindDocumentType("quyet_dinh")!, profile, new DateOnly(2026, 9, 19));

        // The fixture mixes auto / exact / atLeast forms across its paragraphs.
        var lineKeys = context.ObservedValues.Keys.Where(key => key.EndsWith(".line_spacing_lines", StringComparison.Ordinal)).ToArray();
        var pointKeys = context.ObservedValues.Keys.Where(key => key.EndsWith(".line_spacing_pt", StringComparison.Ordinal)).ToArray();
        Assert.NotEmpty(lineKeys);
        Assert.NotEmpty(pointKeys);

        // No key may ever carry both units.
Assert.DoesNotContain(context.ObservedValues.Keys, key =>
            key.EndsWith(".line_spacing", StringComparison.Ordinal));

        // Every observed line value is a plausible multiple, never a point value.
        foreach (var key in lineKeys)
            Assert.InRange(Convert.ToDouble(context.ObservedValues[key], CultureInfo.InvariantCulture), 0.1, 10d);
        // Every observed point value is a plausible point measurement.
        foreach (var key in pointKeys)
            Assert.InRange(Convert.ToDouble(context.ObservedValues[key], CultureInfo.InvariantCulture), 1d, 200d);

        // The profile must not declare a point-unit target for the line-spacing rule.
        var lineTarget = Assert.Single(profile.MutationTargets, target => target.RuleId == "ND30.PL1.I.II.6E.BODY_LINE_SPACING_RANGE");
        Assert.Equal("line_spacing_lines", lineTarget.ConstraintKind);
        Assert.Equal("d\u00f2ng", lineTarget.Unit);
    }

    /// <summary>
    /// Business output must be real Vietnamese, not a literal escape sequence and
    /// not mojibake. Asserted on the runtime string values the client receives.
    /// </summary>
    [Fact]
    public async Task Runtime_business_labels_are_real_vietnamese_text()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var inspection = await Processor().InspectAsync(
            new MemoryStream(await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx")), writable: false),
            CancellationToken.None);

        // Expected text written as single C# escapes so the assertion itself cannot
        // be corrupted by a source-encoding round trip.
        const string expectedTypeLabel = "Quy\u1ebft \u0111\u1ecbnh";
        Assert.Equal(expectedTypeLabel, inspection.Binding!.DocumentTypeLabelVi);
        Assert.Equal(expectedTypeLabel, profile.FindDocumentType("quyet_dinh")!.LabelVi);

        foreach (var label in profile.DocumentTypes.Select(type => type.LabelVi)
            .Concat(profile.MutationTargets.Select(target => target.LabelVi))
            .Concat(new[] { inspection.ScopeStatement }))
        {
            Assert.False(string.IsNullOrWhiteSpace(label));
            Assert.DoesNotContain("\\u", label, StringComparison.Ordinal);
            Assert.DoesNotContain("\u00c3", label, StringComparison.Ordinal);
            Assert.DoesNotContain("\u00d0", label, StringComparison.Ordinal);
            Assert.DoesNotContain("\ufffd", label, StringComparison.Ordinal);
        }

        // A readable Vietnamese sentence, not an escaped fragment.
        Assert.Contains("Ki\u1ec3m tra theo h\u1ed3 s\u01a1", inspection.ScopeStatement!, StringComparison.Ordinal);
        Assert.Contains("quy t\u1eafc", inspection.ScopeStatement!, StringComparison.Ordinal);
        Assert.Equal("d\u00f2ng", profile.FindMutationTarget("paragraph.line_spacing_lines")!.Unit);
    }

    /// <summary>
    /// An owner-confirmed administrative type is honoured, but only after it is
    /// proved to be a type this profile version supports, and only when the rule
    /// and target are still inside that type's live scope.
    /// </summary>
    [Fact]
    public async Task Confirmed_type_is_evaluated_and_applied_in_its_own_scope()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var detected = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);
        Assert.Equal("quyet_dinh", detected.Binding!.DocumentTypeKey);

        // A confirmed type rebuilds the scoped evaluation instead of relabelling
        // the detected one.
        var confirmed = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), "bao_cao", CancellationToken.None);
        Assert.Equal("bao_cao", confirmed.Binding!.DocumentTypeKey);
        Assert.Equal("quyet_dinh", confirmed.Binding.DetectedDocumentTypeKey);
        Assert.True(confirmed.Binding.OwnerConfirmed);
        Assert.NotNull(confirmed.SupportedDocumentTypes);
        Assert.Contains(confirmed.SupportedDocumentTypes!, t => t.TypeKey == "cong_van");

        // Only targets whose rule is in the confirmed type's scope may be offered.
        var scopedRuleIds = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()))
            .ResolveRuleSubset(RuleCatalog.LoadVerifiedRelease(FindRoot()), "bao_cao")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.All(confirmed.MutableTargets!, t => Assert.Contains(t.RuleId, scopedRuleIds));

        var target = confirmed.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var request = new FormattingApplyRequest(
            confirmed.SourceSha256, "bao_cao", "section.margin_right_mm", target.TargetId,
            target.CurrentValue!, "17", confirmed.Binding.Id, confirmed.Binding.Digest, target.RuleId);

        var applied = await Processor().ApplyFormattingAsync(new MemoryStream(bytes, writable: false), request, CancellationToken.None);
        Assert.Equal("bao_cao", applied.DocumentTypeKey);
        Assert.Equal("17", applied.After);
        Assert.True(applied.Reopened);
        Assert.True(applied.Revalidated);
        Assert.True(applied.SourceUnchanged);
    }

    /// <summary>An unsupported or tampered confirmed type is refused, never coerced into the profile.</summary>
    [Fact]
    public async Task Confirmed_type_outside_the_profile_is_refused()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);

        foreach (var tampered in new[] { "phu_luc_ban_sao", "HD05", "not_a_type", "../etc/passwd" })
        {
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
                Processor().InspectAsync(new MemoryStream(bytes, writable: false), tampered, CancellationToken.None));
            Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", error.Code);
        }

        var target = inspection.MutableTargets!.First(t => t.Property == "section.margin_right_mm");
        var bad = new FormattingApplyRequest(
            inspection.SourceSha256, "not_a_type", "section.margin_right_mm", target.TargetId,
            target.CurrentValue!, "17", inspection.Binding!.Id, inspection.Binding.Digest, target.RuleId);
        var applyError = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().ApplyFormattingAsync(new MemoryStream(bytes, writable: false), bad, CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", applyError.Code);

        // A missing confirmed type is refused rather than inferred from the detector.
        var empty = bad with { DocumentTypeKey = "" };
        var missing = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().ApplyFormattingAsync(new MemoryStream(bytes, writable: false), empty, CancellationToken.None));
        Assert.Equal("DOCUMENT_TYPE_REQUIRED", missing.Code);
    }

    /// <summary>
    /// A confirmation may correct which administrative type applies, but it must
    /// never relabel a document the detector showed is not administrative
    /// administrative material (appendix, copy) as an administrative type.
    /// </summary>
    [Theory]
    [InlineData("07-appendix.docx")]
    [InlineData("08-copy.docx")]
    public async Task Non_administrative_documents_cannot_be_relabelled_by_confirmation(string fixture)
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture(fixture));
        var detected = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);
        Assert.NotEqual("cong_van", detected.Binding!.DocumentTypeKey);

        foreach (var adminType in new[] { "cong_van", "quyet_dinh", "bao_cao", "ke_hoach", "to_trinh" })
        {
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
                Processor().InspectAsync(new MemoryStream(bytes, writable: false), adminType, CancellationToken.None));
            Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", error.Code);
        }
    }

    /// <summary>
    /// A Party (HD05) document is refused even when it carries a formal
    /// administrative heading, because the ND30 profile must never be applied to
    /// Party issuing evidence. This is content evidence, not a string key.
    /// </summary>
    [Fact]
    public async Task Party_document_with_a_formal_administrative_heading_is_refused()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("12-party-decision.docx"));

        // Even the plain detection run refuses to bind the administrative profile.
        var detected = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", detected.Code);

        foreach (var adminType in new[] { "cong_van", "quyet_dinh", "bao_cao", "ke_hoach", "to_trinh" })
        {
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
                Processor().InspectAsync(new MemoryStream(bytes, writable: false), adminType, CancellationToken.None));
            Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", error.Code);
        }

        // Mutation cannot be enabled through the apply path either. The real profile
        // binding is used so the refusal comes from the regime check, not an earlier
        // digest or staleness guard.
        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        var digest = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)).ToLowerInvariant();
        var request = new FormattingApplyRequest(
            digest, "quyet_dinh", "section.margin_right_mm", "s1",
            "15", "17", profile.Id, profile.Digest, "ND30.PL1.I.GENERAL.MARGIN_RIGHT");
        var applyError = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().ApplyFormattingAsync(new MemoryStream(bytes, writable: false), request, CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", applyError.Code);
    }

    /// <summary>
    /// The confirmed type must survive into the rule-evaluation context, so a
    /// type-specific applicability field reflects the selected type and not the
    /// detector's suggestion.
    /// </summary>
    [Fact]
    public async Task Confirmed_type_reaches_the_rule_evaluation_context()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var confirmed = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), "bao_cao", CancellationToken.None);
        Assert.Equal("bao_cao", confirmed.Binding!.DocumentTypeKey);

        // The scoped statement names the confirmed type.
        Assert.Contains("Báo cáo", confirmed.ScopeStatement!, StringComparison.Ordinal);
        Assert.DoesNotContain("Quyết định", confirmed.ScopeStatement!, StringComparison.Ordinal);

        // The decisive proof: the rule-evaluation context itself received the
        // selected type, not the detector's suggestion. A binding or statement alone
        // would not prove the evaluator used it.
        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        var parsed = new DocxParser().Parse(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", "02-named-decision.docx")));
        Assert.NotNull(parsed.Document);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);
        Assert.Equal("quyet_dinh", DocumentProcessorService.DetectTypeKey(semantic));

        var confirmedType = profile.FindDocumentType("bao_cao")!;
        var confirmedContext = FormattingObservationAdapter.BuildContext(parsed.Document!, semantic, confirmedType, profile, new DateOnly(2026, 9, 19));
        Assert.Equal("report", confirmedContext.Fields["document.type"]);

        var detectedContext = FormattingObservationAdapter.BuildContext(parsed.Document!, semantic, profile.FindDocumentType("quyet_dinh")!, profile, new DateOnly(2026, 9, 19));
        Assert.Equal("quyet_dinh", detectedContext.Fields["document.type"]);
        Assert.NotEqual(detectedContext.Fields["document.type"], confirmedContext.Fields["document.type"]);
    }

    /// <summary>
    /// A Party issuer carried by a table (not a plain paragraph) is still issuing
    /// evidence, so the administrative profile must refuse the document.
    /// </summary>
    [Fact]
    public async Task Party_authority_in_a_table_is_refused()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("13-party-authority-table.docx"));
        foreach (var adminType in new[] { "cong_van", "quyet_dinh", "bao_cao", "ke_hoach", "to_trinh" })
        {
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
                Processor().InspectAsync(new MemoryStream(bytes, writable: false), adminType, CancellationToken.None));
            Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", error.Code);
        }
    }

    /// <summary>
    /// A legitimate ND30 company document that only mentions the Party or a trade
    /// union in its subject/body must NOT be treated as Party-issued. The gate reads
    /// issuing-authority evidence, never arbitrary body text.
    /// </summary>
    [Fact]
    public async Task Administrative_document_mentioning_the_party_in_its_body_is_not_refused()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("14-admin-body-party-mention.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);
        Assert.NotNull(inspection.Binding);
        Assert.Equal("quyet_dinh", inspection.Binding!.DocumentTypeKey);
        Assert.NotEmpty(inspection.MutableTargets!);

        // A confirmed type on the same document is still honoured.
        var confirmed = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), "bao_cao", CancellationToken.None);
        Assert.Equal("bao_cao", confirmed.Binding!.DocumentTypeKey);
    }

    /// <summary>
    /// A Party banner carried in the Word page header is issuing evidence even
    /// though the detector only flattens tables and body paragraphs. With a formal
    /// QUYET DINH heading in the body this used to be accepted as ND30.
    /// </summary>
    [Fact]
    public async Task Party_banner_in_the_page_header_is_refused()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("15-party-banner-header.docx"));

        // The header is really present, so the refusal is not vacuous.
        var parsed = new DocxParser().Parse(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", "15-party-banner-header.docx")));
        Assert.NotNull(parsed.Document);
        Assert.NotEmpty(parsed.Document!.Headers);
        Assert.Contains(parsed.Document.Headers.SelectMany(h => h.Paragraphs),
            p => p.Runs.Any(r => r.Text.Contains("Đảng", StringComparison.OrdinalIgnoreCase)));

        var detected = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", detected.Code);

        foreach (var adminType in new[] { "cong_van", "quyet_dinh", "bao_cao", "ke_hoach", "to_trinh" })
        {
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
                Processor().InspectAsync(new MemoryStream(bytes, writable: false), adminType, CancellationToken.None));
            Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", error.Code);
        }

        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var request = new FormattingApplyRequest(
            digest, "quyet_dinh", "section.margin_right_mm", "s1",
            "15", "17", profile.Id, profile.Digest, "ND30.PL1.I.GENERAL.MARGIN_RIGHT");
        var applyError = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().ApplyFormattingAsync(new MemoryStream(bytes, writable: false), request, CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", applyError.Code);
    }

    /// <summary>
    /// The header pass must stay narrow: a header carrying no Party issuer must not
    /// refuse a legitimate administrative document, and the ND30 body-mention case
    /// must keep working.
    /// </summary>
    [Fact]
    public async Task Header_check_does_not_over_refuse_administrative_documents()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);
        Assert.Equal("quyet_dinh", inspection.Binding!.DocumentTypeKey);
        Assert.NotEmpty(inspection.MutableTargets!);

        var mention = await File.ReadAllBytesAsync(SemanticFixture("14-admin-body-party-mention.docx"));
        var mentionInspection = await Processor().InspectAsync(new MemoryStream(mention, writable: false), CancellationToken.None);
        Assert.Equal("quyet_dinh", mentionInspection.Binding!.DocumentTypeKey);
    }

    /// <summary>
    /// A Party issuer inside a table in the Word page header is still issuer
    /// evidence. The header cell paragraph is nested under the header root, so the
    /// parser must surface it for the regime gate to see it at all.
    /// </summary>
    [Fact]
    public async Task Party_banner_in_a_header_table_is_refused()
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("16-party-banner-header-table.docx"));

        // The header paragraph exists only because nested header-table paragraphs are
        // now parsed; without that the refusal below would be vacuous.
        var parsed = new DocxParser().Parse(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", "16-party-banner-header-table.docx")));
        Assert.NotNull(parsed.Document);
        Assert.NotEmpty(parsed.Document!.Headers);
        var headerTexts = parsed.Document.Headers.SelectMany(h => h.Paragraphs).Select(p => string.Concat(p.Runs.Select(r => r.Text))).ToList();
        Assert.Contains(headerTexts, t => t.Contains("Đảng", StringComparison.OrdinalIgnoreCase));

        // The issuer really is nested inside a header table. Reading the header part
        // straight from the package proves there is a table and that the header root's
        // only paragraph sits inside it, so this paragraph is reachable solely as a
        // descendant rather than as a direct child.
        var headerXml = ReadHeaderPartXml("16-party-banner-header-table.docx");
        Assert.Contains("<w:tbl>", headerXml, StringComparison.Ordinal);
        var tableStart = headerXml.IndexOf("<w:tbl>", StringComparison.Ordinal);
        var paragraphStart = headerXml.IndexOf("<w:p>", StringComparison.Ordinal);
        var paragraphEnd = headerXml.IndexOf("</w:p>", StringComparison.Ordinal);
        Assert.True(tableStart >= 0 && paragraphStart > tableStart && paragraphEnd > paragraphStart,
            "The header issuer paragraph must be nested inside the header table.");

        var detected = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", detected.Code);

        foreach (var adminType in new[] { "cong_van", "quyet_dinh", "bao_cao", "ke_hoach", "to_trinh" })
        {
            var error = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
                Processor().InspectAsync(new MemoryStream(bytes, writable: false), adminType, CancellationToken.None));
            Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", error.Code);
        }

        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        var digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        var request = new FormattingApplyRequest(
            digest, "quyet_dinh", "section.margin_right_mm", "s1",
            "15", "17", profile.Id, profile.Digest, "ND30.PL1.I.GENERAL.MARGIN_RIGHT");
        var applyError = await Assert.ThrowsAsync<DocumentProcessorException>(() =>
            Processor().ApplyFormattingAsync(new MemoryStream(bytes, writable: false), request, CancellationToken.None));
        Assert.Equal("UNSUPPORTED_DOCUMENT_TYPE", applyError.Code);
    }

    /// <summary>
    /// The canonical profile name for line spacing is `paragraph.line_spacing_lines`.
    /// It must be classified as a paragraph property, otherwise a line-spacing target
    /// would be routed to the section resolver and written nowhere. Only the legacy
    /// `paragraph.line_spacing` spelling used to be recognised.
    ///
    /// Scope note, stated precisely: this asserts the routing/classification defect
    /// that was fixed. The end-to-end write is NOT exercised here, because no shipped
    /// v23 fixture currently exposes a direct line-spacing target: target enumeration
    /// requires direct spacing with an "auto" line rule, and 02-named-decision and
    /// 05-unusual-spacing both fail that check and are therefore (correctly) not
    /// offered the target. Building a fixture with direct auto line spacing is the
    /// outstanding work to make the write itself provable.
    /// </summary>
    [Fact]
    public void Canonical_line_spacing_property_is_classified_as_paragraph_scoped()
    {
        Assert.True(FormattingPropertyMutationExecutor.IsParagraphProperty("paragraph.line_spacing_lines"));
        Assert.True(FormattingPropertyMutationExecutor.IsParagraphProperty("paragraph.line_spacing"));

        var profile = FormattingProfileV1.Load(RuleCatalog.LoadVerifiedRelease(FindRoot()));
        var canonical = profile.FindMutationTarget("paragraph.line_spacing_lines");
        Assert.NotNull(canonical);
        Assert.Equal("paragraph", canonical!.Scope);
        Assert.Equal("dòng", canonical.Unit);
    }

    /// <summary>
    /// No shipped fixture currently offers the canonical line-spacing target, which is
    /// why the write path cannot be proven end-to-end yet. This records that fact so a
    /// future fixture that does offer it is a deliberate, visible change.
    /// </summary>
    [Theory]
    [InlineData("02-named-decision.docx")]
    [InlineData("05-unusual-spacing.docx")]
    public async Task Shipped_fixtures_do_not_currently_expose_a_direct_line_spacing_target(string fixture)
    {
        var bytes = await File.ReadAllBytesAsync(SemanticFixture(fixture));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);
        Assert.DoesNotContain(inspection.MutableTargets!, t => t.Property == "paragraph.line_spacing_lines");
    }

    /// <summary>
    /// Positive end-to-end proof of the canonical line-spacing write path.
    ///
    /// Shipped v23 fixtures carry no direct line spacing, so the copy is made in
    /// memory and given direct <c>w:spacing</c> with <c>w:lineRule="auto"</c> on a
    /// top-level paragraph the semantic detector really classifies as Body. No
    /// fixture file on disk is modified and no persistent artifact is created.
    ///
    /// The body paragraph id is taken from the parser's own top-level paragraph list
    /// (<c>pN</c>), never from a lexicographic pick over Body-role evidence: a Body
    /// role can carry a table-cell id such as <c>t1.r1.c1.p1</c>, which is not a
    /// body paragraph at all.
    /// </summary>
    [Fact]
    public async Task Canonical_line_spacing_target_is_written_end_to_end()
    {
        var fixturePath = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "../../../../../fixtures/v23", "02-named-decision.docx"));
        var original = await File.ReadAllBytesAsync(fixturePath);
        var originalSha = Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant();

        var parsed = new DocxParser().Parse(fixturePath);
        Assert.NotNull(parsed.Document);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);

        // Top-level paragraph ids as the parser numbers them.
        var topLevelIds = parsed.Document!.Paragraphs.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        Assert.NotEmpty(topLevelIds);

        // Body-role evidence restricted to genuine top-level paragraphs, in parser order.
        var bodyIds = semantic.Components
            .Where(c => c.Role == Nd30.SemanticDetector.Model.SemanticRole.Body)
            .Select(c => c.Evidence.ParagraphId)
            .Where(id => topLevelIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => int.Parse(id.TrimStart('p'), CultureInfo.InvariantCulture))
            .ToList();
        Assert.NotEmpty(bodyIds);
        var bodyParagraphId = bodyIds[0];
        var bodyIndex = int.Parse(bodyParagraphId.TrimStart('p'), CultureInfo.InvariantCulture) - 1;
        Assert.InRange(bodyIndex, 0, parsed.Document.Paragraphs.Count - 1);

        // Only fixture data is used to choose and verify the target.
        var expectedText = string.Concat(parsed.Document.Paragraphs[bodyIndex].Runs.Select(r => r.Text));
        Assert.NotEqual(string.Empty, expectedText.Trim());

        var synthetic = AddDirectAutoLineSpacing(original, bodyIndex, lineTwips: 360);

        // Verify the parser really re-reads the injected direct auto line as 1.5 lines.
        using var syntheticSnapshot = await Nd30.DocumentEngine.Safety.SafeDocxSnapshot
            .CreateAsync(new MemoryStream(synthetic, writable: false), CancellationToken.None);
        var reparsed = new DocxParser().Parse(syntheticSnapshot);
        Assert.NotNull(reparsed.Document);
        var rereadParagraph = reparsed.Document!.Paragraphs[bodyIndex];
        var spacing = rereadParagraph.DirectFormatting.LineSpacing;
        Assert.NotNull(spacing);
        // The injected w:line=360 with w:lineRule="auto" must read back as 1.5 lines.
        Assert.Equal("360", spacing!.RawValue);
        Assert.Equal(LineSpacingRule.Auto, spacing.Rule);
        Assert.NotNull(spacing.Lines);
        Assert.Equal(1.5d, spacing.Lines!.Value, 3);

        var inspection = await Processor().InspectAsync(new MemoryStream(synthetic, writable: false), CancellationToken.None);

        // Split the two possible failures: is ANY canonical target offered at all, and
        // is one of them bound to the chosen body paragraph?
        var canonicalTargets = inspection.MutableTargets!
            .Where(t => t.Property == "paragraph.line_spacing_lines")
            .Select(t => new { t.TargetId, t.CurrentValue, t.DirectOnly })
            .ToList();
        Assert.True(canonicalTargets.Count > 0,
            "No paragraph.line_spacing_lines target was offered at all: " +
            string.Join(", ", inspection.MutableTargets!.Select(t => t.Property + "@" + t.TargetId)));
        var offered = canonicalTargets.FirstOrDefault(t => t.TargetId == bodyParagraphId);
        Assert.True(offered is not null,
            $"Canonical targets offered: {string.Join(", ", canonicalTargets.Select(t => t.TargetId))}; chosen body id: {bodyParagraphId}");
        Assert.True(offered!.DirectOnly);
        Assert.Equal("1.5", offered.CurrentValue);

        // Real binding: same profile/type as the untouched original, real in-scope rule.
        var baseline = await Processor().InspectAsync(new MemoryStream(original, writable: false), CancellationToken.None);
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        Assert.Equal(baseline.Binding!.Id, inspection.Binding!.Id);
        Assert.Equal(baseline.Binding.DocumentTypeKey, inspection.Binding.DocumentTypeKey);
        var ruleTarget = FormattingProfileV1.Load(catalog).FindMutationTarget("paragraph.line_spacing_lines")!;
        Assert.Contains(ruleTarget.RuleId, FormattingProfileV1.Load(catalog).ResolveRuleSubset(catalog, inspection.Binding.DocumentTypeKey));

        // Derive the desired value from what was actually observed, then assert it is
        // legal and genuinely different, so this can never be a no-op.
        double observed = double.TryParse(offered.CurrentValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedObserved) ? parsedObserved : 0d;
        string desired = Math.Abs(observed - 1.5d) < 0.001d ? "1" : "1.5";
        Assert.True(desired is "1" or "1.5", "Desired line spacing must stay inside the documented bounds.");
        Assert.NotEqual(desired, offered.CurrentValue);

        var applied = await Processor().ApplyFormattingAsync(
            new MemoryStream(synthetic, writable: false),
            new FormattingApplyRequest(
                inspection.SourceSha256, inspection.Binding.DocumentTypeKey, "paragraph.line_spacing_lines",
                bodyParagraphId, offered.CurrentValue!, desired,
                inspection.Binding.Id, inspection.Binding.Digest, ruleTarget.RuleId),
            CancellationToken.None);

        Assert.Equal("paragraph.line_spacing_lines", applied.Property);
        Assert.Equal(bodyParagraphId, applied.TargetId);
        Assert.Equal(ruleTarget.RuleId, applied.RuleId);
        Assert.Equal(desired, applied.After);
        Assert.True(applied.Reopened);
        Assert.True(applied.Revalidated);
        Assert.True(applied.SourceUnchanged);

        // Output differs; the in-memory source and the on-disk fixture are both intact.
        Assert.NotEqual(inspection.SourceSha256, applied.OutputSha256);
        Assert.Equal(originalSha, Convert.ToHexString(SHA256.HashData(original)).ToLowerInvariant());
        Assert.Equal(originalSha, Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(fixturePath))).ToLowerInvariant());

        // Re-inspection reads back exactly the applied value on the same target.
        var after = await Processor().InspectAsync(new MemoryStream(applied.OutputBytes, writable: false), CancellationToken.None);
        var reread = after.MutableTargets!.FirstOrDefault(t =>
            t.Property == "paragraph.line_spacing_lines" && t.TargetId == bodyParagraphId);
        Assert.NotNull(reread);
        Assert.Equal(desired, reread!.CurrentValue);
    }

    /// <summary>
    /// Returns a modified copy of a DOCX in memory, adding direct paragraph spacing
    /// with an automatic line rule to the given paragraph. The input bytes are never
    /// mutated and nothing is written to disk.
    /// </summary>
    private     /// <summary>
    /// Rewrites the style default run font in memory to a family ND30 does not accept.
    ///
    /// Every checked-in fixture already declares Times New Roman, which is exactly what the
    /// ND30 font-family rule requires, so the rule PASSES and emits no finding. Without a
    /// genuinely non-compliant document the "font finding is reported as SUGGEST_ONLY"
    /// assertion would pass vacuously against an empty collection and prove nothing.
    /// </summary>
    static byte[] WithNonCompliantDefaultFont(byte[] source, string family)
    {
        using var stream = new MemoryStream();
        stream.Write(source, 0, source.Length);
        stream.Position = 0;

        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, true))
        {
            var stylesPart = document.MainDocumentPart!.StyleDefinitionsPart
                ?? throw new InvalidOperationException("fixture has no style definitions part");
            var styles = stylesPart.Styles ?? throw new InvalidOperationException("fixture has no styles root");

            var docDefaults = styles.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.DocDefaults>();
            if (docDefaults is null)
            {
                docDefaults = new DocumentFormat.OpenXml.Wordprocessing.DocDefaults();
                styles.InsertAt(docDefaults, 0);
            }

            var runDefaults = docDefaults.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.RunPropertiesDefault>();
            if (runDefaults is null)
            {
                runDefaults = new DocumentFormat.OpenXml.Wordprocessing.RunPropertiesDefault();
                docDefaults.AppendChild(runDefaults);
            }

            var runProperties = runDefaults.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.RunPropertiesBaseStyle>();
            if (runProperties is null)
            {
                runProperties = new DocumentFormat.OpenXml.Wordprocessing.RunPropertiesBaseStyle();
                runDefaults.AppendChild(runProperties);
            }

            runProperties.RemoveAllChildren<DocumentFormat.OpenXml.Wordprocessing.RunFonts>();
            runProperties.InsertAt(new DocumentFormat.OpenXml.Wordprocessing.RunFonts
            {
                Ascii = family,
                HighAnsi = family,
                ComplexScript = family,
            }, 0);

            // Also override the Normal style, which is what the resolver reaches for a body run.
            var normal = styles.Descendants<DocumentFormat.OpenXml.Wordprocessing.Style>()
                .FirstOrDefault(s => s.StyleId?.Value == "Normal");
            if (normal is not null)
            {
                var normalRunProperties = normal.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.StyleRunProperties>();
                if (normalRunProperties is null)
                {
                    normalRunProperties = new DocumentFormat.OpenXml.Wordprocessing.StyleRunProperties();
                    normal.AppendChild(normalRunProperties);
                }
                normalRunProperties.RemoveAllChildren<DocumentFormat.OpenXml.Wordprocessing.RunFonts>();
                normalRunProperties.InsertAt(new DocumentFormat.OpenXml.Wordprocessing.RunFonts
                {
                    Ascii = family,
                    HighAnsi = family,
                    ComplexScript = family,
                }, 0);
            }

            styles.Save();
        }

        var result = stream.ToArray();

        // Independent read-only verification that the rewrite actually landed.
        using (var verifyStream = new MemoryStream(result, writable: false))
        using (var reopened = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(verifyStream, false))
        {
            var xml = reopened.MainDocumentPart!.StyleDefinitionsPart!.Styles!.OuterXml;
            Assert.Contains(family, xml, StringComparison.Ordinal);
        }

        return result;
    }

    /// <summary>
    /// Injects a direct "space after" on one body paragraph. Zero is below the ND30 minimum
    /// paragraph gap, so this produces a genuine failing finding to assert eligibility on.
    /// </summary>
    static byte[] AddDirectSpacingAfter(byte[] source, int paragraphIndex, int afterTwips)
    {
        using var stream = new MemoryStream();
        stream.Write(source, 0, source.Length);
        stream.Position = 0;

        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, true))
        {
            var body = document.MainDocumentPart!.Document.Body!;
            var target = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .ElementAtOrDefault(paragraphIndex);
            Assert.NotNull(target);

            var properties = target!.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties>();
            if (properties is null)
            {
                properties = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties();
                target.InsertAt(properties, 0);
            }

            // afterLines/afterAutospacing would override an explicit `after`, so clear them.
            var spacing = properties.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines>();
            if (spacing is null)
            {
                spacing = new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines();
                properties.AppendChild(spacing);
            }
            spacing.After = afterTwips.ToString(CultureInfo.InvariantCulture);
            spacing.AfterLines = null;
            spacing.AfterAutoSpacing = null;

            document.MainDocumentPart.Document.Save();
        }

        var result = stream.ToArray();

        using (var verifyStream = new MemoryStream(result, writable: false))
        using (var reopened = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(verifyStream, false))
        {
            var verified = reopened.MainDocumentPart!.Document.Body!
                .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .ElementAtOrDefault(paragraphIndex)!;
            var readBack = verified.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties>()!
                .GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines>();
            Assert.NotNull(readBack);
            Assert.Equal(afterTwips.ToString(CultureInfo.InvariantCulture), readBack!.After!.Value);
        }

        return result;
    }

    /// <summary>
    /// Eligibility must be bound by the profile's declared rule tuple, not by property-name
    /// spelling. A rule targets a bare property (<c>paragraph_spacing_pt</c>,
    /// <c>line_spacing</c>) while the matching mutation target is scope-qualified
    /// (<c>paragraph.spacing_after_pt</c>, <c>paragraph.line_spacing_lines</c>), so these two
    /// findings are the case that any name-based comparison gets wrong. Both rules have a declared
    /// mutation target, so neither may be downgraded to SUGGEST_ONLY; font family has none and
    /// must stay SUGGEST_ONLY.
    /// </summary>
    [Fact]
    public async Task Eligibility_is_bound_by_rule_id_so_target_backed_findings_are_not_downgraded()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);

        // The declared tuple these assertions rely on.
        Assert.Equal(
            "ND30.PL1.I.II.6E.PARAGRAPH_GAP_MIN",
            profile.FindMutationTarget("paragraph.spacing_after_pt")!.RuleId);
        Assert.Equal(
            "ND30.PL1.I.II.6E.BODY_LINE_SPACING_RANGE",
            profile.FindMutationTarget("paragraph.line_spacing_lines")!.RuleId);
        Assert.Null(profile.FindMutationTarget("document.effective_font_family"));

        var original = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        using var snapshot = await Nd30.DocumentEngine.Safety.SafeDocxSnapshot
            .CreateAsync(new MemoryStream(original, writable: false), CancellationToken.None);
        var parsed = new DocxParser().Parse(snapshot);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);

        // A genuine body paragraph, chosen the same way the working line-spacing test does.
        // Both spacing rules observe body-role paragraphs only, so picking the first
        // top-level paragraph instead could silently select a heading and produce no finding.
        var topLevelIds = parsed.Document!.Paragraphs.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        var bodyIds = semantic.Components
            .Where(c => c.Role == Nd30.SemanticDetector.Model.SemanticRole.Body)
            .Select(c => c.Evidence.ParagraphId)
            .Where(id => topLevelIds.Contains(id))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => int.Parse(id.TrimStart('p'), CultureInfo.InvariantCulture))
            .ToList();
        Assert.NotEmpty(bodyIds);
        var bodyIndex = int.Parse(bodyIds[0].TrimStart('p'), CultureInfo.InvariantCulture) - 1;
        Assert.InRange(bodyIndex, 0, parsed.Document.Paragraphs.Count - 1);

        // Both are deliberately non-compliant, against the declared numeric ranges:
        // BODY_LINE_SPACING_RANGE is 1.0-1.5 line (240 twips sits exactly at the minimum and
        // still passes, so 480 = 2.0 lines is used), and PARAGRAPH_GAP_MIN is 6-999 pt, so a
        // 0 twip after-gap fails the minimum.
        //
        // The line spacing is applied to EVERY body paragraph: the range rule reads a single
        // aggregated body value, so a document that merely mixes 2.0 with the compliant
        // paragraphs yields no observation and therefore no finding at all. The after-gap needs
        // only one paragraph, because a minimum rule fails as soon as the smallest gap is 0.
        //
        // Order matters: the line-spacing helper replaces the whole w:spacing element, so the
        // after-gap must be injected last or it would be wiped.
        var bodyIndices = bodyIds
            .Select(id => int.Parse(id.TrimStart('p'), CultureInfo.InvariantCulture) - 1)
            .Where(i => i >= 0 && i < parsed.Document.Paragraphs.Count)
            .Distinct()
            .ToList();

        var bytes = original;
        foreach (var index in bodyIndices)
            bytes = AddDirectAutoLineSpacing(bytes, index, lineTwips: 480);
        bytes = AddDirectSpacingAfter(bytes, bodyIndex, afterTwips: 0);

        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);
        var findings = inspection.Findings!;

        const string GapRule = "ND30.PL1.I.II.6E.PARAGRAPH_GAP_MIN";
        const string LineRule = "ND30.PL1.I.II.6E.BODY_LINE_SPACING_RANGE";

        // The declared tuple is what eligibility binds on, for both rules.
        var targetRuleIds = profile.MutationTargets.Select(t => t.RuleId).ToHashSet(StringComparer.Ordinal);
        Assert.Contains(GapRule, targetRuleIds);
        Assert.Contains(LineRule, targetRuleIds);
        Assert.Equal(LineRule, profile.FindMutationTarget("paragraph.line_spacing_lines")!.RuleId);
        Assert.DoesNotContain("ND30.PL1.I.GENERAL.FONT_FAMILY", targetRuleIds);

        // The eligibility mapping binds on RuleId alone, so the soundness of that depends on one
        // rule id declaring at most one mutation target. That is true of this profile, and it is
        // asserted here rather than assumed: if a future target ever reuses a rule id for a second
        // property, RuleId-only matching would mark a finding eligible on the strength of a target
        // that belongs to a different property, and this assertion fails.
        //
        // Property equality is deliberately NOT also required. The catalog and the profile use
        // different vocabularies for the same quantity (the rule targets `paragraph_spacing_pt`
        // while the target is `paragraph.spacing_after_pt`), so a property comparison would reject
        // genuinely applicable findings and re-introduce the blanket downgrade this test guards.
        var ruleIdCounts = profile.MutationTargets
            .GroupBy(target => target.RuleId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.DoesNotContain(ruleIdCounts, entry => entry.Value > 1);

        var propertyCounts = profile.MutationTargets
            .GroupBy(target => target.Property, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        Assert.DoesNotContain(propertyCounts, entry => entry.Value > 1);

        // Filter by the authoritative rule id, then pin the declared rule-property mapping.
        var gapFindings = findings.Where(f => f.RuleId == GapRule).ToArray();
        var lineFindings = findings.Where(f => f.RuleId == LineRule).ToArray();

        // Both rules genuinely fail on the injected document: the gap against the declared
        // 6-999 pt minimum, the line spacing against the declared 1.0-1.5 line range. The gap
        // proves the bare/qualified property mismatch is handled; the line spacing proves the
        // `semantic_component.body.line_spacing` key actually has a producer.
        Assert.NotEmpty(gapFindings);
        Assert.NotEmpty(lineFindings);
        Assert.Contains(gapFindings, f => f.Property == "paragraph_spacing_pt");
        Assert.Contains(lineFindings, f => f.Property == "line_spacing");

        // Neither is downgraded by the eligibility mapping, and neither is SUGGEST_ONLY.
        Assert.All(gapFindings, f => Assert.NotEqual("SUGGEST_ONLY", f.PatchEligibility));
        Assert.All(lineFindings, f => Assert.NotEqual("SUGGEST_ONLY", f.PatchEligibility));

        // The real defect was a blanket downgrade, so assert it as an invariant over every
        // finding actually reported: nothing bound to a declared target may be SUGGEST_ONLY.
        foreach (var reported in findings)
        {
            if (targetRuleIds.Contains(reported.RuleId))
                Assert.NotEqual("SUGGEST_ONLY", reported.PatchEligibility);
        }

        // The applies are also proven to be genuinely offered: a target-backed finding that
        // had no corresponding target would be an eligibility claim with nothing behind it.

        // The body line-spacing observation is genuinely produced, not synthesised: the finding
        // must carry the injected out-of-range multiple as its observed value. Parsed with the
        // invariant culture and compared numerically, so a substring such as "2" inside "2.04"
        // or "12" cannot satisfy this.
        Assert.Contains(lineFindings, f =>
            double.TryParse(f.Observed, NumberStyles.Float, CultureInfo.InvariantCulture, out var observed)
            && observed == 2.0d);

        // Do not claim an apply that is not actually offered: the injected direct value must
        // appear as a target bound to that same paragraph.
        Assert.Contains(inspection.MutableTargets!, t =>
            t.Property == "paragraph.spacing_after_pt" && t.TargetId == $"p{bodyIndex + 1}");
        Assert.Contains(inspection.MutableTargets!, t =>
            t.Property == "paragraph.line_spacing_lines" && t.TargetId == $"p{bodyIndex + 1}");
    }

    /// <summary>
    /// Sets one paragraph's line rule and value, preserving any existing after-gap.
    ///
    /// The DOCX line rule is what makes the quantity unambiguous: `auto` carries a multiple of
    /// lines, while `exact` and `atLeast` carry an absolute point value. Every case below is
    /// produced with this one helper so no case can be quietly special-cased.
    /// </summary>
    static byte[] SetLineSpacing(
        byte[] source,
        int paragraphIndex,
        DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues rule,
        int value)
    {
        using var stream = new MemoryStream();
        stream.Write(source, 0, source.Length);
        stream.Position = 0;

        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, true))
        {
            var target = document.MainDocumentPart!.Document.Body!
                .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .ElementAtOrDefault(paragraphIndex);
            Assert.NotNull(target);

            var properties = target!.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties>();
            if (properties is null)
            {
                properties = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties();
                target.InsertAt(properties, 0);
            }

            // Reuse the element so an after-gap injected earlier survives.
            var spacing = properties.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines>();
            if (spacing is null)
            {
                spacing = new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines();
                properties.AppendChild(spacing);
            }
            spacing.Line = value.ToString(CultureInfo.InvariantCulture);
            spacing.LineRule = rule;

            document.MainDocumentPart.Document.Save();
        }

        var result = stream.ToArray();

        using (var verifyStream = new MemoryStream(result, writable: false))
        using (var reopened = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(verifyStream, false))
        {
            var verified = reopened.MainDocumentPart!.Document.Body!
                .Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .ElementAtOrDefault(paragraphIndex)!
                .GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties>()!
                .GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines>();
            Assert.NotNull(verified);
            Assert.Equal(rule, verified!.LineRule!.Value);
            Assert.Equal(value.ToString(CultureInfo.InvariantCulture), verified.Line!.Value);
        }

        return result;
    }

    /// <summary>
    /// Applies one line rule uniformly to every detector-identified body paragraph.
    /// <paramref name="skip"/> leaves that index untouched, which is how the
    /// partially-observed case is produced.
    /// </summary>
    static byte[] SetBodyLineSpacing(
        byte[] source,
        IReadOnlyList<int> bodyIndices,
        DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues rule,
        int value,
        int? skip = null)
    {
        var bytes = source;
        foreach (var index in bodyIndices)
        {
            if (skip is { } skipped && skipped == index) continue;
            bytes = SetLineSpacing(bytes, index, rule, value);
        }
        return bytes;
    }

    static (ValidationResult Result, ValidationContext Context, IReadOnlyList<int> BodyIndices)
        EvaluateBodyLineSpacing(byte[] bytes)
    {
        const string LineRuleId = "ND30.PL1.I.II.6E.BODY_LINE_SPACING_RANGE";

        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var documentType = profile.FindDocumentType("quyet_dinh");
        Assert.NotNull(documentType);
        Assert.Contains(LineRuleId, profile.ResolveRuleSubset(catalog, "quyet_dinh"));

        using var snapshot = Nd30.DocumentEngine.Safety.SafeDocxSnapshot
            .CreateAsync(new MemoryStream(bytes, writable: false), CancellationToken.None).GetAwaiter().GetResult();
        var parsed = new DocxParser().Parse(snapshot);
        Assert.NotNull(parsed.Document);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);

        var context = FormattingObservationAdapter.BuildContext(
            parsed.Document!, semantic, documentType!, profile, new DateOnly(2026, 9, 19));

        var bodyIndices = semantic.Components
            .Where(c => c.Role == Nd30.SemanticDetector.Model.SemanticRole.Body)
            .Select(c => c.Evidence.ParagraphId)
            .Where(id => parsed.Document!.Paragraphs.Any(p => p.Id == id))
            .Select(id => int.Parse(id.TrimStart('p'), CultureInfo.InvariantCulture) - 1)
            .Distinct()
            .OrderBy(i => i)
            .ToList();
        Assert.NotEmpty(bodyIndices);

        var rule = catalog.Rules.First(r => r.Id == LineRuleId);
        var result = new RuleEvaluator().Evaluate(rule, context);
        Assert.Equal(LineRuleId, result.RuleId);
        return (result, context, bodyIndices);
    }

    static double ObservedNumber(ValidationResult result) =>
        double.Parse(result.Observed, NumberStyles.Float, CultureInfo.InvariantCulture);

    /// <summary>
    /// The `semantic_component.body.line_spacing` alias must carry a line multiple only when
    /// every body paragraph states one unambiguously, and must stay silent otherwise. These are
    /// the cases the alias fix in 8d has to get right, including the distinction between
    /// "nothing stated" (NOT_EVALUATED) and "partially stated" (NEEDS_REVIEW).
    /// </summary>
    [Fact]
    public void Body_line_spacing_alias_is_unit_safe_across_auto_exact_atleast_and_mixed()
    {
        const string AliasKey = "semantic_component.body.line_spacing";
        var original = File.ReadAllBytes(SemanticFixture("02-named-decision.docx"));

        // The fixture is unmodified on disk; every case below is built in memory.
        var (seed, _, seedBody) = EvaluateBodyLineSpacing(original);
        var indices = seedBody;

        // Uniform auto, in range: evaluated and passing, with the exact multiple observed.
        foreach (var (twips, expected) in new[] { (240, 1.0d), (360, 1.5d) })
        {
            var bytes = SetBodyLineSpacing(original, indices, DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto, twips);
            var (result, context, _) = EvaluateBodyLineSpacing(bytes);

            Assert.Equal(ValidationStatus.PASS, result.Status);
            Assert.True(context.ObservedValues.ContainsKey(AliasKey), "alias must exist for uniform auto");
            Assert.Equal(expected, ObservedNumber(result), 6);
        }

        // Uniform auto, out of range: failing, with the exact multiple observed. Compared
        // numerically, never by substring, so an "Observed" of 2.04 could not pass as 2.
        var tooWide = SetBodyLineSpacing(original, indices, DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto, 480);
        var (failResult, failContext, _) = EvaluateBodyLineSpacing(tooWide);
        Assert.Equal(ValidationStatus.FAIL, failResult.Status);
        Assert.True(failContext.ObservedValues.ContainsKey(AliasKey));
        Assert.Equal(2.0d, ObservedNumber(failResult), 6);

        // Wholly exact or wholly atLeast: no line multiple is stated anywhere, so the alias is
        // absent and the rule reports a missing observation as NOT_EVALUATED. This is the case
        // that must NOT be reported as uncertainty.
        foreach (var rule in new[] { DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Exact, DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.AtLeast })
        {
            var bytes = SetBodyLineSpacing(original, indices, rule, 240);
            var (result, context, _) = EvaluateBodyLineSpacing(bytes);

            Assert.False(context.ObservedValues.ContainsKey(AliasKey), $"{rule} must not publish a line alias");
            Assert.Equal(ValidationStatus.NOT_EVALUATED, result.Status);
        }

        // Mixed auto/exact: some body paragraphs state a multiple and some do not, so the body
        // value is genuinely unknown. The alias must stay absent and the evaluator must report
        // uncertainty rather than silently evaluating the auto paragraphs alone.
        var mixed = original;
        var half = indices.Count / 2;
        for (var i = 0; i < indices.Count; i++)
            mixed = SetLineSpacing(mixed, indices[i], i < half ? DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto : DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Exact, 240);

        var (mixedResult, mixedContext, _) = EvaluateBodyLineSpacing(mixed);
        Assert.False(mixedContext.ObservedValues.ContainsKey(AliasKey), "mixed units must not publish a line alias");
        Assert.Equal(ValidationStatus.NEEDS_REVIEW, mixedResult.Status);

        // One body paragraph missing its observation: same shape as mixed, and equally unknown.
        var partial = SetBodyLineSpacing(original, indices, DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto, 240, skip: indices[^1]);
        var (partialResult, partialContext, _) = EvaluateBodyLineSpacing(partial);
        Assert.False(partialContext.ObservedValues.ContainsKey(AliasKey), "a missing observation must not publish a line alias");
        Assert.Equal(ValidationStatus.NEEDS_REVIEW, partialResult.Status);

        // The seed document itself was never written to.
        Assert.Equal(Convert.ToHexString(SHA256.HashData(original)),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(SemanticFixture("02-named-decision.docx")))));
        _ = seed;
    }

    static ValidationContext BuildFormattingContext(byte[] bytes, string typeKey)
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var documentType = profile.FindDocumentType(typeKey);
        Assert.NotNull(documentType);

        using var snapshot = Nd30.DocumentEngine.Safety.SafeDocxSnapshot
            .CreateAsync(new MemoryStream(bytes, writable: false), CancellationToken.None).GetAwaiter().GetResult();
        var parsed = new DocxParser().Parse(snapshot);
        Assert.NotNull(parsed.Document);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);
        return FormattingObservationAdapter.BuildContext(
            parsed.Document!, semantic, documentType!, profile, new DateOnly(2026, 9, 19));
    }

    /// <summary>
    /// The verified ND30 section rules target the bare `section.page_size`,
    /// `section.orientation` and `section.margin_*_mm` keys, while the adapter published only
    /// `section.<name>.<sectionId>` and `section.<name>.all`. Those six rules therefore had no
    /// producer and evaluated as NOT_EVALUATED for every document, including the right-margin
    /// rule the synthetic pilot applies. A single-section document has one unambiguous value, so
    /// the bare key must be published for it and the rule must actually evaluate.
    /// </summary>
    [Fact]
    public void Section_rules_observe_the_bare_keys_the_release_pack_targets()
    {
        var bytes = File.ReadAllBytes(SemanticFixture("02-named-decision.docx"));
        var context = BuildFormattingContext(bytes, "quyet_dinh");

        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var evaluator = new RuleEvaluator();
        var rules = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["section.page_size"] = "ND30.PL1.I.GENERAL.PAGE_SIZE_A4",
            ["section.orientation"] = "ND30.PL1.I.GENERAL.ORIENTATION_PORTRAIT_DEFAULT",
            ["section.margin_top_mm"] = "ND30.PL1.I.GENERAL.MARGIN_TOP",
            ["section.margin_right_mm"] = "ND30.PL1.I.GENERAL.MARGIN_RIGHT",
            ["section.margin_bottom_mm"] = "ND30.PL1.I.GENERAL.MARGIN_BOTTOM",
            ["section.margin_left_mm"] = "ND30.PL1.I.GENERAL.MARGIN_LEFT",
        };

        var asserted = 0;
        foreach (var (key, ruleId) in rules)
        {
            // The bare key must exist whenever the section-scoped observation for the same
            // property exists. A margin the document never states has no observation at all,
            // which stays honestly unevaluated rather than being invented.
            var scoped = context.ObservedValues.Keys
                .Any(k => k.StartsWith(key + ".", StringComparison.Ordinal));
            var bare = context.ObservedValues.ContainsKey(key);
            Assert.True(bare, $"bare section observation missing while a scoped one exists: {key}");

            if (!scoped && !bare) continue;

            var rule = catalog.Rules.First(r => r.Id == ruleId);
            Assert.NotEqual(ValidationStatus.NOT_EVALUATED, evaluator.Evaluate(rule, context).Status);
            asserted++;
        }

        // Guard against the whole loop passing vacuously on a fixture that states nothing.
        Assert.True(asserted >= 4, $"expected most section rules to evaluate, only {asserted} did");
    }

    /// <summary>
    /// `document_type_abbreviation` stays explicitly pending. ND30 special-cases the abbreviation
    /// form (a two-word type is not simply its initials), so deriving it mechanically would
    /// produce a wrong verdict. This pins that honesty so the four abbreviation rules are never
    /// quietly satisfied by a fabricated observation.
    /// </summary>
    [Fact]
    public void Document_type_abbreviation_stays_pending_and_is_never_invented()
    {
        Assert.Contains("document_type_abbreviation", FormattingProfileV1.PendingObservationProperties);
        Assert.DoesNotContain("document_type_abbreviation", FormattingProfileV1.ObservableProperties);

        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var context = BuildFormattingContext(File.ReadAllBytes(SemanticFixture("02-named-decision.docx")), "quyet_dinh");
        Assert.False(context.ObservedValues.ContainsKey("document_metadata.document_type_abbreviation"));

        var evaluator = new RuleEvaluator();
        var rule = catalog.Rules.First(r => r.Id == "ND30.PL3.I.ABBR.QUYET_DINH");
        Assert.Equal(ValidationStatus.NOT_EVALUATED, evaluator.Evaluate(rule, context).Status);
    }

    /// <summary>
    /// Builds a document with the given sections in memory. A null entry means the property is
    /// not written at all, which is how "the document does not state this" is represented.
    /// </summary>
    static byte[] MultiSectionDocx(params (int? Top, int? Right, int? Bottom, int? Left, uint? Width, uint? Height)[] sections)
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = main.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

            for (var i = 0; i < sections.Length; i++)
            {
                var (top, right, bottom, left, width, height) = sections[i];
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                        new DocumentFormat.OpenXml.Wordprocessing.Text($"Section {i + 1} body"))));

                var properties = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties();
                var sectionProperties = new DocumentFormat.OpenXml.Wordprocessing.SectionProperties();
                if (width is not null && height is not null)
                {
                    sectionProperties.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.PageSize
                    {
                        Width = width.Value,
                        Height = height.Value,
                    });
                }
                var margin = new DocumentFormat.OpenXml.Wordprocessing.PageMargin();
                if (top is not null) margin.Top = int.Parse(top.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (right is not null) margin.Right = uint.Parse(right.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (bottom is not null) margin.Bottom = int.Parse(bottom.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                if (left is not null) margin.Left = uint.Parse(left.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
                sectionProperties.AppendChild(margin);
                properties.AppendChild(sectionProperties);
                // A section break is a paragraph whose pPr carries the sectPr. Appending the
                // properties straight to the body would be invalid and the parser would see a
                // single section, which would make the partial/mixed cases untestable.
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(properties));
            }

            // Final section properties for the last section.
            var last = sections[^1];
            var finalSection = new DocumentFormat.OpenXml.Wordprocessing.SectionProperties();
            if (last.Width is not null && last.Height is not null)
            {
                finalSection.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.PageSize
                {
                    Width = last.Width.Value,
                    Height = last.Height.Value,
                });
            }
            var finalMargin = new DocumentFormat.OpenXml.Wordprocessing.PageMargin();
            if (last.Top is not null) finalMargin.Top = last.Top.Value;
            if (last.Right is not null) finalMargin.Right = (uint)last.Right.Value;
            if (last.Bottom is not null) finalMargin.Bottom = last.Bottom.Value;
            if (last.Left is not null) finalMargin.Left = (uint)last.Left.Value;
            finalSection.AppendChild(finalMargin);
            body.AppendChild(finalSection);

            main.Document.Save();
        }
        return stream.ToArray();
    }

    /// <summary>Reads the min/max of a rule's numeric_range constraint.</summary>
    static (double min, double max) NumericBounds(RuleDefinition rule)
    {
        var map = rule.Constraint as IReadOnlyDictionary<string, object?>;
        Assert.NotNull(map);
        return (
            Convert.ToDouble(map!["min"], CultureInfo.InvariantCulture),
            Convert.ToDouble(map!["max"], CultureInfo.InvariantCulture));
    }

    static ValidationStatus StatusOf(byte[] bytes, string typeKey, string ruleId)
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var rule = catalog.Rules.First(r => r.Id == ruleId);
        return new RuleEvaluator().Evaluate(rule, BuildFormattingContext(bytes, typeKey)).Status;
    }

    static double ObservedSectionMillimetres(byte[] bytes, string typeKey, string key)
    {
        var context = BuildFormattingContext(bytes, typeKey);
        Assert.True(context.ObservedValues.ContainsKey(key), $"missing observation: {key}");
        return Convert.ToDouble(context.ObservedValues[key], CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// A missing margin is not zero. The consensus projection must keep "not stated" distinct from
    /// a stated value, so an all-missing document is honestly NOT_EVALUATED, a partially stated or
    /// disagreeing document is NEEDS_REVIEW, and only a fully stated document produces a number
    /// that the rule can actually judge. This also guards the projection against turning a blank
    /// formatted value into a number.
    /// </summary>
    [Fact]
    public void Section_margin_consensus_distinguishes_missing_partial_mixed_and_complete()
    {
        const uint A4Width = 11906U, A4Height = 16838U;
        const string MarginRightRule = "ND30.PL1.I.GENERAL.MARGIN_RIGHT";

        // 1. Complete and stated: right margin 15mm on both sections, A4 page size.
        var complete = MultiSectionDocx(
            (1134, 850, 1134, 1701, A4Width, A4Height),
            (1134, 850, 1134, 1701, A4Width, A4Height));
        var completeContext = BuildFormattingContext(complete, "quyet_dinh");
        Assert.True(completeContext.ObservedValues.ContainsKey("section.margin_right_mm"));
        Assert.Equal(15.0d, ObservedSectionMillimetres(complete, "quyet_dinh", "section.margin_right_mm"), 1);
        Assert.Equal(15.0d, ObservedSectionMillimetres(complete, "quyet_dinh", "section.margin_right_mm.all"), 1);
        // Per-section projections must carry the same real number, not a blank.
        Assert.Equal(15.0d, ObservedSectionMillimetres(complete, "quyet_dinh", "section.margin_right_mm.s1"), 1);
        // The rule must judge the real number rather than its presence: compare the observed
        // value against the rule's own declared range instead of assuming which side it is on.
        var rightRule = RuleCatalog.LoadVerifiedRelease(FindRoot()).Rules.First(r => r.Id == MarginRightRule);
        var bounds = NumericBounds(rightRule);
        var observedRight = ObservedSectionMillimetres(complete, "quyet_dinh", "section.margin_right_mm");
        var expectedStatus = observedRight >= bounds.min && observedRight <= bounds.max
            ? ValidationStatus.PASS
            : ValidationStatus.FAIL;
        Assert.Equal(expectedStatus, StatusOf(complete, "quyet_dinh", MarginRightRule));

        // 2. Nothing stated at all: no observation, and no exception from parsing a blank.
        var allMissing = MultiSectionDocx(
            (null, null, null, null, null, null),
            (null, null, null, null, null, null));
        var missingContext = BuildFormattingContext(allMissing, "quyet_dinh");
        Assert.False(missingContext.ObservedValues.ContainsKey("section.margin_right_mm"));
        Assert.Equal(ValidationStatus.NOT_EVALUATED, StatusOf(allMissing, "quyet_dinh", MarginRightRule));

        // 3. Only one of two sections states it: unknown, not the single known value.
        var partial = MultiSectionDocx(
            (1134, 850, 1134, 1701, A4Width, A4Height),
            (1134, null, 1134, 1701, A4Width, A4Height));
        Assert.False(BuildFormattingContext(partial, "quyet_dinh").ObservedValues.ContainsKey("section.margin_right_mm"));
        Assert.Equal(ValidationStatus.NEEDS_REVIEW, StatusOf(partial, "quyet_dinh", MarginRightRule));

        // 4. Both state it but disagree: unknown, not one of the two values.
        var mixed = MultiSectionDocx(
            (1134, 850, 1134, 1701, A4Width, A4Height),
            (1134, 2268, 1134, 1701, A4Width, A4Height));
        Assert.False(BuildFormattingContext(mixed, "quyet_dinh").ObservedValues.ContainsKey("section.margin_right_mm"));
        Assert.Equal(ValidationStatus.NEEDS_REVIEW, StatusOf(mixed, "quyet_dinh", MarginRightRule));

        // 5. Page size stated but not A4 must fail on a real comparison, and a missing page size
        // must stay missing rather than becoming the literal string "unknown".
        var notA4 = MultiSectionDocx((1134, 850, 1134, 1701, 12240U, 15840U));
        Assert.NotEqual("A4", BuildFormattingContext(notA4, "quyet_dinh").ObservedValues["section.page_size"]);
        Assert.Equal(ValidationStatus.FAIL, StatusOf(notA4, "quyet_dinh", "ND30.PL1.I.GENERAL.PAGE_SIZE_A4"));

        var noPageSize = MultiSectionDocx((1134, 850, 1134, 1701, null, null));
        Assert.False(BuildFormattingContext(noPageSize, "quyet_dinh").ObservedValues.ContainsKey("section.page_size"));
        Assert.Equal(ValidationStatus.NOT_EVALUATED, StatusOf(noPageSize, "quyet_dinh", "ND30.PL1.I.GENERAL.PAGE_SIZE_A4"));
    }

    /// <summary>
    /// Colour consensus must be complete across runs. Every run explicitly black publishes
    /// "black"; a known non-black run produces a real failing comparison; runs whose colour is
    /// unresolved (automatic or theme-derived) never contribute a value, so a document made only
    /// of such runs publishes nothing, and a document mixing known and unknown is uncertain
    /// instead of falsely passing on the known runs alone.
    /// </summary>
    [Fact]
    public void Font_color_consensus_requires_every_run_to_be_known()
    {
        const string ColorRule = "ND30.PL1.I.GENERAL.FONT_COLOR";
        const string ColorKey = "document.effective_font_color";

        static byte[] DocxWithColors(params string[] colors)
        {
            using var stream = new MemoryStream();
            using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var main = document.AddMainDocumentPart();
                main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
                var body = main.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
                foreach (var color in colors)
                {
                    var runProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                        new DocumentFormat.OpenXml.Wordprocessing.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                        new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "24" });
                    if (color.Length > 0)
                        runProperties.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Color { Val = color });
                    body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                        new DocumentFormat.OpenXml.Wordprocessing.Run(runProperties,
                            new DocumentFormat.OpenXml.Wordprocessing.Text("Body text"))));
                }
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.SectionProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.PageSize { Width = 11906U, Height = 16838U },
                    new DocumentFormat.OpenXml.Wordprocessing.PageMargin { Top = 1134, Right = 1134U, Bottom = 1134, Left = 1701U }));
                main.Document.Save();
            }
            return stream.ToArray();
        }

        // All runs explicitly black -> observed as the word the rule expects, rule passes.
        var allBlack = DocxWithColors("000000", "000000");
        Assert.Equal("black", BuildFormattingContext(allBlack, "quyet_dinh").ObservedValues[ColorKey]);
        Assert.Equal(ValidationStatus.PASS, StatusOf(allBlack, "quyet_dinh", ColorRule));

        // A known non-black run is a real failure, not a normalization to black.
        var oneRed = DocxWithColors("000000", "FF0000");
        Assert.Equal(ValidationStatus.FAIL, StatusOf(oneRed, "quyet_dinh", ColorRule));

        // No run states a colour at all -> nothing is published, so nothing is claimed.
        var noColors = DocxWithColors("", "");
        Assert.False(BuildFormattingContext(noColors, "quyet_dinh").ObservedValues.ContainsKey(ColorKey));

        // Explicitly automatic is unresolved, not black: a document of only these publishes nothing.
        var allAuto = DocxWithColors("auto", "auto");
        Assert.False(BuildFormattingContext(allAuto, "quyet_dinh").ObservedValues.ContainsKey(ColorKey));

        // Known plus unresolved is uncertain, not a pass built from the known runs alone.
        var partial = DocxWithColors("000000", "auto");
        var partialContext = BuildFormattingContext(partial, "quyet_dinh");
        Assert.False(partialContext.ObservedValues.ContainsKey(ColorKey));
        Assert.Equal(ValidationStatus.NEEDS_REVIEW, StatusOf(partial, "quyet_dinh", ColorRule));
    }

    static byte[] ColourRunsDocx(params (string? Val, string? Theme)[] runs)
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = main.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            foreach (var (val, theme) in runs)
            {
                var color = new DocumentFormat.OpenXml.Wordprocessing.Color();
                if (val is not null) color.Val = val;
                if (theme is not null) color.ThemeColor = DocumentFormat.OpenXml.Wordprocessing.ThemeColorValues.Accent1;

                var runProperties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                    new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "24" });
                runProperties.AppendChild(color);

                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                    new DocumentFormat.OpenXml.Wordprocessing.Run(runProperties,
                        new DocumentFormat.OpenXml.Wordprocessing.Text("Body text"))));
            }
            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.SectionProperties(
                new DocumentFormat.OpenXml.Wordprocessing.PageSize { Width = 11906U, Height = 16838U },
                new DocumentFormat.OpenXml.Wordprocessing.PageMargin { Top = 1134, Right = 1134U, Bottom = 1134, Left = 1701U }));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Word writes a literal Val even when the real colour comes from the theme, and that Val is
    /// very often 000000. Reading it as a definite colour made a themed run pass the ND30 black
    /// rule. A theme-resolved colour must stay unresolved, must not be replaced by an inherited
    /// value, and must make the document either unevaluated (nothing known) or uncertain
    /// (something known alongside something unknown).
    /// </summary>
    [Fact]
    public void Theme_resolved_font_colour_is_never_reported_as_black()
    {
        const string ColorRule = "ND30.PL1.I.GENERAL.FONT_COLOR";
        const string ColorKey = "document.effective_font_color";

        // Every run resolves through the theme: the cached 000000 must not become a verdict.
        var allTheme = ColourRunsDocx(("000000", "accent1"), ("000000", "accent1"));
        var allThemeContext = BuildFormattingContext(allTheme, "quyet_dinh");
        Assert.False(allThemeContext.ObservedValues.ContainsKey(ColorKey));
        Assert.Equal(ValidationStatus.NOT_EVALUATED, StatusOf(allTheme, "quyet_dinh", ColorRule));

        // Known black plus a themed run is uncertain, not a pass built from the known run.
        var mixedTheme = ColourRunsDocx(("000000", null), ("000000", "accent1"));
        Assert.False(BuildFormattingContext(mixedTheme, "quyet_dinh").ObservedValues.ContainsKey(ColorKey));
        Assert.Equal(ValidationStatus.NEEDS_REVIEW, StatusOf(mixedTheme, "quyet_dinh", ColorRule));

        // Explicit black with no theme attribute is still a genuine, resolvable black.
        var explicitBlack = ColourRunsDocx(("000000", null), ("000000", null));
        Assert.Equal("black", BuildFormattingContext(explicitBlack, "quyet_dinh").ObservedValues[ColorKey]);
        Assert.Equal(ValidationStatus.PASS, StatusOf(explicitBlack, "quyet_dinh", ColorRule));
    }

    /// <summary>
    /// US Letter is 12240 x 15840 twips. The previous constants described a different sheet, so a
    /// genuine Letter document was labelled custom and the page-size rule failed on a real
    /// document for the wrong reason.
    /// </summary>
    [Fact]
    public void Us_letter_page_size_is_recognised()
    {
        var letter = MultiSectionDocx((1134, 1134, 1134, 1701, 12240U, 15840U));
        Assert.Equal("Letter", BuildFormattingContext(letter, "quyet_dinh").ObservedValues["section.page_size"]);
        Assert.Equal(ValidationStatus.FAIL, StatusOf(letter, "quyet_dinh", "ND30.PL1.I.GENERAL.PAGE_SIZE_A4"));

        var a4 = MultiSectionDocx((1134, 1134, 1134, 1701, 11906U, 16838U));
        Assert.Equal("A4", BuildFormattingContext(a4, "quyet_dinh").ObservedValues["section.page_size"]);
        Assert.Equal(ValidationStatus.PASS, StatusOf(a4, "quyet_dinh", "ND30.PL1.I.GENERAL.PAGE_SIZE_A4"));
    }

    /// <summary>
    /// Builds a validation context with a caller-declared semantic model, so role certainty is
    /// stated explicitly by the test instead of being faked through detector confidence. The
    /// document itself is still produced by the real parser.
    /// </summary>
    static (ValidationContext Context, DocumentModel Doc) ContextWithSemantic(
        byte[] bytes, Nd30.SemanticDetector.Model.SemanticDocumentModel semantic)
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var documentType = profile.FindDocumentType("quyet_dinh");
        Assert.NotNull(documentType);

        using var snapshot = Nd30.DocumentEngine.Safety.SafeDocxSnapshot
            .CreateAsync(new MemoryStream(bytes, writable: false), CancellationToken.None).GetAwaiter().GetResult();
        var parsed = new DocxParser().Parse(snapshot);
        Assert.NotNull(parsed.Document);

        var context = FormattingObservationAdapter.BuildContext(
            parsed.Document!, semantic, documentType!, profile, new DateOnly(2026, 9, 19));
        return (context, parsed.Document!);
    }

    static Nd30.SemanticDetector.Model.SemanticComponentCandidate Component(
        Nd30.SemanticDetector.Model.SemanticRole role, string paragraphId, Nd30.SemanticDetector.Model.ClassificationStatus status = Nd30.SemanticDetector.Model.ClassificationStatus.CONFIRMED,
        double confidence = 1.0d, IReadOnlyList<string>? competing = null, string sourceKind = "body") =>
        new(
            Id: role + ":" + paragraphId,
            Role: role,
            Text: string.Empty,
            Confidence: confidence,
            Method: Nd30.SemanticDetector.Model.ClassificationMethod.Deterministic,
            Status: status,
            Evidence: new Nd30.SemanticDetector.Model.SemanticSourceEvidence(sourceKind, paragraphId, Array.Empty<string>(), 0, string.Empty, Array.Empty<string>()),
            CompetingCandidateIds: competing ?? Array.Empty<string>());

    static Nd30.SemanticDetector.Model.SemanticDocumentModel SemanticOf(
        IReadOnlyList<Nd30.SemanticDetector.Model.SemanticComponentCandidate> components,
        IReadOnlyList<Nd30.SemanticDetector.Model.SemanticAmbiguity>? ambiguities = null) =>
        new(
            DocumentId: "test",
            Classification: new Nd30.SemanticDetector.Model.DocumentClassificationResult(Nd30.SemanticDetector.Model.AdministrativeDocumentClass.NamedAdministrativeDocument, "quyet_dinh", 1.0d, Nd30.SemanticDetector.Model.ClassificationStatus.CONFIRMED, false, Array.Empty<string>(), Array.Empty<string>()),
            TemplateMatch: new Nd30.SemanticDetector.Model.TemplateMatchResult(null, 0.0d, Nd30.SemanticDetector.Model.ClassificationStatus.UNKNOWN, false, Array.Empty<string>(), Array.Empty<string>()),
            Components: components,
            Ambiguities: ambiguities ?? Array.Empty<Nd30.SemanticDetector.Model.SemanticAmbiguity>(),
            RequiresReview: false,
            Diagnostics: Array.Empty<string>());

    static void AssertObserved(ValidationContext context, string key, string expected)
    {
        Assert.True(context.ObservedValues.ContainsKey(key), $"expected an observation for {key}");
        Assert.Equal(expected.ToLowerInvariant(), context.ObservedValues[key]?.ToString()?.ToLowerInvariant());
        Assert.DoesNotContain(key, context.UncertainTargets);
    }

    static void AssertUncertain(ValidationContext context, string key)
    {
        Assert.Contains(key, context.UncertainTargets);
        Assert.False(context.ObservedValues.ContainsKey(key), $"{key} must not carry a value while uncertain");
    }

    static void AssertNothing(ValidationContext context, string key)
    {
        Assert.False(context.ObservedValues.ContainsKey(key), $"{key} must not be published");
        Assert.DoesNotContain(key, context.UncertainTargets);
    }

    /// <summary>
    /// Stage A: non-body role formatting must be derived from every run of every paragraph the
    /// detector assigned to the role, never from a single representative run, and the key shape
    /// must be the snake_case role the release-pack rules actually target.
    /// </summary>
    [Fact]
    public void Stage_a_role_formatting_consensus_uses_every_run_of_every_role_paragraph()
    {
        const string HeadingSize = "semantic_component.document_type_heading.font_size_pt";
        const string HeadingBold = "semantic_component.document_type_heading.bold";
        const string BasisSize = "semantic_component.legal_basis_block.font_size_pt";

        // Two body paragraphs, each with two runs, plus a table cell paragraph.
        var bytes = RoleFormattingDocx();
        var parsedOnce = BuildFormattingContext(bytes, "quyet_dinh");
        Assert.NotNull(parsedOnce);

        // --- homogeneous heading: every run agrees, so the value is observed ---
        var homogeneous = SemanticOf(new[]
        {
            Component(Nd30.SemanticDetector.Model.SemanticRole.DocumentTypeHeading, "p1"),
        });
        var (homogeneousContext, _) = ContextWithSemantic(bytes, homogeneous);
        AssertObserved(homogeneousContext, HeadingSize, "14");
        AssertObserved(homogeneousContext, HeadingBold, "true");

        // --- mixed runs in one heading paragraph: first run right, second wrong ---
        var mixedRuns = SemanticOf(new[]
        {
            Component(Nd30.SemanticDetector.Model.SemanticRole.DocumentTypeHeading, "p2"),
        });
        var (mixedContext, _) = ContextWithSemantic(bytes, mixedRuns);
        AssertUncertain(mixedContext, HeadingSize);

        // --- two legitimate same-role paragraphs, equal formatting: consensus regardless of order ---
        var equalPair = new[] { Component(Nd30.SemanticDetector.Model.SemanticRole.LegalBasisBlock, "p3"), Component(Nd30.SemanticDetector.Model.SemanticRole.LegalBasisBlock, "p4") };
        foreach (var ordering in new[] { equalPair, equalPair.Reverse().ToArray() })
        {
            var (context, _) = ContextWithSemantic(bytes, SemanticOf(ordering));
            AssertObserved(context, BasisSize, "12");
        }

        // --- two same-role paragraphs that disagree: uncertain, and order must not decide it ---
        var disagreeing = SemanticOf(new[]
        {
            Component(Nd30.SemanticDetector.Model.SemanticRole.LegalBasisBlock, "p3"),
            new Nd30.SemanticDetector.Model.SemanticComponentCandidate(
                "legal:p5", Nd30.SemanticDetector.Model.SemanticRole.LegalBasisBlock, string.Empty, 1.0d, Nd30.SemanticDetector.Model.ClassificationMethod.Deterministic,
                Nd30.SemanticDetector.Model.ClassificationStatus.CONFIRMED,
                new Nd30.SemanticDetector.Model.SemanticSourceEvidence("body", "p5", Array.Empty<string>(), 1, string.Empty, Array.Empty<string>()),
                Array.Empty<string>()),
        });
        var (disagree, _) = ContextWithSemantic(bytes, disagreeing);
        AssertUncertain(disagree, BasisSize);

        // --- unknown everywhere: nothing published, nothing certain ---
        var unknown = SemanticOf(new[] { Component(Nd30.SemanticDetector.Model.SemanticRole.IssuePlaceAndDate, "p6") });
        var (unknownContext, _) = ContextWithSemantic(bytes, unknown);
        AssertNothing(unknownContext, "semantic_component.issue_place_and_date.font_size_pt");

        // --- low confidence, a competing candidate, or an explicit ambiguity each force review ---
        var lowConfidence = SemanticOf(new[]
        {
            Component(Nd30.SemanticDetector.Model.SemanticRole.SubjectNamedDocument, "p1", Nd30.SemanticDetector.Model.ClassificationStatus.INFERRED_LOW, 0.4d),
        });
        AssertUncertain(ContextWithSemantic(bytes, lowConfidence).Context, "semantic_component.subject_named_document.font_size_pt");

        var competing = SemanticOf(new[]
        {
            Component(Nd30.SemanticDetector.Model.SemanticRole.SubjectNamedDocument, "p1", Nd30.SemanticDetector.Model.ClassificationStatus.INFERRED_HIGH, 0.9d, new[] { "other" }),
        });
        AssertUncertain(ContextWithSemantic(bytes, competing).Context, "semantic_component.subject_named_document.font_size_pt");

        var declaredAmbiguity = SemanticOf(
            new[] { Component(Nd30.SemanticDetector.Model.SemanticRole.RecipientList, "p7") },
            new[] { new Nd30.SemanticDetector.Model.SemanticAmbiguity("TWO_CANDIDATES", "ambiguous", new[] { "RecipientList:p7" }) });
        AssertUncertain(ContextWithSemantic(bytes, declaredAmbiguity).Context, "semantic_component.recipient_list_label.font_size_pt");

        // --- a role paragraph inside a table cell must be found by exact id ---
        // The id is read from the parsed document instead of assumed, so this cannot silently
        // bind to a different paragraph if the parser numbers them differently.
        var (_, parsedForTable) = ContextWithSemantic(
            bytes, SemanticOf(Array.Empty<Nd30.SemanticDetector.Model.SemanticComponentCandidate>()));
        var cellParagraph = parsedForTable.Tables
            .SelectMany(table => table.Rows)
            .SelectMany(row => row.Cells)
            .SelectMany(cell => cell.Paragraphs)
            .Select(paragraph => paragraph.Id)
            .FirstOrDefault();
        Assert.False(string.IsNullOrEmpty(cellParagraph), "fixture must contain a table-cell paragraph");

        var inTable = SemanticOf(new[]
        {
            Component(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, cellParagraph!, sourceKind: "table_cell"),
        });
        var (tableContext, _) = ContextWithSemantic(bytes, inTable);
        AssertObserved(tableContext, "semantic_component.national_header.font_size_pt", "10");
    }

    /// <summary>
    /// Synthetic document for the stage A consensus test: numbered paragraphs whose runs carry
    /// explicit sizes, plus one paragraph inside a table cell (p8).
    /// </summary>
    static byte[] RoleFormattingDocx()
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = main.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());

            // p1 homogeneous heading (28 half-points = 14pt, bold), p2 mixed runs (28 then 24).
            body.AppendChild(RoleParagraph("28", "28", bold: true));
            body.AppendChild(RoleParagraph("28", "24", bold: true));
            // p3 and p4 agree (24); p5 disagrees (36).
            body.AppendChild(RoleParagraph("24"));
            body.AppendChild(RoleParagraph("24"));
            body.AppendChild(RoleParagraph("36"));
            // p6 states no direct size at all.
            body.AppendChild(RoleParagraph(null));
            body.AppendChild(RoleParagraph("24"));

            var table = new DocumentFormat.OpenXml.Wordprocessing.Table();
            var row = new DocumentFormat.OpenXml.Wordprocessing.TableRow();
            var cell = new DocumentFormat.OpenXml.Wordprocessing.TableCell();
            cell.AppendChild(RoleParagraph("20"));
            row.AppendChild(cell);
            table.AppendChild(row);
            body.AppendChild(table);

            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                new DocumentFormat.OpenXml.Wordprocessing.Run(
                    new DocumentFormat.OpenXml.Wordprocessing.RunProperties(new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "24" }),
                    new DocumentFormat.OpenXml.Wordprocessing.Text("Signing"))));
            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.SectionProperties(
                new DocumentFormat.OpenXml.Wordprocessing.PageSize { Width = 11906U, Height = 16838U },
                new DocumentFormat.OpenXml.Wordprocessing.PageMargin { Top = 1134, Right = 1134U, Bottom = 1134, Left = 1701U }));
            main.Document.Save();
        }
        return stream.ToArray();

        static DocumentFormat.OpenXml.Wordprocessing.Paragraph RoleParagraph(string? first, string? second = null, bool bold = false)
        {
            var paragraph = new DocumentFormat.OpenXml.Wordprocessing.Paragraph();
            foreach (var size in new[] { first, second })
            {
                if (size is null) continue;
                var properties = new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                    new DocumentFormat.OpenXml.Wordprocessing.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                    new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = size });
                if (bold) properties.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Bold());
                paragraph.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Run(properties,
                    new DocumentFormat.OpenXml.Wordprocessing.Text("Nội dung")));
            }
            return paragraph;
        }
    }

    /// <summary>Simple body paragraphs used by the composite line-spacing tests.</summary>
    static byte[] PlainParagraphsDocx(int count)
    {
        using var stream = new MemoryStream();
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new DocumentFormat.OpenXml.Wordprocessing.Document();
            var body = main.Document.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Body());
            for (var i = 0; i < count; i++)
            {
                body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Paragraph(
                    new DocumentFormat.OpenXml.Wordprocessing.Run(
                        new DocumentFormat.OpenXml.Wordprocessing.RunProperties(
                            new DocumentFormat.OpenXml.Wordprocessing.RunFonts { Ascii = "Times New Roman", HighAnsi = "Times New Roman" },
                            new DocumentFormat.OpenXml.Wordprocessing.FontSize { Val = "24" }),
                        new DocumentFormat.OpenXml.Wordprocessing.Text("Nội dung"))));
            }
            body.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.SectionProperties(
                new DocumentFormat.OpenXml.Wordprocessing.PageSize { Width = 11906U, Height = 16838U },
                new DocumentFormat.OpenXml.Wordprocessing.PageMargin { Top = 1134, Right = 1134U, Bottom = 1134, Left = 1701U }));
            main.Document.Save();
        }
        return stream.ToArray();
    }

    /// <summary>
    /// Stage B: the two composite block rules target keys built from two roles each. The producer
    /// must gather both roles' paragraphs, accept only an unambiguous line multiple, and never turn
    /// an absolute spacing into a line verdict.
    /// </summary>
    [Fact]
    public void Stage_b_composite_role_line_producers_are_unit_safe()
    {
        const string MottoBlock = "semantic_component.national_header_motto_block.line_spacing";
        const string IssuerBlock = "semantic_component.issuer_block.line_spacing";
        var Auto = DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto;
        var Exact = DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Exact;

        var baseDoc = PlainParagraphsDocx(4);

        Nd30.SemanticDetector.Model.SemanticComponentCandidate Part(
            Nd30.SemanticDetector.Model.SemanticRole role, string id) => Component(role, id);

        // Both roles state the same line multiple: the composite value is unambiguous.
        var bothAuto = SetLineSpacing(SetLineSpacing(baseDoc, 0, Auto, 360), 1, Auto, 360);
        var (mottoOk, _) = ContextWithSemantic(bothAuto, SemanticOf(new[] { Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1"), Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2") }));
        AssertObserved(mottoOk, MottoBlock, "1.5");

        // The two roles disagree: uncertain, not one of the two values.
        var disagreeing = SetLineSpacing(SetLineSpacing(baseDoc, 0, Auto, 360), 1, Auto, 480);
        var (mottoMixed, _) = ContextWithSemantic(disagreeing, SemanticOf(new[] { Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1"), Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2") }));
        AssertUncertain(mottoMixed, MottoBlock);

        // Mixed units: an absolute spacing is never folded into a line verdict.
        var mixedUnits = SetLineSpacing(SetLineSpacing(baseDoc, 0, Auto, 360), 1, Exact, 240);
        var (mottoUnits, _) = ContextWithSemantic(mixedUnits, SemanticOf(new[] { Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1"), Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2") }));
        AssertUncertain(mottoUnits, MottoBlock);

        // Every part absolute: no line multiple is stated, so no line verdict at all.
        var allExact = SetLineSpacing(SetLineSpacing(baseDoc, 0, Exact, 240), 1, Exact, 240);
        var (mottoNone, _) = ContextWithSemantic(allExact, SemanticOf(new[] { Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1"), Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2") }));
        AssertNothing(mottoNone, MottoBlock);

        // A required role is absent: the composite cannot be formed, so it is uncertain.
        var missingRole = SetLineSpacing(baseDoc, 0, Auto, 360);
        var (mottoMissing, _) = ContextWithSemantic(missingRole, SemanticOf(new[] { Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1") }));
        AssertUncertain(mottoMissing, MottoBlock);

        // The issuer composite works the same way.
        var issuerBoth = SetLineSpacing(SetLineSpacing(baseDoc, 2, Auto, 360), 3, Auto, 360);
        var (issuerOk, _) = ContextWithSemantic(issuerBoth, SemanticOf(new[] { Part(Nd30.SemanticDetector.Model.SemanticRole.IssuingAuthorityParent, "p3"), Part(Nd30.SemanticDetector.Model.SemanticRole.IssuingAuthority, "p4") }));
        AssertObserved(issuerOk, IssuerBlock, "1.5");
    }

    /// <summary>
    /// Stage C: the detector's RecipientList component is the lexical label paragraph, not the
    /// list itself. Its formatting belongs to the label role, and must stop being judged as list
    /// formatting. The real list is not collected here, so it stays honestly unobserved.
    /// </summary>
    [Fact]
    public void Stage_c_recipient_label_is_observed_as_its_own_role()
    {
        const string LabelSize = "semantic_component.recipient_list_label.font_size_pt";
        const string ListSize = "semantic_component.recipient_list.font_size_pt";

        var bytes = PlainParagraphsDocx(2);
        var label = Component(Nd30.SemanticDetector.Model.SemanticRole.RecipientList, "p1");

        var (context, _) = ContextWithSemantic(bytes, SemanticOf(new[] { label }));

        // The label paragraph is observed under the label role.
        AssertObserved(context, LabelSize, "12");

        // It is no longer judged as list formatting, so the list rule has nothing to judge.
        AssertNothing(context, ListSize);

        // And no following paragraph is invented to stand in for the list.
        AssertNothing(context, "semantic_component.recipient_list.alignment");
    }

    /// <summary>
    /// Stage B fail-closed gate. Two ways the composite block could previously publish a verdict it
    /// had no right to publish:
    ///
    /// 1. it ignored the same uncertainty signals the single-role consensus honours, so two
    ///    uncertain role labels that happened to agree produced a block verdict;
    /// 2. it filtered blank paragraph ids out, so a role with one usable candidate and one
    ///    candidate with no id still counted as satisfied and published a partial consensus.
    ///
    /// Both must be uncertain instead.
    /// </summary>
    [Fact]
    public void Stage_b_composite_block_fails_closed_on_uncertain_evidence()
    {
        const string MottoBlock = "semantic_component.national_header_motto_block.line_spacing";
        var Auto = DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto;

        var baseDoc = PlainParagraphsDocx(4);
        var bothAuto = SetLineSpacing(SetLineSpacing(baseDoc, 0, Auto, 360), 1, Auto, 360);

        Nd30.SemanticDetector.Model.SemanticComponentCandidate Part(
            Nd30.SemanticDetector.Model.SemanticRole role, string id,
            Nd30.SemanticDetector.Model.ClassificationStatus status =
                Nd30.SemanticDetector.Model.ClassificationStatus.CONFIRMED,
            double confidence = 1.0d,
            IReadOnlyList<string>? competing = null) => Component(role, id, status, confidence, competing);

        // Agreeing values, but one role label is only low-confidence inference.
        var lowConfidence = SemanticOf(new[]
        {
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1",
                Nd30.SemanticDetector.Model.ClassificationStatus.INFERRED_LOW, 0.4d),
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2"),
        });
        AssertUncertain(ContextWithSemantic(bothAuto, lowConfidence).Context, MottoBlock);

        // Agreeing values, but one candidate has a competing alternative.
        var competing = SemanticOf(new[]
        {
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1",
                Nd30.SemanticDetector.Model.ClassificationStatus.INFERRED_HIGH, 0.9d, new[] { "other" }),
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2"),
        });
        AssertUncertain(ContextWithSemantic(bothAuto, competing).Context, MottoBlock);

        // Agreeing values, but an ambiguity is explicitly declared over one of the candidates.
        var declared = SemanticOf(
            new[]
            {
                Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1"),
                Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2"),
            },
            new[]
            {
                new Nd30.SemanticDetector.Model.SemanticAmbiguity(
                    "TWO_CANDIDATES", "ambiguous header or motto",
                    new[] { "NationalHeader:p1" }),
            });
        AssertUncertain(ContextWithSemantic(bothAuto, declared).Context, MottoBlock);

        // A role that has one usable candidate and one candidate with no paragraph id is only
        // partially identified, so it cannot satisfy the block.
        var partialIds = SemanticOf(new[]
        {
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, "p1"),
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalHeader, string.Empty),
            Part(Nd30.SemanticDetector.Model.SemanticRole.NationalMotto, "p2"),
        });
        AssertUncertain(ContextWithSemantic(bothAuto, partialIds).Context, MottoBlock);
    }

    static byte[] AddDirectAutoLineSpacing(byte[] source, int paragraphIndex, int lineTwips)
    {
        using var stream = new MemoryStream();
        stream.Write(source, 0, source.Length);
        stream.Position = 0;

        // An explicit `using` block, not a `using var` declaration: the return value is
        // evaluated before a `using var` is disposed, so the package would still be open
        // when the bytes are captured and the archive would never be finalized.
        string selectedText;
        using (var document = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(stream, true))
        {
            var body = document.MainDocumentPart!.Document.Body!;
            // Direct children of the body in document order, matching how the parser
            // numbers paragraphs.
            var target = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .ElementAtOrDefault(paragraphIndex);
            Assert.NotNull(target);
            selectedText = string.Concat(target!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));

            var properties = target.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties>();
            if (properties is null)
            {
                properties = new DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties();
                target.InsertAt(properties, 0);
            }
            properties.RemoveAllChildren<DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines>();
            properties.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines
            {
                Line = lineTwips.ToString(CultureInfo.InvariantCulture),
                LineRule = DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto,
            });
            document.MainDocumentPart.Document.Save();
        }
        // The package is closed here, so the central directory is written.

        var result = stream.ToArray();

        // Verify the produced bytes independently by reopening read-only, so a
        // finalized-but-wrong document cannot be mistaken for a good one.
        using (var verifyStream = new MemoryStream(result, writable: false))
        using (var reopened = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(verifyStream, false))
        {
            var body = reopened.MainDocumentPart!.Document.Body!;
            var verified = body.Elements<DocumentFormat.OpenXml.Wordprocessing.Paragraph>()
                .ElementAtOrDefault(paragraphIndex);
            Assert.NotNull(verified);
            var verifiedText = string.Concat(verified!.Descendants<DocumentFormat.OpenXml.Wordprocessing.Text>().Select(t => t.Text));
            Assert.Equal(selectedText, verifiedText);
            var spacing = verified.GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.ParagraphProperties>()?
                .GetFirstChild<DocumentFormat.OpenXml.Wordprocessing.SpacingBetweenLines>();
            Assert.NotNull(spacing);
            Assert.Equal(lineTwips.ToString(CultureInfo.InvariantCulture), spacing!.Line?.Value);
            Assert.Equal(DocumentFormat.OpenXml.Wordprocessing.LineSpacingRuleValues.Auto, spacing.LineRule?.Value);
        }

        return result;
    }

    /// <summary>
    /// The host <c>/capabilities</c> metadata must describe what this build can really do.
    /// It once advertised <c>paragraph.alignment.direct</c>, a pre-profile spike name that
    /// is not an operation and not a profile target, so callers reading metadata saw an
    /// apply contract that did not exist. Source-level because the Host assembly is not
    /// referenced here (a running host holds its bin locked).
    /// </summary>
    [Fact]
    public void Host_capabilities_metadata_cannot_advertise_a_property_that_is_not_an_operation()
    {
        var program = File.ReadAllText(Path.Combine(FindRoot(), "src", "DocumentProcessor.Host", "Program.cs"));
        var match = System.Text.RegularExpressions.Regex.Match(
            program, @"operations\s*=\s*new\[\]\s*\{(?<body>[^}]*)\}");
        Assert.True(match.Success, "/capabilities no longer declares an operations array");

        var advertised = System.Text.RegularExpressions.Regex
            .Matches(match.Groups["body"].Value, @"[A-Za-z_][A-Za-z0-9_.]*")
            .Select(m => m.Value)
            .ToArray();

        Assert.Contains("inspect", advertised);
        Assert.Contains("apply", advertised);

        // Exact set, order-insensitive. The verb/dot checks alone would let any other
        // verb through, so pin the advertised surface to precisely these two operations.
        Assert.Equal(
            new[] { "apply", "inspect" },
            advertised.OrderBy(op => op, StringComparer.Ordinal).ToArray());

        // An operation is a verb, never a dotted property/rule id. Naming a property here
        // is how the alignment-direct mismatch reappeared.
        Assert.All(advertised, op => Assert.DoesNotContain(".", op));

        // supportedProperties is projected from the profile, so it cannot drift from it.
        Assert.Contains("CurrentProfile?.MutationTargets", program);
    }

    /// <summary>
    /// The ND30 font-family rule must stay evaluated and reported, but this build must
    /// not offer an apply for it. The observation resolves the inherited family of
    /// top-level body paragraphs only, so a document-wide rewrite could not be proven
    /// safe; advertising it previously produced an offer the server always refused.
    /// </summary>
    [Fact]
    public async Task Font_family_is_reported_but_not_offered_as_an_apply()
    {
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);

        // No mutation target, so nothing can be offered or executed for it.
        Assert.Null(profile.FindMutationTarget("document.effective_font_family"));
        Assert.DoesNotContain(profile.MutationTargets, t => t.Property == "document.effective_font_family");
        Assert.DoesNotContain(profile.MutationTargets, t => t.Scope == "document");

        // Every advertised target must carry a scope this build can actually execute,
        // and a document-scope target would need a non-empty id the webapp rejects.
        Assert.All(profile.MutationTargets, t => Assert.Contains(t.Scope, new[] { "paragraph", "section" }));

        // Validation coverage must NOT shrink: the rule is still in the scoped subset.
        foreach (var type in new[] { "cong_van", "quyet_dinh", "bao_cao", "ke_hoach", "to_trinh" })
        {
            Assert.Contains("ND30.PL1.I.GENERAL.FONT_FAMILY", profile.ResolveRuleSubset(catalog, type));
        }

        // The inspection advertises no font-family target, and any font-family finding
        // is reported as SUGGEST_ONLY rather than claiming it can be auto-applied.
        var bytes = await File.ReadAllBytesAsync(SemanticFixture("02-named-decision.docx"));
        var inspection = await Processor().InspectAsync(new MemoryStream(bytes, writable: false), CancellationToken.None);

        Assert.DoesNotContain(inspection.SupportedProperties!, p => p == "document.effective_font_family");
        Assert.All(inspection.MutableTargets!, t => Assert.NotEqual("document.effective_font_family", t.Property));
        Assert.All(inspection.MutableTargets!, t => Assert.False(string.IsNullOrEmpty(t.TargetId)));

        // Every checked-in fixture already satisfies the ND30 font rule, so inspect a copy
        // whose default font genuinely violates it. Otherwise the rule passes, no finding is
        // emitted, and the assertions below would hold vacuously.
        var nonCompliant = WithNonCompliantDefaultFont(bytes, "Arial");
        var nonCompliantInspection = await Processor().InspectAsync(new MemoryStream(nonCompliant, writable: false), CancellationToken.None);

        // A rule targets the bare property name, so match on that rather than on the
        // mutation target's scope-qualified name.
        var fontFindings = nonCompliantInspection.Findings!
            .Where(f => f.Property == "effective_font_family")
            .ToArray();

        // Prove the rule really is evaluated and reported before asserting how it is
        // classified. Without this, an empty collection would make the SUGGEST_ONLY
        // assertion below pass vacuously and hide the very coverage loss it guards.
        Assert.NotEmpty(fontFindings);
        Assert.Contains(fontFindings, f => f.RuleId == "ND30.PL1.I.GENERAL.FONT_FAMILY");

        Assert.All(fontFindings, f => Assert.Equal("SUGGEST_ONLY", f.PatchEligibility));

        // Still no apply offered, even for a document that clearly violates the rule.
        Assert.DoesNotContain(nonCompliantInspection.SupportedProperties!, p => p == "document.effective_font_family");
        Assert.DoesNotContain(nonCompliantInspection.MutableTargets!, t => t.Property == "document.effective_font_family");

        // The positive direction (a target-backed finding is never downgraded by this mapping)
        // is asserted precisely, rule by rule, in
        // Eligibility_is_bound_by_rule_id_so_target_backed_findings_are_not_downgraded. It is not
        // asserted as a blanket invariant here, because a rule whose own declared autofix policy
        // is SUGGEST_ONLY is legitimately reported that way and is not a downgrade.

        Assert.NotEmpty(inspection.Findings!);

        // Properties that DO have targets must still be genuinely applicable, and the
        // offer set must not have been emptied by dropping the unsafe font-family target.
        Assert.NotEmpty(inspection.MutableTargets!);
        Assert.All(inspection.MutableTargets!, t => Assert.True(
            t.Property is not (null or "" or "document.effective_font_family")));
    }

    [Fact]
    public void Observations_never_claim_a_value_the_resolver_could_not_produce()
    {
        var parsed = new DocxParser().Parse(Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../../fixtures/v23", "02-named-decision.docx")));
        Assert.NotNull(parsed.Document);
        var semantic = new AdministrativeSemanticDetector().Detect(parsed.Document!);
        var catalog = RuleCatalog.LoadVerifiedRelease(FindRoot());
        var profile = FormattingProfileV1.Load(catalog);
        var documentType = profile.FindDocumentType("quyet_dinh")!;

        var context = Nd30.LegalValidator.Adapters.FormattingObservationAdapter.BuildContext(
            parsed.Document!, semantic, documentType, profile, new DateOnly(2026, 9, 19));

        // Every rule in scope must be either evaluated or explicitly declared
        // unevaluable: a declared cause, or a genuinely missing observation.
        var subset = profile.ResolveRuleSubset(catalog, "quyet_dinh");
        var report = new ValidationEngine(catalog).Validate(context, subset);
        foreach (var rule in catalog.Rules.Where(rule => subset.Contains(rule.Id)))
        {
            var result = report.Results.Single(x => x.RuleId == rule.Id);
            if (result.Status == ValidationStatus.NOT_EVALUATED)
                Assert.True(
                    result.MissingCapabilities.Count > 0
                    || result.Observed == "missing observation"
                    || result.Reason.StartsWith("No authoritative observation", StringComparison.Ordinal),
                    $"{rule.Id} was not evaluated without a declared cause.");
        }
        Assert.False(report.FullComplianceClaimAllowed);
    }
}