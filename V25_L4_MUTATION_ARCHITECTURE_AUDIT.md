# V25 L4 MUTATION ARCHITECTURE AUDIT

## 1. Executive conclusion

Decision: **NOT READY — FOUNDATION REQUIRED**.

The product architecture explicitly anticipates reversible safe patches, but current runtime stops at an immutable, technically-eligible RemediationProposal. There is no authorized mutation path, no mutation executor, no document/target stale-state contract, no authorization artifact, no durable idempotency record, no conflict classifier/resolver, and no atomic DOCX writer/post-write validation pipeline. Therefore UC4 (approved automatic DOCX mutation) is architecturally plausible but unsafe to implement as a single next step. UC5 (unapproved automatic mutation) is not justified.

## 2. Canonical baseline

Repository: quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30
Baseline: `69c51fef21cfbcd9dae1395c454c1b755ac32f38`.
Discovery branch: `feature/gd4-v25-l4-mutation-discovery`.
V25 L3 is merged and its locked regression floor is 79/79 PASS.

## 3. Current architecture map

Official/verified legal source -> Source Registry -> Atomic Canonical Rules -> applicability/temporal resolution -> Document Engine/Semantic Detector -> Legal Validator -> ValidationResult -> RemediationPlanner -> RemediationProposal -> **missing authorization/mutation authority**.

Authority boundaries observed:
- Validator: evaluation only.
- RemediationPlanner: proposal + technical eligibility only.
- PackageSafetyPreflight: package mutation-risk classification only.
- Mutation authority: absent.
- Authorization authority: absent.

L3 deliberately states that ELIGIBLE is technical eligibility only and mutation requires a separate authorized path.

## 4. Product/use-case evidence

Architecture documentation says auto-fix is allowed only for safe, local, traceable changes and shows Validator -> Reversible safe patches -> Structural + visual QA. FIX-SAFETY defines SAFE/GUARDED/SUGGEST_ONLY/PROHIBITED and a patch contract with before/after plus pre/postconditions. This is evidence that a mutation capability is an intended architectural direction, but not evidence that unattended mutation is required now.

| Use case | Current support | Missing capability | Recommended status |
|---|---|---|---|
| UC1 detect/report | Validator runtime exists | none material | REQUIRED / supported |
| UC2 human suggestion | SUGGEST_ONLY + remediation proposal semantics | presentation/UI outside runtime | REQUIRED / supported at domain layer |
| UC3 proposal for later approval | deterministic RemediationProposal exists | approval transport/UI | REQUIRED foundation / supported |
| UC4 user approves then system edits DOCX | architectural intent exists | identity, authorization, executor, atomic output, post-validation | OPTIONAL future capability; unsafe currently |
| UC5 unattended auto-write | no source evidence requiring it | all mutation foundations plus explicit product policy | NOT JUSTIFIED |

## 5. Document mutability audit

Current Document Engine is predominantly read-only.

Confirmed capabilities:
- Opens DOCX read-only through `WordprocessingDocument.Open(path,false)`.
- Parses body paragraphs, tables, sections, headers, footers, styles, numbering, fields and relationships into an intermediate DocumentModel.
- PackageSafetyPreflight detects required OPC parts, signature package structures, protection, macros, OLE, tracked changes, external relationships and selected unsupported structures.
- PackagePreserver can copy a file without mutation, compute whole-file SHA-256, and inventory ZIP parts.

Not found as production runtime capability:
- paragraph/run/style/section/table/header/footer/numbering mutation API;
- DOCX serialize/save mutation path;
- round-trip mutation verification;
- unknown-part-preserving writer contract;
- signature-preserving writer;
- macro/OLE mutation-preservation guarantee.

Important distinction: `PackagePreserver.Sha256()` is a utility that can hash a file, but no proposal or validation contract binds that hash to the document state. It is therefore not currently a stale-state guard.

Footnotes/endnotes/comments are recorded read-only when present. Unsupported structures are not evidence of safe mutability.

## 6. Stale-state analysis

No canonical document version, ETag, generation token, target fingerprint, expected-current-state fingerprint, or rule-version binding is present in RemediationProposal. Proposal ID hashes finding semantics, target, expected/observed values, evidence references and decision; it does not prove that the source DOCX remains unchanged after proposal creation.

Option A — whole-document SHA-256:
- Safety: strong wrong-document/stale-document rejection.
- False stale: high; any unrelated change invalidates all proposals.
- Complexity: low because PackagePreserver already exposes SHA-256.
- Determinism: strong on identical package bytes.
- DOCX compatibility: straightforward byte-level package identity.

