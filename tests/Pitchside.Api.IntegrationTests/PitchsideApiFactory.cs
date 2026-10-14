using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Pitchside.Api.IntegrationTests;

/// <summary>
/// The API in process with every provider local. Settings go in through <c>UseSetting</c>, which the minimal-hosting
/// <c>Program</c> sees before it registers services.
/// </summary>
public class PitchsideApiFactory : WebApplicationFactory<Program>
{
    protected virtual IReadOnlyDictionary<string, string?> Overrides { get; } = new Dictionary<string, string?>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseEnvironment("Testing");
        var settings = new Dictionary<string, string?>
        {
            ["Storage:Provider"] = "InMemory",
            ["Bus:Provider"] = "InProcess",
            ["Agents:Provider"] = "Scripted",
        };
        foreach (var (key, value) in Overrides)
        {
            settings[key] = value;
        }

        foreach (var (key, value) in settings)
        {
            builder.UseSetting(key, value);
        }
    }
}
