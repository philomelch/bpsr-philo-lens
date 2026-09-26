using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;

namespace ReleaseTool;

/// <summary>Assembles the release outputs from the repo's files: manifest.json (for the registry),
/// notes.md (the GitHub Release body) and &lt;dll&gt;.sha256 (for manual installs).</summary>
internal static class ReleaseBuilder
{
    public static void Run(ReleaseOptions options)
    {
        var metadata = PluginMetadata.Parse(File.ReadAllText(Path.Combine(options.Root, "stellar-plugin.json")));
        var section = ChangelogReader.ReadSection(File.ReadAllText(Path.Combine(options.Root, "CHANGELOG.md")), options.Version);
        var minFramework = FrameworkRequirement.ReadMinimum(File.ReadAllText(Path.Combine(options.Root, "Directory.Packages.props")));
        var dllName = Path.GetFileName(options.DllPath);
        var sha256 = Sha256Of(options.DllPath);

        var manifest = new ReleaseManifest(
            ReleaseManifest.CurrentSchema, metadata.Id, metadata.Name, metadata.Description, metadata.Author,
            metadata.Tags, metadata.Homepage,
            new PluginVersion(
                Version: options.Version,
                Date: section.Date ?? DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Dll: dllName,
                DllUrl: $"https://github.com/{options.Repository}/releases/download/v{options.Version}/{dllName}",
                Sha256: sha256,
                MinModSystemVersion: minFramework,
                MaxModSystemVersion: null,
                SourceRepository: $"https://github.com/{options.Repository}.git",
                SourceCommit: options.Commit,
                SourceTag: $"v{options.Version}",
                Changelog: section.Changelog));

        Directory.CreateDirectory(options.OutputDirectory);
        File.WriteAllText(Path.Combine(options.OutputDirectory, "manifest.json"), JsonSerializer.Serialize(manifest, Json.Options));
        File.WriteAllText(Path.Combine(options.OutputDirectory, "notes.md"), section.Body + "\n");
        File.WriteAllText(Path.Combine(options.OutputDirectory, dllName + ".sha256"), $"{sha256}  {dllName}\n");
    }

    private static string Sha256Of(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}
