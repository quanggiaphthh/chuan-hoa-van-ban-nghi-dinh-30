using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Package;
using Nd30.DocumentEngine.Parsing;
using Nd30.LegalValidator.Adapters;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Xunit;

namespace Nd30.LegalValidator.Tests;

public sealed class PartyObservationContextBridgeTests
{
    private static string CreateDocx(bool mixed = false, bool landscape = false)
    {
        var file = Path.Combine(Path.GetTempPath(), $"hd05-bridge-{Guid.NewGuid():N}.docx");
        using (var package = WordprocessingDocument.Create(file, WordprocessingDocumentType.Document))
        {
            var main = package.AddMainDocumentPart();
            var styles = main.AddNewPart<StyleDefinitionsPart>();
            styles.Styles = new W.Styles(
                new W.Style(new W.StyleRunProperties(
                    new W.FontSize { Val = "28" }, new W.Bold()))
                { StyleId = "Base", Type = W.StyleValues.Paragraph },
                new W.Style(new W.BasedOn { Val = "Base" })
                { StyleId = "Child", Type = W.StyleValues.Paragraph });
            styles.Styles.Save();
            var other = new W.RunProperties();
            if (mixed)
            {
                other.Append(new W.FontSize { Val = "32" });
                other.Append(new W.Bold { Val = false });
            }
            var body = new W.Body(
                new W.Paragraph(
                    new W.ParagraphProperties(new W.ParagraphStyleId { Val = "Child" }),
                    new W.Run(new W.Text("ĐẢNG ")),
                    new W.Run(other, new W.Text("CỘNG SẢN VIỆT NAM"))),
                new W.SectionProperties(
                    new W.PageSize
                    {
                        Width = landscape ? 15840u : 12240u,
                        Height = landscape ? 12240u : 15840u,
                        Orient = landscape
                            ? W.PageOrientationValues.Landscape : W.PageOrientationValues.Portrait
                    },
                    new W.PageMargin { Top = 1134, Bottom = 1134, Left = 1701, Right = 850 }));
            main.Document = new W.Document(body);
            main.Document.Save();
        }
        return file;
    }

    private static DocumentModel Parsed(string path)
    {
        var parsed = new DocxParser().Parse(path);
        Assert.NotNull(parsed.Document);
        return parsed.Document!;
    }

    private static IReadOnlyList<string> Ids(ParagraphModel paragraph) =>
        paragraph.Runs.Select(r => r.Id).ToArray();

