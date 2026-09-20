---

description: "Implementation tasks for IDEAEngineering Manifest Import"
---

# Tasks: IDEAEngineering Manifest Import

**Input**: Design documents from `specs/003-ideaengineering-manifest-import/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

**Ledger status**: This is the historical Feature 003 implementation ledger. Every
task marked `[X]` records work already completed and verified in the documented
workflow; the integrated baseline is tracked on `main`.

**Execution rule**: Work sequentially in this single agent. Every production behavior
is introduced through a public-seam test (red, green, refactor). Do not modify the
IDEAEngineering checkout.

## Phase 1: Foundational boundaries

**Purpose**: Establish the public import seam, safe source readers, diagnostics, and
the minimal public-safe fixture boundary before story work.

- [X] T001 [P] Add manifest import enums, request/result, snapshot metadata, diagnostics, and source execution/proposal domain types in `src/ProjectManagementCompiler/Domain/ManifestImportEntities.cs`, `src/ProjectManagementCompiler/Domain/ProposalEntities.cs`, and `src/ProjectManagementCompiler/Domain/Diagnostics.cs`; acceptance: types compile and expose no local absolute path fields.
- [X] T002 [P] Add `IIdeaEngineeringManifestImporter`, source-reader abstractions, and in-memory application state seams in `src/ProjectManagementCompiler/Application/ManifestImport/IIdeaEngineeringManifestImporter.cs`, `src/ProjectManagementCompiler/Sources/IManifestSourceReader.cs`, and `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs`; acceptance: public interfaces hide Git/filesystem details.
- [X] T003 [P] Validate the source-owned public-safe manifest, schema, and seven-case fixture catalogue through the accepted Git-object boundary; acceptance: the catalogue is consumed without copying the IDEAEngineering checkout, credentials, private paths, or generated files.
- [X] T004 Register the new test file and importer service construction in `tests/ProjectManagementCompiler.Tests/Program.cs` and `src/ProjectManagementCompiler/Program.cs`; acceptance: the test runner discovers the new tests while existing tests still build.

## Phase 2: User Story 1 - Authoritative snapshot (Priority: P1)

**Goal**: Import one exact accepted Git commit through the manifest and produce a
validated, reproducible snapshot with declared authority and source metadata.

**Independent test**: `ManifestImportTests` imports the accepted commit through a fake
or safe Git reader and asserts source identity, classification, totals, diagnostics,
and deterministic snapshot ID without prior application state.

### Tests first

- [X] T005 [US1] Add failing public-seam tests for exact commit validation, missing/non-commit object, repository-relative manifest path, and no fallback scanning in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`; acceptance: tests fail for the intended missing importer behavior.
- [X] T006 [US1] Add authority and fixture-oracle tests for seven roles, owner-wins conflicts, duplicate/missing roles, typed identity, dependency targets/cycles, and catalogue PASS/PASS_WITH_WARNINGS/FAIL cases in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`, `tests/ProjectManagementCompiler.Tests/AuthorityResolutionTests.cs`, and the readiness regression suites; acceptance: the public test runner covers the source catalogue and authority boundaries.
- [X] T007 [US1] Add failing accepted-source compatibility assertions for 6/35/53/7 and 512/88/600 totals plus `PASS_WITH_WARNINGS` and `PMC-CALENDAR-001` in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`; acceptance: tests fail before adapters exist.

### Implementation

