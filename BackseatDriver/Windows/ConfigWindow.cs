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

        if (ImGui.Button("Save and Close"))
        {
            config.Save();
            plugin.ToggleConfigUI();
        }
    }
}
