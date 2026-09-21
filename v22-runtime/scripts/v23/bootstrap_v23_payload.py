#!/usr/bin/env python3
from pathlib import Path
from zipfile import ZipFile
from io import BytesIO
import base64
import hashlib

ROOT = Path(__file__).resolve().parents[2]
parts = []
for i in range(1, 5):
    text = (ROOT / '.v23-payload' / f'part{i}v2.b64').read_text(encoding='utf-8').strip()
    # part3v2 was staged with an accidental trailing duplicate. The canonical
    # payload chunks are fixed at 5,225 characters; trim any staging surplus.
    parts.append(text[:5225])
raw = base64.b64decode(''.join(parts), validate=True)
actual = hashlib.sha256(raw).hexdigest()
expected = 'dfa454c9a3d0cdec8651e497bfe9eabf926c0ba4f5d3254aa929f77be5b5840c'
if actual != expected:
    raise SystemExit(f'v23 payload sha mismatch: expected={expected} actual={actual}')
with ZipFile(BytesIO(raw), 'r') as archive:
    bad = archive.testzip()
    if bad:
        raise SystemExit(f'v23 payload zip corrupt: {bad}')
    archive.extractall(ROOT)
print(f'v23 payload verified and extracted sha256={actual}')
