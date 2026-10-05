using System;
using System.Collections.Generic;
using Stellar.Abstractions.Domain.GameData;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;
using Stellar.PhiloLens.UI;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class PartyTextTests
{
    private const int Marksman = 11;
    private const int MarksmanSpec = 110001;
    private const int ShieldKnight = 12;
    private const int VerdantOracle = 5;

    private readonly FakeCombatData _combatData = new();
    private long _clockMs = 1_000_000;

    [Fact]
    public void The_header_shows_members_out_of_capacity_and_the_role_counts()
    {
        var text = NewText();

        text.Refresh(PartyOf(
            Entry("Aria", ShieldKnight, PartyRole.Tank),
            Entry("Bram", VerdantOracle, PartyRole.Healer),
            Entry("Cleo", Marksman, PartyRole.Dps)));

        Assert.Equal("Party (3/5)", text.Title);
        Assert.Equal("Tank: 1    Healers: 1    DPS: 1", text.Roles);
    }

    [Fact]
    public void Each_row_shows_name_class_spec_and_scores()
    {
        var text = NewText();

        var aria = Entry("Aria", Marksman, PartyRole.Dps);
        text.Refresh(PartyOf(aria with
        {
            Member = aria.Member with { AbilityScore = 61_404, SeasonStrength = 3_863 },
            ClassSpec = new ClassSpec(Marksman, MarksmanSpec, SpecSource.Talents),
        }));

        Assert.Equal(1, text.RowCount);
        Assert.Equal("Aria", text.Name(0));
        Assert.Equal($"Marksman · {ProfessionSpecs.Name(MarksmanSpec)}", text.ClassSpec(0));
        Assert.Equal("61,404", text.AbilityScore(0));
        Assert.Equal("3,863", text.SeasonStrength(0));
    }

    [Fact]
    public void The_season_column_shows_the_initials_of_the_games_name_for_the_stat()
    {
        _combatData.AttributeNames[StatLabels.SeasonStrengthAttributeId] = "Illusion-Breaking Strength";
        var text = NewText();

        text.Refresh(PartyOf(Entry("Aria", Marksman, PartyRole.Dps)));

        Assert.Equal("IBS", text.SeasonStrengthLabel);
        Assert.True(text.SeasonStrengthLabelFits);
    }

    // Short number headers sit right-aligned over the numbers; longer ones go left and widen their column.
    [Theory]
    [InlineData("IBS", true)]
    [InlineData("Season Strength", false)]
    [InlineData("幻想破壊力", false)]
    public void Only_a_short_season_header_sits_over_the_numbers(string label, bool fits) =>
        Assert.Equal(fits, PartyColumns.FitsOverSeasonStrengths(label));

    [Theory]
    [InlineData("AS", true)]
    [InlineData("評価", true)]
    [InlineData("Ability Score", false)]
    public void Only_a_short_ability_score_header_sits_over_the_numbers(string label, bool fits) =>
        Assert.Equal(fits, PartyColumns.FitsOverAbilityScores(label));

    [Theory]
    [InlineData("Illusion-Breaking Strength", "IBS")]
    [InlineData("dream force power", "DFP")]
    [InlineData("  Twin  Words ", "TW")]
    [InlineData("幻想破壊力", "幻想破壊力")]
    [InlineData("Strength", "Strength")]
    [InlineData("Élan Vital", "ÉV")]
    // Regression: non-Latin words gave meaningless initials (a lone Thai vowel sign) or half a surrogate pair.
    [InlineData("เสริม พลัง", "เสริม พลัง")]
    [InlineData("\U0001D400bc Def", "\U0001D400bc Def")]
    public void Initials_take_the_first_letter_of_each_word_but_keep_a_single_word(string name, string expected) =>
        Assert.Equal(expected, StatLabels.Initials(name));

    [Fact]
    public void Without_a_game_name_the_season_column_uses_the_neutral_label()
    {
        var text = NewText();

        text.Refresh(PartyOf(Entry("Aria", Marksman, PartyRole.Dps)));

        Assert.Equal("stats.season_strength", text.SeasonStrengthLabel);
        Assert.False(text.SeasonStrengthLabelFits);
    }

    [Fact]
    public void An_unknown_season_strength_leaves_the_cell_empty()
    {
        var text = NewText();

        var aria = Entry("Aria", Marksman, PartyRole.Dps);
        text.Refresh(PartyOf(aria with { Member = aria.Member with { SeasonStrength = 0 } }));

        Assert.Equal(string.Empty, text.SeasonStrength(0));
    }

    // The card's copy of each member's raid group goes stale (a 20-member raid showed groups of 6, 4, 8 and 2),
    // so a raid is one list in the party's order, still counted out of twenty.
    [Fact]
    public void A_raid_is_one_list_without_group_headings_out_of_twenty()
    {
        var text = NewText();

        text.Refresh(PartyOf(
            Entry("Aria", ShieldKnight, PartyRole.Tank),
            Entry("Bram", VerdantOracle, PartyRole.Healer),
            InGroup(Entry("Cleo", Marksman, PartyRole.Dps), 3),
            InGroup(Entry("Dana", Marksman, PartyRole.Dps), 2)));

        Assert.Equal("Party (4/20)", text.Title);
        Assert.Equal(4, text.RowCount);
        Assert.Equal("<b>Name</b>\nAria\nBram\nCleo\nDana", text.NamesColumn);
    }

    // Each column is one text: its bold header, then a line per member, so every column has the same lines.
    [Fact]
    public void Each_column_is_its_header_then_a_line_per_member_even_where_a_value_is_missing()
    {
        var text = NewText();
        var bram = Entry("Bram", Marksman, PartyRole.Dps);

        text.Refresh(PartyOf(
            Entry("Aria", ShieldKnight, PartyRole.Tank),
            bram with { Member = bram.Member with { SeasonStrength = 0 } }));

        Assert.Equal("<b>Name</b>\nAria\nBram", text.NamesColumn);
        Assert.Equal("<b>Class</b>\nShield Knight\nMarksman", text.ClassesColumn);
        Assert.Equal("<b>AS</b> \n60,000 \n60,000 ", text.AbilityScoresColumn);
        Assert.Equal("<b>stats.season_strength</b> \n4,000 \n  ", text.SeasonStrengthsColumn);
    }

    // Regression: Unity right-aligns by a line's last character, so a number ending in a thin "1" sat further right
    // than the rest; and a separately drawn bold header dropped its trailing space and sat a character right of
    // its numbers. The header is now the first line of the same text, and every line ends with the same character.
    [Fact]
    public void Right_aligned_numbers_and_their_header_all_end_with_the_same_character_in_one_text()
    {
        _combatData.AttributeNames[StatLabels.SeasonStrengthAttributeId] = "Illusion-Breaking Strength";
        var text = NewText();
        var aria = Entry("Aria", Marksman, PartyRole.Dps);

        text.Refresh(PartyOf(aria with { Member = aria.Member with { AbilityScore = 61_451, SeasonStrength = 4_231 } }));

        Assert.Equal("<b>AS</b> \n61,451 ", text.AbilityScoresColumn);
        Assert.Equal("<b>IBS</b> \n4,231 ", text.SeasonStrengthsColumn);
    }

    // Names come from other players; a "<" mustn't open a rich-text tag that restyles the lines after it.
    [Fact]
    public void A_name_cannot_inject_rich_text()
    {
        var text = NewText();

        text.Refresh(PartyOf(Entry("<b>Aria", Marksman, PartyRole.Dps)));

        Assert.Equal("<b>Name</b>\n‹b>Aria", text.NamesColumn);
    }

    [Fact]
    public void A_full_raid_fits()
    {
        var members = new InspectedPartyMember[PartyRoster.RaidCapacity];
        for (var i = 0; i < members.Length; i++)
            members[i] = InGroup(Entry("Member" + i, Marksman, PartyRole.Dps), 1 + i / PartyRoster.RegularCapacity);
        var text = NewText();

        text.Refresh(PartyOf(members));

        Assert.Equal(PartyText.MaxRows, text.RowCount);
        Assert.Equal("Member19", text.Name(PartyText.MaxRows - 1));
    }

    [Fact]
    public void No_party_means_no_rows()
    {
        var text = NewText();
        text.Refresh(PartyOf(Entry("Aria", Marksman, PartyRole.Dps)));

        text.Refresh(null);

        Assert.False(text.HasParty);
    }

    [Fact]
    public void A_class_name_not_loaded_yet_appears_after_the_retry()
    {
        _combatData.ProfessionNames.Remove(Marksman);
        var text = NewText();
        var party = PartyOf(Entry("Aria", Marksman, PartyRole.Dps));
        text.Refresh(party);
        Assert.Equal("Class 11", text.ClassSpec(0));

        _combatData.ProfessionNames[Marksman] = "Marksman";
        _clockMs += 1_000;
        text.Refresh(party);

        Assert.Equal("Marksman", text.ClassSpec(0));
    }

    private PartyText NewText() => new(new FakeLocalization(), _combatData, () => _clockMs);

    private static InspectedPartyMember Entry(string name, int classId, PartyRole role) =>
        new(new PartyMember(100001, name, classId, role, 60_000, 4_000), new ClassSpec(classId, 0, SpecSource.None));

    private static InspectedPartyMember InGroup(InspectedPartyMember entry, int group) =>
        entry with { Member = entry.Member with { Group = group } };

    private static InspectedParty PartyOf(params InspectedPartyMember[] members)
    {
        var roster = new PartyMember[members.Length];
        for (var i = 0; i < members.Length; i++) roster[i] = members[i].Member;
        return new InspectedParty(new PartyRoster(roster), members);
    }

    private sealed class FakeLocalization : ILocalization
    {
        public string Language => "en";

        public event Action? LanguageChanged { add { } remove { } }

        public string T(string key) => key switch
        {
            "stats.none" => "—",
            "party.column.name" => "Name",
            "party.column.class" => "Class",
            "party.column.ability_score" => "AS",
            _ => key,
        };

        public string TFormat(string key, params object[] args) => key switch
        {
            "party.title" => $"Party ({args[0]}/{args[1]})",
            "party.roles" => $"Tank: {args[0]}    Healers: {args[1]}    DPS: {args[2]}",
            "inspect.class_id" => $"Class {args[0]}",
            _ => string.Join(" · ", args),
        };
    }

    private sealed class FakeCombatData : IGameDataCombat
    {
        public Dictionary<int, string> ProfessionNames { get; } = new()
        {
            [Marksman] = "Marksman",
            [ShieldKnight] = "Shield Knight",
            [VerdantOracle] = "Verdant Oracle",
        };

        public BuffInfo? GetBuff(int id) => null;
        public SkillInfo? GetSkill(int id) => null;
        public IReadOnlyCollection<BuffInfo> AllBuffs() => Array.Empty<BuffInfo>();
        public TalentInfo? GetTalent(int id) => null;
        public Dictionary<int, string> AttributeNames { get; } = new();

        public AttributeInfo? GetAttribute(int id) =>
            AttributeNames.TryGetValue(id, out var name) ? new AttributeInfo(id, name, string.Empty, string.Empty, default) : null;
        public AttributeProfileInfo? GetAttributeProfile(int id) => null;
        public DamageAttrInfo? GetDamageAttr(int id) => null;

        public ProfessionInfo? GetProfession(int id) =>
            ProfessionNames.TryGetValue(id, out var name) ? new ProfessionInfo(id, name, string.Empty, true, Array.Empty<int>()) : null;
    }
}
