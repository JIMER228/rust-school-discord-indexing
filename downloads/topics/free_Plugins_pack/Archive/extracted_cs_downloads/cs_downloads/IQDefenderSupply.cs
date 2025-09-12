using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using ConVar;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine;
using VLB;
using Object = System.Object;
using Physics = UnityEngine.Physics;
using Random = System.Random;
using Time = UnityEngine.Time;
using Pool = Facepunch.Pool;

namespace Oxide.Plugins
{
    [Info("IQDefenderSupply", "Mercury", "1.9.15")]
    [Description("IQDefenderSupply")]
    public class IQDefenderSupply : RustPlugin
    {
        /// <summary>
        /// - Теперь для каждого дропа на карте будет свое название в зависимости от уровня сложности
        /// - Скорректирована очистка локальных хранилищ
        /// - Исправлен конфликт с PVEMode (Calling hook CanEntityTakeDamage resulted in a conflict between the following plugins: IQDefenderSupply - True (Boolean), PveMode (False (Boolean)))
        /// - Исправлено когда нет сохраненных позиций - команда не позволяла вызвать защищенный груз на игрока
        /// - Добавлена поддержка GoldCard (разработчик должен обновить свой плагин)
        /// </summary>
        
        private const Boolean LanguageEn = false;

        #region Reference

        [PluginReference] Plugin IQChat, IQDronePatrol, IQGuardianDrone, NpcSpawn, IQTurret, PveMode, LootDefender, TruePVE;

        #region Removed
        
        private Object canRemove(BasePlayer player, BaseEntity entity)
        {
            if (entity.OwnerID == ownerIDElemnts) return false;
            return null;
        }

        #endregion
        
        #region IQChat

        public void SendChat(String Message, BasePlayer player, ConVar.Chat.ChatChannel channel = ConVar.Chat.ChatChannel.Global)
        {
            if (IQChat)
                IQChat?.Call("API_ALERT_PLAYER", player, Message, config.generalSetting.alertSetting.iqchatSetting.customPrefix, config.generalSetting.alertSetting.iqchatSetting.customAvatar);
            else player.SendConsoleCommand("chat.add", channel, 0, Message);
        }
        
        #endregion
        
        #region IQDronePatrol
        
        private Boolean IsDronePatrol(UInt64 ownerID)
        {
            if (!IQDronePatrol) return false;
            return IQDronePatrol.Call<Boolean>("IsValidTurret", ownerID);
        }
        private class CustomPatrol
        {
            public String pluginName;
            public Vector3 position;
            public PositionSetting settingPosition = new();
            public DroneSetting settingDrone = new();

            internal class DroneSetting
            {
                public Int32 droneCountSpawned;
                public Int32 droneAttackedCount;
                public Dictionary<String, Int32> keyDrones = new();
            }
            internal class PositionSetting
            {
                public Int32 countSpawnPoint;
                public Int32 radiusFindedPoints;
            }
        }

        #endregion

        #region Pve

        private void PveModeRemoveZone(UInt64 netID)
        {
            if (!PveMode) return;
            if (!config.otherPlugins.pveMode.usePveMode) return;
            PveMode.Call("EventRemovePveMode", $"IQDefenderSupply_Drop_{netID}", true);
        }
        private void PveModeAddedZone(SupplyDrop supplyDrop)
        {
            if (!PveMode) return;
            if (!config.otherPlugins.pveMode.usePveMode) return;
            if (!supplyDrop) return;
            
            Configuration.OtherPlugins.PveModePlugin pveModeConfiguration = config.otherPlugins.pveMode;
            Dictionary<String, Object> configPve = new()
            {
                ["Damage"] = pveModeConfiguration.Damage,
                ["ScaleDamage"] = pveModeConfiguration.ScaleDamage.ToDictionary(x => x.Type, v => v.Scale),
                ["LootCrate"] = pveModeConfiguration.LootCrate,
                ["HackCrate"] = true,
                ["LootNpc"] = pveModeConfiguration.LootNpc,
                ["DamageNpc"] = pveModeConfiguration.DamageNpc,
                ["DamageTank"] = true,
                ["DamageHelicopter"] = true,
                ["DamageTurret"] = true,
                ["TargetNpc"] = pveModeConfiguration.TargetNpc,
                ["TargetTank"] = true,
                ["TargetHelicopter"] = true,
                ["TargetTurret"] = true,
                ["CanEnter"] = pveModeConfiguration.CanEnter,
                ["CanEnterCooldownPlayer"] = pveModeConfiguration.CanEnterCooldownPlayer,
                ["TimeExitOwner"] = pveModeConfiguration.TimeExitOwner,
                ["AlertTime"] = pveModeConfiguration.AlertTime,
                ["RestoreUponDeath"] = pveModeConfiguration.RestoreUponDeath,
                ["CooldownOwner"] = pveModeConfiguration.CooldownOwner,
                ["Darkening"] = pveModeConfiguration.Darkening
            };

            HashSet<UInt64> npcIdSet = new();
            if (npcDefendersDrops.TryGetValue(supplyDrop, out Dictionary<ScientistNPC, String> npcs))
            {
                foreach (ScientistNPC npc in npcs.Keys)
                    npcIdSet.Add(npc.net.ID.Value);
            }

            HashSet<UInt64> turretsID = new();
            if (turretsDefenderDrops.TryGetValue(supplyDrop, out List<AutoTurret> turrets))
            {
                foreach (AutoTurret turret in turrets)
                    npcIdSet.Add(turret.net.ID.Value);
            }

            PveMode.CallHook("EventAddPveMode", $"IQDefenderSupply_Drop_{supplyDrop.net.ID.Value}", configPve, supplyDrop.transform.position,
                pveModeConfiguration.radiusPveZone,
                new HashSet<UInt64> { supplyDrop.net.ID.Value }, npcIdSet, new HashSet<UInt64>(), new HashSet<UInt64>(),
                turretsID, new HashSet<UInt64>(), null);
        }
        
        #endregion
        
        #endregion
        
        #region Vars

        private static IQDefenderSupply _;
        
        private const UInt64 ownerIDElemnts = 3746538364;
        private const UInt64 skinIDElement = 3352994990;
        private const Single checkDirectionsDistance = 5; // Расстояние для проверки вокруг точки

        private const String explosionPrefab = "assets/bundled/prefabs/fx/explosions/explosion_01.prefab";
        private const String engineStopperPrefab = "assets/prefabs/weapons/military flamethrower/militaryflamethrower.entity.prefab";
        private const String supplyDropPrefab = "assets/prefabs/misc/supply drop/supply_drop.prefab";
        private const String simpleLightPrefab = "assets/prefabs/deployable/playerioents/lights/simplelight.prefab";
        private const String sirenLightPrefab = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
        private const String shutterMetalEmbrasurePrefab = "assets/prefabs/building/wall.window.embrasure/shutter.metal.embrasure.a.prefab";
        private const String windowWallPrefab = "assets/prefabs/building/wall.window.bars/wall.window.bars.toptier.prefab";
        private const String prisonWallPrefab = "assets/prefabs/building/wall.frame.cell/wall.frame.cell.prefab";
        private const String prisonFloorPrefab = "assets/prefabs/building/floor.grill/floor.grill.prefab";
        private const String prisonDoorPrefab = "assets/prefabs/building/wall.frame.cell/wall.frame.cell.gate.prefab";
        private const String cardReaderPrefab = "assets/prefabs/io/electric/switches/cardreader.prefab";
        private const String sphereVisualPrefab = "assets/prefabs/visualization/sphere.prefab";
        private const String turretPrefab = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab";
        private const String cargoPlanePrefab = "assets/prefabs/npc/cargo plane/cargo_plane.prefab";
        private const String markerPrefab = "assets/prefabs/tools/map/genericradiusmarker.prefab";
        private const String invisibleVendingPrefab = "assets/prefabs/deployable/vendingmachine/vending_mapmarker.prefab";

        public Dictionary<TypeLevels, String> namingMappingMarker = new()
        {
            [TypeLevels.Level0] = "LITE DEFENDER SUPPLY",
            [TypeLevels.Level1] = "MIDDLE DEFENDER SUPPLY",
            [TypeLevels.Level2] = "HARD DEFENDER SUPPLY",
        };
        
        public enum TypeLevels
        {
            Level0,
            Level1,
            Level2,
        }

        private enum TypeActionDiscord
        {
            CargoSpawned,
            SupplyDropped,
            SupplyDestroyed,
            SupplyOpened
        }
        
        private Coroutine routineGeneratedPosition;

        private Timer timerController;

        private Dictionary<String, Vector3> allMonuments = new Dictionary<String, Vector3>();
        private Dictionary<SupplyDrop, Vector3> droneActivePosition = new();
        private Dictionary<SupplyDrop, Dictionary<ScientistNPC, String>> npcDefendersDrops = new ();
        private Dictionary<SupplyDrop, List<AutoTurret>> turretsDefenderDrops = new ();
        
        private List<SupplyDrop> listDestroyedSupplyInvoke = new ();
        private Dictionary<CargoPlane, CargoPlaneRepository> activeCargoPlane = new();
        private List<LangingController> langingComponents = new ();
        private Dictionary<SupplyDrop, ActiveDropRepository> levelsDropActive = new();
        private Dictionary<SupplyDrop, BaseEntity> alarmDrops = new ();
        private Dictionary<CardReader, List<BaseEntity>> readerManipulator = new();
        private Dictionary<SupplyDrop, MarkerRepository> mapMarkers = new ();

        private class ActiveDropRepository
        {
            public List<BaseEntity> entitiesList = new();
            public TypeLevels levelSupply;
        }
        private class CargoPlaneRepository
        {
            public String presetKey;
            public Vector3 dropPosition;
        }