- [X] T008 [US1] Implement safe Git object capture with commit-object validation, argument-list process invocation, manifest-first reads, declared-path-only reads, bounded sizes, and safe relative provenance in `src/ProjectManagementCompiler/Sources/ManifestGitObjectReader.cs`; acceptance: all captured blobs resolve from the requested commit and no shell-concatenated user input is used.
- [X] T009 [US1] Implement manifest parsing, contract `0.1.0` allow-list, path normalization/containment, role uniqueness, and bounded source document capture in `src/ProjectManagementCompiler/Extraction/ManifestContractParser.cs`, `src/ProjectManagementCompiler/Sources/ManifestCaptureSupport.cs`, and `src/ProjectManagementCompiler/Sources/ManifestGitObjectReader.cs`; acceptance: unsafe/missing/undeclared/ambiguous inputs fail with stable diagnostics and no legacy fallback occurs.
- [X] T010 [US1] Implement the bounded `System.Text.Json` schema evaluator for the source contract subset and fail-closed unsupported dialect/keyword handling in `src/ProjectManagementCompiler/Extraction/BoundedJsonSchemaValidator.cs`; acceptance: supported source schemas validate in-process and unsupported semantics emit `PMC-CONTRACT-002`.
- [X] T011 [US1] Implement manifest planning extraction and owner-wins authority mapping using typed identities in `src/ProjectManagementCompiler/Application/ManifestImport/IdeaEngineeringManifestImporter.cs`, `src/ProjectManagementCompiler/Extraction/AuthorityResolution.cs`, and `src/ProjectManagementCompiler/Extraction/IdeaPlanningDiscovery.cs`; acceptance: accepted source totals, dependency checks, and source provenance are correct without read-order authority.
- [X] T012 [US1] Implement `IIdeaEngineeringManifestImporter.ImportAsync` orchestration, deterministic snapshot ID, validation classification, and atomic `ManifestImportResult` construction in `src/ProjectManagementCompiler/Application/ManifestImport/IdeaEngineeringManifestImporter.cs`; acceptance: accepted exact commit produces a classified official result with immutable metadata and stable diagnostics, while an unready commit remains a candidate.
- [X] T013 [US1] Run focused US1 tests in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`, the authority/readiness regression suites, and the fixture catalogue validator, then refactor without changing the public contract; acceptance: all T005-T007 tests pass and legacy source/application tests remain green.

## Phase 3: User Story 2 - Execution truth (Priority: P1)

**Goal**: Preserve source execution recording/state/result/unknowns separately from
planning and readiness evidence.

**Independent test**: Import accepted source and inspect P01 plus every other delivery
card through the public importer and official analysis.

### Tests first

- [X] T014 [US2] Add failing execution-register tests for P01 values, 52 `NOT_RECORDED` cards, null actual/remaining fields, independent result state, unknown forecast, register revision, and status date in `tests/ProjectManagementCompiler.Tests/ManifestExecutionTests.cs`; acceptance: tests fail before the register adapter exists.
- [X] T015 [US2] Add tests proving readiness evidence cannot overwrite source execution and official metrics do not consume proposals in `tests/ProjectManagementCompiler.Tests/ManifestExecutionTests.cs`, `tests/ProjectManagementCompiler.Tests/ManagementEvidenceTests.cs`, and `tests/ProjectManagementCompiler.Tests/MetricsTests.cs`; acceptance: tests fail for any inferred actual/progress.

### Implementation

- [X] T016 [US2] Implement source execution register parsing, schema/semantic checks, typed card matching, completion-evidence rules, and unknown preservation in `src/ProjectManagementCompiler/Extraction/ManifestExecutionAdapter.cs` and `src/ProjectManagementCompiler/Domain/ManifestImportEntities.cs`; acceptance: P01 and all 52 unrecorded cards match source exactly.
- [X] T017 [US2] Add source execution to canonical project and update analysis/metrics to use it for official mode while retaining schema-1 read compatibility in `src/ProjectManagementCompiler/Domain/CanonicalProject.cs`, `src/ProjectManagementCompiler/Management/ExecutionTruthResolver.cs`, and `src/ProjectManagementCompiler/Management/ManagementMetricsAnalyzer.cs`; acceptance: calculated forecast is unknown without remaining evidence and baseline remains visible.
- [X] T018 [US2] Keep readiness evidence as a separate collection and attach independent provenance/diagnostics in `src/ProjectManagementCompiler/Management/IdeaEngineeringReadinessAdapter.cs` and `src/ProjectManagementCompiler/Application/ManifestImport/IdeaEngineeringManifestImporter.cs`; acceptance: readiness conflicts produce diagnostics but never change execution state/result.
- [X] T019 [US2] Run focused execution and metrics tests in `tests/ProjectManagementCompiler.Tests/ManifestExecutionTests.cs`, `tests/ProjectManagementCompiler.Tests/MetricsTests.cs`, and the existing readiness/evidence regression tests; acceptance: T014-T015 pass and no prior management test regresses.

## Phase 4: User Story 3 - Candidates and previews (Priority: P2)

**Goal**: Capture stable working-tree previews and unready candidates without replacing
the last valid official snapshot.

**Independent test**: Load official state, import failed/unready/working-tree inputs,
and compare current official, latest attempt, and active preview.

### Tests first

- [X] T020 [US3] Add tests for working-tree pre/post mutation, reparse/link escape, size limits, deterministic preview identity, and preview classification in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`, `tests/ProjectManagementCompiler.Tests/SourceCaptureTests.cs`, and `scripts/verify-web.ps1`; acceptance: the public boundary rejects mixed or unsafe previews.
- [X] T021 [US3] Add application-state and loopback tests for failed candidate retention, unready committed `CANDIDATE_PREVIEW`, valid `UNCOMMITTED_PREVIEW`, and last-valid official preservation in `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs`, `tests/ProjectManagementCompiler.Tests/ProposalTests.cs`, and `scripts/verify-web.ps1`; acceptance: failed and preview attempts never replace official state.

### Implementation

