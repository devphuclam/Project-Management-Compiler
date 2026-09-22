# Feature Specification: Management Report Readability

**Feature Branch**: `codex/feature007-management-report-readability`

**Created**: 2026-09-22

**Status**: Approved — implementation authorized 2026-09-22

**Input**: User description: "Replace the current executive progress export with a clean, concise, logically organized six-sheet management report that executives can understand, including a complete WBS and truthful Actual evidence, while preserving a separate future direction for a round-trip Project Workbook."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Understand the project quickly (Priority: P1)

As a sponsor or manager, I want the exported progress report to lead with the
project's current position, supported progress, next milestone, and decisions
that need attention so that I can understand the situation without decoding
technical identifiers or source-system language.

**Why this priority**: The report succeeds only if its intended reader can make
a management decision from it. A technically correct workbook that first
requires explanation is not a usable management report.

**Independent Test**: Export a report from the accepted official fixture, give
only the workbook to a management reader, and verify that the reader can state
the current phase, supported progress, next milestone, and highest-priority
decision from `Tổng quan` within 60 seconds.

**Acceptance Scenarios**:

1. **Given** a complete official snapshot, **when** the reader opens the
   exported workbook, **then** `Tổng quan` is active and presents the project
   position before supporting schedule and detail.
2. **Given** project facts and management attention items, **when** `Tổng quan`
   is read, **then** it answers current position, supported progress, plan
   change, next milestone, and required decisions in that order.
3. **Given** more than five attention items, **when** `Tổng quan` is produced,
   **then** only the five highest-priority actions appear there and the complete
   relevant population remains available in `Điều hành 30 ngày` or supporting
   detail.
4. **Given** source names containing IDs, prefixes, Markdown markers, or raw
   state tokens, **when** a reader-facing label is produced, **then** it shows a
   concise meaningful name without altering the source fact.
5. **Given** no action requiring management attention, **when** the report is
   opened, **then** the summary uses a concise empty-state message rather than
   an empty panel or invented narrative.

---

### User Story 2 - Compare plan with recorded execution (Priority: P2)

As a project manager, I want planned and recorded Actual evidence shown together
on a daily Gantt so that I can see timing variance and evidence gaps without the
report inventing dates or treating missing evidence as zero progress.

**Why this priority**: Schedule comparison is the report's primary analytical
view, but its value depends on preserving the boundary between plan, recorded
execution, forecast, and missing evidence.

**Independent Test**: Export a report from a fixture containing complete Actual
dates, start-only evidence, finish-only evidence, effort-only evidence, and no
execution evidence. Verify that each shape uses the approved interval, marker,
or empty state and that no missing date is inferred.

**Acceptance Scenarios**:

1. **Given** an item with Actual start and Actual finish, **when** `Gantt` is
   viewed, **then** its Actual lane shows the recorded interval in green.
2. **Given** an in-progress item with Actual start but no Actual finish, **when**
   `Gantt` is viewed, **then** its Actual lane remains open through the analysis
   date without implying a finish.
3. **Given** an item with Actual finish but no Actual start, **when** `Gantt` is
   viewed, **then** a `✓` appears on the recorded finish date and no duration is
   fabricated.
4. **Given** effort evidence without any Actual date, **when** `Gantt` is
   viewed, **then** a `●` and `Có ghi nhận` communicate evidence without placing
   it on an invented date.
5. **Given** no execution evidence, **when** `Gantt` is viewed, **then** the
   Actual timeline remains empty and the row says `Chưa ghi nhận`.
6. **Given** valid non-negative Actual and remaining effort with a positive
   total, **when** progress is shown, **then** it equals Actual effort divided by
   Actual plus remaining effort; otherwise no percentage is fabricated.

---

### User Story 3 - Explore the full scope hierarchy (Priority: P2)

As a project manager, I want a complete WBS that opens at a useful level and can
expand to every delivery card so that I can review the project's decomposition
without scanning identifier-heavy technical rows.

**Why this priority**: A readable schedule does not replace scope structure.
The WBS must make parentage, ownership, state, and attention visible while
keeping detailed dates, relationships, and evidence available on demand.

**Independent Test**: Export the accepted fixture and reconcile every Project,
Phase, Work Package, and Delivery Card against the canonical hierarchy. Verify
that each appears once, the workbook opens through Work Package depth, and all
Delivery Cards are reachable by expanding row groups.

**Acceptance Scenarios**:

1. **Given** a valid four-level project hierarchy, **when** `WBS` is opened,
   **then** it shows Project → Phase → Work Package → Delivery Card in stable
   order with one row per element.
2. **Given** the initial workbook state, **when** `WBS` is opened, **then**
   Project, Phase, and Work Package rows are visible while Delivery Card rows
   are available through expansion.
