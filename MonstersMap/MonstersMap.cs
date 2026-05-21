using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects;
using Dalamud.Game.ClientState.Objects.Enums;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.IoC;
using Dalamud.Game.Text.SeStringHandling;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Reflection;
using Lumina.Excel.Sheets;

namespace MonstersMap;

public class Plugin : IDalamudPlugin {
    public string Name => "MonstersMap";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;

    private readonly WindowSystem ws = new("MonstersMap");
    private readonly MonstersMapWindow window = new();
    public static FlaggedMonster? CurrentFlag { get; set; }

    public Plugin() {
        try {
            ws.AddWindow(window);
            
            CommandManager.AddHandler("/monsters", new Dalamud.Game.Command.CommandInfo(OnMonsterSearchCommand) {
                HelpMessage = "Open the monster search window"
            });

            PluginInterface.UiBuilder.Draw += Draw;
            PluginInterface.UiBuilder.OpenMainUi += OnOpenMainUI;
            Log.Information("MonstersMap plugin initialized successfully");
        } catch (Exception ex) {
            Log.Error($"Error initializing MonstersMap: {ex.Message}");
        }
    }

    private void OnOpenMainUI() {
        window.IsOpen = true;
    }

    private void OnMonsterSearchCommand(string command, string args) {
        window.IsOpen = !window.IsOpen;
    }

    private void Draw() {
        ws.Draw();
    }

    public void Dispose() {
        try {
            ws.RemoveAllWindows();
            PluginInterface.UiBuilder.Draw -= Draw;
            PluginInterface.UiBuilder.OpenMainUi -= OnOpenMainUI;
            CommandManager.RemoveHandler("/monsters");
        } catch (Exception ex) {
            Log.Error($"Error disposing MonstersMap: {ex.Message}");
        }
    }
}

public struct FlaggedMonster {
    public string Name { get; set; }
    public Vector3 Position { get; set; }
    public uint CurrentZoneId { get; set; }
}

public class MonstersMapWindow : Window {
    private string monsterSearchInput = string.Empty;
    private string lastSearchedMonster = string.Empty;
    private List<MonsterLocationResult> foundMonsters = new();
    private int selectedMonsterIndex = -1;

    public MonstersMapWindow() : base("Monster Search", ImGuiWindowFlags.AlwaysAutoResize) { }

