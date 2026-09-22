# Data Model: Management Report Readability

## Model boundary

The feature adds presentation projections beneath the immutable canonical
project. Source adapters and canonical entities do not change. Every report
entity is rebuilt from one official `CompilationResult` and is discarded after
the XLSX bytes are produced.

```text
Official CompilationResult
  ├─ canonical hierarchy / baseline / dependencies
  ├─ effective official execution
  ├─ management analysis / attention
  └─ import metadata / provenance
       ↓ deterministic report projectors
ExecutiveProgressReport
  ├─ summary
  ├─ OperatingItems
  ├─ DailyGantt
  ├─ Wbs
  ├─ DeliveryCardDetails
  └─ Metadata
       ↓ worksheet composers
ExecutiveWorkbookDocument
       ↓ deterministic Open XML serializer
Management Report XLSX
```

The report projection cannot be persisted as project authority and cannot be
used as input to any preview/import path.

## Reader-facing text policy

`ReaderFacingTextPolicy` is a deterministic value policy rather than a stored
entity.

### Inputs

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `SourceText` | string | no | Original source title or label. |
| `ExactId` | string | no | Stable identity already displayed elsewhere. |
| `EntityKind` | report row kind | no | Allows removal of a recognized kind label, not arbitrary words. |

### Output

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `ReaderFacingName` | string | yes | Meaningful normalized text, or the approved missing-name label. |

### Invariants

- Trim surrounding whitespace and presentation-only Markdown markers.
- Repeated leading bracket prefixes are removed only when each token is the
  exact ID, a known ancestor ID, or a recognized identity-shaped prefix.
- A leading or trailing exact ID is removed only across a recognized separator.
- Hierarchy arrows and entity-kind decoration are removed only when they are
  presentation prefixes, not when the words are meaningful title content.
- Internal words, punctuation, Vietnamese diacritics, and meaningful bracketed
  text are preserved.
- Cleanup never translates, summarizes, generates, or changes business meaning.
- An empty result becomes `Chưa ghi nhận`, never a fabricated title.

## Executive progress report

The existing `ExecutiveProgressReport` remains the root presentation model and
adds the following feature-owned fields.

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `OperatingItems` | ordered item collection | yes | Unified 30-day action population. |
| `Wbs` | WBS projection | yes | Complete four-level management WBS. |
| `Metadata` | report metadata | yes | Authority and audit facts for the final sheet. |

Existing summary, attention, daily-Gantt, and delivery-card detail fields remain
available during migration. Obsolete sheet-specific collections may be removed
only after their replacement tests are green.

## Actual presentation

### Actual presentation kind

`ExecutiveActualPresentationKind` has exactly:

- `RecordedInterval`
- `OpenRecordedInterval`
- `CompletionPoint`
- `EffortOnly`
- `None`

### Daily Gantt row additions

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `ActualPresentationKind` | enum | yes | One of the five shapes above. |
| `ActualEvidenceLabel` | string | yes | Approved concise label such as `Có ghi nhận` or `Chưa ghi nhận`. |
| `ActualEffortHours` | decimal | no | Direct official actual effort for cards; conservative aggregate for roll-ups. |
| `RemainingEffortHours` | decimal | no | Direct official remaining effort for cards; conservative aggregate for roll-ups. |

Existing `ActualStart`, `ActualFinish`, and `ActualDisplayThrough` fields keep
their factual meanings. The presentation kind does not replace or synthesize
them.

### Classification rules

| Evidence shape | Kind | Calendar rendering | Fixed-row label |
| --- | --- | --- | --- |
| Actual start and Actual finish | `RecordedInterval` | Inclusive green interval | Recorded state/progress label |
| In-progress Actual start without finish | `OpenRecordedInterval` | Green interval through analysis date | In-progress label |
| Actual finish without Actual start | `CompletionPoint` | `✓` on recorded finish | Completion recorded |
| Official effort evidence without Actual date | `EffortOnly` | No dated bar or point | `● Có ghi nhận` |
| No official execution evidence | `None` | Empty Actual timeline | `Chưa ghi nhận` |

Additional invariants:

- `CompletionPoint` requires a non-null Actual finish and null Actual start.
- `OpenRecordedInterval` requires a non-null Actual start, null Actual finish,
  in-progress execution, and display-through equal to the analysis date.
