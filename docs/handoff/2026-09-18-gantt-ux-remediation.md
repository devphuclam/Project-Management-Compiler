# Handoff: Gantt UX / Visual Design Remediation

Date: 2026-09-18
Repository: `devphuclam/Project-Management-Compiler`
Baseline before this increment: `e9a7447edb9b2f25eb12b7f9941e82e5178d9583`
Original working branch: `codex/005-gantt-ux-remediation`
Follow-up worktree branch: `codex/gantt-remediation`
Follow-up base: `318750a`

## Current state

The original Gantt remediation increment is implemented and already integrated
into `main` at `cf090dc`. This document preserves the historical branch and
commit context; it is not a request to integrate that old branch again.

The current trust/presentation follow-up has been implemented from that `main`
baseline in the working tree. It remains a separate, uncommitted refinement
pass; no commit, merge, or push was requested for this pass.

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
  - Trust-center inspector with safe source evidence, authored/derived plan
    boundaries, actual effort/last-update fields, calculated CPM labeling,
    typed alerts, and decision-gate labels.
  - Execution-evidence and Critical path presets, compact visible-row state,
    open actual bars, finish-only completion markers, and keyboard-operable
    alert markers.
  - No plan dragging, forecast fabrication, `innerHTML`, or external assets.
- `src/ProjectManagementCompiler/wwwroot/styles.css`
  - Split task-identity/timeline layout, sticky headers, weekend shading,
    lane styles, markers, connectors, selected/related states, and responsive
    overflow behavior.
- `scripts/verify-web.ps1`
  - Static and real-shaped API contracts for the trust/presentation seams.
- `docs/research/2026-09-18-management-apps-ux-patterns.md`
  - Official Microsoft Project/Planner Premium and Oracle Primavera P6/Cloud
    research plus MVP2 deferrals.
- `docs/superpowers/plans/2026-09-18-gantt-ux-remediation.md`
  - The approved execution plan and remaining checklist.
- This handoff document.

## Verification already run

From the current working tree:

- `node --check src/ProjectManagementCompiler/wwwroot/app.js` — PASS.
- `git diff --check` — PASS.
- `./scripts/verify.ps1` — PASS (152 custom tests, API checks, web checks).
- Web verification summary:
  `Health=ok`, `ProjectId=IE-PROD-ROADMAP-001`, `Cards=53`,
  `OverdueAfterExecution=1`, `AtRiskAfterExecution=1`, `ReopenOverdue=1`,
  `ReopenAtRisk=1`, `JsonBytes=434897`, `XlsxBytes=47616`, `SheetCount=6`,
  `SecurityChecks=PASS`.

## Follow-up completion

The follow-up pass was completed in the isolated `codex/gantt-remediation`
worktree. It closes the original manual-review and verification gaps without
changing the compiler/API contract:

- Added a `Late start` filter backed by the derived `START_DELAY` alert.
- Made alert markers and the row inspector show the safe derived message, with
  reason IDs for dependency-driven risk.
- Added ISO week labels, a clear uppercase `AS OF` marker, numeric timestamp
  formatting, and removed duplicate month detail labels.
- Added a distinct critical-path bar/row treatment and synchronized 40px task,
  timeline, and connector geometry.
- Extended web/API verification for no fabricated planning-only ACTUAL lanes,
  authored milestone dates, immutable PLAN dates, and in-progress ACTUAL
  displayed through the explicit as-of date while remaining open-ended.
- Reviewed the real-shaped fixture manually at `2026-09-28` through month,
  week, and day zoom; hierarchy expansion; critical/dependency mode; typed
  `WorkPackage:P04` versus `DeliveryCard:P04`; P04 overdue/start-delay; and P06
  at-risk reason display.
- Added the Gantt-specific controls to `docs/runbook/mvp1-local.md`.

## Current follow-up status

The trust/presentation follow-up is complete in the working tree and is not
yet integrated into `main` or pushed. It keeps the baseline immutable and does
not add MVP2 schedule-comparison features. The final review covered:

- typed `WorkPackage:P04` versus `DeliveryCard:P04` inspector variance;
- safe item provenance and per-boundary authored/derived plan origins;
- in-progress P04 shown through `AS OF` while actual finish stays UNKNOWN;
- finish-only P05 completion shown as a dated ACTUAL finish marker without an
  invented start;
- P04 overdue/start-delay and P06 at-risk successor in the Risks preset;
- Execution evidence, Dependencies, and Critical path presets;
- decision-gate labeling, dependency arrowheads, compact view-state summary,
  and keyboard selection of alert markers.

The only current integration action is to review, commit, merge/push this
working-tree follow-up when explicitly requested.

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
