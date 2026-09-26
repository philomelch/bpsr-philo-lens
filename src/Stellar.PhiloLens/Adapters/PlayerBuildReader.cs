using System;
using System.Collections.Generic;
using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters;

/// <summary>Reads a player's <see cref="PlayerBuild"/>. Called every tick while the window is open, so it
/// only rebuilds the Imagine and season-talent lists when the framework hands out a new buff or skill list
/// (both are cached snapshots, replaced only on change), and keeps the previous instance when the Deep
/// Slumber effects didn't actually change (e.g. a combat buff refreshed) so the UI doesn't redraw.</summary>
internal sealed class PlayerBuildReader : IPlayerBuildSource
{
    // EAttrType.AttrSeasonStrength; the game names it per season (e.g. "Illusion-Breaking Strength").
    private const int SeasonStrengthAttributeId = 11440;

    private readonly ICombatLookup _combatLookup;
    private readonly IEntityDetail _entityDetail;
    private readonly IGameDataResonance _resonanceData;

    private EntityId _cachedEntity;
    private IReadOnlyList<ActiveBuff>? _cachedBuffs;
    private IReadOnlyList<SkillLevel>? _cachedSkills;
    private IReadOnlyList<int> _seasonTalent = Array.Empty<int>();
    private IReadOnlyList<EquippedImagine> _imagines = Array.Empty<EquippedImagine>();

    public PlayerBuildReader(ICombatLookup combatLookup, IEntityDetail entityDetail, IGameDataResonance resonanceData)
    {
        _combatLookup = combatLookup;
        _entityDetail = entityDetail;
        _resonanceData = resonanceData;
    }

    public PlayerBuild Read(long entityId)
    {
        var entity = new EntityId(entityId);
        if (entity != _cachedEntity) Reset(entity);

        RefreshSeasonTalent(_combatLookup.BuffsFor(entity));
        RefreshImagines(_combatLookup.GetSkillLevels(entity));

        var abilityScore = _combatLookup.GetFightPoint(entity);
        // Out of range, the profile data loaded with the card still carries the score.
        if (abilityScore == 0) abilityScore = _entityDetail.GetSocialSnapshot(entity)?.FightPoint ?? 0;
        var seasonStrength = _entityDetail.GetAttribute(entity, SeasonStrengthAttributeId);
        return new PlayerBuild(abilityScore, seasonStrength, _imagines, _seasonTalent);
    }

    private void Reset(EntityId entity)
    {
        _cachedEntity = entity;
        _cachedBuffs = null;
        _cachedSkills = null;
        _seasonTalent = Array.Empty<int>();
        _imagines = Array.Empty<EquippedImagine>();
    }

    private void RefreshSeasonTalent(IReadOnlyList<ActiveBuff> buffs)
    {
        if (ReferenceEquals(buffs, _cachedBuffs)) return;
        _cachedBuffs = buffs;

        var sources = new BuffSource[buffs.Count];
        for (var i = 0; i < buffs.Count; i++) sources[i] = new BuffSource(buffs[i].BaseId, buffs[i].SourceKind);

        var deepSlumber = SeasonTalentBuffs.From(sources);
        if (!SeasonTalentBuffs.SameIds(deepSlumber, _seasonTalent)) _seasonTalent = deepSlumber;
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
