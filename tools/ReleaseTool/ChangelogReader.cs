using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace ReleaseTool;

/// <summary>Reads one version's section from a Keep-a-Changelog style CHANGELOG.md:
/// <c>## [1.2.0] - 2026-09-18</c>, then <c>### Added</c> / <c>Changed</c> / <c>Fixed</c> /
/// <c>Removed</c> headings with <c>- item</c> bullets. Indented lines continue the previous bullet.</summary>
internal static class ChangelogReader
{
    private static readonly Regex VersionHeading =
        new(@"^## \[(?<version>[^\]]+)\](\s*-\s*(?<date>\d{4}-\d{2}-\d{2}))?\s*$", RegexOptions.CultureInvariant);
    private static readonly Regex Bullet = new(@"^[-*]\s+(?<text>.+)$", RegexOptions.CultureInvariant);
    private static readonly string[] Buckets = { "Added", "Changed", "Fixed", "Removed" };

    public static ChangelogSection ReadSection(string markdown, string version)
    {
        var lines = markdown.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => HeadingVersion(line) == version);
        if (start < 0)
            throw new ReleaseException($"CHANGELOG.md has no '## [{version}] - YYYY-MM-DD' section.");

        var end = Array.FindIndex(lines, start + 1, line => line.StartsWith("## ", StringComparison.Ordinal));
        var body = lines[(start + 1)..(end < 0 ? lines.Length : end)];
        var date = VersionHeading.Match(lines[start]).Groups["date"];

        var entries = ReadEntries(body, version);
        if (entries.Values.All(items => items.Count == 0))
            throw new ReleaseException($"CHANGELOG.md section [{version}] has no entries.");

        return new ChangelogSection(
            date.Success ? date.Value : null,
            new Changelog(entries["Added"], entries["Changed"], entries["Fixed"], entries["Removed"]),
            string.Join('\n', body).Trim());
    }

    private static string? HeadingVersion(string line)
    {
        var match = VersionHeading.Match(line);
        return match.Success ? match.Groups["version"].Value : null;
    }

    private static Dictionary<string, List<string>> ReadEntries(IEnumerable<string> body, string version)
    {
        var entries = Buckets.ToDictionary(bucket => bucket, _ => new List<string>(), StringComparer.Ordinal);
        List<string>? current = null;

        foreach (var line in body)
        {
            if (line.StartsWith("### ", StringComparison.Ordinal))
            {
                var heading = line[4..].Trim();
                if (!entries.TryGetValue(heading, out current))
                    throw new ReleaseException(
                        $"CHANGELOG.md section [{version}] has '### {heading}'; use only {string.Join(", ", Buckets)}.");
            }
            else if (Bullet.Match(line) is { Success: true } bullet)
            {
                if (current is null)
                    throw new ReleaseException($"CHANGELOG.md section [{version}] has a bullet outside a ### heading.");
                current.Add(bullet.Groups["text"].Value.Trim());
            }
            else if (current is { Count: > 0 } && line.StartsWith("  ", StringComparison.Ordinal))
            {
                current[^1] = $"{current[^1]} {line.Trim()}";
            }
        }
        return entries;
    }
}
