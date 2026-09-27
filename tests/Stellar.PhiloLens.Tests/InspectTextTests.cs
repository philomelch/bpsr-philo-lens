using System;
using System.Collections.Generic;
using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Domain.GameData;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;
using Stellar.PhiloLens.UI;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class InspectTextTests
{
    private const int FoodiesGrace = 2010071;

    private readonly FakeCombatData _combatData = new();
    private long _clockMs = 1_000_000;

    [Fact]
    public void A_consumable_whose_name_was_not_loaded_yet_appears_after_the_retry()
    {
        var text = NewText();
        var player = PlayerWith(new ActiveConsumable(FoodiesGrace, ConsumableKind.FoodBonus, StartMs: 0, DurationMs: 0));
        text.Refresh(player);
        Assert.Equal(string.Empty, text.Consumables);

        _combatData.Buffs[FoodiesGrace] = new BuffInfo(FoodiesGrace, "Foodie's Grace", "Luck +2%", string.Empty, BuffCategory.Unknown, false);
        _clockMs += 1_000;
        text.Refresh(player);

        Assert.Equal("Foodie's Grace: Luck +2%", text.Consumables);
    }

    [Fact]
    public void A_complete_consumable_list_is_not_rebuilt_without_a_change()
    {
        _combatData.Buffs[FoodiesGrace] = new BuffInfo(FoodiesGrace, "Foodie's Grace", "Luck +2%", string.Empty, BuffCategory.Unknown, false);
        var text = NewText();
        var player = PlayerWith(new ActiveConsumable(FoodiesGrace, ConsumableKind.FoodBonus, 0, 0));
        text.Refresh(player);

        _combatData.Buffs[FoodiesGrace] = new BuffInfo(FoodiesGrace, "Renamed", "Luck +2%", string.Empty, BuffCategory.Unknown, false);
        _clockMs += 1_000;
        text.Refresh(player);

        Assert.Equal("Foodie's Grace: Luck +2%", text.Consumables);
    }

    private InspectText NewText() =>
        new(new FakeLocalization(), _combatData, new NoBuildNames(), new FakeCombatSnapshot(), () => _clockMs);

    private static InspectedPlayer PlayerWith(params ActiveConsumable[] consumables) =>
        new(1, new ClassSpec(5, 50001, SpecSource.Talents), PlayerBuild.Empty with { Consumables = consumables });

    private sealed class FakeLocalization : ILocalization
    {
        public string Language => "en";

        public event Action? LanguageChanged { add { } remove { } }

        public string T(string key) => key;

        public string TFormat(string key, params object[] args) => key switch
        {
            "consumable.line" => $"{args[0]}: {args[1]}",
            _ => string.Join(" · ", args),
        };
    }

    private sealed class FakeCombatData : IGameDataCombat
    {
        public Dictionary<int, BuffInfo> Buffs { get; } = new();

        public BuffInfo? GetBuff(int id) => Buffs.TryGetValue(id, out var buff) ? buff : null;
        public SkillInfo? GetSkill(int id) => null;
        public IReadOnlyCollection<BuffInfo> AllBuffs() => Buffs.Values;
        public ProfessionInfo? GetProfession(int id) => new ProfessionInfo(id, "Verdant Oracle", string.Empty, true, Array.Empty<int>());
        public TalentInfo? GetTalent(int id) => null;
        public AttributeInfo? GetAttribute(int id) => null;
        public AttributeProfileInfo? GetAttributeProfile(int id) => null;
        public DamageAttrInfo? GetDamageAttr(int id) => null;
    }

    private sealed class NoBuildNames : IBuildNameSource
    {
        public int Version => 0;
        public string? SeasonTalentTitle => null;
        public void Refresh() { }
        public string? ImagineName(int skillId) => null;
        public IReadOnlyList<string> SeasonTalentBoardNames(IReadOnlyList<int> buffIds) => Array.Empty<string>();
    }

    // Server time not synced yet (0): no countdowns, the case where only the retry can redraw the list.
    private sealed class FakeCombatSnapshot : ICombatSnapshot
    {
        public bool IsAvailable => false;
        public EntityId LocalEntityId => EntityId.None;
        public IReadOnlyList<SkillCooldown> LocalCooldowns => Array.Empty<SkillCooldown>();
        public IReadOnlyList<ActiveBuff> LocalBuffs => Array.Empty<ActiveBuff>();
        public long ServerNowMs => 0;
        public DateTimeOffset ServerNow => DateTimeOffset.UnixEpoch;
        public IReadOnlyList<CombatEvent> RecentEvents => Array.Empty<CombatEvent>();
    }
}
