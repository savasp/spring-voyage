// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Services;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Cvoya.Spring.Host.Api.Models;
using Cvoya.Spring.Manifest;

using Microsoft.Extensions.Logging;

/// <summary>
/// File-system backed <see cref="IPackageCatalogService"/>. Scans a
/// <c>packages/</c> root on disk and materialises <see cref="UnitTemplateSummary"/>
/// entries for every <c>packages/{package}/units/{name}.yaml</c> file.
///
/// The packages root is configured via <see cref="PackageCatalogOptions.Root"/>
/// (setting <c>Packages:Root</c>). When the directory is missing the service
/// returns empty results rather than throwing — this is the normal case for
/// deployments that don't ship the packages tree alongside the API.
/// </summary>
public class FileSystemPackageCatalogService(
    PackageCatalogOptions options,
    ILogger<FileSystemPackageCatalogService> logger)
    : IPackageCatalogService
{
    /// <inheritdoc />
    public Task<IReadOnlyList<UnitTemplateSummary>> ListUnitTemplatesAsync(
        CancellationToken cancellationToken)
    {
        var root = options.Root;
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
        {
            logger.LogDebug(
                "Package catalog root '{Root}' does not exist; returning empty template list.",
                root);
            return Task.FromResult<IReadOnlyList<UnitTemplateSummary>>(Array.Empty<UnitTemplateSummary>());
        }

        var templates = new List<UnitTemplateSummary>();
        foreach (var packageDir in Directory.EnumerateDirectories(root))
        {
            var unitsDir = Path.Combine(packageDir, "units");
            if (!Directory.Exists(unitsDir))
            {
                continue;
            }

            var packageName = Path.GetFileName(packageDir);
            foreach (var file in Directory.EnumerateFiles(unitsDir, "*.yaml"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryAddTemplate(templates, packageName, file);
            }
            foreach (var file in Directory.EnumerateFiles(unitsDir, "*.yml"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryAddTemplate(templates, packageName, file);
            }
        }

        templates.Sort(static (a, b) =>
        {
            var cmp = string.Compare(a.Package, b.Package, StringComparison.OrdinalIgnoreCase);
            return cmp != 0 ? cmp : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
        });

        return Task.FromResult<IReadOnlyList<UnitTemplateSummary>>(templates);
    }

    /// <inheritdoc />
    public async Task<string?> LoadUnitTemplateYamlAsync(
        string package,
        string name,
        CancellationToken cancellationToken)
    {
        var root = options.Root;
        if (string.IsNullOrWhiteSpace(root))
        {
            return null;
        }

        // Defensive: reject identifiers that attempt directory traversal so the
        // caller can't read arbitrary files off the host by asking for
        // "../../etc/passwd" as a template name.
        if (ContainsTraversal(package) || ContainsTraversal(name))
        {
            return null;
        }

        var candidate = Path.Combine(root, package, "units", name + ".yaml");
        if (!File.Exists(candidate))
        {
            candidate = Path.Combine(root, package, "units", name + ".yml");
            if (!File.Exists(candidate))
            {
                return null;
            }
        }

        // Re-check the resolved path is still inside the packages root.
        var fullRoot = Path.GetFullPath(root);
        var fullCandidate = Path.GetFullPath(candidate);
        if (!fullCandidate.StartsWith(fullRoot, StringComparison.Ordinal))
        {
            return null;
        }

        return await File.ReadAllTextAsync(fullCandidate, cancellationToken);
    }

    private void TryAddTemplate(List<UnitTemplateSummary> target, string packageName, string file)
    {
        try
        {
            var yaml = File.ReadAllText(file);
            var manifest = ManifestParser.Parse(yaml);
            var relativePath = Path.GetRelativePath(options.Root!, file).Replace('\\', '/');
            target.Add(new UnitTemplateSummary(
                Package: packageName,
                Name: manifest.Name!,
                Description: manifest.Description,
                Path: relativePath));
        }
        catch (ManifestParseException ex)
        {
            logger.LogWarning(
                ex,
                "Skipping unit template '{File}' because its YAML could not be parsed.",
                file);
        }
    }

    private static bool ContainsTraversal(string segment) =>
        string.IsNullOrWhiteSpace(segment)
        || segment.Contains("..", StringComparison.Ordinal)
        || segment.Contains('/', StringComparison.Ordinal)
        || segment.Contains('\\', StringComparison.Ordinal);
}

/// <summary>
/// Options bag for the file-system backed package catalog.
/// </summary>
public class PackageCatalogOptions
{
    /// <summary>
    /// Absolute or relative path to the packages root. When <c>null</c> or the
    /// path does not exist, the catalog is empty.
    /// </summary>
    public string? Root { get; set; }
}