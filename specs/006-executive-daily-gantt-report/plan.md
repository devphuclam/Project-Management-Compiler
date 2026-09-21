# Executive Daily Gantt Report Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use
> `superpowers:executing-plans` to implement this plan task-by-task. This
> project owner has previously asked to avoid unnecessary subagent fan-out.
> Track execution through the checkboxes in [tasks.md](tasks.md).

**Goal:** Upgrade the existing official management-report export to one
five-sheet workbook with a daily hierarchical Gantt, evidence-backed Actual
lanes, an exact 30-day operating view, and truthful progress coverage.

**Architecture:** Keep the existing official-only application seam and enrich
the immutable executive report projection. A focused daily-Gantt projector
derives Plan, Actual, forecast, coverage, and near-term rows; a workbook
composer maps the report into five reader-facing sheets; the XLSX exporter
serializes that neutral workbook document with the existing BCL-only package
approach.

**Tech Stack:** C# on .NET 10, ASP.NET Core minimal hosting, BCL
`System.IO.Compression` and XML APIs, vanilla browser shell, and the existing
dependency-free executable test runner.

**Spec:** [spec.md](spec.md)

## Global Constraints

- Only `CurrentOfficialResult` and approved deterministic analysis may feed the
  management workbook; proposals and previews never become official Actual.
- The existing `Xuất báo cáo tiến độ` action and dated file naming remain the
  single management-export path.
- The technical CARIO + Gantt workbook and its seven-sheet preview-import
  contract remain unchanged.
- No package, runtime, Office automation, network service, database, Docker,
  source write-back, or generated prose may be added.
- Plan-bar position encodes dates; effort percentage is a separate value and
  never changes bar length.
- Missing execution evidence stays `Chưa cập nhật` or `Chưa đủ dữ liệu`; it
  never becomes zero, green, or `Chưa bắt đầu`.
- Implementation follows RED–GREEN–REFACTOR at the projection, workbook,
  application, and compatibility seams.

## Review Focus

- In-progress state without `actualStart`: no green lane, explicit missing-
  Actual text, and successful sparse-data export.
- Partial child coverage: no whole-scope percentage; coverage fraction remains
  visible on phase/work-package roll-ups.
- Source reporting date different from analysis as-of date: provenance and
  30-day window use source date; open Actual ends at analysis date.
- Same-day and out-of-baseline Actual/forecast dates: one daily cell remains
  visible without silently clipping the variance.
- Wide, multi-month schedules: frozen identity columns and headers remain
  readable, while the full Gantt paginates rather than shrinking to illegible
  print.
- Near-term clipping: crossing intervals use explicit continuation symbols;
  wholly pre-window overdue Plan receives a dated overdue marker rather than a
  fabricated blue bar inside the 30-date axis.

---

**Branch**: `codex/feature006-executive-daily-gantt-report` | **Date**:
2026-09-21 | **Spec**: [spec.md](spec.md)

**Input**: Feature specification from
`specs/006-executive-daily-gantt-report/spec.md`

## Summary

Feature 006 supersedes the four-sheet presentation contract from Feature 005
without adding another export endpoint. It preserves the official-only
projection boundary and expands the report to five sheets: `Tổng quan`,
`Gantt theo ngày`, `30 ngày tới`, `Vấn đề cần xử lý`, and `Chi tiết công
việc`. Plan and Actual appear as paired lanes in each logical Gantt band;
progress remains effort-based and carries explicit evidence coverage.

## Technical Context

**Language/Version**: C# on .NET 10 (`net10.0`)

**Primary Dependencies**: Existing .NET BCL compression/XML APIs, ASP.NET Core
minimal hosting, and repository-owned test/support code. No new dependency.

**Storage**: N/A. Workbook bytes are generated in memory from one immutable
official `CompilationResult`; the workbook is not persisted as application
state or accepted as input.

**Testing**: Existing executable test project (`dotnet run --project
tests/ProjectManagementCompiler.Tests/ProjectManagementCompiler.Tests.csproj
--no-restore`) with focused projection, workbook-package, application/export,
and compatibility tests; complete verification through `scripts/verify.ps1`.

**Target Platform**: Local loopback ASP.NET Core application on Windows and
other supported .NET 10 hosts; generated files target standard XLSX readers.

**Project Type**: Local web application and project-management compiler.

**Performance Goals**: Generate and return the accepted 6-phase, 35-work-
package, 53-card official report within five seconds. Daily axes remain bounded
by the accepted baseline plus attributable Actual/forecast variance dates.

**Constraints**: Deterministic, offline, BCL-only, presentation-only, official-
snapshot-only, no state mutation, no source write-back, and no semantic
inference from elapsed time or repository activity.

