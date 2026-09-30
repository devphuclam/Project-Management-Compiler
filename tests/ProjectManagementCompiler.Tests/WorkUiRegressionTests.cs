namespace ProjectManagementCompiler.Tests;

internal static class WorkUiRegressionTests
{
    public static void WorkListUsesCanonicalHierarchyAndOneTypedDeliveryCardIdentity()
    {
        var app = File.ReadAllText(AppJsPath());
        var list = ExtractFunction(app, "function renderWorkList(");
        var hierarchy = ExtractFunction(app, "function buildWorkListHierarchy(");
        var scopedCards = ExtractFunction(app, "function workCardScopeEntries(");
        TestAssert.True(list is not null && hierarchy is not null && scopedCards is not null,
            "Missing approved Feature 009 behavior: the canonical Work List hierarchy and renderer do not exist.");
        var renderer = list! + "\n" + hierarchy! + "\n" + scopedCards!;

        Require(renderer, "work.phases", "List phase groups must come from the Work projection.");
        Require(renderer, "work.workPackages", "List Work Packages must come from the Work projection.");
        Require(renderer, "work.cards", "Delivery Card payloads must come from the Work projection's single card collection.");
        Require(renderer, "workPackageIds", "List phase ancestry must follow the projection's Work Package references.");
        Require(renderer, "deliveryCardIds", "List package ancestry and order must follow the projection's Delivery Card references.");
        Require(renderer, "key.kind", "Rendered card identity must preserve its canonical kind.");
        Require(renderer, "key.id", "Rendered card identity must preserve its canonical stable ID.");
        Require(renderer, "renderedCardKeys.has(identity)", "A canonical Delivery Card identity must not be rendered twice through hierarchy references.");
        Require(renderer, "renderedCardKeys.add(identity)", "The canonical typed identity must be recorded after its single List row is rendered.");
        Require(renderer, "cards.appendChild(renderWorkCard(card))", "Each eligible canonical Delivery Card reference must create exactly one card row.");
        Require(renderer, "card.key.kind !== \"DeliveryCard\"", "Only canonical Delivery Cards may become Work rows.");
        Require(renderer, "card.phaseId !== phase.id || card.workPackageId !== workPackage.id", "A card must remain under its authoritative Phase and Work Package.");
        TestAssert.False(renderer.Contains(".sort(", StringComparison.Ordinal), "List traversal must retain the canonical order supplied by hierarchy references.");
        TestAssert.False(renderer.Contains("state.views.wbs", StringComparison.Ordinal), "The browser must not independently reconstruct Work from WBS.");
        TestAssert.False(renderer.Contains("state.views.gantt", StringComparison.Ordinal), "Gantt control points must not be added to the Work List.");
        TestAssert.False(renderer.Contains("milestones", StringComparison.OrdinalIgnoreCase), "Milestones and decision points are not Work List items.");
    }

    public static void WorkListLeadsWithReaderCopyAndKeepsTechnicalEvidenceOutOfDefaultRows()
    {
        var app = File.ReadAllText(AppJsPath());
        var list = ExtractFunction(app, "function renderWorkList(");
        var card = ExtractFunction(app, "function renderWorkCard(");
        TestAssert.True(list is not null && card is not null, "Missing approved Feature 009 behavior: the primary Work List/card renderers do not exist.");
        var readerCopy = list! + "\n" + card!;

        Require(readerCopy, "Đầu mối / vai trò", "The reader-facing assignment column must use the approved wording.");
        Require(readerCopy, "Kết thúc kế hoạch", "Baseline planned finish must not be relabeled as a deadline.");
        Require(readerCopy, "plannedFinish", "A known baseline planned finish must remain available in the List.");
        Require(readerCopy, "execution", "The List may show only the canonical effective execution state.");
        Require(readerCopy, "roles", "The List must show source-backed roles without inventing an owner.");

        var nameIndex = card!.IndexOf("identity.appendChild(node(\"strong\", card.name", StringComparison.Ordinal);
        var idIndex = card.IndexOf("identity.appendChild(stableId)", StringComparison.Ordinal);
        TestAssert.True(nameIndex >= 0 && idIndex > nameIndex, "The reader-facing Delivery Card name must precede its technical stable ID.");
        foreach (var technicalField in new[] { "plannedEffortHours", "actualEffortHours", "remainingEffortHours", "sourceReferences", "sourceSha", "diagnostics", "state.views.cpm" })
        {
            TestAssert.False(readerCopy.Contains(technicalField, StringComparison.OrdinalIgnoreCase), $"'{technicalField}' must not dominate the default Work rows.");
        }
    }

