using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class WorkDayDialogView : UserControl
{
    public WorkDayDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, StartBox, EndBox, BreakBox, AbsenceBox, NoteBox);
    }
}
