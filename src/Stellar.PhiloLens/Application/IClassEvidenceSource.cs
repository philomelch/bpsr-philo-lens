using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Application;

/// <summary>Port: reads a player's raw class/spec readings (<see cref="ClassEvidence"/>, resolved by
/// <see cref="ClassSpecResolver"/>). Implemented in Adapters over the framework, and by fakes in tests.</summary>
internal interface IClassEvidenceSource
{
    /// <summary>False while live game state must not be read (e.g. during a zone load).</summary>
    bool CanRead { get; }

    /// <param name="entityId">The player's framework entity id value.</param>
    ClassEvidence Read(long entityId);
}
