using System.IO.Compression;
using System.Globalization;
using ProjectManagementCompiler.Outputs;

namespace ProjectManagementCompiler.Tests;

internal static class XlsxPreviewImporterTests
{
    public static void ValidWorkbookPreservesPreviewIdentityTasksAxisAndLanes()
    {
        var result = XlsxPreviewTestFixtures.Import(XlsxPreviewTestFixtures.ValidWorkbookBytes());

        TestAssert.True(result.IsValid, "A fresh compiler workbook must import. Diagnostics: " + result.DiagnosticsText);
        var preview = result.Preview;
        TestAssert.Equal("PMC-PROJECT-001", XlsxPreviewTestFixtures.PreviewImportView.ReadString(preview, "ProjectId"), "Preview must preserve the exported project ID.");
        TestAssert.Equal("Preview Project", XlsxPreviewTestFixtures.PreviewImportView.ReadString(preview, "ProjectName"), "Preview must preserve the exported project name.");
        TestAssert.Equal(2, XlsxPreviewTestFixtures.PreviewImportView.ReadEnumerable(preview, "Tasks").Count(), "Preview must show every exported task row.");
        TestAssert.Equal(
            "2026-09-18,2026-09-19,2026-09-20,2026-09-21",
            string.Join(',', XlsxPreviewTestFixtures.PreviewImportView.ReadEnumerable(preview, "DateAxis").Select(value => value is DateOnly date
                ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : value.ToString())),
            "Preview must preserve the ordered daily axis.");

        var rows = XlsxPreviewTestFixtures.PreviewImportView.ReadEnumerable(preview, "GanttRows").ToArray();
        foreach (var lane in new[] { "PLAN", "ACTUAL", "ALERT", "MILESTONE" })
        {
            TestAssert.True(rows.Any(row => string.Equals(XlsxPreviewTestFixtures.PreviewImportView.ReadString(row, "Lane"), lane, StringComparison.Ordinal)), "Preview must preserve the " + lane + " lane.");
        }

        var plan = rows.Single(row => XlsxPreviewTestFixtures.PreviewImportView.ReadString(row, "Id") == "P01-A"
            && XlsxPreviewTestFixtures.PreviewImportView.ReadString(row, "Lane") == "PLAN");
        TestAssert.Equal("50%", XlsxPreviewTestFixtures.PreviewImportView.ReadString(plan, "RecordedPercent"), "Preview must preserve the exported recorded percentage.");
    }

