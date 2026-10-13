using System.Collections;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using PersoNet.ApiClient;

namespace PersoNet.Desktop.Presentation.Common;

/// <summary>
/// Base for list + editor screens. Field errors go through <see cref="INotifyDataErrorInfo"/> so the view shows them
/// under the field they belong to; <see cref="ErrorMessage"/> is only for problems not tied to one field.
/// </summary>
public abstract partial class EditorViewModel : ObservableObject, INotifyDataErrorInfo
{
    private readonly Dictionary<string, string> _errors = new(StringComparer.Ordinal);

    public event EventHandler<DataErrorsChangedEventArgs>? ErrorsChanged;

    [ObservableProperty]
    public partial string? ErrorMessage { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    public bool IsIdle => !IsBusy;

    public bool HasErrors => _errors.Count > 0;

    public IEnumerable GetErrors(string? propertyName) =>
        propertyName is not null && _errors.TryGetValue(propertyName, out var error) ? new[] { error } : [];

    /// <summary>Returns the error shown for a field, or null.</summary>
    public string? GetError(string propertyName) => _errors.GetValueOrDefault(propertyName);

    protected void SetError(string propertyName, string? error)
    {
        var changed = error is null ? _errors.Remove(propertyName) : _errors.TryAdd(propertyName, error) || Replace(propertyName, error);

        if (changed)
        {
            ErrorsChanged?.Invoke(this, new DataErrorsChangedEventArgs(propertyName));
            OnPropertyChanged(nameof(HasErrors));
        }
    }

    protected void ClearAllErrors()
    {
        foreach (var propertyName in _errors.Keys.ToList())
        {
            SetError(propertyName, error: null);
        }

        ErrorMessage = null;
    }

    /// <summary>Puts a server error next to its field when the code belongs to one; returns false otherwise.</summary>
    protected abstract bool TryShowOnField(ApiException exception);

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPropertyChanged(e);

        // Editing a field takes its old error away; it is checked again on save.
        if (e.PropertyName is { } name && _errors.ContainsKey(name))
        {
            SetError(name, error: null);
        }
    }

    /// <summary>Runs an API call with a busy indicator, turning failures into messages instead of exceptions.</summary>
    protected async Task RunAsync(Func<Task> action)
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            await action();
        }
        catch (ApiException exception) when (TryShowOnField(exception))
        {
        }
        catch (ApiException exception)
        {
            ErrorMessage = exception.Message;
        }
        catch (HttpRequestException)
        {
            ErrorMessage = "Brak połączenia z serwerem PersoNet. Sprawdź, czy serwer działa, i spróbuj ponownie.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool Replace(string propertyName, string error)
    {
        if (_errors[propertyName] == error)
        {
            return false;
        }

        _errors[propertyName] = error;
        return true;
    }
}
