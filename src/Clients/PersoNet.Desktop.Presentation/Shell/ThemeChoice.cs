using CommunityToolkit.Mvvm.ComponentModel;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Shell;

/// <summary>One option of the theme menu, with the icon shown on the menu button.</summary>
public sealed partial class ThemeChoice(AppTheme theme, string name, string glyph) : ObservableObject
{
    public AppTheme Theme { get; } = theme;

    public string Name { get; } = name;

    public string Glyph { get; } = glyph;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}
