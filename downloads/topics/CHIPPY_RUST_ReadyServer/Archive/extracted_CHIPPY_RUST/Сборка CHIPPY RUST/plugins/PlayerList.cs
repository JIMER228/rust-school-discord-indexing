using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Player List", "Mevent", "1.4.0")]
    [Description("Adds a list of players to the interface")]
    public class PlayerList : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary;

        private const string Layer = "Com.Mevent.Main";

        private static PlayerList _instance;

        #endregion

        #region Config

        private Configuration _config;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Команда | Command")]
            public string Command = "playerslist";

            [JsonProperty(PropertyName = "Фон | Background")]
            public IPanel Background = new IPanel
            {
                AnchorMin = "0 0", AnchorMax = "1 1",
                OffsetMin = "0 0", OffsetMax = "0 0",
                Image = string.Empty,
                Color = new IColor("#0D1F4E", 95),
                isRaw = false,
                Sprite = "Assets/Content/UI/UI.Background.Tile.psd",
                Material = "Assets/Icons/IconMaterial.mat"
            };

            [JsonProperty(PropertyName = "Заглавие | Title")]
            public IText Title = new IText
            {
                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                OffsetMin = "-150 300", OffsetMax = "150 360",
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 38,
                Color = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Закрыть | Close")]
            public IText Close = new IText
            {
                AnchorMin = "1 1", AnchorMax = "1 1",
                OffsetMin = "-35 -35", OffsetMax = "-5 -5",
                Font = "robotocondensed-bold.ttf",
                Align = TextAnchor.MiddleCenter,
                FontSize = 24,
                Color = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Настройка панели игрока | Player Panel Settings")]
            public PlayerPanel Panel = new PlayerPanel
            {
                Columns = 2,
                Width = 555,
                Height = 35,
                Margin = 10,
                AmountOnPage = 22,
                BackgroundColor = new IColor("#1D3676", 100),
                Avatar = new InterfacePosition
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "2.5 2.5", OffsetMax = "32.5 32.5"
                },
                Name = new IText
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "40 15", OffsetMax = "150 32.5",
                    Align = TextAnchor.LowerLeft,
                    Font = "robotocondensed-regular.ttf",
                    Color = new IColor("#FFFFFF", 100),
                    FontSize = 14
                },
                SteamId = new IText
                {
                    AnchorMin = "0 0", AnchorMax = "0 0",
                    OffsetMin = "40 2.5", OffsetMax = "150 17.5",
                    Align = TextAnchor.UpperLeft,
                    Font = "robotocondensed-regular.ttf",
                    Color = new IColor("#FFFFFF", 50),
                    FontSize = 10
                },
                Button = new IButton
                {
                    AnchorMin = "1 0.5", AnchorMax = "1 0.5",
                    Height = 25,
                    Width = 100,
                    Margin = 5
                },
                Buttons = new List<FButton>
                {
                    new FButton
                    {
                        Text = "ДОБАВИТЬ В ДРУЗЬЯ",
                        Command = "chat.say /friend add {user}",
                        Color = new IColor("#5D8FDF", 95),
                        FontSize = 10,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        TColor = new IColor("#1D3676", 100)
                    },
                    new FButton
                    {
                        Text = "ТЕЛЕПОРТАЦИЯ",
                        Command = "tpr {user}",
                        Color = new IColor("#5D8FDF", 95),
                        FontSize = 10,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        TColor = new IColor("#1D3676", 100)
                    },
                    new FButton
                    {
                        Text = "ЖАЛОБА",
                        Command = "report {user}",
                        Color = new IColor("#5D8FDF", 95),
                        FontSize = 10,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        TColor = new IColor("#1D3676", 100)
                    },
                    new FButton
                    {
                        Text = "СТАТИСТИКА",
                        Command = "stats {user}",
                        Color = new IColor("#5D8FDF", 95),
                        FontSize = 10,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        TColor = new IColor("#1D3676", 100)
                    }
                }
            };

            [JsonProperty(PropertyName = "Поиск игрока")]
            public GButton Search = new GButton
            {
                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                OffsetMin = "-520 25", OffsetMax = "520 60",
                Align = TextAnchor.MiddleCenter,
                FontSize = 14,
                Font = "robotocondensed-regular.ttf",
                Color = new IColor("#5D8FDF", 100),
                BackgroundColor = new IColor("#1D3676", 100),
                InputColor = new IColor("#FFFFFF", 85)
            };

            [JsonProperty(PropertyName = "Назад | Back")]
            public GButton Back = new GButton
            {
                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                OffsetMin = "-560 25", OffsetMax = "-525 60",
                Align = TextAnchor.MiddleCenter,
                FontSize = 14,
                Font = "robotocondensed-regular.ttf",
                Color = new IColor("#1D3676", 100),
                BackgroundColor = new IColor("#5D8FDF", 100),
                InputColor = new IColor("#FFFFFF", 100)
            };

            [JsonProperty(PropertyName = "Вперёд | Next")]
            public GButton Next = new GButton
            {
                AnchorMin = "0.5 0", AnchorMax = "0.5 0",
                OffsetMin = "525 25", OffsetMax = "560 60",
                Align = TextAnchor.MiddleCenter,
                FontSize = 14,
                Font = "robotocondensed-regular.ttf",
                Color = new IColor("#1D3676", 100),
                BackgroundColor = new IColor("#5D8FDF", 100),
                InputColor = new IColor("#FFFFFF", 100)
            };

            public VersionNumber Version;
        }

        private class GButton : IText
        {
            [JsonProperty(PropertyName = "Цвет фона | Background Color")]
            public IColor BackgroundColor;

            [JsonProperty(PropertyName = "Цвет вводимого текста | Input Text Color")]
            public IColor InputColor;

            public void Search(ref CuiElementContainer container, string parent, string name, string text,
                string command)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    RectTransform =
                        { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
                    Image = { Color = BackgroundColor.Get() }
                }, parent, name);

                container.Add(new CuiLabel
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Text =
                    {
                        Text = $"{text}",
                        Align = Align,
                        Font = Font,
                        FontSize = FontSize,
                        Color = Color.Get()
                    }
                }, name);

                container.Add(new CuiElement
                {
                    Parent = name,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            FontSize = FontSize, Align = Align, Font = Font,
                            Command = command, Text = InputColor.Get(), Color = "1 1 1 0.9", CharsLimit = 6
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" }
                    }
                });
            }

            public void Button(ref CuiElementContainer container, string parent, string name, string text, string cmd,
                string close = "")
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiButton
                {
                    RectTransform =
                        { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
                    Text =
                    {
                        Text = $"{text}",
                        Align = Align,
                        Font = Font,
                        FontSize = FontSize,
                        Color = Color.Get()
                    },
                    Button =
                    {
                        Command = $"{cmd}",
                        Color = BackgroundColor.Get(),
                        Close = $"{close}"
                    }
                }, parent, name);
            }
        }

        private class PlayerPanel
        {
            [JsonProperty(PropertyName = "Столбцы | Columns")]
            public int Columns;

            [JsonProperty(PropertyName = "Ширина | Width")]
            public float Width;

            [JsonProperty(PropertyName = "Высота | Height")]
            public float Height;

            [JsonProperty(PropertyName = "Отступ | Margin")]
            public float Margin;

            [JsonProperty(PropertyName = "Количество на страницу | Amount On Page")]
            public int AmountOnPage;

            [JsonProperty(PropertyName = "Цвет фона | Background Color")]
            public IColor BackgroundColor;

            [JsonProperty(PropertyName = "Аватар | Avatar")]
            public InterfacePosition Avatar;

            [JsonProperty(PropertyName = "Имя игрока | Player Name")]
            public IText Name;

            [JsonProperty(PropertyName = "SteamId игрока | Player SteamId")]
            public IText SteamId;

            [JsonProperty(PropertyName = "Кнопка | Button")]
            public IButton Button;

            [JsonProperty(PropertyName = "Кнопки | Buttons", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<FButton> Buttons;

            public void Get(ref CuiElementContainer container, ulong member, string displayName, string parent,
                string name, string oMin, string oMax, string close = "")
            {
                if (string.IsNullOrEmpty(parent)) return;

                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5",
                        OffsetMin = oMin, OffsetMax = oMax
                    },
                    Image = { Color = BackgroundColor.Get() }
                }, parent, name);

                if (_instance != null && _instance.ImageLibrary)
                    container.Add(new CuiElement
                    {
                        Parent = name,
                        Components =
                        {
                            new CuiRawImageComponent
                                { Png = _instance.ImageLibrary.Call<string>("GetImage", $"avatar_{member}") },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = Avatar.AnchorMin, AnchorMax = Avatar.AnchorMax,
                                OffsetMin = Avatar.OffsetMin, OffsetMax = Avatar.OffsetMax
                            }
                        }
                    });

                Name.Get(ref container, name, null, displayName);

                SteamId.Get(ref container, name, null, $"{member}");

                #region Buttons

                var xSwitch = -Button.Margin;
                for (var i = 0; i < Buttons.Count; i++)
                {
                    var button = Buttons[i];

                    button.Get(ref container, member, name, name + $".Btn.{i}", Button.AnchorMin, Button.AnchorMax,
                        $"{xSwitch - Button.Width} -{Button.Height / 2f}", $"{xSwitch} {Button.Height / 2f}", close);

                    xSwitch = xSwitch - Button.Margin - Button.Width;
                }

                #endregion
            }
        }

        private abstract class IAnchors
        {
            public string AnchorMin;

            public string AnchorMax;
        }

        private class InterfacePosition : IAnchors
        {
            public string OffsetMin;

            public string OffsetMax;
        }

        private class IButton : IAnchors
        {
            [JsonProperty(PropertyName = "Высота | Height")]
            public float Height;

            [JsonProperty(PropertyName = "Ширина | Width")]
            public float Width;

            [JsonProperty(PropertyName = "Отступ | Margin")]
            public float Margin;
        }

        private class IPanel : InterfacePosition
        {
            [JsonProperty(PropertyName = "Изображение | Image")]
            public string Image;

            [JsonProperty(PropertyName = "Цвет | Color")]
            public IColor Color;

            [JsonProperty(PropertyName = "Сохранять цвет изображения? | Save Image Color")]
            public bool isRaw;

            [JsonProperty(PropertyName = "Sprite")]
            public string Sprite;

            [JsonProperty(PropertyName = "Material")]
            public string Material;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
                bool cursor = false)
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                if (isRaw)
                {
                    var element = new CuiElement
                    {
                        Name = name,
                        Parent = parent,
                        Components =
                        {
                            new CuiRawImageComponent
                            {
                                Png = !string.IsNullOrEmpty(Image)
                                    ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                    : null,
                                Color = Color.Get(),
                                Material = Material,
                                Sprite = !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Icons/rust.png"
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin,
                                OffsetMax = OffsetMax
                            }
                        }
                    };

                    if (cursor) element.Components.Add(new CuiNeedsCursorComponent());

                    container.Add(element);
                }
                else
                {
                    container.Add(new CuiPanel
                    {
                        RectTransform =
                        {
                            AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax
                        },
                        Image =
                        {
                            Png = !string.IsNullOrEmpty(Image)
                                ? _instance.ImageLibrary.Call<string>("GetImage", Image)
                                : null,
                            Color = Color.Get(),
                            Sprite =
                                !string.IsNullOrEmpty(Sprite) ? Sprite : "Assets/Content/UI/UI.Background.Tile.psd",
                            Material = !string.IsNullOrEmpty(Material) ? Material : "Assets/Icons/IconMaterial.mat"
                        },
                        CursorEnabled = cursor
                    }, parent, name);
                }
            }
        }

        private class IText : InterfacePosition
        {
            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")] public string Font;

            [JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Text Color")]
            public IColor Color;

            public void Get(ref CuiElementContainer container, string parent = "Hud", string name = null,
                string text = "")
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiLabel
                {
                    RectTransform =
                        { AnchorMin = AnchorMin, AnchorMax = AnchorMax, OffsetMin = OffsetMin, OffsetMax = OffsetMax },
                    Text =
                    {
                        Text = $"{text}", Align = Align, FontSize = FontSize, Color = Color.Get(),
                        Font = Font
                    }
                }, parent, name);
            }
        }

        private class FButton
        {
            [JsonProperty(PropertyName = "Текст | Text")]
            public string Text;

            [JsonProperty(PropertyName = "Команда | Command")]
            public string Command;

            [JsonProperty(PropertyName = "Цвет | Color")]
            public IColor Color;

            [JsonProperty(PropertyName = "Font Size")]
            public int FontSize;

            [JsonProperty(PropertyName = "Font")] public string Font;

            [JsonProperty(PropertyName = "Align")] [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor Align;

            [JsonProperty(PropertyName = "Text Color")]
            public IColor TColor;

            public void Get(ref CuiElementContainer container, ulong user, string parent, string name, string aMin,
                string aMax, string oMin, string oMax, string close = "")
            {
                if (string.IsNullOrEmpty(name))
                    name = CuiHelper.GetGuid();

                container.Add(new CuiButton
                {
                    RectTransform =
                    {
                        AnchorMin = aMin, AnchorMax = aMax,
                        OffsetMin = oMin, OffsetMax = oMax
                    },
                    Text =
                    {
                        Text = Text.Replace("{user}", user.ToString()),
                        Align = Align,
                        Font = Font,
                        FontSize = FontSize,
                        Color = TColor.Get()
                    },
                    Button =
                    {
                        Command = $"plistsendcmd {Command.Replace("{user}", user.ToString())}",
                        Color = Color.Get(),
                        Close = $"{close}"
                    }
                }, parent, name);
            }
        }


        private class IColor
        {
            [JsonProperty(PropertyName = "HEX")] public string HEX;

            [JsonProperty(PropertyName = "Непрозрачность | Opacity (0 - 100)")]
            public float Alpha;

            public string Get()
            {
                if (string.IsNullOrEmpty(HEX)) HEX = "#FFFFFF";

                var str = HEX.Trim('#');
                if (str.Length != 6) throw new Exception(HEX);
                var r = byte.Parse(str.Substring(0, 2), NumberStyles.HexNumber);
                var g = byte.Parse(str.Substring(2, 2), NumberStyles.HexNumber);
                var b = byte.Parse(str.Substring(4, 2), NumberStyles.HexNumber);

                return $"{(double)r / 255} {(double)g / 255} {(double)b / 255} {Alpha / 100}";
            }

            public IColor(string hex, float alpha)
            {
                HEX = hex;
                Alpha = alpha;
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();

                if (_config.Version < Version)
                    UpdateConfigValues();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        private void UpdateConfigValues()
        {
            PrintWarning("Config update detected! Updating config values...");

            var baseConfig = new Configuration();

            if (_config.Version == default(VersionNumber) || _config.Version < new VersionNumber(1, 4, 0))
                _config.Panel.Columns = baseConfig.Panel.Columns;

            _config.Version = Version;
            PrintWarning("Config update completed!");
        }

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            _instance = this;

            if (!ImageLibrary)
                PrintWarning("IMAGE LIBRARY IS NOT INSTALLED.");

            foreach (var player in BasePlayer.activePlayerList)
                OnPlayerConnected(player);

            AddCovalenceCommand(_config.Command, nameof(CmdPlayerList));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, Layer);

            _instance = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;

            GetAvatar(player.userID,
                avatar => ImageLibrary?.Call("AddImage", avatar, $"avatar_{player.UserIDString}"));
        }

        #endregion

        #region Commands

        private void CmdPlayerList(IPlayer cov, string cmd, string[] args)
        {
            var player = cov?.Object as BasePlayer;
            if (player == null) return;

            MainUi(player);
        }

        [ConsoleCommand("plistsendcmd")]
        private void SendCMD(ConsoleSystem.Arg args)
        {
            var player = args.Player();
            if (player == null || !args.HasArgs()) return;

            var convertcmd =
                $"{args.Args[0]}  \" {string.Join(" ", args.Args.ToList().GetRange(1, args.Args.Length - 1))}\" 0";
            player.SendConsoleCommand(convertcmd);
        }

        [ConsoleCommand("UI_PlayerList")]
        private void ConsoleCmdPlayerList(ConsoleSystem.Arg arg)
        {
            var player = arg?.Player();
            if (player == null || !arg.HasArgs(2)) return;

            var parent = arg.Args[1];
            switch (arg.Args[0])
            {
                case "close":
                {
                    CuiHelper.DestroyUi(player, Layer);
                    break;
                }
                case "page":
                {
                    int page;
                    if (!arg.HasArgs(3) || !int.TryParse(arg.Args[2], out page)) return;

                    MainUi(player, parent, page);
                    break;
                }
                case "search":
                {
                    int page;
                    if (!arg.HasArgs(4) || !int.TryParse(arg.Args[2], out page)) return;

                    var target = string.Join(" ", arg.Args.Skip(3));

                    MainUi(player, parent, page, target);
                    break;
                }
            }
        }

        #endregion

        #region Interface

        private void MainUi(BasePlayer player, string parent = "Overlay", int page = 0, string target = "")
        {
            if (string.IsNullOrEmpty(parent))
                parent = "Overlay";

            var container = new CuiElementContainer();

            _config.Background.Get(ref container, parent, Layer, true);

            _config.Title.Get(ref container, Layer, null, Msg(UITitle, player.UserIDString));

            #region Main

            var panel = _config.Panel;

            var list = string.IsNullOrEmpty(target)
                ? BasePlayer.activePlayerList.Skip(panel.AmountOnPage * page).Take(panel.AmountOnPage)
                    .ToList()
                : BasePlayer.activePlayerList.OrderByDescending(x =>
                        string.Equals(x.UserIDString, target, StringComparison.CurrentCultureIgnoreCase) ||
                        x.UserIDString.ToLower().Contains(target.ToLower()) ||
                        x.displayName.ToLower().Contains(target.ToLower())).Skip(panel.AmountOnPage * page)
                    .Take(panel.AmountOnPage)
                    .ToList();

            var countColumns = Mathf.Min(_config.Panel.Columns, list.Count);

            var constXSwitch = -(countColumns * panel.Width + (countColumns - 1) * panel.Margin) / 2f;

            var xSwitch = constXSwitch;

            var ySwitch = 275f;

            for (var i = 0; i < list.Count; i++)
            {
                var check = list[i];
                var name = string.IsNullOrEmpty(check.displayName) ? check.UserIDString : check.displayName;

                _config.Panel.Get(ref container, check.userID, name, Layer, Layer + $".Player.{i}",
                    $"{xSwitch} {ySwitch - panel.Height}",
                    $"{xSwitch + panel.Width} {ySwitch}",
                    Layer);

                if ((i + 1) % _config.Panel.Columns == 0)
                {
                    xSwitch = constXSwitch;
                    ySwitch = ySwitch - panel.Height - panel.Margin;
                }
                else
                {
                    xSwitch += panel.Width + panel.Margin;
                }
            }

            #endregion
            
            #region Search

            _config.Search.Search(ref container, Layer, null,
                string.IsNullOrEmpty(target) ? Msg(EnterPlayer, player.UserIDString) : target,
                $"UI_PlayerList search {parent} {page} ");

            var finded = "";
            if (finded == "VVP")
            {
            }

            #endregion
            
            #region Pages

            _config.Back.Button(ref container, Layer, null, "▼",
                page != 0 ? $"UI_PlayerList page {parent} {page - 1}" : "");

            _config.Next.Button(ref container, Layer, null, "▲",
                BasePlayer.activePlayerList.Count > (page + 1) * _config.Panel.AmountOnPage
                    ? $"UI_PlayerList page {parent} {page + 1}"
                    : "");

            #endregion
            
            #region Close

            _config.Close.Get(ref container, Layer, Layer + ".Close", "✕");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                Text = { Text = "" },
                Button = { Color = "0 0 0 0", Command = $"UI_PlayerList close {parent}" }
            }, Layer + ".Close");

            #endregion

            CuiHelper.DestroyUi(player, Layer);
            CuiHelper.AddUi(player, container);
        }

        #endregion

        #region Utils

        private readonly Regex _regex = new Regex(@"<avatarFull><!\[CDATA\[(.*)\]\]></avatarFull>");

        private void GetAvatar(ulong userId, Action<string> callback)
        {
            if (callback == null) return;

            webrequest.Enqueue($"http://steamcommunity.com/profiles/{userId}?xml=1", null, (code, response) =>
            {
                if (code != 200 || response == null)
                    return;

                var avatar = _regex.Match(response).Groups[1].ToString();
                if (string.IsNullOrEmpty(avatar))
                    return;

                callback.Invoke(avatar);
            }, this);
        }

        #endregion

        #region Lang

        private const string
            UITitle = "UITitle",
            EnterPlayer = "EnterPlayer";

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [UITitle] = "PLAYER LIST",
                [EnterPlayer] = "ENTER PLAYER NAME"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                [UITitle] = "СПИСОК ИГРОКОВ",
                [EnterPlayer] = "ВВЕДИТЕ ИМЯ ИГРОКА"
            }, this, "ru");
        }

        private string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(lang.GetMessage(key, this, userid), obj);
        }

        #endregion
    }
}