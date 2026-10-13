using System.Windows.Controls;
using System.Windows.Input;
using PersoNet.Contracts.Employees;
using PersoNet.Desktop.Presentation.Employees;

namespace PersoNet.Desktop.Views;

public partial class EmployeesView : UserControl
{
    public EmployeesView()
    {
        InitializeComponent();
        FormFocus.Attach(this, NumberBox, FirstNameBox, LastNameBox, EmailBox, HireDateBox, DepartmentBox, PositionBox, ManagerBox, LastDayBox);

        // Ctrl+F jumps to the search box, as in most list screens.
        InputBindings.Add(new KeyBinding(new FocusSearchCommand(this), Key.F, ModifierKeys.Control));
    }

    // Picking a row opens that employee's card; the view model ignores the row that is already open.
    private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is EmployeesViewModel viewModel && List.SelectedItem is EmployeeListItem item)
        {
            viewModel.OpenCommand.Execute(item);
        }
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
