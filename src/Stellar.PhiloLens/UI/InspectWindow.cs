using System;
using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;

namespace Stellar.PhiloLens.UI;

/// <summary>Shows the inspected player's class, spec, stats and build at a fixed spot left of screen centre,
/// next to where the profile card opens. Opened by the card's button and closed together with the card.
/// All text comes from <see cref="InspectText"/>, refreshed by <see cref="RefreshText"/> from the update tick;
/// the element callbacks only read its cached strings, so the UI never triggers game reads.</summary>
internal sealed class InspectWindow : IDisposable
{
    private const string WindowId = "philo-lens.inspect";

    // The profile card lives on the game's full-screen function layer, which the framework reports as
    // FullScreenMenu. The flag doesn't say which window is open, but the window only opens from the
    // card, so the flag dropping means the card was closed.
    private const GameUIState CardLayer = GameUIState.FullScreenMenu;

    // Offset from the left edge of the canvas (vertically centred), in canvas units.
    private const float LeftEdgeOffset = 60f;
    private const float Width = 300f;
    private const float SectionGap = 6f;

    // Black backdrop behind the text: the glass frame alone is too see-through over bright scenery.
    private const float BackdropOpacity = 0.7f;

    private readonly IClientState _clientState;
    private readonly ILocalization _localization;
    private readonly InspectionService _inspection;
    private readonly InspectText _text;
    private readonly IWindowControl _control;

    public InspectWindow(IWindowHost windows, IClientState clientState, ILocalization localization,
        InspectionService inspection, InspectText text)
    {
        _clientState = clientState;
        _localization = localization;
        _inspection = inspection;
        _text = text;
        _control = windows.Register(new WindowRegistration(
            Spec: new WindowSpec(WindowId, "Philo Lens", new WindowRect(LeftEdgeOffset, 0f, Width, 0f),
                WindowCategory.Tools, WindowPanelStyle.GlassMenu)
            {
                Anchor = WindowAnchor.Left,
                Closable = true,
                StartVisible = false,
                ShouldRender = IsCardLayerOpen,
            },
            Root: new ColumnElement(new HudElement[]
            {
                new BackdropElement(Backdrop),
                new ConditionalElement(When: HasPlayer, Then: BuildContent(), Else: new TextElement(LoadingText)),
            }),
            OnClose: Hide));
    }

    public bool IsShown => _control.IsShown;

    public void Show()
    {
        _control.SetVisible(true);
        _control.BringToFront();
    }

    /// <summary>Rebuilds the window text when the shown player or names changed. Skipped while game reads
    /// are unsafe (zone loads), keeping the last text.</summary>
    public void RefreshText()
    {
        if (_clientState.IsWorldActive) _text.Refresh(_inspection.Current);
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

    private ColumnElement BuildContent() => new(new HudElement[]
    {
        new TextElement(() => _text.ClassSpec, Emphasis: true),
        new ConditionalElement(When: () => _text.SpecNote.Length > 0, Then: new TextElement(() => _text.SpecNote)),
        new SeparatorElement(),
        new TextElement(() => _text.Stats),
        Section(() => _localization.T("section.consumables"), () => _text.Consumables),
        Section(() => _localization.T("section.imagines"), () => _text.Imagines),
        Section(() => _text.SeasonTalentTitle, () => _text.SeasonTalent),
    }, Gap: 2f);

    // A titled block that is hidden while it has nothing to list (e.g. no Imagines, or out of range).
    private static ConditionalElement Section(Func<string> title, Func<string> body) => new(
        When: () => body().Length > 0,
        Then: new ColumnElement(new HudElement[]
        {
            new SpacerElement(Height: SectionGap),
            new TextElement(title, Emphasis: true),
            new TextElement(body),
        }, Gap: 2f));

    private bool IsCardLayerOpen() =>
        _clientState.Phase == GamePhase.World && (_clientState.UiState & CardLayer) != 0;

    private bool HasPlayer() => _inspection.Current.HasValue;

    private static float Backdrop() => BackdropOpacity;

    private string LoadingText() => _localization.T("inspect.loading");

    // Keeps IsShown in sync with the close button.
    private void Hide() => _control.SetVisible(false);
}
