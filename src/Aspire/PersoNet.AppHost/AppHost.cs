// Local development only. Production runs on IIS and Windows services (see docs/adr/0005).
var builder = DistributedApplication.CreateBuilder(args);

var database = builder.AddPostgres("postgres")
    .WithDataVolume()
    .AddDatabase("personet");

var identity = builder.AddProject<Projects.PersoNet_Identity>("identity");

var api = builder.AddProject<Projects.PersoNet_Api>("api")
    .WithReference(database)
    .WithReference(identity)
    .WaitFor(database);

builder.AddProject<Projects.PersoNet_Worker>("worker")
    .WithReference(database)
    .WaitFor(database);

builder.AddProject<Projects.PersoNet_Web>("web")
    .WithReference(api)
    .WithReference(identity)
    .WaitFor(api);

await builder.Build().RunAsync().ConfigureAwait(false);