    public static void UnifiedWorkKanbanGroupsScopedCanonicalCardsDeterministically()
    {
        var app = File.ReadAllText(AppJsPath());
        var entry = ExtractFunction(app, "function renderWorkEntry(");
        var list = ExtractFunction(app, "function renderWorkList(");
        var hierarchy = ExtractFunction(app, "function buildWorkListHierarchy(");
        var scopedCards = ExtractFunction(app, "function workCardScopeEntries(");
        var filteredCards = ExtractFunction(app, "function filterWorkEntries(");
        var kanban = ExtractFunction(app, "function renderWorkKanban(");
        var groups = ExtractFunction(app, "function buildWorkKanbanGroups(");
        var ordering = ExtractFunction(app, "function sortWorkKanbanEntries(");

        TestAssert.True(kanban is not null, "Missing Feature 009 behavior: the primary Work destination has no Unified Kanban renderer.");
        TestAssert.True(entry is not null && list is not null && hierarchy is not null && scopedCards is not null && filteredCards is not null && groups is not null && ordering is not null,
            "Unified Kanban must share explicit Work mode, card collection, grouping, and deterministic ordering seams.");

        Require(entry!, "state.work.mode", "List/Kanban mode must be transient shared Work view state.");
        Require(entry!, "renderWorkList(work)", "List must remain available in the primary Work destination.");
        Require(entry!, "renderWorkKanban(work)", "Kanban must be selectable inside Work without replacing the legacy Advanced route.");
        Require(entry!, "Danh sách", "The native Work mode control must expose List by its reader-facing name.");
        Require(entry!, "Kanban", "The native Work mode control must expose Kanban.");

        var collectionContract = list! + "\n" + hierarchy! + "\n" + scopedCards! + "\n" + filteredCards! + "\n" + kanban!;
        Require(list!, "buildWorkListHierarchy(work)", "List must consume the shared filtered canonical hierarchy projection.");
        Require(hierarchy!, "filterWorkEntries(work)", "List hierarchy must consume the shared criteria-filtered canonical Delivery Card collection.");
        Require(kanban!, "filterWorkEntries(work)", "Kanban must use the same filtered Work card collection as List.");
        Require(filteredCards!, "workCardScopeEntries(work)", "The shared filter pipeline must apply explicit phase scope before other criteria.");
        Require(scopedCards!, "work.cards", "The canonical Work projection is the only Delivery Card payload collection.");
        Require(scopedCards!, "card.key.kind !== \"DeliveryCard\"", "Phase, Work Package, milestone, and decision-point identities must not become Kanban cards.");
        Require(scopedCards!, "key.kind + \":\" + card.key.id", "Card identity must remain the canonical kind plus stable ID.");
        Require(scopedCards!, "key.id", "Card membership must preserve the stable canonical card ID.");
        Require(scopedCards!, "workPackageIds", "Canonical phase-to-package references define hierarchy membership.");
        Require(scopedCards!, "deliveryCardIds", "Canonical package-to-card references define hierarchy membership and order.");
        Require(scopedCards!, "phaseId", "A card must remain attached to its canonical phase.");
        Require(scopedCards!, "workPackageId", "A card must remain attached to its canonical Work Package.");
        Require(scopedCards!, "state.work.phaseScope", "Explicit phase scope must be shared without using current-phase focus as a filter.");
        Require(scopedCards!, "seenIdentities.has(identity)", "A canonical card referenced more than once must not appear twice in the shared collection.");
        Require(scopedCards!, "seenIdentities.add(identity)", "The single typed card identity must be recorded before returning the shared collection.");
        TestAssert.False(scopedCards!.Contains("focusedPhaseId", StringComparison.Ordinal), "Current-phase focus must not narrow All phases membership.");
        TestAssert.False(collectionContract.Contains("state.views.kanban", StringComparison.Ordinal), "Unified Kanban must not consume the legacy Kanban projection.");
        TestAssert.False(collectionContract.Contains("wipLimit", StringComparison.OrdinalIgnoreCase), "Unified Kanban must not inherit legacy WIP semantics.");

        Require(kanban!, "sortWorkKanbanEntries", "Kanban cards must use the approved deterministic presentation order.");
        Require(kanban!, "buildWorkKanbanGroups", "Kanban groups must be computed from the shared in-scope Work card collection.");
        Require(kanban!, "renderWorkKanbanGroup", "Every state group, including zero-count groups, must have a renderer.");
        Require(groups!, "NOT_STARTED", "Explicit NOT_STARTED cards must map to the Chưa bắt đầu group.");
        Require(groups!, "IN_PROGRESS", "Explicit IN_PROGRESS cards must map to the Đang làm group.");
        Require(groups!, "COMPLETED", "Explicit COMPLETED cards must map to the Hoàn thành group.");
        Require(groups!, "SUSPENDED", "SUSPENDED must remain a distinct authored-state group.");
        Require(groups!, "CANCELLED", "CANCELLED must remain a distinct authored-state group.");
        Require(groups!, "Chưa ghi nhận", "Null/unrecorded state must have a separate reader-facing group.");
        Require(groups!, "executionState: null", "Unrecorded state must be represented as null, never defaulted to NOT_STARTED.");
        Require(groups!, "groups.find", "A Delivery Card must be assigned to at most one matching state group.");
        Require(groups!, "group.cards.push", "Each eligible Delivery Card must be placed in only its one authored-state group.");
        Require(groups!, "group.count = group.cards.length", "Each count must be calculated from cards remaining after active scope.");
        Require(ordering!, "currentPhaseId", "The known current phase must be ordered first without changing card membership.");
        Require(ordering!, "phaseOrder", "Remaining phases must follow canonical hierarchy order.");
        Require(ordering!, "workPackageOrder", "Work Packages must follow canonical hierarchy order.");
        Require(ordering!, "cardOrder", "Cards must follow canonical/source order within their Work Package.");
        Require(ordering!, "compareOrdinalWorkIds", "Tied or unavailable canonical positions must use ordinal stable-ID ordering.");
        TestAssert.False(ordering!.Contains("localeCompare", StringComparison.Ordinal), "Kanban order must not depend on locale-sensitive sorting.");
    }

