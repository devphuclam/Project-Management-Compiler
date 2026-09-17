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
| `plannedWorkHours` | number | 512 |
| `reserveHours` | number | 88 |
| `capacityHours` | number | 600 |
| `validationState` | enum | `KNOWN`, `WARNING`, `BLOCKED`, or `UNKNOWN` |

### Phase, WorkPackage, DeliveryCard

All three records use `id`, `name`, `parentId`, `phaseId`, `sourceReferences`,
and kind-specific fields.

| Record | Required fields | Effort rule |
|---|---|---|
| `Phase` | `id`, `name`, `plannedStart`, `plannedFinish`, `plannedWorkHours`, `reserveHours`, `milestoneIds` | Phase effort is source capacity context, not a fourth task level |
| `WorkPackage` | `id`, `name`, `phaseId`, `plannedStart`, `plannedFinish`, `plannedHours`, `dependencyIds`, `completionCondition`, `deliveryCardIds` | Appendix A is authoritative; contributes once to capacity totals |
| `DeliveryCard` | `id`, `workPackageId`, `phaseId`, `name`, `plannedStart`, `deadline`, `plannedHours`, `state`, `roleAssignmentIds` | Kanban detail supports card CPM; child hours are not added to parent hours |

The canonical model must retain both the work-package effort and the detailed
card effort. `analysis.effortAccounting` names the chosen roll-up level and any
reconciliation warnings.

### MilestoneDecision

| Field | Type | Rule |
|---|---|---|
| `id` | string | `G-D0`, `G-MS0` … `G-MS5` |
| `kind` | enum | `DECISION` or `MILESTONE` |
| `parentId` | string/null | Phase or work package context |
| `name` | string | Source-authored card title |
| `plannedDate` | date | Zero-duration source date |
| `state` | ExecutionState | Starts `NOT_STARTED` |
| `dependencyIds` | string[] | Gate prerequisites |
| `plannedHours` | number | Always `0` for current source |

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
plannedHours: number | null
plannedState: KNOWN | UNKNOWN
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
`remainingHours`, `consumptionState`, and source references.

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
- diagnostics generated by calculation.

Calculated analysis is replaceable and is never treated as source authority.
