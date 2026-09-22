# Contract: Management Report Workbook

## Artifact identity

The workbook produced by `Xuất báo cáo tiến độ` is a **Management Report**:

- presentation-only;
- compiled from one complete official snapshot;
- deterministic and read-only;
- never accepted as a Tracking Source, Project Workbook, or preview import;
- distinct from `Xuất dữ liệu CARIO + Gantt`.

## Workbook contract

The workbook contains exactly these visible sheets in this order:

1. `Tổng quan`
2. `Điều hành 30 ngày`
3. `Gantt`
4. `WBS`
5. `Chi tiết công việc`
6. `Thông tin báo cáo`

`Tổng quan` is the active sheet. Every sheet opens at 100% zoom, hides default
gridlines, and freezes the context required by its contract. No hidden data
sheet or import marker is permitted.

## Shared reader-language contract

- One cell communicates one idea.
- Headings are short and action-oriented.
- Reader-facing names show meaningful text once; IDs appear only in an explicit
  identity column or the metadata sheet.
- Recognized ID prefixes, repeated bracket prefixes, hierarchy arrows,
  Markdown decoration, entity-kind prefixes, raw enums, and validation codes do
  not appear in reader-facing fields.
- `Chưa ghi nhận` describes missing execution evidence.
- `Chưa phân công` describes a missing owner.
- `Cập nhật đến dd/MM/yyyy` communicates recency where needed.
- The first five sheets contain no absolute path, snapshot ID, commit hash,
  authority rank, extraction rule, validation state, or repeated provenance
  paragraph.
- Text cleanup is deterministic; no generated narrative, explanation, cause, or
  recommendation is added.

## Shared visual contract

| Meaning | Color | Required non-color cue |
| --- | --- | --- |
| Immutable plan | Blue | `Kế hoạch` lane/text |
| Recorded Actual | Green | `Thực tế`, interval, `✓`, or `●` |
| Supported forecast or attention | Amber | Forecast/attention label |
| Blocked, overdue, or decision required | Red | State/action text |
| Missing or unavailable evidence | Gray | `Chưa ghi nhận` or explicit blank-state text |

Colors may reinforce meaning but never carry it alone. Standard fonts, dark
body text, strong-contrast headers, wrapped long titles, and restrained borders
are required. Decorative gradients, oversized title banners, repeated legends,
and empty dashboard panels are prohibited.

## Sheet 1: `Tổng quan`

### Reader questions

The first visible page answers, in order:

1. Where is the project now?
2. How much supported progress is recorded?
3. What changed from plan?
4. What milestone comes next?
5. What needs a decision?

### Content

- Concise report title and cleaned project name.
- `Cập nhật đến dd/MM/yyyy`; analysis date appears only when different.
- Current phase, next milestone, evidence-backed progress, schedule condition,
  and decision count/value in a compact information hierarchy.
- One phase/milestone schedule overview using the same Plan/Actual meanings as
  the full Gantt.
- At most five actions, with columns `Việc cần xử lý`, `Ảnh hưởng`, `Đầu mối`,
  and `Cần xong trước`.
- Empty action text: `Hiện chưa có nội dung cần xin ý kiến`.

### View and print

- Landscape.
- Fit to one page wide while keeping the summary and action list legible.
- Freeze the summary/context boundary and descriptive schedule columns.
- The overview schedule may continue horizontally when forcing it to one page
  would make daily labels unreadable.

## Sheet 2: `Điều hành 30 ngày`

### Window and population

The window is the official reporting date through 29 calendar days later.
Rows appear in this order:

1. decisions and blockers;
2. unfinished overdue work;
3. active work;
4. planned work and milestones intersecting the window.

One concern appears once after stable-target deduplication. A decision or
blocker may remain eligible even when its target schedule lies outside the
window. No full Kanban board is included.

### Columns

Primary columns are:

1. `Việc cần làm`
2. `Ảnh hưởng`
3. `Đầu mối`
4. `Cần xong trước`
5. `Trạng thái`
6. `Bối cảnh tiến độ`

Raw evidence, alert, decision, and validation IDs are prohibited. Empty state:
`Không có quyết định, công việc quá hạn hoặc công việc giao với 30 ngày tới.`

### View and print

- Landscape and one page wide when fixture content remains readable.
- Continue vertically to additional pages rather than shrink body text.
- Freeze title/context and column headers.
- Wrap action and consequence text without semantic truncation.

## Sheet 3: `Gantt`

### Population and hierarchy

The full daily schedule contains Project, Phase, Work Package, Delivery Card,
and Milestone rows in canonical stable order. Every canonical Delivery Card
appears exactly once. Milestones remain zero-duration symbols.

