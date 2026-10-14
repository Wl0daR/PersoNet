using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Company;
using PersoNet.Contracts.Company;
using PersoNet.Contracts.WorkTime;
using PersoNet.Desktop.Presentation.Common;
using PersoNet.Desktop.Presentation.WorkTime;

namespace PersoNet.Desktop.Presentation.Company;

/// <summary>
/// Dialog "Czas pracy": where the company's working time comes from. Whatever the choice, HR can type and correct any
/// day in the employee's card.
/// </summary>
public sealed partial class WorkTimeSettingsDialogViewModel : DialogViewModel
{
    private readonly ICompanyApi _api;

    public WorkTimeSettingsDialogViewModel(ICompanyApi api, CompanyResponse company)
    {
        ArgumentNullException.ThrowIfNull(company);

        _api = api;
        Options = [.. WorkTimeTexts.Sources.Select(source => new WorkTimeSourceOption(source, source.Kind == company.WorkTimeSource))];
    }

    public override string Title => "Źródło czasu pracy";

    public IReadOnlyList<WorkTimeSourceOption> Options { get; }

    /// <summary>The settings after a successful save.</summary>
    public CompanyResponse? Saved { get; private set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => RunAsync(async () =>
    {
        var chosen = Options.FirstOrDefault(option => option.IsChecked)?.Kind ?? WorkTimeSourceKind.Manual;
        Saved = await _api.SaveWorkTimeSettingsAsync(new SaveWorkTimeSettingsRequest(chosen), cancellationToken);

        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception) => false;
}

/// <summary>A way of recording working time with a radio button.</summary>
public sealed partial class WorkTimeSourceOption(WorkTimeSourceChoice source, bool isChecked) : ObservableObject
{
    public WorkTimeSourceKind Kind => source.Kind;

    public string Name => source.Name;

    public string Hint => source.Hint;

    [ObservableProperty]
    public partial bool IsChecked { get; set; } = isChecked;
}
