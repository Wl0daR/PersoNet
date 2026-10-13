using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class LeaveView : UserControl
{
    public LeaveView()
    {
        InitializeComponent();
        FormFocus.Attach(this, DecisionBox);
    }
}
