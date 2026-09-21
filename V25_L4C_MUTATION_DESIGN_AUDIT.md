# V25 L4C — MINIMAL SURGICAL MUTATION DESIGN AUDIT

## 1. Baseline

- Repository: `quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30`
- Discovery branch: `feature/gd4-v25-l4c-mutation-discovery`
- Canonical baseline: `8406492df9970b74177b65186dcc3a3e04719dcd`
- Locked regression: 107 cases PASS, 0 failed, 0 skipped.
- This commit is documentation-only. Production files changed: 0. Tests changed: 0. Workflow files changed: 0.

## 2. Current authority chain

`Document → DocumentIdentity → Validator → Finding → RemediationPlanner → RemediationProposal → StaleStateVerifier → AuthorizationBoundary → AuthorizedMutationRequest → STOP`

There is no mutation authority at this baseline. `AuthorizedMutationRequest` is an authorization-domain request, not a writer capability.

## 3. Product requirement evidence

Mutation is justified as a product direction, but not yet justified as an implementation at the current request-contract boundary.

Positive evidence:

- `ARCHITECTURE.md` explicitly places `Reversible safe patches` after Validator and requires auto-fix only for safe, local, traceable changes.
- `FIX-SAFETY.md` explicitly classifies font/margins/italic/spacing as SAFE examples, requires surgical property-level patching, preconditions/postconditions, reversibility and structural/visual verification.
- L3 explicitly says an ELIGIBLE proposal still requires a separate authorized path; L4B supplies that authorization boundary while deliberately deferring the executor.

Therefore this project is intended to progress beyond proposals. However, the present `AuthorizedMutationRequest` omits mutation-critical values needed for a fail-closed executor: it carries `Operation` and `TargetId`, but not the proposal's `Expected` (observed/before state) or `ProposedValue` (desired/after state), and it has no typed property/target locator contract. Mutation must not infer these values by reloading a proposal or rule after authorization.

## 4. Candidate mutation matrix

| Candidate | Locality | Deterministic target | Preservation risk | Legal/semantic risk | OpenXML complexity | Reversible | Recommendation |
|---|---|---:|---|---|---|---:|---|
| Paragraph alignment | paragraph property | Medium: current `pN` is parser-position identity only | Low if direct property already exists; Medium if inherited | Low | Low | Yes | Best candidate, but only after target/precondition contract |
| Paragraph spacing | paragraph property | Medium | Low/Medium | Low | Medium because before/after/line-rule units matter | Yes | Defer behind alignment |
| Line spacing | paragraph property | Medium | Low/Medium | Low | Medium/High due `line` + `lineRule` semantics | Yes | Defer |
| Indentation | paragraph property | Medium | Medium; numbering/list interaction | Medium | Medium | Yes | Defer |
| Font family | run/style/effective property | Medium/Low | Medium/High because mixed runs and inheritance | Low | Medium | Yes | Defer |
| Font size | run/style/effective property | Medium/Low | Medium/High because mixed runs and inheritance | Low | Medium | Yes | Defer |
| Bold/italic | run/style/effective property | Medium/Low | Medium due inheritance and semantic emphasis | Medium | Low/Medium | Yes | Defer |
| Page margins | section property | Medium | Medium/High; section layout/reflow | Low | Medium | Yes | Defer |
| Section properties | section-level | Medium | High | Medium | High | Yes in principle | Reject as first family |

### Selected first mutation candidate

**Paragraph alignment (`w:pPr/w:jc`) is the only recommended first family**, but it is **not implementation-ready at the current baseline**. Selection is conditional on a target/precondition request-contract extension described below.

## 5. Formatting/style inheritance analysis

`DocxParser` captures direct paragraph formatting separately from paragraph style. `EffectiveFormattingResolver` resolves defaults → style inheritance → direct paragraph formatting and reports provenance such as `document-default`, `inherited-style:*`, `paragraph-style`, or `direct-formatting`.

For paragraph alignment, blindly creating direct `w:jc` when the observed effective value comes from a style/default would introduce unnecessary direct formatting and change the document's formatting semantics. Conversely, editing a shared style can affect multiple paragraphs and violates the first-operation locality requirement.

