// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Secrets;

using Cvoya.Spring.Dapr.Secrets;
using Cvoya.Spring.Dapr.Tenancy;

using global::Dapr.Client;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for <see cref="DaprStateBackedSecretStore"/>. Verifies that the
/// returned storeKey is an opaque GUID, that plaintext round-trips
/// through the <see cref="DaprClient"/>, and that no tenant or other
/// structural information is embedded in the backend key — the registry
/// is the sole authority for tenant correlation.
/// </summary>
public class DaprStateBackedSecretStoreTests
{
    private const string Component = "statestore";
    private readonly DaprClient _dapr = Substitute.For<DaprClient>();
    private readonly DaprStateBackedSecretStore _sut;

    public DaprStateBackedSecretStoreTests()
    {
        var options = Options.Create(new SecretsOptions
        {
            StoreComponent = Component,
            KeyPrefix = "secrets/",
        });
        var logger = Substitute.For<ILogger<DaprStateBackedSecretStore>>();
        _sut = new DaprStateBackedSecretStore(_dapr, options, logger);
    }

    [Fact]
    public async Task WriteAsync_ReturnsOpaqueGuidStoreKey()
    {
        var ct = TestContext.Current.CancellationToken;

        var key = await _sut.WriteAsync("hunter2", ct);

        key.ShouldNotBeNullOrWhiteSpace();
        Guid.TryParseExact(key, "N", out _).ShouldBeTrue();
        key.ShouldNotContain("hunter2");
    }

    [Fact]
    public async Task WriteAsync_CallsDaprClient_WithPrefixedOpaqueKey()
    {
        var ct = TestContext.Current.CancellationToken;

        var key = await _sut.WriteAsync("hunter2", ct);

        await _dapr.Received(1).SaveStateAsync(
            Component,
            $"secrets/{key}",
            "hunter2",
            Arg.Any<global::Dapr.Client.StateOptions?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task WriteAsync_BackendKey_DoesNotEncodeTenant()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.WriteAsync("hunter2", ct);

        // The backend key must carry no tenant segment. The registry
        // (ISecretRegistry) is the sole authority for tenant correlation.
        await _dapr.Received().SaveStateAsync(
            Component,
            Arg.Is<string>(k => !k.Contains("t1") && !k.Contains("local") && !k.Contains("tenant")),
            Arg.Any<string>(),
            Arg.Any<global::Dapr.Client.StateOptions?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadAsync_ReturnsStoredValue()
    {
        var ct = TestContext.Current.CancellationToken;

        _dapr
            .GetStateAsync<string?>(Component, "secrets/abc", cancellationToken: Arg.Any<CancellationToken>())
            .Returns("hunter2");

        var result = await _sut.ReadAsync("abc", ct);

        result.ShouldBe("hunter2");
    }

    [Fact]
    public async Task DeleteAsync_CallsDaprClient()
    {
        var ct = TestContext.Current.CancellationToken;

        await _sut.DeleteAsync("abc", ct);

        await _dapr.Received(1).DeleteStateAsync(
            Component,
            "secrets/abc",
            Arg.Any<global::Dapr.Client.StateOptions?>(),
            Arg.Any<IReadOnlyDictionary<string, string>?>(),
            Arg.Any<CancellationToken>());
    }
}