# Tasks: Executive Daily Gantt Report

**Input**: Design documents from
`specs/006-executive-daily-gantt-report/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/executive-daily-gantt-workbook.md`,
`contracts/executive-progress-export.md`, and `quickstart.md`

**Tests**: Required. The project constitution and approved workflow require a
red-green-refactor cycle at the projection, workbook, application, and
compatibility seams. Every task marked **RED** must be run and observed failing
for the intended reason before its paired production task starts.

**Organization**: Tasks are grouped by user story. Each story has an explicit
independent test and produces a reviewable increment. Requirement identifiers
in task descriptions provide direct traceability for Spec Kit analysis.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel after the stated prerequisite because it uses
  different files and does not depend on another incomplete task.
- **[Story]**: Maps the task to the corresponding user story in `spec.md`.
- All paths are repository-relative and exact.

---

## Phase 1: Setup and Preservation Baseline

**Purpose**: Establish a trustworthy green baseline and lock the two existing
export contracts before refactoring shared workbook code.

- [X] T001 Run `scripts/build.ps1` and `scripts/test.ps1`, inspect `git status --short`, and record the exact warning/error, PASS/FAIL, branch, and pre-existing untracked-file results in `specs/006-executive-daily-gantt-report/tasks.md` before any production edit. Evidence: 2026-09-21 managed-worktree baseline on `codex/feature006-exec` built with 0 warnings and 0 errors; with `IDEAENGINEERING_ROOT` set to the approved local reference checkout, the test log recorded 278 PASS and 0 FAIL. The managed worktree was clean; the user's original-workspace `outputs/`, `package.json`, and `package-lock.json` remain preserved and unstaged there.
- [X] T002 [P] Run and record the current four-sheet management export's names/order and active sheet as the intentionally superseded baseline in `specs/006-executive-daily-gantt-report/tasks.md`, while retaining green preservation assertions for deterministic ZIP/XML packaging, the dated endpoint filename, and the official-only action in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`, `tests/ProjectManagementCompiler.Tests/ExecutiveProgressExportApplicationTests.cs`, and `tests/ProjectManagementCompiler.Tests/ExecutiveProgressUiTests.cs` (FR-001, FR-042). Evidence: the pre-006 management workbook contract is exactly `Tổng quan` → `Lịch trình` → `Vấn đề cần xử lý` → `Chi tiết công việc`, with `activeTab="0"` (`Tổng quan`). This order is intentionally superseded by the approved five-sheet workbook in T018; the existing reader-facing authority, endpoint, dated filename, and presentation-only tests remain green preservation constraints until their successor assertions are introduced.
- [X] T003 [P] Characterize the current CARIO-only and CARIO + Gantt sheet contracts, preview markers, import acceptance/rejection, semantic digest, and official/proposal/preview isolation as green assertions in `tests/ProjectManagementCompiler.Tests/CarioXlsxTests.cs` and `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs` (FR-041, SC-009). Evidence: `ExistingTechnicalExportContractIsCharacterizedBeforeExecutiveExport` preserves the seven-sheet `01_TASKS` through `07_GANTT` CARIO + Gantt contract, `PMC_EXPORT_KIND`, semantic digest, and proposal count; `ExistingTechnicalPreviewContractIsCharacterizedBeforeExecutiveExport` preserves accepted preview identity and marker semantics; malformed package/marker/header/relationship cases fail closed. These existing executable assertions remain unchanged and green. Phase-1 regression on 2026-09-21 with process-scoped `IDEAENGINEERING_ROOT`: 278 PASS, 0 FAIL, 0 error lines.

**Checkpoint**: The pre-feature build is green, existing management behavior is
recorded, and the technical export/import boundary is protected by executable
tests.

---

## Phase 2: Foundational Workbook Boundary

**Purpose**: Separate workbook composition from Open XML serialization before
adding daily-Gantt behavior to the already-large exporter.

**Critical**: No user-story implementation begins until this refactor is green
and behavior-preserving.

