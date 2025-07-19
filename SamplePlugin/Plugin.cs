using BSDriverPlugin.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Common.Lua;
using ImGuiNET;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using static Dalamud.Interface.Utility.Raii.ImRaii;
using static FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource.SchedulerResource;

namespace BSDriverPlugin;

public class RoleHints
{

    [JsonPropertyName("en")]
    public string stage_name { get; set; } = "";

    [JsonPropertyName("g")]
    public string general { get; set; } = "";

    [JsonPropertyName("d")]
    public string dps { get; set; } = "";

    [JsonPropertyName("h")]
    public string healer { get; set; } = "";

    [JsonPropertyName("t")]
    public string tank { get; set; } = "";
}

public class MapRoleHints
{
    [JsonPropertyName("en")]
    public string en_name { get; set; } = "";

    [JsonPropertyName("st")]
    public List<RoleHints> stages { get; set; } = [];

    [JsonPropertyName("g")]
    public string general { get; set; } = "";

    [JsonPropertyName("d")]
    public string dps { get; set; } = "";

    [JsonPropertyName("h")]
    public string healer { get; set; } = "";

    [JsonPropertyName("t")]
    public string tank { get; set; } = "";
}

public class TerritoryRoleHints
{
    [JsonPropertyName("en")]
    public string en_name { get; set; } = "";

