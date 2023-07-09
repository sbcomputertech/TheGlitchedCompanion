using HarmonyLib;
using UnityEngine;

namespace TGC.Client
{
    [HarmonyPatch(typeof(Player_DevConsole))]
    public static class DevConsoleInterop
    {
        [HarmonyPatch(nameof(Player_DevConsole.Update))]
        [HarmonyPrefix]
        public static void AllowOpeningConsoleAndFixPosition(ref Player_DevConsole __instance)
        {
            __instance.NEVER = false;
            __instance.panelConsole.transform.position = new Vector3(500, 400, 0);
        }
    }
}
