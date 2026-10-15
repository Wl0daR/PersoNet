using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class AbsenceDialogView : UserControl
{
    public AbsenceDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, StartBox, EndBox, NoteBox);
    }
}
