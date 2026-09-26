using System;
using System.Linq;
using System.Xml.Linq;

namespace ReleaseTool;

/// <summary>Derives the manifest's minModSystemVersion: the plugin is compiled against the pinned
/// Stellar.Abstractions version, so that is the oldest framework it can run on.</summary>
internal static class FrameworkRequirement
{
    private const string SdkPackage = "Stellar.Abstractions";

    public static string ReadMinimum(string packagesPropsXml)
    {
        var version = XDocument.Parse(packagesPropsXml)
            .Descendants("PackageVersion")
            .Where(element => string.Equals((string?)element.Attribute("Include"), SdkPackage, StringComparison.OrdinalIgnoreCase))
            .Select(element => (string?)element.Attribute("Version"))
            .SingleOrDefault();

        if (version is null)
            throw new ReleaseException($"Directory.Packages.props doesn't pin {SdkPackage}.");
        return Formats.Require(Formats.Version, version, $"{SdkPackage} version");
    }
}
