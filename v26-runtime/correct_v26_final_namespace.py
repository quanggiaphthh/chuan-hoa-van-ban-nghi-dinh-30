#!/usr/bin/env python3
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'v22-runtime/src/LegalValidator/Authorization/AuthorizationBoundary.cs'
s=p.read_text(); needle='namespace Nd30.LegalValidator.Authorization;'; first=s.find(needle); second=s.find(needle,first+len(needle))
if second<0: raise SystemExit('second namespace not found')
p.write_text(s[:second]+s[second+len(needle):])
e=root/'v22-runtime/src/LegalValidator/Execution/V26ExecutionSafety.cs'; x=e.read_text(); old='return new(new(q.RequestId'
if x.count(old)!=2: raise SystemExit(f'expected 2 ambiguous constructors, found {x.count(old)}')
e.write_text(x.replace(old,'return new AuthorizedPlanOperation(new PlanOperation(q.RequestId'))
print('V26 final namespace + authorized plan constructor correctives applied')
