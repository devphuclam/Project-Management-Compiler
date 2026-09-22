# Contract: Executive Progress Export

## User action

The existing application action `Xuất báo cáo tiến độ` remains the only
Management Report download action. Its visible output changes from the Feature
006 five-sheet workbook to the Feature 007 six-sheet contract in
[management-report-workbook.md](management-report-workbook.md).

`Xuất dữ liệu CARIO + Gantt` remains a separate technical download and is not
renamed, merged, imported into, or repurposed by this feature.

## Application interface

The existing compiler operation remains:

```csharp
byte[] ExportExecutiveProgressXlsx(CompilationResult result)
```

It accepts an already compiled result, validates the official boundary, returns
one in-memory XLSX package, and does not mutate its input.

## HTTP download

The existing route remains:

```text
GET /api/exports/executive-progress.xlsx
```

Success response:

- status `200 OK`;
- media type
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`;
- file name `<SafeProjectName>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`, using the
  official source reporting date;
- body satisfying the six-sheet workbook contract.

No new endpoint, query parameter, upload action, or source-write operation is
introduced.

## Authority selection

- Use `CompilerApplicationState.CurrentOfficialResult` only.
- Do not fall back to an active working-tree preview, manifest candidate, XLSX
  preview, local proposal, or generic `Current` state.
- If an official result exists while another preview is active, export the
  official result.
- Local proposals and preview facts never appear as official Actual.

## Errors

| Condition | Status | Code | Phase |
| --- | --- | --- | --- |
| No project/state loaded | `404` | `NO_PROJECT` | `executive-export` |
| Some state loaded but no official snapshot | `404` | `NO_OFFICIAL_SNAPSHOT` | `executive-export` |
| Official snapshot lacks required metadata, reporting date, analysis date, or hierarchy/axis input | `422` | `EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL` | `executive-export` |
| Official Actual contains an unrepresentable contradiction | `422` | stable specific executive-export diagnostic | `executive-export` |

Sparse but valid Actual evidence is not an error. It produces the approved
interval, completion point, effort marker, or missing-evidence state.

## Read-only and reproducibility contract

Before and after export, all of these remain semantically identical:

- canonical project and baseline;
- official source execution;
- local execution proposals;
- active manifest/XLSX preview state;
- semantic digest;
- source repository HEAD, tracked diff, staged diff, and untracked inventory.

The same report projection produces deterministic workbook content with no
uncontrolled environment timestamp or machine-specific metadata.

## Non-importability

The Management Report:

- has the six reader-facing sheets, not the technical workbook contract;
- contains no PMC preview marker or round-trip schema;
- is rejected by the existing technical workbook preview importer;
- cannot create/update official execution, proposals, preview state, canonical
  state, or a future Project Setup Workspace.

## Performance

For the accepted IDEAEngineering source of 6 phases, 35 work packages, 53
delivery cards, and 7 control points, the local application returns the
workbook within five seconds in the acceptance environment.

## Compatibility

- `IProjectCompiler.ExportExecutiveProgressXlsx` signature is unchanged.
- Browser action, route, media type, and dated filename are unchanged.
- CARIO export route, seven-sheet technical package, preview markers, and
  preview-import behavior are unchanged.
- Feature 005/006 artifacts remain historical records; this contract supersedes
  only their reader-facing workbook shape.

