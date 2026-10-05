using System.Collections.Generic;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class PartyInspectionServiceTests
{
    private const long CardCharId = 100001;
    private const long MemberCharId = 100002;
    private const int Marksman = 11;
    private const int MarksmanSpec = 110001;
    private const int ShieldKnight = 12;
    private const int LucyTransform = 14;

    private static readonly long CardEntityId = PlayerEntityIds.FromCharId(CardCharId);
    private static readonly long MemberEntityId = PlayerEntityIds.FromCharId(MemberCharId);

    private readonly FakeRosterSource _roster = new();
    private readonly FakeClassEvidenceSource _evidence = new();
    private long _clockMs = 1_000_000;

    [Fact]
    public void Inspect_reads_the_party_right_away()
    {
        _roster.Reading = InParty(Member(CardCharId, Marksman), Member(MemberCharId, ShieldKnight));
        var service = NewService();

        service.Inspect(CardEntityId);

        Assert.Equal(CardCharId, _roster.LastCharId);
        Assert.Equal(2, service.Current?.Members.Count);
    }

    [Fact]
    public void A_card_still_loading_is_read_again_a_second_later_not_every_tick()
    {
        var service = NewService();
        service.Inspect(CardEntityId);

        service.Refresh();
        Assert.Equal(1, _roster.Reads);

        _clockMs += 1_000;
        _roster.Reading = InParty(Member(CardCharId, Marksman));
        service.Refresh();

        Assert.Equal(2, _roster.Reads);
        Assert.NotNull(service.Current);
    }

    [Fact]
    public void A_solo_player_is_not_read_again()
    {
        _roster.Reading = PartyRosterReading.Solo;
        var service = NewService();
        service.Inspect(CardEntityId);

        _clockMs += 5_000;
        service.Refresh();

        Assert.Equal(1, _roster.Reads);
        Assert.Null(service.Current);
    }

    [Fact]
    public void A_solo_players_own_stats_are_kept_from_the_card()
    {
        _roster.Reading = PartyRosterReading.Solo with { Profile = new ProfileStats(61_404, 3_863) };
        var service = NewService();

        service.Inspect(CardEntityId);

        Assert.Equal(new ProfileStats(61_404, 3_863), service.Profile);
    }

    // 1.png: the main view showed 3,604 while the party row showed the party's older copy, 3,904. Regression from
    // review: the row then took the card's value even while the main view showed a fresher live one.
    [Fact]
    public void The_inspected_players_row_shows_what_the_main_view_shows()
    {
        _roster.Reading = InParty(Member(CardCharId, Marksman), Member(MemberCharId, ShieldKnight));
        var service = NewService();
        service.Inspect(CardEntityId);

        service.ShowInspectedPlayerStats(abilityScore: 62_000, seasonStrength: 3_604);

        var own = service.Current!.Members[0].Member;
        var other = service.Current.Members[1].Member;
        Assert.Equal((62_000, 3_604), (own.AbilityScore, own.SeasonStrength));
        Assert.Equal((60_000, 4_000), (other.AbilityScore, other.SeasonStrength));
    }

    [Fact]
    public void An_unknown_main_view_stat_keeps_the_rows_value()
    {
        _roster.Reading = InParty(Member(CardCharId, Marksman));
        var service = NewService();
        service.Inspect(CardEntityId);

        service.ShowInspectedPlayerStats(abilityScore: 0, seasonStrength: 3_604);

        Assert.Equal((60_000, 3_604), (service.Current!.Members[0].Member.AbilityScore, service.Current.Members[0].Member.SeasonStrength));
    }

    // Called every tick, so unchanged numbers must not build a new party (the UI would redraw each time).
    [Fact]
    public void Unchanged_stats_keep_the_same_party_instance()
    {
        _roster.Reading = InParty(Member(CardCharId, Marksman));
        var service = NewService();
        service.Inspect(CardEntityId);
        service.ShowInspectedPlayerStats(62_000, 3_604);
        var shown = service.Current;

        service.ShowInspectedPlayerStats(62_000, 3_604);
        service.ShowInspectedPlayerStats(0, 0);

        Assert.Same(shown, service.Current);
    }

    [Fact]
    public void The_shown_stats_survive_the_slow_spec_refresh()
    {
        _roster.Reading = InParty(Member(CardCharId, Marksman));
        var service = NewService();
        service.Inspect(CardEntityId);
        service.ShowInspectedPlayerStats(62_000, 3_604);

        _evidence.Evidence[CardEntityId] = new ClassEvidence(Marksman, 0, 0, MarksmanSpec, 0);
        _clockMs += 1_000;
        service.Refresh();

        Assert.Equal(MarksmanSpec, service.Current!.Members[0].ClassSpec.SpecId);
        Assert.Equal(62_000, service.Current.Members[0].Member.AbilityScore);
    }

    [Fact]
    public void Inspecting_another_player_forgets_the_previous_stats()
    {
        _roster.Reading = PartyRosterReading.Solo with { Profile = new ProfileStats(61_404, 3_863) };
        var service = NewService();
        service.Inspect(CardEntityId);

        _evidence.CanRead = false;
        service.Inspect(MemberEntityId);

        Assert.Equal(default, service.Profile);
    }

    [Fact]
    public void Reading_is_given_up_after_ten_tries()
    {
        var service = NewService();
        service.Inspect(CardEntityId);

        for (var i = 0; i < 20; i++)
        {
            _clockMs += 1_000;
            service.Refresh();
        }

        Assert.Equal(10, _roster.Reads);
    }

    [Fact]
    public void Nothing_is_read_while_game_reads_are_unsafe()
    {
        _evidence.CanRead = false;
        var service = NewService();

        service.Inspect(CardEntityId);

        Assert.Equal(0, _roster.Reads);
    }

    [Fact]
    public void A_nearby_members_spec_is_shown()
    {
        _roster.Reading = InParty(Member(MemberCharId, Marksman));
        _evidence.Evidence[MemberEntityId] = new ClassEvidence(Marksman, 0, 0, MarksmanSpec, 0);
        var service = NewService();

        service.Inspect(CardEntityId);

        Assert.Equal(new ClassSpec(Marksman, MarksmanSpec, SpecSource.Talents), service.Current?.Members[0].ClassSpec);
    }

    [Fact]
    public void A_member_out_of_range_shows_the_rosters_class()
    {
        _roster.Reading = InParty(Member(MemberCharId, ShieldKnight));
        var service = NewService();

        service.Inspect(CardEntityId);

        Assert.Equal(new ClassSpec(ShieldKnight, 0, SpecSource.None), service.Current?.Members[0].ClassSpec);
    }

    [Fact]
    public void A_transformed_member_keeps_the_rosters_class()
    {
        _roster.Reading = InParty(Member(MemberCharId, Marksman));
        _evidence.Evidence[MemberEntityId] = new ClassEvidence(LucyTransform, 0, 0, 0, 0);
        var service = NewService();

        service.Inspect(CardEntityId);

        Assert.Equal(Marksman, service.Current?.Members[0].ClassSpec.ClassId);
    }

    [Fact]
    public void A_spec_seen_later_appears_on_the_next_slow_refresh()
    {
        _roster.Reading = InParty(Member(MemberCharId, Marksman));
        var service = NewService();
        service.Inspect(CardEntityId);

        _evidence.Evidence[MemberEntityId] = new ClassEvidence(Marksman, 0, 0, MarksmanSpec, 0);
        service.Refresh();
        Assert.Equal(0, service.Current?.Members[0].ClassSpec.SpecId);

        _clockMs += 1_000;
        service.Refresh();
        Assert.Equal(MarksmanSpec, service.Current?.Members[0].ClassSpec.SpecId);
    }

    [Fact]
    public void An_unchanged_party_keeps_the_same_instance_so_the_ui_does_not_redraw()
    {
        _roster.Reading = InParty(Member(MemberCharId, Marksman));
        var service = NewService();
        service.Inspect(CardEntityId);
        var first = service.Current;

        _clockMs += 1_000;
        service.Refresh();

        Assert.Same(first, service.Current);
    }

    [Fact]
    public void Inspecting_another_player_drops_the_previous_party()
    {
        _roster.Reading = InParty(Member(MemberCharId, Marksman));
        var service = NewService();
        service.Inspect(CardEntityId);

        _roster.Reading = PartyRosterReading.Solo;
        service.Inspect(MemberEntityId);

        Assert.Null(service.Current);
        Assert.Equal(MemberCharId, _roster.LastCharId);
    }

    private PartyInspectionService NewService() => new(_roster, _evidence, () => _clockMs);

    private static PartyMember Member(long charId, int classId) =>
        new(charId, "Member" + charId, classId, PartyRole.Dps, 60_000, 4_000);

    private static PartyRosterReading InParty(params PartyMember[] members) =>
        new(PartyRosterStatus.InParty, new PartyRoster(members));

    private sealed class FakeRosterSource : IPartyRosterSource
    {
        public PartyRosterReading Reading { get; set; } = PartyRosterReading.Pending;
        public int Reads { get; private set; }
        public long LastCharId { get; private set; }

        public PartyRosterReading Read(long charId)
        {
            Reads++;
            LastCharId = charId;
            return Reading;
        }
    }

    private sealed class FakeClassEvidenceSource : IClassEvidenceSource
    {
        public bool CanRead { get; set; } = true;
        public Dictionary<long, ClassEvidence> Evidence { get; } = new();

        public ClassEvidence Read(long entityId) => Evidence.TryGetValue(entityId, out var evidence) ? evidence : default;
    }
}
