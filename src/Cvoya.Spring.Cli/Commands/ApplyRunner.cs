// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Commands;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Cvoya.Spring.Manifest;

/// <summary>
/// Parses a unit manifest YAML file and applies it against a <see cref="SpringApiClient"/>.
/// Thin wrapper over <see cref="ManifestParser"/> so the parse + apply logic can be unit
/// tested without going through <c>System.CommandLine</c>. The parser itself now lives
/// in <c>Cvoya.Spring.Manifest</c> so the API host can reuse it from the
/// <c>/units/from-yaml</c> and <c>/units/from-template</c> endpoints.
/// </summary>
public static class ApplyRunner
{
    /// <summary>
    /// Sections of the unit manifest grammar that are parsed but not yet wired up
    /// through <see cref="SpringApiClient"/>. Exposed via the shared parser so the
    /// CLI and API emit the same warning vocabulary.
    /// </summary>
    internal static IReadOnlyList<string> UnsupportedSections => ManifestParser.UnsupportedSections;

    /// <summary>
    /// Parses the manifest YAML text. Delegates to <see cref="ManifestParser.Parse"/>.
    /// </summary>
    public static UnitManifest Parse(string yamlText) => ManifestParser.Parse(yamlText);

    /// <summary>
    /// Parses the manifest at <paramref name="filePath"/>. Delegates to
    /// <see cref="ManifestParser.ParseFile"/>.
    /// </summary>
    public static UnitManifest ParseFile(string filePath) => ManifestParser.ParseFile(filePath);

    /// <summary>
    /// Applies a parsed manifest through <paramref name="client"/>.
    /// Writes progress to <paramref name="stdout"/> and errors to <paramref name="stderr"/>.
    /// Returns a non-zero exit code if the API rejects any call.
    /// </summary>
    public static async Task<int> ApplyAsync(
        UnitManifest manifest,
        SpringApiClient client,
        TextWriter stdout,
        TextWriter stderr,
        CancellationToken ct = default)
    {
        var unitName = manifest.Name!;

        stdout.WriteLine($"[apply] creating unit '{unitName}'...");
        try
        {
            // Forward description from the manifest; display name defaults to the unit name
            // when no separate display name is declared in the manifest today.
            await client.CreateUnitAsync(unitName, unitName, manifest.Description, ct);
        }
        catch (System.Exception ex)
        {
            stderr.WriteLine($"[error] failed to create unit '{unitName}': {ex.Message}");
            return 1;
        }

        var createdMembers = 0;
        foreach (var member in manifest.Members ?? new List<MemberManifest>())
        {
            var address = ResolveMemberAddress(member);
            if (address is null)
            {
                stdout.WriteLine("[warn] member entry has no 'agent' or 'unit' field; skipping");
                continue;
            }

            try
            {
                await client.AddMemberAsync(unitName, address.Value.Scheme, address.Value.Path, ct);
                stdout.WriteLine($"[apply] added member {address.Value.Scheme}:{address.Value.Path}");
                createdMembers++;
            }
            catch (System.Exception ex)
            {
                stderr.WriteLine(
                    $"[error] failed to add member {address.Value.Scheme}:{address.Value.Path} to unit '{unitName}': {ex.Message}");
                return 1;
            }
        }

        WarnUnsupportedSections(manifest, stdout);

        stdout.WriteLine($"[apply] done: unit '{unitName}', {createdMembers} member(s) added.");
        return 0;
    }

    /// <summary>
    /// Prints a dry-run plan for the manifest without invoking any API.
    /// </summary>
    public static void PrintPlan(UnitManifest manifest, TextWriter stdout)
    {
        var unitName = manifest.Name!;
        stdout.WriteLine($"[dry-run] plan for unit '{unitName}':");
        stdout.WriteLine($"[dry-run]   create unit '{unitName}'");

        if (manifest.Members is { Count: > 0 })
        {
            foreach (var member in manifest.Members)
            {
                var address = ResolveMemberAddress(member);
                if (address is null)
                {
                    stdout.WriteLine("[dry-run]   (skipped member with no 'agent' or 'unit' field)");
                    continue;
                }
                stdout.WriteLine($"[dry-run]   add member {address.Value.Scheme}:{address.Value.Path}");
            }
        }
        else
        {
            stdout.WriteLine("[dry-run]   (no members declared)");
        }

        WarnUnsupportedSections(manifest, stdout);
        stdout.WriteLine("[dry-run] no API calls were made.");
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

    private static void WarnUnsupportedSections(UnitManifest manifest, TextWriter stdout)
    {
        foreach (var section in ManifestParser.CollectUnsupportedSections(manifest))
        {
            stdout.WriteLine(
                $"[warn] section '{section}' is parsed but not yet applied (follow-up issue pending)");
        }
    }
}