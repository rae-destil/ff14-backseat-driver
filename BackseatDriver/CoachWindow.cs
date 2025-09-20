using BackseatDriver;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BackseatDriver.Windows;

public class CoachWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;
    private bool needRefresh = false;

    private Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.Action> actionSheet;
    private Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.BNpcBase> bnpcBaseSheet;
    private Lumina.Excel.ExcelSheet<Lumina.Excel.Sheets.BNpcName> bnpcSheet;

    public CoachWindow(Plugin plugin, Configuration config)
        : base("Backseat Driver Coach##imdumb", ImGuiWindowFlags.AlwaysAutoResize)
    {
        Plugin = plugin;
        Configuration = config;

        Plugin.enemiesTracker.registerOnChangeCb(onEnemiesChanged);
        Plugin.enemiesTracker.registerOnCastCb(onCasting);

        actionSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>();
        bnpcBaseSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcBase>();
        bnpcSheet = Plugin.DataManager.GetExcelSheet<Lumina.Excel.Sheets.BNpcName>();
    }

    public void onEnemiesChanged()
    {
        needRefresh = true;
    }
    public void onCasting(string enemyName, uint castId)
    {
        var actionRow = actionSheet?.GetRow(castId);
        var actionName = actionRow != null ? actionRow.Value.Name : $"Action #{castId}";

        Plugin.Log.Info($"{enemyName} casts {actionName}");    
        
        //Plugin.ChatGui.Print($"{enemyName} casted {actionName}");
    }

    public void Dispose() { }

    public override void Draw()
    {
        const float CapPx = 768f; // max height, idk
        const float CapPy = 480f; // max width, idk
        float h = MathF.Min(CapPx, ImGui.GetContentRegionAvail().Y);
        float w = MathF.Min(CapPy, ImGui.GetContentRegionAvail().X);

        foreach (var enemyInfo in Plugin.enemiesTracker.currEnemies)
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

        if (ImGui.Button("Close"))
        {
            Plugin.ToggleCoachUI();
        }
    }
}
