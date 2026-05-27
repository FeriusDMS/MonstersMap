using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.IoC;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Lumina.Excel.Sheets;

namespace MonstersMap;

public class Plugin : IDalamudPlugin
{
    public string Name => "MonstersMap";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;

    private readonly WindowSystem _ws = new("MonstersMap");
    private readonly MonstersMapWindow _window;

    public Plugin()
    {
        try
        {
            _window = new MonstersMapWindow();
            _ws.AddWindow(_window);

            CommandManager.AddHandler("/monsters", new Dalamud.Game.Command.CommandInfo(OnCommand)
            {
                HelpMessage = "Open the monster search window (/monsters)"
            });

            PluginInterface.UiBuilder.Draw += Draw;
            PluginInterface.UiBuilder.OpenMainUi += OpenMainUi;
            Log.Information("[MonstersMap] Plugin initialized.");
        }
        catch (Exception ex)
        {
            Log.Error($"[MonstersMap] Init error: {ex}");
            throw;
        }
    }

    private void OpenMainUi() => _window.IsOpen = true;
    private void OnCommand(string cmd, string args) => _window.IsOpen = !_window.IsOpen;
    private void Draw() => _ws.Draw();

    public void Dispose()
    {
        _ws.RemoveAllWindows();
        PluginInterface.UiBuilder.Draw -= Draw;
        PluginInterface.UiBuilder.OpenMainUi -= OpenMainUi;
        CommandManager.RemoveHandler("/monsters");
    }
}

public sealed record MonsterLocationResult(
    string Name,
    Vector3 Position,
    uint TerritoryType,
    string TerritoryName,
    uint MapId);

internal static class MonsterDiscovery
{
    private static string? _cachedLang;
    private static List<MonsterLocationResult>? _globalCache;
    private static readonly object Lock = new();

    public static IReadOnlyList<MonsterLocationResult> Search(
        IDataManager dataManager,
        IObjectTable objectTable,
        uint currentTerritoryType,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<MonsterLocationResult>();

        var live = GetLiveMonsters(objectTable, dataManager, currentTerritoryType);

        var global = GetGlobalCache(dataManager);

        var needle = query.Trim();
        return live
            .Concat(global)
            .Where(r => r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(r => (r.Name, r.TerritoryType))
            .OrderBy(r => r.Name)
            .ToList();
    }

    private static List<MonsterLocationResult> GetGlobalCache(IDataManager dataManager)
    {
        lock (Lock)
        {
            var currentLanguage = dataManager.Language.ToString();

            if (_globalCache is not null && _cachedLang == currentLanguage)
                return _globalCache;

            _cachedLang = currentLanguage;
            _globalCache = BuildGlobalCache(dataManager);
            return _globalCache;
        }
    }

    private static List<MonsterLocationResult> BuildGlobalCache(IDataManager dataManager)
    {
        Plugin.Log.Information($"[MonstersMap] Building global cache (language={dataManager.Language})…");

        var levelSheet = dataManager.GetExcelSheet<Level>(dataManager.Language);
        if (levelSheet is null)
        {
            Plugin.Log.Error("[MonstersMap] Level sheet could not be loaded!");
            return new List<MonsterLocationResult>();
        }

        var bNpcNameSheet = dataManager.GetExcelSheet<BNpcName>(dataManager.Language);
        if (bNpcNameSheet is null)
        {
            Plugin.Log.Error("[MonstersMap] BNpcName sheet could not be loaded!");
            return new List<MonsterLocationResult>();
        }

        var results = new List<MonsterLocationResult>();
        var seen = new HashSet<(uint BNpcNameId, uint TerritoryId)>();

        foreach (var row in levelSheet)
        {
            if (row.Type != 9)
                continue;

            uint bNpcNameId = row.Object.RowId;
            if (bNpcNameId == 0)
                continue;

            if (!bNpcNameSheet.TryGetRow(bNpcNameId, out var bNpcName))
                continue;

            var name = bNpcName.Singular.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            if (!row.Territory.IsValid)
                continue;

            var terrRow = row.Territory.Value;
            uint territoryId = terrRow.RowId;
            if (territoryId == 0)
                continue;

            var key = (bNpcNameId, territoryId);
            if (!seen.Add(key))
                continue;

            string territoryName = "Unknown";
            if (terrRow.PlaceName.IsValid)
            {
                var n = terrRow.PlaceName.Value.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(n))
                    territoryName = n;
            }

            uint mapId = terrRow.Map.IsValid ? terrRow.Map.Value.RowId : 0;
            var position = new Vector3(row.X, row.Y, row.Z);
            results.Add(new MonsterLocationResult(name, position, territoryId, territoryName, mapId));
        }

        Plugin.Log.Information($"[MonstersMap] Global cache: {results.Count} unique entries.");
        return results;
    }

