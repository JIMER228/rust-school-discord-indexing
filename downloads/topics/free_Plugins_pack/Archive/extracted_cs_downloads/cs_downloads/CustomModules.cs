using System.Collections.Generic;
using CompanionServer.Handlers;
using Newtonsoft.Json;
using UnityEngine;
using System;
using Oxide.Plugins.CustomModulesExtensionMethods;
using Oxide.Core;
using Rust.Modular;
using System.Reflection;
using System.Collections;
using ProtoBuf;
using Oxide.Core.Plugins;
using Facepunch;

namespace Oxide.Plugins
{
    [Info("CustomModules", "Adem", "1.2.2")]
    class CustomModules : RustPlugin
    {
        #region Variables
        [PluginReference] Plugin VehicleDeployedLocks;
        const bool en = false;
        private static CustomModules ins;
        HashSet<CustomCarSaveData> customCarSaveDatas = new HashSet<CustomCarSaveData>();
        HashSet<CustomCarClass> customCarClasses = new HashSet<CustomCarClass>();
        HashSet<string> subscribeMethods = new HashSet<string>
        {
            "OnVehicleModulesAssigned",
            "OnLootEntityEnd",
            "OnEntitySpawned",
            "OnEntityMounted",
            "OnEntityDismounted",
            "OnEngineStopped",
            "OnBookmarkControlStarted",
            "OnBookmarkControlEnded",
            "CanPickupEntity",
            "OnEntityDeath",
            "OnVehicleModuleMove",
            "OnTurretAuthorize",
            "OnRfFrequencyChange",
            "CanEntityTakeDamage",
            "CanEntityBeTargeted",
            "OnLootSpawn",
            "OnTurretTarget"
        };
        bool unloading = false;
        bool fullLoad = false;
        #endregion Variables

        #region Hooks
        void Init() => Unsubscribes();

        void OnServerInitialized()
        {
            ins = this;
            UpdateConfig();
            LoadDefaultMessages();
            PermissionManager.RegisterPermissions();
            LoadAllCustomCars();
            UpdateAllComputerStation();
            Subscribes();
            CustomCarSpawner.StartRespawnCorountime();
            NextTick(() => fullLoad = true);
        }

        void Unload()
        {
            unloading = true;
            CustomCarSpawner.StopRespawnCorountime();
            Unsubscribes();
            SaveAllCustomCars();
            DeleteAllCustomCars();
        }

        void OnServerSave()
        {
            SaveAllCustomCars();
        }

        void OnVehicleModulesAssigned(ModularCar car, ItemModVehicleModule[] modulePreset)
        {
            if (car == null) return;
            NextTick(() =>
            {
                AttachOrUpdateCustomCarClass(car);
            });
        }

        void OnEntitySpawned(ModularCar car)
        {
            NextTick(() =>
            {
                UpdateNewCar(car);
                AttachOrUpdateCustomCarClass(car);
            });
        }

        void OnLootEntityEnd(BasePlayer player, ModularCarGarage entity)
        {
            if (entity != null && entity.lockedOccupant != null) AttachOrUpdateCustomCarClass(entity.lockedOccupant);
        }

        void OnEntityMounted(ModularCarSeat modularCarSeat, BasePlayer player)
        {
            if (modularCarSeat == null || modularCarSeat.associatedSeatingModule == null || !modularCarSeat.associatedSeatingModule.Vehicle.HasDriver()) return;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetCarId() == modularCarSeat.associatedSeatingModule.Vehicle.net.ID.Value);
            if (customCarClass != null) customCarClass.OnPlayerMounted();
        }