3. **Given** a WBS row, **when** its primary columns are read, **then** they are
   exactly `WBS`, `Mã`, `Hạng mục`, `Loại`, `Đầu mối`, `Trạng thái`, `% thực
   tế`, and `Cần chú ý` in that order.
4. **Given** a row whose source title repeats its ID or hierarchy prefix,
   **when** `Hạng mục` is produced, **then** it contains only the meaningful
   reader-facing name and the stable identity appears once in `Mã`.
5. **Given** optional planning, Actual, relationship, and evidence detail,
   **when** the reader opens `WBS`, **then** those columns are present in
   collapsible groups and do not obscure the primary columns.
6. **Given** a milestone or decision gate, **when** WBS membership is evaluated,
   **then** it remains in schedule and operating views but is not represented as
   a WBS element.

---

### User Story 4 - Run the next 30 days (Priority: P3)

As a delivery lead, I want one operating sheet that brings together decisions,
blockers, overdue work, active work, and near-term planned work so that I can
run the next 30 days without reconciling separate issue and schedule sheets.

**Why this priority**: This view converts report facts into an ordered operating
agenda while avoiding a duplicated full Kanban board in the management report.

**Independent Test**: Use a fixture with decisions, blockers, overdue open
work, active work, milestones, and planned items before, inside, crossing, and
after the reporting window. Verify inclusion, category order, dates, and empty
states against the official snapshot.

**Acceptance Scenarios**:

1. **Given** an official reporting date, **when** `Điều hành 30 ngày` is
   produced, **then** its operating window covers that date through 29 calendar
   days later.
2. **Given** multiple qualifying items, **when** the sheet is read top to bottom,
   **then** decisions and blockers appear first, overdue unfinished work second,
   active work third, and intersecting planned work and milestones last.
3. **Given** a qualifying action, **when** its row is read, **then** the action,
   consequence, owner, required date, state, and minimum schedule context are
   available without raw diagnostic codes.
4. **Given** no qualifying work, **when** the sheet is produced, **then** it
   contains a concise explicit empty state and retains its headings.

---

### User Story 5 - Trace facts without cluttering the report (Priority: P3)

As a reviewer, I want work-level detail and report authority available in
dedicated places so that I can trace a management statement without forcing
every reader-facing sheet to repeat commit hashes, paths, snapshot IDs, or
methodology prose.

**Why this priority**: Traceability remains mandatory, but concentrating it in
the correct supporting views makes the report both auditable and readable.

**Independent Test**: Trace a delivery-card statement from the summary or
Gantt to `Chi tiết công việc`, then inspect `Thông tin báo cáo` for authority
and snapshot metadata. Verify that the first five sheets contain no raw
validation codes or repeated provenance boilerplate.

**Acceptance Scenarios**:

1. **Given** an official snapshot with delivery cards, **when** `Chi tiết công
   việc` is opened, **then** it contains one stable row per Delivery Card with
   reader-facing fields before technical identity and provenance fields.
2. **Given** report authority and source metadata, **when** `Thông tin báo cáo`
   is opened, **then** it contains reporting date, analysis date, authority,
   source commit, snapshot identity, baseline, contract version, and concise
   limitations in one place.
3. **Given** an official snapshot and an active local preview or proposal,
   **when** the progress report is exported, **then** only official facts appear
   and the preview or proposal remains unchanged.
4. **Given** the management report, **when** it is offered to any existing
   workbook import flow, **then** it is not accepted as a project authority or
   tracking source.

### Edge Cases

- A source title may contain repeated IDs, nested bracket prefixes, hierarchy
  arrows, Markdown decoration, entity-kind labels, or a meaningful title that
  begins with text resembling an ID; cleanup must remove only recognized noise.
- Reader-facing names may be long Vietnamese text; they must wrap without
  semantic truncation or identifier reintroduction.
- Actual evidence may be complete, start-only, finish-only, effort-only,
  missing, stale, or contradictory; every valid sparse shape remains visible
  and every unsafe contradiction fails closed.
- Actual and remaining effort may be missing, non-finite, negative, or total
  zero; none of these cases may become a fabricated percentage or numeric zero.
- A project may have no management actions, no next milestone, no qualifying
  30-day work, no owner, or no recorded Actual evidence; each view retains a
  concise explicit empty or missing state.
- Planned or Actual intervals may begin before or end after the visible Gantt
  window; continuation must be represented without moving an endpoint.
- An unfinished item may already be overdue while its complete planned interval
  lies before the 30-day axis; it remains actionable without fabricating an
  in-window plan bar.
- A hierarchy element may be orphaned or unresolved; it remains visible with
  explicit unknown-parent context rather than being silently dropped.
- The official snapshot may be absent or incomplete while another preview is
  active; export must not fall back to that preview.
