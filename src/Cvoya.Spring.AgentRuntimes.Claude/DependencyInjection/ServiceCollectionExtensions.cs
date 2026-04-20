// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.DependencyInjection;

using Cvoya.Spring.Core.AgentRuntimes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

/// <summary>
/// DI extension methods for registering the Claude agent runtime
/// (<see cref="ClaudeAgentRuntime"/>) with the Spring Voyage host.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="ClaudeAgentRuntime"/> as an
    /// <see cref="IAgentRuntime"/> alongside the named
    /// <see cref="HttpClient"/> it uses for the REST fallback path.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses <see cref="ServiceCollectionDescriptorExtensions.TryAddEnumerable(IServiceCollection, ServiceDescriptor)"/>
    /// so a downstream host (e.g. the private cloud repo) can register
    /// its own <see cref="IAgentRuntime"/> with id <c>claude</c> first
    /// — the pre-registration wins and this call becomes a no-op for
    /// that slot. Per <see cref="IAgentRuntimeRegistry"/> semantics, the
    /// registry deduplicates on id, so duplicates simply mean the first
    /// registered runtime serves the lookup; the registration here is
    /// safe to call after a custom replacement.
    /// </para>
    /// <para>
    /// Idempotent for repeat calls in the same DI container — the
    /// concrete <see cref="ClaudeAgentRuntime"/> registration is gated on
    /// "is it already present?" before being added.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection for chaining.</returns>
    public static IServiceCollection AddCvoyaSpringAgentRuntimeClaude(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        // Named HTTP client for the REST fallback path. AddHttpClient is
        // idempotent on the named-options binding but not on the HTTP
        // client factory registration; the factory tolerates repeated
        // configuration calls because they're merged into the same
        // options entry.
        services.AddHttpClient(ClaudeAgentRuntime.HttpClientName);

        // Concrete singleton — registered before the IAgentRuntime
        // mapping so consumers that want the strongly-typed runtime
        // (e.g. for diagnostics, the wizard's runtime-detail view) can
        // resolve it directly. TryAdd so a host that pre-registers a
        // custom subclass keeps that registration.
        services.TryAddSingleton<ClaudeAgentRuntime>();

        // Enumerable IAgentRuntime registration — AgentRuntimeRegistry
        // (in Cvoya.Spring.Dapr) enumerates every IAgentRuntime in DI,
        // and TryAddEnumerable deduplicates on the implementation type
        // so calling AddCvoyaSpringAgentRuntimeClaude() twice does not
        // produce two entries.
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IAgentRuntime, ClaudeAgentRuntime>(
                sp => sp.GetRequiredService<ClaudeAgentRuntime>()));

        return services;
    }
}