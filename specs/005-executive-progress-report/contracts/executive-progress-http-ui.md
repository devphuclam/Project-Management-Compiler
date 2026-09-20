# Contract: Executive Progress Export Application, HTTP, and UI

## Application seam

`IProjectCompiler` exposes a distinct executive export operation. Its input is
an existing `CompilationResult`; it does not accept source paths, workbook
uploads, preview models, or proposals.

The operation must fail closed unless all are true:

- `Project.ImportMetadata` exists;
- metadata classification is `OfficialCommit`;
- source reporting date is present and valid;
- compiled analysis and management views are available.

On success it projects once and passes the immutable projection to
`ExecutiveProgressXlsxExporter`. It does not call the CARIO mapper/exporter and
does not modify `CompilationResult` or `CompilerApplicationState`.

## HTTP download

### `GET /api/exports/executive-progress.xlsx`

**Selection**: current official result only. Active manifest preview and active
XLSX preview are ignored even when currently displayed in the browser.

**Success**:

- status: `200 OK`;
- content type:
  `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`;
- body: executive workbook bytes;
- download name:
  `<FilesystemSafeProjectName>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`, where the date
  is the official source reporting date.

**No project**:

```json
{
  "code": "NO_PROJECT",
  "message": "No compiled project is loaded.",
  "phase": "executive-export"
}
```

Return `404 Not Found`.

**No official manifest snapshot**:

```json
{
  "code": "NO_OFFICIAL_SNAPSHOT",
  "message": "An official manifest snapshot is required for the management report.",
  "phase": "executive-export"
}
```

Return `404 Not Found`. A preview does not qualify.

**Official snapshot is incomplete for this export**:

Return `422 Unprocessable Entity` with the application's structured error shape,
the stable code `EXECUTIVE_EXPORT_INCOMPLETE_OFFICIAL`, phase
`executive-export`, and no partial workbook. This response covers an official
result that lacks valid source reporting metadata, an analysis as-of date, or
the compiled management views required by the projection. No state changes
occur.

## UI actions

The header exposes two separate controls:

1. primary: `Xuất báo cáo tiến độ` →
   `/api/exports/executive-progress.xlsx`;
2. secondary: `Xuất dữ liệu CARIO + Gantt` →
   `/api/exports/cario.xlsx`.

The management action is enabled only when an official snapshot is available.
Preview mode cannot redirect it to a preview export. The technical preview
export route and XLSX preview import flow remain separate and unchanged.

No redesign is part of this feature. The work is limited to clear labels,
action hierarchy, disabled/unavailable state, and current responsive wrapping.

## File-name rules

- Reuse the application's existing filesystem-safe project-name semantics.
- Preserve Vietnamese report wording without accents in the file suffix:
  `_BaoCaoTienDo_`.
- Format date as invariant `yyyy-MM-dd`.
- Do not use export wall-clock time, source commit, snapshot ID, or preview
  identity in the name.

## State and authority assertions

Before and after an export request, all of these remain semantically equal:

- current official canonical project and semantic digest;
- source execution snapshot;
- local proposal collection;
- active manifest preview and active XLSX preview;
- source repository contents and Git state.

The endpoint performs no save, import, proposal, or source-capture operation.
