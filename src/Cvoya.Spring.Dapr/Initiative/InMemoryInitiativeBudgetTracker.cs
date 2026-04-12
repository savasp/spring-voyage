// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Initiative;

using System.Collections.Concurrent;

using Cvoya.Spring.Core.Initiative;

/// <summary>
/// Process-local, in-memory <see cref="IInitiativeBudgetTracker"/> implementation that
/// tracks Tier 2 spend per agent and resets the running total on each UTC day
/// rollover. Suitable as a default so the DI object graph resolves end-to-end; a
/// Dapr actor-state-backed implementation will replace this in a follow-up so
/// budget state survives process restarts and is shared across replicas.
/// </summary>
public class InMemoryInitiativeBudgetTracker : IInitiativeBudgetTracker
{
    /// <summary>
    /// Fallback daily cap (in dollars) applied when the agent's policy does not specify
    /// a <see cref="Tier2Config.MaxCostPerDay"/>.
    /// </summary>
    public const decimal DefaultMaxCostPerDay = 3.00m;

    private readonly ConcurrentDictionary<string, AgentBudgetState> _state = new();
    private readonly IAgentPolicyStore _policyStore;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryInitiativeBudgetTracker"/> class.
    /// </summary>
    /// <param name="policyStore">Policy store used to resolve the per-agent Tier 2 daily cap.</param>
    public InMemoryInitiativeBudgetTracker(IAgentPolicyStore policyStore)
    {
        _policyStore = policyStore;
    }

    /// <inheritdoc />
    public async Task<bool> TryConsumeAsync(string agentId, decimal estimatedCost, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentId);
        cancellationToken.ThrowIfCancellationRequested();

        var policy = await _policyStore.GetPolicyAsync(agentId, cancellationToken);
        var cap = policy.Tier2?.MaxCostPerDay ?? DefaultMaxCostPerDay;

        var state = _state.GetOrAdd(agentId, _ => new AgentBudgetState());
        var today = DateTime.UtcNow.Date;

        lock (state.Gate)
        {
            if (state.Day != today)
            {
                state.Day = today;
                state.Spent = 0m;
            }

            if (state.Spent + estimatedCost > cap)
            {
                return false;
            }

            state.Spent += estimatedCost;
            return true;
        }
    }

    private sealed class AgentBudgetState
    {
        public readonly object Gate = new();
        public DateTime Day = DateTime.UtcNow.Date;
        public decimal Spent;
    }
}