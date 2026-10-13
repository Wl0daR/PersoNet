using PersoNet.Application;
using PersoNet.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

// Runs as a Windows service in production, outside IIS, so background jobs are never recycled (see docs/adr/0005).
builder.Services.AddWindowsService(options => options.ServiceName = "PersoNet Worker");

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var host = builder.Build();
await host.RunAsync().ConfigureAwait(false);
