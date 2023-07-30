using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;

namespace TGC.Client
{
    [HarmonyPatch(typeof(Player_DevConsole))]
    public static class DevConsoleInterop
    {
        private static Player_DevConsole _Instance;
        private static List<CustomDevCommand> _CustomCommands = new();
        private static GameObject _ConsoleMainGameObj;
        private static CanvasGroup _OutputCanvasGroupToSetAlpha;

        public static bool IsConsoleOpen => _Instance.showingConsole;

        [HarmonyPatch(nameof(Player_DevConsole.Awake))]
        [HarmonyPostfix]
        private static void InitialiseInteropOnAwake(ref Player_DevConsole __instance)
        {
            _Instance = __instance;
            _Instance.NEVER = false;
            
            _ConsoleMainGameObj = _Instance.transform.Find("F3").gameObject;
            _ConsoleMainGameObj.transform.position = new Vector3(400, 900, 0);

            _OutputCanvasGroupToSetAlpha = _Instance.consoleOutput.GetComponent<CanvasGroup>();
        }

        [HarmonyPatch(nameof(Player_DevConsole.Update))]
        [HarmonyPostfix]
        private static void FixThingsOnUpdate()
        {
            _Instance.consoleOutput.gameObject.SetActive(true);
            
            if (_Instance.showingConsole)
            {
                _OutputCanvasGroupToSetAlpha.alpha = 1;
            }
        }

        [HarmonyPatch(nameof(Player_DevConsole.RunConsoleCommand))]
        [HarmonyPrefix]
        private static bool InjectCommandBeforeDefaultRun()
        {
            var cmd = _Instance.propeties[0];
            var args = _Instance.propeties.Length > 1 ? _Instance.propeties[1..] : Array.Empty<string>();

            var commandFound = _CustomCommands.FirstOrDefault(c => c.Name == cmd);
            if (commandFound == null)
            {
                // run the original command handler code
                return true;
            }

            commandFound.Function(args);
            _OutputCanvasGroupToSetAlpha.alpha = 1f;
            return false;
        }

        [HarmonyPatch(nameof(Player_DevConsole.ConsoleHelp))]
        [HarmonyPostfix]
        private static void InjectCustomCommandsIntoHelp()
        {
            if (_Instance.propeties.Length != 1) return;
            _Instance.consoleHelp.text += $"\n{PluginInfo.PLUGIN_SHORT_NAME}:";
            foreach (var command in _CustomCommands)
            {
                _Instance.consoleHelp.text += $"\n- {command.Name}: {command.Description}";
            }
        }

        public static void WriteMessage(string message, string colorName = "white")
        {
            var colorText = $"<color={colorName}>[{PluginInfo.PLUGIN_SHORT_NAME}] {message}</color>";
            _Instance.consoleOutput.text += "\n" + colorText;
        }

        public static void RegisterCustomCommand(string name, string desc, Action<string[]> command)
        {
            if (_CustomCommands.Any(c => c.Name == name))
            {
                ModMain.LogSource.LogError($"Attempted to add command {name}, while one with that name already is added!");
                return;
            }
            _CustomCommands.Add(new CustomDevCommand(name, desc, command));
        }

        public static void Hide()
        {
            _Instance.interactTimeConsole = 0f;
            _Instance.showingConsole = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    public class CustomDevCommand
    {
        public string Name;
        public string Description;
        public Action<string[]> Function;

        public CustomDevCommand(string name, string description, Action<string[]> function)
        {
            Name = name;
            Description = description;
            Function = function;
        }
    }
}