    [Fact]
    public void Whole_paragraph_inherited_style_is_review_only_never_authoritative()
    {
        var file = CreateDocx();
        try
        {
            var before = PackagePreserver.Sha256(file);
            var doc = Parsed(file);
            var context = PartyObservationContextBridge.Create(doc, new DateOnly(2026, 10, 19));
            var paragraph = Assert.Single(doc.Paragraphs);
            var projected = PartyObservationContextBridge.ProjectRun(
                context, doc, paragraph, "heading01", Ids(paragraph),
                ObservedRunProperty.FontSizePt);

            Assert.Equal(ValidationStatus.NEEDS_REVIEW, projected.Status);
            Assert.Equal("party_candidate.heading01.font_size_pt", projected.TargetKey);
            Assert.Equal(14d, context.ObservedValues[projected.TargetKey]);
            Assert.Contains(projected.TargetKey, context.UncertainTargets);
            var ev = Assert.Single(context.Evidence[projected.TargetKey]);
            Assert.Equal(EvidenceAuthority.Derived, ev.Authority);
            Assert.True(ev.RequiresReview);
            Assert.Equal("complete_paragraph_runs_only", ev.Attributes!["source_scope"]);
            Assert.Equal(PatchPolicy.PROHIBITED, context.DocumentPatchPolicy);
            Assert.Empty(context.AvailableCapabilities);
            Assert.Empty(context.Fields);
            Assert.Equal(before, PackagePreserver.Sha256(file));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Mixed_run_formatting_is_review_not_false_legal_fail_or_pass()
    {
        var file = CreateDocx(mixed: true);
        try
        {
            var d = Parsed(file);
            var p = Assert.Single(d.Paragraphs);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            var a = PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "mixedsize", Ids(p), ObservedRunProperty.FontSizePt);
            var b = PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "mixedbold", Ids(p), ObservedRunProperty.Bold);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, a.Status);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, b.Status);
            Assert.Equal("non_uniform_effective_run_values", ctx.ObservedValues[a.TargetKey]);
            Assert.Equal("non_uniform_effective_run_values", ctx.ObservedValues[b.TargetKey]);
            Assert.All(ctx.Evidence.Values.SelectMany(x => x),
                evidence => Assert.Equal(EvidenceAuthority.Derived, evidence.Authority));
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Partial_run_provenance_never_creates_an_observed_value()
    {
        var file = CreateDocx(mixed: true);
        try
        {
            var d = Parsed(file);
            var p = Assert.Single(d.Paragraphs);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            var a = PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "partial", [p.Runs[0].Id], ObservedRunProperty.Bold);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, a.Status);
            Assert.Equal("INCOMPLETE_OR_UNORDERED_RUN_SPAN", a.Reason);
            Assert.False(ctx.ObservedValues.ContainsKey(a.TargetKey));
            Assert.Empty(ctx.AvailableCapabilities);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Paragraph_from_another_docx_is_not_evaluated()
    {
        var first = CreateDocx();
        var other = CreateDocx();
        try
        {
            var d1 = Parsed(first);
            var d2 = Parsed(other);
            var paragraph = Assert.Single(d2.Paragraphs);
            var ctx = PartyObservationContextBridge.Create(d1, new DateOnly(2026, 10, 19));
            var observation = PartyObservationContextBridge.ProjectRun(
                ctx, d1, paragraph, "foreign", Ids(paragraph), ObservedRunProperty.Bold);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, observation.Status);
            Assert.Equal("PARAGRAPH_PROVENANCE_NOT_CONFIRMED", observation.Reason);
            Assert.Empty(ctx.ObservedValues);
        }
        finally { File.Delete(first); File.Delete(other); }
    }

    [Fact]
    public void Duplicate_candidate_id_cannot_overwrite_original_evidence()
    {
        var file = CreateDocx();
        try
        {
            var d = Parsed(file);
            var p = Assert.Single(d.Paragraphs);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            var initial = PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "same", Ids(p), ObservedRunProperty.FontSizePt);
            var second = PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "same", Ids(p), ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, initial.Status);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, second.Status);
            Assert.Equal("DUPLICATE_CANDIDATE_TARGET", second.Reason);
            Assert.Equal(14d, ctx.ObservedValues[initial.TargetKey]);
            Assert.Single(ctx.Evidence[initial.TargetKey]);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Malformed_candidate_identifier_cannot_inject_validation_field_path()
    {
        var file = CreateDocx();
        try
        {
            var d = Parsed(file);
            var p = Assert.Single(d.Paragraphs);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            Assert.Throws<ArgumentException>(() => PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "semantic_component.national_header", Ids(p),
                ObservedRunProperty.Bold));
            Assert.Empty(ctx.ObservedValues);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Portrait_margin_observation_is_derived_and_requires_review()
    {
        var file = CreateDocx();
        try
        {
            var d = Parsed(file);
            var section = Assert.Single(d.Sections);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            var assessment = PartyObservationContextBridge.ProjectMargins(
                ctx, d, section, "section01", new MarginTarget(20, 20, 30, 15),
                new MarginPrintEvidence(DeclaredPrintSurface.SingleSided, true, true, true));
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, assessment.Status);
            Assert.Contains(assessment.TargetKey, ctx.UncertainTargets);
            Assert.Equal(EvidenceAuthority.Derived, Assert.Single(ctx.Evidence[assessment.TargetKey]).Authority);
            Assert.Equal(PatchPolicy.PROHIBITED, ctx.DocumentPatchPolicy);
            Assert.Empty(ctx.AvailableCapabilities);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Duplex_back_and_unknown_intent_have_no_authoritative_margin_observation()
    {
        var file = CreateDocx();
        try
        {
            var d = Parsed(file);
            var section = Assert.Single(d.Sections);
            foreach (var surface in new[] { DeclaredPrintSurface.DuplexBack, DeclaredPrintSurface.Unknown })
            {
                var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
                var assessment = PartyObservationContextBridge.ProjectMargins(
                    ctx, d, section, "backside", new MarginTarget(20, 20, 15, 30),
                    new MarginPrintEvidence(surface, true, true, true));
                Assert.Equal(ValidationStatus.NOT_EVALUATED, assessment.Status);
                Assert.Empty(ctx.ObservedValues);
                Assert.Empty(ctx.AvailableCapabilities);
            }
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void Foreign_section_and_landscape_remain_not_evaluated()
    {
        var file = CreateDocx(landscape: true);
        var foreignFile = CreateDocx();
        try
        {
            var d = Parsed(file);
            var foreign = Parsed(foreignFile);
            var section = Assert.Single(d.Sections);
            var target = new MarginTarget(20, 20, 30, 15);
            var facts = new MarginPrintEvidence(DeclaredPrintSurface.DuplexFront, true, true, true);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            var wrong = PartyObservationContextBridge.ProjectMargins(
                ctx, d, Assert.Single(foreign.Sections), "wrongowner", target, facts);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, wrong.Status);
            Assert.Equal("SECTION_PROVENANCE_NOT_CONFIRMED", wrong.Reason);
            var landscape = PartyObservationContextBridge.ProjectMargins(
                ctx, d, section, "landscape", target, facts);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, landscape.Status);
            Assert.Equal("NON_PORTRAIT_SECTION", landscape.Reason);
            Assert.Empty(ctx.ObservedValues);
        }
        finally { File.Delete(file); File.Delete(foreignFile); }
    }

    [Fact]
    public void Existing_rule_evaluator_cannot_promote_candidate_evidence_to_pass()
    {
        var file = CreateDocx();
        try
        {
            var d = Parsed(file);
            var p = Assert.Single(d.Paragraphs);
            var ctx = PartyObservationContextBridge.Create(d, new DateOnly(2026, 10, 19));
            var observation = PartyObservationContextBridge.ProjectRun(
                ctx, d, p, "candidate01", Ids(p), ObservedRunProperty.FontSizePt);
            var target = new RuleTarget("party_candidate", "candidate01", "font_size_pt");
            var constraint = new Dictionary<string, object?>
            {
                ["type"] = "equals",
                ["value"] = 14d
            };
            // Synthetic unit-only rule; not registered or loaded in RuleCatalog.
            RuleDefinition Rule(string maturity) => new(
                "UNIT_TEST_ONLY_NOT_A_CANONICAL_PARTY_RULE", "party", "document_format",
                maturity, new RuleSource("TEST_NOT_A_LEGAL_SOURCE", "unit-test"),
                null, null, target, constraint, null,
                "LEGAL_WARNING", "PROHIBITED", ["independent_party_legal_verification"]);
            var evaluator = new RuleEvaluator();
            Assert.Equal(ValidationStatus.NOT_EVALUATED,
                evaluator.Evaluate(Rule("extracted"), ctx).Status);
            Assert.Equal(ValidationStatus.NOT_EVALUATED,
                evaluator.Evaluate(Rule("verified"), ctx).Status);
            ctx.AddCapability("independent_party_legal_verification");
            var reviewed = evaluator.Evaluate(Rule("verified"), ctx);
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, reviewed.Status);
            Assert.NotEqual(PatchEligibility.ELIGIBLE, reviewed.PatchEligibility);
            Assert.True(observation.RequiresHumanReview);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void V23_legacy_bridge_rejects_self_consistent_foreign_document_paragraph()
    {
        var a = CreateDocx();
        var b = CreateDocx();
        try
        {
            var docA = Parsed(a);
            var docB = Parsed(b);
            var paragraphB = Assert.Single(docB.Paragraphs);
            var context = PartyObservationContextBridge.Create(docA, new DateOnly(2026, 10, 21));
            var result = PartyObservationContextBridge.ProjectRun(
                context, docB, paragraphB, "crossdoc",
                Ids(paragraphB), ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, result.Status);
            Assert.Equal("DOCUMENT_SESSION_MISMATCH", result.Reason);
            Assert.Empty(context.ObservedValues);
            Assert.Empty(context.Evidence);
            Assert.Equal(PatchPolicy.PROHIBITED, context.DocumentPatchPolicy);
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public void V23_legacy_bridge_rejects_self_consistent_foreign_section()
    {
        var a = CreateDocx();
        var b = CreateDocx();
        try
        {
            var docA = Parsed(a);
            var docB = Parsed(b);
            var context = PartyObservationContextBridge.Create(docA, new DateOnly(2026, 10, 21));
            var result = PartyObservationContextBridge.ProjectMargins(
                context, docB, Assert.Single(docB.Sections), "crosssection",
                new MarginTarget(20, 20, 30, 15),
                new MarginPrintEvidence(DeclaredPrintSurface.SingleSided, true, true, true));
            Assert.Equal(ValidationStatus.NOT_EVALUATED, result.Status);
            Assert.Equal("DOCUMENT_SESSION_MISMATCH", result.Reason);
            Assert.Empty(context.ObservedValues);
            Assert.Empty(context.Evidence);
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public void V23_unbound_context_cannot_create_observations()
    {
        var file = CreateDocx();
        try
        {
            var doc = Parsed(file);
            var paragraph = Assert.Single(doc.Paragraphs);
            var context = new ValidationContext();
            var result = PartyObservationContextBridge.ProjectRun(
                context, doc, paragraph, "unbound",
                Ids(paragraph), ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, result.Status);
            Assert.Equal("DOCUMENT_SESSION_MISMATCH", result.Reason);
            Assert.Empty(context.ObservedValues);
            Assert.Equal(PatchPolicy.PROHIBITED, context.DocumentPatchPolicy);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void V23_reparse_same_file_is_different_document_instance()
    {
        var file = CreateDocx();
        try
        {
            var a = Parsed(file);
            var b = Parsed(file);
            var p = Assert.Single(b.Paragraphs);
            var context = PartyObservationContextBridge.Create(a, new DateOnly(2026, 10, 21));
            var result = PartyObservationContextBridge.ProjectRun(
                context, b, p, "reparsed", Ids(p), ObservedRunProperty.FontSizePt);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, result.Status);
            Assert.Equal("DOCUMENT_SESSION_MISMATCH", result.Reason);
            Assert.Empty(context.Evidence);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void V23_session_rejects_foreign_paragraph_and_section()
    {
        var a = CreateDocx();
        var b = CreateDocx();
        try
        {
            var session = PartyEvidenceSession.Open(Parsed(a), new DateOnly(2026, 10, 21));
            var foreign = Parsed(b);
            var paragraph = Assert.Single(foreign.Paragraphs);
            var run = session.ObserveRun(
                paragraph, "foreignrun", Ids(paragraph), ObservedRunProperty.Bold);
            var margin = session.ObserveMargins(
                Assert.Single(foreign.Sections), "foreignmargin",
                new MarginTarget(20, 20, 30, 15),
                new MarginPrintEvidence(DeclaredPrintSurface.SingleSided, true, true, true));
            Assert.Equal(ValidationStatus.NOT_EVALUATED, run.Status);
            Assert.Equal(ValidationStatus.NOT_EVALUATED, margin.Status);
            Assert.Empty(session.Snapshot().ObservedTargetKeys);
            Assert.Equal(0, session.Snapshot().EvidenceRecordCount);
        }
        finally { File.Delete(a); File.Delete(b); }
    }

    [Fact]
    public void V23_session_own_document_is_review_only_and_snapshot_is_stable()
    {
        var file = CreateDocx();
        try
        {
            var doc = Parsed(file);
            var p = Assert.Single(doc.Paragraphs);
            var session = PartyEvidenceSession.Open(doc, new DateOnly(2026, 10, 21));
            var before = session.Snapshot();
            var observation = session.ObserveRun(
                p, "ownrun", Ids(p), ObservedRunProperty.FontSizePt);
            var after = session.Snapshot();
            Assert.Equal(ValidationStatus.NEEDS_REVIEW, observation.Status);
            Assert.Empty(before.ObservedTargetKeys);
            Assert.Single(after.ObservedTargetKeys);
            Assert.Equal(1, after.EvidenceRecordCount);
            Assert.Equal(PatchPolicy.PROHIBITED, after.DocumentPatchPolicy);
            Assert.True(after.RequiresHumanReview);
        }
        finally { File.Delete(file); }
    }

    [Fact]
    public void V23_session_duplicate_concurrent_observations_do_not_overwrite()
    {
        var file = CreateDocx();
        try
        {
            var doc = Parsed(file);
            var p = Assert.Single(doc.Paragraphs);
            var ids = Ids(p);
            var session = PartyEvidenceSession.Open(doc, new DateOnly(2026, 10, 21));
            var statuses = new ValidationStatus[12];
            Parallel.For(0, statuses.Length, i =>
            {
                statuses[i] = session.ObserveRun(
                    p, "same", ids, ObservedRunProperty.FontSizePt).Status;
            });
            Assert.Equal(1, statuses.Count(s => s == ValidationStatus.NEEDS_REVIEW));
            Assert.Equal(11, statuses.Count(s => s == ValidationStatus.NOT_EVALUATED));
            var snapshot = session.Snapshot();
            Assert.Single(snapshot.ObservedTargetKeys);
            Assert.Equal(1, snapshot.EvidenceRecordCount);
            Assert.Equal(12, snapshot.Results.Count);
        }
        finally { File.Delete(file); }
    }
}
