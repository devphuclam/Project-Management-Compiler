# Implementation Plan: Read-only XLSX Gantt Preview Import

**Branch**: `codex/excel-gantt-export` | **Date**: 2026-09-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/004-xlsx-gantt-preview-import/spec.md`

## Summary

Add a bounded XLSX input adapter for the compiler's own seven-sheet CARIO +
Gantt workbook. The adapter validates the package and provenance contract,
parses the exported task table and daily Gantt projection into a separate
read-only preview model, and exposes that model through a local upload flow.
The preview never becomes a `CanonicalProject`, never replaces the official
manifest result, and is cleared on a new official import or application restart.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: Existing BCL `System.IO.Compression`, XML APIs,
ASP.NET Core minimal hosting, and the repository's existing dependency-free
test runner. No new package.

**Storage**: In-memory `CompilerApplicationState` only for the active preview;
the uploaded workbook is not persisted and the source repository is never
modified.

**Testing**: Existing executable test project (`dotnet run --project
tests/ProjectManagementCompiler.Tests`) with red-green regression tests,
package-level Open XML checks, API behavior checks, and browser/static UI
checks.

**Target Platform**: Local loopback ASP.NET Core application on Windows and
other supported .NET 10 hosts.

**Project Type**: Local web application and project-management compiler.

**Performance Goals**: A valid workbook within the application size ceiling
should produce a visible preview within five seconds in the local application.
Validation must reject oversized package entries before reading their bodies.

**Constraints**: Offline and dependency-free; no Office COM, third-party Excel
library, database, connector, Docker, public registry access, or source-repo
write. The preview must be fail-closed and must not participate in official
analysis, source execution, or proposal state.

**Scale/Scope**: One active preview per application session; one seven-sheet
workbook per import; task and Gantt rows are bounded by the exporter contract
and application hard ceilings.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Canonical model before views — PASS**: The preview is an output-derived
  read-only model and is not presented as a canonical project. Official views
  continue to consume the existing canonical model.
- **II. Baseline and evidence are first-class — PASS**: Workbook values are
  labelled exported/provenance data; the feature does not infer actuals,
  forecasts, or source authority.
- **III. Deterministic extraction — PASS**: Parsing is deterministic and
  contract-driven; no AI/LLM inference or business-value guessing is used.
- **IV. Test through deep interfaces — PASS**: Import validation, state
  isolation, package export markers, and UI/API seams have observable tests.
- **V. Restricted-environment delivery — PASS**: The design uses installed
  .NET/BCL capabilities and introduces no package or external service.
- **VI. Explicit scope and safe failure — PASS**: Only the supported PMC
  workbook is accepted; malformed or ambiguous input fails closed with
  diagnostics and no partial preview.

No constitution violation requires a complexity exception.

## Project Structure

### Documentation (this feature)

```text
specs/004-xlsx-gantt-preview-import/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   └── xlsx-preview.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/ProjectManagementCompiler/
├── Application/
│   ├── CompilerApplicationState.cs
│   └── ManifestImport/
│       └── ManifestImportApplicationService.cs
├── Outputs/
│   ├── CarioXlsxExporter.cs
│   ├── GanttXlsxModel.cs
│   ├── XlsxPreviewImporter.cs
│   └── XlsxPreviewModel.cs
├── Program.cs
└── wwwroot/
    ├── app.js
    ├── index.html
    └── styles.css

tests/ProjectManagementCompiler.Tests/
├── CarioXlsxTests.cs
├── Program.cs
├── XlsxPreviewUiTests.cs
├── XlsxPreviewImporterTests.cs
└── XlsxPreviewApplicationTests.cs
```

**Structure Decision**: Keep the import parser beside the existing narrow XLSX
export adapter, keep the preview model and lifecycle separate from canonical
application results, expose a minimal local HTTP upload/clear/read surface, and
extend the existing vanilla browser UI. Tests cross the exporter/importer,
application-state, endpoint, and UI seams rather than inspecting private
implementation details.

## Phase 0: Research Decisions

Phase 0 decisions are recorded in [research.md](research.md). The approved
design resolves all scope, authority, storage, and dependency questions before
implementation:

- the supported input is only the PMC seven-sheet workbook;
- the marker stays in `05_PROJECT_INFO` so the sheet count remains seven;
- the preview is a separate in-memory projection;
- the existing BCL Open XML approach is retained;
- failures are structured and fail closed.

## Phase 1: Design Artifacts

- [data-model.md](data-model.md) defines the temporary preview and contract
  entities, validation rules, and lifecycle.
- [contracts/xlsx-preview.md](contracts/xlsx-preview.md) defines workbook,
  HTTP, and UI behavior contracts.
- [quickstart.md](quickstart.md) defines runnable validation scenarios and the
  expected test/verification commands.

The constitution check remains PASS after these design decisions: the new
model is a view/output projection, not a second canonical model or authority.

## Implementation Sequence

1. Run Spec Kit analyze and resolve artifact findings without production-code
   changes.
2. Run Spec Kit converge against the current codebase and append any remaining
   pre-implementation tasks; do not start code until the artifacts converge.
3. Add a failing exporter marker test and implement the marker rows.
4. Add a failing valid-import test and implement bounded package parsing.
5. Add failing fail-closed tests for package, marker, sheet, header, value, and
   size failures; implement diagnostics and ceilings.
6. Add failing application-state/API tests proving preview isolation and
   lifecycle; implement state and endpoints.
7. Add failing browser behavior tests and implement the source-intake picker,
   mode switch, preview rendering, and read-only controls.
8. Run focused tests after each red-green cycle, then related regressions,
   full verification, Spec Kit analyze, Spec Kit converge, code review, and
   final integration checks.

## Complexity Tracking

No constitution violations or justified complexity exceptions are present.
