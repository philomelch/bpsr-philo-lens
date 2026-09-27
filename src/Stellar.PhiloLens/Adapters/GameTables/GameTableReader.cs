using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Stellar.Abstractions.Services;

namespace Stellar.PhiloLens.Adapters.GameTables;

/// <summary>
/// Read-only access to the game's static configuration tables (<c>Bokura.*TableBase</c>) through
/// <see cref="StellarInterop"/> reflection. This is an SDK escape hatch, approved by the user for
/// reading tables the typed <c>IGameData</c> services don't expose (see README "Game data access").
/// It only calls the tables' own getters — <c>GetTable</c>, <c>GetRow</c> and row enumeration — and
/// never writes anything. Mirrors the framework's own table readers (<c>PandaGameDataProbe</c>).
/// Every member swallows failures and returns an empty result, so a game patch that renames a table
/// or column degrades to "no name" instead of an exception. Main thread only, like all game reads.
/// </summary>
internal sealed class GameTableReader
{
    private const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    // GetTable(bool autoLoad): true makes the game deserialize the rows before returning.
    private static readonly object[] AutoLoadArgs = { true };

    // Guards against a runaway enumerator proxy; real tables are orders of magnitude smaller.
    private const int MaxRows = 100_000;

    private readonly IPluginLog _log;

    public GameTableReader(IPluginLog log) => _log = log;

    /// <summary>The loaded table for <paramref name="typeName"/> (e.g. <c>Bokura.SkillAoyiTableBase</c>),
    /// or null when the type or its <c>GetTable</c> is missing.</summary>
    public object? GetTable(string typeName)
    {
        var getTable = StellarInterop.FindType(typeName)?.GetMethod("GetTable", AnyStatic);
        if (getTable is null)
        {
            _log.Warning($"[Stellar.PhiloLens] game table {typeName} not found");
            return null;
        }

        try
        {
            return getTable.GetParameters().Length == 0
                ? getTable.Invoke(null, Array.Empty<object>())
                : getTable.Invoke(null, AutoLoadArgs);
        }
        catch (Exception ex)
        {
            _log.Warning($"[Stellar.PhiloLens] {typeName}.GetTable threw: {ex.GetType().Name}: {ex.Message}");
            return null;
        }
    }

    /// <summary>The row with id <paramref name="id"/>, or null when there is none.</summary>
    public static object? GetRow(object table, int id) => TryGetRow(table, id, out var row) ? row : null;

    /// <summary>Looks row <paramref name="id"/> up. Returns false when the lookup itself failed (no row
    /// accessor, or it threw), so callers can tell that apart from a table that simply has no such row
    /// (true, with a null <paramref name="row"/>).</summary>
    public static bool TryGetRow(object table, int id, out object? row)
    {
        row = null;
        try
        {
            var getRow = table.GetType().GetMethod("GetRow", AnyInstance, null, new[] { typeof(int) }, null)
                ?? table.GetType().GetMethod("get_Item", AnyInstance, null, new[] { typeof(int) }, null);
            if (getRow is null) return false;
            row = getRow.Invoke(table, new object[] { id });
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Every row of <paramref name="table"/>. The table proxy's typed enumerator yields
    /// key/value pairs; each value is a row.</summary>
    public static List<object> Rows(object table)
    {
        var rows = new List<object>();
        try
        {
            var enumerator = TypedGetEnumerator(table.GetType())?.Invoke(table, Array.Empty<object>());
            var moveNext = enumerator?.GetType().GetMethod("MoveNext", AnyInstance, null, Type.EmptyTypes, null);
            var current = enumerator?.GetType().GetProperty("Current", AnyInstance);
            if (enumerator is null || moveNext is null || current is null) return rows;

            PropertyInfo? pairValue = null;
            for (var i = 0; i < MaxRows && moveNext.Invoke(enumerator, Array.Empty<object>()) is true; i++)
            {
                if (current.GetValue(enumerator) is not { } pair) continue;
                pairValue ??= pair.GetType().GetProperty("Value", AnyInstance);
                if (pairValue?.GetValue(pair) is { } row) rows.Add(row);
            }
        }
        catch
        {
            // Keep whatever was read before the proxy failed.
        }

        return rows;
    }

    public static int ReadInt(object row, string column) => ReadColumn(row, column) switch
    {
        int value => value,
        long value => unchecked((int)value),
        uint value => unchecked((int)value),
        _ => 0,
    };

    /// <summary>A text column. The game resolves localised strings when it reads the row, so this is
    /// already in the player's language; anything that isn't a plain string reads as empty.</summary>
    public static string ReadString(object row, string column) => ReadColumn(row, column) as string ?? string.Empty;

    /// <summary>A nested int column (<c>[[3, 3002010, 1], …]</c> in the table data).</summary>
    public static List<int[]> ReadIntRows(object row, string column)
    {
        var result = new List<int[]>();
        try
        {
            var value = ReadColumn(row, column);
            for (var i = 0; value is not null && i < Length(value); i++) result.Add(ReadInts(Item(value, i)));
        }
        catch
        {
            // Keep the rows read so far.
        }

        return result;
    }

    private static int[] ReadInts(object? value)
    {
        if (value is null) return Array.Empty<int>();
        if (value is int[] ints) return ints;

        var result = new int[Length(value)];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = Item(value, i) switch
            {
                int item => item,
                long item => unchecked((int)item),
                _ => 0,
            };
        }

        return result;
    }

    // Table arrays are either real arrays/lists or the game's own wrappers (Int32Array / Int32Table), which
    // expose Length + get_Item but not the BCL IList — so both shapes are read through Length/Count + get_Item.
    private static int Length(object value)
    {
        if (value is ICollection collection) return collection.Count;
        var length = FindIntProperty(value.GetType(), "Length") ?? FindIntProperty(value.GetType(), "Count");
        try { return length?.GetValue(value) is int count ? count : 0; } catch { return 0; }
    }

    private static object? Item(object value, int index)
    {
        if (value is IList list) return list[index];
        var getItem = FindIndexer(value.GetType());
        try { return getItem?.Invoke(value, new object[] { index }); } catch { return null; }
    }

    // The game's wrappers declare Length both on the base array type and as an override, so a plain
    // GetProperty("Length") throws AmbiguousMatchException; pick the int, non-indexed one explicitly.
    private static PropertyInfo? FindIntProperty(Type type, string name)
    {
        foreach (var property in type.GetProperties(AnyInstance))
        {
            if (property.Name == name && property.PropertyType == typeof(int) && property.GetIndexParameters().Length == 0)
                return property;
        }

        return null;
    }

    private static MethodInfo? FindIndexer(Type type)
    {
        foreach (var method in type.GetMethods(AnyInstance))
        {
            var parameters = method.GetParameters();
            if (method.Name == "get_Item" && parameters.Length == 1 && parameters[0].ParameterType == typeof(int))
                return method;
        }

        return null;
    }

    private static object? ReadColumn(object row, string column)
    {
        foreach (var property in row.GetType().GetProperties(AnyInstance))
        {
            if (property.Name != column || property.GetIndexParameters().Length != 0) continue;
            try { return property.GetValue(row); } catch { return null; }
        }

        return null;
    }

    // The proxy has two GetEnumerator methods: the typed parameterless one (wanted) and an explicit
    // IEnumerable one with a mangled IL2CPP name.
    private static MethodInfo? TypedGetEnumerator(Type tableType)
    {
        foreach (var method in tableType.GetMethods(AnyInstance))
        {
            if (method.Name == "GetEnumerator" && method.GetParameters().Length == 0) return method;
        }

        return null;
    }
}
