namespace Stellar.PhiloLens.Domain;

/// <summary>How a player's spec was determined. Shown to the user, because a spec seen from casts in
/// this zone is stronger evidence than none at all.</summary>
internal enum SpecSource
{
    /// <summary>No spec known; only the class is shown.</summary>
    None,

    /// <summary>The player was seen using a spec-defining skill in the current zone.</summary>
    Casts,

    /// <summary>Read from the spec's root talent buff, which the server sends when the player comes
    /// into range. The reliable source.</summary>
    Talents,
}

/// <summary>A player's resolved class and, when known, spec. <c>0</c> means unknown.</summary>
internal readonly record struct ClassSpec(int ClassId, int SpecId, SpecSource SpecSource)
{
    public static ClassSpec Unknown => default;

    public bool HasClass => ClassId != 0;

    public bool HasSpec => SpecId != 0;
}

/// <summary>Everything observed about one player's class and spec, before any of it is trusted.
/// Each value is the raw reading from its source, <c>0</c> when that source has nothing.</summary>
/// <param name="LiveClassId">The player's profession attribute; a transform id while transformed.</param>
/// <param name="PartyClassId">The class the party roster reports (party members only).</param>
/// <param name="ProfileClassId">The class from the player's profile data (loaded with the profile card).</param>
/// <param name="TalentSpecId">The spec read from the player's talent buffs; only while they are in range.</param>
/// <param name="CastSpecId">The spec recognised from skills the player cast in this zone.</param>
internal readonly record struct ClassEvidence(int LiveClassId, int PartyClassId, int ProfileClassId,
    int TalentSpecId, int CastSpecId);
