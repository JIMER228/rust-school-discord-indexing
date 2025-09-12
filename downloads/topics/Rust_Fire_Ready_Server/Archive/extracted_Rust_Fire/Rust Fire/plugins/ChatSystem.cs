using System;
using System.Collections.Generic;
using System.Linq;
using ConVar;
using Facepunch;
using Facepunch.Math;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;

namespace Oxide.Plugins
{
    [Info("Chat System", "Orange/wazzzup", "1.0.3")]
    [Description("New chat system for you!")]
    public class ChatSystem : RustPlugin
    {
        #region Vars

        private const string permModerate = "chatsystem.moderator";

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            foreach (var value in config.prefix)
            {
                permission.RegisterPermission(value.perm, this);
            }

            lang.RegisterMessages(EN, this);
            permission.RegisterPermission(permModerate, this);
            cmd.AddChatCommand("chat", this, "cmdChat");
            cmd.AddChatCommand("mute", this, "cmdMute");
            cmd.AddChatCommand("unmute", this, "cmdUnMute");
            LoadData();
        }

        private void Unload()
        {
            SaveData();
        }

        object OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (channel == ConVar.Chat.ChatChannel.Team) return null;
            OnChat(player.IPlayer, message.Trim());
            return false;
        }

        #endregion

        #region Core

        private void OnChat(IPlayer player, string message)
        {
            var id = player.Id;
            playersData.TryAdd(id, new PlayerSettings());

            if (Spam(player, message))
            {
                return;
            }

            if (Flooding(player))
            {
                return;
            }

            if (IsMuted(player))
            {
                return;
            }

            if (IsBadMessage(player, message))
            {
                playersData[id].muteDuration = config.words.autoMuteTime;
                playersData[id].muteTime = Now();
                return;
            }

            if (config.global.log)
            {
                LogToFile("global", $"[{DateTime.UtcNow.ToLongTimeString()}] [{player.Id}] {player.Name}: {message}", this);
            }

            message = GetFormattedMessage(player, message);
            playersData[id].lastMessage = Now();
            SendMessage(message, id, player.Name);
        }

        private string GetFormattedMessage(IPlayer player, string message)
        {
            var id = player.Id;
            var disabled = playersData[id].prefixDisabled;

            foreach (var settings in config.prefix)
            {
                if (disabled && !settings.perm.Contains("default"))
                {
                    continue;
                }

                if (permission.UserHasPermission(id, settings.perm))
                {
                    return settings.format
                        .Replace("{tag}", settings.tag)
                        .Replace("{name}", player.Name)
                        .Replace("{message}", message);
                }
            }

            return string.Empty;
        }

        private void SendMessage(string message, string sender, string senderName)
        {
            ConsoleNetwork.BroadcastToAllClients("chat.add", 0, sender, message);
            ConsoleNetwork.SendClientCommand(Network.Net.sv.connections, $"echo {message}");
            
            RCon.Broadcast(RCon.LogType.Chat, new Chat.ChatEntry
            {
                Channel = Chat.ChatChannel.Global,
                Message = message,
                UserId = sender,
                Username = senderName,
                Color = "white",
                Time = Epoch.Current
            });
        }

        private bool Spam(IPlayer player, string message)
        {
            if (message.Length > 200)
            {
                player.Message(getMessage(player.Id, "Too Long"));
                return true;
            }

            return false;
        }

        private bool Flooding(IPlayer player)
        {
            try
            {
                var id = player.Id;
                var last = playersData[id].lastMessage;
                var left = config.global.chatCooldown - Passed(last);

                if (left > 0)
                {
                    player.Message(getMessage(id, "Too Fast"));
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        private bool IsMuted(IPlayer player)
        {
            var id = player.Id;
            var info = playersData[id];
            if (info.muteDuration < 0.1)
            {
                return false;
            }

            var left = info.muteDuration - Passed(info.muteTime);
            if (left > 0)
            {
                player.Message(getMessage(id, "Still Muted", GetTimeString(Convert.ToInt32(left))));
                return true;
            }
            else
            {
                playersData[id].muteDuration = 0;
                playersData[id].muteTime = 0;
                return false;
            }
        }

        private bool IsBadMessage(IPlayer player, string message)
        {
            var words = message.ToLower().Split(' ');

            foreach (var word in words)
            {
                if (config.words.whitelist.Contains(word))
                {
                    continue;
                }

                foreach (var badWord in config.words.blacklist)
                {
                    if (word.Contains(badWord))
                    {
                        player.Message(getMessage(player.Id, "Bad Words"));
                        return true;
                    }
                }
            }

            return false;
        }

        #endregion

        #region Helpers

        private double Now()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1, 0, 0, 0)).TotalSeconds;
        }

        private int Passed(double a)
        {
            return Convert.ToInt32(Now() - a);
        }

        private string GetTimeString(int time)
        {
            var timeString = string.Empty;
            var days = time / 86400;
            time = time % 86400;
            if (days > 0)
            {
                timeString += days + " д";
            }

            var hours = time / 3600;
            time = time % 3600;
            if (hours > 0)
            {
                if (days > 0)
                {
                    timeString += ", ";
                }

                timeString += hours + " ч";
            }

            var minutes = time / 60;
            time = time % 60;
            if (minutes > 0)
            {
                if (hours > 0)
                {
                    timeString += ", ";
                }

                timeString += minutes + " мин";
            }

            var seconds = time;
            if (seconds > 0)
            {
                if (minutes > 0)
                {
                    timeString += ", ";
                }

                timeString += seconds + " сек";
            }

            return timeString;
        }

        #endregion

        #region Configuration

        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "A. Global chat settings")]
            public GlobalChatSettings global;

            [JsonProperty(PropertyName = "B. Words settings")]
            public WordsSettings words;

            [JsonProperty(PropertyName = "C. Prefixes")]
            public List<ChatPrefix> prefix;
        }

        private class ChatPrefix
        {
            [JsonProperty(PropertyName = "1. Permission")]
            public string perm;

            [JsonProperty(PropertyName = "2. Tag")]
            public string tag;

            [JsonProperty(PropertyName = "3. Format")]
            public string format;
        }

        private class WordsSettings
        {
            [JsonProperty(PropertyName = "1. Blacklist")]
            public List<string> blacklist;

            [JsonProperty(PropertyName = "2. Whitelist")]
            public List<string> whitelist;

            [JsonProperty(PropertyName = "3. Auto-mute for bad word")]
            public int autoMuteTime;
        }

        private class GlobalChatSettings
        {
            [JsonProperty(PropertyName = "1. Message cooldown")]
            public int chatCooldown;

            [JsonProperty(PropertyName = "2. Log messages")]
            public bool log;
        }

        private ConfigData GetDefaultConfig()
        {
            return new ConfigData
            {
                global = new GlobalChatSettings
                {
                    chatCooldown = 3,
                    log = true,
                },
                prefix = new List<ChatPrefix>
                {
                    new ChatPrefix
                    {
                        perm = "chatsystem.admin",
                        tag = "<color=#ff0000>[ADMIN]</color>",
                        format = "{tag} {name}: {message}"
                    },
                    new ChatPrefix
                    {
                        perm = "chatsystem.vip",
                        tag = "<color=#f7ff00>[VIP]</color>",
                        format = "{tag} {name}: {message}"
                    },
                    new ChatPrefix
                    {
                        perm = "chatsystem.vip_plus",
                        tag = "<color=#f7ff00>[VIP+]</color>",
                        format = "{tag} {name}: {message}"
                    },
                    new ChatPrefix
                    {
                        perm = "chatsystem.default",
                        tag = "",
                        format = "{name}: {message}"
                    }
                },
                words = new WordsSettings
                {
                    blacklist = new List<string>
                    {
                        "shit",
                        "fuck",
                        "pussy"
                    },
                    whitelist = new List<string>
                    {
                        "bullshit",
                        "shits",
                        "fuckup"
                    },
                    autoMuteTime = 600
                }
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();

                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            PrintError("Configuration file is corrupt(or not exists), creating new one!");
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Localization

        private Dictionary<string, string> EN = new Dictionary<string, string>
        {
            {"Too Long", "Your message is too long!"},
            {"Too Fast", "Your are messaging too fast!"},
            {"Still Muted", "You are still muted for {0}!"},
            {"Bad Words", "Your message contains bad words. It will be not sent :("},
            {"Usage", "Usage:\n/chat global - toggle global chat\n/chat prefix - toggle prefixes"},
            {"Chat Enabled", "Global chat was Enabled!"},
            {"Chat Disabled", "Global chat was Disabled!"},
            {"Prefix Enabled", "Prefix was Enabled!"},
            {"Prefix Disabled", "Prefix was Disabled!"},
            {"Permission", "You don't have access to that!"},
            {"Muted", "{0} was muted for {1}"},
            {"Usage Mute", "Usage:\n/mute <name or id> <duration>\n/unmute <name or id>"},
            {"No Player", "Can't find that player!"},
            {"Unmuted", "{0} was unmuted"},
            {"Multiple", "Multiply players found:\n{0}"}
        };

        private string getMessage(string playerID, string key, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, playerID), args);
        }

        #endregion

        #region Data

        private Dictionary<string, PlayerSettings> playersData = new Dictionary<string, PlayerSettings>();

        private class PlayerSettings
        {
            public double lastMessage;
            public double muteDuration;
            public double muteTime;
            public bool chatDisabled;
            public bool prefixDisabled;
        }

        private const string filename = "ChatSystem/player_data";

        private void LoadData()
        {
            try
            {
                playersData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, PlayerSettings>>(filename);
            }
            catch (Exception e)
            {
                PrintWarning(e.Message);
            }

            SaveData();
            timer.Every(550f, SaveData);
        }

        private void SaveData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(filename, playersData);
        }

        #endregion

        #region Commands

        private void cmdChat(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;

            if (args == null || args?.Length == 0)
            {
                player.ChatMessage(getMessage(id, "Usage"));
                return;
            }

            playersData.TryAdd(player.UserIDString, new PlayerSettings());
            var action = args[0].ToLower();

            switch (action)
            {
                case "global":
                    var toggle = !playersData[id].chatDisabled;
                    playersData[id].chatDisabled = toggle;
                    player.ChatMessage(getMessage(id, toggle ? "Chat Disabled" : "Chat Enabled"));
                    break;

                case "prefix":
                    var prefix = !playersData[id].prefixDisabled;
                    playersData[id].prefixDisabled = prefix;
                    player.ChatMessage(getMessage(id, prefix ? "Prefix Disabled" : "Prefix Enabled"));
                    break;
                default:
                    player.ChatMessage(getMessage(id, "Usage"));
                    break;
            }
        }

        [ConsoleCommand("mute")]
        private void cmdMuteConsole(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }

            var args = arg.Args;
            if (args == null || args.Length < 2)
            {
                SendReply(arg, getMessage(null, "Usage Mute"));
                return;
            }

            var name = args[0];
            var duration = 0;
            int.TryParse(args[1], out duration);
            var targets = BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(name.ToLower())).ToList();
            if (targets.Count == 0)
            {
                SendReply(arg, getMessage(null, "No Player"));
                return;
            }

            if (targets.Count > 1)
            {
                SendReply(arg, getMessage(null, "Multiple", targets.ToSentence()));
                return;
            }

            var target = targets[0];
            var settings = (PlayerSettings) null;
            if (playersData.TryGetValue(target.UserIDString, out settings) == false)
            {
                settings = new PlayerSettings();
                playersData.Add(target.UserIDString, settings);
            }

            var left = (double) 0;
            if (settings.muteDuration > 0.1)
            {
                left = settings.muteDuration - Passed(settings.muteTime);
            }

            duration += Convert.ToInt32(left);
            playersData[target.UserIDString].muteDuration = duration;
            playersData[target.UserIDString].muteTime = Now();
            SendReply(arg, getMessage(null, "Muted", target.displayName, GetTimeString(duration)));
            Server.Broadcast(getMessage(null, "Muted", target.displayName, GetTimeString(duration)));
        }

        [ConsoleCommand("unmute")]
        private void cmdUnMuteConsole(ConsoleSystem.Arg arg)
        {
            if (!arg.IsAdmin)
            {
                return;
            }

            var args = arg.Args;
            if (args == null || args?.Length < 1)
            {
                PrintWarning(getMessage(null, "Usage Mute"));
                return;
            }

            var name = args[0];
            var target = BasePlayer.Find(name);
            if (target == null)
            {
                PrintWarning(getMessage(null, "No Player"));
                return;
            }

            playersData.TryAdd(target.UserIDString, new PlayerSettings());
            playersData[target.UserIDString].muteDuration = 0;
            playersData[target.UserIDString].muteTime = 0;
            Server.Broadcast(getMessage(null, "Unmuted", target.displayName));
        }

        private void cmdMute(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;

            if (!permission.UserHasPermission(id, permModerate))
            {
                player.ChatMessage(getMessage(id, "Permission"));
                return;
            }

            if (args == null || args?.Length < 2)
            {
                player.ChatMessage(getMessage(id, "Usage Mute"));
                return;
            }

            var name = args[0];
            var duration = Convert.ToInt32(args[1]);
            var targets = BasePlayer.activePlayerList.Where(x => x.displayName.ToLower().Contains(name.ToLower())).ToList();

            if (targets.Count == 0)
            {
                player.ChatMessage(getMessage(id, "No Player"));
                return;
            }

            if (targets.Count > 1)
            {
                player.ChatMessage(getMessage(id, "Multiple", targets.ToSentence()));
                return;
            }

            var target = targets[0];
            playersData.TryAdd(target.UserIDString, new PlayerSettings());
            playersData[target.UserIDString].muteDuration = duration;
            playersData[target.UserIDString].muteTime = Now();
            Server.Broadcast(getMessage(null, "Muted", target.displayName, GetTimeString(duration)));
        }

        private void cmdUnMute(BasePlayer player, string command, string[] args)
        {
            var id = player.UserIDString;

            if (!permission.UserHasPermission(id, permModerate))
            {
                player.ChatMessage(getMessage(id, "Permission"));
                return;
            }

            if (args == null || args?.Length < 1)
            {
                player.ChatMessage(getMessage(id, "Usage Mute"));
                return;
            }

            var name = args[0];
            var target = BasePlayer.Find(name);
            if (target == null)
            {
                player.ChatMessage(getMessage(id, "No Player"));
                return;
            }

            playersData.TryAdd(target.UserIDString, new PlayerSettings());
            playersData[target.UserIDString].muteDuration = 0;
            playersData[target.UserIDString].muteTime = 0;
            Server.Broadcast(getMessage(null, "Unmuted", target.displayName));
        }

        #endregion
    }
}