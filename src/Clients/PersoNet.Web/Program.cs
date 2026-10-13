using PersoNet.ApiClient;
using PersoNet.Web.Authentication;
using PersoNet.Web.Components;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddPersoNetAuthentication();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// "https+http://api" is resolved by service discovery locally; on IIS the real address comes from configuration.
var apiBaseAddress = builder.Configuration["PersoNetApi:BaseAddress"] ?? "https+http://api";
builder.Services.AddPersoNetApiClient(new Uri(apiBaseAddress));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapAuthenticationEndpoints();
app.MapDefaultEndpoints();

await app.RunAsync().ConfigureAwait(false);
