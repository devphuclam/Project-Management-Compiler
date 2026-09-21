# Quickstart: Executive Daily Gantt Report

## Prerequisites

- .NET 10 SDK/runtime already installed.
- Repository checked out on `codex/feature006-executive-daily-gantt-report`.
- No package installation, Office automation, database, network service, or
  source write-back is required.
- For real-source acceptance, set `IDEAENGINEERING_ROOT` to the local
  IDEAEngineering checkout. Do not commit a workstation-specific absolute
  path.

## 1. Capture the implementation baseline

From the Project Management Compiler repository root:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\test.ps1
git status --short
```

Expected before feature implementation: build and tests pass; pre-existing
untracked files remain visible and are not added to feature commits.

## 2. Run focused projection tests

The executable test runner must register
`ExecutiveDailyGanttProjectionTests`. During TDD, run the full test executable
and use its named PASS/FAIL output to confirm these cases:

- completed Actual uses actual start/finish;
- in-progress Actual ends at analysis as-of without creating actual finish;
- in-progress without actual start has no lane;
- unrecorded is `Chưa cập nhật`, never zero or `Chưa bắt đầu`;
- valid effort produces the approved percentage;
- incomplete effort produces an explicit insufficient-data label;
- partial roll-up coverage suppresses whole-scope percentage;
- full hierarchy contains every canonical delivery card exactly once;
- 30-day window is source date through plus 29 days;
- overdue open work remains in the near-term view;
- out-of-window non-overdue work is excluded.

## 3. Run focused workbook tests

`ExecutiveProgressXlsxTests` must prove:

- exactly five sheets in approved order and active `Tổng quan`;
- every Gantt date is a distinct daily column;
- month/date/weekday headers, weekends, and report-date marker exist;
- each non-milestone task band has adjacent `Kế hoạch` and `Thực tế` rows;
- Plan, Actual, forecast, overdue, unknown, and milestone styles exist with
  accompanying labels;
- fixed columns and date headers are frozen;
- full Gantt uses readable horizontal pagination;
- overview and 30-day sheets use approved landscape settings;
- detail contains Actual dates, effort, percentage, recording state, and latest
  update columns;
- first four sheets contain no forbidden technical provenance or proposal data.

## 4. Run application and compatibility tests

`ExecutiveProgressExportApplicationTests`, `ExecutiveProgressUiTests`,
`CarioXlsxTests`, and `XlsxPreviewImporterTests` must prove:

- the existing progress-report action and route remain unchanged;
- only `CurrentOfficialResult` is exported;
- sparse Actual succeeds with explicit unknowns;
- contradictory official Actual fails closed;
- file naming still uses official source reporting date;
- export does not mutate project/proposal/preview state;
- the five-sheet management workbook is not importable;
- the seven-sheet CARIO workbook remains unchanged.

## 5. Run the real IDEAEngineering flow

Start the application with the existing runner:

```powershell
$ideaRoot = $env:IDEAENGINEERING_ROOT
if ([string]::IsNullOrWhiteSpace($ideaRoot) -or -not (Test-Path -LiteralPath $ideaRoot)) {
  throw 'Set IDEAENGINEERING_ROOT to an existing local IDEAEngineering checkout.'
}

pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-project.ps1 `
  -RepositoryRoot $ideaRoot `
  -ManifestPath 'planning/project-management-compiler-manifest.json' `
  -ImportMode GIT_COMMIT `
  -ImportManifest
```

If the source commit must be pinned, use the commit already approved for the
current local acceptance run rather than inventing or updating one.

Open `http://127.0.0.1:5050/` and select `Xuất báo cáo tiến độ`.

Expected workbook name:

```text
<Project>_BaoCaoTienDo_<source-reporting-date>.xlsx
```

For the current real execution register, verify truthful sparse evidence:

- total delivery cards: 53;
- recorded cards: 1;
- unrecorded cards: 52;
- no invented project percentage when actual/remaining effort are absent;
- P01 may show its recorded state but receives no green Actual lane while
  actual start is absent;
- coverage and latest official update remain visible.

These values are acceptance expectations for the current source snapshot, not
hard-coded production constants.

## 6. Inspect every sheet visually

Open or render every generated sheet at 100% zoom.

### `Tổng quan`

- Four approved summary blocks are visible before the Gantt.
- Progress, coverage, and update recency can be read without horizontal scroll.
- Phase/milestone Plan and Actual meanings match the legend.
- At most five attention items appear.

### `Gantt theo ngày`

- Hierarchy is Project → Phase → Work package → Delivery card → Milestone.
- Identity columns and headers remain frozen while scrolling.
- Paired lanes align to the same daily columns.
- Weekend backgrounds and the report-date marker remain visible but calm.
- Long Vietnamese labels wrap or fit without semantic clipping.

### `30 ngày tới`

- Exactly 30 dates are shown.
- Overdue open work appears before future work.
- Intervals crossing the window use labelled continuation symbols, while a
  wholly pre-window overdue Plan uses a dated overdue marker and no fabricated
  in-window blue bar.
- Plan/Actual semantics match the full Gantt.

### `Vấn đề cần xử lý`

- Action, impact, owner, and due condition are readable.
- No raw technical state or evidence ID appears.

### `Chi tiết công việc`

- Actual dates, effort, progress, recording state, owner, update, and short
  reference are traceable on one row per delivery card.
- Missing numeric values remain blank/explicit rather than zero.

Record any clipping, ambiguous lane, or unreadable print scaling as a failed
acceptance item; XML assertions alone do not prove visual quality.

## 7. Validate authority and source immutability

Before and after both management and CARIO exports, capture:

```powershell
$ideaRoot = $env:IDEAENGINEERING_ROOT
if ([string]::IsNullOrWhiteSpace($ideaRoot) -or -not (Test-Path -LiteralPath $ideaRoot)) {
  throw 'Set IDEAENGINEERING_ROOT to an existing local IDEAEngineering checkout.'
}

git -C $ideaRoot rev-parse HEAD
git -C $ideaRoot status --porcelain=v1 -uall
git -C $ideaRoot diff --no-ext-diff --binary
git -C $ideaRoot diff --cached --no-ext-diff --binary
```

Require equality across the before/after captures. Also compare the compiler's
semantic digest, official source-execution projection, proposal list, and
active preview state before and after downloads.

## 8. Full verification gate

Run:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
git diff --check
git status --short
```

Expected:

- zero build warnings/errors;
- zero failed tests;
- web smoke verification passes;
- no unintended generated/temp files are staged;
- no secret, credential, absolute private configuration, or proprietary source
  material is included.

After fresh verification, run Spec Kit analyze, Spec Kit converge, and code
review before merge or push.
