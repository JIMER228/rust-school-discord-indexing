using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("MyMiniUI", "MaltrzD", "0.0.2")]
    public class MyMiniUI : RustPlugin
    {
        [PluginReference] Plugin ImageLibrary;
        private ConfigData configuration;

        public string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary.Call("GetImage", shortname, skin);
        public bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);


        #region OXIDE HOOKS
        private void Loaded() => ReadConfig();
        private void OnPlayerConnected(BasePlayer player)
        {
            CuiHelper.AddUi(player, AddMyMiniUI());
        }
        private void OnServerInitialized()
        {
            AddImage(configuration.imageUrl, "MyMiniBtn");

            ListHashSet<BasePlayer> players = BasePlayer.activePlayerList;
            foreach (var player in players)
            {
                CuiHelper.DestroyUi(player, "SwitchMyMiniImage");

                CuiHelper.AddUi(player, AddMyMiniUI());
            }
        }
        #endregion

        [ConsoleCommand("switchmini")]
        private void SwitchMini(ConsoleSystem.Arg args)
        {
            BasePlayer player = args?.Player() ?? null;

            if (player != null)
            {
                player.SendConsoleCommand("chat.say /switchmini");
            }
        }

        #region METHODS
        private CuiElementContainer AddMyMiniUI()
        {
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiElement
            {
                Components = {
                    new CuiRawImageComponent
                    {
                        Png = GetImage("MyMiniBtn"),
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = configuration.anchorMin,
                        AnchorMax = configuration.anchorMax
                    }
                },
                Parent = "Overlay",
                Name = "SwitchMyMiniImage"
            });
            container.Add(new CuiButton
            {
                Button =
                {
                    Color = "1 1 1 0",
                    Command = "switchmini"
                },
                RectTransform =
                {
                    AnchorMin = "0 0",
                    AnchorMax = "1 1"
                }
            }, "SwitchMyMiniImage", "SwitchMyMiniBtn");

            return container;
        }
        #endregion

        #region CONFIGURATION
        class ConfigData
        {
            public string anchorMin = "0.2937501 0.025";
            public string anchorMax = "0.340625 0.1083333";
            [JsonProperty("Ссылка на картинку кнопки")] public string imageUrl = "https://i.imgur.com/LHSEZ5k.png";
        }
        protected override void LoadDefaultConfig()
        {
            var config = new ConfigData();
            SaveConfig(config);
        }
        void SaveConfig(object config)
        {
            Config.WriteObject(config, true);
        }
        void ReadConfig()
        {
            base.Config.Settings.ObjectCreationHandling = ObjectCreationHandling.Replace;
            configuration = Config.ReadObject<ConfigData>();
            SaveConfig(configuration);
        }
        #endregion
    }
}
