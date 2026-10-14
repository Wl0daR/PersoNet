using ClosedXML.Excel;

namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>
/// Writes reports to an Excel workbook (.xlsx), a sheet per table: the title and its line above a table with a header row
/// that stays in view and filters. Dates and numbers are real values, so the sheet can be sorted and summed.
/// </summary>
public static class ExcelExport
{
    private const int TitleRow = 1;
    private const int SubtitleRow = 2;
    private const int HeaderRow = 4;
    private const int MaxSheetName = 31;
    private const double MaxColumnWidth = 60;

    public static void Write(Stream stream, IReadOnlyList<ReportTable> tables)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(tables);

        using var workbook = new XLWorkbook();
        foreach (var table in tables)
        {
            AddSheet(workbook, table);
        }

        workbook.SaveAs(stream);
    }

    private static void AddSheet(XLWorkbook workbook, ReportTable table)
    {
        var name = table.SheetName.Length > MaxSheetName ? table.SheetName[..MaxSheetName] : table.SheetName;
        var sheet = workbook.Worksheets.Add(name);

        sheet.Cell(TitleRow, 1).Value = table.Title;
        sheet.Cell(TitleRow, 1).Style.Font.SetBold().Font.SetFontSize(14);
        sheet.Cell(SubtitleRow, 1).Value = table.Subtitle;
        sheet.Cell(SubtitleRow, 1).Style.Font.SetItalic();

        for (var column = 0; column < table.Columns.Count; column++)
        {
            var header = sheet.Cell(HeaderRow, column + 1);
            header.Value = table.Columns[column].Header;
            header.Style.Font.SetBold().Fill.SetBackgroundColor(XLColor.FromHtml("#DCE6F1")).Alignment.SetWrapText();
        }

        for (var row = 0; row < table.Rows.Count; row++)
        {
            for (var column = 0; column < table.Columns.Count; column++)
            {
                var value = column < table.Rows[row].Count ? table.Rows[row][column] : null;
                Write(sheet.Cell(HeaderRow + 1 + row, column + 1), value, table.Columns[column].Format);
            }
        }

        var lastRow = HeaderRow + Math.Max(table.Rows.Count, 1);
        var range = sheet.Range(HeaderRow, 1, lastRow, Math.Max(table.Columns.Count, 1));
        range.SetAutoFilter();
        sheet.SheetView.FreezeRows(HeaderRow);

        // Widths from the table only: the title and its line may run across empty columns.
        sheet.Columns(1, table.Columns.Count).AdjustToContents(HeaderRow, lastRow, 8, MaxColumnWidth);
    }

    private static void Write(IXLCell cell, object? value, ReportFormat format)
    {
        switch (value)
        {
            case null:
                return;
            case DateOnly date:
                cell.Value = date.ToDateTime(TimeOnly.MinValue);
                cell.Style.DateFormat.Format = "dd.mm.yyyy";
                return;
            case int number:
                cell.Value = number;
                return;
            case decimal number:
                // Excel's general format shows 1 as "1" and 0,5 as "0,5", in the reader's own separators.
                cell.Value = format == ReportFormat.WholeNumber ? decimal.Round(number) : number;
                return;
            default:
                cell.Value = Convert.ToString(value, System.Globalization.CultureInfo.CurrentCulture);
                return;
        }
    }
}
