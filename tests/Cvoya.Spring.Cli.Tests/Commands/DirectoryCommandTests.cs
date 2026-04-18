// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Tests.Commands;

using System.CommandLine;

using Cvoya.Spring.Cli.Commands;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for the pure helpers + command-tree shape of
/// <see cref="DirectoryCommand"/>. The wire-layer (search endpoint
/// round-trip) is exercised from the Host.Api integration tests — here
/// we pin the parsing + subcommand wiring so regressions surface without
/// spinning up an HTTP harness (#528).
/// </summary>
public class DirectoryCommandTests
{
    [Fact]
    public void ParseAddress_Valid_ReturnsSchemeAndPath()
    {
        var parsed = DirectoryCommand.ParseAddress("agent://ada");
        parsed.ShouldNotBeNull();
        parsed!.Value.Scheme.ShouldBe("agent");
        parsed.Value.Path.ShouldBe("ada");
    }

    [Fact]
    public void ParseAddress_UnitScheme_ReturnsPath()
    {
        var parsed = DirectoryCommand.ParseAddress("unit://platform/foundation");
        parsed.ShouldNotBeNull();
        parsed!.Value.Scheme.ShouldBe("unit");
        parsed.Value.Path.ShouldBe("platform/foundation");
    }

    [Theory]
    [InlineData("")]
    [InlineData("no-separator")]
    [InlineData("://empty-scheme")]
    [InlineData("scheme://")]
    public void ParseAddress_Invalid_ReturnsNull(string address)
    {
        DirectoryCommand.ParseAddress(address).ShouldBeNull();
    }

    [Fact]
    public void Create_WiresAllThreeBrowseVerbs()
    {
        // `search` was added in #542; `list` + `show` arrive with #528.
        // Lock the set so a future refactor that drops a verb breaks this
        // test rather than silently regressing CLI/portal parity.
        var outputOption = new Option<string>("--output", "-o")
        {
            DefaultValueFactory = _ => "table",
        };

        var cmd = DirectoryCommand.Create(outputOption);

        cmd.Name.ShouldBe("directory");
        var verbs = cmd.Subcommands.Select(s => s.Name).ToArray();
        verbs.ShouldContain("search");
        verbs.ShouldContain("list");
        verbs.ShouldContain("show");
    }

    [Fact]
    public void ListCommand_DeclaresExpectedFilters()
    {
        var outputOption = new Option<string>("--output", "-o")
        {
            DefaultValueFactory = _ => "table",
        };

        var list = DirectoryCommand.Create(outputOption)
            .Subcommands
            .First(s => s.Name == "list");

        var optionNames = list.Options.Select(o => o.Name).ToArray();
        optionNames.ShouldContain("--domain");
        optionNames.ShouldContain("--owner");
        optionNames.ShouldContain("--limit");
        optionNames.ShouldContain("--offset");
        optionNames.ShouldContain("--typed-only");
        optionNames.ShouldContain("--inside");
    }

    [Fact]
    public void ShowCommand_RequiresSlugArgument()
    {
        var outputOption = new Option<string>("--output", "-o")
        {
            DefaultValueFactory = _ => "table",
        };

        var show = DirectoryCommand.Create(outputOption)
            .Subcommands
            .First(s => s.Name == "show");

        show.Arguments.Count.ShouldBe(1);
        show.Arguments[0].Name.ShouldBe("slug");
    }
}