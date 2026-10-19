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
            ("DepartmentId", "Dział"), ("PositionId", "Stanowisko"), ("ManagerId", "Przełożony"), ("ClockId", "Identyfikator RCP"),
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
        ["Absence"] = [("Type", "Rodzaj"), ("StartDate", "Od"), ("EndDate", "Do"), ("Note", "Uwagi")],
        ["MedicalExam"] = [("Type", "Rodzaj badania"), ("ExamDate", "Data badania"), ("ValidUntil", "Orzeczenie ważne do"), ("Note", "Uwagi")],
        ["EmployeeTraining"] = [("CourseId", "Szkolenie"), ("CompletedOn", "Data ukończenia"), ("ValidUntil", "Ważne do"), ("Note", "Uwagi")],
        [AccountEntity] = [("State", "Stan konta")],
        ["Role"] = [("Name", "Nazwa"), ("Description", "Opis"), ("Permissions", "Uprawnienia")],
        ["WorkDay"] =
        [
            ("Date", "Dzień"), ("StartTime", "Od"), ("EndTime", "Do"), ("BreakMinutes", "Przerwa (min)"), ("Absence", "Nieobecność"),
            ("Note", "Uwagi"), ("Origin", "Źródło"), ("Problem", "Problem z odbiciami"),
        ],
        ["CompanySettings"] =
        [
            ("Name", "Nazwa"), ("LegalName", "Pełna nazwa"), ("TaxId", "NIP"), ("Regon", "REGON"), ("Street", "Ulica i numer"),
            ("PostalCode", "Kod pocztowy"), ("City", "Miejscowość"), ("Email", "E-mail"), ("Phone", "Telefon"),
            ("PortalLeaveTypes", "Urlopy zgłaszane w portalu"), ("WorkTimeSource", "Źródło czasu pracy"),
        ],
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
        ["Type:Maternity"] = "urlop macierzyński",
        ["Type:Paternity"] = "urlop ojcowski",
        ["Type:Parental"] = "urlop rodzicielski",
        ["Type:ChildRearing"] = "urlop wychowawczy",
        ["Type:OvertimeTimeOff"] = "odbiór nadgodzin",
        ["Type:HolidayTimeOff"] = "odbiór dnia wolnego za święto",
        ["Type:PaidRelease"] = "zwolnienie płatne",
        ["Type:Sickness"] = "choroba (L4)",
        ["Type:ChildCareAllowance"] = "opieka nad chorym dzieckiem",
        ["Type:FamilyCareAllowance"] = "opieka nad chorym członkiem rodziny",
        ["Type:Rehabilitation"] = "świadczenie rehabilitacyjne",
        ["Type:WorkAccident"] = "choroba po wypadku przy pracy",
        ["Type:Unexcused"] = "nieobecność nieusprawiedliwiona",
        ["Type:OtherExcused"] = "inna nieobecność usprawiedliwiona",
        ["Type:BusinessTrip"] = "delegacja",
        ["Type:Training"] = "szkolenie",
        ["Type:MedicalExam"] = "badania lekarskie",
        ["Type:TradeFair"] = "targi",
        ["Type:RemoteWork"] = "praca zdalna (home office)",
        ["Type:Initial"] = "badanie wstępne",
        ["Type:Periodic"] = "badanie okresowe",
        ["Type:Control"] = "badanie kontrolne",
        ["Status:Pending"] = "czeka na decyzję",
        ["Status:Approved"] = "zatwierdzony",
        ["Status:Rejected"] = "odrzucony",
        ["Status:Cancelled"] = "anulowany",
        ["Absence:Sickness"] = "choroba (L4)",
        ["Absence:CareAllowance"] = "opieka (zasiłek opiekuńczy)",
        ["Absence:Excused"] = "nieobecność usprawiedliwiona",
        ["Absence:Unexcused"] = "nieobecność nieusprawiedliwiona",
        ["Origin:Manual"] = "wpis ręczny",
        ["Origin:Clock"] = "odbicia z czytnika",
        ["Problem:None"] = "brak",
        ["Problem:MissingOut"] = "brak wyjścia",
        ["Problem:MissingIn"] = "brak wejścia",
        ["Problem:TooLong"] = "ponad dobę między wejściem a wyjściem",
        ["WorkTimeSource:Manual"] = "wpis ręczny",
        ["WorkTimeSource:FileImport"] = "import plików",
        ["WorkTimeSource:RogerPrMaster"] = "Roger PR Master",
        ["WorkTimeSource:Api"] = "inny system przez API",
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
        "Absence" => "Nieobecność",
        "MedicalExam" => "Badanie lekarskie",
        "EmployeeTraining" => "Szkolenie",
        AccountEntity => "Konto w portalu",
        "Role" => "Rola",
        "RoleAssignment" => "Role i uprawnienia",
        "CompanySettings" => "Ustawienia firmy",
        "WorkDay" => "Czas pracy",
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

        if (property == "PortalLeaveTypes")
        {
            return string.Join(", ", stored.Split(", ", StringSplitOptions.RemoveEmptyEntries).Select(type => Values.GetValueOrDefault($"Type:{type}", type)));
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
