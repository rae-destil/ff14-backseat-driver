using BackseatDriver;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace BackseatDriver.Windows;

public class CoachWindow : Window, IDisposable
{
    private Plugin plugin;
    private Configuration config;

    private Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Action> actionSheet;
    private Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.BNpcBase> bnpcBaseSheet;
    private Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.BNpcName> bnpcSheet;

    private CoachActionHint lastActionHint = new();

    private Queue<string> eventsLog;
    const int EVENTS_LOG_MAX_ENTRIES = 100;
    private bool eventsStickyScroll = false;
    private bool pendingLogUpdate = false;

    private HashSet<string> relevantEnemyIDs = new();

    public CoachWindow(Plugin plugin, Configuration config)
        : base("Backseat Driver Coach##imdumb", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.config = config;

        this.plugin.enemiesTracker.registerOnChangeCb(onEnemiesChanged);
        this.plugin.enemiesTracker.registerOnCastCb(onCasting);

        actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        bnpcBaseSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcBase>();
        bnpcSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>();

        eventsLog = new Queue<string>(EVENTS_LOG_MAX_ENTRIES);
    }

    public void onEnemiesChanged()
    {
        if (plugin.current_map_hint == null || plugin.current_map_hint.coachHints.Count == 0)
        {
            relevantEnemyIDs.Clear();
            return;
        }

        relevantEnemyIDs.UnionWith(plugin.current_map_hint.coachHints.Keys);
    }
    public void onCasting(string enemyName, ulong enemyId, uint castId)
    {
        if (!config.DisplayNerdStuff && !relevantEnemyIDs.Contains(enemyId.ToString()))
        {
            return;
        }

        var actionRow = actionSheet?.GetRow(castId);
        var actionName = actionRow != null ? actionRow.Value.Name : $"Action #{castId}";

        var castString = "";

        if (config.DisplayNerdStuff)
        {
            castString = $"{enemyName} ({enemyId}) casts {actionName} ({castId}).";
        }
        else
        {
            castString = $"{enemyName} casts {actionName}.";
        }

        if (plugin.current_map_hint?.coachHints.Count > 0)
        {
            var hint = plugin.getCoachingActionHints(enemyId.ToString(), castId.ToString(), ref lastActionHint);
            if (hint != null)
            {
                if (lastActionHint.general != "...")
                {
                    castString += $"\n{lastActionHint.general}";
                }
                if (lastActionHint.roleSpecific != "...")
                {
                    castString += $"\n{lastActionHint.roleSpecific}";
                }
            }
        }
        
        if (eventsLog.Count == EVENTS_LOG_MAX_ENTRIES)
        {
            eventsLog.Dequeue();
        }

        eventsLog.Enqueue(castString);
        pendingLogUpdate = true;

        //Plugin.Log.Info(castString);
    }

    public void Dispose() { }

    public override void Draw()
    {
        const float CapPx = 240f; // max height, idk
        const float CapPy = 480f; // max width, idk
        float h = MathF.Min(CapPx, ImGui.GetContentRegionAvail().Y);
        float w = MathF.Min(CapPy, ImGui.GetContentRegionAvail().X);

        if (config.DisplayNerdStuff)
        {
            int rows = plugin.enemiesTracker.currEnemies.Count;
            float rowH = ImGui.GetTextLineHeightWithSpacing();
            float pad = ImGui.GetStyle().WindowPadding.Y * 2f;
            float contentH = ((rows + 1) * rowH) + pad; // +1 because of the 'title' lol

            float childH = MathF.Min(contentH, CapPx);

            if (ImGui.BeginChild("nearby_enemies", size : new Vector2(0, childH), border: true))
            {
                ImGui.TextUnformatted("Nearby Enemies:");

                foreach (var enemyInfo in plugin.enemiesTracker.currEnemies)
                {
                    if (enemyInfo.castId == 0)
                    {
                        ImGui.TextUnformatted($"{enemyInfo.Name}: DID {enemyInfo.DataId}; EID {enemyInfo.EntityId}");
                    }
                    else
                    {
                        ImGui.TextUnformatted($"{enemyInfo.Name}: DID {enemyInfo.DataId}; EID {enemyInfo.EntityId} (casting {enemyInfo.castId})");
                    }
                }

                ImGui.EndChild();
            }
        }

        ImGui.Separator();

        ImGui.TextUnformatted("Casts Log");

        if (ImGui.BeginChild("events_log", new Vector2(CapPy, h), true))
        {
            if (eventsStickyScroll)
            {
                pendingLogUpdate = false;
                ImGui.SetScrollY(ImGui.GetScrollMaxY());
            }

            foreach (var castLine in eventsLog)
            {
                ImGui.TextUnformatted(castLine);
            }

            // note how this is here because we want the *next* iteration to snap to the bottom after we've added content in the *current* draw call
            eventsStickyScroll = ImGui.GetScrollY() >= (ImGui.GetScrollMaxY() - 1.0f) && pendingLogUpdate;

            ImGui.EndChild();
        }


        if (ImGui.Button("Close"))
        {
            plugin.ToggleCoachUI();
        }
    }
}
