using System.Collections.Generic;
using Bokura;
using Stellar.Abstractions.Domain.DeepSlumber;
using Stellar.Abstractions.Domain.GameData;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Adapters.GameTables;
using Xunit;

namespace Stellar.PhiloLens.Tests;

// The fake tables are static, so these tests must not run in parallel with each other.
[Collection(nameof(GameTableNamesTests))]
public sealed class GameTableNamesTests
{
    private const int ArachnocrabSkill = 3938;
    private const int ArachnocrabItem = 3000029;
    private const int TinaSkill = 3921;
    private const int TinaItem = 3000011;
    private const int FantasiaImpactBuff = 3002010;
    private const int ReconstructBuff = 3002030;
    private const int BlinkBreathBuff = 3002090;
    private const int EndlessMindBuff = 3003410;
    private const int EndlessMindChildBuff = 3003420;
    private const int OldSeasonBuff = 3009050;
    private const int GrantBuff = 3;

    private readonly FakeItemData _items = new();
    private readonly FakeResonanceData _resonance = new();
    private readonly FakeDeepSlumber _ownSeasonData = new();

    public GameTableNamesTests()
    {
        SkillAoyiTableBase.Rows.Clear();
        SeasonTalentTemplateTableBase.Rows.Clear();
        SeasonTalentTreeTableBase.Rows.Clear();
        SeasonTalentEffectOrdinaryTableBase.Rows.Clear();
        FunctionTableBase.Rows.Clear();
    }

    [Fact]
    public void Names_an_imagine_after_its_item_without_the_shared_label()
    {
        AddImagine(ArachnocrabSkill, ArachnocrabItem, "Battle Imagine - Phantom Arachnocrab");
        AddImagine(TinaSkill, TinaItem, "Battle Imagine - Tina");

        var names = Names();

        Assert.Equal("Phantom Arachnocrab", names.ImagineName(ArachnocrabSkill));
        Assert.Equal("Tina", names.ImagineName(TinaSkill));
    }

    [Fact]
    public void Keeps_the_whole_item_name_when_there_is_no_shared_label()
    {
        AddImagine(ArachnocrabSkill, ArachnocrabItem, "Arcane! Divine Reliance");
        AddImagine(TinaSkill, TinaItem, "Stunt! Kinetic Strike");

        Assert.Equal("Arcane! Divine Reliance", Names().ImagineName(ArachnocrabSkill));
    }

    [Fact]
    public void Falls_back_to_the_skill_name_when_the_imagine_table_has_no_row()
    {
        _resonance.SkillNames[ArachnocrabSkill] = "Venom Burst";

        Assert.Equal("Venom Burst", Names().ImagineName(ArachnocrabSkill));
    }

