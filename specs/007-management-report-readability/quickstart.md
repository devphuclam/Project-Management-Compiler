# Quickstart: Management Report Readability

## Prerequisites

- .NET 10 SDK/runtime and PowerShell 7 already installed.
- Repository checked out on `codex/feature007-management-report-readability`.
- Microsoft Excel or another standards-compliant XLSX reader for the mandatory
  visual review.
- No package installation, Office automation, Docker, database, network
  service, or source write-back is required.
- For real-source acceptance, `IDEAENGINEERING_ROOT` points to the approved
  local IDEAEngineering checkout. Do not commit an absolute workstation path.

## 1. Capture the baseline

From the Project Management Compiler repository root:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
git status --short
```

Expected before implementation: build and tests pass. Existing untracked files
remain visible and are not added to feature commits.

## 2. Follow the test-first sequence

For each implementation phase:

1. Add the named test to the executable test runner.
2. Run the test project and record the intended failure.
3. Add the smallest production change that satisfies the test.
4. Run focused and related regression tests until green.
5. Refactor only while the same tests remain green.

Run the executable suite with:

```powershell
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
```

## 3. Validate deterministic reader language

Focused tests must cover:

- exact ID at the beginning and end of a title;
- repeated nested prefixes such as `[PH0][PLN01]`;
- phase/work-package/delivery-card kind labels and hierarchy arrows;
- Markdown backticks, bold, and underscore markers;
- meaningful bracketed Vietnamese text that must be preserved;
- long Vietnamese names and missing names;
- approved labels `Chưa ghi nhận`, `Chưa phân công`, and
  `Cập nhật đến dd/MM/yyyy`;
- absence of raw enum and validation tokens on reader-facing projections.

Expected: every sheet projection uses the same policy and produces the same
Reader-Facing Name for the same source value.

## 4. Validate the Actual evidence matrix

The synthetic executive fixture must contain at least one row for each shape:

| Fixture shape | Expected Gantt result |
| --- | --- |
| Actual start + finish | Green recorded interval |
| In-progress start only | Green open interval through analysis date |
| Finish only | `✓` on the recorded finish date; no inferred start |
| Effort only | `● Có ghi nhận`; no dated marker |
| No execution evidence | Empty Actual lane and `Chưa ghi nhận` |

Also require:

- valid effort produces `actual / (actual + remaining)`;
- missing, negative, non-finite, or zero-total effort produces no percentage;
- parent roll-ups disclose coverage and suppress incomplete-scope percentage;
- contradictory Actual fails with a structured executive-export diagnostic.

## 5. Validate the WBS projection and outline

Projection tests must reconcile the accepted fixture to:

- 1 Project;
- 6 Phases;
- 35 Work Packages;
- 53 Delivery Cards;
- 0 Milestones/decision gates inside WBS.

Workbook tests must prove:

- exact primary column order;
- one clean `Hạng mục` and one separate `Mã` per row;
- correct positional WBS numbering and parentage;
- Delivery Card rows initially hidden at outline level 3;
- Project, Phase, and Work Package rows visible on open;
- expanding groups exposes all 53 Delivery Cards exactly once;
- Plan, Actual, Relationships, and Evidence column groups exist and initially
  collapse without hiding primary columns;
- filter and frozen identity context cover the complete table.

## 6. Validate all six sheet contracts

`ExecutiveProgressXlsxTests` and related projection tests must prove:

- exact sheet names/order and active `Tổng quan`;
- summary questions and at-most-five actions;
- one unified 30-day operating population in the approved category order;
- full daily Gantt hierarchy and the complete Actual truth matrix;
- WBS membership, outlines, column groups, filters, and clean names;
- one detail row per Delivery Card with reader-first columns;
- centralized metadata on `Thông tin báo cáo`;
- no commit, snapshot ID, absolute path, raw code, or repeated provenance on the
  first five sheets;
- approved zoom, panes, wrapping, orientation, and print-fit behavior;
- deterministic package bytes for the same report.

## 7. Validate application and compatibility boundaries

`ExecutiveProgressExportApplicationTests`, `ExecutiveProgressUiTests`,
`CarioXlsxTests`, and `XlsxPreviewImporterTests` must prove:

- the existing action, route, media type, and dated filename remain unchanged;
- only `CurrentOfficialResult` is exported while previews/proposals are active;
- export does not mutate canonical, official, proposal, preview, or source
  state;
- sparse evidence exports successfully;
- unsafe contradiction fails closed;
- the six-sheet report is rejected by workbook preview/import;
- the technical seven-sheet CARIO + Gantt package remains unchanged.

## 8. Run the official real-source flow

Set the local source path and use an already-approved exact commit:

```powershell
$ideaRoot = $env:IDEAENGINEERING_ROOT
if ([string]::IsNullOrWhiteSpace($ideaRoot) -or -not (Test-Path -LiteralPath $ideaRoot)) {
  throw 'Set IDEAENGINEERING_ROOT to the approved local IDEAEngineering checkout.'
}

