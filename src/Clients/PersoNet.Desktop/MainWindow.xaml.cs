using System.Windows;
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
        DataContext = viewModel;
        FitToScreen();
        SizeChanged += (_, _) => UpdateNavigationMode();
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

    // On a small or highly scaled screen (e.g. 1366 x 768 at 150 % gives about 910 x 480 to work with) the default size
    // would not fit, so the window opens maximized instead of hanging off the screen.
    private void FitToScreen()
    {
        var area = SystemParameters.WorkArea;
        if (Width > area.Width || Height > area.Height)
        {
            Width = Math.Max(MinWidth, area.Width * 0.9);
            Height = Math.Max(MinHeight, area.Height * 0.9);
            WindowState = WindowState.Maximized;
        }

        WindowStartupLocation = WindowStartupLocation.CenterScreen;
    }

    private void UpdateNavigationMode() => IsNavigationCompact = _userChoice ?? ActualWidth < CompactNavigationBelow;

    private void OnToggleNavigation(object sender, RoutedEventArgs e)
    {
        _userChoice = !IsNavigationCompact;
        UpdateNavigationMode();
    }
}
