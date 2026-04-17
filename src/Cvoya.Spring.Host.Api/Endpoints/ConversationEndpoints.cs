// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Host.Api.Endpoints;

using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Core.Observability;
using Cvoya.Spring.Host.Api.Auth;
using Cvoya.Spring.Host.Api.Models;

using Microsoft.AspNetCore.Mvc;

/// <summary>
/// Maps the conversation read + send endpoints introduced by #452. Conversations
/// are a projection of the activity-event store — see
/// <see cref="IConversationQueryService"/> — so these endpoints stay thin: they
/// delegate reads to the query service and threaded sends to the existing
/// <see cref="IMessageRouter"/>, stamping the path's conversation id onto the
/// outbound message.
/// </summary>
public static class ConversationEndpoints
{
    /// <summary>
    /// Registers conversation endpoints on the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group for chaining.</returns>
    public static RouteGroupBuilder MapConversationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/conversations")
            .WithTags("Conversations");

        group.MapGet("/", ListConversationsAsync)
            .WithName("ListConversations")
            .WithSummary("List conversations derived from the activity event stream")
            .Produces<IReadOnlyList<ConversationSummary>>(StatusCodes.Status200OK);

        group.MapGet("/{id}", GetConversationAsync)
            .WithName("GetConversation")
            .WithSummary("Get a single conversation thread (summary + ordered events)")
            .Produces<ConversationDetail>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPost("/{id}/messages", PostConversationMessageAsync)
            .WithName("PostConversationMessage")
            .WithSummary("Thread a new message into an existing conversation")
            .Produces<ConversationMessageResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        return group;
    }

    private static async Task<IResult> ListConversationsAsync(
        [AsParameters] ConversationListQuery query,
        IConversationQueryService queryService,
        CancellationToken cancellationToken)
    {
        var filters = new ConversationQueryFilters(
            Unit: query.Unit,
            Agent: query.Agent,
            Status: query.Status,
            Participant: query.Participant,
            Limit: query.Limit);

        var summaries = await queryService.ListAsync(filters, cancellationToken);
        return Results.Ok(summaries);
    }

    private static async Task<IResult> GetConversationAsync(
        string id,
        IConversationQueryService queryService,
        CancellationToken cancellationToken)
    {
        var detail = await queryService.GetAsync(id, cancellationToken);
        if (detail is null)
        {
            return Results.Problem(
                detail: $"Conversation '{id}' not found",
                statusCode: StatusCodes.Status404NotFound);
        }

        return Results.Ok(detail);
    }

    private static async Task<IResult> PostConversationMessageAsync(
        string id,
        ConversationMessageRequest request,
        IMessageRouter messageRouter,
        IAuthenticatedCallerAccessor callerAccessor,
        CancellationToken cancellationToken)
    {
        if (request is null || request.To is null || string.IsNullOrWhiteSpace(request.To.Scheme))
        {
            return Results.Problem(
                detail: "Request body must include a destination address (to.scheme and to.path).",
                statusCode: StatusCodes.Status400BadRequest);
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return Results.Problem(
                detail: "Conversation id is required.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var from = callerAccessor.GetHumanAddress();
        var to = new Address(request.To.Scheme, request.To.Path ?? string.Empty);
        var messageId = Guid.NewGuid();

        // Wrap the text as a Domain payload — same shape as SendMessage.
        var payload = System.Text.Json.JsonSerializer.SerializeToElement(request.Text ?? string.Empty);
        var message = new Message(
            messageId,
            from,
            to,
            MessageType.Domain,
            id,
            payload,
            DateTimeOffset.UtcNow);

        var result = await messageRouter.RouteAsync(message, cancellationToken);
        if (!result.IsSuccess)
        {
            var error = result.Error!;
            var statusCode = error.Code switch
            {
                "ADDRESS_NOT_FOUND" => StatusCodes.Status404NotFound,
                "PERMISSION_DENIED" => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status502BadGateway,
            };
            return Results.Problem(detail: error.Message, statusCode: statusCode);
        }

        return Results.Ok(new ConversationMessageResponse(messageId, id, result.Value?.Payload));
    }
}

/// <summary>
/// Maps the inbox endpoint introduced by #456. The inbox is a filtered view of
/// <see cref="IConversationQueryService.ListInboxAsync"/> scoped to the
/// authenticated caller's <c>human://</c> address. "Respond" is explicitly not
/// a separate endpoint: it's a thin wrapper over
/// <see cref="ConversationEndpoints.MapConversationEndpoints"/> — the
/// <c>POST /api/v1/conversations/{id}/messages</c> call — so we don't fork the
/// message-send contract.
/// </summary>
public static class InboxEndpoints
{
    /// <summary>
    /// Registers inbox endpoints on the supplied route builder.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The route group for chaining.</returns>
    public static RouteGroupBuilder MapInboxEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/inbox")
            .WithTags("Inbox");

        group.MapGet("/", ListInboxAsync)
            .WithName("ListInbox")
            .WithSummary("List conversations awaiting the current human caller")
            .Produces<IReadOnlyList<InboxItem>>(StatusCodes.Status200OK);

        return group;
    }

    private static async Task<IResult> ListInboxAsync(
        IConversationQueryService queryService,
        IAuthenticatedCallerAccessor callerAccessor,
        CancellationToken cancellationToken)
    {
        var caller = callerAccessor.GetHumanAddress();
        var items = await queryService.ListInboxAsync(
            $"{caller.Scheme}://{caller.Path}",
            cancellationToken);
        return Results.Ok(items);
    }
}