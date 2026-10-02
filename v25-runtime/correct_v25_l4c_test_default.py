#!/usr/bin/env python3
from pathlib import Path

OLD_DECLARATION = 'static string Doc(W.JustificationValues? a=W.JustificationValues.Left,bool prot=false){'
NEW_DECLARATION = 'static string Doc(W.JustificationValues? a=null,bool prot=false,bool missingDirect=false){if(a is null&&!missingDirect)a=W.JustificationValues.Left;'
OLD_CALL = 'var s=Doc(null);var o=s+".o";'
NEW_CALL = 'var s=Doc(null,missingDirect:true);var o=s+".o";'


def replace_or_accept(source: str, old: str, new: str, label: str) -> str:
    if new in source:
        return source
    if source.count(old) != 1:
        raise SystemExit(f"expected {label} source pattern not found or ambiguous")
    return source.replace(old, new, 1)


def correct_source(source: str) -> str:
    source = replace_or_accept(source, OLD_DECLARATION, NEW_DECLARATION, "CS1736")
    return replace_or_accept(source, OLD_CALL, NEW_CALL, "missing-direct fixture call")


def main() -> None:
    path = Path(__file__).resolve().parent / "materialize_v25_l4c.py"
    original = path.read_text(encoding="utf-8")
    corrected = correct_source(original)
    if corrected != original:
        path.write_text(corrected, encoding="utf-8")
        print("V25 L4C CS1736 test-generator corrective applied")
    else:
        print("V25 L4C CS1736 test-generator corrective already applied")


if __name__ == "__main__":
    main()
