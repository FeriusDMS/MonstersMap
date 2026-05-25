// ╔══════════════════════════════════════════════════════════════════════════╗
// ║  MonstersMap.cs — version corrigée                                      ║
// ║                                                                          ║
// ║  Corrections majeures :                                                  ║
// ║  1. Lecture correcte du sheet Level via l'API typée Lumina               ║
// ║     (plus de réflexion fragile)                                          ║
// ║  2. Résolution du BNpcName via RowRef<BNpcName>.Value.Singular           ║
// ║  3. Langue du client passée à GetExcelSheet<T>(dataManager.Language)     ║
// ║     → noms localisés (FR / EN / DE / JP)                                ║
// ║  4. Filtre Type == 9 pour ne garder que les BattleNpc (monstres)         ║
// ║  5. Flag placé via GameGui.OpenMapWithMapLink + vraie formule de conv.   ║
// ╚══════════════════════════════════════════════════════════════════════════╝

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

// ─────────────────────────────────────────────────────────────────────────────
//  Plugin entry point
// ─────────────────────────────────────────────────────────────────────────────

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

// ─────────────────────────────────────────────────────────────────────────────
//  Data record
// ─────────────────────────────────────────────────────────────────────────────

public sealed record MonsterLocationResult(
    string Name,
    Vector3 Position,
    uint TerritoryType,
    string TerritoryName,
    uint MapId);

// ─────────────────────────────────────────────────────────────────────────────
//  Monster discovery — lecture correcte des sheets Lumina
// ─────────────────────────────────────────────────────────────────────────────

internal static class MonsterDiscovery
{
    private static Dalamud.ClientLanguage? _cachedLang;
    private static List<MonsterLocationResult>? _globalCache;
    private static readonly object Lock = new();

