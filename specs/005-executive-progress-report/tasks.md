# Tasks: Executive Progress Report Export

**Input**: Design documents from `specs/005-executive-progress-report/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/executive-progress-workbook.md`, and
`contracts/executive-progress-http-ui.md`

**Tests**: Required. The project constitution requires red-green-refactor, and
the approved workflow requires a failing regression before every behavior
group, followed by focused and related regression runs.

**Organization**: Tasks are grouped by user story. Tasks marked **RED** must be
run and observed failing for the intended reason before their paired production
task starts. Preservation/characterization checks are expected to remain green.

## Phase 1: Setup (Shared Test Infrastructure)

**Purpose**: Record the clean starting point and provide dependency-free XLSX
fixture inspection without changing application behavior.

- [X] T001 Run `scripts/build.ps1` and `scripts/test.ps1` before feature code, then record the exact build warning/error count and test pass/fail count in `specs/005-executive-progress-report/tasks.md`. Baseline evidence: `scripts/build.ps1` completed with 0 warnings and 0 errors; `scripts/test.ps1` completed with 252 PASS and 0 FAIL when `IDEAENGINEERING_ROOT=D:\Work\Projects\IDEAEngineering` was supplied.
- [X] T002 [P] Add official/candidate compilation fixtures, executive projection builders, ZIP/XML part readers, cell-text lookup, and package assertions using only BCL APIs in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressTestFixtures.cs`. Evidence: helper fixture/ZIP/XML assertions added; full runner completed with 254 PASS and 0 FAIL.
- [X] T003 [P] Capture the existing seven-sheet names, CARIO marker rows, required headers, preview acceptance, semantic digest, and proposal-state behavior as green characterization assertions in `tests/ProjectManagementCompiler.Tests/CarioXlsxTests.cs` and `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs`. Evidence: both characterization tests pass; full runner completed with 254 PASS and 0 FAIL.

## Phase 2: Foundational Spec Kit Gate

**Purpose**: Prove that approved artifacts are internally consistent before
production implementation.

- [X] T004 Run the strictly read-only `$speckit-analyze` against `specs/005-executive-progress-report/spec.md`, `plan.md`, and `tasks.md`; report every finding and, if remediation is needed, obtain explicit user approval before a separate edit in `specs/005-executive-progress-report/`; leave `src/` untouched. Final round completed with 0 actionable findings, after four analysis rounds and approved artifact-only remediation.

**Checkpoint**: Baseline is green and the pre-implementation Spec Kit analysis
has no unresolved CRITICAL/HIGH finding. `speckit-converge` is not run here; its
skill contract makes it a post-implementation gate.

## Phase 3: User Story 1 - Understand project status in one minute (Priority: P1) 🎯 MVP

**Goal**: Produce an evidence-safe `Tổng quan` from an official snapshot so a
manager can identify current phase, next milestone, schedule condition,
readiness, progress evidence, and top actions within one minute.

**Independent Test**: Project an official fixture and export it directly; the
workbook opens on `Tổng quan`, contains the four approved summary blocks and no
more than five ranked actions, and never fabricates a percentage or green state.

### Tests and implementation for User Story 1

- [X] T005 [US1] **RED** Add and register projection regressions for official source/reporting dates before, within, and after planning bounds; overlapping-phase and same-date-milestone tie-breaks; current phase or exact unknown text; nearest milestone selected only from `MilestoneKind.Milestone`, explicit exclusion of `MilestoneKind.Decision`, or exact missing text; every normative schedule/readiness precedence, label, tone, and detail template including no-schedule-alert with open readiness work; supported state counts including zero-complete/nonzero-in-progress; and percentage eligibility (`actual` and `remaining` both present and non-negative with sum greater than zero, rounded by `MidpointRounding.AwayFromZero`; missing, negative, or zero-sum values produce exact text `Chưa đủ dữ liệu để tính % hoàn thành`) in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressProjectionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`; run them and capture the intended failure. Evidence: initial RED run failed only because the projector type was absent; the regression set then passed after T006/T007.
- [X] T006 [US1] Implement immutable report, condition, milestone, progress, schedule-row, detail-row, and attention-item records with the invariants `overviewAttention == allAttention.Where(item => item.OverviewEligible).Take(5)`, deterministic order, and no raw diagnostics in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportModel.cs`. Evidence: model added; `scripts/build.ps1` completed with 0 warnings and 0 errors.
- [X] T007 [US1] Implement source-date, current-phase, nearest-milestone, separate schedule/readiness, evidence-safe progress rounded with `MidpointRounding.AwayFromZero`, supported counts, and top-five eligible summary projection in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`; use the exact normative labels, precedence, and Vietnamese templates in `specs/005-executive-progress-report/data-model.md` and make T005 green. Evidence: projection regressions and full executable runner passed with 257 PASS and 0 FAIL.
- [X] T008 [US1] Run the focused `ExecutiveProgressProjectionTests` and related `ManagementProjectionTests`, `MetricsTests`, `ManagementEvidenceTests`, and `AnalysisTests` through the executable runner; record exact results in `specs/005-executive-progress-report/tasks.md` before workbook work. Evidence: related tests passed in the full executable runner with 257 PASS and 0 FAIL.
- [X] T009 [US1] **RED** Add and register XLSX regressions proving exactly four sheets in approved order, active sheet `Tổng quan`, title/source date/planning window/compact provenance, four summary blocks, at most five attention items, explicit empty state, no raw technical terms/commit hashes/snapshot IDs/manifest paths/validation codes, standard font, 100% zoom, hidden gridlines, wrapped primary text, frozen context, landscape fit-to-width settings, and text plus approved blue/green/amber/red/gray tone semantics in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`; run them and capture the intended failure. Evidence: initial RED run failed only because `ExecutiveProgressXlsxExporter` was absent; all prior 257 tests remained green.
- [X] T010 [US1] Implement a BCL-only four-sheet package shell, standard-font styles with approved blue/green/amber/red/gray meanings, shared date/number/cell writers, workbook active-sheet metadata, and `Tổng quan` title/provenance/summary/action regions in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs`; do not call or modify `CarioXlsxExporter`, and make T009 green. Evidence: exporter uses only BCL Open XML package parts, emits exactly four approved sheets with `Tổng quan` active, and the XLSX regressions pass.
- [X] T011 [US1] Run focused projection/export tests plus `ManagementProjectionTests`, `ApplicationTests`, `CarioXlsxTests`, and `GanttXlsxTests`; record exact results in `specs/005-executive-progress-report/tasks.md` and stop if any source-backed value differs. Evidence: the latest full executable runner completed with 273 PASS and 0 FAIL; build completed with 0 warnings and 0 errors.
- [X] T012 [US1] Project and export one official fixture directly through `ExecutiveProgressReportProjector` and `ExecutiveProgressXlsxExporter`, inspect its `Tổng quan` package content, and record the independent MVP result in `specs/005-executive-progress-report/tasks.md` without adding an application composition seam. Evidence: fixture package assertions verify title, source date, planning window, four summary regions, bounded attention, and no technical provenance markers.

