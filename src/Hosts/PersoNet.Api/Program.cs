using PersoNet.Api.Tenancy;
using PersoNet.Application;
using PersoNet.Application.Abstractions;
using PersoNet.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ITenantContext, HttpTenantContext>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    // Deployments run migrations as a separate step (see docs/adr/0005).
    await app.Services.MigrateDatabaseAsync().ConfigureAwait(false);

    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

// Module endpoints are mapped under this group (see "Konwencje API" in the plan).
app.MapGroup("/api/v1");

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Entry point marker used by integration tests.
/// </summary>
public partial class Program;
