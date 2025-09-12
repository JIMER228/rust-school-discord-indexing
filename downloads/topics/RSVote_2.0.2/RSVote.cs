using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using Oxide.Core.Libraries.Covalence;
using System.Linq;
using Oxide.Core;

namespace Oxide.Plugins
{
    [Info("RustServer.gg Vote", "TimQQ@Codefling", "2.0.2")]
    [Description("Voting reward plugin for RustServers.gg with language support and Discord integration")]
    public class RSVote: RustPlugin
    {
        #region Vars

        private Dictionary<ulong, Dictionary<string, DateTime>> cooldown = new Dictionary<ulong, Dictionary<string, DateTime>>();
        private Dictionary<ulong, Dictionary<string, int>> voteStreaks = new Dictionary<ulong, Dictionary<string, int>>();
        private Dictionary<ulong, Dictionary<string, DateTime>> lastVoteTime = new Dictionary<ulong, Dictionary<string, DateTime>>();
        private Dictionary<ulong, Dictionary<string, int>> totalVotes = new Dictionary<ulong, Dictionary<string, int>>();
        private Dictionary<ulong, Dictionary<string, DateTime>> firstVote = new Dictionary<ulong, Dictionary<string, DateTime>>();
        private const string DiscordWebhookUrl = "https://discord.com/api/webhooks/your-webhook-url";

        #endregion

        #region Language

        private Dictionary<string, string> lang = new Dictionary<string, string>();

        protected override void LoadDefaultMessages()
        {
            lang = new Dictionary<string, string>
            {
                ["CooldownMessage"] = "Please wait {0} seconds before trying again!",
                ["ClaimingReward"] = "Trying to claim reward...",
                ["VotePrompt"] = "Vote at the servers page to claim reward!\n{0}",
                ["AlreadyClaimed"] = "You already claimed your reward(s)",
                ["RewardReceived"] = "You received {0} for voting!",
                ["RewardFailed"] = "Failed to claim reward, please contact the server owner!",
                ["DiscordVoteMessage"] = "**{0}** has voted and received: {1}",
                ["GlobalVoteMessage"] = "<color=#00ff00>{0}</color> has voted and received: {1}!",
                ["DiscordGlobalVoteMessage"] = "🎮 **{0}** has voted and received: {1}!",
                ["StreakMessage"] = "Vote streak: {0} days!",
                ["StreakBonus"] = "You received a streak bonus reward!",
                ["StreakReset"] = "Your vote streak has been reset!",
                ["DiscordStreakMessage"] = "🔥 **{0}** is on a {1} day voting streak!",
                ["GlobalStreakMessage"] = "<color=#ffd700>{0}</color> is on a {1} day voting streak! 🔥",
                ["StatsMessage"] = "Your voting statistics:\nTotal votes: {0}\nCurrent streak: {1}\nFirst vote: {2}",
                ["LeaderboardTitle"] = "🏆 Top Voters 🏆",
                ["LeaderboardEntry"] = "{0}. {1}: {2} votes",
                ["NoVoteHistory"] = "No voting history found.",
                ["StatsCommandUsage"] = "Usage: /votestats [player]",
                ["PlayerNotFound"] = "Player not found.",
                ["NoPermission"] = "You don't have permission to use this command.",
                ["DiscordStatsMessage"] = "📊 **Voting Statistics**\nPlayer: {0}\nTotal votes: {1}\nCurrent streak: {2}"
            };
        }

        private void LoadMessages()
        {
            // Load default messages first
            LoadDefaultMessages();

            // Try to load custom messages from file
            var customMessages = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, string>>($"{Name}/lang");
            if (customMessages != null)
            {
                // Update or add custom messages
                foreach (var message in customMessages)
                {
                    if (lang.ContainsKey(message.Key))
                    {
                        lang[message.Key] = message.Value;
                    }
                    else
                    {
                        lang.Add(message.Key, message.Value);
                    }
                }
            }
        }

        private void SaveMessages()
        {
            Interface.Oxide.DataFileSystem.WriteObject($"{Name}/lang", lang);
        }

