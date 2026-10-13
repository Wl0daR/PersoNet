using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class LeaveFormView : UserControl
{
    public LeaveFormView()
    {
        InitializeComponent();
        FormFocus.Attach(this, EmployeeBox, KindBox, StartBox, EndBox, HoursBox, NoteBox);
    }
}
