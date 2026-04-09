/*
 * Copyright CVOYA LLC.
 *
 * This source code is proprietary and confidential.
 * Unauthorized copying, modification, distribution, or use of this file,
 * via any medium, is strictly prohibited without the prior written consent of CVOYA LLC.
 */

namespace Cvoya.Spring.Dapr.Execution;

/// <summary>
/// Configuration options for the AI provider.
/// </summary>
public class AiProviderOptions
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public const string SectionName = "AiProvider";

    /// <summary>
    /// The API key for authenticating with the AI provider.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The model identifier to use for completions.
    /// </summary>
    public string Model { get; set; } = "claude-sonnet-4-20250514";

    /// <summary>
    /// The maximum number of tokens to generate in a response.
    /// </summary>
    public int MaxTokens { get; set; } = 4096;

    /// <summary>
    /// The base URL for the AI provider API.
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.anthropic.com";
}
