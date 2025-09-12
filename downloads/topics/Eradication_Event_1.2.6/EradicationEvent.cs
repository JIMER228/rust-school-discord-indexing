using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

#region patches

//V1.0.1 Patched to kill gui on event end. Added Colors to Winner and announcments. Shortened end and start commands to /erad and /eradend
//v1.0.2 Completely Repatched due to some issues with Timers and Participants.
//v1.0.3 Added option to toggle scheduled events.
//v1.0.4 Added flag to check if event isnt running to prevent console messages of participation.
//v1.0.5 Added more checks, added a participant clear at the end of the event.
//v1.0.6 Added even more checks to make sure nothing tracks or ui activating when event isnt live. Also worked on ending event.
//v1.0.7 Method in ending event, if there was a tie, it would decide winner, but still say nobody participated. Added missing event tags, added missing colors.
//v1.0.8 If one person won, it would announce nobody participated, and the winner at the same time. Removed Double SFX.
//v1.1.0 Added config option to disable or enable loot table, sfx,  Added Economics x Server Rewards Support, reworked winner announcement.
//v.1.1.1 Added a Check and Catch if SR or Eco is not loaded. If not it will proceed with event end as needed.
//v1.1.2 Cleaned up Timer for Auto sched.
//v1.1.3 Tweaked the way the plugin detects if an event is able to be Scheduled depending on active players.
//v1.1.4 Added a Min Max Item Randomizer for the Loot Table.
//v1.1.5 Corrected a line if config option was null.
//v1.2.0 Converted Old UI To Leaderboard Style UI With Countdown, Added Hooks for use with HUD.
//v1.2.1 Added a check, if the threshold for players needed for events, fell below during an event, it wouldn't schedule the next after that event ended, until threshold is reached again.
//v1.2.2 Added new config options for commands and event messaging / toggle, ui positioning, also redid the entire lang for the plugin, now it can be customized as needed, cleaned up and reorganized code.
//v1.2.3 Worked out every outcome if loot table, SR, or Eco are being used in diff combos, to print the correct output to chat of rewards given.
//v1.2.4 UI refresh wasnt being called for participants, would only update locally on kill, Resolved that.
//v1.2.5 Added option to run commands for the winner.
//v1.2.6 Rewards Dist. Section cleaned up.

#endregion


namespace Oxide.Plugins

{
    [Info("Eradication Event", "Wrecks", "1.2.6")]
    [Description(
        "Wildlife Killing Event, Winner gets Loot you state in Table, Features A UI Leaderboard that tracks time and kills while counting down.")]
    class EradicationEvent : RustPlugin

    {
        [PluginReference] private Plugin Economics, ServerRewards;

        #region declarations

        private class PlayerData
        {
            public int KillCount { get; set; }
        }

        private bool enableServerRewards;
        private bool enableEconomicRewards;
        private bool enableLootTable;
        private bool isEradEventRunning;
        private bool eradEventStarted;
        private Dictionary<string, PlayerData> playerData = new Dictionary<string, PlayerData>();
        private Timer eradEventTimer;
        private string winnerSteamID;
        private int highestKillCount;
        private int minPlayersToStart;
        private int randomEventMin;
        private int randomEventMax;
        private int eventDuration;
        private ItemDefinition rewardItemDef;
        private int rewardQuantity;
        private Dictionary<string, string> messages;
        private List<Dictionary<string, object>> winnerRewards;
        private string startEventSound = "assets/bundled/prefabs/fx/player/howl.prefab";
        private bool enableStartSfx;
        private string winnerSoundEffect = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab";
        private bool manuallyEnded;
        private bool isEventScheduledEnabled;
        private bool chatCountdown;
        private Timer endEventTimer;
        private Timer eradEventScheduler;
        private List<string> eradParticipatingPlayers = new List<string>();

        private Dictionary<ulong, CuiElementContainer>
            playerUiContainers = new Dictionary<ulong, CuiElementContainer>();

        private string eradEventStartCommand;
        private string eradEventEndCommand;
        private const string StartPermission = "eradicationevent.start";
        private const string EndPermission = "eradicationevent.end";
        private float scheduledTime;
        private bool firstEventScheduled;
        private bool rewardsGiven;
        private bool EnableWinnerCommands;

        private readonly List<string> killCountColors = new List<string>
        {
            "0.7 0.7 0 1",
            "0.8 0.8 0 1",
            "0.9 0.9 0 1",
            "1 1 0 1",
            "1 1 0.2 1",
            "1 1 0.3 1",
        };

        #endregion

        #region oxidehooks

        void Init()
        {
            CheckPluginLoaded("Economics");
            CheckPluginLoaded("ServerRewards");
            LoadConfig();
            LoadMessages();
            isEventScheduledEnabled = Config.Get<bool>("EnableScheduledEvents");
            var eradStartCommand = Config.Get<string>("EradStartCommand", "erad");
            AddCovalenceCommand(eradEventStartCommand, nameof(CmdStartAnimalKillTimer), StartPermission);
            var eradEndCommand = Config.Get<string>("EradEndCommand", "eradend");
            AddCovalenceCommand(eradEventEndCommand, nameof(CmdEndErad), EndPermission);
            enableStartSfx = Config.Get<bool>("EnableStartSFX");
            chatCountdown = Config.Get<bool>("ChatCountdownEnabled");
            EnableWinnerCommands = Config.Get<bool>("EnableWinnerCommands");
        }

