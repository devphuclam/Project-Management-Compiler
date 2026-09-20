# Feature Specification: Executive Progress Report Export

**Feature Branch**: `codex/feature005-executive-progress-report`

**Created**: 2026-09-20

**Status**: Approved for planning

**Input**: User description: "Export a separate, human-readable Excel progress
report for management from the official IDEAEngineering snapshot, while keeping
the existing CARIO + Gantt workbook as the technical/audit artifact."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Understand project status in one minute (Priority: P1)

As a project sponsor, I want to open a concise progress workbook and immediately
see where the project stands, what milestone comes next, whether schedule or
readiness needs attention, and what requires my decision so that I do not have
to interpret technical identifiers or raw export data.

**Why this priority**: The existing workbook is an audit and integration
artifact. A management report delivers value only when a non-technical reader
can understand the current situation quickly and without assistance.

**Independent Test**: Export the report from an official IDEAEngineering
snapshot, open it at normal zoom, and confirm that the first sheet answers the
five management questions within one minute: current phase, next milestone,
schedule condition, evidence-backed progress, and required decisions.

**Acceptance Scenarios**:

1. **Given** an official project snapshot, **when** the user exports the
   management report, **then** the workbook opens on `Tổng quan` and presents a
   Vietnamese title, reporting date, planning window, schedule condition,
   readiness condition, next milestone, recorded progress, and management
   attention items.
2. **Given** insufficient actual and remaining effort evidence, **when** the
   report is opened, **then** it says `Chưa đủ dữ liệu để tính % hoàn thành`
   and shows only supported work-item counts instead of presenting zero or an
   estimated percentage.
3. **Given** no management item requires attention, **when** the report is
   exported, **then** the first sheet says `Hiện chưa có nội dung cần xin ý
   kiến` instead of displaying an empty or successful-looking warning panel.
4. **Given** the reader views the first sheet at 100% zoom, **when** they scan
   the report, **then** headings, values, and action text are visible without
   clipped labels or unexplained technical status codes.

---

### User Story 2 - Review the management schedule and action list (Priority: P2)

As a project manager, I want the report to separate the executive timeline,
work-package schedule, management actions, and delivery-card detail so that I
can answer follow-up questions without overwhelming the sponsor.

**Why this priority**: Management readers need progressive disclosure. Phase
and milestone information belongs in the initial view; work packages and
delivery cards remain available for traceability but must not dominate it.

**Independent Test**: Review all four report sheets and confirm that the
overview stops at phases and milestones, the schedule stops at work packages,
the action list contains human-readable management items, and delivery-card
identifiers appear only in the final detail sheet.

**Acceptance Scenarios**:

1. **Given** a project with phases, milestones, work packages, and delivery
   cards, **when** the report is exported, **then** `Tổng quan` shows only the
   phase-level timeline and key milestones, `Lịch trình` shows work packages,
   and `Chi tiết công việc` contains delivery-card detail.
2. **Given** more than five actionable management items, **when** the report is
   exported, **then** `Tổng quan` shows at most five items ordered by the
   approved management priority and `Vấn đề cần xử lý` retains the complete
   actionable list.
3. **Given** source labels such as `` `[PH2][C01-A] Tạo loại tài liệu` ``,
   **when** they are presented in the management report, **then** mechanical
   prefixes, backticks, and redundant identifiers are removed while the source
   meaning remains unchanged.
4. **Given** source role codes or missing ownership, **when** ownership is
   displayed, **then** the report uses an approved Vietnamese role label or
   `Chưa xác định đầu mối`; it does not show a comma-separated code list.
5. **Given** the user prints the first two sheets in landscape orientation,
   **when** standard page scaling is used, **then** the management content is
   legible and the primary schedule area fits the intended page width.

---

### User Story 3 - Preserve authority and the technical export contract (Priority: P3)

As a project owner, I want the management workbook to be a separate,
presentation-only output so that clearer wording cannot alter official facts,
replace the CARIO/audit workbook, or become a new source of project truth.

**Why this priority**: The compiler must preserve the boundary between official
source evidence, derived analysis, local proposals, and presentation outputs.
Improving readability must not weaken that boundary.

**Independent Test**: Export both workbook types from the same official
snapshot, compare their source-backed values, run the existing CARIO workbook
contract and preview-import regressions, and attempt to use the executive
workbook as an import; confirm that the technical contract remains unchanged
and the executive file is not accepted as source or preview input.

**Acceptance Scenarios**:

1. **Given** an official snapshot, **when** both exports are requested, **then**
   the management workbook and CARIO + Gantt workbook are separate files with
   separate user-facing controls and names.
2. **Given** the management workbook is exported, **when** its contents are
   inspected, **then** every status, date, count, milestone, issue, and progress
   statement reconciles to official source or approved deterministic analysis.
3. **Given** an XLSX preview or other non-authoritative state, **when** the user
   requests the management report, **then** the system does not represent that
   state as the official management report.
