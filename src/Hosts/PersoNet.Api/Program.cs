using PersoNet.Application;
using PersoNet.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi("v1");
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
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
