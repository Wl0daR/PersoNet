using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PersoNet.ApiClient;
using PersoNet.Desktop.Authentication;
using PersoNet.Desktop.Presentation.Organization;
using PersoNet.Desktop.Presentation.Shell;

namespace PersoNet.Desktop;

/// <summary>
/// Composition root of the desktop app: the same Generic Host (DI, configuration, logging) as the server hosts.
/// </summary>
public partial class App : Application
{
    private readonly IHost _host;

    public App()
    {
        var builder = Host.CreateApplicationBuilder();

        // Sign-in also provides the access token for API calls, so it is registered before the API client.
        builder.Services.AddDesktopAuthentication(builder.Configuration);

        var apiBaseAddress = builder.Configuration["PersoNetApi:BaseAddress"] ?? "https://localhost:7016";
        builder.Services.AddPersoNetApiClient(new Uri(apiBaseAddress));

        builder.Services.AddSingleton<DepartmentsViewModel>();
        builder.Services.AddSingleton<PositionsViewModel>();
        builder.Services.AddSingleton<ShellViewModel>();
        builder.Services.AddSingleton<MainWindow>();

        _host = builder.Build();
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        await _host.StartAsync().ConfigureAwait(true);

        var window = _host.Services.GetRequiredService<MainWindow>();
        window.Show();

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await _host.StopAsync().ConfigureAwait(true);
        _host.Dispose();

        base.OnExit(e);
    }
}