        void OnEntityDismounted(ModularCarSeat modularCarSeat, BasePlayer player)
        {
            if (modularCarSeat == null || !player.IsRealPlayer() || modularCarSeat.associatedSeatingModule == null || modularCarSeat.associatedSeatingModule.Vehicle.HasDriver()) return;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetCarId() == modularCarSeat.associatedSeatingModule.Vehicle.net.ID.Value);
            if (customCarClass != null) customCarClass.OnPlayerDismounted();
        }

        object OnEngineStop(ModularCar modularCar)
        {
            if (modularCar == null)
                return null;

            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetCarId() == modularCar.net.ID.Value);

            if (customCarClass != null)
            {
                if (customCarClass.underwater && modularCar.HasDriver() && modularCar.GetFuelSystem().HasFuel() && modularCar.MeetsEngineRequirements())
                    return true;
            }

            return null;
        }

        void OnBookmarkControlStarted(ComputerStation computerStation, BasePlayer player, string bookmarkName, IRemoteControllable remoteControllable)
        {
            if (remoteControllable == null || remoteControllable.GetEnt() == null || player == null) return;
            foreach (CustomCarClass customCarClass in customCarClasses)
            {
                if (customCarClass == null || !customCarClass.PlayerHasKey(player)) continue;
                ModuleBaseClass moduleBaseClass = customCarClass.GetModuleByEntId(remoteControllable.GetEnt().net.ID.Value);
                if (moduleBaseClass == null) continue;
                RemoteModuleClass remoteModuleClass = moduleBaseClass as RemoteModuleClass;
                if (remoteModuleClass == null) continue;
                remoteModuleClass.StartControl(player);
                break;
            }
        }

        void OnBookmarkControlEnded(ComputerStation computerStation, BasePlayer player, BaseEntity controlledEntity)
        {
            if (controlledEntity == null || player == null) return;
            foreach (CustomCarClass customCarClass in customCarClasses)
            {
                if (customCarClass == null) continue;
                ModuleBaseClass moduleBaseClass = customCarClass.GetModuleByEntId(controlledEntity.net.ID.Value);
                if (moduleBaseClass == null) continue;
                RemoteModuleClass remoteModuleClass = moduleBaseClass as RemoteModuleClass;
                if (remoteModuleClass == null) continue;
                remoteModuleClass.StopControl();
                break;
            }
        }

        object CanPickupEntity(BasePlayer player, AutoTurret autoTurret)
        {
            if (autoTurret == null) return null;
            if (customCarClasses.Any(x => x != null && x.GetModuleByEntId(autoTurret.net.ID.Value) != null)) return false;
            return null;
        }

        object CanPickupEntity(BasePlayer player, RFBroadcaster broadcaster)
        {
            if (broadcaster == null) return null;
            if (customCarClasses.Any(x => x != null && x.GetModuleByEntId(broadcaster.net.ID.Value) != null)) return false;
            return null;
        }

        object CanPickupEntity(BasePlayer player, CCTV_RC cctv)
        {
            if (cctv == null) return null;
            if (customCarClasses.Any(x => x != null && x.GetModuleByEntId(cctv.net.ID.Value) != null)) return false;
            return null;
        }

        object OnVehicleModuleMove(BaseVehicleModule module, BaseModularVehicle vehicle, BasePlayer player)
        {
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetCarId() == vehicle.net.ID.Value);

            if (customCarClass == null)
                return null;

            return
                customCarClass.CanMoveCarModule(module.net.ID.Value);
        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            if (item == null || playerLoot == null || playerLoot.loot == null || playerLoot.loot.entitySource == null)
                return null;

            if (playerLoot.loot.entitySource is ModularCarGarage)
            {
                if (!LootManager.IsPlayerCanInstallItemToCar(playerLoot.baseEntity.UserIDString, item.info.shortname, item.skin))
                {
                    NotifyManager.SendMessageToPlayer(playerLoot.baseEntity, "NoPermission", _config.prefix);
                    return true;
                }
            }

            return null;
        }

        object OnTurretAuthorize(AutoTurret turret, BasePlayer player)
        {
            if (turret == null || player == null) return null;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(turret.net.ID.Value) != null);
            if (customCarClass == null) return null;
            if (!customCarClass.PlayerHasKey(player)) return true;
            return null;
        }

        object OnEntityTakeDamage(AutoTurret turret, HitInfo info)
        {
            if (!turret.IsExists() || info == null)
                return null;

            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(turret.net.ID.Value) != null);
            if (customCarClass != null)
            {
                customCarClass.OnChildEntityTakeDamage(turret.net.ID.Value, info);
                return true;
            }
            return null;
        }

        object OnEntityTakeDamage(RFBroadcaster rFBroadcaster, HitInfo info)
        {
            if (!rFBroadcaster.IsExists() || info == null)
                return null;

            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(rFBroadcaster.net.ID.Value) != null);
            if (customCarClass != null) return true;
            return null;
        }

        void OnEntityTakeDamage(BaseVehicleModule entity, HitInfo info)
        {
            if (!entity.IsExists() || info == null || entity.net == null)
                return;

            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByNetId(entity.net.ID.Value) != null);

            if (customCarClass != null)
            {
                ModuleBaseClass moduleBaseClass = customCarClass.GetModuleByNetId(entity.net.ID.Value);

                if (moduleBaseClass != null)
                    moduleBaseClass.OnModuleTakeDamage();
            }
        }

        object OnRfFrequencyChange(RFBroadcaster rFBroadcaster, int frequency, BasePlayer player)
        {
            if (player == null || rFBroadcaster == null) return null;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(rFBroadcaster.net.ID.Value) != null);
            if (customCarClass == null) return null;

            if (!customCarClass.PlayerHasKey(player)) return true;
            else if (_config.otherPluginsConfig.vehicleDeployedLocksConfig.enable && ins.plugins.Exists("VehicleDeployedLocks") && !(bool)VehicleDeployedLocks.Call("API_CanAccessVehicle", player, customCarClass.modularCar, true))
            {
                return true;
            }

            RemoteModuleClass remoteModuleClass = customCarClass.GetModuleByEntId(rFBroadcaster.net.ID.Value) as RemoteModuleClass;
            if (remoteModuleClass == null) return null;

            remoteModuleClass.UpdateFrequency(frequency.ToString());

            return null;
        }

        void OnLootSpawn(LootContainer container)
        {
            if (container == null || container.inventory == null) return;
            HashSet<ItemConfig> itemConfigs = _config.customItems.Where(x => x.cratesSetting.Any(y => container.ShortPrefabName == y.Key));
            foreach (ItemConfig itemConfig in itemConfigs)
            {
                if (UnityEngine.Random.Range(0f, 100f) <= itemConfig.cratesSetting.FirstOrDefault(x => container.ShortPrefabName == x.Key).Value)
                {
                    LootManager.TrySpawnItemInDefaultCrate(container, itemConfig);
                    return;
                }
            }
        }

        object OnTurretTarget(AutoTurret turret, BasePlayer player)
        {
            if (!turret.IsExists() || player == null)
                return null;

            if (turret.skinID != TurretModuleClass.turretSkinID)
                return null;

            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(turret.net.ID.Value) != null);

            if (customCarClass != null)
            {
                if (player.UserIDString.IsSteamId())
                {
                    if (!_config.mainConfig.allowAttackPlayers)
                        return true;
                }
                else if (!_config.mainConfig.allowAttackNpc)
                {
                    return true;
                }
                else if (player.InSafeZone())
                {
                    return true;
                }
            }

            return null;
        }

        #region OtherPlugins
        object CanEntityTakeDamage(AutoTurret autoTurret, HitInfo hitinfo)
        {
            if (!autoTurret.IsExists() || hitinfo == null) return null;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(autoTurret.net.ID.Value) != null);
            if (customCarClass != null)
            {
                customCarClass.OnChildEntityTakeDamage(autoTurret.net.ID.Value, hitinfo);
                return false;
            }
            return null;
        }

        object CanEntityTakeDamage(RFBroadcaster rFBroadcaster, HitInfo hitinfo)
        {
            if (!rFBroadcaster.IsExists() || hitinfo == null) return null;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(rFBroadcaster.net.ID.Value) != null);
            if (customCarClass != null) return true;
            return null;
        }

        object CanEntityBeTargeted(BasePlayer player, BaseEntity turret)
        {
            if (player == null || turret == null)
                return null;

            if (turret.skinID != TurretModuleClass.turretSkinID)
                return null;

            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetModuleByEntId(turret.net.ID.Value) != null);

            if (customCarClass != null)
            {
                if (player.UserIDString.IsSteamId())
                {
                    if (!_config.mainConfig.allowAttackPlayers)
                        return false;
                }
                else if (!_config.mainConfig.allowAttackNpc)
                {
                    return false;
                }
                else if (player.InSafeZone())
                {
                    return false;
                }
            }

            return null;
        }

        object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || player.skinID != RemoteModuleClass.dummySkinID) return null;
            if (baseLock == null) return null;
            VehicleModuleSeating seatingModule = baseLock.GetParentEntity() as VehicleModuleSeating;
            if (seatingModule == null) return null;
            ModularCar car = seatingModule.Vehicle as ModularCar;
            if (car != null && player.skinID == RemoteModuleClass.dummySkinID) return true;
            return null;
        }
        #endregion OtherPlugins
        #endregion Hooks

        #region Commands
        [ChatCommand("givemodule")]
        void ModuleGiveChatCommande(BasePlayer player, string command, string[] arg)
        {
            if (player == null || arg == null || arg.Length == 0) return;

            string customModuleName = arg[0];
            int amount = 1;
            if (arg.Length > 1) amount = Int32.Parse(arg[1]);

            ItemConfig itemConfig = _config.customItems.FirstOrDefault(x => x.customShortname == customModuleName);
            if (itemConfig == null) return;

            if (!player.IsAdmin && !permission.UserHasPermission(player.UserIDString, _config.mainConfig.givePermission))
            {
                PrintToChat(player, "You do not have permission to use this command!");
                return;
            }

            LootManager.GiveCustomItemToPlayer(player, itemConfig, amount);
            NotifyManager.SendMessageToPlayer(player, "GetModule", ins._config.prefix);
        }

        [ConsoleCommand("givemodule")]
        void ModuleGiveConsoleCommande(ConsoleSystem.Arg arg)
        {
            if (arg == null || arg.Player() != null || arg.Args.Length < 2) return;
            ulong userId = Convert.ToUInt64(arg.Args[0]);
            BasePlayer target = BasePlayer.FindByID(userId);

            if (target == null)
            {
                PrintError("Player not found!");
                return;
            }
            string customModuleName = arg.Args[1];
            int amount = 1;
            if (arg.Args.Length > 2)
            {
                amount = Convert.ToInt32(arg.Args[2]);
                if (amount <= 0) return;
            }

            ItemConfig itemConfig = _config.customItems.FirstOrDefault(x => x.customShortname == customModuleName);
            if (itemConfig == null) return;

            LootManager.GiveCustomItemToPlayer(target, itemConfig, amount);
            PrintToChat(target, GetMessage("GetModule", target.UserIDString, ins._config.prefix));
            Puts($"{itemConfig.name} was given to the {target.displayName}"); ;
        }

        [ChatCommand("spawncustomcar")]
        void SpawnCustomCarChatCommande(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin || arg.Length == 0) return;
            CustomCarSpawner.SpawnCar(arg[0], player.transform.position, player.transform.rotation, player);
        }

        [ChatCommand("customcarspawnpoint")]
        private void ChatNewCustomSpawnPoint(BasePlayer player, string command, string[] arg)
        {
            if (!player.IsAdmin) return;

            string carPresetConfig = "";
            if (arg.Length > 0)
            {
                carPresetConfig = arg[0];
                if (!_config.carPresets.Any(x => x.presetName == carPresetConfig))
                {
                    NotifyManager.PrintError(player, "PresetNotFoundExeption", carPresetConfig);
                    return;
                }
            }

            MonumentInfo monumentInfo = TerrainMeta.Path.Monuments.FirstOrDefault(x => Vector3.Distance(player.transform.position, x.transform.position) < x.Bounds.size.x);
            if (monumentInfo != null)
            {
                CustomCarSpawner.AddMonumentSpawnPoint(player, monumentInfo, carPresetConfig);
            }
            else
            {
                CustomCarSpawner.AddMapSpawnPoint(player, carPresetConfig);
            }
        }
        #endregion Commands

        #region Methods
        void UpdateConfig()
        {
            if (_config.version == Version) return;

            if (_config.version.Minor == 0)
            {
                if (_config.version.Patch <= 1)
                {
                }
                if (_config.version.Patch < 6)
                {
                    _config.respawnSetting = new RespawnSetting
                    {
                        enable = false,
                        respawnPeriod = 7200,
                        mapRespawnConfig = new RespawnPositionsConfig
                        {
                            carPresetLocations = new Dictionary<string, List<LocationConfig>>(),
                            randomSpawnLocations = new List<LocationConfig>()
                        },
                        monumentRespawnConfigs = new HashSet<MonumentRespawnPositionsConfig>
                        {
                            new MonumentRespawnPositionsConfig
                            {
                                monumentPreset = "assets/bundled/prefabs/autospawn/monument/roadside/supermarket_1.prefab",
                                randomSpawnLocations = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(16.6, 0.0, -3.8)",
                                        rotation = "(0, 172.9239, 0)"
                                    }
                                },
                                carPresetLocations = new Dictionary<string, List<LocationConfig>>()
                            },
                            new MonumentRespawnPositionsConfig
                            {
                                monumentPreset = "assets/bundled/prefabs/autospawn/monument/roadside/gas_station_1.prefab",
                                randomSpawnLocations = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(4.0, 3.0, 7.4)",
                                        rotation = "(0, 90, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(4.0, 3.0, -8.8)",
                                        rotation = "(0, 90, 0)"
                                    }
                                },
                                carPresetLocations = new Dictionary<string, List<LocationConfig>>()
                            }
                        },
                        presets = new Dictionary<string, float>
                        {
                            ["buoyancy_car_1"] = 70,
                            ["remote_car_1"] = 30
                        }
                    };
                    _config.carPresets = new List<CarPresetConfig>
                    {
                        new CarPresetConfig
                        {
                            presetName = "buoyancy_car_1",
                            carPrefabName = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",
                            modules = new List<string>
                            {
                                "buoyancyitem_1",
                                "vehicle.1mod.cockpit.with.engine",
                                "buoyancyitem_1"
                            },
                            engineComponents = true,
                            engineComponentsLvl = 1,
                            fuel = 50,
                        },
                        new CarPresetConfig
                        {
                            presetName = "remote_car_1",
                            carPrefabName = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",
                            modules = new List<string>
                            {
                                "remoteitem_1",
                                "vehicle.2mod.flatbed"
                            },
                            engineComponents = true,
                            engineComponentsLvl = 1,
                            fuel = 50,
                        }
                    };
                    _config.otherPluginsConfig.vehicleDeployedLocksConfig = new VehicleDeployedLocksConfig();
                }

                if (_config.version.Patch < 7)
                {
                    _config.mainConfig.allowAttackPlayers = true;
                    _config.mainConfig.allowAttackNpc = true;
                }

                _config.mainConfig.lightOperatonModeAtRemote = 1;
                _config.version = new VersionNumber(1, 1, 0);
            }
            if (_config.version.Minor == 1)
            {
                if (_config.version.Patch < 7)
                {
                    for (int i = 0; i < _config.turretModules.Count; i++)
                    {
                        TurretModuleConfig turretModuleConfig = _config.turretModules[i];
                        for (int a = 0; a < turretModuleConfig.turretlocations.Count; a++)
                        {
                            LocationConfig locationConfig = turretModuleConfig.turretlocations[a];
                            string[] versionArray = locationConfig.position.Split(',');
                            locationConfig.position = locationConfig.position.Replace(versionArray[1], " -0.16");
                        }
                    }

                    foreach (BaseModuleConfig baseModuleConfig in _config.turretModules)
                        baseModuleConfig.permissionForPlacement = "";

                    foreach (BaseModuleConfig baseModuleConfig in _config.boostModules)
                        baseModuleConfig.permissionForPlacement = "";

                    foreach (BaseModuleConfig baseModuleConfig in _config.buoyancyModules)
                        baseModuleConfig.permissionForPlacement = "";

                    foreach (BaseModuleConfig baseModuleConfig in _config.remoteControlModuleConfigs)
                        baseModuleConfig.permissionForPlacement = "";

                    foreach (BaseModuleConfig baseModuleConfig in _config.regularModules)
                        baseModuleConfig.permissionForPlacement = "";
                }
            }

            _config.version = Version;
            SaveConfig();
        }

        void Unsubscribes()
        {
            foreach (string hook in subscribeMethods)
                Unsubscribe(hook);
            if (_config.mainConfig.enableSpawnInCrates)
                Unsubscribe("OnLootSpawn");
        }

        void Subscribes()
        {
            Subscribe("OnLootSpawn");

            foreach (string hook in subscribeMethods)
                Subscribe(hook);
        }

        void SaveAllCustomCars()
        {
            foreach (CustomCarClass customCarClass in customCarClasses) customCarClass.SaveCar();
            Interface.Oxide.DataFileSystem.WriteObject(Title, customCarSaveDatas);
        }

        void LoadAllCustomCars()
        {
            customCarSaveDatas = Interface.Oxide.DataFileSystem.ReadObject<HashSet<CustomCarSaveData>>(Title);
            foreach (ModularCar modularCar in ModularCar.allCarsList)
            {
                if (modularCar.IsExists())
                    AttachOrUpdateCustomCarClass(modularCar);
            }
        }

        void UpdateAllComputerStation()
        {
            foreach (ComputerStation computerStation in BaseNetworkable.serverEntities.OfType<ComputerStation>())
            {
                computerStation.autoGatherRadius = 0;
                List<string> cameraIDs = new List<string>();
                foreach (string cameraId in computerStation.controlBookmarks)
                    cameraIDs.Add(cameraId);
                computerStation.controlBookmarks.Clear();
                if (computerStation != null) computerStation.GatherStaticCameras();
                foreach (string cameraId in cameraIDs) computerStation.ForceAddBookmark(cameraId);
            }
        }

        void UpdateNewCar(ModularCar modularCar)
        {
            foreach (BaseVehicleModule baseVehicleModule in modularCar.AttachedModuleEntities)
            {
                if (baseVehicleModule == null || baseVehicleModule.AssociatedItemInstance.skin != 0) continue;
                HashSet<ItemConfig> customModules = _config.customItems.Where(x => x.spawnChance > 0 && x.skin != 0 && x.shortname == baseVehicleModule.AssociatedItemInstance.info.shortname);
                if (customModules.Count == 0) continue;
                customModules.OrderBy(x => x.spawnChance);
                foreach (ItemConfig itemConfig in customModules)
                {
                    if (UnityEngine.Random.Range(0, 100) <= itemConfig.spawnChance)
                    {
                        baseVehicleModule.AssociatedItemInstance.skin = itemConfig.skin;
                        break;
                    }
                }
            }
        }

        void AttachOrUpdateCustomCarClass(ModularCar modularCar)
        {
            if (modularCar == null || modularCar.net == null) return;
            CustomCarClass customCarClass = customCarClasses.FirstOrDefault(x => x != null && x.GetCarId() == modularCar.net.ID.Value);
            if (customCarClass != null)
            {
                customCarClass.UpdateModules();
                return;
            }

            if (HasAnyCustomModule(modularCar) || (customCarSaveDatas != null && customCarSaveDatas.Any(x => x != null && x.netId == modularCar.net.ID.Value)))
            {
                customCarClass = modularCar.gameObject.AddComponent<CustomCarClass>();
                customCarClass.Init(modularCar);
                customCarClasses.Add(customCarClass);
            }
        }

        void DeleteAllCustomCars()
        {
            foreach (CustomCarClass customCarClass in customCarClasses) if (customCarClass != null) GameObject.DestroyImmediate(customCarClass);
        }

        bool HasAnyCustomModule(ModularCar modularCar)
        {
            return modularCar.AttachedModuleEntities.Any(x => x != null && x.AssociatedItemInstance != null && _config.customItems.Any(y => x.AssociatedItemInstance.info.shortname == y.shortname && x.AssociatedItemInstance.skin == y.skin));
        }
        #endregion Methods

        #region Classes
        class CustomCarClass : FacepunchBehaviour
        {
            internal bool underwater = false;
            internal ModularCar modularCar;
            HashSet<ModuleBaseClass> customModules = new HashSet<ModuleBaseClass>();
            internal CustomCarSaveData customCarSaveData;
            internal BasePlayer playerController;

            internal Vector3 GetCarPosition()
            {
                return modularCar.transform.position;
            }

            internal ulong GetCarId()
            {
                if (modularCar == null) return 0;
                return modularCar.net.ID.Value;
            }

            internal object CanMoveCarModule(ulong moduleId)
            {
                HashSet<ModuleBaseClass> moduleBaseClasses = customModules.Where(x => x != null && x.baseVehicleModule.net.ID.Value == moduleId);

                if (moduleBaseClasses.Count > 0 && moduleBaseClasses.Any(x => x != null && !x.CanMoveModule()))
                    return false;

                return null;
            }

            internal ModuleBaseClass GetModuleByEntId(ulong netId)
            {
                return customModules.FirstOrDefault(x => x != null && x.IsOwnEntity(netId));
            }

            internal ModuleBaseClass GetModuleByNetId(ulong netId)
            {
                return customModules.FirstOrDefault(x => x != null && x.baseVehicleModule.IsExists() && x.baseVehicleModule.net != null && x.baseVehicleModule.net.ID.Value == netId);
            }

            internal void OnPlayerMounted()
            {
                foreach (ModuleBaseClass moduleBaseClass in customModules) moduleBaseClass.AwakeClass();

                if (modularCar.transform.position.y < 0f && modularCar.GetFuelSystem().HasFuel())
                {
                    modularCar.engineController.owner.SetFlag(BaseEntity.Flags.On, true, false, true);
                }
            }

            internal void OnPlayerDismounted()
            {
                if (!modularCar.AnyMounted()) foreach (ModuleBaseClass moduleBaseClass in customModules)
                        moduleBaseClass.Sleep();
            }

            internal bool PlayerHasKey(BasePlayer player) => modularCar.PlayerHasUnlockPermission(player);

            internal void OnChildEntityTakeDamage(ulong netId, HitInfo info)
            {
                ModuleBaseClass moduleBaseClass = GetModuleByEntId(netId);
                if (moduleBaseClass != null) moduleBaseClass.OnChildEntityTakeDamage(info);
            }

            internal void Init(ModularCar modularCar)
            {
                this.modularCar = modularCar;
                LoadCarData();
                UpdateModules();
            }

            internal void SaveCar()
            {
                if (!modularCar.IsExists()) return;
                customCarSaveData = ins.customCarSaveDatas.FirstOrDefault(x => x != null && x.netId == modularCar.net.ID.Value);
                if (customCarSaveData == null)
                {
                    customCarSaveData = new CustomCarSaveData(modularCar.net.ID.Value);
                    ins.customCarSaveDatas.Add(customCarSaveData);
                }
                customCarSaveData.modules.Clear();
                foreach (ModuleBaseClass moduleBaseClass in customModules) if (moduleBaseClass != null) moduleBaseClass.Save();
            }

            internal void LoadCarData()
            {
                customCarSaveData = ins.customCarSaveDatas.FirstOrDefault(x => x != null && x.netId == modularCar.net.ID.Value);
            }

            #region Build
            internal void UpdateModules()
            {
                foreach (BaseVehicleModule attachModule in modularCar.AttachedModuleEntities.Where(x => x != null && x.AssociatedItemInstance != null))
                {
                    TryUpdateItem(attachModule.AssociatedItemInstance);
                    TryAddCustomModuleToCar(attachModule.AssociatedItemInstance);
                }
                underwater = customModules.Any(x => x != null && x.baseModuleConfig.underwaerEngine);
                customModules.RemoveWhere(x => x == null);
                SaveCar();
            }

            internal void TryUpdateItem(Item item)
            {
                if (item.skin == 0 && !ins.fullLoad && customCarSaveData != null)
                {
                    HashSet<ItemConfig> itemConfigs = ins._config.customItems.Where(x => x.shortname == item.info.shortname);
                    if (itemConfigs.Count == 0) return;
                    CustomCarModuleSaveData customCarModuleSaveData = customCarSaveData.modules.FirstOrDefault(x => itemConfigs.Any(y => y.customShortname == x.customShortname && y.shortname == item.info.shortname) && !modularCar.AttachedModuleEntities.Any(z => z != null && z.AssociatedItemInstance.uid.Value == x.itemuid));
                    if (customCarModuleSaveData == null) return;
                    customCarModuleSaveData.itemuid = item.uid.Value;
                    ItemConfig itemConfig = itemConfigs.FirstOrDefault(x => x.customShortname == customCarModuleSaveData.customShortname);
                    if (itemConfig == null) return;
                    if (itemConfig.name != "") item.name = itemConfig.name;
                    item.skin = itemConfig.skin;
                }
            }

            void TryAddCustomModuleToCar(Item item)
            {
                if (item == null || customModules.Any(x => x != null && x.IsOwnItem(item.uid.Value)))
                    return;

                BaseVehicleModule baseVehicleModule = FindVehicleModule(item);
                if (baseVehicleModule == null)
                    return;

                HashSet<ItemConfig> customItemsForItem = ins._config.customItems.Where(x => x.shortname == item.info.shortname && x.skin == item.skin);

                TurretModuleConfig turretModuleConfig = ins._config.turretModules.FirstOrDefault(x => customItemsForItem.Any(y => y.customShortname == x.customItemShortname));
                if (turretModuleConfig != null) TryAddTurretModule(turretModuleConfig, baseVehicleModule);

                BuoyancyModuleConfig buoyancyModuleConfig = ins._config.buoyancyModules.FirstOrDefault(x => customItemsForItem.Any(y => y.customShortname == x.customItemShortname));
                if (buoyancyModuleConfig != null) TryAddBuoyancyModule(buoyancyModuleConfig, baseVehicleModule);

                BoostModuleConfig boostModuleConfig = ins._config.boostModules.FirstOrDefault(x => customItemsForItem.Any(y => y.customShortname == x.customItemShortname));
                if (boostModuleConfig != null) TryAddBoostModule(boostModuleConfig, baseVehicleModule);

                RemoteControlModuleConfig remoteControlModuleConfig = ins._config.remoteControlModuleConfigs.FirstOrDefault(x => customItemsForItem.Any(y => y.customShortname == x.customItemShortname));
                if (remoteControlModuleConfig != null) TryAddRemoteModule(remoteControlModuleConfig, baseVehicleModule);

                BaseModuleConfig baseModuleConfig = ins._config.regularModules.FirstOrDefault(x => customItemsForItem.Any(y => y.customShortname == x.customItemShortname));
                if (baseModuleConfig != null) TryAddBaseModule(baseModuleConfig, baseVehicleModule);
            }

            void TryAddTurretModule(TurretModuleConfig turretModuleConfig, BaseVehicleModule baseVehicleModule)
            {
                TurretModuleClass newTurretModule = baseVehicleModule.gameObject.AddComponent<TurretModuleClass>();
                newTurretModule.InitTurretModule(turretModuleConfig, baseVehicleModule, this);
                customModules.Add(newTurretModule);
            }

            void TryAddBuoyancyModule(BuoyancyModuleConfig buoyancyModuleConfig, BaseVehicleModule baseVehicleModule)
            {
                BuoyancyModuleClass newBuoyancyModule = baseVehicleModule.gameObject.AddComponent<BuoyancyModuleClass>();
                newBuoyancyModule.InitBuoyancyModule(buoyancyModuleConfig, baseVehicleModule, this);
                customModules.Add(newBuoyancyModule);
            }

            void TryAddBoostModule(BoostModuleConfig boostModuleConfig, BaseVehicleModule baseVehicleModule)
            {
                BoostModuleClass newBuoyancyModule = baseVehicleModule.gameObject.AddComponent<BoostModuleClass>();
                newBuoyancyModule.InitBoostModule(boostModuleConfig, baseVehicleModule, this);
                customModules.Add(newBuoyancyModule);
            }

            void TryAddRemoteModule(RemoteControlModuleConfig remoteModuleConfig, BaseVehicleModule baseVehicleModule)
            {
                RemoteModuleClass newRemoteModule = baseVehicleModule.gameObject.AddComponent<RemoteModuleClass>();
                newRemoteModule.InitRemoteModule(remoteModuleConfig, baseVehicleModule, this);
                customModules.Add(newRemoteModule);
            }

            void TryAddBaseModule(BaseModuleConfig baseModuleConfig, BaseVehicleModule baseVehicleModule)
            {
                ModuleBaseClass newModuleBaseClass = baseVehicleModule.gameObject.AddComponent<ModuleBaseClass>();
                newModuleBaseClass.Init(baseModuleConfig, baseVehicleModule, this);
                customModules.Add(newModuleBaseClass);
            }

            BaseVehicleModule FindVehicleModule(Item item)
            {
                return modularCar.AttachedModuleEntities.FirstOrDefault(x => x != null && x.AssociatedItemInstance.uid == item.uid);
            }
            #endregion Build

            void OnDestroy()
            {
                foreach (ModuleBaseClass moduleBase in customModules)
                    if (moduleBase != null)
                        Destroy(moduleBase);
            }
        }

        class RemoteModuleClass : ModuleBaseClass
        {
            internal const uint dummySkinID = 3432446;
            RemoteControlModuleConfig remoteConfig;
            Coroutine remoteCorountine;
            CCTV_RC camera;
            RFBroadcaster rfBroadcaster;
            BasePlayer dummy;

            internal void InitRemoteModule(RemoteControlModuleConfig remoteConfig, BaseVehicleModule baseVehicleModule, CustomCarClass customCarClass)
            {
                this.remoteConfig = remoteConfig;
                Init(remoteConfig, baseVehicleModule, customCarClass);
                CreateBroadcaster();
                CreateCamera();
            }

            internal override void Save()
            {
                base.Save();
                if (rfBroadcaster == null) return;
                customCarModuleSaveData.cameraId = rfBroadcaster.frequency.ToString();
            }

            internal void UpdateFrequency(string frequency)
            {
                if (camera != null) camera.UpdateIdentifier(frequency, false);
            }

            internal void StartControl(BasePlayer player)
            {
                if (ins._config.otherPluginsConfig.vehicleDeployedLocksConfig.enable && ins.plugins.Exists("VehicleDeployedLocks") && !(bool)ins.VehicleDeployedLocks.Call("API_CanAccessVehicle", player, customCarClass.modularCar, true))
                {
                    return;
                }
                if (this.customCarClass.playerController != null)
                {
                    return;
                }
                this.customCarClass.playerController = player;
                SpawnDummy();

                if (remoteCorountine == null) remoteCorountine = ServerMgr.Instance.StartCoroutine(RemoteCorountine());
                if (baseVehicleModule.Vehicle.engineController != null) baseVehicleModule.Vehicle.engineController.TryStartEngine(dummy);
                CheckLight();
            }

            internal void StopControl()
            {
                customCarClass.playerController = null;
                KillDummy();
                if (remoteCorountine != null)
                    ServerMgr.Instance.StopCoroutine(remoteCorountine);
                remoteCorountine = null;
            }

            void CreateCamera()
            {
                Vector3 localPosition = remoteConfig.cameraLocationConfig.position.ToVector3();
                Vector3 localRotation = remoteConfig.cameraLocationConfig.rotation.ToVector3();

                camera = BuildManager.SpawnChildEntity(baseVehicleModule, "assets/prefabs/deployable/cctvcamera/cctv_deployed.prefab", localPosition, localRotation, isDecor: false) as CCTV_RC;
                camera.isStatic = true;
                camera.clientLerpSpeed = 0;
                camera.serverLerpSpeed = 0;
                camera.turnSpeed = 0;

                camera.hasPTZ = false;
                camera.decay = null;
                camera.InitializeHealth(float.MaxValue, float.MaxValue);
                camera.UpdateFromInput(5, 0);
                camera.rcIdentifier = rfBroadcaster.frequency.ToString();
                attachedEntities.Add(camera);
            }

            void CreateBroadcaster()
            {
                Vector3 localPosition = remoteConfig.broadcasterLocationConfig.position.ToVector3();
                Vector3 localRotation = remoteConfig.broadcasterLocationConfig.rotation.ToVector3();

                rfBroadcaster = BuildManager.SpawnChildEntity(baseVehicleModule, "assets/prefabs/deployable/playerioents/gates/rfbroadcaster/rfbroadcaster.prefab", localPosition, localRotation, isDecor: false) as RFBroadcaster;
                attachedEntities.Add(rfBroadcaster);
                rfBroadcaster.decay = null;
                rfBroadcaster.InitializeHealth(float.MaxValue, float.MaxValue);
                rfBroadcaster.limitNetworking = true;
                rfBroadcaster.limitNetworking = false;
                rfBroadcaster.frequency = customCarModuleSaveData != null && customCarModuleSaveData.cameraId != "" ? Int32.Parse(customCarModuleSaveData.cameraId) : UnityEngine.Random.Range(0, 9999);
            }

            void SpawnDummy()
            {
                KillDummy();
                BaseVehicle.MountPointInfo mountInfo = baseVehicleModule.Vehicle.allMountPoints.FirstOrDefault(x => x.isDriver && !x.mountable.AnyMounted());
                if (mountInfo == null) return;
                dummy = GameManager.server.CreateEntity("assets/prefabs/player/player.prefab", baseVehicleModule.transform.position) as BasePlayer;
                dummy.enableSaving = false;
                dummy.skinID = dummySkinID;
                dummy.Spawn();
                dummy.InitializeHealth(float.MaxValue, float.MaxValue);
                dummy.limitNetworking = true;
                mountInfo.mountable.MountPlayer(dummy);
            }

            void KillDummy()
            {
                if (dummy.IsExists())
                {
                    foreach (BaseVehicle.MountPointInfo allMountPoint in baseVehicleModule.allMountPoints)
                    {
                        if (allMountPoint.mountable != null && allMountPoint.isDriver) allMountPoint.mountable.DismountAllPlayers();
                    }
                    dummy.Kill();
                }
                customCarClass.OnPlayerDismounted();
            }

            IEnumerator RemoteCorountine()
            {
                while (CheckDriver() && CheckCar())
                {
                    baseVehicleModule.Vehicle.PlayerServerInput(customCarClass.playerController.serverInput, dummy);
                    NightLightCheck();
                    yield return CoroutineEx.waitForSeconds(0.075f);
                }
                StopControl();
            }

            internal void CheckLight()
            {
                if (ins._config.mainConfig.lightOperatonModeAtRemote == 2)
                    baseVehicleModule.Vehicle.LightToggle(dummy);
            }

            bool CheckDriver()
            {
                return dummy.IsExists() && customCarClass.playerController.IsExists() && customCarClass.playerController.isMounted && !customCarClass.playerController.IsSleeping();
            }

            bool CheckCar()
            {
                return baseVehicleModule.health >= baseVehicleModule.MaxHealth() * baseModuleConfig.minHealthPercent / 100;
            }

            void NightLightCheck()
            {
                if (ins._config.mainConfig.lightOperatonModeAtRemote != 1)
                    return;

                if (baseVehicleModule.Vehicle.LightsAreOn)
                {
                    if (ConVar.Env.time > 9 && ConVar.Env.time < 18) baseVehicleModule.Vehicle.LightToggle(dummy);
                }

                if (!baseVehicleModule.Vehicle.LightsAreOn)
                {
                    if (ConVar.Env.time < 9 || ConVar.Env.time > 18) baseVehicleModule.Vehicle.LightToggle(dummy);
                }
            }

            override internal void OnDestroy()
            {
                KillDummy();
                StopControl();
                base.OnDestroy();
            }
        }

        class BoostModuleClass : ModuleBaseClass
        {
            BoostModuleConfig boostModuleConfig;
            Coroutine boostCorountine;
            HashSet<BaseEntity> rochetLaunchers = new HashSet<BaseEntity>();
            HashSet<BaseEntity> fireEntities = new HashSet<BaseEntity>();
            float boostScale;

            internal void InitBoostModule(BoostModuleConfig boostModuleConfig, BaseVehicleModule baseVehicleModule, CustomCarClass customCarClass)
            {
                this.boostModuleConfig = boostModuleConfig;
                Init(boostModuleConfig, baseVehicleModule, customCarClass);
                SpawnRocketLaunchers();
                boostScale = 35000 * boostModuleConfig.boostScale;
            }

            void SpawnRocketLaunchers()
            {
                foreach (LocationConfig locationConfig in boostModuleConfig.rocketLauncherLocations)
                {
                    Vector3 localPosition = locationConfig.position.ToVector3();
                    Vector3 localRotation = locationConfig.rotation.ToVector3();

                    BaseEntity entity = BuildManager.SpawnChildEntity(baseVehicleModule, "assets/prefabs/weapons/rocketlauncher/rocket_launcher.entity.prefab", localPosition, localRotation, isDecor: true);
                    rochetLaunchers.Add(entity);
                }
            }

            internal override void AwakeClass()
            {
                if (boostCorountine == null) boostCorountine = ServerMgr.Instance.StartCoroutine(BoostCorountine());
            }

            IEnumerator BoostCorountine()
            {
                while (baseVehicleModule != null && baseVehicleModule.Vehicle.HasDriver() && baseVehicleModule.Vehicle.GetFuelSystem().HasFuel() && baseVehicleModule.health >= baseVehicleModule.MaxHealth() * baseModuleConfig.minHealthPercent / 100)
                {
                    PlayerBoostControl();
                    yield return CoroutineEx.waitForSeconds(0.2f);
                }
                boostCorountine = null;
                DeleteFires();
            }

            void PlayerBoostControl()
            {
                if (!baseVehicleModule.Vehicle.engineController.IsOn) return;
                BasePlayer driver = baseVehicleModule.Vehicle.GetDriver();
                if (driver == null) return;
                InputState inputState = driver.userID.IsSteamId() ? driver.serverInput : customCarClass.playerController.serverInput;
                if (inputState.IsDown(BUTTON.SPRINT))
                {
                    Effect.server.Run("assets/bundled/prefabs/fx/impacts/blunt/snow/snow.prefab", baseVehicleModule.transform.position);
                    Vector3 force = baseVehicleModule.Vehicle.transform.forward.normalized * boostScale;
                    if (boostModuleConfig.reduceEffectivenessModuleAfterdamage) force *= baseVehicleModule.health / baseVehicleModule.MaxHealth();
                    baseVehicleModule.Vehicle.rigidBody.AddForce(force, ForceMode.Force);
                    CreateFires();
                    TakeFuel();
                }
                else DeleteFires();
            }

            void TakeFuel()
            {
                EntityFuelSystem entityFuelSystem = baseVehicleModule.Vehicle.GetFuelSystem() as EntityFuelSystem;
                entityFuelSystem.pendingFuel = +boostModuleConfig.fuelConsScale;
            }

            void CreateFires()
            {
                if (!fireEntities.IsEmpty()) return;
                foreach (BaseEntity entity in rochetLaunchers)
                {
                    if (entity != null) CreateFire(entity);
                }
            }

            void CreateFire(BaseEntity entity)
            {
                BaseEntity fireEntity = GameManager.server.CreateEntity("assets/prefabs/weapons/flamethrower/flamethrower.entity.prefab");
                fireEntity.enableSaving = false;
                fireEntity.SetFlag(BaseEntity.Flags.Reserved8, true);
                fireEntity.SetParent(entity);
                fireEntity.transform.localPosition = new Vector3(0.253f, 0.393f, -0.106f);
                fireEntity.transform.localEulerAngles = new Vector3(341.829f, 6.471f, 47.888f);
                fireEntity.Spawn();
                fireEntity.SetFlag(BaseEntity.Flags.Reserved8, true);
                fireEntities.Add(fireEntity);
            }

            void DeleteFires()
            {
                if (fireEntities.IsEmpty()) return;
                foreach (BaseEntity entity in fireEntities) if (entity.IsExists()) entity.Kill();
                fireEntities.Clear();
            }

            override internal void OnDestroy()
            {
                if (boostCorountine != null) ServerMgr.Instance.StopCoroutine(boostCorountine);
                DeleteFires();
                base.OnDestroy();
            }
        }

        class TurretModuleClass : ModuleBaseClass
        {
            internal const uint turretSkinID = 3432445;
            TurretModuleConfig turretModuleConfig;
            List<AutoTurret> turrets = new List<AutoTurret>();

            internal override bool CanMoveModule()
            {
                if (turrets.Any(x => x != null && x.inventory.itemList.Count > 0)) return false;
                else return true;
            }

            internal void InitTurretModule(TurretModuleConfig turretModuleConfig, BaseVehicleModule baseVehicleModule, CustomCarClass customCarClass)
            {
                this.turretModuleConfig = turretModuleConfig;
                Init(turretModuleConfig, baseVehicleModule, customCarClass);
                SpawnTurrets();
                LoadTurrets();
                if (baseVehicleModule.Vehicle.AnyMounted()) AwakeClass();
            }

            internal override void Save()
            {
                base.Save();
                List<TurretData> turretDatas = new List<TurretData>();
                foreach (AutoTurret autoTurret in turrets)
                {
                    if (autoTurret == null) continue;

                    string weaponshortname = "";
                    Item weaponItem = autoTurret.inventory.GetSlot(0);
                    if (weaponItem != null) weaponshortname = weaponItem.info.shortname;

                    Dictionary<string, int> ammoCounts = new Dictionary<string, int>();
                    foreach (Item item in autoTurret.inventory.itemList)
                    {
                        if (item.info.shortname == weaponshortname) continue;
                        if (ammoCounts.ContainsKey(item.info.shortname)) ammoCounts[item.info.shortname] += item.amount;
                        else ammoCounts.Add(item.info.shortname, item.amount);
                    }

                    List<ulong> authedPalyerIDs = new List<ulong>();
                    foreach (PlayerNameID playerNameID in autoTurret.authorizedPlayers) authedPalyerIDs.Add(playerNameID.userid);

                    TurretData turretData = new TurretData(weaponshortname, authedPalyerIDs, ammoCounts);
                    turretDatas.Add(turretData);
                }
                customCarModuleSaveData.turretDatas = turretDatas;
            }

            void SpawnTurrets()
            {
                foreach (LocationConfig locationConfig in turretModuleConfig.turretlocations)
                {
                    Vector3 localPosition = locationConfig.position.ToVector3();
                    Vector3 localRotation = locationConfig.rotation.ToVector3();

                    AutoTurret turret = BuildManager.SpawnChildEntity(baseVehicleModule, "assets/prefabs/npc/autoturret/autoturret_deployed.prefab", localPosition, localRotation, isDecor: false) as AutoTurret;
                    turret.skinID = turretSkinID;
                    turret.gameObject.layer = (int)Rust.Layer.Vehicle_Detailed;
                    turrets.Add(turret);
                    attachedEntities.Add(turret);
                }
            }

            void LoadTurrets()
            {
                if (customCarModuleSaveData == null)
                    return;

                for (int i = 0; i < customCarModuleSaveData.turretDatas.Count && i < turrets.Count; i++)
                {
                    TurretData turretData = customCarModuleSaveData.turretDatas[i];
                    AutoTurret autoTurret = turrets[i];

                    if (autoTurret == null)
                        return;

                    if (turretData.weaponShortname != "")
                        autoTurret.inventory.Insert(ItemManager.CreateByName(turretData.weaponShortname));

                    foreach (ulong userId in turretData.authedPlayerIDs)
                    {
                        BasePlayer player = BasePlayer.allPlayerList.FirstOrDefault(x => x != null && x.userID == userId);

                        if (player == null)
                            continue;

                        PlayerNameID playerNameID = new PlayerNameID();
                        playerNameID.userid = player.userID;
                        playerNameID.username = player.displayName;
                        autoTurret.authorizedPlayers.Add(playerNameID);
                    }

                    foreach (var pair in turretData.ammoCounts)
                    {
                        autoTurret.inventory.Insert(ItemManager.CreateByName(pair.Key, pair.Value));
                    }
                }
            }

            internal override void Sleep()
            {
                foreach (AutoTurret autoTurret in turrets)
                {
                    if (autoTurret != null) autoTurret.UpdateFromInput(0, 0);
                }
            }

            internal override void AwakeClass()
            {
                if (baseVehicleModule.health <= baseVehicleModule._maxHealth * turretModuleConfig.minHealthPercent / 100)
                {
                    Sleep();
                    return;
                }
                foreach (AutoTurret autoTurret in turrets)
                {
                    autoTurret.ClearConnections();
                    if (autoTurret == null) continue;
                    autoTurret.UpdateFromInput(100, 0);

                    foreach (BaseVehicle.MountPointInfo mountPointInfo in baseVehicleModule.Vehicle.allMountPoints)
                    {
                        if (mountPointInfo != null && mountPointInfo.mountable._mounted != null)
                        {
                            PlayerNameID playerNameID = new PlayerNameID();
                            playerNameID.userid = mountPointInfo.mountable._mounted.userID;
                            playerNameID.username = mountPointInfo.mountable._mounted.displayName;
                            autoTurret.authorizedPlayers.Add(playerNameID);
                        }
                    }
                }
            }

            protected override void DestroyComponents()
            {
                foreach (AutoTurret autoTurret in turrets)
                    if (autoTurret.IsExists())
                        autoTurret.Kill();
            }
        }

        class BuoyancyModuleClass : ModuleBaseClass
        {
            BuoyancyModuleConfig buoyancyModuleConfig;
            Coroutine buoyancyCorountine;
            bool doubleModule = false;
            bool inWater = false;

            internal void InitBuoyancyModule(BuoyancyModuleConfig buoyancyModuleConfig, BaseVehicleModule baseVehicleModule, CustomCarClass customCarClass)
            {
                this.buoyancyModuleConfig = buoyancyModuleConfig;
                Init(buoyancyModuleConfig, baseVehicleModule, customCarClass);
                if (baseVehicleModule.Vehicle.HasDriver() || baseVehicleModule.Vehicle.transform.position.y < 0) AwakeClass();
                doubleModule = baseVehicleModule.AssociatedItemDef.shortname.Contains("2mod");
            }

            internal override void AwakeClass()
            {
                if (buoyancyCorountine == null) buoyancyCorountine = ServerMgr.Instance.StartCoroutine(BuoyancyCorountine());
            }

            IEnumerator BuoyancyCorountine()
            {
                while (baseVehicleModule != null && (baseVehicleModule.Vehicle.HasDriver() || baseVehicleModule.Vehicle.transform.position.y <= 0) && baseVehicleModule.health > baseVehicleModule._maxHealth * buoyancyModuleConfig.minHealthPercent / 100)
                {
                    if (baseVehicleModule.Vehicle.transform.position.y <= -0.35f)
                    {
                        AddBuoyancy();
                        PlayerWaterControl();
                        if (!inWater) InWater();
                    }
                    else if (inWater) OutWater();
                    yield return CoroutineEx.waitForSeconds(0.1f);
                }
                if (inWater) OutWater();
                buoyancyCorountine = null;
            }

            void InWater()
            {
                baseVehicleModule.Vehicle.rigidBody.maxAngularVelocity = 0.4f;
                inWater = true;
            }

            void OutWater()
            {
                baseVehicleModule.Vehicle.rigidBody.maxAngularVelocity = 0.7f;
                inWater = false;
            }

            void AddBuoyancy()
            {
                float currentDepth = -baseVehicleModule.transform.position.y + 0.35f;
                if (currentDepth > 1) currentDepth = 1;

                Vector3 force = new Vector3(0f, currentDepth * 10000 * buoyancyModuleConfig.buoyancy, 0f);
                if (buoyancyModuleConfig.reduceEffectivenessModuleAfterdamage) force *= baseVehicleModule.health / baseVehicleModule.MaxHealth();
                if (doubleModule)
                {
                    baseVehicleModule.Vehicle.rigidBody.AddForceAtPosition(force, baseVehicleModule.CenterPoint() + baseVehicleModule.transform.forward.normalized * 2.15f, ForceMode.Force);
                    baseVehicleModule.Vehicle.rigidBody.AddForceAtPosition(force, baseVehicleModule.CenterPoint() - baseVehicleModule.transform.forward.normalized * 2f, ForceMode.Force);
                }
                baseVehicleModule.Vehicle.rigidBody.AddForceAtPosition(force, baseVehicleModule.CenterPoint() + baseVehicleModule.transform.right.normalized * 2, ForceMode.Force);
                baseVehicleModule.Vehicle.rigidBody.AddForceAtPosition(force, baseVehicleModule.CenterPoint() - baseVehicleModule.transform.right.normalized * 2, ForceMode.Force);
            }

            void PlayerWaterControl()
            {
                if (!baseVehicleModule.Vehicle.engineController.IsOn) return;

                BasePlayer driver = baseVehicleModule.Vehicle.GetDriver();
                if (driver == null) return;

                InputState inputState = driver.userID.IsSteamId() ? driver.serverInput : customCarClass.playerController.serverInput;

                ControlGas(inputState);
                ControlRotate(inputState);
            }

            void ControlGas(InputState inputState)
            {
                if (inputState.IsDown(BUTTON.FORWARD)) baseVehicleModule.Vehicle.rigidBody.AddForce(transform.forward.normalized * 6000 * buoyancyModuleConfig.boostScale, ForceMode.Force);
                else if (inputState.IsDown(BUTTON.BACKWARD)) baseVehicleModule.Vehicle.rigidBody.AddForce(-transform.forward.normalized * 6000 * buoyancyModuleConfig.boostScale, ForceMode.Force);
            }

            void ControlRotate(InputState inputState)
            {
                float multiplicator = buoyancyModuleConfig.rotateScale * baseVehicleModule.Vehicle.rigidBody.velocity.magnitude * 50;
                if (Vector3.Angle(baseVehicleModule.Vehicle.rigidBody.velocity, baseVehicleModule.transform.forward) > 90) multiplicator *= -1;
                if (multiplicator > 300 * buoyancyModuleConfig.rotateScale) multiplicator = 300 * buoyancyModuleConfig.rotateScale;
                if (inputState.IsDown(BUTTON.RIGHT)) baseVehicleModule.Vehicle.rigidBody.AddTorque(transform.up.normalized * multiplicator, ForceMode.Force);
                if (inputState.IsDown(BUTTON.LEFT)) baseVehicleModule.Vehicle.rigidBody.AddTorque(transform.up.normalized * -multiplicator, ForceMode.Force);
            }

            override internal void OnDestroy()
            {
                if (buoyancyCorountine != null) ServerMgr.Instance.StopCoroutine(buoyancyCorountine);
                base.OnDestroy();
            }
        }

        class ModuleBaseClass : FacepunchBehaviour
        {
            internal BaseModuleConfig baseModuleConfig;
            internal BaseVehicleModule baseVehicleModule;
            internal CustomCarClass customCarClass;
            internal HashSet<BaseEntity> attachedEntities = new HashSet<BaseEntity>();
            protected CustomCarModuleSaveData customCarModuleSaveData;

            internal bool IsOwnItem(ulong uid) => baseVehicleModule.AssociatedItemInstance.uid.Value == uid;

            internal bool IsOwnEntity(ulong uid) => attachedEntities.Any(x => x != null && x.net != null && x.net.ID.Value == uid);

            internal void OnChildEntityTakeDamage(HitInfo info)
            {
                baseVehicleModule.AcceptPropagatedDamage(info.damageTypes.Total(), Rust.DamageType.Bullet);
                OnModuleTakeDamage();
            }

            internal void OnModuleTakeDamage()
            {
                if (baseVehicleModule.health <= baseVehicleModule.MaxHealth() * baseModuleConfig.minHealthPercent / 100)
                    Sleep();
            }

            internal virtual bool CanMoveModule() => true;

            internal void Init(BaseModuleConfig baseModuleConfig, BaseVehicleModule baseVehicleModule, CustomCarClass customCarClass)
            {
                this.baseModuleConfig = baseModuleConfig;
                this.baseVehicleModule = baseVehicleModule;
                this.customCarClass = customCarClass;
                LoadModuleData();
                CreateEntities();
                AwakeClass();
            }

            void LoadModuleData()
            {
                if (customCarClass.customCarSaveData == null) return;
                customCarModuleSaveData = customCarClass.customCarSaveData.modules.FirstOrDefault(x => x != null && x.itemuid == baseVehicleModule.AssociatedItemInstance.uid.Value);
            }

            protected void CreateEntities()
            {
                foreach (var pair in baseModuleConfig.entities)
                {
                    if (pair.Key == "diving.tank")
                    {
                        foreach (LocationConfig locationConfig in pair.Value)
                        {
                            Vector3 localPosition = locationConfig.position.ToVector3();
                            Vector3 localRotation = locationConfig.rotation.ToVector3();

                            BaseEntity entity = BuildManager.CreateChildDroppedItem(baseVehicleModule, pair.Key, localPosition, localRotation);
                            attachedEntities.Add(entity);
                        }
                    }
                    else
                    {
                        foreach (LocationConfig locationConfig in pair.Value)
                        {
                            Vector3 localPosition = locationConfig.position.ToVector3();
                            Vector3 localRotation = locationConfig.rotation.ToVector3();

                            BaseEntity entity = BuildManager.SpawnChildEntity(baseVehicleModule, pair.Key, localPosition, localRotation, isDecor: true);
                            attachedEntities.Add(entity);
                        }
                    }
                }
            }

            internal virtual void Save()
            {
                customCarModuleSaveData = customCarClass.customCarSaveData.modules.FirstOrDefault(x => x != null && x.itemuid == baseVehicleModule.AssociatedItemInstance.uid.Value);
                if (customCarModuleSaveData == null)
                {
                    customCarModuleSaveData = new CustomCarModuleSaveData(baseVehicleModule.AssociatedItemInstance.uid.Value, new List<TurretData>(), "");
                    customCarClass.customCarSaveData.modules.Add(customCarModuleSaveData);
                }
                customCarModuleSaveData.customShortname = baseModuleConfig.customItemShortname;
            }

            internal virtual void Sleep()
            {

            }

            internal virtual void AwakeClass()
            {

            }

            virtual internal void OnDestroy()
            {
                if (!ins.unloading) Save();
                DestroyComponents();
            }

            protected virtual void DestroyComponents()
            {
                foreach (BaseEntity baseEntity in attachedEntities)
                    if (baseEntity.IsExists()) baseEntity.Kill();
            }
        }

        sealed class MovableDroppedItem : DroppedItem
        {
            public override void OnParentChanging(BaseEntity oldParent, BaseEntity newParent)
            {

            }

            public override float MaxVelocity()
            {
                return 100;
            }

            public override float GetDespawnDuration()
            {
                return float.MaxValue;
            }
        }

        static class BuildManager
        {
            internal static BaseEntity CreateChildDroppedItem(BaseEntity parrentEntity, string shortname, Vector3 localPosition, Vector3 localRotation)
            {
                MovableDroppedItem droppedItem = CreateMovableDroppedItem(shortname, parrentEntity.transform.position, parrentEntity.transform.rotation);

                droppedItem.SetParent(parrentEntity, false, true);
                droppedItem.transform.localPosition = localPosition;
                droppedItem.transform.localEulerAngles = localRotation;
                droppedItem.syncPosition = false;

                droppedItem.CancelInvoke(droppedItem.IdleDestroy);
                droppedItem.SendNetworkUpdate();

                return droppedItem;
            }

            internal static MovableDroppedItem CreateMovableDroppedItem(string shortname, Vector3 position, Quaternion rotation)
            {
                DroppedItem droppedItem = GameManager.server.CreateEntity("assets/prefabs/misc/burlap sack/generic_world.prefab", position, rotation) as DroppedItem;
                droppedItem.enableSaving = false;
                droppedItem.allowPickup = false;
                droppedItem.item = ItemManager.CreateByName(shortname);

                droppedItem.StopAllCoroutines();
                MovableDroppedItem movableDroppedItem = droppedItem.gameObject.AddComponent<MovableDroppedItem>();
                BuildManager.CopySerializableFields(droppedItem, movableDroppedItem);
                UnityEngine.GameObject.DestroyImmediate(droppedItem, true);
                droppedItem = movableDroppedItem;

                movableDroppedItem.Spawn();

                DestroyUnnessesaryComponents(droppedItem);
                DestroyEntityConponents<PhysicsEffects>(droppedItem);
                DestroyEntityConponents<EntityCollisionMessage>(droppedItem);

                return movableDroppedItem;
            }


            internal static void UpdateMeshColliders(BaseEntity entity)
            {
                MeshCollider[] meshColliders = entity.GetComponentsInChildren<MeshCollider>();

                for (int i = 0; i < meshColliders.Length; i++)
                {
                    MeshCollider meshCollider = meshColliders[i];
                    meshCollider.convex = true;
                }
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, LocationConfig locationConfig, ulong skinId, bool isDecor)
            {
                Vector3 localPosition = locationConfig.position.ToVector3();
                Vector3 localRotation = locationConfig.rotation.ToVector3();
                return SpawnChildEntity(parrentEntity, prefabName, localPosition, localRotation, skinId, isDecor);
            }

            internal static BaseEntity SpawnRegularEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);
                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnStaticEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, false);
                DestroyUnnessesaryComponents(entity);

                StabilityEntity stabilityEntity = entity as StabilityEntity;
                if (stabilityEntity != null)
                    stabilityEntity.grounded = true;

                BaseCombatEntity baseCombatEntity = entity as BaseCombatEntity;
                if (baseCombatEntity != null)
                    baseCombatEntity.pickup.enabled = false;

                entity.Spawn();
                return entity;
            }

            internal static BaseEntity SpawnChildEntity(BaseEntity parrentEntity, string prefabName, Vector3 localPosition, Vector3 localRotation, ulong skinId = 0, bool isDecor = true, bool enableSaving = false)
            {
                BaseEntity entity = isDecor ? CreateDecorEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId) : CreateEntity(prefabName, parrentEntity.transform.position, Quaternion.identity, skinId, enableSaving);
                SetParent(parrentEntity, entity, localPosition, localRotation);

                DestroyUnnessesaryComponents(entity);

                if (isDecor)
                    DestroyDecorComponents(entity);

                UpdateMeshColliders(entity);
                entity.Spawn();
                return entity;
            }

            internal static void UpdateEntityMaxHealth(BaseCombatEntity baseCombatEntity, float maxHealth)
            {
                baseCombatEntity.startHealth = maxHealth;
                baseCombatEntity.InitializeHealth(maxHealth, maxHealth);
            }

            internal static BaseEntity CreateEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId, bool enableSaving)
            {
                BaseEntity entity = GameManager.server.CreateEntity(prefabName, position, rotation);
                entity.enableSaving = enableSaving;
                entity.skinID = skinId;
                return entity;
            }

            static BaseEntity CreateDecorEntity(string prefabName, Vector3 position, Quaternion rotation, ulong skinId = 0, bool enableSaving = false)
            {
                BaseEntity entity = CreateEntity(prefabName, position, rotation, skinId, enableSaving);

                BaseEntity trueBaseEntity = entity.gameObject.AddComponent<BaseEntity>();
                CopySerializableFields(entity, trueBaseEntity);
                UnityEngine.Object.DestroyImmediate(entity, true);
                entity.SetFlag(BaseEntity.Flags.Busy, true);
                entity.SetFlag(BaseEntity.Flags.Locked, true);

                return trueBaseEntity;
            }

            internal static void SetParent(BaseEntity parrentEntity, BaseEntity childEntity, Vector3 localPosition, Vector3 localRotation)
            {
                childEntity.SetParent(parrentEntity, true, false);
                childEntity.transform.localPosition = localPosition;
                childEntity.transform.localEulerAngles = localRotation;
            }

            static void DestroyDecorComponents(BaseEntity entity)
            {
                Component[] components = entity.GetComponentsInChildren<Component>();

                for (int i = 0; i < components.Length; i++)
                {
                    Component component = components[i];

                    EntityCollisionMessage entityCollisionMessage = component as EntityCollisionMessage;

                    if (entityCollisionMessage != null || (component != null && component.name != entity.PrefabName))
                    {
                        Transform transform = component as Transform;
                        if (transform != null)
                            continue;

                        Collider collider = component as Collider;
                        if (collider != null && collider is MeshCollider == false)
                            continue;

                        if (component is Model)
                            continue;

                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                    }
                }
            }

            static void DestroyUnnessesaryComponents(BaseEntity entity)
            {
                DestroyEntityConponent<GroundWatch>(entity);
                DestroyEntityConponent<DestroyOnGroundMissing>(entity);
                DestroyEntityConponent<TriggerHurtEx>(entity);

                if (entity is BradleyAPC == false)
                    DestroyEntityConponent<Rigidbody>(entity);
            }

            internal static void DestroyEntityConponent<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy component = entity.GetComponent<TypeForDestroy>();
                if (component != null)
                    UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
            }

            internal static void DestroyEntityConponents<TypeForDestroy>(BaseEntity entity)
            {
                if (entity == null)
                    return;

                TypeForDestroy[] components = entity.GetComponentsInChildren<TypeForDestroy>();

                for (int i = 0; i < components.Length; i++)
                {
                    TypeForDestroy component = components[i];

                    if (component != null)
                        UnityEngine.GameObject.DestroyImmediate(component as UnityEngine.Object);
                }
            }

            internal static void CopySerializableFields<T>(T src, T dst)
            {
                FieldInfo[] srcFields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);
                foreach (FieldInfo field in srcFields)
                {
                    object value = field.GetValue(src);
                    field.SetValue(dst, value);
                }
            }
        }

        public class CustomCarSaveData
        {
            public ulong netId;
            public List<CustomCarModuleSaveData> modules = new List<CustomCarModuleSaveData>();

            public CustomCarSaveData(ulong netId)
            {
                this.netId = netId;
            }
        }

        public class CustomCarModuleSaveData
        {
            public ulong itemuid;
            public string customShortname;
            public List<TurretData> turretDatas;
            public string cameraId;

            public CustomCarModuleSaveData(ulong itemuid, List<TurretData> turretDatas, string cameraId)
            {
                this.itemuid = itemuid;
                this.turretDatas = turretDatas;
                this.cameraId = cameraId;
            }
        }

        public class TurretData
        {
            public string weaponShortname;
            public List<ulong> authedPlayerIDs;
            public Dictionary<string, int> ammoCounts;

            public TurretData(string weaponShortname, List<ulong> authedPlayerIDs, Dictionary<string, int> ammoCounts)
            {
                this.weaponShortname = weaponShortname;
                this.authedPlayerIDs = authedPlayerIDs;
                this.ammoCounts = ammoCounts;
            }
        }

        static class CustomCarSpawner
        {
            static Coroutine respawnCorountine;

            internal static void AddMapSpawnPoint(BasePlayer player, string carPresetName)
            {
                LocationConfig locationConfig = new LocationConfig()
                {
                    position = player.transform.position.ToString(),
                    rotation = $"(0, {player.viewAngles.y}, 0)"
                };

                if (carPresetName == "")
                {
                    ins._config.respawnSetting.mapRespawnConfig.randomSpawnLocations.Add(locationConfig);
                }
                else
                {
                    if (!ins._config.respawnSetting.mapRespawnConfig.carPresetLocations.ContainsKey(carPresetName))
                    {
                        ins._config.respawnSetting.mapRespawnConfig.carPresetLocations.Add(carPresetName, new List<LocationConfig> { locationConfig });
                    }
                    else ins._config.respawnSetting.mapRespawnConfig.carPresetLocations[carPresetName].Add(locationConfig);
                }

                ins.SaveConfig();
                NotifyManager.SendMessageToPlayer(player, "AddedMapSpawnPoint", ins._config.prefix);
            }

            internal static void AddMonumentSpawnPoint(BasePlayer player, MonumentInfo monumentInfo, string carPresetName)
            {
                LocationConfig locationConfig = new LocationConfig()
                {
                    position = monumentInfo.transform.InverseTransformPoint(player.transform.position).ToString(),
                    rotation = $"(0, {(player.viewAngles - monumentInfo.transform.rotation.eulerAngles).y}, 0)"
                };

                MonumentRespawnPositionsConfig monumentRespawnPositions = ins._config.respawnSetting.monumentRespawnConfigs.FirstOrDefault(x => x.monumentPreset == monumentInfo.name);
                if (monumentRespawnPositions == null)
                {
                    monumentRespawnPositions = new MonumentRespawnPositionsConfig
                    {
                        monumentPreset = monumentInfo.name,
                        carPresetLocations = new Dictionary<string, List<LocationConfig>>(),
                        randomSpawnLocations = new List<LocationConfig>()
                    };
                    ins._config.respawnSetting.monumentRespawnConfigs.Add(monumentRespawnPositions);
                }

                if (carPresetName == "")
                {
                    monumentRespawnPositions.randomSpawnLocations.Add(locationConfig);
                }
                else
                {
                    if (!monumentRespawnPositions.carPresetLocations.ContainsKey(carPresetName))
                    {
                        monumentRespawnPositions.carPresetLocations.Add(carPresetName, new List<LocationConfig> { locationConfig });
                    }
                    else monumentRespawnPositions.carPresetLocations[carPresetName].Add(locationConfig);
                }

                ins.SaveConfig();
                NotifyManager.SendMessageToPlayer(player, "AddedMomumentSpawnPoint", ins._config.prefix, monumentInfo.name);
            }

            internal static void StartRespawnCorountime()
            {
                if (respawnCorountine != null || !ins._config.respawnSetting.enable) return;
                respawnCorountine = ServerMgr.Instance.StartCoroutine(RespawnCorountine());
            }

            internal static void StopRespawnCorountime()
            {
                if (respawnCorountine != null) ServerMgr.Instance.StopCoroutine(respawnCorountine);
                respawnCorountine = null;
            }

            static IEnumerator RespawnCorountine()
            {
                while (true)
                {
                    yield return CoroutineEx.waitForSeconds(ins._config.respawnSetting.respawnPeriod);

                    foreach (var monumentRespawnConfig in ins._config.respawnSetting.monumentRespawnConfigs)
                    {
                        HashSet<MonumentInfo> monumentInfos = TerrainMeta.Path.Monuments.Where(x => x != null && x.name == monumentRespawnConfig.monumentPreset);
                        foreach (MonumentInfo monumentInfo in monumentInfos)
                        {
                            if (monumentInfo == null) continue;
                            foreach (LocationConfig locationConfig in monumentRespawnConfig.randomSpawnLocations)
                            {
                                SpawnRandomCar(monumentInfo.transform.TransformPoint(locationConfig.position.ToVector3()), monumentInfo.transform.rotation * Quaternion.Euler(locationConfig.rotation.ToVector3()));
                                yield return CoroutineEx.waitForSeconds(0.1f);
                            }
                            foreach (var carLocations in monumentRespawnConfig.carPresetLocations)
                            {
                                foreach (LocationConfig locationConfig in carLocations.Value)
                                {
                                    SpawnCar(carLocations.Key, monumentInfo.transform.TransformPoint(locationConfig.position.ToVector3()), monumentInfo.transform.rotation * Quaternion.Euler(locationConfig.rotation.ToVector3()));
                                    yield return CoroutineEx.waitForSeconds(0.1f);
                                }
                            }
                        }
                    }

                    foreach (LocationConfig locationConfig in ins._config.respawnSetting.mapRespawnConfig.randomSpawnLocations)
                    {
                        SpawnRandomCar(locationConfig.position.ToVector3(), Quaternion.Euler(locationConfig.rotation.ToVector3()));
                        yield return CoroutineEx.waitForSeconds(0.1f);
                    }

                    foreach (var pair in ins._config.respawnSetting.mapRespawnConfig.carPresetLocations)
                    {
                        foreach (LocationConfig locationConfig in pair.Value)
                        {
                            SpawnCar(pair.Key, locationConfig.position.ToVector3(), Quaternion.Euler(locationConfig.rotation.ToVector3()));
                            yield return CoroutineEx.waitForSeconds(0.1f);
                        }
                    }
                }
            }

            static void SpawnRandomCar(Vector3 position, Quaternion rotation)
            {
                if (!ins._config.respawnSetting.presets.Any(y => y.Value > 0)) return;
                string presetName = "";

                while (presetName == "")
                {
                    int randomIndex = UnityEngine.Random.Range(0, ins._config.respawnSetting.presets.Count - 1);
                    var pair = ins._config.respawnSetting.presets.ElementAt(randomIndex);
                    if (UnityEngine.Random.Range(0, 100) < pair.Value)
                    {
                        presetName = pair.Key;
                        break;
                    }
                }
                SpawnCar(presetName, position, rotation);
            }

            internal static void SpawnCar(string carPresetName, Vector3 position, Quaternion rotation, BasePlayer initiator = null)
            {
                if (!CheckPosition(position)) return;

                CarPresetConfig carPresetConfig = ins._config.carPresets.FirstOrDefault(x => x.presetName == carPresetName);
                if (carPresetConfig == null)
                {
                    NotifyManager.PrintError(initiator, "PresetNotFoundExeption", carPresetConfig);
                    return;
                }
                ModularCar car = GameManager.server.CreateEntity(carPresetConfig.carPrefabName, position, rotation) as ModularCar;
                if (car == null)
                {
                    NotifyManager.PrintError(initiator, "PrefabNotFoundExeption", carPresetConfig);
                    return;
                }
                car.spawnSettings.useSpawnSettings = false;
                car.Spawn();
                UpdateCustomCar(car, carPresetConfig);
            }

            static bool CheckPosition(Vector3 position)
            {
                return !ins.customCarClasses.Any(x => x != null && Vector3.Distance(x.GetCarPosition(), position) < 6);
            }

            static void UpdateCustomCar(ModularCar modularCar, CarPresetConfig carPresetConfig)
            {
                if (modularCar == null) return;
                List<string> modules = carPresetConfig.modules;
                for (int socketIndex = 0; socketIndex < modularCar.TotalSockets && socketIndex < modules.Count; socketIndex++)
                {
                    string shortName = modules[socketIndex];
                    if (shortName == "") continue;
                    ItemConfig itemConfig = ins._config.customItems.FirstOrDefault(x => x.customShortname == shortName);
                    Item existingItem = modularCar.Inventory.ModuleContainer.GetSlot(socketIndex);
                    if (existingItem != null) continue;

                    Item moduleItem = itemConfig == null ? ItemManager.CreateByName(shortName) : ItemManager.CreateByName(itemConfig.shortname, skin: itemConfig.skin);
                    if (moduleItem == null) continue;

                    moduleItem.conditionNormalized = 100;
                    if (!modularCar.TryAddModule(moduleItem, socketIndex)) moduleItem.Remove();
                }
                ins.AttachOrUpdateCustomCarClass(modularCar);

                if (carPresetConfig.fuel > 0)
                {
                    EntityFuelSystem entityFuelSystem = modularCar.GetFuelSystem() as EntityFuelSystem;
                    StorageContainer fuelContainer = entityFuelSystem.GetFuelContainer();
                    fuelContainer.inventory.AddItem(fuelContainer.allowedItem, carPresetConfig.fuel);
                }

                if (carPresetConfig.engineComponents)
                {
                    modularCar.Invoke(() => AddEngineParts(modularCar, carPresetConfig.engineComponentsLvl), 1f);
                }
            }

            static void AddEngineParts(ModularCar modularCar, int partsLvl)
            {
                if (modularCar == null) return;
                foreach (BaseVehicleModule module in modularCar.AttachedModuleEntities)
                {
                    VehicleModuleEngine engineModule = module as VehicleModuleEngine;
                    if (engineModule == null) continue;
                    Rust.Modular.EngineStorage engineStorage = engineModule.GetContainer() as Rust.Modular.EngineStorage;
                    if (engineStorage == null) continue;
                    ItemContainer inventory = engineStorage.inventory;
                    for (int i = 0; i < inventory.capacity; i++)
                    {
                        ItemModEngineItem output;
                        if (!engineStorage.allEngineItems.TryGetItem(partsLvl, engineStorage.slotTypes[i], out output)) continue;
                        ItemDefinition component = output.GetComponent<ItemDefinition>();
                        Item item = ItemManager.Create(component);
                        if (item == null) continue;
                        item._maxCondition = int.MaxValue;
                        item.condition = int.MaxValue;
                        item.MoveToContainer(engineStorage.inventory, i, allowStack: false);
                    }
                    engineModule.RefreshPerformanceStats(engineStorage);
                    return;
                }
            }
        }

        static class NotifyManager
        {
            internal static void PrintError(BasePlayer player, string langKey, params object[] args)
            {
                if (player == null) ins.PrintError(ClearColorAndSize(GetMessage(langKey, null, args)));
                else ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }

            internal static string ClearColorAndSize(string message)
            {
                message = message.Replace("</color>", string.Empty);
                message = message.Replace("</size>", string.Empty);
                while (message.Contains("<color="))
                {
                    int index = message.IndexOf("<color=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                while (message.Contains("<size="))
                {
                    int index = message.IndexOf("<size=");
                    message = message.Remove(index, message.IndexOf(">", index) - index + 1);
                }
                return message;
            }

            internal static void SendMessageToPlayer(BasePlayer player, string langKey, params object[] args)
            {
                ins.PrintToChat(player, GetMessage(langKey, player.UserIDString, args));
            }
        }

        static class LootManager
        {
            internal static void TrySpawnItemInDefaultCrate(LootContainer lootContatiner, ItemConfig itemConfig, int removeItemIndex = 0)
            {
                ins.NextTick(() =>
                {
                    Item item = LootManager.CreateItem(itemConfig, 1);
                    if (lootContatiner.inventory.itemList.Count > removeItemIndex)
                    {
                        Item removeItem = lootContatiner.inventory.itemList[removeItemIndex];
                        if (removeItem != null) lootContatiner.inventory.Remove(removeItem);
                    }
                    if (!item.MoveToContainer(lootContatiner.inventory)) item.Remove();
                });
            }

            internal static Item CreateItem(ItemConfig itemConfig, int amount)
            {
                Item item = ItemManager.CreateByName(itemConfig.shortname, amount, itemConfig.skin);
                if (itemConfig.name != "") item.name = itemConfig.name;
                return item;
            }

            internal static void GiveCustomItemToPlayer(BasePlayer player, ItemConfig itemConfg, int amount)
            {
                Item item = CreateItem(itemConfg, amount);
                GiveItemToPLayer(player, item);
            }

            static void GiveItemToPLayer(BasePlayer player, Item item)
            {
                int spaceCountItem = PlayerInventory.GetSpaceCountItem(player, item.info.shortname, item.MaxStackable(), item.skin);
                int inventoryItemCount;
                if (spaceCountItem > item.amount) inventoryItemCount = item.amount;
                else inventoryItemCount = spaceCountItem;

                if (inventoryItemCount > 0)
                {
                    Item itemInventory = ItemManager.CreateByName(item.info.shortname, inventoryItemCount, item.skin);
                    if (item.skin != 0) itemInventory.name = item.name;

                    item.amount -= inventoryItemCount;
                    PlayerInventory.MoveInventoryItem(player, itemInventory);
                }

                if (item.amount > 0) PlayerInventory.DropExtraItem(player, item);
            }

            internal static ItemConfig GetItemConfigByShornameAndSkin(string shortname, ulong skin)
            {
                return ins._config.customItems.FirstOrDefault(x => x.shortname == shortname && x.skin == skin);
            }

            internal static HashSet<BaseModuleConfig> GetModuleConfigsByCustomItemShortname(string customItemShortname)
            {
                HashSet<BaseModuleConfig> baseModuleConfigs = new HashSet<BaseModuleConfig>();

                TurretModuleConfig turretModuleConfig = ins._config.turretModules.FirstOrDefault(x => x.customItemShortname == customItemShortname);
                if (turretModuleConfig != null)
                    baseModuleConfigs.Add(turretModuleConfig);

                BuoyancyModuleConfig buoyancyModuleConfig = ins._config.buoyancyModules.FirstOrDefault(x => x.customItemShortname == customItemShortname);
                if (buoyancyModuleConfig != null)
                    baseModuleConfigs.Add(buoyancyModuleConfig);

                BoostModuleConfig boostModuleConfig = ins._config.boostModules.FirstOrDefault(x => x.customItemShortname == customItemShortname);
                if (boostModuleConfig != null)
                    baseModuleConfigs.Add(boostModuleConfig);

                RemoteControlModuleConfig remoteControlModuleConfig = ins._config.remoteControlModuleConfigs.FirstOrDefault(x => x.customItemShortname == customItemShortname);
                if (remoteControlModuleConfig != null)
                    baseModuleConfigs.Add(remoteControlModuleConfig);

                BaseModuleConfig baseModuleConfig = ins._config.regularModules.FirstOrDefault(x => x.customItemShortname == customItemShortname);
                if (baseModuleConfig != null)
                    baseModuleConfigs.Add(baseModuleConfig);

                return baseModuleConfigs;
            }

            internal static bool IsPlayerCanInstallItemToCar(string userIdString, string shortname, ulong skin)
            {
                ItemConfig itemConfig = GetItemConfigByShornameAndSkin(shortname, skin);

                if (itemConfig == null)
                    return true;

                HashSet<BaseModuleConfig> customModuleConfigs = GetModuleConfigsByCustomItemShortname(itemConfig.customShortname);
                return !customModuleConfigs.Any(x => x.permissionForPlacement != "" && !PermissionManager.IsUserHasPermission(userIdString, x.permissionForPlacement));
            }

            static class PlayerInventory
            {
                internal static int GetSpaceCountItem(BasePlayer player, string shortname, int stack, ulong skinID)
                {
                    int slots = player.inventory.containerMain.capacity + player.inventory.containerBelt.capacity;
                    int taken = player.inventory.containerMain.itemList.Count + player.inventory.containerBelt.itemList.Count;
                    int result = (slots - taken) * stack;

                    List<Item> allItems = Pool.Get<List<Item>>();
                    player.inventory.GetAllItems(allItems);

                    foreach (Item item in allItems)
                        if (item.info.shortname == shortname && item.skin == skinID && item.amount < stack)
                            result += stack - item.amount;

                    Pool.FreeUnmanaged(ref allItems);

                    return result;
                }

                internal static void MoveInventoryItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable())
                    {
                        List<Item> allItems = Pool.Get<List<Item>>();
                        player.inventory.GetAllItems(allItems);

                        foreach (Item itemInv in allItems)
                        {
                            if (itemInv.info.shortname == item.info.shortname && itemInv.skin == item.skin && itemInv.amount < itemInv.MaxStackable())
                            {
                                if (itemInv.amount + item.amount <= itemInv.MaxStackable())
                                {
                                    itemInv.amount += item.amount;
                                    itemInv.MarkDirty();
                                    return;
                                }
                                else
                                {
                                    item.amount -= itemInv.MaxStackable() - itemInv.amount;
                                    itemInv.amount = itemInv.MaxStackable();
                                }
                            }
                        }

                        Pool.FreeUnmanaged(ref allItems);

                        if (item.amount > 0) player.inventory.GiveItem(item);
                    }
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            player.inventory.GiveItem(thisItem);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) player.inventory.GiveItem(item);
                    }
                }

                internal static void DropExtraItem(BasePlayer player, Item item)
                {
                    if (item.amount <= item.MaxStackable()) item.Drop(player.transform.position, Vector3.up);
                    else
                    {
                        while (item.amount > item.MaxStackable())
                        {
                            Item thisItem = ItemManager.CreateByName(item.info.shortname, item.MaxStackable(), item.skin);
                            if (item.skin != 0) thisItem.name = item.name;
                            thisItem.Drop(player.transform.position, Vector3.up);
                            item.amount -= item.MaxStackable();
                        }
                        if (item.amount > 0) item.Drop(player.transform.position, Vector3.up);
                    }
                }
            }
        }

        static class PermissionManager
        {
            internal static void RegisterPermissions()
            {
                ins.permission.RegisterPermission(ins._config.mainConfig.givePermission, ins);

                foreach (BaseModuleConfig baseModuleConfig in ins._config.turretModules)
                    if (baseModuleConfig.permissionForPlacement != "")
                        ins.permission.RegisterPermission(baseModuleConfig.permissionForPlacement, ins);

                foreach (BaseModuleConfig baseModuleConfig in ins._config.boostModules)
                    if (baseModuleConfig.permissionForPlacement != "")
                        ins.permission.RegisterPermission(baseModuleConfig.permissionForPlacement, ins);

                foreach (BaseModuleConfig baseModuleConfig in ins._config.buoyancyModules)
                    if (baseModuleConfig.permissionForPlacement != "")
                        ins.permission.RegisterPermission(baseModuleConfig.permissionForPlacement, ins);

                foreach (BaseModuleConfig baseModuleConfig in ins._config.remoteControlModuleConfigs)
                    if (baseModuleConfig.permissionForPlacement != "")
                        ins.permission.RegisterPermission(baseModuleConfig.permissionForPlacement, ins);

                foreach (BaseModuleConfig baseModuleConfig in ins._config.regularModules)
                    if (baseModuleConfig.permissionForPlacement != "")
                        ins.permission.RegisterPermission(baseModuleConfig.permissionForPlacement, ins);
            }

            internal static bool IsUserHasPermission(string userIdString, string permissionName)
            {
                return ins.permission.UserHasPermission(userIdString, permissionName);
            }
        }
        #endregion Classes

        #region Lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetModule"] = "{0} You <color=#738d43>got</color> a custom module!",

                ["PresetNotFoundExeption"] = "Preset {0} <color=#ce3f27>not found<color=#ce3f27>!",
                ["PrefabNotFoundExeption"] = "Prefab {0} <color=#ce3f27>not found<color=#ce3f27>!",

                ["AddedMapSpawnPoint"] = "{0} The spawn point of the car has been <color=#738d43>successfully</color> added",
                ["AddedMomumentSpawnPoint"] = "{0} The spawn point of the car has been <color=#738d43>successfully</color> added to <color=#738d43>{1}</color> monument",

                ["NoPermission"] = "{0} You <color=#ce3f27>do not have permission</color> to use this module!",
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["GetModule"] = "{0} Вы <color=#738d43>получили</color> кастомный модуль!",

                ["PresetNotFoundExeption"] = "Пресет {0} <color=#ce3f27>не найден<color=#ce3f27>!",
                ["PrefabNotFoundExeption"] = "Префаб {0} <color=#ce3f27>не найден<color=#ce3f27>!",

                ["AddedMapSpawnPoint"] = "{0} Точка спавна машины <color=#738d43>успешно</color> добавлена",
                ["AddedMomumentSpawnPoint"] = "{0} Точка спавна машины <color=#738d43>успешно</color> добавлена на монумент <color=#738d43>{1}",

                ["NoPermission"] = "{0} У вас <color=#ce3f27>нет разрешения</color> для использования этого модуля!",

            }, this, "ru");
        }

        static string GetMessage(string langKey, string userID) => ins.lang.GetMessage(langKey, ins, userID);

        static string GetMessage(string langKey, string userID, params object[] args) => (args.Length == 0) ? GetMessage(langKey, userID) : string.Format(GetMessage(langKey, userID), args);

        #endregion Lang

        #region Config  

        private PluginConfig _config;

        protected override void LoadDefaultConfig() => _config = PluginConfig.DefaultConfig();

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<PluginConfig>();
            Config.WriteObject(_config, true);
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        public class MainConfig
        {
            [JsonProperty(en ? "Permission to give items" : "Разрешение для выдачи кастомный модулей")] public string givePermission { get; set; }
            [JsonProperty(en ? "Allow the spawn of modules in the crates? [true/false]" : "Включить спавн модулей в ящиках? [true/false]")] public bool enableSpawnInCrates { get; set; }
            [JsonProperty(en ? "Allow turrets to attack NPCs? [true/false]" : "Разрешить турелям атаковать нпс? [true/false]")] public bool allowAttackNpc { get; set; }
            [JsonProperty(en ? "Allow turrets to attack players? [true/false]" : "Разрешить турелям атаковать игроков? [true/false]")] public bool allowAttackPlayers { get; set; }
            [JsonProperty(en ? "The mode of operation of the light with remote control (0 - off, 1 - turns on automatically at night, 2 - on constantly)" : "Режим работы света при дистанционном управлении (0 - выключен, 1 - включается ночью автоматически, 2 - включен постоянно)")] public int lightOperatonModeAtRemote { get; set; }
        }

        public class BaseModuleConfig
        {
            [JsonProperty(en ? "Custom item shortname" : "Кастомный шортнейм предмета")] public string customItemShortname { get; set; }
            [JsonProperty(en ? "Permission to install the module on the car" : "Разрешение на установку")] public string permissionForPlacement { get; set; }
            [JsonProperty(en ? "Locations of decorative objects" : "Расположения декоративных объектов")] public Dictionary<string, List<LocationConfig>> entities { get; set; }
            [JsonProperty(en ? "Allow the engine to work underwater? [true/false]" : "Разрешить двигателю работать под водой? [true/false]")] public bool underwaerEngine { get; set; }
            [JsonProperty(en ? "The percentage of health of the module on which it stops working (0 - 100)" : "Процент здоровья модуля, на котором он переставет работать (0 - 100)")] public float minHealthPercent { get; set; }
        }

        public class BoostModuleConfig : BaseModuleConfig
        {
            [JsonProperty(en ? "Acceleration Multiplier" : "Множитель ускорения", Order = 100)] public float boostScale { get; set; }
            [JsonProperty(en ? "Fuel consumption multiplier" : "Множитель потребления топлива", Order = 101)] public float fuelConsScale { get; set; }
            [JsonProperty(en ? "Locations of rocket launchers" : "Расположения ракетниц", Order = 103)] public HashSet<LocationConfig> rocketLauncherLocations { get; set; }
            [JsonProperty(en ? "Degrade the effectiveness of the module with loss of health? [true/false]" : "Ухудшать эффективность модуля при потере здоровья? [true/false]", Order = 104)] public bool reduceEffectivenessModuleAfterdamage { get; set; }
        }

        public class BuoyancyModuleConfig : BaseModuleConfig
        {
            [JsonProperty(en ? "Buoyancy Multiplier" : "Множитель плавучести", Order = 100)] public float buoyancy { get; set; }
            [JsonProperty(en ? "Thrust Multiplier" : "Множитель тяги", Order = 101)] public float boostScale { get; set; }
            [JsonProperty(en ? "Turn Multiplier" : "Множитель поворота", Order = 102)] public float rotateScale { get; set; }
            [JsonProperty(en ? "Degrade the effectiveness of the module with loss of health? [true/false]" : "Ухудшать эффективность модуля при потере здоровья? [true/false]", Order = 103)] public bool reduceEffectivenessModuleAfterdamage { get; set; }
        }

        public class RemoteControlModuleConfig : BaseModuleConfig
        {
            [JsonProperty(en ? "Camera location" : "Расположение камеры", Order = 100)] public LocationConfig cameraLocationConfig { get; set; }
            [JsonProperty(en ? "Receiver location" : "Расположение приемника", Order = 101)] public LocationConfig broadcasterLocationConfig { get; set; }
        }

        public class TurretModuleConfig : BaseModuleConfig
        {
            [JsonProperty(en ? "Turret locations" : "Расположения турелей", Order = 100)] public List<LocationConfig> turretlocations { get; set; }
        }

        public class LocationConfig
        {
            [JsonProperty(en ? "Position" : "Позиция")] public string position { get; set; }
            [JsonProperty(en ? "Rotation" : "Вращение")] public string rotation { get; set; }
        }

        public class ItemConfig
        {
            [JsonProperty("Custom Shortname")] public string customShortname { get; set; }
            [JsonProperty("Shortname")] public string shortname { get; set; }
            [JsonProperty("Skin")] public ulong skin { get; set; }
            [JsonProperty("Name")] public string name { get; set; }
            [JsonProperty(en ? "The probability of replacing the standard module when the car is spawned with this one (0 - 100)" : "Вероятность замены стандартного модуля при спавне машины на этот (0 - 100)")] public float spawnChance { get; set; }
            [JsonProperty(en ? "Crate prefab - chance" : "Префаб ящика - шанс выпадения")] public Dictionary<string, float> cratesSetting { get; set; }
        }

        public class RespawnSetting
        {
            [JsonProperty(en ? "Enable spawn of custom cars" : "Включить респавн машин")] public bool enable { get; set; }
            [JsonProperty(en ? "Time between respawns [sec]" : "Период респавна [sec]")] public int respawnPeriod { get; set; }
            [JsonProperty(en ? "Car preset - probability" : "Пресет машины - вероятность")] public Dictionary<string, float> presets { get; set; }
            [JsonProperty(en ? "Setting up respawn cars on monuments" : "Настройка респавна машин на монументах")] public HashSet<MonumentRespawnPositionsConfig> monumentRespawnConfigs { get; set; }
            [JsonProperty(en ? "Setting up respawn cars on the map (for custom monuments)" : "Настройка респавна машин на карте (для кастомных монументах)")] public RespawnPositionsConfig mapRespawnConfig { get; set; }
        }

        public class RespawnPositionsConfig
        {
            [JsonProperty(en ? "Spawn points of random cars" : "Точки спавна рандомных машин")] public List<LocationConfig> randomSpawnLocations { get; set; }
            [JsonProperty(en ? "Car preset - custom spawn points" : "Пресет машины - кастомные точки спавна")] public Dictionary<string, List<LocationConfig>> carPresetLocations { get; set; }
        }

        public class MonumentRespawnPositionsConfig : RespawnPositionsConfig
        {
            [JsonProperty(en ? "Monument Preset" : "Пресет монумента")] public string monumentPreset { get; set; }
        }

        public class CarPresetConfig
        {
            [JsonProperty(en ? "Preset Name" : "Название пресета")] public string presetName { get; set; }
            [JsonProperty(en ? "Car prefab" : "Префаб машины")] public string carPrefabName { get; set; }
            [JsonProperty(en ? "Module shortnames / custom module shortnames" : "Шортнеймы модулей / кастомные шортнеймы модулей")] public List<string> modules { get; set; }
            [JsonProperty(en ? "Fuel" : "Топливо")] public int fuel { get; set; }
            [JsonProperty(en ? "Add components to the engine? [true/false]" : "Добавить компоненты в двинатель? [true/false]")] public bool engineComponents { get; set; }
            [JsonProperty(en ? "Engine component level (1 - 3)" : "Уровень компонентов в двигателе (1 - 3)")] public int engineComponentsLvl { get; set; }
        }

        public class OtherPluginsConfig
        {
            [JsonProperty(en ? "VehicleDeployedLocks setting" : "Настройка VehicleDeployedLocks")] public VehicleDeployedLocksConfig vehicleDeployedLocksConfig { get; set; }
        }

        public class VehicleDeployedLocksConfig
        {
            [JsonProperty(en ? "Is the VehicleDeployedLocks plugin used? [true/false]" : "Используется плагин VehicleDeployedLocks? [true/false]")] public bool enable { get; set; }
        }

        private class PluginConfig
        {
            [JsonProperty(en ? "Version" : "Версия")] public VersionNumber version { get; set; }
            [JsonProperty(en ? "Prefix of chat messages" : "Префикс в чате")] public string prefix { get; set; }
            [JsonProperty(en ? "Main settings" : "Основные настройки")] public MainConfig mainConfig { get; set; }
            [JsonProperty(en ? "Turret modules" : "Модули турелей")] public List<TurretModuleConfig> turretModules { get; set; }
            [JsonProperty(en ? "Reactive modules" : "Модули ускорения")] public HashSet<BoostModuleConfig> boostModules { get; set; }
            [JsonProperty(en ? "Buoyancy modules" : "Модули плавучести")] public HashSet<BuoyancyModuleConfig> buoyancyModules { get; set; }
            [JsonProperty(en ? "Remote control modules" : "Модули дистанционного управления")] public HashSet<RemoteControlModuleConfig> remoteControlModuleConfigs { get; set; }
            [JsonProperty(en ? "Other modules" : "Другие модули")] public HashSet<BaseModuleConfig> regularModules { get; set; }
            [JsonProperty(en ? "Custom items" : "Кастомные предметы")] public HashSet<ItemConfig> customItems { get; set; }
            [JsonProperty(en ? "Setting up the spawn of cars (/customcarspawnpoint)" : "Настройка спавна машин (/customcarspawnpoint)")] public RespawnSetting respawnSetting { get; set; }
            [JsonProperty(en ? "Setting up car presets" : "Настройка пресетов машин")] public List<CarPresetConfig> carPresets { get; set; }
            [JsonProperty(en ? "Supported Plugins" : "Поддерживаемые плагины")] public OtherPluginsConfig otherPluginsConfig { get; set; }

            public static PluginConfig DefaultConfig()
            {
                return new PluginConfig()
                {
                    version = new VersionNumber(1, 2, 2),
                    prefix = "[CustomModules]",
                    mainConfig = new MainConfig
                    {
                        givePermission = "custommodules.items",
                        enableSpawnInCrates = false,
                        allowAttackNpc = false,
                        allowAttackPlayers = false,
                        lightOperatonModeAtRemote = 2
                    },
                    turretModules = new List<TurretModuleConfig>
                    {
                        new TurretModuleConfig
                        {
                            customItemShortname = "turretitem_1",
                            permissionForPlacement = "",
                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                            },
                            turretlocations = new List<LocationConfig>
                            {
                                new LocationConfig
                                {
                                    position = "(0, -0.16, 0)",
                                    rotation = "(0, 180, 0)"
                                }
                            },
                            minHealthPercent = 20
                        },
                        new TurretModuleConfig
                        {
                            customItemShortname = "turretbuoyancyitem_1",
                            permissionForPlacement = "",
                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                            },
                            turretlocations = new List<LocationConfig>
                            {
                                new LocationConfig
                                {
                                    position = "(0, -0.16, 0)",
                                    rotation = "(0, 180, 0)"
                                }
                            },
                            minHealthPercent = 20
                        }
                    },
                    boostModules = new HashSet<BoostModuleConfig>
                    {
                        new BoostModuleConfig
                        {
                            customItemShortname = "boostitem_1",
                            permissionForPlacement = "",
                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                            },
                            minHealthPercent = 0,
                            boostScale = 1,
                            fuelConsScale = 1,
                            rocketLauncherLocations = new HashSet<LocationConfig>
                            {
                                new LocationConfig
                                {
                                    position = "(0.704, 0.421, 0)",
                                    rotation = "(1.536, 265.570, 307.920)"
                                },
                                new LocationConfig
                                {
                                    position = "(-0.716, 0.569, 0)",
                                    rotation = "(358.464, 94.430, 127.920)"
                                }
                            },
                            reduceEffectivenessModuleAfterdamage = true
                        },
                        new BoostModuleConfig
                        {
                            customItemShortname = "boostitem_2",
                            permissionForPlacement = "",
                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                            },
                            minHealthPercent = 0,
                            boostScale = 1,
                            fuelConsScale = 1,
                            rocketLauncherLocations = new HashSet<LocationConfig>
                            {
                                new LocationConfig
                                {
                                    position = "(0.824, 0.421, 0)",
                                    rotation = "(1.536, 265.570, 307.920)"
                                },
                                new LocationConfig
                                {
                                    position = "(-0.836, 0.569, 0)",
                                    rotation = "(358.464, 94.430, 127.920)"
                                }
                            },
                            reduceEffectivenessModuleAfterdamage = true
                        }
                    },
                    buoyancyModules = new HashSet<BuoyancyModuleConfig>
                    {
                        new BuoyancyModuleConfig
                        {
                            customItemShortname = "buoyancyitem_1",
                            permissionForPlacement = "",
                            underwaerEngine = true,

                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                                ["assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab"] = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, -0.5)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, 0.5)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, -0.5)",
                                        rotation = "(0, 0, 90)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, 0.5)",
                                        rotation = "(0, 0, 90)"
                                    },
                                }
                            },
                            minHealthPercent = 0,
                            buoyancy = 1,
                            boostScale = 1,
                            rotateScale = 1,
                            reduceEffectivenessModuleAfterdamage = true
                        },
                        new BuoyancyModuleConfig
                        {
                            customItemShortname = "buoyancyitem_2",
                            permissionForPlacement = "",
                            underwaerEngine = true,

                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                                ["assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab"] = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, -2.1)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, -2.1)",
                                        rotation = "(0, 0, 90)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, -0.75)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, -0.75)",
                                        rotation = "(0, 0, 90)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, 0.5)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, 0.5)",
                                        rotation = "(0, 0, 90)"
                                    },
                                }
                            },
                            minHealthPercent = 0,
                            buoyancy = 1,
                            boostScale = 1,
                            rotateScale = 1,
                            reduceEffectivenessModuleAfterdamage = true
                        },
                        new BuoyancyModuleConfig
                        {
                            customItemShortname = "turretbuoyancyitem_1",
                            permissionForPlacement = "",
                            underwaerEngine = true,

                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                                ["assets/prefabs/misc/summer_dlc/inner_tube/innertube.deployed.prefab"] = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, -0.5)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.95, 0.25, 0.5)",
                                        rotation = "(0, 0, 270)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, -0.5)",
                                        rotation = "(0, 0, 90)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(-0.95, 0.25, 0.5)",
                                        rotation = "(0, 0, 90)"
                                    },
                                }
                            },
                            minHealthPercent = 0,
                            buoyancy = 1,
                            boostScale = 1,
                            rotateScale = 1,
                            reduceEffectivenessModuleAfterdamage = true
                        }
                    },
                    remoteControlModuleConfigs = new HashSet<RemoteControlModuleConfig>
                    {
                        new RemoteControlModuleConfig
                        {
                            customItemShortname = "remoteitem_1",
                            permissionForPlacement = "",
                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                                ["assets/prefabs/weapons/halloween/pitchfork/pitchfork.entity.prefab"] = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(-0.864, 0.357, -0.758)",
                                        rotation = "(281.023, 270, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(0.864, 0.357, -0.758)",
                                        rotation = "(281.023, 90, 0)"
                                    }
                                }
                            },
                            cameraLocationConfig = new LocationConfig
                            {
                                position = "0, 1.207, -0.93",
                                rotation = "16.241, 0, 0"
                            },
                            broadcasterLocationConfig = new LocationConfig
                            {
                                position = "(0, 0.8, -0.93)",
                                rotation = "(0, 0, 0)"
                            },
                            minHealthPercent = 0,
                        }
                    },
                    regularModules = new HashSet<BaseModuleConfig>
                    {
                        new BaseModuleConfig
                        {
                            customItemShortname = "underwaterengineitem_1",
                            permissionForPlacement = "",
                            entities = new Dictionary<string, List<LocationConfig>>
                            {
                                ["diving.tank"] = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(0, 0.55, 0.4)",
                                        rotation = "(90, 0, 0)"
                                    }
                                }
                            },
                            minHealthPercent = 0,
                            underwaerEngine = true
                        }
                    },
                    customItems = new HashSet<ItemConfig>
                    {
                        new ItemConfig
                        {
                            customShortname = "turretitem_1",
                            shortname = "vehicle.1mod.flatbed",
                            skin = 2829491848,
                            name = en ? "Turret module" : "Модуль-турель",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {

                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "turretbuoyancyitem_1",
                            shortname = "vehicle.1mod.flatbed",
                            skin = 2829491419,
                            name = en ? "Floating turret module" : "Плавающий модуль-турель",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {

                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "buoyancyitem_1",
                            shortname = "vehicle.1mod.flatbed",
                            skin = 2829492327,
                            name = en ? "Floating module" : "Плавающий модуль",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {
                                ["crate_elite"] = 5
                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "buoyancyitem_2",
                            shortname = "vehicle.2mod.flatbed",
                            skin = 2829490814,
                            name = en ? "Large floating module" : "Большой плавающий модуль",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {
                                ["crate_elite"] = 2
                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "boostitem_1",
                            shortname = "vehicle.1mod.storage",
                            skin = 2829494228,
                            name = en ? "Jet module" : "Реактивный модуль",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {

                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "boostitem_2",
                            shortname = "vehicle.1mod.taxi",
                            skin = 2829492964,
                            name = en ? "Jet module-taxi" : "Jet taxi module",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {

                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "remoteitem_1",
                            shortname = "vehicle.1mod.cockpit.with.engine",
                            skin = 2829490458,
                            name = en ? "Remote control module" : "Модуль дистанционного управления",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {

                            }
                        },
                        new ItemConfig
                        {
                            customShortname = "underwaterengineitem_1",
                            shortname = "vehicle.1mod.engine",
                            skin = 2829489113,
                            name = en ? "Underwater engine" : "Подводный двигитель",
                            spawnChance = 0,
                            cratesSetting = new Dictionary<string, float>
                            {

                            }
                        }
                    },
                    respawnSetting = new RespawnSetting
                    {
                        enable = false,
                        respawnPeriod = 7200,
                        mapRespawnConfig = new RespawnPositionsConfig
                        {
                            carPresetLocations = new Dictionary<string, List<LocationConfig>>(),
                            randomSpawnLocations = new List<LocationConfig>()
                        },
                        monumentRespawnConfigs = new HashSet<MonumentRespawnPositionsConfig>
                        {
                            new MonumentRespawnPositionsConfig
                            {
                                monumentPreset = "assets/bundled/prefabs/autospawn/monument/roadside/supermarket_1.prefab",
                                randomSpawnLocations = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(16.6, 0.0, -3.8)",
                                        rotation = "(0, 172.9239, 0)"
                                    }
                                },
                                carPresetLocations = new Dictionary<string, List<LocationConfig>>()
                            },
                            new MonumentRespawnPositionsConfig
                            {
                                monumentPreset = "assets/bundled/prefabs/autospawn/monument/roadside/gas_station_1.prefab",
                                randomSpawnLocations = new List<LocationConfig>
                                {
                                    new LocationConfig
                                    {
                                        position = "(4.0, 3.0, 7.4)",
                                        rotation = "(0, 90, 0)"
                                    },
                                    new LocationConfig
                                    {
                                        position = "(4.0, 3.0, -8.8)",
                                        rotation = "(0, 90, 0)"
                                    }
                                },
                                carPresetLocations = new Dictionary<string, List<LocationConfig>>()
                            }
                        },
                        presets = new Dictionary<string, float>
                        {
                            ["buoyancy_car_1"] = 70,
                            ["remote_car_1"] = 30
                        }
                    },
                    carPresets = new List<CarPresetConfig>
                    {
                        new CarPresetConfig
                        {
                            presetName = "buoyancy_car_1",
                            carPrefabName = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",
                            modules = new List<string>
                            {
                                "buoyancyitem_1",
                                "vehicle.1mod.cockpit.with.engine",
                                "buoyancyitem_1"
                            },
                            engineComponents = true,
                            engineComponentsLvl = 1,
                            fuel = 50,
                        },
                        new CarPresetConfig
                        {
                            presetName = "remote_car_1",
                            carPrefabName = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",
                            modules = new List<string>
                            {
                                "remoteitem_1",
                                "vehicle.2mod.flatbed"
                            },
                            engineComponents = true,
                            engineComponentsLvl = 1,
                            fuel = 50,
                        }
                    },
                    otherPluginsConfig = new OtherPluginsConfig
                    {
                        vehicleDeployedLocksConfig = new VehicleDeployedLocksConfig()
                    }
                };
            }
        }
        #endregion Config
    }
}

