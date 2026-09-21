#!/usr/bin/env python3
from pathlib import Path
import base64, hashlib, io, tarfile
ROOT=Path(__file__).resolve().parents[1]; PAY=ROOT/'v24-runtime/.v24-payload/code'; MAT=ROOT/'v24-runtime/materialize_v24_compact.py'
OLD='79c349ee1c4cb5e8fe58629018d9914d61c0a69e14c6f249490999483cd02e8f'
raw=base64.b64decode((PAY/'part-00').read_text().strip()+(PAY/'part-01').read_text().strip(),validate=True); assert hashlib.sha256(raw).hexdigest()==OLD
out=io.BytesIO(); changed=False
with tarfile.open(fileobj=io.BytesIO(raw),mode='r:xz') as src, tarfile.open(fileobj=out,mode='w:xz') as dst:
    for m in src.getmembers():
        data=src.extractfile(m).read() if m.isfile() else None
        if m.name.endswith('tests/LegalValidator.Tests/LegalValidatorTests.cs'):
            text=data.decode(); assert 'using Xunit;' not in text; assert text.count('fixtures","v23","01-named-decision.docx')==2
            text='using Xunit;\n'+text.replace('fixtures","v23","01-named-decision.docx','fixtures","v23","02-named-decision.docx'); data=text.encode(); m.size=len(data); changed=True
        dst.addfile(m,io.BytesIO(data) if data is not None else None)
assert changed
new=out.getvalue(); newsha=hashlib.sha256(new).hexdigest(); enc=base64.b64encode(new).decode(); (PAY/'part-00').write_text(enc[:8000]); (PAY/'part-01').write_text(enc[8000:])
t=MAT.read_text(); assert OLD in t; MAT.write_text(t.replace(OLD,newsha))
guard=ROOT/'v24-runtime/assert_v24_canonicalization.py'; guard.write_text("""#!/usr/bin/env python3
from pathlib import Path
import base64,io,tarfile
ROOT=Path(__file__).resolve().parents[1]; test=ROOT/'v22-runtime/tests/LegalValidator.Tests/LegalValidatorTests.cs'; text=test.read_text()
assert 'using Xunit;' in text
assert 'fixtures\",\"v23\",\"01-named-decision.docx' not in text
assert text.count('fixtures\",\"v23\",\"02-named-decision.docx')>=2
p=ROOT/'v24-runtime/.v24-payload/code'; raw=base64.b64decode((p/'part-00').read_text().strip()+(p/'part-01').read_text().strip(),validate=True)
with tarfile.open(fileobj=io.BytesIO(raw),mode='r:xz') as tf:
 m=next(x for x in tf.getmembers() if x.name.endswith('tests/LegalValidator.Tests/LegalValidatorTests.cs')); c=tf.extractfile(m).read().decode()
assert 'using Xunit;' in c and 'fixtures\",\"v23\",\"01-named-decision.docx' not in c
assert not (ROOT/'v24-runtime/apply_v24_runtime_corrections.py').exists()
assert 'apply_v24_runtime_corrections.py' not in (ROOT/'.github/workflows/v24-validator-gate.yml').read_text()
print('V25 L1 canonicalization guard PASS')
""")
(ROOT/'v24-runtime/apply_v24_runtime_corrections.py').unlink(); (ROOT/'v25-runtime/canonicalize_v24_payload_once.py').unlink(); (ROOT/'.github/workflows/v25-l1-canonicalize-once.yml').unlink()
print('canonicalized V24 code payload',newsha)