    public static void WorkAttentionUsesProjectedConsequencesAndTruthfulEmptyCopy()
    {
        var app = File.ReadAllText(AppJsPath());
        var entry = ExtractFunction(app, "function renderWorkEntry(");
        var renderer = ExtractFunction(app, "function renderWorkAttention(");
        var summary = ExtractFunction(app, "function renderWorkAttentionSummary(");
        var listCard = ExtractFunction(app, "function renderWorkCard(");
        var kanbanCard = ExtractFunction(app, "function renderWorkKanbanCard(");
        var groups = ExtractFunction(app, "function buildWorkKanbanGroups(");

        TestAssert.True(renderer is not null, "Missing Feature 009 behavior: projected Work attention is not rendered on Work cards.");
        TestAssert.True(entry is not null && summary is not null && listCard is not null && kanbanCard is not null && groups is not null,
            "Work attention requires one projected summary and consistent List/Kanban card presentation.");

        Require(renderer!, "card.attention", "Work attention must come only from the already projected WorkCard attention array.");
        Require(renderer!, "entry.consequence", "Reader-facing attention copy must use the supported consequence.");
        Require(renderer!, "entry.code", "The allowlisted code may remain a technical signal/filter identity.");
        Require(entry!, "renderWorkAttentionSummary", "The Work view must show a truthful summary for the current scoped cards.");
        Require(entry!, "filterWorkEntries(work)", "The attention summary must share the filtered Work result set rather than read unrelated project-wide alerts.");
        Require(summary!, "Không có tín hiệu cần chú ý được hỗ trợ", "The empty state must say no supported signal is available from current evidence.");
        Require(summary!, "không khẳng định là không có rủi ro", "The empty state must not claim the work is risk-free.");
        Require(listCard!, "renderWorkAttention(card)", "List cards must show only supported projected attention when present.");
        Require(kanbanCard!, "renderWorkAttention(card)", "Kanban cards must show only supported projected attention when present.");
        TestAssert.False(renderer!.Contains("alert.message", StringComparison.OrdinalIgnoreCase), "Raw analysis messages must not become Work card copy.");
        TestAssert.False(renderer.Contains("state.views", StringComparison.Ordinal), "Work attention rendering must not read another analysis/view projection.");
        TestAssert.False(renderer.Contains("readiness", StringComparison.OrdinalIgnoreCase), "Readiness evidence must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("governance", StringComparison.OrdinalIgnoreCase), "Governance evidence must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("gate", StringComparison.OrdinalIgnoreCase), "Gate records must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("decision", StringComparison.OrdinalIgnoreCase), "Decision or human-action evidence must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("diagnostics", StringComparison.OrdinalIgnoreCase), "Diagnostics must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("import", StringComparison.OrdinalIgnoreCase), "Import/source warnings must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("warnings", StringComparison.OrdinalIgnoreCase), "Arbitrary warnings must not be synthesized into Work attention.");
        TestAssert.False(renderer.Contains("node(\"span\", entry.code", StringComparison.Ordinal), "Machine codes must not be displayed as primary reader-facing copy.");
        TestAssert.False(groups!.Contains("attention", StringComparison.OrdinalIgnoreCase), "Needs Attention must remain independent from authored-state membership.");
    }

