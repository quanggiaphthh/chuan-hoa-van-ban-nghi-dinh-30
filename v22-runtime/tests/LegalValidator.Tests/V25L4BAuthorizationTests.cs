using Xunit;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Authorization;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
namespace Nd30.LegalValidator.Tests;
public sealed class V25L4BAuthorizationTests {
 static DocumentIdentity Doc(char c='a')=>new("SHA-256",new string(c,64));
 static RuleIdentity Rule()=>new("RULE.1","SRC","loc");
 static DocumentStateBinding Bind(char c='a')=>new(Doc(c),Rule());
 static PackageSafetyState Safety(PatchPolicy p=PatchPolicy.NORMAL,bool signed=false,bool prot=false)=>new(true,signed,prot,false,false,false,false,[],p,[]);
 static ValidationResult Finding(ValidationStatus s=ValidationStatus.FAIL,PatchEligibility pe=PatchEligibility.ELIGIBLE)=>new("RULE.1",s,"LEGAL_ERROR",new("SRC","loc"),new("paragraph","title","font"),"Times New Roman","Arial",[new("semantic_component_detection",EvidenceAuthority.Authoritative,"ev-1",1,false,new Dictionary<string,string>{{"target_id","p-1"}})],[],[],"applicable",1,pe,"test");
 static RemediationProposal Proposal(ValidationStatus s=ValidationStatus.FAIL,PatchEligibility pe=PatchEligibility.ELIGIBLE,PackageSafetyState? safety=null,DocumentStateBinding? binding=null)=>new RemediationPlanner().Propose(Finding(s,pe),safety??Safety(),stateBinding:binding??Bind());
 static AuthorizationResult Go(RemediationProposal p,PackageSafetyState? safety=null,AuthorizationArtifact? a=null,DocumentIdentity? actual=null)=>AuthorizationBoundary.Authorize(p,safety??Safety(),a,actual??Doc(),Rule());
 [Fact] public void Eligible_without_authorization_is_rejected(){Assert.Equal(AuthorizationDecision.MISSING_AUTHORIZATION,Go(Proposal()).Decision);}
 [Fact] public void Authorization_for_proposal_A_cannot_authorize_B(){var a=Proposal();var b=new RemediationPlanner().Propose(Finding(),Safety(),operation:"set_other",stateBinding:Bind());var art=AuthorizationArtifact.Issue(a);Assert.Equal(AuthorizationDecision.PROPOSAL_MISMATCH,Go(b,a:art).Decision);}
 [Fact] public void Authorization_for_document_A_cannot_authorize_document_B(){var p=Proposal();var art=AuthorizationArtifact.Issue(p) with { StateBinding=Bind('b') };Assert.Equal(AuthorizationDecision.DOCUMENT_STATE_MISMATCH,Go(p,a:art).Decision);}
 [Fact] public void Stale_actual_document_is_rejected(){var p=Proposal();Assert.Equal(AuthorizationDecision.STALE_STATE,Go(p,a:AuthorizationArtifact.Issue(p),actual:Doc('b')).Decision);}
 [Theory][InlineData(ValidationStatus.NOT_EVALUATED,PatchEligibility.UNKNOWN)][InlineData(ValidationStatus.NOT_APPLICABLE,PatchEligibility.NOT_NEEDED)] public void Non_eligible_status_cannot_be_authorized(ValidationStatus s,PatchEligibility pe){var p=Proposal(s,pe);var art=AuthorizationArtifact.Issue(p);Assert.Equal(AuthorizationDecision.PROPOSAL_NOT_ELIGIBLE,Go(p,a:art).Decision);}
 [Fact] public void Suggestion_only_cannot_be_authorized(){var p=Proposal(pe:PatchEligibility.SUGGEST_ONLY);Assert.Equal(AuthorizationDecision.PROPOSAL_NOT_ELIGIBLE,Go(p,a:AuthorizationArtifact.Issue(p)).Decision);}
 [Fact] public void Blocked_proposal_cannot_be_authorized(){var blocked=Safety(PatchPolicy.AUDIT_ONLY,signed:true);var p=Proposal(safety:blocked);Assert.Equal(AuthorizationDecision.PROPOSAL_NOT_ELIGIBLE,Go(p,blocked,AuthorizationArtifact.Issue(p)).Decision);}
 [Theory][InlineData(true,false)][InlineData(false,true)] public void Protected_or_signed_safety_cannot_be_overridden(bool signed,bool prot){var p=Proposal();var unsafeState=Safety(PatchPolicy.AUDIT_ONLY,signed,prot);Assert.Equal(AuthorizationDecision.SAFETY_BLOCKED,Go(p,unsafeState,AuthorizationArtifact.Issue(p)).Decision);}
 [Fact] public void Valid_exact_binding_creates_authorized_request(){var p=Proposal();var x=Go(p,a:AuthorizationArtifact.Issue(p));Assert.True(x.Authorized);Assert.Equal(AuthorizationDecision.AUTHORIZED,x.Decision);Assert.NotNull(x.Request);}
 [Fact] public void Authorized_request_has_no_document_mutation_effect(){var p=Proposal();var before=p.ProposedValue;var x=Go(p,a:AuthorizationArtifact.Issue(p));Assert.NotNull(x.Request);Assert.Equal(before,p.ProposedValue);Assert.Contains("no mutation authority",x.Reason,StringComparison.OrdinalIgnoreCase);}
 [Fact] public void Malformed_authorization_identity_fails_closed(){var p=Proposal();var bad=AuthorizationArtifact.Issue(p) with { AuthorizationId="bad" };Assert.Equal(AuthorizationDecision.MALFORMED_AUTHORIZATION,Go(p,a:bad).Decision);}
 [Fact] public void Request_identity_is_deterministic_and_distinct_from_proposal_and_authorization(){var p=Proposal();var art=AuthorizationArtifact.Issue(p);var a=Go(p,a:art).Request!;var b=Go(p,a:art).Request!;Assert.Equal(a.RequestId,b.RequestId);Assert.Equal(a.IdempotencyKey,b.IdempotencyKey);Assert.NotEqual(p.ProposalId,a.RequestId);Assert.NotEqual(art.AuthorizationId,a.RequestId);}
 [Fact] public void Request_preserves_proposal_rule_finding_evidence_and_document_provenance(){var p=Proposal();var r=Go(p,a:AuthorizationArtifact.Issue(p)).Request!;Assert.Equal(p.ProposalId,r.ProposalId);Assert.Equal(p.RuleId,r.RuleId);Assert.Equal(p.FindingReference,r.FindingReference);Assert.Equal(p.EvidenceReferences,r.EvidenceReferences);Assert.Equal(p.StateBinding,r.StateBinding);}
 [Fact] public void Actor_reference_is_opaque_not_authentication(){var p=Proposal();var art=AuthorizationArtifact.Issue(p,"opaque:caller-7");Assert.Equal("opaque:caller-7",art.ActorReference);Assert.True(Go(p,a:art).Authorized);}
}
