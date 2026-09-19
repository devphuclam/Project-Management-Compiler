# Gantt visual proposal

Status: approved and implemented in the production Gantt, 2026-09-19. Open `gantt-visual-proposal.html` to review the original visual direction, or run the application to exercise the source-backed implementation. Click `P04` to compare the default schedule with a selected task and its direct dependency links.

## Why this composition

The current Gantt at a 1280-pixel viewport reserves about 310 pixels for an empty inspector. Four task columns compress Vietnamese task names, and every task receives an empty actual lane. The day grid, row rules, and SVG links compete visually. The prototype gives the timeline its width back, opens task detail on demand, keeps only `Task / ID` and `State` by default, and draws the two direct links relevant to the selected task.

The controlled public fixture provides the prototype names, dates, and links: `P03 → P04 → P06 → P07` in PH0. The diagram illustrates direct links `P03 → P04` and `P04 → P06`; its optional downstream disclosure continues to `P07`. These are planning dependencies, not a claim of recorded execution or a forecast.

## Reference patterns

- [ClickUp Gantt view](https://help.clickup.com/hc/en-us/articles/6310249474967-Create-and-share-a-Gantt-view) separates a task sidebar from the timeline and lets people add sidebar columns. We use a compact `Columns` menu and a stable task pane.
- [Jira Plans dependency display](https://support.atlassian.com/jira-software-cloud/docs/change-how-advanced-roadmaps-displays-dependencies/) offers lines or badges and reserves red lines for conflicting dates. We use a quiet default and colored direct paths only after task selection; the prototype does not show a conflict.
- [Microsoft Project Task Path](https://support.microsoft.com/en-us/project/highlight-how-tasks-link-to-other-tasks) focuses a selected task's predecessors and successors. We put that relationship next to the selected task and in a short inspector flow.

These are interaction references. The prototype is an original composition in the Compiler's visual language, not a copy of another product's assets or source.

## Production acceptance — verified

1. A 1280-pixel viewport shows the project header, toolbar, PH0 task names, dates, and P04 bar without a permanently open inspector.
2. Selecting P04 opens a narrow, dismissible drawer. `P03 → P04 → P06` is readable in text, with matching direct links on the chart. The full downstream chain stays a separate disclosure.
3. Week boundaries and weekends are visible; daily vertical grid lines and repeated no-evidence dashes are absent from the default plan view.
4. Optional metadata columns can be shown or hidden without clipping the header. Keyboard focus remains visible.
5. PLAN, ACTUAL, ALERT, provenance, and CPM retain their existing semantic boundaries when the visual design is implemented in the application.

The HTML remains a standalone design artifact with public fixture data. The source-backed implementation now lives in `src/ProjectManagementCompiler/wwwroot/app.js` and `styles.css`, with regression coverage in `GanttUiRegressionTests.cs` and the local web verification gate.
