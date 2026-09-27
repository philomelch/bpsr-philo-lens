using System.Collections.Generic;

// Stand-ins for the game's IL2CPP table proxies, with the same member shapes the game exposes
// (static GetTable(bool), GetRow(int), a typed GetEnumerator of key/value pairs, Int32Table-style
// wrappers with Length + get_Item). They live in the game's own namespace so the production code
// finds them by the same type names it uses in-game.
namespace Bokura;

public sealed class SkillAoyiTableBase
{
    public static Dictionary<int, SkillAoyiTableBase> Rows { get; } = new();

    public int Id { get; set; }
    public int AoyiItemId { get; set; }

    public static TableProxy<SkillAoyiTableBase> GetTable(bool autoLoad) => new(Rows);
}

public sealed class SeasonTalentTemplateTableBase
{
    public static Dictionary<int, SeasonTalentTemplateTableBase> Rows { get; } = new();

    public int Id { get; set; }
    public string TemplateName { get; set; } = string.Empty;
    public int BelongSeasonId { get; set; }
    public int BelongFunction { get; set; }

    public static TableProxy<SeasonTalentTemplateTableBase> GetTable(bool autoLoad) => new(Rows);
}

public sealed class FunctionTableBase
{
    public static Dictionary<int, FunctionTableBase> Rows { get; } = new();

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public static TableProxy<FunctionTableBase> GetTable(bool autoLoad) => new(Rows);
}

public sealed class SeasonTalentTreeTableBase
{
    public static Dictionary<int, SeasonTalentTreeTableBase> Rows { get; } = new();

    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int GroupId { get; set; }

    public static TableProxy<SeasonTalentTreeTableBase> GetTable(bool autoLoad) => new(Rows);
}

public sealed class SeasonTalentEffectOrdinaryTableBase
{
    public static Dictionary<int, SeasonTalentEffectOrdinaryTableBase> Rows { get; } = new();

    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int GroupId { get; set; }
    public Int32Table Effect { get; set; } = new();

    public static TableProxy<SeasonTalentEffectOrdinaryTableBase> GetTable(bool autoLoad) => new(Rows);
}

public sealed class BuffTableBase
{
    public static Dictionary<int, BuffTableBase> Rows { get; } = new();

    /// <summary>While true, GetTable throws, like a table that isn't loaded or readable.</summary>
    public static bool Unavailable { get; set; }

    public int Id { get; set; }
    public int BuffAbilityType { get; set; }

    public static TableProxy<BuffTableBase> GetTable(bool autoLoad) =>
        Unavailable ? throw new System.InvalidOperationException("table not loaded") : new(Rows);
}

public sealed class TableProxy<TRow>
{
    private readonly Dictionary<int, TRow> _rows;

    public TableProxy(Dictionary<int, TRow> rows) => _rows = rows;

    public TRow? GetRow(int id) => _rows.TryGetValue(id, out var row) ? row : default;

    public Dictionary<int, TRow>.Enumerator GetEnumerator() => _rows.GetEnumerator();
}

/// <summary>Like the game's Bokura.Table.Int32Table: Length + get_Item, no BCL IList.</summary>
public sealed class Int32Table
{
    private readonly List<Int32Array> _rows = new();

    public int Length => _rows.Count;

    public Int32Array this[int index] => _rows[index];

    public Int32Table Add(params int[] values)
    {
        _rows.Add(new Int32Array(values));
        return this;
    }
}

/// <summary>Like the game's generic TableArray base, which declares its own Length.</summary>
public class TableArray
{
    // A wrong value on purpose: reading the base Length instead of the override would show up in tests.
    private readonly int _baseLength = -1;

    public int Length => _baseLength;
}

/// <summary>Like the game's Bokura.Table.Int32Array: Length + get_Item, no BCL IList. It re-declares
/// Length over its base, so reflection sees two Length properties (as on the real IL2CPP proxy).</summary>
public sealed class Int32Array : TableArray
{
    private readonly int[] _values;

    public Int32Array(int[] values) => _values = values;

    public new int Length => _values.Length;

    public int this[int index] => _values[index];
}
