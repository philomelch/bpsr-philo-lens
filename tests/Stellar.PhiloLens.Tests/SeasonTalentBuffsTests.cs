using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class SeasonTalentBuffsTests
{
    private const int Talent = 6;
    private const int Equip = 10;
    private const int SeasonTalent = 13;

    [Fact]
    public void Keeps_only_season_talent_buffs()
    {
        var ids = SeasonTalentBuffs.From(new[]
        {
            new BuffSource(3002010, SeasonTalent),
            new BuffSource(2404150, Equip),
            new BuffSource(2202020, Talent),
        });

        Assert.Equal(new[] { 3002010 }, ids);
    }

    [Fact]
    public void Sorts_and_removes_duplicates_so_the_order_is_stable()
    {
        var ids = SeasonTalentBuffs.From(new[]
        {
            new BuffSource(3059050, SeasonTalent),
            new BuffSource(3002010, SeasonTalent),
            new BuffSource(3059050, SeasonTalent),
        });

        Assert.Equal(new[] { 3002010, 3059050 }, ids);
    }

    [Fact]
    public void Same_ids_compare_equal_regardless_of_instance()
    {
        var first = SeasonTalentBuffs.From(new[] { new BuffSource(3002010, SeasonTalent) });
        var second = SeasonTalentBuffs.From(new[] { new BuffSource(3002010, SeasonTalent), new BuffSource(1, Equip) });

        Assert.True(SeasonTalentBuffs.SameIds(first, second));
        Assert.False(SeasonTalentBuffs.SameIds(first, System.Array.Empty<int>()));
    }
}
