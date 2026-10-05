using System;
using System.Collections.Generic;
using System.Globalization;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters.Party;

/// <summary>A parsed <see cref="PartyRosterChunk"/> result: the reading, plus the field names the chunk
/// reported (the <c>T</c>, <c>A</c> and error lines) for the diagnostics log.</summary>
internal readonly record struct ParsedPartyRoster(PartyRosterReading Reading, IReadOnlyList<string> Notes);

/// <summary>Parses the text <see cref="PartyRosterChunk"/> leaves behind. Never throws: anything it doesn't
/// recognise reads as <see cref="PartyRosterStatus.Pending"/> (no answer yet) or
/// <see cref="PartyRosterStatus.Unreadable"/> (an answer it can't use).</summary>
internal static class PartyRosterParser
{
    private const char FieldSeparator = '\t';
    private const char LineSeparator = '\n';

    // M, charId, roleId, classId, abilityScore, seasonStrength, enterTime, groupId, name
    private const int MemberFieldCount = 9;

    private static readonly ParsedPartyRoster NoAnswer = new(PartyRosterReading.Pending, Array.Empty<string>());

    /// <param name="output">The chunk's result global; null when it was never set.</param>
    /// <param name="charId">The player the chunk was asked about; an answer about anyone else is ignored.</param>
    public static ParsedPartyRoster Parse(string? output, long charId)
    {
        if (string.IsNullOrEmpty(output)) return NoAnswer;

        var lines = output.Split(LineSeparator);
        var header = lines[0].Split(FieldSeparator);
        if (header.Length < 3 || header[0] != PartyRosterChunk.Header
            || header[1] != charId.ToString(CultureInfo.InvariantCulture))
        {
            return NoAnswer;
        }

        var notes = new List<string>();
        if (header.Length > 3) notes.Add("error: " + header[3]);
        var reading = header[2] switch
        {
            "nocard" or "pending" => PartyRosterReading.Pending,
            "solo" => PartyRosterReading.Solo,
            "party" => ReadParty(lines, notes),
            _ => PartyRosterReading.Unreadable,
        };
        AddFieldNotes(lines, notes);
        return new ParsedPartyRoster(reading with { Profile = ReadProfile(lines) }, notes);
    }

    // P, abilityScore, seasonStrength
    private static ProfileStats ReadProfile(string[] lines)
    {
        const int ProfileFieldCount = 3;
        foreach (var line in lines)
        {
            var fields = line.Split(FieldSeparator);
            if (fields[0] == "P" && fields.Length >= ProfileFieldCount) return new ProfileStats(ToLong(fields[1]), ToLong(fields[2]));
        }

        return default;
    }

    private static PartyRosterReading ReadParty(string[] lines, List<string> notes)
    {
        var members = new List<(PartyMember Member, long EnterTime)>();
        string[]? order = null;
        var raidFlag = false;
        foreach (var line in lines)
        {
            var fields = line.Split(FieldSeparator);
            if (fields[0] == "M" && fields.Length >= MemberFieldCount) members.Add(ReadMember(fields));
            else if (fields[0] == "O") order = fields;
            else if (fields[0] == "T") raidFlag = IsRaidTeam(fields);
        }

        if (members.Count == 0)
        {
            notes.Add("party without readable members");
            return PartyRosterReading.Unreadable;
        }

        return new PartyRosterReading(PartyRosterStatus.InParty,
            new PartyRoster(InPartyOrder(members, order), RaidFlag: raidFlag));
    }

    // teamData.teamMemberType: 0 for a regular party, 1 for a raid (the same value the framework's own team
    // reader maps to a 20-member raid).
    private static bool IsRaidTeam(string[] teamFields)
    {
        const string TeamTypePrefix = "teamMemberType=";
        const long RaidTeamType = 1;
        foreach (var pair in teamFields)
        {
            if (pair.StartsWith(TeamTypePrefix, StringComparison.Ordinal)) return ToLong(pair[TeamTypePrefix.Length..]) == RaidTeamType;
        }

        return false;
    }

    private static (PartyMember Member, long EnterTime) ReadMember(string[] fields)
    {
        var member = new PartyMember(
            CharId: ToLong(fields[1]),
            Name: fields[8],
            ClassId: (int)ToLong(fields[3]),
            Role: PartyRoles.FromRoleId((int)ToLong(fields[2])),
            AbilityScore: ToLong(fields[4]),
            SeasonStrength: ToLong(fields[5]),
            Group: (int)ToLong(fields[7]));
        return (member, ToLong(fields[6]));
    }

    // The party's own order when the roster carried it; members it doesn't list follow by when they joined. Not by
    // raid group: the roster's copy of each member's group goes stale when the leader moves people.
    private static PartyMember[] InPartyOrder(List<(PartyMember Member, long EnterTime)> members, string[]? order)
    {
        var rank = new Dictionary<long, int>();
        for (var i = 1; order is not null && i < order.Length; i++) rank.TryAdd(ToLong(order[i]), i);

        members.Sort((a, b) =>
        {
            var byRank = RankOf(rank, a.Member).CompareTo(RankOf(rank, b.Member));
            return byRank != 0 ? byRank : a.EnterTime.CompareTo(b.EnterTime);
        });

        var ordered = new PartyMember[members.Count];
        for (var i = 0; i < members.Count; i++) ordered[i] = members[i].Member;
        return ordered;
    }

    private static int RankOf(Dictionary<long, int> rank, PartyMember member) =>
        rank.TryGetValue(member.CharId, out var position) ? position : int.MaxValue;

    private static void AddFieldNotes(string[] lines, List<string> notes)
    {
        foreach (var line in lines)
        {
            if (line.StartsWith("T\t", StringComparison.Ordinal)) notes.Add("team fields: " + MaskIds(line[2..]));
            else if (line.StartsWith("A\t", StringComparison.Ordinal)) notes.Add("stat fields: " + line[2..].Replace(FieldSeparator, ' '));
            else if (line.StartsWith("V\t", StringComparison.Ordinal)) notes.Add("card view fields: " + line[2..].Replace(FieldSeparator, ' '));
        }
    }

    // Team fields can hold character and team ids, and text such as a name; the log only needs the field
    // names and small values, so ids and any text are masked.
    private static string MaskIds(string pairs)
    {
        var masked = new List<string>();
        foreach (var pair in pairs.Split(FieldSeparator))
        {
            var equals = pair.IndexOf('=', StringComparison.Ordinal);
            masked.Add(equals < 0 ? pair : pair[..(equals + 1)] + MaskValue(pair[(equals + 1)..]));
        }

        return string.Join(' ', masked);
    }

    private static string MaskValue(string value)
    {
        const long LargestPlainValue = 1_000_000;
        if (value is "true" or "false") return value;
        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)) return "<text>";
        return Math.Abs(number) > LargestPlainValue ? "<id>" : value;
    }

    private static long ToLong(string text) =>
        long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
