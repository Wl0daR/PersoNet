using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class DepartmentDialogView : UserControl
{
    public DepartmentDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, NameBox, CodeBox, ParentBox);
    }
}
