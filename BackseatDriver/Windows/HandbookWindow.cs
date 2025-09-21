using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace BackseatDriver.Windows;

public class HandbookWindow : Window, IDisposable
{
    private Plugin plugin;
    private Configuration config;
    private Dictionary<string, List<RoleHints>> availableDuties;
    private Dictionary<string, List<RoleHints>> filteredDuties = new();

    private string filterQuery = "";
    private string prevFilterQuery = "";


    public HandbookWindow(Plugin plugin, Configuration config)
        : base("Backseat Driver Handbook##imdumb", ImGuiWindowFlags.AlwaysAutoResize)
    {
        this.plugin = plugin;
        this.config = config;
        availableDuties = new Dictionary<string, List<RoleHints>>();

        if (plugin.instances_data == null)
        {
            throw new Exception("Cannot instantiate handbook with missing instances data");
        }

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
        const float CapPy = 480f; // max width, idk
        float h = MathF.Min(CapPx, ImGui.GetContentRegionAvail().Y);
        float w = MathF.Min(CapPy, ImGui.GetContentRegionAvail().X);

        if (ImGui.BeginChild("duties_scroll", new Vector2(CapPy, h), true))
        {
            ImGui.PushItemWidth(-1); // full width
            ImGui.InputTextWithHint("##dutysearch", "Search duties...", ref filterQuery, 128);           
            ImGui.PopItemWidth();

            if (!string.Equals(filterQuery, prevFilterQuery, StringComparison.Ordinal))
            {
                prevFilterQuery = filterQuery;
                filteredDuties.Clear();

                var q = filterQuery.Trim();
                if (q.Length == 0)
                {
                    foreach (var kv in availableDuties)
                    {
                        filteredDuties.Add(kv.Key, kv.Value);
                    }
                }
                else
                {
                    var qLower = q.ToLowerInvariant();
                    foreach (var kv in availableDuties)
                    {
                        // fuzzy on map name OR any stage name
                        bool matchMap = _isFuzzySubsequence(kv.Key.ToLowerInvariant(), qLower);
                        bool matchAnyStage = !matchMap && kv.Value.Any(st => _isFuzzySubsequence(st.stage_name.ToLowerInvariant(), qLower));
                        if (matchMap || matchAnyStage)
                        {
                            filteredDuties.Add(kv.Key, kv.Value);
                        }
                    }
                }
            }

            if (filteredDuties.Count == 0)
            {
                foreach (var kv in availableDuties)
                { 
                    filteredDuties.Add(kv.Key, kv.Value);
                }
            }

            foreach (var mapPair in filteredDuties)
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
        }
        ImGui.EndChild();

        if (ImGui.Button("Close"))
        {
            plugin.ToggleHandbookUI();
        }
    }
    private static bool _isFuzzySubsequence(string textLower, string patternLower)
    {
        if (patternLower.Length == 0) return true;
        int ti = 0, pi = 0;
        while (ti < textLower.Length && pi < patternLower.Length)
        {
            if (textLower[ti] == patternLower[pi]) pi++;
            ti++;
        }
        return pi == patternLower.Length;
    }
    private void _renderHintSection(RoleHints stage, string mapId)
    {
        bool buttonClicked = false;

        if (!string.IsNullOrWhiteSpace(stage.general) && stage.general != "...")
        {
            if (ImGui.Button($"General##{mapId}"))
            {
                plugin.printTitle($"General advice for {stage.stage_name}:", EnixTextColor.BlueLight);
                Plugin.ChatGui.Print($"{stage.general}");
                buttonClicked = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stage.dps) && stage.dps != "...")
        {
            ImGui.SameLine();
            if (ImGui.Button($"DPS##{mapId}"))
            {
                plugin.printTitle($"DPS advice for {stage.stage_name}:", EnixTextColor.RedDPS);
                Plugin.ChatGui.Print($"{stage.dps}");
                buttonClicked = true;
            }
        }
        
        if (!string.IsNullOrWhiteSpace(stage.healer) && stage.healer != "...")
        {
            ImGui.SameLine();
            if (ImGui.Button($"Healer##{mapId}"))
            {
                plugin.printTitle($"Healer advice for {stage.stage_name}:", EnixTextColor.GreenHealer);
                Plugin.ChatGui.Print($"{stage.healer}");
                buttonClicked = true;
            }
        }

        if (!string.IsNullOrWhiteSpace(stage.tank) && stage.tank != "...")
        {
            ImGui.SameLine();
            if (ImGui.Button($"Tank##{mapId}"))
            {
                plugin.printTitle($"Tank advice for {stage.stage_name}:", EnixTextColor.BlueTank);
                Plugin.ChatGui.Print($"{stage.tank}");
                buttonClicked = true;
            }
        }

        if (buttonClicked && !config.KeepDriverOpenOnClick)
        {
            plugin.ToggleHandbookUI();
        }
    }
}
