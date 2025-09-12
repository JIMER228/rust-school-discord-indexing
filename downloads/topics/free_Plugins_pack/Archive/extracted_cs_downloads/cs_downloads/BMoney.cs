using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections;

namespace Oxide.Plugins
{
	[Info("BMoney", "King", "1.0.0")]
	public class BMoney : RustPlugin
	{
        #region [Vars]
        [PluginReference] private Plugin ImageLibrary = null;
        private List<ulong> openUI = new List<ulong>();
        private const string Layer = "BMoney.Layer";
        #endregion
    
        #region [ImageLibrary]
        private bool HasImage(string imageName, ulong imageId = 0) => (bool)ImageLibrary.Call("HasImage", imageName, imageId);
        private bool AddImage(string url, string shortname, ulong skin = 0) => (bool)ImageLibrary?.Call("AddImage", url, shortname, skin);
        private string GetImage(string shortname, ulong skin = 0) => (string)ImageLibrary?.Call("GetImage", shortname, skin);
        #endregion

        #region [Data]
        Dictionary<ulong, playerData> _playerList = new Dictionary<ulong, playerData>();

		public class playerData
		{
			public int Second;

            public int Taken;
        }

		private playerData GetPlayerData(ulong member)
		{
			if (!_playerList.ContainsKey(member))
				_playerList.Add(member, new playerData());

			return _playerList[member];
		}

		private void SavePlayer()
		{
			Interface.Oxide.DataFileSystem.WriteObject($"{Name}/PlayerList", _playerList);
		}

		private void LoadPlayer()
		{
			try
			{
				_playerList = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, playerData>>($"{Name}/PlayerList");
			}
			catch (Exception e)
			{
				PrintError(e.ToString());
			}

			if (_playerList == null) _playerList = new Dictionary<ulong, playerData>();
		}
        #endregion

        #region [Oxide]
		private void Init()
		{
			LoadPlayer();
		}

        private void OnServerInitialized()
        {
            AddImage("https://pic.moscow.ovh/images/2024/12/11/fb5d68a643e07f412e5bee88a2c0abff.png", $"{Name}.ImageFon");
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }

            timer.Every(1, TimeHandle);
        }

