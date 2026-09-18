# Gantt UX / Visual Design Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the MVP1 table-like Gantt view with an accessible, date-driven split-pane timeline while preserving the compiler's canonical PLAN/ACTUAL/ALERT, typed dependency, CPM, execution-overlay, and provenance semantics.

**Architecture:** Keep the management projection and `/api/execution` contract as the semantic source of truth. Add a deep presentation module inside the existing vanilla browser adapter: it consumes `views.wbs`, `views.gantt`, `views.dependencyNetwork`, `views.cpm`, `summary.analysis.asOfDate`, and `summary.baseline`, then owns only row flattening, display filtering, date-to-pixel mapping, SVG connector rendering, and DOM event delegation. The left task grid and right timeline share one row model and one vertical scroll surface so hierarchy, selection, filters, and lanes cannot drift apart.

**Tech Stack:** ASP.NET Core .NET 10 static web assets, vanilla JavaScript DOM/SVG APIs, CSS Grid/sticky positioning, PowerShell verification, existing dependency-free C# test runner.

**Spec:** Approved Gantt UX / visual-design remediation request in the task attachment; semantic constraints are also defined by `CONTEXT.md`, `docs/superpowers/specs/2026-09-17-project-management-compiler-design.md`, and `specs/001-mvp1-project-management-compiler/spec.md`.

## Global Constraints

- Preserve source PLAN values and never make planned bars draggable or editable.
- Keep ACTUAL evidence separate from PLAN; an in-progress actual lane ends at the explicit analysis `asOfDate` already supplied to the management engine.
- Render ALERT as a marker/annotation, not a fabricated schedule bar or forecast.
- Resolve dependency identity as `(kind, id)` so `WorkPackage:P04` and `DeliveryCard:P04` remain distinct; only analysis-eligible typed edges may become execution connectors.
- Do not add a frontend framework, external Gantt library, CDN asset, third-party JavaScript, canvas dependency, or network request from the browser UI.
- Use DOM construction and `textContent`; do not introduce `innerHTML` or unsafe interpolation of source names.
- Filters, zoom, expand/collapse, selection, and dependency visibility are presentation state only and must not mutate canonical or analysis data.
- Preserve desktop-first responsive behavior, keyboard focus, ARIA labels, non-color-only state cues, and the existing loopback-only application contract.

---

### Task 1: Lock the browser contract with failing verification assertions

**Files:**
- Modify: `scripts/verify-web.ps1` in the browser/static-contract assertion section.
- Test: `scripts/verify-web.ps1` itself, executed through `scripts/verify.ps1`.

**Interfaces:**
- Consumes: the existing real-shaped fixture, compiled local API, and static `app.js`/`index.html` text.
- Produces: a regression gate that fails while the old table Gantt remains and proves the required Gantt presentation seams exist.

- [ ] **Step 1: Write the failing assertions**

Add assertions after the existing browser security assertions for these required seams:

