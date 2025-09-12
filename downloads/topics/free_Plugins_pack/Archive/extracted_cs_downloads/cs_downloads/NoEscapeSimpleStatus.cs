using UnityEngine;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Core.Configuration;
using System.Collections.Generic;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;

namespace Oxide.Plugins
{
    [Info("NoEscapeSimpleStatus", "Nogard", "1.2.0")]
    [Description("Detects when a player enters or leaves raid or combat lock")]
    internal class NoEscapeSimpleStatus : RustPlugin
    {
        #region Fields

        private readonly HashSet<BasePlayer> _combatBlockedPlayers = new();
        private readonly HashSet<BasePlayer> _raidBlockedPlayers = new();
        private readonly Dictionary<BasePlayer, float> _statusTimers = new();
        private readonly Dictionary<ulong, PlayerStatusMonitor> _playerMonitors = new();

        private ConfigFile _config;

        [PluginReference] private Plugin NoEscape, SimpleStatus, ImageLibrary;

        #endregion


        #region Classes

        public class ConfigFile
        {
            [JsonProperty(PropertyName = "General")]
            public GeneralSettings General { get; set; }

            [JsonProperty(PropertyName = "Blockages")]
            public BlockageSettings Blockages { get; set; }

            public class GeneralSettings
            {
                [JsonProperty(PropertyName = "Only with permission")]
                public bool OnlyWithPermission { get; set; }

                [JsonProperty(PropertyName = "Permissions")]
                public Dictionary<PermissionType, string> Permissions { get; set; }
            }

            public class BlockageSettings
            {
                public BlockageConfig Combat { get; set; }
                public BlockageConfig Raid { get; set; }
            }

            public class BlockageConfig
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool Enabled { get; set; }

                [JsonProperty(PropertyName = "Duration set in NoEscape plugin settings")]
                public int Duration { get; set; }

                [JsonProperty(PropertyName = "Simple Status Settings")]
                public SimpleStatusConfig SimpleStatus { get; set; }
            }

            public class SimpleStatusConfig
            {
                [JsonProperty(PropertyName = "Title")]
                public string Title { get; set; }

                [JsonProperty(PropertyName = "Title Color (RGB Hexadecimal format)")]
                public string TitleColor { get; set; }

                [JsonProperty(PropertyName = "Text Color (RGB Hexadecimal format)")]
                public string TextColor { get; set; }

                [JsonProperty(PropertyName = "Background Color (RGB Hexadecimal format)")]
                public string BackgroundColor { get; set; }

                [JsonProperty(PropertyName = "Icon URL")]
                public string IconURL { get; set; }

                [JsonProperty(PropertyName = "Icon Color (RGB Hexadecimal format)")]
                public string IconColor { get; set; }
            }

            public enum PermissionType
            {
                Combat,
                Raid
            }

            public static ConfigFile DefaultConfig()
            {
                return new ConfigFile()
                {
                    General = new GeneralSettings()
                    {
                        OnlyWithPermission = false,
                        Permissions = new Dictionary<PermissionType, string>
                        {
                            { PermissionType.Combat, "noescapesimplestatus.combat" },
                            { PermissionType.Raid, "noescapesimplestatus.raid" }
                        }
                    },
                    Blockages = new BlockageSettings()
                    {
                        Combat = new BlockageConfig()
                        {
                            Enabled = true,
                            Duration = 180,
                            SimpleStatus = new SimpleStatusConfig()
                            {
                                Title = "Combat",
                                TitleColor = "#E9C6C1",
                                TextColor = "#E9C6C1",
                                BackgroundColor = "#C53D28",
                                IconURL = "https://i.postimg.cc/65SyPLf2/espada.png",
                                IconColor = "#E9C6C1",
                            }
                        },
                        Raid = new BlockageConfig()
                        {
                            Enabled = true,
                            Duration = 300,
                            SimpleStatus = new SimpleStatusConfig()
                            {
                                Title = "Raid",
                                TitleColor = "#419CDC",
                                TextColor = "#419CDC",
                                BackgroundColor = "#164163",
                                IconURL = "https://i.postimg.cc/dVNXK3Sx/explosion.png",
                                IconColor = "#419CDC",
                            }
                        }
                    }
                };
            }
        }

        public class PlayerStatusMonitor : MonoBehaviour
        {
            private BasePlayer _player;
            private NoEscapeSimpleStatus _plugin;
            private float _combatCheckTime;
            private float _raidCheckTime;
            private const float CHECK_INTERVAL = 0.5f;

            public void Initialize(NoEscapeSimpleStatus plugin, BasePlayer player)
            {
                _plugin = plugin;
                _player = player;
                _combatCheckTime = 0f;
                _raidCheckTime = 0f;
            }

