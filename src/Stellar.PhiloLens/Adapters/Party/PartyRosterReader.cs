using System;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;

namespace Stellar.PhiloLens.Adapters.Party;

/// <summary>Reads the open profile card's party roster from the game's Lua state, through the framework's
/// <see cref="ILua"/> escape hatch (no typed service carries the roster). The chunk only reads data the game
/// already loaded for the card; see <see cref="PartyRosterChunk"/>. Main thread only.</summary>
internal sealed partial class PartyRosterReader : IPartyRosterSource
{
    private readonly ILua _lua;
    private readonly IPluginLog _log;

    public PartyRosterReader(ILua lua, IPluginLog log)
    {
        _lua = lua;
        _log = log;
    }

    public PartyRosterReading Read(long charId)
    {
        if (!_lua.Ready) return PartyRosterReading.Pending;

        try
        {
            _lua.DoString(PartyRosterChunk.For(charId));
            var parsed = PartyRosterParser.Parse(_lua.ReadGlobalString(PartyRosterChunk.ResultGlobal), charId);
            LogOutcome(parsed, charId);
            return parsed.Reading;
        }
        catch (Exception ex)
        {
            // The Lua bridge is reflection over game internals; a game update can break it. Hide the section.
            LogFailure(ex);
            return PartyRosterReading.Unreadable;
        }
    }
}
