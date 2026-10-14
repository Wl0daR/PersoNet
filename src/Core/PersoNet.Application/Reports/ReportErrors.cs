using PersoNet.Application.Common;

namespace PersoNet.Application.Reports;

internal static class ReportErrors
{
    public static readonly AppError DateOutOfRange =
        AppError.Validation("report.date_out_of_range", "Podaj datę z lat 2000–2100.");

    public static readonly AppError YearOutOfRange =
        AppError.Validation("report.year_out_of_range", "Podaj rok z zakresu 2000–2100.");

    public static readonly AppError DaysOutOfRange =
        AppError.Validation("report.days_out_of_range", $"Okres może mieć od 1 do {ReportService.MaxDaysAhead} dni.");
}