            void Update()
            {
                if (_player == null || _plugin == null || _plugin.NoEscape == null)
                {
                    Destroy(this);
                    return;
                }

                float time = Time.time;

                if (time >= _combatCheckTime)
                {
                    _plugin.HandleBlockState(_player, "combat", _plugin._combatBlockedPlayers, "IsCombatBlocked", ConfigFile.PermissionType.Combat);
                    _combatCheckTime = time + CHECK_INTERVAL;
                }

                if (time >= _raidCheckTime)
                {
                    _plugin.HandleBlockState(_player, "raid", _plugin._raidBlockedPlayers, "IsRaidBlocked", ConfigFile.PermissionType.Raid);
                    _raidCheckTime = time + CHECK_INTERVAL;
                }
            }

            void OnDestroy()
            {
                if (_plugin != null && _player != null)
                {
                    _plugin._combatBlockedPlayers.Remove(_player);
                    _plugin._raidBlockedPlayers.Remove(_player);
                    _plugin._statusTimers.Remove(_player);

                    if (_plugin._playerMonitors.ContainsKey(_player.userID))
                    {
                        _plugin._playerMonitors.Remove(_player.userID);
                    }

                    if (_plugin.SimpleStatus != null)
                    {
                        _plugin.SimpleStatus.Call("SetStatus", _player.UserIDString, "combat", 0);
                        _plugin.SimpleStatus.Call("SetStatus", _player.UserIDString, "raid", 0);
                    }
                }
            }
        }

        #endregion


