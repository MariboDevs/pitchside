using Pitchside.Agents;
using Pitchside.Agents.Workflow;
using Pitchside.Api;
using Pitchside.Api.Broadcast;
using Pitchside.Api.Endpoints;
using Microsoft.AspNetCore.HttpOverrides;
using Pitchside.Api.Hubs;
using Pitchside.Api.Mcp;
using Pitchside.Api.Middleware;
using Pitchside.Contracts;
using Pitchside.Evaluation;
using Pitchside.Runtime;
using Pitchside.Runtime.Broadcast;
using Pitchside.ServiceDefaults;

var builder = WebApplication.CreateBuilder(args);
builder.AddServiceDefaults();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    foreach (var converter in ContractJson.Options.Converters)
    {
        options.SerializerOptions.Converters.Add(converter);
    }
});
builder.Services.AddProblemDetails();
builder.Services.Configure<PitchsideOptions>(builder.Configuration.GetSection(PitchsideOptions.SectionName));

builder.Services.AddSignalR().AddJsonProtocol(options =>
{
    foreach (var converter in ContractJson.Options.Converters)
    {
        options.PayloadSerializerOptions.Converters.Add(converter);
    }
});
builder.Services.AddSingleton<IMatchBroadcaster, SignalRMatchBroadcaster>();
builder.Services.AddSingleton<ViewerRegistry>();
builder.Services.AddSingleton<MatchSnapshots>();
builder.Services.AddSingleton<Pitchside.Overlay.OverlayBook>();
builder.Services.AddSingleton<OverlayCoordinator>();
builder.Services.AddPitchsideRuntime(builder.Configuration);
builder.Services.AddPitchsideEvaluation(builder.Configuration);
builder.Services.AddFoundryEvaluationJudge(builder.Configuration);
builder.Services.AddPitchsideMcp(builder.Configuration);
builder.Services.AddSingleton<GroundednessBatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GroundednessBatcher>());

var pitchside = builder.Configuration.GetSection(PitchsideOptions.SectionName).Get<PitchsideOptions>() ?? new PitchsideOptions();
if (pitchside.RunAgentsInApi)
{
    builder.Services.AddSingleton<INarrativePublisher, StoreAndBroadcastNarrativePublisher>();
    builder.Services.AddPitchsideAgents(builder.Configuration);
    builder.Services.AddMomentWorkflowRunner();
}

// Behind the Container Apps ingress the caller's address arrives in X-Forwarded-For. The ingress appends the address
// it saw, so only the last entry is trusted (ForwardLimit 1); the usage guard counts per-caller limits against it.
// The header is honoured only where that ingress is the one way in: Container Apps sets CONTAINER_APP_NAME, and
// UsageGuard:TrustForwardedFor overrides. Anywhere else a caller could write the header and pick its own address.
var trustForwardedFor = builder.Configuration.GetValue<bool?>("UsageGuard:TrustForwardedFor")
    ?? !string.IsNullOrEmpty(builder.Configuration["CONTAINER_APP_NAME"]);
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;
    options.ForwardLimit = 1;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
if (trustForwardedFor)
{
    app.UseForwardedHeaders();
}

app.UseMiddleware<RequestIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseWebSockets();
app.UseMiddleware<UsageGuardMiddleware>();
app.MapDefaultEndpoints();
app.MapMatchEndpoints();
app.MapOverlayEndpoints();
app.MapAuditEndpoints();
app.MapTraceEndpoints();
app.MapUsageEndpoints();
app.MapMcp(McpServerSetup.Path);
if (string.Equals(pitchside.Mode, PitchsideOptions.ReplayMode, StringComparison.OrdinalIgnoreCase))
{
    app.MapReplayEndpoints();
}

app.MapHub<MatchHub>(MatchHub.Path);
app.Run();

/// <summary>The API's entry point, public so integration tests can host it.</summary>
public partial class Program;
