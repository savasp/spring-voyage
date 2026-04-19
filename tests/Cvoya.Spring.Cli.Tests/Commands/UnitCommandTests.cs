// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Tests.Commands;

using Cvoya.Spring.Cli.Commands;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for the free-function validators on
/// <see cref="UnitCommand"/>. The action pipelines themselves are
/// exercised end-to-end elsewhere — these tests pin the validation
/// contract so <c>spring unit create</c> and
/// <c>spring unit create-from-template</c> reject mis-composed flag
/// sets consistently (#598).
/// </summary>
public class UnitCommandTests
{
    // Canonical rejection message — operators read this verbatim when
    // they combine --provider / --model with a non-dapr-agent tool.
    private const string ExpectedErrorMessage =
        "--provider and --model are only meaningful for --tool=dapr-agent; " +
        "other tools (claude-code, codex, gemini) have their provider hardcoded in the tool CLI.";

    [Theory]
    [InlineData("dapr-agent", "openai", "gpt-4o")]
    [InlineData("dapr-agent", "anthropic", null)]
    [InlineData("dapr-agent", null, "claude-sonnet-4-20250514")]
    [InlineData("dapr-agent", null, null)]
    public void ValidateProviderModelAgainstTool_DaprAgent_Accepts(
        string tool,
        string? provider,
        string? model)
    {
        UnitCommand.ValidateProviderModelAgainstTool(tool, provider, model)
            .ShouldBeNull();
    }

    [Theory]
    [InlineData(null, null, null)]
    [InlineData(null, null, "")]
    // No --tool supplied and no provider/model either — nothing to reject.
    [InlineData("", null, null)]
    public void ValidateProviderModelAgainstTool_NoFlags_Accepts(
        string? tool,
        string? provider,
        string? model)
    {
        UnitCommand.ValidateProviderModelAgainstTool(tool, provider, model)
            .ShouldBeNull();
    }

    [Theory]
    [InlineData("claude-code", "anthropic", null)]
    [InlineData("claude-code", null, "claude-sonnet-4-20250514")]
    [InlineData("claude-code", "anthropic", "claude-sonnet-4-20250514")]
    [InlineData("codex", "openai", "gpt-4o")]
    [InlineData("codex", "openai", null)]
    [InlineData("gemini", "google", "gemini-2.5-pro")]
    [InlineData("gemini", null, "gemini-2.5-pro")]
    [InlineData("custom", "ollama", "llama3.2:3b")]
    public void ValidateProviderModelAgainstTool_NonDaprAgent_RejectsWithCanonicalMessage(
        string tool,
        string? provider,
        string? model)
    {
        var error = UnitCommand.ValidateProviderModelAgainstTool(tool, provider, model);
        error.ShouldBe(ExpectedErrorMessage);
    }

    [Fact]
    public void ValidateProviderModelAgainstTool_CaseInsensitive_OnTool()
    {
        // The option's allow-list is lowercase but operators sometimes
        // type "Dapr-Agent"; the validator must normalise before the
        // check so they're not rejected for a casing accident.
        UnitCommand.ValidateProviderModelAgainstTool(
            "Dapr-Agent",
            provider: "openai",
            model: "gpt-4o")
            .ShouldBeNull();
    }

    [Fact]
    public void ValidateProviderModelAgainstTool_NoToolProvided_DoesNotSecondGuessServerDefault()
    {
        // When --tool is omitted the server picks the deployment default
        // (claude-code in today's build). The CLI must not assume that
        // default — rejecting --provider in that case would mean
        // operators who pin provider + model without passing --tool hit
        // a confusing error. The server already enforces the honest
        // contract at dispatch time.
        UnitCommand.ValidateProviderModelAgainstTool(
            tool: null,
            provider: "openai",
            model: "gpt-4o")
            .ShouldBeNull();
    }

    // -----------------------------------------------------------------
    // #626: inline credential flag resolution
    // -----------------------------------------------------------------

