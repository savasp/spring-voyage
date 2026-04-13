// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Services;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Cvoya.Spring.Host.Api.Models;

/// <summary>
/// Surfaces the on-disk <c>packages/*/units/*.yaml</c> tree as a catalog of
/// unit templates the wizard can pick from. A pluggable interface so the
/// private cloud repo can back the catalog with a tenant-scoped store.
/// </summary>
public interface IPackageCatalogService
{
    /// <summary>
    /// Lists every unit template currently reachable from the configured
    /// packages root. Returns an empty list when the packages directory does
    /// not exist (e.g. the API is running outside the repo).
    /// </summary>
    Task<IReadOnlyList<UnitTemplateSummary>> ListUnitTemplatesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// Loads the raw YAML for the template identified by
    /// <paramref name="package"/> and <paramref name="name"/>, or returns
    /// <c>null</c> when the template is not found.
    /// </summary>
    Task<string?> LoadUnitTemplateYamlAsync(
        string package,
        string name,
        CancellationToken cancellationToken);
}