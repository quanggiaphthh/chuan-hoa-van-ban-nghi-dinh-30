# GĐ4 — V27 PAUSED CHECKPOINT

Status: **PAUSED BY USER REQUEST — NOT V27 FINAL PASS**

Repository: `quanggiaphthh/chuan-hoa-van-ban-nghi-dinh-30`

Branch: `feature/gd4-v27-structural-visual-qa`

Canonical V27 baseline / main at start: `f55d82be4657f9da6bee594325d2cccb26c995ff`

Last implementation/foundation SHA before this checkpoint: `7ef170d02d955b4476482090b8c29a0a31be8181`

Fresh GitHub Actions evidence: run `35659835713` — **SUCCESS** on SHA `7ef170d02d955b4476482090b8c29a0a31be8181`.

## Verified state

- V26 canonical baseline remains locked.
- Locked V26 regression floor: LegalValidator 99 + DocumentEngine 31 + SemanticDetector 13 = **143/143 PASS**.
- V27 Structural QA RED was demonstrated genuinely before production materialization.
- V27 Structural QA GREEN: **7/7 PASS**.
- Structural regression after V27: LegalValidator 106 + DocumentEngine 31 + SemanticDetector 13 = **150/150 PASS**.
- Release build: PASS, 0 warnings, 0 errors in the verified structural run.
- Structural authority/static gate: PASS.
- Structural QA is read-only and allowed deltas are derived from the authority-closed `MutationPlan`; V26 production mutation/authorization contracts remain locked.
- Preinstalled renderer probe previously established that the GitHub Ubuntu runner did not provide `soffice`/`libreoffice` by default.
- Renderer foundation corrective was added to provision and verify LibreOffice reproducibly in CI.
- Fresh run `35659835713` completed **SUCCESS**, including the renderer-foundation workflow introduced by SHA `7ef170d...`.

## Scope intentionally not completed

V27 is **not FINAL PASS**. Development is intentionally paused before implementing/closing the remaining Visual QA product scope:

1. Visual RED / genuine failure proof.
2. Production visual evidence adapter/service.
3. Real-DOCX render comparison/evidence contract beyond renderer-foundation smoke verification.
4. Mutation-aware page/layout visual policy.
5. Real-DOCX end-to-end V26 mutation → canonical revalidation → Structural QA → Visual QA.
6. Final corruption/negative-path visual suite.
7. Final full regression/static/package/enforce gates for completed V27.
8. `V27_STRUCTURAL_VISUAL_QA_PASS.md` and fresh checkpoint-SHA FINAL PASS run.

No claim is made that pixel-perfect equality is legal authority. Visual QA remains an evidence/QA layer and must not become mutation, authorization, or legal-validation authority.

## Resume point

When development resumes, continue on this same V27 branch from this paused checkpoint. Do not redo V26 or Structural QA discovery. First revalidate the locked baseline/checkpoint and renderer foundation, then continue with **Visual RED → Visual GREEN → real-DOCX E2E → final regression/static/package/enforce → final V27 checkpoint and fresh checkpoint-SHA CI**.

## Product direction after V27

After V27 is eventually completed, the next major direction should return to the project's original legal/formatting knowledge objective: audit the canonical rule coverage against Nghị định 30/2020/NĐ-CP, its appendices, Hướng dẫn 05 and other accepted authoritative formatting guidance, mapping source requirement → canonical rule → detector → validator → autofix eligibility → QA evidence.

## Integrity

- No merge to `main` is performed by this paused checkpoint.
- No force-push or history rewrite is intended.
- This file records a resumable engineering checkpoint only; it is deliberately **not** a PASS checkpoint.
