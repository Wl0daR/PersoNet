using System.IO;
using System.Text.Json;
using System.Windows;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Settings;

/// <summary>
/// Applies the theme picked in the side menu to every window and keeps it in the user's local settings
/// (%LOCALAPPDATA%\PersoNet\desktop.json), so the app opens in the same theme next time.
/// </summary>
public sealed class ThemeService : IThemeService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PersoNet", "desktop.json");

    public ThemeService()
    {
        Theme = Load();
    }

    public AppTheme Theme { get; private set; }

    /// <summary>Applies the remembered theme; called once at start.</summary>
    public void Apply() => Application.Current.ThemeMode = Theme switch
    {
        AppTheme.Light => ThemeMode.Light,
        AppTheme.Dark => ThemeMode.Dark,
        _ => ThemeMode.System,
    };

    public void SetTheme(AppTheme theme)
    {
        Theme = theme;
        Apply();
        Save();
    }

    private static AppTheme Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var settings = JsonSerializer.Deserialize<DesktopSettings>(File.ReadAllText(SettingsPath));
                if (settings is not null && Enum.IsDefined(settings.Theme))
                {
                    return settings.Theme;
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        {
            // An unreadable settings file only means the default theme.
        }

        return AppTheme.System;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(new DesktopSettings(Theme)));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The theme still changes now; it is only not remembered.
        }
    }

    private sealed record DesktopSettings(AppTheme Theme);
}
