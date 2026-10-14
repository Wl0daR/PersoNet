using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class ClockIdDialogView : UserControl
{
    public ClockIdDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, ClockIdBox);
    }
}
