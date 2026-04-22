// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Tests.Commands;

using System.CommandLine;

using Cvoya.Spring.Cli.Commands;

using Shouldly;

using Xunit;

/// <summary>
/// Parser-level tests for the <c>spring agent-runtime</c> verb family
/// (#688). Verifies the subcommand tree wires up without regressions;
/// wire-level behaviour against the API surface is covered by the
/// integration tests in <c>Cvoya.Spring.Host.Api.Tests</c>.
/// </summary>
public class AgentRuntimeCommandTests
{
    private static Option<string> CreateOutputOption()
    {
        // Mirror Program.cs's recursive binding (#1067) so subcommand-level
        // `--output json` (e.g. `agent-runtime config get <id> --output json`)
        // parses cleanly under the test root the same way it does in the
        // real CLI.
        return new Option<string>("--output", "-o")
        {
            Description = "Output format",
            DefaultValueFactory = _ => "table",
            Recursive = true,
        };
    }

    [Theory]
    [InlineData("agent-runtime list")]
    [InlineData("agent-runtime show claude")]
    [InlineData("agent-runtime install claude")]
    [InlineData("agent-runtime install claude --model claude-opus-4-7 --default-model claude-opus-4-7")]
    [InlineData("agent-runtime uninstall claude --force")]
    [InlineData("agent-runtime models list claude")]
    [InlineData("agent-runtime models set claude claude-opus-4-7,claude-sonnet-4-6")]
    [InlineData("agent-runtime models add claude claude-haiku-4-5")]
    [InlineData("agent-runtime models remove claude claude-haiku-4-5")]
    [InlineData("agent-runtime config set claude defaultModel=claude-opus-4-7")]
    // #1066: read-only `config get <id>` sibling of `config set`.
    [InlineData("agent-runtime config get claude")]
    [InlineData("agent-runtime config get claude --output json")]
    [InlineData("agent-runtime credentials status claude")]
    [InlineData("agent-runtime refresh-models claude")]
    [InlineData("agent-runtime refresh-models claude --credential sk-ant-api-test")]
    [InlineData("agent-runtime refresh-models ollama")]
    // #1066: real `validate-credential` verb (was previously missing
    // even though `credentials status` pointed operators at it).
    [InlineData("agent-runtime validate-credential claude")]
    [InlineData("agent-runtime validate-credential claude --credential sk-ant-api-test")]
    [InlineData("agent-runtime validate-credential claude --credential sk-ant-api-test --secret-name custom")]
    [InlineData("agent-runtime validate-credential ollama")]
    public void EveryVerb_ParsesWithoutErrors(string argLine)
    {
        var outputOption = CreateOutputOption();
        var command = AgentRuntimeCommand.Create(outputOption);
        var root = new RootCommand { Options = { outputOption } };
        root.Subcommands.Add(command);

        var parseResult = root.Parse(argLine);

        parseResult.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void Install_WithoutId_FailsToParse()
    {
        var outputOption = CreateOutputOption();
        var command = AgentRuntimeCommand.Create(outputOption);
        var root = new RootCommand { Options = { outputOption } };
        root.Subcommands.Add(command);

        var parseResult = root.Parse("agent-runtime install");

        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void ModelsSet_WithoutModels_FailsToParse()
    {
        var outputOption = CreateOutputOption();
        var command = AgentRuntimeCommand.Create(outputOption);
        var root = new RootCommand { Options = { outputOption } };
        root.Subcommands.Add(command);

        var parseResult = root.Parse("agent-runtime models set claude");

        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void RefreshModels_WithoutId_FailsToParse()
    {
        var outputOption = CreateOutputOption();
        var command = AgentRuntimeCommand.Create(outputOption);
        var root = new RootCommand { Options = { outputOption } };
        root.Subcommands.Add(command);

        var parseResult = root.Parse("agent-runtime refresh-models");

        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void ValidateCredential_WithoutId_FailsToParse()
    {
        // #1066: parser-level guard so the new verb keeps requiring an
        // id argument — a bare `spring agent-runtime validate-credential`
        // would otherwise silently default to ... nothing useful.
        var outputOption = CreateOutputOption();
        var command = AgentRuntimeCommand.Create(outputOption);
        var root = new RootCommand { Options = { outputOption } };
        root.Subcommands.Add(command);

        var parseResult = root.Parse("agent-runtime validate-credential");

        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void ConfigGet_WithoutId_FailsToParse()
    {
        var outputOption = CreateOutputOption();
        var command = AgentRuntimeCommand.Create(outputOption);
        var root = new RootCommand { Options = { outputOption } };
        root.Subcommands.Add(command);

        var parseResult = root.Parse("agent-runtime config get");

        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void CredentialsStatus_MissingRowHint_PointsAt_Real_ValidateCredential_Verb()
    {
        // #1066 regression gate: the old hint pointed at a non-existent
        // `... validate-credential` subcommand. Lock the new wording so
        // any future drift (typo, dropped --credential mention, etc.)
        // surfaces as a test failure instead of operator confusion.
        var formatted = string.Format(
            System.Globalization.CultureInfo.InvariantCulture,
            AgentRuntimeCommand.CredentialsStatusMissingRowHintFormat,
            "claude");

        formatted.ShouldContain("No credential-health row recorded for runtime 'claude'");
        formatted.ShouldContain("spring agent-runtime validate-credential claude --credential <key>");
        formatted.ShouldContain("/settings/agent-runtimes");
        formatted.ShouldNotContain("...");
    }
}