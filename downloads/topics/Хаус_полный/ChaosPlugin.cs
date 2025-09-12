using Oxide.Core;
using Oxide.Core.Plugins;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;
using Rust;
using Oxide.Game.Rust.Cui;
using System;
using UnityEngine.AI;

namespace Oxide.Plugins
{
    [Info("Chaos Mode", "YourName", "1.0.0")]
    [Description("Creates extreme chaos and monstrous behaviors in the game")]
    public class ChaosPlugin : RustPlugin
    {
        private Dictionary<ulong, float> playerJumpPowers = new Dictionary<ulong, float>();
        private Dictionary<ulong, float> playerSizes = new Dictionary<ulong, float>();
        private Dictionary<ulong, PlayerModifiers> playerModifiers = new Dictionary<ulong, PlayerModifiers>();
        private Dictionary<BaseNpc, NpcChaosData> npcChaos = new Dictionary<BaseNpc, NpcChaosData>();
        private Timer chaosTimer;
        private List<ActiveEvent> activeEvents = new List<ActiveEvent>();
        private List<BaseNpc> chaosNPCs = new List<BaseNpc>();

        // UI Elements
        private const string UIMain = "ChaosUI";
        private const string UIPanel = UIMain + ".Panel";
        private const string UIEvent = UIPanel + ".Event";
        private const string UITimer = UIPanel + ".Timer";
        private const string UIEventList = UIPanel + ".EventList";

        #region Plugin Priority and Permissions
        private const string PermAdmin = "chaos.admin";
        private const string PermUse = "chaos.use";
        private const int PluginPriority = 0; // Highest priority
        #endregion

        private class ActiveEvent
        {
            public string Name { get; set; }
            public float EndTime { get; set; }
        }

        private class PlayerModifiers
        {
            public float HealthMultiplier = 1f;
            public float DamageMultiplier = 1f;
            public float SpeedMultiplier = 1f;
            public float FallDamageReduction = 0f;
            public bool IsGodMode = false;
            public bool NoClip = false;
            public float GatherMultiplier = 1f;
        }

        private class NpcChaosData
        {
            public bool IsMutated;
            public float SizeMultiplier;
            public float SpeedMultiplier;
            public float DamageMultiplier;
            public bool IsExplosive;
            public bool IsTeleporter;
            public bool IsResourceDropper;
            public string CustomBehavior;
        }

        // Configuration
        private Configuration config;
        public class Configuration
        {
            // Event Settings
            public float ChaosInterval = 30f; // Changed to 30 seconds for more frequent chaos
            public bool EnableRandomSizes = true;
            public bool EnableJumpModification = true;
            public bool EnableResourceMultiplier = true;
            public bool EnableCombatChaos = true;
            public bool EnableEventChaos = true;
            public int MaxSimultaneousEvents = 5; // Increased simultaneous events

            // Player Modification Settings
            public float MinPlayerSize = 0.3f; // Even smaller possible
            public float MaxPlayerSize = 8f; // Even larger possible
            public float MinJumpPower = 2f; // Increased minimum jump
            public float MaxJumpPower = 10f; // Increased maximum jump
            public float SuperJumpChance = 0.2f; // More frequent super jumps
            public float DamageReflectChance = 0.25f; // More frequent damage reflection
            public float ResourceMultiplierChance = 0.4f; // More frequent resource bonuses

            // Balance Settings
            public float BaseHealthMultiplier = 3f; // More base health
            public float MaxHealthMultiplier = 8f; // Much more max health
            public float BaseDamageMultiplier = 2f; // More base damage
            public float MaxDamageMultiplier = 5f; // Much more max damage
            public float BaseSpeedMultiplier = 1.5f; // Faster base speed
            public float MaxSpeedMultiplier = 4f; // Much faster max speed
            public float BaseFallDamageReduction = 0.9f; // Almost no fall damage by default
            public float MaxFallDamageReduction = 1f; // Complete fall damage immunity possible
            public float BaseGatherMultiplier = 5f; // Much more resource gathering
            public float MaxGatherMultiplier = 20f; // Extreme resource gathering

            // Event Chances - More balanced distribution for variety
            public float WeirdEffectChance = 0.4f;
            public float RaidDefenseChance = 0.55f;
            public float LootExplosionChance = 0.7f;
            public float RandomTeleportChance = 0.8f;
            public float WeatherChangeChance = 0.9f;
            public float AnimalPartyChance = 0.95f;
            public float ResourceRainChance = 1f;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null)
                {
                    LoadDefaultConfig();
                }
            }
            catch
            {
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        void Init()
        {
            // Register permissions
            permission.RegisterPermission(PermAdmin, this);
            permission.RegisterPermission(PermUse, this);

            // Start the chaos timer
            if (chaosTimer != null)
            {
                chaosTimer.Destroy();
            }
            chaosTimer = timer.Every(config.ChaosInterval, () =>
            {
                TriggerRandomChaosEvent();
                foreach (var player in BasePlayer.activePlayerList)
                {
                    if (player != null)
                    {
                        UpdatePlayerUI(player);
                    }
                }
            });

            Puts("Chaos Mode initialized! Prepare for weirdness...");

            // Create UI for all active players
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    CreateUI(player);
                    UpdatePlayerUI(player);
                }
            }

