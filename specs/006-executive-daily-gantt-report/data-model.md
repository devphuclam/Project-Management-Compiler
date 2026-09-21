# Data Model: Executive Daily Gantt Report

## Projection boundary

The feature extends the presentation-only `ExecutiveProgressReport`. It does
not change canonical planning entities, official source-execution records,
proposal entities, CARIO mapping entities, or import contracts.

```text
Official CompilationResult
  ├── Canonical baseline and hierarchy
  ├── Official source execution
  ├── Deterministic analysis
  └── Management evidence
          │
          ▼
ExecutiveDailyGanttProjector
          │ ExecutiveDailyGanttProjection
          ▼
ExecutiveProgressReportProjector
          │ ExecutiveProgressReport
          ▼
ExecutiveProgressWorkbookComposer
          │ ExecutiveWorkbookDocument
          ▼
ExecutiveProgressXlsxExporter
          │ five-sheet XLSX bytes
          ▼
Official management-report download
```

No reverse transition exists.

## Executive progress report changes

`ExecutiveProgressReport` retains project identity, source/analysis dates,
summary conditions, milestone, progress, and attention collections. It adds:

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `DailyGantt` | `ExecutiveDailyGanttProjection` | yes | All overview, full-hierarchy, and near-term rows plus date bounds. |

The legacy weekly `OverviewTimeline` and `WorkPackageSchedule` projections are
removed from workbook composition after daily projection tests are green. They
may remain temporarily during refactoring but cannot be serialized into the
Feature 006 workbook.

## Executive progress summary changes

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `RecordedPercent` | integer 0–100 | no | Rounded actual / (actual + remaining), only when eligible. |
| `Statement` | string | yes | Approved valid/insufficient Vietnamese statement. |
| `ActualEffortHours` | decimal | no | Official aggregate; non-negative when present. |
| `RemainingEffortHours` | decimal | no | Official aggregate; non-negative when present. |
| `CompletedCount` | integer | yes | Evidence-backed completed cards. |
| `InProgressCount` | integer | yes | Evidence-backed in-progress cards. |
| `NotStartedCount` | integer | yes | Explicitly recorded not-started cards only. |
| `UnknownCount` | integer | yes | Cards without a supported explicit execution state. |
| `RecordedCardCount` | integer | yes | Cards with official `recordingState = RECORDED`. |
| `TotalCardCount` | integer | yes | All delivery cards in report scope. |
| `ProgressEligibleCardCount` | integer | yes | Cards with valid actual and remaining effort and positive total. |
| `LastOfficialUpdate` | datetime with offset | no | Maximum official execution `lastUpdatedAt`; absent remains explicit. |

### Project progress eligibility

The headline percentage remains eligible only when the approved aggregate
actual and remaining effort are both finite, non-negative, and sum to more
than zero. `RecordedCardCount / TotalCardCount` appears beside it regardless of
percentage eligibility. `ProgressEligibleCardCount` states how many cards
support effort percentages; it never substitutes for recording coverage.

## Executive daily Gantt projection

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `FullStart` | date | yes | Minimum attributable baseline, Actual, forecast, and reporting/as-of boundary used by full rows. |
| `FullFinish` | date | yes | Maximum attributable boundary; must be on/after `FullStart`. |
| `NearTermStart` | date | yes | Exact official source reporting date. |
| `NearTermFinish` | date | yes | `NearTermStart + 29 days`. |
| `OverviewRows` | row collection | yes | Project, phase, and milestone rows in stable hierarchy order. |
| `FullRows` | row collection | yes | Complete Project → Phase → Work package → Delivery card → Milestone hierarchy. |
| `NearTermRows` | row collection | yes | Eligible overdue/open or window-intersecting delivery cards and milestones. |

The projector must return non-null collections. If no attributable planning
date exists, projection fails with a structured incomplete-official diagnostic
rather than inventing an axis.

## Daily Gantt row

### Row kind

`ExecutiveDailyGanttRowKind` has exactly:

- `Project`
- `Phase`
- `WorkPackage`
- `DeliveryCard`
- `Milestone`

