using System.Text.RegularExpressions;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Leave;

namespace PersoNet.Domain.Company;

/// <summary>
/// The company's own data (name, tax and statistical numbers, address, contact) and how it runs leave. One record per
/// company, set by HR; until then the defaults apply (see <see cref="CreateDefault"/>).
/// </summary>
public sealed partial class CompanySettings : Entity, ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int LegalNameMaxLength = 300;
    public const int StreetMaxLength = 200;
    public const int CityMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;

    // Used by EF Core.
    private CompanySettings()
    {
        Name = string.Empty;
        PortalLeaveTypes = [];
    }

    private CompanySettings(Guid tenantId)
        : this()
    {
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }

    /// <summary>The name people use and the apps show, for example "Acme".</summary>
    public string Name { get; private set; }

    /// <summary>The full registered name, for example "Acme Polska sp. z o.o.".</summary>
    public string? LegalName { get; private set; }

    /// <summary>NIP, ten digits without separators.</summary>
    public string? TaxId { get; private set; }

    /// <summary>REGON, nine or fourteen digits.</summary>
    public string? Regon { get; private set; }

    public string? Street { get; private set; }

    /// <summary>Polish postal code in the form 00-000.</summary>
    public string? PostalCode { get; private set; }

    public string? City { get; private set; }

    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    /// <summary>Kinds of leave employees may request in the portal themselves; HR registers the others.</summary>
    public List<LeaveType> PortalLeaveTypes { get; private set; }

    /// <summary>The settings a company has before HR changes them: its name, no details, every kind of leave in the portal.</summary>
    public static CompanySettings CreateDefault(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Company settings must belong to a tenant.");
        }

        var settings = new CompanySettings(tenantId);
        settings.ChangeDetails(new CompanyDetails(name, null, null, null, null, null, null, null, null));
        settings.ChangePortalLeaveTypes(Enum.GetValues<LeaveType>());
        return settings;
    }

    public void ChangeDetails(CompanyDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        var name = details.Name?.Trim();
        Name = string.IsNullOrEmpty(name) ? throw new DomainException("Company name is required.")
            : name.Length > NameMaxLength ? throw new DomainException($"Company name cannot exceed {NameMaxLength} characters.")
            : name;
        LegalName = Optional(details.LegalName, LegalNameMaxLength, "Legal name");

        var taxId = Digits(details.TaxId);
        TaxId = taxId is null || IsValidTaxId(taxId) ? taxId : throw new DomainException("Tax id (NIP) is not valid.");

        var regon = Digits(details.Regon);
        Regon = regon is null || IsValidRegon(regon) ? regon : throw new DomainException("REGON is not valid.");

        Street = Optional(details.Street, StreetMaxLength, "Street");
        var postalCode = Optional(details.PostalCode, 6, "Postal code");
        PostalCode = postalCode is null || IsValidPostalCode(postalCode) ? postalCode : throw new DomainException("Postal code must look like 00-000.");
        City = Optional(details.City, CityMaxLength, "City");

        var email = Optional(details.Email, EmailMaxLength, "E-mail")?.ToLowerInvariant();
        Email = email is null || IsValidEmail(email) ? email : throw new DomainException("E-mail is not valid.");
        Phone = Optional(details.Phone, PhoneMaxLength, "Phone");
    }

    public void ChangePortalLeaveTypes(IEnumerable<LeaveType> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        var chosen = types.Distinct().Order().ToList();
        if (chosen.Any(type => !Enum.IsDefined(type)))
        {
            throw new DomainException("Unknown kind of leave.");
        }

        PortalLeaveTypes = chosen;
    }

    public bool AllowsInPortal(LeaveType type) => PortalLeaveTypes.Contains(type);

    /// <summary>NIP: ten digits whose last one is the checksum of the first nine (weights 6, 5, 7, 2, 3, 4, 5, 6, 7).</summary>
    public static bool IsValidTaxId(string digits) =>
        digits is { Length: 10 } && digits.All(char.IsAsciiDigit)
        && Checksum(digits, [6, 5, 7, 2, 3, 4, 5, 6, 7]) is var check and < 10 && check == digits[9] - '0';

    /// <summary>REGON: nine or fourteen digits, the last one a checksum of the others.</summary>
    public static bool IsValidRegon(string digits)
    {
        if (!digits.All(char.IsAsciiDigit))
        {
            return false;
        }

        int[]? weights = digits.Length switch
        {
            9 => [8, 9, 2, 3, 4, 5, 6, 7],
            14 => [2, 4, 8, 5, 0, 9, 7, 3, 6, 1, 2, 4, 8],
            _ => null,
        };

        return weights is not null && Checksum(digits, weights) % 10 == digits[^1] - '0';
    }

    public static bool IsValidPostalCode(string value) => PostalCodePattern().IsMatch(value);

    public static bool IsValidEmail(string value) => EmailPattern().IsMatch(value);

    /// <summary>The digits of a number typed with spaces, dashes or a "PL" prefix; null when nothing was typed.</summary>
    public static string? Digits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("PL", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[2..];
        }

        return string.Concat(trimmed.Where(character => character is not (' ' or '-')));
    }

    private static int Checksum(string digits, int[] weights) =>
        weights.Select((weight, index) => weight * (digits[index] - '0')).Sum() % 11;

    private static string? Optional(string? value, int maxLength, string what)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null
            : trimmed.Length > maxLength ? throw new DomainException($"{what} cannot exceed {maxLength} characters.")
            : trimmed;
    }

    [GeneratedRegex(@"^\d{2}-\d{3}$", RegexOptions.CultureInvariant)]
    private static partial Regex PostalCodePattern();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex EmailPattern();
}

/// <summary>The company's data as HR types it.</summary>
public sealed record CompanyDetails(
    string Name,
    string? LegalName,
    string? TaxId,
    string? Regon,
    string? Street,
    string? PostalCode,
    string? City,
    string? Email,
    string? Phone);
