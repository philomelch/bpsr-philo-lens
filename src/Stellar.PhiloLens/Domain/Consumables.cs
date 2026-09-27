using System;
using System.Collections.Generic;

namespace Stellar.PhiloLens.Domain;

/// <summary>The consumable buff categories the window shows, in display order.</summary>
internal enum ConsumableKind
{
    /// <summary>A food buff ("Cuisine").</summary>
    Food,

    /// <summary>A potion (serum) buff ("Potion").</summary>
    Potion,

    /// <summary>The bonus some food triggers ("Foodie's Grace").</summary>
    FoodBonus,
}

/// <summary>A buff with its timing, as the framework reports it.</summary>
/// <param name="BaseId">The buff's table id.</param>
/// <param name="StartMs">Server epoch ms when the buff was applied.</param>
/// <param name="DurationMs">Total duration in ms; 0 or less means it doesn't run out.</param>
internal readonly record struct TimedBuff(int BaseId, long StartMs, int DurationMs);

/// <summary>A food, potion or food-bonus buff a player has, with its timing.</summary>
internal readonly record struct ActiveConsumable(int BuffId, ConsumableKind Kind, long StartMs, int DurationMs)
{
    public bool HasTimer => DurationMs > 0;

    /// <summary>Time left at <paramref name="nowMs"/> (server epoch ms), never below zero.</summary>
    public long RemainingMs(long nowMs) => Math.Max(0, StartMs + DurationMs - nowMs);
}

/// <summary>Picks the consumable buffs out of a player's buffs.</summary>
internal static class ConsumableBuffs
{
    /// <summary>The buffs in <paramref name="buffs"/> that <paramref name="kindOf"/> classes as consumables,
    /// food first, then potions, then food bonuses (each by buff id), so the order is stable.</summary>
    public static IReadOnlyList<ActiveConsumable> From(IReadOnlyList<TimedBuff> buffs, Func<int, ConsumableKind?> kindOf)
    {
        List<ActiveConsumable>? consumables = null;
        for (var i = 0; i < buffs.Count; i++)
        {
            if (kindOf(buffs[i].BaseId) is not { } kind) continue;
            (consumables ??= new List<ActiveConsumable>()).Add(
                new ActiveConsumable(buffs[i].BaseId, kind, buffs[i].StartMs, buffs[i].DurationMs));
        }

        if (consumables is null) return Array.Empty<ActiveConsumable>();
        consumables.Sort((left, right) => left.Kind != right.Kind ? left.Kind.CompareTo(right.Kind) : left.BuffId.CompareTo(right.BuffId));
        return consumables;
    }

    /// <summary>True when both lists hold the same buffs with the same timing.</summary>
    public static bool Same(IReadOnlyList<ActiveConsumable> left, IReadOnlyList<ActiveConsumable> right)
    {
        if (left.Count != right.Count) return false;
        for (var i = 0; i < left.Count; i++)
        {
            if (left[i] != right[i]) return false;
        }

        return true;
    }
}

/// <summary>Formats time left as a countdown: "4:05" under an hour, "1:02:03" from an hour.</summary>
internal static class Countdown
{
    private const long MsPerSecond = 1_000;
    private const long SecondsPerMinute = 60;
    private const long SecondsPerHour = 3_600;

    public static string Format(long remainingMs)
    {
        // Rounded up, so the display reaches 0:00 only when the time is really up.
        var totalSeconds = (Math.Max(0, remainingMs) + MsPerSecond - 1) / MsPerSecond;
        var hours = totalSeconds / SecondsPerHour;
        var minutes = totalSeconds % SecondsPerHour / SecondsPerMinute;
        var seconds = totalSeconds % SecondsPerMinute;
        return hours > 0 ? $"{hours}:{minutes:00}:{seconds:00}" : $"{minutes}:{seconds:00}";
    }
}
