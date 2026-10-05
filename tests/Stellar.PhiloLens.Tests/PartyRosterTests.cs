using System.Linq;
using Stellar.PhiloLens.Domain;
using Xunit;

namespace Stellar.PhiloLens.Tests;

public sealed class PartyRosterTests
{
    [Theory]
    [InlineData(1, (int)PartyRole.Dps)]
    [InlineData(2, (int)PartyRole.Healer)]
    [InlineData(3, (int)PartyRole.Tank)]
    [InlineData(0, (int)PartyRole.Unknown)]
    [InlineData(4, (int)PartyRole.Unknown)]
    public void Role_ids_map_to_roles(int roleId, int expected) =>
        Assert.Equal((PartyRole)expected, PartyRoles.FromRoleId(roleId));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(5, 5)]
    [InlineData(6, 20)]
    [InlineData(20, 20)]
    public void A_party_bigger_than_five_is_a_raid(int memberCount, int expectedCapacity) =>
        Assert.Equal(expectedCapacity, RosterOf(Enumerable.Repeat(PartyRole.Dps, memberCount).ToArray()).Capacity);

    // pt4: a raid of four reads as a raid once anyone sits beyond group 1.
    [Fact]
    public void A_small_party_with_someone_beyond_group_one_is_a_raid()
    {
        var roster = new PartyRoster(new[]
        {
            new PartyMember(100001, "Aria", 1, PartyRole.Dps, 0, 0),
            new PartyMember(100002, "Bram", 1, PartyRole.Dps, 0, 0, Group: 2),
        });

        Assert.True(roster.IsRaid);
        Assert.Equal(PartyRoster.RaidCapacity, roster.Capacity);
    }

    [Fact]
    public void Roles_are_counted()
    {
        var roster = RosterOf(PartyRole.Tank, PartyRole.Healer, PartyRole.Dps, PartyRole.Dps, PartyRole.Unknown);

        Assert.Equal(1, roster.CountOf(PartyRole.Tank));
        Assert.Equal(1, roster.CountOf(PartyRole.Healer));
        Assert.Equal(2, roster.CountOf(PartyRole.Dps));
    }

    [Fact]
    public void Player_entity_ids_round_trip_through_the_character_id()
    {
        const long charId = 100001;

        var entityId = PlayerEntityIds.FromCharId(charId);

        Assert.Equal((charId << 16) | 640, entityId);
        Assert.Equal(charId, PlayerEntityIds.CharIdOf(entityId));
    }

    private static PartyRoster RosterOf(params PartyRole[] roles) =>
        new(roles.Select((role, i) => new PartyMember(100001 + i, "Member" + i, 1, role, 0, 0)).ToArray());
}
