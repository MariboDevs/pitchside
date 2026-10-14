# WP-03: Live runtime

| | |
| --- | --- |
| Branch | `feat/wp03-runtime` |
| Depends on | WP-02 |
| Owner | Sipho (implemented by Claude Code) |
| Effort | 3 days |
| Changes contracts | adds SignalR hub contract, storage documents, bus envelope |

## Goal

Make a match live: events flow from a producer, through a bus, into exactly one actor per match that holds the
state, runs the stats and moment stages, persists, and pushes to clients in real time. Everything runs locally
in process with no cloud, and the same code runs against Azure Event Hubs and Cosmos DB when configured.

## Topology

```
Producer (Replay or Live) --> IEventBus --> MatchIngestConsumer --> MatchActor (one per match, Akka.NET)
                                                                      |-- IMatchStatsEngine (WP-01)
                                                                      |-- IMomentDetector (WP-02)
                                                                      |-- IMatchStore (InMemory | Cosmos)
                                                                      |-- IMatchBroadcaster (SignalR groups)
                                                                      |-- IMomentSink (queue read by WP-04)
```

## Components

### Akka.NET

- Package `Akka.Hosting` plus `Akka` (pin versions in `Directory.Packages.props`; check current stable).
- `MatchSupervisorActor` creates and supervises one `MatchActor` per `matchId`, by name `match-{matchId}`.
  Supervision: `OneForOneStrategy`, restart on exception up to 5 times in 1 minute, then stop and log Error.
- `MatchActor` is a `ReceivePersistentActor`? No. Persistence is through `IMatchStore`, not Akka.Persistence,
  to keep one storage model. On restart the actor rebuilds state by replaying stored events from `IMatchStore`.
- Messages, one type per file under `Messages/`: `IngestEvent(MatchEvent)`, `GetState`, `GetFactSheet`,
  `GetMoments`, `Subscribe(connectionId)`, `Unsubscribe`, `StartReplay(seed, speed)`, `StopReplay`.
- Idempotency: the actor ignores an `IngestEvent` whose `seq` is not exactly `lastSeq + 1`; an older seq is
  dropped and counted; a gap raises a `GapDetected` log at Warning and requests a resend from the store.
- The actor is the only writer. Reads go through `GetState` snapshots, never shared mutable state.
- Writes and pushes go through a per-match `MatchOutbox` owned by the supervisor, so they survive an actor
  restart: writes land one at a time in issue order and are retried until they succeed, so the stored events are
  always a gapless prefix, and a restarted actor waits for earlier writes before it rebuilds. Pushes stay in order
  across a restart. After a rebuild the actor rewrites what the stored events imply (durable watermark, status,
  fact sheets, moments) as idempotent upserts without broadcasting, and hands moments that have no narrative back
  to the crew.
- Lifecycle: `SetLifecycle(matchId, status)` carries the producer's pause, resume and stop to the actor, which
  persists and broadcasts the status. `Completed` and `Stopped` are terminal in the actor and in every store.
- Match ids are plain tokens (`[A-Za-z0-9_-]{1,64}`); the supervisor refuses anything else, and unsubscribing or a
  lifecycle change never creates a match actor.

### Event bus (`IEventBus`)

```
Task PublishAsync(EventEnvelope envelope, CancellationToken ct)
IAsyncEnumerable<EventEnvelope> SubscribeAsync(string consumerGroup, CancellationToken ct)
```

`EventEnvelope { string MatchId; int Seq; string Type; string PayloadJson; DateTimeOffset PublishedUtc }`.
Partition key is `MatchId` so one match's events are ordered.

- `InProcessEventBus`: `System.Threading.Channels`, bounded 10,000, one channel per consumer group.
- `EventHubsEventBus`: `Azure.Messaging.EventHubs` producer and `EventProcessorClient` with Blob checkpoints;
  partition key `MatchId`. Auth by `DefaultAzureCredential` only; a connection string is never read.

### Storage (`IMatchStore`)

Documents, each its own C# record and each its own Cosmos container, partition key `/matchId`:

| Container | Document | Id |
| --- | --- | --- |
| `matches` | `MatchDocument` plus `status`, `lastSeq`, `createdUtc` | `matchId` |
| `events` | `MatchEvent` | `{matchId}-{seq:00000}` |
| `factsheets` | `FactSheet` snapshots at every moment's `factSheetSeq` and every 60 s of clock | `{matchId}-{asOfSeq:00000}` |
| `moments` | `Moment` | moment id |
| `narratives` | written by WP-04 | narrative id |
| `overlay` | written by WP-05 | entry id |
| `audit` | written by WP-07 | ulid |

- `InMemoryMatchStore`: dictionaries, used by tests and the default local mode.
- `CosmosMatchStore`: `Microsoft.Azure.Cosmos` with `DefaultAzureCredential`. In Azure the database and
  containers are provisioned by Bicep (WP-09), because Entra ID data-plane roles cannot create them; startup checks
  they exist and fails with a clear message if not. The key-authenticated local emulator creates them on startup
  (idempotent). Serverless-compatible (no throughput set). Point reads by id and partition key; queries only with
  the partition key in the filter. Status updates are ETag-conditional replaces. Retries left to the SDK's defaults.
- Local Cosmos: the Aspire AppHost runs the Cosmos DB Linux emulator through
  `Aspire.Hosting.Azure.CosmosDB` `RunAsEmulator()`, so `Storage:Provider=Cosmos` works on a laptop with
  Docker. Tests never need it; `InMemory` is the test provider.

### Replay producer

