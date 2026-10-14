using PersoNet.Contracts.Audit;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeeHistoryViewModelTests
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private readonly FakeEmployeesApi _employees = new();

    [Fact]
    public async Task History_loads_when_the_tab_is_opened_and_can_show_one_kind_of_change()
    {
        var jan = _employees.Add("P001", "Jan", "Nowak");
        var at = new DateTimeOffset(2025, 5, 20, 9, 30, 0, TimeSpan.Zero);
        _employees.History[jan.Id] =
        [
            new(Guid.NewGuid(), at.AddHours(1), "Anna Kowalska", "Wniosek urlopowy", "Zatwierdzono wniosek: urlop bezpłatny, 2 czerwca 2025", HistoryAction.Changed,
                [new("Stan", "czeka na decyzję", "zatwierdzony")]),
            new(Guid.NewGuid(), at, "Anna Kowalska", "Kartoteka", "Założono kartotekę", HistoryAction.Created,
                [new("Imię", null, "Jan"), new("Telefon", null, null)]),
        ];
        var history = new EmployeeHistoryViewModel(_employees);

        await history.ShowAsync(jan, Cancellation);
        _employees.HistoryReads.ShouldBe(0);

        history.IsActive = true;

        _employees.HistoryReads.ShouldBe(1);
        history.Summary.ShouldBe("2 zmiany");
        history.Subjects.ShouldBe([EmployeeHistoryViewModel.AllSubjects, "Kartoteka", "Wniosek urlopowy"]);
        var approval = history.Items[0];
        approval.WhenAndWho.ShouldEndWith("· Anna Kowalska");
        approval.WhenAndWho.ShouldStartWith("20 maja 2025, ");
        approval.Changes.ShouldHaveSingleItem().Text.ShouldBe("Stan: czeka na decyzję → zatwierdzony");
        history.Items[1].Changes.Select(change => change.Text).ShouldBe(["Imię: Jan", "Telefon: (puste)"]);

        history.Subject = "Kartoteka";

        history.Items.ShouldHaveSingleItem().Description.ShouldBe("Założono kartotekę");
        history.Summary.ShouldBe("1 zmiana z 2");
    }

    [Fact]
    public async Task Employee_without_changes_gets_a_message_instead_of_an_empty_list()
    {
        var jan = _employees.Add("P001", "Jan", "Nowak");
        var history = new EmployeeHistoryViewModel(_employees) { IsActive = true };

        await history.ShowAsync(jan, Cancellation);

        history.IsEmpty.ShouldBeTrue();
        history.EmptyText.ShouldStartWith("Nie ma jeszcze zapisanych zmian.");
    }
}
