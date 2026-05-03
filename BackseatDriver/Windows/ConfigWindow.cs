using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using Dalamud.Bindings.ImGui;

namespace BackseatDriver.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration config;
    private Plugin plugin;

    public ConfigWindow(Plugin plugin) : base("Backseat Driver Configuration###ConstID", ImGuiWindowFlags.AlwaysAutoResize)
    {
        config = plugin.Configuration;
        this.plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var configValue = config.KeepDriverOpenOnClick;
        if (ImGui.Checkbox("Keep driver window open after getting a hint", ref configValue))
        {
            config.KeepDriverOpenOnClick = configValue;
        }

        configValue = config.DisplayNerdStuff;
        if (ImGui.Checkbox("Display nerd stuff", ref configValue))
        {
            config.DisplayNerdStuff = configValue;
        }

        configValue = config.CoachModeEchoIntoChat;
        if (ImGui.Checkbox("Print coach hints in chat.", ref configValue))
        {
            config.CoachModeEchoIntoChat = configValue;
        }

        configValue = config.CoachModeLogToFile;
        if (ImGui.Checkbox("Write coach hints to file.", ref configValue))
        {
            config.CoachModeLogToFile = configValue;
        }

        configValue = config.CoachModeLogMapChanges;
        if (ImGui.Checkbox("Write territory and map changes to file.", ref configValue))
        {
            config.CoachModeLogMapChanges = configValue;
        }

        if (config.CoachModeLogToFile || config.CoachModeLogMapChanges)
        {
            ImGui.SetNextItemWidth(120);
            var maxSizeMb = config.SessionLogMaxSizeMb;
            if (ImGui.InputFloat("Max log size (MB)", ref maxSizeMb, 0.1f, 1.0f, "%.2f"))
            {
                config.SessionLogMaxSizeMb = Math.Max(0, maxSizeMb);
            }

            ImGui.SetNextItemWidth(120);
            var trimPercent = config.SessionLogTrimPercent;
            if (ImGui.InputInt("Log truncate percent", ref trimPercent))
            {
                config.SessionLogTrimPercent = Math.Clamp(trimPercent, 1, 100);
            }

            ImGui.TextWrapped($"Log file: {plugin.GetSessionLogPath()}");
        }

        if (ImGui.Button("Save and Close"))
        {
            config.Save();
            plugin.ToggleConfigUI();
        }
    }
}
