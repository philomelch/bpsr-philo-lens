using System.Text.RegularExpressions;

namespace ReleaseTool;

/// <summary>Input formats the registry and launcher depend on. The launcher compares versions
/// with System.Version, so only plain numeric MAJOR.MINOR.PATCH is accepted.</summary>
internal static class Formats
{
    public static readonly Regex Version = new(@"^\d+\.\d+\.\d+$", RegexOptions.CultureInvariant);
    public static readonly Regex PluginId = new(@"^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.CultureInvariant);
    public static readonly Regex CommitSha = new(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant);
    public static readonly Regex Repository = new(@"^[A-Za-z0-9-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant);

    public static string Require(Regex format, string value, string what)
    {
        if (!format.IsMatch(value))
            throw new ReleaseException($"{what} '{value}' is not in the expected format ({format}).");
        return value;
    }
}
