using System;
using System.Collections.Generic;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Application;

/// <summary>One party member as the UI shows it: the roster entry plus the best class/spec known.</summary>
internal sealed record InspectedPartyMember(PartyMember Member, ClassSpec ClassSpec);

/// <summary>The inspected player's party as the UI shows it.</summary>
internal sealed record InspectedParty(PartyRoster Roster, IReadOnlyList<InspectedPartyMember> Members);

/// <summary>Tracks the inspected player's party, and their own stats from the same card data. The card is read
/// once per inspection, retried at a slow pace while it is still loading. Each member's spec is then
/// re-resolved at the same slow pace, so a member who comes into range or casts shows their spec without a
/// per-tick cost.</summary>
internal sealed class PartyInspectionService
{
    private const long ReadIntervalMs = 1_000;

    // The card's data normally arrives within a second; past this the read is given up for the inspection.
    private const int MaxReadAttempts = 10;
    private const long SpecRefreshIntervalMs = 1_000;

    private readonly IPartyRosterSource _rosterSource;
    private readonly IClassEvidenceSource _classSource;
    private readonly Func<long> _clockMs;

    private long? _charId;
    private PartyRoster? _roster;
    private int _readAttempts;
    private long _lastReadMs;
    private long _lastSpecRefreshMs;

    /// <param name="clockMs">Monotonic milliseconds for the read and refresh pacing.</param>
    public PartyInspectionService(IPartyRosterSource rosterSource, IClassEvidenceSource classSource, Func<long> clockMs)
    {
        _rosterSource = rosterSource;
        _classSource = classSource;
        _clockMs = clockMs;
    }

    /// <summary>The inspected player's party; null while unknown or when they aren't in one.</summary>
    public InspectedParty? Current { get; private set; }

    /// <summary>The inspected player's own stats from the same card data; zeros until it has been read.</summary>
    public ProfileStats Profile { get; private set; }

    /// <summary>Starts tracking the party of the player with <paramref name="entityId"/>.</summary>
    public void Inspect(long entityId)
    {
        _charId = PlayerEntityIds.CharIdOf(entityId);
        _roster = null;
        _readAttempts = 0;
        Current = null;
        Profile = default;
        Refresh();
    }

    /// <summary>Makes the inspected player's row show the stats the main view shows (live while they're in range,
    /// from their card otherwise), so the two never disagree; the party's copy of a member's stats can lag behind.
    /// A <c>0</c> (not known) keeps the row's value. Builds a new party only when the row's numbers change.</summary>
    public void ShowInspectedPlayerStats(long abilityScore, long seasonStrength)
    {
        if (Current is not { } party || _charId is not { } charId) return;

        var members = party.Members;
        for (var row = 0; row < members.Count; row++)
        {
            var member = members[row].Member;
            if (member.CharId != charId) continue;

            var shownAbilityScore = abilityScore > 0 ? abilityScore : member.AbilityScore;
            var shownSeasonStrength = seasonStrength > 0 ? seasonStrength : member.SeasonStrength;
            if (member.AbilityScore == shownAbilityScore && member.SeasonStrength == shownSeasonStrength) return;

            _roster = _roster!.WithStatsFor(charId, shownAbilityScore, shownSeasonStrength);
            var updated = new InspectedPartyMember[members.Count];
            for (var i = 0; i < members.Count; i++) updated[i] = members[i] with { Member = _roster.Members[i] };
            Current = new InspectedParty(_roster, updated);
            return;
        }
    }

    /// <summary>Reads the roster if it's still missing and due, and keeps the members' specs current.</summary>
    public void Refresh()
    {
        if (_charId is not { } charId || !_classSource.CanRead) return;

        var nowMs = _clockMs();
        if (_roster is null && !TryReadRoster(charId, nowMs)) return;
        if (Current is null || nowMs - _lastSpecRefreshMs >= SpecRefreshIntervalMs) RefreshSpecs(nowMs);
    }

    private bool TryReadRoster(long charId, long nowMs)
    {
        var firstRead = _readAttempts == 0;
        if (_readAttempts >= MaxReadAttempts || (!firstRead && nowMs - _lastReadMs < ReadIntervalMs)) return false;

        _readAttempts++;
        _lastReadMs = nowMs;
        var reading = _rosterSource.Read(charId);
        Profile = reading.Profile;
        if (reading is { Status: PartyRosterStatus.InParty, Roster: { } roster })
        {
            _roster = roster;
            return true;
        }

        // Solo or unreadable won't change while the card stays open, so stop asking.
        if (reading.Status != PartyRosterStatus.Pending) _readAttempts = MaxReadAttempts;
        return false;
    }

    private void RefreshSpecs(long nowMs)
    {
        _lastSpecRefreshMs = nowMs;
        var members = _roster!.Members;
        var resolved = new InspectedPartyMember[members.Count];
        var changed = Current is null;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            var evidence = _classSource.Read(PlayerEntityIds.FromCharId(member.CharId));
            // The roster's class is the member's real class, so it stands in while a live reading is a transform.
            resolved[i] = new InspectedPartyMember(member, ClassSpecResolver.Resolve(evidence, member.ClassId));
            if (!changed && Current!.Members[i].ClassSpec != resolved[i].ClassSpec) changed = true;
        }

        if (changed) Current = new InspectedParty(_roster, resolved);
    }
}
