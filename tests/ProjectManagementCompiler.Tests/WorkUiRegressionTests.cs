namespace ProjectManagementCompiler.Tests;

internal static class WorkUiRegressionTests
{
    public static void WorkListUsesCanonicalHierarchyAndOneTypedDeliveryCardIdentity()
    {
        var app = File.ReadAllText(AppJsPath());
        var list = ExtractFunction(app, "function renderWorkList(");
        TestAssert.True(list is not null, "Missing approved Feature 009 behavior: the primary Work List renderer does not exist.");
        var renderer = list!;

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
        var scopedCards = ExtractFunction(app, "function workCardScopeEntries(");
        var kanban = ExtractFunction(app, "function renderWorkKanban(");
        var groups = ExtractFunction(app, "function buildWorkKanbanGroups(");
        var ordering = ExtractFunction(app, "function sortWorkKanbanEntries(");

        TestAssert.True(kanban is not null, "Missing Feature 009 behavior: the primary Work destination has no Unified Kanban renderer.");
        TestAssert.True(entry is not null && list is not null && scopedCards is not null && groups is not null && ordering is not null,
            "Unified Kanban must share explicit Work mode, card collection, grouping, and deterministic ordering seams.");

        Require(entry!, "state.work.mode", "List/Kanban mode must be transient shared Work view state.");
        Require(entry!, "renderWorkList(work)", "List must remain available in the primary Work destination.");
        Require(entry!, "renderWorkKanban(work)", "Kanban must be selectable inside Work without replacing the legacy Advanced route.");
        Require(entry!, "Danh sách", "The native Work mode control must expose List by its reader-facing name.");
        Require(entry!, "Kanban", "The native Work mode control must expose Kanban.");

        var collectionContract = list! + "\n" + scopedCards! + "\n" + kanban!;
        Require(list!, "workCardScopeEntries(work)", "List must use the shared canonical Delivery Card scope helper.");
        Require(kanban!, "workCardScopeEntries(work)", "Kanban must use the same scoped Work card collection as List.");
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
}
