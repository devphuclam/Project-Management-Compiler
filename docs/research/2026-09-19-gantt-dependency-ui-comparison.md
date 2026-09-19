# Gantt dependency UI comparison

## Purpose

This note compares the dependency presentation patterns documented by ClickUp,
Jira Plans, and Microsoft Project, then translates the useful parts into a
bounded recommendation for Project Management Compiler.

The comparison uses current first-party help documentation. Product editions,
permissions, and rollout settings can change the exact interaction.

## What the products make explicit

### ClickUp

ClickUp represents a dependency as an arrow connecting two task bars. The
arrow starts at the blocking task and ends at the blocked task, so direction is
visible without opening a detail view. ClickUp also exposes connector handles
when a user hovers a bar and can reschedule downstream dependent tasks when
`Reschedule dependencies` is enabled.

Sources:

- [Create Dependency Relationships in views](https://help.clickup.com/hc/en-us/articles/12305064244247-Create-Dependency-Relationships-in-views)
- [How to use Gantt charts for project planning](https://help.clickup.com/hc/en-us/articles/40982447293207-How-to-use-gantt-charts-for-project-planning)
- [Rescheduling dependencies](https://help.clickup.com/hc/en-us/articles/6304547785367-Rescheduling-dependencies)

### Jira Plans / Advanced Roadmaps

Jira supports dependency display as either lines between bars or badges at
bar ends. A dependency can be inspected directly from its line or icon. The
details include direction, work item, status, assignee, and lead time; a
dependency filter can hide unrelated work and keep the dependent chain in
focus. Jira also uses red dependency links to signal overlap or an off-track
relationship in the documented timeline experience.

Sources:

- [View and manage dependencies in your plans](https://support.atlassian.com/jira-software-cloud/docs/view-and-manage-dependencies-in-advanced-roadmaps/)
- [View all of a work item's dependencies on your plan](https://support.atlassian.com/jira-software-cloud/docs/view-all-of-an-issues-dependencies-on-your-timeline/)
- [What are dependencies on the timeline?](https://support.atlassian.com/jira-software-cloud/docs/manage-dependencies-between-epics-on-the-timeline/)
- [Change how Advanced Roadmaps displays dependencies](https://support.atlassian.com/jira-software-cloud/docs/change-how-advanced-roadmaps-displays-dependencies/)

### Microsoft Project

Microsoft Project uses formal predecessor/successor terminology. A normal
link is `Finish-to-Start`: the predecessor finishes before the successor can
start. The Gantt view draws link lines, but the lines can be hidden without
removing the dependency. The more important interaction is Task Path, which
can highlight predecessors, driving predecessors, successors, and driven
successors for the selected task.

Sources:

- [Link tasks in a project](https://support.microsoft.com/en-US/project/link-tasks-in-a-project)
- [Change a task link](https://support.microsoft.com/en-us/project/change-a-task-link)
- [Successors task field](https://support.microsoft.com/en-us/project/successors-task-field)
- [Highlight how tasks link to other tasks](https://support.microsoft.com/en-us/project/highlight-how-tasks-link-to-other-tasks)

## Comparative takeaway

| Need | Best reference pattern | Why it matters here |
| --- | --- | --- |
| Understand arrow direction | ClickUp / Microsoft Project | Make `predecessor → successor` visible and use a real arrowhead. |
| Inspect one relationship | Jira / Microsoft Project | Selecting a line or task should reveal dependency type and endpoint IDs. |
| Understand downstream impact | Microsoft Project | Separate all successors from the successors directly driven by the selected task. |
| Reduce visual noise | Jira / Microsoft Project | Allow lines to be hidden and provide a focused dependency-chain mode. |
| Communicate schedule risk | Jira | Use red only for a derived conflict/off-track condition, not for every connector. |

## Recommendation for Project Management Compiler

The current implementation should converge on this interaction model:

1. Keep dependency lines hidden by default. `Show dependencies` should be an
   explicit view control and should change to `Hide dependencies` when active.
2. When visible, use stepped connectors with arrowheads and a hover label such
   as `P04 → P06 · FINISH_TO_START`.
3. Selecting a row must show a plain-language `DEPENDENCY IMPACT` section:
   `Depends on`, `Affects`, and the typed relationship for each edge.
4. Add a focused chain mode later, following the Microsoft Project/Jira idea:
   `Predecessors`, `Successors`, `Driving`, and `Driven`, with unrelated rows
   dimmed or hidden. This should be a view/filter operation, not a mutation of
   the immutable baseline.
5. Reserve red for a derived schedule conflict or off-track dependency. A
   normal dependency should use a neutral connector; selected incoming and
   outgoing edges may use distinct accessible accent colors.

The key design rule is: a connector is only a visual aid. The authoritative
meaning must remain readable as structured endpoint IDs, direction, dependency
type, and derived impact in the inspector.