**Scale/Scope**: One official project snapshot; exactly five workbook sheets;
full Project → Phase → Work package → Delivery card → Milestone hierarchy;
one calendar date per Gantt column; exact 30-date operating window; up to five
overview attention items and a complete action sheet.

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-checked after Phase 1 design.*

- **I. Canonical model before views — PASS**: The daily-Gantt projector consumes
  `CompilationResult`, canonical planning/execution entities, and approved
  analysis. Workbook code consumes only the executive projection and does not
  inspect manifest JSON or CARIO rows.
- **II. Baseline and evidence are first-class — PASS**: Plan, Actual, forecast,
  progress, recording coverage, and derived variance have separate fields and
  display rules. Unrecorded evidence remains explicit.
- **III. Deterministic extraction — PASS**: Date intervals, percentage
  eligibility, hierarchy, near-term membership, labels, and ordering use fixed
  rules. No LLM or generated narrative is introduced.
- **IV. Test through deep interfaces — PASS**: `ExecutiveDailyGanttProjector`,
  `ExecutiveProgressWorkbookComposer`, and `ExecutiveProgressXlsxExporter`
  expose narrow observable seams with fixture-driven contract tests.
- **V. Restricted-environment delivery — PASS**: The design uses installed .NET
  and existing repository scripts only.
- **VI. Explicit scope and safe failure — PASS**: Sparse evidence exports with
  explicit unknowns; contradictory official data fails with a structured
  diagnostic; non-official states cannot enter the report.

No constitution violation or complexity exception is required.

## Project Structure

### Documentation (this feature)

```text
specs/006-executive-daily-gantt-report/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── quickstart.md
├── contracts/
│   ├── executive-daily-gantt-workbook.md
│   └── executive-progress-export.md
├── checklists/
│   └── requirements.md
└── tasks.md
```

### Source Code (repository root)

```text
src/ProjectManagementCompiler/
├── Management/
│   ├── ExecutiveProgressReportModel.cs          # extend report/detail fields
│   ├── ExecutiveProgressReportProjector.cs      # compose existing summaries
│   ├── ExecutiveDailyGanttModel.cs               # new daily projection types
│   └── ExecutiveDailyGanttProjector.cs           # new hierarchy/Actual/window rules
├── Outputs/
│   ├── ExecutiveWorkbookDocument.cs             # new neutral sheet/row/cell model
│   ├── ExecutiveProgressWorkbookComposer.cs      # new five-sheet presentation mapping
│   ├── ExecutiveProgressXlsxExporter.cs          # XLSX package serialization
│   └── CarioXlsxExporter.cs                      # regression-only; do not modify
├── Application/
│   ├── IProjectCompiler.cs                       # signature remains unchanged
│   └── ProjectCompiler.cs                        # official validation remains unchanged
├── Program.cs                                    # endpoint/file naming remain unchanged
└── wwwroot/
    └── app.js                                    # existing export action remains unchanged

tests/ProjectManagementCompiler.Tests/
├── ExecutiveProgressTestFixtures.cs
├── ExecutiveDailyGanttProjectionTests.cs         # new projection contract tests
├── ExecutiveProgressProjectionTests.cs
├── ExecutiveProgressXlsxTests.cs
├── ExecutiveProgressExportApplicationTests.cs
├── ExecutiveProgressUiTests.cs
├── CarioXlsxTests.cs
├── XlsxPreviewImporterTests.cs
└── Program.cs

docs/runbook/mvp1-local.md
```

**Structure Decision**: Add one focused projection module rather than growing
the existing 934-line executive projector. Extract a small neutral workbook
document and composer so the existing 746-line exporter can concentrate on
Open XML serialization instead of mixing business selection, layout, and
package writing. Do not generalize or modify `CarioXlsxExporter`.

## Deep Interfaces and Ownership

### Daily Gantt projection

`ExecutiveDailyGanttProjector.Build(CompilationResult result)` produces one
`ExecutiveDailyGanttProjection`. It owns:

- canonical hierarchy and stable source order;
- direct delivery-card Plan/Actual/forecast fields;
- conservative phase/work-package roll-ups with coverage;
- overview-row selection;
- exact 30-date membership and ordering;
- blocked/overdue presentation flags.

It does not own workbook colors, XML, HTTP selection, or CARIO mapping.

### Workbook composition

`ExecutiveProgressWorkbookComposer.Build(ExecutiveProgressReport report)`
produces one `ExecutiveWorkbookDocument` with exactly five ordered worksheets.
It owns reader-facing cell content, paired Plan/Actual rows, widths, merged
month headers, freeze panes, print settings, and semantic style tokens.

