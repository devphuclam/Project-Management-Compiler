namespace ProjectManagementCompiler.Tests;

internal static class ManagementUiShellTests
{
    public static void ExistingWorkspaceDestinationsAndActionsAreCharacterized()
    {
        var index = File.ReadAllText(IndexPath());
        var app = File.ReadAllText(AppJsPath());

        TestAssert.Contains("id=\"view-tabs\"", index, "The current workspace must expose its view navigation landmark.");
        foreach (var view in new[] { "dashboard", "wbs", "gantt", "kanban", "dependencies", "cpm", "management-control", "source" })
        {
            TestAssert.Contains($"data-view=\"{view}\"", index, $"The existing {view} destination must remain represented in the workspace.");
        }

        foreach (var action in new[]
        {
            "manifest-import-button",
            "xlsx-preview-import-button",
            "analyze-button",
            "export-executive-button",
            "export-xlsx-button",
            "save-json-button",
            "refresh-button",
            "reopen-button",
            "execution-form"
        })
        {
            TestAssert.Contains($"id=\"{action}\"", index, $"The existing {action} action must not disappear during navigation work.");
        }

        TestAssert.Contains("activateView", app, "The workspace must keep one explicit view-activation behavior.");
        TestAssert.Contains("/api/views", app, "The workspace must continue loading its current compiled view set.");
    }

    public static void DefaultBranchImportHasAnExplicitLocalOnlyActionAndNoStartupRead()
    {
        var index = File.ReadAllText(IndexPath());
        var app = File.ReadAllText(AppJsPath());

        TestAssert.Contains("id=\"manifest-default-branch-import-button\"", index, "The source form must have one deliberate local-default read action.");
        TestAssert.Contains("Đọc phiên bản mới nhất có sẵn trong bản cục bộ", index, "The action must explain that it reads the local copy.");
        TestAssert.Contains("async function importDefaultBranch()", app, "The normal source action must use its dedicated import behavior.");
        TestAssert.Contains("/api/manifest-import/default-branch", app, "The normal source action must call the no-mode/no-SHA API.");
        TestAssert.Contains("manifest-default-branch-import-button", app, "The explicit action must be connected to user input.");

        var restoreCall = app.LastIndexOf("restoreSourcePreferences();", StringComparison.Ordinal);
        TestAssert.True(restoreCall >= 0, "Saved source locations should be restored as preferences.");
        var startupTail = app[restoreCall..];
        TestAssert.False(startupTail.Contains("importDefaultBranch();", StringComparison.Ordinal), "Restoring preferences must never auto-import a source.");
    }

    public static void DefaultBranchSourceFlowStoresOnlyApprovedPreferencesAndStatesLocalFreshness()
    {
        var index = File.ReadAllText(IndexPath());
        var app = File.ReadAllText(AppJsPath());

        TestAssert.Contains("pmc.source.repositoryRoot", app, "The browser may remember the repository folder.");
        TestAssert.Contains("pmc.source.manifestPath", app, "The browser may remember the manifest path.");
        TestAssert.Contains("Không kiểm tra nguồn từ xa", index + app, "The UI must not imply that GitHub or another remote was checked for newer changes.");

        var importStart = app.IndexOf("async function importDefaultBranch()", StringComparison.Ordinal);
        TestAssert.True(importStart >= 0, "The default import function must be present for source-workflow contract checks.");
        var importEnd = app.IndexOf("async function importManifest()", importStart, StringComparison.Ordinal);
        TestAssert.True(importEnd > importStart, "The default and exact-source import flows must remain separate.");
        var defaultImport = app[importStart..importEnd];
        TestAssert.False(defaultImport.Contains("requestedCommit", StringComparison.Ordinal), "The normal source action must not submit a commit chosen by the caller.");
        TestAssert.False(defaultImport.Contains("mode:", StringComparison.Ordinal), "The normal source action must not submit an authority mode.");
    }

