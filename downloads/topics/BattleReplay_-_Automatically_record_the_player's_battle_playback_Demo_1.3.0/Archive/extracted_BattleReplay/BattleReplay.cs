using Newtonsoft.Json;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using Oxide.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.IO;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BattleReplay", "Rule · 4u", "1.3.0")]
    [Description("Automatic recording based on conditions, excluding teammates and checking weapon possession with performance optimization and manual trigger options.")]
    internal class BattleReplay : RustPlugin
    {
        #region Configuration
        private static ConfigData config = new ConfigData();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Check Interval (seconds)")]
            public float checkInterval = 5f;

            [JsonProperty(PropertyName = "Recording Distance (meters)")]
            public float recordingDistance = 50f;

            [JsonProperty(PropertyName = "Region Size (meters)")]
            public float regionSize = 100f;

            [JsonProperty(PropertyName = "Recording Cooldown (seconds)")]
            public float recordingCooldown = 60f;

            [JsonProperty(PropertyName = "Auto Recording Length (minutes)")]
            public int autoRecordingLength = 1;

            [JsonProperty(PropertyName = "Skip Admins")]
            public bool skipAdmin = true;

            [JsonProperty(PropertyName = "Total Recorded Logs")]
            public int totalRecordedLogs = 0;

            [JsonProperty(PropertyName = "Total Uploaded Logs")]
            public int totalUploadedLogs = 0;

            [JsonProperty(PropertyName = "Max Replay Recordings Per Player")]
            public int maxReplayRecordings = 3;

            [JsonProperty(PropertyName = "Replay Recording Duration (minutes)")]
            public int replayDuration = 5;

            // Discord Configuration
            [JsonProperty(PropertyName = "Discord Webhook URL")]
            public string discordWebhookUrl = "https://discord.com/api/webhooks/your-webhook-url";

            [JsonProperty(PropertyName = "Discord Notification for Recording Start")]
            public bool discordNotifyRecordingStart = true;

            [JsonProperty(PropertyName = "Discord Notification for Recording Stop")]
            public bool discordNotifyRecordingStop = true;

            [JsonProperty(PropertyName = "Discord Message Color")]
            public int discordMessageColor = 39423;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<ConfigData>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Your config seems to be corrupted. Will load defaults.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig() => Config.WriteObject(config);
        #endregion

        private Dictionary<ulong, PlayerState> playerStates = new Dictionary<ulong, PlayerState>();
        private Dictionary<ulong, Timer> _timers = new Dictionary<ulong, Timer>();

        private void Loaded()
        {
            timer.Every(config.checkInterval, CheckPlayerProximity);
        }

        private void CheckPlayerProximity()
        {
            var players = new List<BasePlayer>();
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null && player.IsConnected && (!config.skipAdmin || !player.IsAdmin))
                {
                    players.Add(player);
                    if (!playerStates.ContainsKey(player.userID))
                    {
                        playerStates[player.userID] = new PlayerState();
                    }
                }
            }

            var playerGroups = DividePlayersByRegion(players);

            foreach (var group in playerGroups)
            {
                CheckPlayerDistancesInGroup(group);
            }
        }

        private List<List<BasePlayer>> DividePlayersByRegion(List<BasePlayer> players)
        {
            Dictionary<Vector3, List<BasePlayer>> regions = new Dictionary<Vector3, List<BasePlayer>>();

            foreach (var player in players)
            {
                Vector3 region = new Vector3(
                    Mathf.Floor(player.transform.position.x / config.regionSize),
                    Mathf.Floor(player.transform.position.y / config.regionSize),
                    Mathf.Floor(player.transform.position.z / config.regionSize)
                );

                if (!regions.ContainsKey(region))
                {
                    regions[region] = new List<BasePlayer>();
                }

                regions[region].Add(player);
            }

            return new List<List<BasePlayer>>(regions.Values);
        }

        private void CheckPlayerDistancesInGroup(List<BasePlayer> group)
        {
            HashSet<ulong> recordedPlayers = new HashSet<ulong>();
            bool anyHoldingWeapon = false;

            foreach (var player in group)
            {
                if (player == null || !player.IsConnected) continue;

                if (IsHoldingWeapon(player))
                {
                    anyHoldingWeapon = true;
                    break;
                }
            }

            if (anyHoldingWeapon)
            {
                foreach (var playerA in group)
                {
                    if (playerA == null || !playerA.IsConnected || recordedPlayers.Contains(playerA.userID) || playerStates[playerA.userID].IsRecording) continue;

                    foreach (var playerB in group)
                    {
                        if (playerB == playerA || playerB == null || !playerB.IsConnected || recordedPlayers.Contains(playerB.userID) || playerStates[playerB.userID].IsRecording) continue;

                        if (!ArePlayersTeammates(playerA, playerB))
                        {
                            float distance = Vector3.Distance(playerA.transform.position, playerB.transform.position);

                            if (distance <= config.recordingDistance && !IsInCooldown(playerA.userID) && !IsInCooldown(playerB.userID))
                            {
                                StartRecording(playerA);
                                StartRecording(playerB);

                                recordedPlayers.Add(playerA.userID);
                                recordedPlayers.Add(playerB.userID);
                            }
                        }
                    }
                }
            }
        }

        private bool IsInCooldown(ulong userId)
        {
            return playerStates.TryGetValue(userId, out PlayerState state) &&
                   UnityEngine.Time.realtimeSinceStartup - state.LastRecordingTime < config.recordingCooldown;  // 使用 UnityEngine.Time
        }

        private bool IsHoldingWeapon(BasePlayer player)
        {
            var activeItem = player?.GetActiveItem();
            return activeItem?.info?.category == ItemCategory.Weapon;
        }

        private void StartRecording(BasePlayer player)
        {
            if (player.Connection.IsRecording)
            {
                Puts($"Player {player.displayName} is already recording.");
                return;
            }

            try
            {
                player.StartDemoRecording();
                Puts($"Started demo recording for player {player.displayName}.");

                playerStates[player.userID].IsRecording = true;
                playerStates[player.userID].LastRecordingTime = UnityEngine.Time.realtimeSinceStartup;  // 使用 UnityEngine.Time

                config.totalRecordedLogs++;
                SaveConfig();

                if (config.autoRecordingLength > 0)
                {
                    if (_timers.ContainsKey(player.userID))
                    {
                        _timers[player.userID].Destroy();
                        _timers.Remove(player.userID);
                    }

                    _timers[player.userID] = timer.Once(config.autoRecordingLength * 60, () => StopRecording(player));
                }

                // Notify Discord
                if (config.discordNotifyRecordingStart)
                {
                    NotifyDiscord(player, "Recording started", true);
                }
            }
            catch (Exception ex)
            {
                Puts($"Failed to start recording for {player.displayName}: {ex.Message}");
            }
        }

        private void StopRecording(BasePlayer player)
        {
            if (!playerStates.TryGetValue(player.userID, out var state) || !state.IsRecording) return;

            player.StopDemoRecording();
            _timers.Remove(player.userID);
            state.IsRecording = false;

            player.ChatMessage("Recording complete. Please contact an admin for the demo file.");
            Puts($"Stopped recording for {player.displayName}.");

            config.totalUploadedLogs++;
            SaveConfig();

            // Notify Discord
            if (config.discordNotifyRecordingStop)
            {
                NotifyDiscord(player, "Recording stopped", false);
            }
        }

        private void NotifyDiscord(BasePlayer player, string msg, bool isStart)
        {
            if (string.IsNullOrEmpty(config.discordWebhookUrl)) return;

            var embed = new
            {
                embeds = new[]
                {
                    new
                    {
                        title = isStart ? "Recording Started" : "Recording Stopped",
                        description = msg,
                        color = config.discordMessageColor,
                        fields = new[]
                        {
                            new { name = "Player", value = player.displayName, inline = false },
                            new { name = "Steam ID", value = player.UserIDString, inline = false },
                        }
                    }
                }
            };

            string json = JsonConvert.SerializeObject(embed);

            // Send POST request using HttpWebRequest
            try
            {
                var request = (HttpWebRequest)WebRequest.Create(config.discordWebhookUrl);
                request.Method = "POST";
                request.ContentType = "application/json";
                using (var streamWriter = new StreamWriter(request.GetRequestStream()))
                {
                    streamWriter.Write(json);
                }

                var response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode == HttpStatusCode.NoContent)
                {
                    Puts("Successfully sent Discord message.");
                }
                else
                {
                    Puts("Failed to send Discord message.");
                }
            }
            catch (Exception ex)
            {
                Puts($"Error sending Discord message: {ex.Message}");
            }
        }

        private bool ArePlayersTeammates(BasePlayer playerA, BasePlayer playerB)
        {
            return playerA != null && playerB != null && playerA.currentTeam == playerB.currentTeam && playerA.currentTeam != 0;
        }

        private class PlayerState
        {
            public bool IsRecording;
            public float LastRecordingTime;
            public int ReplayCount;
        }

        #region Commands
        [ChatCommand("battlelog")]
        void BattleLog(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                player.ChatMessage("Showing battle log for player: " + player.displayName);
                player.ChatMessage($"Total Recorded Logs: {config.totalRecordedLogs}");
                player.ChatMessage($"Total Uploaded Logs: {config.totalUploadedLogs}");
            }
            else if (args.Length == 1 && args[0].ToLower() == "stats")
            {
                player.ChatMessage("BattleReplay Stats:");
                player.ChatMessage($"Total Recorded Logs: {config.totalRecordedLogs}");
                player.ChatMessage($"Total Uploaded Logs: {config.totalUploadedLogs}");
            }
            else
            {
                player.ChatMessage("Invalid usage. Use '/battlelog' to view your battle log or '/battlelog stats' to view the plugin statistics.");
            }
        }

        [ChatCommand("replay")]
        void Replay(BasePlayer player, string command, string[] args)
        {
            if (playerStates[player.userID].ReplayCount >= config.maxReplayRecordings)
            {
                player.ChatMessage("You have reached the maximum number of replay recordings.");
                return;
            }

            if (args.Length != 1)
            {
                player.ChatMessage("Usage: /replay <playername|playerid>");
                return;
            }

            string input = args[0];
            BasePlayer targetPlayer = null;

            if (ulong.TryParse(input, out ulong playerId))
            {
                targetPlayer = BasePlayer.FindByID(playerId);
            }
            else
            {
                targetPlayer = BasePlayer.Find(input);
            }

            if (targetPlayer != null)
            {
                Puts($"Starting replay recording for player {targetPlayer.displayName} (ID: {targetPlayer.userID})");

                StartRecording(targetPlayer);

                playerStates[player.userID].ReplayCount++;

                player.ChatMessage($"Started recording for {targetPlayer.displayName}, regardless of distance.");

                _timers[targetPlayer.userID] = timer.Once(config.replayDuration * 60, () => StopRecording(targetPlayer));
            }
            else
            {
                player.ChatMessage("Player not found. Please provide a valid player name or ID.");
                Puts($"Failed to find player: {input}");
            }
        }
        #endregion

        #region Permissions
        private void Init()
        {
            permission.RegisterPermission("battlereplay.admin", this);
        }
        #endregion
    }
}
