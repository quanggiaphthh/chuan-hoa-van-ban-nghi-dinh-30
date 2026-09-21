#!/usr/bin/env python3
from pathlib import Path
import argparse
ROOT=Path(__file__).resolve().parents[1]
AUTH=ROOT/'v22-runtime/src/LegalValidator/Authorization/AuthorizationBoundary.cs'
TEST=ROOT/'v22-runtime/tests/LegalValidator.Tests/V25L4BAuthorizationTests.cs'
p=argparse.ArgumentParser();p.add_argument('--tests-only',action='store_true');p.add_argument('--production-only',action='store_true');args=p.parse_args()
if not args.tests_only:
 AUTH.parent.mkdir(parents=True,exist_ok=True)
 AUTH.write_text(r'''using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Remediation;
using Nd30.LegalValidator.State;
namespace Nd30.LegalValidator.Authorization;
public sealed record AuthorizationArtifact(string AuthorizationId,string ProposalId,DocumentStateBinding StateBinding,string Scope="single-proposal-single-document-state",string? ActorReference=null) {
 public static bool IsValid(AuthorizationArtifact? x)=>x is not null&&Regex.IsMatch(x.AuthorizationId??"","^[0-9a-f]{24}$",RegexOptions.CultureInvariant)&&!string.IsNullOrWhiteSpace(x.ProposalId)&&x.StateBinding is not null&&x.Scope=="single-proposal-single-document-state";
 public static AuthorizationArtifact Issue(RemediationProposal proposal,string? actorReference=null){
  if(proposal.StateBinding is null) throw new ArgumentException("Proposal state binding is required.");
  var seed=string.Join("|","authorize",proposal.ProposalId,proposal.StateBinding.ExpectedDocument.Algorithm,proposal.StateBinding.ExpectedDocument.Digest,proposal.StateBinding.ExpectedDocument.Scope,proposal.StateBinding.ExpectedRule.RuleId,proposal.StateBinding.ExpectedRule.SourceId,proposal.StateBinding.ExpectedRule.SourceLocator,actorReference??"");
  return new(Hash(seed),proposal.ProposalId,proposal.StateBinding,ActorReference:actorReference);
 }
 internal static string Hash(string seed)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed))).ToLowerInvariant()[..24];
}
public enum AuthorizationDecision { AUTHORIZED, MISSING_AUTHORIZATION, MALFORMED_AUTHORIZATION, PROPOSAL_MISMATCH, DOCUMENT_STATE_MISMATCH, STALE_STATE, PROPOSAL_NOT_ELIGIBLE, SAFETY_BLOCKED }
public sealed record AuthorizedMutationRequest(string RequestId,string IdempotencyKey,string ProposalId,DocumentStateBinding StateBinding,AuthorizationArtifact Authorization,string RuleId,string FindingReference,IReadOnlyList<string> EvidenceReferences,string Operation,string? TargetId);
public sealed record AuthorizationResult(AuthorizationDecision Decision,bool Authorized,AuthorizedMutationRequest? Request,string Reason);
public static class AuthorizationBoundary {
 public static AuthorizationResult Authorize(RemediationProposal proposal,PackageSafetyState safety,AuthorizationArtifact? artifact,DocumentIdentity actualDocument,RuleIdentity actualRule){
  if(artifact is null)return R(AuthorizationDecision.MISSING_AUTHORIZATION,"Explicit authorization artifact is required.");
  if(!AuthorizationArtifact.IsValid(artifact))return R(AuthorizationDecision.MALFORMED_AUTHORIZATION,"Authorization binding is malformed.");
  if(proposal.Decision!=RemediationDecision.ELIGIBLE||!proposal.Executable||proposal.StateBinding is null)return R(AuthorizationDecision.PROPOSAL_NOT_ELIGIBLE,"Only an eligible, state-bound proposal may be authorized.");
  if(safety.IsSigned||safety.IsProtected||safety.PatchPolicy==PatchPolicy.AUDIT_ONLY||safety.PatchPolicy==PatchPolicy.PROHIBITED||!safety.IsReadable||safety.UnsupportedFeatures.Count>0)return R(AuthorizationDecision.SAFETY_BLOCKED,"Authorization cannot override package safety policy.");
  if(!StringComparer.Ordinal.Equals(artifact.ProposalId,proposal.ProposalId))return R(AuthorizationDecision.PROPOSAL_MISMATCH,"Authorization is bound to a different proposal.");
  if(artifact.StateBinding!=proposal.StateBinding)return R(AuthorizationDecision.DOCUMENT_STATE_MISMATCH,"Authorization is bound to a different expected state.");
  var stale=StaleStateVerifier.Verify(proposal.StateBinding,actualDocument,actualRule);
  if(stale.Decision!=StaleStateDecision.MATCH)return R(AuthorizationDecision.STALE_STATE,$"State verification failed: {stale.Decision}.");
  var requestSeed=string.Join("|","mutation-request",artifact.AuthorizationId,proposal.ProposalId,actualDocument.Digest,proposal.Operation,proposal.TargetId??"");
  var requestId=AuthorizationArtifact.Hash(requestSeed);
  var req=new AuthorizedMutationRequest(requestId,requestId,proposal.ProposalId,proposal.StateBinding,artifact,proposal.RuleId,proposal.FindingReference,proposal.EvidenceReferences,proposal.Operation,proposal.TargetId);
  return new(AuthorizationDecision.AUTHORIZED,true,req,"Explicit intent and exact state binding verified; request carries no mutation authority.");
 }
 static AuthorizationResult R(AuthorizationDecision d,string reason)=>new(d,false,null,reason);
}
''',encoding='utf-8')
if not args.production_only:
 TEST.write_text(r'''using Xunit;
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
''',encoding='utf-8')
print('V25 L4B materialization complete')