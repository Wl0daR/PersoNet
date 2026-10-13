using Microsoft.EntityFrameworkCore;
using PersoNet.Application.Abstractions;
using PersoNet.Contracts;
using PersoNet.Domain.Tenants;

namespace PersoNet.Api.Development;

internal static class DevelopmentData
{
    /// <summary>
    /// Creates the demo company that the demo account in PersoNet.Identity belongs to.
    /// </summary>
    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<IAppDbContext>();

        var seed = PersoNetAuth.DevelopmentSeed.TenantId;
        if (await dbContext.Tenants.AnyAsync(tenant => tenant.Id == seed, cancellationToken))
        {
            return;
        }

        dbContext.Tenants.Add(Tenant.Create(
            seed,
            PersoNetAuth.DevelopmentSeed.TenantName,
            PersoNetAuth.DevelopmentSeed.TenantSubdomain));

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
