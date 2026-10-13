using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace PersoNet.Desktop.Views;

/// <summary>
/// Runs a command with the item of a list, grid or tree when the user double clicks it or presses Enter on it, the way
/// records open in desktop programs. The command gets the item as its parameter.
/// </summary>
public static class OpenOnActivate
{
    public static readonly DependencyProperty CommandProperty = DependencyProperty.RegisterAttached(
        "Command", typeof(ICommand), typeof(OpenOnActivate), new PropertyMetadata(null, OnCommandChanged));

    public static ICommand? GetCommand(DependencyObject element)
    {
        ArgumentNullException.ThrowIfNull(element);
        return (ICommand?)element.GetValue(CommandProperty);
    }

    public static void SetCommand(DependencyObject element, ICommand? value)
    {
        ArgumentNullException.ThrowIfNull(element);
        element.SetValue(CommandProperty, value);
    }

    private static void OnCommandChanged(DependencyObject element, DependencyPropertyChangedEventArgs e)
    {
        if (element is not Control control)
        {
            return;
        }

        control.MouseDoubleClick -= OnDoubleClick;
        control.PreviewKeyDown -= OnKeyDown;

        if (e.NewValue is not null)
        {
            control.MouseDoubleClick += OnDoubleClick;
            control.PreviewKeyDown += OnKeyDown;
        }
    }

    // Only a double click on an item counts, not on a header, the scroll bar or the empty space below the items.
    private static void OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || sender is not DependencyObject list)
        {
            return;
        }

        for (var element = e.OriginalSource as DependencyObject; element is not null && element != list; element = Parent(element))
        {
            if (element is DataGridRow or ListBoxItem or TreeViewItem)
            {
                e.Handled = Run(list, ((FrameworkElement)element).DataContext);
                return;
            }

            if (element is ScrollBar or DataGridColumnHeader)
            {
                return;
            }
        }
    }

    private static void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || Keyboard.Modifiers != ModifierKeys.None || sender is not DependencyObject list)
        {
            return;
        }

        var item = list switch
        {
            Selector selector => selector.SelectedItem,
            TreeView tree => tree.SelectedItem,
            _ => null,
        };

        if (item is not null)
        {
            e.Handled = Run(list, item);
        }
    }

    private static bool Run(DependencyObject list, object? item)
    {
        var command = GetCommand(list);
        if (item is null || command?.CanExecute(item) != true)
        {
            return false;
        }

        command.Execute(item);
        return true;
    }

    private static DependencyObject? Parent(DependencyObject element) =>
        element is Visual or System.Windows.Media.Media3D.Visual3D ? VisualTreeHelper.GetParent(element) : LogicalTreeHelper.GetParent(element);
}
