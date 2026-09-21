#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]

# Keep the generated expectation matrix aligned with the document-level review policy:
# optional low-confidence component heuristics do not by themselves force whole-document review.
gen = ROOT / 'scripts' / 'v23' / 'generate_semantic_fixtures.py'
text = gen.read_text(encoding='utf-8')
old = "'11-markings-signature.docx': {'class':'NamedAdministrativeDocument','specific':'thong_bao','review':True,"
new = "'11-markings-signature.docx': {'class':'NamedAdministrativeDocument','specific':'thong_bao','review':False,"
if old not in text and new not in text:
    raise SystemExit('expected markings/signature fixture expectation pattern not found')
if old in text:
    gen.write_text(text.replace(old, new, 1), encoding='utf-8')

# Eliminate the xUnit2031 analyzer warning without changing test semantics.
test = ROOT / 'tests' / 'SemanticDetector.Tests' / 'SemanticDetectorTests.cs'
text = test.read_text(encoding='utf-8')
old = 'var nh=Assert.Single(s.Components.Where(x=>x.Role==SemanticRole.NationalHeader));'
new = 'var nh=Assert.Single(s.Components, x=>x.Role==SemanticRole.NationalHeader);'
if old not in text and new not in text:
    raise SystemExit('expected Assert.Single pattern not found')
if old in text:
    test.write_text(text.replace(old, new, 1), encoding='utf-8')

print('v23 runtime corrections applied')
