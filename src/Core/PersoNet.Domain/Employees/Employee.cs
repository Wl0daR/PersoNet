using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Employees;

/// <summary>
/// A person employed by the company: identification, where they work (department, position, manager) and when.
/// Contract terms live in contracts; sensitive personal data (PESEL, address) will get its own, separately protected record.
/// </summary>
public sealed class Employee : Entity, ITenantOwned
{
    public const int NumberMaxLength = 20;
    public const int NameMaxLength = 100;
    public const int EmailMaxLength = 254;
    public const int PhoneMaxLength = 30;

    // Used by EF Core.
    private Employee()
    {
        Number = string.Empty;
        FirstName = string.Empty;
        LastName = string.Empty;
        SearchText = string.Empty;
    }

    private Employee(Guid tenantId, string number)
    {
        TenantId = tenantId;
        Number = number;
        FirstName = string.Empty;
        LastName = string.Empty;
        SearchText = string.Empty;
    }

    public Guid TenantId { get; private set; }

    /// <summary>
    /// Personnel number ("numer ewidencyjny"), unique within the company. Entered by HR, so numbering from an earlier
    /// system can be kept. Stored upper-case.
    /// </summary>
    public string Number { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    /// <summary>Work e-mail; unique within the company when given.</summary>
    public string? Email { get; private set; }

    public string? Phone { get; private set; }

    public Guid? DepartmentId { get; private set; }

    public Guid? PositionId { get; private set; }

    /// <summary>Direct manager (another employee). Cycles are checked by the caller, which knows the whole chain.</summary>
    public Guid? ManagerId { get; private set; }

    public DateOnly HireDate { get; private set; }

    /// <summary>Last day of work; null while employed.</summary>
    public DateOnly? TerminationDate { get; private set; }

    /// <summary>Upper-case "last first number email" used for case-insensitive search.</summary>
    public string SearchText { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public bool IsEmployedOn(DateOnly date) => HireDate <= date && (TerminationDate is null || date <= TerminationDate);

    public static Employee Create(Guid tenantId, EmployeeDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (tenantId == Guid.Empty)
        {
            throw new DomainException("Employee must belong to a tenant.");
        }

        var employee = new Employee(tenantId, NormalizeNumber(details.Number));
        employee.Update(details);
        return employee;
    }

    public void Update(EmployeeDetails details)
    {
        ArgumentNullException.ThrowIfNull(details);

        if (TerminationDate is { } terminated && details.HireDate > terminated)
        {
            throw new DomainException("Hire date cannot be after the termination date.");
        }

        Number = NormalizeNumber(details.Number);
        FirstName = Required(details.FirstName, NameMaxLength, "First name");
        LastName = Required(details.LastName, NameMaxLength, "Last name");
        Email = Optional(details.Email, EmailMaxLength, "E-mail")?.ToLowerInvariant();
        Phone = Optional(details.Phone, PhoneMaxLength, "Phone");
        HireDate = details.HireDate;
        DepartmentId = details.DepartmentId;
        PositionId = details.PositionId;

        if (details.ManagerId == Id)
        {
            throw new DomainException("Employee cannot be their own manager.");
        }

        ManagerId = details.ManagerId;
        SearchText = string.Join(' ', LastName, FirstName, Number, Email).ToUpperInvariant();
    }

    public void Terminate(DateOnly lastDay)
    {
        if (lastDay < HireDate)
        {
            throw new DomainException("Termination date cannot be before the hire date.");
        }

        TerminationDate = lastDay;
    }

    /// <summary>Undoes a termination, for example one entered by mistake.</summary>
    public void Reinstate() => TerminationDate = null;

    /// <summary>
    /// Hires a former employee again from <paramref name="hireDate"/>, which must come after the last day of the previous
    /// employment. The earlier contracts stay ended; the new one is added separately.
    /// </summary>
    public void Rehire(DateOnly hireDate)
    {
        if (TerminationDate is not { } lastDay)
        {
            throw new DomainException("Only a former employee can be hired again.");
        }

        if (hireDate <= lastDay)
        {
            throw new DomainException("The new hire date must come after the last day of the previous employment.");
        }

        HireDate = hireDate;
        TerminationDate = null;
    }

    private static string NormalizeNumber(string number)
    {
        var normalized = Required(number, NumberMaxLength, "Personnel number").ToUpperInvariant();

        if (normalized.Any(char.IsWhiteSpace))
        {
            throw new DomainException("Personnel number cannot contain spaces.");
        }

        return normalized;
    }

    private static string Required(string value, int maxLength, string what) =>
        Optional(value, maxLength, what) ?? throw new DomainException($"{what} is required.");

    private static string? Optional(string? value, int maxLength, string what)
    {
        var trimmed = value?.Trim();

        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        if (trimmed.Length > maxLength)
        {
            throw new DomainException($"{what} cannot exceed {maxLength} characters.");
        }

        return trimmed;
    }
}

/// <summary>Everything HR edits on an employee's card, in one value so create and update stay in step.</summary>
public sealed record EmployeeDetails(
    string Number,
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    DateOnly HireDate,
    Guid? DepartmentId,
    Guid? PositionId,
    Guid? ManagerId);
