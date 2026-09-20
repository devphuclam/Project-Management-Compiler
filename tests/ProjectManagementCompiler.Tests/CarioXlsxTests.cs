using System.IO.Compression;
using System.Xml.Linq;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class CarioXlsxTests
{
    public static void CarioXlsxUsesTheApprovedContractHeadersAndRecordSemantics()
    {
        var project = CaptureCanonicalProject();
        var model = new CarioMappingProjector().Build(project);
        var bytes = new CarioXlsxExporter().Export(model);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        TestAssert.Equal(
            "Work Item Type|Task ID|Phase|Work Package|Nội dung công việc|Ngày bắt đầu dự kiến|Deadline|Mức độ ưu tiên|Đơn vị / Phòng ban|Ban|Ghi chú|Trạng thái ban đầu|Planned Effort (hours)|Baseline / Analysis State|Source Reference",
            string.Join('|', ReadHeader(archive, "xl/worksheets/sheet1.xml")),
            "01_TASKS must use the approved CARIO contract headers.");
        TestAssert.Equal(
            "Task ID|Project Logical Role|CARIO Person / Account|CARIO Role|Mapping Status|Source Reference",
            string.Join('|', ReadHeader(archive, "xl/worksheets/sheet2.xml")),
            "02_ASSIGNMENTS must use the approved CARIO contract headers.");
        TestAssert.Equal(
            "Parent ID|Child ID|Relationship Type|Child Type|Name|Planned Date / Deadline|Source Reference",
            string.Join('|', ReadHeader(archive, "xl/worksheets/sheet3.xml")),
            "03_CHILDREN_MILESTONES must use the approved CARIO contract headers.");
        TestAssert.Equal(
            "Subject Kind|Subject ID|Predecessor Kind|Predecessor ID|Dependency Type|Analysis Eligibility|Validation State|Source Reference",
            string.Join('|', ReadHeader(archive, "xl/worksheets/sheet4.xml")),
            "04_DEPENDENCIES must use the approved CARIO contract headers.");
        TestAssert.Equal(
            "Field|Value|Data State|Source Reference",
            string.Join('|', ReadHeader(archive, "xl/worksheets/sheet5.xml")),
            "05_PROJECT_INFO must use the approved CARIO contract headers.");
        TestAssert.Equal(
            "Warning ID|Severity|Code|Message|Affected Item IDs|Source Reference",
            string.Join('|', ReadHeader(archive, "xl/worksheets/sheet6.xml")),
            "06_IMPORT_WARNINGS must use the approved CARIO contract headers.");

        TestAssert.Equal(60, model.Tasks.Count, "CARIO task rows must include 53 cards and 7 decision/milestone records.");
        TestAssert.Equal(60, model.ChildrenMilestones.Count, "The children/milestones sheet must preserve all card and milestone relationships.");
        TestAssert.Equal(53, model.Tasks.Count(task => task.WorkItemType == "DeliveryCard"), "CARIO must preserve all 53 executable delivery-card rows.");
        TestAssert.Equal(7, model.Tasks.Count(task => task.WorkItemType is "Decision" or "Milestone"), "CARIO must preserve all 7 decision/milestone rows.");
        TestAssert.True(model.Tasks.All(task => !string.IsNullOrWhiteSpace(task.SourceReference)), "CARIO task rows must retain safe source provenance.");
    }

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

        var dependencySheetXml = LoadXml(archive, "xl/worksheets/sheet4.xml");
        TestAssert.Contains("FINISH_TO_START", dependencySheetXml, "Dependency types must use stable contract enum codes.");
        TestAssert.Contains("INVALID_SOURCE_EVIDENCE", dependencySheetXml, "Invalid source dependency state must remain explicit in the workbook.");
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

    public static void CarioXlsxExportIsDeterministicForTheSameModel()
    {
        var project = CaptureCanonicalProject();
        var model = new CarioMappingProjector().Build(project);
        var first = new CarioXlsxExporter().Export(model);
        var second = new CarioXlsxExporter().Export(model);

        TestAssert.True(first.SequenceEqual(second), "The same canonical model must produce deterministic CARIO package bytes.");
    }

    public static void CarioXlsxGanttWorkbookContainsPreviewProvenanceMarkers()
    {
        var result = new ProjectCompiler().CompileAsync(new CompilationRequest
        {
            SourcePath = Path.Combine(Directory.GetCurrentDirectory(), "tests", "fixtures", "ideaengineering"),
            AsOfDate = new DateOnly(2026, 9, 28)
        }).GetAwaiter().GetResult();
        var bytes = new ProjectCompiler().ExportCarioXlsx(result);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var projectInfo = LoadXml(archive, "xl/worksheets/sheet5.xml");
        TestAssert.Contains("PMC_EXPORT_KIND", projectInfo, "The seven-sheet CARIO + Gantt export must identify its producer.");
        TestAssert.Contains("CARIO_GANTT", projectInfo, "The preview marker must identify the Gantt workbook contract.");
        TestAssert.Contains("PMC_EXPORT_CONTRACT_VERSION", projectInfo, "The preview marker must identify the contract version.");
        TestAssert.Contains("PMC_PROJECT_ID", projectInfo, "The preview marker must carry the project ID.");
        TestAssert.Contains("PMC_PROJECT_NAME", projectInfo, "The preview marker must carry the project name.");
    }

    private static string LoadXml(ZipArchive archive, string entryName)
    {
        using var stream = archive.GetEntry(entryName)!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IReadOnlyList<string> ReadHeader(ZipArchive archive, string entryName)
    {
        using var stream = archive.GetEntry(entryName)!.Open();
        var document = XDocument.Load(stream);
        var headerRow = document
            .Descendants()
            .Where(element => element.Name.LocalName == "row")
            .First();
        return headerRow
            .Descendants()
            .Where(element => element.Name.LocalName == "t")
            .Select(element => element.Value)
            .ToArray();
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
