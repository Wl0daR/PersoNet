using PersoNet.Application.Common;

namespace PersoNet.Application.Accounts;

internal static class AccountErrors
{
    public static readonly AppError EmployeeNotFound =
        AppError.NotFound("employee.not_found", "Nie znaleziono pracownika.");

    public static readonly AppError EmailRequired = AppError.Validation(
        "account.email_required",
        "Wpisz w kartotece służbowy e-mail pracownika. Tym adresem będzie się logować do portalu.");

    public static readonly AppError EmployeeTerminated = AppError.Validation(
        "account.employee_terminated",
        "Pracownik już nie pracuje w firmie, więc nie dostanie dostępu do portalu.");

    public static readonly AppError Blocked = AppError.Validation(
        "account.blocked",
        "Konto jest zablokowane. Najpierw je odblokuj.");

    public static readonly AppError NotFound =
        AppError.NotFound("account.not_found", "Pracownik nie ma jeszcze konta w portalu.");
}
