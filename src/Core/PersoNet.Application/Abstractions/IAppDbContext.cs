using Microsoft.EntityFrameworkCore;
using PersoNet.Domain.Absences;
using PersoNet.Domain.Access;
using PersoNet.Domain.Audit;
using PersoNet.Domain.Company;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Safety;
using PersoNet.Domain.Tenants;
using PersoNet.Domain.WorkTime;

namespace PersoNet.Application.Abstractions;

/// <summary>
/// The persistence entry point for use cases. The DbContext is the unit of work, so there is no repository layer on top.
/// </summary>
public interface IAppDbContext
{
    DbSet<Tenant> Tenants { get; }

    DbSet<Department> Departments { get; }

    DbSet<Position> Positions { get; }

    DbSet<Employee> Employees { get; }

    DbSet<EmploymentContract> Contracts { get; }

    DbSet<LeaveRequest> LeaveRequests { get; }

    DbSet<LeaveEntitlement> LeaveEntitlements { get; }

    /// <summary>Who changed what and when. Filled automatically on save; add entries only for changes kept elsewhere (accounts).</summary>
    DbSet<AuditEntry> AuditLog { get; }

    /// <summary>Roles the company defines, with the permissions each grants.</summary>
    DbSet<Role> Roles { get; }

    /// <summary>Which employee has which role.</summary>
    DbSet<RoleAssignment> RoleAssignments { get; }

    /// <summary>The company's data and leave settings; at most one record, missing until HR first saves them.</summary>
    DbSet<CompanySettings> CompanySettings { get; }

    /// <summary>Days of employees' timesheets: hours worked or absences.</summary>
    DbSet<WorkDay> WorkDays { get; }

    /// <summary>Clock-in and clock-out events from time clock systems, kept as they came.</summary>
    DbSet<ClockEvent> ClockEvents { get; }

    /// <summary>Sickness, unexcused absences and work away from the workplace, as periods.</summary>
    DbSet<Absence> Absences { get; }

    /// <summary>The company's list of trainings.</summary>
    DbSet<TrainingCourse> TrainingCourses { get; }

    /// <summary>Trainings employees completed.</summary>
    DbSet<EmployeeTraining> EmployeeTrainings { get; }

    /// <summary>Occupational medical examinations of employees.</summary>
    DbSet<MedicalExam> MedicalExams { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
