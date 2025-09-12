using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Simple Events", "Orange", "1.0.22")]
    [Description("https://rustworkshop.space/")]
    public class SimpleEvents : RustPlugin
    {
        #region Vars
        
        private EventController script;
        private static SimpleEvents plugin;
        private const float refreshRate = 1f;
        private const int textSize = 14;
        private const string fontRegular = "RobotoCondensed-Regular.ttf";
        private const string fontBold = "RobotoCondensed-Bold.ttf";
        private const int vehicleCooldown = 30;
        private const int eventStartInterval = 300;
        
        private static HashSet<uint> looted = new HashSet<uint>();
        private static HashSet<PlayerData> scoreList = new HashSet<PlayerData>();
        private static PlayerData[] orderedScore = {};
        private static Dictionary<ulong, DateTime> recentlyWasInCopter = new Dictionary<ulong, DateTime>();
        
        private static EventDefinition def;
        private static string textLeft;
        private static string textRight;
        private static CuiElementContainer container;
        private static CuiElementContainer buttonShow;
        private static CuiElementContainer buttonHide;
        private static int timeLeft;
        private const string elemMain = "simplevents.main";
        private const string elemButton = "simpleevents.button";
        private static HashSet<ulong> disabledUI = new HashSet<ulong>();
        private static int lastEventNum;
        private static float lastEventTime;
        
        private static string type => def?.type ?? "Default";
        private static RewardDefinition[] rewards => def?.rewards ?? new RewardDefinition[] { };
        private static string displayName => def?.displayName ?? "Unknown";
        private static string description => def?.description ?? "Unknown";
        private static string[] targets => def?.targets ?? new string[] { };
        private static PluginTimers Timer => plugin.timer;

        #endregion
        
        #region Oxide Hooks
        
        private void Init()
        {
            plugin = this;
            cmd.AddConsoleCommand("simpleevents.start", this, nameof(cmdControlConsole));
            cmd.AddConsoleCommand("simpleevents.stop", this, nameof(cmdControlConsole));
            cmd.AddConsoleCommand("simpleevents.ui.toggle", this, nameof(cmdControlConsole));
            UnsubscribeFromHooks();
            buttonShow = new CuiElementContainer {{GetButton(false), "Hud", elemButton}};
            buttonHide = new CuiElementContainer {{GetButton(true), "Hud", elemButton}};
        }

        private void OnServerInitialized()
        {
            timer.Once(3f, () =>
            {
                script = new GameObject().AddComponent<EventController>();
            });
        }

        private void Unload()
        {
            plugin = null;
            UnityEngine.Object.Destroy(script);
        }

        private void OnCollectiblePickup(Item item, BasePlayer player)
        {
            AddPoints(player, item.info.shortname, item.amount);
        }
        
        private void OnCropGather(GrowableEntity  plant, Item item, BasePlayer player)
        {
            AddPoints(player, item.info.shortname, item.amount);
        }
        
        private void OnDispenserGather(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            AddPoints(player, item.info.shortname, item.amount);
        }
        
        private void OnDispenserBonus(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            AddPoints(player, item.info.shortname, item.amount);
        }

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            var player = info?.InitiatorPlayer;
            if (player == null || player.userID.IsSteamId() == false)
            {
                return;
            }
            
            AddPoints(player, entity.ShortPrefabName, 1);
        }
        
        private void OnLootEntity(BasePlayer player, LootContainer entity)
        {
            if (player.userID.IsSteamId() == false)
            {
                return;
            }

            if (looted.Contains(entity.net.ID) == false)
            {
                looted.Add(entity.net.ID);
                AddPoints(player, entity.ShortPrefabName, 1);
            }
        }
        
        private void OnEntityMounted(BaseMountable entity, BasePlayer player)
        {
            OnEntityDismounted(entity, player);
        }
        
        private void OnEntityDismounted(BaseMountable entity, BasePlayer player)
        {
            if (recentlyWasInCopter.ContainsKey(player.userID))
            {
                recentlyWasInCopter[player.userID] = DateTime.Now;
            }
            else
            {
                recentlyWasInCopter.Add(player.userID, DateTime.Now);
            }
        }

        #endregion

        #region Commands

        private void cmdControlConsole(ConsoleSystem.Arg arg)
        {
            var command = arg.cmd?.FullName ?? "null";
            switch (command)
            {
                case "simpleevents.start":
                    if (arg.IsAdmin == false)
                    {
                        SendReply(arg, "No Permission");
                        return;
                    }

                    if (script == null)
                    {
                        SendReply(arg, "Script is not loaded yet!");
                        return;
                    }
                    
                    script.StartRandomEvent();
                    break;
                
                case "simpleevents.stop":
                    if (arg.IsAdmin == false)
                    {
                        SendReply(arg, "No Permission");
                        return;
                    }
                    
                    if (script == null)
                    {
                        SendReply(arg, "Script is not loaded yet!");
                        return;
                    }
                    
                    script.StopEvent();
                    break;
                
                case "simpleevents.ui.toggle":
                    var player = arg.Player();
                    if (player != null)
                    {
                        if (disabledUI.Contains(player.userID))
                        {
                            disabledUI.Remove(player.userID);
                        }
                        else
                        {
                            disabledUI.Add(player.userID);
                            CuiHelper.DestroyUi(player, elemMain);
                        }
                    }
                    break;
                
                default:
                    SendReply(arg, $"Unknown command '{command}'");
                    break;
            }
        }

        #endregion

        #region Core

        private void SubscribeToHooks()
        {
            UnsubscribeFromHooks();
            
            switch (type)
            {
                case "Kill":
                    Subscribe(nameof(OnEntityDeath));
                    break;
                    
                case "Gather":
                    Subscribe(nameof(OnCropGather));
                    Subscribe(nameof(OnCollectiblePickup));
                    Subscribe(nameof(OnDispenserGather));
                    Subscribe(nameof(OnDispenserBonus));
                    break;
                    
                case "Loot":
                    Subscribe(nameof(OnLootEntity));
                    break;

                case "KingOfTheHill":
                    Subscribe(nameof(OnEntityDismounted));
                    Subscribe(nameof(OnEntityMounted));
                    break;
            }
        }

        private void UnsubscribeFromHooks()
        {
            Unsubscribe(nameof(OnEntityDeath));
            Unsubscribe(nameof(OnCropGather));
            Unsubscribe(nameof(OnCollectiblePickup));
            Unsubscribe(nameof(OnDispenserGather));
            Unsubscribe(nameof(OnDispenserBonus));
            Unsubscribe(nameof(OnLootEntity));
            Unsubscribe(nameof(OnEntityDismounted));
            Unsubscribe(nameof(OnEntityMounted));
        }
        
        private static void AddPoints(BasePlayer player, string prefab, int points)
        {
            if (targets.Contains(prefab) == false)
            {
                return;
            }

            var data = scoreList.FirstOrDefault(x => x.userID == player.userID);
            if (data == null)
            {
                data = new PlayerData(player);
                scoreList.Add(data);
            }
                
            data.score += points;
        }
        
        private static void RewardWinners()
        {
            orderedScore = scoreList.OrderByDescending(x => x.score).ToArray();

            foreach (var reward in rewards)
            {
                var item = reward.item;
                    
                foreach (var num in reward.places)
                {
                    var place = num - 1; 
                        
                    if (orderedScore.Length > place)
                    {
                        var playerInfo = orderedScore[place];
                        var player = playerInfo.player;

                        if (player != null)
                        {
                            item.GiveTo(player);
                        }
                        else
                        {
                            item.GiveTo(playerInfo.userID.ToString());
                        }
                    }
                }
            }

            var text = string.Empty;
            
            for (var i = 0; i < 3; i++)
            {
                var num1 = "Unknown";
                var num2 = 0;

                if (orderedScore.Length > i)
                {
                    var value = orderedScore[i];
                    num1 = value.playerName;
                    num2 = value.score;

                    if (num1.Length > 10)
                    {
                        num1 = num1.Substring(0, 10);
                    }
                }

                text += $"{i + 1}. {num1} ({num2})\n";
            }
            
            plugin.Broadcast(Message.EventEnded, displayName, text);
        }
        
        private static void UpdateText()
        {
            timeLeft--;
            var time = $"{timeLeft/60:00}:{timeLeft%60:00}";
            textLeft = string.Empty;
            textRight = string.Empty;
                
            orderedScore = scoreList.OrderByDescending(x => x.score).ToArray();

            for (var i = 0; i < 3; i++)
            {
                var num1 = "Unknown";
                var num2 = 0;

                if (orderedScore.Length > i)
                {
                    var value = orderedScore[i];
                    num1 = value.playerName;
                    num2 = value.score;

                    if (num1.Length > 10)
                    {
                        num1 = num1.Substring(0, 10);
                    }
                }

                textLeft += $"{i + 1}. {num1}\n";
                textRight += $"{num2}\n";
            }

            textLeft = plugin.GetMessage(Message.UILeft1, null, textLeft);
            textRight = plugin.GetMessage(Message.UIRight, null, time, textRight);
        }

        private static void UpdatePlayers()
        {
            if (type == "KingOfTheHill")
            {
                scoreList.Clear();
                
                foreach (var player in BasePlayer.activePlayerList)
                {
                    var value = Convert.ToInt32(player.transform.position.y);
                    
                    if (player.isMounted == true || value >= 400)
                    {
                        value = 0;
                    }
                    else
                    {
                        var lastVehicleUse = new DateTime();
                        if (recentlyWasInCopter.TryGetValue(player.userID, out lastVehicleUse) == true)
                        {
                            var passed = (DateTime.Now - lastVehicleUse).TotalSeconds;
                            if (passed < vehicleCooldown)
                            {
                                value = 0;
                            }
                        }
                    }

                    var info = new PlayerData(player)
                    {
                        score = value
                    };
                    
                    scoreList.Add(info);
                }

                return;
            }
            
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (scoreList.Any(x => x.userID == player.userID) == false)
                {
                    scoreList.Add(new PlayerData(player));
                }
            }
        }
        
        private static void BuildUI()
        {
            container = new CuiElementContainer
            {
                new CuiElement
                {
                    Name = elemMain,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0 0 0 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0.5",
                            AnchorMax = "1 0.5"
                        }
                    }
                },
                new CuiElement
                {
                    Name = elemMain + ".panel",
                    Parent = elemMain,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = "0.2 0.2 0.2 0"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 0.5",
                            AnchorMax = "1 0.5",
                            OffsetMax = "-10 50",
                            OffsetMin = "-200 -50"
                        }
                    }
                },
                new CuiElement
                {
                    Parent = elemMain + ".panel",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Align = TextAnchor.UpperLeft,
                            Text = plugin.GetMessage(Message.UIHeader, null, displayName),
                            FontSize = textSize,
                            Font = fontBold
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        }
                    }
                },
                new CuiElement
                {
                    Parent = elemMain + ".panel",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Align = TextAnchor.MiddleLeft,
                            Text = textLeft,
                            FontSize = textSize,
                            Font = fontRegular
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        }
                    }
                },
                new CuiElement
                {
                    Parent = elemMain + ".panel",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Align = TextAnchor.UpperRight,
                            Text = textRight,
                            FontSize = textSize,
                            Font = fontBold
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                        }
                    }
                }
            };
        }

        private static void ShowUI(BasePlayer player, string personalText)
        {
            if (disabledUI.Contains(player.userID))
            {
                CuiHelper.DestroyUi(player, elemMain);
                CuiHelper.DestroyUi(player, elemButton);
                CuiHelper.AddUi(player, buttonShow);
                return;
            }
            
            var personal = new CuiElementContainer();
            personal.AddRange(container);
            personal.Add(new CuiElement
            {
                Parent = elemMain + ".panel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Align = TextAnchor.LowerCenter,
                        Text = personalText,
                        FontSize = textSize + 1,
                        Font = fontRegular
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.1",
                        AnchorMax = "1 1",
                    }
                }
            });

            CuiHelper.DestroyUi(player, elemMain);
            CuiHelper.AddUi(player, personal);
            CuiHelper.DestroyUi(player, elemButton);
            CuiHelper.AddUi(player, buttonHide);
        }

        private static void SetDefaultValues()
        {
            looted.Clear();
            scoreList.Clear();
            orderedScore = new PlayerData[]{};
            timeLeft = 0;
            recentlyWasInCopter.Clear();
        }

        private static CuiButton GetButton(bool hide)
        {
            return new CuiButton
            {
                Text =
                {
                    Text = hide ? ">" : "<",
                    FontSize = 15,
                    Color = "1 1 1 1",
                    Align = TextAnchor.MiddleCenter,
                    
                },
                Button =
                {
                    Command = "simpleevents.ui.toggle",
                    Color = "1 1 1 0.4",
                },
                RectTransform =
                {
                    AnchorMin = "1 0.5",
                    AnchorMax = "1 0.5",
                    OffsetMin = "-25 60",
                    OffsetMax = "-5 80"
                },
            };
        }

        #endregion
        
        #region Configuration

        private static ConfigData config = GetDefaultConfig();

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Cooldown between events")]
            public int cooldown = 3600;

            [JsonProperty(PropertyName = "Minimal players to start event")]
            public int minPlayers = 10;

            [JsonProperty(PropertyName = "Randomize events")]
            public bool randomizeEvents = false;

            [JsonProperty(PropertyName = "Events")]
            public EventDefinition[] events =
            {
                new EventDefinition
                {
                    shortname = "kill.bots",
                    displayName = "NPC Killer",
                    duration = 300,
                    type = "Kill",
                    targets = new[]
                    {
                        "bandit_guard",
                        "murderer",
                        "scarecrow",
                        "scientist_astar_full_any",
                        "scientist_junkpile_pistol",
                        "scientist_turret_any",
                        "scientist",
                        "scientist_gunner",
                        "scientistjunkpile",
                        "scientistpeacekeeper",
                        "scientiststationary",
                        "heavyscientist",
                        "humannpc",
                        "scientistnpc"
                    }
                },
                new EventDefinition
                {
                    shortname = "gather.all",
                    displayName = "Gather Master",
                    duration = 300,
                    type = "Gather",
                    targets = new[]
                    {
                        "wood",
                        "stones",
                        "sulfur.ore",
                        "metal.ore",
                        "hq.metal.ore",
                        "cloth",
                        "leather",
                        "fat.animal"
                    }
                }, 
            };
        }

        private class EventDefinition
        {
            [JsonProperty(PropertyName = "Shortname")]
            public string shortname;
            
            [JsonProperty(PropertyName = "Display name")]
            public string displayName;
            
            [JsonProperty(PropertyName = "Duration")]
            public int duration = 300;

            [JsonProperty(PropertyName = "Description")]
            public string description = string.Empty;

            [JsonProperty(PropertyName = "Type")]
            public string type = "Default";

            [JsonProperty(PropertyName = "Targets")]
            public string[] targets;

            [JsonProperty(PropertyName = "Rewards")]
            public RewardDefinition[] rewards =
            {
                new RewardDefinition
                {
                    places = new[] {1},
                    item = new BaseItem
                    {
                        command = "sr add {userid} 50",
                        description = "50 RP"
                    }
                },
                new RewardDefinition
                {
                    places = new[] {2},
                    item = new BaseItem
                    {
                        command = "sr add {userid} 25",
                        description = "25 RP"
                    }
                },
                new RewardDefinition
                {
                    places = new[] {3},
                    item = new BaseItem
                    {
                        command = "sr add {userid} 15",
                        description = "15 RP"
                    }
                },
                new RewardDefinition
                {
                    places = new[] {4, 5},
                    item = new BaseItem
                    {
                        command = "sr add {userid} 10",
                        description = "10 RP"
                    }
                },
            };
        }

        private class RewardDefinition
        {
            [JsonProperty(PropertyName = "Places")]
            public int[] places;
        
            [JsonProperty(PropertyName = "Item")]
            public BaseItem item;
        }

        private static ConfigData GetDefaultConfig()
        {
            return new ConfigData();
        }

        #endregion
        
        #region Language

        private enum Message
        {
            EventStarting,
            EventStarted,
            EventEnded,
            UIHeader,
            UILeft1,
            UIRight,
            UIPersonal,
        }
        
        private Dictionary<object, string> langMessages = new Dictionary<object, string>
        {
            {Message.EventStarting, "Event '<color=#00ffff>{0}</color>' is starting in {1} seconds! ({2})"},
            {Message.EventStarted, "Event '<color=#00ffff>{0}</color>' is starting! ({1})"},
            {Message.EventEnded, "Event '<color=#00ffff>{0}</color>' is ended! Winners:\n{1}"},
            {Message.UIHeader, "<color=#00ffff>[Event]</color> {0}"},
            {Message.UILeft1, "{0}"},
            {Message.UIRight, "{0}\n{1}"},
            {Message.UIPersonal, "<color=#FF7400>Your place is {0} ({1})</color>"}
        };

        #endregion
        
        #region Utils ver 0.0.1

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
                PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");

                timer.Every(10f,
                    () =>
                    {
                        PrintError("Configuration file is corrupt! Check your config file at https://jsonlint.com/");
                    });
                LoadDefaultConfig();
                return;
            }

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(config);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(langMessages.ToDictionary(x => x.Key.ToString(), y => y.Value), this);
        }

        private string GetMessage(Message key, string userID = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key.ToString(), this, userID), args);
        }

        private void Broadcast(Message key, params object[] args)
        {
            var text = GetMessage(key, null, args);

            foreach (var player in BasePlayer.activePlayerList)
            {
                player.ChatMessage(text);
            }
        }

        #endregion
        
        #region BaseItem Support 1.1.0

        private class BaseItem
        {
            [JsonProperty(PropertyName = "Command")]
            public string command = string.Empty;

            [JsonProperty(PropertyName = "Shortname")]
            private string shortname = string.Empty;

            [JsonProperty(PropertyName = "Description")]
            public string description = string.Empty;

            [JsonProperty(PropertyName = "Amount")]
            private int amount = 1;

            [JsonProperty(PropertyName = "Skin")] 
            private ulong skinId;

            [JsonProperty(PropertyName = "Display name")]
            private string displayName;

            [JsonProperty(PropertyName = "Blueprint")]
            private bool isBlueprint;

            public void GiveTo(BasePlayer player)
            {
                if (player == null)
                {
                    return;
                }
                
                var item = Create();
                if (item != null)
                {
                    player.GiveItem(item);
                }

                if (string.IsNullOrEmpty(command) == false)
                {
                    RunCommand(player);
                }
                
                player.ChatMessage($"You received '{description}'");
            }

            public void GiveTo(string playerID)
            {
                var player = BasePlayer.Find(playerID) ?? BasePlayer.FindSleeping(playerID);

                if (player == null)
                {
                    RunCommand(playerID);
                }
                else
                {
                    GiveTo(player);
                }
            }

            private Item Create()
            {
                if (isBlueprint)
                {
                    var blueprint = ItemManager.CreateByName("blueprintbase", amount);
                    blueprint.blueprintTarget = ItemManager.FindItemDefinition(shortname).itemid;
                    return blueprint;
                }

                var item = ItemManager.CreateByName(shortname, amount, skinId);
                if (item == null)
                {
                    return null;
                }

                item.name = displayName;
                return item;
            }

            private void RunCommand(BasePlayer player)
            {
                RunCommand(player.UserIDString);
            }

            private void RunCommand(string playerID)
            {
                if (string.IsNullOrEmpty(command))
                {
                    return;
                }

                var cmd = command
                    .Replace("{userid}", playerID, StringComparison.OrdinalIgnoreCase)
                    .Replace("{playerid}", playerID, StringComparison.OrdinalIgnoreCase);
                ConsoleSystem.Run(ConsoleSystem.Option.Server, cmd);
            }
        }

        #endregion

        #region Script

        private class EventController : MonoBehaviour
        {
            private void Start()
            {
                InvokeRepeating(nameof(StartRandomEvent), config.cooldown, eventStartInterval);
            }

            private void OnDestroy()
            {
                StopEvent(false);
            }

            public void StartRandomEvent()
            {
                if (Math.Abs(lastEventTime - Time.realtimeSinceStartup) < config.cooldown)
                {
                    return;
                }

                if (def != null || BasePlayer.activePlayerList.Count < config.minPlayers || config.events.Length == 0)
                {
                    return;
                }

                if (config.randomizeEvents == true)
                {
                    def = config.events.GetRandom();
                }
                else
                {
                    if (config.events.Length <= lastEventNum)
                    {
                        lastEventNum = 0;
                    }

                    def = config.events[lastEventNum];
                    lastEventNum++;
                }
                
                StartEvent();
            }
            
            private void StartEvent()
            {
                if (def == null)
                {
                    return;
                }

                for (var i = 0; i < 3; i++)
                {
                    var num1 = i * 20;
                    var num2 = 60 - num1;
                    
                    Timer.Once(num1, () =>
                    {
                        plugin.Broadcast(Message.EventStarting, displayName, num2, description);
                    });
                }
                
                Timer.Once(60f, () =>
                {
                    SetDefaultValues();
                    plugin.Broadcast(Message.EventStarted, displayName, description);
                    InvokeRepeating(nameof(RefreshUI), refreshRate , refreshRate);
                    Invoke(nameof(TimedStop), def.duration);
                    timeLeft = def.duration;
                    plugin.SubscribeToHooks();
                    lastEventTime = UnityEngine.Time.realtimeSinceStartup;
                });
            }

            public void StopEvent(bool hooks = true)
            {
                if (hooks == true)
                {
                    plugin.UnsubscribeFromHooks();
                }
                
                CancelInvoke(nameof(TimedStop));
                CancelInvoke(nameof(RefreshUI));
                SetDefaultValues();
                def = null;

                foreach (var player in BasePlayer.activePlayerList)
                {
                    CuiHelper.DestroyUi(player, elemMain);
                    CuiHelper.DestroyUi(player, elemButton);
                }
            }
            
            private void TimedStop()
            {
                RewardWinners();
                StopEvent();
            }

            private void RefreshUI()
            {
                if (def == null)
                {
                    return;
                }
                
                UpdatePlayers();
                UpdateText();
                BuildUI();

                foreach (var player in BasePlayer.activePlayerList)
                {
                    var data = orderedScore.First(x => x.userID == player.userID);
                    var place = Array.IndexOf(orderedScore, data) + 1;
                    var points = data.score;
                    var personalText = plugin.GetMessage(Message.UIPersonal, player.UserIDString, place, points);
                    ShowUI(player, personalText);
                }
            }
        }
        
        private class PlayerData
        {
            public BasePlayer player;
            public ulong userID;
            public string playerName;
            public int score;

            public PlayerData(BasePlayer value)
            {
                player = value;
                userID = value.userID;
                playerName = value.displayName;
                score = 0;
            }
        }

        #endregion
    }
}