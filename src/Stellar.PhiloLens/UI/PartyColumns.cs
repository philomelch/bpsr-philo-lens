namespace Stellar.PhiloLens.UI;

/// <summary>The party table's number columns: their fixed widths, and which headers fit over the numbers.
/// Shared by <see cref="PartyText"/>, which decides the fit once per rebuild, and <see cref="PartyPanel"/>,
/// which lays the columns out.</summary>
internal static class PartyColumns
{
    // Number cells get a fixed width so the numbers line up on the right; a cell that fits its text can't
    // right-align (it is exactly as wide as the text). Wide enough for a six-digit score and a four-digit stat.
    public const float AbilityScoreWidth = 64f;
    public const float SeasonStrengthWidth = 48f;

    // The longest header that still fits in the numbers' width (bold, about 9 units a character; a little less
    // for wide CJK characters, which then overhang into the gap before the column).
    private const int AbilityScoreHeaderMaxChars = 6;
    private const int SeasonStrengthHeaderMaxChars = 4;

    /// <summary>True when the AS header is short enough to sit right-aligned over the scores.</summary>
    public static bool FitsOverAbilityScores(string label) => label.Length <= AbilityScoreHeaderMaxChars;

    /// <summary>True when the season header ("IBS") is short enough to sit right-aligned over the values; the
    /// "Season Strength" fallback or a one-word name isn't.</summary>
    public static bool FitsOverSeasonStrengths(string label) => label.Length <= SeasonStrengthHeaderMaxChars;
}
