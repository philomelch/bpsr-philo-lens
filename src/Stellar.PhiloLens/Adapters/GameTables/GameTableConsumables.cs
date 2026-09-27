using System;
using System.Collections.Generic;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters.GameTables;

/// <summary>Classifies buffs by the game buff table's own <c>BuffAbilityType</c> column, which the typed
/// buff data doesn't expose: 101 = food ("Cuisine"), 102 = potion, 104 = food bonus ("Foodie's Grace").
/// Each buff id is looked up once and cached, so new seasons' foods and potions are recognised automatically.
/// Only answers read from the table are cached: while the table can't be read, buffs count as "not a
/// consumable" for now and are asked again, and the table itself is re-requested at a slow pace.</summary>
internal sealed class GameTableConsumables : IConsumableCatalog
{
    private const string BuffTable = "Bokura.BuffTableBase";
    private const int FoodAbility = 101;
    private const int PotionAbility = 102;
    private const int FoodBonusAbility = 104;

    // Pace for re-requesting an unreadable table: slow enough that a table a game patch removed logs one
    // warning every few seconds at most, fast enough to recover from a table that wasn't ready yet.
    private const long TableRetryIntervalMs = 5_000;

    private readonly GameTableReader _tables;
    private readonly Func<long> _clockMs;
    private readonly Dictionary<int, ConsumableKind?> _kinds = new();
    private object? _table;
    private long? _lastTableRequestMs;

    /// <param name="clockMs">Monotonic milliseconds for the retry pacing; the system tick count by default
    /// (tests pass their own).</param>
    public GameTableConsumables(GameTableReader tables, Func<long>? clockMs = null)
    {
        _tables = tables;
        _clockMs = clockMs ?? (() => Environment.TickCount64);
    }

    // Re-requests an unreadable table at the slow pace, so asking this is how a caller notices recovery.
    public bool IsReady => Table() is not null;

    public ConsumableKind? KindOf(int buffId)
    {
        if (_kinds.TryGetValue(buffId, out var cached)) return cached;
        if (Table() is not { } table) return null;

        if (!GameTableReader.TryGetRow(table, buffId, out var row))
        {
            // The table stopped answering: drop it so it is requested again, and don't remember this buff.
            _table = null;
            return null;
        }

        var kind = KindFor(row is null ? 0 : GameTableReader.ReadInt(row, "BuffAbilityType"));
        _kinds[buffId] = kind;
        return kind;
    }

    private object? Table()
    {
        if (_table is not null) return _table;

        var now = _clockMs();
        if (_lastTableRequestMs is { } last && now - last < TableRetryIntervalMs) return null;

        _lastTableRequestMs = now;
        _table = _tables.GetTable(BuffTable);
        return _table;
    }

    private static ConsumableKind? KindFor(int abilityType) => abilityType switch
    {
        FoodAbility => ConsumableKind.Food,
        PotionAbility => ConsumableKind.Potion,
        FoodBonusAbility => ConsumableKind.FoodBonus,
        _ => null,
    };
}
