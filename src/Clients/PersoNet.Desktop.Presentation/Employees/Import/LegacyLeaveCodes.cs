using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Leave;

namespace PersoNet.Desktop.Presentation.Employees.Import;

/// <summary>What a code of the earlier system's <c>urlopy_typy</c> becomes in PersoNet: a kind of leave, an absence, or the yearly entitlement.</summary>
public sealed record LegacyLeaveCode(LeaveKind? Leave, AbsenceKind? Absence, bool IsEntitlement = false);

/// <summary>
/// The kinds of leave and absence of the earlier system, by their short name (<c>nazwa_skrot</c>). The numbers they have
/// in <c>absencja.idAbsencja</c> come from <c>urlopy_typy</c>; without that table the numbers of the "Kadry" database are used.
/// </summary>
public static class LegacyLeaveCodes
{
    public static readonly IReadOnlyDictionary<string, LegacyLeaveCode> ByShortName = new Dictionary<string, LegacyLeaveCode>(StringComparer.OrdinalIgnoreCase)
    {
        ["U"] = new(LeaveKind.Annual, null),
        ["UB"] = new(LeaveKind.Unpaid, null),
        ["UM"] = new(LeaveKind.Maternity, null),
        ["UOK"] = new(LeaveKind.Occasional, null),
        ["UOP"] = new(LeaveKind.ChildCare, null),
        ["UNŻ"] = new(LeaveKind.OnDemand, null),
        ["UON"] = new(LeaveKind.OvertimeTimeOff, null),
        ["UOŚ"] = new(LeaveKind.HolidayTimeOff, null),
        ["UOJ"] = new(LeaveKind.Paternity, null),
        ["URODZ"] = new(LeaveKind.Parental, null),
        ["UWYCH"] = new(LeaveKind.ChildRearing, null),
        ["SW"] = new(LeaveKind.ForceMajeure, null),
        ["ZP"] = new(LeaveKind.PaidRelease, null),
        ["R"] = new(null, null, IsEntitlement: true),
        ["CH"] = new(null, AbsenceKind.Sickness),
        ["CHOPD"] = new(null, AbsenceKind.ChildCareAllowance),
        ["CHOP"] = new(null, AbsenceKind.FamilyCareAllowance),
        ["ZRH"] = new(null, AbsenceKind.Rehabilitation),
        ["CHW"] = new(null, AbsenceKind.WorkAccident),
        ["NN"] = new(null, AbsenceKind.Unexcused),
        ["IN"] = new(null, AbsenceKind.OtherExcused),
        ["DEL"] = new(null, AbsenceKind.BusinessTrip),
        ["SZK"] = new(null, AbsenceKind.Training),
        ["BAD"] = new(null, AbsenceKind.MedicalExam),
        ["TAR"] = new(null, AbsenceKind.TradeFair),
        ["HO"] = new(null, AbsenceKind.RemoteWork),
    };

    /// <summary>The <c>urlopy_typy</c> of the "Kadry" database: number → short name.</summary>
    public static readonly IReadOnlyDictionary<string, string> DefaultNumbers = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["1"] = "U",
        ["2"] = "UB",
        ["3"] = "UM",
        ["4"] = "UOK",
        ["5"] = "UOP",
        ["6"] = "UNŻ",
        ["7"] = "UON",
        ["8"] = "UOŚ",
        ["9"] = "HO",
        ["10"] = "UOJ",
        ["11"] = "URODZ",
        ["12"] = "UWYCH",
        ["13"] = "SW",
        ["14"] = "R",
        ["15"] = "CH",
        ["16"] = "CHOPD",
        ["17"] = "CHOP",
        ["18"] = "ZRH",
        ["19"] = "CHW",
        ["21"] = "NN",
        ["22"] = "DEL",
        ["23"] = "SZK",
        ["24"] = "BAD",
        ["25"] = "IN",
        ["26"] = "TAR",
        ["27"] = "ZP",
    };
}
