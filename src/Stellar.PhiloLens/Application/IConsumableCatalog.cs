using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Application;

/// <summary>Port: which buffs are consumables (food, potions, food bonuses), by the game's own buff
/// categories. Reads game data, so only call while game reads are safe.</summary>
internal interface IConsumableCatalog
{
    /// <summary>True while the game's buff categories can be read. While false, <see cref="KindOf"/> answers
    /// null for everything, so callers should ask again once this turns true.</summary>
    bool IsReady { get; }

    /// <summary>The consumable kind of buff <paramref name="buffId"/>, or null when it isn't one.</summary>
    ConsumableKind? KindOf(int buffId);
}
