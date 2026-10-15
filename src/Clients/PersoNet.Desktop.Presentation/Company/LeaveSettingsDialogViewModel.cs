using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Company;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.Leave;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.Leave;

namespace PersoNet.Desktop.Presentation.Company;

/// <summary>
/// Dialog "Urlopy w portalu": the kinds of leave employees request in the portal themselves. HR can still register every
/// kind in this app.
/// </summary>
public sealed partial class LeaveSettingsDialogViewModel : DialogViewModel
{
    private readonly ICompanyApi _api;

    public LeaveSettingsDialogViewModel(ICompanyApi api, CompanyResponse company)
    {
        ArgumentNullException.ThrowIfNull(company);

        _api = api;
        Options = [.. LeaveTexts.Kinds.Select(kind => new PortalLeaveOption(kind.Kind, kind.Name, Hint(kind.Kind), company.PortalLeaveKinds.Contains(kind.Kind)))];
    }

    public override string Title => "Urlopy w portalu";

    public IReadOnlyList<PortalLeaveOption> Options { get; }

    /// <summary>The settings after a successful save.</summary>
    public CompanyResponse? Saved { get; private set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var kinds = Options.Where(option => option.IsChecked).Select(option => option.Kind).ToList();
        Saved = await _api.SaveLeaveSettingsAsync(new SaveLeaveSettingsRequest(kinds), cancellationToken);

        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception) => false;

    private static string Hint(LeaveKind kind) => kind switch
    {
        LeaveKind.Annual => "Z rocznego wymiaru; przełożony zatwierdza wniosek.",
        LeaveKind.OnDemand => "Najwyżej 4 dni w roku, w ramach urlopu wypoczynkowego.",
        LeaveKind.Occasional => "Ślub, narodziny dziecka, pogrzeb. Często wymaga dokumentu, więc wiele firm przyjmuje go w kadrach.",
        LeaveKind.Unpaid => "Na pisemny wniosek pracownika.",
        LeaveKind.ChildCare => "2 dni lub 16 godzin w roku (art. 188 KP).",
        LeaveKind.ForceMajeure => "2 dni lub 16 godzin w roku (art. 148¹ KP).",
        LeaveKind.Maternity or LeaveKind.Paternity or LeaveKind.Parental or LeaveKind.ChildRearing => "Na wniosek z dokumentami; zwykle przyjmuje go kadry.",
        LeaveKind.OvertimeTimeOff => "Czas wolny za nadgodziny (art. 151² KP).",
        LeaveKind.HolidayTimeOff => "Dzień wolny za święto przypadające w sobotę.",
        LeaveKind.PaidRelease => "Inne zwolnienie od pracy z zachowaniem wynagrodzenia.",
        _ => string.Empty,
    };
}

/// <summary>A kind of leave with a check box: checked means employees request it in the portal.</summary>
public sealed partial class PortalLeaveOption(LeaveKind kind, string name, string hint, bool isChecked) : ObservableObject
{
    public LeaveKind Kind => kind;

    public string Name => name;

    public string Hint => hint;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}
