using ProjectManagementCompiler.Domain;

namespace ProjectManagementCompiler.Management;

public sealed class WorkingCalendar
{
    private readonly HashSet<DayOfWeek> workingWeekdays;
    private readonly int minutesPerWorkingDay;

    public WorkingCalendar(CalendarDefinition? definition = null)
    {
        var calendar = definition ?? new CalendarDefinition();
        workingWeekdays = calendar.WorkingWeekdays.Count == 0
            ? [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday]
            : calendar.WorkingWeekdays.ToHashSet();
        minutesPerWorkingDay = checked((int)Math.Round((calendar.HoursPerWorkingDay ?? 8m) * 60m, MidpointRounding.AwayFromZero));
    }

    public int? SignedWorkingMinutes(DateOnly? from, DateOnly? to)
    {
        if (from is null || to is null)
        {
            return null;
        }

        if (from == to)
        {
            return 0;
        }

        return to > from
            ? CountForward(from.Value, to.Value)
            : -CountForward(to.Value, from.Value);
    }

    public DateOnly AddWorkingMinutes(DateOnly start, int workingMinutes)
    {
        if (workingMinutes == 0)
        {
            return NormalizeWorkingDate(start, 1);
        }

        var direction = Math.Sign(workingMinutes);
        var workingDays = Math.Abs(workingMinutes) / minutesPerWorkingDay;
        var date = NormalizeWorkingDate(start, direction);
        for (var index = 0; index < workingDays; index++)
        {
            date = NormalizeWorkingDate(date.AddDays(direction), direction);
        }

        return date;
    }

    private int CountForward(DateOnly from, DateOnly to)
    {
        var workingDays = 0;
        for (var date = from; date < to; date = date.AddDays(1))
        {
            if (workingWeekdays.Contains(date.DayOfWeek))
            {
                workingDays++;
            }
        }

        return checked(workingDays * minutesPerWorkingDay);
    }

    private DateOnly NormalizeWorkingDate(DateOnly date, int direction)
    {
        while (!workingWeekdays.Contains(date.DayOfWeek))
        {
            date = date.AddDays(direction);
        }

        return date;
    }
}
