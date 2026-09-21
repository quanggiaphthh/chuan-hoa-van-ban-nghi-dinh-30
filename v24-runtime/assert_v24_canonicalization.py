#!/usr/bin/env python3
from pathlib import Path
import base64,io,tarfile
ROOT=Path(__file__).resolve().parents[1]; test=ROOT/'v22-runtime/tests/LegalValidator.Tests/LegalValidatorTests.cs'; text=test.read_text()
assert 'using Xunit;' in text
assert 'fixtures","v23","01-named-decision.docx' not in text
assert text.count('fixtures","v23","02-named-decision.docx')>=2
p=ROOT/'v24-runtime/.v24-payload/code'; raw=base64.b64decode((p/'part-00').read_text().strip()+(p/'part-01').read_text().strip(),validate=True)
with tarfile.open(fileobj=io.BytesIO(raw),mode='r:xz') as tf:
 m=next(x for x in tf.getmembers() if x.name.endswith('tests/LegalValidator.Tests/LegalValidatorTests.cs')); c=tf.extractfile(m).read().decode()
assert 'using Xunit;' in c and 'fixtures","v23","01-named-decision.docx' not in c
assert not (ROOT/'v24-runtime/apply_v24_runtime_corrections.py').exists()
assert 'apply_v24_runtime_corrections.py' not in (ROOT/'.github/workflows/v24-validator-gate.yml').read_text()
print('V25 L1 canonicalization guard PASS')