    [JsonPropertyName("maps")]
    public Dictionary<string, MapRoleHints> maps { get; set; } = new();
}
public enum Role
{
    Tank,
    Healer,
    DPS,
    Unknown
}

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ITextureProvider TextureProvider { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; set; } = null!;

    private Dictionary<string, TerritoryRoleHints>? instances_data;
    private TerritoryRoleHints? current_territory_hint;
    private MapRoleHints? current_map_hint;
    private bool showHintPopup = false;

    private static readonly Dictionary<uint, Role> classjob_to_role = new()
    {
        { 19, Role.Tank }, // Paladin
        { 21, Role.Tank }, // Warrior
        { 32, Role.Tank }, // Dark Knight
        { 37, Role.Tank }, // Gunbreaker

        { 24, Role.Healer }, // White Mage
        { 28, Role.Healer }, // Scholar
        { 33, Role.Healer }, // Astrologian
        { 40, Role.Healer }, // Sage

        { 20, Role.DPS }, // Monk
        { 22, Role.DPS }, // Dragoon
        { 30, Role.DPS }, // Ninja
        { 34, Role.DPS }, // Samurai
        { 39, Role.DPS }, // Reaper
        { 23, Role.DPS }, // Bard
        { 31, Role.DPS }, // Machinist
        { 38, Role.DPS }, // Dancer
        { 25, Role.DPS }, // Black Mage
        { 27, Role.DPS }, // Summoner
        { 35, Role.DPS }, // Red Mage
        { 36, Role.DPS }, // Blue Mage (limited)
    };

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("BSDriverPlugin");
    private ConfigWindow ConfigWindow { get; init; }
    private MainWindow MainWindow { get; init; }

    private bool load_instances_data()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("BSDriverPlugin.Data.instances_data.json.gz") ??
            throw new FileNotFoundException($"Resource not found: {"instances_data.json.gz"}");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, Encoding.UTF8);
        var json_str = reader.ReadToEnd();
        this.instances_data = JsonSerializer.Deserialize<Dictionary<string, TerritoryRoleHints>>(json_str);

        return true;
    }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        this.load_instances_data(); 

        ConfigWindow = new ConfigWindow(this);
        MainWindow = new MainWindow(this);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(MainWindow);

        CommandManager.AddHandler("/pbsdriver", new CommandInfo(OnMainSettingsCmd)
        {
            HelpMessage = "Display main setting menu."
        });

        CommandManager.AddHandler("/pbsdriver-hint", new CommandInfo(OnHintCmd)
        {
            HelpMessage = "Show all hints for the current instance."
        });

        CommandManager.AddHandler("/pbsdriver-now", new CommandInfo(OnImmediateHintCmd)
        {
            HelpMessage = "Print hints for your job in the current instance."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;
        PluginInterface.UiBuilder.Draw += DrawHintUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleMainUI;

        // Add a simple message to the log with level set to information
        // Use /xllog to open the log window in-game
        // Example Output: 00:57:54.959 | INF | [SamplePlugin] ===A cool log message from Sample Plugin===
        Log.Information($"===A cool log message from {PluginInterface.Manifest.Name}===");
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        MainWindow.Dispose();

        CommandManager.RemoveHandler("/pbsdriver");
    }

    private void _loadCurrentMapHints()
    {
        this.current_territory_hint = null;
        this.current_map_hint = null;

        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null || !localPlayer.ClassJob.IsValid)
        {
            return;
        }

        var territoryId = Plugin.ClientState.TerritoryType;
        var mapId = Plugin.ClientState.MapId;
        var jobId = localPlayer.ClassJob.RowId;
        var jobStr = localPlayer.ClassJob.Value.Abbreviation.ExtractText();

        //Log.Info($"Player {localPlayer.Name} JobID={jobId} ({jobStr}) is in territory {territoryId} map {mapId}");

        var territory_hint = this.instances_data?.GetValueOrDefault(territoryId.ToString());
        if (territory_hint == null)
        {
            return;
        }


        var hint = territory_hint.maps.GetValueOrDefault(mapId.ToString());
        if (hint == null)
        {
            return;
        }

        if (hint.stages.Count == 0 && (hint.general == "..." || hint.general == ""))
        {
            ChatGui.Print($"No hints to display for {hint.en_name}.");
            return;
        }

        this.current_territory_hint = territory_hint;
        this.current_map_hint = hint;
    }

    private void OnMainSettingsCmd(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }
    private void OnHintCmd(string command, string args)
    {
        _loadCurrentMapHints();
        this.showHintPopup = !this.showHintPopup;
    }

    private void _renderHintSection(RoleHints stage, string mapId)
    {
        if (!string.IsNullOrWhiteSpace(stage.general) && stage.general != "...")
        {
            if (ImGui.Button($"General##{mapId}-{stage.stage_name}"))
            {
                ChatGui.Print($"General advice for {stage.stage_name}: \n{stage.general}");
                showHintPopup = false;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(stage.dps) && stage.dps != "...")
        {
            if (ImGui.Button($"DPS##{mapId}-{stage.stage_name}"))
            {
                ChatGui.Print($"DPS advice for {stage.stage_name}: \n{stage.dps}");
                showHintPopup = false;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(stage.healer) && stage.healer != "...")
        {
            if (ImGui.Button($"Healer##{mapId}-{stage.stage_name}"))
            {
                ChatGui.Print($"Healer advice for {stage.stage_name}: \n{stage.healer}");
                showHintPopup = false;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(stage.tank) && stage.tank != "...")
        {
            if (ImGui.Button($"Tank##{mapId}-{stage.stage_name}"))
            {
                ChatGui.Print($"Tank advice for {stage.stage_name}: \n{stage.tank}");
                showHintPopup = false;
            }
        }
    }
    private void DrawHintUI()
    {
        if (!showHintPopup || current_territory_hint == null) return;

        ImGui.OpenPopup("Backseat Driver");
        if (ImGui.BeginPopupModal("Backseat Driver", ref showHintPopup, ImGuiWindowFlags.AlwaysAutoResize))
        {
            ImGui.TextUnformatted("What hints are you interested in?");
            //ImGui.Separator();

            foreach (var mapPair in current_territory_hint.maps)
            {
                var mapId = mapPair.Key;
                var mapHint = mapPair.Value;

                ImGui.TextUnformatted($"{mapHint.en_name}");

                var stages = mapHint.stages;
                if (stages.Count == 0)
                {
                    stages = new List<RoleHints>
                    {
                        new RoleHints()
                        {
                            stage_name = mapHint.en_name,
                            general = mapHint.general,
                            dps = mapHint.dps,
                            healer = mapHint.healer,
                            tank = mapHint.tank
                        }
                    };
                }

                bool tabsNeeded = stages.Count > 1;

                if (stages.Count == 1)
                {
                    _renderHintSection(stages[0], mapId);
                }
                else
                {
                    if (ImGui.BeginTabBar($"##StagesTabBar_{mapId}"))
                    {
                        foreach (var stage in stages)
                        {
                            var tabLabel = string.IsNullOrWhiteSpace(stage.stage_name) ? "Stage" : stage.stage_name;
                            if (ImGui.BeginTabItem($"{tabLabel}##{mapId}-{tabLabel}"))
                            {
                                _renderHintSection(stage, mapId);
                                ImGui.EndTabItem();
                            }
                        }
                        ImGui.EndTabBar();
                    }
                }
            }

            if (ImGui.Button("Close"))
            {
                showHintPopup = false;
            }

            ImGui.EndPopup();
        }
    }

    private void OnImmediateHintCmd(string command, string args)
    {
        _loadCurrentMapHints();
        //ChatGui.Print($"Created hints for {jobStr} ({job_role}) in {hint.en_name} (territory {territoryId} map {mapId})");
        //ChatGui.Print($"Hints for {jobStr} ({job_role}) in {hint.en_name} (territory {territoryId} map {mapId}): {job_hint}");
        // ChatGui.Print($"General advice for {hint.en_name}: \n{hint.general}\n\nSpecifically for {jobStr} ({job_role}):\n{job_hint}");
        var hint = this.current_map_hint;

        if (Plugin.ClientState.LocalPlayer == null || hint == null)
        {
            return;
        }

        uint jobId = Plugin.ClientState.LocalPlayer.ClassJob.RowId;
        var jobStr = Plugin.ClientState.LocalPlayer.ClassJob.Value.Abbreviation.ExtractText();

        Role job_role = classjob_to_role.GetValueOrDefault(jobId, Role.Unknown);
        string job_hint = job_role switch
        {
            Role.Tank => hint.tank,
            Role.Healer => hint.healer,
            Role.DPS => hint.dps,
            _ => ""
        };

        if (hint.general != "" && hint.general != "...")
        {
            ChatGui.Print($"General advice for {hint.en_name}: \n{hint.general}");
        }

        if (job_hint != "" && job_hint != "...")
        {
            ChatGui.Print($"Specifically for {jobStr} ({job_role}):\n{job_hint}");
        }
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
