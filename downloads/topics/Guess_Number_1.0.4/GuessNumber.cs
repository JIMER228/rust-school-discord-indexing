using System;
using System.Collections.Generic;
using ConVar;
using Newtonsoft.Json;
using WebSocketSharp;

namespace Oxide.Plugins
{
    [Info("GuessNumber", "LAGZYA", "1.0.4")]
    public class GuessNumber : RustPlugin
    {
        #region Config
        
        public class ConfigData
        {
            [JsonProperty("Only command start event")] public bool OnTimer = false;
            
            [JsonProperty("How often will the event be launched(Seconds)")] public int restartEvent = 90;
            [JsonProperty("How many minimum people should there be to launch?")] public int MinPlayers = 3;
            [JsonProperty("Duration of the event(Seconds)")] public int timeEvent = 60;
            [JsonProperty("Notification of the start of the event")] public string textStart = "<color=orange>GIVEAWAY!</color> The event \"Guess the number\" begins, write it first in the chat.";


            [JsonProperty("Notification of who won")] public string textWon = "<color=orange>{0}</color> HAS WON GIVEAWAY WITH THE WINNING NUMBER {1}!\nTHE WINNER WILL RECEIVE <color=yellow>{2}</color>!";

            [JsonProperty("Minimum number")] public int Minimum = 1;
            [JsonProperty("Maximum number")] public int Maximum = 2500;
            
            [JsonProperty("Enable automatic rewards(true = yes")] public bool IsAutoRewards = true;
            [JsonProperty("Rewards")] public List<Reward> Rewards = new  List<Reward>();

            public class Reward
            {
                [JsonProperty("ShortName(Item)")]
                public string Shortname = "rifle.ak";
                [JsonProperty("Amount(Item)")]
                public int Amount = 1;
                [JsonProperty("Custom Name(Item)")]
                public string Name = "";
                [JsonProperty("SkinID(Item)")]
                public ulong SkinId = 0;
                [JsonProperty("Set true if you want to use the command as a reward")]
                public bool IsCommand = false;
                [JsonProperty("Display name(For message and GameTip)")]
                public string DisplayName = "Rifle - AK";
                [JsonProperty("Command(steamId={0}, playerName={1})")]
                public string Command = "";
            }
            public static ConfigData GetNewConf()
            {
                ConfigData newConfig = new ConfigData();
                newConfig.Rewards = new List<Reward>()
                {
                    new Reward(),
                    new Reward()
                    {
                        Shortname = "",
                        Amount = 0,
                        IsCommand = true,
                        DisplayName = "HOME RECYCLER",
                        Command = "giverecycler {0}"
                    }
                };
                return newConfig;
            }
        }
        protected override void LoadDefaultConfig() => cfg = ConfigData.GetNewConf();
        protected override void SaveConfig() => Config.WriteObject(cfg);

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                cfg = Config.ReadObject<ConfigData>();
            }
            catch
            {
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }
        private ConfigData cfg { get; set;}
        
        #endregion

        #region Var
        
        private int NumberEvent;
        private bool IsStart = false;
        private Timer CancelTimer;
        private Timer GameTip;
        
        #endregion

        #region Command

