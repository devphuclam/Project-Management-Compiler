# Contract: Executive Daily Gantt Workbook

## Purpose and authority

The workbook is a point-in-time management presentation generated from one
official IDEAEngineering snapshot and approved deterministic analysis. It is
not a source, editable execution register, proposal, canonical file, technical
CARIO workbook, or preview-import input.

This contract supersedes the Feature 005 four-sheet presentation contract. It
does not change the technical CARIO + Gantt workbook contract.

## Package contract

- Format: Office Open XML `.xlsx`.
- Worksheet count: exactly five; no hidden metadata sheet.
- Worksheet order and exact names:
  1. `Tổng quan`
  2. `Gantt theo ngày`
  3. `30 ngày tới`
  4. `Vấn đề cần xử lý`
  5. `Chi tiết công việc`
- Active worksheet: `Tổng quan`.
- Gridlines hidden and zoom set to 100% on all sheets.
- No CARIO preview marker such as `PMC_EXPORT_KIND` or
  `PMC_EXPORT_CONTRACT_VERSION` is present.
- ZIP entry timestamps and generated package ordering are deterministic for
  the same report model.

## Common wording and semantics

- Reader-facing language is Vietnamese.
- Missing/unrecorded execution is `Chưa cập nhật`; it is not `Chưa bắt đầu`.
- Missing percentage evidence is `Chưa đủ dữ liệu để tính % hoàn thành` on
  summaries and `Chưa đủ dữ liệu` on compact rows.
- Missing owner is `Chưa xác định đầu mối`.
- Dates display consistently as `dd/MM/yyyy` outside the compact daily axis.
- Raw validation states, commit hashes, snapshot IDs, absolute paths, manifest
  paths, and long provenance locators do not appear on the first four sheets.
- A compact provenance sentence names the official IDEAEngineering source and
  the source reporting date.

## Common visual semantics

| Meaning | Visual | Required text/symbol support |
| --- | --- | --- |
| Plan/baseline | restrained blue | Lane label `Kế hoạch` and plan date columns |
| Evidence-backed Actual | green | Lane label `Thực tế` and state/progress text |
| Official future forecast | amber | `Dự báo` or forecast date text |
| Blocked/overdue | red cue | State or variance text |
| Missing/insufficient data | gray | Explicit missing-data text |
| Weekend | light gray background | weekday/date headers remain visible |
| Reporting boundary | red vertical border | `Ngày báo cáo` label/date |
| Milestone | diamond or equivalent symbol | milestone name/date text |

Color never carries meaning alone.

## Common daily axis

- One worksheet column represents one calendar date.
- A month header spans all visible date columns in that month.
- The next header row shows day-of-month; the following row shows Vietnamese
  weekday abbreviation.
- Saturday and Sunday backgrounds are subdued when not covered by a stronger
  Plan/Actual/forecast style.
- The official source reporting date receives a labelled red vertical marker.
- Full and overview axes include all baseline dates and valid out-of-baseline
  Actual/forecast dates selected by the daily projection.
- The 30-day axis is exactly source reporting date through 29 days later.

## Paired task-band contract

Every non-milestone logical Gantt row uses two adjacent worksheet rows:

1. `Kế hoạch`: the immutable baseline interval.
2. `Thực tế`: evidence-backed Actual followed, when eligible, by official
   forecast.

Identity and management columns are displayed once for the task band, using
vertical merge or an equivalent non-repeating layout. Plan and Actual labels
remain visible. Lane length represents dates only; percentage is printed in a
fixed management column.

Milestones use one zero-duration row with the approved symbol and no fabricated
Actual lane.

### Delivery-card Actual lane

- Completed: green from actual start through recorded actual finish.
- In progress: green from actual start through analysis as-of date; no finish
  marker or completion claim.
- Suspended/cancelled: green past interval only when an actual start exists;
  the state remains visible.
- Not started/unrecorded/missing actual start: no green cells.
- Forecast: amber from the day after analysis as-of through official forecast
  finish, only when that finish is later than as-of.
- Invalid contradictory dates do not render; export fails at the validated
  official boundary.

### Roll-up lane

Project, phase, and work-package Actual lanes may summarize known child
activity. The fixed columns must display `Độ phủ recorded/total` beside the
roll-up. Partial coverage cannot receive a whole-scope percentage.

## Sheet 1: `Tổng quan`

### Header and summary region

The first visible region contains:

1. `Báo cáo điều hành tiến độ` and cleaned project name.
2. Official source reporting date, analysis date when different, and baseline
   planning window.
3. Four compact blocks in this order:
   - `Giai đoạn hiện tại`
   - `Mốc kế tiếp`
   - `Tiến độ thực tế`
   - `Cần quyết định`

`Tiến độ thực tế` contains:

- supported percentage or insufficient-data statement;
- actual and remaining effort when both are known;
- `Độ phủ ghi nhận recorded/total`;
- `Đủ effort eligible/total`;
- latest official update or `Chưa cập nhật`.

### Overview Gantt

