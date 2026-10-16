#pragma warning disable OPENAI001
using Microsoft.Extensions.AI;
using OpenAI.Chat;
using Pitchside.Agents.Providers;

namespace Pitchside.Agents.Tests;

public sealed class FoundryChatOptionsBuilderTests
{
    [Fact]
    public void The_fast_tier_runs_without_reasoning_at_temperature_zero()
    {
        var chat = FoundryChatOptionsBuilder.Build(new FoundryOptions(), DeploymentTier.Fast, 400);

        chat.MaxOutputTokens.ShouldBe(400);
        chat.ResponseFormat.ShouldBe(Microsoft.Extensions.AI.ChatResponseFormat.Json);
        chat.Temperature.ShouldBe(0);
        Raw(chat).ReasoningEffortLevel.ShouldBe(new ChatReasoningEffortLevel("none"));
    }

    [Fact]
    public void The_quality_tier_reasons_at_low_effort_and_leaves_temperature_to_the_model()
    {
        var chat = FoundryChatOptionsBuilder.Build(new FoundryOptions(), DeploymentTier.Quality, 400);

        chat.Temperature.ShouldBeNull();
        Raw(chat).ReasoningEffortLevel.ShouldBe(new ChatReasoningEffortLevel("low"));
    }

    [Fact]
    public void An_empty_effort_sends_neither_effort_nor_temperature()
    {
        var options = new FoundryOptions { ReasoningEffort = new FoundryReasoningEffort { Fast = string.Empty } };

        var chat = FoundryChatOptionsBuilder.Build(options, DeploymentTier.Fast, 400);

        chat.Temperature.ShouldBeNull();
        chat.RawRepresentationFactory.ShouldBeNull();
    }

    [Fact]
    public void An_empty_effort_clears_a_callers_temperature()
    {
        var chat = new ChatOptions { Temperature = 0, MaxOutputTokens = 50 };

        FoundryChatOptionsBuilder.Apply(chat, string.Empty);

        chat.Temperature.ShouldBeNull();
        chat.MaxOutputTokens.ShouldBe(50);
        chat.RawRepresentationFactory.ShouldBeNull();
    }

    [Fact]
    public void A_reasoning_effort_clears_a_callers_temperature()
    {
        var chat = new ChatOptions { Temperature = 0 };

        FoundryChatOptionsBuilder.Apply(chat, "low");

        chat.Temperature.ShouldBeNull();
        Raw(chat).ReasoningEffortLevel.ShouldBe(new ChatReasoningEffortLevel("low"));
    }

    [Fact]
    public void A_configured_effort_is_sent_as_given()
    {
        var options = new FoundryOptions { ReasoningEffort = new FoundryReasoningEffort { Quality = "medium" } };

        var chat = FoundryChatOptionsBuilder.Build(options, DeploymentTier.Quality, 400);

        Raw(chat).ReasoningEffortLevel.ShouldBe(new ChatReasoningEffortLevel("medium"));
    }

    [Fact]
    public void Each_tier_has_its_own_deadline()
    {
        var options = new FoundryOptions { TimeoutSeconds = 4, QualityTimeoutSeconds = 12 };

        options.TimeoutFor(DeploymentTier.Fast).ShouldBe(TimeSpan.FromSeconds(4));
        options.TimeoutFor(DeploymentTier.Quality).ShouldBe(TimeSpan.FromSeconds(12));
    }

    private static ChatCompletionOptions Raw(ChatOptions chat) => chat.RawRepresentationFactory.ShouldNotBeNull()(null!).ShouldBeOfType<ChatCompletionOptions>();
}
