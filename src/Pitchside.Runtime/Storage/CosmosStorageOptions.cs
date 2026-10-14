namespace Pitchside.Runtime.Storage;

/// <summary><c>Storage:Cosmos</c>: the account endpoint and database. Authentication is a token credential, never a key.</summary>
public sealed class CosmosStorageOptions
{
    public Uri? AccountEndpoint { get; set; }

    public string Database { get; set; } = "pitchside";
}
