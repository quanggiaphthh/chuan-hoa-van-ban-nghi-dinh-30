# V25 L4A DOCUMENT IDENTITY PASS

## Repository / baseline
- Repository: `quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30`
- Branch: `feature/gd4-v25-l4a-document-identity`
- Canonical baseline: `69c51fef21cfbcd9dae1395c454c1b755ac32f38`
- Verified implementation/gate SHA: `d38842d3b72ea3a3b68f0798543650953e9c1e5c`
- Implementation verification Actions run: `35571248069` — SUCCESS.
- This checkpoint is documentation-only; a fresh CI run on the checkpoint commit is required before FINAL PASS.

## Architecture
L4A remains foundation-only:
`Document -> DocumentIdentity -> Validator -> Finding -> RemediationPlanner -> RemediationProposal + DocumentStateBinding -> STOP`.
No authorization or mutation authority is introduced.

## RED evidence
Run `35571248069` materialized the new L4A tests before production state code. `Verify L4A RED is real` completed with the expected failing test command under `continue-on-error`, and `Enforce RED` passed by requiring that step outcome to be `failure`. Production L4A materialization occurred only after RED enforcement.

## DocumentIdentity contract
- Algorithm: exact canonical `SHA-256`.
- Digest: lowercase hexadecimal, exactly 64 characters.
- Scope: `whole-document-bytes`.
- Hash input: exact source file bytes read through `File.OpenRead`; no DOCX semantic normalization.
- Same bytes produce same identity; changed bytes produce a different identity.
- Identity is a stale-state primitive, not a signature, authentication credential, authorization token or write authority.

## Stale-state decision contract
Pure/read-only `StaleStateVerifier` decisions:
- `MATCH`
- `STALE_DOCUMENT`
- `MISSING_EXPECTED_IDENTITY`
- `UNSUPPORTED_IDENTITY`
- `RULE_IDENTITY_MISMATCH`

All mismatch/missing/unsupported paths fail closed. `MATCH` sets state compatibility only and explicitly leaves `Authorized=false`.

## Rule identity decision
Current canonical `ValidationResult` exposes `RuleId` and `RuleSource(SourceId, Locator)` but no rule-pack version/hash. L4A therefore binds the minimum existing identity as `RuleId + SourceId + SourceLocator`. A mismatch is detectable as `RULE_IDENTITY_MISMATCH`.

Limitation: semantic rule changes that retain the exact same RuleId/SourceId/Locator cannot be detected by this contract. Rule-pack/canonical-rule content hashing is deferred; L4A does not invent a versioning service.

## Proposal-binding decision
`RemediationProposal` gains an optional explicit `DocumentStateBinding` containing expected document and rule identities. The L3 deterministic ProposalId seed is intentionally unchanged. State binding is separate from immutable proposal semantic identity, preserving backward compatibility and avoiding needless ProposalId churn.

Existing L3 provenance remains preserved: RuleId, finding reference, evidence references and deterministic target remain unchanged.

## Targeted tests
L4A adds 13 xUnit test cases (11 test methods; one 3-case theory), covering:
1. exact bytes deterministic identity;
2. changed bytes change identity;
3. canonical SHA-256/lowercase/scope format;
4. explicit proposal state binding + provenance preservation;
5. document mismatch -> STALE_DOCUMENT;
6. missing expected identity fail-closed;
7-9. unsupported algorithm variants fail-closed;
10. malformed digest fail-closed;
11. MATCH != authorization;
12. rule identity mismatch;
13. state binding does not alter L3 ProposalId.

## Runtime verification
Actions run `35571248069`:
- bootstrap/materialization through L3: PASS;
- RED proof + enforcement: PASS;
- deterministic fixtures: PASS;
- V22/V23/V24 static audits: PASS;
- build: PASS;
- L4A targeted suite: PASS;
- full regression: PASS;
- static no-write gate: PASS;
- packaging: PASS;
- artifact upload: PASS;
- enforce gate: PASS.

Locked baseline remains 79 tests. L4A contributes 13 new cases; expected full count is 92 with failed 0 and skipped 0. CI step-level success is confirmed; final checkpoint CI is required to re-certify the checkpoint SHA.

## No-write verification
Static gate rejects production L4A state namespace occurrences of `SaveAs`, `.Save(`, `WriteAll`, `OpenWrite`, `FileMode.Create`, `File.Replace`, `File.Delete`, and writable `WordprocessingDocument.Open(...true)`. L4A production state code only reads source bytes and performs pure identity/state comparisons.

## Limitations / deferred gaps
Explicitly deferred to later stages:
- target-level fingerprint/canonicalization;
- authorization artifact / AuthorizedMutationRequest;
- DOCX mutation/executor/writer;
- durable idempotency;
- conflict management/resolution;
- atomic output/rollback/post-write verification;
- rule-pack deterministic semantic hash/version service.

## Integrity
- `main` was verified at canonical baseline before branch creation.
- L4A branch was created directly from `69c51fef21cfbcd9dae1395c454c1b755ac32f38`, not from discovery commit.
- No merge to main.
- No force push or history rewrite.
- V22/V23/V24/V25 L1-L3 remain locked.