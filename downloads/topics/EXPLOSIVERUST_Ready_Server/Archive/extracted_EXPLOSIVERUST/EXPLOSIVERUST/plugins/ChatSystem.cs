using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("ChatSystem", "https://topplugin.ru / https://discord.com/invite/5DPTsRmd3G", "1.0.3")]
    public class ChatSystem : RustPlugin
    {
        #region Вар
        string Layer = "Chat_UI";

        string Perm = "chatsystem.mute";

        Dictionary<ulong, DBSettings> DB = new Dictionary<ulong, DBSettings>();

        [PluginReference] Plugin LogoSystem;
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
            public bool LogoEnable = true;
            public bool SortEnable = true;
        }
        #endregion

        #region Префиксы
        List<Settings> settings = new List<Settings>()
        {
            new Settings
            {
                Prefix = "",
                Color = "#55aaff",
                NameColor = "#55aaff",
                Perm = "chatsystem.default"
            },
            new Settings
            {
                Prefix = "ツ",
                Color = "#deae6a",
                NameColor = "#55aaff",
                Perm = "chatsystem.vip"
            },
            new Settings
            {
                Prefix = "〄",
                Color = "#896ade",
                NameColor = "#55aaff",
                Perm = "chatsystem.master"
            },
            new Settings
            {
                Prefix = "₪",
                Color = "#ee3e61",
                NameColor = "#55aaff",
                Perm = "chatsystem.legend"
            },
            new Settings
            {
                Prefix = "¥",
                Color = "#ee3e61",
                NameColor = "#55aaff",
                Perm = "chatsystem.EXPLOSIVE"
            },
            new Settings
            {
                Prefix = "マ",
                Color = "#16de69",
                NameColor = "#16de69",
                Perm = "chatsystem.moder"
            },
            new Settings
            {
                Prefix = "么",
                Color = "#ee3e61",
                NameColor = "#ee3e61",
                Perm = "chatsystem.admin"
            },
        };

        List<string> Message = new List<string>()
        {
            "<size=12>Хочешь узнать о сервере побольше?</size>\n<size=10>Пропиши <color=#ee3e61>/menu</color> или нажми на <color=#ee3e61>3</color> окно сверху.</size>",
   
            "<size=12>Наш магазин - <color=#ee3e61>https://explosive.gamestores.app</color></size>",
            "<size=12>Увидел читера или нарушителей?</size>\n<size=10>Отправляй жалобу в <color=#ee3e61>/report</color></size>",
            "<size=12>Дискорд - <color=#ee3e61>https://discord.gg/WmvaKKt2tM</color></size>"
        };
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
                if (message.Length > 2 && message.Length < 12 && message[0] == '.')
                {
                    var invertedMessage = ConvertCyrillicToLatin(message);
                    if (invertedMessage.Length == message.Length && invertedMessage[0] == '/' && Interface.Oxide.CallHook("IOnPlayerCommand", player, invertedMessage) != null)
                    {
                        HandleMessage(player, invertedMessage, channel);
                        return true;
                    }
                }
                else
                {
                    HandleMessage(player, message, channel);
                }
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
            PrintWarning("\n-----------------------------\n" +
            "     Author - https://topplugin.ru/\n" +
            "     VK - https://vk.com/rustnastroika\n" +
            "     Discord - https://discord.com/invite/5DPTsRmd3G\n" +
            "-----------------------------");
            foreach(var check in BasePlayer.activePlayerList)
                OnPlayerConnected(check);

            timer.Every(300, () =>
            {
                var message = Message.GetRandom();
                foreach (var check in BasePlayer.activePlayerList.ToList())
                {
                    if (!DB[check.userID].Hints) continue;
                    check.SendConsoleCommand("chat.add", 0, 76561199115219111, message);
                }
            }).Callback();

            foreach (var check in settings)
                permission.RegisterPermission(check.Perm, this);

            permission.RegisterPermission(Perm, this);
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
            var DataBase = Interface.Oxide.DataFileSystem.ReadObject<DBSettings>($"ChatSystem/{player.userID}");
            
            if (!DB.ContainsKey(player.userID))
                DB.Add(player.userID, new DBSettings());
             
            DB[player.userID] = DataBase ?? new DBSettings();
        }

        void SaveDataBase(ulong userId) => Interface.Oxide.DataFileSystem.WriteObject($"ChatSystem/{userId}", DB[userId]);
        #endregion

        #region Команды
        [ChatCommand("mute")]
        void ChatMute(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                SendReply(player, "<size=12>У вас нет доступа к команде /mute</size>");
                return;
            }

            if (args.Length < 3)
            {
                SendReply(player, "<size=12>Пример использования:\n/mute <color=#ee3e61>[имя или steamid]</color> <color=#ee3e61>[причина мута]</color> <color=#ee3e61>[время мута (в секундах)]</color></size>");
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
                SendReply(check, $"<size=12>Игрок <color=#ee3e61>{target.displayName}</color> получил мут!\nДлительность: <color=#ee3e61>{time}</color> сек.\nПричина: <color=#ee3e61>{args[1]}</color>!</size>");
        }

        [ChatCommand("unmute")]
        void ChatUnMute(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, Perm))
            {
                SendReply(player, "<size=12>У вас нет доступа к команде /unmute</size>");
                return;
            }

            if (args.Length < 1)
            {
                SendReply(player, "<size=12>Пример использования:\n/unmute <color=#ee3e61>[имя или steamid]</color></size>");
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
                SendReply(check, $"<size=12>Модератор <color=#ee3e61>{player.displayName}</color> снял мут у игрока <color=#ee3e61>{target.displayName}</color>!</size>");
        }

        [ConsoleCommand("chat")]
        void ConsoleSkip(ConsoleSystem.Arg args)
        {
            var player = args.Player();

            if (player != null && args.HasArgs(1))
            {
                if (args.Args[0] == "prefix")
                {
                    var text = args.Args[1] == "-" ? "" : args.Args[1];
                    if (text == DB[player.userID].Prefix)
                    {
                        SendReply(player, "<size=12>У вас уже установлен данный префикс!</size>");
                        return;
                    }

                    var check = settings.FirstOrDefault(z => z.Prefix == text);
                    DB[player.userID].Prefix = check.Prefix;
                    DB[player.userID].Color = check.Color;
                    DB[player.userID].NameColor = check.NameColor;
                    NameUI(player);
                    player.SendConsoleCommand($"note.inv 605467368 1 \"<size=12>Префикс изменен</size>\"");
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
                if (args.Args[0] == "logo")
                {
                    DB[player.userID].LogoEnable = bool.Parse(args.Args[1]);
                    LogoUI(player);
                    player.Command("logo_enable");
                }
                if (args.Args[0] == "sort")
                {
                    DB[player.userID].SortEnable = bool.Parse(args.Args[1]);
                    SortUI(player);
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
                RectTransform = { AnchorMin = "0.09 0", AnchorMax = "1 1", OffsetMax = "0 0" },
                Image = { Color = "0 0 0 0" }
            }, "Menu", Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0.855", AnchorMax = "0.96 1", OffsetMax = "0 0" },
                Text = { Text = $"<size=25><b>Основные настройки</b></size>\nЗдесь вы можете настроить игровой чат и другие настройки сервера.", Color = "1 1 1 0.5", Align = TextAnchor.MiddleLeft, FontSize = 12, Font = "robotocondensed-regular.ttf" }
            }, Layer);

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.04 0", AnchorMax = "0.96 0.83", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=25>Настройки чата:</size></b>\n\nПРЕФИКC\n\nПОДСКАЗКИ\n\nГЛОБАЛЬНЫЙ ЧАТ\n\n\n<b><size=25>Дополнительные настройки:</size></b>\n\nЛОГОТИП\n\nСОРТИРОВКА В ПЕЧАХ", Color = "1 1 1 0.5", Align = TextAnchor.UpperLeft, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, Layer);

            container.Add(new CuiPanel
            {
                RectTransform = { AnchorMin = $"0 0", AnchorMax = $"1 0.25", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                Image = { Color = "0.10 0.13 0.19 1" }
            }, Layer, "InfoChat");

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = "0.02 0", AnchorMax = "1 0.98", OffsetMax = "0 0" },
                Text = { Text = $"<b><size=25>ИНФОРМАЦИЯ</size></b>\n\nПодсказки - сообщения сервера с подсказками (которые отправляются в чат каждые ~5 минут)\nГлобальный чат - отключает общий чат, но информационные сообщения от плагинов и раста остаются\n\nОписание префиксов: 1 - пустой; ツ - Vip; 〄 - MASTER; ₪ - LEGEND; ¥ - EXPLOSIVE; マ - Модератор; 么 - Администратор", Color = "1 1 1 0.5", Align = TextAnchor.UpperLeft, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, "InfoChat");

            float width = 0.06f, height = 0.06f, startxBox = 0.54f, startyBox = 0.745f - height, xmin = startxBox, ymin = startyBox;
            foreach (var check in settings)
            {
                var text = check.Prefix == "" ? "-" : check.Prefix;
                var color = permission.UserHasPermission(player.UserIDString, check.Perm) ? "0.10 0.13 0.19 1" : "0.93 0.24 0.38 0.3";
                var command = permission.UserHasPermission(player.UserIDString, check.Perm) ? $"chat prefix {text}" : "";
                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = $"{xmin} {ymin}", AnchorMax = $"{xmin + width} {ymin + height * 1}", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                    Button = { Color = color, Command = command },
                    Text = { Text = text, Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
                }, Layer);

                xmin += width;
                if (xmin + width >= 1)
                {
                    xmin = startxBox;
                    ymin -= height;
                }
            }

            CuiHelper.AddUi(player, container);
            NameUI(player);
            HintsUI(player);
            ChatEnableUI(player);
            LogoUI(player);
            SortUI(player);
        }

        void NameUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Name");
            var container = new CuiElementContainer();

            container.Add(new CuiLabel
            {
                RectTransform = { AnchorMin = $"0.54 0.75", AnchorMax = $"0.96 0.8", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                Text = { Text = $"Ваш ник: <color={DB[player.userID].Color}>{DB[player.userID].Prefix}</color> <color={DB[player.userID].NameColor}>{player.displayName}</color>", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 10, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, Layer, "Name");

            CuiHelper.AddUi(player, container);
        }

        void HintsUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "Hints");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.54 0.62", AnchorMax = $"0.96 0.68", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                Button = { Color = "0.10 0.13 0.19 1", Command = DB[player.userID].Hints == true ? $"chat hints {false}" : $"chat hints {true}" },
                Text = { Text = !DB[player.userID].Hints ? "ВЫКЛЮЧЕНЫ" : "ВКЛЮЧЕНЫ", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, Layer, "Hints");

            CuiHelper.AddUi(player, container);
        }

        void ChatEnableUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "ChatEnable");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.54 0.555", AnchorMax = $"0.96 0.615", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                Button = { Color = "0.10 0.13 0.19 1", Command = DB[player.userID].ChatEnable == true ? $"chat chatenable {false}" : $"chat chatenable {true}" },
                Text = { Text = !DB[player.userID].ChatEnable ? "ВЫКЛЮЧЕН" : "ВКЛЮЧЕН", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, Layer, "ChatEnable");

            CuiHelper.AddUi(player, container);
        }

        void LogoUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "LogoEnable");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.54 0.36", AnchorMax = $"0.96 0.42", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                Button = { Color = "0.10 0.13 0.19 1", Command = DB[player.userID].LogoEnable == true ? $"chat logo {false}" : $"chat logo {true}" },
                Text = { Text = !DB[player.userID].LogoEnable ? "ВЫКЛЮЧЕН" : "ВКЛЮЧЕН", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, Layer, "LogoEnable");

            CuiHelper.AddUi(player, container);
        }

        void SortUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "SortEnable");
            var container = new CuiElementContainer();

            container.Add(new CuiButton
            {
                RectTransform = { AnchorMin = $"0.54 0.295", AnchorMax = $"0.96 0.355", OffsetMin = "1 1", OffsetMax = "-1 -1" },
                Button = { Color = "0.10 0.13 0.19 1", Command = DB[player.userID].SortEnable == true ? $"chat sort {false}" : $"chat sort {true}" },
                Text = { Text = !DB[player.userID].SortEnable ? "ВЫКЛЮЧЕНА" : "ВКЛЮЧЕНА", Color = "1 1 1 0.5", Align = TextAnchor.MiddleCenter, FontSize = 12, Font = "robotocondensed-regular.ttf", FadeIn = 1f }
            }, Layer, "SortEnable");

            CuiHelper.AddUi(player, container);
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

        private string CyrillicString = "йцукенгшщзхъ\\фывапролджэячсмитьбю.ё 1234567890-=Ё!\"№;%:?*()_+";
        private string LatinString = "qwertyuiop[]\\asdfghjkl;'zxcvbnm,./` 1234567890-=~!@#$%^&*()_+";

        private string ConvertCyrillicToLatin(string latin)
        {
            var result = string.Empty;

            foreach (var c in latin)
            {
                var idx = CyrillicString.IndexOf(c);
                if (idx > 0)
                    result += LatinString[idx];
            }

            return result;
        }
        #endregion

        #region Апи
        bool ApiLogo(BasePlayer player)
        {
            return DB[player.userID].LogoEnable;
        }

        bool ApiSort(BasePlayer player)
        {
            return DB[player.userID].SortEnable;
        }
        #endregion
    }
}