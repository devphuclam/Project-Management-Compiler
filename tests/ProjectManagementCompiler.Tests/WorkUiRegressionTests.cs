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

        var nameIndex = card!.IndexOf("card.name", StringComparison.Ordinal);
        var idIndex = card.IndexOf("card.key.id", StringComparison.Ordinal);
        TestAssert.True(nameIndex >= 0 && idIndex > nameIndex, "The reader-facing Delivery Card name must precede its technical stable ID.");
        foreach (var technicalField in new[] { "plannedEffortHours", "actualEffortHours", "remainingEffortHours", "sourceReferences", "sourceSha", "diagnostics", "state.views.cpm" })
        {
            TestAssert.False(readerCopy.Contains(technicalField, StringComparison.OrdinalIgnoreCase), $"'{technicalField}' must not dominate the default Work rows.");
        }
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
