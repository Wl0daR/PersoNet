using System.Globalization;

namespace PersoNet.Application.Audit;

/// <summary>Polish names of the audited records and their fields, and readable forms of the stored values.</summary>
internal static class AuditTexts
{
    public const string AccountEntity = "Account";

    /// <summary>What HR did with the account, kept in the entry's state: a link, a block or an unblock.</summary>
    public const string AccountEventProperty = "Event";
    public const string AccountEventLink = "Link";
    public const string AccountEventBlock = "Block";
    public const string AccountEventUnblock = "Unblock";

    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    /// <summary>Fields shown in the history, in this order; technical ones (search text, timestamps) are left out.</summary>
    private static readonly Dictionary<string, (string Property, string Label)[]> Fields = new(StringComparer.Ordinal)
    {
        ["Employee"] =
        [
            ("Number", "Numer ewidencyjny"), ("FirstName", "Imię"), ("LastName", "Nazwisko"), ("Email", "E-mail służbowy"),
            ("Phone", "Telefon"), ("HireDate", "Data zatrudnienia"), ("TerminationDate", "Ostatni dzień pracy"),
            ("DepartmentId", "Dział"), ("PositionId", "Stanowisko"), ("ManagerId", "Przełożony"),
        ],
        ["EmploymentContract"] =
        [
            ("Type", "Rodzaj umowy"), ("StartDate", "Od"), ("EndDate", "Do"), ("WorkingTime", "Wymiar etatu"),
            ("TerminatedOn", "Rozwiązana z końcem zatrudnienia"),
        ],
        ["LeaveRequest"] =
        [
            ("Type", "Rodzaj"), ("StartDate", "Od"), ("EndDate", "Do"), ("Days", "Dni robocze"), ("Hours", "Godziny"),
            ("Note", "Uwagi"), ("Status", "Stan"), ("DecisionNote", "Uzasadnienie decyzji"),
        ],
        ["LeaveEntitlement"] = [("Year", "Rok"), ("Days", "Wymiar (dni)"), ("CarriedOverDays", "Zaległy z poprzedniego roku (dni)")],
        [AccountEntity] = [("State", "Stan konta")],
        ["Role"] = [("Name", "Nazwa"), ("Description", "Opis"), ("Permissions", "Uprawnienia")],
    };

    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
    {
        ["Type:Probation"] = "umowa o pracę na okres próbny",
        ["Type:FixedTerm"] = "umowa o pracę na czas określony",
        ["Type:Indefinite"] = "umowa o pracę na czas nieokreślony",
        ["Type:Mandate"] = "umowa zlecenie",
        ["Type:SpecificWork"] = "umowa o dzieło",
        ["Type:B2B"] = "współpraca B2B",
        ["Type:Annual"] = "urlop wypoczynkowy",
        ["Type:OnDemand"] = "urlop na żądanie",
        ["Type:Occasional"] = "urlop okolicznościowy",
        ["Type:Unpaid"] = "urlop bezpłatny",
        ["Type:ChildCare"] = "opieka nad dzieckiem (art. 188)",
        ["Type:ForceMajeure"] = "siła wyższa (art. 148¹)",
        ["Status:Pending"] = "czeka na decyzję",
        ["Status:Approved"] = "zatwierdzony",
        ["Status:Rejected"] = "odrzucony",
        ["Status:Cancelled"] = "anulowany",
        ["State:None"] = "brak konta",
        ["State:Invited"] = "czeka na ustawienie hasła",
        ["State:Active"] = "aktywne",
        ["State:Blocked"] = "zablokowane",
    };

    public static string Subject(string entityType) => entityType switch
    {
        "Employee" => "Kartoteka",
        "EmploymentContract" => "Umowa",
        "LeaveRequest" => "Wniosek urlopowy",
        "LeaveEntitlement" => "Wymiar urlopu",
        AccountEntity => "Konto w portalu",
        "Role" => "Rola",
        "RoleAssignment" => "Role i uprawnienia",
        _ => entityType,
    };

    public static IReadOnlyList<(string Property, string Label)> FieldsOf(string entityType) =>
        Fields.TryGetValue(entityType, out var fields) ? fields : [];

    /// <summary>The stored invariant text as a reader expects it: Polish dates and numbers, names of choices.</summary>
    public static string? Value(string property, string? stored)
    {
        if (stored is null)
        {
            return null;
        }

        if (property == "Permissions")
        {
            return string.Join(", ", stored.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(Access.PermissionCatalog.NameOf));
        }

        if (Values.TryGetValue($"{property}:{stored}", out var name))
        {
            return name;
        }

        if (DateOnly.TryParseExact(stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date.ToString("d MMMM yyyy", Polish);
        }

        if (property is "WorkingTime" or "Hours" && decimal.TryParse(stored, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            return number.ToString("0.##", Polish);
        }

        return stored;
    }
}
