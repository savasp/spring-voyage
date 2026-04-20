// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.Tests;

using System.Net;
using System.Text;

/// <summary>
/// Minimal HTTP message handler stub for the REST-fallback path tests.
/// Records the most recent request so callers can assert headers.
/// </summary>
internal sealed class StubHttpHandler : HttpMessageHandler
{
    private readonly Dictionary<string, (HttpStatusCode, string)> _responses = new();

    public HttpRequestMessage? LastRequest { get; private set; }
    public int CallCount { get; private set; }

    public void Add(string host, HttpStatusCode status, string body) =>
        _responses[host] = (status, body);

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        CallCount++;
        LastRequest = request;

        var host = request.RequestUri?.Host ?? string.Empty;
        if (!_responses.TryGetValue(host, out var r))
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent($"no stub for {host}"),
            });
        }

        return Task.FromResult(new HttpResponseMessage(r.Item1)
        {
            Content = new StringContent(r.Item2, Encoding.UTF8, "application/json"),
        });
    }
}