- `EffortOnly` requires at least one official effort fact and no Actual date.
- A row never receives a date solely because effort or state is present.
- Roll-ups apply the same shape vocabulary over known child evidence and always
  retain recorded/total coverage; partial coverage never claims whole-scope
  completion.

## Executive WBS projection

### WBS projection

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `Rows` | ordered WBS row collection | yes | Every canonical WBS member exactly once. |
| `ProjectCount` | integer | yes | Exactly 1 for an exportable report. |
| `PhaseCount` | integer | yes | Reconciles to canonical phases. |
| `WorkPackageCount` | integer | yes | Reconciles to canonical work packages. |
| `DeliveryCardCount` | integer | yes | Reconciles to canonical delivery cards. |

### WBS row kind

`ExecutiveWbsRowKind` has exactly:

- `Project`
- `Phase`
- `WorkPackage`
- `DeliveryCard`

Milestones and decision gates are deliberately not WBS row kinds.

### WBS row fields

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `WbsNumber` | string | yes | Positional number from canonical hierarchy order (`1`, `1.1`, …). |
| `ReferenceCode` | string | yes | Stable identity displayed once in `Mã`. |
| `DisplayName` | string | yes | Reader-Facing Name only. |
| `Kind` | enum | yes | One of the four WBS kinds. |
| `ParentReferenceCode` | string | no | Canonical parent identity; absent only for Project or unresolved parentage. |
| `Depth` | integer 0–3 | yes | Project 0, Phase 1, Work Package 2, Delivery Card 3. |
| `OwnerLabel` | string | yes | Reader-facing owner or `Chưa phân công`. |
| `StateLabel` | string | yes | Approved Vietnamese state or `Chưa ghi nhận`. |
| `ProgressPercent` | integer 0–100 | no | Evidence-based only. |
| `ProgressLabel` | string | yes | Percentage or concise missing-evidence label. |
| `AttentionLabel` | string | yes | Concise action/attention state; no raw diagnostic code. |
| `PlannedStart` / `PlannedFinish` | date | no | Immutable baseline fields. |
| `ActualStart` / `ActualFinish` | date | no | Direct or explicitly conservative official execution fields. |
| `ActualEffortHours` | decimal | no | Official actual effort. |
| `RemainingEffortHours` | decimal | no | Official remaining effort. |
| `LastOfficialUpdate` | timestamp | no | Latest official update in row scope. |
| `PredecessorCodes` | ordered string collection | yes | Stable predecessor IDs, deduplicated. |
| `DependencyLabel` | string | yes | Reader-facing dependency condition. |
| `EvidenceSummary` | string | yes | Concise completion/evidence summary. |
| `SourceReferenceLabel` | string | yes | Minimal relative source locator; never an absolute path. |
| `SourceOrder` | integer | yes | Deterministic canonical order within kind/parent. |

### Hierarchy and numbering rules

1. The Project row is first and uses `1`.
2. Phases follow canonical collection order and use `1.n`.
3. Work Packages follow canonical order within their Phase and use `1.n.m`.
4. Delivery Cards follow canonical order within their Work Package and use
   `1.n.m.k`.
5. A missing parent never causes a row to disappear or acquire a false parent.
   The row carries unresolved-parent context and the export fails only when the
   official contradiction cannot be represented safely.
6. Dates, dependencies, state, and execution facts never determine WBS
   parentage or numbering.

### Progress and roll-up rules

- Delivery Card progress uses the approved Actual/(Actual+Remaining) rule.
- Parent percentage is available only when every descendant Delivery Card is
  eligible; otherwise `ProgressPercent` is null and coverage remains explicit
  in the evidence summary.
- Parent Actual boundaries are conservative child roll-ups and never imply full
  evidence coverage.
- Attention is projected from supported management analysis and never from
  workbook formatting.

## 30-day operating projection

### Operating category

`ExecutiveOperatingCategory` has this display order:

1. `DecisionOrBlocker`
2. `OverdueUnfinished`
3. `Active`
4. `PlannedOrMilestone`

### Operating item fields

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `StableKey` | string | yes | Deduplication identity; not displayed to readers. |
| `Category` | enum | yes | Determines group and order. |
| `TargetKind` / `TargetId` | string | yes | Traceability identity; kept out of the primary action text. |
| `Action` | string | yes | One concise action or decision. |
| `Consequence` | string | yes | Source-backed impact; no inferred cause. |
| `OwnerLabel` | string | yes | Reader-facing owner or `Chưa phân công`. |
| `RequiredDate` | date | no | Supported due, planned finish, or milestone date. |
| `RequiredDateLabel` | string | yes | Concise date/condition text. |
| `StateLabel` | string | yes | Approved Vietnamese state. |
| `ScheduleContext` | string | yes | Minimum plan/Actual context required to act. |
| `SourceOrder` | integer | yes | Tie-breaker after category and date. |

