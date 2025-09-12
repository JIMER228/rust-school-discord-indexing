// Copyright © 2023 Tryware OÜ. All rights reserved.

using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Better No Workbench", "Tryhard", "2.0.0")]
    [Description("Adds workbench access to players with permission")]
    public class BetterNoWorkbench : RustPlugin
    {
        #region Fields

        private const string InstanceId = "com.Tryhard.BetterNoWorkbench";
        private static Harmony _harmony;
        static BetterNoWorkbench instance;
        static string perm = "betternoworkbench.on";

        #endregion

        #region Oxide Hooks 

        private void OnServerInitialized()
        {
            permission.RegisterPermission(perm, this);

            instance = this;

            if (_harmony == null)
                _harmony = new Harmony(InstanceId);

            _harmony.PatchAll();
        }

        private void Unload()
        {
            _harmony.UnpatchAll(InstanceId);

            instance = null;
        }

        #endregion

        #region Methods

        private static float GetLevel(float current, BasePlayer player)
        {
            if (player == null || !instance.permission.UserHasPermission(player.UserIDString, perm)) return current;

            return Mathf.Max(current, config.wbLevel);
        }

        #endregion

        #region Harmony

        [HarmonyPatch(typeof(BasePlayer), "get_currentCraftLevel")]
        public class BasePlayer_currentCraftLevel_Patch
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il)
            {
                foreach (var instruction in instructions)
                {
                    if (instruction.opcode == OpCodes.Ret)
                    {
                        yield return new CodeInstruction(OpCodes.Ldarg_0);
                        yield return new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(BetterNoWorkbench), nameof(GetLevel)));
                    }

                    yield return instruction;
                }
            }

        }

        #endregion

        #region Config

        static Configuration config;
        public class Configuration
        {
            [JsonProperty("Default Workbench Level")]
            public float wbLevel = 3f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion
    }
}