$sourceCommit = '<approved-exact-40-character-SHA>'
if ($sourceCommit -notmatch '^[0-9a-f]{40}$') {
  throw 'Set $sourceCommit to an approved full commit SHA.'
}

& git -C $ideaRoot cat-file -e "$sourceCommit^{commit}"
if ($LASTEXITCODE -ne 0) {
  throw "The approved source commit is not available locally: $sourceCommit"
}

pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-project.ps1 `
  -RepositoryRoot $ideaRoot `
  -ManifestPath 'planning/project-management-compiler-manifest.json' `
  -ImportMode GIT_COMMIT `
  -SourceCommit $sourceCommit `
  -ImportManifest
```

Open `http://127.0.0.1:5050/` and choose `Xuất báo cáo tiến độ`, or download
the same route after the import is ready:

```powershell
$destination = Join-Path $env:TEMP 'PMC-BaoCaoTienDo-Feature007.xlsx'
Invoke-WebRequest `
  -Uri 'http://127.0.0.1:5050/api/exports/executive-progress.xlsx' `
  -OutFile $destination
Get-Item -LiteralPath $destination
```

Expected filename from the browser:

```text
<Project>_BaoCaoTienDo_<official-reporting-date>.xlsx
```

## 9. Perform the mandatory visual review

Open the generated workbook at 100% zoom and inspect every sheet.

### `Tổng quan`

- The project position, supported progress, plan change, next milestone, and
  decision are understandable within 60 seconds.
- No source code, hash, raw state, repeated ID, or methodology paragraph
  competes with the headline.
- Landscape print preview preserves readable summary and action text.

### `Điều hành 30 ngày`

- Decisions/blockers, overdue, active, and planned/milestone groups appear in
  that order without duplicate targets.
- Action, consequence, owner, date, state, and schedule context are readable.
- Landscape print preview does not compress body text beyond normal reading.

### `Gantt`

- Identity appears once per task band; Plan and Actual lanes align.
- `✓` and `● Có ghi nhận` are understandable without relying on color.
- Weekend shading and report boundary are visible but subdued.
- Horizontal scrolling preserves identity and date headers.

### `WBS`

- The sheet opens at Work Package depth.
- Expanding one Work Package reveals its Delivery Cards in canonical order.
- Expanding all rows exposes the complete 1/6/35/53 hierarchy.
- Optional column groups expand independently and primary columns remain fixed.
- `Hạng mục` reads as a name, not as a source code or compiler trace.

### `Chi tiết công việc`

- Filters and frozen columns support follow-up without losing identity.
- Missing dates/numbers remain blank and are explained by adjacent concise text.
- Reader-facing columns precede technical traceability.

### `Thông tin báo cáo`

- Authority and source identity are complete but concise.
- No credential, absolute private path, or repeated narrative appears.

Any clipped primary content, unreadable scaling, ambiguous Actual marker,
misleading percentage, or repeated source-code noise is a failed acceptance
item even when XML tests pass.

## 10. Verify performance and immutability

Measure the accepted fixture export around the compiler operation and require a
duration under five seconds. Before and after both Management Report and
technical workbook downloads, compare:

```powershell
git -C $ideaRoot rev-parse HEAD
git -C $ideaRoot status --porcelain=v1 -uall
git -C $ideaRoot diff --no-ext-diff --binary
git -C $ideaRoot diff --cached --no-ext-diff --binary
```

Also compare the compiler semantic digest, official source-execution
projection, proposals, and active preview state. Every before/after value must
match.

## 11. Full verification gate

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
git diff --check
git status --short
```

Expected:

- zero build warnings/errors and zero failed tests;
- launcher and web smoke verification pass;
- visual acceptance evidence is recorded for all six sheets;
- no temporary/generated workbook, credential, absolute private configuration,
  or proprietary source material is staged;
- Spec Kit analysis/convergence and code review complete before merge or push.

