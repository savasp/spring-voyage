// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Manifest;

using System.Collections.Generic;

using YamlDotNet.Serialization;

/// <summary>
/// Discriminates the kind of package declared in a <c>package.yaml</c>.
/// Matches the <c>kind:</c> scalar on the root document.
/// </summary>
public enum PackageKind
{
    /// <summary>The package bundles a unit (a composite of agents and/or sub-units).</summary>
    UnitPackage,

    /// <summary>The package bundles a single agent.</summary>
    AgentPackage,
}

/// <summary>
/// Root document for a <c>package.yaml</c> manifest. Parsed by
/// <see cref="PackageManifestParser"/>. A package declares its kind,
/// metadata, inputs schema, and the root artefact it wraps.
/// </summary>
/// <remarks>
/// <para>
/// The package YAML shape (decision 2 in ADR-0035, refined by #1629 PR7):
/// </para>
/// <code>
/// apiVersion: spring.voyage/v1
/// kind: UnitPackage            # or AgentPackage
/// metadata:
///   name: my-package
///   description: ...
/// inputs:
///   - name: team_name
///     type: string
///     required: true
/// unit: sv-oss-design          # bare = local symbol → ./units/sv-oss-design.yaml
///                              # qualified = cross-package = other-pkg/sv-oss-design
/// </code>
/// <para>
/// <b>Reference grammar (#1629 PR7):</b> within a manifest, references
/// between artefacts use IaC-style local symbols (the bare-name form
/// above) that are mapped to fresh Guids at install time and never
/// persist. Cross-package references to live entities are written as
/// 32-char no-dash hex Guids — display-name lookups across packages are
/// gone, since display names aren't unique. Path-style references like
/// <c>unit://eng/backend</c> are rejected.
/// </para>
/// </remarks>
public class PackageManifest
{
    /// <summary>API version string (e.g. <c>spring.voyage/v1</c>).</summary>
    [YamlMember(Alias = "apiVersion")]
    public string? ApiVersion { get; set; }

    /// <summary>Package kind. Must be <c>UnitPackage</c> or <c>AgentPackage</c>.</summary>
    [YamlMember(Alias = "kind")]
    public string? Kind { get; set; }

    /// <summary>Package-level metadata (name, description, etc.).</summary>
    [YamlMember(Alias = "metadata")]
    public PackageMetadata? Metadata { get; set; }

    /// <summary>
    /// Declared inputs for the package. Each entry defines a scalar input
    /// that may be referenced as <c>${{ inputs.&lt;name&gt; }}</c> in any
    /// YAML value within the package.
    /// </summary>
    [YamlMember(Alias = "inputs")]
    public List<PackageInputDefinition>? Inputs { get; set; }

    /// <summary>
    /// The root unit slot (used when <see cref="Kind"/> is
    /// <c>UnitPackage</c>). Accepts either a bare/qualified reference string
    /// (bare resolves to <c>./units/&lt;name&gt;.yaml</c>; <c>pkg/name</c>
    /// resolves via the catalog) or an inline unit body — see
    /// <see cref="InlineArtefactDefinition"/>. Inline bodies enable the
    /// wizard's single-artefact "scratch" path (ADR-0035 decision 6) without
    /// reintroducing the dual-pipeline divergence ADR-0035 explicitly rejected.
    /// </summary>
    [YamlMember(Alias = "unit")]
    public InlineArtefactDefinition? Unit { get; set; }

    /// <summary>
    /// The root agent slot (used when <see cref="Kind"/> is
    /// <c>AgentPackage</c>). Accepts either a bare/qualified reference string
    /// (bare resolves to <c>./agents/&lt;name&gt;.yaml</c>; <c>pkg/name</c>
    /// resolves via the catalog) or an inline agent body — see
    /// <see cref="InlineArtefactDefinition"/>.
    /// </summary>
    [YamlMember(Alias = "agent")]
    public InlineArtefactDefinition? Agent { get; set; }

    /// <summary>
    /// Additional sub-unit references. Each entry resolves the same way as
    /// <see cref="Unit"/>. These represent artefacts bundled alongside the
    /// root unit in a <c>UnitPackage</c>.
    /// </summary>
    [YamlMember(Alias = "subUnits")]
    public List<string>? SubUnits { get; set; }

    /// <summary>
    /// Skill references bundled in the package. Bare name resolves to
    /// <c>./skills/&lt;name&gt;.md</c>.
    /// </summary>
    [YamlMember(Alias = "skills")]
    public List<string>? Skills { get; set; }

    /// <summary>
    /// Workflow references bundled in the package. Bare name resolves to
    /// <c>./workflows/&lt;name&gt;/</c>.
    /// </summary>
    [YamlMember(Alias = "workflows")]
    public List<string>? Workflows { get; set; }

