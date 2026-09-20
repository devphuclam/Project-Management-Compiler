# Implementation Plan: Executive Progress Report Export

**Branch**: `codex/feature005-executive-progress-report` | **Date**: 2026-09-20 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from `specs/005-executive-progress-report/spec.md`

## Summary

Add a second, presentation-only XLSX export for project sponsors and managers.
An `ExecutiveProgressReportProjector` will derive one deterministic
reader-facing model from the current official canonical project, approved
analysis, and management views. A separate `ExecutiveProgressXlsxExporter`
will render exactly four Vietnamese sheets with progressive disclosure, a
weekly schedule, evidence-safe progress wording, and print-ready formatting.
The existing seven-sheet CARIO + Gantt exporter and preview-import contract
remain unchanged.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: Existing .NET BCL `System.IO.Compression`, XML APIs,
ASP.NET Core minimal hosting, vanilla HTML/CSS/JavaScript, and the repository's
dependency-free executable test runner. No new package.

**Storage**: N/A. The report is generated in memory from the current official
snapshot and downloaded as a point-in-time file; it is not persisted or
rehydrated by the application.

**Testing**: Existing executable test project (`dotnet run --project
tests/ProjectManagementCompiler.Tests`) with red-green tests at projection,
Open XML package, application/API, import-boundary, and static browser seams;
full verification through `scripts/verify.ps1`.

**Target Platform**: Local loopback ASP.NET Core application on Windows and
other supported .NET 10 hosts; generated files target standard XLSX readers.

**Project Type**: Local web application and project-management compiler.

**Performance Goals**: Generate and return a supported official management
workbook within five seconds on the local application. Workbook construction
remains bounded by the already bounded official snapshot.

**Constraints**: Deterministic and offline; no AI-generated prose, Office COM,
third-party spreadsheet library, database, connector, Docker, public registry
access, source-repository write, or canonical/proposal mutation. Unknown values
must remain explicit. The executive workbook must not satisfy the CARIO preview
contract.

**Scale/Scope**: One official snapshot at a time; exactly four output sheets;
overview limited to four summary blocks and five attention items; phase and
milestone rows on the overview, work-package rows on the schedule, and
delivery-card rows on the detail sheet.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Canonical model before views — PASS**: The new projector consumes the
  existing `CanonicalProject`, `ManagementAnalysis`, and `ManagementViewSet`.
  The XLSX adapter consumes only the resulting executive projection and never
  reads source-specific manifest objects or CARIO rows.
- **II. Baseline and evidence are first-class — PASS**: Source reporting dates,
  baseline dates, recorded execution, derived schedule alerts, readiness, and
  unknown states remain separate. Percentage is omitted unless both actual and
  remaining effort satisfy the approved evidence rule.
- **III. Deterministic extraction — PASS**: Titles are cleaned mechanically,
  terminology is table-driven, attention ordering is stable, and prose comes
  from fixed factual templates. No AI/LLM inference is introduced.
- **IV. Test through deep interfaces — PASS**: Projection rules, workbook
  contract, official-only application selection, non-importability, and UI
  actions each have observable contract tests.
- **V. Restricted-environment delivery — PASS**: The design uses only the
  installed .NET/BCL and current web stack; no package or service is added.
- **VI. Explicit scope and safe failure — PASS**: No official snapshot means
  no executive export. Missing/ambiguous values stay visible, and the exporter
  cannot promote a preview, proposal, or output workbook to authority.

No constitution violation requires a complexity exception.

## Project Structure

### Documentation (this feature)

