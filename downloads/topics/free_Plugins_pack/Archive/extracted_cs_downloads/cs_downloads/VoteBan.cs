using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Libraries.Covalence;
using Newtonsoft.Json;
using UnityEngine;
using System.Threading.Tasks;

//  ____  _       _ _       
// / ___|(_) __ _(_) | ___  
// \___ \| |/ _` | | |/ _ \ 
//  ___) | | (_| | | | (_) |
// |____/|_|\__, |_|_|\___/ 
//          |___/             
// ☽✧✵✧✵✧✵✧✵✧✵✧✵✧☾
// ✦    RUST PLUGINS   ✦
// ✦     Sigilo.dev    ✦
// ☽✧✵✧✵✧✵✧✵✧✵✧✵✧☾

namespace Oxide.Plugins
{
    [Info("VoteBan", "Sigilo", "1.1.1")]
    [Description("Player voting system for banning players")]
    class VoteBan : RustPlugin
    {
        #region Configuration

        private Configuration config;

        class Configuration
        {
            [JsonProperty("Required percentage (0-100)")]
            public float RequiredPercentage = 90f;
            
            [JsonProperty("Vote duration (seconds)")]
            public int VoteDuration = 120;
            
            [JsonProperty("Cooldown between votes (minutes)")]
            public int CooldownMinutes = 10;
            
            [JsonProperty("Ban duration (minutes, 0 for permanent)")]
            public int BanDuration = 60;
            
            [JsonProperty("Discord Webhook URL")]
            public string WebhookUrl = "";
            
            [JsonProperty("Log to console")]
            public bool LogToConsole = false;

            [JsonProperty("Minimum players required for voting")]
            public int MinimumPlayers = 9;

            [JsonProperty("Allow voting to ban self")]
            public bool AllowSelfVote = false;

            [JsonProperty("Allow voting to ban admins")]
            public bool AllowAdminVote = false;

            [JsonProperty("Reminder interval (seconds)")]
            public int ReminderInterval = 30;
            
