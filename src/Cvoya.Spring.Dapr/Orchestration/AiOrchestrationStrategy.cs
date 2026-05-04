// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Orchestration;

using System.Text.Json;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Orchestration;

using Microsoft.Extensions.Logging;

/// <summary>
/// Orchestration strategy that uses an AI provider to decide how to route
/// a message to the most appropriate member within a unit.
/// </summary>
/// <remarks>
/// Resolves the provider per-message through <see cref="IAiProviderRegistry"/>
/// using the unit's <see cref="IUnitContext.ProviderId"/>. When the unit
/// hasn't declared a provider id the strategy falls back to <paramref name="defaultProvider"/>
/// (the platform-default <see cref="IAiProvider"/>, typically
/// <c>AnthropicProvider</c> per OSS DI registration order). Without this
/// per-unit selection, every dispatch hit whichever provider DI happened
/// to bind first, ignoring the manifest's <c>execution.provider</c> slot
/// (#1696).
/// </remarks>
public class AiOrchestrationStrategy(
    IAiProviderRegistry providerRegistry,
    IAiProvider defaultProvider,
    ILoggerFactory loggerFactory) : IOrchestrationStrategy
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<AiOrchestrationStrategy>();

    /// <inheritdoc />
    public async Task<Message?> OrchestrateAsync(Message message, IUnitContext context, CancellationToken cancellationToken = default)
    {
        if (context.Members.Count == 0)
        {
            _logger.LogWarning("Unit {UnitAddress} has no members to route to", context.UnitAddress);
            return null;
        }

        var prompt = BuildRoutingPrompt(message, context);

        // Per-unit provider resolution. Order:
        //   (1) registry hit on the unit's declared provider id
        //   (2) default IAiProvider injected through DI (OSS = Anthropic)
        // A declared provider id that doesn't resolve drops to (2) with a
        // warning so the operator sees an actionable log line; otherwise a
        // typo silently dispatches to the wrong endpoint.
        IAiProvider aiProvider;
        if (!string.IsNullOrWhiteSpace(context.ProviderId))
        {
            var resolved = providerRegistry.Get(context.ProviderId);
            if (resolved is null)
            {
                _logger.LogWarning(
                    "Unit {UnitAddress} declares provider '{ProviderId}' but no IAiProvider with that id is registered; falling back to default provider '{DefaultId}'.",
                    context.UnitAddress, context.ProviderId, defaultProvider.Id);
                aiProvider = defaultProvider;
            }
            else
            {
                aiProvider = resolved;
            }
        }
        else
        {
            aiProvider = defaultProvider;
        }

        _logger.LogInformation(
            "AI orchestration for message {MessageId} in unit {UnitAddress} via provider {ProviderId} with {MemberCount} members",
            message.Id, context.UnitAddress, aiProvider.Id, context.Members.Count);

        var aiResponse = await aiProvider.CompleteAsync(prompt, cancellationToken);

        var targetAddress = ParseRoutingDecision(aiResponse, context.Members);
        if (targetAddress is null)
        {
            _logger.LogWarning(
                "AI returned an invalid routing decision for message {MessageId}: {Response}",
                message.Id, aiResponse);
            return null;
        }

        _logger.LogInformation(
            "AI routed message {MessageId} to member {TargetAddress}",
            message.Id, targetAddress);

        var forwardedMessage = message with { To = targetAddress };
        return await context.SendAsync(forwardedMessage, cancellationToken);
    }

    /// <summary>
    /// Builds the routing prompt that instructs the AI to select the best member.
    /// </summary>
    internal static string BuildRoutingPrompt(Message message, IUnitContext context)
    {
        var memberList = string.Join("\n", context.Members.Select(m => $"- {m.Scheme}://{m.Path}"));
        var payloadText = message.Payload.ValueKind != JsonValueKind.Undefined
            ? message.Payload.GetRawText()
            : "{}";

        return $"""
            You are a message router for a multi-agent unit.

            ## Message
            From: {message.From.Scheme}://{message.From.Path}
            Type: {message.Type}
            Payload: {payloadText}

            ## Available Members
            {memberList}

            Select the single best member to handle this message.
            Respond with ONLY the member address in the format: scheme://path
            """;
    }

    /// <summary>
    /// Parses the AI response to extract a target member address.
    /// Returns null if the response does not match any known member.
    /// </summary>
    internal static Address? ParseRoutingDecision(string aiResponse, IReadOnlyList<Address> members)
    {
        var trimmed = aiResponse.Trim();

        foreach (var member in members)
        {
            var memberUri = $"{member.Scheme}://{member.Path}";
            if (trimmed.Contains(memberUri, StringComparison.OrdinalIgnoreCase))
            {
                return member;
            }
        }

        return null;
    }
}