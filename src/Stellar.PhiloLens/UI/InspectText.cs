using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Stellar.Abstractions.Domain.GameData;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.UI;

/// <summary>The inspection window's text, built from an <see cref="InspectedPlayer"/>. <see cref="Refresh"/>
/// runs from the update tick (never from the window's text callbacks, which only read the cached strings) and
/// rebuilds only when the player, the language or the loaded names changed, or, while a name is still
/// missing, at most once per <see cref="RetryIntervalMs"/>. The consumable countdowns are rebuilt on their own,
/// once per second while one is running.</summary>
internal sealed class InspectText
{
    // EAttrType ids; their localised labels come from the game's attribute table.
    private const int AbilityScoreAttributeId = 10030;
    private const int SeasonStrengthAttributeId = 11440;

    // A missing name (e.g. the profession table still loading after login) is retried at this pace, not per tick.
    private const long RetryIntervalMs = 1_000;

    private readonly ILocalization _localization;
    private readonly IGameDataCombat _combatData;
    private readonly IBuildNameSource _buildNames;
    private readonly ICombatSnapshot _combatSnapshot;
    private readonly Func<long> _clockMs;

    private InspectedPlayer? _rendered;
    private string? _renderedLanguage;
    private int _renderedNamesVersion = -1;
    private bool _complete;
    private long _lastRenderMs;
    private IReadOnlyList<ActiveConsumable>? _renderedConsumables;
    private long _renderedConsumablesSecond = -1;
    private bool _consumablesComplete;
    private long _lastConsumablesRenderMs;

    /// <param name="clockMs">Monotonic milliseconds for the retry pacing; the system tick count by default
    /// (tests pass their own).</param>
    public InspectText(ILocalization localization, IGameDataCombat combatData, IBuildNameSource buildNames,
        ICombatSnapshot combatSnapshot, Func<long>? clockMs = null)
    {
        _localization = localization;
        _combatData = combatData;
        _buildNames = buildNames;
        _combatSnapshot = combatSnapshot;
        _clockMs = clockMs ?? (() => Environment.TickCount64);
    }

    public string ClassSpec { get; private set; } = string.Empty;

    /// <summary>"Spec not seen yet" while only the class is known; empty otherwise.</summary>
    public string SpecNote { get; private set; } = string.Empty;
    public string Stats { get; private set; } = string.Empty;
    public string Imagines { get; private set; } = string.Empty;
    public string SeasonTalent { get; private set; } = string.Empty;
    public string SeasonTalentTitle { get; private set; } = string.Empty;

    /// <summary>Food, potion and food-bonus buffs, one per line, with a countdown where they run out.</summary>
    public string Consumables { get; private set; } = string.Empty;

    /// <summary>Rebuilds the text if anything it shows changed. Reads game data, so only call while game
    /// reads are safe (<c>IsWorldActive</c>).</summary>
    public void Refresh(InspectedPlayer? current)
    {
        _buildNames.Refresh();
        if (current is not { } player) return;

        var rebuilt = !IsUpToDate(current);
        if (rebuilt) Rebuild(player);
        RefreshConsumables(player.Build.Consumables, rebuilt);
    }

    private void Rebuild(InspectedPlayer player)
    {
        _rendered = player;
        _renderedLanguage = _localization.Language;
        _renderedNamesVersion = _buildNames.Version;
        _lastRenderMs = _clockMs();
        _complete = true;
        ClassSpec = FormatClassSpec(player.ClassSpec);
        SpecNote = player.ClassSpec is { HasClass: true, HasSpec: false } ? _localization.T("inspect.spec_not_seen") : string.Empty;
        Stats = FormatStats(player.Build);
        Imagines = FormatImagines(player.Build.Imagines);
        SeasonTalent = FormatSeasonTalent(player.Build.SeasonTalentBuffIds);
        SeasonTalentTitle = _buildNames.SeasonTalentTitle ?? _localization.T("section.season_talent");
    }

    // Countdowns change every second; the rest of the text doesn't, so this part is rebuilt on its own.
    private void RefreshConsumables(IReadOnlyList<ActiveConsumable> consumables, bool force)
    {
        var nowMs = _combatSnapshot.ServerNowMs;
        var second = HasRunningTimer(consumables, nowMs) ? nowMs / 1_000 : -1;
        var unchanged = ReferenceEquals(consumables, _renderedConsumables) && second == _renderedConsumablesSecond;
        // A buff name missing because the buff table was still loading is retried like a missing class name.
        var retryDue = !_consumablesComplete && _clockMs() - _lastConsumablesRenderMs >= RetryIntervalMs;
        if (!force && unchanged && !retryDue) return;

        _renderedConsumables = consumables;
        _renderedConsumablesSecond = second;
        _lastConsumablesRenderMs = _clockMs();
        _consumablesComplete = true;
        Consumables = FormatConsumables(consumables, nowMs);
    }

