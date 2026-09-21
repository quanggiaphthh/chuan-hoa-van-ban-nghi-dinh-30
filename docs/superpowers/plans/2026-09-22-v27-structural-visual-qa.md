# V27 Structural + Visual QA Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add fail-closed, mutation-plan-aware structural QA and real renderer-backed visual/page-layout QA after V26 canonical revalidation without changing V26 mutation authority.

**Architecture:** V27 adds read-only snapshot/comparison and rendering/evidence services composed after the locked V26 executor/revalidator. Structural allowed deltas are derived only from V26 `MutationPlan.Operations`; visual evidence is produced only by a renderer proven in the GitHub Actions runtime.

**Tech Stack:** .NET 10, C#, Open XML SDK, xUnit, Python 3.12 materializers/static audits, GitHub Actions Ubuntu runner, LibreOffice/soffice only after runtime probe.

**Spec:** `docs/superpowers/specs/2026-09-22-v27-structural-visual-qa.md`

## Global Constraints

- Canonical main baseline: `f55d82be4657f9da6bee594325d2cccb26c995ff`.
- V26 checkpoint: `1ba4f5a02022f46edd5d92057c4898ff7f1ac605`.
- Locked regression floor: 143/143 = LegalValidator 99 + DocumentEngine 31 + SemanticDetector 13.
- Single branch: `feature/gd4-v27-structural-visual-qa`; no main merge, force push, history rewrite, V28, or V27 sub-stage naming.
- V22–V26 production contracts are locked; do not modify V26 mutation families, authorization, MutationPlan, PlanOperation, executor, canonical revalidation semantics, or Legal Validator rules.
- Structural QA has no caller-controlled ignore/whitelist.
- Visual PASS requires actual runtime renderer evidence; missing renderer cannot yield FINAL PASS.
- QA namespaces have no DOCX write/remediation authority.

## Review Focus

- Ambiguous paragraph target path: structural QA must fail closed rather than authorize a nearby paragraph.
- Relationship ID reorder with same semantic target: canonical inventory must remain deterministic without hiding target loss.
- Renderer writes lock/temp files: it must operate on a disposable copy while source SHA remains unchanged.
- Legal spacing mutation causing legitimate local reflow: visual policy must not equate any page/pixel change with corruption.
- Corrupt/missing rendered artifact after a zero exit code: renderer result must be `RENDER_FAILED`, not PASS.

---

### Task 1: V27 structural RED materializer

**Files:**
- Create: `v27-runtime/materialize_v27_structural.py`
- Generated test: `v22-runtime/tests/LegalValidator.Tests/V27StructuralQaTests.cs`

**Interfaces:**
- Consumes: locked V26 `MutationPlan`, `PlanOperation`, OpenXML SDK.
- Produces: failing tests naming `StructuralSnapshotter`, `StructuralQaService`, `StructuralQaVerdict` contracts.

- [ ] Write `materialize_v27_structural.py --tests-only` to emit tests for allowed alignment, allowed spacing, unrelated text, removed paragraph, table loss/change, section settings, relationship/media, header/footer, numbering/style references, page break, deterministic snapshot, and ambiguous target fail-closed.
- [ ] Materialize locked V22–V26 production/tests exactly as the V26 canonical workflow and run the 143 inventory before adding V27 tests.
- [ ] Run `dotnet test tests/LegalValidator.Tests/LegalValidator.Tests.csproj -c Release --filter FullyQualifiedName~V27StructuralQaTests` and record genuine RED caused by missing V27 QA types, not environment/configuration failure.
- [ ] Commit the RED materializer/evidence scaffolding.

### Task 2: Structural snapshot and authorized-delta comparator GREEN

**Files:**
- Generated production: `v22-runtime/src/LegalValidator/Qa/StructuralSnapshot.cs`
- Generated production: `v22-runtime/src/LegalValidator/Qa/StructuralSnapshotter.cs`
- Generated production: `v22-runtime/src/LegalValidator/Qa/StructuralQaService.cs`
- Modify materializer: `v27-runtime/materialize_v27_structural.py`

**Interfaces:**
- Produces: `StructuralSnapshotter.Capture(string docxPath)`, immutable deterministic snapshot identity, `StructuralQaService.Compare(sourcePath, outputPath, MutationPlan)` returning typed evidence/verdict.
- Consumes: exact V26 `MutationPlan.Operations`; no additional policy parameter.

