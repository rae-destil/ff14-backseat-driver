using System;
using System.Numerics;
using Dalamud.Interface.Windowing;
using ImGuiNET;

namespace BSDriverPlugin.Windows;

public class ConfigWindow : Window, IDisposable
{
    private Configuration Configuration;
    private Plugin Plugin;

    // We give this window a constant ID using ###
    // This allows for labels being dynamic, like "{FPS Counter}fps###XYZ counter window",
    // and the window ID will always be "###XYZ counter window" for ImGui
    public ConfigWindow(Plugin plugin) : base("Backseat Driver Configuration###ConstID", ImGuiWindowFlags.AlwaysAutoResize)
    {
        Configuration = plugin.Configuration;
        Plugin = plugin;
    }

    public void Dispose() { }

    public override void Draw()
    {
        // can't ref a property, so use a local copy
        var configValue = Configuration.KeepDriverOpenOnClick;
        if (ImGui.Checkbox("Keep driver window open after getting a hint", ref configValue))
        {
            Configuration.KeepDriverOpenOnClick = configValue;
        }

        configValue = Configuration.DisplayNerdStuff;
        if (ImGui.Checkbox("Display nerd stuff", ref configValue))
        {
            Configuration.DisplayNerdStuff = configValue;
        }

        if (ImGui.Button("Save and Close"))
        {
            Configuration.Save();
            Plugin.ToggleConfigUI();
        }
    }
}