```powershell
Assert-Condition ($appJs.Contains('gantt-timeline', [StringComparison]::Ordinal)) 'Gantt renderer must expose a split timeline surface.'
Assert-Condition ($appJs.Contains('gantt-as-of-marker', [StringComparison]::Ordinal)) 'Gantt renderer must expose an explicit as-of marker.'
Assert-Condition ($appJs.Contains('gantt-plan-bar', [StringComparison]::Ordinal)) 'Gantt renderer must render immutable PLAN bars.'
Assert-Condition ($appJs.Contains('gantt-actual-bar', [StringComparison]::Ordinal)) 'Gantt renderer must render ACTUAL bars.'
Assert-Condition ($appJs.Contains('gantt-alert-marker', [StringComparison]::Ordinal)) 'Gantt renderer must render ALERT markers.'
Assert-Condition ($appJs.Contains('gantt-milestone', [StringComparison]::Ordinal)) 'Gantt renderer must render milestone markers.'
Assert-Condition ($appJs.Contains('Show dependencies', [StringComparison]::Ordinal)) 'Gantt toolbar must expose dependency visibility.'
Assert-Condition ($appJs.Contains('Expand all', [StringComparison]::Ordinal)) 'Gantt toolbar must expose expand-all.'
Assert-Condition ($appJs.Contains('Collapse all', [StringComparison]::Ordinal)) 'Gantt toolbar must expose collapse-all.'
Assert-Condition ($appJs.Contains('fit-project', [StringComparison]::Ordinal)) 'Gantt toolbar must expose fit-project.'
Assert-Condition ($appJs.Contains('critical-only', [StringComparison]::Ordinal)) 'Gantt filters must expose critical-only.'
Assert-Condition (-not $appJs.Contains('draggable', [StringComparison]::OrdinalIgnoreCase)) 'PLAN bars must not be draggable.'
Assert-Condition (-not $indexHtml.Contains('cdn.', [StringComparison]::OrdinalIgnoreCase)) 'Gantt must not add CDN assets.'
$ganttStart = $appJs.IndexOf('function renderGantt', [StringComparison]::Ordinal)
$ganttEnd = $appJs.IndexOf('function renderKanban', [StringComparison]::Ordinal)
Assert-Condition ($ganttStart -ge 0 -and $ganttEnd -gt $ganttStart) 'Gantt renderer source boundary must be discoverable.'
$ganttSource = $appJs.Substring($ganttStart, $ganttEnd - $ganttStart)
Assert-Condition (-not $ganttSource.Contains('FORECAST', [StringComparison]::OrdinalIgnoreCase)) 'Gantt must not fabricate forecast presentation.'
```

- [ ] **Step 2: Run the targeted verification to prove RED**

Run:

```powershell
.\scripts\verify.ps1
```

Expected: the build and existing tests may pass, but `verify-web.ps1` fails at the first new Gantt seam assertion because the current renderer has no split-pane timeline contract.

- [ ] **Step 3: Commit the red contract**

```powershell
git add scripts/verify-web.ps1
git commit -m "test: lock gantt remediation browser contract"
```

### Task 2: Build the presentation row and timeline module

**Files:**
- Modify: `src/ProjectManagementCompiler/wwwroot/app.js` in the current Gantt rendering and browser event-registration sections.
- Test: `scripts/verify-web.ps1` plus the real-shaped API assertions it already runs.

**Interfaces:**
- Consumes: WBS nodes for hierarchy and authored planned dates; Gantt items/milestones for execution lanes and alerts; dependency-network edges for typed connectors; CPM rows for the opt-in critical set; `summary.analysis.asOfDate` and `summary.baseline` for the explicit analysis marker and project range.
- Produces: `renderGantt(view, analysis, baseline)` as the only Gantt entry point, a flat row model with `rowKey`, `kind`, `id`, `parentKey`, `depth`, `children`, `isSummary`, `plan`, `actual`, `alerts`, `milestone`, `dependencyKeys`, `isCritical`, `state`, and `roles`, and a presentation state containing zoom, filters, expansion, selection, and dependency visibility.

- [ ] **Step 1: Add the failing static/data assertions for the new seams**

Extend the Task 1 contract checks to require the named implementation seams and semantic inputs:

```powershell
Assert-Condition ($appJs.Contains('views.dependencyNetwork.edges', [StringComparison]::Ordinal)) 'Gantt connectors must consume typed dependency-network edges.'
Assert-Condition ($appJs.Contains('analysis.asOfDate', [StringComparison]::Ordinal)) 'Gantt must use the explicit analysis as-of date.'
Assert-Condition ($appJs.Contains('timelineStart', [StringComparison]::Ordinal) -and $appJs.Contains('timelineEnd', [StringComparison]::Ordinal)) 'Gantt must derive a project timeline range from authored dates.'
Assert-Condition ($appJs.Contains('selectedRowKey', [StringComparison]::Ordinal)) 'Gantt must keep selection as presentation state.'
Assert-Condition ($appJs.Contains('actualFinish || analysis.asOfDate', [StringComparison]::Ordinal)) 'Gantt must label open actual work through the supplied as-of date.'
```

- [ ] **Step 2: Run verification and observe RED**

Run `.\scripts\verify.ps1`; the new assertions must fail before the renderer is implemented.

- [ ] **Step 3: Implement the smallest presentation module**

Replace the existing table renderer with DOM-only helpers that:

