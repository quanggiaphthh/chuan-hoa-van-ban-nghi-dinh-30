#!/usr/bin/env python3
from pathlib import Path
import argparse
ROOT=Path(__file__).resolve().parents[1]
STATE=ROOT/'v22-runtime/src/LegalValidator/State/DocumentState.cs'
PLAN=ROOT/'v22-runtime/src/LegalValidator/Remediation/RemediationPlanner.cs'
TEST=ROOT/'v22-runtime/tests/LegalValidator.Tests/V25L4ADocumentIdentityTests.cs'
p=argparse.ArgumentParser();p.add_argument('--tests-only',action='store_true');p.add_argument('--production-only',action='store_true');args=p.parse_args()
if not args.tests_only:
 STATE.parent.mkdir(parents=True,exist_ok=True)
 STATE.write_text(r'''using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Nd30.LegalValidator.Model;
namespace Nd30.LegalValidator.State;
public sealed record DocumentIdentity(string Algorithm,string Digest,string Scope="whole-document-bytes") {
 public const string SupportedAlgorithm="SHA-256";
 public static bool IsValid(DocumentIdentity? x)=>x is not null&&x.Algorithm==SupportedAlgorithm&&x.Scope=="whole-document-bytes"&&Regex.IsMatch(x.Digest,"^[0-9a-f]{64}$",RegexOptions.CultureInvariant);
}
public static class DocumentIdentityService {
 public static DocumentIdentity FromFile(string path){using var s=File.OpenRead(path);return new(DocumentIdentity.SupportedAlgorithm,Convert.ToHexString(SHA256.HashData(s)).ToLowerInvariant());}
}
public sealed record RuleIdentity(string RuleId,string SourceId,string SourceLocator) {
 public static RuleIdentity From(ValidationResult r)=>new(r.RuleId,r.Source.SourceId,r.Source.Locator);
}
public sealed record DocumentStateBinding(DocumentIdentity ExpectedDocument,RuleIdentity ExpectedRule);
public enum StaleStateDecision { MATCH, STALE_DOCUMENT, MISSING_EXPECTED_IDENTITY, UNSUPPORTED_IDENTITY, RULE_IDENTITY_MISMATCH }
public sealed record StaleStateResult(StaleStateDecision Decision,bool StateMatches,bool Authorized,string Reason);
public static class StaleStateVerifier {
 public static StaleStateResult Verify(DocumentStateBinding? expected,DocumentIdentity actual,RuleIdentity actualRule){
  if(expected is null||expected.ExpectedDocument is null)return R(StaleStateDecision.MISSING_EXPECTED_IDENTITY,"Expected document identity is required.");
  if(!DocumentIdentity.IsValid(expected.ExpectedDocument)||!DocumentIdentity.IsValid(actual))return R(StaleStateDecision.UNSUPPORTED_IDENTITY,"Only canonical SHA-256 whole-document identities are supported.");
  if(expected.ExpectedDocument!=actual)return R(StaleStateDecision.STALE_DOCUMENT,"Document bytes no longer match proposal state.");
  if(expected.ExpectedRule!=actualRule)return R(StaleStateDecision.RULE_IDENTITY_MISMATCH,"Rule/source identity no longer matches proposal state.");
  return new(StaleStateDecision.MATCH,true,false,"State matches; this is not authorization.");
 }
 static StaleStateResult R(StaleStateDecision d,string reason)=>new(d,false,false,reason);
}
''',encoding='utf-8')
 # Extend L3 proposal without changing ProposalId seed: state binding is explicit but separate from immutable semantic proposal id.
 s=PLAN.read_text(encoding='utf-8')
 if 'using Nd30.LegalValidator.State;' not in s:s=s.replace('using Nd30.LegalValidator.Model;','using Nd30.LegalValidator.Model;\nusing Nd30.LegalValidator.State;')
 s=s.replace('bool Executable,string Reason);','bool Executable,string Reason,DocumentStateBinding? StateBinding=null);')
 s=s.replace('public RemediationProposal Propose(ValidationResult finding,PackageSafetyState safety,string operation="set_property") {','public RemediationProposal Propose(ValidationResult finding,PackageSafetyState safety,string operation="set_property",DocumentStateBinding? stateBinding=null) {')
 s=s.replace('return new(id,finding.RuleId,findingRef,refs,target,operation,finding.Observed,finding.Expected,d,executable,reason);','return new(id,finding.RuleId,findingRef,refs,target,operation,finding.Observed,finding.Expected,d,executable,reason,stateBinding);')
 PLAN.write_text(s,encoding='utf-8')
