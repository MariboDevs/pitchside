using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Pitchside.Runtime.Usage;

/// <summary>Registers the one <see cref="UsageGuard"/> of a host, from <c>UsageGuard:*</c>.</summary>
public static class UsageServiceCollectionExtensions
{
    public static IServiceCollection AddUsageGuard(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.Configure<UsageGuardOptions>(configuration.GetSection(UsageGuardOptions.SectionName));
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<UsageGuardOptions>>();
            return new UsageGuard(options, sp.GetRequiredService<TimeProvider>(), options.Value.Enabled ?? EnabledByDefault(configuration));
        });
        return services;
    }

    /// <summary>
    /// Whether the limits apply when <c>UsageGuard:Enabled</c> is unset: whenever something paid is called. A host that
    /// is scripted throughout (the agents' and the speech's defaults) is not capped, so tests and local runs are not;
    /// Foundry agents or Azure speech, either one, turn the limits on.
    /// </summary>
    public static bool EnabledByDefault(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var scriptedAgents = string.Equals(configuration["Agents:Provider"] ?? "Scripted", "Scripted", StringComparison.OrdinalIgnoreCase);
        var azureSpeech = string.Equals(configuration["Speech:Provider"], "Azure", StringComparison.OrdinalIgnoreCase);
        return !scriptedAgents || azureSpeech;
    }
}
