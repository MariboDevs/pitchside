namespace Pitchside.Runtime.Bus;

/// <summary><c>Bus:EventHubs</c>: namespace, hub and checkpoint container. Authentication is a token credential only.</summary>
public sealed class EventHubsBusOptions
{
    /// <summary>The fully qualified namespace, such as <c>pitchside.servicebus.windows.net</c>.</summary>
    public string? Namespace { get; set; }

    public string? HubName { get; set; }

    /// <summary>The blob container URI the processor checkpoints into.</summary>
    public Uri? CheckpointBlobUri { get; set; }
}
