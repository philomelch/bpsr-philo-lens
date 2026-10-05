using System.Globalization;
using Stellar.Abstractions.Domain.GameData;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.UI;

/// <summary>Formats a <see cref="ClassSpec"/> as "Class · Spec", or the class alone when the spec is unknown.</summary>
internal static class ClassSpecText
{
    /// <param name="complete">False when the class name wasn't loaded yet and a placeholder was used, so the
    /// caller knows to try again later.</param>
    public static string Format(ILocalization localization, IGameDataCombat combatData, ClassSpec classSpec,
        out bool complete)
    {
        complete = true;
        if (!classSpec.HasClass) return localization.T("inspect.unknown_class");

        // The profession table loads lazily after login; the caller retries until it has the name.
        var className = combatData.GetProfession(classSpec.ClassId)?.Name;
        if (string.IsNullOrEmpty(className))
        {
            complete = false;
            className = localization.TFormat("inspect.class_id", classSpec.ClassId.ToString(CultureInfo.InvariantCulture));
        }

        // Spec names exist only in English in the SDK; fall back to the class alone for an unmapped id.
        var specName = classSpec.HasSpec ? ProfessionSpecs.Name(classSpec.SpecId) : null;
        return specName is null ? className : localization.TFormat("inspect.class_spec", className, specName);
    }
}