    public static void WorkSearchAndFiltersUseOneSharedTransientPipeline()
    {
        var app = File.ReadAllText(AppJsPath());
        var workState = ExtractFunction(app, "function createWorkState()");
        var filter = ExtractFunction(app, "function filterWorkEntries(");
        var entry = ExtractFunction(app, "function renderWorkEntry(");
        var list = ExtractFunction(app, "function renderWorkList(");
        var hierarchy = ExtractFunction(app, "function buildWorkListHierarchy(");
        var kanban = ExtractFunction(app, "function renderWorkKanban(");

        TestAssert.True(workState is not null, "Work must own a transient shared criteria state.");
        TestAssert.True(filter is not null, "Missing approved Feature 009 behavior: shared Work search and filtering have not been implemented.");
        TestAssert.True(entry is not null && list is not null && hierarchy is not null && kanban is not null, "List and Kanban must remain consumers of the same Work entry state.");

        Require(workState!, "mode: \"list\"", "The active Work mode must remain in shared in-memory Work state.");
        Require(workState!, "phaseScope", "Explicit phase scope must remain a shared Work criterion.");
        Require(workState!, "query", "Search query must remain a shared Work criterion.");
        Require(workState!, "authoredStateFilter", "Authored-state selection must remain a shared Work criterion.");
        Require(workState!, "needsAttentionOnly", "Needs Attention selection must remain a shared Work criterion.");
        Require(workState!, "includeUnrecorded", "Unrecorded selection must remain separate and explicit.");
        TestAssert.False(workState!.Contains("localStorage", StringComparison.OrdinalIgnoreCase), "Work interaction state must not be persisted to localStorage.");

        Require(entry!, "renderWorkFilters(work)", "One Work-level filter surface must apply regardless of selected mode.");
        Require(entry!, "filterWorkEntries(work)", "Work attention summary and both views must use the filtered card collection.");
        Require(list!, "buildWorkListHierarchy(work)", "List must consume the shared Work hierarchy projection.");
        Require(hierarchy!, "filterWorkEntries(work)", "List hierarchy must consume the shared Work filtering pipeline.");
        Require(kanban!, "filterWorkEntries(work)", "Kanban must consume the same Work filtering pipeline as List.");
        Require(filter!, "workCardScopeEntries(work)", "The shared pipeline must begin from the canonical card collection after explicit phase scope.");
        Require(filter!, "normalizeWorkSearchValue", "Work search must use its approved normalized matching rule.");
        Require(filter!, "authoredStateFilter", "Authored-state filtering must use projected effective execution state.");
        Require(filter!, "needsAttentionOnly", "Needs Attention filtering must use the projected Work attention signal.");
        Require(filter!, "includeUnrecorded", "The explicit unrecorded criterion must remain distinct from authored state.");
    }

