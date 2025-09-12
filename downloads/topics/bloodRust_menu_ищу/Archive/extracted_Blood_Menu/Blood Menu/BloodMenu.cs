using System.Collections.Generic;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace Oxide.Plugins
{
    [Info("BloodMenu", "[LimePlugin] Chibubrik", "1.0.0")]
    public class BloodMenu : RustPlugin
    {
        #region Вар
        string Layer = "Menu_UI";

        [PluginReference] Plugin ImageLibrary;

        Dictionary<ulong, string> PlayerButton = new Dictionary<ulong, string>(); 
        #endregion

        #region Класс
        public class ButtonSettings {
            [JsonProperty("Название кнопки")] public string DisplayName;
            [JsonProperty("Команда, которая будет исполняться по нажатию кнопки")] public string Command;
            [JsonProperty("Вызываемый плагин")] public string PluginName;
            [JsonProperty("Вызываемый метод из плагина")] public string MethodPlugin;
            [JsonProperty("Изображение кнопки (иконка)")] public string Url;
        }
        #endregion

        #region Конфиг
        private Configuration config;
        private class Configuration
        {
            [JsonProperty("Пункты в меню")] public List<ButtonSettings> settings = new List<ButtonSettings>();

            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    settings = new List<ButtonSettings>() {
                        new ButtonSettings() {
                            DisplayName = "Расписание вайпов",
                            Command = "wipe",
                            PluginName = "BloodSchedule",
                            MethodPlugin = "WipeUI",
                            Url = "https://i.postimg.cc/ZvTTdGqs/1alvRCw.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Корзина",
                            Command = "store",
                            PluginName = "GameStoresRUST",
                            MethodPlugin = "InitializeStore",
                            Url = "https://i.postimg.cc/WhCBLRvR/PvAhjYX.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Наёмники",
                            Command = "mercenaries",
                            PluginName = "BloodQuests",
                            MethodPlugin = "OpenUI",
                            Url = "https://i.postimg.cc/3dqT842c/bZbDoQN.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Батлпасс",
                            Command = "pass",
                            PluginName = "BloodPass",
                            MethodPlugin = "PassUI",
                            Url = "https://i.postimg.cc/k5yQpQFs/daf4c0a5d884eec1.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Точки телепортации",
                            Command = "tpmenu",
                            PluginName = "BloodDetonator",
                            MethodPlugin = "InitializeInterface",
                            Url = "https://i.postimg.cc/Mc72m9NC/HKDza3v.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Статистика",
                            Command = "stat",
                            PluginName = "BloodStats",
                            MethodPlugin = "StatUI",
                            Url = "https://i.postimg.cc/qhwP9zkQ/IFTIyD2.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Вайпблок",
                            Command = "block",
                            PluginName = "BloodBlock",
                            MethodPlugin = "BlockUI",
                            Url = "https://i.postimg.cc/68GNyW05/ABdA8uC.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Пожаловаться",
                            Command = "report",
                            PluginName = "BloodReport",
                            MethodPlugin = "ReportUI",
                            Url = "https://i.postimg.cc/RWGZLVt1/cramnDK.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Информация",
                            Command = "info",
                            PluginName = "BloodInfo",
                            MethodPlugin = "InfoUI",
                            Url = "https://i.postimg.cc/jwRVgzrL/hxa68rW.png"
                        },
                        new ButtonSettings() {
                            DisplayName = "Настройки",
                            Command = "chat",
                            PluginName = "BloodSettings",
                            MethodPlugin = "SettingsUI",
                            Url = "https://i.postimg.cc/K1Tdm9G1/uakjlj3.png"
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config?.settings == null) LoadDefaultConfig();
            }
            catch
            {
                PrintWarning($"Что то с этим конфигом не так! 'oxide/config/{Name}', создаём новую конфигурацию!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized() {
            foreach (var check in config.settings)
                cmd.AddChatCommand(check.Command, this, nameof(ChatMenu));

            foreach (var check in config.settings)
                ImageLibrary.Call("AddImage", check.Url, check.Url);

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void OnPlayerConnected(BasePlayer player) {
            if (!PlayerButton.ContainsKey(player.userID))
                PlayerButton.Add(player.userID, config.settings[0].Command);
            SteamAvatarAdd(player.UserIDString);
        }
        #endregion

        #region Команды
        [ChatCommand("menu")]
        void ChatPlayerMenu(BasePlayer player) {
            PlayerButton[player.userID] = "wipe";
            MenuUI(player);
        }

        void ChatMenu(BasePlayer player, string command, string[] args) {
            Puts(command);
            PlayerButton[player.userID] = command.ToLower();
            MenuUI(player);
        }

        [ConsoleCommand("menu")]
        void ConsoleMenu(ConsoleSystem.Arg args) {
            var player = args.Player();
            var command = PlayerButton[player.userID] = args.Args[0];
            ButtonUI(player, command);
            UI(player, command);
        }
        #endregion

        #region Интерфейс
        void MenuUI(BasePlayer player) {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                CursorEnabled = true,
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.9" }
            }, "Overlay", Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0.23", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer);
            
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Sprite = "assets/content/ui/ui.background.transparent.radial.psd", Color = HexToCuiColor("#363636", 60) }
            }, Layer);

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0.005 0.952", AnchorMax = "0.026 0.989", OffsetMax = "0 0" },
                Button = { Color = HexToCuiColor("#e0947a", 50), Close = Layer },
                Text = { Text = "✕", Color = HexToCuiColor("#e0947a", 100), Font = "robotocondensed-bold.ttf", FontSize = 20, Align = TextAnchor.MiddleCenter }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-390.5 -227", OffsetMax = "392 227" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "MenuLayer");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.889", AnchorMax = "0.273 1", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, "MenuLayer", "Profile");

            container.Add(new CuiElement
            {
                Parent = "Profile",
                Components = 
                {
                    new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", player.UserIDString) },
                    new CuiRectTransformComponent { AnchorMin = "0.035 0.128", AnchorMax = "0.21 0.85", OffsetMax = "0 0" },
                }
            });

            CuiHelper.AddUi(player, container);
            ProfileEXPUpdate(player);
            UI(player, PlayerButton[player.userID]);
            ButtonUI(player, PlayerButton[player.userID]);
        }

        void UI(BasePlayer player, string command) {
            CuiHelper.DestroyUi(player, "Menu_Block");
            var container = new CuiElementContainer();
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.282 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "MenuLayer", "Menu_Block");

            CuiHelper.AddUi(player, container);

            var check = config.settings.FirstOrDefault(z => z.Command == command);
            plugins?.Find(check.PluginName)?.CallHook(check.MethodPlugin, player);
        }

        void ButtonUI(BasePlayer player, string command) {
            CuiHelper.DestroyUi(player, "ButtonLayer");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.273 0.87" },
                Image = { Color = "0 0 0 0" }
            }, "MenuLayer", "ButtonLayer");

            float width = 0.997f, height = 0.085f, startxBox = 0f, startyBox = 1f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in config.settings)
            {
                string buttonColor = check.Command == command ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                string imageColor = check.Command == command ? HexToCuiColor("#45403b", 100) : "1 1 1 0.2";
                string text = check.Command == command ? $"<b><color=#45403b>{check.DisplayName}</color></b>" : check.DisplayName;
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMax = "0 0" },
                    Button = { Color = buttonColor, Command = $"menu {check.Command}", Material = "assets/content/ui/uibackgroundblur.mat" },
                    Text = { Text = "" }
                }, "ButtonLayer", "Button");
                xmin += width;
                if (xmin + width >= 0)
                {
                    xmin = startxBox;
                    ymin -= height + 0.0167f;
                }

                container.Add(new CuiElement
                {
                    Parent = "Button",
                    Components = 
                    {
                        new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", check.Url), Color = imageColor  },
                        new CuiRectTransformComponent { AnchorMin = "0.01 0.06", AnchorMax = "0.15 0.94", OffsetMin = "7 7", OffsetMax = "-7 -7" },
                    }
                });

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0.152 0", AnchorMax = "1 1" },
                    Text = { Text = text, Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                }, "Button");
            }

            CuiHelper.AddUi(player, container);
        }
        #endregion

        #region Хелпер
        public string HexToCuiColor(string HEX, float Alpha = 100)
        {
            if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

            var str = HEX.Trim('#');
            var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

            return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
        }

        void SteamAvatarAdd(string userid)
        {
            string url = "http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key=B23DC0D84302CF828713C73F35A30006&" + "steamids=" + userid;
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code == 200)
                {
                    string Avatar = (string)JObject.Parse(response)["response"]?["players"]?[0]?["avatarfull"];
                    ImageLibrary.Call("AddImage", Avatar, userid);
                }
            }, this);
        }

        void ProfileEXPUpdate(BasePlayer player) {
            CuiHelper.DestroyUi(player, ".EXP");
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.24 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=16>{player.displayName}</size></b>\n{plugins.Find("BloodQuests")?.Call("PlayerEXPQuests", (ulong) player.userID)} <color=#7f7d7d>EXP</color>", Color = HexToCuiColor("#cec5bb", 100), Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, "Profile", ".EXP");

            CuiHelper.AddUi(player, container);
        }
        #endregion
    }
}