    public static void OverviewRendersSourceBackedFactsAndTruthfulProgressCoverage()
    {
        var app = File.ReadAllText(AppJsPath());

        TestAssert.Contains("function renderOverview(overview)", app, "The loaded workspace must render the dedicated Overview projection.");
        TestAssert.Contains("overview.project.name", app, "Overview must show the cleaned project identity from the server projection.");
        TestAssert.Contains("overview.currentPhase", app, "Overview must show only the uniquely dated current phase.");
        TestAssert.Contains("overview.nextControlPoint", app, "Overview must show the next source-backed milestone or decision.");
        TestAssert.Contains("String(point.kind || \"\").toUpperCase() === \"DECISION\"", app, "Overview must consume the API's snake-case enum contract for decision points.");
        TestAssert.Contains("NOT_STARTED: \"Chưa bắt đầu\"", app, "Source-backed control-point state must be rendered from the API's enum casing.");
        TestAssert.Contains("progress.recordedPercent", app, "Overview must consume the server's evidence-qualified effort percentage.");
        TestAssert.Contains("progress.eligibleCardCount", app, "Overview must show effort-coverage counts when project coverage is partial.");
        TestAssert.Contains("progress.completedCardCount", app, "Completed-card count must remain separate from effort percentage.");
        TestAssert.Contains("overview.attentionItems", app, "Overview must display the bounded, mapped attention list.");
        TestAssert.Contains("Chưa có mốc tiếp theo được ghi trong kế hoạch.", app, "Missing future control points need an evidence-based empty state.");
        TestAssert.Contains("Chưa có công việc cần chú ý từ dữ liệu hiện có.", app, "An empty alert list must not be turned into an invented health claim.");
    }

    public static void OfficialImportOpensOverviewAndKeepsDirectGanttAccess()
    {
        var index = File.ReadAllText(IndexPath());
        var app = File.ReadAllText(AppJsPath());

        TestAssert.Contains("data-view=\"overview\"", index, "Overview must be reachable as a primary workspace destination.");
        TestAssert.Contains("data-view=\"gantt\"", index, "Gantt must remain directly reachable beside Overview.");
        var applyStart = app.IndexOf("function applyManifestImport(", StringComparison.Ordinal);
        var applyEnd = app.IndexOf("async function importDefaultBranch(", applyStart, StringComparison.Ordinal);
        TestAssert.True(applyStart >= 0 && applyEnd > applyStart, "The official import response handler must remain a distinct boundary.");
        var applyImport = app[applyStart..applyEnd];
        TestAssert.Contains("classification === \"OFFICIAL_COMMIT\"", applyImport, "Only a successful official snapshot may become the Overview destination.");
        TestAssert.Contains("state.activeView = \"overview\"", applyImport, "A successful official import must open Overview immediately.");
    }

    public static void OverviewIsDispatchedByTheActiveViewRenderer()
    {
        var app = File.ReadAllText(AppJsPath());
        var renderStart = app.IndexOf("function renderActiveView(", StringComparison.Ordinal);
        var renderEnd = app.IndexOf("function ", renderStart + 10, StringComparison.Ordinal);
        TestAssert.True(renderStart >= 0 && renderEnd > renderStart, "The application must have one central active-view renderer.");
        var renderer = app[renderStart..renderEnd];
        TestAssert.Contains("state.views.overview", renderer, "Overview must render from the aggregate compiled view response.");
        TestAssert.Contains("renderOverview", renderer, "Selecting Overview must invoke its dedicated renderer.");
    }

    public static void PrimaryNavigationContainsOnlyOverviewAndGantt()
    {
        var index = File.ReadAllText(IndexPath());
        var navStart = index.IndexOf("<nav id=\"view-tabs\"", StringComparison.Ordinal);
        var navEnd = index.IndexOf("</nav>", navStart, StringComparison.Ordinal);
        TestAssert.True(navStart >= 0 && navEnd > navStart, "The primary view navigation must remain a semantic navigation landmark.");
        var primaryNavigation = index[navStart..navEnd];

        TestAssert.Contains("data-view=\"overview\"", primaryNavigation, "Overview must remain in primary navigation.");
        TestAssert.Contains("data-view=\"gantt\"", primaryNavigation, "Gantt must remain in primary navigation.");
        foreach (var advancedView in new[] { "dashboard", "wbs", "kanban", "dependencies", "cpm", "management-control", "source" })
        {
            TestAssert.False(primaryNavigation.Contains($"data-view=\"{advancedView}\"", StringComparison.Ordinal), $"The specialist {advancedView} view belongs under Advanced.");
        }
        TestAssert.False(index.Contains("data-view=\"work\"", StringComparison.Ordinal), "The navigation must not expose a nonfunctional Work placeholder.");
    }

    public static void AdvancedDisclosureRetainsSpecialistViewsAndTools()
    {
        var index = File.ReadAllText(IndexPath());
        var start = index.IndexOf("<details id=\"advanced-navigation\"", StringComparison.Ordinal);
        var end = index.IndexOf("</details>", start, StringComparison.Ordinal);
        TestAssert.True(start >= 0 && end > start, "Specialist capabilities must have one explicit Advanced disclosure.");
        var advanced = index[start..end];
        foreach (var view in new[] { "dashboard", "wbs", "kanban", "dependencies", "cpm", "management-control", "source" })
        {
            TestAssert.Contains($"data-view=\"{view}\"", advanced, $"Advanced must retain the {view} destination.");
        }
        foreach (var action in new[] { "export-xlsx-button", "refresh-button", "save-json-button", "execution-panel", "execution-form" })
        {
            TestAssert.Contains($"id=\"{action}\"", advanced, $"Advanced must retain the {action} capability.");
        }
        TestAssert.Contains("querySelectorAll(\".tab\")", File.ReadAllText(AppJsPath()), "Primary and Advanced buttons must share the keyboard-operable activation behavior.");
    }

