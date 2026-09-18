using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;

namespace ProjectManagementCompiler.Outputs;

public sealed class CarioXlsxExporter
{
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeDocumentRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";

    private static readonly string[] SheetNames =
    [
        "01_TASKS",
        "02_ASSIGNMENTS",
        "03_CHILDREN_MILESTONES",
        "04_DEPENDENCIES",
        "05_PROJECT_INFO",
        "06_IMPORT_WARNINGS"
    ];

    public byte[] Export(CarioWorkbookModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", WriteContentTypes);
            WriteEntry(archive, "_rels/.rels", WriteRootRelationships);
            WriteEntry(archive, "xl/workbook.xml", WriteWorkbook);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WriteWorkbookRelationships);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", writer => WriteTasks(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet2.xml", writer => WriteAssignments(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet3.xml", writer => WriteChildrenMilestones(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet4.xml", writer => WriteDependencies(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet5.xml", writer => WriteProjectInfo(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet6.xml", writer => WriteWarnings(writer, model));
        }

        return output.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string name, Action<XmlWriter> write)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        entry.LastWriteTime = new DateTimeOffset(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
        using var stream = entry.Open();
        using var writer = XmlWriter.Create(stream, new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            OmitXmlDeclaration = false,
            Indent = false
        });
        writer.WriteStartDocument();
        write(writer);
        writer.WriteEndDocument();
    }

    private static void WriteContentTypes(XmlWriter writer)
    {
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteDefault(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteDefault(writer, "xml", "application/xml");
        WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        for (var index = 1; index <= SheetNames.Length; index++)
        {
            WriteOverride(writer, $"/xl/worksheets/sheet{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        }

        writer.WriteEndElement();
    }

    private static void WriteDefault(XmlWriter writer, string extension, string contentType)
    {
        writer.WriteStartElement("Default", ContentTypesNamespace);
        writer.WriteAttributeString("Extension", extension);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteOverride(XmlWriter writer, string partName, string contentType)
    {
        writer.WriteStartElement("Override", ContentTypesNamespace);
        writer.WriteAttributeString("PartName", partName);
        writer.WriteAttributeString("ContentType", contentType);
        writer.WriteEndElement();
    }

    private static void WriteRootRelationships(XmlWriter writer)
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
        writer.WriteAttributeString("Id", "rId1");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
        writer.WriteAttributeString("Target", "xl/workbook.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorkbook(XmlWriter writer)
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteStartAttribute("xmlns", "r", "http://www.w3.org/2000/xmlns/");
        writer.WriteString(OfficeDocumentRelationshipsNamespace);
        writer.WriteEndAttribute();
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        for (var index = 0; index < SheetNames.Length; index++)
        {
            writer.WriteStartElement("sheet", SpreadsheetNamespace);
            writer.WriteAttributeString("name", SheetNames[index]);
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", OfficeDocumentRelationshipsNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorkbookRelationships(XmlWriter writer)
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        for (var index = 0; index < SheetNames.Length; index++)
        {
            writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
            writer.WriteAttributeString("Id", $"rId{index + 1}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{index + 1}.xml");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteTasks(XmlWriter writer, CarioWorkbookModel model)
    {
        WriteWorksheet(writer, ["Work Item Type", "Task ID", "Phase", "Work Package", "Nội dung công việc", "Ngày bắt đầu dự kiến", "Deadline", "Mức độ ưu tiên", "Đơn vị / Phòng ban", "Ban", "Ghi chú", "Trạng thái ban đầu", "Planned Effort (hours)", "Baseline / Analysis State", "Source Reference"],
            model.Tasks.Select(task => new[]
            {
                Cell.Text(task.WorkItemType),
                Cell.Text(task.TaskId),
                Cell.Text(task.PhaseId),
                Cell.Text(task.WorkPackageId),
                Cell.Text(task.Title),
                Cell.Text(FormatDate(task.PlannedStart)),
                Cell.Text(FormatDate(task.PlannedDeadline)),
                Cell.Text(task.Priority),
                Cell.Text(task.Department),
                Cell.Text(task.Team),
                Cell.Text(task.Notes),
                Cell.Text(task.InitialState),
                Cell.Number(task.PlannedEffortHours),
                Cell.Text(task.BaselineAnalysisState),
                Cell.Text(task.SourceReference)
            }));
    }

    private static void WriteAssignments(XmlWriter writer, CarioWorkbookModel model)
    {
        WriteWorksheet(writer, ["Task ID", "Project Logical Role", "CARIO Person / Account", "CARIO Role", "Mapping Status", "Source Reference"],
            model.Assignments.Select(assignment => new[]
            {
                Cell.Text(assignment.TaskId),
                Cell.Text(assignment.LogicalRole),
                Cell.Text(assignment.ConcreteIdentity),
                Cell.Text(assignment.CarioRoleCode),
                Cell.Text(assignment.MappingStatus),
                Cell.Text(assignment.SourceReference)
            }));
    }

    private static void WriteChildrenMilestones(XmlWriter writer, CarioWorkbookModel model)
    {
        WriteWorksheet(writer, ["Parent ID", "Child ID", "Relationship Type", "Child Type", "Name", "Planned Date / Deadline", "Source Reference"],
            model.ChildrenMilestones.Select(record => new[]
            {
                Cell.Text(record.ParentId),
                Cell.Text(record.RecordId),
                Cell.Text(record.Relationship),
                Cell.Text(record.RecordType),
                Cell.Text(record.Name),
                Cell.Text(FormatDate(record.PlannedDate)),
                Cell.Text(record.SourceReference)
            }));
    }

    private static void WriteDependencies(XmlWriter writer, CarioWorkbookModel model)
    {
        WriteWorksheet(writer, ["Task / Milestone ID", "Depends On", "Dependency Type", "Analysis Eligibility", "Validation State", "Source Reference"],
            model.Dependencies.Select(dependency => new[]
            {
                Cell.Text(dependency.SubjectId),
                Cell.Text(dependency.PredecessorId),
                Cell.Text(FormatEnum(dependency.DependencyType)),
                Cell.Text(dependency.AnalysisEligible ? "TRUE" : "FALSE"),
                Cell.Text(FormatEnum(dependency.ValidationState)),
                Cell.Text(dependency.SourceReference)
            }));
    }

    private static void WriteProjectInfo(XmlWriter writer, CarioWorkbookModel model)
    {
        WriteWorksheet(writer, ["Field", "Value", "Data State", "Source Reference"],
            model.ProjectInfo.Select(info => new[]
            {
                Cell.Text(info.Key),
                Cell.Text(info.Value),
                Cell.Text(FormatEnum(info.DataState)),
                Cell.Text(info.SourceReference)
            }));
    }

    private static void WriteWarnings(XmlWriter writer, CarioWorkbookModel model)
    {
        WriteWorksheet(writer, ["Warning ID", "Severity", "Code", "Message", "Affected Item IDs", "Source Reference"],
            model.Warnings.Select(warning => new[]
            {
                Cell.Text(warning.Id),
                Cell.Text(warning.Severity.ToString()),
                Cell.Text(warning.Code),
                Cell.Text(warning.Message),
                Cell.Text(string.Join(",", warning.AffectedIds)),
                Cell.Text(string.Join(";", warning.SourceReferences.Select(FormatSourceReference)))
            }));
    }

    private static void WriteWorksheet(XmlWriter writer, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<Cell>> rows)
    {
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        var rowNumber = 1;
        WriteRow(writer, rowNumber++, headers.Select(Cell.Text).ToArray());
        foreach (var row in rows)
        {
            WriteRow(writer, rowNumber++, row);
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, int rowNumber, IReadOnlyList<Cell> cells)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        for (var index = 0; index < cells.Count; index++)
        {
            var cell = cells[index];
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", $"{ColumnName(index + 1)}{rowNumber}");
            if (cell.IsNumber)
            {
                writer.WriteAttributeString("t", "n");
                writer.WriteStartElement("v", SpreadsheetNamespace);
                writer.WriteString(cell.Value ?? string.Empty);
                writer.WriteEndElement();
            }
            else
            {
                writer.WriteAttributeString("t", "inlineStr");
                writer.WriteStartElement("is", SpreadsheetNamespace);
                writer.WriteStartElement("t", SpreadsheetNamespace);
                if (!string.IsNullOrEmpty(cell.Value))
                {
                    writer.WriteString(cell.Value);
                }

                writer.WriteEndElement();
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static string ColumnName(int column)
    {
        var value = column;
        var name = new StringBuilder();
        while (value > 0)
        {
            value--;
            name.Insert(0, (char)('A' + value % 26));
            value /= 26;
        }

        return name.ToString();
    }

    private static string FormatDate(DateOnly? date) => date is null || date == DateOnly.MinValue
        ? string.Empty
        : date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string FormatEnum<TEnum>(TEnum value)
        where TEnum : struct, Enum =>
        JsonNamingPolicy.SnakeCaseUpper.ConvertName(value.ToString()) ?? value.ToString().ToUpperInvariant();

    private static string FormatSourceReference(Domain.SourceReference reference)
    {
        var suffix = string.Join("#", new[] { reference.Section, reference.Table, reference.Item }.Where(value => !string.IsNullOrWhiteSpace(value)));
        return string.IsNullOrWhiteSpace(suffix)
            ? $"{reference.SourceId}:{reference.RelativeFile}"
            : $"{reference.SourceId}:{reference.RelativeFile}#{suffix}";
    }

    private readonly record struct Cell(string? Value, bool IsNumber)
    {
        public static Cell Text(string? value) => new(value ?? string.Empty, false);

        public static Cell Number(decimal? value) => value is null
            ? Text(string.Empty)
            : new(value.Value.ToString("0.####################", CultureInfo.InvariantCulture), true);
    }
}
