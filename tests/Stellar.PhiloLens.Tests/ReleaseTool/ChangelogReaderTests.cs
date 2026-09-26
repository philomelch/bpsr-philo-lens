using ReleaseTool;
using Xunit;

namespace Stellar.PhiloLens.Tests.ReleaseTool;

public sealed class ChangelogReaderTests
{
    private const string Markdown = """
        # Changelog

        ## [Unreleased]

        ## [1.1.0] - 2026-09-18

        ### Added

        - Party roster window.
        - Hotkey toggle that wraps
          onto a second line.

        ### Fixed

        - Crash on zone load.

        ## [1.0.0] - 2026-09-01

        ### Added

        - First release.
        """;

    [Fact]
    public void Reads_only_the_requested_section()
    {
        var section = ChangelogReader.ReadSection(Markdown, "1.1.0");

        Assert.Equal("2026-09-18", section.Date);
        Assert.Equal(new[] { "Party roster window.", "Hotkey toggle that wraps onto a second line." }, section.Changelog.Added);
        Assert.Equal(new[] { "Crash on zone load." }, section.Changelog.Fixed);
        Assert.Empty(section.Changelog.Changed);
        Assert.DoesNotContain("First release", section.Body, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_section_is_an_error()
    {
        var problem = Assert.Throws<ReleaseException>(() => ChangelogReader.ReadSection(Markdown, "2.0.0"));
        Assert.Contains("[2.0.0]", problem.Message, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Empty_section_is_an_error()
    {
        const string empty = "## [1.0.0] - 2026-09-01\n\n### Added\n";
        Assert.Throws<ReleaseException>(() => ChangelogReader.ReadSection(empty, "1.0.0"));
    }

    [Fact]
    public void Unknown_heading_is_an_error()
    {
        const string security = "## [1.0.0] - 2026-09-01\n\n### Security\n\n- Patched.\n";
        Assert.Throws<ReleaseException>(() => ChangelogReader.ReadSection(security, "1.0.0"));
    }
}
