using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Globalization;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("BloodChat", "[LimePlugin] Chibubrik", "1.0.0")]
    public class BloodChat : RustPlugin
    {
        #region Вар
        string Layer = "Chat_UI";
        Dictionary<ulong, DBSettings> DB = new Dictionary<ulong, DBSettings>();
        #endregion

        #region Класс
        public class Settings
        {
            public string Prefix;
            public string Color;
            public string NameColor;
            public string Perm;
        }

        public class DBSettings
        {
            public string Prefix = "";
            public string Color = "#55aaff";
            public string NameColor = "#55aaff";
            public double Time;
            public bool Hints = true;
            public bool ChatEnable = true;
        }
        #endregion

        #region Конфиг
        Configuration config;
        class Configuration
        {
            [JsonProperty("Пермишен модератора (mute и unmute)")] public string perm = "bloodchat.mute";
            [JsonProperty("Чеерез какое время будут выскакивать сообщения от сервера в чат(в секундах)")] public int time = 300;
            [JsonProperty("Список префиксов и их настройки")] public List<Settings> settings;
            [JsonProperty("Список сообщений в чат")] public List<string> message;
            public static Configuration GetNewCong()
            {
                return new Configuration
                {
                    settings = new List<Settings>() {
                        new Settings
                        {
                            Prefix = "",
                            Color = "#55aaff",
                            NameColor = "#55aaff",
                            Perm = ""
                        },
                        new Settings
                        {
                            Prefix = "вип",
                            Color = "#deae6a",
                            NameColor = "#55aaff",
                            Perm = "bloodchat.v"
                        },
                        new Settings
                        {
                            Prefix = "элита",
                            Color = "#896ade",
                            NameColor = "#55aaff",
                            Perm = "bloodchat.e"
                        },
                        new Settings
                        {
                            Prefix = "печенька",
                            Color = "#e0947a",
                            NameColor = "#55aaff",
                            Perm = "bloodchat.c"
                        },
                        new Settings
                        {
                            Prefix = "ютубер",
                            Color = "#e0947a",
                            NameColor = "#55aaff",
                            Perm = "bloodchat.youtube"
                        },
                        new Settings
                        {
                            Prefix = "модератор",
                            Color = "#16de69",
                            NameColor = "#16de69",
                            Perm = "bloodchat.moder"
                        },
                        new Settings
                        {
                            Prefix = "админ",
                            Color = "#e0947a",
                            NameColor = "#e0947a",
                            Perm = "bloodchat.admin"
                        }
                    },
                    message = new List<string>() {
                        "<size=12>Хочешь узнать о сервере побольше?</size>\n<size=10>Пропиши <color=#e0947a>/menu</color> или нажми на <color=#e0947a>3</color> окно сверху.</size>",
                        "<size=12>Группа ВК - <color=#e0947a>vk.com/limeplugin</color></size>",
                        "<size=12>Наш магазин - <color=#e0947a>limeplugin</color></size>",
                        "<size=12>Увидел читера или нарушителей?</size>\n<size=10>Отправляй жалобу в <color=#e0947a>/report</color></size>",
                        "<size=12>Дискорд - <color=#e0947a>https://discord.gg/kNw2gvDtzm</color></size>",
                        "<size=12>Наш сайт - <color=#e0947a>limeplugin</color></size>\n<size=10>Не забывай крутить <color=#e0947a>ежедневный</color> кейс!</size>"
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
                if (config?.message == null) LoadDefaultConfig();
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
        void HandleMessage(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (DB[player.userID].Time > CurTime())
            {
                player.SendConsoleCommand($"note.inv 605467368 -{FormatShortTime(TimeSpan.FromSeconds(DB[player.userID].Time - CurTime()))} \"<size=12>У вас мут чата!</size>\"");
                return;
            }

            if (!DB[player.userID].ChatEnable)
            {
                SendReply(player, "<size=12>У вас отключен игровой чат, включить его можно в настройках чата!</size>");
                return;
            }

            foreach (var check in BasePlayer.activePlayerList.ToList())
            {
                if (!DB[check.userID].ChatEnable) continue;

                check.SendConsoleCommand("chat.add", 0, player.userID, $"<size=12><color={DB[player.userID].Color}>{DB[player.userID].Prefix}</color> <color={DB[player.userID].NameColor}>{player.displayName}</color>: {message}</size>");
            }
        }

        object OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            if (channel == ConVar.Chat.ChatChannel.Global)
            {
                HandleMessage(player, message, channel);
            }
            else
            {
                foreach (var check in player.Team.members)
                {
                    var target = BasePlayer.FindByID(check);
                    if (target == null || !target.IsConnected) continue;
                    
                    target.SendConsoleCommand("chat.add", 1, player.userID, $"<size=12>{player.displayName}: {message}</size>");
                }
            }
            return false;
        }

        void OnUserPermissionRevoked(string id, string perm)
        {
            BasePlayer player = BasePlayer.Find(id);

            if (player != null && !player.IsSleeping())
            {
                DB[player.userID].Prefix = "";
                DB[player.userID].Color = "#55aaff";
                DB[player.userID].NameColor = "#55aaff";
                SendReply(player, "<size=12>У вас закончилась активная привилегия, ваш префикс изменен на стандартный</size>");
                return;
            }
        }

        void OnServerInitialized() 
        {
            foreach(var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);

            timer.Every(config.time, () =>
            {
                var message = config.message.GetRandom();
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    if (!DB[check.userID].Hints) continue;
                    check.SendConsoleCommand("chat.add", 0, 0, message);
                }
            }).Callback();

            foreach (var check in config.settings)
                permission.RegisterPermission(check.Perm, this);

            permission.RegisterPermission(config.perm, this);
        }

        void OnPlayerConnected(BasePlayer player) => CreateDataBase(player); 

        void OnPlayerDisconnected(BasePlayer player, string reason) => SaveDataBase(player.userID);

        void Unload()
        {
            foreach (var check in DB)
                SaveDataBase(check.Key);
        }
        #endregion

        #region Дата
        void CreateDataBase(BasePlayer player)
        {
            var DataBase = Interface.Oxide.DataFileSystem.ReadObject<DBSettings>($"MenuSystem/DBChat/{player.userID}");
            
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DBSettings());
             
            DB[player.userID] = DataBase ?? new DBSettings();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"MenuSystem/DBChat/{userId}", DB[userId]);
        #endregion

        #region Команды
        [ChatCommand("mute")]
        void ChatMute(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.perm))
            {
                SendReply(player, "<size=12>У вас нет доступа к команде /mute</size>");
                return;
            }

            if (args.Length < 3)
            {
                SendReply(player, "<size=12>Пример использования:\n/mute <color=#e0947a>[имя или steamid]</color> <color=#e0947a>[причина мута]</color> <color=#e0947a>[время мута (в секундах)]</color></size>");
                return;
            }

            var target = FindBasePlayer(args[0]);
            if (target == null)
            {
                SendReply(player, $"<size=12>Игрок не найден!</size>");
                return;
            }

            double time = double.Parse(args[2]);

            DB[target.userID].Time = CurTime() + time;

            foreach (var check in BasePlayer.activePlayerList)
                SendReply(check, $"<size=12>Игрок <color=#e0947a>{target.displayName}</color> получил мут!\nДлительность: <color=#e0947a>{time}</color> сек.\nПричина: <color=#e0947a>{args[1]}</color>!</size>");
        }

        [ChatCommand("unmute")]
        void ChatUnMute(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.perm))
            {
                SendReply(player, "<size=12>У вас нет доступа к команде /unmute</size>");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "<size=12>Пример использования:\n/unmute <color=#e0947a>[имя или steamid]</color></size>");
                return;
            }

            var target = FindBasePlayer(args[0]);
            if (DB[target.userID].Time < CurTime())
            {
                SendReply(player, "<size=12>У игрока нет блокировки чата!</size>");
                return;
            }

            DB[target.userID].Time = 0;

            foreach (var check in BasePlayer.activePlayerList)
                SendReply(check, $"<size=12>Модератор <color=#e0947a>{player.displayName}</color> снял мут у игрока <color=#e0947a>{target.displayName}</color>!</size>");
        }

        [ConsoleCommand("chat")]
        void ConsoleChat(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                var check = config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList();
                if (args.Args[0] == "namecolor")
                {
                    int page = int.Parse(args.Args[1]);
                    DB[player.userID].NameColor = check.ElementAt(page).NameColor;
                    NameUI(player);
                    NameColorUI(player, page);
                    AlertUI(player, "Цвет ника успешно изменен!", "1");
                }
                if (args.Args[0] == "prefix")
                {
                    int page = int.Parse(args.Args[1]);
                    DB[player.userID].Prefix = check.ElementAt(page).Prefix;
                    NameUI(player);
                    PrefixUI(player, page);
                    AlertUI(player, "Префикс успешно изменен!", "1");
                }
                if (args.Args[0] == "prefixcolor")
                {
                    int page = int.Parse(args.Args[1]);
                    DB[player.userID].Color = check.ElementAt(page).Color;
                    NameUI(player);
                    PrefixColorUI(player, page);
                    AlertUI(player, "Цвет префикса успешно изменен!", "1");
                }
                if (args.Args[0] == "hints")
                {
                    DB[player.userID].Hints = bool.Parse(args.Args[1]);
                    HintsUI(player);
                }
                if (args.Args[0] == "chatenable")
                {
                    DB[player.userID].ChatEnable = bool.Parse(args.Args[1]);
                    ChatEnableUI(player);
                }
            }
        }
        #endregion

        #region Интерфейс
        void ChatUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, Layer);
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Settings", Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.785", AnchorMax = "1 0.886", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer, "ColorName");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=14>Цвет ника</size>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "ColorName");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.671", AnchorMax = "1 0.775", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer, "Prefix");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=14>Префикс</size>\n<color=#ffffff40>то что будет перед ником</color>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "Prefix");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 0.56", AnchorMax = "1 0.66", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer, "ColorPrefix");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=14>Цвет префикса</size>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "ColorPrefix");

            CuiHelper.AddUi(player, container);
            NameUI(player);
            NameColorUI(player, config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList().FindIndex(a => a.NameColor == DB[player.userID].NameColor));
            PrefixUI(player, config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList().FindIndex(a => a.Prefix == DB[player.userID].Prefix));
            PrefixColorUI(player, config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList().FindIndex(a => a.Color == DB[player.userID].Color));
            HintsUI(player);
            ChatEnableUI(player);
        }

        void NameUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Name");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0 0.897", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat" }
            }, Layer, "Name");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=14>Ваш ник сейчас: <color={DB[player.userID].Color}>{DB[player.userID].Prefix}</color> <color={DB[player.userID].NameColor}>{player.displayName}</color></size>\n<color=#ffffff40>именно так будет выглядить ник в чате</color>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "Name");

            CuiHelper.AddUi(player, container);
        }

        void NameColorUI(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, "NameLayer");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.5 0.785", AnchorMax = $"1 0.886", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "NameLayer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.2 0.3", AnchorMax = $"0.3 0.7", OffsetMax = "0 0" },
                Button = { Color = page != 0 ? "1 1 1 0.2" : "1 1 1 0.15", Command = page != 0 ? $"chat namecolor {page - 1}" : "" },
                Text = { Text = "<", Color = page != 0 ? "1 1 1 0.9" : HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "NameLayer");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.3 0.3", AnchorMax = $"0.82 0.7", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "NameLayer", "NameColor");

            string name = page != 0 ? DB[player.userID].NameColor : "не установлен";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = name, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "NameColor");

            var nameColor = config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList();
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.82 0.3", AnchorMax = $"0.92 0.7", OffsetMax = "0 0" },
                Button = { Color = nameColor.Count() > (page + 1) ? "1 1 1 0.2" : "1 1 1 0.15", Command = nameColor.Count() > (page + 1) ? $"chat namecolor {page + 1}" : "" },
                Text = { Text = ">", Color = nameColor.Count() > (page + 1) ? "1 1 1 0.9" : HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "NameLayer");

            CuiHelper.AddUi(player, container);
        }

        void PrefixUI(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, "PrefixLayer");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.5 0.671", AnchorMax = $"1 0.775", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "PrefixLayer");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.2 0.3", AnchorMax = $"0.3 0.7", OffsetMax = "0 0" },
                Button = { Color = page != 0 ? "1 1 1 0.2" : "1 1 1 0.15", Command = page != 0 ? $"chat prefix {page - 1}" : "" },
                Text = { Text = "<", Color = page != 0 ? "1 1 1 0.9" : HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "PrefixLayer");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.3 0.3", AnchorMax = $"0.82 0.7", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "PrefixLayer", "PrefixText");

            string prefix = page != 0 ? DB[player.userID].Prefix : "не установлен";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = prefix, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "PrefixText");

            var prefixColor = config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList();
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.82 0.3", AnchorMax = $"0.92 0.7", OffsetMax = "0 0" },
                Button = { Color = prefixColor.Count() > (page + 1) ? "1 1 1 0.2" : "1 1 1 0.15", Command = prefixColor.Count() > (page + 1) ? $"chat prefix {page + 1}" : "" },
                Text = { Text = ">", Color = prefixColor.Count() > (page + 1) ? "1 1 1 0.9" : HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "PrefixLayer");

            CuiHelper.AddUi(player, container);
        }

        void PrefixColorUI(BasePlayer player, int page)
        {
            CuiHelper.DestroyUi(player, "PrefixColor");
            var container = new CuiElementContainer();

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.5 0.56", AnchorMax = $"1 0.66", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, Layer, "PrefixColor");

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.2 0.3", AnchorMax = $"0.3 0.7", OffsetMax = "0 0" },
                Button = { Color = page != 0 ? "1 1 1 0.2" : "1 1 1 0.15", Command = page != 0 ? $"chat prefixcolor {page - 1}" : "" },
                Text = { Text = "<", Color = page != 0 ? "1 1 1 0.9" : HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "PrefixColor");

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.3 0.3", AnchorMax = $"0.82 0.7", OffsetMax = "0 0" },
                Image = { Color = "1 1 1 0.1" }
            }, "PrefixColor", "PrefixText");

            string prefix = page != 0 ? DB[player.userID].Color : "не установлен";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = prefix, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "PrefixText");

            var prefixColor = config.settings.Where(z => (string.IsNullOrEmpty(z.Perm) || permission.UserHasPermission(player.UserIDString, z.Perm))).ToList();
            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.82 0.3", AnchorMax = $"0.92 0.7", OffsetMax = "0 0" },
                Button = { Color = prefixColor.Count() > (page + 1) ? "1 1 1 0.2" : "1 1 1 0.15", Command = prefixColor.Count() > (page + 1) ? $"chat prefixcolor {page + 1}" : "" },
                Text = { Text = ">", Color = prefixColor.Count() > (page + 1) ? "1 1 1 0.9" : HexToCuiColor("#7f7d7d", 100), Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "PrefixColor");

            CuiHelper.AddUi(player, container);
        }

        void HintsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hints");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0.445", AnchorMax = $"1 0.548", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat", Command = DB[player.userID].Hints == true ? $"chat hints {false}" : $"chat hints {true}" },
                Text = { Text = "" }
            }, Layer, "Hints");

            var colorButton = !DB[player.userID].Hints ? HexToCuiColor("#e0947a", 30) : HexToCuiColor("#bbc47e", 30);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.89 0.41", AnchorMax = $"0.97 0.59", OffsetMax = "0 0" },
                Image = { Color = colorButton }
            }, "Hints");

            var anchor = !DB[player.userID].Hints ? 0 : 0.04;
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"{0.89 + anchor} 0.25", AnchorMax = $"{0.93 + anchor} 0.75", OffsetMax = "0 0" },
                Image = { Color = colorButton }
            }, "Hints", "Text");

            var text = !DB[player.userID].Hints ? "○" : "|";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = text, Color = "1 1 1 0.8", Align = TextAnchor.MiddleCenter, FontSize = 14, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "Text");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                Text = { Text = "<size=14>Подсказки</size>\n<color=#ffffff40>информационные сообщения в глобальном чате</color>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "Hints");

            CuiHelper.AddUi(player, container);
        }

        void ChatEnableUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ChatEnable");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0 0.333", AnchorMax = $"1 0.433", OffsetMax = "0 0" },
                Button = { Color = "1 1 1 0.04", Material = "assets/content/ui/uibackgroundblur.mat", Command = DB[player.userID].ChatEnable == true ? $"chat chatenable {false}" : $"chat chatenable {true}" },
                Text = { Text = "" }
            }, Layer, "ChatEnable");

            var colorButton = !DB[player.userID].ChatEnable ? HexToCuiColor("#e0947a", 30) : HexToCuiColor("#bbc47e", 30);
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0.89 0.41", AnchorMax = $"0.97 0.59", OffsetMax = "0 0" },
                Image = { Color = colorButton }
            }, "ChatEnable");

            var anchor = !DB[player.userID].ChatEnable ? 0 : 0.04;
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"{0.89 + anchor} 0.25", AnchorMax = $"{0.93 + anchor} 0.75", OffsetMax = "0 0" },
                Image = { Color = colorButton }
            }, "ChatEnable", "Text");

            var text = !DB[player.userID].ChatEnable ? "<color=#e0947a>○</color>" : "<color=#bbc47e>|</color>";
            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 1", OffsetMax = "0 0" },
                Text = { Text = text, Color = HexToCuiColor("#e0947a", 100), Align = TextAnchor.MiddleCenter, FontSize = 16, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "Text");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.025 0", AnchorMax = $"0.96 1", OffsetMax = "0 0" },
                Text = { Text = "<size=14>Глобальный чат</size>\n<color=#ffffff40>если отключить - сообщения от игры и сервера останутся</color>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "ChatEnable");

            CuiHelper.AddUi(player, container);
        }

        Timer Timer = null;
        void AlertUI(BasePlayer player, string message, string color) {
            if (Timer != null)
                Timer.Destroy();
            CuiHelper.DestroyUi(player, ".AlertInfo");
            var container = new CuiElementContainer();
                        
            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = "0 1.01", AnchorMax = "1 1.07", OffsetMax = "0 0" },
                Image = { Color = color == "0" ? HexToCuiColor("#e0947a", 20) : HexToCuiColor("#bbc47e", 20) }
            }, Layer, ".AlertInfo");

            container.Add(new CuiLabel 
            {
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1", OffsetMax = $"0 0"} ,
                Text = { Text = message, Align = TextAnchor.MiddleCenter, Font = "robotocondensed-regular.ttf", FontSize = 12, Color =  color == "0" ? HexToCuiColor("#e0947a", 100) : HexToCuiColor("#bbc47e", 100)}
            }, ".AlertInfo");

            CuiHelper.AddUi(player, container);
            Timer = timer.In(5f, () => CuiHelper.DestroyUi(player, ".AlertInfo"));
        }
        #endregion

        #region Хелпер
        BasePlayer FindBasePlayer(string nameOrUserId)
        {
            nameOrUserId = nameOrUserId.ToLower();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            foreach (var player in BasePlayer.sleepingPlayerList)
            {
                if (player.displayName.ToLower().Contains(nameOrUserId) || player.UserIDString == nameOrUserId) return player;
            }
            return default(BasePlayer);
        }

        double CurTime() => new TimeSpan(DateTime.UtcNow.Ticks).TotalSeconds;

        public static string FormatShortTime(TimeSpan time)
        {
            string result = string.Empty;
            result = $"{time.TotalSeconds.ToString("00")}";
            return result;
        }

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