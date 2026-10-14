using System.Net;
using System.Net.Http.Json;
using PersoNet.Contracts.Account;
using PersoNet.Contracts.Employees;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class EmployeeAccountEndpointTests(PersoNetApiFactory factory)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Hr_creates_the_account_gets_a_link_and_can_block_and_unblock_it()
    {
        using var hr = factory.CreateSignedInClient();
        var employee = await HireAsync(hr, Email("jan"));

        var before = await hr.GetFromJsonAsync<EmployeeAccountResponse>(Account(employee.Id), Cancellation);
        using var created = await hr.PostAsync(Account(employee.Id, "link"), null, Cancellation);
        var link = (await created.Content.ReadFromJsonAsync<AccountLinkResponse>(Cancellation))!;
        using var blocked = await hr.PostAsync(Account(employee.Id, "block"), null, Cancellation);
        using var linkWhileBlocked = await hr.PostAsync(Account(employee.Id, "link"), null, Cancellation);
        using var unblocked = await hr.PostAsync(Account(employee.Id, "unblock"), null, Cancellation);

        before!.State.ShouldBe(AccountState.None);
        before.Email.ShouldBe(employee.Email);
        created.StatusCode.ShouldBe(HttpStatusCode.OK, await created.Content.ReadAsStringAsync(Cancellation));
        link.Account.State.ShouldBe(AccountState.Invited);
        link.Link.AbsoluteUri.ShouldContain("/account/activate");
        (await blocked.Content.ReadFromJsonAsync<EmployeeAccountResponse>(Cancellation))!.State.ShouldBe(AccountState.Blocked);
        await ShouldFailAsync(linkWhileBlocked, HttpStatusCode.BadRequest, "account.blocked");
        (await unblocked.Content.ReadFromJsonAsync<EmployeeAccountResponse>(Cancellation))!.State.ShouldBe(AccountState.Active);
    }

    [Fact]
    public async Task Account_needs_a_work_email_and_a_current_employee()
    {
        using var hr = factory.CreateSignedInClient();
        var withoutEmail = await HireAsync(hr, email: null);
        var former = await HireAsync(hr, Email("ewa"));
        using var terminated = await hr.PostAsJsonAsync(
            new Uri($"/api/v1/employees/{former.Id}/terminate", UriKind.Relative), new TerminateEmployeeRequest(new DateOnly(2024, 12, 31)), Cancellation);
        terminated.StatusCode.ShouldBe(HttpStatusCode.OK, await terminated.Content.ReadAsStringAsync(Cancellation));

        using var noEmail = await hr.PostAsync(Account(withoutEmail.Id, "link"), null, Cancellation);
        using var gone = await hr.PostAsync(Account(former.Id, "link"), null, Cancellation);
        using var blockNothing = await hr.PostAsync(Account(withoutEmail.Id, "block"), null, Cancellation);

        await ShouldFailAsync(noEmail, HttpStatusCode.BadRequest, "account.email_required");
        await ShouldFailAsync(gone, HttpStatusCode.BadRequest, "account.employee_terminated");
        await ShouldFailAsync(blockNothing, HttpStatusCode.NotFound, "account.not_found");
    }

    [Fact]
    public async Task Email_used_by_another_company_is_refused_and_employees_cannot_manage_accounts()
    {
        using var hr = factory.CreateSignedInClient();
        var email = Email("ola");
        var employee = await HireAsync(hr, email);
        factory.Accounts.TakenElsewhere.Add(email);
        using var portal = factory.CreatePortalClient(email);

        using var taken = await hr.PostAsync(Account(employee.Id, "link"), null, Cancellation);
        using var notHr = await portal.PostAsync(Account(employee.Id, "link"), null, Cancellation);
        using var otherCompany = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        using var foreign = await otherCompany.GetAsync(Account(employee.Id), Cancellation);

        await ShouldFailAsync(taken, HttpStatusCode.Conflict, "account.email_taken");
        notHr.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Uri Account(Guid employeeId, string? action = null) =>
        new($"/api/v1/employees/{employeeId}/account{(action is null ? string.Empty : "/" + action)}", UriKind.Relative);

    private static string Email(string name) => $"{name}-{Guid.NewGuid():N}@acme.test";

    private static async Task<EmployeeResponse> HireAsync(HttpClient hr, string? email)
    {
        var number = "K" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var response = await hr.PostAsJsonAsync(
            new Uri("/api/v1/employees", UriKind.Relative),
            new SaveEmployeeRequest(number, "Jan", "Kontowy", email, null, new DateOnly(2024, 3, 1), null, null, null),
            Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }

    private static async Task ShouldFailAsync(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        var body = await response.Content.ReadAsStringAsync(Cancellation);
        response.StatusCode.ShouldBe(status, body);
        body.ShouldContain(code);
    }
}
