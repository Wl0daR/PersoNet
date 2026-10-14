using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PersoNet.Contracts.Audit;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Infrastructure.Persistence;

namespace PersoNet.Api.IntegrationTests;

[Collection(ApiTestGroup.Name)]
public sealed class AuditEndpointTests(PersoNetApiFactory factory)
{
    private const string Employees = "/api/v1/employees";

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Every_change_of_the_card_leave_and_account_is_in_the_history_with_who_made_it()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var boss = await HireAsync(hr, new SaveEmployeeRequest("B" + tag, "Ewa", "Szefowa", null, null, new DateOnly(2024, 1, 1), null, null, null));
        var request = new SaveEmployeeRequest("H" + tag, "Jan", "Historyczny", $"jan-{tag}@acme.test", "600100200", new DateOnly(2024, 3, 1), null, null, null);
        var employee = await HireAsync(hr, request);

        using var changed = await hr.PutAsJsonAsync(
            new Uri($"{Employees}/{employee.Id}", UriKind.Relative), request with { Phone = "600100300", ManagerId = boss.Id }, Cancellation);
        changed.StatusCode.ShouldBe(HttpStatusCode.OK, await changed.Content.ReadAsStringAsync(Cancellation));
        using var unchanged = await hr.PutAsJsonAsync(
            new Uri($"{Employees}/{employee.Id}", UriKind.Relative), request with { Phone = "600100300", ManagerId = boss.Id }, Cancellation);

        using var created = await hr.PostAsJsonAsync(
            new Uri("/api/v1/leave-requests", UriKind.Relative),
            new CreateLeaveRequest(employee.Id, LeaveKind.Unpaid, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 4), null),
            Cancellation);
        created.StatusCode.ShouldBe(HttpStatusCode.Created, await created.Content.ReadAsStringAsync(Cancellation));
        var leave = (await created.Content.ReadFromJsonAsync<LeaveRequestResponse>(Cancellation))!;
        using var approved = await hr.PostAsJsonAsync(
            new Uri($"/api/v1/leave-requests/{leave.Id}/approve", UriKind.Relative), new LeaveDecisionRequest(null), Cancellation);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK, await approved.Content.ReadAsStringAsync(Cancellation));

        using var link = await hr.PostAsync(new Uri($"{Employees}/{employee.Id}/account/link", UriKind.Relative), null, Cancellation);
        using var block = await hr.PostAsync(new Uri($"{Employees}/{employee.Id}/account/block", UriKind.Relative), null, Cancellation);
        link.StatusCode.ShouldBe(HttpStatusCode.OK, await link.Content.ReadAsStringAsync(Cancellation));
        block.StatusCode.ShouldBe(HttpStatusCode.OK, await block.Content.ReadAsStringAsync(Cancellation));

        var history = (await hr.GetFromJsonAsync<List<HistoryEntryResponse>>(new Uri($"{Employees}/{employee.Id}/history", UriKind.Relative), Cancellation))!;

        history.Select(entry => entry.Description).ShouldBe(
        [
            "Zablokowano konto w portalu",
            "Utworzono konto w portalu i link do ustawienia hasła",
            "Zatwierdzono wniosek: urlop bezpłatny, 2 czerwca 2025 – 4 czerwca 2025",
            "Złożono wniosek: urlop bezpłatny, 2 czerwca 2025 – 4 czerwca 2025",
            "Zmieniono dane",
            "Założono kartotekę",
        ]);
        history.ShouldAllBe(entry => entry.UserName == "Anna Kowalska");
        history.Select(entry => entry.OccurredAt).ShouldBeInOrder(SortDirection.Descending);

        var cardChange = history[4];
        cardChange.Subject.ShouldBe("Kartoteka");
        cardChange.Changes.ShouldBe(
        [
            new HistoryFieldChange("Telefon", "600100200", "600100300"),
            new HistoryFieldChange("Przełożony", null, "Ewa Szefowa"),
        ]);
        history[2].Changes.ShouldBe([new HistoryFieldChange("Stan", "czeka na decyzję", "zatwierdzony")]);
        history[0].Changes.ShouldBe([new HistoryFieldChange("Stan konta", "czeka na ustawienie hasła", "zablokowane")]);
        history[5].Changes.ShouldContain(new HistoryFieldChange("Data zatrudnienia", null, "1 marca 2024"));
    }

    [Fact]
    public async Task History_is_for_hr_of_the_same_company_only()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var email = $"ola-{tag}@acme.test";
        var employee = await HireAsync(hr, new SaveEmployeeRequest("O" + tag, "Ola", "Prywatna", email, null, new DateOnly(2024, 1, 1), null, null, null));
        using var portal = factory.CreatePortalClient(email);
        using var otherCompany = factory.CreateSignedInClient(await factory.CreateTenantAsync());
        var history = new Uri($"{Employees}/{employee.Id}/history", UriKind.Relative);

        using var own = await portal.GetAsync(history, Cancellation);
        using var foreign = await otherCompany.GetAsync(history, Cancellation);

        own.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        foreign.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Database_refuses_to_change_or_remove_audit_entries()
    {
        using var hr = factory.CreateSignedInClient();
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var employee = await HireAsync(hr, new SaveEmployeeRequest("A" + tag, "Adam", "Trwały", null, null, new DateOnly(2024, 1, 1), null, null, null));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PersoNetDbContext>();

        var update = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE audit_log SET user_name = 'Ktoś inny' WHERE employee_id = {employee.Id}", Cancellation));
        var delete = await Should.ThrowAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"DELETE FROM audit_log WHERE employee_id = {employee.Id}", Cancellation));

        update.MessageText.ShouldContain("append-only");
        delete.MessageText.ShouldContain("append-only");
    }

    private static async Task<EmployeeResponse> HireAsync(HttpClient hr, SaveEmployeeRequest request)
    {
        using var response = await hr.PostAsJsonAsync(new Uri(Employees, UriKind.Relative), request, Cancellation);
        response.StatusCode.ShouldBe(HttpStatusCode.Created, await response.Content.ReadAsStringAsync(Cancellation));

        return (await response.Content.ReadFromJsonAsync<EmployeeResponse>(Cancellation))!;
    }
}
