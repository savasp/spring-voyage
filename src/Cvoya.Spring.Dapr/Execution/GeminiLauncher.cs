// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Execution;

using System.Text.Json;

using Cvoya.Spring.Core.Execution;

using Microsoft.Extensions.Logging;

/// <summary>
/// <see cref="IAgentToolLauncher"/> for Gemini CLI containers. Materialises a
/// per-invocation working directory containing:
/// <list type="bullet">
///   <item><c>GEMINI.md</c> — the assembled system prompt (all four layers).
///         Gemini CLI reads this file as its instructions file.</item>
///   <item><c>.mcp.json</c> — MCP server endpoint + bearer token the Gemini agent will dial.</item>
/// </list>
/// The directory is bind-mounted at <c>/workspace</c> inside the container and
/// <see cref="CleanupAsync"/> removes it after the run completes.
/// <para>
/// <b>Expected container image shape:</b> The image must bundle the Gemini CLI
/// and the A2A sidecar from <c>agents/a2a-sidecar/</c>. The sidecar wraps the
/// <c>gemini</c> CLI binary, exposing it behind an A2A endpoint. The container
/// must read <c>GEMINI.md</c> and <c>.mcp.json</c> from the <c>/workspace</c>
/// mount and honour the <c>GOOGLE_API_KEY</c> environment variable for
/// authentication with the Google AI API.
/// </para>
/// </summary>
public class GeminiLauncher(ILoggerFactory loggerFactory) : IAgentToolLauncher
{
    internal const string WorkspaceMountPath = "/workspace";
    private readonly ILogger _logger = loggerFactory.CreateLogger<GeminiLauncher>();

    /// <inheritdoc />
    public string Tool => "gemini";

    /// <inheritdoc />
    public async Task<AgentLaunchPrep> PrepareAsync(
        AgentLaunchContext context,
        CancellationToken cancellationToken = default)
    {
        var workdir = Path.Combine(
            Path.GetTempPath(),
            "spring-gemini-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(workdir);

        await File.WriteAllTextAsync(
            Path.Combine(workdir, "GEMINI.md"),
            context.Prompt,
            cancellationToken);

        var mcpConfig = new
        {
            mcpServers = new Dictionary<string, object>
            {
                ["spring-voyage"] = new
                {
                    type = "http",
                    url = context.McpEndpoint,
                    headers = new Dictionary<string, string>
                    {
                        ["Authorization"] = $"Bearer {context.McpToken}"
                    }
                }
            }
        };

        await File.WriteAllTextAsync(
            Path.Combine(workdir, ".mcp.json"),
            JsonSerializer.Serialize(mcpConfig, new JsonSerializerOptions { WriteIndented = true }),
            cancellationToken);

        _logger.LogInformation(
            "Prepared Gemini working directory {Workdir} for agent {AgentId} conversation {ConversationId}",
            workdir, context.AgentId, context.ConversationId);

        var envVars = new Dictionary<string, string>
        {
            ["SPRING_AGENT_ID"] = context.AgentId,
            ["SPRING_CONVERSATION_ID"] = context.ConversationId,
            ["SPRING_MCP_ENDPOINT"] = context.McpEndpoint,
            ["SPRING_AGENT_TOKEN"] = context.McpToken,
            ["SPRING_SYSTEM_PROMPT"] = context.Prompt
        };

        var mounts = new List<string>
        {
            $"{workdir}:{WorkspaceMountPath}"
        };

        return new AgentLaunchPrep(workdir, envVars, mounts);
    }

    /// <inheritdoc />
    public Task CleanupAsync(string workingDirectory, CancellationToken cancellationToken = default)
    {
        try
        {
            if (Directory.Exists(workingDirectory))
            {
                Directory.Delete(workingDirectory, recursive: true);
                _logger.LogDebug("Deleted Gemini working directory {Workdir}", workingDirectory);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to delete Gemini working directory {Workdir}; leaving in place for operator inspection.",
                workingDirectory);
        }

        return Task.CompletedTask;
    }
}