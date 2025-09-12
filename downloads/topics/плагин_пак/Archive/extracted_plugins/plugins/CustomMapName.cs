// Reference: 0Harmony
using HarmonyLib;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Custom Map Name", "Billy Joe", "1.0.1")]
    [Description("Allows you to customize your servers map name displayed in server browser.")]
    public class CustomMapName : CovalencePlugin
    {
        #region Defines
        private static Harmony _harmony;
        #endregion

        #region Config
        static Configuration config;
        public class Configuration
        {
            [JsonProperty(PropertyName = "Custom Map Name")] public string customMapName;
            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    customMapName = "Billy Joe's Custom Map"
                };
            }
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

        protected override void LoadDefaultConfig() => config = Configuration.DefaultConfig();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Hooks
        void Loaded()
        {
            if (_harmony == null) _harmony = new Harmony("com.BillyJoe.CustomMapName");
            _harmony.Patch(AccessTools.Method(typeof(ServerMgr), "UpdateServerInformation"), transpiler: new HarmonyMethod(typeof(CustomName), "Transpiler"));
        }

        void Unload()
        {
            _harmony.UnpatchAll("com.BillyJoe.CustomMapName");
            config = null;
        }
        #endregion

        #region Harmony
        private static class CustomName
        {
            internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                var list = instructions.ToList();
                int mapNameLine = list.FindLastIndex(i => i.opcode == OpCodes.Ldstr && (string)i.operand == "stok");
                if (mapNameLine == -1) return list;

                list[mapNameLine - 2].opcode = OpCodes.Ldstr;
                list[mapNameLine - 2].operand = config.customMapName;

                return list;
            }
        }
        #endregion
    }
}