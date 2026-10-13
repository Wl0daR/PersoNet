using System.Windows;
using System.Windows.Controls;
using PersoNet.Desktop.Presentation.Organization;

namespace PersoNet.Desktop.Views;

public partial class DepartmentsView : UserControl
{
    public DepartmentsView()
    {
        InitializeComponent();
        FormFocus.Attach(this, NameBox, CodeBox, ParentBox);
    }

    // TreeView.SelectedItem cannot be bound, so the selection is passed to the view model here.
    private void OnSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (DataContext is DepartmentsViewModel viewModel && e.NewValue is DepartmentNode node)
        {
            viewModel.Selected = node;
        }
    }
}
