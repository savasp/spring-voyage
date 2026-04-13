// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Cli.Tests;

using System.Text.Json;

using Cvoya.Spring.Cli.Output;

using Shouldly;

using Xunit;

public class OutputFormatterTests
{
    [Fact]
    public void FormatTable_ArrayData_ProducesAlignedColumns()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
            [
                { "id": "a1", "name": "Alice" },
                { "id": "b2", "name": "Bob" }
            ]
            """);

        var result = OutputFormatter.FormatTable(json, ["id", "name"]);

        result.ShouldContain("ID");
        result.ShouldContain("NAME");
        result.ShouldContain("a1");
        result.ShouldContain("Alice");
        result.ShouldContain("b2");
        result.ShouldContain("Bob");

        // Verify alignment: header and separator lines should have consistent structure
        var lines = result.Split(Environment.NewLine);
        lines.Length.ShouldBeGreaterThanOrEqualTo(3); // header + separator + at least 1 row
        lines[1].ShouldMatch(@"^-+\s+-+$"); // separator line
    }

    [Fact]
    public void FormatTable_SingleObject_TreatedAsOneRowTable()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
            { "id": "x1", "name": "Xander" }
            """);

        var result = OutputFormatter.FormatTable(json, ["id", "name"]);

        result.ShouldContain("x1");
        result.ShouldContain("Xander");
    }

    [Fact]
    public void FormatTable_EmptyArray_ReturnsNoResultsMessage()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("[]");

        var result = OutputFormatter.FormatTable(json, ["id", "name"]);

        result.ShouldBe("No results found.");
    }

    [Fact]
    public void FormatJson_ProducesValidIndentedJson()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
            { "id": "test", "value": 42 }
            """);

        var result = OutputFormatter.FormatJson(json);

        // Should be valid JSON
        var reparsed = JsonSerializer.Deserialize<JsonElement>(result);
        reparsed.GetProperty("id").GetString().ShouldBe("test");
        reparsed.GetProperty("value").GetInt32().ShouldBe(42);

        // Should be indented (contain newlines)
        result.ShouldContain(Environment.NewLine);
    }

    [Fact]
    public void FormatTable_MissingProperty_ShowsEmptyString()
    {
        var json = JsonSerializer.Deserialize<JsonElement>("""
            [{ "id": "a1" }]
            """);

        var result = OutputFormatter.FormatTable(json, ["id", "name"]);

        result.ShouldContain("a1");
        // "name" column should exist in header but value should be empty
        result.ShouldContain("NAME");
    }
}