- [X] T004 Add a **RED** deep-interface test for an ordered neutral workbook document, semantic cell styles, merged ranges, panes, widths, zoom, gridlines, and print settings in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`; observe failure because the document/composer seam does not exist (FR-036, FR-037, FR-038, FR-042). Evidence: RED on 2026-09-21: 278 PASS, 1 FAIL, `ExecutiveProgressWorkbookComposer must exist so composition stays separate from Open XML serialization.` The direct seam test is green after T005–T007.
- [X] T005 Implement immutable `ExecutiveWorkbookDocument`, worksheet, row, cell, range, pane, print-mode, number-format, and semantic-style value types with the exact invariants from `data-model.md` in `src/ProjectManagementCompiler/Outputs/ExecutiveWorkbookDocument.cs` (FR-035, FR-036, FR-037, FR-038, FR-042). Evidence: the output-only immutable model validates sheet identity/order, active index, positive finite widths, non-overlapping in-range merges, pane bounds, 100% zoom, print settings, explicit cell type/format, and semantic style tokens.
- [X] T006 Extract `ExecutiveProgressWorkbookComposer.Build(ExecutiveProgressReport)` and move the current reader-facing worksheet construction into `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs` without changing the preserved pre-feature workbook behavior. Evidence: the composer retains the exact Feature-005 four-sheet order and reader-facing content while making rows, semantic styles, panes, widths, and print settings independently inspectable.
- [X] T007 Refactor `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs` to serialize `ExecutiveWorkbookDocument` deterministically with BCL-only Open XML package APIs, preserving package ordering and controlled ZIP timestamps and making T004 green (FR-042). Evidence: `Export(ExecutiveProgressReport)` now delegates to the composer; the serializer owns only package parts, relationships, style mapping, typed values, cell addresses, merges, panes, and fixed ZIP metadata. `ExecutiveWorkbookPackageRemainsDeterministicForTheSameReport` is green.
- [X] T008 Run the focused executive workbook tests plus all management, CARIO, preview-import, and application regressions from `tests/ProjectManagementCompiler.Tests/Program.cs`, then record exact results in `specs/006-executive-daily-gantt-report/tasks.md`; stop if the refactor changes any characterized behavior. Evidence: 2026-09-21 `scripts/build.ps1` succeeded with 0 warnings and 0 errors; full `scripts/test.ps1` with process-scoped `IDEAENGINEERING_ROOT` exited 0 with 280 PASS, 0 FAIL, and 0 error lines. No characterized management, CARIO, preview-import, or application regression changed.

**Checkpoint**: Projection selects facts, the composer selects presentation,
and the exporter only serializes a neutral deterministic workbook document.

---

## Phase 3: User Story 1 - See Plan and Actual Together (Priority: P1) MVP

**Goal**: Produce truthful daily overview and detailed Gantt projections where
the immutable Plan and evidence-backed Actual are adjacent, date-based, and
accompanied by percentage and coverage semantics.

**Independent Test**: Build a synthetic official snapshot containing completed,
in-progress, unrecorded, forecast, partial-coverage, and overdue cards. Project
and export it directly; verify Plan/Actual intervals, percentage eligibility,
coverage, daily axes, milestone symbols, and missing-data labels without using
the HTTP endpoint.

### Tests and implementation for User Story 1

- [x] T009 [US1] Extend `tests/ProjectManagementCompiler.Tests/ExecutiveProgressTestFixtures.cs` with deterministic official fixtures for completed early/late, open in-progress, in-progress-without-start, explicit not-started, unrecorded, suspended/cancelled, official forecast, valid/one-sided/zero/missing effort, partial coverage, reporting dates before/after baseline, out-of-baseline Actual, one-day work, milestone, long Vietnamese/markdown titles, repeated raw IDs across kinds, and separately identified contradictory/negative-effort rejection cases.
- [X] T010 [US1] Add and register **RED** direct-card projection tests in `tests/ProjectManagementCompiler.Tests/ExecutiveDailyGanttProjectionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs` for actual-start/finish rules, analysis-date display-through, no invented Actual, official-only forecast, the valid effort formula with midpoint-away-from-zero rounding, `Chưa cập nhật`, and explicit insufficient data for missing or zero-sum evidence (FR-015–FR-020, FR-022–FR-024, FR-040; SC-003, SC-004).
- [X] T011 [US1] Add **RED** hierarchy and roll-up projection tests in `tests/ProjectManagementCompiler.Tests/ExecutiveDailyGanttProjectionTests.cs` for the exact row-kind set, hierarchy levels, stable source order, orphan retention, full attributable axis, overview selection, zero-duration milestones, child counts, conservative Actual/forecast roll-ups, and percentage suppression unless `ProgressEligibleChildCount == TotalChildCount` and total is greater than zero (FR-009, FR-011–FR-014, FR-021, FR-025, FR-026; SC-003, SC-004).
- [X] T012 [P] [US1] Add **RED** workbook assertions in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs` for one calendar date per column, grouped month/date/Vietnamese-weekday headers, subdued weekends, labelled report-date border, adjacent `Kế hoạch`/`Thực tế` rows with identity shown once, blue Plan, green Actual, amber official forecast, red blocked/overdue text plus cue, gray unknown, milestone symbol, fixed management columns, panes, and readable landscape pagination (FR-009, FR-012–FR-022, FR-027, FR-035–FR-038; SC-003, SC-006).
- [X] T013 [US1] Implement `ExecutiveDailyGanttRowKind`, `ExecutiveDailyGanttRow`, and `ExecutiveDailyGanttProjection` in `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttModel.cs` with exactly the fields, nullable boundaries, hierarchy levels 0–4, non-null collections, integer percentage 0–100, child counts, labels, flags, and source-order constraints defined in `data-model.md`.
- [X] T014 [US1] Implement `ExecutiveDailyGanttProjector.Build(CompilationResult)` in `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttProjector.cs`, resolving card truth through `ExecutionTruthResolver.ForCard`, deriving direct/roll-up intervals and coverage conservatively, constructing the complete hierarchy and overview rows, and failing with a structured diagnostic when no truthful full axis exists; make T010 and T011 green (FR-004, FR-009, FR-011, FR-015–FR-026, FR-039, FR-040).
- [X] T015 [US1] Add required `DailyGantt`, recording/eligibility counts, and latest-update fields to `src/ProjectManagementCompiler/Management/ExecutiveProgressReportModel.cs`, then compose them from the daily projector without retaining weekly workbook semantics in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs` (FR-008, FR-009, FR-023–FR-026).
- [X] T016 [US1] Compose and serialize the overview and full daily-Gantt regions with paired lanes, fixed columns, daily headers, semantic text/styles, merges, panes, and print behavior in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs` and `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs`; make T012 green without using shapes, macros, Office automation, or percentage-scaled bars (FR-009, FR-012–FR-022, FR-027, FR-035–FR-038, FR-042).
- [X] T017 [US1] Run all projection/workbook tests plus related execution-truth, analysis, metrics, calendar, and management-projection tests registered in `tests/ProjectManagementCompiler.Tests/Program.cs`; record the RED-to-GREEN evidence and the independent US1 fixture result in `specs/006-executive-daily-gantt-report/tasks.md` (SC-003, SC-004, SC-006).

