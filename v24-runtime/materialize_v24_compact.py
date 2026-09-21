#!/usr/bin/env python3
from pathlib import Path
import base64, hashlib, json, lzma, tarfile, io, shutil

HERE = Path(__file__).resolve().parent
REPO = HERE.parent
TARGET = REPO / 'v22-runtime'
PAYLOAD = HERE / '.v24-payload'

CODE_SHA = 'e6615e19f1f31b787803163d209dbb7ddecdb28aa300e570aa0e67476a03eb88'
RULES_SHA = '9d8dbd90e6d5e944ebf34f3f4ca5564c385ba8563f5332f1640f9d55ae152865'


def decode_parts(folder: Path, names: list[str]) -> bytes:
    text = ''.join((folder / n).read_text(encoding='utf-8').strip() for n in names)
    return base64.b64decode(text, validate=True)

code_xz = decode_parts(PAYLOAD / 'code', ['part-00','part-01'])
actual = hashlib.sha256(code_xz).hexdigest()
if actual != CODE_SHA:
    raise SystemExit(f'v24 code payload sha mismatch expected={CODE_SHA} actual={actual}')
with tarfile.open(fileobj=io.BytesIO(code_xz), mode='r:xz') as tf:
    members = tf.getmembers()
    for m in members:
        resolved = (TARGET / m.name).resolve()
        if TARGET.resolve() not in resolved.parents and resolved != TARGET.resolve():
            raise SystemExit(f'unsafe tar path: {m.name}')
    tf.extractall(TARGET)

rules_xz = decode_parts(PAYLOAD / 'rules', ['part-00','part-01','part-02','part-03'])
actual = hashlib.sha256(rules_xz).hexdigest()
if actual != RULES_SHA:
    raise SystemExit(f'v24 rules payload sha mismatch expected={RULES_SHA} actual={actual}')
data = json.loads(lzma.decompress(rules_xz).decode('utf-8'))
release = data['release']
rules = data['rules']
if len(release.get('rules', [])) != 435 or len(rules) != 435:
    raise SystemExit(f'expected 435 release rules and 435 rule objects; got {len(release.get("rules", []))}/{len(rules)}')
byid = {r.get('id'): r for r in rules}
if len(byid) != 435 or set(release['rules']) != set(byid):
    raise SystemExit('compact rule catalog IDs do not exactly match verified release pack')
if any(r.get('maturity') != 'verified' for r in rules):
    raise SystemExit('compact rule catalog contains non-verified rule')

release_dir = TARGET / 'release'
rules_dir = TARGET / 'rules' / 'generated-v24'
release_dir.mkdir(parents=True, exist_ok=True)
if rules_dir.exists():
    shutil.rmtree(rules_dir)
rules_dir.mkdir(parents=True)
(release_dir / 'admin-nd30-verified-rc-v20.yaml').write_text(
    json.dumps(release, ensure_ascii=False, separators=(',',':')), encoding='utf-8')
for i, rid in enumerate(release['rules']):
    (rules_dir / f'{i:03d}.yaml').write_text(
        json.dumps(byid[rid], ensure_ascii=False, separators=(',',':')), encoding='utf-8')

print(f'v24 compact payload materialized: code_sha={CODE_SHA} rules_sha={RULES_SHA} rules={len(rules)}')
