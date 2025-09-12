using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;

namespace Oxide.Plugins
{
    [Info("Team Rank Manager", "Mahdi", "1.0.2")]
    [Description("Manages team ranks based on team size")]
    public class TeamRankManager : RustPlugin
    {
        #region Fields
        private Timer checkTeamsTimer;
        private const string PERMISSION_VIP = "vip";
        private const string PERMISSION_VIPPLUS = "vip+";
        private const string PERMISSION_ADMIN = "teamrankmanager.admin";
        private Dictionary<ulong, string> playerRanks = new Dictionary<ulong, string>();
        private DateTime rankExpiryDate;
        #endregion

        #region Configuration
        private Configuration config;

        public class Configuration
        {
            [JsonProperty("Team Size Ranks")]
            public List<TeamSizeRank> TeamSizeRanks = new List<TeamSizeRank>();

            [JsonProperty("Rank Duration (days)")]
            public int RankDuration = 999;

            [JsonProperty("Save Interval (hours)")]
            public int SaveInterval = 3;

            [JsonProperty("Check Interval (seconds)")]
            public float CheckInterval = 300f;

            [JsonProperty("VIP Duration")]
            public string VIPDuration = "25d";
        }

        public class TeamSizeRank
        {
            [JsonProperty("Minimum Team Size")]
            public int MinSize { get; set; }

            [JsonProperty("Maximum Team Size")]
            public int MaxSize { get; set; }

