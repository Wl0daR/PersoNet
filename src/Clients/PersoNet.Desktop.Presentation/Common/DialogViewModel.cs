using CommunityToolkit.Mvvm.Input;

namespace PersoNet.Desktop.Presentation.Common;

/// <summary>
/// View model shown in a window of its own (a dialog): a form or a record that the user confirms or closes. The window
/// closes when <see cref="Close"/> is called; the result tells the opener whether anything was saved.
/// </summary>
public abstract partial class DialogViewModel : EditorViewModel
{
    /// <summary>Raised by <see cref="Close"/>; the argument is the result (true when something was saved).</summary>
    public event EventHandler<bool>? CloseRequested;

    /// <summary>Window title, e.g. "Nowa umowa".</summary>
    public abstract string Title { get; }

    /// <summary>True when the dialog changed data, so the screen that opened it reloads.</summary>
    public bool HasChanges { get; protected set; }

    /// <summary>Closes the window without saving (Esc, "Anuluj").</summary>
    [RelayCommand]
    public void Cancel() => Close(HasChanges);

    protected void Close(bool result) => CloseRequested?.Invoke(this, result);
}
