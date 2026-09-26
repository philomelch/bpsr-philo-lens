using System;
using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Services;

namespace Stellar.PhiloLens.UI;

/// <summary>Adds a button to the game's native profile card that opens the inspection for the carded
/// player. The framework injects and styles the button; this only supplies the label, icon and action.</summary>
internal sealed class ProfileCardButton : IDisposable
{
    private const string ActionId = "philo-lens.inspect";

    private readonly IProfileCardActions _profileCardActions;
    private readonly ILocalization _localization;
    private readonly Action<long> _onInspect;
    private readonly byte[]? _icon = EmbeddedIcons.Load(EmbeddedIcons.Lens);
    private IDisposable? _registration;

    /// <param name="onInspect">Called on the main thread with the carded player's entity id value.</param>
    public ProfileCardButton(IProfileCardActions profileCardActions, ILocalization localization, Action<long> onInspect)
    {
        _profileCardActions = profileCardActions;
        _localization = localization;
        _onInspect = onInspect;
        Register();
        _localization.LanguageChanged += OnLanguageChanged;
    }

    public void Dispose()
    {
        // Dispose must never throw: the framework may already be shutting down.
        try { _localization.LanguageChanged -= OnLanguageChanged; } catch { /* swallow */ }
        try { _registration?.Dispose(); } catch { /* swallow */ }
    }

    // A registered button's label is fixed, so a language switch replaces the registration with a relabelled one.
    private void OnLanguageChanged()
    {
        try { _registration?.Dispose(); } catch { /* swallow */ }
        Register();
    }

    private void Register() => _registration = _profileCardActions.Register(
        new ProfileCardActionSpec(ActionId, _localization.T("card.button"), _icon, OnClick));

    private void OnClick(EntityId entity) => _onInspect(entity.Value);
}
