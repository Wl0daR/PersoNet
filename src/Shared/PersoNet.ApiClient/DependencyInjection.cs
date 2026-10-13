using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using PersoNet.ApiClient.Authentication;
using PersoNet.ApiClient.Employees;
using PersoNet.ApiClient.Organization;

namespace PersoNet.ApiClient;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the typed PersoNet.Api clients used by the desktop and web apps. Calls carry the token from the
    /// app's <see cref="IAccessTokenProvider"/>; register it before or after this call.
    /// </summary>
    public static IServiceCollection AddPersoNetApiClient(this IServiceCollection services, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        // A trailing slash makes relative paths like "api/v1/..." resolve under the base address.
        var root = baseAddress.AbsoluteUri.EndsWith('/') ? baseAddress : new Uri(baseAddress.AbsoluteUri + "/");

        services.TryAddSingleton<IAccessTokenProvider, NoAccessTokenProvider>();
        services.AddTransient<AccessTokenHandler>();

        services.AddHttpClient<IOrganizationApi, OrganizationApi>(client => client.BaseAddress = root)
            .AddHttpMessageHandler<AccessTokenHandler>()
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

        services.AddHttpClient<IEmployeesApi, EmployeesApi>(client => client.BaseAddress = root)
            .AddHttpMessageHandler<AccessTokenHandler>()
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());

        return services;
    }
}