        private string GetMsg(string key, string userId = null, params object[] args)
        {
            return string.Format(lang.ContainsKey(key) ? lang[key] : key, args);
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            LoadMessages();
            foreach (var command in config.commands)
            {
                cmd.AddChatCommand(command, this, cmdChat);
            }
            
            cmd.AddChatCommand(config.statsCommand, this, cmdStats);
            cmd.AddChatCommand(config.leaderboardCommand, this, cmdLeaderboard);
            
            timer.Every(Core.Random.Range(300, 500), () => { cooldown.Clear(); });
            LoadData();
            
            // Log loaded servers
            foreach (var server in config.servers)
            {
                Puts($"Loaded server '{server.name}' with id '{server.serverId}'");
            }
        }

        private void OnServerSave()
        {
            SaveConfig();
            SaveMessages();
            SaveData();
        }

        #endregion

        #region Commands

        private void cmdChat(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                // Show available servers
                var serverList = string.Join("\n", config.servers.Select(s => 
                    $"- {s.name} (ID: {s.serverId})"));
                player.ChatMessage($"Available servers:\n{serverList}\n\nUsage: /{command} <server-id>");
                return;
            }

            var serverId = args[0];
            var serverConfig = GetServerConfig(serverId);
            
            if (!cooldown.ContainsKey(player.userID))
            {
                cooldown[player.userID] = new Dictionary<string, DateTime>();
            }

            if (cooldown[player.userID].ContainsKey(serverId))
            {
                var nextUse = cooldown[player.userID][serverId];
                if (nextUse > DateTime.UtcNow)
                {
                    var remainingSeconds = (nextUse - DateTime.UtcNow).TotalSeconds;
                    player.ChatMessage(GetMsg("CooldownMessage", player.UserIDString, remainingSeconds));
                    return;
                }
                cooldown[player.userID][serverId] = DateTime.UtcNow.AddSeconds(config.cooldownSeconds);
            }
            else
            {
                cooldown[player.userID][serverId] = DateTime.UtcNow.AddSeconds(config.cooldownSeconds);
            }

            player.ChatMessage(GetMsg("ClaimingReward", player.UserIDString));
            var url = BuildUrl("status", player.UserIDString, serverId);
            Execute(url, "status", player, serverId);
        }

        private void cmdStats(BasePlayer player, string command, string[] args)
        {
            if (args.Length > 0 && !permission.UserHasPermission(player.UserIDString, "rsvote.admin"))
            {
                player.ChatMessage(GetMsg("NoPermission", player.UserIDString));
                return;
            }

            BasePlayer target = player;
            if (args.Length > 0)
            {
                target = BasePlayer.Find(args[0]);
                if (target == null)
                {
                    player.ChatMessage(GetMsg("PlayerNotFound", player.UserIDString));
                    return;
                }
            }

            if (!totalVotes.ContainsKey(target.userID))
            {
                player.ChatMessage(GetMsg("NoVoteHistory", player.UserIDString));
                return;
            }

            var streak = voteStreaks.ContainsKey(target.userID) ? voteStreaks[target.userID] : new Dictionary<string, int>();
            var firstVoteDate = firstVote.ContainsKey(target.userID) ? firstVote[target.userID] : new Dictionary<string, DateTime>();
            
            player.ChatMessage(GetMsg("StatsMessage", player.UserIDString, 
                totalVotes[target.userID].Count, streak.Count, firstVoteDate.Count));

            // Send Discord notification for each server where the player has voted
            foreach (var server in config.servers)
            {
                if (server.enableDiscordNotifications && totalVotes[target.userID].ContainsKey(server.serverId))
                {
                    SendDiscordNotification(target.displayName, 
                        $"Stats for {server.name} - Total votes: {totalVotes[target.userID][server.serverId]}, " +
                        $"Streak: {GetVoteStreak(target.userID, server.serverId)}",
                        server.serverId);
                }
            }
        }

