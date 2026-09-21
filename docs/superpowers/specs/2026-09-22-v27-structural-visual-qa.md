# GĐ4 V27 — Structural + Visual QA Specification

## Authority and baseline

Canonical main baseline: `f55d82be4657f9da6bee594325d2cccb26c995ff`.
V26 checkpoint: `1ba4f5a02022f46edd5d92057c4898ff7f1ac605`.
Locked regression floor: LegalValidator 99 + DocumentEngine 31 + SemanticDetector 13 = 143/143.

V22–V26 are locked. V27 MUST NOT change V26 mutation families, authorization, `MutationPlan`, `PlanOperation`, `AtomicMutationPlanExecutor`, canonical revalidation semantics, or Legal Validator rules to make QA pass. If V27 requires a V26 production-contract change, V27 is BLOCKED pending explicit architectural authorization.

## Objective

V27 adds a read-only post-revalidation QA layer:

`authorized V26 mutation → atomic output → canonical revalidation → structural QA → render/visual QA → typed QA evidence/verdict → publication eligibility`.

OpenXML/package validity is not visual correctness. Structural and visual QA inspect only; neither can authorize or perform remediation.

## Discovery matrix

| Capability | Source evidence at baseline | Runtime verified before V27 | Gap | V27 decision |
|---|---|---:|---|---|
| structural snapshot | DocxParser/package APIs exist | no | no immutable whole-package structural snapshot | implement read-only snapshotter |
| paragraph/table preservation | parser + V26 preservation tests | partial | no authorized-delta comparator | implement |
| sections | OpenXML SDK available | no V27 comparison | snapshot sectPr/order/settings |
| headers/footers | package relationships available | no V27 comparison | not snapshotted | snapshot refs + part hashes |
| relationships | PackageSafetyPreflight/package APIs | partial | no semantic inventory diff | implement deterministic inventory |
| images/media | OPC package parts | preservation only | no inventory/hash QA | snapshot URI/content-type/hash |
| numbering | OpenXML SDK | no | no reference inventory | snapshot paragraph num refs + numbering part identity |
| styles | OpenXML SDK | no | no reference inventory | snapshot style refs + styles part identity |
| page size/orientation | sectPr | no | no QA diff | snapshot |
| margins | sectPr | no | no QA diff | snapshot |
| page/section breaks | paragraph/run + sectPr | no | no QA diff | snapshot explicit breaks and section boundaries |
| DOCX rendering | no renderer subsystem found | no | renderer unknown | probe CI before adapter selection |
| PDF conversion | none found | no | renderer unknown | use LibreOffice headless only if real CI probe succeeds |
| page count | none | no | requires render | derive from rendered PDF metadata/tooling in CI |
| visual page generation | none | no | requires render tool | use existing Ubuntu package tooling only after probe |
| visual comparison | none | no | no justified threshold | minimum V27 uses render/page/layout evidence; no pixel authority |
| CI rendering support | V26 runs ubuntu-latest; no renderer setup | no | unknown | explicit `command -v soffice`, version probe; controlled apt fallback only if justified and logged |

Source evidence and runtime evidence are distinct. A source capability is never recorded as runtime PASS without execution evidence.

## Structural QA contract

Create a V27 QA namespace/project area under `Nd30.LegalValidator.Qa` (or equivalent existing assembly namespace) that has no DOCX write authority.

`StructuralSnapshot` is immutable and deterministically identified from canonicalized semantic fields. It captures, where present:

- ordered body block inventory (paragraph/table and stable ordinal);
- paragraph text/content anchors and paragraph property facts needed to distinguish allowed alignment/spacing mutations;
- table count/order and stable table content identity;
- section count/order and section properties;
- header/footer references and referenced part hashes;
- relationship inventory: source part, relationship id/type/target/mode;
- media/image inventory: URI, content type, SHA-256;
- paragraph numbering references and numbering part identity;
- paragraph style references and styles part identity;
- page width/height/orientation and margins per section;
- explicit page breaks and section boundaries;
- representative deterministic content anchors.

No byte-identical XML requirement is imposed where semantic canonicalization is sufficient.

`StructuralQaService.Compare(source, output, mutationPlan)` derives every permitted difference directly from the exact authority-closed `MutationPlan.Operations`. There is no caller-supplied ignore list, generic whitelist, `ignoreDifferences`, or callback authority.

For V26's locked SAFE families only:

- `paragraph.alignment` permits only the exact targeted paragraph alignment property to transition from `ExpectedBefore` to `DesiredAfter`;
- `paragraph.spacing.after` permits only the exact targeted paragraph spacing-after property to transition from `ExpectedBefore` to `DesiredAfter`.

Every other observed structural difference is unauthorized and fails closed. Missing/ambiguous target resolution also fails closed.

Required structural tests include: allowed alignment PASS; allowed spacing PASS; unrelated text FAIL; removed paragraph FAIL; changed/removed table FAIL; unexpected section property FAIL; relationship/media loss FAIL; header/footer change FAIL; numbering/style reference change FAIL; deterministic snapshot PASS; unexpected page break FAIL.

