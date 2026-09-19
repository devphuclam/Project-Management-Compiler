---

description: "Implementation tasks for IDEAEngineering Manifest Import"
---

# Tasks: IDEAEngineering Manifest Import

**Input**: Design documents from `specs/003-ideaengineering-manifest-import/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/`

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
- [X] T029 [US4] Implement explicit proposal preview projection with estimated/non-authoritative labels in `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`; acceptance: official analysis and digest are byte-for-byte unchanged after preview.
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
- [X] T050 [US5] Fetch `origin/main`, fast-forward local `main` only after all checks, merge the implementation branch with `--ff-only`, push `main`, and verify remote SHA; acceptance: remote `main` points at the verified final commit with no force push.

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

- [X] T060 [US4] Add failing controlled-evidence tests in `tests/ProjectManagementCompiler.Tests/ProposalTests.cs` for empty/default/malformed evidence and one valid evidence record; acceptance: malformed evidence remains diagnosed and `DRAFT`, while valid controlled evidence alone permits `READY_FOR_REVIEW` without changing official execution (FR-048, SC-015).
- [X] T061 [US4] Implement controlled evidence validation in `src/ProjectManagementCompiler/Domain/ProposalEntities.cs` and `src/ProjectManagementCompiler/Application/ManifestImport/ExecutionProposalService.cs`; acceptance: required identity, type, description, recording time, recorder, and safe path/URI rules are enforced before readiness (FR-048).
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
- [X] T071 [US5] Run fresh full verification: `scripts/test.ps1`, `scripts/verify.ps1`, `scripts/verify-web.ps1`, `node --check src/ProjectManagementCompiler/wwwroot/app.js`, `git diff --check`, and changed-file safety scans; acceptance: every command exits successfully with exact output recorded in the final report.
- [X] T072 [US5] Only after T068-T071 are green, fetch `origin/main`, fast-forward local `main`, merge `codex/feature003-hardening` with `--ff-only`, push `main`, and verify the remote SHA; acceptance: no force push, no merge commit, and remote `main` equals the verified commit.

## Phase 10: Convergence

**Purpose**: Record the remaining implementation gaps observed by the explicit
pre-implementation converge pass. These tasks are executed through the Phase 9
TDD groups; they do not authorize implementation before the artifact gate.

- [X] T073 [US5] Neutralize the legacy overlay after schema 1.0 migration and remove every effective resolver/analysis/dashboard/alert/Gantt fallback from `CanonicalJsonSerializer`, `ExecutionTruthResolver`, and related projections per FR-043 (contradicts).
- [X] T074 [US5] Add semantic v2 validation for import metadata, source execution, proposal targets/lifecycle, evidence, and authority combinations in `CanonicalProjectValidator` per FR-044 (partial).
- [X] T075 [US1] Replace cached working-tree manifest/bytes and comparable `git-state-unavailable` values with independent pre/post reads that fail closed per FR-045 and FR-046 (partial).
- [X] T076 [US1] Add pre-body Git tree mode/blob-size checks, symlink rejection, and hard application ceilings that cannot be bypassed by caller limits per FR-047 (missing).
- [X] T077 [US4] Validate controlled proposal evidence fields before lifecycle readiness so malformed non-empty evidence cannot produce `READY_FOR_REVIEW` per FR-048 (partial).
- [X] T078 [US4] Make `CompilerApplicationState` the only mutable proposal owner and hydrate it on canonical reopen/save/list, including migrated proposals, per FR-049 and SC-016 (partial).
- [X] T079 [US2] Replace manifest-workflow UI wording that says manual/record/calculated with source/proposal/source-forecast wording per FR-050 and SC-017 (partial).

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

## TDD checkpoints

For each story, test tasks are intentionally before implementation tasks. At every
checkpoint, run focused tests, then related regression tests. Mark a task `[X]` only
after the acceptance evidence has been observed in the current worktree.
