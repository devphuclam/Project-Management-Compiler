# Tasks: Management Report Readability

**Input**: Design documents from `specs/007-management-report-readability/`

**Prerequisites**: [plan.md](plan.md), [spec.md](spec.md),
[research.md](research.md), [data-model.md](data-model.md),
[contracts/](contracts/), and [quickstart.md](quickstart.md)

**Tests**: Required. The project constitution and approved workflow require
RED–GREEN–REFACTOR. Every test task below must be run and observed failing for
the intended reason before its paired production task begins.

**Organization**: Tasks are grouped by user story. Each story has an explicit
independent test and a checkpoint. Release of the replacement workbook requires
all five stories because the six-sheet contract is atomic.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: May run in parallel because it uses a different file and has no
  dependency on an incomplete task.
- **[Story]**: Maps the task to a user story from [spec.md](spec.md).
- Every task names the exact repository path it changes or records.

## Phase 1: Setup and Baseline

**Purpose**: Capture the coherent Feature 006 starting point and reusable test
data before production behavior changes.

- [X] T001 Run `scripts/build.ps1` and `scripts/test.ps1`, capture the exact pass/fail and `git status --short` baseline, and create the implementation log at `specs/007-management-report-readability/implementation-evidence.md`
- [X] T002 Add reusable fixture builders for nested/repeated title prefixes, all five Actual evidence shapes, WBS hierarchy counts, operating categories, and official metadata without changing existing fixture expectations in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressTestFixtures.cs`

**Checkpoint**: Existing build and tests are characterized, and future RED
tests can use deterministic repository-owned fixtures.

---

## Phase 2: Foundational Contracts

**Purpose**: Establish shared language and spreadsheet capabilities required by
every story.

**⚠️ CRITICAL**: Do not begin a user-story implementation until this phase is
green.

### Reader-language policy

- [X] T003 Add and register RED tests covering exact IDs, repeated `[PH0][PLN01]` prefixes, kind labels, arrows, Markdown markers, meaningful bracketed text, long Vietnamese titles, empty titles, `Chưa ghi nhận`, and `Chưa phân công` in `tests/ProjectManagementCompiler.Tests/ReaderFacingTextPolicyTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`
- [X] T004 Implement deterministic cleanup that removes only recognized identity/formatting noise, preserves business meaning, never generates prose, and returns `Chưa ghi nhận` for an empty result in `src/ProjectManagementCompiler/Management/ReaderFacingTextPolicy.cs`
- [X] T005 [P] Replace private name cleanup and obsolete owner/evidence wording with `ReaderFacingTextPolicy` while preserving projection facts in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`
- [X] T006 [P] Replace private name cleanup and obsolete owner/evidence wording with `ReaderFacingTextPolicy` while preserving daily hierarchy and evidence facts in `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttProjector.cs`

### Neutral workbook progressive disclosure

