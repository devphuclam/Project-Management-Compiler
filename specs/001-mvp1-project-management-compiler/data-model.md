# MVP1 Canonical Data Model

The public snapshot contract is `schemaVersion: "1.0"`. IDs are stable within
one project snapshot and are source-derived where the source provides an ID.
Dates use ISO `YYYY-MM-DD`; hours are decimal numbers; missing data is `null`
plus an explicit data-state field where interpretation matters.

## Root document

```json
{
  "schemaVersion": "1.0",
  "project": {},
  "sources": [],
  "baseline": {},
  "phases": [],
  "workPackages": [],
  "deliveryCards": [],
  "milestones": [],
  "dependencies": [],
  "responsibilityRoles": [],
  "assignments": [],
  "capacity": {},
  "reserve": {},
  "policies": {},
  "executionOverlay": {
    "records": []
  },
  "provenance": [],
  "warnings": [],
  "analysis": {}
}
```

## Entities

### Project

| Field | Type | Rule |
|---|---|---|
| `id` | string | Stable neutral ID; fixture uses `idea-ddm-technical-pilot-2026` |
| `name` | string | Source project name, not the provisional compiler name |
| `description` | string/null | Source description when present |
| `targetDate` | date/null | Authoritative target date |
| `sourceIds` | string[] | References to `ProjectSource` records |

### ProjectSource

| Field | Type | Rule |
|---|---|---|
| `id` | string | Stable source ID |
| `kind` | enum | MVP1 value `repository` |
| `repository` | string | URL or source label; no secret or absolute temp path |
| `resolvedRef` | string/null | Commit or ref when available |
| `captureState` | enum | `KNOWN`, `BLOCKED`, or `UNKNOWN` |
| `capturedAtUtc` | datetime/null | Capture metadata only; excluded from semantic equivalence and content digests |
| `documents` | SourceDocument[] | Discovered document metadata |

### ProjectBaseline

| Field | Type | Rule |
|---|---|---|
| `id` | string | `IE-PLAN-DEC2026-002` |
| `version` | string | `0.1` |
| `status` | string | Source state, currently `Draft` |
| `authorityDocumentId` | string | Points to DOC-07 source document |
| `planningStart` / `planningFinish` | date | 2026-09-18 / 2026-12-31 |
| `targetDate` | date | 2026-12-31 |
| `plannedEffortHours` | number | 512 |
| `reserveHours` | number | 88 |
| `capacityHours` | number | 600 |
| `validationState` | enum | `KNOWN`, `WARNING`, `BLOCKED`, or `UNKNOWN` |

### Phase, WorkPackage, DeliveryCard

All three records use `id`, `name`, `parentId`, `phaseId`, `sourceReferences`,
and kind-specific fields.

| Record | Required fields | Effort rule |
|---|---|---|
| `Phase` | `id`, `name`, `plannedStart`, `plannedFinish`, `plannedEffortHours`, `plannedDurationWorkingMinutes`, `durationState`, `reserveHours`, `milestoneIds` | Phase effort is source capacity context, not a fourth task level; its authored dates remain baseline |
| `WorkPackage` | `id`, `name`, `phaseId`, `plannedStart`, `plannedFinish`, `plannedEffortHours`, `plannedDurationWorkingMinutes`, `durationState`, `dependencyIds`, `completionCondition`, `deliveryCardIds` | Appendix A is authoritative; contributes once to authoritative effort totals |
| `DeliveryCard` | `id`, `workPackageId`, `phaseId`, `name`, `plannedStart`, `plannedFinish`, `plannedEffortHours`, `plannedDurationWorkingMinutes`, `durationState`, `state`, `roleAssignmentIds` | Kanban detail supports card CPM; child effort is not added to parent effort |

The canonical model must retain both the work-package effort and the detailed
card effort. `analysis.effortAccounting` names the chosen roll-up level and any
reconciliation warnings.

