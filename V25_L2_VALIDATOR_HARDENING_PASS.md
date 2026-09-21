# V25 L2 VALIDATOR HARDENING PASS

Repository: quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30
Branch: feature/gd4-v25-l2-validator-hardening
Canonical baseline: b89f0d24f1086846d2d25b053db5b40bca3c296a
Implementation evidence SHA: 6cc44722bdeed926932c2e83f43bf6bc2c59ea81
Implementation Actions run: 35556138945 — SUCCESS
Checkpoint final SHA: the Git commit containing this checkpoint; fresh CI on that SHA is required before FINAL PASS declaration.
Timestamp: 2026-09-21T03:03:20Z

## Gap matrix summary
Already safe: missing required capability -> NOT_EVALUATED; missing observation -> NOT_EVALUATED; non-applicable and temporally inactive -> NOT_APPLICABLE; constraint evaluator exceptions -> NOT_EVALUATED; evidence/provenance retained in ValidationResult; aggregate full-compliance claim requires 100% evaluated applicable rules with no FAIL/NEEDS_REVIEW/NOT_EVALUATED.
Implemented: unsupported applicability operator previously collapsed to false/NOT_APPLICABLE; now tri-state evaluation maps unsupported operator to NOT_EVALUATED with diagnostic reason.
Deferred: malformed/corrupt DOCX and unsupported document structures remain Document Engine boundary concerns already covered by V22 safety tests; large evidence-schema redesign, contradiction model, universal confidence thresholds, and new overall verdicts are out of scope.
Architectural gaps: EvidenceAuthority is intentionally narrow (Derived/Authoritative); no generic contradiction state or universal confidence threshold exists, so L2 does not invent either.

## Invariants protected
No false PASS; missing capability is not PASS; missing observation/evidence does not become PASS where current rule contract requires it; applicability and temporality remain non-failing when inactive; evaluator errors are isolated as NOT_EVALUATED; evidence reference/source/rule provenance is preserved; aggregate safety remains conservative.

## Production changes
v25-runtime/materialize_v25_l2.py applies one deterministic V25 overlay to RuleEvaluator after verified V24 materialization: applicability Condition becomes bool? and unsupported operators return NOT_EVALUATED. The same materializer supplies seven targeted regression tests. V24 payload and V22/V23 production semantics are unchanged.

## TDD evidence
RED: Unsupported_applicability_operator_is_not_evaluated failed before production hardening: expected NOT_EVALUATED, actual NOT_APPLICABLE.
GREEN targeted: 7/7 PASS.

## Regression matrix
V22 Document Engine: 31/31 PASS.
V23 Semantic Detector: 13/13 PASS.
V24 + V25 Legal Validator: 25/25 PASS (18 baseline + 7 V25 L2).
Total: 69/69 PASS; failed 0; skipped 0.
Build: PASS, 0 warnings, 0 errors.
V22/V23/V24 static audits: PASS.
V25 L1 canonicalization guard: PASS before V25 overlay and verified again in final static sequence.
Packaging: PASS.
Enforce gate: PASS.

## Known limitations / deferred architectural gaps
No new OCR, shapes, tracked-change, comments/footnotes support; no patch/autofix redesign; no evidence contradiction schema; no universal confidence threshold; no new compliance verdict.

V22, V23 and V24 semantics remain locked. Main must remain b89f0d24f1086846d2d25b053db5b40bca3c296a. No merge to main is part of this checkpoint.
