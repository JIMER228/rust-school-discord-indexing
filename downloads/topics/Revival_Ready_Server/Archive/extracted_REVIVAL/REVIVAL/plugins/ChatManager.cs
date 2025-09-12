// ДОБАВЛЕНЫ ЛОГИ НА ВСЁ ( НА ЧАТ НА PM )
// Добавлены команды /chat help and /chat info


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
using Newtonsoft.Json.Linq;
using Network;
using System.Text.RegularExpressions;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("ChatManager", "Developer x SwUn ", "1.0.1")]
    public class ChatManager : RustPlugin
    {

        private static ChatManager m_Instance;
        private const string PERM_MODER = "chatmanager.moderator";
        Dictionary<ulong, double> mutes = new Dictionary<ulong, double>();
        Dictionary<ulong, PlayerData> players = new Dictionary<ulong, PlayerData>();
        private PlayerData defaultData = new PlayerData();
        bool globalMute = false;

        [PluginReference("Clans")]
        Plugin Clans;

        void OnServerInitialized()
        {
            m_Instance = this;
            lang.RegisterMessages(Messages, this, "en");
            Messages = lang.GetMessages("en", this);
            LoadConfig();
            LoadConfigValues();
            LoadData();
                timer.Every(1f, () =>
            {
                List<ulong> toDelete = new List<ulong>();
                toDelete.AddRange(floods.Keys.ToList().Where(flood => --floods[flood] < 0));
                toDelete.ForEach(p => floods.Remove(p));
            });
        }

        void Unload()
        {
            OnServerSave();
        }

     Dictionary<ulong, int> floods = new Dictionary<ulong, int>();

        bool? OnPlayerChat(ConsoleSystem.Arg arg)
        {
            var sender = arg.Player();
            var message = arg.GetString(0, "text").Trim();
int m = 0;
try 
{
    m++;
            if (SpamCheck(sender, message) != null) return true;
    m++;
            if (MuteCheck(sender) != null) return true;
    m++;

            int floodTime;
            if (floods.TryGetValue(sender.userID, out floodTime))
            {
                floodTime++;
                SendReply(sender,string.Format(Messages["floodTimeout"], floodTime));
                floods[sender.userID] = floodTime;
                return true;
            }
            else
            {
                floods[sender.userID] = 3;
            }

            if (Configuration.CapsBlock && !IsModerator(sender))
            {
                 message = RemoveCaps(message);
            }

    m++;
            bool mute;
            var censorMessage = CensorBadWords(message, out mute);
            if (Configuration.BadWordsBlock & mute)
            {
                Mute(sender, Configuration.MuteBadWordsDefault);
            }

    m++;
            var pData = GetPlayerData(sender);
    m++;
            var prefix = config.Get(config.prefixes, pData.Prefix).Format;
    m++;
            var nameColor = config.Get(config.names, pData.NameColor).Format;
    m++;
            var messageColor = config.Get(config.messages, pData.MessageColor).Format;
    m++;

                #region Logs
                        
                LogToFile("ChatManager", $"[{DateTime.Now.ToShortTimeString()}] {sender.displayName}: Сообщение: {message}", this, true); 
                    
                #endregion

    m++;
            var name = Format(nameColor, sender.displayName);
    m++;
            message = Format(messageColor, message);
    m++;
            censorMessage = Format(messageColor, censorMessage);
    m++;
            if (prefix.Length > 0)
                name = GetClanTag(sender.userID) + $"{prefix} {name}";
            else
                name = GetClanTag(sender.userID) + $"{name}";
    m++;
            foreach (var player in BasePlayer.activePlayerList)
            {
                SendChat(player, name, GetPlayerData(player).Censor ? censorMessage : message, sender.userID);
            }
    m++;
     return false;
}
     catch(Exception ex) {
        PrintError($"Ошибка под индексом {m}");
        PrintError(ex.Message);
     }
     return null;
        }

        void SendChat(BasePlayer player, string name, string message, ulong userId = 0)
        {
            if (globalMute == true && !IsModerator(player))
            {
                Reply(player,  "MUTE.ALL.ACTIVE");
                return;
            }
            player.SendConsoleCommand("chat.add", userId,
                string.IsNullOrEmpty(name) ? $"{message}" : $"{name}: {message}");
        }

        public string CensorBadWords(string input, out bool found)
        {
            found = false;
            string temp = input.ToLower();
            foreach (var swear in config.badWords)
            {
                var firstIndex = temp.IndexOf(swear.Key);
                if (firstIndex >= 0 && swear.Value.All(exception => temp.IndexOf(exception) < 0))
                    while (firstIndex < input.Length && input[firstIndex] != ' ')
                    {
                        input = input.Remove(firstIndex, 1);
                        input = input.Insert(firstIndex, "*");
                        firstIndex++;
                        found = true;
                    }
            }
            return input;
        }
        void Mute(BasePlayer player, long time, string reason = "")
        {
            mutes[player.userID] = GrabCurrentTime() + time;
            string message = Messages["USER.MUTED.REASON"];
            if (string.IsNullOrEmpty(reason))
                message = Messages["USER.MUTED"];
            BroadcastChat("", string.Format(message, player.displayName, reason, TimeToString(time)));
        }
        void BroadcastChat(string name, string message, ulong userId = 0, string censormessage = "")
        {
            if (string.IsNullOrEmpty(censormessage)) censormessage = message;
            ConsoleNetwork.BroadcastToAllClients("chat.add", userId, string.IsNullOrEmpty(name) ? $"{censormessage}" : $"{name}: {censormessage}");
        }
        string RemoveCaps(string message)
        {
            var ss = message.Split(' ');
            for (int j = 0; j < ss.Length; j++)
                for (int i = 1; i < ss[j].Length; i++)
                {
                    var sym = ss[j][i];
                    if (char.IsLower(sym)) continue;
                    ss[j] = ss[j].Remove(i, 1);
                    ss[j] = ss[j].Insert(i, char.ToLower(sym).ToString());
                }
            return string.Join(" ", ss);
        }

        bool? MuteCheck(BasePlayer sender)
        {
            double muteTime;
            if (mutes.TryGetValue(sender.userID, out muteTime))
            {
                var remain = muteTime - GrabCurrentTime();
                if (remain >= 0)
                {
                    Reply(sender, "YOU.MUTED", TimeToString(remain));
                    return true;
                }
                mutes.Remove(sender.userID);
            }
            return null;
        }


        public string TimeToString(double time)
        {
            TimeSpan elapsedTime = TimeSpan.FromSeconds(time);
            int hours = elapsedTime.Hours;
            int minutes = elapsedTime.Minutes;
            int seconds = elapsedTime.Seconds;
            int days = Mathf.FloorToInt((float)elapsedTime.TotalDays);
            string s = "";

            if (days > 0) s += $"{days}дн.";
            if (hours > 0) s += $"{hours}ч. ";
            if (minutes > 0) s += $"{minutes}мин. ";
            if (seconds > 0) s += $"{seconds}сек.";
            else s = s.TrimEnd(' ');
            return s;
        }
        bool? SpamCheck(BasePlayer sender, string message)
        {
            if (message.Length > 500)
            {
                sender.Kick("CHAT SPAM > 500 CHARS");
                return false;
            }
            if (message.Length > 100)
            {
                SendReply(sender, "Запрещено отправлять столько символов");
                return false;
            }
            return null;
        }

        #region Clan Helper

        string GetClanTag(ulong id)
        {
            string clantag = (string)Clans?.Call("GetClanOf", id);
            string togetherstring = "<color=" + Configuration.ClanColor + ">[" + clantag + "]</color> ";

            if (clantag != null && !string.IsNullOrEmpty(clantag))
                return togetherstring;

            return null;
        }

        #endregion

        #region Commands

        Dictionary<ulong, ulong> pmHistory = new Dictionary<ulong, ulong>();

        [ChatCommand("pm")]
        void cmdChatPM(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 2)
            {
                Reply(player, "CMD.PM.HELP");
                return;
            }
            var argList = args.ToList();
            argList.RemoveAt(0);
            string message = string.Join(" ", argList.ToArray());
            var reciever = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower()
               .Contains(args[0].ToLower()));
            if (reciever == null)
            {
                Reply(player, "PLAYER.NOT.FOUND", args[0]);
                return;
            }

            if (GetPlayerData(reciever).BlackList.Contains(player.userID))
            {
                Reply(player, "PM.YOU.ARE.BLACK.LIST", reciever.displayName);
                return;
            }

            pmHistory[player.userID] = reciever.userID;
            pmHistory[reciever.userID] = player.userID;

            Reply(player, "PM.SENDER.FORMAT", reciever.displayName, message);
            Reply(reciever, "PM.RECEIVER.FORMAT", player.displayName, message);

                LogToFile("ChatManagerPM", $"[{DateTime.Now.ToShortTimeString()}] {player.displayName} написал {reciever.displayName}: Сообщение: {message}", this, true); 

            if (GetPlayerData(reciever).PMSound)
            {
                Effect.server.Run(Configuration.PrivateSoundMessagePath, reciever.GetNetworkPosition());
            }
        }

        [ChatCommand("r")]
        void cmdChatR(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                Reply(player, "CMD.R.HELP");
                return;
            }
            var argList = args.ToList();
            string message = string.Join(" ", argList.ToArray());
            ulong recieverUserId;

            if (!pmHistory.TryGetValue(player.userID, out recieverUserId))
            {
                Reply(player, "PM.NO.MESSAGES");
                return;
            }
            var reciever = BasePlayer.activePlayerList.FirstOrDefault(p => p.userID == recieverUserId);
            if (reciever == null)
            {
                Reply(player, "PM.PLAYER.LEAVE");
                return;
            }

            if (GetPlayerData(reciever).BlackList.Contains(player.userID))
            {
                Reply(player, "PM.YOU.ARE.BLACK.LIST", reciever.displayName);
                return;
            }

            Reply(player, "PM.SENDER.FORMAT", reciever.displayName, message);
            Reply(reciever, "PM.RECEIVER.FORMAT", player.displayName, message);

             LogToFile("ChatManagerR", $"[{DateTime.Now.ToShortTimeString()}] {player.displayName} написал {reciever.displayName}: Сообщение: {message}", this, true); 

            if (GetPlayerData(reciever).PMSound)
            {
                Effect.server.Run(Configuration.PrivateSoundMessagePath, reciever.GetNetworkPosition());
            }
        }

        [ChatCommand("mute")]
        void cmdChatMute(BasePlayer player, string command, string[] args)
        {
            if (!IsModerator(player))
            {
                Reply(player, "NO.ACCESS");
                return;
            }
            if (args.Length < 2)
            {
                Reply(player, "CMD.MUTE.HELP");
                return;
            }
            var mutePlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower().Contains(args[0].ToLower()));

            if (mutePlayer == null)
            {
                Reply(player, "PLAYER.NOT.FOUND", args[0]);
                return;
            }

            if (mutes.ContainsKey(mutePlayer.userID))
            {
                Reply(player, "USER.ALREADY.MUTED", mutePlayer.displayName, TimeToString(mutes[mutePlayer.userID]));
                return;
            }
            double time = StringToTime(args[1]);
            string reason = "";
            if (args.Length > 2)
                 reason = args[2];
            Mute(mutePlayer, Convert.ToInt64(time), reason);
        }
        [ChatCommand("muteall")]
        void CmdGlobalMute(BasePlayer player, string command, string[] args)
        {
            if (!IsModerator(player))
            {
                Reply(player, "NO.ACCESS");
                return;
            }
            if (args.Length == 0)
            {
                Reply(player, "CMD.MUTE.ALL.HELP");
                return;
            }
            if (args[0] == "on")
            {
                globalMute = true;
                PublicMessage("MUTE.ALL.ENABLED");
                return;
            }
            else
            {
                globalMute = false;
                PublicMessage("MUTE.ALL.DISABLED");
                return;
            }

        }

        [ChatCommand("unmute")]
        void cmdChatUnMute(BasePlayer player, string command, string[] args)
        {
            if (!IsModerator(player))
            {
                Reply(player, "NO.ACCESS");
                return;
            }
            if (args.Length == 0)
            {
                Reply(player, "CMD.UNMUTE.HELP");
                return;
            }
            var mutePlayer = BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower().Contains(args[0].ToLower()));

            if (mutePlayer == null)
            {
                Reply(player, "PLAYER.NOT.FOUND", args[0]);
                return;
            }
            if (!mutes.ContainsKey(mutePlayer.userID))
            {
                SendReply(player, "У игрока нет мута");
                return;
            }
            mutes.Remove(mutePlayer.userID);
            BroadcastChat("", string.Format(Messages["USER.UNMUTED"], mutePlayer.displayName));
        }

        public long StringToTime(string time)
        {
            time = time.Replace(" ", "").Replace("d", "d ").Replace("h", "h ").Replace("m", "m ").Replace("s", "s ").TrimEnd(' ');
            var arr = time.Split(' ');
            long seconds = 0;
            foreach (var s in arr)
            {
                var n = s.Substring(s.Length - 1, 1);
                var t = s.Remove(s.Length - 1, 1);
                int d = int.Parse(t);
                switch (n)
                {
                    case "s":
                        seconds += d;
                        break;
                    case "m":
                        seconds += d * 60;
                        break;
                    case "h":
                        seconds += d * 3600;
                        break;
                    case "d":
                        seconds += d * 86400;
                        break;
                }
            }
            return seconds;
        }
        bool IsModerator(BasePlayer player) => PermissionService.HasPermission(player.userID, PERM_MODER);

        [ChatCommand("chat")]
        void cmdChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                Reply(player, "CMD.CHAT.HELP");
                return;
            }
            var playerData = GetPlayerData(player);
            switch (args[0])
            {
                case "censor":
                    if (args.Length == 1 || (args[1] != "on" && args[1] != "off"))
                    {
                        Reply(player, "CMD.CHAT.CENSOR.HELP");
                        return;
                    }
                    bool censor = args[1] == "on";
                    playerData.Censor = censor;
                    if (censor)
                    {
                        Reply(player, "CENSOR.ENABLED");
                        return;
                    }
                    else
                    {
                        Reply(player, "CENSOR.DISABLED");
                        return;
                    }
                case "ignore":
                    if (args.Length == 2 && args[1] == "list")
                    {
                        if (playerData.BlackList.Count == 0)
                        {
                            Reply(player, "IGNORE.LIST.IS.EMPTY");
                            return;
                        }
                        SendReply(player, Messages["IGNORE.LIST"] + string.Join(", ", playerData.BlackList.Select(p => GetPlayerData(p).Name).ToArray()));
                        return;
                    }
                    if (args.Length < 3 || (args[1] != "add" && args[1] != "remove"))
                    {
                        Reply(player, "CMD.CHAT.IGNORE.HELP");
                        return;
                    }
                    int mode = args[1] == "add" ? 1 : -1;

                    var ignorePlayer =
                        BasePlayer.activePlayerList.FirstOrDefault(p => p.displayName.ToLower()
                            .Contains(args[2].ToLower()));
                    if (ignorePlayer == null)
                    {
                        Reply(player, "PLAYER.NOT.FOUND", args[2]);
                        return;
                    }
                    if (mode == 1)
                    {
                        if (!playerData.BlackList.Contains(ignorePlayer.userID))
                        {
                            playerData.BlackList.Add(ignorePlayer.userID);
                            Reply(player, "USER.ADD.IGNORE.LIST", ignorePlayer.displayName);
                            Reply(ignorePlayer, "YOU.ADD.IGNORE.LIST", player.displayName);
                            return;
                        }
                        else
                        {
                            Reply(player, "USER.IS.IGNORE.LIST", ignorePlayer.displayName);
                            Reply(ignorePlayer, "YOU.REMOVE.IGNORE.LIST", player.displayName);
                            return;
                        }
                    }
                    else
                    {
                        if (playerData.BlackList.Contains(ignorePlayer.userID))
                            playerData.BlackList.Remove(ignorePlayer.userID);
                        Reply(player, "USER.REMOVE.IGNORE.LIST", ignorePlayer.displayName);

                        return;
                    }
                case "sound":
                    if (args.Length == 1 || (args[1] != "on" && args[1] != "off"))
                    {
                        Reply(player, "CMD.CHAT.SOUND.HELP");
                        return;
                    }
                    bool pmSound = args[1] == "on";
                    playerData.PMSound = pmSound;
                    if (pmSound)
                    {
                        Reply(player, "SOUND.ENABLED");
                        return;
                    }
                    else
                    {
                        Reply(player, "SOUND.DISABLED");
                        return;
                    }
                case "prefix":
                    var aviablePrefixes = config.prefixes
                        .Where(p => PermissionService.HasPermission(player.userID, p.Perm) &&
                                    GetPlayerData(player).Prefix != p.Perm).ToList();
                    if (aviablePrefixes.Count == 0)
                    {
                        Reply(player, "NO.AVAILABLE.PREFIXS");
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Reply(player, "CMD.CHAT.PREFIX.HELP",
                            string.Join("\n", aviablePrefixes.Select(p => $"/chat prefix {p.Arg} - {p.Format}").ToArray()));
                        return;
                    }
                    var selectedPrefix = aviablePrefixes.FirstOrDefault(p => p.Arg == args[1]);
                    if (selectedPrefix == null)
                    {
                        Reply(player, "PREFIX.NOT.FOUND", args[1]);
                        return;
                    }
                    playerData.Prefix = selectedPrefix.Perm;
                    Reply(player, "PREFIX.CHANGED", selectedPrefix.Arg);
                    return;
                case "name":
                    var aviableNameColors = config.names
                        .Where(p => PermissionService.HasPermission(player.userID, p.Perm) &&
                                    GetPlayerData(player).NameColor != p.Perm).ToList();
                    if (aviableNameColors.Count == 0)
                    {
                        Reply(player, "NO.AVAILABLE.COLORS");
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Reply(player, "CMD.CHAT.NAME.HELP",
                            string.Join("\n", aviableNameColors.Select(p => $"/chat name {Format(p.Format, p.Arg)}").ToArray()));
                        return;
                    }
                    var selectedNameColor = aviableNameColors.FirstOrDefault(p => p.Arg == args[1]);
                    if (selectedNameColor == null)
                    {
                        Reply(player, "COLOR.NOT.FOUND", args[1]);
                        return;
                    }
                    playerData.NameColor = selectedNameColor.Perm;
                    Reply(player, "NAME.COLOR.CHANGED", selectedNameColor.Arg);
                    return;
                case "message":
                    var aviableMessageColors = config.messages
                        .Where(p => PermissionService.HasPermission(player.userID, p.Perm) &&
                                    GetPlayerData(player).MessageColor != p.Perm).ToList();
                    if (aviableMessageColors.Count == 0)
                    {
                        Reply(player, "NO.AVAILABLE.COLORS");
                        return;
                    }
                    if (args.Length == 1)
                    {
                        Reply(player, "CMD.CHAT.MESSAGE.HELP",
                            string.Join("\n", aviableMessageColors.Select(p => $"/chat message {Format(p.Format, p.Arg)}").ToArray()));
                        return;
                    }
                    var selectedMessageColor = aviableMessageColors.FirstOrDefault(p => p.Arg == args[1]);
                    if (selectedMessageColor == null)
                    {
                        Reply(player, "COLOR.NOT.FOUND", args[1]);
                        return;
                    }
                    playerData.MessageColor = selectedMessageColor.Perm;
                    Reply(player, "MESSAGE.COLOR.CHANGED", selectedMessageColor.Arg);
                    return;
				case "info":
				if (args.Length == 1)
				{
					Reply(player, "CMD.CHAT.INFO.HELP");
					return;
				}
				return;
				case "help":
				if (args.Length == 1)
				{
					Reply(player, "HELP.COMMAND.BIND");
					return;
				}
				return;
            }
        }

        /* [ChatCommand("fake")]
        void cmdchatCommand(BasePlayer player, string command, string[] args)
        {
            if (!IsModerator(player))
            {
                Reply(player, "NO.ACCESS");
                return;
            }
            if (args.Length > 1)
            {
                string line = "";
                string author = "";
                for (int i = 0; i < args.Length; ++i)
                {
                    if (i == 0)
                        author = args[i];
                    else
                        line += " " + args[i];
                }


                Net.sv.write.Start();
                Net.sv.write.PacketID(Message.Type.ConsoleCommand);
                Net.sv.write.String("chat.add2 " + (76561197960279927 + (ulong)(UnityEngine.Random.Range(1, 1000))) + " \"" + line.Remove(0, 1) + "\" \"<color=#5af>" + author + "</color>\"");
                Net.sv.write.Send(new SendInfo(true));
            }
        } */

        void Reply(BasePlayer player, string langKey, params string[] args)
        {
            SendReply(player, Format(Messages[langKey], args));
        }

        void PublicMessage(string langKey)
        {
            rust.BroadcastChat(Format(Messages[langKey]));
        }

        string Format(string input, params string[] args)
        {
            string ret = input;
            for (int argIndex = 0; argIndex < args.Length; argIndex++)
                ret = ret.Replace($"{{{argIndex}}}", args[argIndex]);
            return ret;
        }

        static double GrabCurrentTime() => DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;

