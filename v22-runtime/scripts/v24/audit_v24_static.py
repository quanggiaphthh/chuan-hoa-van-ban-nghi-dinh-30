#!/usr/bin/env python3
from pathlib import Path
import yaml, sys
ROOT=Path(__file__).resolve().parents[2]
errors=[]
required=[
 'src/LegalValidator/LegalValidator.csproj','src/LegalValidator/Model/ValidationModels.cs','src/LegalValidator/Catalog/RuleCatalog.cs',
 'src/LegalValidator/Evaluation/ValidationContext.cs','src/LegalValidator/Evaluation/RuleEvaluator.cs','src/LegalValidator/Evaluation/ValidationEngine.cs',
 'src/LegalValidator/Adapters/SemanticValidationContextBuilder.cs','tests/LegalValidator.Tests/LegalValidator.Tests.csproj','tests/LegalValidator.Tests/LegalValidatorTests.cs']
for x in required:
 if not (ROOT/x).exists(): errors.append(f'missing:{x}')
pack=yaml.safe_load((ROOT/'release/admin-nd30-verified-rc-v20.yaml').read_text(encoding='utf-8'))
if len(pack.get('rules',[]))!=435: errors.append('verified_pack_count_not_435')
byid={}
for p in (ROOT/'rules').rglob('*.yaml'):
 d=yaml.safe_load(p.read_text(encoding='utf-8'))
 if isinstance(d,dict) and d.get('id'):byid[d['id']]=d
for rid in pack.get('rules',[]):
 if rid not in byid:errors.append(f'missing_rule:{rid}')
 elif byid[rid].get('maturity')!='verified':errors.append(f'non_verified_in_pack:{rid}')
text=(ROOT/'src/LegalValidator/Evaluation/RuleEvaluator.cs').read_text(encoding='utf-8')
for token in ['NOT_EVALUATED','NOT_APPLICABLE','NEEDS_REVIEW','workflow_event_log','system_capability_audit','same_day_or_next_workday']:
 if token not in text:errors.append(f'evaluator_missing:{token}')
eng=(ROOT/'src/LegalValidator/Evaluation/ValidationEngine.cs').read_text(encoding='utf-8')
model=(ROOT/'src/LegalValidator/Model/ValidationModels.cs').read_text(encoding='utf-8')
if 'FullComplianceClaimAllowed' not in model or 'EvaluatedCoveragePercent' not in model or 'Full compliance cannot be claimed' not in eng:errors.append('coverage_contract_missing')
test=(ROOT/'tests/LegalValidator.Tests/LegalValidatorTests.cs').read_text(encoding='utf-8')
for token in ['435','Missing_capability_is_not_evaluated','Workflow_rule_rejects_docx_metadata','Full_verified_pack_never_claims_compliance_from_docx_only']:
 if token not in test:errors.append(f'test_missing:{token}')
print(f'v24 static audit: verified_rules={len(pack.get("rules",[]))} errors={len(errors)}')
for e in errors: print('ERROR',e)
sys.exit(1 if errors else 0)
