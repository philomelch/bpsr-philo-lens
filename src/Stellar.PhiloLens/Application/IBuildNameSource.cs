using System.Collections.Generic;

namespace Stellar.PhiloLens.Application;

/// <summary>Port: display names for build pieces the typed game data doesn't name. Implemented in
/// Adapters over the game's own tables, so names follow the game language and new seasons.</summary>
internal interface IBuildNameSource
{
    /// <summary>Changes whenever the names below were (re)loaded, so callers know to redraw.</summary>
    int Version { get; }

    /// <summary>The game's own name for the season-talent system (e.g. "Deep Slumber"), or null when the
    /// game data doesn't name it.</summary>
    string? SeasonTalentTitle { get; }

    /// <summary>Re-checks the running season and reloads season-specific names when it changed. Reads game
    /// data, so only call while game reads are safe.</summary>
    void Refresh();

    /// <summary>The Battle Imagine's name (e.g. "Phantom Arachnocrab") for its skill id, or null.</summary>
    string? ImagineName(int skillId);

    /// <summary>The season-talent boards (e.g. "Fantasia Impact") the player runs, judged from their
    /// season-talent buffs, most evident first; empty when none can be told.</summary>
    IReadOnlyList<string> SeasonTalentBoardNames(IReadOnlyList<int> buffIds);
}
