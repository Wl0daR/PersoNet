using System.Globalization;
using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.Leave;

internal static class LeaveErrors
{
    public static readonly AppError NotFound =
        AppError.NotFound("leave.not_found", "Nie znaleziono wniosku urlopowego.");

    public static readonly AppError EmployeeNotFound =
        AppError.Validation("leave.employee_not_found", "Wybierz pracownika z listy.");

    public static readonly AppError EndBeforeStart =
        AppError.Validation("leave.end_before_start", "Urlop nie może kończyć się przed pierwszym dniem.");

    public static readonly AppError CrossesYear =
        AppError.Validation("leave.crosses_year", "Urlop na przełomie lat podziel na dwa wnioski, osobno dla każdego roku.");

    public static readonly AppError NoWorkingDays =
        AppError.Validation("leave.no_working_days", "W wybranym okresie nie ma dni roboczych (same weekendy lub święta).");

    public static readonly AppError OutsideEmployment =
        AppError.Validation("leave.outside_employment", "Urlop musi przypadać w okresie zatrudnienia pracownika.");

    public static readonly AppError Overlap =
        AppError.Conflict("leave.overlap", "W tym okresie pracownik ma już inny oczekujący lub zatwierdzony urlop.");

    public static readonly AppError NoEntitlement =
        AppError.Validation("leave.no_entitlement", "Najpierw ustaw wymiar urlopu pracownika na ten rok.");

    public static readonly AppError NotEnoughDays =
        AppError.Validation("leave.not_enough_days", "Pracownikowi nie zostało tyle urlopu wypoczynkowego w tym roku.");

    public static readonly AppError OnDemandLimit =
        AppError.Validation("leave.on_demand_limit", "Urlop na żądanie to najwyżej 4 dni w roku.");

    public static readonly AppError ChildCareLimit =
        AppError.Validation("leave.child_care_limit", "Zwolnienie na opiekę nad dzieckiem to najwyżej 2 dni albo 16 godzin w roku (przy niepełnym etacie proporcjonalnie mniej).");

    public static readonly AppError ForceMajeureLimit =
        AppError.Validation("leave.force_majeure_limit", "Zwolnienie z powodu siły wyższej to najwyżej 2 dni albo 16 godzin w roku (przy niepełnym etacie proporcjonalnie mniej).");

    public static readonly AppError HoursNotAllowed =
        AppError.Validation("leave.hours_not_allowed", "Na godziny można wziąć tylko urlop wypoczynkowy, opiekę nad dzieckiem i zwolnienie z powodu siły wyższej.");

    public static readonly AppError HoursOneDay =
        AppError.Validation("leave.hours_one_day", "Urlop na godziny dotyczy jednego dnia.");

    public static readonly AppError HoursOnlyForRemainder =
        AppError.Validation("leave.hours_only_for_remainder", "Urlop wypoczynkowy na godziny można dać tylko wtedy, gdy zostało mniej niż jeden dzień urlopu (art. 154² Kodeksu pracy).");

    public static readonly AppError AlreadyDecided =
        AppError.Conflict("leave.already_decided", "Ten wniosek został już rozpatrzony.");

    public static readonly AppError NotActive =
        AppError.Conflict("leave.not_active", "Anulować można tylko wniosek oczekujący lub zatwierdzony.");

    public static readonly AppError ReasonRequired =
        AppError.Validation("leave.reason_required", "Podaj powód odrzucenia, żeby pracownik wiedział, co zmienić.");

    public static readonly AppError NoteTooLong =
        AppError.Validation("leave.note_too_long", "Uwagi mogą mieć najwyżej 500 znaków.");

    public static readonly AppError EntitlementInvalid =
        AppError.Validation("leave.entitlement_invalid", "Liczba dni musi być od 0 do 60.");

    public static readonly AppError MonthOutOfRange =
        AppError.Validation("leave.month_out_of_range", "Miesiąc musi być liczbą od 1 do 12.");

    public static readonly AppError DepartmentNotFound =
        AppError.NotFound("department.not_found", "Nie znaleziono działu.");

    public static readonly AppError YearOutOfRange =
        AppError.Validation("leave.year_out_of_range", "Rok musi być z zakresu 2000–2100.");

    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static AppError HoursInvalid(decimal dailyHours) =>
        AppError.Validation("leave.hours_invalid", string.Create(CultureInfo.GetCultureInfo("pl-PL"), $"Podaj liczbę godzin większą od 0 i nie większą niż dzień pracy pracownika ({dailyHours:0.##} godz.), w pełnych kwadransach."));

    public static AppError DaysOrHours(bool hourlyChosen) =>
        AppError.Validation("leave.days_or_hours", hourlyChosen
            ? "W tym roku pracownik korzysta z tego zwolnienia w godzinach, więc kolejny wniosek też musi być w godzinach."
            : "W tym roku pracownik korzysta z tego zwolnienia w dniach, więc kolejny wniosek też musi być w dniach.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("leave.invalid", exception.Message);
}
