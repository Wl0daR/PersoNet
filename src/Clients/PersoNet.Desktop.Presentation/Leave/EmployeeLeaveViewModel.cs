using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Leave;
using PersoNet.Contracts.Employees;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Leave;

/// <summary>
/// "Urlop" tab of an employee's card: the annual leave entitlement of a year (set by HR), the balance, and the employee's
/// requests in that year.
/// </summary>
public sealed partial class EmployeeLeaveViewModel(ILeaveApi api, TimeProvider time) : EditorViewModel
{
    public const int MaxDays = 60;

    private EmployeeResponse? _employee;
    private int _loadVersion;

    public ObservableCollection<LeaveRow> Items { get; } = [];

    public IReadOnlyList<int> Years { get; } = Enumerable.Range(time.GetLocalNow().Year - 1, 3).ToList();

    [ObservableProperty]
    public partial int Year { get; set; } = time.GetLocalNow().Year;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Summary), nameof(HasEntitlement))]
    public partial LeaveBalanceResponse? Balance { get; set; }

    /// <summary>Days of the yearly entitlement, as typed.</summary>
    [ObservableProperty]
    public partial string EditDays { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string EditCarriedOver { get; set; } = "0";

    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    public bool HasEntitlement => Balance?.HasEntitlement == true;

    /// <summary>"Zostało 14 dni" with the parts it is made of.</summary>
    public string Summary => Balance is not { } balance
        ? string.Empty
        : !balance.HasEntitlement
            ? $"Wymiar urlopu na {balance.Year} nie jest jeszcze ustawiony. Zwykle 20 dni, a po 10 latach pracy (z nauką) 26 dni; przy niepełnym etacie proporcjonalnie mniej."
            : $"Wykorzystano {LeaveTexts.Days(balance.UsedDays)}, {LeaveTexts.Days(balance.PendingDays)} czeka na decyzję, w tym na żądanie {LeaveTexts.Days(balance.OnDemandDays)} z 4.";

    public async Task ShowAsync(EmployeeResponse? employee, CancellationToken cancellationToken)
    {
        _employee = employee;
        Items.Clear();
        Balance = null;
        EditDays = string.Empty;
        EditCarriedOver = "0";
        ClearAllErrors();

        if (employee is not null)
        {
            await LoadAsync(cancellationToken);
        }
    }

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee)
        {
            return Task.CompletedTask;
        }

        var version = ++_loadVersion;
        var year = Year;

        return RunAsync(async () =>
        {
            var balance = await api.GetBalanceAsync(employee.Id, year, cancellationToken);
            var requests = await api.ListAsync(state: null, year, employee.Id, cancellationToken);

            // A newer employee or year may have been picked while this one loaded.
            if (version != _loadVersion)
            {
                return;
            }

            Show(balance);
            Items.Clear();
            foreach (var request in requests)
            {
                Items.Add(LeaveTexts.ToRow(request));
            }

            IsEmpty = Items.Count == 0;
        });
    }

    [RelayCommand]
    public Task SaveEntitlementAsync(CancellationToken cancellationToken)
    {
        if (_employee is not { } employee || !Validate(out var days, out var carriedOver))
        {
            return Task.CompletedTask;
        }

        return RunAsync(async () => Show(await api.SetEntitlementAsync(employee.Id, Year, new SaveLeaveEntitlementRequest(days, carriedOver), cancellationToken)));
    }

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.Code == "leave.entitlement_invalid")
        {
            SetError(nameof(EditDays), exception.Message);
            return true;
        }

        return false;
    }

    partial void OnYearChanged(int value) => _ = LoadAsync(CancellationToken.None);

    private void Show(LeaveBalanceResponse balance)
    {
        Balance = balance;
        EditDays = balance.HasEntitlement ? balance.EntitlementDays.ToString(CultureInfo.InvariantCulture) : string.Empty;
        EditCarriedOver = balance.CarriedOverDays.ToString(CultureInfo.InvariantCulture);
        ClearAllErrors();
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
