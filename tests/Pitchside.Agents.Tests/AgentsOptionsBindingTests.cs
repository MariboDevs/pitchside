using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pitchside.Contracts;

namespace Pitchside.Agents.Tests;

public sealed class AgentsOptionsBindingTests
{
    private static AgentsOptions Bind(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        using var provider = new ServiceCollection().AddPitchsideAgents(configuration).BuildServiceProvider();
        return provider.GetRequiredService<IOptions<AgentsOptions>>().Value;
    }

    [Fact]
    public void Without_configuration_every_persona_and_language_is_written()
    {
        var options = Bind([]);
        options.Personas.ShouldBe(Personas.Viewer);
        options.Languages.ShouldBe(Languages.All);
    }

    [Fact]
    public void A_configured_list_replaces_the_defaults()
    {
        var options = Bind(new()
        {
            ["Agents:Personas:0"] = Personas.Casual,
            ["Agents:Languages:0"] = Languages.English,
        });
        options.Personas.ShouldBe([Personas.Casual]);
        options.Languages.ShouldBe([Languages.English]);
    }

    [Fact]
    public void An_empty_setting_writes_the_base_narrative_only()
    {
        var options = Bind(new() { ["Agents:Personas"] = "" });
        options.Personas.ShouldBeEmpty();
        options.Languages.ShouldBe(Languages.All);
    }
}
