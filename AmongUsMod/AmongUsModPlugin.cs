using BepInEx;
using BepInEx.Unity.IL2CPP;
using BepInEx.Logging;
using HarmonyLib;

namespace AmongUsMod;

[BepInPlugin("com.tdeshane.amongusmod", "Among Us Mod", "1.0.0")]
[BepInProcess("Among Us.exe")]
public class AmongUsModPlugin : BasePlugin
{
    public const string Id = "com.tdeshane.amongusmod";
    internal static ManualLogSource Log;
    public Harmony Harmony { get; } = new(Id);

    public override void Load()
    {
        Log = base.Log;
        Log.LogInfo("Among Us Mod loading...");

        Harmony.PatchAll();

        Log.LogInfo("Among Us Mod loaded!");
        Log.LogInfo("  - Always Impostor: ON");
        Log.LogInfo("  - AI NPC Bots: ON");
        Log.LogInfo("  - 3D Crewmates: ON");
    }
}