    /// <summary>
    /// Retourne tous les monstres dont le nom contient <paramref name="query"/>
    /// (insensible à la casse), en fusionnant les données statiques (Level sheet)
    /// et les monstres actuellement spawné dans la zone du joueur.
    /// </summary>
    public static IReadOnlyList<MonsterLocationResult> Search(
        IDataManager dataManager,
        IObjectTable objectTable,
        uint currentTerritoryType,
        string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<MonsterLocationResult>();

        // 1. Monstres spawné en live (position exacte, temps réel)
        var live = GetLiveMonsters(objectTable, dataManager, currentTerritoryType);

        // 2. Base globale depuis les sheets de données du jeu
        var global = GetGlobalCache(dataManager);

        // 3. Fusion + filtre par nom
        var needle = query.Trim();
        return live
            .Concat(global)
            .Where(r => r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase))
            .DistinctBy(r => (r.Name, r.TerritoryType))
            .OrderBy(r => r.Name)
            .ToList();
    }

    // ── Cache global ─────────────────────────────────────────────────────────

    private static List<MonsterLocationResult> GetGlobalCache(IDataManager dataManager)
    {
        lock (Lock)
        {
            // Invalide si la langue du client a changé (changement de langue en cours de jeu)
            if (_globalCache is not null && _cachedLang == dataManager.Language)
                return _globalCache;

            _cachedLang = dataManager.Language;
            _globalCache = BuildGlobalCache(dataManager);
            return _globalCache;
        }
    }

    /// <summary>
    /// Parcourt le sheet <c>Level</c> de Lumina pour extraire tous les BattleNpc
    /// (Type == 9) avec leur nom localisé, position et territoire.
    ///
    /// Pourquoi ça ne fonctionnait pas avant :
    ///  - L'ancien code utilisait la réflexion (GetPropertyValue / ExtractDisplayText)
    ///    pour tenter de lire "BNpcName", "Name", etc. sur le row. Or dans l'API
    ///    Lumina moderne, BNpcName est un <c>RowRef&lt;BNpcName&gt;</c> : une référence
    ///    paresseuse vers une autre sheet. La réflexion retournait l'objet RowRef brut,
    ///    jamais le texte réel — d'où 0 résultat.
    ///  - L'ancien code utilisait <c>dataManager.GameData.GetExcelSheet&lt;Level&gt;()</c>
    ///    sans langue → noms en japonais ou vides selon la config Lumina.
    ///  - Aucun filtre sur Type → on parcourait tout l'univers (décors, objets…).
    /// </summary>
    private static List<MonsterLocationResult> BuildGlobalCache(IDataManager dataManager)
    {
        Plugin.Log.Information($"[MonstersMap] Building global cache (language={dataManager.Language})…");

        // ✅ Utiliser dataManager.GetExcelSheet<T>(language) et non dataManager.GameData.GetExcelSheet<T>()
        //    La version Dalamud (IDataManager) gère correctement la langue et le contexte du plugin.
        var levelSheet = dataManager.GetExcelSheet<Level>(dataManager.Language);

        if (levelSheet is null)
        {
            Plugin.Log.Error("[MonstersMap] Level sheet could not be loaded!");
            return new List<MonsterLocationResult>();
        }

        var results = new List<MonsterLocationResult>();
        var seen = new HashSet<(uint BNpcNameId, uint TerritoryId)>();

        foreach (var row in levelSheet)
        {
            // ✅ Filtre : Type == 9 = BattleNpc uniquement
            //    Type 8 = EventNpc (marchands, PNJ de quête…)
            //    Sans ce filtre on récupère des milliers d'objets de décor sans nom.
            if (row.Type != 9)
                continue;

            // ✅ Résolution correcte : BNpcName est un RowRef<BNpcName>
            //    .IsValid vérifie que la référence pointe vers une ligne existante
            //    .Value donne la ligne BNpcName
            //    .Singular est le SeString du nom → .ExtractText() donne le string localisé
            if (!row.BNpcName.IsValid)
                continue;

            var bNpcNameRow = row.BNpcName.Value;
            if (bNpcNameRow.RowId == 0)
                continue;   // Ligne vide (row 0 = "")

            var name = bNpcNameRow.Singular.ExtractText();
            if (string.IsNullOrWhiteSpace(name))
                continue;

            // Territoire
            if (!row.Territory.IsValid)
                continue;

            var terrRow = row.Territory.Value;
            uint territoryId = terrRow.RowId;
            if (territoryId == 0)
                continue;

            // Dédoublonnage par (BNpcNameId, TerritoryId) pour avoir un seul résultat
            // par type de monstre par zone (pas besoin de lister 50× le même gobelin)
            var key = (bNpcNameRow.RowId, territoryId);
            if (!seen.Add(key))
                continue;

            // Nom de la zone
            string territoryName = "Unknown";
            if (terrRow.PlaceName.IsValid)
            {
                var n = terrRow.PlaceName.Value.Name.ExtractText();
                if (!string.IsNullOrWhiteSpace(n))
                    territoryName = n;
            }

            // Map ID (nécessaire pour OpenMapWithMapLink)
            uint mapId = terrRow.Map.IsValid ? terrRow.Map.Value.RowId : 0;

            // Position monde
            var position = new Vector3(row.X, row.Y, row.Z);

            results.Add(new MonsterLocationResult(name, position, territoryId, territoryName, mapId));
        }

        Plugin.Log.Information($"[MonstersMap] Global cache: {results.Count} unique entries.");
        return results;
    }

    // ── Monstres en vie dans la zone actuelle ────────────────────────────────

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

    // ── Helpers ──────────────────────────────────────────────────────────────

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

// ─────────────────────────────────────────────────────────────────────────────
//  UI — fenêtre ImGui
// ─────────────────────────────────────────────────────────────────────────────

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

        // ── Résultats ──────────────────────────────────────────────────────
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

    // ── Search ───────────────────────────────────────────────────────────────

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

    // ── Flag ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ouvre la carte et place un marqueur via l'API officielle Dalamud
    /// <see cref="IGameGui.OpenMapWithMapLink"/>.
    ///
    /// MapLinkPayload attend des coordonnées carte (typ. 1–42), pas des
    /// coordonnées monde. On utilise la formule reverse-engineered par la
    /// communauté FFXIV (SizeFactor + Offset du sheet Map).
    /// </summary>
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

            // ✅ OpenMapWithMapLink ouvre la carte au bon endroit et place le flag
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

    /// <summary>
    /// Convertit les coordonnées monde FFXIV en coordonnées carte affichées
    /// dans l'UI du jeu.
    ///
    /// Formule standard (reverse-engineered) :
    ///   mapCoord = ((worldCoord + offset) * 0.02 * sizeFactor) / 100 + 1
    ///
    /// SizeFactor et Offset viennent du sheet <c>Map</c> de Lumina.
    /// L'axe Z du monde correspond à l'axe Y de la carte.
    /// </summary>
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

        // Fallback grossier si Map sheet inaccessible
        return (worldPos.X / 50f + 21f, worldPos.Z / 50f + 21f);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

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