            // Override server settings
            SetServerOverrides();
        }

        private void SetServerOverrides()
        {
            try
            {
                // Override server settings for better chaos experience
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "pve", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "antihack.noclip_protection", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "antihack.maxdesync", "1");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "antihack.maxviolation", "1000");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.radiation", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.respawnresetrange", "0");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.itemdespawn", "7200");
                ConsoleSystem.Run(ConsoleSystem.Option.Server, "server.corpsedespawn", "600");
                
                // Broadcast server settings change
                foreach (var player in BasePlayer.activePlayerList)
                {
                    Player.Message(player, "Chaos Mode has taken control of the server!", "Chaos");
                }

                Puts("Server settings overridden for Chaos Mode");
            }
            catch (Exception ex)
            {
                Puts($"Error setting server overrides: {ex.Message}");
            }
        }

        protected override void LoadDefaultConfig()
        {
            Puts("Creating new configuration file");
            config = new Configuration
            {
                // Event Settings
                ChaosInterval = 30f,
                EnableRandomSizes = true,
                EnableJumpModification = true,
                EnableResourceMultiplier = true,
                EnableCombatChaos = true,
                EnableEventChaos = true,
                MaxSimultaneousEvents = 5,

                // Player Modification Settings
                MinPlayerSize = 0.3f,
                MaxPlayerSize = 8f,
                MinJumpPower = 2f,
                MaxJumpPower = 10f,
                SuperJumpChance = 0.2f,
                DamageReflectChance = 0.25f,
                ResourceMultiplierChance = 0.4f,

                // Balance Settings
                BaseHealthMultiplier = 3f,
                MaxHealthMultiplier = 8f,
                BaseDamageMultiplier = 2f,
                MaxDamageMultiplier = 5f,
                BaseSpeedMultiplier = 1.5f,
                MaxSpeedMultiplier = 4f,
                BaseFallDamageReduction = 0.9f,
                MaxFallDamageReduction = 1f,
                BaseGatherMultiplier = 5f,
                MaxGatherMultiplier = 20f,

                // Event Chances
                WeirdEffectChance = 0.4f,
                RaidDefenseChance = 0.55f,
                LootExplosionChance = 0.7f,
                RandomTeleportChance = 0.8f,
                WeatherChangeChance = 0.9f,
                AnimalPartyChance = 0.95f,
                ResourceRainChance = 1f
            };
        }

        #region Command Handling
        [ChatCommand("chaos")]
        void ChaosCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermAdmin))
            {
                Player.Message(player, "You don't have permission to use this command!", "Chaos");
                return;
            }

            if (args.Length == 0)
            {
                SendHelpText(player);
                return;
            }

            Puts($"Chaos command received: {string.Join(" ", args)}"); // Debug log

            switch (args[0].ToLower())
            {
                case "event":
                    Puts("Triggering random chaos event..."); // Debug log
                    TriggerRandomChaosEvent();
                    Player.Message(player, "Triggered a random chaos event!", "Chaos");
                    CreateUI(player); // Ensure UI exists
                    UpdatePlayerUI(player); // Update UI immediately
                    break;

                case "size":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos size <playerName> <size>", "Chaos");
                        return;
                    }
                    var target = BasePlayer.Find(args[1]);
                    if (target == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float size))
                    {
                        ModifyPlayerSize(target, size);
                        Player.Message(player, $"Changed {target.displayName}'s size to {size}x", "Chaos");
                    }
                    break;

                case "health":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos health <playerName> <multiplier>", "Chaos");
                        return;
                    }
                    var healthTarget = BasePlayer.Find(args[1]);
                    if (healthTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float healthMult))
                    {
                        SetPlayerHealthMultiplier(healthTarget, healthMult);
                        Player.Message(player, $"Changed {healthTarget.displayName}'s health multiplier to {healthMult}x", "Chaos");
                    }
                    break;

                case "speed":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos speed <playerName> <multiplier>", "Chaos");
                        return;
                    }
                    var speedTarget = BasePlayer.Find(args[1]);
                    if (speedTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float speedMult))
                    {
                        SetPlayerSpeedMultiplier(speedTarget, speedMult);
                        Player.Message(player, $"Changed {speedTarget.displayName}'s speed multiplier to {speedMult}x", "Chaos");
                    }
                    break;

                case "god":
                    if (args.Length < 2)
                    {
                        Player.Message(player, "Usage: /chaos god <playerName>", "Chaos");
                        return;
                    }
                    var godTarget = BasePlayer.Find(args[1]);
                    if (godTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    ToggleGodMode(godTarget);
                    Player.Message(player, $"Toggled god mode for {godTarget.displayName}", "Chaos");
                    break;

                case "reset":
                    if (args.Length < 2)
                    {
                        ResetAllPlayers();
                        Player.Message(player, "Reset all players to default state", "Chaos");
                    }
                    else
                    {
                        var resetTarget = BasePlayer.Find(args[1]);
                        if (resetTarget == null)
                        {
                            Player.Message(player, "Player not found!", "Chaos");
                            return;
                        }
                        ResetPlayer(resetTarget);
                        Player.Message(player, $"Reset {resetTarget.displayName} to default state", "Chaos");
                    }
                    break;

                case "gather":
                    if (args.Length < 3)
                    {
                        Player.Message(player, "Usage: /chaos gather <playerName> <multiplier>", "Chaos");
                        return;
                    }
                    var gatherTarget = BasePlayer.Find(args[1]);
                    if (gatherTarget == null)
                    {
                        Player.Message(player, "Player not found!", "Chaos");
                        return;
                    }
                    if (float.TryParse(args[2], out float gatherMult))
                    {
                        SetGatherMultiplier(gatherTarget, gatherMult);
                        Player.Message(player, $"Changed {gatherTarget.displayName}'s gather rate to {gatherMult}x", "Chaos");
                    }
                    break;

                default:
                    SendHelpText(player);
                    break;
            }
        }

        void SendHelpText(BasePlayer player)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("Chaos Mode Commands:");
            sb.AppendLine("/chaos event - Trigger a random chaos event");
            sb.AppendLine("/chaos size <player> <size> - Change player size");
            sb.AppendLine("/chaos health <player> <multiplier> - Set health multiplier");
            sb.AppendLine("/chaos speed <player> <multiplier> - Set speed multiplier");
            sb.AppendLine("/chaos god <player> - Toggle god mode");
            sb.AppendLine("/chaos gather <player> <multiplier> - Set gather multiplier");
            sb.AppendLine("/chaos reset [player] - Reset all or specific player");
            Player.Message(player, sb.ToString(), "Chaos");
        }
        #endregion

        #region Player Modifications
        private void SetPlayerHealthMultiplier(BasePlayer player, float multiplier)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].HealthMultiplier = Mathf.Clamp(multiplier, 1f, config.MaxHealthMultiplier);
            UpdatePlayerHealth(player);
        }

        private void SetPlayerSpeedMultiplier(BasePlayer player, float multiplier)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].SpeedMultiplier = Mathf.Clamp(multiplier, 0.5f, config.MaxSpeedMultiplier);
        }

        private void SetGatherMultiplier(BasePlayer player, float multiplier)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].GatherMultiplier = Mathf.Clamp(multiplier, 1f, config.MaxGatherMultiplier);
        }

        private void ToggleGodMode(BasePlayer player)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();

            playerModifiers[player.userID].IsGodMode = !playerModifiers[player.userID].IsGodMode;
            Player.Message(player, playerModifiers[player.userID].IsGodMode ? "God mode enabled!" : "God mode disabled!", "Chaos");
        }

        private void UpdatePlayerHealth(BasePlayer player)
        {
            if (!playerModifiers.ContainsKey(player.userID)) return;

            float maxHealth = 100f * playerModifiers[player.userID].HealthMultiplier;
            player.health = maxHealth;
            player.MaxHealth();
        }

        private void ResetPlayer(BasePlayer player)
        {
            ModifyPlayerSize(player, 1f);
            playerJumpPowers.Remove(player.userID);
            playerModifiers.Remove(player.userID);
            player.health = 100f;
            player.MaxHealth();
        }

        private void ResetAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                ResetPlayer(player);
            }
        }
        #endregion

        #region UI System
        private void CreateUI(BasePlayer player)
        {
            var ui = new CuiElementContainer();

            // Background gradient panel
            ui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.8 0.5", AnchorMax = "0.99 0.95" },
                CursorEnabled = false
            }, "Hud", UIMain);

            // Top border
            ui.Add(new CuiPanel
            {
                Image = { Color = "1 0.5 0 1" },
                RectTransform = { AnchorMin = "0 0.98", AnchorMax = "1 1" }
            }, UIMain);

            // Bottom border
            ui.Add(new CuiPanel
            {
                Image = { Color = "1 0.5 0 1" },
                RectTransform = { AnchorMin = "0 0", AnchorMax = "1 0.02" }
            }, UIMain);

            // Title background with gradient
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.7 0.3 0 0.95", Material = "assets/content/ui/uibackgroundblur-ingame.mat" },
                RectTransform = { AnchorMin = "0 0.85", AnchorMax = "1 1" }
            }, UIMain);

            // Event text with glow effect
            ui.Add(new CuiElement
            {
                Parent = UIMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "CHAOS MODE",
                        FontSize = 16,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = "1 0.6 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.85",
                        AnchorMax = "1 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0.5 0.2 0 1",
                        Distance = "1 1"
                    }
                },
                Name = UIEvent
            });

            // Timer section with background
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.1 0.7", AnchorMax = "0.9 0.85" }
            }, UIMain, "TimerPanel");

            // Timer text with pulse effect
            ui.Add(new CuiElement
            {
                Parent = "TimerPanel",
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "Next event: 1:00",
                        FontSize = 14,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = "0.7 1 0.7 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0 0.5 0 1",
                        Distance = "1 1"
                    }
                },
                Name = UITimer
            });

            // Event list header
            ui.Add(new CuiElement
            {
                Parent = UIMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "ACTIVE EVENTS",
                        FontSize = 14,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.UpperCenter,
                        Color = "1 0.8 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.6",
                        AnchorMax = "1 0.7"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0.5 0.4 0 1",
                        Distance = "1 1"
                    }
                }
            });

            // Event list background
            ui.Add(new CuiPanel
            {
                Image = { Color = "0.1 0.1 0.1 0.95", Material = "assets/content/ui/uibackgroundblur.mat" },
                RectTransform = { AnchorMin = "0.05 0.05", AnchorMax = "0.95 0.6" }
            }, UIMain, UIEventList);

            CuiHelper.DestroyUi(player, UIMain);
            CuiHelper.AddUi(player, ui);
        }

        private string GetEventColor(string eventName)
        {
            switch (eventName)
            {
                case "GIANT MODE": return "1 0.5 0.5 1";
                case "SUPER SPEED": return "0.5 1 0.5 1";
                case "RANDOM HEALTH": return "1 0.5 1 1";
                case "DANCE PARTY": return "0.5 0.5 1 1";
                case "RAID DEFENSE": return "1 0.7 0.3 1";
                case "LOOT EXPLOSION": return "1 1 0.5 1";
                case "RANDOM TELEPORT": return "0.7 0.3 1 1";
                case "ANIMAL PARTY": return "0.3 1 0.7 1";
                case "RESOURCE RAIN": return "0.5 1 1 1";
                default: return "1 1 1 1";
            }
        }

        private void UpdatePlayerUI(BasePlayer player)
        {
            if (player == null) return;

            Puts($"Updating UI for player {player.displayName}"); // Debug log

            // First destroy existing UI elements
            CuiHelper.DestroyUi(player, UIEvent);
            CuiHelper.DestroyUi(player, UITimer);
            CuiHelper.DestroyUi(player, UIEventList);

            var ui = new CuiElementContainer();

            // Update title based on active events
            string titleText = activeEvents.Count > 0 ? "CHAOS UNLEASHED!" : "CHAOS MODE";
            string titleColor = activeEvents.Count > 0 ? "1 0.3 0 1" : "1 0.6 0 1";

            // Title text
            ui.Add(new CuiElement
            {
                Parent = UIMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = titleText,
                        FontSize = 20,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = titleColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.85",
                        AnchorMax = "1 1"
                    },
                    new CuiOutlineComponent
                    {
                        Color = "0.5 0.2 0 1",
                        Distance = "1 1"
                    }
                },
                Name = UIEvent
            });

            // Update timer
            float timeLeft = config.ChaosInterval - (Time.time % config.ChaosInterval);
            string timerText = $"Next event: {(int)(timeLeft / 60)}:{(int)(timeLeft % 60):00}";
            string timerColor = timeLeft < 10 ? "1 0 0 1" : timeLeft < 30 ? "1 1 0 1" : "0.7 1 0.7 1";

            // Timer text
            ui.Add(new CuiElement
            {
                Parent = UIMain,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = timerText,
                        FontSize = 16,
                        Font = "robotocondensed-regular.ttf",
                        Align = TextAnchor.MiddleCenter,
                        Color = timerColor
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.75",
                        AnchorMax = "1 0.85"
                    },
                    new CuiOutlineComponent
                    {
                        Color = timeLeft < 10 ? "0.5 0 0 1" : "0 0.5 0 1",
                        Distance = "1 1"
                    }
                },
                Name = UITimer
            });

            // Event list container
            ui.Add(new CuiPanel
            {
                Image = { Color = "0 0 0 0.8" },
                RectTransform = { AnchorMin = "0 0.1", AnchorMax = "1 0.75" }
            }, UIMain, UIEventList);

            // Event list header
            ui.Add(new CuiElement
            {
                Parent = UIEventList,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "ACTIVE EVENTS",
                        FontSize = 16,
                        Font = "robotocondensed-bold.ttf",
                        Align = TextAnchor.UpperCenter,
                        Color = "1 0.8 0 1"
                    },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = "0 0.9",
                        AnchorMax = "1 1"
                    }
                }
            });

            // Display active events
            float currentY = 0.85f;
            float entryHeight = 0.15f;

            Puts($"Active events count: {activeEvents.Count}"); // Debug log

            foreach (var evt in activeEvents.OrderBy(e => e.EndTime))
            {
                Puts($"Adding event to UI: {evt.Name}"); // Debug log

                float remaining = evt.EndTime - Time.time;
                float progress = Mathf.Clamp01(remaining / 30f);
                string eventColor = GetEventColor(evt.Name);

                // Event text
                string eventText = $"• {evt.Name} ({(int)remaining}s)";
                ui.Add(new CuiElement
                {
                    Parent = UIEventList,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = eventText,
                            FontSize = 14,
                            Font = "robotocondensed-regular.ttf",
                            Align = TextAnchor.MiddleLeft,
                            Color = eventColor
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = $"0.05 {currentY - entryHeight}",
                            AnchorMax = $"0.95 {currentY}"
                        }
                    }
                });

                // Progress bar background
                ui.Add(new CuiPanel
                {
                    Image = { Color = "0.2 0.2 0.2 1" },
                    RectTransform = { 
                        AnchorMin = $"0.05 {currentY - entryHeight + 0.05}",
                        AnchorMax = $"0.95 {currentY - entryHeight + 0.08}"
                    }
                }, UIEventList);

                // Progress bar fill
                ui.Add(new CuiPanel
                {
                    Image = { Color = eventColor },
                    RectTransform = { 
                        AnchorMin = $"0.05 {currentY - entryHeight + 0.05}",
                        AnchorMax = $"{0.05 + (0.9 * progress)} {currentY - entryHeight + 0.08}"
                    }
                }, UIEventList);

                currentY -= entryHeight;
            }

            // Apply the UI changes
            CuiHelper.AddUi(player, ui);
        }
        #endregion

        #region Hooks
        void OnPlayerConnected(BasePlayer player)
        {
            if (!playerModifiers.ContainsKey(player.userID))
                playerModifiers[player.userID] = new PlayerModifiers();
            
            if (config.EnableRandomSizes)
            {
                playerSizes[player.userID] = Random.Range(config.MinPlayerSize, config.MaxPlayerSize);
                ModifyPlayerSize(player, playerSizes[player.userID]);
            }

            if (config.EnableJumpModification)
            {
                playerJumpPowers[player.userID] = Random.Range(config.MinJumpPower, config.MaxJumpPower);
            }

            // Apply base modifiers
            playerModifiers[player.userID].HealthMultiplier = config.BaseHealthMultiplier;
            playerModifiers[player.userID].SpeedMultiplier = config.BaseSpeedMultiplier;
            playerModifiers[player.userID].FallDamageReduction = config.BaseFallDamageReduction;
            playerModifiers[player.userID].GatherMultiplier = config.BaseGatherMultiplier;

            UpdatePlayerHealth(player);
            
            string welcomeMessage = $"Welcome to CHAOS MODE! ";
            if (config.EnableJumpModification)
                welcomeMessage += $"Jump power: {playerJumpPowers[player.userID]}x ";
            if (config.EnableRandomSizes)
                welcomeMessage += $"Size: {playerSizes[player.userID]}x";
            
            Player.Message(player, welcomeMessage, "Chaos");

            // Create UI for new player
            CreateUI(player);
            UpdatePlayerUI(player);
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            playerJumpPowers.Remove(player.userID);
            playerSizes.Remove(player.userID);
            playerModifiers.Remove(player.userID);
            CuiHelper.DestroyUi(player, UIMain);
        }

        object OnPlayerJump(BasePlayer player)
        {
            if (!config.EnableJumpModification || !playerJumpPowers.ContainsKey(player.userID))
                return null;

            var rigidbody = player.GetComponent<Rigidbody>();
            if (rigidbody != null)
            {
                float jumpForce = playerJumpPowers[player.userID] * 5f;
                
                if (Random.Range(0f, 1f) < config.SuperJumpChance)
                {
                    jumpForce *= 5f;
                    Player.Message(player, "SUPER JUMP!", "Chaos");
                }

                rigidbody.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
            }
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null || info == null) return null;

            // Handle player damage
            if (entity is BasePlayer player)
            {
                if (!playerModifiers.ContainsKey(player.userID))
                    return null;

                var mods = playerModifiers[player.userID];

                // God mode check
                if (mods.IsGodMode)
                    return true; // Block all damage

                // Fall damage reduction
                if (info.damageTypes.Has(DamageType.Fall))
                {
                    info.damageTypes.Scale(DamageType.Fall, 1f - mods.FallDamageReduction);
                }

                // Damage multiplier for PvP
                if (info.Initiator is BasePlayer)
                {
                    info.damageTypes.Scale(DamageType.Generic, mods.DamageMultiplier);
                }
            }

            // Handle damage reflection
            if (config.EnableCombatChaos && entity is BasePlayer && info?.Initiator is BasePlayer)
            {
                if (Random.Range(0f, 1f) < config.DamageReflectChance)
                {
                    BasePlayer attacker = info.Initiator.ToPlayer();
                    attacker.Hurt(info.damageTypes.Total());
                    Player.Message(attacker, "Your attack was reflected!", "Chaos");
                }
            }

            return null;
        }

        void OnGatherItem(Item item, BaseEntity entity)
        {
            if (!config.EnableResourceMultiplier) return;

            var player = entity.ToPlayer();
            if (player == null || !playerModifiers.ContainsKey(player.userID)) return;

            float multiplier = playerModifiers[player.userID].GatherMultiplier;
            if (Random.Range(0f, 1f) < config.ResourceMultiplierChance)
            {
                multiplier *= Random.Range(2, 10);
                Player.Message(player, $"Lucky! Resources multiplied by {multiplier}x!", "Chaos");
            }

            item.amount = Mathf.RoundToInt(item.amount * multiplier);
        }

        private void ModifyPlayerSize(BasePlayer player, float scale)
        {
            if (player?.transform != null)
            {
                Puts($"Modifying size for {player.displayName} to {scale}x"); // Debug log
                try
                {
                    player.transform.localScale = new Vector3(scale, scale, scale);
                    player.SendNetworkUpdate();
                    player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position);
                }
                catch (Exception ex)
                {
                    Puts($"Error modifying player size: {ex.Message}");
                }
            }
        }
        #endregion

        #region Events
        private void TriggerRandomChaosEvent()
        {
            Puts("TriggerRandomChaosEvent called"); // Debug log
            if (!config.EnableEventChaos)
            {
                Puts("Event chaos is disabled!");
                return;
            }

            var players = BasePlayer.activePlayerList.ToList();
            if (players.Count == 0)
            {
                Puts("No active players found!");
                return;
            }

            float eventRoll = Random.Range(0f, 1f);
            Puts($"Event roll: {eventRoll}"); // Debug log
            
            if (eventRoll < 0.15f) // Increased chance for weird effects
            {
                Puts("Triggering weird effect");
                TriggerWeirdEffect(players);
            }
            else if (eventRoll < 0.3f) // Increased chance for raid defense
            {
                Puts("Triggering raid defense");
                TriggerRaidDefense(players);
            }
            else if (eventRoll < 0.45f) // Increased chance for loot explosion
            {
                Puts("Triggering loot explosion");
                TriggerLootExplosion(players);
            }
            else if (eventRoll < 0.6f) // Increased chance for random teleport
            {
                Puts("Triggering random teleport");
                TriggerRandomTeleport(players);
            }
            else if (eventRoll < 0.7f)
            {
                Puts("Triggering weather change");
                TriggerWeatherChange();
            }
            else if (eventRoll < 0.8f)
            {
                Puts("Triggering animal party");
                TriggerAnimalParty(players);
            }
            else if (eventRoll < 0.85f)
            {
                Puts("Triggering resource rain");
                TriggerResourceRain(players);
            }
            else if (eventRoll < 0.9f)
            {
                Puts("Triggering chaos NPC event");
                TriggerChaosNPCEvent();
            }
            else if (eventRoll < 0.95f)
            {
                Puts("Triggering mind control event");
                TriggerMindControlEvent();
            }
            else
            {
                Puts("Triggering mass chaos event");
                // Trigger multiple events at once for maximum chaos
                TriggerWeirdEffect(players);
                TriggerLootExplosion(players);
                TriggerChaosNPCEvent();
                TriggerResourceRain(players);
                UpdateEventUI("MASS CHAOS", 300f);
                Server.Broadcast("CHAOS EVENT: MASS CHAOS - EVERYTHING IS HAPPENING!");
            }
        }

        private void TriggerWeirdEffect(List<BasePlayer> players)
        {
            int effectType = Random.Range(0, 12); // Increased number of effects
            string eventName = "";
            float duration = 180f; // 3 minutes default

            switch (effectType)
            {
                case 0:
                    eventName = "GIANT MODE";
                    foreach (var player in players)
                    {
                        float newSize = Random.Range(3f, 8f);
                        ModifyPlayerSize(player, newSize);
                        playerSizes[player.userID] = newSize;
                        Player.Message(player, $"You've grown to {newSize}x size!", "Chaos");
                    }
                    break;

                case 1:
                    eventName = "SUPER SPEED";
                    foreach (var player in players)
                    {
                        if (!playerModifiers.ContainsKey(player.userID))
                            playerModifiers[player.userID] = new PlayerModifiers();
                        playerModifiers[player.userID].SpeedMultiplier = Random.Range(3f, 6f);
                        Player.Message(player, "GOTTA GO FAST!", "Chaos");
                    }
                    break;

                case 2:
                    eventName = "BOUNCY CASTLE";
                    foreach (var player in players)
                    {
                        if (!playerModifiers.ContainsKey(player.userID))
                            playerModifiers[player.userID] = new PlayerModifiers();
                        playerJumpPowers[player.userID] = Random.Range(8f, 15f);
                        Player.Message(player, "BOUNCE! BOUNCE! BOUNCE!", "Chaos");
                    }
                    break;

                case 3:
                    eventName = "GRAVITY SHIFT";
                    foreach (var player in players)
                    {
                        var rigidbody = player.GetComponent<Rigidbody>();
                        if (rigidbody != null)
                        {
                            rigidbody.mass = Random.Range(0.05f, 0.3f);
                            Player.Message(player, "Gravity? What gravity?", "Chaos");
                        }
                    }
                    break;

                case 4:
                    eventName = "EXPLOSIVE TOUCH";
                    foreach (var player in players)
                    {
                        timer.Every(0.5f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_satchel.prefab", player.transform.position);
                                foreach (var collider in Physics.OverlapSphere(player.transform.position, 5f))
                                {
                                    var target = collider.GetComponentInParent<BasePlayer>();
                                    if (target != null && target != player)
                                    {
                                        target.Hurt(35f);
                                    }
                                }
                            }
                        });
                    }
                    break;

                case 5:
                    eventName = "RESOURCE AURA";
                    foreach (var player in players)
                    {
                        timer.Every(1f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                for (int i = 0; i < Random.Range(3, 8); i++)
                                {
                                    DropRandomResources(player.transform.position);
                                }
                            }
                        });
                    }
                    break;

                case 6:
                    eventName = "CHAOS STORM";
                    foreach (var player in players)
                    {
                        timer.Every(0.3f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                if (Random.value < 0.3f)
                                {
                                    Vector3 strikePos = player.transform.position + new Vector3(Random.Range(-15f, 15f), 0, Random.Range(-15f, 15f));
                                    Effect.server.Run("assets/bundled/prefabs/fx/lightning_strike.prefab", strikePos);
                                    foreach (var collider in Physics.OverlapSphere(strikePos, 5f))
                                    {
                                        var target = collider.GetComponentInParent<BasePlayer>();
                                        if (target != null)
                                        {
                                            target.Hurt(20f);
                                        }
                                    }
                                }
                            }
                        });
                    }
                    break;

                case 7:
                    eventName = "RANDOM TELEPORTS";
                    foreach (var player in players)
                    {
                        timer.Every(2f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                Vector3 randomPos = player.transform.position + new Vector3(
                                    Random.Range(-30f, 30f),
                                    Random.Range(0f, 8f),
                                    Random.Range(-30f, 30f)
                                );
                                player.Teleport(randomPos);
                                Effect.server.Run("assets/bundled/prefabs/fx/gestures/wave.prefab", player.transform.position);
                            }
                        });
                    }
                    break;

                case 8:
                    eventName = "FIRE TRAIL";
                    foreach (var player in players)
                    {
                        timer.Every(0.2f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                Effect.server.Run("assets/bundled/prefabs/fx/fire.prefab", player.transform.position);
                                foreach (var collider in Physics.OverlapSphere(player.transform.position, 3f))
                                {
                                    var target = collider.GetComponentInParent<BasePlayer>();
                                    if (target != null && target != player)
                                    {
                                        target.Hurt(10f);
                                    }
                                }
                            }
                        });
                    }
                    break;

                case 9:
                    eventName = "LOOT PINATA";
                    foreach (var player in players)
                    {
                        timer.Every(0.5f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                if (Random.value < 0.3f)
                                {
                                    Item randomItem = ItemManager.CreateByName(GetRandomResource(), Random.Range(50, 200));
                                    if (randomItem != null)
                                    {
                                        randomItem.Drop(
                                            player.transform.position + Vector3.up * 3f,
                                            Vector3.up * Random.Range(2f, 5f) + player.transform.forward * Random.Range(-3f, 3f)
                                        );
                                    }
                                }
                            }
                        });
                    }
                    break;

                case 10:
                    eventName = "SUPER STRENGTH";
                    foreach (var player in players)
                    {
                        if (!playerModifiers.ContainsKey(player.userID))
                            playerModifiers[player.userID] = new PlayerModifiers();
                        playerModifiers[player.userID].DamageMultiplier = Random.Range(5f, 10f);
                        Player.Message(player, "UNLIMITED POWER!", "Chaos");
                    }
                    break;

                case 11:
                    eventName = "CHAOS VISION";
                    foreach (var player in players)
                    {
                        timer.Every(0.5f, () => {
                            if (player != null && Time.time < Time.time + duration)
                            {
                                string[] effects = {
                                    "assets/bundled/prefabs/fx/explosions/explosion_satchel.prefab",
                                    "assets/bundled/prefabs/fx/fire.prefab",
                                    "assets/bundled/prefabs/fx/smoke_signal_green.prefab",
                                    "assets/bundled/prefabs/fx/gestures/wave.prefab",
                                    "assets/bundled/prefabs/fx/lightning_strike.prefab"
                                };
                                Vector3 effectPos = player.transform.position + new Vector3(
                                    Random.Range(-10f, 10f),
                                    Random.Range(0f, 5f),
                                    Random.Range(-10f, 10f)
                                );
                                Effect.server.Run(effects[Random.Range(0, effects.Length)], effectPos);
                            }
                        });
                    }
                    break;
            }

            UpdateEventUI(eventName, duration);
            Server.Broadcast($"CHAOS EVENT: {eventName}!");
        }

        private void TriggerRaidDefense(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                for (int i = 0; i < Random.Range(2, 5); i++)
                {
                    Vector3 spawnPos = player.transform.position + new Vector3(
                        Random.Range(-20f, 20f),
                        0f,
                        Random.Range(-20f, 20f)
                    );
                    
                    GameManager.server.CreateEntity("assets/prefabs/npc/autoturret/autoturret_deployed.prefab", spawnPos)?.Spawn();
                }
            }
            UpdateEventUI("RAID DEFENSE", 45f);
            Server.Broadcast("CHAOS EVENT: Auto-turret Defense System!");
        }

        private void TriggerLootExplosion(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                for (int i = 0; i < Random.Range(5, 15); i++)
                {
                    Vector3 spawnPos = player.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        Random.Range(5f, 15f),
                        Random.Range(-10f, 10f)
                    );
                    
                    GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", spawnPos)?.Spawn();
                }
            }
            UpdateEventUI("LOOT EXPLOSION", 60f);
            Server.Broadcast("CHAOS EVENT: Loot Explosion!");
        }

        private void TriggerRandomTeleport(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                Vector3 randomPos = player.transform.position + new Vector3(
                    Random.Range(-50f, 50f),
                    Random.Range(0f, 10f),
                    Random.Range(-50f, 50f)
                );
                player.Teleport(randomPos);
            }
            UpdateEventUI("RANDOM TELEPORT", 15f);
            Server.Broadcast("CHAOS EVENT: Random Teleport Party!");
        }

        private void TriggerWeatherChange()
        {
            string[] weathers = { "rain", "fog", "storm", "clear" };
            string randomWeather = weathers[Random.Range(0, weathers.Length)];
            ConVar.Weather.rain = randomWeather == "rain" ? 1 : 0;
            ConVar.Weather.fog = randomWeather == "fog" ? 1 : 0;
            UpdateEventUI($"WEATHER: {randomWeather.ToUpper()}", 120f);
            Server.Broadcast($"CHAOS EVENT: Weather changed to {randomWeather}!");
        }

        private void TriggerAnimalParty(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                for (int i = 0; i < Random.Range(2, 5); i++)
                {
                    Vector3 spawnPos = player.transform.position + new Vector3(
                        Random.Range(-20f, 20f),
                        0f,
                        Random.Range(-20f, 20f)
                    );
                    
                    string[] animals = { 
                        "assets/rust.ai/agents/chicken/chicken.prefab",
                        "assets/rust.ai/agents/boar/boar.prefab",
                        "assets/rust.ai/agents/stag/stag.prefab",
                        "assets/rust.ai/agents/wolf/wolf.prefab",
                        "assets/rust.ai/agents/bear/bear.prefab",
                        "assets/rust.ai/agents/horse/horse.prefab"
                    };
                    string randomAnimal = animals[Random.Range(0, animals.Length)];
                    
                    GameManager.server.CreateEntity(randomAnimal, spawnPos)?.Spawn();
                }
            }
            UpdateEventUI("ANIMAL PARTY", 30f);
            Server.Broadcast("CHAOS EVENT: Animal Party!");
        }

        private void TriggerResourceRain(List<BasePlayer> players)
        {
            foreach (var player in players)
            {
                for (int i = 0; i < 10; i++)
                {
                    Vector3 randomPos = player.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        15f,
                        Random.Range(-10f, 10f)
                    );
                    Item randomItem = ItemManager.CreateByName(GetRandomResource(), Random.Range(50, 200));
                    randomItem?.Drop(randomPos, Vector3.zero);
                }
            }
            UpdateEventUI("RESOURCE RAIN", 30f);
            Server.Broadcast("CHAOS EVENT: It's raining resources!");
        }

        private string GetRandomResource()
        {
            string[] resources = new[] {
                "wood", "stones", "metal.ore", "metal.refined",
                "sulfur.ore", "sulfur", "charcoal", "cloth",
                "leather", "metal.fragments", "gunpowder",
                "scrap", "crude.oil", "lowgradefuel",
                "explosive.timed", "explosive.satchel",
                "ammo.rifle", "ammo.pistol", "weapon.mod.holosight"
            };
            return resources[Random.Range(0, resources.Length)];
        }
        #endregion

        private void UpdateEventUI(string eventName, float duration)
        {
            Puts($"UpdateEventUI called: {eventName}, duration: {duration}"); // Debug log

            var newEvent = new ActiveEvent
            {
                Name = eventName,
                EndTime = Time.time + duration
            };

            activeEvents.Add(newEvent);
            Puts($"Added new event. Total active events: {activeEvents.Count}"); // Debug log

            // Remove expired events
            activeEvents.RemoveAll(e => Time.time >= e.EndTime);

            // Limit number of simultaneous events
            while (activeEvents.Count > config.MaxSimultaneousEvents)
            {
                activeEvents.RemoveAt(0);
            }

            // Update UI for all players immediately
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    CreateUI(player); // Ensure UI exists
                    UpdatePlayerUI(player);
                }
            }

            // Schedule regular UI updates
            timer.Once(1f, () =>
            {
                StartEventTimer(duration);
            });
        }

        private void StartEventTimer(float duration)
        {
            Timer eventTimer = null;
            eventTimer = timer.Every(1f, () =>
            {
                // Remove expired events
                bool hasExpiredEvents = false;
                activeEvents.RemoveAll(e =>
                {
                    bool expired = Time.time >= e.EndTime;
                    if (expired) hasExpiredEvents = true;
                    return expired;
                });

                // Update UI for all players
                if (hasExpiredEvents || activeEvents.Count > 0)
                {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        if (player != null)
                        {
                            UpdatePlayerUI(player);
                        }
                    }
                }

                // Stop timer if no active events
                if (activeEvents.Count == 0)
                {
                    if (eventTimer != null)
                    {
                        eventTimer.Destroy();
                        eventTimer = null;
                    }
                }
            });
        }

        private void DropRandomResources(Vector3 position)
        {
            string[] resources = {
                "wood", "stones", "metal.ore", "metal.refined",
                "sulfur.ore", "sulfur", "charcoal", "cloth",
                "leather", "metal.fragments", "gunpowder",
                "scrap", "crude.oil", "lowgradefuel",
                "explosive.timed", "explosive.satchel",
                "ammo.rifle", "ammo.pistol", "weapon.mod.holosight"
            };

            for (int i = 0; i < Random.Range(1, 4); i++)
            {
                string resource = resources[Random.Range(0, resources.Length)];
                Item item = ItemManager.CreateByName(resource, Random.Range(10, 100));
                if (item != null)
                {
                    item.Drop(
                        position + new Vector3(Random.Range(-2f, 2f), 1f, Random.Range(-2f, 2f)),
                        Vector3.up * 2f
                    );
                }
            }
        }

        private void TriggerChaosNPCEvent()
        {
            string eventName = "MUTANT HORDE";
            float duration = 300f;

            // Spawn mutated NPCs near each player
            foreach (var player in BasePlayer.activePlayerList)
            {
                int npcCount = Random.Range(3, 8);
                for (int i = 0; i < npcCount; i++)
                {
                    // Get random position around player
                    float angle = Random.Range(0f, 360f);
                    float distance = Random.Range(10f, 30f);
                    Vector3 offset = new Vector3(
                        Mathf.Cos(angle * Mathf.Deg2Rad) * distance,
                        0f,
                        Mathf.Sin(angle * Mathf.Deg2Rad) * distance
                    );
                    
                    Vector3 spawnPos = player.transform.position + offset;
                    spawnPos.y = TerrainMeta.HeightMap.GetHeight(spawnPos);

                    string[] npcPrefabs = {
                        "assets/rust.ai/agents/bear/bear.prefab",
                        "assets/rust.ai/agents/wolf/wolf.prefab",
                        "assets/rust.ai/agents/boar/boar.prefab",
                        "assets/prefabs/npc/scientist/scientist.prefab",
                        "assets/rust.ai/agents/horse/horse.prefab"
                    };

                    var npc = GameManager.server.CreateEntity(
                        npcPrefabs[Random.Range(0, npcPrefabs.Length)],
                        spawnPos
                    ) as BaseNpc;

                    if (npc != null)
                    {
                        npc.Spawn();
                        
                        // Apply modifications to all NPCs
                        var baseCombat = npc as BaseCombatEntity;
                        if (baseCombat != null)
                        {
                            baseCombat.startHealth *= Random.Range(2f, 6f);
                            baseCombat.health = baseCombat.startHealth;
                        }
                        
                        var nav = npc.GetComponent<BaseNavigator>();
                        if (nav != null)
                        {
                            nav.Speed = Mathf.Max(nav.Speed * Random.Range(2f, 5f), 1f);
                        }
                        
                        Puts($"Successfully spawned NPC at {spawnPos}");
                    }
                }
            }

            UpdateEventUI(eventName, duration);
            Server.Broadcast($"CHAOS EVENT: {eventName} - Mutated creatures are hunting you!");
        }

        private void TriggerMindControlEvent()
        {
            string eventName = "MIND CONTROL";
            float duration = 60f;

            foreach (var player in BasePlayer.activePlayerList)
            {
                if (Random.value < 0.5f)
                {
                    // Random movement and actions
                    timer.Every(1f, () => {
                        if (player != null && Time.time < Time.time + duration)
                        {
                            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0, Random.Range(-1f, 1f)).normalized;
                            player.ClientRPCPlayer(null, player, "ForcePositionTo", player.transform.position + randomDir * 2f);
                            
                            if (Random.value < 0.2f)
                            {
                                player.GetComponent<Rigidbody>()?.AddForce(Vector3.up * 5f, ForceMode.Impulse);
                            }
                        }
                    });
                }
            }

            UpdateEventUI(eventName, duration);
            Server.Broadcast($"CHAOS EVENT: {eventName} - You are not in control anymore!");
        }

        void OnEntitySpawned(BaseEntity entity)
        {
            if (entity is BaseNpc npc && Random.value < 0.3f)
            {
                MutateNPC(npc);
            }
        }

        void Unload()
        {
            // Reset all player sizes and modifiers
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player != null)
                {
                    ResetPlayer(player);
                    CuiHelper.DestroyUi(player, UIMain);
                }
            }

            // Clean up NPCs
            foreach (var npc in chaosNPCs.ToList())
            {
                if (npc != null && !npc.IsDestroyed)
                {
                    npc.transform.localScale = Vector3.one;
                }
            }

            chaosNPCs.Clear();
            npcChaos.Clear();
            chaosTimer?.Destroy();
        }

        #region NPC Modifications
        private void MutateNPC(BaseNpc npc)
        {
            if (!npcChaos.ContainsKey(npc))
            {
                npcChaos[npc] = new NpcChaosData
                {
                    IsMutated = true,
                    SizeMultiplier = Random.Range(3f, 8f), // Even larger size range
                    SpeedMultiplier = Random.Range(2f, 5f), // Faster movement
                    DamageMultiplier = Random.Range(2f, 6f), // More damage
                    IsExplosive = Random.value < 0.4f, // More explosive NPCs
                    IsTeleporter = Random.value < 0.4f, // More teleporting NPCs
                    IsResourceDropper = Random.value < 0.4f // More resource dropping NPCs
                };
            }

            var chaosData = npcChaos[npc];
            npc.transform.localScale = Vector3.one * chaosData.SizeMultiplier;
            
            // Apply damage multiplier for NPCs
            var baseCombatEntity = npc as BaseCombatEntity;
            if (baseCombatEntity != null)
            {
                baseCombatEntity.startHealth *= chaosData.DamageMultiplier;
                baseCombatEntity.health *= chaosData.DamageMultiplier;
            }

            // Modify movement speed using BaseNavigator
            var baseNavigator = npc.GetComponent<BaseNavigator>();
            if (baseNavigator != null)
            {
                baseNavigator.Speed = Mathf.Max(baseNavigator.Speed * chaosData.SpeedMultiplier, 1f);
            }

            chaosNPCs.Add(npc);

            // Start chaos behavior timer
            timer.Every(0.5f, () => // More frequent checks for chaos
            {
                if (npc == null || npc.IsDestroyed)
                    return;

                if (chaosData.IsTeleporter && Random.value < 0.1f) // More frequent teleporting
                {
                    // Find nearest player for targeted teleporting
                    BasePlayer nearestPlayer = null;
                    float nearestDistance = float.MaxValue;
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        float distance = Vector3.Distance(npc.transform.position, player.transform.position);
                        if (distance < nearestDistance)
                        {
                            nearestDistance = distance;
                            nearestPlayer = player;
                        }
                    }

                    if (nearestPlayer != null)
                    {
                        Vector3 targetPos = nearestPlayer.transform.position + new Vector3(
                            Random.Range(-10f, 10f),
                            Random.Range(0f, 5f),
                            Random.Range(-10f, 10f)
                        );
                        npc.transform.position = targetPos;
                        Effect.server.Run("assets/bundled/prefabs/fx/gestures/wave.prefab", npc.transform.position);
                    }
                }

                if (chaosData.IsExplosive && Random.value < 0.05f) // More frequent explosions
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_satchel.prefab", npc.transform.position);
                    foreach (var collider in Physics.OverlapSphere(npc.transform.position, 8f)) // Larger explosion radius
                    {
                        var player = collider.GetComponentInParent<BasePlayer>();
                        if (player != null)
                        {
                            player.Hurt(75f * chaosData.DamageMultiplier); // More explosion damage
                        }
                    }
                }

                if (chaosData.IsResourceDropper && Random.value < 0.08f) // More frequent resource drops
                {
                    for (int i = 0; i < Random.Range(2, 6); i++) // Drop more resources
                    {
                        DropRandomResources(npc.transform.position);
                    }
                }

                // Random effects
                if (Random.value < 0.05f) // 5% chance each tick for random effects
                {
                    switch (Random.Range(0, 4))
                    {
                        case 0: // Lightning strike
                            Effect.server.Run("assets/bundled/prefabs/fx/lightning_strike.prefab", npc.transform.position);
                            break;
                        case 1: // Fire effect
                            Effect.server.Run("assets/bundled/prefabs/fx/fire.prefab", npc.transform.position);
                            break;
                        case 2: // Smoke screen
                            Effect.server.Run("assets/bundled/prefabs/fx/smoke_signal_green.prefab", npc.transform.position);
                            break;
                        case 3: // Spawn a random item
                            Item randomItem = ItemManager.CreateByName(GetRandomResource(), Random.Range(50, 200));
                            if (randomItem != null)
                            {
                                randomItem.Drop(npc.transform.position + Vector3.up * 2f, Vector3.up * 5f);
                            }
                            break;
                    }
                }
            });
        }
        #endregion

        #region NPC Hooks
        object OnNpcTarget(BaseNpc npc, BaseEntity target)
        {
            if (!npcChaos.ContainsKey(npc)) return null;
            var chaosData = npcChaos[npc];

            // Random chance to change target
            if (Random.value < 0.2f)
            {
                var players = BasePlayer.activePlayerList;
                if (players.Count > 0)
                {
                    var randomPlayer = players[Random.Range(0, players.Count)];
                    return randomPlayer;
                }
            }

            // Random chance to ignore target
            if (Random.value < 0.1f)
            {
                return false;
            }

            return null;
        }

        void OnNpcDestinationSet(BaseEntity entity)
        {
            var npc = entity as BaseNpc;
            if (npc == null || !npcChaos.ContainsKey(npc)) return;

            var chaosData = npcChaos[npc];

            // Random chance to teleport to nearest player
            if (chaosData.IsTeleporter && Random.value < 0.3f)
            {
                BasePlayer nearestPlayer = null;
                float nearestDistance = float.MaxValue;
                foreach (var player in BasePlayer.activePlayerList)
                {
                    float distance = Vector3.Distance(npc.transform.position, player.transform.position);
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearestPlayer = player;
                    }
                }

                if (nearestPlayer != null)
                {
                    Vector3 targetPos = nearestPlayer.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        Random.Range(0f, 5f),
                        Random.Range(-10f, 10f)
                    );
                    npc.transform.position = targetPos;
                    Effect.server.Run("assets/bundled/prefabs/fx/gestures/wave.prefab", npc.transform.position);
                }
            }

            // Random chance to create chaos at current position
            if (Random.value < 0.2f)
            {
                Vector3 pos = npc.transform.position;
                timer.Once(1f, () =>
                {
                    if (npc == null || npc.IsDestroyed) return;
                    
                    // Create explosion effect
                    Effect.server.Run("assets/bundled/prefabs/fx/explosions/explosion_satchel.prefab", pos);
                    
                    // Spawn random items
                    for (int i = 0; i < Random.Range(2, 6); i++)
                    {
                        Item randomItem = ItemManager.CreateByName(GetRandomResource(), Random.Range(25, 100));
                        if (randomItem != null)
                        {
                            randomItem.Drop(pos + new Vector3(Random.Range(-3f, 3f), 1f, Random.Range(-3f, 3f)), Vector3.up * 3f);
                        }
                    }
                });
            }
        }

        void OnNpcAlert(ScientistNPC scientist)
        {
            if (scientist == null) return;

            // Random chance to trigger chaos event
            if (Random.value < 0.3f)
            {
                // Call for reinforcements
                int npcCount = Random.Range(1, 4);
                for (int i = 0; i < npcCount; i++)
                {
                    Vector3 spawnPos = scientist.transform.position + new Vector3(
                        Random.Range(-10f, 10f),
                        0f,
                        Random.Range(-10f, 10f)
                    );

                    var newNpc = GameManager.server.CreateEntity(
                        "assets/prefabs/npc/scientist/scientist.prefab",
                        spawnPos
                    ) as BaseNpc;

                    if (newNpc != null)
                    {
                        newNpc.Spawn();
                        MutateNPC(newNpc);
                    }
                }

                // Create smoke screen
                Effect.server.Run("assets/bundled/prefabs/fx/smoke_signal_green.prefab", scientist.transform.position);
            }
        }

        void OnNpcConversationStart(NPCTalking npc, BasePlayer player, ConversationData conversation)
        {
            if (Random.value < 0.2f)
            {
                // Trigger random chaos event when talking to NPCs
                TriggerRandomChaosEvent();
                Player.Message(player, "The NPC's words trigger something chaotic!", "Chaos");
            }
        }

        void OnNpcRadioChatter(ScientistNPC scientist)
        {
            if (Random.value < 0.2f)
            {
                // Random chance to call in supply drops during radio chatter
                Vector3 dropPos = scientist.transform.position + new Vector3(
                    Random.Range(-50f, 50f),
                    50f,
                    Random.Range(-50f, 50f)
                );

                GameManager.server.CreateEntity("assets/prefabs/misc/supply drop/supply_drop.prefab", dropPos)?.Spawn();
                Server.Broadcast("A scientist's radio chatter has called in a supply drop!");
            }
        }
        #endregion
    }
} 