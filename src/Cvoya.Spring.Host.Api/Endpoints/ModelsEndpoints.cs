// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using System.Text.Json.Serialization;

using Cvoya.Spring.Core.Execution;

using Microsoft.AspNetCore.Http;

/// <summary>
/// Maps the model-discovery endpoint (<c>GET /api/v1/models/{provider}</c>).
/// Feeds the unit-creation wizard's model dropdown with the current list from
/// the provider's API when available, with a graceful fallback to a curated
/// static list when no API key is configured or the provider is unreachable.
/// See issue #597.
/// </summary>
/// <remarks>
/// <para>
/// The endpoint always returns 200 with a list — the <see cref="IModelCatalog"/>
/// implementation is responsible for falling back to the static list on
/// dynamic-fetch failures and logging a warning. This keeps the wizard
/// functional on networks that can't reach provider APIs (air-gapped,
/// dev-without-secrets, etc.) and avoids coupling the UI to provider
/// availability.
/// </para>
/// </remarks>
public static class ModelsEndpoints
{
    /// <summary>
    /// Registers the model-discovery endpoint on <paramref name="app"/>.
    /// </summary>
    public static RouteGroupBuilder MapModelsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/models")
            .WithTags("Models");

        group.MapGet("/{provider}", ListModelsAsync)
            .WithName("ListModelsForProvider")
            .WithSummary("List available models for an AI provider, dynamically when possible")
            .Produces<ModelsResponse>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> ListModelsAsync(
        string provider,
        IModelCatalog catalog,
        CancellationToken cancellationToken)
    {
        var models = await catalog.GetAvailableModelsAsync(provider, cancellationToken);
        return Results.Ok(new ModelsResponse(provider, models.ToArray()));
    }
}

/// <summary>
/// Response body for <c>GET /api/v1/models/{provider}</c>.
/// </summary>
/// <param name="Provider">Echoes the requested provider id.</param>
/// <param name="Models">The ordered list of model identifiers for that provider.</param>
public record ModelsResponse(
    [property: JsonPropertyName("provider")] string Provider,
    [property: JsonPropertyName("models")] string[] Models);