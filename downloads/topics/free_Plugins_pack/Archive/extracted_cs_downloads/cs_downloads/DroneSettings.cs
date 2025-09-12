using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using Oxide.Core;
using Oxide.Core.Plugins;
using Rust;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VLB;

namespace Oxide.Plugins
{
    [Info("Drone Settings", "WhiteThunder", "1.1.1")]
    [Description("Allows changing speed, toughness and other properties of RC drones.")]
    internal class DroneSettings : CovalencePlugin
    {
        #region Fields

        [PluginReference]
        private Plugin DroneScaleManager;

        private static DroneSettings _pluginInstance;
        private static Configuration _pluginConfig;

        private const string PermissionProfilePrefix = "dronesettings";

        private const string BaseDroneType = "BaseDrone";

        private DroneProperties _vanillaDroneProperties;
        private ProtectionProperties _vanillaDroneProtection;
        private List<ProtectionProperties> _customProtectionProperties = new List<ProtectionProperties>();

        #endregion

        #region Hooks

        private void Init()
        {
            _pluginInstance = this;
            _pluginConfig.Init(this);

            Unsubscribe(nameof(OnEntitySpawned));
        }

        private void OnServerInitialized()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null)
                    continue;

                OnEntitySpawned(drone);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                var station = player.GetMounted() as ComputerStation;
                if (station == null)
                    continue;

                var drone = GetControlledDrone(station);
                if (drone == null)
                    continue;

