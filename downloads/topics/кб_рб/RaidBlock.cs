using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Time = UnityEngine.Time;

namespace Oxide.Plugins
{
    [Info("Raid Block", "BlackWolf", "1.0.0")]
    public class RaidBlock : RustPlugin
    {
        [PluginReference]
        private Plugin CombatBlock, AdvancedHitBar;

        #region Fields
        private const string PermissionAdmin = "raidblock.admin";
        private const float RAID_BLOCK_TIME = 120f; // 2 minutes in seconds
        private Dictionary<ulong, Timer> raidBlockTimers = new();
        private Dictionary<ulong, string> activeCui = new();
        private Dictionary<ulong, float> blockStartTimes = new();

        // Configuration
        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty("Enable sound effects")]
            public bool EnableSoundEffects = true;

            [JsonProperty("Sound effect volume")]
            public float SoundVolume = 1f;

            [JsonProperty("Show block message to nearby players")]
            public bool ShowToNearbyPlayers = true;

            [JsonProperty("Nearby players notification range")]
            public float NotificationRange = 30f;

            [JsonProperty("UI Color (hex)")]
            public string UiColor = "#FF0000";

            [JsonProperty("UI Position")]
            public UiPosition UiPos = new UiPosition { AnchorMin = "0.8 0.93", AnchorMax = "0.99 0.98" };
        }

        private class UiPosition
        {
            public string AnchorMin;
            public string AnchorMax;
        }

        // Lists of items that trigger raid block
        private readonly HashSet<string> raidItems = new HashSet<string>
        {
            "explosive.timed",        // C4
            "ammo.rocket.basic",     // Regular Rocket
            "ammo.rocket.fire",      // Incendiary Rocket
            "ammo.rocket.hv",        // High Velocity Rocket
            "ammo.rocket.smoke",     // Smoke Rocket
            "grenade.f1",            // F1 Grenade
            "grenade.beancan",       // Beancan Grenade
            "grenade.satchel",       // Satchel Charge
            "ammo.rifle.explosive",  // Explosive Ammo
            "ammo.pistol.fire",      // Fire Pistol Ammo
            "surveycharge",          // Survey Charge
            "explosive.underwater",   // Underwater Explosive
            "ammo.grenadelauncher.he", // HE Grenades
            "ammo.grenadelauncher.buckshot", // Buckshot Grenades
            "grenade.molotov",       // Molotov
            "arrow.fire",            // Fire Arrow
            "ammo.rifle.incendiary", // Incendiary Rifle Ammo
            "ammo.pistol.fire",      // Incendiary Pistol Ammo
            "ammo.shotgun.fire"      // Incendiary Shotgun Ammo
        };

        private readonly HashSet<string> raidTools = new HashSet<string>
        {
            "hammer.salvaged",
            "axe.salvaged",
            "pickaxe",
            "jackhammer",
            "chainsaw"
        };
        #endregion

        #region Initialization
        private void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            cmd.AddChatCommand("raidblock", this, CmdRaidBlock);
            LoadConfig();
        }

        protected override void LoadDefaultConfig()
        {
            Config.WriteObject(new ConfigData(), true);
        }

        private void LoadConfig()
        {
            config = Config.ReadObject<ConfigData>();
            if (config == null) LoadDefaultConfig();
        }

        private void OnServerInitialized()
        {
            // Clean up any existing data
            foreach (var timer in raidBlockTimers.Values)
                timer?.Destroy();
            raidBlockTimers.Clear();
            activeCui.Clear();
        }

        private void Unload()
        {
            foreach (var timer in raidBlockTimers.Values)
                timer?.Destroy();
            
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }
        #endregion

        #region API Methods
        // API methods for other plugins to use
        public bool IsPlayerBlocked(ulong userId) => IsRaidBlocked(userId);
        public float GetPlayerRaidTimeRemaining(ulong userId) => GetRemainingBlockTime(userId);
        #endregion

        #region Combat Events
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                // Get the attacker
                BasePlayer attacker = info?.InitiatorPlayer;
                if (attacker == null) return;

                // Only handle building/raid targets
                bool isRaidableEntity = entity is BuildingBlock || 
                                      entity is Door || 
                                      entity is AutoTurret || 
                                      entity is StorageContainer ||
                                      entity is BuildingPrivlidge ||
                                      entity is BaseOven;
                
                if (!isRaidableEntity) return;

                // Check if it's raid damage
                bool isRaidDamage = false;

                // Check weapon/tool
                Item weapon = attacker.GetActiveItem();
                if (weapon != null)
                {
                    // Check if weapon is in raid items list
                    if (raidItems.Contains(weapon.info.shortname))
                    {
                        isRaidDamage = true;
                    }
                    // Check if tool is in raid tools list
                    else if (raidTools.Contains(weapon.info.shortname))
                    {
                        isRaidDamage = true;
                    }
                }

                // Check damage types
                if (!isRaidDamage && info.damageTypes != null)
                {
                    // Check for explosive damage
                    if (info.damageTypes.Has(Rust.DamageType.Explosion))
                    {
                        isRaidDamage = true;
                    }
                    // Check for fire damage from molotovs/fire arrows
                    else if (info.damageTypes.Has(Rust.DamageType.Heat))
                    {
                        isRaidDamage = true;
                    }
                }

                // Check projectile
                if (!isRaidDamage && info.ProjectilePrefab != null)
                {
                    string projectileName = info.ProjectilePrefab.name.ToLower();
                    if (projectileName.Contains("explosive") || 
                        projectileName.Contains("rocket") || 
                        projectileName.Contains("grenade") ||
                        projectileName.Contains("molotov") ||
                        projectileName.Contains("fire"))
                    {
                        isRaidDamage = true;
                    }
                }

                if (isRaidDamage)
                {
                    ApplyRaidBlock(attacker);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnEntityTakeDamage: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            // Skip check for admins
            if (permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                return null;

            if (IsRaidBlocked(player.userID))
            {
                SendReply(player, "You cannot use commands while raid blocked!");
                return false;
            }
            return null;
        }

        // Handle thrown explosives (Satchels, Beancans, etc.)
        private void OnExplosiveThrown(BasePlayer player, BaseEntity entity, ThrownWeapon weapon)
        {
            try
            {
                if (player == null || entity == null) return;

                string itemName = entity.ShortPrefabName;
                if (raidItems.Contains(itemName))
                {
                    ApplyRaidBlock(player);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnExplosiveThrown: {ex.Message}");
            }
        }

        // Handle C4 placement
        private void OnExplosiveDropped(BasePlayer player, BaseEntity entity, ThrownWeapon weapon)
        {
            try
            {
                if (player == null || entity == null) return;

                if (entity is TimedExplosive)
                {
                    ApplyRaidBlock(player);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in OnExplosiveDropped: {ex.Message}");
            }
        }
        #endregion

        #region Raid Block Methods
        private void ApplyRaidBlock(BasePlayer player)
        {
            try
            {
                // Skip if player has admin permission
                if (permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                    return;

                ulong userId = player.userID;

                // Check if already blocked
                if (IsRaidBlocked(userId))
                {
                    RefreshRaidBlock(player);
                    return;
                }

                // Initialize timer
                blockStartTimes[userId] = Time.realtimeSinceStartup;
                raidBlockTimers[userId] = timer.Once(RAID_BLOCK_TIME, () => RemoveRaidBlock(player));

                // Show UI
                CreateUI(player, RAID_BLOCK_TIME);

                // Notify player
                SendReply(player, $"You are now raid blocked for {RAID_BLOCK_TIME} seconds!");

                // Notify nearby players
                if (config.ShowToNearbyPlayers)
                    NotifyNearbyPlayers(player);

                // Play sound
                if (config.EnableSoundEffects)
                    player.Command("note.raid", config.SoundVolume, 1, 0);
            }
            catch (Exception ex)
            {
                PrintError($"Error in ApplyRaidBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void RefreshRaidBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Reset timer
                raidBlockTimers[userId]?.Destroy();
                blockStartTimes[userId] = Time.realtimeSinceStartup;
                raidBlockTimers[userId] = timer.Once(RAID_BLOCK_TIME, () => RemoveRaidBlock(player));

                // Update UI
                CreateUI(player, RAID_BLOCK_TIME);
            }
            catch (Exception ex)
            {
                PrintError($"Error in RefreshRaidBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void RemoveRaidBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Clean up
                if (raidBlockTimers.ContainsKey(userId))
                {
                    raidBlockTimers[userId]?.Destroy();
                    raidBlockTimers.Remove(userId);
                }
                
                blockStartTimes.Remove(userId);
                DestroyUI(player);

                // Notify player
                if (player != null && player.IsConnected)
                {
                    SendReply(player, "Your raid block has expired!");
                    if (config.EnableSoundEffects)
                        player.Command("note.raid", config.SoundVolume, 0.5f, 0);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in RemoveRaidBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private bool IsRaidBlocked(ulong userId)
        {
            // Check for admin permission override
            var player = BasePlayer.FindByID(userId);
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                return false;

            return raidBlockTimers.ContainsKey(userId) && raidBlockTimers[userId] != null;
        }

        private float GetRemainingBlockTime(ulong userId)
        {
            if (!blockStartTimes.ContainsKey(userId)) return 0f;
            float elapsed = Time.realtimeSinceStartup - blockStartTimes[userId];
            return Math.Max(0f, RAID_BLOCK_TIME - elapsed);
        }

        private void NotifyNearbyPlayers(BasePlayer attacker)
        {
            try
            {
                var nearbyPlayers = new List<BasePlayer>();
                var entities = new List<BaseEntity>();
                Vis.Entities(attacker.transform.position, config.NotificationRange, entities);
                
                foreach (var entity in entities)
                {
                    if (entity is BasePlayer player && player != attacker && player.IsConnected)
                        nearbyPlayers.Add(player);
                }

                foreach (var player in nearbyPlayers)
                    SendReply(player, $"{attacker.displayName} has started raiding nearby!");
            }
            catch (Exception ex)
            {
                PrintError($"Error in NotifyNearbyPlayers: {ex.Message}");
            }
        }
        #endregion

        #region UI Methods
        private void CreateUI(BasePlayer player, float duration)
        {
            if (player == null || !player.IsConnected || player.IsDead()) return;
            
            try
            {
                DestroyUI(player);

                var elements = new CuiElementContainer();

                string panelName = player.UserIDString + "_RaidBlock_UI";

                // Main panel with background
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.8" },
                    RectTransform = { AnchorMin = config.UiPos.AnchorMin, AnchorMax = config.UiPos.AnchorMax }
                }, "Overlay", panelName);

                // Add colored bar on the left
                elements.Add(new CuiPanel
                {
                    Image = { Color = config.UiColor },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.02 1" }
                }, panelName);

                // Block text with icon
                elements.Add(new CuiLabel
                {
                    Text = 
                    {
                        Text = "💣 RAID BLOCK",
                        FontSize = 12,
                        Font = "robotocondensed-bold.ttf",
                        Color = config.UiColor,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform = { AnchorMin = "0.05 0.5", AnchorMax = "0.6 1" }
                }, panelName);

                string timerName = player.UserIDString + "_RaidBlock_Timer";

                // Timer text
                elements.Add(new CuiLabel
                {
                    Text = 
                    {
                        Text = $"{duration:F0}s",
                        FontSize = 12,
                        Font = "robotocondensed-bold.ttf",
                        Color = "1 1 1 1",
                        Align = TextAnchor.MiddleRight
                    },
                    RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.95 1" }
                }, panelName, timerName);

                try
                {
                    // Store and send UI
                    var json = CuiHelper.ToJson(elements);
                    activeCui[player.userID] = json;
                    CuiHelper.AddUi(player, json);

                    // Start timer updates
                    timer.Every(1f, () =>
                    {
                        if (player == null || !player.IsConnected || !IsRaidBlocked(player.userID))
                        {
                            DestroyUI(player);
                            return;
                        }

                        float remaining = GetRemainingBlockTime(player.userID);
                        UpdateTimerText(player, remaining);
                    });
                }
                catch (Exception ex)
                {
                    PrintError($"Error adding UI for {player?.displayName}: {ex.Message}");
                    DestroyUI(player); // Clean up on error
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error creating UI for {player?.displayName}: {ex.Message}\nStack: {ex.StackTrace}");
                DestroyUI(player); // Clean up on error
            }
        }

        private void UpdateTimerText(BasePlayer player, float remaining)
        {
            if (player == null || !player.IsConnected || player.IsDead()) return;

            try
            {
                string timerName = player.UserIDString + "_RaidBlock_Timer";
                string panelName = player.UserIDString + "_RaidBlock_UI";

                CuiHelper.DestroyUi(player, timerName);

                var elements = new CuiElementContainer();
                elements.Add(new CuiLabel
                {
                    Text = 
                    {
                        Text = $"{remaining:F0}s",
                        FontSize = 12,
                        Font = "robotocondensed-bold.ttf",
                        Color = remaining <= 10 ? "1 0.3 0.3 1" : "1 1 1 1", // Red when < 10s
                        Align = TextAnchor.MiddleRight
                    },
                    RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.95 1" }
                }, panelName, timerName);

                CuiHelper.AddUi(player, CuiHelper.ToJson(elements));
            }
            catch (Exception ex)
            {
                PrintError($"Error updating timer for {player?.displayName}: {ex.Message}");
                DestroyUI(player); // Clean up on error
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            if (player == null) return;
            
            try
            {
                string panelName = player.UserIDString + "_RaidBlock_UI";
                string timerName = player.UserIDString + "_RaidBlock_Timer";

                CuiHelper.DestroyUi(player, timerName);
                CuiHelper.DestroyUi(player, panelName);

                if (activeCui.ContainsKey(player.userID))
                {
                    activeCui.Remove(player.userID);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error destroying UI for {player?.displayName}: {ex.Message}");
            }
        }
        #endregion

        #region Commands
        private void CmdRaidBlock(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                SendReply(player, "You don't have permission to use this command!");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Usage: /raidblock <check/clear> [player name]");
                return;
            }

            switch (args[0].ToLower())
            {
                case "check":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /raidblock check <player name>");
                        return;
                    }
                    var targetPlayer = BasePlayer.Find(args[1]);
                    if (targetPlayer == null)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    SendReply(player, IsRaidBlocked(targetPlayer.userID) 
                        ? $"{targetPlayer.displayName} is raid blocked!" 
                        : $"{targetPlayer.displayName} is not raid blocked.");
                    break;

                case "clear":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /raidblock clear <player name>");
                        return;
                    }
                    var clearPlayer = BasePlayer.Find(args[1]);
                    if (clearPlayer == null)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    if (IsRaidBlocked(clearPlayer.userID))
                    {
                        RemoveRaidBlock(clearPlayer);
                        SendReply(player, $"Removed raid block from {clearPlayer.displayName}");
                    }
                    else
                    {
                        SendReply(player, $"{clearPlayer.displayName} is not raid blocked!");
                    }
                    break;

                default:
                    SendReply(player, "Usage: /raidblock <check/clear> [player name]");
                    break;
            }
        }
        #endregion
    }
} 