# Handoff — Approved Gantt visual redesign

## Outcome

The approved visual proposal is implemented in the source-backed Gantt. The application keeps the existing canonical planning, execution, provenance, and CPM boundaries while replacing the dense presentation layer.

The production behavior now is:

- the default Plan view starts at week scale with `Task / ID` and `State` visible;
- `Primary owner` and `Attention` are optional in the compact `Columns` menu, and the task pane expands with enabled columns;
- secondary modes, hierarchy commands, and filters live in `View options` instead of occupying permanent toolbar rows;
- the default Plan canvas collapses unused ACTUAL sub-lanes, removes repeated no-evidence dashes, and shows week/month boundaries instead of dense daily rules;
- selecting a task automatically opens dependency focus and draws only its direct predecessor/successor connectors;
- predecessor connectors and rows are blue, successor connectors and rows are green, and the selected row is teal;
- task detail opens as a dismissible overlay drawer, so the chart keeps its width when no task is selected;
- the drawer states the direct relationship as `Depends on → Selected → Blocks`, then discloses the short downstream preview separately;
- full execution, CPM, management, source, provenance, and alert detail remains available through drawer disclosures.

## Demo

1. Run `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File .\scripts\run-project.ps1`.
2. Load `tests\fixtures\ideaengineering-real-shaped` with as-of date `2026-09-19`.
3. Open `Gantt` and confirm the default header is `TASK / ID · STATE` with no permanent inspector.
4. Open `Columns` to toggle `Primary owner` or `Attention` independently.
5. Select delivery card `P04`.
6. Confirm the chart shows only `P03 → P04` and `P04 → P06`; the drawer reads `P03 / Depends on`, `P04 / Selected`, and `P06 / Blocks`.
7. Expand `Show downstream impact` to see `P04 → P06 → P07` plus the count and terminal item for the remaining project chain.
8. Close the drawer or press Escape to return to the uncluttered Plan canvas.

## TDD and verification

The UI contract was updated before implementation and observed failing against the prior Gantt. The implementation then made the regression suite green. Coverage includes compact column defaults, menu placement, selection-first direct connectors, drawer behavior, dependency flow wording, progressive downstream disclosure, calm weekly grid, and mode-strip semantics.

Verified commands:

- `node --check src/ProjectManagementCompiler/wwwroot/app.js`
- `.\scripts\test.ps1`
- `.\scripts\verify.ps1`

The final verification reports a clean build with zero warnings/errors, all tests passing, `Health: ok`, 53 cards, six XLSX sheets, and `SecurityChecks: PASS`. Browser verification used the public real-shaped fixture at a 1280-pixel desktop viewport; default, selected P04, Columns, downstream disclosure, close, and console-error states were checked. No browser warnings or errors were recorded.

## Git state at handoff

- Branch: `codex/gantt-visual-proposal`
- Visual prototype commit: `90ebaf2`
- TDD contract commit: `4e72d95`
- Production implementation commit: `9fb1d3f`
- The local `package.json` and `package-lock.json` remain untracked and were intentionally excluded.
- Nothing from this increment has been pushed or merged at the time this handoff was written.
