using System;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using Rust;
using System.Collections.Generic;
using UnityEngine;
using Oxide.Core.Plugins;
using ConVar;

namespace Oxide.Plugins
{
    [Info("XDWipeReward", "DezLife", "0.1.0")]
    [Description("Награда первым N игрокам после вайпа")]
    public class XDWipeReward : RustPlugin
    {
        [PluginReference] Plugin IQChat;
        #region Config
        public Configuration config;

        public class Setings
        {
            [JsonProperty("Количество игроков")]
            public int PlayersIntConnect;

            [JsonProperty("Команда для выдачи приза (если не нужно то оставить поля пустым)")]
            public string CommandPrize;

            [JsonProperty("У вас магазин ОВХ?")]
            public bool OVHStore;

            [JsonProperty("Бонус в виде баланса GameStores или OVH (если не нужно оставить пустым)")]
            public string GameStoreBonus;

            [JsonProperty("Лог сообщения(Показывается в магазине после выдачи в истории. Если OVH оставить пустым)")]
            public string GameStoreMSG;

            [JsonProperty("Id Магазина(GameStore. Если OVH оставить пустым)")]
            public string Store_Id;
            [JsonProperty("API KEY Магазина(GameStore. Если OVH оставить пустым)")]
            public string Store_Key;
        }

        public class Configuration
        {
            [JsonProperty("Настройки")]
            public Setings setings;
        }


        protected override void LoadDefaultConfig()
        {
            config = new Configuration()
            {
                setings = new Setings
                {
                    PlayersIntConnect = 100,
                    CommandPrize = "say %STEAMID%",
                    OVHStore = false,
                    GameStoreBonus = "",
                    GameStoreMSG = "За заход после вайпа:3",
                    Store_Id = "ID",
                    Store_Key = "KEY"

                }
            };
            SaveConfig(config);
        }

        void SaveConfig(Configuration config)
        {
            Config.WriteObject(config, true);
            SaveConfig();
        }

        public void LoadConfigVars()
        {
            config = Config.ReadObject<Configuration>();
            Config.WriteObject(config, true);
        }
        #endregion

        private void OnServerInitialized()
        {
            LoadConfigVars();
            for (int i = 0; i < BasePlayer.activePlayerList.Count; i++)
            {
                OnPlayerInit(BasePlayer.activePlayerList[i]);
            }

            if (!string.IsNullOrEmpty(config.setings.GameStoreBonus) && !config.setings.OVHStore)
            {
                if (config.setings.Store_Id == "ID" || config.setings.Store_Key == "KEY")
                {
                    PrintError("Вы не настроили ID И KEY от магазина GameStores");
                    return;
                }
            }
        }

        void OnNewSave(string filename)
        {
            Wipe = true;
        }

        void OnPlayerInit(BasePlayer player)
        {
            if (player.IsReceivingSnapshot)
            {
                NextTick(() => OnPlayerInit(player));
                return;
            }

            if (Wipe)
            {
                if (Players.Count >= config.setings.PlayersIntConnect)
                {
                    Wipe = false;
                    return;
                }
                if (!Players.Contains(player.userID))
                {
                    Players.Add(player.userID);

                    WipeRewardGui(player);
                }
            }
        }

        [ConsoleCommand("giveprize")]
        void GivePrize(ConsoleSystem.Arg arg)
        {
            BasePlayer p = arg.Player();
            CuiHelper.DestroyUi(p, WipeR);

            if (!string.IsNullOrEmpty(config.setings.CommandPrize))
            {
                Server.Command(config.setings.CommandPrize.Replace("%STEAMID%", p.UserIDString));
            }
            if (!string.IsNullOrEmpty(config.setings.GameStoreBonus))
            {
                GiveReward(p.userID);
            }

            SendChat(p, "Вы успешно <color=#A1FF919A>забрали награду</color>!");
        }

       void GiveReward(ulong ID)
        {
           
                string url = $"https://gamestores.app/api?shop_id=32665&secret=7e96ca4803c0f38c3c7c1a83717a331e&action=moneys&type=plus&steam_id={ID}&amount=25&mess=За заход после вайпа:3";
                webrequest.Enqueue(url, null, (i, s) =>
                {
                    if (i != 200) { }
                    if (s.Contains("success"))
                    {
                        PrintWarning($"Игрок [{ID}] зашел 1 из первых, и получил бонус в нашем магазине. В виде [{config.setings.GameStoreBonus} руб]");
                    }
                    else
                    {
                        PrintWarning($"Игрок {ID} проголосовал за сервер, но не авторизован в магазине.");
                    }
                }, this);
        }



        #region Parent
        public static string WipeR = "WipeR_CUI";
        #endregion

        #region GUI

        public void WipeRewardGui(BasePlayer p)
        {
            CuiHelper.DestroyUi(p, WipeR);
            CuiElementContainer container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = false,
                RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-175 -340", OffsetMax = "-1 -280" },
                Image = { Color = "0 0 0 0.4", Material = "assets/content/ui/uibackgroundblur.mat", Sprite = "assets/content/ui/ui.background.tiletex.psd" }
            }, "Overlay", WipeR);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.1973175 0.01111111", AnchorMax = "0.7988508 0.3999999" },
                Button = { Command = "giveprize", Color = HexToRustFormat("#71FF9A9A") },
                Text = { Text = "Забрать награду", Align = TextAnchor.MiddleCenter, FontSize = 13 }
            }, WipeR);

            #region Title
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0 0.4444442", AnchorMax = "1 1" },
                Text = { Text = $"Вы {Players.Count} из {config.setings.PlayersIntConnect}\n Поэтому получаете награду", FontSize = 13, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter }

            }, WipeR);

            #endregion

            CuiHelper.AddUi(p, container);
        }

        #endregion

        #region Help

        List<ulong> Players = new List<ulong>();
        bool Wipe = false;
        private static string HexToRustFormat(string hex)
        {
            Color color;
            ColorUtility.TryParseHtmlString(hex, out color);
            return string.Format("{0:F2} {1:F2} {2:F2} {3:F2}", color.r, color.g, color.b, color.a);
        }

        public void SendChat(BasePlayer player, string Message)
        {
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, "");
            else player.SendConsoleCommand("chat.add", 0, Message);
        }

        #endregion

    }
}
