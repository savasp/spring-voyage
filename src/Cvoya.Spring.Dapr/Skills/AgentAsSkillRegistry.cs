// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Skills;

using System.Text.Json;

using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Skills;
using Cvoya.Spring.Core.Units;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

/// <summary>
/// Exposes every registered agent as a skill through the shared
/// <see cref="ISkillRegistry"/> seam (#359). Each tool corresponds to one
/// agent and, when invoked, dispatches a <see cref="Message"/> to that
/// agent through the platform's <see cref="IMessageRouter"/> and returns the
/// agent's response payload.
/// </summary>
/// <remarks>
/// <para>
/// Identity is preserved verbatim — the tool name is
/// <c>agent_{agent-id}</c>, with path segments flattened on <c>/</c> so the
/// tool surface matches the agent's directory address. The tool description
/// carries the agent's display description and role (when present) so a
/// caller can reason about the capability without a separate lookup.
/// </para>
/// <para>
/// Unit-boundary opacity (#413) is honoured at tool-enumeration time. An
/// agent whose contribution would be hidden by an ancestor unit's boundary
/// under <see cref="BoundaryViewContext.External"/> is not advertised. The
/// filter is intentionally conservative: when the aggregator throws, times
/// out, or produces no information (the agent has no expertise seeded),
/// the agent is still advertised. Invocation routes through
/// <see cref="IMessageRouter"/>, which applies its own permission checks on
/// top, so advertising an agent without expertise cannot grant access that
/// the router would otherwise deny.
/// </para>
/// <para>
/// Registered once per host as an <see cref="ISkillRegistry"/>. Because
/// tools are enumerated eagerly at DI resolution time (see
/// <see cref="Mcp.McpServer"/>), the registry computes its tool list
/// <em>lazily</em> — each call to <see cref="GetToolDefinitions"/> reads a
/// snapshot of the directory. The platform caches the MCP server tool set
/// at <see cref="Mcp.McpServer"/> startup, so late-registered agents do not
/// appear in an already-running MCP session until the host restarts; this
/// matches the behaviour of every other <see cref="ISkillRegistry"/> today
/// and is tracked for follow-up once the platform adds a live tool-update
/// notification channel.
/// </para>
/// </remarks>
public class AgentAsSkillRegistry : ISkillRegistry
{
    /// <summary>Prefix applied to every agent-backed tool so naming never collides with connector-owned tools.</summary>
    public const string ToolPrefix = "agent_";

    private static readonly JsonElement ToolInputSchema = JsonSerializer.SerializeToElement(new
    {
        type = "object",
        properties = new
        {
            message = new
            {
                type = "string",
                description = "Natural-language instruction or payload to deliver to the agent.",
            },
            conversationId = new
            {
                type = "string",
                description = "Optional conversation id used to correlate related turns.",
            },
        },
        required = new[] { "message" },
        additionalProperties = false,
    });

