using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pitchside.Runtime.Usage;

namespace Pitchside.Runtime.Tests;

public sealed class UsageGuardDefaultsTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("Scripted", "Scripted", false)]
    [InlineData("Foundry", null, true)]
    [InlineData(null, "Azure", true)]
    [InlineData("Scripted", "azure", true)]
    public void The_limits_apply_by_default_whenever_a_paid_service_is_called(string? agents, string? speech, bool enabled) =>
        UsageServiceCollectionExtensions.EnabledByDefault(Configuration(agents, speech)).ShouldBe(enabled);

    [Fact]
    public void An_explicit_setting_wins_over_the_default()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Speech:Provider"] = "Azure",
            ["UsageGuard:Enabled"] = "false",
        }).Build();

        using var services = new ServiceCollection().AddUsageGuard(configuration).BuildServiceProvider();

        services.GetRequiredService<UsageGuard>().Enabled.ShouldBeFalse();
    }

    private static IConfiguration Configuration(string? agents, string? speech) =>
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Agents:Provider"] = agents,
            ["Speech:Provider"] = speech,
        }).Build();
}
