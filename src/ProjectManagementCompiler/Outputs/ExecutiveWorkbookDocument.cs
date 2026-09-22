using System.Collections.ObjectModel;

namespace ProjectManagementCompiler.Outputs;

internal sealed class ExecutiveWorkbookDocument
{
    public ExecutiveWorkbookDocument(IReadOnlyList<ExecutiveWorkbookWorksheet> sheets, int activeSheetIndex)
    {
        ArgumentNullException.ThrowIfNull(sheets);
        if (sheets.Count == 0)
        {
            throw new ArgumentException("An executive workbook document must contain at least one worksheet.", nameof(sheets));
        }

        if (activeSheetIndex < 0 || activeSheetIndex >= sheets.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(activeSheetIndex), "The active worksheet index must refer to an existing worksheet.");
        }

        if (sheets.Any(sheet => sheet is null))
        {
            throw new ArgumentException("An executive workbook document cannot contain a null worksheet.", nameof(sheets));
        }

        if (sheets.Select(sheet => sheet.Name).Distinct(StringComparer.Ordinal).Count() != sheets.Count)
        {
            throw new ArgumentException("Executive workbook worksheet names must be unique.", nameof(sheets));
        }

        Sheets = Copy(sheets);
        ActiveSheetIndex = activeSheetIndex;
    }

    public IReadOnlyList<ExecutiveWorkbookWorksheet> Sheets { get; }
    public int ActiveSheetIndex { get; }

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}

internal sealed class ExecutiveWorkbookWorksheet
{
    public ExecutiveWorkbookWorksheet(
        string name,
        IReadOnlyList<ExecutiveWorkbookRow> rows,
        IReadOnlyList<double> columnWidths,
        IReadOnlyList<ExecutiveWorkbookRange> mergedRanges,
        ExecutiveWorkbookPane freezePane,
        ExecutiveWorkbookPrintSettings printSettings,
        bool showGridLines,
        int zoomPercent,
        IReadOnlyList<ExecutiveWorkbookColumnGroup>? columnGroups = null,
        ExecutiveWorkbookRange? autoFilterRange = null,
        bool outlineSummaryBelow = true,
        bool outlineSummaryRight = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columnWidths);
        ArgumentNullException.ThrowIfNull(mergedRanges);
        ArgumentNullException.ThrowIfNull(freezePane);
        ArgumentNullException.ThrowIfNull(printSettings);
        columnGroups ??= Array.Empty<ExecutiveWorkbookColumnGroup>();
        if (columnWidths.Count == 0 || columnWidths.Any(width => double.IsNaN(width) || double.IsInfinity(width) || width <= 0d))
        {
            throw new ArgumentException("Every used or reserved worksheet column must have a positive finite width.", nameof(columnWidths));
        }

        if (zoomPercent != 100)
        {
            throw new ArgumentOutOfRangeException(nameof(zoomPercent), "Executive worksheets must open at 100% zoom.");
        }

