using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Numerics;

namespace BSDriverPlugin.Windows;

public class DriverWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;

    public DriverWindow(Plugin plugin, Configuration config)
        : base("Backseat Driver##imdumb", ImGuiWindowFlags.AlwaysAutoResize)
    {
        Plugin = plugin;
        Configuration = config;
    }

    public void Dispose() { }

    private string? _getTerritoryName()
    {
        var territoryId = Plugin.ClientState.TerritoryType;
        if (Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territoryId, out var territoryRow))
        {
            return territoryRow.PlaceName.Value.Name.ExtractText();
        }
        return null;
    }

    public override void Draw()
    {
        if (Configuration.DisplayNerdStuff)
        {
            ImGui.TextUnformatted($"TID: {Plugin.ClientState.TerritoryType}, MID: {Plugin.ClientState.MapId}");
        }

        if (Plugin.ClientState.LocalPlayer == null)
        {
            ImGui.TextUnformatted("Loading hints...");
        }
        else if (Plugin.current_territory_hint == null)
        {
            var mapName = _getTerritoryName();
            if (mapName != null)
            {
                ImGui.TextUnformatted($"No hints to display for {mapName}.");
            }
            else
            {
                ImGui.TextUnformatted("Invalid territory.");
            }
        }
        else
        {
            var mapName = _getTerritoryName();
            if (mapName != null)
            {
                ImGui.TextUnformatted($"Hints for {mapName}:");
            }
            else
            {
                ImGui.TextUnformatted("What hints are you interested in?");
            }
            
            foreach (var mapPair in Plugin.current_territory_hint.maps)
            {
                var mapId = mapPair.Key;
                var mapHint = mapPair.Value;

                if (mapHint.tank == "..." && mapHint.dps == "..." && mapHint.healer == "..." && mapHint.general == "...")
                {
                    continue;
                }

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
        }
        
        if (ImGui.Button("Configuration"))
        {
            Plugin.ToggleConfigUI();
        }
        ImGui.SameLine();
        if (ImGui.Button("Close"))
        {
            Plugin.ToggleDriverUI();
        }
    }

    private void _renderHintSection(RoleHints stage, string mapId)
    {
        bool buttonClicked = false;

        if (!string.IsNullOrWhiteSpace(stage.general) && stage.general != "...")
        {
            if (ImGui.Button($"General##{mapId}-{stage.stage_name}"))
            {
                Plugin.printTitle($"General advice for {stage.stage_name}:", EnixTextColor.BlueLight);
                Plugin.ChatGui.Print($"{stage.general}");
                buttonClicked = true;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(stage.dps) && stage.dps != "...")
        {
            if (ImGui.Button($"DPS##{mapId}-{stage.stage_name}"))
            {
                Plugin.printTitle($"DPS advice for {stage.stage_name}:", EnixTextColor.RedDPS);
                Plugin.ChatGui.Print($"{stage.dps}");
                buttonClicked = true;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(stage.healer) && stage.healer != "...")
        {
            if (ImGui.Button($"Healer##{mapId}-{stage.stage_name}"))
            {
                Plugin.printTitle($"Healer advice for {stage.stage_name}:", EnixTextColor.GreenHealer);
                Plugin.ChatGui.Print($"{stage.healer}");
                buttonClicked = true;
            }
        }
        ImGui.SameLine();
        if (!string.IsNullOrWhiteSpace(stage.tank) && stage.tank != "...")
        {
            if (ImGui.Button($"Tank##{mapId}-{stage.stage_name}"))
            {
                Plugin.printTitle($"Tank advice for {stage.stage_name}:", EnixTextColor.BlueTank);
                Plugin.ChatGui.Print($"{stage.tank}");
                buttonClicked = true;
            }
        }

        if (buttonClicked && !Configuration.KeepDriverOpenOnClick)
        {
            Plugin.ToggleDriverUI();
        }
    }
}
