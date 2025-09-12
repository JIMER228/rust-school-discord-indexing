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
    [Info("Combat Block", "BlackWolf", "1.0.0")]
    public class CombatBlock : RustPlugin
    {
        [PluginReference]
        private Plugin RaidBlock, AdvancedHitBar;

        #region Fields
        private const string PermissionAdmin = "combatblock.admin";
        private const float COMBAT_BLOCK_TIME = 45f; // Changed from 120 to 45 seconds
        private Dictionary<ulong, Timer> combatBlockTimers = new();
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
            public string UiColor = "#FF6600";

            [JsonProperty("UI Position")]
            public UiPosition UiPos = new UiPosition { AnchorMin = "0.8 0.88", AnchorMax = "0.99 0.93" };
        }

        private class UiPosition
        {
            public string AnchorMin;
            public string AnchorMax;
        }
        #endregion

        #region API Methods
        // API methods for other plugins to use
        public bool IsPlayerCombatBlocked(ulong userId) => IsCombatBlocked(userId);
        public float GetPlayerCombatTimeRemaining(ulong userId) => GetRemainingBlockTime(userId);
        #endregion

        #region Initialization
        private void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            cmd.AddChatCommand("combatblock", this, CmdCombatBlock);
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
            foreach (var timer in combatBlockTimers.Values)
                timer?.Destroy();
            combatBlockTimers.Clear();
            activeCui.Clear();
        }

        private void Unload()
        {
            foreach (var timer in combatBlockTimers.Values)
                timer?.Destroy();
            
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }
        #endregion

        #region Combat Events
        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            try
            {
                // Get the attacker and victim
                BasePlayer attacker = info?.InitiatorPlayer;
                BasePlayer victim = entity as BasePlayer;

                // Skip if no attacker or victim
                if (attacker == null && victim == null) return;

                // Skip if it's a building/raid target
                if (entity is BuildingBlock || 
                    entity is Door || 
                    entity is AutoTurret || 
                    entity is StorageContainer ||
                    entity is BuildingPrivlidge ||
                    entity is BaseOven ||
                    entity is LootContainer) return;

                // Apply combat block to attacker if they exist
                if (attacker != null)
                {
                    // Check if already raid blocked
                    object isRaidBlocked = RaidBlock?.Call("IsPlayerBlocked", attacker.userID);
                    if (isRaidBlocked is bool && (bool)isRaidBlocked) return;

                    // Check weapon and damage types
                    bool shouldBlock = false;
                    Item weapon = attacker.GetActiveItem();

                    if (weapon != null)
                    {
                        // Check for explosive ammo
                        var ammoType = weapon.GetHeldEntity()?.GetComponent<BaseProjectile>()?.primaryMagazine?.ammoType;
                        if (ammoType != null && !ammoType.shortname.Contains("explosive"))
                        {
                            shouldBlock = true;
                        }
                        // Check other combat weapons
                        else if (!weapon.info.shortname.Contains("explosive") &&
                            !weapon.info.shortname.Contains("rocket") &&
                            !weapon.info.shortname.Contains("grenade") &&
                            !weapon.info.shortname.Contains("satchel") &&
                            !weapon.info.shortname.Contains("c4"))
                        {
                            shouldBlock = true;
                        }
                    }

                    // Check damage types
                    if (info.damageTypes != null)
                    {
                        bool isCombatDamage = info.damageTypes.Has(Rust.DamageType.Bullet) ||
                                            info.damageTypes.Has(Rust.DamageType.Blunt) ||
                                            info.damageTypes.Has(Rust.DamageType.Slash) ||
                                            info.damageTypes.Has(Rust.DamageType.Stab);

                        if (isCombatDamage)
                        {
                            shouldBlock = true;
                        }
                    }

                    if (shouldBlock)
                    {
                        ApplyCombatBlock(attacker);
                    }
                }

                // Apply combat block to victim if they exist and took damage
                if (victim != null && info.damageTypes.Total() > 0)
                {
                    // Check if already raid blocked
                    object isRaidBlocked = RaidBlock?.Call("IsPlayerBlocked", victim.userID);
                    if (isRaidBlocked is bool && (bool)isRaidBlocked) return;

                    ApplyCombatBlock(victim);
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

            if (IsCombatBlocked(player.userID))
            {
                SendReply(player, "You cannot use commands while combat blocked!");
                return false;
            }
            return null;
        }
        #endregion

        #region Combat Block Methods
        private void ApplyCombatBlock(BasePlayer player)
        {
            try
            {
                // Skip if player has admin permission
                if (permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                    return;

                ulong userId = player.userID;

                // Check if already blocked
                if (IsCombatBlocked(userId))
                {
                    RefreshCombatBlock(player);
                    return;
                }

                // Initialize timer
                blockStartTimes[userId] = Time.realtimeSinceStartup;
                combatBlockTimers[userId] = timer.Once(COMBAT_BLOCK_TIME, () => RemoveCombatBlock(player));

                // Show UI
                CreateUI(player, COMBAT_BLOCK_TIME);

                // Notify player
                SendReply(player, $"You are now combat blocked for {COMBAT_BLOCK_TIME} seconds!");

                // Notify nearby players
                if (config.ShowToNearbyPlayers)
                    NotifyNearbyPlayers(player);

                // Play sound
                if (config.EnableSoundEffects)
                    player.Command("note.combat", config.SoundVolume, 1, 0);
            }
            catch (Exception ex)
            {
                PrintError($"Error in ApplyCombatBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void RefreshCombatBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Reset timer
                combatBlockTimers[userId]?.Destroy();
                blockStartTimes[userId] = Time.realtimeSinceStartup;
                combatBlockTimers[userId] = timer.Once(COMBAT_BLOCK_TIME, () => RemoveCombatBlock(player));

                // Update UI
                CreateUI(player, COMBAT_BLOCK_TIME);
            }
            catch (Exception ex)
            {
                PrintError($"Error in RefreshCombatBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void RemoveCombatBlock(BasePlayer player)
        {
            try
            {
                ulong userId = player.userID;

                // Clean up
                if (combatBlockTimers.ContainsKey(userId))
                {
                    combatBlockTimers[userId]?.Destroy();
                    combatBlockTimers.Remove(userId);
                }
                
                blockStartTimes.Remove(userId);
                DestroyUI(player);

                // Notify player
                if (player != null && player.IsConnected)
                {
                    SendReply(player, "Your combat block has expired!");
                    if (config.EnableSoundEffects)
                        player.Command("note.combat", config.SoundVolume, 0.5f, 0);
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error in RemoveCombatBlock: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private bool IsCombatBlocked(ulong userId)
        {
            // Check for admin permission override
            var player = BasePlayer.FindByID(userId);
            if (player != null && permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                return false;

            return combatBlockTimers.ContainsKey(userId) && combatBlockTimers[userId] != null;
        }

        private float GetRemainingBlockTime(ulong userId)
        {
            if (!blockStartTimes.ContainsKey(userId)) return 0f;
            float elapsed = Time.realtimeSinceStartup - blockStartTimes[userId];
            return Math.Max(0f, COMBAT_BLOCK_TIME - elapsed);
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
                    SendReply(player, $"{attacker.displayName} has started combat nearby!");
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
            if (player == null || !player.IsConnected) return;
            
            try
            {
                DestroyUI(player);

                var elements = new CuiElementContainer();

                // Main panel with background
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0.8" },
                    RectTransform = { AnchorMin = config.UiPos.AnchorMin, AnchorMax = config.UiPos.AnchorMax }
                }, "Hud", "CombatBlock_UI");

                // Add colored bar on the left
                elements.Add(new CuiPanel
                {
                    Image = { Color = config.UiColor },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.02 1" }
                }, "CombatBlock_UI");

                // Block text with icon
                elements.Add(new CuiLabel
                {
                    Text = 
                    {
                        Text = "⚔ COMBAT BLOCK",
                        FontSize = 12,
                        Font = "robotocondensed-bold.ttf",
                        Color = config.UiColor,
                        Align = TextAnchor.MiddleLeft
                    },
                    RectTransform = { AnchorMin = "0.05 0.5", AnchorMax = "0.6 1" }
                }, "CombatBlock_UI");

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
                }, "CombatBlock_UI", "CombatBlock_Timer");

                // Store and send UI
                var json = CuiHelper.ToJson(elements);
                activeCui[player.userID] = json;
                CuiHelper.AddUi(player, json);

                // Start timer updates
                timer.Every(1f, () =>
                {
                    if (player == null || !player.IsConnected || !IsCombatBlocked(player.userID))
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
                PrintError($"Error creating UI for {player.displayName}: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void UpdateTimerText(BasePlayer player, float remaining)
        {
            if (player == null || !player.IsConnected) return;

            try
            {
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
                }, "CombatBlock_UI", "CombatBlock_Timer");

                CuiHelper.DestroyUi(player, "CombatBlock_Timer");
                CuiHelper.AddUi(player, CuiHelper.ToJson(elements));
            }
            catch (Exception ex)
            {
                PrintError($"Error updating timer for {player.displayName}: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            if (activeCui.ContainsKey(player.userID))
            {
                CuiHelper.DestroyUi(player, "CombatBlock_UI");
                activeCui.Remove(player.userID);
            }
        }
        #endregion

        #region Commands
        private void CmdCombatBlock(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
            {
                SendReply(player, "You don't have permission to use this command!");
                return;
            }

            if (args.Length == 0)
            {
                SendReply(player, "Usage: /combatblock <check/clear> [player name]");
                return;
            }

            switch (args[0].ToLower())
            {
                case "check":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /combatblock check <player name>");
                        return;
                    }
                    var targetPlayer = BasePlayer.Find(args[1]);
                    if (targetPlayer == null)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    SendReply(player, IsCombatBlocked(targetPlayer.userID) 
                        ? $"{targetPlayer.displayName} is combat blocked!" 
                        : $"{targetPlayer.displayName} is not combat blocked.");
                    break;

                case "clear":
                    if (args.Length < 2)
                    {
                        SendReply(player, "Usage: /combatblock clear <player name>");
                        return;
                    }
                    var clearPlayer = BasePlayer.Find(args[1]);
                    if (clearPlayer == null)
                    {
                        SendReply(player, "Player not found!");
                        return;
                    }
                    if (IsCombatBlocked(clearPlayer.userID))
                    {
                        RemoveCombatBlock(clearPlayer);
                        SendReply(player, $"Removed combat block from {clearPlayer.displayName}");
                    }
                    else
                    {
                        SendReply(player, $"{clearPlayer.displayName} is not combat blocked!");
                    }
                    break;

                default:
                    SendReply(player, "Usage: /combatblock <check/clear> [player name]");
                    break;
            }
        }
        #endregion
    }
} 