        if (freezePane.FrozenRows > rows.Count || freezePane.FrozenColumns > columnWidths.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(freezePane), "Freeze panes must remain within the used worksheet range.");
        }

        if (mergedRanges.Any(range => range is null))
        {
            throw new ArgumentException("A worksheet cannot contain a null merged range.", nameof(mergedRanges));
        }

        var ranges = mergedRanges.ToArray();
        if (ranges.Any(range => range.EndRow > rows.Count || range.EndColumn > columnWidths.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(mergedRanges), "Merged ranges must remain within the used worksheet range.");
        }

        for (var first = 0; first < ranges.Length; first++)
        {
            for (var second = first + 1; second < ranges.Length; second++)
            {
                if (ranges[first].Overlaps(ranges[second]))
                {
                    throw new ArgumentException("Merged worksheet ranges must not overlap.", nameof(mergedRanges));
                }
            }
        }

        if (columnGroups.Any(group => group is null))
        {
            throw new ArgumentException("A worksheet cannot contain a null column group.", nameof(columnGroups));
        }

        var groups = columnGroups.ToArray();
        if (groups.Any(group => group.EndColumn > columnWidths.Count))
        {
            throw new ArgumentOutOfRangeException(nameof(columnGroups), "Column groups must remain within the used worksheet columns.");
        }

        for (var first = 0; first < groups.Length; first++)
        {
            for (var second = first + 1; second < groups.Length; second++)
            {
                if (groups[first].Overlaps(groups[second]))
                {
                    throw new ArgumentException("Worksheet column groups must not overlap.", nameof(columnGroups));
                }
            }
        }

        if (autoFilterRange is not null)
        {
            if (autoFilterRange.EndRow > rows.Count || autoFilterRange.EndColumn > columnWidths.Count)
            {
                throw new ArgumentOutOfRangeException(nameof(autoFilterRange), "The auto-filter range must remain within the used worksheet range.");
            }

            if (ranges.Any(autoFilterRange.Overlaps))
            {
                throw new ArgumentException("The auto-filter range must not intersect a merged range.", nameof(autoFilterRange));
            }
        }

        Name = name;
        Rows = Copy(rows);
        ColumnWidths = Copy(columnWidths);
        MergedRanges = Copy(ranges);
        FreezePane = freezePane;
        PrintSettings = printSettings;
        ShowGridLines = showGridLines;
        ZoomPercent = zoomPercent;
        ColumnGroups = Copy(groups);
        AutoFilterRange = autoFilterRange;
        OutlineSummaryBelow = outlineSummaryBelow;
        OutlineSummaryRight = outlineSummaryRight;
    }

    public string Name { get; }
    public IReadOnlyList<ExecutiveWorkbookRow> Rows { get; }
    public IReadOnlyList<double> ColumnWidths { get; }
    public IReadOnlyList<ExecutiveWorkbookRange> MergedRanges { get; }
    public ExecutiveWorkbookPane FreezePane { get; }
    public ExecutiveWorkbookPrintSettings PrintSettings { get; }
    public bool ShowGridLines { get; }
    public int ZoomPercent { get; }
    public IReadOnlyList<ExecutiveWorkbookColumnGroup> ColumnGroups { get; }
    public ExecutiveWorkbookRange? AutoFilterRange { get; }
    public bool OutlineSummaryBelow { get; }
    public bool OutlineSummaryRight { get; }

    private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) =>
        new ReadOnlyCollection<T>(values.ToArray());
}

internal sealed class ExecutiveWorkbookRow
{
    public ExecutiveWorkbookRow(
        IReadOnlyList<ExecutiveWorkbookCell> cells,
        int outlineLevel = 0,
        bool hidden = false,
        bool collapsed = false)
    {
        ArgumentNullException.ThrowIfNull(cells);
        if (cells.Any(cell => cell is null))
        {
            throw new ArgumentException("A worksheet row cannot contain a null cell.", nameof(cells));
        }

        if (outlineLevel is < 0 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(outlineLevel), "Worksheet row outline levels must be between zero and seven.");
        }

        Cells = new ReadOnlyCollection<ExecutiveWorkbookCell>(cells.ToArray());
        OutlineLevel = outlineLevel;
        Hidden = hidden;
        Collapsed = collapsed;
    }

    public IReadOnlyList<ExecutiveWorkbookCell> Cells { get; }
    public int OutlineLevel { get; }
    public bool Hidden { get; }
    public bool Collapsed { get; }
}

internal sealed class ExecutiveWorkbookCell
{
    public ExecutiveWorkbookCell(
        object value,
        ExecutiveWorkbookStyleToken styleToken,
        ExecutiveWorkbookNumberFormat numberFormat,
        bool isReportingBoundary = false)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value is not string and not DateOnly and not int and not long and not decimal and not double)
        {
            throw new ArgumentException("An executive workbook cell must contain reader-facing text, a date, or a supported numeric value.", nameof(value));
        }

        if (value is double doubleValue && (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue)))
        {
            throw new ArgumentOutOfRangeException(nameof(value), "An executive workbook numeric value must be finite.");
        }

        if (!Enum.IsDefined(styleToken) || !Enum.IsDefined(numberFormat))
        {
            throw new ArgumentOutOfRangeException(nameof(styleToken), "A workbook cell must use an approved semantic style and number format.");
        }

        Value = value;
        StyleToken = styleToken;
        NumberFormat = numberFormat;
        IsReportingBoundary = isReportingBoundary;
    }

    public object Value { get; }
    public ExecutiveWorkbookStyleToken StyleToken { get; }
    public ExecutiveWorkbookNumberFormat NumberFormat { get; }
    public bool IsReportingBoundary { get; }
}

