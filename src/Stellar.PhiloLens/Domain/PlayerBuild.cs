using System;
using System.Collections.Generic;

namespace Stellar.PhiloLens.Domain;

/// <summary>An equipped Battle Imagine and its tier.</summary>
internal readonly record struct EquippedImagine(int SkillId, int Tier);

/// <summary>A buff reduced to what the build needs: its table id and where it came from.</summary>
/// <param name="BaseId">The buff's table id.</param>
/// <param name="SourceKind">The game's fight-source type; 13 = season talent.</param>
internal readonly record struct BuffSource(int BaseId, int SourceKind);

/// <summary>Picks the season-talent effects out of a player's buffs.</summary>
internal static class SeasonTalentBuffs
{
    // The game's fight-source type (EFightSource) for season talents (named "Deep Slumber" in season 3).
    private const int SeasonTalentSource = 13;

    /// <summary>The season-talent buff ids in <paramref name="buffs"/>, sorted and de-duplicated so the
    /// order is stable no matter how the framework orders the buffs.</summary>
    public static IReadOnlyList<int> From(IReadOnlyList<BuffSource> buffs)
    {
        var ids = new SortedSet<int>();
        for (var i = 0; i < buffs.Count; i++)
        {
            if (buffs[i].SourceKind == SeasonTalentSource) ids.Add(buffs[i].BaseId);
        }

        return ids.Count == 0 ? Array.Empty<int>() : new List<int>(ids);
    }

    /// <summary>True when both lists hold the same ids, so callers can keep the instance they show.</summary>
    public static bool SameIds(IReadOnlyList<int> left, IReadOnlyList<int> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i]) return false;
        }

        return true;
    }
}

/// <summary>What a player's build shows beyond class and spec. <c>0</c> means not known.</summary>
/// <param name="AbilityScore">The server-computed ability score.</param>
/// <param name="SeasonStrength">The season strength stat (e.g. "Illusion-Breaking Strength"); only sent while the player is in range.</param>
/// <param name="Imagines">Equipped Battle Imagines with their tier.</param>
/// <param name="SeasonTalentBuffIds">Season-talent effects, as buff ids.</param>
internal readonly record struct PlayerBuild(long AbilityScore, long SeasonStrength,
    IReadOnlyList<EquippedImagine> Imagines, IReadOnlyList<int> SeasonTalentBuffIds)
{
    public static PlayerBuild Empty => new(0, 0, Array.Empty<EquippedImagine>(), Array.Empty<int>());
}
