using System;
using System.Collections.Generic;
using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters;

/// <summary>Reads a player's <see cref="PlayerBuild"/>. Called every tick while the window is open, so it
/// only rebuilds the Imagine, season-talent and consumable lists when the framework hands out a new buff or
/// skill list (both are cached snapshots, replaced only on change), and keeps the previous instance when a
/// list didn't actually change (e.g. a combat buff refreshed) so the UI doesn't redraw.</summary>
internal sealed class PlayerBuildReader : IPlayerBuildSource
{
    // EAttrType.AttrSeasonStrength; the game names it per season (e.g. "Illusion-Breaking Strength").
    private const int SeasonStrengthAttributeId = 11440;

    private readonly ICombatLookup _combatLookup;
    private readonly IEntityDetail _entityDetail;
    private readonly IGameDataResonance _resonanceData;
    private readonly IConsumableCatalog _consumableCatalog;

    private EntityId _cachedEntity;
    private IReadOnlyList<ActiveBuff>? _cachedBuffs;
    private IReadOnlyList<SkillLevel>? _cachedSkills;
    private IReadOnlyList<int> _seasonTalent = Array.Empty<int>();
    private IReadOnlyList<ActiveConsumable> _consumables = Array.Empty<ActiveConsumable>();

    // False when the consumables were worked out while the buff categories couldn't be read; they are then
    // worked out again as soon as the catalog becomes ready, not only when the player's buffs change.
    private bool _consumablesComplete;
    private IReadOnlyList<EquippedImagine> _imagines = Array.Empty<EquippedImagine>();

    public PlayerBuildReader(ICombatLookup combatLookup, IEntityDetail entityDetail, IGameDataResonance resonanceData,
        IConsumableCatalog consumableCatalog)
    {
        _combatLookup = combatLookup;
        _entityDetail = entityDetail;
        _resonanceData = resonanceData;
        _consumableCatalog = consumableCatalog;
    }

    public PlayerBuild Read(long entityId)
    {
        var entity = new EntityId(entityId);
        if (entity != _cachedEntity) Reset(entity);

        var buffs = _combatLookup.BuffsFor(entity);
        RefreshBuffLists(buffs);
        if (!_consumablesComplete && _consumableCatalog.IsReady) RefreshConsumables(buffs);
        RefreshImagines(_combatLookup.GetSkillLevels(entity));

        var abilityScore = _combatLookup.GetFightPoint(entity);
        // Out of range, the profile data loaded with the card still carries the score.
        if (abilityScore == 0) abilityScore = _entityDetail.GetSocialSnapshot(entity)?.FightPoint ?? 0;
        var seasonStrength = _entityDetail.GetAttribute(entity, SeasonStrengthAttributeId);
        return new PlayerBuild(abilityScore, seasonStrength, _imagines, _seasonTalent, _consumables);
    }

    private void Reset(EntityId entity)
    {
        _cachedEntity = entity;
        _cachedBuffs = null;
        _cachedSkills = null;
        _seasonTalent = Array.Empty<int>();
        _consumables = Array.Empty<ActiveConsumable>();
        _consumablesComplete = false;
        _imagines = Array.Empty<EquippedImagine>();
    }

    private void RefreshBuffLists(IReadOnlyList<ActiveBuff> buffs)
    {
        if (ReferenceEquals(buffs, _cachedBuffs)) return;
        _cachedBuffs = buffs;

        var sources = new BuffSource[buffs.Count];
        for (var i = 0; i < buffs.Count; i++) sources[i] = new BuffSource(buffs[i].BaseId, buffs[i].SourceKind);

        var seasonTalent = SeasonTalentBuffs.From(sources);
        if (!SeasonTalentBuffs.SameIds(seasonTalent, _seasonTalent)) _seasonTalent = seasonTalent;

        RefreshConsumables(buffs);
    }

    private void RefreshConsumables(IReadOnlyList<ActiveBuff> buffs)
    {
        var timed = new TimedBuff[buffs.Count];
        for (var i = 0; i < buffs.Count; i++) timed[i] = new TimedBuff(buffs[i].BaseId, buffs[i].CreateTimeMs, buffs[i].DurationMs);

        var consumables = ConsumableBuffs.From(timed, _consumableCatalog.KindOf);
        if (!ConsumableBuffs.Same(consumables, _consumables)) _consumables = consumables;
        // Checked after the lookups, so a table that failed in the middle of them also counts as incomplete.
        _consumablesComplete = _consumableCatalog.IsReady;
    }

    private void RefreshImagines(IReadOnlyList<SkillLevel> skills)
    {
        if (ReferenceEquals(skills, _cachedSkills)) return;
        _cachedSkills = skills;

        var imagines = new List<EquippedImagine>(2);
        for (var i = 0; i < skills.Count; i++)
        {
            // A skill is a Battle Imagine exactly when the resonance table knows it (its Imagine slot).
            if (_resonanceData.GetImagineForSkill(skills[i].SkillId) is not null)
                imagines.Add(new EquippedImagine(skills[i].SkillId, skills[i].Tier));
        }

        _imagines = imagines;
    }
}
