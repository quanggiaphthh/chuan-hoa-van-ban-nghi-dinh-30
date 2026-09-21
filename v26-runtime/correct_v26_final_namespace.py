#!/usr/bin/env python3
from pathlib import Path
import runpy
root=Path(__file__).resolve().parents[1]
p=root/'v22-runtime/src/LegalValidator/Authorization/AuthorizationBoundary.cs'; s=p.read_text(); needle='namespace Nd30.LegalValidator.Authorization;'; first=s.find(needle); second=s.find(needle,first+len(needle))
if second<0: raise SystemExit('second namespace not found')
p.write_text(s[:second]+s[second+len(needle):])
e=root/'v22-runtime/src/LegalValidator/Execution/V26ExecutionSafety.cs'; x=e.read_text(); old='return new(new(q.RequestId'
if x.count(old)!=2: raise SystemExit(f'expected 2 ambiguous constructors, found {x.count(old)}')
x=x.replace(old,'return new AuthorizedPlanOperation(new PlanOperation(q.RequestId')
needle2='if(q.V26Intent is not null){var i=q.V26Intent;if(q.Authorization.MutationIntentId!=i.MutationIntentId'
replace2='if(q.V26Intent is not null){var i=q.V26Intent;var exact=AuthorizationArtifact.H(string.Join("|","v26-spacing",i.ProposalId,i.StateBinding.ExpectedDocument.Digest,i.RuleId,i.FindingReference,i.TargetPath,i.Property,i.Provenance,i.ExpectedBefore,i.DesiredAfter));if(exact!=i.MutationIntentId)throw new ArgumentException("V26 intent identity/semantics mismatch.");if(q.Authorization.MutationIntentId!=i.MutationIntentId'
if needle2 not in x: raise SystemExit('V26 intent binding insertion point not found')
e.write_text(x.replace(needle2,replace2))
t=root/'v22-runtime/tests/LegalValidator.Tests/V26FinalAuthorityRevalidationTests.cs'; z=t.read_text().replace('Assert.Null(Type.GetType("Nd30.LegalValidator.Execution.PlanOperation, LegalValidator")?.GetConstructors().FirstOrDefault());','Assert.False(Type.GetType("Nd30.LegalValidator.Execution.PlanOperation, LegalValidator")!.IsPublic);'); t.write_text(z)
runpy.run_path(str(root/'v26-runtime/materialize_v26_preserved_tests.py'),run_name='__main__')
print('V26 final authority/revalidation corrective + preserved execution coverage applied')
