// Copyright CVOYA LLC. Licensed under the Business Source License 1.1.
// See LICENSE.md in the project root for full license terms.

namespace Cvoya.Spring.Dapr.Tests.Execution;

using System.Text.Json;

using Cvoya.Spring.Core.Execution;
using Cvoya.Spring.Dapr.Execution;

using Shouldly;

using Xunit;

/// <summary>
/// Unit tests for the static extraction helper on
/// <see cref="DbUnitExecutionStore.Extract(JsonElement?)"/>. The DB
/// integration path is exercised indirectly via the integration tests.
/// </summary>
public class DbUnitExecutionStoreTests
{
    [Fact]
    public void Extract_ReturnsNull_WhenDefinitionIsMissing()
    {
        DbUnitExecutionStore.Extract(null).ShouldBeNull();
    }

    [Fact]
    public void Extract_ReturnsNull_WhenNoExecutionBlock()
    {
        using var doc = JsonDocument.Parse("""{"instructions":"hi"}""");
        DbUnitExecutionStore.Extract(doc.RootElement).ShouldBeNull();
    }

    [Fact]
    public void Extract_ReturnsAllFields()
    {
        using var doc = JsonDocument.Parse("""
            {
              "execution": {
                "image": "ghcr.io/foo:latest",
                "runtime": "podman",
                "tool": "dapr-agent",
                "provider": "ollama",
                "model": "llama3.2:3b"
              }
            }
            """);
        var defaults = DbUnitExecutionStore.Extract(doc.RootElement);
        defaults.ShouldNotBeNull();
        defaults!.Image.ShouldBe("ghcr.io/foo:latest");
        defaults.Runtime.ShouldBe("podman");
        defaults.Tool.ShouldBe("dapr-agent");
        defaults.Provider.ShouldBe("ollama");
        defaults.Model.ShouldBe("llama3.2:3b");
    }

    [Fact]
    public void Extract_ReturnsNull_WhenBlockIsEmptyObject()
    {
        using var doc = JsonDocument.Parse("""{"execution":{}}""");
        DbUnitExecutionStore.Extract(doc.RootElement).ShouldBeNull();
    }

    [Fact]
    public void Extract_TrimsWhitespace()
    {
        using var doc = JsonDocument.Parse("""{"execution":{"image":"  ghcr.io/x  "}}""");
        var defaults = DbUnitExecutionStore.Extract(doc.RootElement);
        defaults.ShouldNotBeNull();
        defaults!.Image.ShouldBe("ghcr.io/x");
    }

    [Fact]
    public void UnitExecutionDefaults_IsEmpty_WhenAllFieldsNullOrBlank()
    {
        new UnitExecutionDefaults().IsEmpty.ShouldBeTrue();
        new UnitExecutionDefaults(Image: "  ", Runtime: null).IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void UnitExecutionDefaults_IsEmpty_FalseWhenOneFieldSet()
    {
        new UnitExecutionDefaults(Image: "x").IsEmpty.ShouldBeFalse();
        new UnitExecutionDefaults(Model: "x").IsEmpty.ShouldBeFalse();
    }
}