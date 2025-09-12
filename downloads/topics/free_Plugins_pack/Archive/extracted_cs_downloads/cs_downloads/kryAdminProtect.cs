using System.Collections.Generic;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
	[Info("kryAdminProtect", "xkrystalll", "1.0.0")]
	class kryAdminProtect : RustPlugin
	{
        #region Hooks
        void Loaded()
        {
            LoadCfg();
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                if(cfg.MainSettings.trustedPlayers.Contains(player.UserIDString))
                {
                    return;
                }
                player.Kick("你不在名单上");
            }
        }
        #endregion

        #region Config
        private PluginConfig cfg;

        public class PluginConfig
        {
            [JsonProperty("Основные настройки")]
            public Settings MainSettings = new Settings();
            public class Settings
            {
                [JsonProperty("Включить защиту ownerid?")]
                public bool protectEnabled = true;
                [JsonProperty("Игроки, у которых есть разрешение на использование ownerid (steam64)")]
                public List<string> trustedPlayers = new List<string>()
                {
                    "steamid1",
                    "steamid2"
                };
            }
        }

        void LoadCfg() 
        {
            cfg = Config.ReadObject<PluginConfig>();
        }
        void SaveConfig(object config) => Config.WriteObject(config, true);
        protected override void LoadDefaultConfig()
        {
            var config = new PluginConfig();
            SaveConfig(config);
        } 
        #endregion
    }
}