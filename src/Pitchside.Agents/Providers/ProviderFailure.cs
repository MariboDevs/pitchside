using System.Text.Json;
using Azure.Identity;

namespace Pitchside.Agents.Providers;

/// <summary>
/// The failures a stage absorbs: an unreadable answer, a missing setting, a transport or service error, a credential
/// that could not produce a token, a call that ran past its deadline, or an aggregate made only of those. Each becomes
/// an objection or a skipped check, never a lost moment and never a crashed host.
/// </summary>
public static class ProviderFailure
{
    public static bool IsRecoverable(Exception exception) => exception switch
    {
        JsonException or FormatException or InvalidOperationException or HttpRequestException or TimeoutException => true,
        System.ClientModel.ClientResultException or AuthenticationFailedException => true,
        AggregateException aggregate => aggregate.InnerExceptions.Count > 0 && aggregate.InnerExceptions.All(IsRecoverable),
        _ => false,
    };
}