        private void cmdLeaderboard(BasePlayer player, string command, string[] args)
        {
            var leaderboard = GetLeaderboard();
            if (leaderboard.Count == 0)
            {
                player.ChatMessage(GetMsg("NoVoteHistory", player.UserIDString));
                return;
            }

            player.ChatMessage(GetMsg("LeaderboardTitle", player.UserIDString));
            for (int i = 0; i < Math.Min(leaderboard.Count, config.leaderboardSize); i++)
            {
                var entry = leaderboard[i];
                player.ChatMessage(GetMsg("LeaderboardEntry", player.UserIDString, 
                    i + 1, entry.Key, entry.Value));
            }
        }

        #endregion

        #region Core

        private string BuildUrl(string action, string userId, string serverId)
        {
            var url = ConfigData.baseUrl;
            url += $"action={action}";
            url += $"&key={GetServerConfig(serverId).apiKey}";
            url += $"&server={serverId}";
            url += $"&steamid={userId}";
            return url;
        }

        private ServerConfig GetServerConfig(string serverId)
        {
            return config.servers.FirstOrDefault(s => s.serverId == serverId) ?? config.servers[0];
        }

        private void Execute(string url, string action, BasePlayer player, string serverId)
        {
            webrequest.Enqueue(url, null, (i, s) => { HandleResponse(url, i, s, action, player, serverId); }, this);
        }

        private void HandleResponse(string url, int code, string response, string action, BasePlayer player, string serverId)
        {
            var rInt = 0;
            if (int.TryParse(response, out rInt) == false)
            {
                PrintWarning($"Failed to parse response from '{response}'");
                return;
            }

            if (action == "status")
            {
                switch (rInt)
                {
                    case 0:
                        player.ChatMessage(GetMsg("VotePrompt", player.UserIDString, 
                            $"https://rustservers.gg/server/{serverId}"));
                        break;

                    case 1:
                        GiveRandomReward(player, serverId);
                        break;

                    case 2:
                        player.ChatMessage(GetMsg("AlreadyClaimed", player.UserIDString));
                        break;
                }
            }

            if (action == "claim")
            {
                switch (rInt)
                {
                    case 0:
                    case 1:
                    case 2:
                        // Ignore
                        break;
                }
            }
        }

        private void GiveRandomReward(BasePlayer player, string serverId)
        {
            var userId = player.UserIDString;
            var serverConfig = GetServerConfig(serverId);
            var streak = GetVoteStreak(player.userID, serverId);
            var totalVoteCount = GetTotalVotes(player.userID, serverId);

            // Find eligible reward categories
            var eligibleCategories = serverConfig.rewardCategories
                .Where(cat => 
                    (cat.requiredStreak <= 0 || streak >= cat.requiredStreak) &&
                    (cat.requiredVotes <= 0 || totalVoteCount >= cat.requiredVotes))
                .ToList();

            if (eligibleCategories.Count == 0)
            {
                // Fallback to default category
                eligibleCategories = serverConfig.rewardCategories
                    .Where(cat => string.IsNullOrEmpty(cat.name) || cat.name == "Default")
                    .ToList();
            }

            if (eligibleCategories.Count == 0)
            {
                PrintWarning($"No eligible reward categories found for player '{userId}' on server '{serverId}'");
                player.ChatMessage(GetMsg("RewardFailed", userId));
                return;
            }

            // Select a random category from eligible ones
            var selectedCategory = eligibleCategories.GetRandom();
            var reward = selectedCategory.rewards.GetRandom();

            var result = reward.GiveTo(userId);
            if (result == false)
            {
                PrintWarning($"Failed to give reward to '{userId}' ({reward.Info()})");
                player.ChatMessage(GetMsg("RewardFailed", userId));
                return;
            }

            // Handle vote streak
            UpdateVoteStreak(player, serverId);

            // Update vote statistics
            UpdateVoteStats(player, serverId);

            Puts($"Player {userId} received reward for voting on server {serverId} (Category: {selectedCategory.name})");
            player.ChatMessage(GetMsg("RewardReceived", userId, reward.Info()));
            
            // Send Discord notification if enabled
            if (serverConfig.enableDiscordNotifications)
            {
                SendDiscordNotification(player.displayName, 
                    $"{reward.Info()} (Category: {selectedCategory.name})",
                    serverId);
            }

            // Send global message if enabled
            if (serverConfig.enableGlobalMessages)
            {
                SendGlobalMessage(player.displayName, 
                    $"{reward.Info()} (Category: {selectedCategory.name})",
                    serverId);
            }

            var claimUrl = BuildUrl("claim", userId, serverId);
            Execute(claimUrl, "claim", player, serverId);
        }

