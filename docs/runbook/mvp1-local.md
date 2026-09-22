# Run the local Project Management Compiler (MVP1 + MVP2.1 readiness + executive report)

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

For the normal Windows demo, use the repository launcher instead of typing the
long `dotnet` command:

```powershell
.\scripts\run-project.ps1
```

The current hardening closeout registers and executes the full custom-runner
regression suite;
the verification scripts also cover the launcher, web/API flow, JSON/XLSX
outputs, and browser-source safety assertions.

Or double-click `Run Project.cmd` in the repository root. The launcher waits
for `GET /api/health`, opens `http://127.0.0.1:5050/`, and keeps the application
running until you press `Ctrl+C`. Use `-NoBrowser` when the browser is already
open:

```powershell
.\scripts\run-project.ps1 -NoBrowser
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
legacy regression fixture, but it is not the primary acceptance source.

## MVP2.1 repository-readiness demo

Repository readiness is an explicit, bounded opt-in. It is not recursive
specification discovery. In the source-intake panel:

1. enter the source directory;
2. enter `specs/004-technical-pilot-readiness` as the **Readiness increment
   path**;
3. select **Include repository readiness evidence**;
4. choose an as-of date and select **Analyze source**.

The **Include repository readiness evidence** checkbox is the authoritative
enable/disable switch. If it is off, the readiness path is ignored and the
compile remains planning-only with **Repository readiness: Not requested**,
even when the path text is retained in the field. If it is on, the path is
required and must identify the bounded readiness increment.

The checkbox without a path is rejected with
`MANAGEMENT_EVIDENCE_PATH_REQUIRED` and the message “Select the readiness
increment path before including repository readiness evidence.” The bounded
profile reads the six declared readiness files and optionally the increment
root `pg4-gate-record.md` when that actual record exists. The optional record
is expected later in the workflow, so its absence is shown as “Not yet
recorded”, not as a capture failure.

The Management control view separates task state, readiness result, owner,
waiting-for role, pending action, due condition, blocker, gate effect, and
provenance. A gate/decision/human action is standalone management evidence;
it is not converted into a DeliveryCard. Effective gate values use the
attributable actual record before register/README summaries. Equal-authority
conflicts remain visible and do not become an effective value.

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
5. Use **Save JSON** to download
   `<ProjectName>_project.json`. Use the **Reopen
   saved canonical JSON** control to validate and recalculate that snapshot
   without re-reading the source directory.
6. Use **Xuất dữ liệu CARIO + Gantt** to download
   `<ProjectName>_CARIO_GANTT.xlsx`. It is a human-assisted fill file, not a
   claimed native CARIO import. The default route exports the official source
   snapshot when one is loaded; a preview must be requested explicitly through
   `GET /api/exports/cario-preview.xlsx`.

### Báo cáo tiến độ gửi quản lý

Sau khi import thành công **Official Git commit**, dùng nút **Xuất báo cáo
tiến độ** ở header. Đây là bản trình bày dành cho người quản lý, không phải
file kỹ thuật để import ngược vào Compiler. Một lần xuất tạo đúng một workbook
với sáu sheet theo thứ tự cố định:

- file có tên `<ProjectName>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`, ngày lấy từ
  `statusDate` của Execution Register;
- `Tổng quan` trả lời nhanh dự án đang ở đâu, tiến độ có bằng chứng, thay đổi
  so với kế hoạch, mốc kế tiếp và quyết định cần xử lý; danh sách chú ý bị giới
  hạn ở mức đọc được.
- `Điều hành 30 ngày` gom một lần duy nhất các quyết định/blocker, việc quá
  hạn, việc đang chạy và việc/mốc nằm trong cửa sổ từ ngày báo cáo đến ngày
  thứ 29. Các dòng được xếp theo mức độ cần xử lý và không lặp lại giữa hai
  danh sách cũ.
- `Gantt` giữ đầy đủ Project → Phase → Work package → Delivery card →
  Milestone, với một ngày trên mỗi cột và hai làn liền kề `Kế hoạch` / `Thực tế`.
- `WBS` là cây bốn cấp để đọc phạm vi: mặc định mở đến Work Package; Delivery
  Card, ngày, Actual, quan hệ và bằng chứng được mở rộng khi cần.
- `Chi tiết công việc` có đúng một dòng cho mỗi Delivery Card với tên dễ đọc,
  kế hoạch, Actual, forecast, effort, %, trạng thái ghi nhận, đầu mối, cập nhật
  cuối, quan hệ và mã tham chiếu ngắn.
- `Thông tin báo cáo` tập trung authority, source/snapshot, baseline, contract,
  ngày báo cáo và các giới hạn diễn giải để năm sheet đầu không bị lặp
  provenance kỹ thuật.

Trong mọi Gantt, thanh **Kế hoạch** màu xanh là ngày baseline bất biến; thanh
**Thực tế** màu xanh lá chỉ xuất hiện khi source đã ghi nhận ngày Actual;
**Dự báo** màu hổ phách chỉ là forecast chính thức; quá hạn/bị chặn là cue đỏ;
unknown là xám; milestone là hình thoi; vạch ngày báo cáo là mốc dọc. Độ dài
thanh luôn biểu diễn **ngày**, không biểu diễn effort. `% thực tế` là số riêng
được tính từ `giờ thực tế / (giờ thực tế + giờ còn lại)` chỉ khi cả hai giá trị
official hợp lệ; không có hoặc zero-sum thì ghi `Chưa đủ dữ liệu`, không bịa
0%. Thiếu ngày Actual/forecast cũng giữ trống/unknown thay vì kéo dài thanh.

Nếu chưa có official snapshot, nút này bị khóa. Working-tree preview, candidate
preview, proposal và XLSX preview không được dùng làm báo cáo chính thức.
File báo cáo tiến độ không phải input hợp lệ của **Import XLSX preview** và
không thay thế file **CARIO + Gantt** kỹ thuật bảy sheet; hai luồng export vẫn
tách biệt. Workbook quản lý này là đầu ra lưu trữ/trình bày của MVP hiện tại,
không phải **Project Workbook** để nhập ngược. Ranh giới Project Workbook
tương lai sẽ là một luồng thiết lập dự án riêng: người dùng tạo/chọn cấu trúc
quản lý, chọn đúng một nguồn theo dõi chính thức, rồi Compiler mới cho phép
đọc lại workbook đó theo một hợp đồng nhập riêng. Chưa dùng báo cáo sếp để
thay thế ranh giới này.

### Read-only XLSX preview workflow

The workbook can be inspected inside the compiler only through the explicit
preview path. This is a presentation aid, not a way to import arbitrary Excel
or to turn spreadsheet values into source truth:

1. Export a fresh `<ProjectName>_CARIO_GANTT.xlsx` from this application after
   the provenance-marker version is installed.
2. In the source-intake panel choose **Import XLSX preview** and select that
   file. The importer requires the seven compiler-generated sheets and the
   four `PMC_*` marker rows in `05_PROJECT_INFO`.
3. Confirm the page shows **XLSX Preview**, **Read-only**, and
   **Non-authoritative**, then review the task table and daily Gantt. PLAN,
   ACTUAL, ALERT, MILESTONE, `Recorded %`, and `Not recorded` values are
   displayed exactly as exported.
4. Proposal/execution controls are unavailable in preview mode. Choose
   **Official source** to return to the manifest-backed views or **Clear
   preview** to remove the temporary preview.

The preview is held in memory only. A new official manifest import clears it,
and restart does not restore it. Invalid, altered, unmarked, externally linked,
or oversized workbooks return `422 INVALID_XLSX_PREVIEW` and leave the current
official result and any existing valid preview unchanged.

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
- `GET /api/exports/executive-progress.xlsx` (official snapshot only; download
  name: `<ProjectName>_BaoCaoTienDo_<YYYY-MM-DD>.xlsx`)
- `GET /api/exports/cario.xlsx` (official/source snapshot; download name:
  `<ProjectName>_CARIO_GANTT.xlsx`)
- `GET /api/exports/cario-preview.xlsx` (explicit non-authoritative preview;
  download name: `<ProjectName>_CARIO_GANTT_PREVIEW.xlsx`)
- `POST /api/xlsx-preview` (multipart field `file`; explicit read-only import)
- `GET /api/xlsx-preview` (active temporary preview)
- `DELETE /api/xlsx-preview` (clear the temporary preview)

The persisted JSON contains safe source metadata, provenance, the immutable
baseline, and the execution overlay, but not captured source text or absolute
local paths. The workbook contains the six CARIO sheets plus a visual Gantt
sheet:

`01_TASKS`, `02_ASSIGNMENTS`, `03_CHILDREN_MILESTONES`, `04_DEPENDENCIES`,
`05_PROJECT_INFO`, `06_IMPORT_WARNINGS`, and `07_GANTT`.

`07_GANTT` uses one daily column per calendar day, freezes the identity
columns, shades weekends, marks the as-of date, and keeps PLAN, ACTUAL, ALERT,
and MILESTONE lanes distinct. `Recorded %` is populated only when actual and
remaining effort are both recorded; otherwise it remains `Not recorded`.

Generated JSON/XLSX files, `bin/`, `obj/`, and temporary verification files are
not repository inputs and must not be committed.

## MVP1 / MVP2.1 boundaries

MVP1 intentionally excludes databases, authentication, multi-user hosting,
CARIO API/browser automation, provider integrations, AI extraction, advanced
forecasting, resource leveling, WebSockets, SaaS deployment, and Docker.
