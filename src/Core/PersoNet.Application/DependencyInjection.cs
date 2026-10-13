using Microsoft.Extensions.DependencyInjection;
using PersoNet.Application.Employees;
using PersoNet.Application.Organization;

namespace PersoNet.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(TimeProvider.System);

        services.AddScoped<DepartmentQueries>();
        services.AddScoped<DepartmentCommands>();
        services.AddScoped<PositionQueries>();
        services.AddScoped<PositionCommands>();
        services.AddScoped<EmployeeQueries>();
        services.AddScoped<EmployeeCommands>();
        services.AddScoped<ContractService>();

        return services;
    }
}
