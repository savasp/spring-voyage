// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Dapr.Data;
using Cvoya.Spring.Dapr.Tenancy;
using Cvoya.Spring.Host.Api.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Maps unit-scoped secret CRUD endpoints
/// (<c>/api/v1/units/{id}/secrets</c>). Tenant-scoped and platform-scoped
/// endpoints are deferred — the underlying <see cref="ISecretRegistry"/>
/// / <see cref="ISecretStore"/> abstractions already support them.
///
/// <para>
/// <b>Security contract.</b> Plaintext enters the system exclusively via
/// the <c>POST</c> body. It is never echoed in any response body, list
/// entry, or log line. The only way to read a plaintext value back out
/// is via server-side <see cref="ISecretResolver.ResolveAsync"/>.
/// </para>
/// </summary>
public static class SecretEndpoints
{
    /// <summary>
    /// Registers secret endpoints on the specified endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder for chaining.</returns>
    public static RouteGroupBuilder MapSecretEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/units/{id}/secrets")
            .WithTags("Secrets");

        group.MapGet("/", ListUnitSecretsAsync)
            .WithName("ListUnitSecrets")
            .WithSummary("List secret metadata for a unit. Never returns plaintext or store keys.")
            .Produces<UnitSecretsListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/", CreateUnitSecretAsync)
            .WithName("CreateUnitSecret")
            .WithSummary("Register a unit-scoped secret. Provide exactly one of 'value' (pass-through write) or 'externalStoreKey' (bind existing reference).")
            .Produces<CreateSecretResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        group.MapDelete("/{name}", DeleteUnitSecretAsync)
            .WithName("DeleteUnitSecret")
            .WithSummary("Delete a unit-scoped secret. The underlying plaintext (if owned by the platform) is also removed.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return group;
    }

