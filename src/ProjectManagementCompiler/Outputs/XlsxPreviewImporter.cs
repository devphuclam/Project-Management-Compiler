using System.Globalization;
using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace ProjectManagementCompiler.Outputs;

public sealed class XlsxPreviewImporter
{
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string RelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string OfficeDocumentRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string ExpectedExportKind = "CARIO_GANTT";
    private const string ExpectedContractVersion = "1.0";
    private const int MaximumHardRowsPerSheet = 5_000;
    private const int MaximumHardEntryCount = 64;
    private const long MaximumHardUploadBytes = 8 * 1024 * 1024;
    private const long MaximumHardPackageBytes = 8 * 1024 * 1024;
    private const long MaximumHardEntryBytes = 4 * 1024 * 1024;
    private static readonly Regex PercentPattern = new(
        "^(?:Not recorded|(?:100(?:\\.0+)?|(?:\\d{1,2}(?:\\.\\d+)?))%)$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] ExpectedSheets =
    [
        "01_TASKS",
        "02_ASSIGNMENTS",
        "03_CHILDREN_MILESTONES",
        "04_DEPENDENCIES",
        "05_PROJECT_INFO",
        "06_IMPORT_WARNINGS",
        "07_GANTT"
    ];
    private static readonly string[] RequiredTaskHeaders =
    [
        "Work Item Type", "Task ID", "Phase", "Work Package", "Nội dung công việc",
        "Ngày bắt đầu dự kiến", "Deadline", "Mức độ ưu tiên", "Đơn vị / Phòng ban", "Ban",
        "Ghi chú", "Trạng thái ban đầu", "Planned Effort (hours)", "Baseline / Analysis State",
        "Source Reference"
    ];
    private static readonly string[] RequiredProjectInfoHeaders =
    [
        "Field", "Value", "Data State", "Source Reference"
    ];
    private static readonly string[] RequiredGanttHeaders =
    [
        "Level", "Type", "ID", "Name", "Lane", "Status", "Owner / Role", "Plan Start",
        "Plan Finish", "Actual Start", "Actual Finish", "Recorded %", "Critical", "Evidence",
        "Source Reference"
    ];
    private static readonly HashSet<string> AllowedPackageParts =
    [
        "[Content_Types].xml",
        "_rels/.rels",
        "xl/workbook.xml",
        "xl/_rels/workbook.xml.rels",
        "xl/styles.xml",
        "xl/worksheets/sheet1.xml",
        "xl/worksheets/sheet2.xml",
        "xl/worksheets/sheet3.xml",
        "xl/worksheets/sheet4.xml",
        "xl/worksheets/sheet5.xml",
        "xl/worksheets/sheet6.xml",
        "xl/worksheets/sheet7.xml"
    ];

    private readonly XlsxPreviewImportLimits limits;

    public XlsxPreviewImporter()
        : this(null)
    {
    }

    public XlsxPreviewImporter(XlsxPreviewImportLimits? requestedLimits)
    {
        requestedLimits ??= new XlsxPreviewImportLimits();
        limits = new XlsxPreviewImportLimits
        {
            MaxUploadBytes = Clamp(requestedLimits.MaxUploadBytes, MaximumHardUploadBytes),
            MaxPackageBytes = Clamp(requestedLimits.MaxPackageBytes, MaximumHardPackageBytes),
            MaxEntryBytes = Clamp(requestedLimits.MaxEntryBytes, MaximumHardEntryBytes),
            MaxEntryCount = Clamp(requestedLimits.MaxEntryCount, MaximumHardEntryCount),
            MaxRowsPerSheet = Clamp(requestedLimits.MaxRowsPerSheet, MaximumHardRowsPerSheet)
        };
    }

    public XlsxPreviewImportResult Import(string fileName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(fileName) || !fileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return Fail("PMC-XLSX-FILENAME", "The selected file must use the .xlsx extension.");
        }

        if (bytes is null || bytes.Length == 0)
        {
            return Fail("PMC-XLSX-PACKAGE", "The selected workbook is empty.");
        }

        if (bytes.LongLength > limits.MaxUploadBytes)
        {
            return Fail("PMC-XLSX-SIZE", $"The workbook exceeds the {limits.MaxUploadBytes} byte upload ceiling.");
        }

        try
        {
            using var archive = new ZipArchive(new MemoryStream(bytes, writable: false), ZipArchiveMode.Read, leaveOpen: false);
            ValidatePackageEntries(archive);
            var package = ReadPackage(archive);
            var sheets = ReadWorkbook(package);
            var projectInfo = ParseProjectInfo(sheets["05_PROJECT_INFO"]);
            var tasks = ParseTasks(sheets["01_TASKS"]);
            var gantt = ParseGantt(sheets["07_GANTT"], tasks.TaskIds);
            var preview = new XlsxPreviewModel
            {
                FileName = fileName,
                ExportKind = projectInfo.ExportKind,
                ContractVersion = projectInfo.ContractVersion,
                ProjectId = projectInfo.ProjectId,
                ProjectName = projectInfo.ProjectName,
                SourceIdentity = gantt.SourceIdentity,
                SnapshotId = gantt.SnapshotId,
                AsOfDate = gantt.AsOfDate,
                DateAxis = gantt.DateAxis,
                Tasks = tasks.Rows,
                GanttRows = gantt.Rows
            };
            return new XlsxPreviewImportResult { Preview = preview };
        }
        catch (XlsxPreviewImportException exception)
        {
            return Fail(exception.Code, exception.Message);
        }
        catch (Exception exception) when (exception is InvalidDataException
            or IOException
            or XmlException
            or InvalidOperationException
            or ArgumentException
            or FormatException
            or OverflowException)
        {
            return Fail("PMC-XLSX-PACKAGE", "The workbook package is malformed or cannot be read safely.");
        }
    }