            [JsonProperty("Count only voters (if false, non-voters count as No)")]
            public bool CountOnlyVoters = true;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();
            }
            catch
            {
                PrintError("Configuration file is corrupt, using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig() => config = new Configuration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Initialization

        private const string permStartVote = "voteban.start";    
        private const string permVote = "voteban.vote";          
        private Dictionary<ulong, DateTime> lastVoteCall = new Dictionary<ulong, DateTime>();
        private VoteSession activeVote;
        private Dictionary<ulong, DateTime> tempBans = new Dictionary<ulong, DateTime>();

        void Init()
        {
            permission.RegisterPermission(permStartVote, this);
            permission.RegisterPermission(permVote, this);
            LoadDefaultMessages();
            LoadBanData();
            timer.Every(60f, CheckTempBans);
        }

        void Unload()
        {
            SaveBanData();
        }
        
        protected override void LoadDefaultMessages()
        {
            var messages = new Dictionary<string, string>
            {
                ["VoteStarted"] = "A vote has started to ban <color=#B87333>{0}</color> for <color=#9CAF88>{1}</color>!",
                ["VoteDuration"] = "Vote will last <color=#9CAF88>{0}</color> seconds",
                ["VoteInstructions"] = "Type <color=#E6BE8A>/y</color> to ban or <color=#E6BE8A>/n</color> to cancel",
                ["VoteSuccess"] = "Vote passed (<color=#9CAF88>{0}/{1}</color> votes): <color=#B87333>{2}</color> has been banned for <color=#9CAF88>{3}</color>",
                ["VoteFailed"] = "Vote failed (<color=#9CAF88>{0}/{1}</color> votes): <color=#B87333>{2}</color> will not be banned",
                ["Cooldown"] = "Wait <color=#9CAF88>{0}</color> minutes before starting another vote",
                ["NoPermission"] = "You don't have permission to start a vote",
                ["InvalidTarget"] = "Player '<color=#B87333>{0}</color>' not found",
                ["VoteInProgress"] = "A vote is already in progress",
                ["InvalidCommandBan"] = "Usage: <color=#E6BE8A>/voteban</color> <color=#B87333><player></color>",
                ["MinimumPlayers"] = "Need at least <color=#9CAF88>{0}</color> players online to start a vote",
                ["CannotVoteAdmin"] = "Cannot vote to ban <color=#B87333>admin</color>",
                ["CannotVoteSelf"] = "Cannot vote to ban <color=#B87333>yourself</color>",
                ["AlreadyBanned"] = "<color=#B87333>{0}</color> is already banned",
                ["BanSuccessOnline"] = "<color=#708090>You have been banned via vote</color>",
                ["NoVotePermission"] = "You don't have permission to vote",
                ["VoteReminder"] = "Vote to ban <color=#B87333>{0}</color> - {1}s left",
                ["AlreadyVoted"] = "You have already voted"
            };

            lang.RegisterMessages(messages, this, "en");
            lang.RegisterMessages(messages, this);
        }

        #endregion

        #region Voting System

        class VoteSession
        {
            public string TargetName;
            public ulong TargetId;
            public Dictionary<ulong, bool> Votes = new Dictionary<ulong, bool>();
            public Timer VoteTimer;
            public Timer ReminderTimer;
            public int RemainingTime;
        }

        [ChatCommand("voteban")]
        void CmdVoteBan(BasePlayer player, string command, string[] args)
        {
            StartVote(player, args);
        }

        void StartVote(BasePlayer initiator, string[] args)
        {
            if (!permission.UserHasPermission(initiator.UserIDString, permStartVote))
            {
                SendReply(initiator, Lang("NoPermission", initiator.UserIDString));
                return;
            }

            if (BasePlayer.activePlayerList.Count < config.MinimumPlayers)
            {
                SendReply(initiator, Lang("MinimumPlayers", initiator.UserIDString, config.MinimumPlayers));
                return;
            }

            if (activeVote != null)
            {
                SendReply(initiator, Lang("VoteInProgress", initiator.UserIDString));
                return;
            }

            if (args.Length == 0)
            {
                SendReply(initiator, Lang("InvalidCommandBan", initiator.UserIDString));
                return;
            }

            if (lastVoteCall.TryGetValue(initiator.userID, out DateTime lastCall) && 
                (DateTime.Now - lastCall).TotalMinutes < config.CooldownMinutes)
            {
                var remainingTime = config.CooldownMinutes - (DateTime.Now - lastCall).TotalMinutes;
                SendReply(initiator, Lang("Cooldown", initiator.UserIDString, Math.Round(remainingTime, 2)));
                return;
            }

            var target = FindPlayer(args[0]);
            if (target == null)
            {
                SendReply(initiator, Lang("InvalidTarget", initiator.UserIDString, args[0]));
                return;
            }

            if (tempBans.ContainsKey(target.userID))
            {
                SendReply(initiator, Lang("AlreadyBanned", initiator.UserIDString, target.displayName));
                return;
            }

            if (!config.AllowSelfVote && target.userID == initiator.userID)
            {
                SendReply(initiator, Lang("CannotVoteSelf", initiator.UserIDString));
                return;
            }

            if (!config.AllowAdminVote && target.IsAdmin)
            {
                SendReply(initiator, Lang("CannotVoteAdmin", initiator.UserIDString));
                return;
            }

            activeVote = new VoteSession
            {
                TargetName = target.displayName,
                TargetId = target.userID,
                RemainingTime = config.VoteDuration,
                VoteTimer = timer.Once(config.VoteDuration, EndVote)
            };

            lastVoteCall[initiator.userID] = DateTime.Now;

            string banDurationText = config.BanDuration > 0 ? $"{config.BanDuration} minutes" : "permanently";
            BroadcastChat(Lang("VoteStarted", null, target.displayName, banDurationText));
            BroadcastChat(Lang("VoteDuration", null, config.VoteDuration));
            BroadcastChat(Lang("VoteInstructions"));

            if (config.ReminderInterval > 0 && config.ReminderInterval < config.VoteDuration)
            {
                activeVote.RemainingTime = config.VoteDuration;
                activeVote.ReminderTimer = timer.Every(config.ReminderInterval, () =>
                {
                    activeVote.RemainingTime -= config.ReminderInterval;
                    
                    if (activeVote.RemainingTime <= 5) return;
                    
                    BroadcastChat(Lang("VoteReminder", null, activeVote.TargetName, activeVote.RemainingTime));
                    BroadcastChat(Lang("VoteInstructions"));
                });
            }
        }

        [ChatCommand("y")]
        void VoteYes(BasePlayer player, string command, string[] args)
        {
            RegisterVote(player, true);
        }

        [ChatCommand("n")]
        void VoteNo(BasePlayer player, string command, string[] args)
        {
            RegisterVote(player, false);
        }

        void RegisterVote(BasePlayer voter, bool vote)
        {
            if (activeVote == null)
            {
                SendReply(voter, Lang("VoteInProgress", voter.UserIDString));
                return;
            }
            
            if (!permission.UserHasPermission(voter.UserIDString, permVote))
            {
                SendReply(voter, Lang("NoVotePermission", voter.UserIDString));
                return;
            }
            
            if (activeVote.Votes.ContainsKey(voter.userID))
            {
                SendReply(voter, Lang("AlreadyVoted", voter.UserIDString));
                return;
            }
            
            activeVote.Votes[voter.userID] = vote;
            SendReply(voter, $"You voted {(vote ? "<color=#E6BE8A>YES</color>" : "<color=#708090>NO</color>")}");
        }

        void EndVote()
        {
            if (activeVote == null) return;

            if (activeVote.ReminderTimer != null)
            {
                activeVote.ReminderTimer.Destroy();
            }

            int participants;
            float votePercentage;
            var yesVotes = activeVote.Votes.Count(v => v.Value);
            var totalVotes = activeVote.Votes.Count;
            
            if (config.CountOnlyVoters)
            {
                participants = totalVotes > 0 ? totalVotes : 1;
                votePercentage = (yesVotes * 100.0f) / participants;
            }
            else
            {
                participants = BasePlayer.activePlayerList.Count;
                votePercentage = (yesVotes * 100.0f) / participants;
            }
            
            var requiredVotes = (int)Math.Ceiling(participants * (config.RequiredPercentage / 100));

            try
            {
                if (yesVotes >= requiredVotes)
                {
                    ExecuteBan(activeVote, yesVotes, participants, votePercentage);
                    string banDurationText = config.BanDuration > 0 ? $"{config.BanDuration} minutes" : "permanently";
                    BroadcastChat(Lang("VoteSuccess", null, yesVotes, participants, activeVote.TargetName, banDurationText));
                }
                else
                {
                    BroadcastChat(Lang("VoteFailed", null, yesVotes, participants, activeVote.TargetName));
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in EndVote: {ex.Message}");
            }
            finally
            {
                activeVote = null;
            }
        }

        void ExecuteBan(VoteSession vote, int yesVotes, int participants, float votePercentage)
        {
            try
            {
                IPlayer targetIPlayer = covalence.Players.FindPlayer(vote.TargetId.ToString());
                string reason = config.BanDuration > 0 
                    ? $"Banned by vote for {config.BanDuration} minutes" 
                    : "Permanently banned by vote";

                if (targetIPlayer != null)
                {
                    if (config.BanDuration > 0)
                    {
                        tempBans[vote.TargetId] = DateTime.Now.AddMinutes(config.BanDuration);
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, $"banid {vote.TargetId} {config.BanDuration * 60} \"{reason}\"");
                    }
                    else
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, $"banid {vote.TargetId} \"{reason}\"");
                    }

                    var basePlayer = BasePlayer.FindByID(vote.TargetId);
                    if (basePlayer != null)
                    {
                        basePlayer.Kick(reason);
                    }
                }
                else
                {
                    if (config.BanDuration > 0)
                    {
                        tempBans[vote.TargetId] = DateTime.Now.AddMinutes(config.BanDuration);
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, $"banid {vote.TargetId} {config.BanDuration * 60} \"{reason}\"");
                    }
                    else
                    {
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, $"banid {vote.TargetId} \"{reason}\"");
                    }
                }
                SaveBanData();

                SendDiscordEmbed("Vote Ban Success",
                    $"Player **{vote.TargetName}** has been banned via vote.",
                    3066993,
                    new List<Dictionary<string, object>>
                    {
                        new Dictionary<string, object> { { "name", "Player Name" }, { "value", vote.TargetName }, { "inline", true } },
                        new Dictionary<string, object> { { "name", "Player ID" }, { "value", vote.TargetId.ToString() }, { "inline", true } },
                        new Dictionary<string, object> { { "name", "Ban Duration" }, { "value", config.BanDuration > 0 ? $"{config.BanDuration} minutes" : "Permanent" }, { "inline", true } },
                        new Dictionary<string, object> { { "name", "Vote Result" }, { "value", $"{vote.Votes.Count(v => v.Value)}/{participants} ({Math.Round(votePercentage, 1)}%)" }, { "inline", true } },
                        new Dictionary<string, object> { { "name", "Count Mode" }, { "value", config.CountOnlyVoters ? "Only Voters" : "All Players" }, { "inline", true } }
                    }
                );
            }
            catch (Exception ex)
            {
                PrintError($"Error in ExecuteBan: {ex.Message}");
            }
        }