    public static void InvalidPackagesAndContractMutationsFailClosed()
    {
        var valid = XlsxPreviewTestFixtures.ValidWorkbookBytes();
        var cases = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["corrupt package"] = XlsxPreviewTestFixtures.CorruptPackage(),
            ["missing gantt sheet"] = XlsxPreviewTestFixtures.RemoveEntry(valid, "xl/worksheets/sheet7.xml"),
            ["missing marker"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet5.xml", RemoveMarker(valid, "PMC_EXPORT_KIND")),
            ["duplicate gantt sheet"] = XlsxPreviewTestFixtures.AddEntry(valid, "xl/worksheets/sheet7.xml", ReadEntry(valid, "xl/worksheets/sheet7.xml")),
            ["unsupported marker version"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet5.xml", ReplaceFirst(ReadEntry(valid, "xl/worksheets/sheet5.xml"), "<t>1.0</t>", "<t>9.0</t>")),
            ["conflicting project marker"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet5.xml", ConflictingMarker(valid, "PMC_PROJECT_NAME", "Different Project")),
            ["invalid header"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet1.xml", ReplaceFirst(ReadEntry(valid, "xl/worksheets/sheet1.xml"), "Task ID", "Task Identity")),
            ["invalid task date"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet1.xml", ReplaceFirst(ReadEntry(valid, "xl/worksheets/sheet1.xml"), "2026-09-18", "2026-99-99")),
            ["invalid task effort"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet1.xml", ReplaceFirst(ReadEntry(valid, "xl/worksheets/sheet1.xml"), "<v>8</v>", "<v>not-number</v>")),
            ["external relationship"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/_rels/workbook.xml.rels", ReadEntry(valid, "xl/_rels/workbook.xml.rels").Replace("Target=\"worksheets/sheet1.xml\"", "Target=\"https://example.invalid/sheet1.xml\" TargetMode=\"External\"", StringComparison.Ordinal)),
            ["duplicate task identity"] = XlsxPreviewTestFixtures.ReplaceEntry(valid, "xl/worksheets/sheet1.xml", DuplicateTaskIdentity(ReadEntry(valid, "xl/worksheets/sheet1.xml")))
        };

        foreach (var (name, bytes) in cases)
        {
            var result = XlsxPreviewTestFixtures.Import(bytes);
            TestAssert.False(result.IsValid, name + " must be rejected without a preview.");
            TestAssert.True(result.Read<object>("Preview") is null, name + " must not render partial preview rows.");
            TestAssert.True(result.DiagnosticsText.Contains("PMC-XLSX-", StringComparison.Ordinal), name + " must expose a structured PMC-XLSX diagnostic.");
        }
    }

    public static void OversizedPackageEntryIsRejectedBeforePreview()
    {
        var result = XlsxPreviewTestFixtures.Import(XlsxPreviewTestFixtures.OversizedEntry(XlsxPreviewTestFixtures.ValidWorkbookBytes()));

        TestAssert.False(result.IsValid, "An oversized worksheet entry must fail closed.");
        TestAssert.True(result.DiagnosticsText.Contains("SIZE", StringComparison.OrdinalIgnoreCase), "Oversized package diagnostics must identify the size limit.");
    }

    public static void AggregatePackageAndCallerLimitsCannotBypassHardCeiling()
    {
        var aggregate = XlsxPreviewTestFixtures.ValidWorkbookBytes();
        var large = new string('x', 3 * 1024 * 1024);
        foreach (var entry in new[] { "xl/worksheets/sheet5.xml", "xl/worksheets/sheet6.xml", "xl/worksheets/sheet7.xml" })
        {
            aggregate = XlsxPreviewTestFixtures.ReplaceEntry(aggregate, entry, large);
        }

        var aggregateResult = new XlsxPreviewImporter(new XlsxPreviewImportLimits
        {
            MaxPackageBytes = 64 * 1024 * 1024,
            MaxEntryBytes = 64 * 1024 * 1024,
            MaxUploadBytes = 64 * 1024 * 1024
        }).Import("aggregate.xlsx", aggregate);
        TestAssert.False(aggregateResult.IsValid, "Caller-provided limits must not raise the aggregate application ceiling.");
        TestAssert.True(aggregateResult.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-XLSX-SIZE"), "Aggregate size rejection must be structured.");

        var callerResult = new XlsxPreviewImporter(new XlsxPreviewImportLimits
        {
            MaxEntryBytes = 64 * 1024 * 1024,
            MaxUploadBytes = 64 * 1024 * 1024
        }).Import("oversized.xlsx", XlsxPreviewTestFixtures.OversizedEntry(XlsxPreviewTestFixtures.ValidWorkbookBytes()));
        TestAssert.False(callerResult.IsValid, "Caller-provided entry limits must not raise the hard per-entry ceiling.");
        TestAssert.True(callerResult.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-XLSX-SIZE"), "Per-entry size rejection must be structured.");
    }

    public static void CallerRowLimitCanOnlyTightenTheHardCeiling()
    {
        var result = new XlsxPreviewImporter(new XlsxPreviewImportLimits
        {
            MaxRowsPerSheet = 1
        }).Import("rows.xlsx", XlsxPreviewTestFixtures.ValidWorkbookBytes());

        TestAssert.False(result.IsValid, "A caller-supplied lower row limit must be enforced.");
        TestAssert.True(result.Diagnostics.Any(diagnostic => diagnostic.Code == "PMC-XLSX-SIZE"), "Row-limit rejection must be structured.");
    }

    private static string ReadEntry(byte[] source, string name)
    {
        using var archive = new ZipArchive(new MemoryStream(source), ZipArchiveMode.Read);
        using var reader = new StreamReader(archive.GetEntry(name)!.Open());
        return reader.ReadToEnd();
    }

    private static string RemoveMarker(byte[] source, string marker)
    {
        var document = System.Xml.Linq.XDocument.Parse(ReadEntry(source, "xl/worksheets/sheet5.xml"));
        var row = document.Descendants().FirstOrDefault(row => row.Descendants().Any(cell => cell.Value == marker));
        row?.Remove();
        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static string ReplaceFirst(string source, string oldValue, string newValue) =>
        source.Replace(oldValue, newValue, StringComparison.Ordinal);

    private static string DuplicateTaskIdentity(string source)
    {
        var document = System.Xml.Linq.XDocument.Parse(source);
        var rows = document.Descendants().Where(element => element.Name.LocalName == "row").ToArray();
        var taskRow = rows.First(row => row.Descendants().Any(cell => cell.Value == "P01-A"));
        var duplicate = new System.Xml.Linq.XElement(taskRow);
        duplicate.SetAttributeValue("r", "4");
        document.Root!.Element(document.Root.Name.Namespace + "sheetData")!.Add(duplicate);
        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }

    private static string ConflictingMarker(byte[] source, string marker, string replacement)
    {
        var document = System.Xml.Linq.XDocument.Parse(ReadEntry(source, "xl/worksheets/sheet5.xml"));
        var row = document.Descendants().First(row => row.Descendants().Any(cell => cell.Value == marker));
        var valueCell = row.Descendants().Where(cell => cell.Name.LocalName == "c").Skip(1).First();
        var text = valueCell.Descendants().First(element => element.Name.LocalName == "t");
        text.Value = replacement;
        var duplicate = new System.Xml.Linq.XElement(row);
        duplicate.SetAttributeValue("r", "30");
        document.Root!.Element(document.Root.Name.Namespace + "sheetData")!.Add(duplicate);
        return document.ToString(System.Xml.Linq.SaveOptions.DisableFormatting);
    }
}
