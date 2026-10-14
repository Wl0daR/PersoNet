using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PersoNet.ApiClient.Reports;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Reports;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>"Stan zatrudnienia": everyone employed on a day, with their contract and the totals of each department.</summary>
public sealed partial class HeadcountReportViewModel(IReportsApi reports, IFileService files, TimeProvider time) : ReportViewModel(files)
{
    public const string NoDepartment = "Bez działu";

    private HeadcountReport? _report;

    public override string Name => "Stan zatrudnienia";

    public override string Description => "Kto jest zatrudniony w wybranym dniu, na jakiej umowie i w jakim wymiarze, z podsumowaniem działów.";

    /// <summary>The day of the report, today at first.</summary>
    [ObservableProperty]
    public partial DateTime? Date { get; set; } = time.GetLocalNow().Date;

    public ObservableCollection<HeadcountRow> Rows { get; } = [];

    public ObservableCollection<HeadcountDepartmentRow> Departments { get; } = [];

    protected override string FileName => $"Stan zatrudnienia {FileDate(_report?.Date ?? Day)}.xlsx";

    private DateOnly Day => DateOnly.FromDateTime(Date ?? time.GetLocalNow().Date);

    /// <summary>"1 etat", "3 etaty", "2,5 etatu".</summary>
    public static string FullTime(decimal value) => decimal.Truncate(value) == value
        ? Plural((int)value, "etat", "etaty", "etatów")
        : $"{value.ToString("0.##", Polish)} etatu";

    public static string ContractName(ContractKind? kind) => kind is { } value
        ? ContractsViewModel.Kinds.FirstOrDefault(choice => choice.Kind == value)?.Name ?? value.ToString()
        : "Brak umowy w tym dniu";

    protected override async Task LoadReportAsync(Func<bool> isCurrent, CancellationToken cancellationToken)
    {
        var report = await reports.GetHeadcountAsync(Day, cancellationToken);
        if (!isCurrent())
        {
            return;
        }

        _report = report;
        Rows.Clear();
        foreach (var person in report.Rows)
        {
            Rows.Add(new HeadcountRow(person));
        }

        Departments.Clear();
        foreach (var department in report.Departments)
        {
            Departments.Add(new HeadcountDepartmentRow(department.DepartmentName ?? NoDepartment, People(department.People), FullTime(department.FullTimeEquivalent)));
        }

        IsEmpty = Rows.Count == 0;
        Summary = $"{LongDate(report.Date)}: {People(report.People)}, {FullTime(report.FullTimeEquivalent)} na umowach o pracę";
    }

    protected override IReadOnlyList<ReportTable> Tables()
    {
        if (_report is not { } report)
        {
            return [];
        }

        var subtitle = $"Stan na {LongDate(report.Date)}: {People(report.People)}, {FullTime(report.FullTimeEquivalent)} na umowach o pracę.";
        return
        [
            new ReportTable(
                "Pracownicy",
                "Stan zatrudnienia",
                subtitle,
                [
                    new("Nr ewidencyjny"), new("Nazwisko"), new("Imię"), new("Dział"), new("Stanowisko"),
                    new("Data zatrudnienia", ReportFormat.Date), new("Umowa"), new("Umowa do", ReportFormat.Date), new("Etat", ReportFormat.Number),
                ],
                [.. report.Rows.Select(person => (IReadOnlyList<object?>)
                [
                    person.Number, person.LastName, person.FirstName, person.DepartmentName, person.PositionName,
                    person.HireDate, ContractName(person.ContractKind), person.ContractEndDate, person.WorkingTime,
                ])]),
            new ReportTable(
                "Działy",
                "Stan zatrudnienia według działów",
                subtitle,
                [new("Dział"), new("Osoby", ReportFormat.WholeNumber), new("Etaty", ReportFormat.Number)],
                [.. report.Departments.Select(department => (IReadOnlyList<object?>)
                    [department.DepartmentName ?? NoDepartment, department.People, department.FullTimeEquivalent])]),
        ];
    }

    partial void OnDateChanged(DateTime? value)
    {
        if (value is not null)
        {
            _ = LoadAsync(CancellationToken.None);
        }
    }
}

/// <summary>A person on the headcount list.</summary>
public sealed record HeadcountRow(HeadcountPerson Person)
{
    public string Number => Person.Number;

    public string Name => $"{Person.LastName} {Person.FirstName}";

    public string Department => Person.DepartmentName ?? HeadcountReportViewModel.NoDepartment;

    public string Position => Person.PositionName ?? "—";

    public DateOnly HireDate => Person.HireDate;

    public string Contract => HeadcountReportViewModel.ContractName(Person.ContractKind);

    /// <summary>"do 31.12.2025", or "bezterminowo".</summary>
    public string ContractEnd => Person.ContractKind is null ? string.Empty
        : Person.ContractEndDate is { } end ? $"do {end:dd.MM.yyyy}" : "bezterminowo";

    /// <summary>"1", "0,5", or a dash for a civil contract.</summary>
    public string WorkingTime => Person.WorkingTime is { } value ? value.ToString("0.##", System.Globalization.CultureInfo.GetCultureInfo("pl-PL")) : "—";
}

/// <summary>A department's totals.</summary>
public sealed record HeadcountDepartmentRow(string Name, string People, string FullTime);
