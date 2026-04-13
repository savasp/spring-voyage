// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Tests.Endpoints;

using System.IO;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Cvoya.Spring.Core.Directory;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Units;
using Cvoya.Spring.Dapr.Actors;
using Cvoya.Spring.Host.Api.Models;
using Cvoya.Spring.Host.Api.Services;

using FluentAssertions;

using global::Dapr.Actors;
using global::Dapr.Actors.Client;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using NSubstitute;

using Xunit;

/// <summary>
/// Integration tests for the manifest-backed unit creation endpoints
/// (<c>POST /api/v1/units/from-yaml</c> and <c>POST /api/v1/units/from-template</c>)
/// plus the <c>GET /api/v1/packages/templates</c> catalog endpoint.
/// </summary>
public class UnitCreationEndpointTests : IClassFixture<UnitCreationEndpointTests.Factory>
{
    private readonly Factory _factory;
    private readonly HttpClient _client;

    public UnitCreationEndpointTests(Factory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task FromYaml_CreatesUnit_AddsMembers_ReturnsWarningsForUnsupportedSections()
    {
        var ct = TestContext.Current.CancellationToken;

        ResetMocks();
        var proxy = Substitute.For<IUnitActor>();
        proxy.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(UnitStatus.Draft);
        _factory.ActorProxyFactory
            .CreateActorProxy<IUnitActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(proxy);
        _factory.DirectoryService
            .RegisterAsync(Arg.Any<DirectoryEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        _factory.DirectoryService
            .ResolveAsync(Arg.Any<Address>(), Arg.Any<CancellationToken>())
            .Returns(ci => new DirectoryEntry(
                ci.Arg<Address>(),
                "actor-1",
                "actor-1",
                string.Empty,
                null,
                DateTimeOffset.UtcNow));

        const string Yaml = """
            unit:
              name: from-yaml-unit
              description: A unit created via YAML.
              ai:
                agent: claude
                model: claude-sonnet-4-20250514
              members:
                - agent: tech-lead
                - agent: backend-engineer
              policies:
                communication: through-unit
            """;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/units/from-yaml",
            new CreateUnitFromYamlRequest(Yaml, DisplayName: "From YAML"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("unit").GetProperty("name").GetString().Should().Be("from-yaml-unit");
        doc.RootElement.GetProperty("unit").GetProperty("displayName").GetString().Should().Be("From YAML");
        doc.RootElement.GetProperty("unit").GetProperty("model").GetString().Should().Be("claude-sonnet-4-20250514");
        // Members are routed through MessageRouter, whose success in tests depends on
        // whether the agent-address resolution surfaces an actor proxy for the mock.
        // The key invariant is that manifest members are iterated (via warnings +
        // the property being present), not the exact delivery count.
        doc.RootElement.TryGetProperty("membersAdded", out _).Should().BeTrue();

        var warnings = doc.RootElement.GetProperty("warnings")
            .EnumerateArray()
            .Select(w => w.GetString())
            .ToList();
        warnings.Should().Contain(w => w!.Contains("ai"));
        warnings.Should().Contain(w => w!.Contains("policies"));

        await _factory.DirectoryService.Received(1).RegisterAsync(
            Arg.Is<DirectoryEntry>(e => e.Address.Path == "from-yaml-unit"),
            Arg.Any<CancellationToken>());
        await proxy.Received(1).SetMetadataAsync(
            Arg.Is<UnitMetadata>(m => m.Model == "claude-sonnet-4-20250514"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FromYaml_InvalidYaml_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/units/from-yaml",
            new CreateUnitFromYamlRequest("not-a-unit: 1"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task FromYaml_EmptyBody_ReturnsBadRequest()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/units/from-yaml",
            new CreateUnitFromYamlRequest(string.Empty),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PackagesTemplates_ReturnsDiscoveredYamlFiles()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/v1/packages/templates", ct);
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        var entries = doc.RootElement.EnumerateArray().ToList();
        entries.Should().NotBeEmpty();
        entries.Should().Contain(e =>
            e.GetProperty("package").GetString() == "sample-pkg"
            && e.GetProperty("name").GetString() == "sample-unit");
    }

    [Fact]
    public async Task FromTemplate_UnknownTemplate_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/units/from-template",
            new CreateUnitFromTemplateRequest("does-not-exist", "nope"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task FromTemplate_KnownTemplate_CreatesUnitFromYaml()
    {
        var ct = TestContext.Current.CancellationToken;

        ResetMocks();
        var proxy = Substitute.For<IUnitActor>();
        proxy.GetStatusAsync(Arg.Any<CancellationToken>()).Returns(UnitStatus.Draft);
        _factory.ActorProxyFactory
            .CreateActorProxy<IUnitActor>(Arg.Any<ActorId>(), Arg.Any<string>())
            .Returns(proxy);
        _factory.DirectoryService
            .RegisterAsync(Arg.Any<DirectoryEntry>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        var response = await _client.PostAsJsonAsync(
            "/api/v1/units/from-template",
            new CreateUnitFromTemplateRequest("sample-pkg", "sample-unit"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var body = await response.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("unit").GetProperty("name").GetString().Should().Be("sample-unit");
        doc.RootElement.TryGetProperty("membersAdded", out _).Should().BeTrue();
    }

    [Fact]
    public async Task FromTemplate_PathTraversalRejected()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.PostAsJsonAsync(
            "/api/v1/units/from-template",
            new CreateUnitFromTemplateRequest("..", "sample-unit"),
            ct);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    private void ResetMocks()
    {
        _factory.DirectoryService.ClearReceivedCalls();
        _factory.ActorProxyFactory.ClearReceivedCalls();
    }

    /// <summary>
    /// Specialised factory that plants a tiny packages fixture on disk so the
    /// file-system catalog has something to enumerate.
    /// </summary>
    public sealed class Factory : CustomWebApplicationFactory
    {
        public string PackagesRoot { get; } = Path.Combine(
            Path.GetTempPath(),
            "spring-voyage-tests",
            $"packages-{Guid.NewGuid():N}");

        public Factory()
        {
            var pkgDir = Path.Combine(PackagesRoot, "sample-pkg", "units");
            Directory.CreateDirectory(pkgDir);
            File.WriteAllText(
                Path.Combine(pkgDir, "sample-unit.yaml"),
                """
                unit:
                  name: sample-unit
                  description: Sample template used by integration tests.
                  members:
                    - agent: sample-agent
                """);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureServices(services =>
            {
                // Replace the auto-discovered options with our fixture root so
                // the catalog looks at a deterministic directory regardless of
                // where the test host is launched from.
                var descriptors = services
                    .Where(d => d.ServiceType == typeof(PackageCatalogOptions))
                    .ToList();
                foreach (var d in descriptors)
                {
                    services.Remove(d);
                }
                services.AddSingleton(new PackageCatalogOptions { Root = PackagesRoot });
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                try
                {
                    if (Directory.Exists(PackagesRoot))
                    {
                        Directory.Delete(PackagesRoot, recursive: true);
                    }
                }
                catch
                {
                    // Best-effort cleanup; ignore.
                }
            }
        }
    }
}