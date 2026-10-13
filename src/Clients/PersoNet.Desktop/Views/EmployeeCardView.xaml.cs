using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class EmployeeCardView : UserControl
{
    public EmployeeCardView()
    {
        InitializeComponent();
        FormFocus.Attach(this, NumberBox, FirstNameBox, LastNameBox, EmailBox, HireDateBox, DepartmentBox, PositionBox, ManagerBox, LastDayBox);
    }
}
