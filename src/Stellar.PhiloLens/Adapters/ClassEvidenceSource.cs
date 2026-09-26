using Stellar.Abstractions.Domain;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters;

/// <summary>Reads a player's <see cref="ClassEvidence"/> from the framework's typed services.</summary>
internal sealed class ClassEvidenceSource : IClassEvidenceSource
{
    // EAttrType.AttrProfessionId: the player's class, or a transform id while transformed.
    private const int ProfessionAttributeId = 220;

    private readonly IEntityDetail _entityDetail;
    private readonly ICombatSpec _combatSpec;
    private readonly IPartyRoster _partyRoster;
    private readonly IClientState _clientState;

    public ClassEvidenceSource(IEntityDetail entityDetail, ICombatSpec combatSpec, IPartyRoster partyRoster,
        IClientState clientState)
    {
        _entityDetail = entityDetail;
        _combatSpec = combatSpec;
        _partyRoster = partyRoster;
        _clientState = clientState;
    }

    // IsWorldActive, not Phase/IsLoggedIn: those stay true mid-transition, exactly when a raw game
    // read corrupts the world-connect handshake (see the framework's plugin-development.md).
    public bool CanRead => _clientState.IsWorldActive;

    public ClassEvidence Read(long entityId)
    {
        var entity = new EntityId(entityId);
        // Loaded by the game when the profile card opens, so it covers players outside our range.
        var profile = _entityDetail.GetSocialSnapshot(entity);

        // GetSubProfession returns the talent spec when known and the cast guess otherwise, so it only adds
        // information when TryGetTalentSpec has nothing.
        _combatSpec.TryGetTalentSpec(entity, out var talentSpecId);
        return new ClassEvidence(
            LiveClassId: ToClassId(_entityDetail.GetAttribute(entity, ProfessionAttributeId)),
            PartyClassId: PartyClassId(entity),
            ProfileClassId: profile?.ProfessionId ?? 0,
            TalentSpecId: talentSpecId,
            CastSpecId: _combatSpec.GetSubProfession(entity));
    }

    private int PartyClassId(EntityId entity)
    {
        // Indexed loop: foreach over the interface would allocate an enumerator on every read.
        var members = _partyRoster.Members;
        for (var i = 0; i < members.Count; i++)
        {
            if (members[i].EntityId == entity) return members[i].Profession;
        }

        return 0;
    }

    private static int ToClassId(long attributeValue) =>
        attributeValue is > 0 and <= int.MaxValue ? (int)attributeValue : 0;
}