- The workbook may span many daily columns or many detail rows; readability and
  frozen context take precedence over forcing every sheet onto one printed page.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The existing `Xuất báo cáo tiến độ` action MUST export one
  presentation-only Management Report from the current official snapshot.
- **FR-002**: The Management Report MUST contain exactly these sheets in this
  order: `Tổng quan`, `Điều hành 30 ngày`, `Gantt`, `WBS`, `Chi tiết công việc`,
  and `Thông tin báo cáo`.
- **FR-003**: The workbook MUST open on `Tổng quan`.
- **FR-004**: `Tổng quan` MUST answer current position, supported progress,
  change from plan, next milestone, and required decision in that order, and
  MUST show at most five priority actions.
- **FR-005**: `Điều hành 30 ngày` MUST cover the official reporting date through
  29 calendar days later and MUST order decisions/blockers, overdue unfinished
  work, active work, and intersecting planned work/milestones in that sequence.
- **FR-006**: Each operating row MUST state the action, consequence, owner,
  required date, state, and only the schedule context needed to act.
- **FR-007**: `Gantt` MUST include the complete Project, Phase, Work Package,
  Delivery Card, and milestone schedule on a daily calendar.
- **FR-008**: Each scheduled work item MUST show its reader-facing name once and
  adjacent `Kế hoạch` and `Thực tế` lanes where those lanes apply.
- **FR-009**: Actual start plus Actual finish MUST appear as a green recorded
  interval, and Actual start without finish for in-progress work MUST appear as
  an open recorded interval through the analysis date.
- **FR-010**: Actual finish without Actual start MUST appear as a `✓` completion
  point on the recorded finish date and MUST NOT be represented as a one-day
  duration.
- **FR-011**: Effort evidence without an Actual date MUST appear as a `●` with
  `Có ghi nhận`, while no execution evidence MUST leave the Actual timeline
  empty and use `Chưa ghi nhận`.
- **FR-012**: The report MUST NOT infer Actual start, Actual finish, effort,
  progress, owner, forecast, or causality from planned dates, update timestamps,
  state labels, or other unrelated evidence.
- **FR-013**: A progress percentage MUST be shown only when Actual and remaining
  effort are finite, non-negative, and have a positive sum; the value MUST be
  Actual effort divided by Actual plus remaining effort.
- **FR-014**: `WBS` MUST contain every Project, Phase, Work Package, and Delivery
  Card exactly once in canonical stable order, and MUST exclude milestones and
  decision gates from WBS membership.
- **FR-015**: WBS numbering and indentation MUST communicate the Project → Phase
  → Work Package → Delivery Card hierarchy independently of dates or execution
  fields.
- **FR-016**: `WBS` MUST open with Project, Phase, and Work Package rows visible,
  while every Delivery Card remains available through expandable row groups.
- **FR-017**: The visible WBS columns MUST be `WBS`, `Mã`, `Hạng mục`, `Loại`,
  `Đầu mối`, `Trạng thái`, `% thực tế`, and `Cần chú ý`, in that order.
- **FR-018**: WBS planning, Actual, relationship, and evidence details MUST be
  present in separately collapsible column groups.
- **FR-019**: `Hạng mục` and other reader-facing name fields MUST contain only
  the meaningful Reader-Facing Name and MUST NOT repeat their row's `Mã`,
  recognized hierarchy prefixes, kind labels, arrows, or Markdown decoration.
- **FR-020**: `Chi tiết công việc` MUST contain one stable row per Delivery Card,
  place reader-facing fields before technical identity/provenance, support
  filtering, and keep identity context visible while scrolling.
- **FR-021**: `Thông tin báo cáo` MUST centralize report authority, reporting and
  analysis dates, source commit, snapshot identity, baseline, contract version,
  and concise limitations.
- **FR-022**: The first five sheets MUST NOT expose raw enum values, validation
  codes, repeated provenance boilerplate, absolute paths, or AI-generated
  explanatory narrative.
- **FR-023**: Missing owners MUST use `Chưa phân công`; missing execution
  evidence MUST use `Chưa ghi nhận`; recency labels MUST use concise date-based
  wording rather than repeated provenance sentences.
- **FR-024**: Plan, recorded Actual, supported forecast/attention, blocked or
  overdue conditions, and missing evidence MUST use the approved blue, green,
  amber, red, and gray meanings, with text or symbols so color is never the sole
  carrier of meaning.
- **FR-025**: `Tổng quan` and `Điều hành 30 ngày` MUST target readable landscape
  printing; `Gantt`, `WBS`, and `Chi tiết công việc` MUST prioritize normal-zoom
  screen readability with frozen context and without clipped primary content.
- **FR-026**: Export MUST be deterministic for the same official facts and MUST
  leave the canonical project, official execution, proposals, previews, source
  repository, and application state unchanged.
