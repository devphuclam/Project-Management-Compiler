# Feature 005 acceptance evidence

Date: 2026-09-20

## Real IDEAEngineering export

The accepted source was imported from the exact official commit:

```text
0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4
```

The web acceptance gate generated the executive workbook successfully. The
observed download was 14,212 bytes and returned `200 OK` with the XLSX content
type. The response filename ended with:

```text
_BaoCaoTienDo_2026-09-19.xlsx
```

The package checks found the four sheets in this order:

```text
Tổng quan
Lịch trình
Vấn đề cần xử lý
Chi tiết công việc
```

The first sheet is active. The workbook contains no CARIO or `PMC_*` technical
markers, and the technical XLSX preview importer rejects the executive package.
The package also carries 100% view settings, hidden gridlines, wrapped primary
text, frozen context, landscape orientation, and fit-to-one-page-wide print
settings. Excel COM read-only inspection of the generated package reported
`Zoom=100`, `Orientation=2 (landscape)`, `FitToWidth=1`, and `FitToHeight=False`
for all four sheets. Exporting the workbook through Excel's fixed-format print
path produced five landscape pages. Rendered pages 1-5 were inspected: the
overview, schedule, action list, and two detail pages kept primary text inside
their cells after default reader-text wrapping; no clipped primary cell was
observed. The report marker remains visible when the source reporting date is
outside the planning bounds through the dedicated boundary regressions.

The timed acceptance was performed after the official snapshot was loaded:
the executive endpoint returned the 14,212-byte workbook in 146 ms in the
direct acceptance run, below the five-second budget. The reviewer was the
Codex acceptance pass (package assertions plus rendered-page inspection); the
overview exposes current phase, next milestone, schedule condition, evidence-
safe progress, and decision/action attention without requiring technical IDs.

The same acceptance run downloaded the existing technical CARIO + Gantt
workbook and verified its seven-sheet contract, daily axis, PLAN/ACTUAL/ALERT
lanes, preview markers, and read-only preview lifecycle.

## Source immutability

Capture command family used before and after the export:

```powershell
git -C D:\Work\Projects\IDEAEngineering rev-parse HEAD
git -C D:\Work\Projects\IDEAEngineering status --porcelain=v1 -uall
git -C D:\Work\Projects\IDEAEngineering diff --no-ext-diff --binary
git -C D:\Work\Projects\IDEAEngineering diff --cached --no-ext-diff --binary
```

Before and after export:

| Check | Before | After | Result |
|---|---|---|---|
| `HEAD` | `0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4` | `0cf89de164f75fbbfde23d0a24cd5dadb3ac71c4` | identical |
| Status | `M .gitattributes`; `?? RUN_VERIFIERS.bat`; three pre-existing `qualification/q15/**/TestResults/*.trx` files | same four entries | identical |
| Tracked diff SHA-256 | `8fe655d2e1b40d81f41e556bacc9155abbac70c5da82a313a134cb0e167ef0d5` | same | identical |
| Staged diff SHA-256 | `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` | same | identical |
| Tracked diff bytes | `439` | `439` | identical |
| Staged diff bytes | `0` | `0` | identical |

The source checkout was not written, and the existing dirty state was not
cleaned or altered.

## Commands and results

The following commands were executed on the feature worktree. The fixture
locator was supplied with `IDEAENGINEERING_ROOT` for the executable runner.

```powershell
dotnet build .\ProjectManagementCompiler.sln --no-restore
.\scripts\build.ps1
$env:IDEAENGINEERING_ROOT = 'D:\Work\Projects\IDEAEngineering'
.\scripts\test.ps1
pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify.ps1
.\scripts\verify-web.ps1
git diff --check
```

Results:

- direct solution build: `0 Warning(s)`, `0 Error(s)`, exit `0`;
- executable runner: `273 PASS`, `0 FAIL`, exit `0`;
- full `scripts/verify.ps1`: exit `0`; build `0/0`, executable runner `273/0`, launcher pass, web smoke `SecurityChecks=PASS`;
- web smoke: exit `0`, `SecurityChecks=PASS`, official import shape `6 / 35 / 53 / 7`, technical workbook `7` sheets, executive workbook `4` sheets;
- `git diff --check`: exit `0` (Git emitted only the repository's existing LF/CRLF advisory warnings).

The remaining gates are post-implementation Spec Kit analyze/converge and
repository code review before integration.

## Post-implementation Spec Kit analyze

The required prerequisite command was run in the feature worktree:

```powershell
.\.specify\scripts\powershell\check-prerequisites.ps1 -Json -RequireSpec -RequireTasks -IncludeTasks
```

The strictly read-only analyze pass found `0` actionable findings:

- 30 functional requirements and 7 success criteria were covered;
- all 32 tasks had an implementation or verification mapping, with only the
  post-analyze/converge/review gates remaining at the time of the pass;
- required artifacts and referenced source/test/doc paths exist;
- plan and implementation agree on .NET 10, BCL/dependency-free XLSX output,
  no ClosedXML dependency, separate executive/CARIO purposes, and official-only
  export authority;
- no unresolved clarification/action markers or constitution conflicts were
  found.

No remediation edit was required after this analysis.

## Post-implementation Spec Kit converge

Converge ran after the read-only analyze pass and checked the same 37
requirements/success criteria, plan decisions, constitution constraints,
implementation paths, and task list. Result: **converged**. No missing,
partial, contradicting, or unrequested finding required a new task, so the
append-only convergence phase was not added.