#endregion

#region Data

        public class PlayerData
        {
            public string Prefix = "chatmanager.default";
            public string NameColor = "chatmanager.default";
            public string MessageColor = "chatmanager.default";
            public bool Censor = true;
            public bool PMSound = true;
            public string Name = "";
            public List<ulong> BlackList = new List<ulong>();
        }

        PlayerData GetPlayerData(BasePlayer player)
        {
            var data = GetPlayerData(player.userID);
            data.Name = player.displayName;
            return data;
        }
        PlayerData GetPlayerData(ulong userId)
        {
            PlayerData config;
            if (players.TryGetValue(userId, out config))
            {
                if (config.Prefix != "chatmanager.default" && !PermissionService.HasPermission(userId, config.Prefix)) config.Prefix = "chatmanager.default";
                if (config.NameColor != "chatmanager.default" && !PermissionService.HasPermission(userId, config.NameColor)) config.NameColor = "chatmanager.default";
                if (config.MessageColor != "chatmanager.default" && !PermissionService.HasPermission(userId, config.MessageColor)) config.MessageColor = "chatmanager.default";
                return config;
            }
            config = new PlayerData();
            players[userId] = config;
            return config;
        }

        DynamicConfigFile mutes_File = Interface.Oxide.DataFileSystem.GetFile("ChatManager/ChatManager_Mutes");
        DynamicConfigFile players_File = Interface.Oxide.DataFileSystem.GetFile("ChatManager/ChatManager_Players");

        void LoadData()
        {
            mutes = mutes_File.ReadObject<Dictionary<ulong, double>>();
            players = players_File.ReadObject<Dictionary<ulong, PlayerData>>();
        }

        void OnServerSave()
        {
            mutes_File.WriteObject(mutes);
            players_File.WriteObject(players.Where(p => !p.Value.Equals(defaultData)).ToDictionary(p => p.Key, p => p.Value));
        }