```javascript
function buildGanttRows(wbsRoot, ganttView) { /* flatten project/phase/WP/card/milestone */ }
function buildTimelineRange(rows, baseline) { /* authored baseline + visible milestone/card dates */ }
function buildDateScale(start, end, zoom) { /* day/week/month display ticks only */ }
function renderGantt(view, analysis, baseline) { /* toolbar + split grid + one row model */ }
```

The implementation must use `node()`, `textContent`, `createElementNS()` for SVG, and `style.setProperty()` for positions. Phase and work-package rows use their existing authored WBS dates as summary presentation spans; no new schedule calculation is introduced. Card PLAN/ACTUAL/ALERT data stays sourced from `view.items`. Milestones get a single zero-duration diamond at `plannedDate`. The row key is `kind + ':' + id`; connector eligibility requires `edge.includedInAnalysis`, and endpoints resolve through the same typed key.

- [ ] **Step 4: Run the targeted verification and the existing test suite**

Run:

```powershell
.\scripts\verify-web.ps1
.\scripts\test.ps1
```

Expected: all new static contract checks and all existing tests pass.

- [ ] **Step 5: Commit the semantic renderer increment**

```powershell
git add src/ProjectManagementCompiler/wwwroot/app.js scripts/verify-web.ps1
git commit -m "feat: add typed gantt timeline projection"
```

### Task 3: Add split-pane layout, lanes, controls, and accessible visual language

**Files:**
- Modify: `src/ProjectManagementCompiler/wwwroot/index.html` only if a stable Gantt mount or detail-region landmark is needed; keep the existing execution form contract unchanged.
- Modify: `src/ProjectManagementCompiler/wwwroot/styles.css` in the Gantt/table responsive sections.
- Modify: `src/ProjectManagementCompiler/wwwroot/app.js` for toolbar and row event delegation.
- Test: `scripts/verify-web.ps1` static security/accessibility assertions and manual browser review against the real-shaped fixture.

**Interfaces:**
- Consumes: Task 2 row model and presentation state.
- Produces: a sticky task identity pane, horizontally scrollable chronological timeline, synchronized row heights, compact toolbar, filters, selection detail panel, and focusable execution action.

- [ ] **Step 1: Add failing layout/security assertions**

Add these checks to `verify-web.ps1`:

```powershell
Assert-Condition ($appJs.Contains('gantt-grid', [StringComparison]::Ordinal)) 'Gantt must expose the split task/timeline grid.'
Assert-Condition ($appJs.Contains('gantt-detail-panel', [StringComparison]::Ordinal)) 'Gantt must expose selected-item details.'
Assert-Condition ($appJs.Contains('Record execution', [StringComparison]::Ordinal)) 'Gantt selection must link to the execution update action.'
Assert-Condition ($appJs.Contains('textContent', [StringComparison]::Ordinal)) 'Browser UI must render untrusted labels with textContent.'
Assert-Condition (-not $appJs.Contains('innerHTML', [StringComparison]::OrdinalIgnoreCase)) 'Browser UI must not use unsafe innerHTML rendering.'
```

- [ ] **Step 2: Run verification and observe RED**

Run `.\scripts\verify.ps1`; the new layout assertions must fail until the split-pane markup and renderer are present.

- [ ] **Step 3: Implement the layout and event behavior**

Add CSS classes for `.gantt-shell`, `.gantt-toolbar`, `.gantt-grid`, `.gantt-task-pane`, `.gantt-timeline-pane`, `.gantt-timeline`, `.gantt-row`, `.gantt-plan-bar`, `.gantt-actual-bar`, `.gantt-alert-marker`, `.gantt-milestone`, `.gantt-as-of-marker`, `.gantt-weekend`, `.gantt-detail-panel`, `.gantt-selected`, `.gantt-related`, and `.gantt-dimmed`. Use CSS Grid for identity/timeline alignment, `position: sticky` for the left pane and timeline header, 32–40px row heights, subtle weekend backgrounds, and media queries that preserve a usable horizontal scroll on narrow screens.

Add event delegation for toolbar buttons, filter changes, row click/keyboard activation, dependency toggle, zoom controls, fit, expand/collapse, and `Record execution`. Selecting a DeliveryCard fills the existing `#execution-work-item` field and focuses the form; it does not issue a baseline mutation. Detail content must show PLAN, ACTUAL, `asOfDate`, variance when supplied, typed dependencies, logical roles, and alert reasons using safe text nodes.

