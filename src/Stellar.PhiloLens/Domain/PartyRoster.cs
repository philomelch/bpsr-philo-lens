using System.Collections.Generic;

namespace Stellar.PhiloLens.Domain;

/// <summary>The role a party member fills, as the party roster reports it.</summary>
internal enum PartyRole
{
    Unknown,
    Dps,
    Healer,
    Tank,
}

/// <summary>Maps the roster's role id to a <see cref="PartyRole"/>.</summary>
internal static class PartyRoles
{
    // Observed in captures: every Beat Performer and Verdant Oracle carried 2, every Shield Knight and
    // Heavy Guardian 3, every other class 1.
    private const int DpsId = 1;
    private const int HealerId = 2;
    private const int TankId = 3;

    public static PartyRole FromRoleId(int roleId) => roleId switch
    {
        DpsId => PartyRole.Dps,
        HealerId => PartyRole.Healer,
        TankId => PartyRole.Tank,
        _ => PartyRole.Unknown,
    };
}

/// <summary>One member of an inspected player's party, as the party roster reports it.</summary>
/// <param name="CharId">The member's character id.</param>
/// <param name="Name">The member's character name.</param>
/// <param name="ClassId">The member's class; <c>0</c> when the roster didn't say.</param>
/// <param name="Role">The role the member fills.</param>
/// <param name="AbilityScore">The member's Ability Score; <c>0</c> when unknown.</param>
/// <param name="SeasonStrength">The member's season strength stat; <c>0</c> when unknown.</param>
/// <param name="Group">The member's raid group (1 to 4) as the roster's copy last saw it; always 1 in a regular
/// party. It can be stale (the leader may have moved them since), so it is only used to tell that a party is a raid.</param>
internal sealed record PartyMember(long CharId, string Name, int ClassId, PartyRole Role, long AbilityScore,
    long SeasonStrength, int Group = PartyRoster.FirstGroup);

/// <summary>An inspected player's whole party, ordered by raid group and then the party's own order.</summary>
/// <param name="Members">The members.</param>
/// <param name="RaidFlag">The roster's own "this is a raid" flag.</param>
internal sealed record PartyRoster(IReadOnlyList<PartyMember> Members, bool RaidFlag = false)
{
    /// <summary>The most members a regular party holds, and the most in one raid group.</summary>
    public const int RegularCapacity = 5;

    public const int RaidCapacity = 20;

    public const int FirstGroup = 1;

    /// <summary>True when the party is a raid: the roster says so, or, should that flag ever go missing, there
    /// are more members than a regular party holds or anyone sits beyond the first group.</summary>
    public bool IsRaid
    {
        get
        {
            if (RaidFlag || Members.Count > RegularCapacity) return true;
            for (var i = 0; i < Members.Count; i++)
            {
                if (Members[i].Group > FirstGroup) return true;
            }

            return false;
        }
    }

    /// <summary>How many members the party can hold.</summary>
    public int Capacity => IsRaid ? RaidCapacity : RegularCapacity;

    /// <summary>This roster with fresher stats for one member. The party list is the team's own copy of each
    /// member's profile and can lag behind it; a value of <c>0</c> (not known) keeps the roster's.</summary>
    public PartyRoster WithStatsFor(long charId, long abilityScore, long seasonStrength)
    {
        var members = new PartyMember[Members.Count];
        for (var i = 0; i < Members.Count; i++)
        {
            var member = Members[i];
            members[i] = member.CharId != charId ? member : member with
            {
                AbilityScore = abilityScore > 0 ? abilityScore : member.AbilityScore,
                SeasonStrength = seasonStrength > 0 ? seasonStrength : member.SeasonStrength,
            };
        }

        return this with { Members = members };
    }

    public int CountOf(PartyRole role)
    {
        var count = 0;
        for (var i = 0; i < Members.Count; i++)
        {
            if (Members[i].Role == role) count++;
        }

        return count;
    }
}
