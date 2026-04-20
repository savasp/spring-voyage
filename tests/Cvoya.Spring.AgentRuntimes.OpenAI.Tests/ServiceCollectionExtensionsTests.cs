// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.OpenAI.Tests;

using Cvoya.Spring.AgentRuntimes.OpenAI;
using Cvoya.Spring.AgentRuntimes.OpenAI.DependencyInjection;
using Cvoya.Spring.Core.AgentRuntimes;

using Microsoft.Extensions.DependencyInjection;

using Shouldly;

using Xunit;

public class ServiceCollectionExtensionsTests
{
    [Fact]
    public void AddCvoyaSpringAgentRuntimeOpenAI_RegistersOpenAiAgentRuntime()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeOpenAI();

        using var provider = services.BuildServiceProvider();
        var runtimes = provider.GetServices<IAgentRuntime>().ToList();

        runtimes.ShouldContain(r => r is OpenAiAgentRuntime);
        runtimes.OfType<OpenAiAgentRuntime>().Single().Id.ShouldBe("openai");
    }

    [Fact]
    public void AddCvoyaSpringAgentRuntimeOpenAI_IsIdempotent()
    {
        // TryAddEnumerable drops the second registration of the same
        // implementation type, so calling the extension twice must
        // leave a single instance in the enumerable.
        var services = new ServiceCollection();
        services.AddLogging();

        services.AddCvoyaSpringAgentRuntimeOpenAI();
        services.AddCvoyaSpringAgentRuntimeOpenAI();

        using var provider = services.BuildServiceProvider();
        var runtimes = provider.GetServices<IAgentRuntime>().OfType<OpenAiAgentRuntime>().ToList();

        runtimes.Count.ShouldBe(1);
    }

    [Fact]
    public void AddCvoyaSpringAgentRuntimeOpenAI_RegistersNamedHttpClient()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeOpenAI();

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        var client = factory.CreateClient(OpenAiAgentRuntime.HttpClientName);
        client.ShouldNotBeNull();
    }
}