    private void ValidatePackageEntries(ZipArchive archive)
    {
        if (archive.Entries.Count > limits.MaxEntryCount)
        {
            throw Invalid("PMC-XLSX-SIZE", "The workbook contains more package entries than the application ceiling.");
        }

        long totalBytes = 0;
        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            ValidateEntryName(entry.FullName);
            if (!seenNames.Add(entry.FullName))
            {
                throw Invalid("PMC-XLSX-PACKAGE", $"Package part '{entry.FullName}' is duplicated.");
            }

            if (!AllowedPackageParts.Contains(entry.FullName))
            {
                throw Invalid("PMC-XLSX-PACKAGE", $"Unsupported package part '{entry.FullName}'.");
            }

            if (IsSymlink(entry))
            {
                throw Invalid("PMC-XLSX-PACKAGE", $"Symlink package entry '{entry.FullName}' is not supported.");
            }

            if (entry.Length < 0 || entry.Length > limits.MaxEntryBytes)
            {
                throw Invalid("PMC-XLSX-SIZE", $"Package entry '{entry.FullName}' exceeds the per-entry ceiling.");
            }

            try
            {
                totalBytes = checked(totalBytes + entry.Length);
            }
            catch (OverflowException)
            {
                throw Invalid("PMC-XLSX-SIZE", "The workbook aggregate package size is not representable.");
            }

            if (totalBytes > limits.MaxPackageBytes)
            {
                throw Invalid("PMC-XLSX-SIZE", "The workbook exceeds the aggregate package-size ceiling.");
            }
        }

