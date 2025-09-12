using HarmonyLib;
using Oxide.Core;
using Oxide.Core.Plugins;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Delay OnServerSave", "nivex", "0.1.2")]
    [Description("Delay the OnServerSave hook by some seconds.")]
    public class DelayOnServerSave : CovalencePlugin
    {
        private Harmony _harmony;
        private static Coroutine _co;
        private static FieldInfo hookSubscriptionsFieldInfo;
        private static PropertyInfo pluginsPropertyInfo;

        private void OnServerInitialized()
        {
            hookSubscriptionsFieldInfo = typeof(PluginManager).GetField("hookSubscriptions", BindingFlags.Instance | BindingFlags.NonPublic);
            if (hookSubscriptionsFieldInfo == null)
            {
                return;
            }
            Type hookSubscriptionsType = hookSubscriptionsFieldInfo.FieldType.GetGenericArguments()[1];
            pluginsPropertyInfo = hookSubscriptionsType.GetProperty("Plugins", BindingFlags.Instance | BindingFlags.Public);
            if (pluginsPropertyInfo == null)
            {
                return;
            }
            _harmony = new Harmony(Name + "Patch");
            _harmony.PatchAll();
        }

        private void Unload()
        {
            if (_co != null)
            {
                ServerMgr.Instance.StopCoroutine(_co);
                Interface.CallHook("OnServerSave");
                _co = null;
            }
            if (_harmony != null)
            {
                pluginsPropertyInfo = null;
                hookSubscriptionsFieldInfo = null;
                _harmony.UnpatchAll(Name + "Patch");
            }
        }

        [HarmonyPatch]
        public static class DoAutomatedSavePatch
        {
            internal static MethodBase TargetMethod()
            {
                return typeof(SaveRestore).GetMethod("DoAutomatedSave", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            internal static MethodInfo CallHookMethod()
            {
                return typeof(Interface).GetMethod("CallHook", new[] { typeof(string) });
            }
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> codes = new(instructions);
                MethodInfo callHookMethod = CallHookMethod();
                for (int i = 0; i < codes.Count; i++)
                {
                    if (i == 0 || codes[i].opcode != OpCodes.Call || !(codes[i].operand is MethodInfo currentMethod) || !currentMethod.Equals(callHookMethod))
                    {
                        continue;
                    }
                    if (codes[i - 1].opcode != OpCodes.Ldstr || !(codes[i - 1].operand is string arg) || arg != "OnServerSave")
                    {
                        continue;
                    }
                    codes[i].opcode = OpCodes.Call;
                    codes[i].operand = typeof(DelayOnServerSave).GetMethod("StartDelayedSave", BindingFlags.Static | BindingFlags.NonPublic);
                    break;
                }
                return codes;
            }
        }

        private static void StartDelayedSave()
        {
            if (_co != null)
            {
                ServerMgr.Instance.StopCoroutine(_co);
            }
            _co = ServerMgr.Instance.StartCoroutine(DelayedSaveCoroutine());
        }

        private static IEnumerator DelayedSaveCoroutine()
        {
            yield return CoroutineEx.waitForSeconds(1f);
           
            yield return new WaitWhile(() => SaveRestore.IsSaving);

            yield return CoroutineEx.waitForSeconds(1f);

            IDictionary hookSubscriptions = hookSubscriptionsFieldInfo.GetValue(Interface.Oxide.RootPluginManager) as IDictionary;

            if (hookSubscriptions != null)
            {
                foreach (DictionaryEntry entry in hookSubscriptions)
                {
                    if (entry.Value == null || entry.Key as string != "OnServerSave")
                    {
                        continue;
                    }
                    List<Plugin> plugins = pluginsPropertyInfo.GetValue(entry.Value) as List<Plugin>;
                    if (!plugins.IsNullOrEmpty())
                    {
                        foreach (Plugin plugin in plugins)
                        {
                            plugin.CallHook("OnServerSave");
                            yield return null;
                        }
                    }
                    break;
                }
            }

            _co = null;
        }
    }
}
