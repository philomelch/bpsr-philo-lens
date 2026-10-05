using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.UI;

/// <summary>The party section's text, built from an <see cref="InspectedParty"/>. Like <see cref="InspectText"/>,
/// <see cref="Refresh"/> runs from the update tick and rebuilds only when the party or the language changed, or,
/// while a class name is still missing, at most once per <see cref="RetryIntervalMs"/>. The element callbacks
/// only read the cached strings.
/// <para>The members are listed in the party's own order, a row each. A raid isn't split into its groups: the
/// card's data only carries a copy of each member's group that goes stale when the leader moves people, and the
/// game shows several groups of more than five that way.</para></summary>
internal sealed class PartyText
{
    /// <summary>Rows the section can show: a full raid.</summary>
    public const int MaxRows = PartyRoster.RaidCapacity;

    private const long RetryIntervalMs = 1_000;

    // A non-breaking space: an empty cell that still takes its line in a column text.
    private const string BlankCell = " ";

    // Unity right-aligns a line by its last character's drawn shape, so a number ending in a thin "1" sat further
    // right than the others. Ending every right-aligned line, header included, with the same character lines the
    // digits up again. (A separately drawn bold header dropped this space, which is why headers live in the text.)
    private const char RightAlignPad = ' ';

    private readonly ILocalization _localization;
    private readonly IGameDataCombat _combatData;
    private readonly Func<long> _clockMs;

    private readonly string[] _names = new string[MaxRows];
    private readonly string[] _classSpecs = new string[MaxRows];
    private readonly string[] _abilityScores = new string[MaxRows];
    private readonly string[] _seasonStrengths = new string[MaxRows];

    private InspectedParty? _rendered;
    private string? _renderedLanguage;
    private bool _complete = true;
    private long _lastRenderMs;

    /// <param name="clockMs">Monotonic milliseconds for the retry pacing; the system tick count by default
    /// (tests pass their own).</param>
    public PartyText(ILocalization localization, IGameDataCombat combatData, Func<long>? clockMs = null)
    {
        _localization = localization;
        _combatData = combatData;
        _clockMs = clockMs ?? (() => Environment.TickCount64);
        Array.Fill(_names, string.Empty);
        Array.Fill(_classSpecs, string.Empty);
        Array.Fill(_abilityScores, string.Empty);
        Array.Fill(_seasonStrengths, string.Empty);
    }

    /// <summary>True while there's a party to show.</summary>
    public bool HasParty => RowCount > 0;

    /// <summary>How many member rows are filled in.</summary>
    public int RowCount { get; private set; }

    /// <summary>"Party (3/5)".</summary>
    public string Title { get; private set; } = string.Empty;

    /// <summary>"Tank: 1    Healers: 1    DPS: 2".</summary>
    public string Roles { get; private set; } = string.Empty;

    /// <summary>The season strength column's header: the initials of the game's name for the stat this season
    /// (e.g. "IBS"), or the neutral "Season Strength" when the game data has no name.</summary>
    public string SeasonStrengthLabel { get; private set; } = string.Empty;

    /// <summary>True when the AS header is short enough for its column to be right-aligned under it.</summary>
    public bool AbilityScoreLabelFits { get; private set; }

    /// <summary>True when <see cref="SeasonStrengthLabel"/> is short enough for its column to be right-aligned
    /// under it; otherwise the whole column is left-aligned and fits the header.</summary>
    public bool SeasonStrengthLabelFits { get; private set; }

    /// <summary>The name column as one rich text: the header in bold, then a line per member.</summary>
    public string NamesColumn { get; private set; } = string.Empty;

    /// <summary>The class column as one rich text: the header in bold, then a line per member.</summary>
    public string ClassesColumn { get; private set; } = string.Empty;

    /// <summary>The Ability Score column as one rich text: the header in bold, then a line per member.</summary>
    public string AbilityScoresColumn { get; private set; } = string.Empty;

    /// <summary>The season strength column as one rich text: the header in bold, then a line per member.</summary>
    public string SeasonStrengthsColumn { get; private set; } = string.Empty;

    public string Name(int row) => _names[row];
    public string ClassSpec(int row) => _classSpecs[row];
    public string AbilityScore(int row) => _abilityScores[row];

