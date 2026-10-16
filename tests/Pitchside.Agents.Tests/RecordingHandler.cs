using System.Net;

namespace Pitchside.Agents.Tests;

/// <summary>An HTTP handler that records each request and its body and answers with a fixed status and body.</summary>
internal sealed class RecordingHandler(HttpStatusCode status, byte[] body) : HttpMessageHandler
{
    public List<(HttpRequestMessage Request, string Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var content = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        Requests.Add((request, content));
        return new HttpResponseMessage(status) { Content = new ByteArrayContent(body) };
    }
}
