using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace UnlockMod;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class ModMain : BaseUnityPlugin
{
	private void Awake()
	{
		Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} ver {PluginInfo.PLUGIN_VERSION} loaded!"); // print a message to the BepInEx console
		var harmony = new Harmony(PluginInfo.PLUGIN_GUID); // create a harmony patcher
		harmony.PatchAll(); // run all patches in the mod assembly
	}
}