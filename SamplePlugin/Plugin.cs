using BSDriverPlugin.Windows;
using Dalamud.Game.Command;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
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
using static FFXIVClientStructs.FFXIV.Client.System.Scheduler.Resource.SchedulerResource;

namespace BSDriverPlugin;

public class MapRoleHints
{
    [JsonPropertyName("en")]
    public string en_name { get; set; } = "";

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
            HelpMessage = "Print hints for your job in the current instance."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

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

    private void OnMainSettingsCmd(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        ToggleMainUI();
    }
    private void OnHintCmd(string command, string args)
    {
        // in response to the slash command, just toggle the display status of our main ui
        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null || !localPlayer.ClassJob.IsValid)
        {
            return;
        }

        var territoryId = Plugin.ClientState.TerritoryType;
        var mapId = Plugin.ClientState.MapId;
        var jobId = localPlayer.ClassJob.RowId;
        var jobStr = localPlayer.ClassJob.Value.Abbreviation.ExtractText();
        
        Log.Info($"Player {localPlayer.Name} JobID={jobId} ({jobStr}) is in territory {territoryId} map {mapId}");

        var hint = this.instances_data?.GetValueOrDefault(territoryId.ToString())?.maps.GetValueOrDefault(mapId.ToString());
        if (hint == null)
        {
            Log.Info($"No hints found for territory {territoryId} map {mapId}");
            return;
        }

        Role job_role = classjob_to_role.GetValueOrDefault(jobId, Role.Unknown);
        string job_hint = job_role switch
        {
            Role.Tank => hint.tank,
            Role.Healer => hint.healer,
            Role.DPS => hint.dps,
            _ => ""
        };

        //ChatGui.Print($"Hints for {jobStr} ({job_role}) in {hint.en_name} (territory {territoryId} map {mapId}): {job_hint}");
        ChatGui.Print($"Hints for {jobStr} ({job_role}) in {hint.en_name}: \n{hint.general}\n{job_hint}");
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleMainUI() => MainWindow.Toggle();
}
