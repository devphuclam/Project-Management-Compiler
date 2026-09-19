# Handoff — Gantt metadata visibility and selection contrast

## Scope closed

The Gantt UI now supports the three metadata columns as optional controls:

- `State`, `Primary owner`, and `Attention` remain visible by default;
- each column can be hidden independently from the always-visible `Columns`
  control row below the Gantt view presets;
- the task pane and timeline keep one shared responsive grid definition;
- long metadata headers truncate inside their cells instead of spilling into
  the timeline;
- selecting a row keeps timeline bars and dependency evidence fully visible;
  only unrelated task-pane metadata is de-emphasized.

## Repository state

- Product repository: `devphuclam/Project-Management-Compiler`
- Branch: `codex/gantt-column-options-visible`
- Worktree: dedicated isolated worktree for this branch
- Test commit: `aba3719` — `test: cover Gantt metadata visibility and selection contrast`
- Implementation commit: `8395eb3` — `fix: make Gantt metadata columns optional and visible`
- Follow-up test commit: `b5d8bd9` — `test: keep Gantt column controls visible`
- Follow-up implementation commit: `0fb3e50` — `fix: keep Gantt column controls visible`
- The original checkout's existing `.gitignore`, `package.json`, and
  `package-lock.json` changes were not touched.

## Verification

- Custom runner: 194 registered, 194 executed, 194 passed, 0 failed.
- `scripts/build.ps1`: pass.
- `scripts/verify.ps1`: pass, including launcher and web/API verification.
- `node --check src/ProjectManagementCompiler/wwwroot/app.js`: pass.
- `git diff --check`: pass.
- Changed-file secret/path scan: pass.

## Next step

Push this branch, fast-forward `main`, and then restart the local app before
checking the browser at `http://127.0.0.1:5050/`. Open `Plan` and use the
always-visible `Columns` checkboxes below the view presets to verify the
optional metadata layout. `Advanced filters` remains a separate disclosure
for schedule filters.