    public override void Draw() {
        ImGui.Text("Search for a monster:");
        ImGui.SetNextItemWidth(250);
        
        if (ImGui.InputText("##monsterInput", ref monsterSearchInput, 100, ImGuiInputTextFlags.EnterReturnsTrue)) {
            SearchMonster();
        }

        ImGui.SameLine();
        
        if (ImGui.Button("Search##monsterSearch", new Vector2(80, 0))) {
            SearchMonster();
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // Display found monsters
        if (foundMonsters.Count > 0) {
            ImGui.Text($"Found {foundMonsters.Count} monster(s):");
            ImGui.BeginChild("##monsterList", new Vector2(350, 150), true);
            
            for (int i = 0; i < foundMonsters.Count; i++) {
                var monster = foundMonsters[i];
                var label = FormatMonsterLabel(monster);
                
                if (ImGui.Selectable(label, selectedMonsterIndex == i)) {
                    selectedMonsterIndex = i;
                }
            }
            
            ImGui.EndChild();

            ImGui.Spacing();

            if (selectedMonsterIndex >= 0 && selectedMonsterIndex < foundMonsters.Count) {
                var selected = foundMonsters[selectedMonsterIndex];
                ImGui.TextWrapped($"Selected: {selected.Name}");
                ImGui.Text($"Zone: {selected.TerritoryName} ({selected.TerritoryType})");
                ImGui.Text($"Position: X={selected.Position.X:F2} Y={selected.Position.Y:F2} Z={selected.Position.Z:F2}");

                if (ImGui.Button("Flag Location", new Vector2(150, 0))) {
                    PlaceFlag(selected);
                }

                ImGui.SameLine();

                if (ImGui.Button("Clear Flag", new Vector2(150, 0))) {
                    ClearFlag();
                }
            }
        } else if (!string.IsNullOrEmpty(lastSearchedMonster)) {
            ImGui.TextWrapped($"No monsters found matching '{lastSearchedMonster}'");
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        if (Plugin.CurrentFlag.HasValue) {
            var flag = Plugin.CurrentFlag.Value;
            ImGui.Text($"Current Flag: {flag.Name}");
            ImGui.Text($"Zone: {flag.CurrentZoneId}");
            ImGui.TextWrapped($"Position: X={flag.Position.X:F2} Y={flag.Position.Y:F2} Z={flag.Position.Z:F2}");
        }
    }

    private void SearchMonster() {
        if (string.IsNullOrWhiteSpace(monsterSearchInput)) {
            Plugin.Log.Warning("Monster name is empty");
            return;
        }

        foundMonsters.Clear();
        selectedMonsterIndex = -1;
        lastSearchedMonster = monsterSearchInput.Trim();

        try {
            var results = MonsterDiscovery.Search(
                Plugin.DataManager,
                Plugin.ObjectTable,
                Plugin.ClientState.TerritoryType,
                lastSearchedMonster);

            foundMonsters.AddRange(results);

            foreach (var monster in results) {
                Plugin.Log.Information($"Found monster: {monster.Name} in {monster.TerritoryName} at {monster.Position}");
            }

            if (foundMonsters.Count == 0) {
                Plugin.Log.Warning($"No monsters found matching '{lastSearchedMonster}'");
            } else {
                Plugin.Log.Information($"Found {foundMonsters.Count} monster(s) matching '{lastSearchedMonster}'");
            }
        } catch (Exception ex) {
            Plugin.Log.Error($"Error searching for monster: {ex.Message}");
        }
    }

    private void PlaceFlag(MonsterLocationResult monster) {
        try {
            // Store the flag information (this is CLIENT-SIDE ONLY)
            Plugin.CurrentFlag = new FlaggedMonster {
                Name = monster.Name,
                Position = monster.Position,
                CurrentZoneId = monster.TerritoryType
            };

            Plugin.Log.Information($"Flag stored for {monster.Name} in territory {monster.TerritoryType} at {monster.Position}");

            // Try to place a waymark on the map using ImGui (client-side)
            // The flag will be visible in the map overlay
            var mapCoordinates = ConvertWorldToMapCoordinates(monster.Position);
            Plugin.Log.Information($"Map coordinates: X={mapCoordinates.X:F2} Y={mapCoordinates.Y:F2}");
        } catch (Exception ex) {
            Plugin.Log.Error($"Error placing flag: {ex.Message}");
        }
    }

    private void ClearFlag() {
        Plugin.CurrentFlag = null;
        foundMonsters.Clear();
        selectedMonsterIndex = -1;
        Plugin.Log.Information("Flag cleared");
    }

    /// <summary>
    /// Converts world coordinates to map coordinates
    /// This is used for displaying positions on the map UI
    /// </summary>
    private Vector2 ConvertWorldToMapCoordinates(Vector3 worldPosition) {
        // FFXIV map coordinate conversion (client-side calculation)
        // Each zone has different scale factors
        const float scale = 50.0f; // Default scale
        
        var mapX = (worldPosition.X - 0) / scale;
        var mapY = (worldPosition.Z - 0) / scale;
        
        return new Vector2(mapX, mapY);
    }

    private string FormatMonsterLabel(MonsterLocationResult monster) {
        if (monster.TerritoryType == Plugin.ClientState.TerritoryType) {
            var playerPosition = Plugin.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero;
            var distance = Vector3.Distance(playerPosition, monster.Position);
            return $"{monster.Name} - {distance:F2}y";
        }

        return $"{monster.Name} - {monster.TerritoryName}";
    }
}

public sealed record MonsterLocationResult(
    string Name,
    Vector3 Position,
    uint TerritoryType,
    string TerritoryName);

internal sealed record MonsterLocationCandidate(
    string Name,
    Vector3 Position,
    uint TerritoryType,
    string TerritoryName)
{
    public MonsterSearchCandidate SearchCandidate => new(this.Name, this.Position);
}

internal static class MonsterDiscovery {
    private static IReadOnlyList<MonsterLocationCandidate>? cachedGlobalCandidates;
    private static readonly object cacheLock = new();

    public static IReadOnlyList<MonsterLocationResult> Search(
        IDataManager dataManager,
        IObjectTable objectTable,
        uint currentTerritoryType,
        string searchText) {
        var candidates = GetCandidates(dataManager, objectTable, currentTerritoryType);
        if (candidates.Count == 0) {
            return Array.Empty<MonsterLocationResult>();
        }

        var searchCandidates = candidates.Select(candidate => candidate.SearchCandidate).ToArray();
        var results = MonsterSearch.FindMatches(searchCandidates, searchText);
        var matchedResults = new List<MonsterLocationResult>(results.Count);

        foreach (var result in results) {
            for (var index = 0; index < candidates.Count; index++) {
                var candidate = candidates[index];
                if (!string.Equals(candidate.Name, result.Name, StringComparison.Ordinal) || candidate.Position != result.Position) {
                    continue;
                }

                matchedResults.Add(new MonsterLocationResult(
                    candidate.Name,
                    candidate.Position,
                    candidate.TerritoryType,
                    candidate.TerritoryName));
                break;
            }
        }

        return matchedResults;
    }

    private static IReadOnlyList<MonsterLocationCandidate> GetCandidates(
        IDataManager dataManager,
        IObjectTable objectTable,
        uint currentTerritoryType) {
        var currentTerritoryName = ResolveTerritoryName(dataManager, currentTerritoryType);

        var currentSpawnCandidates = objectTable
            .Where(obj => obj is IBattleNpc && obj.ObjectKind == ObjectKind.BattleNpc)
            .Select(obj => new MonsterLocationCandidate(
                obj.Name?.TextValue ?? string.Empty,
                obj.Position,
                currentTerritoryType,
                currentTerritoryName))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.Name));

        var globalCandidates = GetGlobalCandidates(dataManager);

        return globalCandidates
            .Concat(currentSpawnCandidates)
            .DistinctBy(candidate => (candidate.Name, candidate.Position, candidate.TerritoryType))
            .ToArray();
    }

