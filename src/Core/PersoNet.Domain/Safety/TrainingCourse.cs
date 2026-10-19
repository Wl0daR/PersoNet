using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Organization;

namespace PersoNet.Domain.Safety;

/// <summary>
/// A training the company runs or sends people to, for example "BHP – szkolenie okresowe" or a product training. It may
/// have to be repeated after <see cref="ValidityMonths"/>, and may be mandatory for everyone or for chosen departments
/// (and the departments under them), so HR sees who has not had it or whose one is running out.
/// </summary>
public sealed class TrainingCourse : Entity, ITenantOwned
{
    public const int NameMaxLength = 200;

    public const int DescriptionMaxLength = 1000;

    /// <summary>Longest validity accepted: twenty years.</summary>
    public const int MaxValidityMonths = 240;

    // Used by EF Core.
    private TrainingCourse()
    {
        Name = string.Empty;
        NormalizedName = string.Empty;
        DepartmentIds = [];
    }

    private TrainingCourse(Guid tenantId)
    {
        TenantId = tenantId;
        Name = string.Empty;
        NormalizedName = string.Empty;
        IsActive = true;
        DepartmentIds = [];
    }

    public Guid TenantId { get; private set; }

    public string Name { get; private set; }

    /// <summary>Upper-case name keeping names unique within a company regardless of letter case.</summary>
    public string NormalizedName { get; private set; }

    /// <summary>How long a completed training is valid; null when it does not expire.</summary>
    public int? ValidityMonths { get; private set; }

    /// <summary>True when the people it concerns must have it (everyone, or those of <see cref="DepartmentIds"/>).</summary>
    public bool IsMandatory { get; private set; }

    /// <summary>Departments a mandatory training concerns, with the ones under them; empty means everyone.</summary>
    public List<Guid> DepartmentIds { get; private set; }

    public string? Description { get; private set; }

    public bool IsActive { get; private set; }

    public static TrainingCourse Create(Guid tenantId, string name, int? validityMonths, bool isMandatory, IEnumerable<Guid>? departmentIds, string? description)
    {
        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Training course must belong to a tenant.");
        }

        var course = new TrainingCourse(tenantId);
        course.Change(name, validityMonths, isMandatory, departmentIds ?? [], description);
        return course;
    }

    public void Change(string name, int? validityMonths, bool isMandatory, IEnumerable<Guid> departmentIds, string? description)
    {
        ArgumentNullException.ThrowIfNull(departmentIds);

        if (validityMonths is < 1 or > MaxValidityMonths)
        {
            throw new DomainException($"Training validity must be from 1 to {MaxValidityMonths} months.");
        }

        var cleaned = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        if (cleaned is { Length: > DescriptionMaxLength })
        {
            throw new DomainException($"A description can have at most {DescriptionMaxLength} characters.");
        }

        var ids = departmentIds.Distinct().ToList();
        if (ids.Contains(Guid.Empty))
        {
            throw new DomainException("Training department must be a real department.");
        }

        Name = OrganizationNames.Normalize(name, NameMaxLength, "Training name");
        NormalizedName = Name.ToUpperInvariant();
        ValidityMonths = validityMonths;
        IsMandatory = isMandatory;
        Description = cleaned;

        // A new list rather than changes in place, so EF Core and the audit log see the change.
        if (!ids.Order().SequenceEqual(DepartmentIds.Order()))
        {
            DepartmentIds = ids;
        }
    }

    /// <summary>The last day a training completed on that day is valid, or null when it does not expire.</summary>
    public DateOnly? ValidUntil(DateOnly completedOn) => ValidityMonths is { } months ? completedOn.AddMonths(months) : null;

    /// <summary>
    /// True when the training is mandatory for someone in this department, given the department and the ones above it
    /// (<paramref name="departmentAndAncestors"/>); a training for everyone concerns people without a department too.
    /// </summary>
    public bool IsMandatoryFor(IReadOnlyCollection<Guid> departmentAndAncestors)
    {
        ArgumentNullException.ThrowIfNull(departmentAndAncestors);

        return IsActive && IsMandatory && (DepartmentIds.Count == 0 || DepartmentIds.Any(departmentAndAncestors.Contains));
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;
}
