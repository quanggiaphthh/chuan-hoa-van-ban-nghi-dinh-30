using Nd30.DocumentEngine.Formatting;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Evaluation;
using Nd30.LegalValidator.Model;
using Nd30.SemanticDetector.Model;

namespace Nd30.LegalValidator.Adapters;

public static class SemanticValidationContextBuilder
{
    public static ValidationContext Build(DocumentModel document, SemanticDocumentModel semantic, DateOnly evaluationDate)
    {
        var c=new ValidationContext{EvaluationDate=evaluationDate,DocumentPatchPolicy=document.Safety.PatchPolicy};
        c.AddCapability("semantic_component_detection","document_type_classification","hierarchy_parser","appendix_detection","copy_component_detection","template_matching","effective_run_style","effective_paragraph_style","document_metadata_parser","document_encoding");
        c.SetField("document.type",DocumentType(semantic));
        foreach(var component in semantic.Components)
        {
            var role=Snake(component.Role.ToString());var prefix=$"semantic_component.{role}";
            var evidence=new EvidenceRecord("semantic_component_detection",EvidenceAuthority.Derived,component.Evidence.ParagraphId,component.Confidence,component.Status==ClassificationStatus.INFERRED_LOW||component.CompetingCandidateIds.Count>0,
                new Dictionary<string,string>{{"source_kind",component.Evidence.SourceKind},{"component_id",component.Id}});
            c.Observe(prefix+".text",component.Text,evidence);
            c.Observe(prefix+".present",true,evidence);
            c.Observe(prefix+".uppercase",component.Text.Any(char.IsLetter)&&component.Text.Where(char.IsLetter).All(char.IsUpper),evidence);
            if(component.Role==SemanticRole.SubjectOfficialLetter)c.Observe(prefix+".prefix",component.Text.TrimStart().StartsWith("V/v",StringComparison.OrdinalIgnoreCase)?"V/v":"",evidence);
            if(evidence.RequiresReview){c.MarkUncertain(prefix+".text");c.MarkUncertain(prefix+".present");}
            AddFormatting(document,component,prefix,c,evidence);
        }
        if(semantic.RequiresReview)
        {
            foreach(var a in semantic.Ambiguities)
                foreach(var id in a.CandidateIds)
                    foreach(var comp in semantic.Components.Where(x=>x.Id==id)) c.MarkUncertain($"semantic_component.{Snake(comp.Role.ToString())}.text");
        }
        return c;
    }

    private static void AddFormatting(DocumentModel doc,SemanticComponentCandidate comp,string prefix,ValidationContext c,EvidenceRecord evidence)
    {
        var p=FindParagraph(doc,comp.Evidence.ParagraphId);if(p is null)return;RunModel? r=comp.Evidence.RunIds.Select(id=>p.Runs.FirstOrDefault(x=>x.Id==id)).FirstOrDefault(x=>x is not null)??p.Runs.FirstOrDefault();
        try
        {
            var f=new EffectiveFormattingResolver().Resolve(doc,p,r);
            c.Observe(prefix+".font_size_pt",f.FontSizePt.Value,evidence);
            c.Observe(prefix+".bold",f.Bold.Value,evidence);c.Observe(prefix+".italic",f.Italic.Value,evidence);c.Observe(prefix+".alignment",f.Alignment.Value,evidence);
            if(f.LineSpacing.Value is not null)c.Observe(prefix+".line_spacing",f.LineSpacing.Value.Lines??f.LineSpacing.Value.Points,evidence);
        }
        catch(StyleInheritanceCycleException){c.MarkUncertain(prefix+".font_size_pt");c.MarkUncertain(prefix+".bold");c.MarkUncertain(prefix+".italic");}
    }
    private static ParagraphModel? FindParagraph(DocumentModel d,string id)=>d.Paragraphs.Concat(d.Tables.SelectMany(t=>t.Rows).SelectMany(r=>r.Cells).SelectMany(c=>c.Paragraphs)).Concat(d.Headers.SelectMany(h=>h.Paragraphs)).Concat(d.Footers.SelectMany(f=>f.Paragraphs)).FirstOrDefault(p=>p.Id==id);
    private static string DocumentType(SemanticDocumentModel s)=>s.Classification.DocumentClass switch{AdministrativeDocumentClass.OfficialLetter=>"official_letter",AdministrativeDocumentClass.NamedAdministrativeDocument=>s.Classification.SpecificType switch{"to_trinh"=>"submission","bao_cao"=>"report",null=>"named_administrative",var x=>x},AdministrativeDocumentClass.Appendix=>"appendix",AdministrativeDocumentClass.Copy=>"copy",_=>"unknown"};
    private static string Snake(string s)=>System.Text.RegularExpressions.Regex.Replace(s,"([a-z0-9])([A-Z])","$1_$2").ToLowerInvariant();
}
