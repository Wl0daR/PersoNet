using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// Dialog with one leave request: its details and the employee's balance, and for a waiting request the decision
/// (approve, or reject with a reason). An approved or waiting request can also be cancelled.
/// </summary>
public sealed partial class LeaveDecisionViewModel(ILeaveApi leave, LeaveRow row) : DialogViewModel
{
    public const int NoteMaxLength = LeaveFormViewModel.NoteMaxLength;

    public LeaveRow Row { get; private set; } = row;

    public override string Title => $"Wniosek urlopowy – {Row.Request.EmployeeName}";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(BalanceText))]
    public partial LeaveBalanceResponse? Balance { get; set; }

    /// <summary>Note with a decision; the reason when rejecting.</summary>
    [ObservableProperty]
    public partial string DecisionNote { get; set; } = string.Empty;

    /// <summary>First click on "Anuluj urlop" asks for confirmation; the second one cancels.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CancelText))]
    public partial bool IsConfirmingCancel { get; set; }

    public string? BalanceText => LeaveTexts.BalanceText(Balance);

    public string CancelText => IsConfirmingCancel ? "Na pewno anulować?" : "Anuluj urlop";

    public bool CanDecide => Row.IsPending;

    public bool CanCancelLeave => Row.IsPending || Row.IsApproved;

    /// <summary>Loads the employee's balance for the year of the request.</summary>
    public async Task LoadAsync(CancellationToken cancellationToken)
    {
        try
        {
            Balance = await leave.GetBalanceAsync(Row.Request.EmployeeId, Row.Request.StartDate.Year, cancellationToken);
        }
        catch (ApiException)
        {
            Balance = null;
        }
        catch (HttpRequestException)
        {
            Balance = null;
        }
    }

    [RelayCommand]
    public Task ApproveAsync(CancellationToken cancellationToken) => !CanDecide
        ? Task.CompletedTask
        : RunAsync(async () => Decided(await leave.ApproveAsync(Row.Request.Id, NoteOrNull(), cancellationToken)));

    [RelayCommand]
    public Task RejectAsync(CancellationToken cancellationToken)
    {
        if (!CanDecide)
        {
            return Task.CompletedTask;
        }

        if (string.IsNullOrWhiteSpace(DecisionNote))
        {
            SetError(nameof(DecisionNote), "Podaj powód odrzucenia, żeby pracownik wiedział, co zmienić.");
            return Task.CompletedTask;
        }

        return RunAsync(async () => Decided(await leave.RejectAsync(Row.Request.Id, DecisionNote.Trim(), cancellationToken)));
    }

    [RelayCommand]
    public Task CancelLeaveAsync(CancellationToken cancellationToken)
    {
        if (!CanCancelLeave)
        {
            return Task.CompletedTask;
        }

        if (!IsConfirmingCancel)
        {
            IsConfirmingCancel = true;
            return Task.CompletedTask;
        }

        return RunAsync(async () => Decided(await leave.CancelAsync(Row.Request.Id, NoteOrNull(), cancellationToken)));
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code is not ("leave.note_too_long" or "leave.reason_required"))
        {
            return false;
        }

        SetError(nameof(DecisionNote), exception.Message);
        return true;
    }

    private void Decided(LeaveRequestResponse decided)
    {
        Row = LeaveTexts.ToRow(decided);
        HasChanges = true;
        Close(true);
    }

    private string? NoteOrNull() => string.IsNullOrWhiteSpace(DecisionNote) ? null : DecisionNote.Trim();
}
