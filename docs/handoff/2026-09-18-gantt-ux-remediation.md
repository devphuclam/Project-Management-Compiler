# Handoff: Gantt UX / Visual Design Remediation

Date: 2026-09-18
Repository: `devphuclam/Project-Management-Compiler`
Baseline before this increment: `e9a7447edb9b2f25eb12b7f9941e82e5178d9583`
Working branch: `codex/005-gantt-ux-remediation`

## Current state

The current Gantt remediation increment is implemented and ready to be
integrated into `main`. It is an incremental, verified work-in-progress; it is
not a claim that every visual-review item in the approved remediation request
is complete.

Commits in this increment:

- `a86775f` — approved implementation plan.
- `25366df` — failing/static browser contract for the required Gantt seams.
- `f31b391` — split-pane timeline renderer and visual styles.

The requested integration is a fast-forward of this branch into `main`,
followed by a normal push to `origin/main`. No force-push is required.

## Files delivered by this increment

- `src/ProjectManagementCompiler/wwwroot/app.js`
  - DOM-only split-pane Gantt presentation.
  - Project → Phase → WorkPackage → DeliveryCard hierarchy plus milestones.
  - Separate PLAN, ACTUAL, and ALERT lanes.
  - Explicit analysis as-of marker.
  - Month/week/day zoom, fit-to-project, expand/collapse, phase/state/critical/
    overdue/at-risk filters, selection details, and execution-form handoff.
  - Typed dependency selection/connectors and P04 WorkPackage vs DeliveryCard
    identity separation.
  - No plan dragging, forecast fabrication, `innerHTML`, or external assets.
- `src/ProjectManagementCompiler/wwwroot/styles.css`
  - Split task-identity/timeline layout, sticky headers, weekend shading,
    lane styles, markers, connectors, selected/related states, and responsive
    overflow behavior.
- `scripts/verify-web.ps1`
  - Static contract assertions committed in `25366df`.
- `docs/superpowers/plans/2026-09-18-gantt-ux-remediation.md`
  - The approved execution plan and remaining checklist.
- This handoff document.

## Verification already run

From the remediation worktree:

- `node --check src/ProjectManagementCompiler/wwwroot/app.js` — PASS.
- `git diff --check` — PASS.
- `./scripts/verify.ps1` — PASS (build, custom tests, API checks, web checks).
- Web verification summary:
  `Health=ok`, `ProjectId=IE-PROD-ROADMAP-001`, `Cards=53`,
  `OverdueAfterExecution=1`, `AtRiskAfterExecution=1`, `ReopenOverdue=1`,
  `ReopenAtRisk=1`, `JsonBytes=434312`, `XlsxBytes=47625`, `SheetCount=6`,
  `SecurityChecks=PASS`.

## Remaining work for the next session

1. Start the application from the checked-out `main` and perform the manual
   browser review using `tests/fixtures/ideaengineering-real-shaped` with
   `2026-09-28` as-of date.
2. Verify project-fit, month/week/day zoom, critical-path mode, dependency
   toggle, selection/detail panel, execution-form focus, filters, and
   collapsed/expanded phases.
3. Confirm the visible result has one typed DeliveryCard `P04`, a distinct
   WorkPackage `P04`, typed dependency edges, a visible as-of marker, no
   fabricated forecast, and no duplicate rows or console errors.
4. Extend `scripts/verify-web.ps1` with the remaining Task 2–4 static/data
   assertions in the plan, and update `docs/runbook/mvp1-local.md` with the
   Gantt-specific demo controls.
5. Re-run the full verification suite and inspect the final diff before the
   next coherent commit.

The earlier browser snapshot showed the old table renderer because it was
served by a stale/other local app instance. Treat that snapshot as a reminder
to reload the app from the current checkout, not as visual acceptance evidence.

## Continue at home

```powershell
Set-Location 'C:\Users\TD-999\Research\Projects\Project Management Compiler'
git fetch origin
git switch main
git pull --ff-only origin main
git status --short --branch
git log --oneline --decorate -8
.\scripts\verify.ps1
dotnet run --project .\src\ProjectManagementCompiler\ProjectManagementCompiler.csproj --no-restore
```

Open `http://127.0.0.1:5050`, load
`tests\fixtures\ideaengineering-real-shaped`, set the explicit as-of date to
`2026-09-28`, and select **Analyze source**. The detailed local runbook is
`docs/runbook/mvp1-local.md`; the implementation checklist is
`docs/superpowers/plans/2026-09-18-gantt-ux-remediation.md`.

Before any further push, inspect `git status --short --branch`, run the full
verification commands, run `git diff --check`, and confirm no generated files,
credentials, private company material, or copied IDEAEngineering source has
entered the public repository.
