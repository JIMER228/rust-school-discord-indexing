using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("VehicleSpawner", "RIPJAWBONES", "1.0.5")]
    [Description("Allows players to spawn vehicle prefabs with chat commands and limits the number of vehicles a player can have with various restrictions and options.")]
    class VehicleSpawner : RustPlugin
    {
        #region Plugin References
        [PluginReference] private Plugin NoEscape = null;
        #endregion

        #region Data Fields
        private const string DespawnButtonColor = "0.835 0.254 0.156 0.5";
        private const string ButtonTextColor = "2.11 2.26 2.37 0.7";
        private const string SpawnButtonColor = "0.556 0.741 0.247 0.5";
        private const string BackgroundMaterial = "assets/content/ui/uibackgroundblur-ingamemenu.mat";
        private Dictionary<ulong, string> openMenus = new Dictionary<ulong, string>();
        private Dictionary<ulong, bool> lastUiState = new Dictionary<ulong, bool>();
        private Dictionary<ulong, DateTime> lastSpawnTimes = new Dictionary<ulong, DateTime>();
        private Dictionary<ulong, Dictionary<string, DateTime>> lastPrefabSpawnTimes = new Dictionary<ulong, Dictionary<string, DateTime>>();
        private Dictionary<ulong, List<BaseEntity>> playerEntities = new Dictionary<ulong, List<BaseEntity>>();
        private Dictionary<ulong, Timer> activeCountdownTimers = new Dictionary<ulong, Timer>();
        private Dictionary<ulong, string> activeTimerCommands = new Dictionary<ulong, string>();
        #endregion

        #region Configuration
        private Configuration config;

        class Configuration
        {
            public Dictionary<string, PrefabData> Prefabs { get; set; }
            public MessageSettings MsgSettings { get; set; } = new MessageSettings();
            public Settings Settings { get; set; } = new Settings();
            public string UsePermission { get; set; } = "vehiclespawner.use";
            public string UiPermission { get; set; } = "vehiclespawner.ui";
        }

        public class MessageSettings
        {
            [JsonProperty(PropertyName = "Message Icon SteamID")]
            public ulong MessageIconSteamId { get; set; } = 76561197960839785;

            [JsonProperty(PropertyName = "Message Prefix")]
            public string MessagePrefix { get; set; } = "[Vehicle Manager]";

            [JsonProperty(PropertyName = "Message Prefix Color")]
            public string MessagePrefixColor { get; set; } = "#5af";
        }

        class Settings
        {
            [JsonProperty("AmountOfPrefabsAllowedAtOnce")]
            public int AmountOfPrefabsAllowedAtOnce { get; set; } = 2;

            [JsonProperty("DestroyOnDisconnect")]
            public bool DestroyOnDisconnect { get; set; } = true;

            [JsonProperty("DestroyOnDeath")]
            public bool DestroyOnDeath { get; set; } = true;

            [JsonProperty("GlobalSpawnCooldownTimer")]
            public float GlobalSpawnCooldownTimer { get; set; } = 10.0f;

            [JsonProperty("BlockNearPlayer")]
            public BlockNearPlayerSettings BlockNearPlayer { get; set; } = new BlockNearPlayerSettings();

            [JsonProperty("UseRaidBlocked")]
            public bool UseRaidBlocked { get; set; } = true;

            [JsonProperty("UseCombatBlocked")]
            public bool UseCombatBlocked { get; set; } = true;
        }

        public class BlockNearPlayerSettings
        {
            [JsonProperty("UsePlayerDistance")]
            public bool UsePlayerDistance { get; set; } = true;

            [JsonProperty("PlayerDistance")]
            public float PlayerDistance { get; set; } = 5.0f;

            [JsonProperty("IgnoreTeam")]
            public bool IgnoreTeam { get; set; } = true;
        }

        class PrefabData
        {
            public int SpawnOption { get; set; } = 1; // Default to current method
            public float SpawnHeight { get; set; } = 3.0f; // Default height
            public string PrefabPath { get; set; }
            public int FuelAmount { get; set; } = 0;
            public float SpawnDistance { get; set; }
            public List<string> ChatCommands { get; set; }
            public Vector3 Rotation { get; set; } = Vector3.zero;
            public float CooldownTimer { get; set; } = 60.0f;
            public float SpawnDelay { get; set; } = 5.0f;
            public bool BlockOnLand { get; set; } = false;
            public bool BlockButAllowInSafeZone { get; set; } = false;
            public bool BlockInBuildingBlocked { get; set; } = true;
            public bool BlockButAllowInBuildingAuthorized { get; set; } = false;
            public string Permission { get; set; } = "";
            public bool LockFuel { get; set; } = false;
            public bool AutoMount { get; set; } = false;
            public bool SkySpawn { get; set; } = false;
            public float SkySpawnHeight { get; set; } = 50.0f;
            public bool BlockInSafeZone { get; set; } = true;
            public bool BlockInAuthorizedArea { get; set; } = true;
            public bool UseTracks { get; set; } = false;
            public bool DisableUIButton { get; set; } = false;
        }
        #endregion

        #region Configuration Methods
        protected override void LoadDefaultConfig()
        {
            config = new Configuration
            {
                UsePermission = "vehiclespawner.use",
                Prefabs = new Dictionary<string, PrefabData>
                {
                    {
                        "Minicopter", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/content/vehicles/minicopter/minicopter.entity.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 5.0f,
                            ChatCommands = new List<string> { "mini", "mymini", "minicopter", "myminicopter" },
                            Rotation = new Vector3(0, 90, 0),
                            CooldownTimer = 0.0f,
                            SpawnDelay = 0.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.minicopter",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Scrap Transport Helicopter", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 8.0f,
                            ChatCommands = new List<string> { "scrap", "myscrap", "heli", "myheli", "helicopter", "myhelicopter", "scrappy", "myscrappy", "scraptransport" },
                            Rotation = new Vector3(0, 180, 0),
                            CooldownTimer = 10.0f,
                            SpawnDelay = 5.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.scraptransporthelicopter",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Attack Helicopter", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 8.0f,
                            ChatCommands = new List<string> { "attack", "myattack", "attackheli", "myattackheli", "attackhelicopter", "myattackhelicopter", "atkheli", "atk", "myatk", "myatkheli" },
                            Rotation = new Vector3(0, 90, 0),
                            CooldownTimer = 180.0f,
                            SpawnDelay = 10.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.attackhelicopter",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Chinook", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/prefabs/npc/ch47/ch47.entity.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 10.0f,
                            ChatCommands = new List<string> { "chinook", "mychinook", "ch47", "mych47" },
                            Rotation = new Vector3(0, 240, 0),
                            CooldownTimer = 180.0f,
                            SpawnDelay = 10.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.chinook",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Balloon", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 12.0f,
                            ChatCommands = new List<string> { "balloon", "myballoon", "hotairballoon", "myhotairballoon", "myhab", "hab" },
                            Rotation = new Vector3(0, 90, 0),
                            CooldownTimer = 120.0f,
                            SpawnDelay = 10.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.balloon",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Horse", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/rust.ai/nextai/testridablehorse.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 5.0f,
                            ChatCommands = new List<string> { "horse", "myhorse" },
                            Rotation = new Vector3(0, 0, 0),
                            CooldownTimer = 180.0f,
                            SpawnDelay = 10.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.horse",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Car", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 6.0f,
                            ChatCommands = new List<string> { "sedan", "mysedan", "car", "mycar" },
                            Rotation = new Vector3(0, 0, 0),
                            CooldownTimer = 180.0f,
                            SpawnDelay = 10.0f,
                            BlockOnLand = false,
                            Permission = "vehiclespawner.car",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "Row Boat", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/content/vehicles/boats/rowboat/rowboat.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 9.0f,
                            ChatCommands = new List<string> { "rowboat", "row", "myrow", "myrowboat", "boat", "myboat" },
                            Rotation = new Vector3(0, 0, 0),
                            CooldownTimer = 10.0f,
                            SpawnDelay = 3.0f,
                            BlockOnLand = true,
                            Permission = "vehiclespawner.rowboat",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    },
                    {
                        "RHIB", new PrefabData
                        {
                            DisableUIButton = false,
                            PrefabPath = "assets/content/vehicles/boats/rhib/rhib.prefab",
                            FuelAmount = 0,
                            SpawnDistance = 10.0f,
                            ChatCommands = new List<string> { "rhib", "myrhib", "mybigboat", "bigboat" },
                            Rotation = new Vector3(0, 0, 0),
                            CooldownTimer = 10.0f,
                            SpawnDelay = 5.0f,
                            BlockOnLand = true,
                            Permission = "vehiclespawner.rhib",
                            SpawnOption = 1,
                            SpawnHeight = 3.0f
                        }
                    }
                },
                Settings = new Settings
                {
                    AmountOfPrefabsAllowedAtOnce = 2,
                    DestroyOnDisconnect = true,
                    DestroyOnDeath = true,
                    GlobalSpawnCooldownTimer = 10.0f,
                    BlockNearPlayer = new BlockNearPlayerSettings
                    {
                        UsePlayerDistance = true,
                        PlayerDistance = 5.0f,
                        IgnoreTeam = true
                    },
                    UseRaidBlocked = true,
                    UseCombatBlocked = true
                },
                MsgSettings = new MessageSettings
                {
                    MessageIconSteamId = 76561197960839785,
                    MessagePrefix = "[VehicleSpawner]",
                    MessagePrefixColor = "#5af"
                },
        		UiPermission = "vehiclespawner.ui"
            };
            SaveConfig();
        }

        private void LoadConfig()
        {
            config = Config.ReadObject<Configuration>();
            if (config == null)
            {
                PrintError("Config is null, using default values.");
                LoadDefaultConfig();
            }
        }

        private void SaveConfig() => Config.WriteObject(config, true);
        #endregion

        #region Initialization
        private void OnServerInitialized()
        {
            LoadConfig();
        }

        private void Init()
        {
            LoadConfig();

            permission.RegisterPermission(config.UsePermission, this);
            permission.RegisterPermission(config.UiPermission, this);

            RegisterChatCommands();

            foreach (var prefabEntry in config.Prefabs)
            {
                var prefabData = prefabEntry.Value;

                if (!string.IsNullOrEmpty(prefabData.Permission))
                {
                    permission.RegisterPermission(prefabData.Permission, this);
                }
            }
        }

        private void RegisterChatCommands()
        {
            AddCovalenceCommand("destroyall", nameof(HandleDestroyAllCommand));
            foreach (var prefabEntry in config.Prefabs)
            {
                foreach (var chatCommand in prefabEntry.Value.ChatCommands)
                {
                    AddCovalenceCommand($"no{chatCommand}", nameof(NoVehicleCommand));
                    AddCovalenceCommand(chatCommand, nameof(VehicleSpawnCommand));
                }
            }
        }
        #endregion

        #region Utility Methods
        private void LockVehicleFuelContainer(BaseVehicle vehicle, bool lockFuel)
        {
            if (!lockFuel || vehicle == null) return;

            var fuelSystem = vehicle.GetFuelSystem() as EntityFuelSystem;
            if (fuelSystem == null) return;

            var fuelContainer = fuelSystem.GetFuelContainer();
            if (fuelContainer != null)
            {
                fuelContainer.inventory.SetLocked(true);
            }
        }

        private void AddFuelToVehicle(BaseVehicle vehicle, int fuelAmount)
        {
            if (vehicle == null) return;

            var fuelSystem = vehicle.GetFuelSystem() as EntityFuelSystem;
            if (fuelSystem == null) return;

            var fuelContainer = fuelSystem.GetFuelContainer();
            if (fuelContainer == null) return;

            var fuelItem = ItemManager.CreateByItemID(-946369541, fuelAmount);
            if (fuelItem != null)
            {
                fuelItem.MoveToContainer(fuelContainer.inventory);
            }
        }

        private void RemoveOldestEntity(ulong playerId)
        {
            if (!playerEntities.ContainsKey(playerId) || playerEntities[playerId].Count == 0)
            {
                return;
            }

            // Get the oldest entity
            BaseEntity oldestEntity = playerEntities[playerId].FirstOrDefault();

            // Kill the oldest entity
            oldestEntity?.Kill();

            // Remove the oldest entity from the list
            playerEntities[playerId].Remove(oldestEntity);

            // Notify the player
            var basePlayer = BasePlayer.FindByID(playerId);
            if (basePlayer != null)
            {
                SendMessage(basePlayer, "One of your vehicles has been removed to make space for a new one.");
            }

            // Check if the removal has cleared the list and clean up if necessary
            if (playerEntities[playerId].Count == 0)
            {
                playerEntities.Remove(playerId);
            }
        }


        private bool IsCombatBlocked(BasePlayer player)
        {
            return NoEscape?.Call<bool>("IsCombatBlocked", player) ?? false;
        }

        private bool IsRaidBlocked(BasePlayer player)
        {
            return NoEscape?.Call<bool>("IsRaidBlocked", player) ?? false;
        }

        private void SendMessage(object player, string message)
        {
            if (player == null) return;

            BasePlayer basePlayer = null;

            if (player is IPlayer)
            {
                var iplayer = (IPlayer)player;
                basePlayer = iplayer.Object as BasePlayer;
            }
            else if (player is BasePlayer)
            {
                basePlayer = (BasePlayer)player;
            }

            if (basePlayer != null)
            {
                var coloredMessagePrefix = $"<color={config.MsgSettings.MessagePrefixColor}>{config.MsgSettings.MessagePrefix}</color>";
                var fullMessage = $"{coloredMessagePrefix} {message}";
                basePlayer.SendConsoleCommand("chat.add", 2, config.MsgSettings.MessageIconSteamId.ToString(), fullMessage);
            }
        }

        private void AutoMount(BasePlayer player, BaseEntity entity)
        {
            var vehicle = entity as BaseVehicle;
            if (vehicle != null && vehicle.mountPoints != null && vehicle.mountPoints.Count > 0)
            {
                var mountable = vehicle.mountPoints[0].mountable;
                if (mountable != null)
                {
                    mountable.MountPlayer(player);
                }
                else
                {
                    SendMessage(player, "Failed to mount. No mountable found at the first mount point.");
                }
            }
            else
            {
                SendMessage(player, $"The entity {entity.ShortPrefabName} does not support auto-mounting.");
            }
        }

        private void RemoveFuelFromPrefab(BaseEntity entity)
        {
            var vehicle = entity as BaseVehicle;
            if (vehicle != null)
            {
                var fuelSystem = vehicle.GetFuelSystem() as EntityFuelSystem;
                if (fuelSystem != null)
                {
                    var fuelContainer = fuelSystem.fuelStorageInstance.Get(true);
                    if (fuelContainer != null)
                    {
                        var fuelItem = fuelContainer.inventory.FindItemByItemID(-946369541);
                        fuelItem?.RemoveFromContainer();
                        fuelItem?.Remove();
                    }
                }
            }
        }

        private bool FindNearbyTrack(Vector3 position)
        {
            TrainTrackSpline nearestTrackSpline;
            float nearestSplineDistance;
            return TrainTrackSpline.TryFindTrackNear(position, 15.0f, out nearestTrackSpline, out nearestSplineDistance);
        }

        private bool MoveToTrack(TrainCar train, Vector3 position)
        {
            TrainTrackSpline trainTrackSpline;
            float frontWheelSplineDist;
            if (!TrainTrackSpline.TryFindTrackNear(train.GetFrontWheelPos(), 15.0f, out trainTrackSpline, out frontWheelSplineDist))
            {
                return false;
            }
            train.FrontWheelSplineDist = frontWheelSplineDist;
            Vector3 targetFrontWheelTangent;
            Vector3 positionAndTangent = trainTrackSpline.GetPositionAndTangent(train.FrontWheelSplineDist, train.transform.forward, out targetFrontWheelTangent);
            train.SetTheRestFromFrontWheelData(ref trainTrackSpline, positionAndTangent, targetFrontWheelTangent, train.localTrackSelection, null, true);
            train.FrontTrackSection = trainTrackSpline;
            return true;
        }

        private static Vector3 GetPositionInFrontOfPlayer(BasePlayer player, float distance)
        {
            return player.transform.position + (player.eyes.BodyForward() * distance);
        }

        private static Vector3 GetGroundBuilding(Vector3 position)
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(position + Vector3.up * 100, Vector3.down, out hitInfo, Mathf.Infinity, LayerMask.GetMask("Terrain", "World", "Construction", "Deployable", "Vehicle")))
            {
                position.y = hitInfo.point.y;
            }
            return position;
        }
        #endregion

		#region Helper
		private string SetFlags(BasePlayer player, string prefabKey)
		{
		    var prefabData = config.Prefabs[prefabKey];
		    
		    if (prefabData.BlockButAllowInBuildingAuthorized && prefabData.BlockButAllowInSafeZone)
		    {
		        if (!player.IsBuildingAuthed() && !player.InSafeZone())
		        {
		            return $"You can only spawn <color=#00ff00>{prefabKey}s</color> in building authorized areas or safe zones.";
		        }
		    }
		    else
		    {
		        if (prefabData.BlockButAllowInBuildingAuthorized && !player.IsBuildingAuthed())
		        {
		            return "You can only spawn in building authorized areas.";
		        }
		
		        if (prefabData.BlockButAllowInSafeZone && !player.InSafeZone())
		        {
		            return "You can only spawn in safe zones.";
		        }
		    }
		
		    if (prefabData.BlockButAllowInBuildingAuthorized && prefabData.BlockButAllowInSafeZone)
		    {
		        if (!player.InSafeZone() && !player.IsBuildingAuthed())
		        {
		            return "You can only spawn in building authorized areas or safe zones.";
		        }
		    }
		
		    if (prefabData.BlockInBuildingBlocked && player.IsBuildingBlocked())
		    {
		        return "You cannot spawn while building blocked.";
		    }
		
		    if (prefabData.BlockInSafeZone && player.InSafeZone())
		    {
		        return $"You can not spawn a <color=#00ff00>{prefabKey}</color> in safe zones.";
		    }
		
		    if (prefabData.BlockInAuthorizedArea && player.IsBuildingAuthed())
		    {
		        return $"You cannot spawn a <color=#55aaff>{prefabKey}</color> in an authorized area.";
		    }
		
		    if (!IsPlayerOutside(player))
		    {
		        return "test2 Bad location detected, try spawning elsewhere.";
		    }
		
            if (config.Settings.BlockNearPlayer.UsePlayerDistance)
            {
                int nearbyPlayerCount = 0;

                foreach (BasePlayer nearPlayer in BasePlayer.activePlayerList)
                {
                    if (!nearPlayer.IsConnected || !nearPlayer.IsAlive()) continue;

                    if (nearPlayer == player) continue;

                    if (Vector3.Distance(player.transform.position, nearPlayer.transform.position) <= config.Settings.BlockNearPlayer.PlayerDistance)
                    {
                        if (!config.Settings.BlockNearPlayer.IgnoreTeam || nearPlayer.Team == null || !nearPlayer.Team.members.Contains(player.userID))
                        {
                            nearbyPlayerCount++;
                        }
                    }
                }
		
                if (nearbyPlayerCount > 0)
                {
                    return $"Spawn blocked. Players are nearby.";
                }
            }
		
		    if (config.Settings.UseRaidBlocked && IsRaidBlocked(player))
		    {
		        return "You cannot spawn while raid blocked.";
		    }
		
		    if (config.Settings.UseCombatBlocked && IsCombatBlocked(player))
		    {
		        return $"You cannot spawn a <color=#00ff00>{prefabKey}</color> while combat blocked.";
		    }
		
		    return "OK";
		}
		#endregion

		#region Validation Methods
		private bool IsGroundDetected(BasePlayer player)
		{
		    Vector3 rayStart = player.transform.position + new Vector3(0, 0.5f, 0);
		    return Physics.Raycast(rayStart, Vector3.down, out _, Mathf.Infinity, LayerMask.GetMask("Terrain", "World"));
		}
		private bool IsPlayerBelowGround(BasePlayer player)
		{
		    float terrainHeight = TerrainMeta.HeightMap.GetHeight(player.transform.position);
		    return player.transform.position.y < (terrainHeight - 5.0f);
		}

		private bool IsPlayerOutside(BasePlayer player)
		{
		    bool isObstructionAbove = Physics.Raycast(new Ray(player.transform.position + new Vector3(0, 1.0f, 0), Vector3.up), 50f);
		    return !isObstructionAbove;
		}
		#endregion Validation Methods

		#region Validation
		private string ValidateSpawnLocation(BasePlayer player)
		{
		    if (!IsGroundDetected(player))
		    {
		        return "Spawn failed: No ground detected below.";
		    }
		    if (IsPlayerBelowGround(player))
		    {
		        return "Spawn failed: You are below ground level.";
		    }
		    if (!IsPlayerOutside(player))
		    {
		        return "Spawn failed: You must be outside to spawn.";
		    }
		    return "OK";
		}
		#endregion Validation

        #region Command Handlers
        private void HandleDestroyAllCommand(IPlayer iPlayer, string command, string[] args)
        {
            var basePlayer = iPlayer.Object as BasePlayer;
            if (basePlayer == null || !basePlayer.IsConnected)
            {
                SendMessage(iPlayer, "An error occurred. You might not be connected properly.");
                return;
            }

            if (!permission.UserHasPermission(basePlayer.UserIDString, config.UsePermission))
            {
                SendMessage(basePlayer, "You do not have permission to use this command.");
                return;
            }

            if (playerEntities.TryGetValue(basePlayer.userID, out List<BaseEntity> entities))
            {
                if (entities.Count > 0)
                {
                    foreach (var entity in entities.ToList())
                    {
                        RemoveFuelFromPrefab(entity);
                        entity.Kill();
                    }
                    entities.Clear();
                    SendMessage(basePlayer, "All your vehicles have been destroyed.");
                }
                else
                {
                    SendMessage(basePlayer, "You do not have any vehicles spawned.");
                }
            }
            else
            {
                SendMessage(basePlayer, "You do not have any vehicles spawned.");
            }
        }

        private void NoVehicleCommand(IPlayer iPlayer, string command, string[] args)
        {
            var basePlayer = iPlayer.Object as BasePlayer;
            if (basePlayer == null)
            {
                SendMessage(basePlayer, "An error occurred. Unable to process the command.");
                return;
            }

            if (!permission.UserHasPermission(basePlayer.UserIDString, config.UsePermission))
            {
                SendMessage(basePlayer, "You do not have permission to use this command.");
                return;
            }

            string prefabCommand = command.Substring(2).ToLower();

            if (!playerEntities.ContainsKey(basePlayer.userID) || playerEntities[basePlayer.userID].Count == 0)
            {
                SendMessage(basePlayer, "You do not have any vehicles spawned.");
                return;
            }

            string prefabKey = config.Prefabs
                .FirstOrDefault(prefabEntry => prefabEntry.Value.ChatCommands.Contains(prefabCommand, StringComparer.OrdinalIgnoreCase))
                .Key;

            if (string.IsNullOrEmpty(prefabKey))
            {
                SendMessage(basePlayer, $"Vehicle command <color=#55aaff>{prefabCommand}</color> not found.");
                return;
            }

            if (!string.IsNullOrEmpty(config.Prefabs[prefabKey].Permission) && !permission.UserHasPermission(basePlayer.UserIDString, config.Prefabs[prefabKey].Permission))
            {
                SendMessage(basePlayer, $"You do not have permission to destroy your <color=#55aaff>{prefabKey}</color>.");
                return;
            }

            bool prefabFound = false;
            List<BaseEntity> entitiesToRemove = new List<BaseEntity>();
            foreach (var entity in playerEntities[basePlayer.userID])
            {
                if (entity.PrefabName.Equals(config.Prefabs[prefabKey].PrefabPath, StringComparison.OrdinalIgnoreCase))
                {
                    entitiesToRemove.Add(entity);
                    prefabFound = true;
                }
            }

            foreach (var entity in entitiesToRemove)
            {
                playerEntities[basePlayer.userID].Remove(entity);
                entity.Kill();
            }

            if (prefabFound)
            {
                SendMessage(basePlayer, $"<color=#55aaff>{prefabKey}</color> has been destroyed.");
            }
            else
            {
                SendMessage(basePlayer, $"Vehicle spawned with command <color=#55aaff>{prefabCommand}</color> not found.");
            }
        }

        private void VehicleSpawnCommand(IPlayer player, string command, string[] args)
        {
            string cleanCommand = command.TrimStart('/');
            string prefabKey = config.Prefabs.FirstOrDefault(p => p.Value.ChatCommands.Contains(cleanCommand, StringComparer.OrdinalIgnoreCase)).Key;

            BasePlayer basePlayer = player.Object as BasePlayer;
            if (basePlayer == null) return;

            if (!permission.UserHasPermission(basePlayer.UserIDString, config.UsePermission))
            {
                SendMessage(player, $"You do not have permission to spawn a <color=#55aaff>{prefabKey}</color>.");
                return;
            }

            if ((player.Object as BasePlayer)?.GetMountedVehicle() != null)
            {
                SendMessage(player, $"You cannot spawn a <color=#55aaff>{prefabKey}</color> while mounted.");
                return;
            }

        if (activeTimerCommands.TryGetValue(basePlayer.userID, out string previousCommand))
        {
            string previousPrefabKey = config.Prefabs
                .FirstOrDefault(p => p.Value.ChatCommands.Contains(previousCommand, StringComparer.OrdinalIgnoreCase)).Key;

            if (previousCommand == command)
            {
                if (activeCountdownTimers.TryGetValue(basePlayer.userID, out Timer existingTimer))
                {
                    existingTimer.Destroy();
                    activeCountdownTimers.Remove(basePlayer.userID);
                    activeTimerCommands.Remove(basePlayer.userID);
                    lastSpawnTimes[basePlayer.userID] = DateTime.UtcNow;
                    SendMessage(player, $"<color=#55aaff>{previousPrefabKey}</color> countdown cancelled.");
                }
                return;
            }
            else
            {
                if (activeCountdownTimers.TryGetValue(basePlayer.userID, out Timer existingTimer))
                {
                    existingTimer.Destroy();
                    activeCountdownTimers.Remove(basePlayer.userID);
                    activeTimerCommands.Remove(basePlayer.userID);
                    SendMessage(player, $"<color=#55aaff>{previousPrefabKey}</color> countdown cancelled.");
                }
            }
        }

            foreach (var prefabEntry in config.Prefabs)
            {
                var prefabData = prefabEntry.Value;
                if (prefabData.ChatCommands.Contains(command, StringComparer.OrdinalIgnoreCase))
                {

                    if (!string.IsNullOrEmpty(prefabData.Permission) && !permission.UserHasPermission(player.Id, prefabData.Permission))
                    {
                        SendMessage(player, $"You do not have permission to spawn a <color=#55aaff>{prefabKey}</color>.");
                        return;
                    }

                    if (prefabData.UseTracks)
                    {
                        Vector3 playerPosition = basePlayer.transform.position + (basePlayer.eyes.BodyForward() * prefabData.SpawnDistance);
                        if (!FindNearbyTrack(playerPosition))
                        {
                            SendMessage(player, "You must be near train tracks to spawn a <color=#55aaff>{prefabKey}</color>.");
                            return;
                        }
                    }

                    if (lastSpawnTimes.TryGetValue(basePlayer.userID, out DateTime lastGlobalSpawnTime))
                    {
                        TimeSpan timeSinceLastGlobalSpawn = DateTime.UtcNow - lastGlobalSpawnTime;
                        if (timeSinceLastGlobalSpawn.TotalSeconds < config.Settings.GlobalSpawnCooldownTimer)
                        {
                            SendMessage(player, $"Vehicle cooldown active. Please wait <color=#FF7276>{(config.Settings.GlobalSpawnCooldownTimer - timeSinceLastGlobalSpawn.TotalSeconds):N0}</color> more seconds before spawning any <color=#55aaff>vehicles</color>.");
                            return;
                        }
                    }

                    if (lastPrefabSpawnTimes.TryGetValue(basePlayer.userID, out Dictionary<string, DateTime> prefabCooldowns) &&
                        prefabCooldowns.TryGetValue(prefabData.PrefabPath, out DateTime lastPrefabSpawnTime))
                    {
                        TimeSpan timeSinceLastPrefabSpawn = DateTime.UtcNow - lastPrefabSpawnTime;
                        if (timeSinceLastPrefabSpawn.TotalSeconds < prefabData.CooldownTimer)
                        {
                            SendMessage(player, $"Cooldown for <color=#55aaff>{prefabKey}s</color> active. Please wait <color=#FF7276>{(prefabData.CooldownTimer - timeSinceLastPrefabSpawn.TotalSeconds):N0}</color> more seconds to spawn this vehicle again.");
                            return;
                        }
                    }

                    string locationValidationResult = ValidateSpawnLocation(basePlayer);
                    if (locationValidationResult != "OK")
                    {
                        SendMessage(player, locationValidationResult);
                        return;
                    }

                    string flagResult = SetFlags(basePlayer, prefabKey);
                    if (flagResult != "OK")
                    {
                        SendMessage(player, flagResult);
                        return;
                    }


                    Vector3 spawnPosition = GetPositionInFrontOfPlayer(basePlayer, prefabData.SpawnDistance);
                    spawnPosition = GetGroundBuilding(spawnPosition);

                    if (prefabData.BlockOnLand)
                    {
                        int topologyLayer = TerrainMeta.TopologyMap.GetTopology(spawnPosition);
                        bool isOnMainLand = (topologyLayer & (int)TerrainTopology.Enum.Mainland) != 0;
                        bool isInRiver = (topologyLayer & (int)TerrainTopology.Enum.River) != 0;
                        bool isInLake = (topologyLayer & (int)TerrainTopology.Enum.Lake) != 0;

                        if (isOnMainLand && !isInRiver && !isInLake)
                        {
                            SendMessage(player, $"You cannot spawn a <color=#55aaff>{prefabKey}</color> on land!");
                            return;
                        }
                    }

                    if (prefabData.SpawnDelay <= 0)
                    {
                        SpawnVehicle(player, prefabEntry.Key, prefabData.PrefabPath, prefabData.SpawnDistance, prefabData.Rotation, prefabData.BlockOnLand);

                        if (!lastPrefabSpawnTimes.ContainsKey(basePlayer.userID))
                        {
                            lastPrefabSpawnTimes[basePlayer.userID] = new Dictionary<string, DateTime>();
                        }
                        lastPrefabSpawnTimes[basePlayer.userID][prefabData.PrefabPath] = DateTime.UtcNow;
                        return;
                    }

                    SendMessage(player, $"Spawning a <color=#55aaff>{prefabKey}</color> in <color=#FF7276>{prefabData.SpawnDelay}</color> seconds.");

                    Timer newTimer = timer.Once(prefabData.SpawnDelay, () =>
                    {
                        if (!player.IsConnected || player.Object == null) return;

                        SpawnVehicle(player, prefabEntry.Key, prefabData.PrefabPath, prefabData.SpawnDistance, prefabData.Rotation, prefabData.BlockOnLand);

                        if (playerEntities.TryGetValue(basePlayer.userID, out List<BaseEntity> spawnedEntities) && spawnedEntities.Any(e => e.PrefabName == prefabData.PrefabPath))
                        {
                            if (!lastPrefabSpawnTimes.ContainsKey(basePlayer.userID))
                            {
                                lastPrefabSpawnTimes[basePlayer.userID] = new Dictionary<string, DateTime>();
                            }
                            lastPrefabSpawnTimes[basePlayer.userID][prefabData.PrefabPath] = DateTime.UtcNow;
                        }

                        lastSpawnTimes[basePlayer.userID] = DateTime.UtcNow;

                        activeCountdownTimers.Remove(basePlayer.userID);
                        activeTimerCommands.Remove(basePlayer.userID);
                    });

                    activeCountdownTimers[basePlayer.userID] = newTimer;
                    activeTimerCommands[basePlayer.userID] = command;

                    return;
                }
            }

            SendMessage(player, $"Vehicle not found.");
        }
        #endregion

        #region Spawning Logic
        private void SpawnVehicle(IPlayer iPlayer, string prefabKey, string prefabPath, float spawnDistance, Vector3 configRotation, bool blockOnLand)
        {
            var basePlayer = iPlayer?.Object as BasePlayer;
            if (basePlayer == null) return;

            ulong playerId = basePlayer.userID;

            if (lastSpawnTimes.TryGetValue(playerId, out DateTime lastGlobalSpawnTime))
            {
                TimeSpan timeSinceLastGlobalSpawn = DateTime.UtcNow - lastGlobalSpawnTime;
                if (timeSinceLastGlobalSpawn.TotalSeconds < config.Settings.GlobalSpawnCooldownTimer)
                {
                    SendMessage(basePlayer, $"Vehicle cooldown active. Please wait {(config.Settings.GlobalSpawnCooldownTimer - timeSinceLastGlobalSpawn.TotalSeconds):N0} more seconds before spawning any <color=#55aaff>vehicles</color>.");
                    return;
                }
            }

            if (!playerEntities.ContainsKey(playerId))
            {
                playerEntities[playerId] = new List<BaseEntity>();
            }
            else if (playerEntities[playerId].Count >= config.Settings.AmountOfPrefabsAllowedAtOnce)
            {
                RemoveOldestEntity(playerId);
            }

            var prefabData = config.Prefabs[prefabKey];
            Vector3 spawnPosition = CalculateSpawnPosition(basePlayer, spawnDistance, prefabData.SpawnOption, prefabData.SpawnHeight, prefabData.BlockOnLand);
            Quaternion spawnRotation = Quaternion.LookRotation(basePlayer.eyes.BodyForward()) * Quaternion.Euler(configRotation);

            string flagResult = SetFlags(basePlayer, prefabKey);
            if (flagResult != "OK")
            {
                SendMessage(basePlayer, flagResult);
                return;
            }

            BaseEntity entity = GameManager.server.CreateEntity(prefabPath, spawnPosition, spawnRotation);
            if (entity == null)
            {
                PrintError("Failed to create prefab entity.");
                return;
            }

            entity.Spawn();

            PostSpawnActions(basePlayer, entity, prefabKey, prefabPath);

            playerEntities[playerId].Add(entity);
            SendMessage(basePlayer, $"Your <color=#55aaff>{prefabKey}</color> has spawned.");
            lastSpawnTimes[basePlayer.userID] = DateTime.UtcNow;
        }

        private Vector3 CalculateSpawnPosition(BasePlayer player, float distance, int spawnOption, float spawnHeight, bool blockOnLand)
        {
                    Vector3 position;
                    if (blockOnLand)
                    {
                        float heightOffset = 3.0f;
                        Vector3 playerPosition = player.transform.position;
                        Vector3 playerEyesDirection = player.eyes.BodyForward();
                        position = playerPosition + playerEyesDirection * distance + Vector3.up * heightOffset;
                    }
            else
            {
                switch (spawnOption)
                {
                    case 1:
                        position = GetPositionInFrontOfPlayer(player, distance);
                        position = GetGroundBuilding(position);
                        break;
                    case 2:
                        Vector3 playerPosition = player.transform.position;
                        Vector3 playerEyesDirection = player.eyes.BodyForward();
                        position = playerPosition + playerEyesDirection * distance + Vector3.up * spawnHeight;
                        break;
                    default:
                        position = Vector3.zero; // Fallback in case of configuration errors
                        PrintWarning($"Invalid spawn option: {spawnOption}. Please check your configuration.");
                        break;
                }
            }
            return position;
        }


        private void PostSpawnActions(BasePlayer player, BaseEntity entity, string prefabKey, string prefabPath)
        {
            var prefabData = config.Prefabs[prefabKey];
            if (prefabData == null) return;

            if (prefabData.UseTracks && entity is TrainCar train && !MoveToTrack(train, entity.transform.position))
            {
                entity.Kill();
                SendMessage(player, "Failed to spawn. No nearby tracks found.");
                return;
            }

            if (prefabData.SkySpawn)
            {
                SkySpawn(player, entity, prefabData.SkySpawnHeight);
            }

            if (prefabData.AutoMount)
            {
                AutoMount(player, entity);
            }

            if (prefabData.FuelAmount > 0 && entity is BaseVehicle vehicle)
            {
                AddFuelToVehicle(vehicle, prefabData.FuelAmount);
                if (prefabData.LockFuel)
                {
                    LockVehicleFuelContainer(vehicle, true);
                }
            }
        }

        private void SkySpawn(BasePlayer player, BaseEntity entity, float skySpawnHeight)
        {
            if (entity == null) return;

            var vehicle = entity as BaseVehicle;
            if (vehicle != null && vehicle.mountPoints != null && vehicle.mountPoints.Count > 0)
            {
                var mountable = vehicle.mountPoints[0].mountable;
                if (mountable != null)
                {
                    mountable.MountPlayer(player);
                    entity.SetFlag(BaseEntity.Flags.On, true);
                }
                else
                {
                    player.ChatMessage("Failed to mount. No mountable found at the first mount point.");
                }
            }

            if (player != null)
            {
            }

            Vector3 skySpawnPosition = entity.transform.position + new Vector3(0, skySpawnHeight, 0);

            entity.transform.position = skySpawnPosition;
            entity.SendNetworkUpdateImmediate();
        }
        #endregion

        #region Event Handlers
        void OnEntityKill(BaseNetworkable entity)
        {
            if (entity == null) return;

            BaseEntity baseEntity = entity as BaseEntity;
            if (baseEntity == null) return;

            foreach (var playerEntry in playerEntities.ToList())
            {
                ulong playerId = playerEntry.Key;
                List<BaseEntity> entitiesList = playerEntry.Value;

                if (entitiesList.Contains(baseEntity))
                {
                    entitiesList.Remove(baseEntity);

                    // Check if the player has more vehicles than allowed
                    if (entitiesList.Count > config.Settings.AmountOfPrefabsAllowedAtOnce)
                    {
                        // Remove the oldest vehicle
                        RemoveOldestEntity(playerId);
                    }

                    if (entitiesList.Count == 0)
                    {
                        playerEntities.Remove(playerId);
                    }

                    break;
                }
            }
        }

        void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity is BasePlayer player)
            {
                if (!permission.UserHasPermission(player.UserIDString, config.UsePermission))
                {
                    return;
                }

                bool hasBulletDamage = info.damageTypes.Has(Rust.DamageType.Bullet);
                bool hasExplosionDamage = info.damageTypes.Has(Rust.DamageType.Explosion);
                bool hasBluntDamage = info.damageTypes.Has(Rust.DamageType.Blunt);
                bool hasAnimalDamage = info.damageTypes.Has(Rust.DamageType.Bite);

                if (activeTimerCommands.TryGetValue(player.userID, out string prefabCommand) && !string.IsNullOrEmpty(prefabCommand))
                {
                    string prefabKey = config.Prefabs.FirstOrDefault(p => p.Value.ChatCommands.Contains(prefabCommand, StringComparer.OrdinalIgnoreCase)).Key;

                    if (hasBulletDamage || hasExplosionDamage || hasAnimalDamage)
                    {
                        if (activeCountdownTimers.TryGetValue(player.userID, out Timer timer))
                        {
                            timer.Destroy();
                            activeCountdownTimers.Remove(player.userID);
                            activeTimerCommands.Remove(player.userID);
                            SendMessage(player, $"Your {prefabKey} spawn countdown has been cancelled due to taking damage.");
                        }
                    }
                }
            }
        }

        void OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (config.Settings.DestroyOnDeath)
            {
                if (playerEntities.TryGetValue(player.userID, out List<BaseEntity> vehicles))
                {
                    foreach (var vehicle in vehicles.ToList())
                    {
                        if (vehicle != null && !vehicle.IsDestroyed)
                        {
                            RemoveFuelFromPrefab(vehicle);
                            vehicle.Kill();
                        }
                    }

                    vehicles.Clear();

                    playerEntities.Remove(player.userID);
                }
            }

            var hadUiOpen = openMenus.ContainsKey(player.userID);
            lastUiState[player.userID] = hadUiOpen;

            if (hadUiOpen)
            {
                DestroyUI(player);
            }
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (config.Settings.DestroyOnDisconnect && playerEntities.ContainsKey(player.userID))
            {
                var entitiesCopy = new List<BaseEntity>(playerEntities[player.userID]);

                foreach (var entity in entitiesCopy)
                {
                    RemoveFuelFromPrefab(entity);
                    entity?.Kill();
                }
                playerEntities.Remove(player.userID);
            }
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            HandlePlayerUIState(player);
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, config.UiPermission)) return;

            bool shouldRecreateUi = lastUiState.TryGetValue(player.userID, out bool hadUiOpenBefore) && hadUiOpenBefore;

            if (shouldRecreateUi)
            {
                player.SendConsoleCommand("vehiclemenu false");
            }

        }
        #endregion

        #region UI
        private Dictionary<ulong, float> fireThirdPressStartTime = new Dictionary<ulong, float>();

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.UiPermission)) return;

            bool fireThirdDown = input.IsDown(BUTTON.FIRE_THIRD);

            if (input.WasJustPressed(BUTTON.FIRE_THIRD))
            {
                var hadUiOpen = openMenus.ContainsKey(player.userID);

                lastUiState[player.userID] = hadUiOpen;

                if (hadUiOpen)
                {
                    DestroyUI(player);
                }
                else
                {
                    bool lastState = lastUiState.TryGetValue(player.userID, out bool visible) ? visible : false;
                    CreateUi(player, lastState);
                }

                return;
            }

            if (fireThirdDown)
            {
                if (!fireThirdPressStartTime.ContainsKey(player.userID))
                {
                    fireThirdPressStartTime[player.userID] = Time.realtimeSinceStartup;
                }
                else
                {
                    float heldTime = Time.realtimeSinceStartup - fireThirdPressStartTime[player.userID];

                    if (heldTime > 1.5f)
                    {
                        bool lastState = lastUiState.TryGetValue(player.userID, out bool visible) ? visible : true;
                        CreateUi(player, lastState);

                        fireThirdPressStartTime.Remove(player.userID);
                    }
                }
            }
            else if (fireThirdPressStartTime.ContainsKey(player.userID))
            {
                fireThirdPressStartTime.Remove(player.userID);
            }
        }

        private void HandlePlayerUIState(BasePlayer player)
        {
            if (player == null || !permission.UserHasPermission(player.UserIDString, config.UiPermission)) return;

            bool lastState = lastUiState.TryGetValue(player.userID, out bool visible) ? visible : false;

            CreateUi(player, lastState);
        }

        private void DestroyUI(BasePlayer player)
        {
            if (openMenus.TryGetValue(player.userID, out string uiName))
            {
                CuiHelper.DestroyUi(player, uiName);
                openMenus.Remove(player.userID);
            }
        }
        private CuiElementContainer CreateUi(BasePlayer player, bool visibilityState = false)
        {
            DestroyUI(player);

            CuiElementContainer container = new CuiElementContainer();
            string rootPanelName;

            if (!visibilityState)
            {
                rootPanelName = container.Add(new CuiPanel
                {
                    Image = new CuiImageComponent
                    {
                        Color = "0.231 0.235 0.207 0.85",
                        Material = BackgroundMaterial
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.985 0.4",
                        AnchorMax = "1 0.8"
                    }
                }, "Hud.Menu");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                    Button = { Command = "vehiclemenu true", Color = SpawnButtonColor, Material = BackgroundMaterial },
                    Text = { Text = "◀\n\nV\nE\nH\nI\nC\nL\nE\n \nM\nE\nN\nU\n\n◀", Align = TextAnchor.MiddleCenter, FontSize = 12, Color = ButtonTextColor }
                }, rootPanelName);
            }
            else
            {
                rootPanelName = container.Add(new CuiPanel
                {
                    Image = new CuiImageComponent
                    {
                        Color = "0.231 0.235 0.207 0.85",
                        Material = BackgroundMaterial
                    },
                    RectTransform = { AnchorMin = "0.93 0.4", AnchorMax = "1 0.8" }
                }, "Hud.Menu");

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.2 1" },
                    Button = { Command = "vehiclemenu false", Color = DespawnButtonColor, Material = BackgroundMaterial },
                    Text = { Text = "▶\n\nV\nE\nH\nI\nC\nL\nE\n  \nM\nE\nN\nU\n\n▶", Align = TextAnchor.MiddleCenter, FontSize = 12, Color = ButtonTextColor }
                }, rootPanelName);

                float anchorMinY = 0.985f;
                float buttonHeight = 0.08f;
                foreach (var prefabEntry in config.Prefabs)
                {
                    if (prefabEntry.Value.DisableUIButton) continue;

                    string prefabKey = prefabEntry.Key;
                    var firstCommand = prefabEntry.Value.ChatCommands.FirstOrDefault();
                    if (string.IsNullOrEmpty(firstCommand)) continue;

                    container.Add(new CuiButton
                    {
                        RectTransform = { AnchorMin = $"0.25 {anchorMinY - buttonHeight}", AnchorMax = $"0.94 {anchorMinY}" },
                        Button = { Command = $"chat.say /{firstCommand}", Color = SpawnButtonColor, Material = BackgroundMaterial },
                        Text = { Text = prefabKey, Align = TextAnchor.MiddleCenter, FontSize = 12, Color = ButtonTextColor }
                    }, rootPanelName);

                    anchorMinY -= buttonHeight + 0.02f;
                }

                container.Add(new CuiButton
                {
                    RectTransform = { AnchorMin = "0.25 0.013", AnchorMax = "0.94 0.087" },
                    Button = { Command = "destroyall", Color = DespawnButtonColor, Material = BackgroundMaterial },
                    Text = { Text = "Remove Vehicle", Align = TextAnchor.MiddleCenter, FontSize = 12, Color = ButtonTextColor }
                }, rootPanelName);
            }

            openMenus[player.userID] = rootPanelName;
            CuiHelper.AddUi(player, container);

            return container;
        }
        #endregion

        #region UI Commands
        [ConsoleCommand("vehiclemenu")]
        private void Command_VehicleMenu(ConsoleSystem.Arg arg)
        {
            BasePlayer player = arg.Player();
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, config.UiPermission))
            {
                SendMessage(player, $"You do not have permission to use this command.");
                return;
            }

            bool visibilityState = arg.GetBool(0, false);
            CreateUi(player, visibilityState);
        }

        [ChatCommand("vehiclemenu")]
        private void ChatCommand_VehicleMenu(BasePlayer player, string command, string[] args)
        {
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, config.UiPermission))
            {
                SendMessage(player, "You do not have permission to use this command.");
                return;
            }

            bool isUiShown = openMenus.ContainsKey(player.userID);

            if (isUiShown)
            {
                DestroyUI(player);
            }
            else
            {
                bool lastState = lastUiState.TryGetValue(player.userID, out bool visible) ? visible : true;
                CreateUi(player, lastState);
            }

            string stateMessage = isUiShown ? "hidden" : "shown";
            SendMessage(player, $"Vehicle menu is now {stateMessage}.");
        }

        [ChatCommand("vmenu")]
        private void ShowVehicleMenu(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, config.UiPermission))
            {
                SendMessage(player, "You do not have permission to access the vehicle menu.");
                return;
            }

            bool isMenuOpen = openMenus.ContainsKey(player.userID);

            if (isMenuOpen)
            {
                DestroyUI(player);
            }
            else
            {
                bool lastState = lastUiState.TryGetValue(player.userID, out bool visible) ? visible : true;
                CreateUi(player, lastState);
            }

        }
        #endregion

        #region Cleanup
        private void Unload()
        {
            foreach (var playerList in playerEntities.Values)
            {
                foreach (var entity in playerList)
                {
                    entity?.Kill();
                }
            }

    	foreach (var playerId in openMenus.Keys.ToList())
    	{
        	var player = BasePlayer.FindByID(playerId);
        	if (player != null)
        	{
            	DestroyUI(player);
        	}
    	}
            openMenus.Clear();
            playerEntities.Clear();
            lastSpawnTimes.Clear();
            lastPrefabSpawnTimes.Clear();
            activeCountdownTimers.Clear();
            activeTimerCommands.Clear();
        }
        #endregion
    }
} 