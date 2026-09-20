# Tasks: Read-only XLSX Gantt Preview Import

**Input**: Design documents from `specs/004-xlsx-gantt-preview-import/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, and
`contracts/xlsx-preview.md`

**Tests**: Required. The user explicitly requested the Spec Kit flow and the
implementation plan requires red-green TDD for every behavior group.

**Organization**: Tasks are grouped by user story. Every test task must be run
and observed failing before its corresponding production task is started.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Establish deterministic workbook fixtures and test registration
without adding dependencies.

- [X] T001 [P] Add a reusable exported-workbook fixture and ZIP/XML mutation helpers in `tests/ProjectManagementCompiler.Tests/XlsxPreviewTestFixtures.cs` for producing valid bytes and controlled missing-sheet, marker, header, date, duplicate-identity, corrupt-package, and oversized-entry cases.
- [X] T002 Register the feature test methods in execution order in `tests/ProjectManagementCompiler.Tests/Program.cs`, preserving the executable test runner's existing zero-failure reporting contract.

## Phase 2: Foundational (Preview Boundary)

**Purpose**: Establish a clean baseline and keep all production seams behind
the pre-implementation Spec Kit gates.

- [X] T003 Run the baseline executable test suite through `scripts/test.ps1` and record the existing pass/fail count before feature implementation; do not change production code. Baseline: `238 PASS / 0 FAIL` with `IDEAENGINEERING_ROOT` configured.
- [X] T004 Run the baseline build through `scripts/build.ps1` and record the existing warning/error count before feature implementation; do not change production code. Isolated baseline build: `0 Warning / 0 Error` using a temporary `BaseOutputPath` because the running local app held the normal DLL lock.

**Checkpoint**: Baseline verification is recorded and no feature production
code is implemented before the Spec Kit gates.

## Phase 2A: Spec Kit Pre-Implementation Gates

**Purpose**: Prove that the specification, plan, contracts, and task list are
consistent before any production implementation begins.

- [X] T005 Run `speckit-analyze` against `spec.md`, `plan.md`, and `tasks.md`, resolve every CRITICAL/HIGH artifact finding, and leave production code untouched. Result: no remaining CRITICAL/HIGH findings after adding the UI-test and manifest-service paths to `plan.md`.
- [X] T006 Run `speckit-converge` against the current codebase and approved artifacts, append any remaining pre-implementation work to this `tasks.md`, and do not begin implementation until the artifacts are consistent. Result: no uncovered gap; existing T001–T029 already represent every missing preview seam, so no duplicate convergence tasks were appended.

## Phase 3: User Story 1 - View an exported workbook (Priority: P1) 🎯 MVP

**Goal**: A newly exported PMC workbook can be selected and rendered as an
isolated daily Gantt plus task-table preview.

**Independent Test**: Export a valid seven-sheet workbook, upload it through the
preview path, and verify project identity, task rows, date axis, lane values,
`Recorded %`, and `Not recorded` values without using official analysis.

### Tests for User Story 1 (TDD RED first)

- [X] T007 [US1] Add a failing exporter contract regression in `tests/ProjectManagementCompiler.Tests/CarioXlsxTests.cs` proving `05_PROJECT_INFO` contains exactly one `PMC_EXPORT_KIND=CARIO_GANTT`, `PMC_EXPORT_CONTRACT_VERSION=1.0`, `PMC_PROJECT_ID`, and `PMC_PROJECT_NAME` marker row set while the workbook remains seven sheets.
- [X] T008 [US1] Add a failing valid-import regression in `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs` proving a valid exported workbook returns project identity, the task table, an ordered daily axis, PLAN/ACTUAL/ALERT/MILESTONE rows, and exact `Recorded %`/`Not recorded` display values.
- [X] T009 [US1] Add a failing application/API behavior regression in `tests/ProjectManagementCompiler.Tests/XlsxPreviewApplicationTests.cs` proving a successful upload creates an active preview without replacing `CompilerApplicationState.CurrentOfficialResult`.
- [X] T010 [US1] Add failing static UI regressions in `tests/ProjectManagementCompiler.Tests/XlsxPreviewUiTests.cs` proving the source-intake picker, `XLSX Preview` banner, official-mode return action, and read-only task/Gantt surface are required before UI production changes.

### Implementation for User Story 1

- [X] T011 [US1] Add the four reserved marker rows to `src/ProjectManagementCompiler/Outputs/CarioXlsxExporter.cs` without adding an eighth sheet or changing existing CARIO column headers; make the exporter test in T007 green.
- [X] T012 [US1] Implement bounded valid-package parsing in `src/ProjectManagementCompiler/Outputs/XlsxPreviewImporter.cs` and `src/ProjectManagementCompiler/Outputs/XlsxPreviewModel.cs`, parsing `01_TASKS`, `05_PROJECT_INFO`, and `07_GANTT` while preserving exported values and row order; make T008 green.
- [X] T013 [US1] Add `ActiveXlsxPreview`, replacement, clear, and read accessors to `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs`, then add `POST /api/xlsx-preview` and `GET /api/xlsx-preview` in `src/ProjectManagementCompiler/Program.cs`; make T009 green without calling `Set`, `BuildImportedResult`, or proposal services.
- [X] T014 [US1] Add the source-intake file picker, preview mode banner, official-mode return action, and read-only task/Gantt rendering in `src/ProjectManagementCompiler/wwwroot/index.html`, `src/ProjectManagementCompiler/wwwroot/app.js`, and `src/ProjectManagementCompiler/wwwroot/styles.css`; make T010 green and make the P1 preview flow visible.

**Checkpoint**: User Story 1 is independently demonstrable: a fresh marked
workbook imports, displays, and leaves official state untouched.

## Phase 4: User Story 2 - Reject untrusted workbook content (Priority: P2)

**Goal**: Invalid, altered, incomplete, malformed, or oversized workbooks fail
closed with actionable diagnostics and never render partial rows.

**Independent Test**: Run each mutation fixture against the importer and HTTP
endpoint; verify the expected diagnostic and unchanged official/preview state.

### Tests for User Story 2 (TDD RED first)

- [X] T015 [US2] Add failing importer regressions in `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs` for missing/duplicate sheets, missing/conflicting marker rows, unsupported contract version, malformed required headers, invalid dates/numbers, duplicate task identity, corrupt ZIP, and external/unsupported package relationships.
- [X] T016 [US2] Add failing bounded-read regressions in `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs` proving oversized ZIP entries and aggregate package limits are rejected before their bodies are read and caller-controlled values cannot raise the application ceiling.
- [X] T017 [US2] Add a failing HTTP/state regression in `tests/ProjectManagementCompiler.Tests/XlsxPreviewApplicationTests.cs` proving a failed upload returns the existing API error shape, leaves the official result unchanged, and leaves a previously valid preview unchanged.

### Implementation for User Story 2

- [X] T018 [US2] Implement strict workbook package, sheet-order, relationship, marker, header, cell-value, identity, date-axis, and lane validation in `src/ProjectManagementCompiler/Outputs/XlsxPreviewImporter.cs`; make T015 green with structured `PMC-XLSX-*` diagnostics.
- [X] T019 [US2] Implement hard request, ZIP entry-count, per-entry, and aggregate size ceilings and reject-before-body-read behavior in `src/ProjectManagementCompiler/Outputs/XlsxPreviewImporter.cs`; make T016 green.
- [X] T020 [US2] Map importer failures to `422 INVALID_XLSX_PREVIEW` and preserve state on failure in `src/ProjectManagementCompiler/Program.cs` and `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs`; make T017 green.

**Checkpoint**: User Story 2 is independently testable: every malformed or
untrusted fixture is rejected without a partial or substituted preview.

## Phase 5: User Story 3 - Preserve source authority and lifecycle (Priority: P3)

**Goal**: Preview mode remains temporary, non-authoritative, and isolated from
official analysis and proposal behavior.

**Independent Test**: Load an official manifest, create/list a proposal, import
and clear a preview, import a new official manifest, and restart the app; verify
all authority and lifecycle acceptance scenarios.

### Tests for User Story 3 (TDD RED first)

- [X] T021 [US3] Add failing lifecycle regressions in `tests/ProjectManagementCompiler.Tests/XlsxPreviewApplicationTests.cs` proving preview import does not alter official project, source execution, analysis, alerts, or proposal projection, and that `DELETE /api/xlsx-preview` clears only the preview.
- [X] T022 [US3] Add a failing official-import lifecycle regression in `tests/ProjectManagementCompiler.Tests/ManifestImportTests.cs` or `tests/ProjectManagementCompiler.Tests/XlsxPreviewApplicationTests.cs` proving a new official manifest import clears the active XLSX preview while retaining official proposals according to the existing state rules.
- [X] T023 [US3] Add failing UI regressions in `tests/ProjectManagementCompiler.Tests/XlsxPreviewUiTests.cs` proving preview mode labels are explicit, proposal controls are unavailable, `Official source` restores official mode, and the preview is not persisted or rehydrated by startup code.

### Implementation for User Story 3

- [X] T024 [US3] Add `DELETE /api/xlsx-preview`, `NO_ACTIVE_XLSX_PREVIEW`, and official-import preview clearing in `src/ProjectManagementCompiler/Program.cs`, `src/ProjectManagementCompiler/Application/CompilerApplicationState.cs`, and `src/ProjectManagementCompiler/Application/ManifestImport/ManifestImportApplicationService.cs`; make T021 and T022 green.
- [X] T025 [US3] Complete preview-mode guards, official/preview switching, read-only task/Gantt rendering, and proposal-control hiding in `src/ProjectManagementCompiler/wwwroot/app.js`, `src/ProjectManagementCompiler/wwwroot/index.html`, and `src/ProjectManagementCompiler/wwwroot/styles.css`; make T023 green.

**Checkpoint**: All user stories are independently testable and the workbook
cannot become an official source or proposal input.

## Phase 6: Polish & Cross-Cutting Verification

**Purpose**: Keep artifacts, runbooks, and verification aligned with the actual
implementation and run the complete quality gates.

- [X] T026 [P] Update `specs/001-mvp1-project-management-compiler/contracts/cario-workbook.md`, `specs/001-mvp1-project-management-compiler/spec.md`, and `docs/runbook/mvp1-local.md` to document the marker, fresh-export requirement, XLSX preview boundary, and exact user flow without claiming arbitrary Excel import.
- [X] T027 [P] Extend `scripts/verify-web.ps1` with a fresh marked-workbook upload/preview/clear smoke path and explicit assertions for read-only/non-authoritative labels, without writing to IDEAEngineering.
- [X] T028 Run the feature quickstart from `specs/004-xlsx-gantt-preview-import/quickstart.md`, focused tests, the full executable test suite, `dotnet build`, `git diff --check`, and package-level XLSX verification; record exact results in the handoff or final report. Verification: solution build `0 Warning / 0 Error`; executable suite `251 PASS / 0 FAIL`; node and PowerShell syntax checks pass; package-level marker/import/size/lifecycle tests pass.
- [ ] T029 Run `$speckit-analyze` after implementation, resolve any artifact gaps, run `$speckit-converge`, perform code review, and only then prepare final integration checks.

## Dependencies & Execution Order

### Phase Dependencies

- Setup (Phase 1) precedes all feature work.
- Foundational (Phase 2) precedes all user stories.
- User Story 1 (Phase 3) is the MVP and precedes the lifecycle integration in
  User Story 3; User Story 2 may begin after the parser seam from Phase 2 but
  must complete before final verification.
- User Story 3 depends on the active preview endpoint and model from User Story
  1 and the fail-closed endpoint behavior from User Story 2.
- Polish (Phase 6) depends on all desired user stories being green.

### Parallel Opportunities

- T001 and T003/T004 can be prepared in parallel because they affect separate
  fixture/verification surfaces; T002 follows the test registration edits.
- T007, T008, T009, and T010 can be written in parallel as independent RED tests,
  then run individually to confirm the expected failures.
- T015, T016, and T017 can be written in parallel after T012, then implemented
  sequentially because they converge on importer and state error semantics.
- T024 and T025 can be prepared in parallel after the implementation is stable.

### Within Each User Story

- Write each listed regression first and observe the expected failure.
- Implement the smallest production change that makes that regression green.
- Run the focused test, then related regressions before moving to the next task.
- Do not mark a task complete without the command output supporting it.

## Implementation Strategy

### MVP First (User Story 1)

1. Complete setup and preview-boundary tasks.
2. Add marker-aware export and valid import.
3. Add the endpoint and UI preview mode.
4. Stop and verify the P1 user journey independently.

### Incremental Delivery

1. Add fail-closed validation and bounded reads.
2. Add lifecycle clearing, explicit mode switching, and proposal isolation.
3. Update documentation and run the full verification gates.

### Notes

- No task may add a package, database, connector, Dockerfile, Office
  automation, or source-repository write.
- The existing export changes in this worktree remain separate from the feature
  artifact commit history and must not be accidentally staged by implementation
  commands.
