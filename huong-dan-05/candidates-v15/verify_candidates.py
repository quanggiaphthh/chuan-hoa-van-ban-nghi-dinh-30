#!/usr/bin/env python3
"""Static candidate-only audit. Never imports Party rules into the runtime."""
import json
from pathlib import Path
import sys
import yaml
from jsonschema import Draft202012Validator, FormatChecker

HERE = Path(__file__).resolve().parent
REPO = HERE.parents[1]
SCHEMA = json.loads((REPO / "schemas/rule.schema.json").read_text(encoding="utf-8"))
files = sorted(HERE.glob("[0-9][0-9]-*.yaml"))
assert len(files) == 12, f"Expected 12 candidate YAML files, got {len(files)}"

# Values independently transcribed from official HD05, Appendix 2, PDF 1-indexed
# pages 31–32 and Part II.IV.4.1, PDF page 28.
expected = [
    ("PL2.ROW01.PARTY_HEADING.SIZE", 15, None, 31),
    ("PL2.ROW01.PARTY_HEADING.BOLD", True, None, 31),
    ("PL2.ROW02.ISSUER.SIZE", 14, None, 31),
    ("PL2.ROW03.NUMBER.SIZE", 14, None, 31),
    ("PL2.ROW05.TYPE.SIZE", 15, 16, 31),
    ("PL2.ROW05.NAMED_SUBJECT.SIZE", 14, 15, 31),
    ("PL2.ROW05.LETTER_SUBJECT.SIZE", 12, None, 32),
    ("PL2.ROW06.BODY.SIZE", 14, 15, 32),
    ("PARTII.IV.4.1.1.MARGIN_TOP", 20, None, 28),
    ("PARTII.IV.4.1.2.MARGIN_BOTTOM", 20, None, 28),
    ("PARTII.IV.4.1.3.MARGIN_LEFT", 30, None, 28),
    ("PARTII.IV.4.1.4.MARGIN_RIGHT", 15, None, 28),
]
seen = set()
validator = Draft202012Validator(SCHEMA, format_checker=FormatChecker())
for i, (file, (suffix, minimum, maximum, page)) in enumerate(zip(files, expected), 1):
    rule = yaml.safe_load(file.read_text(encoding="utf-8"))
    errors = list(validator.iter_errors(rule))
    if errors:
        raise AssertionError(f"{file}: schema errors: " + "; ".join(str(e) for e in errors))
    rule_id = f"PARTY.HD05.2026.{suffix}"
    assert rule["id"] == rule_id, (file, rule["id"], rule_id)
    assert rule_id not in seen, f"Duplicate ID: {rule_id}"
    seen.add(rule_id)
    assert rule["regime"] == "party" and rule["domain"] == "document_format"
    assert rule["source"]["source_id"] == "VN-PARTY-HD05-2026"
    assert isinstance(rule["source"]["locator"], dict)
    assert rule["source"]["locator"]["pdf_page"] == page
    assert rule["maturity"] == "extracted", "Do not mark a candidate verified"
    assert rule["autofix"]["policy"] == "PROHIBITED"
    assert rule["temporality"]["effective_from"] == "2026-05-27"
    assert rule["extensions"]["implementation_status"] == "NOT_IN_RUNTIME"
    assert rule["extensions"]["confirmation_state"] == "NEEDS_INDEPENDENT_LEGAL_REVIEW"
    constraint = rule["constraint"]
    if maximum is None:
        assert constraint == {"type": "equals", "value": minimum}, (rule_id, constraint)
    else:
        assert constraint["type"] == "numeric_range" and constraint["min"] == minimum and constraint["max"] == maximum, rule_id
    conditions = {c["field"]: c["value"] for c in rule["applicability"]["all"]}
    assert conditions["regime.value"] == "party"
    assert conditions["document.class"] == "official_party_document"
    if i >= 9:
        assert conditions["document.page_orientation"] == "portrait"
        assert "verified_page_orientation" in rule["requires_capability"]
        if i in (9,10):  # top/bottom: still 20 mm on duplex back pages
            assert "document.page_side" not in conditions
            assert constraint["value"] == 20
        else:  # left/right: mirrored only on duplex back pages
            assert conditions["document.page_side"] == "front_or_single"
            assert {"verified_print_side", "confirmed_duplex_or_single_sided_intent"}.issubset(
                set(rule["requires_capability"]))
            assert constraint["value"] in (15, 30)
    elif i == 7:
        assert conditions["document.type"] == "official_letter"
    elif i in (5,6):
        assert conditions["document.heading_variant"] == "named"
    assert len(rule["requires_capability"]) == len(set(rule["requires_capability"]))
    assert "confirmed_party_regime" in rule["requires_capability"]
    # A schema-compliant candidate must still be blocked by policy/review.
    assert rule["validation"]["severity"] == "LEGAL_WARNING"
