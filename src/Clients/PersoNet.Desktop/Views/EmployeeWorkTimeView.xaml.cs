using System.Windows;
using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class EmployeeWorkTimeView : UserControl
{
    private const double ShortHeight = 400;

    public EmployeeWorkTimeView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// In a tall enough tab only the list of days scrolls. In a short one (laptop at 150%) the whole tab scrolls,
    /// so the list keeps room for several days instead of shrinking to one.
    /// </summary>
    private void OnPageSizeChanged(object sender, SizeChangedEventArgs e)
    {
        Page.VerticalScrollBarVisibility = e.NewSize.Height < ShortHeight ? ScrollBarVisibility.Auto : ScrollBarVisibility.Disabled;
    }
}
