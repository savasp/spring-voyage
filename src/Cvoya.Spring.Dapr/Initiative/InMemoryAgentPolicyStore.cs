// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Initiative;

using System.Collections.Concurrent;

using Cvoya.Spring.Core.Initiative;

/// <summary>
/// Process-local, in-memory <see cref="IAgentPolicyStore"/> implementation backed by a
/// <see cref="ConcurrentDictionary{TKey, TValue}"/>. Suitable as a default so the DI
/// object graph resolves end-to-end; a Dapr actor-state-backed implementation will
/// replace this in a follow-up so policies survive process restarts and are shared
/// across replicas.
/// </summary>
public class InMemoryAgentPolicyStore : IAgentPolicyStore
{
    private readonly ConcurrentDictionary<string, InitiativePolicy> _policies = new();

    /// <inheritdoc />
    /// <remarks>
    /// Returns the policy previously supplied via <see cref="SetPolicyAsync"/> for the
    /// given target identifier, or a default <see cref="InitiativePolicy"/> (which is
    /// <see cref="InitiativeLevel.Passive"/>) when no policy has been stored.
    /// </remarks>
    public Task<InitiativePolicy> GetPolicyAsync(string targetId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = _policies.TryGetValue(targetId, out var stored)
            ? stored
            : new InitiativePolicy();

        return Task.FromResult(policy);
    }

    /// <inheritdoc />
    public Task SetPolicyAsync(string targetId, InitiativePolicy policy, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(targetId);
        ArgumentNullException.ThrowIfNull(policy);
        cancellationToken.ThrowIfCancellationRequested();

        _policies[targetId] = policy;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Looks up the agent-scoped policy (<c>"agent:{agentId}"</c>) and returns its
    /// <see cref="InitiativePolicy.MaxLevel"/>. Unit-ceiling enforcement (intersecting
    /// the agent policy with the enclosing unit's policy) is a follow-up; the
    /// agent-unit relationship is not yet modelled here.
    /// </remarks>
    public Task<InitiativeLevel> GetEffectiveLevelAsync(string agentId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        cancellationToken.ThrowIfCancellationRequested();

        var agentKey = agentId.StartsWith("agent:", StringComparison.Ordinal)
            ? agentId
            : $"agent:{agentId}";

        if (_policies.TryGetValue(agentKey, out var agentPolicy))
        {
            return Task.FromResult(agentPolicy.MaxLevel);
        }

        return Task.FromResult(InitiativeLevel.Passive);
    }
}