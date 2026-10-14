var builder = DistributedApplication.CreateBuilder(args);

// The API is the first resource. Cosmos DB, Event Hubs, the agent host and the web overlay join it here,
// each one a line in this file and a row in docs/MICROSOFT-INTEGRATION.md.
builder.AddProject<Projects.Pitchside_Api>("api")
    .WithExternalHttpEndpoints();

builder.Build().Run();
