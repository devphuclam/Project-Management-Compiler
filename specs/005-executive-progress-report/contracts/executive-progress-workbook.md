# Contract: Executive Progress Workbook

## Purpose and authority

The workbook is a presentation-only management report generated from one
official IDEAEngineering manifest snapshot. It is not a source, editable input,
execution record, proposal, or CARIO preview. Export is read-only with respect
to the application state and source repository.

## Package contract

- Format: Office Open XML `.xlsx` package produced with existing .NET BCL APIs.
- Total worksheet count: exactly four; no hidden metadata worksheet.
- Worksheet order and exact names:
  1. `Tổng quan`
  2. `Lịch trình`
  3. `Vấn đề cần xử lý`
  4. `Chi tiết công việc`
- Active worksheet on open: `Tổng quan`.
- `PMC_EXPORT_KIND`, `PMC_EXPORT_CONTRACT_VERSION`, and other CARIO preview
  markers are absent.
- The package must not satisfy the seven-sheet CARIO workbook importer.
- Core document metadata may identify the generating application but cannot be
  used as project authority.

## Common presentation contract

- Reader-facing content is Vietnamese and uses standard spreadsheet fonts.
- Source labels are mechanically cleaned; source meaning is not rewritten.
- Missing, unknown, ambiguous, and not-run values use explicit approved text.
- Color never replaces a text label:
  - blue: plan/baseline;
  - green: evidence-backed completion;
  - amber: attention;
  - red: blocked or overdue;
  - gray: missing or insufficient data.
- Raw data-state/validation codes, commit hashes, snapshot IDs, manifest paths,
  long source references, and diagnostics do not appear on the first three
  sheets.
- Long text wraps and row height accommodates it; primary text is not clipped
  or semantically truncated.
- Gridlines are hidden and zoom is set to 100% on all sheets.

## Sheet 1: `Tổng quan`

The sheet opens with:

1. report title and project name;
2. source reporting date and baseline planning window;
3. compact provenance line: source project and date through which source data
   is current; if analysis uses a different date, that date is also stated;
4. no more than four summary blocks, in this order:
   - `Tình trạng lịch trình` — includes current phase and schedule condition;
   - `Mốc sắp tới` — next milestone name/date or explicit missing value;
   - `Tiến độ được ghi nhận` — valid percentage or exact insufficient-data
     statement plus supported state counts;
   - `Việc cần quyết định` — readiness condition and actionable count, without
     collapsing readiness into schedule health;
5. phase-and-milestone timeline;
6. zero-to-five ranked management attention items.

If there is no actionable item, the attention area says exactly
`Hiện chưa có nội dung cần xin ý kiến`.

### Overview timeline

- Rows: phases and canonical `MilestoneKind.Milestone` entries only; a
  `MilestoneKind.Decision` remains an action/decision concern and never appears
  as a timeline milestone. No work-package or delivery-card rows appear.
- Fixed label area: reader-facing name, plan dates, and state.
- Time axis: month group headers with ISO week detail; not one column per day.
- Date range: official planning bounds extended in either direction as needed
  to include the source reporting date and supported timeline rows, including
  when the reporting date precedes planning start or follows planning finish.
- `Ngày báo cáo` is visibly marked at the week containing the source reporting
  date and is text-labelled so meaning does not depend only on color.
- Current phase and next milestone are text-identifiable.

## Sheet 2: `Lịch trình`

- One row per work package; delivery cards are not expanded here.
- Reader-facing fixed columns, in order:
  `Giai đoạn`, `Gói công việc`, `Bắt đầu kế hoạch`, `Kết thúc kế hoạch`,
  `Tình trạng`, `Đầu mối`.
- No raw phase/work-package ID column.
- Timeline uses the same month/week axis and source reporting-date marker as
  the overview.
- Rows use official plan dates and the conservative state/accountable-owner
  derivation in `data-model.md`; readiness state never substitutes for work-
  package execution state.

## Sheet 3: `Vấn đề cần xử lý`

- Contains every actionable item from the executive projection, including
  valid open decisions and pending human actions that are not eligible for the
  overview's first five.
- Required columns, in order:
  `Việc cần xử lý`, `Ảnh hưởng`, `Đầu mối`, `Cần xong trước`.
- Row order exactly matches deterministic management priority: blocked,
  overdue, decision before nearest milestone, at-risk, missing owner, other
  open decision, then pending human action; ties use known earliest due date,
  source order, and stable identity.
- No raw evidence/decision/action identifier or diagnostic code is shown.
- Empty state says `Hiện chưa có nội dung cần xin ý kiến`.

## Sheet 4: `Chi tiết công việc`

- One row per delivery card in stable project order.
- Columns, in order:
  `Công việc`, `Giai đoạn`, `Gói công việc`, `Bắt đầu kế hoạch`,
  `Kết thúc kế hoạch`, `Đầu mối`, `Tình trạng`, `Mã tham chiếu`.
- `Mã tham chiếu` is the only intended raw work-item identity in the workbook
  and appears after all reader-facing fields.
- Technical source references, diagnostics, and internal data-state codes remain
  absent.

## View and print contract

- `Tổng quan`: useful title/summary rows and fixed timeline labels remain visible
  while scrolling; landscape, fit to one page wide, unlimited page height.
- `Lịch trình`: header row and fixed descriptive columns remain visible while
  scrolling; landscape, fit to one page wide, unlimited page height.
- `Vấn đề cần xử lý` and `Chi tiết công việc`: table header remains visible;
  print settings favor legible width without forcing unreadably small text.
- First two sheets must be readable at 100% zoom and pass print-preview review
  without clipped primary headings or action text.

## Compatibility assertions

For the same official snapshot:

- source-backed dates, states, counts, owners, milestones, and effort values in
  this workbook reconcile to canonical/analysis values;
- exporting this workbook leaves semantic digest, official state, source
  execution, proposals, and source files unchanged;
- `CarioXlsxExporter` still emits the existing seven sheets, headers, and
  preview markers;
- `XlsxPreviewImporter` rejects this workbook as an unsupported contract and
  does not replace an existing preview or official state.
