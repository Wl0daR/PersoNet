using CommunityToolkit.Mvvm.ComponentModel;

namespace PersoNet.Desktop.Presentation.Shell;

/// <summary>
/// Main window of the HR desktop app: hosts the module menu and open document tabs.
/// </summary>
public sealed partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    public partial string Title { get; set; } = "PersoNet – Kadry";
}
