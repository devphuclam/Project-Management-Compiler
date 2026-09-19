# Handoff — Focused Gantt workspace and dependency impact

## What changed

The Gantt view now follows the dependency-reading pattern used by established project-management tools:

- entering Gantt switches the page into a schedule-first workspace: the setup panel, dashboard summary, and execution updater leave the schedule surface;
- the application header becomes compact while Gantt is active, with a small project context bar preserving the project name, baseline window, and reporting date;
- the schedule keeps the task grid, timeline, and row inspector as the primary surface, while controls are grouped into a compact toolbar;
- selecting a row exposes an `Impact focus` control with `Depends on`, `Affects`, and `Both`;
- the selected direction highlights the corresponding upstream or downstream chain in the task pane and timeline;
- the optional connector layer draws only the selected row's direct links, so the timeline does not become a full-graph spiderweb;
- the row inspector names direct predecessors and successors instead of showing only typed keys;
- dependency connectors use readable tooltips with the direction and dependency type;
- the inspector explains the downstream scheduling consequence with a short chain preview and an expandable full successor chain;
- optional `State`, `Primary owner`, and `Attention` columns remain individually hideable.

The baseline model and execution overlay are unchanged. This is a presentation and navigation increment over the existing dependency graph.

## Demo path

1. Start the local app and load `tests/fixtures/ideaengineering-real-shaped`.
2. Open `Gantt`.
3. Confirm the summary dashboard is no longer above the schedule; the compact header and project context remain.
4. Select a delivery card such as `P04`.
5. Use `Depends on` to isolate predecessor work, `Affects` to isolate successor work, or `Both` to see the complete neighborhood.
6. Use `Show dependencies` when the connector paths are needed. Only the selected card's direct links are drawn; hovering a connector exposes the named predecessor, successor, and dependency type.
7. Read `DEPENDENCY IMPACT` in the row inspector. Expand `Show full successor chain` only when the short preview is not enough.
8. Return to `Dashboard` to restore the control-center summary and the normal application chrome.

## Verification

- `node --check src/ProjectManagementCompiler/wwwroot/app.js`
- `dotnet run --project tests/ProjectManagementCompiler.Tests/ProjectManagementCompiler.Tests.csproj --no-restore`
- `dotnet build ProjectManagementCompiler.sln --no-restore`
- `pwsh -NoLogo -NoProfile -ExecutionPolicy Bypass -File scripts/verify.ps1`

The full test suite and verification gate pass. The verification output reports `Health: ok`, 53 cards, 6 workbook sheets, and `SecurityChecks: PASS`.

## Design evidence

This interaction follows the documented behavior of established tools: Jira filters a selected work item's dependency chain, ClickUp uses arrows between directly connected tasks, and Microsoft Project keeps dependency links separate from the date grid. The implementation keeps the same distinction: direct links are optional visual aids; the inspector and impact highlight remain the readable source for the full relationship.

## Next handoff action

Keep the branch increment focused on the Gantt dependency-reading experience. Before merging, inspect the diff for generated files and public-repository safety, then fast-forward `main`, rebuild the app from the authoritative checkout, and push `main` without committing unrelated local package files.
