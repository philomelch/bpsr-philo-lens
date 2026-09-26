using System.Collections.Generic;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Application;

/// <summary>One inspected player as the UI shows it.</summary>
internal readonly record struct InspectedPlayer(long EntityId, ClassSpec ClassSpec, PlayerBuild Build);

/// <summary>Tracks the player being inspected and keeps their class/spec current. Remembers each
/// player's last real class, so a Battle Imagine transform never replaces it on screen.</summary>
internal sealed class InspectionService
{
    private readonly IClassEvidenceSource _source;
    private readonly IPlayerBuildSource _buildSource;

    // Only players the user inspected end up here, so it stays small for a session.
    private readonly Dictionary<long, int> _lastKnownClassIds = new();

    private long? _targetEntityId;

    public InspectionService(IClassEvidenceSource source, IPlayerBuildSource buildSource)
    {
        _source = source;
        _buildSource = buildSource;
    }

    /// <summary>The inspected player, or null before the first successful read.</summary>
    public InspectedPlayer? Current { get; private set; }

    /// <summary>Starts inspecting <paramref name="entityId"/> and reads it right away when possible.</summary>
    public void Inspect(long entityId)
    {
        _targetEntityId = entityId;
        Current = null;
        Refresh();
    }

    /// <summary>Re-reads the inspected player. Keeps the last value while reads are unsafe.</summary>
    public void Refresh()
    {
        if (_targetEntityId is not { } entityId || !_source.CanRead) return;

        _lastKnownClassIds.TryGetValue(entityId, out var lastKnownClassId);
        var classSpec = ClassSpecResolver.Resolve(_source.Read(entityId), lastKnownClassId);
        if (classSpec.HasClass) _lastKnownClassIds[entityId] = classSpec.ClassId;
        Current = new InspectedPlayer(entityId, classSpec, _buildSource.Read(entityId));
    }
}
