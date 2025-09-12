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

//v1.0.1 Patched forced events saying nobody participated. Updated Start Event Sound. Updated Colors on player and kill count announcements.
//v1.0.1 Changed kills to bot kills
//v1.0.2 Fixed players with 0 kills after using /botkills from winning if they were the only ones with data.
//v1.0.3 Added true or false on scheduling random events.
//v1.0.4 Tie breaker wasnt breaking the tie, re added the check.
//v1.0.5 Event wasnt rescheduling after a natural end. Added checks to get rescheduled. Added on player connected hook as a tester. 
//v1.0.6 Removed an else function, Some endings announce nobody played. Will find a workaround and update soon.
//v1.0.7 Swapped around the else function I commented out, now the tie, single winner and no players seem to be announcing correctly.
//v1.0.8 Did some troubleshooting tests on server, never saw a non participation message, looks good now. 
//v1.0.9 On some we saw no participants on event wins, Removed that message. Also Removed some redundant code.
//v1.1.0 Added config option for start SFX.
//v1.1.1 Added message for manually ended events.
//v1.1.2 Changed second config option for null name to "", it was throwing errors in console at end due to incorrect format. Added more checks for UI kill.
//v1.1.2 Removed some debugging.
//v1.1.3 Added config option to disable or enable loot table, Added Economics Support, reworked winner announcement.
//v1.1.4 Added ServerRewards Support. Can use either or together if needed.
//v1.1.5 Added a Check and Catch if SR or Eco is not loaded. If not it will proceed with event end as needed.
//v1.1.6 Cleaned up Timer for Auto sched. and Duration Removed some unused code.
//v1.1.7 Tweaked the way the plugin detects if an event is able to be Scheduled depending on active players. 
//v1.1.8 Added a Min Max Item Randomizer for the Loot Table.
//v1.1.9 Corrected a line if config option was null.
//v1.2.0 Leaderboard UI - Ridamees
//v1.2.1 Added Event start and end hooks, for use with HUD.
//v1.2.2 Changed Time Formatting to Display M / S Remaining.
//v1.2.3 Corrected a check if enough players are on for scheduled events.
//v1.2.4 Added a check, if the threshold for players needed for events, fell below during an event, it wouldn't schedule the next after that event ended, until threshold is reached again.
//v1.2.5 Added new config options for commands and event messaging / toggle, ui positioning, also redid the entire lang for the plugin, now it can be customized as needed, cleaned up and reorganized code.
//v1.2.51 Fixed Tie Message Key
//v1.2.6 Worked out every outcome if loot table, SR, or Eco are being used in diff combos, to print the correct output to chat of rewards given.
//v1.2.7 Added option to run commands for the winner, added checks for pvp kills.
//v1.2.8 Rewards Dist. Section cleaned up.

#endregion


namespace Oxide.Plugins

{
    [Info("Bot Purge Event", "Wrecks", "1.2.8")]
    [Description("Bot Killing event featuring a Leaderboard UI.")]
    class BotPurgeEvent : RustPlugin

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
        private bool isEventRunning;
        private bool eventStarted;
        private Dictionary<string, PlayerData> playerData = new Dictionary<string, PlayerData>();
        private Timer botPurgeTimer;
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
        private string startEventSound = "assets/bundled/prefabs/fx/headshot.prefab";
        private bool enableStartSfx;
        private string winnerSoundEffect = "assets/prefabs/misc/xmas/presents/effects/unwrap.prefab";
        private bool manuallyEnded;
        private bool isEventSchedulingEnabled;
        private bool chatCountdown;
        private Timer endEventTimer;
        private Timer eventScheduler;
        private List<string> participatingPlayers = new List<string>();

        private Dictionary<ulong, CuiElementContainer>
            playerUiContainers = new Dictionary<ulong, CuiElementContainer>();

