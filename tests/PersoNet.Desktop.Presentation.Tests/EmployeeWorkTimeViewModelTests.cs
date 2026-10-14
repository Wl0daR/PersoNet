using ClosedXML.Excel;
using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Company;
using PersoNet.Desktop.Presentation.WorkTime;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class EmployeeWorkTimeViewModelTests : IDisposable
{
    private static readonly EmployeeResponse Ala = new(Guid.NewGuid(), "P0001", "Ala", "Nowak", null, null, new DateOnly(2024, 1, 1), null, null, null, null);

    private readonly FakeWorkTimeApi _api = new();
    private readonly FakeFileService _files = new();
    private readonly FakeDialogService _dialogs = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 11, 20, 9, 0, 0, TimeSpan.Zero));
    private readonly string _folder = Directory.CreateTempSubdirectory("personet-worktime-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private async Task<EmployeeWorkTimeViewModel> ShowAsync(bool canEdit = true)
    {
        var viewModel = new EmployeeWorkTimeViewModel(_api, _files, _time, _dialogs, canEdit);
        await viewModel.ShowAsync(Ala, Cancellation);
        return viewModel;
    }

    [Fact]
    public async Task The_tab_shows_the_current_month_day_by_day_and_moves_between_months()
    {
        var viewModel = await ShowAsync();

        viewModel.MonthTitle.ShouldBe("Listopad 2025");
        viewModel.Rows.Count.ShouldBe(30);
        viewModel.Rows[0].DayText.ShouldBe("sob 1");
        viewModel.Rows[0].IsDayOff.ShouldBeTrue();
        viewModel.NormText.ShouldBe("160:00");
        viewModel.WorkedText.ShouldBe("0:00");
        viewModel.ClockIdText.ShouldBe("Identyfikator RCP: nie ustawiono");

        await viewModel.PreviousMonthAsync(Cancellation);

        viewModel.MonthTitle.ShouldBe("Październik 2025");
        _api.Loaded.ShouldBe([(2025, 11), (2025, 10)]);
    }

    [Fact]
    public async Task A_day_is_typed_in_its_dialog_and_the_month_shows_the_new_hours()
    {
        var viewModel = await ShowAsync();
        var monday = viewModel.Rows.First(row => row.Date == new DateOnly(2025, 11, 3));
        _dialogs.When<WorkDayDialogViewModel>(async dialog =>
        {
            dialog.StartText.ShouldBe("08:00");
            dialog.EndText.ShouldBe("16:00");
            dialog.StartText = "7.30";
            dialog.EndText = "1815";
            dialog.BreakText = "15";
            dialog.Note = "inwentaryzacja";
            await dialog.SaveAsync(Cancellation);
        });

        await viewModel.OpenDayAsync(monday);

        _api.Days[new DateOnly(2025, 11, 3)].ShouldBe(new SaveWorkDayRequest(new TimeOnly(7, 30), new TimeOnly(18, 15), 15, null, "inwentaryzacja"));
        var row = viewModel.Selected.ShouldNotBeNull();
        row.HoursText.ShouldBe("07:30–18:15 (przerwa 15 min)");
        row.WorkedText.ShouldBe("10:30");
        row.OvertimeText.ShouldBe("2:30");
        viewModel.WorkedText.ShouldBe("10:30");
    }

    [Fact]
    public async Task Wrong_times_are_flagged_in_the_dialog_and_the_server_answer_goes_next_to_its_field()
    {
        var viewModel = await ShowAsync();
        WorkDayDialogViewModel? opened = null;
        _dialogs.When<WorkDayDialogViewModel>(async dialog =>
        {
            opened = dialog;
            dialog.StartText = "25:00";
            await dialog.SaveAsync(Cancellation);
            dialog.GetError(nameof(WorkDayDialogViewModel.StartText)).ShouldNotBeNull();

            dialog.StartText = "8:00";
            dialog.EndText = "8:00";
            await dialog.SaveAsync(Cancellation);
        });

        await viewModel.OpenDayAsync(viewModel.Rows[2]);

        opened!.GetError(nameof(WorkDayDialogViewModel.EndText)).ShouldBe("Praca musi kończyć się o innej godzinie, niż się zaczyna.");
        _api.Days.ShouldBeEmpty();
    }

    [Fact]
    public async Task An_absence_is_saved_instead_of_hours()
    {
        var viewModel = await ShowAsync();
        _dialogs.When<WorkDayDialogViewModel>(async dialog =>
        {
            dialog.IsAbsence = true;
            dialog.Absence = WorkDayDialogViewModel.Absences.Single(choice => choice.Kind == AbsenceKind.Sickness);
            await dialog.SaveAsync(Cancellation);
        });

        await viewModel.OpenDayAsync(viewModel.Rows[2]);

        _api.Days.Single().Value.ShouldBe(new SaveWorkDayRequest(null, null, 0, AbsenceKind.Sickness, string.Empty));
        viewModel.Rows[2].HoursText.ShouldBe("Choroba (L4)");
        viewModel.AbsenceText.ShouldBe("8:00");
    }

    [Fact]
    public async Task The_month_is_filled_from_the_plan_and_a_day_cleared_again()
    {
        var viewModel = await ShowAsync();

        await viewModel.FillFromPlanAsync(Cancellation);
        viewModel.Selected = viewModel.Rows[2];
        await viewModel.ClearDayAsync(Cancellation);

        _api.Fills.ShouldBe(1);
        viewModel.WorkedText.ShouldBe("152:00");
        viewModel.Rows[2].IsRecorded.ShouldBeFalse();
    }

    [Fact]
    public async Task The_time_clock_id_is_set_in_its_dialog_and_one_already_taken_is_refused_there()
    {
        var viewModel = await ShowAsync();
        ClockIdDialogViewModel? opened = null;
        _dialogs.When<ClockIdDialogViewModel>(async dialog =>
        {
            opened = dialog;
            dialog.ClockId = FakeWorkTimeApi.TakenClockId;
            await dialog.SaveAsync(Cancellation);
            dialog.GetError(nameof(ClockIdDialogViewModel.ClockId)).ShouldNotBeNull();

            dialog.ClockId = " 0077 ";
            await dialog.SaveAsync(Cancellation);
        });

        await viewModel.EditClockIdAsync(Cancellation);

        _api.ClockId.ShouldBe("0077");
        viewModel.ClockIdText.ShouldBe("Identyfikator RCP: 0077");
    }

    [Fact]
    public async Task The_timesheet_is_saved_to_excel_with_a_row_per_day_and_the_totals()
    {
        var viewModel = await ShowAsync();
        await viewModel.FillFromPlanAsync(Cancellation);
        _files.SavePath = Path.Combine(_folder, "karta.xlsx");

        viewModel.Export();
        viewModel.OpenExport();

        _files.SuggestedName.ShouldBe("Karta ewidencji Nowak Ala 2025-11.xlsx");
        _files.Opened.ShouldBe([_files.SavePath]);
        using var workbook = new XLWorkbook(_files.SavePath);
        var sheet = workbook.Worksheet("Karta ewidencji");
        sheet.Cell(1, 1).GetString().ShouldBe("Karta ewidencji czasu pracy – Ala Nowak (P0001)");
        sheet.Cell(4, 7).GetString().ShouldBe("Przepracowano (godz.)");
        sheet.Cell(7, 1).GetDateTime().ShouldBe(new DateTime(2025, 11, 3));
        sheet.Cell(7, 3).GetString().ShouldBe("08:00");
        sheet.Cell(7, 7).GetDouble().ShouldBe(8);
        sheet.Cell(35, 2).GetString().ShouldBe("Razem");
        sheet.Cell(35, 7).GetDouble().ShouldBe(160);
    }

    [Fact]
    public async Task Someone_who_may_only_look_cannot_change_days()
    {
        var viewModel = await ShowAsync(canEdit: false);

        viewModel.OpenDayCommand.CanExecute(viewModel.Rows[2]).ShouldBeFalse();
        viewModel.FillFromPlanCommand.CanExecute(null).ShouldBeFalse();
        viewModel.EditClockIdCommand.CanExecute(null).ShouldBeFalse();
        viewModel.ExportCommand.CanExecute(null).ShouldBeTrue();
    }

    [Fact]
    public async Task The_company_picks_where_working_time_comes_from()
    {
        var api = new FakeCompanyApi();
        var company = new CompanyViewModel(api, _dialogs);
        await company.LoadAsync(Cancellation);
        _dialogs.When<WorkTimeSettingsDialogViewModel>(async dialog =>
        {
            dialog.Options.Single(option => option.Kind == WorkTimeSourceKind.RogerPrMaster).IsChecked = true;
            dialog.Options.Single(option => option.Kind == WorkTimeSourceKind.Manual).IsChecked = false;
            await dialog.SaveAsync(Cancellation);
        });

        company.WorkTimeSource.ShouldBe("Wpis ręczny");
        await company.EditWorkTimeAsync();

        api.Company.WorkTimeSource.ShouldBe(WorkTimeSourceKind.RogerPrMaster);
        company.WorkTimeSource.ShouldBe("Roger PR Master");
    }
}
