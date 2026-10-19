using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PersoNet.ApiClient.Reports;
using PersoNet.Contracts.Reports;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Safety;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>
/// "Badania i szkolenia": medical certificates and mandatory trainings of the people employed today that are missing,
/// have expired or run out in the coming days, the most urgent first, so HR knows whom to send where.
/// </summary>
public sealed partial class SafetyRenewalsReportViewModel(IReportsApi reports, IFileService files, TimeProvider time) : ReportViewModel(files)
{
    private SafetyRenewalReport? _report;

    public override string Name => "Badania i szkolenia";

    public override string Description => "Badania lekarskie i szkolenia obowiązkowe, których brakuje, które straciły ważność albo wkrótce ją stracą.";

    [ObservableProperty]
    public partial PeriodChoice Period { get; set; } = ContractsEndingReportViewModel.Periods[1];

    public ObservableCollection<SafetyRenewalLine> Rows { get; } = [];

    protected override string FileName => $"Badania i szkolenia {FileDate(_report?.From ?? Today)}.xlsx";

    private DateOnly Today => DateOnly.FromDateTime(time.GetLocalNow().DateTime);

    protected override async Task LoadReportAsync(Func<bool> isCurrent, CancellationToken cancellationToken)
    {
        var report = await reports.GetSafetyRenewalsAsync(Period.Days, cancellationToken);
        if (!isCurrent())
        {
            return;
        }

        _report = report;
        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new SafetyRenewalLine(row, report.From));
        }

        IsEmpty = Rows.Count == 0;
        int Count(SafetyRenewalState state) => report.Rows.Count(row => row.State == state);
        Summary = Rows.Count == 0
            ? $"Do {LongDate(report.To)} wszystkie badania i szkolenia obowiązkowe są ważne."
            : $"Nieważne: {Count(SafetyRenewalState.Expired)}, tracą ważność do {LongDate(report.To)}: {Count(SafetyRenewalState.Expiring)}, "
                + $"brak: {Count(SafetyRenewalState.Missing)} · {People(report.Rows.Select(row => row.EmployeeId).Distinct().Count())}";
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
                "Badania i szkolenia",
                "Badania i szkolenia do odnowienia",
                $"Stan na {LongDate(report.From)}; tracące ważność do {LongDate(report.To)}.",
                [
                    new("Nr ewidencyjny"), new("Nazwisko"), new("Imię"), new("Dział"), new("Co"), new("Stan"),
                    new("Ostatnio", ReportFormat.Date), new("Ważne do", ReportFormat.Date),
                ],
                [.. report.Rows.Select(row => (IReadOnlyList<object?>)
                [
                    row.Number, row.LastName, row.FirstName, row.DepartmentName, SafetyRenewalLine.ItemName(row), SafetyRenewalLine.StateName(row.State),
                    row.LastDone, row.ValidUntil,
                ])]),
        ];
    }

    partial void OnPeriodChanged(PeriodChoice value) => _ = LoadAsync(CancellationToken.None);
}

/// <summary>Something to renew, in words.</summary>
public sealed record SafetyRenewalLine(SafetyRenewalRow Row, DateOnly Today)
{
    public string Name => $"{Row.LastName} {Row.FirstName}";

    public string Details => string.Join(" · ", new[] { Row.Number, Row.DepartmentName }.Where(text => !string.IsNullOrEmpty(text)));

    public string Item => ItemName(Row);

    public string State => StateName(Row.State);

    /// <summary>"od 3 mar 2025", "do 12 lis 2026" or "nigdy", for the date column.</summary>
    public string When => Row.ValidUntil is { } until
        ? (Row.State == SafetyRenewalState.Expired ? "od " : "do ") + SafetyTexts.Date(Row.State == SafetyRenewalState.Expired ? until.AddDays(1) : until)
        : Row.LastDone is { } done ? $"zrobione {SafetyTexts.Date(done)}" : "nigdy";

    public SafetyTone Tone => Row.State == SafetyRenewalState.Expiring ? SafetyTone.Warning : SafetyTone.Critical;

    public static string ItemName(SafetyRenewalRow row) =>
        row.Item == SafetyItemKind.MedicalExam ? "Badania lekarskie" : $"Szkolenie: {row.Name}";

    public static string StateName(SafetyRenewalState state) => state switch
    {
        SafetyRenewalState.Missing => "brak",
        SafetyRenewalState.Expired => "nieważne",
        _ => "traci ważność",
    };
}
