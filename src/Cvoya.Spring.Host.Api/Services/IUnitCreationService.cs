// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Services;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Cvoya.Spring.Host.Api.Models;

/// <summary>
/// Centralised unit-creation pipeline shared by <c>POST /api/v1/units</c>,
/// <c>POST /api/v1/units/from-yaml</c>, and <c>POST /api/v1/units/from-template</c>.
/// Keeping the actor-create + directory-register + member-wiring logic in a single
/// place avoids duplicating it across three endpoints and gives downstream consumers
/// (e.g. the private cloud repo) a single extension point to wrap.
/// </summary>
public interface IUnitCreationService
{
    /// <summary>
    /// Creates a unit from the caller-supplied fields. No members are added —
    /// callers wire members up through the existing member endpoints.
    /// </summary>
    Task<UnitCreationResult> CreateAsync(
        CreateUnitRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Creates a unit from a parsed unit manifest, forwarding members declared
    /// in the manifest to the unit actor. Warnings for unsupported manifest
    /// sections are surfaced through <see cref="UnitCreationResult.Warnings"/>.
    /// </summary>
    Task<UnitCreationResult> CreateFromManifestAsync(
        Manifest.UnitManifest manifest,
        UnitCreationOverrides overrides,
        CancellationToken cancellationToken);
}

/// <summary>
/// Optional caller-supplied overrides applied on top of values derived from a
/// manifest (YAML import or template). Each field is optional; <c>null</c> means
/// "use the manifest value".
/// </summary>
/// <param name="DisplayName">Override for the unit's display name.</param>
/// <param name="Color">Override for the unit's UI colour.</param>
/// <param name="Model">Override for the unit's default model hint.</param>
public record UnitCreationOverrides(
    string? DisplayName = null,
    string? Color = null,
    string? Model = null);

/// <summary>
/// Outcome of a unit-creation call.
/// </summary>
/// <param name="Unit">The created unit's canonical response projection.</param>
/// <param name="Warnings">
/// Non-fatal warnings collected during creation (e.g. manifest sections parsed
/// but not yet applied, members skipped because their address was unresolvable).
/// Always non-null; empty when no warnings were produced.
/// </param>
/// <param name="MembersAdded">Number of members successfully added.</param>
public record UnitCreationResult(
    UnitResponse Unit,
    IReadOnlyList<string> Warnings,
    int MembersAdded);