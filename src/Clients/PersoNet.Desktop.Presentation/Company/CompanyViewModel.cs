using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Company;
using PersoNet.Contracts.Company;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;
using PersoNet.Desktop.Presentation.WorkTime;

namespace PersoNet.Desktop.Presentation.Company;

/// <summary>
/// "Ustawienia firmy" screen: the company's data, which kinds of leave employees request in the portal, and where working
/// time comes from. All are shown read-only and changed in dialogs.
/// </summary>
public sealed partial class CompanyViewModel(ICompanyApi api, IDialogService dialogs) : EditorViewModel
{
    public const string NotGiven = "nie podano";

    /// <summary>The company's data, one line per field.</summary>
    public ObservableCollection<CompanyFact> Details { get; } = [];

    /// <summary>Every kind of leave and who registers it.</summary>
    public ObservableCollection<PortalLeaveRow> LeaveKinds { get; } = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditDetailsCommand), nameof(EditLeaveCommand), nameof(EditWorkTimeCommand))]
    public partial CompanyResponse? Company { get; set; }

    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PortalSummary { get; set; } = string.Empty;

    /// <summary>How working time is recorded, e.g. "Roger PR Master".</summary>
    [ObservableProperty]
    public partial string WorkTimeSource { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string WorkTimeHint { get; set; } = string.Empty;

    [RelayCommand]
    public Task LoadAsync(CancellationToken cancellationToken) => RunAsync(async () => Show(await api.GetAsync(cancellationToken)));

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    public async Task EditDetailsAsync()
    {
        if (Company is not { } company)
        {
            return;
        }

        var dialog = new CompanyDetailsDialogViewModel(api, company);
        if (await dialogs.ShowAsync(dialog) && dialog.Saved is { } saved)
        {
            Show(saved);
        }
    }

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    public async Task EditLeaveAsync()
    {
        if (Company is not { } company)
        {
            return;
        }

        var dialog = new LeaveSettingsDialogViewModel(api, company);
        if (await dialogs.ShowAsync(dialog) && dialog.Saved is { } saved)
        {
            Show(saved);
        }
    }

    [RelayCommand(CanExecute = nameof(IsLoaded))]
    public async Task EditWorkTimeAsync()
    {
        if (Company is not { } company)
        {
            return;
        }

        var dialog = new WorkTimeSettingsDialogViewModel(api, company);
        if (await dialogs.ShowAsync(dialog) && dialog.Saved is { } saved)
        {
            Show(saved);
        }
    }

    protected override bool TryShowOnField(ApiException exception) => false;

    private bool IsLoaded() => Company is not null;

    private void Show(CompanyResponse company)
    {
        Company = company;
        Name = company.Name;

        Details.Clear();
        Details.Add(new CompanyFact("Nazwa", company.Name));
        Details.Add(new CompanyFact("Pełna nazwa", company.LegalName ?? NotGiven));
        Details.Add(new CompanyFact("NIP", company.TaxId is { } taxId ? FormatTaxId(taxId) : NotGiven));
        Details.Add(new CompanyFact("REGON", company.Regon ?? NotGiven));
        Details.Add(new CompanyFact("Adres", Address(company)));
        Details.Add(new CompanyFact("E-mail", company.Email ?? NotGiven));
        Details.Add(new CompanyFact("Telefon", company.Phone ?? NotGiven));

        LeaveKinds.Clear();
        foreach (var kind in LeaveTexts.Kinds)
        {
            LeaveKinds.Add(new PortalLeaveRow(kind.Name, company.PortalLeaveKinds.Contains(kind.Kind)));
        }

        var inPortal = LeaveKinds.Count(row => row.InPortal);
        PortalSummary = inPortal == LeaveKinds.Count ? "Pracownicy proszą w portalu o każdy rodzaj urlopu."
            : inPortal == 0 ? "Portal nie przyjmuje wniosków. Wszystkie urlopy wpisuje dział kadr."
            : $"W portalu: {inPortal} z {LeaveKinds.Count} rodzajów. Pozostałe wpisuje dział kadr.";

        var source = WorkTimeTexts.Sources.FirstOrDefault(choice => choice.Kind == company.WorkTimeSource) ?? WorkTimeTexts.Sources[0];
        WorkTimeSource = source.Name;
        WorkTimeHint = source.Hint;
    }

    /// <summary>NIP as people write it: 526-104-38-38.</summary>
    public static string FormatTaxId(string digits) =>
        digits.Length == 10 ? $"{digits[..3]}-{digits[3..6]}-{digits[6..8]}-{digits[8..]}" : digits;

    private static string Address(CompanyResponse company)
    {
        var town = string.Join(' ', new[] { company.PostalCode, company.City }.Where(part => !string.IsNullOrEmpty(part)));
        var address = string.Join(", ", new[] { company.Street, town }.Where(part => !string.IsNullOrEmpty(part)));
        return address.Length == 0 ? NotGiven : address;
    }
}

/// <summary>One line of the company's data; <see cref="IsGiven"/> is false for an empty field.</summary>
public sealed record CompanyFact(string Label, string Value)
{
    public bool IsGiven => Value != CompanyViewModel.NotGiven;
}

/// <summary>A kind of leave and whether employees request it in the portal or HR registers it.</summary>
public sealed record PortalLeaveRow(string Name, bool InPortal)
{
    public string Where => InPortal ? "pracownik w portalu" : "tylko dział kadr";
}