Option B — target-level fingerprint:
- Safety: better concurrency for unrelated edits, but depends on stable canonical target serialization/identity that does not yet exist.
- False stale: lower.
- Complexity: materially higher.
- Determinism: only safe after canonical target fingerprint rules are defined.

Option C — whole-document + target fingerprint:
- Safety: strongest context binding and useful diagnostics.
- False stale: whole-document component still rejects unrelated changes unless policy permits controlled rebase.
- Complexity: highest.

Minimum viable foundation recommendation: begin with a canonical whole-document SHA-256 identity contract and expected current value/precondition. Do not introduce target fingerprint until stable canonical target serialization is specified and tested.

## 7. Authorization analysis

A proposal must never be its own write credential.

A. Explicit user approval token: good user-intent boundary; must bind proposal/document fingerprint and resist replay.
B. Caller/API boolean flag: weak auditability and privilege-confusion risk; not sufficient as canonical authority.
C. Signed authorization object: strongest independent verification but excessive before trust/key-management requirements exist.
D. Internal trusted execution context: useful deployment mechanism but insufficient proof of user intent by itself.

Recommended future architecture: an explicit immutable authorization artifact created by a trusted boundary, binding proposal ID + document identity + intended operation + authorization identity/context. Cryptographic signing is optional until a real trust-boundary requirement is established. A bare `approved=true` flag should not be canonical authority.

## 8. Mutation contract proposal

Keep two distinct contracts.

`RemediationProposal`: diagnostic/proposal artifact, no write authority.

Future `AuthorizedMutationRequest` minimum semantics:
- request/idempotency key;
- proposal ID;
- RuleId and finding/evidence provenance references;
- source document identity/fingerprint;
- deterministic target ID;
- expected current state;
- operation + replacement;
- package safety state/preconditions;
- explicit authorization reference/artifact;
- rule/rule-pack identity or version binding;
- output policy (new output by default, never implicit overwrite).

Timestamp is audit metadata, not a substitute for stale-state identity.

## 9. Idempotency analysis

Retry after an unknown network outcome is unsafe without execution identity. Minimum future contract: caller-supplied or deterministically generated idempotency key bound to immutable request semantics plus a durable execution record. Resulting-state detection can supplement but must not replace request identity. Reusing a key with different semantics must fail closed. Current runtime has no durable mutation execution journal.

## 10. Atomicity / rollback analysis

Future safe pipeline should be:

source -> verify fingerprint/preconditions -> working copy/new output -> apply surgical change -> structural validation -> serialize/close -> reopen -> package preflight -> verify intended change -> validator regression/postconditions -> publish output atomically.

The original must never be the working file. Failures during mutation, serialization, reopen or validation discard/quarantine the candidate output. Default product behavior should create a new output artifact; replacement of an original, if ever allowed, needs a separate explicit persistence policy. This avoids pretending DOCX ZIP mutation itself is transactional.

## 11. Conflict model

Minimum classification semantics:
- `NO_CONFLICT`: disjoint deterministic targets/properties.
- `DUPLICATE`: same target, operation, expected state and replacement.
- `CONFLICT`: same target/property with different replacement, overlapping text ranges, or known parent/child incompatible structural operations.
- `UNKNOWN`: interaction cannot be proven safe (including style/direct-formatting or section/paragraph interactions without a formal dependency model).

Fail closed for CONFLICT and UNKNOWN. No resolver should be introduced before deterministic target and operation semantics exist.

## 12. Signed/protected safety

Current preflight detects OPC `_xmlsignatures/` structure but explicitly does **not** cryptographically verify signature validity. It detects Word documentProtection. Signed/protected/macros/OLE/tracked-change packages are classified AUDIT_ONLY; selected unsupported structures are PROHIBITED. Future mutation must preserve these blocks: AUDIT_ONLY and PROHIBITED are no-write states. No implementation should claim cryptographic signature validation from the current detector.

## 13. Post-mutation verification

A future mutation executor must at minimum:
1. close/serialize candidate output successfully;
2. reopen DOCX;
3. rerun package safety/integrity checks;
4. confirm intended target/property reached the requested state;
5. verify unrelated package/content invariants appropriate to the operation;
6. rerun the originating rule and require resolution where semantically applicable;
7. rerun relevant validator regression and reject newly introduced high-severity findings;
8. preserve provenance/audit result including before/after hashes.

Byte-identical deterministic output should not be required blindly because ZIP serialization metadata/order may vary; deterministic semantic outcome and stable canonical verification are the relevant requirements.

