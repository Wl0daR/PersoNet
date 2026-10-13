using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PersoNet.Application.Abstractions;
using PersoNet.Infrastructure.Persistence;
using PersoNet.Infrastructure.Tenancy;

namespace PersoNet.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence. Hosts with signed-in users register their own <see cref="ITenantContext"/> before calling this.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(DbContextOptionsSetup.ConnectionStringName)
            ?? throw new InvalidOperationException(
                $"Connection string '{DbContextOptionsSetup.ConnectionStringName}' is missing. See README for local setup.");

        services.TryAddScoped<ITenantContext, NoTenantContext>();

        services.AddDbContext<PersoNetDbContext>(options => options.UsePersoNetDatabase(connectionString));
        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<PersoNetDbContext>());

        services.AddHealthChecks().AddDbContextCheck<PersoNetDbContext>("database");

        return services;
    }

    /// <summary>
    /// Applies pending migrations. Used at startup in Development only; deployments run migrations as a separate step.
    /// </summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PersoNetDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}
