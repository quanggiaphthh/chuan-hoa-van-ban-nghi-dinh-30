using System.Security.Cryptography;
using System.Text;
using Nd30.DocumentEngine.Model;
using Nd30.LegalValidator.Model;
using Nd30.LegalValidator.State;
namespace Nd30.LegalValidator.Remediation;
public enum RemediationDecision { ELIGIBLE, SUGGESTION_ONLY, BLOCKED_STATUS, BLOCKED_INSUFFICIENT_EVIDENCE, BLOCKED_AMBIGUOUS, BLOCKED_UNSUPPORTED, BLOCKED_PROTECTED }
public sealed record RemediationProposal(string ProposalId,string RuleId,string FindingReference,IReadOnlyList<string> EvidenceReferences,string? TargetId,string Operation,string Expected,string ProposedValue,RemediationDecision Decision,bool Executable,string Reason,DocumentStateBinding? StateBinding=null);
public sealed class RemediationPlanner {
 public RemediationProposal Propose(ValidationResult finding,PackageSafetyState safety,string operation="set_property",DocumentStateBinding? stateBinding=null) {
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
  return new(id,finding.RuleId,findingRef,refs,target,operation,finding.Observed,finding.Expected,d,executable,reason,stateBinding);
 }
}