- [X] T007 Add RED model-contract tests requiring row `OutlineLevel` in 0–7, Delivery Card `Hidden` state, parent `Collapsed` state, non-overlapping one-based column groups in used bounds, and a valid optional auto-filter range in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`
- [X] T008 Extend worksheet, row, and column value objects with validated row outlines, column groups, initial hidden/collapsed state, summary direction, and optional auto-filter metadata in `src/ProjectManagementCompiler/Outputs/ExecutiveWorkbookDocument.cs`
- [X] T009 Add RED package assertions for worksheet `outlinePr`, row outline/hidden/collapsed attributes, grouped columns, and `autoFilter` XML while preserving deterministic package bytes in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`
- [X] T010 Serialize outline settings, row and column grouping, hidden/collapsed state, and auto-filter ranges deterministically with no macro, connection, or external-link parts in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressXlsxExporter.cs`

**Checkpoint**: One language policy supplies all reader names/labels, and the
neutral workbook can express the approved WBS disclosure without external
packages.

---

## Phase 3: User Story 1 — Understand the project quickly (Priority: P1) 🎯 Reviewable MVP

**Goal**: Deliver a concise `Tổng quan` that answers current position, supported
progress, plan change, next milestone, and required decision in that order.

**Independent Test**: Compose `Tổng quan` from the accepted official fixture
and verify that a reader can identify the four requested facts and the top
decision within 60 seconds, with no technical metadata or identifier clutter.

### Tests for User Story 1

- [X] T011 [US1] Add RED projection tests for concise current phase, evidence-backed progress, schedule variance, next milestone, at-most-five actions, missing-action state, and clean Reader-Facing Names in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressProjectionTests.cs`
- [X] T012 [US1] Add RED worksheet tests for summary question order, compact headline blocks, phase/milestone overview, action columns, active first sheet, landscape settings, frozen context, and absence of forbidden technical text in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`

### Implementation for User Story 1

- [X] T013 [US1] Project concise summary statements and deterministic priority actions without commit, snapshot, path, raw enum, validation code, or generated explanation in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`
- [X] T014 [P] [US1] Create shared typed cell, date, style, range, and concise label helpers without business selection logic in the neutral workbook composition boundary (`src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`)
- [X] T015 [US1] Compose the complete `Tổng quan` sheet with at most five actions and readable landscape/normal-zoom behavior in the neutral workbook composition boundary.
- [X] T016 [US1] Delegate only the overview sheet to the focused composer while preserving the still-unmigrated supporting sheets during this checkpoint in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`

**Checkpoint**: `Tổng quan` is independently composable, visually reviewable,
and answers the sponsor's primary questions without technical decoding.

---

## Phase 4: User Story 2 — Compare plan with recorded execution (Priority: P2)

**Goal**: Render all five supported Actual evidence shapes truthfully on the
daily Gantt without inferring dates or progress.

**Independent Test**: Project and compose a fixture containing recorded
interval, open interval, finish-only, effort-only, and no-evidence rows; each
must produce the approved interval/marker/empty state and zero inferred dates.

### Tests for User Story 2

- [X] T017 [US2] Add RED projection tests for `RecordedInterval`, `OpenRecordedInterval`, `CompletionPoint`, `EffortOnly`, and `None`, including finish-only with null start, effort-only with no date, invalid-effort percentage suppression, and conservative roll-up coverage in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressProjectionTests.cs`
- [X] T018 [US2] Add RED workbook tests for one daily column per date, adjacent `Kế hoạch`/`Thực tế` lanes, green intervals, `✓` finish point, `● Có ghi nhận`, empty `Chưa ghi nhận`, milestone symbols, weekend shading, reporting boundary, frozen context, and horizontal pagination in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`

### Implementation for User Story 2

- [X] T019 [P] [US2] Add exactly `RecordedInterval`, `OpenRecordedInterval`, `CompletionPoint`, `EffortOnly`, and `None`, plus required evidence label and nullable official effort fields, to `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttModel.cs`
- [X] T020 [US2] Classify direct and roll-up Actual shapes from official facts only, retain `ActualFinish` without inventing `ActualStart`, and calculate percentage only for finite non-negative effort with a positive sum in `src/ProjectManagementCompiler/Management/ExecutiveDailyGanttProjector.cs`
- [X] T021 [US2] Compose the full daily `Gantt` sheet from explicit projection shapes, including truthful markers, continuation cues, hierarchy, coverage, panes, and print behavior in the neutral workbook composition boundary.
- [X] T022 [US2] Replace the legacy five-sheet daily-Gantt construction with the `Gantt` sheet without changing official selection or workbook serialization ownership in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`

**Checkpoint**: The Gantt independently distinguishes plan, recorded
execution, sparse evidence, and missing evidence at normal zoom.

---

## Phase 5: User Story 3 — Explore the full scope hierarchy (Priority: P2)

**Goal**: Add a complete Project → Phase → Work Package → Delivery Card WBS
that opens at Work Package depth and progressively reveals details.

**Independent Test**: Reconcile the accepted fixture to exactly 1 Project, 6
Phases, 35 Work Packages, and 53 Delivery Cards; then expand the workbook
outline and verify every Delivery Card appears once with correct parentage.

### Tests for User Story 3

