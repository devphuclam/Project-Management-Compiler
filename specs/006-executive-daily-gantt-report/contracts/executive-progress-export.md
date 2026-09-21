# Contract: Executive Progress Export

## User action

The existing application action `Xuất báo cáo tiến độ` remains the only
management-report download action. Its visible meaning changes from the
Feature 005 four-sheet report to the Feature 006 five-sheet daily Gantt report.

`Xuất dữ liệu CARIO + Gantt` remains a separate technical download and is not
renamed, merged, or repurposed.

## Application interface

The existing compiler operation remains:

```csharp
byte[] ExportExecutiveProgressXlsx(CompilationResult result)
```

The method accepts only an already compiled result. It validates that the
result represents a complete official manifest snapshot with:

- import metadata;
- official-commit classification;
- source register reporting date;
- analysis as-of date;
- a baseline/hierarchy sufficient to create a daily axis.

The method returns one in-memory XLSX package and does not mutate its input.

## HTTP download

The existing download route remains:

```text
GET /api/exports/executive-progress.xlsx
```

Success response:

- status: `200 OK`;
- media type:
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`;
- file name: `<SafeProjectName>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`, where the date
  is the official source reporting date;
- body: workbook satisfying
  [executive-daily-gantt-workbook.md](executive-daily-gantt-workbook.md).

## Authority selection

- Use `CompilerApplicationState.CurrentOfficialResult` only.
- Do not fall back to active working-tree preview, manifest candidate, XLSX
  preview, local execution proposal, or generic `Current` state.
- If an official result exists while another preview is active, export the
  official result.

## Errors

| Condition | Status | Code | Phase |
| --- | --- | --- | --- |
| No project or state loaded | `404` | `NO_PROJECT` | `executive-export` |
| Some state loaded but no official snapshot | `404` | `NO_OFFICIAL_SNAPSHOT` | `executive-export` |
| Official snapshot lacks required metadata/date/axis input | `422` | `EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL` | `executive-export` |
| Official Actual contains an unrepresentable contradiction | `422` | a stable specific executive-export diagnostic | `executive-export` |

Sparse but valid Actual evidence is not an error. The workbook exports with
explicit missing-data wording.

## Read-only and reproducibility contract

Before and after export, all of these remain semantically identical:

- canonical project and baseline;
- official source execution;
- local execution proposals;
- active preview state;
- semantic digest;
- source repository HEAD, tracked diff, staged diff, and untracked inventory.

The same projected report produces deterministic package content except for no
uncontrolled timestamp or environment metadata.

## Non-importability

The management workbook:

- has five sheets, not the technical seven-sheet contract;
- contains no PMC preview markers;
- is rejected by the technical workbook preview importer;
- cannot create or update official execution, proposals, or canonical state.

## Performance

For the accepted IDEAEngineering source (6 phases, 35 work packages, 53
delivery cards, and 7 control points), the local application returns the
workbook within five seconds under the acceptance environment.

## Compatibility

- `IProjectCompiler.ExportExecutiveProgressXlsx` signature remains unchanged.
- The browser action continues to use the existing route.
- CARIO export route, file name, seven-sheet package, preview markers, and
  preview-import behavior remain unchanged.
