using System.Collections.Generic;

namespace Stellar.PhiloLens.Domain;

/// <summary>Works out which season-talent boards (e.g. "Fantasia Impact") a player is running from their
/// season-talent buffs. Each buff comes from a node, and a node belongs to one board; a few shared nodes
/// sit on several boards and say nothing about which one is active, so they are ignored.</summary>
internal static class SeasonTalentBoards
{
    /// <summary>The boards behind <paramref name="buffIds"/>, the one with the most matching buffs first.</summary>
    /// <param name="buffIds">The player's season-talent buffs.</param>
    /// <param name="boardsByBuff">For each known buff, the boards whose nodes grant it.</param>
    public static List<int> Active(IReadOnlyList<int> buffIds, IReadOnlyDictionary<int, IReadOnlyList<int>> boardsByBuff)
    {
        var votes = new Dictionary<int, int>();
        for (var i = 0; i < buffIds.Count; i++)
        {
            if (!boardsByBuff.TryGetValue(buffIds[i], out var boards) || boards.Count != 1) continue;
            votes.TryGetValue(boards[0], out var count);
            votes[boards[0]] = count + 1;
        }

        var active = new List<int>(votes.Keys);
        active.Sort((left, right) => votes[right] != votes[left] ? votes[right].CompareTo(votes[left]) : left.CompareTo(right));
        return active;
    }
}