    [Fact]
    public void Names_the_board_behind_a_players_deep_slumber_buffs()
    {
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 });
        AddNode(1001, group: 10, FantasiaImpactBuff);
        AddNode(1003, group: 10, ReconstructBuff);

        Assert.Equal(new[] { "Fantasia Impact" }, Names().SeasonTalentBoardNames(new[] { FantasiaImpactBuff, ReconstructBuff }));
    }

    [Fact]
    public void A_shared_node_alone_does_not_name_a_board()
    {
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10, 90 });
        AddBoard(2, "Endless Mind", season: 3, nodeGroups: new[] { 11, 90 });
        AddNode(1009, group: 90, BlinkBreathBuff);

        Assert.Empty(Names().SeasonTalentBoardNames(new[] { BlinkBreathBuff }));
    }

    [Fact]
    public void Lists_the_board_with_the_most_matching_buffs_first()
    {
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 });
        AddBoard(2, "Endless Mind", season: 3, nodeGroups: new[] { 11 });
        AddNode(1001, group: 10, FantasiaImpactBuff);
        AddNode(1101, group: 11, EndlessMindBuff);
        AddNode(1103, group: 11, EndlessMindChildBuff);

        var names = Names().SeasonTalentBoardNames(new[] { FantasiaImpactBuff, EndlessMindBuff, EndlessMindChildBuff });

        Assert.Equal(new[] { "Endless Mind", "Fantasia Impact" }, names);
    }

    [Fact]
    public void Keeps_only_boards_of_the_current_season()
    {
        _ownSeasonData.SeasonLevels = new[] { new[] { 2, 100 }, new[] { 3, 90 } };
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 });
        AddBoard(2, "Old Board", season: 2, nodeGroups: new[] { 20 });
        AddNode(1001, group: 10, FantasiaImpactBuff);
        AddNode(2001, group: 20, OldSeasonBuff);

        Assert.Equal(new[] { "Fantasia Impact" }, Names().SeasonTalentBoardNames(new[] { FantasiaImpactBuff, OldSeasonBuff }));
    }

    [Fact]
    public void Names_nothing_before_the_first_refresh()
    {
        AddImagine(ArachnocrabSkill, ArachnocrabItem, "Battle Imagine - Phantom Arachnocrab");
        AddImagine(TinaSkill, TinaItem, "Battle Imagine - Tina");
        _resonance.SkillNames[ArachnocrabSkill] = "Venom Burst";

        Assert.Equal("Venom Burst", NewSource().ImagineName(ArachnocrabSkill));
    }

    [Fact]
    public void Titles_the_season_talent_system_with_the_games_feature_name()
    {
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 }, feature: 850001);
        FunctionTableBase.Rows[850001] = new FunctionTableBase { Id = 850001, Name = "Deep Slumber" };

        Assert.Equal("Deep Slumber", Names().SeasonTalentTitle);
    }

    [Fact]
    public void Has_no_title_when_the_game_names_no_feature()
    {
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 });

        Assert.Null(Names().SeasonTalentTitle);
    }

    [Fact]
    public void Reloads_the_boards_when_a_new_season_starts()
    {
        _ownSeasonData.SeasonLevels = new[] { new[] { 3, 90 } };
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 });
        AddBoard(2, "Next Season Board", season: 4, nodeGroups: new[] { 20 });
        AddNode(1001, group: 10, FantasiaImpactBuff);
        AddNode(2001, group: 20, OldSeasonBuff);
        var names = Names();
        var versionBefore = names.Version;

        _ownSeasonData.SeasonLevels = new[] { new[] { 3, 90 }, new[] { 4, 1 } };
        names.Refresh();

        Assert.NotEqual(versionBefore, names.Version);
        Assert.Equal(new[] { "Next Season Board" }, names.SeasonTalentBoardNames(new[] { FantasiaImpactBuff, OldSeasonBuff }));
    }

    [Fact]
    public void Keeps_the_version_while_nothing_changed()
    {
        AddBoard(1, "Fantasia Impact", season: 3, nodeGroups: new[] { 10 });
        var names = Names();
        var versionBefore = names.Version;

        names.Refresh();

        Assert.Equal(versionBefore, names.Version);
    }

    private static void AddBoard(int id, string name, int season, int[] nodeGroups, int feature = 0)
    {
        SeasonTalentTemplateTableBase.Rows[id] = new SeasonTalentTemplateTableBase
        {
            Id = id,
            TemplateName = name,
            BelongSeasonId = season,
            BelongFunction = feature,
        };
        foreach (var group in nodeGroups)
        {
            var nodeId = id * 1000 + group;
            SeasonTalentTreeTableBase.Rows[nodeId] = new SeasonTalentTreeTableBase { Id = nodeId, TemplateId = id, GroupId = group };
        }
    }

    private static void AddNode(int rowId, int group, int buffId) =>
        SeasonTalentEffectOrdinaryTableBase.Rows[rowId] = new SeasonTalentEffectOrdinaryTableBase
        {
            Id = rowId,
            GroupId = group,
            Effect = new Int32Table().Add(GrantBuff, buffId, 1),
        };

    // Names load through Refresh (the update tick in the plugin), so a fresh source is refreshed once here.
    private GameTableNames Names()
    {
        var names = NewSource();
        names.Refresh();
        return names;
    }

    private GameTableNames NewSource()
    {
        var log = new SilentLog();
        return new GameTableNames(new GameTableReader(log), _items, _resonance, _ownSeasonData, log);
    }

    private void AddImagine(int skillId, int itemId, string itemName)
    {
        SkillAoyiTableBase.Rows[skillId] = new SkillAoyiTableBase { Id = skillId, AoyiItemId = itemId };
        _items.Names[itemId] = itemName;
    }

    private sealed class FakeItemData : IGameDataInventory
    {
        public Dictionary<int, string> Names { get; } = new();

        public ItemInfo? GetItem(int id) =>
            Names.TryGetValue(id, out var name) ? new ItemInfo(id, name, string.Empty, string.Empty, 0, default, 0) : null;

        public EquipInfo? GetEquip(int id) => null;
        public WeaponInfo? GetWeapon(int id) => null;
    }

    private sealed class FakeResonanceData : IGameDataResonance
    {
        public Dictionary<int, string> SkillNames { get; } = new();

        public ImagineInfo? GetImagineForSkill(int skillId) =>
            SkillNames.TryGetValue(skillId, out var name) ? new ImagineInfo(skillId, name, string.Empty, 1, 0, 0) : null;
    }

    private sealed class FakeDeepSlumber : IDeepSlumber
    {
        public IReadOnlyList<int[]>? SeasonLevels { get; set; }

        public bool IsAvailable => SeasonLevels is not null;

        public DeepSlumberState? GetState() =>
            SeasonLevels is null ? null : new DeepSlumberState(SeasonLevels, System.Array.Empty<DeepSlumberLine>());

        public System.Threading.Tasks.Task<DeepSlumberApplyResult> ApplySetupAsync(
            DeepSlumberSetup target, System.Threading.CancellationToken ct = default) => throw new System.NotSupportedException();
    }

    private sealed class SilentLog : IPluginLog
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
        public void Debug(string message) { }
    }
}
