using Microsoft.Extensions.DependencyInjection;
using PersoNet.Application.Accounts;
using PersoNet.Application.Audit;
using PersoNet.Application.Employees;
using PersoNet.Application.Leave;
using PersoNet.Application.Organization;
using PersoNet.Application.SelfService;

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
        services.AddScoped<LeaveService>();
        services.AddScoped<PortalService>();
        services.AddScoped<AccountService>();
        services.AddScoped<AuditQueries>();

        return services;
    }
}
