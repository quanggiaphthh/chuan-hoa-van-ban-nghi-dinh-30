using System.Text.Json;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using Nd30.DocumentEngine.Parsing;
using Nd30.SemanticDetector.Detection;
using Nd30.SemanticDetector.Model;
using Xunit;

namespace Nd30.SemanticDetector.Tests;

public class SemanticDetectorTests
{
    static string FixtureDir => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../fixtures/v23"));
    static string F(string n)=>Path.Combine(FixtureDir,n);
    static SemanticDocumentModel S(string n)
    {
        var parsed=new DocxParser().Parse(F(n));
        Assert.NotNull(parsed.Document);
        return new AdministrativeSemanticDetector().Detect(parsed.Document!);
    }

    [Fact]
    public void Expected_fixture_matrix_is_satisfied()
    {
        using var json=JsonDocument.Parse(File.ReadAllText(F("expected.json")));
        foreach(var entry in json.RootElement.EnumerateObject())
        {
            var expected=entry.Value;
            var s=S(entry.Name);
            Assert.Equal(expected.GetProperty("class").GetString(),s.Classification.DocumentClass.ToString());
            var specific=expected.GetProperty("specific");
            Assert.Equal(specific.ValueKind==JsonValueKind.Null?null:specific.GetString(),s.Classification.SpecificType);
            Assert.Equal(expected.GetProperty("review").GetBoolean(),s.RequiresReview);
            var template=expected.GetProperty("template");
            Assert.Equal(template.ValueKind==JsonValueKind.Null?null:template.GetString(),s.TemplateMatch.TemplateId);
            foreach(var role in expected.GetProperty("roles").EnumerateArray())
                Assert.Contains(s.Components,x=>x.Role.ToString()==role.GetString());
        }
    }

    [Fact]
    public void Official_letter_and_named_document_route_differently()
    {
        var official=S("01-official-letter.docx");
        var named=S("04-table-header.docx");
        Assert.Equal(AdministrativeDocumentClass.OfficialLetter,official.Classification.DocumentClass);
        Assert.Equal("cong_van",official.Classification.SpecificType);
        Assert.DoesNotContain(official.Components,x=>x.Role==SemanticRole.DocumentTypeHeading);
        Assert.Equal(AdministrativeDocumentClass.NamedAdministrativeDocument,named.Classification.DocumentClass);
        Assert.Equal("bao_cao",named.Classification.SpecificType);
        Assert.Contains(named.Components,x=>x.Role==SemanticRole.DocumentTypeHeading);
    }

