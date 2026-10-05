using System;
using Stellar.PhiloLens.Application;

namespace Stellar.PhiloLens.Adapters.Party;

// Proof-of-concept logging: where the game keeps the roster, and some of its field names, were found by shape
// rather than known for sure. Each finished read logs what it saw (names and small values only; no player
// names, ids masked) so an in-game test can pin them down before this becomes a framework PR.
internal sealed partial class PartyRosterReader
{
    private const string LogTag = "[Stellar.PhiloLens] party roster:";

    // A pending read repeats about once a second while the card loads; its notes are logged once per player.
    private long _pendingLoggedFor;

    private void LogOutcome(ParsedPartyRoster parsed, long charId)
    {
        if (parsed.Reading.Status == PartyRosterStatus.Pending)
        {
            if (parsed.Notes.Count == 0 || _pendingLoggedFor == charId) return;
            _pendingLoggedFor = charId;
        }

        var members = parsed.Reading.Roster?.Members.Count ?? 0;
        _log.Info($"{LogTag} {parsed.Reading.Status}, {members} member(s)");
        foreach (var note in parsed.Notes) _log.Info($"{LogTag}   {note}");
    }

    private void LogFailure(Exception ex) => _log.Warning($"{LogTag} read failed: {ex.Message}");
}
