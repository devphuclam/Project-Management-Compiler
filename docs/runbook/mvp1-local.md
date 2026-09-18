# Run the local MVP1

Project Management Compiler is a dependency-free .NET 10 local application.
It reads only the allow-listed planning documents from an IDEAEngineering-shaped
source directory. The source is treated as untrusted data; the compiler does
not execute scripts, hooks, builds, binaries, or macros from it.

## Prerequisites

- Installed .NET 10 SDK/runtime.
- No database, Docker, package installation, browser driver, Office, or network
  service is required.

## Verify and run

From the repository root:

```powershell
dotnet restore .\ProjectManagementCompiler.sln
dotnet build .\ProjectManagementCompiler.sln --no-restore
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
.\scripts\verify.ps1
dotnet run --project .\src\ProjectManagementCompiler\ProjectManagementCompiler.csproj --no-restore
```

Open [http://127.0.0.1:5050](http://127.0.0.1:5050). The server is loopback
only by default and is not a LAN service.

For the primary MVP1 demo, enter the real-shaped repository-relative fixture path:

```text
tests\fixtures\ideaengineering-real-shaped
```

Choose an explicit as-of date such as `2026-09-28`, then select **Analyze
source**. The real-shaped fixture should show 6 phases, 35 work packages, 53
delivery cards, 7 milestones, 512 authoritative work-package hours, 88 reserve
hours, 600 capacity hours, and unresolved CARIO identity/priority/department/team
mapping warnings. WorkPackage `P04` and DeliveryCard `P04` intentionally coexist;
the dependency view distinguishes their typed identities.

The older `tests\fixtures\ideaengineering` fixture remains available as a
legacy regression fixture, but it is not the primary MVP1 acceptance source.

## Browser workflow

1. Review the project summary and **Source / Warnings** tab.
2. Inspect WBS, the three Gantt lanes (**PLAN**, **ACTUAL**, **ALERT**), Kanban,
   Dependencies, CPM, and Dashboard.
3. Confirm the dashboard uses `Delivery cards completed 0/53` before execution
   evidence is entered. CPM finish and forecast finish remain separate; forecast
   is `UNKNOWN` when evidence is insufficient.
4. Enter delivery-card ID `P04`, choose `In progress`, provide an actual start
   such as `2026-09-25`, and apply the update. The baseline PLAN dates remain
   unchanged; the ACTUAL and ALERT lanes are recalculated. Enter `P06` as
   `Not started` afterward to see the dependent `AT_RISK` alert.
5. Use **Save project JSON** to download
   `<ProjectName>_project.json`. Use the **Reopen
   saved canonical JSON** control to validate and recalculate that snapshot
   without re-reading the source directory.
6. Use **Export CARIO XLSX** to download
   `<ProjectName>_CARIO.xlsx`. It is a human-assisted fill file, not a claimed
   native CARIO import.

### Gantt review controls

Use the Gantt tab as a management timeline rather than a task table:

- Start with **Fit project** and **Month** zoom, then switch to **Week** and
  **Day** to check the date scale. The vertical `AS OF` marker is the explicit
  analysis date, not the browser's hidden current date.
- Use **Expand all** / **Collapse all** to inspect the Project → Phase → Work
  Package → Delivery Card hierarchy. Summary rows inherit their authored child
  range; they are not a second schedule.
- Turn on **Critical path** only when reviewing the CPM set. Use **Show
  dependencies** to reveal typed connectors, then select a row to highlight its
  direct relationships and open the row inspector.
- Filter by phase, execution state, **Critical only**, **Overdue**, **At risk**,
  or **Late start**. ALERT markers are derived signals and are not forecast
  bars. A missing ACTUAL lane shows `—` until execution evidence exists.
- Select Delivery Card `P04` and choose **Record execution** to jump to the
  existing execution form. After applying an in-progress update, confirm the
  PLAN bar is unchanged, ACTUAL ends at the chosen as-of date in the view, and
  the ALERT marker explains the derived condition.

## API and output names

- `GET /api/health`
- `POST /api/compile`
- `POST /api/reopen`
- `POST /api/execution`
- `GET /api/project`
- `GET /api/views` and `GET /api/views/{dashboard|wbs|gantt|kanban|dependencies|cpm}`
- `GET /api/warnings`
- `GET /api/exports/project.json` (download name: `<ProjectName>_project.json`)
- `GET /api/exports/cario.xlsx`

The persisted JSON contains safe source metadata, provenance, the immutable
baseline, and the execution overlay, but not captured source text or absolute
local paths. The workbook contains exactly these sheets:

`01_TASKS`, `02_ASSIGNMENTS`, `03_CHILDREN_MILESTONES`, `04_DEPENDENCIES`,
`05_PROJECT_INFO`, and `06_IMPORT_WARNINGS`.

Generated JSON/XLSX files, `bin/`, `obj/`, and temporary verification files are
not repository inputs and must not be committed.

## MVP1 boundaries

MVP1 intentionally excludes databases, authentication, multi-user hosting,
CARIO API/browser automation, provider integrations, AI extraction, advanced
forecasting, resource leveling, WebSockets, SaaS deployment, and Docker.