4. **Given** an executive workbook, **when** a user attempts to import it using
   the workbook preview flow, **then** it does not satisfy the technical CARIO
   workbook contract and cannot affect official state or proposals.
5. **Given** the feature is installed, **when** the existing technical workbook
   is exported and previewed, **then** its seven-sheet contract, provenance
   markers, and current import behavior remain unchanged.

---

### Edge Cases

- No official snapshot is loaded when the user requests the management report.
- The reporting date precedes the planning start or follows the planning
  finish.
- Actual effort is known but remaining effort is missing, negative, or invalid;
  the inverse case also occurs.
- A project has no completed work but has one or more in-progress records.
- A project has no actionable management items, exactly five items, or more
  than five items at the same priority and due condition.
- A project contains duplicate raw identifiers across different entity kinds,
  such as a work package and delivery card both named `P04`.
- A source title contains backticks, bracketed identity prefixes, Vietnamese
  diacritics, a long sentence, or text that still exceeds the presentation
  width after mechanical cleanup.
- An owner has a known person, a known organizational role, several technical
  role codes, or no attributable owner.
- No next milestone can be identified from the official schedule.
- Schedule analysis has no active alert while readiness evidence still contains
  open decisions or blocking actions.
- A technical warning has no management consequence and therefore must not be
  promoted to the executive attention list.
- The workbook is opened or printed on a machine with standard spreadsheet
  fonts but without project-specific fonts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a distinct `Xuất báo cáo tiến độ` action
  for an official project snapshot.
- **FR-002**: The existing technical export MUST remain available through a
  separately labelled `Xuất dữ liệu CARIO + Gantt` action.
- **FR-003**: The management report MUST be derived only from the current
  official snapshot and its approved deterministic analysis; it MUST NOT use an
  XLSX preview or another non-authoritative presentation state as official
  input.
- **FR-004**: The management report file name MUST follow
  `<Project>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`, using a filesystem-safe project
  name and the source reporting date.
- **FR-005**: The workbook MUST contain exactly four reader-facing sheets in
  this order: `Tổng quan`, `Lịch trình`, `Vấn đề cần xử lý`, and `Chi tiết công
  việc`.
- **FR-006**: The workbook MUST open on `Tổng quan` and show the report title,
  source reporting date, and baseline planning window before any detailed table.
- **FR-007**: `Tổng quan` MUST present schedule condition and implementation
  readiness as separate states; absence of a schedule alert MUST NOT imply that
  readiness is complete.
- **FR-008**: Management narrative MUST use deterministic, factual sentence
  templates. It MUST NOT use generated prose, promotional language, confidence
  claims, or inferred causes.
- **FR-009**: A recorded completion percentage MUST be shown only when actual
  effort and remaining effort are both finite, non-negative, and their sum is
  greater than zero. Otherwise the report MUST say `Chưa đủ dữ liệu để tính %
  hoàn thành` and MAY show evidence-backed state counts.
- **FR-010**: `Tổng quan` MUST contain no more than four summary blocks:
  `Tình trạng lịch trình`, `Mốc sắp tới`, `Tiến độ được ghi nhận`, and `Việc cần
  quyết định`.
- **FR-011**: `Tổng quan` MUST show a phase-and-milestone timeline grouped by
  month with weekly detail and a visible `Ngày báo cáo` marker.
- **FR-012**: `Tổng quan` MUST show at most five management attention items,
  ordered by: blocked work, overdue work, decisions required before the nearest
  milestone, at-risk work, and important work without an owner; ties MUST use
  the nearest attributable due condition first and a stable source order last.
- **FR-013**: `Lịch trình` MUST show the work-package schedule without expanding
  delivery cards into the primary schedule and without presenting a daily
  column for every calendar day.
- **FR-014**: `Vấn đề cần xử lý` MUST contain the complete actionable list with
  the reader-facing fields `Việc cần xử lý`, `Ảnh hưởng`, `Đầu mối`, and `Cần
  xong trước`.
- **FR-015**: Technical warnings without a supported management consequence
  MUST remain out of the executive attention list.
- **FR-016**: `Chi tiết công việc` MUST provide delivery-card detail for
  traceability and place its short `Mã tham chiếu` field after reader-facing
  description, schedule, owner, and status fields.
- **FR-017**: The first three sheets MUST NOT display raw phase, work-package,
  delivery-card, decision, or action identifiers as primary labels. Identifiers
  MAY appear only in the final detail sheet's reference field.
- **FR-018**: Reader-facing labels MUST remove bracketed identity prefixes,
  markdown backticks, and redundant leading identifiers mechanically while
  preserving the remaining source text and meaning.
- **FR-019**: Reader-facing execution and management states MUST use the
  approved Vietnamese terms: `Chưa cập nhật`, `Chưa bắt đầu`, `Đang thực hiện`,
  `Hoàn thành`, `Tạm dừng`, `Đã hủy`, `Chưa chốt`, `Chưa đánh giá`, and `Bị
  chặn`, as applicable.