        private void OnServerInitialized()
        {
            if (CanScheduleNextErad())
            {
                if (isEventScheduledEnabled && !eradEventStarted)
                {
                    ScheduleNextErad();
                }
                else if (eradEventStarted && isEventScheduledEnabled)
                {
                    double timeRemaining = (scheduledTime - Time.realtimeSinceStartup);
                    double minutesRemaining = Math.Ceiling(timeRemaining / 60);
                    Puts($"The next Eradication Event will start in {minutesRemaining} minute(s).");
                }
            }
            else
            {
                Puts("Not enough players to schedule the Eradication Event.");
            }
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (eradEventStarted && entity is BaseAnimalNPC)
            {
                BasePlayer attacker = hitInfo?.Initiator as BasePlayer;

                if (attacker != null)
                {
                    PlayerData EradattackerData = GetPlayerData(attacker.UserIDString);
                    EradattackerData.KillCount++;

                    int killCount = EradattackerData.KillCount;

                    if (!eradParticipatingPlayers.Contains(attacker.UserIDString))
                    {
                        eradParticipatingPlayers.Add(attacker.UserIDString);
                    }

                    if (eradEventStarted)
                    {
                        ShowEradUI(attacker, killCount);

                        foreach (string participantID in eradParticipatingPlayers)
                        {
                            BasePlayer participant = BasePlayer.Find(participantID);
                            if (participant != null)
                            {
                                ShowEradUI(participant, killCount);
                            }
                        }
                    }
                }
            }
        }


        void OnPlayerConnected(BasePlayer player)
        {
            // Check if there are enough players online and schedule the next event if needed
            if (!isEradEventRunning && isEventScheduledEnabled && CanScheduleNextErad())
            {
                ScheduleNextErad();
            }
        }

        //Triggers KillTimer if not enough players online.
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(1f, () =>
            {
                if (BasePlayer.activePlayerList.Count < minPlayersToStart && nextEradEventTimer != null &&
                    !nextEradEventTimer.Destroyed)
                {
                    CancelScheduledEradEvent();
                }
            });
        }

        void Unload()
        {
            if (eradEventStarted)
            {
                EndEradEvent();
            }

            if (eradEventScheduler != null)
            {
                eradEventScheduler.Destroy();
                eradEventScheduler = null;
            }

            if (eradEventTimer != null)
            {
                eradEventTimer.Destroy();
                eradEventTimer = null;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyEradUI(player);
            }
        }

        #endregion

        #region eventmethods

