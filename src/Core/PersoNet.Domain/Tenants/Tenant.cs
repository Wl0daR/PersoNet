using System.Text.RegularExpressions;
using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Tenants;

/// <summary>
/// A customer company. Root of all company data; every tenant-owned record carries its id.
/// </summary>
public sealed partial class Tenant : Entity
{
    public const int NameMaxLength = 200;
    public const int SubdomainMaxLength = 63;

    // Used by EF Core.
    private Tenant()
    {
        Name = string.Empty;
        Subdomain = string.Empty;
    }

    private Tenant(string name, string subdomain)
    {
        Name = name;
        Subdomain = subdomain;
        IsActive = true;
    }

    public string Name { get; private set; }

    /// <summary>
    /// DNS label of the company's address, for example "acme" in acme.personet.pl.
    /// </summary>
    public string Subdomain { get; private set; }

    public bool IsActive { get; private set; }

    public static Tenant Create(string name, string subdomain) =>
        new(NormalizeName(name), NormalizeSubdomain(subdomain));

    public void Rename(string name) => Name = NormalizeName(name);

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string NormalizeName(string name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            throw new DomainException("Tenant name is required.");
        }

        if (trimmed.Length > NameMaxLength)
        {
            throw new DomainException($"Tenant name cannot exceed {NameMaxLength} characters.");
        }

        return trimmed;
    }

    private static string NormalizeSubdomain(string subdomain)
    {
        var normalized = subdomain?.Trim().ToLowerInvariant() ?? string.Empty;

        if (!SubdomainPattern().IsMatch(normalized))
        {
            throw new DomainException(
                "Subdomain must be 1-63 characters of lowercase letters, digits or hyphens, and cannot start or end with a hyphen.");
        }

        return normalized;
    }

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex SubdomainPattern();
}
