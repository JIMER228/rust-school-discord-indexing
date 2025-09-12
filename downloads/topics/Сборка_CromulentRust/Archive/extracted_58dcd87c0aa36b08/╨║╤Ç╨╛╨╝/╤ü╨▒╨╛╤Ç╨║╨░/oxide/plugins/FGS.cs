
using Oxide.Core.Plugins;
using Newtonsoft.Json;
using System.Collections.Generic;
using System;

namespace Oxide.Plugins
{
    [Info("FGS", "FourTeen", "1.0.1")]
    internal class FGS : RustPlugin
    {
        int fake_online = 0;
        [PluginReference] private Plugin FLuma;

        #region Config
        internal class Configuration
        {
            [JsonProperty("CFG")]
            public Dictionary<Int32, Int32> ManualTimeOnline = new Dictionary<Int32, Int32>();
            public static Configuration Generate()
            {
                return new Configuration
                {

                    ManualTimeOnline = new Dictionary<int, int>
                    {
                        [00] = 0,
                        [01] = 0,
                        [02] = 0,
                        [03] = 0,
                        [04] = 0,
                        [05] = 0,
                        [06] = 0,
                        [07] = 0,
                        [08] = 0,
                        [09] = 0,
                        [10] = 0,
                        [11] = 0,
                        [12] = 0,
                        [13] = 0,
                        [14] = 0,
                        [15] = 0,
                        [16] = 0,
                        [17] = 0,
                        [18] = 0,
                        [19] = 0,
                        [20] = 0,
                        [21] = 0,
                        [22] = 0,
                        [23] = 0,
                    }
                };
            }
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                Settings = Config.ReadObject<Configuration>();
            }
            catch
            {
                PrintWarning($"Error reading config, creating one new config!");
                LoadDefaultConfig();
            }

            SaveConfig();
        }
        protected override void LoadDefaultConfig() => Settings = Configuration.Generate();
        protected override void SaveConfig() => Config.WriteObject(Settings);
        private static Configuration Settings;
        #endregion
        void OnServerInitialized()
        {
            LoadConfig();
        }
        int getFakes()
        {
            Int32 Time = DateTime.Now.Hour;
            Int32 ManualOnline = Settings.ManualTimeOnline.ContainsKey(Time) ? Settings.ManualTimeOnline[Time] : 0;
            fake_online = ManualOnline;
            return fake_online;
        }
    }
}
