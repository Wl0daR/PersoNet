using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace PersoNet.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers persistence, e-mail and file storage. Filled in as the modules are built.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        return services;
    }
}
