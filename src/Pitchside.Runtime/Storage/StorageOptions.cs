namespace Pitchside.Runtime.Storage;

/// <summary>The <c>Storage</c> configuration section.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";
    public const string InMemoryProvider = "InMemory";
    public const string CosmosProvider = "Cosmos";

    public string Provider { get; set; } = InMemoryProvider;

    public CosmosStorageOptions Cosmos { get; set; } = new();
}