- [X] T023 [US3] Add and register RED projection tests for exact four-level membership, canonical order, positional WBS numbering, parent identity, depths 0–3, milestone/gate exclusion, unresolved-parent visibility, clean `Hạng mục`, and 1/6/35/53 reconciliation in `tests/ProjectManagementCompiler.Tests/ExecutiveWbsProjectionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`
- [X] T024 [US3] Extend the RED WBS tests for `Chưa phân công`, approved state, evidence-based percentage, attention, planned/Actual fields, latest update, ordered predecessor IDs, dependency state, concise evidence, and relative-only source reference in `tests/ProjectManagementCompiler.Tests/ExecutiveWbsProjectionTests.cs`
- [X] T025 [US3] Add RED workbook tests for exact primary columns `WBS`, `Mã`, `Hạng mục`, `Loại`, `Đầu mối`, `Trạng thái`, `% thực tế`, `Cần chú ý`; initially hidden Delivery Cards; expandable parent groups; collapsed Plan/Actual/Relationships/Evidence columns; filtering; and frozen identity context in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`

### Implementation for User Story 3

- [X] T026 [P] [US3] Define WBS kinds exactly as Project, Phase, WorkPackage, and DeliveryCard and add the constrained row/projection fields from `data-model.md` in `src/ProjectManagementCompiler/Management/ExecutiveWbsModel.cs`
- [X] T027 [US3] Project canonical four-level membership, stable source order, positional numbering, parentage, depths 0–3, Reader-Facing Names, and explicit unresolved-parent context without including milestones in `src/ProjectManagementCompiler/Management/ExecutiveWbsProjector.cs`
- [X] T028 [US3] Add owner/state/progress roll-ups, attention, plan/Actual facts, update, predecessors, dependency condition, concise evidence, and minimal relative source locator without fabricating missing values in `src/ProjectManagementCompiler/Management/ExecutiveWbsProjector.cs`
- [X] T029 [US3] Add the immutable WBS projection to the root report and invoke `ExecutiveWbsProjector` from the official report boundary in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportModel.cs` and `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`
- [X] T030 [US3] Compose `WBS` with clean names, eight visible primary columns, Delivery Card row groups initially hidden, four independent optional column groups initially collapsed, complete filter range, and frozen context in the neutral workbook composition boundary.
- [X] T031 [US3] Insert the WBS composer into the interim workbook without altering Gantt or technical workbook contracts in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`

**Checkpoint**: Scope hierarchy is complete, readable by default, and fully
expandable without treating milestones or dates as WBS structure.

---

## Phase 6: User Story 4 — Run the next 30 days (Priority: P3)

**Goal**: Replace separate near-term/issues journeys with one deduplicated
operating agenda ordered by management urgency.

**Independent Test**: Project a fixture containing decisions, blockers,
overdue unfinished work, active work, intersecting future work, milestones,
duplicates, and no-item state; verify exact membership and category order.

### Tests for User Story 4

- [X] T032 [US4] Add and register RED projection tests for the reporting-date-through-plus-29-days window, decision/blocker precedence, overdue unfinished membership, active membership, planned/milestone intersection, outside-window exclusion, stable-target deduplication, known-before-unknown dates, source-order ties, and empty population in `tests/ProjectManagementCompiler.Tests/ExecutiveOperatingProjectionTests.cs` and `tests/ProjectManagementCompiler.Tests/Program.cs`
- [X] T033 [US4] Add RED workbook tests for the primary columns `Việc cần làm`, `Ảnh hưởng`, `Đầu mối`, `Cần xong trước`, `Trạng thái`, `Bối cảnh tiến độ`; approved group order; concise empty state; wrapped text; frozen headings; and readable landscape print settings in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`

### Implementation for User Story 4

- [X] T034 [P] [US4] Define operating categories exactly as DecisionOrBlocker, OverdueUnfinished, Active, and PlannedOrMilestone and add required stable key, target, action, consequence, owner, date, state, schedule context, and source-order fields in `src/ProjectManagementCompiler/Management/ExecutiveOperatingModel.cs`
- [X] T035 [US4] Project and deduplicate attention plus near-term schedule facts using category/date/source order without dropping supported decisions outside the schedule window or creating a Kanban workflow in `src/ProjectManagementCompiler/Management/ExecutiveOperatingProjector.cs`
- [X] T036 [US4] Add the ordered operating collection to the root report and invoke the operating projector from official attention and daily-Gantt projections in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportModel.cs` and `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`
- [X] T037 [US4] Compose `Điều hành 30 ngày` with one row per deduplicated concern, approved columns/order/empty state, wrapped text, frozen headers, and bounded landscape printing in the neutral workbook composition boundary.
- [X] T038 [US4] Replace legacy `30 ngày tới` and `Vấn đề cần xử lý` sheet construction with the one focused operating composer in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`

**Checkpoint**: A delivery lead can run the next 30 days from one operating
sheet without reconciling duplicate schedule and issue rows.

---

## Phase 7: User Story 5 — Trace facts without cluttering the report (Priority: P3)

**Goal**: Complete reader-first Delivery Card detail and centralize authority,
source, baseline, contract, and limitations on one metadata sheet.

**Independent Test**: Trace one summary/Gantt item through its Delivery Card
detail to report metadata while verifying that the first five sheets contain no
hash, snapshot ID, absolute path, raw code, or repeated provenance boilerplate.

### Tests for User Story 5

