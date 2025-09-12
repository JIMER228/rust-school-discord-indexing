using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Random = System.Random;

using UnityEngine;
using UnityEngine.Networking;
using System.Collections;

using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Oxide.Core.Libraries.Covalence;

using Oxide.Ext.Discord;
using Oxide.Ext.Discord.Attributes;
using Oxide.Ext.Discord.Entities;
using Oxide.Ext.Discord.Entities.Activities;
using Oxide.Ext.Discord.Builders.MessageComponents;
using Oxide.Ext.Discord.Entities.Interactions;
using Oxide.Ext.Discord.Entities.Interactions.ApplicationCommands;
using Oxide.Ext.Discord.Entities.Interactions.MessageComponents;
using Oxide.Ext.Discord.Entities.Emojis;
using Oxide.Ext.Discord.Entities.Channels;
using Oxide.Ext.Discord.Entities.Gatway;
using Oxide.Ext.Discord.Entities.Gatway.Commands;
using Oxide.Ext.Discord.Entities.Gatway.Events;
using Oxide.Ext.Discord.Entities.Guilds;
using Oxide.Ext.Discord.Entities.Messages;
using Oxide.Ext.Discord.Entities.Messages.Embeds;
using Oxide.Ext.Discord.Entities.Users;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Extensions;

namespace Oxide.Plugins
{
    [Info("MapVoter", "Kaysharp", "1.3.37")]
    class MapVoter : CovalencePlugin
    {
        [PluginReference]  Plugin ImageLibrary, WipeInfoApi ,ServerRewards, Kits;
        #region fields
        [DiscordClient] private DiscordClient _client;
        readonly Dictionary<string, Timer> dotimer = new Dictionary<string, Timer>();
        private readonly Random _random = new Random();
        private string permissionUse = "MapVoter.use";
        private string permissionVote = "MapVoter.vote";
        private string PermissionManager = "MapVoter.Manager";
        public static ConfigData config { get; set; }
        public static ConfigData configTEMP { get; set; }
        private List<uint> Generating = new List<uint>();
        private List<RustMaps> Maps = new List<RustMaps>();
        private List<RustMaps> MVData = new List<RustMaps>();
        private AutoWipeConfig WipeData;
        private List<RustMaps> FiltredMaps = new List<RustMaps>();
        private List<Result> FMaps = new List<Result>();
        filteredMaps fMaps = new filteredMaps();
        private Dictionary<uint,int> VoteResult = new Dictionary<uint,int>();
        private VoteData voteData;
        private List<string> Voted = new List<string>();
        private List<CustomMap> CustomMaps = new List<CustomMap>();
        private string _stopvote , nbMaps, size, CMapSize,isOnlyAuth, StopVotingafter, AutoVoting , enableautowipe, _nbMap, _isCustomMap, bpswipe, _MapWipeSchedule, _FullWipeSchedule= String.Empty;
        private bool VoteStarted = false;
        private int RustMapsPage = 0;
        private string filterId = string.Empty;
        private DateTime CurrentDate;
        private bool IsFullWipe = false;
        private bool ForcedWipe = false;
        private bool IsRestarting, WipeCanceled, VotingCanceled = false;
        private bool IsWipeDay = false;
        private string MagnifyIconURL = "https://chaoscode.io/oxide/Images/magnifyingglass.png";
        private const string VoteButtonId = nameof(MapVoter) + "_Vote";
        private const string VoteButtonIcon = "📩";
        private bool LogToDiscord = false;
        private bool isCustomMaps = false;
        private bool isFunKitEnabled = false;
#if SIMULATE_OXIDE_PATCH
        static readonly VersionNumber CurrentOxideRustVersion = new VersionNumber(0, 0, 0);
#else
        static readonly VersionNumber CurrentOxideRustVersion = Interface.Oxide.GetAllExtensions().First(e => e.Name == "Rust").Version;
#endif
        private int? _channelCount;
        private Timer StatusTimer = null;
        private UpdatePresenceCommand DiscordPresence = new UpdatePresenceCommand
        {
            Activities = new List<DiscordActivity>
            {
                new DiscordActivity
                {
                    Type = ActivityType.Game,
                    Name = "Discrod bot Initializing..."
                }
            }
        };
        private Snowflake _botId;
        #endregion
        #region Config  
        public class ConfigData
        {
            [JsonProperty(PropertyName = "Commands")]
            public CommandsOptions Commands = new CommandsOptions();

            [JsonProperty(PropertyName = "Rewards")]
            public Rewards rewards = new Rewards();

            [JsonProperty(PropertyName = "Fun Kit")]
            public FunKit funKit = new FunKit();

            [JsonProperty(PropertyName = "Options")]
            public Options Settings = new Options();

            [JsonProperty(PropertyName = "Discord Settings")]
            public DiscordSettings _DiscordSettings = new DiscordSettings();

            [JsonProperty(PropertyName = "Auto Vote")]
            public MVAutoVote AutoVote = new MVAutoVote();

            [JsonProperty(PropertyName = "Auto Wipe")]
            public AutoWipe _AutoWipe = new AutoWipe();
            public class CommandsOptions
            {
                [JsonProperty(PropertyName = "Open MapVoter UI")]
                public string mapvote { get; set; }

                [JsonProperty(PropertyName = "Generate Mpas")]
                public string Generate { get; set; }

                [JsonProperty(PropertyName = "vote result")]
                public string voteresult { get; set; }

            }
            public class Rewards
            {
                [JsonProperty(PropertyName = "Rust rewards")]
                public bool RustRewards { get; set; }
                [JsonProperty(PropertyName = "reward points")]
                public int RWPoints { get; set; }
                [JsonProperty(PropertyName = "Kits")]
                public bool Kits { get; set; }
                [JsonProperty(PropertyName = "Kit name")]
                public string KitName { get; set; }
            }
            public class FunKit
            {
                [JsonProperty(PropertyName = "Fun kit enabled")]
                public bool enabled { get; set; }

                [JsonProperty(PropertyName = "Enable Fun kit x minutes before wipe")]
                public int KitStartTime { get; set; }

                [JsonProperty(PropertyName = "Kit name")]
                public string KitName { get; set; }
                [JsonProperty(PropertyName = "Permission")]
                public string Perm { get; set; }

            }
            public class Options
            {
                [JsonProperty(PropertyName = "Select random maps from rustmaps filter id instead of generating random maps on wipe day (true/false)")]
                public bool isRandomMapsFromfilterID { get; set; }

                [JsonProperty(PropertyName = "How many pages the plugin looks up per search request(every page has 30 maps")]
                public int MaxPages { get; set; }

                [JsonProperty(PropertyName = "Enable Discord bot (true/false)")]
                public bool isDiscordBot { get; set; }

                [JsonProperty(PropertyName = "Only players with permission MapVoter.Vote can vote (true/false)")]
                public bool isOnlyPlayersWithPermCanVote { get; set; }

                [JsonProperty(PropertyName = "Log to Discord (true/false)")]
                public bool DiscordLogs { get; set; }

                [JsonProperty(PropertyName = "Discord Logs Channel Id")]
                public string DiscordLogChannelID { get; set; }

                [JsonProperty(PropertyName = "Disable UI")]
                public bool UIisDisabled { get; set; }

                [JsonProperty(PropertyName = "RustMaps API key")]
                public string APIKey { get; set; }

                [JsonProperty(PropertyName = "Map size")]
                public int size = 3500;

                [JsonProperty(PropertyName = "staging")]
                public bool staging { get; set; }
                [JsonProperty(PropertyName = "barren")]
                public bool barren { get; set; }

                [JsonProperty(PropertyName = "Stop voting after (minutes)")]
                public int Stopvote { get; set; }

                [JsonProperty(PropertyName = "avatar url")]
                public string avatar_url { get; set; }

                [JsonProperty(PropertyName = "Discord footer")]
                public string Footer { get; set; }

                [JsonProperty(PropertyName = "filter Id")]
                public string filterId  { get; set; }
                
            }   
            public class DiscordSettings
            {
                [JsonProperty(PropertyName = "Vote Channel id")]
                public string Vote_Channel_id { get; set; }

                [JsonProperty(PropertyName = "Discord Apikey")]
                public string Discord_ApiKey { get; set; }

                [JsonProperty(PropertyName = "Discord Command Prefix")]
                public string Commandprefix { get; set; }

                [JsonProperty(PropertyName = "Discord Channels")]
                public List<DiscordChannel> Channels { get; set; }

                [JsonProperty(PropertyName = "Discord Command Role Assignment (Empty = All roles can use command.)")]
                public Dictionary<string, List<string>> CmdRoles { get; set; }
            }
            public class DiscordChannel
            {
                [JsonProperty(PropertyName = "Discord Channel ID")] 
                public Snowflake ChannelId { get; set; }

                [JsonProperty(PropertyName = "Commands")]
                public List<string> Commands { get; set; }
            }
            public class MVAutoVote
            {
                [JsonProperty(PropertyName = "Auto start vote")]
                public bool isAutoVote { get; set; }

                [JsonProperty(PropertyName = "Only Authenticated users can vote through discord")]
                public bool IsOnlyAuthUsers { get; set; }

                [JsonProperty(PropertyName = "Start voting x days before wipe")]
                public int StartVotingDaysBeforewipe { get; set; }

                [JsonProperty(PropertyName = "Start voting at (HH:mm) 24-hour clock")]
                public string StartVotingat { get; set; }
                
                [JsonProperty(PropertyName = "Number of maps to generate")]
                public int nbm { get; set; }

            } 
            public class AutoWipe
            {
                [JsonProperty(PropertyName = "Enable Auto wipe")]
                public bool isAutoWipe { get; set; }

                [JsonProperty(PropertyName = "Custom Map")]
                public CustomMap _CustomMap = new CustomMap();

                [JsonProperty(PropertyName = "Wipe BPs at forced wipe day")]
                public bool WipeBPs { get; set; }

                [JsonProperty(PropertyName = "Forced Wipe time (HH:mm) 24-hour clock")]
                public string  ForcedWipeTime { get; set; }

                [JsonProperty(PropertyName = "Wipe time (HH:mm) 24-hour clock")]
                public string  WipeTime { get; set; }

                [JsonProperty(PropertyName = "Map Wipe schedule")]
                public List<int> MWschedule =  new List<int>();

                [JsonProperty(PropertyName = "BP Wipe schedule")]
                public List<int> FWSchedule = new List<int>();

            }
            public class CustomMap
            {
                [JsonProperty(PropertyName = "Custom map")]
                public bool isCustomMap { get; set; }