    private readonly IDirectoryService _directoryService;
    private readonly IMessageRouter _messageRouter;
    private readonly IExpertiseAggregator _expertiseAggregator;
    private readonly IMembershipLookup _membershipLookup;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes the registry with the platform services it delegates to.
    /// </summary>
    /// <remarks>
    /// <see cref="IUnitMembershipRepository"/> is scoped (it consumes the
    /// request-scoped <c>SpringDbContext</c>), but this registry is a
    /// singleton so every skill-invocation call site gets the same tool
    /// surface. The registry indirects membership reads through
    /// <see cref="IServiceScopeFactory"/> so a fresh scope is created per
    /// call — matching how <c>McpServer</c> resolves
    /// <c>IUnitPolicyEnforcer</c>.
    /// </remarks>
    public AgentAsSkillRegistry(
        IDirectoryService directoryService,
        IMessageRouter messageRouter,
        IServiceScopeFactory scopeFactory,
        IExpertiseAggregator expertiseAggregator,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
        : this(
              directoryService,
              messageRouter,
              new ScopedMembershipLookup(scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory))),
              expertiseAggregator,
              timeProvider,
              loggerFactory)
    {
    }

    /// <summary>
    /// Test-friendly constructor that takes a pre-resolved membership lookup.
    /// Production code paths use the public constructor; tests supply
    /// <see cref="DirectMembershipLookup"/> so they can substitute the
    /// repository without a DI container.
    /// </summary>
    internal AgentAsSkillRegistry(
        IDirectoryService directoryService,
        IMessageRouter messageRouter,
        IMembershipLookup membershipLookup,
        IExpertiseAggregator expertiseAggregator,
        TimeProvider timeProvider,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(directoryService);
        ArgumentNullException.ThrowIfNull(messageRouter);
        ArgumentNullException.ThrowIfNull(membershipLookup);
        ArgumentNullException.ThrowIfNull(expertiseAggregator);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _directoryService = directoryService;
        _messageRouter = messageRouter;
        _membershipLookup = membershipLookup;
        _expertiseAggregator = expertiseAggregator;
        _timeProvider = timeProvider;
        _logger = loggerFactory.CreateLogger<AgentAsSkillRegistry>();
    }

    /// <inheritdoc />
    public string Name => "agents";

    /// <summary>
    /// Gets the tool-definition list for every currently registered agent,
    /// filtered by the external view of every ancestor unit's boundary.
    /// </summary>
    /// <remarks>
    /// Called synchronously from <see cref="Mcp.McpServer"/>'s constructor to
    /// build the tool lookup, so the method collapses the async directory
    /// read into a blocking wait. Production call sites call this only once
    /// at host startup; production callers that need periodic refresh use
    /// <see cref="GetToolDefinitionsAsync"/> instead.
    /// </remarks>
    public IReadOnlyList<ToolDefinition> GetToolDefinitions()
    {
        // Preserves the synchronous ISkillRegistry contract. The async
        // equivalent is exposed for callers that can await.
        return GetToolDefinitionsAsync(CancellationToken.None).GetAwaiter().GetResult();
    }

    /// <summary>
    /// Async equivalent of <see cref="GetToolDefinitions"/>. Preferred by
    /// callers that can await so the underlying directory / boundary reads
    /// don't block a thread pool worker.
    /// </summary>
    public async Task<IReadOnlyList<ToolDefinition>> GetToolDefinitionsAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<DirectoryEntry> entries;
        try
        {
            entries = await _directoryService.ListAllAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to enumerate directory for agent-as-skill registry; returning empty tool set.");
            return Array.Empty<ToolDefinition>();
        }

        var tools = new List<ToolDefinition>(entries.Count);
        foreach (var entry in entries)
        {
            if (!string.Equals(entry.Address.Scheme, "agent", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            bool hidden;
            try
            {
                hidden = await IsHiddenByBoundaryAsync(entry.Address, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Never block agent advertisement on a boundary-store failure.
                // The router still applies its own permission checks at invocation
                // time, so a best-effort "visible" decision can never grant
                // access the router would otherwise refuse.
                _logger.LogWarning(ex,
                    "Boundary check failed for agent {Scheme}://{Path}; advertising agent.",
                    entry.Address.Scheme, entry.Address.Path);
                hidden = false;
            }

            if (hidden)
            {
                _logger.LogDebug(
                    "Agent {Scheme}://{Path} hidden by ancestor unit boundary; not advertised as skill.",
                    entry.Address.Scheme, entry.Address.Path);
                continue;
            }

            tools.Add(BuildToolDefinition(entry));
        }

        tools.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        return tools;
    }

    /// <inheritdoc />
    public async Task<JsonElement> InvokeAsync(
        string toolName,
        JsonElement arguments,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(toolName);

        if (!toolName.StartsWith(ToolPrefix, StringComparison.Ordinal))
        {
            throw new SkillNotFoundException(toolName);
        }

        var agentPath = toolName[ToolPrefix.Length..];
        if (string.IsNullOrWhiteSpace(agentPath))
        {
            throw new SkillNotFoundException(toolName);
        }

        agentPath = DenormaliseAgentPath(agentPath);
        var destination = new Address("agent", agentPath);

        // Re-check boundary opacity on invocation so a race where a boundary is
        // tightened between enumeration and invocation still denies external
        // access.
        if (await IsHiddenByBoundaryAsync(destination, cancellationToken))
        {
            _logger.LogWarning(
                "Invocation of agent-as-skill {Tool} denied by unit boundary.",
                toolName);
            throw new SkillNotFoundException(toolName);
        }

        var message = arguments.ValueKind == JsonValueKind.Object && arguments.TryGetProperty("message", out var messageProp)
            ? messageProp.GetString() ?? string.Empty
            : string.Empty;

        var conversationId = arguments.ValueKind == JsonValueKind.Object
            && arguments.TryGetProperty("conversationId", out var convProp)
            && convProp.ValueKind == JsonValueKind.String
            ? convProp.GetString()
            : null;

        var payload = JsonSerializer.SerializeToElement(new { message });

        var envelope = new Message(
            Guid.NewGuid(),
            new Address("platform", "agent-as-skill"),
            destination,
            MessageType.Domain,
            conversationId,
            payload,
            _timeProvider.GetUtcNow());

        var result = await _messageRouter.RouteAsync(envelope, cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error;
            var errorText = error is null
                ? "routing failed"
                : $"{error.Code}: {error.Message}";

            _logger.LogWarning(
                "Agent-as-skill invocation {Tool} failed: {Error}",
                toolName, errorText);
            return JsonSerializer.SerializeToElement(new
            {
                error = errorText,
            });
        }

        if (result.Value is null)
        {
            return JsonSerializer.SerializeToElement(new
            {
                delivered = true,
                response = (string?)null,
            });
        }

        return result.Value.Payload;
    }

    /// <summary>
    /// Builds the canonical tool name for an agent address. Forward-slashes
    /// in the path are flattened to double-underscores so the tool name is a
    /// single identifier — MCP client libraries tokenize on <c>/</c>.
    /// </summary>
    internal static string NormaliseAgentPath(string agentPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(agentPath);
        return agentPath.Replace("/", "__", StringComparison.Ordinal);
    }

    /// <summary>Reverses the transformation applied by <see cref="NormaliseAgentPath"/>.</summary>
    internal static string DenormaliseAgentPath(string toolSuffix)
    {
        ArgumentException.ThrowIfNullOrEmpty(toolSuffix);
        return toolSuffix.Replace("__", "/", StringComparison.Ordinal);
    }

    private ToolDefinition BuildToolDefinition(DirectoryEntry entry)
    {
        var toolName = $"{ToolPrefix}{NormaliseAgentPath(entry.Address.Path)}";
        var description = string.IsNullOrWhiteSpace(entry.Description)
            ? $"Invoke agent {entry.DisplayName} ({entry.Address.Path})."
            : entry.Description;
        if (!string.IsNullOrWhiteSpace(entry.Role))
        {
            description = $"{description} Role: {entry.Role}.";
        }

        return new ToolDefinition(toolName, description, ToolInputSchema);
    }

    /// <summary>
    /// Returns <c>true</c> when at least one ancestor unit's boundary strips
    /// every trace of this agent from its external view. An agent with no
    /// unit memberships, or whose every ancestor's boundary still lets at
    /// least one of the agent's entries through, is treated as externally
    /// visible.
    /// </summary>
    /// <remarks>
    /// The check intentionally does not fail-closed on "no expertise": an
    /// agent that has not been seeded with expertise may still be a
    /// legitimate external skill. Opacity rules are keyed on
    /// <see cref="ExpertiseEntry.Origin"/> so an agent with no entries
    /// produces nothing to match against — which is exactly what the
    /// aggregator itself does for an empty-membership root.
    /// </remarks>
    private async Task<bool> IsHiddenByBoundaryAsync(
        Address agentAddress,
        CancellationToken cancellationToken)
    {
        // `IUnitMembershipRepository.ListByAgentAsync` is keyed on the
        // agent's canonical path (equivalent to `Address.Path`) — the
        // membership table does not store the scheme.
        IReadOnlyList<UnitMembership> memberships;
        try
        {
            memberships = await _membershipLookup.ListByAgentAsync(agentAddress.Path, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogDebug(ex,
                "Membership lookup failed for {Scheme}://{Path}; treating agent as externally visible.",
                agentAddress.Scheme, agentAddress.Path);
            return false;
        }

        if (memberships.Count == 0)
        {
            return false;
        }

        foreach (var membership in memberships)
        {
            var unitAddress = new Address("unit", membership.UnitId);
            AggregatedExpertise insideView;
            AggregatedExpertise externalView;
            try
            {
                insideView = await _expertiseAggregator.GetAsync(
                    unitAddress, BoundaryViewContext.InsideUnit, cancellationToken);
                externalView = await _expertiseAggregator.GetAsync(
                    unitAddress, BoundaryViewContext.External, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogDebug(ex,
                    "Aggregator read failed for unit {UnitId}; skipping boundary check for this ancestor.",
                    membership.UnitId);
                continue;
            }

            var insideMatch = CountAgentContributions(insideView, agentAddress);
            if (insideMatch == 0)
            {
                // Nothing to hide in this ancestor's view — it doesn't
                // contribute opacity information either way.
                continue;
            }

            var externalMatch = CountAgentContributions(externalView, agentAddress);
            if (externalMatch == 0)
            {
                // Every contribution from this agent was stripped by this
                // ancestor's boundary — the agent is opaque from outside.
                return true;
            }
        }

        return false;
    }

    private static int CountAgentContributions(AggregatedExpertise snapshot, Address agentAddress)
    {
        var count = 0;
        foreach (var entry in snapshot.Entries)
        {
            if (string.Equals(entry.Origin.Scheme, agentAddress.Scheme, StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.Origin.Path, agentAddress.Path, StringComparison.Ordinal))
            {
                count++;
            }
        }
        return count;
    }

    /// <summary>
    /// Narrow shim over the scoped <see cref="IUnitMembershipRepository"/> so
    /// the singleton registry can call a scoped service without capturing a
    /// stale scope. Implementations are responsible for opening / disposing
    /// any scope they need.
    /// </summary>
    internal interface IMembershipLookup
    {
        Task<IReadOnlyList<UnitMembership>> ListByAgentAsync(
            string agentAddress,
            CancellationToken cancellationToken);
    }

    /// <summary>
    /// Production membership lookup — creates a fresh DI scope per call so
    /// the scoped <see cref="IUnitMembershipRepository"/> (and the
    /// underlying <c>SpringDbContext</c>) is constructed and disposed per
    /// skill-enumeration / skill-invocation.
    /// </summary>
    private sealed class ScopedMembershipLookup(IServiceScopeFactory scopeFactory) : IMembershipLookup
    {
        public async Task<IReadOnlyList<UnitMembership>> ListByAgentAsync(
            string agentAddress, CancellationToken cancellationToken)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IUnitMembershipRepository>();
            return await repository.ListByAgentAsync(agentAddress, cancellationToken);
        }
    }
}