## Visual QA contract

Renderer selection is runtime-evidence driven. The V27 GitHub Actions job MUST probe `soffice`/LibreOffice before production adapter selection and log exact version. No source-only assumption is permitted.

If a viable LibreOffice runtime exists, implement a read-only `IDocumentRenderer` adapter that renders from a disposable copy/working directory, never from a writable canonical source path. Record:

- renderer name and exact version;
- source/output `DocumentIdentity` values;
- mutation plan id;
- input DOCX SHA-256;
- rendered artifact SHA-256;
- render status;
- page count;
- page dimensions/layout metadata available from deterministic rendered artifacts.

Visual verdict is typed: `PASS`, `FAIL`, `NOT_EVALUATED`, `RENDER_FAILED`. Missing renderer is never PASS. Renderer failure is `RENDER_FAILED`. Publication eligibility fails closed whenever visual QA is required and visual verdict is not PASS.

V27 does not use OCR or AI vision as CI authority and does not use pixel-perfect equality. No arbitrary similarity threshold is introduced. Minimum visual success is a real runtime chain `DOCX → renderer → rendered artifact → page/layout evidence → QA result`. Page-count changes are mutation-aware diagnostics, not a universal corruption rule.

## QA coordinator and evidence

`DocumentQaCoordinator` composes after V26 canonical revalidation. It does not modify `AtomicMutationPlanExecutor`; the integration scenario explicitly calls V26 execution/revalidation and then V27 QA.

`DocumentQaResult` binds:

- source and output `DocumentIdentity`;
- `MutationPlanId`;
- source/output structural snapshot identities;
- structural diff/evidence and verdict;
- renderer/version;
- rendered artifact identities;
- visual/page/layout evidence;
- final QA verdict and publication eligibility;
- diagnostics.

Structural failure rejects publication and visual execution may be skipped with an explicit non-PASS typed state. QA cannot generate a mutation plan, authorize remediation, or write DOCX.

## Real DOCX and corruption evidence

At least one deterministic real DOCX fixture must include paragraphs, a table, header/footer, numbering/style references, image/media, multiple sections, explicit page/section break behavior, and a V26 alignment or spacing target. If canonical fixtures lack any feature, V27 creates a minimal deterministic test fixture with a documented purpose.

At least one E2E test executes:

`real source DOCX → V26 authorized mutation → output DOCX → canonical revalidation → structural QA → actual renderer → visual evidence → final QA verdict`.

Controlled test-only corruption helpers create unrelated text mutation, deleted paragraph, table loss, removed relationship/media, header/footer mutation, section/layout mutation, numbering/style reference mutation, and unexpected page break. These helpers are never production authority.

## CI gate

Single workflow: `.github/workflows/v27-structural-visual-qa-gate.yml` on branch `feature/gd4-v27-structural-visual-qa`.

The workflow must:

1. verify canonical baseline ancestry and main baseline expectation;
2. materialize locked V22–V26 exactly as canonical V26 does, including preserved V26 tests;
3. prove locked baseline inventory 99/31/13 = 143 before V27 tests;
4. run an explicit inventory gate;
5. prove genuine structural RED before production structural QA materialization;
6. run structural GREEN;
7. probe real renderer and log version;
8. prove visual RED before renderer/visual production materialization;
9. run visual GREEN;
10. Release build;
11. V27 targeted tests;
12. real DOCX E2E;
13. corruption suite;
14. full regression with failed=0 and skipped=0;
15. static no-write/no-authority safety;
16. source SHA immutability including render path;
17. emit render evidence;
18. package tested source;
19. upload QA reports/rendered artifacts/test results;
20. enforce every required gate.

Static gates reject DOCX write APIs in QA namespaces, QA-to-mutation/remediation dependencies, LegalValidator visual mutation calls, caller-supplied ignore policies, and bypass of V26 authorization.

## Renderer failure policy

If the GitHub Actions runtime cannot demonstrate a viable production renderer, V27 stops with `VISUAL QA BLOCKED — RENDERER FOUNDATION REQUIRED`; no PASS checkpoint is created. A renderer installation may be added only when the workflow demonstrates an auditable Ubuntu package source/version and the dependency is architecturally justified; opaque binaries are prohibited.

## Final acceptance

`GĐ4 — V27 FINAL PASS` requires fresh CI on the final checkpoint SHA proving: locked 143 preserved; all V27 tests pass with zero failed/skipped; build zero warnings/errors; structural QA and controlled corruption detection pass; real DOCX E2E passes; actual renderer executes; rendered artifact and page/layout evidence exist; QA has no mutation authority; source is immutable; full regression, packaging, artifact upload, and enforce pass; canonical main remains `f55d82be4657f9da6bee594325d2cccb26c995ff`.

Only after a successful implementation-SHA CI may `V27_STRUCTURAL_VISUAL_QA_PASS.md` be created. A second fresh CI must run on that checkpoint SHA before final PASS is declared.