#endregion

#region Localization

        private Dictionary<string, string> Messages = new Dictionary<string, string>()
        {
            {"CMD.CHAT.HELP", "ДОСТУПНЫЕ КОМАНДЫ:\n/chat censor - цензура в чате\n/chat prefix - доступные префиксы\n/chat name - доступные цвета имени\n/chat message - доступные цвета сообщений\n/chat ignore - черный список\n/chat sound - звук при получении ЛС" },
            {"CMD.CHAT.HELP.PERMISSION", "\n/chat admin - режим администратора\n/chat moderator - режим модератора"},
            {"CMD.CHAT.PREFIX.HELP", "ДОСТУПНЫЕ ПРЕФИКСЫ:\n{0}" },
            {"NO.AVAILABLE.PREFIXS", "У вас нет доступных префиксов" },
            {"PREFIX.RESET", "Префикс успешно удален" },
            {"PREFIX.NOT.FOUND", "Префикс с названием \"{0}\" не найден" },
            {"NO.ACCESS.THIS.PREFIX", "У вас нет доступа к данному префиксу" },
            {"PREFIX.CHANGED", "Префикс изменен на {0}" },
            {"CMD.CHAT.NAME.HELP", "ДОСТУПНЫЕ ЦВЕТА:\n{0}" },
			{"CMD.CHAT.INFO.HELP", "Информация сервера \n Рейты на сервере - <color=#86d3db>x2</color> | На скрап <color=#86d3db>x1.5</color> \n Максимум игроков в команде - <color=#86d3db>3</color> \n Максимальная карта - <color=#86d3db>2850-2950</color> \n Группа ВК - <color=#86d3db>vk.com/old.island</color> \n Сайт сервера - <color=#86d3db>store.oldisland.cf</color> \n Интересные плагины - <color=#86d3db>/menu , /kit , /trade , /friend</color>" },
            {"NO.AVAILABLE.COLORS", "У вас нет доступных цветов" },
			{"HELP.COMMAND.BIND", "Команды сервера \n <color=#86d3db>/menu \n /kit \n /transfer \n /block \n /trade</color> \n Бинды сервера - \n <color=#86d3db>bind Кнопка chat.say /up \n bind Кнопка chat.say /remove \n bind Кнопка chat.say /menu \n bind Кнопка chat.say /tpa</color>" },
            {"NAME.COLOR.RESET", "Цвет успешно сброшен" },
            {"COLOR.NOT.FOUND", "Цвет с названием \"{0}\" не найден" },
            {"NO.ACCESS.THIS.COLOR", "У вас нет доступа к данному цвету"},
            {"NO.NAME.COLOR.CHANGED", "Вы не можете изменить цвет имени игрока, для начала установите один из доступных префиксов" },
            {"NAME.COLOR.CHANGED", "Цвет имени успешно изменен на {0}" },
            {"CMD.CHAT.MESSAGE.HELP", "ДОСТУПНЫЕ ЦВЕТА:\n{0}" },
            {"NO.MESSAGE.COLOR.CHANGED", "Вы не можете изменить цвет сообщений, для начала установите один из доступных префиксов" },
            {"MESSAGE.COLOR.CHANGED", "Цвет чат сообщений успешно изменен на {0}" },
            {"CMD.CHAT.SOUND.HELP", "Используйте /chat sound on или /chat sound off чтобы включить или выключить звуковое оповещение при получении ЛС" },
            {"SOUND.ENABLED", "Вы включили звуковое оповещение при получении ЛС" },
            {"SOUND.DISABLED", "Вы выключили звуковое оповещение при получении ЛС" },
            {"CMD.MUTE.HELP", "Используйте /mute \"имя игрока\" <длительность> <причина> чтобы заблокировать чат игроку" },
            {"USER.ALREADY.MUTED", "Игрок \"{0}\" уже заблокирован."},
            {"TIME.FORMAT.ERROR", "Некорректный формат времени.\nИспользуйте: #d дни #h часы #m минуты #s секунды.\nПример 1 час 30 минут: 1h30m" },
            {"USER.MUTED", "Игроку \"{0}\" заблокировали чат.\nДлительность: {2}" },
            {"USER.MUTED.REASON", "Игроку \"{0}\" заблокировали чат.\nДлительность: {2}\nПричина: {1}" },
            {"USER.MUTED.LOG", "\"{0}\" заблокировал чат \"{1}/{2}\" длительность \"{3}\"" },
            {"CMD.UNMUTE.HELP", "Используйте /unmute \"имя игрока\" чтобы разблокировать чат игроку" },
            {"USER.UNMUTED", "Игроку \"{0}\" разблокировали чат" },
            {"USER.UNMUTED.LOG", "\"{0}\" разблокировал чат \"{1}/{2}\"" },
            {"YOU.MUTED", "Ваш чат заблокирован!\nОсталось: {0}" },
            {"CMD.MUTE.ALL.HELP", "Используйте /muteall on или /muteall off чтобы заблокировать или разблокировать общий чат" },
            {"MUTE.ALL.ENABLED", "Общий чат заблокирован" },
            {"MUTE.ALL.DISABLED", "Общий чат разблокирован" },
            { "MUTE.ALL.ACTIVE", "Глобальное отключение чата активно, вы не можете общаться в чате" },
            {"FLOOD.PROTECTION", "Защита от флуда, подождите {0}" },
            {"CMD.CHAT.IGNORE.HELP", "СПИСОК КОМАНД:\n/chat ignore add \"имя игрока\" - добавить в черный список\n/chat ignore remove \"имя игрока\" - удалить из черного списка\n/chat ignore list - показать черный список"},
            {"USER.IS.IGNORE.LIST", "Игрок \"{0}\" уже находится в черном списке" },
            {"USER.ADD.IGNORE.LIST", "Вы добавили игрока \"{0}\" в черный список" },
            {"YOU.ADD.IGNORE.LIST", "Игрок \"{0}\" добавил вас в черный список" },
            {"IGNORE.LIST.IS.EMPTY", "Черный список пуст" },
            {"USER.REMOVE.IGNORE.LIST", "Вы удалили игрока \"{0}\" из черного списка" },
            {"YOU.REMOVE.IGNORE.LIST", "Игрок \"{0}\" удалил вас из черного списка" },
            {"IGNORE.LIST", "ЧЁРНЫЙ СПИСОК:\n" },
            {"CMD.PM.HELP", "Используйте /pm \"имя игрока\" \"сообщение\" чтобы отправить ЛС игроку" },
            {"PM.SENDER.FORMAT", "<color=#e664a5>ЛС для {0}</color>: {1}"},
            {"PM.RECEIVER.FORMAT",  "<color=#e664a5>ЛС от {0}</color>: {1}" },
            {"PM.NO.MESSAGES", "Вы не получали личных сообщений" },
            {"PM.PLAYER.LEAVE", "Игрок с которым вы переписывались вышел с сервера" },
            {"PM.YOU.ARE.BLACK.LIST", "Вы не можете отправить ЛС игроку \"{0}\", он добавил вас в черный список" },
            {"CMD.R.HELP", "Используйте /r \"сообщение\" чтобы ответить но последнее ЛС" },
            {"CMD.CHAT.CENSOR.HELP", "Используйте /chat censor on или /chat censor off чтобы включить или выключить показ нецензурные слов в чате" },
            {"CENSOR.ENABLED", "Вы включили цензуру в чате" },
            {"CENSOR.DISABLED", "Вы выключили цензуру в чате" },
            {"NO.ACCESS", "У вас нет доступа к этой команде" },
            {"PLAYER.NOT.FOUND", "Игрок \"{0}\" не найден" },
            {"MULTIPLE.PLAYERS.FOUND", "НАЙДЕНО НЕСКОЛЬКО ИГРОКОВ:\n{0}"},
            {"floodTimeout", "Слишком быстро отправляете сообщения, попробуйте через {0} сек."}
        };