    public static void WorkSearchControlsExposeOnlyApprovedReaderFacingCriteria()
    {
        var app = File.ReadAllText(AppJsPath());
        var html = File.ReadAllText(IndexHtmlPath());
        var filters = ExtractFunction(app, "function renderWorkFilters(");
        var normalize = ExtractFunction(app, "function normalizeWorkSearchValue(");
        var searchMatch = ExtractFunction(app, "function workEntryMatchesQuery(");

        TestAssert.True(filters is not null && normalize is not null && searchMatch is not null,
            "Work search and filter controls must have explicit, testable presentation and matching seams.");

        foreach (var controlId in new[] { "work-search", "work-authored-state", "work-needs-attention", "work-include-unrecorded" })
        {
            Require(filters!, controlId, $"The Work filter surface must expose the shared '{controlId}' control.");
            Require(html, $"id=\"{controlId}\"", $"The approved Work filter control '{controlId}' must be defined in the existing HTML shell.");
        }
        Require(html, "id=\"work-filters-template\"", "Work filter controls must be semantic HTML in a reusable native template.");

        Require(normalize!, "normalize(\"NFD\")", "Search normalization must decompose Vietnamese diacritics before matching.");
        Require(normalize!, "đĐ", "Search normalization must account for Vietnamese đ/Đ, which NFD does not decompose.");
        Require(searchMatch!, "card.name", "Search may match the reader-facing Delivery Card name.");
        Require(searchMatch!, "card.key.id", "Search may match the canonical stable Delivery Card ID.");
        Require(searchMatch!, "phase.name", "Search may match the reader-facing parent Phase name.");
        Require(searchMatch!, "workPackage.name", "Search may match the reader-facing parent Work Package name.");
        foreach (var forbidden in new[] { "sourceReferences", "diagnostics", "analysisMessage", "proposal", "sourcePath" })
            TestAssert.False(searchMatch!.Contains(forbidden, StringComparison.OrdinalIgnoreCase), $"Work search must not inspect '{forbidden}'.");
    }

    public static void WorkFilteredListProjectionKeepsOnlyRequiredAncestorPaths()
    {
        var app = File.ReadAllText(AppJsPath());
        var hierarchy = ExtractFunction(app, "function buildWorkListHierarchy(");
        var list = ExtractFunction(app, "function renderWorkList(");

        TestAssert.True(hierarchy is not null, "Missing approved behavior: filtered Work List ancestry has no shared hierarchy projection.");
        TestAssert.True(list is not null, "The Work List renderer must remain available.");
        Require(list!, "buildWorkListHierarchy(work)", "List rows and ancestor visibility must come from one filtered hierarchy projection.");
        Require(hierarchy!, "filterWorkEntries(work)", "List hierarchy visibility must use the shared surviving-card result set.");
        Require(hierarchy!, "workCardScopeEntries(work)", "The projection must distinguish cards absent from scope from scoped cards removed by result filters.");
        Require(hierarchy!, "hasWorkResultFilters", "Phase scope alone must not activate result-filter ancestor pruning.");
        Require(hierarchy!, "phaseScope", "An explicit phase scope must not be widened while revealing search results.");
        Require(hierarchy!, "workPackageIds", "Phase ancestry must use canonical Work Package references.");
        Require(hierarchy!, "deliveryCardIds", "Work Package ancestry must use canonical Delivery Card references.");
        Require(hierarchy!, "cardCount", "Only surviving Delivery Cards may contribute to the displayed result count.");
        Require(hierarchy!, "temporarilyExpanded", "Search expansion must be transient and separate from saved expansion state.");
    }

