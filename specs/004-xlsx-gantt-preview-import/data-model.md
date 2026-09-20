# Data Model: XLSX Gantt Preview Import

## XLSX Preview

Temporary read-only projection of one validated PMC workbook.

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `fileName` | string | yes | Original selected name; must end in `.xlsx` case-insensitively. |
| `exportKind` | string | yes | Must equal `CARIO_GANTT`. |
| `contractVersion` | string | yes | Must equal supported version `1.0`. |
| `projectId` | string | yes | Non-empty stable canonical project identifier. |
| `projectName` | string | yes | Non-empty display name. |
| `sourceIdentity` | string | no | Exported source commit/identity; displayed as provenance only. |
| `snapshotId` | string | no | Exported snapshot identifier; not used to establish official authority. |
| `asOfDate` | ISO date | no | Exported reporting date when present; invalid dates reject import. |
| `dateAxis` | ordered ISO dates | yes | Non-empty, unique, ascending daily labels from `07_GANTT`. |
| `tasks` | ordered task rows | yes | Parsed from `01_TASKS`; required headers and stable task identities. |
| `ganttRows` | ordered Gantt rows | yes | Parsed from `07_GANTT`; exported values only. |
| `diagnostics` | diagnostic list | yes | Empty after success; populated on rejected import response. |

The model has no canonical baseline, analysis, source execution, proposal, or
mutable execution fields. It cannot be passed to `ProjectCompiler.BuildResult`
or `ExecutionProposalService`.

## Preview task row

Represents one exported row from `01_TASKS`.

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `workItemType` | string | yes | Non-empty exported type. |
| `taskId` | string | yes | Non-empty and unique within the workbook task table. |
| `phase` | string | no | Exported hierarchy label. |
| `workPackage` | string | no | Exported hierarchy label. |
| `title` | string | yes | Non-empty exported task name. |
| `plannedStart` | ISO date/text | no | Preserve exported value; malformed authored date rejects import. |
| `deadline` | ISO date/text | no | Preserve exported value; malformed authored date rejects import. |
| `status` | string | no | Exported initial state; not converted into execution authority. |
| `plannedEffortHours` | number/text | no | Preserve exported numeric value; malformed numeric value rejects import. |
| `sourceReference` | string | no | Preserve exported provenance text. |

The importer may retain additional contract columns as display values, but it
must not infer values for blank cells.

## Gantt row

Represents one exported row from `07_GANTT`.

| Field | Type | Required | Validation / meaning |
| --- | --- | --- | --- |
| `level` | integer/text | yes | Non-negative exported hierarchy depth. |
| `type` | string | yes | Non-empty exported node type. |
| `id` | string | yes | Non-empty exported node identity. |
| `name` | string | yes | Non-empty exported display name. |
| `lane` | enum text | yes | `PLAN`, `ACTUAL`, `ALERT`, or `MILESTONE`. |
| `status` | string | no | Preserve exported display value. |
| `ownerRole` | string | no | Preserve exported display value. |
| `planStart` / `planFinish` | date/text | no | Preserve exported values; malformed dates reject import. |
| `actualStart` / `actualFinish` | date/text | no | Display only; no actual inference or authority promotion. |
| `recordedPercent` | string/number | no | Preserve `Recorded %` or `Not recorded` semantics. |
| `critical` | string | no | Preserve exported display value. |
| `evidence` | string | no | Preserve exported display value. |
| `sourceReference` | string | no | Preserve exported provenance text. |
| `dailyCells` | ordered daily display cells | yes | Aligned one-to-one with `dateAxis`; no recomputation. |

## Workbook contract marker

Reserved rows in `05_PROJECT_INFO` identify the producer and version:

| Field | Required value |
| --- | --- |
| `PMC_EXPORT_KIND` | `CARIO_GANTT` |
| `PMC_EXPORT_CONTRACT_VERSION` | `1.0` |
| `PMC_PROJECT_ID` | Equal to preview `projectId`. |
| `PMC_PROJECT_NAME` | Equal to preview `projectName`. |

Missing, duplicated, or conflicting marker rows reject the workbook.

## State transitions

```text
No preview
  --valid upload--> Active preview
  --invalid upload--> No preview (or previous active preview unchanged)

Active preview
  --valid upload--> Replacement active preview
  --invalid upload--> Same active preview
  --clear--> No preview
  --official manifest import--> No preview
  --application restart--> No preview
```

The official compiler state and local proposals are independent of every preview
transition.
