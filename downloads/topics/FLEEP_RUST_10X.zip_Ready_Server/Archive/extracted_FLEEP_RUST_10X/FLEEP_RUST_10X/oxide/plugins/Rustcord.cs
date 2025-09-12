using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core.Plugins;
using UnityEngine;
using Facepunch;
using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Core.Libraries.Covalence;
using Oxide.Ext.Discord.Builders;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Entities.Channels;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Commands;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Messages.Embeds;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Logging;

namespace Oxide.Plugins
{
    [Info("Rustcord", "https://discord.gg/9vyTXsJyKR", "3.0.0")]
    [Description("Complete game server monitoring through discord.")]
    internal class Rustcord : RustPlugin
    {
        [PluginReference] Plugin PrivateMessages, BetterChatMute, Clans, AdminChat, DiscordAuth, AdminHammer, AdminRadar, Vanish, RaidableBases;
        [DiscordClient] private DiscordClient _client;

        private Settings _settings;
        private int? _channelCount;
        private Snowflake _botId;

        private UpdatePresenceCommand DiscordPresence = new UpdatePresenceCommand
        {
            Activities = new List<DiscordActivity>
            {
                new DiscordActivity
                {
                    Type = ActivityType.Game,
                    Name = "Rustcord Initializing..."
                }
            }
        };
        private Timer StatusTimer = null;

        private class Settings
        {
            [JsonProperty(PropertyName = "General Settings")]
            public GeneralSettings General { get; set; }

            [JsonProperty(PropertyName = "Discord to Game Settings")]
            public DiscordSideSettings DiscordSide { get; set; }

            [JsonProperty(PropertyName = "Rust Logging Settings")]
            public GameLogSettings GameLog { get; set; }

            [JsonProperty(PropertyName = "Plugin Logging Settings")]
            public PluginLogSettings PluginLog { get; set; }

            [JsonProperty(PropertyName = "Discord Output Formatting")]
            public OutputSettings OutputFormat { get; set; }

            [JsonProperty(PropertyName = "Logging Exclusions")]
            public ExcludedSettings Excluded { get; set; }

            [JsonProperty(PropertyName = "Filter Settings")]
            public FilterSettings Filters { get; set; }

            [JsonProperty(PropertyName = "Discord Logging Channels")]
            public List<Channel> Channels { get; set; }

            [JsonProperty(PropertyName = "Discord Command Role Assignment (Empty = All roles can use command.)")]
            public Dictionary<string, List<string>> Commandroles { get; set; }

            public class Channel
            {
                [JsonProperty(PropertyName = "Discord Channel ID #")]
                public Snowflake Channelid { get; set; }

                [JsonProperty(PropertyName = "Channel Flags")]
                public List<string> perms { get; set; }

                [JsonProperty(PropertyName = "Custom: Words/Phrases to Log")]
                public List<string> CustomFilter { get; set; }
            }



        }

        public class GeneralSettings
        {
            [JsonProperty(PropertyName = "API Key (Bot Token)")]
            public string Apikey { get; set; }

            [JsonProperty(PropertyName = "Auto Reload Plugin")]
            public bool AutoReloadPlugin { get; set; }

            [JsonProperty(PropertyName = "Auto Reload Time (Seconds)")]
            public int AutoReloadTime { get; set; }

            [JsonProperty(PropertyName = "Enable Bot Status")]
            public bool EnableBotStatus { get; set; }

            [JsonProperty(PropertyName = "In-Game Rp Command")]
            public string ReportCommand { get; set; }

            [JsonConverter(typeof(StringEnumConverter))]
            [DefaultValue(DiscordLogLevel.Info)]
            [JsonProperty(PropertyName = "Discord Extension Log Level (Verbose/Debug/Info/Warning/Error/Exception/Off)")]
            public DiscordLogLevel ExtensionDebugging { get; set; } = DiscordLogLevel.Info;
        }

        public class DiscordSideSettings
        {
            [JsonProperty(PropertyName = "Discord Command Prefix")]
            public string Commandprefix { get; set; }

            [JsonProperty(PropertyName = "Discord to Game Chat: Icon (Steam ID)")]
            public ulong GameChatIconSteamID { get; set; }

            [JsonProperty(PropertyName = "Discord to Game Chat: Tag")]
            public string GameChatTag { get; set; }

            [JsonProperty(PropertyName = "Discord to Game Chat: Tag Color (Hex)")]
            public string GameChatTagColor { get; set; }

            [JsonProperty(PropertyName = "Discord to Game Chat: Player Name Color (Hex)")]
            public string GameChatNameColor { get; set; }

            [JsonProperty(PropertyName = "Discord to Game Chat: Message Color (Hex)")]
            public string GameChatTextColor { get; set; }
        }

