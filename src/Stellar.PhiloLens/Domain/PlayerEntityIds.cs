namespace Stellar.PhiloLens.Domain;

/// <summary>Converts between a player's character id and their framework entity id, which packs the
/// character id above a 16-bit entity-type tag.</summary>
internal static class PlayerEntityIds
{
    private const int CharIdShift = 16;

    // The entity-type tag every player character carries in the low 16 bits.
    private const long PlayerTypeTag = 640;

    public static long FromCharId(long charId) => (charId << CharIdShift) | PlayerTypeTag;

    public static long CharIdOf(long entityId) => entityId >> CharIdShift;
}
