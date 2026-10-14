using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Company;
using PersoNet.Contracts.Company;

namespace PersoNet.Api.Company;

/// <summary>
/// The company's data, leave and working time settings. Everyone in the company may read them (the portal shows only the kinds of
/// leave it allows); changing them needs <see cref="Permissions.SettingsManage"/>.
/// </summary>
internal static class CompanyEndpoints
{
    public static RouteGroupBuilder MapCompanyEndpoints(this RouteGroupBuilder api)
    {
        var company = api.MapGroup("/company").WithTags("Ustawienia firmy");

        company.MapGet("/", async (CompanyService company, CancellationToken cancellationToken) =>
                (await company.GetAsync(cancellationToken)).ToOk())
            .WithName("GetCompany").WithSummary("Dane firmy i ustawienia urlopów");

        company.MapPut("/details", async (SaveCompanyDetailsRequest request, CompanyService company, CancellationToken cancellationToken) =>
                (await company.SaveDetailsAsync(request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("SaveCompanyDetails").WithSummary("Zmień dane firmy");

        company.MapPut("/leave", async (SaveLeaveSettingsRequest request, CompanyService company, CancellationToken cancellationToken) =>
                (await company.SaveLeaveSettingsAsync(request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("SaveLeaveSettings").WithSummary("Ustal, o które rodzaje urlopów pracownicy proszą w portalu");

        company.MapPut("/work-time", async (SaveWorkTimeSettingsRequest request, CompanyService company, CancellationToken cancellationToken) =>
                (await company.SaveWorkTimeSettingsAsync(request, cancellationToken)).ToOk())
            .RequireAuthorization(Permissions.SettingsManage)
            .WithName("SaveWorkTimeSettings").WithSummary("Wybierz, skąd pochodzi czas pracy (ręcznie, import plików, Roger PR Master, API)");

        return api;
    }
}
