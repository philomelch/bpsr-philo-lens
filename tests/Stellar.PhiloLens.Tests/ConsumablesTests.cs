using System.Collections.Generic;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class ConsumablesTests
{
    private const int AtkFood = 2032018;
    private const int FirePotion = 2033014;
    private const int FoodiesGrace = 2010071;
    private const int ChainJump = 682501;

    private static readonly Dictionary<int, ConsumableKind> Kinds = new()
    {
        [AtkFood] = ConsumableKind.Food,
        [FirePotion] = ConsumableKind.Potion,
        [FoodiesGrace] = ConsumableKind.FoodBonus,
    };

    private static ConsumableKind? KindOf(int buffId) => Kinds.TryGetValue(buffId, out var kind) ? kind : null;

    [Fact]
    public void Keeps_only_consumables_food_first_then_potions_then_bonuses()
    {
        var consumables = ConsumableBuffs.From(new[]
        {
            new TimedBuff(FoodiesGrace, 0, 0),
            new TimedBuff(ChainJump, 0, -1),
            new TimedBuff(FirePotion, 1_000, 1_800_000),
            new TimedBuff(AtkFood, 2_000, 3_600_000),
        }, KindOf);

        Assert.Equal(new[] { AtkFood, FirePotion, FoodiesGrace }, new[] { consumables[0].BuffId, consumables[1].BuffId, consumables[2].BuffId });
        Assert.Equal(3, consumables.Count);
    }

    [Fact]
    public void Has_nothing_when_no_buff_is_a_consumable()
    {
        Assert.Empty(ConsumableBuffs.From(new[] { new TimedBuff(ChainJump, 0, -1) }, KindOf));
    }

    [Fact]
    public void Time_left_counts_down_from_start_plus_duration_and_stops_at_zero()
    {
        var food = new ActiveConsumable(AtkFood, ConsumableKind.Food, StartMs: 10_000, DurationMs: 60_000);

        Assert.Equal(45_000, food.RemainingMs(25_000));
        Assert.Equal(0, food.RemainingMs(90_000));
    }

    [Fact]
    public void A_buff_without_duration_has_no_timer()
    {
        Assert.False(new ActiveConsumable(FoodiesGrace, ConsumableKind.FoodBonus, 0, 0).HasTimer);
        Assert.False(new ActiveConsumable(FoodiesGrace, ConsumableKind.FoodBonus, 0, -1).HasTimer);
    }

    [Fact]
    public void Same_compares_contents()
    {
        var first = ConsumableBuffs.From(new[] { new TimedBuff(AtkFood, 1, 2) }, KindOf);
        var second = ConsumableBuffs.From(new[] { new TimedBuff(AtkFood, 1, 2), new TimedBuff(ChainJump, 0, -1) }, KindOf);
        var refreshed = ConsumableBuffs.From(new[] { new TimedBuff(AtkFood, 5, 2) }, KindOf);

        Assert.True(ConsumableBuffs.Same(first, second));
        Assert.False(ConsumableBuffs.Same(first, refreshed));
    }

    [Theory]
    [InlineData(0, "0:00")]
    [InlineData(1, "0:01")]
    [InlineData(59_000, "0:59")]
    [InlineData(245_000, "4:05")]
    [InlineData(3_600_000, "1:00:00")]
    [InlineData(3_723_400, "1:02:04")]
    [InlineData(-5_000, "0:00")]
    public void Formats_time_left_as_a_countdown(long remainingMs, string expected)
    {
        Assert.Equal(expected, Countdown.Format(remainingMs));
    }
}