        private int GetVoteStreak(ulong userId, string serverId)
        {
            if (!voteStreaks.ContainsKey(userId))
                voteStreaks[userId] = new Dictionary<string, int>();
            return voteStreaks[userId].ContainsKey(serverId) ? voteStreaks[userId][serverId] : 0;
        }

        private int GetTotalVotes(ulong userId, string serverId)
        {
            if (!totalVotes.ContainsKey(userId))
                totalVotes[userId] = new Dictionary<string, int>();
            return totalVotes[userId].ContainsKey(serverId) ? totalVotes[userId][serverId] : 0;
        }

        private void UpdateVoteStreak(BasePlayer player, string serverId)
        {
            var now = DateTime.UtcNow;
            var userId = player.userID;
            var serverConfig = GetServerConfig(serverId);

            if (!lastVoteTime.ContainsKey(userId))
                lastVoteTime[userId] = new Dictionary<string, DateTime>();
            if (!voteStreaks.ContainsKey(userId))
                voteStreaks[userId] = new Dictionary<string, int>();

            if (!lastVoteTime[userId].ContainsKey(serverId))
            {
                // First vote
                voteStreaks[userId][serverId] = 1;
                lastVoteTime[userId][serverId] = now;
                player.ChatMessage(GetMsg("StreakMessage", player.UserIDString, 1));
                return;
            }

            var lastVote = lastVoteTime[userId][serverId];
            var timeSinceLastVote = now - lastVote;

            if (timeSinceLastVote.TotalHours <= 24)
            {
                // Within 24 hours, increment streak
                voteStreaks[userId][serverId]++;
                player.ChatMessage(GetMsg("StreakMessage", player.UserIDString, voteStreaks[userId][serverId]));
                
                // Give streak bonus if enabled
                if (serverConfig.enableStreakBonus && voteStreaks[userId][serverId] % serverConfig.streakBonusInterval == 0)
                {
                    GiveStreakBonus(player, serverId);
                }
            }
            else if (timeSinceLastVote.TotalHours <= 48)
            {
                // Within 48 hours, maintain streak
                player.ChatMessage(GetMsg("StreakMessage", player.UserIDString, voteStreaks[userId][serverId]));
            }
            else
            {
                // Streak broken
                player.ChatMessage(GetMsg("StreakReset", player.UserIDString));
                voteStreaks[userId][serverId] = 1;
            }

            lastVoteTime[userId][serverId] = now;

            // Send streak messages if enabled
            if (serverConfig.enableGlobalMessages)
            {
                Server.Broadcast(GetMsg("GlobalStreakMessage", null, player.displayName, voteStreaks[userId][serverId]));
            }

            if (serverConfig.enableDiscordNotifications)
            {
                SendDiscordNotification(player.displayName, $"Streak: {voteStreaks[userId][serverId]} days", serverId);
            }
        }

        private void GiveStreakBonus(BasePlayer player, string serverId)
        {
            var serverConfig = GetServerConfig(serverId);
            var bonusReward = serverConfig.streakBonusReward;
            if (bonusReward != null)
            {
                bonusReward.GiveTo(player.UserIDString);
                player.ChatMessage(GetMsg("StreakBonus", player.UserIDString));
            }
        }

