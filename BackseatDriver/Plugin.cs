using BackseatDriver.Windows;
using Dalamud.Game.Command;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Interface.Windowing;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Common.Lua;
using FFXIVClientStructs.FFXIV.Component.GUI;
using Dalamud.Bindings.ImGui;
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
using BackseatDriver;

namespace BackseatDriver;

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

public enum EnixTextColor
{
    White = 0,
    BluePale = 33,
    BlueLight = 58,
    BlueTank = 37,
    YellowBright = 44,
    YellowOrange = 45,
    GreenBright = 60,
    RedBright = 68,
    RedDPS = 545,
    GreenSoft = 71,
    GreenHealer = 504,
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
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IObjectTable Objects { get; private set; } = null!;

    public Dictionary<string, TerritoryRoleHints>? instances_data { get; private set; }
    public TerritoryRoleHints? current_territory_hint { get; set; }
    public MapRoleHints? current_map_hint { get; set; }
    private bool waitingForPlayer = false;
    private uint lastLoadedMapId;
    public EnemiesTracker enemiesTracker { get; private set; }

    private static readonly Dictionary<uint, Role> ClassJob_To_Role = new()
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

    private static readonly Dictionary<Role, EnixTextColor> RoleToColor = new()
    {
        { Role.Tank, EnixTextColor.BlueTank },
        { Role.Healer, EnixTextColor.GreenHealer },
        { Role.DPS, EnixTextColor.RedDPS },
        { Role.Unknown, EnixTextColor.White }
    };

    public Configuration Configuration { get; init; }

    public readonly WindowSystem WindowSystem = new("BackseatDriver");
    private ConfigWindow ConfigWindow { get; init; }
    private DriverWindow DriverWindow { get; init; }
    private HandbookWindow HandbookWindow { get; init; }
    private CoachWindow CoachWindow { get; init; }

    private bool load_instances_data()
    {
        var assembly = Assembly.GetExecutingAssembly();

        using var stream = assembly.GetManifestResourceStream("BackseatDriver.Data.instances_data.json.gz") ??
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

        enemiesTracker = new EnemiesTracker(this, Configuration);

        ConfigWindow = new ConfigWindow(this);
        DriverWindow = new DriverWindow(this, Configuration);
        HandbookWindow = new HandbookWindow(this, Configuration);
        CoachWindow = new CoachWindow(this, Configuration);

        WindowSystem.AddWindow(ConfigWindow);
        WindowSystem.AddWindow(DriverWindow);
        WindowSystem.AddWindow(HandbookWindow);
        WindowSystem.AddWindow(CoachWindow);

        CommandManager.AddHandler("/pbsdriver", new CommandInfo(OnDriverCmd)
        {
            HelpMessage = "Show all hints for the current duty."
        });

        CommandManager.AddHandler("/pbsdriver-quick", new CommandInfo(OnImmediateHintCmd)
        {
            HelpMessage = "Print hints for your job for all encounters in the current duty."
        });

        CommandManager.AddHandler("/pbsdriver-handbook", new CommandInfo(OnHandbookCmd)
        {
            HelpMessage = "Opens a handbook containing all duties covered by the plugin."
        });

        PluginInterface.UiBuilder.Draw += DrawUI;

        // This adds a button to the plugin installer entry of this plugin which allows
        // to toggle the display status of the configuration ui
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUI;

        // Adds another button that is doing the same but for the main ui of the plugin
        PluginInterface.UiBuilder.OpenMainUi += ToggleDriverUI;

        ClientState.TerritoryChanged += OnTerritoryChanged;

        Framework.Update += OnFrameworkUpdate;
    }

    public void Dispose()
    {
        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();

        CommandManager.RemoveHandler("/pbsdriver");
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (waitingForPlayer && ClientState.LocalPlayer is not null)
        {
            waitingForPlayer = false;

            _loadCurrentMapHints();
        }

        if (ClientState.LocalPlayer is not null && this.lastLoadedMapId != ClientState.MapId)
        {
            _loadCurrentMapHints();
        }

        enemiesTracker.scan();
    }

    private void OnTerritoryChanged(ushort newTerritoryId)
    {
        waitingForPlayer = true;
    }

