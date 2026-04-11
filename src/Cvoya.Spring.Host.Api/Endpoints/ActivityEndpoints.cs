// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using System.Text.Json;

using Cvoya.Spring.Core.Capabilities;
using Cvoya.Spring.Core.Observability;
using Cvoya.Spring.Host.Api.Models;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Maps activity-related API endpoints for querying and streaming activity events.
/// </summary>
public static class ActivityEndpoints
{
    /// <summary>
    /// Registers activity endpoints on the specified endpoint route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group builder for chaining.</returns>
    public static RouteGroupBuilder MapActivityEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/activity")
            .WithTags("Activity");

        group.MapGet("/", QueryActivityAsync)
            .WithName("QueryActivity")
            .WithSummary("Query activity events with filters and pagination");

        group.MapGet("/stream", StreamActivityAsync)
            .WithName("StreamActivity")
            .WithSummary("Stream activity events via SSE");

        return group;
    }

    private static async Task<IResult> QueryActivityAsync(
        [AsParameters] ActivityQueryParametersDto query,
        IActivityQueryService queryService,
        CancellationToken cancellationToken)
    {
        var parameters = new ActivityQueryParameters(
            query.Source, query.EventType, query.Severity,
            query.From, query.To, query.Page ?? 1, query.PageSize ?? 50);
        var result = await queryService.QueryAsync(parameters, cancellationToken);
        return Results.Ok(result);
    }

    private static async Task StreamActivityAsync(
        HttpContext httpContext,
        IActivityObservable activityObservable,
        CancellationToken cancellationToken)
    {
        httpContext.Response.Headers.ContentType = "text/event-stream";
        httpContext.Response.Headers.CacheControl = "no-cache";
        httpContext.Response.Headers.Connection = "keep-alive";

        var tcs = new TaskCompletionSource();
        using var reg = cancellationToken.Register(() => tcs.TrySetCanceled());

        using var subscription = activityObservable.ActivityStream
            .Subscribe(
                evt =>
                {
                    var json = JsonSerializer.Serialize(evt);
                    // Fire-and-forget write within the subscription callback.
                    // HttpContext response writing is inherently sequential per request.
                    httpContext.Response.WriteAsync($"data: {json}\n\n", cancellationToken)
                        .ContinueWith(_ => httpContext.Response.Body.FlushAsync(cancellationToken), cancellationToken);
                },
                ex => tcs.TrySetException(ex),
                () => tcs.TrySetResult());

        try
        {
            await tcs.Task;
        }
        catch (OperationCanceledException)
        {
            // Client disconnected — expected.
        }
    }
}