#!/usr/bin/env python3
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'v22-runtime/src/LegalValidator/Execution/AtomicMutationPlanExecutor.cs'
s=p.read_text(encoding='utf-8')
s=s.replace('using Nd30.DocumentEngine.Model;using Nd30.DocumentEngine.Package;', 'using Nd30.DocumentEngine.Model;using Nd30.DocumentEngine.Package;using Nd30.LegalValidator.State;')
p.write_text(s,encoding='utf-8')
t=root/'v22-runtime/tests/LegalValidator.Tests/V26AtomicExecutionTests.cs'
s=t.read_text(encoding='utf-8').replace('using Nd30.DocumentEngine.Model;using Nd30.LegalValidator.Execution;', 'using Nd30.DocumentEngine.Model;using Nd30.LegalValidator.State;using Nd30.LegalValidator.Execution;')
t.write_text(s,encoding='utf-8')
print('V26 atomic namespace corrective applied')
