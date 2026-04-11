// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Execution;

using System.Text;
using System.Text.Json;

using Cvoya.Spring.Core;
using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Dapr.Observability;

using Microsoft.Extensions.Logging;

/// <summary>
/// Execution dispatcher that handles hosted mode by assembling a prompt
/// and calling an AI provider in-process. Supports both non-streaming and
/// streaming execution paths.
/// </summary>
public class HostedExecutionDispatcher(
    IAiProvider aiProvider,
    IPromptAssembler promptAssembler,
    StreamEventPublisher? streamEventPublisher,
    ILoggerFactory loggerFactory) : IExecutionDispatcher
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<HostedExecutionDispatcher>();

    /// <summary>
    /// Initializes a new instance without a stream event publisher (non-streaming only).
    /// </summary>
    public HostedExecutionDispatcher(
        IAiProvider aiProvider,
        IPromptAssembler promptAssembler,
        ILoggerFactory loggerFactory)
        : this(aiProvider, promptAssembler, null, loggerFactory)
    {
    }

    /// <inheritdoc />
    public async Task<Message?> DispatchAsync(Message message, ExecutionMode mode, CancellationToken cancellationToken = default)
    {
        if (mode != ExecutionMode.Hosted)
        {
            throw new SpringException($"HostedExecutionDispatcher only handles Hosted mode, got {mode}");
        }

        _logger.LogDebug("Assembling prompt for message {MessageId}.", message.Id);
        var prompt = await promptAssembler.AssembleAsync(message, cancellationToken);

        // Use streaming path when a publisher is available.
        if (streamEventPublisher is not null)
        {
            return await DispatchStreamingAsync(message, prompt, cancellationToken);
        }

        _logger.LogDebug("Sending prompt to AI provider for message {MessageId}.", message.Id);
        var response = await aiProvider.CompleteAsync(prompt, cancellationToken);

        _logger.LogDebug("Received AI response for message {MessageId}.", message.Id);

        var responsePayload = JsonSerializer.SerializeToElement(new { text = response });

        return new Message(
            Guid.NewGuid(),
            message.To,
            message.From,
            MessageType.Domain,
            message.ConversationId,
            responsePayload,
            DateTimeOffset.UtcNow);
    }

    private async Task<Message?> DispatchStreamingAsync(Message message, string prompt, CancellationToken cancellationToken)
    {
        var agentId = message.To.Path;
        var responseBuilder = new StringBuilder();

        _logger.LogDebug("Starting streaming dispatch for message {MessageId} to agent {AgentId}.",
            message.Id, agentId);

        await foreach (var streamEvent in aiProvider.StreamCompleteAsync(prompt, cancellationToken))
        {
            await streamEventPublisher!.PublishAsync(agentId, streamEvent, cancellationToken);

            if (streamEvent is StreamEvent.TokenDelta tokenDelta)
            {
                responseBuilder.Append(tokenDelta.Text);
            }
        }

        _logger.LogDebug("Streaming completed for message {MessageId}.", message.Id);

        var responsePayload = JsonSerializer.SerializeToElement(new { text = responseBuilder.ToString() });

        return new Message(
            Guid.NewGuid(),
            message.To,
            message.From,
            MessageType.Domain,
            message.ConversationId,
            responsePayload,
            DateTimeOffset.UtcNow);
    }
}