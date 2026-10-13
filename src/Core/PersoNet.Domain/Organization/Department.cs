using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Organization;

/// <summary>
/// An organizational unit of a company, for example "Dział IT". Departments form a tree through <see cref="ParentId"/>.
/// </summary>
public sealed class Department : Entity, ITenantOwned
{
    public const int NameMaxLength = 200;
    public const int CodeMaxLength = 20;

    // Used by EF Core.
    private Department()
    {
        Name = string.Empty;
        Code = string.Empty;
    }

    private Department(Guid tenantId, string name, string code, Guid? parentId)
    {
        TenantId = tenantId;
        Name = name;
        Code = code;
        ParentId = parentId;
        IsActive = true;
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Short, unique code within the company, for example "IT" or "KAD". Stored upper-case.
    /// </summary>
    public string Code { get; private set; }

    public Guid? ParentId { get; private set; }

    public bool IsActive { get; private set; }

    public static Department Create(Guid tenantId, string name, string code, Guid? parentId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Department must belong to a tenant.");
        }

        return new Department(tenantId, OrganizationNames.Normalize(name, NameMaxLength, "Department name"), NormalizeCode(code), parentId);
    }

    public void Update(string name, string code)
    {
        Name = OrganizationNames.Normalize(name, NameMaxLength, "Department name");
        Code = NormalizeCode(code);
    }

    /// <summary>
    /// Moves the department under another one, or to the top level when <paramref name="parentId"/> is null.
    /// Cycles deeper than the department itself are checked by the caller, which knows the whole tree.
    /// </summary>
    public void MoveTo(Guid? parentId)
    {
        if (parentId == Id)
        {
            throw new DomainException("Department cannot be its own parent.");
        }

        ParentId = parentId;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static string NormalizeCode(string code)
    {
        var normalized = OrganizationNames.Normalize(code, CodeMaxLength, "Department code").ToUpperInvariant();

        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new DomainException("Department code cannot contain spaces.");
        }

        return normalized;
    }
}