internal sealed class ExecutiveWorkbookRange
{
    public ExecutiveWorkbookRange(int startRow, int startColumn, int endRow, int endColumn)
    {
        if (startRow < 1 || startColumn < 1 || endRow < startRow || endColumn < startColumn)
        {
            throw new ArgumentOutOfRangeException(nameof(startRow), "Worksheet ranges use one-based, ordered row and column coordinates.");
        }

        StartRow = startRow;
        StartColumn = startColumn;
        EndRow = endRow;
        EndColumn = endColumn;
    }

    public int StartRow { get; }
    public int StartColumn { get; }
    public int EndRow { get; }
    public int EndColumn { get; }

    public bool Overlaps(ExecutiveWorkbookRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StartRow <= other.EndRow
            && EndRow >= other.StartRow
            && StartColumn <= other.EndColumn
            && EndColumn >= other.StartColumn;
    }
}

internal sealed class ExecutiveWorkbookColumnGroup
{
    public ExecutiveWorkbookColumnGroup(
        int startColumn,
        int endColumn,
        int outlineLevel,
        bool hidden,
        bool collapsed)
    {
        if (startColumn < 1 || endColumn < startColumn)
        {
            throw new ArgumentOutOfRangeException(nameof(startColumn), "Column groups use one-based, ordered coordinates.");
        }

        if (outlineLevel is < 1 or > 7)
        {
            throw new ArgumentOutOfRangeException(nameof(outlineLevel), "Column-group outline levels must be between one and seven.");
        }

        StartColumn = startColumn;
        EndColumn = endColumn;
        OutlineLevel = outlineLevel;
        Hidden = hidden;
        Collapsed = collapsed;
    }

    public int StartColumn { get; }
    public int EndColumn { get; }
    public int OutlineLevel { get; }
    public bool Hidden { get; }
    public bool Collapsed { get; }

    public bool Overlaps(ExecutiveWorkbookColumnGroup other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return StartColumn <= other.EndColumn && EndColumn >= other.StartColumn;
    }
}

internal sealed class ExecutiveWorkbookPane
{
    public ExecutiveWorkbookPane(int frozenRows, int frozenColumns)
    {
        if (frozenRows < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frozenRows), "Frozen rows cannot be negative.");
        }

        if (frozenColumns < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(frozenColumns), "Frozen columns cannot be negative.");
        }

        FrozenRows = frozenRows;
        FrozenColumns = frozenColumns;
    }

    public int FrozenRows { get; }
    public int FrozenColumns { get; }
}

internal enum ExecutiveWorkbookPrintOrientation
{
    Landscape,
    Portrait
}

internal sealed class ExecutiveWorkbookPrintSettings
{
    public ExecutiveWorkbookPrintSettings(ExecutiveWorkbookPrintOrientation orientation, int fitToWidth, int fitToHeight)
    {
        if (!Enum.IsDefined(orientation))
        {
            throw new ArgumentOutOfRangeException(nameof(orientation), "A worksheet must use an approved print orientation.");
        }

        if (fitToWidth < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fitToWidth), "Print width fitting cannot be negative.");
        }

        if (fitToHeight < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(fitToHeight), "Print height fitting cannot be negative.");
        }

        Orientation = orientation;
        FitToWidth = fitToWidth;
        FitToHeight = fitToHeight;
    }

    public ExecutiveWorkbookPrintOrientation Orientation { get; }
    public int FitToWidth { get; }
    public int FitToHeight { get; }
}

internal enum ExecutiveWorkbookNumberFormat
{
    Text,
    Date,
    DayOfMonth,
    Number,
    Percentage,
    Hours
}

internal enum ExecutiveWorkbookStyleToken
{
    Default,
    Title,
    Subtitle,
    Header,
    Plan,
    ActualComplete,
    Forecast,
    Attention,
    BlockedOrOverdue,
    Unknown,
    Weekend,
    Milestone,
    ReportingBoundary,
    ProjectHierarchy,
    PhaseHierarchy,
    WorkPackageHierarchy,
    DeliveryCardHierarchy
}
