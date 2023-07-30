using HarmonyLib;

namespace TGC.Client;

[HarmonyPatch(typeof(MainMenu_VersionTextManager))]
public static class PatchVersionText
{
    [HarmonyPatch(nameof(MainMenu_VersionTextManager.Start))]
    [HarmonyPostfix]
    private static void PatchTextWithModNameVersion(ref MainMenu_VersionTextManager __instance)
    {
        __instance.txt.text += $"\n{PluginInfo.PLUGIN_NAME} v{PluginInfo.PLUGIN_VERSION}";
    }
}