**Checkpoint**: User Story 1 is independently demonstrable from an official
fixture. Every visible green interval and percentage reconciles to evidence;
missing evidence remains explicit.

---

## Phase 4: User Story 2 - One Complete Management Workbook (Priority: P2)

**Goal**: Upgrade the one existing management export to exactly five coherent
sheets with an executive opening view, complete hierarchy, issues, and one-row-
per-card traceability.

**Independent Test**: Export one official fixture and prove that the workbook
contains exactly the approved five sheets in order, opens on `Tổng quan`, and
lets a reviewer trace every headline/issue to a delivery-card row without a
second file.

### Tests and implementation for User Story 2

- [X] T018 [US2] Add **RED** package tests in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs` for exactly `Tổng quan`, `Gantt theo ngày`, `30 ngày tới`, `Vấn đề cần xử lý`, and `Chi tiết công việc` in that order; active sheet zero; hidden gridlines; 100% zoom; executive source/planning context; four approved summary blocks; at most five overview attention items before the Gantt; and explicit empty states for no attention or no next milestone (FR-002, FR-003, FR-008–FR-010; SC-001, SC-002).
- [X] T019 [US2] Add **RED** workbook-content tests in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs` for complete Project → Phase → Work package → Delivery card → Milestone hierarchy, every canonical delivery card exactly once even when raw IDs repeat across kinds, distinct milestone identification, the complete four-column actionable issue list, the exact 16-column delivery-card detail contract, mechanically cleaned but untruncated Vietnamese/markdown text, traceability, no clipped primary text settings, and forbidden technical/proposal content on the first four sheets (FR-011, FR-021, FR-032–FR-038; SC-006, SC-010).
- [X] T020 [US2] Extend `ExecutiveProgressSummary` and `ExecutiveDeliveryCardDetail` with the exact count, Actual/forecast date, effort, percentage, recording-label, latest-update, and final short-reference fields from `data-model.md`, and populate only direct official card values in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportModel.cs` and `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs` (FR-008, FR-023–FR-025, FR-033, FR-034, FR-040).
- [X] T021 [US2] Replace the temporary four-sheet composition with the exact five-sheet contract, executive summary/Gantt/attention regions, complete hierarchy, full action table, and one stable detail row per card in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`; preserve long Vietnamese text and all explicit unknowns (FR-002, FR-003, FR-008–FR-011, FR-032–FR-036).
- [X] T022 [US2] Serialize active-sheet metadata, month/task-band merges, typed dates/numbers/percentages, standard-font styles, widths, wrapping, freeze panes, filters where deterministic, and approved landscape/horizontal pagination in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs`; make T018 and T019 green while keeping deterministic package content (FR-003, FR-013, FR-014, FR-034–FR-038).
- [X] T023 [US2] Run focused five-sheet workbook tests, inspect generated XML for sheet order/content boundaries, and perform the US2 traceability walk on the official fixture; record exact results in `specs/006-executive-daily-gantt-report/tasks.md` (SC-001, SC-002, SC-006, SC-010).

**Checkpoint**: The upgraded management export is a single five-sheet file;
the former four-sheet presentation contract no longer leaks into production.

---

## Phase 5: User Story 3 - Focus on the Next 30 Days (Priority: P3)

**Goal**: Provide an exact 30-date operating view that retains overdue open
work, includes planned intervals intersecting the window, and orders the most
urgent work first.

**Independent Test**: Project a fixture with work before, crossing, inside, and
after the boundary. Verify an inclusive source-date through +29-day axis,
membership, deterministic order, shared Plan/Actual semantics, and the empty
state.

### Tests and implementation for User Story 3

- [X] T024 [US3] Add and register **RED** near-term projection tests in `tests/ProjectManagementCompiler.Tests/ExecutiveDailyGanttProjectionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs` for exact start/+29 finish, unfinished-overdue inclusion, interval intersection, exclusion of completed/non-overdue outside work, missing-boundary behavior, in-window milestones, and order by overdue group → known nearest planned finish → stable source order (FR-028–FR-030; SC-005).
- [X] T025 [P] [US3] Add **RED** workbook tests in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs` for exactly 30 daily columns, qualifying-work summary counts, next in-window milestone or explicit missing state, paired Plan/Actual/forecast/missing semantics identical to the full Gantt, truthful left/right continuation symbols for intervals clipped by the window, an explicit dated overdue marker instead of a fabricated in-window Plan bar when Plan lies wholly before the axis, frozen context, legible landscape printing, and `Không có công việc quá hạn hoặc giao với cửa sổ 30 ngày.` when empty (FR-028–FR-031, FR-035–FR-038; SC-005, SC-006).
- [X] T026 [US3] Implement exact near-term membership, milestone inclusion, deterministic ordering, and summary counts in `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttProjector.cs`, keeping `NearTermStart = SourceReportingDate` and `NearTermFinish = SourceReportingDate + 29 days`; make T024 green (FR-028–FR-030).
- [X] T027 [US3] Compose and serialize `30 ngày tới` with the exact 30-date axis and the same fixed columns, lanes, labels, styles, panes, and bounded print rules as the full Gantt in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs` and `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs`; make T025 green (FR-028–FR-031, FR-035–FR-038).
- [X] T028 [US3] Run the near-term boundary matrix, workbook assertions, and independent empty/non-empty fixture checks through `tests/ProjectManagementCompiler.Tests/Program.cs`, then record exact results in `specs/006-executive-daily-gantt-report/tasks.md` (SC-005, SC-006).

**Checkpoint**: The near-term sheet always represents exactly 30 dates and
never hides qualifying overdue work.

---

## Phase 6: User Story 4 - Preserve Authority and Existing Contracts (Priority: P4)

**Goal**: Keep the richer report official-only, read-only, deterministic, non-
importable, and isolated from the technical CARIO + Gantt workbook.

**Independent Test**: Export from official, proposal, and preview states; test
sparse and contradictory Actual; compare application/source state before and
after; and rerun the characterized technical export/import suite.

### Tests and implementation for User Story 4

- [X] T029 [P] [US4] Add **RED** application tests in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressExportApplicationTests.cs` for `CurrentOfficialResult` selection, no preview/proposal fallback, sparse-valid success, stable diagnostics for incomplete or contradictory official Actual, unchanged compiler signature/route/date-based filename/content type, semantic and application-state immutability, deterministic bytes, and the five-second accepted-fixture budget (FR-001, FR-004–FR-007, FR-039, FR-042; SC-007, SC-008).
- [X] T030 [P] [US4] Add compatibility regressions proving the five-sheet management workbook has no preview markers and is rejected without changing the last valid preview, while the characterized CARIO-only/CARIO + Gantt packages, preview importer, and separate accessible UI actions remain unchanged in `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs`, `tests/ProjectManagementCompiler.Tests/CarioXlsxTests.cs`, and `tests/ProjectManagementCompiler.Tests/ExecutiveProgressUiTests.cs` (FR-001, FR-006, FR-041; SC-009).
- [X] T031 [US4] Add fail-closed validation for no truthful axis, actual finish before start, negative effort, and other unrepresentable official contradictions with stable executive-export diagnostics in `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttProjector.cs` and `src/ProjectManagementCompiler/Application/ProjectCompiler.cs`; keep sparse non-contradictory evidence exportable (FR-004, FR-039, FR-040).
- [X] T032 [US4] Wire the upgraded report behind the unchanged `IProjectCompiler.ExportExecutiveProgressXlsx(CompilationResult)` seam and existing official-only download in `src/ProjectManagementCompiler/Application/IProjectCompiler.cs`, `src/ProjectManagementCompiler/Application/ProjectCompiler.cs`, and `src/ProjectManagementCompiler/Program.cs`; retain the existing `Xuất báo cáo tiến độ` binding and separate technical action in `src/ProjectManagementCompiler/wwwroot/app.js` and make T029/T030 green (FR-001, FR-004–FR-007, FR-041).
- [X] T033 [US4] Extend `scripts/verify-web.ps1` to import an official fixture, download and validate the five management sheets and dated filename, reject preview-only authority, download/preview the unchanged technical workbook, assert management non-importability, and compare digest/proposal/preview state before and after (FR-001, FR-005–FR-007, FR-041; SC-008, SC-009).
- [X] T034 [US4] Run all executive, application, UI, CARIO, preview-import, proposal, canonical serialization, manifest-import, and web-smoke regressions; record exact correctness, deterministic-package, elapsed-time, and state-immutability results in `specs/006-executive-daily-gantt-report/tasks.md` (SC-007, SC-008, SC-009).

