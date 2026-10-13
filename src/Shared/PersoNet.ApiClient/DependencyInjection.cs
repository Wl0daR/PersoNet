using Microsoft.Extensions.DependencyInjection;

namespace PersoNet.ApiClient;

public static class DependencyInjection
{
    public const string HttpClientName = "PersoNet.Api";

    /// <summary>
    /// Registers the HTTP client used by the desktop and web apps to call PersoNet.Api.
    /// The typed client generated from the OpenAPI document will be added on top of it.
    /// </summary>
    public static IHttpClientBuilder AddPersoNetApiClient(this IServiceCollection services, Uri baseAddress)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(baseAddress);

        var builder = services.AddHttpClient(HttpClientName, client => client.BaseAddress = baseAddress);
        builder.AddStandardResilienceHandler();

        return builder;
    }
}
