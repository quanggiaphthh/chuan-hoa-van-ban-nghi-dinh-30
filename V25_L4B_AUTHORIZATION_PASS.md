# V25 L4B AUTHORIZATION PASS

- Repository: quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30
- Branch: feature/gd4-v25-l4b-authorization
- Baseline: b7529e7c82d8bed2703bc97621f07e3f8ce0afc9
- Implementation gate SHA: 0340c98b4e2425102a4cf6faa2ce535ea404dbb6
- Implementation Actions run: 35572437614 — SUCCESS
- Scope: authorization boundary + AuthorizedMutationRequest contract only; no DOCX mutation.

## RED evidence

The workflow materializes all locked stages through L4A, then materializes only `V25L4BAuthorizationTests`. The targeted test command is expected to fail before the L4B production namespace exists. `Verify L4B RED is real` completed with the expected failure outcome and `Enforce RED` passed before production materialization.

## Authorization authority

`AuthorizationBoundary` is a pure domain boundary. An eligible proposal is not authorization. A valid `AuthorizationArtifact` is required and is narrowly scoped to one proposal and one expected document state. It is a domain expression of explicit intent, not an authenticated/cryptographic token.

## AuthorizedMutationRequest contract

A successful exact-binding decision produces an immutable `AuthorizedMutationRequest` carrying:

- RequestId;
- IdempotencyKey;
- ProposalId;
- DocumentStateBinding;
- AuthorizationArtifact;
- RuleId;
- FindingReference;
- EvidenceReferences;
- Operation;
- TargetId.

The request is a foundation contract only and has no writer/executor/save authority.

## Proposal/document binding and stale behavior

Authorization is rejected when the artifact proposal id differs from the proposal, when its expected state differs from the proposal binding, or when L4A `StaleStateVerifier` returns anything other than MATCH for the actual document/rule state.

## Safety override prohibition

Authorization cannot override signed/protected/AUDIT_ONLY/PROHIBITED/unreadable/unsupported package safety. Non-eligible, NOT_EVALUATED, NOT_APPLICABLE, blocked and suggestion-only proposals do not produce a valid request.

## Request identity / replay foundation

`AuthorizationId` and `RequestId` are deterministic lowercase 24-hex SHA-256-derived domain identities over canonical bound inputs. `RequestId` is also exposed as the L4B `IdempotencyKey` foundation for a future durable replay/idempotency layer. ProposalId, AuthorizationId and RequestId remain distinct semantic identities. No durable journal/store exists in L4B.

## Actor semantics

`ActorReference` is optional and opaque. It is not claimed to be authenticated and L4B adds no authentication, user database or RBAC subsystem.

## Tests / runtime

- Locked previous regression floor: 92 cases.
- L4B targeted source covers 14 test methods / 15 executed cases (one two-row status theory and one two-row protected/signed theory).
- Expected full regression total: 107 cases.
- Implementation run: build PASS; L4B targeted PASS; full regression PASS; static no-write/no-auto-approval PASS; packaging/integrity PASS; enforce PASS.
- Failed: 0.
- Skipped: 0.

## No-write verification

Static gate covers both `src/LegalValidator/State` and `src/LegalValidator/Authorization` and rejects Save/SaveAs, WriteAll/OpenWrite, FileMode.Create, File.Replace/Delete and writable WordprocessingDocument open patterns. It also rejects obvious automatic/broad approval patterns. No mutation executor or DOCX writer is introduced.

## Deferred gaps

Deferred beyond L4B: MutationExecutor, target-level fingerprint, durable idempotency store/journal, conflicts, atomic output, rollback, post-write validation, authentication/RBAC, cryptographic approval token, batch approval and UI/API.

## Integrity

Main was verified at baseline before branch creation and was not modified or merged by this work. No force push or history rewrite was performed. Previous stages remain locked.

A fresh GitHub Actions run on the checkpoint commit containing this document is required before FINAL PASS.