    private static IEnumerable<MonsterLocationResult> GetLiveMonsters(
        IObjectTable objectTable,
        IDataManager dataManager,
        uint currentTerritoryType)
    {
        var territoryName = ResolveTerritoryName(dataManager, currentTerritoryType);
        var mapId = ResolveMapId(dataManager, currentTerritoryType);

        return objectTable
            .Where(obj => obj is IBattleNpc && obj.ObjectKind == ObjectKind.BattleNpc)
            .Select(obj =>
            {
                var name = obj.Name?.TextValue ?? string.Empty;
                return new MonsterLocationResult(name, obj.Position, currentTerritoryType, territoryName, mapId);
            })
            .Where(r => !string.IsNullOrWhiteSpace(r.Name));
    }

    private static string ResolveTerritoryName(IDataManager dataManager, uint territoryId)
    {
        var sheet = dataManager.GetExcelSheet<TerritoryType>(dataManager.Language);
        if (sheet is null || !sheet.TryGetRow(territoryId, out var row))
            return $"Zone {territoryId}";

        if (row.PlaceName.IsValid)
        {
            var name = row.PlaceName.Value.Name.ExtractText();
            if (!string.IsNullOrWhiteSpace(name))
                return name;
        }

        return $"Zone {territoryId}";
    }

    private static uint ResolveMapId(IDataManager dataManager, uint territoryId)
    {
        var sheet = dataManager.GetExcelSheet<TerritoryType>();
        if (sheet is null || !sheet.TryGetRow(territoryId, out var row))
            return 0;

        return row.Map.IsValid ? row.Map.Value.RowId : 0;
    }
}

public class MonstersMapWindow : Window
{
    private string _input = string.Empty;
    private string _lastQuery = string.Empty;
    private List<MonsterLocationResult> _results = new();
    private int _selected = -1;
    private string _status = string.Empty;

    public MonstersMapWindow()
        : base("Monster Search##MonstersMapWin", ImGuiWindowFlags.AlwaysAutoResize) { }

    public override void Draw()
    {
        ImGui.Text("Search for a monster:");
        ImGui.SetNextItemWidth(250);

        if (ImGui.InputText("##input", ref _input, 100, ImGuiInputTextFlags.EnterReturnsTrue))
            Search();

        ImGui.SameLine();

        if (ImGui.Button("Search##go", new Vector2(80, 0)))
            Search();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (_results.Count > 0)
        {
            ImGui.Text($"Found {_results.Count} result(s) matching '{_lastQuery}':");
            ImGui.BeginChild("##list", new Vector2(390, 160), true);

            for (int i = 0; i < _results.Count; i++)
            {
                var m = _results[i];
                var label = MakeLabel(m, i);

                if (ImGui.Selectable(label, _selected == i))
                    _selected = i;
            }

            ImGui.EndChild();
            ImGui.Spacing();

            if (_selected >= 0 && _selected < _results.Count)
            {
                var sel = _results[_selected];
                ImGui.TextWrapped($"Name: {sel.Name}");
                ImGui.Text($"Zone: {sel.TerritoryName}  (id={sel.TerritoryType})");
                ImGui.Text($"Position: X={sel.Position.X:F1}  Y={sel.Position.Y:F1}  Z={sel.Position.Z:F1}");
                ImGui.Spacing();

                if (ImGui.Button("Flag on Map##flag", new Vector2(160, 0)))
                    PlaceFlag(sel);

                ImGui.SameLine();

                if (ImGui.Button("Clear##clr", new Vector2(80, 0)))
                    Reset();
            }
        }
        else if (!string.IsNullOrEmpty(_lastQuery))
        {
            ImGui.TextWrapped($"No monsters found matching '{_lastQuery}'");
        }

        if (!string.IsNullOrEmpty(_status))
        {
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped(_status);
        }
    }

