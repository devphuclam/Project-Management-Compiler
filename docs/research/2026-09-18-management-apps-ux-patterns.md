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

## Microsoft Project / Microsoft Planner Premium

**Source boundary.** Microsoft Project desktop and Microsoft Planner Premium overlap in scheduling concepts but are not one identical product. The Project documentation below is used for schedule calculation, status/baseline fields, slack, and variance. The Planner Premium documentation is used for the collaborative Timeline, dependency, milestone, calendar, and task-history surface. [Planner Premium advanced capabilities](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)

### Timeline / Gantt as a planning surface

- Planner Premium's Timeline view presents tasks and dependencies in a Gantt-chart format so the user can see how the schedule is laid out. [Planner Premium advanced capabilities](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)
- Microsoft Project calculates a Scheduled Finish from the scheduled start, duration, dependencies, constraints, the project calendar, task calendars, and—when applicable—resource calendars and assignment units. This is a calculated schedule output, not simply a label attached to a task. [Scheduled Finish task field](https://support.microsoft.com/en-us/project/scheduled-finish-task-field)
- Project's Tracking Gantt overlays baseline bars and scheduled bars, allowing a user to see whether the current schedule has moved against the stored plan. [Review the progress of your schedule](https://support.microsoft.com/en-us/project/review-the-progress-of-your-schedule)

### Dependency types and context

- Planner Premium documents four advanced dependency types: Finish-to-Start, Start-to-Start, Start-to-Finish, and Finish-to-Finish. Its scheduling engine updates execution dates from those relationships. [Planner Premium advanced dependencies](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)
- **Design inference:** the UI should show the relationship type and direction in the inspector or dependency detail, not only draw a generic connector. Different relationship types change which endpoint constrains the successor, so a connector without its semantics is incomplete. This inference is grounded in Microsoft's explicit four-type model and calculated schedule output. [Planner Premium advanced dependencies](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner), [Scheduled Finish task field](https://support.microsoft.com/en-us/project/scheduled-finish-task-field)

### Critical path, slack, and milestones

- Project defines the critical path as the series of tasks that dictates the project finish date. A task is generally critical when it has no slack, and the path can change as work is completed or another chain is delayed. [Manage your project's critical path](https://support.microsoft.com/en-us/project/manage-your-project-s-critical-path)
- Project calculates slack from early and late dates. Free Slack is the time a task can slip without delaying a successor; Finish Slack is the time between early and late finish. [Available fields reference](https://support.microsoft.com/en-us/project/available-fields-reference)
- Planner Premium exposes a Critical path option in Timeline and exposes milestones for marking points in the schedule. The documentation describes these as schedule views/markers, so the repository should model them as calculated analysis and event markers rather than as user-authored status values. The latter is a design inference. [Planner Premium critical path and milestones](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)

### Calendars and the reporting boundary

- Planner Premium starts from a Monday-to-Friday work template but allows a custom work week; the scheduling engine updates the plan so work is placed only on selected working days. [Planner Premium custom calendar](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)
- Project's status date is a reporting boundary used in calculated status fields, while the schedule itself also uses calendars and dependencies. The Status field classifies tasks as Complete, On Schedule, Late, or Future Task relative to that status date. [Status task field](https://support.microsoft.com/en-us/project/status-task-field), [Scheduled Finish task field](https://support.microsoft.com/en-us/project/scheduled-finish-task-field)
- **Repository implication:** keep asOfDate explicit and visible. It can be the repository's reporting boundary for derived alerts, but it must not be presented as an actual completion date or as a stored baseline date.

### Baseline as a stored snapshot, not a forecast

- Project describes a baseline as a group of reference points captured when the original plan is refined; it can include start, finish, duration, work, and cost information, and additional numbered baselines can be stored. [Create or update a baseline or an interim plan](https://support.microsoft.com/en-us/project/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop)
- When a baseline is saved, Project copies the task's planned completion into Baseline Finish. That field remains unavailable until a baseline exists, and it is intended for comparison with scheduled or actual finish. [Baseline Finish fields](https://support.microsoft.com/en-us/project/baseline-finish-fields)
- Project's variance views compare baseline data with scheduled and actual data; its Variance table and Tracking Gantt make the comparison visible in both tabular and graphical forms. [Create or update a baseline or an interim plan](https://support.microsoft.com/en-us/project/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop), [Review the progress of your schedule](https://support.microsoft.com/en-us/project/review-the-progress-of-your-schedule)

### Baseline / Actual / Scheduled Finish separation

| Field concept | Meaning in the Microsoft documentation | Trust treatment for this repository |
| --- | --- | --- |
| Baseline Finish | Planned completion copied into the baseline when the snapshot is saved. | Immutable stored plan evidence; never overwrite it with a later schedule. [Baseline Finish fields](https://support.microsoft.com/en-us/project/baseline-finish-fields) |
| Scheduled Finish | Current date calculated from schedule inputs such as duration, dependencies, constraints, and calendars. | Derived plan output; show its calculation/provenance separately from source-authored dates. [Scheduled Finish task field](https://support.microsoft.com/en-us/project/scheduled-finish-task-field) |
| Actual Finish | Date and time when the task or assignment was completed. | Execution evidence; keep unknown when the source has planning only. [Available fields reference](https://support.microsoft.com/en-us/project/available-fields-reference) |
| Finish Variance | Difference between a baseline finish and the current finish. | Derived comparison; only calculate when a real baseline and comparison date both exist. [Available fields reference](https://support.microsoft.com/en-us/project/available-fields-reference) |

### History and change visibility

- Planner Premium's task history records recent progress and schedule-impacting changes in a Changes pane inside Task Details. The documented examples include label changes, duration changes, and changes to other tasks that affect the schedule. [Planner Premium task history](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)
- A history/change pane is different from a baseline: history answers “what changed and when,” while a baseline answers “which stored plan should this schedule be compared with.” This distinction is a design inference from Microsoft's separate task-history and baseline features. [Planner Premium task history](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner), [Create or update a baseline or an interim plan](https://support.microsoft.com/en-us/project/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop)

### Deferred MVP2 candidates for this repository

The repository's MVP1 can display a read-only timeline/Gantt, source milestones, typed source references, an explicit asOfDate, and separate planning/execution evidence. The following mature scheduling features are **deferred MVP2 candidates and are not implemented in MVP1**:

1. A stored, identifiable baseline snapshot with baseline metadata and immutable Baseline Start/Finish fields.
2. Baseline-vs-current comparison, Tracking Gantt overlays, finish variance, and a dedicated variance table.
3. Scheduler-calculated critical path, free/total float, slack propagation, and calendar-aware date recalculation.
4. Full editing and calculation for Finish-to-Start, Start-to-Start, Start-to-Finish, and Finish-to-Finish relationships.
5. Configurable project/task/resource calendars and non-working-day scheduling.
6. A Planner-style task history/change pane or an audit trail for schedule-impacting edits.

Milestone markers and the existing asOfDate remain useful MVP1 presentation concepts, but a product-grade milestone workflow and scheduler-grade status-date engine are also outside the MVP1 contract. These boundaries are repository planning decisions informed by the Microsoft features above, not claims about Microsoft's product limitations.

## Oracle Primavera P6 / Primavera Cloud

**Source boundary.** P6 Professional is used below for CPM scheduling, activity relationships, calendars, baselines, milestones, and float. Primavera Cloud is used for its schedule-comparison, baseline, activity-field, and API documentation. The two products share scheduling vocabulary, but the repository should not assume their field names or workflows are interchangeable.

### Timeline / Gantt and detail context

- P6's Gantt Chart displays each activity's duration as a horizontal bar on a configurable timescale, with relationship lines connecting activity bars. Its Activity Table can be used alongside the chart to view schedule data and track planned versus actual start/finish. [The Gantt Chart](https://docs.oracle.com/cd/F51303_01/English/User_Guides/p6_pro_user/the_gantt_chart.htm)
- Primavera Cloud's Activities page provides a Gantt layout synchronized with the Activities table, customizable timescale, activity detail windows, and a visible Data Date. [Primavera Cloud Schedule Management User Guide](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/primavera_schedule_management_user.pdf)
- **Design implication:** the timeline should stay paired with a stable identity/table region and a selected-item detail surface. The Gantt is a schedule explanation; it is not a replacement for activity identity, field provenance, or status evidence. [The Gantt Chart](https://docs.oracle.com/cd/F51303_01/English/User_Guides/p6_pro_user/the_gantt_chart.htm), [Primavera Cloud Schedule Management User Guide](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/primavera_schedule_management_user.pdf)

### Dependency types as schedule logic

- P6 supports Finish-to-Start, Finish-to-Finish, Start-to-Start, and Start-to-Finish relationships. P6 uses relationships together with activity durations to determine schedule dates, and Finish-to-Start is the default relationship type. [Relationships](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/relationships.htm)
- In the P6 Gantt, the endpoint of a relationship line communicates its type: for example, Finish-to-Start starts at the predecessor's right edge and ends at the successor's left edge, while Start-to-Start connects the two left edges. [View activity relationships in the Gantt Chart](https://docs.oracle.com/cd/F25600_01/English/User_Guides/p6_pro_user/view_activity_relationships_in_the_gantt_chart.htm)
- **Repository implication:** preserve dependency type, direction, lag, and the canonical predecessor/successor identity. A generic “blocked” badge loses the scheduling semantics that make the relationship actionable.

### Critical path, float, and milestones are calculated analysis

- P6 uses Critical Path Method scheduling. It performs forward and backward passes to calculate early and late dates, and calculates float using activity duration and calendar definitions. [Scheduling projects](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/scheduling_projects.htm)
- P6 can define critical activities using a Total Float threshold or the Longest Path option. The critical flag is therefore a scheduler result based on configured analysis, not a source-authored fact. [Define critical activities](https://docs.oracle.com/cd/G48902_01/client_help/en_US/define_critical_activities.htm)
- Primavera Cloud's scheduler likewise uses CPM to assign activity dates, calculate total float, and set the project critical path; its API exposes calculated fields such as early finish, remaining float, float-path order, and a critical indicator. [Schedule a project](https://docs.oracle.com/en/industries/construction-engineering/primavera-cloud/rest-api/op-action-scheduleproject-post.html), [View activities by baseline](https://docs.oracle.com/en/industries/construction-engineering/primavera-cloud/rest-api/op-activity-baseline-data-get.html)
- P6 start and finish milestones have no duration, time-based costs, or resource assignments. Primavera Cloud describes milestones as zero-duration activities that can be shown on a Gantt and used in rollups. [Define milestones](https://docs.oracle.com/cd/F25600_01/English/User_Guides/p6_pro_user/define_milestones.htm), [Milestones Overview](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/283884.htm)
- **Repository implication:** a critical flag, float value, or driving path should be labeled calculated and should carry the schedule/as-of context that produced it. A milestone should remain a sparse event marker, not a decorative task color.

### Calendars and the Primavera Data Date / asOfDate analogy

- P6 supports global, project, and resource calendar pools. Calendars can define work hours, holidays, project-specific work/non-work days, and resource vacation; calendar assignments affect activity scheduling, tracking, and resource leveling. [Calendars](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/calendars.htm)
- P6 defines Data Date as the date used as the starting point to calculate the schedule. The project scheduling workflow allows a data date to be set per project before scheduling. [Dates tab - Project Details](https://docs.oracle.com/cd/G48902_01/client_help/en_US/dates_tab_-_project_details.htm), [Schedule a project](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/schedule_a_project.htm)
- Primavera Cloud's scheduling API calls dataDate the progress point or “as-of date” for project activities; status is up to date as of that date, and the value can be set during scheduling or manually. [Schedule a project](https://docs.oracle.com/en/industries/construction-engineering/primavera-cloud/rest-api/op-action-scheduleproject-post.html)
- **Repository analogy:** the repository's asOfDate should be documented as the reporting boundary for derived late/overdue/at-risk conditions, analogous to Primavera's Data Date. It is not an assertion that the MVP1 compiler implements Primavera's scheduler. It must not be confused with an Actual Finish, a Baseline Date, or the wall-clock “today.”

### Baseline as a stored schedule version

- P6 creates a baseline by saving a copy of the current project or converting another project into a baseline. A baseline can be assigned for project summarization and separate baselines can be assigned for comparison. [Create a baseline](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/create_a_baseline.htm), [Assign baselines to projects](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/assign_baselines_to_projects.htm)
- P6's current-vs-baseline view displays current and baseline bars and supports target/variance columns; while the current project is open, baseline data can be viewed but not changed directly. [Comparing Current and Baseline Schedules](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/62723.htm)
- Primavera Cloud describes baselines as capturing a schedule at a point in time and supports baseline fields such as baseline finish, baseline early/late dates, and baseline total float. [Activities Page Tools](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/178414.htm), [Activities Fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/95116.htm)

### Baseline / Actual / Scheduled (Planned) Finish separation

| Field concept | Primavera meaning | Trust treatment for this repository |
| --- | --- | --- |
| Baseline Finish / BL Finish | Finish value stored in a selected baseline version. | Snapshot evidence; require an identifiable baseline before comparing. [Activities Fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/95116.htm) |
| Planned Finish | Date an activity is scheduled to finish if it has not started; calculated by the scheduler and not changed by the scheduler after the activity starts. | Current planned/scheduled projection; keep distinct from actual and baseline. [Activities Fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/95116.htm) |
| Actual Finish | Date the activity was completed. | Execution evidence; do not infer it from a planned date or from elapsed time. [Activities Fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/95116.htm), [View activities by baseline](https://docs.oracle.com/en/industries/construction-engineering/primavera-cloud/rest-api/op-activity-baseline-data-get.html) |
| Finish / Early Finish / Remaining Finish | Current or calculated schedule dates; Early Finish is calculated from network logic, constraints, and resource availability, while Remaining Finish describes the remaining work. | Derived schedule output; expose the field name and calculation context in the inspector. [Activities Fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/95116.htm) |

### History, change visibility, and variance

- P6 baseline comparison can display current and baseline bars, target/variance values, and planned/actual/earned-value columns to identify schedule and cost movement. [Comparing Current and Baseline Schedules](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/62723.htm)
- Primavera Cloud Schedule Comparison can compare the current schedule, a baseline, a scenario, or a specific point in project history. It calculates variances and exposes them in the activity table, detail windows, Gantt, and a Schedule Variance Analysis panel. [Compare Two Versions of a Project Schedule](https://docs.oracle.com/cd/E80480_01/help/en/user/191096.htm)
- Cloud change types identify added, edited, and deleted activities, and the comparison table can expose the two schedule values plus the variance. [Understanding Change Types in the Schedule Comparison Activity Table](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/255383.htm), [Working with the Schedule Comparison Page](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/196651.htm)
- Primavera Cloud baseline variance fields include BL Variance - Finish, defined as the duration difference between the activity Finish date and BL Finish date. [Baseline Variance Fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/290074.htm)
- **Design implication:** a history view and a baseline comparison view answer different questions. The former explains edits between versions; the latter compares a current or revised schedule to a named stored reference. Both should remain read-only analysis surfaces until an explicit merge/update workflow exists. [Compare Two Versions of a Project Schedule](https://docs.oracle.com/cd/E80480_01/help/en/user/191096.htm)

### Deferred MVP2 candidates for this repository

The repository's MVP1 can display a read-only timeline/Gantt, source milestones, typed source references, an explicit asOfDate, and separate planning/execution evidence. The following Primavera-grade capabilities are **deferred MVP2 candidates and are not implemented in MVP1**:

1. A stored baseline project/schedule version with selectable current/original/supplementary comparison roles.
2. Baseline bars, Baseline/Actual/Scheduled-or-Planned Finish fields, start/finish variance, and baseline total-float comparison.
3. CPM scheduling with forward/backward passes, calculated critical path, multiple float paths, free/total/remaining float, and configurable critical thresholds.
4. Calendar-aware scheduling across project, activity, resource, holiday, and non-working-day calendars.
5. Full FS/FF/SS/SF dependency scheduling with lag, constraints, data-date advancement, and rescheduling.
6. Schedule history/change comparison for added, edited, and deleted activities plus a read-only variance-analysis panel.

The MVP1 asOfDate is a transparent reporting boundary only; it is not a Primavera Data Date implementation and does not recalculate a CPM schedule. These are repository scope decisions informed by Oracle's documented capabilities, not claims that P6 or Primavera Cloud behave identically in every deployment.

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
- [Microsoft Planner Premium advanced capabilities](https://support.microsoft.com/en-us/planner/teams/advanced-capabilities-with-premium-plans-in-planner)
- [Microsoft Project Scheduled Finish field](https://support.microsoft.com/en-us/project/scheduled-finish-task-field)
- [Microsoft Project critical path](https://support.microsoft.com/en-us/project/manage-your-project-s-critical-path)
- [Microsoft Project available fields reference](https://support.microsoft.com/en-us/project/available-fields-reference)
- [Microsoft Project baseline and interim plan](https://support.microsoft.com/en-us/project/create-or-update-a-baseline-or-an-interim-plan-in-project-desktop)
- [Microsoft Project Baseline Finish fields](https://support.microsoft.com/en-us/project/baseline-finish-fields)
- [Microsoft Project schedule progress and variance](https://support.microsoft.com/en-us/project/review-the-progress-of-your-schedule)
- [Microsoft Project Status field](https://support.microsoft.com/en-us/project/status-task-field)
- [Oracle P6 Professional Gantt Chart](https://docs.oracle.com/cd/F51303_01/English/User_Guides/p6_pro_user/the_gantt_chart.htm)
- [Oracle P6 relationships](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/relationships.htm)
- [Oracle P6 scheduling projects](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/scheduling_projects.htm)
- [Oracle P6 critical activities](https://docs.oracle.com/cd/G48902_01/client_help/en_US/define_critical_activities.htm)
- [Oracle P6 calendars](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/calendars.htm)
- [Oracle P6 milestones](https://docs.oracle.com/cd/F25600_01/English/User_Guides/p6_pro_user/define_milestones.htm)
- [Oracle P6 project Data Date](https://docs.oracle.com/cd/G48902_01/client_help/en_US/dates_tab_-_project_details.htm)
- [Oracle P6 create and assign baselines](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/create_a_baseline.htm)
- [Oracle P6 current and baseline schedules](https://docs.oracle.com/cd/G48902_01/English/User_Guides/p6_pro_user/62723.htm)
- [Oracle Primavera Cloud schedule management guide](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/primavera_schedule_management_user.pdf)
- [Oracle Primavera Cloud schedule API and Data Date](https://docs.oracle.com/en/industries/construction-engineering/primavera-cloud/rest-api/op-action-scheduleproject-post.html)
- [Oracle Primavera Cloud activity fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/95116.htm)
- [Oracle Primavera Cloud activities by baseline API](https://docs.oracle.com/en/industries/construction-engineering/primavera-cloud/rest-api/op-activity-baseline-data-get.html)
- [Oracle Primavera Cloud baseline variance fields](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/290074.htm)
- [Oracle Primavera Cloud compare schedule versions](https://docs.oracle.com/cd/E80480_01/help/en/user/191096.htm)
- [Oracle Primavera Cloud schedule comparison page](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/196651.htm)
- [Oracle Primavera Cloud change types](https://docs.oracle.com/cd/E80480_01/English/user_guides/schedule_management_user_guide/255383.htm)
