using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace PersoNet.Desktop.Views;

public partial class EmployeeLeaveView : UserControl
{
    public EmployeeLeaveView()
    {
        InitializeComponent();

        // The calendar and the request list do not scroll themselves; the mouse wheel over them scrolls the whole tab.
        CalendarBox.PreviewMouseWheel += PassWheelToParent;
        RequestsList.PreviewMouseWheel += PassWheelToParent;
    }

    private static void PassWheelToParent(object sender, MouseWheelEventArgs e)
    {
        if (sender is not FrameworkElement { Parent: UIElement parent })
        {
            return;
        }

        e.Handled = true;
        parent.RaiseEvent(new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta) { RoutedEvent = MouseWheelEvent, Source = sender });
    }
}