    /// <summary>Empty when the roster didn't carry the member's season strength.</summary>
    public string SeasonStrength(int row) => _seasonStrengths[row];

    /// <summary>Rebuilds the text if the party changed. Reads game data (class names), so only call while game
    /// reads are safe (<c>IsWorldActive</c>).</summary>
    public void Refresh(InspectedParty? party)
    {
        var sameInputs = ReferenceEquals(party, _rendered) && ReferenceEquals(_localization.Language, _renderedLanguage);
        if (sameInputs && (_complete || _clockMs() - _lastRenderMs < RetryIntervalMs)) return;

        _rendered = party;
        _renderedLanguage = _localization.Language;
        _lastRenderMs = _clockMs();
        _complete = true;
        if (party is null)
        {
            RowCount = 0;
            return;
        }

        Rebuild(party);
    }

    private void Rebuild(InspectedParty party)
    {
        var roster = party.Roster;
        Title = _localization.TFormat("party.title", Count(roster.Members.Count), Count(roster.Capacity));
        Roles = _localization.TFormat("party.roles", Count(roster.CountOf(PartyRole.Tank)),
            Count(roster.CountOf(PartyRole.Healer)), Count(roster.CountOf(PartyRole.Dps)));
        var abilityScoreLabel = _localization.T("party.column.ability_score");
        AbilityScoreLabelFits = PartyColumns.FitsOverAbilityScores(abilityScoreLabel);
        SeasonStrengthLabel = StatLabels.ShortOf(_localization, _combatData, StatLabels.SeasonStrengthAttributeId,
            "stats.season_strength");
        SeasonStrengthLabelFits = PartyColumns.FitsOverSeasonStrengths(SeasonStrengthLabel);
        FillRows(party.Members);

        NamesColumn = JoinColumn(_localization.T("party.column.name"), _names, padded: false);
        ClassesColumn = JoinColumn(_localization.T("party.column.class"), _classSpecs, padded: false);
        AbilityScoresColumn = JoinColumn(abilityScoreLabel, _abilityScores, padded: true);
        SeasonStrengthsColumn = JoinColumn(SeasonStrengthLabel, _seasonStrengths, padded: true);
    }

    private void FillRows(IReadOnlyList<InspectedPartyMember> members)
    {
        RowCount = Math.Min(members.Count, MaxRows);
        for (var row = 0; row < RowCount; row++)
        {
            var member = members[row];
            _names[row] = AsPlainText(member.Member.Name);
            _classSpecs[row] = ClassSpecText.Format(_localization, _combatData, member.ClassSpec, out var complete);
            if (!complete) _complete = false;
            _abilityScores[row] = Score(member.Member.AbilityScore);
            _seasonStrengths[row] = member.Member.SeasonStrength > 0 ? Score(member.Member.SeasonStrength) : string.Empty;
        }
    }

    // One text per column: the header in bold, then a line per member. Lines inside one text all use the font's line
    // spacing, and every column has the same bold first line, so the columns line up exactly whatever each line
    // says, and a header is drawn exactly like the values under it. An empty cell holds a non-breaking space, so
    // each column has a line for every member (a trailing empty line could be dropped).
    private string JoinColumn(string header, string[] cells, bool padded)
    {
        var column = new StringBuilder("<b>").Append(AsPlainText(header)).Append("</b>");
        if (padded) column.Append(RightAlignPad);
        for (var row = 0; row < RowCount; row++)
        {
            column.Append('\n').Append(OrBlank(cells[row]));
            if (padded) column.Append(RightAlignPad);
        }

        return column.ToString();
    }

    private static string OrBlank(string cell) => cell.Length > 0 ? cell : BlankCell;

    // Names come from other players and the columns are rich text, so a "<" could start a tag that restyles every
    // line after it in the column; a look-alike angle bracket can't. Headers go through it too, for the season
    // name the game data supplies.
    private static string AsPlainText(string name) => name.Replace('<', '‹');

    private string Score(long value) =>
        value > 0 ? value.ToString("N0", CultureInfo.InvariantCulture) : _localization.T("stats.none");

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
