namespace PersoNet.Desktop.Presentation.Common;

/// <summary>Opens dialog windows over the active window.</summary>
public interface IDialogService
{
    /// <summary>
    /// Shows <paramref name="dialog"/> as a modal window and completes when it closes; true when it was closed with a
    /// saved result.
    /// </summary>
    Task<bool> ShowAsync(DialogViewModel dialog);
}
