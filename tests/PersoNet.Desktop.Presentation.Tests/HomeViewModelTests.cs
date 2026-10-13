using Microsoft.Extensions.Time.Testing;
using PersoNet.Desktop.Presentation.Home;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class HomeViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Start_screen_counts_people_and_the_active_structure()
    {
        var employees = new FakeEmployeesApi();
        employees.Add("P001", "Anna", "Zielińska");
        var former = employees.Add("P002", "Jan", "Nowak", new DateOnly(2020, 1, 1));
        await employees.TerminateAsync(former.Id, new DateOnly(2024, 12, 31), Cancellation);
        var organization = new FakeOrganizationApi();
        organization.Add("Dział IT", "IT");
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 9, 0, 0, TimeSpan.Zero));
        var viewModel = new HomeViewModel(employees, organization, new FakeLeaveApi(), time) { UserName = "Anna Kowalska" };

        await viewModel.LoadAsync(Cancellation);

        viewModel.EmployedCount.ShouldBe(1);
        viewModel.FormerCount.ShouldBe(1);
        viewModel.DepartmentCount.ShouldBe(1);
        viewModel.PositionCount.ShouldBe(0);
        viewModel.IsLoaded.ShouldBeTrue();
        viewModel.Greeting.ShouldBe("Dzień dobry, Anna");
        viewModel.Today.ShouldBe("wtorek, 13 października 2026");
    }

    [Fact]
    public void Evening_greeting_after_six()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 10, 13, 19, 0, 0, TimeSpan.Zero));
        time.SetLocalTimeZone(TimeZoneInfo.Utc);

        new HomeViewModel(new FakeEmployeesApi(), new FakeOrganizationApi(), new FakeLeaveApi(), time).Greeting.ShouldBe("Dobry wieczór");
    }
}
