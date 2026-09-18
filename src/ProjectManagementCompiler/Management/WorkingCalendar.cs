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
}