        #endregion

        #region Helper Methods

        BasePlayer FindPlayer(string search)
        {
            return BasePlayer.activePlayerList
                .FirstOrDefault(p => p.displayName.Contains(search, StringComparison.OrdinalIgnoreCase) 
                                   || p.UserIDString == search);
        }

        string Lang(string key, string userId = null, params object[] args)
        {
            try 
            {
                var message = lang.GetMessage(key, this, userId);
                return args == null || args.Length == 0 ? message : string.Format(message, args);
            }
            catch (Exception ex)
            {
                PrintError($"Lang format error for key '{key}': {ex.Message}");
                return key;
            }
        }

        void BroadcastChat(string message)
        {
            foreach (var player in BasePlayer.activePlayerList)
                SendReply(player, message);
            
            if (config.LogToConsole)
                Puts(message);
        }

        async Task SendDiscordEmbed(string title, string description, int color, List<Dictionary<string, object>> fields)
        {
            if (string.IsNullOrEmpty(config.WebhookUrl)) return;

            var embed = new Dictionary<string, object>
            {
                {"title", title},
                {"description", description},
                {"color", color},
                {"timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}
            };

            if (fields != null && fields.Count > 0)
            {
                embed["fields"] = fields;
            }

            var payloadObj = new Dictionary<string, object>
            {
                {"username", "Vote Ban System"},
                {"embeds", new object[] { embed } }
            };

            var payload = JsonConvert.SerializeObject(payloadObj);

            try
            {
                webrequest.EnqueuePost(
                    config.WebhookUrl.TrimEnd('/'),
                    payload,
                    (code, response) =>
                    {
                        if (code != 204 && code != 200)
                            PrintError($"Discord webhook error: {code} - {response}");
                    },
                    this,
                    new Dictionary<string, string>
                    {
                        ["Content-Type"] = "application/json"
                    }
                );
            }
            catch (Exception ex)
            {
                PrintError($"Discord embed log failed: {ex.Message}");
            }
        }

        void LoadBanData()
        {
            tempBans = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, DateTime>>(Name) ?? new Dictionary<ulong, DateTime>();
        }

        void SaveBanData()
        {
            Interface.Oxide.DataFileSystem.WriteObject(Name, tempBans);
        }

        #endregion

        void CheckTempBans()
        {
            var now = DateTime.Now;
            foreach (var entry in tempBans.Where(entry => entry.Value <= now).ToList())
            {
                try
                {
                    ConsoleSystem.Run(ConsoleSystem.Option.Server, $"unban {entry.Key}");
                    var targetName = covalence.Players.FindPlayerById(entry.Key.ToString())?.Name ?? "Unknown";
                    
                    SendDiscordEmbed("Temporary Ban Expired",
                        $"Player **{targetName}** has been unbanned.",
                        3447003,
                        new List<Dictionary<string, object>>
                        {
                            new Dictionary<string, object> { { "name", "Player Name" }, { "value", targetName }, { "inline", true } },
                            new Dictionary<string, object> { { "name", "Player ID" }, { "value", entry.Key.ToString() }, { "inline", true } }
                        }
                    );
                }
                catch (Exception ex)
                {
                    PrintError($"Error unbanning player with ID {entry.Key}: {ex.Message}");
                }
                tempBans.Remove(entry.Key);
            }
            SaveBanData();
        }
    }
} 