- [X] T039 [US5] Add RED projection tests for one detail row per Delivery Card, reader-first field order, concise missing labels, predecessors/dependency context, stable reference, minimal relative provenance, and complete centralized official metadata in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressProjectionTests.cs`
- [X] T040 [US5] Add RED workbook tests for exact six-sheet order and active `Tổng quan`, filtered/frozen reader-first `Chi tiết công việc`, label/value `Thông tin báo cáo`, zero forbidden technical tokens on sheets 1–5, and complete authority metadata only on sheet 6 in `tests/ProjectManagementCompiler.Tests/ExecutiveProgressXlsxTests.cs`
- [X] T041 [US5] Add RED application regressions for unchanged method, route, media type, dated filename, `CurrentOfficialResult` selection, sparse-evidence success, contradiction failure, input/state immutability, deterministic bytes, report non-importability, and unchanged technical CARIO + Gantt package in the existing executive/application regression suites.

### Implementation for User Story 5

- [X] T042 [P] [US5] Add `ExecutiveReportMetadata` with required authority, source identity, snapshot/project/baseline identities, contract version, register revision, reporting/analysis dates, planning range, and concise limitations; extend detail with attention, dependency, and minimal provenance fields in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportModel.cs`
- [X] T043 [US5] Project metadata exclusively from official import/baseline facts and project one reader-first detail row per canonical Delivery Card without absolute paths or fabricated values in `src/ProjectManagementCompiler/Management/ExecutiveProgressReportProjector.cs`
- [X] T044 [P] [US5] Compose filtered/frozen `Chi tiết công việc` with reader-facing fields before technical identity and blank typed cells beside concise missing-evidence labels in the neutral workbook composition boundary.
- [X] T045 [P] [US5] Compose `Thông tin báo cáo` as concise label/value sections for authority, dates, source commit/object, snapshot, project, baseline, revision, contract, planning window, and limitations in the neutral workbook composition boundary.
- [X] T046 [US5] Assemble exactly `Tổng quan`, `Điều hành 30 ngày`, `Gantt`, `WBS`, `Chi tiết công việc`, and `Thông tin báo cáo` in order, activate index 0, and preserve deterministic composition in `src/ProjectManagementCompiler/Outputs/ExecutiveProgressWorkbookComposer.cs`
- [X] T047 [US5] Preserve `ExportExecutiveProgressXlsx`, `/api/exports/executive-progress.xlsx`, official-only selection, and dated filename while updating only test-demonstrated compatibility expectations.

**Checkpoint**: One six-sheet report supports management reading and audit
follow-up while remaining presentation-only and non-importable.

---

## Phase 8: Polish and Release Evidence

**Purpose**: Validate cross-story quality, real-source behavior, visual
readability, and repository hygiene before implementation can be reviewed.

- [X] T048 [P] Update the progress-report runbook for the six-sheet reader journey, official-only export, non-importability, and separate future Project Workbook boundary in `docs/runbook/mvp1-local.md`
- [X] T049 Run focused suites plus `scripts/verify.ps1`, `git diff --check`, and `git status --short`, and append exact commands/results with zero failed checks to `specs/007-management-report-readability/implementation-evidence.md`
- [X] T050 Run the approved exact-commit IDEAEngineering import and export flow, measure the 6/35/53/7 fixture under five seconds, compare source/application state before and after, and append artifact path plus immutable before/after evidence to `specs/007-management-report-readability/implementation-evidence.md`
- [X] T051 Open the real workbook at 100% zoom, inspect all six sheets and the first two landscape print previews against `quickstart.md`, record any clipping or ambiguity as failure, and append screenshots/findings to `specs/007-management-report-readability/implementation-evidence.md`
- [X] T052 Inspect staged/untracked inventory for generated workbooks, secrets, credentials, absolute private configuration, and proprietary source material; record the clean public-repository decision in `specs/007-management-report-readability/implementation-evidence.md`
- [X] T053 Run post-implementation Spec Kit analyze/converge and standards-plus-spec code review, resolve every approved critical/high finding, and record final traceability and review results in `specs/007-management-report-readability/implementation-evidence.md`

---

## Dependencies and Execution Order

### Phase dependencies

- **Phase 1** starts immediately.
- **Phase 2** depends on the baseline/fixture tasks and blocks all stories.
- **US1** starts after Phase 2 and establishes the summary/formatting seam.
- **US2** and **US3** start after Phase 2; their projection/model work may run
  in parallel, but each top-level composer integration must be serialized with
  T016/T022/T031.
