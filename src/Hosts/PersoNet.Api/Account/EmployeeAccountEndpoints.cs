using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Accounts;

namespace PersoNet.Api.Account;

/// <summary>An employee's access to the portal, managed by HR from the employee card.</summary>
internal static class EmployeeAccountEndpoints
{
    public static RouteGroupBuilder MapEmployeeAccountEndpoints(this RouteGroupBuilder api)
    {
        var account = api.MapGroup("/employees/{id:guid}/account")
            .WithTags("Konta pracowników")
            .RequireAuthorization(Permissions.AccountsManage);

        account.MapGet("/", async (Guid id, AccountService accounts, CancellationToken cancellationToken) =>
                (await accounts.GetAsync(id, cancellationToken)).ToOk())
            .WithName("GetEmployeeAccount").WithSummary("Czy pracownik ma konto w portalu");

        account.MapPost("/link", async (Guid id, AccountService accounts, CancellationToken cancellationToken) =>
                (await accounts.CreateLinkAsync(id, cancellationToken)).ToOk())
            .WithName("CreateEmployeeAccountLink")
            .WithSummary("Link do ustawienia hasła (pierwszy zakłada konto, kolejny pozwala ustawić nowe hasło)");

        account.MapPost("/block", async (Guid id, AccountService accounts, CancellationToken cancellationToken) =>
                (await accounts.SetBlockedAsync(id, blocked: true, cancellationToken)).ToOk())
            .WithName("BlockEmployeeAccount").WithSummary("Zablokuj logowanie pracownika");

        account.MapPost("/unblock", async (Guid id, AccountService accounts, CancellationToken cancellationToken) =>
                (await accounts.SetBlockedAsync(id, blocked: false, cancellationToken)).ToOk())
            .WithName("UnblockEmployeeAccount").WithSummary("Odblokuj logowanie pracownika");

        return api;
    }
}