Therefore L4C-I must support **only the narrow subcase where the target paragraph already has an explicit direct alignment property and the validator's observed effective alignment is sourced from direct formatting**. If alignment is inherited from paragraph style/default, the executor must reject it as unsupported for L4C-I. It must not edit styles and must not create a direct override merely to force the requested effective value.

This restriction avoids cross-paragraph side effects and keeps the first mutation reversible at one existing XML property.

## 6. Target model

Current parser ids (`p1`, `p2`, …) are deterministic for a byte-identical input but are positional identities, not stable mutation locators across arbitrary edits/reopen cycles. Whole-document SHA-256 MATCH prevents pre-mutation drift, so `pN` can contribute to lookup, but it should not be the sole mutation target contract.

Minimum target contract proposed for L4C-I:

- `PartUri`: exactly `/word/document.xml` for the first family;
- `NodeKind`: `paragraph`;
- `ModelTargetId`: existing canonical `pN` provenance id;
- `StructuralPath`: body-child paragraph ordinal/path used to resolve the OpenXML node;
- `ContentAnchor`: deterministic hash of normalized target paragraph text plus a narrowly defined structural discriminator;
- `Property`: typed `paragraph.alignment`.

Resolution must require one and only one node satisfying the locator under the already-MATCHed document identity. Zero → `TARGET_NOT_FOUND`; more than one → `TARGET_AMBIGUOUS`. Raw index alone is insufficient.

The target contract is deliberately not a general XPath API and must not permit arbitrary caller-supplied XML paths.

## 7. Precondition model

Whole-document SHA-256 MATCH is necessary but target-level precondition remains required.

For the proposed first operation, the authorized request must bind:

- `ExpectedSource = direct-formatting`;
- `ExpectedDirectAlignment = <canonical enum/value>`;
- `DesiredAlignment = <canonical enum/value>`;
- exact target locator above.

Execution semantics: resolve target; verify it already contains explicit direct `w:jc`; canonicalize its current value; require equality with `ExpectedDirectAlignment`; only then replace the value with `DesiredAlignment`.

If the property is absent, inherited, already changed, malformed, or differs from expected: fail closed. No silent overwrite and no style edit.

Second application to an output document should normally return `PRECONDITION_FAILED` because expected old alignment no longer matches. `ALREADY_SATISFIED` may be added later only if its semantics are explicitly distinguished from replay/idempotency.

## 8. Request-input authority

A future executor must accept **only `AuthorizedMutationRequest`**. It must not accept `ValidationResult`, `RemediationProposal`, bare RuleId, or arbitrary patch payload.

Current L4B request is insufficient for execution because `Expected` and `ProposedValue` are dropped when converting proposal → authorized request. It also lacks typed `Property`, formatting provenance and a mutation-grade target locator.

Minimum extension before implementation:

`AuthorizedMutationRequest` must immutably bind a single typed mutation intent containing `{ OperationFamily, Property, TargetLocator, ExpectedTargetState, DesiredTargetState }`. Those fields must be included in ProposalId/AuthorizationId/RequestId canonical semantics as appropriate, so no post-authorization substitution is possible.

Do not recover missing values by looking up the rule or proposal after authorization; that would weaken exact authorization binding.

## 9. Package mutability

`PackageSafetyPreflight` currently classifies:

- unsupported/unreadable → `PROHIBITED`;
- signature/protection/macros/OLE/tracked changes → `AUDIT_ONLY`;
- external relationships only → `GUARDED`;
- otherwise → `NORMAL`.

L4C-I should be stricter than the generic preflight: **only `PatchPolicy.NORMAL` is mutable**. `GUARDED` is deferred. Signed, protected, macro-enabled, OLE, tracked-change, unsupported, unreadable, AUDIT_ONLY and PROHIBITED packages are rejected. Authorization cannot override this.

`PackagePreserver` currently proves only byte copy, SHA-256 and part-name inventory. It does not prove semantic preservation after an OpenXML writable round-trip. Therefore L4C-I must add preservation verification before claiming mutation safety; it must not rely on `PartInventory` alone.

## 10. Output-to-new-file design

Required future pipeline:

`input → recompute/verify DocumentIdentity → safety preflight → validate AuthorizedMutationRequest binding → safe output-path resolution → byte-copy input to unique temp output → open temp only for narrow writable mutation → close → reopen read-only → package/integrity checks → target postcondition → representative preservation checks → recompute output identity → promote temp to final new output → return typed result`