Authored planned dates are nullable. A missing date is `null` with an explicit
unknown/invalid diagnostic; `0001-01-01` is never a business date. Work-package
dates may legitimately be absent when Appendix A does not author them. A source
delivery-card state is nullable and carries a data state so missing, `NOT-RUN`,
and explicit `NOT_STARTED` remain distinct.

`plannedEffortHours` is authored work effort and is used for capacity/load.
`plannedDurationWorkingMinutes` is authored elapsed working time and is used
for dependency CPM. The compiler never derives duration from effort unless the
source explicitly states that the conversion is safe. `plannedStart` and
`plannedFinish` are immutable baseline dates; calculated earliest/latest dates,
float, and forecast dates live only under `analysis`.

### ExecutionOverlay and ExecutionRecord

`ExecutionOverlay` is mutable user-maintained evidence layered over the
immutable planning baseline. It is keyed by `workItemId`, which must resolve to
an executable delivery card. It does not replace `DeliveryCard.state` from the
source baseline; the effective authored execution state is read from the
overlay when a manual update exists and otherwise remains the source state.

```text
ExecutionOverlay
  records: ExecutionRecord[]

ExecutionRecord
  workItemId: string
  executionState: NOT_STARTED | IN_PROGRESS | COMPLETED | SUSPENDED | CANCELLED
  actualStart: date | null
  actualStartState: KNOWN | UNKNOWN | NOT_RUN | INVALID
  actualFinish: date | null
  actualFinishState: KNOWN | UNKNOWN | NOT_RUN | INVALID
  actualEffortHours: number | null
  actualEffortState: KNOWN | UNKNOWN | NOT_RUN | INVALID
  remainingEffortHours: number | null
  remainingEffortState: KNOWN | UNKNOWN | NOT_RUN | INVALID
  lastUpdatedAt: datetime | null
  note: string | null
  evidenceReference: SourceReference | null
```

The record is valid only when actual finish is not before actual start, effort
values are non-negative finite numbers, and state/date combinations follow the
manual-update rules. A completed record requires an actual finish; an in-
progress record requires an actual start; suspended and cancelled records do
not receive fabricated actual dates. Empty planning-only overlays retain
explicit `UNKNOWN`/`NOT_RUN` states rather than inferred actuals.

Actual effort is not actual duration. Actual duration is derived only from
actual dates and the project working calendar, or from actual start through an
explicit as-of date for an in-progress card. The overlay never contains
`OVERDUE` or `AT_RISK` as execution state.

### MilestoneDecision

| Field | Type | Rule |
|---|---|---|
| `id` | string | `G-D0`, `G-MS0` … `G-MS5` |
| `kind` | enum | `DECISION` or `MILESTONE` |
| `parentId` | string/null | Phase or work package context |
| `name` | string | Source-authored card title |
| `plannedDate` | date | Zero-duration source date |
| `state` | ExecutionState/null | Explicit recognized source state; absent or unrecognized source state remains null/unknown |
| `dependencyIds` | string[] | Gate prerequisites |
| `plannedEffortHours` | number | Always `0` for current source |
| `plannedDurationWorkingMinutes` | number | Always `0` for current source |

### Dependency

```text
subjectId, subjectKind, predecessorId, predecessorKind,
dependencyType, analysisEligible, validationState, sourceReferences
```

`dependencyType` is `FS` in the supported graph. Work-package dependencies are
retained for baseline trace but are not analysis-eligible when their equivalent
card edges are present.

### Estimate

```text
plannedEffortHours: number | null
plannedEffortState: KNOWN | UNKNOWN
plannedDurationWorkingMinutes: number | null
plannedDurationState: KNOWN | UNKNOWN | NOT_RUN | INVALID
plannedStart: date | null
plannedFinish: date | null
actualHours: number | null
actualState: KNOWN | UNKNOWN | NOT_RUN
remainingHours: number | null
remainingState: KNOWN | UNKNOWN | NOT_RUN
completionEvidenceState: KNOWN | UNKNOWN | NOT_RUN
```

