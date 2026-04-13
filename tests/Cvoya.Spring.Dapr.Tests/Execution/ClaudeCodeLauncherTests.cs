// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Execution;

using System.Text.Json;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Dapr.Execution;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for <see cref="ClaudeCodeLauncher"/>.
/// </summary>
public class ClaudeCodeLauncherTests
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ClaudeCodeLauncher _launcher;

    public ClaudeCodeLauncherTests()
    {
        _loggerFactory = Substitute.For<ILoggerFactory>();
        _loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _launcher = new ClaudeCodeLauncher(_loggerFactory);
    }

    [Fact]
    public void Tool_IsClaudeCode()
    {
        _launcher.Tool.ShouldBe("claude-code");
    }

    [Fact]
    public async Task PrepareAsync_WritesPromptAndMcpConfig()
    {
        var context = new AgentLaunchContext(
            AgentId: "ada",
            ConversationId: "conv-42",
            Prompt: "## Platform Instructions\nBe helpful.",
            McpEndpoint: "http://host.docker.internal:9999/mcp/",
            McpToken: "top-secret-token");

        AgentLaunchPrep prep;
        try
        {
            prep = await _launcher.PrepareAsync(context, TestContext.Current.CancellationToken);

            File.Exists(Path.Combine(prep.WorkingDirectory, "CLAUDE.md")).ShouldBeTrue();
            File.Exists(Path.Combine(prep.WorkingDirectory, ".mcp.json")).ShouldBeTrue();

            var promptOnDisk = await File.ReadAllTextAsync(
                Path.Combine(prep.WorkingDirectory, "CLAUDE.md"),
                TestContext.Current.CancellationToken);
            promptOnDisk.ShouldBe(context.Prompt);

            var mcpConfig = await File.ReadAllTextAsync(
                Path.Combine(prep.WorkingDirectory, ".mcp.json"),
                TestContext.Current.CancellationToken);
            var parsed = JsonDocument.Parse(mcpConfig).RootElement;
            var server = parsed.GetProperty("mcpServers").GetProperty("spring-voyage");
            server.GetProperty("url").GetString().ShouldBe(context.McpEndpoint);
            server.GetProperty("headers").GetProperty("Authorization").GetString()
                .ShouldBe("Bearer top-secret-token");

            prep.EnvironmentVariables["SPRING_AGENT_ID"].ShouldBe(context.AgentId);
            prep.EnvironmentVariables["SPRING_CONVERSATION_ID"].ShouldBe(context.ConversationId);
            prep.EnvironmentVariables["SPRING_MCP_ENDPOINT"].ShouldBe(context.McpEndpoint);
            prep.EnvironmentVariables["SPRING_AGENT_TOKEN"].ShouldBe(context.McpToken);
            prep.EnvironmentVariables["SPRING_SYSTEM_PROMPT"].ShouldBe(context.Prompt);

            prep.VolumeMounts.ShouldHaveSingleItem()
                .ShouldBe($"{prep.WorkingDirectory}:/workspace");
        }
        finally
        {
            // Explicit cleanup in case the test body fails before calling CleanupAsync.
        }

        await _launcher.CleanupAsync(prep.WorkingDirectory, TestContext.Current.CancellationToken);
        Directory.Exists(prep.WorkingDirectory).ShouldBeFalse();
    }

    [Fact]
    public async Task CleanupAsync_NonexistentDirectory_DoesNotThrow()
    {
        var nonexistent = Path.Combine(Path.GetTempPath(), "definitely-does-not-exist-" + Guid.NewGuid());

        var act = () => _launcher.CleanupAsync(nonexistent, TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }
}