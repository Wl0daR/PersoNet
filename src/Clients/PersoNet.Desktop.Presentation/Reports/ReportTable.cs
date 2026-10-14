namespace PersoNet.Desktop.Presentation.Reports;

/// <summary>How a column's values are written to the spreadsheet, so Excel can sort and sum them.</summary>
public enum ReportFormat
{
    Text,
    Date,
    WholeNumber,
    Number,
}

/// <summary>A column of an exported report.</summary>
public sealed record ReportColumn(string Header, ReportFormat Format = ReportFormat.Text);

/// <summary>
/// A report as it goes to a spreadsheet: one sheet with a title, a line saying what it shows, and a table. A value is a
/// string, a <see cref="DateOnly"/>, a number or null (an empty cell).
/// </summary>
public sealed record ReportTable(string SheetName, string Title, string Subtitle, IReadOnlyList<ReportColumn> Columns, IReadOnlyList<IReadOnlyList<object?>> Rows);
