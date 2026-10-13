using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class PositionDialogView : UserControl
{
    public PositionDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, NameBox);
    }
}
