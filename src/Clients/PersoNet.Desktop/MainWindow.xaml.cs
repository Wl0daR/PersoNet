using System.Windows;
using PersoNet.Desktop.Presentation.Shell;

namespace PersoNet.Desktop;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
