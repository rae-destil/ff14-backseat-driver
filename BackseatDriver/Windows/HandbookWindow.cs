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

public class HandbookWindow : Window, IDisposable
{
    private Plugin Plugin;
    private Configuration Configuration;
    private Dictionary<string, List<RoleHints>> availableDuties;

    public HandbookWindow(Plugin plugin, Configuration config)
        : base("Backseat Driver Handbook##imdumb", ImGuiWindowFlags.AlwaysAutoResize)
    {
        Plugin = plugin;
        Configuration = config;
        availableDuties = new Dictionary<string, List<RoleHints>>();


        foreach (var hints in plugin.instances_data.Values)
        {
            if (hints.maps.Count == 0)
            { 
                continue; 
            }

            var terrInfo = new List<RoleHints>();

            foreach (var map in hints.maps.Values)
            {
                if (map.stages.Count == 0)
                {
                    // need at least one field to be not empty
                    if ((map.general != "" && map.general != "...") ||
                        (map.dps != "" && map.dps != "...") ||
                        (map.healer != "" && map.healer != "...") ||
                        (map.tank != "" && map.tank != "..."))
                    {
                        var uglyHack = new RoleHints()
                        {
                            stage_name = map.en_name,
                            general = map.general,
                            dps = map.dps,
                            healer = map.healer,
                            tank = map.tank
                        };

                        terrInfo.Add(uglyHack);
                    }
                }
                else
                {
                    foreach (var stage in map.stages)
                    {
                        if ((stage.general != "" && map.general != "...") ||
                            (stage.dps != "" && map.dps != "...") ||
                            (stage.healer != "" && map.healer != "...") ||
                            (stage.tank != "" && map.tank != "..."))
                        {
                            terrInfo.Add(stage);
                        }
                    }
                }
            }

            if (terrInfo.Count > 0) 
            {
                availableDuties.Add(hints.en_name, terrInfo);
            }
        }

    }

    public void Dispose() { }

    public override void Draw()
    {
        const float CapPx = 768f; // max height, idk
        float h = MathF.Min(CapPx, ImGui.GetContentRegionAvail().Y);

        if (ImGui.BeginChild("duties_scroll", new Vector2(0, h), true))
        {
            foreach (var mapPair in availableDuties)
            {
                var mapName = mapPair.Key;
                var mapHint = mapPair.Value;

                if (ImGui.CollapsingHeader($"{mapName}##g",
                    ImGuiTreeNodeFlags.SpanAvailWidth | ImGuiTreeNodeFlags.Framed))
                {
                    ImGui.Indent();
                    foreach (var st in mapHint)
                    {
                        ImGui.TextUnformatted($"{st.stage_name}");
                        ImGui.SameLine();
                        _renderHintSection(st, $"{mapName}-{st.stage_name}");
                    }
                    ImGui.Unindent();
                }
            }
            ImGui.EndChild();
        }

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
            if (ImGui.Button($"General##{mapId}"))
            {
                Plugin.printTitle($"General advice for {stage.stage_name}:", EnixTextColor.BlueLight);
                Plugin.ChatGui.Print($"{stage.general}");
                buttonClicked = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stage.dps) && stage.dps != "...")
        {
            ImGui.SameLine();
            if (ImGui.Button($"DPS##{mapId}"))
            {
                Plugin.printTitle($"DPS advice for {stage.stage_name}:", EnixTextColor.RedDPS);
                Plugin.ChatGui.Print($"{stage.dps}");
                buttonClicked = true;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(stage.healer) && stage.healer != "...")
        {
            ImGui.SameLine();
            if (ImGui.Button($"Healer##{mapId}"))
            {
                Plugin.printTitle($"Healer advice for {stage.stage_name}:", EnixTextColor.GreenHealer);
                Plugin.ChatGui.Print($"{stage.healer}");
                buttonClicked = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stage.tank) && stage.tank != "...")
        {
            ImGui.SameLine();
            if (ImGui.Button($"Tank##{mapId}"))
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
