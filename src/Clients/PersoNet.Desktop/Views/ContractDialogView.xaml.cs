using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class ContractDialogView : UserControl
{
    public ContractDialogView()
    {
        InitializeComponent();
        FormFocus.Attach(this, StartBox, EndBox, WorkingTimeBox);
    }
}
