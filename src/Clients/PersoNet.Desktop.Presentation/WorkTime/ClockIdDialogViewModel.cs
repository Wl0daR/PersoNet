using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.WorkTime;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.WorkTime;

/// <summary>
/// Dialog "Identyfikator RCP": the employee's card number or user id in the time clock system, which tells PersoNet whose
/// clock events are whose.
/// </summary>
public sealed partial class ClockIdDialogViewModel(IWorkTimeApi api, Guid employeeId, string? current) : DialogViewModel
{
    public const int MaxLength = 50;

    public override string Title => "Identyfikator w systemie RCP";

    [ObservableProperty]
    public partial string ClockId { get; set; } = current ?? string.Empty;

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken)
    {
        var value = ClockId.Trim();
        if (value.Any(char.IsWhiteSpace))
        {
            SetError(nameof(ClockId), "Identyfikator nie może zawierać spacji.");
            return Task.CompletedTask;
        }

        return RunAsync(async () =>
        {
            await api.SetClockIdAsync(employeeId, new SaveClockIdRequest(value.Length == 0 ? null : value), cancellationToken);
            HasChanges = true;
            Close(true);
        });
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code is "worktime.clock_id_taken" or "worktime.clock_id_invalid")
        {
            SetError(nameof(ClockId), exception.Message);
            return true;
        }

        return false;
    }
}
