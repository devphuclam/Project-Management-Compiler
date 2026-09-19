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
