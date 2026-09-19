namespace ProjectManagementCompiler.Tests;

internal static class GanttUiRegressionTests
{
    public static void GanttOffersOptionalMetadataColumns()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("columns: { state: true, owner: true, attention: true }", appJs, "Gantt must keep metadata columns visible by default while allowing them to be hidden.");
        TestAssert.Contains("data-gantt-column", appJs, "Gantt must expose a column-visibility control seam.");
        TestAssert.Contains("gantt-column-options", appJs, "Gantt must render a dedicated metadata-column options group.");
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

    public static void GanttMetadataColumnOptionsAreAlwaysVisible()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("toolbar.appendChild(columnOptions)", appJs, "Gantt column options must be rendered in the visible toolbar.");
        TestAssert.False(appJs.Contains("filters.appendChild(columnOptions)", StringComparison.Ordinal), "Gantt column options must not be hidden inside Advanced filters.");
    }

    public static void GanttDependencyControlsExplainDirection()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("predecessor → successor", appJs, "Gantt must explain dependency arrow direction in the UI.");
        TestAssert.Contains("Dependency lines:", appJs, "Gantt must explain what dependency connectors represent.");
        TestAssert.Contains("state.gantt.showDependencies ? \"Hide dependencies\" : \"Show dependencies\"", appJs, "Gantt dependency control must communicate its current action.");
    }

    public static void GanttDependencyConnectorsOnlyRenderWhenEnabled()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("if (!state.gantt.showDependencies) return svg;", appJs, "Gantt connectors must be explicitly opt-in.");
        TestAssert.False(appJs.Contains("if (!state.gantt.showDependencies && !isRelated) return;", StringComparison.Ordinal), "Selecting a row must not reveal dependency lines while the dependency view is off.");
        TestAssert.Contains(" L \" + bend + \" ", appJs, "Gantt dependency connectors must use clear stepped paths.");
        TestAssert.Contains("path.appendChild(title)", appJs, "Gantt dependency connectors must expose endpoint text on hover.");
    }

    public static void GanttDependencyInspectorShowsImpact()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("DEPENDENCY IMPACT", appJs, "Gantt inspector must name the dependency impact section plainly.");
        TestAssert.Contains("Depends on", appJs, "Gantt inspector must show upstream dependencies in plain language.");
        TestAssert.Contains("Affects", appJs, "Gantt inspector must show downstream impact in plain language.");
        TestAssert.Contains("predecessorKey + \" → \" + subjectKey", appJs, "Gantt inspector must show dependency direction explicitly.");
    }

    public static void GanttDependencyImpactOffersDirectionalFocus()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("dependencyFocus: \"both\"", appJs, "Gantt must keep an explicit dependency impact focus mode.");
        TestAssert.Contains("Depends on", appJs, "Gantt must offer an upstream dependency focus.");
        TestAssert.Contains("Affects", appJs, "Gantt must offer a downstream dependency focus.");
        TestAssert.Contains("Both", appJs, "Gantt must offer a combined dependency impact focus.");
        TestAssert.Contains("data-gantt-focus", appJs, "Directional impact controls must expose a stable browser interaction seam.");
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

        TestAssert.Contains("Driving impact", appJs, "Gantt inspector must explain the downstream scheduling consequence.");
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

    private static string ReadAppJs() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js"));

    private static string ReadStyles() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "styles.css"));
}
