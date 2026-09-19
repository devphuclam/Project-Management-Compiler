namespace ProjectManagementCompiler.Tests;

internal static class GanttUiRegressionTests
{
    public static void GanttOffersOptionalMetadataColumns()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("columns: { state: true, owner: false, attention: false }", appJs, "Gantt must start with the approved compact Task / ID and State columns.");
        TestAssert.Contains("data-gantt-column", appJs, "Gantt must expose a column-visibility control seam.");
        TestAssert.Contains("gantt-columns-menu", appJs, "Gantt must render metadata options in a compact Columns menu.");
        TestAssert.Contains("taskIdOption.disabled = true", appJs, "The identity column must remain visible and non-toggleable.");
        TestAssert.Contains("state.gantt.columns[columnTarget.dataset.ganttColumn]", appJs, "Gantt column controls must update visibility state.");
    }

    public static void GanttMetadataColumnLayoutCannotOverflowTaskPane()
    {
        var appJs = ReadAppJs();
        var styles = ReadStyles();

        TestAssert.Contains("--gantt-task-columns", appJs, "Gantt must calculate task-pane columns from visible metadata columns.");
        TestAssert.Contains("--gantt-task-columns", styles, "Gantt task-pane grid must use the calculated column definition.");
        TestAssert.Contains(".gantt-task-header > span", styles, "Gantt header labels must be constrained inside their grid tracks.");
        TestAssert.Contains("text-overflow: ellipsis", styles, "Gantt metadata labels must truncate instead of escaping into the timeline.");
        TestAssert.Contains("gantt-hide-attention", styles, "Gantt must hide the Attention track and cells as one layout unit.");
    }

    public static void GanttMetadataColumnsUseCompactMenu()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("gantt-menu gantt-columns-menu", appJs, "Column choices must live in a compact disclosure menu.");
        TestAssert.Contains("Visible in the task list", appJs, "The Columns menu must explain what its choices control.");
        TestAssert.False(appJs.Contains("toolbar.appendChild(columnOptions)", StringComparison.Ordinal), "Column checkboxes must not consume an always-visible toolbar row.");
    }

    public static void GanttDependencyControlsExplainDirection()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("predecessor → successor", appJs, "Gantt must explain dependency arrow direction in the UI.");
        TestAssert.Contains("Select a task to inspect its direct links", appJs, "Gantt must explain the selection-first dependency interaction.");
        TestAssert.Contains("state.gantt.showDependencies ? \"Hide dependencies\" : \"Show dependencies\"", appJs, "Gantt dependency control must communicate its current action.");
    }

    public static void GanttSelectionOpensFocusedDependencies()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("if (!state.gantt.showDependencies || !selectedRowKey) return svg;", appJs, "Dependency connectors must stay hidden until a row is selected.");
        TestAssert.Contains("state.gantt.showDependencies = true;", appJs, "Selecting a row must immediately reveal its focused direct links.");
        TestAssert.Contains("state.gantt.selectedRowKey = null", appJs, "The details drawer must provide a return to the uncluttered plan view.");
        TestAssert.Contains(" L \" + bend + \" ", appJs, "Gantt dependency connectors must use clear stepped paths.");
        TestAssert.Contains("path.appendChild(title)", appJs, "Gantt dependency connectors must expose endpoint text on hover.");
    }

    public static void GanttDependencyInspectorShowsImpact()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("DEPENDENCY IMPACT", appJs, "Gantt inspector must name the dependency impact section plainly.");
        TestAssert.Contains("Depends on", appJs, "Gantt inspector must show upstream dependencies in plain language.");
        TestAssert.Contains("Blocks", appJs, "Gantt inspector must show downstream impact in plain language.");
        TestAssert.Contains("gantt-impact-flow", appJs, "Gantt inspector must present direct dependency impact as a readable flow.");
        TestAssert.Contains("predecessorKey + \" → \" + subjectKey", appJs, "Gantt inspector must show dependency direction explicitly.");
    }

    public static void GanttDependencyImpactOffersProgressiveDisclosure()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("dependencyFocus: \"both\"", appJs, "Gantt must keep an explicit dependency impact focus mode.");
        TestAssert.Contains("Show downstream impact", appJs, "Transitive successors must be available without crowding the direct-link summary.");
        TestAssert.Contains("gantt-impact-chain", appJs, "The full successor chain must use progressive disclosure.");
    }

    public static void GanttDependencyImpactTraversesTheWholeChain()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("function dependencyImpact", appJs, "Gantt must calculate a dependency impact projection.");
        TestAssert.Contains("while (frontier.length)", appJs, "Dependency impact must traverse beyond only the directly linked row.");
        TestAssert.Contains("gantt-upstream", appJs, "Upstream rows must be visually distinguishable.");
        TestAssert.Contains("gantt-downstream", appJs, "Downstream rows must be visually distinguishable.");
    }

    public static void GanttDependencyInspectorShowsNamedDrivingImpact()
    {
        var appJs = ReadAppJs();
        var styles = ReadStyles();

        TestAssert.Contains("Dependency impact", appJs, "Gantt drawer must explain the scheduling consequence in plain language.");
        TestAssert.Contains("dependencyNodeLabel", appJs, "Dependency impact details must use readable node names instead of only typed keys.");
        TestAssert.Contains("gantt-impact-summary", styles, "Dependency impact summary must have a distinct visual treatment.");
    }

    public static void GanttDependencyConnectorsStayFocusedOnTheSelectedRow()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("const connectorEdges = selectedRowKey", appJs, "Dependency connectors must have a selected-row presentation mode.");
        TestAssert.Contains("subjectKey === selectedRowKey || predecessorKey === selectedRowKey", appJs, "Selected-row connector mode must keep only direct dependency links visible.");
        TestAssert.Contains("directEdgeKeys", appJs, "The dependency projection must distinguish direct links from the transitive impact chain.");
    }

    public static void SelectingGanttRowKeepsTimelineEvidenceVisible()
    {
        var appJs = ReadAppJs();
        var styles = ReadStyles();

        TestAssert.Contains("if (dimmed) taskRow.classList.add(\"gantt-dimmed\")", appJs, "Selection may de-emphasize task metadata without hiding timeline evidence.");
        TestAssert.False(appJs.Contains("[taskRow, timelineRow].forEach(element => {", StringComparison.Ordinal), "Selection must not dim task rows and timeline rows together.");
        TestAssert.Contains(".gantt-timeline-row.gantt-dimmed", styles, "Timeline rows must explicitly neutralize selection dimming if a dimmed class reaches them.");
        TestAssert.Contains("opacity: 1", styles, "Timeline evidence must remain fully visible after row selection.");
    }

    public static void GanttUsesFocusedWorkspaceLayout()
    {
        var appJs = ReadAppJs();
        var styles = ReadStyles();

        TestAssert.Contains("is-gantt-focus", appJs, "Gantt must enter a focused schedule workspace.");
        TestAssert.Contains("gantt-context-bar", appJs, "Focused Gantt mode must retain a compact project context header.");
        TestAssert.Contains(".page.is-gantt-focus #summary-panel", styles, "Focused Gantt mode must remove the dashboard summary from the schedule surface.");
        TestAssert.Contains(".page.is-gantt-focus #execution-panel", styles, "Focused Gantt mode must remove the execution updater from the schedule surface.");
        TestAssert.Contains("gantt-toolbar-tools", styles, "Focused Gantt controls must use a compact tool row.");
        TestAssert.Contains("gantt-detail-drawer", appJs, "Selected row detail must render as the approved contextual drawer.");
        TestAssert.Contains("gantt-detail-drawer", styles, "The contextual drawer must overlay the chart instead of permanently shrinking it.");
    }

    public static void GanttPlanViewUsesCalmTimelineGrid()
    {
        var appJs = ReadAppJs();
        var styles = ReadStyles();

        TestAssert.Contains("zoom: \"week\"", appJs, "The approved planning view must start at a readable weekly scale.");
        TestAssert.Contains("gantt-week-start", appJs, "The timeline must identify weekly boundaries.");
        TestAssert.Contains("gantt-month-start", appJs, "The timeline must identify monthly boundaries.");
        TestAssert.False(appJs.Contains("node(\"span\", \"—\", \"gantt-actual-empty\")", StringComparison.Ordinal), "Plan view must not repeat empty actual-evidence dashes down the chart.");
        TestAssert.Contains(".gantt-zoom-week .gantt-day-grid:not(.gantt-week-start)", styles, "Weekly view must suppress dense daily grid lines.");
        TestAssert.Contains(".gantt-plan-view .gantt-actual-lane", styles, "Plan view must collapse the unused actual lane.");
    }

    private static string ReadAppJs() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js"));

    private static string ReadStyles() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "styles.css"));
}