        #region Hooks
        protected override void LoadDefaultConfig() => _config = ConfigFile.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<ConfigFile>() ?? ConfigFile.DefaultConfig();
            }
            catch (Exception ex)
            {
                PrintError($"Configuration error: {ex.Message}");
                LoadDefaultConfig();
            }
            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config, true);

        void Init()
        {
            foreach (var permissionEntry in _config.General.Permissions.Values)
                permission.RegisterPermission(permissionEntry, this);

            _combatBlockedPlayers.Clear();
            _raidBlockedPlayers.Clear();
            _statusTimers.Clear();
            _playerMonitors.Clear();
        }

        void OnServerInitialized()
        {
            if (!VerifyDependencies())
                return;

            InitializeImages();
            OnSimpleStatusReady();
            InitializeExistingPlayers();
        }

        private void OnServerSave() => SaveConfig();

        void OnPlayerConnected(BasePlayer player)
        {
            NextTick(() => AttachMonitorToPlayer(player));
        }

        void OnPlayerDisconnected(BasePlayer player)
        {
            if (player == null)
                return;

            if (_playerMonitors.TryGetValue(player.userID, out PlayerStatusMonitor monitor))
            {
                if (monitor != null)
                {
                    UnityEngine.Object.Destroy(monitor.gameObject);
                }
                _playerMonitors.Remove(player.userID);
            }

            _combatBlockedPlayers.Remove(player);
            _raidBlockedPlayers.Remove(player);
            _statusTimers.Remove(player);
        }

        void Unload()
        {
            foreach (var monitor in _playerMonitors.Values)
            {
                if (monitor != null)
                {
                    UnityEngine.Object.Destroy(monitor.gameObject);
                }
            }

            _playerMonitors.Clear();
            _combatBlockedPlayers.Clear();
            _raidBlockedPlayers.Clear();
            _statusTimers.Clear();
        }
        #endregion


        #region Helper Methods

        private int GetNoEscapeTimeRemaining(BasePlayer player, string blockType)
        {
            object result = NoEscape.Call("GetRemainingBlockTime", player, blockType);
            return result is float ? (int)Math.Ceiling((float)result) : 0;
        }

        private void HandleBlockState(BasePlayer player, string statusKey, HashSet<BasePlayer> blockedPlayers, string noEscapeMethod, ConfigFile.PermissionType permissionType)
        {
            if (player == null || NoEscape == null || SimpleStatus == null)
                return;

            bool isBlocked = (bool)NoEscape.Call(noEscapeMethod, player);
            bool wasBlocked = blockedPlayers.Contains(player);
            ConfigFile.BlockageConfig settings = statusKey == "raid" ? _config.Blockages.Raid : _config.Blockages.Combat;

            if (isBlocked && settings.Enabled)
            {
                if (_config.General.OnlyWithPermission && !permission.UserHasPermission(player.UserIDString, _config.General.Permissions[permissionType]))
                    return;

                int noEscapeTimeRemaining = GetNoEscapeTimeRemaining(player, statusKey);

                if (!wasBlocked)
                {
                    blockedPlayers.Add(player);
                    _statusTimers[player] = noEscapeTimeRemaining;
                    SimpleStatus.Call("SetStatus", player.UserIDString, statusKey, noEscapeTimeRemaining, false);
                    Interface.CallHook($"OnPlayerEnter{char.ToUpper(statusKey[0]) + statusKey.Substring(1)}Block", player);
                }
                else if (!_statusTimers.ContainsKey(player) || Math.Abs(_statusTimers[player] - noEscapeTimeRemaining) > 1f)
                {
                    _statusTimers[player] = noEscapeTimeRemaining;
                    SimpleStatus.Call("SetStatus", player.UserIDString, statusKey, noEscapeTimeRemaining, false);
                }
            }
            else if (!isBlocked && wasBlocked)
            {
                blockedPlayers.Remove(player);
                if (_statusTimers.ContainsKey(player))
                {
                    _statusTimers.Remove(player);
                }
                SimpleStatus.Call("SetStatus", player.UserIDString, statusKey, 0);
                Interface.CallHook($"OnPlayerExit{char.ToUpper(statusKey[0]) + statusKey.Substring(1)}Block", player);
            }
        }

        private void OnSimpleStatusReady()
        {
            SimpleStatus.CallHook("CreateStatus", this, "raid", new Dictionary<string, object>
            {
                ["color"] = ConvertHexToRgba(_config.Blockages.Raid.SimpleStatus.BackgroundColor),
                ["title"] = _config.Blockages.Raid.SimpleStatus.Title,
                ["titleColor"] = ConvertHexToRgba(_config.Blockages.Raid.SimpleStatus.TitleColor),
                ["text"] = null,
                ["textColor"] = ConvertHexToRgba(_config.Blockages.Raid.SimpleStatus.TextColor),
                ["icon"] = "raid",
                ["iconColor"] = ConvertHexToRgba(_config.Blockages.Raid.SimpleStatus.IconColor),
            });

            SimpleStatus.CallHook("CreateStatus", this, "combat", new Dictionary<string, object>
            {
                ["color"] = ConvertHexToRgba(_config.Blockages.Combat.SimpleStatus.BackgroundColor),
                ["title"] = _config.Blockages.Combat.SimpleStatus.Title,
                ["titleColor"] = ConvertHexToRgba(_config.Blockages.Combat.SimpleStatus.TitleColor),
                ["text"] = null,
                ["textColor"] = ConvertHexToRgba(_config.Blockages.Combat.SimpleStatus.TextColor),
                ["icon"] = "combat",
                ["iconColor"] = ConvertHexToRgba(_config.Blockages.Combat.SimpleStatus.IconColor),
            });
        }

        private string ConvertHexToRgba(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex))
                return "Invalid Input";

            if (hex.StartsWith("#"))
                hex = hex.Substring(1);

            if (hex.Length != 6 && hex.Length != 8)
                return "Invalid Input";

            int red = 0;
            int green = 0;
            int blue = 0;

            bool parsed = int.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, null, out red) &&
                          int.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, null, out green) &&
                          int.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, null, out blue);

            float alpha = 1.0f;

            if (hex.Length == 8)
            {
                parsed &= int.TryParse(hex.Substring(6, 2), NumberStyles.HexNumber, null, out int alphaInt);
                alpha = alphaInt / 255f;
            }

            if (!parsed)
                return "Invalid Input";

            return $"{(red / 255f):0.00} {(green / 255f):0.00} {(blue / 255f):0.00} {alpha:0.00}";
        }

        private void InitializeImages()
        {
            try
            {
                ImageLibrary.Call("AddImage", _config.Blockages.Raid.SimpleStatus.IconURL, "raid", 0UL);
                ImageLibrary.Call("AddImage", _config.Blockages.Combat.SimpleStatus.IconURL, "combat", 0UL);
            }
            catch (Exception ex)
            {
                PrintError($"Error initializing images: {ex.Message}");
            }
        }

        private bool VerifyDependencies()
        {
            if (!NoEscape)
            {
                PrintError("NoEscape plugin not found or not loaded!");
                return false;
            }

            if (!SimpleStatus)
            {
                PrintError("SimpleStatus plugin not found or not loaded!");
                return false;
            }

            if (!ImageLibrary)
            {
                PrintError("ImageLibrary plugin not found or not loaded!");
                return false;
            }

            return true;
        }

        private void AttachMonitorToPlayer(BasePlayer player)
        {
            if (player == null || player.IsDestroyed || !player.IsConnected)
                return;

            try
            {
                if (_playerMonitors.ContainsKey(player.userID))
                    return;

                GameObject monitorObj = new GameObject("StatusMonitor");
                monitorObj.transform.SetParent(player.transform);

                PlayerStatusMonitor monitor = monitorObj.AddComponent<PlayerStatusMonitor>();
                monitor.Initialize(this, player);

                _playerMonitors[player.userID] = monitor;
            }
            catch (Exception ex)
            {
                PrintError($"Error attaching monitor to player {player.displayName}: {ex.Message}");
            }
        }

        private void InitializeExistingPlayers()
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
            {
                if (player.IsConnected)
                {
                    AttachMonitorToPlayer(player);
                }
            }
        }

        #endregion
    }
} 