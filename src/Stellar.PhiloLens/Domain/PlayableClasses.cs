using System.Collections.Generic;

namespace Stellar.PhiloLens.Domain;

/// <summary>The game's player-selectable classes, and how spec ids relate to them.</summary>
/// <remarks>The profession attribute is not always a real class: while a Battle Imagine transform is
/// active it reads the transform's row instead (e.g. 14 = Lucy, 15 = Natsu), so every class value must
/// pass <see cref="IsPlayable"/> before it is shown. When the game adds a class, add its id here;
/// until then it resolves to unknown rather than a guess.</remarks>
internal static class PlayableClasses
{
    // Stormblade 1, Frost Mage 2, Twin Striker 3, Wind Knight 4, Verdant Oracle 5, Heavy Guardian 9,
    // Marksman 11, Shield Knight 12, Beat Performer 13.
    private static readonly HashSet<int> Ids = new() { 1, 2, 3, 4, 5, 9, 11, 12, 13 };

    // Spec ids are "<classId>_00_<specIndex>" read as a decimal number: 50001 = Verdant Oracle's first spec.
    private const int SpecIdClassDivisor = 10_000;

    public static bool IsPlayable(int classId) => Ids.Contains(classId);

    /// <summary>The class a spec belongs to, or 0 when <paramref name="specId"/> is not a spec id.</summary>
    public static int ClassOfSpec(int specId)
    {
        var classId = specId / SpecIdClassDivisor;
        return IsPlayable(classId) ? classId : 0;
    }
}