namespace Oxide.Plugins.CustomModulesExtensionMethods
{
    public static class ExtensionMethods
    {
        public static bool Any<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return true;
            return false;
        }

        public static HashSet<TSource> Where<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            HashSet<TSource> result = new HashSet<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) result.Add(enumerator.Current);
            return result;
        }

        public static TSource FirstOrDefault<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate)
        {
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (predicate(enumerator.Current)) return enumerator.Current;
            return default(TSource);
        }

        public static HashSet<T> OfType<T>(this IEnumerable<BaseNetworkable> source)
        {
            HashSet<T> result = new HashSet<T>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) if (enumerator.Current is T) result.Add((T)(object)enumerator.Current);
            return result;
        }

        public static List<TSource> ToList<TSource>(this IEnumerable<TSource> source)
        {
            List<TSource> result = new List<TSource>();
            using (var enumerator = source.GetEnumerator()) while (enumerator.MoveNext()) result.Add(enumerator.Current);
            return result;
        }

        public static List<TSource> OrderBy<TSource>(this IEnumerable<TSource> source, Func<TSource, float> predicate)
        {
            List<TSource> result = source.ToList();
            for (int i = 0; i < result.Count; i++)
            {
                for (int j = 0; j < result.Count - 1; j++)
                {
                    if (predicate(result[j]) > predicate(result[j + 1]))
                    {
                        TSource z = result[j];
                        result[j] = result[j + 1];
                        result[j + 1] = z;
                    }
                }
            }
            return result;
        }

        public static bool IsExists(this BaseNetworkable entity) => entity != null && !entity.IsDestroyed;

        public static bool IsRealPlayer(this BasePlayer player) => player != null && player.userID.IsSteamId();

        public static TSource ElementAt<TSource>(this IEnumerable<TSource> source, int index)
        {
            int movements = 0;
            using (var enumerator = source.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (movements == index) return enumerator.Current;
                    movements++;
                }
            }
            return default(TSource);
        }
    }
}