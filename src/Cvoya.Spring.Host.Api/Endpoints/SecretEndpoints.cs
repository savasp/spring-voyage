// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Core.Tenancy;
using Cvoya.Spring.Dapr.Data;
using Cvoya.Spring.Dapr.Tenancy;
using Cvoya.Spring.Host.Api.Models;

using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

/// <summary>
/// Maps the secret-CRUD endpoints for all three scopes:
/// unit (<c>/api/v1/units/{id}/secrets</c>), tenant
/// (<c>/api/v1/tenant/secrets</c>), and platform
/// (<c>/api/v1/platform/secrets</c>).
///
/// <para>
/// <b>Security contract.</b> Plaintext enters the system exclusively via
/// the <c>POST</c> body. It is never echoed in any response body, list
/// entry, or log line. The only way to read a plaintext value back out
/// is via server-side <see cref="ISecretResolver.ResolveAsync"/>.
/// </para>
///
/// <para>
/// <b>RBAC.</b> Authorization is delegated to <see cref="ISecretAccessPolicy"/>.
/// The OSS host wires the allow-all default; the private cloud repo
/// registers an implementation that enforces tenant-admin and
/// platform-admin roles. The endpoints do not reference role strings —
/// the extension point is intentionally scope-shaped.
/// </para>
///
/// <para>
/// <b>Origin safety on DELETE.</b> The registry records every entry with a
/// <see cref="SecretOrigin"/>. The DELETE handler calls
/// <see cref="ISecretStore.DeleteAsync"/> only for
/// <see cref="SecretOrigin.PlatformOwned"/> rows. For
/// <see cref="SecretOrigin.ExternalReference"/> rows it removes the
/// registry pointer only, leaving the externally-managed store key
/// untouched — so a DELETE in the private-cloud Key Vault implementation
/// can never destroy a customer-owned secret.
/// </para>
/// </summary>
public static class SecretEndpoints
{
    /// <summary>
    /// The canonical owner id used for <see cref="SecretScope.Platform"/>.
    /// A single constant keeps the registry rows addressable by a
    /// well-known key; the private cloud repo is free to partition
    /// further by layering additional logic on top of
    /// <see cref="ISecretAccessPolicy"/>.
    /// </summary>
    public const string PlatformOwnerId = "platform";