runtime_release = json.loads((REPO / "v22-runtime/release/admin-nd30-verified-rc-v20.yaml").read_text(encoding="utf-8"))
assert len(runtime_release["rules"]) == 435
assert all(r.startswith("ND30.") for r in runtime_release["rules"])
assert seen.isdisjoint(runtime_release["rules"]), "Party draft appeared in ND30 verified release"
party_selector = (REPO / "rule-packs/party-document-format-2026.yaml").read_text(encoding="utf-8")
assert "VN-PARTY-HD05-2026" in party_selector
assert "candidates-v15" not in party_selector, "Candidate files may never be activated through selector"
print("HD05 V15 STATIC AUDIT PASS — 12/12 schema-valid extracted candidates; 12/12 values/source pages; 0 verified; 0 autofix; ND30 release 435 unchanged.")

# V16: negative applicability matrix. This tests only the EXTRACTED YAML
# conditions, not runtime support, renderer parity, or legal certification.
def eligible_for_review(rule, facts, capabilities):
    if not set(rule["requires_capability"]).issubset(capabilities):
        return False
    return all(
        c["operator"] == "equals" and
        c["field"] in facts and facts[c["field"]] == c["value"]
        for c in rule["applicability"]["all"]
    )

def read(n):
    return yaml.safe_load(files[n - 1].read_text(encoding="utf-8"))

general = {"regime.value": "party", "document.class": "official_party_document",
           "document.page_orientation": "portrait", "document.page_side": "front_or_single"}
for n in range(9, 13):
    rule = read(n)
    caps = set(rule["requires_capability"])
    assert eligible_for_review(rule, general, caps), f"Expected candidate to match front/single portrait {n}"
    assert not eligible_for_review(rule, general | {"document.page_orientation": "landscape"}, caps)
    assert not eligible_for_review(rule, general | {"regime.value": "administrative"}, caps)
    assert not eligible_for_review(rule, general | {"document.class": "specialized"}, caps)
    assert not eligible_for_review(rule, general, caps - {"verified_page_orientation"})
    back = general | {"document.page_side": "back"}
    if n in (9,10):
        assert eligible_for_review(rule, back, caps), "Top/bottom must apply on duplex back"
    else:
        assert not eligible_for_review(rule, back, caps), "Front left/right cannot apply on duplex back"
        assert not eligible_for_review(rule, general, caps - {"verified_print_side"})
        assert not eligible_for_review(rule, general, caps - {"confirmed_duplex_or_single_sided_intent"})
        assert not eligible_for_review(rule, {k:v for k,v in general.items() if k != "document.page_side"}, caps)

# Mixed-run and uncertain-role formatting cannot be marked as observable.
for n in range(1, 9):
    rule = read(n)
    facts = {c["field"]: c["value"] for c in rule["applicability"]["all"]}
    caps = set(rule["requires_capability"])
    assert "effective_style_complete_span" in caps
    assert eligible_for_review(rule, facts, caps)
    assert not eligible_for_review(rule, facts, caps - {"effective_style_complete_span"})
    assert not eligible_for_review(rule, facts, caps - {"verified_semantic_role"})
    assert not eligible_for_review(rule, facts | {"regime.value": "administrative"}, caps)
    if n == 7:
        assert not eligible_for_review(rule, facts | {"document.type": "decision"}, caps)
    if n in (5, 6):
        assert not eligible_for_review(rule, facts | {"document.heading_variant": "letter"}, caps)

print("HD05 V16 APPLICABILITY STATIC MATRIX PASS — 4 margin cases and 8 formatting roles; portrait/duplex/landscape, role, regime, incomplete-run negatives. NOT a DOCX runtime test.")
