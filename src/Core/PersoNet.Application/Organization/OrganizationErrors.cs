using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.Organization;

internal static class OrganizationErrors
{
    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static readonly AppError DepartmentNotFound =
        AppError.NotFound("department.not_found", "Nie znaleziono działu.");

    public static readonly AppError ParentNotFound =
        AppError.Validation("department.parent_not_found", "Nie znaleziono działu nadrzędnego.");

    public static readonly AppError DepartmentCodeTaken =
        AppError.Conflict("department.code_taken", "Dział o tym kodzie już istnieje.");

    public static readonly AppError DepartmentCycle =
        AppError.Validation("department.cycle", "Dział nie może podlegać samemu sobie ani swojemu poddziałowi.");

    public static readonly AppError DepartmentHasActiveChildren =
        AppError.Conflict("department.has_active_children", "Najpierw dezaktywuj poddziały tego działu.");

    public static readonly AppError PositionNotFound =
        AppError.NotFound("position.not_found", "Nie znaleziono stanowiska.");

    public static readonly AppError PositionNameTaken =
        AppError.Conflict("position.name_taken", "Stanowisko o tej nazwie już istnieje.");

    public static readonly AppError PositionDepartmentNotFound =
        AppError.Validation("position.department_not_found", "Nie znaleziono jednego z wybranych działów.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("organization.invalid", exception.Message);
}
