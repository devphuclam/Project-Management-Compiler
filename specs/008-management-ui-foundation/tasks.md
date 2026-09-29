# Tasks: Management UI Workspace Foundation

**Input**: Approved design artifacts from `/specs/008-management-ui-foundation/`
**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, and `contracts/`
**Execution policy**: Sequential single-agent work; no subagents. Implement one vertical slice at a time: add a focused failing test, observe the failure, implement the minimum behavior, and rerun the focused test before moving on. Keep each increment reviewable.

## Phase 1: Setup and inventory

**Purpose**: Establish the existing behavior and current validation baseline before changing the workspace.

- [X] T001 [US3] Add passing characterization assertions for the current primary and secondary destinations and key actions in `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`, based on `src/ProjectManagementCompiler/wwwroot/index.html` and `src/ProjectManagementCompiler/wwwroot/app.js`; register them in the existing test runner.

## Phase 2: Foundational contracts

**Purpose**: Establish the new behavior at the smallest source and management projection seams.

- [X] T002 [US1] Add a failing happy-path resolver test proving that `refs/remotes/origin/HEAD` is read, its allowed symbolic target resolves to one full commit ID, and the result exposes that exact ID in `tests/ProjectManagementCompiler.Tests/ManifestDefaultBranchResolverTests.cs`.
- [X] T003 [US1] Implement the typed resolver seam and happy path in `src/ProjectManagementCompiler/Sources/ManifestDefaultBranchResolver.cs`, sharing the existing Git command runner only as needed; rerun T002.
- [X] T004 [US1] Add failing resolver boundary tests for missing/malformed/disallowed refs, non-commit results, command errors, bounded output, cancellation, and absence of fetch/network/ref-mutation/checkout commands in `tests/ProjectManagementCompiler.Tests/ManifestDefaultBranchResolverTests.cs`.
- [X] T005 [US1] Implement the fail-closed validation, typed failures, limits, and cancellation behavior in `src/ProjectManagementCompiler/Sources/ManifestDefaultBranchResolver.cs`; rerun T004.
- [X] T006 [US2] Add failing progress-coverage tests for missing/invalid/negative/zero-total effort, complete versus partial coverage, partial-total labeling, midpoint rounding, and separate completion counts in `tests/ProjectManagementCompiler.Tests/ProjectOverviewProjectionTests.cs`.
- [X] T007 [US2] Implement the shared effort eligibility/progress seam in `src/ProjectManagementCompiler/Management/ProgressCoverageProjector.cs` and reuse it from `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`, `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttProjector.cs`, and `ProjectDeliveryCardDetail` projection; rerun T006 and relevant existing executive-progress tests.

## Phase 3: User Story 1 — Open the current project from a local source (Priority: P1)

**Goal**: An explicit local-source action reads the newest official commit available in the selected local copy and preserves authority and recovery behavior.

- [X] T008 [US1] Add failing application-service tests for one resolution, exact-SHA official import, forwarding optional settings, no caller-controlled mode/SHA, and retaining official state on resolution/import/candidate failure in `tests/ProjectManagementCompiler.Tests/ManifestDefaultBranchImportTests.cs`.
- [X] T009 [US1] Implement the typed default-branch request and `ImportDefaultBranchAsync` through the existing importer/state path in `src/ProjectManagementCompiler/Application/ManifestImport/ManifestDefaultBranchImportRequest.cs` and `src/ProjectManagementCompiler/Application/ManifestImport/ManifestImportApplicationService.cs`; rerun T008.
- [X] T010 [US1] Add a failing API-level test for `POST /api/manifest-import/default-branch`, request shape/validation, exact imported identity, and safe recovery errors in `tests/ProjectManagementCompiler.Tests/ManifestDefaultBranchImportTests.cs`.
- [X] T011 [US1] Register the resolver and expose the POST route without accepting caller mode or commit in `src/ProjectManagementCompiler/Program.cs`; rerun T010 and T008.
- [X] T012 [US1] Add failing browser-contract tests for explicit-only source reads, only the approved preference keys, no startup import, local-copy freshness wording, success/candidate/error states, and exact-source recovery in `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`.
- [X] T013 [US1] Implement the unloaded/local-source form and browser behavior in `src/ProjectManagementCompiler/wwwroot/index.html` and `src/ProjectManagementCompiler/wwwroot/app.js`; persist only repository-root and manifest-path preferences and rerun T012 plus T001.

**Checkpoint**: The new local-default action resolves and imports one exact commit in one request. Existing exact-commit and preview flows remain available and retain their authority rules.

## Phase 4: User Story 2 — Understand the loaded project at a glance (Priority: P1)

**Goal**: A concise read-only Overview shows only source-backed facts and truthful progress coverage.

