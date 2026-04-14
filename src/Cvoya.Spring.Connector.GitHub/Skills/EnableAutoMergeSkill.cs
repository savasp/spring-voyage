// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Connector.GitHub.Skills;

using System.Text.Json;

using Microsoft.Extensions.Logging;

using Octokit;

/// <summary>
/// Enables auto-merge on a pull request so it will be merged automatically once
/// branch-protection checks pass. GitHub exposes this capability only through
/// GraphQL's <c>enablePullRequestAutoMerge</c> mutation, so this skill resolves
/// the PR's node id via REST and then posts the mutation directly through
/// <see cref="IConnection"/>. Introducing the full <c>Octokit.GraphQL</c> client
/// would be overkill for a single mutation — a raw GraphQL request keeps the
/// dependency surface minimal.
/// </summary>
public class EnableAutoMergeSkill(IGitHubClient gitHubClient, ILoggerFactory loggerFactory)
{
    private readonly ILogger _logger = loggerFactory.CreateLogger<EnableAutoMergeSkill>();

    /// <summary>
    /// Enables auto-merge on the specified pull request.
    /// </summary>
    /// <param name="owner">The repository owner.</param>
    /// <param name="repo">The repository name.</param>
    /// <param name="number">The pull request number.</param>
    /// <param name="mergeMethod">Merge strategy to use when auto-merging: <c>merge</c>, <c>squash</c> (default), or <c>rebase</c>.</param>
    /// <param name="commitHeadline">Optional commit headline passed through to GitHub.</param>
    /// <param name="commitBody">Optional commit body passed through to GitHub.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The result as a JSON element confirming the auto-merge request.</returns>
    public async Task<JsonElement> ExecuteAsync(
        string owner,
        string repo,
        int number,
        string? mergeMethod,
        string? commitHeadline,
        string? commitBody,
        CancellationToken cancellationToken = default)
    {
        var graphqlMergeMethod = ParseMergeMethod(mergeMethod);

        _logger.LogInformation(
            "Enabling auto-merge on {Owner}/{Repo}#{Number} method={Method}",
            owner, repo, number, graphqlMergeMethod);

        var pr = await gitHubClient.PullRequest.Get(owner, repo, number);
        if (string.IsNullOrWhiteSpace(pr.NodeId))
        {
            throw new InvalidOperationException(
                $"PR {owner}/{repo}#{number} has no node id; cannot enable auto-merge.");
        }

        const string mutation = """
            mutation EnableAutoMerge($prId: ID!, $mergeMethod: PullRequestMergeMethod!, $headline: String, $body: String) {
              enablePullRequestAutoMerge(input: {
                pullRequestId: $prId,
                mergeMethod: $mergeMethod,
                commitHeadline: $headline,
                commitBody: $body
              }) {
                pullRequest { number autoMergeRequest { enabledAt mergeMethod } }
              }
            }
            """;

        var variables = new Dictionary<string, object?>
        {
            ["prId"] = pr.NodeId,
            ["mergeMethod"] = graphqlMergeMethod,
            ["headline"] = commitHeadline,
            ["body"] = commitBody,
        };

        var payload = new Dictionary<string, object?>
        {
            ["query"] = mutation,
            ["variables"] = variables,
        };

        var response = await gitHubClient.Connection.Post<JsonElement>(
            uri: new Uri("graphql", UriKind.Relative),
            body: payload,
            accepts: "application/json",
            contentType: "application/json",
            parameters: (IDictionary<string, string>?)null,
            cancellationToken: cancellationToken);

        var body = response.Body;

        // GraphQL errors come back in the `errors` array; surface them cleanly.
        if (body.ValueKind == JsonValueKind.Object
            && body.TryGetProperty("errors", out var errors)
            && errors.ValueKind == JsonValueKind.Array
            && errors.GetArrayLength() > 0)
        {
            var messages = errors.EnumerateArray()
                .Select(e => e.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "")
                .Where(m => !string.IsNullOrEmpty(m))
                .ToArray();
            throw new InvalidOperationException(
                "Failed to enable auto-merge: " + string.Join("; ", messages));
        }

        return JsonSerializer.SerializeToElement(new
        {
            enabled = true,
            number = pr.Number,
            node_id = pr.NodeId,
            merge_method = graphqlMergeMethod,
        });
    }

    private static string ParseMergeMethod(string? mergeMethod) =>
        (mergeMethod?.ToLowerInvariant()) switch
        {
            "merge" => "MERGE",
            "rebase" => "REBASE",
            _ => "SQUASH",
        };
}