## 14. Threat / failure model

| Failure / abuse | Impact | Existing defense | Gap | Required mitigation |
|---|---|---|---|---|
| stale proposal | wrong edit | none bound to proposal | no state identity | document fingerprint + expected state |
| replayed authorization | repeated write | none | no auth artifact/journal | bound auth + idempotency |
| duplicate request | duplicate mutation | none | no execution record | durable idempotency |
| conflicting patches | inconsistent output | L3 blocks ambiguous single proposal only | no batch conflict semantics | classifier, fail closed |
| ambiguous target | wrong target | L3 blocks multiple/no deterministic targets | none for future executor | retain invariant |
| unsupported structure | corruption | PROHIBITED preflight | writer preservation unproven | no-write until supported |
| signed/protected | signature/protection loss | AUDIT_ONLY | crypto validity not checked | no mutation; document limitation |
| partial write | corrupted file | no writer exists | no atomic output | working copy + atomic publish |
| corrupted output | unusable DOCX | parser/preflight can inspect | no post-write pipeline | reopen + validate |
| wrong document | unintended file | none | proposal not document-bound | document fingerprint |
| wrong target | unintended edit | deterministic target proposal | target state not rebound | expected current state |
| evidence no longer valid | invalid remediation | provenance retained | no freshness binding | state/rule binding + revalidation |
| rule changed | outdated patch | RuleId retained | no rule version/hash binding | bind rule/rule-pack identity |
| crafted DOCX | parser/resource abuse | preflight/read-only parser, selected unsupported detection | bounded-resource policy incomplete | limits + hardened parsing before mutation |
| package/path traversal | unsafe extraction | runtime uses ZipArchive/OpenXML without repository extraction writer | future writer risk | never trust entry paths for filesystem extraction |
| resource exhaustion | availability | no explicit mutation service | limits not defined | size/part/XML limits |

## 15. Option comparison

### Option A — Proposal-only
Benefit: safest current production posture; uses L3 fully. Complexity low. No new write surface. Suitable immediately. Limitation: user must perform edits externally.

### Option B — Authorized Mutation Minimal
Benefit: supports UC4 for narrowly SAFE deterministic changes. Requires document identity/fingerprint, explicit authorization artifact, deterministic mutation request, surgical writer, stale guard, atomic new-output pipeline and post-validation. Complexity medium-high and test burden substantial. Not safe to implement until foundation contracts are separately locked.

### Option C — Full Mutation Service
Benefit: scalable multi-request execution with durable idempotency, conflict management, journal/audit, recovery and richer authorization. Complexity and attack surface high; requires persistence/service architecture not currently present. Premature now.

## 16. Decision gate

**DECISION 3 — NOT READY — FOUNDATION REQUIRED.**

Reason: mutation is an architectural direction, but current source provides read-only parsing/safety, proposal eligibility and a file hash utility—not the contracts required to safely turn approval into a write. Building an executor now would collapse multiple unresolved authority and state-consistency concerns into one implementation.

## 17. Recommended next scope

Proceed only in small locked implementation rounds if product owner confirms UC4:

- L4A — Document Identity & Stale-State Contract: canonical whole-document SHA-256 binding, expected-state semantics, rule/rule-pack identity; no writer.
- L4B — Authorization & AuthorizedMutationRequest Contract: explicit immutable authorization boundary + idempotency key semantics; still no DOCX mutation.
- L4C — Minimal Surgical Mutation Executor for one proven SAFE property class, output-to-new-file only, preserving no-write safety states.
- L4D — Idempotency + Conflict Safety: durable execution identity and fail-closed conflict classification.
- L4E — Atomic Output + Post-Mutation Verification: reopen, preflight, intended-state verification, validator postconditions and preservation checks.

Do not begin L4C before L4A and L4B are independently verified.

## 18. Explicit out of scope

No code implementation in this discovery. No MutationEngine/API/writer/fingerprint implementation/conflict resolver/authorization service. No cryptographic signature parser. No comments/footnotes/shapes/tracked-changes mutation. No unattended auto-fix. No UI/API. No merge to main.

## 19. Regression requirements

All future mutation work must preserve the locked floor:
- V22: 31 PASS.
- V23: 13 PASS.
- Legal Validator/L2: 25 PASS.
- V25 L3 patch safety: 10 PASS.
- Total: 79/79 PASS, failed 0, skipped 0.

Each implementation round must add targeted RED->GREEN safety tests without deleting or weakening locked tests. Signed/protected/audit-only/prohibited no-write invariants and Finding != Write Authority remain mandatory.