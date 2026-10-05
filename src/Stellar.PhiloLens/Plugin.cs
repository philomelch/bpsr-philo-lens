using System;
using Stellar.Abstractions.Plugins;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Adapters;
using Stellar.PhiloLens.Adapters.GameTables;
using Stellar.PhiloLens.Adapters.Party;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.UI;

namespace Stellar.PhiloLens;

/// <summary>Entry point and composition root: the only place that builds concrete types. It wires
/// Adapters → Application → UI, and releases everything in <see cref="Dispose"/>. No logic lives here.</summary>
public sealed class Plugin : IStellarPlugin
{
    private const string LogTag = "[Stellar.PhiloLens]";

    private readonly IPluginServices _services;
    private readonly InspectionService _inspection;
    private readonly PartyInspectionService _party;
    private readonly InspectWindow _window;
    private readonly ProfileCardButton _cardButton;

    public Plugin(IPluginServices services)
    {
        _services = services;
        var gameTables = new GameTableReader(services.Log);
        var classEvidence = new ClassEvidenceSource(services.EntityDetail, services.CombatSpec, services.PartyRoster,
            services.ClientState);
        _inspection = new InspectionService(classEvidence,
            new PlayerBuildReader(services.CombatLookup, services.EntityDetail, services.ResonanceData,
                new GameTableConsumables(gameTables)));
        // Lua escape hatch, approved for read-only use: no typed service carries another player's party roster.
        _party = new PartyInspectionService(new PartyRosterReader(services.Lua, services.Log), classEvidence,
            () => Environment.TickCount64);
        var buildNames = new GameTableNames(gameTables, services.GameData.Inventory, services.ResonanceData,
            services.DeepSlumber, services.Log);
        var inspectText = new InspectText(services.Localization, services.GameData.Combat, buildNames, services.CombatSnapshot);
        var partyPanel = new PartyPanel(new PartyText(services.Localization, services.GameData.Combat));
        _window = new InspectWindow(services.Windows, services.ClientState, services.Localization,
            new InspectWindowContent(_inspection, inspectText, _party, partyPanel));
        _cardButton = new ProfileCardButton(services.ProfileCardActions, services.Localization, OnInspect);

        // A method group stored once, so Dispose can unsubscribe the same delegate (inline lambdas leak).
        _services.Framework.Update += OnUpdate;
        _services.Log.Info($"{LogTag} loaded");
    }

    /// <summary>User-visible: shown in Settings → Plugins and as the hotkey group header.</summary>
    public string Name => "Philo Lens";

    public void Dispose()
    {
        // Dispose must never throw: detaches can race framework shutdown.
        try { _services.Framework.Update -= OnUpdate; } catch { /* swallow */ }
        _cardButton.Dispose();
        _window.Dispose();
    }

    // Only while the window is open: the spec can appear mid-inspection once the player casts.
    private void OnUpdate(float deltaTime)
    {
        if (!_window.IsShown) return;

        _window.HideIfCardClosed();
        // The card read comes first: it also carries the player's own stats for when they're out of range.
        _party.Refresh();
        _inspection.Refresh(_party.Profile);
        ShowInspectedPlayerStatsInParty();
        _window.RefreshText();
    }

    private void OnInspect(long entityId)
    {
        _party.Inspect(entityId);
        _inspection.Inspect(entityId, _party.Profile);
        ShowInspectedPlayerStatsInParty();
        // Build the text before showing, so the previous player's text never flashes up.
        _window.RefreshText();
        _window.Show();
    }

    // The inspected player's party row shows the same numbers as the main view above it.
    private void ShowInspectedPlayerStatsInParty()
    {
        if (_inspection.Current is { } player)
            _party.ShowInspectedPlayerStats(player.Build.AbilityScore, player.Build.SeasonStrength);
    }
}
