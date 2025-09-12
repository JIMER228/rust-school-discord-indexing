using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Safe Kick", "supreme", "1.1.0")]
    [Description("Teleports players that are kicked for particular reasons to a safe place")]
    public class SafeKick : RustPlugin
    {
        #region Class Fields

        private PluginConfig _pluginConfig;

        private const string UsePermission = "safekick.use";
        private readonly Hash<string, MonumentInfo> _cachedMonuments = new Hash<string, MonumentInfo>();
        private readonly Hash<ulong, Vector3> _lastPlayerPosition = new Hash<ulong, Vector3>();

        #endregion

        #region Hooks

        private void OnServerInitialized()
        {
            permission.RegisterPermission(UsePermission, this);
            GetMonuments();
        }

        private void OnPlayerKicked(BasePlayer player, string reason)
        {
            if (permission.UserHasPermission(player.UserIDString, UsePermission) && IsReasonSafe(reason))
            {
                _lastPlayerPosition[player.userID] = player.transform.position;
                TeleportToRandomMonument(player);
            }
        }
        
        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (permission.UserHasPermission(player.UserIDString, UsePermission) && IsReasonSafe(reason))
            {
                _lastPlayerPosition[player.userID] = player.transform.position;
                TeleportToRandomMonument(player);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (permission.UserHasPermission(player.UserIDString, UsePermission) && _lastPlayerPosition.ContainsKey(player.userID))
            {
                player.Teleport(_lastPlayerPosition[player.userID]);
                _lastPlayerPosition.Remove(player.userID);
            }
        }

        #endregion

        #region Core Methods

        private void TeleportToRandomMonument(BasePlayer player)
        {
            List<string> monuments = Facepunch.Pool.GetList<string>();
            foreach (string monumentName in _pluginConfig.MonumentSafePositions.Keys)
            {
                if (monuments.Contains(monumentName))
                {
                    continue;
                }
                
                monuments.Add(monumentName);
            }
            
            string randomizedMonument = monuments.GetRandom();
            Facepunch.Pool.FreeList(ref monuments);
            MonumentInfo monumentInfo = _cachedMonuments[randomizedMonument];
            player.Teleport(monumentInfo.transform.TransformPoint(_pluginConfig.MonumentSafePositions[randomizedMonument].GetRandom()));
        }

        private bool IsReasonSafe(string reason)
        {
            string lowerCaseReason = reason.ToLower();
            foreach (string kickReason in _pluginConfig.KickReasons)
            {
                if (lowerCaseReason.Contains(kickReason))
                {
                    return true;
                }
            }

            return false;
        }

        #endregion

        #region Helper Methods

        private void GetMonuments()
        {
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                _cachedMonuments[monument.name] = monument;
            }
        }

        #endregion

        #region Chat Commands

        [ChatCommand("getmonumentpos")]
        private void MonumentPosCommand(BasePlayer player)
        {
            if (!player.IsAdmin)
            {
                player.ChatMessage("You are not an admin!");
                return;
            }
            
            foreach (MonumentInfo monument in _cachedMonuments.Values)
            {
                if (Vector3.Distance(monument.transform.position, player.transform.position) < 300f)
                {
                    Vector3 monumentPosition = monument.transform.InverseTransformPoint(player.transform.position);
                    player.ChatMessage($"Your position in <color=#acfa58>{monument.displayPhrase.english} ({monument.name})</color> is:\nx: {monumentPosition.x}\ny: {monumentPosition.y}\nz: {monumentPosition.z}</color>");
                    Puts($"Position in {monument.displayPhrase.english} ({monument.name}) is:\nx: {monumentPosition.x} y: {monumentPosition.y} z: {monumentPosition.z}");
                }
            }
        }

        #endregion
        
        #region Configuration
        private class PluginConfig
        {
            [JsonProperty(PropertyName = "Kick Reasons")]
            public HashSet<string> KickReasons { get; set; }
            
            [JsonProperty(PropertyName = "Monuments & positions to teleport (Randomized if more than one monument)")]
            public Hash<string, List<Vector3>> MonumentSafePositions { get; set; }
        }

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Loading Default Config");
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Config.Settings.DefaultValueHandling = DefaultValueHandling.Populate;
            _pluginConfig = AdditionalConfig(Config.ReadObject<PluginConfig>());
            Config.WriteObject(_pluginConfig);
        }

        private PluginConfig AdditionalConfig(PluginConfig pluginConfig)
        {
            pluginConfig.KickReasons = pluginConfig.KickReasons ?? new HashSet<string>
            {
                "auth",
                "packet flooding: player tick",
                "unresponsive",
                "steam"
            };

            pluginConfig.MonumentSafePositions = pluginConfig.MonumentSafePositions ?? new Hash<string, List<Vector3>>
            {
                ["assets/bundled/prefabs/autospawn/monument/medium/compound.prefab"] = new List<Vector3>
                {
                    new Vector3(),
                    new Vector3()
                },
                ["assets/bundled/prefabs/autospawn/monument/medium/bandit_town.prefab"] = new List<Vector3>
                {
                    new Vector3(),
                    new Vector3()
                }
            };
            
            return pluginConfig;
        }

        #endregion
    }
}