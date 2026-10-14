using System.Text.Json.Serialization;
using PersoNet.Contracts.Leave;

namespace PersoNet.Contracts.WorkTime;

/// <summary>How the company records working time; sent as text, e.g. "RogerPrMaster".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<WorkTimeSourceKind>))]
public enum WorkTimeSourceKind
{
    /// <summary>HR types the hours, or fills the month from the planned hours and corrects it.</summary>
    Manual = 1,

    /// <summary>HR imports files exported from a time clock system.</summary>
    FileImport = 2,

    /// <summary>Events come from Roger PR Master on their own.</summary>
    RogerPrMaster = 3,

    /// <summary>Another time clock system sends events through the API.</summary>
    Api = 4,
}

/// <summary>An absence that is not leave; sent as text, e.g. "Sickness".</summary>
[JsonConverter(typeof(JsonStringEnumConverter<AbsenceKind>))]
public enum AbsenceKind
{
    /// <summary>Choroba (L4).</summary>
    Sickness = 1,

    /// <summary>Opieka z zasiłkiem opiekuńczym.</summary>
    CareAllowance = 2,

    /// <summary>Inna nieobecność usprawiedliwiona.</summary>
    Excused = 3,

    /// <summary>Nieobecność nieusprawiedliwiona.</summary>
    Unexcused = 4,
}

/// <summary>The kind of day in the basic working time system; sent as text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<TimesheetDayKind>))]
public enum TimesheetDayKind
{
    Working = 1,

    /// <summary>Saturday, the day off of the five-day week.</summary>
    DayOff = 2,

    Sunday = 3,

    /// <summary>A public holiday on a day other than Sunday.</summary>
    Holiday = 4,
}

/// <summary>What is wrong with the clock events of a day; sent as text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockIssue>))]
public enum ClockIssue
{
    None = 0,
    MissingOut = 1,
    MissingIn = 2,
    TooLong = 3,
}

/// <summary>Whether a person came in or went out; sent as text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockEventDirection>))]
public enum ClockEventDirection
{
    In = 1,
    Out = 2,
}

/// <summary>How events reached PersoNet; sent as text.</summary>
[JsonConverter(typeof(JsonStringEnumConverter<ClockEventOrigin>))]
public enum ClockEventOrigin
{
    Manual = 1,
    File = 2,
    Integration = 3,
}

/// <summary>A clock-in or clock-out; <see cref="OccurredAt"/> is the company's local time.</summary>
public sealed record ClockEventResponse(Guid Id, DateTime OccurredAt, ClockEventDirection Direction, ClockEventOrigin Origin, string? Device);

/// <summary>
/// One day of a timesheet. Hours are decimal hours (7.5 = 7 h 30 min). <see cref="IsRecorded"/> is false for a day with
/// nothing typed or clocked; <see cref="IsManual"/> marks a day HR typed or corrected, which clock events no longer change.
/// <see cref="Leave"/> is approved leave on the day, whose hours count in <see cref="AbsenceHours"/> like other absences.
/// </summary>
public sealed record TimesheetDay(
    DateOnly Date,
    TimesheetDayKind Kind,
    string? HolidayName,
    bool IsEmployed,
    decimal PlannedHours,
    TimeOnly? Start,
    TimeOnly? End,
    int BreakMinutes,
    decimal WorkedHours,
    decimal OvertimeHours,
    decimal OverPlanHours,
    decimal DayOffHours,
    decimal SundayHolidayHours,
    decimal NightHours,
    AbsenceKind? Absence,
    LeaveKind? Leave,
    decimal AbsenceHours,
    string? Note,
    bool IsRecorded,
    bool IsManual,
    ClockIssue Issue,
    IReadOnlyList<ClockEventResponse> Events);

/// <summary>
/// The month in figures. <see cref="NormHours"/> is the working time norm of the month (art. 130 KP) for the days of
/// employment; <see cref="PlannedHours"/> the hours planned on working days, which is less when a holiday falls on Saturday.
/// </summary>
public sealed record TimesheetTotals(
    decimal NormHours,
    decimal PlannedHours,
    decimal WorkedHours,
    decimal OvertimeHours,
    decimal OverPlanHours,
    decimal DayOffHours,
    decimal SundayHolidayHours,
    decimal NightHours,
    decimal LeaveHours,
    decimal AbsenceHours,
    int DaysWorked,
    int DaysWithIssues);

/// <summary>An employee's timesheet for a month ("karta ewidencji czasu pracy").</summary>
public sealed record TimesheetResponse(
    Guid EmployeeId,
    string Number,
    string FirstName,
    string LastName,
    string? ClockId,
    int Year,
    int Month,
    WorkTimeSourceKind Source,
    IReadOnlyList<TimesheetDay> Days,
    TimesheetTotals Totals);

/// <summary>A day typed by HR: hours worked (start, end, break in minutes) or an absence, not both.</summary>
public sealed record SaveWorkDayRequest(TimeOnly? Start, TimeOnly? End, int BreakMinutes, AbsenceKind? Absence, string? Note);

/// <summary>The employee's identifier in the time clock system (card number or user id); empty clears it.</summary>
public sealed record SaveClockIdRequest(string? ClockId);

/// <summary>
/// A clock event from a time clock system. The employee is found by <see cref="EmployeeId"/> or, more usually, by their
/// <see cref="ClockId"/> in that system. <see cref="OccurredAt"/> is the company's local time.
/// </summary>
public sealed record ClockEventInput(Guid? EmployeeId, string? ClockId, DateTime OccurredAt, ClockEventDirection Direction, string? Device);

/// <summary>Events to add, from a file HR imports or from a connected system. Events already stored are skipped.</summary>
public sealed record ImportClockEventsRequest(ClockEventOrigin Origin, IReadOnlyList<ClockEventInput> Events);

/// <summary>
/// What an import did: events added, events already there, identifiers no employee has (those events are left out), and
/// how many working days were worked out again.
/// </summary>
public sealed record ImportClockEventsResponse(int Received, int Added, int Duplicates, IReadOnlyList<string> UnknownClockIds, int DaysUpdated);
