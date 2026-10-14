var builder = DistributedApplication.CreateBuilder(args);

// The API is the first resource. Cosmos DB, Event Hubs, the agent host and the web overlay join it here,
// each one a line in this file and a row in docs/MICROSOFT-INTEGRATION.md.
builder.AddProject<Projects.Pitchside_Api>("api")
    .WithExternalHttpEndpoints();

// The agent crew. Agents:Provider defaults to Scripted, so it runs offline; set it to Foundry with the
// Foundry:Endpoint and deployment names in user-secrets to call the deployed models.
builder.AddProject<Projects.Pitchside_Agents_Host>("agents");

builder.Build().Run();
