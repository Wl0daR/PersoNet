using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class EntitlementDialogView : UserControl
{
    public EntitlementDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, DaysBox, CarriedOverBox);
    }
}
