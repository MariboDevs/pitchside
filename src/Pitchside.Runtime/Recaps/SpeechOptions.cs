namespace Pitchside.Runtime.Recaps;

/// <summary>
/// <c>Speech:*</c>: whether and how the full-time recap is spoken (WP-10). <c>None</c> turns speech off, <c>Scripted</c>
/// writes a silent clip with no network, <c>Azure</c> calls Azure AI Speech with the host's identity.
/// </summary>
public sealed class SpeechOptions
{
    public const string SectionName = "Speech";
    public const string NoneProvider = "None";
    public const string ScriptedProvider = "Scripted";
    public const string AzureProvider = "Azure";

    public string Provider { get; set; } = NoneProvider;

    /// <summary>The Speech resource's region, such as <c>southafricanorth</c>.</summary>
    public string? Region { get; set; }

    /// <summary>The Speech resource's ARM id, which an Entra ID token for the speech endpoint must name.</summary>
    public string? ResourceId { get; set; }

    /// <summary>
    /// The neural voice per language; a language without one is spoken with the English audio. The defaults are voices
    /// from the Speech voice gallery; check they are offered in the resource's region, and override any that are not.
    /// </summary>
    public Dictionary<string, string> Voices { get; set; } = new(StringComparer.Ordinal)
    {
        ["en"] = "en-ZA-LeahNeural",
        ["af"] = "af-ZA-AdriNeural",
        ["zu"] = "zu-ZA-ThandoNeural",
        ["es"] = "es-ES-ElviraNeural",
    };

    /// <summary>The blob service the audio is kept in, such as <c>https://account.blob.core.windows.net</c>; unset keeps it in memory.</summary>
    public Uri? BlobServiceUri { get; set; }

    public string BlobContainer { get; set; } = "recaps";

    public bool Enabled => !string.Equals(Provider, NoneProvider, StringComparison.OrdinalIgnoreCase);
}
