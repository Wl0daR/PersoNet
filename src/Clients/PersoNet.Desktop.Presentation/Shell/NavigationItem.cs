namespace PersoNet.Desktop.Presentation.Shell;

/// <summary>
/// One entry of the side menu. <see cref="Glyph"/> is a Segoe Fluent Icons character; <see cref="Page"/> is the screen's view model.
/// </summary>
public sealed record NavigationItem(string Title, string Glyph, object Page);
