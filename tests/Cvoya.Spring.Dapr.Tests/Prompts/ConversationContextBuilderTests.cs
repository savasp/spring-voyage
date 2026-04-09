/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Tests.Prompts;

using System.Text.Json;
using Cvoya.Spring.Core.Messaging;
using Cvoya.Spring.Dapr.Prompts;
using FluentAssertions;
using Xunit;

/// <summary>
/// Unit tests for <see cref="ConversationContextBuilder"/>.
/// </summary>
public class ConversationContextBuilderTests
{
    private readonly ConversationContextBuilder _builder = new();

    private static Message CreateMessage(string senderPath, string text)
    {
        return new Message(
            Guid.NewGuid(),
            new Address("agent", senderPath),
            new Address("agent", "receiver"),
            MessageType.Domain,
            "conv-1",
            JsonSerializer.SerializeToElement(new { text }),
            DateTimeOffset.UtcNow);
    }

    /// <summary>
    /// Verifies that prior messages are included in the output.
    /// </summary>
    [Fact]
    public void Build_IncludesPriorMessages()
    {
        var messages = new List<Message>
        {
            CreateMessage("team/alice", "Hello there"),
            CreateMessage("team/bob", "Hi Alice")
        };

        var result = _builder.Build(messages, null);

        result.Should().Contain("Prior Messages");
        result.Should().Contain("agent://team/alice");
        result.Should().Contain("Hello there");
        result.Should().Contain("Hi Alice");
    }

    /// <summary>
    /// Verifies that checkpoint state is included in the output.
    /// </summary>
    [Fact]
    public void Build_IncludesCheckpointState()
    {
        var result = _builder.Build([], "Step 3 of 5 completed");

        result.Should().Contain("Last Checkpoint");
        result.Should().Contain("Step 3 of 5 completed");
    }

    /// <summary>
    /// Verifies that empty conversation produces an empty string.
    /// </summary>
    [Fact]
    public void Build_HandlesEmptyConversation()
    {
        var result = _builder.Build([], null);

        result.Should().BeEmpty();
    }
}
