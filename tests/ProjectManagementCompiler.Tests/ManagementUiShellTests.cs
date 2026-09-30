using System.Text.RegularExpressions;

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

    public static void PrimaryNavigationContainsOnlyOverviewWorkAndGantt()
    {
        var index = File.ReadAllText(IndexPath());
        var navStart = index.IndexOf("<nav id=\"view-tabs\"", StringComparison.Ordinal);
        var navEnd = index.IndexOf("</nav>", navStart, StringComparison.Ordinal);
        TestAssert.True(navStart >= 0 && navEnd > navStart, "The primary view navigation must remain a semantic navigation landmark.");
        var primaryNavigation = index[navStart..navEnd];

        var destinations = Regex.Matches(primaryNavigation, "<button\\b[^>]*data-view=\"([^\"]+)\"[^>]*>([^<]*)</button>", RegexOptions.Singleline)
            .Cast<Match>()
            .Select(match => match.Groups[1].Value + ":" + match.Groups[2].Value.Trim());
        TestAssert.Equal("overview:Tổng quan|work:Công việc|gantt:Gantt", string.Join('|', destinations), "Loaded primary navigation must contain exactly the three approved destinations in order.");
        foreach (var advancedView in new[] { "dashboard", "wbs", "kanban", "dependencies", "cpm", "management-control", "source" })
        {
            TestAssert.False(primaryNavigation.Contains($"data-view=\"{advancedView}\"", StringComparison.Ordinal), $"The specialist {advancedView} view belongs under Advanced.");
        }
    }

    public static void WorkEntryDefaultsToAllPhaseListWithIndependentCurrentPhaseFocus()
    {
        var app = File.ReadAllText(AppJsPath());
        var index = File.ReadAllText(IndexPath());

        TestAssert.True(app.Contains("work: createWorkState()", StringComparison.Ordinal), "Work interaction state must live in the existing transient browser state object.");
        TestAssert.True(app.Contains("mode: \"list\"", StringComparison.Ordinal), "The first Work mode must be List.");
        TestAssert.True(app.Contains("phaseScope: { kind: \"all\", phaseId: null }", StringComparison.Ordinal), "Work must start with an explicit All phases scope.");
        TestAssert.True(app.Contains("focusedPhaseId", StringComparison.Ordinal), "Current-phase focus must have a state field separate from phase scope.");
        TestAssert.True(app.Contains("initializeWorkFocus", StringComparison.Ordinal), "Work must initialize focus from the projected current phase rather than choosing one in the browser.");
        TestAssert.True(app.Contains("setWorkPhaseScope", StringComparison.Ordinal), "Explicit phase selection must update the shared Work scope rather than mode-specific state.");
        TestAssert.True(app.Contains("state.work.phaseScope", StringComparison.Ordinal), "Explicit phase scope must be owned by shared Work view state.");
        TestAssert.True(app.Contains("renderWorkEntry(activeWorkProjection())", StringComparison.Ordinal), "The Work destination must consume the retained official Work projection through its shared selector.");
        TestAssert.Contains("data-view=\"work\"", index, "Công việc must be a real destination rather than a dead or absent placeholder.");
        TestAssert.False(app.Contains("pmc.work.", StringComparison.Ordinal), "Work scope/focus state must not be persisted to localStorage.");

        var focusStart = app.IndexOf("function initializeWorkFocus(", StringComparison.Ordinal);
        var focusEnd = app.IndexOf("\n  function ", focusStart + 10, StringComparison.Ordinal);
        TestAssert.True(focusStart >= 0 && focusEnd > focusStart, "Current-phase focus must have an explicit projection-backed initializer.");
        var focusInitializer = app[focusStart..focusEnd];
        TestAssert.True(focusInitializer.Contains("work.currentPhaseId", StringComparison.Ordinal), "Current-phase focus must come from the Work projection.");
        TestAssert.True(focusInitializer.Contains("matches.length !== 1", StringComparison.Ordinal), "Unknown or ambiguous current phases must produce no focus.");
        TestAssert.True(focusInitializer.Contains("state.work.focusedPhaseId", StringComparison.Ordinal), "The uniquely resolved current phase must be stored as focus.");
        TestAssert.False(focusInitializer.Contains("phaseScope", StringComparison.Ordinal), "Current-phase focus must not narrow the All phases scope.");

        var scopeStart = app.IndexOf("function setWorkPhaseScope(", StringComparison.Ordinal);
        var scopeEnd = app.IndexOf("\n  function ", scopeStart + 10, StringComparison.Ordinal);
        TestAssert.True(scopeStart >= 0 && scopeEnd > scopeStart, "Explicit phase selection must have one shared Work scope setter.");
        var scopeSetter = app[scopeStart..scopeEnd];
        TestAssert.True(scopeSetter.Contains("state.work.phaseScope", StringComparison.Ordinal), "Explicit phase selection must update state shared by Work modes.");
        TestAssert.True(scopeSetter.Contains("kind: \"phase\"", StringComparison.Ordinal), "A valid explicit selection must narrow Work to its selected phase.");
    }

    public static void WorkUsesOnlyOfficialProjectionCardsAndKeepsControlPointsOutOfTheCollection()
    {
        var app = File.ReadAllText(AppJsPath());
        var workProjectionStart = app.IndexOf("function activeWorkProjection(", StringComparison.Ordinal);
        var workProjectionEnd = app.IndexOf("\n  function ", workProjectionStart + 10, StringComparison.Ordinal);
        TestAssert.True(workProjectionStart >= 0 && workProjectionEnd > workProjectionStart, "Work must have a discoverable projection-selection boundary.");
        var workProjection = app[workProjectionStart..workProjectionEnd];

        TestAssert.True(workProjection.Contains("state.officialWorkProjection", StringComparison.Ordinal), "Work must resolve the separately retained last official Work projection.");
        TestAssert.False(workProjection.Contains("state.activeViewsClassification", StringComparison.Ordinal), "Candidate classification of other active views must not hide official Work.");
        TestAssert.False(workProjection.Contains("state.views.work", StringComparison.Ordinal), "Work must not resolve from candidate or uncommitted preview views.");
        TestAssert.False(workProjection.Contains("state.views.gantt", StringComparison.Ordinal), "Gantt control points must not be added to the Work card collection.");
        TestAssert.False(workProjection.Contains("milestones", StringComparison.Ordinal), "Milestones and decision points remain outside Work items.");
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

    public static void WorkParityGateKeepsLegacyWbsAndKanbanUnderAdvanced()
    {
        var index = File.ReadAllText(IndexPath());
        var app = File.ReadAllText(AppJsPath());
        var navStart = index.IndexOf("<nav id=\"view-tabs\"", StringComparison.Ordinal);
        var navEnd = index.IndexOf("</nav>", navStart, StringComparison.Ordinal);
        var advancedStart = index.IndexOf("<details id=\"advanced-navigation\"", StringComparison.Ordinal);
        var advancedEnd = index.IndexOf("</details>", advancedStart, StringComparison.Ordinal);
        TestAssert.True(navStart >= 0 && navEnd > navStart && advancedStart >= 0 && advancedEnd > advancedStart,
            "Parity-gated specialist routes require the approved primary and Advanced navigation regions.");
        var primary = index[navStart..navEnd];
        var advanced = index[advancedStart..advancedEnd];
        TestAssert.False(primary.Contains("data-view=\"kanban\"", StringComparison.Ordinal),
            "Unified Work must remain the only normal Kanban destination.");
        TestAssert.False(primary.Contains("data-view=\"wbs\"", StringComparison.Ordinal),
            "Legacy WBS must not return to primary navigation during the parity gate.");
        TestAssert.Contains("data-view=\"kanban\"", advanced,
            "Legacy Kanban must remain reachable under Advanced until its parity evidence passes.");
        TestAssert.Contains("data-view=\"wbs\"", advanced,
            "Legacy WBS must not be retired until hierarchy parity evidence passes.");
        TestAssert.Contains("renderWbs(state.views.wbs)", app,
            "The legacy WBS route must continue to render from its existing projection.");
        TestAssert.Contains("renderKanban(state.views.kanban)", app,
            "The legacy Kanban route must continue to render from its existing projection.");
        TestAssert.Contains("renderWorkKanban(work)", app,
            "Unified Kanban must remain available inside the normal Work destination.");
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

    public static void WorkLayoutKeeps360And1280PixelControlsInsideThePage()
    {
        var styles = File.ReadAllText(StylesPath());
        var app = File.ReadAllText(AppJsPath());

        TestAssert.Contains(".work-workspace {", styles, "Work must use a constrained responsive workspace rather than widen the page.");
        TestAssert.Contains("min-width: 0", styles, "Work grid children must be allowed to shrink within a 360px viewport.");
        TestAssert.Contains("@media (max-width: 760px)", styles, "Work needs a narrow layout rule that applies at 360px.");
        TestAssert.Contains("work-kanban-group-selector", app, "At narrow width, users must reach all Kanban groups without horizontal scrolling.");
        TestAssert.Contains("work-kanban-count-strip", app, "All narrow Kanban group counts must stay visible without page-level clipping.");
        TestAssert.Contains("work-inspector[data-inspector-open=\"true\"]", styles, "The narrow open inspector must be styled as a full-screen surface.");
        TestAssert.Contains("inset: 0", styles, "The narrow inspector must occupy the viewport rather than a clipped workspace column.");
        TestAssert.Contains("100dvh", styles, "The narrow inspector must size to the visible viewport on mobile browsers.");
        TestAssert.Contains(".work-list-row", styles, "List rows must retain a readable single-column layout at 360px.");
        TestAssert.False(styles.Contains("html, body { overflow-x: hidden", StringComparison.Ordinal),
            "Responsive Work must not hide page overflow globally; fix the Work layout and preserve contained Gantt scrolling.");
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