    public void printTitle(string text, EnixTextColor color)
    {
        var title = new SeStringBuilder().AddUiForeground((ushort)color).AddText(text).AddUiForegroundOff().BuiltString;
        ChatGui.Print(title);
    }

    private void _loadCurrentMapHints()
    {
        this.current_territory_hint = null;
        this.current_map_hint = null;
        this.lastLoadedMapId = 0;

        var localPlayer = Plugin.ClientState.LocalPlayer;
        if (localPlayer == null || !localPlayer.ClassJob.IsValid)
        {
            Log.Info("No local player or invalid class job. Cannot load hints.");
            return;
        }

        var territoryId = Plugin.ClientState.TerritoryType;
        var mapId = Plugin.ClientState.MapId;
        var jobId = localPlayer.ClassJob.RowId;
        var jobStr = localPlayer.ClassJob.Value.Abbreviation.ExtractText();

        Log.Information($"Loading hints for territory {territoryId} (map {mapId}) for job {jobStr} ({jobId}).");

        var territory_hint = this.instances_data?.GetValueOrDefault(territoryId.ToString());
        this.lastLoadedMapId = mapId;

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
            return;
        }

        Log.Information($"Loaded hints for {territory_hint.en_name} ({territoryId}) - {hint.en_name} ({mapId}) for job {jobStr} ({jobId}).");
        this.current_territory_hint = territory_hint;
        this.current_map_hint = hint;
        this.lastLoadedMapId = mapId;
    }

    private void OnImmediateHintCmd(string command, string args)
    {
        _loadCurrentMapHints();

        var hint = this.current_map_hint;

        if (Plugin.ClientState.LocalPlayer == null || hint == null)
        {
            ChatGui.PrintError("No hints available in here.");
            return;
        }

        uint jobId = Plugin.ClientState.LocalPlayer.ClassJob.RowId;
        var jobStr = Plugin.ClientState.LocalPlayer.ClassJob.Value.Abbreviation.ExtractText();

        Role job_role = ClassJob_To_Role.GetValueOrDefault(jobId, Role.Unknown);


        if (hint.stages.Count > 0)
        {
            foreach (var stage in hint.stages)
            {
                printTitle(stage.stage_name, EnixTextColor.BlueLight);

                if (stage.general != "" && stage.general != "...")
                {
                    ChatGui.Print($"{stage.general}");
                }

                string job_hint = job_role switch
                {
                    Role.Tank => stage.tank,
                    Role.Healer => stage.healer,
                    Role.DPS => stage.dps,
                    _ => ""
                };

                if (job_hint != "" && job_hint != "...")
                {
                    printTitle($"Specifically for {jobStr} ({job_role}):", RoleToColor[job_role]);
                    ChatGui.Print($"{job_hint}");
                }
            }
        }
        else
        {
            string job_hint = job_role switch
            {
                Role.Tank => hint.tank,
                Role.Healer => hint.healer,
                Role.DPS => hint.dps,
                _ => ""
            };
            
            printTitle(hint.en_name, EnixTextColor.BlueLight);

            if (hint.general != "" && hint.general != "...")
            {
                ChatGui.Print($"{hint.general}");
            }

            if (job_hint != "" && job_hint != "...")
            {
                printTitle($"Specifically for {jobStr} ({job_role})", RoleToColor[job_role]);
                ChatGui.Print($"{job_hint}");
            }
        }
    }
    private void OnDriverCmd(string command, string args)
    {
        _loadCurrentMapHints();
        ToggleDriverUI();
    }
    private void OnHandbookCmd(string command, string args)
    {
        if (current_map_hint != null)
        {
            ChatGui.Print("Handbook is unavailable in duties.");
            return;
        }

        ToggleHandbookUI();
    }

    private void DrawUI() => WindowSystem.Draw();

    public void ToggleConfigUI() => ConfigWindow.Toggle();
    public void ToggleDriverUI() => DriverWindow.Toggle();
    public void ToggleHandbookUI() => HandbookWindow.Toggle();
    public void ToggleCoachUI() => CoachWindow.Toggle();
}
