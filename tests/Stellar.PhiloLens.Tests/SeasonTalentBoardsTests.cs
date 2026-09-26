using System.Collections.Generic;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class SeasonTalentBoardsTests
{
    private const int FantasiaImpact = 1;
    private const int EndlessMind = 2;

    private static readonly Dictionary<int, IReadOnlyList<int>> BoardsByBuff = new()
    {
        [100] = new[] { FantasiaImpact },
        [101] = new[] { FantasiaImpact },
        [200] = new[] { EndlessMind },
        [900] = new[] { FantasiaImpact, EndlessMind },
    };

    [Fact]
    public void Picks_the_board_whose_nodes_the_player_has()
    {
        Assert.Equal(new[] { FantasiaImpact }, SeasonTalentBoards.Active(new[] { 100, 101 }, BoardsByBuff));
    }

    [Fact]
    public void Ignores_shared_nodes_and_unknown_buffs()
    {
        Assert.Empty(SeasonTalentBoards.Active(new[] { 900, 555 }, BoardsByBuff));
    }

    [Fact]
    public void Orders_boards_by_how_many_buffs_match()
    {
        Assert.Equal(new[] { FantasiaImpact, EndlessMind }, SeasonTalentBoards.Active(new[] { 200, 100, 101 }, BoardsByBuff));
    }
}
