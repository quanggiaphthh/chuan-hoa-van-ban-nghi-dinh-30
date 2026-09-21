# V25 L3 PATCH SAFETY PASS

- Repository: quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30
- Branch: feature/gd4-v25-l3-patch-safety
- Canonical baseline: 2da5f093f8248773463ed63c4aca0acd9713df11
- Verified source SHA before checkpoint: 59722c4b6bc6776be64fed141d5a73f49567cf56
- Source verification Actions run: 35556635825 (SUCCESS)
- Timestamp: 2026-09-21T10:11+07:00

## Architecture map
Validator evaluates only. V25 L3 adds a pure remediation proposal/eligibility layer. It has no DOCX/file write API. Mutation authority remains separate and is not implemented by this layer. Existing V22 PackageSafetyPreflight is the authority for readable/signed/protected/unsupported package signals.

## Gap matrix summary
Already safe: validator has PatchEligibility and V22 package safety policy; signed/protected/tracked-change packages are AUDIT_ONLY; unsupported/unreadable packages are PROHIBITED. Implemented: explicit immutable remediation proposal boundary with deterministic identity, provenance, target disambiguation and fail-closed eligibility. Deferred: stale document fingerprint/version precondition, patch application/idempotency, overlap/conflict resolver, cryptographic signature validity. These are architectural gaps because no canonical mutation/application API or document-state fingerprint contract exists. OCR, new signature parser, shapes/comments/footnotes/tracked-change mutation remain out of scope.

## RED evidence
L3 tests were materialized before production RemediationPlanner. The RED test step failed as required and the CI gate explicitly enforced that failure before materializing production code. Initial GREEN run exposed one test-harness defect: record equality compared array references in the determinism assertion; production build was already successful. The assertion was corrected to compare deterministic proposal semantics, then fresh run 35556635825 passed.

## Production changes
`v25-runtime/materialize_v25_l3.py` materializes `RemediationPlanner`, `RemediationProposal`, typed `RemediationDecision`, and 10 targeted tests. The planner consumes ValidationResult + PackageSafetyState and never writes a document.

## Safety invariants
- Finding is not write authority.
- PASS, NOT_EVALUATED, NOT_APPLICABLE, NEEDS_REVIEW are non-executable.
- FAIL without evidence reference or deterministic target is non-executable.
- Multiple targets are blocked ambiguous.
- Unsupported/unreadable/PROHIBITED packages are blocked.
- Signed/protected/AUDIT_ONLY packages are blocked.
- RuleId, finding reference, evidence references and target identity are preserved.
- Same semantic input produces the same proposal id/decision/target.
- Static gate rejects common write primitives in the remediation namespace.
- ELIGIBLE means technically eligible only; mutation still requires a separate authorized path.

## Tests / regression
- Existing locked floor: 69 tests.
- V25 L3 targeted: 10 tests.
- Expected combined suite: 79 tests; GitHub Actions full regression step SUCCESS.
- Failed: 0 in successful source verification run.
- Skipped: 0 in successful source verification run.
- Build: SUCCESS.
- Static audits: SUCCESS.
- Packaging: SUCCESS.
- Enforce gate: SUCCESS.

## Known limitations / deferred gaps
No patch application API exists in L3, therefore stale-precondition rejection and apply-twice idempotency are not claimed. No cryptographic digital-signature parser was added; only existing OPC signature-structure detection is consumed. No conflict resolver was introduced because L3 does not batch/apply patches.

V22, V23, V24, V25 L1 and V25 L2 remain locked. Main is unchanged by this branch. No merge, force-push or history rewrite was performed.
