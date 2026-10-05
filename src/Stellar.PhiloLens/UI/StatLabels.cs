using System;
using Stellar.Abstractions.Services;

namespace Stellar.PhiloLens.UI;

/// <summary>Stat labels as the game names them, so season-specific stats follow the season and the game language.</summary>
internal static class StatLabels
{
    // EAttrType ids; their localised names come from the game's attribute table.
    public const int AbilityScoreAttributeId = 10030;

    // AttrSeasonStrength; the game names it per season (e.g. "Illusion-Breaking Strength").
    public const int SeasonStrengthAttributeId = 11440;

    private static readonly char[] WordSeparators = { ' ', '-', '‐', '‑' };

    /// <summary>The game's name for <paramref name="attributeId"/>, or the plugin's own label under
    /// <paramref name="fallbackKey"/> when the game data has none.</summary>
    public static string Of(ILocalization localization, IGameDataCombat combatData, int attributeId, string fallbackKey)
    {
        var label = combatData.GetAttribute(attributeId)?.Name;
        return string.IsNullOrEmpty(label) ? localization.T(fallbackKey) : label;
    }

    /// <summary>Like <see cref="Of"/>, but the game's name is shortened to its initials ("Illusion-Breaking
    /// Strength" → "IBS") for a narrow column header. The plugin's own fallback label is kept whole.</summary>
    public static string ShortOf(ILocalization localization, IGameDataCombat combatData, int attributeId, string fallbackKey)
    {
        var label = combatData.GetAttribute(attributeId)?.Name;
        return string.IsNullOrEmpty(label) ? localization.T(fallbackKey) : Initials(label);
    }

    /// <summary>The first letter of each word, upper-cased; words are split on spaces and hyphens. Only Latin-script
    /// names are shortened: a name of one word (e.g. in a language written without spaces), or one whose words don't
    /// all start with a Latin letter (initials of Thai, say, can be a lone vowel sign), is returned whole.</summary>
    public static string Initials(string name)
    {
        var words = name.Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2) return name;

        var initials = new char[words.Length];
        for (var i = 0; i < words.Length; i++)
        {
            if (!IsLatinLetter(words[i][0])) return name;
            initials[i] = char.ToUpperInvariant(words[i][0]);
        }

        return new string(initials);
    }

    // Basic Latin through Latin Extended-B (A–Z, é, ł, …). Also rules out a surrogate half, which a word starting
    // with a character outside the Basic Multilingual Plane would otherwise be cut to.
    private const char LastLatinExtendedB = 'ɏ';

    private static bool IsLatinLetter(char c) => c <= LastLatinExtendedB && char.IsLetter(c);
}