- [X] T022 [US3] Implement stability-checked working-tree capture, safe file/reparse protections, and preview identity in `src/ProjectManagementCompiler/Sources/ManifestWorkingTreeReader.cs`; acceptance: content/HEAD/status mutation during capture returns `PMC-SNAPSHOT-002` and no mixed snapshot.
- [X] T023 [US3] Implement official/candidate/preview classification from validation and source readiness in `src/ProjectManagementCompiler/Application/ManifestImport/IdeaEngineeringManifestImporter.cs`; acceptance: only source-ready exact commits can become official.
- [X] T024 [US3] Implement state transitions and explicit official/latest/preview read models in `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs` and `src/ProjectManagementCompiler/Application/ManifestImport/ManifestImportApplicationService.cs`; acceptance: failed/preview results never replace official.
- [X] T025 [US3] Run focused preview/state tests in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`, `tests/ProjectManagementCompiler.Tests/ProposalTests.cs`, and `scripts/verify-web.ps1`; acceptance: T020-T021 pass and official semantic digest remains unchanged across candidate/preview attempts.

## Phase 5: User Story 4 - Local execution proposals (Priority: P3)

**Goal**: Store local execution edits as reviewable, stale-aware proposals with an
explicit non-authoritative preview and no write-back.

**Independent test**: Create/edit/preview/export a proposal, compare official digest,
then import a newer source revision and inspect lifecycle.

### Tests first

- [X] T026 [US4] Add failing proposal lifecycle tests for create/update/list, base snapshot/register revision, completion readiness, stale-base transition, official immutability, and estimated scenario labels in `tests/ProjectManagementCompiler.Tests/ProposalTests.cs`; acceptance: tests fail before proposal service exists.
- [X] T027 [US4] Add API contract checks for `/api/proposals` and `/api/execution` proposal-only compatibility behavior in `scripts/verify-web.ps1`; acceptance: responses prove routes cannot mutate official source execution.

### Implementation

- [X] T028 [US4] Implement proposal entities, validation, lifecycle/stale-base evaluation, and in-memory proposal store in `src/ProjectManagementCompiler/Domain/ProposalEntities.cs` and `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`; acceptance: proposals cannot mutate official source execution and incomplete completion stays `DRAFT`.
- [X] T029 [US4] Implement explicit proposal preview projection with estimated/non-authoritative labels in `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`; acceptance: the canonical semantic digest of official analysis is unchanged after preview, independent of JSON formatting.
- [X] T030 [US4] Add proposal routes and convert `/api/execution` into a proposal-only compatibility alias in `src/ProjectManagementCompiler/Program.cs` and `src/ProjectManagementCompiler/Application/IProjectCompiler.cs`; acceptance: responses clearly identify proposal-only behavior and no route writes source actuals.
- [X] T031 [US4] Implement deterministic `execution-proposal.json` export with safe metadata and no raw source/absolute paths in `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`; acceptance: repeated export is identical for the same proposal and contains base ID/revision/evidence/diagnostics.
- [X] T032 [US4] Run focused proposal/API/export tests through `tests/ProjectManagementCompiler.Tests/ProposalTests.cs`, `tests/ProjectManagementCompiler.Tests/CanonicalMigrationTests.cs`, and `scripts/verify-web.ps1`; acceptance: T026-T027 pass and compatibility behavior does not weaken official-authority assertions.

## Phase 6: User Story 5 - Reopen and export (Priority: P3)

**Goal**: Persist/reopen canonical schema 2.0 while migrating legacy overlays into
proposals and preserving authority metadata in exports.

**Independent test**: Reopen schema 1.0 and 2.0 artifacts and compare official,
preview, proposal, diagnostics, and export metadata.

### Tests first

- [X] T033 [US5] Add failing serializer/migration tests for schema 2.0 shape, schema 1.0 overlay migration, migration diagnostic, no official actual promotion, and deterministic digest in `tests/ProjectManagementCompiler.Tests/CanonicalMigrationTests.cs`; acceptance: tests fail before serializer changes.
- [X] T034 [US5] Add export tests for official/preview/candidate metadata, invalid-candidate blocking, and absence of local paths/raw source bodies in `tests/ProjectManagementCompiler.Tests/CanonicalMigrationTests.cs` and `scripts/verify-web.ps1`; acceptance: tests fail before export integration.

### Implementation

- [X] T035 [US5] Extend canonical model/serializer/validator/digest to write schema 2.0, read schema 1.0, migrate overlays to proposals, and preserve source metadata in `src/ProjectManagementCompiler/Domain/CanonicalProject.cs`, `src/ProjectManagementCompiler/Outputs/CanonicalJsonSerializer.cs`, `src/ProjectManagementCompiler/Outputs/CanonicalProjectValidator.cs`, and `src/ProjectManagementCompiler/Outputs/CanonicalJsonDigest.cs`; acceptance: v1/v2 tests pass and legacy actuals never become official.
- [X] T036 [US5] Integrate official/preview export selection and invalid-candidate blocking in `src/ProjectManagementCompiler/Application/ProjectCompiler.cs` and `src/ProjectManagementCompiler/Program.cs`; acceptance: exports carry authority/source metadata and previews have recognizable labels while the existing CARIO exporter remains baseline-safe.
- [X] T037 [US5] Run focused reopen/export tests in `tests/ProjectManagementCompiler.Tests/CanonicalMigrationTests.cs` and the complete canonical/CARIO regression set; acceptance: T033-T034 pass with no legacy reopen/export regression.

## Phase 7: UI, launcher, and public verification

**Purpose**: Expose the new boundary clearly without regressing the accepted Gantt
visual, and prove the end-to-end contract.

- [X] T038 [US1] Add manifest import controls for repository root, manifest path, source commit, and mode in `src/ProjectManagementCompiler/wwwroot/index.html` and `src/ProjectManagementCompiler/wwwroot/app.js`; acceptance: no personal default path is committed and the form calls the new endpoint.
- [X] T039 [US2] Add official/preview/candidate badges, source metadata, diagnostics, execution truth fields, provenance, and explicit proposal wording in `src/ProjectManagementCompiler/wwwroot/app.js` and `src/ProjectManagementCompiler/wwwroot/styles.css`; acceptance: recording state, execution state, result state, baseline, actual, remaining, forecast, blocker, health, and authority are visually distinct.
- [X] T040 [US4] Add explicit proposal preview controls and deterministic proposal export action in `src/ProjectManagementCompiler/wwwroot/app.js`; acceptance: local edits are labeled proposal and estimated/non-authoritative.
- [X] T041 [US1] Extend `scripts/run-project.ps1`, `scripts/verify-launcher.ps1`, and `scripts/verify-web.ps1` for manifest path, source commit, preview mode, and loopback manifest import while preserving legacy invocation; acceptance: launcher contract has no hardcoded personal path.
- [X] T042 [P] [US5] Update `docs/handoff/2026-09-19-ideaengineering-manifest-import-design.md` and `specs/003-ideaengineering-manifest-import/quickstart.md` with the safe manifest quickstart, proposal/no-write-back behavior, and verification flow; acceptance: documentation matches the shipped endpoints and commands without a local absolute path.
- [X] T043 [US1] Add accepted-source and full fixture compatibility checks that read catalogue expectations and verify every valid/invalid fixture outcome in `src/ProjectManagementCompiler/Extraction/ManifestFixtureCatalogueValidator.cs`, `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs`, and `scripts/verify-web.ps1`; acceptance: all seven catalogue cases match expected status/codes.
- [X] T044 [US1] Run `node --check src/ProjectManagementCompiler/wwwroot/app.js` after UI changes; acceptance: exit code 0.
- [X] T045 [US5] Run `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1` and `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1`; acceptance: both complete successfully with no new warnings/errors.

## Phase 8: Convergence, review, and integration evidence

**Purpose**: Detect unbuilt spec work, perform manual standards/spec review without
subagents, and collect fresh evidence before integration.

- [X] T046 [US1] Run Spec Kit analyze using `specs/003-ideaengineering-manifest-import/plan.md`, `spec.md`, `tasks.md`, and all design artifacts; acceptance: no critical inconsistency remains and analysis does not silently modify artifacts.
- [X] T047 [US1] Run Spec Kit converge against implementation and append any missing work to `specs/003-ideaengineering-manifest-import/tasks.md`; acceptance: the implementation converged after reconciling stale generated file references, so no additional convergence phase was required.
- [X] T048 [US5] Perform manual code review of `src/ProjectManagementCompiler/` and `tests/ProjectManagementCompiler.Tests/` against repository standards and spec/ADR authority, including Git argument safety, path containment, no fallback, state retention, proposal isolation, and serialization safety; acceptance: no blocking finding remains.
- [X] T049 [US5] Run `git diff --check`, changed-file secret/absolute-path/raw-source scans, fresh `git status --short --branch`, `git diff --stat main...HEAD`, and `git log --oneline main..HEAD`; acceptance: only intended public source/docs/tests are changed and no sensitive artifact is present.
- [X] T050 [US5] **Historical integration record** — the original Feature 003 implementation was fast-forward integrated into `main` after its verification gates; this row is audit provenance and is not a current branch or push instruction.

## Phase 9: MVP2.2 Correctness & Hardening

**Purpose**: Close the audited authority, validation, capture, persistence,
evidence, and presentation gaps without adding product scope. Every numbered
hardening group follows red → confirmed failure → smallest fix → focused tests →
related regressions → refactor after green. Tests must use public application or
importer seams; small Git/filesystem fakes are allowed only at the capture
boundary.

### Artifact gate and migration authority

- [X] T051 [US5] Re-read `spec.md`, `plan.md`, `research.md`, `data-model.md`, all contracts, `quickstart.md`, and the handoff; run the feature prerequisite check with `SPECIFY_FEATURE_DIRECTORY` set explicitly; acceptance: all artifacts describe .NET 10, dependency-free execution, state-owned proposals, semantic v2 validation, and neutralized schema 1.0 overlays.
- [X] T052 [US5] Add failing regression tests in `tests/ProjectManagementCompiler.Tests/CanonicalMigrationTests.cs`, analysis/projection coverage, and Gantt coverage for schema 1.0 `IN_PROGRESS` and `COMPLETED` overlays; acceptance: before the fix, migrated overlay data can be observed as an effective fallback, an alert/analysis input, or an `ACTUAL` lane, and the test names record each forbidden path (FR-043, SC-011).
- [X] T053 [US5] Fix schema 1.0 migration and execution authority in `src/ProjectManagementCompiler/Outputs/CanonicalJsonSerializer.cs`, `src/ProjectManagementCompiler/Domain/CanonicalProject.cs`, and the resolver/analysis projections; acceptance: every legacy value is preserved only in a proposal, `ExecutionOverlay` is empty/neutralized with a diagnostic, and resolver, dashboard, alerts, effort, and Gantt actuals ignore it (FR-036, FR-043).

### Canonical v2 semantic validation

- [X] T054 [US5] Add failing tamper tests in `tests/ProjectManagementCompiler.Tests/CanonicalMigrationTests.cs` for invalid `ImportMetadata`, `SourceExecution`, proposal identity/target/lifecycle, unsafe evidence, and invalid state combinations; acceptance: JSON deserialization succeeds but canonical reopen is rejected and the prior official snapshot remains unchanged (FR-044, SC-012).
- [X] T055 [US5] Implement fail-closed semantic validation in `src/ProjectManagementCompiler/Outputs/CanonicalProjectValidator.cs` and related domain validators; acceptance: schema 2.0 validation covers import metadata, source execution, proposals, identities, targets, paths, lifecycle, evidence, and authority invariants without trusting serializer defaults (FR-044).

### Working-tree and bounded Git capture

- [X] T056 [US1] Add failing deterministic capture tests in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs` or `SourceCaptureTests.cs` for manifest/declaration/declared-file mutation between independent pre/post reads and for two unavailable Git-state reads; acceptance: mutation and unavailable state reject the preview and never replace official state, with no equal magic fallback sentinel (FR-045, FR-046, SC-013).
- [X] T057 [US1] Implement independent pre/post reads and fail-closed Git-state handling in `src/ProjectManagementCompiler/Sources/ManifestWorkingTreeReader.cs` and its capture seam; acceptance: no manifest or state fingerprint is reused across probes and a failed `HEAD`/status read cannot compare equal as stable (FR-045, FR-046).
- [X] T058 [US1] Add failing Git object tests for symlink tree mode `120000`, oversized blob rejection before body read, caller-limit bypass, and one bounded regular blob in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs` or `SourceCaptureTests.cs`; acceptance: the oversized test proves body-read was not invoked and symlink is rejected (FR-047, SC-014).
- [X] T059 [US1] Implement tree/mode/blob-size checks and hard application ceilings in `src/ProjectManagementCompiler/Sources/ManifestGitObjectReader.cs`, `src/ProjectManagementCompiler/Domain/ManifestImportEntities.cs`, and importer configuration; acceptance: mode and size are checked before body reads, symlink mode is rejected, hard ceilings cannot be raised by caller input, and bounded regular blobs remain readable (FR-047).

### Evidence and proposal ownership

- [X] T060 [US4] **Historical MVP2.2 baseline record** — add controlled-evidence tests in `tests/ProjectManagementCompiler.Tests/ProposalTests.cs` for empty/default/malformed evidence and one valid evidence record; acceptance: the baseline established draft-empty behavior and rejected malformed evidence without changing official execution. The current contract, including required `Result`, is covered by T087/T088 (FR-048, SC-015).
- [X] T061 [US4] **Historical MVP2.2 baseline record** — implement readiness validation in `src/ProjectManagementCompiler/Domain/ProposalEntities.cs` and `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`; acceptance: the baseline established required identity, type, description, recording time, recorder, and safe provenance behavior. The current source-compatible `Result` and optional-locator boundary is covered by T088 (FR-048).
- [X] T062 [US4] Add failing save/reopen/list tests in `tests/ProjectManagementCompiler.Tests/ProposalTests.cs`, `CanonicalMigrationTests.cs`, and application/API coverage; acceptance: a created proposal survives canonical save, reopen, hydration, list, and stale-base evaluation with the same semantic identity and source execution remains immutable (FR-049, SC-016).
- [X] T063 [US4] Replace the service-owned mutable proposal dictionary with `CompilerApplicationState` ownership and canonical projection/hydration in `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs`, `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`, `src/ProjectManagementCompiler/Application/ProjectCompiler.cs`, and `src/ProjectManagementCompiler/Program.cs`; acceptance: create/update/list/migration/save/reopen/stale-base all use one retained set and schema 1.0 migrated proposals hydrate through the same path (FR-049).

### UI semantics and documentation truth

- [X] T064 [US2] Add a deterministic UI regression check for manifest-workflow labels in `scripts/verify-web.ps1` or the existing public web verification; acceptance: source execution is not called manual, proposal action is not called record execution, and source forecast is not called calculated forecast (FR-050, SC-017).
- [X] T065 [US2] Correct only the affected wording in `src/ProjectManagementCompiler/wwwroot/app.js` and related text resources; acceptance: no layout or product redesign is introduced and all authority labels remain visually distinct (FR-050).
- [X] T066 [US5] Reconcile implementation-facing documentation in `plan.md`, `research.md`, `data-model.md`, all contracts, `tasks.md`, `quickstart.md`, and `docs/handoff/2026-09-19-ideaengineering-manifest-import-design.md`; acceptance: no obsolete target or dependency, independent proposal-store, deserialize-only validation, or legacy-overlay-authority claim remains (FR-044, FR-049, FR-050).

### Hardening verification

- [X] T067 [US1] Run the focused regression groups after each green fix and then the related canonical, importer, proposal, API, and Gantt regressions; acceptance: all T052/T054/T056/T058/T060/T062/T064 tests pass in the current worktree.
- [X] T068 [US5] Run Spec Kit analyze again after implementation and resolve every critical/high inconsistency; acceptance: analyze reports artifact/code/task coherence and does not modify artifacts silently.
- [X] T069 [US5] Run Spec Kit converge again after implementation and reconcile any remaining implementation gaps in `tasks.md`; acceptance: no unimplemented hardening requirement remains before review.
- [X] T070 [US5] Perform code review against repository standards and Feature 003 authority, including fail-closed behavior, bounded reads, source immutability, proposal ownership, and UI truth; acceptance: no blocking finding remains.
- [X] T071 [US5] Run fresh full verification: `scripts/test.ps1`, `scripts/verify.ps1`, `scripts/verify-web.ps1`, `node --check src/ProjectManagementCompiler/wwwroot/app.js`, `git diff --check`, and changed-file safety scans; acceptance: every command exits successfully with exact output recorded in `docs/handoff/2026-09-19-ideaengineering-manifest-import-design.md`.
- [X] T072 [US5] **Historical baseline** — only after T068-T071 are green, integrate the verified feature branch into `main` with a fast-forward-only update, push `main`, and verify the remote SHA; acceptance: no force push, no merge commit, and remote `main` equals the verified commit. This completed task records the hardening integration gate and is not a current branch-name instruction.

## Phase 10: Convergence

**Purpose**: Preserve the remaining implementation gaps observed by the explicit
pre-implementation converge pass as historical records. T073-T079 are not a
second implementation scope and do not authorize new work; their current behavior
is represented by the primary Phase 9 tasks they reference.

- [X] T073 [US5] **Historical convergence record for T053** — the schema 1.0 overlay neutralization and forbidden resolver/analysis/dashboard/alert/Gantt fallbacks were closed under FR-043; no additional implementation scope remains in this convergence row.
- [X] T074 [US5] **Historical convergence record for T055** — semantic v2 validation for metadata, source execution, proposal targets/lifecycle, evidence, and authority combinations was closed under FR-044; no additional implementation scope remains in this convergence row.
- [X] T075 [US1] **Historical convergence record for T057** — independent working-tree pre/post reads and fail-closed Git-state handling were closed under FR-045/FR-046; no additional implementation scope remains in this convergence row.
- [X] T076 [US1] **Historical convergence record for T059** — pre-body Git tree mode/blob-size checks, symlink rejection, and hard ceilings were closed under FR-047; no additional implementation scope remains in this convergence row.
- [X] T077 [US4] **Historical convergence record for T061/T088** — controlled proposal evidence validation and atomic readiness boundaries were closed under FR-048/FR-053; no additional implementation scope remains in this convergence row.
- [X] T078 [US4] **Historical convergence record for T063/T094** — single proposal ownership and canonical reopen/save/list hydration were closed under FR-049/SC-016; no additional implementation scope remains in this convergence row.
- [X] T079 [US2] **Historical convergence record for T065** — manifest-workflow source/proposal/source-forecast wording was closed under FR-050/SC-017; no additional implementation scope remains in this convergence row.

## Phase 11: Final correctness micro-pass

**Purpose**: Record the audited Feature 003 compatibility and semantic
corrections that followed MVP2.2 hardening. The historical implementation branch
was `codex/feature003-final-micro-pass`; that name is provenance only, not a
current branch-name instruction. Every code task required a named red regression,
confirmed failure, smallest fix, focused green run, and related regression run.

### Artifact gate

- [X] T080 [US5] Re-read the current Feature 003 spec, plan, research, data model,
  contracts, quickstart, ADR-0006, handoff, implementation, and tests against
  the accepted IDEAEngineering source contract; acceptance: the micro-pass
  requirements are explicit and no source checkout is modified (FR-051–FR-057,
  SC-018–SC-024). The historical implementation branch was
  `codex/feature003-final-micro-pass`; this is provenance only, not a current
  branch-name instruction.
- [X] T081 [US5] Run Spec Kit prerequisite check, analyze, and converge before
  implementation; acceptance: every Critical/High finding is resolved and any
  converged task is reconciled before code changes begin.
- [X] T082 [US5] Keep documentation truth synchronized with the actual .NET 10,
  dependency-free implementation, distinguishing original Feature 003, MVP2.2
  hardening, this micro-pass, and local verification; acceptance: no stale
  pre-implementation or remote-CI claim remains (FR-057, SC-024).

### TDD regressions and minimal fixes

- [X] T083 [US1] Add a failing public-path regression using
  `ProjectCompiler.ApplyExecutionUpdate` proving an explicit legacy overlay
  drives legacy analysis and Gantt `ACTUAL`, while manifest `SourceExecution`
  wins and migrated schema-1.0 overlays remain proposal-only; acceptance:
  tests are red before the resolver fix and cover analysis and Gantt separately
  (FR-051, SC-018).
- [X] T084 [US1] Restore the narrow legacy resolver branch without weakening
  manifest authority; acceptance: legacy behavior is green, overlay cannot
  override manifest source execution, and migration still clears the overlay
  (FR-051, SC-018).
- [X] T085 [US1] Add a failing Git capture regression whose aggregate remaining
  budget is below a declared blob size and whose fake proves `git show`/body read
  is never called; acceptance: symlink, hard-ceiling, and bounded regular-blob
  regressions remain covered (FR-052, SC-019).
- [X] T086 [US1] Pass remaining aggregate budget into the bounded Git blob read
  and reject before body read; acceptance: per-file, hard-file, and remaining
  total limits all apply before `git show` (FR-052, SC-019).
- [X] T087 [US4] Add failing proposal evidence boundary regressions for empty
  draft evidence, empty object, missing `RecordedBy`, missing `Result`,
  unsupported type, unsafe path, credential URI, malformed commit, valid
  source-compatible evidence without a locator, ready completion, and atomic
  state preservation; acceptance: each malformed input is red before the
  boundary fix and no malformed item can persist (FR-053, SC-020).
- [X] T088 [US4] Implement source-contract-compatible controlled evidence rules
  shared by source and proposal validation; acceptance: required fields include
  result, locators are optional but independently safe, zero evidence is draft,
  and only completion plus valid evidence yields `READY_FOR_REVIEW` (FR-048,
  FR-053, SC-015, SC-020).
- [X] T089 [US4] Add a failing `/api/execution` API/web regression for a legacy
  evidence reference; acceptance: the response contains no fabricated
  `COMPATIBILITY_UPDATE`, `RecordedBy`, or `Result`, and the official source is
  unchanged (FR-054, SC-021).
- [X] T090 [US4] Change the compatibility route to retain only a safe
  `legacyEvidenceReference` proposal field; acceptance: no unsupported
  controlled evidence is emitted and unsafe legacy references fail safely
  (FR-054, SC-021).
- [X] T091 [US5] Add failing canonical v2 tamper regressions for full Git SHA,
  credential-bearing URI identity, revision minimum, half-hour effort,
  metadata/project/baseline IDs, source IDs, and register revision/status date;
  acceptance: deserialize-successful tampering fails closed (FR-055, SC-022).
- [X] T092 [US5] Implement the smallest semantic validator/adapter corrections
  for those v2 invariants, preserving the explicit schema-1.0 migrated proposal
  revision-zero exception; acceptance: accepted source compatibility remains
  green and every tamper test rejects without official-state replacement
  (FR-055, SC-022).
- [X] T093 [US4] Add a failing mixed-session regression for official import →
  schema-1.0 reopen → proposal update → save/reopen/list; acceptance: state and
  both applicable projections agree, legacy is not promoted, and source
  execution is immutable (FR-056, SC-023).
- [X] T094 [US4] Make proposal projection updates coherent across current and
  current-official applicable results without aliasing; acceptance: migrated
  proposals use the same state owner and survive canonical round-trip (FR-056,
  SC-023).

### Post-implementation gates

- [X] T095 [US5] Run focused and related regressions after every green group and
  mark only observed work complete; acceptance: all T083/T085/T087/T089/T091/T093
  tests pass with exact counts recorded.
- [X] T096 [US5] Run Spec Kit analyze and converge after implementation and
  reconcile any remaining task; acceptance: no Critical/High finding or
  unimplemented micro-pass requirement remains.
- [X] T097 [US5] Perform manual code review against the fixed baseline and all
  authority/security contracts; acceptance: no blocking finding, no package,
  DB, connector, Docker, auth, AI, forecast, UI redesign, or source-repo change.
- [X] T098 [US5] Run fresh full verification including build/test, all repository
  verifier scripts, web/launcher checks, JavaScript syntax, diff/safety scans,
  and source compatibility read-only checks; acceptance: every command exits
  successfully with exact test count and command output captured in
  `docs/handoff/2026-09-19-ideaengineering-manifest-import-design.md`. The
  timed full `scripts/verify.ps1` gate must also complete within 120 seconds on
  the supported workstation, with elapsed time recorded in that handoff. This
  is an engineering verification bound, not a user-visible SLA. The
  verifier-discovered typed Gantt identity collision is covered by the
  red/green regression `TypedGanttIdentityAllowsSameRawIdAcrossKinds`; the
  preview identity key is `Type + ID + Lane`, preserving the existing typed-ID
  contract without adding product scope. The web harness uses a temporary
  `.xlsx` filename so the fail-closed preview extension contract is exercised
  rather than bypassed or misreported.
- [X] T099 [US5] Only after T096-T098 are green, fetch `origin/main`, fast-forward
  `main`, merge this branch with `--ff-only`, push `main`, and verify the remote
  SHA; acceptance: no force push, no merge commit, and the final report does not
  claim Feature 003/MVP2.2 closure unless every DoD item is actually green.

## Dependencies and execution order

- Phase 1 blocks all user stories.
- US1 and US2 are the MVP foundation; US3 depends on the importer result and state
  model from US1; US4 depends on source execution from US2 and state from US3; US5
  depends on all canonical/proposal models.
- UI, launcher, and compatibility fixture work begins after the relevant public
  operations exist.
- Convergence and review occur only after implementation and verification commands
  have fresh output.
- MVP2.2 hardening follows the explicit artifact order: update/reconcile design
  artifacts, run analyze, run converge, re-run analyze if converge changes
  tasks, then begin implementation. TDD is required inside each hardening group;
  it does not replace Spec Kit.

## Requirement traceability

The matrix below makes the semantic coverage already present in the task text
explicit. Acceptance details remain on the referenced task rows; a task may cover
more than one requirement.

| Requirement | Task IDs |
|---|---|
| FR-001 | T005, T041 |
| FR-002 | T005, T009 |
| FR-003 | T009, T010 |
| FR-004 | T005, T009, T020, T022 |
| FR-005 | T005, T008, T020, T022 |
| FR-006 | T021, T024 |
| FR-007 | T006, T011 |
| FR-008 | T006, T011 |
| FR-009 | T010, T016 |
| FR-010 | T001, T009, T012 |
| FR-011 | T006, T011, T016 |
| FR-012 | T006, T007, T011, T016, T018, T043 |
| FR-013 | T001, T012, T035, T036 |
| FR-014 | T012, T020, T022 |
| FR-015 | T014, T016, T017 |
| FR-016 | T014, T016 |
| FR-017 | T014, T016 |
| FR-018 | T007, T014, T016 |
| FR-019 | T007, T014, T016 |
| FR-020 | T015, T017, T039 |
| FR-021 | T014, T017 |
| FR-022 | T007, T011, T043 |
| FR-023 | T015, T018 |
| FR-024 | T015, T017, T026, T028 |
| FR-025 | T015, T029, T031 |
| FR-026 | T026, T028 |
| FR-027 | T026, T028 |
| FR-028 | T026, T029, T040 |
| FR-029 | T026, T028, T060, T061, T087, T088 |
| FR-030 | T031, T034, T036 |
| FR-031 | T015, T028, T030, T042 |
| FR-032 | T023, T024, T039 |
| FR-033 | T038, T039, T041 |
| FR-034 | T039 |
| FR-035 | T014, T017, T039 |
| FR-036 | T033, T035, T052, T053 |
| FR-037 | T034, T036 |
| FR-038 | T012, T023, T024 |
| FR-039 | T038, T041 |
| FR-040 | T003, T006, T043 |
| FR-041 | T003, T031, T034, T049 |
| FR-042 | T009, T041, T084 |
| FR-043 | T052, T053, T073 |
| FR-044 | T054, T055, T074, T091, T092 |
| FR-045 | T020, T022, T056, T057, T075 |
| FR-046 | T020, T022, T056, T057, T075 |
| FR-047 | T058, T059, T076, T085, T086 |
| FR-048 | T060, T061, T077, T087, T088 |
| FR-049 | T062, T063, T078, T093, T094 |
| FR-050 | T064, T065, T079 |
| FR-051 | T083, T084 |
| FR-052 | T085, T086 |
| FR-053 | T087, T088 |
| FR-054 | T089, T090 |
| FR-055 | T091, T092 |
| FR-056 | T093, T094 |
| FR-057 | T080, T082, T096, T098 |
| SC-001 | T007, T011, T013, T043 |
| SC-002 | T006, T043 |
| SC-003 | T007, T014, T016, T019 |
| SC-004 | T005, T006, T009, T010, T020, T043 |
| SC-005 | T038, T039, T041 |
| SC-006 | T015, T017, T029 |
| SC-007 | T033, T035, T037, T052, T053 |
| SC-008 | T021, T023, T024, T025 |
| SC-009 | T049, T071, T098 |
| SC-010 | T013, T019, T037, T045, T071, T084 |
| SC-011 | T052, T053 |
| SC-012 | T054, T055, T091, T092 |
| SC-013 | T020, T022, T056, T057 |
| SC-014 | T058, T059, T085, T086 |
| SC-015 | T060, T061, T088 |
| SC-016 | T062, T063, T093, T094 |
| SC-017 | T064, T065 |
| SC-018 | T083, T084 |
| SC-019 | T085, T086 |
| SC-020 | T087, T088 |
| SC-021 | T089, T090 |
| SC-022 | T091, T092 |
| SC-023 | T093, T094 |
| SC-024 | T080, T082, T098 |

## TDD checkpoints

For each story, test tasks are intentionally before implementation tasks. At every
checkpoint, run focused tests, then related regression tests. Mark a task `[X]` only
after the acceptance evidence has been observed in the current worktree.