- [ ] Implement production materialization only after Task 1 RED is recorded.
- [ ] Snapshot ordered paragraphs/tables, content anchors, paragraph alignment/spacing-after, section/page/margin settings, explicit page breaks, header/footer refs/hashes, relationships, media hashes, numbering/style refs and relevant part identities.
- [ ] Canonicalize relationship/media inventories deterministically while retaining relationship ID/type/target/mode evidence.
- [ ] Implement exact allowed-delta derivation for only `paragraph.alignment` and `paragraph.spacing.after`; require target and expected-before match; reject ambiguity.
- [ ] Compare every non-authorized field fail-closed and emit diagnostics naming the changed invariant.
- [ ] Run V27 structural tests GREEN, then run the complete solution tests and record every failure if any.
- [ ] Commit structural GREEN.

### Task 3: Renderer runtime probe and visual RED

**Files:**
- Create: `v27-runtime/probe_renderer.sh`
- Create: `v27-runtime/materialize_v27_visual.py`
- Generated test: `v22-runtime/tests/LegalValidator.Tests/V27VisualQaTests.cs`

**Interfaces:**
- Produces: runtime probe output `renderer.env` containing renderer path/name/version only when actual executable succeeds.
- Visual RED defines `IDocumentRenderer`, `RenderResult`, `VisualQaVerdict`, `VisualQaService` behavior.

- [ ] Implement probe that runs `command -v soffice || command -v libreoffice`, executes `--version`, validates non-empty version, and exits non-zero if unavailable.
- [ ] Add workflow probe before choosing the production adapter; if base runner lacks renderer, use only an auditable Ubuntu apt package installation path, log package candidate/installed version, then probe again. Do not download opaque binaries.
- [ ] Emit visual tests for real successful render evidence, missing renderer `NOT_EVALUATED`, process/conversion failure `RENDER_FAILED`, missing/corrupt artifact `RENDER_FAILED`, renderer/version/hash/page evidence, and no pixel-perfect authority.
- [ ] Run targeted visual tests before production visual materialization and record genuine RED.
- [ ] Commit renderer probe + visual RED.

### Task 4: LibreOffice adapter and visual GREEN

**Files:**
- Generated production: `v22-runtime/src/LegalValidator/Qa/Rendering/IDocumentRenderer.cs`
- Generated production: `v22-runtime/src/LegalValidator/Qa/Rendering/LibreOfficeDocumentRenderer.cs`
- Generated production: `v22-runtime/src/LegalValidator/Qa/VisualQaService.cs`
- Modify: `v27-runtime/materialize_v27_visual.py`

**Interfaces:**
- `IDocumentRenderer.Render(string canonicalDocxPath, string evidenceDirectory)` returns typed `RenderResult`.
- Adapter receives the probed executable/version from controlled configuration; it copies input into a disposable writable directory before rendering.

- [ ] Implement typed `PASS/FAIL/NOT_EVALUATED/RENDER_FAILED` contracts and fail-closed publication semantics.
- [ ] Implement process invocation with bounded timeout, captured exit/stdout/stderr, disposable working copy, expected PDF existence/non-zero length validation and SHA-256 identities.
- [ ] Extract page count/dimensions from rendered PDF using an auditable runner package/tool available in CI; log its version. If extraction tooling cannot be established, treat the visual foundation as blocked rather than invent metadata.
- [ ] Implement mutation-aware page/layout evidence without universal page-count equality and without pixel threshold.
- [ ] Run visual tests GREEN on the real renderer runtime and rerun complete solution tests.
- [ ] Commit visual GREEN.

### Task 5: Real DOCX E2E and corruption suite

**Files:**
- Create: `v27-runtime/generate_v27_fixture.py`
- Create/generated: `v22-runtime/tests/fixtures/v27-structural-visual.docx` during CI only, not committed binary unless repository convention requires it.
- Generated tests: `v22-runtime/tests/LegalValidator.Tests/V27EndToEndQaTests.cs`
- Test-only helper: `v22-runtime/tests/LegalValidator.Tests/V27CorruptionFixture.cs`

**Interfaces:**
- Consumes: locked V26 executor/revalidator, V27 structural and visual services.
- Produces: real E2E QA result and controlled corruption detection evidence.

- [ ] Generate deterministic fixture containing paragraphs, alignment/spacing target, table, header/footer, numbering/style references, image/media, at least two sections, and explicit page break.
- [ ] Add E2E test that creates authority-closed V26 plan through the locked authority path, executes V26 atomic output + canonical revalidation, then invokes V27 QA and requires structural PASS plus actual renderer visual PASS.
- [ ] Add source SHA before/after assertions covering mutation and renderer execution.
- [ ] Add test-only controlled corruptions: unrelated text, paragraph deletion, table loss, relationship/media loss, header/footer change, section/layout change, numbering/style change, unexpected page break.
- [ ] Require each corruption to produce structural FAIL with a specific diagnostic class.
- [ ] Run E2E + corruption tests, then full solution tests.
- [ ] Commit E2E/corruption suite.