- **FR-027**: The Management Report MUST remain non-importable and MUST NOT
  change the separate technical `Xuất dữ liệu CARIO + Gantt` export or its
  existing preview/import contract.
- **FR-028**: If no complete official snapshot is available, or official facts
  contain a contradiction that cannot be represented truthfully, export MUST
  fail with a clear structured error instead of using preview data or guessing.

### Scope Boundaries

**In scope**:

- The reader-facing workbook downloaded by `Xuất báo cáo tiến độ`.
- The six-sheet reader journey, deterministic wording cleanup, truthful Actual
  representation, WBS hierarchy, visual hierarchy, and print/screen behavior.
- Regression protection for authority, non-importability, and the separate
  technical workbook contract.

**Out of scope**:

- The future guided Project Setup Workspace for unconfigured projects.
- Selecting or changing a project's single Tracking Source.
- A versioned round-trip Project Workbook or importing the Management Report.
- A full Kanban sheet in the Management Report.
- Changes to the technical CARIO + Gantt workbook or source write-back.
- AI/LLM rewriting, inferred management commentary, or invented source facts.

### Key Entities

- **Management Report**: A presentation-only workbook compiled from one official
  snapshot for sponsor and manager review; it is never an import authority.
- **Report Sheet**: One of the six ordered reader views, each with a defined
  management question, content population, and print or screen purpose.
- **Reader-Facing Name**: A deterministic presentation of a source title with
  recognized identity and formatting noise removed while preserving meaning.
- **WBS Node**: A Project, Phase, Work Package, or Delivery Card with stable
  identity, parentage, order, WBS number, depth, and optional supporting detail.
- **Actual Evidence Shape**: The supported combination of recorded Actual dates
  and effort that determines whether the report shows an interval, open
  interval, completion point, evidence marker, or missing state.
- **Operating Item**: A decision, blocker, overdue item, active item, planned
  item, or milestone that qualifies for the 30-day management agenda.
- **Report Metadata**: The authority, dates, source commit, snapshot identity,
  baseline, contract version, and limitations needed to audit the report.
- **Project Workbook**: A future, separate, versioned round-trip artifact; it is
  not delivered or accepted as input by this feature.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A management reader can identify the current phase, supported
  progress, next milestone, and top required decision from `Tổng quan` within
  60 seconds without external explanation.
- **SC-002**: Every exported Management Report contains exactly the six approved
  sheets in the approved order and opens on `Tổng quan`.
- **SC-003**: Across the accepted fixtures, 100% of reader-facing `Hạng mục`
  values omit repeated row IDs and recognized source hierarchy or formatting
  noise while preserving the meaningful title.
- **SC-004**: Reconciliation against the canonical hierarchy finds 100% of
  Projects, Phases, Work Packages, and Delivery Cards exactly once in WBS, with
  correct parentage, order, depth, and default outline visibility.
- **SC-005**: A fixture covering all five supported Actual evidence shapes
  produces the approved interval or marker for every row and produces zero
  inferred Actual dates.
- **SC-006**: Automated content inspection finds zero raw enum values,
  validation codes, absolute paths, or repeated provenance boilerplate on the
  first five sheets.
- **SC-007**: At normal zoom, all six sheets retain unclipped primary headings
  and identity context; the first two sheets also pass landscape print preview
  review without unreadably compressed core content.
- **SC-008**: Exporting the report changes zero official snapshot, proposal,
  preview, source-repository, or application-state values.
- **SC-009**: The existing technical CARIO + Gantt workbook and preview/import
  acceptance suite remains unchanged and passes in full.
- **SC-010**: For the accepted IDEAEngineering fixture of 6 phases, 35 work
  packages, 53 delivery cards, and 7 control points, the local export completes
  within five seconds.
- **SC-011**: Repeating an export from identical official facts produces the
  same workbook content apart from no uncontrolled environment metadata.

## Assumptions

- The current official snapshot remains the sole authority for report facts.
- The existing `Xuất báo cáo tiến độ` action and download behavior remain the
  user entry point; this feature changes its workbook contract, not its purpose.
- The report's reader-facing language is Vietnamese, while stable project IDs
  remain available only where identity or traceability requires them.
- Existing canonical hierarchy, schedule, Actual evidence, attention, and
  provenance facts are sufficient inputs; this feature does not expand source
  discovery or mutate source material.
- The accepted IDEAEngineering fixture represents the initial scale target;
  larger projects may paginate horizontally or vertically rather than sacrifice
  readability.
- The approved design and ADR separating Management Report from Project
  Workbook are authoritative for this feature.
- Implementation will follow the constitution's red-green-refactor workflow
  and must preserve all existing technical workbook and authority tests.
