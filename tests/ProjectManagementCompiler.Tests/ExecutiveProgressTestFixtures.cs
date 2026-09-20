using System.IO.Compression;
using System.Xml.Linq;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class ExecutiveProgressTestFixtures
{
    public static CompilationResult BuildOfficialFixtureResult(DateOnly? asOfDate = null)
    {
        var project = CapturePlanningFixture();
        var sourceProject = SourceExecutionTestFixtures.Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 19),
            ActualEffortHours = 4m,
            RemainingEffortHours = 4m,
            LastUpdatedAt = new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero)
        });
        return SourceExecutionTestFixtures.BuildResult(sourceProject, asOfDate ?? new DateOnly(2026, 9, 19));
    }

    public static CanonicalProject CapturePlanningFixture()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }

    public static string ReadEntry(byte[] package, string entryName)
    {
        using var archive = new ZipArchive(new MemoryStream(package), ZipArchiveMode.Read);
        var entry = archive.GetEntry(entryName) ?? throw new InvalidOperationException($"Missing XLSX entry '{entryName}'.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    public static XDocument ReadXml(byte[] package, string entryName) => XDocument.Parse(ReadEntry(package, entryName));

    public static IReadOnlyList<string> SheetNames(byte[] package) =>
        ReadXml(package, "xl/workbook.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "sheet")
            .Select(element => (string?)element.Attribute("name") ?? string.Empty)
            .ToArray();

    public static string WorksheetText(byte[] package, int sheetNumber) =>
        string.Join("|", ReadXml(package, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName is "t" or "v")
            .Select(element => element.Value));

    public static IReadOnlyList<string> WorksheetHeaders(byte[] package, int sheetNumber) =>
        ReadXml(package, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Select(row => row.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value).ToArray())
            .FirstOrDefault() ?? Array.Empty<string>();

    public static IReadOnlyList<string> WorksheetRow(byte[] package, int sheetNumber, string firstCellText) =>
        ReadXml(package, $"xl/worksheets/sheet{sheetNumber}.xml")
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Select(row => row.Descendants().Where(element => element.Name.LocalName == "t").Select(element => element.Value).ToArray())
            .FirstOrDefault(cells => cells.FirstOrDefault() == firstCellText) ?? Array.Empty<string>();

    public static void AssertHasSheetNames(byte[] package, params string[] expected)
    {
        TestAssert.Equal(string.Join('|', expected), string.Join('|', SheetNames(package)), "The workbook sheet names must remain in the approved order.");
    }
}