        private void SendGlobalMessage(string playerName, string reward, string serverId = null)
        {
            var serverConfig = !string.IsNullOrEmpty(serverId)
                ? GetServerConfig(serverId)
                : config.servers.FirstOrDefault();

            if (serverConfig == null || !serverConfig.enableGlobalMessages) return;

            var message = GetMsg("GlobalVoteMessage", null, playerName, reward);
            Server.Broadcast(message);
        }

        private void SendDiscordNotification(string playerName, string reward, string serverId = null)
        {
            // If serverId is provided, use that server's config, otherwise use the first server's config
            var serverConfig = !string.IsNullOrEmpty(serverId) 
                ? GetServerConfig(serverId) 
                : config.servers.FirstOrDefault();

            if (serverConfig == null || !serverConfig.enableDiscordNotifications) return;

            var message = serverConfig.enableGlobalMessages 
                ? GetMsg("DiscordGlobalVoteMessage", null, playerName, reward)
                : GetMsg("DiscordVoteMessage", null, playerName, reward);

            // Use Discord Messages plugin instead of webhook
            plugins.Find("DiscordMessages")?.Call("SendMessage", message);
        }

        private void UpdateVoteStats(BasePlayer player, string serverId)
        {
            var userId = player.userID;
            
            if (!totalVotes.ContainsKey(userId))
                totalVotes[userId] = new Dictionary<string, int>();
            if (!firstVote.ContainsKey(userId))
                firstVote[userId] = new Dictionary<string, DateTime>();
            
            if (!totalVotes[userId].ContainsKey(serverId))
            {
                totalVotes[userId][serverId] = 0;
                firstVote[userId][serverId] = DateTime.UtcNow;
            }
            
            totalVotes[userId][serverId]++;
        }

        private List<KeyValuePair<string, int>> GetLeaderboard()
        {
            var leaderboard = new List<KeyValuePair<string, int>>();
            foreach (var kvp in totalVotes)
            {
                var player = BasePlayer.FindByID(kvp.Key);
                if (player != null)
                {
                    leaderboard.Add(new KeyValuePair<string, int>(player.displayName, kvp.Value.Count));
                }
            }
            return leaderboard.OrderByDescending(x => x.Value).ToList();
        }

        #endregion

        #region Config

        private class ServerConfig
        {
            [JsonProperty("Server Name")] 
            public string name = "Default Server";

            [JsonProperty("Server Id")] 
            public string serverId = string.Empty;

            [JsonProperty("API Key")] 
            public string apiKey = string.Empty;

            [JsonProperty("Enable Discord Notifications")] 
            public bool enableDiscordNotifications = false;

            [JsonProperty("Enable Global Messages")] 
            public bool enableGlobalMessages = true;

            [JsonProperty("Global Message Color")] 
            public string globalMessageColor = "#00ff00";

            [JsonProperty("Vote Reward Categories")] 
            public List<VoteRewardCategory> rewardCategories = new List<VoteRewardCategory>();

            [JsonProperty("Enable Streak Bonus")] 
            public bool enableStreakBonus = true;

            [JsonProperty("Streak Bonus Interval (days)")] 
            public int streakBonusInterval = 7;

            [JsonProperty("Streak Bonus Reward")] 
            public RewardItem streakBonusReward = new RewardItem
            {
                shortname = "scrap",
                amountMin = 100,
                amountMax = 200,
                description = "Scrap",
                skinId = 0
            };
        }

        private class ConfigData
        {
            [JsonProperty("Servers")] 
            public List<ServerConfig> servers = new List<ServerConfig>
            {
                new ServerConfig()
            };

            [JsonProperty("Cooldown Seconds")] 
            public int cooldownSeconds = 5;

            [JsonProperty("Chat Commands")] 
            public string[] commands =
            {
                "claim",
                "vote",
                "votes",
            };

            [JsonProperty("Stats Command")] 
            public string statsCommand = "votestats";

            [JsonProperty("Leaderboard Command")] 
            public string leaderboardCommand = "voteleaderboard";

            [JsonProperty("Leaderboard Size")] 
            public int leaderboardSize = 10;