### Fields

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `Kind` | row-kind enum | yes | Determines hierarchy, lane eligibility, and milestone rendering. |
| `ReferenceCode` | string | yes | Stable short identity; not a raw provenance locator. |
| `DisplayName` | string | yes | Mechanically cleaned reader-facing title. |
| `HierarchyLevel` | integer 0–4 | yes | Project 0, Phase 1, Work package 2, Delivery card 3, Milestone 2 or approved parent level. |
| `PhaseDisplayName` | string | no | Parent phase for detail/filter context. |
| `WorkPackageDisplayName` | string | no | Parent work package for card detail. |
| `PlannedStart` | date | no | Immutable baseline start. |
| `PlannedFinish` | date | no | Immutable baseline finish; not before planned start when both exist. |
| `ActualStart` | date | no | Direct official actual start for cards or labelled child roll-up boundary. |
| `ActualFinish` | date | no | Direct recorded actual finish; never receives analysis date. |
| `ActualDisplayThrough` | date | no | Actual finish when completed; analysis date for valid open in-progress; roll-up boundary under roll-up rules. |
| `ForecastFinish` | date | no | Official forecast date only. |
| `ProgressPercent` | integer 0–100 | no | Row-scope effort percentage only when eligibility and coverage rules pass. |
| `ProgressLabel` | string | yes | Percentage text or explicit insufficient-data text. |
| `RecordedChildCount` | integer | yes | Recorded card count in row scope; 0 or 1 for a delivery card. |
| `ProgressEligibleChildCount` | integer | yes | Effort-eligible card count in row scope. |
| `TotalChildCount` | integer | yes | All delivery cards in row scope; 1 for a delivery card. |
| `CoverageLabel` | string | yes | `Độ phủ {recorded}/{total}` with explicit zero/empty behavior. |
| `StateLabel` | string | yes | Approved Vietnamese execution/unknown label. |
| `OwnerLabel` | string | yes | Approved owner label or `Chưa xác định đầu mối`. |
| `LastOfficialUpdate` | datetime with offset | no | Direct or maximum child official update. |
| `IsBlocked` | boolean | yes | Supported blocked condition for red cue and text. |
| `IsOverdue` | boolean | yes | Supported derived overdue condition for red cue and near-term ordering. |
| `IsCurrent` | boolean | yes | Baseline/reporting-date context; never substitutes for execution state. |
| `IsNextMilestone` | boolean | yes | Highlights the selected next milestone. |
| `SourceOrder` | integer | yes | Stable deterministic order from canonical source. |

### Direct delivery-card rules

1. Resolve execution through `ExecutionTruthResolver.ForCard`.
2. If no official record or `IsRecorded` is false:
   - state is `Chưa cập nhật`;
   - Actual dates, forecast, percentage, and last update are absent;
   - recorded and eligible counts are 0; total is 1;
   - no green or amber lane is eligible.
3. Explicit `NotStarted` remains `Chưa bắt đầu`; it has no Actual lane.
4. `InProgress` receives an Actual lane only when `ActualStart` exists.
   `ActualDisplayThrough = AnalysisAsOfDate`; `ActualFinish` remains null.
5. `Completed` receives an Actual lane only when both actual dates exist and
   `ActualFinish >= ActualStart`; `ActualDisplayThrough = ActualFinish`.
6. Suspended/cancelled records may display recorded past Actual only when a
   valid actual start exists. No future Actual is invented.
7. Forecast is eligible only from official `ForecastFinish`. The amber segment
   starts on the day after `AnalysisAsOfDate`; if forecast is not after the
   analysis date, no future segment is drawn, but the date remains available in
   detail.
8. Percentage is eligible only when actual and remaining effort are both
   finite, non-negative, and sum to more than zero. Round to a whole percent
   with midpoint away from zero, matching Feature 005.
9. A recorded state with incomplete Actual evidence is exported with explicit
   missing-data text unless canonical validation already classifies the record
   as contradictory and blocks export.

### Work-package and phase roll-up rules

The scope is all descendant delivery cards.

- `TotalChildCount` is the complete descendant-card count.
- `RecordedChildCount` counts only official recorded cards.
- `ProgressEligibleChildCount` counts only cards satisfying the effort rule.
- Percentage is present only when `ProgressEligibleChildCount ==
  TotalChildCount`, total is greater than zero, and aggregate actual plus
  remaining is greater than zero.
- Known activity may produce a roll-up Actual interval:
  - start = minimum valid child actual start;
  - display-through = maximum child actual finish for completed known children,
    or analysis date when any valid child is in progress;
  - the coverage label is always shown beside a roll-up lane.
- A roll-up Actual interval never means all child work is recorded unless
  `RecordedChildCount == TotalChildCount`.
- Forecast finish is shown only when every non-completed descendant has an
  official forecast finish; use the maximum qualifying finish. Partial
  forecasts do not become a roll-up forecast.
- Existing conservative state precedence remains: explicit in-progress child;
  otherwise unanimous fully recorded completed/not-started/suspended/cancelled;
  otherwise `Chưa cập nhật`.

### Project roll-up rules

The Project row follows the same child-count, coverage, percentage, Actual, and
forecast rules over all delivery cards. Its planned interval is the immutable
baseline planning window.

### Milestone rules

- Plan date is the milestone's official planned date and has zero duration.
- A milestone has no Actual lane unless the canonical milestone carries an
  approved direct execution date in a future model revision; delivery-card
  execution is not substituted.
