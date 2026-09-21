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

    public byte[] Export(ExecutiveProgressReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        return Serialize(new ExecutiveProgressWorkbookComposer().Build(report));
    }

    private static byte[] Serialize(ExecutiveWorkbookDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var output = new MemoryStream();
        using (var archive = new ZipArchive(output, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", writer => WriteContentTypes(writer, document));
            WriteEntry(archive, "_rels/.rels", WriteRootRelationships);
            WriteEntry(archive, "xl/workbook.xml", writer => WriteWorkbook(writer, document));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", writer => WriteWorkbookRelationships(writer, document));
            WriteEntry(archive, "xl/styles.xml", WriteStyles);
            for (var index = 0; index < document.Sheets.Count; index++)
            {
                var sheet = document.Sheets[index];
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", writer => WriteWorksheet(writer, sheet));
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

    private static void WriteContentTypes(XmlWriter writer, ExecutiveWorkbookDocument document)
    {
        writer.WriteStartElement("Types", ContentTypesNamespace);
        WriteDefault(writer, "rels", "application/vnd.openxmlformats-package.relationships+xml");
        WriteDefault(writer, "xml", "application/xml");
        WriteOverride(writer, "/xl/workbook.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml");
        WriteOverride(writer, "/xl/styles.xml", "application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml");
        for (var index = 1; index <= document.Sheets.Count; index++)
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

    private static void WriteWorkbook(XmlWriter writer, ExecutiveWorkbookDocument document)
    {
        writer.WriteStartElement("workbook", SpreadsheetNamespace);
        writer.WriteAttributeString("xmlns", "r", "http://www.w3.org/2000/xmlns/", OfficeDocumentRelationshipsNamespace);
        writer.WriteStartElement("bookViews", SpreadsheetNamespace);
        writer.WriteStartElement("workbookView", SpreadsheetNamespace);
        writer.WriteAttributeString("activeTab", document.ActiveSheetIndex.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("firstSheet", "0");
        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheets", SpreadsheetNamespace);
        for (var index = 0; index < document.Sheets.Count; index++)
        {
            writer.WriteStartElement("sheet", SpreadsheetNamespace);
            writer.WriteAttributeString("name", document.Sheets[index].Name);
            writer.WriteAttributeString("sheetId", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("r", "id", OfficeDocumentRelationshipsNamespace, $"rId{index + 1}");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorkbookRelationships(XmlWriter writer, ExecutiveWorkbookDocument document)
    {
        writer.WriteStartElement("Relationships", PackageRelationshipsNamespace);
        for (var index = 0; index < document.Sheets.Count; index++)
        {
            writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
            writer.WriteAttributeString("Id", $"rId{index + 1}");
            writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet");
            writer.WriteAttributeString("Target", $"worksheets/sheet{index + 1}.xml");
            writer.WriteEndElement();
        }

        writer.WriteStartElement("Relationship", PackageRelationshipsNamespace);
        writer.WriteAttributeString("Id", $"rId{document.Sheets.Count + 1}");
        writer.WriteAttributeString("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles");
        writer.WriteAttributeString("Target", "styles.xml");
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteWorksheet(XmlWriter writer, ExecutiveWorkbookWorksheet worksheet)
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
        writer.WriteAttributeString("showGridLines", worksheet.ShowGridLines ? "1" : "0");
        writer.WriteAttributeString("zoomScale", worksheet.ZoomPercent.ToString(CultureInfo.InvariantCulture));
        if (worksheet.FreezePane.FrozenRows > 0 || worksheet.FreezePane.FrozenColumns > 0)
        {
            writer.WriteStartElement("pane", SpreadsheetNamespace);
            if (worksheet.FreezePane.FrozenColumns > 0)
            {
                writer.WriteAttributeString("xSplit", worksheet.FreezePane.FrozenColumns.ToString(CultureInfo.InvariantCulture));
            }

            if (worksheet.FreezePane.FrozenRows > 0)
            {
                writer.WriteAttributeString("ySplit", worksheet.FreezePane.FrozenRows.ToString(CultureInfo.InvariantCulture));
            }

            writer.WriteAttributeString("topLeftCell", CellAddress(worksheet.FreezePane.FrozenColumns + 1, worksheet.FreezePane.FrozenRows + 1));
            writer.WriteAttributeString("activePane", worksheet.FreezePane.FrozenColumns > 0 && worksheet.FreezePane.FrozenRows > 0 ? "bottomRight" : worksheet.FreezePane.FrozenRows > 0 ? "bottomLeft" : "topRight");
            writer.WriteAttributeString("state", "frozen");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteEndElement();
        writer.WriteStartElement("sheetFormatPr", SpreadsheetNamespace);
        writer.WriteAttributeString("defaultRowHeight", "18");
        writer.WriteEndElement();
        writer.WriteStartElement("cols", SpreadsheetNamespace);
        for (var index = 0; index < worksheet.ColumnWidths.Count; index++)
        {
            writer.WriteStartElement("col", SpreadsheetNamespace);
            writer.WriteAttributeString("min", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("max", (index + 1).ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("width", worksheet.ColumnWidths[index].ToString(CultureInfo.InvariantCulture));
            writer.WriteAttributeString("customWidth", "1");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
        writer.WriteStartElement("sheetData", SpreadsheetNamespace);
        for (var rowIndex = 0; rowIndex < worksheet.Rows.Count; rowIndex++)
        {
            WriteRow(writer, worksheet.Rows[rowIndex], rowIndex + 1);
        }

        writer.WriteEndElement();
        if (worksheet.MergedRanges.Count > 0)
        {
            writer.WriteStartElement("mergeCells", SpreadsheetNamespace);
            writer.WriteAttributeString("count", worksheet.MergedRanges.Count.ToString(CultureInfo.InvariantCulture));
            foreach (var range in worksheet.MergedRanges)
            {
                writer.WriteStartElement("mergeCell", SpreadsheetNamespace);
                writer.WriteAttributeString("ref", $"{CellAddress(range.StartColumn, range.StartRow)}:{CellAddress(range.EndColumn, range.EndRow)}");
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteStartElement("pageMargins", SpreadsheetNamespace);
        writer.WriteAttributeString("left", "0.25");
        writer.WriteAttributeString("right", "0.25");
        writer.WriteAttributeString("top", "0.5");
        writer.WriteAttributeString("bottom", "0.5");
        writer.WriteAttributeString("header", "0.2");
        writer.WriteAttributeString("footer", "0.2");
        writer.WriteEndElement();
        writer.WriteStartElement("pageSetup", SpreadsheetNamespace);
        writer.WriteAttributeString("orientation", worksheet.PrintSettings.Orientation == ExecutiveWorkbookPrintOrientation.Landscape ? "landscape" : "portrait");
        writer.WriteAttributeString("fitToWidth", worksheet.PrintSettings.FitToWidth.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fitToHeight", worksheet.PrintSettings.FitToHeight.ToString(CultureInfo.InvariantCulture));
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteRow(XmlWriter writer, ExecutiveWorkbookRow row, int rowNumber)
    {
        writer.WriteStartElement("row", SpreadsheetNamespace);
        writer.WriteAttributeString("r", rowNumber.ToString(CultureInfo.InvariantCulture));
        for (var cellIndex = 0; cellIndex < row.Cells.Count; cellIndex++)
        {
            var cell = row.Cells[cellIndex];
            writer.WriteStartElement("c", SpreadsheetNamespace);
            writer.WriteAttributeString("r", CellAddress(cellIndex + 1, rowNumber));
            var styleId = StyleId(cell);
            if (styleId != 0)
            {
                writer.WriteAttributeString("s", styleId.ToString(CultureInfo.InvariantCulture));
            }

            WriteCellValue(writer, cell.Value);
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static void WriteCellValue(XmlWriter writer, object value)
    {
        if (value is string text)
        {
            writer.WriteAttributeString("t", "inlineStr");
            writer.WriteStartElement("is", SpreadsheetNamespace);
            writer.WriteStartElement("t", SpreadsheetNamespace);
            if (text.StartsWith(' ') || text.EndsWith(' '))
            {
                writer.WriteAttributeString("space", XmlNamespace, "preserve");
            }

            writer.WriteString(text);
            writer.WriteEndElement();
            writer.WriteEndElement();
            return;
        }

        writer.WriteStartElement("v", SpreadsheetNamespace);
        switch (value)
        {
            case DateOnly date:
                writer.WriteString(date.ToDateTime(TimeOnly.MinValue).ToOADate().ToString(CultureInfo.InvariantCulture));
                break;
            case int integer:
                writer.WriteString(integer.ToString(CultureInfo.InvariantCulture));
                break;
            case long longValue:
                writer.WriteString(longValue.ToString(CultureInfo.InvariantCulture));
                break;
            case decimal decimalValue:
                writer.WriteString(decimalValue.ToString(CultureInfo.InvariantCulture));
                break;
            case double doubleValue:
                writer.WriteString(doubleValue.ToString(CultureInfo.InvariantCulture));
                break;
            default:
                throw new InvalidOperationException($"Unsupported executive workbook cell value type '{value.GetType().Name}'.");
        }

        writer.WriteEndElement();
    }

    private static void WriteStyles(XmlWriter writer)
    {
        writer.WriteStartElement("styleSheet", SpreadsheetNamespace);
        writer.WriteStartElement("numFmts", SpreadsheetNamespace);
        writer.WriteAttributeString("count", "3");
        WriteNumberFormat(writer, 164, "dd/MM/yyyy");
        WriteNumberFormat(writer, 165, "#,##0.00");
        WriteNumberFormat(writer, 166, "d");
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
        writer.WriteAttributeString("count", "3");
        WriteBorder(writer, null);
        WriteBorder(writer, "B7C9D6");
        WriteBorder(writer, "C00000");
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
        var styles = Enum.GetValues<ExecutiveWorkbookStyleToken>();
        var formats = Enum.GetValues<ExecutiveWorkbookNumberFormat>();
        writer.WriteAttributeString("count", (styles.Length * formats.Length * 2).ToString(CultureInfo.InvariantCulture));
        foreach (var isReportingBoundary in new[] { false, true })
        {
            foreach (var style in styles)
            {
                var definition = StyleDefinitionFor(style, isReportingBoundary);
                foreach (var format in formats)
                {
                    WriteCellFormat(writer, definition, NumberFormatId(format));
                }
            }
        }

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

    private static void WriteNumberFormat(XmlWriter writer, int id, string formatCode)
    {
        writer.WriteStartElement("numFmt", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", id.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("formatCode", formatCode);
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

    private static void WriteBorder(XmlWriter writer, string? color)
    {
        writer.WriteStartElement("border", SpreadsheetNamespace);
        foreach (var side in new[] { "left", "right", "top", "bottom" })
        {
            writer.WriteStartElement(side, SpreadsheetNamespace);
            writer.WriteAttributeString("style", color is null ? "none" : "thin");
            if (color is not null)
            {
                writer.WriteStartElement("color", SpreadsheetNamespace);
                writer.WriteAttributeString("rgb", color);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
        }

        writer.WriteStartElement("diagonal", SpreadsheetNamespace);
        writer.WriteEndElement();
        writer.WriteEndElement();
    }

    private static void WriteCellFormat(XmlWriter writer, StyleDefinition definition, int numberFormatId)
    {
        writer.WriteStartElement("xf", SpreadsheetNamespace);
        writer.WriteAttributeString("numFmtId", numberFormatId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fontId", definition.FontId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("fillId", definition.FillId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("borderId", definition.BorderId.ToString(CultureInfo.InvariantCulture));
        writer.WriteAttributeString("xfId", "0");
        if (definition.Wrap)
        {
            writer.WriteStartElement("alignment", SpreadsheetNamespace);
            writer.WriteAttributeString("wrapText", "1");
            writer.WriteAttributeString("vertical", "top");
            writer.WriteEndElement();
        }

        writer.WriteEndElement();
    }

    private static int StyleId(ExecutiveWorkbookCell cell)
    {
        var formatCount = Enum.GetValues<ExecutiveWorkbookNumberFormat>().Length;
        var styleCount = Enum.GetValues<ExecutiveWorkbookStyleToken>().Length;
        var styleOffset = cell.IsReportingBoundary ? styleCount : 0;
        return ((styleOffset + (int)cell.StyleToken) * formatCount) + (int)cell.NumberFormat;
    }

    private static int NumberFormatId(ExecutiveWorkbookNumberFormat format) => format switch
    {
        ExecutiveWorkbookNumberFormat.Text => 49,
        ExecutiveWorkbookNumberFormat.Date => 164,
        ExecutiveWorkbookNumberFormat.DayOfMonth => 166,
        ExecutiveWorkbookNumberFormat.Number => 4,
        ExecutiveWorkbookNumberFormat.Percentage => 10,
        ExecutiveWorkbookNumberFormat.Hours => 165,
        _ => throw new InvalidOperationException($"Unsupported executive workbook number format '{format}'.")
    };

    private static StyleDefinition StyleDefinitionFor(ExecutiveWorkbookStyleToken style, bool isReportingBoundary)
    {
        var definition = style switch
        {
            ExecutiveWorkbookStyleToken.Title => new StyleDefinition(1, 0, 0, false),
            ExecutiveWorkbookStyleToken.Subtitle => new StyleDefinition(0, 6, 1, true),
            ExecutiveWorkbookStyleToken.Header => new StyleDefinition(0, 6, 1, true),
            ExecutiveWorkbookStyleToken.Plan => new StyleDefinition(0, 2, 1, true),
            ExecutiveWorkbookStyleToken.ActualComplete => new StyleDefinition(0, 3, 1, true),
            ExecutiveWorkbookStyleToken.Forecast => new StyleDefinition(0, 4, 1, true),
            ExecutiveWorkbookStyleToken.Attention => new StyleDefinition(0, 4, 1, true),
            ExecutiveWorkbookStyleToken.BlockedOrOverdue => new StyleDefinition(0, 5, 1, true),
            ExecutiveWorkbookStyleToken.Unknown => new StyleDefinition(0, 6, 1, true),
            ExecutiveWorkbookStyleToken.Weekend => new StyleDefinition(0, 6, 1, true),
            ExecutiveWorkbookStyleToken.Milestone => new StyleDefinition(0, 2, 1, true),
            ExecutiveWorkbookStyleToken.ReportingBoundary => new StyleDefinition(0, 0, 2, true),
            ExecutiveWorkbookStyleToken.ProjectHierarchy => new StyleDefinition(0, 2, 1, true),
            ExecutiveWorkbookStyleToken.PhaseHierarchy => new StyleDefinition(0, 2, 1, true),
            ExecutiveWorkbookStyleToken.WorkPackageHierarchy => new StyleDefinition(0, 0, 0, true),
            ExecutiveWorkbookStyleToken.DeliveryCardHierarchy => new StyleDefinition(0, 0, 0, true),
            _ => new StyleDefinition(0, 0, 0, true)
        };

        return isReportingBoundary && style != ExecutiveWorkbookStyleToken.ReportingBoundary
            ? definition with { BorderId = 2 }
            : definition;
    }

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

    private sealed record StyleDefinition(int FontId, int FillId, int BorderId, bool Wrap);
}
