# XLSX Preview Contract

## Scope

This contract describes the only workbook shape accepted by the XLSX preview
flow. It is not a general Excel import contract and it does not establish
source authority.

## Workbook package

The workbook MUST contain exactly these seven worksheets, with these names and
in this order:

1. `01_TASKS`
2. `02_ASSIGNMENTS`
3. `03_CHILDREN_MILESTONES`
4. `04_DEPENDENCIES`
5. `05_PROJECT_INFO`
6. `06_IMPORT_WARNINGS`
7. `07_GANTT`

The package MUST contain a valid workbook relationship graph and no unsupported
external relationship is followed by the importer.

## Provenance marker

`05_PROJECT_INFO` MUST contain one row for each marker below, using the normal
`Field`/`Value` columns:

| Field | Required value |
| --- | --- |
| `PMC_EXPORT_KIND` | `CARIO_GANTT` |
| `PMC_EXPORT_CONTRACT_VERSION` | `1.0` |
| `PMC_PROJECT_ID` | Non-empty and equal to the workbook project identity. |
| `PMC_PROJECT_NAME` | Non-empty and equal to the workbook project name. |

Duplicate or conflicting marker rows are invalid.

## Required sheet headers

`01_TASKS` MUST have this header row:

```text
Work Item Type, Task ID, Phase, Work Package, Nội dung công việc,
Ngày bắt đầu dự kiến, Deadline, Mức độ ưu tiên, Đơn vị / Phòng ban, Ban,
Ghi chú, Trạng thái ban đầu, Planned Effort (hours), Baseline / Analysis State,
Source Reference
```

`07_GANTT` MUST have the identity and semantic headers below in columns A–O,
followed by one or more ISO `yyyy-MM-dd` daily date headers:

```text
Level, Type, ID, Name, Lane, Status, Owner / Role, Plan Start, Plan Finish,
Actual Start, Actual Finish, Recorded %, Critical, Evidence, Source Reference
```

The importer MUST preserve the row order, identity values, lane labels,
display values, and daily cell values from the workbook. It MUST NOT calculate a
new schedule or fill a blank actual/percentage value.

## POST `/api/xlsx-preview`

The request is `multipart/form-data` with one file field named `file`.

Success (`200`) returns a JSON preview object containing:

- `fileName`, `exportKind`, `contractVersion`;
- `projectId`, `projectName`, `sourceIdentity`, `snapshotId`, `asOfDate`;
- `dateAxis`, `tasks`, and `ganttRows`;
- `readOnly: true` and `authoritative: false`.

Failure (`422`) returns the existing API error shape:

```json
{
  "code": "INVALID_XLSX_PREVIEW",
  "message": "The selected workbook did not satisfy the PMC preview contract.",
  "phase": "xlsx-preview",
  "diagnostics": [
    {
      "code": "PMC-XLSX-...",
      "severity": "ERROR",
      "message": "..."
    }
  ]
}
```

Failed requests MUST NOT replace the official result or an existing valid
preview.

## GET `/api/xlsx-preview`

- `200`: returns the active preview with `readOnly: true` and
  `authoritative: false`.
- `404`: returns `NO_ACTIVE_XLSX_PREVIEW` when no preview exists.

## DELETE `/api/xlsx-preview`

- `204`: clears the active preview.
- The endpoint does not alter the official source result or proposals.

## UI contract

- The source-intake panel exposes a file picker labelled `Import XLSX preview`.
- Successful import activates a visibly labelled `XLSX Preview` mode.
- The mode displays `Read-only` and `Non-authoritative` labels, the workbook
  identity, daily Gantt, and task table.
- Proposal/execution update controls are unavailable in preview mode.
- `Official source` restores the official mode without mutating it.
- `Clear preview` removes the preview and leaves official state available.
