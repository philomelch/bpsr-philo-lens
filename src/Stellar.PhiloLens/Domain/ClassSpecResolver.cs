namespace Stellar.PhiloLens.Domain;

/// <summary>Turns raw <see cref="ClassEvidence"/> into the class and spec to show. Never guesses: a value
/// is used only when it is a real class, and a spec only when it belongs to that class. A talent spec
/// wins over a cast spec, because the root talent buff is what actually defines the spec.</summary>
internal static class ClassSpecResolver
{
    /// <param name="evidence">What the sources currently report.</param>
    /// <param name="lastKnownClassId">The last real class resolved for this player, used while every live
    /// source reads a transform; <c>0</c> when none.</param>
    public static ClassSpec Resolve(ClassEvidence evidence, int lastKnownClassId)
    {
        var talentClassId = PlayableClasses.ClassOfSpec(evidence.TalentSpecId);
        var castClassId = PlayableClasses.ClassOfSpec(evidence.CastSpecId);
        var specClassId = talentClassId != 0 ? talentClassId : castClassId;
        var classId = FirstPlayable(evidence.LiveClassId, evidence.PartyClassId, evidence.ProfileClassId,
            lastKnownClassId, specClassId);
        if (classId == 0) return ClassSpec.Unknown;

        // A spec from before a class change still belongs to the old class (the framework holds it for a
        // few seconds during a swap); showing it under the new class would be wrong.
        if (talentClassId == classId) return new ClassSpec(classId, evidence.TalentSpecId, SpecSource.Talents);
        if (castClassId == classId) return new ClassSpec(classId, evidence.CastSpecId, SpecSource.Casts);
        return new ClassSpec(classId, 0, SpecSource.None);
    }

    private static int FirstPlayable(int first, int second, int third, int fourth, int fifth)
    {
        if (PlayableClasses.IsPlayable(first)) return first;
        if (PlayableClasses.IsPlayable(second)) return second;
        if (PlayableClasses.IsPlayable(third)) return third;
        if (PlayableClasses.IsPlayable(fourth)) return fourth;
        return PlayableClasses.IsPlayable(fifth) ? fifth : 0;
    }
}
