// Copyright © 2023 Tryware OÜ. All rights reserved.

using HarmonyLib;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;
using UnityEngine;
using OpCodes = System.Reflection.Emit.OpCodes;

namespace Oxide.Plugins
{
    [Info("Always Bonus", "Tryhard", "1.1.2")]
    [Description("Hits X markers on trees and stars on nodes automatically")]
    public class AlwaysBonus : RustPlugin
    {
        #region Fields
        private const string InstanceId = "com.Tryhard.AlwaysBonus";
        private static Harmony _harmony;
        #endregion

        #region Harmony
        private void Loaded()
        {
            if (_harmony == null)
                _harmony = new Harmony(InstanceId);

            if (config.nodex)
                _harmony.Patch(AccessTools.Method(typeof(OreResourceEntity), nameof(OreResourceEntity.OnAttacked)), transpiler: new HarmonyMethod(typeof(OreResourceEntity_Patch), nameof(OreResourceEntity_Patch.Transpiler)));

            if (config.treex)
                _harmony.Patch(AccessTools.Method(typeof(TreeEntity), nameof(TreeEntity.DidHitMarker)), transpiler: new HarmonyMethod(typeof(TreeEntity_Patch), nameof(TreeEntity_Patch.Transpiler)));
        }

        private void Unload() => _harmony.UnpatchAll(InstanceId);

        public static class OreResourceEntity_Patch
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = new List<CodeInstruction>(instructions);

                foreach (var instruction in list)
                {
                    if (instruction.opcode == OpCodes.Ldc_R4 && (float)instruction.operand == 1.5f)
                    {
                        instruction.operand = 25f;
                        return list;
                    }
                }

                return list;
            }
        }

        public static class TreeEntity_Patch
        {
            [HarmonyTranspiler]
            public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                List<CodeInstruction> list = new List<CodeInstruction>()
                {
                    new CodeInstruction(OpCodes.Ldc_I4_1),
                    new CodeInstruction(OpCodes.Ret)
                };

                return list;
            }
        }

        #endregion

        #region Config
        static Configuration config;
        public class Configuration
        {
            [JsonProperty("Enable atuo tree X farming")]
            public bool treex = true;

            [JsonProperty("Enable atuo node star farming")]
            public bool nodex = true;
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