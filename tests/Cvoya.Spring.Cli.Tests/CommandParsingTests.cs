// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Tests;

using System.CommandLine;

using Cvoya.Spring.Cli.Commands;

using Shouldly;

using Xunit;

public class CommandParsingTests
{
    private static Option<string> CreateOutputOption()
    {
        return new Option<string>("--output", "-o")
        {
            Description = "Output format",
            DefaultValueFactory = _ => "table"
        };
    }

    [Fact]
    public void AgentCreate_ParsesIdAndNameOptions()
    {
        var outputOption = CreateOutputOption();
        var agentCommand = AgentCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(agentCommand);

        var parseResult = rootCommand.Parse("agent create my-agent --name \"My Agent\"");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("id").ShouldBe("my-agent");
        parseResult.GetValue<string>("--name").ShouldBe("My Agent");
    }

    [Fact]
    public void MessageSend_ParsesAddressAndTextArguments()
    {
        var outputOption = CreateOutputOption();
        var messageCommand = MessageCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(messageCommand);

        var parseResult = rootCommand.Parse("message send agent://ada \"Review PR #42\"");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("address").ShouldBe("agent://ada");
        parseResult.GetValue<string>("text").ShouldBe("Review PR #42");
    }

    [Fact]
    public void UnitCreate_ParsesNameAndMetadataOptions()
    {
        // After #117 the CLI mirrors the server CreateUnitRequest contract:
        // a positional `name` (unit address + identifier) plus optional
        // --display-name and --description metadata.
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit create eng-team --display-name \"Engineering Team\" --description \"Builds the product\"");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("name").ShouldBe("eng-team");
        parseResult.GetValue<string>("--display-name").ShouldBe("Engineering Team");
        parseResult.GetValue<string>("--description").ShouldBe("Builds the product");
    }

    [Fact]
    public void ApplyCommand_ParsesFileOption()
    {
        var applyCommand = ApplyCommand.Create();
        var rootCommand = new RootCommand();
        rootCommand.Subcommands.Add(applyCommand);

        var parseResult = rootCommand.Parse("apply -f manifest.yaml");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("-f").ShouldBe("manifest.yaml");
    }

    [Fact]
    public void OutputOption_AcceptsJson()
    {
        var outputOption = CreateOutputOption();
        var agentCommand = AgentCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(agentCommand);

        var parseResult = rootCommand.Parse("--output json agent list");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue(outputOption).ShouldBe("json");
    }

    [Fact]
    public void OutputOption_DefaultsToTable()
    {
        var outputOption = CreateOutputOption();
        var agentCommand = AgentCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(agentCommand);

        var parseResult = rootCommand.Parse("agent list");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue(outputOption).ShouldBe("table");
    }

    // --- #320: unit membership management commands ---

    [Fact]
    public void UnitMembersList_ParsesUnitArgument()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse("--output json unit members list eng-team");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("unit").ShouldBe("eng-team");
        parseResult.GetValue(outputOption).ShouldBe("json");
    }

    [Fact]
    public void UnitMembersAdd_ParsesAllOverrideOptions()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit members add eng-team --agent ada --model claude-opus-4 --specialty coding --enabled true --execution-mode OnDemand");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("unit").ShouldBe("eng-team");
        parseResult.GetValue<string>("--agent").ShouldBe("ada");
        parseResult.GetValue<string>("--model").ShouldBe("claude-opus-4");
        parseResult.GetValue<string>("--specialty").ShouldBe("coding");
        parseResult.GetValue<bool?>("--enabled").ShouldBe(true);
        parseResult.GetValue<string>("--execution-mode").ShouldBe("OnDemand");
    }

    [Fact]
    public void UnitMembersAdd_ParsesWithoutAgentOrUnit_LeavesErrorsEmpty()
    {
        // #331 relaxed the parser-level `Required = true` on --agent because
        // `members add` now accepts `--unit` as an alternative. The
        // mutual-exclusion rule ("exactly one of --agent / --unit") is
        // enforced at action time with a readable error message; the parse
        // step itself stays successful so the action can take over and
        // surface the right diagnostic to the user.
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse("unit members add eng-team");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("--agent").ShouldBeNull();
        parseResult.GetValue<string>("--unit").ShouldBeNull();
    }

    [Fact]
    public void UnitMembersAdd_RejectsInvalidExecutionMode()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit members add eng-team --agent ada --execution-mode Invalid");

        parseResult.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public void UnitMembersConfig_ParsesLikeAdd()
    {
        // `config` is a semantic alias over the same PUT upsert; both share the
        // same flag set so callers can use whichever verb reads better at the
        // shell prompt.
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit members config eng-team --agent ada --model gpt-4o --enabled false");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("unit").ShouldBe("eng-team");
        parseResult.GetValue<string>("--agent").ShouldBe("ada");
        parseResult.GetValue<string>("--model").ShouldBe("gpt-4o");
        parseResult.GetValue<bool?>("--enabled").ShouldBe(false);
    }

    [Fact]
    public void UnitMembersRemove_ParsesUnitAndAgent()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse("unit members remove eng-team --agent ada");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("unit").ShouldBe("eng-team");
        parseResult.GetValue<string>("--agent").ShouldBe("ada");
    }

    [Fact]
    public void UnitPurge_ParsesIdAndConfirm()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse("unit purge eng-team --confirm");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("id").ShouldBe("eng-team");
        parseResult.GetValue<bool>("--confirm").ShouldBeTrue();
    }

    [Fact]
    public void UnitPurge_ConfirmDefaultsFalse()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse("unit purge eng-team");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("id").ShouldBe("eng-team");
        parseResult.GetValue<bool>("--confirm").ShouldBeFalse();
    }

    [Fact]
    public void AgentPurge_ParsesIdAndConfirm()
    {
        var outputOption = CreateOutputOption();
        var agentCommand = AgentCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(agentCommand);

        var parseResult = rootCommand.Parse("agent purge ada --confirm");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("id").ShouldBe("ada");
        parseResult.GetValue<bool>("--confirm").ShouldBeTrue();
    }

    // --- #315: --model and --color on `spring unit create` ---------------

    [Fact]
    public void UnitCreate_ParsesModelAndColorOptions()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit create eng-team --model claude-sonnet-4-20250514 --color #6366f1");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("name").ShouldBe("eng-team");
        parseResult.GetValue<string>("--model").ShouldBe("claude-sonnet-4-20250514");
        parseResult.GetValue<string>("--color").ShouldBe("#6366f1");
    }

    // --- #316: `spring unit create --from-template <pkg>/<name>` -----------

    [Fact]
    public void UnitCreate_ParsesFromTemplateWithName()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit create --from-template software-engineering/engineering-team --name run42-eng --display-name \"Engineering (run 42)\" --model claude-sonnet-4 --color #336699");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("--from-template")
            .ShouldBe("software-engineering/engineering-team");
        parseResult.GetValue<string>("--name").ShouldBe("run42-eng");
        parseResult.GetValue<string>("--display-name").ShouldBe("Engineering (run 42)");
        parseResult.GetValue<string>("--model").ShouldBe("claude-sonnet-4");
        parseResult.GetValue<string>("--color").ShouldBe("#336699");
    }

    [Fact]
    public void UnitCreate_PositionalNameAloneStillParses_ForDirectCreatePath()
    {
        // Positional 'name' stayed mandatory pre-#316 so every old invocation
        // must keep parsing without errors on the new Argument.Arity setting.
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse("unit create my-unit");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("name").ShouldBe("my-unit");
        parseResult.GetValue<string>("--from-template").ShouldBeNull();
    }

    [Fact]
    public void UnitCreate_FromTemplateWithoutPositionalName_LeavesArgumentEmpty()
    {
        // When --from-template is present the positional 'name' becomes
        // optional — the parser should leave it null rather than erroring.
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit create --from-template software-engineering/engineering-team --name run42-eng");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("name").ShouldBeNull();
        parseResult.GetValue<string>("--name").ShouldBe("run42-eng");
    }

    // --- #331: `spring unit members add --unit <child>` -------------------

    [Fact]
    public void UnitMembersAdd_ParsesUnitOption()
    {
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit members add parent-unit --unit child-unit");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("unit").ShouldBe("parent-unit");
        parseResult.GetValue<string>("--unit").ShouldBe("child-unit");
        parseResult.GetValue<string>("--agent").ShouldBeNull();
    }

    [Fact]
    public void UnitMembersAdd_ParsesAgentAndUnitTogether_ForActionToReject()
    {
        // Parser is intentionally permissive; the action layer enforces the
        // "exactly one of --agent / --unit" rule with a readable error
        // message. See UnitCommand.CreateMembersAddCommand for the runtime
        // check introduced by #331.
        var outputOption = CreateOutputOption();
        var unitCommand = UnitCommand.Create(outputOption);
        var rootCommand = new RootCommand { Options = { outputOption } };
        rootCommand.Subcommands.Add(unitCommand);

        var parseResult = rootCommand.Parse(
            "unit members add parent-unit --agent ada --unit child-unit");

        parseResult.Errors.ShouldBeEmpty();
        parseResult.GetValue<string>("--agent").ShouldBe("ada");
        parseResult.GetValue<string>("--unit").ShouldBe("child-unit");
    }
}