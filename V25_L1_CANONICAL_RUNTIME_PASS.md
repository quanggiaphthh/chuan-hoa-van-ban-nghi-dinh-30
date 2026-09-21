# V25 L1 CANONICAL RUNTIME CHECKPOINT

Repository: quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30
Branch: feature/gd4-v25-l1-canonical-runtime
Baseline: eba7fcb536550164e42f339ddf7b25276ba7b359
Status: fresh runtime verification requested for this checkpoint commit.

Root cause: compact canonical V24 code payload materialized LegalValidatorTests.cs without `using Xunit;` and with two stale references to `fixtures/v23/01-named-decision.docx`; V23 canonical inventory uses `02-named-decision.docx`.

Canonicalization: corrected inside `v24-runtime/.v24-payload/code/*`; payload checksum in `materialize_v24_compact.py` updated. Workflow no longer invokes the post-materialization correction script. `v24-runtime/apply_v24_runtime_corrections.py` was removed. `v24-runtime/assert_v24_canonicalization.py` guards canonical payload and materialized output.

V22/V23 remain locked. V24 validator semantics are unchanged. Main is not modified by this branch.
