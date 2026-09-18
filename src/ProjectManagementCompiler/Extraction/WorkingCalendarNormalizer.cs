using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Extraction;

public sealed record WorkingDuration
{
    public int? WorkingMinutes { get; init; }
    public DataState State { get; init; } = DataState.Unknown;
    public ImportWarning? Diagnostic { get; init; }
}

public sealed class WorkingCalendarNormalizer
{
    public static IReadOnlyList<DayOfWeek> DefaultWorkingWeekdays { get; } =
        [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday];

    public WorkingDuration Normalize(DateOnly start, DateOnly finish) => Normalize(start, null, finish, null);

    public WorkingDuration Normalize(DateOnly start, string? startMarker, DateOnly finish, string? finishMarker)
    {
        if (finish < start)
        {
            return Invalid("AUTHORED_SCHEDULE_INVALID", "The authored finish date precedes the authored start date.");
        }

        var startPart = ParseMarker(startMarker, isFinish: false);
        var finishPart = ParseMarker(finishMarker, isFinish: true);
        if (startPart is null || finishPart is null)
        {
            return Invalid("AUTHORED_SCHEDULE_AMBIGUOUS", "An authored AM/PM marker is not recognized safely.");
        }

        if ((startMarker is null) != (finishMarker is null))
        {
            return Invalid("AUTHORED_SCHEDULE_AMBIGUOUS", "Authored AM/PM markers must be present for both schedule endpoints.");
        }

        var minutes = 0;
        for (var date = start; date <= finish; date = date.AddDays(1))
        {
            if (!DefaultWorkingWeekdays.Contains(date.DayOfWeek))
            {
                continue;
            }

            var dayStart = date == start ? startPart.Value : 0;
            var dayFinish = date == finish ? finishPart.Value : 8 * 60;
            if (date == start && date == finish && dayFinish <= dayStart)
            {
                return Invalid("AUTHORED_SCHEDULE_INVALID", "The authored AM/PM schedule has no positive working duration.");
            }

            minutes += Math.Max(0, dayFinish - dayStart);
        }

        return new WorkingDuration { WorkingMinutes = minutes, State = DataState.Known };
    }

    private static int? ParseMarker(string? marker, bool isFinish)
    {
        if (string.IsNullOrWhiteSpace(marker))
        {
            return isFinish ? 8 * 60 : 0;
        }

        return marker.Trim().ToUpperInvariant() switch
        {
            "AM" => isFinish ? 4 * 60 : 0,
            "PM" => isFinish ? 8 * 60 : 4 * 60,
            _ => null
        };
    }

    private static WorkingDuration Invalid(string code, string message) => new()
    {
        State = DataState.Invalid,
        Diagnostic = new ImportWarning
        {
            Id = code,
            Severity = WarningSeverity.Warning,
            Code = code,
            Message = message
        }
    };
}
