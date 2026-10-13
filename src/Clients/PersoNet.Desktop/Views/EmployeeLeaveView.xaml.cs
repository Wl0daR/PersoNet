using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class EmployeeLeaveView : UserControl
{
    public EmployeeLeaveView()
    {
        InitializeComponent();
        FormFocus.Attach(this, DaysBox, CarriedOverBox);
    }
}
