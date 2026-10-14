using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PersoNet.ApiClient.Reports;
using PersoNet.Contracts.Reports;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>
/// "Kończące się umowy": contracts that end in the coming days, soonest first, with whether a next contract is already in
/// place, so HR knows which ones need a decision.
/// </summary>
public sealed partial class ContractsEndingReportViewModel(IReportsApi reports, IFileService files, TimeProvider time) : ReportViewModel(files)
{
    private ContractsEndingReport? _report;

    public override string Name => "Kończące się umowy";

    public override string Description => "Umowy, które kończą się w wybranym okresie, z informacją, czy jest już kolejna umowa.";

    /// <summary>How far ahead the report looks.</summary>
    public static IReadOnlyList<PeriodChoice> Periods { get; } =
    [
        new(30, "30 dni"),
        new(60, "60 dni"),
        new(90, "90 dni"),
        new(180, "pół roku"),
        new(365, "rok"),
    ];

    [ObservableProperty]
    public partial PeriodChoice Period { get; set; } = Periods[1];

    public ObservableCollection<ContractEndingLine> Rows { get; } = [];

    protected override string FileName => $"Kończące się umowy {FileDate(_report?.From ?? Today)}.xlsx";

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    protected override async Task LoadReportAsync(Func<bool> isCurrent, CancellationToken cancellationToken)
    {
        var today = Today;
        var report = await reports.GetContractsEndingAsync(today, Period.Days, cancellationToken);
        if (!isCurrent())
        {
            return;
        }

        _report = report;
        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new ContractEndingLine(row, today));
        }

        IsEmpty = Rows.Count == 0;
        var open = report.Rows.Count(row => !row.HasNextContract);
        Summary = Rows.Count == 0
            ? $"Do {LongDate(report.To)} nie kończy się żadna umowa."
            : $"Do {LongDate(report.To)}: {Plural(Rows.Count, "umowa", "umowy", "umów")}, bez kolejnej umowy: {open}";
    }

    protected override IReadOnlyList<ReportTable> Tables()
    {
        if (_report is not { } report)
        {
            return [];
        }

        return
        [
            new ReportTable(
                "Kończące się umowy",
                "Kończące się umowy",
                $"Umowy z końcem od {LongDate(report.From)} do {LongDate(report.To)}.",
                [
                    new("Nr ewidencyjny"), new("Nazwisko"), new("Imię"), new("Dział"), new("Stanowisko"), new("Umowa"),
                    new("Od", ReportFormat.Date), new("Do", ReportFormat.Date), new("Dni do końca", ReportFormat.WholeNumber), new("Etat", ReportFormat.Number),
                    new("Kolejna umowa"),
                ],
                [.. report.Rows.Select(row => (IReadOnlyList<object?>)
                [
                    row.Number, row.LastName, row.FirstName, row.DepartmentName, row.PositionName, HeadcountReportViewModel.ContractName(row.Kind),
                    row.StartDate, row.EndDate, row.EndDate.DayNumber - report.From.DayNumber, row.WorkingTime,
                    row.HasNextContract ? "tak" : "nie",
                ])]),
        ];
    }

    partial void OnPeriodChanged(PeriodChoice value) => _ = LoadAsync(CancellationToken.None);
}

/// <summary>How many days ahead the report looks, with its name in the list.</summary>
public sealed record PeriodChoice(int Days, string Name);

/// <summary>A contract that ends soon, in words.</summary>
public sealed record ContractEndingLine(ContractEndingRow Row, DateOnly Today)
{
    public string Name => $"{Row.LastName} {Row.FirstName}";

    public string Details => string.Join(" · ", new[] { Row.Number, Row.PositionName, Row.DepartmentName }.Where(text => !string.IsNullOrEmpty(text)));

    public string Contract => HeadcountReportViewModel.ContractName(Row.Kind);

    public DateOnly EndDate => Row.EndDate;

    public int DaysLeft => Row.EndDate.DayNumber - Today.DayNumber;

    /// <summary>"dziś", "jutro", "za 12 dni".</summary>
    public string DaysLeftText => DaysLeft switch
    {
        0 => "dziś",
        1 => "jutro",
        _ => $"za {DaysLeft} dni",
    };

    public bool HasNextContract => Row.HasNextContract;

    public string NextContract => Row.HasNextContract ? "Jest kolejna umowa" : "Brak kolejnej umowy";
}