        public class GameLogSettings
        {
            [JsonProperty(PropertyName = "Enable Logging: Player Chat")]
            public bool LogChat { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Joins & Quits")]
            public bool LogJoinQuits { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Deaths")]
            public bool LogDeaths { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Vehicle Spawns (Heli/APC/Plane/Ship)")]
            public bool LogVehicleSpawns { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Crate Drops (Hackable/Supply)")]
            public bool LogCrateDrops { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Usergroup Changes")]
            public bool LogUserGroups { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Permission Changes")]
            public bool LogPermissions { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Kicks & Bans")]
            public bool LogKickBans { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Player Name Changes")]
            public bool LogNameChanges { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Server Commands (Gestures/Note Edits)")]
            public bool LogServerCommands { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Server Messages (Give/Item Spawns")]
            public bool LogServerMessages { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Player F7 Reports")]
            public bool LogF7Reports { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Team Changes")]
            public bool LogTeams { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: RCON Connections")]
            public bool LogRCON { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Spectates")]
            public bool LogSpectates { get; set; }

            [JsonProperty(PropertyName = "Enable Custom Logging")]
            public bool EnableCustomLogging { get; set; }
        }

        public class PluginLogSettings
        {
            [JsonProperty(PropertyName = "Enable Logging: AdminHammer")]
            public bool LogPluginAdminHammer { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Admin Radar")]
            public bool LogPluginAdminRadar { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Better Chat Mute")]
            public bool LogPluginBetterChatMute { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Clans")]
            public bool LogPluginClans { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Discord Auth")]
            public bool LogPluginDiscordAuth { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Private Messages")]
            public bool LogPluginPrivateMessages { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Raidable Bases")]
            public bool LogPluginRaidableBases { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Sign Artist")]
            public bool LogPluginSignArtist { get; set; }

            [JsonProperty(PropertyName = "Enable Logging: Vanish")]
            public bool LogPluginVanish { get; set; }
        }

        public class OutputSettings
        {
            [JsonProperty(PropertyName = "Output Type: Bans (Simple/Embed)")]
            public string OutputTypeBans { get; set; }

            [JsonProperty(PropertyName = "Output Type: Bug Report (Simple/Embed)")]
            public string OutputTypeBugs { get; set; }

            [JsonProperty(PropertyName = "Output Type: Deaths (Simple/Embed/DeathNotes)")]
            public string OutputTypeDeaths { get; set; }

            [JsonProperty(PropertyName = "Output Type: F7 Reports (Simple/Embed)")]
            public string OutputTypeF7Report { get; set; }

            [JsonProperty(PropertyName = "Output Type: Join/Quit (Simple/Embed)")]
            public string OutputTypeJoinQuit { get; set; }

            [JsonProperty(PropertyName = "Output Type: Join Player Info (Admin Channel) (Simple/Embed)")]
            public string OutputTypeJoinAdminChan { get; set; }

            [JsonProperty(PropertyName = "Output Type: Kicks (Simple/Embed)")]
            public string OutputTypeKicks { get; set; }

            [JsonProperty(PropertyName = "Output Type: Note Logging (Simple/Embed)")]
            public string OutputTypeNoteLog { get; set; }

            [JsonProperty(PropertyName = "Output Type: Player Name Change (Simple/Embed)")]
            public string OutputTypeNameChange { get; set; }

            [JsonProperty(PropertyName = "Output Type: /Report (Simple/Embed)")]
            public string OutputTypeReports { get; set; }

            [JsonProperty(PropertyName = "Output Type: Teams (Simple/Embed)")]
            public string OutputTypeTeams { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Admin Hammer (Simple/Embed)")]
            public string OutputTypeAdminHammer { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Admin Radar (Simple/Embed)")]
            public string OutputTypeAdminRadar { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Better Chat Mute (Simple/Embed)")]
            public string OutputTypeBetterChatMute { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Clans (Simple/Embed)")]
            public string OutputTypeClans { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Private Messages (Simple/Embed)")]
            public string OutputTypePMs { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Raidable Bases (Simple/Embed)")]
            public string OutputTypeRaidableBases { get; set; }

            [JsonProperty(PropertyName = "Output Type (Plugin): Vanish (Simple/Embed)")]
            public string OutputTypeVanish { get; set; }
        }

        public class ExcludedSettings
        {
            [JsonProperty(PropertyName = "Do Not Log Listed Groups")]
            public List<string> LogExcludeGroups { get; set; }

            [JsonProperty(PropertyName = "Do Not Log Listed Permissions")]
            public List<string> LogExcludePerms { get; set; }
        }

        public class FilterSettings
        {
            [JsonProperty(PropertyName = "Chat Filter: Replacement Word")]
            public string FilteredWord { get; set; }

            [JsonProperty(PropertyName = "Chat Filter: Words to Filter")]
            public List<string> FilterWords { get; set; }
        }

        enum CacheType
        {
            OnPlayerChat = 0,
            OnPlayerConnected = 1,
            OnPlayerDisconnected = 2,
            OnPlayerJoin = 3
        }

        Dictionary<CacheType, Dictionary<BasePlayer, Dictionary<string, string>>> cache = new Dictionary<CacheType, Dictionary<BasePlayer, Dictionary<string, string>>>();
        private string rbdiff;

        Dictionary<string, string> GetPlayerCache(BasePlayer player, string message, CacheType type)
        {
            switch (type)
            {
                case CacheType.OnPlayerChat:
                    {
                        Dictionary<string, string> dict;
                        if (!cache[CacheType.OnPlayerChat].TryGetValue(player, out dict))
                        {
                            cache[CacheType.OnPlayerChat].Add(player, dict = new Dictionary<string, string>
                            {
                                ["playername"] = player.displayName,
                                ["message"] = message,
                                ["playersteamid"] = player.UserIDString
                            });
                        }

                        dict["playername"] = player.displayName;
                        dict["message"] = message;
                        dict["playersteamid"] = player.UserIDString;
                        return dict;
                    }
                case CacheType.OnPlayerConnected:
                    {
                        Dictionary<string, string> dict;
                        if (!cache[CacheType.OnPlayerConnected].TryGetValue(player, out dict))
                        {
                            cache[CacheType.OnPlayerConnected].Add(player, dict = new Dictionary<string, string>
                            {
                                ["playername"] = player.displayName,
                                ["playerip"] = message.Substring(0, message.IndexOf(":")),
                                ["playersteamid"] = player.UserIDString
                            });
                        }

                        dict["playername"] = player.displayName;
                        dict["playerip"] = message.Substring(0, message.IndexOf(":"));
                        dict["playersteamid"] = player.UserIDString;
                        return dict;
                    }
                case CacheType.OnPlayerJoin:
                    {
                        Dictionary<string, string> dict;
                        if (!cache[CacheType.OnPlayerJoin].TryGetValue(player, out dict))
                        {
                            cache[CacheType.OnPlayerDisconnected].Add(player, dict = new Dictionary<string, string>
                            {
                                ["playername"] = player.displayName
                            });
                        }

                        dict["playername"] = player.displayName;
                        return dict;
                    }
                case CacheType.OnPlayerDisconnected:
                default:
                    {
                        Dictionary<string, string> dict;
                        if (!cache[CacheType.OnPlayerDisconnected].TryGetValue(player, out dict))
                        {
                            cache[CacheType.OnPlayerDisconnected].Add(player, dict = new Dictionary<string, string>
                            {
                                ["playername"] = player.displayName,
                                ["reason"] = message,
                            });
                        }

                        dict["playername"] = player.displayName;
                        dict["reason"] = message;
                        return dict;
                    }
            }
        }

        //CONFIG FILE
        private Settings GetDefaultSettings()
        {
            return new Settings
            {
                General = new GeneralSettings
                {
                    Apikey = "BotToken",
                    AutoReloadPlugin = false,
                    AutoReloadTime = 901,
                    EnableBotStatus = false,
                    ReportCommand = "report",
                    ExtensionDebugging = DiscordLogLevel.Info
                },
                DiscordSide = new DiscordSideSettings
                {
                    Commandprefix = "!",
                    GameChatIconSteamID = 76561199066612103,
                    GameChatTag = "[RUSTCORD]",
                    GameChatTagColor = "#7289DA",
                    GameChatNameColor = "#55aaff",
                    GameChatTextColor = "#ffffff",
                },
                GameLog = new GameLogSettings
                {
                    LogChat = true,
                    LogJoinQuits = true,
                    LogDeaths = false,
                    LogVehicleSpawns = false,
                    LogCrateDrops = false,
                    LogUserGroups = false,
                    LogPermissions = false,
                    LogKickBans = true,
                    LogNameChanges = false,
                    LogServerCommands = false,
                    LogServerMessages = false,
                    LogF7Reports = false,
                    LogTeams = false,
                    LogRCON = false,
                    LogSpectates = false,
                    EnableCustomLogging = false
                },
                PluginLog = new PluginLogSettings
                {
                    LogPluginAdminHammer = false,
                    LogPluginAdminRadar = false,
                    LogPluginBetterChatMute = false,
                    LogPluginClans = false,
                    LogPluginDiscordAuth = false,
                    LogPluginPrivateMessages = false,
                    LogPluginRaidableBases = false,
                    LogPluginSignArtist = false,
                    LogPluginVanish = false
                },
                OutputFormat = new OutputSettings
                {
                    OutputTypeBans = "Simple",
                    OutputTypeBugs = "Simple",
                    OutputTypeDeaths = "Simple",
                    OutputTypeF7Report = "Simple",
                    OutputTypeJoinQuit = "Simple",
                    OutputTypeJoinAdminChan = "Simple",
                    OutputTypeKicks = "Simple",
                    OutputTypeNameChange = "Simple",
                    OutputTypeNoteLog = "Simple",
                    OutputTypeReports = "Simple",
                    OutputTypeTeams = "Simple",
                    OutputTypeAdminHammer = "Simple",
                    OutputTypeAdminRadar = "Simple",
                    OutputTypeBetterChatMute = "Simple",
                    OutputTypeClans = "Simple",
                    OutputTypePMs = "Simple",
                    OutputTypeRaidableBases = "Simple",
                    OutputTypeVanish = "Simple"
                },
                
                Channels = new List<Settings.Channel>
                    {
                        new Settings.Channel
                            {
                                perms = new List<string>
                                {
                                    "cmd_allow",
                                    "cmd_players",
                                    "cmd_kick",
                                    "cmd_com",
                                    "cmd_mute",
                                    "cmd_unmute",
                                    "msg_join",
                                    "msg_joinlog",
                                    "msg_quit",
                                    "death_pvp",
                                    "msg_chat",
                                    "msg_teamchat",
                                    "game_report",
                                    "game_bug",
                                    "msg_serverinit"
                                },
                                CustomFilter = new List<string>
                                {
                                    "keyword1",
                                    "keyword2"
                                }
                        },
                        new Settings.Channel
                        {
                            perms = new List<string>
                            {
                                "msg_joinlog",
                                "game_report",
                                "game_bug"
                            },
                            CustomFilter = new List<string>
                            {
                                "keyword1",
                                "keyword2"
                            }
                        }
                    },
                Commandroles = new Dictionary<string, List<string>>
                {
                    {
                        "players", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "mute", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "unmute", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "kick", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "ban", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "timeban", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "unban", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    },
                    {
                        "com", new List<string>()
                        {
                            "DiscordRoleName",
                            "DiscordRoleName2"
                        }
                    }
                },
                Filters = new FilterSettings
                {
                    FilterWords = new List<string>
                    {
                        "badword1",
                        "badword2"
                    },
                    FilteredWord = "<censored>",
                },
                Excluded = new ExcludedSettings
                {
                    LogExcludeGroups = new List<string>
                    {
                        "default"
                    },
                    LogExcludePerms = new List<string>
                    {
                        "example.permission1",
                        "example.permission2"
                    }
                },
                
            };
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Attempting to create default config...");
            Config.Clear();
            Config.WriteObject(GetDefaultSettings(), true);
            Config.Save();
        }
        //END CONFIG FILE


        private void OnServerInitialized()
        {
            var reloadtime = _settings.General.AutoReloadTime;

            permission.RegisterPermission("rustcord.hidejoinquit", this);
            permission.RegisterPermission("rustcord.hidechat", this);

            if (_settings.General.AutoReloadPlugin && _settings.General.AutoReloadTime > 59)
            {
                timer.Every(reloadtime, () => Reload());
            }

            if (_client != null)
            {
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_serverinit"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnInitMsg"));
                        });
                    }
                }
            }
        }
        void OnServerShutdown()
        {

            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("msg_serverinit"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnServerShutdown"));
                    });
                }
            }
        }

        void SubscribeHooks()
        {
            if (_settings.GameLog.LogChat) Subscribe(nameof(OnPlayerChat));
            if (_settings.GameLog.LogJoinQuits)
            {
                Subscribe(nameof(OnPlayerConnected));
                Subscribe(nameof(OnPlayerDisconnected));
            }



            if (_settings.GameLog.LogDeaths)
            {
                if (_settings.OutputFormat.OutputTypeDeaths == "DeathNotes") Subscribe(nameof(OnDeathNotice));
                if ((_settings.OutputFormat.OutputTypeDeaths == "Simple") || (_settings.OutputFormat.OutputTypeDeaths == "Embed")) Subscribe(nameof(OnPlayerDeath));
            }


            if (_settings.GameLog.LogVehicleSpawns) Subscribe(nameof(OnEntitySpawned));
            if (_settings.GameLog.LogCrateDrops)
            {
                Subscribe(nameof(OnCrateDropped));
                Subscribe(nameof(OnSupplyDropLanded));
            }
            if (_settings.GameLog.LogUserGroups)
            {
                Subscribe(nameof(OnGroupCreated));
                Subscribe(nameof(OnGroupDeleted));
                Subscribe(nameof(OnUserGroupAdded));
                Subscribe(nameof(OnUserGroupRemoved));
            }
            if (_settings.GameLog.LogPermissions)
            {
                Subscribe(nameof(OnUserPermissionGranted));
                Subscribe(nameof(OnGroupPermissionGranted));
                Subscribe(nameof(OnUserPermissionRevoked));
                Subscribe(nameof(OnGroupPermissionRevoked));
            }
            if (_settings.GameLog.LogKickBans)
            {
                Subscribe(nameof(OnUserKicked));
                Subscribe(nameof(OnUserBanned));
                Subscribe(nameof(OnUserUnbanned));
            }
            if (_settings.GameLog.LogNameChanges) Subscribe(nameof(OnUserNameUpdated));
            if (_settings.GameLog.LogServerMessages) Subscribe(nameof(OnServerMessage));
            if (_settings.GameLog.LogServerCommands) Subscribe(nameof(OnServerCommand));
            if (_settings.GameLog.LogF7Reports) Subscribe(nameof(OnPlayerReported));
            if (_settings.GameLog.LogTeams)
            {
                Subscribe(nameof(OnTeamCreated));
                Subscribe(nameof(OnTeamAcceptInvite));
                Subscribe(nameof(OnTeamLeave));
                Subscribe(nameof(OnTeamKick));
                Subscribe(nameof(OnTeamDisbanded));
            }
            if (_settings.GameLog.LogRCON)
            {
                Subscribe(nameof(OnRconConnection));
            }
            if (_settings.GameLog.LogSpectates)
            {
                Subscribe(nameof(OnPlayerSpectate));
                Subscribe(nameof(OnPlayerSpectateEnd));
            }
            if (_settings.PluginLog.LogPluginAdminHammer)
            {
                Subscribe(nameof(OnAdminHammerEnabled));
                Subscribe(nameof(OnAdminHammerDisabled));
            }
            if (_settings.PluginLog.LogPluginAdminRadar)
            {
                Subscribe(nameof(OnRadarActivated));
                Subscribe(nameof(OnRadarDeactivated));
            }
            if (_settings.PluginLog.LogPluginBetterChatMute)
            {
                Subscribe(nameof(OnBetterChatMuted));
                Subscribe(nameof(OnBetterChatTimeMuted));
                Subscribe(nameof(OnBetterChatUnmuted));
                Subscribe(nameof(OnBetterChatMuteExpired));
            }
            if (_settings.PluginLog.LogPluginClans)
            {
                Subscribe(nameof(OnClanCreate));
                Subscribe(nameof(OnClanDisbanded));
                Subscribe(nameof(OnClanChat));
            }
            if (_settings.PluginLog.LogPluginPrivateMessages) Subscribe(nameof(OnPMProcessed));
            if (_settings.PluginLog.LogPluginRaidableBases)
            {
                Subscribe(nameof(OnRaidableBaseStarted));
                Subscribe(nameof(OnRaidableBaseEnded));
            }
            if (_settings.PluginLog.LogPluginSignArtist) Subscribe(nameof(OnImagePost));
            if (_settings.PluginLog.LogPluginDiscordAuth)
            {
                Subscribe(nameof(OnAuthenticate));
                Subscribe(nameof(OnDeauthenticate));
            }
            if (_settings.PluginLog.LogPluginVanish)
            {
                Subscribe(nameof(OnVanishDisappear));
                Subscribe(nameof(OnVanishReappear));
            }
        }
        private void Init()
        {
            cache[CacheType.OnPlayerChat] = new Dictionary<BasePlayer, Dictionary<string, string>>();
            cache[CacheType.OnPlayerConnected] = new Dictionary<BasePlayer, Dictionary<string, string>>();
            cache[CacheType.OnPlayerDisconnected] = new Dictionary<BasePlayer, Dictionary<string, string>>();
            cache[CacheType.OnPlayerJoin] = new Dictionary<BasePlayer, Dictionary<string, string>>();
            UnsubscribeHooks();
        }

        void UnsubscribeHooks()
        {
            Unsubscribe(nameof(OnPlayerChat));
            Unsubscribe(nameof(OnPlayerConnected));
            Unsubscribe(nameof(OnPlayerDisconnected));
            Unsubscribe(nameof(OnDeathNotice));
            Unsubscribe(nameof(OnPlayerDeath));
            Unsubscribe(nameof(OnEntitySpawned));
            Unsubscribe(nameof(OnCrateDropped));
            Unsubscribe(nameof(OnSupplyDropLanded));
            Unsubscribe(nameof(OnGroupCreated));
            Unsubscribe(nameof(OnGroupDeleted));
            Unsubscribe(nameof(OnUserGroupAdded));
            Unsubscribe(nameof(OnUserGroupRemoved));
            Unsubscribe(nameof(OnUserPermissionGranted));
            Unsubscribe(nameof(OnGroupPermissionGranted));
            Unsubscribe(nameof(OnUserPermissionRevoked));
            Unsubscribe(nameof(OnGroupPermissionRevoked));
            Unsubscribe(nameof(OnUserKicked));
            Unsubscribe(nameof(OnUserBanned));
            Unsubscribe(nameof(OnUserUnbanned));
            Unsubscribe(nameof(OnUserNameUpdated));
            Unsubscribe(nameof(OnServerMessage));
            Unsubscribe(nameof(OnServerCommand));
            Unsubscribe(nameof(OnPlayerReported));
            Unsubscribe(nameof(OnTeamCreated));
            Unsubscribe(nameof(OnTeamAcceptInvite));
            Unsubscribe(nameof(OnTeamLeave));
            Unsubscribe(nameof(OnTeamKick));
            Unsubscribe(nameof(OnTeamDisbanded));
            Unsubscribe(nameof(OnRconConnection));
            Unsubscribe(nameof(OnPlayerSpectate));
            Unsubscribe(nameof(OnPlayerSpectateEnd));
            Unsubscribe(nameof(OnAdminHammerEnabled));
            Unsubscribe(nameof(OnAdminHammerDisabled));
            Unsubscribe(nameof(OnRadarActivated));
            Unsubscribe(nameof(OnRadarDeactivated));
            Unsubscribe(nameof(OnBetterChatTimeMuted));
            Unsubscribe(nameof(OnBetterChatMuted));
            Unsubscribe(nameof(OnBetterChatTimeMuted));
            Unsubscribe(nameof(OnBetterChatUnmuted));
            Unsubscribe(nameof(OnBetterChatMuteExpired));
            Unsubscribe(nameof(OnClanCreate));
            Unsubscribe(nameof(OnClanDisbanded));
            Unsubscribe(nameof(OnClanChat));
            Unsubscribe(nameof(OnPMProcessed));
            Unsubscribe(nameof(OnImagePost));
            Unsubscribe(nameof(OnAuthenticate));
            Unsubscribe(nameof(OnDeauthenticate));
            Unsubscribe(nameof(OnRaidableBaseStarted));
            Unsubscribe(nameof(OnRaidableBaseEnded));
        }

        private void Loaded()
        {
            _settings = Config.ReadObject<Settings>();

            // Make sure objects are not taken off the config, otherwise some parts of code will release NRE.

            if (_settings.OutputFormat.OutputTypeBans == null)
                _settings.OutputFormat.OutputTypeBans = "Simple";
            if (_settings.OutputFormat.OutputTypeBugs == null)
                _settings.OutputFormat.OutputTypeBugs = "Simple";
            if (_settings.OutputFormat.OutputTypeDeaths == null)
                _settings.OutputFormat.OutputTypeDeaths = "Simple";
            if (_settings.OutputFormat.OutputTypeF7Report == null)
                _settings.OutputFormat.OutputTypeF7Report = "Simple";
            if (_settings.OutputFormat.OutputTypeJoinQuit == null)
                _settings.OutputFormat.OutputTypeJoinQuit = "Simple";
            if (_settings.OutputFormat.OutputTypeJoinAdminChan == null)
                _settings.OutputFormat.OutputTypeJoinAdminChan = "Simple";
            if (_settings.OutputFormat.OutputTypeKicks == null)
                _settings.OutputFormat.OutputTypeKicks = "Simple";
            if (_settings.OutputFormat.OutputTypeNameChange == null)
                _settings.OutputFormat.OutputTypeNameChange = "Simple";
            if (_settings.OutputFormat.OutputTypeNoteLog == null)
                _settings.OutputFormat.OutputTypeNoteLog = "Simple";
            if (_settings.OutputFormat.OutputTypeReports == null)
                _settings.OutputFormat.OutputTypeReports = "Simple";
            if (_settings.OutputFormat.OutputTypeTeams == null)
                _settings.OutputFormat.OutputTypeTeams = "Simple";
            if (_settings.OutputFormat.OutputTypeAdminHammer == null)
                _settings.OutputFormat.OutputTypeAdminHammer = "Simple";
            if (_settings.OutputFormat.OutputTypeAdminRadar == null)
                _settings.OutputFormat.OutputTypeAdminRadar = "Simple";
            if (_settings.OutputFormat.OutputTypeBetterChatMute == null)
                _settings.OutputFormat.OutputTypeBetterChatMute = "Simple";
            if (_settings.OutputFormat.OutputTypeClans == null)
                _settings.OutputFormat.OutputTypeClans = "Simple";
            if (_settings.OutputFormat.OutputTypePMs == null)
                _settings.OutputFormat.OutputTypePMs = "Simple";
            if (_settings.OutputFormat.OutputTypeRaidableBases == null)
                _settings.OutputFormat.OutputTypeRaidableBases = "Simple";
            if (_settings.OutputFormat.OutputTypeVanish == null)
                _settings.OutputFormat.OutputTypeVanish = "Simple";
            foreach (var channel in _settings.Channels)
            {
                if (channel.CustomFilter == null)
                {
                    channel.CustomFilter = new List<string>();
                }
            }
            if (_settings.General.ReportCommand == null)
                _settings.General.ReportCommand = "report";
            if (string.IsNullOrEmpty(_settings.DiscordSide.GameChatTag))
                _settings.DiscordSide.GameChatTag = "[Rustcord]";
            if (string.IsNullOrEmpty(_settings.DiscordSide.GameChatTagColor))
                _settings.DiscordSide.GameChatTagColor = "#7289DA";
            if (string.IsNullOrEmpty(_settings.DiscordSide.GameChatNameColor))
                _settings.DiscordSide.GameChatNameColor = "#55aaff";
            if (string.IsNullOrEmpty(_settings.DiscordSide.GameChatTextColor))
                _settings.DiscordSide.GameChatTextColor = "#ffffff";
            if (_settings.DiscordSide.GameChatIconSteamID.Equals(0uL))
                _settings.DiscordSide.GameChatIconSteamID = 76561199066612103;
            if (_settings.Channels == null)
                _settings.Channels = new List<Settings.Channel>();
            if (_settings.Commandroles == null)
                _settings.Commandroles = new Dictionary<string, List<string>>();
            _settings.Filters = new FilterSettings
            {
                FilteredWord = _settings.Filters?.FilteredWord ?? "<censored>",
                FilterWords = _settings.Filters?.FilterWords ?? new List<string>()
            };
            if (_settings.Excluded.LogExcludeGroups == null)
                _settings.Excluded.LogExcludeGroups = new List<string>();
            if (_settings.Excluded.LogExcludePerms == null)
                _settings.Excluded.LogExcludePerms = new List<string>();

            Config.WriteObject(_settings, true);
            // ------------------------------------------------------------------------
        }

        private void OnDiscordClientCreated()
        {
            if (string.IsNullOrEmpty(_settings.General.Apikey) || _settings.General.Apikey == null || _settings.General.Apikey == "BotToken")
            {
                PrintError("API key is empty or invalid!");
                return;
            }

            bool flag = true;
            try
            {
                DiscordSettings settings = new DiscordSettings
                {
                    ApiToken = _settings.General.Apikey,
                    Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages,
                    LogLevel = _settings.General.ExtensionDebugging
                };
                _client.Connect(settings);
            }
            catch (Exception e)
            {
                flag = false;
                PrintError($"Rustcord failed to create client! Exception message: {e}");
            }

            if (flag)
            {
                cmd.AddChatCommand(_settings.General.ReportCommand, this, "cmdReport");
                cmd.AddChatCommand("bug", this, "cmdBug");
                SubscribeHooks();
            }

            if (_settings.GameLog.EnableCustomLogging)
                UnityEngine.Application.logMessageReceived += ConsoleLog;
        }

        private void Reload()
        {
            rust.RunServerCommand("oxide.reload Rustcord");
        }

        void OnDiscordGatewayReady(GatewayReadyEvent rdy)
        {
            _botId = rdy.User.Id;
            SubscribeHooks();
            _channelCount = _settings?.Channels.Count;
            if (_settings.General.EnableBotStatus)
            {
                NextFrame(() =>
                {
                    if (StatusTimer != null && !StatusTimer.Destroyed)
                    {
                        StatusTimer.Destroy();
                    }
                    StatusTimer = timer.Every(6f, () =>
                    {
                        var text = new Dictionary<string, string>
                        {
                            ["players"] = Convert.ToString(BasePlayer.activePlayerList.Count),
                            ["maxplayers"] = Convert.ToString(ConVar.Server.maxplayers),
                            ["sleepers"] = Convert.ToString(BasePlayer.sleepingPlayerList.Count)
                        };
                        var msg = Translate("Discord_Status", text);
                        DiscordPresence.Activities[0].Name = string.IsNullOrEmpty(msg) ? "Rustcord initializing...." : msg;
                        _client.Bot.UpdateStatus(DiscordPresence);
                    });
                });
            }
        }

        void OnDiscordGuildCreated(DiscordGuild newguild)
        {

            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("msg_plugininit"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, c => {
                        c.CreateMessage(_client, "Rustcord Initialized!");
                    }, newguild.Id);
                }
            }
        }

        private void Unload()
        {
            if (StatusTimer != null && !StatusTimer.Destroyed)
            {
                StatusTimer.Destroy();
            }
            if (_settings.GameLog.EnableCustomLogging)
                UnityEngine.Application.logMessageReceived -= ConsoleLog;
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                { "Discord_PlayersResponse", ":mag_right: Connected Players [{count}/{maxplayers}]: {playerslist}" },
                { "Discord_Status", "{players}/{maxplayers} Online, {sleepers} Sleepers." },
                { "RUST_OnInitMsg", ":vertical_traffic_light: Server is back online! Players may now re-join. :vertical_traffic_light:" },
                { "RUST_OnServerShutdown", ":vertical_traffic_light: Server shutting down. :vertical_traffic_light:" },
                { "RUST_OnPlayerGesture", ":speech_left: {playername}: {gesture}"},
                { "RUST_OnPlayerChat", ":speech_left: {playername}: {message}"},
                { "RUST_OnPlayerTeamChat", ":speech_left: {playername}: {message}"},
                { "RUST_OnPlayerJoin", ":white_check_mark: {playername} has connected!" },
                { "RUST_OnPlayerJoinAdminLog", ":clipboard: {playername} has connected! (IP: {playerip}    SteamID: {playersteamid})" },
                { "RUST_OnPlayerQuit", ":x: {playername} has disconnected! ({reason})" },
                { "RUST_OnPlayerBug", ":beetle: {playername}: {message}"},
                { "RUST_OnPlayerReport", ":warning: {playername}: {message}"},
                { "RUST_OnPlaneSpawn", ":airplane: Cargo Plane has spawned."},
                { "RUST_OnBradleySpawn", ":trolleybus: Bradley APC has spawned."},
                { "RUST_OnShipSpawn", ":ship: Cargo Ship has spawned."},
                { "RUST_OnSupplyDrop", ":airplane: A supply drop has landed."},
                { "RUST_OnHeliSpawn", ":helicopter: Patrol Helicopter has spawned."},
                { "RUST_OnChinookSpawn", ":helicopter: Chinook Helicopter has spawned."},
                { "RUST_OnCrateDropped", ":helicopter: A Chinook has delivered a crate."},
                { "RUST_OnTeamAcceptInvite", ":family_mwgb: {playername} joined {teamleader}'s team. ({teamid})"},
                { "RUST_OnTeamCreated", ":family_mwgb: {playername} created a new team. ({teamid})"},
                { "RUST_OnTeamKicked", ":family_mwgb: {teamleader} kicked {playername} from their team. ({teamid})"},
                { "RUST_OnTeamLeave", ":family_mwgb: {playername} left {teamleader}'s team ({teamid})."},
                { "RUST_OnTeamDisbanded", ":family_mwgb: {teamleader}'s team has been disbanded. ({teamid})"},
                { "RUST_OnGroupCreated", ":desktop: Group {groupname} has been created."},
                { "RUST_OnGroupDeleted", ":desktop: Group {groupname} has been deleted."},
                { "RUST_OnUserGroupAdded", ":desktop: {playername} ({steamid}) has been added to group: {group}."},
                { "RUST_OnUserGroupRemoved", ":desktop: {playername} ({steamid}) has been removed from group: {group}."},
                { "RUST_OnUserPermissionGranted", ":desktop: {playername} ({steamid}) has been granted permission: {permission}."},
                { "RUST_OnGroupPermissionGranted", ":desktop: Group {group} has been granted permission: {permission}."},
                { "RUST_OnUserPermissionRevoked", ":desktop: {playername} ({steamid}) has been revoked permission: {permission}."},
                { "RUST_OnGroupPermissionRevoked", ":desktop: Group {group} has been revoked permission: {permission}."},
                { "RUST_OnPlayerKicked", ":desktop: {playername} has been kicked for: {reason}"},
                { "RUST_OnPlayerBanned", ":desktop: {playername} ({steamid}/{ip}) has been banned for: {reason}"}, //only works with vanilla/native system atm
				{ "RUST_OnPlayerUnBanned", ":desktop: {playername} ({steamid}/{ip}) has been unbanned."}, //only works with vanilla/native system atm
				{ "RUST_OnPlayerNameChange", ":desktop: {oldname} ({steamid}) is now playing as {newname}."},
                { "RUST_OnPlayerReported", ":desktop: {reporter} reported {targetplayer} ({targetsteamid}) to Facepunch for {reason}. Message: {message}"},
                { "RUST_OnPlayerDeath", ":skull_crossbones: {killer} killed {victim}."},
                { "RUST_OnF1ItemSpawn", ":desktop: {name}: {givemessage}."},
                { "RUST_OnNoteUpdate", ":desktop: [NOTES] {playername}: {notemessage}."},
                { "RUST_OnRCONConnected", ":desktop: [RCON] New connection from: {ip}."},
                { "RUST_OnPlayerSpectate", ":desktop: {player} is spectating {target}"},
                { "RUST_CustomLog", ":desktop: [Custom] {logtext}"},
                { "RUST_OnPlayerSpectateEnd", ":desktop: {player} stopped spectating {target}"},
                { "PLUGIN_AdminHammer_Enabled", ":hammer: {player} has enabled Admin Hammer."},
                { "PLUGIN_AdminHammer_Disabled", ":hammer: {player} has disabled Admin Hammer."},
                { "PLUGIN_AdminRadar_Enabled", ":satellite: {player} has enabled Admin Radar."},
                { "PLUGIN_AdminRadar_Disabled", ":satellite: {player} has disabled Admin Radar."},
                { "PLUGIN_BetterChatMute_Mute", "[MUTE] :zipper_mouth: {muter} has permanently muted {target}. Reason: {reason}"},
                { "PLUGIN_BetterChatMute_UnMute", "[MUTE] :loudspeaker: {unmuter} has unmuted {target}."},
                { "PLUGIN_BetterChatMute_TimedMute", "[MUTE] :hourglass_flowing_sand: {muter} has been temporarily muted {target} for {time}. Reason: {reason}"},
                { "PLUGIN_BetterChatMute_MuteExpire", "[MUTE] :hourglass: {target}'s temporary mute has expired."},
                { "PLUGIN_Clans_Chat", ":speech_left: [CLANS] {playername}: {message}"},
                { "PLUGIN_Clans_CreateClan", ":family_mwgb: Clan [{clan}] has been created."},
                { "PLUGIN_Clans_DisbandClan", ":family_mwgb: Clan [{clan}] has been disbanded."},
                { "PLUGIN_DiscordAuth_Auth", ":lock: {gamename} has linked to Discord account {discordname}."},
                { "PLUGIN_DiscordAuth_Deauth", ":unlock: {gamename} has been unlinked from Discord account {discordname}."},
                { "PLUGIN_PrivateMessages_PM", "[PM] {sender}  :incoming_envelope: {target}: {message}"},
                { "PLUGIN_RaidableBases_Started", ":house: {difficulty} Raidable Base has spawned at {position}."},
                { "PLUGIN_RaidableBases_Ended", ":house: {difficulty} Raidable Base at {position} has ended."},
                { "PLUGIN_SignArtist", "{player} posted an image to a sign.\nPosition: ({position})"},
                { "PLUGIN_Vanish_Disappear", ":ghost: {player} has vanished." },
                { "PLUGIN_Vanish_Reappear", ":ghost: {player} has reappeared." }
            }, this);
        }
        private void OnPlayerChat(BasePlayer player, string message, ConVar.Chat.ChatChannel channel)
        {
            if (_client == null) return;
            if (player == null || message == null) return;
            if (permission.UserHasPermission(player.UserIDString, "rustcord.hidechat")) return;
            if (BetterChatMute?.Call<bool>("API_IsMuted", player.IPlayer) ?? false) return;
            if (_settings.Filters.FilterWords != null && _settings.Filters.FilterWords.Count > 0)
            {
                for (int i = _settings.Filters.FilterWords.Count - 1; i >= 0; i--)
                {
                    while (message.Contains(" " + _settings.Filters.FilterWords[i] + " ") || message.Contains(_settings.Filters.FilterWords[i]))
                        message = message.Replace(_settings.Filters.FilterWords[i], _settings.Filters.FilteredWord ?? "");
                }
            }

            var text = GetPlayerCache(player, message, CacheType.OnPlayerChat);

            for (int i = 0; i < _settings.Channels.Count; i++)
            {
                if (_settings.Channels[i].perms.Contains(channel == ConVar.Chat.ChatChannel.Team ? "msg_teamchat" : "msg_chat"))
                {
                    if (!(player.IsValid())) continue;

                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate(channel == ConVar.Chat.ChatChannel.Team ? "RUST_OnPlayerTeamChat" : "RUST_OnPlayerChat", text));

                    });
                }
            }

            text.Clear();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (_client == null) return;
            if (player == null) return;
            if (!player.IsValid()) return;

            HandleAdminJoin(player);
            HandlePlayerJoin(player);
        }

        private void HandleAdminJoin(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeJoinAdminChan == "Simple")
            {
                var text = GetPlayerCache(player, player.net.connection.ipaddress, CacheType.OnPlayerConnected);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_joinlog"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            // Admin
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerJoinAdminLog", text));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeJoinAdminChan == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("PLAYER INFO")
                                                  .AddColor("#00FF00")
                                                  .AddThumbnail("https://i.imgur.com/AfbPIrb.png")
                                                  .AddField("Name", player.displayName, true)
                                                  .AddField("IP", player.net.connection.ipaddress.Split(':')[0], true)
                                                  .AddField("Steam ID", player.UserIDString, true);



                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_joinlog"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        private void HandlePlayerJoin(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, "rustcord.hidejoinquit")) return;

            if (_settings.OutputFormat.OutputTypeJoinQuit == "Simple")
            {
                var text = GetPlayerCache(player, null, CacheType.OnPlayerJoin);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_join"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan => { chan.CreateMessage(_client, Translate("RUST_OnPlayerJoin", text)); });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeJoinQuit == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("PLAYER JOIN")
                                                  .AddColor("#00FF00")
                                                  .AddThumbnail("https://i.imgur.com/hQK7Jjv.png")
                                                  .AddDescription($"{player.displayName} has joined the server.");



                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_join"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_client == null) return;
            if (player == null || string.IsNullOrEmpty(reason)) return;
            if (permission.UserHasPermission(player.UserIDString, "rustcord.hidejoinquit"))
                return;
            if (_settings.OutputFormat.OutputTypeJoinQuit == "Simple")
            {
                var text = GetPlayerCache(player, reason, CacheType.OnPlayerDisconnected);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_quit"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerQuit", text));
                        });
                    }
                }

                cache[CacheType.OnPlayerChat].Remove(player);
                cache[CacheType.OnPlayerConnected].Remove(player);
                cache[CacheType.OnPlayerDisconnected].Remove(player);
                cache[CacheType.OnPlayerJoin].Remove(player);
            }
            if (_settings.OutputFormat.OutputTypeJoinQuit == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("PLAYER QUIT")
                                                  .AddColor("#00FF00")
                                                  .AddThumbnail("https://i.imgur.com/py6bHm0.png")
                                                  .AddDescription($"{player.displayName} has left the server.")
                                                  .AddField("Reason", reason, true);



                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_quit"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (player == null) return;
            if (info?.InitiatorPlayer == null) return;
            if ((player.IsNpc) || (info.InitiatorPlayer.IsNpc)) return;

            if (_settings.OutputFormat.OutputTypeDeaths == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("PLAYER DEATH")
                                              .AddColor("#000000")
                                              .AddThumbnail("https://i.imgur.com/UZZTf08.png")
                                              .AddField("Victim", player.displayName, true)
                                              .AddBlankField(true)
                                              .AddField("Killer", info.InitiatorPlayer.displayName, true);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_deaths"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeDeaths == "Simple")
            {
                var dict = new Dictionary<string, string>
                {
                    { "victim", player.displayName },
                    { "killer", info.InitiatorPlayer.displayName }
                };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_deaths"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerDeath", dict));
                        });

                    }
                }
            }
        }

        private void OnDeathNotice(Dictionary<string, object> data, string message)
        {
            if (_client == null) return;
            if (data["VictimEntityType"] == null || data["KillerEntityType"] == null) return;
            int victimType = (int)data["VictimEntityType"];
            int killerType = (int)data["KillerEntityType"];

            var _DeathNotes = plugins.Find("DeathNotes");

            if (_DeathNotes != null)
                if ((victimType == 5 && (killerType == 5 || killerType == 6 || killerType == 7 || killerType == 8 || killerType == 9 || killerType == 10 || killerType == 11 || killerType == 12 || killerType == 14 || killerType == 15)))
                {
                    message = (string)_DeathNotes.Call("StripRichText", message);
                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("death_pvp"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, ":skull_crossbones: " + message);
                            });
                        }
                    }

                }
                else if ((victimType == 2 && killerType == 5) || (victimType == 5 && killerType == 2))
                {
                    message = (string)_DeathNotes.Call("StripRichText", message);

                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("death_animal"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, ":skull_crossbones: " + message);
                            });
                        }
                    }

                }
                else if ((victimType == 5 && (killerType == 0 || killerType == 1)) || ((victimType == 0 || victimType == 1) && (killerType == 5)))
                {
                    message = (string)_DeathNotes.Call("StripRichText", message);

                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("death_vehicle"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, ":skull_crossbones: " + message);
                            });
                        }
                    }
                }
                else if ((victimType == 5 && (killerType == 3 || killerType == 4)) || ((victimType == 3 || victimType == 4) && (killerType == 5)))
                {
                    message = (string)_DeathNotes.Call("StripRichText", message);

                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("death_npc"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, ":skull_crossbones: " + message);
                            });
                        }
                    }

                }
        }

        private void OnDiscordGuildMessageCreated(DiscordMessage message)
        {
            if ((message.Content?.Length ?? 0) == 0) return;
            Settings.Channel channelidx = FindChannelById(message.ChannelId);
            if (channelidx == null)
                return;

            if (message.Author.Id == _botId) return;
            if (message.Content[0] == _settings.DiscordSide.Commandprefix[0])
            {
                if (!channelidx.perms.Contains("cmd_allow"))
                    return;
                string cmd;
                string msg;
                try
                {
                    cmd = message.Content.Split(' ')[0].ToLower();
                    if (string.IsNullOrEmpty(cmd.Trim()))
                        cmd = message.Content.Trim().ToLower();
                }
                catch
                {
                    cmd = message.Content.Trim().ToLower();
                }

                cmd = cmd.Remove(0, 1);

                msg = message.Content.Remove(0, 1 + cmd.Length).Trim();
                cmd = cmd.Trim();
                cmd = cmd.ToLower();

                if (!channelidx.perms.Contains("cmd_" + cmd))
                    return;
                if (!_settings.Commandroles.ContainsKey(cmd))
                {
                    DiscordToGameCmd(cmd, msg, message.Author, message.ChannelId);
                    return;
                }
                var roles = _settings.Commandroles[cmd];
                if (roles.Count == 0)
                {
                    DiscordToGameCmd(cmd, msg, message.Author, message.ChannelId);
                    return;
                }

                foreach (var roleid in message.Member.Roles)
                {
                    var rolename = GetRoleNameById(roleid);
                    if (roles.Contains(rolename))
                    {
                        DiscordToGameCmd(cmd, msg, message.Author, message.ChannelId);
                        break;
                    }
                }
            }
            else
            {
                var chattag = _settings.DiscordSide.GameChatTag;
                var chattagcolor = _settings.DiscordSide.GameChatTagColor;
                var chatnamecolor = _settings.DiscordSide.GameChatNameColor;
                var chattextcolor = _settings.DiscordSide.GameChatTextColor;
                if (!channelidx.perms.Contains("msg_chat")) return;
                string nickname = message.Member?.Nickname ?? "";
                if (nickname.Length == 0)
                    nickname = message.Author.Username;
                //PrintToChat("<color=" + chattagcolor + ">" + chattag + "</color> " + "<color=" + chatnamecolor + ">" + nickname + ":</color> " + "<color=" + chattextcolor + ">" + message.content + "</color>");
                string text = $"<color={chattagcolor}>{chattag}</color> <color={chatnamecolor}>{nickname}:</color> <color={chattextcolor}>{message.Content}</color>";
                foreach (var player in BasePlayer.activePlayerList) Player.Message(player, text, _settings.DiscordSide.GameChatIconSteamID);
                Puts("[DISCORD] " + nickname + ": " + message.Content);
            }
        }

        private void DiscordToGameCmd(string command, string param, DiscordUser author, Snowflake channelid)
        {
            switch (command)
            {
                case "players":
                    {
                        string listStr = string.Empty;
                        var pList = BasePlayer.activePlayerList;
                        int i = 0;
                        foreach (var player in pList)
                        {
                            listStr += player.displayName + " " + "[" + (i++ + 1) + "]";
                            if (i != pList.Count)
                                listStr += ", ";

                            if (i % 25 == 0 || i == pList.Count)
                            {
                                var text = new Dictionary<string, string>
                                {
                                    ["count"] = Convert.ToString(BasePlayer.activePlayerList.Count),
                                    ["maxplayers"] = Convert.ToString(ConVar.Server.maxplayers),
                                    ["playerslist"] = listStr
                                };
                                GetChannel(_client, channelid, chan =>
                                {
                                    // Connected Players [{count}/{maxplayers}]: {playerslist}
                                    chan.CreateMessage(_client, Translate("Discord_PlayersResponse", text));
                                });
                                text.Clear();
                                listStr = string.Empty;
                            }
                        }

                        break;
                    }
                case "kick":
                    {
                        if (String.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !kick <steam id> <reason>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length < 2)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !kick <steam id> <reason>");
                            });
                            return;
                        }
                        BasePlayer plr = BasePlayer.Find(_param[0]);
                        if (plr == null)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Error: player not found");
                            });
                            return;
                        }
                        plr.Kick(param.Remove(0, _param[0].Length + 1));
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Success: Kick command executed!");
                        });
                        break;
                    }
                case "timeban":
                    {
                        if (string.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !timeban <steamid> <name> <duration> <reason>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length < 3)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !timeban <steamid> <name> <duration> <reason>");
                            });
                            return;
                        }
                        var plr = covalence.Players.FindPlayer(_param[0]);
                        if (plr == null)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Error: player not found");
                            });
                            return;
                        }
                        string[] args = new string[4];
                        args[0] = _param[0]; // id
                        args[1] = _param[1]; // name
                        args[2] = "\""; // reason
                        for (int i = 3; i < _param.Length; i++)
                        {
                            args[2] += _param[i];
                            if (i != _param.Length - 1)
                                args[2] += " ";
                        }
                        args[2] += "\"";
                        args[3] = _param[2];
                        this.Server.Command("banid", args);
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Success: Ban command executed!");
                        });
                        break;
                    }
                case "ban":
                    {
                        if (string.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !ban <name/id> <reason>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length < 2)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !ban <name/id> <reason>");
                            });
                            return;
                        }
                        var plr = covalence.Players.FindPlayer(_param[0]);
                        if (plr == null)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Error: player not found");
                            });
                            return;
                        }
                        plr.Ban(param.Remove(0, _param[0].Length + 1));
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Success: Ban command executed!");
                        });
                        break;
                    }
                case "unban":
                    {
                        if (string.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !unban <name/id>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        var plr = covalence.Players.FindPlayer(_param[0]);
                        if (plr == null)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Error: player not found");
                            });
                            return;
                        }
                        plr.Unban();
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Success: Unban command executed!");
                        });
                        break;
                    }
                case "com":
                    {
                        if (String.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !com <command>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length > 1)
                        {
                            string[] args = new string[_param.Length - 1];
                            Array.Copy(_param, 1, args, 0, args.Length);
                            this.Server.Command(_param[0], args);
                        }
                        else
                        {
                            this.Server.Command(param);
                        }
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Success: Console command executed!");
                        });
                        break;
                    }
                case "mute":
                    {
                        if (BetterChatMute == null)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "This command requires the Better Chat Mute plugin.");
                                return;
                            });
                        }
                        if (string.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !mute <playername/steamid> <time (optional)> <reason (optional)>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length >= 1)
                        {
                            this.Server.Command($"mute {string.Join(" ", _param)}");
                            return;
                        }
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Success: Mute command executed!");
                        });
                        break;
                    }
                case "unmute":
                    {
                        if (BetterChatMute == null)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "This command requires the Better Chat Mute plugin.");
                                return;
                            });
                        }
                        if (String.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !unmute <playername/steamid>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length > 1)
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !unmute <playername/steamid>");
                            });
                            return;
                        }
                        if (_param.Length == 1)
                        {
                            this.Server.Command($"unmute {string.Join(" ", _param)}");
                            return;
                        }
                        break;
                    }
            }

        }

        //GAME COMMANDS

        // /report [message]
        void cmdReport(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, "Syntax: /report [message]");
                return;
            }

            if (_settings.OutputFormat.OutputTypeReports == "Embed")
            {
                string message = string.Join(" ", args);
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("PLAYER REPORT")
                                                  .AddColor("#FF0000")
                                                  .AddThumbnail("https://i.imgur.com/qg7v0Tv.png")
                                                  .AddDescription($"{player.displayName} has submitted a report.")
                                                  .AddField("Message", message, true);



                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("game_report"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeReports == "Simple")
            {
                string message = string.Join(" ", args);

                var dict = new Dictionary<string, string>
                    {
                        { "playername", player.displayName },
                        { "message", message }
                    };

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("game_report"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerReport", dict));
                        });
                    }
                }
            }

            SendReply(player, "Your report has been submitted to Discord.");

        }

        void cmdBug(BasePlayer player, string command, string[] args)
        {
            if (args.Length < 1)
            {
                SendReply(player, "Syntax: /bug [message]");
                return;
            }
            if (_settings.OutputFormat.OutputTypeBugs == "Embed")
            {
                string message = string.Join(" ", args);
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("BUG REPORT")
                                                  .AddThumbnail("https://i.imgur.com/GLjfCFd.png")
                                                  .AddColor("#FF0000")
                                                  .AddDescription($"{player.displayName} has reported a bug.")
                                                  .AddField("Bug", message, true);



                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("game_bug"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBugs == "Simple")
            {
                string message = string.Join(" ", args);
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("game_bug"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerBug", new Dictionary<string, string>
                        {
                            { "playername", player.displayName },
                            { "message", message }
                        }));
                        });
                    }
                }
            }

            SendReply(player, "Your bug report has been submitted to Discord.");

        }
        //NPC VEHICLE SPAWN LOGGING

        private void OnEntitySpawned(BaseEntity Entity)
        {
            if (Entity == null) return;
            if (Entity is BaseHelicopter)
            {
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_helispawn"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnHeliSpawn"));
                        });
                    }
                }
            }
            if (Entity is CargoPlane)
            {
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_planespawn"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlaneSpawn"));
                        });
                    }
                }
            }
            if (Entity is CargoShip)
            {
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_shipspawn"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnShipSpawn"));
                        });
                    }
                }

            }
            if (Entity is CH47Helicopter)
            {
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_chinookspawn"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnChinookSpawn"));
                        });
                    }
                }
            }
            if (Entity is BradleyAPC)
            {
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("msg_bradleyspawn"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnBradleySpawn"));
                        });
                    }
                }
            }

        }

        private string Translate(string msg, Dictionary<string, string> parameters = null)
        {
            if (string.IsNullOrEmpty(msg))
                return string.Empty;

            msg = lang.GetMessage(msg, this);

            if (parameters != null)
            {
                foreach (var lekey in parameters)
                {
                    if (msg.Contains("{" + lekey.Key + "}"))
                        msg = msg.Replace("{" + lekey.Key + "}", lekey.Value);
                }
            }

            return msg;
        }

        private Settings.Channel FindChannelById(Snowflake id)
        {
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].Channelid == id)
                    return _settings.Channels[i];
            }

            return null;
        }

        private void GetChannel(DiscordClient c, Snowflake chan_id, Action<DiscordChannel> cb, Snowflake guildid = default(Snowflake))
        {
            //Guild g = guildid == null ? c.DiscordServers.FirstOrDefault(x => x.channels.FirstOrDefault(y => y.id == chan_id) != null) : c.GetGuild(guildid);
            DiscordGuild g = null;
            DiscordChannel foundchan = null;
            if (guildid.IsValid())
                g = c.Bot.GetGuild(guildid);
            else
                foreach (var G in c.Bot.Servers.Values)
                {
                    foundchan = G.Channels[chan_id];
                    if (foundchan != null)
                    {
                        g = G;
                        break;
                    }
                }
            if (g == null)
            {
                PrintWarning($"Rustcord failed to fetch channel! (chan_id={chan_id}). Guild is invalid.");
                return;
            }
            if (g.Unavailable ?? false == true)
            {
                PrintWarning($"Rustcord failed to fetch channel! (chan_id={chan_id}). Guild is possibly invalid or not available yet.");
                return;
            }
            //Channel foundchan = g?.channels?.FirstOrDefault(z => z.id == chan_id);
            if (foundchan == null)
            {
                if (guildid.IsValid()) return; // Ignore printing error
                PrintWarning($"Rustcord failed to fetch channel! (chan_id={chan_id}).");
                return;
            }
            if (foundchan.Id != chan_id) return;
            cb?.Invoke(foundchan);
        }

        private string GetRoleNameById(Snowflake id)
        {
            //var role = _client.DiscordServers.FirstOrDefault(x => x.roles.FirstOrDefault(y => y.id == id) != null)?.roles.FirstOrDefault(z => z.id == id);
            //return role?.name ?? "";
            foreach (var r in _client.Bot.Servers.Values)
            {
                var role = r.Roles[id];
                if (role != null)
                {
                    return role.Name;
                }
            }
            return string.Empty;
        }

        private IPlayer FindPlayer(string nameorId)
        {
            foreach (var player in covalence.Players.Connected)
            {
                if (player.Id == nameorId)
                    return player;

                if (player.Name == nameorId)
                    return player;
            }

            return null;
        }

        private DiscordUser FindUserByID(Snowflake id)
        {
            foreach (DiscordGuild guild in _client.Bot.Servers.Values)
            {
                var member = guild.Members[id];
                if (member != null)
                {
                    return member.User;
                }
            }

            return null;
        }

        private BasePlayer FindPlayerByID(string Id)
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.UserIDString == Id)
                    return player;
            }

            return null;
        }

        private IPlayer GetPlayer(string id)
        {
            return covalence.Players.FindPlayerById(id);
        }

        private IPlayer GetPlayer(ulong id)
        {
            return GetPlayer(id.ToString());
        }

        //CONSOLE LOGGING 

        void OnCrateDropped(HackableLockedCrate crate)
        {
            var dict = new Dictionary<string, string> { };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_cratedrop"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnCrateDropped", dict));
                    });
                }
            }
        }

        void OnSupplyDropLanded(SupplyDrop entity)
        {
            var dict = new Dictionary<string, string> { };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_supplydrop"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnSupplyDrop", dict));
                    });
                }
            }
        }

        void OnGroupCreated(string name)
        {
            var dict = new Dictionary<string, string>
            {
                            { "groupname", name }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_groups"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnGroupCreated", dict));
                    });
                }
            }
        }

        void OnGroupDeleted(string name)
        {
            var dict = new Dictionary<string, string>
            {
                { "groupname", name }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_groups"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnGroupDeleted", dict));
                    });
                }
            }
        }



        void OnUserGroupAdded(string id, string groupName)
        {
            if (_settings.Excluded.LogExcludeGroups.Contains(groupName))
            {
                return;
            }
            var player = covalence.Players.FindPlayerById(id);
            if (player == null) return;
            var dict = new Dictionary<string, string>
            {
                { "playername", player.Name },
                { "steamid", id },
                { "group", groupName }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_groups"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnUserGroupAdded", dict));
                    });
                }
            }
        }

        void OnUserGroupRemoved(string id, string groupName)
        {
            if (_settings.Excluded.LogExcludeGroups.Contains(groupName)) return;
            var player = covalence.Players.FindPlayerById(id);
            if (player == null) return;
            var dict = new Dictionary<string, string>
            {
                            { "playername", player.Name },
                            { "steamid", id },
                            { "group", groupName }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_groups"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnUserGroupRemoved", dict));
                    });
                }
            }
        }

        void OnUserPermissionGranted(string id, string permName)
        {
            if (_settings.Excluded.LogExcludePerms.Contains(permName)) return;
            var player = covalence.Players.FindPlayerById(id);
            if (player == null) return;
            var dict = new Dictionary<string, string>
            {
                            { "playername", player.Name },
                            { "steamid", id },
                            { "permission", permName }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_perms"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnUserPermissionGranted", dict));
                    });
                }
            }
        }

        void OnGroupPermissionGranted(string name, string perm)
        {
            if (_settings.Excluded.LogExcludePerms.Contains(perm)) return;
            var dict = new Dictionary<string, string>
            {
                            { "group", name },
                            { "permission", perm }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_perms"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnGroupPermissionGranted", dict));
                    });
                }
            }
        }

        void OnUserPermissionRevoked(string id, string permName)
        {
            if (_settings.Excluded.LogExcludePerms.Contains(permName)) return;
            var player = covalence.Players.FindPlayerById(id);
            if (player == null) return;
            var dict = new Dictionary<string, string>
            {
                            { "playername", player.Name },
                            { "steamid", id },
                            { "permission", permName }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_perms"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnUserPermissionRevoked", dict));
                    });
                }
            }
        }

        void OnGroupPermissionRevoked(string name, string perm)
        {
            if (_settings.Excluded.LogExcludePerms.Contains(perm)) return;
            var dict = new Dictionary<string, string>
            {
                        { "group", name },
                        { "permission", perm }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_perms"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnGroupPermissionRevoked", dict));
                    });
                }
            }
        }

        void OnUserKicked(IPlayer player, string reason)
        {
            if (_settings.OutputFormat.OutputTypeKicks == "Simple")
            {
                var dict = new Dictionary<string, string>
            {
                            { "playername", player.Name },
                            { "reason", reason }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_kicks"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerKicked", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeKicks == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("PLAYER KICKED")
                                              .AddThumbnail("https://i.imgur.com/ekF9ClZ.png")
                                              .AddField("Name", player.Name, true)
                                              .AddField("Reason", reason, true);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_kicks"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        void OnUserBanned(string name, string bannedId, string address, string reason)
        {
            if (_settings.OutputFormat.OutputTypeBans == "Simple")
            {
                var dict = new Dictionary<string, string>
            {
                        { "playername", name },
                        { "steamid", bannedId },
                        { "ip", address },
                        { "reason", reason }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_bans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerBanned", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBans == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("PLAYER BANNED")
                                              .AddThumbnail("https://i.imgur.com/ekF9ClZ.png")
                                              .AddField("Name", name, true)
                                              .AddField("IP", address, true)
                                              .AddField("Steam ID", bannedId, true)
                                              .AddField("Ban Reason", reason, false);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_bans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }

        }

        private void OnUserUnbanned(string name, string id, string ip)
        {
            if (_settings.OutputFormat.OutputTypeBans == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "playername", name },
                            { "steamid", id },
                            { "ip", ip }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_bans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerUnBanned", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBans == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("PLAYER UNBANNED")
                                              .AddThumbnail("https://i.imgur.com/ekF9ClZ.png")
                                              .AddField("Name", name, true)
                                              .AddField("IP", ip, true)
                                              .AddField("Steam ID", id, true);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_bans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        void OnUserNameUpdated(string id, string oldName, string newName) //TESTING FUNCTION
        {

            if ((oldName == newName) || (oldName == "Unnamed")) return;
            if (_settings.OutputFormat.OutputTypeNameChange == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("PLAYER NAME CHANGE")
                                              .AddThumbnail("https://i.imgur.com/Fq4LvFz.png")
                                              .AddField("Old Name", oldName, true)
                                              .AddBlankField(true)
                                              .AddField("New Name", newName, true)
                                              .AddField("Steam ID", GetFormattedSteamID(id), false);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_namechange"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeNameChange == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "oldname", oldName },
                            { "newname", newName },
                            { "steamid", id }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_namechange"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerNameChange", dict));
                        });
                    }
                }
            }

        }

        private object OnServerMessage(string message, string name)
        {
            if (message.Contains("gave") && name == "SERVER")
            {
                var dict = new Dictionary<string, string>
                        {
                                { "name", name },
                                { "givemessage", message }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_admingive"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnF1ItemSpawn", dict));
                        });
                    }
                }
            }

            return null;
        }

        private void OnServerCommand(ConsoleSystem.Arg arg)
        {
            var player1 = arg.Player();
            var emote = arg.GetString(0);

            if (arg.cmd.Name == "gesture")
            {
                if (_emotes.ContainsKey(emote))
                {
                    var emoji = _emotes[emote];
                    var dict = new Dictionary<string, string>
                        {
                                    {"playername", player1.displayName },
                                    {"gesture", emoji }
                        };
                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("msg_gestures"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, Translate("RUST_OnPlayerGesture", dict));
                            });
                        }
                    }
                }
            }
            if (arg.cmd.FullName == "note.update")
            {
                BasePlayer player = arg.Connection.player as BasePlayer;
                if (player == null)
                    return;
                var notemsg = arg.GetString(1, string.Empty);

                if (_settings.OutputFormat.OutputTypeNoteLog == "Simple")
                {
                    var dict = new Dictionary<string, string>
                        {
                                { "playername", player.displayName },
                                { "notemessage", notemsg }
                        };
                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("log_itemnote"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, Translate("RUST_OnNoteUpdate", dict));
                            });
                        }
                    }
                }
                if (_settings.OutputFormat.OutputTypeNoteLog == "Embed")
                {
                    DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("NOTE UPDATE")
                                              .AddThumbnail("https://i.imgur.com/AZvqSSf.png")
                                              .AddField("Author", player.displayName, false)
                                              .AddField("Message", notemsg, false);

                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("log_itemnote"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, builder.Build());
                            });
                        }
                    }
                }
            }
        }
        private readonly Dictionary<string, string> _emotes = new Dictionary<string, string>
        {
            ["wave"] = ":wave:",
            ["shrug"] = ":shrug:",
            ["victory"] = ":trophy:",
            ["thumbsup"] = ":thumbsup:",
            ["chicken"] = ":chicken:",
            ["hurry"] = ":runner:",
            ["whoa"] = ":flag_white:"
        };
        void OnPlayerReported(BasePlayer reporter, string targetName, string targetId, string subject, string message, string type)
        {
            if (_settings.OutputFormat.OutputTypeF7Report == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("PLAYER F7 REPORT")
                                              .AddThumbnail("https://i.imgur.com/qg7v0Tv.png")
                                              .AddField("Reporter", reporter.displayName, false)
                                              .AddField("User Reported", GetPlayerFormattedField(covalence.Players.FindPlayerById(targetId)), false)
                                              .AddField("Reason", subject, false)
                                              .AddField("Message", message, false);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_f7reports"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeF7Report == "Simple")
            {
                var dict = new Dictionary<string, string>
             {
                            { "reporter", reporter.displayName },
                            { "targetplayer", targetName },
                            { "targetsteamid", targetId },
                            { "reason", subject },
                            { "message", message }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_f7reports"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnPlayerReported", dict));
                        });
                    }
                }
            }

        }

        void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            if (_settings.OutputFormat.OutputTypeTeams == "Embed")
            {
                string leaderName = covalence.Players.FindPlayerById(team.teamLeader.ToString())?.Name;

                List<string> players = Pool.GetList<string>();
                foreach (ulong member in team.members)
                {
                    IPlayer memberPlayer = covalence.Players.FindPlayerById(member.ToString());
                    if (memberPlayer != null)
                    {
                        players.Add(GetPlayerFormattedField(memberPlayer));
                    }
                    else
                    {
                        players.Add(member.ToString());
                    }
                }

                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("NEW TEAM CREATED")
                                              .AddThumbnail("https://i.imgur.com/ChdmYGD.png")
                                              .AddColor("#800080")
                                              .AddField("Team ID", team.teamID.ToString(), true)
                                              .AddBlankField(true)
                                              .AddField("Leader", leaderName, true)
                                              .AddField("Members", string.Join("\n", players), false);

                Pool.FreeList(ref players);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeTeams == "Simple")
            {
                var dict = new Dictionary<string, string>
            {
                { "playername", player.displayName },
                { "teamid", team.teamID.ToString() }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnTeamCreated", dict));
                        });
                    }
                }
            }

        }
        void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeTeams == "Embed")
            {
                NextTick(() =>
                {
                    string leaderName = covalence.Players.FindPlayerById(team.teamLeader.ToString())?.Name;

                    List<string> players = Pool.GetList<string>();
                    foreach (ulong member in team.members)
                    {
                        IPlayer memberPlayer = covalence.Players.FindPlayerById(member.ToString());
                        if (memberPlayer != null)
                        {
                            players.Add(GetPlayerFormattedField(memberPlayer));
                        }
                        else
                        {
                            players.Add(member.ToString());
                        }
                    }

                    DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("TEAM JOIN")
                                                  .AddColor("#00FF00")
                                                  .AddThumbnail("https://i.imgur.com/nV7KfWf.png")
                                                  .AddDescription($"{player.displayName} has joined {leaderName}'s team.")
                                                  .AddField("Team ID", team.teamID.ToString(), true)
                                                  .AddBlankField(true)
                                                  .AddField("Team Leader", leaderName, true)
                                                  .AddField("Team Members", string.Join("\n", players), false);

                    Pool.FreeList(ref players);

                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("log_teams"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, builder.Build());
                            });
                        }
                    }
                });
            }
            if (_settings.OutputFormat.OutputTypeTeams == "Simple")
            {
                var dict = new Dictionary<string, string>
            {
                 { "playername", player.displayName },
                 { "teamleader", team.GetLeader().displayName },
                 { "teamid", team.teamID.ToString() }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnTeamAcceptInvite", dict));
                        });
                    }
                }
            }

        }

        private string GetPlayerFormattedField(IPlayer player)
        {
            return $"{player.Name} ([{player.Id}](https://steamcommunity.com/profiles/{player.Id}))";
        }
        private string GetFormattedSteamID(string id)
        {
            return $"[{id}](https://steamcommunity.com/profiles/{id})";
        }
        void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeTeams == "Embed")
            {
                string leaderName = covalence.Players.FindPlayerById(team.teamLeader.ToString())?.Name;
                if (player.displayName == team.GetLeader().displayName) return;
                List<string> players = Pool.GetList<string>();
                foreach (ulong member in team.members)
                {
                    IPlayer memberPlayer = covalence.Players.FindPlayerById(member.ToString());
                    if (memberPlayer != null)
                    {
                        players.Add(GetPlayerFormattedField(memberPlayer));
                    }
                    else
                    {
                        players.Add(member.ToString());
                    }
                }

                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                .AddTitle("TEAM PART")
                                                .AddColor("#FFA500")
                                                .AddThumbnail("https://i.imgur.com/92y7DWt.png")
                                                .AddDescription($"{player.displayName} left {leaderName}'s team.")
                                                .AddField("Team ID", team.teamID.ToString(), true)
                                                .AddBlankField(true)
                                                .AddField("Team Leader", leaderName, true)
                                                .AddField("Team Members", string.Join("\n", players), false);

                Pool.FreeList(ref players);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeTeams == "Simple")
            {
                if (player == team.GetLeader()) return;
                if ((team == null) || (player == null)) return;
                var dict = new Dictionary<string, string>
            {
                            { "playername", player.displayName },
                            { "teamleader", team.GetLeader().displayName },
                            { "teamid", team.teamID.ToString() }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnTeamLeave", dict));
                        });
                    }
                }
            }
        }
        void OnTeamKick(RelationshipManager.PlayerTeam team, BasePlayer player, ulong target)
        {
            if (_settings.OutputFormat.OutputTypeTeams == "Embed")
            {
                NextTick(() =>
                {
                    string leaderName = covalence.Players.FindPlayerById(team.teamLeader.ToString())?.Name;
                    var targetplayer = GetPlayer(target);

                    List<string> players = Pool.GetList<string>();
                    foreach (ulong member in team.members)
                    {
                        IPlayer memberPlayer = covalence.Players.FindPlayerById(member.ToString());
                        if (memberPlayer != null)
                        {
                            players.Add(GetPlayerFormattedField(memberPlayer));
                        }
                        else
                        {
                            players.Add(member.ToString());
                        }
                    }

                    DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                                  .AddTitle("TEAM KICK")
                                                  .AddColor("#FF0000")
                                                  .AddThumbnail("https://i.imgur.com/92y7DWt.png")
                                                  .AddDescription($"{targetplayer.Name} was kicked from {leaderName}'s team.")
                                                  .AddField("Team ID", team.teamID.ToString(), true)
                                                  .AddBlankField(true)
                                                  .AddField("Team Leader", leaderName, true)
                                                  .AddField("Team Members", string.Join("\n", players), false);

                    Pool.FreeList(ref players);

                    for (int i = 0; i < _channelCount; i++)
                    {
                        if (_settings.Channels[i].perms.Contains("log_teams"))
                        {
                            GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                            {
                                chan.CreateMessage(_client, builder.Build());
                            });
                        }
                    }
                });
            }
            if (_settings.OutputFormat.OutputTypeTeams == "Simple")
            {
                var targetplayer = FindPlayerByID(target.ToString());
                var dict = new Dictionary<string, string>
                        {
                            { "playername", targetplayer.displayName },
                            { "teamleader", team.GetLeader().displayName },
                            { "teamid", team.teamID.ToString() }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnTeamKicked", dict));
                        });
                    }
                }
            }
        }
        void OnTeamDisbanded(RelationshipManager.PlayerTeam team)
        {
            if (_settings.OutputFormat.OutputTypeTeams == "Embed")
            {
                string leaderName = covalence.Players.FindPlayerById(team.teamLeader.ToString())?.Name;

                List<string> players = Pool.GetList<string>();
                foreach (ulong member in team.members)
                {
                    IPlayer memberPlayer = covalence.Players.FindPlayerById(member.ToString());
                    if (memberPlayer != null)
                    {
                        players.Add(GetPlayerFormattedField(memberPlayer));
                    }
                    else
                    {
                        players.Add(member.ToString());
                    }
                }

                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("TEAM DISBANDED")
                                              .AddColor("#FF0000")
                                              .AddThumbnail("https://i.imgur.com/B9mPg0l.png")
                                              .AddField("Team ID", team.teamID.ToString(), true)
                                              .AddBlankField(true)
                                              .AddField("Leader", leaderName, true);

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeTeams == "Simple")
            {
                var dict = new Dictionary<string, string>
            {
                            { "teamleader", team.GetLeader().displayName },
                            { "teamid", team.teamID.ToString() }
            };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("log_teams"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("RUST_OnTeamDisbanded", dict));
                        });
                    }
                }
            }
        }

        private void ConsoleLog(string condition, string stackTrace, LogType type)
        {
            if (string.IsNullOrEmpty(condition))
            {
                return;
            }
            var dict = new Dictionary<string, string>
            {
                { "logtext", condition }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                Settings.Channel channel = _settings.Channels[i];
                if (channel.CustomFilter.Any(c => condition.Contains(c)))
                {
                    GetChannel(_client, channel.Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_CustomLog", dict));
                    });
                }
            }
        }

        private void OnPlayerSpectate(BasePlayer player, string spectateFilter)
        {
            var dict = new Dictionary<string, string>
            {
                { "player", player.displayName },
                { "target", spectateFilter }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_spectates"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnPlayerSpectate", dict));
                    });

                }
            }
        }
        private void OnPlayerSpectateEnd(BasePlayer player, string spectateFilter)
        {
            var dict = new Dictionary<string, string>
            {
                { "player", player.displayName },
                { "target", spectateFilter }
            };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_spectates"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnPlayerSpectateEnd", dict));
                    });
                }
            }
        }

        //RCON SUPPORT
        private void OnRconConnection(IPAddress ip)
        {
            var dict = new Dictionary<string, string>
                        {
                            { "ip", ip.ToString() }
                        };
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("log_rcon"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("RUST_OnRCONConnected", dict));
                    });
                }
            }
        }


        //           ======================================================================
        //           ||                      EXTERNAL PLUGINS SUPPORT                    ||
        //           ======================================================================

        //Admin Hammer
        void OnAdminHammerEnabled(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeAdminHammer == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminhammer"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_AdminHammer_Enabled", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeAdminHammer == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Admin Hammer")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.displayName} has enabled Admin Hammer.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5ebb965d00f21.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminhammer"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }

        }
        void OnAdminHammerDisabled(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeAdminHammer == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminhammer"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_AdminHammer_Disabled", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeAdminHammer == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Admin Radar")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.displayName} has disabled Admin Hammer.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5ebb965d00f21.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminhammer"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        //Admin Radar
        void OnRadarActivated(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeAdminRadar == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminradar"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_AdminRadar_Enabled", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeAdminRadar == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Admin Radar")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.displayName} has enabled Admin Radar.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5b7e1bc17d769.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminradar"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }
        void OnRadarDeactivated(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeAdminRadar == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminradar"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_AdminRadar_Disabled", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeAdminRadar == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Admin Radar")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.displayName} has disabled Admin Radar.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5b7e1bc17d769.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_adminradar"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        //Better Chat Mute

        private void OnBetterChatMuted(IPlayer target, IPlayer player, string reason)
        {
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "target", target.Name },
                            { "reason", reason },
                            { "muter", player.Name }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_BetterChatMute_Mute", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Better Chat Mute")
                                              .AddColor("#800080")
                                              .AddDescription($"Permanent Mute Issued By: {player.Name}.")
                                              .AddField("Muted Player", target.Name, false)
                                              .AddField("Reason", reason, false)
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5f242f4c92225.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        private void OnBetterChatTimeMuted(IPlayer target, IPlayer player, TimeSpan time, string reason)
        {
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "target", target.Name },
                            { "reason", reason },
                            { "muter", player.Name },
                            { "time", FormatTime((TimeSpan) time) }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_BetterChatMute_TimedMute", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Better Chat Mute")
                                              .AddColor("#800080")
                                              .AddDescription($"Temporary Mute Issued By: {player.Name}.")
                                              .AddField("Muted Player", target.Name, true)
                                              .AddBlankField(true)
                                              .AddField("Duration", FormatTime((TimeSpan)time), true)
                                              .AddField("Reason", reason, false)
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5f242f4c92225.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        private void OnBetterChatUnmuted(IPlayer target, IPlayer player)
        {
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "target", target.Name },
                            { "unmuter", player.Name }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_BetterChatMute_UnMute", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Better Chat Mute")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.Name} removed {target.Name}'s mute.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5f242f4c92225.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }


        private void OnBetterChatMuteExpired(IPlayer target)
        {
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "target", target.Name }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_BetterChatMute_MuteExpire", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeBetterChatMute == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Better Chat Mute")
                                              .AddColor("#800080")
                                              .AddDescription($"{target.Name}'s mute has expired.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5f242f4c92225.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_betterchatmute"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        private static string FormatTime(TimeSpan time)
        {
            var values = new List<string>();

            if (time.Days != 0)
                values.Add($"{time.Days} day(s)");

            if (time.Hours != 0)
                values.Add($"{time.Hours} hour(s)");

            if (time.Minutes != 0)
                values.Add($"{time.Minutes} minute(s)");

            if (time.Seconds != 0)
                values.Add($"{time.Seconds} second(s)");

            return values.ToSentence();
        }

        //Clans
        void OnClanCreate(string tag, string ownerID)
        {
            if ((_settings.PluginLog.LogPluginClans == true) && (_settings.OutputFormat.OutputTypeClans == "Simple"))
            {
                var dict = new Dictionary<string, string>
                        {
                            { "clan", tag }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_clans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_Clans_CreateClan", dict));
                        });
                    }
                }

            }
            if ((_settings.PluginLog.LogPluginClans == true) && (_settings.OutputFormat.OutputTypeClans == "Embed"))
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                          .AddTitle("CLANS")
                                          .AddColor("#800080")
                                          .AddDescription($"Clan {tag} has been created.")
                                          .AddThumbnail("https://assets.umod.org/images/icons/plugin/5b63bf642ea2c.jpg");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_clans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }
        void OnClanDisbanded(string tag, List<string> memberUserIDs)
        {
            if ((_settings.PluginLog.LogPluginClans == true) && (_settings.OutputFormat.OutputTypeClans == "Simple"))
            {
                var dict = new Dictionary<string, string>
                        {
                            { "clan", tag }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_clans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_Clans_DisbandClan", dict));
                        });
                    }
                }
            }
            if ((_settings.PluginLog.LogPluginClans == true) && (_settings.OutputFormat.OutputTypeClans == "Embed"))
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                          .AddTitle("CLANS")
                                          .AddColor("#800080")
                                          .AddDescription($"Clan {tag} has been Disbanded.")
                                          .AddThumbnail("https://assets.umod.org/images/icons/plugin/5b63bf642ea2c.jpg");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_clans"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }


        void OnClanChat(IPlayer player, string message)
        {
            var dict = new Dictionary<string, string>
                        {
                            { "playername", player.Name },
                            { "message", message }
                        };
            if (player.Name == null || message == null) return;
            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("plugin_clanchat"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate("PLUGIN_Clans_Chat", dict));
                    });
                }
            }
        }

        // Vanish
        void OnVanishDisappear(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeVanish == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_vanish"))
                    {

                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_Vanish_Disappear", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeVanish == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Vanish")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.displayName} has vanished.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5e2c4da074770.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_vanish"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }
        void OnVanishReappear(BasePlayer player)
        {
            if (_settings.OutputFormat.OutputTypeVanish == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName }
                        };
                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_vanish"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_Vanish_Reappear", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeVanish == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Vanish")
                                              .AddColor("#800080")
                                              .AddDescription($"{player.displayName} has reappeared.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5e2c4da074770.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_vanish"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }


        //PrivateMessages
        [HookMethod("OnPMProcessed")]
        void OnPMProcessed(IPlayer sender, IPlayer target, string message)
        {
            if (_settings.OutputFormat.OutputTypePMs == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "sender", sender.Name },
                            { "target", target.Name },
                            { "message", message }
                        };

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_privatemessages"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_PrivateMessages_PM", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypePMs == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Private Message")
                                              .AddColor("#800080")
                                              .AddField("From", sender.Name, true)
                                              .AddBlankField(true)
                                              .AddField("To", target.Name, true)
                                              .AddField("Message", message, false)
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5b66ed6b7e606.jpg");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_privatemessages"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }

        //RaidableBases
        void OnRaidableBaseStarted(Vector3 pos, int difficulty)
        {
            string rbdiff = string.Empty;
            if (difficulty == 0) rbdiff = "Easy";
            if (difficulty == 1) rbdiff = "Medium";
            if (difficulty == 2) rbdiff = "Hard";

            if (_settings.OutputFormat.OutputTypeRaidableBases == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "position", pos.ToString() },
                            { "difficulty", rbdiff }
                        };

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_raidablebases"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_RaidableBases_Started", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeRaidableBases == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Raidable Bases")
                                              .AddColor("#800080")
                                              .AddDescription("A Raidable Base Has Spawned.")
                                              .AddField("Difficulty", rbdiff, true)
                                              .AddBlankField(true)
                                              .AddField("Location", pos.ToString(), true)
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5e986213be8c8.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_raidablebases"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }
        void OnRaidableBaseEnded(Vector3 pos, int difficulty)
        {
            string rbdiff = string.Empty;
            if (difficulty == 0) rbdiff = "Easy";
            if (difficulty == 1) rbdiff = "Medium";
            if (difficulty == 2) rbdiff = "Hard";

            if (_settings.OutputFormat.OutputTypeRaidableBases == "Simple")
            {
                var dict = new Dictionary<string, string>
                        {
                            { "position", pos.ToString() },
                            { "difficulty", rbdiff }
                        };

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_raidablebases"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, Translate("PLUGIN_RaidableBases_Ended", dict));
                        });
                    }
                }
            }
            if (_settings.OutputFormat.OutputTypeRaidableBases == "Embed")
            {
                DiscordEmbedBuilder builder = new DiscordEmbedBuilder()
                                              .AddTitle("Raidable Bases")
                                              .AddColor("#800080")
                                              .AddDescription($"Raidable Base at {pos.ToString()} Has Ended.")
                                              .AddThumbnail("https://assets.umod.org/images/icons/plugin/5e986213be8c8.png");

                for (int i = 0; i < _channelCount; i++)
                {
                    if (_settings.Channels[i].perms.Contains("plugin_raidablebases"))
                    {
                        GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                        {
                            chan.CreateMessage(_client, builder.Build());
                        });
                    }
                }
            }
        }


        //SignArtist
        private void OnImagePost(BasePlayer player, string image)
        {
            var dict = new Dictionary<string, string>
                        {
                            { "player", player.displayName },
                            { "position", $"{player.transform.position.x} {player.transform.position.y} {player.transform.position.z}" }
                        };

            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("plugin_signartist"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, SignArtistEmbed(Translate("PLUGIN_SignArtist", dict), image));
                    });
                }
            }
        }

        private DiscordEmbed SignArtistEmbed(string text, string image)
        {
            DiscordEmbed embed = new DiscordEmbedBuilder()
                                 .AddTitle(text)
                                 .AddColor(52326)
                                 .AddImage(image)
                                 .Build();

            return embed;
        }
        //DiscordAuth
        private void OnAuthenticate(string steamId, string discordId)
        {
            Snowflake id;
            if (Snowflake.TryParse(discordId, out id))
            {
                ProcessDiscordAuth("PLUGIN_DiscordAuth_Auth", steamId, id);
            }
        }

        private void OnDeauthenticate(string steamId, string discordId)
        {
            Snowflake id;
            if (Snowflake.TryParse(discordId, out id))
            {
                ProcessDiscordAuth("PLUGIN_DiscordAuth_Deauth", steamId, id);
            }
        }

        private void ProcessDiscordAuth(string key, string steamId, Snowflake discordId)
        {
            var player = covalence.Players.FindPlayerById(steamId);
            var user = FindUserByID(discordId);

            if (player == null || user == null)
                return;

            for (int i = 0; i < _channelCount; i++)
            {
                if (_settings.Channels[i].perms.Contains("plugin_discordauth"))
                {
                    GetChannel(_client, _settings.Channels[i].Channelid, chan =>
                    {
                        chan.CreateMessage(_client, Translate(key, new Dictionary<string, string>
                        {
                            { "gamename", player.Name },
                            { "discordname", user.GetFullUserName }
                        }));
                    });
                }
            }
        }
    }
}