Each work item shows its readable name once and uses adjacent `Kế hoạch` and
`Thực tế` lanes. Project, Phase, and Work Package roll-ups disclose evidence
coverage whenever Actual is summarized.

### Actual truth matrix

| Evidence shape | Required rendering |
| --- | --- |
| Actual start + Actual finish | Green interval over recorded dates |
| In-progress Actual start only | Green open interval through analysis date |
| Actual finish only | Green `✓` on recorded finish; no duration |
| Effort evidence without Actual date | `● Có ghi nhận`; no dated cell |
| No execution evidence | Empty Actual timeline and `Chưa ghi nhận` |

Actual start is never inferred from Actual finish, effort, planned dates,
update time, or completion state. Forecast appears only from supported official
forecast evidence.

### Calendar

- One calendar day per timeline column.
- Month, date, and weekday context remains visible.
- Weekends use subdued background.
- The official reporting boundary is labelled and visible even when it lies
  outside the baseline interval.
- Intervals extending outside a visible sub-window use `◀` or `▶`; endpoints
  are never moved.

### View and print

- Freeze identity columns and date headers.
- Keep daily columns readable at 100% zoom.
- Landscape with horizontal pagination; do not fit the full multi-month axis to
  one page wide.

## Sheet 4: `WBS`

### Membership and hierarchy

The WBS contains exactly:

```text
L0 Project
  L1 Phase
    L2 Work Package
      L3 Delivery Card
```

Milestones and decision gates are not WBS members. Numbering follows canonical
parent/child order and does not depend on dates, IDs, or execution state.

### Primary columns

These columns are visible on open, in this order:

1. `WBS`
2. `Mã`
3. `Hạng mục`
4. `Loại`
5. `Đầu mối`
6. `Trạng thái`
7. `% thực tế`
8. `Cần chú ý`

`Hạng mục` contains only the Reader-Facing Name. It must not repeat its row's
`Mã`, `PH0`, `PLN01`, `[PH0][PLN01]`, an entity-kind label, a hierarchy arrow,
or Markdown decoration.

### Optional column groups

Each group is present and initially collapsed:

- **Kế hoạch**: planned start, planned finish.
- **Thực tế**: actual start, actual finish, actual effort, remaining effort,
  latest official update.
- **Quan hệ**: predecessors, dependency state.
- **Bằng chứng**: concise evidence/completion summary, minimal relative source
  reference.

No optional group may hide or duplicate the eight primary columns.

### Row outline

- The sheet opens through Work Package depth.
- Delivery Card rows are initially hidden under expandable Work Package groups.
- Expanding all groups reveals every Delivery Card exactly once.
- Parent rows precede their children and retain the expand/collapse control.
- Unresolved parentage remains explicit and never silently drops a row.

### View

- Freeze the header and the primary identity columns.
- Enable filter over the complete WBS table.
- Optimize for 100% screen reading; do not force the full table onto one printed
  page.

## Sheet 5: `Chi tiết công việc`

One stable row represents each Delivery Card. Reader-facing fields precede
technical identity and provenance. The table includes:

- work name, phase, work package;
- owner, state, attention, and evidence-backed progress;
- planned start/finish;
- Actual start/finish and forecast finish;
- Actual/remaining effort;
- latest official update;
- predecessor/dependency context;
- stable reference code and minimal source reference.

Blank typed dates and numbers remain blank; an adjacent text field communicates
missing evidence. The sheet freezes its header and identity columns and enables
filtering across the complete table.

## Sheet 6: `Thông tin báo cáo`

This is the only sheet that presents complete report authority and audit
metadata:

- authority/classification;
- reporting date and analysis date;
- source commit/object identity;
- snapshot and project identity;
- baseline identity/version and planning window;
- register revision and contract version;
- concise limitations.

Metadata is organized as short label/value rows. It must not contain secrets,
credentials, private workstation paths, or generated narrative.

## Progress calculation

```text
progress = actual effort / (actual effort + remaining effort)
```

The percentage is shown only when both values are finite, non-negative, and
their sum is positive. Missing or invalid input never becomes zero. A parent
percentage requires every descendant Delivery Card to be eligible; otherwise
the parent uses a concise missing-evidence label and exposes coverage.

## Workbook integrity

- The same report projection produces deterministic package content.
- The package contains no macros, external links, data connections, or remote
  resources.
- Worksheet names, order, active sheet, outlines, filters, panes, types, number
  formats, and print settings are machine-verifiable.
- The workbook carries no technical preview markers and must be rejected by the
  existing XLSX preview importer.

