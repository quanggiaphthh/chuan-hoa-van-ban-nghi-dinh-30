using Xunit;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Remediation;
namespace Nd30.LegalValidator.Tests;
public sealed class V25L3PatchSafetyTests {
 static PackageSafetyState Safety(PatchPolicy p=PatchPolicy.NORMAL,bool signed=false,bool prot=false,IReadOnlyList<string>? unsupported=null)=>new(true,signed,prot,false,false,false,false,unsupported??[],p,[]);
 static EvidenceRecord Ev(string reference="ev-1",string? target="p-1")=>new("semantic_component_detection",EvidenceAuthority.Authoritative,reference,1.0,false,target is null?null:new Dictionary<string,string>{{"target_id",target}});
 static ValidationResult Finding(ValidationStatus s=ValidationStatus.FAIL,PatchEligibility pe=PatchEligibility.ELIGIBLE,IReadOnlyList<EvidenceRecord>? ev=null)=>new("RULE.1",s,"LEGAL_ERROR",new("SRC","loc"),new("paragraph","title","font"),"Times New Roman","Arial",ev??[Ev()],[],[],"applicable",1.0,pe,"test");
 [Fact] public void Not_evaluated_is_never_executable(){Assert.False(new RemediationPlanner().Propose(Finding(ValidationStatus.NOT_EVALUATED,PatchEligibility.UNKNOWN),Safety()).Executable);}
 [Fact] public void Not_applicable_is_never_executable(){Assert.False(new RemediationPlanner().Propose(Finding(ValidationStatus.NOT_APPLICABLE,PatchEligibility.NOT_NEEDED),Safety()).Executable);}
 [Fact] public void Fail_without_evidence_is_not_executable(){var p=new RemediationPlanner().Propose(Finding(ev:Array.Empty<EvidenceRecord>()),Safety());Assert.False(p.Executable);Assert.Equal(RemediationDecision.BLOCKED_INSUFFICIENT_EVIDENCE,p.Decision);}
 [Fact] public void Ambiguous_target_is_blocked(){var p=new RemediationPlanner().Propose(Finding(ev:[Ev("a","p-1"),Ev("b","p-2")]),Safety());Assert.Equal(RemediationDecision.BLOCKED_AMBIGUOUS,p.Decision);Assert.False(p.Executable);}
 [Fact] public void Unsupported_structure_is_blocked(){var p=new RemediationPlanner().Propose(Finding(),Safety(PatchPolicy.PROHIBITED,unsupported:["altChunk"]));Assert.Equal(RemediationDecision.BLOCKED_UNSUPPORTED,p.Decision);Assert.False(p.Executable);}
 [Fact] public void Signed_document_is_blocked(){var p=new RemediationPlanner().Propose(Finding(),Safety(PatchPolicy.AUDIT_ONLY,signed:true));Assert.Equal(RemediationDecision.BLOCKED_PROTECTED,p.Decision);Assert.False(p.Executable);}
 [Fact] public void Protected_document_is_blocked(){var p=new RemediationPlanner().Propose(Finding(),Safety(PatchPolicy.AUDIT_ONLY,prot:true));Assert.Equal(RemediationDecision.BLOCKED_PROTECTED,p.Decision);Assert.False(p.Executable);}
 [Fact] public void Proposal_preserves_rule_finding_and_evidence_provenance(){var p=new RemediationPlanner().Propose(Finding(),Safety());Assert.Equal("RULE.1",p.RuleId);Assert.Contains("RULE.1",p.FindingReference);Assert.Equal(["ev-1"],p.EvidenceReferences);Assert.Equal("p-1",p.TargetId);}
 [Fact] public void Same_input_produces_deterministic_proposal(){var x=new RemediationPlanner();var a=x.Propose(Finding(),Safety());var b=x.Propose(Finding(),Safety());Assert.Equal(a.ProposalId,b.ProposalId);Assert.Equal(a.Decision,b.Decision);Assert.Equal(a.TargetId,b.TargetId);Assert.Equal(a.EvidenceReferences,b.EvidenceReferences);}
 [Fact] public void Proposal_does_not_mutate_document_or_grant_write_authority(){var f=Finding();var s=Safety();var p=new RemediationPlanner().Propose(f,s);Assert.True(p.Executable);Assert.Equal(RemediationDecision.ELIGIBLE,p.Decision);Assert.Contains("separate authorized path",p.Reason,StringComparison.OrdinalIgnoreCase);Assert.Equal("Arial",f.Observed);Assert.Equal(PatchPolicy.NORMAL,s.PatchPolicy);}
}
