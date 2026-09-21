# Feature Specification: Executive Daily Gantt Report

**Feature Branch**: `codex/feature006-executive-daily-gantt-report`

**Created**: 2026-09-21

**Status**: Draft for review

**Input**: User description: "Upgrade the application's existing executive progress export so one management workbook presents the approved daily Gantt design, including evidence-backed Actual progress directly on the Gantt, a 30-day operating view, management issues, and delivery-card detail."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See plan and actual progress together (Priority: P1)

As a project sponsor, I want the exported management workbook to show the
approved baseline and official actual progress together so that I can see how
far execution has advanced, whether work is late, and how trustworthy the
reported progress is without interpreting a technical workbook.

**Why this priority**: A plan-only Gantt cannot answer the sponsor's primary
question: how much work has actually run and how execution compares with the
approved plan.

**Independent Test**: Export a report from an official snapshot containing a
mixture of completed, in-progress, forecast, and unrecorded delivery cards.
Open `Tổng quan` and `Gantt theo ngày` and verify that Plan and Actual are
visually distinct, percentage and coverage statements reconcile to the
snapshot, and missing Actual remains explicit.

**Acceptance Scenarios**:

1. **Given** an official delivery card with planned and actual dates, **when**
   the report is exported, **then** the daily Gantt shows a blue Plan lane and
   a green Actual lane in the same logical task band.
2. **Given** an in-progress card with an actual start and no actual finish,
   **when** the report is exported, **then** its Actual lane runs from the
   actual start through the report's analysis date and does not imply an
   actual finish.
3. **Given** a completed card with actual start and actual finish, **when** the
   report is exported, **then** its Actual lane ends on the recorded actual
   finish.
4. **Given** a card marked in progress but lacking an actual start, **when**
   the report is exported, **then** no green Actual lane is invented and the
   row says that Actual data is insufficient.
5. **Given** valid actual and remaining effort, **when** progress is shown,
   **then** the percentage equals actual effort divided by actual plus
   remaining effort; otherwise the report says `Chưa đủ dữ liệu để tính % hoàn
   thành`.
6. **Given** only some cards have sufficient Actual data, **when** a summary or
   roll-up is presented, **then** the report shows its evidence coverage and
   does not present a partial percentage as whole-project completion.

---

### User Story 2 - Review one complete management workbook (Priority: P2)

As a project manager, I want one export containing executive, detailed, near-
term, issue, and traceability views so that I can send a single coherent file
to management and answer follow-up questions from the same snapshot.

**Why this priority**: Separate or competing management exports create version
confusion and make it harder to reconcile the headline with the underlying
work.

**Independent Test**: Export one workbook from the approved fixture and verify
that it contains exactly five sheets in the approved order, opens on the
executive sheet, and provides a traceable path from a headline to a delivery
card without requiring another file.

**Acceptance Scenarios**:

1. **Given** an official project snapshot, **when** the user chooses the
   existing progress-report export action, **then** one workbook is downloaded
   with sheets `Tổng quan`, `Gantt theo ngày`, `30 ngày tới`, `Vấn đề cần xử
   lý`, and `Chi tiết công việc`, in that order.
2. **Given** the workbook is opened, **when** the reader first views it, **then**
   `Tổng quan` is active and shows current phase, next milestone, actual
   progress, evidence coverage, update recency, and management decisions before
   detailed rows.
3. **Given** the reader opens `Gantt theo ngày`, **when** the schedule is
   inspected, **then** Project, Phase, Work package, Delivery card, and
   Milestone hierarchy is visible with one calendar day per timeline column.
4. **Given** a milestone date, **when** it appears on a Gantt, **then** it uses
   a distinct milestone symbol and remains identifiable without relying only
   on color.
5. **Given** weekends and the reporting boundary, **when** the Gantt is viewed,
   **then** weekends have a subdued background and the reporting date has a
   clearly labelled vertical marker.

---

### User Story 3 - Focus on the next 30 days (Priority: P3)

As a delivery lead, I want a near-term daily view containing overdue open work
and work scheduled in the next 30 days so that I can run the project without
searching the full roadmap.

**Why this priority**: The full project Gantt provides traceability, but daily
management needs a smaller actionable window.

**Independent Test**: Use a fixture with items before, within, crossing, and
after the 30-day boundary. Verify that the near-term sheet includes only the
approved population, orders it deterministically, and uses the same Plan and
Actual meanings as the full Gantt.

**Acceptance Scenarios**:

