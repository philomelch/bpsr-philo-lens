using System.Linq;
using Stellar.PhiloLens.Adapters.Party;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class PartyRosterParserTests
{
    private const long CardCharId = 100001;
    private const long SecondCharId = 100002;
    private const long ThirdCharId = 100003;

    [Fact]
    public void A_party_is_read_with_each_members_role_class_and_stats()
    {
        // M, charId, roleId, classId, abilityScore, seasonStrength, enterTime, groupId, name
        var parsed = Parse("party", $"M\t{CardCharId}\t2\t13\t61805\t4279\t1\t2\tAria Vale");

        var member = Assert.Single(parsed.Reading.Roster!.Members);
        Assert.Equal(PartyRosterStatus.InParty, parsed.Reading.Status);
        Assert.Equal(new PartyMember(CardCharId, "Aria Vale", 13, PartyRole.Healer, 61_805, 4_279, Group: 2), member);
    }

    [Fact]
    public void Members_follow_the_partys_own_order()
    {
        var parsed = Parse("party",
            Member(CardCharId, "Aria", enterTime: 1),
            Member(SecondCharId, "Bram", enterTime: 2),
            Member(ThirdCharId, "Cleo", enterTime: 3),
            $"O\t{ThirdCharId}\t{CardCharId}\t{SecondCharId}");

        Assert.Equal(new[] { "Cleo", "Aria", "Bram" }, parsed.Reading.Roster!.Members.Select(m => m.Name));
    }

    [Fact]
    public void Without_a_party_order_members_follow_when_they_joined()
    {
        var parsed = Parse("party",
            Member(CardCharId, "Aria", enterTime: 30),
            Member(SecondCharId, "Bram", enterTime: 10));

        Assert.Equal(new[] { "Bram", "Aria" }, parsed.Reading.Roster!.Members.Select(m => m.Name));
    }

    // Regression: members were sorted by their raid group, which the roster's copy can hold stale.
    [Fact]
    public void Raid_members_keep_the_partys_order_whatever_group_they_carry()
    {
        var parsed = Parse("party",
            Member(CardCharId, "Aria", enterTime: 1, group: 2),
            Member(SecondCharId, "Bram", enterTime: 2),
            Member(ThirdCharId, "Cleo", enterTime: 3),
            $"O\t{CardCharId}\t{ThirdCharId}\t{SecondCharId}");

        Assert.Equal(new[] { "Aria", "Cleo", "Bram" }, parsed.Reading.Roster!.Members.Select(m => m.Name));
        Assert.Equal(2, parsed.Reading.Roster.Members[0].Group);
    }

    // pt4-style: a raid whose members all still sit in group 1 is only recognisable by the roster's flag.
    [Theory]
    [InlineData("teamMemberType=1", true)]
    [InlineData("teamMemberType=0", false)]
    [InlineData("teamNum=2", false)]
    public void The_rosters_team_type_marks_a_raid(string teamField, bool expectRaid)
    {
        var parsed = Parse("party",
            $"T\tteamNum=2\t{teamField}",
            Member(CardCharId, "Aria", enterTime: 1),
            Member(SecondCharId, "Bram", enterTime: 2));

        Assert.Equal(expectRaid, parsed.Reading.Roster!.IsRaid);
    }

    [Theory]
    [InlineData("solo")]
    [InlineData("party")]
    public void The_inspected_players_own_stats_are_read_solo_or_partied(string status)
    {
        var parsed = Parse(status, "P\t61404\t3863", Member(CardCharId, "Aria", enterTime: 1));

        Assert.Equal(new ProfileStats(61_404, 3_863), parsed.Reading.Profile);
    }

    [Fact]
    public void A_missing_season_strength_reads_as_zero()
    {
        var parsed = Parse("party", $"M\t{CardCharId}\t1\t11\t60000\t\t1\t1\tAria");

        Assert.Equal(0, parsed.Reading.Roster!.Members[0].SeasonStrength);
    }

    [Theory]
    [InlineData("solo", (int)PartyRosterStatus.Solo)]
    [InlineData("nocard", (int)PartyRosterStatus.Pending)]
    [InlineData("pending", (int)PartyRosterStatus.Pending)]
    [InlineData("shape", (int)PartyRosterStatus.Unreadable)]
    [InlineData("party", (int)PartyRosterStatus.Unreadable)] // a party without one readable member
    public void The_chunks_status_maps_to_a_reading(string status, int expected) =>
        Assert.Equal((PartyRosterStatus)expected, Parse(status).Reading.Status);

    [Fact]
    public void An_answer_about_another_player_is_ignored()
    {
        var parsed = PartyRosterParser.Parse($"philo-lens/1\t{SecondCharId}\tsolo", CardCharId);

        Assert.Equal(PartyRosterStatus.Pending, parsed.Reading.Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("something else entirely")]
    public void No_answer_reads_as_pending(string? output) =>
        Assert.Equal(PartyRosterStatus.Pending, PartyRosterParser.Parse(output, CardCharId).Reading.Status);

    [Fact]
    public void A_lua_error_is_unreadable_and_noted()
    {
        var parsed = PartyRosterParser.Parse($"philo-lens/1\t{CardCharId}\terror\tattempt to index a nil value", CardCharId);

        Assert.Equal(PartyRosterStatus.Unreadable, parsed.Reading.Status);
        Assert.Contains("error: attempt to index a nil value", parsed.Notes);
    }

    [Fact]
    public void Field_notes_keep_names_and_small_values_but_mask_ids()
    {
        var parsed = Parse("party",
            $"T\tteamId=987654321\tleaderId={CardCharId}0\tteamNum=1",
            "A\tfightPoint\tseasonStrength",
            Member(CardCharId, "Aria", enterTime: 1));

        Assert.Contains("team fields: teamId=<id> leaderId=<id> teamNum=1", parsed.Notes);
        Assert.Contains("stat fields: fightPoint seasonStrength", parsed.Notes);
    }

    // Regression: text fields (e.g. a team or leader name) used to be logged as-is.
    [Fact]
    public void Text_in_team_fields_is_never_logged()
    {
        var parsed = Parse("party",
            "T\tteamName=Aria's Crew\tleaderName=Aria\tisMatching=true",
            Member(CardCharId, "Aria", enterTime: 1));

        Assert.Contains("team fields: teamName=<text> leaderName=<text> isMatching=true", parsed.Notes);
    }

    [Fact]
    public void The_card_views_fields_are_noted_while_the_profile_is_not_found()
    {
        var parsed = Parse("pending", "V\tcardId_:number\tviewData:table");

        Assert.Equal(PartyRosterStatus.Pending, parsed.Reading.Status);
        Assert.Contains("card view fields: cardId_:number viewData:table", parsed.Notes);
    }

    private static ParsedPartyRoster Parse(string status, params string[] lines) =>
        PartyRosterParser.Parse(string.Join("\n", new[] { $"philo-lens/1\t{CardCharId}\t{status}" }.Concat(lines)), CardCharId);

    // A DPS Marksman with no stats; the tests that need more write the line out in full.
    private static string Member(long charId, string name, long enterTime, int group = 1) =>
        $"M\t{charId}\t1\t11\t0\t0\t{enterTime}\t{group}\t{name}";
}