    public static void WorkFilteredListHasScopedEmptyRecoveryAndExpansionRestoration()
    {
        var app = File.ReadAllText(AppJsPath());
        var emptyState = ExtractFunction(app, "function renderWorkEmptyState(");
        var clearCriterion = ExtractFunction(app, "function clearWorkCriterion(");

        TestAssert.True(emptyState is not null && clearCriterion is not null,
            "Work empty states need explicit, testable recovery actions that do not erase unrelated criteria.");
        Require(emptyState!, "empty-phase", "A selected Phase with no canonical Delivery Cards must have its own empty state.");
        Require(emptyState!, "no-results", "A query/filter combination with no matches must have a distinct empty state.");
        Require(emptyState!, "Xem tất cả giai đoạn", "The empty-Phase recovery must act on Phase scope only.");
        Require(emptyState!, "Xóa tìm kiếm", "No-match recovery must offer a query-only reset when a query is active.");
        Require(emptyState!, "Bỏ lọc trạng thái", "No-match recovery must offer a status-only reset when selected.");
        Require(emptyState!, "Cần chú ý", "No-match recovery must offer an attention-only reset when selected.");
        Require(emptyState!, "Chưa ghi nhận", "No-match recovery must offer an unrecorded-only reset when excluded.");
        Require(emptyState!, "setWorkPhaseScope(null)", "The Phase recovery action must not clear query or other filters.");
        Require(emptyState!, "clearWorkCriterion", "Each filter recovery action must clear only its named criterion.");
        foreach (var criterion in new[] { "query", "authoredStateFilter", "needsAttentionOnly", "includeUnrecorded" })
            Require(clearCriterion!, criterion, $"The recovery helper must support resetting only '{criterion}'.");
        Require(clearCriterion!, "switch", "Only explicitly requested filter criteria may be cleared.");
    }

    public static void WorkInspectorUsesTypedIdentityAndEvidenceBackedCardFields()
    {
        var app = File.ReadAllText(AppJsPath());
        var model = ExtractFunction(app, "function buildWorkInspectorModel(");
        var renderer = ExtractFunction(app, "function renderWorkInspector(");
        TestAssert.True(model is not null && renderer is not null,
            "Missing approved Feature 009 behavior: Work has no shared read-only inspector model and renderer.");
        var evidenceLabels = renderer + "\n"
            + ExtractFunction(app, "function workDataStateLabel(")
            + "\n" + ExtractFunction(app, "function workDetailValue(")
            + "\n" + ExtractFunction(app, "function workExecutionStateLabel(");

        Require(model!, "key.kind", "Inspector identity must retain the canonical kind-qualified identity.");
        Require(model!, "key.id", "Inspector identity must retain the canonical stable ID.");
        Require(model!, "work.cards", "Delivery Card details must resolve from the one official Work card collection.");
        var inspectorContract = model + "\n" + renderer;
        foreach (var field in new[] { "plannedStart", "plannedFinish", "plannedEffortHours", "plannedEffortState", "execution", "recorded", "actualStart", "actualFinish", "actualEffortHours", "remainingEffortHours", "roles", "sourceReferences" })
            Require(inspectorContract, field, $"The shared Delivery Card inspector must preserve evidence-backed '{field}' data.");
        Require(renderer!, "Kết thúc kế hoạch", "Inspector finish dates must retain baseline planned semantics.");
        Require(renderer!, "Chưa ghi nhận", "No execution record must remain visibly distinct from an authored state.");
        Require(evidenceLabels, "UNKNOWN", "Unknown evidence must remain distinct from numeric zero.");
        Require(evidenceLabels, "INVALID", "Invalid evidence must remain distinct from unknown and zero.");
        Require(evidenceLabels, "BLOCKED", "Blocked evidence must remain distinct from unknown and zero.");
        Require(evidenceLabels, "UNRESOLVED", "Unresolved evidence must remain distinct from unknown and zero.");
        Require(evidenceLabels, "NOT_RUN", "Not-run evidence must remain distinct from an authored execution state.");
        Require(renderer!, "sourceReferences", "Provenance must remain available in secondary inspector detail.");
        TestAssert.False(renderer!.Contains("Propose execution update", StringComparison.Ordinal), "The read-only Work inspector must not offer proposal actions.");
        TestAssert.False(renderer.Contains("data-work-edit", StringComparison.Ordinal), "The read-only Work inspector must not offer edits.");
    }