                [JsonProperty(PropertyName = "Map URL")]
                public string Url { get; set; }
            }

        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                Config.WriteObject(config);
                if (config == null) throw new Exception();
            }
            catch
            {
                Config.WriteObject(config, false, $"{Interface.Oxide.ConfigDirectory}/{Name}.jsonError");
                PrintError("The configuration file contains an error and has been replaced with a default config.\n" +
                           "The error configuration file was saved in the .jsonError extension");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig() => config = GetDefaultSettings();
        private ConfigData GetDefaultSettings()
        {
            return new ConfigData
            {
                Commands = new ConfigData.CommandsOptions
                {
                    mapvote = "mvote",
                    Generate = "MapVoter.generate",
                    voteresult = "voteresult"
                },
                rewards = new ConfigData.Rewards
                {
                    RustRewards = true,
                    RWPoints = 5,
                    Kits = true,
                    KitName = string.Empty
                },
                funKit = new ConfigData.FunKit
                {
                    enabled = false,
                    KitStartTime = 120,
                    KitName = "FunKit",
                    Perm = ""
                },
                Settings = new ConfigData.Options
                {
                    isRandomMapsFromfilterID = false,
                    MaxPages = 10,
                    isDiscordBot = true,
                    isOnlyPlayersWithPermCanVote =false,
                    DiscordLogs = false,
                    DiscordLogChannelID = string.Empty,
                    UIisDisabled = false,
                    APIKey = "https://rustmaps.com/user/profile",
                    size = 3500,
                    staging = false,
                    barren = false,
                    Stopvote = 60,
                    avatar_url = "",
                    Footer = "",
                    filterId = "Visit https://rustmaps.com/ and adjust your map requirements. In the red box above the settings hit the Share button,the string at the end of the URL is the filterId.Example URL: https://rustmaps.com/?share=gEU5W6BUuUG5FpPlyv2nhQ the string at the end in this case {gEU5W6BUuUG5FpPlyv2nhQ} is the filterId."
                },
                _DiscordSettings = new ConfigData.DiscordSettings
                {
                    Vote_Channel_id = "",
                    Discord_ApiKey = "BotToken",
                    Commandprefix = "!",
                    Channels = new List<ConfigData.DiscordChannel>
                    {
                        new ConfigData.DiscordChannel
                        {
                            Commands = new List<string>
                            {
                                "generate",
                                "vote",
                                "mapwipe",
                                "bpwipe",
                                "cancelwipe",
                                "stopvoting",
                                "update",
                                "cancelupdate"
                            }
                        }
                    },
                    CmdRoles = new Dictionary<string, List<string>>
                    {
                        {
                            "generate", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "vote", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "mapwipe", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "bpwipe", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "cancelwipe", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "stopvoting", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "update", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        },
                        {
                            "cancelupdate", new List<string>()
                            {
                                "DiscordRoleName",
                                "DiscordRoleName2"
                            }
                        }
                    }
                },
                AutoVote = new ConfigData.MVAutoVote
                {
                    isAutoVote = false,
                    IsOnlyAuthUsers = false,
                    StartVotingDaysBeforewipe = 0,
                    StartVotingat = "17:00",
                    nbm = 4,
                },
                _AutoWipe = new ConfigData.AutoWipe
                {
                    isAutoWipe = true,
                    _CustomMap = new ConfigData.CustomMap
                    {
                        isCustomMap = false,
                        Url = ""
                    },
                    WipeBPs = true,
                    ForcedWipeTime  = "19:00",
                    WipeTime = "19:00",
                    MWschedule = new List<int>
                    {
                        7,
                        14,
                        21,
                        28
                    },
                    FWSchedule = new List<int>
                    {
                        0
                    }
                }
            };
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        protected override void LoadDefaultMessages() {
            lang.RegisterMessages(new Dictionary<string, string> {
                ["NoPermission"] = "You don't have the permission to use this command.",
                ["NoPermissionVote"] = "You don't have the permission to vote.",
                ["args"] = "Must type number of maps to generate.",
                ["GUI.Title"] = "Maps",
                ["GUI.CreateNew"] = "Settings",
                ["GUI.AddMaps"] = "Add Maps",
                ["UI.Title.Editor"] = "Settings",
                ["UI.Title.Add"] = "Add Maps",
                ["UI.Title"] = "Map Seed",
                ["UI.Details"] = "Generate",
                ["UI.cDetails"] = "Custom map details",
                ["UI.Procedural"] = "Procedural Map",
                ["UI.Custom"] = "Custom Map",
                ["UI.MapSize"] = "Map size",
                ["UI.generate"] = "Generate",
                ["UI.nb"] = "Ammount",
                ["UI.Save"] = "Save",
                ["MapLocked"] = "Map Locked {0}",
                ["Voting"] = "Map Vote has started! /{0}.",
                ["MapSave.Error.NoName"] = "Error",
                ["UI.NoMapsAvailable"] = "There are currently no Maps available",
                ["UI.Vote"] = "Vote",
                ["UI.Voted"] = "You have already voted.",
                ["UI.Generating"] = "Generating Map(s).This step usually takes around 2-5 minutes.",
                ["UI.Votenotstarted"] = "Map vote is not activated at the moment.",
                ["VoteStarted"] = "Map vote has started",
                ["VoteSuccess"] = "Thanks for voting",
                ["UI.stopVoteTimer"] = "Stop voting after",
                ["WipeAPIerror"] = "WipeAPI not found https://umod.org/plugins/wipe-info-api",
                ["GUI.SelectMaps"] = "Select Maps",
                ["UI.Select"] = "Select",
                ["UI.Loading"] = "Loading Maps.Filter Id : {0}",
                ["UI.Selected"] = "Selected",
                ["VoteResult"] = "Map Seed: {0} Votes: {1} Image Url : {2}",
                ["CurrentState"] = "Map Id: {0} current state: {1}",
                ["Commands"] = "Commands",
                ["Vote channel id"] = "Vote channel id",
                ["Error channel id"] = "Vote channel id is null or Empty",
                ["Options"] = "Options",
                ["AutoVote"] = "Auto Vote",
                ["AutoWipe"] = "Auto Wipe",
                ["Options"] = "Options",
                ["RustMaps API key"] = "RustMaps API key",
                ["Map size"] = "Map size",
                ["Stop voting after (minutes)"] = "Stop voting after (minutes)",
                ["Only Authanticated users can vote through discord"] = "Only Authanticated users can vote through discord",
                ["Discord avatar url"] = "Discord avatar url",
                ["Discord footer"] = "Discord footer",
                ["filter Id"] = "filter Id",
                ["Auto start voting"] = "Auto start voting",
                ["Start voting at (HH:mm) 24-hour clock (server local time)"] = "Start voting at (HH:mm) 24-hour clock (server local time)",
                ["Number of maps to generate"] = "Number of maps to generate",
                ["Open MapVoter UI"] = "Open MapVoter GUI",
                ["Generate Map(s)"] = "Generate Map(s)",
                ["vote result"] = "vote result",
                ["Auto Wipe"] = "Auto Wipe",
                ["Enable Auto wipe"] = "Enable Auto wipe",
                ["Custom map"] = "Custom map",
                ["Custom map URL"] = "Custom map URL",
                ["Wipe BPs at forced wipe day"] = "Wipe BPs at forced wipe day",
                ["Wipe time (HH:mm) 24-hour clock (server local time)"] = "Wipe time (HH:mm) 24-hour clock (server local time)",
                ["Map wipe schedule (Days since forced wipe)"] = "Map wipe schedule (Days since forced wipe)",
                ["Full wipe schedule(Days since forced wipe)"] = "Full wipe schedule(Days since forced wipe)",
                ["Discord Settings"] = "Discord Settings",
                ["Discord Webhook"] = "Discord Webhook",
                ["Bot Apikey"] = "Bot Apikey",
                ["Discord Command Prefix"] = "Discord Command Prefix",
                ["NewOxideVersion"] = "New Oxide.Rust version detected {0} -> {1}.",
                ["OxideUpToDate"] = "Current Oxide.Rust version is up-to-date, scheduling check after {0} Seconds...",
                ["AutoWiping"] ="Auto wiping in {0} Minutes: IsFullWipe : {1} isForcedWipe : {2}",
                ["VoteLog"] ="{0} has voted on map number {1}",
                ["SyntaxError"] = "Syntax error : canConvertDelta {0} canConvertMapSize {1} canConvertMapSeed {2}",
                ["SyntaxError1"] = "Syntax error : canConvertDelta {0} canConvertMapSize {1}",
                ["SyntaxError2"] ="Syntax error : canConvertDelta {0} canConvertMapSize {1} canConverMapUrl {2}",
                ["GUI.Details"] = "Details",
                ["map.Id"] = "Id",
                ["map.seed"] = "Seed",
                ["map.size"] = "Size",
                ["map.imgUrl"] = "Map image url",
                ["map.downloadUrl"] = "Direct download link",
                ["map.Name"] = "Name",
                ["UI.Add"] = "Add",
                ["Restarting"] = "Restarting the server in {0} seconds",
                ["UI.Added"] = "Saved",
                ["interrupted"] = "server restart interrupted",
                ["Defaultfilterid"] = "Visit https://rustmaps.com/ and adjust your map requirements. In the red box above the settings hit the Share button, the string at the end of the URL is the filterId.Example URL: https://rustmaps.com/?share=gEU5W6BUuUG5FpPlyv2nhQ the string at the end in this case {gEU5W6BUuUG5FpPlyv2nhQ} is the filterId"
            }, this);
        }
        string GetMsg(string key) => lang.GetMessage(key, this);
        #endregion
        #region Hooks
        private void OnServerInitialized()
        {
            Unsubscribe(nameof(OnItemAction));
            Unsubscribe(nameof(CanUnlockTechTreeNode));
            Unsubscribe(nameof(OnPlayerRespawned));
            Unsubscribe(nameof(OnPlayerConnected));
            //To make sure no one have permission for fun kit after wipe.
            server.Command($"oxide.revoke group default {config.funKit.Perm}");
            MVData = Interface.Oxide.DataFileSystem.ReadObject<List<RustMaps>>($"MapVoter/{Name}");
            wipeinfo = Interface.Oxide.DataFileSystem.ReadObject<Wipeinfo>("MapVoter/wipeinfo");
            voteData = Interface.Oxide.DataFileSystem.ReadObject<VoteData>("MapVoter/VotesData");
            CustomMaps = Interface.Oxide.DataFileSystem.ReadObject<List<CustomMap>>("MapVoter/CustomMapsData");
            
            if (voteData == null)
            {
                voteData = new VoteData(false);
            }
            else if (voteData.isVoteDay)
            {
                Maps = voteData.Maps;
                Voted = voteData.Voted;
                VoteResult = voteData.VoteResult;
                isCustomMaps = voteData.CustomMap;
                VoteStarted = true;
                dotimer["VoteA"] = timer.Every( 3600, () =>
                {
                    MapVoteA();
                });
            }
            if (wipeinfo == null)
            {
                wipeinfo = new Wipeinfo();
                SaveWipeinfoData();
            }
            isCustomMaps = voteData.CustomMap;
            AddCovalenceCommand(config.Commands.Generate, nameof(cmdGenerate));
            AddCovalenceCommand(config.Commands.mapvote, nameof(cmdVote));
            AddCovalenceCommand(config.Commands.voteresult, nameof(cmdVoteResult));
            permission.RegisterPermission(permissionUse, this);
            permission.RegisterPermission(PermissionManager, this);
            permission.RegisterPermission(permissionVote, this);
            if (ImageLibrary != null)
            {
                RegisterImage("Maps.magnifyicon", MagnifyIconURL);
                foreach (var map in CustomMaps)
                {
                    RegisterImage(map.Name, map.Image);
                }
            }
            filterId = config.Settings.filterId;
            CurrentDate = DateTime.Now;
            WipeData = new AutoWipeConfig(IsWipeDay);
            SaveData();
            if (config.Settings.DiscordLogs && config.Settings.DiscordLogChannelID != null && config.Settings.DiscordLogChannelID != string.Empty && config.Settings.isDiscordBot)
                LogToDiscord = true;
            Puts("Time zone of the current computer:" + TimeZone.CurrentTimeZone.StandardName.ToString() + " \nCurrent date time : " + DateTime.Now);
            GetWipeSchedule();
            if (!IsRestarting && !WipeCanceled && config._AutoWipe.isAutoWipe)
            {
                CheckIfitsWipeDay();
                return;
            }
            if (config.AutoVote.isAutoVote && !VotingCanceled && !config._AutoWipe._CustomMap.isCustomMap)
            {
                autoStartVote();
                return;
            }
        }
        private void OnServerSave()
        {
            SaveData();
            SaveWipeinfoData();
            if (!IsRestarting && !WipeCanceled && config._AutoWipe.isAutoWipe)
            {
                CheckIfitsWipeDay();
                return;
            }
            if (config.AutoVote.isAutoVote && !VotingCanceled && !config._AutoWipe._CustomMap.isCustomMap)
            {
                autoStartVote();
                return;
            }
        }
        private void Unload()
        {
            if (isFunKitEnabled)
                server.Command($"oxide.revoke group default {config.funKit.Perm}");
            SaveData();
            SaveWipeinfoData();
            timer = null;
            Voted.Clear();
            Maps.Clear();
            VoteResult.Clear();
            Generating.Clear();
            FiltredMaps.Clear();
            FMaps.Clear();
            MVData.Clear();
            VoteStarted = false;
            foreach (var timer in dotimer.Values) {
                timer.Destroy();
            }
            dotimer.Clear();
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UI_MENU);
            }
        }
        private void SaveData()
        {
            try 
            {
                Interface.Oxide.DataFileSystem.WriteObject($"MapVoter/{Name}", MVData);
                Interface.Oxide.DataFileSystem.WriteObject($"MapVoter/VotesData", voteData);
                Interface.Oxide.DataFileSystem.WriteObject("MapVoter/CustomMapsData", CustomMaps);
                Interface.Oxide.DataFileSystem.WriteObject("wipe", WipeData);

            } catch (Exception e) {
                Puts(e.Message);
            }
        }
        private void Reload() => ConsoleSystem.Run(ConsoleSystem.Option.Unrestricted, ($"oxide.reload MapVoter"));
        private void OnPlayerDisconnected (BasePlayer player)
        {
            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.DestroyUi(player, UI_POPUP);
        }
        private object OnItemAction(Item item, string action, BasePlayer player)
        {
            if (player == null || item == null) return null;
            if (action != "study") return null;
            return false;
        }
        private object CanUnlockTechTreeNode(BasePlayer player, TechTreeData.NodeInstance node, TechTreeData techTree)
        {
            return false;
        }
        void OnPlayerRespawned(BasePlayer player)
        {
            if (player == null || player.IsNpc)
                return;
            Puts($"Fun kit is enabled /kit {config.funKit.KitName}.");
            InventoryStrip(player);
            Kits?.Call("GiveKit", player, config.funKit.KitName);
        }
        void OnPlayerConnected(BasePlayer player)
        {
            Puts($"Fun kit is enabled /kit {config.funKit.KitName}.");
        }
        #endregion
        #region Commands
        private void cmdGenerate(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            if (args.Length == 0) 
            {
                player.Reply(GetMsg("args"));
                return;
            }
            if (args.Length == 2)
            {
                if(!string.IsNullOrEmpty(args[1]))
                    size = args[1];
            }
            if (args.Length == 3)
            {
                if(!string.IsNullOrEmpty(args[2]))
                    _stopvote = args[2];
            }
            seedGenerate(Int16.Parse(args[0]));
            foreach (var aPlayer in BasePlayer.activePlayerList)
            {
                if (IsAdmin(aPlayer))
                    aPlayer.SendConsoleCommand("chat.add", 0, 76561199214531833UL, GetMsg("UI.Generating"));
                if (LogToDiscord && config.Settings.isDiscordBot)
                    Log(GetMsg("UI.Generating"));
            }
        }
        private void cmdVote(IPlayer player, string cmd, string[] args)
        {
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if (!IsAdmin(Player))
            {
                if (!VoteStarted) 
                {
                    player.Reply(GetMsg("UI.Votenotstarted"));
                    return;
                }
                if (config.Settings.UIisDisabled)
                {
                    if (config.Settings.isOnlyPlayersWithPermCanVote && !player.HasPermission(permissionVote))
                    {
                        player.Reply(GetMsg("NoPermissionVote"));
                        return;
                    }
                    if (Voted.Contains(Player.UserIDString))
                    {
                        player.Reply(GetMsg("UI.Voted"));
                        return;
                    }
                    int val;
                    var key = Convert.ToUInt32(args[0]);
                    if(VoteResult.TryGetValue(key,out val))
                    {
                        VoteResult[key] = val + 1;
                    }
                    Voted.Add(Player.UserIDString);
                    if (ServerRewards != null && config.rewards.RustRewards)
                        ServerRewards?.Call("AddPoints", ((BasePlayer)player).userID, config.rewards.RWPoints);
                    if (Kits != null && config.rewards.Kits && (bool)Kits?.Call("IsKit", config.rewards.KitName))
                        Kits?.Call("GiveKit", (BasePlayer)player, config.rewards.KitName);
                    player.Reply(GetMsg("VoteSuccess"));
                    if (LogToDiscord && config.Settings.isDiscordBot)
                    {
                        int i = 1;
                        foreach (var akey in VoteResult.Keys)
                        {
                            if (akey == key)
                                Log(string.Format(GetMsg("VoteLog"), Player.displayName, i));
                            i++;
                        }
                    }                    
                    return;
                }
            }
            OpenMapVoteGrid(Player);
        }
        private void cmdVoteResult(IPlayer player, string cmd, string[] args)
        {
            if(VoteStarted) return;
            if((VoteResult!= null) && (!VoteResult.Any())) return;
            var keyOfMaxValue = VoteResult.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
            player.Reply($"Map Locked : seed : {keyOfMaxValue} Votes : {VoteResult[keyOfMaxValue]}");
            if (LogToDiscord && config.Settings.isDiscordBot)
                Log($"Map Locked : seed : {keyOfMaxValue} Votes : {VoteResult[keyOfMaxValue]}");
        }
        [Command("clearvotes")]
        private void cmdClearVotes(IPlayer player, string cmd, string[] args)
        {
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;

            if (IsAdmin(Player))
            {
                voteData = new VoteData();
                SaveData();
                server.Command("o.reload MapVoter");
            }
        }
        [Command("GUI.close")]
        private void cmdMapVoteClose(IPlayer player, string cmd, string[] args)
        {
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            CuiHelper.DestroyUi(Player, UI_MENU);
            CuiHelper.DestroyUi(Player, UI_POPUP);
        }
        [Command("Map.Generate")]
        private void cmdGenerateMap(IPlayer player, string cmd, string[] args)
        {
            
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;

            if (IsAdmin(Player))
            {
                configTEMP = config;
                CMapSize = config.Settings.size.ToString();
                StopVotingafter = config.Settings.Stopvote.ToString();
                AutoVoting = config.AutoVote.isAutoVote.ToString();
                enableautowipe = config._AutoWipe.isAutoWipe.ToString();
                _nbMap = config.AutoVote.nbm.ToString();
                _isCustomMap = config._AutoWipe._CustomMap.isCustomMap.ToString();
                bpswipe = config._AutoWipe.WipeBPs.ToString();
                isOnlyAuth = config.AutoVote.IsOnlyAuthUsers.ToString();
                _MapWipeSchedule = string.Join(",", config._AutoWipe.MWschedule.Select(x=> x.ToString()).ToArray());
                _FullWipeSchedule = string.Join(",", config._AutoWipe.FWSchedule.Select(x => x.ToString()).ToArray());
                OpenMapGenerator(Player);
            }
        }
        [Command("Map.CustomMaps")]
        private void cmdCustomMaps(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if (args.Length == 0)
            {
                OpenCustomMapSelect(Player);
                return;
            }
                
            if (args[0] == "add")
            {
                OpenCustomMapEditor(Player);
            }
        }
        [Command("StartVote")]
        private void cmdStartVote(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if (IsAdmin(Player))
            {
                voteData = new VoteData();
                VoteResult.Clear();
                if (!Maps.Any())
                {
                    if (args.Length == 0) 
                    {
                        player.Reply(GetMsg("args"));
                        return;
                    }
                    if (args.Length == 2)
                    {
                        if(!string.IsNullOrEmpty(args[1]))
                            size = args[1];
                    }
                    if (args.Length == 3)
                    {
                        if(!string.IsNullOrEmpty(args[2]))
                            _stopvote = args[2];
                    }
                    seedGenerate(Int16.Parse(args[0]));
                    return;
                }
                foreach(var map in Maps)
                {
                    VoteResult.Add(Convert.ToUInt32(map.Seed),0);
                }
                VoteStarted = true;
                if (config.Settings.isDiscordBot)
                    NotifyDiscord();
                NextTick(() =>
                {
                    foreach (var aplayer in players.Connected)
                    {
                        aplayer.Message(string.Format(GetMsg("Voting"), config.Commands.mapvote));
                    }
                });
                dotimer["VoteA"] = timer.Every(config.Settings.Stopvote * 60 / 4,() =>
                {
                    MapVoteA();
                }
                );
                int stopvote;
                if (args.Length != 0)
                {
                    if (!int.TryParse(args[0], out stopvote))
                        return;
                        
                }
                else
                    stopvote = config.Settings.Stopvote;
                dotimer["StopVoting"] = timer.Once(stopvote * 60,() =>
                {
                    StopVoting();
                }
                );        
            }
        }
        [Command("Map.Save")]
        private void cmdSaveConfig(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            SaveConfig();
            server.Command("o.reload MapVoter");
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            CuiHelper.DestroyUi(Player, UI_MENU);
            CuiHelper.DestroyUi(Player, UI_POPUP);
            OpenMapVoteGrid(Player);

        }
        [Command("Map.Savemap")]
        private void cmdSaveMap(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            BasePlayer Player = player.Object as BasePlayer;
            CustomMaps.Add(new CustomMap(mapname, downloadurl, cimageurl));
            CreateMenuPopup(Player, GetMsg("Saved"));
            RegisterImage(mapname, cimageurl);
            mapname = string.Empty;
            downloadurl = string.Empty;
            cimageurl = string.Empty;
            OpenCustomMapEditor(Player);
        }
        [Command("Map.GenerateNow")]
        private void ccmdGenerate(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if (string.IsNullOrEmpty(nbMaps))
            {
                CreateMenuPopup(Player, GetMsg("MapSave.Error.NoName"));
                return;
            }
            if (IsAdmin(Player))
            {
                seedGenerate(Int16.Parse(nbMaps));
                CuiHelper.DestroyUi(Player, UI_MENU);
                CuiHelper.DestroyUi(Player, UI_POPUP);
            }
            OpenMapVoteGrid(Player);
            CreateMenuPopup(Player,GetMsg("UI.Generating"));
            if (IsAdmin(Player))
                Player.SendConsoleCommand("chat.add", 0, 76561199214531833UL, GetMsg("UI.Generating"));
        }
        [Command("Mapvoter.reload")]
        private void ccmdClear(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            foreach (BasePlayer Player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(Player, UI_MENU);
                CuiHelper.DestroyUi(Player, UI_POPUP);
            }
            Reload();
        }
        [Command("Map.set")]
        private void ccmdSet(IPlayer player, string cmd, string[] args)
        {

            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            bool flag;
            bool canConvert;
            int x =0;
            if (args[0] == "mapname")
            {
                var val = string.Join(" ", args.Skip(1));
                mapname = (string)val;
                OpenCustomMapEditor(Player);
                return;
            }
            if (args[0] == "downloadurl")
            {
                var val = string.Join(" ", args.Skip(1));
                downloadurl = (string)val;
                OpenCustomMapEditor(Player);
                return;
            }
            if (args[0] == "imageurl")
            {
                var val = string.Join(" ", args.Skip(1));
                cimageurl = (string)val;
                OpenCustomMapEditor(Player);
                return;
            }
            if (args[0] == "nbMaps") 
            {
                var val = string.Join(" ", args.Skip(1));
                nbMaps = (string)val;
            }
            if (args[0] == "Size") 
            {
                var val = string.Join(" ", args.Skip(1));
                size = (string)val;
            }
            if (args[0] == "stopvote") 
            {
                var val = string.Join(" ", args.Skip(1));
                _stopvote = (string)val;
            }
            if (args[0] == "filterid")
            {
                var val = string.Join(" ", args.Skip(1));
                filterId = (string)val;
                return;
            }
            if (args[0] == "APIKey") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Settings.APIKey = (string)val;
            }
            if (args[0] == "CMapSize") 
            {
                var val = string.Join(" ", args.Skip(1));
                canConvert = Int32.TryParse((string)val,out x);
                if(canConvert) 
                {
                    CMapSize = (string)val;
                    configTEMP.Settings.size = x;
                }
                   
            }
            if (args[0] == "Cstopvote") 
            {
                var val = string.Join(" ", args.Skip(1));
                
                canConvert = Int32.TryParse((string)val,out x);
                if(canConvert) 
                {
                    StopVotingafter = (string)val;
                    configTEMP.Settings.Stopvote = x;
                }
            }
            if (args[0] == "votechannelid")
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP._DiscordSettings.Vote_Channel_id = (string)val;
            }
            if (args[0] == "avatar") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Settings.avatar_url = (string)val;
            }
            if (args[0] == "BOTKey")
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP._DiscordSettings.Discord_ApiKey = (string)val;
            }
            if (args[0] == "prefix")
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP._DiscordSettings.Commandprefix = (string)val;
            }
            if (args[0] == "footer") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Settings.Footer = (string)val;
            }
            if (args[0] == "filterid") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Settings.filterId = (string)val;
            }
            if (args[0] == "AutoStartVoting") 
            {
                var val = string.Join(" ", args.Skip(1));
                AutoVoting = (string)val;
                if (Boolean.TryParse(val, out flag))
                    configTEMP.AutoVote.isAutoVote = flag;
                else
                    configTEMP.AutoVote.isAutoVote = false;
            }
            if (args[0] == "isOnlyAuth") 
            {
                var val = string.Join(" ", args.Skip(1));
                isOnlyAuth = (string)val;
                if (Boolean.TryParse(val, out flag))
                    configTEMP.AutoVote.IsOnlyAuthUsers = flag;
                else
                    configTEMP.AutoVote.IsOnlyAuthUsers = false;
            }
            
            if (args[0] == "Startat") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.AutoVote.StartVotingat = (string)val;
            }
            if (args[0] == "nbmGenerate") 
            {
                var val = string.Join(" ", args.Skip(1));
                canConvert = Int32.TryParse((string)val,out x);
                if (canConvert)
                {
                    _nbMap = (string)val;
                    configTEMP.AutoVote.nbm = x;
                }
            }
            if (args[0] == "UICmd") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Commands.mapvote = (string)val;
            }
            if (args[0] == "GenerateCmd") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Commands.Generate = (string)val;
            }
            if (args[0] == "resultCmd") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP.Commands.voteresult = (string)val;
            }
            if (args[0] == "enableautowipe") 
            {
                var val = string.Join(" ", args.Skip(1));
                enableautowipe = (string)val;
                if (Boolean.TryParse(val, out flag))
                    configTEMP._AutoWipe.isAutoWipe = flag;
                else 
                    configTEMP._AutoWipe.isAutoWipe = false;
            }
            if (args[0] == "iscustommap") 
            {
                var val = string.Join(" ", args.Skip(1));
                _isCustomMap = (string)val;
                if (Boolean.TryParse(val, out flag))
                    configTEMP._AutoWipe._CustomMap.isCustomMap = flag;
                else 
                    configTEMP._AutoWipe._CustomMap.isCustomMap = false;
            }
            if (args[0] == "custommapurl") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP._AutoWipe._CustomMap.Url = (string)val;
            }
            if (args[0] == "wipebps") 
            {
                var val = string.Join(" ", args.Skip(1));
                bpswipe = (string)val;
                if (Boolean.TryParse(val, out flag))
                    configTEMP._AutoWipe.WipeBPs = flag;
                else 
                    configTEMP._AutoWipe.WipeBPs = false;
            }
            if (args[0] == "wipetime") 
            {
                var val = string.Join(" ", args.Skip(1));
                configTEMP._AutoWipe.WipeTime = (string)val;
            }
            if (args[0] == "mapwipeschedule")
            {
                int r;
                var val = string.Join(" ", args.Skip(1));
                _MapWipeSchedule = val;
                configTEMP._AutoWipe.MWschedule = _MapWipeSchedule.Split(',').Where(i => int.TryParse(i, out r)).Select(int.Parse).ToList();
            }
            if (args[0] == "fullwipeschedule")   
            {
                int r;
                var val = string.Join(" ", args.Skip(1));
                _FullWipeSchedule = val;
                configTEMP._AutoWipe.FWSchedule = _FullWipeSchedule.Split(',').Where(i => int.TryParse(i, out r)).Select(int.Parse).ToList();
            }

            OpenMapGenerator(Player);
        }
        [Command("Map.Textclear")]
        private void ccmdtextclear(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
           BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if (args[0] == "mapname")
            {
                mapname = string.Empty;
                OpenCustomMapEditor(Player);
                return;
            }
            if (args[0] == "downloadurl")
            {
                downloadurl = string.Empty;
                OpenCustomMapEditor(Player);
                return;
            }
            if (args[0] == "imageurl")
            {
                cimageurl = string.Empty;
                OpenCustomMapEditor(Player);
                return;
            }
            if (args[0] == "mapwipeschedule")
            {
                _MapWipeSchedule = string.Empty;
                configTEMP._AutoWipe.MWschedule.Clear();
            }
            if (args[0] == "fullwipeschedule")   
            {
                _FullWipeSchedule = string.Empty;
                configTEMP._AutoWipe.FWSchedule.Clear();
            }
            if (args[0] == "nbMaps") 
            {
                nbMaps = string.Empty;
            }
            if (args[0] == "Size") 
            {
                size = string.Empty;
            }
            if (args[0] == "stopvote") 
            {
                _stopvote = string.Empty;
            }
            if (args[0] == "filterid")
            {
                filterId = string.Empty;
                return;
            }
            if (args[0] == "APIKey") 
            {
                configTEMP.Settings.APIKey = string.Empty;
            }
            if (args[0] == "CMapSize") 
            {
                CMapSize = string.Empty;
                configTEMP.Settings.size = 3500;
            }
            if (args[0] == "Cstopvote") 
            {
                StopVotingafter = string.Empty;
                configTEMP.Settings.Stopvote = 60;
            }
            if (args[0] == "votechannelid")
            {
                configTEMP._DiscordSettings.Vote_Channel_id = string.Empty;
            }
            if (args[0] == "avatar") 
            {
                configTEMP.Settings.avatar_url = string.Empty;
            }
            if (args[0] == "footer") 
            {
                configTEMP.Settings.Footer = string.Empty;
            }
            if (args[0] == "filterid") 
            {
                configTEMP.Settings.filterId = string.Empty;
            }
            if (args[0] == "isOnlyAuth") 
            {
                isOnlyAuth = string.Empty;
                configTEMP.AutoVote.IsOnlyAuthUsers = false;
            }
            if (args[0] == "prefix")
            {
                configTEMP._DiscordSettings.Commandprefix = string.Empty;
            }
            if (args[0] == "AutoStartVoting") 
            {
                AutoVoting = string.Empty;
                configTEMP.AutoVote.isAutoVote = false;
            }
            if (args[0] == "Startat") 
            {
                configTEMP.AutoVote.StartVotingat = string.Empty;
            }
            if (args[0] == "nbmGenerate") 
            {
                _nbMap = string.Empty;
                configTEMP.AutoVote.nbm = 0;
            }
            if (args[0] == "UICmd") 
            {
                configTEMP.Commands.mapvote = string.Empty;
            }
            if (args[0] == "GenerateCmd") 
            {
                configTEMP.Commands.Generate = string.Empty;
            }
            if (args[0] == "resultCmd") 
            {
                configTEMP.Commands.voteresult = string.Empty;
            }
            if (args[0] == "enableautowipe") 
            {
                enableautowipe = string.Empty;
                configTEMP._AutoWipe.isAutoWipe = false;
            }
            if (args[0] == "BOTKey")
            {
                configTEMP._DiscordSettings.Discord_ApiKey = string.Empty;
            }
            if (args[0] == "iscustommap") 
            {
                _isCustomMap = string.Empty;
                configTEMP._AutoWipe._CustomMap.isCustomMap = false;
            }
            if (args[0] == "custommapurl") 
            {
                configTEMP._AutoWipe._CustomMap.Url = string.Empty;
            }
            if (args[0] == "wipebps") 
            {
                bpswipe = string.Empty;
                configTEMP._AutoWipe.WipeBPs = false;
            }
            if (args[0] == "wipetime") 
            {
                configTEMP._AutoWipe.WipeTime = string.Empty;
            }
            OpenMapGenerator(Player);
        }
        [Command("Maps.gridview")]
        private void cmdVotes(IPlayer player, string cmd, string[] args)
        {
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if(!IsAdmin(Player))
            {
                if (!VoteStarted)
                {
                    return;
                }
            }
            if(args[0] == "Vote")
            {
                if (config.Settings.isOnlyPlayersWithPermCanVote && !player.HasPermission(permissionVote))
                {
                    player.Reply(GetMsg("NoPermissionVote"));
                    return;
                }
                if (!VoteStarted)
                {
                    return;
                }
                if (Voted.Contains(Player.UserIDString))
                {
                    CreateMenuPopup(Player,GetMsg("UI.Voted"));
                    return;
                }
                int val;
                var key = Convert.ToUInt32(args[1]);
                if(VoteResult.TryGetValue(key,out val))
                {
                    VoteResult[key] = val + 1;
                }
                Voted.Add(Player.UserIDString);
                CuiHelper.DestroyUi(Player, UI_MENU);
                CuiHelper.DestroyUi(Player, UI_POPUP);
                player.Reply(GetMsg("VoteSuccess"));
                if (LogToDiscord && config.Settings.isDiscordBot)
                {
                    int i = 1;
                    foreach (var akey in VoteResult.Keys)
                    {
                        if (akey == key)
                            Log(string.Format(GetMsg("VoteLog"), Player.displayName, i));
                        i++;
                    }
                }
                OpenMapVoteGrid(Player);
            }
            if(args[0] == "page")
            {
                OpenMapVoteGrid(Player, Int16.Parse(args[1]), Convert.ToUInt64(args[2]));
                return;
            }
            if(args[0] == "inspect")
            {
                OpenMapVoteView(Player, args[1] , Int16.Parse(args[2]), Convert.ToUInt64(args[3]));                    
                return;
            }
        }
        
        [Command("Map.Select")]
        private void cmdselect(IPlayer player, string cmd, string[] args)
        {
            BasePlayer Player = player.Object as BasePlayer;
            if (Player == null)
                return;
            if(!IsAdmin(Player))
            {
                return;
            }
            if (args.Length == 0)
            {
                FiltredMaps.Clear();
                FMaps.Clear();

                CreateMenuPopup(Player,string.Format(GetMsg("UI.Loading"),filterId),2f);
                ServerMgr.Instance.StartCoroutine(GetRequest());
                timer.Once(2f, () =>
                {
                    OpenMapSelect(Player);
                });
                return;
            }
            if(args[0] == "select")
            {
                var map = FiltredMaps.Find(x => x.Seed == Convert.ToInt32(args[1]));
                if (map != null)
                {
                    if (!Maps.Contains(map))
                    {
                        CreateMenuPopup(Player, GetMsg("UI.Selected"),0.5f);
                        Maps.Add(map);
                    }
                }
                return;
            }
            if(args[0] == "page")
            {
                OpenMapSelectGrid(Player, Int16.Parse(args[1]), Convert.ToUInt64(args[2]));
                return;
            }
            if(args[0] == "inspect")
            {
                OpenMapSelectView(Player, args[1] , Int16.Parse(args[2]), Convert.ToUInt64(args[3]));                    
                return;
            }
            if (args[0] == "selectCustomMap")
            {
                var map = CustomMaps.Find(x => x.Name == string.Join(" ", args.Skip(1)));
                if (map != null)
                {
                    isCustomMaps = true;
                    Maps.Add(new RustMaps(map.Name, _random.Next(1, 2147483647), map.Url, map.Image));
                }
                   
                CreateMenuPopup(Player, GetMsg("UI.Selected"), 0.5f);
            }
        }
        [Command("MapVoter.cancelwipe")]
        private void cmdCancelWipe(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            server.Command("restart -1");
            WipeData = new AutoWipeConfig(false,0,0,DateTime.Now,false,false);
            SaveData();
            if (!dotimer.ContainsKey("StopVoting"))
                return;
            dotimer["StopVoting"].Destroy();
            dotimer.Remove("StopVoting");
            IsRestarting = false;
            WipeCanceled = true;
            Puts(GetMsg("interrupted"));
            VoteStarted = false;
            if (LogToDiscord && config.Settings.isDiscordBot)
                Log(GetMsg("interrupted"));
        }
        [Command("MapVoter.StopVoting")]
        private void cmdStopVoting(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(permissionUse))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            if (!dotimer.ContainsKey("StopVoting"))
                return;
            dotimer["StopVoting"].Destroy();
            dotimer.Remove("StopVoting");
            VoteStarted = false;
            StopVoting();
            VotingCanceled = true;
            if (LogToDiscord && config.Settings.isDiscordBot)
                Log(GetMsg("The vote has been stopped"));
        }

        [Command("MapVoter.update")]
        private void cmdServerUpdate(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            WipeData = new AutoWipeConfig(false,0,0,DateTime.Now,false,true);
            SaveData();
            if (args.Length == 0)
            {
                server.Command("restart");
                return;
            }
            int delta;
            bool isNumber = int.TryParse(args[0], out delta);
            if(!isNumber)
            {
                server.Command("restart");
                return;
            }
            server.Command("restart",delta);
            if (LogToDiscord && config.Settings.isDiscordBot)
                Log(GetMsg("Updating the server"));
        }
        [Command("MapVoter.CancelUpdate")]
        private void cmdCancelUpdate(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            WipeData = new AutoWipeConfig(false,0,0,DateTime.Now,false,false);
            SaveData();
            server.Command("restart -1");
            VoteStarted = false;
            Puts(GetMsg("interrupted"));
            if (LogToDiscord && config.Settings.isDiscordBot)
                Log(GetMsg("interrupted"));
        }
        [Command("MapVoter.mapwipe")]
        private void cmdwipe(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            if (args.Length == 0)
                return;
            bool canConvertDelta,canConvertMapSize;
            int x,delta,y;
            if (args.Length == 1)
            {
                canConvertDelta = Int32.TryParse(args[0],out delta);
                if (canConvertDelta)
                {
                    int seed = (int)_random.Next(1,2147483647);
                    WipeData = new AutoWipeConfig(true,seed,config.Settings.size,DateTime.Now,false,false);
                    SaveData();
                    server.Command("restart",delta);
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(string.Format("Wiping the server in {0} \n seed {1} \n size {2}", delta, seed, config.Settings.size));
                    return;
                }
            }
            if (args.Length == 2)
            {
                canConvertDelta = Int32.TryParse(args[0],out delta);
                canConvertMapSize= Int32.TryParse(args[1],out x);
                if (canConvertDelta && canConvertMapSize)
                {
                    int seed = (int)_random.Next(1,2147483647);
                    WipeData = new AutoWipeConfig(true,seed,x,DateTime.Now,false,false);
                    SaveData();
                    server.Command("restart",delta);
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(string.Format("Wiping the server in {0} \n seed {1} \n size {2}", delta, seed, x));
                    return;
                }
                Uri uriResult;
                bool result = Uri.TryCreate(args[1], UriKind.Absolute, out uriResult) 
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result && canConvertDelta)
                {
                    WipeData = new AutoWipeConfig(true,0,0,DateTime.Now,false,false,true,args[1]);
                    SaveData();
                    server.Command("restart",delta);
                }
                return;
            }
            if (args.Length == 3)
            {
                canConvertDelta = Int32.TryParse(args[0],out delta);
                canConvertMapSize= Int32.TryParse(args[1],out x);
                var canConvertMapSeed= Int32.TryParse(args[2],out y);
                if (canConvertDelta && canConvertMapSize && canConvertMapSeed)
                {
                    WipeData = new AutoWipeConfig(true,y,x,DateTime.Now,false,false);
                    SaveData();
                    server.Command("restart",delta);
                    return;
                }
                Puts(string.Format(GetMsg("SyntaxError"),canConvertDelta,canConvertMapSize,canConvertMapSeed));
                return;
            }
        }
        [Command("MapVoter.bpwipe")]
        private void cmdfullwipe(IPlayer player, string cmd, string[] args)
        {
            if (!player.HasPermission(PermissionManager))
            {
                player.Reply(GetMsg("NoPermission"));
                return;
            }
            if (args.Length == 0)
                return;
            bool canConvertDelta,canConvertMapSize = false;
            int x,delta,y;
            if (args.Length == 1)
            {
                canConvertDelta = Int32.TryParse(args[0],out delta);
                if (canConvertDelta)
                {
                    int seed = (int)_random.Next(1,2147483647);
                    WipeData = new AutoWipeConfig(true,seed,config.Settings.size,DateTime.Now,true,false);
                    SaveData();
                    server.Command("restart",delta);
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(string.Format("Wiping the server in {0} \n seed {1} \n size {2}", delta, seed, config.Settings.size));
                    return;
                }
                Puts(string.Format(GetMsg("SyntaxError1"),canConvertDelta,canConvertMapSize));
            }
            if (args.Length == 2)
            {
                canConvertDelta = Int32.TryParse(args[0],out delta);
                canConvertMapSize= Int32.TryParse(args[1],out x);
                if (canConvertDelta && canConvertMapSize)
                {
                    int seed = (int)_random.Next(1,2147483647);
                    WipeData = new AutoWipeConfig(true,seed,x,DateTime.Now,true,false);
                    SaveData();
                    server.Command("restart",delta);
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(string.Format("Wiping the server in {0} \n seed {1} \n size {2}", delta, seed, x));
                    return;
                }
                Uri uriResult;
                bool result = Uri.TryCreate(args[1], UriKind.Absolute, out uriResult) 
                    && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
                if (result && canConvertDelta)
                {
                    WipeData = new AutoWipeConfig(true,0,0,DateTime.Now,true,false,true,args[1]);
                    Debug.Log(WipeData.iSCustomMap + WipeData.LevelUrl);
                    SaveData();
                    server.Command("restart",delta);
                }
                Puts(string.Format(GetMsg("SyntaxError2"),canConvertDelta,canConvertMapSize,result));
                return;
            }
            if (args.Length == 3)
            {
                canConvertDelta = Int32.TryParse(args[0],out delta);
                canConvertMapSize= Int32.TryParse(args[1],out x);
                var canConvertMapSeed= Int32.TryParse(args[2],out y);
                if (canConvertDelta && canConvertMapSize && canConvertMapSeed)
                {
                    WipeData = new AutoWipeConfig(true,y,x,DateTime.Now,true,false);
                    SaveData();
                    server.Command("restart",delta);
                    return;
                }
                Puts(string.Format(GetMsg("SyntaxError"),canConvertDelta,canConvertMapSize,canConvertMapSeed));
                return;
            }
        }
        #endregion
        #region Map Grid View
        private const string UI_MENU = "Maps.menu";
        private const string UI_POPUP = "Maps.popup";
        private void OpenMapVoteGrid(BasePlayer player, int page = 0, ulong npcId = 0UL)
        {
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU,UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, GetMsg("GUI.Title"), 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), "GUI.close");
            if (IsAdmin(player) && npcId == 0UL)
            {
                UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("Custom Maps"), 14, new UI4(0.61f, 0.9375f, 0.7225f, 0.9825f), "Map.CustomMaps");
                UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("GUI.CreateNew"), 14, new UI4(0.85f, 0.9375f, 0.9525f, 0.9825f), "Map.Generate");
                UI.Button(container, UI_MENU, UI.Color("#38848b", 1f), GetMsg("GUI.SelectMaps"), 14, new UI4(0.73f, 0.9375f, 0.8425f, 0.9825f), "Map.Select");
                
            }           
            
            CreateGridView(player, container, page, npcId);

            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        private void OpenMapSelectGrid(BasePlayer player, int page = 0, ulong npcId = 0UL)
        {
            ulong npcID = 0UL;
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU,UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, GetMsg("GUI.Title"), 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

            //UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), "GUI.close");
            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcID}");
            if (IsAdmin(player) && npcId == 0UL)
            {
                UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("GUI.CreateNew"), 14, new UI4(0.80f, 0.9375f, 0.9295f, 0.9825f), "Map.Generate");
                UI.Button(container, UI_MENU, UI.Color("#38848b", 1f), GetMsg("GUI.SelectMaps"), 14, new UI4(0.68f, 0.9375f, 0.7925f, 0.9825f), "Map.Select");
            } 
            if (IsAdmin(player))
            {
                UI.Label(container, UI_MENU, "Filter id :", 14, new UI4(0.45f, 0.9375f, 0.5255f, 0.9825f), TextAnchor.MiddleLeft);
                UI.Panel(container, UI_MENU, UI.Color("#ffffff", 0.5f), new UI4(0.53f, 0.9375f, 0.7255f, 0.9825f));
                UI.Input(container, UI_MENU, string.Empty, 14, $"Map.set filterid ", new UI4(0.53f, 0.9375f, 0.7255f, 0.9825f), TextAnchor.MiddleLeft);
                UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON), 
                    new UI4(0.73f, 0.9375f, 0.7625f, 0.9825f), "Map.Select");
            }         
            
            CreateSelectView(player, container, page, npcId);

            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        string mapname, downloadurl, cimageurl = string.Empty;
        private bool IsAdmin(BasePlayer player) => permission.UserHasPermission(player.UserIDString, permissionUse);
        #endregion
        #region Map Editor
        private void OpenCustomMapEditor(BasePlayer player)
        {
            ulong npcId = 0UL;
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, GetMsg("New Map"), 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

            //UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), "GUI.close");
            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcId}");

            AddTitleSperator(container, 0, GetMsg("UI.cDetails"));
            AddInputField(container, 1, GetMsg("Name"), "mapname", mapname);
            AddInputField(container, 2, GetMsg("Download url (.map)"), "downloadurl", downloadurl);
            AddInputField(container, 3, GetMsg("Image url"), "imageurl", cimageurl);
            UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("Save"), 14, new UI4(0.28f, 0.7f, 0.495f, 0.74f), $"Map.Savemap");
            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        private void OpenCustomMapSelect(BasePlayer player, bool overwrite = false)
        {
            ulong npcId = 0UL;
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, GetMsg("Maps"), 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);
            UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("Add new map"), 14, new UI4(0.85f, 0.9375f, 0.9525f, 0.9825f), "Map.CustomMaps add");

            //UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), "GUI.close");
            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcId}");
            CreateCustomMapSelectView(player, container, 0, npcId);
            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        private void CreateCustomMapSelectView(BasePlayer player, CuiElementContainer container, int page = 0, ulong npcId = 0UL)
        {
            if (CustomMaps.Count == 0)
            {
                UI.Label(container, UI_MENU, GetMsg("UI.NoMapsAvailable"), 14, new UI4(0.015f, 0.88f, 0.99f, 0.92f), TextAnchor.MiddleLeft);
                return;
            }
            int max = Mathf.Min(CustomMaps.Count, (page + 1) * 8);
            int count = 0;
            for (int i = page * 8; i < max; i++)
            {
                CreateCustomMapSelectEntry(player, container, CustomMaps[i], count, page, npcId);
                count += 1;
            }

            if (page > 0)
                UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "◀\n\n◀\n\n◀", 16, new UI4(0.005f, 0.35f, 0.03f, 0.58f), $"Map.Select page {page - 1} {npcId}");
            if (max < FMaps.Count)
                UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "▶\n\n▶\n\n▶", 16, new UI4(0.97f, 0.35f, 0.995f, 0.58f), $"Map.Select page {page + 1} {npcId}");
        }
        void CreateCustomMapSelectEntry(BasePlayer player, CuiElementContainer container, CustomMap map, int index, int page, ulong npcId)
        {
            UI4 position = MapAlign.Get(index);

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));
            UI.Label(container, UI_MENU, map.Name, 14, new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), position);
            //UI.Label(container, UI_MENU, string.Format("Size : {0}", map.Size), 12,
                    //new UI4(position.xMin + 0.005f, position.yMin + 0.0475f, position.xMax - 0.005f, position.yMax - 0.3f), TextAnchor.MiddleLeft);
            var imageId = TGetImage(map.Name);
            //string imageId = GetImage(map.Seed.ToString());
            UI.Image(container, UI_MENU, imageId, new UI4(position.xMin + 0.005f, position.yMax - 0.3f, position.xMax - 0.005f, position.yMax - 0.0075f));

            string buttonText = GetMsg("UI.Select");
            string buttonCommand = $"Map.Select selectCustomMap {map.Name}";
            string buttonColor = UI.Color("#6a8b38", 1f);
            UI.Button(container, UI_MENU, buttonColor, buttonText, 14,
                new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
        }
        private void OpenMapGenerator(BasePlayer player , bool overwrite = false)
        {
            ulong npcId = 0UL;
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, GetMsg("UI.Title.Editor"), 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

            //UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), "GUI.close");
            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcId}");
            
            AddTitleSperator(container, 0, GetMsg("UI.Details"));
            AddInputField(container, 1, GetMsg("UI.nb"), "nbMaps", nbMaps);
            AddInputField(container, 2, GetMsg("UI.MapSize"), "Size", size);
            AddInputField(container, 3, GetMsg("UI.stopVoteTimer"), "stopvote", _stopvote);

            AddTitleSperator(container, 5, GetMsg("Options"));
            AddInputField(container, 6, GetMsg("RustMaps API key"), "APIKey", configTEMP.Settings.APIKey);
            AddInputField(container, 7, GetMsg("Map size"), "CMapSize", CMapSize);
            AddInputField(container, 8, GetMsg("Stop voting after (minutes)"), "Cstopvote", StopVotingafter);
            AddInputField(container, 9, GetMsg("Only Authanticated users can vote through discord"), "isOnlyAuth", isOnlyAuth);
            AddInputField(container, 10, GetMsg("Discord avatar url"), "avatar", configTEMP.Settings.avatar_url);
            AddInputField(container, 11, GetMsg("Discord footer"), "footer", configTEMP.Settings.Footer);
            AddInputField(container, 12, GetMsg("filter Id"), "filterid", configTEMP.Settings.filterId);

            AddTitleSperator(container, 13, GetMsg("Auto Vote"));
            AddInputField(container, 14, GetMsg("Auto start voting"), "AutoStartVoting", AutoVoting);
            AddInputField(container, 15, GetMsg("Start voting at (HH:mm) 24-hour clock (server local time)"), "Startat", configTEMP.AutoVote.StartVotingat);
            AddInputField(container, 16, GetMsg("Number of maps to generate"), "nbmGenerate", _nbMap);

            AddTitleSperatorTopRight(container, 0, GetMsg("Commands"));
            AddInputFieldRightSide(container, 1, GetMsg("Open MapVoter UI"), "UICmd", configTEMP.Commands.mapvote);
            AddInputFieldRightSide(container, 2, GetMsg("Generate Map(s)"), "GenerateCmd", configTEMP.Commands.Generate);
            AddInputFieldRightSide(container, 3, GetMsg("vote result"), "resultCmd", configTEMP.Commands.voteresult);

            AddTitleSperatorTopRight(container, 5, GetMsg("Auto Wipe"));
            AddInputFieldRightSide(container, 6, GetMsg("Enable Auto wipe"), "enableautowipe", enableautowipe);
            AddInputFieldRightSide(container, 7, GetMsg("Custom map"), "iscustommap", _isCustomMap);
            AddInputFieldRightSide(container, 8, GetMsg("Custom map URL"), "custommapurl", configTEMP._AutoWipe._CustomMap.Url);
            AddInputFieldRightSide(container, 9, GetMsg("Wipe BPs at forced wipe day"), "wipebps", bpswipe);
            AddInputFieldRightSide(container, 10, GetMsg("Wipe time (HH:mm) 24-hour clock (server local time)"), "wipetime", configTEMP._AutoWipe.WipeTime);
            AddInputFieldRightSide(container, 11, GetMsg("Map wipe schedule (Days since forced wipe)"), "mapwipeschedule", _MapWipeSchedule);
            AddInputFieldRightSide(container, 12, GetMsg("Full wipe schedule(Days since forced wipe)"), "fullwipeschedule", _FullWipeSchedule);
            
            AddTitleSperatorTopRight(container, 13, GetMsg("Discord Settings"));
            AddInputFieldRightSide(container, 14, GetMsg("Vote channel id"), "votechannelid", configTEMP._DiscordSettings.Vote_Channel_id);
            AddInputFieldRightSide(container, 15, GetMsg("Bot Apikey"), "BOTKey", configTEMP._DiscordSettings.Discord_ApiKey);
            AddInputFieldRightSide(container, 16, GetMsg("Discord Command Prefix"), "prefix", configTEMP._DiscordSettings.Commandprefix);

            //UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("UI.generate"), 14, new UI4(0.005f, 0.005f, 0.2475f, 0.045f), $"Map.Save {overwrite}");
            UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("UI.Save"), 14, new UI4(0.7525f, 0.005f, 0.995f, 0.045f), $"Map.Save {overwrite}");
            UI.Button(container, UI_MENU, UI.Color("#6a8b38", 1f), GetMsg("UI.generate"), 14, new UI4(0.28f, 0.7f, 0.495f, 0.74f), $"Map.GenerateNow {overwrite}");

            //UI.Panel(container, UI_MENU, UI.Color("#007acc", 1f), new UI4(0.505f, 0.88f, 0.995f, 0.92f));
            //UI.Label(container, UI_MENU, "test", 14, new UI4(0.51f, 0.88f, 0.995f, 0.92f), TextAnchor.MiddleLeft);
            //AddInputField(container, 4, GetMsg("UI.stopVoteTimer"), "OUTPUT", OUTPUT, 6);

            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        private void OpenMapSelect(BasePlayer player, bool overwrite = false)
        {
            ulong npcId = 1UL;
            ulong npcID = 0UL;
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 0.5f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, GetMsg("UI.Title.Editor"), 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

            //UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), "GUI.close");
            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcID}");
            if (IsAdmin(player))
            {
                UI.Label(container, UI_MENU, "Filter id :", 14, new UI4(0.45f, 0.9375f, 0.5255f, 0.9825f), TextAnchor.MiddleLeft);
                UI.Panel(container, UI_MENU, UI.Color("#ffffff", 0.5f), new UI4(0.53f, 0.9375f, 0.7255f, 0.9825f));
                UI.Input(container, UI_MENU, string.Empty, 14, $"Map.set filterid ", new UI4(0.53f, 0.9375f, 0.7255f, 0.9825f), TextAnchor.MiddleLeft);
                UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON), 
                    new UI4(0.73f, 0.9375f, 0.7625f, 0.9825f), "Map.Select");
                //UI.Button(container, UI_MENU, UI.Color("#38848b", 1f), GetMsg("GUI.SelectMaps"), 14, new UI4(0.73f, 0.9375f, 0.8425f, 0.9825f), "Map.Select");
            }
            CreateSelectView(player, container, 0, npcId);

            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        private void CreateSelectView(BasePlayer player, CuiElementContainer container, int page = 0, ulong npcId = 0UL)
        {
            if (config.Settings.filterId.Equals("Visit https://rustmaps.com/ and adjust your map requirements. In the red box above the settings hit the Share button,the string at the end of the URL is the filterId.Example URL: https://rustmaps.com/?share=gEU5W6BUuUG5FpPlyv2nhQ the string at the end in this case {gEU5W6BUuUG5FpPlyv2nhQ} is the filterId."))
            {
                
                UI.Label(container, UI_MENU, GetMsg("UI.NoMapsAvailable"), 14, new UI4(0.51f, 0.76f, 0.995f, 0.92f), TextAnchor.MiddleLeft);
                return;
            }
            
            if (FiltredMaps.Count == 0)
            {
                UI.Label(container, UI_MENU, GetMsg("UI.NoMapsAvailable"), 14, new UI4(0.015f, 0.88f, 0.99f, 0.92f), TextAnchor.MiddleLeft);
                return;
            }
            int max = Mathf.Min(FiltredMaps.Count, (page + 1) * 8);
            int count = 0;
            for (int i = page * 8; i < max; i++)                
            {
                CreateMapSelectEntry(player, container, FMaps[i], count, page, npcId);
                count += 1;
            }

            if (page > 0)
                UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "◀\n\n◀\n\n◀", 16, new UI4(0.005f, 0.35f, 0.03f, 0.58f), $"Map.Select page {page - 1} {npcId}");
            if (max < FiltredMaps.Count)
                UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "▶\n\n▶\n\n▶", 16, new UI4(0.97f, 0.35f, 0.995f, 0.58f), $"Map.Select page {page + 1} {npcId}");
            if (max == FiltredMaps.Count && !fMaps.LastPage)
            {
                RustMapsPage++;
                ServerMgr.Instance.StartCoroutine(GetRequest(RustMapsPage));
                CreateMenuPopup(player,string.Format(GetMsg("UI.Loading"),config.Settings.filterId),1f);
                timer.Once(2f, () =>
                {
                    OpenMapSelectGrid(player,page,npcId);
                });
            }
        }
        void CreateMapSelectEntry(BasePlayer player, CuiElementContainer container, Result map, int index, int page, ulong npcId)
        {
            int val = 0;
            UI4 position = MapAlign.Get(index);

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));
            UI.Label(container, UI_MENU, map.Seed.ToString(), 14, new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), position);
            UI.Label(container, UI_MENU, string.Format("Size : {0}",map.Size), 12,
                    new UI4(position.xMin + 0.005f, position.yMin + 0.0475f, position.xMax - 0.005f, position.yMax - 0.3f), TextAnchor.MiddleLeft);
            var imageId = TGetImage(map.Seed.ToString());
            //string imageId = GetImage(map.Seed.ToString());
            UI.Image(container, UI_MENU, imageId, new UI4(position.xMin + 0.005f, position.yMax - 0.3f, position.xMax - 0.005f, position.yMax - 0.0075f));

            //UI.Button(container, UI_MENU, "0 0 0 0", string.Empty, 0, new UI4(position.xMin + 0.005f, position.yMax - 0.3f, position.xMax - 0.005f, position.yMax - 0.0075f), $"Maps.gridview inspect {CommandSafe(map.Seed)} {page} {npcId}");
            
            var _map = FiltredMaps.Find(x => x.Id == map.Id);
            if  (_map != null)
            {
                if(MVData.Exists(x => x.Id == _map.Id))
                {
                    string buttonText = GetMsg("UI.Select");
                    string buttonCommand = $"Map.Select select {map.Seed}";
                    string buttonColor = UI.Color("#ff0000", 1f);
                    UI.Button(container, UI_MENU, buttonColor, buttonText, 14, 
                        new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
                    
                    UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON), 
                        new UI4(position.xMin + 0.005f, position.yMin + 0.0075f, position.xMin + 0.033f, position.yMin + 0.0475f), $"Map.Select inspect {map.Seed} {page} {npcId}");
                
                }else
                {
                    
                    string buttonText = GetMsg("UI.Select");
                    string buttonCommand = $"Map.Select select {map.Seed}";
                    string buttonColor = UI.Color("#6a8b38", 1f);
                    UI.Button(container, UI_MENU, buttonColor, buttonText, 14, 
                        new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
                    
                    UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON), 
                        new UI4(position.xMin + 0.005f, position.yMin + 0.0075f, position.xMin + 0.033f, position.yMin + 0.0475f), $"Map.Select inspect {map.Seed} {page} {npcId}");
                
                }
            }
            else
            {
                    
                string buttonText = GetMsg("UI.Select");
                string buttonCommand = $"Map.Select select {map.Seed}";
                string buttonColor = UI.Color("#6a8b38", 1f);
                UI.Button(container, UI_MENU, buttonColor, buttonText, 14, 
                    new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
                    
                UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON), 
                    new UI4(position.xMin + 0.005f, position.yMin + 0.0075f, position.xMin + 0.033f, position.yMin + 0.0475f), $"Map.Select inspect {map.Seed} {page} {npcId}");
               
            }
            
        }
        private void CreateGridView(BasePlayer player, CuiElementContainer container, int page = 0, ulong npcId = 0UL)
        {
            if (Maps.Count == 0)
            {
                UI.Label(container, UI_MENU, GetMsg("UI.NoMapsAvailable"), 14, new UI4(0.015f, 0.88f, 0.99f, 0.92f), TextAnchor.MiddleLeft);
                return;
            }

            int max = Mathf.Min(Maps.Count, (page + 1) * 8);
            int count = 0;
            for (int i = page * 8; i < max; i++)                
            {
                CreateMapVoteEntry(player, container, Maps[i], count, page, npcId);                
                count += 1;
            }

            if (page > 0)
                UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "◀\n\n◀\n\n◀", 16, new UI4(0.005f, 0.35f, 0.03f, 0.58f), $"Maps.gridview page {page - 1} {npcId}");
            if (max < Maps.Count)
                UI.Button(container, UI_MENU, UI.Color("#007acc", 1f), "▶\n\n▶\n\n▶", 16, new UI4(0.97f, 0.35f, 0.995f, 0.58f), $"Maps.gridview page {page + 1} {npcId}");
        }
        private const string DEFAULT_ICON = "Maps.magnifyicon";
        private const string MAGNIFY_ICON = "Maps.magnifyicon";
        private void CreateMapVoteEntry(BasePlayer player, CuiElementContainer container, RustMaps map, int index, int page, ulong npcId)
        {
            int val;
            UI4 position = MapAlign.Get(index);
            string imageId;
            if (!VoteResult.TryGetValue(Convert.ToUInt32(map.Seed), out val))
            {
                val = 0;
            }
            if (isCustomMaps)
            {
                
                UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));
                UI.Label(container, UI_MENU, map.Id, 14, new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));

                UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), position);
                UI.Label(container, UI_MENU, string.Format("Votes : {0}", val), 12,
                        new UI4(position.xMin + 0.005f, position.yMin + 0.0475f, position.xMax - 0.005f, position.yMax - 0.3f), TextAnchor.MiddleLeft);
                imageId = string.IsNullOrEmpty(map.ImageIconUrl) ? GetImage(DEFAULT_ICON) : GetImage(map.Id);
                UI.Image(container, UI_MENU, imageId, new UI4(position.xMin + 0.005f, position.yMax - 0.3f, position.xMax - 0.005f, position.yMax - 0.0075f));

                if (Voted.Contains(player.UserIDString))
                {
                    string buttonText = GetMsg("UI.Vote");
                    string buttonCommand = $"Maps.gridview Vote {map.Seed}";
                    string buttonColor = UI.Color("#ff0000", 1f);

                    UI.Button(container, UI_MENU, buttonColor, buttonText, 14,
                        new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
                }
                else
                {
                    string buttonText = GetMsg("UI.Vote");
                    string buttonCommand = $"Maps.gridview Vote {map.Seed}";
                    string buttonColor = UI.Color("#6a8b38", 1f);

                    UI.Button(container, UI_MENU, buttonColor, buttonText, 14,
                        new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
                }
                UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON),
                   new UI4(position.xMin + 0.005f, position.yMin + 0.0075f, position.xMin + 0.033f, position.yMin + 0.0475f), $"Maps.gridview inspect {map.Seed} {page} {npcId}");
                return;
            }

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));
            UI.Label(container, UI_MENU, map.Seed.ToString(), 14, new UI4(position.xMin, position.yMax, position.xMax, position.yMax + 0.04f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), position);
            UI.Label(container, UI_MENU, string.Format("Votes : {0}",val), 12,
                    new UI4(position.xMin + 0.005f, position.yMin + 0.0475f, position.xMax - 0.005f, position.yMax - 0.3f), TextAnchor.MiddleLeft);
            imageId = string.IsNullOrEmpty(map.ImageIconUrl) ? GetImage(DEFAULT_ICON) : GetImage(map.Seed.ToString());
            UI.Image(container, UI_MENU, imageId, new UI4(position.xMin + 0.005f, position.yMax - 0.3f, position.xMax - 0.005f, position.yMax - 0.0075f));
            
            //UI.Button(container, UI_MENU, "0 0 0 0", string.Empty, 0, new UI4(position.xMin + 0.005f, position.yMax - 0.3f, position.xMax - 0.005f, position.yMax - 0.0075f), $"Maps.gridview inspect {CommandSafe(map.Seed)} {page} {npcId}");
            if (Voted.Contains(player.UserIDString))
            {
                string buttonText = GetMsg("UI.Vote");
                string buttonCommand = $"Maps.gridview Vote {map.Seed}";
                string buttonColor = UI.Color("#ff0000", 1f);
                
                UI.Button(container, UI_MENU, buttonColor, buttonText, 14, 
                    new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
            }else{
            string buttonText = GetMsg("UI.Vote");
            string buttonCommand = $"Maps.gridview Vote {map.Seed}";
            string buttonColor = UI.Color("#6a8b38", 1f);
            
            UI.Button(container, UI_MENU, buttonColor, buttonText, 14, 
                new UI4(position.xMin + 0.038f, position.yMin + 0.0075f, position.xMax - 0.005f, position.yMin + 0.0475f), buttonCommand);
            }
            UI.ImageButton(container, UI_MENU, ICON_BACKGROUND_COLOR, GetImage(MAGNIFY_ICON), 
               new UI4(position.xMin + 0.005f, position.yMin + 0.0075f, position.xMin + 0.033f, position.yMin + 0.0475f), $"Maps.gridview inspect {map.Seed} {page} {npcId}");
        }
        #endregion
        #region ImageLibrary        
        private void RegisterImage(string name, string url) => ImageLibrary?.Call("AddImage", url, name.Replace(" ", ""), 0UL, null);

        private string GetImage(string name, ulong skinId = 0UL) => ImageLibrary?.Call<string>("GetImage", name.Replace(" ", ""), skinId, false);
        private string TGetImage(string name, ulong skinId = 0UL) => ImageLibrary?.Call<string>("GetImage", name.Replace(" ", ""), skinId, false);
        #endregion
        #region Popup Messages
        private void CreateMenuPopup(BasePlayer player, string text, float duration = 5f)
        {
            CuiElementContainer container = UI.Container(UI_POPUP, UI.Color("#d08822", 1f), new UI4(0.2f, 0.11f, 0.8f, 0.15f));
            UI.Label(container, UI_POPUP, text, 14, UI4.Full);

            CuiHelper.DestroyUi(player, UI_POPUP);
            CuiHelper.AddUi(player, container);

            player.Invoke(() => CuiHelper.DestroyUi(player, UI_POPUP), duration);
        }
        #endregion
        #region UI Grid Helper
        private readonly GridAlignment MapAlign = new GridAlignment(4, 0.04f, 0.2f, 0.04f, 0.87f, 0.39f, 0.06f);

        private readonly GridAlignment MainAlign = new GridAlignment(6, 0.545f, 0.065f, 0.0035f, 0.8275f, 0.1f, 0.005f);
        private readonly GridAlignment WearAlign = new GridAlignment(7, 0.51f, 0.065f, 0.0035f, 0.3575f, 0.1f, 0.005f);
        private readonly GridAlignment BeltAlign = new GridAlignment(6, 0.545f, 0.065f, 0.0035f, 0.2025f, 0.1f, 0.005f);

        private class GridAlignment
        {
            internal int Columns { get; set; }
            internal float XOffset { get; set; }
            internal float Width { get; set; }
            internal float XSpacing { get; set; }
            internal float YOffset { get; set; }
            internal float Height { get; set; }
            internal float YSpacing { get; set; }

            internal GridAlignment(int columns, float xOffset, float width, float xSpacing, float yOffset, float height, float ySpacing)
            {
                Columns = columns;
                XOffset = xOffset;
                Width = width;
                XSpacing = xSpacing;
                YOffset = yOffset;
                Height = height;
                YSpacing = ySpacing;
            }

            internal UI4 Get(int index)
            {
                int rowNumber = index == 0 ? 0 : Mathf.FloorToInt(index / Columns);
                int columnNumber = index - (rowNumber * Columns);

                float offsetX = XOffset + (Width * columnNumber) + (XSpacing * columnNumber);

                float offsetY = (YOffset - (rowNumber * Height) - (YSpacing * rowNumber));

                return new UI4(offsetX, offsetY - Height, offsetX + Width, offsetY);
            }
        }
        #endregion
        #region Map View       
        private void OpenMapVoteView(BasePlayer player, string name, int page, ulong npcId)
        {
            var map = Maps.Find(x => x.Seed == Convert.ToInt32(name));

            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));

            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));


            
            if (isCustomMaps)
            {
                UI.Label(container, UI_MENU, $"{GetMsg("Map")} - {map.Id}", 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);
                UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcId}");
                UI.Image(container, UI_MENU, GetImage(map.Id), new UI4(0.01f, 0.03f, 0.5f, 0.92f));

                AddTitleSperatorTopRight(container, 0, GetMsg("GUI.Details"));
                AddInputFieldRightSide(container, 1, GetMsg("Name"), "map.Id", map.Id);
            }
            else
            {
                UI.Label(container, UI_MENU, $"{GetMsg("UI.Title")} - {name}", 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);
                UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Maps.gridview page 0 {npcId}");
                UI.Image(container, UI_MENU, GetImage(map.Seed.ToString()), new UI4(0.01f, 0.03f, 0.5f, 0.92f));

                AddTitleSperatorTopRight(container, 0, GetMsg("GUI.Details"));
                AddInputFieldRightSide(container, 1, GetMsg("map.Id"), "map.Id", map.Id);
                AddInputFieldRightSide(container, 2, GetMsg("map.seed"), "map.seed", map.Seed);
                AddInputFieldRightSide(container, 3, GetMsg("map.size"), "map.size", map.Size);
            }
            

            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        private void OpenMapSelectView(BasePlayer player, string name, int page, ulong npcId)
        {
            var map = Maps.Find(x => x.Seed == Convert.ToInt32(name));
            CuiElementContainer container = UI.BlurContainer(UI_MENU, new UI4(0.2f, 0.15f, 0.8f, 0.85f));
            if (map == null)
            {
                var _map = FMaps.Find(x => x.Seed == Convert.ToInt32(name));

                UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

                UI.Label(container, UI_MENU, $"{GetMsg("UI.Title")} - {name}", 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

                UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Map.Select page 0 {npcId}");

                UI.Image(container, UI_MENU, GetImage(_map.Seed.ToString()), new UI4(0.01f, 0.03f, 0.5f, 0.92f));

                AddTitleSperatorTopRight(container, 0, GetMsg("GUI.Details"));
                AddInputFieldRightSide(container, 1, GetMsg("map.Id"), "map.Id", _map.Id);
                AddInputFieldRightSide(container, 2, GetMsg("map.seed"), "map.seed", _map.Seed);
                AddInputFieldRightSide(container, 3, GetMsg("map.size"), "map.size", _map.Size);

                CuiHelper.DestroyUi(player, UI_MENU);
                CuiHelper.AddUi(player, container);
                return;
            }
            UI.Panel(container, UI_MENU, UI.Color("#232323", 1f), new UI4(0.005f, 0.93f, 0.995f, 0.99f));

            UI.Label(container, UI_MENU, $"{GetMsg("UI.Title")} - {name}", 20, new UI4(0.015f, 0.93f, 0.99f, 0.99f), TextAnchor.MiddleLeft);

            UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "<b>×</b>", 20, new UI4(0.9575f, 0.9375f, 0.99f, 0.9825f), $"Map.Select page 0 {npcId}");

            UI.Image(container, UI_MENU, GetImage(map.Seed.ToString()), new UI4(0.01f, 0.03f, 0.5f, 0.92f));

            AddTitleSperatorTopRight(container, 0, GetMsg("GUI.Details"));
            AddInputFieldRightSide(container, 1, GetMsg("map.Id"), "map.Id", map.Id);
            AddInputFieldRightSide(container, 2, GetMsg("map.seed"), "map.seed", map.Seed);
            AddInputFieldRightSide(container, 3, GetMsg("map.size"), "map.size", map.Size);
                    
            CuiHelper.DestroyUi(player, UI_MENU);
            CuiHelper.AddUi(player, container);
        }
        #endregion
        #region UI Helper       
        public static class UI
        {
            public static CuiElementContainer Container(string panelName, string color, UI4 dimensions, string parent = "Overlay")
            {
                
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat" },
                            RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                            CursorEnabled = true
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
                return container;
            }

            public static CuiElementContainer BlurContainer(string panelName, UI4 dimensions, string color = "0 0 0 0.9")
            {
                CuiElementContainer container = new CuiElementContainer()
                {
                    {
                        new CuiPanel
                        {
                            Image = {Color = color, Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"},
                            RectTransform = {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax()},
                            CursorEnabled = true
                        },
                        new CuiElement().Parent = "Hud",
                        panelName.ToString()
                    }
                };
                return container;
            }

            public static CuiElementContainer Popup(string panelName, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter, string parent = "Overlay")
            {
                CuiElementContainer container = UI.Container(panelName, "0 0 0 0", dimensions);

                UI.Label(container, panelName, text, size, UI4.Full, align);

                return container;
            }

            public static void Panel(CuiElementContainer container, string panel, string color, UI4 dimensions)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = color },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);
            }

            public static void Label(CuiElementContainer container, string panel, string text, int size, UI4 dimensions, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiLabel
                {
                    Text = { FontSize = size, Align = align, Text = text },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                },
                panel);
            }

            public static void Button(CuiElementContainer container, string panel, string color, string text, int size, UI4 dimensions, string command, TextAnchor align = TextAnchor.MiddleCenter)
            {
                container.Add(new CuiButton
                {
                    Button = { Color = color, Command = command, FadeIn = 0f },
                    RectTransform = { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() },
                    Text = { Text = text, FontSize = size, Align = align }
                },
                panel);
            }

            public static void ImageButton(CuiElementContainer container, string panel, string color, string png, UI4 dimensions, string command)
            {
                UI.Panel(container, panel, color, dimensions);
                UI.Image(container, panel, png, dimensions);
                UI.Button(container, panel, "0 0 0 0", string.Empty, 0, dimensions, command);
            }

            public static void Input(CuiElementContainer container, string panel, string text, int size, string command, UI4 dimensions, TextAnchor anchor = TextAnchor.MiddleLeft)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = anchor,                            
                            CharsLimit = 300,
                            Command = command + text,
                            FontSize = size,
                            IsPassword = false,
                            Text = text
                        },
                        new CuiRectTransformComponent {AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                });
            }

            public static void Image(CuiElementContainer container, string panel, string png, UI4 dimensions)
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = panel,
                    Components =
                    {
                        new CuiRawImageComponent {Png = png },
                        new CuiRectTransformComponent { AnchorMin = dimensions.GetMin(), AnchorMax = dimensions.GetMax() }
                    }
                });
            }

            public static void Toggle(CuiElementContainer container, string panel, string boxColor, int fontSize, UI4 dimensions, string command, bool isOn)
            {
                UI.Panel(container, panel, boxColor, dimensions);

                if (isOn)
                    UI.Label(container, panel, "✔", fontSize, dimensions);

                UI.Button(container, panel, "0 0 0 0", string.Empty, 0, dimensions, command);
            }

            public static string Color(string hexColor, float alpha)
            {
                if (hexColor.StartsWith("#"))
                    hexColor = hexColor.TrimStart('#');

                int red = int.Parse(hexColor.Substring(0, 2), NumberStyles.AllowHexSpecifier);
                int green = int.Parse(hexColor.Substring(2, 2), NumberStyles.AllowHexSpecifier);
                int blue = int.Parse(hexColor.Substring(4, 2), NumberStyles.AllowHexSpecifier);

                return $"{(double)red / 255} {(double)green / 255} {(double)blue / 255} {alpha}";
            }
        }

        public class UI4
        {
            public float xMin, yMin, xMax, yMax;

            public UI4(float xMin, float yMin, float xMax, float yMax)
            {
                this.xMin = xMin;
                this.yMin = yMin;
                this.xMax = xMax;
                this.yMax = yMax;
            }

            public string GetMin() => $"{xMin} {yMin}";

            public string GetMax() => $"{xMax} {yMax}";

            private static UI4 _full;

            public static UI4 Full
            {
                get
                {
                    if (_full == null)
                        _full = new UI4(0, 0, 1, 1);
                    return _full;
                }
            }
        }
        #endregion
        #region Editor Helpers
        private const string ICON_BACKGROUND_COLOR = "1 1 1 0.15";
        private const float EDITOR_ELEMENT_HEIGHT = 0.04f;

        private void AddInputField(CuiElementContainer container, int index, string title, string fieldName, object currentValue, int additionalHeight = 0)
        {
            float yMin = GetVerticalPos(index, 0.88f);
            float yMax = yMin + EDITOR_ELEMENT_HEIGHT;

            if (additionalHeight != 0)
                yMin = GetVerticalPos(index + additionalHeight, 0.88f);

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(0.005f, yMin, 0.175f, yMax));
            UI.Label(container, UI_MENU, title, 12, new UI4(0.01f, yMin, 0.175f, yMax - 0.0075f), TextAnchor.UpperLeft);

            UI.Panel(container, UI_MENU, ICON_BACKGROUND_COLOR, new UI4(0.175f, yMin, 0.495f, yMax));

            string label = GetInputLabel(currentValue);
            if (!string.IsNullOrEmpty(label))
            {
                UI.Label(container, UI_MENU, label, 12, new UI4(0.18f, yMin, 0.47f, yMax - 0.0075f), TextAnchor.UpperLeft);
                UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "X", 14, new UI4(0.47f, yMax - EDITOR_ELEMENT_HEIGHT, 0.495f, yMax), $"Map.Textclear {fieldName}");
            }
            else UI.Input(container, UI_MENU, string.Empty, 12, $"Map.set {fieldName}", new UI4(0.18f, yMin, 0.495f, yMax - 0.0075f), TextAnchor.UpperLeft);
        }
        private void AddInputFieldRightSide(CuiElementContainer container, int index, string title, string fieldName, object currentValue, int additionalHeight = 0)
        {
            float yMin = GetVerticalPos(index, 0.88f);
            float yMax = yMin + EDITOR_ELEMENT_HEIGHT;
            
            if (additionalHeight != 0)
                yMin = GetVerticalPos(index + additionalHeight, 0.88f);

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(0.505f, yMin, 0.680f, yMax));
            UI.Label(container, UI_MENU, title, 12, new UI4(0.51f, yMin, 0.680f, yMax - 0.0075f), TextAnchor.UpperLeft);

            UI.Panel(container, UI_MENU, ICON_BACKGROUND_COLOR, new UI4(0.680f, yMin, 0.995f, yMax));

            string label = GetInputLabel(currentValue);
            if (!string.IsNullOrEmpty(label))
            {
                UI.Label(container, UI_MENU, label, 12, new UI4(0.69f, yMin, 0.97f, yMax - 0.0075f), TextAnchor.UpperLeft);
                UI.Button(container, UI_MENU, UI.Color("#d85540", 1f), "X", 14, new UI4(0.97f, yMax - EDITOR_ELEMENT_HEIGHT, 0.995f, yMax), $"Map.Textclear {fieldName}");
            }
            else UI.Input(container, UI_MENU, string.Empty, 12, $"Map.set {fieldName}", new UI4(0.69f, yMin, 0.995f, yMax - 0.0075f), TextAnchor.UpperLeft);
        }
        private void AddTitleSperator(CuiElementContainer container, int index, string title)
        {
            float yMin = GetVerticalPos(index, 0.88f);
            float yMax = yMin + EDITOR_ELEMENT_HEIGHT;

            UI.Panel(container, UI_MENU, UI.Color("#007acc", 1f), new UI4(0.005f, yMin, 0.495f, yMax));
            UI.Label(container, UI_MENU, title, 14, new UI4(0.01f, yMin, 0.495f, yMax), TextAnchor.MiddleLeft);
        }
        private void AddTitleSperatorTopRight(CuiElementContainer container, int index, string title)
        {
            float yMin = GetVerticalPos(index, 0.88f);
            float yMax = yMin + EDITOR_ELEMENT_HEIGHT;

            UI.Panel(container, UI_MENU, UI.Color("#007acc", 1f), new UI4(0.505f, yMin, 0.995f, yMax));
            UI.Label(container, UI_MENU, title, 14, new UI4(0.51f, yMin, 0.995f, yMax), TextAnchor.MiddleLeft);
        }
        private void AddLabelField(CuiElementContainer container, int index, string title, string value, int additionalHeight = 0)
        {
            float yMin = GetVerticalPos(index, 0.88f);
            float yMax = yMin + EDITOR_ELEMENT_HEIGHT;

            if (additionalHeight != 0)
                yMin = GetVerticalPos(index + additionalHeight, 0.88f);

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(0.005f, yMin, 0.175f, yMax));
            UI.Label(container, UI_MENU, title, 12, new UI4(0.01f, yMin, 0.175f, yMax - 0.0075f), TextAnchor.UpperLeft);

            UI.Panel(container, UI_MENU, ICON_BACKGROUND_COLOR, new UI4(0.175f, yMin, 0.495f, yMax));
            UI.Label(container, UI_MENU, value, 12, new UI4(0.18f, yMin, 0.495f, yMax - 0.0075f), TextAnchor.UpperLeft);
        }

        private void AddToggleField(CuiElementContainer container, int index, string title, string fieldName, bool currentValue)
        {
            float yMin = GetVerticalPos(index, 0.88f);
            float yMax = yMin + EDITOR_ELEMENT_HEIGHT;

            UI.Panel(container, UI_MENU, UI.Color("#d08822", 1f), new UI4(0.005f, yMin, 0.175f, yMax));
            UI.Label(container, UI_MENU, title, 14, new UI4(0.01f, yMin, 0.175f, yMax), TextAnchor.MiddleLeft);
            UI.Toggle(container, UI_MENU, ICON_BACKGROUND_COLOR, 14, new UI4(0.175f, yMin, 0.205f, yMax), $"kits.creator {fieldName} {!currentValue}", currentValue);
        }

        private string GetInputLabel(object obj)
        {
            if (obj is string)
                return string.IsNullOrEmpty(obj as string) ? null : obj.ToString();
            else if (obj is int)
                return (int)obj <= 0 ? null : obj.ToString();
            else if (obj is float)
                return (float)obj <= 0 ? null : obj.ToString();
            return null;
        }

        private float GetVerticalPos(int i, float start = 0.9f) => start - (i * (EDITOR_ELEMENT_HEIGHT + 0.005f));
        #endregion
        #region Methods
        public void InventoryStrip(BasePlayer player)
        {
            player.inventory.containerMain?.Clear();
            player.inventory.containerWear?.Clear();
            player.inventory.containerBelt?.Clear();
            ItemManager.DoRemoves();
        }
        private void seedGenerate(int nb)
        {
            Voted.Clear();
            Maps.Clear();
            VoteResult.Clear();
            Generating.Clear();
            for (int i =0; i < nb; i++)
            {
                uint value = (uint)_random.Next(1,2147483647);
                Generating.Add(value);
            }
            foreach(var seed in Generating)
            {
                ServerMgr.Instance.StartCoroutine(PostRequest(seed));
            }
        }
        private IEnumerator PostRequest(uint seed)
        {
            int MapSize;
            if (!String.IsNullOrEmpty(size))   
            {
                MapSize = Int16.Parse(size);
            }
            else 
                MapSize = config.Settings.size;
            if(MapSize == 0) MapSize = config.Settings.size;

            List<IMultipartFormSection> formData = new List<IMultipartFormSection>();
            string link = String.Format("https://rustmaps.com/api/v2/maps/{0}/{1}",seed.ToString(),MapSize.ToString());

            formData.Add(new MultipartFormDataSection("staging=false&barren=false"));
            UnityWebRequest www = UnityWebRequest.Post(link,formData);
            www.SetRequestHeader("X-API-Key",config.Settings.APIKey);
            www.SetRequestHeader("accept","application/json");
            yield return www.SendWebRequest();

            string response = System.Text.Encoding.UTF8.GetString(www.downloadHandler.data);
            Mapid Map = JsonConvert.DeserializeObject<Mapid>(www.downloadHandler.text);

            string mapId = Map.Id;

            if(www.responseCode == 409)
            {
                ServerMgr.Instance.StartCoroutine(GetRequest(String.Format("https://rustmaps.com/api/v2/maps/{0}",mapId)));
            }
            else if (www.responseCode == 200)
            {
                PrintWarning(GetMsg("UI.Generating"));
                PrintWarning(MapSize.ToString());
                if (config.Settings.isDiscordBot)
                    if (LogToDiscord)
                        Log(GetMsg("UI.Generating") +"\n"+ MapSize.ToString());
                timer.Once(60, () =>
                {
                    ServerMgr.Instance.StartCoroutine(GetRequest(String.Format("https://rustmaps.com/api/v2/maps/{0}",mapId),mapId));
                });
            }
        }
        private IEnumerator GetRequest(int page = 1)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get($"https://rustmaps.com/api/v2/maps/filter/{filterId}?page={page}"))
            {
                webRequest.SetRequestHeader("X-API-Key",config.Settings.APIKey);
                webRequest.SetRequestHeader("accept","application/json");
                yield return webRequest.SendWebRequest();
                if (webRequest.responseCode == 404)
                {
                    PrintWarning(string.Format("Error: {0}", webRequest.error));
                    webRequest.Dispose();
                    yield break;
                }
                if(webRequest.responseCode == 200)
                {
                    fMaps = JsonConvert.DeserializeObject<filteredMaps>(webRequest.downloadHandler.text);
                    foreach (var map in fMaps.Results)
                    {
                        if(!FMaps.Contains(map))
                        {
                            FMaps.Add(map);
                            ServerMgr.Instance.StartCoroutine(GetMapInfo($"https://rustmaps.com/api/v2/maps/{map.Id}"));
                        }
                            
                    }
                }
                webRequest.Dispose();
            }
        }
        private IEnumerator GetRequestByFilter(int page = 1)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get($"https://rustmaps.com/api/v2/maps/filter/{filterId}?page={page}"))
            {
                webRequest.SetRequestHeader("X-API-Key", config.Settings.APIKey);
                webRequest.SetRequestHeader("accept", "application/json");
                yield return webRequest.SendWebRequest();
                if (webRequest.responseCode == 404)
                {
                    PrintWarning(string.Format("Error: {0}", webRequest.error));
                    webRequest.Dispose();
                    yield break;
                }
                if (webRequest.responseCode == 200)
                {
                    fMaps = JsonConvert.DeserializeObject<filteredMaps>(webRequest.downloadHandler.text);
                    foreach (var map in fMaps.Results)
                    {
                        if (!FMaps.Contains(map))
                        {
                            FMaps.Add(map);
                        }
                    }
                }
                webRequest.Dispose();
            }
        }
        private IEnumerator GetMapInfo(string uri)
        {
             using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                webRequest.SetRequestHeader("X-API-Key",config.Settings.APIKey);
                webRequest.SetRequestHeader("accept","application/json");
                yield return webRequest.SendWebRequest();
                if (webRequest.responseCode == 404)
                {
                    PrintWarning(string.Format("Error: {0}", webRequest.error));
                    webRequest.Dispose();
                    yield break;
                }
                if(webRequest.responseCode == 200)
                {
                    RustMaps Map = JsonConvert.DeserializeObject<RustMaps>(webRequest.downloadHandler.text);
                    if (!FiltredMaps.Contains(Map))
                    {
                        FiltredMaps.Add(Map);
                        RegisterImage(Map.Seed.ToString(), Map.ImageIconUrl);
                    }
                }
                webRequest.Dispose();
            }
        }
        private IEnumerator GetRequest(string uri, string mapId = null)
        {
            using (UnityWebRequest webRequest = UnityWebRequest.Get(uri))
            {
                webRequest.SetRequestHeader("X-API-Key",config.Settings.APIKey);
                webRequest.SetRequestHeader("accept","application/json");
                yield return webRequest.SendWebRequest();
                if (webRequest.responseCode == 404)
                {
                    PrintWarning(string.Format("Error: {0}", webRequest.error));
                    webRequest.Dispose();
                    yield break;
                }
                if(webRequest.responseCode == 200)
                {
                    string response = System.Text.Encoding.UTF8.GetString(webRequest.downloadHandler.data);
                    RustMaps Map = JsonConvert.DeserializeObject<RustMaps>(webRequest.downloadHandler.text);
                    PrintWarning(Map.ImageIconUrl);
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(string.Format(GetMsg("CurrentState"), mapId, "ready"));
                    if (!MVData.Exists(x => x.Id == Map.Id))
                    {
                        Maps.Add(Map);
                        VoteResult.Add(Convert.ToUInt32(Map.Seed),0);
                        RegisterImage(Map.Seed.ToString(), Map.ImageIconUrl);
                    }
                    Generating.Remove(Convert.ToUInt32(Map.Seed));
                    if(Generating.Count == 0)
                    {
                        voteData = new VoteData(true, Voted, VoteResult, Maps);
                        VoteStarted = true;
                        if (config.Settings.isDiscordBot)
                            NotifyDiscord();
                        
                        int stopvote;
                        if (string.IsNullOrEmpty(_stopvote))
                        {
                            stopvote = config.Settings.Stopvote;
                        }
                        bool isNumber = int.TryParse(_stopvote, out stopvote);
                        if(!isNumber)
                        {
                            stopvote = config.Settings.Stopvote;
                        }
                        var daysTillNextWipe = (int)WipeInfoApi.Call("GetDaysTillWipe");
                        if (daysTillNextWipe == 0)
                        {
                            MapVoteA();
                            dotimer["VoteA"] = timer.Every(config.Settings.Stopvote * 60 / 4, () =>
                            {
                                MapVoteA();
                            });
                            var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                            if (stopvote > diffInMinutes)
                            {
                                stopvote = diffInMinutes - 10;
                            }
                            dotimer["StopVoting"] = timer.Once(stopvote * 60, () =>
                            {
                                StopVoting();
                            });
                        }
                    }
                }
                if(webRequest.responseCode == 409)
                {
                    CurrentState Map = JsonConvert.DeserializeObject<CurrentState>(webRequest.downloadHandler.text);
                    PrintWarning(string.Format(GetMsg("CurrentState"),mapId,Map.currentState));
                    if (config.Settings.isDiscordBot)
                        if (LogToDiscord)
                            Log(string.Format(GetMsg("CurrentState"), mapId, Map.currentState));
                    foreach (var Player in BasePlayer.activePlayerList)
                    {
                        if (IsAdmin(Player))
                            Player.SendConsoleCommand("chat.add", 0, 76561199214531833UL, string.Format(GetMsg("CurrentState"),mapId,Map.currentState));
                    }
                    timer.Once(60, () =>
                    {
                        ServerMgr.Instance.StartCoroutine(GetRequest(String.Format("https://rustmaps.com/api/v2/maps/{0}",mapId),mapId));
                    });
                }
                webRequest.Dispose();
            }
        }
        
        private void NotifyDiscord()
        {
            SendToDiscord(null, 1);
        }
        void SendToDiscord(RustMaps map = null, int i = 0, bool voteended = false)
        {
            var embeds = new List<DiscordEmbed>();
            MessageComponentBuilder builder = new MessageComponentBuilder();
            if (string.IsNullOrEmpty(config._DiscordSettings.Vote_Channel_id))
            {
                Puts(GetMsg("Error channel id"));
                return;
            }
            ulong chanid = Convert.ToUInt64(config._DiscordSettings.Vote_Channel_id);

            if (voteended)
            {
                var embed = new DiscordEmbed();
                embed.Title = ConVar.Server.hostname;
                embed.Description = string.Format(GetMsg("MapLocked"),i);
                embed.Color = new DiscordColor();
                embed.Color = DiscordColor.Gold;
                embed.Image = new EmbedImage(map.ImageIconUrl);
                embed.Author = new EmbedAuthor(config.Settings.Footer, config.Settings.avatar_url);
                embed.Footer = new EmbedFooter(config.Settings.Footer, config.Settings.avatar_url);
                if (isCustomMaps)
                {
                    embed.Fields = new List<EmbedField>
                    {
                        new EmbedField("Map ",$"{map.Id}" , true),
                        new EmbedField("Click & Connect", $"[steam://connect/{covalence.Server.Address}:{covalence.Server.Port}](steam://connect/{covalence.Server.Address}:{covalence.Server.Port})",false)
                    };
                }
                else
                {
                    embed.Fields = new List<EmbedField>
                    {
                        new EmbedField("Map Seed ",$"[{map.Seed.ToString()}](https://rustmaps.com/map/{map.Size.ToString()}_{map.Seed.ToString()})" , true),
                        new EmbedField("Map Size ", map.Size.ToString(), true),
                        new EmbedField("Click & Connect", $"[steam://connect/{covalence.Server.Address}:{covalence.Server.Port}](steam://connect/{covalence.Server.Address}:{covalence.Server.Port})",false)
                    };
                }
                embeds.Add(embed);

                MessageCreate create = new MessageCreate
                {
                    Content = "||@everyone||",
                    Embeds = embeds
                };
                GetChannel(_client, (Snowflake)chanid, chan =>
                {
                    chan.CreateMessage(_client, create);
                });
            }
            else
            {
                foreach (var amap in Maps)
                {
                    var embed = new DiscordEmbed();
                    embed.Title = ConVar.Server.hostname;
                    embed.Description = GetMsg("VoteStarted");
                    embed.Color = new DiscordColor();
                    embed.Color = DiscordColor.Gold;
                    embed.Image = new EmbedImage(amap.ImageIconUrl);
                    embed.Author = new EmbedAuthor(config.Settings.Footer, config.Settings.avatar_url);
                    embed.Footer = new EmbedFooter(config.Settings.Footer, config.Settings.avatar_url);
                    if (isCustomMaps)
                    {
                        embed.Fields = new List<EmbedField>
                        {
                            new EmbedField("Map",$"{amap.Id}" , true),
                            new EmbedField("To vote on this map click: ", $"Map {i}", true),
                            new EmbedField("Click & Connect", $"[steam://connect/{covalence.Server.Address}:{covalence.Server.Port}](steam://connect/{covalence.Server.Address}:{covalence.Server.Port})",false)
                        };
                    }
                    else
                    {
                        embed.Fields = new List<EmbedField>
                        {
                            new EmbedField("Map Seed ",$"[{amap.Seed.ToString()}](https://rustmaps.com/map/{amap.Size.ToString()}_{amap.Seed.ToString()})" , true),
                            new EmbedField("Map Size ", amap.Size.ToString(), true),
                            new EmbedField("To vote on this map click: ", $"Map {i}", true),
                            new EmbedField("Click & Connect", $"[steam://connect/{covalence.Server.Address}:{covalence.Server.Port}](steam://connect/{covalence.Server.Address}:{covalence.Server.Port})",false)
                        };
                    }
                    embeds.Add(embed);
                    builder.AddActionButton(ButtonStyle.Secondary, $"Map {i}", $"{VoteButtonId} {i}", false, DiscordEmoji.FromCharacter(VoteButtonIcon));
                    i++;
                }
                MessageCreate create = new MessageCreate
                {
                    Content = "||@everyone||",
                    Embeds = embeds,
                    Components = builder.Build()
                };
                GetChannel(_client, (Snowflake)chanid, chan =>
                {
                    chan.CreateMessage(_client, create);
                });
            }
        }
        private void MapVoteA()
        {
            NextTick(() =>
            {
                foreach (var aplayer in players.Connected)
                {
                    aplayer.Message(string.Format(GetMsg("Voting"), config.Commands.mapvote));
                }
            });
        }
        private void StopVoting()
        {
            if (dotimer.ContainsKey("VoteA"))
            {
                dotimer["VoteA"].Destroy();
                dotimer.Remove("VoteA");
            }
           
            //VoteStarted = false;
            voteData = new VoteData(false);
            int i = 1;
            var keyOfMaxValue = VoteResult.Aggregate((x, y) => x.Value > y.Value ? x : y).Key;
            PrintWarning(keyOfMaxValue.ToString());
            RustMaps selectedMap = Maps.Find(x => x.Seed == keyOfMaxValue);
            PrintWarning(selectedMap.Seed.ToString());
                
            try {
                MVData.Add(new RustMaps(selectedMap.Id, selectedMap.Staging, selectedMap.Seed,
                            selectedMap.Size, selectedMap.Monuments, selectedMap.Url, selectedMap.ImageUrl, selectedMap.ImageIconUrl, selectedMap.ThumbnailUrl));
            }
            catch (Exception E)
            {
                Puts(E.ToString());
            }
            if (isCustomMaps)
                WipeData = new AutoWipeConfig(IsWipeDay, 0, 0, DateTime.Now, IsFullWipe, ForcedWipe, true, selectedMap.Url);
            else
                WipeData = new AutoWipeConfig(IsWipeDay, selectedMap.Seed, selectedMap.Size, DateTime.Now, IsFullWipe, ForcedWipe);
            SaveData();
            foreach(var map in Maps)
            {
                if (map.Seed == keyOfMaxValue)
                {
                    if (config.Settings.isDiscordBot)
                        SendToDiscord(map,i,true);
                    PrintWarning($"Map Locked : \n id : {map.Id}, seed : {map.Seed}, Votes : {VoteResult[keyOfMaxValue]}");
                    
                    NextTick(() =>
                    {
                        foreach (var aplayer in players.Connected)
                        {
                            aplayer.Message(string.Format(GetMsg("MapLocked"), i));
                        }
                    });
                    foreach (BasePlayer player in BasePlayer.activePlayerList)
                    {
                        CuiHelper.DestroyUi(player, UI_MENU);
                        CuiHelper.DestroyUi(player, UI_POPUP);
                    }
                    break;
                }
                i++;
            }
            Voted.Clear();
            Maps.Clear();
            Generating.Clear();
        }
        private IEnumerator GetRandomMapsFromFilterID()
        {
            Debug.Log("Selecting Maps from RustMaps filter id: "+config.Settings.filterId + "This step usually takes around 1-30 seconds.");
            fMaps = new filteredMaps();
            Voted.Clear();
            VoteResult.Clear();
            Maps.Clear();
            FiltredMaps.Clear();
            int i = 1;
            while (!fMaps.LastPage && i < config.Settings.MaxPages)
            {
                yield return ServerMgr.Instance.StartCoroutine(GetRequestByFilter(i));
                i++;
            }
            int MapsPicked = 0;
            while (MapsPicked < config.AutoVote.nbm)
            {
                var index = _random.Next(0, FMaps.Count - 1);
                yield return ServerMgr.Instance.StartCoroutine(GetMapInfo($"https://rustmaps.com/api/v2/maps/{FMaps[index].Id}"));
                MapsPicked++;
            }
            for (int j = 0; j < FiltredMaps.Count; j++)
            {
                if (!Maps.Contains(FiltredMaps[j]))
                {
                    Maps.Add(FiltredMaps[j]);
                    VoteResult.TryAdd(Convert.ToUInt32(FiltredMaps[j].Seed), 0);
                }
            }
            voteData = new VoteData(true, Voted, VoteResult, Maps);
            VoteStarted = true;
            if (config.Settings.isDiscordBot)
                NotifyDiscord();

            int stopvote;
            if (string.IsNullOrEmpty(_stopvote))
            {
                stopvote = config.Settings.Stopvote;
            }
            bool isNumber = int.TryParse(_stopvote, out stopvote);
            if (!isNumber)
            {
                stopvote = config.Settings.Stopvote;
            }
            var daysTillNextWipe = (int)WipeInfoApi.Call("GetDaysTillWipe");
            if (daysTillNextWipe == 0)
            {
                MapVoteA();
                dotimer["VoteA"] = timer.Every(config.Settings.Stopvote * 60 / 4, () =>
                {
                    MapVoteA();
                });
                var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                if (stopvote > diffInMinutes)
                {
                    stopvote = diffInMinutes - 10;
                }
                dotimer["StopVoting"] = timer.Once(stopvote * 60, () =>
                {
                    StopVoting();
                });
            }
        }
        private bool autoStartVote(bool custommap = false)
        {
            if (config.AutoVote.isAutoVote)
            {
                if (WipeInfoApi == null)
                {
                    PrintWarning(GetMsg("WipeAPIerror"));
                    return false;
                }
                
                var daysTillNextWipe = (int)WipeInfoApi.Call("GetDaysTillWipe");
                var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                Debug.Log("Time zone of the current computer:" +TimeZone.CurrentTimeZone.StandardName.ToString() +" \nCurrent date time : "+ DateTime.Now);
                if (daysTillNextWipe == 0)
                    Debug.Log("WipeTime: " + DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null).AddDays(daysTillNextWipe).ToString() + " current time: " + DateTime.Now);
                if (daysTillNextWipe > 0 && daysTillNextWipe == config.AutoVote.StartVotingDaysBeforewipe && !voteData.isVoteDay && !VoteStarted)
                {
                    Debug.Log("WipeTime: " + DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null).AddDays(daysTillNextWipe).ToString() + " current time: " + DateTime.Now);
                    DateTime WDate = DateTime.ParseExact(config.AutoVote.StartVotingat, "HH:mm", null);
                    int result = DateTime.Compare(DateTime.Now, WDate);
                    if (result >= 0 && !custommap)
                    {
                        if (config.Settings.isRandomMapsFromfilterID)
                            ServerMgr.Instance.StartCoroutine(GetRandomMapsFromFilterID());
                        else
                            seedGenerate(config.AutoVote.nbm);
                        config.AutoVote.isAutoVote = false;
                        return false;
                    }
                    else if (result >= 0 && custommap && CustomMaps.Count > 0)
                    {
                        isCustomMaps = true;
                        foreach (var map in CustomMaps)
                        {
                            var seed = _random.Next(1, 2147483647);
                            Maps.Add(new RustMaps(map.Name, seed, map.Url, map.Image));
                            VoteResult.Add(Convert.ToUInt32(seed), 0);
                        }
                        voteData = new VoteData(true, Voted, VoteResult, Maps, true);
                        VoteStarted = true;
                        if (config.Settings.isDiscordBot)
                            NotifyDiscord();
                        return false;
                    }
                }
                if (daysTillNextWipe == 0 && diffInMinutes >= 0 && !VoteStarted && !voteData.isVoteDay && !dotimer.ContainsKey("StopVoting"))
                {
                    Debug.Log("WipeTime: " + DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null).AddDays(daysTillNextWipe).ToString() + " current time: " + DateTime.Now);
                    DateTime WDate = DateTime.ParseExact(config.AutoVote.StartVotingat, "HH:mm",null);
                    int result = DateTime.Compare(DateTime.Now, WDate);
                    if (result >= 0 && !custommap)
                    {
                        if (config.Settings.isRandomMapsFromfilterID)
                            ServerMgr.Instance.StartCoroutine(GetRandomMapsFromFilterID());
                        else
                            seedGenerate(config.AutoVote.nbm);
                        config.AutoVote.isAutoVote = false;
                        return true;
                    }
                    else if (custommap && CustomMaps.Count > 0)
                    {
                        isCustomMaps = true;
                        foreach (var map in CustomMaps)
                        {
                            var seed = _random.Next(1, 2147483647);
                            Maps.Add(new RustMaps(map.Name, seed, map.Url, map.Image));
                            VoteResult.Add(Convert.ToUInt32(seed), 0);
                        }
                        voteData = new VoteData(true, Voted, VoteResult, Maps, true);
                        VoteStarted = true;
                        if (config.Settings.isDiscordBot)
                            NotifyDiscord();
                        return true;
                    }
                }
                if (daysTillNextWipe == 0 && diffInMinutes >= 0 && diffInMinutes  <= 120 && VoteStarted && voteData.isVoteDay && !dotimer.ContainsKey("StopVoting"))
                {
                    Debug.Log("WipeTime: " + DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null).AddDays(daysTillNextWipe).ToString() + " current time: " + DateTime.Now);
                    DateTime WDate = DateTime.ParseExact(config.AutoVote.StartVotingat, "HH:mm", null);
                    int result = DateTime.Compare(DateTime.Now, WDate);
                    if (result >= 0)
                    {
                        int stopvote;
                        if (string.IsNullOrEmpty(_stopvote))
                        {
                            stopvote = config.Settings.Stopvote;
                        }
                        bool isNumber = int.TryParse(_stopvote, out stopvote);
                        if (!isNumber)
                        {
                            stopvote = config.Settings.Stopvote;
                        }
                        MapVoteA();
                        dotimer["VoteA"] = timer.Every(config.Settings.Stopvote * 60 / 4, () =>
                        {
                            MapVoteA();
                        });
                        if (stopvote > diffInMinutes)
                        {
                            stopvote = diffInMinutes - 10;
                        }
                        dotimer["StopVoting"] = timer.Once(stopvote * 60, () =>
                        {
                            StopVoting();
                        });
                        return true;
                    }
                }
            }
            return false;
        }
        #endregion
        #region Auto Wipe Methods
        int nbOxideVersionChecks = 0;
        private void OxideVersionCheck(int delta, bool callback = false)
        {
            if (nbOxideVersionChecks > 2)
                return;
            webrequest.Enqueue("https://umod.org/games/rust.json", null,
                (responseCode, json) => {
                    if (responseCode != 200)
                    {
                        Puts($"Failed to fetch latest Oxide.Rust version from uMod.org API - code {responseCode}");
                    }
                    else
                    {
                        try
                        {
                            var response = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);

                            if (response == null)
                            {
                                throw new Exception("Response is null");
                            }

                            var latestVersionStr = response["latest_release_version"];

                            var array = ((string)latestVersionStr).Split('.');

                            var version = new VersionNumber(int.Parse(array[0]), int.Parse(array[1]), int.Parse(array[2]));
                            if (version > CurrentOxideRustVersion && IsForcedWipeDay())
                            {
                                Puts(string.Format(GetMsg("NewOxideVersion"),CurrentOxideRustVersion,version));
                                server.Command("restart",60);
                                return;
                            }else if (version > CurrentOxideRustVersion)
                            {
                                Puts(string.Format(GetMsg("NewOxideVersion"),CurrentOxideRustVersion,version));
                                if (config.Settings.isDiscordBot)
                                    if (LogToDiscord)
                                        Log(string.Format(GetMsg("NewOxideVersion"), CurrentOxideRustVersion, version));
                                return;
                            }
                            nbOxideVersionChecks++;
                            delta = delta - 1;
                            if (delta <= 0)
                                delta = 1;
                            if (callback ==  false)
                            {
                                Puts(string.Format(GetMsg("OxideUpToDate"),delta * 60));
                                if (config.Settings.isDiscordBot)
                                    if (LogToDiscord)
                                        Log(string.Format(GetMsg("OxideUpToDate"), delta * 60));
                                dotimer["OxideVersionCheck"] = timer.Once(delta * 60 , () => 
                                {
                                    server.Command("restart",120);
                                    OxideVersionCheck((int)(DateTime.ParseExact(config._AutoWipe.ForcedWipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes,true);
                                });
                            }else{
                                 Puts(string.Format(GetMsg("OxideUpToDate"),delta * 60));
                                dotimer["OxideVersionCheck"] = timer.Once(delta * 60 , () => 
                                {
                                    server.Command("restart",120);
                                   OxideVersionCheck((int)(DateTime.ParseExact(config._AutoWipe.ForcedWipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes,true);
                                });
                            }
                               
                        }
                        catch (Exception e)
                        {
                            Puts(e.ToString());
                        }
                    }
                }, this);
        }
        private void CheckIfitsWipeDay()
        {
            if (config.funKit.enabled && !isFunKitEnabled)
            {
                var daysTillNextWipe = (int)WipeInfoApi.Call("GetDaysTillWipe");
                var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                if (daysTillNextWipe == 0 && diffInMinutes >= 0 && diffInMinutes <= config.funKit.KitStartTime)
                {
                    Debug.Log("Fun kit");
                    Subscribe(nameof(OnItemAction));
                    Subscribe(nameof(CanUnlockTechTreeNode));
                    Subscribe(nameof(OnPlayerRespawned));
                    Subscribe(nameof(OnPlayerConnected));
                    isFunKitEnabled = true;
                    server.Command($"oxide.grant group default {config.funKit.Perm}");
                    server.Command($"global.say Fun kit is enabled /kit {config.funKit.KitName}.");
                }
            }
            if (config._AutoWipe.isAutoWipe && config._AutoWipe._CustomMap.isCustomMap)
            {
                var isautovote = autoStartVote(true);
                if (isautovote)
                {
                    var daysTillNextWipe = (int)WipeInfoApi.Call("GetDaysTillWipe");
                    var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;

                    if (daysTillNextWipe == 0 && diffInMinutes >= 0 && diffInMinutes <= 120)
                    {
                        server.Command("restart", diffInMinutes * 60);
                        IsRestarting = true;
                        IsFullWipe = GetIsFullWipe();
                        IsWipeDay = true;
                        Puts(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                        if (LogToDiscord && config.Settings.isDiscordBot)
                            Log(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                        if (CustomMaps.Count == 0 && config._AutoWipe._CustomMap.Url != "")
                            WipeData = new AutoWipeConfig(IsWipeDay, 0, 0, DateTime.Now, IsFullWipe, ForcedWipe, config._AutoWipe._CustomMap.isCustomMap, config._AutoWipe._CustomMap.Url);

                        SaveData();
                    }
                }
                return;
            }
            var isAutoVote = autoStartVote();
            if (IsForcedWipeDay() && isAutoVote && config._AutoWipe.isAutoWipe)
            {
                var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.ForcedWipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                
                if (diffInMinutes >= 0 && diffInMinutes <= 120)
                {
                    server.Command("restart",diffInMinutes * 60);
                    IsRestarting = true;
                    
                    IsFullWipe = GetIsFullWipe();
                    ForcedWipe = true;
                    IsWipeDay = true;
                    Puts(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                    if (!dotimer.ContainsKey("OxideVersionCheck"))
                        OxideVersionCheck(diffInMinutes);
                }
                return;
            }
            if (isAutoVote && config._AutoWipe.isAutoWipe)
            {
                PrintWarning("Auto Starting Map vote.");
                var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.WipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                
                if (diffInMinutes >= 0 && diffInMinutes <= 120)
                {
                    server.Command("restart",diffInMinutes * 60);
                    
                    IsFullWipe = GetIsFullWipe();
                    IsWipeDay = true;
                    Puts(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                }
                return;
            }
            if (IsForcedWipeDay() && config._AutoWipe.isAutoWipe)
            {
                var diffInMinutes = (int)(DateTime.ParseExact(config._AutoWipe.ForcedWipeTime, "HH:mm", null) - DateTime.Now).TotalMinutes;
                if (diffInMinutes >= 0 && diffInMinutes <= 120)
                {
                    if (diffInMinutes >= 0 && !VoteStarted && !voteData.isVoteDay && !dotimer.ContainsKey("StopVoting"))
                    {
                        config.Settings.Stopvote = diffInMinutes - 15;
                        seedGenerate(config.AutoVote.nbm);
                        config.AutoVote.isAutoVote = false;
                    }
                    server.Command("restart",diffInMinutes * 60 );
                    IsRestarting = true;

                    IsFullWipe = GetIsFullWipe();
                    ForcedWipe = true;
                    IsWipeDay = true;
                    Puts(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                    if (LogToDiscord && config.Settings.isDiscordBot)
                        Log(String.Format(GetMsg("AutoWiping"), diffInMinutes.ToString(), IsFullWipe.ToString(), ForcedWipe.ToString()));
                    if (!dotimer.ContainsKey("OxideVersionCheck"))
                        OxideVersionCheck(diffInMinutes);
                }
                return;
            }
        }
        private void GetWipeSchedule()
        {
            DateTime CurrentWipe = wipeinfo.PreviousWipe;
            Debug.Log("Current wipe: " + CurrentWipe.Date);
            DateTime lastForcedWipe = (DateTime)WipeInfoApi.Call("GetForcedWipe", CurrentWipe);
            TimeSpan time = TimeSpan.Parse(config._AutoWipe.WipeTime);
            TimeSpan Forcedwipetime = TimeSpan.Parse(config._AutoWipe.ForcedWipeTime);
            foreach (var wipe in config._AutoWipe.MWschedule)
            {
                if (lastForcedWipe.AddDays(wipe) > CurrentWipe)
                    Debug.Log("Next map wipe at: "+ lastForcedWipe.AddDays(wipe).ToShortDateString() + " " + time);
            }
            foreach (var wipe in config._AutoWipe.FWSchedule)
            { 
                if (lastForcedWipe.AddDays(wipe) > CurrentWipe)
                    Debug.Log("Next full wipe at: " + lastForcedWipe.Date.AddDays(wipe).ToShortDateString() +" "+ time);
            }
        }
        private bool GetIsFullWipe()
        {
            if (WipeInfoApi == null)
            {
                PrintWarning(GetMsg("WipeAPIerror"));
                return false;
            }
            if (SaveRestore.SaveCreatedTime > wipeinfo.PreviousWipe)
            {
                wipeinfo.PreviousWipe = SaveRestore.SaveCreatedTime.Date;
                NextTick(SaveWipeinfoData);
            }
            DateTime CurrentWipe = wipeinfo.PreviousWipe;
            DateTime lastForcedWipe = (DateTime)WipeInfoApi.Call("GetForcedWipe",CurrentWipe);
            DateTime nextForcedWipe = (DateTime)WipeInfoApi.Call("GetForcedWipe",CurrentWipe.AddMonths(1));
            int DaysSinceForcedWipe = (int)(DateTime.Now - lastForcedWipe).TotalDays;
            int DaysTillNextWipe = (int)(DateTime.Now - nextForcedWipe).TotalDays;
            if (DaysTillNextWipe == 0 && config._AutoWipe.FWSchedule.Contains(DaysTillNextWipe))
                return true;
            if (config._AutoWipe.FWSchedule.Contains(DaysSinceForcedWipe) && (int)WipeInfoApi.Call("GetDaysTillWipe") == 0)
                return true;
            return false;
        }
        Wipeinfo wipeinfo;
        
        private bool IsForcedWipeDay()
        {
            if (WipeInfoApi == null)
            {
                PrintWarning(GetMsg("WipeAPIerror"));
                return false;
            }
            if (SaveRestore.SaveCreatedTime > wipeinfo.PreviousWipe)
            {
                wipeinfo.PreviousWipe = SaveRestore.SaveCreatedTime;
                NextTick(SaveWipeinfoData);
            }
            var CurrentDate = DateTime.Now.Date;
            DateTime CurrentWipe = wipeinfo.PreviousWipe;
            DateTime lastForcedWipe = (DateTime)WipeInfoApi.Call("GetForcedWipe",CurrentWipe);
            DateTime NextForcedWipe = (DateTime)WipeInfoApi.Call("GetForcedWipe",CurrentWipe.AddMonths(1));
            if (CurrentWipe.Month != lastForcedWipe.Month)
            {
                return lastForcedWipe.Date == CurrentDate.Date && !(bool)WipeInfoApi.Call("IsNewSaveVersion");
            }
            else if (CurrentWipe.Month == lastForcedWipe.Month && CurrentWipe <= lastForcedWipe) 
            {
                return lastForcedWipe.Date == CurrentDate.Date && !(bool)WipeInfoApi.Call("IsNewSaveVersion");
            }
                
            else
                return NextForcedWipe.Date == CurrentDate.Date && !(bool)WipeInfoApi.Call("IsNewSaveVersion");
        }
        private void SaveWipeinfoData()
        {
            Interface.Oxide.DataFileSystem.WriteObject("MapVoter/wipeinfo", wipeinfo);
        }
        private class Wipeinfo
        {
            public DateTime PreviousWipe { get; set; } = DateTime.MinValue;
        }
        #endregion
        #region Discord
        private void OnDiscordClientCreated()
        {
            if (string.IsNullOrEmpty(config._DiscordSettings.Discord_ApiKey) || config._DiscordSettings.Discord_ApiKey == null || config._DiscordSettings.Discord_ApiKey == "BotToken")
            {
                PrintError("API key is empty or invalid!");
                return;
            }
            if (!config.Settings.isDiscordBot)
                return;
            //bool flag = true;
            try
            {
                DiscordSettings settings = new DiscordSettings
                {
                    ApiToken = config._DiscordSettings.Discord_ApiKey,
                    Intents = GatewayIntents.Guilds | GatewayIntents.GuildMembers | GatewayIntents.GuildMessages | GatewayIntents.DirectMessages,
                };
                _client.Connect(settings);
            }
            catch (Exception e)
            {
                //flag = false;
                PrintError($"MapVoter failed to create client! Exception message: {e}");
            }
        }
        void OnDiscordGatewayReady(GatewayReadyEvent rdy)
        {
            _botId = rdy.User.Id;
            _channelCount = config._DiscordSettings.Channels.Count;
        }

        void OnDiscordGuildCreated(DiscordGuild newguild)
        {
            for (int i = 0; i < _channelCount; i++)
            {
                GetChannel(_client, config._DiscordSettings.Channels[i].ChannelId, c =>
                {
                    c.CreateMessage(_client, "MapVoter Initialized!");
                }, newguild.Id);
            }

        }
        private void OnDiscordInteractionCreated(DiscordInteraction interaction)
        {
            if (interaction.Type != InteractionType.MessageComponent)
            {
                return;
            }

            if (!interaction.Data.ComponentType.HasValue || interaction.Data.ComponentType.Value != MessageComponentType.Button)
            {
                return;
            }

            string[] args = interaction.Data.CustomId.Split(' ');

            switch (args[0])
            {
                case VoteButtonId:
                    HandleVoteButton(interaction);
                    break;
            }
        }
        void HandleVoteButton(DiscordInteraction interaction)
        {
            string[] args = interaction.Data.CustomId.Split(' ');
            ProssesDiscordVote(interaction.Member.User, args, true, interaction);
        }
        private void OnDiscordGuildMessageCreated(DiscordMessage message)
        {
            if ((message.Content?.Length ?? 0) == 0) return;
            ConfigData.DiscordChannel channelidx = FindChannelById(message.ChannelId);
            if (channelidx == null)
                return;
            if (message.Author.Id == _botId) return;
            if (message.Content[0] == config._DiscordSettings.Commandprefix[0])
            {
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
                if (!config._DiscordSettings.CmdRoles.ContainsKey(cmd))
                {
                    DiscordToGameCmd(cmd, msg, message.Author, message.ChannelId);
                    return;
                }
                var roles = config._DiscordSettings.CmdRoles[cmd];
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

        }
        private void DiscordToGameCmd(string command, string param, DiscordUser author, Snowflake channelid)
        {
            switch (command)
            {
                case "generate":
                    {
                        if (String.IsNullOrEmpty(param))
                        {
                            GetChannel(_client, channelid, chan =>
                            {
                                chan.CreateMessage(_client, "Syntax: !generate <Number of map> <size>");
                            });
                            return;
                        }
                        string[] _param = param.Split(' ');
                        if (_param.Length >= 1)
                        {
                            server.Command($"MapVoter.generate {string.Join(" ", _param)}");
                        }
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Generating");
                        });
                        break;
                    }
                case "vote":
                    {
                        string[] _param = param.Split(' ');
                        if (_param.Length == 1)
                        {
                            ProssesDiscordVote(author,_param);
                        }
                        break;
                    }
                case "mapwipe":
                {
                    string[] _param = param.Split(' ');
                    if (String.IsNullOrEmpty(param) || _param.Length > 3)
                    {
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Syntax: !mapwipe <delay in seconds> <Map size>, !mapwipe <delay in seconds> <Map size> <Map seed> or  <delay in seconds> <Custom map url>");
                        });
                        return;
                    }
                    server.Command($"MapVoter.mapwipe {string.Join(" ", _param)}");
                    GetChannel(_client, channelid, chan =>
                    {
                        chan.CreateMessage(_client, string.Format(GetMsg("Restarting"),_param[0]));
                    });
                    break;
                }
                case "bpwipe":
                {
                    string[] _param = param.Split(' ');
                    if (String.IsNullOrEmpty(param) || _param.Length != 2)
                    {
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Syntax: !bpwipe <delay in seconds> <Map size> or <delay in seconds> <Custom map url>");
                        });
                        return;
                    }
                    if (_param.Length == 2)
                    {
                        server.Command($"MapVoter.bpwipe {string.Join(" ", _param)}");
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, string.Format(GetMsg("Restarting"),_param[0]));
                        });
                    }
                    break;
                }
                case "cancelwipe":
                {
                    server.Command($"MapVoter.cancelwipe");
                    GetChannel(_client, channelid, chan =>
                    {
                        chan.CreateMessage(_client, GetMsg("interrupted"));
                    });
                    break;
                }
                case "stopvoting":
                {
                    server.Command("MapVoter.stopvoting");
                    break;
                }
                case "update":
                {
                    string[] _param = param.Split(' ');
                    if (String.IsNullOrEmpty(param) || _param.Length != 1)
                    {
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, "Syntax: !update <Delay in seconds>");
                        });
                        return;
                    }
                    if (_param.Length == 1)
                    {
                        server.Command($"MapVoter.update {string.Join(" ", _param)}");
                        GetChannel(_client, channelid, chan =>
                        {
                            chan.CreateMessage(_client, string.Format(GetMsg("Restarting"),_param[0]));
                        });
                    }
                    break;
                }
                case "cancelupdate":
                {
                    server.Command($"Mapvoter.CancelUpdate");
                    GetChannel(_client, channelid, chan =>
                    {
                        chan.CreateMessage(_client, GetMsg("interrupted"));
                    });
                    break;
                }
            }
        }
        private void ProssesDiscordVote(DiscordUser author, string[] args, bool fromButton = false, DiscordInteraction interaction = null)
        {
            if (author == null)
                return;
            if (!VoteStarted)
            {
                interaction.CreateInteractionResponse(_client, new InteractionResponse
                {
                    Type = InteractionResponseType.ChannelMessageWithSource,
                    Data = new InteractionCallbackData
                    {
                        Content = GetMsg("UI.Votenotstarted"),
                        Flags = MessageFlags.Ephemeral
                    }
                });
                return;
            }
            if (args.Length < 1)
            {
                author.SendDirectMessage(_client, "Syntax: !vote <map id>");
                return;
            }
            int val;
            int i;
            uint key;
            bool isNumber;
            if (fromButton)
            {
                isNumber = int.TryParse(args[1], out i);
            }
            else
            {
                isNumber = int.TryParse(args[0], out i);
            }

            if (i - 1 > VoteResult.Count || i - 1 < 0 || !isNumber)
            {
                author.SendDirectMessage(_client, "Syntax: !vote <map id>");
                return;
            }
            if (!config.AutoVote.IsOnlyAuthUsers)
            {
                if (config.Settings.isOnlyPlayersWithPermCanVote)
                {
                    if (interaction == null)
                    {
                        author.SendDirectMessage(_client, GetMsg("NoPermissionVote"));
                    }
                    else
                    {
                        interaction.CreateInteractionResponse(_client, new InteractionResponse
                        {
                            Type = InteractionResponseType.ChannelMessageWithSource,
                            Data = new InteractionCallbackData
                            {
                                Content = GetMsg("NoPermissionVote"),
                                Flags = MessageFlags.Ephemeral
                            }
                        });
                    }
                    return;
                }
                if (Voted.Contains(author.Id))
                {
                    if (interaction == null)
                    {
                        author.SendDirectMessage(_client, GetMsg("UI.Voted"));
                    }
                    else
                    {
                        interaction.CreateInteractionResponse(_client, new InteractionResponse
                        {
                            Type = InteractionResponseType.ChannelMessageWithSource,
                            Data = new InteractionCallbackData
                            {
                                Content = GetMsg("UI.Voted"),
                                Flags = MessageFlags.Ephemeral
                            }
                        });
                    }
                    return;
                }
                key = VoteResult.ElementAt(i - 1).Key;
                if (VoteResult.TryGetValue(key, out val))
                {
                    VoteResult[key] = val + 1;
                }
               
                Voted.Add(author.Id);
                if (LogToDiscord)
                    Log(string.Format(GetMsg("VoteLog"), author.Username, i));
                if (interaction == null)
                {
                    author.SendDirectMessage(_client, GetMsg("VoteSuccess"));
                }
                else
                {
                    interaction.CreateInteractionResponse(_client, new InteractionResponse
                    {
                        Type = InteractionResponseType.ChannelMessageWithSource,
                        Data = new InteractionCallbackData
                        {
                            Content = GetMsg("VoteSuccess"),
                            Flags = MessageFlags.Ephemeral
                        }
                    });
                }

                Puts(string.Format(GetMsg("VoteLog"), author.Username, i));
            }
            if (config.AutoVote.IsOnlyAuthUsers && author.IsLinked())
            {

                IPlayer player = author.Player;
                if (Voted.Contains(player.Id) || Voted.Contains(author.Id))
                {
                    if (interaction == null)
                    {
                        author.SendDirectMessage(_client, GetMsg("UI.Voted"));
                    }
                    else
                    {
                        interaction.CreateInteractionResponse(_client, new InteractionResponse
                        {
                            Type = InteractionResponseType.ChannelMessageWithSource,
                            Data = new InteractionCallbackData
                            {
                                Content = GetMsg("UI.Voted"),
                                Flags = MessageFlags.Ephemeral
                            }
                        });
                    }
                    return;
                }
                if (config.Settings.isOnlyPlayersWithPermCanVote && !player.HasPermission(permissionVote))
                {
                    if (interaction == null)
                    {
                        author.SendDirectMessage(_client, GetMsg("NoPermissionVote"));
                    }
                    else
                    {
                        interaction.CreateInteractionResponse(_client, new InteractionResponse
                        {
                            Type = InteractionResponseType.ChannelMessageWithSource,
                            Data = new InteractionCallbackData
                            {
                                Content = GetMsg("NoPermissionVote"),
                                Flags = MessageFlags.Ephemeral
                            }
                        });
                    }
                    return;
                }
                key = VoteResult.ElementAt(i - 1).Key;
                if (VoteResult.TryGetValue(key, out val))
                {
                    VoteResult[key] = val + 1;
                }
                Voted.Add(player.Id);
                if (ServerRewards != null && config.rewards.RustRewards)
                    ServerRewards?.Call("AddPoints", ((BasePlayer)player).userID, 5);
                if (Kits != null && config.rewards.Kits && (bool)Kits?.Call("IsKit",config.rewards.KitName))
                    Kits?.Call("GiveKit", (BasePlayer)player, config.rewards.KitName);
                if (LogToDiscord && config.Settings.isDiscordBot)
                    Log(string.Format(GetMsg("VoteLog"), ((BasePlayer)player).displayName, key));
                if (interaction == null)
                {
                    author.SendDirectMessage(_client, GetMsg("VoteSuccess"));
                }
                else
                {
                    interaction.CreateInteractionResponse(_client, new InteractionResponse
                    {
                        Type = InteractionResponseType.ChannelMessageWithSource,
                        Data = new InteractionCallbackData
                        {
                            Content = GetMsg("VoteSuccess"),
                            Flags = MessageFlags.Ephemeral
                        }
                    });
                }
                Puts(string.Format(GetMsg("VoteLog"), player.Name, i));
            }
        }
        private string GetRoleNameById(Snowflake id)
        {
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
        private ConfigData.DiscordChannel FindChannelById(Snowflake id)
        {
            for (int i = 0; i < _channelCount; i++)
            {
                if (config._DiscordSettings.Channels[i].ChannelId == id)
                    return config._DiscordSettings.Channels[i];
            }

            return null;
        }
        private void Log(string message)
        {
            ulong chanid = Convert.ToUInt64(config.Settings.DiscordLogChannelID);

            MessageCreate create = new MessageCreate
            {
                Content = message,
            };
            GetChannel(_client, (Snowflake)chanid, chan =>
            {
                chan.CreateMessage(_client, create);
            });
        }
        private void GetChannel(DiscordClient c, Snowflake chan_id, Action<DiscordChannel> cb, Snowflake guildid = default(Snowflake))
        {
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
                PrintWarning($"MapVoter failed to fetch channel! (chan_id={chan_id}). Guild is invalid.");
                return;
            }
            if (g.Unavailable ?? false == true)
            {
                PrintWarning($"MapVoter failed to fetch channel! (chan_id={chan_id}). Guild is possibly invalid or not available yet.");
                return;
            }
            if (foundchan == null)
            {
                if (guildid.IsValid()) return; // Ignore printing error
                PrintWarning($"MapVoter failed to fetch channel! (chan_id={chan_id}).");
                return;
            }
            if (foundchan.Id != chan_id) return;
            cb?.Invoke(foundchan);
        }
        #endregion
        #region Classes
        public class CustomMap
        {
            public string Name;
            public string Url;
            public string Image;
            public CustomMap() { }
            public CustomMap(string name, string url, string image)
            {
                Name = name;
                Url = url;
                Image = image;
            }
        }
        public class VoteData
        {
            public bool CustomMap;
            public bool isVoteDay;
            public List<string> Voted = new List<string>();
            public Dictionary<uint, int> VoteResult = new Dictionary<uint, int>();
            public List<RustMaps> Maps = new List<RustMaps>();
            public VoteData() { }
            public VoteData (bool isvoteday)
            {
                this.isVoteDay = isvoteday;
            }
            public VoteData(bool isvoteday, List<string> voted, Dictionary<uint, int> voteResult, List<RustMaps> maps, bool custommap = false)
            {
                this.isVoteDay = isvoteday;
                Voted = voted;
                VoteResult = voteResult;
                Maps = maps;
                this.CustomMap = custommap;
            }
        }
        public class Monument
        {
            [JsonProperty(PropertyName ="prefab")]
            public string Prefab { get; set; }

            [JsonProperty(PropertyName ="monument")]
            public string Monumentx { get; set; }

            [JsonProperty(PropertyName ="biome")]
            public string Biome { get; set; }

            [JsonProperty(PropertyName ="x")]
            public int X { get; set; }

            [JsonProperty(PropertyName ="y")]
            public int Y { get; set; }
        }

        public class RustMaps
        {
            [JsonProperty(PropertyName ="id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName ="staging")]
            public bool Staging { get; set; }

            [JsonProperty(PropertyName ="seed")]
            public int Seed { get; set; }

            [JsonProperty(PropertyName ="size")]
            public int Size { get; set; }

            [JsonProperty(PropertyName ="monuments")]
            public List<Monument> Monuments { get; set; }

            [JsonProperty(PropertyName ="url")]
            public string Url { get; set; }

            [JsonProperty(PropertyName ="imageUrl")]
            public string ImageUrl { get; set; }

            [JsonProperty(PropertyName ="imageIconUrl")]
            public string ImageIconUrl { get; set; }

            [JsonProperty(PropertyName ="thumbnailUrl")]
            public string ThumbnailUrl { get; set; }
            public RustMaps(){  }
            public RustMaps (string id, bool staging, int seed, int size, List<Monument> Monuments, string url, string ImageUrl, string ImageIconUrl, string ThumbnailUrl)
            {
                this.Id = id;
                this.Staging = staging;
                this.Seed = seed;
                this.Size = size;
                this.Monuments = Monuments;
                this.Url = url;
                this.ImageUrl = ImageUrl;
                this.ImageIconUrl = ImageIconUrl;
                this.ThumbnailUrl = ThumbnailUrl;
            }
            public RustMaps(int seed, int size, string ImageIconUrl)
            {
                this.Id = string.Empty;
                this.Size = size;
                this.Seed = seed;
                this.ImageIconUrl = ImageIconUrl;
            }
            public RustMaps(string id, int seed, string url, string ImageIconUrl)
            {
                this.Seed = seed;
                this.Id = id;
                this.Url = url;
                this.ImageIconUrl = ImageIconUrl;
            }
        }
        public class Mapid
        {
            [JsonProperty(PropertyName ="mapId")]
            public string Id { get; set; }
        }
        public class CurrentState
        {
            [JsonProperty(PropertyName ="currentState")]
            public string currentState { get; set; }
        }
        public class Result
        {
            [JsonProperty(PropertyName ="id")]
            public string Id { get; set; }

            [JsonProperty(PropertyName ="seed")]
            public int Seed { get; set; }

            [JsonProperty(PropertyName ="size")]
            public int Size { get; set; }
        }

        public class filteredMaps
        {
            [JsonProperty(PropertyName ="results")]
            public List<Result> Results { get; set; }

            [JsonProperty(PropertyName ="page")]
            public int Page { get; set; }

            [JsonProperty(PropertyName ="perPage")]
            public int PerPage { get; set; }

            [JsonProperty(PropertyName ="lastPage")]
            public bool LastPage { get; set; }
        }
        #region Discord Class
        public class Message
        {
            public string username { get; set; }
            public string avatar_url { get; set; }
            public string content { get; set;}
            public List<Embeds> embeds { get; set; }

            public class Fields
            {
                public string name { get; set; }
                public string value { get; set; }
                public bool inline { get; set; }
                public Fields(string name, string value, bool inline)
                {
                    this.name = name;
                    this.value = value;
                    this.inline = inline;
                }
            }

            public class Footer
            {
                public string text { get; set; }
                public Footer(string text)
                {
                    this.text = text;
                }
            }

            public class Image
            {
                public string url { get; set; }
                public Image(string url)
                {
                    this.url = url;
                }
            }

            public class Embeds
            {
                public string title { get; set; }
                public string description { get; set; }
                public Image image { get; set; }
                public List<Fields> fields { get; set; }
                public Footer footer { get; set; }
                public Embeds(string title, string description, List<Fields> fields, Footer footer, Image image)
                {
                    this.title = title;
                    this.description = description;
                    this.image = image;
                    this.fields = fields;
                    this.footer = footer;
                }
            }

            public Message(string username, string avatar_url, List<Embeds> embeds, string content = null)
            {
                this.username = username;
                this.avatar_url = avatar_url;
                this.content = content;
                this.embeds = embeds;
            }
        }
        public class AutoWipeConfig
        {
            [JsonProperty(PropertyName ="isWipeDay")]
            public bool IsWipeDay { get; set; }

            [JsonProperty(PropertyName ="MapSeed")]
            public int MapSeed { get; set; }

            [JsonProperty(PropertyName ="MapSize")]
            public int MapSize { get; set; }

            [JsonProperty(PropertyName ="WipeDate")]
            public DateTime WipeDate { get; set; }

            [JsonProperty(PropertyName ="FullWipe")]
            public bool FullWipe { get; set; }

            [JsonProperty(PropertyName ="ForcedWipe")]
            public bool ForcedWipe { get; set; }

            [JsonProperty(PropertyName ="iSCustomMap")]
            public bool iSCustomMap { get; set; }

            [JsonProperty(PropertyName ="LevelUrl")]
            public string LevelUrl { get; set; }
            public AutoWipeConfig() { }
            public AutoWipeConfig(bool isWipeday, int mapseed, int mapsize, DateTime wipedate, bool fullwipe, bool forcedwipe, bool isCustomMap = false, string levelurl = "")
            {
                this.MapSeed = mapseed;
                this.MapSize = mapsize;
                this.WipeDate = wipedate;
                this.FullWipe = fullwipe;
                this.ForcedWipe = forcedwipe;
                this.IsWipeDay = isWipeday;
                this.iSCustomMap = isCustomMap;
                this.LevelUrl = levelurl;
            }
            public AutoWipeConfig(bool isWipeday)
            {
                this.IsWipeDay = isWipeday;
            }
        }
        #endregion
        #endregion
    }
}