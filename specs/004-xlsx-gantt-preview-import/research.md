# Research: XLSX Gantt Preview Import

## Decision: Accept only the compiler's seven-sheet workbook

**Rationale:** The application owns a narrow, deterministic export contract.
Accepting arbitrary Excel layouts would require an inference layer and would
make provenance and semantic validation unreliable. The importer will require
the exact seven sheet names and required headers already produced by the
exporter.

**Alternatives considered:**

- Parse any workbook with a recognizable Gantt header: rejected because it
  would not establish project identity or source provenance.
- Accept only `07_GANTT`: rejected because the rest of the workbook contract
  carries task identity and provenance needed to validate the projection.

## Decision: Add a provenance marker inside `05_PROJECT_INFO`

**Rationale:** Reserved project-info rows preserve the existing seven-sheet
  workbook shape, require no new Open XML part, and remain inspectable in Excel.
  The importer requires `PMC_EXPORT_KIND=CARIO_GANTT` and
  `PMC_EXPORT_CONTRACT_VERSION=1.0` before it accepts a preview.

**Alternatives considered:**

- Add an eighth hidden metadata sheet: rejected because it changes the existing
  seven-sheet contract and complicates compatibility.
- Use an external sidecar signature: rejected because the user selects one
  workbook and the application must validate it without another file.

## Decision: Keep preview state separate from canonical state

**Rationale:** The workbook is an output projection, not a source authority.
An independent `XlsxPreviewModel` makes it structurally impossible for the
preview to feed official analysis, source execution, or proposals through the
existing canonical pipeline.

**Alternatives considered:**

- Convert workbook rows into `CanonicalProject`: rejected because it would
  promote output data to authority and risk fabricating semantic fields.
- Reuse the official `CompilationResult`: rejected because it would blur the
  official/preview boundary and make proposal actions available in the wrong
  context.

## Decision: Use BCL ZIP/XML parsing with bounded reads

**Rationale:** The exporter already uses `System.IO.Compression` and XML APIs,
the project constitution forbids package installation, and the workbook shape
is narrow enough for deterministic parsing. Entry count, compressed/uncompressed
size, and request limits will be enforced before body reads where possible.

**Alternatives considered:**

- Office COM automation: rejected because it requires Excel and is unsafe for a
  local server process.
- A third-party spreadsheet package: rejected because it adds a prohibited
  dependency and is unnecessary for the supported Open XML subset.

## Decision: Fail closed and preserve the last valid state

**Rationale:** A partial Gantt can look authoritative. Any contract, package,
  marker, header, identity, date, or limit failure returns diagnostics and does
  not replace either the official project or an existing valid preview.

**Alternatives considered:**

- Best-effort rendering with warnings: rejected because it silently weakens the
  trust boundary and violates explicit safe-failure requirements.

## Decision: Session-only lifecycle

**Rationale:** The preview is a temporary inspection result. Clearing it on
  restart and after a new official manifest import prevents stale workbook data
  from being mistaken for the current source snapshot.

**Alternatives considered:**

- Persist the upload or hydrate it on startup: rejected because no persistence
  contract is needed and it would create a second project state to reconcile.

## Decision: UI uses an explicit preview mode

**Rationale:** A visible mode boundary is necessary for users to distinguish
  exported workbook data from official source data. The preview shows the daily
  Gantt and read-only task table, while source-execution proposal controls are
  unavailable.

**Alternatives considered:**

- Blend workbook rows into official tabs: rejected because the data authority
  and lifecycle would become ambiguous.