**Checkpoint**: User Story 1 is independently demonstrable by creating a
four-sheet workbook directly from an official fixture and reading its first
sheet at normal zoom.

## Phase 4: User Story 2 - Review schedule and action detail (Priority: P2)

**Goal**: Complete progressive disclosure: phase/milestone overview,
work-package schedule, full management action list, and delivery-card detail.

**Independent Test**: Inspect all four sheets and prove that each entity level
appears only at its approved depth, labels remain human-readable, weekly timing
is visible, and the full action list matches deterministic priority.

### Tests and implementation for User Story 2

- [X] T013 [US2] **RED** Add projection regressions for mechanical cleanup (surrounding backticks, repeated leading bracket prefixes, and exact redundant leading ID only), preserved Vietnamese source meaning, long untruncated text, every normative Vietnamese state label, and conservative state derivation: card from official effective execution only, milestone from direct state only, phase always unknown, and work package using explicit-in-progress or unanimous evidenced-state rules with mixed/partial evidence remaining unknown. In the same file, cover concrete identity, every approved role-code mapping including `OPS`/`DATA`/`SPEC`/`PILOT`, delivery-card CARIO-`A` selection without `R/C/I/O` substitution, direct readiness owner for work packages, unanimous child accountable-owner roll-up, conflicts/multiple/unresolved roles, evidence-action owner-field precedence, and `Chưa xác định đầu mối` fallback in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressProjectionTests.cs`; run them and capture the intended failure. Evidence: focused projection regressions were red before projector implementation and green afterward; the latest full runner is 273 PASS and 0 FAIL.
- [X] T014 [US2] **RED** Add projection regressions for phase/`MilestoneKind.Milestone`/work-package/card boundaries, `MilestoneKind.Decision` exclusion from timeline rows while remaining eligible for decision attention, duplicate raw IDs across different entity kinds, source-value reconciliation, and attention candidate filtering; prove management-consequence requirement and token translations, unknown technical-token exclusion, human-readable due labels, invalid/ambiguous evidence exclusion, deduplication, overview priority `Blocked → Overdue → DecisionBeforeNextMilestone → AtRisk → MissingOwner`, every exact decision-to-milestone attribution rule, non-attributable open decisions/pending human actions retained only in the full list, known-earliest due dates before unknown dates, stable source order, zero/exactly-five/more-than-five boundaries, top-five eligible cap, and complete list retention in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressProjectionTests.cs`; run them and capture the intended failure. Evidence: attention and entity-boundary regressions were red before projection implementation and green afterward; the latest full runner is 273 PASS and 0 FAIL.
- [X] T015 [US2] Implement the normative terminology and role tables, mechanical label cleanup, owner resolution, reader-facing state mapping, phase/work-package/card rows, valid attention candidate derivation, decision-to-nearest-milestone attribution, overview eligibility, full-list retention, deduplication, and stable sorting in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`; invalid/ambiguous evidence and technical diagnostics without management consequence must not qualify, then make T013 and T014 green. Evidence: projector implementation and all projection assertions pass without mutating canonical source values.
- [X] T016 [US2] Run focused projection tests and related readiness, reconciliation, Gantt, execution-truth, and management-control regressions from `tests/ProjectManagementCompiler.Tests/`; record exact results in `specs/005-executive-progress-report/tasks.md`. Evidence: the latest full executable runner completed with 273 PASS and 0 FAIL.
- [X] T017 [US2] **RED** Add workbook regressions for `Tổng quan` phase/`MilestoneKind.Milestone` rows with no decision-kind timeline rows, `Lịch trình` work-package-only rows, `Vấn đề cần xử lý` exact four columns/all ranked items, `Chi tiết công việc` exact eight columns/card IDs only in final `Mã tham chiếu`, month groups with ISO-week columns, text-labelled `Ngày báo cáo`, reporting dates before planning start and after planning finish extending the axis so the marker remains visible, no daily axis, frozen panes, wrapping, and landscape print settings in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`; run them and capture the intended failure. Evidence: workbook layout regressions were red before axis/detail implementation and green afterward; package XML and settings checks pass.
- [X] T018 [US2] Implement the overview timeline, work-package schedule, complete action table, delivery-card detail table, month/week axis, reporting-date marker, column widths, row wrapping, panes, and print definitions in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs`; make T017 green without changing first-three-sheet identifier rules. Evidence: exporter emits the approved four sheets, monthly/ISO-week axis, connected `■`/`◆` timeline row markers, visible out-of-range report marker, freeze panes, wrap text, and landscape fit-to-width settings.
- [X] T019 [US2] Run all executive projection/XLSX tests, inspect generated worksheet XML for every content-boundary assertion, and record exact results in `specs/005-executive-progress-report/tasks.md`. Evidence: the latest full executable runner completed with 273 PASS and 0 FAIL; web smoke gate and the Excel print render inspected the generated package and passed.

**Checkpoint**: User Story 2 is independently reviewable across all four sheets
without exposing delivery-card/code noise in management views.

## Phase 5: User Story 3 - Preserve authority and technical export contract (Priority: P3)

**Goal**: Expose a distinct official-only download while proving that previews,
proposals, CARIO export, and source truth remain isolated.

**Independent Test**: Download both workbook types from an official snapshot,
activate each preview type, attempt to import the executive workbook, and prove
official state/digest/proposals and the CARIO contract remain unchanged.

### Tests and implementation for User Story 3

- [X] T020 [US3] **RED** Add and register application/API regressions for the not-yet-implemented `ExportExecutiveProgressXlsx(CompilationResult)` seam and `GET /api/exports/executive-progress.xlsx`; prove fail-closed behavior for missing `ImportMetadata`, non-`OfficialCommit` classification, missing/invalid `RegisterStatusDate`, and missing analysis as-of date, plus preview isolation, source-date file name `<Project>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`, `NO_PROJECT`, `NO_OFFICIAL_SNAPSHOT`, exact `422 Unprocessable Entity` plus `EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL` and phase `executive-export` for an incomplete official result, XLSX content type, five-second fixture budget, and no mutation of semantic digest/source execution/proposals/preview state in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressExportApplicationTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`; run them and capture the intended failure before changing `IProjectCompiler`, `ProjectCompiler`, or the endpoint. Evidence: RED run failed because the seam and endpoint were absent; after T021 the application/export regressions pass.
- [X] T021 [US3] Add `ExportExecutiveProgressXlsx(CompilationResult)` plus projector/exporter constructor dependencies in `src/ProjectManagementCompiler/Application/IProjectCompiler.cs` and `src/ProjectManagementCompiler/Application/ProjectCompiler.cs`, implement official-manifest validation and the stable `EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL` diagnostic, then add the official-only endpoint with `422 Unprocessable Entity` mapping and dated file naming in `src/ProjectManagementCompiler/Program.cs`; use `CurrentOfficialResult` and never substitute active manifest/XLSX preview state, then make T020 green. Evidence: application regressions and `scripts/verify-web.ps1` pass; web gate returned the dated XLSX and preserved digest/source execution.
- [X] T022 [US3] **RED** Add and register static browser regressions for separate `Xuất báo cáo tiến độ` (primary) and `Xuất dữ liệu CARIO + Gantt` (secondary) controls, correct endpoint bindings, official availability state, preview non-redirection, and responsive action wrapping in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressUiTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`; run them and capture the intended failure. Evidence: RED run failed because the old single-action header had no executive control or official availability behavior; the three UI regressions now pass.
- [X] T023 [US3] Add the two approved Vietnamese export controls and official-availability behavior without redesigning the header in `src/ProjectManagementCompiler/wwwroot/index.html`, `src/ProjectManagementCompiler/wwwroot/app.js`, and `src/ProjectManagementCompiler/wwwroot/styles.css`; preserve existing technical preview behavior and make T022 green. Evidence: UI keeps the primary action disabled until `OFFICIAL_COMMIT`, routes executive and CARIO downloads separately, and preserves preview state.
- [X] T024 [US3] Add acceptance regressions proving the executive workbook has no CARIO markers, is rejected by `XlsxPreviewImporter`, cannot replace a valid existing preview or official state, and leaves the existing seven-sheet technical workbook semantically unchanged in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressExportApplicationTests.cs`, `tests/ProjectManagementCompiler.Tests/XlsxPreviewImporterTests.cs`, and `tests/ProjectManagementCompiler.Tests/CarioXlsxTests.cs`. Evidence: presentation-only/rejection/state-isolation regression passes; existing CARIO and preview characterization tests remain green.
- [X] T025 [US3] Run all executive, CARIO, preview-import, proposal, canonical serialization, manifest import, and UI regressions; record exact results in `specs/005-executive-progress-report/tasks.md`. Evidence: the latest full executable runner completed with 273 PASS and 0 FAIL; `scripts/verify-web.ps1` completed with `SecurityChecks=PASS`.

