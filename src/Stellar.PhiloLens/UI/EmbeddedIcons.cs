using System.IO;

namespace Stellar.PhiloLens.UI;

/// <summary>Loads the plugin's icons, embedded in the DLL as <c>Icons.&lt;file&gt;</c> resources.</summary>
internal static class EmbeddedIcons
{
    /// <summary>The profile-card button icon: white on transparent, since the framework tints it.</summary>
    public const string Lens = "lens.png";

    /// <summary>The PNG bytes of <paramref name="fileName"/>, or null when the resource is missing
    /// (the card then shows a label-only button). Load once and keep the array: the framework caches
    /// the decoded texture by array reference.</summary>
    public static byte[]? Load(string fileName)
    {
        using var stream = typeof(EmbeddedIcons).Assembly.GetManifestResourceStream($"Icons.{fileName}");
        if (stream is null) return null;

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
