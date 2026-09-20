# XLSX Gantt Preview Import Design

**Status:** Approved for implementation by the user on 2026-09-20

## Context

The Project Management Compiler now exports an official-source workbook named
`<ProjectName>_CARIO_GANTT.xlsx`. The workbook contains six CARIO-oriented
worksheets and a daily `07_GANTT` worksheet. The current application imports a
repository manifest, not an XLSX file, so an exported workbook cannot currently
be opened back inside the application.

The requested behavior is a read-only round-trip preview. The workbook must
remain an output artifact and must never become a competing source of truth for
the canonical project, source execution, analysis, or execution proposals.

## Goals

- Allow a user to choose a workbook produced by the Project Management
  Compiler from the browser UI.
- Validate the workbook as a bounded PMC export before displaying it.
- Reconstruct the exported task table and daily Gantt projection faithfully
  enough to display them in a read-only preview.
- Keep the official manifest snapshot and its derived views unchanged.
- Make invalid, tampered, incomplete, oversized, or malformed workbooks fail
  closed with actionable diagnostics.
- Keep the feature dependency-free by using the existing BCL ZIP and XML
  facilities.

## Non-goals

- Importing arbitrary third-party Excel Gantt files.
- Turning an XLSX file into a `CanonicalProject`.
- Recomputing analysis, CPM, alerts, actuals, or proposals from workbook data.
- Writing back to IDEAEngineering or to the uploaded workbook.
- Persisting an XLSX preview across application restarts.
- Adding a database, connector, Office COM dependency, or third-party Excel
  package.

## Rejected approaches

### Parse only `07_GANTT`

This would make the visual path short, but it would not validate project
identity, task provenance, or the rest of the workbook contract. It also makes
it easy for a damaged workbook to look plausible. The importer will require the
full exported contract and use `07_GANTT` as the visual projection within that
validated package.

### Hydrate `CanonicalProject` from Excel

This would reuse existing renderers, but it would incorrectly treat an output
projection as authoritative input. It could also create execution or proposal
semantics that the workbook does not own. The preview model will remain a
separate application state.

### Office automation or a spreadsheet package

The application already writes the narrow Open XML package with BCL APIs. A
new runtime dependency would add deployment and trust cost without solving a
requirement. The importer will use `System.IO.Compression` and XML APIs only.

## Architecture

### `XlsxPreviewImporter`

Add a focused application/output adapter that accepts a bounded byte stream and
returns either a validated `XlsxPreviewModel` or structured diagnostics. It will
perform the following stages in order:

1. Enforce the request and archive-entry ceilings before materializing entry
   bodies.
2. Open the ZIP package and require the exact seven workbook sheets:
   `01_TASKS`, `02_ASSIGNMENTS`, `03_CHILDREN_MILESTONES`,
   `04_DEPENDENCIES`, `05_PROJECT_INFO`, `06_IMPORT_WARNINGS`, and `07_GANTT`.
3. Validate the PMC marker and contract version in `05_PROJECT_INFO`.
4. Validate required headers and row shapes in `01_TASKS` and `07_GANTT`.
5. Parse project identity, provenance, task rows, the daily date axis, and Gantt
   rows without recalculating schedule or execution semantics.
6. Return a model only after every required validation succeeds.

No importer path will call `CompilerApplicationState.Set`,
`ProjectCompiler.BuildImportedResult`, `ExecutionProposalService`, or any
analysis service.

### `XlsxPreviewModel`

The model is a read-only projection containing:

- workbook file name, contract version, export kind, project ID and project name;
- source identity, snapshot ID, and as-of value when present;
- the validated daily axis from `07_GANTT`;
- the exported Gantt rows and lane values from `07_GANTT`;
- the exported task rows from `01_TASKS`;
- non-authoritative validation diagnostics, which are empty for a successful
  import.

The model has no `CanonicalProject`, `ManagementAnalysis`, proposal collection,
or mutable execution state.

### Application state

Extend `CompilerApplicationState` with an `ActiveXlsxPreview` value and clear
operation. The state transitions are:

