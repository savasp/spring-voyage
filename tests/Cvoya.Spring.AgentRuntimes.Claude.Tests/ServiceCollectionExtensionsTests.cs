// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.Tests;

using Cvoya.Spring.AgentRuntimes.Claude.DependencyInjection;
using Cvoya.Spring.Core.AgentRuntimes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

/// <summary>
/// Verifies the DI extension wires the runtime so consumers that
/// depend on the <see cref="IAgentRuntime"/> contract can resolve it
/// without referencing the concrete project.
/// </summary>
public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCvoyaSpringAgentRuntimeClaude_RegistersRuntimeAsIAgentRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeClaude();

        using var provider = services.BuildServiceProvider();

        var runtimes = provider.GetServices<IAgentRuntime>().ToArray();
        runtimes.ShouldContain(r => r.Id == "claude");
    }

    [Fact]
    public void AddCvoyaSpringAgentRuntimeClaude_IsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeClaude();
        services.AddCvoyaSpringAgentRuntimeClaude();

        using var provider = services.BuildServiceProvider();

        var runtimes = provider.GetServices<IAgentRuntime>()
            .Where(r => r.Id == "claude")
            .ToArray();
        runtimes.Length.ShouldBe(1);
    }

    [Fact]
    public void AddCvoyaSpringAgentRuntimeClaude_RegistersHttpClientFactory()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeClaude();

        using var provider = services.BuildServiceProvider();

        var factory = provider.GetService<IHttpClientFactory>();
        factory.ShouldNotBeNull();

        // Naming the client matches the constant the runtime resolves.
        var client = factory!.CreateClient(ClaudeAgentRuntime.HttpClientName);
        client.ShouldNotBeNull();
    }

    [Fact]
    public void AddCvoyaSpringAgentRuntimeClaude_RuntimeImplementsContract()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeClaude();

        using var provider = services.BuildServiceProvider();

        var runtime = provider.GetServices<IAgentRuntime>().Single(r => r.Id == "claude");
        runtime.ShouldBeOfType<ClaudeAgentRuntime>();
        runtime.DefaultModels.Count.ShouldBeGreaterThan(0);
    }
}