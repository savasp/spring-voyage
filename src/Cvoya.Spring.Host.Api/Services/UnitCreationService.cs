// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Services;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Actors;
using Cvoya.Spring.Dapr.Routing;
using Cvoya.Spring.Host.Api.Models;
using Cvoya.Spring.Manifest;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;

/// <summary>
/// Default <see cref="IUnitCreationService"/> implementation.
///
/// The raw ingredients (directory register + actor metadata + member routing)
/// are identical to what <see cref="Endpoints.UnitEndpoints.CreateUnitAsync"/>
/// and <see cref="Endpoints.UnitEndpoints.AddMemberAsync"/> used to do inline;
/// this service just packages them so the three create endpoints share a path.
/// </summary>
public class UnitCreationService(
    IDirectoryService directoryService,
    IActorProxyFactory actorProxyFactory,
    MessageRouter messageRouter)
    : IUnitCreationService
{
    /// <inheritdoc />
    public Task<UnitCreationResult> CreateAsync(
        CreateUnitRequest request,
        CancellationToken cancellationToken) =>
        CreateCoreAsync(
            name: request.Name,
            displayName: request.DisplayName,
            description: request.Description,
            model: request.Model,
            color: request.Color,
            members: Array.Empty<MemberManifest>(),
            warnings: new List<string>(),
            cancellationToken);

    /// <inheritdoc />
    public Task<UnitCreationResult> CreateFromManifestAsync(
        UnitManifest manifest,
        UnitCreationOverrides overrides,
        CancellationToken cancellationToken)
    {
        var name = manifest.Name!;
        var displayName = !string.IsNullOrWhiteSpace(overrides.DisplayName)
            ? overrides.DisplayName!
            : name;
        var description = manifest.Description ?? string.Empty;
        var model = overrides.Model
            ?? manifest.Ai?.Model;
        var color = overrides.Color;

        var warnings = new List<string>();
        foreach (var section in ManifestParser.CollectUnsupportedSections(manifest))
        {
            warnings.Add(
                $"section '{section}' is parsed but not yet applied");
        }

        return CreateCoreAsync(
            name,
            displayName,
            description,
            model,
            color,
            manifest.Members ?? new List<MemberManifest>(),
            warnings,
            cancellationToken);
    }

    private async Task<UnitCreationResult> CreateCoreAsync(
        string name,
        string displayName,
        string description,
        string? model,
        string? color,
        IReadOnlyList<MemberManifest> members,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        var actorId = Guid.NewGuid().ToString();
        var address = new Address("unit", name);
        var entry = new DirectoryEntry(
            address,
            actorId,
            displayName,
            description,
            null,
            DateTimeOffset.UtcNow);

        await directoryService.RegisterAsync(entry, cancellationToken);

        // DisplayName/Description live on the directory entity; only forward
        // the actor-owned fields (Model, Color) to the metadata write to avoid
        // a double-write — mirrors UnitEndpoints.CreateUnitAsync.
        var metadata = new UnitMetadata(
            DisplayName: null,
            Description: null,
            Model: model,
            Color: color);

        if (metadata.Model is not null || metadata.Color is not null)
        {
            var proxy = actorProxyFactory.CreateActorProxy<IUnitActor>(
                new ActorId(actorId), nameof(IUnitActor));
            await proxy.SetMetadataAsync(metadata, cancellationToken);
        }

        var membersAdded = 0;
        foreach (var member in members)
        {
            var resolved = ResolveMemberAddress(member);
            if (resolved is null)
            {
                warnings.Add("member entry had no 'agent' or 'unit' field; skipped");
                continue;
            }

            var payload = JsonSerializer.SerializeToElement(new
            {
                Action = "AddMember",
                MemberScheme = resolved.Value.Scheme,
                MemberPath = resolved.Value.Path,
            });

            var message = new Message(
                Guid.NewGuid(),
                new Address("human", "api"),
                address,
                MessageType.Domain,
                null,
                payload,
                DateTimeOffset.UtcNow);

            var result = await messageRouter.RouteAsync(message, cancellationToken);
            if (!result.IsSuccess)
            {
                warnings.Add(
                    $"failed to add member {resolved.Value.Scheme}:{resolved.Value.Path}: {result.Error!.Message}");
                continue;
            }
            membersAdded++;
        }

        var response = new UnitResponse(
            entry.ActorId,
            entry.Address.Path,
            entry.DisplayName,
            entry.Description,
            entry.RegisteredAt,
            UnitStatus.Draft,
            metadata.Model,
            metadata.Color);

        return new UnitCreationResult(response, warnings, membersAdded);
    }

    private static (string Scheme, string Path)? ResolveMemberAddress(MemberManifest member)
    {
        if (!string.IsNullOrWhiteSpace(member.Agent))
        {
            return ("agent", member.Agent!);
        }
        if (!string.IsNullOrWhiteSpace(member.Unit))
        {
            return ("unit", member.Unit!);
        }
        return null;
    }
}