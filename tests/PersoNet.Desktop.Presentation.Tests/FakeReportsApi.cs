using PersoNet.ApiClient.Reports;
using PersoNet.Contracts.Reports;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>Returns the reports the test sets up and remembers what was asked for.</summary>
internal sealed class FakeReportsApi : IReportsApi
{
    public HeadcountReport? Headcount { get; set; }

    public LeaveUsageReport? LeaveUsage { get; set; }

    public ContractsEndingReport? ContractsEnding { get; set; }

    public List<string> Calls { get; } = [];

    public Task<HeadcountReport> GetHeadcountAsync(DateOnly day, CancellationToken cancellationToken)
    {
        Calls.Add($"headcount {day:yyyy-MM-dd}");
        return Task.FromResult((Headcount ?? new HeadcountReport(day, 0, 0, [], [])) with { Date = day });
    }

    public Task<LeaveUsageReport> GetLeaveUsageAsync(int year, CancellationToken cancellationToken)
    {
        Calls.Add($"leave {year}");
        return Task.FromResult((LeaveUsage ?? new LeaveUsageReport(year, [])) with { Year = year });
    }

    public Task<ContractsEndingReport> GetContractsEndingAsync(DateOnly from, int days, CancellationToken cancellationToken)
    {
        Calls.Add($"contracts {from:yyyy-MM-dd} {days}");
        return Task.FromResult((ContractsEnding ?? new ContractsEndingReport(from, from.AddDays(days - 1), [])) with { From = from, To = from.AddDays(days - 1) });
    }
}