1. **Given** the official source reporting date, **when** `30 ngày tới` is
   produced, **then** its date axis covers exactly that date through 29 days
   later.
2. **Given** unfinished work whose planned finish precedes the reporting date,
   **when** the near-term view is produced, **then** the overdue work remains
   visible even though its planned dates precede the 30-day window.
3. **Given** work whose planned interval intersects the 30-day window, **when**
   the near-term view is produced, **then** the work is included.
4. **Given** work outside the window that is completed or not overdue, **when**
   the near-term view is produced, **then** it is excluded.
5. **Given** several eligible items, **when** they are listed, **then** overdue
   open work appears first, followed by nearest planned finish and stable
   project order.

---

### User Story 4 - Preserve authority and existing contracts (Priority: P4)

As a project owner, I want the richer workbook to remain a presentation-only
projection of official evidence so that visual improvements cannot turn local
proposals into facts or disrupt the technical CARIO workbook.

**Why this priority**: Management clarity is valuable only if the report
preserves the source-of-truth boundary and can be reproduced from the same
official snapshot.

**Independent Test**: Load official, proposal, and preview states; export the
management and technical workbooks; and verify that only official execution
appears as Actual, neither export mutates application state, and the technical
workbook contract remains unchanged.

**Acceptance Scenarios**:

1. **Given** an official snapshot plus local execution proposals, **when** the
   management workbook is exported, **then** proposal values do not appear as
   official Actual.
2. **Given** only a preview or no official snapshot, **when** management export
   is requested, **then** the system refuses to label or export it as the
   official management report.
3. **Given** a valid official snapshot with sparse Actual data, **when** the
   report is exported, **then** export succeeds with truthful missing-data
   labels rather than fabricated values.
4. **Given** contradictory official Actual dates or invalid effort values that
   cannot be represented truthfully, **when** export is requested, **then** it
   fails with a specific user-facing diagnostic rather than producing a
   misleading workbook.
5. **Given** either workbook is exported, **when** project state is compared
   before and after, **then** the official snapshot, source execution,
   proposals, and source repository are unchanged.
6. **Given** the feature is installed, **when** the technical CARIO + Gantt
   workbook is exported or previewed, **then** its sheet contract and import
   behavior remain unchanged.

### Edge Cases

- The source reporting date falls before the baseline start or after the
  baseline finish.
- Planning spans several months and produces more daily columns than fit on
  one screen.
- A one-day task has the same planned start and finish.
- An in-progress card has a valid actual start after its planned finish.
- A completed card finishes before planned start or after planned finish.
- A card has actual effort but no remaining effort, the inverse, a negative
  value, or an actual-plus-remaining sum of zero.
- A card has execution state but no actual dates, or actual dates but no
  effort evidence.
- A work package or phase has only partial child-card Actual coverage.
- A card has an official forecast finish but no sufficient basis for a
  completion percentage.
- There are no items in the 30-day window, no management issues, or no next
  milestone.
- A delivery card spans a weekend or crosses a month boundary.
- Project titles and work-item descriptions contain Vietnamese diacritics,
  markdown decoration, long text, or repeated identifiers.
- The project contains enough rows that vertical scrolling is required while
  fixed identity columns and timeline headers must remain visible.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The application MUST use its existing management progress-report
  export action to produce one production workbook; it MUST NOT introduce a
  second competing management export.
- **FR-002**: The workbook MUST contain exactly five reader-facing sheets in
  this order: `Tổng quan`, `Gantt theo ngày`, `30 ngày tới`, `Vấn đề cần xử
  lý`, and `Chi tiết công việc`.
- **FR-003**: The workbook MUST open on `Tổng quan`.
- **FR-004**: The workbook MUST derive all project, plan, Actual, forecast,
  attention, ownership, and progress values from the current official snapshot
  and approved deterministic analysis.
- **FR-005**: Local execution proposals, workbook previews, working-tree
  previews, and other non-authoritative states MUST NOT appear as official
  Actual in the management workbook.
- **FR-006**: The workbook MUST remain presentation-only and MUST NOT qualify as
  a technical workbook import, proposal, execution record, or source input.
- **FR-007**: Producing the workbook MUST NOT change the official snapshot,
  canonical project, source execution, proposals, preview state, or source
  repository.
- **FR-008**: `Tổng quan` MUST present current phase, next milestone, actual
  progress, evidence coverage, latest official update, and management decisions
  before its Gantt region.
