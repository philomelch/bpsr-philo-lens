using System;
using System.Collections.Generic;

namespace ReleaseTool;

/// <summary>Command-line options: <c>--root . --repository owner/repo --commit &lt;sha&gt;
/// --version 1.2.0 --dll path/to/Plugin.dll --output release/</c>.</summary>
internal sealed record ReleaseOptions(
    string Root,
    string Repository,
    string Commit,
    string Version,
    string DllPath,
    string OutputDirectory)
{
    public static ReleaseOptions Parse(IReadOnlyList<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Count; i += 2)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Count)
                throw new ReleaseException($"Expected '--name value' pairs, got '{args[i]}'.");
            values[args[i][2..]] = args[i + 1];
        }

        return new ReleaseOptions(
            Root: Required(values, "root"),
            Repository: Formats.Require(Formats.Repository, Required(values, "repository"), "--repository"),
            Commit: Formats.Require(Formats.CommitSha, Required(values, "commit"), "--commit"),
            Version: Formats.Require(Formats.Version, Required(values, "version"), "--version"),
            DllPath: Required(values, "dll"),
            OutputDirectory: Required(values, "output"));
    }

    private static string Required(Dictionary<string, string> values, string name) =>
        values.TryGetValue(name, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new ReleaseException($"Missing required option --{name}.");
}
