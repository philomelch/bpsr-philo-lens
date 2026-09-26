using System.Collections.Generic;
using Stellar.PhiloLens.Domain;

namespace Stellar.PhiloLens.Adapters.GameTables;

/// <summary>Season-talent names: the season system (called "Deep Slumber" in season 3) and its boards. A
/// board ("template") is a tree of nodes; each node's effect grants a buff. The index links every buff to the
/// board(s) it can come from, for the running season only, and <see cref="SeasonTalentBoards"/> picks the
/// boards a player runs from the buffs they carry. The index is rebuilt whenever the season changes.</summary>
internal sealed partial class GameTableNames
{
    private const string BoardTable = "Bokura.SeasonTalentTemplateTableBase";
    private const string NodeTable = "Bokura.SeasonTalentTreeTableBase";
    private const string NodeEffectTable = "Bokura.SeasonTalentEffectOrdinaryTableBase";
    private const string FeatureTable = "Bokura.FunctionTableBase";

    // Effect entries are [effectType, buffId, …]; type 3 grants a buff.
    private const int GrantBuffEffect = 3;

    private SeasonTalentIndex? _seasonTalent;

    public string? SeasonTalentTitle => _seasonTalent?.Title;

    // Rebuilt when first needed, when the player's own season data arrives, and when a new season starts.
    // Returns true when it was rebuilt.
    private bool RefreshSeasonTalent()
    {
        var season = CurrentSeasonId();
        if (_seasonTalent is not null && _seasonTalent.Season == season) return false;

        _seasonTalent = LoadSeasonTalentIndex(season);
        return true;
    }

    public IReadOnlyList<string> SeasonTalentBoardNames(IReadOnlyList<int> buffIds)
    {
        if (_seasonTalent is null) return System.Array.Empty<string>();

        var names = new List<string>();
        foreach (var boardId in SeasonTalentBoards.Active(buffIds, _seasonTalent.BoardsByBuff))
        {
            if (_seasonTalent.Boards.TryGetValue(boardId, out var board)) names.Add(board.Name);
        }

        return names;
    }

    private SeasonTalentIndex LoadSeasonTalentIndex(int season)
    {
        var boards = LoadBoards(season);
        var boardsByBuff = LoadBoardsByBuff(LoadBoardsByNodeGroup(boards));
        var title = FeatureName(boards);

        var boardNames = new List<string>(boards.Count);
        foreach (var board in boards.Values) boardNames.Add(board.Name);
        _log.Info($"{LogTag} Season talent '{title ?? "?"}': season {season}, {boards.Count} boards " +
            $"[{string.Join(", ", boardNames)}], {boardsByBuff.Count} buffs indexed");
        return new SeasonTalentIndex(season, title, boards, boardsByBuff);
    }

    // Board id → board, for the running season only (every board if the season is unknown).
    private Dictionary<int, Board> LoadBoards(int season)
    {
        var boards = new Dictionary<int, Board>();
        var table = _tables.GetTable(BoardTable);
        if (table is null) return boards;

        foreach (var row in GameTableReader.Rows(table))
        {
            var name = GameTableReader.ReadString(row, "TemplateName");
            var boardSeason = GameTableReader.ReadInt(row, "BelongSeasonId");
            if (name.Length == 0 || (season != 0 && boardSeason != season)) continue;
            boards[GameTableReader.ReadInt(row, "Id")] = new Board(name, GameTableReader.ReadInt(row, "BelongFunction"));
        }

        return boards;
    }

    // The game names each of its features (menus) in the function table; the boards say which feature they
    // belong to, so that feature's name is what the game calls the season-talent system this season.
    private string? FeatureName(Dictionary<int, Board> boards)
    {
        var featureId = 0;
        foreach (var board in boards.Values)
        {
            if (board.FeatureId != 0) { featureId = board.FeatureId; break; }
        }

        var table = featureId == 0 ? null : _tables.GetTable(FeatureTable);
        var row = table is null ? null : GameTableReader.GetRow(table, featureId);
        var name = row is null ? string.Empty : GameTableReader.ReadString(row, "Name");
        return name.Length == 0 ? null : name;
    }

    // Node group → the boards whose trees contain it.
    private Dictionary<int, HashSet<int>> LoadBoardsByNodeGroup(Dictionary<int, Board> boards)
    {
        var boardsByGroup = new Dictionary<int, HashSet<int>>();
        var table = _tables.GetTable(NodeTable);
        if (table is null) return boardsByGroup;

        foreach (var row in GameTableReader.Rows(table))
        {
            var boardId = GameTableReader.ReadInt(row, "TemplateId");
            if (!boards.ContainsKey(boardId)) continue;

            var groupId = GameTableReader.ReadInt(row, "GroupId");
            if (!boardsByGroup.TryGetValue(groupId, out var groupBoards)) boardsByGroup[groupId] = groupBoards = new HashSet<int>();
            groupBoards.Add(boardId);
        }

        return boardsByGroup;
    }

    // Buff → the boards whose nodes grant it (through the node's group).
    private Dictionary<int, IReadOnlyList<int>> LoadBoardsByBuff(Dictionary<int, HashSet<int>> boardsByGroup)
    {
        var boardSets = new Dictionary<int, HashSet<int>>();
        var table = _tables.GetTable(NodeEffectTable);
        if (table is not null)
        {
            foreach (var row in GameTableReader.Rows(table))
            {
                if (!boardsByGroup.TryGetValue(GameTableReader.ReadInt(row, "GroupId"), out var boards)) continue;
                foreach (var entry in GameTableReader.ReadIntRows(row, "Effect"))
                {
                    if (entry.Length < 2 || entry[0] != GrantBuffEffect) continue;
                    if (!boardSets.TryGetValue(entry[1], out var set)) boardSets[entry[1]] = set = new HashSet<int>();
                    set.UnionWith(boards);
                }
            }
        }

        var boardsByBuff = new Dictionary<int, IReadOnlyList<int>>(boardSets.Count);
        foreach (var (buffId, boards) in boardSets) boardsByBuff[buffId] = new List<int>(boards);
        return boardsByBuff;
    }

    // The latest season the local player has a season-talent level in, i.e. the running season; 0 if unknown.
    private int CurrentSeasonId()
    {
        var latest = 0;
        if (_deepSlumberState.GetState() is not { } state) return latest;
        foreach (var seasonLevel in state.SeasonLevels)
        {
            if (seasonLevel.Length > 0 && seasonLevel[0] > latest) latest = seasonLevel[0];
        }

        return latest;
    }

    private readonly record struct Board(string Name, int FeatureId);

    private sealed record SeasonTalentIndex(
        int Season,
        string? Title,
        IReadOnlyDictionary<int, Board> Boards,
        IReadOnlyDictionary<int, IReadOnlyList<int>> BoardsByBuff);
}
