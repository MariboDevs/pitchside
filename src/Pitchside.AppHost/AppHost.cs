var builder = DistributedApplication.CreateBuilder(args);

// The API: match actors, stats, moments, SignalR and the replay producer. Every provider defaults to its
// in-process local mode, so `dotnet run` needs nothing but .NET.
var api = builder.AddProject<Projects.Pitchside_Api>("api")
    .WithExternalHttpEndpoints();

// Storage:Provider=Cosmos runs the Cosmos DB Linux emulator in Docker and points the API at it. The emulator's
// connection string is injected at run time by Aspire and never written to a file.
if (string.Equals(builder.Configuration["Storage:Provider"], "Cosmos", StringComparison.OrdinalIgnoreCase))
{
    var cosmos = builder.AddAzureCosmosDB("cosmos").RunAsEmulator();
    api.WithReference(cosmos)
        .WaitFor(cosmos)
        .WithEnvironment("Storage__Provider", "Cosmos");
}

// The agent crew runs inside the API (Pitchside:RunAgentsInApi) until moments reach the separate agent host over
// the event bus; a host started here would read an empty in-process sink.

builder.Build().Run();
