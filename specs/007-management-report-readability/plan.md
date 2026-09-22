# Implementation Plan: Management Report Readability

**Branch**: `codex/feature007-management-report-readability` | **Date**:
2026-09-22 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from
`specs/007-management-report-readability/spec.md`

## Summary

Replace the existing five-sheet executive progress workbook in place with the
approved six-sheet Management Report. Preserve the official-only application
seam, canonical model, endpoint, filename, and technical CARIO + Gantt contract.
Add deterministic reader-language cleanup, explicit Actual evidence shapes, a
unified 30-day operating projection, a complete four-level management WBS, and
native spreadsheet outlines/filters. Compose each sheet from neutral report
projections and serialize it with the repository's existing BCL-only XLSX path.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: Existing ASP.NET Core minimal hosting and .NET BCL
`System.IO.Compression`, XML, globalization, and collection APIs. No new
dependency.

**Storage**: N/A. Report models and workbook bytes are generated in memory from
one immutable official `CompilationResult`; no report becomes application state
or an import source.

**Testing**: Existing dependency-free executable test project via
`dotnet run --project tests/ProjectManagementCompiler.Tests/ProjectManagementCompiler.Tests.csproj --no-restore`,
plus complete local verification through `scripts/verify.ps1`. Implementation
uses RED–GREEN–REFACTOR at policy, projection, workbook-document,
serialization, application, and compatibility seams.

**Target Platform**: Local loopback ASP.NET Core application on Windows and
other supported .NET 10 hosts; output targets standards-compliant XLSX readers.

**Project Type**: Local web application and deterministic project-management
compiler.

**Performance Goals**: Generate and return the accepted 6-phase, 35-work-
package, 53-delivery-card, 7-control-point report within five seconds. Daily
axes remain bounded by official attributable plan/Actual/forecast dates.

**Constraints**: Offline and deterministic; BCL-only; official-snapshot-only;
read-only; no source write-back; no AI/LLM rewriting; no fabricated Actual,
owner, progress, forecast, dependency, or explanation; no import authority;
normal-zoom readability takes precedence over forced one-page scaling.

**Scale/Scope**: One official project snapshot; exactly six report sheets; one
Project, all Phases, Work Packages, Delivery Cards, and schedule milestones;
full daily Gantt; exact 30-date operating window; WBS row/column outlines; at
most five overview actions; one detail row per Delivery Card.

## Constitution Check

*GATE: Passed before Phase 0 research and re-checked after Phase 1 design.*

- **I. Canonical model before views — PASS**: New reader, Actual, operating,
  WBS, and metadata types are output projections over `CompilationResult`.
  Source and CARIO objects do not enter worksheet composition.
- **II. Baseline and evidence are first-class — PASS**: Plan, direct Actual,
  roll-up coverage, forecast, attention, and missing evidence remain separate.
  Finish-only and effort-only evidence gain explicit shapes instead of inferred
  dates.
- **III. Deterministic extraction — PASS**: Name cleanup, WBS numbering,
  operating membership, ordering, labels, and layout use fixed rules. No LLM or
  generated narrative is introduced.
- **IV. Test through deep interfaces — PASS**: `ReaderFacingTextPolicy`,
  `ExecutiveDailyGanttProjector`, `ExecutiveOperatingProjector`,
  `ExecutiveWbsProjector`, worksheet composers, and the XLSX serializer expose
  narrow observable contracts with fixture-driven tests.
- **V. Restricted-environment delivery — PASS**: The design uses installed .NET
  and repository scripts only. No package/runtime installation, Docker,
  external API, Office automation, or secret is required.
- **VI. Explicit scope and safe failure — PASS**: Sparse valid evidence has
  explicit presentation; unsafe contradictions fail with structured
  diagnostics; non-official data and report imports remain excluded.

No constitution violation or complexity exception is required.

## Project Structure

### Documentation (this feature)

```text
specs/007-management-report-readability/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── executive-progress-export.md
│   └── management-report-workbook.md
├── checklists/
│   ├── requirements.md
│   └── report-readability.md
└── tasks.md
```

### Source Code (repository root)

