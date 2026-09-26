using Stellar.Abstractions.Plugins;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Adapters;
using Stellar.PhiloLens.Adapters.GameTables;
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
    private readonly InspectWindow _window;
    private readonly ProfileCardButton _cardButton;

    public Plugin(IPluginServices services)
    {
        _services = services;
        _inspection = new InspectionService(
            new ClassEvidenceSource(services.EntityDetail, services.CombatSpec, services.PartyRoster, services.ClientState),
            new PlayerBuildReader(services.CombatLookup, services.EntityDetail, services.ResonanceData));
        var buildNames = new GameTableNames(new GameTableReader(services.Log), services.GameData.Inventory,
            services.ResonanceData, services.DeepSlumber, services.Log);
        _window = new InspectWindow(services.Windows, services.ClientState, services.Localization, _inspection,
            new InspectText(services.Localization, services.GameData.Combat, buildNames));
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
        _inspection.Refresh();
        _window.RefreshText();
    }

    private void OnInspect(long entityId)
    {
        _inspection.Inspect(entityId);
        // Build the text before showing, so the previous player's text never flashes up.
        _window.RefreshText();
        _window.Show();
    }
}
