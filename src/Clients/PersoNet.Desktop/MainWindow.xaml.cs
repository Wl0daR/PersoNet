using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using PersoNet.Desktop.Presentation.Shell;

namespace PersoNet.Desktop;

public partial class MainWindow : Window
{
    /// <summary>Below this window width the side menu shows icons only, leaving the room to the screen's content.</summary>
    public const double CompactNavigationBelow = 1100;

    public static readonly DependencyProperty IsNavigationCompactProperty = DependencyProperty.Register(
        nameof(IsNavigationCompact), typeof(bool), typeof(MainWindow), new PropertyMetadata(false, OnCompactChanged));

    public static readonly DependencyProperty IsNavigationExpandedProperty = DependencyProperty.Register(
        nameof(IsNavigationExpanded), typeof(bool), typeof(MainWindow), new PropertyMetadata(true));

    // Set once the user toggles the menu themselves; the window width no longer decides for them.
    private bool? _userChoice;

    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        Views.ListSelection.Attach(this);
        DataContext = viewModel;
        FitToScreen();
        SizeChanged += (_, _) => UpdateNavigationMode();
        PreviewKeyDown += OnPreviewKeyDown;
    }

    public bool IsNavigationCompact
    {
        get => (bool)GetValue(IsNavigationCompactProperty);
        set => SetValue(IsNavigationCompactProperty, value);
    }

    /// <summary>Opposite of <see cref="IsNavigationCompact"/>, for visibility bindings.</summary>
    public bool IsNavigationExpanded
    {
        get => (bool)GetValue(IsNavigationExpandedProperty);
        private set => SetValue(IsNavigationExpandedProperty, value);
    }

    private static void OnCompactChanged(DependencyObject window, DependencyPropertyChangedEventArgs e) =>
        ((MainWindow)window).IsNavigationExpanded = !(bool)e.NewValue;

    // The app opens maximized, so the lists and the calendar get the whole screen. The size it restores to still fits a small
    // or highly scaled screen (e.g. 1366 x 768 at 150 % gives about 910 x 480 to work with).
    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width || Height > area.Height)
        {
            Width = Math.Max(MinWidth, area.Width * 0.9);
            Height = Math.Max(MinHeight, area.Height * 0.9);
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        WindowState = WindowState.Maximized;
    }

    // Each screen declares its shortcuts (F5, Ctrl+N, Ctrl+F), but they reach it only while the keyboard focus is inside the
    // screen. After a click in the side menu the focus stays in the menu, so the window passes the shortcut on.
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (FindScreen(ScreenHost) is not { IsKeyboardFocusWithin: false } screen)
        {
            return;
        }

        foreach (var binding in screen.InputBindings.OfType<KeyBinding>())
        {
            if (binding.Gesture.Matches(screen, e) && binding.Command?.CanExecute(binding.CommandParameter) == true)
            {
                binding.Command.Execute(binding.CommandParameter);
                e.Handled = true;
                return;
            }
        }
    }

    private static UserControl? FindScreen(DependencyObject parent)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if ((child as UserControl ?? FindScreen(child)) is { } screen)
            {
                return screen;
            }
        }

        return null;
    }

    private void UpdateNavigationMode() => IsNavigationCompact = _userChoice ?? ActualWidth < CompactNavigationBelow;

    // A left click opens the theme menu above the button, like a drop-down.
    private void OnOpenThemeMenu(object sender, RoutedEventArgs e)
    {
        var button = (Button)sender;
        button.ContextMenu.DataContext = button.DataContext;
        button.ContextMenu.PlacementTarget = button;
        button.ContextMenu.IsOpen = true;
    }

    private void OnToggleNavigation(object sender, RoutedEventArgs e)
    {
        _userChoice = !IsNavigationCompact;
        UpdateNavigationMode();
    }
}