- **FR-009**: `Tổng quan` MUST contain a daily phase-and-milestone Gantt using
  the same Plan, Actual, forecast, weekend, milestone, and reporting-date
  meanings as the detailed Gantt.
- **FR-010**: `Tổng quan` MUST show at most five management attention items in
  the approved priority order; the complete list MUST remain available in
  `Vấn đề cần xử lý`.
- **FR-011**: `Gantt theo ngày` MUST present the complete Project → Phase → Work
  package → Delivery card → Milestone hierarchy.
- **FR-012**: Every calendar date from the displayed planning start through the
  displayed planning finish MUST occupy one distinct daily timeline column.
- **FR-013**: Daily axes MUST group dates by month, identify weekdays, visually
  distinguish weekends, and label the official reporting-date marker.
- **FR-014**: Each logical Gantt item MUST keep its descriptive identity visible
  once while presenting Plan and Actual as two adjacent lanes in the same task
  band.
- **FR-015**: The Plan lane MUST represent immutable baseline start and finish
  dates in blue.
- **FR-016**: A completed delivery card's Actual lane MUST represent its
  recorded actual start through actual finish in green.
- **FR-017**: An in-progress delivery card's Actual lane MUST represent its
  recorded actual start through the report analysis date in green and MUST NOT
  imply an actual finish.
- **FR-018**: A delivery card without a valid actual start MUST NOT receive a
  green Actual lane, even when its execution state is in progress.
- **FR-019**: An official forecast segment MAY appear in amber only when an
  official forecast finish is available; elapsed calendar time alone MUST NOT
  create a forecast.
- **FR-020**: Blocked or overdue work MUST have a textual state or variance
  indicator and a red visual cue; color alone MUST NOT carry the meaning.
- **FR-021**: Milestones MUST use a distinct symbol and zero-duration display.
- **FR-022**: The length and position of Gantt lanes MUST encode dates only;
  they MUST NOT be stretched or shortened to represent effort percentage.
- **FR-023**: A progress percentage MUST be shown only when actual and
  remaining effort are finite, non-negative, and their sum is greater than
  zero; it MUST equal actual divided by actual plus remaining effort.
- **FR-024**: When the percentage eligibility rule is not met, the report MUST
  say `Chưa đủ dữ liệu để tính % hoàn thành` or use an equivalent row-level
  insufficient-data label; it MUST NOT substitute zero.
- **FR-025**: Every project, phase, or work-package roll-up that presents a
  percentage MUST include all delivery cards in its relevant scope; otherwise
  it MUST show a coverage fraction instead of a partial percentage presented
  as complete scope.
- **FR-026**: Roll-up Actual date lanes MAY summarize known child-card activity
  only when their evidence coverage is displayed and their presentation cannot
  be mistaken for complete child coverage.
- **FR-027**: `Gantt theo ngày` MUST provide fixed reader-facing columns for
  item identity, description, state, primary owner, actual percentage or
  insufficient-data label, and latest official update before the timeline.
- **FR-028**: `30 ngày tới` MUST use an inclusive 30-date window beginning on
  the official source reporting date and ending 29 days later.
- **FR-029**: `30 ngày tới` MUST include unfinished overdue work and work whose
  planned interval intersects that window.
- **FR-030**: `30 ngày tới` MUST order overdue open work first, then nearest
  planned finish, then stable project order.
- **FR-031**: `30 ngày tới` MUST use the same Plan, Actual, forecast, progress,
  and missing-data semantics as `Gantt theo ngày`.
- **FR-032**: `Vấn đề cần xử lý` MUST retain the complete actionable list with
  action, impact, primary owner, and required-by condition.
- **FR-033**: `Chi tiết công việc` MUST contain one stable row per delivery card
  with description, phase, work package, planned start and finish, actual start
  and finish, actual effort, remaining effort, actual percentage or missing-
  data label, execution state, owner, latest official update, and short
  reference code.
- **FR-034**: Reader-facing states and missing values MUST use clear Vietnamese
  labels and MUST NOT expose raw technical validation codes on the first four
  sheets.
- **FR-035**: Blue MUST mean Plan, green evidence-backed Actual, amber official
  forecast or attention, red blocked/overdue, and gray missing or insufficient
  data; every meaning MUST also be available in text or symbols.
- **FR-036**: Primary headings, task descriptions, percentages, coverage, dates,
  and state labels MUST remain readable at normal zoom without clipping.
- **FR-037**: Timeline headers and fixed descriptive columns MUST remain visible
  while readers scroll through large daily schedules.
