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
            // Limits never apply to the scripted provider, the agents' default, so tests and local runs are not capped.
            var scripted = string.Equals(configuration["Agents:Provider"] ?? "Scripted", "Scripted", StringComparison.OrdinalIgnoreCase);
            return new UsageGuard(options, sp.GetRequiredService<TimeProvider>(), options.Value.Enabled ?? !scripted);
        });
        return services;
    }
}
