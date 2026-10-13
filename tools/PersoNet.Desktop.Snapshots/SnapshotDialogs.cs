using System.Windows;
using System.Windows.Interop;
using PersoNet.Desktop.Dialogs;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Snapshots;

/// <summary>
/// Opens dialogs off screen without waiting for the user: runs <see cref="Script"/> (which renders the window and may
/// open further dialogs from it), then closes the window.
/// </summary>
internal sealed class SnapshotDialogs : IDialogService
{
    // The largest screen the snapshots use (1440 x 900), so a dialog opens at its own size.
    private static readonly Size LargestScreen = new(1440 * 0.95, 900 * 0.95);

    private static readonly Dictionary<Window, Size> NaturalSizes = [];

    public Window? Owner { get; set; }

    /// <summary>What to do with the next dialog; used once.</summary>
    public Func<DialogViewModel, Window, Task>? Script { get; set; }

    /// <summary>The size a dialog window opened at, before it was fitted to a smaller screen.</summary>
    public static Size NaturalSize(Window window) => NaturalSizes[window];

    public async Task<bool> ShowAsync(DialogViewModel dialog)
    {
        var script = Script;
        Script = null;

        var window = new DialogWindow(dialog, DialogService.CreateView(dialog), LargestScreen)
        {
            Owner = Owner,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = -20000,
            Top = -20000,
            ShowActivated = false,
        };
        var closed = false;
        window.Closed += (_, _) => closed = true;
        window.SourceInitialized += (_, _) => HwndSource.FromHwnd(new WindowInteropHelper(window).Handle).AddHook(Snapshots.AllowLargerThanScreen);
        window.Show();
        await Snapshots.SettleAsync();

        var view = (FrameworkElement)window.Content;
        NaturalSizes[window] = new Size(view.ActualWidth, view.ActualHeight);

        try
        {
            if (script is not null)
            {
                await script(dialog, window);
            }
        }
        finally
        {
            NaturalSizes.Remove(window);
            if (!closed)
            {
                window.Close();
            }
        }

        return dialog.HasChanges;
    }
}
