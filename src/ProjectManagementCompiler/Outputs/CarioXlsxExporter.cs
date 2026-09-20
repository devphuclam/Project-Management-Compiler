using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Xml;
using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Outputs;

public sealed class CarioXlsxExporter
{
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeDocumentRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const int GanttDefaultStyle = 0;
    private const int GanttTitleStyle = 1;
    private const int GanttMetaStyle = 2;
    private const int GanttHeaderStyle = 3;
    private const int GanttPlanStyle = 4;
    private const int GanttActualStyle = 5;
    private const int GanttAlertStyle = 6;
    private const int GanttMilestoneStyle = 7;
    private const int GanttWeekendStyle = 8;
    private const int GanttAsOfStyle = 9;

    private static readonly string[] CarioSheetNames =
    [
        "01_TASKS",
        "02_ASSIGNMENTS",
        "03_CHILDREN_MILESTONES",
        "04_DEPENDENCIES",
        "05_PROJECT_INFO",
        "06_IMPORT_WARNINGS"
    ];

    // This overload is the legacy CARIO-only adapter contract. The application
    // export uses ExportWithGantt so the management workbook cannot be mistaken
    // for the six-sheet CARIO-only package.
    public byte[] Export(CarioWorkbookModel model) => ExportInternal(model, null);

    public byte[] ExportWithGantt(CarioWorkbookModel model, GanttXlsxModel gantt)
    {
        ArgumentNullException.ThrowIfNull(gantt);
        return ExportInternal(model, gantt);
    }

