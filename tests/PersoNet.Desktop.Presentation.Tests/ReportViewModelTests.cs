using ClosedXML.Excel;
using Microsoft.Extensions.Time.Testing;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Reports;
using PersoNet.Desktop.Presentation.Reports;

namespace PersoNet.Desktop.Presentation.Tests;

public sealed class ReportViewModelTests : IDisposable
{
    private readonly FakeReportsApi _reports = new();
    private readonly FakeFileService _files = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2025, 6, 2, 9, 0, 0, TimeSpan.Zero));
    private readonly string _folder = Directory.CreateTempSubdirectory("personet-reports-").FullName;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    [Fact]
    public async Task Headcount_shows_today_with_totals_and_exports_people_and_departments_to_excel()
    {
        _reports.Headcount = new HeadcountReport(
            default,
            3,
            1.5m,
            [new HeadcountDepartment("Sprzedaż", 2, 1.5m), new HeadcountDepartment(null, 1, 0m)],
            [
                Person("P1", "Adamski", "Sprzedaż", ContractKind.FixedTerm, new DateOnly(2025, 12, 31), 0.5m),
                Person("P2", "Nowak", "Sprzedaż", ContractKind.Indefinite, null, 1m),
                Person("P3", "Zięba", null, null, null, null),
            ]);
        var report = new HeadcountReportViewModel(_reports, _files, _time);
        _files.SavePath = Path.Combine(_folder, "stan.xlsx");

        await report.LoadAsync(Cancellation);
        report.Export();
        report.OpenExport();

        _reports.Calls.ShouldBe(["headcount 2025-06-02"]);
        report.Summary.ShouldBe("2 czerwca 2025: 3 osoby, 1,5 etatu na umowach o pracę");
        report.Departments.Select(department => $"{department.Name}: {department.People}, {department.FullTime}")
            .ShouldBe(["Sprzedaż: 2 osoby, 1,5 etatu", "Bez działu: 1 osoba, 0 etatów"]);
        report.Rows[0].ContractEnd.ShouldBe("do 31.12.2025");
        report.Rows[0].ContractDetails.ShouldBe("do 31.12.2025 · etat 0,5");
        report.Rows[0].Details.ShouldBe("P1 · Handlowiec");
        report.Rows[1].ContractEnd.ShouldBe("bezterminowo");
        report.Rows[2].Contract.ShouldBe("Brak umowy w tym dniu");
        _files.SuggestedName.ShouldBe("Stan zatrudnienia 2025-06-02.xlsx");
        _files.Opened.ShouldBe([_files.SavePath]);

        using var workbook = new XLWorkbook(_files.SavePath);
        var people = workbook.Worksheet("Pracownicy");
        people.Cell(1, 1).GetString().ShouldBe("Stan zatrudnienia");
        people.Cell(4, 1).GetString().ShouldBe("Nr ewidencyjny");
        people.Cell(5, 2).GetString().ShouldBe("Adamski");
        people.Cell(5, 6).GetDateTime().ShouldBe(new DateTime(2024, 1, 1));
        people.Cell(5, 7).GetString().ShouldBe("Umowa o pracę na czas określony");
        people.Cell(5, 9).GetDouble().ShouldBe(0.5);
        people.Cell(7, 8).IsEmpty().ShouldBeTrue();
        var departments = workbook.Worksheet("Działy");
        departments.Cell(6, 1).GetString().ShouldBe("Bez działu");
        departments.Cell(5, 2).GetDouble().ShouldBe(2);
    }

    [Fact]
    public async Task Leave_usage_counts_days_of_eight_hours_and_flags_people_without_an_entitlement()
    {
        _reports.LeaveUsage = new LeaveUsageReport(0,
        [
            new LeaveUsageRow(Guid.NewGuid(), "P1", "Ala", "Nowak", "Kadry", true, 26, 2, 224m, 40m, 8m, 176m, 1, 8m),
            new LeaveUsageRow(Guid.NewGuid(), "P2", "Bartek", "Adamski", null, false, 0, 0, 0m, 0m, 0m, 0m, 0, 8m),
        ]);
        var report = new LeaveUsageReportViewModel(_reports, _files, _time);
        _files.SavePath = Path.Combine(_folder, "urlopy.xlsx");

        await report.LoadAsync(Cancellation);
        await report.PreviousYearAsync(Cancellation);
        report.Export();

        _reports.Calls.ShouldBe(["leave 2025", "leave 2024"]);
        report.Summary.ShouldBe("Rok 2024: 2 osoby, do wykorzystania łącznie 22 dni, bez ustalonego wymiaru: 1 osoba");
        report.Rows[0].Entitlement.ShouldBe("26 dni + 2 zaległe");
        report.Rows[0].Pending.ShouldBe("1 dzień");
        report.Rows[1].Entitlement.ShouldBe("nie ustalono");
        report.Rows[1].Remaining.ShouldBe("—");

        using var workbook = new XLWorkbook(_files.SavePath);
        var sheet = workbook.Worksheet("Urlopy 2024");
        sheet.Cell(5, 7).GetDouble().ShouldBe(28);
        sheet.Cell(5, 10).GetDouble().ShouldBe(22);
        sheet.Cell(6, 5).IsEmpty().ShouldBeTrue();
    }

    [Fact]
    public async Task Contracts_ending_look_ahead_the_picked_period_and_tell_which_have_no_next_contract()
    {
        _reports.ContractsEnding = new ContractsEndingReport(default, default,
        [
            Ending("Nowak", new DateOnly(2025, 6, 3), hasNext: true),
            Ending("Adamski", new DateOnly(2025, 6, 20), hasNext: false),
        ]);
        var report = new ContractsEndingReportViewModel(_reports, _files, _time);

        await report.LoadAsync(Cancellation);
        report.Period = ContractsEndingReportViewModel.Periods[0];
        await report.LoadAsync(Cancellation);

        _reports.Calls.ShouldContain("contracts 2025-06-02 60");
        _reports.Calls.ShouldContain("contracts 2025-06-02 30");
        report.Summary.ShouldBe("Do 1 lipca 2025: 2 umowy, bez kolejnej umowy: 1");
        report.Rows[0].DaysLeftText.ShouldBe("jutro");
        report.Rows[1].DaysLeftText.ShouldBe("za 18 dni");
        report.Rows[1].NextContract.ShouldBe("Brak kolejnej umowy");
    }

    [Fact]
    public async Task Nothing_is_exported_from_an_empty_report_or_when_the_user_cancels_saving()
    {
        var report = new ContractsEndingReportViewModel(_reports, _files, _time);

        await report.LoadAsync(Cancellation);
        var canExportEmpty = report.ExportCommand.CanExecute(null);
        _reports.ContractsEnding = new ContractsEndingReport(default, default, [Ending("Nowak", new DateOnly(2025, 6, 3), hasNext: false)]);
        await report.LoadAsync(Cancellation);
        report.Export();

        canExportEmpty.ShouldBeFalse();
        report.Summary.ShouldBe("Do 31 lipca 2025: 1 umowa, bez kolejnej umowy: 1");
        report.HasExport.ShouldBeFalse();
        report.OpenExportCommand.CanExecute(null).ShouldBeFalse();
    }

    [Fact]
    public void The_reports_screen_acts_on_the_open_tab()
    {
        var screen = new ReportsViewModel(
            new HeadcountReportViewModel(_reports, _files, _time),
            new LeaveUsageReportViewModel(_reports, _files, _time),
            new ContractsEndingReportViewModel(_reports, _files, _time));

        screen.SelectedIndex = 2;

        screen.Selected.ShouldBe(screen.ContractsEnding);
        screen.Reports.Select(report => report.Name).ShouldBe(["Stan zatrudnienia", "Wykorzystanie urlopów", "Kończące się umowy"]);
    }

    private static HeadcountPerson Person(string number, string lastName, string? department, ContractKind? kind, DateOnly? end, decimal? workingTime) =>
        new(Guid.NewGuid(), number, "Imię", lastName, department, "Handlowiec", new DateOnly(2024, 1, 1), kind, end, workingTime);

    private static ContractEndingRow Ending(string lastName, DateOnly end, bool hasNext) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "P1", "Imię", lastName, "Sprzedaż", "Handlowiec", ContractKind.FixedTerm, new DateOnly(2024, 1, 1), end, 1m, hasNext);
}
