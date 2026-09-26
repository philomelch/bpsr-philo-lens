using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Stellar.PhiloLens.Adapters;
using Xunit;

namespace Stellar.PhiloLens.Tests;

/// <summary>Every language catalog must carry exactly the keys of en.json (the source of truth),
/// so no language silently falls back to English. Replaces the devkit's i18n-catalog checker.</summary>
public sealed class LocalizationCatalogTests
{
    private static readonly string[] Languages = { "ja", "th", "id", "fil" };

    [Fact]
    public void Every_catalog_has_the_same_keys_as_english()
    {
        var english = ReadKeys("en");

        foreach (var language in Languages)
        {
            var keys = ReadKeys(language);
            Assert.True(english.SetEquals(keys),
                $"{language}.json keys differ from en.json. Missing: [{string.Join(", ", english.Except(keys))}] " +
                $"Extra: [{string.Join(", ", keys.Except(english))}]");
        }
    }

    private static HashSet<string> ReadKeys(string language)
    {
        var assembly = typeof(ClassEvidenceSource).Assembly;
        using var stream = assembly.GetManifestResourceStream($"Lang.{language}.json")
            ?? throw new FileNotFoundException($"Embedded catalog Lang.{language}.json is missing.");
        using var document = JsonDocument.Parse(stream);
        return document.RootElement.EnumerateObject().Select(property => property.Name).ToHashSet();
    }
}