**Checkpoint**: Visual reporting is richer, but authority, importability, state,
and technical workbook behavior are unchanged.

---

## Phase 7: Polish and Cross-Cutting Verification

**Purpose**: Document the final user flow, validate the real IDEAEngineering
snapshot, visually inspect the artifact, and complete every release gate.

- [X] T035 [P] Update the one-report user flow, exact five sheets, daily Plan/Actual meanings, effort percentage versus date lanes, sparse-data wording, official-only authority, and technical-export separation in `docs/runbook/mvp1-local.md` (FR-001, FR-006, FR-034, FR-035).
- [X] T036 Run the real IDEAEngineering flow from `quickstart.md`, capture source-repository HEAD/status/diffs before and after, generate the management workbook, and record the observed 53 total / 1 recorded / 52 unrecorded acceptance data plus zero invented percentage/Actual lane in `specs/006-executive-daily-gantt-report/tasks.md`; do not stage generated files under `outputs/` (FR-004–FR-007, FR-039, FR-040).
- [ ] T037 Open or render every sheet of the generated workbook at 100% zoom, time a management reader identifying current phase/next milestone/progress/coverage/top decision within 60 seconds, verify headings, unclipped Vietnamese text, fixed context, Plan/Actual/forecast/overdue/weekend/milestone/report-date cues, traceability, and print behavior, then record pass/fail evidence and any artifact path in `specs/006-executive-daily-gantt-report/tasks.md` (FR-034–FR-038; SC-001, SC-006, SC-010).
- [X] T038 Run `scripts/verify.ps1` and `git diff --check`, inspect `git status --short`, staged content, generated junk, credentials/secrets/private configuration, and public-repository fixture safety, then record exact final results in `specs/006-executive-daily-gantt-report/tasks.md` (FR-041, FR-042; SC-007, SC-008, SC-009).
- [X] T039 Run the post-implementation `$speckit-converge` and branch `$code-review` gates against `specs/006-executive-daily-gantt-report/`, resolve approved findings through fresh RED tests, and record the final zero-blocker evidence and reviewed commit SHA in `specs/006-executive-daily-gantt-report/tasks.md` before merge or push.