    [Fact]
    public void Ambiguity_requires_review_and_exposes_competing_candidates()
    {
        var s=S("03-ambiguous-type.docx");
        Assert.True(s.RequiresReview);
        Assert.True(s.Classification.NeedsReview);
        Assert.Equal(AdministrativeDocumentClass.Unknown,s.Classification.DocumentClass);
        Assert.Contains("quyet_dinh",s.Classification.CompetingTypes);
        Assert.Contains("cong_van",s.Classification.CompetingTypes);
        Assert.Contains(s.Ambiguities,x=>x.Code=="NAMED_AND_OFFICIAL_LETTER_SIGNALS" || x.Code=="DOCUMENT_TYPE_AMBIGUOUS");
        Assert.Contains(s.Components,x=>x.CompetingCandidateIds.Count>0);
        Assert.Contains(s.Diagnostics,x=>x.Contains("structural auto-fix",StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Multiple_named_headings_require_review()
    {
        var s=S("06-multiple-type-candidates.docx");
        Assert.True(s.RequiresReview);
        Assert.Equal(AdministrativeDocumentClass.Unknown,s.Classification.DocumentClass);
        Assert.Contains(s.Ambiguities,x=>x.Code=="MULTIPLE_DOCUMENT_TYPE_HEADINGS");
        Assert.True(s.Components.Count(x=>x.Role==SemanticRole.DocumentTypeHeading)>=2);
    }

    [Fact]
    public void Table_based_header_retains_table_cell_evidence()
    {
        var s=S("04-table-header.docx");
        var nh=Assert.Single(s.Components, x=>x.Role==SemanticRole.NationalHeader);
        Assert.Equal("table-cell",nh.Evidence.SourceKind);
        Assert.StartsWith("t1.",nh.Evidence.ParagraphId);
        Assert.NotEmpty(nh.Evidence.RunIds);
        Assert.Contains(s.Components,x=>x.Role==SemanticRole.IssuingAuthorityParent && x.Evidence.SourceKind=="table-cell");
        Assert.Contains(s.Components,x=>x.Role==SemanticRole.IssuingAuthority && x.Evidence.SourceKind=="table-cell");
    }

    [Fact]
    public void Unusual_spacing_does_not_break_lexical_detection()
    {
        var s=S("05-unusual-spacing.docx");
        Assert.Equal("thong_bao",s.Classification.SpecificType);
        Assert.Contains(s.Components,x=>x.Role==SemanticRole.NationalHeader);
        Assert.Contains(s.Components,x=>x.Role==SemanticRole.NationalMotto);
        Assert.Contains(s.Components,x=>x.Role==SemanticRole.SubjectNamedDocument);
    }

    [Fact]
    public void Hierarchy_is_deterministic()
    {
        var s=S("09-hierarchy.docx");
        foreach(var role in new[]{SemanticRole.PartHeading,SemanticRole.ChapterHeading,SemanticRole.SectionHeading,SemanticRole.SubsectionHeading,SemanticRole.ArticleHeading,SemanticRole.Clause,SemanticRole.Point})
            Assert.Contains(s.Components,x=>x.Role==role && x.Status is ClassificationStatus.CONFIRMED or ClassificationStatus.INFERRED_HIGH);
    }

    [Fact]
    public void Appendix_and_copy_are_routed_without_inventing_medium()
    {
        var appendix=S("07-appendix.docx");
        Assert.Equal(AdministrativeDocumentClass.Appendix,appendix.Classification.DocumentClass);
        Assert.Null(appendix.TemplateMatch.TemplateId);
        Assert.Equal(new[]{"ND30.PL3.TEMPLATE_2_1","ND30.PL3.TEMPLATE_2_2"},appendix.TemplateMatch.CandidateTemplateIds);
        Assert.True(appendix.TemplateMatch.NeedsReview);

        var copy=S("08-copy.docx");
        Assert.Equal(AdministrativeDocumentClass.Copy,copy.Classification.DocumentClass);
        Assert.Null(copy.TemplateMatch.TemplateId);
        Assert.Equal(new[]{"ND30.PL3.TEMPLATE_3_1","ND30.PL3.TEMPLATE_3_2"},copy.TemplateMatch.CandidateTemplateIds);
        Assert.True(copy.TemplateMatch.NeedsReview);
    }

    [Fact]
    public void Decision_template_remains_1_2_or_1_3_until_semantics_resolve_it()
    {
        var s=S("02-named-decision.docx");
        Assert.Equal("quyet_dinh",s.Classification.SpecificType);
        Assert.Null(s.TemplateMatch.TemplateId);
        Assert.Equal(new[]{"ND30.PL3.TEMPLATE_1_2","ND30.PL3.TEMPLATE_1_3"},s.TemplateMatch.CandidateTemplateIds);
        Assert.True(s.TemplateMatch.NeedsReview);
    }

    [Fact]
    public void Missing_components_stay_unknown_not_false_positive()
    {
        var s=S("10-missing-components.docx");
        Assert.Equal(AdministrativeDocumentClass.Unknown,s.Classification.DocumentClass);
        Assert.True(s.RequiresReview);
        Assert.Null(s.TemplateMatch.TemplateId);
        Assert.Contains(s.Components,x=>x.Role==SemanticRole.Body);
        Assert.DoesNotContain(s.Components,x=>x.Role==SemanticRole.DocumentTypeHeading);
    }

    [Fact]
    public void Markings_signature_and_contact_are_evidence_backed()
    {
        var s=S("11-markings-signature.docx");
        foreach(var role in new[]{SemanticRole.UrgencyMark,SemanticRole.ClassificationMark,SemanticRole.CirculationInstruction,SemanticRole.SigningAuthorityPrefix,SemanticRole.SignerTitle,SemanticRole.SignerName,SemanticRole.IssuedCopyCount,SemanticRole.ContactInformation})
            Assert.Contains(s.Components,x=>x.Role==role && !string.IsNullOrWhiteSpace(x.Evidence.ParagraphId));
    }

    [Fact]
    public void Every_semantic_component_carries_traceable_evidence()
    {
        foreach(var f in Directory.GetFiles(FixtureDir,"*.docx"))
        {
            var s=S(Path.GetFileName(f));
            Assert.All(s.Components,c=>
            {
                Assert.InRange(c.Confidence,0d,1d);
                Assert.False(string.IsNullOrWhiteSpace(c.Evidence.ParagraphId));
                Assert.False(string.IsNullOrWhiteSpace(c.Evidence.Text));
                Assert.NotEmpty(c.Evidence.RunIds);
                Assert.NotEmpty(c.Evidence.Reasons);
            });
        }
    }

    [Fact]
    public void Semantic_fixtures_are_valid_openxml()
    {
        foreach(var f in Directory.GetFiles(FixtureDir,"*.docx"))
        {
            using var d=WordprocessingDocument.Open(f,false);
            var errors=new OpenXmlValidator().Validate(d).ToList();
            Assert.True(errors.Count==0,$"{Path.GetFileName(f)}: "+string.Join(" | ",errors.Select(e=>e.Description)));
        }
    }
}
