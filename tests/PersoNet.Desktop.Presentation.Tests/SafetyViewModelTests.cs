using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Safety;
using PersoNet.Desktop.Presentation.Safety;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class SafetyViewModelTests
{
    private static readonly EmployeeResponse Employee = new(Guid.CreateVersion7(), "P001", "Jan", "Nowak", null, null, new DateOnly(2020, 1, 2), null, null, null, null);

    private readonly FakeSafetyApi _api = new();
    private readonly FakeOrganizationApi _organization = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 16, 9, 0, 0, TimeSpan.Zero));

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_tab_says_first_whether_the_certificate_is_valid_and_which_mandatory_trainings_are_missing()
    {
        var bhp = _api.AddCourse("BHP okresowe", 36);
        _api.AddCourse("Pierwsza pomoc", 24);
        _api.Exams.Add(new(Guid.NewGuid(), Employee.Id, MedicalExamKind.Initial, new DateOnly(2022, 6, 1), new DateOnly(2023, 6, 1), null, false));
        _api.Exams.Add(new(Guid.NewGuid(), Employee.Id, MedicalExamKind.Periodic, new DateOnly(2023, 6, 26), new DateOnly(2025, 6, 26), "Bez przeciwwskazań", false));
        _api.Trainings.Add(new(Guid.NewGuid(), Employee.Id, bhp.Id, bhp.Name, new DateOnly(2022, 5, 10), new DateOnly(2025, 5, 10), null, false));
        var tab = new EmployeeSafetyViewModel(_api, _time, _dialogs, canManage: true);

        await tab.ShowAsync(Employee, Cancellation);

        tab.ExamSummary.ShouldBe("Orzeczenie lekarskie ważne do 26 cze 2025.");
        tab.ExamTone.ShouldBe(SafetyTone.Warning);
        tab.MissingText.ShouldBe("Brak szkoleń obowiązkowych: Pierwsza pomoc.");
        tab.Items.Select(row => (row.Name, row.StateText)).ShouldBe(
        [
            ("Badanie okresowe", "ważne jeszcze 10 dni"),
            ("Badanie wstępne", "zastąpione nowszym"),
            ("BHP okresowe", "nieważne"),
        ]);
    }

    [Fact]
    public async Task Without_any_examination_the_employee_may_not_work()
    {
        var tab = new EmployeeSafetyViewModel(_api, _time, _dialogs, canManage: true);

        await tab.ShowAsync(Employee, Cancellation);

        tab.IsEmpty.ShouldBeTrue();
        tab.ExamTone.ShouldBe(SafetyTone.Critical);
        tab.ExamSummary.ShouldBe("Brak badań lekarskich: pracownik nie może pracować bez ważnego orzeczenia.");
        tab.HasMissing.ShouldBeFalse();
    }

    [Fact]
    public async Task A_training_takes_its_validity_from_the_list_and_a_certificate_cannot_end_before_the_examination()
    {
        _api.AddCourse("BHP okresowe", 36);
        var tab = new EmployeeSafetyViewModel(_api, _time, _dialogs, canManage: true);
        await tab.ShowAsync(Employee, Cancellation);
        _dialogs.When<TrainingDialogViewModel>(async dialog =>
        {
            dialog.EditValidUntil.ShouldBeNull();
            dialog.EditCourse = dialog.Courses.Single();
            dialog.EditValidUntil.ShouldBe(new DateTime(2028, 6, 16));
            dialog.EditCompletedOn = new DateTime(2025, 6, 2);
            dialog.EditValidUntil.ShouldBe(new DateTime(2028, 6, 2));
            await dialog.SaveAsync(Cancellation);
        });

        await tab.NewTrainingAsync(Cancellation);

        tab.Selected.ShouldNotBeNull().Name.ShouldBe("BHP okresowe");
        tab.Selected.ValidUntilText.ShouldBe("2 cze 2028");

        MedicalExamDialogViewModel? wrong = null;
        _dialogs.When<MedicalExamDialogViewModel>(async dialog =>
        {
            dialog.EditKind.Kind.ShouldBe(MedicalExamKind.Periodic);
            dialog.EditValidUntil = new DateTime(2025, 6, 1);
            await dialog.SaveAsync(Cancellation);
            wrong = dialog;
        });
        await tab.NewExamAsync(Cancellation);

        wrong.ShouldNotBeNull().GetError(nameof(MedicalExamDialogViewModel.EditValidUntil)).ShouldBe("Orzeczenie nie może tracić ważności przed dniem badania.");
        _api.Exams.ShouldBeEmpty();
    }

    [Fact]
    public async Task Removal_needs_a_second_click_and_someone_who_may_only_read_cannot_change_anything()
    {
        _api.Exams.Add(new(Guid.NewGuid(), Employee.Id, MedicalExamKind.Periodic, new DateOnly(2025, 1, 2), new DateOnly(2027, 1, 2), null, false));
        var tab = new EmployeeSafetyViewModel(_api, _time, _dialogs, canManage: true);
        await tab.ShowAsync(Employee, Cancellation);
        tab.Selected = tab.Items[0];

        await tab.DeleteAsync(Cancellation);
        tab.DeleteText.ShouldBe("Na pewno usunąć?");
        await tab.DeleteAsync(Cancellation);

        _api.Exams.ShouldBeEmpty();
        tab.IsEmpty.ShouldBeTrue();

        var reader = new EmployeeSafetyViewModel(_api, _time, _dialogs, canManage: false);
        await reader.ShowAsync(Employee, Cancellation);
        reader.NewExamCommand.Execute(null);
        _dialogs.Shown.ShouldBeEmpty();
    }

    [Fact]
    public async Task The_list_of_trainings_shows_how_often_and_for_whom_and_a_training_no_longer_run_can_be_hidden()
    {
        var store = _organization.Add("Magazyn", "MAG");
        _organization.Add("Biuro", "BIU");
        _api.AddCourse("BHP okresowe", 36);
        _api.AddCourse("Wózki widłowe", 60, departmentIds: [store.Id]);
        _api.AddCourse("Excel", null, isMandatory: false);
        var screen = new TrainingCoursesViewModel(_api, _organization, _dialogs);

        await screen.LoadAsync(Cancellation);

        screen.Courses.Select(course => (course.Name, course.ValidityText, course.MandatoryFor)).ShouldBe(
        [
            ("BHP okresowe", "co 3 lata", "wszyscy pracownicy"),
            ("Excel", "bezterminowo", "nieobowiązkowe"),
            ("Wózki widłowe", "co 5 lat", "Magazyn"),
        ]);

        screen.Selected = screen.Courses[1];
        screen.ToggleActiveText.ShouldBe("Nie prowadzimy");
        await screen.ToggleActiveAsync(Cancellation);

        screen.Courses.Select(course => course.Name).ShouldBe(["BHP okresowe", "Wózki widłowe"]);
    }

    [Fact]
    public async Task A_new_training_is_mandatory_in_the_departments_ticked_and_its_validity_is_checked()
    {
        var store = _organization.Add("Magazyn", "MAG");
        var screen = new TrainingCoursesViewModel(_api, _organization, _dialogs);
        await screen.LoadAsync(Cancellation);
        _dialogs.When<TrainingCourseDialogViewModel>(async dialog =>
        {
            dialog.DepartmentsSummary.ShouldBe("Nie wybrano działu: szkolenie jest obowiązkowe dla wszystkich pracowników.");
            dialog.EditName = " Suwnice ";
            dialog.EditValidityMonths = "500";
            await dialog.SaveAsync(Cancellation);
            dialog.GetError(nameof(TrainingCourseDialogViewModel.EditValidityMonths)).ShouldBe("Podaj liczbę miesięcy od 1 do 240, np. 12 dla szkolenia co rok.");

            dialog.EditValidityMonths = "120";
            dialog.DepartmentFilter = "mag";
            dialog.Departments.Single(option => option.IsVisible).IsChecked = true;
            dialog.DepartmentsSummary.ShouldBe("Obowiązkowe w 1 dziale i działach mu podległych.");
            await dialog.SaveAsync(Cancellation);
        });

        await screen.NewAsync(Cancellation);

        var saved = _api.Courses.ShouldHaveSingleItem();
        saved.Name.ShouldBe("Suwnice");
        saved.ValidityMonths.ShouldBe(120);
        saved.DepartmentIds.ShouldBe([store.Id]);
        screen.Selected.ShouldNotBeNull().MandatoryFor.ShouldBe("Magazyn");
    }

    [Theory]
    [InlineData(null, "bezterminowo")]
    [InlineData(12, "co rok")]
    [InlineData(24, "co 2 lata")]
    [InlineData(60, "co 5 lat")]
    [InlineData(18, "co 18 miesięcy")]
    [InlineData(3, "co 3 miesiące")]
    public void Validity_is_said_in_words(int? months, string text) => SafetyTexts.Validity(months).ShouldBe(text);
}
