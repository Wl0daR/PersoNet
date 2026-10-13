using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Domain.Abstractions;
using PersoNet.Domain.Organization;
using PersoNet.Domain.Tenants;

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

    public PersoNetDbContext(DbContextOptions<PersoNetDbContext> options, ITenantContext tenantContext)
        : this((DbContextOptions)options, tenantContext)
    {
    }

    /// <summary>
    /// Lets tests derive a context with extra entities.
    /// </summary>
    protected PersoNetDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<Position> Positions => Set<Position>();

    // Read by the query filters on every query, so each context instance sees its own tenant.
    private Guid? CurrentTenantId => _tenantContext.TenantId;

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        EnsureWritesStayInTenant();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        EnsureWritesStayInTenant();
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
