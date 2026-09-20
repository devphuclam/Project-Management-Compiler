using System.IO.Compression;
using System.Xml.Linq;
using ProjectManagementCompiler.Application;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Extraction;
using ProjectManagementCompiler.Management;
using ProjectManagementCompiler.Outputs;
using ProjectManagementCompiler.Sources;

namespace ProjectManagementCompiler.Tests;

internal static class GanttXlsxTests
{
    public static void ExcelExportButtonNamesGanttWorkbook()
    {
        var indexPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "ProjectManagementCompiler",
            "wwwroot",
            "index.html");
        var index = File.ReadAllText(indexPath);

        TestAssert.Contains("CARIO + Gantt", index, "The export action must tell users that the workbook contains the visual Gantt sheet.");
    }

    public static void PreviewWorkbookExportHasExplicitRoute()
    {
        var programPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "src",
            "ProjectManagementCompiler",
            "Program.cs");
        var program = File.ReadAllText(programPath);

        TestAssert.Contains("/api/exports/cario-preview.xlsx", program, "Preview export must require an explicit preview route instead of silently replacing the official workbook.");
    }

    public static void WorkbookExportSelectionKeepsPreviewOutOfOfficialRoute()
    {
        var officialMetadata = Metadata(ManifestImportClassification.OfficialCommit, "official-snapshot");
        var previewMetadata = Metadata(ManifestImportClassification.UncommittedPreview, "preview-snapshot");
        var official = Result(officialMetadata);
        var preview = Result(previewMetadata);
        var state = new CompilerApplicationState();
        state.RecordManifestImport(Import(ManifestImportClassification.OfficialCommit, officialMetadata), official);
        state.RecordManifestImport(Import(ManifestImportClassification.UncommittedPreview, previewMetadata), preview);

        TestAssert.Equal(
            "official-snapshot",
            WorkbookExportSelection.ResolveOfficial(state)!.Project.ImportMetadata!.SnapshotId,
            "The default workbook route must keep selecting the official snapshot while a preview is active.");
        TestAssert.Equal(
            "preview-snapshot",
            WorkbookExportSelection.ResolvePreview(state)!.Project.ImportMetadata!.SnapshotId,
            "The preview workbook route must select only the explicitly active preview.");

        var previewOnlyState = new CompilerApplicationState();
        previewOnlyState.RecordManifestImport(Import(ManifestImportClassification.UncommittedPreview, previewMetadata), preview);
        TestAssert.True(
            WorkbookExportSelection.ResolveOfficial(previewOnlyState) is null,
            "A preview-only session must not become the default official workbook export.");
    }

    public static void CarioXlsxIncludesDailyGanttProjection()
    {
        var project = CaptureCanonicalProject();
        var asOfDate = new DateOnly(2026, 9, 28);
        var updated = SourceExecutionTestFixtures.Apply(project, new ExecutionUpdate
        {
            WorkItemId = "P04-A",
            ExecutionState = ExecutionState.InProgress,
            ActualStart = new DateOnly(2026, 9, 25),
            LastUpdatedAt = new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero)
        });
        var analysis = new ManagementAnalysisOrchestrator().Analyze(updated, asOfDate);
        var views = new ManagementViewProjector().Build(updated, analysis, asOfDate);
        var result = new CompilationResult
        {
            Project = updated,
            Analysis = analysis,
            Views = views,
            Cario = new CarioMappingProjector().Build(updated),
            Mapping = new CarioMappingConfiguration(),
            SemanticDigest = "test"
        };

        var bytes = new ProjectCompiler().ExportCarioXlsx(result);

        using var archive = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var workbook = System.Xml.Linq.XDocument.Load(archive.GetEntry("xl/workbook.xml")!.Open());
        var sheetNames = workbook
            .Descendants()
            .Where(element => element.Name.LocalName == "sheet")
            .Select(element => (string)element.Attribute("name")!)
            .ToArray();

        TestAssert.Equal(
            "01_TASKS,02_ASSIGNMENTS,03_CHILDREN_MILESTONES,04_DEPENDENCIES,05_PROJECT_INFO,06_IMPORT_WARNINGS,07_GANTT",
            string.Join(',', sheetNames),
            "The management workbook must append a Gantt worksheet after the CARIO sheets.");
        TestAssert.True(
            archive.Entries.Any(entry => entry.FullName == "xl/styles.xml"),
            "The Gantt worksheet must carry workbook styles for visual bars and markers.");

        var ganttXml = LoadXml(archive, "xl/worksheets/sheet7.xml");
        TestAssert.Contains("/xl/worksheets/sheet7.xml", LoadXml(archive, "[Content_Types].xml"), "The Gantt worksheet must be declared in the package content types.");
        TestAssert.Contains("styles", LoadXml(archive, "xl/_rels/workbook.xml.rels"), "The workbook relationships must resolve the stylesheet part.");
        var ganttDocument = XDocument.Parse(ganttXml);
        var pane = ganttDocument.Descendants().Single(element => element.Name.LocalName == "pane");
        TestAssert.Equal("15", (string?)pane.Attribute("xSplit"), "The Gantt sheet must freeze the identity columns before the date axis.");
        TestAssert.Equal("5", (string?)pane.Attribute("ySplit"), "The Gantt sheet must freeze its title, metadata, legend, and header rows.");
        var header = ganttDocument
            .Descendants()
            .Single(element => element.Name.LocalName == "row" && (string?)element.Attribute("r") == "5")
            .Descendants()
            .Where(element => element.Name.LocalName == "t")
            .Select(element => element.Value)
            .Take(15)
            .ToArray();
        TestAssert.Equal(
            "Level|Type|ID|Name|Lane|Status|Owner / Role|Plan Start|Plan Finish|Actual Start|Actual Finish|Recorded %|Critical|Evidence|Source Reference",
            string.Join('|', header),
            "The Gantt sheet must expose stable identity and evidence columns before daily dates.");
        var dataRows = ganttDocument.Descendants().Where(element => element.Name.LocalName == "row").ToArray();
        var planRow = dataRows.Single(row => HasText(row, "P04-A") && HasText(row, "PLAN"));
        var actualRow = dataRows.Single(row => HasText(row, "P04-A") && HasText(row, "ACTUAL"));
        TestAssert.Contains("s=\"4\"", planRow.ToString(), "PLAN cells must use the plan fill style.");
        TestAssert.Contains("s=\"5\"", actualRow.ToString(), "ACTUAL cells must use the actual fill style.");
        TestAssert.True(dataRows.Any(row => HasText(row, "G-D0") && HasText(row, "MILESTONE")), "Milestones must remain zero-duration markers in the Gantt hierarchy.");
        TestAssert.False(dataRows.Any(row => HasText(row, "P01-A") && HasText(row, "ACTUAL")), "Planning-only cards must not receive fabricated ACTUAL rows.");
        TestAssert.Contains("2026-09-18", ganttXml, "The Gantt axis must expose the first baseline day.");
        TestAssert.Contains("2026-09-19", ganttXml, "The Gantt axis must expose adjacent daily columns.");
        TestAssert.Contains("PLAN", ganttXml, "The Gantt worksheet must label the immutable plan lane.");
        TestAssert.Contains("ACTUAL", ganttXml, "The Gantt worksheet must label recorded actual evidence.");
        TestAssert.Contains("ALERT", ganttXml, "The Gantt worksheet must label derived alert lanes.");
        TestAssert.Contains("P04-A", ganttXml, "The Gantt worksheet must preserve delivery-card IDs.");
        TestAssert.Contains("Not recorded", ganttXml, "Missing effort evidence must not become a fabricated percentage.");
        TestAssert.Contains("FF", LoadXml(archive, "xl/styles.xml"), "Gantt styles must contain explicit fill colors.");
    }

    private static bool HasText(XElement row, string value) => row
        .Descendants()
        .Any(element => element.Name.LocalName == "t" && element.Value == value);

    private static string LoadXml(ZipArchive archive, string entryName)
    {
        using var stream = archive.GetEntry(entryName)!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static ManifestImportResult Import(ManifestImportClassification classification, ManifestSnapshotMetadata metadata) => new()
    {
        Classification = classification,
        Snapshot = new IdeaEngineeringSnapshot
        {
            Project = new CanonicalProject { ImportMetadata = metadata },
            Metadata = metadata
        }
    };

    private static CompilationResult Result(ManifestSnapshotMetadata metadata) => new()
    {
        Project = new CanonicalProject { ImportMetadata = metadata },
        Analysis = new ManagementAnalysis { AsOfDate = new DateOnly(2026, 9, 28) }
    };

    private static ManifestSnapshotMetadata Metadata(ManifestImportClassification classification, string snapshotId) => new()
    {
        Classification = classification,
        SnapshotId = snapshotId,
        SourceIdentity = snapshotId
    };

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