    private static async Task<IResult> ListUnitSecretsAsync(
        string id,
        [FromServices] IDirectoryService directoryService,
        [FromServices] ISecretRegistry registry,
        [FromServices] SpringDbContext db,
        CancellationToken cancellationToken)
    {
        var unitAddress = new Address("unit", id);
        var entry = await directoryService.ResolveAsync(unitAddress, cancellationToken);
        if (entry is null)
        {
            return Results.Problem(
                detail: $"Unit '{id}' not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        // The ISecretRegistry.ListAsync surface returns SecretRefs only.
        // The UI wants the createdAt timestamp too, so project directly
        // off the tracked entity — tenant filtering is applied by the
        // registry contract's tenant context (re-expressed here through
        // DbContext access because we also need the timestamp column).
        // The tenant filter uses the same ITenantContext that the registry
        // would consult.
        var refs = await registry.ListAsync(SecretScope.Unit, id, cancellationToken);

        // To get the createdAt timestamps without adding a second method
        // on the registry, pull them with a single indexed query.
        var refNames = refs.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var timestamps = await db.SecretRegistryEntries
            .AsNoTracking()
            .Where(e => e.Scope == SecretScope.Unit && e.OwnerId == id)
            .Select(e => new { e.TenantId, e.Name, e.CreatedAt })
            .ToListAsync(cancellationToken);

        // Intersect by name — the ISecretRegistry.ListAsync call already
        // enforces tenant filtering. Belt-and-braces: only keep rows
        // whose name actually appeared in the registry response.
        var timestampsByName = timestamps
            .Where(t => refNames.Contains(t.Name))
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First().CreatedAt, StringComparer.Ordinal);

        var metadata = refs
            .Select(r => new SecretMetadata(
                r.Name,
                r.Scope,
                timestampsByName.GetValueOrDefault(r.Name, DateTimeOffset.MinValue)))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();

        return Results.Ok(new UnitSecretsListResponse(metadata));
    }

    private static async Task<IResult> CreateUnitSecretAsync(
        string id,
        CreateSecretRequest request,
        [FromServices] IDirectoryService directoryService,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] SpringDbContext db,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Results.Problem(
                detail: "Request body must include a non-empty 'name'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var hasValue = !string.IsNullOrEmpty(request.Value);
        var hasExternal = !string.IsNullOrWhiteSpace(request.ExternalStoreKey);

        if (hasValue == hasExternal)
        {
            return Results.Problem(
                detail: "Request body must include exactly one of 'value' or 'externalStoreKey'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (hasValue && !options.Value.AllowPassThroughWrites)
        {
            return Results.Problem(
                detail: "Pass-through secret writes are disabled (Secrets:AllowPassThroughWrites = false).",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (hasExternal && !options.Value.AllowExternalReferenceWrites)
        {
            return Results.Problem(
                detail: "External-reference secret writes are disabled (Secrets:AllowExternalReferenceWrites = false).",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var unitAddress = new Address("unit", id);
        var entry = await directoryService.ResolveAsync(unitAddress, cancellationToken);
        if (entry is null)
        {
            return Results.Problem(
                detail: $"Unit '{id}' not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        var max = options.Value.MaxSecretsPerOwner;
        if (max > 0)
        {
            var existing = await registry.ListAsync(SecretScope.Unit, id, cancellationToken);
            var alreadyHas = existing.Any(r => string.Equals(r.Name, request.Name, StringComparison.Ordinal));
            if (!alreadyHas && existing.Count >= max)
            {
                return Results.Problem(
                    title: "Too many secrets",
                    detail: $"Unit '{id}' already holds {existing.Count} secrets; limit is {max} (Secrets:MaxSecretsPerOwner).",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
        }

        var secretRef = new SecretRef(SecretScope.Unit, id, request.Name);

        string storeKey;
        if (hasValue)
        {
            // Pass-through write: persist plaintext via the store, then
            // record the structural reference.
            storeKey = await store.WriteAsync(request.Value!, cancellationToken);
        }
        else
        {
            // External reference: we do NOT touch the store, only the
            // registry. The caller is responsible for ensuring the
            // external key is reachable.
            storeKey = request.ExternalStoreKey!;
        }

        try
        {
            await registry.RegisterAsync(secretRef, storeKey, cancellationToken);
        }
        catch
        {
            // Registry write failed after a successful pass-through store
            // write — clean up the orphaned store value before re-throwing
            // so the POST either fully succeeds or leaves no residue. We
            // only clean up values WE just wrote; external references are
            // managed by the caller and must never be touched.
            if (hasValue)
            {
                try
                {
                    await store.DeleteAsync(storeKey, CancellationToken.None);
                }
                catch
                {
                    // Swallow cleanup failure — the registry exception is
                    // the primary error; a reconciliation sweep (see the
                    // rotation/versioning follow-up issue) handles orphans.
                }
            }
            throw;
        }

        var row = await db.SecretRegistryEntries
            .AsNoTracking()
            .Where(e => e.Scope == SecretScope.Unit && e.OwnerId == id && e.Name == request.Name)
            .Select(e => new { e.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var createdAt = row?.CreatedAt ?? DateTimeOffset.UtcNow;

        return Results.Created(
            $"/api/v1/units/{id}/secrets/{request.Name}",
            new CreateSecretResponse(request.Name, SecretScope.Unit, createdAt));
    }

    private static async Task<IResult> DeleteUnitSecretAsync(
        string id,
        string name,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var secretRef = new SecretRef(SecretScope.Unit, id, name);

        var storeKey = await registry.LookupStoreKeyAsync(secretRef, cancellationToken);
        if (storeKey is null)
        {
            return Results.Problem(
                detail: $"Secret '{name}' not found for unit '{id}'",
                statusCode: StatusCodes.Status404NotFound);
        }

        // Ordering: delete the store value FIRST, then the registry row.
        // If the store delete fails we keep the registry row so the
        // operator can retry — the secret stays fully resolvable in the
        // meantime, which is the safe state. Dapr state-store deletes are
        // idempotent against missing keys, so a retry after partial
        // progress is always safe.
        //
        // Caveat to resolve separately (tracked follow-up): we do not yet
        // distinguish platform-owned storeKeys from external references.
        // The private cloud Key Vault impl MUST gate `store.DeleteAsync`
        // on that distinction before it ships — otherwise a DELETE here
        // could destroy a customer-owned Key Vault secret.
        try
        {
            await store.DeleteAsync(storeKey, cancellationToken);
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger("Cvoya.Spring.Host.Api.Endpoints.SecretEndpoints");
            logger.LogError(ex,
                "Store-delete failed for unit secret '{Unit}/{Name}'; registry row retained so operator can retry.",
                id, name);
            return Results.Problem(
                title: "Secret store delete failed",
                detail: $"Underlying store rejected the delete for '{name}'. The secret remains resolvable; retry the DELETE. Underlying error: {ex.Message}",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        try
        {
            await registry.DeleteAsync(secretRef, cancellationToken);
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger("Cvoya.Spring.Host.Api.Endpoints.SecretEndpoints");
            logger.LogError(ex,
                "Registry-delete failed for unit secret '{Unit}/{Name}' after successful store delete; retry will complete.",
                id, name);
            return Results.Problem(
                title: "Secret registry delete failed",
                detail: $"Store value for '{name}' was removed but the registry entry could not be cleared. Retry the DELETE.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.NoContent();
    }
}