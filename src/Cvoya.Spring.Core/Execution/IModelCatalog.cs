// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Execution;

/// <summary>
/// Resolves the list of model identifiers available for a given AI provider
/// (e.g. <c>claude</c>, <c>openai</c>, <c>ollama</c>). Feeds the unit-creation
/// wizard's model dropdown so the list reflects what the provider actually
/// supports rather than a hard-coded snapshot that goes stale the moment the
/// provider ships a new model (#597).
/// </summary>
/// <remarks>
/// <para>
/// Implementations may fetch the list dynamically from the provider's models
/// endpoint when one exists (Anthropic, OpenAI, Ollama), and must fall back
/// to a curated static list when the provider has no such endpoint or when
/// the fetch fails (missing credentials, network error, rate limit). A
/// graceful fallback keeps the wizard functional in environments where the
/// platform has no provider API key configured — the user can still type or
/// pick one of the well-known names.
/// </para>
/// <para>
/// Results should be cached in-memory with a short TTL (measured in hours,
/// not seconds) so the wizard doesn't hit the provider on every page render.
/// </para>
/// <para>
/// The catalog names <em>model identifiers only</em>. Richer metadata
/// (pricing, context windows, capabilities) is deliberately out of scope —
/// that belongs to a full model registry, which is tracked separately.
/// </para>
/// </remarks>
public interface IModelCatalog
{
    /// <summary>
    /// Returns the set of model identifiers available for the given provider.
    /// </summary>
    /// <param name="providerId">
    /// The provider identifier used by the UI (e.g. <c>claude</c>, <c>openai</c>,
    /// <c>google</c>, <c>ollama</c>). Unknown providers yield an empty list.
    /// </param>
    /// <param name="cancellationToken">Cancels an in-flight provider fetch.</param>
    /// <returns>
    /// An ordered list of model identifiers. When the dynamic fetch fails the
    /// implementation returns the static fallback list rather than throwing, so
    /// the caller never sees provider-specific HTTP errors.
    /// </returns>
    Task<IReadOnlyList<string>> GetAvailableModelsAsync(
        string providerId,
        CancellationToken cancellationToken = default);
}