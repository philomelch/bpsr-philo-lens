using System.Collections.Generic;

namespace ReleaseTool;

/// <summary>The release manifest (schema 1) attached to every GitHub Release as manifest.json.
/// The shared registry reads it to build plugins.json. Field names match
/// StellarResonance/docs/manifest-standard.md, so the registry copies them through unchanged.</summary>
internal sealed record ReleaseManifest(
    int Schema,
    string Id,
    string Name,
    string Description,
    string Author,
    IReadOnlyList<string>? Tags,
    string? Homepage,
    PluginVersion Release)
{
    public const int CurrentSchema = 1;
}

/// <summary>One published build, in the registry's per-version shape.</summary>
internal sealed record PluginVersion(
    string Version,
    string Date,
    string Dll,
    string DllUrl,
    string Sha256,
    string MinModSystemVersion,
    string? MaxModSystemVersion,
    string SourceRepository,
    string SourceCommit,
    string SourceTag,
    Changelog Changelog);
