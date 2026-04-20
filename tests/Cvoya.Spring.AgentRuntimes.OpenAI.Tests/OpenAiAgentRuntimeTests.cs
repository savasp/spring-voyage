// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.AgentRuntimes.OpenAI.Tests;

using System.Net;
using System.Text;

using Cvoya.Spring.AgentRuntimes.OpenAI;
using Cvoya.Spring.Core.AgentRuntimes;

using Microsoft.Extensions.Logging.Abstractions;

using NSubstitute;

using Shouldly;

using Xunit;

public class OpenAiAgentRuntimeTests
{
    private static readonly OpenAiAgentRuntimeSeed TestSeed = new(
        Models: new[] { "gpt-4o", "gpt-4o-mini", "o3-mini" },
        DefaultModel: "gpt-4o",
        BaseUrl: "https://api.openai.com");

    [Fact]
    public void Identity_MatchesContract()
    {
        var runtime = CreateRuntime(new StubHandler());

        runtime.Id.ShouldBe("openai");
        runtime.DisplayName.ShouldBe("OpenAI (dapr-agent + OpenAI API)");
        runtime.ToolKind.ShouldBe("dapr-agent");
    }

    [Fact]
    public void CredentialSchema_IsApiKey_WithDisplayHint()
    {
        var runtime = CreateRuntime(new StubHandler());

        runtime.CredentialSchema.Kind.ShouldBe(AgentRuntimeCredentialKind.ApiKey);
        runtime.CredentialSchema.DisplayHint.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void DefaultModels_MatchesSeed()
    {
        var runtime = CreateRuntime(new StubHandler());

        runtime.DefaultModels.Select(m => m.Id).ShouldBe(new[] { "gpt-4o", "gpt-4o-mini", "o3-mini" });
        // The seed does not declare per-model context windows today.
        runtime.DefaultModels.ShouldAllBe(m => m.ContextWindow == null);
    }

    [Fact]
    public async Task ValidateCredentialAsync_BlankCredential_ReturnsInvalid_NoHttpCall()
    {
        var handler = new StubHandler();
        var runtime = CreateRuntime(handler);

        var result = await runtime.ValidateCredentialAsync("   ", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
        result.Status.ShouldBe(CredentialValidationStatus.Invalid);
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        handler.CallCount.ShouldBe(0);
    }

    [Fact]
    public async Task ValidateCredentialAsync_200_ReturnsValid()
    {
        var handler = new StubHandler();
        handler.Add("api.openai.com", HttpStatusCode.OK, "{\"data\":[]}");
        var runtime = CreateRuntime(handler);

        var result = await runtime.ValidateCredentialAsync("sk-good", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeTrue();
        result.Status.ShouldBe(CredentialValidationStatus.Valid);
        result.ErrorMessage.ShouldBeNull();

        handler.LastRequest!.RequestUri!.AbsolutePath.ShouldBe("/v1/models");
        handler.LastRequest!.Headers.GetValues("Authorization").ShouldContain("Bearer sk-good");
    }

    [Fact]
    public async Task ValidateCredentialAsync_401_ReturnsInvalid_WithBodySurfaced()
    {
        const string body = "{\"error\":{\"message\":\"Incorrect API key provided.\",\"code\":\"invalid_api_key\"}}";
        var handler = new StubHandler();
        handler.Add("api.openai.com", HttpStatusCode.Unauthorized, body);
        var runtime = CreateRuntime(handler);

        var result = await runtime.ValidateCredentialAsync("sk-bad", TestContext.Current.CancellationToken);

        result.Valid.ShouldBeFalse();
        result.Status.ShouldBe(CredentialValidationStatus.Invalid);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("401");
        result.ErrorMessage.ShouldContain("Incorrect API key provided.");
    }

    [Fact]
    public async Task ValidateCredentialAsync_403_ReturnsInvalid()
    {
        var handler = new StubHandler();
        handler.Add("api.openai.com", HttpStatusCode.Forbidden, "{}");
        var runtime = CreateRuntime(handler);

        var result = await runtime.ValidateCredentialAsync("sk-restricted", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CredentialValidationStatus.Invalid);
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ValidateCredentialAsync_500_ReturnsNetworkError()
    {
        var handler = new StubHandler();
        handler.Add("api.openai.com", HttpStatusCode.InternalServerError, "upstream blew up");
        var runtime = CreateRuntime(handler);

        var result = await runtime.ValidateCredentialAsync("sk-anything", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CredentialValidationStatus.NetworkError);
        result.ErrorMessage.ShouldNotBeNull();
        result.ErrorMessage!.ShouldContain("500");
    }

    [Fact]
    public async Task ValidateCredentialAsync_503_ReturnsNetworkError()
    {
        var handler = new StubHandler();
        handler.Add("api.openai.com", HttpStatusCode.ServiceUnavailable, "");
        var runtime = CreateRuntime(handler);

        var result = await runtime.ValidateCredentialAsync("sk-anything", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CredentialValidationStatus.NetworkError);
    }

    [Fact]
    public async Task ValidateCredentialAsync_NetworkException_ReturnsNetworkError()
    {
        var runtime = CreateRuntime(new ThrowingHandler(new HttpRequestException("DNS lookup failed")));

        var result = await runtime.ValidateCredentialAsync("sk-anything", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CredentialValidationStatus.NetworkError);
        result.ErrorMessage.ShouldNotBeNullOrWhiteSpace();
        result.ErrorMessage!.ShouldContain("DNS lookup failed");
    }

    [Fact]
    public async Task ValidateCredentialAsync_TimeoutException_ReturnsNetworkError()
    {
        var runtime = CreateRuntime(new ThrowingHandler(new TaskCanceledException("timeout")));

        var result = await runtime.ValidateCredentialAsync("sk-anything", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CredentialValidationStatus.NetworkError);
    }

    [Fact]
    public async Task ValidateCredentialAsync_RespectsExternalCancellation()
    {
        var runtime = CreateRuntime(new ThrowingHandler(
            new TaskCanceledException("the caller cancelled")));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Should.ThrowAsync<OperationCanceledException>(async () =>
            await runtime.ValidateCredentialAsync("sk-x", cts.Token));
    }

    [Fact]
    public async Task ValidateCredentialAsync_HonoursSeedBaseUrl()
    {
        var seed = TestSeed with { BaseUrl = "https://openai.proxy.example/v1-prefix" };
        var handler = new StubHandler();
        // The base URL host is the only routing key for the stub, so
        // a 200 from openai.proxy.example proves the runtime did not
        // hardcode api.openai.com when the seed pinned a different value.
        handler.Add("openai.proxy.example", HttpStatusCode.OK, "{\"data\":[]}");
        var runtime = CreateRuntime(handler, seed);

        var result = await runtime.ValidateCredentialAsync("sk-good", TestContext.Current.CancellationToken);

        result.Status.ShouldBe(CredentialValidationStatus.Valid);
        handler.LastRequest!.RequestUri!.Host.ShouldBe("openai.proxy.example");
        handler.LastRequest.RequestUri.AbsolutePath.ShouldBe("/v1-prefix/v1/models");
    }

    [Fact]
    public async Task VerifyContainerBaselineAsync_FailsWhenDaprActorsAbsent()
    {
        // This test project does not reference Dapr.Actors, so the
        // baseline check must surface the missing dependency rather
        // than silently passing. The pass-path is exercised by the
        // sibling test in Cvoya.Spring.Integration.Tests, which
        // transitively loads Dapr.Actors via Cvoya.Spring.Dapr.
        AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(a => string.Equals(a.GetName().Name, "Dapr.Actors", StringComparison.OrdinalIgnoreCase))
            .ShouldBeFalse(
                "Test invariant: this project must not transitively reference Dapr.Actors. " +
                "If it does, the baseline-failure assertion below is no longer valid.");

        var runtime = CreateRuntime(new StubHandler());

        var result = await runtime.VerifyContainerBaselineAsync(TestContext.Current.CancellationToken);

        result.Passed.ShouldBeFalse();
        result.Errors.Count.ShouldBe(1);
        result.Errors[0].ShouldContain("Dapr.Actors");
    }

    private static OpenAiAgentRuntime CreateRuntime(
        HttpMessageHandler handler,
        OpenAiAgentRuntimeSeed? seed = null)
    {
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(Arg.Any<string>())
            .Returns(_ => new HttpClient(handler, disposeHandler: false));

        return new OpenAiAgentRuntime(
            factory,
            NullLogger<OpenAiAgentRuntime>.Instance,
            () => seed ?? TestSeed);
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, (HttpStatusCode Status, string Body)> _responses = new();

        public HttpRequestMessage? LastRequest { get; private set; }
        public int CallCount { get; private set; }

        public void Add(string host, HttpStatusCode status, string body) =>
            _responses[host] = (status, body);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequest = request;

            var host = request.RequestUri?.Host ?? string.Empty;
            if (!_responses.TryGetValue(host, out var r))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent($"no stub for {host}"),
                });
            }

            return Task.FromResult(new HttpResponseMessage(r.Status)
            {
                Content = new StringContent(r.Body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }
}