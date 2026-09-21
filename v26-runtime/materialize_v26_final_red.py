#!/usr/bin/env python3
from pathlib import Path
root=Path(__file__).resolve().parents[1]
p=root/'v22-runtime/tests/LegalValidator.Tests/V26FinalGapRedTests.cs'
p.write_text(r'''using Xunit;using Nd30.LegalValidator.Execution;namespace Nd30.LegalValidator.Tests;public sealed class V26FinalGapRedTests{
[Fact]public void Executable_plan_has_no_raw_string_authority_factory(){Assert.DoesNotContain(typeof(MutationPlan).GetMethods(),m=>m.Name=="Create"&&m.GetParameters().Any(p=>p.ParameterType==typeof(string)));}
[Fact]public void Production_executor_has_no_arbitrary_revalidation_delegate(){Assert.DoesNotContain(typeof(AtomicMutationPlanExecutor).GetMethods(),m=>m.Name=="Execute"&&m.GetParameters().Any(p=>p.ParameterType.IsGenericType&&p.ParameterType.GetGenericTypeDefinition()==typeof(Func<,>)));}
}''',encoding='utf-8')
print('V26 final gap RED tests materialized')
