using Microsoft.Extensions.AI;
using Pitchside.Agents.Providers;

namespace Pitchside.Agents.Tests;

public sealed class DeadlineChatClientTests
{
    [Fact]
    public async Task A_call_past_its_deadline_fails_as_a_timeout()
    {
        using var client = new DeadlineChatClient(new HangingChatClient(), TimeSpan.FromMilliseconds(100), "judge");

        var timeout = await Should.ThrowAsync<TimeoutException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "score")]));

        timeout.Message.ShouldContain("judge");
    }

    [Fact]
    public async Task The_callers_own_cancellation_stays_a_cancellation()
    {
        using var client = new DeadlineChatClient(new HangingChatClient(), TimeSpan.FromSeconds(30), "judge");
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Should.ThrowAsync<OperationCanceledException>(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "score")], cancellationToken: cancel.Token));
    }

    [Fact]
    public async Task Work_before_the_deadline_does_not_count_against_it()
    {
        var prepared = false;
        using var client = new DeadlineChatClient(new ReadyChatClient(), TimeSpan.FromMilliseconds(150), "judge", async ct =>
        {
            await Task.Delay(400, ct);
            prepared = true;
        });

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "score")]);

        prepared.ShouldBeTrue();
        response.Text.ShouldBe("ready");
    }
}
