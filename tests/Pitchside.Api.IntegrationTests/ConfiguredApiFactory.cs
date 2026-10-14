namespace Pitchside.Api.IntegrationTests;

/// <summary>The API with settings a test chooses, such as a replay cap of one or live mode.</summary>
public sealed class ConfiguredApiFactory(IReadOnlyDictionary<string, string?> overrides) : PitchsideApiFactory
{
    protected override IReadOnlyDictionary<string, string?> Overrides { get; } = overrides;
}