- **FR-020**: Source data-state and validation terms such as `KNOWN`,
  `CALCULATED`, `MATCHED`, and `UNRESOLVED_IDENTITY` MUST NOT appear in the
  reader-facing report.
- **FR-021**: Known owner identities and organizational roles MUST use approved
  reader-facing names. Missing ownership MUST display `Chưa xác định đầu mối`;
  the report MUST NOT concatenate raw technical role codes in management views.
- **FR-022**: Color MUST reinforce, not replace, text and MUST use the approved
  meanings: blue for plan, green for evidence-backed completion, amber for
  attention, red for blocked or overdue, and gray for missing or insufficient
  data.
- **FR-023**: The first three sheets MUST NOT contain commit hashes, snapshot
  identifiers, manifest paths, long source references, validation codes, or raw
  import diagnostics.
- **FR-024**: `Tổng quan` MUST contain one compact provenance line stating the
  source project and the date through which source data is current.
- **FR-025**: The first two sheets MUST be readable at 100% zoom, use visible
  unclipped headings, retain useful row/column context while scrolling, and
  support landscape printing at a legible scale.
- **FR-026**: Unknown, missing, ambiguous, and not-run values MUST remain
  explicit as `Chưa cập nhật`, `Chưa đủ dữ liệu`, `Chưa đánh giá`, or another
  approved truthful label; they MUST NOT become zero, success, green status, or
  an estimated forecast.
- **FR-027**: The management report MUST be presentation-only and MUST NOT
  satisfy the CARIO + Gantt preview-import contract or become canonical,
  execution, proposal, or source input.
- **FR-028**: Producing either workbook MUST NOT modify the official snapshot,
  canonical project, source execution, local proposals, or source repository.
- **FR-029**: The existing CARIO + Gantt workbook's seven-sheet contract,
  provenance markers, workbook preview behavior, and official/preview export
  selection MUST remain unchanged.
- **FR-030**: The feature MUST work in the existing restricted local
  environment without requiring a database, Office automation, network
  service, connector, or newly installed runtime/package.

### Key Entities

- **Executive Progress Report**: A presentation-only workbook generated from
  one official snapshot, containing four Vietnamese reader-facing sheets and a
  source reporting date.
- **Executive Summary**: The factual first-sheet projection containing schedule
  condition, readiness condition, next milestone, recorded progress, and at
  most five management attention items.
- **Executive Schedule Row**: A phase, milestone, or work-package presentation
  row with a cleaned reader-facing name, plan dates, truthful state, ownership,
  and display semantics appropriate to its sheet.
- **Management Attention Item**: An official or deterministically derived item
  with a human-readable action, consequence, attributable owner, due condition,
  source category, and stable priority.
- **Display Terminology**: The approved deterministic mappings from source
  states, role codes, and mechanically decorated labels to Vietnamese
  reader-facing text.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In a timed acceptance review, a management reader can identify the
  current project phase, next milestone, schedule condition, progress evidence
  condition, and required decision items from `Tổng quan` within 60 seconds.
- **SC-002**: In 100% of official fixture exports, the first three sheets contain
  zero raw technical status terms, long source references, commit hashes,
  snapshot identifiers, or primary task labels consisting only of internal
  identifiers.
- **SC-003**: In 100% of missing, unknown, ambiguous, and not-run fixtures, the
  report uses an approved explicit unknown label and never substitutes zero,
  completion, green status, or a calculated forecast.
- **SC-004**: Every displayed project date, milestone, count, status, owner,
  attention item, and percentage reconciles exactly to the official snapshot or
  approved deterministic analysis for all acceptance fixtures.
- **SC-005**: The generated workbook contains exactly four sheets in the
  approved order, opens on `Tổng quan`, and its first two sheets pass visual
  review at 100% zoom and landscape print-preview without clipped primary
  content.
- **SC-006**: All existing technical workbook export and preview-import
  acceptance scenarios continue to pass with byte/semantic contract behavior
  unchanged except where pre-existing nondeterministic package metadata is
  already excluded from comparison.
- **SC-007**: A supported official project report is available for download
  within five seconds of the user's export action in the local application.

## Assumptions

- The report audience is a project sponsor or manager who needs a concise
  Vietnamese status report and may ask the project manager to trace details.
- The official snapshot remains the sole authority for the management report;
  local proposals and XLSX previews do not qualify as official report input.
- Source titles are mechanically cleaned but not semantically rewritten or
  summarized by AI.
- Existing planning and execution semantics, including the separation of
  baseline, source execution, readiness evidence, and derived alerts, remain
  unchanged.
- The report is a point-in-time file. It does not refresh after download; users
  re-import an updated official snapshot and export a new dated report.
- Standard spreadsheet software and fonts are available to the recipient.
- PDF and presentation-deck exports are outside this feature.
- The existing CARIO + Gantt workbook remains the technical and audit artifact;
  this feature does not redesign or replace it.
