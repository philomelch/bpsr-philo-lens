using System.Collections.Generic;

namespace Stellar.PhiloLens.Domain;

/// <summary>Shortens Battle Imagine item names ("Battle Imagine - Tina") to what players call them
/// ("Tina"). The label is found as the prefix every Imagine item name shares, so it works in any game
/// language without hard-coding the English text.</summary>
internal static class ImagineNames
{
    /// <summary>The label all <paramref name="names"/> start with, ending at a separator (space, dash,
    /// "・", …), or empty when they share none. Needs at least two names to tell a label from a name.</summary>
    public static string CommonLabel(IReadOnlyList<string> names)
    {
        if (names.Count < 2) return string.Empty;

        var length = names[0].Length;
        for (var i = 1; i < names.Count; i++) length = SharedLength(names[0], names[i], length);

        // Cut back to the last separator, so "Battle Imagine - T" (Tina, Tatta) never eats part of a name.
        while (length > 0 && char.IsLetterOrDigit(names[0][length - 1])) length--;
        return names[0].Substring(0, length);
    }

    /// <summary><paramref name="name"/> without <paramref name="label"/>; the full name when it doesn't
    /// start with it or nothing would be left.</summary>
    public static string WithoutLabel(string name, string label) =>
        label.Length > 0 && name.Length > label.Length && name.StartsWith(label, System.StringComparison.Ordinal)
            ? name.Substring(label.Length).Trim()
            : name;

    private static int SharedLength(string first, string second, int max)
    {
        var length = 0;
        while (length < max && length < second.Length && first[length] == second[length]) length++;
        return length;
    }
}
