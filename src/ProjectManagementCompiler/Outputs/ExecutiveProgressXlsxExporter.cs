using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Outputs;

public sealed class ExecutiveProgressXlsxExporter
{
    private const string ContentTypesNamespace = "http://schemas.openxmlformats.org/package/2006/content-types";
    private const string PackageRelationshipsNamespace = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const string SpreadsheetNamespace = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private const string OfficeDocumentRelationshipsNamespace = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    private const int DefaultStyle = 0;
    private const int TitleStyle = 1;
    private const int SubtitleStyle = 2;
    private const int HeaderStyle = 3;
    private const int PlanStyle = 4;
    private const int CompleteStyle = 5;
    private const int AttentionStyle = 6;
    private const int BlockedStyle = 7;
    private const int UnknownStyle = 8;
    private const int MarkerStyle = 9;

    private static readonly string[] SheetNames =
    [
        "Tổng quan",
        "Lịch trình",
        "Vấn đề cần xử lý",
        "Chi tiết công việc"
    ];

    public byte[] Export(ExecutiveProgressReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", writer => WriteContentTypes(writer));
            WriteEntry(archive, "_rels/.rels", WriteRootRelationships);
            WriteEntry(archive, "xl/workbook.xml", writer => WriteWorkbook(writer));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", WriteWorkbookRelationships);
            WriteEntry(archive, "xl/styles.xml", WriteStyles);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", writer => WriteOverview(writer, report));
            WriteEntry(archive, "xl/worksheets/sheet2.xml", writer => WriteSchedule(writer, report));
            WriteEntry(archive, "xl/worksheets/sheet3.xml", writer => WriteAttention(writer, report));
            WriteEntry(archive, "xl/worksheets/sheet4.xml", writer => WriteDetails(writer, report));
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
        WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
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
        writer.WriteAttributeString("xmlns", "r", "http://www.w3.org/2000/xmlns/", OfficeDocumentRelationshipsNamespace);
        writer.WriteStartElement("bookViews", SpreadsheetNamespace);
        writer.WriteStartElement("workbookView", SpreadsheetNamespace);
        writer.WriteAttributeString("activeTab", "0");
        writer.WriteAttributeString("firstSheet", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
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

        writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
        writer.WriteAttributeString("Id", $"rId{SheetNames.Length + 1}");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
        writer.WriteAttributeString("Target", "styles.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteOverview(XmlWriter writer, ExecutiveProgressReport report)
    {
        var axis = BuildTimelineAxis(report.OverviewTimeline, report);
        var rows = new List<Row>
        {
            Row.Of(Cell.Text("Báo cáo tiến độ", TitleStyle)),
            Row.Of(Cell.Text(report.ProjectName, SubtitleStyle)),
            Row.Of(Cell.Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", MarkerStyle)),
            Row.Of(Cell.Text($"Khung kế hoạch: {FormatDate(report.PlanningStart)} – {FormatDate(report.PlanningFinish)}", DefaultStyle)),
            Row.Of(Cell.Text(BuildProvenance(report), SubtitleStyle)),
            Row.Blank(),
            Row.Of(Cell.Text("Tình trạng lịch trình", HeaderStyle)),
            Row.Of(Cell.Text($"Giai đoạn hiện tại: {report.CurrentPhase}", StyleFor(report.ScheduleCondition.Tone))),
            Row.Of(Cell.Text(report.ScheduleCondition.Label, StyleFor(report.ScheduleCondition.Tone)), Cell.Text(report.ScheduleCondition.Detail, StyleFor(report.ScheduleCondition.Tone))),
            Row.Blank(),
            Row.Of(Cell.Text("Mốc sắp tới", HeaderStyle)),
            Row.Of(Cell.Text(report.NextMilestone.DisplayName, report.NextMilestone.IsMissing ? UnknownStyle : PlanStyle), Cell.Text(FormatDate(report.NextMilestone.PlannedDate), report.NextMilestone.IsMissing ? UnknownStyle : PlanStyle)),
            Row.Blank(),
            Row.Of(Cell.Text("Tiến độ được ghi nhận", HeaderStyle)),
            Row.Of(Cell.Text(report.Progress.Statement, report.Progress.RecordedPercent is null ? UnknownStyle : CompleteStyle)),
            Row.Of(Cell.Text(BuildCounts(report.Progress), DefaultStyle)),
            Row.Blank(),
            Row.Of(Cell.Text("Việc cần quyết định", HeaderStyle)),
            Row.Of(Cell.Text(report.ReadinessCondition.Label, StyleFor(report.ReadinessCondition.Tone)), Cell.Text(report.ReadinessCondition.Detail, StyleFor(report.ReadinessCondition.Tone))),
            Row.Blank(),
            Row.Of(Cell.Text("Dòng thời gian kế hoạch", HeaderStyle)),
            Row.Of(
                Cell.Text("Giai đoạn / mốc", HeaderStyle),
                Cell.Text("Bắt đầu kế hoạch", HeaderStyle),
                Cell.Text("Kết thúc kế hoạch", HeaderStyle),
                Cell.Text("Tình trạng", HeaderStyle),
                Cell.Text("Đầu mối", HeaderStyle)),
        };

        rows.AddRange(TimelineAxisRows(axis, fixedColumnCount: 5));
        rows.AddRange(report.OverviewTimeline.Select(row => Row.Of(
            new[]
            {
                Cell.Text(row.DisplayName, row.IsCurrent ? PlanStyle : DefaultStyle),
                Cell.Text(FormatDate(row.PlannedStart), PlanStyle),
                Cell.Text(FormatDate(row.PlannedFinish), PlanStyle),
                Cell.Text(row.StateLabel, StateStyle(row.StateLabel)),
                Cell.Text(row.OwnerLabel)
            }
            .Concat(TimelineCells(row, axis))
            .ToArray())));

        rows.Add(Row.Blank());
        rows.Add(Row.Of(Cell.Text("Ngày báo cáo", MarkerStyle), Cell.Text(FormatDate(report.SourceReportingDate), MarkerStyle)));
        rows.Add(Row.Of(Cell.Text("Nội dung cần xin ý kiến", HeaderStyle)));
        var attention = report.OverviewAttention.Take(5).ToArray();
        if (attention.Length == 0)
        {
            rows.Add(Row.Of(Cell.Text("Hiện chưa có nội dung cần xin ý kiến", UnknownStyle)));
        }
        else
        {
            rows.Add(Row.Of(Cell.Text("Việc cần xử lý", HeaderStyle), Cell.Text("Ảnh hưởng", HeaderStyle), Cell.Text("Đầu mối", HeaderStyle), Cell.Text("Cần xong trước", HeaderStyle)));
            rows.AddRange(attention.Select(item => Row.Of(
                Cell.Text(item.Action, AttentionStyle),
                Cell.Text(item.Impact, AttentionStyle),
                Cell.Text(item.OwnerLabel, item.OwnerLabel == "Chưa xác định đầu mối" ? UnknownStyle : DefaultStyle),
                Cell.Text(item.DueLabel))));
        }

        WriteWorksheet(
            writer,
            rows,
            new[] { 30d, 18d, 18d, 34d, 24d }.Concat(axis.WeekStarts.Select(_ => 8d)).ToArray(),
            freezeRows: 22 + (axis.WeekStarts.Count > 0 ? 2 : 0),
            freezeColumns: 1);
    }

    private static void WriteSchedule(XmlWriter writer, ExecutiveProgressReport report)
    {
        var axis = BuildTimelineAxis(report.WorkPackageSchedule, report);
        var rows = new List<Row>
        {
            Row.Of(Cell.Text("Lịch trình", TitleStyle)),
            Row.Of(Cell.Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", MarkerStyle)),
            Row.Blank(),
            Row.Of(
                Cell.Text("Giai đoạn", HeaderStyle),
                Cell.Text("Gói công việc", HeaderStyle),
                Cell.Text("Bắt đầu kế hoạch", HeaderStyle),
                Cell.Text("Kết thúc kế hoạch", HeaderStyle),
                Cell.Text("Tình trạng", HeaderStyle),
                Cell.Text("Đầu mối", HeaderStyle))
        };
        rows.AddRange(TimelineAxisRows(axis, fixedColumnCount: 6));
        rows.AddRange(report.WorkPackageSchedule.Select(row => Row.Of(
            new[]
            {
                Cell.Text(row.PhaseDisplayName ?? "Chưa xác định"),
                Cell.Text(row.DisplayName),
                Cell.Text(FormatDate(row.PlannedStart), PlanStyle),
                Cell.Text(FormatDate(row.PlannedFinish), PlanStyle),
                Cell.Text(row.StateLabel, StateStyle(row.StateLabel)),
                Cell.Text(row.OwnerLabel, row.OwnerLabel == "Chưa xác định đầu mối" ? UnknownStyle : DefaultStyle)
            }
            .Concat(TimelineCells(row, axis))
            .ToArray())));
        if (report.WorkPackageSchedule.Count == 0)
        {
            rows.Add(Row.Of(Cell.Text("Chưa có dữ liệu gói công việc", UnknownStyle)));
        }

        rows.Add(Row.Blank());
        rows.Add(Row.Of(Cell.Text("Ngày báo cáo", MarkerStyle), Cell.Text(FormatDate(report.SourceReportingDate), MarkerStyle)));
        WriteWorksheet(
            writer,
            rows,
            new[] { 24d, 32d, 18d, 18d, 22d, 28d }.Concat(axis.WeekStarts.Select(_ => 8d)).ToArray(),
            freezeRows: 4 + (axis.WeekStarts.Count > 0 ? 2 : 0),
            freezeColumns: 2);
    }

    private static void WriteAttention(XmlWriter writer, ExecutiveProgressReport report)
    {
        var rows = new List<Row>
        {
            Row.Of(Cell.Text("Vấn đề cần xử lý", TitleStyle)),
            Row.Of(Cell.Text("Tất cả nội dung có hành động hoặc quyết định cần theo dõi.", SubtitleStyle)),
            Row.Blank(),
            Row.Of(Cell.Text("Việc cần xử lý", HeaderStyle), Cell.Text("Ảnh hưởng", HeaderStyle), Cell.Text("Đầu mối", HeaderStyle), Cell.Text("Cần xong trước", HeaderStyle))
        };
        if (report.AllAttention.Count == 0)
        {
            rows.Add(Row.Of(Cell.Text("Hiện chưa có nội dung cần xin ý kiến", UnknownStyle)));
        }
        else
        {
            rows.AddRange(report.AllAttention.Select(item => Row.Of(
                Cell.Text(item.Action, AttentionStyle),
                Cell.Text(item.Impact, AttentionStyle),
                Cell.Text(item.OwnerLabel, item.OwnerLabel == "Chưa xác định đầu mối" ? UnknownStyle : DefaultStyle),
                Cell.Text(item.DueLabel))));
        }

        WriteWorksheet(writer, rows, new[] { 40d, 44d, 28d, 20d }, freezeRows: 4, freezeColumns: 0);
    }

    private static void WriteDetails(XmlWriter writer, ExecutiveProgressReport report)
    {
        var rows = new List<Row>
        {
            Row.Of(Cell.Text("Chi tiết công việc", TitleStyle)),
            Row.Of(Cell.Text($"Ngày báo cáo: {FormatDate(report.SourceReportingDate)}", MarkerStyle)),
            Row.Blank(),
            Row.Of(
                Cell.Text("Công việc", HeaderStyle),
                Cell.Text("Giai đoạn", HeaderStyle),
                Cell.Text("Gói công việc", HeaderStyle),
                Cell.Text("Bắt đầu kế hoạch", HeaderStyle),
                Cell.Text("Kết thúc kế hoạch", HeaderStyle),
                Cell.Text("Đầu mối", HeaderStyle),
                Cell.Text("Tình trạng", HeaderStyle),
                Cell.Text("Mã tham chiếu", HeaderStyle))
        };
        rows.AddRange(report.DeliveryCardDetails.Select(detail => Row.Of(
            Cell.Text(detail.Description),
            Cell.Text(detail.PhaseName),
            Cell.Text(detail.WorkPackageName),
            Cell.Text(FormatDate(detail.PlannedStart), PlanStyle),
            Cell.Text(FormatDate(detail.PlannedFinish), PlanStyle),
            Cell.Text(detail.OwnerLabel, detail.OwnerLabel == "Chưa xác định đầu mối" ? UnknownStyle : DefaultStyle),
            Cell.Text(detail.StateLabel, StateStyle(detail.StateLabel)),
            Cell.Text(detail.ReferenceCode, DefaultStyle))));
        if (report.DeliveryCardDetails.Count == 0)
        {
            rows.Add(Row.Of(Cell.Text("Chưa có dữ liệu công việc", UnknownStyle)));
        }

        WriteWorksheet(writer, rows, new[] { 40d, 24d, 28d, 18d, 18d, 28d, 22d, 18d }, freezeRows: 4, freezeColumns: 0);
    }

    private static void WriteWorksheet(XmlWriter writer, IReadOnlyList<Row> rows, IReadOnlyList<double> widths, int freezeRows, int freezeColumns)
    {
        writer.WriteStartElement("worksheet", SpreadsheetNamespace);
        writer.WriteStartElement("sheetPr", SpreadsheetNamespace);
        writer.WriteStartElement("pageSetUpPr", SpreadsheetNamespace);
        writer.WriteAttributeString("fitToPage", "1");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetViews", SpreadsheetNamespace);
        writer.WriteStartElement("sheetView", SpreadsheetNamespace);
        writer.WriteAttributeString("workbookViewId", "0");
        writer.WriteAttributeString("showGridLines", "0");
        writer.WriteAttributeString("zoomScale", "100");
        if (freezeRows > 0 || freezeColumns > 0)
        {
            writer.WriteStartElement("pane", SpreadsheetNamespace);
            if (freezeColumns > 0)
            {
                writer.WriteAttributeString("xSplit", freezeColumns.ToString(CultureInfo.InvariantCulture));
            }

            if (freezeRows > 0)
            {
                writer.WriteAttributeString("ySplit", freezeRows.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteAttributeString("topLeftCell", CellAddress(freezeColumns + 1, freezeRows + 1));
            writer.WriteAttributeString("activePane", freezeColumns > 0 && freezeRows > 0 ? "bottomRight" : freezeRows > 0 ? "bottomLeft" : "topRight");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr", SpreadsheetNamespace);
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var index = 0; index < widths.Count; index++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace);
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", widths[index].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            WriteRow(writer, rows[rowIndex], rowIndex + 1);
        }

        writer.WriteEndElement();
        writer.WriteStartElement("pageMargins", SpreadsheetNamespace);
        writer.WriteAttributeString("left", "0.25");
        writer.WriteAttributeString("right", "0.25");
        writer.WriteAttributeString("top", "0.5");
        writer.WriteAttributeString("bottom", "0.5");
        writer.WriteAttributeString("header", "0.2");
        writer.WriteAttributeString("footer", "0.2");
        writer.WriteEndElement();
        writer.WriteStartElement("pageSetup", SpreadsheetNamespace);
        writer.WriteAttributeString("orientation", "landscape");
        writer.WriteAttributeString("fitToWidth", "1");
        writer.WriteAttributeString("fitToHeight", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, Row row, int rowNumber)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        if (row.Cells.Count == 0)
        {
            writer.WriteEndElement();
            return;
        }

        for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
        {
            var cell = row.Cells[cellIndex];
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", $"{CellAddress(cellIndex + 1, rowNumber)}");
            if (cell.Style != DefaultStyle)
            {
                writer.WriteAttributeString("s", cell.Style.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            if (cell.Value.StartsWith(' ') || cell.Value.EndsWith(' '))
            {
                writer.WriteAttributeString("space", XmlNamespace, "preserve");
            }

            writer.WriteString(cell.Value);
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteStyles(XmlWriter writer)
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteStartElement("numFmts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "0");
        writer.WriteEndElement();
        writer.WriteStartElement("fonts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteFont(writer, "Aptos", "11", false, false);
        WriteFont(writer, "Aptos Display", "16", true, false);
        writer.WriteEndElement();
        writer.WriteStartElement("fills", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "7");
        WriteFill(writer, "none");
        WriteFill(writer, "gray125");
        WriteFill(writer, "DDEBF7");
        WriteFill(writer, "E2F0D9");
        WriteFill(writer, "FFF2CC");
        WriteFill(writer, "F4CCCC");
        WriteFill(writer, "E7E6E6");
        writer.WriteEndElement();
        writer.WriteStartElement("borders", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "2");
        WriteBorder(writer, false);
        WriteBorder(writer, true);
        writer.WriteEndElement();
        writer.WriteStartElement("cellStyleXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "1");
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", "0");
        writer.WriteAttributeString("fillId", "0");
        writer.WriteAttributeString("borderId", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("cellXfs", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "10");
        WriteCellFormat(writer, 0, 0, 0, false, true);
        WriteCellFormat(writer, 1, 0, 0, true, false, fontSize: "16");
        WriteCellFormat(writer, 0, 6, 1, true, true);
        WriteCellFormat(writer, 0, 6, 1, true, true, fontSize: "11");
        WriteCellFormat(writer, 0, 2, 1, false, true);
        WriteCellFormat(writer, 0, 3, 1, false, true);
        WriteCellFormat(writer, 0, 4, 1, false, true);
        WriteCellFormat(writer, 0, 5, 1, false, true);
        WriteCellFormat(writer, 0, 6, 1, false, true);
        WriteCellFormat(writer, 0, 2, 1, true, true);
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

    private static void WriteFont(XmlWriter writer, string name, string size, bool bold, bool italic)
    {
        writer.WriteStartElement("font", SpreadsheetNamespace);
        writer.WriteStartElement("sz", SpreadsheetNamespace);
        writer.WriteAttributeString("val", size);
        writer.WriteEndElement();
        writer.WriteStartElement("name", SpreadsheetNamespace);
        writer.WriteAttributeString("val", name);
        writer.WriteEndElement();
        if (bold)
        {
            writer.WriteStartElement("b", SpreadsheetNamespace);
            writer.WriteEndElement();
        }

        if (italic)
        {
            writer.WriteStartElement("i", SpreadsheetNamespace);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteFill(XmlWriter writer, string kind)
    {
        writer.WriteStartElement("fill", SpreadsheetNamespace);
        if (kind is "none" or "gray125")
        {
            writer.WriteStartElement(kind, SpreadsheetNamespace);
            writer.WriteEndElement();
        }
        else
        {
            writer.WriteStartElement("patternFill", SpreadsheetNamespace);
            writer.WriteAttributeString("patternType", "solid");
            writer.WriteStartElement("fgColor", SpreadsheetNamespace);
            writer.WriteAttributeString("rgb", kind);
            writer.WriteEndElement();
            writer.WriteStartElement("bgColor", SpreadsheetNamespace);
            writer.WriteAttributeString("indexed", "64");
            writer.WriteEndElement();
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteBorder(XmlWriter writer, bool colored)
    {
        writer.WriteStartElement("border", SpreadsheetNamespace);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            writer.WriteStartElement(side, SpreadsheetNamespace);
            writer.WriteAttributeString("style", colored ? "thin" : "none");
            writer.WriteEndElement();
        }

        writer.WriteStartElement("diagonal", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteCellFormat(XmlWriter writer, int fontId, int fillId, int borderId, bool bold, bool wrap, string? fontSize = null)
    {
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", "0");
        writer.WriteAttributeString("fontId", fontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", fillId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("borderId", borderId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("xfId", "0");
        if (bold || wrap)
        {
            writer.WriteStartElement("alignment", SpreadsheetNamespace);
            if (wrap)
            {
                writer.WriteAttributeString("wrapText", "1");
            }

            writer.WriteAttributeString("vertical", "top");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static string BuildProvenance(ExecutiveProgressReport report) =>
        report.AnalysisAsOfDate == report.SourceReportingDate
            ? $"Nguồn chính thức IDEAEngineering · dữ liệu cập nhật đến {FormatDate(report.SourceReportingDate)}"
            : $"Nguồn chính thức IDEAEngineering · dữ liệu cập nhật đến {FormatDate(report.SourceReportingDate)} · phân tích đến {FormatDate(report.AnalysisAsOfDate)}";

    private static TimelineAxis BuildTimelineAxis(IEnumerable<ExecutiveScheduleRow> rows, ExecutiveProgressReport report)
    {
        var dates = rows
            .SelectMany(row => new[] { row.PlannedStart, row.PlannedFinish })
            .Concat(new DateOnly?[] { report.PlanningStart, report.PlanningFinish, report.SourceReportingDate })
            .Where(date => date is not null)
            .Select(date => date!.Value)
            .ToArray();
        if (dates.Length == 0)
        {
            return new TimelineAxis(Array.Empty<DateOnly>(), report.SourceReportingDate);
        }

        var start = dates.Min();
        var finish = dates.Max();
        var weekStart = start.AddDays(-(int)start.DayOfWeek + (int)DayOfWeek.Monday);
        if (start.DayOfWeek == DayOfWeek.Sunday)
        {
            weekStart = start.AddDays(-6);
        }

        var lastWeek = finish.AddDays(-(int)finish.DayOfWeek + (int)DayOfWeek.Monday);
        if (finish.DayOfWeek == DayOfWeek.Sunday)
        {
            lastWeek = finish.AddDays(-6);
        }

        var weekStarts = new List<DateOnly>();
        for (var cursor = weekStart; cursor <= lastWeek; cursor = cursor.AddDays(7))
        {
            weekStarts.Add(cursor);
        }

        return new TimelineAxis(weekStarts, report.SourceReportingDate);
    }

    private static IReadOnlyList<Row> TimelineAxisRows(TimelineAxis axis, int fixedColumnCount)
    {
        if (axis.WeekStarts.Count == 0)
        {
            return Array.Empty<Row>();
        }

        var monthCells = axis.WeekStarts
            .Select((week, index) =>
            {
                var previous = index == 0 ? (DateOnly?)null : axis.WeekStarts[index - 1];
                var isFirstWeekOfMonth = previous is null || previous.Value.Month != week.Month || previous.Value.Year != week.Year;
                return Cell.Text(isFirstWeekOfMonth ? $"Tháng {week:MM/yyyy}" : string.Empty, isFirstWeekOfMonth ? PlanStyle : DefaultStyle);
            })
            .ToArray();
        var weekCells = axis.WeekStarts
            .Select(week =>
            {
                var weekNumber = ISOWeek.GetWeekOfYear(week.ToDateTime(TimeOnly.MinValue));
                var marker = axis.ReportingDate >= week && axis.ReportingDate <= week.AddDays(6)
                    ? " · Ngày báo cáo"
                    : string.Empty;
                return Cell.Text($"W{weekNumber:00}{marker}", marker.Length > 0 ? MarkerStyle : DefaultStyle);
            })
            .ToArray();

        var monthPrefix = new[] { Cell.Text("Tháng", HeaderStyle) }
            .Concat(Enumerable.Repeat(Cell.Text(string.Empty), fixedColumnCount - 1));
        var weekPrefix = new[] { Cell.Text("Tuần ISO", HeaderStyle) }
            .Concat(Enumerable.Repeat(Cell.Text(string.Empty), fixedColumnCount - 1));
        return
        [
            Row.Of(monthPrefix.Concat(monthCells).ToArray()),
            Row.Of(weekPrefix.Concat(weekCells).ToArray())
        ];
    }

    private static IReadOnlyList<CellValue> TimelineCells(ExecutiveScheduleRow row, TimelineAxis axis)
    {
        if (axis.WeekStarts.Count == 0 || row.PlannedStart is null && row.PlannedFinish is null)
        {
            return Array.Empty<CellValue>();
        }

        var start = row.PlannedStart ?? row.PlannedFinish!.Value;
        var finish = row.PlannedFinish ?? row.PlannedStart!.Value;
        if (finish < start)
        {
            (start, finish) = (finish, start);
        }

        return axis.WeekStarts
            .Select(week =>
            {
                var weekFinish = week.AddDays(6);
                var visible = row.Kind == ExecutiveScheduleRowKind.Milestone
                    ? start >= week && start <= weekFinish
                    : start <= weekFinish && finish >= week;
                return Cell.Text(visible ? row.Kind == ExecutiveScheduleRowKind.Milestone ? "◆" : "■" : string.Empty, visible ? TimelineStyle(row) : DefaultStyle);
            })
            .ToArray();
    }

    private static int TimelineStyle(ExecutiveScheduleRow row) =>
        row.Kind == ExecutiveScheduleRowKind.Milestone || row.IsCurrent ? MarkerStyle : PlanStyle;

    private sealed record TimelineAxis(IReadOnlyList<DateOnly> WeekStarts, DateOnly ReportingDate);

    private static string BuildCounts(ExecutiveProgressSummary progress) =>
        $"Hoàn thành: {progress.CompletedCount} · Đang thực hiện: {progress.InProgressCount} · Chưa bắt đầu: {progress.NotStartedCount} · Chưa cập nhật: {progress.UnknownCount}";

    private static int StateStyle(string stateLabel) => stateLabel switch
    {
        "Hoàn thành" => CompleteStyle,
        "Bị chặn" or "Trễ kế hoạch" => BlockedStyle,
        "Đang thực hiện" or "Cần xử lý" or "Cần quyết định" => AttentionStyle,
        "Chưa cập nhật" or "Chưa đánh giá" => UnknownStyle,
        _ => DefaultStyle
    };

    private static int StyleFor(ExecutiveConditionTone tone) => tone switch
    {
        ExecutiveConditionTone.Plan => PlanStyle,
        ExecutiveConditionTone.Complete => CompleteStyle,
        ExecutiveConditionTone.Attention => AttentionStyle,
        ExecutiveConditionTone.Blocked => BlockedStyle,
        _ => UnknownStyle
    };

    private static string FormatDate(DateOnly? date) => date?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Chưa xác định";

    private static string CellAddress(int column, int row)
    {
        var value = column;
        var result = string.Empty;
        while (value > 0)
        {
            value--;
            result = (char)('A' + value % 26) + result;
            value /= 26;
        }

        return result + row.ToString(CultureInfo.InvariantCulture);
    }

    private sealed record CellValue(string Value, int Style);
    private sealed record Row(IReadOnlyList<CellValue> Cells)
    {
        public static Row Of(params CellValue[] cells) => new(cells);
        public static Row Blank() => new(Array.Empty<CellValue>());
    }

    private static class Cell
    {
        public static CellValue Text(string? value, int style = DefaultStyle) => new(value ?? string.Empty, style);
    }
}
