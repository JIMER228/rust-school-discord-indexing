using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("AirDropMarker", "AhigaO#4485", "1.1.1")]
    internal class AirDropMarker : RustPlugin
    {
        #region Static
        private Configuration _config;
        private Dictionary<ulong, BaseEntity> Markers = new Dictionary<ulong, BaseEntity>();
        private float Size;
        private float Square;

        private class Configuration
        {
            [JsonProperty(PropertyName = "Show notify in chat")]
            public bool chatNotify = true;

            [JsonProperty(PropertyName = "Create MapMarker on map")]
            public bool createMarker = true;
            
            [JsonProperty(PropertyName = "Select the type of marker (shopmarker | cratemarker)")]
            public string marker = "cratemarker";
        }

        #endregion
        
        #region Config

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        protected override void LoadDefaultConfig() => _config = new Configuration();

        #endregion
        
        #region OxideHooks

        private void OnServerInitialized()
        {
            var size = ConVar.Server.worldsize;
            Size = size / 2f;
            Square = Mathf.Floor(size / 146.3f);
            foreach (var check in BaseNetworkable.serverEntities.OfType<SupplyDrop>())
            {
                if (!check.IsValid()) continue;
                SpawnMarker(check);
            }
        }

        private void Unload()
        {
            if (!_config.createMarker) return;
            foreach (var check in Markers) check.Value?.Kill();
        }

        private void OnEntitySpawned(SupplyDrop drop)
        {
            if (!drop.IsValid()) return;
            SpawnMarker(drop);
        }

        private void OnEntityKill(SupplyDrop entity)
        {
            if (!entity.IsValid()) return;
            Markers[entity.net.ID.Value]?.Kill();
            Markers.Remove(entity.net.ID.Value);
        }

        #endregion

        #region Function

        private void SpawnMarker(SupplyDrop drop)
        {
            var position = drop.transform.position;
            if (_config.createMarker)
            {
                if (Markers.ContainsKey(drop.net.ID.Value)) return;
                var marker = GameManager.server.CreateEntity(_config.marker == "cratemarker" ? "assets/prefabs/tools/map/cratemarker.prefab" : "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab", position);
                if (_config.marker != "cratemarker")
                    (marker as VendingMachineMapMarker).markerShopName = "Air Drop";
                marker.enableSaving = false;
                marker.Spawn();
                Markers.Add(drop.net.ID.Value, marker);
            }
            if (!_config.chatNotify) return;
            var msg = GetGrid(position);
            foreach (var check in BasePlayer.activePlayerList) SendMessage(check, "CM_DROPSPAWNED", msg);
        }
        
        private string GetGrid(Vector3 pos)
        {
            var letter = 'A';
            var xCoordinate = Mathf.Floor((pos.x + Size) / 146.3f);
            var z = Square - Mathf.Floor((pos.z + Size) / 146.3f) - 1;
            letter = (char) (letter + xCoordinate % 26);
            return xCoordinate > 25 ? $"A{letter}{z}" : $"{letter}{z}";
        }

        #endregion
        
        #region Language

        private void SendMessage(BasePlayer player, string msg, params object[] args) => Player.Message(player, GetMsg(player.UserIDString, msg, args), 0);

        private string GetMsg(string player, string msg, params object[] args) =>
            string.Format(lang.GetMessage(msg, this, player), args);

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["CM_DROPSPAWNED"] = "AirDrop has been dropped in square {0} (You can see the marker when you open the map)"
            }, this);

        }

        #endregion
    }
} 