        [ChatCommand("numberstart")]
        private void EventCommand(BasePlayer player, string command, string[] args)
        {
            if(!permission.UserHasPermission(player.UserIDString, "guessnumber.admin")) return;
            var num1 = 0;
            var num2 = 0;
            if (args.Length < 2 || !int.TryParse(args[0], out num1) || !int.TryParse(args[1], out num2))
            {
                num1 = cfg.Minimum;
                num2 = cfg.Maximum;
            }
            
            if (IsStart)
            {
                SendReply(player, "Event already start!");
                return;
            }
            
            IsStart = true;
            NumberEvent = UnityEngine.Random.Range(num1, num2);
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                basePlayer.SendConsoleCommand("gametip.hidegametip");
                basePlayer.SendConsoleCommand("gametip.showgametip", cfg.textStart);
                SendReply(basePlayer, cfg.textStart);
            }
            GameTip?.Destroy();
            GameTip = timer.Once(10, () =>
            {
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    basePlayer.SendConsoleCommand("gametip.hidegametip");
                }
            });
            PrintWarning($"Start GuessNumber event Number({NumberEvent})");
            SendReply(player, "Start event!");
            CancelTimer = timer.Once(cfg.timeEvent, () =>
            {
                if (IsStart)
                {
                    IsStart = false;
                    PrintWarning("No one guessed the number, so the event will be started again later.");
                    foreach (var basePlayer in BasePlayer.activePlayerList)
                    {

                        SendReply(basePlayer,
                            "No one guessed the number, so the event will be started again later.");
                    }
                }
            });
        }

        #endregion
        
        #region Hooks
        
        private void OnServerInitialized(bool initial)
        {
            permission.RegisterPermission("guessnumber.admin", this);
            if(!cfg.OnTimer)
            {
                timer.Every(cfg.restartEvent, () =>
                {
                    if (BasePlayer.activePlayerList.Count >= cfg.MinPlayers)
                    {
                        if (IsStart == false)
                        {
                            IsStart = true;
                            NumberEvent = UnityEngine.Random.Range(cfg.Minimum, cfg.Maximum);
                            foreach (var basePlayer in BasePlayer.activePlayerList)
                            {
                                basePlayer.SendConsoleCommand("gametip.hidegametip");
                                basePlayer.SendConsoleCommand("gametip.showgametip", cfg.textStart);
                                SendReply(basePlayer, cfg.textStart);
                            }

                            GameTip?.Destroy();
                            GameTip = timer.Once(10, () =>
                            {
                                foreach (var basePlayer in BasePlayer.activePlayerList)
                                {
                                    basePlayer.SendConsoleCommand("gametip.hidegametip");
                                }
                            });
                            PrintWarning($"Start GuessNumber event Number({NumberEvent})");
                            CancelTimer = timer.Once(cfg.timeEvent, () =>
                            {
                                if (IsStart)
                                {
                                    IsStart = false;
                                    PrintWarning(
                                        "No one guessed the number, so the event will be started again later.");
                                    foreach (var basePlayer in BasePlayer.activePlayerList)
                                    {

                                        SendReply(basePlayer,
                                            "No one guessed the number, so the event will be started again later.");
                                    }
                                }
                            });
                        }
                    }
                });
            }
        }

        private void Unload()
        {
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                basePlayer.SendConsoleCommand("gametip.hidegametip");
            }
        }
        
        private void OnPlayerChat(BasePlayer player, string message, Chat.ChatChannel channel)
        {
            var number = 0;
            if (player == null|| !int.TryParse(message, out number) || channel != Chat.ChatChannel.Global || IsStart == false || CancelTimer == null) return;
            if(number != NumberEvent) return;
            var reward = FindRewards();
            var nameRewards = "";
            if (reward != null)
                nameRewards = reward.DisplayName;
            foreach (var basePlayer in BasePlayer.activePlayerList)
            {
                basePlayer.SendConsoleCommand("gametip.hidegametip");
                basePlayer.SendConsoleCommand("gametip.showgametip", string.Format(cfg.textWon, player.displayName.ToUpper(), NumberEvent, nameRewards));
                SendReply(basePlayer, string.Format(cfg.textWon, player.displayName.ToUpper(), NumberEvent, nameRewards));
            }

            GameTip?.Destroy();
            CancelTimer?.Destroy();
            CancelTimer = null;
            GameTip = timer.Once(10, () =>
            {
                foreach (var basePlayer in BasePlayer.activePlayerList)
                {
                    basePlayer.SendConsoleCommand("gametip.hidegametip");
                }
            });
            IsStart = false;
            NumberEvent = -1;
            LogToFile("LOGSWINS", $"Winner {player.displayName} [{player.userID}] [{DateTime.UtcNow}]", this);
            if(!cfg.IsAutoRewards) return;
            GiveReward(player, reward);
        }
        
        #endregion

        #region Mettods

        private ConfigData.Reward FindRewards()
        {
            return cfg.Rewards.GetRandom();
        }
        
        private void GiveReward(BasePlayer player, ConfigData.Reward reward)
        {
            if (reward.IsCommand)
            {
                rust.RunServerCommand(String.Format(reward.Command, player.userID, player.displayName));
            }
            else
            {
                var item = ItemManager.CreateByName(reward.Shortname, reward.Amount);
                item.skin = reward.SkinId;
                if (!reward.Name.IsNullOrEmpty())
                    item.name = reward.Name;
                if (!player.inventory.GiveItem(item))
                    item.Drop(player.GetDropPosition(), player.GetDropVelocity());
            }
        }

        #endregion
    }
}