    public static void WorkInspectorLimitsDependenciesAndNonCardSemantics()
    {
        var app = File.ReadAllText(AppJsPath());
        var model = ExtractFunction(app, "function buildWorkInspectorModel(");
        var links = ExtractFunction(app, "function buildWorkDependencyLinks(");
        TestAssert.True(model is not null && links is not null,
            "Missing approved Feature 009 behavior: Work inspector dependency and kind resolution are not implemented.");

        Require(model!, "findWorkWbsNode", "Non-card inspectors must resolve only existing typed WBS evidence.");
        Require(model!, "milestoneKind", "Milestones must retain their existing kind without Delivery Card semantics.");
        Require(model!, "identity.kind === \"DeliveryCard\"", "Card-only fields must be added only in the Delivery Card branch.");
        Require(links!, "includedInAnalysis !== true", "Only explicitly analysis-included direct edges may become primary Work relationships.");
        Require(links!, "predecessorKey", "Dependency details must preserve predecessor direction.");
        Require(links!, "subjectKey", "Dependency details must preserve successor direction.");
        Require(links!, "network.nodes", "A primary dependency link requires a resolved canonical node and reader-facing name.");
        Require(links!, "predecessors", "A direct predecessor must be presented as an item the selected work depends on.");
        Require(links!, "successors", "A direct successor must be presented as an item directly affected by the selected work.");
        TestAssert.False(links!.Contains("dependencyImpact", StringComparison.Ordinal), "Work must not inherit transitive Gantt impact calculations.");
        TestAssert.False(links.Contains("downstreamKeys", StringComparison.Ordinal), "Work relationships must remain direct, not a downstream chain.");
    }

    public static void WorkSelectionFocusScrollAndCriteriaRemainIndependent()
    {
        var app = File.ReadAllText(AppJsPath());
        var workState = ExtractFunction(app, "function createWorkState()");
        var close = ExtractFunction(app, "function closeWorkInspector()");
        var render = ExtractFunction(app, "function renderActiveView()");
        var hidden = ExtractFunction(app, "function renderWorkEntry(");
        var scrollPlan = ExtractFunction(app, "function workScrollPlan(");
        var focusChoice = ExtractFunction(app, "function chooseWorkFocusReturn(");

        TestAssert.True(workState is not null && close is not null && render is not null && hidden is not null && scrollPlan is not null && focusChoice is not null,
            "Missing approved Feature 009 behavior: Work selection, inspector, focus, and scroll state are not independently managed.");
        foreach (var stateField in new[] { "selectedItemKey", "inspectorOpen", "focusReturnKey", "scrollPositions", "phaseScope", "query" })
            Require(workState!, stateField, $"Transient Work state must represent '{stateField}' independently.");
        TestAssert.False(workState!.Contains("localStorage", StringComparison.OrdinalIgnoreCase), "Inspector selection, filters, and scroll must not be persisted to localStorage.");
        TestAssert.False(close!.Contains("selectedItemKey = null", StringComparison.Ordinal), "Closing the inspector must not clear canonical selection.");
        Require(close!, "restoreWorkFocusReturn", "Closing the inspector must restore focus to the invoking item or approved fallback.");
        Require(hidden!, "đang được chọn", "A selected item outside current results must be explained.");
        Require(hidden!, "Mở chi tiết", "A hidden selected item must remain inspectable without widening filters.");
        Require(hidden!, "event.key !== \"Escape\"", "Escape must close the read-only inspector without changing selected identity.");
        Require(hidden!, "closeWorkInspector()", "Escape and Close must use the same focus-return behavior.");
        Require(render!, "captureWorkModeScroll", "Work must save the outgoing mode scroll position before replacing its view.");
        Require(render!, "restoreWorkModeScroll", "Work must restore or reset scroll according to criteria identity.");
        Require(scrollPlan!, "criteria", "Scroll restoration must be conditioned on shared Work criteria equality.");
        Require(scrollPlan!, "selectedMatches", "Criteria changes reveal selection only when it remains in current results.");
        Require(focusChoice!, "invokerKey", "Selection identity and keyboard focus return target must remain separate.");
        Require(focusChoice!, "fallbackId", "Unavailable or filtered invokers must use the approved focus fallback.");
    }

