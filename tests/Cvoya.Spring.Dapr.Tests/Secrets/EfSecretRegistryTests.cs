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

        await sut.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-abc", SecretOrigin.PlatformOwned, ct);

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
        await t2.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-xyz", SecretOrigin.PlatformOwned, ct);

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

        await t1.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "a"), "k-t1-a", SecretOrigin.PlatformOwned, ct);
        await t1.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "b"), "k-t1-b", SecretOrigin.PlatformOwned, ct);
        await t2.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "c"), "k-t2-c", SecretOrigin.PlatformOwned, ct);

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

        await sut.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", SecretOrigin.PlatformOwned, ct);
        await sut.RegisterAsync(new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-2", SecretOrigin.PlatformOwned, ct);

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
            new SecretRef(SecretScope.Unit, "u1", "foo"), "k-t1", SecretOrigin.PlatformOwned, ct);
        await NewRegistry("t2").RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "k-t2", SecretOrigin.PlatformOwned, ct);

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

    [Theory]
    [InlineData(SecretOrigin.PlatformOwned)]
    [InlineData(SecretOrigin.ExternalReference)]
    public async Task LookupAsync_ReturnsOrigin_AsRegistered(SecretOrigin origin)
    {
        // The origin field is load-bearing on the DELETE path — if the
        // registry loses it, the store-layer delete gate in the endpoints
        // becomes useless. Parameterise both values.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", origin, ct);

        var pointer = await sut.LookupAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        pointer.ShouldNotBeNull();
        pointer!.StoreKey.ShouldBe("sk-1");
        pointer.Origin.ShouldBe(origin);
    }

    [Fact]
    public async Task LookupAsync_MissingRef_ReturnsNull()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        var pointer = await sut.LookupAsync(
            new SecretRef(SecretScope.Unit, "u1", "nope"), ct);

        pointer.ShouldBeNull();
    }

    [Fact]
    public async Task Register_SameTriple_Updates_Origin_OnReplacement()
    {
        // Re-registration must replace the origin as well as the store
        // key — otherwise a platform-owned → external-reference switch
        // (or vice versa) would leave stale origin data that would
        // mis-gate the DELETE path.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", SecretOrigin.PlatformOwned, ct);
        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "kv://ext/1", SecretOrigin.ExternalReference, ct);

        var pointer = await sut.LookupAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        pointer.ShouldNotBeNull();
        pointer!.StoreKey.ShouldBe("kv://ext/1");
        pointer.Origin.ShouldBe(SecretOrigin.ExternalReference);
    }

    [Fact]
    public async Task Register_NewEntry_StartsAtVersionOne()
    {
        // Pre-existing registration behavior: new rows initialize at
        // version 1 so audit decorators always see a stable starting
        // version. Legacy rows predating the migration remain at null.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", SecretOrigin.PlatformOwned, ct);

        var lookup = await sut.LookupWithVersionAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);

        lookup.ShouldNotBeNull();
        lookup!.Value.Version.ShouldBe(1);
    }

    [Fact]
    public async Task RotateAsync_PassThrough_IncrementsVersion_AndInvokesDelete()
    {
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", SecretOrigin.PlatformOwned, ct);

        var deleteCalls = new List<string>();
        var rotation = await sut.RotateAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"),
            "sk-2",
            SecretOrigin.PlatformOwned,
            (key, _) => { deleteCalls.Add(key); return Task.CompletedTask; },
            ct);

        rotation.FromVersion.ShouldBe(1);
        rotation.ToVersion.ShouldBe(2);
        rotation.PreviousPointer.StoreKey.ShouldBe("sk-1");
        rotation.PreviousPointer.Origin.ShouldBe(SecretOrigin.PlatformOwned);
        rotation.NewPointer.StoreKey.ShouldBe("sk-2");
        rotation.NewPointer.Origin.ShouldBe(SecretOrigin.PlatformOwned);
        rotation.PreviousStoreKeyDeleted.ShouldBeTrue();

        // Immediate-delete policy: the old store slot was reclaimed
        // before the rotate returned.
        deleteCalls.ShouldBe(new[] { "sk-1" });

        // The registry now points at the new key / bumped version.
        var pointer = await sut.LookupWithVersionAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);
        pointer.ShouldNotBeNull();
        pointer!.Value.Pointer.StoreKey.ShouldBe("sk-2");
        pointer.Value.Version.ShouldBe(2);
    }

    [Fact]
    public async Task RotateAsync_ExternalReference_SkipsDelete()
    {
        // External-reference entries point at customer-owned slots;
        // rotation must never invoke the delete delegate, otherwise a
        // private-cloud Key Vault implementation would destroy data
        // the platform does not own.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "kv://old", SecretOrigin.ExternalReference, ct);

        var deleteCalls = new List<string>();
        var rotation = await sut.RotateAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"),
            "kv://new",
            SecretOrigin.ExternalReference,
            (key, _) => { deleteCalls.Add(key); return Task.CompletedTask; },
            ct);

        rotation.PreviousPointer.Origin.ShouldBe(SecretOrigin.ExternalReference);
        rotation.PreviousStoreKeyDeleted.ShouldBeFalse();
        deleteCalls.ShouldBeEmpty();
    }

    [Fact]
    public async Task RotateAsync_SwitchOrigin_PlatformToExternal_InvokesDeleteOnOldPlatformSlot()
    {
        // A rotation that flips origin (platform-owned to external-
        // reference) still reclaims the old platform-owned slot — the
        // decision hinges on the PREVIOUS origin, not the new one.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-old", SecretOrigin.PlatformOwned, ct);

        var deleteCalls = new List<string>();
        var rotation = await sut.RotateAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"),
            "kv://new",
            SecretOrigin.ExternalReference,
            (key, _) => { deleteCalls.Add(key); return Task.CompletedTask; },
            ct);

        rotation.PreviousPointer.Origin.ShouldBe(SecretOrigin.PlatformOwned);
        rotation.NewPointer.Origin.ShouldBe(SecretOrigin.ExternalReference);
        rotation.PreviousStoreKeyDeleted.ShouldBeTrue();
        deleteCalls.ShouldBe(new[] { "sk-old" });

        var pointer = await sut.LookupAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), ct);
        pointer!.StoreKey.ShouldBe("kv://new");
        pointer.Origin.ShouldBe(SecretOrigin.ExternalReference);
    }

    [Fact]
    public async Task RotateAsync_MissingRef_Throws()
    {
        // Rotation is NOT a create operation. A missing reference is
        // a precondition failure surfaced to the endpoint layer as 404.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.RotateAsync(
                new SecretRef(SecretScope.Unit, "u1", "missing"),
                "sk-new",
                SecretOrigin.PlatformOwned,
                null,
                ct));
    }

    [Fact]
    public async Task RotateAsync_NullDelegate_DoesNotThrow()
    {
        // A null delete delegate is legitimate — tests (and any caller
        // that doesn't want cleanup) can omit it.
        var ct = TestContext.Current.CancellationToken;
        var sut = NewRegistry("t1");

        await sut.RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-1", SecretOrigin.PlatformOwned, ct);

        var rotation = await sut.RotateAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"),
            "sk-2",
            SecretOrigin.PlatformOwned,
            null,
            ct);

        rotation.ToVersion.ShouldBe(2);
        // Delegate was null, so no cleanup happened.
        rotation.PreviousStoreKeyDeleted.ShouldBeFalse();
    }

    [Theory]
    [InlineData("t1")]
    [InlineData("t2")]
    public async Task RotateAsync_CrossTenantIsolation_TreatsOtherTenantAsMissing(string tenant)
    {
        // Tenant isolation applies to rotate exactly as it does to lookup
        // and delete. An entry owned by tenant B must look "missing" to
        // a rotator running as tenant A.
        var ct = TestContext.Current.CancellationToken;

        var other = tenant == "t1" ? "t2" : "t1";
        await NewRegistry(other).RegisterAsync(
            new SecretRef(SecretScope.Unit, "u1", "foo"), "sk-other", SecretOrigin.PlatformOwned, ct);

        var sut = NewRegistry(tenant);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.RotateAsync(
                new SecretRef(SecretScope.Unit, "u1", "foo"),
                "sk-new",
                SecretOrigin.PlatformOwned,
                null,
                ct));
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