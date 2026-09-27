using Bokura;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Adapters.GameTables;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

// The fake buff table is static, so these tests must not run in parallel with other table tests.
[Collection(nameof(GameTableNamesTests))]
public sealed class GameTableConsumablesTests
{
    private long _nowMs = 1_000_000;

    public GameTableConsumablesTests()
    {
        BuffTableBase.Rows.Clear();
        BuffTableBase.Unavailable = false;
    }

    // The expected kind is passed as its int value: ConsumableKind is internal, test methods are public.
    [Theory]
    [InlineData(101, (int)ConsumableKind.Food)]
    [InlineData(102, (int)ConsumableKind.Potion)]
    [InlineData(104, (int)ConsumableKind.FoodBonus)]
    public void Classifies_buffs_by_the_games_ability_type(int abilityType, int expectedKind)
    {
        BuffTableBase.Rows[7] = new BuffTableBase { Id = 7, BuffAbilityType = abilityType };

        Assert.Equal((ConsumableKind)expectedKind, Catalog().KindOf(7));
    }

    [Fact]
    public void Other_and_unknown_buffs_are_not_consumables()
    {
        BuffTableBase.Rows[7] = new BuffTableBase { Id = 7, BuffAbilityType = 103 };

        var catalog = Catalog();

        Assert.Null(catalog.KindOf(7));
        Assert.Null(catalog.KindOf(999));
    }

    [Fact]
    public void Looks_each_buff_up_once()
    {
        BuffTableBase.Rows[7] = new BuffTableBase { Id = 7, BuffAbilityType = 101 };
        var catalog = Catalog();
        catalog.KindOf(7);

        BuffTableBase.Rows[7] = new BuffTableBase { Id = 7, BuffAbilityType = 102 };

        Assert.Equal(ConsumableKind.Food, catalog.KindOf(7));
    }

    [Fact]
    public void An_unreadable_table_is_not_remembered_as_no_consumables()
    {
        BuffTableBase.Rows[7] = new BuffTableBase { Id = 7, BuffAbilityType = 101 };
        BuffTableBase.Unavailable = true;
        var catalog = Catalog();

        Assert.False(catalog.IsReady);
        Assert.Null(catalog.KindOf(7));

        BuffTableBase.Unavailable = false;
        _nowMs += 5_000;

        Assert.True(catalog.IsReady);
        Assert.Equal(ConsumableKind.Food, catalog.KindOf(7));
    }

    [Fact]
    public void An_unreadable_table_is_requested_again_only_after_a_pause()
    {
        BuffTableBase.Unavailable = true;
        var catalog = Catalog();
        Assert.False(catalog.IsReady);

        BuffTableBase.Unavailable = false;
        _nowMs += 1_000;

        Assert.False(catalog.IsReady);
    }

    private GameTableConsumables Catalog() => new(new GameTableReader(new SilentLog()), () => _nowMs);

    private sealed class SilentLog : IPluginLog
    {
        public void Info(string message) { }
        public void Warning(string message) { }
        public void Error(string message) { }
        public void Debug(string message) { }
    }
}
