namespace Pitchside.Runtime.Bus;

/// <summary>The <c>Bus</c> configuration section.</summary>
public sealed class BusOptions
{
    public const string SectionName = "Bus";
    public const string InProcessProvider = "InProcess";
    public const string EventHubsProvider = "EventHubs";

    /// <summary>The consumer group the match ingest reads.</summary>
    public const string IngestConsumerGroup = "ingest";

    public string Provider { get; set; } = InProcessProvider;

    public EventHubsBusOptions EventHubs { get; set; } = new();
}
