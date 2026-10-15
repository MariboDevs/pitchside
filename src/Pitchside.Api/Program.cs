using Pitchside.Agents;
using Pitchside.Agents.Workflow;
using Pitchside.Api;
using Pitchside.Api.Broadcast;
using Pitchside.Api.Endpoints;
using Pitchside.Api.Hubs;
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
builder.Services.AddSingleton<GroundednessBatcher>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<GroundednessBatcher>());

var pitchside = builder.Configuration.GetSection(PitchsideOptions.SectionName).Get<PitchsideOptions>() ?? new PitchsideOptions();
if (pitchside.RunAgentsInApi)
{
    builder.Services.AddSingleton<INarrativePublisher, StoreAndBroadcastNarrativePublisher>();
    builder.Services.AddPitchsideAgents(builder.Configuration);
    builder.Services.AddMomentWorkflowRunner();
}

var app = builder.Build();
app.UseMiddleware<RequestIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();
app.MapDefaultEndpoints();
app.MapMatchEndpoints();
app.MapOverlayEndpoints();
app.MapAuditEndpoints();
if (string.Equals(pitchside.Mode, PitchsideOptions.ReplayMode, StringComparison.OrdinalIgnoreCase))
{
    app.MapReplayEndpoints();
}

app.MapHub<MatchHub>(MatchHub.Path);
app.Run();

/// <summary>The API's entry point, public so integration tests can host it.</summary>
public partial class Program;