    private byte[] ExportInternal(CarioWorkbookModel model, GanttXlsxModel? gantt)
    {
        ArgumentNullException.ThrowIfNull(model);

        var sheetNames = gantt is null
            ? CarioSheetNames
            : CarioSheetNames.Append("07_GANTT").ToArray();

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", writer => WriteContentTypes(writer, sheetNames.Length, gantt is not null));
            WriteEntry(archive, "_rels/.rels", WriteRootRelationships);
            WriteEntry(archive, "xl/workbook.xml", writer => WriteWorkbook(writer, sheetNames));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", writer => WriteWorkbookRelationships(writer, sheetNames.Length, gantt is not null));
            WriteEntry(archive, "xl/worksheets/sheet1.xml", writer => WriteTasks(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet2.xml", writer => WriteAssignments(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet3.xml", writer => WriteChildrenMilestones(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet4.xml", writer => WriteDependencies(writer, model));
            WriteEntry(archive, "xl/worksheets/sheet5.xml", writer => WriteProjectInfo(writer, model, gantt is not null));
            WriteEntry(archive, "xl/worksheets/sheet6.xml", writer => WriteWarnings(writer, model));
            if (gantt is not null)
            {
                WriteEntry(archive, "xl/worksheets/sheet7.xml", writer => WriteGantt(writer, gantt));
                WriteEntry(archive, "xl/styles.xml", WriteStyles);
            }
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

    private static void WriteContentTypes(XmlWriter writer, int sheetCount, bool includeStyles)
    {
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteDefault(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteDefault(writer, "xml", "application/xml");
        WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        for (var index = 1; index <= sheetCount; index++)
        {
            WriteOverride(writer, $"/xl/worksheets/sheet{index}.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml");
        }

        if (includeStyles)
        {
            WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
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

    private static void WriteWorkbook(XmlWriter writer, IReadOnlyList<string> sheetNames)
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteStartAttribute("xmlns", "r", "http://www.w3.org/2000/xmlns/");
        writer.WriteString(OfficeDocumentRelationshipsNamespace);
        writer.WriteEndAttribute();
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        for (var index = 0; index < sheetNames.Count; index++)
        {
            writer.WriteStartElement("sheet", SpreadsheetNamespace);
            writer.WriteAttributeString("name", sheetNames[index]);
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", OfficeDocumentRelationshipsNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorkbookRelationships(XmlWriter writer, int sheetCount, bool includeStyles)
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        for (var index = 0; index < sheetCount; index++)
        {
            writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
            writer.WriteAttributeString("Id", $"rId{index + 1}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{index + 1}.xml");
            writer.WriteEndElement();
        }

        if (includeStyles)
        {
            writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
            writer.WriteAttributeString("Id", $"rId{sheetCount + 1}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
            writer.WriteAttributeString("Target", "styles.xml");
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
        WriteWorksheet(writer, ["Subject Kind", "Subject ID", "Predecessor Kind", "Predecessor ID", "Dependency Type", "Analysis Eligibility", "Validation State", "Source Reference"],
            model.Dependencies.Select(dependency => new[]
            {
                Cell.Text(dependency.SubjectKind),
                Cell.Text(dependency.SubjectId),
                Cell.Text(dependency.PredecessorKind),
                Cell.Text(dependency.PredecessorId),
                Cell.Text(FormatEnum(dependency.DependencyType)),
                Cell.Text(dependency.AnalysisEligible ? "TRUE" : "FALSE"),
                Cell.Text(FormatEnum(dependency.ValidationState)),
                Cell.Text(dependency.SourceReference)
            }));
    }

    private static void WriteProjectInfo(XmlWriter writer, CarioWorkbookModel model, bool includeGanttPreviewMarkers)
    {
        WriteWorksheet(writer, ["Field", "Value", "Data State", "Source Reference"],
            BuildProjectInfoRows(model, includeGanttPreviewMarkers).Select(info => new[]
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

    private static IReadOnlyList<CarioProjectInfoRow> BuildProjectInfoRows(
        CarioWorkbookModel model,
        bool includeGanttPreviewMarkers)
    {
        var rows = model.ProjectInfo
            .Where(info => info.Key is not "PMC_EXPORT_KIND"
                and not "PMC_EXPORT_CONTRACT_VERSION"
                and not "PMC_PROJECT_ID"
                and not "PMC_PROJECT_NAME")
            .ToList();
        if (!includeGanttPreviewMarkers)
        {
            return rows;
        }

        var projectId = model.ProjectInfo
            .FirstOrDefault(info => string.Equals(info.Key, "Project ID", StringComparison.OrdinalIgnoreCase))
            ?.Value;
        rows.Add(new CarioProjectInfoRow
        {
            Key = "PMC_EXPORT_KIND",
            Value = "CARIO_GANTT",
            DataState = DataState.Known,
            SourceReference = "ProjectManagementCompiler"
        });
        rows.Add(new CarioProjectInfoRow
        {
            Key = "PMC_EXPORT_CONTRACT_VERSION",
            Value = "1.0",
            DataState = DataState.Known,
            SourceReference = "ProjectManagementCompiler"
        });
        rows.Add(new CarioProjectInfoRow
        {
            Key = "PMC_PROJECT_ID",
            Value = projectId,
            DataState = string.IsNullOrWhiteSpace(projectId) ? DataState.Unknown : DataState.Known,
            SourceReference = "ProjectManagementCompiler"
        });
        rows.Add(new CarioProjectInfoRow
        {
            Key = "PMC_PROJECT_NAME",
            Value = model.ProjectName,
            DataState = string.IsNullOrWhiteSpace(model.ProjectName) ? DataState.Unknown : DataState.Known,
            SourceReference = "ProjectManagementCompiler"
        });
        return rows;
    }

    private static void WriteGantt(XmlWriter writer, GanttXlsxModel model)
    {
        var rows = BuildGanttRows(model);
        var dates = BuildDateAxis(model, rows);
        const int dateStartColumn = 16;
        var lastColumn = dateStartColumn + dates.Count - 1;

        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        WriteGanttSheetViews(writer, dateStartColumn);
        WriteGanttColumns(writer, dateStartColumn, dates.Count);
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        WriteRow(writer, 1, [Cell.Text($"{model.ProjectName} — Gantt snapshot").WithStyle(GanttTitleStyle)]);
        WriteRow(writer, 2,
        [
            Cell.Text("As of").WithStyle(GanttMetaStyle),
            Cell.Text(FormatDate(model.AsOfDate)),
            Cell.Text("Scope").WithStyle(GanttMetaStyle),
            Cell.Text(model.SnapshotScope),
            Cell.Text("Source").WithStyle(GanttMetaStyle),
            Cell.Text(model.SourceIdentity),
            Cell.Text("Snapshot").WithStyle(GanttMetaStyle),
            Cell.Text(model.SnapshotId)
        ]);
        WriteRow(writer, 3,
        [
            Cell.Text("Recorded % is shown only when actual and remaining effort are both known; otherwise the export says Not recorded.")
        ]);
        WriteRow(writer, 4,
        [
            Cell.Text("Legend").WithStyle(GanttMetaStyle),
            Cell.Text("PLAN").WithStyle(GanttPlanStyle),
            Cell.Text("ACTUAL").WithStyle(GanttActualStyle),
            Cell.Text("ALERT").WithStyle(GanttAlertStyle),
            Cell.Text("MILESTONE").WithStyle(GanttMilestoneStyle),
            Cell.Text("Weekend columns are shaded; the as-of day is marked in the axis.")
        ]);

        var headers = new List<Cell>
        {
            Cell.Text("Level").WithStyle(GanttHeaderStyle),
            Cell.Text("Type").WithStyle(GanttHeaderStyle),
            Cell.Text("ID").WithStyle(GanttHeaderStyle),
            Cell.Text("Name").WithStyle(GanttHeaderStyle),
            Cell.Text("Lane").WithStyle(GanttHeaderStyle),
            Cell.Text("Status").WithStyle(GanttHeaderStyle),
            Cell.Text("Owner / Role").WithStyle(GanttHeaderStyle),
            Cell.Text("Plan Start").WithStyle(GanttHeaderStyle),
            Cell.Text("Plan Finish").WithStyle(GanttHeaderStyle),
            Cell.Text("Actual Start").WithStyle(GanttHeaderStyle),
            Cell.Text("Actual Finish").WithStyle(GanttHeaderStyle),
            Cell.Text("Recorded %").WithStyle(GanttHeaderStyle),
            Cell.Text("Critical").WithStyle(GanttHeaderStyle),
            Cell.Text("Evidence").WithStyle(GanttHeaderStyle),
            Cell.Text("Source Reference").WithStyle(GanttHeaderStyle)
        };
        headers.AddRange(dates.Select(date => Cell.Text(FormatDate(date)).WithStyle(date == model.AsOfDate ? GanttAsOfStyle : GanttHeaderStyle)));
        WriteRow(writer, 5, headers);

        var rowNumber = 6;
        foreach (var row in rows)
        {
            WriteGanttDataRow(writer, rowNumber++, row, model, dates);
        }

        writer.WriteEndElement();
        writer.WriteStartElement("autoFilter", SpreadsheetNamespace);
        writer.WriteAttributeString("ref", $"A5:{ColumnName(lastColumn)}{rowNumber - 1}");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteGanttSheetViews(XmlWriter writer, int dateStartColumn)
    {
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteStartElement("pane", SpreadsheetNamespace);
        writer.WriteAttributeString("xSplit", (dateStartColumn - 1).ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("ySplit", "5");
        writer.WriteAttributeString("topLeftCell", $"{ColumnName(dateStartColumn)}6");
        writer.WriteAttributeString("activePane", "bottomRight");
        writer.WriteAttributeString("state", "frozen");
        writer.WriteEndElement();
        writer.WriteStartElement("selection", SpreadsheetNamespace);
        writer.WriteAttributeString("pane", "bottomRight");
        writer.WriteAttributeString("activeCell", $"{ColumnName(dateStartColumn)}6");
        writer.WriteAttributeString("sqref", $"{ColumnName(dateStartColumn)}6");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteGanttColumns(XmlWriter writer, int dateStartColumn, int dateCount)
    {
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        WriteColumn(writer, 1, 1, 8);
        WriteColumn(writer, 2, 2, 14);
        WriteColumn(writer, 3, 3, 16);
        WriteColumn(writer, 4, 4, 34);
        WriteColumn(writer, 5, 5, 12);
        WriteColumn(writer, 6, 6, 18);
        WriteColumn(writer, 7, 7, 22);
        WriteColumn(writer, 8, 11, 14);
        WriteColumn(writer, 12, 12, 13);
        WriteColumn(writer, 13, 14, 12);
        WriteColumn(writer, 15, 15, 34);
        WriteColumn(writer, dateStartColumn, dateStartColumn + dateCount - 1, 11);
        writer.WriteEndElement();
    }

    private static void WriteColumn(XmlWriter writer, int minimum, int maximum, double width)
    {
        writer.WriteStartElement("col", SpreadsheetNamespace);
        writer.WriteAttributeString("min", minimum.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("max", maximum.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("width", width.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("customWidth", "1");
        writer.WriteEndElement();
    }

    private static void WriteGanttDataRow(
        XmlWriter writer,
        int rowNumber,
        GanttExportRow row,
        GanttXlsxModel model,
        IReadOnlyList<DateOnly> dates)
    {
        var actual = row.Item?.Lanes.FirstOrDefault(lane => lane.Lane == GanttLane.Actual);
        var laneStyle = row.Lane switch
        {
            GanttExportLane.Actual => GanttActualStyle,
            GanttExportLane.Alert => GanttAlertStyle,
            GanttExportLane.Milestone => GanttMilestoneStyle,
            _ => GanttPlanStyle
        };
        var cells = new List<Cell>
        {
            Cell.Text(row.Depth.ToString(CultureInfo.InvariantCulture)),
            Cell.Text(row.Node.Kind.ToString()),
            Cell.Text(row.Node.Id),
            Cell.Text(new string(' ', row.Depth * 2) + row.DisplayName),
            Cell.Text(FormatGanttLane(row.Lane)).WithStyle(laneStyle),
            Cell.Text(FormatRowStatus(row)),
            Cell.Text(row.Item is null ? string.Empty : string.Join(", ", row.Item.LogicalRoles)),
            Cell.Text(FormatDate(row.Node.PlannedStart)),
            Cell.Text(FormatDate(row.Node.PlannedFinish)),
            Cell.Text(FormatDate(actual?.Start)),
            Cell.Text(FormatDate(actual?.Finish)),
            Cell.Text(row.Lane == GanttExportLane.Plan ? FormatRecordedPercent(row.Item) : string.Empty),
            Cell.Text(FormatCritical(row)),
            Cell.Text(FormatEvidence(row)),
            Cell.Text(FormatSourceReferences(row.Node.SourceReferences))
        };
        cells.AddRange(dates.Select(date => BuildTimelineCell(row, model, date)));
        WriteRow(writer, rowNumber, cells);
    }

    private static Cell BuildTimelineCell(GanttExportRow row, GanttXlsxModel model, DateOnly date)
    {
        var defaultStyle = date == model.AsOfDate
            ? GanttAsOfStyle
            : date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
                ? GanttWeekendStyle
                : GanttDefaultStyle;

        if (row.Lane == GanttExportLane.Alert && date == model.AsOfDate)
        {
            return Cell.Text(row.AlertCode ?? "!").WithStyle(GanttAlertStyle);
        }

        if (row.Lane == GanttExportLane.Milestone && row.Timeline?.Start == date)
        {
            return Cell.Text("◆").WithStyle(GanttMilestoneStyle);
        }

        if (row.Lane is not (GanttExportLane.Plan or GanttExportLane.Actual)
            || !IsWithinLane(row.Timeline, date, model.AsOfDate))
        {
            return Cell.Text(string.Empty).WithStyle(defaultStyle);
        }

        var style = row.Lane == GanttExportLane.Actual ? GanttActualStyle : GanttPlanStyle;
        var label = row.Lane == GanttExportLane.Actual && row.Timeline?.IsOpenEnded == true && date == model.AsOfDate
            ? "→"
            : string.Empty;
        return Cell.Text(label).WithStyle(style);
    }

    private static bool IsWithinLane(GanttLaneEntry? lane, DateOnly date, DateOnly asOfDate)
    {
        if (lane is null)
        {
            return false;
        }

        var start = lane.Start;
        var finish = lane.Finish ?? (lane.IsOpenEnded ? asOfDate : lane.Start);
        if (start is null)
        {
            return finish == date;
        }

        return finish is not null && start.Value <= date && date <= finish.Value;
    }

    private static IReadOnlyList<GanttExportRow> BuildGanttRows(GanttXlsxModel model)
    {
        var itemsById = model.Gantt.Items.ToDictionary(item => item.WorkItemId, StringComparer.OrdinalIgnoreCase);
        var milestonesById = model.Gantt.Milestones.ToDictionary(milestone => milestone.MilestoneId, StringComparer.OrdinalIgnoreCase);
        var rows = new List<GanttExportRow>();

        AppendNode(model.Wbs.Root, 0);
        return rows;

        void AppendNode(WbsNode node, int depth)
        {
            itemsById.TryGetValue(node.Id, out var item);
            milestonesById.TryGetValue(node.Id, out var milestone);
            var planLane = item?.Lanes.FirstOrDefault(lane => lane.Lane == GanttLane.Plan)
                ?? (node.PlannedStart is null && node.PlannedFinish is null
                    ? null
                    : new GanttLaneEntry
                    {
                        WorkItemId = node.Id,
                        Lane = GanttLane.Plan,
                        Start = node.PlannedStart,
                        Finish = node.PlannedFinish,
                        State = DataState.Known,
                        Label = "PLAN"
                    });
            rows.Add(new GanttExportRow
            {
                Node = node,
                Depth = depth,
                Lane = node.Kind == WbsNodeKind.Milestone ? GanttExportLane.Milestone : GanttExportLane.Plan,
                DisplayName = node.Name,
                Item = item,
                Timeline = planLane,
                Milestone = milestone
            });

            if (node.Kind == WbsNodeKind.DeliveryCard && item is not null)
            {
                var actual = item.Lanes.FirstOrDefault(lane => lane.Lane == GanttLane.Actual);
                if (actual is not null)
                {
                    rows.Add(new GanttExportRow
                    {
                        Node = node,
                        Depth = depth + 1,
                        Lane = GanttExportLane.Actual,
                        DisplayName = $"↳ {node.Name}",
                        Item = item,
                        Timeline = actual
                    });
                }

                foreach (var alert in item.Lanes.Where(lane => lane.Lane == GanttLane.Alert))
                {
                    rows.Add(new GanttExportRow
                    {
                        Node = node,
                        Depth = depth + 1,
                        Lane = GanttExportLane.Alert,
                        DisplayName = $"↳ {node.Name}",
                        Item = item,
                        AlertCode = alert.AlertCode,
                        AlertLabel = alert.Label
                    });
                }
            }

            foreach (var child in node.Children)
            {
                AppendNode(child, depth + 1);
            }
        }
    }

    private static IReadOnlyList<DateOnly> BuildDateAxis(GanttXlsxModel model, IReadOnlyList<GanttExportRow> rows)
    {
        var dates = rows
            .SelectMany(row => new[]
            {
                row.Node.PlannedStart,
                row.Node.PlannedFinish,
                row.Timeline?.Start,
                row.Timeline?.Finish
            })
            .Where(ValidDate)
            .Select(value => value!.Value)
            .Append(model.AsOfDate)
            .Distinct()
            .OrderBy(value => value)
            .ToArray();
        if (dates.Length == 0)
        {
            return [model.AsOfDate];
        }

        var first = dates[0];
        var last = dates[^1];
        var axis = new List<DateOnly>();
        for (var date = first; ; date = date.AddDays(1))
        {
            axis.Add(date);
            if (date == last)
            {
                break;
            }
        }

        return axis;
    }

    private static string FormatGanttLane(GanttExportLane lane) => lane switch
    {
        GanttExportLane.Actual => "ACTUAL",
        GanttExportLane.Alert => "ALERT",
        GanttExportLane.Milestone => "MILESTONE",
        _ => "PLAN"
    };

    private static string FormatRowStatus(GanttExportRow row)
    {
        if (row.Lane == GanttExportLane.Alert)
        {
            return row.AlertCode ?? "Attention";
        }

        if (row.Item?.ExecutionState is { } itemState)
        {
            return FormatExecutionState(itemState);
        }

        if (row.Node.ExecutionState is { } nodeState)
        {
            return FormatExecutionState(nodeState);
        }

        return row.Node.Kind is WbsNodeKind.DeliveryCard or WbsNodeKind.Milestone
            ? "Not recorded"
            : "Plan only";
    }

    private static string FormatRecordedPercent(GanttItem? item)
    {
        var actual = item?.Lanes.FirstOrDefault(lane => lane.Lane == GanttLane.Actual);
        if (actual?.ActualEffortHours is not decimal actualHours
            || actual.RemainingEffortHours is not decimal remainingHours
            || actualHours < 0
            || remainingHours < 0
            || actualHours + remainingHours <= 0)
        {
            return "Not recorded";
        }

        return $"{actualHours / (actualHours + remainingHours) * 100m:0.#}%";
    }

    private static string FormatCritical(GanttExportRow row) =>
        row.Item?.IsCritical == true || row.Milestone?.IsCritical == true ? "YES" : string.Empty;

    private static string FormatEvidence(GanttExportRow row) => row.Item is null
        ? "Baseline"
        : row.Item.HasExecutionEvidence ? "Recorded" : "Not recorded";

    private static string FormatExecutionState(ExecutionState state) => state switch
    {
        ExecutionState.NotStarted => "NOT_STARTED",
        ExecutionState.InProgress => "IN_PROGRESS",
        ExecutionState.Completed => "COMPLETED",
        ExecutionState.Suspended => "SUSPENDED",
        ExecutionState.Cancelled => "CANCELLED",
        _ => state.ToString().ToUpperInvariant()
    };

    private static string FormatSourceReferences(IReadOnlyList<Domain.SourceReference> references) =>
        references.Count == 0
            ? string.Empty
            : string.Join(";", references.Select(FormatSourceReference).Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal));

    private static void WriteStyles(XmlWriter writer)
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        WriteFonts(writer);
        WriteFills(writer);
        writer.WriteStartElement("borders", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("border", SpreadsheetNamespace);
        writer.WriteStartElement("left", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteStartElement("right", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteStartElement("top", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteStartElement("bottom", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteStartElement("diagonal", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();

        writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        WriteStyleXf(writer, 0, 0, 0);
        writer.WriteEndElement();

        writer.WriteStartElement("cellXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "10");
        WriteStyleXf(writer, 0, 0, 0);
        WriteStyleXf(writer, 1, 2, 0);
        WriteStyleXf(writer, 0, 8, 0);
        WriteStyleXf(writer, 1, 2, 0);
        WriteStyleXf(writer, 0, 3, 0);
        WriteStyleXf(writer, 0, 4, 0);
        WriteStyleXf(writer, 0, 5, 0);
        WriteStyleXf(writer, 0, 6, 0);
        WriteStyleXf(writer, 0, 7, 0);
        WriteStyleXf(writer, 0, 8, 0);
        writer.WriteEndElement();

        writer.WriteStartElement("cellStyles", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("cellStyle", SpreadsheetNamespace);
        writer.WriteAttributeString("name", "Normal");
        writer.WriteAttributeString("xfId", "0");
        writer.WriteAttributeString("builtinId", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteFonts(XmlWriter writer)
    {
        writer.WriteStartElement("fonts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteFont(writer, bold: false, "FF334155");
        WriteFont(writer, bold: true, "FFFFFFFF");
        writer.WriteEndElement();
    }

    private static void WriteFont(XmlWriter writer, bool bold, string color)
    {
        writer.WriteStartElement("font", SpreadsheetNamespace);
        if (bold)
        {
            writer.WriteStartElement("b", SpreadsheetNamespace);
            writer.WriteEndElement();
        }

        writer.WriteStartElement("sz", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "11");
        writer.WriteEndElement();
        writer.WriteStartElement("color", SpreadsheetNamespace);
        writer.WriteAttributeString("rgb", color);
        writer.WriteEndElement();
        writer.WriteStartElement("name", SpreadsheetNamespace);
        writer.WriteAttributeString("val", "Calibri");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteFills(XmlWriter writer)
    {
        writer.WriteStartElement("fills", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "9");
        WritePatternFill(writer, "none", null);
        WritePatternFill(writer, "gray125", null);
        WritePatternFill(writer, "solid", "FF0F766E");
        WritePatternFill(writer, "solid", "FFBFE7F5");
        WritePatternFill(writer, "solid", "FFCDEFE0");
        WritePatternFill(writer, "solid", "FFF6D28A");
        WritePatternFill(writer, "solid", "FFE2E8F0");
        WritePatternFill(writer, "solid", "FFF8FAFC");
        WritePatternFill(writer, "solid", "FFE0F2FE");
        writer.WriteEndElement();
    }

    private static void WritePatternFill(XmlWriter writer, string pattern, string? foregroundColor)
    {
        writer.WriteStartElement("fill", SpreadsheetNamespace);
        writer.WriteStartElement("patternFill", SpreadsheetNamespace);
        writer.WriteAttributeString("patternType", pattern);
        if (foregroundColor is not null)
        {
            writer.WriteStartElement("fgColor", SpreadsheetNamespace);
            writer.WriteAttributeString("rgb", foregroundColor);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteStyleXf(XmlWriter writer, int fontId, int fillId, int borderId)
    {
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("xfId", "0");
        writer.WriteEndElement();
    }

    private enum GanttExportLane
    {
        Plan,
        Actual,
        Alert,
        Milestone
    }

    private sealed record GanttExportRow
    {
        public WbsNode Node { get; init; } = new();
        public int Depth { get; init; }
        public GanttExportLane Lane { get; init; }
        public string DisplayName { get; init; } = string.Empty;
        public GanttItem? Item { get; init; }
        public GanttLaneEntry? Timeline { get; init; }
        public GanttMilestoneEntry? Milestone { get; init; }
        public string? AlertCode { get; init; }
        public string? AlertLabel { get; init; }
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
            if (cell.StyleIndex > 0)
            {
                writer.WriteAttributeString("s", cell.StyleIndex.ToString(CultureInfo.InvariantCulture));
            }
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

    private static bool ValidDate(DateOnly? date) => date is not null && date != DateOnly.MinValue;

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

    private readonly record struct Cell(string? Value, bool IsNumber, int StyleIndex = GanttDefaultStyle)
    {
        public static Cell Text(string? value) => new(value ?? string.Empty, false);

        public static Cell Number(decimal? value) => value is null
            ? Text(string.Empty)
            : new(value.Value.ToString("0.####################", CultureInfo.InvariantCulture), true);

        public Cell WithStyle(int styleIndex) => this with { StyleIndex = styleIndex };
    }
}
