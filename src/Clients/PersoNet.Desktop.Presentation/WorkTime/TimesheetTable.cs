using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.Reports;

namespace PersoNet.Desktop.Presentation.WorkTime;

/// <summary>The month's timesheet as a sheet of a workbook: a row per day and the totals at the bottom.</summary>
public static class TimesheetTable
{
    public static ReportTable For(TimesheetResponse sheet)
    {
        ArgumentNullException.ThrowIfNull(sheet);

        var columns = new ReportColumn[]
        {
            new("Data", ReportFormat.Date),
            new("Dzień"),
            new("Od"),
            new("Do"),
            new("Przerwa (min)", ReportFormat.WholeNumber),
            new("Plan (godz.)", ReportFormat.Number),
            new("Przepracowano (godz.)", ReportFormat.Number),
            new("Nadgodziny (godz.)", ReportFormat.Number),
            new("Ponad etat (godz.)", ReportFormat.Number),
            new("W sobotę (godz.)", ReportFormat.Number),
            new("W niedzielę i święto (godz.)", ReportFormat.Number),
            new("W nocy (godz.)", ReportFormat.Number),
            new("Nieobecność"),
            new("Nieobecność (godz.)", ReportFormat.Number),
            new("Uwagi"),
        };

        var rows = sheet.Days.Select(day => (IReadOnlyList<object?>)
        [
            day.Date,
            WorkTimeTexts.ShortDay(day.Date),
            day.Start is { } start ? WorkTimeTexts.Time(start) : null,
            day.End is { } end ? WorkTimeTexts.Time(end) : null,
            day.BreakMinutes > 0 ? day.BreakMinutes : null,
            NullIfZero(day.PlannedHours),
            NullIfZero(day.WorkedHours),
            NullIfZero(day.OvertimeHours),
            NullIfZero(day.OverPlanHours),
            NullIfZero(day.DayOffHours),
            NullIfZero(day.SundayHolidayHours),
            NullIfZero(day.NightHours),
            day.Absence is { } absence ? WorkTimeTexts.AbsenceName(absence) : day.Leave is { } leave ? LeaveTexts.KindName(leave) : null,
            NullIfZero(day.AbsenceHours),
            string.Join("; ", new[] { day.HolidayName, WorkTimeTexts.IssueText(day.Issue), day.Note }.Where(part => !string.IsNullOrEmpty(part))),
        ]).ToList();

        var totals = sheet.Totals;
        rows.Add(
        [
            null, "Razem", null, null, null, totals.PlannedHours, totals.WorkedHours, totals.OvertimeHours, totals.OverPlanHours, totals.DayOffHours,
            totals.SundayHolidayHours, totals.NightHours, null, totals.LeaveHours + totals.AbsenceHours,
            $"Wymiar czasu pracy: {WorkTimeTexts.Total(totals.NormHours)} godz.",
        ]);

        var month = LeaveCalendar.MonthTitle(new DateOnly(sheet.Year, sheet.Month, 1));
        return new ReportTable(
            "Karta ewidencji",
            $"Karta ewidencji czasu pracy – {sheet.FirstName} {sheet.LastName} ({sheet.Number})",
            $"{month} · wymiar {WorkTimeTexts.Total(totals.NormHours)} godz. · źródło: {WorkTimeTexts.SourceName(sheet.Source).ToLowerInvariant()}",
            columns,
            rows);
    }

    private static decimal? NullIfZero(decimal hours) => hours == 0 ? null : hours;
}
