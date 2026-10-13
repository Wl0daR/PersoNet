using System.Globalization;
using System.Windows.Data;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Views;

/// <summary>Initials of a list row for its avatar.</summary>
public sealed class EmployeeInitialsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is EmployeeListItem item ? Initials.Of(item.FirstName, item.LastName) : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

/// <summary>Second line of a list row: number and position, e.g. "P0004 · Programista .NET".</summary>
public sealed class EmployeeDetailsConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is EmployeeListItem item
            ? string.Join(" · ", new[] { item.Number, item.PositionName }.Where(part => !string.IsNullOrEmpty(part)))
            : string.Empty;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
