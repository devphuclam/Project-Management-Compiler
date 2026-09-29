# Quickstart: Management UI Workspace Foundation

This is the implementation/verification guide for feature 008. It assumes the existing local .NET 10 SDK/runtime and repository scripts are already available; it does not install tools or dependencies.

## Prerequisites

- Windows checkout on `codex/project-ui-redesign`.
- Existing .NET 10 SDK and Git executable.
- Existing public real-shaped test fixture. Do not use or copy private IDEAEngineering files into this repository or feature artifacts.
- The app binds to loopback only. Its default is `http://127.0.0.1:5050`; set `PMC_LOOPBACK_PORT` to a free TCP port when running a second local instance for isolated verification. The configured port never changes the loopback host.

## Focused development checks

From the repository root:

```powershell
dotnet run --project .\tests\ProjectManagementCompiler.Tests\ProjectManagementCompiler.Tests.csproj --no-restore
```

During implementation, run the closest focused test executable after each red/green slice, then:

```powershell
.\scripts\test.ps1
.\scripts\verify.ps1
```

Do not restore/install packages for this feature. If an existing prerequisite is missing, report it rather than substituting a new tool.

## Manual app checks

1. Launch using the repository's supported `scripts\run-project.ps1` workflow.
2. In a clean browser profile, confirm the app presents an unloaded state and performs no import before a user action.
3. Enter a permitted local repository root and manifest path. Reload the browser and verify the values may be remembered but no import occurs.
4. Explicitly choose “Đọc phiên bản hiện có trên máy”. Confirm the UI says the newest version available in this local copy, not the online latest version.
5. Confirm the response/source details identify the exact imported commit. Confirm the user lands on Overview after an official success.
6. With no configured `origin/HEAD`, with an invalid ref, and with an invalid manifest, confirm safe errors appear, no fallback occurs, and any previous official snapshot remains loaded.
7. Confirm Overview date comes from the official reporting/as-of date; check current phase, next dated control point, up to three existing alerts, completion count, and effort coverage.
8. Use a sparse-effort fixture. Confirm percentage is withheld, eligible/total count is visible, any shown effort totals are labeled partial, and completed count remains separate.
9. Confirm Overview and Gantt are primary; WBS/Kanban/technical routes remain accessible through Advanced. Use keyboard only for navigation and inspect focus visibility.
10. Repeat the unloaded state and Overview at 360px and 1280px widths. Gantt timeline scrolling remains inside its own surface.

## Source safety

- The source repository is read-only. Do not run fetch, checkout, reset, or ref-update commands as part of default-ref import.
- Do not add private repository paths, credentials, source documents, or personal data to fixtures, snapshots, screenshots, or docs.
- Browser local storage must contain only repository-root and manifest-path preferences for this feature.
