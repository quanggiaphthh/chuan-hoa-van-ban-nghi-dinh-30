using Xunit;
using Nd30.DocumentEngine.Model;
using Nd30.DocumentEngine.Safety;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
namespace Nd30.LegalValidator.Tests;
public sealed class V25L4ADocumentIdentityTests {
 static string Temp(byte[] b){var p=Path.GetTempFileName();File.WriteAllBytes(p,b);return p;}
 static ValidationResult Finding()=>new("RULE.1",ValidationStatus.FAIL,"LEGAL_ERROR",new("SRC","loc"),new("paragraph","title","font"),"Times New Roman","Arial",[new("semantic_component_detection",EvidenceAuthority.Authoritative,"ev-1",1,false,new Dictionary<string,string>{{"target_id","p-1"}})],[],[],"applicable",1,PatchEligibility.ELIGIBLE,"test");
 static PackageSafetyState Safety()=>new(true,false,false,false,false,false,false,[],PatchPolicy.NORMAL,[]);
 [Fact] public void Exact_same_bytes_have_same_identity(){var p=Temp([1,2,3]);try{Assert.Equal(DocumentIdentityService.FromFile(p),DocumentIdentityService.FromFile(p));}finally{File.Delete(p);}}
 [Fact] public void Oversized_identity_input_is_rejected_before_hashing(){var p=Temp([0]);using(var s=new FileStream(p,FileMode.Open,FileAccess.Write,FileShare.None)){s.SetLength(PackageSafetyLimits.MaximumCompressedInputBytes+1);}try{var e=Assert.Throws<DocumentPackageException>(()=>DocumentIdentityService.FromFile(p));Assert.Equal("INPUT_TOO_LARGE",e.Code);}finally{File.Delete(p);}}
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
