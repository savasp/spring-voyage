// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Execution;

using System.Text.Json;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Messaging;

using Microsoft.Extensions.Logging;

/// <summary>
/// Dispatches agent work to an external agent-tool container. The container
/// (e.g., a configured Claude Code or Codex image) receives the assembled
/// system prompt via the <c>SPRING_SYSTEM_PROMPT</c> environment variable and
/// runs its own agent loop; Spring Voyage does not implement one.
/// </summary>
public class DelegatedExecutionDispatcher(
    IContainerRuntime containerRuntime,
    IPromptAssembler promptAssembler,
    ILoggerFactory loggerFactory) : IExecutionDispatcher
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<DelegatedExecutionDispatcher>();

    /// <inheritdoc />
    public async Task<Message?> DispatchAsync(
        Message message,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Dispatching execution for message {MessageId} to {Destination}",
            message.Id, message.To);

        var prompt = await promptAssembler.AssembleAsync(message, cancellationToken);

        var envVars = new Dictionary<string, string>
        {
            ["SPRING_SYSTEM_PROMPT"] = prompt
        };

        var config = new ContainerConfig(
            Image: message.To.Path,
            EnvironmentVariables: envVars);

        string? containerName = null;

        // Register cancellation callback to stop the container if the token fires.
        await using var cancellationRegistration = cancellationToken.Register(() =>
        {
            if (containerName is not null)
            {
                _logger.LogWarning("Cancellation requested, stopping container {ContainerName}", containerName);
                _ = containerRuntime.StopAsync(containerName, CancellationToken.None);
            }
        });

        var result = await containerRuntime.RunAsync(config, cancellationToken);
        containerName = result.ContainerId;

        _logger.LogInformation(
            "Container {ContainerId} completed with exit code {ExitCode}",
            result.ContainerId, result.ExitCode);

        return BuildResponseMessage(message, result);
    }

    private static Message BuildResponseMessage(Message originalMessage, ContainerResult result)
    {
        var payload = result.ExitCode == 0
            ? JsonSerializer.SerializeToElement(new
            {
                Output = result.StandardOutput,
                ExitCode = result.ExitCode
            })
            : JsonSerializer.SerializeToElement(new
            {
                Error = result.StandardError,
                Output = result.StandardOutput,
                ExitCode = result.ExitCode
            });

        return new Message(
            Id: Guid.NewGuid(),
            From: originalMessage.To,
            To: originalMessage.From,
            Type: MessageType.Domain,
            ConversationId: originalMessage.ConversationId,
            Payload: payload,
            Timestamp: DateTimeOffset.UtcNow);
    }
}