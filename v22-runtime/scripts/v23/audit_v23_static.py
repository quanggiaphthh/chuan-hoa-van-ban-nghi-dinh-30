#!/usr/bin/env python3
from pathlib import Path
import json, re, sys

ROOT=Path(__file__).resolve().parents[2]
errors=[]
required=[
 'src/SemanticDetector/SemanticDetector.csproj',
 'src/SemanticDetector/Model/SemanticModels.cs',
 'src/SemanticDetector/Detection/AdministrativeSemanticDetector.cs',
 'tests/SemanticDetector.Tests/SemanticDetector.Tests.csproj',
 'tests/SemanticDetector.Tests/SemanticDetectorTests.cs',
 'scripts/v23/generate_semantic_fixtures.py',
 'fixtures/v23/expected.json',
]
for rel in required:
    if not (ROOT/rel).exists(): errors.append(f'missing {rel}')

fixtures=sorted((ROOT/'fixtures/v23').glob('*.docx'))
if len(fixtures)<9: errors.append(f'expected >=9 v23 DOCX fixtures, found {len(fixtures)}')
exp=json.loads((ROOT/'fixtures/v23/expected.json').read_text(encoding='utf-8'))
if set(exp)!=set(p.name for p in fixtures): errors.append('expected.json fixture set mismatch')

model=(ROOT/'src/SemanticDetector/Model/SemanticModels.cs').read_text(encoding='utf-8')
det=(ROOT/'src/SemanticDetector/Detection/AdministrativeSemanticDetector.cs').read_text(encoding='utf-8')
tests=(ROOT/'tests/SemanticDetector.Tests/SemanticDetectorTests.cs').read_text(encoding='utf-8')
sol=(ROOT/'DocumentEngine.slnx').read_text(encoding='utf-8')
for token in ['SemanticComponentCandidate','DocumentClassificationResult','TemplateMatchResult','SemanticAmbiguity','CompetingCandidateIds','RequiresReview']:
    if token not in model: errors.append(f'model missing {token}')
for token in ['NationalHeader','NationalMotto','IssuingAuthority','DocumentNumber','DocumentNotation','IssuePlaceAndDate','DocumentTypeHeading','SubjectNamedDocument','SubjectOfficialLetter','LegalBasisBlock','ArticleHeading','Clause','Point','Addressee','RecipientList','SigningAuthorityPrefix','SignerTitle','SignerName','AppendixNumber','AppendixTitle','CopyFormHeading','UrgencyMark','ClassificationMark','ContactInformation']:
    if token not in det: errors.append(f'detector missing role logic {token}')
for token in ['OfficialLetter','NamedAdministrativeDocument','Appendix','Copy','Unknown']:
    if token not in det: errors.append(f'document class missing {token}')
for token in ['Semantic_fixtures_are_valid_openxml','Ambiguity_requires_review_and_exposes_competing_candidates','Table_based_header_retains_table_cell_evidence','Missing_components_stay_unknown_not_false_positive']:
    if token not in tests: errors.append(f'test missing {token}')
if 'src/SemanticDetector/SemanticDetector.csproj' not in sol or 'tests/SemanticDetector.Tests/SemanticDetector.Tests.csproj' not in sol:
    errors.append('solution missing v23 projects')
if re.search(r'OpenAI|Gemini|HttpClient|WebRequest',det,re.I): errors.append('semantic detector must be deterministic/local; external AI/network reference found')
if 'structural auto-fix must remain disabled' not in det: errors.append('ambiguous classification safety diagnostic missing')

cap_path=ROOT/'runtime/capabilities/capability-registry.yaml'
if cap_path.exists():
    import yaml
    reg=yaml.safe_load(cap_path.read_text(encoding='utf-8'))
    ids={c['capability_id']:c for c in reg['capabilities']}
    for cid in ['semantic_component_detection','document_type_classification','hierarchy_parser','appendix_detection','copy_component_detection','template_matching']:
        if cid not in ids: errors.append(f'capability missing {cid}')
        elif ids[cid].get('runtime_result_when_missing')!='NOT_EVALUATED': errors.append(f'{cid}: missing fallback must remain NOT_EVALUATED')
else:
    print('capability-registry: not staged in runtime CI subset; full-package capability audit remains authoritative')

print('V23 STATIC AUDIT:', 'PASS' if not errors else 'FAIL')
print('fixtures:',len(fixtures))
print('errors:',len(errors))
for e in errors: print('-',e)
if errors: sys.exit(1)
