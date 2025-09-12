/* Copyright (C) Whispers88 - All Rights Reserved
 * Unauthorized copying of this file, via any medium is strictly prohibited
 * Proprietary and confidential
 * Written by Whispers88 rustafarian.server@gmail.com, Feb 2025
 * Version 1.1.3
 *
 *You may not copy, modify, merge, publish, distribute, sublicense, or sell copies of This Software without the Developer’s consent

   THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO,
   THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS
   BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE
   GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
   LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Configuration;
using Oxide.Core.Libraries.Covalence;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;
using Facepunch;
using System.Collections;
using UnityEngine.Networking;
using Oxide.Core.Plugins;
using System.Text;
using HarmonyLib;
using System.Linq;
using System.IO;
using System.Collections.Concurrent;
using ProtoBuf;

namespace Oxide.Plugins
{
    [Info("Team Tracker", "Whispers88", "1.1.3")]
    [Description("Tracks player data to identify associated players")]
    public class TeamTracker : CovalencePlugin
    {
        private Dictionary<ulong, Dictionary<ulong, int>> _proximityticks = new Dictionary<ulong, Dictionary<ulong, int>>();

        private HashSet<ulong> _whitelistPlayers = new HashSet<ulong>();
        private static string permWhitelist = "teamtracker.whitelist";
        private static string permAdmin = "teamtracker.admin";

        private static int maxPlayers = 5;
        private static int banWeighting = 5;
        private static bool banAssociation = true;
        private static float proximityDistanceSqrd = 225;

        private static string banText = "Banned Reason: Breaking Team Limits";
        private static string profileLink = "http://steamcommunity.com/profiles/";

        private static TeamTracker? _teamTracker;

        private static string onlineEmoji = string.Empty;
        private static string offlineEmoji = string.Empty;
        private static string teamMemberEmoji = string.Empty;
        private static string mainMemberEmoji = string.Empty;
        private static string unknownEmoji = string.Empty;
        private static string serverName = string.Empty;

        private StringBuilder _stringBuilder = new StringBuilder();

        #region Init
        private void Init()
        {
            _teamTracker = this;

            onlineEmoji = $":{config.discordSettings.onlineEmoji}:";
            offlineEmoji = $":{config.discordSettings.offlineEmoji}:";
            teamMemberEmoji = $":{config.discordSettings.teamMemberEmoji}:";
            unknownEmoji = $":{config.discordSettings.unknownEmoji}:";
            mainMemberEmoji = $":{config.discordSettings.mainMemberEmoji}:";

            maxPlayers = config.MaxPlayers;
            banWeighting = config.banSettings.weighting;
            banAssociation = config.banSettings.associationBan;
            banText = config.banSettings.banText;
            proximityDistanceSqrd = (float)Math.Pow(config.proximitySettings.proximityDistance, 2);
            profileLink = config.bmSettings.bmProfileLinks ? "https://www.battlemetrics.com/rcon/players/?filter[search]={0}&method=quick&redirect=1" : "http://steamcommunity.com/profiles/{0}";
            CheckPlayerVisMask = LayerMask.GetMask("Deployed", "Construction", "World");
            _V3up = new Vector3(0, 1.5f, 0);

            permission.RegisterPermission(permWhitelist, this);
            permission.RegisterPermission(permAdmin, this);

            AddCovalenceCommand("clearplayerdata", "ClearPlayerDataCMD");
            AddCovalenceCommand("teamcheck", "CheckPlayerDataCMD");
        }

        Harmony harmony = new Harmony("teamtracker");

        private void OnServerInitialized()
        {
            serverName = string.IsNullOrEmpty(config.discordSettings.serverName) ? ConVar.Server.hostname : config.discordSettings.serverName;

            if (RelationshipManager.maxTeamSize != maxPlayers)
            {
                Puts($"Warning server may be misconfigured maxteam size is {RelationshipManager.maxTeamSize} and {maxPlayers} usually these should be the same.");
            }

            foreach (BasePlayer basePlayer in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(basePlayer);
            }
            if (config.proximitySettings.enabled)
            {
                ServerMgr.Instance.InvokeRepeating(RunProximityChecks, 30f, config.proximitySettings.proximityCheckInterval);
            }
            ServerMgr.Instance.InvokeRepeating(CheckWebQueue, 30f, 30f);

            var classProcessor = harmony.CreateClassProcessor(typeof(BuildingPrivlidge_OnKilled_Patch));
            classProcessor.Patch();

            var classProcessor2 = harmony.CreateClassProcessor(typeof(BuildingPrivlidge_ServerProjectileHit_Patch));
            classProcessor2.Patch();
        }

        private void CheckWebQueue()
        {
            if (payloadCoroutine != null || payload2Send.Count < 1)
                return;

            payloadCoroutine = SendPayload();
            ServerMgr.Instance.StartCoroutine(payloadCoroutine);
        }

        private void SetupHooks()
        {
            if (!config.turretSettings.enabled)
            {
                Unsubscribe(nameof(OnTurretAuthorize));
            }
            if (!config.toolcupboardSettings.enabled)
            {
                Unsubscribe(nameof(OnCupboardAuthorize));
            }
            if (!config.bagSettings.enabled)
            {
                Unsubscribe(nameof(CanAssignBed));
            }
            if (!config.codeLockSettings.enabled)
            {
                Unsubscribe(nameof(OnCodeEntered));
            }
            if (!config.healingSettings.enabled)
            {
                Unsubscribe(nameof(OnPlayerRevive));
                Unsubscribe(nameof(OnPlayerAssist));
            }
            if (!config.vehicleSettings.enabled)
            {
                Unsubscribe(nameof(OnEntityMounted));
            }
            if (!config.raidingSettings.enabled)
            {
                Unsubscribe(nameof(OnExplosiveThrown));
                Unsubscribe(nameof(OnRocketLaunched));
            }
            if (!config.lootingSettings.enabled)
            {
                Unsubscribe(nameof(OnLootEntity));
            }
            if (!config.pvpSettings.enabled)
            {
                Unsubscribe(nameof(OnPlayerWound));
                Unsubscribe(nameof(OnPlayerDeath));
            }
        }
        #endregion Init

        #region Configuration

        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Max Players in a Team")]
            public int MaxPlayers = 5;

            [JsonProperty("Only count online players")]
            public bool _onlinePlayersOnly = false;

            [JsonProperty("Clear data on wipe")]
            public bool wipeData = true;

            [JsonProperty("Discord Settings")]
            public DiscordSettings discordSettings = new DiscordSettings();

            [JsonProperty("Warning Settings")]
            public WarningSettings warningSettings = new WarningSettings();

            [JsonProperty("Ban Settings")]
            public BanSettings banSettings = new BanSettings();

            [JsonProperty("BattleMetrics Settings")]
            public BattleMetricsSettings bmSettings = new BattleMetricsSettings();

            [JsonProperty("Proximity Settings")]
            public ProximitySettings proximitySettings = new ProximitySettings();

            [JsonProperty("Team Settings")]
            public TeamSettings teamSettings = new TeamSettings();

            [JsonProperty("Turret Settings")]
            public TurretSettings turretSettings = new TurretSettings();

            [JsonProperty("Toolcupboard Settings")]
            public ToolcupboardSettings toolcupboardSettings = new ToolcupboardSettings();

            [JsonProperty("Bag Settings")]
            public BagSettings bagSettings = new BagSettings();

            [JsonProperty("CodeLock Settings")]
            public CodeLockSettings codeLockSettings = new CodeLockSettings();

            [JsonProperty("Healing Settings")]
            public HealingSettings healingSettings = new HealingSettings();

            [JsonProperty("Vehicle Settings")]
            public VehicleSettings vehicleSettings = new VehicleSettings();

            [JsonProperty("Raiding Settings")]
            public RaidingSettings raidingSettings = new RaidingSettings();

            [JsonProperty("PVP Settings")]
            public PVPSettings pvpSettings = new PVPSettings();

            [JsonProperty("Looting Settings")]
            public LootingSettings lootingSettings = new LootingSettings();

            [JsonProperty("Lang Settings")]
            public LangSettings langSettings = new LangSettings();

            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonConvert.DeserializeObject<Dictionary<string, object>>(ToJson());
        }

        #region Settings
        public class LangSettings
        {
            [JsonProperty("Warning Title {0} - Type {1}- Server Name")]
            public string warningTitle = "{0} Warning - {1}";

            [JsonProperty("Ban Title {0} - Server Name")]
            public string banTitle = "Teaming Ban - {0}";

            [JsonProperty("Team Request Title {0} - Server Name")]
            public string requestTitle = "Team Request - {0}";

            [JsonProperty("Player Subtitle")]
            public string playerSubtitle = "Player:";

            [JsonProperty("UserID Subtitle")]
            public string userIDSubtitle = "User ID:";

            [JsonProperty("Time Played Subtitle")]
            public string timePlayedSubtitle = "Time Played:";

            [JsonProperty("Position Subtitle")]
            public string posSubtitle = "Pos:";

            [JsonProperty("Previous Warnings Subtitle")]
            public string previousSubtitle = "Previous Warnings:";

            [JsonProperty("Warning Players Subtitle")]
            public string warningsSubtitle = "Warning Players:";

            [JsonProperty("Warning Players Continued Subtitle")]
            public string warningsContSubtitle = "Warning Players Cont:";

            [JsonProperty("Current Team Members Subtitle")]
            public string currentTeam = "Current Team Members:";

            [JsonProperty("Record Team Members Subtitle")]
            public string recordedTeam = "Record Team Members:";

            [JsonProperty("Associated Players Subtitle")]
            public string associatedPlayersSubtitle = "Associated Players";

            [JsonProperty("Warning Notes Subtitle")]
            public string warningsNotesSubtitle = "Warning Notes:";

            [JsonProperty("Ban Notes Subtitle")]
            public string banNotesSubtitle = "Notes:";
        }

        public class DiscordSettings
        {
            [JsonProperty("Server Name")]
            public string serverName = string.Empty;

            [JsonProperty("Discord Webhook")]
            public string discordWebhook = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty("Discord Webhook for Team Checks")]
            public string discordWebhookTeamChecks = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty("Team Check Embed Color")]
            public string embedColor = "13200406";

            [JsonProperty("RoleID to Tag")]
            public string roleIDtag = "0";

            [JsonProperty("User Name")]
            public string userName = "Team Limits Report";

            [JsonProperty("Author Icon URL")]
            public string authorIcon = "https://files.facepunch.com/garry/f549bfc2-2a49-4eb8-a701-3efd7ae046ac.png";

            [JsonProperty("Online Emoji")]
            public string onlineEmoji = "green_circle";

            [JsonProperty("Offline Emoji")]
            public string offlineEmoji = "red_circle";

            [JsonProperty("Never Connected Emoji")]
            public string unknownEmoji = "black_circle";

            [JsonProperty("Main Player Emoji")]
            public string mainMemberEmoji = "bust_in_silhouette";

            [JsonProperty("Team Member Emoji")]
            public string teamMemberEmoji = "busts_in_silhouette";

            [JsonProperty("Show Associated Players in warning logs")]
            public bool associatedPlayers = true;

            [JsonProperty("Show Team Players in warning logs")]
            public bool teamPlayers = false;

            [JsonProperty("Log Team Checks to Rcon")]
            public bool logTeamChecksRcon = false;

        }
        public class WarningSettings
        {
            [JsonProperty("Length of time to display warning")]
            public float warningShowTime = 15f;

            [JsonProperty("Use Lang for warning messages")]
            public bool useLang = false;

            [JsonProperty("Warning Text")]
            public string warningText = "<size=11><color=black><b>This server has a team limit of {0} players<b></color> <br>to swap team members contact support";
        }

        public class BanSettings
        {
            [JsonProperty("Enable Bans")]
            public bool enabled = false;

            [JsonProperty("Log Bans to Discord")]
            public bool logBans = true;

            [JsonProperty("Discord Webhook for Bans")]
            public string discordWebhook = "https://support.discord.com/hc/en-us/articles/228383668-Intro-to-Webhooks";

            [JsonProperty("Ban Duration (days)")]
            public int banDuration = 7;

            [JsonProperty("Ban Text")]
            public string banText = "Banned Reason: Breaking Team Limits";

            [JsonProperty("Local Bans")]
            public bool localBan = false;

            [JsonProperty("Ban Whole Team")]
            public bool banTeam = false;

            [JsonProperty("Ban by Max Warnings")]
            public bool maxWarningsBan = true;

            [JsonProperty("Max Warnings")]
            public int maxWarnings = 3;

            [JsonProperty("Ban by Unique Warnings")]
            public bool uniqueWarningsBan = true;

            [JsonProperty("Max Unique Warnings")]
            public int uniqueWarnings = 3;

            [JsonProperty("Enable Ban by Max Association")]
            public bool associationBan = true;

            [JsonProperty("Max Association Weighting")]
            public int weighting = 5;

            [JsonProperty("Min Association for Logging")]
            public int weightingMin = 2;

            [JsonProperty("Ban all associated players above max weighting")]
            public bool banAssociatedPlayers = false;

            [JsonProperty("Embed Color")]
            public string embedColor = "0";
        }

        public class BattleMetricsSettings
        {
            [JsonProperty("Use BM profile links")]
            public bool bmProfileLinks = false;

            [JsonProperty("BattleMetrics Bans")]
            public bool battlemetricsBan = false;

            [JsonProperty("BattleMetrics API")]
            public string APIKey = string.Empty;

            [JsonProperty("BattleMetrics serverID")]
            public string serverID = string.Empty;

            [JsonProperty("BattleMetrics orgID")]
            public string orgID = string.Empty;

            [JsonProperty("BattleMetrics BanList ID")]
            public string banListID = string.Empty;

            [JsonProperty("BattleMetrics Org Wide Ban")]
            public bool orgWide = false;

            [JsonProperty("BattleMetrics Auto Add")]
            public bool autoAdd = false;

            [JsonProperty("BattleMetrics Native Enabled")]
            public bool native = false;
        }

        public class ProximitySettings
        {
            [JsonProperty("Enable Proximity Checks")]
            public bool enabled = true;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = true;

            [JsonProperty("Proximity Distance")]
            public float proximityDistance = 15f;

            [JsonProperty("Proximity Check Interval (seconds)")]
            public float proximityCheckInterval = 360f;

            [JsonProperty("Ticks per Report")]
            public int proximityAlertTicks = 2;

            [JsonProperty("Online Time Reduction Factor (Reduce ticks by % of online time)")]
            public float onlineTimeFactor = 10;

            [JsonProperty("Proximity Vision Checks")]
            public bool proximityVision = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 1f;

            [JsonProperty("Embed Color")]
            public string embedColor = "13200406";
        }

        public class TurretSettings
        {
            [JsonProperty("Enable Turret Checks")]
            public bool enabled = true;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 1f;

            [JsonProperty("Embed Color")]
            public string embedColor = "16711680";
        }

        public class TeamSettings
        {
            [JsonProperty("Enable Team Member Checks")]
            public bool enabled = true;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 1f;

            [JsonProperty("Embed Color")]
            public string embedColor = "4299569";
        }

        public class ToolcupboardSettings
        {
            [JsonProperty("Enable Toolcupboard Checks")]
            public bool enabled = false;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 0.2f;

            [JsonProperty("Embed Color")]
            public string embedColor = "11952688";
        }

        public class BagSettings
        {
            [JsonProperty("Enable Bag Checks")]
            public bool enabled = true;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 1f;

            [JsonProperty("Embed Color")]
            public string embedColor = "2042328";
        }

        public class CodeLockSettings
        {
            [JsonProperty("Enable CodeLock Checks")]
            public bool enabled = true;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 1f;

            [JsonProperty("Embed Color")]
            public string embedColor = "13200406";
        }

        public class HealingSettings
        {
            [JsonProperty("Enable Healing Checks")]
            public bool enabled = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 0.5f;

            [JsonProperty("Team Association weighting limit for logging")]
            public float weightingFilter = 2f;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = false;

            [JsonProperty("Embed Color")]
            public string embedColor = "9961352";
        }

        public class VehicleSettings
        {
            [JsonProperty("Enable Vehicle Checks")]
            public bool enabled = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 0.5f;

            [JsonProperty("Team Association weighting limit for logging")]
            public float weightingFilter = 2f;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = false;

            [JsonProperty("Embed Color")]
            public string embedColor = "12263372";
        }

        public class RaidingSettings
        {
            [JsonProperty("Enable Raiding Checks")]
            public bool enabled = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 0.5f;

            [JsonProperty("Team Association weighting limit for logging")]
            public float weightingFilter = 2f;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = false;

            [JsonProperty("Embed Color")]
            public string embedColor = "12263372";
        }

        public class PVPSettings
        {
            [JsonProperty("Enable PVP Checks")]
            public bool enabled = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 0.5f;

            [JsonProperty("Team Association weighting limit for logging")]
            public float weightingFilter = 2f;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = false;

            [JsonProperty("Embed Color")]
            public string embedColor = "12263372";
        }

        public class LootingSettings
        {
            [JsonProperty("Enable Looting Checks")]
            public bool enabled = true;

            [JsonProperty("Warn Players")]
            public bool showWarning = false;

            [JsonProperty("Weighting")]
            public float weighting = 0.5f;

            [JsonProperty("Team Association weighting limit for logging")]
            public float weightingFilter = 2f;

            [JsonProperty("Log to Discord")]
            public bool logtoDiscord = false;

            [JsonProperty("Embed Color")]
            public string embedColor = "12263372";
        }

        #endregion Settings

        protected override void LoadDefaultConfig() => config = new Configuration();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    throw new JsonException();
                }
                SaveConfig();
            }
            catch
            {
                Puts($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            LogWarning($"Configuration changes saved to {Name}.json");
            Config.WriteObject(config, true);
        }

        private void Loaded()
        {
            SetupHooks();

            _ProximityDataCFG = Interface.Oxide.DataFileSystem.GetFile("TeamTracker/ProximityData");
            _BagsDataCFG = Interface.Oxide.DataFileSystem.GetFile("TeamTracker/BagsData");
            _PlayerDataCFG = Interface.Oxide.DataFileSystem.GetFile("TeamTracker/PlayerData");
            _PlayerTimeDataCFG = Interface.Oxide.DataFileSystem.GetFile("TeamTracker/PlayerTimeData");
            _PlayerInfoCFG = Interface.Oxide.DataFileSystem.GetFile("TeamTracker/PlayerInfo");
            LoadData();
        }
        #endregion Configuration

        #region Localization

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["WarningText"] = "<size=11><color=black><b>This server has a team limit of {0} players<b></color> <br>to swap team members contact support"

            }, this);
        }

        #endregion Localization

        #region Data
        private Dictionary<ulong, PlayerData> _playerData = new Dictionary<ulong, PlayerData>();

        public void AssociationCheck()
        {
            foreach (var playerData in _AssociationCheckQueue)
            {
                int i = 0;
                foreach (var player in playerData._associatedPlayers)
                {
                    if (player.Value < banWeighting)
                        continue;

                    if (config._onlinePlayersOnly && _playerInfo.TryGetValue(player.Key, out var playerInfo) && !playerInfo.online)
                    {
                        continue;
                    }
                    i++;
                }

                if (i < maxPlayers)
                    return;

                if (banAssociation)
                {
                    PlayerInfo? mainPlayerinfo = GetPlayerInfo(playerData.PlayerID);
                    string warnings = string.Empty;
                    foreach (var warning in playerData._warnings)
                    {
                        warnings += $"{warning},";
                    }

                    StringBuilder stringBuilder = _stringBuilder;
                    stringBuilder.Clear();

                    stringBuilder.Append($"Ban by Association Limits\n**Player:**{mainPlayerinfo?.name}-{playerData.PlayerID}\n**Previous Warnings:**{warnings}\n**Associated Players:**\n");
                    List<KeyValuePair<ulong, float>> associatedPlayersList = Pool.Get<List<KeyValuePair<ulong, float>>>();
                    associatedPlayersList.AddRange(playerData._associatedPlayers.OrderByDescending(kv => kv.Value));
                    foreach (var player in associatedPlayersList)
                    {
                        PlayerInfo? playerinfo = GetPlayerInfo(player.Key);

                        if (playerinfo == null) continue;
                        if (player.Value < config.banSettings.weightingMin)
                        {
                            break;
                        }
                        string pnotes = $"{playerinfo.name} - {player.Key} **[{player.Value}]** \n";
                        if (stringBuilder.Length + pnotes.Length > 1024)
                        {
                            break;
                        }
                        stringBuilder.Append(pnotes);
                    }
                    Pool.FreeUnmanaged(ref associatedPlayersList);
                    BanPlayer(playerData.PlayerID, banText, stringBuilder.ToString());
                }
            }
            _AssociationCheckQueue.Clear();
        }

        public class PlayerData
        {
            public int WarningCount = 0;
            public ulong PlayerID;
            public bool Banned = false;
            public Dictionary<WarningType, int> _warnings = new Dictionary<WarningType, int>();
            public Dictionary<ulong, float> _associatedPlayers = new Dictionary<ulong, float>();
            public HashSet<ulong> _teamHistory = new HashSet<ulong>();
        }
        private PlayerData? AddAssociationData(ulong playerID, ulong associatedPlayerID, float weighting)
        {
            if (playerID == 0 || playerID == associatedPlayerID) return null;

            if (!_playerData.TryGetValue(playerID, out var playerdata))
            {
                playerdata = new PlayerData() { PlayerID = playerID };
                _playerData.Add(playerID, playerdata);
            }
            AddAssociation(playerdata, associatedPlayerID, weighting);
            return playerdata;
        }

        private PlayerData? AddAssociationDataAll(ulong playerID, HashSet<ulong> playerid, float weighting)
        {
            if (playerID == 0) return null;

            if (!_playerData.TryGetValue(playerID, out var playerdata))
            {
                playerdata = new PlayerData() { PlayerID = playerID };
                _playerData.Add(playerID, playerdata);
            }
            foreach (var player in playerid)
            {
                AddAssociationData(playerID, playerid, weighting);
            }
            return playerdata;
        }

        private PlayerData? AddAssociationData(ulong playerID, HashSet<ulong> playerid, float weighting)
        {
            if (playerID == 0) return null;

            if (!_playerData.TryGetValue(playerID, out var playerdata))
            {
                playerdata = new PlayerData() { PlayerID = playerID };
                _playerData.Add(playerID, playerdata);
            }
            foreach (var player in playerid)
            {
                AddAssociation(playerdata, player, weighting);
            }
            return playerdata;
        }

        private HashSet<PlayerData> _AssociationCheckQueue = new HashSet<PlayerData>();
        private void AddAssociation(PlayerData playerdata, ulong playerid, float weighting)
        {
            if (playerid == playerdata.PlayerID || playerid == 0)
            {
                return;
            }
            if (playerdata._associatedPlayers.TryGetValue(playerid, out float count))
            {
                playerdata._associatedPlayers[playerid] = count + (float)Math.Round(weighting, 3);
            }
            else
            {
                playerdata._associatedPlayers.Add(playerid, weighting);
            }
            _AssociationCheckQueue.Add(playerdata);
        }

        public void AddAssociation(PlayerData playerdata, HashSet<ulong> playerid, float weighting)
        {
            foreach (var player in playerid)
            {
                AddAssociation(playerdata, player, weighting);
            }
        }

        private DynamicConfigFile _TeamDataCFG;
        private DynamicConfigFile _ProximityDataCFG;
        private DynamicConfigFile _BagsDataCFG;
        private DynamicConfigFile _PlayerDataCFG;
        private DynamicConfigFile _PlayerInfoCFG;
        private DynamicConfigFile _PlayerTimeDataCFG;

        private void LoadData()
        {
            try
            {
                _proximityticks = _ProximityDataCFG.ReadObject<Dictionary<ulong, Dictionary<ulong, int>>>();
            }
            catch
            {
                _proximityticks = new Dictionary<ulong, Dictionary<ulong, int>>();
            }
            try
            {
                _BagsList = _BagsDataCFG.ReadObject<Dictionary<uint, List<BagInfo>>>();
            }
            catch
            {
                _BagsList = new Dictionary<uint, List<BagInfo>>();
            }
            try
            {
                _playerData = _PlayerDataCFG.ReadObject<Dictionary<ulong, PlayerData>>();
            }
            catch
            {
                _playerData = new Dictionary<ulong, PlayerData>();
            }
            try
            {
                _playerTimeData = _PlayerTimeDataCFG.ReadObject<Dictionary<ulong, float>>();
            }
            catch
            {
                _playerTimeData = new Dictionary<ulong, float>();
            }
            try
            {
                _playerInfo = _PlayerInfoCFG.ReadObject<Dictionary<ulong, PlayerInfo>>();
            }
            catch
            {
                _playerInfo = new Dictionary<ulong, PlayerInfo>();
            }
            Puts("Data Loaded");
        }

        private void Unload()
        {
            ServerMgr.Instance.CancelInvoke(RunProximityChecks);

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerDisconnected(player, string.Empty);
            }
            SaveData();
            harmony.UnpatchAll("teamtracker");
            _teamTracker = null;
        }

        private void SaveData()
        {
            _BagsDataCFG.WriteObject(_BagsList);
            _ProximityDataCFG.WriteObject(_proximityticks);
            _PlayerDataCFG.WriteObject(_playerData);
            _PlayerTimeDataCFG.WriteObject(_playerTimeData);
            _PlayerInfoCFG.WriteObject(_playerInfo);
        }
        void OnNewSave(string filename)
        {
            if (!config.wipeData)
                return;
            _proximityticks.Clear();
            _BagsList.Clear();
            _playerData.Clear();
            _playerTimeData.Clear();
            _playerInfo.Clear();
            SaveData();
        }
        #endregion Data

        #region Commands
        private void ClearPlayerDataCMD(IPlayer iplayer, string command, string[] args)
        {
            if (!HasPerm(iplayer.Id, permAdmin))
                return;

            if (args.Length == 0)
            {
                iplayer.Message("Syntax: /clearplayerdata <playername or steamid>");
                return;

            }

            IPlayer? target = covalence.Players.FindPlayer(args[0]);

            if (target == null)
            {
                iplayer.Message("Player not found");
                return;
            }
            ulong targetID = ulong.Parse(target.Id);
            _playerData.Remove(targetID);
            _proximityticks.Remove(targetID);
            _playerTimeData.Remove(targetID);

            foreach (var player in _playerData)
            {
                PlayerData pdata = player.Value;
                pdata._associatedPlayers.Remove(targetID);
                pdata._teamHistory.Remove(targetID);
            }

            foreach (var player in _proximityticks)
            {
                player.Value.Remove(targetID);
            }

            foreach (var building in _BagsList)
            {
                foreach (var bag in building.Value)
                {
                    if (bag.OwnerID == targetID)
                    {
                        building.Value.Remove(bag);
                        continue;
                    }
                    if (bag.deployerUserID == targetID)
                    {
                        building.Value.Remove(bag);
                        continue;
                    }
                }
            }

            SaveData();

            iplayer.Message($"Player Data Cleared for {target.Name}");
        }

        private void CheckPlayerDataCMD(IPlayer iplayer, string command, string[] args)
        {
            if (!HasPerm(iplayer.Id, permAdmin))
                return;

            if (args.Length == 0)
            {
                iplayer.Message("Syntax: /teamcheck <playername or steamid>");
                return;
            }

            IPlayer? target = covalence.Players.FindPlayer(args[0]);

            if (target == null)
            {
                iplayer.Message("Player not found");
                return;
            }
            ulong targetID = ulong.Parse(target.Id);

            if (!_playerData.TryGetValue(targetID, out var pdata))
            {
                iplayer.Message($"Player {target.Name} has no associations");
                return;
            }

            if (!iplayer.IsServer)
                iplayer.Message($"Player {target.Name} data sent to discord");

            if (iplayer.IsServer && config.discordSettings.logTeamChecksRcon)
            {
                SendRequestRCON(targetID, pdata);
            }
            SendRequestDiscord(targetID, pdata);

        }

        #endregion Commands

        #region PlayerTimeTracker
        private Dictionary<ulong, float> _playerTimeData = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> _timeconnected = new Dictionary<ulong, float>();

        private Dictionary<ulong, PlayerInfo> _playerInfo = new Dictionary<ulong, PlayerInfo>();
        private class PlayerInfo
        {
            public ulong steamid;
            public string name;
            public bool online;
        }
        void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, permWhitelist) || player.IsAdmin || player.IsDeveloper)
            {
                _whitelistPlayers.Add(player.userID);
                return;
            }

            _timeconnected[player.userID] = Time.time;

            if (!_playerData.TryGetValue(player.userID, out var pdata))
            {
                pdata = new PlayerData() { PlayerID = player.userID };
                _playerData.Add(player.userID, pdata);
            }

            if (!_playerInfo.TryGetValue(player.userID, out var playerInfo))
            {
                playerInfo = new PlayerInfo() { steamid = player.userID, name = player.displayName, online = true };
                _playerInfo.Add(player.userID, playerInfo);
            }
            else
            {
                playerInfo.name = player.displayName;
                playerInfo.online = true;
            }

            if (player.Team != null)
            {
                foreach (var member in player.Team.members)
                {
                    pdata._teamHistory.Add(member);
                }
            }
        }
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (_playerInfo.TryGetValue(player.userID, out var playerInfo))
            {
                playerInfo.online = false;
            }

            if (_timeconnected.TryGetValue(player.userID, out float connectionTime))
            {
                float totalTime = Time.time - connectionTime;

                if (_playerTimeData.ContainsKey(player.userID))
                {
                    _playerTimeData[player.userID] += totalTime;
                }
                else
                {
                    _playerTimeData[player.userID] = totalTime;
                }

                _timeconnected.Remove(player.userID);
            }
        }


        #endregion PlayerTimeTracker

        #region Checks

        #region Codelocks
        private void OnCodeEntered(CodeLock codeLock, BasePlayer player, string code)
        {
            if (code != codeLock.code && code != codeLock.guestCode)
                return;

            if (IsWhitelisted(player.userID))
            {
                return;
            }

            HashSet<ulong> authedplayers = Pool.Get<HashSet<ulong>>();

            foreach (var entry in codeLock.whitelistPlayers)
            {
                if (IsWhitelisted(entry))
                    continue;
                authedplayers.Add(entry);
            }

            foreach (var entry in codeLock.guestPlayers)
            {
                if (IsWhitelisted(entry))
                    continue;

                authedplayers.Add(entry);
            }

            authedplayers.Remove(player.userID);

            //update other players
            foreach (var authedplayer in authedplayers)
            {
                if (!_playerData.TryGetValue(authedplayer, out var data))
                {
                    data = new PlayerData() { PlayerID = authedplayer };
                    _playerData.Add(authedplayer, data);
                }
                AddAssociation(data, player.userID, config.codeLockSettings.weighting);
            }

            //update new auth
            if (!_playerData.TryGetValue(player.userID, out var pdata))
            {
                pdata = new PlayerData() { PlayerID = player.userID };
                _playerData.Add(player.userID, pdata);
            }

            AddAssociation(pdata, authedplayers, config.codeLockSettings.weighting);

            if (authedplayers.Count < maxPlayers)
            {
                Pool.FreeUnmanaged(ref authedplayers);
                return;
            }

            //Send Alert
            WarnPlayers(authedplayers, WarningType.CodeLock, config.codeLockSettings.showWarning);
            WarnPlayer(player.userID, WarningType.CodeLock, config.codeLockSettings.showWarning);


            //Send to Discord
            if (config.codeLockSettings.logtoDiscord)
            {
                SendToDiscord(player.userID, pdata, WarningType.CodeLock, authedplayers, config.codeLockSettings.embedColor);
            }
            Pool.FreeUnmanaged(ref authedplayers);

        }

        #endregion Codelocks

        #region Bags
        //remove bags data for raided base
        [HarmonyPatch(typeof(BuildingPrivlidge), "OnDied")] // Change this to Die
        public static class BuildingPrivlidge_OnKilled_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(BuildingPrivlidge __instance, HitInfo info)
            {
                if (info == null || __instance.buildingID == 0)
                {
                    return;
                }
                if (info.Initiator is BasePlayer player)
                {
                    if (__instance.IsAuthed(player))
                        return;
                }

                _teamTracker._BagsList.Remove(__instance.buildingID);
            }
        }

        private Dictionary<uint, List<BagInfo>> _BagsList = new Dictionary<uint, List<BagInfo>>();
        private struct BagInfo
        {
            public ulong deployerUserID;
            public ulong OwnerID;
        }

        //lets get a hook for when a bag is assigned
        private void CanAssignBed(BasePlayer player, SleepingBag bag, ulong targetPlayerId)
        {
            if (bag.buildingID != 0)
            {
                NextTick(() => {
                    BedCheck(bag);
                });
                return;
            }

            if (player.userID == targetPlayerId)
                return;

            if (TeamMembersCount(player) >= maxPlayers && !HasTeamMember(player, targetPlayerId))
            {
                //Send Alert
                WarnPlayer(targetPlayerId, WarningType.Bag, config.bagSettings.showWarning);
                WarnPlayer(player.userID, WarningType.Bag, config.bagSettings.showWarning);
                if (config.bagSettings.logtoDiscord)
                {
                    //Send to Discord
                    SendToDiscord(bag.OwnerID, _playerData[bag.OwnerID], WarningType.Bag, new HashSet<ulong> { targetPlayerId }, config.bagSettings.embedColor, $"Sleepingbag given to {targetPlayerId} by {player.userID}");
                }
            }

            //add association for placer
            AddAssociationData(player.userID, targetPlayerId, config.bagSettings.weighting);

            //add association for recipient
            AddAssociationData(targetPlayerId, player.userID, config.bagSettings.weighting);
        }

        [HarmonyPatch(typeof(SleepingBag), "OnPlaced"), AutoPatch]
        public static class OnPlaced_Patch
        {
            [HarmonyPrefix]
            public static void Postfix(SleepingBag __instance, BasePlayer player)
            {
                if (__instance == null || __instance.buildingID == 0)
                {
                    return;
                }

                __instance.OwnerID = player.userID;
                __instance.deployerUserID = player.userID;
                _teamTracker.BedCheck(__instance);
            }
        }

        private void BedCheck(SleepingBag bag)
        {
            if (!config.bagSettings.enabled)
                return;

            BagInfo bagInfo = new BagInfo
            {
                deployerUserID = bag.deployerUserID,
                OwnerID = bag.OwnerID,
            };

            if (!_BagsList.TryGetValue(bag.buildingID, out List<BagInfo>? baglist))
            {
                baglist = new List<BagInfo>() { bagInfo };
                _BagsList.Add(bag.buildingID, baglist);
                return;
            }

            baglist.Add(bagInfo);

            HashSet<ulong> authedplayers = Pool.Get<HashSet<ulong>>();

            foreach (BagInfo pbag in baglist)
            {
                if (IsWhitelisted(pbag.OwnerID) || IsWhitelisted(pbag.deployerUserID))
                    continue;

                authedplayers.Add(pbag.OwnerID);
                authedplayers.Add(pbag.deployerUserID);
            }

            foreach (var authedplayer in authedplayers)
            {
                AddAssociationData(authedplayer, authedplayers, config.bagSettings.weighting);
            }

            if (authedplayers.Count <= maxPlayers)
            {
                Pool.FreeUnmanaged(ref authedplayers);
                return;
            }

            //Send Warnings
            WarnPlayers(authedplayers, WarningType.Bag, config.bagSettings.showWarning);

            authedplayers.Remove(bag.OwnerID);

            if (config.bagSettings.logtoDiscord)
            {
                //Send to Discord
                SendToDiscord(bag.OwnerID, _playerData[bag.OwnerID], WarningType.Bag, authedplayers, config.bagSettings.embedColor, $"Sleepingbag placed for {bag.deployerUserID} deployed by {bag.OwnerID}");
            }

            Pool.FreeUnmanaged(ref authedplayers);
        }

        #endregion

        #region Turrets
        private void OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (IsWhitelisted(player.userID))
                return;

            BuildingPrivlidge buildingPrivlidge = turret.GetBuildingPrivilege();
            if (buildingPrivlidge == null || buildingPrivlidge.cachedProtectedMinutes < 1)
                return;

            int count = 1;
            HashSet<ulong> authedplayers = Pool.Get<HashSet<ulong>>();

            foreach (var entry in turret.authorizedPlayers)
            {
                if (IsWhitelisted(entry.userid))
                    continue;

                if (entry.userid == player.userID)
                {
                    count = 0;
                    continue;
                }
                authedplayers.Add(entry.userid);
            }

            foreach (var authedplayer in authedplayers)
            {
                AddAssociationData(authedplayer, player.userID, config.turretSettings.weighting);
            }

            PlayerData? pdata = AddAssociationData(player.userID, authedplayers, config.turretSettings.weighting);

            if (pdata == null || turret.authorizedPlayers.Count + count <= maxPlayers)
            {
                Pool.FreeUnmanaged(ref authedplayers);
                return;
            }

            WarnPlayers(authedplayers, WarningType.Turret, config.turretSettings.showWarning);
            WarnPlayer(player.userID, WarningType.Turret, config.turretSettings.showWarning);


            if (config.turretSettings.logtoDiscord)
            {
                SendToDiscord(player.userID, pdata, WarningType.Turret, authedplayers, config.turretSettings.embedColor);
            }
        }

        #endregion Turrets

        #region Toolcupboard
        private void OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (IsWhitelisted(player.userID))
            {
                return;
            }

            if (privilege.cachedProtectedMinutes == 0 && Time.time - privilege.timePlaced > 900)
                return;

            HashSet<ulong> authedplayers = Pool.Get<HashSet<ulong>>();
            int count = 1;
            foreach (var entry in privilege.authorizedPlayers)
            {
                if (IsWhitelisted(entry.userid))
                    continue;

                if (entry.userid == player.userID)
                {
                    count = 0;
                    continue;
                }
                authedplayers.Add(entry.userid);
            }

            foreach (var authedplayer in authedplayers)
            {
                AddAssociationData(authedplayer, player.userID, config.toolcupboardSettings.weighting);
            }

            PlayerData? pdata = AddAssociationData(player.userID, authedplayers, config.toolcupboardSettings.weighting);

            if (pdata == null || privilege.authorizedPlayers.Count + count <= maxPlayers)
            {
                Pool.FreeUnmanaged(ref authedplayers);
                return;
            }

            WarnPlayers(authedplayers, WarningType.Toolcupboard, config.toolcupboardSettings.showWarning);
            WarnPlayer(player.userID, WarningType.Toolcupboard, config.toolcupboardSettings.showWarning);

            if (config.toolcupboardSettings.logtoDiscord)
            {
                SendToDiscord(player.userID, pdata, WarningType.Toolcupboard, authedplayers, config.toolcupboardSettings.embedColor);
            }

        }

        #endregion Toolcupboard

        #region TeamChecks
        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (IsWhitelisted(player.userID))
            {
                return;
            }
            HashSet<ulong> teamMembers = Pool.Get<HashSet<ulong>>();
            PlayerData? pdata = AddAssociationData(player.userID, teamMembers, config.teamSettings.weighting);

            foreach (var member in teamMembers)
            {
                if (IsWhitelisted(member))
                    continue;

                pdata._teamHistory.Add(member);

                if (!_playerData.TryGetValue(member, out var data))
                {
                    data = new PlayerData() { PlayerID = member };
                    _playerData.Add(member, data);
                }

                data._teamHistory.Add(player.userID);
                AddAssociation(data, teamMembers, config.teamSettings.weighting);
            }

            if (pdata == null || pdata._teamHistory.Count <= maxPlayers)
            {
                Pool.FreeUnmanaged(ref teamMembers);
                return;
            }

            WarnPlayers(teamMembers, WarningType.Team, config.teamSettings.showWarning);
            WarnPlayer(player.userID, WarningType.Team, config.teamSettings.showWarning);

            if (config.teamSettings.logtoDiscord)
            {
                SendToDiscord(player.userID, pdata, WarningType.Team, pdata._teamHistory, config.teamSettings.embedColor);
            }
            Pool.FreeUnmanaged(ref teamMembers);
        }
        #endregion TeamChecks

        #region HealingChecks
        private void OnPlayerAssist(BasePlayer player, BasePlayer reviver)
        {
            if (IsWhitelisted(reviver.userID) || IsWhitelisted(player.userID))
                return;

            if (reviver.userID == player.userID || reviver.userID == 0 || player.userID == 0)
            {
                return;
            }

            PlayerData? pdata = AddAssociationData(reviver.userID, player.userID, config.healingSettings.weighting);
            AddAssociationData(player.userID, reviver.userID, config.healingSettings.weighting);

            if (pdata == null)
                return;

            if (maxPlayers == 1 || TeamMembersCount(player) >= maxPlayers && !HasTeamMember(player, reviver.userID))
            {
                WarnPlayer(reviver.userID, WarningType.Healing, config.healingSettings.showWarning);
                WarnPlayer(player.userID, WarningType.Healing, config.healingSettings.showWarning);
                if (config.healingSettings.logtoDiscord)
                {
                    SendToDiscord(reviver.userID, pdata, WarningType.Healing, new HashSet<ulong> { player.userID }, config.healingSettings.embedColor, $"{reviver.userID} revived {player.userID}");
                }
            }
        }

        private void OnPlayerRevive(BasePlayer reviver, BasePlayer player)
        {
            if (IsWhitelisted(reviver.userID) || IsWhitelisted(player.userID))
                return;

            if (reviver.userID == player.userID || reviver.userID == 0 || player.userID == 0)
            {
                return;
            }

            PlayerData? pdata = AddAssociationData(reviver.userID, player.userID, config.healingSettings.weighting);
            AddAssociationData(player.userID, reviver.userID, config.healingSettings.weighting);

            if (pdata == null)
                return;

            if (maxPlayers == 1 || TeamMembersCount(reviver) >= maxPlayers && !HasTeamMember(reviver, player.userID))
            {
                WarnPlayer(reviver.userID, WarningType.Healing, config.healingSettings.showWarning);
                WarnPlayer(player.userID, WarningType.Healing, config.healingSettings.showWarning);
                if (config.healingSettings.logtoDiscord)
                {
                    SendToDiscord(reviver.userID, pdata, WarningType.Healing, new HashSet<ulong> { player.userID }, config.healingSettings.embedColor, $"{reviver.userID} healed {player.userID}");
                }
            }

        }

        #endregion HealingChecks

        #region VehicleChecks
        private LayerMask playerlayer = LayerMask.GetMask("Player (Server)");
        private uint[] _skipMounts = new uint[] { 3524763474, 2254147427, 1863405911 };
        Collider[] colBuffer = new Collider[8];
        void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {

            if (!IsSteamId(player.userID) || player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone) || IsWhitelisted(player.userID))
                return;

            if (_skipMounts.Contains(entity.prefabID)) //skip caboose mounts
                return;

            var parent = entity.GetParentEntity();
            if (parent == null)
                return;

            int len = Physics.OverlapSphereNonAlloc(player.transform.position, 3f, colBuffer, playerlayer);

            if (len == 0)
                return;

            HashSet<ulong> mountedPlayers = Pool.Get<HashSet<ulong>>();

            int nteam = 0;
            for (int i = 0; i < len; i++)
            {
                var ent = colBuffer[i];
                BasePlayer otherplayer = ent.GetComponent<BasePlayer>();

                if (otherplayer == null || otherplayer.userID == player.userID || !IsSteamId(otherplayer.userID))
                    continue;

                if (!otherplayer.isMounted && !otherplayer.HasParent())
                    continue;

                if (IsWhitelisted(otherplayer.userID))
                    continue;

                mountedPlayers.Add(otherplayer.userID);

                if (!HasTeamMember(player, otherplayer))
                {
                    nteam++;
                }
            }

            mountedPlayers.Add(player.userID);

            PlayerData? data = AddAssociationDataAll(player.userID, mountedPlayers, config.vehicleSettings.weighting);

            if (TeamMembersCount(player) + nteam > maxPlayers)
            {
                WarnPlayer(player.userID, WarningType.Vehicle, config.vehicleSettings.showWarning);
                WarnPlayers(mountedPlayers, WarningType.Vehicle, config.vehicleSettings.showWarning);

                if (config.vehicleSettings.logtoDiscord)
                    SendToDiscord(player.userID, data, WarningType.Vehicle, mountedPlayers, config.vehicleSettings.embedColor, $"{player.userID} mounted a {entity.ShortPrefabName} with {mountedPlayers.Count - 1} other players, {nteam} players were not in the team.");
            }

            Pool.FreeUnmanaged(ref mountedPlayers);
        }
        #endregion VehicleChecks

        #region Proximity Checks
        private void RunProximityChecks()
        {
            List<BasePlayer> activeplayers = Pool.Get<List<BasePlayer>>();

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (ShouldSkipPlayer(player))
                    continue;
                activeplayers.Add(player);
            }

            foreach (var baseplayer in activeplayers)
            {
                if (!_proximityticks.TryGetValue(baseplayer.userID, out Dictionary<ulong, int>? data))
                {
                    data = new Dictionary<ulong, int>();
                    _proximityticks.Add(baseplayer.userID, data);
                }

                foreach (var otherplayer in activeplayers)
                {
                    if (baseplayer.userID == otherplayer.userID)
                        continue;
                    if ((baseplayer.lastGroundedPosition - otherplayer.lastGroundedPosition).sqrMagnitude > proximityDistanceSqrd)
                        continue;

                    if (config.proximitySettings.proximityVision && !Vischeck(baseplayer, otherplayer))
                        continue;

                    if (!data.TryGetValue(otherplayer.userID, out int tick))
                    {
                        tick = 1;
                        data.Add(otherplayer.userID, tick);
                    }
                    else
                    {
                        tick++;
                        data[otherplayer.userID] = tick;
                    }
                }
                if (data.Count < maxPlayers)
                    continue;

                int ticks = 0;
                foreach (var tick in data)
                {
                    if (tick.Value < config.proximitySettings.proximityAlertTicks)
                    {
                        continue;
                    }
                    ticks++;
                }
                if (ticks < maxPlayers)
                    continue;

                float hoursconnected = GetHoursConnected(baseplayer.userID);

                HashSet<ulong> players = Pool.Get<HashSet<ulong>>();
                foreach (var tick1 in data)
                {
                    if (tick1.Value < config.proximitySettings.proximityAlertTicks)
                        continue;

                    players.Add(tick1.Key);

                    if (!_playerData.TryGetValue(tick1.Key, out var pdata2))
                    {
                        pdata2 = new PlayerData() { PlayerID = tick1.Key };
                        _playerData.Add(tick1.Key, pdata2);
                    }

                    AddAssociation(pdata2, baseplayer.userID, config.proximitySettings.weighting * (hoursconnected < 1 ? 1 : config.proximitySettings.onlineTimeFactor / 100 * hoursconnected));
                }

                foreach (var player in players)
                {
                    data.Remove(player);
                }

                PlayerData? pdata = AddAssociationData(baseplayer.userID, players, config.proximitySettings.weighting * (hoursconnected < 1 ? 1 : config.proximitySettings.onlineTimeFactor / 100 * hoursconnected));

                if (pdata == null)
                {
                    Pool.FreeUnmanaged(ref players);
                    continue;
                }

                WarnPlayers(players, WarningType.Proximity, config.proximitySettings.showWarning);
                WarnPlayer(baseplayer.userID, WarningType.Proximity, config.proximitySettings.showWarning);

                if (config.proximitySettings.logtoDiscord)
                {
                    SendToDiscord(baseplayer.userID, pdata, WarningType.Proximity, players, config.proximitySettings.embedColor);
                }
                Pool.FreeUnmanaged(ref players);
            }
            Pool.FreeUnmanaged(ref activeplayers);
        }

        private bool ShouldSkipPlayer(BasePlayer player)
        {
            return player.IsNoob() || player.modelState.sleeping || player.IsDead() || player._limitedNetworking || player.modelState.crawling || !player.modelState.onground || player.HasPlayerFlag(BasePlayer.PlayerFlags.SafeZone) || !IsSteamId(player.userID);
        }

        private float GetHoursConnected(ulong userId)
        {
            if (_timeconnected.TryGetValue(userId, out var connectedTime))
            {
                var playerDataTime = _playerTimeData.TryGetValue(userId, out var playerTime) ? playerTime : 0;
                return (Time.time - connectedTime + playerDataTime) / 3600f;
            }
            return 0;
        }

        private static LayerMask CheckPlayerVisMask = LayerMask.GetMask("Deployed", "Construction", "World");
        private static Vector3 _V3up;
        private static bool Vischeck(BasePlayer basePlayer1, BasePlayer basePlayer2)
        {
            float distSquared = (basePlayer2.lastGroundedPosition - basePlayer1.lastGroundedPosition).sqrMagnitude;

            if (distSquared < 1)
                return true;

            if (!Physics.Raycast(basePlayer1.lastGroundedPosition + _V3up, (basePlayer2.lastGroundedPosition - basePlayer1.lastGroundedPosition).normalized, Mathf.Sqrt(distSquared), CheckPlayerVisMask))
                return true;

            return false;
        }

        #endregion Proximity Checks

        #region Raiding

        private Dictionary<ulong, List<RaidInfo>> _raidingData = new Dictionary<ulong, List<RaidInfo>>();
        private struct RaidInfo
        {
            public ulong attacker;
            public float time;
        }

        [HarmonyPatch(typeof(ItemModProjectileRadialDamage), "ServerProjectileHit")] // Change this to Die
        public static class BuildingPrivlidge_ServerProjectileHit_Patch
        {
            [HarmonyPrefix]
            public static void Prefix(ItemModProjectileRadialDamage __instance, HitInfo info)
            {
                if (info == null) return;
                _teamTracker.OnExplosiveAmmo(info);
            }
        }
        private const string explosiveammo = "riflebullet_explosive";
        private void OnExplosiveAmmo(HitInfo info)
        {
            if (info?.Initiator == null || info.ProjectilePrefab == null)
                return;

            if (info.ProjectilePrefab.name != explosiveammo)
                return;

            BasePlayer player = info.Initiator as BasePlayer;
            if (IsWhitelisted(player.userID))
                return;

            if (info.HitEntity is DecayEntity decayEntity)
            {
                BuildingPrivlidge buildingPrivlidge = decayEntity.GetBuildingPrivilege();
                if (buildingPrivlidge == null || buildingPrivlidge.cachedProtectedMinutes < 1 || buildingPrivlidge.IsAuthed(player))
                    return;
                LogRaider(player, buildingPrivlidge);
            }
        }
        private void OnExplosiveThrown(BasePlayer player, BaseEntity baseEntity, ThrownWeapon instance)
        {
            if (IsWhitelisted(player.userID))
                return;

            BuildingPrivlidge buildingPrivlidge = player.GetBuildingPrivilege();
            if (buildingPrivlidge == null || buildingPrivlidge.cachedProtectedMinutes < 1 || buildingPrivlidge.IsAuthed(player))
                return;

            LogRaider(player, buildingPrivlidge);
        }

        private void OnRocketLaunched(BasePlayer player, BaseEntity baseEntity)
        {
            if (IsWhitelisted(player.userID))
                return;

            Transform rocketTransform = baseEntity.transform;
            RaycastHit raycastHit;
            if (!Physics.Raycast(player.eyes.HeadRay(), out raycastHit, 150f, 1237003025))
            {
                return;
            }

            BuildingPrivlidge? buildingPrivlidge = raycastHit.GetEntity()?.GetBuildingPrivilege();
            if (buildingPrivlidge == null || buildingPrivlidge.cachedProtectedMinutes < 1 || buildingPrivlidge.IsAuthed(player))
                return;

            LogRaider(player, buildingPrivlidge);
        }

        private void LogRaider(BasePlayer player, BuildingPrivlidge buildingPrivlidge)
        {
            RaidInfo raidInfo = new RaidInfo { attacker = player.userID, time = Time.time };
            if (!_raidingData.TryGetValue(buildingPrivlidge.buildingID, out List<RaidInfo>? raidinfos))
            {
                raidinfos = new List<RaidInfo> { raidInfo };
                _raidingData.Add(buildingPrivlidge.buildingID, raidinfos);
                return;
            }

            raidinfos.Add(raidInfo);

            if (raidinfos.Count < maxPlayers)
            {
                return;
            }

            HashSet<ulong> raidingPlayers = Pool.Get<HashSet<ulong>>();
            for (int i = raidinfos.Count - 1; i >= 0; i--)
            {
                var raid = raidinfos[i];
                if (raid.time < Time.time - 20)
                {
                    raidinfos.RemoveAt(i);
                    continue;
                }
                raidingPlayers.Add(raid.attacker);
            }

            PlayerData? data = AddAssociationDataAll(player.userID, raidingPlayers, config.raidingSettings.weighting);
            if (raidingPlayers.Count <= maxPlayers)
            {
                Pool.FreeUnmanaged(ref raidingPlayers);
                return;
            }

            WarnPlayers(raidingPlayers, WarningType.Raiding, config.raidingSettings.showWarning);

            if (config.raidingSettings.logtoDiscord)
            {
                SendToDiscord(player.userID, data, WarningType.Raiding, raidingPlayers, config.raidingSettings.embedColor);
            }

            raidinfos.Clear();
            Pool.FreeUnmanaged(ref raidingPlayers);
        }
        #endregion Raiding

        #region LootingChecks

        private Dictionary<ulong, HashSet<ulong>> _lootingPlayers = new Dictionary<ulong, HashSet<ulong>>();
        void OnLootEntity(BasePlayer player, PlayerCorpse target)
        {
            if (player == null)
                return;

            if (player.IsNoob() || player.InSafeZone() || player.IsBuildingAuthed() || IsWhitelisted(player.userID))
                return;

            int len = Physics.OverlapSphereNonAlloc(player.transform.position, 3f, colBuffer, playerlayer);

            if (len == 0)
                return;

            HashSet<ulong> nearbyPlayers = Pool.Get<HashSet<ulong>>();

            int nteam = 0;
            for (int i = 0; i < len; i++)
            {
                var ent = colBuffer[i];
                BasePlayer otherplayer = ent.GetComponent<BasePlayer>();

                if (player.userID.Get() == target.playerSteamID)
                    continue;

                if (otherplayer == null || otherplayer.userID == player.userID || otherplayer.IsNoob() || !IsSteamId(otherplayer.userID) || IsWhitelisted(player.userID))
                    continue;

                if ((otherplayer.inventory?.loot?.entitySource?.net?.connection?.userid ?? 0) != target.OwnerID)
                    continue;

                if (!HasTeamMember(player, otherplayer))
                {
                    nteam++;
                }
                nearbyPlayers.Add(otherplayer.userID);
            }

            if (nearbyPlayers.Count == 0)
            {
                Pool.FreeUnmanaged(ref nearbyPlayers);
                return;
            }

            nearbyPlayers.Add(player.userID);

            PlayerData? data = AddAssociationDataAll(player.userID, nearbyPlayers, config.lootingSettings.weighting);

            if (TeamMembersCount(player) + nteam > maxPlayers)
            {
                WarnPlayers(nearbyPlayers, WarningType.Looting, config.lootingSettings.showWarning);
                if (config.lootingSettings.logtoDiscord)
                    SendToDiscord(player.userID, data, WarningType.Looting, nearbyPlayers, config.lootingSettings.embedColor, $"{player.userID} is looting {target.playerSteamID} with {nearbyPlayers.Count} other players, {nteam} players were not in the team.");
            }
            Pool.FreeUnmanaged(ref nearbyPlayers);
        }

        #endregion LootingChecks

        #region PVPing
        private void OnPlayerWound(BasePlayer BasePlayer, HitInfo HitInfo) => HandlePvpState(BasePlayer, HitInfo);

        private void OnPlayerDeath(BasePlayer targetPlayer, HitInfo info)
        {
            if (targetPlayer.IsWounded())
                return;
            HandlePvpState(targetPlayer, info);
        }

        private void HandlePvpState(BasePlayer targetPlayer, HitInfo info)
        {
            BasePlayer? attacker = info?.InitiatorPlayer;
            if (info == null || attacker == null)
                return;

            if (IsWhitelisted(attacker.userID))
                return;

            HashSet<ulong> pvpHistory = Pool.Get<HashSet<ulong>>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player.userID == attacker.userID
                    || player.userID == targetPlayer.userID
                    || IsWhitelisted(player.userID)
                    || ShouldSkipPlayer(player)
                    || Time.time - player.lastDealtDamageTime > 20f
                    || (player.lastGroundedPosition - attacker.lastGroundedPosition).sqrMagnitude > proximityDistanceSqrd
                    || !Vischeck(player, targetPlayer)
                    || HasTeamMember(player, attacker))
                    continue;
                pvpHistory.Add(player.userID);
            }

            if (pvpHistory.Count < maxPlayers)
            {
                Pool.FreeUnmanaged(ref pvpHistory);
                return;
            }

            pvpHistory.Add(attacker.userID);
            PlayerData? data = AddAssociationDataAll(attacker.userID, pvpHistory, config.pvpSettings.weighting);

            WarnPlayers(pvpHistory, WarningType.Pvping, config.pvpSettings.showWarning);

            if (config.pvpSettings.logtoDiscord)
            {
                SendToDiscord(attacker.userID, data, WarningType.Pvping, pvpHistory, config.pvpSettings.embedColor);
            }
            Pool.FreeUnmanaged(ref pvpHistory);
        }
        #endregion PVPing

        #endregion Checks

        #region SendWarnings
        public enum WarningType
        {
            Proximity,
            Turret,
            Team,
            Toolcupboard,
            Bag,
            CodeLock,
            Healing,
            Vehicle,
            Ban,
            Raiding,
            Pvping,
            Looting
        }

        private void AddWarning(PlayerData playerData, WarningType warningType)
        {
            playerData.WarningCount++;
            if (playerData._warnings.TryGetValue(warningType, out var warning))
            {
                playerData._warnings[warningType] = warning + 1;
            }
            else
            {
                playerData._warnings.Add(warningType, 1);
            }
        }

        private void WarnPlayers(HashSet<ulong> players, WarningType warningType, bool showWarning)
        {
            foreach (var player in players)
            {
                WarnPlayer(player, warningType, showWarning);
            }
        }
        private void WarnPlayer(ulong playerid, WarningType warningType, bool showWarning)
        {
            if (config._onlinePlayersOnly && _playerInfo.TryGetValue(playerid, out var playerInfo) && !playerInfo.online)
            {
                return;
            }

            if (!_playerData.TryGetValue(playerid, out var pdata))
            {
                pdata = new PlayerData() { PlayerID = playerid };
                _playerData.Add(playerid, pdata);
            }

            AddWarning(pdata, warningType);

            if (config.banSettings.maxWarningsBan && pdata.WarningCount >= config.banSettings.maxWarnings || config.banSettings.uniqueWarningsBan && pdata._warnings.Count >= config.banSettings.uniqueWarnings)
            {
                string warnings = string.Empty;
                foreach (var warning in pdata._warnings)
                {
                    warnings += $"{warning},";
                }
                var mainPlayerinfo = GetPlayerInfo(playerid);

                StringBuilder stringBuilder = _stringBuilder;
                stringBuilder.Clear();

                stringBuilder.Append($"Ban by Warning Limits\n**Player:**{mainPlayerinfo?.name}-{pdata.PlayerID}\n**Warnings:**{warnings}\n**Associated Players:**\n");
                List<KeyValuePair<ulong, float>> associatedPlayersList = Pool.Get<List<KeyValuePair<ulong, float>>>();
                associatedPlayersList.AddRange(pdata._associatedPlayers.OrderByDescending(kv => kv.Value));
                foreach (var player in associatedPlayersList)
                {
                    PlayerInfo? playerinfo = GetPlayerInfo(player.Key);

                    if (playerinfo == null) continue;
                    if (player.Value < config.banSettings.weightingMin)
                    {
                        break;
                    }
                    string pnotes = $"{playerinfo.name} - {player.Key} **[{player.Value}]** \n";
                    if (stringBuilder.Length + pnotes.Length > 1024)
                    {
                        break;
                    }
                    stringBuilder.Append(pnotes);
                }
                Pool.FreeUnmanaged(ref associatedPlayersList);
                BanPlayer(playerid, banText, stringBuilder.ToString());
                return;
            }

            if (!showWarning)
                return;

            ServerMgr.Instance.Invoke(() => SendWarningNote(playerid), UnityEngine.Random.Range(5, 30));
        }

        private void SendWarningNote(ulong playerid)
        {
            BasePlayer player = BasePlayer.FindByID(playerid);

            if (player == null || !player.IsConnected || player.IsSleeping())
                return;

            if (string.IsNullOrEmpty(config.warningSettings.warningText))
            {
                return;
            }
            player.SendConsoleCommand("gametip.showgametip", config.warningSettings.useLang ? GetLang("WarningText", player.UserIDString) : string.Format(config.warningSettings.warningText, maxPlayers));
            ServerMgr.Instance.Invoke(() => player?.SendConsoleCommand("gametip.hidegametip"), config.warningSettings.warningShowTime);
        }
        #endregion SendWarnings

        #region SendToDiscord

        private string PreviousWarnings(PlayerData playerData)
        {
            if (playerData._warnings.Count == 0)
            {
                return "None";
            }

            StringBuilder warnings = _stringBuilder;
            warnings.Clear();

            warnings.Append($"**[{playerData.WarningCount}]** ");

            foreach (var warning in playerData._warnings)
            {
                warnings.Append($"{warning.Key}:{warning.Value},");
            }

            if (warnings.Length > 0)
            {
                warnings.Length--; //remove the last comma
            }

            return warnings.ToString();
        }
        private PlayerInfo? GetPlayerInfo(ulong playerID)
        {
            if (_playerInfo.TryGetValue(playerID, out var playerInfo))
            {
                return playerInfo;
            }
            IPlayer iplayer = covalence.Players.FindPlayerById(playerID.ToString());
            if (iplayer != null)
            {
                return new PlayerInfo() { steamid = playerID, name = iplayer.Name, online = iplayer.IsConnected };
            }
            return null;
        }

        private RelationshipManager.PlayerTeam GetPlayerTeam(ulong playerID)
        {
            return RelationshipManager.ServerInstance.FindPlayersTeam(playerID);
        }

        private string AssociatedPlayers(PlayerData playerData, HashSet<ulong>? warningPlayers = null)
        {
            var team = GetPlayerTeam(playerData.PlayerID);
            List<KeyValuePair<ulong, float>> associatedPlayersList = Pool.Get<List<KeyValuePair<ulong, float>>>();
            associatedPlayersList.AddRange(playerData._associatedPlayers.OrderByDescending(kv => kv.Value));

            StringBuilder associatedPlayers = _stringBuilder;
            associatedPlayers.Clear();

            int characterCount = 0;

            foreach (var player in associatedPlayersList)
            {
                if (player.Value < config.banSettings.weightingMin)
                    continue;

                if (warningPlayers?.Contains(player.Key) ?? false)
                    continue;

                PlayerInfo? playerinfo = GetPlayerInfo(player.Key);

                string playerInfo;
                if (playerinfo == null)
                {
                    playerInfo = $"{unknownEmoji}[{player.Key}]({ProfileLink(player.Key)}) **[{player.Value}]** \n";
                }
                else
                {
                    string playerStatus = playerinfo.online ? onlineEmoji : offlineEmoji;

                    if (team != null && team.members.Contains(player.Key))
                    {
                        playerInfo = $"{playerStatus}{teamMemberEmoji} [{playerinfo.name}]({ProfileLink(playerinfo.steamid)}) - {playerinfo.steamid} **[{player.Value}]** \n";
                    }
                    else
                    {
                        playerInfo = $"{playerStatus} [{playerinfo.name}]({ProfileLink(playerinfo.steamid)}) - {playerinfo.steamid} **[{player.Value}]** \n";
                    }
                }

                if (characterCount + playerInfo.Length > 1010)
                {
                    associatedPlayers.Append("more...");
                    break;
                }

                associatedPlayers.Append(playerInfo);
                characterCount += playerInfo.Length;
            }

            if (associatedPlayers.Length == 0)
            {
                associatedPlayers.Append("None");
            }

            Pool.FreeUnmanaged(ref associatedPlayersList);
            return associatedPlayers.ToString();
        }

        private string TeleportFormat(IPlayer player)
        {
            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer != null)
            {
                return $"```teleportpos {basePlayer.transform.position}```";
            }
            return $"```teleportpos {player?.Position()}```";
        }

        private List<string> WarningPlayers(ulong target, HashSet<ulong> playerIDs, PlayerData? playerData = null)
        {
            List<string> warningdata = Pool.Get<List<string>>();
            if (playerData == null)
            {
                if (!_playerData.TryGetValue(target, out playerData))
                {
                    warningdata.Add("None");
                    return warningdata;
                }
            }

            BasePlayer? targetPlayer = BasePlayer.FindByID(target);
            if (targetPlayer == null)
            {
                warningdata.Add("None");
                return warningdata;
            }
            var team = GetPlayerTeam(playerData.PlayerID);

            StringBuilder warningPlayers = _stringBuilder;
            warningPlayers.Clear();

            string targetPlayerStatus = targetPlayer.IsConnected ? onlineEmoji : offlineEmoji;

            string targetPlayerInfo = $"{targetPlayerStatus}{mainMemberEmoji} [{targetPlayer.displayName}]({ProfileLink(targetPlayer.userID)}) - {targetPlayer.userID} \n";

            warningPlayers.Append(targetPlayerInfo);

            foreach (var player in playerIDs)
            {
                if (player == target)
                    continue;

                PlayerInfo? playerinfo = GetPlayerInfo(player);
                string playerInfo;
                float association = playerData._associatedPlayers.TryGetValue(player, out float value) ? value : 0f;

                if (playerinfo == null)
                {
                    playerInfo = $"{unknownEmoji} [{player}]({ProfileLink(player)}) **[{association}]** \n";
                }
                else
                {
                    string playerStatus = playerinfo.online ? onlineEmoji : offlineEmoji;
                    if (team != null && team.members.Contains(player))
                    {
                        playerInfo = $"{playerStatus}{teamMemberEmoji} [{playerinfo.name}]({ProfileLink(playerinfo.steamid)}) - {playerinfo.steamid} **[{association}]** \n";
                    }
                    else
                    {
                        playerInfo = $"{playerStatus} [{playerinfo.name}]({ProfileLink(playerinfo.steamid)}) - {playerinfo.steamid} **[{association}]** \n";
                    }
                }
                if (warningPlayers.Length + playerInfo.Length > 1010)
                {
                    warningdata.Add(warningPlayers.ToString());
                    warningPlayers.Clear();
                    warningPlayers.Append(playerInfo);
                    continue;
                }
                warningPlayers.Append(playerInfo);
            }

            if (warningPlayers.Length > 0)
            {
                warningdata.Add(warningPlayers.ToString());
            }
            return warningdata;
        }

        private void SendToDiscord(ulong playerID, PlayerData playerData, WarningType warningType, HashSet<ulong> warningplayers, string embedColor = "0", string warningMsg = "")
        {
            if (playerData == null)
            {
                Puts($"Couldn't find player {playerID} in Send to Discord {warningType}");
                return;
            }
            IPlayer player = covalence.Players.FindPlayerById(playerID.ToString());
            if (player == null)
            {
                Puts($"Couldn't find player {playerID} in Send to Discord");
                return;
            }

            string connectedtime = TimeSpan.FromSeconds((_timeconnected.ContainsKey(playerID) ? Time.time - _timeconnected[playerID] : 0) + (_playerTimeData.ContainsKey(playerID) ? _playerTimeData[playerID] : 0)).ToString(@"hh\:mm");
            List<string> warningPlayers = WarningPlayers(playerID, warningplayers, playerData);
            List<DiscordMessage.Fields> fields = new List<DiscordMessage.Fields>()
            {
                new DiscordMessage.Fields(config.langSettings.playerSubtitle, $":{config.discordSettings.mainMemberEmoji}:[{player.Name}]({ProfileLink(playerID)}{playerID})", true),
                new DiscordMessage.Fields(config.langSettings.userIDSubtitle, $"{playerID}", true),
                new DiscordMessage.Fields(config.langSettings.timePlayedSubtitle, $"{connectedtime}", true),
                new DiscordMessage.Fields(config.langSettings.posSubtitle, $"{TeleportFormat(player)}", false),
                new DiscordMessage.Fields(config.langSettings.previousSubtitle, PreviousWarnings(playerData), false),
                new DiscordMessage.Fields(config.langSettings.warningsSubtitle, warningPlayers.Count > 0 ? warningPlayers[0] : "None", false)
            };

            if (warningPlayers.Count > 1)
            {
                fields.Add(new DiscordMessage.Fields(config.langSettings.warningsContSubtitle, warningPlayers[1], false));
            }

            if (config.discordSettings.associatedPlayers)
            {
                fields.Add(new DiscordMessage.Fields(config.langSettings.associatedPlayersSubtitle, AssociatedPlayers(playerData, warningplayers), false));
            }
            if (config.discordSettings.teamPlayers)
            {
                fields.Add(new DiscordMessage.Fields(config.langSettings.recordedTeam, CurrentTeam(playerData), false));
            }
            if (!string.IsNullOrEmpty(warningMsg))
            {
                fields.Add(new DiscordMessage.Fields(config.langSettings.warningsNotesSubtitle, warningMsg, false));
            }
            Pool.FreeUnmanaged(ref warningPlayers);

            string payload = SerializeDiscordMessage(config.discordSettings.userName, config.discordSettings.authorIcon, string.Format(config.langSettings.warningTitle, warningType, serverName), fields, DateTime.Now.ToString("dddd, MMMM d, yyyy h:mm tt"), embedColor);

            payload2Send.Enqueue((payload, config.discordSettings.discordWebhook));
            if (payloadCoroutine == null)
            {
                payloadCoroutine = SendPayload();
                ServerMgr.Instance.StartCoroutine(payloadCoroutine);
            }
        }

        private string CurrentTeam(PlayerData playerData)
        {
            var team = GetPlayerTeam(playerData.PlayerID);

            StringBuilder currentteam = _stringBuilder;
            currentteam.Clear();

            foreach (var member in playerData._teamHistory)
            {
                if (team != null && team.members.Contains(member))
                {
                    currentteam.Append($"{teamMemberEmoji} [{member}]({ProfileLink(member)}) \n");
                }
                else
                {
                    currentteam.Append($"[{member}]({ProfileLink(member)}) \n");
                }
            }
            if (currentteam.Length == 0)
            {
                return "None";
            }
            return currentteam.ToString();
        }

        private void SendRequestDiscord(ulong playerID, PlayerData playerData)
        {
            string connectedtime = TimeSpan.FromSeconds((_timeconnected.ContainsKey(playerID) ? Time.time - _timeconnected[playerID] : 0) + (_playerTimeData.ContainsKey(playerID) ? _playerTimeData[playerID] : 0)).ToString(@"hh\:mm");
            PlayerInfo? playerinfo = GetPlayerInfo(playerID);

            List<DiscordMessage.Fields> fields = new List<DiscordMessage.Fields>()
            {
                new DiscordMessage.Fields(config.langSettings.playerSubtitle, $":{config.discordSettings.mainMemberEmoji}:[{playerinfo?.name}]({ProfileLink(playerID)})", true),
                new DiscordMessage.Fields(config.langSettings.userIDSubtitle, $"{playerID}", true),
                new DiscordMessage.Fields(config.langSettings.timePlayedSubtitle, $"{connectedtime}", true),
                new DiscordMessage.Fields(config.langSettings.previousSubtitle, PreviousWarnings(playerData), false),
                new DiscordMessage.Fields(config.langSettings.associatedPlayersSubtitle, AssociatedPlayers( playerData), false),
                new DiscordMessage.Fields(config.langSettings.currentTeam, CurrentTeam(playerData), false)
            };

            string payload = SerializeDiscordMessage(config.discordSettings.userName, config.discordSettings.authorIcon, string.Format(config.langSettings.requestTitle, serverName), fields, DateTime.Now.ToString("dddd, MMMM d, yyyy h:mm tt"), config.discordSettings.embedColor);
            payload2Send.Enqueue((payload, config.discordSettings.discordWebhookTeamChecks));
            if (payloadCoroutine == null)
            {
                payloadCoroutine = SendPayload();
                ServerMgr.Instance.StartCoroutine(payloadCoroutine);
            }
        }

        private void SendRequestRCON(ulong playerID, PlayerData playerData)
        {
            BasePlayer player1 = BasePlayer.FindByID(playerID);
            if (player1 == null)
            {
                Puts($"Couldn't find player {playerID} in Send to Discord");
                return;
            }
            StringBuilder stringBuilder = _stringBuilder;
            stringBuilder.Clear();
            stringBuilder.Append($"Team Request {playerID}");
            string connectedtime = TimeSpan.FromSeconds((_timeconnected.ContainsKey(playerID) ? Time.time - _timeconnected[playerID] : 0) + (_playerTimeData.ContainsKey(playerID) ? _playerTimeData[playerID] : 0)).ToString(@"hh\:mm");
            stringBuilder.Append($"Time Played {connectedtime}");
            stringBuilder.Append($"Previous Warnings: {PreviousWarnings(playerData)}");
            stringBuilder.Append($"Associated Players: {AssociatedPlayers(playerData)}");
            stringBuilder.Append($"Recorded Team Members: {CurrentTeam(playerData)}");
            Puts(stringBuilder.ToString());
        }

        private Queue<(string, string)> payload2Send = new Queue<(string, string)>();
        IEnumerator payloadCoroutine = null;
        IEnumerator SendPayload()
        {
            for (int i = 0; i < payload2Send.Count; i++)
            {
                var payload = payload2Send.Dequeue();
                UnityWebRequest www = new UnityWebRequest(payload.Item2, "POST");
                byte[] jsonToSend = Encoding.UTF8.GetBytes(payload.Item1);

                www.uploadHandler = new UploadHandlerRaw(jsonToSend);

                www.SetRequestHeader("Content-Type", "application/json");
                www.timeout = 5;

                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Puts($"Error sending Discord Message: {www.error}");
                    //Puts($"payload:{payload.Item1}");
                }
                www.Dispose();
                yield return CoroutineEx.waitForSeconds(3f);
            }
            payloadCoroutine = null;
        }

        #region DiscordEmbedClass
        public class DiscordMessage
        {
            [JsonProperty("username")]
            public string Username { get; set; }

            [JsonProperty("avatar_url")]
            public string AvatarUrl { get; set; }

            [JsonProperty("embeds")]
            public List<Embeds> EmbedsList { get; set; }

            public class Fields
            {
                [JsonProperty("name")]
                public string Name { get; set; }

                [JsonProperty("value")]
                public string Value { get; set; }

                [JsonProperty("inline")]
                public bool Inline { get; set; }

                public Fields(string name, string value, bool inline)
                {
                    Name = name;
                    Value = value;
                    Inline = inline;
                }
            }

            public class Footer
            {
                [JsonProperty("text")]
                public string Text { get; set; }

                public Footer(string text)
                {
                    Text = text;
                }
            }

            public class Embeds
            {
                [JsonProperty("title")]
                public string Title { get; set; }

                [JsonProperty("color")]
                public string Color { get; set; }

                [JsonProperty("fields")]
                public List<Fields> Fields { get; set; }

                [JsonProperty("footer")]
                public Footer Footer { get; set; }

                public Embeds(string title, List<Fields> fields, Footer footer)
                {
                    Title = title;
                    Fields = fields;
                    Footer = footer;
                }
            }

            public DiscordMessage(string username, string avatar_url, List<Embeds> embeds)
            {
                Username = username;
                AvatarUrl = avatar_url;
                EmbedsList = embeds;
            }
        }

        #endregion DiscordEmbedClass


        #endregion SendToDiscord

        #region Bans
        private void BanPlayer(ulong steamID, string reason, string notes)
        {
            IPlayer player = covalence.Players.FindPlayerById(steamID.ToString());
            if (player == null)
            {
                Puts($"Player {steamID} couldn't be found, no ban was applied");
                return;
            }

            if (config.banSettings.logBans)
            {
                if (_playerData.TryGetValue(steamID, out var pdata) && !pdata.Banned)
                {
                    pdata.Banned = true;
                    LogBan(player, reason, notes, pdata);
                }
            }

            if (!config.banSettings.enabled)
                return;

            if (config.banSettings.localBan)
            {
                server.Ban(steamID.ToString(), reason, TimeSpan.FromHours(config.banSettings.banDuration));
            }

            if (config.bmSettings.battlemetricsBan)
            {
                if (string.IsNullOrEmpty(config.bmSettings.APIKey))
                {
                    Puts("No API Key set for BattleMetrics Ban");
                }
                else
                {
                    ServerMgr.Instance.StartCoroutine(BMBan(steamID, reason, notes));
                }
            }
        }
        #endregion Bans

        #region BattleMetrics

        #region Methods
        private void LogBan(IPlayer player, string reason, string notes, PlayerData playerData)
        {
            ulong playerID = ulong.Parse(player.Id);
            string connectedtime = TimeSpan.FromSeconds((_timeconnected.ContainsKey(playerID) ? Time.time - _timeconnected[playerID] : 0) + (_playerTimeData.ContainsKey(playerID) ? _playerTimeData[playerID] : 0)).ToString(@"hh\:mm");

            List<DiscordMessage.Fields> fields = new List<DiscordMessage.Fields>()
            {
                new DiscordMessage.Fields(config.langSettings.playerSubtitle, $":{config.discordSettings.mainMemberEmoji}:[{player.Name}]({ProfileLink(playerID)})", true),
                new DiscordMessage.Fields(config.langSettings.userIDSubtitle, $"{playerID}", true),
                new DiscordMessage.Fields(config.langSettings.timePlayedSubtitle, $"{connectedtime}", true),
                new DiscordMessage.Fields(config.langSettings.warningsSubtitle, PreviousWarnings(playerData), false),
                new DiscordMessage.Fields(config.langSettings.associatedPlayersSubtitle, AssociatedPlayers(playerData), false),
                new DiscordMessage.Fields(config.langSettings.banNotesSubtitle, $"{notes}", false)
            };

            string payload = SerializeDiscordMessage(config.discordSettings.userName, config.discordSettings.authorIcon, string.Format(config.langSettings.banTitle, serverName), fields, DateTime.Now.ToString("dddd, MMMM d, yyyy h:mm tt"), config.banSettings.embedColor);
            payload2Send.Enqueue((payload, config.banSettings.discordWebhook));
            if (payloadCoroutine == null)
            {
                payloadCoroutine = SendPayload();
                ServerMgr.Instance.StartCoroutine(payloadCoroutine);
            }
        }

        #region Pooling
        public class StringWriterPool
        {
            private static ConcurrentBag<StringWriter> _pool = new ConcurrentBag<StringWriter>();

            public static StringWriter Acquire()
            {
                if (!_pool.TryTake(out var writer))
                {
                    writer = new StringWriter();
                }
                else
                {
                    writer.GetStringBuilder().Clear();
                }
                return writer;
            }

            public static void Release(StringWriter writer)
            {
                _pool.Add(writer);
            }
        }

        public class JsonTextWriterPool
        {
            private static ConcurrentBag<JsonTextWriter> _pool = new ConcurrentBag<JsonTextWriter>();

            public static JsonTextWriter Acquire(StringWriter writer)
            {
                if (!_pool.TryTake(out var jsonWriter))
                {
                    jsonWriter = new JsonTextWriter(writer);
                }
                return jsonWriter;
            }

            public static void Release(JsonTextWriter jsonWriter)
            {
                jsonWriter.Flush();
                _pool.Add(jsonWriter);
            }
        }


        #endregion Pooling

        private string SerializeDiscordMessage(string userName, string authorIcon, string title, List<DiscordMessage.Fields> fields, string footerText, string color)
        {

            StringWriter sw = StringWriterPool.Acquire();
            JsonTextWriter writer = JsonTextWriterPool.Acquire(sw);

            try
            {
                writer.WriteStartObject();

                writer.WritePropertyName("username");
                writer.WriteValue(userName);

                writer.WritePropertyName("avatar_url");
                writer.WriteValue(authorIcon);

                writer.WritePropertyName("embeds");
                writer.WriteStartArray();

                writer.WriteStartObject();

                writer.WritePropertyName("title");
                writer.WriteValue(title);

                writer.WritePropertyName("color");
                writer.WriteValue(color);

                writer.WritePropertyName("fields");
                writer.WriteStartArray();

                foreach (var field in fields)
                {
                    writer.WriteStartObject();

                    writer.WritePropertyName("name");
                    writer.WriteValue(field.Name);

                    writer.WritePropertyName("value");
                    writer.WriteValue(field.Value);

                    writer.WritePropertyName("inline");
                    writer.WriteValue(field.Inline);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WritePropertyName("footer");
                writer.WriteStartObject();

                writer.WritePropertyName("text");
                writer.WriteValue(footerText);
                writer.WriteEndObject();

                writer.WriteEndObject();
                writer.WriteEndArray();
                writer.WriteEndObject();

                return sw.ToString();
            }
            finally
            {
                StringWriterPool.Release(sw);
                JsonTextWriterPool.Release(writer);
            }
        }

        private IEnumerator BMBan(ulong steamID, string reason, string notes, bool retry = false)
        {
            PlayerDataRequest playerDataRequest = new PlayerDataRequest()
            {
                data = new List<DatumPD>()
               {
                new DatumPD()
                    {
                        attributes = new AttributesPD
                        {
                            identifier = steamID.ToString(),
                            type = "steamID"
                        },
                        type = "identifier"
                    }
                }
            };
            UnityWebRequest www = new UnityWebRequest("https://api.battlemetrics.com/players/quick-match", "POST");

            byte[] jsonToSend = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(playerDataRequest).ToString());
            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();

            www.SetRequestHeader("Authorization", $"Bearer {config.bmSettings.APIKey}");
            www.SetRequestHeader("Content-Type", "application/json");

            www.timeout = 5;
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Puts($"Error getting player data: {www.error}");
                if (!retry)
                {
                    Puts($"Couldn't ban player {steamID} retrying in 60 seconds");
                    timer.Once(60f, () => BMBan(steamID, reason, notes, true));
                }
                else
                {
                    server.Ban(steamID.ToString(), reason, TimeSpan.FromDays(config.banSettings.banDuration));
                    Puts($"Couldn't ban player {steamID} through BM, applying local ban");
                }
                www.Dispose();
                yield break;
            }
            PlayerMatchResult playerData;
            try
            {
                playerData = JsonConvert.DeserializeObject<PlayerMatchResult>(www.downloadHandler.text);
            }
            catch (Exception ex)
            {
                Puts($"Error deserializing player data: {ex.Message}");
                server.Ban(steamID.ToString(), reason, TimeSpan.FromDays(config.banSettings.banDuration));
                www.Dispose();
                yield break;
            }
            www.Dispose();

            BanClass banInfo = CreateBanPayload(steamID, reason, notes, playerData.Data[0].Relationships.Player.Data.Id);

            UnityWebRequest www2 = new UnityWebRequest("https://api.battlemetrics.com/bans", "POST");
            byte[] jsonToSend2 = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(banInfo).ToString());
            www2.uploadHandler = new UploadHandlerRaw(jsonToSend2);
            www2.SetRequestHeader("Authorization", $"Bearer {config.bmSettings.APIKey}");
            www2.SetRequestHeader("Content-Type", "application/json");
            www2.timeout = 5;

            yield return www2.SendWebRequest();
            if (www2.result != UnityWebRequest.Result.Success)
            {
                Puts($"Error sending ban request: {www2.error} {www2.result}");
                if (!retry)
                {
                    Puts($"Couldn't ban player {steamID} retrying in 60 seconds");
                    timer.Once(60f, () => BMBan(steamID, reason, notes, true));
                }
                else
                {
                    server.Ban(steamID.ToString(), reason, TimeSpan.FromDays(config.banSettings.banDuration));
                    Puts($"Error:28264 Couldn't ban player {steamID} through BM, applying local ban");
                }
                www2.Dispose();
                yield break;
            }
            www2.Dispose();

        }

        private BanClass CreateBanPayload(ulong steamID, string reason, string notes, string bmID)
        {
            BanClass banClass = new BanClass()
            {
                data = new Data2()
                {
                    type = "ban",
                    attributes = new Attributes2()
                    {
                        reason = reason,
                        note = notes,
                        expires = DateTime.UtcNow.Add(TimeSpan.FromDays(config.banSettings.banDuration)),
                        identifiers = new Identifier[]
                        {
                            new Identifier()
                            {
                                identifier = steamID.ToString(),
                                type = "steamID",
                                manual = true
                            }
                         },
                        orgWide = config.bmSettings.orgWide,
                        autoAddEnabled = config.bmSettings.autoAdd,
                        nativeEnabled = config.bmSettings.native
                    },
                    relationships = new Relationships2()
                    {
                        player = new Player2()
                        {
                            data = new Data1()
                            {
                                type = "player",
                                id = bmID
                            }
                        },
                        server = new Server()
                        {
                            data = new Data3()
                            {
                                type = "server",
                                id = config.bmSettings.serverID
                            }
                        }
                    }
                }
            };

            if (!string.IsNullOrEmpty(config.bmSettings.orgID))
            {
                banClass.data.relationships.organization = new Organization()
                {
                    data = new Data4()
                    {
                        id = config.bmSettings.orgID,
                        type = "organization"
                    }
                };
            }

            if (!string.IsNullOrEmpty(config.bmSettings.banListID))
            {
                banClass.data.relationships.banList = new Banlist()
                {
                    data = new Data5()
                    {
                        id = config.bmSettings.banListID,
                        type = "banList"
                    }
                };
            }
            return banClass;
        }

        #endregion Methods

        #region Ban Class


        public class BanClass
        {
            public Data2 data { get; set; }
        }

        public class Data2
        {
            public string type { get; set; }
            public Attributes2 attributes { get; set; }
            public Relationships2 relationships { get; set; }
        }

        public class Attributes2
        {
            public string reason { get; set; }
            public string note { get; set; }
            public DateTime expires { get; set; }
            public Identifier[] identifiers { get; set; }
            public bool orgWide { get; set; }
            public bool autoAddEnabled { get; set; }
            public bool nativeEnabled { get; set; }
        }

        public class Identifier
        {
            public string type { get; set; }
            public string identifier { get; set; }
            public bool manual { get; set; }
        }

        public class Relationships2
        {
            public Player2 player { get; set; }
            public Server server { get; set; }
            public Organization organization { get; set; }
            public Banlist banList { get; set; }
        }

        public class Player2
        {
            public Data1 data { get; set; }
        }

        public class Data1
        {
            public string type { get; set; }
            public string id { get; set; }
        }

        public class Server
        {
            public Data3 data { get; set; }
        }

        public class Data3
        {
            public string type { get; set; }
            public string id { get; set; }
        }

        public class Organization
        {
            public Data4 data { get; set; }
        }

        public class Data4
        {
            public string type { get; set; }
            public string id { get; set; }
        }

        public class Banlist
        {
            public Data5 data { get; set; }
        }

        public class Data5
        {
            public string type { get; set; }
            public string id { get; set; }
        }

        #endregion Ban Class

        #region Player Data Class

        #region RequestPayload
        public class AttributesPD
        {
            public string type;
            public string identifier;
        }

        public class DatumPD
        {
            public string type;
            public AttributesPD attributes;
        }

        public class PlayerDataRequest
        {
            public List<DatumPD> data;
        }


        #endregion RequestPayload


        public class PlayerMatchResult
        {
            public List<DataItem> Data;
        }

        public class DataItem
        {
            public Relationships Relationships;
        }

        public class Relationships
        {
            public Player Player;
        }

        public class Player
        {
            public PlayerDataMR Data;
        }

        public class PlayerDataMR
        {
            public string Id;
        }

        #endregion

        #endregion BattleMetrics

        #region Helpers
        private string GetLang(string langKey, string playerId = null, params object[] args) => string.Format(lang.GetMessage(langKey, this, playerId), args);

        private bool HasPerm(string id, string perm) => permission.UserHasPermission(id, perm);

        private int TeamMembersCount(BasePlayer player)
        {
            var team = player.Team;
            if (team == null)
                return 1;
            return team.members.Count;
        }

        private bool HasTeamMember(BasePlayer player, BasePlayer target)
        {
            var team = player.Team;
            if (team == null)
                return false;
            return team.members.Contains(target.userID);
        }

        private bool HasTeamMember(BasePlayer player, ulong target)
        {
            var team = player.Team;
            if (team == null)
                return false;
            return team.members.Contains(target);
        }

        public static bool IsSteamId(ulong id)
        {
            return id > 76561197960265728L;
        }

        public bool IsWhitelisted(ulong id)
        {
            return _whitelistPlayers.Contains(id);
        }

        private static string ProfileLink(ulong steamID) => string.Format(profileLink, steamID);
        #endregion Helpers

    }
} 