using System.Text.RegularExpressions;

namespace ProjectManagementCompiler.Tests;

internal static class FixtureShapeTests
{
    private static readonly string FixtureRoot = Path.Combine(
        Directory.GetCurrentDirectory(),
        "tests",
        "fixtures",
        "ideaengineering");

    private static readonly string[] RecognizedPaths =
    [
        "README.md",
        "docs/product/instances/idea-engineering/DOC-07-mvp-roadmap-and-delivery-plan.md",
        "docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md",
        "docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html",
        "docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md"
    ];

    public static void RecognizedFixturePathsExistAndArePublicSafe()
    {
        foreach (var relativePath in RecognizedPaths)
        {
            var fullPath = Path.Combine(FixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            TestAssert.True(File.Exists(fullPath), $"Recognized fixture path should exist: {relativePath}");
        }

        TestAssert.False(
            Directory.Exists(Path.Combine(FixtureRoot, ".git")),
            "The controlled fixture must not contain a private reference checkout.");

        var allText = string.Join(
            Environment.NewLine,
            RecognizedPaths.Select(path => File.ReadAllText(Path.Combine(FixtureRoot, path.Replace('/', Path.DirectorySeparatorChar)))));

        TestAssert.False(allText.Contains("IDEAEngineering", StringComparison.Ordinal), "Fixture must use public-safe synthetic text.");
        TestAssert.False(allText.Contains("@", StringComparison.Ordinal), "Fixture must not contain employee or email data.");
        TestAssert.False(allText.Contains("ghp_", StringComparison.Ordinal), "Fixture must not contain credential material.");
    }

    public static void FixtureHasRequiredCountsAndAuthoritativeEffort()
    {
        var readme = Read("README.md");
        TestAssert.Contains("Phases: PH0, PH1, PH2, PH3, PH4, PH5", readme, "Fixture README should declare all phases.");
        TestAssert.Contains("Delivery cards: 53", readme, "Fixture README should declare the delivery-card count.");
        TestAssert.Contains("Gate/milestone records: 7", readme, "Fixture README should declare the gate count.");
        TestAssert.Contains("Authoritative work-package effort: 512 hours", readme, "Fixture README should declare authoritative effort.");

        var appendix = Read("docs/product/instances/idea-engineering/planning/DOC-07-appendix-A-task-breakdown-december-2026.md");
        var packageRows = Regex.Matches(appendix, "^\\| (P\\d{2}|F\\d{2}|C\\d{2}|W\\d{2}|L\\d{2}|Q\\d{2}) \\|.*$", RegexOptions.Multiline);
        TestAssert.Equal(35, packageRows.Count, "Appendix A should contain exactly 35 work-package rows.");

        var packageEffort = packageRows.Sum(row =>
        {
            var cells = row.Value.Split('|', StringSplitOptions.TrimEntries);
            return int.Parse(cells[6]);
        });
        TestAssert.Equal(512, packageEffort, "Appendix A work-package effort should total 512 hours.");

        var roadmap = Read("docs/product/instances/idea-engineering/planning/idea-roadmap-december-2026.html");
        foreach (var phase in new[] { "PH0", "PH1", "PH2", "PH3", "PH4", "PH5" })
        {
            TestAssert.Contains($"data-phase=\"{phase}\"", roadmap, $"Roadmap should contain phase {phase}.");
        }

        foreach (var gate in new[] { "G-D0", "G-MS0", "G-MS1", "G-MS2", "G-MS3", "G-MS4", "G-MS5" })
        {
            TestAssert.Contains($"data-gate=\"{gate}\"", roadmap, $"Roadmap should contain gate {gate}.");
        }

        var kanban = Read("docs/product/instances/idea-engineering/planning/idea-technical-pilot-kanban-cario.md");
        var cardRows = Regex.Matches(kanban, "^\\| (?:P|F|C|W|L|Q)\\d{2}-[A-Z] \\|.*$", RegexOptions.Multiline);
        TestAssert.Equal(53, cardRows.Count, "Kanban should contain exactly 53 delivery-card rows.");
        TestAssert.Contains("| State | NOT_STARTED |", kanban, "Cards should be authored as NOT_STARTED.");
        TestAssert.Contains("WIP policy | 1", kanban, "Kanban should encode WIP=1.");
        TestAssert.Contains("Initial reserve | 88", kanban, "Kanban should encode the initial reserve.");
        TestAssert.Contains("Capacity hours | 600", kanban, "Kanban should encode capacity.");
        TestAssert.Contains("| Missing predecessor | X99 |", kanban, "Kanban should retain a missing source predecessor for extraction diagnostics.");
    }

    private static string Read(string relativePath) =>
        File.ReadAllText(Path.Combine(FixtureRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
}
