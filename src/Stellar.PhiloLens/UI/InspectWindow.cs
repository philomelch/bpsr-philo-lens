using System;
using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;

namespace Stellar.PhiloLens.UI;

/// <summary>What the Lens window shows: the inspected player and their party, with the text built from each.</summary>
internal sealed record InspectWindowContent(InspectionService Inspection, InspectText Text, PartyInspectionService Party,
    PartyPanel PartyPanel);

/// <summary>Shows the inspected player's class, spec, stats and build, and their party to the right of it when
/// they're in one. It opens near the middle of the screen and can be dragged by its title bar; the framework
/// remembers where the player put it. Opened by the card's button and closed together with the card.
/// All text comes from <see cref="InspectText"/> and <see cref="PartyText"/>, refreshed by <see cref="RefreshText"/>
/// from the update tick; the element callbacks only read their cached strings, so the UI never triggers game reads.</summary>
internal sealed class InspectWindow : IDisposable
{
    private const string WindowId = "philo-lens.inspect";

    // The profile card lives on the game's full-screen function layer, which the framework reports as
    // FullScreenMenu. The flag doesn't say which window is open, but the window only opens from the
    // card, so the flag dropping means the card was closed.
    private const GameUIState CardLayer = GameUIState.FullScreenMenu;

    // The window's height fits its content, so the framework can only centre its top edge. Raising the top
    // by about half a typical filled-in window (canvas units) makes it open roughly centred on screen.
    private const float CentreTopOffset = -180f;
    private const float Width = 300f;

    // The window's width follows its content: the player's column is as wide as its longest line (its text
    // never wraps, which keeps auto-width safe), and the party section only appears for a party.
    private const float SectionGap = 6f;

    // Space on each side of the line between the player's column and the party section.
    private const float PartyGap = 16f;

    // Space between the text and the left and right edges of the dark backdrop.
    private const float SideMargin = 8f;

    // Black backdrop behind the text: the glass frame alone is too see-through over bright scenery.
    private const float BackdropOpacity = 0.7f;

    private readonly IClientState _clientState;
    private readonly ILocalization _localization;
    private readonly InspectWindowContent _content;
    private readonly IWindowControl _control;

    public InspectWindow(IWindowHost windows, IClientState clientState, ILocalization localization,
        InspectWindowContent content)
    {
        _clientState = clientState;
        _localization = localization;
        _content = content;
        _control = windows.Register(new WindowRegistration(
            Spec: new WindowSpec(WindowId, "Philo Lens", new WindowRect(0f, CentreTopOffset, Width, 0f),
                WindowCategory.Tools, WindowPanelStyle.GlassMenu)
            {
                Anchor = WindowAnchor.Center,
                AutoSizeWidth = true,
                // Free drag by the title bar; the framework saves the position per layout slot and resolution.
                Draggable = true,
                Closable = true,
                StartVisible = false,
                ShouldRender = IsCardLayerOpen,
            },
            Root: new ColumnElement(new HudElement[]
            {
                new BackdropElement(Backdrop),
                WithSideMargins(new ConditionalElement(When: HasPlayer, Then: BuildContent(),
                    Else: new TextElement(LoadingText, NoWrap: true))),
            }),
            OnClose: Hide));
    }

    public bool IsShown => _control.IsShown;

    public void Show()
    {
        _control.SetVisible(true);
        _control.BringToFront();
    }

    /// <summary>Rebuilds the window text when the shown player, their party or names changed. Skipped while game
    /// reads are unsafe (zone loads), keeping the last text.</summary>
    public void RefreshText()
    {
        if (!_clientState.IsWorldActive) return;

        _content.Text.Refresh(_content.Inspection.Current);
        _content.PartyPanel.Refresh(_content.Party.Current);
    }

    /// <summary>Hides the window once the profile card it was opened from has closed.</summary>
    public void HideIfCardClosed()
    {
        if (_control.IsShown && !IsCardLayerOpen()) Hide();
    }

    public void Dispose()
    {
        // Dispose must never throw: the framework may already be shutting down.
        try { _control.Remove(); } catch { /* swallow */ }
    }

    // The backdrop stretches over all the content, so space beside the text keeps it off the backdrop's edges.
    private static RowElement WithSideMargins(HudElement content) => new(new HudElement[]
    {
        new SpacerElement(Width: SideMargin),
        content,
        new SpacerElement(Width: SideMargin),
    });

    private RowElement BuildContent() => new(new HudElement[]
    {
        BuildPlayerColumn(),
        new ConditionalElement(When: () => _content.PartyPanel.HasParty, Then: new RowElement(new HudElement[]
        {
            new SeparatorElement(Vertical: true),
            _content.PartyPanel.Build(),
        }, PartyGap)),
    }, PartyGap);

    private ColumnElement BuildPlayerColumn()
    {
        var text = _content.Text;
        return new ColumnElement(new HudElement[]
        {
            new TextElement(() => text.ClassSpec, Emphasis: true, NoWrap: true),
            new ConditionalElement(When: () => text.SpecNote.Length > 0,
                Then: new TextElement(() => text.SpecNote, NoWrap: true)),
            new SeparatorElement(),
            new TextElement(() => text.Stats, NoWrap: true),
            Section(() => _localization.T("section.consumables"), () => text.Consumables),
            Section(() => _localization.T("section.imagines"), () => text.Imagines),
            Section(() => text.SeasonTalentTitle, () => text.SeasonTalent),
        }, Gap: 2f);
    }

    // A titled block that is hidden while it has nothing to list (e.g. no Imagines, or out of range).
    private static ConditionalElement Section(Func<string> title, Func<string> body) => new(
        When: () => body().Length > 0,
        Then: new ColumnElement(new HudElement[]
        {
            new SpacerElement(Height: SectionGap),
            new TextElement(title, Emphasis: true, NoWrap: true),
            new TextElement(body, NoWrap: true),
        }, Gap: 2f));

    private bool IsCardLayerOpen() =>
        _clientState.Phase == GamePhase.World && (_clientState.UiState & CardLayer) != 0;

    private bool HasPlayer() => _content.Inspection.Current.HasValue;

    private static float Backdrop() => BackdropOpacity;

    private string LoadingText() => _localization.T("inspect.loading");

    // Keeps IsShown in sync with the close button.
    private void Hide() => _control.SetVisible(false);
}
