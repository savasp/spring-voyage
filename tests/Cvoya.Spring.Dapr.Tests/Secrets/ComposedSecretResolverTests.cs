// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Secrets;

using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Dapr.Secrets;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for <see cref="ComposedSecretResolver"/> verifying that it
/// correctly composes <see cref="ISecretRegistry"/> and
/// <see cref="ISecretStore"/>.
/// </summary>
public class ComposedSecretResolverTests
{
    [Fact]
    public async Task ResolveAsync_ExistingRef_ReturnsPlaintextFromStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Substitute.For<ISecretRegistry>();
        var store = Substitute.For<ISecretStore>();

        registry.LookupStoreKeyAsync(Arg.Any<SecretRef>(), ct).Returns("sk-1");
        store.ReadAsync("sk-1", ct).Returns("hunter2");

        var sut = new ComposedSecretResolver(registry, store);

        var result = await sut.ResolveAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        result.ShouldBe("hunter2");
    }

    [Fact]
    public async Task ResolveAsync_MissingRef_ReturnsNullWithoutReadingStore()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Substitute.For<ISecretRegistry>();
        var store = Substitute.For<ISecretStore>();

        registry.LookupStoreKeyAsync(Arg.Any<SecretRef>(), ct).Returns((string?)null);

        var sut = new ComposedSecretResolver(registry, store);

        var result = await sut.ResolveAsync(
            new SecretRef(SecretScope.Unit, "u1", "missing"), ct);

        result.ShouldBeNull();
        await store.DidNotReceive().ReadAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListAsync_DelegatesToRegistry()
    {
        var ct = TestContext.Current.CancellationToken;
        var registry = Substitute.For<ISecretRegistry>();
        var store = Substitute.For<ISecretStore>();

        var expected = new List<SecretRef>
        {
            new(SecretScope.Unit, "u1", "a"),
            new(SecretScope.Unit, "u1", "b"),
        };
        registry.ListAsync(SecretScope.Unit, "u1", ct).Returns(expected);

        var sut = new ComposedSecretResolver(registry, store);

        var result = await sut.ListAsync(SecretScope.Unit, "u1", ct);

        result.ShouldBe(expected);
    }
}