```text
XLSX upload
  -> validate and parse
  -> replace ActiveXlsxPreview only after complete success
  -> UI enters XLSX Preview mode

official manifest import
  -> clear ActiveXlsxPreview
  -> update official state as it does today

application restart
  -> no preview is hydrated
```

An invalid upload leaves both the current official state and any previously
valid preview unchanged.

### HTTP API

Add:

- `POST /api/xlsx-preview` using `multipart/form-data` with one `.xlsx` file;
- `DELETE /api/xlsx-preview` to clear the active preview;
- `GET /api/xlsx-preview` to retrieve the active preview for refreshes.

The POST response contains the read-only preview model. Validation failures use
the existing API error shape with an XLSX-preview phase and stable diagnostic
codes. The endpoint never mutates the official manifest result.

### UI

Add an `Import XLSX preview` file picker in the source-intake panel. On a
successful import:

- switch immediately to an `XLSX Preview` mode;
- show the file name, project identity, contract version, and a clear
  `Read-only · Non-authoritative` banner;
- render the daily Gantt and task table from the preview model;
- disable/hide source-execution proposal actions while preview mode is active;
- provide `Official source` and `Clear preview` actions.

The existing official tabs and state remain available through the explicit
`Official source` mode. Preview data is never mixed into official dashboard,
analysis, alert, ACTUAL, or proposal views.

## Export contract changes

The exporter will keep exactly seven worksheets and add reserved provenance
rows to `05_PROJECT_INFO`:

| Field | Value |
| --- | --- |
| `PMC_EXPORT_KIND` | `CARIO_GANTT` |
| `PMC_EXPORT_CONTRACT_VERSION` | `1.0` |
| `PMC_PROJECT_ID` | canonical project ID |
| `PMC_PROJECT_NAME` | canonical project name |

The importer requires these markers and the exact contract version. The
currently downloaded workbook predates this marker and must be exported again
after implementation before it can be imported.

The importer requires the existing header contracts, including row 5 of
`07_GANTT`, the ISO daily date labels, lane column, identity columns, and the
`Recorded %` / `Not recorded` semantics. It preserves the exported values; it
does not infer missing actuals or recompute dates.

## Failure handling and safety

- Reject non-`.xlsx` uploads and packages that are not valid ZIP/Open XML
  workbooks.
- Reject missing or duplicate required sheets, marker mismatch, unsupported
  contract version, missing required headers, invalid dates, duplicate task
  identities, and malformed cell values.
- Enforce application-level request, entry-count, and entry-size ceilings
  independently of caller-controlled values.
- Do not follow external relationships or access paths outside the uploaded
  package.
- Do not partially render a workbook after any validation failure.
- Preserve the last valid official state and last valid preview on failure.

## Testing strategy

Implementation will use TDD in these groups, with a red regression test before
each production change:

1. Export marker and contract-version test.
2. Valid workbook importer test that checks project identity, task rows, daily
   axis, lanes, and `Recorded %`/`Not recorded` values.
3. Fail-closed tests for missing sheet, missing marker, unsupported version,
   malformed header, invalid date, duplicate identity, corrupt ZIP, and size
   ceiling.
4. Application-state tests proving a preview does not change official result,
   proposals, source execution, or analysis, and that official re-import clears
   the preview.
5. HTTP tests for upload, retrieval, clear, and unchanged official state on
   failed upload.
6. Browser/static behavior tests for the file picker, preview banner, mode
   switch, read-only task/Gantt rendering, and disabled proposal actions.
7. Full build, focused tests, related regressions, package verification, and
   live web verification.

## Acceptance criteria

- A newly exported PMC workbook can be uploaded through the UI and appears in
  `XLSX Preview` with the same daily Gantt rows and task data.
- The preview is visibly non-authoritative and read-only.
- Official source views and proposal state are byte/semantic-equivalent before
  and after a successful preview import.
- Invalid or tampered workbooks produce diagnostics and no partial preview.
- A new official manifest import clears the old preview.
- Restarting the application does not recreate the preview.
- No package, database, connector, or source-repository write is introduced.