#endregion

#region Configuration

        private void GetConfig<T>(ref T variable, params string[] path)
        {
            if (path.Length == 0)
                return;

            if (Config.Get(path) == null)
            {
                Config.Set(path.Concat(new object[] { variable }).ToArray());
                PrintWarning($"Added field to config: {string.Join("/", path)}");
            }

            variable = (T)Convert.ChangeType(Config.Get(path), typeof(T));
        }

        public static class Configuration
        {
            public static bool CapsBlock = true;
            public static bool BadWordsBlock = true;
            public static bool PrivateSoundMessage = true;
            public static string PrivateSoundMessagePath = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab";
            public static int MuteDefault = 900;
            public static int MuteBadWordsDefault = 1800;
            public static string ClanColor = "#FFA500";
            public static bool UserLink = true;
        }

#endregion

#region Loading

        protected override void LoadDefaultConfig() => PrintWarning("Создание нового файла конфигурации...");

        private void LoadConfigValues()
        {
            GetConfig(ref Configuration.CapsBlock, "Выключить заглавные буквы в чате");
            GetConfig(ref Configuration.BadWordsBlock, "Автоматически блокировать чат за нецензурную лексику");
            GetConfig(ref Configuration.PrivateSoundMessage, "Воспроизводить звук при получении личного сообщения");
            GetConfig(ref Configuration.PrivateSoundMessagePath, "Полный путь к звуковому файлу");
            GetConfig(ref Configuration.MuteDefault, "Длительность блокировки чата по умолчанию(в секундах)");
            GetConfig(ref Configuration.MuteBadWordsDefault, "Длительность блокировки чата за нецензурную лексику(в секундах)");
            GetConfig(ref Configuration.ClanColor, "Цвет тега клана");
            GetConfig(ref Configuration.UserLink, "Убирать ссылки с ника");

            SaveConfig();
        }

        public class ChatPrivilege
        {
            [JsonProperty("Привилегия")]
            public string Perm;
            [JsonProperty("Аргумент")]
            public string Arg;
            [JsonProperty("Формат")]
            public string Format;
        }

        public class ChatConfig
        {
            [JsonProperty("Префиксы")]
            public List<ChatPrivilege> prefixes;
            [JsonProperty("Имена")]
            public List<ChatPrivilege> names;
            [JsonProperty("Сообщения")]
            public List<ChatPrivilege> messages;
            [JsonProperty("Список начальных букв нецензурных слов или слова целиком | список исключений")]
            public Dictionary<string, List<string>> badWords;

            public void RegisterPerms()
            {
                PermissionService.RegisterPermissions(prefixes.Select(p => p.Perm).ToList());
                PermissionService.RegisterPermissions(names.Select(p => p.Perm).ToList());
                PermissionService.RegisterPermissions(messages.Select(p => p.Perm).ToList());
                PermissionService.RegisterPermissions(new List<string>() { PERM_MODER });
            }

            public ChatPrivilege Get(List<ChatPrivilege> list, string perm)
            {
                return list.FirstOrDefault(p => p.Perm == perm) ?? list.First();
            }
        }

        private DynamicConfigFile configFile = Interface.Oxide.DataFileSystem.GetFile("ChatManager/ChatManager_Config");
        private ChatConfig config;
        public new void LoadConfig()
        {
            if (!configFile.Exists())
            {
                configFile.WriteObject(config = new ChatConfig()
                {
                    prefixes = new List<ChatPrivilege>()
                    {
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.default",
                            Arg = "default",
                            Format = "<color=#ffffff>[Player]</color>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.lite",
                            Arg = "lite",
                            Format = "<color=#99ff99>[Lite]</color>",
                        },
                         new ChatPrivilege()
                        {
                            Perm = "chatmanager.premium",
                            Arg = "premium",
                            Format = "<color=#FFD700>[premium]</color>",
                        },
                         new ChatPrivilege()
                        {
                            Perm = "chatmanager.revival",
                            Arg = "revival",
                            Format = "<color=#42aaff>[revival]</color>",
                        },
                         new ChatPrivilege()
                        {
                            Perm = "chatmanager.youtube",
                            Arg = "youtube",
                            Format = "<color=#ff0000>[youtuber]</color>",
                        },
                         new ChatPrivilege()
                        {
                            Perm = "chatmanager.admin",
                            Arg = "admin",
                            Format = "<color=#000000>[admin]</color>",
                        },
                         new ChatPrivilege()
                        {
                            Perm = "chatmanager.zervila",
                            Arg = "zervila",
                            Format = "<color=#8b00ff>[zervila]</color>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.mod",
                            Arg = "mod",
                            Format = "<color=#30d5c8>[moderator]</color>",
                        }
                    },
                    names = new List<ChatPrivilege>()
                    {
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.default",
                            Arg = "default",
                            Format = "<color=#55aaff>{0}</color>",
                        },
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.purple",
                            Arg = "purple",
                            Format = "<color=#a5e664>{0}</color>",
                        }
                    },
                    messages = new List<ChatPrivilege>()
                    {
                        new ChatPrivilege()
                        {
                            Perm = "chatmanager.default",
                            Arg = "default",
                            Format = "<color=#ffffff>{0}</color>",
                        }
                    },
                    badWords = new Dictionary<string, List<string>>()
                    {
                        { "ебля", new List<string>() },
                        { "сука", new List<string>() },
                        { "пидор", new List<string>() },
                    }
                });
            }
            else
            {
                config = configFile.ReadObject<ChatConfig>();
            }
            config.RegisterPerms();
        }
#endregion

#region PermissionService

        public static class PermissionService
        {
            public static Permission permission = Interface.GetMod().GetLibrary<Permission>();

            public static bool HasPermission(ulong uid, string permissionName)
            {
                return !string.IsNullOrEmpty(permissionName) && permission.UserHasPermission(uid.ToString(), permissionName);
            }

            public static void RegisterPermissions(List<string> permissions)
            {
                if (permissions == null) throw new ArgumentNullException("commands");

                foreach (var permissionName in permissions.Where(permissionName => !permission.PermissionExists(permissionName)))
                {
                    permission.RegisterPermission(permissionName, m_Instance);
                }
            }
        }

#endregion
    }
}