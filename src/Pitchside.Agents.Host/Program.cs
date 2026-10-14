using Pitchside.Agents;
using Pitchside.ServiceDefaults;

// The agent host runs the moment workflows as their own service, the pitchside-agents container in Azure. Its
// moment sink is the in-process queue until the runtime (WP-03) feeds it from the event bus.
var builder = Host.CreateApplicationBuilder(args);
builder.AddServiceDefaults();
builder.Services.AddPitchsideAgents(builder.Configuration);
builder.Services.AddMomentWorkflowRunner();

builder.Build().Run();