```text
specs/005-executive-progress-report/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── acceptance.md                  # created during final acceptance
├── contracts/
│   ├── executive-progress-workbook.md
│   └── executive-progress-http-ui.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/ProjectManagementCompiler/
├── Application/
│   ├── IProjectCompiler.cs
│   └── ProjectCompiler.cs
├── Management/
│   ├── ExecutiveProgressReportModel.cs
│   └── ExecutiveProgressReportProjector.cs
├── Outputs/
│   ├── CarioXlsxExporter.cs
│   └── ExecutiveProgressXlsxExporter.cs
├── Program.cs
└── wwwroot/
    ├── app.js
    ├── index.html
    └── styles.css

tests/ProjectManagementCompiler.Tests/
├── ExecutiveProgressTestFixtures.cs
├── ExecutiveProgressProjectionTests.cs
├── ExecutiveProgressXlsxTests.cs
├── ExecutiveProgressExportApplicationTests.cs
├── ExecutiveProgressUiTests.cs
├── XlsxPreviewImporterTests.cs
├── CarioXlsxTests.cs
└── Program.cs
```

**Structure Decision**: Keep semantic projection in `Management`, XLSX package
serialization in `Outputs`, and official-snapshot selection in `Application`.
Expose one composition method through `IProjectCompiler` and one official-only
HTTP endpoint. Keep the executive and technical exporters semantically
independent; do not refactor `CarioXlsxExporter` as part of this feature.

## Phase 0: Research Decisions

Phase 0 decisions are recorded in [research.md](research.md):

- use a separate executive projection and exporter rather than adding an
  executive mode to the CARIO workbook;
- gate the export on an official manifest snapshot at both application and
  endpoint seams;
- retain the BCL Open XML approach without modifying CARIO package code;
- define deterministic title, terminology, progress, ownership, readiness,
  attention ranking, and deduplication rules;
- use progressive disclosure and workbook print/view settings as part of the
  output contract, not as incidental styling.

All technical context questions are resolved; no `NEEDS CLARIFICATION` remains.

## Phase 1: Design Artifacts

- [data-model.md](data-model.md) defines the executive projection, progress
  evidence, schedule rows, attention items, terminology, invariants, and
  projection flow.
- [contracts/executive-progress-workbook.md](contracts/executive-progress-workbook.md)
  defines the exact four-sheet XLSX shape, ordering, language, layout, print,
  authority, and non-importability contracts.
- [contracts/executive-progress-http-ui.md](contracts/executive-progress-http-ui.md)
  defines the compiler method, official-only endpoint, file name, error shape,
  and separate UI controls.
- [quickstart.md](quickstart.md) defines runnable fixture checks, a real
  IDEAEngineering acceptance flow, visual review, and complete verification.

The constitution check remains PASS after Phase 1. The design introduces no
new authority, persistence owner, source adapter, or technical workbook schema.

## Implementation Sequence

1. Run Spec Kit analyze before touching production code. Because analyze is
   strictly read-only, report any inconsistency and obtain explicit user
   approval before a separate remediation edit. `speckit-converge` is reserved
   for its documented post-implementation gate.
2. Add failing projection tests for source-date handling, mechanical title
   cleanup, Vietnamese terminology, owner display, evidence-safe percentage,
   readiness separation, attention filtering/ranking/deduplication, and stable
   ordering; observe RED before implementing the projector.
3. Implement the smallest `ExecutiveProgressReportModel` and projector needed
   to make the focused projection tests green, then run related management
   regressions.
4. Add failing XLSX package tests for the exact sheet contract, content
   boundaries, styles, weekly/month grouping, report-date marker, active sheet,
   freeze panes, 100% zoom, and landscape fit settings; observe RED before
   implementing the exporter.
5. Implement the separate BCL-based executive exporter without changing the
   CARIO exporter; run focused package tests and existing CARIO regressions.
6. Add failing application/API/UI tests for official-only selection, dated file
   naming, separate Vietnamese actions, preview rejection, no state mutation,
   and unchanged CARIO behavior; observe RED before implementing composition,
   endpoint, and UI changes.
7. Generate the real IDEAEngineering workbook, perform the timed and visual
   acceptance checks, run all focused and full verification commands, then run
   Spec Kit analyze again, Spec Kit converge, and code review.

## Complexity Tracking

No constitution violations or justified complexity exceptions are present.