- Current Feature 006 displays the approved milestone symbol, state label, and
  next-milestone flag; Actual remains `Chưa cập nhật` where absent.

## Full hierarchy construction

Stable order is:

1. Project row.
2. Each phase in canonical order.
3. Each work package under its phase in canonical order.
4. Each delivery card under its work package in canonical order.
5. Milestones/control points at the approved phase/project position, ordered by
   planned date then source order.

Orphans retain stable source order under the nearest known parent and display
explicit unknown parent text; hierarchy construction never drops a canonical
delivery card silently.

## Overview selection

`OverviewRows` contains:

- the Project row;
- all Phase rows;
- milestone rows of kind `Milestone`, including the selected next milestone;
- no Work package or Delivery card rows.

The overview shares the full axis so phase/milestone context reconciles with
the detailed Gantt.

## Near-term selection and order

### Window

```text
NearTermStart  = SourceReportingDate
NearTermFinish = SourceReportingDate + 29 calendar days
```

### Delivery-card membership

A card qualifies when either condition is true:

1. It is not officially completed and has a planned finish before
   `NearTermStart` (unfinished overdue work); or
2. Its planned interval intersects `[NearTermStart, NearTermFinish]`:
   `PlannedStart <= NearTermFinish` and `PlannedFinish >= NearTermStart`.

A missing planned boundary cannot satisfy condition 2. Supported analysis may
still include it under condition 1 only when an attributable overdue alert
exists and the row remains explicitly date-incomplete.

### Milestone membership

A milestone qualifies when its planned date is within the exact window. The
next milestone may also appear as context only when its date is inside the
window; it is not forced into the sheet from outside.

### Order

1. Unfinished overdue delivery cards.
2. Known planned finish ascending; unknown after known.
3. Stable source order.
4. Milestones are ordered at their planned date without displacing overdue
   cards from the first group.

## Delivery-card detail changes

`ExecutiveDeliveryCardDetail` adds:

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `ActualStart` / `ActualFinish` | date | no | Direct official values only. |
| `ForecastFinish` | date | no | Direct official forecast only. |
| `ActualEffortHours` | decimal | no | Direct official cumulative actual effort. |
| `RemainingEffortHours` | decimal | no | Direct official remaining estimate. |
| `ProgressPercent` | integer 0–100 | no | Direct card percentage when eligible. |
| `ProgressLabel` | string | yes | Percentage or explicit insufficient-data text. |
| `RecordingLabel` | string | yes | `Đã ghi nhận` or `Chưa cập nhật`. |
| `LastOfficialUpdate` | datetime with offset | no | Direct official update timestamp. |

`ReferenceCode` remains the final short traceability field. No absolute path,
commit hash, snapshot ID, evidence description, or raw validation code appears.

## Workbook document

`ExecutiveWorkbookDocument` is an internal output value model.

### Workbook

| Field | Invariant |
| --- | --- |
| `Sheets` | Exactly five non-hidden sheets in contract order. |
| `ActiveSheetIndex` | Exactly 0. |

### Worksheet

| Field | Invariant |
| --- | --- |
| `Name` | Exact approved sheet name. |
| `Rows` | Ordered immutable row collection. |
| `ColumnWidths` | One positive width per used/reserved column. |
| `MergedRanges` | Non-overlapping valid ranges, used for title/month/task-band presentation only. |
| `FreezeRows` / `FreezeColumns` | Non-negative and within the used range. |
| `PrintMode` | Landscape; overview/30-day fit to width, full Gantt horizontal pagination allowed. |
| `ShowGridLines` | False. |
| `ZoomPercent` | 100. |

### Cell

| Field | Invariant |
| --- | --- |
| `Value` | Reader-facing text/date/number already selected by the composer. |
| `StyleToken` | One semantic token resolved by the XLSX serializer. |
| `NumberFormat` | Explicit for dates, numbers, and percentages. |

Semantic style tokens include title, subtitle, header, default, plan,
actual-complete, forecast, attention, blocked/overdue, unknown, weekend,
milestone, report-date boundary, and hierarchy levels. Color reinforces text
and symbols but never replaces them.

## Export validation states

| Condition | Outcome |
| --- | --- |
| No official result | Existing `NO_PROJECT` or `NO_OFFICIAL_SNAPSHOT` response. |
| Missing import metadata/reporting date/analysis date | Existing incomplete-official failure. |
| Sparse but non-contradictory Actual | Export succeeds with explicit missing-data labels. |
| Actual finish before actual start, negative effort, or other unrepresentable official contradiction | Export fails closed with a specific executive-export diagnostic. |
| Proposal/preview differs from official | Official workbook remains unchanged. |

## Immutability

Projection and export are pure with respect to the input graph. Tests compare
the semantic digest, official source-execution projection, proposals, preview
state, and source repository before and after export. Workbook creation never
normalizes or writes values back into canonical entities.
