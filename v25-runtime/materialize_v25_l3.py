#!/usr/bin/env python3
from pathlib import Path
import argparse
ROOT=Path(__file__).resolve().parents[1]
SRC=ROOT/'v22-runtime/src/LegalValidator/Remediation/RemediationPlanner.cs'
TEST=ROOT/'v22-runtime/tests/LegalValidator.Tests/V25L3PatchSafetyTests.cs'
p=argparse.ArgumentParser();p.add_argument('--tests-only',action='store_true');p.add_argument('--production-only',action='store_true');args=p.parse_args()
if not args.tests_only:
 SRC.parent.mkdir(parents=True,exist_ok=True)
 SRC.write_text(r'''using System.Security.Cryptography;
using System.Text;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Model;
namespace Nd30.LegalValidator.Remediation;
public enum RemediationDecision { ELIGIBLE, SUGGESTION_ONLY, BLOCKED_STATUS, BLOCKED_INSUFFICIENT_EVIDENCE, BLOCKED_AMBIGUOUS, BLOCKED_UNSUPPORTED, BLOCKED_PROTECTED }
public sealed record RemediationProposal(string ProposalId,string RuleId,string FindingReference,IReadOnlyList<string> EvidenceReferences,string? TargetId,string Operation,string Expected,string ProposedValue,RemediationDecision Decision,bool Executable,string Reason);
public sealed class RemediationPlanner {
 public RemediationProposal Propose(ValidationResult finding,PackageSafetyState safety,string operation="set_property") {
  var refs=finding.Evidence.Select(x=>x.Reference).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  var targets=finding.Evidence.Select(x=>x.Attributes is not null&&x.Attributes.TryGetValue("target_id",out var t)?t:null).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.Ordinal).OrderBy(x=>x,StringComparer.Ordinal).ToArray();
  var findingRef=$"{finding.RuleId}:{finding.Target.Key}:{finding.Status}";
  RemediationDecision d; string reason;
  if(finding.Status is ValidationStatus.NOT_EVALUATED or ValidationStatus.NOT_APPLICABLE or ValidationStatus.PASS or ValidationStatus.NEEDS_REVIEW){d=RemediationDecision.BLOCKED_STATUS;reason=$"Finding status {finding.Status} has no executable remediation authority.";}
  else if(finding.Evidence.Count==0||refs.Length==0){d=RemediationDecision.BLOCKED_INSUFFICIENT_EVIDENCE;reason="Authoritative evidence reference is required for remediation.";}
  else if(targets.Length!=1){d=targets.Length>1?RemediationDecision.BLOCKED_AMBIGUOUS:RemediationDecision.BLOCKED_INSUFFICIENT_EVIDENCE;reason=targets.Length>1?"Evidence identifies multiple candidate targets.":"Evidence does not identify a deterministic target.";}
  else if(!safety.IsReadable||safety.UnsupportedFeatures.Count>0||safety.PatchPolicy==PatchPolicy.PROHIBITED){d=RemediationDecision.BLOCKED_UNSUPPORTED;reason="Document/package is unreadable or contains unsupported mutation-risk structure.";}
  else if(safety.IsSigned||safety.IsProtected||safety.PatchPolicy==PatchPolicy.AUDIT_ONLY){d=RemediationDecision.BLOCKED_PROTECTED;reason="Signed/protected/audit-only document cannot be mutated.";}
  else if(finding.PatchEligibility==PatchEligibility.SUGGEST_ONLY){d=RemediationDecision.SUGGESTION_ONLY;reason="Rule permits suggestion only.";}
  else if(finding.PatchEligibility!=PatchEligibility.ELIGIBLE){d=RemediationDecision.BLOCKED_STATUS;reason=$"Finding patch eligibility is {finding.PatchEligibility}.";}
  else {d=RemediationDecision.ELIGIBLE;reason="Proposal is technically eligible; mutation still requires a separate authorized path.";}
  var target=targets.Length==1?targets[0]:null; var executable=d==RemediationDecision.ELIGIBLE;
  var seed=string.Join("|",findingRef,operation,target??"",finding.Expected,finding.Observed,string.Join(",",refs),d);var id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant()[..24];
  return new(id,finding.RuleId,findingRef,refs,target,operation,finding.Observed,finding.Expected,d,executable,reason);
 }
}
''',encoding='utf-8')
if not args.production_only:
 TEST.write_text(r'''using Xunit;
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
''',encoding='utf-8')
print('V25 L3 materialization complete')