Never open the source writable. Never overwrite the source in L4C-I.

## 11. Source immutability

Hard invariant: source is byte-for-byte unchanged.

Capture source SHA-256 before any work and recompute after success or failure. Any mismatch is a hard failure. Source and output paths must resolve to different files.

## 12. Atomic temp-output strategy

Use a unique temp file in the final output directory (same filesystem). Copy source bytes to temp; mutate temp; close all handles; reopen and verify. Only after every verification passes may temp be promoted to the requested new output name using a non-overwriting atomic rename where supported.

If mutation/write/reopen/postcondition/integrity verification fails, delete only the owned temp artifact. Do not publish a partial final output and never modify/delete source.

## 13. Failure states

Minimum typed outcomes:

- `SUCCESS`
- `STALE_DOCUMENT`
- `AUTHORIZATION_REJECTED`
- `TARGET_NOT_FOUND`
- `TARGET_AMBIGUOUS`
- `PRECONDITION_FAILED`
- `UNSUPPORTED_OPERATION`
- `PACKAGE_NOT_MUTABLE`
- `OUTPUT_PATH_REJECTED`
- `WRITE_FAILED`
- `POSTCONDITION_FAILED`
- `OUTPUT_INTEGRITY_FAILED`
- `SOURCE_IMMUTABILITY_VIOLATION`

No catch-all success. Exceptions are translated to a failure outcome only after temp cleanup and source-integrity verification.

## 14. Post-write verification

Required distinction:

**Postcondition verification** (mandatory): output reopens; OPC required parts remain; package safety is re-evaluated; target resolves uniquely; direct alignment equals desired value; source hash is unchanged; output identity is computed; representative unrelated text/parts/relationships remain present.

**Full legal validation** (strongly recommended where existing validator/rule pack can deterministically re-run): revalidate the originating rule and require the original finding to clear/pass. This is additional assurance, not a substitute for exact property postcondition verification.

Preservation checks should compare at minimum part inventory, relationship inventory for untouched parts, content types, styles/numbering/header/footer/image/custom XML presence and representative unrelated content. The first implementation should fail rather than claim preservation for package classes not covered by these checks.

## 15. Reversibility / audit result

Mutation result/audit contract must record:

- RequestId / IdempotencyKey / ProposalId / AuthorizationId;
- RuleId and finding/evidence provenance;
- input DocumentIdentity;
- output DocumentIdentity;
- exact target locator;
- property/operation;
- before direct value + provenance;
- after direct value;
- package safety before/after;
- output path/result status.

This is sufficient to describe reversal/audit; L4C-I does not implement rollback.

## 16. Idempotency behavior

No durable idempotency store in L4C-I. RequestId remains replay foundation only.

If the same semantic operation is applied again to the produced output, the input document identity and expected old target state will no longer match. The executor must fail closed (`STALE_DOCUMENT` for original-state binding and/or `PRECONDITION_FAILED` for target state), not blindly rewrite. Durable timeout/retry reconciliation remains L4D.

## 17. Path safety

Minimum boundary:

- canonicalize source, temp and final paths;
- source and final must differ after canonicalization;
- final output must be under an explicitly supplied/approved output root, not arbitrary traversal;
- reject `..` escape after canonicalization;
- reject existing final output in L4C-I rather than overwrite;
- create unpredictable owned temp name;
- avoid following a final-path symlink/reparse-point where the runtime can detect it;
- open/create temp with exclusive creation semantics;
- promotion must not overwrite an existing file.

The mutation domain contract should not grant filesystem path authority; path policy belongs to the execution boundary.

## 18. Fixture strategy

Before implementation, fixtures/tests must cover real OpenXML shapes for the selected direct-alignment subcase:

1. NORMAL safe document with explicit direct `w:jc` on exactly one target paragraph and unrelated paragraphs/styles/header/image content;
2. already-correct target;
3. stale whole-document variant;
4. target missing;
5. target locator ambiguous (synthetic only if it models a plausible duplicate anchor/path conflict);
6. inherited-alignment document (must reject; no direct override creation);
7. signed/protected/AUDIT_ONLY package using existing safety harness/fixture strategy;
8. tracked-change/macro/OLE as existing policy fixtures permit;
9. unrelated content/package preservation fixture;
10. output collision/path traversal cases.