```text
src/ProjectManagementCompiler/
├── Management/
│   ├── ReaderFacingTextPolicy.cs                 # new shared deterministic wording policy
│   ├── ExecutiveProgressReportModel.cs           # extend report/detail/metadata fields
│   ├── ExecutiveProgressReportProjector.cs       # compose all report projections
│   ├── ExecutiveDailyGanttModel.cs                # add explicit Actual shape/effort fields
│   ├── ExecutiveDailyGanttProjector.cs            # classify interval/point/effort/missing truth
│   ├── ExecutiveOperatingModel.cs                 # new unified 30-day item contract
│   ├── ExecutiveOperatingProjector.cs             # new membership/deduplication/order seam
│   ├── ExecutiveWbsModel.cs                       # new four-level management WBS contract
│   ├── ExecutiveWbsProjector.cs                   # new hierarchy/numbering/detail projection
│   └── WbsProjector.cs                            # regression-only; generic contract unchanged
├── Outputs/
│   ├── ExecutiveWorkbookDocument.cs              # add row/column outline and filter metadata
│   ├── ExecutiveProgressWorkbookComposer.cs      # six-sheet order/orchestration only
│   ├── ExecutiveOverviewWorksheetComposer.cs     # new summary sheet composer
│   ├── ExecutiveOperatingWorksheetComposer.cs    # new 30-day operating sheet composer
│   ├── ExecutiveGanttWorksheetComposer.cs        # extracted full daily Gantt composer
│   ├── ExecutiveWbsWorksheetComposer.cs          # new WBS outlines/groups/filter composer
│   ├── ExecutiveDetailWorksheetComposer.cs       # extracted reader-first detail composer
│   ├── ExecutiveReportInfoWorksheetComposer.cs   # new centralized metadata composer
│   ├── ExecutiveWorksheetFormatting.cs           # shared cells, dates, styles, and ranges
│   ├── ExecutiveProgressXlsxExporter.cs          # serialize outlines/filters deterministically
│   └── CarioXlsxExporter.cs                      # regression-only; do not modify
├── Application/
│   ├── IProjectCompiler.cs                       # signature unchanged
│   └── ProjectCompiler.cs                        # official validation unchanged unless test gap found
├── Program.cs                                    # endpoint/file naming unchanged
└── wwwroot/
    └── app.js                                    # export actions unchanged

tests/ProjectManagementCompiler.Tests/
├── ReaderFacingTextPolicyTests.cs
├── ExecutiveProgressTestFixtures.cs
├── ExecutiveDailyGanttProjectionTests.cs
├── ExecutiveOperatingProjectionTests.cs
├── ExecutiveWbsProjectionTests.cs
├── ExecutiveProgressProjectionTests.cs
├── ExecutiveProgressXlsxTests.cs
├── ExecutiveProgressExportApplicationTests.cs
├── ExecutiveProgressUiTests.cs
├── CarioXlsxTests.cs
├── XlsxPreviewImporterTests.cs
└── Program.cs

docs/runbook/mvp1-local.md
```

**Structure Decision**: Keep the existing compiler/application seams and add
report-specific projections rather than altering source or generic WBS
contracts. Reduce the current monolithic worksheet composer by extracting one
focused composer per sheet plus a small shared formatting helper. The top-level
composer remains the single authority for sheet order, and the exporter remains
the single authority for XLSX package/XML syntax.

## Architecture and Ownership

### Reader-facing text policy

`ReaderFacingTextPolicy` owns deterministic removal of recognized source noise
and fixed missing-data labels. It does not own source extraction, translation,
summarization, or spreadsheet cells. All executive projectors use it; worksheet
composers receive already-clean names.

### Daily Gantt projection

`ExecutiveDailyGanttProjector.Build(CompilationResult)` continues to own the
complete daily hierarchy, Plan/Actual/forecast facts, conservative roll-ups,
coverage, and near-term schedule selection. It adds explicit Actual
presentation kind and evidence labels so the composer does not infer evidence
shape from nullable fields.

### Operating projection

`ExecutiveOperatingProjector.Build(CompilationResult, ExecutiveDailyGanttProjection,
IReadOnlyList<ExecutiveAttentionItem>)` owns 30-day membership, stable-target
deduplication, category precedence, dates, and deterministic ordering. It does
not own cell widths, colors, or print settings.

### WBS projection

`ExecutiveWbsProjector.Build(CompilationResult)` owns WBS membership,
parentage, positional numbering, stable ordering, owner/state/progress,
attention, dependencies, evidence summary, and minimal source locator. It does
not include milestones, decide row hiding, or emit Open XML.

### Report projection

`ExecutiveProgressReportProjector.Build(CompilationResult)` remains the root
orchestrator. It validates required official metadata, invokes the focused
projectors, constructs centralized report metadata, and returns one immutable
`ExecutiveProgressReport`.

### Workbook composition

`ExecutiveProgressWorkbookComposer.Build(ExecutiveProgressReport)` invokes the
six focused worksheet composers and returns one `ExecutiveWorkbookDocument` in
exact contract order. Sheet composers own labels, rows, widths, merged ranges,
freeze panes, outlines, filters, and print settings. They do not inspect source
JSON, resolve authority, calculate progress, or write ZIP/XML.

### XLSX serialization

`ExecutiveProgressXlsxExporter.Export(ExecutiveProgressReport)` keeps its
existing signature. It serializes the neutral workbook document, including
row/column outlines, hidden/collapsed state, filters, panes, styles, types,
merges, page settings, and deterministic package metadata.

## Planned Interfaces

