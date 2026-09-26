using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Application;

/// <summary>Port: reads a player's <see cref="PlayerBuild"/> (ability score, strength, Battle Imagines and
/// build effects). Only called while reads are safe (<see cref="IClassEvidenceSource.CanRead"/>).</summary>
internal interface IPlayerBuildSource
{
    /// <param name="entityId">The player's framework entity id value.</param>
    PlayerBuild Read(long entityId);
}
