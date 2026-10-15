using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Absences;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Absences;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeeAbsencesViewModelTests
{
    private static readonly EmployeeResponse Employee = new(Guid.CreateVersion7(), "P001", "Jan", "Nowak", null, null, new DateOnly(2020, 1, 2), null, null, null, null);

    private readonly FakeAbsencesApi _api = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 16, 9, 0, 0, TimeSpan.Zero));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_year_of_absences_is_listed_with_days_by_kind()
    {
        Add(AbsenceKind.Sickness, new DateOnly(2024, 12, 30), new DateOnly(2025, 1, 5));
        Add(AbsenceKind.RemoteWork, new DateOnly(2025, 3, 3), new DateOnly(2025, 3, 3));
        Add(AbsenceKind.Training, new DateOnly(2024, 5, 6), new DateOnly(2024, 5, 6));
        var tab = new EmployeeAbsencesViewModel(_api, _time, _dialogs, canManage: true);

        await tab.ShowAsync(Employee, Cancellation);

        tab.Year.ShouldBe(2025);
        tab.Items.Select(row => row.KindName).ShouldBe(["Praca zdalna (home office)", "Choroba (L4)"]);
        tab.Items[1].Period.ShouldBe("30 gru 2024 – 5 sty 2025");
        tab.Summary.ShouldBe("Choroba (L4): 5 dni · Praca zdalna (home office): 1 dzień");

        await tab.PreviousYearAsync(Cancellation);

        tab.Items.Select(row => row.Absence.Kind).ShouldBe([AbsenceKind.Sickness, AbsenceKind.Training]);
    }

    [Fact]
    public async Task Hr_adds_an_absence_in_a_dialog_and_an_overlapping_one_is_shown_on_the_first_day()
    {
        Add(AbsenceKind.Sickness, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        var tab = new EmployeeAbsencesViewModel(_api, _time, _dialogs, canManage: true);
        await tab.ShowAsync(Employee, Cancellation);
        AbsenceDialogViewModel? overlapping = null;
        _dialogs.When<AbsenceDialogViewModel>(async dialog =>
        {
            dialog.EditStartDate.ShouldBe(new DateTime(2025, 6, 16));
            dialog.EditKind = EmployeeAbsencesViewModel.Kinds.Single(kind => kind.Kind == AbsenceKind.BusinessTrip);
            dialog.EditEndDate = new DateTime(2025, 6, 18);
            dialog.EditNote = " Targi w Poznaniu ";
            await dialog.SaveAsync(Cancellation);
        });

        await tab.NewAsync(Cancellation);

        tab.Items.Count.ShouldBe(2);
        tab.Selected.ShouldNotBeNull().KindName.ShouldBe("Delegacja");
        tab.Selected.Absence.Note.ShouldBe("Targi w Poznaniu");

        _dialogs.When<AbsenceDialogViewModel>(async dialog =>
        {
            dialog.EditStartDate = new DateTime(2025, 6, 5);
            await dialog.SaveAsync(Cancellation);
            overlapping = dialog;
        });
        await tab.NewAsync(Cancellation);

        overlapping.ShouldNotBeNull().GetError(nameof(AbsenceDialogViewModel.EditStartDate)).ShouldBe("W tym okresie pracownik ma już wpisaną inną nieobecność.");
        tab.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Removal_needs_a_second_click_and_someone_who_may_only_read_cannot_change_anything()
    {
        Add(AbsenceKind.Sickness, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        var tab = new EmployeeAbsencesViewModel(_api, _time, _dialogs, canManage: true);
        await tab.ShowAsync(Employee, Cancellation);
        tab.Selected = tab.Items[0];

        await tab.DeleteAsync(Cancellation);
        tab.DeleteText.ShouldBe("Na pewno usunąć?");
        await tab.DeleteAsync(Cancellation);

        tab.IsEmpty.ShouldBeTrue();
        _api.Absences.ShouldBeEmpty();

        Add(AbsenceKind.Sickness, new DateOnly(2025, 6, 2), new DateOnly(2025, 6, 6));
        var readOnly = new EmployeeAbsencesViewModel(_api, _time, _dialogs, canManage: false);
        await readOnly.ShowAsync(Employee, Cancellation);
        readOnly.Selected = readOnly.Items[0];

        readOnly.EditCommand.CanExecute(null).ShouldBeFalse();
        readOnly.DeleteCommand.CanExecute(null).ShouldBeFalse();
    }

    private void Add(AbsenceKind kind, DateOnly start, DateOnly end)
    {
        var days = end.DayNumber - start.DayNumber + 1;
        _api.Absences.Add(new AbsenceResponse(Guid.CreateVersion7(), Employee.Id, kind, start, end, days, days, null));
    }
}