    // Without server time yet (ServerNowMs is 0 until the first sync) no countdown can be shown.
    private static bool HasRunningTimer(IReadOnlyList<ActiveConsumable> consumables, long nowMs)
    {
        if (nowMs <= 0) return false;
        for (var i = 0; i < consumables.Count; i++)
        {
            if (consumables[i].HasTimer && consumables[i].RemainingMs(nowMs) > 0) return true;
        }

        return false;
    }

    private string FormatConsumables(IReadOnlyList<ActiveConsumable> consumables, long nowMs)
    {
        var lines = new StringBuilder();
        for (var i = 0; i < consumables.Count; i++)
        {
            var consumable = consumables[i];
            // A buff that has run out is about to be removed by the server; don't show it at 0:00 meanwhile.
            if (consumable.HasTimer && nowMs > 0 && consumable.RemainingMs(nowMs) == 0) continue;

            var buff = _combatData.GetBuff(consumable.BuffId);
            if (buff is not { } info || string.IsNullOrEmpty(info.Name))
            {
                _consumablesComplete = false;
                continue;
            }

            var line = consumable.HasTimer && nowMs > 0
                ? _localization.TFormat("consumable.line_timer", info.Name, info.Description, Countdown.Format(consumable.RemainingMs(nowMs)))
                : _localization.TFormat("consumable.line", info.Name, info.Description);
            AppendLine(lines, line);
        }

        return lines.ToString();
    }

    private bool IsUpToDate(InspectedPlayer? current)
    {
        var sameInputs = current == _rendered && ReferenceEquals(_localization.Language, _renderedLanguage)
            && _renderedNamesVersion == _buildNames.Version;
        return sameInputs && (_complete || _clockMs() - _lastRenderMs < RetryIntervalMs);
    }

    private string FormatClassSpec(ClassSpec classSpec)
    {
        if (!classSpec.HasClass) return _localization.T("inspect.unknown_class");

        // The profession table loads lazily after login; retry on the next poll until it has the name.
        var className = _combatData.GetProfession(classSpec.ClassId)?.Name;
        if (string.IsNullOrEmpty(className))
        {
            _complete = false;
            className = _localization.TFormat("inspect.class_id", classSpec.ClassId.ToString(CultureInfo.InvariantCulture));
        }

        // Spec names exist only in English in the SDK; fall back to the class alone for an unmapped id.
        var specName = classSpec.HasSpec ? ProfessionSpecs.Name(classSpec.SpecId) : null;
        return specName is null ? className : _localization.TFormat("inspect.class_spec", className, specName);
    }

    private string FormatStats(PlayerBuild build) =>
        StatRow(AbilityScoreAttributeId, "stats.ability_score", build.AbilityScore) + "\n"
        + StatRow(SeasonStrengthAttributeId, "stats.season_strength", build.SeasonStrength);

    private string StatRow(int attributeId, string fallbackLabelKey, long value)
    {
        var label = _combatData.GetAttribute(attributeId)?.Name;
        if (string.IsNullOrEmpty(label)) label = _localization.T(fallbackLabelKey);
        var text = value > 0 ? value.ToString("N0", CultureInfo.InvariantCulture) : _localization.T("stats.none");
        return _localization.TFormat("stats.row", label, text);
    }

    private string FormatImagines(IReadOnlyList<EquippedImagine> imagines)
    {
        var lines = new StringBuilder();
        for (var i = 0; i < imagines.Count; i++)
        {
            var name = _buildNames.ImagineName(imagines[i].SkillId);
            if (string.IsNullOrEmpty(name)) continue;
            AppendLine(lines, _localization.TFormat("imagine.tier", name, imagines[i].Tier.ToString(CultureInfo.InvariantCulture)));
        }

        return lines.ToString();
    }

    private string FormatSeasonTalent(IReadOnlyList<int> buffIds)
    {
        var lines = new StringBuilder();
        foreach (var name in _buildNames.SeasonTalentBoardNames(buffIds)) AppendLine(lines, name);
        return lines.ToString();
    }

    private static void AppendLine(StringBuilder lines, string line)
    {
        if (lines.Length > 0) lines.Append('\n');
        lines.Append(line);
    }
}
