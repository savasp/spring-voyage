// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Secrets;

using Cvoya.Spring.Core.Secrets;
using Cvoya.Spring.Core.Tenancy;
using Cvoya.Spring.Dapr.Data;
using Cvoya.Spring.Dapr.Secrets;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Tests for <see cref="EfSecretRegistry"/> — tenant filtering is
/// mandatory per the PR spec, so the cross-tenant isolation test is
/// parameterised across the two fixture tenants (<c>"t1"</c> / <c>"t2"</c>).
/// </summary>
public class EfSecretRegistryTests : IDisposable
{
    private readonly SpringDbContext _db;

    public EfSecretRegistryTests()
    {
        var options = new DbContextOptionsBuilder<SpringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new SpringDbContext(options);
    }

    [Theory]
    [InlineData("t1")]
    [InlineData("t2")]
    public async Task RegisterThenLookup_ReturnsStoreKey_ForSameTenant(string tenant)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry(tenant);

        await sut.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-abc", ct);

        var found = await sut.LookupStoreKeyAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        found.ShouldBe("sk-abc");
    }

    [Fact]
    public async Task CrossTenantIsolation_ReturnsNull_WhenEntryBelongsToDifferentTenant()
    {
        var ct = TestContext.Current.CancellationToken;

        // Tenant t2 registers (Unit, "u1", "foo"). Tenant t1 must NOT
        // see it — this is the mandatory cross-tenant isolation test
        // called out in the PR spec.
        var t2 = NewRegistry("t2");
        await t2.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-xyz", ct);

        var t1 = NewRegistry("t1");
        var found = await t1.LookupStoreKeyAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        found.ShouldBeNull();
    }

    [Fact]
    public async Task List_ReturnsOnlyCurrentTenantEntries()
    {
        var ct = TestContext.Current.CancellationToken;

        var t1 = NewRegistry("t1");
        var t2 = NewRegistry("t2");

        await t1.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "a"), "k-t1-a", ct);
        await t1.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "b"), "k-t1-b", ct);
        await t2.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "c"), "k-t2-c", ct);

        var t1List = await t1.ListAsync(SecretScope.Unit, "u1", ct);
        var t2List = await t2.ListAsync(SecretScope.Unit, "u1", ct);

        t1List.Select(r => r.Name).OrderBy(s => s).ShouldBe(new[] { "a", "b" });
        t2List.Select(r => r.Name).ShouldBe(new[] { "c" });
    }

    [Theory]
    [InlineData("t1")]
    [InlineData("t2")]
    public async Task Register_SameTriple_Replaces_PreviousStoreKey(string tenant)
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry(tenant);

        await sut.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", ct);
        await sut.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-2", ct);

        var found = await sut.LookupStoreKeyAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        found.ShouldBe("sk-2");

        // There should be exactly one row — the earlier register was an
        // in-place update, not an insert, so the unique index on
        // (tenant, scope, owner, name) is not violated.
        var count = await _db.SecretRegistryEntries.CountAsync(ct);
        count.ShouldBe(1);
    }

    [Theory]
    [InlineData("t1")]
    [InlineData("t2")]
    public async Task Delete_RemovesEntry_OnlyInCurrentTenant(string tenant)
    {
        var ct = TestContext.Current.CancellationToken;

        // Register identical (Unit, "u1", "foo") in both tenants; only
        // the one in the deleting tenant should disappear.
        await NewRegistry("t1").RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "k-t1", ct);
        await NewRegistry("t2").RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "k-t2", ct);

        var sut = NewRegistry(tenant);
        await sut.DeleteAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        var other = tenant == "t1" ? "t2" : "t1";
        var otherReg = NewRegistry(other);

        (await sut.LookupStoreKeyAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct)).ShouldBeNull();
        (await otherReg.LookupStoreKeyAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct)).ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_Missing_IsNoOp()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        // Should not throw.
        await sut.DeleteAsync(new SecretRef(SecretScope.Unit, "missing", "none"), ct);
    }

    private EfSecretRegistry NewRegistry(string tenantId)
    {
        var tenantContext = Substitute.For<ITenantContext>();
        tenantContext.CurrentTenantId.Returns(tenantId);
        return new EfSecretRegistry(_db, tenantContext);
    }

    public void Dispose()
    {
        _db.Dispose();
        GC.SuppressFinalize(this);
    }
}