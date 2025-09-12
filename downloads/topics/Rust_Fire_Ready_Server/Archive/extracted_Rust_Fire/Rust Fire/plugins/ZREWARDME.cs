using System.Collections.Generic;
using Oxide.Core;
using System.Linq;
using Rust;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("ZREWARDME", "fermens", "0.0.5")]
    [Description("Награда за игру на серевере с тегом и без")]
    class ZREWARDME : RustPlugin
    {
        #region Config
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }
        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();
        }
        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Сколько нужно провести времени на сервере?")]
            public float connectingtime;

            [JsonProperty("Допустимые теги [писать с нижним регистром]")]
            public string[] tags;

            [JsonProperty("Награда(ы)")]
            public string[] rewards;

            [JsonProperty("Сообщение в чат [после получения награды]")]
            public string message;

            [JsonProperty("Сообщение в чат [при входе на сервер]")]
            public string messagejoin;

            [JsonProperty("Бонус к добычи [додаеться к основному рейту]")]
            public float gbonus;

            [JsonProperty("Обнулять каждый вайп список игроков, которые получили награды?")]
            public bool wipe;

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    connectingtime = 1800f,
                    tags = new string[] { "haxlite", "hxlite" },
                    message = "<color=yellow>ВЫ ПОЛУЧИЛИ ПРИВИЛЕГИЮ GOD НА 1 ДЕНЬ.\nСПАСИБО ЗА ТО, ЧТО ИГРАЕТЕ У НАС И С НАШИМ ТЕГОМ.</color>",
                    rewards = new string[] { "addgroup {steamid} god 1d" },
                    wipe = false,
                    gbonus = 0.2f
                };
            }
        }
        #endregion

        Dictionary<BasePlayer, Timer> controller = new Dictionary<BasePlayer, Timer>();
        List<ulong> rewarded = new List<ulong>();
        List<string> HasTag = new List<string>();
        bool wipe;

        private void OnNewSave(string filename)
        {
            if (!config.wipe) return;
            if (rewarded.Count == 0) wipe = true;
            else rewarded.Clear();
        }

        private void OnServerInitialized()
        {
            if(config.messagejoin == null)
            {
                config.messagejoin = "<color=yellow>За игру с нашим тегом вы получаете +0.2 к добычи.</color>";
                SaveConfig();
            }


            if (!wipe) rewarded = Interface.Oxide.DataFileSystem.ReadObject<List<ulong>>("rewardtag");
            else wipe = false;
            foreach (BasePlayer player in BasePlayer.activePlayerList) OnPlayerConnected(player);
        }

        private void Unload()
        {
            if (rewarded != null && rewarded.Count > 0) Interface.Oxide.DataFileSystem.WriteObject("rewardtag", rewarded);
            foreach (var z in controller) z.Value?.Destroy();
            controller.Clear();
        }

        [PluginReference] private Plugin XRate;

        private void OnPlayerConnected(BasePlayer player)
        {
            string name = player.displayName.ToLower();
            if (!config.tags.Any(x => name.Contains(x))) return;
            if (!HasTag.Contains(player.UserIDString)) HasTag.Add(player.UserIDString);
            if (XRate != null)
            {
                XRate.Call("getuserrate", player.UserIDString, config.gbonus);
                player.ChatMessage(config.messagejoin);
            }
            if (!rewarded.Contains(player.userID))
            {
                DESRTOYIT(player);
                controller.Add(player, timer.Once(config.connectingtime, () => REWARDME(player)));
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (HasTag.Contains(player.UserIDString)) HasTag.Remove(player.UserIDString);
            DESRTOYIT(player);
        }

        private void REWARDME(BasePlayer player)
        {
            if (!DESRTOYIT(player) || !player.IsConnected) return;
            foreach (var z in config.rewards) Server.Command(z.Replace("{steamid}", player.UserIDString));
            player.ChatMessage(config.message);
            rewarded.Add(player.userID);
        }

        private bool DESRTOYIT(BasePlayer player)
        {
            Timer time;
            if (controller.TryGetValue(player, out time))
            {
                time?.Destroy();
                controller.Remove(player);
                return true;
            }
            return false;
        }

        #region БОНУС ДОБЫЧИ
        float APIBONUS(string id)
        {
            if (!HasTag.Contains(id)) return 0f;
            return config.gbonus;
        }
        #endregion
    }
}
