// OpenID Connect server (OpenIddict + ASP.NET Core Identity, Entra ID federation) is added next; see docs/adr/0004.
var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var app = builder.Build();

app.UseHttpsRedirection();

app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
