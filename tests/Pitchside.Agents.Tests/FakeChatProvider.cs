using Pitchside.Agents.Providers;

namespace Pitchside.Agents.Tests;

/// <summary>A provider whose answers a test scripts per call, with an optional delay and a concurrency gauge.</summary>
internal sealed class FakeChatProvider(Func<ChatRequest, int, string> answer, TimeSpan? delay = null) : IChatProvider
{
    private readonly List<ChatRequest> _requests = [];
    private int _calls;
    private int _inFlight;
    private int _maxInFlight;

    public string Name => "fake";

    public IReadOnlyList<ChatRequest> Requests
    {
        get
        {
            lock (_requests)
            {
                return [.. _requests];
            }
        }
    }

    public int MaxInFlight => Volatile.Read(ref _maxInFlight);

    public async Task<ChatResult> CompleteAsync(ChatRequest request, CancellationToken cancellationToken)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }

        var call = Interlocked.Increment(ref _calls);
        var now = Interlocked.Increment(ref _inFlight);
        int seen;
        while (now > (seen = Volatile.Read(ref _maxInFlight)) && Interlocked.CompareExchange(ref _maxInFlight, now, seen) != seen)
        {
        }

        try
        {
            if (delay is { } wait)
            {
                await Task.Delay(wait, cancellationToken);
            }

            return new ChatResult(answer(request, call), Name, "fake-model", 10, 20);
        }
        finally
        {
            Interlocked.Decrement(ref _inFlight);
        }
    }
}