    /// <summary>
    /// Registers the unit-scoped, tenant-scoped, and platform-scoped
    /// secret endpoints on the specified endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The unit-scoped route group builder for chaining.</returns>
    public static RouteGroupBuilder MapSecretEndpoints(this IEndpointRouteBuilder app)
    {
        var unitGroup = app.MapGroup("/api/v1/units/{id}/secrets")
            .WithTags("Secrets");

        unitGroup.MapGet("/", ListUnitSecretsAsync)
            .WithName("ListUnitSecrets")
            .WithSummary("List secret metadata for a unit. Never returns plaintext or store keys.")
            .Produces<UnitSecretsListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        unitGroup.MapPost("/", CreateUnitSecretAsync)
            .WithName("CreateUnitSecret")
            .WithSummary("Register a unit-scoped secret. Provide exactly one of 'value' (pass-through write) or 'externalStoreKey' (bind existing reference).")
            .Produces<CreateSecretResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        unitGroup.MapPut("/{name}", RotateUnitSecretAsync)
            .WithName("RotateUnitSecret")
            .WithSummary("Rotate a unit-scoped secret. Replaces the value/pointer and bumps the version atomically. Provide exactly one of 'value' or 'externalStoreKey'. The entry must already exist — use POST to create.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        unitGroup.MapDelete("/{name}", DeleteUnitSecretAsync)
            .WithName("DeleteUnitSecret")
            .WithSummary("Delete a unit-scoped secret. The underlying plaintext is removed only for platform-owned entries; external references leave the external store key untouched.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        var tenantGroup = app.MapGroup("/api/v1/tenant/secrets")
            .WithTags("Secrets");

        tenantGroup.MapGet("/", ListTenantSecretsAsync)
            .WithName("ListTenantSecrets")
            .WithSummary("List secret metadata for the current tenant. Never returns plaintext or store keys.")
            .Produces<SecretsListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        tenantGroup.MapPost("/", CreateTenantSecretAsync)
            .WithName("CreateTenantSecret")
            .WithSummary("Register a tenant-scoped secret. Provide exactly one of 'value' (pass-through write) or 'externalStoreKey' (bind existing reference).")
            .Produces<CreateSecretResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        tenantGroup.MapPut("/{name}", RotateTenantSecretAsync)
            .WithName("RotateTenantSecret")
            .WithSummary("Rotate a tenant-scoped secret. Replaces the value/pointer and bumps the version atomically. Provide exactly one of 'value' or 'externalStoreKey'. The entry must already exist — use POST to create.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        tenantGroup.MapDelete("/{name}", DeleteTenantSecretAsync)
            .WithName("DeleteTenantSecret")
            .WithSummary("Delete a tenant-scoped secret. The underlying plaintext is removed only for platform-owned entries; external references leave the external store key untouched.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        var platformGroup = app.MapGroup("/api/v1/platform/secrets")
            .WithTags("Secrets");

        platformGroup.MapGet("/", ListPlatformSecretsAsync)
            .WithName("ListPlatformSecrets")
            .WithSummary("List platform-scoped secret metadata. Never returns plaintext or store keys.")
            .Produces<SecretsListResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden);

        platformGroup.MapPost("/", CreatePlatformSecretAsync)
            .WithName("CreatePlatformSecret")
            .WithSummary("Register a platform-scoped secret. Provide exactly one of 'value' (pass-through write) or 'externalStoreKey' (bind existing reference).")
            .Produces<CreateSecretResponse>(StatusCodes.Status201Created)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests);

        platformGroup.MapPut("/{name}", RotatePlatformSecretAsync)
            .WithName("RotatePlatformSecret")
            .WithSummary("Rotate a platform-scoped secret. Replaces the value/pointer and bumps the version atomically. Provide exactly one of 'value' or 'externalStoreKey'. The entry must already exist — use POST to create.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound);

        platformGroup.MapDelete("/{name}", DeletePlatformSecretAsync)
            .WithName("DeletePlatformSecret")
            .WithSummary("Delete a platform-scoped secret. The underlying plaintext is removed only for platform-owned entries; external references leave the external store key untouched.")
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return unitGroup;
    }

    // ------------------------------------------------------------------
    // Unit-scoped handlers
    // ------------------------------------------------------------------

    private static async Task<IResult> ListUnitSecretsAsync(
        string id,
        [FromServices] IDirectoryService directoryService,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] SpringDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.List, SecretScope.Unit, id, cancellationToken))
        {
            return Forbidden(SecretScope.Unit, SecretAccessAction.List);
        }

        var unitAddress = new Address("unit", id);
        var entry = await directoryService.ResolveAsync(unitAddress, cancellationToken);
        if (entry is null)
        {
            return Results.Problem(
                detail: $"Unit '{id}' not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        var metadata = await ListMetadataAsync(registry, db, SecretScope.Unit, id, cancellationToken);
        return Results.Ok(new UnitSecretsListResponse(metadata));
    }

    private static async Task<IResult> CreateUnitSecretAsync(
        string id,
        CreateSecretRequest request,
        [FromServices] IDirectoryService directoryService,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] SpringDbContext db,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Create, SecretScope.Unit, id, cancellationToken))
        {
            return Forbidden(SecretScope.Unit, SecretAccessAction.Create);
        }

        var validationError = ValidateCreateRequest(request, options.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        var unitAddress = new Address("unit", id);
        var entry = await directoryService.ResolveAsync(unitAddress, cancellationToken);
        if (entry is null)
        {
            return Results.Problem(
                detail: $"Unit '{id}' not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        return await CreateSecretAsync(
            SecretScope.Unit, id, request, store, registry, db, options.Value, cancellationToken);
    }

    private static async Task<IResult> RotateUnitSecretAsync(
        string id,
        string name,
        RotateSecretRequest request,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Rotate, SecretScope.Unit, id, cancellationToken))
        {
            return Forbidden(SecretScope.Unit, SecretAccessAction.Rotate);
        }

