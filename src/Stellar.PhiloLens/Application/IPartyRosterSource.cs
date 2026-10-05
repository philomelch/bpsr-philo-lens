using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Application;

/// <summary>What a party roster read found.</summary>
internal enum PartyRosterStatus
{
    /// <summary>The profile card's data isn't there yet (still loading, or the card isn't open); try again.</summary>
    Pending,

    /// <summary>The player isn't in a party.</summary>
    Solo,

    /// <summary>The player is in a party; the roster is attached.</summary>
    InParty,

    /// <summary>The data was there but couldn't be understood; trying again won't help.</summary>
    Unreadable,
}

/// <summary>The inspected player's own stats from the profile data the game loaded for their card. Unlike the
/// live stats, these are there whatever the distance. <c>0</c> means the card didn't carry the value.</summary>
internal readonly record struct ProfileStats(long AbilityScore, long SeasonStrength);

/// <summary>The outcome of one roster read. <see cref="Roster"/> is set only for
/// <see cref="PartyRosterStatus.InParty"/>; <see cref="Profile"/> is filled whenever the card's data was found
/// (<see cref="PartyRosterStatus.Solo"/> or <see cref="PartyRosterStatus.InParty"/>).</summary>
internal readonly record struct PartyRosterReading(PartyRosterStatus Status, PartyRoster? Roster, ProfileStats Profile = default)
{
    public static PartyRosterReading Pending => new(PartyRosterStatus.Pending, null);
    public static PartyRosterReading Solo => new(PartyRosterStatus.Solo, null);
    public static PartyRosterReading Unreadable => new(PartyRosterStatus.Unreadable, null);
}

/// <summary>Port: reads the party, and the player's own stats, of the player whose profile card is open, from
/// the data the game loaded for that card. Implemented in Adapters, and by fakes in tests.</summary>
internal interface IPartyRosterSource
{
    /// <param name="charId">The character id of the player whose card is open.</param>
    PartyRosterReading Read(long charId);
}
