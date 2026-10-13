using System.Windows.Controls;

namespace PersoNet.Desktop.Views;

public partial class ContractsView : UserControl
{
    public ContractsView()
    {
        InitializeComponent();
        FormFocus.Attach(this, StartBox, EndBox, WorkingTimeBox);
    }
}
