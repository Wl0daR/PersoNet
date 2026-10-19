using PersoNet.Api.Common;
using PersoNet.Application.Abstractions;
using PersoNet.Application.Reports;

namespace PersoNet.Api.Reports;

/// <summary>Company-wide HR reports. All of them need <see cref="Permissions.ReportsRead"/>.</summary>
internal static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this RouteGroupBuilder api)
    {
        var reports = api.MapGroup("/reports").WithTags("Raporty").RequireAuthorization(Permissions.ReportsRead);

        reports.MapGet("/headcount", async (DateOnly? date, ReportService service, CancellationToken cancellationToken) =>
                (await service.GetHeadcountAsync(date, cancellationToken)).ToOk())
            .WithName("GetHeadcountReport").WithSummary("Stan zatrudnienia na dzień (domyślnie dziś), z podsumowaniem działów");

        reports.MapGet("/leave-usage", async (int? year, ReportService service, CancellationToken cancellationToken) =>
                (await service.GetLeaveUsageAsync(year, cancellationToken)).ToOk())
            .WithName("GetLeaveUsageReport").WithSummary("Wykorzystanie urlopu wypoczynkowego w roku (domyślnie bieżącym)");

        reports.MapGet("/contracts-ending", async (DateOnly? from, int? days, ReportService service, CancellationToken cancellationToken) =>
                (await service.GetContractsEndingAsync(from, days, cancellationToken)).ToOk())
            .WithName("GetContractsEndingReport").WithSummary("Umowy kończące się w okresie (domyślnie 60 dni od dziś)");

        reports.MapGet("/safety-renewals", async (int? days, ReportService service, CancellationToken cancellationToken) =>
                (await service.GetSafetyRenewalsAsync(days, cancellationToken)).ToOk())
            .WithName("GetSafetyRenewalsReport").WithSummary("Brakujące, nieważne i kończące się badania lekarskie i szkolenia obowiązkowe (domyślnie 60 dni od dziś)");

        return api;
    }
}
