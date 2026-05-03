// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Services;

using Cvoya.Spring.Core.Security;
using Cvoya.Spring.Dapr.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

/// <summary>
/// Default scoped implementation of <see cref="IParticipantDisplayNameResolver"/>.
/// Looks up display names from <c>AgentDefinitions</c> and <c>UnitDefinitions</c>
/// tables for the <c>agent://</c> and <c>unit://</c> schemes respectively.
/// For <c>human:id:&lt;uuid&gt;</c> (identity form, post-#1491) the UUID is
/// resolved to a display name via <see cref="IHumanIdentityResolver.GetDisplayNameAsync"/>;
/// for <c>human://&lt;username&gt;</c> (legacy navigation form) the username
/// path is returned as-is.
///
/// Results are cached in a per-request dictionary so repeated calls for the
/// same address (e.g. a single human appearing as the <c>Human</c> on multiple
/// inbox rows) issue at most one database round-trip.
/// </summary>
internal sealed class ParticipantDisplayNameResolver(
    SpringDbContext db,
    IHumanIdentityResolver humanIdentityResolver,
    ILogger<ParticipantDisplayNameResolver> logger)
    : IParticipantDisplayNameResolver
{
    private readonly Dictionary<string, string> _cache = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public async ValueTask<string> ResolveAsync(
        string address,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return address;
        }

        if (_cache.TryGetValue(address, out var cached))
        {
            return cached;
        }

        var result = await ResolveInternalAsync(address, cancellationToken);
        _cache[address] = result;
        return result;
    }

    private async Task<string> ResolveInternalAsync(
        string address,
        CancellationToken cancellationToken)
    {
        // Check for identity form "scheme:id:<uuid>" first (no "://" separator).
        var idIdx = address.IndexOf(":id:", StringComparison.Ordinal);
        if (idIdx > 0)
        {
            var scheme = address[..idIdx];
            var uuidStr = address[(idIdx + 4)..];

            if (string.Equals(scheme, "human", StringComparison.OrdinalIgnoreCase)
                && Guid.TryParse(uuidStr, out var humanId))
            {
                try
                {
                    var displayName = await humanIdentityResolver.GetDisplayNameAsync(humanId, cancellationToken);
                    if (displayName is not null)
                    {
                        return displayName;
                    }
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Failed to resolve display name for human {HumanId}; falling back to UUID.",
                        humanId);
                }

                // Fall back to the UUID string when no display name is available.
                return uuidStr;
            }

            // Agent / unit identity form: NormaliseSource emits "agent:id:<uuid>"
            // / "unit:id:<uuid>" whenever the activity event was persisted with
            // the actor UUID as the source — which is the common case. Look up
            // the entity by ActorId (the same UUID) so the thread surfaces show
            // the agent / unit name instead of a raw UUID (#1545, #1547, #1548).
            if (!Cvoya.Spring.Core.Identifiers.GuidFormatter.TryParse(uuidStr, out var idGuid))
            {
                return uuidStr;
            }

            if (string.Equals(scheme, "agent", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var name = await db.AgentDefinitions
                        .AsNoTracking()
                        .Where(a => a.Id == idGuid && a.DeletedAt == null)
                        .Select(a => a.DisplayName)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }

                    logger.LogDebug(
                        "No agent definition found for actor id {ActorId}; falling back to UUID.",
                        uuidStr);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Failed to resolve display name for agent actor id {ActorId}; falling back to UUID.",
                        uuidStr);
                }

                return uuidStr;
            }

            if (string.Equals(scheme, "unit", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var name = await db.UnitDefinitions
                        .AsNoTracking()
                        .Where(u => u.Id == idGuid && u.DeletedAt == null)
                        .Select(u => u.DisplayName)
                        .FirstOrDefaultAsync(cancellationToken);

                    if (!string.IsNullOrWhiteSpace(name))
                    {
                        return name;
                    }

                    logger.LogDebug(
                        "No unit definition found for actor id {ActorId}; falling back to UUID.",
                        uuidStr);
                }
                catch (Exception ex)
                {
                    logger.LogDebug(
                        ex,
                        "Failed to resolve display name for unit actor id {ActorId}; falling back to UUID.",
                        uuidStr);
                }

                return uuidStr;
            }

            // Unknown identity-form scheme — fall back to the uuid portion.
            return uuidStr;
        }

        var separatorIdx = address.IndexOf("://", StringComparison.Ordinal);
        string schemeNav;
        string path;

        if (separatorIdx > 0)
        {
            schemeNav = address[..separatorIdx];
            path = address[(separatorIdx + 3)..];
        }
        else
        {
            // No scheme separator — return as-is (defensive).
            return address;
        }

        if (string.IsNullOrWhiteSpace(path))
        {
            return address;
        }

        try
        {
            // Path may already be a Guid hex form (post-#1629). Parse and
            // resolve via the Guid id.
            if (!Cvoya.Spring.Core.Identifiers.GuidFormatter.TryParse(path, out var pathGuid))
            {
                return path;
            }

            if (string.Equals(schemeNav, "agent", StringComparison.OrdinalIgnoreCase))
            {
                var name = await db.AgentDefinitions
                    .AsNoTracking()
                    .Where(a => a.Id == pathGuid && a.DeletedAt == null)
                    .Select(a => a.DisplayName)
                    .FirstOrDefaultAsync(cancellationToken);

                if (name is not null)
                {
                    return name;
                }

                logger.LogDebug(
                    "No agent definition found for {AgentId}; falling back to path.",
                    path);
                return path;
            }

            if (string.Equals(schemeNav, "unit", StringComparison.OrdinalIgnoreCase))
            {
                var name = await db.UnitDefinitions
                    .AsNoTracking()
                    .Where(u => u.Id == pathGuid && u.DeletedAt == null)
                    .Select(u => u.DisplayName)
                    .FirstOrDefaultAsync(cancellationToken);

                if (name is not null)
                {
                    return name;
                }

                logger.LogDebug(
                    "No unit definition found for {UnitId}; falling back to path.",
                    path);
                return path;
            }

            // For human:// (legacy navigation form) and any other scheme,
            // return the path component as-is. The human's display name is
            // now stored in the humans table, but the legacy navigation form
            // only carries the username slug — callers that hold the identity
            // form get full resolution above.
            return path;
        }
        catch (Exception ex)
        {
            logger.LogDebug(
                ex,
                "Failed to resolve display name for {Address}; falling back to path.",
                address);
            return path;
        }
    }
}