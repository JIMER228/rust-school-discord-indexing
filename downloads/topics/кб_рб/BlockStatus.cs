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
    [Info("Block Status", "BlackWolf", "1.0.0")]
    public class BlockStatus : RustPlugin
    {
        [PluginReference]
        private Plugin CombatBlock, RaidBlock, ImageLibrary;

        #region Fields
        private const string PermissionAdmin = "blockstatus.admin";
        private const string PermissionHide = "blockstatus.hide";
        
        // UI Elements
        private Dictionary<ulong, string> activeUi = new Dictionary<ulong, string>();
        private Dictionary<ulong, Timer> uiUpdateTimers = new Dictionary<ulong, Timer>();
        
        // Configuration
        private ConfigData config;

        private class ConfigData
        {
            [JsonProperty("UI Position")]
            public UiPosition UiPos = new UiPosition { AnchorMin = "0.8 0.8", AnchorMax = "0.99 0.98" };
            
            [JsonProperty("Combat Block Settings")]
            public BlockSettings CombatBlockSettings = new BlockSettings 
            { 
                Enabled = true,
                Icon = "⚔",
                Title = "COMBAT BLOCK",
                Color = "#FF6600",
                Position = 0
            };
            
            [JsonProperty("Raid Block Settings")]
            public BlockSettings RaidBlockSettings = new BlockSettings 
            { 
                Enabled = true,
                Icon = "💣",
                Title = "RAID BLOCK",
                Color = "#FF0000",
                Position = 1
            };
            
            [JsonProperty("Background Color")]
            public string BackgroundColor = "0 0 0 0.8";
            
            [JsonProperty("Text Color")]
            public string TextColor = "1 1 1 1";
            
            [JsonProperty("Update Interval (seconds)")]
            public float UpdateInterval = 1f;
            
            [JsonProperty("Spacing Between Blocks")]
            public float Spacing = 0.05f;
            
            [JsonProperty("Show Block Time Remaining")]
            public bool ShowTimeRemaining = true;
        }

        private class BlockSettings
        {
            [JsonProperty("Enable this block type")]
            public bool Enabled = true;
            
            [JsonProperty("Icon")]
            public string Icon = "⚔";
            
            [JsonProperty("Title")]
            public string Title = "BLOCK";
            
            [JsonProperty("Color (hex)")]
            public string Color = "#FFFFFF";
            
            [JsonProperty("Position (0 = top, 1 = middle, etc.)")]
            public int Position = 0;
        }

        private class UiPosition
        {
            public string AnchorMin;
            public string AnchorMax;
        }
        
        // Block status tracking
        private class PlayerBlockStatus
        {
            public bool IsCombatBlocked;
            public float CombatTimeRemaining;
            public bool IsRaidBlocked;
            public float RaidTimeRemaining;
        }
        
        private Dictionary<ulong, PlayerBlockStatus> playerBlockStatus = new Dictionary<ulong, PlayerBlockStatus>();
        #endregion

        #region Initialization
        private void Init()
        {
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionHide, this);
            cmd.AddChatCommand("blockstatus", this, CmdBlockStatus);
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
            // Check for required plugins
            if (CombatBlock == null || RaidBlock == null)
            {
                PrintError("CombatBlock or RaidBlock plugin not found! BlockStatus requires both plugins to function.");
                return;
            }
            
            // Clean up any existing data
            foreach (var timer in uiUpdateTimers.Values)
                timer?.Destroy();
            uiUpdateTimers.Clear();
            activeUi.Clear();
            playerBlockStatus.Clear();
            
            // Start periodic updates
            timer.Every(config.UpdateInterval, UpdateAllPlayers);
        }

        private void Unload()
        {
            foreach (var timer in uiUpdateTimers.Values)
                timer?.Destroy();
            
            foreach (var player in BasePlayer.activePlayerList)
                DestroyUI(player);
        }
        #endregion

        #region Block Status Methods
        private void UpdateAllPlayers()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                if (player == null || !player.IsConnected) continue;
                UpdatePlayerBlockStatus(player);
            }
        }
        
        private void UpdatePlayerBlockStatus(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            // Skip if player has hide permission
            if (permission.UserHasPermission(player.UserIDString, PermissionHide))
            {
                DestroyUI(player);
                return;
            }
            
            // Get current block status
            bool isCombatBlocked = false;
            float combatTimeRemaining = 0f;
            bool isRaidBlocked = false;
            float raidTimeRemaining = 0f;
            
            // Check CombatBlock status
            if (config.CombatBlockSettings.Enabled && CombatBlock != null)
            {
                object combatResult = CombatBlock.Call("IsPlayerCombatBlocked", player.userID);
                if (combatResult is bool && (bool)combatResult)
                {
                    isCombatBlocked = true;
                    object timeResult = CombatBlock.Call("GetPlayerCombatTimeRemaining", player.userID);
                    if (timeResult is float)
                        combatTimeRemaining = (float)timeResult;
                }
            }
            
            // Check RaidBlock status
            if (config.RaidBlockSettings.Enabled && RaidBlock != null)
            {
                object raidResult = RaidBlock.Call("IsPlayerBlocked", player.userID);
                if (raidResult is bool && (bool)raidResult)
                {
                    isRaidBlocked = true;
                    object timeResult = RaidBlock.Call("GetPlayerRaidTimeRemaining", player.userID);
                    if (timeResult is float)
                        raidTimeRemaining = (float)timeResult;
                }
            }
            
            // Update player status
            var status = playerBlockStatus.ContainsKey(player.userID) 
                ? playerBlockStatus[player.userID] 
                : new PlayerBlockStatus();
                
            status.IsCombatBlocked = isCombatBlocked;
            status.CombatTimeRemaining = combatTimeRemaining;
            status.IsRaidBlocked = isRaidBlocked;
            status.RaidTimeRemaining = raidTimeRemaining;
            
            playerBlockStatus[player.userID] = status;
            
            // Update UI
            if (isCombatBlocked || isRaidBlocked)
                CreateUI(player);
            else
                DestroyUI(player);
        }
        #endregion

        #region UI Methods
        private void CreateUI(BasePlayer player)
        {
            if (player == null || !player.IsConnected) return;
            
            try
            {
                DestroyUI(player);
                
                var status = playerBlockStatus.ContainsKey(player.userID) 
                    ? playerBlockStatus[player.userID] 
                    : new PlayerBlockStatus();
                
                // Skip if no active blocks
                if (!status.IsCombatBlocked && !status.IsRaidBlocked)
                    return;
                
                var elements = new CuiElementContainer();
                
                // Create parent panel
                elements.Add(new CuiPanel
                {
                    Image = { Color = "0 0 0 0" },
                    RectTransform = { AnchorMin = config.UiPos.AnchorMin, AnchorMax = config.UiPos.AnchorMax }
                }, "Hud", "BlockStatus_UI");
                
                // Add block panels
                List<BlockInfo> activeBlocks = new List<BlockInfo>();
                
                if (status.IsCombatBlocked && config.CombatBlockSettings.Enabled)
                {
                    activeBlocks.Add(new BlockInfo
                    {
                        Type = "Combat",
                        Settings = config.CombatBlockSettings,
                        TimeRemaining = status.CombatTimeRemaining
                    });
                }
                
                if (status.IsRaidBlocked && config.RaidBlockSettings.Enabled)
                {
                    activeBlocks.Add(new BlockInfo
                    {
                        Type = "Raid",
                        Settings = config.RaidBlockSettings,
                        TimeRemaining = status.RaidTimeRemaining
                    });
                }
                
                // Sort blocks by position
                activeBlocks.Sort((a, b) => a.Settings.Position.CompareTo(b.Settings.Position));
                
                // Calculate panel heights and positions
                float panelHeight = 1.0f / activeBlocks.Count;
                float spacing = config.Spacing / activeBlocks.Count;
                
                for (int i = 0; i < activeBlocks.Count; i++)
                {
                    var block = activeBlocks[i];
                    float yMin = 1f - ((i + 1) * panelHeight) + (spacing * i);
                    float yMax = 1f - (i * panelHeight) - (spacing * i);
                    
                    string panelName = $"BlockStatus_{block.Type}";
                    string timerName = $"BlockStatus_{block.Type}_Timer";
                    
                    // Add block panel
                    elements.Add(new CuiPanel
                    {
                        Image = { Color = config.BackgroundColor },
                        RectTransform = { AnchorMin = $"0 {yMin}", AnchorMax = $"1 {yMax}" }
                    }, "BlockStatus_UI", panelName);
                    
                    // Add colored bar on the left
                    elements.Add(new CuiPanel
                    {
                        Image = { Color = HexToRustColor(block.Settings.Color) },
                        RectTransform = { AnchorMin = "0 0", AnchorMax = "0.02 1" }
                    }, panelName);
                    
                    // Block text with icon
                    elements.Add(new CuiLabel
                    {
                        Text = 
                        {
                            Text = $"{block.Settings.Icon} {block.Settings.Title}",
                            FontSize = 12,
                            Font = "robotocondensed-bold.ttf",
                            Color = HexToRustColor(block.Settings.Color),
                            Align = TextAnchor.MiddleLeft
                        },
                        RectTransform = { AnchorMin = "0.05 0.5", AnchorMax = "0.6 1" }
                    }, panelName);
                    
                    // Timer text
                    if (config.ShowTimeRemaining)
                    {
                        elements.Add(new CuiLabel
                        {
                            Text = 
                            {
                                Text = $"{block.TimeRemaining:F0}s",
                                FontSize = 12,
                                Font = "robotocondensed-bold.ttf",
                                Color = config.TextColor,
                                Align = TextAnchor.MiddleRight
                            },
                            RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.95 1" }
                        }, panelName, timerName);
                    }
                }
                
                // Store and send UI
                var json = CuiHelper.ToJson(elements);
                activeUi[player.userID] = json;
                CuiHelper.AddUi(player, json);
                
                // Start timer updates if needed
                if (config.ShowTimeRemaining)
                {
                    if (uiUpdateTimers.ContainsKey(player.userID))
                        uiUpdateTimers[player.userID]?.Destroy();
                    
                    uiUpdateTimers[player.userID] = timer.Every(1f, () => UpdateTimerText(player));
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error creating UI for {player.displayName}: {ex.Message}\nStack: {ex.StackTrace}");
            }
        }
        
        private void UpdateTimerText(BasePlayer player)
        {
            if (player == null || !player.IsConnected) 
            {
                if (uiUpdateTimers.ContainsKey(player.userID))
                {
                    uiUpdateTimers[player.userID]?.Destroy();
                    uiUpdateTimers.Remove(player.userID);
                }
                return;
            }
            
            try
            {
                var status = playerBlockStatus.ContainsKey(player.userID) 
                    ? playerBlockStatus[player.userID] 
                    : new PlayerBlockStatus();
                
                // Update combat block timer
                if (status.IsCombatBlocked && config.CombatBlockSettings.Enabled)
                {
                    object timeResult = CombatBlock.Call("GetPlayerCombatTimeRemaining", player.userID);
                    if (timeResult is float)
                    {
                        float remaining = (float)timeResult;
                        status.CombatTimeRemaining = remaining;
                        
                        var elements = new CuiElementContainer();
                        elements.Add(new CuiLabel
                        {
                            Text = 
                            {
                                Text = $"{remaining:F0}s",
                                FontSize = 12,
                                Font = "robotocondensed-bold.ttf",
                                Color = remaining <= 10 ? "1 0.3 0.3 1" : config.TextColor,
                                Align = TextAnchor.MiddleRight
                            },
                            RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.95 1" }
                        }, "BlockStatus_Combat", "BlockStatus_Combat_Timer");
                        
                        CuiHelper.DestroyUi(player, "BlockStatus_Combat_Timer");
                        CuiHelper.AddUi(player, CuiHelper.ToJson(elements));
                    }
                }
                
                // Update raid block timer
                if (status.IsRaidBlocked && config.RaidBlockSettings.Enabled)
                {
                    object timeResult = RaidBlock.Call("GetPlayerRaidTimeRemaining", player.userID);
                    if (timeResult is float)
                    {
                        float remaining = (float)timeResult;
                        status.RaidTimeRemaining = remaining;
                        
                        var elements = new CuiElementContainer();
                        elements.Add(new CuiLabel
                        {
                            Text = 
                            {
                                Text = $"{remaining:F0}s",
                                FontSize = 12,
                                Font = "robotocondensed-bold.ttf",
                                Color = remaining <= 10 ? "1 0.3 0.3 1" : config.TextColor,
                                Align = TextAnchor.MiddleRight
                            },
                            RectTransform = { AnchorMin = "0.65 0.5", AnchorMax = "0.95 1" }
                        }, "BlockStatus_Raid", "BlockStatus_Raid_Timer");
                        
                        CuiHelper.DestroyUi(player, "BlockStatus_Raid_Timer");
                        CuiHelper.AddUi(player, CuiHelper.ToJson(elements));
                    }
                }
                
                playerBlockStatus[player.userID] = status;
                
                // If no blocks are active anymore, destroy the UI
                if (!status.IsCombatBlocked && !status.IsRaidBlocked)
                {
                    DestroyUI(player);
                    if (uiUpdateTimers.ContainsKey(player.userID))
                    {
                        uiUpdateTimers[player.userID]?.Destroy();
                        uiUpdateTimers.Remove(player.userID);
                    }
                }
            }
            catch (Exception ex)
            {
                PrintError($"Error updating timer for {player.displayName}: {ex.Message}");
            }
        }

        private void DestroyUI(BasePlayer player)
        {
            if (player == null) return;
            
            CuiHelper.DestroyUi(player, "BlockStatus_UI");
            
            if (activeUi.ContainsKey(player.userID))
                activeUi.Remove(player.userID);
                
            if (uiUpdateTimers.ContainsKey(player.userID))
            {
                uiUpdateTimers[player.userID]?.Destroy();
                uiUpdateTimers.Remove(player.userID);
            }
        }
        
        private string HexToRustColor(string hex)
        {
            if (string.IsNullOrEmpty(hex)) return "1 1 1 1";
            
            try
            {
                hex = hex.TrimStart('#');
                int len = hex.Length;
                
                if (len != 6 && len != 8)
                    return "1 1 1 1";
                
                float r = int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float g = int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float b = int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255f;
                float a = len == 8 ? int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber) / 255f : 1f;
                
                return $"{r} {g} {b} {a}";
            }
            catch
            {
                return "1 1 1 1";
            }
        }
        
        private class BlockInfo
        {
            public string Type;
            public BlockSettings Settings;
            public float TimeRemaining;
        }
        #endregion

        #region Commands
        private void CmdBlockStatus(BasePlayer player, string command, string[] args)
        {
            if (args.Length == 0)
            {
                SendReply(player, "Usage: /blockstatus <toggle/reload>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "toggle":
                    ToggleBlockStatus(player);
                    break;
                    
                case "reload":
                    if (!permission.UserHasPermission(player.UserIDString, PermissionAdmin))
                    {
                        SendReply(player, "You don't have permission to reload the configuration!");
                        return;
                    }
                    
                    LoadConfig();
                    SendReply(player, "BlockStatus configuration reloaded!");
                    UpdateAllPlayers();
                    break;
                    
                default:
                    SendReply(player, "Usage: /blockstatus <toggle/reload>");
                    break;
            }
        }
        
        private void ToggleBlockStatus(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, PermissionHide))
            {
                permission.RevokeUserPermission(player.UserIDString, PermissionHide);
                SendReply(player, "Block status display enabled!");
                UpdatePlayerBlockStatus(player);
            }
            else
            {
                permission.GrantUserPermission(player.UserIDString, PermissionHide, this);
                SendReply(player, "Block status display disabled!");
                DestroyUI(player);
            }
        }
        #endregion

        #region Hooks
        private void OnPlayerConnected(BasePlayer player)
        {
            UpdatePlayerBlockStatus(player);
        }
        
        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (uiUpdateTimers.ContainsKey(player.userID))
            {
                uiUpdateTimers[player.userID]?.Destroy();
                uiUpdateTimers.Remove(player.userID);
            }
            
            if (activeUi.ContainsKey(player.userID))
                activeUi.Remove(player.userID);
                
            if (playerBlockStatus.ContainsKey(player.userID))
                playerBlockStatus.Remove(player.userID);
        }
        #endregion
    }
} 