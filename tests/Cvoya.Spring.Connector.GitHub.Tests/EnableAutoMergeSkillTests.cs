// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Connector.GitHub.Tests;

using System.Net;
using System.Text.Json;

using Cvoya.Spring.Connector.GitHub.Skills;

using Microsoft.Extensions.Logging;

using NSubstitute;

using Octokit;
using Octokit.Internal;

using Shouldly;

using Xunit;

public class EnableAutoMergeSkillTests
{
    private readonly IGitHubClient _gitHubClient;
    private readonly IConnection _connection;
    private readonly EnableAutoMergeSkill _skill;

    public EnableAutoMergeSkillTests()
    {
        _gitHubClient = Substitute.For<IGitHubClient>();
        _connection = Substitute.For<IConnection>();
        _gitHubClient.Connection.Returns(_connection);
        var loggerFactory = Substitute.For<ILoggerFactory>();
        loggerFactory.CreateLogger(Arg.Any<string>()).Returns(Substitute.For<ILogger>());
        _skill = new EnableAutoMergeSkill(_gitHubClient, loggerFactory);
    }

    [Fact]
    public async Task ExecuteAsync_PostsGraphQlMutation_ReturnsEnabled()
    {
        _gitHubClient.PullRequest.Get("owner", "repo", 42)
            .Returns(PrTestHelpers.CreatePullRequest(42, nodeId: "PR_node123"));

        Uri? capturedUri = null;
        object? capturedBody = null;
        var successBody = JsonSerializer.SerializeToElement(new
        {
            data = new
            {
                enablePullRequestAutoMerge = new { pullRequest = new { number = 42 } },
            },
        });
        var response = Substitute.For<IResponse>();
        response.StatusCode.Returns(HttpStatusCode.OK);
        var apiResp = new ApiResponse<JsonElement>(response, successBody);

        _connection
            .Post<JsonElement>(
                Arg.Do<Uri>(u => capturedUri = u),
                Arg.Do<object>(b => capturedBody = b),
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<IDictionary<string, string>?>(),
                Arg.Any<CancellationToken>())
            .Returns(apiResp);

        var result = await _skill.ExecuteAsync(
            "owner", "repo", 42, mergeMethod: "squash",
            commitHeadline: "Headline", commitBody: "Body",
            TestContext.Current.CancellationToken);

        capturedUri.ShouldNotBeNull();
        capturedUri!.ToString().ShouldBe("graphql");

        var body = capturedBody as Dictionary<string, object?>;
        body.ShouldNotBeNull();
        body!.ShouldContainKey("query");
        body.ShouldContainKey("variables");
        var vars = (Dictionary<string, object?>)body["variables"]!;
        vars["prId"].ShouldBe("PR_node123");
        vars["mergeMethod"].ShouldBe("SQUASH");

        result.GetProperty("enabled").GetBoolean().ShouldBeTrue();
        result.GetProperty("node_id").GetString().ShouldBe("PR_node123");
        result.GetProperty("merge_method").GetString().ShouldBe("SQUASH");
    }

    [Fact]
    public async Task ExecuteAsync_GraphQlErrors_ThrowsInvalidOperationException()
    {
        _gitHubClient.PullRequest.Get("owner", "repo", 42)
            .Returns(PrTestHelpers.CreatePullRequest(42, nodeId: "PR_node123"));

        var errorBody = JsonSerializer.SerializeToElement(new
        {
            errors = new[] { new { message = "auto merge not enabled for this repo" } },
        });
        var response = Substitute.For<IResponse>();
        response.StatusCode.Returns(HttpStatusCode.OK);
        var apiResp = new ApiResponse<JsonElement>(response, errorBody);

        _connection
            .Post<JsonElement>(
                Arg.Any<Uri>(), Arg.Any<object>(), Arg.Any<string>(), Arg.Any<string>(),
                Arg.Any<IDictionary<string, string>?>(), Arg.Any<CancellationToken>())
            .Returns(apiResp);

        var ex = await Should.ThrowAsync<InvalidOperationException>(() =>
            _skill.ExecuteAsync(
                "owner", "repo", 42, mergeMethod: null, commitHeadline: null, commitBody: null,
                TestContext.Current.CancellationToken));
        ex.Message.ShouldContain("auto merge not enabled for this repo");
    }

    [Fact]
    public async Task ExecuteAsync_MissingNodeId_Throws()
    {
        _gitHubClient.PullRequest.Get("owner", "repo", 42)
            .Returns(PrTestHelpers.CreatePullRequest(42, nodeId: null));

        await Should.ThrowAsync<InvalidOperationException>(() =>
            _skill.ExecuteAsync(
                "owner", "repo", 42, mergeMethod: null, commitHeadline: null, commitBody: null,
                TestContext.Current.CancellationToken));
    }
}