    private static IReadOnlyList<MonsterLocationCandidate> GetGlobalCandidates(IDataManager dataManager) {
        lock (cacheLock) {
            if (cachedGlobalCandidates is not null) {
                return cachedGlobalCandidates;
            }

            var sheet = dataManager.GameData.GetExcelSheet<Level>();
            if (sheet is null) {
                cachedGlobalCandidates = Array.Empty<MonsterLocationCandidate>();
                return cachedGlobalCandidates;
            }

            var candidates = new List<MonsterLocationCandidate>();

            foreach (var levelRow in sheet) {
                if (!levelRow.Map.IsValid) {
                    continue;
                }

                var rowObject = (object)levelRow;

                if (!TryGetMonsterName(rowObject, out var monsterName)) {
                    continue;
                }

                if (!TryGetPosition(rowObject, out var position)) {
                    continue;
                }

                if (!TryGetTerritoryType(rowObject, out var territoryType)) {
                    continue;
                }

                var territoryName = ResolveTerritoryName(dataManager, territoryType);
                candidates.Add(new MonsterLocationCandidate(monsterName, position, territoryType, territoryName));
            }

            cachedGlobalCandidates = candidates;
            return cachedGlobalCandidates;
        }
    }

    private static string ResolveTerritoryName(IDataManager dataManager, uint territoryType) {
        var sheet = dataManager.GameData.GetExcelSheet<TerritoryType>();
        if (sheet is null || !sheet.TryGetRow(territoryType, out var territoryRow)) {
            return $"Territory {territoryType}";
        }

        var placeName = GetPropertyValue(territoryRow, "PlaceName", "PlaceNameZone", "PlaceNameRegion");
        var displayText = ExtractDisplayText(placeName);
        return string.IsNullOrWhiteSpace(displayText) ? $"Territory {territoryType}" : displayText;
    }