### ResponsibilityRole and Assignment

`ResponsibilityRole` contains either a CARIO code (`A`, `R+`, `R`, `C`, `I`,
`O`) or a project logical role (`LEAD`, `PDA`, `PROC`, `DEV2`, `QLHT`, `HTKT`,
`SPEC`, `PILOT`) plus its source meaning.

`Assignment` contains `workItemId`, `logicalRoleCode`, `carioRoleCode`,
`concreteIdentity`, `mappingStatus`, and `sourceReferences`. A concrete identity
is nullable and is never inferred.

### CapacityPlan, Calendar, Reserve

`CapacityPlan` contains capacity hours, source resource policy, resource
logical role, and `effortAccountingLevel`. `Calendar` contains working weekdays
and hours per working day. `Reserve` contains `initialHours`, `consumedHours`,
`remainingHours`, `consumptionState`, and source references. For the current
baseline, initial reserve is known as 88 hours; consumption is not proved by
source evidence, so consumed and remaining values are null with
`NOT_RUN`/`UNKNOWN` state rather than fabricated zero values.

### Provenance and diagnostics

`SourceReference` contains:

```text
sourceId, repository, resolvedRef, relativeFile,
section, table, item, extractionRule, authorityRank,
confidenceState, validationState
```

`ImportWarning` contains `id`, `severity`, `code`, `message`, `affectedIds`, and
`sourceReferences`. Diagnostics are part of the persisted contract.

## Execution state and derived state

Authored state is one of `NOT_STARTED`, `IN_PROGRESS`, `COMPLETED`, `SUSPENDED`,
or `CANCELLED`. `OVERDUE` is never persisted as authored state. It is derived
from `currentDate > deadline` and state not completed/cancelled.

## ManagementAnalysis

The analysis section is calculated from the immutable baseline and contains:

- dependency CPM state and node metrics;
- dependency critical-path IDs;
- baseline finish and calculated finish;
- baseline-vs-calculated variance;
- resource/baseline schedule constraint summary;
- forecast state and value;
- capacity/load summary;
- health metrics and their rule IDs;
- effort-accounting reconciliation;
- execution status counts and per-work-item schedule variance;
- structured alerts with `START_DELAY`, `OVERDUE`, `COMPLETED_LATE`,
  `COMPLETED_ON_TIME`, `SUSPENDED`, `CANCELLED`, and conservative `AT_RISK`
  conditions;
- the explicit `asOfDate` used to derive time-sensitive conditions;
- diagnostics generated by calculation.

Calculated analysis is replaceable and is never treated as source authority.
Alerts and variance are derived from the baseline, execution overlay, calendar,
dependency graph, and explicit as-of date. They are not persisted as source
authority and may be recalculated after JSON reopen.

## Structural validity and retained source evidence

Reopen and normal analysis fail when a structural invariant is violated:
malformed schema or required fields, duplicate IDs, impossible parent/phase
membership, nonexistent work-package/card or assignment references, invalid
baseline field types, or any other required relationship that the canonical
model declares structural. A source dependency target that is absent from the
source may be retained for provenance only when it is marked
`validationState: INVALID_SOURCE_EVIDENCE`, `analysisEligible: false`, and has
an explicit diagnostic; it is excluded from CPM. An unmarked missing reference
is a structural failure.

Work-package dependencies remain a traceability graph owned by Appendix A.
Delivery-card and milestone dependencies form the detailed execution graph.
Known milestone/gate IDs may be predecessors of delivery cards or other gates
and are eligible for CPM when their durations are safe. Equivalent edges are
not silently double-fed into CPM; source work-package edges remain visible with
their own provenance and analysis eligibility.

The authoritative work-package effort roll-up for the current fixture is
exactly 512 hours. Card-level effort remains available for detail and
reconciliation but is never added to that total a second time.
