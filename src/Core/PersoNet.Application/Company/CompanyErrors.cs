using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Company;

namespace PersoNet.Application.Company;

internal static class CompanyErrors
{
    public static readonly AppError NoTenant =
        AppError.Forbidden("tenant.missing", "Użytkownik nie jest przypisany do firmy.");

    public static readonly AppError NameRequired =
        AppError.Validation("company.name_required", "Podaj nazwę firmy.");

    public static readonly AppError NameTooLong =
        AppError.Validation("company.name_too_long", $"Nazwa firmy może mieć najwyżej {CompanySettings.NameMaxLength} znaków.");

    public static readonly AppError TaxIdInvalid =
        AppError.Validation("company.tax_id_invalid", "NIP jest niepoprawny. Wpisz 10 cyfr, na przykład 525-000-12-34.");

    public static readonly AppError RegonInvalid =
        AppError.Validation("company.regon_invalid", "REGON jest niepoprawny. Wpisz 9 lub 14 cyfr.");

    public static readonly AppError PostalCodeInvalid =
        AppError.Validation("company.postal_code_invalid", "Kod pocztowy wpisz w formie 00-000.");

    public static readonly AppError EmailInvalid =
        AppError.Validation("company.email_invalid", "Adres e-mail jest niepoprawny.");

    public static readonly AppError UnknownLeaveKind =
        AppError.Validation("company.leave_kind_unknown", "Wybrano nieznany rodzaj urlopu.");

    public static readonly AppError LeaveKindNotInPortal =
        AppError.Validation("leave.kind_not_in_portal", "O ten rodzaj urlopu prosi się w dziale kadr, nie w portalu.");

    public static readonly AppError UnknownWorkTimeSource =
        AppError.Validation("company.work_time_source_unknown", "Wybrano nieznane źródło czasu pracy.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("company.invalid", exception.Message);
}