    private static bool TryGetMonsterName(object row, out string monsterName) {
        var rawName = GetPropertyValue(row, "BNpcName", "BNpcBase", "Name", "Resident", "MonsterName");
        monsterName = ExtractDisplayText(rawName) ?? string.Empty;
        return !string.IsNullOrWhiteSpace(monsterName);
    }

    private static bool TryGetPosition(object row, out Vector3 position) {
        if (TryGetFloatProperty(row, out var x, "X", "PosX", "XPos") &&
            TryGetFloatProperty(row, out var y, "Y", "PosY", "YPos") &&
            TryGetFloatProperty(row, out var z, "Z", "PosZ", "ZPos")) {
            position = new Vector3(x, y, z);
            return true;
        }

        position = Vector3.Zero;
        return false;
    }

    private static bool TryGetTerritoryType(object row, out uint territoryType) {
        var rawTerritory = GetPropertyValue(row, "TerritoryType", "Territory", "TerritoryTypeId");
        if (TryGetUInt32(rawTerritory, out territoryType)) {
            return true;
        }

        territoryType = 0;
        return false;
    }

    private static bool TryGetFloatProperty(object source, out float value, params string[] propertyNames) {
        var rawValue = GetPropertyValue(source, propertyNames);
        if (rawValue is null) {
            value = 0;
            return false;
        }

        try {
            value = Convert.ToSingle(rawValue);
            return true;
        } catch {
            value = 0;
            return false;
        }
    }

    private static bool TryGetUInt32(object? rawValue, out uint value) {
        if (rawValue is null) {
            value = 0;
            return false;
        }

        var extracted = ExtractDisplayText(rawValue);
        if (rawValue is uint directValue) {
            value = directValue;
            return true;
        }

        if (rawValue is int intValue && intValue >= 0) {
            value = (uint)intValue;
            return true;
        }

        if (rawValue is long longValue && longValue >= 0) {
            value = (uint)longValue;
            return true;
        }

        var rowIdProperty = rawValue.GetType().GetProperty("RowId", BindingFlags.Instance | BindingFlags.Public);
        if (rowIdProperty?.GetValue(rawValue) is uint rowId) {
            value = rowId;
            return true;
        }

        if (uint.TryParse(extracted, out value)) {
            return true;
        }

        value = 0;
        return false;
    }

    private static object? GetPropertyValue(object source, params string[] propertyNames) {
        var type = source.GetType();
        foreach (var propertyName in propertyNames) {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null) {
                continue;
            }

            var value = property.GetValue(source);
            if (value is not null) {
                return value;
            }
        }

        return null;
    }

    private static string? ExtractDisplayText(object? value) {
        if (value is null) {
            return null;
        }

        if (value is string text) {
            return text;
        }

        var type = value.GetType();

        var extractTextMethod = type.GetMethod("ExtractText", BindingFlags.Instance | BindingFlags.Public, Type.EmptyTypes);
        if (extractTextMethod?.ReturnType == typeof(string)) {
            return extractTextMethod.Invoke(value, null) as string;
        }

        foreach (var propertyName in new[] { "TextValue", "Name", "Singular", "Value", "DisplayText" }) {
            var property = type.GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public);
            if (property is null) {
                continue;
            }

            var propertyValue = property.GetValue(value);
            var extracted = ExtractDisplayText(propertyValue);
            if (!string.IsNullOrWhiteSpace(extracted)) {
                return extracted;
            }
        }

        var valueNullableProperty = type.GetProperty("ValueNullable", BindingFlags.Instance | BindingFlags.Public);
        if (valueNullableProperty is not null) {
            return ExtractDisplayText(valueNullableProperty.GetValue(value));
        }

        return value.ToString();
    }
}
