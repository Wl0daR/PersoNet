// Local development only. Production runs on IIS and Windows services (see docs/adr/0005).
// No containers: the database is a locally installed PostgreSQL, configured with
// dotnet user-secrets set ConnectionStrings:personet "<connection string>" --project src/Aspire/PersoNet.AppHost
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddConnectionString("personet");

var identity = builder.AddProject<Projects.PersoNet_Identity>("identity");

var api = builder.AddProject<Projects.PersoNet_Api>("api")
    .WithReference(database)
    .WithReference(identity);

builder.AddProject<Projects.PersoNet_Worker>("worker")
    .WithReference(database);

builder.AddProject<Projects.PersoNet_Web>("web")
    .WithReference(api)
    .WithReference(identity)
    .WaitFor(api);

await builder.Build().RunAsync().ConfigureAwait(false);
