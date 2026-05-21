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

namespace MonstersMap;

public class Plugin : IDalamudPlugin {
    public string Name => "MonstersMap";

    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
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
    private List<(string Name, Vector3 Position)> foundMonsters = new();
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
                var distance = Vector3.Distance(Plugin.ObjectTable.LocalPlayer?.Position ?? Vector3.Zero, monster.Position);
                
                string label = $"{monster.Name} (Distance: {distance:F2}y)";
                
                if (ImGui.Selectable(label, selectedMonsterIndex == i)) {
                    selectedMonsterIndex = i;
                }
            }
            
            ImGui.EndChild();

            ImGui.Spacing();

            if (selectedMonsterIndex >= 0 && selectedMonsterIndex < foundMonsters.Count) {
                var selected = foundMonsters[selectedMonsterIndex];
                ImGui.TextWrapped($"Selected: {selected.Name}");
                ImGui.Text($"Position: X={selected.Position.X:F2} Y={selected.Position.Y:F2} Z={selected.Position.Z:F2}");

                if (ImGui.Button("Flag Location", new Vector2(150, 0))) {
                    PlaceFlag(selected.Name, selected.Position);
                }

                ImGui.SameLine();

                if (ImGui.Button("Clear Flag", new Vector2(150, 0))) {
                    ClearFlag();
                }
            }
        } else if (!string.IsNullOrEmpty(lastSearchedMonster)) {
            ImGui.TextWrapped($"No monsters found matching '{lastSearchedMonster}' in current area");
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

        var searchTerms = NormalizeSearchText(lastSearchedMonster)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        try {
            // Search through all currently spawned combat NPCs.
            foreach (var obj in Plugin.ObjectTable) {
                if (obj == null) continue;

                if (obj is not IBattleNpc || obj.ObjectKind != ObjectKind.BattleNpc) {
                    continue;
                }

                var name = NormalizeSearchText(obj.Name?.TextValue ?? string.Empty);
                
                // Match each word from the search, which makes two-part names more reliable.
                if (searchTerms.All(term => name.Contains(term, StringComparison.OrdinalIgnoreCase))) {
                    foundMonsters.Add((obj.Name?.TextValue ?? string.Empty, obj.Position));
                    Plugin.Log.Information($"Found monster: {obj.Name?.TextValue ?? string.Empty} at {obj.Position}");
                }
            }

            if (foundMonsters.Count == 0) {
                Plugin.Log.Warning($"No monsters found matching '{lastSearchedMonster}' among currently spawned monsters");
            } else {
                Plugin.Log.Information($"Found {foundMonsters.Count} monster(s) matching '{lastSearchedMonster}'");
            }
        } catch (Exception ex) {
            Plugin.Log.Error($"Error searching for monster: {ex.Message}");
        }
    }

    private static string NormalizeSearchText(string text) {
        return string.Join(
            ' ',
            text
                .Trim()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(part => part.Normalize(NormalizationForm.FormKC)));
    }

    private void PlaceFlag(string monsterName, Vector3 position) {
        try {
            // Store the flag information (this is CLIENT-SIDE ONLY)
            Plugin.CurrentFlag = new FlaggedMonster {
                Name = monsterName,
                Position = position,
                CurrentZoneId = Plugin.ClientState.TerritoryType
            };

            Plugin.Log.Information($"Flag placed for {monsterName} at {position}");

            // Try to place a waymark on the map using ImGui (client-side)
            // The flag will be visible in the map overlay
            var mapCoordinates = ConvertWorldToMapCoordinates(position);
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
}
