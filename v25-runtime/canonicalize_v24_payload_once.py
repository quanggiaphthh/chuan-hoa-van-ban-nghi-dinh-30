#!/usr/bin/env python3
from pathlib import Path
import base64, hashlib, io, lzma, tarfile

ROOT=Path(__file__).resolve().parents[1]
PAY=ROOT/'v24-runtime/.v24-payload/code'
MAT=ROOT/'v24-runtime/materialize_v24_compact.py'
WF=ROOT/'.github/workflows/v24-validator-gate.yml'
OLD='79c349ee1c4cb5e8fe58629018d9914d61c0a69e14c6f249490999483cd02e8f'
parts=[PAY/'part-00',PAY/'part-01']
raw=base64.b64decode(''.join(p.read_text().strip() for p in parts),validate=True)
assert hashlib.sha256(raw).hexdigest()==OLD
out=io.BytesIO(); changed=False
with tarfile.open(fileobj=io.BytesIO(raw),mode='r:xz') as src, tarfile.open(fileobj=out,mode='w:xz') as dst:
    for m in src.getmembers():
        data=src.extractfile(m).read() if m.isfile() else None
        if m.name.endswith('tests/LegalValidator.Tests/LegalValidatorTests.cs'):
            text=data.decode('utf-8')
            assert 'using Xunit;' not in text, 'baseline unexpectedly already canonicalized'
            assert text.count('fixtures","v23","01-named-decision.docx')==2, 'expected exactly two stale fixture references'
            text='using Xunit;\n'+text.replace('fixtures","v23","01-named-decision.docx','fixtures","v23","02-named-decision.docx')
            data=text.encode('utf-8'); m.size=len(data); changed=True
        dst.addfile(m,io.BytesIO(data) if data is not None else None)
assert changed
new=out.getvalue(); newsha=hashlib.sha256(new).hexdigest(); enc=base64.b64encode(new).decode('ascii')
(PAY/'part-00').write_text(enc[:8000],encoding='utf-8'); (PAY/'part-01').write_text(enc[8000:],encoding='utf-8')
text=MAT.read_text(); assert OLD in text; MAT.write_text(text.replace(OLD,newsha),encoding='utf-8')
wf=WF.read_text(); wf=wf.replace('      - feature/gd4-v24-validator-runtime','      - feature/gd4-v25-l1-canonical-runtime')
block='      - name: Apply V24 runtime corrections\n        run: python v24-runtime/apply_v24_runtime_corrections.py\n\n'
assert block in wf; wf=wf.replace(block,'      - name: Assert V24 canonical materialization\n        run: python v24-runtime/assert_v24_canonicalization.py\n\n')
WF.write_text(wf,encoding='utf-8')
guard=ROOT/'v24-runtime/assert_v24_canonicalization.py'
guard.write_text("""#!/usr/bin/env python3
from pathlib import Path
import base64, hashlib, io, tarfile
ROOT=Path(__file__).resolve().parents[1]
test=ROOT/'v22-runtime/tests/LegalValidator.Tests/LegalValidatorTests.cs'
text=test.read_text(encoding='utf-8')
assert 'using Xunit;' in text, 'materialized V24 test lacks using Xunit;'
assert 'fixtures\",\"v23\",\"01-named-decision.docx' not in text, 'materialized V24 test contains stale fixture path'
assert text.count('fixtures\",\"v23\",\"02-named-decision.docx') >= 2, 'canonical named-decision fixture reference missing'
parts=ROOT/'v24-runtime/.v24-payload/code'
raw=base64.b64decode((parts/'part-00').read_text().strip()+(parts/'part-01').read_text().strip(),validate=True)
with tarfile.open(fileobj=io.BytesIO(raw),mode='r:xz') as tf:
    member=next(m for m in tf.getmembers() if m.name.endswith('tests/LegalValidator.Tests/LegalValidatorTests.cs'))
    canonical=tf.extractfile(member).read().decode('utf-8')
assert 'using Xunit;' in canonical
assert 'fixtures\",\"v23\",\"01-named-decision.docx' not in canonical
assert not (ROOT/'v24-runtime/apply_v24_runtime_corrections.py').exists(), 'obsolete post-materialization correction script still exists'
workflow=(ROOT/'.github/workflows/v24-validator-gate.yml').read_text()
assert 'apply_v24_runtime_corrections.py' not in workflow
print('V25 L1 canonicalization guard PASS')
""",encoding='utf-8')
(ROOT/'v24-runtime/apply_v24_runtime_corrections.py').unlink()
(ROOT/'v25-runtime/canonicalize_v24_payload_once.py').unlink()
(ROOT/'.github/workflows/v25-l1-canonicalize-once.yml').unlink()
print('canonicalized V24 code payload',newsha)
