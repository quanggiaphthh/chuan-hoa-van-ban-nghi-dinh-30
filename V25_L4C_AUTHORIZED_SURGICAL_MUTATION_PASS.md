# GĐ4 — V25 L4C AUTHORIZED SURGICAL MUTATION — CHECKPOINT

## Repository state

- Repository: `quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30`
- Branch: `feature/gd4-v25-l4c-authorized-surgical-mutation`
- Canonical baseline: `8406492df9970b74177b65186dcc3a3e04719dcd`
- Verified implementation/workflow SHA before this checkpoint: `c6c1575d1cc8c57e29bd2e9633be92a9149efa09`
- Fresh implementation run: `35645806956`
- Run conclusion: `SUCCESS`

This document is a checkpoint. FINAL PASS requires a separate fresh GitHub Actions SUCCESS on the checkpoint commit itself.

## Scope

L4C introduces exactly one authorized surgical mutation family:

- target: one paragraph;
- property: paragraph alignment;
- source provenance: existing direct formatting only;
- operation: exact authorized before → after alignment;
- output: a new DOCX file; the source is never the mutation target.

No generic mutation engine, style/inherited-format mutation, batch mutation, conflict resolver, source overwrite, or second mutation family is introduced.

## Authority chain

The runtime separates:

`ValidationResult → RemediationProposal → Exact Mutation Intent → StaleStateVerifier → AuthorizationBoundary → AuthorizedMutationRequest → ParagraphAlignmentMutationExecutor`

The exact intent binds proposal/document state/rule/finding/evidence, paragraph target locator, `paragraph.alignment`, direct-formatting provenance, expected-before, desired-after, and deterministic intent identity. The executor does not re-derive the desired value from RuleCatalog or the current validator/rule state.

## TDD evidence

Run `35645806956` confirms both RED boundaries before production materialization:

1. Authority RED is genuine: intent tests fail before exact-intent production exists.
2. Authority GREEN: 7/7 PASS after exact-intent production materialization.
3. Executor RED is genuine: mutation tests fail because `Nd30.LegalValidator.Mutation` does not yet exist.
4. Executor production is materialized only after RED enforcement.
5. Final build then succeeds with 0 warnings and 0 errors.

## Correctives

### CS1736 test-generator corrective

The L4C test helper previously used a non-compile-time OpenXML value as a default parameter. The canonical test generation path was corrected to use a nullable sentinel and resolve `Left` inside the helper body. The missing-direct fixture remains semantically distinct through `missingDirect:true`.

### CS9135 production corrective

OpenXML SDK v3 represents `JustificationValues` as a readonly struct; `Left`, `Center`, `Right`, and `Both` therefore cannot be used as constant-pattern switch arms. The canonical L4C generator now maps direct justification using explicit equality comparisons:

- Left → LEFT
- Center → CENTER
- Right → RIGHT
- Both → JUSTIFY
- unsupported values → null / fail closed

The earlier speculative post-materialization OpenXML enum corrective is not part of the execution path.

## Mutation safety verified

Run `35645806956` verifies the targeted L4C suite 16/16 PASS, including:

- deterministic exact intent identity;
- target/before/after changes alter authority-bearing intent semantics;
- wrong authorization is rejected;
- stale document produces no output;
- missing target produces no output;
- missing direct formatting is rejected;
- expected-before mismatch is rejected;
- source/output same path is rejected;
- existing output is rejected;
- reapplication does not blind-write and is rejected as stale;
- successful mutation writes a new file;
- source identity remains unchanged;
- output identity changes;
- output reopens successfully;
- unrelated representative content is preserved.

Static gates also verify that State/Remediation/Authorization namespaces retain no write primitives, writable OpenXML is confined to the mutation executor, the executor requires `PatchPolicy.NORMAL`, direct provenance is enforced, and mutation code does not consult RuleCatalog/ProposedValue/finding.Expected to reconstruct the authorized desired value.

## Runtime evidence

Fresh run `35645806956` on implementation/workflow SHA `c6c1575d1cc8c57e29bd2e9633be92a9149efa09`:

- Build: PASS — 0 warnings, 0 errors
- L4C targeted: 16/16 PASS
- LegalValidator full regression: 80/80 PASS
- DocumentEngine: 31/31 PASS
- SemanticDetector: 13/13 PASS
- Full executed regression total: 124/124 PASS
- Failed: 0
- Skipped: 0
- V22 static audit: PASS — fixtures=22, errors=0
- V23 static audit: PASS — fixtures=11, errors=0
- V24 static audit: PASS — verified_rules=435, errors=0
- Static authority/write boundary: PASS
- Runtime source package SHA-256 verification: PASS
- Artifact upload: PASS
- Enforce gate: PASS

### Regression-floor accounting

The earlier L4C run materialized L4B with `--production-only`, so its final regression did not include the complete L4B test suite. The workflow was corrected to materialize L4B normally before L4C. The corrected fresh run executes 124 tests in the full solution: 80 LegalValidator + 31 DocumentEngine + 13 SemanticDetector.

The locked minimum requirement was 107 pre-L4C cases plus the L4C additions. The corrected runtime total is greater than that minimum and, critically, includes the L4B tests that were previously omitted. No assertion was weakened, no test was skipped, and no expected result was changed to obtain the higher count.

## Package/output evidence

The tested runtime source is packaged and SHA-256 verified in CI. The successful mutation test verifies source immutability, new-output identity change, reopen success, and representative unrelated-content preservation. L4C remains restricted to `PatchPolicy.NORMAL`; existing safety authorities continue to prevent unsafe package classes from being treated as ordinary writable documents.

## Deferred

Not claimed or implemented in L4C:

- durable exactly-once/idempotency journal;
- batch mutation and conflict resolution;
- style or inherited-format mutation;
- other formatting families;
- source overwrite;
- rollback service;
- authenticated identity/RBAC;
- cryptographic authorization token;
- unattended generic autofix.

## Integrity

- `main` was not merged or modified by L4C implementation work.
- Previous stages remain locked.
- No force push or history rewrite is claimed.
- FINAL PASS is intentionally withheld until this checkpoint SHA itself receives a fresh successful Actions run.
