/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Actors;

/// <summary>
/// Centralized constants for Dapr actor state keys.
/// Prevents typos and ensures consistency across parallel work.
/// </summary>
public static class StateKeys
{
    /// <summary>
    /// State key for the currently active conversation channel.
    /// </summary>
    public const string ActiveConversation = "Agent:ActiveConversation";

    /// <summary>
    /// State key for the list of pending conversation channels.
    /// </summary>
    public const string PendingConversations = "Agent:PendingConversations";

    /// <summary>
    /// State key for the observation channel (batched events).
    /// </summary>
    public const string ObservationChannel = "Agent:ObservationChannel";

    /// <summary>
    /// State key for the agent definition.
    /// </summary>
    public const string AgentDefinition = "Agent:Definition";

    /// <summary>
    /// State key for the agent's initiative state.
    /// </summary>
    public const string InitiativeState = "Agent:InitiativeState";

    /// <summary>
    /// State key for unit members.
    /// </summary>
    public const string Members = "Unit:Members";

    /// <summary>
    /// State key for unit policies.
    /// </summary>
    public const string Policies = "Unit:Policies";

    /// <summary>
    /// State key for the unit directory cache.
    /// </summary>
    public const string DirectoryCache = "Unit:DirectoryCache";

    /// <summary>
    /// State key for the unit definition.
    /// </summary>
    public const string UnitDefinition = "Unit:Definition";
}