### Completion evidence — 2026-09-21

- T010–T017 / T024–T028: focused daily-Gantt and workbook seams are registered in the self-hosted test runner. The final full run reported 295 `PASS` and 0 `FAIL`; it covers direct Actual/forecast/effort truth, conservative hierarchy roll-ups, the exact 30-day boundary, sparse/empty states, typed cells, paired lanes, continuation markers, and the readable horizontal overdue marker.
- T018–T023: package and XML assertions confirm the exact five-sheet order, `Tổng quan` active tab, hidden gridlines, 100% zoom, typed values, hierarchy/detail traceability, and presentation-only boundaries. The generated report was rendered on all five sheets; the pre-window overdue label was corrected to the horizontally merged `Quá hạn · dd/MM` cue rather than vertical text in a single date cell.
- T029–T034: `scripts/verify.ps1` passed after the final code change: build 0 warnings / 0 errors, 295 `PASS` / 0 `FAIL`, `LauncherContract` PASS, and web verification reported `Cards: 53`, `ExecutiveSheetCount: 5`, and `SecurityChecks: PASS`. The web gate also verifies official-only export, dated filename, preview rejection, technical-workbook preservation, and state immutability.
- T035–T036: real source run used only approved commit `0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4`. The source checkout remained unchanged before/after (`HEAD cf0118a300822c05812b134c3ea5edfc9feb0271`, clean status and empty staged/unstaged diffs). The exported executive workbook contained 53 delivery cards, 1 recorded card, and 52 `Chưa cập nhật` cards; unknown execution remained explicit and no Actual lane or percentage was invented. The generated workbook was kept outside the repository.
- T039: `$speckit-converge` found no code gap against the 42 FRs, 10 SCs, four user stories, plan decisions, and six constitution principles; no convergence tasks were appended. Manual two-axis branch review against `5651fd6` found 0 standards blockers and 0 spec blockers. Reviewed implementation commit: `d9ae1f7` (`feat(report): export executive daily gantt workbook`).
- T038: final repository inspection found only this Spec Kit evidence file modified, no staged files, no untracked generated artifacts, no whitespace errors from `git diff --check`, and 0 matches for the credential/private-key screening pattern in the pending diff. The public fixture policy remains unchanged; no source repository content or generated workbook is staged.
- T037 remains an explicit human acceptance gate. Automated render/layout checks cover all five sheets, but the 60-second management-reader comprehension check must be performed by a designated business reader rather than claimed by the implementation agent.