    /// <summary>
    /// Declarative connectors block (#1670). Lists each connector type the
    /// package depends on, whether it is required at install time, and how
    /// its binding inherits to member units. Operators configure each
    /// declared connector once at install time; the resolved binding is
    /// inherited by every member unit unless the unit's own
    /// <c>connectors:</c> block opts out via <c>inherit: false</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Inheritance forms accepted on each entry's <c>inherit</c> slot:
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>all</c> (default) — every member unit inherits.</description></item>
    ///   <item><description><c>[unit-a, unit-b]</c> — only the named members inherit.</description></item>
    /// </list>
    /// <para>
    /// Per-unit opt-out is expressed in the unit YAML by declaring the
    /// connector slug in the unit's <c>connectors:</c> block with
    /// <c>inherit: false</c>.
    /// </para>
    /// </remarks>
    [YamlMember(Alias = "connectors")]
    public List<RequiredConnector>? Connectors { get; set; }
}

/// <summary>
/// One entry in a package's <c>connectors:</c> block (#1670). Declares
/// the connector type the package depends on plus how its binding
/// inherits to member units.
/// </summary>
public class RequiredConnector
{
    /// <summary>
    /// The connector type slug (matches
    /// <c>Cvoya.Spring.Connectors.IConnectorType.Slug</c>) — e.g.
    /// <c>github</c>. The manifest parser validates the slug against the
    /// connector registry at install time; an unknown slug is a parse error.
    /// </summary>
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    /// <summary>
    /// When <c>true</c>, the install pipeline rejects the request with a
    /// <c>ConnectorBindingMissing</c> 400 if the operator has not supplied
    /// a binding for this connector at install time. Defaults to <c>true</c>
    /// — a connector is declared because it is needed.
    /// </summary>
    [YamlMember(Alias = "required")]
    public bool Required { get; set; } = true;

    /// <summary>
    /// Inheritance scope. Accepts the literal string <c>all</c> (every
    /// member unit inherits — the default) or a YAML sequence of member
    /// unit names (only the named members inherit). The two shapes are
    /// surfaced through <see cref="InheritAll"/> and
    /// <see cref="InheritUnits"/> after parsing — the raw YAML node lives
    /// on <see cref="InheritRaw"/> so the parser can distinguish "absent"
    /// from "explicitly set to all".
    /// </summary>
    /// <remarks>
    /// Per-unit opt-out (<c>inherit: false</c>) is expressed on the unit
    /// side, not here — see <see cref="ConnectorManifest.Inherit"/>.
    /// </remarks>
    [YamlMember(Alias = "inherit")]
    public object? InheritRaw { get; set; }

    /// <summary>
    /// True when <see cref="InheritRaw"/> is absent or the literal string
    /// <c>all</c>. Set by the parser after reading the raw YAML.
    /// </summary>
    [YamlIgnore]
    public bool InheritAll { get; set; } = true;

    /// <summary>
    /// When non-null, the explicit list of member unit names that inherit
    /// the package-level binding. <c>null</c> when <see cref="InheritAll"/>
    /// is <c>true</c>. Set by the parser after reading the raw YAML.
    /// </summary>
    [YamlIgnore]
    public IReadOnlyList<string>? InheritUnits { get; set; }
}

/// <summary>
/// Package-level metadata block.
/// </summary>
public class PackageMetadata
{
    /// <summary>Unique package name (must be a valid identifier).</summary>
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    /// <summary>Human-readable description of the package.</summary>
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    /// <summary>Optional display name for the package.</summary>
    [YamlMember(Alias = "displayName")]
    public string? DisplayName { get; set; }
}

/// <summary>
/// Declares a single scalar input for a package. Input types are
/// <c>string</c>, <c>int</c>, <c>bool</c>, and <c>secret</c>
/// (per ADR-0035 decision 8).
/// </summary>
public class PackageInputDefinition
{
    /// <summary>Input key name. Used in <c>${{ inputs.&lt;name&gt; }}</c> expressions.</summary>
    [YamlMember(Alias = "name")]
    public string? Name { get; set; }

    /// <summary>
    /// Scalar type: <c>string</c> (default), <c>int</c>, or <c>bool</c>.
    /// Use <see cref="Secret"/> for secret-typed inputs.
    /// </summary>
    [YamlMember(Alias = "type")]
    public string? Type { get; set; }

    /// <summary>When <c>true</c>, the input must be supplied; an absent value is a parse error.</summary>
    [YamlMember(Alias = "required")]
    public bool Required { get; set; }

    /// <summary>
    /// When <c>true</c>, the input is secret-typed. The value is stored as a
    /// secret reference, not as cleartext. Secret inputs are never round-tripped
    /// in export output as plain values.
    /// </summary>
    [YamlMember(Alias = "secret")]
    public bool Secret { get; set; }

    /// <summary>Human-readable description of the input's purpose.</summary>
    [YamlMember(Alias = "description")]
    public string? Description { get; set; }

    /// <summary>
    /// Optional default value (ignored for <c>required: true</c> inputs when
    /// no value is supplied — a required input with no value is an error even
    /// if a default is declared).
    /// </summary>
    [YamlMember(Alias = "default")]
    public string? Default { get; set; }
}