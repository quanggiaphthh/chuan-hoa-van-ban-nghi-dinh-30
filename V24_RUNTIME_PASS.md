# V24 RUNTIME PASS

## Canonical scope

- Repository: quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30
- Branch: feature/gd4-v24-validator-runtime
- Verified implementation SHA: 1bcbe17be50fc9662e8b9d80944762ee6f6bd0c5
- GitHub Actions run: 35553459781
- Workflow: V24 Legal Validator Runtime Gate
- Runtime evidence UTC: 2026-09-21T02:13:23Z through 2026-09-21T02:14:00Z
- Conclusion: SUCCESS

## Correctives verified by CI

1. Python static-audit dependency: PyYAML==6.0.3.
2. Canonical V24 runtime payload restored on the V24 feature branch; main was not used as the working branch.
3. Materialized LegalValidatorTests correction adds the missing `using Xunit;` import.
4. Materialized LegalValidatorTests correction points the named-decision integration tests to the actual V23 fixture `fixtures/v23/02-named-decision.docx` instead of the nonexistent `01-named-decision.docx`.

## Runtime matrix

- Checkout: PASS, exact SHA 1bcbe17be50fc9662e8b9d80944762ee6f6bd0c5.
- .NET 10 setup: PASS; SDK 10.0.401.
- Python setup: PASS; Python 3.12.14.
- PyYAML: PASS; 6.0.3 installed and imported.
- V22 static audit: PASS; fixtures=22, errors=0.
- V23 static audit: PASS; fixtures=11, errors=0.
- V24 static audit: PASS; verified_rules=435, errors=0.
- Restore: PASS.
- Build: PASS; 0 warnings, 0 errors.
- DocumentEngine.Tests: 31/31 PASS.
- SemanticDetector.Tests: 13/13 PASS.
- LegalValidator.Tests: 18/18 PASS.
- Total runtime tests: 62 PASS, 0 failed, 0 skipped.
- Final V22/V23/V24 static regression: PASS.
- Runtime source package creation: PASS.
- Gate enforcement: PASS.

## Known limitations / warnings

- GitHub runner reports Node.js 20 deprecation warnings for current action versions forced onto Node 24; these warnings did not fail the gate.
- `materialize_v24_compact.py` emits a Python 3.14 future-behavior DeprecationWarning for `tarfile.extractall`; CI currently uses Python 3.12.14 and the gate passes.
- V23 static audit notes that the capability registry is not staged in the runtime CI subset; the full-package capability audit remains authoritative.

## Lock statement

V24 Legal Validator runtime verification is PASS for the verified implementation SHA and run above. V22 Document Engine and V23 Semantic Detector remain locked except for regression verification. The `main` branch was not merged or modified by this V24 verification/fix loop. The V24 feature branch remains unmerged pending a separate instruction.
