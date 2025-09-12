using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("BloodSettings", "[LimePlugin] Chibubrik", "1.0.0")]
    public class BloodSettings : RustPlugin
    {
        #region Вар
        string Layer = "Settings_UI";

        [PluginReference] Plugin ImageLibrary, BloodChat;

        Dictionary<ulong, string> PlayerButton = new Dictionary<ulong, string>(); 
        #endregion

        #region Класс
        public class Settings {
            [JsonProperty("Название кнопки")] public string DisplayName; 
            [JsonProperty("Команда, которая будет исполняться по нажатию кнопки")] public string Command; 
            [JsonProperty("Изображение кнопки(иконка)")] public string Url; 
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("Список со всей информацией")] public List<Settings> settings;
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    settings = new List<Settings>() {
                        new Settings() {
                            DisplayName = "Чат",
                            Command = "chat",
                            Url = "https://i.postimg.cc/jWrB2z2Z/X0wgen6.png"
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
                PrintWarning($"Ошибка чтения конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewCong();
        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        #region Хуки
        void OnServerInitialized()
        {
            foreach (var check in config.settings)
                ImageLibrary.Call("AddImage", check.Url, check.Url);

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            NextTick(() => {
                if (!PlayerButton.ContainsKey(player.userID))
                    PlayerButton.Add(player.userID, config.settings.ElementAt(0).Command);
            });
        }
        #endregion

        #region Команда
        [ConsoleCommand("settings")]
        void ConsoleInfo(ConsoleSystem.Arg args) {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "command") {
                    string command = args.Args[1];
                    if (PlayerButton[player.userID] != command) {
                        PlayerButton[player.userID] = command;
                        SettingsUI(player);
                    }
                }
            }
        }
        #endregion

        #region Интерфейс
        void SettingsUI(BasePlayer player) {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu_Block", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "0.273 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Buttons");

            float width = 0.996f, height = 0.077f, startxBox = 0f, startyBox = 0.996f - height, xmin = startxBox, ymin = startyBox;
            foreach (var i in Enumerable.Range(0, 11)) {
                var item = config.settings.ElementAtOrDefault(i);
                if (item != null)
                {
                    string buttonColor = item.Command == PlayerButton[player.userID] ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                    string imageColor = item.Command == PlayerButton[player.userID] ? HexToCuiColor("#45403b", 100) : "1 1 1 0.2";
                    string text = item.Command == PlayerButton[player.userID] ? $"<b><color=#45403b>{item.DisplayName}</color></b>" : item.DisplayName;
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Button = { Color = buttonColor, Command = $"settings command {item.Command}", Material = "assets/content/ui/uibackgroundblur.mat" },
                        Text = { Text = "" }
                    }, "Buttons", "Button");
                    xmin += width + 0.02f;
                    if (xmin + width >= 0)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.015f;
                    }

                    container.Add(new CuiElement
                    {
                        Parent = "Button",
                        Components = 
                        {
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", item.Url), Color = imageColor  },
                            new CuiRectTransformComponent { AnchorMin = "0.01 0.06", AnchorMax = "0.22 0.94", OffsetMin = "8 8", OffsetMax = "-8 -8" },
                        }
                    });

                    container.Add(new CuiLabel
                    {
                        RectTransform = { AnchorMin = "0.19 0", AnchorMax = "1 1" },
                        Text = { Text = text, Color = "1 1 1 0.2", Align = TextAnchor.MiddleLeft, Font = "robotocondensed-regular.ttf" }
                    }, "Button");
                }
                else {
                    container.Add(new CuiPanel
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Image = { Color = "1 1 1 0.02", Material = "assets/content/ui/uibackgroundblur.mat" }
                    }, "Buttons");
                    xmin += width + 0.02f;
                    if (xmin + width >= 0)
                    {
                        xmin = startxBox;
                        ymin -= height + 0.015f;
                    }
                }
            }

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0.284 0", AnchorMax = "1 1" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "Settings");

            CuiHelper.AddUi(player, container);


            if (PlayerButton[player.userID] == "chat")
                BloodChat?.Call("ChatUI", player);
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
        #endregion
    }
}