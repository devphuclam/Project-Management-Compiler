namespace ProjectManagementCompiler.Tests;

internal static class GanttUiRegressionTests
{
    public static void GanttOffersOptionalMetadataColumns()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("columns: { state: true, plannedEffort: true, recordedPercent: true, owner: false, attention: false }", appJs, "Gantt must start with the approved compact Task / ID, State, Planned hours, and Recorded % columns.");
        TestAssert.Contains("plannedEffortHours", appJs, "Gantt rows must carry authored planned effort into the task list.");
        TestAssert.Contains("gantt-column-planned-effort", appJs, "Gantt must expose planned effort as a layout-aware task column.");
        TestAssert.Contains("Planned h", appJs, "Gantt must label planned effort with a compact hours label.");
        TestAssert.Contains("gantt-hide-planned-effort", appJs, "Gantt must hide planned effort and its cells as one optional column.");
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
        TestAssert.Contains("gantt-task-planned-effort", styles, "Gantt planned-effort cells must have a dedicated compact visual treatment.");
    }

    public static void GanttMetadataColumnsUseCompactMenu()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("createMenu(\"Columns\", \"gantt-columns-menu\")", appJs, "Column choices must live in a compact disclosure menu.");
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
        TestAssert.Contains(" L \" + bend + \" ", appJs, "Gantt dependency connectors must use clear stepped paths.");
        TestAssert.Contains("path.appendChild(title)", appJs, "Gantt dependency connectors must expose endpoint text on hover.");
    }

    public static void GanttDetailClosePreservesSelectionAndUsesFocusReturn()
    {
        var appJs = ReadAppJs();
        var closeActionStart = appJs.IndexOf("} else if (action === \"close-details\") {", StringComparison.Ordinal);
        var closeActionEnd = closeActionStart < 0 ? -1 : appJs.IndexOf("} else if (action === ", closeActionStart + 1, StringComparison.Ordinal);
        var closeAction = closeActionStart >= 0 && closeActionEnd > closeActionStart
            ? appJs[closeActionStart..closeActionEnd]
            : string.Empty;
        var escapeHandlerStart = appJs.IndexOf("shell.addEventListener(\"keydown\", event => {", StringComparison.Ordinal);
        var escapeHandlerEnd = escapeHandlerStart < 0 ? -1 : appJs.IndexOf("\n    });", escapeHandlerStart, StringComparison.Ordinal);
        var escapeHandler = escapeHandlerStart >= 0 && escapeHandlerEnd > escapeHandlerStart
            ? appJs[escapeHandlerStart..escapeHandlerEnd]
            : string.Empty;
        var select = ExtractFunction(appJs, "function selectGanttRow(");
        var close = ExtractFunction(appJs, "function closeGanttDetails(");
        var escape = ExtractFunction(appJs, "function handleGanttEscape(");
        var focus = ExtractFunction(appJs, "function restoreGanttFocusReturn(");

        TestAssert.False(closeAction.Contains("state.gantt.selectedRowKey = null", StringComparison.Ordinal),
            "Current Close action clears the selected canonical identity instead of only closing the drawer.");
        TestAssert.False(escapeHandler.Contains("state.gantt.selectedRowKey = null", StringComparison.Ordinal),
            "Current Escape handler clears the selected canonical identity instead of only closing the drawer.");
        TestAssert.True(select is not null && close is not null && escape is not null && focus is not null,
            "Gantt selection, drawer visibility, Escape, and focus return must have testable behavior seams.");
        TestAssert.Contains("closeGanttDetails()", closeAction, "The visible Close action must run the selection-preserving close behavior.");
        TestAssert.Contains("selectGanttRow(selectTarget.dataset.ganttSelect)", appJs, "A rendered row selection must use the shared selection transition.");
        TestAssert.Contains("handleGanttEscape(event)", appJs, "The Gantt Escape listener must use the shared close transition.");
        TestAssert.Contains("state.gantt.selectedRowKey", select!, "Selecting a row must preserve its canonical identity.");
        TestAssert.Contains("state.gantt.detailsOpen = Boolean", select!, "Selecting a row must open its detail drawer.");
        TestAssert.Contains("state.gantt.detailsOpen = false", close!, "Close must hide the drawer.");
        TestAssert.False(close!.Contains("state.gantt.selectedRowKey = null", StringComparison.Ordinal),
            "Closing the drawer must not clear canonical selection.");
        TestAssert.Contains("renderActiveView()", close!, "The closed drawer state must be rendered before focus is restored.");
        TestAssert.Contains("restoreGanttFocusReturn()", close!, "Close must restore focus after rerendering.");
        TestAssert.True(close!.IndexOf("renderActiveView()", StringComparison.Ordinal) < close.IndexOf("restoreGanttFocusReturn()", StringComparison.Ordinal),
            "Focus must be restored only after the Gantt view has rerendered.");
        TestAssert.Contains("closeGanttDetails()", escape!, "Escape must use the same close and focus-return behavior.");
        TestAssert.False(escape!.Contains("state.gantt.selectedRowKey = null", StringComparison.Ordinal),
            "Escape must not clear canonical selection.");
        foreach (var statePath in new[] { "state.gantt.phaseFilter =", "state.gantt.executionFilter =", "state.gantt.attentionOnly =", "state.gantt.criticalOnly =", "state.gantt.overdueOnly =", "state.gantt.atRiskOnly =", "state.gantt.lateStartOnly =", "state.work." })
            TestAssert.False(close.Contains(statePath, StringComparison.Ordinal), $"Closing details must not reset '{statePath}'.");
        TestAssert.Contains("button.gantt-row-select[data-gantt-select]", focus!, "Focus should return to a rendered Gantt row when one is available.");
        TestAssert.Contains("!candidate.disabled", focus!, "Disabled rows must not be selected as the focus-return target.");
        TestAssert.Contains("gantt-selection-status", focus!, "A hidden selection should return focus to its status explanation.");
        TestAssert.Contains("gantt-view-heading", focus!, "The Gantt heading is the final focus-return fallback.");
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

    public static void GanttModeStripPreservesViewMeaning()
    {
        var appJs = ReadAppJs();

        TestAssert.Contains("state.gantt.preset === \"plan\" && !state.gantt.selectedRowKey", appJs, "Plan mode must only appear active for the actual Plan preset.");
        TestAssert.Contains("action === \"dependency-mode\"", appJs, "Dependency focus must have a non-toggle mode action.");
        TestAssert.Contains("applyGanttPreset(\"plan\")", appJs, "Returning to Plan must reset secondary view state through the preset contract.");
    }

    public static void GanttSelectionNavigatesToWorkWithoutChangingScheduleSemantics()
    {
        var appJs = ReadAppJs();
        var ganttDetailsStart = appJs.IndexOf("function renderGanttDetail(", StringComparison.Ordinal);
        var ganttDetailsEnd = appJs.IndexOf("function resolveRelevantPhase(", ganttDetailsStart, StringComparison.Ordinal);
        var ganttDetails = ganttDetailsStart >= 0 && ganttDetailsEnd > ganttDetailsStart
            ? appJs[ganttDetailsStart..ganttDetailsEnd]
            : string.Empty;

        TestAssert.Contains("detailsOpen", appJs, "Gantt selected identity and drawer visibility must be separate state.");
        TestAssert.Contains("Mở trong Công việc", ganttDetails, "The selected Gantt item must offer reverse navigation to the shared Work inspector.");
        TestAssert.Contains("open-in-work", ganttDetails, "Work navigation must be an explicit reader-operable Gantt drawer action.");
        TestAssert.Contains("prepareGanttToWork", appJs, "Reverse navigation must use the shared typed-identity handoff.");
        TestAssert.Contains("state.work.selectedItemKey", appJs, "Gantt selection must resolve to the canonical Work selection.");
        TestAssert.Contains("state.work.phaseScope", appJs, "Gantt-to-Work navigation must not clear phase scope or other Work criteria.");
        TestAssert.Contains("state.gantt.detailsOpen", appJs, "Gantt drawer visibility must be preserved independently from selection.");
        TestAssert.Contains("buildGanttRows", appJs, "Navigation may focus existing Gantt rows but must not introduce a new schedule calculation.");
    }

    public static void ManifestWorkflowUsesAuthorityAwareExecutionLabels()
    {
        var appJs = ReadAppJs();
        var indexHtml = ReadIndexHtml();

        TestAssert.Contains("Source execution: Loaded", appJs, "Manifest workflow must label recorded execution as source execution.");
        TestAssert.Contains("Source execution: Not recorded", appJs, "Manifest workflow must distinguish an unrecorded source execution state.");
        TestAssert.Contains("Propose execution update", appJs, "Execution editing must describe a local proposal instead of source write-back.");
        TestAssert.Contains("Source forecast", appJs, "SourceExecution.ForecastFinish must be presented as source forecast.");
        TestAssert.Contains("propose execution updates", indexHtml, "The application framing must describe proposal intent instead of source write-back.");
        TestAssert.False(appJs.Contains("Manual execution:", StringComparison.Ordinal), "Manifest workflow must not label source execution as manual execution.");
        TestAssert.False(appJs.Contains("Record execution", StringComparison.Ordinal), "Manifest workflow must not imply that the browser records source execution.");
        TestAssert.False(indexHtml.Contains("record execution", StringComparison.Ordinal), "The application framing must not imply that the browser records source execution.");
    }

    private static string ReadAppJs() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js"));

    private static string ReadStyles() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "styles.css"));

    private static string ReadIndexHtml() => File.ReadAllText(Path.Combine(
        Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "index.html"));

    private static string? ExtractFunction(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return null;
        var end = source.IndexOf("\n  function ", start + signature.Length, StringComparison.Ordinal);
        return end > start ? source[start..end] : source[start..];
    }
}
