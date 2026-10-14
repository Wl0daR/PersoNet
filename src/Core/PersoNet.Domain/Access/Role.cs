using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Organization;

namespace PersoNet.Domain.Access;

/// <summary>
/// A set of permissions the company defines and gives to people, for example "Kierownik działu" or "Kadrowa". The
/// permission codes are checked by the caller against the list the application knows.
/// </summary>
public sealed class Role : Entity, ITenantOwned
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    // Used by EF Core.
    private Role()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        Permissions = [];
    }

    private Role(Guid tenantId)
        : this()
    {
        TenantId = tenantId;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Upper-case name that keeps names unique within a company regardless of letter case.</summary>
    public string NormalizedName { get; private set; }

    public string? Description { get; private set; }

    /// <summary>Permission codes the role grants, sorted and without repeats.</summary>
    public List<string> Permissions { get; private set; }

    public static Role Create(Guid tenantId, string name, string? description, IEnumerable<string> permissions)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Role must belong to a tenant.");
        }

        var role = new Role(tenantId);
        role.Change(name, description, permissions);
        return role;
    }

    public void Change(string name, string? description, IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        Name = OrganizationNames.Normalize(name, NameMaxLength, "Role name");
        NormalizedName = Name.ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null
            : description.Trim() is { Length: <= DescriptionMaxLength } trimmed ? trimmed
            : throw new DomainException($"Role description can have at most {DescriptionMaxLength} characters.");
        Permissions = permissions.Select(code => code.Trim()).Where(code => code.Length > 0).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
    }
}

/// <summary>A role given to an employee; they have its permissions while employed and signed in with their work e-mail.</summary>
public sealed class RoleAssignment : Entity, ITenantOwned
{
    // Used by EF Core.
    private RoleAssignment()
    {
    }

    private RoleAssignment(Guid tenantId, Guid employeeId, Guid roleId)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        RoleId = roleId;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public Guid RoleId { get; private set; }

    public static RoleAssignment Create(Guid tenantId, Guid employeeId, Guid roleId) =>
        tenantId == Guid.Empty || employeeId == Guid.Empty || roleId == Guid.Empty
            ? throw new DomainException("A role is given to an employee of a tenant.")
            : new RoleAssignment(tenantId, employeeId, roleId);
}