```csharp
public static class ReaderFacingTextPolicy
{
    public static string CleanName(string? sourceText, string? exactId = null);
    public static string OwnerOrMissing(string? owner);
}

public enum ExecutiveActualPresentationKind
{
    RecordedInterval,
    OpenRecordedInterval,
    CompletionPoint,
    EffortOnly,
    None
}

public sealed class ExecutiveOperatingProjector
{
    public IReadOnlyList<ExecutiveOperatingItem> Build(
        CompilationResult result,
        ExecutiveDailyGanttProjection dailyGantt,
        IReadOnlyList<ExecutiveAttentionItem> attention);
}

public sealed class ExecutiveWbsProjector
{
    public ExecutiveWbsProjection Build(CompilationResult result);
}

internal sealed class ExecutiveProgressWorkbookComposer
{
    public ExecutiveWorkbookDocument Build(ExecutiveProgressReport report);
}
```

Signatures may be narrowed during TDD, but authority, inputs, and ownership in
this plan are normative. Detailed fields and invariants are defined in
[data-model.md](data-model.md).

## Phase 0: Research Decisions

[research.md](research.md) resolves all planning questions:

- replace the reader workbook in place;
- use the approved six-sheet journey and defer full Kanban;
- centralize deterministic reader-language cleanup;
- classify Actual interval, open interval, completion point, effort-only, and
  missing evidence explicitly;
- introduce a report-specific WBS projection without changing generic WBS;
- unify 30-day operating concerns with stable-target deduplication;
- add native row/column outlines and filters to the neutral workbook document;
- separate sheet composition from package serialization;
- preserve endpoint, authority, non-importability, and technical workbook
  compatibility.

No `NEEDS CLARIFICATION` remains.

## Phase 1: Design Artifacts

- [data-model.md](data-model.md) defines reader language, Actual evidence shape,
  WBS rows, operating items, metadata, detail ordering, workbook outlines, and
  safety invariants.
- [contracts/management-report-workbook.md](contracts/management-report-workbook.md)
  defines exact sheets, content, wording, WBS disclosure, Gantt truth, visual
  semantics, panes, filtering, print, and non-importability.
- [contracts/executive-progress-export.md](contracts/executive-progress-export.md)
  preserves application interface, route, authority, errors, immutability,
  performance, and technical-export compatibility.
- [quickstart.md](quickstart.md) defines baseline capture, RED–GREEN–REFACTOR
  sequence, focused test matrices, real-source flow, six-sheet visual review,
  performance/immutability checks, and final verification.

### Post-design constitution re-check

All six constitution principles remain PASS. Phase 1 introduces no source
adapter, canonical mutation, persistence owner, package dependency, authority
mode, remote service, report-import path, or source write-back. New output
models remain downstream projections of the canonical official snapshot.

## TDD Implementation Sequence

1. Capture build/test/status baseline and characterize the existing Feature 006
   workbook, route, official selection, and technical workbook compatibility.
2. Add RED reader-language tests for nested IDs, kind/Markdown noise,
   meaningful brackets, long Vietnamese names, and approved missing labels;
   implement the shared policy and migrate executive projections.
3. Add RED Actual-shape tests for complete, open, finish-only, effort-only, and
   missing evidence; extend the Gantt model/projector and keep roll-ups
   conservative.
4. Add RED WBS projection tests for exact 1/6/35/53 membership, stable
   parentage/numbering, clean names, owner/state/progress, dependency/evidence
   groups, milestone exclusion, and unresolved-parent handling; implement the
   focused WBS model/projector.
5. Add RED operating-projection tests for exact 30-day boundary, decision and
   blocker precedence, overdue/active/planned membership, deduplication,
   ordering, and empty state; implement the operating model/projector.
6. Add RED neutral-document tests for six sheets, row/column groups,
   hidden/collapsed state, filters, panes, and validation; extend the workbook
   model and XLSX serializer.
7. Add RED worksheet/package tests story by story; extract sheet composers and
   implement `Tổng quan`, `Điều hành 30 ngày`, `Gantt`, `WBS`, `Chi tiết công
   việc`, and `Thông tin báo cáo` in contract order.
8. Add RED application and compatibility regressions for unchanged action,
   route, filename, official-only selection, sparse-data success, fail-closed
   contradictions, immutability, non-importability, and unchanged technical
   CARIO + Gantt behavior.
9. Update the local runbook, generate a report from the approved real source,
   inspect all six sheets and print previews at 100% zoom, and record exact
   acceptance evidence.
10. Run focused tests, full `scripts/verify.ps1`, `git diff --check`, Spec Kit
    analyze/converge, and code review before any merge or push.

## Complexity Tracking

No constitution violations or justified complexity exceptions are present.
The new policy/projector/composer modules isolate independently testable
responsibilities that are already distinct in the approved contract; they do
not add a generic spreadsheet framework or alter canonical/source ownership.

