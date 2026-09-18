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

    public static void InclusiveFinishOffsetsRespectPartialDaysFridaysAndWeekends()
    {
        var calendar = new WorkingCalendar(new CalendarDefinition
        {
            WorkingWeekdays = [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            HoursPerWorkingDay = 8m
        });
        var monday = new DateOnly(2026, 9, 21);

        TestAssert.Equal(monday, calendar.DateForFinishOffset(monday, 0), "Zero-duration finish must remain on the normalized anchor date.");
        TestAssert.Equal(monday, calendar.DateForFinishOffset(monday, 240), "A half-day finish must remain on the first working date.");
        TestAssert.Equal(monday, calendar.DateForFinishOffset(monday, 480), "An exactly one-day finish must use inclusive finish semantics.");
        TestAssert.Equal(new DateOnly(2026, 9, 22), calendar.DateForFinishOffset(monday, 720), "A one-and-a-half-day finish must use the second working date.");
        TestAssert.Equal(new DateOnly(2026, 9, 22), calendar.DateForFinishOffset(monday, 960), "An exactly two-day finish must remain on the second working date.");

        TestAssert.Equal(new DateOnly(2026, 9, 18), calendar.DateForFinishOffset(new DateOnly(2026, 9, 18), 480), "A Friday one-day finish must remain on Friday.");
        TestAssert.Equal(new DateOnly(2026, 9, 21), calendar.DateForFinishOffset(new DateOnly(2026, 9, 18), 960), "A Friday two-day finish must roll to Monday.");
        TestAssert.Equal(new DateOnly(2026, 9, 21), calendar.DateForFinishOffset(new DateOnly(2026, 9, 19), 480), "A weekend anchor must normalize before applying duration.");
        TestAssert.Equal(new DateOnly(2026, 9, 22), calendar.DateForFinishOffset(new DateOnly(2026, 9, 19), 960), "A weekend two-day finish must use Monday and Tuesday.");
    }
}
