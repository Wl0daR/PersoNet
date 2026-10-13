using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>Dialog where HR sets the yearly annual leave entitlement of an employee, with the days carried over.</summary>
public sealed partial class EntitlementDialogViewModel : DialogViewModel
{
    public const int MaxDays = 60;

    private readonly ILeaveApi _api;
    private readonly EmployeeResponse _employee;

    public EntitlementDialogViewModel(ILeaveApi api, EmployeeResponse employee, int year, LeaveBalanceResponse? balance)
    {
        ArgumentNullException.ThrowIfNull(employee);

        _api = api;
        _employee = employee;
        Year = year;
        EditDays = balance is { HasEntitlement: true } ? balance.EntitlementDays.ToString(CultureInfo.InvariantCulture) : string.Empty;
        EditCarriedOver = (balance?.CarriedOverDays ?? 0).ToString(CultureInfo.InvariantCulture);
        ClearAllErrors();
    }

    public int Year { get; }

    public override string Title => $"Wymiar urlopu {Year} – {_employee.FirstName} {_employee.LastName}";

    /// <summary>Balance after a successful save.</summary>
    public LeaveBalanceResponse? Saved { get; private set; }

    /// <summary>Days of the yearly entitlement, as typed.</summary>
    [ObservableProperty]
    public partial string EditDays { get; set; }

    [ObservableProperty]
    public partial string EditCarriedOver { get; set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate(out var days, out var carriedOver)
        ? Task.CompletedTask
        : RunAsync(async () =>
        {
            Saved = await _api.SetEntitlementAsync(_employee.Id, Year, new SaveLeaveEntitlementRequest(days, carriedOver), cancellationToken);
            HasChanges = true;
            Close(true);
        });

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code != "leave.entitlement_invalid")
        {
            return false;
        }

        SetError(nameof(EditDays), exception.Message);
        return true;
    }

    private bool Validate(out int days, out int carriedOver)
    {
        var daysOk = int.TryParse(EditDays.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out days) && days <= MaxDays;
        var carriedOk = int.TryParse(string.IsNullOrWhiteSpace(EditCarriedOver) ? "0" : EditCarriedOver.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out carriedOver)
            && carriedOver <= MaxDays;

        SetError(nameof(EditDays), daysOk ? null : "Podaj liczbę dni od 0 do 60, np. 20 albo 26.");
        SetError(nameof(EditCarriedOver), carriedOk ? null : "Podaj liczbę dni od 0 do 60 albo zostaw 0.");

        return daysOk && carriedOk;
    }
}
