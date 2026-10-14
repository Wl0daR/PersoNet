using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Access;
using PersoNet.Domain.Audit;
using PersoNet.Domain.Company;
using PersoNet.Domain.Employees;
using PersoNet.Domain.Leave;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Tenants;
using PersoNet.Domain.WorkTime;
using PersoNet.Infrastructure.Audit;

namespace PersoNet.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context. Every <see cref="ITenantOwned"/> entity is filtered to the current tenant,
/// writes of another tenant's data are rejected, and every connection carries the tenant for PostgreSQL row-level security
/// (see docs/adr/0003).
/// </summary>
public class PersoNetDbContext : DbContext, IAppDbContext
{
    public const string TenantFilter = "Tenant";

    private static readonly MethodInfo ApplyTenantFilterMethod =
        typeof(PersoNetDbContext).GetMethod(nameof(ApplyTenantFilter), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private readonly ITenantContext _tenantContext;
    private readonly IAuditActor _auditActor;
    private readonly TimeProvider _time;

    public PersoNetDbContext(DbContextOptions<PersoNetDbContext> options, ITenantContext tenantContext, IAuditActor? auditActor = null, TimeProvider? time = null)
        : this((DbContextOptions)options, tenantContext, auditActor, time)
    {
    }

    /// <summary>
    /// Lets tests derive a context with extra entities.
    /// </summary>
    protected PersoNetDbContext(DbContextOptions options, ITenantContext tenantContext, IAuditActor? auditActor = null, TimeProvider? time = null)
        : base(options)
    {
        _tenantContext = tenantContext;
        _auditActor = auditActor ?? new SystemAuditActor();
        _time = time ?? TimeProvider.System;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Position> Positions => Set<Position>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<EmploymentContract> Contracts => Set<EmploymentContract>();

    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<LeaveEntitlement> LeaveEntitlements => Set<LeaveEntitlement>();

    public DbSet<AuditEntry> AuditLog => Set<AuditEntry>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RoleAssignment> RoleAssignments => Set<RoleAssignment>();

    public DbSet<CompanySettings> CompanySettings => Set<CompanySettings>();

    public DbSet<WorkDay> WorkDays => Set<WorkDay>();

    public DbSet<ClockEvent> ClockEvents => Set<ClockEvent>();

    // Read by the query filters on every query, so each context instance sees its own tenant.
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureWritesStayInTenant();
        RecordAuditTrail();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureWritesStayInTenant();
        RecordAuditTrail();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        ArgumentNullException.ThrowIfNull(optionsBuilder);

        optionsBuilder.AddInterceptors(new TenantSessionInterceptor(() => CurrentTenantId));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PersoNetDbContext).Assembly);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType) && entityType.BaseType is null)
            {
                ApplyTenantFilterMethod.MakeGenericMethod(entityType.ClrType).Invoke(this, [modelBuilder]);
            }
        }
    }

    private void ApplyTenantFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ITenantOwned
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(TenantFilter, entity => entity.TenantId == CurrentTenantId);
        modelBuilder.Entity<TEntity>().HasIndex(entity => entity.TenantId);
    }

    // Every change of company data gets its audit entry in the same save, so both are stored or neither.
    private void RecordAuditTrail()
    {
        var entries = AuditTrail.Collect(ChangeTracker, _auditActor, _time.GetUtcNow());
        if (entries.Count > 0)
        {
            AuditLog.AddRange(entries);
        }
    }

    private void EnsureWritesStayInTenant()
    {
        var tenantId = CurrentTenantId;

        foreach (var entry in ChangeTracker.Entries<ITenantOwned>())
        {
            if (entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
                && entry.Entity.TenantId != tenantId)
            {
                throw new InvalidOperationException(
                    $"Cannot save {entry.Entity.GetType().Name} of another tenant than the current one.");
            }
        }
    }
}
