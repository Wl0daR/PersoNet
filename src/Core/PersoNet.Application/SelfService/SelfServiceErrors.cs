using PersoNet.Application.Common;

namespace PersoNet.Application.SelfService;

internal static class SelfServiceErrors
{
    public static readonly AppError NotLinked = AppError.Forbidden(
        "self.not_linked",
        "Twoje konto nie jest połączone z kartoteką pracownika. Poproś dział kadr, aby wpisał w kartotece Twój służbowy e-mail.");

    public static readonly AppError Ambiguous = AppError.Forbidden(
        "self.ambiguous",
        "Twój e-mail jest wpisany w kilku kartotekach. Poproś dział kadr o poprawienie danych.");

    public static readonly AppError RequestNotFound =
        AppError.NotFound("leave.not_found", "Nie znaleziono wniosku urlopowego.");

    public static readonly AppError OnlyPendingCanBeWithdrawn =
        AppError.Validation("self.not_pending", "Wycofać można tylko wniosek, który czeka na decyzję. W sprawie zatwierdzonego urlopu skontaktuj się z działem kadr.");
}