        private readonly Dictionary<TypeLevels, Dictionary<String, PositionEntity>> positionCashed = new() 
        {
            [TypeLevels.Level0] = new Dictionary<String, PositionEntity>
            {
                [engineStopperPrefab] = new PositionEntity
                {
                    entityPosList = new List<PositionEntity.EntityPos>() 
                    {
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(-0.9f, 0.18f, 1f),
                            localQuaternion = Quaternion.Euler(-10f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(-0.9f, 0.18f, -1f),
                            localQuaternion = Quaternion.Euler(-4f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(0.9f, 0.18f, 1f),
                            localQuaternion = Quaternion.Euler(-10f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(0.9f, 0.18f, -1f),
                            localQuaternion = Quaternion.Euler(-4f, 0f, 90f),
                        },
                    },
                    spherePos = new PositionEntity.SpherePos
                    {
                        sizeSphere = 1f,
                        position = new Vector3(0f, 0.9f, 0f),
                    }
                },
                [turretPrefab] = new PositionEntity
                {
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new PositionEntity.EntityPos
                        {
                            localPosition = new Vector3(0f, 0.4f, 0f),
                            localQuaternion = default,
                        }
                    },
                    spherePos = new PositionEntity.SpherePos()
                    {
                        sizeSphere = 1.7f,
                        position = new Vector3(0f, 0.8f, 0f),
                    }
                },
                [shutterMetalEmbrasurePrefab] = new PositionEntity
                {
                    spherePos = new PositionEntity.SpherePos()
                    {
                        sizeSphere = 0.8f,
                        position = new Vector3(0f, 0.8f, 0f),
                    },
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(-1.67f, 0.1f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, -4),
                        },
                        new()
                        {
                            localPosition = new Vector3(1.27f, 0.1f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 4),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0.1f, -1.37f),
                            localQuaternion = Quaternion.Euler(0, 90, 4),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0.1f, 1.67f),
                            localQuaternion = Quaternion.Euler(0, 90, -4),
                        },
                        new()
                        {
                            localPosition = new Vector3(-1.67f, 1.9f, 0f), 
                            localQuaternion = Quaternion.Euler(0, 0, -4),
                        },
                        new()
                        {
                            localPosition = new Vector3(1.27f, 1.9f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 4),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 1.9f, -1.37f),
                            localQuaternion = Quaternion.Euler(0, 90, 4),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 1.9f, 1.67f),
                            localQuaternion = Quaternion.Euler(0, 90, -4),
                        },
                    },
                }
            },
            [TypeLevels.Level1] = new Dictionary<String, PositionEntity>
            {
                [engineStopperPrefab] = new PositionEntity
                {
                    entityPosList = new List<PositionEntity.EntityPos>() 
                    {
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(-0.9f, 0.18f, 1f),
                            localQuaternion = Quaternion.Euler(-10f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(-0.9f, 0.18f, -1f),
                            localQuaternion = Quaternion.Euler(-4f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(0.9f, 0.18f, 1f),
                            localQuaternion = Quaternion.Euler(-10f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(0.9f, 0.18f, -1f),
                            localQuaternion = Quaternion.Euler(-4f, 0f, 90f),
                        },
                    },
                    spherePos = new PositionEntity.SpherePos
                    {
                        sizeSphere = 1f,
                        position = new Vector3(0f, 0.9f, 0f),
                    }
                },
                [prisonWallPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(1.3f, 0f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0f, -1.3f),
                            localQuaternion = Quaternion.Euler(0, 90, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0f, 1.3f),
                            localQuaternion = Quaternion.Euler(0, 90, 0),
                        },
                    },
                },
                [prisonFloorPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(0f, 0f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 2.8f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                    },
                },
                [prisonDoorPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(-1.3f, 0f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                    },
                },
                [cardReaderPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(-0.13f, 0.08f, -1.1f),
                            localQuaternion = Quaternion.Euler(0, -90, 0),
                        },
                    },
                },
                [simpleLightPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(0f, 2.85f, 0f),
                            localQuaternion = Quaternion.Euler(0, 90, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 2.85f, 0f),
                            localQuaternion = Quaternion.Euler(0, -90, 0),
                        },
                    },
                },
            },
            [TypeLevels.Level2] = new Dictionary<String, PositionEntity>
            {
                [engineStopperPrefab] = new PositionEntity
                {
                    entityPosList = new List<PositionEntity.EntityPos>() 
                    {
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(-0.9f, 0.18f, 1f),
                            localQuaternion = Quaternion.Euler(-10f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(-0.9f, 0.18f, -1f),
                            localQuaternion = Quaternion.Euler(-4f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(0.9f, 0.18f, 1f),
                            localQuaternion = Quaternion.Euler(-10f, 0f, 90f),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition =  new Vector3(0.9f, 0.18f, -1f),
                            localQuaternion = Quaternion.Euler(-4f, 0f, 90f),
                        },
                    },
                    spherePos = new PositionEntity.SpherePos
                    {
                        sizeSphere = 1f,
                        position = new Vector3(0f, 0.9f, 0f),
                    }
                },
                [prisonWallPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(1.3f, 0f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0f, -1.3f),
                            localQuaternion = Quaternion.Euler(0, 90, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0f, 1.3f),
                            localQuaternion = Quaternion.Euler(0, 90, 0),
                        },
                    },
                },
                [prisonFloorPrefab] = new PositionEntity 
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(0f, 0f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 2.8f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                    },
                },
                [windowWallPrefab] = new PositionEntity 
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(1.3f, 2.75f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, -90),
                        },
                        new()
                        {
                            localPosition = new Vector3(-1.3f, 2.75f, 0f),
                            localQuaternion = Quaternion.Euler(180, 0, 90),
                        },
                    },
                },
                [prisonDoorPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(-1.3f, 0f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 0),
                        },
                    },
                },
                [cardReaderPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(-0.13f, 0.08f, -1.1f),
                            localQuaternion = Quaternion.Euler(0, -90, 0),
                        },
                    },
                },
                [simpleLightPrefab] = new PositionEntity
                {
                    spherePos = null,
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new()
                        {
                            localPosition = new Vector3(0f, 0.65f, 1f),
                            localQuaternion = Quaternion.Euler(0, 0, 90),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0.65f, -1f),
                            localQuaternion = Quaternion.Euler(180, 0, 90),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0.65f, 1f),
                            localQuaternion = Quaternion.Euler(0, 0, 90),
                        },
                        new()
                        {
                            localPosition = new Vector3(0f, 0.65f, -1f),
                            localQuaternion = Quaternion.Euler(-180, 0, 90),
                        },
                    },
                },
                [turretPrefab] = new PositionEntity
                {
                    entityPosList = new List<PositionEntity.EntityPos>()
                    {
                        new PositionEntity.EntityPos
                        {
                            localPosition = new Vector3(0f, -0.2f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 90),
                        },
                        new PositionEntity.EntityPos
                        {
                            localPosition = new Vector3(0f, -0.2f, 0f),
                            localQuaternion = Quaternion.Euler(0, 0, 90),
                        }
                    },
                    spherePos = new PositionEntity.SpherePos()
                    {
                        sizeSphere = 1.3f,
                        position = new Vector3(0f, 0.8f, 0f),
                    }
                },
            },
        };

        private class MarkerRepository
        {
            public VendingMachineMapMarker vending;
            public MapMarkerGenericRadius marker;
        }
        
        public class PositionEntity
        {
            public List<EntityPos> entityPosList = new List<EntityPos>();
            public SpherePos spherePos = new SpherePos();
            
            public class SpherePos
            {
                public Single sizeSphere;
                public Vector3 position;
            }
            public class EntityPos
            {
                public Vector3 localPosition;
                public Quaternion localQuaternion;
            }
        }
        
        #endregion
        
        #region Configuration
        
        private static Configuration config = new Configuration();

        private class Configuration
        {
            [JsonProperty(LanguageEn ? "Auto event settings for protected cargo plane launch" : "Настройка автоматического запуска самолета с защищенным грузом")]
            public AutoEvent autoEventSetting = new AutoEvent();
            [JsonProperty(LanguageEn ? "Other settings" : "Прочие настройки")]
            public GeneralSetting generalSetting = new GeneralSetting();
            [JsonProperty(LanguageEn ? "Protected cargo presets settings" : "Настройка пресетов защищенных грузов")]
            public Dictionary<String, PresetSupply> presetSupplyDrops = new Dictionary<String, PresetSupply>();
            [JsonProperty(LanguageEn ? "Configuring supported plugins" : "Настройка поддерживаемых плагинов")]
            public OtherPlugins otherPlugins = new OtherPlugins();
            public String GetRandomPreset()
            {
                List<String> randomList = Pool.Get<List<String>>();
                String presetKey = String.Empty;
                    
                randomList = presetSupplyDrops.Keys.ToList();
                presetKey = randomList.GetRandom();
                    
                Pool.FreeUnmanaged(ref randomList);
                    
                return presetKey;
            }
            
            internal class GeneralSetting
            {
                [JsonProperty(LanguageEn ? "Time after which protected cargo will be removed after being fully looted" : "Через сколько будет удален защищенный груз после его полного залутывания")]
                public Int32 timeRevomeDropAfterLoot;
                [JsonProperty(LanguageEn ? "When will the protected loot be removed if players do not loot it" : "Через сколько будет удален защищенный груз если его не залутали игроки")]
                public Int32 timeRevomeDropNoLoot;
                [JsonProperty(LanguageEn ? "Notification settings from the plugin" : "Настройка уведомлений от плагина")]
                public AlertController alertSetting;
                [JsonProperty(LanguageEn ? "Automatically clear custom drop positions on map change/server wipe (true - yes/false - no)" : "Автоматически очищать кастомные позиции для сброса груза при смене карты/очистки сервера (true - да/false - нет)")]
                public Boolean clearCustomPositionsWipe;
                [JsonProperty(LanguageEn ? "Discord notification settings. [MessageType (CargoSpawned - Plane departure, SupplyDropped - Supply drop, SupplyDestroyed - Supply removed, SupplyOpened - Supply opened)] = Setting" : "Настройка уведомлений в Discord. [ТипСообщения (CargoSpawned - Вылет самолета, SupplyDropped - Сброс груза, SupplyDestroyed - Удаление груза, SupplyOpened - Открытие груза)] = Настройка")]
                public Dictionary<TypeActionDiscord, DiscordSettings> typeAlertDiscord = new();
                
                internal class DiscordSettings
                {
                    [JsonProperty(LanguageEn ? "WebHook (leave empty to not use this type of notification)" : "WebHook (оставьте пустым чтобы не использовать этот тип уведомления)")]
                    public String webHooks;
                    [JsonProperty(LanguageEn ? "Title" : "Заголовок")]
                    public String title;
                    [JsonProperty(LanguageEn ? "Description" : "Описание")]
                    public String description;
                    [JsonProperty(LanguageEn ? "Color (Embed discord format)" : "Цвет (формат Embed discord)")]
                    public Int32 color;
                    [JsonProperty(LanguageEn ? "Footer text" : "Текст в подвале")]
                    public String footer;
                    [JsonProperty(LanguageEn ? "Author name" : "Имя автора")]
                    public String authorName;
                    [JsonProperty(LanguageEn ? "Author avatar (use direct .png link)" : "Аватар автора (используйте прямую ссылку .png)")]
                    public String authorAvatar;
                    [JsonProperty(LanguageEn ? "Thumbnail avatar (use direct .png link)" : "Аватар в оглавлении (используйте прямую ссылку .png)")]
                    public String thumbnailAvatar;
                    [JsonProperty(LanguageEn ? "Message above embed (e.g., @everyone)" : "Сообщение над embed (например @everyone)")]
                    public String content;

                    public DiscordMessage GetMessageDiscord()
                    {
                        if (String.IsNullOrWhiteSpace(webHooks)) return null;
                        
                        DiscordMessage message = new DiscordMessage
                        {
                            Embeds = new List<Embed>
                            {
                                new Embed
                                {
                                    Title = title,
                                    Description = description,
                                    Color = color,
                                    Footer = new EmbedFooter { Text = footer },
                                    Author = new EmbedAuthor
                                    {
                                        Name = authorName,
                                        IconUrl = authorAvatar
                                    },
                                    Thumbnail = new EmbedThumbnail
                                    {
                                        Url = thumbnailAvatar
                                    }
                                }
                            },
                            Content = content
                        };

                        return message;
                    }
                }

                internal class AlertController
                {
                    [JsonProperty(LanguageEn ? "IQChat: Notification format settings" : "IQChat : Настройка формата уведомлений")]
                    public IQChatSetting iqchatSetting;
                    [JsonProperty(LanguageEn ? "Use GameTip notification for cargo plane takeoff" : "Использовать уведомление GameTip о вылете самолета с защищенным грузом")]
                    public Boolean useAlertGameTipStartCargo;
                    [JsonProperty(LanguageEn ? "Use chat notification for cargo plane takeoff" : "Использовать уведомление в чате о вылете самолета с защищенным грузом")]
                    public Boolean useAlertChatStartCargo;
                    [JsonProperty(LanguageEn ? "Use chat notification for dropped protected cargo" : "Использовать уведомление в чате о сброшенном защищенном грузе")]
                    public Boolean useAlertDropped;
                    [JsonProperty(LanguageEn ? "Use chat notification when player starts looting protected cargo" : "Использовать уведомление в чате о том что игрок начал лутать защищенный груз")]
                    public Boolean useAlertLootedDrop;
                    
                    internal class IQChatSetting
                    {
                        [JsonProperty(LanguageEn ? "IQChat : Custom prefix in chat" : "IQChat : Кастомный префикс в чате")]
                        public String customPrefix;
                        [JsonProperty(LanguageEn ? "IQChat : Custom chat avatar (If required)" : "IQChat : Кастомный аватар в чате(Если требуется)")]
                        public String customAvatar;
                    }
                }
            }
            
            internal class AutoEvent
            {
                [JsonProperty(LanguageEn ? "Use automatic launch of planes with protected cargo (true - yes/false - no)" : "Использовать автоматический запуск самолетов с защищенным грузом (true - да/false - нет)")]
                public Boolean useAutoEvent;
                [JsonProperty(LanguageEn ? "Preset list settings for automatic launch [Preset] = Chance (From 0 to 100)" : "Настройка списка пресетов для автоматического запуска [Пресет] = Шанс (От 0 до 100)")]
                public Dictionary<String, Int32> presetDroppedList = new Dictionary<String, Int32>();
                [JsonProperty(LanguageEn ? "How often the protected drop will be launched automatically (specify the time in seconds)" : "Раз в какое время будет запускаться автоматически защищенный дроп (укажите время в секундах)")]
                public Int32 timeStarted;
                public String GetRandomPreset()
                {
                    foreach (KeyValuePair<String, Int32> presetList in presetDroppedList)
                    {
                        if (_.IsRare(presetList.Value))
                            return presetList.Key;
                    }
                    
                    List<String> randomList = Pool.Get<List<String>>();
                    String presetKey = String.Empty;
                    
                    randomList = presetDroppedList.Keys.ToList();
                    presetKey = randomList.GetRandom();
                    
                    Pool.FreeUnmanaged(ref randomList);
                    
                    return presetKey;
                }
            }
            
            internal class PresetSupply
            {
                [JsonProperty(LanguageEn ? "Drop protection settings" : "Настройка защиты дропа")]
                public GeneralDrop generalSetting = new GeneralDrop();
                internal class GeneralDrop
                {
                    [JsonProperty(LanguageEn ? "Drop protection level: 0 - Easy, 1 - Medium, 2 - Hard" : "Уровень защиты дропа : 0 - Легий, 1 - Средний, 2 - Сложный")]
                    public TypeLevels defenderLevel;
                    [JsonProperty(LanguageEn ? "Drop protection settings with access card (for defender levels `Medium` and `Hard`)" : "Настройка защиты дропа картой доступа (для уровней защиты `Средний` и `Сложный`)")]
                    public CardReaders cardReaderSetting = new CardReaders();
                    [JsonProperty(LanguageEn ? "G-Map marker display settings" : "Настройка отображения маркеров на G-Map")]
                    public MapMarkerPreset mapMarkerSetting = new MapMarkerPreset();
                    [JsonProperty(LanguageEn ? "Additional settings" : "Настройка дополнений")]
                    public ReferenceAdditional referenceAdditional = new ReferenceAdditional();
                    
                    internal class MapMarkerPreset
                    {
                        [JsonProperty(LanguageEn ? "Display marker with protected cargo on the map (true - yes/false - no)" : "Отображать на карте маркер с защищенным грузом (true - да/false - нет)")]
                        public Boolean useMapMarker;
                        [JsonProperty(LanguageEn ? "Main marker color" : "Основной цвет маркера")]
                        public String mainColorMarker;
                        [JsonProperty(LanguageEn ? "Outline marker color" : "Цвет обводки маркера")]
                        public String additionalColorMarker;
                        [JsonProperty(LanguageEn ? "Marker radius on the map" : "Радиус маркера на карте")]
                        public Single radiusMarker;
                    }
                    
                    internal class ReferenceAdditional
                    {
                        [JsonProperty(LanguageEn ? "IQDronePatrol: Drone protection settings for the drop" : "IQDronePatrol : Настройка дронов для защиты дропа")]
                        public IQDronePatrol droneSpawnSetting = new IQDronePatrol();
                        [JsonProperty(LanguageEn ? "NPCSpawn: NPC protection settings for the drop" : "NPCSpawn : Настройка NPC для защиты дропа")]
                        public NPCSpawn npcSpawnSetting = new NPCSpawn();

                        internal class IQDronePatrol
                        {
                            [JsonProperty(LanguageEn ? "Use defender drones in this preset" : "Использовать дронов-защитников в данном пресете")]
                            public Boolean useDrones;
                            [JsonProperty(LanguageEn ? "Number of drones spawned to protect the drop" : "Сколько дронов будет спавнится для защиты дропа")]
                            public AmountSetting countDrone;
                            [JsonProperty(LanguageEn ? "Number of drones that can attack one player simultaneously" : "Сколько дронов смогут одновремено нападать на одного игрока сразу")]
                            public Int32 droneAttackedCount;
                            [JsonProperty(LanguageEn ? "Drone preset settings and selection chance [PresetFromConfig] = Chance" : "Настройка пресетов дронов и шанс на выбор данного пресета [ПресетИзКонфига] = Шанс")]
                            public Dictionary<String, Int32> keyDrones;
                            
                            public CustomPatrol GetPresetPatrol(Vector3 position)
                            {
                                CustomPatrol myPatrol = new CustomPatrol
                                {
                                    pluginName = "IQDefenderSupply",
                                    position = position,
                                    settingDrone = new CustomPatrol.DroneSetting
                                    {
                                        droneCountSpawned = countDrone.GetAmount(), 
                                        droneAttackedCount = droneAttackedCount, 
                                        keyDrones = keyDrones,
                                    },
                                    settingPosition = new CustomPatrol.PositionSetting
                                    {
                                        countSpawnPoint = 200, 
                                        radiusFindedPoints = 40
                                    },
                                };

                                return myPatrol;
                            }
                        }
                        
                        internal class NPCSpawn
                        {
                            [JsonProperty(LanguageEn ? "Use NPCSpawn in this preset" : "Использовать NPCSpawn в данном пресете")]
                            public Boolean useNpcSpawn;
                            [JsonProperty(LanguageEn ? "Number of NPCs to spawn near the drop" : "Сколько NPC будет заспавнено возле дропа")]
                            public AmountSetting countSpawnNpc;
                            [JsonProperty(LanguageEn ? "Bot settings" : "Настройка ботов")]
                            public PresetBot presetBot = new();
                            
                            internal class PresetBot
                            {
                                [JsonProperty(LanguageEn ? "Bot health" : "Здоровье ботов")]
                                public Single health;
                                [JsonProperty(LanguageEn ? "Damage multiplier" : "Множитель урона")]
                                public Single damageScale;
                                [JsonProperty(LanguageEn ? "Aim cone multiplier" : "Множитель разброса")]
                                public Single aimConeScale;
                                [JsonProperty(LanguageEn ? "Running speed" : "Скорость бега")]
                                public Single runningSpeed;

                                [JsonProperty(LanguageEn ? "NPC clothing" : "Одежда NPC")]
                                public List<ItemBot> wearNPC = new List<ItemBot>(6);
                                [JsonProperty(LanguageEn ? "NPC weapon variation" : "Вариация оружия NPC")]
                                public List<ItemBot> beltNPC = new List<ItemBot>();

                                [JsonProperty(LanguageEn ? "Drop loot settings from NPC" : "Настройка выпадаемого лута с NPC")]
                                public PresetsLoot itemDropLootNPC = new PresetsLoot();
                                
                                internal class ItemBot
                                {
                                    [JsonProperty("Shortname")]
                                    public String shortname;
                                    [JsonProperty("SkinID")]
                                    public UInt64 skinID;
                                    [JsonProperty(LanguageEn ? "Mods weapon" : "Список модов")]
                                    public List<String> mods;
                                }

                                public JObject GetJObjectConfigNpc()
                                {
                                    JArray arrayWear = new JArray();
                                    foreach (ItemBot wearItem in wearNPC)
                                        arrayWear.Add(new JObject { ["ShortName"] = wearItem.shortname, ["Amount"] = 1, ["SkinID"] = wearItem.skinID, });
                                    
                                    JArray arrayBelt = new JArray();
                                    foreach (ItemBot beltItem in beltNPC)
                                        arrayBelt.Add(new JObject { ["ShortName"] = beltItem.shortname, ["Amount"] = 1, ["SkinID"] = beltItem.skinID, ["Mods"] = new JArray { beltItem.mods.Select(y => y) } });

                                    HashSet<String> states = new() { "RoamState", "ChaseState", "CombatState" };

                                    if (beltNPC.Any(x => x.shortname is "rocket.launcher" or "explosive.timed"))
                                        states.Add("RaidState");
                                    
                                    JObject configNpc = new JObject()
                                    {
                                        ["Name"] = "DEFENDER_SUPPLY_BOT",
                                        ["WearItems"] = arrayWear,
                                        ["BeltItems"] = arrayBelt,
                                        ["Kit"] = "",
                                        ["Health"] = health,
                                        ["RoamRange"] = 30f,
                                        ["ChaseRange"] = 90f,
                                        ["DamageScale"] = damageScale,
                                        ["TurretDamageScale"] = 1f,
                                        ["AreaMask"] = 1,
                                        ["AgentTypeID"] = -1372625422,
                                        ["HomePosition"] = "",
                                        ["AimConeScale"] = aimConeScale,
                                        ["States"] = new JArray { states },
                                        ["DisableRadio"] = false,
                                        ["Stationary"] = false,
                                        ["CanUseWeaponMounted"] = false,
                                        ["CanRunAwayWater"] = true,
                                        ["Speed"] = runningSpeed,
                                        ["AttackRangeMultiplier"] = 2f,
                                        ["SenseRange"] = 150f,
                                        ["CheckVisionCone"] = false,
                                        ["MemoryDuration"] = 300f,
                                        ["VisionCone"] = 135f,
                                    };

                                    return configNpc;
                                }
                                    
                            }
                        }
                    }
                    
                    internal class CardReaders
                    {
                        [JsonProperty(LanguageEn ? "Use access cards for drop (true - yes/false - no)" : "Использовать карты доступы для дропа (true - да/false - нет)")]
                        public Boolean useCardLevel;
                        [JsonProperty(LanguageEn ? "Use random access card for drop" : "Использовать случайную карту доступа для дропа")]
                        public Boolean useRandomCardLevel;
                        [JsonProperty(LanguageEn ? "Required access card: 1 - Green, 2 - Blue, 3 - Red" : "Требуемая карта доступа : 1 - Зеленая, 2 - Синяя, 3 - Красная")]
                        public Int32 keyCardLevel;

                        public Int32 GetCardLevel() => useRandomCardLevel ? Oxide.Core.Random.Range(1, 3) : keyCardLevel;
                    }
                }

                [JsonProperty(LanguageEn ? "Turret settings for protected drop" : "Настройка турели на защищенном дропе")]
                public TurretSettings turretSetting = new TurretSettings();
                [JsonProperty(LanguageEn ? "Custom loot settings in drop" : "Настройка кастомного лута в дропе")]
                public PresetsLoot lootSetting = new PresetsLoot();

                internal class PresetsLoot
                {
                    [JsonProperty(LanguageEn ? "Use custom loot list (true - yes/false - no)" : "Использовать свой список лута (true - да/false - нет)")]
                    public Boolean useCustomLoot;
                    [JsonProperty(LanguageEn ? "Maximum loot drops" : "Сколько максимум выпадет лута")]
                    public Int32 maxCountLoot;
                    [JsonProperty(LanguageEn ? "List of loot drops" : "Список выпадаемого лута")]
                    public List<LootSetting> lootList = new List<LootSetting>();
                    
                    public List<ResultLoot> GetLootList()
                    {
                        List<ResultLoot> resutlLoot = new List<ResultLoot>();
                        Random random = new Random();

                        List<LootSetting> shuffledLootList = lootList.OrderBy(x => random.Next()).ToList();
                        
                        foreach (LootSetting lootPreset in shuffledLootList)
                        {
                            if (_.IsRare(lootPreset.rareDrop) && resutlLoot.All(x => x.shortname != lootPreset.shortname))
                                resutlLoot.Add(new ResultLoot
                                {
                                    shortname = lootPreset.shortname,
                                    skinID = lootPreset.skinID,
                                    amount = lootPreset.amountController.GetAmount(),
                                });
                        }
                        
                        return resutlLoot;
                    }
                    
                    internal class ResultLoot
                    {
                        [JsonIgnore]
                        public String shortname;
                        [JsonIgnore]
                        public UInt64 skinID;
                        [JsonIgnore]
                        public Int32 amount;
                    }
                    
                    internal class LootSetting
                    {
                        [JsonProperty(LanguageEn ? "Drop chance" : "Шанс выпадения")]
                        public Int32 rareDrop;
                        [JsonProperty("Shortname")]
                        public String shortname;
                        [JsonProperty("SkinID")]
                        public UInt64 skinID;
                        [JsonProperty(LanguageEn ? "Drop quantity setting" : "Настройка количества выпадения")]
                        public AmountSetting amountController = new();
                    }
                }
                
                internal class AmountSetting
                {
                    [JsonProperty(LanguageEn ? "Minimum quantity" : "Минимальное количество")]
                    public Int32 minAmount;
                    [JsonProperty(LanguageEn ? "Maximum quantity" : "Максимальное количество")]
                    public Int32 maxAmount;

                    public Int32 GetAmount() => minAmount >= maxAmount ? minAmount : Oxide.Core.Random.Range(minAmount, maxAmount);
                }
                
                internal class TurretSettings
                {
                    [JsonProperty(LanguageEn ? "Use the turret on the cargo (true - yes/false - no)" : "Использовать турель на грузе (true - да/false - нет)")]
                    public Boolean useTurrets;
                    [JsonProperty(LanguageEn ? "Will loot drop from the turret upon destruction? (true - yes/false - no)" : "Будет ли выпадать лут с турели в случае ее уничтожения (true - да/false - нет)")]
                    public Boolean dropLootTurret;
                    [JsonProperty(LanguageEn ? "Turret mode: true - passive / false - active" : "Мод турели : true - пассивный / false - активный")]
                    public Boolean passiveMode;
                    [JsonProperty(LanguageEn ? "Enemy detection radius (according to the standard - 30.0)" : "Радиус обнаружения врага (по стандарту - 30.0)")]
                    public Single visRadius;
                    [JsonProperty(LanguageEn ? "Turret accuracy (aimCone) (default 4)" : "Точность турели (aimCone) (стандартно 4)")]
                    public Single accuracy;
                    [JsonProperty(LanguageEn ? "Turret health level (default 1000)" : "Уровень здоровья турели (стандартно 1000)")]
                    public Int32 healthTurret;
                    [JsonProperty(LanguageEn ? "Turret weapon configuration" : "Настройка оружия в турели")]
                    public WeaponSetting weaponTurretSetting = new WeaponSetting();

                    internal class WeaponSetting
                    { 
                        [JsonProperty(LanguageEn ? "Turret weapon" : "Оружие в турели")]
                        public ItemSetting weaponTurret = new ItemSetting();
                        [JsonProperty(LanguageEn ? "List of weapon mods in turret" : "Список модов на оружие в турели")]
                        public List<ItemSetting> weaponMods = new List<ItemSetting>();
                        [JsonProperty(LanguageEn ? "List of ammo in turret" : "Список патронов в турели")]
                        public List<AmmoSetting> ammoTurret = new List<AmmoSetting>();
                        
                        public Item GetWeaponTurret()
                        {
                            Item weapon = ItemManager.CreateByName(weaponTurret.shortname, 1, weaponTurret.skinID);
                            
                            foreach (ItemSetting weaponMod in weaponMods)
                            {
                                Item mod = ItemManager.CreateByName(weaponMod.shortname);
                                
                                if(!mod.MoveToContainer(weapon.contents))
                                    mod.Remove();
                            }
                            return weapon;
                        }

                        internal class AmmoSetting
                        {
                            public String shortname;
                            public Int32 amount;
                        }
                        internal class ItemSetting
                        {
                            public String shortname;
                            public UInt64 skinID;
                        }
                    }
                }
            }

            internal class OtherPlugins
            {
                [JsonProperty(LanguageEn ? "Settings PVEMode" : "Настройка PVEMode")]
                public PveModePlugin pveMode;
                
                internal class PveModePlugin
                {
                    public class ScaleDamageConfig
                    {
                        [JsonProperty(LanguageEn ? "Type of target" : "Тип цели")]
                        public String Type;
                        [JsonProperty(LanguageEn ? "Damage Multiplier" : "Множитель урона")] 
                        public Single Scale;
                    }

                    [JsonProperty(LanguageEn ? "Use the PVE mode of the plugin? [true/false] (PVEMode)" : "Использовать PVE режим работы плагина? [true/false] (PVEMode)")]
                    public Boolean usePveMode;
                    [JsonProperty(LanguageEn ? "Radius zone" : "Радиус купола")]
                    public Single radiusPveZone;
                    [JsonProperty(LanguageEn ? "The amount of damage that the player has to do to become the Event Owner" : "Кол-во урона, которое должен нанести игрок, чтобы стать владельцем ивента")]
                    public Single Damage;
                    [JsonProperty(LanguageEn ? "Damage coefficients for calculate to become the Event Owner" : "Коэффициенты урона для подсчета, чтобы стать владельцем события")]
                    public HashSet<ScaleDamageConfig> ScaleDamage;
                    [JsonProperty(LanguageEn ? "Can the non-owner of the event loot the crates? [true/false]" : "Может ли не владелец ивента грабить ящики? [true/false]")]
                    public Boolean LootCrate;
                    [JsonProperty(LanguageEn ? "Can the non-owner of the event loot NPC corpses? [true/false]" : "Может ли не владелец ивента грабить трупы NPC? [true/false]")]
                    public Boolean LootNpc;
                    [JsonProperty(LanguageEn ? "Can the non-owner of the event deal damage to the NPC? [true/false]" : "Может ли не владелец ивента наносить урон по NPC? [true/false]")]
                    public Boolean DamageNpc;
                    [JsonProperty(LanguageEn ? "Can an Npc attack a non-owner of the event? [true/false]" : "Может ли Npc атаковать не владельца ивента? [true/false]")]
                    public Boolean TargetNpc;
                    [JsonProperty(LanguageEn ? "Allow the non-owner of the event to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента не владельцу ивента? [true/false]")]
                    public Boolean CanEnter;
                    [JsonProperty(LanguageEn ? "Allow a player who has an active cooldown of the Event Owner to enter the event zone? [true/false]" : "Разрешать входить внутрь зоны ивента игроку, у которого активен кулдаун на получение статуса владельца ивента? [true/false]")]
                    public Boolean CanEnterCooldownPlayer;
                    [JsonProperty(LanguageEn ? "The time that the Event Owner may not be inside the event zone [sec.]" : "Время, которое владелец ивента может не находиться внутри зоны ивента [сек.]")]
                    public Int32 TimeExitOwner;
                    [JsonProperty(LanguageEn ? "The time until the end of Event Owner status when it is necessary to warn the player [sec.]" : "Время таймера до окончания действия статуса владельца ивента, когда необходимо предупредить игрока [сек.]")]
                    public Int32 AlertTime;
                    [JsonProperty(LanguageEn ? "Prevent the actions of the RestoreUponDeath plugin in the event zone? [true/false]" : "Запрещать работу плагина RestoreUponDeath в зоне действия ивента? [true/false]")]
                    public Boolean RestoreUponDeath;
                    [JsonProperty(LanguageEn ? "The time that the player can`t become the Event Owner, after the end of the event and the player was its owner [sec.]" : "Время, которое игрок не сможет стать владельцем ивента, после того как ивент окончен и игрок был его владельцем [sec.]")]
                    public Double CooldownOwner;
                    [JsonProperty(LanguageEn ? "Darkening the dome (0 - disables the dome)" : "Затемнение купола (0 - отключает купол)")]
                    public Int32 Darkening;
                }
            }
            
            public static Configuration GetNewConfiguration()
            {
                return new Configuration
                {
                    generalSetting = new GeneralSetting
                    {
                        clearCustomPositionsWipe = true,
                        timeRevomeDropAfterLoot = 180,
                        timeRevomeDropNoLoot = 1800,
                        alertSetting = new GeneralSetting.AlertController
                        {
                            iqchatSetting = new GeneralSetting.AlertController.IQChatSetting()
                            {
                                customAvatar = "0",
                                customPrefix = "<color=#CD412B>[IQDefenderSupply]</color> "
                            },
                            useAlertGameTipStartCargo = true,
                            useAlertChatStartCargo = true,
                            useAlertDropped = true,
                            useAlertLootedDrop = true,
                        },
                        typeAlertDiscord = new Dictionary<TypeActionDiscord, GeneralSetting.DiscordSettings>()
                        {
                            [TypeActionDiscord.CargoSpawned] = new GeneralSetting.DiscordSettings
                            {
                                webHooks = "",
                                title = LanguageEn ? "Cargo plane launched" : "Грузовой самолет вылетел",
                                description = LanguageEn ? "A plane has launched to your island, carrying a special cargo of scientists with special protection!" : "Самолет вылетел на ваш остров, он перевозит специальный груз ученых с особенной защитой!",
                                color = 9824766,
                                footer = "",
                                authorName = LanguageEn ? "Scientist records intercepted" : "Перехваченные записи ученых",
                                authorAvatar = "https://i.ibb.co/RjyHCbs/air-plane-New.png",
                                thumbnailAvatar = "https://i.ibb.co/RjyHCbs/air-plane-New.png",
                                content = "@everyone"
                            },
                            [TypeActionDiscord.SupplyDropped] = new GeneralSetting.DiscordSettings
                            {
                                webHooks = "",
                                title = LanguageEn ? "Supply dropped" : "Защищенный груз сброшен",
                                description = LanguageEn ? "Protected cargo has been dropped on your island. You can seize it! If you can..." : "Защищенный груз был сброшен на вашем острове, вы можете завладеть им! Если у вас получится...",
                                color = 9830049,
                                footer = "",
                                authorName = LanguageEn ? "Scientist records intercepted" : "Перехваченные записи ученых",
                                authorAvatar = "https://i.ibb.co/GM1hf85/supply.png",
                                thumbnailAvatar = "https://i.ibb.co/GM1hf85/supply.png",
                                content = ""
                            },
                            [TypeActionDiscord.SupplyOpened] = new GeneralSetting.DiscordSettings
                            {
                                webHooks = "",
                                title = LanguageEn ? "Protected cargo opened" : "Защищенный груз был открыт",
                                description = LanguageEn ? "Protected cargo has started being looted!" : "Защищенный груз начали грабить!",
                                color = 16709013,
                                footer = "",
                                authorName = LanguageEn ? "Scientist records intercepted" : "Перехваченные записи ученых",
                                authorAvatar = "https://i.ibb.co/GM1hf85/supply.png",
                                thumbnailAvatar = "https://i.ibb.co/GM1hf85/supply.png",
                                content = ""
                            },
                            [TypeActionDiscord.SupplyDestroyed] = new GeneralSetting.DiscordSettings
                            {
                                webHooks = "",
                                title = LanguageEn ? "Protected cargo removed" : "Защищенный груз удален",
                                description = LanguageEn ? "Protected cargo has been completely looted!" : "Защищенный груз был полностью ограблен!",
                                color = 16684437,
                                footer = "",
                                authorName = LanguageEn ? "Scientist records intercepted" : "Перехваченные записи ученых",
                                authorAvatar = "https://i.ibb.co/GM1hf85/supply.png",
                                thumbnailAvatar = "https://i.ibb.co/GM1hf85/supply.png",
                                content = ""
                            },
                        }
                    },
                    autoEventSetting = new AutoEvent
                    {
                        useAutoEvent = true,
                        timeStarted = 3600,
                        presetDroppedList = new Dictionary<String, Int32>()
                        {
                            ["lite_supply"] = 80,
                            ["middle_supply"] = 45,
                            ["hard_supply"] = 10,
                        },
                    },
                    presetSupplyDrops = new Dictionary<String, PresetSupply>()
                    {
                        ["lite_supply"] = new PresetSupply
                        {
                            generalSetting = new PresetSupply.GeneralDrop
                            {
                                defenderLevel = TypeLevels.Level0,
                                cardReaderSetting = new PresetSupply.GeneralDrop.CardReaders
                                {
                                    useCardLevel = false,
                                    useRandomCardLevel = false,
                                    keyCardLevel = 0
                                },
                                mapMarkerSetting = new PresetSupply.GeneralDrop.MapMarkerPreset
                                {
                                    useMapMarker = false,
                                    mainColorMarker = "#738D45",
                                    additionalColorMarker = "#C26D33",
                                    radiusMarker = 0.25f,
                                },
                                referenceAdditional = new PresetSupply.GeneralDrop.ReferenceAdditional
                                {
                                    droneSpawnSetting = new PresetSupply.GeneralDrop.ReferenceAdditional.IQDronePatrol
                                    {
                                        useDrones = false,
                                        countDrone = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 3,
                                            maxAmount = 6
                                        },
                                        droneAttackedCount = 2,
                                        keyDrones = new Dictionary<String, Int32>()
                                        {
                                            ["LITE_DRONE"] = 100,
                                        }
                                    },
                                    npcSpawnSetting = new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn
                                    {
                                        useNpcSpawn = false,
                                        countSpawnNpc = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 6,
                                            maxAmount = 6
                                        },
                                        presetBot = new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot
                                        {
                                            health = 150,
                                            damageScale = 1.25f,
                                            aimConeScale = 1f,
                                            runningSpeed = 7f,
                                            wearNPC = new List<PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot>()
                                            {
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "roadsign.jacket",
                                                    skinID = 2991830202,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "coffeecan.helmet",
                                                    skinID = 2991835101,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "roadsign.kilt",
                                                    skinID = 2991832819,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "hoodie",
                                                    skinID = 2936196960,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "pants",
                                                    skinID = 2936196259,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "shoes.boots",
                                                    skinID = 2980941295,
                                                    mods = new List<String>(),
                                                },
                                            },
                                            beltNPC = new List<PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot>()
                                            {
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "smg.mp5",
                                                    skinID = 2873774818,
                                                    mods = new List<String>()
                                                    {
                                                        "weapon.mod.flashlight"
                                                    }
                                                }
                                            },
                                            itemDropLootNPC = new PresetSupply.PresetsLoot() 
                                            {
                                                useCustomLoot = true,
                                                maxCountLoot = 3,
                                                lootList = new List<PresetSupply.PresetsLoot.LootSetting>()
                                                {
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 1,
                                                        shortname = "smg.mp5",
                                                        skinID = 2873774818,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 52,
                                                        shortname = "ammo.pistol",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 15,
                                                            maxAmount = 60
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 12,
                                                        shortname = "metalpipe",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 3,
                                                            maxAmount = 5
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 10,
                                                        shortname = "sheetmetal",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 10,
                                                        shortname = "metalspring",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 5
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 10,
                                                        shortname = "sparkplug3",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 20,
                                                        shortname = "smgbody",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "syringe.medical",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "largemedkit",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 50,
                                                        shortname = "bandage",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 8,
                                                        shortname = "pickaxe",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 5,
                                                        shortname = "knife.combat",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 20,
                                                        shortname = "weapon.mod.simplesight",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "weapon.mod.silencer",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "roadsign.gloves",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 10,
                                                        shortname = "roadsign.kilt",
                                                        skinID = 2991832819,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 20,
                                                        shortname = "grenade.beancan",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "grenade.flashbang",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "grenade.f1",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 30,
                                                        shortname = "grenade.molotov",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                }
                                            }
                                        }
                                    }
                                },
                            },
                            turretSetting = new PresetSupply.TurretSettings
                            {
                                useTurrets = false,
                                dropLootTurret = false,
                                passiveMode = false,
                                visRadius = 40.0f,
                                accuracy = 4.0f,
                                healthTurret = 1000,
                                weaponTurretSetting = new PresetSupply.TurretSettings.WeaponSetting
                                {
                                    weaponTurret = new PresetSupply.TurretSettings.WeaponSetting.ItemSetting
                                    {
                                        shortname = "smg.thompson",
                                        skinID = 0,
                                    },
                                    weaponMods = new List<PresetSupply.TurretSettings.WeaponSetting.ItemSetting>()
                                    {
                                        new PresetSupply.TurretSettings.WeaponSetting.ItemSetting
                                        {
                                            shortname = "weapon.mod.silencer",
                                            skinID = 0
                                        }
                                    },
                                    ammoTurret = new List<PresetSupply.TurretSettings.WeaponSetting.AmmoSetting>()
                                    {
                                        new PresetSupply.TurretSettings.WeaponSetting.AmmoSetting()
                                        {
                                            shortname = "ammo.pistol.fire",
                                            amount = 150,
                                        },
                                        new PresetSupply.TurretSettings.WeaponSetting.AmmoSetting()
                                        {
                                            shortname = "ammo.pistol.hv",
                                            amount = 150,
                                        },
                                    }
                                },
                            },
                            lootSetting = new PresetSupply.PresetsLoot
                            {
                                useCustomLoot = true,
                                maxCountLoot = 8,
                                lootList = new List<PresetSupply.PresetsLoot.LootSetting>
                                {
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "keycard_green",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 86,
                                        shortname = "ammo.pistol",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 25,
                                            maxAmount = 93
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 63,
                                        shortname = "ammo.shotgun",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 12,
                                            maxAmount = 42
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 53,
                                        shortname = "ammo.rifle",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 25,
                                            maxAmount = 128
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "metal.refined",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 42,
                                            maxAmount = 100
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 73,
                                        shortname = "metal.fragments",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 700,
                                            maxAmount = 3000
                                        }
                                    }, 
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "scrap",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 169,
                                            maxAmount = 320
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "hoodie",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "roadsign.kilt",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 5,
                                        shortname = "metal.facemask",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "pants",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 3,
                                        shortname = "metal.plate.torso",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "coffeecan.helmet",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 21,
                                        shortname = "roadsign.jacket",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "bucket.helmet",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "jackhammer",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "explosive.timed",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 8,
                                        shortname = "supply.signal",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 4,
                                        shortname = "military flamethrower",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 13,
                                        shortname = "smg.mp5",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 12,
                                        shortname = "pistol.m92",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 2,
                                        shortname = "rifle.ak",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 11,
                                        shortname = "rifle.m39",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "pistol.prototype17",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 29,
                                        shortname = "grenade.f1",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 3
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "explosive.satchel",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 2,
                                            maxAmount = 2
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "smg.thompson",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "smg.2",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "rifle.semiauto",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 12,
                                        shortname = "shotgun.spas12",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                }
                            }
                        },
                        ["middle_supply"] = new PresetSupply
                        {
                            generalSetting = new PresetSupply.GeneralDrop
                            {
                                defenderLevel = TypeLevels.Level1,
                                cardReaderSetting = new PresetSupply.GeneralDrop.CardReaders
                                {
                                    useCardLevel = true,
                                    useRandomCardLevel = true,
                                    keyCardLevel = 0
                                },
                                mapMarkerSetting = new PresetSupply.GeneralDrop.MapMarkerPreset
                                {
                                    useMapMarker = false,
                                    mainColorMarker = "#C26D33",
                                    additionalColorMarker = "#738D45",
                                    radiusMarker = 0.25f,
                                },
                                referenceAdditional = new PresetSupply.GeneralDrop.ReferenceAdditional
                                {
                                    droneSpawnSetting = new PresetSupply.GeneralDrop.ReferenceAdditional.IQDronePatrol
                                    {
                                        useDrones = false,
                                        countDrone = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 6,
                                            maxAmount = 6
                                        },
                                        droneAttackedCount = 2,
                                        keyDrones = new Dictionary<String, Int32>()
                                        {
                                            ["LITE_DRONE"] = 30,
                                            ["MIDDLE_DRONE"] = 70,
                                        }
                                    },
                                    npcSpawnSetting = new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn
                                    {
                                        useNpcSpawn = false,
                                        countSpawnNpc = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 6,
                                            maxAmount = 12
                                        },
                                        presetBot = new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot
                                        {
                                            health = 175,
                                            damageScale = 1.2f,
                                            aimConeScale = 1f,
                                            runningSpeed = 7.5f,
                                            wearNPC = new List<PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot>()
                                            {
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "roadsign.jacket",
                                                    skinID = 2991830202,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "metal.facemask",
                                                    skinID = 2894883082,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "roadsign.kilt",
                                                    skinID = 2991832819,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "hoodie",
                                                    skinID = 2936196960,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "pants",
                                                    skinID = 2936196259,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "shoes.boots",
                                                    skinID = 2980941295,
                                                    mods = new List<String>(),
                                                },
                                            },
                                            beltNPC = new List<PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot>()
                                            {
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "rifle.semiauto",
                                                    skinID = 2296659119,
                                                    mods = new List<String>()
                                                    {
                                                        "weapon.mod.flashlight"
                                                    }
                                                }
                                            },
                                            itemDropLootNPC = new PresetSupply.PresetsLoot() 
                                            {
                                                useCustomLoot = true,
                                                maxCountLoot = 3,
                                                lootList = new List<PresetSupply.PresetsLoot.LootSetting>()
                                                {
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 2,
                                                        shortname = "rifle.semiauto",
                                                        skinID = 2296659119,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 60,
                                                        shortname = "ammo.rifle",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 15,
                                                            maxAmount = 60
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 16,
                                                        shortname = "metalpipe",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 3,
                                                            maxAmount = 5
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "sheetmetal",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "metalspring",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 5
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "sparkplug3",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "smgbody",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 45,
                                                        shortname = "syringe.medical",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "largemedkit",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 70,
                                                        shortname = "bandage",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 14,
                                                        shortname = "pickaxe",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 13,
                                                        shortname = "knife.combat",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "weapon.mod.simplesight",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "weapon.mod.silencer",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "roadsign.gloves",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 20,
                                                        shortname = "roadsign.kilt",
                                                        skinID = 2991832819,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 30,
                                                        shortname = "grenade.beancan",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "grenade.flashbang",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "grenade.f1",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 40,
                                                        shortname = "grenade.molotov",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                }
                                            }
                                        }
                                    }
                                },
                            },
                            turretSetting = new PresetSupply.TurretSettings
                            {
                                useTurrets = true,
                                dropLootTurret = false,
                                passiveMode = false,
                                visRadius = 40.0f,
                                accuracy = 4.0f,
                                healthTurret = 0,
                                weaponTurretSetting = new PresetSupply.TurretSettings.WeaponSetting
                                {
                                    weaponTurret = new PresetSupply.TurretSettings.WeaponSetting.ItemSetting
                                    {
                                        shortname = "smg.thompson",
                                        skinID = 0,
                                    },
                                    weaponMods = new List<PresetSupply.TurretSettings.WeaponSetting.ItemSetting>()
                                    {
                                        new PresetSupply.TurretSettings.WeaponSetting.ItemSetting
                                        {
                                            shortname = "weapon.mod.silencer",
                                            skinID = 0
                                        }
                                    },
                                    ammoTurret = new List<PresetSupply.TurretSettings.WeaponSetting.AmmoSetting>()
                                    {
                                        new PresetSupply.TurretSettings.WeaponSetting.AmmoSetting()
                                        {
                                            shortname = "ammo.pistol.fire",
                                            amount = 500,
                                        },
                                        new PresetSupply.TurretSettings.WeaponSetting.AmmoSetting()
                                        {
                                            shortname = "ammo.pistol.hv",
                                            amount = 250,
                                        },
                                    }
                                },
                            },
                            lootSetting = new PresetSupply.PresetsLoot
                            {
                                useCustomLoot = true,
                                maxCountLoot = 12,
                                lootList = new List<PresetSupply.PresetsLoot.LootSetting>
                                {
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "wall.window.bars.toptier",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "wall.window.glass.reinforced",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "furnace.large",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 50,
                                        shortname = "workbench1",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 5,
                                        shortname = "ammo.rocket.basic",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 2
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "guntrap",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "flameturret",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "samsite",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "techparts",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 3,
                                            maxAmount = 6
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 50,
                                        shortname = "propanetank",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 2,
                                            maxAmount = 4
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "riflebody",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "autoturret",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 5,
                                        shortname = "hmlmg",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "shotgun.m4",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 6,
                                        shortname = "rifle.ak",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 3,
                                        shortname = "rocket.launcher",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 2,
                                        shortname = "minigun",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 5,
                                        shortname = "multiplegrenadelauncher",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "rifle.semiauto",
                                        skinID = 2296659119,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "keycard_green",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "keycard_green",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 86,
                                        shortname = "ammo.pistol",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 25,
                                            maxAmount = 93
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 63,
                                        shortname = "ammo.shotgun",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 12,
                                            maxAmount = 42
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 53,
                                        shortname = "ammo.rifle",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 25,
                                            maxAmount = 128
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "metal.refined",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 42,
                                            maxAmount = 100
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 73,
                                        shortname = "metal.fragments",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 700,
                                            maxAmount = 3000
                                        }
                                    }, 
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "scrap",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 169,
                                            maxAmount = 320
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "hoodie",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "roadsign.kilt",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 5,
                                        shortname = "metal.facemask",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "pants",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 3,
                                        shortname = "metal.plate.torso",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "coffeecan.helmet",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 21,
                                        shortname = "roadsign.jacket",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "bucket.helmet",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "jackhammer",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "explosive.timed",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 8,
                                        shortname = "supply.signal",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 4,
                                        shortname = "military flamethrower",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 13,
                                        shortname = "smg.mp5",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 12,
                                        shortname = "pistol.m92",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 2,
                                        shortname = "rifle.ak",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 11,
                                        shortname = "rifle.m39",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "pistol.prototype17",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 29,
                                        shortname = "grenade.f1",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 3
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "explosive.satchel",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 2,
                                            maxAmount = 2
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "smg.thompson",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "smg.2",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "rifle.semiauto",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 12,
                                        shortname = "shotgun.spas12",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                }
                            }
                        },
                        ["hard_supply"] = new PresetSupply
                        {
                            generalSetting = new PresetSupply.GeneralDrop
                            {
                                defenderLevel = TypeLevels.Level2,
                                cardReaderSetting = new PresetSupply.GeneralDrop.CardReaders
                                {
                                    useCardLevel = true,
                                    useRandomCardLevel = false,
                                    keyCardLevel = 3
                                },
                                mapMarkerSetting = new PresetSupply.GeneralDrop.MapMarkerPreset
                                {
                                    useMapMarker = false,
                                    mainColorMarker = "#CD412B",
                                    additionalColorMarker = "#1E2020",
                                    radiusMarker = 0.25f,
                                },
                                referenceAdditional = new PresetSupply.GeneralDrop.ReferenceAdditional
                                {
                                    droneSpawnSetting = new PresetSupply.GeneralDrop.ReferenceAdditional.IQDronePatrol
                                    {
                                        useDrones = false,
                                        countDrone = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 6,
                                            maxAmount = 12
                                        },
                                        droneAttackedCount = 2,
                                        keyDrones = new Dictionary<String, Int32>()
                                        {
                                            ["LITE_DRONE"] = 30,
                                            ["MIDDLE_DRONE"] = 50,
                                            ["HARD_DRONE"] = 70,
                                        }
                                    },
                                    npcSpawnSetting = new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn
                                    {
                                        useNpcSpawn = false,
                                        countSpawnNpc = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 10,
                                            maxAmount = 10
                                        },
                                        presetBot = new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot
                                        {
                                            health = 200,
                                            damageScale = 1.5f,
                                            aimConeScale = 1f,
                                            runningSpeed = 8f,
                                            wearNPC = new List<PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot>()
                                            {
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "metal.plate.torso",
                                                    skinID = 2894881287,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "metal.facemask",
                                                    skinID = 2894883082,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "roadsign.kilt",
                                                    skinID = 2991832819,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "hoodie",
                                                    skinID = 2936196960,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "pants",
                                                    skinID = 2936196259,
                                                    mods = new List<String>(),
                                                },
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "shoes.boots",
                                                    skinID = 2980941295,
                                                    mods = new List<String>(),
                                                },
                                            },
                                            beltNPC = new List<PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot>()
                                            {
                                                new PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn.PresetBot.ItemBot
                                                {
                                                    shortname = "rifle.ak",
                                                    skinID = 2329363015,
                                                    mods = new List<String>()
                                                    {
                                                        "weapon.mod.flashlight"
                                                    }
                                                }
                                            },
                                            itemDropLootNPC = new PresetSupply.PresetsLoot() 
                                            {
                                                useCustomLoot = true,
                                                maxCountLoot = 3,
                                                lootList = new List<PresetSupply.PresetsLoot.LootSetting>()
                                                {
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 3,
                                                        shortname = "rifle.ak",
                                                        skinID = 2329363015,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 2,
                                                        shortname = "rifle.semiauto",
                                                        skinID = 2296659119,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 60,
                                                        shortname = "ammo.rifle",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 15,
                                                            maxAmount = 60
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 16,
                                                        shortname = "metalpipe",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 3,
                                                            maxAmount = 5
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "sheetmetal",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "metalspring",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 5
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "sparkplug3",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 15,
                                                        shortname = "smgbody",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 45,
                                                        shortname = "syringe.medical",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "largemedkit",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 70,
                                                        shortname = "bandage",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 3
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 14,
                                                        shortname = "pickaxe",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 13,
                                                        shortname = "knife.combat",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "weapon.mod.simplesight",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "weapon.mod.silencer",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 25,
                                                        shortname = "roadsign.gloves",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 20,
                                                        shortname = "roadsign.kilt",
                                                        skinID = 2991832819,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 30,
                                                        shortname = "grenade.beancan",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "grenade.flashbang",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 35,
                                                        shortname = "grenade.f1",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    }, 
                                                    new PresetSupply.PresetsLoot.LootSetting
                                                    {
                                                        rareDrop = 40,
                                                        shortname = "grenade.molotov",
                                                        skinID = 0,
                                                        amountController = new PresetSupply.AmountSetting
                                                        {
                                                            minAmount = 1,
                                                            maxAmount = 1
                                                        },
                                                    },
                                                }
                                            }
                                        }
                                    }
                                },
                            },
                            turretSetting = new PresetSupply.TurretSettings
                            {
                                useTurrets = true,
                                dropLootTurret = false,
                                passiveMode = false,
                                visRadius = 50.0f,
                                accuracy = 5.0f,
                                healthTurret = 1500,
                                weaponTurretSetting = new PresetSupply.TurretSettings.WeaponSetting
                                {
                                    weaponTurret = new PresetSupply.TurretSettings.WeaponSetting.ItemSetting
                                    {
                                        shortname = "minigun",
                                        skinID = 0,
                                    },
                                    weaponMods = new List<PresetSupply.TurretSettings.WeaponSetting.ItemSetting>(),
                                    ammoTurret = new List<PresetSupply.TurretSettings.WeaponSetting.AmmoSetting>()
                                    {
                                        new PresetSupply.TurretSettings.WeaponSetting.AmmoSetting()
                                        {
                                            shortname = "ammo.rifle",
                                            amount = 500,
                                        },
                                    }
                                },
                            },
                            lootSetting = new PresetSupply.PresetsLoot
                            {
                                useCustomLoot = true,
                                maxCountLoot = 16,
                                lootList = new List<PresetSupply.PresetsLoot.LootSetting>
                                {
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "metal.plate.torso",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "largebackpack",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "gunpowder",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 300,
                                            maxAmount = 1000
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 60,
                                        shortname = "sulfur",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1000,
                                            maxAmount = 2000
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "ammo.rifle.explosive",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 12,
                                            maxAmount = 64
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 80,
                                        shortname = "ammo.rocket.mlrs",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 3,
                                            maxAmount = 6
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 90,
                                        shortname = "coffin.storage",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "riflebody",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 3
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "generator.wind.scrap",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "electric.teslacoil",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "electric.furnace",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 50,
                                        shortname = "weapon.mod.extendedmags",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 8,
                                        shortname = "lmg.m249",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "rifle.lr300",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "weapon.mod.8x.scope",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "weapon.mod.small.scope",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 6,
                                        shortname = "rifle.l96",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "wall.window.bars.toptier",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "wall.window.glass.reinforced",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "furnace.large",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "workbench1",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "ammo.rocket.basic",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 2,
                                            maxAmount = 3
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 57,
                                        shortname = "guntrap",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "flameturret",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "samsite",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "techparts",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 4,
                                            maxAmount = 8
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 50,
                                        shortname = "propanetank",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 3,
                                            maxAmount = 6
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 34,
                                        shortname = "riflebody",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 3
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "autoturret",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "hmlmg",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 20,
                                        shortname = "shotgun.m4",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 13,
                                        shortname = "rifle.ak",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 8,
                                        shortname = "rocket.launcher",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 3,
                                        shortname = "minigun",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 8,
                                        shortname = "multiplegrenadelauncher",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "rifle.semiauto",
                                        skinID = 2296659119,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 60,
                                        shortname = "keycard_green",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "keycard_red",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "keycard_blue",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 86,
                                        shortname = "ammo.pistol",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 25,
                                            maxAmount = 93
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 63,
                                        shortname = "ammo.shotgun",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 12,
                                            maxAmount = 42
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 53,
                                        shortname = "ammo.rifle",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 25,
                                            maxAmount = 128
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "metal.refined",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 42,
                                            maxAmount = 100
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 73,
                                        shortname = "metal.fragments",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1000,
                                            maxAmount = 5000
                                        }
                                    }, 
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "scrap",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 169,
                                            maxAmount = 320
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "hoodie",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "roadsign.kilt",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 10,
                                        shortname = "metal.facemask",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "pants",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 7,
                                        shortname = "metal.plate.torso",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 35,
                                        shortname = "coffeecan.helmet",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "roadsign.jacket",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 44,
                                        shortname = "bucket.helmet",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "jackhammer",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 15,
                                        shortname = "explosive.timed",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 18,
                                        shortname = "supply.signal",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 8,
                                        shortname = "military flamethrower",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "smg.mp5",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 33,
                                        shortname = "pistol.m92",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 13,
                                        shortname = "rifle.ak",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 17,
                                        shortname = "rifle.m39",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "pistol.prototype17",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 50,
                                        shortname = "grenade.f1",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 3
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 40,
                                        shortname = "explosive.satchel",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 2,
                                            maxAmount = 2
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 30,
                                        shortname = "smg.thompson",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "smg.2",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 25,
                                        shortname = "rifle.semiauto",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },         
                                    new PresetSupply.PresetsLoot.LootSetting
                                    {
                                        rareDrop = 12,
                                        shortname = "shotgun.spas12",
                                        skinID = 0,
                                        amountController = new PresetSupply.AmountSetting
                                        {
                                            minAmount = 1,
                                            maxAmount = 1
                                        }
                                    },
                                }
                            }
                        }
                    },
                    otherPlugins = new OtherPlugins
                    {
                        pveMode = new OtherPlugins.PveModePlugin
                        {
                            usePveMode = false,
                            Damage = 500f,
                            ScaleDamage = new HashSet<OtherPlugins.PveModePlugin.ScaleDamageConfig>()
                            {
                                new OtherPlugins.PveModePlugin.ScaleDamageConfig { Type = "Npc", Scale = 1f },
                                new OtherPlugins.PveModePlugin.ScaleDamageConfig { Type = "Helicopter", Scale = 2f }
                            },
                            radiusPveZone = 30f,
                            LootCrate = false,
                            LootNpc = false,
                            DamageNpc = false,
                            TargetNpc = false,
                            CanEnter = false,
                            CanEnterCooldownPlayer = true,
                            TimeExitOwner = 300,
                            AlertTime = 60,
                            RestoreUponDeath = true,
                            CooldownOwner = 86400,
                            Darkening = 1
                        }
                    }
                };
            }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                config = Config.ReadObject<Configuration>();
                if (config == null) LoadDefaultConfig();

                if (config.otherPlugins.pveMode == null)
                {
                    config.otherPlugins.pveMode = new Configuration.OtherPlugins.PveModePlugin
                    {
                        usePveMode = false,
                        Damage = 500f,
                        ScaleDamage = new HashSet<Configuration.OtherPlugins.PveModePlugin.ScaleDamageConfig>()
                        {
                            new Configuration.OtherPlugins.PveModePlugin.ScaleDamageConfig() { Type = "Npc", Scale = 1f },
                            new Configuration.OtherPlugins.PveModePlugin.ScaleDamageConfig() { Type = "Helicopter", Scale = 2f }
                        },
                        radiusPveZone = 30f,
                        LootCrate = false,
                        LootNpc = false,
                        DamageNpc = false,
                        TargetNpc = false,
                        CanEnter = false,
                        CanEnterCooldownPlayer = true,
                        TimeExitOwner = 300,
                        AlertTime = 60,
                        RestoreUponDeath = true,
                        CooldownOwner = 86400,
                        Darkening = 1
                    };
                }
            }
            catch
            {
                PrintWarning(LanguageEn
                    ? $"Error reading #54327 configuration 'oxide/config/{Name}', creating a new configuration!!"
                    : $"Ошибка чтения #54327 конфигурации 'oxide/config/{Name}', создаём новую конфигурацию!!");
                LoadDefaultConfig();
            }

            NextTick(SaveConfig);
        }

        protected override void LoadDefaultConfig() => config = Configuration.GetNewConfiguration();
        protected override void SaveConfig() => Config.WriteObject(config);

        #endregion

        #region Data

        private Dictionary<String, PositionInfo> positionSaved = new();

        private class PositionInfo
        {
            public String monumentParent;
            public Vector3 position;
            public List<String> presetsPriority = new List<String>();
        }

        private void ReadData()
        {
            positionSaved = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<String, PositionInfo>>("IQSystem/IQDefenderSupply/Positions");
            allMonuments = Oxide.Core.Interface.Oxide.DataFileSystem.ReadObject<Dictionary<String, Vector3>>("IQSystem/IQDefenderSupply/MonumentList");
        }

        private void WriteData()
        {
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQDefenderSupply/Positions", positionSaved);
            Oxide.Core.Interface.Oxide.DataFileSystem.WriteObject("IQSystem/IQDefenderSupply/MonumentList", allMonuments);
        }

        private void ValidateData()
        {
            Dictionary<String, List<String>> removedKeys = new();

            foreach (KeyValuePair<String, PositionInfo> positionInfo in positionSaved)
            {
                List<String> keysToRemove = new();
        
                for (Int32 i = 0; i < positionInfo.Value.presetsPriority.Count; i++)
                {
                    String presetKeyInData = positionInfo.Value.presetsPriority[i];
                    if (!config.presetSupplyDrops.ContainsKey(presetKeyInData))
                    {
                        keysToRemove.Add(presetKeyInData);
                
                        if (!removedKeys.ContainsKey(positionInfo.Key))
                            removedKeys[positionInfo.Key] = new List<String>();

                        removedKeys[positionInfo.Key].Add(presetKeyInData);
                    }
                }
        
                foreach (String key in keysToRemove)
                    positionInfo.Value.presetsPriority.Remove(key);
            }

            String validateInfo = String.Empty;
            foreach (KeyValuePair<String, List<String>> entry in removedKeys)
                validateInfo += $"\n{entry.Key} - {String.Join(", ", entry.Value)}";

            if (!String.IsNullOrWhiteSpace(validateInfo))
            {
                PrintWarning(LanguageEn ? $"Keys in your positions have been checked against the configuration. Keys that are not in the configuration have been removed. Positions with removed keys: {validateInfo}" : $"Ключи в ваших позициях были проверены по конфигурации. Удалены ключи, которых нет в конфигурации. Список позиций с удаленными ключами: {validateInfo}");
                WriteData();
            }
        }



        #endregion
        
        #region Commands
        
        private const String syntaxChatCommand = LanguageEn
            ? "<color=#1F6BA0>Information!</color>\nUse the following syntax:" +
              "\n- <color=#1F6BA0>iqds send.supply NamePos PresetName</color> - send a plane to drop cargo at the specified location" +
              "\n- <color=#1F6BA0>iqds send.supply.player NameOrID* PresetName</color> - send a plane to drop cargo at the player location" +
              "\n- <color=#1F6BA0>iqds send.supply.position Position* PresetName</color> - send the aircraft to drop cargo at certain coordinates" +
              "\n- <color=#1F6BA0>iqds setup.pos NamePos* PresetName (e.g., iqds setup.pos myPosName lite_supply, middle_supply)</color> - set a position linked to a monument" +
              "\n- <color=#1F6BA0>iqds custom.pos NamePos* PresetName (e.g., iqds custom.pos myPosName lite_supply, middle_supply)</color> - set a custom position" +
              "\n- <color=#1F6BA0>iqds remove.pos NamePos*</color> - remove a position" +
              "\n- <color=#1F6BA0>iqds edit.pos NamePos* PresetName (e.g., iqds edit.pos myPosName lite_supply, middle_supply)</color> - edit an existing monument-linked position" +
              "\n- <color=#1F6BA0>iqds edit.custom NamePos* (e.g., iqds edit.custom myPosName lite_supply, middle_supply)</color> - edit an existing custom position" +
              "\n- <color=#1F6BA0>iqds info.custom.pos</color> - list all custom positions" +
              "\n- <color=#1F6BA0>iqds info.monument.pos</color> - list all monument-linked positions" +
              "\n- <color=#1F6BA0>iqds info.all.pos</color> - list all positions" +
              "\n * - required arguments"
              : 
              "<color=#1F6BA0>Информация!</color>\nИспользуйте синтаксис :" +
              "\n- <color=#1F6BA0>iqds send.supply NamePos PresetName</color> - отправить самолет на сброс груза на указанную позицию" +
              "\n- <color=#1F6BA0>iqds send.supply.player NameOrID* PresetName</color> - отправить самолет на сброс груза к игроку" +
              "\n- <color=#1F6BA0>iqds send.supply.position Position* PresetName</color> - отправить самолет на сброс груза на определенные координаты" +
              "\n- <color=#1F6BA0>iqds setup.pos NamePos* PresetName (Например : iqds setup.pos myPosName lite_supply,middle_supply)</color> - для установки позиции привязанной к монументу" +
              "\n- <color=#1F6BA0>iqds custom.pos NamePos* PresetName (Например : iqds custom.pos myPosName lite_supply,middle_supply)</color> - для установки кастомной позиции" +
              "\n- <color=#1F6BA0>iqds remove.pos NamePos*</color> - для удаления позиции" +
              "\n- <color=#1F6BA0>iqds edit.pos NamePos* PresetName (Например : iqds edit.pos myPosName lite_supply,middle_supply)</color> - для редактирования координат существующей позиции привязанной к монументу" +
              "\n- <color=#1F6BA0>iqds edit.custom NamePos* (Например : iqds edit.custom myPosName lite_supply,middle_supply)</color> - для редактирования координат существующей кастомной позиции" +
              "\n- <color=#1F6BA0>iqds info.custom.pos</color> - вывод всех кастомных позиций" +
              "\n- <color=#1F6BA0>iqds info.monument.pos</color> - вывод всех привязанных к монументам позиций" +
              "\n- <color=#1F6BA0>iqds info.all.pos</color> - вывод всех позиций" +
              "\n * - обязательные аргументы";
        
        private const String syntaxConsoleCommand = LanguageEn
            ? "Information!\nUse the following syntax:" +
              "\n- iqds send.supply NamePos PresetName - send a plane to drop cargo at the specified location" +
              "\n- iqds send.supply.player NameOrID* PresetName - send a plane to drop cargo at the player location" +
              "\n- iqds send.supply.position Position* PresetName - send the aircraft to drop cargo at certain coordinates" +
              "\n- iqds setup.pos NamePos* PresetName (e.g., iqds setup.pos myPosName lite_supply, middle_supply) - set a position linked to a monument" +
              "\n- iqds custom.pos NamePos* PresetName (e.g., iqds custom.pos myPosName lite_supply, middle_supply) - set a custom position" +
              "\n- iqds remove.pos NamePos* - remove a position" +
              "\n- iqds edit.pos NamePos* PresetName (e.g., iqds edit.pos myPosName lite_supply, middle_supply) - edit an existing monument-linked position" +
              "\n- iqds edit.custom NamePos* (e.g., iqds edit.custom myPosName lite_supply, middle_supply) - edit an existing custom position" +
              "\n- iqds info.custom.pos - list all custom positions" +
              "\n- iqds info.monument.pos - list all monument-linked positions" +
              "\n- iqds info.all.pos - list all positions" +
              "\n * - required arguments"
              : 
            "Информация!\nИспользуйте синтаксис :" +
            "\n- iqds send.supply NamePos PresetName - отправить самолет на сброс груза на указанную позицию" +
            "\n- iqds send.supply.player NameOrID* PresetName - отправить самолет на сброс груза к игроку" +
            "\n- iqds send.supply.position Position* PresetName - отправить самолет на сброс груза на определнные координаты" +
            "\n- iqds setup.pos NamePos* PresetName (Например : iqds setup.pos myPosName lite_supply,middle_supply) - для установки позиции привязанной к монументу" +
            "\n- iqds custom.pos NamePos* PresetName (Например : iqds custom.pos myPosName lite_supply,middle_supply) - для установки кастомной позиции" +
            "\n- iqds remove.pos NamePos* - для удаления позиции" +
            "\n- iqds edit.pos NamePos* PresetName (Например : iqds edit.pos myPosName lite_supply,middle_supply) - для редактирования координат существующей позиции привязанной к монументу" +
            "\n- iqds edit.custom NamePos* (Например : iqds edit.custom myPosName lite_supply,middle_supply) - для редактирования координат существующей кастомной позиции" +
            "\n- iqds info.custom.pos - вывод всех кастомных позиций" +
            "\n- iqds info.monument.pos - вывод всех привязанных к монументам позиций" +
            "\n- iqds info.all.pos - вывод всех позиций" +
            "\n * - обязательные аргументы";
        
        [ConsoleCommand("iqds")]
        private void ConsoleCommandPlugin(ConsoleSystem.Arg args)
        {
            BasePlayer player = args.Player();
            if (player != null && !player.IsAdmin) return;
            
            if (args?.Args == null || args.Args.Length < 1)
            {
                if (player != null)
                    SendChat(syntaxChatCommand, player);
                else Puts(syntaxConsoleCommand);
                return;
            }
            
            String key = args.Args[0];
            switch (key)
            {
                case "info.custom.pos":
                {
                    GetCustomPositions(player);
                    break;
                }
                case "info.monument.pos":
                {
                    GetParentPositions(player);
                    break;
                }
                case "info.all.pos":
                {
                    GetAllPositions(player);
                    break;
                }
                case "send.supply":
                {
                    String namePos = args.Args.Length >= 2 ? args.Args[1] : String.Empty;
                    String keyPreset = args.Args.Length == 3 ? args.Args[2] : String.Empty;
                    
                    SendCargoPlane(keyPreset, namePos, player, isCommand: true);
                    break;
                }
                case "send.supply.player":
                {
                    String playerNameOrID = args.Args.Length >= 2 ? args.Args[1] : String.Empty;
                    if (String.IsNullOrWhiteSpace(playerNameOrID))
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "You did not specify the name or Steam64ID of the player you want to send the shipment to" : "Вы не указали имя или Steam64ID игрока к которому хотите отправить груз", player);
                        else Puts(LanguageEn ? "You did not specify the name or Steam64ID of the player you want to send the shipment to" : "Вы не указали имя или Steam64ID игрока к которому хотите отправить груз");
                        return;
                    }
                    
                    BasePlayer playerTarget = BasePlayer.Find(playerNameOrID);
                    if (!playerTarget)
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "There is no such player on the server" : "Такого игрока нет на сервере", player);
                        else Puts(LanguageEn ? "There is no such player on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                    
                    String keyPreset = args.Args.Length == 3 ? args.Args[2] : String.Empty;
                    
                    SendCargoPlane(keyPreset, default, player, isCommand: true, playerTarget: playerTarget);
                    break;
                }
                case "send.supply.position":
                {
                    String customPosition = args.Args.Length >= 2 ? String.Join(" ", args.Args.Skip(1)) : String.Empty;
                    if (String.IsNullOrWhiteSpace(customPosition))
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "You have not specified a shipping position" : "Вы не указали позицию для отправки груза", player);
                        else Puts(LanguageEn ? "You have not specified a shipping position" : "Вы не указали позицию для отправки груза");
                        return;
                    }
                    
                    if (!TryConvertToVector3(customPosition, out Vector3 customPos))
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "Specify the position correctly, for example -1214.66, 0.54, 68.58" : "Укажите позицию корректно, пример -1214.66, 0.54, 68.58", player);
                        else Puts(LanguageEn ? "Specify the position correctly, for example -1214.66, 0.54, 68.58" : "Укажите позицию корректно, пример -1214.66, 0.54, 68.58");
                        return;
                    }
                    
                    String keyPreset = args.Args.Length == 3 ? args.Args[2] : String.Empty;
                    
                    SendCargoPlane(keyPreset, default, player, isCommand: true, customPos: customPos);
                    break;
                }
                default:
                {
                    if (player != null)
                        SendChat(syntaxChatCommand, player);
                    else Puts(syntaxConsoleCommand);
                    break;
                }
            }
        }

        [ChatCommand("iqds")]
        private void ChatCommandPlugin(BasePlayer player, String cmd, String[] args)
        {
            if (!player.IsAdmin) return;
    
            if (args == null || args.Length < 1)
            {
                SendChat(syntaxChatCommand, player);
                return;
            }
            
            String key = args[0];
            String namePos = String.Empty;

            if (!key.Contains("info") && !key.Contains("send.supply"))
            {
                if (args.Length < 2)
                {
                    SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nYou did not specify the position name" : "<color=#CD412B>Ошибка!</color>Вы не указали название позиции", player);
                    return;
                }
                
                namePos = args[1];
            }
            
            if(key.Contains("send.supply"))
                namePos = args.Length >= 2 ? args[1] : String.Empty;

            switch (key)
            {
                case "info.custom.pos":
                {
                    GetCustomPositions(player);
                    break;
                }
                case "info.monument.pos":
                {
                    GetParentPositions(player);
                    break;
                }
                case "info.all.pos":
                {
                    GetAllPositions(player);
                    break;
                }
                case "send.supply":
                {
                    String keyPreset = args.Length == 3 ? args[2] : String.Empty; 
                    
                    SendCargoPlane(keyPreset, namePos, player, isCommand: true);
                    break;
                }
                case "send.supply.player":
                {
                    String playerNameOrID = args.Length >= 2 ? args[1] : String.Empty;
                    if (String.IsNullOrWhiteSpace(playerNameOrID))
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "You did not specify the name or Steam64ID of the player you want to send the shipment to" : "Вы не указали имя или Steam64ID игрока к которому хотите отправить груз", player);
                        else Puts(LanguageEn ? "You did not specify the name or Steam64ID of the player you want to send the shipment to" : "Вы не указали имя или Steam64ID игрока к которому хотите отправить груз");
                        return;
                    }
                    
                    BasePlayer playerTarget = BasePlayer.Find(playerNameOrID);
                    if (!playerTarget)
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "There is no such player on the server" : "Такого игрока нет на сервере", player);
                        else Puts(LanguageEn ? "There is no such player on the server" : "Такого игрока нет на сервере");
                        return;
                    }
                    
                    String keyPreset = args.Length == 3 ? args[2] : String.Empty;
                    
                    SendCargoPlane(keyPreset, default, player, isCommand: true, playerTarget: playerTarget);
                    break;
                }
                case "send.supply.position":
                {
                    String customPosition = args.Length >= 2 ? String.Join(" ", args.Skip(1)) : String.Empty;
                    if (String.IsNullOrWhiteSpace(customPosition))
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "You have not specified a shipping position" : "Вы не указали позицию для отправки груза", player);
                        else Puts(LanguageEn ? "You have not specified a shipping position" : "Вы не указали позицию для отправки груза");
                        return;
                    }
                    
                    if (!TryConvertToVector3(customPosition, out Vector3 customPos))
                    {
                        if (player != null)
                            SendChat(LanguageEn ? "Specify the position correctly, for example -1214.66, 0.54, 68.58" : "Укажите позицию корректно, пример -1214.66, 0.54, 68.58", player);
                        else Puts(LanguageEn ? "Specify the position correctly, for example -1214.66, 0.54, 68.58" : "Укажите позицию корректно, пример -1214.66, 0.54, 68.58");
                        return;
                    }
                    
                    String keyPreset = args.Length == 3 ? args[2] : String.Empty;
                    
                    SendCargoPlane(keyPreset, default, player, isCommand: true, customPos: customPos);
                    break;
                }
                case "custom.pos":
                {
                    String prestList = args.Length >= 3 ? String.Join("", args.Skip(2)) : String.Empty;
                    
                    SetupPosition(player, namePos, presets: prestList);
                    break;
                }
                case "setup.pos":
                {
                    String prestList = args.Length >= 3 ? String.Join("", args.Skip(2)) : String.Empty;
                    
                    SetupPosition(player, namePos, presets: prestList, isMonument:true);
                    break;
                }
                case "remove.pos":
                {
                    RemovePosition(player, namePos);
                    break;
                }
                case "edit.custom":
                {
                    String prestList = args.Length >= 3 ? String.Join("", args.Skip(2)) : String.Empty;
                    
                    SetupPosition(player, namePos, true, prestList);
                    break;
                }
                case "edit.pos":
                {
                    String prestList = args.Length >= 3 ? String.Join("", args.Skip(2)) : String.Empty;
                    
                    SetupPosition(player, namePos, true, prestList, isMonument: true);
                    break;
                }
                default:
                {
                    SendChat(syntaxChatCommand, player);
                    break;
                }
            }
        }

        #endregion
        
        #region Metods

        #region Help Commands

        private void GetAllPositions(BasePlayer owner)
        {
            if (positionSaved == null || positionSaved.Count == 0)
            {
                if(owner != null)
                    SendChat(LanguageEn ? "<color=#1F6BA0>Info</color>\nDrop positions are not created" : "<color=#1F6BA0>Информация</color>\nПозиции для сброса груза не созданы", owner);
                else Puts(LanguageEn ? "Info\nDrop positions are not created" : "Информация\nПозиции для сброса груза не созданы");
                return;
            }

            String infoPositions = String.Empty;

            foreach (KeyValuePair<String,PositionInfo> positionInfo in positionSaved)
            {
                String infoCustomOrParent = String.IsNullOrWhiteSpace(positionInfo.Value.monumentParent) ? "Custom" : $"Parent {ExtractSubstring(positionInfo.Value.monumentParent)}";
                String supportKeys = positionInfo.Value.presetsPriority.Count == 0 ? (LanguageEn ? "random presets" : "случайные пресеты") : JoinStringList(positionInfo.Value.presetsPriority);
                String infoKeys = LanguageEn ? $"Presets: {supportKeys}" : $"Пресеты: {supportKeys}";
                infoPositions += LanguageEn ? $"\nKey: {positionInfo.Key}\nPosition Type: {infoCustomOrParent}\nCoordinates: {positionInfo.Value.position}\n{infoKeys}\n" : $"\nКлюч: {positionInfo.Key}\nТип позиции: {infoCustomOrParent}\nКоординаты: {positionInfo.Value.position}\n{infoKeys}\n";
            }
            
            if(owner != null)  
                SendChat(LanguageEn ? $"<color=#1F6BA0>Info</color>\nPositions for drop:\n{infoPositions}\n" : $"<color=#1F6BA0>Info</color>\nПозиции для сброса :\n{infoPositions}", owner);
            else Puts(LanguageEn ? $"Info\nPositions for drop:\n{infoPositions}\n" : $"Info\nПозиции для сброса :\n{infoPositions}");
        }
        
        private void GetParentPositions(BasePlayer owner)
        {
            if (positionSaved == null || positionSaved.Count == 0)
            {
                if(owner != null)  
                    SendChat(LanguageEn ? "<color=#1F6BA0>Info</color>\nPositions for cargo drop not created" : "<color=#1F6BA0>Info</color>\nПозиции для сброса груза не созданы", owner);
                else Puts(LanguageEn ? "Info\nPositions for cargo drop not created" : "Info\nПозиции для сброса груза не созданы");
                return;
            }

            String infoPositions = String.Empty;

            foreach (KeyValuePair<String,PositionInfo> positionInfo in positionSaved)
            {
                if (String.IsNullOrWhiteSpace(positionInfo.Value.monumentParent)) continue;
                String infoCustomOrParent = $"Parent {ExtractSubstring(positionInfo.Value.monumentParent)}";
                String supportKeys = positionInfo.Value.presetsPriority.Count == 0 ? (LanguageEn ? "random presets" : "случайные пресеты") : JoinStringList(positionInfo.Value.presetsPriority);
                String infoKeys = LanguageEn ? $"Presets : {supportKeys}" : $"Пресеты : {supportKeys}";
                infoPositions += LanguageEn ? $"\nKey: {positionInfo.Key}\nPosition Type: {infoCustomOrParent}\nCoordinates: {positionInfo.Value.position}\n{infoKeys}\n" : $"\nКлюч : {positionInfo.Key}\nТип позиции : {infoCustomOrParent}\nКоординаты : {positionInfo.Value.position}\n{infoKeys}\n";
            }
            
            if(owner != null)  
                SendChat(LanguageEn ? $"<color=#1F6BA0>Info</color>\nPositions for drop:\n{infoPositions}\n" : $"<color=#1F6BA0>Info</color>\nПозиции для сброса :\n{infoPositions}", owner);
            else Puts(LanguageEn ? $"Info\nPositions for drop:\n{infoPositions}\n" : $"Info\nПозиции для сброса :\n{infoPositions}");
        }
        
        private void GetCustomPositions(BasePlayer owner)
        {
            if (positionSaved == null || positionSaved.Count == 0)
            {
                if(owner != null)  
                    SendChat(LanguageEn ? "<color=#1F6BA0>Info</color>\nPositions for cargo drop not created" : "<color=#1F6BA0>Info</color>\nПозиции для сброса груза не созданы", owner);
                else Puts(LanguageEn ? "Info\nPositions for cargo drop not created" : "Info\nПозиции для сброса груза не созданы");
                return;
            }

            String infoPositions = String.Empty;

            foreach (KeyValuePair<String,PositionInfo> positionInfo in positionSaved)
            {
                if (!String.IsNullOrWhiteSpace(positionInfo.Value.monumentParent)) continue;
                String supportKeys = positionInfo.Value.presetsPriority.Count == 0 ? (LanguageEn ? "random presets" : "случайные пресеты") : JoinStringList(positionInfo.Value.presetsPriority);
                String infoKeys = LanguageEn ? $"Presets : {supportKeys}" : $"Пресеты : {supportKeys}";
                infoPositions += LanguageEn ? "\nKey: {positionInfo.Key}\nPosition type: Custom\nCoordinates: {positionInfo.Value.position}\n{infoKeys}\n" : $"\nКлюч : {positionInfo.Key}\nТип позиции : Custom\nКоординаты : {positionInfo.Value.position}\n{infoKeys}\n";
            }
            
            if(owner != null)  
                SendChat(LanguageEn ? $"<color=#1F6BA0>Info</color>\nDrop positions:\n{infoPositions}\n" : $"<color=#1F6BA0>Info</color>\nПозиции для сброса :\n{infoPositions}", owner);
            else Puts(LanguageEn ? $"Info\nDrop positions:\n{infoPositions}" : $"Info\nПозиции для сброса :\n{infoPositions}");
        }
        
        #endregion
        
        #region Position Controller
        private void SetupPosition(BasePlayer player, String posName, Boolean isEditPos = false, String presets = default, Boolean isMonument = false)
        {
            if (player.InSafeZone())
            {
                SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nCannot set drop position in a safe zone" : "<color=#CD412B>Ошибка!</color>\nНельзя установить позицию для груза в безопасной зоне", player);
                return;
            }
            
            Vector3 playerPosition = player.transform.position + new Vector3(0f, 1f, 0f); 
            Vector3 position = playerPosition;
            String parentMonument = String.Empty;
            
            if (isMonument)
            {
                parentMonument = GetMonumentKey(player.transform.position);
                if (!String.IsNullOrWhiteSpace(parentMonument))
                {
                    MonumentInfo monument = GetMonument(parentMonument);
                    if (monument == null)
                    {
                        SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nMonument recognition failed for binding to position, set the position on or closer to the monument" : $"<color=#CD412B>Ошибка!</color>\nМонумент не удалось распознать для привязки к позиции, установите позицию на монументе или ближе к нему", player);
                        return;
                    }
                    position = monument.transform.InverseTransformPoint(playerPosition);
                }
            }
            
            Boolean isGodPos = IsPositionValid(playerPosition);
            List<String> priorityPresets = new List<String>();
            String existsKeys = String.Empty;
            String errorMessageExistsKeys = String.Empty;

            if (!isGodPos)
            {
                SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nThe position you are standing on is invalid; there are objects around that will interfere with the cargo landing properly" : "<color=#CD412B>Ошибка!</color>\nПозиция на которой вы стоите некорректная, вокруг есть объекты которые помешают грузу правильно приземлиться", player);
                return;
            }

            if (!String.IsNullOrWhiteSpace(presets))
            {
                priorityPresets = ConvertStringToList(presets);

                for (Int32 i = 0; i < priorityPresets.Count; i++)
                {
                    String customKey = priorityPresets[i];

                    if (!config.presetSupplyDrops.ContainsKey(customKey))
                    {
                        priorityPresets.Remove(customKey);
                        existsKeys += $"\n- {customKey}";
                    }
                }
            }
            
            if(!String.IsNullOrWhiteSpace(existsKeys))
                errorMessageExistsKeys = LanguageEn ? $"\n\nInvalid keys were found that are not in the configuration - they have been removed from the specified list.\nList of invalid keys: {existsKeys}" : $"\n\nБыли найдены неверные ключи, которых нет в конфигурации - они были удалены из указанного списка.\nСписок неверных ключей : {existsKeys}";

            if (isEditPos)
            {
                if (!positionSaved.ContainsKey(posName))
                {
                    SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nA position with the name <color=#C26D33>{posName}</color> does not exists" : $"<color=#CD412B>Ошибка!</color>\nПозиция с названием <color=#C26D33>{posName}</color> не существует", player);
                    return;
                }
                
                positionSaved[posName].position = position;
                positionSaved[posName].monumentParent = parentMonument;

                String infoPresetPosition = LanguageEn ? "Presets for the position have not been changed" : "Пресеты для позиции не были изменены";

                if (priorityPresets.Count != 0)
                {
                    positionSaved[posName].presetsPriority = priorityPresets;
                    
                    String presetPositions = String.Join("\n- ", priorityPresets);
                    
                    infoPresetPosition = LanguageEn ? $"New presets for the position : \n- {presetPositions}" : $"Новые пресеты для позиции : \n- {presetPositions}";
                }
                        
                SendChat(LanguageEn ? $"<color=#738D45>Success!</color>\nYou have successfully edit the landing position for the cargo with the name <color=#C26D33>{posName}</color>\n{infoPresetPosition}{errorMessageExistsKeys}" : $"<color=#738D45>Успешно!</color>\nВы успешно изменили позицию для приземления груза с названием <color=#C26D33>{posName}</color>\n{infoPresetPosition}{errorMessageExistsKeys}", player);
            }
            else
            {
                if (!positionSaved.TryAdd(posName, new PositionInfo()))
                {
                    SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nA position with the name <color=#C26D33>{posName}</color> already exists" : $"<color=#CD412B>Ошибка!</color>\nПозиция с названием <color=#C26D33>{posName}</color> уже существует", player);
                    return;
                }
                
                String presetPositions = priorityPresets.Count == 0 ? (LanguageEn ? "random" : "случайные") : String.Join("\n- ", priorityPresets);
                positionSaved[posName].position = position;
                positionSaved[posName].presetsPriority = priorityPresets;
                positionSaved[posName].monumentParent = parentMonument;
                
                SendChat(LanguageEn ? $"<color=#738D45>Success!</color>\nYou have successfully set the landing position for the cargo with the name <color=#C26D33>{posName}</color>\nPresets for this position :\n- {presetPositions}{errorMessageExistsKeys}" : $"<color=#738D45>Успешно!</color>\nВы успешно установили позицию для приземления груза с названием <color=#C26D33>{posName}</color>\nПресеты для данной позиции :\n- {presetPositions}{errorMessageExistsKeys}", player);
            }
        }
        
        private void RemovePosition(BasePlayer player, String posName)
        {
            if (!positionSaved.ContainsKey(posName))
            {
                SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nA position with the name <color=#C26D33>{posName}</color> does not exists" : $"<color=#CD412B>Ошибка!</color>\nПозиция с названием <color=#C26D33>{posName}</color> не существует", player);
                return;
            }

            positionSaved.Remove(posName);
            SendChat(LanguageEn ? $"<color=#738D45>Success!</color>\nYou have successfully deleted the position named <color=#C26D33>{posName}</color>" : $"<color=#738D45>Успешно!</color>\nВы успешно удалили позицию, с названием <color=#C26D33>{posName}</color>", player);
        }

        private String GetMonumentKey(Vector3 position)
        {
            KeyValuePair<String, Vector3> closestPosition = allMonuments
                .OrderBy(pos => Vector3.Distance(pos.Value, position))
                .FirstOrDefault();

            return closestPosition.Key ?? String.Empty;
        }
        
        private MonumentInfo GetMonument(String monumentName) => TerrainMeta.Path.Monuments.FirstOrDefault(x => x.name == monumentName);
        private Vector3 GetResultVector(MonumentInfo monument, Vector3 position) => monument.transform.position + monument.transform.rotation * position;
        
        private void ParseMonuments(Boolean isNewSave = false)
        {
            if(!isNewSave)
                if (allMonuments.Count != 0)
                    return;

            allMonuments.Clear();
            
            Int32 countAddedMonuments = 0;
            foreach (MonumentInfo monument in TerrainMeta.Path.Monuments)
            {
                if (monument.IsSafeZone ||
                    monument.Type == MonumentType.WaterWell ||
                    monument.Type == MonumentType.Cave ||
                    monument.Bounds.size == Vector3.zero)
                {
                    continue;
                } 
                
                Vector3 posMonument = monument.transform.position;
                String nameMonument = monument.name;
                
                if (!allMonuments.TryAdd(nameMonument, posMonument))
                    continue;

                countAddedMonuments++;
            }
            
            Puts(LanguageEn ? $"Information received about {countAddedMonuments} monuments on the map" : $"Получена информация о {countAddedMonuments} монументах на карте");
            WriteData();
        }
        
        #endregion
        
        #region Auto Event
        
        private void StartAutoEvent()
        {
            Configuration.AutoEvent configAutoStart = config.autoEventSetting;
            if (!configAutoStart.useAutoEvent) return;

            if(timerController is { Destroyed: false })
                timerController.Destroy();

            timerController = timer.Once(configAutoStart.timeStarted, () =>
            {
                String presetKey = configAutoStart.GetRandomPreset();

                SendCargoPlane(presetKey);
                StartAutoEvent();
            });
        }

        #endregion
        
        #region Cargo Plane

        private void SendCargoPlane(String keyPreset, String keyPosition = default, BasePlayer ownerAction = null, Boolean isApi = false, Boolean isCommand = false, BasePlayer playerTarget = null, Vector3 customPos = default) 
        {
            if(!String.IsNullOrWhiteSpace(keyPreset))
            {
                if (!config.presetSupplyDrops.ContainsKey(keyPreset))
                {
                    if (ownerAction != null)
                        SendChat(LanguageEn ? $"<color=#CD412B>Error!</color>\nSending cargo plane with key <color=#C26D33>{keyPreset}</color> is not possible. The key does not exist in the configuration.\n" : $"<color=#CD412B>Ошибка!</color>\nОтправить самолет с грузом по ключу <color=#C26D33>{keyPreset}</color> невозможно. Ключ не существует в конфигурации", ownerAction);
                    else PrintWarning(LanguageEn ? $"{(isApi ? "API :" : "")}Error!\nSending cargo plane with key {keyPreset} is not possible. The key does not exist in the configuration." : $"{(isApi ? "API :" : "")}Ошибка!\nОтправить самолет с грузом по ключу {keyPreset} невозможно. Ключ не существует в конфигурации");
                    return;
                }
            }
            else keyPreset = config.GetRandomPreset();
            
            if (playerTarget == null && (positionSaved == null || positionSaved.Count == 0))
            {
                if(ownerAction != null)
                    SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nNo saved positions for sending cargo!" : "<color=#CD412B>Ошибка!</color>\nНет сохраненных позиций для отправки груза!", ownerAction);
                else PrintWarning(LanguageEn ? $"{(isApi ? "API :" : "")}Error!\nNo saved positions for sending cargo!" : $"{(isApi ? "API :" : "")}Ошибка!\nНет сохраненных позиций для отправки груза!");
                return;
            }
            
            CargoPlane cargoPlane = GameManager.server.CreateEntity(cargoPlanePrefab) as CargoPlane;
            Vector3 sendPosition = default;
            if (!playerTarget && customPos == default)
            {
                if (!String.IsNullOrWhiteSpace(keyPosition))
                {
                    if (!positionSaved.TryGetValue(keyPosition, out PositionInfo positionInfo))
                    {
                        if (ownerAction != null)
                            SendChat(LanguageEn ? $"<color=#CD412B>Error!</color>\nPosition with name <color=#C26D33>{keyPosition}</color> does not exist!" : $"<color=#CD412B>Ошибка!</color>\nПозиции с названием <color=#C26D33>{keyPosition}</color> не существует!", ownerAction);
                        else PrintWarning(LanguageEn ? $"{(isApi ? "API :" : "")}Error!\nPosition with name {keyPosition} does not exist!" : $"{(isApi ? "API :" : "")}Ошибка!\nПозиции с названием {keyPosition} не существует!");
                        return;
                    }

                    if (!String.IsNullOrWhiteSpace(positionInfo.monumentParent))
                    {
                        MonumentInfo monument = GetMonument(positionInfo.monumentParent);
                        if (monument == null)
                        {
                            if (ownerAction != null)
                                SendChat(LanguageEn ? $"<color=#CD412B>Error!</color>\nFailed to find a monument-bound position, perhaps the monument is missing on the map." : $"<color=#CD412B>Ошибка!</color>\nНе смогли найти позицию привязанную к монументу, возможно монумент отсутствует на карте", ownerAction);
                            else PrintWarning(LanguageEn ? $"{(isApi ? "API :" : "")}Error!\nFailed to find a position bound to a monument, perhaps the monument is missing on the map." : $"{(isApi ? "API :" : "")}Ошибка!\nНе смогли найти позицию привязанную к монументу, возможно монумент отсутствует на карте");

                            return;
                        }

                        sendPosition = GetResultVector(monument, positionInfo.position);
                    }
                    else sendPosition = positionInfo.position;

                    if (positionInfo.presetsPriority.Count != 0) keyPreset = positionInfo.presetsPriority.GetRandom();
                }
                else sendPosition = GetRandomPositionCargo();
            }
            else if(playerTarget) sendPosition = playerTarget.transform.position;
            else if (customPos != default) sendPosition = customPos;

            if (sendPosition == Vector3.zero)
            {
                if (ownerAction != null)
                    SendChat(LanguageEn ? "<color=#CD412B>Error!</color>\nPosition not found! Perhaps you have few monuments or all positions are already occupied by cargo" : "<color=#CD412B>Ошибка!</color>\nПозиция не найдена! Возможно у вас мало монументов или все позиции уже заняты грузами", ownerAction);
                else PrintWarning(LanguageEn ? $"{(isApi ? "API :" : "")}Error!\nPosition not found! Perhaps you have few monuments or all positions are already occupied by cargo" : $"{(isApi ? "API :" : "")}Ошибка!\nПозиция не найдена! Возможно у вас мало монументов или все позиции уже заняты грузами");
                return;
            }
            
            cargoPlane.InitDropPosition(sendPosition);
            cargoPlane.Spawn();
            cargoPlane.OwnerID = ownerIDElemnts;

            if (config.generalSetting.alertSetting.useAlertGameTipStartCargo)
                MessageGameTipsError("ALERT_GAMETIPS_DEFENDER_CARGO_PLANE_START");
            
            if(config.generalSetting.alertSetting.useAlertChatStartCargo)
                foreach (BasePlayer player in BasePlayer.allPlayerList)
                    SendChat(GetLang("ALERT_CHAT_DEFENDER_CARGO_PLANE_START", player.UserIDString), player);
            
            Interface.CallHook("OnSendedCargo", cargoPlane, sendPosition, keyPreset, keyPosition);
            SendDiscord(TypeActionDiscord.CargoSpawned);
            
            activeCargoPlane.Add(cargoPlane, new CargoPlaneRepository()
            {
                presetKey = keyPreset,
                dropPosition = sendPosition,
            });
            
            if (isCommand)
            {
                String keyPositionVisual = customPos != default ? (LanguageEn ? $"to the {customPos} position" : $"на позицию {customPos}") : playerTarget != null ? (LanguageEn ? $"player {playerTarget.displayName}" : $"игрок {playerTarget.displayName}") : String.IsNullOrWhiteSpace(keyPosition) ? (LanguageEn ? "random" : "случайная позиция") : keyPosition;
                if(ownerAction == null)
                    Puts(LanguageEn ? $"You have successfully sent the plane to drop the cargo. Key : {keyPreset}, position key : {keyPositionVisual}" : $"Вы успешно отправили самолет для сброса груза. Ключ : {keyPreset}, ключ позиции : {keyPositionVisual}");
                else SendChat(LanguageEn ? $"<color=#738D45>Success!</color>\nYou have successfully sent the cargo plane to the position named <color=#C26D33>{keyPosition}</color>, cargo preset key - <color=#C26D33>{keyPreset}</color>" : $"<color=#738D45>Успешно!</color>\nВы успешно отправили самолет с грузом на позицию с названием <color=#C26D33>{keyPositionVisual}</color>, ключ пресета груза - <color=#C26D33>{keyPreset}</color>", ownerAction);
            }
        }
        
        #endregion
        
        #region Building Armored Drop

        #region Spawn Supply
        private void CheckNightLight(SupplyDrop supplyDrop)
        {
            if (Env.time > 20.0 || Env.time < 7.0)
            {
                supplyDrop.SetFlag(BaseEntity.Flags.Reserved1, false);

                if (alarmDrops.ContainsKey(supplyDrop) && alarmDrops[supplyDrop] != null &&
                    !alarmDrops[supplyDrop].IsDestroyed) return;
                
                SirenLight sirenLight = GameManager.server.CreateEntity(sirenLightPrefab, new Vector3(0f, 2.85f, 0f)) as SirenLight;
                sirenLight.OwnerID = ownerIDElemnts;
                sirenLight.SetParent(supplyDrop);
                sirenLight.pickup.enabled = false;
                sirenLight.Spawn();
                
                sirenLight.SetFlag(BaseEntity.Flags.Reserved8, true);
                sirenLight.SetFlag(BaseEntity.Flags.On, true);
                
                alarmDrops.Add(supplyDrop, sirenLight);
                levelsDropActive[supplyDrop].entitiesList.Add(sirenLight);
            }
            else
            {
                if (!alarmDrops.ContainsKey(supplyDrop)) return;
                if(alarmDrops[supplyDrop] == null || alarmDrops[supplyDrop].IsDestroyed) return;
                
                levelsDropActive[supplyDrop].entitiesList.Remove(alarmDrops[supplyDrop]);
                alarmDrops[supplyDrop].Kill();

                alarmDrops.Remove(supplyDrop);
            }
        }

        private SupplyDrop SetupSupplyDrop(String presetKey, Vector3 position, TypeLevels typeLevels, Boolean isLockedDrop, Int32 cardLevel)
        {
            Configuration.PresetSupply presetSupply = config.presetSupplyDrops[presetKey];
            SupplyDrop supplyDrop = GameManager.server.CreateEntity(supplyDropPrefab, position) as SupplyDrop;
            supplyDrop.OwnerID = ownerIDElemnts;
            supplyDrop.skinID = skinIDElement;
            supplyDrop.Spawn();
            
            if(config.generalSetting.alertSetting.useAlertDropped)
            {
                String cordDropOnMap = MapHelper.PositionToString(position);
                foreach (BasePlayer player in BasePlayer.activePlayerList)
                {
                    String messagePlayer = GetLang("ALERT_CHAT_DEFENDER_CARGO_DROPPED", player.UserIDString, cordDropOnMap);
                    if (isLockedDrop && cardLevel != 0)
                    {
                        String cardLevelTitle = cardLevel switch
                        {
                            1 => "CARD_ALERT_GREEN",
                            2 => "CARD_ALERT_BLUE",
                            3 => "CARD_ALERT_RED",
                            _ => String.Empty
                        };

                        messagePlayer += GetLang("ALERT_CHAT_DEFENDER_CARGO_DROPPED_ADDITIONAL_CARD_INFO", player.UserIDString, GetLang(cardLevelTitle, player.UserIDString));
                    }
                    SendChat(messagePlayer, player);
                }
            }
                          
            CreateMapMarker(supplyDrop, presetKey);
            
            if (typeLevels != TypeLevels.Level0)
            {
                NextTick(() =>
                {
                    if (!supplyDrop) return;
                    supplyDrop.CancelInvoke(nameof(SupplyDrop.CheckNightLight));
                    supplyDrop.InvokeRepeating(() => { CheckNightLight(supplyDrop); }, 0.0f, 30f);
                });
            }

            SetLockedStatusDrop(supplyDrop, isLockedDrop);
            
            if (presetSupply.lootSetting.useCustomLoot && presetSupply.lootSetting.lootList != null && presetSupply.lootSetting.lootList.Count != 0)
            {
                List<Configuration.PresetSupply.PresetsLoot.ResultLoot> droppedLootList = presetSupply.lootSetting.GetLootList();

                if (droppedLootList != null)
                {
                    supplyDrop.inventory.Clear();

                    if (droppedLootList.Count == 0)
                        droppedLootList.Add(new Configuration.PresetSupply.PresetsLoot.ResultLoot
                        {
                            shortname = presetSupply.lootSetting.lootList[0].shortname,
                            skinID = presetSupply.lootSetting.lootList[0].skinID,
                            amount = presetSupply.lootSetting.lootList[0].amountController.GetAmount()
                        });

                    Int32 maxDropLoot = presetSupply.lootSetting.maxCountLoot > 18 ? 18 : presetSupply.lootSetting.maxCountLoot;
                    for (Int32 i = 0; i < maxDropLoot && i < droppedLootList.Count; i++)
                    {
                        Int32 randomIndex = Oxide.Core.Random.Range(i, droppedLootList.Count);
                        (droppedLootList[randomIndex], droppedLootList[i]) = (droppedLootList[i], droppedLootList[randomIndex]);

                        Configuration.PresetSupply.PresetsLoot.ResultLoot resultLoot = droppedLootList[i];
                        Item itemDropped = ItemManager.CreateByName(resultLoot.shortname, resultLoot.amount, resultLoot.skinID);

                        if (!itemDropped.MoveToContainer(supplyDrop.inventory))
                            itemDropped.Remove();
                    }
                }
            }
            
            supplyDrop.SetFlag(BaseEntity.Flags.Reserved2, false);
            
            Interface.CallHook("OnSpawnedSupply", supplyDrop, position, presetKey, isLockedDrop);
            SendDiscord(TypeActionDiscord.SupplyDropped);
            
            levelsDropActive.Add(supplyDrop, new ActiveDropRepository()
            {
                entitiesList = new List<BaseEntity>(),
                levelSupply = presetSupply.generalSetting.defenderLevel,
            });

            LangingController langingEffect = supplyDrop.GetOrAddComponent<LangingController>();
            langingEffect.InitializeDrop(supplyDrop, typeLevels, presetKey);
            
            if (supplyDrop.TryGetComponent(out Rigidbody rb))
                rb.mass = 10000;
            
            SendPatrolDrone(supplyDrop, presetKey);
            
            supplyDrop.Invoke(() => DestroySupply(supplyDrop), config.generalSetting.timeRevomeDropNoLoot); 
            supplyDrop.Invoke(() =>
            {
                supplyDrop.limitNetworking = true;
                supplyDrop.limitNetworking = false;
                supplyDrop.SendNetworkUpdate();
            }, 0.5f);
            
            return supplyDrop;
        }

        #endregion
        
        #region Level0

        // [ChatCommand("valid.short")]
        // private void ValidShorts(BasePlayer player)
        // {
        //     foreach (var supply in config.presetSupplyDrops)
        //     {
        //         foreach (var lootList in supply.Value.lootSetting.lootList)
        //         {
        //             Item itemCheck = ItemManager.CreateByName(lootList.shortname, 1, 0);
        //             if (itemCheck == null)
        //             {
        //                 PrintError($"Error : {supply.Key} : {lootList.shortname}");
        //                 return;
        //             }
        //
        //             itemCheck.Remove();
        //         }
        //     }
        // }
        
        private void SpawnedLevel0(String presetKey, Vector3 position)
        {
            if (!config.presetSupplyDrops.TryGetValue(presetKey, out Configuration.PresetSupply presetSupply))
            {
                PrintWarning(LanguageEn ? $"The specified preset - {presetKey} was not found in the configuration, dropping cargo is not possible" : $"Указанный пресет - {presetKey} не найден в конфигурации, вызов дропа невозможен");
                return;
            }

            const TypeLevels level = TypeLevels.Level0;
            SupplyDrop supplyLevel0 = SetupSupplyDrop(presetKey, position, level, false, 0);

            #region Armored

            PositionEntity shutterMetallPrefabSetting = positionCashed[level][shutterMetalEmbrasurePrefab];
            foreach (PositionEntity.EntityPos positionShutterMetall in shutterMetallPrefabSetting.entityPosList)
            {
                SphereEntity sphereArmorSize = SetupSphereEntity(supplyLevel0, shutterMetallPrefabSetting.spherePos.sizeSphere, shutterMetallPrefabSetting.spherePos.position, default);

                BaseEntity shutterMetalEmbrasure = GameManager.server.CreateEntity(shutterMetalEmbrasurePrefab, positionShutterMetall.localPosition, positionShutterMetall.localQuaternion);
                shutterMetalEmbrasure.OwnerID = ownerIDElemnts;
                
                shutterMetalEmbrasure.SetParent(sphereArmorSize);
                shutterMetalEmbrasure.Spawn();

                FixedComponents(shutterMetalEmbrasure);
                    
                levelsDropActive[supplyLevel0].entitiesList.Add(shutterMetalEmbrasure);
            }
            
            #endregion

            #region Turret

            if (presetSupply.turretSetting.useTurrets)
            {
                PositionEntity turretSetting = positionCashed[level][turretPrefab];

                SphereEntity sphereTurretSize = SetupSphereEntity(supplyLevel0, turretSetting.spherePos.sizeSphere, turretSetting.spherePos.position, default);
                AutoTurret turret = SetupTurret(supplyLevel0, presetSupply.turretSetting, sphereTurretSize, turretSetting.entityPosList[0].localPosition);

                levelsDropActive[supplyLevel0].entitiesList.Add(turret);
            }

            #endregion
        }

        #endregion
        
        #region Level1
        
        private void SpawnedLevel1(String presetKey, Vector3 position)
        {
            if (!config.presetSupplyDrops.TryGetValue(presetKey, out Configuration.PresetSupply presetSupply))
            {
                PrintWarning(LanguageEn ? $"The specified preset - {presetKey} was not found in the configuration, dropping cargo is not possible" : $"Указанный пресет - {presetKey} не найден в конфигурации, вызов дропа невозможен");
                return;
            }

            const TypeLevels level = TypeLevels.Level1;

            Boolean isLockedDoor = presetSupply.generalSetting.cardReaderSetting.useCardLevel;
            Int32 cardLevel = presetSupply.generalSetting.cardReaderSetting.GetCardLevel();
            
            SupplyDrop supplyLevel1 = SetupSupplyDrop(presetKey, position, level, isLockedDoor, cardLevel);

            #region Armored

            #region Wall
            
            PositionEntity prisonWall = positionCashed[level][prisonWallPrefab];
            Int32 countLight = 0;
            foreach (PositionEntity.EntityPos positionPrisonWall in prisonWall.entityPosList)
            {
                BaseEntity prisonWallEntity = SetupWall(supplyLevel1, positionPrisonWall);
                
                #region Light

                if (countLight == 0)
                {
                    SetupSimpleLight(supplyLevel1, prisonWallEntity, level, 0);
                    countLight++;
                }

                #endregion
            }
            
            #endregion

            #region Floor
            
            PositionEntity prisonFloor = positionCashed[level][prisonFloorPrefab];
            foreach (PositionEntity.EntityPos positionPrisonFloor in prisonFloor.entityPosList)
                SetupFloor(supplyLevel1, positionPrisonFloor);
            
            #endregion

            #region Door
            
            BaseEntity prisonDoorEntity = SetupDoor(supplyLevel1, level, isLockedDoor);
            
            if (countLight == 1)
            {
                SetupSimpleLight(supplyLevel1, prisonDoorEntity, level, 1);
                countLight++;
            }
            
            if (isLockedDoor)
                SetupCardReader(supplyLevel1, prisonDoorEntity, level, cardLevel);
            
            #endregion

            #endregion
        }

        #endregion
        
        #region Level2
        
        private void SpawnedLevel2(String presetKey, Vector3 position)
        {
            if (!config.presetSupplyDrops.TryGetValue(presetKey, out Configuration.PresetSupply presetSupply))
            {
                PrintWarning(LanguageEn ? $"The specified preset - {presetKey} was not found in the configuration, dropping cargo is not possible" : $"Указанный пресет - {presetKey} не найден в конфигурации, вызов дропа невозможен");
                return;
            }

            const TypeLevels level = TypeLevels.Level2;
            Boolean isLockedDoor = presetSupply.generalSetting.cardReaderSetting.useCardLevel;
            Int32 cardLevel = presetSupply.generalSetting.cardReaderSetting.GetCardLevel();
            
            SupplyDrop supplyLevel2 = SetupSupplyDrop(presetKey, position, level, isLockedDoor, cardLevel);

            #region Armored

            #region Wall
            
            PositionEntity prisonWall = positionCashed[level][prisonWallPrefab];
            foreach (PositionEntity.EntityPos positionPrisonWall in prisonWall.entityPosList)
                SetupWall(supplyLevel2, positionPrisonWall);
            
            #endregion

            #region Floor
            
            PositionEntity prisonFloor = positionCashed[level][prisonFloorPrefab];
            Int32 floorCount = 0;
            foreach (PositionEntity.EntityPos positionPrisonFloor in prisonFloor.entityPosList)
            {
                BaseEntity floor = SetupFloor(supplyLevel2, positionPrisonFloor);

                if (floorCount == 0)
                {
                    Int32 windowWallCount = 0;
                    PositionEntity windowWall = positionCashed[level][windowWallPrefab];
                    foreach (PositionEntity.EntityPos positionWindowWall in windowWall.entityPosList)
                    {
                        BaseEntity windowWallEntity = SetupWindowWall(supplyLevel2, floor, positionWindowWall);
                        
                        for (Int32 i = 0; i < 2; i++)
                        {
                            Int32 indexWindow = (windowWallCount * 2) + i;
                            SetupSimpleLight(supplyLevel2, windowWallEntity, level, indexWindow);
                        }

                        if (presetSupply.turretSetting.useTurrets)
                        {
                            PositionEntity turretSetting = positionCashed[level][turretPrefab];
                            SphereEntity sphereTurretSize = SetupSphereEntity(windowWallEntity, turretSetting.spherePos.sizeSphere, turretSetting.spherePos.position, default);
                            AutoTurret turret = SetupTurret(supplyLevel2, presetSupply.turretSetting, sphereTurretSize, turretSetting.entityPosList[windowWallCount].localPosition, turretSetting.entityPosList[windowWallCount].localQuaternion);

                            levelsDropActive[supplyLevel2].entitiesList.Add(turret);
                        }

                        windowWallCount++;
                    }
                }
                
                floorCount++;
            }
            
            #endregion

            #region Door
            
            BaseEntity prisonDoorEntity = SetupDoor(supplyLevel2, level, isLockedDoor);
            
            if (isLockedDoor)
                SetupCardReader(supplyLevel2, prisonDoorEntity, level, cardLevel);

            #endregion

            #endregion
        }

        #endregion
        
        #region Building Template
        
        #region Simple Light

        private void SetupSimpleLight(SupplyDrop supplyDrop, BaseEntity parentEntity, TypeLevels level, Int32 indexWall)
        {
            PositionEntity simpleLight = positionCashed[level][simpleLightPrefab];
            SimpleLight simpleLightEntity = GameManager.server.CreateEntity(simpleLightPrefab, simpleLight.entityPosList[indexWall].localPosition, simpleLight.entityPosList[indexWall].localQuaternion) as SimpleLight;

            simpleLightEntity.OwnerID = ownerIDElemnts;
            simpleLightEntity.SetParent(parentEntity);
            simpleLightEntity.pickup.enabled = false;
            simpleLightEntity.Spawn();

            FixedComponents(simpleLightEntity);
                    
            simpleLightEntity.SetFlag(BaseEntity.Flags.On, true);

            levelsDropActive[supplyDrop].entitiesList.Add(simpleLightEntity);
        }

        #endregion

        #region Window

        private BaseEntity SetupWindowWall(SupplyDrop supplyDrop, BaseEntity parentEntity, PositionEntity.EntityPos positionWindow)
        {
            SimpleBuildingBlock windowWall = GameManager.server.CreateEntity(windowWallPrefab, positionWindow.localPosition, positionWindow.localQuaternion) as SimpleBuildingBlock;
            windowWall.OwnerID = ownerIDElemnts;
            windowWall.SetParent(parentEntity);
            windowWall.pickup.enabled = false;
            windowWall.Spawn();
        
            FixedComponents(windowWall);
        
            levelsDropActive[supplyDrop].entitiesList.Add(windowWall);
            
            return windowWall;
        }

        #endregion
        
        #region Floor

        private BaseEntity SetupFloor(SupplyDrop supplyDrop, PositionEntity.EntityPos positionPrisonFloor)
        {
            BaseEntity prisonWallEntity = GameManager.server.CreateEntity(prisonFloorPrefab, positionPrisonFloor.localPosition, positionPrisonFloor.localQuaternion);
            prisonWallEntity.OwnerID = ownerIDElemnts;
            prisonWallEntity.SetParent(supplyDrop);
            prisonWallEntity.Spawn();

            FixedComponents(prisonWallEntity);

            levelsDropActive[supplyDrop].entitiesList.Add(prisonWallEntity);

            return prisonWallEntity;
        }

        #endregion
        
        #region Wall

        private BaseEntity SetupWall(SupplyDrop supplyDrop, PositionEntity.EntityPos positionPrisonWall)
        {
            BaseEntity prisonWallEntity = GameManager.server.CreateEntity(prisonWallPrefab, positionPrisonWall.localPosition, positionPrisonWall.localQuaternion);
            prisonWallEntity.OwnerID = ownerIDElemnts;
            prisonWallEntity.SetParent(supplyDrop);
            prisonWallEntity.Spawn();

            FixedComponents(prisonWallEntity);

            levelsDropActive[supplyDrop].entitiesList.Add(prisonWallEntity);

            return prisonWallEntity;
        }

        #endregion
        
        #region Door

        private Door SetupDoor(SupplyDrop supplyDrop, TypeLevels level, Boolean isLockedDoor)
        {
            PositionEntity prisonDoor = positionCashed[level][prisonDoorPrefab];
            Door prisonDoorEntity = GameManager.server.CreateEntity(prisonDoorPrefab, prisonDoor.entityPosList[0].localPosition, prisonDoor.entityPosList[0].localQuaternion) as Door;

            prisonDoorEntity.OwnerID = ownerIDElemnts;
            prisonDoorEntity.SetParent(supplyDrop);
            prisonDoorEntity.pickup.enabled = false;
            prisonDoorEntity.Spawn();

            if (isLockedDoor)
                prisonDoorEntity.SetFlag(BaseEntity.Flags.Busy, true);

            FixedComponents(prisonDoorEntity);

            levelsDropActive[supplyDrop].entitiesList.Add(prisonDoorEntity);

            return prisonDoorEntity;
        }

        #endregion
        
        #region Card Reader

        private void SetupCardReader(SupplyDrop supplyDrop, BaseEntity prisonDoorEntity, TypeLevels level, Int32 cardLevel)
        {
            PositionEntity cardReader = positionCashed[level][cardReaderPrefab];
            BaseEntity cardReaderEntity = GameManager.server.CreateEntity(cardReaderPrefab, cardReader.entityPosList[0].localPosition, cardReader.entityPosList[0].localQuaternion);
            cardReaderEntity.gameObject.SetActive(true);
            CardReader reader = cardReaderEntity as CardReader;
            reader.accessLevel = cardLevel;
            cardReaderEntity.OwnerID = ownerIDElemnts;
            cardReaderEntity.SetParent(prisonDoorEntity, "gate");
            cardReaderEntity.Spawn();
            cardReaderEntity.SetFlag(BaseEntity.Flags.Reserved8, true, false, true);
           
            cardReaderEntity.SendNetworkUpdate();
            cardReaderEntity.SendNetworkUpdateImmediate();
            
            FixedComponents(cardReaderEntity);

            levelsDropActive[supplyDrop].entitiesList.Add(cardReaderEntity);

            if(!readerManipulator.ContainsKey(reader))
            {
                readerManipulator.Add(reader, new List<BaseEntity>());
                readerManipulator[reader].Add(prisonDoorEntity);
                readerManipulator[reader].Add(supplyDrop);
            }
        }

        #endregion
        
        #region Turrets

        private AutoTurret SetupTurret(SupplyDrop supplyDrop, Configuration.PresetSupply.TurretSettings presetTurret, SphereEntity sphereEntity, Vector3 localPosition, Quaternion localQuaternion = default)
        {
            AutoTurret turret = GameManager.server.CreateEntity(turretPrefab, localPosition, localQuaternion) as AutoTurret;
            turret.baseProtection = null; 
            turret.SetParent(sphereEntity, 0);
            turret.EnableGlobalBroadcast(true);
            turret.pickup.enabled = false;

            turret.SetPeacekeepermode(presetTurret.passiveMode);
            
            turret.dropsLoot = presetTurret.dropLootTurret;
            turret.aimCone = presetTurret.accuracy;
            
            turret.Spawn();
            
            if(!turretsDefenderDrops.ContainsKey(supplyDrop))
                turretsDefenderDrops.Add(supplyDrop, new List<AutoTurret>());
            
            turretsDefenderDrops[supplyDrop].Add(turret);
                
            turret.SetFlag(IOEntity.Flag_HasPower, true);
            turret.OwnerID = ownerIDElemnts;
            turret.skinID = skinIDElement;

            Single rangeRadius = presetTurret.visRadius;
            turret.sightRange = rangeRadius;
            turret.targetTrigger.GetComponent<SphereCollider>().radius = rangeRadius;

            turret._health = presetTurret.healthTurret;
            turret._maxHealth = presetTurret.healthTurret;
            turret.startHealth = presetTurret.healthTurret;
            
            FixedComponents(turret);
            HideInputsAndOutputs(turret);
            
            Item weapon = presetTurret.weaponTurretSetting.GetWeaponTurret();

            if (!weapon.MoveToContainer(turret.inventory, 0))
                weapon.Remove();
            else
            {
                turret.CancelInvoke(turret.UpdateAttachedWeapon);
                turret.UpdateAttachedWeapon();
                turret.Invoke(() =>
                {
                    AddReserveAmmo(turret, presetTurret);
                    turret.UpdateTotalAmmo();
                    turret.Reload();
                }, 3f);
            }            
          
            turret.SetFlag(BaseEntity.Flags.Locked, true);
            turret.SetFlag(BaseEntity.Flags.Busy, true);
            turret.SendNetworkUpdate();
            turret.SetIsOnline(true);

            return turret;
        }
        
        private void AddReserveAmmo(AutoTurret turret, Configuration.PresetSupply.TurretSettings presetTurret)
        {
            Int32 slot = 1;
            Int32 maxSlot = turret.inventory.capacity - 1;
            
            foreach (Configuration.PresetSupply.TurretSettings.WeaponSetting.AmmoSetting ammoSetting in presetTurret.weaponTurretSetting.ammoTurret)
            {
                if (slot > maxSlot)
                    break;
                if (ammoSetting.amount < 1) continue;
                
                Item itemAmmo = ItemManager.CreateByName(ammoSetting.shortname, ammoSetting.amount);

                if (!itemAmmo.MoveToContainer(turret.inventory, slot))
                    itemAmmo.Remove();
                
                itemAmmo.MarkDirty();
                slot++;
            }
        }
        
        private void HideInputsAndOutputs(IOEntity ioEntity)
        {
            foreach (IOEntity.IOSlot input in ioEntity.inputs)
                input.type = IOEntity.IOType.Generic;

            foreach (IOEntity.IOSlot output in ioEntity.outputs)
                output.type = IOEntity.IOType.Generic;
        }
        
        #endregion
        
        #endregion
        
        #region Other
        private void SetLockedStatusDrop(SupplyDrop drop, Boolean isLocked) => drop.SetFlag(BaseEntity.Flags.Busy, isLocked);
        private Boolean IsRare(Int32 rarePercent) => Oxide.Core.Random.Range(0, 100) < rarePercent;

        private SphereEntity SetupSphereEntity(BaseEntity entity, Single size, Vector3 localPosition, Quaternion localRotation)
        {
            SphereEntity sparentSphere = GameManager.server.CreateEntity(sphereVisualPrefab, localPosition, localRotation) as SphereEntity;
            sparentSphere.currentRadius = size;
            sparentSphere.lerpRadius = size;
            sparentSphere.transform.localScale = new Vector3(size, size, size);
            
            sparentSphere.EnableSaving(entity.enableSaving);
            sparentSphere.EnableGlobalBroadcast(true);
            
            sparentSphere.SetParent(entity);
            sparentSphere.Spawn();

            entity.SetSlot(BaseEntity.Slot.UpperModifier, sparentSphere);

            if (entity as SupplyDrop)
            {
                SupplyDrop supplyDrop = entity as SupplyDrop;
                if (supplyDrop != null) 
                    levelsDropActive[supplyDrop].entitiesList.Add(sparentSphere);
            }

            return sparentSphere;
        }
        
        private void FixedComponents(BaseEntity entity)
        {
            if (entity as AutoTurret)
                (entity as AutoTurret).targetTrigger.GetOrAddComponent<Rigidbody>().isKinematic = true; 
            
            List<Collider> colliders = Pool.Get<List<Collider>>();
            entity.GetComponentsInChildren(colliders);
            
            foreach (Collider collider in colliders)
                if (!collider.isTrigger)
                    UnityEngine.Object.DestroyImmediate(collider);
            
            Pool.FreeUnmanaged(ref colliders);
            
            if (entity.TryGetComponent(out DestroyOnGroundMissing groundMissing))
                UnityEngine.Object.DestroyImmediate(groundMissing);

            if (entity.TryGetComponent(out GroundWatch groundWatch))
                UnityEngine.Object.DestroyImmediate(groundWatch);
        }
        
        #endregion

        #region Destroyed
        private Boolean unSubEntityKill = false;
        private void UnsubProSubEntityKill(Int32 Time = 1)
        {
            unSubEntityKill = true;
            timer.Once(Time, () => { unSubEntityKill = false; });
        }
        private void DetectSupplyOlds()
        {
            foreach (BaseNetworkable supplyDrop in BaseNetworkable.serverEntities.entityList.Get().Values.Where(x => x != null && x is SupplyDrop && !x.IsDestroyed && (x as SupplyDrop).OwnerID == ownerIDElemnts))
                DestroySupply(supplyDrop as SupplyDrop, true);
        }
        private void DestroyAllDrop()
        {
            List<SupplyDrop> keysSupply = Pool.Get<List<SupplyDrop>>();
            keysSupply = new List<SupplyDrop>(levelsDropActive.Keys);

            for (Int32 i = 0; i < keysSupply.Count; i++)
                DestroySupply(keysSupply[i], true);
            
            Pool.FreeUnmanaged(ref keysSupply);

            if (langingComponents != null && langingComponents.Count != 0)
            {
                for (Int32 i = 0; i < langingComponents.Count; i++)
                {
                    LangingController langing = langingComponents[i];
                    if (langing != null)
                        UnityEngine.Object.DestroyImmediate(langing);
                }
            }

            if (activeCargoPlane != null && activeCargoPlane.Count != 0)
            {
                foreach (CargoPlane activeCargo in activeCargoPlane.Keys)
                {
                    if(activeCargo != null && !activeCargo.IsDestroyed)
                        activeCargo.Kill();
                }
            }

            alarmDrops?.Clear();
            activeCargoPlane?.Clear();
            listDestroyedSupplyInvoke?.Clear();
            npcDefendersDrops?.Clear();
            turretsDefenderDrops?.Clear();
            droneActivePosition?.Clear();
            readerManipulator?.Clear();
            langingComponents?.Clear();
            levelsDropActive?.Clear();
        }

        private void DestroySupply(SupplyDrop drop, Boolean isUnload = false)
        {
            if(isUnload)
                Unsubscribe(nameof(OnEntityKill));
            
            UInt64 dropNetID = drop.net.ID.Value;
            
            UnsubProSubEntityKill();
            
            listDestroyedSupplyInvoke.Remove(drop);
            if (levelsDropActive.TryGetValue(drop, out ActiveDropRepository additionalEntity))
            {
                foreach (BaseEntity entity in additionalEntity.entitiesList)
                {
                    if (entity != null && !entity.IsDestroyed)
                        entity.Kill();
                }

                levelsDropActive.Remove(drop);
            }
            
            Interface.CallHook("OnDestroySupply", drop);

            RemoveMarker(drop);
            CancellPatrolDrone(drop);
            
            if (npcDefendersDrops.TryGetValue(drop, out Dictionary<ScientistNPC, String> npcInfo))
            {
                foreach (ScientistNPC scientistNpc in npcInfo.Keys)
                {
                    if (scientistNpc != null && !scientistNpc.IsDestroyed)
                        scientistNpc.Kill();
                }

                npcDefendersDrops[drop].Clear();
                npcDefendersDrops.Remove(drop);
            }
            
            if (turretsDefenderDrops.ContainsKey(drop))
            {
                turretsDefenderDrops[drop].Clear();
                turretsDefenderDrops.Remove(drop);
            }
            
            _?.PveModeRemoveZone(dropNetID);
            
            Interface.CallHook("OnDestroyedSupply", drop);
            
            if (drop != null && !drop.IsDestroyed)
                drop.Kill();
            
            if (!isUnload)
                SendDiscord(TypeActionDiscord.SupplyDestroyed);
        }

        #endregion
        
        #endregion

        #region Markers

        private void MarkerUpdate()
        {
            foreach (MarkerRepository markerList in mapMarkers.Values)
            {
                if(!markerList.vending.IsDestroyed)
                    markerList.vending.SendNetworkUpdate();
                    
                if(!markerList.marker.IsDestroyed)
                    markerList.marker.SendNetworkUpdate();
            }
        }
        
        private void RemoveMarker(SupplyDrop drop)
        {
            if (!mapMarkers.TryGetValue(drop, out MarkerRepository markerRepository)) return;
            
            if(!markerRepository.vending.IsDestroyed)
                markerRepository.vending.Kill();
                    
            if(!markerRepository.marker.IsDestroyed)
                markerRepository.marker.Kill();
        }
        
        private void CreateMapMarker(SupplyDrop drop, String presetKey)
        {
            if (!config.presetSupplyDrops.TryGetValue(presetKey, out Configuration.PresetSupply supplyDrop)) return;
            if (!supplyDrop.generalSetting.mapMarkerSetting.useMapMarker) return;
            Configuration.PresetSupply.GeneralDrop.MapMarkerPreset markerPreset = supplyDrop.generalSetting.mapMarkerSetting;
            if (mapMarkers.ContainsKey(drop)) return;
            
            VendingMachineMapMarker vending = GameManager.server.CreateEntity(invisibleVendingPrefab, drop.transform.position, Quaternion.identity, true) as VendingMachineMapMarker;
            vending.markerShopName = namingMappingMarker[supplyDrop.generalSetting.defenderLevel];
            vending.enableSaving = false;
            vending.EnableGlobalBroadcast(true);
            vending.Spawn();
                
            MapMarkerGenericRadius genericMarker = GameManager.server.CreateEntity(markerPrefab, new Vector3(), Quaternion.identity, true) as MapMarkerGenericRadius;
            ColorUtility.TryParseHtmlString(markerPreset.mainColorMarker, out Color color1);
            ColorUtility.TryParseHtmlString(markerPreset.additionalColorMarker, out Color color2);
            genericMarker.color1 = color1;
            genericMarker.color2 = color2;
            //genericMarker.color2 = HexParser(color2);
            genericMarker.radius = markerPreset.radiusMarker;
            genericMarker.alpha = 1f;
            genericMarker.SetParent(vending);
            genericMarker.Spawn();
            genericMarker.SendUpdate(); 
            genericMarker.EnableGlobalBroadcast(true);
            //genericMarker.skinID = 643323265;
        
            mapMarkers.Add(drop, new MarkerRepository
            {
                vending = vending,
                marker = genericMarker
            });
        }

        #endregion
        
        #region Additional Plugins Metods

        #region IQDronePatrol

        private void SendPatrolDrone(SupplyDrop supplyDrop, String presetKey)
        {
            if (!IQDronePatrol) return;
            Configuration.PresetSupply.GeneralDrop.ReferenceAdditional.IQDronePatrol droneSpawnController = config.presetSupplyDrops[presetKey].generalSetting.referenceAdditional.droneSpawnSetting;
            if (!droneSpawnController.useDrones) return;
            if (droneActivePosition.ContainsKey(supplyDrop)) return;
            
            Vector3 positionPatrol = new Vector3(supplyDrop.transform.position.x, supplyDrop.transform.position.y, supplyDrop.transform.position.z);
            String json = JsonConvert.SerializeObject(droneSpawnController.GetPresetPatrol(positionPatrol));

            IQDronePatrol.Call<Dictionary<Drone, AutoTurret>>("SendPatrolPoint",json, false);

            droneActivePosition.Add(supplyDrop, positionPatrol);
        }

        private void CancellPatrolDrone(SupplyDrop supplyDrop)
        {
            if (!IQDronePatrol) return;
            IQDronePatrol.Call("CancellPatrol");

            if (!droneActivePosition.TryGetValue(supplyDrop, out Vector3 positionPatrol)) return;

            IQDronePatrol.Call("CancellPatrol", positionPatrol);

            droneActivePosition.Remove(supplyDrop);
        }

        #endregion

        #region NpcSpawn
        
        private void SendNPC(SupplyDrop supplyDrop, String presetKey)
        {
            if (!NpcSpawn) return;
            
            Configuration.PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn npcSpawnController = config.presetSupplyDrops[presetKey].generalSetting.referenceAdditional.npcSpawnSetting;
            if (!npcSpawnController.useNpcSpawn) return;

            if (!npcDefendersDrops.ContainsKey(supplyDrop))
                npcDefendersDrops.Add(supplyDrop, new Dictionary<ScientistNPC, String>());

            JObject configNpc = npcSpawnController.presetBot.GetJObjectConfigNpc();
            Int32 countNpc = npcSpawnController.countSpawnNpc.GetAmount();
            Vector3 centralPosition = supplyDrop.transform.position;
            
            for (Int32 i = 0; i < countNpc; i++)
            {
                Vector3 spawnNpcPosition = RandomCircle(centralPosition, 20f);

                ScientistNPC npc = (ScientistNPC)NpcSpawn.Call("SpawnNpc", spawnNpcPosition, configNpc);
                npc.OwnerID = ownerIDElemnts;
                
                npcDefendersDrops[supplyDrop].Add(npc, presetKey);
            }
        }

        private void MoveCustomLootNpc(ScientistNPC npc, NPCPlayerCorpse corpse)
        {
            if (corpse == null || npc == null) return;
            if (npc.OwnerID != ownerIDElemnts) return;

            NextTick(() =>
            {
                if (corpse.containers.Length >= 1)
                    corpse.containers[0].Clear();
                
                foreach (KeyValuePair<SupplyDrop, Dictionary<ScientistNPC, String>> npcDefendersDrop in npcDefendersDrops)
                {
                    if (!npcDefendersDrop.Value.TryGetValue(npc, out String keyPreset)) continue;
                    
                    List<Configuration.PresetSupply.PresetsLoot.ResultLoot> lootList = new();
                    lootList.AddRange(config.presetSupplyDrops[keyPreset].generalSetting.referenceAdditional.npcSpawnSetting.presetBot.itemDropLootNPC.GetLootList());
                    Int32 maxCountLoot = config.presetSupplyDrops[keyPreset].generalSetting.referenceAdditional.npcSpawnSetting.presetBot.itemDropLootNPC.maxCountLoot;
                    if (lootList.Count == 0)
                        continue;

                    Int32 itemsToDrop = Math.Min(maxCountLoot == 0 ? Oxide.Core.Random.Range(1, lootList.Count + 1) : maxCountLoot, lootList.Count);
                            
                    for (Int32 i = 0; i < itemsToDrop; i++)
                    {
                        Configuration.PresetSupply.PresetsLoot.ResultLoot resultLoot = lootList[i];
                        Item item = ItemManager.CreateByName(resultLoot.shortname, resultLoot.amount,
                            resultLoot.skinID);

                        if (!item.MoveToContainer(corpse.containers[0]))
                            item.Remove();
                    }
                }
            });
        }


        #endregion

        #endregion

        #region Helped

        private void PreLoadPlugin()
        {
            if (NpcSpawn && NpcSpawn.Version < new VersionNumber(2, 7, 1))
            {
                NextTick(() =>
                {
                    PrintError(LanguageEn
                        ? "You have an outdated version of NpcSpawn installed. Please update to version 2.7.1 or higher"
                        : "У вас установлена устаревшая версия NpcSpawn, обновитесь до версии выше 2.7.1");
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            
            if (IQTurret && IQTurret.Version < new VersionNumber(1, 11, 22))
            {
                NextTick(() =>
                {
                    PrintError(LanguageEn
                        ? "You have an outdated version of IQTurret installed. Please update to version 1.11.22 or higher"
                        : "У вас установлена устаревшая версия IQTurret, обновитесь до версии выше 1.11.22");
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            
            if (IQDronePatrol && IQDronePatrol.Version < new VersionNumber(1,10,6)) 
            {
                NextTick(() =>
                {
                    PrintError(LanguageEn
                        ? "You have an outdated version of IQDronePatrol installed. Please update to version 1.10.6 or higher"
                        : "У вас установлена устаревшая версия IQDronePatrol, обновитесь до версии 1.10.6 или выше");
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            
            if (IQGuardianDrone && IQGuardianDrone.Version < new VersionNumber(1, 10, 14)) 
            {
                NextTick(() =>
                {
                    PrintError(LanguageEn
                        ? "You have an outdated version of IQGuardianDrone installed. Please update to version 1.10.14 or higher"
                        : "У вас установлена устаревшая версия IQGuardianDrone, обновитесь до версии 1.10.14 или выше");
                    Interface.Oxide.UnloadPlugin(Name);
                });
            }
            
            DetectSupplyOlds();
        }
        
        private void HookController()
        {
            Boolean isUseCard = false;
            Boolean isUseTurretTarget = false;
            Boolean isUseLootNpc = false;
            Boolean isUseMapMarker = false;
            Boolean isUseCustomLootSupply = false;
    
            foreach (KeyValuePair<String, Configuration.PresetSupply> presetsSupply in config.presetSupplyDrops)
            {
                Configuration.PresetSupply.GeneralDrop generalSetting = presetsSupply.Value.generalSetting;
                Configuration.PresetSupply.GeneralDrop.ReferenceAdditional.NPCSpawn npcSpawnSetting = generalSetting.referenceAdditional.npcSpawnSetting;
                Configuration.PresetSupply.GeneralDrop.ReferenceAdditional.IQDronePatrol droneSpawnSetting = generalSetting.referenceAdditional.droneSpawnSetting;

                if (presetsSupply.Value.lootSetting.useCustomLoot)
                    isUseCustomLootSupply = true;
                
                if (generalSetting.mapMarkerSetting.useMapMarker)
                    isUseMapMarker = true;
                
                if (npcSpawnSetting.useNpcSpawn && npcSpawnSetting.presetBot.itemDropLootNPC.useCustomLoot)
                    isUseLootNpc = true;
        
                if (generalSetting.cardReaderSetting.useCardLevel)
                    isUseCard = true;
        
                if (presetsSupply.Value.turretSetting.useTurrets && (npcSpawnSetting.useNpcSpawn || droneSpawnSetting.useDrones))
                    isUseTurretTarget = true;
        
                if (isUseCard && isUseTurretTarget && isUseLootNpc && isUseMapMarker && isUseCustomLootSupply)
                    break;
            }
    
            if(!isUseMapMarker)
                Unsubscribe(nameof(OnPlayerConnected));
            
            if (!isUseCard)
                Unsubscribe(nameof(OnCardSwipe));

            if (!isUseTurretTarget)
                Unsubscribe(nameof(OnTurretTarget));
    
            if (!isUseLootNpc)
                Unsubscribe(nameof(OnCorpsePopulate));

            if (!isUseCustomLootSupply)
            {
                Unsubscribe(nameof(OnContainerPopulate));
                Unsubscribe(nameof(CanPopulateLoot));
            }

            if (!TruePVE)
            {
                Unsubscribe(nameof(CanEntityBeTargeted));
                Unsubscribe(nameof(CanEntityTakeDamage));
            }
        }
        
        private static Boolean TryConvertToVector3(String positionString, out Vector3 result)
        {
            result = Vector3.zero;

            if (String.IsNullOrWhiteSpace(positionString))
                return false;

            String[] components = Regex.Split(positionString.Trim(), @"[\s,]+");

            if (components.Length != 3)
                return false;

            if (Single.TryParse(components[0], out Single x) &&
                Single.TryParse(components[1], out Single y) &&
                Single.TryParse(components[2], out Single z))
            {
                result = new Vector3(x, y, z);
                return true;
            }

            return false;
        }
        
        private String ExtractSubstring(String input)
        {
            Int32 lastSlashIndex = input.LastIndexOf('/');
            Int32 dotIndex = input.IndexOf('.', lastSlashIndex);

            if (lastSlashIndex == -1 || dotIndex == -1 || dotIndex <= lastSlashIndex)
                return string.Empty;

            return input.Substring(lastSlashIndex + 1, dotIndex - lastSlashIndex - 1);
        }

        private String JoinStringList(List<String> inputList) => String.Join(", ", inputList);
        private List<String> ConvertStringToList(String input)
        {
            String pattern = @"[,\.\;\:\s]+";
        
            return Regex.Split(input, pattern) 
                .Select(item => item.Trim()) 
                .Where(item => !string.IsNullOrEmpty(item)) 
                .ToList();
        }
        
        private Vector3 GetRandomPositionCargo(Int32 tryFindPos = 0)
        {
            if (tryFindPos >= 35)
            {
                PrintWarning(LanguageEn ? "It was not possible to find a position for dumping cargo, there are few monuments on the map, or all positions are already occupied by dropped cargo" : "Не удалось найти позицию для сброса груза, на карте мало монументов или все позиции уже заняты сброшенными грузами");
                return Vector3.zero;
            }
            tryFindPos++;
            Vector3 resultPos = default;
            List<PositionInfo> positions = positionSaved.Values.ToList();
            PositionInfo randomPosInfo = positions.GetRandom();
            if(randomPosInfo == null)
            {
                positions.Clear();
                positions = null;
                return GetRandomPositionCargo(tryFindPos);
            }
            
            if (String.IsNullOrWhiteSpace(randomPosInfo.monumentParent))
                resultPos = randomPosInfo.position;
            else
            {
                MonumentInfo monument = GetMonument(randomPosInfo.monumentParent);
                if (monument == null) return GetRandomPositionCargo(tryFindPos);
                resultPos = GetResultVector(monument, randomPosInfo.position);
            }
            
            positions.Clear();
            positions = null;

            foreach (KeyValuePair<CargoPlane, CargoPlaneRepository> activePlanes in activeCargoPlane)
            {
                if(activePlanes.Value == null) continue;
                if (Vector3.Distance(activePlanes.Value.dropPosition, resultPos) < 3f) 
                    return GetRandomPositionCargo(tryFindPos);
            }

            foreach (SupplyDrop activeDrops in levelsDropActive.Keys)
            {
                if(activeDrops == null) continue;
                if (Vector3.Distance(activeDrops.transform.position, resultPos) < 3f) 
                    return GetRandomPositionCargo(tryFindPos);
            }
            
            return resultPos;
        }
        private Boolean IsPositionValid(Vector3 position)
        {
            Vector3[] directions = { Vector3.forward, Vector3.back, Vector3.left, Vector3.right, Vector3.up};
            foreach (Vector3 dir in directions)
            {
                if (Physics.Raycast(position, dir, checkDirectionsDistance))
                    return false;
            }

            return true;
        }
        
        private void MessageGameTipsError(String langKey)
        {
            foreach (BasePlayer player in BasePlayer.activePlayerList)
                player.SendConsoleCommand("gametip.showtoast", new object[]{ "1", GetLang(langKey, player.UserIDString) });
        }
        
        private Vector3 RandomCircle(Vector3 center, Single radius = 2)
        {
            Vector3 pos;
            pos.x = center.x + UnityEngine.Random.Range(-radius, radius);
            pos.z = center.z + UnityEngine.Random.Range(-radius, radius);
            pos.y = center.y;
            pos.y = GetGroundPosition(pos);
            return pos;
        }

        private static Single GetGroundPosition(Vector3 pos)
        {
            Single y = TerrainMeta.HeightMap.GetHeight(pos);
            if (Physics.Raycast(new Vector3(pos.x, pos.y + 200f, pos.z), Vector3.down, out RaycastHit hit, Mathf.Infinity,
                    LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed")) &&
                !hit.collider.name.Contains("rock_cliff")) return Mathf.Max(hit.point.y, y);
            return y;
        }
        #endregion

        #region Discord

        private void SendDiscord(TypeActionDiscord typeAlertDiscord)
        {
            if (!config.generalSetting.typeAlertDiscord.ContainsKey(typeAlertDiscord)) return;
            DiscordMessage message = config.generalSetting.typeAlertDiscord[typeAlertDiscord].GetMessageDiscord();
            if (message == null) return;
            String webhook = config.generalSetting.typeAlertDiscord[typeAlertDiscord].webHooks;
            
            SendDiscordMessage(webhook, message, code => {
                if (code != 200 && code != 204)
                    PrintWarning($"Failed to send discord message. Response code: {code}");
            });
        }
        
        #region FancyDiscord
        public class EmbedFooter
        {
            [JsonProperty("text")]
            public String Text { get; set; }
        }

        public class EmbedAuthor
        {
            [JsonProperty("name")]
            public String Name { get; set; }

            [JsonProperty("icon_url")]
            public String IconUrl { get; set; }
        }

        public class EmbedThumbnail
        {
            [JsonProperty("url")]
            public String Url { get; set; }
        }

        public class Embed
        {
            [JsonProperty("title")]
            public String Title { get; set; }

            [JsonProperty("description")]
            public String Description { get; set; }

            [JsonProperty("color")]
            public Int32 Color { get; set; }

            [JsonProperty("footer")]
            public EmbedFooter Footer { get; set; }

            [JsonProperty("author")]
            public EmbedAuthor Author { get; set; }

            [JsonProperty("fields")]
            public List<Object> Fields { get; set; } = new List<Object>();

            [JsonProperty("thumbnail")]
            public EmbedThumbnail Thumbnail { get; set; }
        }

        public class DiscordMessage
        {
            [JsonProperty("embeds")]
            public List<Embed> Embeds { get; set; } = new List<Embed>();

            [JsonProperty("content")]
            public String Content { get; set; }
        }

        private void SendDiscordMessage(String url, DiscordMessage message, Action<Int32> callback = null)
        {
            String payload = JsonConvert.SerializeObject(message);
            Dictionary<String, String> header = new Dictionary<String, String>
            {
                { "Content-Type", "application/json" }
            };

            try
            {
                webrequest.Enqueue(url, payload, (code, response) =>
                {
                    if (code != 200 && code != 204)
                    {
                        if (response != null)
                        {
                            try
                            {
                                JObject json = JObject.Parse(response);
                                if (code != 429)
                                    PrintWarning(
                                        $"Discord rejected that payload! Responded with \"{json["message"].ToString()}\" Code: {code}");
                            }
                            catch
                            {
                                PrintWarning(
                                    $"Failed to get a valid response from discord! Error: \"{response}\" Code: {code}");
                            }
                        }
                        else
                        {
                            PrintWarning($"Discord didn't respond (down?) Code: {code}");
                        }
                    }

                    try
                    {
                        callback?.Invoke(code);
                    }
                    catch (Exception ex)
                    {
                    }
                }, this, RequestMethod.POST, header, timeout: 5f);
            }
            catch (Exception e)
            {
                if (_ == null)
                    return;
                
                throw;
            }
        }
        
        #endregion

        #endregion
        
        #endregion

        #region Hooks

        #region Hooks Other Plugins
         
        object CanEntityTakeDamage(BasePlayer player, HitInfo hitinfo) // TruePVE
        {
            AutoTurret turret = hitinfo.Initiator as AutoTurret;
            if (turret == null) return null;
            if (IsValidTurret(turret.OwnerID))
                return true;
       
            return null;
        }
        
        object CanEntityTakeDamage(AutoTurret turret, HitInfo hitinfo) // TruePVE
        {
            if (IsValidTurret(turret.OwnerID)) 
                return true;

            return null;
        }
        
        object CanEntityBeTargeted(BasePlayer player, AutoTurret turret) // TruePVE
        {
            if (IsValidTurret(turret.OwnerID))
                return true;

            return null;
        }
        
        private Object OnContainerPopulate(SupplyDrop container) // Loot Table & Stacksize GUI
        {
            if (container.OwnerID == ownerIDElemnts) return false;
            return null;
        }
        
        private Object CanPopulateLoot(SupplyDrop container) // Alpha Loot
        {
            if (container.OwnerID == ownerIDElemnts) return false;
            return null;
        }

        #endregion
        
        private void Init()
        {
            _ = this;
            ReadData();
        }

        private void OnServerInitialized()
        {
            PreLoadPlugin();
            ValidateData();
            ParseMonuments();
            HookController();
            StartAutoEvent();
        }
        private void OnServerShutdown() => DestroyAllDrop();

        private void Unload()
        {
            if (_ == null) return;
            
            WriteData();
            DestroyAllDrop();

            if(routineGeneratedPosition != null)
            {
                ServerMgr.Instance.StopCoroutine(routineGeneratedPosition);
                routineGeneratedPosition = null;
            }
            
            _ = null;
        }

        private void OnNewSave(String filename)
        {
            if (config.generalSetting.clearCustomPositionsWipe)
            {
                Int32 countRemovePos = 0;
                foreach (KeyValuePair<String,PositionInfo> positionInfo in positionSaved)
                {
                    if(String.IsNullOrWhiteSpace(positionInfo.Value.monumentParent))
                    {
                        NextTick(() => { positionSaved.Remove(positionInfo.Key); });
                        countRemovePos++;
                    }
                }
                
                if(countRemovePos != 0)
                    Puts(LanguageEn ? "Cleared {countRemovePos} old custom positions" : $"Очищено {countRemovePos} старых кастомных позиций");
            }
            
            ParseMonuments(true);
        }

        private void OnPlayerConnected(BasePlayer player) => MarkerUpdate();

        void OnCorpsePopulate(ScientistNPC npc, NPCPlayerCorpse corpse) => MoveCustomLootNpc(npc, corpse);

        private Object OnGoldCardSwipe(CardReader reader, Keycard card, BasePlayer player)
        {
            if (reader.OwnerID is 0 or not ownerIDElemnts) return null;
            if (!readerManipulator.TryGetValue(reader, out List<BaseEntity> manipulator)) return null;

            foreach (BaseEntity baseEntity in manipulator)
            {
                switch (baseEntity)
                {
                    case Door door:
                        door.SetOpen(true);
                        break;
                    case SupplyDrop:
                        baseEntity.SetFlag(BaseEntity.Flags.Busy, false);
                        break;
                }
            }
            
            reader.ResetIOState();
            return false;
        }

        private Object OnCardSwipe(CardReader reader, Keycard card, BasePlayer player)
        {
            if (reader.OwnerID is 0 or not ownerIDElemnts) return null;
            if (card.accessLevel != reader.accessLevel) return null;
            if (!readerManipulator.TryGetValue(reader, out List<BaseEntity> manipulator)) return null;

            foreach (BaseEntity baseEntity in manipulator)
            {
                switch (baseEntity)
                {
                    case Door door:
                        door.SetOpen(true);
                        break;
                    case SupplyDrop:
                        baseEntity.SetFlag(BaseEntity.Flags.Busy, false);
                        break;
                }
            }
            
            reader.ResetIOState();
         
            return null;
        }
        
        private void OnSupplyDropDropped(SupplyDrop supplyDrop, CargoPlane cargoPlane)
        {
            if (supplyDrop == null) return;
            if (cargoPlane.OwnerID != ownerIDElemnts) return;
            if (!activeCargoPlane.TryGetValue(cargoPlane, out CargoPlaneRepository planeRepository)) return;

            String presetKey = planeRepository.presetKey;
            if (String.IsNullOrWhiteSpace(presetKey) || !config.presetSupplyDrops.TryGetValue(presetKey, out Configuration.PresetSupply presetSupply))
            {
                PrintWarning(LanguageEn ? $"The specified preset - {presetKey} was not found in the configuration, dropping cargo is not possible" : $"Указанный пресет - {presetKey} не найден в конфигурации, вызов дропа невозможен");
                return;
            }

            activeCargoPlane.Remove(cargoPlane);
            
            Vector3 dropPosition = supplyDrop.transform.position;
            supplyDrop.Kill();

            switch (presetSupply.generalSetting.defenderLevel)
            {
                case TypeLevels.Level0:
                    SpawnedLevel0(presetKey, dropPosition);
                    break;
                case TypeLevels.Level1:
                    SpawnedLevel1(presetKey, dropPosition);
                    break;
                case TypeLevels.Level2:
                    SpawnedLevel2(presetKey, dropPosition);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        private Object OnTurretTarget(AutoTurret turret, ScientistNPC npc)
        {
            if (turret.OwnerID == 77532325) //for iqturret
                return null;
            
            if (npc == null || npc.IsDestroyed)
                return null;
        
            if (turret.OwnerID == ownerIDElemnts && npc.OwnerID == ownerIDElemnts)
                return false;
            
            return null;
        }
        
        private Object OnTurretTarget(AutoTurret turret, Drone drone)
        {
            if (turret.OwnerID == 77532325) //for iqturret
                return null;
            
            if (drone == null || drone.IsDestroyed)
                return null;
        
            if (turret.OwnerID == ownerIDElemnts && IsDronePatrol(drone.OwnerID))
                return false;
            
            return null;
        }
        
        private void OnLootEntity(BasePlayer player, SupplyDrop supplyDrop)
        {
            if (supplyDrop.OwnerID != ownerIDElemnts && supplyDrop.skinID != skinIDElement) return;
            if (listDestroyedSupplyInvoke.Contains(supplyDrop)) return;
            supplyDrop.OwnerID = player.userID;

            if (levelsDropActive.TryGetValue(supplyDrop, out ActiveDropRepository activeDrop))
            {
                Int32 levelDropInt = (Int32)activeDrop.levelSupply;
                Interface.CallHook("OnLootedDefenderSupply", player, levelDropInt);
            }

            if(config.generalSetting.alertSetting.useAlertLootedDrop)
                foreach (BasePlayer playerList in BasePlayer.activePlayerList)
                    SendChat(GetLang("ALERT_CHAT_DEFENDER_CARGO_DROPPED_LOOTED", playerList.UserIDString, player.displayName), playerList);

            SendDiscord(TypeActionDiscord.SupplyOpened);
            
            StartAutoEvent();
        }
        
        void OnLootEntityEnd(BasePlayer player, SupplyDrop supplyDrop) 
        {
            if (!supplyDrop.OwnerID.IsSteamId()) return;
            if (listDestroyedSupplyInvoke.Contains(supplyDrop)) return;
            if (!levelsDropActive.ContainsKey(supplyDrop)) return;

            listDestroyedSupplyInvoke.Add(supplyDrop);
            supplyDrop.Invoke(() => DestroySupply(supplyDrop), config.generalSetting.timeRevomeDropAfterLoot);
        }
        
        private object OnEntityKill(SupplyDrop supplyDrop)
        {
            if (unSubEntityKill) return null;
            if (supplyDrop.OwnerID.IsSteamId())
            {
                if (!levelsDropActive.ContainsKey(supplyDrop))
                    return null;

                if (!listDestroyedSupplyInvoke.Contains(supplyDrop))
                    return null;
        
                return false;
            }

            if (supplyDrop.OwnerID == ownerIDElemnts || supplyDrop.skinID == skinIDElement)
                DestroySupply(supplyDrop);

            return null;
        }

        //for IQTurret
        Object OnSetupTurret(AutoTurret entityTurret)
        {
            if (IsValidTurret(entityTurret.OwnerID)) return false;
            return null;
        }
        
        //for IQTurret
        Object OnSetupsAutoTurret(AutoTurret entityTurret)
        {
            if (entityTurret.OwnerID == 77532325) return false;
            return null;
        }
        
        #endregion

        #region Lang

        private static StringBuilder sb = new StringBuilder();

        public String GetLang(String LangKey, String userID = null, params Object[] args)
        {
            sb.Clear();
            if (args == null) return lang.GetMessage(LangKey, this, userID);
            sb.AppendFormat(lang.GetMessage(LangKey, this, userID), args);
            return sb.ToString();
        }

        private new void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["ALERT_CHAT_DEFENDER_CARGO_PLANE_START"] = "<color=#CD412B>Attention survivors!</color>\nA defender cargo plane <color=#1F6BA0>`Boeing 747-400ERF`</color> is en route to your island for a delivery!",
                ["ALERT_CHAT_DEFENDER_CARGO_DROPPED"] = "The scientific cargo plane has dropped a defender cargo at grid <color=#738D45>{0}</color>, you can claim it for yourself!",
                ["ALERT_CHAT_DEFENDER_CARGO_DROPPED_LOOTED"] = "<color=#738D45>{0}</color> has reached the defender cargo and started looting it",
                ["ALERT_CHAT_DEFENDER_CARGO_DROPPED_ADDITIONAL_CARD_INFO"] = "\nYo open this cargo, you will need a {0} access card",
                ["CARD_ALERT_GREEN"] = "<color=#738D45>green</color>",
                ["CARD_ALERT_BLUE"] = "<color=#1F6BA0>blue</color>",
                ["CARD_ALERT_RED"] = "<color=#CD412B>red</color>",
                ["ALERT_GAMETIPS_DEFENDER_CARGO_PLANE_START"] = "DEFENDER CARGO PLANE INBOUND"
            }, this);

            lang.RegisterMessages(new Dictionary<String, String>
            {
                ["ALERT_CHAT_DEFENDER_CARGO_PLANE_START"] = "<color=#CD412B>Внимание выжившие!</color>\nНа ваш остров вылетел грузовой самолет ученых <color=#1F6BA0>`Boeing 747-400ERF`</color> для доставки груза!",
                ["ALERT_CHAT_DEFENDER_CARGO_DROPPED"] = "Самолет ученых сбросил защищенный груз в квадрате <color=#738D45>{0}</color>, вы можете присвоить его себе!",
                ["ALERT_CHAT_DEFENDER_CARGO_DROPPED_ADDITIONAL_CARD_INFO"] = "\nДля открытия данного груза потребуется {0} карта доступа",
                ["CARD_ALERT_GREEN"] = "<color=#738D45>зеленая</color>",
                ["CARD_ALERT_BLUE"] = "<color=#1F6BA0>синяя</color>",
                ["CARD_ALERT_RED"] = "<color=#CD412B>красная</color>",
                ["ALERT_CHAT_DEFENDER_CARGO_DROPPED_LOOTED"] = "<color=#738D45>{0}</color> добрался до защищенного груза и начал его грабить",
                ["ALERT_GAMETIPS_DEFENDER_CARGO_PLANE_START"] = "ГРУЗОВОЙ САМОЛЕТ УЧЕНЫХ ПРИБЫЛ",
            }, this, "ru");
        }

        #endregion

        #region Scripting
        public class LangingController : MonoBehaviour
        {
            private Boolean isDestroyed;
            private SupplyDrop supplyDrop;
            private Rigidbody rbSupply;

            private Boolean isStartEffect;
            
            private List<FlameThrower> enginedList;
            
            private TypeLevels currentLevel;
            
            private Single yGroundPos;
            private Single lastYPos;
            private Single timeStationary; 
            private Single timeRotating; 
            
            private Vector3 lastXZPos;

            private String presetInConfig; 
            
            private const Single rotatingThreshold = 0.3f; 
            private const Single rotationChangeThreshold = 0.05f; 
            private const Single maxYDoStartEngine = 70f;
            private const Single maxYStopEngine = 3f;
            private const Single fallSpeedMultiplier = 3.5f;
            private const Single stationaryThreshold = 1f; 
            
            public void InitializeDrop(SupplyDrop drop, TypeLevels level, String presetKey)
            {
                if (_ == null) return;
                presetInConfig = presetKey;
                supplyDrop = drop;
                currentLevel = level;
                enginedList = new List<FlameThrower>();
                isStartEffect = false;
                rbSupply = supplyDrop.GetComponent<Rigidbody>();
                
                yGroundPos = GetGroundPosition(supplyDrop.transform.position).y;

                _.langingComponents.Add(this);
            }

            private void SetupEngine()
            {
                if (_ == null) return;
                _.SendNPC(supplyDrop, presetInConfig);
                
                isStartEffect = true;

                Effect.server.Run(explosionPrefab, transform.position);
                
                supplyDrop.SetFlag(BaseEntity.Flags.Reserved2, true);
                PositionEntity cashePos = _.positionCashed[currentLevel][engineStopperPrefab];
                SphereEntity sphereEngine = _.SetupSphereEntity(supplyDrop, cashePos.spherePos.sizeSphere, cashePos.spherePos.position, default);
                
                foreach (PositionEntity.EntityPos entityPos in cashePos.entityPosList)
                {
                    FlameThrower flamer = GameManager.server.CreateEntity(engineStopperPrefab, entityPos.localPosition, entityPos.localQuaternion) as FlameThrower;
                    flamer.SetParent(sphereEngine);
                    flamer.Spawn();
                
                    enginedList.Add(flamer);
                    _.levelsDropActive[supplyDrop].entitiesList.Add(flamer);
                }

                if (!IsInvoking(nameof(DestroyEngine)))
                    Invoke(nameof(DestroyEngine), 30f);
            }
            
            private void DestroyEngine()
            {
                if (isDestroyed) return;
                isDestroyed = true;
                if (!supplyDrop) return;
                
                supplyDrop.SetFlag(BaseEntity.Flags.Reserved2, false);
                
                if (IsInvoking(nameof(DestroyEngine)))
                    CancelInvoke(nameof(DestroyEngine));
                
                if (enginedList != null)
                {
                    foreach (FlameThrower thrower in enginedList)
                    {
                        if (!thrower.IsDestroyed)
                            thrower.Kill();
                    }

                    enginedList.Clear();
                }
                
                if (_ is { langingComponents: not null } && _.langingComponents.Contains(this))
                    _.langingComponents.Remove(this);
                
                _.PveModeAddedZone(supplyDrop);
                
                Destroy(this);
            }

            private void SpeedController()
            {
                Vector3 newVelocity = rbSupply.velocity;
                newVelocity.y -= fallSpeedMultiplier; 
                rbSupply.velocity = newVelocity;
            }

            private Vector3 GetGroundPosition(Vector3 sourcePos)
            {
                if (Physics.Raycast(sourcePos, Vector3.down, out RaycastHit hit, Mathf.Infinity,
                        LayerMask.GetMask("Terrain", "World", "Default", "Construction", "Deployed")))
                    sourcePos.y = hit.point.y;

                sourcePos.y = Mathf.Max(sourcePos.y, TerrainMeta.HeightMap.GetHeight(sourcePos));

                return sourcePos;
            }
            
            private void CheckUncontrolledFlight()
            {
                if (Mathf.Approximately(supplyDrop.transform.position.y, lastYPos))
                {
                    timeStationary += Time.fixedDeltaTime;
                    if (timeStationary >= stationaryThreshold)
                        DestroyEngine();
                }
                else timeStationary = 0f;
                
                lastYPos = supplyDrop.transform.position.y;
                
                Vector3 currentXZPos = new Vector3(supplyDrop.transform.position.x, 0, supplyDrop.transform.position.z);
                if (Vector3.Distance(currentXZPos, lastXZPos) > rotationChangeThreshold)
                {
                    timeRotating += Time.fixedDeltaTime;
                    if (timeRotating >= rotatingThreshold)
                        DestroyEngine();
                }
                else  timeRotating = 0f;

                lastXZPos = currentXZPos;
            }
            
            private void FixedUpdate()
            {
                Single yPosDrop = supplyDrop.transform.position.y - yGroundPos;
                
                if (yPosDrop > maxYDoStartEngine)
                    SpeedController();
                
                if (!isStartEffect && yPosDrop <= maxYDoStartEngine)
                    SetupEngine();
                
                if(isStartEffect && yPosDrop <= maxYStopEngine)
                    DestroyEngine();
                
                CheckUncontrolledFlight();
            }
            
            private void OnDestroy()
            {
                DestroyEngine();
            }
        }

        #endregion

        #region API
        private Boolean IsValidTurret(UInt64 ownerID) => ownerID == ownerIDElemnts;
        private Boolean IsValidSupplyDrop(UInt64 ownerID) => ownerID == ownerIDElemnts;
        private List<String> GetAllPresetsKeys() => config.presetSupplyDrops.Keys.ToList();
        private List<String> GetAllPositionsKeys() => positionSaved.Keys.ToList();
        private List<String> GetParentPositionsKeys() => positionSaved.Where(x => !String.IsNullOrWhiteSpace(x.Value.monumentParent)).Select(x => x.Key).ToList();
        private List<String> GetCustomPositionsKeys() => positionSaved.Where(x => String.IsNullOrWhiteSpace(x.Value.monumentParent)).Select(x => x.Key).ToList();
        private void SendCargo() => SendCargoPlane(default, isApi: true);
        private void SendCargo(String keyPreset) => SendCargoPlane(keyPreset, isApi: true);
        private void SendCargo(String keyPreset, String keyPosition) => SendCargoPlane(keyPreset, keyPosition, isApi: true);

        #endregion
    }
}