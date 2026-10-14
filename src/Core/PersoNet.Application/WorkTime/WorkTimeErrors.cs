using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.WorkTime;

internal static class WorkTimeErrors
{
    public static readonly AppError EmployeeNotFound =
        AppError.NotFound("worktime.employee_not_found", "Nie znaleziono pracownika.");

    public static readonly AppError YearOutOfRange =
        AppError.Validation("worktime.year_out_of_range", "Rok musi być z zakresu 2000–2100.");

    public static readonly AppError MonthOutOfRange =
        AppError.Validation("worktime.month_out_of_range", "Miesiąc musi być liczbą od 1 do 12.");

    public static readonly AppError NotEmployed =
        AppError.Validation("worktime.not_employed", "W tym dniu pracownik nie był zatrudniony.");

    public static readonly AppError WorkOrAbsence =
        AppError.Validation("worktime.work_or_absence", "Wpisz godziny pracy albo wybierz nieobecność, nie jedno i drugie.");

    public static readonly AppError TimesRequired =
        AppError.Validation("worktime.times_required", "Podaj godzinę rozpoczęcia i zakończenia pracy albo wybierz nieobecność.");

    public static readonly AppError SameTimes =
        AppError.Validation("worktime.same_times", "Praca musi kończyć się o innej godzinie, niż się zaczyna.");

    public static readonly AppError BreakInvalid =
        AppError.Validation("worktime.break_invalid", "Przerwa musi być krótsza niż czas od rozpoczęcia do zakończenia pracy (najwyżej 8 godzin).");

    public static readonly AppError UnknownAbsence =
        AppError.Validation("worktime.absence_unknown", "Wybrano nieznany rodzaj nieobecności.");

    public static readonly AppError LeaveOnDay =
        AppError.Conflict("worktime.leave_on_day", "W tym dniu pracownik ma urlop na cały dzień. Żeby wpisać ten dzień inaczej, najpierw anuluj urlop.");

    public static readonly AppError NoteTooLong =
        AppError.Validation("worktime.note_too_long", "Uwagi mogą mieć najwyżej 200 znaków.");

    public static readonly AppError ClockIdTaken =
        AppError.Conflict("worktime.clock_id_taken", "Ten identyfikator w systemie RCP ma już inny pracownik.");

    public static readonly AppError ClockIdInvalid =
        AppError.Validation("worktime.clock_id_invalid", "Identyfikator w systemie RCP może mieć najwyżej 50 znaków i nie może zawierać spacji.");

    public static readonly AppError ImportEmpty =
        AppError.Validation("worktime.import_empty", "Nie ma żadnych odbić do zapisania.");

    public static readonly AppError ImportTooLarge =
        AppError.Validation("worktime.import_too_large", "Jednorazowo można zapisać najwyżej 50 000 odbić. Podziel import na mniejsze części.");

    public static readonly AppError ImportOrigin =
        AppError.Validation("worktime.import_origin", "Import odbić pochodzi z pliku albo z połączonego systemu.");

    public static readonly AppError EventInvalid =
        AppError.Validation("worktime.event_invalid", "Każde odbicie musi mieć pracownika (identyfikator RCP), datę z lat 2000–2100 i kierunek: wejście albo wyjście.");

    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("worktime.invalid", exception.Message);
}
