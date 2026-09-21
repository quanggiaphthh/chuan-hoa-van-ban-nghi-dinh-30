#!/usr/bin/env python3
from pathlib import Path
from zipfile import ZipFile, ZIP_DEFLATED
import tempfile, os

ROOT = Path(__file__).resolve().parents[2] / "fixtures" / "docx"

def rewrite(path: Path, transform):
    with ZipFile(path, "r") as zin:
        entries = {name: zin.read(name) for name in zin.namelist()}
    xml = entries["word/document.xml"].decode("utf-8")
    entries["word/document.xml"] = transform(xml).encode("utf-8")
    fd, tmp = tempfile.mkstemp(suffix=path.suffix, dir=path.parent)
    os.close(fd)
    try:
        with ZipFile(tmp, "w", ZIP_DEFLATED) as zout:
            for name, data in entries.items():
                zout.writestr(name, data)
        os.replace(tmp, path)
    finally:
        if os.path.exists(tmp):
            os.unlink(tmp)

def fix_table(xml: str) -> str:
    old = "<w:tbl><w:tr>"
    new = '<w:tbl><w:tblPr/><w:tblGrid><w:gridCol w:w="3000"/><w:gridCol w:w="3000"/></w:tblGrid><w:tr>'
    if old not in xml:
        raise SystemExit("expected table fixture pattern not found")
    return xml.replace(old, new, 1)

def fix_page_numbering(xml: str) -> str:
    old = '<w:sectPr><w:pgNumType w:start="3" w:fmt="decimal"/><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701"/></w:sectPr>'
    new = '<w:sectPr><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701"/><w:pgNumType w:start="3" w:fmt="decimal"/></w:sectPr>'
    if old not in xml:
        raise SystemExit("expected page-numbering section pattern not found")
    return xml.replace(old, new, 1)

def fix_header_footer_section(xml: str) -> str:
    old = '<w:titlePg/><w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701"/>'
    new = '<w:pgSz w:w="11906" w:h="16838"/><w:pgMar w:top="1134" w:right="850" w:bottom="1134" w:left="1701"/><w:titlePg/>'
    if old not in xml:
        raise SystemExit("expected header-footer section pattern not found")
    return xml.replace(old, new, 1)

rewrite(ROOT / "07-table.docx", fix_table)
rewrite(ROOT / "08-header-footer.docx", fix_header_footer_section)
rewrite(ROOT / "09-page-numbering.docx", fix_page_numbering)
print("normalized table/header-footer/page-numbering fixtures for OpenXml validation")