- Rows: Project, all phases, and milestone-kind milestones only.
- Axis: full daily axis shared with the detailed Gantt.
- Paired Plan/Actual rows apply to Project and Phase; milestone rule applies to
  milestones.
- Current phase and next milestone are identifiable in text.

### Attention region

- At most five items in approved priority order.
- Columns: `Việc cần xử lý`, `Ảnh hưởng`, `Đầu mối`, `Cần xong trước`.
- Empty state: `Hiện chưa có nội dung cần xin ý kiến`.

### View/print

- Freeze title/summary/context rows and the descriptive Gantt columns.
- Landscape printing; summary blocks and attention list remain legible.
- The daily Gantt may continue horizontally instead of forcing every date onto
  one unreadable printed page.

## Sheet 2: `Gantt theo ngày`

### Fixed columns

Before the daily axis, show these columns in order:

1. `Mã`
2. `Hạng mục`
3. `Trạng thái`
4. `Đầu mối`
5. `% thực tế`
6. `Độ phủ`
7. `Cập nhật cuối`
8. `Làn` (`Kế hoạch` or `Thực tế`)

Short identifiers support hierarchy scanning but never replace the task name.

### Rows

- Complete Project → Phase → Work package → Delivery card → Milestone
  hierarchy in stable order.
- Indentation and a hierarchy symbol distinguish levels.
- Every canonical delivery card appears exactly once as one logical task band.
- Orphans remain visible with explicit unknown parent context.

### View/print

- Freeze all fixed columns and all date-header rows.
- Keep daily columns readable at 100% zoom.
- Landscape with horizontal pagination; do not fit the full multi-month axis
  to one page wide.

## Sheet 3: `30 ngày tới`

### Summary region

Show:

- total qualifying work;
- unfinished overdue count;
- in-progress count;
- completed count where present;
- unrecorded count;
- next milestone inside the window or explicit missing state.

### Fixed columns and rows

Use the same fixed columns and paired-lane semantics as `Gantt theo ngày`.
Include qualifying delivery cards and milestones only. Order overdue open work
first, then nearest planned finish, then stable source order.

### Axis and print

- Exactly 30 daily columns.
- Render only the truthful intersection of Plan, Actual, or forecast intervals
  with the 30-date axis. A `◀` or `▶` continuation symbol at the first or last
  date cell indicates that the underlying interval continues outside the
  visible window; the legend explains both symbols and no endpoint is moved.
- An unfinished overdue item whose whole Plan interval is before the visible
  axis receives no fabricated blue in-window bar. Its state text includes the
  planned-finish date and the first Plan cell shows a distinct red
  `◀ Quá hạn dd/MM` marker, which is an overdue indicator rather than a Plan
  endpoint.
- Freeze fixed columns and date headers.
- Landscape, fit to one page wide where labels remain legible; otherwise use
  the smallest bounded horizontal pagination that preserves readability.

### Empty state

`Không có công việc quá hạn hoặc giao với cửa sổ 30 ngày.`

## Sheet 4: `Vấn đề cần xử lý`

- Preserve the complete Feature 005 attention population and deterministic
  order.
- Columns: `Việc cần xử lý`, `Ảnh hưởng`, `Đầu mối`, `Cần xong trước`.
- No raw evidence ID, decision ID, diagnostic code, or technical token.
- Freeze the table header; wrap long text.
- Empty state: `Hiện chưa có nội dung cần xin ý kiến`.

## Sheet 5: `Chi tiết công việc`

One stable row per delivery card with columns in this order:

1. `Công việc`
2. `Giai đoạn`
3. `Gói công việc`
4. `Bắt đầu kế hoạch`
5. `Kết thúc kế hoạch`
6. `Bắt đầu thực tế`
7. `Kết thúc thực tế`
8. `Kết thúc dự báo`
9. `Giờ thực tế`
10. `Giờ còn lại`
11. `% thực tế`
12. `Trạng thái ghi nhận`
13. `Tình trạng thực thi`
14. `Đầu mối`
15. `Cập nhật cuối`
16. `Mã tham chiếu`

Unknown values use explicit labels or blank typed date/number cells accompanied
by the relevant missing-data label. Missing effort does not become numeric
zero. Freeze the header and first descriptive column; support filtering only if
it does not alter deterministic content.

## Style and accessibility contract

- Use a standard spreadsheet font available on target systems.
- Titles are concise and left aligned; no decorative gradients or oversized
  banners.
- Headers have strong contrast; body text is dark on light backgrounds.
- Long descriptions wrap without semantic truncation.
- Numeric percentage and hour columns are right aligned and typed as numbers
  when present.
- Fixed columns remain wide enough to avoid clipping primary labels at 100%.
- Legend appears once on each Gantt-bearing sheet and explains all semantic
  colors/symbols.

## Forbidden content

The first four sheets must not contain:

- commit SHA or snapshot ID;
- absolute or manifest path;
- validation/reconciliation code;
- proposal data presented as Actual;
- AI-generated narrative or inferred cause;
- fabricated percentage, Actual date, forecast date, owner, or completion.
