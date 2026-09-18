using System.IO.Compression;
using System.Xml.Linq;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class CarioXlsxTests
{
    public static void CarioXlsxUsesSixExactSheetsAndBclPackageParts()
    {
        var project = CaptureCanonicalProject();
        var model = new CarioMappingProjector().Build(project, new CarioMappingConfiguration
        {
            TaskMappings = new Dictionary<string, CarioTaskMapping>(StringComparer.OrdinalIgnoreCase)
            {
                ["P01-A"] = new CarioTaskMapping { Notes = "Kiểm thử tiếng Việt" }
            }
        });
        var bytes = new CarioXlsxExporter().Export(model);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
        foreach (var required in new[]
        {
            "[Content_Types].xml",
            "_rels/.rels",
            "xl/workbook.xml",
            "xl/_rels/workbook.xml.rels",
            "xl/worksheets/sheet1.xml",
            "xl/worksheets/sheet2.xml",
            "xl/worksheets/sheet3.xml",
            "xl/worksheets/sheet4.xml",
            "xl/worksheets/sheet5.xml",
            "xl/worksheets/sheet6.xml"
        })
        {
            TestAssert.True(names.Contains(required), $"XLSX package must contain '{required}'.");
        }

        var workbook = XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        var sheetNames = workbook.Descendants().Where(element => element.Name.LocalName == "sheet").Select(element => (string)element.Attribute("name")!).ToArray();
        TestAssert.Equal(
            "01_TASKS,02_ASSIGNMENTS,03_CHILDREN_MILESTONES,04_DEPENDENCIES,05_PROJECT_INFO,06_IMPORT_WARNINGS",
            string.Join(',', sheetNames),
            "CARIO workbook sheet names and order must be exact.");

        var taskSheetXml = LoadXml(archive, "xl/worksheets/sheet1.xml");
        TestAssert.Contains("Kiểm thử tiếng Việt", taskSheetXml, "XLSX inline strings must preserve Vietnamese text.");
        TestAssert.Contains("2026-09-18", taskSheetXml, "CARIO task sheet must preserve planned baseline dates.");
        TestAssert.Contains("t=\"n\"", taskSheetXml, "Planned effort must remain a numeric worksheet value.");

        var assignmentSheetXml = LoadXml(archive, "xl/worksheets/sheet2.xml");
        TestAssert.Contains("UNRESOLVED_IDENTITY", assignmentSheetXml, "Unresolved mapping status must survive in the assignments sheet.");
        TestAssert.True(assignmentSheetXml.Contains("<t />", StringComparison.Ordinal) || assignmentSheetXml.Contains("<t></t>", StringComparison.Ordinal), "Unresolved concrete identity must remain blank rather than fabricated.");

        var warningSheetXml = LoadXml(archive, "xl/worksheets/sheet6.xml");
        TestAssert.Contains("CARIO_MAPPING_UNRESOLVED", warningSheetXml, "Mapping warnings must be exported to sheet 06.");
    }

    public static void CarioXlsxNeverReplacesBaselineDatesWithActualDates()
    {
        var project = CaptureCanonicalProject();
        var updated = new ExecutionOverlayUpdater().Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P01-A",
            ExecutionState = ExecutionState.Completed,
            ActualStart = new DateOnly(2026, 9, 21),
            ActualFinish = new DateOnly(2026, 9, 22),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 22, 10, 0, 0, TimeSpan.Zero)
        });
        TestAssert.True(updated.Accepted, "The execution update should be accepted for the planned-date regression.");

        var model = new CarioMappingProjector().Build(updated.Project);
        var bytes = new CarioXlsxExporter().Export(model);
        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var taskSheetXml = LoadXml(archive, "xl/worksheets/sheet1.xml");
        var taskRow = XDocument.Parse(taskSheetXml)
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .Single(row => row.Descendants().Any(element => element.Name.LocalName == "t" && element.Value == "P01-A"));
        var taskRowText = string.Join("|", taskRow.Descendants().Where(element => element.Name.LocalName is "t" or "v").Select(element => element.Value));

        TestAssert.Contains("2026-09-18", taskRowText, "Planned start must remain the canonical baseline date.");
        TestAssert.False(taskRowText.Contains("2026-09-21", StringComparison.Ordinal), "Actual start must not overwrite the planned CARIO start cell.");
        TestAssert.False(taskRowText.Contains("2026-09-22", StringComparison.Ordinal), "Actual finish must not overwrite the planned CARIO deadline cell.");
    }

    private static string LoadXml(ZipArchive archive, string entryName)
    {
        using var stream = archive.GetEntry(entryName)!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static CanonicalProject CaptureCanonicalProject()
    {
        var root = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering");
        var snapshot = new LocalRepositorySourceAdapter()
            .CaptureAsync(new SourceRequest { Location = root }, CancellationToken.None)
            .GetAwaiter()
            .GetResult();
        var resolution = AuthorityResolution.Resolve(snapshot);
        return new CanonicalProjectNormalizer().Normalize(new IdeaEngineeringExtractor().Extract(resolution));
    }
}
