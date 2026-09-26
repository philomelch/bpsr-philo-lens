using ReleaseTool;
using Xunit;

namespace Stellar.PhiloLens.Tests.ReleaseTool;

public sealed class ReleaseInputsTests
{
    [Fact]
    public void Metadata_parses_a_complete_file()
    {
        var metadata = PluginMetadata.Parse("""
            { "id": "party-overlay", "name": "Party Overlay", "description": "Shows your party.",
              "author": "me", "tags": ["party"], "homepage": null }
            """);

        Assert.Equal("party-overlay", metadata.Id);
    }

    [Theory]
    [InlineData("""{ "id": "Party Overlay", "name": "n", "description": "d", "author": "a" }""")]
    [InlineData("""{ "id": "party", "name": "n", "description": "TODO: describe", "author": "a" }""")]
    [InlineData("""{ "id": "party", "name": "", "description": "d", "author": "a" }""")]
    [InlineData("""{ "id": "party", "name": "n", "description": "d", "author": "a", "homepage": "javascript:x" }""")]
    [InlineData("""{ "id": "party", "name": "n", "description": "d", "author": "a", "typo": 1 }""")]
    public void Metadata_rejects_invalid_files(string json)
    {
        Assert.ThrowsAny<System.Exception>(() => PluginMetadata.Parse(json));
    }

    [Fact]
    public void Framework_minimum_is_the_pinned_sdk_version()
    {
        const string props = """
            <Project><ItemGroup>
              <PackageVersion Include="Stellar.Abstractions" Version="2.8.1" />
              <PackageVersion Include="xunit" Version="2.9.3" />
            </ItemGroup></Project>
            """;

        Assert.Equal("2.8.1", FrameworkRequirement.ReadMinimum(props));
    }

    [Fact]
    public void Options_reject_a_malformed_commit()
    {
        var args = new[] { "--root", ".", "--repository", "me/repo", "--commit", "abc", "--version", "1.0.0",
            "--dll", "x.dll", "--output", "out" };

        Assert.Throws<ReleaseException>(() => ReleaseOptions.Parse(args));
    }
}