---

## Dependencies and Execution Order

### Phase dependencies

- Phase 1 has no dependencies.
- Phase 2 depends on the Phase 1 baseline and blocks all user stories.
- User Story 1 depends on Phase 2 and is the MVP increment.
- User Story 2 depends on the shared workbook/projection behavior delivered by
  User Story 1, but has its own five-sheet and traceability acceptance test.
- User Story 3 depends on the daily row/lane semantics from User Story 1, but
  has its own population, ordering, axis, and empty-state acceptance test.
- User Story 4 may start its RED compatibility tests after Phase 2; final GREEN
  verification waits for User Stories 1–3 so the complete package is tested.
- Phase 7 depends on all selected stories being GREEN.

### Within each story

- Create/extend fixtures before tests that consume them.
- Run every **RED** test and confirm the intended failure before production
  implementation.
- Implement value models before semantic projectors, projectors before workbook
  composition, and composition before package serialization/integration.
- Run focused tests first, then related regressions, then full verification.
- Commit cohesive GREEN increments with messages describing the actual change;
  do not combine the entire feature into one final commit.

### Parallel opportunities

- T002 and T003 can run in parallel after T001.
- After T009, T012 can be authored in parallel with T010/T011 because it uses a
  different test file; production work still waits for all intended RED results.
