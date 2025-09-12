using System.Collections.Generic;
using System.Linq;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Globalization;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("BloodInfo", "[LimePlugin] Chibubrik", "1.0.0")]
    public class BloodInfo : RustPlugin
    {
        #region Вар
        string Layer = "Info_UI";

        [PluginReference] Plugin ImageLibrary;

        Dictionary<ulong, string> PlayerButton = new Dictionary<ulong, string>(); 
        #endregion

        #region Класс
        public class InfoSettings {
            [JsonProperty("Текст информации")] public string Text;
            [JsonProperty("Изображение к информации")] public string Url;
            [JsonProperty("Текст команды")] public string CommandText;
            [JsonProperty("Цвет кнопки и текста (не знаешь, не лезь)")] public string CommandColor;
            [JsonProperty("Консольная команда")] public string Command;
            [JsonProperty("Положение картинки (AnchorUrlMin)")] public string AnchorUrlMin;
            [JsonProperty("Положение картинки (AnchorUrlMax)")] public string AnchorUrlMax;
            [JsonProperty("Положение блока консольной команды (AnchorCommandMin)")] public string AnchorCommandMin;
            [JsonProperty("Положение блока консольной команды (AnchorCommandMax)")] public string AnchorCommandMax;
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("Список со всей информацией")] public Dictionary<string, Dictionary<int, InfoSettings>> settings;
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    settings = new Dictionary<string, Dictionary<int, InfoSettings>>() {
                        ["Команды"] = new Dictionary<int, InfoSettings>() {
                            [0] = new InfoSettings() {
                                Text = "<b><size=16>Почему так мало команд?</size></b>",
                                Url = null,
                                CommandText = null,
                                CommandColor = null,
                                Command = null,
                                AnchorUrlMin = null,
                                AnchorUrlMax = null,
                                AnchorCommandMin = null,
                                AnchorCommandMax = null
                            }
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
            ImageLibrary.Call("AddImage", "https://i.imgur.com/X0wgen6.png", "X0wgen6");
            foreach (var check in config.settings) {
                foreach (var item in check.Value) {
                    if (item.Value.Url != null)
                        ImageLibrary.Call("AddImage", item.Value.Url, item.Value.Url);
                }
            }
            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);
        }

        void OnPlayerConnected(BasePlayer player)
        {
            NextTick(() => {
                if (!PlayerButton.ContainsKey(player.userID))
                    PlayerButton.Add(player.userID, config.settings.ElementAt(0).Key);
            });
        }
        #endregion

        #region Команда
        [ConsoleCommand("info")]
        void ConsoleInfo(ConsoleSystem.Arg args) {
            var player = args.Player();
            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "command") {
                    string command = "";
                    for (int z = 1; z < args.Args.Length; z++) {
                        command += args.Args[z] + " ";
                    }

                    command = command.TrimEnd();

                    if (PlayerButton[player.userID] != command) {
                        PlayerButton[player.userID] = command;
                        InfoUI(player);
                    }
                }
                if (args.Args[0] == "skip") {
                    TextInfoUI(player, args.Args[1], int.Parse(args.Args[2]));
                }
            }
        }
        #endregion

        #region Интерфейс
        void InfoUI(BasePlayer player) {
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
                if (item.Key != null)
                {
                    string buttonColor = item.Key == PlayerButton[player.userID] ? HexToCuiColor("#b2a9a3", 100) : "1 1 1 0.04";
                    string imageColor = item.Key == PlayerButton[player.userID] ? HexToCuiColor("#45403b", 100) : "1 1 1 0.2";
                    string text = item.Key == PlayerButton[player.userID] ? $"<b><color=#45403b>{item.Key}</color></b>" : item.Key;
                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}" },
                        Button = { Color = buttonColor, Command = $"info command {item.Key}", Material = "assets/content/ui/uibackgroundblur.mat" },
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
                            new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", "X0wgen6"), Color = imageColor  },
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

            CuiHelper.AddUi(player, container);
            TextInfoUI(player, PlayerButton[player.userID]);
        }

        void TextInfoUI(BasePlayer player, string name, int page = 0) {
            CuiHelper.DestroyUi(player, ".Info");
            CuiHelper.DestroyUi(player, "SkipBack");
            CuiHelper.DestroyUi(player, "SkipUp");
            var container = new CuiElementContainer();

            var check = config.settings.FirstOrDefault(z => z.Key == name);
            string anchor = check.Value.Count > 1 ? "0.09" : "0";
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.284 {anchor}", AnchorMax = "1 1" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer, ".Info");

            if (check.Value.Count > 1) {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.284 0", AnchorMax = "0.637 0.077" },
                    Button = { Color = page != 0 ? "1 1 1 0.04" : "1 1 1 0.02", Material = "assets/content/ui/uibackgroundblur.mat", Command = page != 0 ? $"info skip {name} {page - 1}" : "" },
                    Text = { Text = "←", Color = page != 0 ? "1 1 1 0.7" : "1 1 1 0.2", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, Layer, "SkipBack");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.647 0", AnchorMax = "1 0.077" },
                    Button = { Color = check.Value.Count() > (page + 1) ? "1 1 1 0.04" : "1 1 1 0.02", Material = "assets/content/ui/uibackgroundblur.mat", Command = check.Value.Count() > (page + 1) ? $"info skip {name} {page + 1}" : "" },
                    Text = { Text = "→", Color = check.Value.Count() > (page + 1) ? "1 1 1 0.7" : "1 1 1 0.2", FontSize = 14, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf" }
                }, Layer, "SkipUp");
            }

            var item = check.Value[page];
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 0.96" },
                Text = { Text = item.Text, Color = "1 1 1 0.2", Align = TextAnchor.UpperLeft, Font = "robotocondensed-regular.ttf" }
            }, ".Info");

            if (item.Url != null) {
                container.Add(new CuiElement
                {
                    Parent = $".Info",
                    Components = 
                    {
                        new CuiRawImageComponent { Png = (string)ImageLibrary.Call("GetImage", item.Url)  },
                        new CuiRectTransformComponent { AnchorMin = item.AnchorUrlMin, AnchorMax = item.AnchorUrlMax },
                    }
                });
            }

            if (item.Command != null) {
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = item.AnchorCommandMin, AnchorMax = item.AnchorCommandMax },
                    Button = { Color = HexToCuiColor(item.CommandColor, 25), Command = item.Command },
                    Text = { Text = item.CommandText, Color = HexToCuiColor(item.CommandColor, 100), FontSize = 13, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-bold.ttf" }
                }, ".Info");
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
        #endregion
    }
}