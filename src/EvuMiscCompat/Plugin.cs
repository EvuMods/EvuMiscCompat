using BepInEx;
using HarmonyLib;

namespace EvuMiscCompat;

[BepInPlugin(PluginInfo.Guid, PluginInfo.Name, PluginInfo.Version)]
[BepInDependency("jello.trinketsunbound", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("org.bepinex.plugins.dualwield", BepInDependency.DependencyFlags.SoftDependency)]
[BepInDependency("com.orianaventure.mod.LocationReset", BepInDependency.DependencyFlags.SoftDependency)]
public sealed class Plugin : BaseUnityPlugin
{
    internal static BepInEx.Logging.ManualLogSource Log { get; private set; } = null!;

    void Awake()
    {
        Log = Logger;
        LocationResetPlayer.Bind(this);
        Harmony.CreateAndPatchAll(typeof(Plugin).Assembly, PluginInfo.Guid);
        Logger.LogInfo("EvuMiscCompat loaded.");
    }
}