- T024 and T025 can be authored in parallel after User Story 1.
- T029 and T030 can be authored in parallel because they exercise different
  application and compatibility test files.
- T035 can proceed in parallel with final test execution once behavior and
  wording are stable.

---

## Parallel Examples

### User Story 1

```text
After T009:
- Author T010/T011 in ExecutiveDailyGanttProjectionTests.cs.
- In parallel, author T012 in ExecutiveProgressXlsxTests.cs.
- Do not start T013–T016 until both RED seams fail for the expected reason.
```

### User Story 2

```text
- Keep T018 and T019 sequential because both edit ExecutiveProgressXlsxTests.cs.
- After T020, composer work in T021 precedes serializer work in T022.
```

### User Story 3

```text
After User Story 1 is GREEN:
- Author T024 in ExecutiveDailyGanttProjectionTests.cs.
- In parallel, author T025 in ExecutiveProgressXlsxTests.cs.
- Implement T026 and then T027 after both RED results are captured.
```

### User Story 4

```text
After Phase 2:
- Author T029 in ExecutiveProgressExportApplicationTests.cs.
- In parallel, author T030 across compatibility/UI test files.
- Run the complete GREEN gate only after T031–T033 and User Stories 1–3.
```

Parallel markers describe safe file-level concurrency. The default execution
mode for this repository is one native implementation agent working through
the ordered batches; no subagent fan-out is required.

---

## Requirement Coverage Map

