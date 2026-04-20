// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.Claude.Internal;

using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Strongly-typed view of the runtime's <c>agent-runtimes/claude/seed.json</c>
/// embedded resource. Mirrors the schema documented in
/// <c>src/Cvoya.Spring.Core/AgentRuntimes/README.md</c>.
/// </summary>
internal sealed record ClaudeRuntimeSeed(
    [property: JsonPropertyName("models")] IReadOnlyList<string> Models,
    [property: JsonPropertyName("defaultModel")] string DefaultModel,
    [property: JsonPropertyName("baseUrl")] string? BaseUrl = null,
    [property: JsonPropertyName("extras")] JsonElement? Extras = null);

/// <summary>
/// Loads <see cref="ClaudeRuntimeSeed"/> from the embedded
/// <c>agent-runtimes/claude/seed.json</c> resource. Hosts that need to
/// override the seed should register their own <see cref="Cvoya.Spring.Core.AgentRuntimes.IAgentRuntime"/>
/// implementation; the contract intentionally has no per-runtime hot-swap
/// hook.
/// </summary>
internal static class ClaudeRuntimeSeedLoader
{
    /// <summary>
    /// The resource name embedded by the project file (see
    /// <c>Cvoya.Spring.AgentRuntimes.Claude.csproj</c>'s
    /// <c>EmbeddedResource LogicalName</c>). Held as a constant so tests
    /// can pin drift between the csproj declaration and the runtime
    /// loader.
    /// </summary>
    public const string EmbeddedResourceName = "Cvoya.Spring.AgentRuntimes.Claude.agent-runtimes.claude.seed.json";

    /// <summary>
    /// Reads and deserializes the embedded seed file. Throws if the file
    /// is missing, malformed, or violates the documented schema (the
    /// runtime cannot start with a broken seed — fail loud at process
    /// boot rather than silently shipping an empty model list).
    /// </summary>
    public static ClaudeRuntimeSeed Load()
    {
        var assembly = typeof(ClaudeRuntimeSeedLoader).Assembly;
        return Load(assembly);
    }

    /// <summary>
    /// Test seam — load from an arbitrary assembly. Production code calls
    /// the parameterless overload.
    /// </summary>
    internal static ClaudeRuntimeSeed Load(Assembly assembly)
    {
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' was not found. " +
                "Verify the EmbeddedResource entry in Cvoya.Spring.AgentRuntimes.Claude.csproj.");

        var seed = JsonSerializer.Deserialize(
            stream,
            ClaudeRuntimeSeedJsonContext.Default.ClaudeRuntimeSeed)
            ?? throw new InvalidOperationException(
                $"Failed to deserialize embedded seed '{EmbeddedResourceName}': payload was null.");

        if (seed.Models is null || seed.Models.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedded seed '{EmbeddedResourceName}' has no models.");
        }
        if (string.IsNullOrWhiteSpace(seed.DefaultModel))
        {
            throw new InvalidOperationException(
                $"Embedded seed '{EmbeddedResourceName}' is missing 'defaultModel'.");
        }
        if (!seed.Models.Contains(seed.DefaultModel))
        {
            throw new InvalidOperationException(
                $"Embedded seed '{EmbeddedResourceName}' lists 'defaultModel' '{seed.DefaultModel}' " +
                "which does not appear in the 'models' array.");
        }

        return seed;
    }
}

[JsonSerializable(typeof(ClaudeRuntimeSeed))]
internal partial class ClaudeRuntimeSeedJsonContext : JsonSerializerContext;