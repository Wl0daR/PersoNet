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
        AppError.Validation("leave.not_enough_days", "Pracownikowi nie zostało tyle dni urlopu wypoczynkowego w tym roku.");

    public static readonly AppError OnDemandLimit =
        AppError.Validation("leave.on_demand_limit", "Urlop na żądanie to najwyżej 4 dni w roku.");

    public static readonly AppError ChildCareLimit =
        AppError.Validation("leave.child_care_limit", "Zwolnienie na opiekę nad dzieckiem to najwyżej 2 dni w roku.");

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

    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("leave.invalid", exception.Message);
}
