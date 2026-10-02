#!/usr/bin/env python3
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]


def replace_or_accept(source: str, old: str, new: str, label: str) -> str:
    if new in source:
        return source
    if source.count(old) != 1:
        raise SystemExit(f'expected one {label} source seam')
    return source.replace(old, new, 1)


def correct_fixture_generator(source: str) -> str:
    source = replace_or_accept(source, 'from zipfile import ZipFile, ZIP_DEFLATED',
                               'from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED', 'ZipInfo import')
    source = replace_or_accept(
        source,
        "CT='http://schemas.openxmlformats.org/package/2006/content-types'\n",
        "CT='http://schemas.openxmlformats.org/package/2006/content-types'\nZIP_TIME=(1980,1,1,0,0,0)\n\ndef add_entry(archive,name,data):\n    info=ZipInfo(name,ZIP_TIME);info.compress_type=ZIP_DEFLATED;info.create_system=3;info.external_attr=0o600<<16\n    archive.writestr(info,data)\n",
        'deterministic ZIP helper',
    )
    for name, value in [
        ('[Content_Types].xml', 'types'),
        ('_rels/.rels', 'rootrels'),
        ('word/document.xml', 'document'),
        ('word/styles.xml', 'styles'),
        ('word/settings.xml', 'settings'),
        ('word/_rels/document.xml.rels', 'rels'),
    ]:
        source = replace_or_accept(source, f"z.writestr('{name}',{value})",
                                   f"add_entry(z,'{name}',{value})", f'{name} ZIP entry')
    return source


def apply() -> None:
    # Keep the generated expectation matrix aligned with document-level review policy.
    gen = ROOT / 'scripts' / 'v23' / 'generate_semantic_fixtures.py'
    source = gen.read_text(encoding='utf-8')
    corrected = correct_fixture_generator(source)
    old = "'11-markings-signature.docx': {'class':'NamedAdministrativeDocument','specific':'thong_bao','review':True,"
    new = "'11-markings-signature.docx': {'class':'NamedAdministrativeDocument','specific':'thong_bao','review':False,"
    corrected = replace_or_accept(corrected, old, new, 'markings/signature expectation')
    if corrected != source:
        gen.write_text(corrected, encoding='utf-8')

    # Eliminate xUnit2031 without changing test semantics.
    test = ROOT / 'tests' / 'SemanticDetector.Tests' / 'SemanticDetectorTests.cs'
    source = test.read_text(encoding='utf-8')
    old = 'var nh=Assert.Single(s.Components.Where(x=>x.Role==SemanticRole.NationalHeader));'
    new = 'var nh=Assert.Single(s.Components, x=>x.Role==SemanticRole.NationalHeader);'
    corrected = replace_or_accept(source, old, new, 'Assert.Single')
    if corrected != source:
        test.write_text(corrected, encoding='utf-8')

    print('v23 runtime corrections applied')


if __name__ == '__main__':
    apply()
