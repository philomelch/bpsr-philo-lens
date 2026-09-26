using System.Collections.Generic;

namespace ReleaseTool;

/// <summary>One version's changes, in the registry manifest's four buckets.</summary>
internal sealed record Changelog(
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Changed,
    IReadOnlyList<string> Fixed,
    IReadOnlyList<string> Removed);

/// <summary>A CHANGELOG.md section for one version: its date (if given), its entries, and the
/// raw markdown body (used as the GitHub Release notes).</summary>
internal sealed record ChangelogSection(string? Date, Changelog Changelog, string Body);