- **US4** starts after the foundational language contract and existing
  attention/daily-Gantt projections; T038 follows T022 and T031 to avoid
  concurrent edits to the top-level composer.
- **US5** depends on the story projections/composers required for final six-
  sheet assembly; T046 follows T016, T022, T031, and T038.
- **Phase 8** depends on all five stories and is the release gate.

### User-story dependencies

- **US1 (P1)**: Independently testable through the overview projection and
  worksheet composer after Phase 2.
- **US2 (P2)**: Independently testable through daily-Gantt projection/composer;
  no dependency on WBS or operating projection.
- **US3 (P2)**: Independently testable through WBS projection/composer; no
  dependency on Actual Gantt rendering beyond shared official facts.
- **US4 (P3)**: Uses established attention and daily-Gantt facts but owns its
  membership/deduplication contract independently.
- **US5 (P3)**: Detail and metadata are independently testable; final workbook
  assembly depends on all sheet composers.

### Within each story

- Add/register tests and observe the intended RED result.
- Add or extend immutable presentation models.
- Implement projection rules.
- Implement focused worksheet composition.
- Integrate into the top-level composer.
- Run the story's focused and existing related regression tests GREEN before
  moving to the next integration checkpoint.

## Parallel Opportunities

- T005 and T006 may run in parallel after T004.
- US2 model/projection work and US3 model/projection work may run in parallel
  after Phase 2 if they do not edit the top-level composer concurrently.
- T014 may proceed alongside projection refinements because it owns a new
  output-only file.
- T019, T026, T034, and T042 own distinct model files and may proceed in
  parallel after their RED tests exist.
- T044 and T045 may run in parallel after T043.
- T048 may run in parallel with focused automated verification, but real-source
  and visual evidence remain sequential because they inspect the same final
  workbook.

## Parallel Example: User Stories 2 and 3

```text
Track A RED: T017 + T018
Track A GREEN: T019 → T020 → T021

Track B RED: T023 → T024, plus T025
Track B GREEN: T026 → T027 → T028 → T029 → T030

Serialize integration edits afterward:
T022 → T031
```

## Requirement Traceability

| Requirement(s) | Primary implementation/test tasks |
| --- | --- |
| FR-001 | T041, T047 |
| FR-002–FR-003 | T040, T046 |
| FR-004 | T011–T016 |
| FR-005–FR-006 | T032–T038 |
| FR-007–FR-008 | T018, T021–T022 |
| FR-009–FR-013 | T017–T021 |
| FR-014–FR-019 | T023–T031 |
| FR-020 | T039–T044, T046 |
| FR-021 | T039–T040, T042–T043, T045–T046 |
| FR-022–FR-023 | T003–T006, T011–T016, T039–T046 |
| FR-024–FR-025 | T012, T015, T018, T021, T025, T030, T033, T037, T040, T044–T045, T051 |
| FR-026 | T009–T010, T041, T049–T050 |
| FR-027 | T041, T047, T052 |
| FR-028 | T017, T020, T041, T047 |
| SC-001 | T011–T015, T051 |
| SC-002 | T040, T046 |
| SC-003 | T003–T006, T023–T030 |
| SC-004 | T023–T031 |
| SC-005 | T002, T017–T021 |
| SC-006 | T003, T011–T016, T039–T046 |
| SC-007 | T012, T015, T018, T021, T025, T030, T033, T037, T040, T044–T045, T051 |
| SC-008 | T041, T050 |
| SC-009 | T041, T047, T049 |
| SC-010 | T050 |
| SC-011 | T009–T010, T041, T049 |

## Implementation Strategy

### Reviewable MVP slice

1. Complete Setup and Foundational phases.
2. Complete US1.
3. Demonstrate `Tổng quan` independently against the 60-second reader outcome.
4. Do not release this partial slice as the replacement workbook; the six-sheet
   contract requires US2–US5.

### Incremental delivery

1. Shared language and workbook disclosure foundation.
2. US1 summary clarity.
3. US2 truthful Plan/Actual Gantt.
4. US3 complete progressive-disclosure WBS.
5. US4 unified 30-day operating agenda.
6. US5 traceability, metadata, and atomic six-sheet integration.
7. Real-source, visual, compatibility, and public-repository release gates.

## Notes

- `[P]` means different files and no incomplete dependency; it is not a request
  to create subagents.
- Existing user-owned untracked files must remain untouched unless explicitly
  brought into scope.
- Commit after each green logical increment; never combine the entire feature
  into one final commit.
- No implementation, commit, merge, or push begins until the human review gate
  approves these design artifacts.