        return await RotateSecretAsync(
            SecretScope.Unit, id, name, request, store, registry, options.Value, cancellationToken);
    }

    private static async Task<IResult> DeleteUnitSecretAsync(
        string id,
        string name,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Delete, SecretScope.Unit, id, cancellationToken))
        {
            return Forbidden(SecretScope.Unit, SecretAccessAction.Delete);
        }

        return await DeleteSecretAsync(
            SecretScope.Unit, id, name, store, registry, loggerFactory, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Tenant-scoped handlers
    // ------------------------------------------------------------------

    private static async Task<IResult> ListTenantSecretsAsync(
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] ITenantContext tenantContext,
        [FromServices] SpringDbContext db,
        CancellationToken cancellationToken)
    {
        var ownerId = tenantContext.CurrentTenantId;
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.List, SecretScope.Tenant, ownerId, cancellationToken))
        {
            return Forbidden(SecretScope.Tenant, SecretAccessAction.List);
        }

        var metadata = await ListMetadataAsync(registry, db, SecretScope.Tenant, ownerId, cancellationToken);
        return Results.Ok(new SecretsListResponse(metadata));
    }

    private static async Task<IResult> CreateTenantSecretAsync(
        CreateSecretRequest request,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] ITenantContext tenantContext,
        [FromServices] SpringDbContext db,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        var ownerId = tenantContext.CurrentTenantId;
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Create, SecretScope.Tenant, ownerId, cancellationToken))
        {
            return Forbidden(SecretScope.Tenant, SecretAccessAction.Create);
        }

        var validationError = ValidateCreateRequest(request, options.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        return await CreateSecretAsync(
            SecretScope.Tenant, ownerId, request, store, registry, db, options.Value, cancellationToken);
    }

    private static async Task<IResult> RotateTenantSecretAsync(
        string name,
        RotateSecretRequest request,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] ITenantContext tenantContext,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        var ownerId = tenantContext.CurrentTenantId;
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Rotate, SecretScope.Tenant, ownerId, cancellationToken))
        {
            return Forbidden(SecretScope.Tenant, SecretAccessAction.Rotate);
        }

        return await RotateSecretAsync(
            SecretScope.Tenant, ownerId, name, request, store, registry, options.Value, cancellationToken);
    }

    private static async Task<IResult> DeleteTenantSecretAsync(
        string name,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] ITenantContext tenantContext,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var ownerId = tenantContext.CurrentTenantId;
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Delete, SecretScope.Tenant, ownerId, cancellationToken))
        {
            return Forbidden(SecretScope.Tenant, SecretAccessAction.Delete);
        }

        return await DeleteSecretAsync(
            SecretScope.Tenant, ownerId, name, store, registry, loggerFactory, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Platform-scoped handlers
    // ------------------------------------------------------------------

    private static async Task<IResult> ListPlatformSecretsAsync(
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] SpringDbContext db,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.List, SecretScope.Platform, PlatformOwnerId, cancellationToken))
        {
            return Forbidden(SecretScope.Platform, SecretAccessAction.List);
        }

        var metadata = await ListMetadataAsync(registry, db, SecretScope.Platform, PlatformOwnerId, cancellationToken);
        return Results.Ok(new SecretsListResponse(metadata));
    }

    private static async Task<IResult> CreatePlatformSecretAsync(
        CreateSecretRequest request,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] SpringDbContext db,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Create, SecretScope.Platform, PlatformOwnerId, cancellationToken))
        {
            return Forbidden(SecretScope.Platform, SecretAccessAction.Create);
        }

        var validationError = ValidateCreateRequest(request, options.Value);
        if (validationError is not null)
        {
            return validationError;
        }

        return await CreateSecretAsync(
            SecretScope.Platform, PlatformOwnerId, request, store, registry, db, options.Value, cancellationToken);
    }

    private static async Task<IResult> RotatePlatformSecretAsync(
        string name,
        RotateSecretRequest request,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] IOptions<SecretsOptions> options,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Rotate, SecretScope.Platform, PlatformOwnerId, cancellationToken))
        {
            return Forbidden(SecretScope.Platform, SecretAccessAction.Rotate);
        }

        return await RotateSecretAsync(
            SecretScope.Platform, PlatformOwnerId, name, request, store, registry, options.Value, cancellationToken);
    }

    private static async Task<IResult> DeletePlatformSecretAsync(
        string name,
        [FromServices] ISecretStore store,
        [FromServices] ISecretRegistry registry,
        [FromServices] ISecretAccessPolicy accessPolicy,
        [FromServices] ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        if (!await accessPolicy.IsAuthorizedAsync(SecretAccessAction.Delete, SecretScope.Platform, PlatformOwnerId, cancellationToken))
        {
            return Forbidden(SecretScope.Platform, SecretAccessAction.Delete);
        }

        return await DeleteSecretAsync(
            SecretScope.Platform, PlatformOwnerId, name, store, registry, loggerFactory, cancellationToken);
    }

    // ------------------------------------------------------------------
    // Shared helpers
    // ------------------------------------------------------------------

    private static IResult? ValidateCreateRequest(CreateSecretRequest request, SecretsOptions options)
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

        if (hasValue && !options.AllowPassThroughWrites)
        {
            return Results.Problem(
                detail: "Pass-through secret writes are disabled (Secrets:AllowPassThroughWrites = false).",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (hasExternal && !options.AllowExternalReferenceWrites)
        {
            return Results.Problem(
                detail: "External-reference secret writes are disabled (Secrets:AllowExternalReferenceWrites = false).",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private static IResult? ValidateRotateRequest(RotateSecretRequest request, SecretsOptions options)
    {
        // Mirrors ValidateCreateRequest but over the rotation shape (no
        // 'name' field — the name is route-bound). The same two config
        // flags gate rotation; rotating onto a disabled write mode is
        // as much a policy violation as creating in that mode.
        var hasValue = !string.IsNullOrEmpty(request.Value);
        var hasExternal = !string.IsNullOrWhiteSpace(request.ExternalStoreKey);

        if (hasValue == hasExternal)
        {
            return Results.Problem(
                detail: "Request body must include exactly one of 'value' or 'externalStoreKey'.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (hasValue && !options.AllowPassThroughWrites)
        {
            return Results.Problem(
                detail: "Pass-through secret writes are disabled (Secrets:AllowPassThroughWrites = false).",
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (hasExternal && !options.AllowExternalReferenceWrites)
        {
            return Results.Problem(
                detail: "External-reference secret writes are disabled (Secrets:AllowExternalReferenceWrites = false).",
                statusCode: StatusCodes.Status403Forbidden);
        }

        return null;
    }

    private static async Task<IResult> RotateSecretAsync(
        SecretScope scope,
        string ownerId,
        string name,
        RotateSecretRequest request,
        ISecretStore store,
        ISecretRegistry registry,
        SecretsOptions options,
        CancellationToken cancellationToken)
    {
        var validationError = ValidateRotateRequest(request, options);
        if (validationError is not null)
        {
            return validationError;
        }

        var secretRef = new SecretRef(scope, ownerId, name);

        // Pre-flight check: rotate requires an existing entry. We look
        // up explicitly so we can return 404 with a ProblemDetails body
        // — the registry throws InvalidOperationException otherwise.
        var pointer = await registry.LookupAsync(secretRef, cancellationToken);
        if (pointer is null)
        {
            return Results.Problem(
                detail: $"Secret '{name}' not found for {scope} '{ownerId}'. Use POST to create.",
                statusCode: StatusCodes.Status404NotFound);
        }

        var hasValue = !string.IsNullOrEmpty(request.Value);
        string newStoreKey;
        SecretOrigin newOrigin;
        if (hasValue)
        {
            // Write the fresh plaintext first; the registry swap happens
            // transactionally inside RotateAsync. If the registry call
            // then fails, we clean up the orphaned new slot — symmetric
            // with CreateSecretAsync.
            newStoreKey = await store.WriteAsync(request.Value!, cancellationToken);
            newOrigin = SecretOrigin.PlatformOwned;
        }
        else
        {
            newStoreKey = request.ExternalStoreKey!;
            newOrigin = SecretOrigin.ExternalReference;
        }

        try
        {
            // RotateAsync handles the origin-safe old-slot cleanup via
            // the delegate we pass in. We give it ISecretStore.DeleteAsync
            // only if the previous pointer was platform-owned — if the
            // previous pointer was external we never hand the delegate
            // a key, so the registry's internal gate doubles as our
            // outer gate. The registry enforces "platform-owned-only
            // invokes the delegate" regardless of what we pass.
            await registry.RotateAsync(
                secretRef,
                newStoreKey,
                newOrigin,
                async (oldKey, ct) => await store.DeleteAsync(oldKey, ct),
                cancellationToken);
        }
        catch (InvalidOperationException)
        {
            // Race between the LookupAsync pre-flight and the rotate —
            // the entry was removed concurrently. Clean up the orphaned
            // new slot (if we wrote one) and surface 404.
            if (newOrigin == SecretOrigin.PlatformOwned)
            {
                try
                {
                    await store.DeleteAsync(newStoreKey, CancellationToken.None);
                }
                catch
                {
                    // Swallow — reconciliation sweep handles orphans.
                }
            }
            return Results.Problem(
                detail: $"Secret '{name}' not found for {scope} '{ownerId}'. Use POST to create.",
                statusCode: StatusCodes.Status404NotFound);
        }
        catch
        {
            // Registry-side failure after a successful pass-through
            // write — orphaned slot cleanup mirrors CreateSecretAsync.
            if (newOrigin == SecretOrigin.PlatformOwned)
            {
                try
                {
                    await store.DeleteAsync(newStoreKey, CancellationToken.None);
                }
                catch
                {
                    // Reconciliation handles orphans.
                }
            }
            throw;
        }

        return Results.NoContent();
    }

    private static async Task<IResult> CreateSecretAsync(
        SecretScope scope,
        string ownerId,
        CreateSecretRequest request,
        ISecretStore store,
        ISecretRegistry registry,
        SpringDbContext db,
        SecretsOptions options,
        CancellationToken cancellationToken)
    {
        var max = options.MaxSecretsPerOwner;
        if (max > 0)
        {
            var existing = await registry.ListAsync(scope, ownerId, cancellationToken);
            var alreadyHas = existing.Any(r => string.Equals(r.Name, request.Name, StringComparison.Ordinal));
            if (!alreadyHas && existing.Count >= max)
            {
                return Results.Problem(
                    title: "Too many secrets",
                    detail: $"{scope} owner '{ownerId}' already holds {existing.Count} secrets; limit is {max} (Secrets:MaxSecretsPerOwner).",
                    statusCode: StatusCodes.Status429TooManyRequests);
            }
        }

        var hasValue = !string.IsNullOrEmpty(request.Value);
        var secretRef = new SecretRef(scope, ownerId, request.Name);

        string storeKey;
        SecretOrigin origin;
        if (hasValue)
        {
            // Pass-through write: persist plaintext via the store, then
            // record the structural reference. The entry's origin marks
            // the platform as the owner of the resulting slot, enabling
            // store-layer delete/rotate on later mutations.
            storeKey = await store.WriteAsync(request.Value!, cancellationToken);
            origin = SecretOrigin.PlatformOwned;
        }
        else
        {
            // External reference: we do NOT touch the store, only the
            // registry. The origin marks the slot as caller-owned so the
            // store-delete path skips it, preventing data loss on keys
            // the platform never wrote.
            storeKey = request.ExternalStoreKey!;
            origin = SecretOrigin.ExternalReference;
        }

        try
        {
            await registry.RegisterAsync(secretRef, storeKey, origin, cancellationToken);
        }
        catch
        {
            // Registry write failed after a successful pass-through store
            // write — clean up the orphaned store value before re-throwing
            // so the POST either fully succeeds or leaves no residue. We
            // only clean up values WE just wrote; external references are
            // managed by the caller and must never be touched.
            if (origin == SecretOrigin.PlatformOwned)
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
            .Where(e => e.Scope == scope && e.OwnerId == ownerId && e.Name == request.Name)
            .Select(e => new { e.CreatedAt })
            .FirstOrDefaultAsync(cancellationToken);

        var createdAt = row?.CreatedAt ?? DateTimeOffset.UtcNow;
        var location = BuildResourceLocation(scope, ownerId, request.Name);
        return Results.Created(
            location,
            new CreateSecretResponse(request.Name, scope, createdAt));
    }

    private static async Task<IResult> DeleteSecretAsync(
        SecretScope scope,
        string ownerId,
        string name,
        ISecretStore store,
        ISecretRegistry registry,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var secretRef = new SecretRef(scope, ownerId, name);

        var pointer = await registry.LookupAsync(secretRef, cancellationToken);
        if (pointer is null)
        {
            return Results.Problem(
                detail: $"Secret '{name}' not found for {scope} '{ownerId}'",
                statusCode: StatusCodes.Status404NotFound);
        }

        // ORIGIN GATE: only touch the store when the platform owns the
        // underlying slot. For ExternalReference entries we remove the
        // registry row and leave the external store key untouched — that
        // is the whole reason the origin field exists. In OSS / Dapr the
        // distinction is cosmetic; in the private-cloud Key Vault impl it
        // is the guardrail that prevents DELETE from destroying
        // customer-owned secrets.
        if (pointer.Origin == SecretOrigin.PlatformOwned)
        {
            // Ordering: delete the store value FIRST, then the registry row.
            // If the store delete fails we keep the registry row so the
            // operator can retry — the secret stays fully resolvable in
            // the meantime, which is the safe state. Dapr state-store
            // deletes are idempotent against missing keys, so a retry
            // after partial progress is always safe.
            try
            {
                await store.DeleteAsync(pointer.StoreKey, cancellationToken);
            }
            catch (Exception ex)
            {
                var logger = loggerFactory.CreateLogger("Cvoya.Spring.Host.Api.Endpoints.SecretEndpoints");
                logger.LogError(ex,
                    "Store-delete failed for {Scope} secret '{Owner}/{Name}'; registry row retained so operator can retry.",
                    scope, ownerId, name);
                return Results.Problem(
                    title: "Secret store delete failed",
                    detail: $"Underlying store rejected the delete for '{name}'. The secret remains resolvable; retry the DELETE. Underlying error: {ex.Message}",
                    statusCode: StatusCodes.Status500InternalServerError);
            }
        }

        try
        {
            await registry.DeleteAsync(secretRef, cancellationToken);
        }
        catch (Exception ex)
        {
            var logger = loggerFactory.CreateLogger("Cvoya.Spring.Host.Api.Endpoints.SecretEndpoints");
            logger.LogError(ex,
                "Registry-delete failed for {Scope} secret '{Owner}/{Name}' after successful store delete; retry will complete.",
                scope, ownerId, name);
            return Results.Problem(
                title: "Secret registry delete failed",
                detail: $"Store value for '{name}' was removed but the registry entry could not be cleared. Retry the DELETE.",
                statusCode: StatusCodes.Status500InternalServerError);
        }

        return Results.NoContent();
    }

    private static async Task<IReadOnlyList<SecretMetadata>> ListMetadataAsync(
        ISecretRegistry registry,
        SpringDbContext db,
        SecretScope scope,
        string ownerId,
        CancellationToken cancellationToken)
    {
        // The ISecretRegistry.ListAsync surface returns SecretRefs only.
        // The UI wants the createdAt timestamp too, so project directly
        // off the tracked entity. ITenantContext is applied inside the
        // registry, and we re-filter by name after the DbContext query
        // to keep the code path symmetric with tenant-filtered lookups.
        var refs = await registry.ListAsync(scope, ownerId, cancellationToken);

        var refNames = refs.Select(r => r.Name).ToHashSet(StringComparer.Ordinal);
        var timestamps = await db.SecretRegistryEntries
            .AsNoTracking()
            .Where(e => e.Scope == scope && e.OwnerId == ownerId)
            .Select(e => new { e.Name, e.CreatedAt })
            .ToListAsync(cancellationToken);

        var timestampsByName = timestamps
            .Where(t => refNames.Contains(t.Name))
            .GroupBy(t => t.Name)
            .ToDictionary(g => g.Key, g => g.First().CreatedAt, StringComparer.Ordinal);

        return refs
            .Select(r => new SecretMetadata(
                r.Name,
                r.Scope,
                timestampsByName.GetValueOrDefault(r.Name, DateTimeOffset.MinValue)))
            .OrderBy(m => m.Name, StringComparer.Ordinal)
            .ToList();
    }

    private static string BuildResourceLocation(SecretScope scope, string ownerId, string name) => scope switch
    {
        SecretScope.Unit => $"/api/v1/units/{ownerId}/secrets/{name}",
        SecretScope.Tenant => $"/api/v1/tenant/secrets/{name}",
        SecretScope.Platform => $"/api/v1/platform/secrets/{name}",
        _ => $"/api/v1/secrets/{name}",
    };

    private static IResult Forbidden(SecretScope scope, SecretAccessAction action) =>
        Results.Problem(
            title: "Forbidden",
            detail: $"Not authorized to {action} secrets in scope '{scope}'.",
            statusCode: StatusCodes.Status403Forbidden);
}