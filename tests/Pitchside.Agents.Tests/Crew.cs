using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Pitchside.Agents.Checking;
using Pitchside.Agents.Personalisation;
using Pitchside.Agents.Prompts;
using Pitchside.Agents.Providers;
using Pitchside.Agents.Workflow;

namespace Pitchside.Agents.Tests;

/// <summary>Builds the crew's parts the way the host does, with a chosen provider.</summary>
internal static class Crew
{
    public static PromptCatalog Catalog { get; } = new();

    public static CodeFactChecker Checker { get; } = new(Catalog.ForbiddenHashes);

    public static MomentWorkflow Workflow(IChatProvider? provider = null, bool agentFactCheck = true) => new(
        provider ?? new ScriptedChatProvider(),
        Catalog,
        Checker,
        new BaseOnlyPersonaWriter(),
        new EnglishOnlyTranslator(),
        Options.Create(new AgentsOptions { AgentFactCheck = agentFactCheck }),
        TimeProvider.System,
        NullLogger<MomentWorkflow>.Instance);

    public static NarrativeVerifier Verifier(IChatProvider? provider = null, CodeFactChecker? checker = null) =>
        new(checker ?? Checker, provider ?? new ScriptedChatProvider(), Catalog, true);

    public static AgentPersonaWriter Personas(IChatProvider? provider = null, CodeFactChecker? checker = null, TimeProvider? time = null)
    {
        var chat = provider ?? new ScriptedChatProvider();
        return new(chat, Catalog, Verifier(chat, checker), Options.Create(new AgentsOptions()), time ?? TimeProvider.System);
    }

    public static AgentTranslator Translator(IChatProvider? provider = null, TimeProvider? time = null)
    {
        var chat = provider ?? new ScriptedChatProvider();
        return new(chat, Catalog, Verifier(chat), Options.Create(new AgentsOptions()), time ?? TimeProvider.System);
    }

    /// <summary>The whole crew with every persona and language, as the host registers it.</summary>
    public static MomentWorkflow FullWorkflow(IChatProvider? provider = null, TimeProvider? time = null)
    {
        var chat = provider ?? new ScriptedChatProvider();
        return new(
            chat,
            Catalog,
            Checker,
            Personas(chat, time: time),
            Translator(chat, time),
            Options.Create(new AgentsOptions()),
            time ?? TimeProvider.System,
            NullLogger<MomentWorkflow>.Instance);
    }
}
