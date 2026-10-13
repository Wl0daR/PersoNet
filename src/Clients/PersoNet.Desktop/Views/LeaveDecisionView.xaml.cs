using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class LeaveDecisionView : UserControl
{
    public LeaveDecisionView()
    {
        InitializeComponent();
        FormFocus.Attach(this, DecisionBox);
    }
}
