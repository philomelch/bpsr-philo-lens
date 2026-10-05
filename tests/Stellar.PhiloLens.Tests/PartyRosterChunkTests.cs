using Stellar.PhiloLens.Adapters.Party;
using Xunit;

namespace Stellar.PhiloLens.Tests;

/// <summary>The chunk runs inside the game's Lua state, so these pin down what it may and may not do.</summary>
public sealed class PartyRosterChunkTests
{
    private const long CharId = 100001;

    [Fact]
    public void The_chunk_is_built_for_the_requested_player()
    {
        var chunk = PartyRosterChunk.For(CharId);

        Assert.Contains("local CHAR = '100001'", chunk);
        Assert.DoesNotContain("__CHAR__", chunk);
    }

    // Read-only: the only game function it may call is the UI lookup for the open card. Anything that
    // fetches (Async…), sends or changes state could reach the server or alter the game.
    [Theory]
    [InlineData("Async")]
    [InlineData("Send")]
    [InlineData("Request")]
    [InlineData("OpenView")]
    [InlineData("CloseView")]
    [InlineData("VMMgr")]
    public void The_chunk_calls_no_game_function_that_could_fetch_or_change_anything(string forbidden) =>
        Assert.DoesNotContain(forbidden, PartyRosterChunk.For(CharId));

    [Fact]
    public void The_chunk_only_asks_the_ui_for_the_open_card()
    {
        var chunk = PartyRosterChunk.For(CharId);

        Assert.Contains("Z.UIMgr:GetView('idcard')", chunk);
        Assert.Equal(1, Occurrences(chunk, "Z."));
    }

    [Fact]
    public void The_chunk_clears_its_result_first_and_traps_errors()
    {
        var chunk = PartyRosterChunk.For(CharId);

        Assert.StartsWith($"rawset(_G, '{PartyRosterChunk.ResultGlobal}', nil)", chunk);
        Assert.Contains("pcall(function()", chunk);
    }

    // Regression: the 64-bit "LL" suffix used to be stripped from every value, cutting "SKILL" to "SKI".
    [Fact]
    public void Text_values_keep_their_ending_and_only_64_bit_numbers_lose_the_ll_suffix()
    {
        var chunk = PartyRosterChunk.For(CharId);
        var stringBranch = chunk.IndexOf("if type(v) == 'string' then return (string.gsub(v, '%c', ' ')) end",
            System.StringComparison.Ordinal);
        var suffixStrip = chunk.IndexOf("'U?LL$'", System.StringComparison.Ordinal);

        Assert.True(stringBranch >= 0, "text() must return strings before any suffix handling");
        Assert.True(suffixStrip > stringBranch, "the LL suffix may only be stripped after strings were returned");
    }

    // Regression: the first field containing "season" was taken, so a season level or rank could show instead.
    // The in-game log confirmed the field is named seasonStrength, so only that one is read.
    [Fact]
    public void Season_strength_is_read_from_its_exact_field()
    {
        var chunk = PartyRosterChunk.For(CharId);

        Assert.Contains("rawget(stats, 'seasonStrength')", chunk);
        Assert.DoesNotContain("'season', 1, true", chunk);
    }

    // The player's own stats come from the profile they were found in, before the solo/party split, so solo
    // players get them too.
    [Fact]
    public void The_inspected_players_own_stats_are_reported_before_the_party_check()
    {
        var chunk = PartyRosterChunk.For(CharId);
        var ownStats = chunk.IndexOf("local own = findStats(profile)", System.StringComparison.Ordinal);
        var teamCheck = chunk.IndexOf("local team = rawget(profile, 'teamData')", System.StringComparison.Ordinal);

        Assert.True(ownStats >= 0 && ownStats < teamCheck);
    }

    private static int Occurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, System.StringComparison.Ordinal); i >= 0;
             i = text.IndexOf(value, i + value.Length, System.StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
