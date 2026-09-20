# Quickstart: Executive Progress Report Export

## Prerequisites

- .NET 10 SDK/runtime already available.
- This repository checked out on the feature branch.
- Local IDEAEngineering checkout available at
  `D:\Work\Projects\IDEAEngineering` for the real acceptance scenario.
- No Excel package, Office automation, network service, or source-repository
  write is required to build or generate the workbook.

## Build and executable tests

From the repository root:

```powershell
dotnet build .\ProjectManagementCompiler.sln --no-restore
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
```

Expected: zero build warnings/errors and zero failed tests. Feature tests cover
projection semantics, the four-sheet package, official-only selection, file
naming, import rejection, UI labels, state immutability, and CARIO regressions.

## Real IDEAEngineering acceptance setup

Use the official commit already accepted for the local project:

```powershell
$sourceRoot = 'D:\Work\Projects\IDEAEngineering'
$manifestPath = 'planning/project-management-compiler-manifest.json'
$sourceCommit = '0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4'

powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\run-project.ps1 `
  -RepositoryRoot $sourceRoot `
  -ManifestPath $manifestPath `
  -SourceCommit $sourceCommit `
  -ImportMode GIT_COMMIT `
  -ImportManifest
```

Expected official snapshot shape for this acceptance source: 6 phases, 35 work
packages, 53 delivery cards, and 7 control points. The source reporting date is
2026-09-19.

## Browser flow

1. Open `http://127.0.0.1:5050/` after the official import succeeds.
2. Confirm two distinct controls are visible:
   `Xuất báo cáo tiến độ` and `Xuất dữ liệu CARIO + Gantt`.
3. Select `Xuất báo cáo tiến độ`.
4. Confirm the downloaded name ends with
   `_BaoCaoTienDo_2026-09-19.xlsx`.
5. Open the workbook and confirm it starts on `Tổng quan`.

## Workbook acceptance

Inspect the generated file against
[contracts/executive-progress-workbook.md](contracts/executive-progress-workbook.md):

- exactly four sheets in the approved order;
- no raw IDs in the first three sheets;
- overview contains no more than four summary blocks and five attention items;
- phase/milestone overview and work-package schedule use month/week columns;
- visible `Ngày báo cáo` marker at 2026-09-19;
- delivery-card IDs appear only under the last sheet's `Mã tham chiếu` column;
- with the accepted source data, progress is not fabricated: if actual and
  remaining effort are not both valid, the overview says
  `Chưa đủ dữ liệu để tính % hoàn thành` and may show the supported counts
  (currently one in progress and zero completed for this snapshot);
- action and owner cells use readable text, not code lists.

## Timed and visual review

At 100% zoom, ask a reader unfamiliar with the compiler to identify from
`Tổng quan` within 60 seconds:

1. current phase;
2. next milestone;
3. schedule condition;
4. whether progress percentage is supported by evidence;
5. which decisions/actions need attention.

Then open print preview for `Tổng quan` and `Lịch trình`. Both must be landscape,
fit to one page wide, and keep primary headings and action text legible. Record
the reviewer, elapsed time, and any clipped cell as acceptance evidence rather
than declaring visual success from XML tests alone.

## Authority and compatibility checks

1. Before either export, capture the source repository's exact `HEAD`, full
   porcelain status (including untracked files), tracked working-tree diff,
   and staged diff:

   ```powershell
   git -C $sourceRoot rev-parse HEAD
   git -C $sourceRoot status --porcelain=v1 -uall
   git -C $sourceRoot diff --no-ext-diff --binary
   git -C $sourceRoot diff --cached --no-ext-diff --binary
   ```

   Capture the same four outputs after both exports and require byte-for-byte
   equality. The acceptance record stores hashes when diff output is too large;
   it must not omit a pre-existing dirty state.
2. Export `Xuất dữ liệu CARIO + Gantt` from the same official snapshot and
   confirm its existing seven-sheet contract and preview markers are unchanged.
3. Attempt to import the executive workbook through `Import XLSX preview`.
   Expected: structured rejection; official state and any existing preview stay
   unchanged.
4. Activate a candidate/working-tree preview and request the executive endpoint.
   Expected: the report still uses the current official snapshot; if none
   exists, return `NO_OFFICIAL_SNAPSHOT`.
5. Compare semantic digest and proposal list before and after both downloads.
   Expected: identical values.

## Full verification gate

Run and record the exact output of:

```powershell
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
git diff --check
```

After implementation and fresh verification, run Spec Kit analyze, then Spec
Kit converge, then code review. Converge is intentionally a post-implementation
gate for this feature.