            [JsonIgnore] 
            public const string baseUrl = "https://rustservers.gg/vote-api.php?";
        }

        private static ConfigData config = new ConfigData();

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
                for (var i = 0; i < 3; i++)
                {
                    PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                }

                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = new ConfigData();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        #endregion

        #region Rewards

        private interface IReward
        {
            bool GiveTo(string userId);

            string Info();
        }

        private class RewardItem : IReward
        {
            [JsonProperty("Item Shortname")] public string shortname;
            [JsonProperty("Description")] public string description;

            [JsonProperty("Item Amount Min")] public int amountMin;

            [JsonProperty("Item Amount Max")] public int amountMax;

            [JsonProperty("Item Skin id")] public ulong skinId;

            public Item GetItem()
            {
                var def = ItemManager.FindItemDefinition(shortname);
                if (def == null)
                {
                    return null;
                }

                var amount = amountMax > amountMin
                    ? Core.Random.Range(amountMin, amountMax)
                    : Core.Random.Range(amountMax, amountMin);

                var item = ItemManager.Create(def, amount);
                if (item == null)
                {
                    return null;
                }

                item.skin = skinId;
                return item;
            }

            public bool GiveTo(string userId)
            {
                var player = BasePlayer.Find(userId);
                if (player == null)
                {
                    return false;
                }

                var item = GetItem();
                if (item == null)
                {
                    return false;
                }

                player.GiveItem(item);
                return true;
            }

            public string Info()
            {
                return $"Item: {description}";
            }
        }

        private class VoteRewardCategory
        {
            [JsonProperty("Name")] 
            public string name;

            [JsonProperty("Description")] 
            public string description;

            [JsonProperty("Required Streak")] 
            public int requiredStreak;

            [JsonProperty("Required Total Votes")] 
            public int requiredVotes;

            [JsonProperty("Rewards")] 
            public List<RewardItem> rewards = new List<RewardItem>();
        }

        #endregion

        #region Data Management

        private void SaveData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject($"{Name}/vote_stats", new
                {
                    totalVotes,
                    firstVote,
                    voteStreaks,
                    lastVoteTime
                });
            }
            catch (Exception ex)
            {
                PrintError($"Failed to save data: {ex.Message}");
            }
        }

        private void LoadData()
        {
            try
            {
                var data = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, object>>($"{Name}/vote_stats");
                if (data != null)
                {
                    // Initialize dictionaries with default values
                    totalVotes = new Dictionary<ulong, Dictionary<string, int>>();
                    firstVote = new Dictionary<ulong, Dictionary<string, DateTime>>();
                    voteStreaks = new Dictionary<ulong, Dictionary<string, int>>();
                    lastVoteTime = new Dictionary<ulong, Dictionary<string, DateTime>>();

                    // Try to load data if it exists
                    if (data.ContainsKey("totalVotes"))
                        totalVotes = data["totalVotes"] as Dictionary<ulong, Dictionary<string, int>> ?? totalVotes;
                    if (data.ContainsKey("firstVote"))
                        firstVote = data["firstVote"] as Dictionary<ulong, Dictionary<string, DateTime>> ?? firstVote;
                    if (data.ContainsKey("voteStreaks"))
                        voteStreaks = data["voteStreaks"] as Dictionary<ulong, Dictionary<string, int>> ?? voteStreaks;
                    if (data.ContainsKey("lastVoteTime"))
                        lastVoteTime = data["lastVoteTime"] as Dictionary<ulong, Dictionary<string, DateTime>> ?? lastVoteTime;
                }
            }
            catch (Exception ex)
            {
                PrintError($"Failed to load data: {ex.Message}");
                // Initialize empty dictionaries on error
                totalVotes = new Dictionary<ulong, Dictionary<string, int>>();
                firstVote = new Dictionary<ulong, Dictionary<string, DateTime>>();
                voteStreaks = new Dictionary<ulong, Dictionary<string, int>>();
                lastVoteTime = new Dictionary<ulong, Dictionary<string, DateTime>>();
            }
        }

        #endregion
    }
}   