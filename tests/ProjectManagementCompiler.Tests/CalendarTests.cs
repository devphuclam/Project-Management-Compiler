using ProjectManagementCompiler.Domain;
using ProjectManagementCompiler.Management;

namespace ProjectManagementCompiler.Tests;

internal static class CalendarTests
{
    public static void ManagementCalendarUsesWeekdaysSignedVarianceAndWorkingDayDurations()
    {
        var calendar = new WorkingCalendar(new CalendarDefinition
        {
            WorkingWeekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            HoursPerWorkingDay = 8m
        });

        TestAssert.Equal(480, calendar.SignedWorkingMinutes(new DateOnly(2026, 9, 18), new DateOnly(2026, 9, 21)), "Friday-to-Monday variance must count one working day.");
        TestAssert.Equal(-480, calendar.SignedWorkingMinutes(new DateOnly(2026, 9, 21), new DateOnly(2026, 9, 18)), "Reverse working variance must be signed.");
        TestAssert.Equal(0, calendar.SignedWorkingMinutes(new DateOnly(2026, 9, 19), new DateOnly(2026, 9, 20)), "Weekend-only variance must contain no working minutes.");
        TestAssert.Equal(new DateOnly(2026, 9, 21), calendar.AddWorkingMinutes(new DateOnly(2026, 9, 19), 0), "Zero-duration work must normalize to the next working date.");
        TestAssert.Equal(new DateOnly(2026, 9, 22), calendar.AddWorkingMinutes(new DateOnly(2026, 9, 21), 480), "One working day must advance one working date.");
    }
}
