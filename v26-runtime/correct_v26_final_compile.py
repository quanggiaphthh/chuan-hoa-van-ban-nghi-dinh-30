#!/usr/bin/env python3
from pathlib import Path
p=Path(__file__).resolve().parents[1]/'v22-runtime/src/LegalValidator/Execution/V26ExecutionSafety.cs'
s=p.read_text()
old='return new(new(q.RequestId'
if s.count(old)!=2: raise SystemExit(f'expected 2 ambiguous constructors, found {s.count(old)}')
s=s.replace(old,'return new AuthorizedPlanOperation(new PlanOperation(q.RequestId')
p.write_text(s)
print('V26 authorized plan constructor corrective applied')
