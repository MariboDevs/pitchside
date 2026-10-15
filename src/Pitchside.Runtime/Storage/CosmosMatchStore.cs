using System.Globalization;
using System.Net;
using Microsoft.Azure.Cosmos;
using Pitchside.Contracts;

namespace Pitchside.Runtime.Storage;

/// <summary>
/// Azure Cosmos DB storage: one container per document type, partition key <c>/matchId</c>, serverless-compatible
/// (no throughput set). Point reads by id and partition key; every query filters on the partition key. Retries
/// are the SDK's defaults. In Azure the database and containers are provisioned by Bicep: Entra ID data-plane roles
/// cannot create them, so startup only checks that they exist. The key-authenticated local emulator creates them.
/// </summary>
public sealed class CosmosMatchStore(CosmosClient client, string databaseName) : IMatchStore
{
    public const string Matches = "matches";
    public const string Events = "events";
    public const string FactSheets = "factsheets";
    public const string Moments = "moments";
    public const string Narratives = "narratives";
    public const string Overlay = "overlay";
    public const string Audit = "audit";

    private static readonly string[] AllContainers = [Matches, Events, FactSheets, Moments, Narratives, Overlay, Audit];

    public const int MaxStatusAttempts = 10;

    /// <summary>
    /// Creates the database and every container if missing (idempotent), which the emulator allows. Under Entra ID the
    /// create is forbidden, so it falls back to checking that Bicep provisioned each one and fails clearly if not.
    /// </summary>
    public async Task EnsureCreatedAsync(CancellationToken cancellationToken)
    {
        try
        {
            var database = (await client.CreateDatabaseIfNotExistsAsync(databaseName, cancellationToken: cancellationToken).ConfigureAwait(false)).Database;
            foreach (var name in AllContainers)
            {
                await database.CreateContainerIfNotExistsAsync(new ContainerProperties(name, "/matchId"), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
        {
            await VerifyProvisionedAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<bool> TryCreateMatchAsync(StoredMatch match, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);
        try
        {
            await Container(Matches).CreateItemAsync(new CosmosItem<StoredMatch>(match.MatchId, match.MatchId, match.LastSeq, match), new PartitionKey(match.MatchId), cancellationToken: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict)
        {
            return false;
        }
    }

    public Task SaveMatchAsync(StoredMatch match, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(match);
        return Upsert(Matches, new CosmosItem<StoredMatch>(match.MatchId, match.MatchId, match.LastSeq, match), cancellationToken);
    }

    public async Task<StoredMatch?> GetMatchAsync(string matchId, CancellationToken cancellationToken) =>
        (await Read<StoredMatch>(Matches, matchId, matchId, cancellationToken).ConfigureAwait(false))?.Payload;

    public async Task<IReadOnlyList<StoredMatch>> ListMatchesAsync(CancellationToken cancellationToken)
    {
        // The one cross-partition read: the match list is small, one item per match.
        var items = await Query<StoredMatch>(Matches, new QueryDefinition("SELECT * FROM c"), null, cancellationToken).ConfigureAwait(false);
        return [.. items.Select(i => i.Payload).OrderBy(m => m.CreatedUtc).ThenBy(m => m.MatchId, StringComparer.Ordinal)];
    }

    /// <summary>
    /// A read and a replace conditional on the read's ETag, repeated on a conflicting write, so the terminal-status
    /// and highest-seq rules apply to the current document rather than to a stale copy.
    /// </summary>
    public async Task UpdateMatchStatusAsync(string matchId, MatchStatus status, int lastSeq, CancellationToken cancellationToken)
    {
        var container = Container(Matches);
        for (var attempt = 1; ; attempt++)
        {
            ItemResponse<CosmosItem<StoredMatch>> read;
            try
            {
                read = await container.ReadItemAsync<CosmosItem<StoredMatch>>(matchId, new PartitionKey(matchId), cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new KeyNotFoundException($"No match '{matchId}'.", ex);
            }

            var current = read.Resource.Payload;
            var next = current with { Status = MatchStatusTransition.Resolve(current.Status, status), LastSeq = Math.Max(current.LastSeq, lastSeq) };
            try
            {
                await container.ReplaceItemAsync(
                    new CosmosItem<StoredMatch>(matchId, matchId, next.LastSeq, next),
                    matchId,
                    new PartitionKey(matchId),
                    new ItemRequestOptions { IfMatchEtag = read.ETag },
                    cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.PreconditionFailed && attempt < MaxStatusAttempts)
            {
                // Another write landed between the read and the replace; read again and reapply the rules.
                continue;
            }
        }
    }

    public Task AppendEventAsync(MatchEvent matchEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(matchEvent);
        return Upsert(Events, new CosmosItem<MatchEvent>(matchEvent.Id, matchEvent.MatchId, matchEvent.Seq, matchEvent), cancellationToken);
    }

    public async Task<IReadOnlyList<MatchEvent>> GetEventsAsync(string matchId, int fromSeq, int toSeq, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.matchId = @matchId AND c.seq >= @from AND c.seq <= @to ORDER BY c.seq")
            .WithParameter("@matchId", matchId).WithParameter("@from", fromSeq).WithParameter("@to", toSeq);
        return [.. (await Query<MatchEvent>(Events, query, matchId, cancellationToken).ConfigureAwait(false)).Select(i => i.Payload)];
    }

    public Task SaveFactSheetAsync(FactSheet sheet, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sheet);
        var id = string.Create(CultureInfo.InvariantCulture, $"{sheet.MatchId}-{sheet.AsOfSeq:00000}");
        return Upsert(FactSheets, new CosmosItem<FactSheet>(id, sheet.MatchId, sheet.AsOfSeq, sheet), cancellationToken);
    }

    public async Task<FactSheet?> GetFactSheetAsync(string matchId, int atSeq, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT TOP 1 * FROM c WHERE c.matchId = @matchId AND c.seq <= @at ORDER BY c.seq DESC")
            .WithParameter("@matchId", matchId).WithParameter("@at", atSeq);
        return (await Query<FactSheet>(FactSheets, query, matchId, cancellationToken).ConfigureAwait(false)).FirstOrDefault()?.Payload;
    }

    public Task SaveMomentAsync(Moment moment, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(moment);
        return Upsert(Moments, new CosmosItem<Moment>(moment.Id, moment.MatchId, moment.TriggerSeq, moment), cancellationToken);
    }

    public async Task<IReadOnlyList<Moment>> GetMomentsAsync(string matchId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.matchId = @matchId").WithParameter("@matchId", matchId);
        var items = await Query<Moment>(Moments, query, matchId, cancellationToken).ConfigureAwait(false);
        return [.. items.Select(i => i.Payload).OrderBy(m => m.TriggerSeq).ThenByDescending(m => m.Priority)];
    }

    public Task SaveNarrativeAsync(Narrative narrative, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(narrative);
        return Upsert(Narratives, new CosmosItem<Narrative>(narrative.Id, narrative.MatchId, narrative.FactSheetSeq, narrative), cancellationToken);
    }

    public async Task<IReadOnlyList<Narrative>> GetNarrativesAsync(string matchId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.matchId = @matchId").WithParameter("@matchId", matchId);
        var items = await Query<Narrative>(Narratives, query, matchId, cancellationToken).ConfigureAwait(false);
        return [.. items.Select(i => i.Payload).OrderBy(n => n.FactSheetSeq).ThenBy(n => n.Id, StringComparer.Ordinal)];
    }

    public Task SaveOverlayEntryAsync(OverlayEntry entry, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(entry);
        return Upsert(Overlay, new CosmosItem<OverlayEntry>(entry.Id, entry.MatchId, entry.InAtMs, entry), cancellationToken);
    }

    public async Task<IReadOnlyList<OverlayEntry>> GetOverlayEntriesAsync(string matchId, CancellationToken cancellationToken)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.matchId = @matchId").WithParameter("@matchId", matchId);
        var items = await Query<OverlayEntry>(Overlay, query, matchId, cancellationToken).ConfigureAwait(false);
        return [.. items.Select(i => i.Payload).OrderBy(e => e.Period).ThenBy(e => e.InAtMs)];
    }

    private Container Container(string name) => client.GetContainer(databaseName, name);

    private async Task VerifyProvisionedAsync(CancellationToken cancellationToken)
    {
        foreach (var name in AllContainers)
        {
            try
            {
                await Container(name).ReadContainerAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
            }
            catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                throw new InvalidOperationException(
                    $"Cosmos DB container '{databaseName}/{name}' does not exist. In Azure the containers are provisioned by the Bicep templates; Entra ID cannot create them.", ex);
            }
        }
    }

    private Task Upsert<T>(string container, CosmosItem<T> item, CancellationToken cancellationToken) =>
        Container(container).UpsertItemAsync(item, new PartitionKey(item.MatchId), cancellationToken: cancellationToken);

    private async Task<CosmosItem<T>?> Read<T>(string container, string id, string matchId, CancellationToken cancellationToken)
    {
        try
        {
            return (await Container(container).ReadItemAsync<CosmosItem<T>>(id, new PartitionKey(matchId), cancellationToken: cancellationToken).ConfigureAwait(false)).Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    private async Task<List<CosmosItem<T>>> Query<T>(string container, QueryDefinition query, string? matchId, CancellationToken cancellationToken)
    {
        var options = matchId is null ? new QueryRequestOptions() : new QueryRequestOptions { PartitionKey = new PartitionKey(matchId) };
        var results = new List<CosmosItem<T>>();
        using var iterator = Container(container).GetItemQueryIterator<CosmosItem<T>>(query, requestOptions: options);
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync(cancellationToken).ConfigureAwait(false));
        }

        return results;
    }
}
