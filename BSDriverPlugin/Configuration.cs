using Dalamud.Configuration;
using Dalamud.Plugin;
using System;

namespace BSDriverPlugin;

[Serializable]
public class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 0;

    public bool KeepDriverOpenOnClick { get; set; } = true;
    public bool DisplayNerdStuff { get; set; } = false;

    // the below exist just to make saving less cumbersome
    public void Save()
    {
        Plugin.PluginInterface.SavePluginConfig(this);
    }
}
