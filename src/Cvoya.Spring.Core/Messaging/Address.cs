// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Core.Messaging;

using System.Runtime.Serialization;

/// <summary>
/// Represents an addressable endpoint in the Spring Voyage platform.
/// The scheme identifies the type of addressable (e.g., "agent", "unit", "connector")
/// and the path identifies the specific instance (e.g., "engineering-team/ada").
/// </summary>
/// <remarks>
/// Travels across the Dapr Actor remoting boundary as the argument to
/// <c>IUnitActor.AddMemberAsync</c> / <c>RemoveMemberAsync</c>; the
/// <c>[DataContract]</c> annotations let <c>DataContractSerializer</c> handle
/// positional records that lack a parameterless constructor (#261).
/// </remarks>
/// <param name="Scheme">The address scheme identifying the type of addressable.</param>
/// <param name="Path">The path identifying the specific addressable instance.</param>
[DataContract]
public record Address(
    [property: DataMember] string Scheme,
    [property: DataMember] string Path)
{
    /// <summary>Canonical scheme for agent-shaped addresses.</summary>
    public const string AgentScheme = "agent";

    /// <summary>Canonical scheme for unit-shaped addresses.</summary>
    public const string UnitScheme = "unit";

    public sealed override string ToString() => $"{Scheme}:{Path}";

    /// <summary>
    /// Returns the canonical URI form (<c>{scheme}://{path}</c>) used by
    /// wire-shape projections that need a single scheme-prefixed string —
    /// e.g. the <c>member</c> field on <c>UnitMembershipResponse</c> (#1060)
    /// and the <c>source</c> column on activity / cost rows.
    /// </summary>
    /// <remarks>
    /// Distinct from <see cref="ToString"/>, which uses a <c>:</c> separator
    /// for log lines / error messages. The URI form is the contract callers
    /// pipe through jq or hand to other systems.
    /// </remarks>
    public string ToCanonicalUri() => $"{Scheme}://{Path}";

    /// <summary>Builds an agent-scheme address (<c>agent://{path}</c>).</summary>
    public static Address ForAgent(string path) => new(AgentScheme, path);

    /// <summary>Builds a unit-scheme address (<c>unit://{path}</c>).</summary>
    public static Address ForUnit(string path) => new(UnitScheme, path);
}