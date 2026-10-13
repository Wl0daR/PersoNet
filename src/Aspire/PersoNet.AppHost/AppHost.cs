// Local development only. Production runs on IIS and Windows services (see docs/adr/0005).
// No containers: the database is a locally installed PostgreSQL, configured with
// dotnet user-secrets set ConnectionStrings:personet "<connection string>" --project src/Aspire/PersoNet.AppHost
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddConnectionString("personet");

var identity = builder.AddProject<Projects.PersoNet_Identity>("identity")
    .WithReference(database);

// The login server must be HTTPS (OpenID Connect); its address is passed to the API and the web app explicitly.
var identityUrl = identity.GetEndpoint("https");

var api = builder.AddProject<Projects.PersoNet_Api>("api")
    .WithReference(database)
    .WithEnvironment("Identity__Authority", identityUrl);

builder.AddProject<Projects.PersoNet_Worker>("worker")
    .WithReference(database);

builder.AddProject<Projects.PersoNet_Web>("web")
    .WithReference(api)
    .WithEnvironment("Identity__Authority", identityUrl)
    .WaitFor(api)
    .WaitFor(identity);

await builder.Build().RunAsync().ConfigureAwait(false);
