// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Models;

using Cvoya.Spring.Core.Secrets;

/// <summary>
/// Metadata entry returned by secret-listing endpoints. Never contains
/// the plaintext value or the underlying store key — plaintext only
/// enters the system via <see cref="CreateSecretRequest"/> and leaves
/// exclusively through server-side <see cref="ISecretResolver.ResolveAsync"/>.
/// </summary>
/// <param name="Name">The secret name.</param>
/// <param name="Scope">The secret scope.</param>
/// <param name="CreatedAt">Registration timestamp.</param>
public record SecretMetadata(
    string Name,
    SecretScope Scope,
    DateTimeOffset CreatedAt);

/// <summary>
/// Response body for GET <c>/api/v1/units/{id}/secrets</c>.
/// </summary>
/// <param name="Secrets">The metadata entries for this unit.</param>
public record UnitSecretsListResponse(IReadOnlyList<SecretMetadata> Secrets);

/// <summary>
/// Response body for scope-keyed secret listing endpoints
/// (<c>GET /api/v1/tenant/secrets</c>, <c>GET /api/v1/platform/secrets</c>).
/// Mirrors <see cref="UnitSecretsListResponse"/> — same contract, the two
/// shapes only differ in name to keep the unit-scoped response stable.
/// </summary>
/// <param name="Secrets">The metadata entries for the scope/owner.</param>
public record SecretsListResponse(IReadOnlyList<SecretMetadata> Secrets);

/// <summary>
/// Request body for POST <c>/api/v1/units/{id}/secrets</c>. Exactly one
/// of <paramref name="Value"/> or <paramref name="ExternalStoreKey"/>
/// must be provided — "pass-through" write vs "external reference"
/// binding. Supplying both or neither yields 400 Bad Request.
/// </summary>
/// <param name="Name">The secret name (case-sensitive).</param>
/// <param name="Value">
/// Plaintext to store via <see cref="ISecretStore.WriteAsync"/>. Leaves
/// the HTTP layer exactly once (on this request) and is never echoed in
/// any response body or log.
/// </param>
/// <param name="ExternalStoreKey">
/// An externally-managed store key (e.g. a Key Vault secret id) to bind
/// this secret name to. No plaintext is written by the server when this
/// mode is used.
/// </param>
public record CreateSecretRequest(
    string Name,
    string? Value = null,
    string? ExternalStoreKey = null);

/// <summary>
/// Response body returned by the pass-through and external-reference
/// write endpoints. Mirrors <see cref="SecretMetadata"/>; the value /
/// store key are never returned.
/// </summary>
/// <param name="Name">The secret name.</param>
/// <param name="Scope">The secret scope.</param>
/// <param name="CreatedAt">Registration timestamp.</param>
public record CreateSecretResponse(
    string Name,
    SecretScope Scope,
    DateTimeOffset CreatedAt);