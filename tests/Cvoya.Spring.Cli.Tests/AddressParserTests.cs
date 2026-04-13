// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Tests;

using Shouldly;

using Xunit;

public class AddressParserTests
{
    [Fact]
    public void Parse_ValidAddress_ReturnsSchemeAndPath()
    {
        var (scheme, path) = AddressParser.Parse("agent://ada");

        scheme.ShouldBe("agent");
        path.ShouldBe("ada");
    }

    [Fact]
    public void Parse_UnitAddress_ReturnsSchemeAndPath()
    {
        var (scheme, path) = AddressParser.Parse("unit://engineering");

        scheme.ShouldBe("unit");
        path.ShouldBe("engineering");
    }

    [Fact]
    public void Parse_InvalidFormat_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => AddressParser.Parse("invalid-address"));
    }

    [Fact]
    public void Parse_EmptyScheme_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => AddressParser.Parse("://path"));
    }

    [Fact]
    public void Parse_EmptyPath_ThrowsFormatException()
    {
        Should.Throw<FormatException>(() => AddressParser.Parse("agent://"));
    }
}