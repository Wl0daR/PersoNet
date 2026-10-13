using Microsoft.Extensions.DependencyInjection;

namespace PersoNet.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_registers_system_time_provider()
    {
        using var provider = new ServiceCollection().AddApplication().BuildServiceProvider();

        provider.GetRequiredService<TimeProvider>().ShouldBe(TimeProvider.System);
    }
}
