using Microsoft.AspNetCore.Components.Server.Circuits;

namespace PersoNet.Web.Authentication;

/// <summary>
/// The services of the Blazor circuit that is handling the current event. HTTP message handlers run in their own DI
/// scope, so this is how the API token handler finds the signed-in user of the circuit.
/// </summary>
internal sealed class CircuitServicesAccessor
{
    private readonly AsyncLocal<IServiceProvider?> _current = new();

    public IServiceProvider? Services
    {
        get => _current.Value;
        set => _current.Value = value;
    }
}

/// <summary>Sets <see cref="CircuitServicesAccessor.Services"/> for every event a circuit handles.</summary>
internal sealed class CircuitServicesHandler(IServiceProvider services, CircuitServicesAccessor accessor) : CircuitHandler
{
    public override Func<CircuitInboundActivityContext, Task> CreateInboundActivityHandler(Func<CircuitInboundActivityContext, Task> next) =>
        async context =>
        {
            accessor.Services = services;
            await next(context);
            accessor.Services = null;
        };
}
