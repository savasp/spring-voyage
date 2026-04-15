// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Execution;

using System.Net;
using System.Text;

using Cvoya.Spring.Dapr.Execution;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for <see cref="OllamaHealthCheck"/>.
/// </summary>
public class OllamaHealthCheckTests
{
    private static OllamaHealthCheck CreateCheck(
        HttpMessageHandler handler,
        OllamaOptions options,
        ILogger<OllamaHealthCheck>? logger = null)
    {
        var httpClient = new HttpClient(handler);
        return new OllamaHealthCheck(
            httpClient,
            Options.Create(options),
            logger ?? Substitute.For<ILogger<OllamaHealthCheck>>());
    }

    [Fact]
    public async Task StartAsync_TagsEndpointReturns200_LogsHealthy()
    {
        var logger = Substitute.For<ILogger<OllamaHealthCheck>>();
        var handler = new CannedHandler(HttpStatusCode.OK, "{\"models\":[]}");
        var options = new OllamaOptions { BaseUrl = "http://ollama:11434" };
        var check = CreateCheck(handler, options, logger);

        await check.StartAsync(TestContext.Current.CancellationToken);

        handler.LastRequest!.RequestUri!.ToString().ShouldBe("http://ollama:11434/api/tags");
        logger.Received().Log(
            LogLevel.Information,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_ConnectionRefused_LogsWarningByDefault()
    {
        var logger = Substitute.For<ILogger<OllamaHealthCheck>>();
        var handler = new ThrowingHandler(new HttpRequestException("connection refused"));
        var options = new OllamaOptions { BaseUrl = "http://ollama:11434" };
        var check = CreateCheck(handler, options, logger);

        // Should not throw — RequireHealthyAtStartup defaults to false.
        await check.StartAsync(TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_ConnectionRefusedAndRequireHealthy_Throws()
    {
        var handler = new ThrowingHandler(new HttpRequestException("connection refused"));
        var options = new OllamaOptions
        {
            BaseUrl = "http://ollama:11434",
            RequireHealthyAtStartup = true
        };
        var check = CreateCheck(handler, options);

        var act = () => check.StartAsync(TestContext.Current.CancellationToken);

        var ex = await Should.ThrowAsync<InvalidOperationException>(act);
        ex.Message.ShouldContain("could not reach");
        ex.Message.ShouldContain("RequireHealthyAtStartup is true");
    }

    [Fact]
    public async Task StartAsync_NonSuccessStatus_LogsWarning()
    {
        var logger = Substitute.For<ILogger<OllamaHealthCheck>>();
        var handler = new CannedHandler(HttpStatusCode.InternalServerError, "oops");
        var options = new OllamaOptions { BaseUrl = "http://ollama:11434" };
        var check = CreateCheck(handler, options, logger);

        await check.StartAsync(TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StartAsync_Timeout_LogsWarning()
    {
        var logger = Substitute.For<ILogger<OllamaHealthCheck>>();
        var handler = new HangingHandler();
        var options = new OllamaOptions
        {
            BaseUrl = "http://ollama:11434",
            HealthCheckTimeoutSeconds = 1
        };
        var check = CreateCheck(handler, options, logger);

        await check.StartAsync(TestContext.Current.CancellationToken);

        logger.Received().Log(
            LogLevel.Warning,
            Arg.Any<EventId>(),
            Arg.Any<object>(),
            Arg.Any<Exception?>(),
            Arg.Any<Func<object, Exception?, string>>());
    }

    [Fact]
    public async Task StopAsync_IsNoOp()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "{}");
        var check = CreateCheck(handler, new OllamaOptions());

        // Should complete without throwing.
        await check.StopAsync(TestContext.Current.CancellationToken);
    }

    private sealed class CannedHandler(HttpStatusCode status, string body) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            throw exception;
        }
    }

    private sealed class HangingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}