if not args.production_only:
 TEST.write_text(r'''using Xunit;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
namespace Nd30.LegalValidator.Tests;
public sealed class V25L4ADocumentIdentityTests {
 static string Temp(byte[] b){var p=Path.GetTempFileName();File.WriteAllBytes(p,b);return p;}
 static ValidationResult Finding()=>new("RULE.1",ValidationStatus.FAIL,"LEGAL_ERROR",new("SRC","loc"),new("paragraph","title","font"),"Times New Roman","Arial",[new("semantic_component_detection",EvidenceAuthority.Authoritative,"ev-1",1,false,new Dictionary<string,string>{{"target_id","p-1"}})],[],[],"applicable",1,PatchEligibility.ELIGIBLE,"test");
 static PackageSafetyState Safety()=>new(true,false,false,false,false,false,false,[],PatchPolicy.NORMAL,[]);
 [Fact] public void Exact_same_bytes_have_same_identity(){var p=Temp([1,2,3]);try{Assert.Equal(DocumentIdentityService.FromFile(p),DocumentIdentityService.FromFile(p));}finally{File.Delete(p);}}
 [Fact] public void Changed_bytes_change_identity(){var a=Temp([1,2,3]);var b=Temp([1,2,4]);try{Assert.NotEqual(DocumentIdentityService.FromFile(a),DocumentIdentityService.FromFile(b));}finally{File.Delete(a);File.Delete(b);}}
 [Fact] public void Identity_is_canonical_sha256_lower_hex(){var p=Temp([9,8,7]);try{var x=DocumentIdentityService.FromFile(p);Assert.Equal("SHA-256",x.Algorithm);Assert.Equal("whole-document-bytes",x.Scope);Assert.Matches("^[0-9a-f]{64}$",x.Digest);}finally{File.Delete(p);}}
 [Fact] public void Proposal_has_explicit_state_binding_and_preserves_provenance(){var p=Temp([1]);try{var f=Finding();var bind=new DocumentStateBinding(DocumentIdentityService.FromFile(p),RuleIdentity.From(f));var q=new RemediationPlanner().Propose(f,Safety(),stateBinding:bind);Assert.Equal(bind,q.StateBinding);Assert.Equal("RULE.1",q.RuleId);Assert.Contains("RULE.1",q.FindingReference);Assert.Equal(["ev-1"],q.EvidenceReferences);}finally{File.Delete(p);}}
 [Fact] public void Document_mismatch_is_stale_not_pass_or_applicability(){var a=new DocumentIdentity("SHA-256",new string('a',64));var b=new DocumentIdentity("SHA-256",new string('b',64));var r=new RuleIdentity("R","S","L");var x=StaleStateVerifier.Verify(new(a,r),b,r);Assert.Equal(StaleStateDecision.STALE_DOCUMENT,x.Decision);Assert.False(x.StateMatches);}
 [Fact] public void Missing_expected_identity_fails_closed(){var a=new DocumentIdentity("SHA-256",new string('a',64));var r=new RuleIdentity("R","S","L");var x=StaleStateVerifier.Verify(null,a,r);Assert.Equal(StaleStateDecision.MISSING_EXPECTED_IDENTITY,x.Decision);Assert.False(x.StateMatches);}
 [Theory][InlineData("MD5")][InlineData("sha256")][InlineData("SHA-512")] public void Unsupported_algorithm_fails_closed(string alg){var bad=new DocumentIdentity(alg,new string('a',64));var actual=new DocumentIdentity("SHA-256",new string('a',64));var r=new RuleIdentity("R","S","L");Assert.Equal(StaleStateDecision.UNSUPPORTED_IDENTITY,StaleStateVerifier.Verify(new(bad,r),actual,r).Decision);}
 [Fact] public void Malformed_digest_fails_closed(){var bad=new DocumentIdentity("SHA-256","abc");var actual=new DocumentIdentity("SHA-256",new string('a',64));var r=new RuleIdentity("R","S","L");Assert.Equal(StaleStateDecision.UNSUPPORTED_IDENTITY,StaleStateVerifier.Verify(new(bad,r),actual,r).Decision);}
 [Fact] public void Match_never_grants_authorization(){var a=new DocumentIdentity("SHA-256",new string('a',64));var r=new RuleIdentity("R","S","L");var x=StaleStateVerifier.Verify(new(a,r),a,r);Assert.Equal(StaleStateDecision.MATCH,x.Decision);Assert.True(x.StateMatches);Assert.False(x.Authorized);}
 [Fact] public void Rule_identity_mismatch_is_stale_rule_state(){var a=new DocumentIdentity("SHA-256",new string('a',64));var old=new RuleIdentity("R","SRC","loc-1");var current=new RuleIdentity("R","SRC","loc-2");var x=StaleStateVerifier.Verify(new(a,old),a,current);Assert.Equal(StaleStateDecision.RULE_IDENTITY_MISMATCH,x.Decision);Assert.False(x.StateMatches);}
 [Fact] public void State_binding_does_not_change_l3_proposal_id(){var p=Temp([1]);try{var f=Finding();var planner=new RemediationPlanner();var unbound=planner.Propose(f,Safety());var bound=planner.Propose(f,Safety(),stateBinding:new(DocumentIdentityService.FromFile(p),RuleIdentity.From(f)));Assert.Equal(unbound.ProposalId,bound.ProposalId);}finally{File.Delete(p);}}
}
''',encoding='utf-8')
print('V25 L4A materialization complete')