Do not manufacture an unrealistic DOCX merely to satisfy a test. Reuse existing generated DOCX fixture machinery where its structures correspond to actual OOXML.

## 19. Threat model

| Threat/failure | Existing defense | Missing defense | L4C-I requirement |
|---|---|---|---|
| Wrong document | L4A SHA-256 DocumentIdentity | executor recheck | recompute immediately before temp copy |
| Stale document | StaleStateVerifier | execution-time check | fail unless MATCH |
| Wrong proposal | L4B exact ProposalId binding | none if request is intact | accept only authorized request |
| Forged domain authorization object | domain shape validation only | no authentication/crypto authority | do not claim credential security; preserve L4B semantics |
| Wrong target | proposal target id | mutation-grade locator absent | typed composite target locator |
| Ambiguous target | L3 proposal rejects multiple evidence targets | runtime locator uniqueness | require exactly one resolved node |
| Unexpected inherited formatting | effective resolver/provenance exists | request does not bind provenance | direct-existing alignment only; bind expected provenance |
| Unsupported package | preflight PROHIBITED | executor gate | NORMAL only |
| Signed package | AUDIT_ONLY | executor gate | unconditional no mutation |
| Macro/OLE/tracked changes | AUDIT_ONLY | executor gate | unconditional no mutation |
| Output corruption | parser/preflight can reopen | post-write executor verification | reopen + OPC + postcondition checks |
| Partial write | none | atomic publication | temp then verified promotion |
| Source overwrite | no executor today | path boundary | source never writable; hash before/after |
| Malicious path | none | output root/canonicalization | traversal/collision/symlink defenses |
| Duplicate execution | RequestId/IdempotencyKey foundation | no durable journal | fail closed on stale/precondition; L4D durable handling |
| OpenXML normalization | no mutation today | preservation comparison | mutate temp only; compare untouched package evidence |
| Unrelated content loss | part inventory utility | inventory alone insufficient | relationships/content types/representative content preservation checks |

## 20. Exact implementation scope assessment

A safe implementation scope can be defined, but **must not start until the authorized request contract is extended and re-authorized around the exact mutation intent**.

Proposed future L4C-I after that prerequisite:

- exactly one family: existing direct paragraph alignment (`w:jc`) replacement;
- exactly one paragraph target;
- exactly one authorized request / one mutation;
- only `/word/document.xml` body paragraph;
- only when effective alignment provenance is direct formatting and explicit `w:jc` already exists;
- exact expected-old and desired-new alignment bound into authorization/request identity;
- `PatchPolicy.NORMAL` only;
- new-file output only via temp + verified promotion;
- original immutable/hash checked;
- no style edits, no creation of direct overrides, no batch, no overwrite, no durable journal.

## 21. Decision gate

**DECISION B — NOT READY — TARGET CONTRACT REQUIRED.**

Reason: product intent for reversible safe mutation is explicit and paragraph alignment is a plausible first surgical family, but the current `AuthorizedMutationRequest` drops `Expected` and `ProposedValue` and has no typed property, target locator, or formatting-provenance/precondition binding. Implementing a writer now would force the executor to infer or reacquire mutation intent after authorization, violating exact authorization/no-privilege-amplification principles.

Minimum next action is not MutationExecutor. First harden the proposal → authorization → request contract so authorization binds the exact single target/property/before/after mutation intent. Then re-run a discovery/contract gate before enabling writable OpenXML.

## 22. Explicit out of scope

No implementation in this audit. No `MutationExecutor`; no writable `WordprocessingDocument.Open`; no Save/SaveAs; no patch application; no source overwrite; no style mutation; no section/margin/font/spacing mutation; no batch/conflict resolver; no durable idempotency journal; no rollback engine; no authentication/RBAC; no cryptographic authorization token; no signed/protected/AUDIT_ONLY mutation; no UI/API.

## 23. Integrity

This discovery branch was created from canonical main `8406492df9970b74177b65186dcc3a3e04719dcd`. The audit changes documentation only. Main is not merged or modified by this work.