**Checkpoint**: Both files have distinct user purposes, and the executive file
cannot become source, canonical state, execution evidence, proposal state, or a
technical preview.

## Phase 6: Polish and Cross-Cutting Verification

**Purpose**: Align documentation, verify the real IDEAEngineering output, and
complete every post-implementation gate before integration is considered.

- [X] T026 [P] Update the user flow, authority wording, two export purposes, dated file name, four-sheet contents, and explicit non-importability in `docs/runbook/mvp1-local.md`, without describing the executive report as a replacement for CARIO or creating a new root README. Evidence: runbook now documents the two download purposes, official-only gate, four reader-facing sheets, dated name, and non-importability.
- [X] T027 [P] Extend the local web smoke gate to import an official fixture, download both exports, assert executive content-disposition and package sheet names, and assert unchanged CARIO/preview behavior in `scripts/verify-web.ps1`. Evidence: `scripts/verify-web.ps1` completed successfully with official import, four-sheet executive package, seven-sheet CARIO package, preview lifecycle, and source/digest safety checks.
- [X] T028 Generate the report from `D:\Work\Projects\IDEAEngineering` commit `0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4`, capture exact source `HEAD`, `git status --porcelain=v1 -uall`, and tracked diff before and after export and require byte-identical outputs, execute every scenario in `specs/005-executive-progress-report/quickstart.md`, and record source-repository immutability, package, 100%-zoom, landscape print-preview, no-clipping, and timed-under-60-seconds evidence in `specs/005-executive-progress-report/acceptance.md`. Evidence: official API/browser-equivalent flow, separate executive/CARIO downloads, preview rejection and working-tree isolation passed; Excel COM inspection confirmed 100% zoom, landscape, fit-to-one-page-wide, and the rendered five-page print output showed wrapped schedule/detail text with no clipped primary cells; report generation completed within the local five-second budget.
- [X] T029 Run fresh `dotnet build .\ProjectManagementCompiler.sln --no-restore`, the full executable test runner, `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1`, and `git diff --check`; record every exact command, test count, warning/error count, and exit result in `specs/005-executive-progress-report/acceptance.md`. Evidence: the fresh gate completed with build 0 warnings/0 errors, 278 PASS/0 FAIL, web `SecurityChecks=PASS`, and `git diff --check` exit 0.
- [X] T030 Run strictly read-only `$speckit-analyze` after implementation, report every artifact/code-alignment finding, obtain explicit user approval before separate remediation edits in `specs/005-executive-progress-report/`, and rerun affected verification after approved edits. Evidence: the final post-implementation analysis found 0 actionable findings across 30 FR, 7 SC, 32 tasks, all required artifacts, implementation symbols, .NET 10/dependency-free constraints, and constitution alignment; no remediation edit was required.
- [X] T031 Run `$speckit-converge` only after T030, append any uncovered implementation work to `specs/005-executive-progress-report/tasks.md`, complete it with RED-GREEN-focused tests, and repeat T029-T030 until no task remains. Evidence: final converge checked the 37 requirements/success criteria, plan decisions, constitution constraints, implementation paths, and existing tasks; it was clean and appended no convergence phase or task.
- [X] T032 Run the repository code-review workflow against the feature branch, resolve all correctness/spec findings in the reviewed source/test/docs paths, and rerun T029 before any merge or push decision. Evidence: review of `main...HEAD` at `c949ac9` in the dedicated worktree found no remaining correctness, standards, or specification issue; final verification is recorded as 278 PASS/0 FAIL with build 0/0 and web `SecurityChecks=PASS`.

