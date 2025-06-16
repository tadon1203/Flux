using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using Flux.Features;
using Flux.Graphics;

namespace Flux;

[BepInPlugin(PluginName, PluginName, "1.0.0")]
public class Plugin : BasePlugin
{
    public const string PluginName = "Flux";

    public override void Load()
    {
        Logger.Initialize(Log);

        try
        {
            Logger.Info($"Plugin {PluginName} is loading...");

            if (!D3D11Hook.Hook())
                return;

            FeatureManager.Initialize();

            AddComponent<MainMonoBehaviour>();
        }
        catch (Exception e)
        {
            Logger.Error($"An unexpected error occurred while loading plugin {PluginName}: {e}");
        }

        Logger.Info($"{PluginName} has been loaded successfully.");
    }
}