    private void Search()
    {
        _results.Clear();
        _selected = -1;
        _status = string.Empty;
        _lastQuery = _input.Trim();

        if (string.IsNullOrWhiteSpace(_lastQuery))
            return;

        try
        {
            var found = MonsterDiscovery.Search(
                Plugin.DataManager,
                Plugin.ObjectTable,
                Plugin.ClientState.TerritoryType,
                _lastQuery);

            _results.AddRange(found);
            Plugin.Log.Information($"[MonstersMap] '{_lastQuery}' → {_results.Count} result(s)");

            if (_results.Count == 0)
                Plugin.Log.Warning($"[MonstersMap] No results for '{_lastQuery}'");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[MonstersMap] Search error: {ex}");
            _status = $"Search error: {ex.Message}";
        }
    }

    private void PlaceFlag(MonsterLocationResult m)
    {
        try
        {
            if (m.MapId == 0)
            {
                _status = "Cannot flag: Map ID is unknown for this monster.";
                Plugin.Log.Warning($"[MonstersMap] MapId=0 for {m.Name} in territory {m.TerritoryType}");
                return;
            }

            var (mapX, mapY) = WorldToMapCoords(m.Position, m.MapId);

            Plugin.Log.Information(
                $"[MonstersMap] Flag → {m.Name} | territory={m.TerritoryType} map={m.MapId} " +
                $"| world({m.Position.X:F1},{m.Position.Z:F1}) → map({mapX:F2},{mapY:F2})");

            Plugin.GameGui.OpenMapWithMapLink(new MapLinkPayload(
                m.TerritoryType,
                m.MapId,
                mapX,
                mapY));

            _status = $"✓ Flag placed: {m.Name} in {m.TerritoryName} ({mapX:F1}, {mapY:F1})";
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[MonstersMap] PlaceFlag error: {ex}");
            _status = $"Flag error: {ex.Message}";
        }
    }
    private static (float X, float Y) WorldToMapCoords(Vector3 worldPos, uint mapId)
    {
        var mapSheet = Plugin.DataManager.GetExcelSheet<Map>();
        if (mapSheet is not null && mapSheet.TryGetRow(mapId, out var mapRow))
        {
            var sf = mapRow.SizeFactor;
            var ox = mapRow.OffsetX;
            var oy = mapRow.OffsetY;

            if (sf > 0)
            {
                float x = ((worldPos.X + ox) * 0.02f * sf) / 100f + 1f;
                float y = ((worldPos.Z + oy) * 0.02f * sf) / 100f + 1f;
                return (x, y);
            }
        }

        return (worldPos.X / 50f + 21f, worldPos.Z / 50f + 21f);
    }

    private string MakeLabel(MonsterLocationResult m, int idx)
    {
        if (m.TerritoryType == Plugin.ClientState.TerritoryType)
        {
            var playerPos = Plugin.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
            float dist = Vector3.Distance(playerPos, m.Position);
            return $"{m.Name}  [{dist:F0}y — current zone]##{idx}";
        }

        return $"{m.Name}  [{m.TerritoryName}]##{idx}";
    }

    private void Reset()
    {
        _results.Clear();
        _selected = -1;
        _lastQuery = string.Empty;
        _status = string.Empty;
    }
}