// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Execution;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Startup probe for the Ollama server. Issues a single <c>GET /api/tags</c> request
/// (Ollama's cheap native health endpoint) during host startup and logs the outcome.
/// </summary>
/// <remarks>
/// Behaviour is controlled by <see cref="OllamaOptions.RequireHealthyAtStartup"/>:
/// <list type="bullet">
///   <item><c>false</c> (dev default): a failed probe logs a warning and the host keeps
///   starting. Provider calls will fail until Ollama is reachable.</item>
///   <item><c>true</c> (production): a failed probe throws and aborts host startup so
///   operators notice misconfiguration immediately.</item>
/// </list>
/// The probe is best-effort by design — Ollama may take a moment to start after the
/// platform host, especially when the container pulls a model on first run. Downstream
/// provider calls handle the transient case on their own (the <see cref="OllamaProvider"/>
/// surfaces connection failures as <see cref="Cvoya.Spring.Core.SpringException"/>).
/// </remarks>
public class OllamaHealthCheck(
    HttpClient httpClient,
    IOptions<OllamaOptions> options,
    ILogger<OllamaHealthCheck> logger) : IHostedService
{
    private readonly OllamaOptions _options = options.Value;

    /// <inheritdoc />
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/api/tags";

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_options.HealthCheckTimeoutSeconds));

        try
        {
            using var response = await httpClient.GetAsync(endpoint, cts.Token);
            if (response.IsSuccessStatusCode)
            {
                logger.LogInformation(
                    "Ollama health check succeeded at {Endpoint} (default model: {Model}).",
                    endpoint, _options.DefaultModel);
                return;
            }

            HandleFailure($"Ollama health check returned {(int)response.StatusCode} {response.StatusCode}.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            HandleFailure($"Ollama health check timed out after {_options.HealthCheckTimeoutSeconds}s against {endpoint}.");
        }
        catch (HttpRequestException ex)
        {
            HandleFailure($"Ollama health check could not reach {endpoint}: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void HandleFailure(string message)
    {
        if (_options.RequireHealthyAtStartup)
        {
            logger.LogError("{Message}", message);
            throw new InvalidOperationException(
                message + " LanguageModel:Ollama:RequireHealthyAtStartup is true — aborting startup.");
        }

        logger.LogWarning("{Message} Continuing startup; provider calls will fail until Ollama is reachable.", message);
    }
}