- **FR-038**: `Tổng quan` and `30 ngày tới` MUST support legible landscape
  printing; the full daily Gantt MAY paginate horizontally rather than shrink
  its daily columns into unreadable content.
- **FR-039**: Missing but validly sparse Actual evidence MUST not block export;
  contradictory or invalid official Actual values that cannot be represented
  truthfully MUST stop export with a specific diagnostic.
- **FR-040**: A delivery card whose recording state is not recorded MUST appear
  as `Chưa cập nhật`; it MUST NOT be reclassified as `Chưa bắt đầu`, zero
  progress, or a successful state.
- **FR-041**: The existing technical CARIO + Gantt workbook contract, download,
  and preview-import behavior MUST remain unchanged.
- **FR-042**: The management workbook MUST be generated within the existing
  restricted local environment without Office automation, network services,
  external APIs, new runtimes, or package installation.

### Key Entities

- **Executive Daily Gantt Report**: The single presentation-only management
  workbook generated from one official project snapshot.
- **Daily Gantt Item**: A project, phase, work package, delivery card, or
  milestone with reader-facing identity, hierarchy, dates, state, owner, and
  display eligibility.
- **Plan Lane**: The immutable baseline date interval for a Gantt item.
- **Actual Lane**: The evidence-backed actual date interval for a delivery card
  or explicitly labelled roll-up of known child activity.
- **Forecast Segment**: An official future interval from the reporting boundary
  to a recorded forecast finish; it is distinct from Actual.
- **Progress Evidence**: Actual effort, remaining effort, execution state,
  actual dates, last update, and their completeness for percentage and lane
  eligibility.
- **Coverage Indicator**: The count of delivery cards with sufficient evidence
  relative to all delivery cards in the relevant scope.
- **Near-Term Item**: An unfinished overdue item or an item whose planned
  interval intersects the exact 30-date operating window.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a timed review, a management reader can identify current
  phase, next milestone, recorded progress condition, evidence coverage, and
  the most urgent required decision from `Tổng quan` within 60 seconds.
- **SC-002**: In 100% of acceptance fixtures, the workbook contains exactly the
  five approved sheets in the approved order and opens on `Tổng quan`.
- **SC-003**: In 100% of completed, in-progress, unrecorded, forecast, and
  overdue fixtures, Plan and Actual lanes reconcile exactly to authoritative
  dates and no missing Actual is rendered as a green lane or zero percent.
- **SC-004**: In 100% of progress fixtures, displayed percentages equal the
  approved effort formula, and incomplete scope is accompanied by truthful
  coverage instead of a misleading whole-scope percentage.
- **SC-005**: The near-term view covers exactly 30 calendar dates and includes
  every qualifying overdue or intersecting item while excluding every
  non-qualifying item in boundary tests.
- **SC-006**: At normal zoom, all five sheets pass visual review with readable
  headings, unclipped primary content, visible fixed context, and distinguishable
  Plan, Actual, forecast, overdue, weekend, milestone, and reporting-date cues.
- **SC-007**: A supported official project report downloads within five seconds
  in the local acceptance environment.
- **SC-008**: Exporting the management workbook changes zero official snapshot,
  source-execution, proposal, preview, or source-repository values in all
  acceptance tests.
- **SC-009**: All existing technical workbook export and preview-import
  acceptance scenarios continue to pass without contract changes.
- **SC-010**: A manager can trace any headline Actual or issue to the relevant
  delivery-card detail within the same workbook without consulting a second
  report.

## Assumptions

- This feature supersedes the four-sheet presentation contract of Feature 005
  while retaining its existing management export action, official-only
  authority boundary, file naming, and technical-export separation.
- The report is a point-in-time snapshot; recipients regenerate it after a new
  official execution register is reviewed, committed, and imported.
- The official source reporting date anchors provenance and the 30-day window;
  the approved analysis date anchors open-ended in-progress Actual lanes.
- Delivery cards are the atomic source of Actual execution truth. Phase and
  work-package Actual summaries are derived presentations and always disclose
  coverage.
- Progress percentage represents effort evidence, while Gantt lane length
  represents calendar dates; the two measures remain visibly distinct.
- Source titles are cleaned mechanically for presentation but are not
  semantically rewritten by AI.
- Standard spreadsheet software and fonts are available to report recipients.
- Editing Actual values inside the exported workbook, importing the management
  workbook, PDF export, and direct source write-back are outside this feature.
