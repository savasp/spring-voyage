// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Integration.Tests;

using System.ComponentModel;

using Cvoya.Spring.AgentRuntimes.Claude;
using Cvoya.Spring.AgentRuntimes.Claude.DependencyInjection;
using Cvoya.Spring.AgentRuntimes.Claude.Internal;
using Cvoya.Spring.Core.AgentRuntimes;
using Cvoya.Spring.Dapr.AgentRuntimes;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests covering the Claude agent runtime as it is wired in
/// the host (#679). Verifies the runtime resolves through
/// <see cref="IAgentRuntimeRegistry"/> after the host calls
/// <c>AddCvoyaSpringAgentRuntimeClaude()</c>, that the embedded seed
/// catalog is the source of <see cref="IAgentRuntime.DefaultModels"/>,
/// and that <see cref="IAgentRuntime.VerifyContainerBaselineAsync"/>
/// returns a clear error in environments where the <c>claude</c> CLI
/// is absent — the regression gate for #668.
/// </summary>
public class ClaudeAgentRuntimeIntegrationTests
{
    [Fact]
    public void Registry_ResolvesClaudeAfterAddCvoyaSpringAgentRuntimeClaude()
    {
        using var provider = BuildProvider();
        var registry = provider.GetRequiredService<IAgentRuntimeRegistry>();

        var runtime = registry.Get("claude");
        runtime.ShouldNotBeNull();
        runtime!.Id.ShouldBe("claude");
        runtime.ToolKind.ShouldBe("claude-code-cli");
        runtime.DisplayName.ShouldBe("Claude (Claude Code CLI + Anthropic API)");

        // Case-insensitive lookup is part of the registry contract.
        registry.Get("CLAUDE").ShouldNotBeNull();
        registry.All.ShouldContain(r => r.Id == "claude");
    }

    [Fact]
    public void DefaultModels_ComeFromSeedFile()
    {
        using var provider = BuildProvider();
        var runtime = provider.GetRequiredService<IAgentRuntimeRegistry>().Get("claude")!;

        var ids = runtime.DefaultModels.Select(m => m.Id).ToArray();
        ids.ShouldBe(new[]
        {
            "claude-sonnet-4-20250514",
            "claude-opus-4-20250514",
            "claude-haiku-4-20250514",
        });
    }

    [Fact]
    public async Task VerifyContainerBaselineAsync_CliMissing_ReturnsClearError()
    {
        // Regression gate for #668. The runtime must surface a precise,
        // operator-readable error when its required `claude` binary is
        // absent — instead of the legacy host-CLI-dependent error
        // ("Install Claude Code on this host"). We drive the runtime
        // with a stub IProcessRunner that simulates a missing binary so
        // the assertion is hermetic regardless of whether the CI image
        // happens to have claude installed.
        var factory = Substitute.For<IHttpClientFactory>();
        var runner = new MissingCliProcessRunner();
        var runtime = new ClaudeAgentRuntime(factory, runner, NullLogger<ClaudeAgentRuntime>.Instance);

        var result = await runtime.VerifyContainerBaselineAsync(TestContext.Current.CancellationToken);

        result.Passed.ShouldBeFalse();
        result.Errors.ShouldNotBeEmpty();
        result.Errors[0].ShouldNotBeNullOrWhiteSpace();
        // The error must mention the missing executable so an operator
        // knows where to look.
        result.Errors[0].ShouldContain("claude");
        runner.InvocationCount.ShouldBe(1);
    }

    private static ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        // Mimic what AddCvoyaSpringDapr does for the registry. Calling
        // the full AddCvoyaSpringDapr would pull in EF Core / Dapr /
        // every other piece; the registry contract test only needs the
        // registry itself and the runtime registrations.
        services.TryAddSingleton<IAgentRuntimeRegistry, AgentRuntimeRegistry>();
        services.AddCvoyaSpringAgentRuntimeClaude();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Process runner that simulates a missing CLI binary by raising the
    /// same <see cref="Win32Exception"/> the real <see cref="System.Diagnostics.Process"/>
    /// raises when the executable is not on PATH.
    /// </summary>
    private sealed class MissingCliProcessRunner : IProcessRunner
    {
        public int InvocationCount { get; private set; }

        public Task<ProcessRunResult> RunAsync(
            string fileName,
            IReadOnlyList<string> arguments,
            IReadOnlyDictionary<string, string> environment,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            InvocationCount++;
            throw new Win32Exception($"simulated: {fileName} not found");
        }
    }
}