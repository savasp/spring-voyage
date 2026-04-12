// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Commands;

using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

/// <summary>
/// Parses a unit manifest YAML file and applies it against a <see cref="SpringApiClient"/>.
/// Extracted from <see cref="ApplyCommand"/> so the parse + apply logic can be unit tested
/// without going through <c>System.CommandLine</c>.
/// </summary>
public static class ApplyRunner
{
    /// <summary>
    /// Sections of the unit manifest grammar that are parsed but not yet wired up
    /// through <see cref="SpringApiClient"/>. Listed here so we can emit a consistent
    /// warning line per section during both dry-run and real apply.
    /// </summary>
    internal static readonly string[] UnsupportedSections = new[]
    {
        "ai", "connectors", "policies", "humans", "execution",
    };

    /// <summary>
    /// Parses the manifest YAML text into a <see cref="UnitManifest"/>.
    /// Throws <see cref="ManifestParseException"/> if the document is malformed
    /// or the required <c>unit.name</c> field is missing.
    /// </summary>
    public static UnitManifest Parse(string yamlText)
    {
        ManifestDocument? doc;
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();
            doc = deserializer.Deserialize<ManifestDocument>(yamlText);
        }
        catch (YamlDotNet.Core.YamlException ex)
        {
            throw new ManifestParseException($"Invalid YAML: {ex.Message}", ex);
        }

        if (doc?.Unit is null)
        {
            throw new ManifestParseException("Manifest is missing the required 'unit' root section.");
        }

        if (string.IsNullOrWhiteSpace(doc.Unit.Name))
        {
            throw new ManifestParseException("Manifest is missing the required 'unit.name' field.");
        }

        return doc.Unit;
    }

    /// <summary>
    /// Parses the manifest at <paramref name="filePath"/> and returns the resolved unit manifest.
    /// </summary>
    public static UnitManifest ParseFile(string filePath)
    {
        var text = File.ReadAllText(filePath);
        return Parse(text);
    }

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
            await client.CreateUnitAsync(unitName, unitName, ct);
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
        foreach (var section in UnsupportedSections)
        {
            if (IsSectionPresent(manifest, section))
            {
                stdout.WriteLine(
                    $"[warn] section '{section}' is parsed but not yet applied (follow-up issue pending)");
            }
        }
    }

    private static bool IsSectionPresent(UnitManifest manifest, string section) => section switch
    {
        "ai" => manifest.Ai is not null,
        "connectors" => manifest.Connectors is { Count: > 0 },
        "policies" => manifest.Policies is { Count: > 0 },
        "humans" => manifest.Humans is { Count: > 0 },
        "execution" => manifest.Execution is not null,
        _ => false,
    };
}

/// <summary>
/// Thrown when a manifest YAML document cannot be parsed into a valid
/// <see cref="UnitManifest"/>.
/// </summary>
public class ManifestParseException : System.Exception
{
    /// <summary>Creates a new <see cref="ManifestParseException"/>.</summary>
    public ManifestParseException(string message) : base(message) { }

    /// <summary>Creates a new <see cref="ManifestParseException"/> with an inner cause.</summary>
    public ManifestParseException(string message, System.Exception inner) : base(message, inner) { }
}