        private string purgeStartCommand;
        private string purgeEndCommand;
        private const string StartPermission = "botpurgeevent.start";
        private const string EndPermission = "botpurgeevent.end";
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
            string botPurgeStartCommand = Config.Get<string>("BotPurgeStartCommand", "purge");
            AddCovalenceCommand(purgeStartCommand, nameof(CmdStartBotPurgeEvent), StartPermission);
            string botPurgeEndCommand = Config.Get<string>("BotPurgeEndCommand", "purgeEnd");
            AddCovalenceCommand(purgeEndCommand, nameof(CmdEndBotPurgeEvent), EndPermission);
            enableStartSfx = Config.Get<bool>("EnableStartSFX");
            isEventSchedulingEnabled = Config.Get<bool>("EnableScheduledEvents");
            chatCountdown = Config.Get<bool>("ChatCountdownEnabled");
            EnableWinnerCommands = Config.Get<bool>("EnableWinnerCommands");
        }

        private void OnServerInitialized()
        {
            if (CanScheduleNextBotPurge())
            {
                if (isEventSchedulingEnabled && !eventStarted)
                {
                    ScheduleNextBotPurge();
                }
                else if (eventStarted && isEventSchedulingEnabled)
                {
                    double timeRemaining = (scheduledTime - Time.realtimeSinceStartup);
                    double minutesRemaining = Math.Ceiling(timeRemaining / 60);
                    Puts($"The next Bot Purge Event will start in {minutesRemaining} minute(s).");
                }
            }
            else
            {
                Puts("Not enough players to schedule the Bot Purge Event.");
            }
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo hitInfo)
        {
            if (!eventStarted || player == null)
                return;

            if (hitInfo == null || !(hitInfo.Initiator is BasePlayer))
                return;

            BasePlayer attacker = hitInfo.InitiatorPlayer;
            if (attacker == null)
                return;


            if (!(attacker is BasePlayer) || attacker.net == null || attacker.net.connection == null)
                return;


            string attackerID = attacker.UserIDString;
            PlayerData attackerData = GetPlayerData(attackerID);
            if (player.UserIDString == attackerID)
                return;
            attackerData.KillCount++;

            int killCount = attackerData.KillCount;
            if (killCount > highestKillCount)
            {
                highestKillCount = killCount;
                winnerSteamID = attackerID;
            }

            if (!participatingPlayers.Contains(attackerID))
            {
                participatingPlayers.Add(attackerID);
            }

            if (eventStarted)
            {
                // ShowKillCountUI for the attacker
                ShowKillCountUI(attacker, killCount);

                // ShowKillCountUI for all other participating players
                foreach (string participantID in participatingPlayers)
                {
                    if (participantID != attackerID)
                    {
                        BasePlayer participant = covalence.Players.FindPlayerById(participantID)?.Object as BasePlayer;
                        if (participant != null)
                        {
                            ShowKillCountUI(participant, GetPlayerData(participantID).KillCount);
                        }
                    }
                }
            }
        }

        void OnPlayerConnected(BasePlayer player)
        {
            // Check if there are enough players online and schedule the next event if needed
            if (!isEventRunning && isEventSchedulingEnabled && CanScheduleNextBotPurge())
            {
                ScheduleNextBotPurge();
            }
        }

        //Triggers KillTimer if not enough players online.
        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            timer.Once(1f, () =>
            {
                if (BasePlayer.activePlayerList.Count < minPlayersToStart && nextEventTimer != null &&
                    !nextEventTimer.Destroyed)
                {
                    CancelScheduledEvent();
                }
            });
        }

        void Unload()
        {
            if (eventStarted)
            {
                EndBotPurgeEvent();
            }

            if (eventScheduler != null)
            {
                eventScheduler.Destroy();
                eventScheduler = null;
            }

            if (botPurgeTimer != null)
            {
                botPurgeTimer.Destroy();
                botPurgeTimer = null;
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyKillCountUI(player);
            }
        }

        #endregion

        #region config

        // RectTransform = { AnchorMin = $"0.5 1", AnchorMax = $"0.5 1", OffsetMin = $"{250f} {-150f}", OffsetMax = $"{450f} {0f}" },
        protected override void LoadDefaultConfig()
        {
            Config["BotPurgeStartCommand"] = "purge";
            Config["BotPurgeEndCommand"] = "purgeend";
            Config["LeaderboardUIAnchorMin"] = "0.5 1";
            Config["LeaderboardUIAnchorMax"] = "0.5 1";
            Config["LeaderboardUIOffsetMin"] = "250 -150";
            Config["LeaderboardUIOffsetMax"] = "450 0";
            Config["EnableEconomicRewards"] = true;
            Config["EnableServerRewards"] = true;
            Config["EnableStartSFX"] = true;
            Config["EnableScheduledEvents"] = true;
            Config["ChatCountdownEnabled"] = true;
            Config["ChatCountdownInterval(Minutes)"] = 1;
            Config["Minimum Players To Start"] = 2;
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
                    { "name", "Blood Money" }
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
                purgeStartCommand = GetConfigValue("BotPurgeStartCommand", "purge");
                purgeEndCommand = GetConfigValue("BotPurgeEndCommand", "purgeend");
                Config["LeaderboardUIAnchorMin"] = GetConfigValue("LeaderboardUIAnchorMin", "0.5 1");
                Config["LeaderboardUIAnchorMax"] = GetConfigValue("LeaderboardUIAnchorMax", "0.5 1");
                Config["LeaderboardUIOffsetMin"] = GetConfigValue("LeaderboardUIOffsetMin", "250 -150");
                Config["LeaderboardUIOffsetMax"] = GetConfigValue("LeaderboardUIOffsetMax", "450 0");
                Config["EnableServerRewards"] = GetConfigValue("EnableServerRewards", false);
                Config["EnableEconomicRewards"] = GetConfigValue("EnableEconomicRewards", false);
                Config["EnableStartSFX"] = GetConfigValue("EnableStartSFX", true);
                Config["EnableScheduledEvents"] = GetConfigValue("EnableScheduledEvents", true);
                Config["ChatCountdownEnabled"] = GetConfigValue("ChatCountdownEnabled", true);
                Config["ChatCountdownInterval(Minutes)"] = GetConfigValue("ChatCountdownInterval(Minutes)", 1);
                minPlayersToStart = GetConfigValue("Minimum Players To Start", 2);
                randomEventMin = GetConfigValue("MinimumTimeBetweenEvents(Seconds)", 3600);
                randomEventMax = GetConfigValue("MaximumTimeBetweenEvents(Seconds)", 7200);
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
                        { "name", "Blood Money" }
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
                    "EventStarted",
                    "[<color=#880808>BOT PURGE EVENT</color>] The Bot Purge Event has started! <color=#884808>Kill</color> as many bots as you can!" //used
                },
                {
                    "EventAlreadyRunning",
                    "[<color=#880808>BOT PURGE EVENT</color>] The Event is already running."
                }, // used
                {
                    "EventNotRunning",
                    "[<color=#880808>BOT PURGE EVENT</color>] The Event is not currently running."
                }, // used
                {
                    "NoPermissionStart", //used
                    "[<color=#880808>BOT PURGE EVENT</color>] You don't have permission to start the Bot Purge Event."
                },
                {
                    "NoPermissionEnd", //used
                    "[<color=#880808>BOT PURGE EVENT</color>] You don't have permission to end the Bot Purge Event."
                },
                {
                    "ManuallyEnded",
                    "[<color=#880808>BOT PURGE EVENT</color>] The Event was manually Ended."
                },
                {
                    "NoParticipants",
                    "[<color=#880808>BOT PURGE EVENT</color>] The Event has ended! Unfortunately, no one participated."
                },
                {
                    "CountdownText",
                    "[<color=#880808>BOT PURGE EVENT</color>] The Bot Purge Event will end in <color=#880808>{0}</color> minute(s)."
                },
                {
                    "TieMessage",
                    "The Bot Purge Event has ended! There was a tie among <color=#880808>{0}</color> players with <color=#880808>{1}</color> kills."
                },
                {
                    "TieWinnerMessage",
                    "The winner was randomly selected: <color=#880808>{0}</color> with <color=#880808>{1}</color> kills!"
                },
                {
                    "WinnerAnnounce",
                    "[<color=#880808>BOT PURGE EVENT</color>] {0}"
                },
                {
                    "HasEnded",
                    "The Bot Purge Event Has Ended!"
                },
                {
                    "EndMessage",
                    "\n\nThe Bot Purge Event has ended!\n\nThe winner is <color=#880888>{0}</color> with <color=#884808>{1}</color> kill(s)!"
                },
                {
                    "RewardMessage",
                    "\n<color=#880848>{0}</color> <color=#888808>x</color> <color=#088848>{1}</color>"
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
                }
            };

            lang.RegisterMessages(messages, this);
        }

        #endregion

        #region helpers

        void CheckPluginLoaded(string pluginName)
        {
            Plugin plugin = plugins.Find(pluginName);

            if (plugin == null)
            {
                Puts($"The plugin {pluginName} is not loaded!");
            }
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

        private double nextEventTime;

        private Timer nextEventTimer;

        private void ScheduleNextBotPurge()
        {
            // Check if there's an existing timer and return if so
            if (nextEventTimer != null && !nextEventTimer.Destroyed)
            {
                // Puts("An event is already scheduled."); //Debug
                return;
            }

            if (!isEventSchedulingEnabled)
            {
                Puts("Scheduled events are disabled in the config.");
                return;
            }


            if (!CanScheduleNextBotPurge())
            {
                Puts("Not enough players to schedule the event.");
                return;
            }

            int randomSeconds = UnityEngine.Random.Range(randomEventMin, randomEventMax + 1);

            nextEventTime = GrabCurrentTime() + randomSeconds;
            Puts($"The next Bot Purge Event will start in {FormatTimeSpan(randomSeconds)}.");

            nextEventTimer = timer.Once(randomSeconds, StartBotPurgeEvent);
        }

        private bool CanScheduleNextBotPurge()
        {
            bool canStart = BasePlayer.activePlayerList.Count >= minPlayersToStart;
            return canStart;
        }

        // Method above triggers the KillTimer
        private void CancelScheduledEvent()
        {
            if (nextEventTimer != null)
            {
                //Puts("nextEventTimer is not null");
                if (!nextEventTimer.Destroyed)
                {
                    // Puts("nextEventTimer is not destroyed, attempting to destroy");
                    nextEventTimer.Destroy();
                    nextEventTimer = null;
                }
                else
                {
                    //  Puts("nextEventTimer is already destroyed");
                }
            }
            else
            {
                // Puts("nextEventTimer is null");
            }

            Puts("Bot Purge Event has been canceled due to insufficient players.");
        }

        private double GrabCurrentTime()
        {
            return DateTime.UtcNow.Subtract(new DateTime(1970, 1, 1)).TotalSeconds;
        }

        private DateTime eventEndTime;

        // Property to calculate and return minutesLeft
        private int MinutesLeft
        {
            get
            {
                TimeSpan remainingTime = eventEndTime - DateTime.UtcNow;
                int secondsRemaining = Mathf.Max(0, (int)remainingTime.TotalSeconds);
                return Mathf.CeilToInt((float)secondsRemaining / 60f);
            }
        }

        private string FormatTime(TimeSpan timeSpan)
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

        #endregion

        #region ui

        private string panelName = "KillCountPanel";

        private void ShowKillCountUI(BasePlayer player, int playerKillCount)
        {
            DestroyKillCountUI(player);

            CuiElementContainer container = new CuiElementContainer();

            // Customize the position and appearance of the UI elements as per your preference
            string textElementName = "KillCountText";
            string temptextElementName = "TempText";
            container.Add(new CuiPanel
            {
                Image = { Color = "35 35 35 0.17" },
                RectTransform =
                {
                    AnchorMin = GetConfigValue("LeaderboardUIAnchorMin", "0.5 1"),
                    AnchorMax = GetConfigValue("LeaderboardUIAnchorMax", "0.5 1"),
                    OffsetMin = GetConfigValue("LeaderboardUIOffsetMin", "250 -150"),
                    OffsetMax = GetConfigValue("LeaderboardUIOffsetMax", "450 0")
                },
            }, "Hud", panelName);

            container.Add(new CuiElement
            {
                Name = textElementName,
                Parent = panelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = " <color=#880808>BOT </color><color=#880808>PURGE EVENT</color> ", FontSize = 17,
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
                // If there are 3 or fewer entries, add the temptextElementName
                container.Add(new CuiElement
                {
                    Name = temptextElementName,
                    Parent = panelName,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = "<color=#884808>Kill the most bots!</color>", FontSize = 16,
                            Font = "permanentmarker.ttf", Align = TextAnchor.UpperCenter
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 0.3" },
                        new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                    }
                });
            }

            TimeSpan remainingTime = eventEndTime - DateTime.UtcNow;
            string formattedTime = FormatTime(remainingTime);
            container.Add(new CuiElement
            {
                Name = timeLeftElementName,
                Parent = panelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $" <color=#880808>Time Left:<color=white> {FormatTime(remainingTime)}</color></color> ",
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
                    $"{playerRank}. {playerName}<color=#880808> - </color><color=white>{entry.Value.KillCount}</color>";
                if (entry.Key == player.UserIDString)
                {
                    killCountText += " <"; // Add the "<" symbol to the player's own entry
                }

                container.Add(new CuiElement
                {
                    Name = textElementName,
                    Parent = panelName,
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

            // Call AddTimeLeftElement with just the player
            if (timeLeftElementName == null)
            {
                AddTimeLeftElement(player);
            }

            CuiHelper.AddUi(player, container);
            playerUiContainers[player.userID] = container;
        }

        private string timeLeftElementName = "TimeLeftText";

        private void AddTimeLeftElement(BasePlayer player)
        {
            TimeSpan remainingTime = eventEndTime - DateTime.UtcNow;
            string formattedTime = FormatTime(remainingTime);
            if (string.IsNullOrEmpty(timeLeftElementName))
            {
                // The required name is not properly set, cannot add the time left element.
                return;
            }

            // First, destroy the existing time left element if it exists
            CuiHelper.DestroyUi(player, timeLeftElementName);

            CuiElementContainer container = new CuiElementContainer();
            container.Add(new CuiElement
            {
                Name = timeLeftElementName,
                Parent = panelName,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = $" <color=#880808>Time Left:<color=white> {FormatTime(remainingTime)}</color></color> ",
                        FontSize = 15, Font = "permanentmarker.ttf", Align = TextAnchor.LowerCenter
                    },
                    new CuiRectTransformComponent { AnchorMin = "0 0.01", AnchorMax = "1 1" },
                    new CuiOutlineComponent { Color = "0 0 0 0.4", Distance = "0.25 0.25", UseGraphicAlpha = true }
                }
            });
            CuiHelper.AddUi(player, container);
        }

        private string GetFormattedTime(int seconds)
        {
            TimeSpan timeSpan = TimeSpan.FromSeconds(seconds);
            return $"{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
        }

        private string GetKillCountColor(int killCount)
        {
            int colorIndex = killCount / 5;
            colorIndex = Mathf.Clamp(colorIndex, 0, killCountColors.Count - 1);
            return killCountColors[colorIndex];
        }

        private void DestroyKillCountUI(BasePlayer player)
        {
            CuiElementContainer container;
            if (playerUiContainers.TryGetValue(player.userID, out container))
            {
                // Get the panel name to destroy the UI
                string panelName = "KillCountPanel";

                CuiHelper.DestroyUi(player, timeLeftElementName);
                CuiHelper.DestroyUi(player, panelName);
                playerUiContainers.Remove(player.userID);
            }
        }

        #endregion

        #region eventmethods

        private void StartBotPurgeEvent()
        {
            eventStarted = true;
            Interface.CallHook("OnBotPurgeEventStart"); //Start Event Hook
            rewardsGiven = false;
            playerData.Clear();
            highestKillCount = 0;
            winnerSteamID = null;

            //Generate fake player data for leaderboard testing
            //for (int i = 0; i < 3; i++)
            //{
            //    ulong fakePlayerID = 76543210 + (ulong)i;
            //    int fakeKillCount = UnityEngine.Random.Range(1, 15);
            //    
            //    // Create a new PlayerData instance and add it to the playerData dictionary
            //    playerData[fakePlayerID.ToString()] = new PlayerData { KillCount = fakeKillCount };
            //}

            if (botPurgeTimer != null)
            {
                botPurgeTimer.Destroy();
                botPurgeTimer = null;
            }

            int eventDuration = GetConfigValue("EventDuration(Seconds)", 600);
            eventEndTime = DateTime.UtcNow.AddSeconds(eventDuration); // Set eventEndTime
            Server.Broadcast(lang.GetMessage("EventStarted", this));
            if (enableStartSfx)
            {
                //SFX
                foreach (var player in BasePlayer.activePlayerList)
                {
                    EffectNetwork.Send(new Effect(startEventSound, player.transform.position, Vector3.zero));
                }
            }

            botPurgeTimer = timer.Every(1f, () =>
            {
                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiElementContainer container;
                    if (playerUiContainers.TryGetValue(player.userID, out container))
                    {
                        AddTimeLeftElement(player);
                    }
                }
            });
            int countdownIntervalMinutes = GetConfigValue("ChatCountdownInterval(Minutes)", 1);
            botPurgeTimer = timer.Every(countdownIntervalMinutes * 60f, () =>
            {
                int minutesLeft = MinutesLeft;

                if (minutesLeft == 0)
                {
                    isEventRunning = false;
                    EndBotPurgeEvent();
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

        private void OnEventStarted(DateTime eventEndTime)
        {
            Puts(messages["EventStarted"]);
            Server.Broadcast(lang.GetMessage("EventStarted", this));

            foreach (var player in BasePlayer.activePlayerList)
            {
                var playerData = GetPlayerData(player.UserIDString);
                playerData.KillCount = 0;
            }

            botPurgeTimer = timer.Every(1f, () =>
            {
                TimeSpan remainingTime = eventEndTime - DateTime.UtcNow;
                int secondsRemaining = Mathf.Max(0, (int)remainingTime.TotalSeconds);
                if (secondsRemaining == 0)
                {
                    Server.Broadcast(lang.GetMessage("HasEnded", this));
                    isEventRunning = false;
                    EndBotPurgeEvent();
                    return;
                }
            });
        }

        private void CmdStartBotPurgeEvent(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(StartPermission))
            {
                if (eventStarted)
                {
                    player.Message(lang.GetMessage("EventAlreadyRunning", this));
                    return;
                }

                if (eventScheduler != null)
                {
                    eventScheduler.Destroy();
                    eventScheduler = null;
                }

                Puts("EventStarted");
                StartBotPurgeEvent();
            }
            else
            {
                player.Message(lang.GetMessage("NoPermissionStart", this));
            }
        }

        private void CmdEndBotPurgeEvent(IPlayer player, string command, string[] args)
        {
            if (player.HasPermission(EndPermission))
            {
                if (eventStarted)
                {
                    manuallyEnded = true;
                    Server.Broadcast(lang.GetMessage("ManuallyEnded", this));
                    EndBotPurgeEvent();
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

        private void EndBotPurgeEvent()
        {
            if (botPurgeTimer != null)
            {
                botPurgeTimer.Destroy();
                botPurgeTimer = null;
            }

            eventStarted = false;
            isEventRunning = false;

            // Destroy the Kill Count UI for all connected players
            foreach (var playerID in participatingPlayers)
            {
                BasePlayer player = BasePlayer.FindByID(Convert.ToUInt64(playerID));
                if (player != null)
                {
                    DestroyKillCountUI(player);
                }
            }

            bool anyParticipantWithKills = false;
            List<string> winners = new List<string>();
            int highestKillCount = 0;

            foreach (var playerID in participatingPlayers)
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
                            GiveWinnerRewards(randomWinnerSteamID,
                                highestKillCount); // Pass the randomly selected winner SteamID and the highestKillCount
                        string winnerMessage = lang.GetMessage("TieWinnerMessage", this);
                        string formattedWinnerMessage = string.Format(winnerMessage, winnerName, highestKillCount);
                        winnerMessageBuilder.AppendLine(formattedWinnerMessage);
                        winnerMessageBuilder.AppendLine(rewardMessage);
                        string winnerAnnounce = lang.GetMessage("WinnerAnnounce", this);
                        string formattedwinnerAnnounce = string.Format(winnerAnnounce, winnerMessageBuilder.ToString());
                        Server.Broadcast(formattedwinnerAnnounce);
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
                        GiveWinnerRewards(winnerID,
                            highestKillCount);
                    winnerMessageBuilder.AppendLine(rewardMessage);
                    string winnerAnnounce = lang.GetMessage("WinnerAnnounce", this);
                    string formattedwinnerAnnounce = string.Format(winnerAnnounce, winnerMessageBuilder.ToString());
                    Server.Broadcast(formattedwinnerAnnounce);
                }
            }
            else
            {
                // No participants with kills
                Server.Broadcast(lang.GetMessage("NoParticipants", this));
            }

            participatingPlayers.Clear();
            playerData.Clear();

            foreach (var player in BasePlayer.activePlayerList)
            {
                DestroyKillCountUI(player);
            }

            ScheduleNextBotPurge();
            Interface.CallHook("OnBotPurgeEventEnd"); //End Event Hook
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
                return null; // No item rewards to give
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

        #endregion
    }
}