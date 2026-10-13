using System.Windows;
using System.Windows.Media;

namespace PersoNet.Desktop.Views;

/// <summary>
/// Selected row of a list (DataGrid): a light tint of the accent colour with normal text, instead of the solid accent fill,
/// so the number, e-mail and status badge in the row stay readable in light and dark theme. The brushes are rebuilt in the
/// window's resources whenever the theme colours change (a brush with DynamicResource colours is rejected in resources).
/// </summary>
public static class ListSelection
{
    private const string BackgroundKey = "DataGridRowSelectedBackgroundThemeBrush";
    private const string ForegroundKey = "DataGridRowSelectedForegroundThemeBrush";

    private static readonly DependencyProperty AccentColorProperty = DependencyProperty.RegisterAttached(
        "AccentColor", typeof(Color), typeof(ListSelection), new PropertyMetadata(Colors.Transparent, OnColorChanged));

    private static readonly DependencyProperty TextColorProperty = DependencyProperty.RegisterAttached(
        "TextColor", typeof(Color), typeof(ListSelection), new PropertyMetadata(Colors.Transparent, OnColorChanged));

    /// <summary>Keeps the selection brushes of <paramref name="window"/> in step with the theme.</summary>
    public static void Attach(FrameworkElement window)
    {
        ArgumentNullException.ThrowIfNull(window);

        window.SetResourceReference(AccentColorProperty, "SystemAccentColor");
        window.SetResourceReference(TextColorProperty, "TextFillColorPrimary");
    }

    private static void OnColorChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e)
    {
        var element = (FrameworkElement)sender;
        var background = new SolidColorBrush((Color)element.GetValue(AccentColorProperty)) { Opacity = 0.22 };
        var foreground = new SolidColorBrush((Color)element.GetValue(TextColorProperty));
        background.Freeze();
        foreground.Freeze();
        element.Resources[BackgroundKey] = background;
        element.Resources[ForegroundKey] = foreground;
    }
}