- [X] T014 [US2] Add failing projector tests for official reporting-date phase selection, unknown/ambiguous phase, next incomplete future milestone/decision point, source-backed/unknown state, and no-candidate behavior in `tests/ProjectManagementCompiler.Tests/ProjectOverviewProjectionTests.cs`.
- [X] T015 [US2] Define `ProjectOverviewModel` and implement the date-based core projection using canonical facts and `ProgressCoverageProjector` in `src/ProjectManagementCompiler/Management/ProjectOverviewModel.cs` and `src/ProjectManagementCompiler/Management/ProjectOverviewProjector.cs`; rerun T014 and relevant T006 tests.
- [X] T016 [US2] Add failing alert/name tests for deterministic top-three ordering, supported concise Vietnamese consequences, `ReaderFacingTextPolicy.CleanName`, and omission of raw/unsupported diagnostic text in `tests/ProjectManagementCompiler.Tests/ProjectOverviewProjectionTests.cs`.
- [X] T017 [US2] Implement only supported alert mappings, readable names, stable ordering, and the three-item cap in `src/ProjectManagementCompiler/Management/ProjectOverviewProjector.cs`; rerun T016.
- [X] T018 [US2] Add a failing route/view-set contract test for Overview in aggregate views, `GET /api/views/overview`, and `404 NO_PROJECT` in `tests/ProjectManagementCompiler.Tests/ProjectOverviewProjectionTests.cs`.
- [X] T019 [US2] Add Overview to `ManagementViewSet`, populate it in `ManagementViewProjector`, and add the named view route in `src/ProjectManagementCompiler/Management/ManagementViewProjector.cs` and `src/ProjectManagementCompiler/Program.cs`; rerun T018 and all Overview projector tests.
- [X] T020 [US2] Add failing browser-contract tests for the concise Overview, post-import destination, separate completion count, partial coverage label, evidence-backed empty states, and direct Gantt access in `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`.
- [X] T021 [US2] Render the Overview and route successful official imports to it in `src/ProjectManagementCompiler/wwwroot/index.html` and `src/ProjectManagementCompiler/wwwroot/app.js`; rerun T020 and all prior shell tests.

**Checkpoint**: Every shown value comes from the active official projection. A project-wide percentage appears only with complete valid effort coverage.

## Phase 5: User Story 3 — Reach detailed tools without losing the simple workspace (Priority: P1)

**Goal**: Overview and Gantt are primary while all current capabilities remain reachable under Advanced.

- [X] T022 [US3] Add failing navigation contract tests for Overview/Gantt as primary, every existing WBS/Kanban/specialist destination and action under Advanced, no dead Work placeholder, keyboard reachability, and preserved Gantt affordances in `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`.
- [X] T023 [US3] Implement the loaded/unloaded navigation grouping and event behavior in `src/ProjectManagementCompiler/wwwroot/index.html` and `src/ProjectManagementCompiler/wwwroot/app.js`; rerun T022 and T001.
- [X] T024 [US3] Add failing responsive/accessibility assertions for focus visibility, narrow/desktop primary controls, and Gantt's contained timeline scrolling in `tests/ProjectManagementCompiler.Tests/ManagementUiShellTests.cs`.
- [X] T025 [US3] Implement the restrained responsive workspace hierarchy and explicit interactive-state styling in `src/ProjectManagementCompiler/wwwroot/styles.css` without changing Gantt schedule/dependency semantics; rerun T024 and all shell tests.

## Phase 6: Verification and handoff

**Purpose**: Verify the approved increment and public-repository safety before proposing publication.

- [X] T026 Run the focused test executable, `scripts/test.ps1`, and `scripts/verify.ps1`; record pass/fail/blocker evidence without counting blocked checks as passed. `scripts/verify.ps1` passed on loopback port 5052 with the local IDEAEngineering root: build 0 warnings/errors, test runner PASS, launcher PASS, and web integration/security checks PASS.
- [X] T027 Perform the quickstart browser checks at 360px and 1280px, including unloaded state, successful official import, missing/default-ref and invalid-manifest recovery, sparse effort coverage, keyboard navigation, and Gantt preservation. Browser QA confirmed 8/53 effort coverage is labeled partial, failed imports retain the official snapshot, valid retry succeeds, keyboard activation opens Gantt, and mobile timeline scrolling stays inside the page while task details remain in-viewport.
- [X] T028 Review `git diff --check`, `git status`, changed fixture/document content, public-repository privacy, and generated/temp files; commit only verified cohesive increments on `codex/project-ui-redesign`. Do not push or merge without the applicable approval gate. Review found no private IDEA fixture, credentials, secret patterns, or generated/temp files in the repository; the working tree is clean after four cohesive commits.

## Dependencies and execution order

- Work is strictly sequential; no subagents and no parallel edits to shared frontend files.
- T001 captures behavior before shell changes.
- T002–T005 establish and harden the default-ref boundary; T006–T007 establish one shared effort rule.
- Story 1 proceeds as test/implementation pairs before Story 2 uses its official snapshot path.
- Story 2 proceeds as projection, route, then visible UI slices; each implementation follows its failing test.
- Story 3 reorganizes the existing shell only after the new loaded Overview exists.
- Full verification and any publication decision follow implementation. The remote is not used as a test target and no push is authorized by this task list.
