/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Facepunch;
using Network;
using Newtonsoft.Json;
using Rust;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Bike Horn", "VisEntities", "1.3.0")]
    [Description("Adds horn functionality to motorbikes, sidecars, and pedal bikes.")]
    public class BikeHorn : RustPlugin
    {
        #region Fields

        private static BikeHorn _plugin;
        private static Configuration _config;
        private Dictionary<Bike, HornComponent> _bikeHorns = new Dictionary<Bike, HornComponent>();
        public const int LAYER_DOORS = Layers.Mask.Construction;

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Horn Button")]
            public string HornButton { get; set; }

            [JsonProperty("Horn Effect Prefab")]
            public string HornEffectPrefab { get; set; }

            [JsonProperty("Enable Door Interaction")]
            public bool EnableDoorInteraction { get; set; }

            [JsonProperty("Door Interaction Range")]
            public float DoorInteractionRange { get; set; }

            [JsonIgnore]
            public BUTTON Button
            {
                get
                {
                    if (Enum.TryParse(HornButton, true, out BUTTON button))
                    {
                        return button;
                    }

                    return BUTTON.USE;
                }
            }

            [JsonProperty("Bike Short Prefab Names")]
            public List<string> BikeShortPrefabNames { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            if (string.Compare(_config.Version, "1.3.0") < 0)
            {
                _config.HornEffectPrefab = defaultConfig.HornEffectPrefab;
            }

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                HornButton = "USE",
                HornEffectPrefab = "assets/content/nexus/ferry/effects/nexus-ferry-departure-horn.prefab",
                EnableDoorInteraction = true,
                DoorInteractionRange = 10f,
                BikeShortPrefabNames = new List<string>
                {
                    "motorbike_sidecar",
                    "motorbike",
                    "pedalbike"
                }
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            foreach (HornComponent horn in _bikeHorns.Values)
            {
                if (horn != null)
                    horn.Destroy();
            }

            _bikeHorns.Clear();
            _plugin = null;
            _config = null;
        }

        private void OnEntityMounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null || mountable == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return;

            Bike bike = mountable.GetParentEntity() as Bike;
            if (bike == null)
                return;

            if (bike.GetDriver() != player)
                return;

            if (!_config.BikeShortPrefabNames.Contains(bike.ShortPrefabName))
                return;

            if (!_bikeHorns.ContainsKey(bike))
            {
                HornComponent horn = HornComponent.Install(bike, player);
                _bikeHorns[bike] = horn;
            }
        }

        private void OnEntityDismounted(BaseMountable mountable, BasePlayer player)
        {
            if (player == null || mountable == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.USE))
                return;

            Bike bike = mountable.GetParentEntity() as Bike;
            if (bike == null)
                return;

            if (_bikeHorns.TryGetValue(bike, out HornComponent horn))
            {
                if (horn != null && horn.Player == player)
                {
                    horn.Destroy();
                    _bikeHorns.Remove(bike);
                }
            }
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            Bike bike = player.GetMountedVehicle() as Bike;
            if (bike == null)
                return;

            if (_bikeHorns.TryGetValue(bike, out HornComponent horn))
            {
                if (horn != null && horn.Player == player)
                {
                    horn.Destroy();
                    _bikeHorns.Remove(bike);
                }
            }
        }

        #endregion Oxide Hooks

        #region Horn Component

        public class HornComponent : FacepunchBehaviour
        {
            public BasePlayer Player { get; set; }

            private Bike _bike;
            private InputState _playerInput;
            private bool _hornButtonPressed = false;

            public static HornComponent Install(Bike bike, BasePlayer player)
            {
                HornComponent horn = bike.gameObject.AddComponent<HornComponent>();
                horn.Initialize(player);
                return horn;
            }

            public void Initialize(BasePlayer player)
            {
                _bike = GetComponent<Bike>();
                Player = player;
                _playerInput = Player.serverInput;
            }

            public static HornComponent GetComponent(Bike bike)
            {
                return bike.gameObject.GetComponent<HornComponent>();
            }

            public void Destroy()
            {
                DestroyImmediate(this);
            }

            private void Update()
            {
                if (Player == null || Player.IsDestroyed || Player.IsDead() || !Player.IsConnected)
                { 
                    Destroy();
                    _plugin._bikeHorns.Remove(_bike);
                    return;
                }

                if (_playerInput.WasJustPressed(_config.Button) && !_hornButtonPressed)
                {
                    RunEffectAttachedToEntity(_config.HornEffectPrefab, _bike);
                    if (_config.EnableDoorInteraction)
                        TryOpenNearbyDoors();
                    _hornButtonPressed = true;
                }
                else if (_playerInput.WasJustReleased(_config.Button))
                {
                    _hornButtonPressed = false;
                }
            }

            private void TryOpenNearbyDoors()
            {
                List<Door> nearbyDoors = Pool.Get<List<Door>>();
                Vis.Entities(_bike.transform.position, _config.DoorInteractionRange, nearbyDoors, LAYER_DOORS);

                foreach (Door door in nearbyDoors)
                {
                    if (door == null)
                        continue;

                    CodeLock codeLock = door.GetSlot(BaseEntity.Slot.Lock) as CodeLock;

                    if (codeLock != null)
                    {
                        if (!codeLock.whitelistPlayers.Contains(Player.userID))
                            continue;
                    }
                    else if (door.OwnerID != Player.userID)
                        continue;

                    if (door.IsOpen())
                        door.SetOpen(false);
                    else
                        door.SetOpen(true);
                }

                Pool.FreeUnmanaged(ref nearbyDoors);
            }
        }

        #endregion Horn Component

        #region Helper Functions

        public static void RunEffectAttachedToEntity(string effectName, BaseEntity attachedEntity, uint entityBoneID = 0u, Vector3 localPosition = default(Vector3),
            Vector3 localDirection = default(Vector3), Connection effectSourceConnection = null, bool shouldBroadcastToAllPlayers = false, List<Connection> targetConnections = null)
        {
            Effect.server.Run(effectName, attachedEntity, entityBoneID, localPosition, localDirection, effectSourceConnection, shouldBroadcastToAllPlayers, targetConnections);
        }

        #endregion Helper Functions

        #region Permissions

        public static class PermissionUtil
        {
            public const string USE = "bikehorn.use";
            private static readonly List<string> _permissions = new List<string>
            {
                USE
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions
    }
}