## Dependencies and Execution Order

### Phase dependencies

- Setup (Phase 1) precedes all feature work.
- Foundational Spec Kit gate (Phase 2) depends on the generated task list and
  blocks production edits.
- User Story 1 creates the report model, first-sheet projection, and exporter
  shell needed by later stories; the application composition seam waits for
  the RED tests in User Story 3.
- User Story 2 depends on User Story 1's model/exporter but remains independently
  testable by checking entity-depth and schedule/action/detail contracts.
- User Story 3 depends on the completed workbook and adds external access plus
  authority isolation.
- Polish/verification depends on all selected user stories.

### User story dependency graph

```text
Setup → Spec Kit Analyze → US1 (overview/export core)
                              ↓
                         US2 (detail/layout)
                              ↓
                         US3 (official endpoint/UI/authority)
                              ↓
                    Full verification → Analyze → Converge → Review
```

### Parallel opportunities

- T002 and T003 touch separate fixture/characterization surfaces and can run in
  parallel after T001.
- T013 and T014 are sequential because both edit
  `ExecutiveProgressProjectionTests.cs`.
- T022 follows T020 registration because both edit the executable runner's
  `Program.cs`; T023 waits for the endpoint contract from T021.
- T026 and T027 can run in parallel after all story behavior is green.

### Within every RED-GREEN group

1. Add the smallest regression for one named contract and register it.
2. Run it and capture the expected failure; unrelated failure does not count.
3. Implement the smallest correct production change.
4. Run the focused regression until green.
5. Run the related regression set before starting the next behavior group.
6. Refactor only while all focused and related tests remain green.

## Implementation Strategy

### MVP first

1. Complete Setup and the pre-implementation analyze gate.
2. Complete User Story 1 through T012.
3. Stop and validate the first sheet directly with an official fixture.

### Incremental delivery

1. Add User Story 2's progressive disclosure and print-ready schedule.
2. Add User Story 3's official-only endpoint and distinct UI actions.
3. Run real-source acceptance and every post-implementation gate.

### Scope guardrails

- Do not add a package, database, connector, Docker configuration, Office
  automation, AI/LLM prose, PDF/deck export, import path, or source write.
- Do not alter canonical ownership, source execution, proposal semantics,
  manifest capture, or the seven-sheet CARIO contract.
- Do not mark a RED task complete without observed failing output, or a
  verification task complete without fresh command evidence.
- Do not run `speckit-converge` before implementation; do not merge or push
  until T029-T032 are complete and the user requests integration.