`ReplayService` (hosted service): on `StartReplay(seed, speed)`, simulates the seed with
`MatchSimulator.Simulate`, stores the document, and publishes events to the bus with inter-event delays of
`(next.WallTimeUtc - current.WallTimeUtc) / speed`; `speed = 0` means no delay. A replay can be paused,
resumed and stopped. At most 3 concurrent replays per host (configurable), which is the first usage guard.

### SignalR (`MatchHub` at `/hubs/match`)

Client to server:

| Method | Args | Effect |
| --- | --- | --- |
| `JoinMatch` | `matchId` | adds the connection to group `match:{id}`; replies with `MatchSnapshot` |
| `LeaveMatch` | `matchId` | |
| `SetViewer` | `matchId`, `ViewerPreferences { persona, language, clubId?, playerId? }` | stored per connection; WP-06 delivers each connection only the narratives its preferences match; persona `all` is the control room and receives every variant |

Server to client (group `match:{id}`):

| Message | Payload | When |
| --- | --- | --- |
| `Event` | `MatchEvent` | every ingested event |
| `FactSheet` | `FactSheet` | every 5 events or on a moment, whichever first |
| `Moment` | `Moment` | each detected moment |
| `Narrative` | WP-04 contract | each verified narrative, to the connections whose viewer preferences it matches (WP-06) |
| `OverlayEntry` | WP-05 contract | each scheduled entry |
| `MatchStatus` | `{ matchId, status, lastSeq, period, clockMs }` | state changes and every 10 s |

`MatchSnapshot` = document, latest fact sheet, all moments, all narratives for the connection's viewer
preferences, all overlay entries. A client that joins late is complete after one message.

Messages use `ContractJson.Options` (camelCase, snake_case enums).

### API additions (`/api/v1`)

| Route | Method | Purpose |
| --- | --- | --- |
| `/matches` | GET | list stored matches with status |
| `/matches/{matchId}` | GET | document plus status |
| `/matches/{matchId}/events?fromSeq=&toSeq=` | GET | page of events |
| `/matches/{matchId}/facts?atSeq=` | GET | fact sheet, latest if omitted |
| `/matches/{matchId}/moments` | GET | |
| `/replays` | POST `{ seed, speed }` | starts a replay; 429 when the cap is reached |
| `/replays/{matchId}` | DELETE | stops it |
| `/replays/{matchId}/pause`, `/resume` | POST | |

All responses `TypedResults`; errors as RFC 7807 problem details; every request carries an `X-Request-ID`
(generated if absent) that is logged and returned.

## Configuration

| Key | Default | Notes |
| --- | --- | --- |
| `Pitchside:Mode` | `Replay` | `Live` disables the replay endpoints |
| `Replay:MaxConcurrent` | `3` | |
| `Replay:DefaultSpeed` | `1.0` | |
| `Bus:Provider` | `InProcess` | `EventHubs` requires `Bus:EventHubs:Namespace`, `Bus:EventHubs:HubName`, `Bus:EventHubs:CheckpointBlobUri` |
| `Storage:Provider` | `InMemory` | `Cosmos` requires `Storage:Cosmos:AccountEndpoint`, `Storage:Cosmos:Database` |
| `Akka:ClusterMode` | `Local` | reserved; single node for the hackathon, see notes |

## Files

- `src/Pitchside.Runtime/` (class library): `Actors/`, `Messages/`, `Bus/`, `Storage/`, `Replay/`,
  `Broadcast/IMatchBroadcaster.cs`, `MomentSink/IMomentSink.cs` and `ChannelMomentSink.cs`
- `src/Pitchside.Api/`: `Hubs/MatchHub.cs`, `Endpoints/MatchEndpoints.cs` (move and extend the existing),
  `Endpoints/ReplayEndpoints.cs`, `Broadcast/SignalRMatchBroadcaster.cs`, `Program.cs` wiring with
  `Akka.Hosting`
- `src/Pitchside.AppHost/AppHost.cs`: add the Cosmos emulator and Event Hubs emulator resources
  (`Aspire.Hosting.Azure.CosmosDB`, `Aspire.Hosting.Azure.EventHubs`, both with `RunAsEmulator()`), wired to
  the API only when the matching provider is selected via parameters.
- `tests/Pitchside.Runtime.Tests/`: `MatchActorTests` (Akka.TestKit: ordering, idempotency, gap handling,
  restart rebuilds from store), `InProcessEventBusTests`, `InMemoryMatchStoreTests`, `ReplayServiceTests`
  (speed 0 completes a sample match in under 5 s and the store holds every event)
- `tests/Pitchside.Api.IntegrationTests/`: `WebApplicationFactory` tests for every route and a SignalR client
  test that joins a replay and receives `Event`, `FactSheet` and `Moment` messages in order

## Done when

```bash
dotnet build Pitchside.slnx
dotnet test Pitchside.slnx
dotnet run --project src/Pitchside.Api &
curl -s -X POST localhost:5180/api/v1/replays -H 'content-type: application/json' -d '{"seed":7,"speed":0}'
sleep 5
curl -s localhost:5180/api/v1/matches/MER-7/moments | python3 -c "import json,sys; print(len(json.load(sys.stdin)), 'moments')"
```

## Notes for the implementer

- Akka.NET cluster sharding across Container Apps replicas is out of scope for the hackathon: the API runs one
  replica, documented in WP-09, the same decision LedgerCore made for discovery reasons. Design the actor
  messages so sharding can be added without changing them.
- The Cosmos emulator needs Docker and about 3 GB; keep `InMemory` as the default so `dotnet run` works
  everywhere.
- Never block an actor on I/O: persist with `PipeTo` and handle the completion message.
- Verify the current `Aspire.Hosting.Azure.EventHubs` emulator support before relying on it; if it is not
  available, the in-process bus remains the local default and Event Hubs is exercised only in the deployed
  environment.