| Requirement | Primary task coverage |
| --- | --- |
| FR-001 | T002, T029, T030, T032, T033, T035 |
| FR-002 | T018, T021 |
| FR-003 | T018, T021, T022 |
| FR-004 | T014, T029, T031, T032, T036 |
| FR-005 | T029, T032, T033, T036 |
| FR-006 | T029, T030, T032, T035 |
| FR-007 | T029, T033, T036 |
| FR-008 | T015, T018, T020, T021 |
| FR-009 | T011, T012, T015, T016, T021 |
| FR-010 | T018, T021 |
| FR-011 | T011, T014, T019, T021 |
| FR-012 | T012, T016 |
| FR-013 | T012, T016, T022 |
| FR-014 | T011, T012, T016, T022 |
| FR-015 | T010, T014, T016 |
| FR-016 | T010, T014, T016 |
| FR-017 | T010, T014, T016 |
| FR-018 | T010, T014, T016 |
| FR-019 | T010, T014, T016 |
| FR-020 | T010, T012, T014, T016 |
| FR-021 | T011, T012, T016, T019 |
| FR-022 | T010, T012, T016 |
| FR-023 | T010, T014, T015, T020 |
| FR-024 | T010, T014, T015, T020 |
| FR-025 | T011, T014, T015, T020 |
| FR-026 | T011, T014, T015 |
| FR-027 | T012, T016 |
| FR-028 | T024, T025, T026, T027 |
| FR-029 | T024, T026, T027 |
| FR-030 | T024, T026 |
| FR-031 | T025, T027, T028 |
| FR-032 | T019, T021 |
| FR-033 | T019, T020, T021 |
| FR-034 | T019, T020, T021, T022, T035, T037 |
| FR-035 | T005, T012, T016, T019, T022, T025, T027, T035, T037 |
| FR-036 | T004, T005, T012, T016, T019, T022, T037 |
| FR-037 | T004, T005, T012, T016, T019, T022, T037 |
| FR-038 | T004, T005, T012, T016, T019, T022, T025, T027, T037 |
| FR-039 | T014, T029, T031, T036 |
| FR-040 | T010, T014, T020, T031, T036 |
| FR-041 | T003, T030, T032, T033, T034, T038 |
| FR-042 | T002, T004, T005, T007, T016, T029, T038 |
| SC-001 | T018, T021, T023, T037 |
| SC-002 | T018, T021, T022, T023 |
| SC-003 | T009, T010, T011, T012, T013, T014, T015, T016, T017, T036 |
| SC-004 | T010, T011, T014, T015, T017 |
| SC-005 | T024, T025, T026, T027, T028 |
| SC-006 | T012, T017, T018, T019, T022, T023, T025, T027, T028, T037 |
| SC-007 | T029, T034, T038 |
| SC-008 | T029, T033, T034, T036, T038 |
| SC-009 | T003, T030, T033, T034, T038 |
| SC-010 | T019, T021, T023, T037 |

All 42 functional requirements and all 10 measurable success criteria have at
least one implementation or verification task. The mapping is intentionally
many-to-many for authority, accessibility, and compatibility boundaries.

---

## Implementation Strategy

### MVP first

1. Complete Phase 1 and Phase 2.
2. Complete User Story 1 through T017.
3. Stop and validate the synthetic official snapshot independently.
4. Demo truthful Plan/Actual daily lanes and progress coverage before adding
   workbook breadth.

### Incremental delivery

1. Foundation: behavior-preserving workbook boundary.
2. US1: truthful daily Plan/Actual Gantt.
3. US2: complete five-sheet management package.
4. US3: exact 30-day operating view.
5. US4: authority, compatibility, immutability, and performance hardening.
6. Real-source, visual, verification, convergence, and review gates.

### Commit discipline

- Commit the behavior-preserving workbook refactor separately.
- Commit each user story only after its focused and related tests are GREEN.
- Keep test and paired implementation together when that preserves a coherent
  repository state.
- Never stage `outputs/`, `package.json`, `package-lock.json`, secrets, private
  configuration, or proprietary source material unless a later explicit scope
  decision authorizes a specific safe artifact.

## Notes

- `[P]` means safe file-level concurrency, not permission to skip dependency or
  RED-test gates.
- The management workbook is presentation-only; it never becomes canonical
  input, execution evidence, proposal state, or a technical preview.
- Gantt lane length always represents dates. Effort completion remains a
  separate numeric field.
- Sparse valid evidence exports with explicit unknowns; contradictory evidence
  fails closed with a structured diagnostic.
- `CarioXlsxExporter.cs` is regression-only for this feature and must not be
  modified unless a separately approved contradiction is discovered.