### Task 6: QA coordinator, publication eligibility and static safety

**Files:**
- Generated production: `v22-runtime/src/LegalValidator/Qa/DocumentQaCoordinator.cs`
- Generated production: `v22-runtime/src/LegalValidator/Qa/DocumentQaResult.cs`
- Create: `v27-runtime/audit_v27_static.py`
- Generated tests: `v22-runtime/tests/LegalValidator.Tests/V27QaCoordinatorTests.cs`

**Interfaces:**
- `DocumentQaCoordinator.Evaluate(sourcePath, outputPath, MutationPlan, rendererContext)` runs structural first, then visual only under explicit required visual policy, and returns evidence + publication eligibility.

- [ ] Write coordinator tests first: structural FAIL blocks publication; renderer unavailable/failure blocks publication; both PASS allow publication; evidence binds source/output identities and PlanId; QA cannot provide remediation.
- [ ] Run coordinator tests RED before implementation.
- [ ] Implement coordinator/result with no mutation calls and run GREEN.
- [ ] Implement static audit rejecting DOCX write APIs in `Qa`, QA dependencies on remediation/authorization creation, generic ignore/whitelist APIs, renderer writes against canonical input, and V26 production-file modifications relative to baseline.
- [ ] Run static audit and full solution tests.
- [ ] Commit coordinator/static safety.

### Task 7: Single V27 GitHub Actions gate

**Files:**
- Create: `.github/workflows/v27-structural-visual-qa-gate.yml`

**Interfaces:**
- Produces: one authoritative V27 CI gate and artifacts.

- [ ] Implement exact 20-step logical flow from the spec in one workflow, with explicit step IDs and final `if: always()` enforce.
- [ ] Materialize V22–V26 and preserved V26 tests before proving 99/31/13 inventory; do not delete locked tests.
- [ ] Gate structural RED outcome=failure then GREEN=success; probe/install/probe renderer; gate visual RED outcome=failure then GREEN=success.
- [ ] Build Release and assert warnings/errors zero from captured build output.
- [ ] Run V27 targeted, E2E, corruption, full regression, static safety, source immutability, render evidence checks.
- [ ] Package tested source and SHA-256; upload TRX, structural report, renderer/version report, rendered PDF/page metadata and package artifacts.
- [ ] Enforce all required outcomes and renderer evidence; missing renderer fails the workflow.
- [ ] Commit workflow.

### Task 8: Fresh implementation-SHA CI and proven corrections

**Files:**
- Modify only files implicated by proven CI failures.

**Interfaces:**
- Produces: successful implementation SHA CI or exact BLOCKED report.

- [ ] Push the single V27 branch.
- [ ] Trigger/observe fresh V27 workflow on exact implementation SHA.
- [ ] Read the full job log, not only summary.
- [ ] If failure occurs, classify exact failed step/diagnostic/root cause and apply only minimum corrective using RED→GREEN where production behavior changes.
- [ ] Repeat fresh CI until SUCCESS or a real renderer/architectural blocker is established.
- [ ] Verify canonical main still equals `f55d82be4657f9da6bee594325d2cccb26c995ff`.

### Task 9: Final checkpoint and checkpoint-SHA CI

**Files:**
- Create only after implementation-SHA SUCCESS: `V27_STRUCTURAL_VISUAL_QA_PASS.md`

**Interfaces:**
- Produces: one V27 checkpoint and final PASS evidence.

- [ ] Record baseline, implementation SHA, structural architecture/snapshot/allowed-delta model, renderer/version, visual method/limitations, real DOCX scenarios, corruption tests, exact counts, regression/static/artifacts/package/enforce evidence and implementation run ID.
- [ ] Commit checkpoint without altering production/test/workflow behavior.
- [ ] Push branch and obtain fresh CI on the exact checkpoint SHA.
- [ ] Read full checkpoint-SHA log and verify 143 locked baseline, all V27 tests, zero failed/skipped, build zero warnings/errors, structural corruption detection, real renderer/rendered artifact/page-layout evidence, no-write authority separation, source immutability, full regression, packaging and enforce.
- [ ] Verify main remains unchanged.
- [ ] Declare `GĐ4 — V27 FINAL PASS` only if every criterion is proven; otherwise report `GĐ4 — V27 BLOCKED` and do not fabricate PASS evidence.
