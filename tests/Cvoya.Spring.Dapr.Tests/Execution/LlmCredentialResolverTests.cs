// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Execution;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Core.Tenancy;
using Cvoya.Spring.Dapr.Execution;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for <see cref="LlmCredentialResolver"/> — the tier-2 credential
/// resolver introduced by #615. Verifies the unit → tenant → env
/// bootstrap fallback order, the correct provider-to-secret-name
/// mapping, and the fail-clean behaviour when nothing is configured.
/// </summary>
public class LlmCredentialResolverTests
{
    private const string TenantId = "acme";

    private static LlmCredentialResolver CreateSut(ISecretResolver resolver)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.CurrentTenantId.Returns(TenantId);
        return new LlmCredentialResolver(resolver, tenantContext, NullLogger<LlmCredentialResolver>.Instance);
    }

    [Fact]
    public async Task ResolveAsync_UnknownProvider_ReturnsNotFound()
    {
        var resolver = Substitute.For<ISecretResolver>();
        var sut = CreateSut(resolver);

        var result = await sut.ResolveAsync("no-such-provider", unitName: null, TestContext.Current.CancellationToken);

        result.Value.ShouldBeNull();
        result.Source.ShouldBe(LlmCredentialSource.NotFound);
        result.SecretName.ShouldBeEmpty();
        await resolver.DidNotReceiveWithAnyArgs().ResolveWithPathAsync(
            default!, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ResolveAsync_UnitScopedHit_ReturnsUnitSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveWithPathAsync(
                Arg.Is<SecretRef>(r => r.Scope == SecretScope.Unit && r.OwnerId == "u1" && r.Name == "anthropic-api-key"),
                ct)
            .Returns(new SecretResolution("sk-unit", SecretResolvePath.Direct, new SecretRef(SecretScope.Unit, "u1", "anthropic-api-key")));
        var sut = CreateSut(resolver);

        var result = await sut.ResolveAsync("claude", unitName: "u1", ct);

        result.Value.ShouldBe("sk-unit");
        result.Source.ShouldBe(LlmCredentialSource.Unit);
        result.SecretName.ShouldBe("anthropic-api-key");
    }

    [Fact]
    public async Task ResolveAsync_UnitMissesTenantHas_ReportsTenantSource()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        var unitRef = new SecretRef(SecretScope.Unit, "u1", "openai-api-key");
        var tenantRef = new SecretRef(SecretScope.Tenant, TenantId, "openai-api-key");
        resolver.ResolveWithPathAsync(unitRef, ct)
            .Returns(new SecretResolution(
                "sk-tenant",
                SecretResolvePath.InheritedFromTenant,
                tenantRef));
        var sut = CreateSut(resolver);

        var result = await sut.ResolveAsync("openai", unitName: "u1", ct);

        result.Value.ShouldBe("sk-tenant");
        result.Source.ShouldBe(LlmCredentialSource.Tenant);
        result.SecretName.ShouldBe("openai-api-key");
    }

    [Fact]
    public async Task ResolveAsync_NoUnit_QueriesTenantDirectly()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveWithPathAsync(
                Arg.Is<SecretRef>(r => r.Scope == SecretScope.Tenant && r.OwnerId == TenantId && r.Name == "anthropic-api-key"),
                ct)
            .Returns(new SecretResolution(
                "sk-tenant-default",
                SecretResolvePath.Direct,
                new SecretRef(SecretScope.Tenant, TenantId, "anthropic-api-key")));
        var sut = CreateSut(resolver);

        var result = await sut.ResolveAsync("claude", unitName: null, ct);

        result.Value.ShouldBe("sk-tenant-default");
        result.Source.ShouldBe(LlmCredentialSource.Tenant);
        result.SecretName.ShouldBe("anthropic-api-key");
    }

    [Fact]
    public async Task ResolveAsync_FallsThroughToEnvironment_WhenSecretsAreUnset()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveWithPathAsync(Arg.Any<SecretRef>(), ct)
            .Returns(new SecretResolution(null, SecretResolvePath.NotFound, null));
        var sut = CreateSut(resolver);

        using var env = new EnvVarScope("ANTHROPIC_API_KEY", "sk-from-env");

        var result = await sut.ResolveAsync("claude", unitName: "u1", ct);

        result.Value.ShouldBe("sk-from-env");
        result.Source.ShouldBe(LlmCredentialSource.EnvironmentBootstrap);
        result.SecretName.ShouldBe("anthropic-api-key");
    }

    [Fact]
    public async Task ResolveAsync_EnvVarEmpty_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveWithPathAsync(Arg.Any<SecretRef>(), ct)
            .Returns(new SecretResolution(null, SecretResolvePath.NotFound, null));
        var sut = CreateSut(resolver);

        // Deliberately clear the env var so the test doesn't pick up a
        // CI-supplied key.
        using var env = new EnvVarScope("ANTHROPIC_API_KEY", null);

        var result = await sut.ResolveAsync("claude", unitName: null, ct);

        result.Value.ShouldBeNull();
        result.Source.ShouldBe(LlmCredentialSource.NotFound);
        // Even on NotFound, SecretName is populated so error messages can
        // point operators at the exact secret name they must create.
        result.SecretName.ShouldBe("anthropic-api-key");
    }

    [Fact]
    public async Task ResolveAsync_GoogleProvider_TriesBothGoogleAndGeminiEnvVars()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveWithPathAsync(Arg.Any<SecretRef>(), ct)
            .Returns(new SecretResolution(null, SecretResolvePath.NotFound, null));
        var sut = CreateSut(resolver);

        using var google = new EnvVarScope("GOOGLE_API_KEY", null);
        using var gemini = new EnvVarScope("GEMINI_API_KEY", "gemini-env");

        var result = await sut.ResolveAsync("google", unitName: null, ct);

        result.Value.ShouldBe("gemini-env");
        result.Source.ShouldBe(LlmCredentialSource.EnvironmentBootstrap);
        result.SecretName.ShouldBe("google-api-key");
    }

    [Fact]
    public async Task ResolveAsync_AnthropicAliasFor_Claude()
    {
        var ct = TestContext.Current.CancellationToken;
        var resolver = Substitute.For<ISecretResolver>();
        resolver.ResolveWithPathAsync(Arg.Any<SecretRef>(), ct)
            .Returns(new SecretResolution("sk", SecretResolvePath.Direct, new SecretRef(SecretScope.Tenant, TenantId, "anthropic-api-key")));
        var sut = CreateSut(resolver);

        // Both "claude" and "anthropic" must resolve the same canonical
        // secret name so callers can use either identifier.
        var claude = await sut.ResolveAsync("claude", null, ct);
        var anthropic = await sut.ResolveAsync("anthropic", null, ct);

        claude.SecretName.ShouldBe("anthropic-api-key");
        anthropic.SecretName.ShouldBe("anthropic-api-key");
    }

    private sealed class EnvVarScope : IDisposable
    {
        private readonly string _name;
        private readonly string? _previous;

        public EnvVarScope(string name, string? value)
        {
            _name = name;
            _previous = Environment.GetEnvironmentVariable(name);
            Environment.SetEnvironmentVariable(name, value);
        }

        public void Dispose()
        {
            Environment.SetEnvironmentVariable(_name, _previous);
        }
    }
}