    public static void UnloadedWorkspaceShowsSourceIntakeWithoutProjectTools()
    {
        var index = File.ReadAllText(IndexPath());
        var app = File.ReadAllText(AppJsPath());
        var styles = File.ReadAllText(StylesPath());

        TestAssert.Contains("<body class=\"is-unloaded\">", index, "The page must identify the no-project state before local source is opened.");
        TestAssert.Contains("classList.toggle(\"is-unloaded\"", app, "Workspace visibility must follow loaded-project or workbook-preview state.");
        TestAssert.Contains("body.is-unloaded .views-panel", styles, "Project views must not clutter the unloaded source-intake state.");
        TestAssert.Contains("body.is-unloaded #execution-panel", styles, "Proposal controls must stay out of the no-project state.");
        TestAssert.Contains("<summary", index, "Advanced and source settings must use keyboard-operable native disclosure controls.");
    }

    public static void AdvancedGroupingPreservesExistingGanttControls()
    {
        var app = File.ReadAllText(AppJsPath());
        foreach (var affordance in new[] { "Fit project", "Critical path", "Show dependencies", "Execution evidence", "Needs attention" })
        {
            TestAssert.Contains(affordance, app, $"Grouping views must not remove the existing Gantt affordance '{affordance}'.");
        }
    }

    public static void OverviewAndAdvancedNavigationRemainAccessibleAtNarrowAndWideWidths()
    {
        var styles = File.ReadAllText(StylesPath());

        TestAssert.Contains("button:focus-visible", styles, "Primary and advanced view buttons must show a visible keyboard focus state.");
        TestAssert.Contains("summary:focus-visible", styles, "Native Advanced disclosure controls must show a visible keyboard focus state.");
        TestAssert.Contains(".advanced-view-tabs { display: flex; flex-wrap: wrap", styles, "Advanced destinations must wrap rather than overflow on desktop or narrow screens.");
        TestAssert.Contains("@media (max-width: 700px)", styles, "The Overview must adapt its facts layout at tablet and phone widths.");
        TestAssert.Contains("@media (max-width: 560px)", styles, "The Overview must stack into a readable single column on narrow phones.");
        var ganttRuleStart = styles.IndexOf(".gantt-scroll {", StringComparison.Ordinal);
        var ganttRuleEnd = styles.IndexOf("}", ganttRuleStart, StringComparison.Ordinal);
        TestAssert.True(ganttRuleStart >= 0 && ganttRuleEnd > ganttRuleStart, "The Gantt timeline must keep a contained scrolling surface.");
        TestAssert.Contains("overflow: auto", styles[ganttRuleStart..ganttRuleEnd], "Timeline scrolling must stay inside the Gantt surface rather than widening the whole page.");
        TestAssert.Contains(".gantt-shell { display: grid; grid-template-columns: minmax(0, 1fr);", styles, "The Gantt shell must constrain its implicit grid track so the timeline's intrinsic width cannot widen the page on mobile.");
        TestAssert.Contains(".gantt-context-identity { width: 100%; }", styles, "The stacked mobile Gantt context must stretch its title column instead of sizing to the full unwrapped project name.");
    }

    public static void IsolatedVerificationPortRemainsLoopbackOnlyAndIsSharedWithWebGate()
    {
        var program = File.ReadAllText(ProgramPath());
        var verifyWeb = File.ReadAllText(VerifyWebPath());

        TestAssert.Contains("PMC_LOOPBACK_PORT", program, "An explicitly configured verification port must be supported without changing the default listener.");
        TestAssert.Contains("http://127.0.0.1:{loopbackPort}", program, "A configured port must never change the loopback-only host binding.");
        TestAssert.Contains("binding = loopbackAddress", program, "The health response must report the actual selected loopback endpoint.");
        TestAssert.Contains("PMC_LOOPBACK_PORT", verifyWeb, "The web verification process must inherit the isolated port selection.");
        TestAssert.Contains("$baseUrl", verifyWeb, "Every verification request must target its own selected app instance.");
    }

    private static string IndexPath() => Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "index.html");

    private static string AppJsPath() => Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js");

    private static string StylesPath() => Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "styles.css");

    private static string ProgramPath() => Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "Program.cs");

    private static string VerifyWebPath() => Path.Combine(Directory.GetCurrentDirectory(), "scripts", "verify-web.ps1");
}