- [ ] **Step 4: Run the web verification and browser review**

Run:

```powershell
.\scripts\verify-web.ps1
```

Then launch the local application against `tests/fixtures/ideaengineering-real-shaped` and review at project-fit, month/week/day zoom, critical-path on, dependencies on, selected actual/overdue/at-risk row, and collapsed/expanded phase states. Confirm no console error, duplicate row, hidden as-of marker, large UNKNOWN pill, fabricated forecast, or P04 typed-edge collision.

- [ ] **Step 5: Commit the visual increment**

```powershell
git add src/ProjectManagementCompiler/wwwroot/index.html src/ProjectManagementCompiler/wwwroot/styles.css src/ProjectManagementCompiler/wwwroot/app.js scripts/verify-web.ps1
git commit -m "feat: deliver accessible gantt split pane"
```

### Task 4: Complete regression verification and document the demo path

**Files:**
- Modify: `scripts/verify-web.ps1` to assert the final real-shaped behavior and no external assets.
- Modify: `docs/runbook/mvp1-local.md` with the exact local Gantt demo sequence and controls.
- Test: `scripts/build.ps1`, `scripts/test.ps1`, `scripts/verify-web.ps1`, and `scripts/verify.ps1`.

**Interfaces:**
- Consumes: the completed browser projection and existing real-shaped fixture.
- Produces: reproducible verification evidence for hierarchy, geometry inputs, plan immutability, actual/alert semantics, typed dependencies, filters, security, and local demo operation.

- [ ] **Step 1: Add final regression assertions before the implementation is considered complete**

The verification script must exercise the API after a P04 actual-start update and assert that the returned P04 PLAN lane still starts at `2026-09-23`, ACTUAL starts at `2026-09-25`, and the P06 AT_RISK reason still names P04. Its static checks must reject external script/link URLs, `innerHTML`, `draggable`, and forecast labels in the Gantt renderer. It must also assert the project has one DeliveryCard P04 row and two typed dependency nodes for the P04 ID.

- [ ] **Step 2: Run the full verification commands**

Run in order:

```powershell
.\scripts\build.ps1
.\scripts\test.ps1
.\scripts\verify-web.ps1
.\scripts\verify.ps1
git status --short --branch
git diff --check HEAD~3..HEAD
```

Record the exact exit codes, test count, web verification JSON, current branch, and current commit SHA. Any failure remains a blocker; do not claim completion from a partial run.

- [ ] **Step 3: Commit runbook evidence**

```powershell
git add docs/runbook/mvp1-local.md scripts/verify-web.ps1
git commit -m "docs: document gantt remediation demo and verification"
```

- [ ] **Step 4: Inspect the final diff and repository hygiene**

Run `git status --short --branch`, `git diff --stat main...HEAD`, `git diff --check main...HEAD`, and inspect the changed-file list for credentials, generated output, source copies, or temporary screenshots before requesting integration.

- [ ] **Step 5: Hand off for integration choice**

Use `finishing-a-development-branch` after the full suite is green and present the exact three integration options for the `main` base branch. Do not push, merge, or delete the worktree without the user's explicit integration choice.

## Self-review checklist

- [ ] The only schedule semantics consumed by the browser are already authoritative values from the existing projections and explicit `analysis.asOfDate`.
- [ ] The row model represents Project, Phase, WorkPackage, DeliveryCard, and Milestone without duplicating executable cards.
- [ ] PLAN, ACTUAL, and ALERT remain spatially comparable and visually distinct without color-only meaning.
- [ ] P04 WorkPackage and P04 DeliveryCard use different typed row/edge keys.
- [ ] Milestones are zero-duration diamonds and do not become bars.
- [ ] Filters/zoom/collapse never mutate the source or analysis objects.
- [ ] Dependency connectors are progressive disclosure and never draw traceability-only work-package relations.
- [ ] The explicit as-of marker is visible even when the browser's current date differs.
- [ ] No `innerHTML`, CDN, external asset, third-party package, plan drag, forecast, credential, or proprietary source material is introduced.
- [ ] The final verification evidence includes build, tests, web verification, diff hygiene, and a visual review of all required states.