It does not own source authority, percentage calculation, or ZIP/XML syntax.

### XLSX serialization

`ExecutiveProgressXlsxExporter.Export(ExecutiveProgressReport report)` keeps
its public signature. It delegates sheet construction to the composer and
serializes the returned document deterministically. It owns package parts,
relationships, style IDs, cell addresses, merges, and reproducible ZIP entry
metadata.

## Planned Interfaces

```csharp
public sealed class ExecutiveDailyGanttProjector
{
    public ExecutiveDailyGanttProjection Build(CompilationResult result);
}

public sealed record ExecutiveDailyGanttProjection
{
    public DateOnly FullStart { get; init; }
    public DateOnly FullFinish { get; init; }
    public DateOnly NearTermStart { get; init; }
    public DateOnly NearTermFinish { get; init; }
    public IReadOnlyList<ExecutiveDailyGanttRow> OverviewRows { get; init; }
    public IReadOnlyList<ExecutiveDailyGanttRow> FullRows { get; init; }
    public IReadOnlyList<ExecutiveDailyGanttRow> NearTermRows { get; init; }
}

internal sealed class ExecutiveProgressWorkbookComposer
{
    public ExecutiveWorkbookDocument Build(ExecutiveProgressReport report);
}
```

Detailed fields and invariants are normative in [data-model.md](data-model.md).

## Phase 0: Research Decisions

[research.md](research.md) records the resolved decisions:

- upgrade the single existing management export rather than create a second;
- use exactly five sheets and preserve issue/detail traceability;
- use paired physical rows per logical Gantt item for a clear Plan/Actual lane;
- keep calendar position and effort percentage as separate measures;
- use source reporting date for the 30-day window and analysis date for open
  in-progress Actual;
- show roll-up coverage and suppress incomplete-scope percentages;
- render forecast only from official `forecastFinish` evidence;
- retain BCL Open XML and introduce a neutral workbook document/composer;
- preserve official-only, presentation-only, and technical-export boundaries.

No `NEEDS CLARIFICATION` remains.

## Phase 1: Design Artifacts

- [data-model.md](data-model.md) defines daily Gantt projection entities,
  interval eligibility, coverage, roll-ups, near-term selection, and workbook
  document invariants.
- [contracts/executive-daily-gantt-workbook.md](contracts/executive-daily-gantt-workbook.md)
  defines exact sheet order, daily axes, paired lanes, labels, colors, panes,
  and print behavior.
- [contracts/executive-progress-export.md](contracts/executive-progress-export.md)
  preserves the single official-only export action, download naming, errors,
  immutability, and non-importability boundary.
- [quickstart.md](quickstart.md) defines baseline verification, focused tests,
  the real IDEAEngineering sparse-Actual acceptance, synthetic Actual-lane
  acceptance, visual inspection, and final gates.

### Post-design constitution re-check

All six constitution principles remain PASS. Phase 1 introduces no new source
adapter, persistence owner, package dependency, authority mode, or write-back
path. The workbook model is an output-only value boundary beneath the canonical
and executive projections.

## TDD Implementation Sequence

1. Capture a fresh build/test baseline and existing four-sheet exporter
   behavior before production edits.
2. Add RED projection tests for direct Actual intervals, effort percentage,
   unrecorded state, roll-up coverage, reporting/as-of separation, hierarchy,
   and exact 30-date membership.
3. Implement the daily-Gantt model/projector and extend executive detail and
   progress models; run focused and related management tests GREEN.
4. Add RED workbook-document and XLSX tests for five sheets, daily columns,
   paired lanes, month merges, weekend background, report-date border,
   milestone symbols, panes, and print settings.
5. Extract the neutral workbook document/composer and refactor the exporter to
   deterministic five-sheet serialization; run package tests GREEN.
6. Add RED application/compatibility regressions proving the existing action,
   endpoint, file name, official-only selection, sparse-data success, invalid-
   data failure, state immutability, non-importability, and unchanged CARIO
   contract.
7. Update runbook wording, generate an official real-source workbook, render or
   open every sheet for visual inspection, and record exact acceptance evidence.
8. Run focused tests, full `scripts/verify.ps1`, `git diff --check`, Spec Kit
   analyze, Spec Kit converge, and final code review before merge or push.

## Complexity Tracking

No constitution violations or justified complexity exceptions are present.
The two new production modules isolate genuinely separate responsibilities and
prevent further growth of already-large projector/exporter files; they do not
create a reusable spreadsheet framework or alter the technical exporter.