    public static void WorkRefreshUsesOfficialAuthorityAndRetainsPreviewContext()
    {
        var app = File.ReadAllText(AppJsPath());
        var refresh = ExtractFunction(app, "async function refresh()");
        var updateOfficial = ExtractFunction(app, "function applyOfficialWorkSnapshot(");
        var applyImport = ExtractFunction(app, "function applyManifestImport(");

        TestAssert.True(refresh is not null && updateOfficial is not null && applyImport is not null,
            "Missing approved Feature 009 behavior: refresh does not reconcile Work against official snapshot authority.");
        Require(refresh!, "/api/manifest-import/official", "Refresh must read the existing official snapshot endpoint rather than treating preview views as Work authority.");
        Require(refresh!, "applyOfficialWorkSnapshot", "Successful refresh must reconcile the official Work projection and snapshot identity.");
        Require(updateOfficial!, "snapshotId", "Only the active official snapshot identity may invalidate stale selection.");
        Require(updateOfficial!, "selectedItemKey = null", "A changed official snapshot must clear stale selected identity.");
        Require(updateOfficial!, "inspectorOpen = false", "A changed official snapshot must close stale inspector context.");
        TestAssert.False(updateOfficial!.Contains("query = \"\"", StringComparison.Ordinal), "Snapshot identity must not clear unrelated Work search state.");
        TestAssert.False(updateOfficial.Contains("phaseScope = { kind: \"all\"", StringComparison.Ordinal), "Snapshot identity must not silently widen the user's phase scope.");
        Require(applyImport!, "response.classification === \"OFFICIAL_COMMIT\"", "Candidate and failed imports must not enter the official Work reconciliation path.");
    }

    public static void WorkAndGanttNavigationPreserveTypedIdentityAndInspectorCondition()
    {
        var app = File.ReadAllText(AppJsPath());
        var workNavigation = ExtractFunction(app, "function prepareWorkToGantt(");
        var ganttNavigation = ExtractFunction(app, "function prepareGanttToWork(");
        var inspector = ExtractFunction(app, "function renderWorkInspector(");
        var ganttDetail = ExtractFunction(app, "function renderGanttDetail(");
        var ganttRender = ExtractFunction(app, "function renderGantt(");

        TestAssert.True(workNavigation is not null && ganttNavigation is not null && inspector is not null && ganttDetail is not null && ganttRender is not null,
            "Missing approved Feature 009 behavior: Work and Gantt do not share canonical selection navigation.");
        Require(workNavigation!, "selectedItemKey", "Work-to-Gantt navigation must use the selected typed Work identity.");
        Require(workNavigation!, "state.work.inspectorOpen", "Work-to-Gantt navigation must preserve whether the Work inspector was open.");
        Require(workNavigation!, "selectedRowKey", "Gantt focus must receive the same canonical typed identity.");
        Require(workNavigation!, "expandedKeys", "Gantt navigation must reveal the item's visible ancestor path when available.");
        TestAssert.False(workNavigation!.Contains("state.gantt.showDependencies =", StringComparison.Ordinal),
            "Work-to-Gantt navigation must not change the existing dependency control state.");
        Require(inspector!, "Xem trên Gantt", "The read-only Work inspector must offer navigation to the matching Gantt identity.");
        Require(ganttDetail!, "Mở trong Công việc", "The Gantt inspector must offer reverse navigation to the same Work identity.");
        Require(ganttDetail!, "open-in-work", "Reverse navigation must be an explicit existing Gantt inspector action.");
        Require(ganttNavigation!, "selectedItemKey", "Gantt-to-Work navigation must share the typed selected identity.");
        Require(ganttNavigation!, "inspectorOpen", "Gantt-to-Work navigation must preserve the prior drawer state.");
        Require(ganttRender!, "gantt-selection-status", "Gantt must explain when its current controls hide or do not contain the requested identity.");
        Require(ganttRender!, "selectedRowVisible", "Gantt reveal status must be based on the existing visible-row projection.");
        foreach (var workCriterion in new[] { "phaseScope", "query", "authoredStateFilter", "needsAttentionOnly", "includeUnrecorded", "mode" })
            TestAssert.False(ganttNavigation!.Contains("state.work." + workCriterion + " =", StringComparison.Ordinal),
                $"Gantt-to-Work navigation must retain the user's existing '{workCriterion}' choice.");
        Require(ganttRender!, "detailsOpen", "Gantt inspector visibility must be independent from selected row identity.");
    }

    private static string? ExtractFunction(string source, string signature)
    {
        var start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0) return null;
        var end = source.IndexOf("\n  function ", start + signature.Length, StringComparison.Ordinal);
        return end > start ? source[start..end] : source[start..];
    }

    private static void Require(string source, string expected, string message) =>
        TestAssert.True(source.Contains(expected, StringComparison.Ordinal), message);

    private static string AppJsPath() => Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "app.js");
    private static string IndexHtmlPath() => Path.Combine(Directory.GetCurrentDirectory(), "src", "ProjectManagementCompiler", "wwwroot", "index.html");
}