                OnBookmarkControlStarted(station, player, string.Empty, drone);
            }

            Subscribe(nameof(OnEntitySpawned));
        }

        private void Unload()
        {
            foreach (var entity in BaseNetworkable.serverEntities)
            {
                var drone = entity as Drone;
                if (drone == null || !IsDroneEligible(drone))
                    continue;

                DroneConnectionFixer.RemoveFromDrone(drone);

                if (!ApplySettingsWasBlocked(drone))
                {
                    RestoreVanillaSettings(drone);
                    Interface.CallHook("OnDroneSettingsChanged", drone);
                }
            }

            foreach (var protectionProperties in _customProtectionProperties)
                UnityEngine.Object.Destroy(protectionProperties);

            _pluginConfig = null;
            _pluginInstance = null;
        }

        private void OnEntitySpawned(Drone drone)
        {
            if (!IsDroneEligible(drone))
                return;

            if (_vanillaDroneProtection == null)
                _vanillaDroneProtection = drone.baseProtection;

            if (_vanillaDroneProperties == null)
                _vanillaDroneProperties = DroneProperties.FromDrone(drone);

            // Delay to give other plugins a moment to cache the drone id so they can specify drone type or block this.
            NextTick(() =>
            {
                if (drone == null)
                    return;

                var profile = GetDroneProfile(drone);
                if (profile == null)
                    return;

                TryApplyProfile(drone, profile);
            });
        }

        private void OnBookmarkControlStarted(ComputerStation station, BasePlayer player, string bookmarkName, Drone drone)
        {
            DroneConnectionFixer.OnControlStarted(drone, player);
        }

        private void OnBookmarkControlEnded(ComputerStation station, BasePlayer player, Drone drone)
        {
            if (drone == null)
                return;

            DroneConnectionFixer.OnControlEnded(drone, player);
        }

        private void OnDroneScaled(Drone drone, BaseEntity rootEntity, float scale, float previousScale)
        {
            if (scale == 1)
            {
                DroneConnectionFixer.OnRootEntityChanged(drone, drone);
            }
            else if (previousScale == 1)
            {
                DroneConnectionFixer.OnRootEntityChanged(drone, rootEntity);
            }
        }

        #endregion

        #region API

        private void API_RefreshDroneProfile(Drone drone)
        {
            var profile = GetDroneProfile(drone);
            if (profile == null)
                return;

            TryApplyProfile(drone, profile, restoreVanilla: true);
        }

        #endregion

        #region Helper Methods

        private static string DetermineDroneType(Drone drone)
        {
            return Interface.CallHook("OnDroneTypeDetermine", drone) as string;
        }

        private static bool ApplySettingsWasBlocked(Drone drone)
        {
            object hookResult = Interface.CallHook("OnDroneSettingsChange", drone);
            return hookResult is bool && (bool)hookResult == false;
        }

        private static BaseEntity GetRootEntity(Drone drone)
        {
            return _pluginInstance.DroneScaleManager?.Call("API_GetRootEntity", drone) as BaseEntity;
        }

        private static BaseEntity GetDroneOrRootEntity(Drone drone)
        {
            var rootEntity = GetRootEntity(drone);
            return rootEntity != null ? rootEntity : drone;
        }

        private static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(x, max));

        private static bool IsDroneEligible(Drone drone) => !(drone is DeliveryDrone);

        private static string GetProfilePermission(string droneType, string profileSuffix) =>
            $"{PermissionProfilePrefix}.{droneType}.{profileSuffix}";

        private static Drone GetControlledDrone(ComputerStation station) =>
            station.currentlyControllingEnt.Get(serverside: true) as Drone;

        private void RestoreVanillaSettings(Drone drone)
        {
            if (_vanillaDroneProtection != null && _customProtectionProperties.Contains(drone.baseProtection))
                drone.baseProtection = _vanillaDroneProtection;

            if (_vanillaDroneProperties != null)
                _vanillaDroneProperties?.ApplyToDrone(drone);
        }

        private bool TryApplyProfile(Drone drone, DroneProfile profile, bool restoreVanilla = false)
        {
            if (ApplySettingsWasBlocked(drone))
                return false;

            if (restoreVanilla)
                RestoreVanillaSettings(drone);

            profile.ApplyToDrone(drone);
            Interface.CallHook("OnDroneSettingsChanged", drone);
            return true;
        }

        private ProtectionProperties CreateProtectionProperties(Dictionary<string, float> damageMap)
        {
            var protectionProperties = ScriptableObject.CreateInstance<ProtectionProperties>();
            _customProtectionProperties.Add(protectionProperties);

            foreach (var entry in damageMap)
            {
                DamageType damageType;
                if (!Enum.TryParse<DamageType>(entry.Key, true, out damageType))
                {
                    _pluginInstance.LogError($"Invalid damage type: {entry.Key}");
                    continue;
                }
                protectionProperties.Add(damageType, 1 - Clamp(entry.Value, 0, 1));
            }

            return protectionProperties;
        }

        #endregion

        #region Drone Network Fixer

        // Fixes issue where fast moving drones temporarily disconnect and reconnect.
        // This issue occurs because the drone's network group and the client's secondary network group cannot be changed at the same time.
        private class DroneConnectionFixer : EntityComponent<Drone>
        {
            public static void OnControlStarted(Drone drone, BasePlayer player)
            {
                drone.GetOrAddComponent<DroneConnectionFixer>().AddController(player);
            }

            public static void OnControlEnded(Drone drone, BasePlayer player)
            {
                var component = drone.GetComponent<DroneConnectionFixer>();
                if (component == null)
                    return;

                component.RemoveController(player);
            }

            public static void OnRootEntityChanged(Drone drone, BaseEntity rootEntity)
            {
                var component = drone.GetComponent<DroneConnectionFixer>();
                if (component == null)
                    return;

                component.SetRootEntity(rootEntity);
            }

            public static void RemoveFromDrone(Drone drone) =>
                DestroyImmediate(drone.GetComponent<DroneConnectionFixer>());

            private bool _wasCallingNetworkGroup = false;
            private BaseEntity _rootEntity;
            private List<BasePlayer> _controllers = new List<BasePlayer>();

            private void Awake()
            {
                _rootEntity = GetDroneOrRootEntity(baseEntity);
            }

            private void AddController(BasePlayer player)
            {
                _controllers.Add(player);
            }

            private void RemoveController(BasePlayer player)
            {
                _controllers.Remove(player);
                if (_controllers.Count == 0)
                {
                    DestroyImmediate(this);
                }
            }

            private void SetRootEntity(BaseEntity rootEntity)
            {
                _rootEntity = rootEntity;
            }

            // Using LateUpdate since that's the soonest we can learn about a pending Invoke.
            private void LateUpdate()
            {
                // Detect when UpdateNetworkGroup has been scheduled, in order to schedule a custom one in its place
                if (_rootEntity.isCallingUpdateNetworkGroup && !_wasCallingNetworkGroup)
                    ScheduleCustomUpdateNetworkGroup(_rootEntity);

                _wasCallingNetworkGroup = _rootEntity.isCallingUpdateNetworkGroup;
            }

            private void SendFakeUpdateNetworkGroup(BaseEntity entity, BasePlayer player, uint groupId)
            {
                if (Net.sv.write.Start())
                {
                    Net.sv.write.PacketID(Message.Type.GroupChange);
                    Net.sv.write.EntityID(entity.net.ID);
                    Net.sv.write.GroupID(groupId);
                    Net.sv.write.Send(new SendInfo(player.net.connection));
                }
            }

            private void CustomUpdateNetworkGroup()
            {
                foreach (var player in _controllers)
                {
                    // Temporarily tell the client that the drone is in the global network group.
                    SendFakeUpdateNetworkGroup(_rootEntity, player, BaseNetworkable.GlobalNetworkGroup.ID);

                    // Update the client secondary network group to the one that the drone will change to.
                    player.net.SwitchSecondaryGroup(Network.Net.sv.visibility.GetGroup(_rootEntity.transform.position));
                }

                // Update the drone's network group based on its current position.
                // This will update clients to be aware that the drone is now in the new network group.
                _rootEntity.UpdateNetworkGroup();
            }

            private void ScheduleCustomUpdateNetworkGroup(BaseEntity entity)
            {
                entity.CancelInvoke(entity.UpdateNetworkGroup);
                Invoke(CustomUpdateNetworkGroup, 5);
            }

            private void OnDestroy()
            {
                if (_rootEntity == null)
                    return;

                if (_rootEntity.isCallingUpdateNetworkGroup && !_rootEntity.IsInvoking(_rootEntity.UpdateNetworkGroup))
                    _rootEntity.UpdateNetworkGroup();
            }
        }

        #endregion

        #region Configuration

        private DroneProfile GetDroneProfile(Drone drone)
        {
            var droneType = DetermineDroneType(drone) ?? BaseDroneType;
            return _pluginConfig.FindProfile(droneType, drone.OwnerID);
        }

        private class DroneProfile
        {
            [JsonProperty("PermissionSuffix", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public string PermissionSuffix;

            [JsonProperty("DroneProperties", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public DroneProperties DroneProperties;

            [JsonProperty("DamageScale", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public Dictionary<string, float> DamageScale;

            [JsonIgnore]
            public ProtectionProperties ProtectionProperties;

            [JsonIgnore]
            public string Permission;

            public void Init(DroneSettings pluginInstance, string droneType, bool requiresPermission)
            {
                if (requiresPermission)
                {
                    if (string.IsNullOrWhiteSpace(PermissionSuffix))
                        return;

                    Permission = GetProfilePermission(droneType, PermissionSuffix);
                    pluginInstance.permission.RegisterPermission(Permission, pluginInstance);
                }

                if (DamageScale != null)
                    ProtectionProperties = pluginInstance.CreateProtectionProperties(DamageScale);
            }

            public void ApplyToDrone(Drone drone)
            {
                if (ProtectionProperties != null)
                    drone.baseProtection = ProtectionProperties;

                if (DroneProperties != null)
                    DroneProperties.ApplyToDrone(drone);
            }
        }

        private class DroneProperties
        {
            public static DroneProperties FromDrone(Drone drone)
            {
                return new DroneProperties()
                {
                    KillInWater = drone.killInWater,
                    MovementAcceleration = drone.movementAcceleration,
                    AltitudeAcceleration = drone.altitudeAcceleration,
                    LeanWeight = drone.leanWeight,
                };
            }

            [JsonProperty("KillInWater", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public bool? KillInWater;

            [JsonProperty("MovementAcceleration", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float? MovementAcceleration;

            [JsonProperty("AltitudeAcceleration", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float? AltitudeAcceleration;

            [JsonProperty("LeanWeight", DefaultValueHandling = DefaultValueHandling.Ignore)]
            public float? LeanWeight;

            public void ApplyToDrone(Drone drone)
            {
                if (KillInWater != null)
                    drone.killInWater = (bool)KillInWater;

                if (MovementAcceleration != null)
                    drone.movementAcceleration = (float)MovementAcceleration;

                if (AltitudeAcceleration != null)
                    drone.altitudeAcceleration = (float)AltitudeAcceleration;

                if (LeanWeight != null)
                    drone.leanWeight = (float)LeanWeight;
            }
        }

        private class DroneTypeConfig
        {
            [JsonProperty("DefaultProfile")]
            public DroneProfile DefaultProfile = new DroneProfile();

            [JsonProperty("ProfilesRequiringPermission")]
            public DroneProfile[] ProfilesRequiringPermission = new DroneProfile[0];

            public void Init(DroneSettings pluginInstance, string droneType)
            {
                DefaultProfile.Init(pluginInstance, droneType, requiresPermission: false);

                foreach (var profile in ProfilesRequiringPermission)
                    profile.Init(pluginInstance, droneType, requiresPermission: true);
            }

            public DroneProfile GetProfileForOwner(ulong ownerId)
            {
                if (ownerId == 0 || (ProfilesRequiringPermission?.Length ?? 0) == 0)
                    return DefaultProfile;

                var ownerIdString = ownerId.ToString();
                for (var i = ProfilesRequiringPermission.Length - 1; i >= 0; i--)
                {
                    var profile = ProfilesRequiringPermission[i];
                    if (profile.Permission != null && _pluginInstance.permission.UserHasPermission(ownerIdString, profile.Permission))
                        return profile;
                }

                return DefaultProfile;
            }
        }

        private class Configuration : SerializableConfiguration
        {
            [JsonProperty("SettingsByDroneType")]
            public Dictionary<string, DroneTypeConfig> SettingsByDroneType = new Dictionary<string, DroneTypeConfig>()
            {
                [BaseDroneType] = new DroneTypeConfig()
                {
                    DefaultProfile = new DroneProfile()
                    {
                        DamageScale = new Dictionary<string, float>()
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.2f,
                            [DamageType.Bullet.ToString()] = 0.2f,
                            [DamageType.AntiVehicle.ToString()] = 0.25f,
                        },
                    },
                    ProfilesRequiringPermission = new DroneProfile[]
                    {
                        new DroneProfile()
                        {
                            PermissionSuffix = "god",
                            DroneProperties = new DroneProperties()
                            {
                                KillInWater = false,
                                MovementAcceleration = 30,
                                AltitudeAcceleration = 20,
                                LeanWeight = 0,
                            },
                            DamageScale = new Dictionary<string, float>()
                            {
                                [DamageType.AntiVehicle.ToString()] = 0,
                                [DamageType.Arrow.ToString()] = 0,
                                [DamageType.Bite.ToString()] = 0,
                                [DamageType.Bleeding.ToString()] = 0,
                                [DamageType.Blunt.ToString()] = 0,
                                [DamageType.Bullet.ToString()] = 0,
                                [DamageType.Cold.ToString()] = 0,
                                [DamageType.ColdExposure.ToString()] = 0,
                                [DamageType.Collision.ToString()] = 0,
                                [DamageType.Decay.ToString()] = 0,
                                [DamageType.Drowned.ToString()] = 0,
                                [DamageType.ElectricShock.ToString()] = 0,
                                [DamageType.Explosion.ToString()] = 0,
                                [DamageType.Fall.ToString()] = 0,
                                [DamageType.Fun_Water.ToString()] = 0,
                                [DamageType.Generic.ToString()] = 0,
                                [DamageType.Heat.ToString()] = 0,
                                [DamageType.Hunger.ToString()] = 0,
                                [DamageType.Poison.ToString()] = 0,
                                [DamageType.Radiation.ToString()] = 0,
                                [DamageType.RadiationExposure.ToString()] = 0,
                                [DamageType.Slash.ToString()] = 0,
                                [DamageType.Stab.ToString()] = 0,
                                [DamageType.Suicide.ToString()] = 0,
                                [DamageType.Thirst.ToString()] = 0,
                            },
                        },
                    },
                },
                ["DroneStorage"] = new DroneTypeConfig()
                {
                    DefaultProfile = new DroneProfile()
                    {
                        DroneProperties = new DroneProperties()
                        {
                            MovementAcceleration = 7.5f,
                            AltitudeAcceleration = 7.5f,
                        },
                        DamageScale = new Dictionary<string, float>()
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                        },
                    },
                },
                ["DroneTurrets"] = new DroneTypeConfig()
                {
                    DefaultProfile = new DroneProfile()
                    {
                        DroneProperties = new DroneProperties()
                        {
                            MovementAcceleration = 5,
                            AltitudeAcceleration = 5,
                        },
                        DamageScale = new Dictionary<string, float>()
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                            [DamageType.Explosion.ToString()] = 0.75f,
                            [DamageType.Blunt.ToString()] = 0.75f,
                        },
                    },
                },
                ["RidableDrones"] = new DroneTypeConfig()
                {
                    DefaultProfile = new DroneProfile()
                    {
                        DroneProperties = new DroneProperties()
                        {
                            MovementAcceleration = 7.5f,
                            AltitudeAcceleration = 7.5f,
                        },
                        DamageScale = new Dictionary<string, float>()
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.1f,
                            [DamageType.Bullet.ToString()] = 0.1f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                        },
                    },
                },
                ["MegaDrones"] = new DroneTypeConfig()
                {
                    DefaultProfile = new DroneProfile()
                    {
                        DroneProperties = new DroneProperties()
                        {
                            MovementAcceleration = 20,
                            AltitudeAcceleration = 20,
                            KillInWater = false,
                            LeanWeight = 0.1f,
                        },
                        DamageScale = new Dictionary<string, float>()
                        {
                            [DamageType.Generic.ToString()] = 0.1f,
                            [DamageType.Heat.ToString()] = 0.05f,
                            [DamageType.Bullet.ToString()] = 0.05f,
                            [DamageType.AntiVehicle.ToString()] = 0.1f,
                            [DamageType.Explosion.ToString()] = 0.1f,
                            [DamageType.Blunt.ToString()] = 0.25f,
                        },
                    },
                },
            };

            public void Init(DroneSettings pluginInstance)
            {
                foreach (var entry in SettingsByDroneType)
                    entry.Value.Init(pluginInstance, entry.Key);
            }

            public DroneProfile FindProfile(string droneType, ulong ownerId)
            {
                DroneTypeConfig droneTypeConfig;
                return SettingsByDroneType.TryGetValue(droneType, out droneTypeConfig)
                    ? droneTypeConfig.GetProfileForOwner(ownerId)
                    : null;
            }
        }

        private Configuration GetDefaultConfig() => new Configuration();

        #endregion

        #region Configuration Boilerplate

        private class SerializableConfiguration
        {
            public string ToJson() => JsonConvert.SerializeObject(this);

            public Dictionary<string, object> ToDictionary() => JsonHelper.Deserialize(ToJson()) as Dictionary<string, object>;
        }

        private static class JsonHelper
        {
            public static object Deserialize(string json) => ToObject(JToken.Parse(json));

            private static object ToObject(JToken token)
            {
                switch (token.Type)
                {
                    case JTokenType.Object:
                        return token.Children<JProperty>()
                                    .ToDictionary(prop => prop.Name,
                                                  prop => ToObject(prop.Value));

                    case JTokenType.Array:
                        return token.Select(ToObject).ToList();

                    default:
                        return ((JValue)token).Value;
                }
            }
        }

        private bool MaybeUpdateConfig(SerializableConfiguration config)
        {
            var currentWithDefaults = config.ToDictionary();
            var currentRaw = Config.ToDictionary(x => x.Key, x => x.Value);
            return MaybeUpdateConfigDict(currentWithDefaults, currentRaw);
        }

        private bool MaybeUpdateConfigDict(Dictionary<string, object> currentWithDefaults, Dictionary<string, object> currentRaw)
        {
            bool changed = false;

            foreach (var key in currentWithDefaults.Keys)
            {
                object currentRawValue;
                if (currentRaw.TryGetValue(key, out currentRawValue))
                {
                    var defaultDictValue = currentWithDefaults[key] as Dictionary<string, object>;
                    var currentDictValue = currentRawValue as Dictionary<string, object>;

                    if (defaultDictValue != null)
                    {
                        if (currentDictValue == null)
                        {
                            currentRaw[key] = currentWithDefaults[key];
                            changed = true;
                        }
                        else if (MaybeUpdateConfigDict(defaultDictValue, currentDictValue))
                            changed = true;
                    }
                }
                else
                {
                    currentRaw[key] = currentWithDefaults[key];
                    changed = true;
                }
            }

            return changed;
        }

        protected override void LoadDefaultConfig() => _pluginConfig = GetDefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _pluginConfig = Config.ReadObject<Configuration>();
                if (_pluginConfig == null)
                {
                    throw new JsonException();
                }

                if (MaybeUpdateConfig(_pluginConfig))
                {
                    LogWarning("Configuration appears to be outdated; updating and saving");
                    SaveConfig();
                }
            }
            catch (Exception e)
            {
                LogError(e.Message);
                LogWarning($"Configuration file {Name}.json is invalid; using defaults");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Log($"Configuration changes saved to {Name}.json");
            Config.WriteObject(_pluginConfig, true);
        }

        #endregion
    }
}