            [JsonProperty("Rank Name")]
            public string Rank { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null || config.TeamSizeRanks == null || config.TeamSizeRanks.Count == 0)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                PrintError("Error reading config file! Created default config.");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                TeamSizeRanks = new List<TeamSizeRank>
                {
                    new TeamSizeRank { MinSize = 3, MaxSize = 4, Rank = "vip" },
                    new TeamSizeRank { MinSize = 5, MaxSize = 6, Rank = "vip+" }
                }
            };
            SaveConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config, true);
        }
        #endregion

        #region Oxide Hooks
        private void Init()
        {
            permission.RegisterPermission(PERMISSION_VIP, this);
            permission.RegisterPermission(PERMISSION_VIPPLUS, this);
            permission.RegisterPermission(PERMISSION_ADMIN, this);
            LoadConfig();
            rankExpiryDate = DateTime.Now.AddDays(config.RankDuration);
        }

        private void OnServerInitialized()
        {
            timer.Once(5f, () =>
            {
                if (TimedPermissions == null)
                {
                    PrintError("TimedPermissions plugin not found! Make sure it's installed.");
                    return;
                }
                StartCheckingTeams();
                CheckAndUpdateAllRanks();
            });
        }

        private void Unload()
        {
            checkTeamsTimer?.Destroy();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            timer.Once(5f, () => CheckAndUpdatePlayerRank(player));
        }

        void OnTeamCreated(BasePlayer player, RelationshipManager.PlayerTeam team)
        {
            if (player == null || team == null) return;
            timer.Once(2f, () => 
            {
                foreach (var member in team.members)
                {
                    var teamMember = BasePlayer.FindByID(member);
                    if (teamMember != null)
                    {
                        CheckAndUpdatePlayerRank(teamMember);
                    }
                }
            });
        }

        void OnTeamAcceptInvite(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (player == null || team == null) return;
            timer.Once(2f, () =>
            {
                foreach (var member in team.members)
                {
                    var teamMember = BasePlayer.FindByID(member);
                    if (teamMember != null)
                    {
                        CheckAndUpdatePlayerRank(teamMember);
                    }
                }
            });
        }

        void OnTeamLeave(RelationshipManager.PlayerTeam team, BasePlayer player)
        {
            if (team == null) return;

            timer.Once(2f, () =>
            {
                if (player != null)
                {
                    CheckAndUpdatePlayerRank(player);
                }

                foreach (var member in team.members)
                {
                    var teamMember = BasePlayer.FindByID(member);
                    if (teamMember != null)
                    {
                        CheckAndUpdatePlayerRank(teamMember);
                    }
                }
            });
        }
        #endregion

        #region Core Methods
        private void StartCheckingTeams()
        {
            checkTeamsTimer?.Destroy();
            checkTeamsTimer = timer.Every(config.CheckInterval, CheckAndUpdateAllRanks);
        }

        private string DetermineRank(int teamSize)
        {
            foreach (var rankConfig in config.TeamSizeRanks)
            {
                if (teamSize >= rankConfig.MinSize && teamSize <= rankConfig.MaxSize)
                {
                    return rankConfig.Rank;
                }
            }
            return null;
        }

        private void CheckAndUpdateAllRanks()
        {
            int checkedPlayers = 0;
            int fixedRanks = 0;

            foreach (var currentPlayer in BasePlayer.activePlayerList)
            {
                try
                {
                    checkedPlayers++;
                    if (UpdatePlayerRank(currentPlayer))
                    {
                        fixedRanks++;
                    }
                }
                catch (Exception ex)
                {
                    PrintError($"Error checking player {currentPlayer.displayName}: {ex.Message}");
                }
            }

            if (fixedRanks > 0)
            {
                Puts($"Auto-check: Checked {checkedPlayers} players, fixed {fixedRanks} ranks");
            }
        }

        private bool UpdatePlayerRank(BasePlayer player)
        {
            if (player == null) return false;

            try
            {
                RelationshipManager.PlayerTeam team = RelationshipManager.ServerInstance.FindPlayersTeam(player.userID);
                string expectedRank = null;
                string currentRank = GetCurrentRank(player);

                if (team != null)
                {
                    expectedRank = DetermineRank(team.members.Count);
                }

                if (expectedRank != null && currentRank != expectedRank)
                {
                    GiveRankWithSharedExpiry(player, expectedRank);
                    return true;
                }
                else if (expectedRank == null && currentRank != null)
                {
                    RemovePlayerRanks(player);
                    return true;
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error updating rank for {player.displayName}: {ex.Message}");
            }

            return false;
        }

        private string GetCurrentRank(BasePlayer player)
        {
            foreach (var rankConfig in config.TeamSizeRanks)
            {
                if (permission.UserHasGroup(player.UserIDString, rankConfig.Rank))
                {
                    return rankConfig.Rank;
                }
            }
            return null;
        }

        private void GiveRankWithSharedExpiry(BasePlayer player, string rank)
        {
            if (player == null || string.IsNullOrEmpty(rank)) return;

            try
            {
                RemovePlayerRanks(player);

                permission.AddUserGroup(player.UserIDString, rank);
                TimedPermissions?.Call("AddGroup", player.UserIDString, rank, config.VIPDuration);
                
                playerRanks[player.userID] = rank;
            }
            catch (Exception ex)
            {
                PrintError($"Error giving rank to {player.displayName}: {ex.Message}");
            }
        }

        private void RemovePlayerRanks(BasePlayer player)
        {
            if (player == null) return;

            try
            {
                foreach (var rankConfig in config.TeamSizeRanks)
                {
                    if (permission.UserHasGroup(player.UserIDString, rankConfig.Rank))
                    {
                        permission.RemoveUserGroup(player.UserIDString, rankConfig.Rank);
                        TimedPermissions?.Call("RemoveGroup", player.UserIDString, rankConfig.Rank);
                    }
                }

                if (playerRanks.ContainsKey(player.userID))
                {
                    playerRanks.Remove(player.userID);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error removing ranks from {player.displayName}: {ex.Message}");
            }
        }

        private void CheckAndUpdatePlayerRank(BasePlayer player)
        {
            if (player == null) return;
            UpdatePlayerRank(player);
        }

        [PluginReference]
        private Plugin TimedPermissions;
        #endregion

        #region Commands
        [ChatCommand("checkrank")]
        private void CheckRankCommand(BasePlayer player, string command, string[] args)
        {
            string currentRank = GetCurrentRank(player);
            string rankDisplay = currentRank?.ToUpper() ?? "None";
            player.ChatMessage($"Your current rank: {rankDisplay}");
        }

        [ConsoleCommand("teamrank.check")]
        private void CheckRanksCommand(ConsoleSystem.Arg args)
        {
            var player = args.Connection?.player as BasePlayer;
            if (player != null && !permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
            {
                SendReply(args, "You don't have permission to use this command.");
                return;
            }

            CheckAndUpdateAllRanks();
            SendReply(args, "All player ranks have been checked and updated.");
        }

        [ConsoleCommand("teamrank.reload")]
        private void ReloadConfigCommand(ConsoleSystem.Arg args)
        {
            var player = args.Connection?.player as BasePlayer;
            if (player != null && !permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
            {
                SendReply(args, "You don't have permission to use this command.");
                return;
            }

            LoadConfig();
            SendReply(args, "Plugin configuration has been reloaded successfully.");
        }

        [ConsoleCommand("teamrank.debug")]
        private void DebugRankCommand(ConsoleSystem.Arg args)
        {
            var player = args.Connection?.player as BasePlayer;
            if (player != null && !permission.UserHasPermission(player.UserIDString, PERMISSION_ADMIN))
            {
                SendReply(args, "You don't have permission to use this command.");
                return;
            }

            var targetPlayer = args.GetPlayer(0);
            if (targetPlayer == null)
            {
                SendReply(args, "Player not found!");
                return;
            }

            var team = RelationshipManager.ServerInstance.FindPlayersTeam(targetPlayer.userID);
            var currentRank = GetCurrentRank(targetPlayer);
            var expectedRank = team != null ? DetermineRank(team.members.Count) : null;

            SendReply(args, $"Debug info for {targetPlayer.displayName}:");
            SendReply(args, $"Team size: {(team?.members.Count ?? 0)}");
            SendReply(args, $"Current rank: {(currentRank?.ToUpper() ?? "None")}");
            SendReply(args, $"Expected rank: {(expectedRank?.ToUpper() ?? "None")}");
            SendReply(args, $"Has VIP: {permission.UserHasGroup(targetPlayer.UserIDString, PERMISSION_VIP)}");
            SendReply(args, $"Has VIP+: {permission.UserHasGroup(targetPlayer.UserIDString, PERMISSION_VIPPLUS)}");
        }
        #endregion
    }
}