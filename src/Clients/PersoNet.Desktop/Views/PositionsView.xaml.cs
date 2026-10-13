using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class PositionsView : UserControl
{
    public PositionsView()
    {
        InitializeComponent();
        FormFocus.Attach(this, NameBox);
    }
}