### Membership and deduplication

- The operating window is `[SourceReportingDate, SourceReportingDate + 29]`.
- Supported decisions and blockers remain eligible even when their target plan
  lies outside the window.
- Unfinished overdue Delivery Cards remain eligible.
- Active work remains eligible when supported by official state/evidence.
- Planned work and milestones qualify when their planned interval/date
  intersects the window.
- Items with the same stable target are merged; decision/blocker wording takes
  precedence, followed by overdue, active, and planned context.
- Ordering is category, supported required date (known before unknown), then
  canonical source order.

## Report metadata

`ExecutiveReportMetadata` centralizes technical authority on the sixth sheet.

| Field | Type | Required | Meaning |
| --- | --- | --- | --- |
| `AuthorityLabel` | string | yes | Official source classification in reader language. |
| `SourceIdentity` | string | yes | Exact source commit/object identity. |
| `SnapshotId` | string | yes | Official snapshot identity. |
| `ProjectId` | string | yes | Canonical project identity. |
| `BaselineId` | string | yes | Baseline identity. |
| `BaselineVersion` | string | no | Source baseline version. |
| `ContractVersion` | string | yes | Manifest/report contract context. |
| `RegisterRevision` | integer | yes | Official execution-register revision. |
| `SourceReportingDate` | date | yes | Official status date. |
| `AnalysisAsOfDate` | date | yes | Analysis boundary. |
| `PlanningStart` / `PlanningFinish` | date | no | Immutable baseline range. |
| `Limitations` | ordered text collection | yes | Concise supported limitations, no repeated prose. |

Metadata is never repeated as boilerplate on the first five sheets. A short
`Cập nhật đến dd/MM/yyyy` label may appear where recency is necessary.

## Delivery-card detail

The existing `ExecutiveDeliveryCardDetail` keeps one row per canonical Delivery
Card and adopts the common reader-language and missing-data rules.

Required ordering is reader-first:

1. meaningful work name and hierarchy context;
2. owner, state, attention, progress;
3. plan, Actual, forecast, and effort facts;
4. update and dependency context;
5. stable identity and minimal provenance.

Blank typed dates/numbers remain blank. Missing evidence is communicated by an
adjacent label, never by numeric zero.

## Neutral workbook document extensions

### Workbook

| Field | Invariant |
| --- | --- |
| `Sheets` | Exactly six visible sheets in contract order. |
| `ActiveSheetIndex` | Exactly 0. |

### Worksheet additions

| Field | Type | Validation / meaning |
| --- | --- | --- |
| `ColumnGroups` | group collection | Non-overlapping, one-based ranges within used columns. |
| `AutoFilterRange` | range, optional | Header plus complete data population; no merged intersection. |
| `OutlineSummaryBelow` | boolean | `false` for WBS so parent rows precede hidden children. |
| `OutlineSummaryRight` | boolean | `false` for optional details so primary columns precede groups. |

### Row additions

| Field | Type | Validation / meaning |
| --- | --- | --- |
| `OutlineLevel` | integer 0–7 | WBS depth used by spreadsheet outline. |
| `Hidden` | boolean | Initially true for Delivery Card rows only. |
| `Collapsed` | boolean | True on a visible parent whose immediate child group opens collapsed. |

### Column group

| Field | Type | Validation / meaning |
| --- | --- | --- |
| `StartColumn` / `EndColumn` | integer | Inclusive one-based range after primary WBS columns. |
| `OutlineLevel` | integer 1–7 | Level 1 for each approved optional group. |
| `Hidden` | boolean | True on initial open. |
| `Collapsed` | boolean | Exposes the spreadsheet expand control. |

Approved WBS column groups are Plan, Actual, Relationships, and Evidence.

## State and safety rules

- Projection is pure: no field is written back to canonical, official,
  proposal, preview, or source state.
- A complete official snapshot is required before any report model is built.
- Sparse valid evidence produces an explicit shape or missing label.
- Contradictory official evidence that cannot satisfy these invariants produces
  a structured `executive-export` diagnostic and no workbook.
- The same report model yields deterministic workbook content.

