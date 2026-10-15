using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Pitchside.Api.Endpoints;
using Pitchside.Api.Middleware;
using Pitchside.Runtime.Usage;

namespace Pitchside.Api.IntegrationTests;

public sealed class UsageGuardMiddlewareTests
{
    [Fact]
    public async Task A_replay_that_started_keeps_its_use_even_when_its_response_then_fails()
    {
        await using var factory = new PitchsideApiFactory();
        var guard = new UsageGuard(Options.Create(new UsageGuardOptions { ReplaysPerHourPerIp = 1 }), TimeProvider.System, true);
        var middleware = new UsageGuardMiddleware(
            context =>
            {
                context.Items[ReplayEndpoints.AdmittedItem] = true;
                throw new IOException("The connection closed while the response was written.");
            },
            guard,
            factory.Services.GetRequiredService<IProblemDetailsService>());
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/api/v1/replays";

        await Should.ThrowAsync<IOException>(() => middleware.InvokeAsync(context));

        guard.TryTake(UsageLimits.Replays, ClientKey.Of(context)).Allowed.ShouldBeFalse();
    }
}
