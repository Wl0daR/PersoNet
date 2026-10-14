using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.Access;

internal static class AccessErrors
{
    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static readonly AppError RoleNotFound =
        AppError.NotFound("role.not_found", "Nie znaleziono roli.");

    public static readonly AppError NameTaken =
        AppError.Conflict("role.name_taken", "Rola o tej nazwie już istnieje.");

    public static readonly AppError InUse =
        AppError.Conflict("role.in_use", "Tę rolę mają pracownicy. Najpierw odbierz ją w ich kartotekach (zakładka Konto).");

    public static readonly AppError EmployeeNotFound =
        AppError.NotFound("employee.not_found", "Nie znaleziono pracownika.");

    public static readonly AppError UnknownRole =
        AppError.Validation("role.unknown", "Jednej z wybranych ról już nie ma. Odśwież listę.");

    public static readonly AppError NameRequired =
        AppError.Validation("role.name_required", "Podaj nazwę roli.");

    public static readonly AppError NameTooLong =
        AppError.Validation("role.name_too_long", $"Nazwa roli może mieć najwyżej {Domain.Access.Role.NameMaxLength} znaków.");

    public static AppError UnknownPermission(string code) =>
        AppError.Validation("role.permission_unknown", $"Nie można nadać uprawnienia „{PermissionCatalog.NameOf(code)}” w roli.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("role.invalid", exception.Message);
}
