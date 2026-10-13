using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Organization;

/// <summary>
/// A job title used in the company, for example "Specjalista ds. kadr". Assigned to employees in their contracts.
/// </summary>
public sealed class Position : Entity, ITenantOwned
{
    public const int NameMaxLength = 200;

    // Used by EF Core.
    private Position()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
    }

    private Position(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
        NormalizedName = Normalize(name);
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Upper-case name used to keep names unique within a company regardless of letter case.
    /// </summary>
    public string NormalizedName { get; private set; }

    public bool IsActive { get; private set; }

    public static Position Create(Guid tenantId, string name)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Position must belong to a tenant.");
        }

        return new Position(tenantId, OrganizationNames.Normalize(name, NameMaxLength, "Position name"));
    }

    public void Rename(string name)
    {
        Name = OrganizationNames.Normalize(name, NameMaxLength, "Position name");
        NormalizedName = Normalize(Name);
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string Normalize(string name) => name.ToUpperInvariant();
}
