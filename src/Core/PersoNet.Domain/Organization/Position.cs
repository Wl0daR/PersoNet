using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Organization;

/// <summary>
/// A job title used in the company, for example "Specjalista ds. kadr". Assigned to employees in their contracts.
/// A position can be tied to the departments where it exists, so HR picking a department sees only its positions;
/// one tied to no department fits everywhere.
/// </summary>
public sealed class Position : Entity, ITenantOwned
{
    public const int NameMaxLength = 200;

    // Used by EF Core.
    private Position()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        DepartmentIds = [];
    }

    private Position(Guid tenantId, string name)
    {
        TenantId = tenantId;
        Name = name;
        NormalizedName = Normalize(name);
        IsActive = true;
        DepartmentIds = [];
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Upper-case name used to keep names unique within a company regardless of letter case.
    /// </summary>
    public string NormalizedName { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Departments the position belongs to, without repeats; empty means any department.</summary>
    public List<Guid> DepartmentIds { get; private set; }

    public static Position Create(Guid tenantId, string name, IEnumerable<Guid>? departmentIds = null)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Position must belong to a tenant.");
        }

        var position = new Position(tenantId, OrganizationNames.Normalize(name, NameMaxLength, "Position name"));
        position.AssignDepartments(departmentIds ?? []);
        return position;
    }

    public void Rename(string name)
    {
        Name = OrganizationNames.Normalize(name, NameMaxLength, "Position name");
        NormalizedName = Normalize(Name);
    }

    /// <summary>Replaces the departments the position belongs to.</summary>
    public void AssignDepartments(IEnumerable<Guid> departmentIds)
    {
        ArgumentNullException.ThrowIfNull(departmentIds);

        var ids = departmentIds.Distinct().ToList();
        if (ids.Contains(Guid.Empty))
        {
            throw new DomainException("Position department must be a real department.");
        }

        // A new list rather than changes in place, so EF Core and the audit log see the change.
        if (!ids.Order().SequenceEqual(DepartmentIds.Order()))
        {
            DepartmentIds = ids;
        }
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string Normalize(string name) => name.ToUpperInvariant();
}
