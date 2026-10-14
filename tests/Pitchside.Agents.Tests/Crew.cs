using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;

namespace Pitchside.Agents.Tests;

/// <summary>Builds the crew's parts the way the host does, with a chosen provider.</summary>
internal static class Crew
{
    public static PromptCatalog Catalog { get; } = new();

    public static CodeFactChecker Checker { get; } = new(Catalog.ForbiddenWords);

    public static MomentWorkflow Workflow(IChatProvider? provider = null, bool agentFactCheck = true) => new(
        provider ?? new ScriptedChatProvider(),
        Catalog,
        Checker,
        new BaseOnlyPersonaWriter(),
        new EnglishOnlyTranslator(),
        Options.Create(new AgentsOptions { AgentFactCheck = agentFactCheck }),
        TimeProvider.System,
        NullLogger<MomentWorkflow>.Instance);
}
