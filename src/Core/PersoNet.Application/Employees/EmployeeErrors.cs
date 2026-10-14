using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.Employees;

internal static class EmployeeErrors
{
    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static readonly AppError NotFound =
        AppError.NotFound("employee.not_found", "Nie znaleziono pracownika.");

    public static readonly AppError NumberTaken =
        AppError.Conflict("employee.number_taken", "Pracownik o tym numerze ewidencyjnym już istnieje.");

    public static readonly AppError EmailTaken =
        AppError.Conflict("employee.email_taken", "Ten adres e-mail ma już inny pracownik.");

    public static readonly AppError DepartmentNotFound =
        AppError.Validation("employee.department_not_found", "Nie znaleziono wybranego działu.");

    public static readonly AppError PositionNotFound =
        AppError.Validation("employee.position_not_found", "Nie znaleziono wybranego stanowiska.");

    public static readonly AppError ManagerNotFound =
        AppError.Validation("employee.manager_not_found", "Nie znaleziono wybranego przełożonego.");

    public static readonly AppError ManagerCycle =
        AppError.Validation("employee.manager_cycle", "Przełożonym nie może być ten pracownik ani nikt z jego podwładnych.");

    public static readonly AppError TerminationBeforeHire =
        AppError.Validation("employee.termination_before_hire", "Data zakończenia pracy nie może być wcześniejsza niż data zatrudnienia.");

    public static readonly AppError HireAfterTermination =
        AppError.Validation("employee.hire_after_termination", "Data zatrudnienia nie może być późniejsza niż data zakończenia pracy.");

    public static readonly AppError NotTerminated =
        AppError.Validation("employee.not_terminated", "Ponownie zatrudnić można tylko osobę, której zatrudnienie się zakończyło.");

    public static readonly AppError RehireBeforeTermination =
        AppError.Validation("employee.rehire_before_termination", "Nowa data zatrudnienia musi być późniejsza niż ostatni dzień poprzedniej pracy.");

    public static readonly AppError ReinstateOverlap =
        AppError.Conflict(
            "employee.reinstate_overlap",
            "Nie można cofnąć zakończenia zatrudnienia, bo pracownik ma już nową umowę. Jeśli wraca do pracy, użyj „Zatrudnij ponownie”.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("employee.invalid", exception.Message);
}
