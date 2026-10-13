using PersoNet.Domain.Abstractions;

namespace PersoNet.Domain.Leave;

/// <summary>
/// Annual leave days an employee may take in a year ("wymiar urlopu"), set by HR, plus days carried over from the previous
/// year. Usually 20 or 26 days (art. 154 KP), proportionally less for part-time work or a part of the year.
/// </summary>
public sealed class LeaveEntitlement : Entity, ITenantOwned
{
    public const int MaxDays = 60;

    /// <summary>One day of leave is 8 hours when the entitlement is turned into hours (art. 154² § 1 KP), also for part-time work.</summary>
    public const int HoursPerDay = 8;

    // Used by EF Core.
    private LeaveEntitlement()
    {
    }

    private LeaveEntitlement(Guid tenantId, Guid employeeId, int year)
    {
        TenantId = tenantId;
        EmployeeId = employeeId;
        Year = year;
    }

    public Guid TenantId { get; private set; }

    public Guid EmployeeId { get; private set; }

    public int Year { get; private set; }

    public int Days { get; private set; }

    /// <summary>Unused days from the previous year ("urlop zaległy").</summary>
    public int CarriedOverDays { get; private set; }

    public int Total => Days + CarriedOverDays;

    /// <summary>The whole entitlement in hours, which leave is taken from.</summary>
    public int TotalHours => Total * HoursPerDay;

    public static LeaveEntitlement Create(Guid tenantId, Guid employeeId, int year, int days, int carriedOverDays)
    {
        if (tenantId == Guid.Empty || employeeId == Guid.Empty)
        {
            throw new DomainException("Entitlement must belong to a tenant and an employee.");
        }

        if (year is < 2000 or > 2100)
        {
            throw new DomainException("Year is out of range.");
        }

        var entitlement = new LeaveEntitlement(tenantId, employeeId, year);
        entitlement.Change(days, carriedOverDays);
        return entitlement;
    }

    public void Change(int days, int carriedOverDays)
    {
        if (days is < 0 or > MaxDays || carriedOverDays is < 0 or > MaxDays)
        {
            throw new DomainException($"Leave days must be between 0 and {MaxDays}.");
        }

        Days = days;
        CarriedOverDays = carriedOverDays;
    }
}