        var names = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.Ordinal);
        foreach (var required in AllowedPackageParts)
        {
            if (!names.Contains(required))
            {
                throw Invalid("PMC-XLSX-PACKAGE", $"Required package part '{required}' is missing.");
            }
        }
    }

    private PackageParts ReadPackage(ZipArchive archive)
    {
        var values = new Dictionary<string, XDocument>(StringComparer.Ordinal);
        foreach (var entry in archive.Entries)
        {
            if (entry.FullName is "xl/styles.xml")
            {
                _ = ReadEntry(entry);
                continue;
            }

            values[entry.FullName] = LoadXml(ReadEntry(entry), entry.FullName);
        }

        ValidateContentTypes(values["[Content_Types].xml"]);
        ValidateRootRelationships(values["_rels/.rels"]);
        ValidateWorkbookRelationships(values["xl/_rels/workbook.xml.rels"]);
        return new PackageParts(values);
    }

    private Dictionary<string, SheetRows> ReadWorkbook(PackageParts package)
    {
        var workbook = package.Xml["xl/workbook.xml"];
        var relationships = package.Xml["xl/_rels/workbook.xml.rels"];
        var relationTargets = relationships
            .Descendants(XName.Get("Relationship", RelationshipsNamespace))
            .ToDictionary(
                relation => (string?)relation.Attribute("Id") ?? string.Empty,
                relation => ResolveWorkbookTarget((string?)relation.Attribute("Target")),
                StringComparer.Ordinal);
        var sheets = workbook
            .Descendants(XName.Get("sheet", SpreadsheetNamespace))
            .Select(sheet => new
            {
                Name = (string?)sheet.Attribute("name") ?? string.Empty,
                RelationId = (string?)sheet.Attribute(XName.Get("id", OfficeDocumentRelationshipsNamespace)) ?? string.Empty
            })
            .ToArray();
        if (!sheets.Select(sheet => sheet.Name).SequenceEqual(ExpectedSheets, StringComparer.Ordinal))
        {
            throw Invalid("PMC-XLSX-SHEET", "The workbook must contain exactly the seven supported worksheets in order.");
        }

        var result = new Dictionary<string, SheetRows>(StringComparer.Ordinal);
        foreach (var sheet in sheets)
        {
            if (!relationTargets.TryGetValue(sheet.RelationId, out var target)
                || target is null
                || !package.Xml.TryGetValue(target, out var document))
            {
                throw Invalid("PMC-XLSX-SHEET", $"Worksheet '{sheet.Name}' has no supported relationship target.");
            }

            result[sheet.Name] = ReadRows(document, sheet.Name);
        }

        return result;
    }

    private ProjectInfoValues ParseProjectInfo(SheetRows sheet)
    {
        RequireHeaders(sheet, RequiredProjectInfoHeaders, "05_PROJECT_INFO");
        var values = sheet.Rows.Skip(1)
            .Where(row => !string.IsNullOrWhiteSpace(OptionalCell(row, 0)))
            .GroupBy(row => OptionalCell(row, 0), StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);
        var exportKind = RequiredMarker(values, "PMC_EXPORT_KIND");
        var contractVersion = RequiredMarker(values, "PMC_EXPORT_CONTRACT_VERSION");
        var projectId = RequiredMarker(values, "PMC_PROJECT_ID");
        var projectName = RequiredMarker(values, "PMC_PROJECT_NAME");
        if (!string.Equals(exportKind, ExpectedExportKind, StringComparison.Ordinal)
            || !string.Equals(contractVersion, ExpectedContractVersion, StringComparison.Ordinal))
        {
            throw Invalid("PMC-XLSX-MARKER", "The workbook provenance marker is missing or unsupported.");
        }

        var legacyProjectId = OptionalValue(values, "Project ID");
        var legacyProjectName = OptionalValue(values, "Project name");
        if ((!string.IsNullOrWhiteSpace(legacyProjectId) && !string.Equals(projectId, legacyProjectId, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(legacyProjectName) && !string.Equals(projectName, legacyProjectName, StringComparison.Ordinal)))
        {
            throw Invalid("PMC-XLSX-MARKER", "The compiler project markers conflict with the exported project identity.");
        }

        return new ProjectInfoValues(exportKind, contractVersion, projectId, projectName);
    }

    private TaskValues ParseTasks(SheetRows sheet)
    {
        RequireHeaders(sheet, RequiredTaskHeaders, "01_TASKS");
        var rows = new List<XlsxPreviewTaskRow>();
        var taskIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in DataRows(sheet))
        {
            var taskId = RequiredCell(row, 1, "PMC-XLSX-IDENTITY", "01_TASKS Task ID");
            if (!taskIds.Add(taskId))
            {
                throw Invalid("PMC-XLSX-IDENTITY", $"Task identity '{taskId}' is duplicated.");
            }

            var plannedStart = OptionalDate(row, 5, "01_TASKS planned start");
            var deadline = OptionalDate(row, 6, "01_TASKS deadline");
            var effort = OptionalDecimal(row, 12, "01_TASKS planned effort");
            rows.Add(new XlsxPreviewTaskRow
            {
                WorkItemType = RequiredCell(row, 0, "PMC-XLSX-VALUE", "01_TASKS Work Item Type"),
                TaskId = taskId,
                Phase = OptionalCell(row, 2),
                WorkPackage = OptionalCell(row, 3),
                Title = RequiredCell(row, 4, "PMC-XLSX-VALUE", "01_TASKS title"),
                PlannedStart = plannedStart,
                Deadline = deadline,
                Priority = OptionalCell(row, 7),
                Department = OptionalCell(row, 8),
                Team = OptionalCell(row, 9),
                Notes = OptionalCell(row, 10),
                InitialState = OptionalCell(row, 11),
                PlannedEffortHours = effort,
                BaselineAnalysisState = OptionalCell(row, 13),
                SourceReference = OptionalCell(row, 14)
            });
        }

        if (rows.Count == 0)
        {
            throw Invalid("PMC-XLSX-VALUE", "01_TASKS must contain at least one exported task row.");
        }

        return new TaskValues(rows, taskIds);
    }

    private GanttValues ParseGantt(SheetRows sheet, IReadOnlySet<string> taskIds)
    {
        if (sheet.Rows.Count < 6)
        {
            throw Invalid("PMC-XLSX-VALUE", "07_GANTT must contain a header and at least one exported row.");
        }

        var metadata = sheet.Rows[1];
        var asOfText = OptionalCell(metadata, 1);
        DateOnly? asOf = string.IsNullOrWhiteSpace(asOfText)
            ? null
            : ParseDate(asOfText, "07_GANTT as-of date");
        var sourceIdentity = OptionalCell(metadata, 5);
        var snapshotId = OptionalCell(metadata, 7);
        RequireHeaders(sheet with { Rows = [sheet.Rows[4]] }, RequiredGanttHeaders, "07_GANTT");
        var dateAxis = OrderedValues(sheet.Rows[4])
            .Skip(15)
            .Select(value => ParseDate(value, "07_GANTT daily axis"))
            .ToArray();
        if (dateAxis.Length == 0)
        {
            throw Invalid("PMC-XLSX-VALUE", "07_GANTT must contain at least one daily date column.");
        }

        for (var index = 1; index < dateAxis.Length; index++)
        {
            if (dateAxis[index] != dateAxis[index - 1].AddDays(1))
            {
                throw Invalid("PMC-XLSX-VALUE", "07_GANTT daily date columns must be unique and contiguous.");
            }
        }

        var rows = new List<XlsxPreviewGanttRow>();
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in sheet.Rows.Skip(5))
        {
            if (row.Values.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var id = RequiredCell(row, 2, "PMC-XLSX-IDENTITY", "07_GANTT ID");
            var lane = RequiredCell(row, 4, "PMC-XLSX-VALUE", "07_GANTT Lane");
            var type = RequiredCell(row, 1, "PMC-XLSX-VALUE", "07_GANTT Type");
            if (lane is not ("PLAN" or "ACTUAL" or "ALERT" or "MILESTONE"))
            {
                throw Invalid("PMC-XLSX-VALUE", $"Gantt row '{id}' has unsupported lane '{lane}'.");
            }

            if (!identities.Add(type + "|" + id + "|" + lane))
            {
                throw Invalid("PMC-XLSX-IDENTITY", $"Gantt identity '{type}:{id}' is duplicated in lane '{lane}'.");
            }

            if (type is "DeliveryCard" or "Milestone")
            {
                if (!taskIds.Contains(id))
                {
                    throw Invalid("PMC-XLSX-IDENTITY", $"Gantt row '{id}' does not have a matching exported task.");
                }
            }

            var levelText = RequiredCell(row, 0, "PMC-XLSX-VALUE", "07_GANTT Level");
            if (!int.TryParse(levelText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level) || level < 0)
            {
                throw Invalid("PMC-XLSX-VALUE", $"Gantt row '{id}' has an invalid hierarchy level.");
            }

            var planStart = OptionalDate(row, 7, "07_GANTT plan start");
            var planFinish = OptionalDate(row, 8, "07_GANTT plan finish");
            var actualStart = OptionalDate(row, 9, "07_GANTT actual start");
            var actualFinish = OptionalDate(row, 10, "07_GANTT actual finish");
            var recordedPercent = OptionalCell(row, 11);
            if (!string.IsNullOrWhiteSpace(recordedPercent) && !PercentPattern.IsMatch(recordedPercent))
            {
                throw Invalid("PMC-XLSX-VALUE", $"Gantt row '{id}' has an invalid Recorded % value.");
            }

            rows.Add(new XlsxPreviewGanttRow
            {
                Level = level,
                Type = type,
                Id = id,
                Name = RequiredCell(row, 3, "PMC-XLSX-VALUE", "07_GANTT Name"),
                Lane = lane,
                Status = OptionalCell(row, 5),
                OwnerRole = OptionalCell(row, 6),
                PlanStart = planStart,
                PlanFinish = planFinish,
                ActualStart = actualStart,
                ActualFinish = actualFinish,
                RecordedPercent = recordedPercent,
                Critical = OptionalCell(row, 12),
                Evidence = OptionalCell(row, 13),
                SourceReference = OptionalCell(row, 14),
                DailyCells = Enumerable.Range(15, dateAxis.Length).Select(index => OptionalCell(row, index)).ToArray()
            });
        }

        if (rows.Count == 0)
        {
            throw Invalid("PMC-XLSX-VALUE", "07_GANTT must contain at least one exported Gantt row.");
        }

        return new GanttValues(rows, dateAxis, asOf, sourceIdentity, snapshotId);
    }

    private void RequireHeaders(SheetRows sheet, IReadOnlyList<string> expected, string sheetName)
    {
        if (sheet.Rows.Count == 0)
        {
            throw Invalid("PMC-XLSX-HEADER", $"{sheetName} has no header row.");
        }

        var actual = OrderedValues(sheet.Rows[0]);
        if (actual.Count < expected.Count || !expected.SequenceEqual(actual.Take(expected.Count), StringComparer.Ordinal))
        {
            throw Invalid("PMC-XLSX-HEADER", $"{sheetName} does not match the supported compiler header contract.");
        }
    }

    private static IReadOnlyList<string> OrderedValues(Dictionary<int, string> row) =>
        row.OrderBy(cell => cell.Key).Select(cell => cell.Value).ToArray();

    private static IEnumerable<Dictionary<int, string>> DataRows(SheetRows sheet) =>
        sheet.Rows.Skip(1).Where(row => row.Values.Any(value => !string.IsNullOrWhiteSpace(value)));

    private static string RequiredMarker(Dictionary<string, Dictionary<int, string>[]> values, string key)
    {
        if (!values.TryGetValue(key, out var rows) || rows.Length != 1)
        {
            throw Invalid("PMC-XLSX-MARKER", $"Workbook must contain exactly one '{key}' marker row.");
        }

        var value = OptionalCell(rows[0], 1);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid("PMC-XLSX-MARKER", $"Workbook marker '{key}' must not be blank.");
        }

        return value;
    }

    private static string? OptionalValue(Dictionary<string, Dictionary<int, string>[]> values, string key)
    {
        return values.TryGetValue(key, out var rows)
            ? rows.Length == 1 ? OptionalCell(rows[0], 1) : throw Invalid("PMC-XLSX-MARKER", $"Project info field '{key}' is duplicated.")
            : null;
    }

    private static string RequiredCell(Dictionary<int, string> row, int column, string code, string label)
    {
        var value = OptionalCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw Invalid(code, $"{label} must not be blank.");
        }

        return value;
    }

    private static string OptionalCell(Dictionary<int, string> row, int column) =>
        row.TryGetValue(column, out var value) ? value.Trim() : string.Empty;

    private static string OptionalDate(Dictionary<int, string> row, int column, string label)
    {
        var value = OptionalCell(row, column);
        return string.IsNullOrWhiteSpace(value) ? string.Empty : ParseDate(value, label).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private static DateOnly ParseDate(string value, string label)
    {
        if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw Invalid("PMC-XLSX-VALUE", $"{label} must be an ISO yyyy-MM-dd date.");
        }

        return date;
    }

    private static decimal? OptionalDecimal(Dictionary<int, string> row, int column, string label)
    {
        var value = OptionalCell(row, column);
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var decimalValue))
        {
            throw Invalid("PMC-XLSX-VALUE", $"{label} must be an invariant numeric value.");
        }

        return decimalValue;
    }

    private SheetRows ReadRows(XDocument document, string sheetName)
    {
        var rows = document.Descendants(XName.Get("row", SpreadsheetNamespace))
            .Select(row => row.Descendants(XName.Get("c", SpreadsheetNamespace))
                .ToDictionary(
                    cell => ColumnIndex((string?)cell.Attribute("r") ?? string.Empty),
                    ReadCellValue))
            .ToArray();
        if (rows.Length > limits.MaxRowsPerSheet)
        {
            throw Invalid("PMC-XLSX-SIZE", $"Worksheet '{sheetName}' exceeds the row ceiling.");
        }

        return new SheetRows(rows);
    }

    private static string ReadCellValue(XElement cell)
    {
        if (cell.Element(XName.Get("f", SpreadsheetNamespace)) is not null)
        {
            throw Invalid("PMC-XLSX-VALUE", "Formula cells are not supported in the compiler preview contract.");
        }

        var type = (string?)cell.Attribute("t");
        if (type is "shared" or "str" or "e")
        {
            throw Invalid("PMC-XLSX-VALUE", $"Cell type '{type}' is not supported by the bounded compiler preview parser.");
        }

        return type == "inlineStr"
            ? string.Concat(cell.Descendants(XName.Get("t", SpreadsheetNamespace)).Select(text => text.Value))
            : cell.Element(XName.Get("v", SpreadsheetNamespace))?.Value ?? string.Empty;
    }

    private static int ColumnIndex(string cellReference)
    {
        var letters = new string(cellReference.TakeWhile(char.IsLetter).ToArray()).ToUpperInvariant();
        if (letters.Length == 0)
        {
            throw Invalid("PMC-XLSX-VALUE", "A worksheet cell has no valid column reference.");
        }

        var value = 0;
        foreach (var letter in letters)
        {
            value = checked(value * 26 + letter - 'A' + 1);
        }

        return value - 1;
    }

    private static void ValidateContentTypes(XDocument document)
    {
        var overrides = document.Descendants(XName.Get("Override", ContentTypesNamespace))
            .Select(element => (string?)element.Attribute("PartName"))
            .Where(value => value is not null)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var required in ExpectedSheets.Select((_, index) => $"/xl/worksheets/sheet{index + 1}.xml"))
        {
            if (!overrides.Contains(required))
            {
                throw Invalid("PMC-XLSX-PACKAGE", $"Content types do not declare '{required}'.");
            }
        }
    }

    private static void ValidateRootRelationships(XDocument document)
    {
        var relations = document.Descendants(XName.Get("Relationship", RelationshipsNamespace)).ToArray();
        if (relations.Length != 1
            || relations[0].Attribute("TargetMode") is not null
            || (string?)relations[0].Attribute("Type") != "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"
            || (string?)relations[0].Attribute("Target") != "xl/workbook.xml")
        {
            throw Invalid("PMC-XLSX-RELATIONSHIP", "The workbook root relationship graph is unsupported.");
        }
    }

    private static void ValidateWorkbookRelationships(XDocument document)
    {
        foreach (var relation in document.Descendants(XName.Get("Relationship", RelationshipsNamespace)))
        {
            if (relation.Attribute("TargetMode") is not null)
            {
                throw Invalid("PMC-XLSX-RELATIONSHIP", "External workbook relationships are not supported.");
            }

            var type = (string?)relation.Attribute("Type") ?? string.Empty;
            if (!type.EndsWith("/worksheet", StringComparison.Ordinal)
                && !type.EndsWith("/styles", StringComparison.Ordinal))
            {
                throw Invalid("PMC-XLSX-RELATIONSHIP", "The workbook contains an unsupported relationship type.");
            }
        }
    }

    private static string ResolveWorkbookTarget(string? target)
    {
        if (string.IsNullOrWhiteSpace(target)
            || target.Contains("..", StringComparison.Ordinal)
            || target.StartsWith("/", StringComparison.Ordinal)
            || target.Contains('\\', StringComparison.Ordinal))
        {
            throw Invalid("PMC-XLSX-RELATIONSHIP", "The workbook relationship target is unsafe.");
        }

        return "xl/" + target.TrimStart('/');
    }

    private static void ValidateEntryName(string name)
    {
        var normalized = name.Replace('\\', '/');
        if (!string.Equals(name, normalized, StringComparison.Ordinal)
            || normalized.StartsWith("/", StringComparison.Ordinal)
            || normalized.Split('/').Any(part => part is ".." or "."))
        {
            throw Invalid("PMC-XLSX-PACKAGE", $"Package entry '{name}' has an unsafe path.");
        }
    }

    private static bool IsSymlink(ZipArchiveEntry entry)
    {
        var mode = (entry.ExternalAttributes >> 16) & 0xF000;
        return mode == 0xA000;
    }

    private static string ReadEntry(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static XDocument LoadXml(string content, string entryName)
    {
        using var reader = XmlReader.Create(new StringReader(content), new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersFromEntities = 0
        });
        return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
    }

    private static int Clamp(int requested, int ceiling) => requested <= 0 ? ceiling : Math.Min(requested, ceiling);

    private static long Clamp(long requested, long ceiling) => requested <= 0 ? ceiling : Math.Min(requested, ceiling);

    private static XlsxPreviewImportException Invalid(string code, string message) => new(code, message);

    private static XlsxPreviewImportResult Fail(string code, string message) => new()
    {
        Diagnostics =
        [
            new XlsxPreviewDiagnostic
            {
                Code = code,
                Severity = "ERROR",
                Message = message
            }
        ]
    };

    private sealed record PackageParts(IReadOnlyDictionary<string, XDocument> Xml);

    private sealed record SheetRows(IReadOnlyList<Dictionary<int, string>> Rows)
    {
        public Dictionary<int, string> this[int index] => Rows[index];
    }

    private sealed record ProjectInfoValues(string ExportKind, string ContractVersion, string ProjectId, string ProjectName);

    private sealed record TaskValues(IReadOnlyList<XlsxPreviewTaskRow> Rows, IReadOnlySet<string> TaskIds);

    private sealed record GanttValues(
        IReadOnlyList<XlsxPreviewGanttRow> Rows,
        IReadOnlyList<DateOnly> DateAxis,
        DateOnly? AsOfDate,
        string SourceIdentity,
        string SnapshotId);

    private sealed class XlsxPreviewImportException : Exception
    {
        public XlsxPreviewImportException(string code, string message)
            : base(message) => Code = code;

        public string Code { get; }
    }
}
