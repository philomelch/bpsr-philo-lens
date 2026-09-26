using System.Collections.Generic;
using Stellar.Abstractions.Services;
using Stellar.PhiloLens.Application;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters.GameTables;

/// <summary>Names Battle Imagines and season-talent boards from the game's own tables (read through
/// <see cref="GameTableReader"/>) plus the typed item names. Each table is read once per session.</summary>
internal sealed partial class GameTableNames : IBuildNameSource
{
    private const string LogTag = "[Stellar.PhiloLens]";
    private const string ImagineTable = "Bokura.SkillAoyiTableBase";

    private readonly GameTableReader _tables;
    private readonly IGameDataInventory _itemData;
    private readonly IGameDataResonance _resonanceData;
    private readonly IDeepSlumber _deepSlumberState;
    private readonly IPluginLog _log;
    private Dictionary<int, string>? _imagineNamesBySkill;

    public int Version { get; private set; }

    public GameTableNames(GameTableReader tables, IGameDataInventory itemData, IGameDataResonance resonanceData,
        IDeepSlumber deepSlumber, IPluginLog log)
    {
        _tables = tables;
        _itemData = itemData;
        _resonanceData = resonanceData;
        _deepSlumberState = deepSlumber;
        _log = log;
    }

    public void Refresh()
    {
        if (_imagineNamesBySkill is null)
        {
            _imagineNamesBySkill = LoadImagineNames();
            Version++;
        }

        if (RefreshSeasonTalent()) Version++;
    }

    public string? ImagineName(int skillId)
    {
        // Loaded by Refresh (only while game reads are safe); until then the skill name stands in.
        return _imagineNamesBySkill is not null && _imagineNamesBySkill.TryGetValue(skillId, out var name)
            ? name
            : _resonanceData.GetImagineForSkill(skillId)?.Name;
    }

    // Players know an Imagine by its item ("Battle Imagine - Tina" → "Tina"), not by its skill or creature:
    // some Imagines have no creature, and some creatures are named differently ("Tina - Resonance").
    private Dictionary<int, string> LoadImagineNames()
    {
        var itemNamesBySkill = new Dictionary<int, string>();
        var table = _tables.GetTable(ImagineTable);
        if (table is not null)
        {
            foreach (var row in GameTableReader.Rows(table))
            {
                var skillId = GameTableReader.ReadInt(row, "Id");
                var itemName = _itemData.GetItem(GameTableReader.ReadInt(row, "AoyiItemId"))?.Name;
                if (skillId != 0 && !string.IsNullOrEmpty(itemName)) itemNamesBySkill[skillId] = itemName;
            }
        }

        var label = ImagineNames.CommonLabel(new List<string>(itemNamesBySkill.Values));
        var names = new Dictionary<int, string>(itemNamesBySkill.Count);
        foreach (var (skillId, itemName) in itemNamesBySkill) names[skillId] = ImagineNames.WithoutLabel(itemName, label);

        _log.Info($"{LogTag} Imagine names: {names.Count} imagines, shared label '{label}'");
        return names;
    }
}
