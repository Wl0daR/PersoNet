using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using PersoNet.ApiClient;
using PersoNet.ApiClient.Company;
using PersoNet.Contracts.Company;
using PersoNet.Desktop.Presentation.Common;

namespace PersoNet.Desktop.Presentation.Company;

/// <summary>Dialog "Dane firmy": name, NIP, REGON, address and contact. The server checks the numbers' checksums.</summary>
public sealed partial class CompanyDetailsDialogViewModel : DialogViewModel
{
    public const int NameMaxLength = 200;
    public const int LegalNameMaxLength = 300;
    public const int StreetMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;

    private readonly ICompanyApi _api;

    public CompanyDetailsDialogViewModel(ICompanyApi api, CompanyResponse company)
    {
        ArgumentNullException.ThrowIfNull(company);

        _api = api;
        EditName = company.Name;
        EditLegalName = company.LegalName ?? string.Empty;
        EditTaxId = company.TaxId is { } taxId ? CompanyViewModel.FormatTaxId(taxId) : string.Empty;
        EditRegon = company.Regon ?? string.Empty;
        EditStreet = company.Street ?? string.Empty;
        EditPostalCode = company.PostalCode ?? string.Empty;
        EditCity = company.City ?? string.Empty;
        EditEmail = company.Email ?? string.Empty;
        EditPhone = company.Phone ?? string.Empty;
        ClearAllErrors();
    }

    public override string Title => "Dane firmy";

    /// <summary>The data after a successful save.</summary>
    public CompanyResponse? Saved { get; private set; }

    [ObservableProperty]
    public partial string EditName { get; set; }

    [ObservableProperty]
    public partial string EditLegalName { get; set; }

    [ObservableProperty]
    public partial string EditTaxId { get; set; }

    [ObservableProperty]
    public partial string EditRegon { get; set; }

    [ObservableProperty]
    public partial string EditStreet { get; set; }

    [ObservableProperty]
    public partial string EditPostalCode { get; set; }

    [ObservableProperty]
    public partial string EditCity { get; set; }

    [ObservableProperty]
    public partial string EditEmail { get; set; }

    [ObservableProperty]
    public partial string EditPhone { get; set; }

    [RelayCommand]
    public Task SaveAsync(CancellationToken cancellationToken) => !Validate() ? Task.CompletedTask : RunAsync(async () =>
    {
        Saved = await _api.SaveDetailsAsync(
            new SaveCompanyDetailsRequest(EditName, EditLegalName, EditTaxId, EditRegon, EditStreet, EditPostalCode, EditCity, EditEmail, EditPhone),
            cancellationToken);

        HasChanges = true;
        Close(true);
    });

    protected override bool TryShowOnField(ApiException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var field = exception.Code switch
        {
            "company.name_required" or "company.name_too_long" => nameof(EditName),
            "company.tax_id_invalid" => nameof(EditTaxId),
            "company.regon_invalid" => nameof(EditRegon),
            "company.postal_code_invalid" => nameof(EditPostalCode),
            "company.email_invalid" => nameof(EditEmail),
            _ => null,
        };

        if (field is null)
        {
            return false;
        }

        SetError(field, exception.Message);
        return true;
    }

    // Quick checks of the form; the server checks the NIP and REGON checksums and says which one is wrong.
    private bool Validate()
    {
        SetError(nameof(EditName), string.IsNullOrWhiteSpace(EditName) ? "Podaj nazwę firmy."
            : EditName.Trim().Length > NameMaxLength ? $"Nazwa może mieć najwyżej {NameMaxLength} znaków."
            : null);
        SetError(nameof(EditTaxId), Digits(EditTaxId) is { } taxId && taxId.Length != 10 ? "NIP ma 10 cyfr." : null);
        SetError(nameof(EditRegon), Digits(EditRegon) is { } regon && regon.Length is not (9 or 14) ? "REGON ma 9 lub 14 cyfr." : null);
        SetError(nameof(EditPostalCode), !string.IsNullOrWhiteSpace(EditPostalCode) && !PostalCode().IsMatch(EditPostalCode.Trim())
            ? "Wpisz kod w formie 00-000." : null);
        SetError(nameof(EditEmail), !string.IsNullOrWhiteSpace(EditEmail) && !Email().IsMatch(EditEmail.Trim()) ? "Wpisz adres w formie kadry@firma.pl." : null);

        return !HasErrors;
    }

    private static string? Digits(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        var digits = string.Concat(trimmed.Where(character => character is not (' ' or '-')));
        return digits.Length == 0 ? null : digits;
    }

    [GeneratedRegex(@"^\d{2}-\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PostalCode();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex Email();
}
