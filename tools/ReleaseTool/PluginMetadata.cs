using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ReleaseTool;

/// <summary>The plugin-level fields from stellar-plugin.json: what the launcher shows for the
/// plugin as a whole, independent of any one version.</summary>
internal sealed record PluginMetadata(
    string Id,
    string Name,
    string Description,
    string Author,
    IReadOnlyList<string>? Tags,
    string? Homepage)
{
    public static PluginMetadata Parse(string json)
    {
        var metadata = JsonSerializer.Deserialize<PluginMetadata>(json, Json.Options)
            ?? throw new ReleaseException("stellar-plugin.json is empty.");
        metadata.Validate();
        return metadata;
    }

    private void Validate()
    {
        Formats.Require(Formats.PluginId, Id ?? string.Empty, "stellar-plugin.json id");
        RequireText(Name, "name");
        RequireText(Author, "author");
        RequireText(Description, "description");
        if (Description.StartsWith("TODO", StringComparison.OrdinalIgnoreCase))
            throw new ReleaseException("stellar-plugin.json description is still the TODO placeholder.");
        if (Homepage is not null && !IsHttpUrl(Homepage))
            throw new ReleaseException($"stellar-plugin.json homepage '{Homepage}' must be an http(s) URL.");
    }

    private static void RequireText(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ReleaseException($"stellar-plugin.json {field} is required.");
    }

    private static bool IsHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
