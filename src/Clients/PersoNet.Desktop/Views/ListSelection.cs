using System.Windows;
using System.Windows.Media;

namespace PersoNet.Desktop.Views;

/// <summary>
/// Selected row of a list (DataGrid): a light tint of the accent colour with normal text, instead of the solid accent fill,
/// so the number, e-mail and status badge in the row stay readable in light and dark theme. The brushes are rebuilt in the
/// window's resources whenever the theme changes (WPF rejects a brush with DynamicResource colours in resources).
/// </summary>
public static class ListSelection
{
    private const string BackgroundKey = "DataGridRowSelectedBackgroundThemeBrush";
    private const string ForegroundKey = "DataGridRowSelectedForegroundThemeBrush";
    private const string TextKey = "TextFillColorPrimaryBrush";

    // Follows the theme's text brush: it changes whenever the theme does. Typed as object, so whatever the theme
    // stores under the key is accepted and the brushes are built from what can be read.
    private static readonly DependencyProperty ThemeTextProperty = DependencyProperty.RegisterAttached(
        "ThemeText", typeof(object), typeof(ListSelection), new PropertyMetadata(null, OnThemeChanged));

    /// <summary>Keeps the selection brushes of <paramref name="window"/> in step with the theme.</summary>
    public static void Attach(FrameworkElement window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.SetResourceReference(ThemeTextProperty, TextKey);
    }

    private static void OnThemeChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        if (sender is not FrameworkElement element || e.NewValue is not SolidColorBrush text)
        {
            return;
        }

        var background = new SolidColorBrush(SystemColors.AccentColor) { Opacity = 0.22 };
        var foreground = new SolidColorBrush(text.Color);
        background.Freeze();
        foreground.Freeze();
        element.Resources[BackgroundKey] = background;
        element.Resources[ForegroundKey] = foreground;
    }
}
