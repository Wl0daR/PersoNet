using PersoNet.Application.Common;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Application.Employees;

internal static class ContractErrors
{
    public static readonly AppError NotFound =
        AppError.NotFound("contract.not_found", "Nie znaleziono umowy.");

    public static readonly AppError Overlap =
        AppError.Conflict("contract.overlap", "W tym okresie pracownik ma już inną umowę o pracę.");

    public static readonly AppError BeforeHire =
        AppError.Validation("contract.before_hire", "Umowa nie może zaczynać się przed datą zatrudnienia pracownika.");

    public static readonly AppError EndRequired =
        AppError.Validation("contract.end_required", "Umowa na okres próbny i na czas określony musi mieć datę końca.");

    public static readonly AppError NoEndForIndefinite =
        AppError.Validation("contract.no_end_for_indefinite", "Umowa na czas nieokreślony nie ma daty końca.");

    public static readonly AppError EndBeforeStart =
        AppError.Validation("contract.end_before_start", "Umowa nie może kończyć się przed początkiem.");

    public static readonly AppError ProbationTooLong =
        AppError.Validation("contract.probation_too_long", "Umowa na okres próbny może trwać najwyżej 3 miesiące.");

    public static readonly AppError WorkingTimeInvalid =
        AppError.Validation("contract.working_time_invalid", "Wymiar etatu musi być większy od 0 i nie większy niż 1, np. 1 albo 0,5.");

    public static AppError Invalid(DomainException exception) =>
        AppError.Validation("contract.invalid", exception.Message);
}