    [Theory]
    [InlineData("claude-code", null, "anthropic")]
    [InlineData("codex", null, "openai")]
    [InlineData("gemini", null, "google")]
    [InlineData("dapr-agent", "anthropic", "anthropic")]
    [InlineData("dapr-agent", "claude", "anthropic")]
    [InlineData("dapr-agent", "openai", "openai")]
    [InlineData("dapr-agent", "google", "google")]
    [InlineData("dapr-agent", "gemini", "google")]
    [InlineData("dapr-agent", "ollama", null)]
    [InlineData("custom", "openai", null)]
    [InlineData(null, null, null)]
    public void DeriveRequiredCredentialProvider_MatchesMatrix(
        string? tool,
        string? provider,
        string? expected)
    {
        UnitCommand.DeriveRequiredCredentialProvider(tool, provider)
            .ShouldBe(expected);
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_NoFlags_ReturnsNone()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "claude-code",
            provider: null,
            apiKey: null,
            apiKeyFromFile: null,
            saveAsTenantDefault: false,
            CancellationToken.None);
        result.ErrorMessage.ShouldBeNull();
        result.SecretName.ShouldBeNull();
        result.Key.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_RejectsBothKeyFlagsTogether()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "claude-code",
            provider: null,
            apiKey: "sk-test",
            apiKeyFromFile: "some-path",
            saveAsTenantDefault: false,
            CancellationToken.None);
        result.ErrorMessage!.ShouldContain("mutually exclusive");
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_RejectsSaveFlagWithoutKey()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "claude-code",
            provider: null,
            apiKey: null,
            apiKeyFromFile: null,
            saveAsTenantDefault: true,
            CancellationToken.None);
        result.ErrorMessage!.ShouldContain("--save-as-tenant-default requires");
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_RejectsKeyOnOllamaProvider()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "dapr-agent",
            provider: "ollama",
            apiKey: "sk-test",
            apiKeyFromFile: null,
            saveAsTenantDefault: false,
            CancellationToken.None);
        result.ErrorMessage!.ShouldContain("Ollama");
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_RejectsKeyOnCustomTool()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "custom",
            provider: null,
            apiKey: "sk-test",
            apiKeyFromFile: null,
            saveAsTenantDefault: false,
            CancellationToken.None);
        result.ErrorMessage!.ShouldContain("custom");
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_AcceptsInlineKey_ClaudeCode()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "claude-code",
            provider: null,
            apiKey: "sk-ant-xyz",
            apiKeyFromFile: null,
            saveAsTenantDefault: false,
            CancellationToken.None);
        result.ErrorMessage.ShouldBeNull();
        result.Key.ShouldBe("sk-ant-xyz");
        result.SecretName.ShouldBe("anthropic-api-key");
        result.SaveAsTenantDefault.ShouldBeFalse();
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_AcceptsSaveAsTenantDefaultToggle()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "codex",
            provider: null,
            apiKey: "sk-openai",
            apiKeyFromFile: null,
            saveAsTenantDefault: true,
            CancellationToken.None);
        result.ErrorMessage.ShouldBeNull();
        result.Key.ShouldBe("sk-openai");
        result.SecretName.ShouldBe("openai-api-key");
        result.SaveAsTenantDefault.ShouldBeTrue();
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_ReadsKeyFromFile_StripsTrailingNewline()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "sk-file-key\n", TestContext.Current.CancellationToken);
            var result = await UnitCommand.ResolveCredentialOptionsAsync(
                tool: "gemini",
                provider: null,
                apiKey: null,
                apiKeyFromFile: path,
                saveAsTenantDefault: false,
                TestContext.Current.CancellationToken);
            result.ErrorMessage.ShouldBeNull();
            result.Key.ShouldBe("sk-file-key");
            result.SecretName.ShouldBe("google-api-key");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_RejectsMissingFile()
    {
        var result = await UnitCommand.ResolveCredentialOptionsAsync(
            tool: "claude-code",
            provider: null,
            apiKey: null,
            apiKeyFromFile: "/tmp/does-not-exist-please-really",
            saveAsTenantDefault: false,
            CancellationToken.None);
        result.ErrorMessage!.ShouldContain("Failed to read");
    }

    [Fact]
    public async Task ResolveCredentialOptionsAsync_RejectsEmptyKey()
    {
        var path = Path.GetTempFileName();
        try
        {
            await File.WriteAllTextAsync(path, "\n\n", TestContext.Current.CancellationToken);
            var result = await UnitCommand.ResolveCredentialOptionsAsync(
                tool: "claude-code",
                provider: null,
                apiKey: null,
                apiKeyFromFile: path,
                saveAsTenantDefault: false,
                TestContext.Current.CancellationToken);
            result.ErrorMessage!.ShouldContain("empty");
        }
        finally
        {
            File.Delete(path);
        }
    }
}