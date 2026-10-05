using System;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;

namespace Stellar.PhiloLens.UI;

/// <summary>The Lens window's party section: "Party (3/5)", the role counts, then the members as one table in
/// the party's own order.
/// <para>The table is four columns side by side, each one multi-line text holding its bold header and a line per
/// member (see <see cref="PartyText.NamesColumn"/>). Each column is exactly as wide as its widest line; the rows
/// line up because every line in every column uses the same font line spacing; and a header lines up with its
/// values because it is drawn as part of the same text.</para></summary>
internal sealed class PartyPanel
{
    // The number columns' widths live in PartyColumns; the text columns fit their widest line.
    private const float FitToText = 0f;
    private const float CellGap = 12f;

    private readonly PartyText _text;

    public PartyPanel(PartyText text) => _text = text;

    public bool HasParty => _text.HasParty;

    /// <summary>Rebuilds the section's text if the party changed; see <see cref="PartyText.Refresh"/>.</summary>
    public void Refresh(InspectedParty? party) => _text.Refresh(party);

    public HudElement Build() => new ColumnElement(new HudElement[]
    {
        // NoWrap like every other text here: the window fits its width to its content, which is only safe while
        // no text wraps (a long translated role line would otherwise wrap and clip).
        new TextElement(() => _text.Title, Emphasis: true, NoWrap: true),
        new TextElement(() => _text.Roles, NoWrap: true),
        new SeparatorElement(),
        new RowElement(new HudElement[]
        {
            Column(() => _text.NamesColumn, FitToText, TextAlign.Left),
            Column(() => _text.ClassesColumn, FitToText, TextAlign.Left),
            NumberColumn(() => _text.AbilityScoresColumn, () => _text.AbilityScoreLabelFits, PartyColumns.AbilityScoreWidth),
            NumberColumn(() => _text.SeasonStrengthsColumn, () => _text.SeasonStrengthLabelFits, PartyColumns.SeasonStrengthWidth),
        }, CellGap),
    }, Gap: 2f);

    // A number column is right-aligned in its fixed width, so the numbers line up on the right. A text holds one
    // alignment, so when the header is too long to sit there (the "Season Strength" fallback, a one-word name, a
    // long translation) the whole column is left-aligned and fits its text instead, which widens it. Whether it
    // fits is decided by PartyText when the column is rebuilt; this only reads the answer.
    private static ConditionalElement NumberColumn(Func<string> lines, Func<bool> headerFits, float width) => new(
        When: headerFits,
        Then: Column(lines, width, TextAlign.Right),
        Else: Column(lines, FitToText, TextAlign.Left));

    private static TextElement Column(Func<string> lines, float width, TextAlign align) =>
        new(lines, Width: width, Align: align, NoWrap: true);
}
