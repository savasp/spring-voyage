// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Models;

using System;
using System.Collections.Generic;

/// <summary>
/// Request body for <c>POST /api/v1/packages/install</c>.
/// Accepts one or more install targets as a batch (ADR-0035 decision 14).
/// Single-package install = array-of-one.
/// </summary>
/// <param name="Targets">
/// The packages to install, each with an optional input map.
/// All packages in one request are installed as a single atomic batch:
/// Phase 1 commits all rows or rolls all back; Phase 2 activates in
/// dependency order.
/// </param>
public sealed record PackageInstallRequest(
    IReadOnlyList<PackageInstallTarget> Targets);

/// <summary>
/// A single package within a <see cref="PackageInstallRequest"/>.
/// </summary>
/// <param name="PackageName">
/// The package name. Must match <c>metadata.name</c> in the package YAML
/// (for catalog installs this is the catalog key; for file uploads the
/// YAML is supplied separately and this field is ignored if the YAML
/// declares its own name).
/// </param>
/// <param name="Inputs">
/// Key/value input overrides for this package. Keys must match the
/// <c>inputs</c> schema declared in the <c>package.yaml</c>. Secret-typed
/// inputs must already be in <c>secret://</c> reference form. Null is
/// treated as an empty map.
/// </param>
public sealed record PackageInstallTarget(
    string PackageName,
    IReadOnlyDictionary<string, string>? Inputs);

/// <summary>
/// Response body for <c>POST /api/v1/packages/install</c>,
/// <c>POST /api/v1/installs/{id}/retry</c>, and
/// <c>GET /api/v1/installs/{id}</c>.
/// Carries the shared batch identifier and per-package outcome.
/// </summary>
/// <param name="InstallId">
/// The batch identifier. Use this value as <c>{id}</c> in
/// <c>GET /api/v1/installs/{id}</c>, <c>/retry</c>, and <c>/abort</c>.
/// </param>
/// <param name="Status">
/// Aggregate status: <c>active</c> when all packages succeeded,
/// <c>staging</c> while Phase 2 is in progress, <c>failed</c> if any
/// package failed Phase 2 activation.
/// </param>
/// <param name="Packages">Per-package outcomes.</param>
/// <param name="StartedAt">UTC timestamp when Phase 1 began.</param>
/// <param name="CompletedAt">
/// UTC timestamp when Phase 2 finished (null if still in progress).
/// </param>
/// <param name="Error">
/// Top-level error message for Phase-1 failures. Null for Phase-2 failures
/// (per-package errors are in <see cref="Packages"/>).
/// </param>
public sealed record InstallStatusResponse(
    Guid InstallId,
    string Status,
    IReadOnlyList<InstallPackageDetail> Packages,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string? Error);

/// <summary>
/// Per-package detail within an <see cref="InstallStatusResponse"/>.
/// </summary>
/// <param name="PackageName">The package name.</param>
/// <param name="State">
/// Current state of this package: <c>staging</c>, <c>active</c>, or
/// <c>failed</c>.
/// </param>
/// <param name="ErrorMessage">
/// Activation error detail when <paramref name="State"/> is <c>failed</c>.
/// </param>
public sealed record InstallPackageDetail(
    string PackageName,
    string State,
    string? ErrorMessage);