        private void CmdStartAnimalKillTimer(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(StartPermission))
            {
                if (eradEventStarted)
                {
                    player.Message(lang.GetMessage("EventAlreadyRunning", this));
                    return;
                }

                if (eradEventScheduler != null)
                {
                    eradEventScheduler.Destroy();
                    eradEventScheduler = null;
                }

                StartEradEvent();
            }
            else
            {
                player.Message(messages["NoPermissionStart"]);
            }
        }

        private void CmdEndErad(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(EndPermission))
            {
                if (eradEventStarted)
                {
                    manuallyEnded = true;
                    Server.Broadcast(lang.GetMessage("ManuallyEnded", this));
                    EndEradEvent();
                }
                else
                {
                    player.Message(lang.GetMessage("EventNotRunning", this));
                }
            }
            else
            {
                player.Message(lang.GetMessage("NoPermissionEnd", this));
            }
        }

        private void StartEradEvent()
        {
            eradEventStarted = true;
            Interface.CallHook("OnEradEventStart"); //Start Event Hook
            rewardsGiven = false;
            playerData.Clear();
            highestKillCount = 0;
            winnerSteamID = null;

            /*Generate fake player data for leaderboard testing
            for (int i = 0; i < 3; i++)
                {
                    ulong fakePlayerID = 76543210 + (ulong)i;
                    int fakeKillCount = UnityEngine.Random.Range(1, 15);

                    // Create a new PlayerData instance and add it to the playerData dictionary
                    playerData[fakePlayerID.ToString()] = new PlayerData { KillCount = fakeKillCount };
                }*/

            if (eradEventTimer != null)
            {
                eradEventTimer.Destroy();
                eradEventTimer = null;
            }

            int eventDuration = GetConfigValue("EventDuration(Seconds)", 600);
            eradEventEndTime = DateTime.UtcNow.AddSeconds(eventDuration); // Set eradEventEndTime
            Server.Broadcast(lang.GetMessage("eradEventStarted", this));
            if (enableStartSfx)
            {
                //SFX
                foreach (var player in BasePlayer.activePlayerList)
                {
                    EffectNetwork.Send(new Effect(startEventSound, player.transform.position, Vector3.zero));
                }
            }

            eradEventTimer = timer.Every(1f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiElementContainer eradContainer;
                    if (playerUiContainers.TryGetValue(player.userID, out eradContainer))
                    {
                        AddEradTimeLeftElement(player);
                    }
                }
            });
            int countdownIntervalMinutes = GetConfigValue("ChatCountdownInterval(Minutes)", 1);
            eradEventTimer = timer.Every(countdownIntervalMinutes * 60f, () =>
            {
                int minutesLeft = MinutesLeft;

                if (minutesLeft == 0)
                {
                    isEradEventRunning = false;
                    EndEradEvent();
                    return;
                }

                if (chatCountdown)
                {
                    string messageKey = lang.GetMessage("CountdownText", this);
                    string formattedMessage = string.Format(lang.GetMessage(messageKey, null), minutesLeft);
                    Server.Broadcast(formattedMessage);
                }
            });
        }

        private void OnEradEventStarted(DateTime eradEventEndTime)
        {
            Server.Broadcast(lang.GetMessage("eradEventStarted", this));

            foreach (var player in BasePlayer.activePlayerList)
            {
                var playerData = GetPlayerData(player.UserIDString);
                playerData.KillCount = 0;
            }

            eradEventTimer = timer.Every(1f, () =>
            {
                TimeSpan remainingTime = eradEventEndTime - DateTime.UtcNow;
                int secondsRemaining = Mathf.Max(0, (int)remainingTime.TotalSeconds);
                if (secondsRemaining == 0)
                {
                    Server.Broadcast(lang.GetMessage("HasEnded", this));
                    isEradEventRunning = false;
                    EndEradEvent();
                    return;
                }
            });
        }

        private void EndEradEvent()
        {
            if (eradEventTimer != null)
            {
                eradEventTimer.Destroy();
                eradEventTimer = null;
            }

            eradEventStarted = false;
            isEradEventRunning = false;

            // Destroy the Kill Count UI for all connected players
            foreach (var playerID in eradParticipatingPlayers)
            {
                BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(playerID));
                if (player != null)
                {
                    DestroyEradUI(player);
                }
            }

            var anyParticipantWithKills = false;
            var winners = new List<string>();
            var highestKillCount = 0;

            foreach (var playerID in eradParticipatingPlayers)
            {
                PlayerData playerData = GetPlayerData(playerID);
                if (playerData.KillCount > 0)
                {
                    anyParticipantWithKills = true;

                    int killCount = playerData.KillCount;

                    if (killCount > highestKillCount)
                    {
                        highestKillCount = killCount;
                        winners.Clear();
                        winners.Add(playerID);
                    }
                    else if (killCount == highestKillCount)
                    {
                        winners.Add(playerID);
                    }
                }
            }

            if (anyParticipantWithKills)
            {
                if (winners.Count > 1)
                {
                    // There is a tie among multiple players
                    StringBuilder winnerMessageBuilder = new StringBuilder();
                    string tieMessage = lang.GetMessage("TieMessage", this);
                    string formattedTieMessage = string.Format(tieMessage, winners.Count, highestKillCount);
                    winnerMessageBuilder.AppendLine(formattedTieMessage);

                    // Delays the announcement for the tiebreaker by 3 seconds for dramatic effect.
                    timer.Once(3f, () =>
                    {
                        int randomIndex = UnityEngine.Random.Range(0, winners.Count);
                        string randomWinnerSteamID = winners[randomIndex];
                        string winnerName = covalence.Players.FindPlayerById(randomWinnerSteamID)?.Name ?? "Unknown";
                        ulong randomWinnerUlongID;
                        if (ulong.TryParse(randomWinnerSteamID, out randomWinnerUlongID))
                        {
                            ExecuteCommands(randomWinnerUlongID);
                        }

                        string rewardMessage =
                            GiveWinnerRewards(randomWinnerSteamID, highestKillCount);

                        string winnerMessage = lang.GetMessage("TieWinnerMessage", this);
                        string formattedWinnerMessage = string.Format(winnerMessage, winnerName, highestKillCount);
                        winnerMessageBuilder.AppendLine(formattedWinnerMessage);
                        winnerMessageBuilder.AppendLine(rewardMessage);
                        string winnerAnnounce = lang.GetMessage("WinnerAnnounce", this);
                        string formattedWinnerAnnounce = string.Format(winnerAnnounce, winnerMessageBuilder.ToString());
                        Server.Broadcast(formattedWinnerAnnounce);
                    });
                }
                else if (winners.Count == 1)
                {
                    // There is only one winner
                    string winnerID = winners[0];
                    string winnerName = covalence.Players.FindPlayerById(winnerID)?.Name ?? "Unknown";
                    ulong winnerUlongID;
                    if (ulong.TryParse(winnerID, out winnerUlongID))
                    {
                        ExecuteCommands(winnerUlongID);
                    }

                    StringBuilder winnerMessageBuilder = new StringBuilder();
                    string endMessage = lang.GetMessage("EndMessage", this);
                    string formattedEndMessage = string.Format(endMessage, winnerName, highestKillCount);
                    winnerMessageBuilder.AppendLine(formattedEndMessage);
                    string rewardMessage =
                        GiveWinnerRewards(winnerID, highestKillCount);
                    winnerMessageBuilder.AppendLine(rewardMessage);
                    string winnerAnnounce = lang.GetMessage("WinnerAnnounce", this);
                    string formattedWinnerAnnounce = string.Format(winnerAnnounce, winnerMessageBuilder.ToString());
                    Server.Broadcast(formattedWinnerAnnounce);
                }
            }
            else
            {
                // No participants with kills
                Server.Broadcast(lang.GetMessage("EventEndedNoParticipants", this));
            }

            eradParticipatingPlayers.Clear();
            playerData.Clear();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyEradUI(player);
            }

            ScheduleNextErad();

            Interface.CallHook("OnEradEventEnd"); // End Event Hook
        }


        private string GiveWinnerEconomicReward(string winnerSteamID, BasePlayer player)
        {
            int economyRewardAmount = Config.Get<int>("EconomicRewards");

            if (economyRewardAmount <= 0)
            {
                return null;
            }

            bool economyRewardGiven = false;

            try
            {
                Economics.Call("Deposit", winnerSteamID, (double)economyRewardAmount);
                economyRewardGiven = true;
                string economicsMessage = lang.GetMessage("EconomicRewardMessage", this);
                return string.Format(economicsMessage, player.displayName, economyRewardAmount);
            }
            catch
            {
                Puts("Economics plugin not found or an error occurred while processing economic rewards.");
            }

            return null;
        }

        private string GiveWinnerServerReward(string winnerSteamID, BasePlayer player)
        {
            int serverRewardsAmount = Config.Get<int>("ServerRewards");

            if (serverRewardsAmount <= 0)
            {
                return null;
            }

            bool serverRewardsGiven = false;

            try
            {
                ServerRewards.Call("AddPoints", winnerSteamID, serverRewardsAmount);
                serverRewardsGiven = true;
                string serverRewardsMessage = lang.GetMessage("ServerRewardsMessage", this);
                return string.Format(serverRewardsMessage, player.displayName, serverRewardsAmount);
            }
            catch
            {
                Puts("Server Rewards plugin not found or an error occurred while processing server rewards.");
            }

            return null;
        }

        private string GiveWinnerItemRewards(string winnerSteamID, BasePlayer player)
        {
            List<Dictionary<string, object>> winnerRewards =
                Config.Get<List<Dictionary<string, object>>>("WinnerRewards");
            int RandomItemsMin = Config.Get<int>("RandomItemsMin");
            int RandomItemsMax = Config.Get<int>("RandomItemsMax");

            if (winnerRewards == null || winnerRewards.Count == 0)
            {
                return null;
            }

            winnerRewards = winnerRewards.OrderBy(x => Guid.NewGuid()).ToList();

            int numberOfItemsToGive = new System.Random().Next(RandomItemsMin, RandomItemsMax + 1);

            StringBuilder rewardMessageBuilder = new StringBuilder();
            int itemsGiven = 0;

            foreach (var rewarddata in winnerRewards)
            {
                if (itemsGiven >= numberOfItemsToGive)
                {
                    break;
                }

                string shortname = rewarddata.ContainsKey("shortname")
                    ? rewarddata["shortname"].ToString()
                    : null;
                int minQuantity = rewarddata.ContainsKey("minQuantity")
                    ? Convert.ToInt32(rewarddata["minQuantity"])
                    : 1;
                int maxQuantity = rewarddata.ContainsKey("maxQuantity")
                    ? Convert.ToInt32(rewarddata["maxQuantity"])
                    : 1;
                int randomizedQuantity = new System.Random().Next(minQuantity, maxQuantity + 1);
                ulong skinID = rewarddata.ContainsKey("skinid") ? Convert.ToUInt64(rewarddata["skinid"]) : 0;
                string name = rewarddata.ContainsKey("name") ? rewarddata["name"].ToString() : null;

                if (!string.IsNullOrEmpty(shortname) && randomizedQuantity > 0)
                {
                    ItemDefinition itemDef = ItemManager.FindItemDefinition(shortname);
                    if (itemDef != null)
                    {
                        Item item;
                        if (skinID > 0)
                        {
                            item = ItemManager.CreateByItemID(itemDef.itemid, randomizedQuantity, skinID);
                        }
                        else
                        {
                            item = ItemManager.Create(itemDef, randomizedQuantity);
                        }

                        if (item != null)
                        {
                            string displayName = !string.IsNullOrEmpty(name) ? name : itemDef.displayName.english;
                            if (!string.IsNullOrEmpty(name))
                            {
                                item.name = name;
                            }

                            player.GiveItem(item);
                            string rewardMessage = lang.GetMessage("RewardMessage", this);
                            string formattedRewardMessage =
                                string.Format(rewardMessage, randomizedQuantity, displayName);
                            rewardMessageBuilder.AppendLine(formattedRewardMessage);
                            itemsGiven++;
                        }
                    }
                }
            }

            if (itemsGiven > 0)
            {
                if (Config.Get<bool>("EnableLootTable"))
                {
                    string playerEarnedMessage = lang.GetMessage("PlayerEarnedMessage", this);
                    return string.Format(playerEarnedMessage, player.displayName, rewardMessageBuilder.ToString());
                }
            }

            return null;
        }


        private string GiveWinnerRewards(string winnerSteamID, int highestKillCount)
        {
            BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(winnerSteamID));

            if (player == null)
            {
                Puts("Winner not found in the game. Unable to give rewards.");
                return "Winner not found in the game. Unable to give rewards.";
            }

            StringBuilder rewardMessageBuilder = new StringBuilder();

            string economicRewardMessage = GiveWinnerEconomicReward(winnerSteamID, player);
            if (!string.IsNullOrEmpty(economicRewardMessage))
            {
                rewardMessageBuilder.AppendLine(economicRewardMessage);
            }

            string serverRewardMessage = GiveWinnerServerReward(winnerSteamID, player);
            if (!string.IsNullOrEmpty(serverRewardMessage))
            {
                rewardMessageBuilder.AppendLine(serverRewardMessage);
            }

            string itemRewardMessage = GiveWinnerItemRewards(winnerSteamID, player);
            if (!string.IsNullOrEmpty(itemRewardMessage))
            {
                rewardMessageBuilder.AppendLine(itemRewardMessage);
            }

            // Play the sound effect for the winner
            EffectNetwork.Send(new Effect(winnerSoundEffect, player.transform.position, Vector3.zero));

            return rewardMessageBuilder.ToString();
        }

        private void ExecuteCommands(ulong player)
        {
            if (!EnableWinnerCommands)
            {
                PrintWarning("Commands are not enabled.");
                return;
            }

            var commandsList = Config.Get<List<Dictionary<string, object>>>("WinnerCommands");

            if (commandsList != null && commandsList.Count > 0)
            {
                foreach (var commandData in commandsList)
                {
                    if (commandData.TryGetValue("Command", out var commandObj) && commandObj is string command)
                    {
                        if (!string.IsNullOrEmpty(command))
                        {
                            var replacedCommand = command.Replace("{id}", player.ToString());
                            ConsoleSystem.Run(ConsoleSystem.Option.Server.Quiet(), replacedCommand);
                        }
                    }
                }
            }
            else
            {
                PrintWarning("No commands found in the configuration.");
            }
        }


        private PlayerData GetPlayerData(string playerID)
        {
            PlayerData data;
            if (!playerData.TryGetValue(playerID, out data))
            {
                data = new PlayerData();
                playerData[playerID] = data;
            }

            return data;
        }

        private bool CanScheduleNextErad()
        {
            bool canStart = BasePlayer.activePlayerList.Count >= minPlayersToStart;
            return canStart;
        }


        private DateTime eradEventEndTime; // Declare eradEventEndTime as a field

        // Property to calculate and return minutesLeft
        private int MinutesLeft
        {
            get
            {
                TimeSpan remainingTime = eradEventEndTime - DateTime.UtcNow;
                int secondsRemaining = Mathf.Max(0, (int)remainingTime.TotalSeconds);
                return Mathf.CeilToInt((float)secondsRemaining / 60f);
            }
        }

        private string EradFormatTime(TimeSpan timeSpan)
        {
            // Extract hours, minutes, and seconds
            int hours = timeSpan.Hours;
            int minutes = timeSpan.Minutes;
            int seconds = timeSpan.Seconds;

            // Create the formatted string
            string formattedTime = "";

            if (hours > 0)
            {
                formattedTime += $"{hours:D1}h ";
            }

            if (minutes > 0 || hours > 0)
            {
                formattedTime += $"{minutes:D1}m ";
            }

            formattedTime += $"{seconds:D1}s";

            return formattedTime;
        }

        // Scheduling.
        private string FormatTimeSpan(float seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            int totalHours = (int)timeSpan.TotalHours;
            int totalMinutes = (int)timeSpan.Minutes;

            //Puts($"seconds: {seconds}, totalHours: {totalHours}, totalMinutes: {totalMinutes}"); // DebugSched.

            if (totalHours > 0 && totalMinutes > 0)
            {
                return $"{totalHours} hour(s) and {totalMinutes} minute(s)";
            }
            else if (totalHours > 0)
            {
                return $"{totalHours} hour(s)";
            }
            else
            {
                return $"{totalMinutes} minute(s)";
            }
        }

        private double nextEradEventTime;

        private Timer nextEradEventTimer;

        private void ScheduleNextErad()
        {
            // Check if there's an existing timer and return if so
            if (nextEradEventTimer != null && !nextEradEventTimer.Destroyed)
            {
                // Puts("An event is already scheduled."); //Debug
                return;
            }

            if (!isEventScheduledEnabled)
            {
                Puts("Scheduled events are disabled in the config.");
                return;
            }

            if (!CanScheduleNextErad())
            {
                Puts("Not enough players to schedule the event.");
                return;
            }

            int randomSeconds = UnityEngine.Random.Range(randomEventMin, randomEventMax + 1);

            nextEradEventTime = GrabCurrentTime() + randomSeconds;
            Puts($"The next Eradication Event will start in {FormatTimeSpan(randomSeconds)}.");

            nextEradEventTimer = timer.Once(randomSeconds, StartEradEvent);
        }

        private double GrabCurrentTime()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        // Method above triggers the KillTimer
        private void CancelScheduledEradEvent()
        {
            if (nextEradEventTimer != null)
            {
                //Puts("nextEradEventTimer is not null");
                if (!nextEradEventTimer.Destroyed)
                {
                    //Puts("nextEradEventTimer is not destroyed, attempting to destroy");
                    nextEradEventTimer.Destroy();
                    nextEradEventTimer = null;
                }
                else
                {
                    //Puts("nextEradEventTimer is already destroyed");
                }
            }
            else
            {
                //Puts("nextEradEventTimer is null");
            }

            Puts("Eradication Event has been canceled due to insufficient players.");
        }

        void CheckPluginLoaded(string pluginName)
        {
            Plugin plugin = plugins.Find(pluginName);

            if (plugin == null)
            {
                Puts($"The plugin {pluginName} is not loaded!");
            }
        }

        #endregion

        #region config

        protected override void LoadDefaultConfig()
        {
            Config["EradStartCommand"] = "erad";
            Config["EradEndCommand"] = "eradend";
            Config["LeaderboardUIAnchorMin"] = "0.5 1";
            Config["LeaderboardUIAnchorMax"] = "0.5 1";
            Config["LeaderboardUIOffsetMin"] = "-450 -150";
            Config["LeaderboardUIOffsetMax"] = "-250 0";
            Config["EnableEconomicRewards"] = true;
            Config["EnableServerRewards"] = true;
            Config["EnableStartSFX"] = true;
            Config["EnableScheduledEvents"] = true;
            Config["ChatCountdownEnabled"] = true;
            Config["ChatCountdownInterval(Minutes)"] = 1;
            Config["MinimumPlayersToStart"] = 2;
            Config["MinimumTimeBetweenEvents(Seconds)"] = 3600;
            Config["MaximumTimeBetweenEvents(Seconds)"] = 7200;
            Config["EventDuration(Seconds)"] = 600;
            Config["EconomicRewards"] = 1000;
            Config["ServerRewards"] = 1000;
            Config["EnableLootTable"] = true;
            Config["RandomItemsMin"] = 1;
            Config["RandomItemsMax"] = 3;
            Config["EnableWinnerCommands"] = false;

            Config["WinnerCommands"] = GetConfigValue("WinnerCommands", new List<object>
            {
                new Dictionary<string, object>
                {
                    { "Command", "inventory.giveto {id} stones 100" },
                },
                new Dictionary<string, object>
                {
                    { "Command", "inventory.giveto {id} wood 100" },
                }
            });


            Config["WinnerRewards"] = GetConfigValue("WinnerRewards", new List<object>
            {
                new Dictionary<string, object>
                {
                    { "shortname", "paper" },
                    { "minQuantity", 1 },
                    { "maxQuantity", 5 },
                    { "skinid", null },
                    { "name", "Hunting Spoils" }
                },
                new Dictionary<string, object>
                {
                    { "shortname", "scrap" },
                    { "minQuantity", 5 },
                    { "maxQuantity", 15 },
                    { "skinid", null },
                    { "name", "" }
                }
            });
        }

        private void LoadConfig()
        {
            try
            {
                //RectTransform = { AnchorMin = $"0.5 1", AnchorMax = $"0.5 1", OffsetMin = $"{-450f} {-150f}", OffsetMax = $"{-250f} {0f}" },
                eradEventStartCommand = GetConfigValue("EradStartCommand", "erad");
                eradEventEndCommand = GetConfigValue("EradEndCommand", "eradend");
                Config["LeaderboardUIAnchorMin"] = GetConfigValue("LeaderboardUIAnchorMin", "0.5 1");
                Config["LeaderboardUIAnchorMax"] = GetConfigValue("LeaderboardUIAnchorMax", "0.5 1");
                Config["LeaderboardUIOffsetMin"] = GetConfigValue("LeaderboardUIOffsetMin", "-450 -150");
                Config["LeaderboardUIOffsetMax"] = GetConfigValue("LeaderboardUIOffsetMax", "-250 0");
                Config["EnableServerRewards"] = GetConfigValue("EnableServerRewards", false);
                Config["EnableEconomicRewards"] = GetConfigValue("EnableEconomicRewards", false);
                Config["EnableStartSFX"] = GetConfigValue("EnableStartSFX", true);
                Config["EnableScheduledEvents"] = GetConfigValue("EnableScheduledEvents", true);
                Config["ChatCountdownEnabled"] = GetConfigValue("ChatCountdownEnabled", true);
                Config["ChatCountdownInterval(Minutes)"] = GetConfigValue("ChatCountdownInterval(Minutes)", 1);
                minPlayersToStart = GetConfigValue("MinimumPlayersToStart", 2);
                randomEventMin = GetConfigValue("MinimumTimeBetweenEvents(Seconds)", 3600);
                Config["MinimumTimeBetweenEvents(Seconds)"] = randomEventMin;
                randomEventMax = GetConfigValue("MaximumTimeBetweenEvents(Seconds)", 7200);
                Config["MaximumTimeBetweenEvents(Seconds)"] = randomEventMax;
                Config["EventDuration(Seconds)"] = GetConfigValue("EventDuration(Seconds)", 600);
                Config["EconomicRewards"] = GetConfigValue("EconomicRewards", 1000);
                Config["ServerRewards"] = GetConfigValue("ServerRewards", 1000);
                Config["EnableLootTable"] = GetConfigValue("EnableLootTable", true);
                Config["RandomItemsMin"] = GetConfigValue("RandomItemsMin", 1);
                Config["RandomItemsMax"] = GetConfigValue("RandomItemsMax", 3);
                Config["EnableWinnerCommands"] = GetConfigValue("EnableWinnerCommands", false);

                Config["WinnerCommands"] = GetConfigValue("WinnerCommands", new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "Command", "inventory.giveto {id} stones 100" },
                    },
                    new Dictionary<string, object>
                    {
                        { "Command", "inventory.giveto {id} wood 100" },
                    }
                });

                Config["WinnerRewards"] = GetConfigValue("WinnerRewards", new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "shortname", "paper" },
                        { "minQuantity", 1 },
                        { "maxQuantity", 5 },
                        { "skinid", null },
                        { "name", "Hunting Spoils" }
                    },
                    new Dictionary<string, object>
                    {
                        { "shortname", "scrap" },
                        { "minQuantity", 5 },
                        { "maxQuantity", 15 },
                        { "skinid", null },
                        { "name", "" }
                    }
                });


                SaveConfig();
            }
            catch (Exception ex)
            {
                PrintError("Error loading configuration: " + ex.Message);
            }
        }

        T GetConfigValue<T>(string key, T defaultValue)
        {
            if (Config[key] == null)
            {
                Config[key] = defaultValue;
                SaveConfig();
            }

            return (T)Config[key];
        }

        #endregion

        #region localization

        void LoadMessages()
        {
            messages = new Dictionary<string, string>
            {
                {
                    "eradEventStarted",
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Eradication Event has started! <color=#cd3332>Kill</color> as much Wildlife as you can!"
                },
                {
                    "EventEndedNoParticipants",
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Event has ended! Unfortunately, no one participated."
                },
                {
                    "EventEndedWinner",
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Event has ended! The winner is <color=#cd32cd>{0}</color> with <color=#8032cd>{1}</color> Kills!"
                },
                {
                    "TieMessage",
                    "The Eradication Event has ended! There was a tie among <color=#32CD32>{0}</color> players with <color=#8032cd>{1}</color> kills."
                },
                {
                    "TieWinnerMessage",
                    "The winner was randomly selected: <color=#32CD32>{0}</color> with <color=#8032cd>{1}</color> kills!"
                },
                {
                    "WinnerAnnounce",
                    "[<color=#32CD32>ERADICATION EVENT</color>] {0}"
                },
                {
                    "NotEnoughPlayers",
                    "[<color=#32CD32>ERADICATION EVENT</color>] Not enough players online to start the Event."
                },
                {
                    "EventAlreadyRunning", //used
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Event is already running."
                },
                {
                    "EventNotRunning", //used
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Event is not currently running."
                },
                {
                    "NoPermissionStart",
                    "[<color=#32CD32>ERADICATION EVENT</color>] You don't have permission to start the Eradication Event."
                },
                {
                    "NoPermissionEnd", //used
                    "[<color=#32CD32>ERADICATION EVENT</color>] You don't have permission to end the Eradication Event."
                },
                {
                    "HasEnded",
                    "The Eradication Event Has Ended!"
                },
                {
                    "EndMessage",
                    "\n\nThe Eradication Event has ended!\n\nThe winner is <color=#32CD32>{0}</color> with <color=#8032cd>{1}</color> kill(s)!"
                },
                {
                    "RewardMessage",
                    "\n<color=#32CD32>{0}</color> <color=#8032cd>x</color> <color=#32CD32>{1}</color>"
                },
                {
                    "EconomicRewardMessage",
                    "\n<color=#880888>{0}</color> received <color=#bb9b65>$</color><color=#85bb65>{1}</color>!"
                },
                {
                    "ServerRewardsMessage",
                    "\n<color=#880888>{0}</color> also received <color=#85bb65>{1}</color><color=#bb9b65> RP</color>!"
                },
                {
                    "PlayerEarnedMessage",
                    "\n<color=#880888>{0}</color> earned:\n{1}"
                },
                {
                    "ManuallyEnded",
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Event was manually Ended."
                },
                {
                    "CountdownText",
                    "[<color=#32CD32>ERADICATION EVENT</color>] The Eradication Event will end in <color=#8032cd>{0}</color> minute(s)."
                }
            };

            lang.RegisterMessages(messages, this);
        }

        #endregion

        #region ui

        private string eradKillCount = "EradKillCountPanel";

        private void ShowEradUI(BasePlayer player, int playerKillCount)
        {
            DestroyEradUI(player);

            CuiElementContainer eradcontainer = new CuiElementContainer();

            // Customize the position and appearance of the UI elements as per your preference
            string eradTextElementName = "KillCountText";
            string EradTempTextElementName = "EradTempText"; // Define your EradTempTextElementName

            eradcontainer.Add(new CuiPanel
            {
                //RectTransform = { AnchorMin = $"0.5 1", AnchorMax = $"0.5 1", OffsetMin = $"{-450f} {-150f}", OffsetMax = $"{-250f} {0f}" },
                Image = { Color = "35 35 35 0.17" },
                RectTransform =
                {
                    AnchorMin = GetConfigValue("LeaderboardUIAnchorMin", "0.5 1"),
                    AnchorMax = GetConfigValue("LeaderboardUIAnchorMax", "0.5 1"),
                    OffsetMin = GetConfigValue("LeaderboardUIOffsetMin", "-450 -150"),
                    OffsetMax = GetConfigValue("LeaderboardUIOffsetMax", "-250 0")
                },
            }, "Hud", eradKillCount);
            eradcontainer.Add(new CuiElement
            {
                Name = eradTextElementName,
                Parent = eradKillCount,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = " <color=#32CD32>ERADICATION </color><color=#32CD32>EVENT</color> ", FontSize = 17,
                        Font = "permanentmarker.ttf", Align = TextAnchor.UpperCenter
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.975" },
                    new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                }
            });

            // Check the number of entries in playerData
            int numEntries = playerData.Count;
            if (numEntries <= 4)
            {
                // If there are 3 or fewer entries, add the EradTempTextElementName
                eradcontainer.Add(new CuiElement
                {
                    Name = EradTempTextElementName,
                    Parent = eradKillCount,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "<color=#32CD32>Kill the most Wildlife!</color>", FontSize = 16,
                            Font = "permanentmarker.ttf", Align = TextAnchor.UpperCenter
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.3" },
                        new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                    }
                });
            }

            TimeSpan eradRemainingTime = eradEventEndTime - DateTime.UtcNow;
            string eradFormattedTime = EradFormatTime(eradRemainingTime);
            eradcontainer.Add(new CuiElement
            {
                Name = eradTimeLeftElement,
                Parent = eradKillCount,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text =
                            $" <color=#32CD32>Time Left:<color=white> {EradFormatTime(eradRemainingTime)}</color></color> ",
                        FontSize = 15, Font = "permanentmarker.ttf", Align = TextAnchor.LowerCenter
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0.01", AnchorMax = "1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                }
            });

            // Display top 3 players' kill counts
            int playerRank = 1;
            float verticalPosition = 0.83f;
            foreach (var entry in playerData.OrderByDescending(pair => pair.Value.KillCount).Take(5))
            {
                string textColor = GetKillCountColor(entry.Value.KillCount);
                string playerName = covalence.Players.FindPlayerById(entry.Key)?.Name ?? "Unknown";
                playerName =
                    playerName.Length > 8 ? playerName.Substring(0, 8) : playerName; // Limit name to 8 characters
                string killCountText =
                    $"{playerRank}. {playerName}<color=#32CD32> - </color><color=white>{entry.Value.KillCount}</color>";
                if (entry.Key == player.UserIDString)
                {
                    killCountText += " <"; // Add the "<" symbol to the player's own entry
                }

                eradcontainer.Add(new CuiElement
                {
                    Name = eradTextElementName,
                    Parent = eradKillCount,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = killCountText, Color = textColor, FontSize = 15, Font = "permanentmarker.ttf",
                            Align = TextAnchor.UpperCenter
                        },
                        new CuiRectTransformComponent
                            { AnchorMin = $"0 {verticalPosition - 0.25f}", AnchorMax = $"1 {verticalPosition}" },
                        new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                    }
                });


                playerRank++;
                verticalPosition -= 0.135f;
            }

            // Call AddEradTimeLeftElement with just the player
            if (eradTimeLeftElement == null)
            {
                AddEradTimeLeftElement(player);
            }

            CuiHelper.AddUi(player, eradcontainer);
            playerUiContainers[player.userID] = eradcontainer;
        }

        private string eradTimeLeftElement = "EradTimeLeftText";

        private void AddEradTimeLeftElement(BasePlayer player)
        {
            TimeSpan eradRemainingTime = eradEventEndTime - DateTime.UtcNow;
            string eradFormattedTime = EradFormatTime(eradRemainingTime);
            if (string.IsNullOrEmpty(eradTimeLeftElement))
            {
                // The required name is not properly set, cannot add the time left element.
                return;
            }

            // First, destroy the existing time left element if it exists
            CuiHelper.DestroyUi(player, eradTimeLeftElement);

            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = eradTimeLeftElement,
                Parent = eradKillCount,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text =
                            $" <color=#32CD32>Time Left:<color=white> {EradFormatTime(eradRemainingTime)}</color></color> ",
                        FontSize = 15, Font = "permanentmarker.ttf", Align = TextAnchor.LowerCenter
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0.01", AnchorMax = "1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                }
            });


            CuiHelper.AddUi(player, container);
        }

        private string FormattedTime(int seconds)
        {
            TimeSpan eradTimeSpan = TimeSpan.FromSeconds(seconds);
            return $"{eradTimeSpan.Minutes:D2}:{eradTimeSpan.Seconds:D2}";
        }

        private string GetKillCountColor(int killCount)
        {
            int colorIndex = killCount / 5;
            colorIndex = Mathf.Clamp(colorIndex, 0, killCountColors.Count - 1);
            return killCountColors[colorIndex];
        }

        private void DestroyEradUI(BasePlayer player)
        {
            CuiElementContainer container;
            if (playerUiContainers.TryGetValue(player.userID, out container))
            {
                // Get the panel name to destroy the UI
                string eradKillCountPanel = "EradKillCountPanel";
                CuiHelper.DestroyUi(player, eradTimeLeftElement);
                CuiHelper.DestroyUi(player, eradKillCountPanel);
                playerUiContainers.Remove(player.userID);
            }
        }

        #endregion
    }
}