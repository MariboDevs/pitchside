using System.Text.Json;

namespace Pitchside.Agents.Providers;

/// <summary>
/// The failures a stage absorbs: an unreadable answer, a missing setting, a transport or service error, or a call
/// that ran past <c>Foundry:TimeoutSeconds</c>. Each becomes an objection or a skipped check, never a lost moment.
/// </summary>
public static class ProviderFailure
{
    public static bool IsRecoverable(Exception exception) => exception is JsonException or FormatException
        or InvalidOperationException or HttpRequestException or TimeoutException or System.ClientModel.ClientResultException;
}
