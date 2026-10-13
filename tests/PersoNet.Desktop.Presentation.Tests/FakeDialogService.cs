using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Tests;

/// <summary>
/// Stands in for the dialog windows: <see cref="OnShow"/> plays the user (fills the form, clicks a button); without it
/// the dialog is closed with "Anuluj".
/// </summary>
internal sealed class FakeDialogService : IDialogService
{
    public List<DialogViewModel> Shown { get; } = [];

    public Func<DialogViewModel, Task>? OnShow { get; set; }

    /// <summary>Plays the user on dialogs of type <typeparamref name="T"/>.</summary>
    public void When<T>(Func<T, Task> user)
        where T : DialogViewModel => OnShow = dialog => user((T)dialog);

    public async Task<bool> ShowAsync(DialogViewModel dialog)
    {
        Shown.Add(dialog);

        var closedWith = false;
        void OnClose(object? sender, bool result) => closedWith = result;
        dialog.CloseRequested += OnClose;
        try
        {
            if (OnShow is { } user)
            {
                await user(dialog);
            }
        }
        finally
        {
            dialog.CloseRequested -= OnClose;
        }

        return closedWith || dialog.HasChanges;
    }
}
