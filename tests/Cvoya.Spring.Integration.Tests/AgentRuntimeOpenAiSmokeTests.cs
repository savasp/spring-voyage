// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Integration.Tests;

using Cvoya.Spring.AgentRuntimes.OpenAI;
using Cvoya.Spring.AgentRuntimes.OpenAI.DependencyInjection;
using Cvoya.Spring.Core.AgentRuntimes;
using Cvoya.Spring.Dapr.AgentRuntimes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Shouldly;

using Xunit;

/// <summary>
/// Integration smoke test for the OpenAI agent runtime (#680). Confirms
/// that calling <see cref="ServiceCollectionExtensions.AddCvoyaSpringAgentRuntimeOpenAI"/>
/// is enough for the registry shipped with the Dapr layer to resolve the
/// runtime by id.
/// </summary>
public class AgentRuntimeOpenAiSmokeTests
{
    [Fact]
    public void Registry_ResolvesOpenAiAfterDiWireUp()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeOpenAI();
        // The host normally calls AddCvoyaSpringDapr() to register the
        // registry, but the registry has zero Dapr dependencies and the
        // smoke test should not pull in the entire Dapr stack — register
        // the same default explicitly here.
        services.TryAddSingleton<IAgentRuntimeRegistry, AgentRuntimeRegistry>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IAgentRuntimeRegistry>();

        var runtime = registry.Get("openai");

        runtime.ShouldNotBeNull();
        runtime!.Id.ShouldBe("openai");
        runtime.DisplayName.ShouldBe("OpenAI (dapr-agent + OpenAI API)");
        runtime.ToolKind.ShouldBe("dapr-agent");
        runtime.CredentialSchema.Kind.ShouldBe(AgentRuntimeCredentialKind.ApiKey);
    }

    [Fact]
    public void Registry_OpenAiRuntime_ExposesSeededModelCatalog()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeOpenAI();
        services.TryAddSingleton<IAgentRuntimeRegistry, AgentRuntimeRegistry>();

        using var provider = services.BuildServiceProvider();
        var registry = provider.GetRequiredService<IAgentRuntimeRegistry>();

        var runtime = registry.Get("openai")!;

        runtime.DefaultModels.ShouldNotBeEmpty();
        runtime.DefaultModels.Select(m => m.Id).ShouldContain("gpt-4o");
    }

    [Fact]
    public async Task VerifyContainerBaselineAsync_PassesWhenDaprActorsLoaded()
    {
        // The integration test project references Dapr.Actors directly,
        // so the assembly is loaded into the AppDomain by the time the
        // baseline check runs. This is the regression gate for the dapr-
        // agent baseline declared by #680.
        _ = typeof(global::Dapr.Actors.ActorId);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddCvoyaSpringAgentRuntimeOpenAI();
        services.TryAddSingleton<IAgentRuntimeRegistry, AgentRuntimeRegistry>();

        using var provider = services.BuildServiceProvider();
        var runtime = provider.GetRequiredService<IAgentRuntimeRegistry>().Get("openai")!;

        var result = await runtime.VerifyContainerBaselineAsync(TestContext.Current.CancellationToken);

        result.Passed.ShouldBeTrue();
        result.Errors.ShouldBeEmpty();
    }
}