		private void Unload()
		{
			foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, Layer);
            }

			SavePlayer();
		}

		private void OnNewSave(string filename)
		{
            foreach (var playerData in _playerList)
            {
                playerData.Value.Second = 0;
                playerData.Value.Taken = 0;
            }

            SavePlayer();
		}
        #endregion

        #region [Rust]
        private void OnPlayerConnected(BasePlayer player)
        {
            var data = GetPlayerData(player.userID);
            if (data == null) return;

            if (data.Taken < config._Settings.howTakeBonus)
            {
                if (!openUI.Contains(player.userID))
                    openUI.Add(player.userID);
                MainUi(player, "all");
            }
        }

		private void OnPlayerDisconnected(BasePlayer player)
		{
            if (openUI.Contains(player.userID))
                openUI.Remove(player.userID);
		}
        #endregion

        #region [UI]
        private void MainUi(BasePlayer player, String Type)
        {
            switch(Type)
            {
                case "all":
                {
                    var container = new CuiElementContainer();

                    var data = GetPlayerData(player.userID);
                    if (data == null) return;

                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-141 -211", OffsetMax = "0 -168" },
                        Image = { Color = "0 0 0 0.25", Sprite = "assets/content/ui/ui.background.tiletex.psd" }
                    }, "Overlay", Layer);

                    container.Add(new CuiElement
                    {
                        Parent = Layer,
                        Components =
                        {
                            new CuiRawImageComponent { Png = GetImage($"{Name}.ImageFon") },
                            new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{config._Settings.playerBonus}р на баланс", Color = "1 1 1 0.85", FontSize = 16, Font = "robotocondensed-bold.ttf", Align = TextAnchor.UpperCenter },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.98" },
                    }, Layer);

                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{GetFormatTime(TimeSpan.FromSeconds(config._Settings.howPlay - data.Second))}", Color = "1 1 1 0.85", FontSize = 8, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.86" },
                    }, Layer, Layer + ".TextUpdate");

                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{config._Settings.nameShop}", Color = "1 1 1 0.85", FontSize = 8, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.43" },
                    }, Layer);

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "-0.1 0", AnchorMax = "0 1" },
                        Button = { Color = "1 1 1 0", Command = "BMoney_UI" },
                        Text = { Text = "›", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.65" }
                    }, Layer);

                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.AddUi(player, container);
                    break;
                }
                case "openMenu":
                {   
                    var container = new CuiElementContainer();

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = "1 1", AnchorMax = "1 1", OffsetMin = "-25 -211", OffsetMax = "0 -168" },
                        Button = { Color = "1 1 1 0", Command = "BMoney_UI" },
                        Text = { Text = "‹", FontSize = 22, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter, Color = "1 1 1 0.65" }
                    }, "Overlay", Layer);

                    CuiHelper.DestroyUi(player, Layer);
                    CuiHelper.AddUi(player, container);
                    break;
                }
            }
        }
        #endregion

        #region [ConsoleCommand]
        [ConsoleCommand("BMoney_UI")]
        private void cmdBMoney(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (openUI.Contains(player.userID))
            {
				openUI.Remove(player.userID);
                MainUi(player, "openMenu");
            }
            else
            {
				openUI.Add(player.userID);
                MainUi(player, "all");
            }
        }
        #endregion

        #region [Functional]
		private void TimeHandle()
		{
			foreach (var player in BasePlayer.activePlayerList) 
            {
                var data = GetPlayerData(player.userID);
                if (data == null || data.Taken >= config._Settings.howTakeBonus) continue;

                if (openUI.Contains(player.userID))
                {
                    var container = new CuiElementContainer();

                    container.Add(new CuiLabel
                    {
                        Text = { Text = $"{GetFormatTime(TimeSpan.FromSeconds(config._Settings.howPlay - data.Second))}", Color = "1 1 1 0.85", FontSize = 8, Font = "robotocondensed-regular.ttf", Align = TextAnchor.MiddleCenter },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.86" },
                    }, Layer, Layer + ".TextUpdate");

                    CuiHelper.DestroyUi(player, Layer + ".TextUpdate");
                    CuiHelper.AddUi(player, container);
                }

                data.Second++;
                if (data.Second >= config._Settings.howPlay)
                {
                    RewardPlayer(player);
                    data.Second = 0;
                    data.Taken++;
                    if (data.Taken >= config._Settings.howTakeBonus)
                    {
                        CuiHelper.DestroyUi(player, Layer);
                        if (openUI.Contains(player.userID))
                            openUI.Remove(player.userID);
                    }
                }
            }
		}

        private void RewardPlayer(BasePlayer player)
        {
            string request = $"http://gamestores.ru/api?shop_id={config._Settings.shopID}&secret={config._Settings.secretKeyShop}&action=moneys&type=plus&steam_id={player.userID}&amount={config._Settings.playerBonus}&mess=ActiveBonus";
            webrequest.Enqueue(request, null, (code, response) =>
            {
                if (response.Contains("fail"))
                {
                    player.ChatMessage($"Вы не получили {config._Settings.playerBonus} руб на баланс");
                }
                else
                {
                    player.ChatMessage($"Вы получили {config._Settings.playerBonus} руб на баланс");
                }
            }, this, Core.Libraries.RequestMethod.GET);
        }

        private string GetFormatTime(TimeSpan timespan)
        {
            return string.Format(timespan.TotalHours >= 1 ? "{2:00}:{0:00}:{1:00}" : "{0:00}:{1:00}", timespan.Minutes, timespan.Seconds, System.Math.Floor(timespan.TotalHours));
        }
        #endregion

        #region [Config]
        private PluginConfig config;

        protected override void LoadDefaultConfig()
        {
            config = PluginConfig.DefaultConfig();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            config = Config.ReadObject<PluginConfig>();

            if (config.PluginVersion < Version)
                UpdateConfigValues();

            Config.WriteObject(config, true);
        }

        private void UpdateConfigValues()
        {
            PluginConfig baseConfig = PluginConfig.DefaultConfig();
            if (config.PluginVersion < Version)
            {
                config.PluginVersion = Version;
                if (Version == new VersionNumber(1, 0, 0))
                {
                    //
                }

                PrintWarning("Config checked completed!");
            }
            config.PluginVersion = Version;
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        private class PluginConfig
        {
            [JsonProperty("Основые настройки плагина")]
            public Settings _Settings;

            [JsonProperty("Config version")]
            public VersionNumber PluginVersion = new VersionNumber();

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    _Settings = new Settings()
                    {
                        shopID = string.Empty,
                        secretKeyShop = string.Empty,
                        nameShop = "bolotorust.xyz",
                        playerBonus = 60,
                        howPlay = 3600,
                        howTakeBonus = 2,
                        Command = "money",
                    },
                    PluginVersion = new VersionNumber()
                };
            }
        }

        public class Settings
        {
            [JsonProperty("ID магазина в сервисе")]
            public string shopID;

            [JsonProperty("Секретный ключ магазина в сервисе")]
            public string secretKeyShop;

            [JsonProperty("Ссылка на магазин ( bolotorust.xyz )")]
            public string nameShop;

            [JsonProperty("Бонус в виде баланса для игрока")]
            public int playerBonus;

            [JsonProperty("Сколько нужно отыграть чтобы получить бонус ( Секунды )")]
            public int howPlay;

            [JsonProperty("Сколько раз можно получить бонус")]
            public int howTakeBonus;

            [JsonProperty("Команда для открытия меню плагина FMoney")]
            public string Command;
        }
        #endregion
    }
}