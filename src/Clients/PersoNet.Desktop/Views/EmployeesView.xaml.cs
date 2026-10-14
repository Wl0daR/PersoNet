using System.ComponentModel;
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

        // Ctrl+F jumps to the search box, as in most list screens.
        InputBindings.Add(new KeyBinding(new FocusSearchCommand(this), Key.F, ModifierKeys.Control));
        Loaded += (_, _) =>
        {
            ShowSortArrow();
            if (DataContext is EmployeesViewModel viewModel)
            {
                // Refilling the list must not lose the arrow.
                viewModel.Items.CollectionChanged -= OnItemsChanged;
                viewModel.Items.CollectionChanged += OnItemsChanged;
            }
        };
    }

    // The list is paged, so the server sorts it: the click asks the view model, and the grid only shows the arrow.
    private void OnSorting(object sender, DataGridSortingEventArgs e)
    {
        e.Handled = true;
        if (DataContext is EmployeesViewModel viewModel && Enum.TryParse<EmployeeSort>(e.Column.SortMemberPath, out var column))
        {
            viewModel.SortByCommand.Execute(column);
            ShowSortArrow();
        }
    }

    private void OnItemsChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => ShowSortArrow();

    private void ShowSortArrow()
    {
        if (DataContext is not EmployeesViewModel viewModel)
        {
            return;
        }

        foreach (var column in List.Columns)
        {
            column.SortDirection = column.SortMemberPath == viewModel.Sort.ToString()
                ? viewModel.SortDescending ? ListSortDirection.Descending : ListSortDirection.Ascending
                : null;
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
