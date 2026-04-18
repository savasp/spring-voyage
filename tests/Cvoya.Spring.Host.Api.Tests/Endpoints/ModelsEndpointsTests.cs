// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Tests.Endpoints;

using System.Net;
using System.Net.Http.Json;

using Cvoya.Spring.Host.Api.Endpoints;

using Shouldly;

using Xunit;

/// <summary>
/// Integration tests for the dynamic-model-catalog endpoint
/// (<c>GET /api/v1/models/{provider}</c>). See issue #597.
/// </summary>
public class ModelsEndpointsTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ModelsEndpointsTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task ListModels_UnknownProvider_ReturnsEmptyList()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/v1/models/no-such-provider", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ModelsResponse>(ct);
        body.ShouldNotBeNull();
        body!.Provider.ShouldBe("no-such-provider");
        body.Models.ShouldBeEmpty();
    }

    [Fact]
    public async Task ListModels_Google_AlwaysReturnsStaticFallback()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/v1/models/google", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ModelsResponse>(ct);
        body.ShouldNotBeNull();
        body!.Provider.ShouldBe("google");
        body.Models.ShouldContain("gemini-2.5-pro");
        body.Models.ShouldContain("gemini-2.5-flash");
    }

    [Fact]
    public async Task ListModels_Claude_WithoutApiKey_ReturnsStaticFallback()
    {
        // The integration-test harness runs without provider credentials
        // configured. The catalog logs an info message and returns the
        // static list — the endpoint should therefore always succeed.
        var ct = TestContext.Current.CancellationToken;

        var response = await _client.GetAsync("/api/v1/models/claude", ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ModelsResponse>(ct);
        body.ShouldNotBeNull();
        body!.Provider.ShouldBe("claude");
        body.Models.ShouldContain("claude-sonnet-4-20250514");
    }
}