using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient.Reports;
using PersoNet.Contracts.Reports;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>
/// "Wykorzystanie urlopów": the annual leave of each person in a year (entitlement, taken, waiting for a decision and
/// left), counted in the Labour Code's days of 8 hours like the balance on the employee card.
/// </summary>
public sealed partial class LeaveUsageReportViewModel(IReportsApi reports, IFileService files, TimeProvider time) : ReportViewModel(files)
{
    private LeaveUsageReport? _report;

    public override string Name => "Wykorzystanie urlopów";

    public override string Description => "Urlop wypoczynkowy każdej osoby w roku: wymiar, wykorzystany, czekający na decyzję i pozostały.";

    [ObservableProperty]
    public partial int Year { get; private set; } = time.GetLocalNow().Year;

    public ObservableCollection<LeaveUsageLine> Rows { get; } = [];

    protected override string FileName => $"Wykorzystanie urlopów {_report?.Year ?? Year}.xlsx";

    [RelayCommand]
    public Task PreviousYearAsync(CancellationToken cancellationToken)
    {
        Year--;
        return LoadAsync(cancellationToken);
    }

    [RelayCommand]
    public Task NextYearAsync(CancellationToken cancellationToken)
    {
        Year++;
        return LoadAsync(cancellationToken);
    }

    protected override async Task LoadReportAsync(Func<bool> isCurrent, CancellationToken cancellationToken)
    {
        var report = await reports.GetLeaveUsageAsync(Year, cancellationToken);
        if (!isCurrent())
        {
            return;
        }

        _report = report;
        Rows.Clear();
        foreach (var row in report.Rows)
        {
            Rows.Add(new LeaveUsageLine(row));
        }

        IsEmpty = Rows.Count == 0;
        var withEntitlement = report.Rows.Where(row => row.HasEntitlement).ToList();
        var missing = report.Rows.Count - withEntitlement.Count;
        Summary = $"Rok {report.Year}: {People(report.Rows.Count)}, do wykorzystania łącznie {LeaveTexts.Amount(withEntitlement.Sum(row => Math.Max(0, row.RemainingHours)))}"
            + (missing > 0 ? $", bez ustalonego wymiaru: {People(missing)}" : string.Empty);
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
                $"Urlopy {report.Year}",
                $"Wykorzystanie urlopu wypoczynkowego w {report.Year} r.",
                "Dni po 8 godzin (art. 154² KP). Urlop na żądanie jest wliczony w wykorzystany i czekający. Puste pola: wymiar nie został ustalony.",
                [
                    new("Nr ewidencyjny"), new("Nazwisko"), new("Imię"), new("Dział"),
                    new("Wymiar (dni)", ReportFormat.WholeNumber), new("Zaległy (dni)", ReportFormat.WholeNumber), new("Razem (dni)", ReportFormat.Number),
                    new("Wykorzystany (dni)", ReportFormat.Number), new("Czeka na decyzję (dni)", ReportFormat.Number), new("Pozostało (dni)", ReportFormat.Number),
                    new("Na żądanie (dni)", ReportFormat.WholeNumber),
                ],
                [.. report.Rows.Select(row => (IReadOnlyList<object?>)
                [
                    row.Number, row.LastName, row.FirstName, row.DepartmentName,
                    row.HasEntitlement ? row.EntitlementDays : null, row.HasEntitlement ? row.CarriedOverDays : null, row.HasEntitlement ? Days(row.TotalHours) : null,
                    Days(row.UsedHours), Days(row.PendingHours), row.HasEntitlement ? Days(row.RemainingHours) : null,
                    row.OnDemandDays,
                ])]),
        ];
    }

    private static decimal Days(decimal hours) => Math.Round(hours / LeaveTexts.HoursPerDay, 2);
}

/// <summary>A person's annual leave in the report, in words.</summary>
public sealed record LeaveUsageLine(LeaveUsageRow Row)
{
    public string Number => Row.Number;

    public string Name => $"{Row.LastName} {Row.FirstName}";

    public string Department => Row.DepartmentName ?? HeadcountReportViewModel.NoDepartment;

    /// <summary>The line under the name: "P0002 · Dział Kadr i Płac".</summary>
    public string Details => $"{Row.Number} · {Department}";

    /// <summary>"26 dni + 2 zaległe", or a note that HR has not set it.</summary>
    public string Entitlement => !Row.HasEntitlement ? "nie ustalono"
        : Row.CarriedOverDays > 0 ? $"{LeaveTexts.Days(Row.EntitlementDays)} + {Row.CarriedOverDays} zaległe"
        : LeaveTexts.Days(Row.EntitlementDays);

    public string Used => LeaveTexts.Amount(Row.UsedHours);

    public string Pending => Row.PendingHours > 0 ? LeaveTexts.Amount(Row.PendingHours) : "—";

    public string Remaining => Row.HasEntitlement ? LeaveTexts.Amount(Row.RemainingHours) : "—";

    /// <summary>"2 z 4".</summary>
    public string OnDemand => $"{Row.OnDemandDays} z 4";

    public bool HasEntitlement => Row.HasEntitlement;

    /// <summary>Remaining hours, for sorting the column by amount rather than by text.</summary>
    public decimal RemainingHours => Row.HasEntitlement ? Row.RemainingHours : -1;
}
