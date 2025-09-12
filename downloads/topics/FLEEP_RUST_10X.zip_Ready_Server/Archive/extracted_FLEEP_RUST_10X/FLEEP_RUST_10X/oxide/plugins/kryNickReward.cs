using Newtonsoft.Json;
using System.Collections.Generic;   

namespace Oxide.Plugins
{
    
    [Info("kryNickReward", "https://discord.gg/9vyTXsJyKR", "1.0.0")]
    class kryNickReward : RustPlugin
    {
        private PluginConfig cfg;
        private List<string> permGives = new List<string>();
        private List<string> nickAdds = new List<string>();
        private List<string> groupsGives = new List<string>();
        public class PluginConfig
        {
            [JsonProperty("Включить награду за ник?")]
            public bool isEnabled = true;
            [JsonProperty("Включить выдачу прав?")]
            public bool isPerm = true;
            [JsonProperty("Включить выдачу групп?")]
            public bool isGroup = true;
            [JsonProperty("Какие права выдавать, если в нике есть слова ниже? (может быть пустым)")]
            public string[] permGives = {"permission.one", "permission.two"};
            [JsonProperty("Какие группы выдавать, если в нике есть слова ниже? (может быть пустым)")]
            public string[] groupsGives = {"group1", "group2"};
            [JsonProperty("Какие должны быть слова в нике?")]
            public string[] nickAdds = {"testplugin", "topserver"};
            [JsonProperty("Забирать права, если человек зашёл без приставки?")]
            public bool revokePerms = true;
        }
        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new PluginConfig(), true);
        }
        void Loaded()
        {
            cfg = Config.ReadObject<PluginConfig>();
            Config.WriteObject(cfg);
            NextTick(() => {
                foreach (string groupadd in cfg.groupsGives) { groupsGives.Add(groupadd); }
                foreach (string perm in cfg.permGives) { permGives.Add(perm); }
                foreach (string addn in cfg.nickAdds) { nickAdds.Add(addn); }
            });
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (!cfg.isEnabled){ return; }
            if (nickAdds.IsEmpty()) { PrintError("Enter the prefix for the nickname!"); return; }
            string playernick = player.displayName.ToLower();

            foreach (string nickadd in nickAdds)
            {
                if (playernick.Contains(nickadd.ToLower()))
                {
                    if (cfg.isGroup) { foreach (string group in groupsGives) { player.IPlayer.AddToGroup(group); } }
                    if (cfg.isPerm) { foreach (string perms in permGives){ player.IPlayer.GrantPermission(perms); } }
                    return;
                }
            }
            if (!cfg.revokePerms) { return; }
            foreach (string perms in permGives) { player.IPlayer.RevokePermission(perms); }
        }
    }
}