using System.Windows.Controls;
using System.Windows.Input;

namespace PersoNet.Desktop.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();

        // Ctrl+F jumps to the search box, as in most list screens.
        InputBindings.Add(new KeyBinding(new FocusSearchCommand(this), Key.F, ModifierKeys.Control));
    }

    private sealed class FocusSearchCommand(EmployeesView view) : ICommand
    {
        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            view.SearchBox.Focus();
            view.SearchBox.SelectAll();
        }
    }
}
