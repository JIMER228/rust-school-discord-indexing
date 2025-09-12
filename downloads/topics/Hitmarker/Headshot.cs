using System;
using System.Collections.Generic;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;
using Newtonsoft.Json;
using Oxide.Core;

/*
 * Changelog:
 *
 * Version 0.0.5:
 * - Code Cleanup.
 */

namespace Oxide.Plugins
{
    [Info("Headshot", "Wrecks", "0.0.5")]
    [Description("Displays a Customizable Icon on Headshot Kills.")]
    public class Headshot : RustPlugin
    {
        [PluginReference] private Plugin ImageLibrary;

        #region Declaration

        private Dictionary<ulong, Dictionary<string, int>> _playerData = new();
        private const string DataFolder = "Headshot";

        #endregion

        #region ui

        private void HeadshotUI(BasePlayer player)
        {
            CuiHelper.DestroyUi(player, "HeadshotUI");
            var headshotContainer = new CuiElementContainer();
            string uiAnchorMin;
            string uiAnchorMax;
            string uiOffsetMin;
            string uiOffsetMax;
            switch (_config.UILocation.ToLower())
            {
                case "top right":
                    uiAnchorMin = "1 1";
                    uiAnchorMax = "1 1";
                    uiOffsetMin = "-119.6 -80.001";
                    uiOffsetMax = "-39.6 -0.001";
                    break;
                case "top center":
                    uiAnchorMin = "0.5 1";
                    uiAnchorMax = "0.5 1";
                    uiOffsetMin = "-40 -100";
                    uiOffsetMax = "40 -20";
                    break;
                case "top left":
                    uiAnchorMin = "0 1";
                    uiAnchorMax = "0 1";
                    uiOffsetMin = "26.7 -80";
                    uiOffsetMax = "106.7 0";
                    break;
                case "center":
                    uiAnchorMin = "0.5 0.5";
                    uiAnchorMax = "0.5 0.5";
                    uiOffsetMin = "-40 -40";
                    uiOffsetMax = "40 40";
                    break;
                case "mid center":
                    uiAnchorMin = "0.5 1";
                    uiAnchorMax = "0.5 1";
                    uiOffsetMin = "-40 -169.9";
                    uiOffsetMax = "40 -99.9";
                    break;
                default:
                    uiAnchorMin = "0.5 1";
                    uiAnchorMax = "0.5 1";
                    uiOffsetMin = "-40 -100";
                    uiOffsetMax = "40 -20";
                    break;
            }
            headshotContainer.Add
            (new CuiPanel
            {
                CursorEnabled = false,
                Image = { Color = "0.2169811 0.1975347 0.1975347 0" },
                RectTransform =
                {
                    AnchorMin = uiAnchorMin,
                    AnchorMax = uiAnchorMax,
                    OffsetMin = uiOffsetMin,
                    OffsetMax = uiOffsetMax
                }
            }, "Overlay", "HeadshotUI");
            headshotContainer.Add
            (new CuiElement
            {
                Name = "HeadshotImage",
                Parent = "HeadshotUI",
                FadeOut = 1,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Color = "1 1 1 1", FadeIn = 0, Png = ImageLibrary?.Call<string>("GetImage", _config.BaseIcon)
                    },
                    new CuiOutlineComponent { Color = "0 0 0 0.5", Distance = "1 -1" },
                    new CuiRectTransformComponent
                    {
                        AnchorMin = uiAnchorMin,
                        AnchorMax = uiAnchorMax,
                        OffsetMin = uiOffsetMin,
                        OffsetMax = uiOffsetMax
                    }
                }
            });
            CuiHelper.AddUi(player, headshotContainer);
            timer.Once(1f, () => { CuiHelper.DestroyUi(player, "HeadshotImage"); });
            timer.Once(2f, () => { CuiHelper.DestroyUi(player, "HeadshotUI"); });
        }

        #endregion

        #region commands

        [ChatCommand("headshots")]
        private void Cmdheadshots(BasePlayer player)
        {
            if (player != null && _playerData.TryGetValue(player.userID, out var value))
            {
                var message = $"<color=#ff0a0a>Headshot Medals Achieved</color>:\n\n" + $"<color=#ffff00>Headshots</color>: {value["Headshots"]}\n";
                SendReply(player, message);
            }
            else
            {
                SendReply(player, "<color=#ff0a0a>No Headshot Medals Achieved.</color>");
            }
        }

        #endregion

        #region data

        private void SaveData()
        {
            Interface
                .GetMod()
                .DataFileSystem.WriteObject(DataFolder + "/playerData", _playerData);
        }

        private void LoadData()
        {
            var savedData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, Dictionary<string, int>>>(DataFolder + "/playerData");
            _playerData = savedData ?? new Dictionary<ulong, Dictionary<string, int>>();
            foreach (var data in _playerData.Values) data.TryAdd("Headshots", 0);
        }

        #endregion

        #region Config

        private static Configuration _config;

        public class Configuration
        {
            [JsonProperty("Headshot Icon")] public string BaseIcon { get; set; } = "https://www.dropbox.com/scl/fi/szx56bu8zh99hgon258ii/HEADSHOT.png?rlkey=dvf34q6jjeuqntx2o1yad44ns&dl=1";
            [JsonProperty("UI Location (top right, top center, top left, center, mid center")] public string UILocation { get; set; } = "mid center";
            [JsonProperty("Clear Medals on Wipe?")] public bool WipeReset { get; set; }

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    BaseIcon = "https://www.dropbox.com/scl/fi/szx56bu8zh99hgon258ii/HEADSHOT.png?rlkey=dvf34q6jjeuqntx2o1yad44ns&dl=1",
                    UILocation = "mid center",
                    WipeReset = false
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region oxidehooks

        private void Init()
        {
            LoadConfig();
            LoadData();
        }

        private void OnNewSave(string strFilename)
        {
            if (!_config.WipeReset) return;
            Puts("Server has Wiped, Clearing Headshot Medals.");
            _playerData.Clear();
            SaveData();
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            var playerId = player.userID;
            if (_playerData.TryGetValue(playerId, out var medals)) return;
            _playerData[playerId] = new Dictionary<string, int>
            {
                { "Headshots", 0 }
            };
            SaveData();
        }

        private void OnServerInitialized()
        {
            timer.Once
            (3f, () =>
            {
                if (!ImageLibrary)
                {
                    Puts("Image Library is needed to display the Headshot Icon.");
                    Interface.Oxide.UnloadPlugin(Name);
                    return;
                }
                ImageLibrary.Call("AddImage", _config.BaseIcon, _config.BaseIcon);
            });
        }

        private void Unload()
        {
            SaveData();
            foreach (var player in BasePlayer.activePlayerList) CuiHelper.DestroyUi(player, "HeadshotUI");
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (info?.InitiatorPlayer == null) return;
            var attacker = info.InitiatorPlayer;
            if (attacker.IsNpc)
            {
                return;
            }
            var attackerID = attacker.userID;
            _playerData ??= new Dictionary<ulong, Dictionary<string, int>>();
            if (!_playerData.TryGetValue(attackerID, out var medals))
                _playerData[attackerID] = new Dictionary<string, int>
                {
                    { "Headshots", 0 }
                };
            if (!info.isHeadshot) return;
            HeadshotUI(attacker);
            _playerData[attackerID]["Headshots"]++;
        }

        #endregion
    }
}