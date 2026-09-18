# Management-app UX patterns for the Project Management Compiler

Date: 2026-09-18
Scope: Asana, Jira Plans / Advanced Roadmaps, Smartsheet, ClickUp, Linear, and OpenProject
Method: Review of official product documentation only. The source material is used to identify observable interaction patterns; the recommendations at the end are design inferences for this repository, not claims that any one product is a universal standard.

## Executive synthesis

The most useful management products do not treat a Gantt chart as the whole product. They layer several jobs into one coherent surface:

1. **Structure:** a hierarchy/WBS that explains what the work is.
2. **Time:** a scale that explains when work is planned to happen.
3. **Logic:** dependencies and milestones that explain why dates constrain one another.
4. **Execution:** status, progress, health, alerts, and actual evidence.
5. **Inspection:** a detail surface that lets a user understand or update the selected item without losing the context of the plan.

Across the products reviewed, the recurring UX shape is a split view: identity and hierarchy on the left, chronological schedule on the right. Smartsheet documents this explicitly as a grid beside a chronological chart; OpenProject exposes a work-package list with a timeline and a detail view; Asana and ClickUp use bars/connectors on a zoomable timeline. [Smartsheet Gantt](https://help.smartsheet.com/articles/765675-work-with-gantt-chart), [OpenProject Gantt chart](https://www.openproject.org/docs/user-guide/gantt-chart/), [Asana timeline dependencies](https://help.asana.com/s/article/managing-tasks-and-dependencies-with-timeline?language=en_US), [ClickUp Gantt planning](https://help.clickup.com/hc/en-us/articles/40982447293207-How-to-use-Gantt-charts-for-project-planning)

The strongest lessons for this project are:

- Keep the **PLAN / ACTUAL / ALERT** distinction visible. Planning dates are not the same thing as actual work or a derived warning.
- Make the **hierarchy and identity stable** while the timeline changes scale. Users should always know whether they are looking at a project, phase, work package, delivery card, or milestone.
- Treat **dependency connectors as semantic feedback**, not decoration: a normal relation is quiet, a selected/related relation is emphasized, and a violated or off-track relation is red.
- Make **summary rows explain themselves**. A parent row should roll up child dates/progress, and should not look like an independently authored task when its values are derived.
- Make advanced analysis such as **critical path, driving path, and dependency maps opt-in modes**. They are useful for diagnosis, but they should not dominate the default planning view.
- Use **explicit dates and an explicit as-of marker**. A management UI should never make a calculated status look like a source-authored fact.

## Product observations

### Asana: timeline as a planning and dependency-editing surface

**Sourced facts**

- Asana's timeline displays dated work as bars; dragging a bar or its edges changes the task's dates and synchronizes that change across other project views. Tasks without dates do not appear as timeline bars. [Plan and execute projects with a deadline](https://help.asana.com/s/article/plan-and-execute-projects-with-a-deadline?language=en_US)
- Dependencies can be created by dragging from one timeline item to another. Asana shows the relation as a connector, and uses a red connector when dependent work overlaps or conflicts. Its list view exposes separate `Blocked by` and `Blocking` fields, including multiple dependencies. [Managing tasks and dependencies with Timeline](https://help.asana.com/s/article/managing-tasks-and-dependencies-with-timeline?language=en_US), [Task dependencies](https://help.asana.com/s/article/task-dependencies?language=en_US)
- Timeline supports several zoom levels, including hours, days, weeks, months, quarters, half-year, and years. Asana also documents options for how dependency shifts should maintain, consume, or ignore buffers. [Managing tasks and dependencies with Timeline](https://help.asana.com/s/article/managing-tasks-and-dependencies-with-timeline?language=en_US)
- The critical path is an optional highlighted view of the longest dependent chain. Asana requires dates and dependencies for the chain and does not highlight a path when dependencies are circular. [Critical path on Timeline](https://help.asana.com/s/article/critical-path-on-timeline)

**Design implication**

Asana demonstrates a useful separation between direct manipulation of dates and higher-order diagnosis. The default interaction can stay simple—select, inspect, and connect—while critical path and buffer behavior remain deliberate modes rather than permanent visual noise.

### Jira Plans / Advanced Roadmaps: dependency risk and configurable hierarchy

**Sourced facts**

- Advanced Roadmaps represents dependencies with lines or numbered badges. Dependencies that are off track turn red, making schedule risk visible without changing the underlying work item. [What are dependencies in Advanced Roadmaps?](https://support.atlassian.com/jira-software-cloud/docs/what-are-dependencies-in-advanced-roadmaps/)
- Jira provides a dedicated dependency view/map and allows dependencies to be filtered or refined separately from the main plan. [View and manage dependencies in Advanced Roadmaps](https://support.atlassian.com/jira-software-cloud/docs/view-and-manage-dependencies-in-advanced-roadmaps/)
- Its hierarchy is configurable. Jira documents default levels such as Epic, Story, and subtask, and allows additional levels above Epic through configured work types. [Configure custom hierarchy levels in Advanced Roadmaps](https://support.atlassian.com/jira-software-cloud/docs/configure-custom-hierarchy-levels-in-advanced-roadmaps/)

**Design implication**

Do not bury dependency risk in a generic status color. A dedicated dependency mode/filter and a compact red exception state scale better than making every connector visually loud. Also, hierarchy is a product decision: the UI needs a stable vocabulary and a clear mapping from domain entities to visual levels.

### Smartsheet: grid/timeline parity and derived parent rows

**Sourced facts**

- Smartsheet's Gantt view explicitly pairs a left-side grid with a right-side chronological chart. Bars represent task durations, diamonds represent milestones, and the view can show dependencies, critical path, and baseline information. [Work with a Gantt chart](https://help.smartsheet.com/articles/765675-work-with-gantt-chart)
- When hierarchy and dependencies are enabled, parent-row values can be calculated from child rows and become non-editable. Dates adjust as predecessor information changes. [Enabling dependencies using predecessors](https://help.smartsheet.com/articles/765727-enabling-dependencies-using-predecessors)
- Smartsheet distinguishes critical path from a driving path and a summary path, and supports filtering to focus on critical or non-critical work. [Critical, summary, and driving paths](https://help.smartsheet.com/learning-track/level-3-solutions/critical-summary-and-driving-paths)

**Design implication**

The split-pane model is not merely a visual preference: the grid is the identity/editing anchor and the chart is the temporal explanation. Derived parent values should look derived and should not imply that a user can author contradictory summary dates directly.

### ClickUp: flexible hierarchy, sparse milestones, and visual baselines

**Sourced facts**

- ClickUp's Gantt chart plots task start/due dates and duration, dependencies, progress, milestones, and a critical path. A task needs both a start date and a due date to appear as a bar. [How to use Gantt charts for project planning](https://help.clickup.com/hc/en-us/articles/40982447293207-How-to-use-Gantt-charts-for-project-planning)
- ClickUp supports several time scales and marks milestones with a diamond. It advises using milestones sparingly, because too many reduce their signaling value. [How to use Gantt charts for project planning](https://help.clickup.com/hc/en-us/articles/40982447293207-How-to-use-Gantt-charts-for-project-planning), [Milestones](https://help.clickup.com/hc/en-us/articles/6304458574615-Milestones)
- Gantt views can include progress bars, non-working-day shading, and baselines that act as visual snapshots of task dates for comparison with the current plan. [Create and share a Gantt view](https://help.clickup.com/hc/en-us/articles/6310249474967-Create-and-share-a-Gantt-view)
- ClickUp's own planning guidance differentiates sequential/deadline-bound/dependency-heavy work from fluid support or discovery work, where a Gantt can be less useful. [How to use Gantt charts for project planning](https://help.clickup.com/hc/en-us/articles/40982447293207-How-to-use-Gantt-charts-for-project-planning)

**Design implication**

Milestones should be rare, legible gates—not another task color. A future baseline mode is valuable only if it compares two real snapshots; it must not be confused with a fabricated forecast or an actual-work record.

### Linear: high-level roadmap with milestones and project dependencies

**Sourced facts**

- Linear's timeline is intentionally high-level: it surfaces projects rather than granular issues, and can show milestones and project dependencies across week, month, quarter, and year scales. [Timeline](https://linear.app/docs/timeline)
- Linear milestones divide and organize a project's lifecycle. Issues can be assigned to milestones, and milestone progress is visible on project and initiative timelines. [Project milestones](https://linear.app/docs/project-milestones)
- Project dependencies are shown as blocking relationships. Linear documents a blue line for a valid dependency and a red line for a violated one; the project overview also exposes `Blocked by` and `Blocking`. [Project dependencies](https://linear.app/docs/project-dependencies)

**Design implication**

Not every management view should expose every work item. A useful product may have a high-level roadmap mode and a detailed execution/Gantt mode, both backed by the same canonical identities. Red should communicate a meaningful violation, not simply the presence of a relation.

### OpenProject: explicit scheduling modes and typed relations

**Sourced facts**

- OpenProject's Gantt view supports hierarchical work packages, dependencies, milestones, filters, saved views, zoom, auto-zoom, a detail view, and a red dotted marker for today. The UI also distinguishes dates clamped by parent/child calculations. [Gantt chart](https://www.openproject.org/docs/user-guide/gantt-chart/)
- OpenProject distinguishes manual and automatic scheduling. In manual scheduling, dates remain independently entered even when a predecessor exists; in automatic scheduling, successor dates are derived from predecessor relationships, working days, and optional lag. Parent dates can be derived from the earliest and latest child dates. [Scheduling](https://www.openproject.org/docs/user-guide/gantt-chart/scheduling/)
- Its work-package relations are typed, including `blocks`/`blocked by`, `related`, `requires`, and `parent`/`child`. The predecessor/successor relation is the relation that affects dates. [Work package relations and hierarchies](https://www.openproject.org/docs/user-guide/work-packages/work-package-relations-hierarchies/)
- A milestone can be independently dated when its relations are removed and it is manually scheduled. [Gantt chart FAQ](https://www.openproject.org/docs/user-guide/gantt-chart/gantt-chart-faq/)

**Design implication**

The distinction between authored dates, derived dates, and scheduling mode is critical for a trustworthy compiler UI. Parent/child structure must not silently become a date dependency, and a user should be able to tell whether a date is source-authored or calculated.

## Cross-product pattern matrix

| UX concern | Repeated pattern in the reviewed products | Application to this repository |
| --- | --- | --- |
| Hierarchy / WBS | Indented rows, configurable levels, collapsible summaries, and a clear distinction between parent structure and executable work. | Keep `Project → Phase → Work Package → Delivery Card`, plus milestones, visually stable. Do not flatten `WorkPackage:P04` and `DeliveryCard:P04` into one identity. |
| Timeline scale | Users switch between coarse planning and fine execution scales; fit-to-project is a practical escape hatch. | Preserve month/week/day zoom and fit-to-project. Keep date labels legible at each scale instead of adding more decoration. |
| Dependencies | Connectors or badges; relation direction matters; red is reserved for conflict, off-track, or violated dependency. | Keep typed Finish-to-Start connectors. Highlight selected/related edges, and reserve red for a derived conflict/risk state. |
| Summary rows | Parent dates/progress are often derived from children and may be read-only. | Render rollups as calculated values. The plan baseline remains immutable; do not allow a summary row to imply an independently authored date. |
| Milestones | Sparse diamond markers for gates or outcomes. | Keep milestone diamonds visually distinct and rare. Show authored milestone dates and provenance in the inspector. |
| Critical analysis | Critical path/driving path/dependency maps are optional filters or modes. | Keep critical mode available, but make the default readable for ordinary planning and execution. |
| Progress / actuals | Products combine progress, health, baseline comparison, and execution signals, but the visual layer still needs semantic distinction. | Continue separating `PLAN`, `ACTUAL`, and `ALERT`; never infer actual work merely from elapsed calendar time. |
| Date reference | Today/as-of markers and non-working-day shading orient the user in time. | Keep an explicit as-of marker; derived late-start/overdue logic must name its reason and must not silently use wall-clock time. |
| Filtering | Filter by state, risk, criticality, dependency, or hierarchy level to reduce density. | Keep phase/state/critical/overdue/at-risk/late-start filters. Treat them as a way to answer a question, not as a replacement for a readable default. |
| Selection / detail | A selected row opens a contextual detail surface while preserving the plan. | Use the current inspector as the place for identity, dates, execution evidence, alert reason, dependency context, and source provenance. |
| Collaboration / update flow | Roadmap products expose milestone/dependency health; execution products expose progress and update fields. | Keep planning evidence read-only and route mutable execution updates through the execution overlay/form. |
| Baseline / comparison | Baselines are visual snapshots of real dates, not guesses. | Add baseline comparison only when a stored, identifiable snapshot exists. Do not label calculated risk or elapsed time as forecast. |

## Recommendations for the current app

These are design inferences based on the sources above and the repository's existing domain contract in `CONTEXT.md`.

### Preserve the current foundation

- Keep the desktop-first split pane: identity/WBS on the left, timeline on the right. This is the most repeated interaction model in the review and fits the current vanilla DOM/CSS implementation.
- Keep `PLAN`, `ACTUAL`, and `ALERT` as separate visual lanes. The products reviewed make progress and risk visible, but the repository's stronger contract is that actual work and derived conditions are not interchangeable.
- Keep the explicit `AS OF` date and the safe derived alert message. It gives the user a stable answer to “late as of when?” and prevents a calculated condition from masquerading as an authored state.
- Keep typed dependency edges and identity-qualified references. Parent/child hierarchy, dependency logic, and source identity are different concepts in the products reviewed and in this domain model.

### Prioritize next if the UI is extended

1. **Make the row inspector the trust center.** Show item kind, stable identity, authored plan dates, calculated dates/conditions, actual evidence, dependency direction, and source/provenance in a consistent order. Make “derived” and “unknown” visible labels rather than relying on color alone.
2. **Add a compact view-state summary.** When filters or critical/dependency mode are active, show a small statement such as “Critical · 12 items · As of 2026-09-28”. This prevents users from interpreting a focused view as the entire project.
3. **Strengthen hierarchy affordances.** Use predictable indentation, expand/collapse state, summary-row styling, and a clear read-only treatment for calculated parent dates. The user should understand why a summary bar starts or ends where it does.
4. **Use progressive disclosure for connectors.** Keep normal edges low-contrast; emphasize selected/related edges; surface labels and reason IDs in the inspector; use red only for overlap, off-track, or violated conditions.
5. **Treat milestone gates as events.** Keep diamonds sparse, show the gate name and date in the inspector, and visually distinguish a decision gate from ordinary delivery work.
6. **Add saved view presets before adding more controls.** Useful presets would be `Plan`, `Execution`, `Risk`, `Dependencies`, and `Critical path`. The source products repeatedly use filters and focused views to manage density; presets make that power discoverable without expanding the default toolbar indefinitely.
7. **Consider baseline comparison as a separate mode.** If implemented, show baseline and current plan as two explicitly named date layers, with snapshot date and provenance. Do not use the baseline layer to invent actuals or forecasts.
8. **Keep the default view calm.** A Gantt becomes tiring when every row has a connector, label, color, and badge. Reserve the strongest visual treatments for selection, risk, milestones, and the current as-of boundary.

### Guardrails for this repository

- No external JS, CDN, or charting dependency is needed for these patterns; the split pane, connectors, markers, filters, and inspector can remain DOM/CSS primitives.
- Do not add plan dragging unless the product contract explicitly allows editing the source baseline. The reviewed products support direct date editing, but this compiler currently treats the extracted baseline as authoritative evidence.
- Do not turn `ACTUAL` into elapsed time. Actual work remains unknown when the source has planning only.
- Do not collapse a typed dependency into a generic blocker badge. Preserve direction and canonical kind/id.
- Do not make every alert red. Use the alert type and reason text to explain the condition; reserve red for actionable conflict or violation.
- Do not silently replace an authored date with a derived parent or dependency date. Show the distinction in the row and inspector.

## Suggested review questions for the next UI pass

1. Can a first-time user identify the selected item's kind and stable identity in under a few seconds?
2. Can the user tell which dates are authored plan dates, which are actual evidence, and which are derived conditions without opening developer tools?
3. When a connector is red, can the user tell exactly what relation is violated and why?
4. Does collapsing a phase reduce noise while retaining enough summary information to make the phase useful?
5. Does the default view answer “what is planned, what is happening, and what needs attention?” without requiring critical mode?
6. Can every dense view be reset to a known, named preset?

## Source index

All sources used in this note are official product documentation:

- [Asana Timeline and dependencies](https://help.asana.com/s/article/managing-tasks-and-dependencies-with-timeline?language=en_US)
- [Asana critical path](https://help.asana.com/s/article/critical-path-on-timeline)
- [Jira Advanced Roadmaps dependencies](https://support.atlassian.com/jira-software-cloud/docs/what-are-dependencies-in-advanced-roadmaps/)
- [Jira dependency view](https://support.atlassian.com/jira-software-cloud/docs/view-and-manage-dependencies-in-advanced-roadmaps/)
- [Jira custom hierarchy](https://support.atlassian.com/jira-software-cloud/docs/configure-custom-hierarchy-levels-in-advanced-roadmaps/)
- [Smartsheet Gantt](https://help.smartsheet.com/articles/765675-work-with-gantt-chart)
- [Smartsheet predecessors](https://help.smartsheet.com/articles/765727-enabling-dependencies-using-predecessors)
- [Smartsheet critical, summary, and driving paths](https://help.smartsheet.com/learning-track/level-3-solutions/critical-summary-and-driving-paths)
- [ClickUp Gantt planning](https://help.clickup.com/hc/en-us/articles/40982447293207-How-to-use-Gantt-charts-for-project-planning)
- [ClickUp Gantt view](https://help.clickup.com/hc/en-us/articles/6310249474967-Create-and-share-a-Gantt-view)
- [ClickUp milestones](https://help.clickup.com/hc/en-us/articles/6304458574615-Milestones)
- [Linear timeline](https://linear.app/docs/timeline)
- [Linear project milestones](https://linear.app/docs/project-milestones)
- [Linear project dependencies](https://linear.app/docs/project-dependencies)
- [OpenProject Gantt](https://www.openproject.org/docs/user-guide/gantt-chart/)
- [OpenProject scheduling](https://www.openproject.org/docs/user-guide/gantt-chart/scheduling/)
- [OpenProject relations and hierarchies](https://www.openproject.org/docs/user-guide/work-packages/work-package-relations-hierarchies/)
- [OpenProject Gantt FAQ](https://www.openproject.org/docs/user-guide/gantt-chart/gantt-chart-faq/)
