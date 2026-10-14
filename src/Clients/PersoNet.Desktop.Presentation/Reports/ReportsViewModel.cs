using CommunityToolkit.Mvvm.ComponentModel;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>"Raporty" screen: the company-wide reports, one per tab, each with its own export to Excel.</summary>
public sealed partial class ReportsViewModel : ObservableObject
{
    public ReportsViewModel(HeadcountReportViewModel headcount, LeaveUsageReportViewModel leaveUsage, ContractsEndingReportViewModel contractsEnding)
    {
        Headcount = headcount;
        LeaveUsage = leaveUsage;
        ContractsEnding = contractsEnding;
        Reports = [headcount, leaveUsage, contractsEnding];
    }

    public HeadcountReportViewModel Headcount { get; }

    public LeaveUsageReportViewModel LeaveUsage { get; }

    public ContractsEndingReportViewModel ContractsEnding { get; }

    public IReadOnlyList<ReportViewModel> Reports { get; }

    /// <summary>The open tab; F5 and Ctrl+E act on its report.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Selected))]
    public partial int SelectedIndex { get; set; }

    public ReportViewModel Selected => Reports[Math.Clamp(SelectedIndex, 0, Reports.Count - 1)];

    /// <summary>Loads every report, so switching tabs shows data at once.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        foreach (var report in Reports)
        {
            await report.LoadAsync(cancellationToken);
        }
    }
}
