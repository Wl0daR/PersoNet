namespace PersoNet.Desktop.Presentation.Common;

/// <summary>Colour theme of the app picked by the user.</summary>
public enum AppTheme
{
    /// <summary>Follows the Windows setting (light or dark mode).</summary>
    System,
    Light,
    Dark,
}

/// <summary>Switches the colour theme of the app and remembers the choice for the next start.</summary>
public interface IThemeService
{
    AppTheme Theme { get; }

    void SetTheme(AppTheme theme);
}
