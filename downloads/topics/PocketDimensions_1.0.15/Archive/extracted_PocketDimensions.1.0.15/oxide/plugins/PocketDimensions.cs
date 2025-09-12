using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using WebSocketSharp;


namespace Oxide.Plugins
{
    [Info("PocketDimensions", "Nikedemos", "1.0.15")]
    [Description("It's bigger on the inside")]
    public class PocketDimensions : RustPlugin
    {
        private const string VERSION = "1.0.15";

        [PluginReference]
        private Plugin CopyPaste, NoEscape, ServerRewards, Economics;

        public static PocketDimensions Instance;

        #region CONST/STATIC

        public const float TELEPORTATION_PORTAL_OFFSET = 1f;
        public const float TELEPORTATION_BOX_OFFSET = 1f;

        public const string PREFAB_FOUNDATION_SQUARE = "assets/prefabs/building core/foundation/foundation.prefab";
        public const string PREFAB_FOUNDATION_TRIANGLE = "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";

        public const string PREFAB_FLOOR_SQUARE = "assets/prefabs/building core/floor/floor.prefab";
        public const string PREFAB_FLOOR_FRAME = "assets/prefabs/building core/floor.frame/floor.frame.prefab";

        public const string PREFAB_WALL = "assets/prefabs/building core/wall/wall.prefab";
        public const string PREFAB_WALL_WINDOW = "assets/prefabs/building core/wall.window/wall.window.prefab";
        public const string PREFAB_WALL_FRAME = "assets/prefabs/building core/wall.frame/wall.frame.prefab";

        public const string PREFAB_WALL_DOORWAY = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
        public const string PREFAB_DOOR_METAL = "assets/prefabs/building/door.hinged/door.hinged.metal.prefab";
        public const string PREFAB_TC = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";

        public const string PREFAB_WOOD_STORAGE_BOX = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";

        public const string PREFAB_BUNKER_PORTAL = "assets/prefabs/missions/portal/bunker_door_portal.prefab";

        public const string PREFAB_MAPMARKER_GENERIC = "assets/prefabs/tools/map/genericradiusmarker.prefab";

        public const string PREFAB_BUILD_EFFECT = "assets/bundled/prefabs/fx/build/frame_place.prefab";

        //added in 1.0.12
        public const string PREFAB_GENERATOR_WINDMILL = "assets/prefabs/deployable/windmill/electric.windmill.small.prefab";
        public const string PREFAB_GENERATOR_SOLAR = "assets/prefabs/deployable/playerioents/generators/solar_panels_roof/solarpanel.large.deployed.prefab";
        public const string PREFAB_GENERATOR_FUEL = "assets/prefabs/deployable/playerioents/generators/fuel generator/small_fuel_generator.deployed.prefab";
        public const string PREFAB_GENERATOR_TEST_1 = "assets/prefabs/deployable/playerioents/generators/generator.small.prefab";
        public const string PREFAB_GENERATOR_TEST_2 = "assets/prefabs/io/electric/generators/generator.static.prefab";
        public const string PREFAB_WATER_CATCHER_LARGE = "assets/prefabs/deployable/water catcher/water_catcher_large.prefab";
        public const string PREFAB_WATER_CATCHER_SMALL = "assets/prefabs/deployable/water catcher/water_catcher_small.prefab";
        public const string PREFAB_WATER_COMBINER = "assets/prefabs/deployable/playerioents/fluidcombiner/fluid.combiner.deployed.prefab";
        public const string PREFAB_WATER_SPLITTER = "assets/prefabs/deployable/playerioents/fluidsplitter/fluidsplitter.prefab";
        public const string PREFAB_ELECTRICAL_COMBINER = "assets/prefabs/deployable/playerioents/gates/combiner/electrical.combiner.deployed.prefab";
        public const string PREFAB_ELECTRICAL_SPLITTER = "assets/prefabs/deployable/playerioents/splitter/splitter.prefab";
        public const string PREFAB_ELECTRICAL_BRANCH = "assets/prefabs/deployable/playerioents/gates/branch/electrical.branch.deployed.prefab";
        public const string PREFAB_WATER_PURIFIER_POWERED = "assets/prefabs/deployable/playerioents/poweredwaterpurifier/poweredwaterpurifier.deployed.prefab";
        public const string PREFAB_WATER_BARREL = "assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab";
        public const string PREFAB_WATER_PURIFIER_STORAGE = "assets/prefabs/deployable/playerioents/poweredwaterpurifier/poweredwaterpurifier.storage.prefab";

        //specific to doorway
        public const string SOCKET_NAME_WALL_DOORWAY_DOORWAY_FEMALE_1 = "wall.doorway/sockets/doorway-female/1";
        public const string SOCKET_NAME_WALL_DOORWAY_DOORWAY_FEMALE_2 = "wall.doorway/sockets/doorway-female/2";

        //specific to wall frame
        public const string SOCKET_NAME_WALL_FRAME_FRAME_FEMALE_1 = "wall.frame/sockets/frame-female/1";
        public const string SOCKET_NAME_WALL_FRAME_FRAME_FEMALE_2 = "wall.frame/sockets/frame-female/2";

        //specific to window frame
        public const string SOCKET_NAME_WALL_WINDOW_WINDOW_FEMALE_1 = "wall.window/sockets/window-female/1";
        public const string SOCKET_NAME_WALL_WINDOW_WINDOW_FEMALE_2 = "wall.window/sockets/window-female/2";
        public const string SOCKET_NAME_WALL_WINDOW_SHUTTERS_FEMALE_1 = "wall.window/sockets/shutters-female/1";
        public const string SOCKET_NAME_WALL_WINDOW_SHUTTERS_FEMALE_2 = "wall.window/sockets/shutters-female/2";
        public const string SOCKET_NAME_WALL_WINDOW_DRESSING_FEMALE_1 = "wall.window/sockets/dressing-female/1";
        public const string SOCKET_NAME_WALL_WINDOW_DRESSING_FEMALE_2 = "wall.window/sockets/dressing-female/2";

        //specific to floor frame
        public const string SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_1 = "floor.frame/sockets/frame-female/1";
        public const string SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_2 = "floor.frame/sockets/frame-female/2";
        public const string SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_3 = "floor.frame/sockets/frame-female/3";
        public const string SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_4 = "floor.frame/sockets/frame-female/4";

        public const string FX_CODELOCK = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";
        public const string FX_TELEPORT_APPEAR = "assets/prefabs/missions/portal/proceduraldungeon/effects/appear.prefab";
        public const string FX_TELEPORT_DISAPPEAR = "assets/prefabs/missions/portal/proceduraldungeon/effects/disappear.prefab";

        public const string ITEM_SHORTNAME_WOOD_STORAGE_BOX = "box.wooden";
        public static ItemDefinition ItemDefinitionWoodStorageBox;

        public static readonly BaseEntity.Flags FLAG_INTERNAL = BaseEntity.Flags.Reserved7;
        public static readonly BaseEntity.Flags FLAG_INDESTRUCTIBLE = BaseEntity.Flags.Reserved9;
        public static readonly BaseEntity.Flags FLAG_SPECIFIC = BaseEntity.Flags.OnFire; //used to mark middle foundations and external doorway

        public static readonly BasePlayer.PlayerFlags FLAG_IMMUNE_AFTER_TELEPORT = BasePlayer.PlayerFlags.Unused1;
        public static readonly BasePlayer.PlayerFlags FLAG_TOAST_TIMEOUT = BasePlayer.PlayerFlags.Unused2;

        public const string PREFIX_SHORT = "pd.";
        public const string PREFIX_LONG = "pocketdimensions.";

        public const string CMD_CHECK = PREFIX_SHORT + "check";
        public const string CMD_CONVERT = PREFIX_SHORT + "convert";
        public const string CMD_GIVE_BOX = PREFIX_SHORT + "givebox";
        public const string CMD_LOST_FOUND = PREFIX_SHORT + "lostnfound";
        public const string CMD_LIST = PREFIX_SHORT + "list";

        public const string CMD_LIST_ARG_RECLAIM_ITEM = "reclaim_item";

        public const string CMD_LIST_ARG_TP_TO_PORTAL = "tp_to_portal";
        public const string CMD_LIST_ARG_TP_TO_BOX = "tp_to_box";

        public const string CMD_LIST_ARG_KILL_DIMENSION = "kill_dimension";

        public const string CMD_LIST_ARG_PLAYER_INFO = "player_info";

        public const string CMD_LIST_USAGE_PART_1 = "Please provide a valid dimension number followed by one of these options: ";
        public const string CMD_LIST_USAGE_PART_2 = CMD_LIST_ARG_PLAYER_INFO + ", " + CMD_LIST_ARG_TP_TO_BOX + ", " + CMD_LIST_ARG_TP_TO_PORTAL + ", " + CMD_LIST_ARG_RECLAIM_ITEM + ", " + CMD_LIST_ARG_KILL_DIMENSION;
        public const string CMD_LIST_USAGE_FULL = CMD_LIST_USAGE_PART_1 + CMD_LIST_USAGE_PART_2;
        public const string CMD_LIST_REQUIRES_PLAYER = "This option requires executing of the `" + CMD_LIST + "` command as a player in-game, from chat or F1 console, it cannot be invoked from RCON.";

        public const string CMD_EMERGENCY_CLEANUP = PREFIX_SHORT + "emergencycleanup";
        public const string CMD_REPLACE = PREFIX_SHORT + "replace";

        public const string PERM_ADMIN = PREFIX_LONG + "admin";

        public const string PERM_REPLACE = PREFIX_LONG + "replace";

        public const string PERM_ALL = PREFIX_LONG + "player.all";
        public const string PERM_DEPLOY_PICKUP = PREFIX_LONG + "deploy.pickup";
        public const string PERM_ENTER_EXIT = PREFIX_LONG + "player.enter.exit";
        public const string PERM_CONVERT_TC = PREFIX_LONG + "player.convert";
        public const string PERM_RESPAWN_IN_POCKET = PREFIX_LONG + "player.respawn";

        public const ulong SKIN_WATERBASE_TC = 1337424002;

        public static bool Unloading = false;

        public const int BREAK_TOASTS_EVERY_N_CHARACTERS = 80;

        public static Dictionary<string, Translate.Phrase> ToastsCached;

        public static readonly Vector3 OFFSET_TC_POS_SIZE_1 = new Vector3(0F, 0.08F, -1F);
        public static readonly Vector3 OFFSET_PORTAL_POS_SIZE_1 = new Vector3(0F, 1F, 1.5F);
        public static readonly Vector3 OFFSET_PORTAL_ROT_SIZE_1 = new Vector3(270F, 0F, 0F);


        public const byte LAYER_VEHICLE_WORLD = 15;
        public const byte LAYER_DEFAULT = 0;
        public const byte LAYER_PLAYER = 17;
        public const byte LAYER_DEBRIS = 26;
        public const byte LAYER_DEPLOYED = 8;
        public const byte LAYER_RAGDOLL = 9;
        public const byte LAYER_AI = 11;
        public const byte LAYER_CONSTRUCTION = 21;
        public const byte LAYER_CONSTRUCTION_SOCKET = 22;
        public const byte LAYER_CLUTTER = 25;
        public const byte LAYER_WORLD = 16;
        public const byte LAYER_PREVENT_BUILDING = 29;

        public const int LAYERMASK_INTERESTING_ENTITIES = (1 << LAYER_VEHICLE_WORLD) | (1 << LAYER_DEFAULT) | (1 << LAYER_PLAYER) | (1 << LAYER_DEBRIS) | (1 << LAYER_DEPLOYED) | (1 << LAYER_RAGDOLL) | (1 << LAYER_AI) | (1 << LAYER_CONSTRUCTION) | (1 << LAYER_CONSTRUCTION_SOCKET) | (1 << LAYER_CLUTTER) | (1 << LAYER_WORLD) | (1 << LAYER_PREVENT_BUILDING);

        public const uint PREFAB_ID_SMALL_WOODEN_BOX = 1560881570;

        public const string KILL_PORTAL_REASON_BOX_KILLED = "BOX_KILLED";
        public const string KILL_PORTAL_REASON_SHELLBLOCK_KILLED = "SHELL_KILLED";
        public const string KILL_PORTAL_REASON_AWAKE_BUILDING_NOT_FOUND = "BUILDING_NULL";
        public const string KILL_PORTAL_REASON_AWAKE_SHELLBLOCK_MISCOUNT = "SHELL_MICOUNT";
        public const string KILL_PORTAL_REASON_AWAKE_MIDDLE_FOUNDATION_NULL = "MID_FLOOR_NULL";
        public const string KILL_PORTAL_REASON_AWAKE_EXIT_DOORWAY_NULL = "EXIT_DOOR_NULL";
        public const string KILL_PORTAL_REASON_PARENT_DIMENSION_KILLED = "PARENT_DIM_KILLED";
        public const string KILL_PORTAL_REASON_MANUAL_ADMIN_CMD = "MANUAL_ADM_CMD";

        public const string LOGFILE_PORTAL_KILLS_PREVENTED = "portal_kills_prevented";
        public const string LOGFILE_PORTAL_KILLS_EXPECTED = "portal_kills_expected";
        #endregion

        #region HOOK SUBSCRIPTIONS
        void OnServerInitialized()
        {
            Instance = this;
            KillPortalReasonLast = string.Empty;
            lang.RegisterMessages(LangMessages, this);

            permission.RegisterPermission(PERM_ADMIN, this);
            permission.RegisterPermission(PERM_REPLACE, this);
            permission.RegisterPermission(PERM_ALL, this);
            permission.RegisterPermission(PERM_DEPLOY_PICKUP, this);
            permission.RegisterPermission(PERM_ENTER_EXIT, this);
            permission.RegisterPermission(PERM_CONVERT_TC, this);
            permission.RegisterPermission(PERM_RESPAWN_IN_POCKET, this);

            LoadConfigData();
            ProcessConfigData();

            AddCovalenceCommand(CMD_EMERGENCY_CLEANUP, nameof(CommandEmergencyCleanup), PERM_ADMIN);
            AddCovalenceCommand(CMD_CONVERT, nameof(CommandConvert), PERM_ADMIN);
            AddCovalenceCommand(CMD_CHECK, nameof(CommandViabilityCheck), PERM_ADMIN);
            AddCovalenceCommand(CMD_GIVE_BOX, nameof(CommandGiveBox), PERM_ADMIN);
            AddCovalenceCommand(CMD_LOST_FOUND, nameof(CommandLostFound), PERM_ADMIN);
            AddCovalenceCommand(CMD_LIST, nameof(CommandList), PERM_ADMIN);

            AddCovalenceCommand(CMD_REPLACE, nameof(CommandReplace)); //no perm here. checked inside.

            ItemDefinitionWoodStorageBox = ItemManager.FindItemDefinition(ITEM_SHORTNAME_WOOD_STORAGE_BOX);

            SpawnedPasteRequest.RequestByPosition = new Dictionary<Vector3, SpawnedPasteRequest>();

            ReusableCardinalDirectionPositions = new Vector3[4];
            ReusableNeighbourPosTable = new Vector3[4][]
            {
                new Vector3[8],
                new Vector3[8],
                new Vector3[8],
                new Vector3[8],
            };

            ReusableFloorList = new List<BuildingBlock>();
            ReusableFoundationList = new List<BuildingBlock>();
            ReusableCombinedWallAndDoorwayList = new List<BuildingBlock>();
            ReusableShellElementList = new List<BuildingBlock>();
            ReusableExtraBaseCombatEntities = new List<BaseCombatEntity>();

            ReusableVisibilityList = new ListHashSet<BaseEntity>();
            ReusableColBuffer = new Collider[2048];

            ReusableCopypasteArgs = new string[] { "autoheight", "false" };

            ReusableMatchFoundationToCeiling = new Dictionary<BuildingBlock, BuildingBlock>(9);

            ReusableGatherItemAmountArray = new GatherItemAmount[512];

            DimensionalBox.ServerCacheBox = new Dictionary<ulong, DimensionalBox>();
            DimensionalPocket.ServerCachePortal = new Dictionary<ulong, DimensionalPocket>();
            DimensionalPocket.ServerCacheShellBlockToPocket = new Dictionary<ulong, DimensionalPocket>();
            DimensionalPocket.ServerCacheBuildingIDToPocket = new Dictionary<uint, DimensionalPocket>();

            ToastsCached = new Dictionary<string, Translate.Phrase>();

            ExternalShellBlockReplacementsShortnameToFull = new Dictionary<string, string>
            {
                [Path.GetFileNameWithoutExtension(PREFAB_WALL)] = PREFAB_WALL,
                [Path.GetFileNameWithoutExtension(PREFAB_WALL_FRAME)] = PREFAB_WALL_FRAME,
                [Path.GetFileNameWithoutExtension(PREFAB_WALL_WINDOW)] = PREFAB_WALL_WINDOW,

                [Path.GetFileNameWithoutExtension(PREFAB_FLOOR_SQUARE)] = PREFAB_FLOOR_SQUARE,
                [Path.GetFileNameWithoutExtension(PREFAB_FLOOR_FRAME)] = PREFAB_FLOOR_FRAME,
            };

            //populate families...
            ExternalShellBlockReplacementFamilies = new ListHashSet<string>[]
            {
                //sides
                new ListHashSet<string>
                {
                    PREFAB_WALL,
                    PREFAB_WALL_FRAME,
                    PREFAB_WALL_WINDOW
                },

                //tops
                new ListHashSet<string>
                {
                    PREFAB_FLOOR_SQUARE,
                    PREFAB_FLOOR_FRAME
                },
            };

            //and now from families to final replacements

            ExternalShellBlockReplacements = new Dictionary<string, ListHashSet<string>>();

            //for every family...
            for (var f = 0; f < ExternalShellBlockReplacementFamilies.Length; f++)
            {
                var family = ExternalShellBlockReplacementFamilies[f];

                //and for every member of that family
                for (var m = 0; m < family.Count; m++)
                {
                    var memberName = family[m];

                    var membersOtherMembers = new ListHashSet<string>();

                    //and for every member's other members

                    for (var o = 0; o < family.Count; o++)
                    {
                        if (m == o)
                        {
                            //not you.
                            continue;
                        }

                        var otherMemberName = family[o];

                        membersOtherMembers.Add(otherMemberName);
                    }

                    ExternalShellBlockReplacements.Add(memberName, membersOtherMembers);
                }
            }

            //and sockets to check

            ExternalShellBlockReplacementsSocketsToCheck = new Dictionary<string, ListHashSet<string>>()
            {
                [PREFAB_FLOOR_SQUARE] = new ListHashSet<string>(), //empty, no sockets there to check
                [PREFAB_WALL] = new ListHashSet<string>(), //empty, no sockets to check there

                [PREFAB_WALL_FRAME] = new ListHashSet<string>()
                {
                    SOCKET_NAME_WALL_FRAME_FRAME_FEMALE_1,
                    SOCKET_NAME_WALL_FRAME_FRAME_FEMALE_2
                },
                [PREFAB_WALL_WINDOW] = new ListHashSet<string>
                {
                    SOCKET_NAME_WALL_WINDOW_DRESSING_FEMALE_1,
                    SOCKET_NAME_WALL_WINDOW_SHUTTERS_FEMALE_1,
                    SOCKET_NAME_WALL_WINDOW_WINDOW_FEMALE_1,
                    SOCKET_NAME_WALL_WINDOW_DRESSING_FEMALE_2,
                    SOCKET_NAME_WALL_WINDOW_SHUTTERS_FEMALE_2,
                    SOCKET_NAME_WALL_WINDOW_WINDOW_FEMALE_2
                },


                [PREFAB_FLOOR_FRAME] = new ListHashSet<string>
                {
                    SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_1,
                    SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_2,
                    SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_3,
                    SOCKET_NAME_FLOOR_FRAME_FRAME_FEMALE_4
                },
            };

            Unloading = false;

            DimensionalPlayer.PrepareStatic();


            var findAllPortals = BaseNetworkable.serverEntities.OfType<BasePortal>().ToArray();

            BasePortal candidatePortal;

            for (var e = 0; e < findAllPortals.Length; e++)
            {
                candidatePortal = findAllPortals[e];

                if (!BaseNetworkableEx.IsValid(candidatePortal))
                {
                    continue;
                }

                if (!IsEntityMarkedInternal(candidatePortal))
                {
                    continue;
                }

                candidatePortal.gameObject.AddComponent<DimensionalPocket>();
            }

            var findAllStorageContainers = BaseNetworkable.serverEntities.OfType<StorageContainer>().ToArray();

            StorageContainer candidateContainer;

            for (var e = 0; e < findAllStorageContainers.Length; e++)
            {
                candidateContainer = findAllStorageContainers[e];

                if (!BaseNetworkableEx.IsValid(candidateContainer))
                {
                    continue;
                }

                if (!IsEntityMarkedInternal(candidateContainer))
                {
                    continue;
                }

                candidateContainer.gameObject.AddComponent<DimensionalBox>();

            }

            CheckAllLocks();
            CheckAllVendingMachines();

            ConversionGuiManager.Initialize();

            PositionCheckResult.Reset();

            foreach (var player in BasePlayer.activePlayerList)
            {
                OnPlayerConnected(player);
            }
        }

        public void CheckAllVendingMachines()
        {
            if (Configuration.DimensionaPocketsAllowVendingMachineBroadcast)
            {
                return;
            }

            var findAllVendingMachines = BaseNetworkable.serverEntities.OfType<VendingMachine>().ToArray();

            VendingMachine candidateMachine;

            for (var e = 0; e < findAllVendingMachines.Length; e++)
            {
                candidateMachine = findAllVendingMachines[e];

                OnEntitySpawned(candidateMachine);
            }
        }

        public void CheckAllLocks()
        {
            if (Configuration.DimensionalBoxesAllowLockingWithLocks)
            {
                return;
            }

            var findAllLocks = BaseNetworkable.serverEntities.OfType<BaseLock>().ToArray();

            BaseLock candidateLock;

            for (var e = 0; e < findAllLocks.Length; e++)
            {
                candidateLock = findAllLocks[e];

                OnEntitySpawned(candidateLock);
            }
        }

        void Unload()
        {
            Unloading = true;

            foreach (var player in BasePlayer.activePlayerList)
            {
                ConversionGuiManager.PlayerHideAllUI(player);
            }

            ConversionGuiManager.Cleanup();

            InternalBehaviour.DetachEverythingInternal();

            DimensionalBox.ServerCacheBox = null;
            DimensionalPocket.ServerCachePortal = null;
            DimensionalPocket.ServerCacheShellBlockToPocket = null;
            DimensionalPocket.ServerCacheBuildingIDToPocket = null;
            ToastsCached = null;

            ReusableNeighbourPosTable = null;
            ReusableCardinalDirectionPositions = null;

            ReusableFloorList = null;
            ReusableFoundationList = null;
            ReusableCombinedWallAndDoorwayList = null;

            ReusableVisibilityList = null;
            ReusableColBuffer = null;
            ReusableCopypasteArgs = null;

            ReusableMatchFoundationToCeiling = null;
            ReusableGatherItemAmountArray = null;

            ReusableShellElementList = null;
            ReusableExtraBaseCombatEntities = null;

            ItemDefinitionWoodStorageBox = null;
            SpawnedPasteRequest.RequestByPosition = null;

            DimensionalPlayer.UnloadStatic();

            PositionCheckResult.Reset();

            ExternalShellBlockReplacementsShortnameToFull = null;
            ExternalShellBlockReplacementFamilies = null;
            ExternalShellBlockReplacements = null;
            ExternalShellBlockReplacementsSocketsToCheck = null;

            Instance = null;
            Unloading = false;
        }

        object OnPlayerCommand(BasePlayer player, string command, string[] args)
        {
            if (Instance == null)
            {
                return null;
            }

            if (!PositionWithinDimensionLayer(player.transform.position))
            {
                return null;
            }

            if (!Configuration.ForbiddenCommandsCheckingEnabled)
            {
                return null;
            }

            if (Configuration.ForbiddenCommandsInsideDimensions.IsNullOrEmpty())
            {
                return null;
            }

            if (!Configuration.ForbiddenCommandsInsideDimensions.Contains(command))
            {
                return null;
            }

            if (HasAdminPermission(player))
            {
                return null;
            }

            ToastPlayer(player, MSG(MSG_COMMAND_FORBIDDEN_INSIDE, player.UserIDString, command));

            return true;
        }

        //RemoverTool fix
        object canRemove(BasePlayer player, StorageContainer container)
        {
            if (container == null)
            {
                return null;
            }

            if (container.PrefabName != PREFAB_WOOD_STORAGE_BOX)
            {
                return null;
            }

            if (!IsEntityMarkedInternal(container))
            {
                return null;
            }
            
            return true; //return non null
        }

        //TruePVE fix
        object CanEntityTakeDamage(BaseCombatEntity entity, HitInfo hitInfo)
        {
            if (!BaseNetworkableEx.IsValid(entity))
            {
                return null;
            }

            if (hitInfo == null)
            {
                return null;
            }

            if (!hitInfo.damageTypes.Has(Rust.DamageType.Generic))
            {
                return null;
            }

            if (Mathf.Abs(hitInfo.damageTypes.Get(Rust.DamageType.Generic) - SPECIAL_DAMAGE_AMOUNT) > 0.001F)
            {
                return null;
            }

            return true;

        }

        void OnEntitySpawned(VendingMachine machine)
        {
            if (Instance == null)
            {
                return;
            }

            if (Instance.Configuration.DimensionaPocketsAllowVendingMachineBroadcast)
            {
                //nothing to see here
                return;
            }

            machine.Invoke(() =>
            {
                if (Instance == null)
                {
                    return;
                }

                if (machine == null)
                {
                    return;
                }

                if (!machine.IsBroadcasting())
                {
                    return;
                }

                var buildingID = machine.buildingID;

                if (buildingID == 0)
                {
                    return;
                }

                if (!DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(buildingID))
                {
                    return;
                }

                machine.SetFlag(BaseEntity.Flags.Reserved4, false);
                machine.UpdateMapMarker();
            }, 0.02F);
        }

        void OnToggleVendingBroadcast(VendingMachine machine, BasePlayer player)
        {
            OnEntitySpawned(machine);

            if (Instance == null)
            {
                return;
            }

            if (Instance.Configuration.DimensionaPocketsAllowVendingMachineBroadcast)
            {
                //nothing to see here
                return;
            }

            if (!machine.IsBroadcasting())
            {
                return;
            }

            var buildingID = machine.buildingID;

            if (buildingID == 0)
            {
                return;
            }

            if (!DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(buildingID))
            {
                return;
            }

            ToastPlayer(player, MSG(MSG_NO_VENDING_MACHINE_BROADCASTING_IN_POCKETS, player.UserIDString));
        }

        object OnSleepingBagValidCheck(SleepingBag sleepingBag, ulong playerID, bool ignoreTimers)
        {
            if (Instance == null)
            {
                return null;
            }

            if (sleepingBag == null)
            {
                return null;
            }

            var player = BasePlayer.FindByID(playerID);

            if (player == null)
            {
                return null;
            }

            var buildingID = sleepingBag.buildingID;

            if (buildingID == 0)
            {
                return null;
            }

            if (!DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(buildingID))
            {
                return null;
            }

            bool hasRespawnInPocketPermission = HasRespawnInPocketPermission(player);

            //if both the permission is present, AND you CAN respawn inside without the box being deployed, no need to go further
            if (hasRespawnInPocketPermission && !Configuration.DimensionalPocketsRespawningInsideRequiresDeployment)
            {
                return null;
            }

            //check if the player had just respawned in a non-deployed pocket Dimension


            object returnObject = null;

            if (!hasRespawnInPocketPermission)
            {
                returnObject = false;
            }
            else
            {
                //check if the pocket is deployed

                CheckPositionResultDeep(sleepingBag.transform.position);

                if (PositionCheckResult.BottomPocketFound != null && !PositionCheckResult.PocketIsDeployed)
                {
                    returnObject = false;
                    ToastPlayer(player, MSG(MSG_CANT_RESPAWN_INSIDE_NON_DEPLOYED, player.UserIDString));
                }
            }

            return returnObject;
        }

        void OnEntitySpawned(BaseLock baseLock)
        {
            if (Instance == null)
            {
                return;
            }

            if (Configuration.DimensionalBoxesAllowLockingWithLocks)
            {
                return;
            }

            if (baseLock == null)
            {
                return;
            }

            var lockOwnerThing = baseLock.parentEntity.Get(true) as StorageContainer;

            if (lockOwnerThing == null)
            {
                return;
            }

            if (lockOwnerThing.PrefabName != PREFAB_WOOD_STORAGE_BOX)
            {
                return;
            }

            if (!IsEntityMarkedInternal(lockOwnerThing))
            {
                return;
            }

            DimensionalBox box;

            if (!DimensionalBox.ServerCacheBox.TryGetValue(lockOwnerThing.net.ID.Value, out box))
            {
                return;
            }

            //drop the lock as an item

            baseLock.Invoke(() =>
            {
                if (box == null)
                {
                    baseLock.Kill();
                    return;
                }

                if (box.ThisStorageContainer == null)
                {
                    baseLock.Kill();
                    return;
                }

                var item = ItemManager.Create(baseLock.itemType, 1, baseLock.skinID);
                if (item != null)
                {
                    item.Drop(baseLock.transform.position, Vector3.zero, baseLock.transform.rotation);
                }

                if (baseLock.PrefabName.Contains("code"))
                {
                    Effect.server.Run(FX_CODELOCK, baseLock.transform.position, baseLock.transform.up);
                }

                baseLock.Kill();

            }, 0.01F);

        }

        public static string KillPortalReasonLast = string.Empty;

        object OnEntityKill(BasePortal portal)
        {
            //ignore non-internal portals from consideration, just let them through

            if (!IsEntityMarkedInternal(portal))
            {
                return null;
            }

            //non-specific means "do not kill"
            //specific means "yes kill, we're expecting it, there's also a reason for it"

            if (!IsEntityMarkedSpecific(portal))
            {
                //thwart the attempt and log to file

                LogToFile(LOGFILE_PORTAL_KILLS_PREVENTED, $"[{DateTime.Now}] Net.ID:{portal.net.ID}", this, false);

                return true;

            }

            //log the killing
            LogToFile(LOGFILE_PORTAL_KILLS_EXPECTED, $"[{DateTime.Now}] Net.ID:{portal.net.ID}; {KillPortalReasonLast} ", this, false);

            return null;


        }

        object OnEntityKill(BuildingBlock buildingBlock)
        {
            if (Instance == null)
            {
                return null;
            }

            if (buildingBlock == null)
            {
                return null;
            }

            if (!IsEntityMarkedInternal(buildingBlock))
            {
                return null;
            }

            DimensionalPocket pocket;

            if (!DimensionalPocket.ServerCacheShellBlockToPocket.TryGetValue(buildingBlock.net.ID.Value, out pocket))
            {
                return null;
            }

            return pocket.OnShellBlockKilled(buildingBlock);
        }

        object CanBeTargeted(BasePlayer player, DecayEntity entity)
        {
            if (Instance == null)
            {
                return null;
            }

            if (player.HasPlayerFlag(FLAG_IMMUNE_AFTER_TELEPORT))
            {
                return false;
            }

            return null;
        }

        object OnItemPickup(Item item, BasePlayer player)
        {
            if (Instance == null)
            {
                return null;
            }

            if (!Instance.Configuration.DimensionalBoxesItemCheckTCAuthInsideWhenHandlingItem)
            {
                return null;
            }

            if (item == null)
            {
                return null;
            }

            if (player == null)
            {
                return null;
            }

            if (!IsDimensionalBoxItem(item))
            {
                return null;
            }

            var pocket = TryGetDimensionalPocketFromNonNullItem(item);

            if (pocket == null)
            {
                return null;
            }

            if (pocket.IsPlayerBuildingBlockedOnFirstTcInsideIfThereIsAnyButOnlyIfAtLeast1Auth(player))
            {
                ToastPlayer(player, MSG(MSG_CANT_HANDLE_WITHOUT_TC_AUTH2, player.UserIDString));
                return true;
            }

            return null;

        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info) => OnPlayerMissingCommon(player);

        private void OnPlayerDisconnected(BasePlayer player, string reason) => OnPlayerMissingCommon(player);

        //not actually a hook
        private void OnPlayerMissingCommon(BasePlayer player)
        {
            if (Instance == null)
            {
                return;
            }

            if (IsPlayerNPC(player))
            {
                return;
            }

            DimensionalPlayer dimensionalPlayer;

            if (!DimensionalPlayer.ServerCachePlayer.TryGetValue(player.userID, out dimensionalPlayer))
            {
                return;
            }

            UnityEngine.Object.DestroyImmediate(dimensionalPlayer);
        }

        void OnPlayerSleepEnded(BasePlayer player) => OnPlayerConnected(player);

        void OnPlayerConnected(BasePlayer player)
        {
            if (Instance == null)
            {
                return;
            }

            if (!Configuration.DimensionalPocketsTeleportingInsideRequiresDeployment)
            {
                return;
            }

            if (IsPlayerNPC(player))
            {
                return;
            }

            DimensionalPlayer compo;

            if (DimensionalPlayer.ServerCachePlayer.ContainsKey(player.userID))
            {
                compo = DimensionalPlayer.ServerCachePlayer[player.userID];
            }
            else
            {
                compo = player.gameObject.AddComponent<DimensionalPlayer>();
            }

            compo.OnPlayerMajorUpdate(player);

        }

        object CanMoveItem(Item item, PlayerInventory playerLoot, ItemContainerId targetContainer, int targetSlot, int amount)
        {
            //TODO: Test this
            //ALso: don't allow the player to drop this if not authed.

            if (Instance == null)
            {
                return null;
            }

            if (!Instance.Configuration.DimensionalBoxesItemCheckTCAuthInsideWhenHandlingItem)
            {
                return null;
            }

            if (item == null)
            {
                return null;
            }

            if (!IsDimensionalBoxItem(item))
            {
                return null;
            }

            if (playerLoot == null)
            {
                return null;
            }

            var player = playerLoot.GetComponent<BasePlayer>();

            if (player == null)
            {
                return null;
            }

            var pocket = TryGetDimensionalPocketFromNonNullItem(item);

            if (pocket == null)
            {
                return null;
            }

            if (pocket.IsPlayerBuildingBlockedOnFirstTcInsideIfThereIsAnyButOnlyIfAtLeast1Auth(player))
            {
                ToastPlayer(player, MSG(MSG_CANT_HANDLE_WITHOUT_TC_AUTH2, player.UserIDString));
                return true;
            }

            return null;

        }

        object CanCombineDroppedItem(DroppedItem droppedItem, DroppedItem droppedTargetItem)
        {
            if (Instance == null)
            {
                return null;
            }

            if (droppedItem == null)
            {
                return null;
            }

            if (droppedTargetItem == null)
            {
                return null;
            }

            if (droppedItem.item == null)
            {
                return null;
            }

            if (droppedTargetItem.item == null)
            {
                return null;
            }

            if (!(IsDimensionalBoxItem(droppedItem.item) || IsDimensionalBoxItem(droppedItem.item)))
            {
                return null;
            }

            return false;
        }

        /*
        void OnItemRemove(Item item)
        {
            if (Instance == null)
            {
                return;
            }

            if (item == null)
            {
                return;
            }

            if (!IsDimensionalBoxItem(item))
            {
                return;
            }

            if (item.instanceData == null)
            {
                return;
            }

            var pocket = TryGetDimensionalPocketFromNonNullItem(item);

            if (pocket == null)
            {
                return;
            }

            //MarkEntityAsSpecific(pocket.ThisPortal);            
        }*/

        object OnPortalUse(BasePlayer player, BasePortal portal)
        {
            if (Instance == null)
            {
                return null;
            }

            if (player == null)
            {
                return null;
            }

            if (!IsEntityMarkedInternal(portal))
            {
                return null;
            }

            DimensionalPocket pocket;

            if (!DimensionalPocket.ServerCachePortal.TryGetValue(portal.net.ID.Value, out pocket))
            {
                return null;
            }

            if (!HasEnterExitPermission(player))
            {
                ToastPlayer(player, MSG(MSG_NO_PERMISSION_ENTER_EXIT, player.UserIDString));
                return true;
            }

            if (Instance.Configuration.CheckEscapeBlockWhenExitingDimensions)
            {
                if (IsEscapeBlocked(player))
                {
                    ToastPlayer(player, MSG(MSG_ESCAPE_BLOCKED_CANT_EXIT, player.UserIDString));
                    return true;
                }
            }

            pocket.PlayerUsePortal(player);

            //return non null
            return true;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (Instance == null)
            {
                return null;
            }

            if (info == null)
            {
                return null;
            }


            if (!IsEntityMarkedInternal(entity))
            {
                return null;
            }

            //always let decay damage through
            if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Decay)
            {
                return null;
            }

            if (!Configuration.DimensionalPocketsExteriorBlocksAllowNonDecayDamage)
            {
                if (DimensionalPocket.ServerCacheShellBlockToPocket.ContainsKey(entity.net.ID.Value))
                {
                    return true;
                }
            }

            if (!IsEntityMarkedIndestructible(entity))
            {
                return null;
            }

            //is this a box?...
            if (DimensionalBox.ServerCacheBox.ContainsKey(entity.net.ID.Value))
            {
                if (!Configuration.DimensionalBoxesAllowDamage)
                {
                    return true;
                }
                else
                {
                    //adjust the damage
                    info.damageTypes.ScaleAll(1F / Mathf.Max(0.001F, Configuration.DimensionaBoxesDamageResistanceDivisor));
                    return null;
                }
            }

            return null;

        }

        void OnLootEntity(BasePlayer player, BuildingPrivlidge tc)
        {
            if (Instance == null)
            {
                return;
            }

            if (player == null)
            {
                return;
            }

            if (!HasConvertTCPermission(player))
            {
                return;
            }


            if (tc.buildingID == 0)
            {
                return;
            }

            if (IsTCWaterBaseTC(tc))
            {
                return;
            }

            //is it already converted?

            if (DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(tc.buildingID))
            {
                return;
            }

            ConversionGuiManager.PlayerOpenTC(player, tc);

        }

        void OnLootEntityEnd(BasePlayer player, BuildingPrivlidge tc)
        {
            if (Instance == null)
            {
                return;
            }

            if (player == null)
            {
                return;
            }

            if (IsTCWaterBaseTC(tc))
            {
                return;
            }


            ConversionGuiManager.PlayerHideAllUI(player);

        }

        object CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (Instance == null)
            {
                return null;
            }

            if (player == null)
            {
                return null;
            }

            if (container == null)
            {
                return null;
            }

            if (container.PrefabName != PREFAB_WOOD_STORAGE_BOX)
            {
                return null;
            }

            if (!IsEntityMarkedInternal(container))
            {
                return null;
            }

            DimensionalBox box;

            if (!DimensionalBox.ServerCacheBox.TryGetValue(container.net.ID.Value, out box))
            {
                return null;
            }

            if (!HasEnterExitPermission(player))
            {
                ToastPlayer(player, MSG(MSG_NO_PERMISSION_ENTER_EXIT, player.UserIDString));
                return true;
            }

            if (Instance.Configuration.CheckEscapeBlockWhenEnteringDimensions)
            {
                if (IsEscapeBlocked(player))
                {
                    ToastPlayer(player, MSG(MSG_CANT_ENTER_ESCAPE_BLOCKED, player.UserIDString));
                    return true;
                }
            }

            if (Instance.Configuration.DimensionalBoxesCheckBuildingBlockOutsideWhenEnteringDimension)
            {
                if (player.IsBuildingBlocked())
                {
                    ToastPlayer(player, MSG(MSG_CANT_ENTER_BUILDING_BLOCKED, player.UserIDString));
                    return true;
                }
            }

            if (Instance.Configuration.DimensionalBoxesCheckTCAuthInsideWhenEnteringDimension)
            {
                if (box.PairedPocket.IsPlayerBuildingBlockedOnFirstTcInsideIfThereIsAnyButOnlyIfAtLeast1Auth(player))
                {
                    ToastPlayer(player, MSG(MSG_CANT_ENTER_WITHOUT_TC_AUTH2, player.UserIDString));
                    return true;
                }
            }

            if (container.IsLocked())
            {
                player.ShowToast(GameTip.Styles.Red_Normal, StorageContainer.LockedMessage);
                return true;
            };

            if (!container.CanOpenLootPanel(player, container.panelName))
            {
                return true;
            };

            box.PlayerOpenedBox(player);

            //return non null
            return true;
        }

        object CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (Instance == null)
            {
                return null;
            }

            if (prefab.prefabID != PREFAB_ID_SMALL_WOODEN_BOX)
            {
                return null;
            }

            var player = planner.GetOwnerPlayer();

            if (player == null)
            {
                return null;
            }

            //ok, it's a small box. have you verified the item or what...?

            var item = planner.GetItem();

            //looking for existing and fresh

            bool isExisting = IsDimensionalBoxItem(item);
            bool isFresh = IsDimensionalBoxItem(item, true);

            if (!(isExisting || isFresh))
            {
                return null;
            }

            //now player!


            if (!HasDeployPickupPermission(player))
            {
                ToastPlayer(player, MSG(MSG_NO_PERM_DEPLOY_PICKUP, player.UserIDString));
                return true;
            }

            if (Instance.Configuration.CheckEscapeBlockWhenDeployingDimensions)
            {
                if (IsEscapeBlocked(player))
                {
                    ToastPlayer(player, MSG(MSG_ESCAPE_BLOCKED_CANT_DEPLOY, player.UserIDString));
                    return true;
                }
            }

            var targetPosition = target.entity && target.entity.transform && target.socket ? target.GetWorldPosition() : target.position;

            DimensionalPocket itemDimension = null;

            bool itemDimensionNotNull = false;

            if (isExisting)
            {
                var pocket = TryGetDimensionalPocketFromNonNullItem(item);

                if (pocket != null)
                {
                    itemDimension = pocket;
                    itemDimensionNotNull = true;
                }
            }

            CheckPositionResultDeep(targetPosition, itemDimension);

            //now check for any problems.
            //first, cannot deploy in itself.

            if (PositionCheckResult.BottomPocketFound != null)
            {
                if (Instance.Configuration.DimensionalPocketsMaxInceptionDepth == 0)
                {
                    ToastPlayer(player, MSG(MSG_CANT_DEPLOY_DIMENSIONS_INSIDE_DIMENSIONS, player.UserIDString));
                    return true;
                }


                //ok, we're trying to deploy a dimension inside a dimension.
                if (itemDimensionNotNull)
                {
                    //are you trying to deploy in yourself?!

                    //check if you short circuited
                    if (PositionCheckResult.ShortCircuitDetected)
                    {
                        ToastPlayer(player, MSG(MSG_CANT_DEPLOY_SHORT_CIRCUIT, player.UserIDString));
                        return true;
                    }

                    if (itemDimension == PositionCheckResult.BottomPocketFound)
                    {
                        ToastPlayer(player, MSG(MSG_CANT_DEPLOY_IN_ITSELF, player.UserIDString));
                        return true; //return non null
                    }

                    //what about deploying in undeployed dimensions?!
                    //should that be a config value?



                }

                //for depth, new dimensions should be checked too!

                if (PositionCheckResult.NoMoreDepthAllowed)
                {
                    ToastPlayer(player, MSG(MSG_CANT_DEPLOY_TOO_DEEP, player.UserIDString, Instance.Configuration.DimensionalPocketsMaxInceptionDepth));
                    return true;
                }
            }

            return null;
        }

        void OnEntityBuilt(Planner planner, GameObject gameObject)
        {
            if (Instance == null)
            {
                return;
            }

            var maybeDimensionalBoxItem = planner.GetItem();

            if (maybeDimensionalBoxItem == null)
            {
                return;
            }

            var maybeStorage = gameObject.GetComponent<StorageContainer>();

            if (maybeStorage == null)
            {
                return;
            }

            //here, only non-fresh matter
            if (!IsDimensionalBoxItem(maybeDimensionalBoxItem))
            {
                return;
            }

            MarkEntityAsIndestructible(maybeStorage);
            MarkEntityAsInternal(maybeStorage);

            var newDimensionalBox = maybeStorage.gameObject.AddComponent<DimensionalBox>();

            if (!newDimensionalBox.TryBuildFromItem(maybeDimensionalBoxItem))
            {
                var player = planner.GetOwnerPlayer();

                if (player == null)
                {
                    return;
                }

                ToastPlayer(player, MSG(MSG_DIMENSION_DOESNT_EXIST, player.UserIDString));
                maybeStorage.Invoke(() => maybeStorage.Kill(BaseNetworkable.DestroyMode.Gib), 0.01F);
            }
            
        }

        object CanPickupEntity(BasePlayer player, StorageContainer entity)
        {
            if (Instance == null)
            {
                return null;
            }

            if (player == null)
            {
                return null;
            }

            if (!IsEntityMarkedInternal(entity))
            {
                return null;
            }

            DimensionalBox box;

            if (!DimensionalBox.ServerCacheBox.TryGetValue(entity.net.ID.Value, out box))
            {
                return null;
            }

            if (!HasDeployPickupPermission(player))
            {
                ToastPlayer(player, MSG(MSG_NO_PERM_DEPLOY_PICKUP, player.UserIDString));
                return false;
            }

            if (Instance.Configuration.CheckEscapeBlockWhenPickingUpDimensions)
            {
                if (IsEscapeBlocked(player))
                {
                    ToastPlayer(player, MSG(MSG_ESCAPE_BLOCKED_CANT_PICKUP, player.UserIDString));
                    return false;
                }
            }

            var res = box.CanPickupBox(player);
            return res;
        }

        void OnEntityPickedUp(StorageContainer container, Item createdItem, BasePlayer player)
        {
            if (Instance == null)
            {
                return;
            }

            if (container.PrefabName != PREFAB_WOOD_STORAGE_BOX)
            {
                return;
            }

            if (!IsEntityMarkedInternal(container))
            {
                return;
            }

            DimensionalBox box;

            if (!DimensionalBox.ServerCacheBox.TryGetValue(container.net.ID.Value, out box))
            {
                return;
            }

            box.OnPickedUp(createdItem, player);
        }

        #endregion

        #region DIMENSIONAL PLAYER
        public class DimensionalPlayer : InternalBehaviour
        {
            public static Hash<ulong, DimensionalPlayer> ServerCachePlayer;
            public BasePlayer Player;
            public ulong SteamID;

            public Vector3 LastCheckedPosition;

            public bool WasFlyingLastTimeChecked;

            public static void PrepareStatic()
            {
                ServerCachePlayer = new Hash<ulong, DimensionalPlayer>();
            }

            public static void UnloadStatic()
            {
                ServerCachePlayer = null;
            }

            public void OnPlayerMajorUpdate(BasePlayer player)
            {
                Player = player;
                SteamID = Player.userID;

                LastCheckedPosition = Player.transform.position;
                WasFlyingLastTimeChecked = Player.IsFlying;

                if (!ServerCachePlayer.ContainsKey(SteamID))
                {
                    ServerCachePlayer.Add(SteamID, this);
                }                

                PositionChangeUpdate(true);

                if (!Player.IsInvoking(PositionChangeUpdate))
                {
                    Player.InvokeRandomized(PositionChangeUpdate, 0.5F, 0.5F, 0.1F);
                }
            }

            public void PositionChangeUpdate() => PositionChangeUpdate(false);

            public void PositionChangeUpdate(bool whenPreparing = false)
            {
                if (Player.HasPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot))
                {
                    return;
                }

                //when preparing, force an update anyway

                if (!whenPreparing)
                {
                    if (Player.transform.position == LastCheckedPosition)
                    {
                        return;
                    }

                    if (WasFlyingLastTimeChecked || Player.IsFlying)
                    {
                        LastCheckedPosition = Player.transform.position;
                        WasFlyingLastTimeChecked = Player.IsFlying;

                        return;
                    }
                }


                //position changed to something different.

                //check if the new position is inside the range for pocket dimensions.

                //bool wasInDimensionLayerBefore = PositionWithinDimensionLayer(LastCheckedPosition);
                bool isInDimensionLayerNow = PositionWithinDimensionLayer(Player.transform.position);

                if (isInDimensionLayerNow)
                {
                    bool needTeleportBack = false;
                    string reasonForTeleportingBack = default(string);

                    //yes, this is in the range.

                    //check how much it changed.

                    var distanceTraversed = Vector3.Distance(LastCheckedPosition, Player.transform.position);

                    if (distanceTraversed > 40F)
                    {
                        //that looks like a teleport to a dimension above.
                        //do a raycast down, 10 meters max.
                        //if you encounter absolutely nothing, you need to be teleported back.

                        //by "something" we mean a decay entity.

                        CheckPositionResultDeep(Player.transform.position);

                        if (PositionCheckResult.NothingAround)
                        {
                            //absolutely nothing found. We need to teleport the player back.
                            needTeleportBack = true;
                            reasonForTeleportingBack = MSG(MSG_DIMENSION_DOESNT_EXIST, Player.UserIDString);
                        }
                        else
                        {
                            if (PositionCheckResult.BottomPocketFound != null)
                            {
                                if (!PositionCheckResult.PocketIsDeployed)
                                {
                                    needTeleportBack = true;
                                    reasonForTeleportingBack = MSG(MSG_CANT_TELEPORT_INSIDE_NON_DEPLOYED, Player.UserIDString);
                                }

                            }
                        }
                    }

                    if (needTeleportBack)
                    {
                        TeleportPlayerTo(Player, LastCheckedPosition);
                        ToastPlayer(Player, reasonForTeleportingBack);
                    }
                }


                LastCheckedPosition = Player.transform.position;
                WasFlyingLastTimeChecked = Player.IsFlying;



            }

            void OnDestroy()
            {
                if (Player != null)
                {
                    Player.CancelInvoke(PositionChangeUpdate);
                }

                ServerCachePlayer.Remove(SteamID);
            }

        }

        #endregion

        #region LANG

        public const string MSG_POCKET_DIMENSION_ITEM_NAME = nameof(MSG_POCKET_DIMENSION_ITEM_NAME);

        public const string MSG_CONVERT_COST_CURRENCY_NAME_SERVER_REWARDS = nameof(MSG_CONVERT_COST_CURRENCY_NAME_SERVER_REWARDS);
        public const string MSG_CONVERT_COST_CURRENCY_NAME_ECONOMICS = nameof(MSG_CONVERT_COST_CURRENCY_NAME_ECONOMICS);
        public const string MSG_CONVERT_COST_LABEL_TEXT_AND_FORMAT = nameof(MSG_CONVERT_COST_LABEL_TEXT_AND_FORMAT);

        public const string MSG_CONVERT_BUTTON_CONVERT = nameof(MSG_CONVERT_BUTTON_CONVERT);
        public const string MSG_CONVERT_TEST_FAILED = nameof(MSG_CONVERT_TEST_FAILED);

        public const string MSG_CONVERTED_SUCCESSFULLY = nameof(MSG_CONVERTED_SUCCESSFULLY);

        public const string MSG_CONVERT_COST_CANNOT_AFFORD = nameof(MSG_CONVERT_COST_CANNOT_AFFORD);
        public const string MSG_CONVERT_COST_FORMAT_SERVER_REWARDS = nameof(MSG_CONVERT_COST_FORMAT_SERVER_REWARDS);
        public const string MSG_CONVERT_COST_FORMAT_ECONOMICS = nameof(MSG_CONVERT_COST_FORMAT_ECONOMICS);
        public const string MSG_CONVERT_COST_FORMAT_CUSTOM_ITEM_AMOUNT = nameof(MSG_CONVERT_COST_FORMAT_CUSTOM_ITEM_AMOUNT);

        public const string MSG_CONVERT_COST_MISSING_PLUGIN = nameof(MSG_CONVERT_COST_MISSING_PLUGIN);

        public const string MSG_COMMAND_FORBIDDEN_INSIDE = nameof(MSG_COMMAND_FORBIDDEN_INSIDE);

        public const string MSG_NO_PERMISSION_ENTER_EXIT = nameof(MSG_NO_PERMISSION_ENTER_EXIT);
        public const string MSG_NO_PERM_DEPLOY_PICKUP = nameof(MSG_NO_PERM_DEPLOY_PICKUP);
        public const string MSG_NO_CONVERT_PERMISSION = nameof(MSG_NO_CONVERT_PERMISSION);
        public const string MSG_DIMENSION_DOESNT_EXIST = nameof(MSG_DIMENSION_DOESNT_EXIST);

        public const string MSG_CANT_RESPAWN_INSIDE_NON_DEPLOYED = nameof(MSG_CANT_RESPAWN_INSIDE_NON_DEPLOYED);
        public const string MSG_CANT_TELEPORT_INSIDE_NON_DEPLOYED = nameof(MSG_CANT_TELEPORT_INSIDE_NON_DEPLOYED);
        public const string MSG_CANT_DEPLOY_IN_ITSELF = nameof(MSG_CANT_DEPLOY_IN_ITSELF);
        public const string MSG_CANT_DEPLOY_SHORT_CIRCUIT = nameof(MSG_CANT_DEPLOY_SHORT_CIRCUIT);

        public const string MSG_CANT_DEPLOY_DIMENSIONS_INSIDE_DIMENSIONS = nameof(MSG_CANT_DEPLOY_DIMENSIONS_INSIDE_DIMENSIONS);

        public const string MSG_CANT_DEPLOY_TOO_DEEP = nameof(MSG_CANT_DEPLOY_TOO_DEEP);

        public const string MSG_CANT_PICK_UP_WITH_PLAYERS_INSIDE = nameof(MSG_CANT_PICK_UP_WITH_PLAYERS_INSIDE);
        public const string MSG_CANT_PICK_UP_WITHOUT_TC_AUTH2 = nameof(MSG_CANT_PICK_UP_WITHOUT_TC_AUTH2);
        public const string MSG_CANT_HANDLE_WITHOUT_TC_AUTH2 = nameof(MSG_CANT_HANDLE_WITHOUT_TC_AUTH2);

        public const string MSG_CANT_GET_OUT_STUCK = nameof(MSG_CANT_GET_OUT_STUCK);
        public const string MSG_CANT_ENTER_ESCAPE_BLOCKED = nameof(MSG_CANT_ENTER_ESCAPE_BLOCKED);

        public const string MSG_CANT_ENTER_BUILDING_BLOCKED = nameof(MSG_CANT_ENTER_BUILDING_BLOCKED);
        public const string MSG_CANT_ENTER_WITHOUT_TC_AUTH2 = nameof(MSG_CANT_ENTER_WITHOUT_TC_AUTH2);

        public const string MSG_ESCAPE_BLOCKED_CANT_EXIT = nameof(MSG_ESCAPE_BLOCKED_CANT_EXIT);
        public const string MSG_ESCAPE_BLOCKED_CANT_DEPLOY = nameof(MSG_ESCAPE_BLOCKED_CANT_DEPLOY);
        public const string MSG_ESCAPE_BLOCKED_CANT_PICKUP = nameof(MSG_ESCAPE_BLOCKED_CANT_PICKUP);

        public const string MSG_VIABILITY_OK = nameof(MSG_VIABILITY_OK);
        public const string MSG_VIABILITY_TOO_MANY_TCS = nameof(MSG_VIABILITY_TOO_MANY_TCS);
        public const string MSG_VIABILITY_NO_BUILDING_BLOCKS = nameof(MSG_VIABILITY_NO_BUILDING_BLOCKS);
        public const string MSG_VIABILITY_ONLY_SQUARE_FOUNDATIONS = nameof(MSG_VIABILITY_ONLY_SQUARE_FOUNDATIONS);
        public const string MSG_VIABILITY_MUST_HAVE_9_FOUNDATIONS = nameof(MSG_VIABILITY_MUST_HAVE_9_FOUNDATIONS);
        public const string MSG_VIABILITY_MUST_HAVE_AT_LEAST_9_FLOORS = nameof(MSG_VIABILITY_MUST_HAVE_AT_LEAST_9_FLOORS);
        public const string MSG_VIABILITY_MUST_HAVE_1_DOORWAY = nameof(MSG_VIABILITY_MUST_HAVE_1_DOORWAY);
        public const string MSG_VIABILITY_MUST_HAVE_AT_LEAST_35_WALLS = nameof(MSG_VIABILITY_MUST_HAVE_AT_LEAST_35_WALLS);
        public const string MSG_VIABILITY_FOUNDATIONS_MUST_BE_SAME_LEVEL = nameof(MSG_VIABILITY_FOUNDATIONS_MUST_BE_SAME_LEVEL);
        public const string MSG_VIABILITY_CEILING_MISSING = nameof(MSG_VIABILITY_CEILING_MISSING);

        public const string MSG_VIABILITY_FOUNDATIONS_MUST_MATCH_CEILINGS = nameof(MSG_VIABILITY_FOUNDATIONS_MUST_MATCH_CEILINGS);
        public const string MSG_VIABILITY_ALREADY_POCKET_DIMENSION = nameof(MSG_VIABILITY_ALREADY_POCKET_DIMENSION);
        public const string MSG_VIABILITY_WALL_MISSING_AT_LEVEL = nameof(MSG_VIABILITY_WALL_MISSING_AT_LEVEL);
        public const string MSG_VIABILITY_TOO_MANY_DOORWAYS = nameof(MSG_VIABILITY_TOO_MANY_DOORWAYS);
        public const string MSG_VIABILITY_NO_EXTERNAL_DOORWAYS_FOUND = nameof(MSG_VIABILITY_NO_EXTERNAL_DOORWAYS_FOUND);
        public const string MSG_VIABILITY_NO_SOLAR_OR_WINDMILL_ALLOWED = nameof(MSG_VIABILITY_NO_SOLAR_OR_WINDMILL_ALLOWED);
        public const string MSG_VIABILITY_NO_TEST_GENERATORS_ALLOWED = nameof(MSG_VIABILITY_NO_TEST_GENERATORS_ALLOWED);
        public const string MSG_VIABILITY_IO_CONNECTION_OUTSIDE_CUBE = nameof(MSG_VIABILITY_IO_CONNECTION_OUTSIDE_CUBE);
        public const string MSG_VIABILITY_MORE_ENTITIES_IN_VOLUME_THAT_BUILDING2 = nameof(MSG_VIABILITY_MORE_ENTITIES_IN_VOLUME_THAT_BUILDING2);
        public const string MSG_VIABILITY_LESS_ENTITIES_IN_VOLUME_THAT_BUILDING2 = nameof(MSG_VIABILITY_LESS_ENTITIES_IN_VOLUME_THAT_BUILDING2);
        public const string MSG_VIABILITY_SOMETHING_STICKING_OUT = nameof(MSG_VIABILITY_SOMETHING_STICKING_OUT);
        public const string MSG_VIABILITY_DOOR_FOUND_IN_EXTERNAL_DOORWAY = nameof(MSG_VIABILITY_DOOR_FOUND_IN_EXTERNAL_DOORWAY);
        public const string MSG_VIABILITY_SOMETHING_IN_FRONT_OF_DOORWAY = nameof(MSG_VIABILITY_SOMETHING_IN_FRONT_OF_DOORWAY);
        public const string MSG_VIABILITY_WATERBASES_NOT_SUPPORTED_YET = nameof(MSG_VIABILITY_WATERBASES_NOT_SUPPORTED_YET);

        public const string MSG_NO_VENDING_MACHINE_BROADCASTING_IN_POCKETS = nameof(MSG_NO_VENDING_MACHINE_BROADCASTING_IN_POCKETS);


        public const string MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE = nameof(MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE);
        public const string MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE_TC_MISSING = nameof(MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE_TC_MISSING);

        public const string MSG_REQUEST_DEPLOY_SUCCESS = nameof(MSG_REQUEST_DEPLOY_SUCCESS);
        public const string MSG_REQUEST_TIMEOUT = nameof(MSG_REQUEST_TIMEOUT);
        public const string MSG_REQUEST_CONTAINER_NULL = nameof(MSG_REQUEST_CONTAINER_NULL);
        public const string MSG_REQUEST_PLAYER_NULL = nameof(MSG_REQUEST_PLAYER_NULL);
        public const string MSG_REQUEST_MISSING_ENTRY = nameof(MSG_REQUEST_MISSING_ENTRY);

        public const string MSG_VOLUME_OUTSIDE_KILLS_PLAYERS = nameof(MSG_VOLUME_OUTSIDE_KILLS_PLAYERS);
        public const string MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_BOX = nameof(MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_BOX);
        public const string MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_PORTAL = nameof(MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_PORTAL);
        public const string MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_RANDOM = nameof(MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_RANDOM);


        public const string MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_REQUIRES_PLAYER = nameof(MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_REQUIRES_PLAYER);

        public const string MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_RAYCAST_FAIL = nameof(MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_RAYCAST_FAIL);


        public const string MSG_CHECK_COMMON_NOT_LOOKING_AT_TC = nameof(MSG_CHECK_COMMON_NOT_LOOKING_AT_TC);

        public const string MSG_REPLACE_USAGE = nameof(MSG_REPLACE_USAGE);
        public const string MSG_REPLACE_USAGE_PREFAB_NOT_FOUND = nameof(MSG_REPLACE_USAGE_PREFAB_NOT_FOUND);

        public const string MSG_NO_PERMISSION_REPLACE = nameof(MSG_NO_PERMISSION_REPLACE);

        public const string MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_BUILDING_BLOCKED = nameof(MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_BUILDING_BLOCKED);

        public const string MSG_REPLACE_NOT_LOOKING_AT_BUILDING_BLOCK = nameof(MSG_REPLACE_NOT_LOOKING_AT_BUILDING_BLOCK);

        public const string MSG_REPLACE_NOT_LOOKING_AT_TOP_OR_SIDE = nameof(MSG_REPLACE_NOT_LOOKING_AT_TOP_OR_SIDE);

        public const string MSG_REPLACE_NOT_EXTERNAL_SHELL_BLOCK = nameof(MSG_REPLACE_NOT_EXTERNAL_SHELL_BLOCK);

        public const string MSG_REPLACE_SHELL_BLOCK_ALREADY_THAT = nameof(MSG_REPLACE_SHELL_BLOCK_ALREADY_THAT);

        public const string MSG_REPLACE_SOMETHING_INSIDE_SOCKET = nameof(MSG_REPLACE_SOMETHING_INSIDE_SOCKET);

        public const string MSG_REPLACE_INVALID_REPLACEMENT = nameof(MSG_REPLACE_INVALID_REPLACEMENT);

        private static Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            [MSG_POCKET_DIMENSION_ITEM_NAME] = "Pocket Dimension",

            [MSG_CONVERT_COST_CURRENCY_NAME_ECONOMICS] = "¤",
            [MSG_CONVERT_COST_CURRENCY_NAME_SERVER_REWARDS] = "RP",
            [MSG_CONVERT_COST_FORMAT_SERVER_REWARDS] = "{0} {1}",
            [MSG_CONVERT_COST_FORMAT_CUSTOM_ITEM_AMOUNT] = "{1} x {0}",
            [MSG_CONVERT_COST_FORMAT_ECONOMICS] = "{1}{0}",

            [MSG_CONVERT_COST_LABEL_TEXT_AND_FORMAT] = "Conversion cost: {0}",

            [MSG_CONVERT_COST_CANNOT_AFFORD] = "You can't afford to convert this base.",

            [MSG_CONVERT_COST_MISSING_PLUGIN] = "The {0} plugin is not loaded",

            [MSG_CONVERT_BUTTON_CONVERT] = "Convert to Pocket Dimension",
            [MSG_CONVERT_TEST_FAILED] = "Cannot convert: {0}",

            [MSG_CONVERTED_SUCCESSFULLY] = "Succesfully converted the base into a Pocket Dimension",

            [MSG_COMMAND_FORBIDDEN_INSIDE] = "You cannot use the command \"{0}\" inside a Pocket Dimension",

            [MSG_NO_PERMISSION_ENTER_EXIT] = "You don't have the permission to enter or exit Pocket Dimensions",
            [MSG_NO_PERM_DEPLOY_PICKUP] = "You don't have the permission to deploy or pick up Pocket Dimensions",
            [MSG_NO_CONVERT_PERMISSION] = "You don't have the permission to convert bases into Pocket Dimensions",

            [MSG_DIMENSION_DOESNT_EXIST] = "This Pocket Dimension was destroyed or decayed away",
            [MSG_CANT_RESPAWN_INSIDE_NON_DEPLOYED] = "Cannot respawn inside non-deployed Pocket Dimensions",
            [MSG_CANT_TELEPORT_INSIDE_NON_DEPLOYED] = "Cannot teleport inside non-deployed Pocket Dimensions",
            [MSG_CANT_DEPLOY_IN_ITSELF] = "Cannot deploy a Pocket Dimension inside of itself",
            [MSG_CANT_DEPLOY_SHORT_CIRCUIT] = "Cannot deploy Pocket Dimensions looping back on themselves",
            [MSG_CANT_DEPLOY_DIMENSIONS_INSIDE_DIMENSIONS] = "Cannot deploy Pocket Dimensions inside Pocket Dimensions",
            [MSG_CANT_DEPLOY_TOO_DEEP] = "Cannot deploy Pocket Dimensions more than {0} levels deep",

            [MSG_CANT_PICK_UP_WITH_PLAYERS_INSIDE] = "You can't pick up Pocket Dimension Boxes with players inside of it",
            [MSG_CANT_PICK_UP_WITHOUT_TC_AUTH2] = "You can't pick up this Pocket Dimension Box, you're not authorized on its TC inside (and at least one other player is)",
            [MSG_CANT_HANDLE_WITHOUT_TC_AUTH2] = "You can't handle this Pocket Dimension Item, you're not authorized on its TC inside (and at least one other player is)",
            [MSG_CANT_ENTER_WITHOUT_TC_AUTH2] = "You can't enter this Pocket Dimension, you're not authorized on its TC inside (and at least one other player is)",

            [MSG_CANT_GET_OUT_STUCK] = "The box has been picked up, you're currently stuck here",
            [MSG_CANT_ENTER_ESCAPE_BLOCKED] = "You can't enter a Pocket Dimension while you're Escape Blocked",
            [MSG_ESCAPE_BLOCKED_CANT_EXIT] = "You can't exit a Pocket Dimension while you're Escape Blocked",

            [MSG_ESCAPE_BLOCKED_CANT_DEPLOY] = "You can't deploy a Pocket Dimension while you're Escape Blocked",
            [MSG_ESCAPE_BLOCKED_CANT_PICKUP] = "You can't pickup a Pocket Dimension while you're Escape Blocked",

            [MSG_CANT_ENTER_BUILDING_BLOCKED] = "You can't enter a Pocket Dimension while you're building blocked",


            [MSG_VIABILITY_OK] = "OK",
            [MSG_VIABILITY_TOO_MANY_TCS] = "Too many TCs, only 1 allowed",
            [MSG_VIABILITY_NO_BUILDING_BLOCKS] = "No building blocks found",
            [MSG_VIABILITY_ONLY_SQUARE_FOUNDATIONS] = "Only square foundations allowed",
            [MSG_VIABILITY_MUST_HAVE_9_FOUNDATIONS] = "Must be exactly 9 square foundations, {0} found",
            [MSG_VIABILITY_MUST_HAVE_AT_LEAST_9_FLOORS] = "Must have at least 9 floors, for the ceiling. Counted {0}",
            [MSG_VIABILITY_MUST_HAVE_1_DOORWAY] = "Must have at least 1 Doorway. Counted 0",
            [MSG_VIABILITY_MUST_HAVE_AT_LEAST_35_WALLS] = "Must have at least 35 walls. Counted {0}",
            [MSG_VIABILITY_FOUNDATIONS_MUST_BE_SAME_LEVEL] = "All foundations must be at the same level",
            [MSG_VIABILITY_CEILING_MISSING] = "A ceiling is missing above one of the foundations",


            [MSG_VIABILITY_FOUNDATIONS_MUST_MATCH_CEILINGS] = "9 foundations and 9 ceilings must be placed in a 3x3 pattern",
            [MSG_VIABILITY_ALREADY_POCKET_DIMENSION] = "Looks like this is already a Pocket Dimension",
            [MSG_VIABILITY_WALL_MISSING_AT_LEVEL] = "External wall/doorway missing at level {0}",
            [MSG_VIABILITY_TOO_MANY_DOORWAYS] = "Too many external doorways, only one allowed total. Counted {0}",
            [MSG_VIABILITY_NO_EXTERNAL_DOORWAYS_FOUND] = "No external doorways found, need exactly 1",
            [MSG_VIABILITY_NO_SOLAR_OR_WINDMILL_ALLOWED] = "Solar Panels or Windmills are not allowed on potential Pocket Dimensions",
            [MSG_VIABILITY_NO_TEST_GENERATORS_ALLOWED] = "Test Generators are not allowed on potential Pocket Dimensions",
            [MSG_VIABILITY_IO_CONNECTION_OUTSIDE_CUBE] = "Connected to an IO Entity outside of the 3 by 3 cube",
            [MSG_VIABILITY_MORE_ENTITIES_IN_VOLUME_THAT_BUILDING2] = "More entities ({0}) inside volume than building ({1}): {2}",
            [MSG_VIABILITY_LESS_ENTITIES_IN_VOLUME_THAT_BUILDING2] = "Less entities ({0}) inside volume than building ({1}): {2}",
            [MSG_VIABILITY_SOMETHING_STICKING_OUT] = "A structure is sticking out of the 3x3x3 cubic volume",
            [MSG_VIABILITY_DOOR_FOUND_IN_EXTERNAL_DOORWAY] = "The external doorway cannot have any doors (or vending machines) installed",
            [MSG_VIABILITY_SOMETHING_IN_FRONT_OF_DOORWAY] = "Something tall is obstructing the space in front of the external doorway",
            [MSG_VIABILITY_WATERBASES_NOT_SUPPORTED_YET] = "Converting Water Bases is not supported yet",

            [MSG_NO_VENDING_MACHINE_BROADCASTING_IN_POCKETS] = "Vending Machine broadcasting inside Pocket Dimensions is not allowed",

            [MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE] = "Invalid CopyPaste structure for file \"{0}\": {1}",
            [MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE_TC_MISSING] = "Tool Cupboard is missing",

            [MSG_REQUEST_DEPLOY_SUCCESS] = "Successfully deployed a new Pocket Dimension",
            [MSG_REQUEST_TIMEOUT] = "Deploy request from CopyPaste has timed out",
            [MSG_REQUEST_CONTAINER_NULL] = "Container that spawned is now null",
            [MSG_REQUEST_PLAYER_NULL] = "The requesting player is now null",
            [MSG_REQUEST_MISSING_ENTRY] = "The configuration is missing an entry for skin {0}!",

            [MSG_VOLUME_OUTSIDE_KILLS_PLAYERS] = "You have been killed by the inter-dimensional vacuum. Try staying inside your dimension bounds next time. You will find your corpse at {0}",

            [MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_BOX] = "the location of the box containing that dimension.",
            [MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_PORTAL] = "the location of the exit portal of that dimension.",

            [MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_RANDOM] = "a random spawn point on the map, look for the death marker.",

            [MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_REQUIRES_PLAYER] = "This command must be ran by a valid player in-game.",

            [MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_RAYCAST_FAIL] = "You're not looking at anything at all.",

            [MSG_CHECK_COMMON_NOT_LOOKING_AT_TC] = "You're not looking at a Tool Cupboard.",

            [MSG_REPLACE_USAGE] = "Type `/" + PREFIX_SHORT + CMD_REPLACE + " [prefab name]` while looking at an external shell block.\nFor sides:\n{0}\nFor tops:\n{1}",

            [MSG_REPLACE_USAGE_PREFAB_NOT_FOUND] = "Wrong replacement prefab name. Use one of the following from the list below.\nFor sides:\n{0}\nFor tops:\n{1}",

            [MSG_NO_PERMISSION_REPLACE] = "You don't have the permission to replace external shell blocks.",

            [MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_BUILDING_BLOCKED] = "You're building blocked, you cannot use this command right now.",

            [MSG_REPLACE_NOT_LOOKING_AT_BUILDING_BLOCK] = "You're not looking at a building block.",

            [MSG_REPLACE_NOT_LOOKING_AT_TOP_OR_SIDE] = "You're not looking at a valid building block. It must be a normal wall / wall frame / window frame (for sides), or a square floor / square floor frame (for tops).",

            [MSG_REPLACE_NOT_EXTERNAL_SHELL_BLOCK] = "This building block is not one of the external shell blocks in this Pocket Dimension.",

            [MSG_REPLACE_SHELL_BLOCK_ALREADY_THAT] = "This external shell block is already a `{0}`! Pick another prefab to replace it with.",

            [MSG_REPLACE_SOMETHING_INSIDE_SOCKET] = "This external shell block `{0}` has something deployed in/on it inside socket `{1}`, please pick it up or destroy it first. ",

            [MSG_REPLACE_INVALID_REPLACEMENT] = "Invalid replacement prefab name for this external shell block.",
        };

        private static string MSG(string msg, string userID = null, params object[] args)
        {
            if (args == null)
            {
                return Instance.lang.GetMessage(msg, Instance, userID);
            }
            else
            {
                return string.Format(Instance.lang.GetMessage(msg, Instance, userID), args);
            }

        }
        #endregion

        #region GUI
        public class ColorDefinition
        {
            public Color UnityColor;
            public string UnityString;

            public string HexString;

            public float R;
            public float G;
            public float B;

            public string UnityStringWithAlpha(float alpha)
            {
                return string.Format("{0} {1}", UnityString, alpha);
            }

            public static ColorDefinition FromHex(string hex)
            {
                hex = hex.ToUpper();

                //get rid of it
                hex = hex.Replace("#", "");

                var newColorDef = new ColorDefinition
                {
                    R = (float)short.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255,
                    G = (float)short.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255,
                    B = (float)short.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255
                };

                //and now force it back
                hex = "#" + hex;

                newColorDef.HexString = hex;
                newColorDef.UnityString = string.Format("{0} {1} {2}", newColorDef.R, newColorDef.G, newColorDef.B);
                newColorDef.UnityColor = new Color(newColorDef.R, newColorDef.G, newColorDef.B);

                return newColorDef;
            }
        }

        public static class ConversionGuiManager
        {
            public static string ContainerButtonJSON;
            public static string ContainerToastJSON;
            public static string ContainerCostJSON;

            public const string ARG_NETID = "[NETID]";
            public const string ARG_LANG = "[LANG]";
            public const string ARG_TOAST = "[TOAST]";

            public const float GUI_FADE = 0.125F;

            public const string GUI_COLOR_PURPLISH = "#fe80fe";
            public const string GUI_COLOR_RUSTISH = "#f7ebe1";
            public const string GUI_COLOR_REDDISH = "#ce422b";

            public const string CUI_NAME_BUTTON_AND_CMD = "pd.gui.convert";

            public const string CUI_NAME_TOAST_BG = "pd.gui.toast.bg";

            public const string CUI_NAME_TOAST_TEXT = "pd.gui.toast.text";

            public const string CUI_NAME_COST = "pd.gui.cost";

            public const string CUI_SPRITE = "Assets/Content/UI/UI.Background.TileTex.psd";

            public static string FinalCurrencyUnitName;
            public static string FinalCurrencyFormat;

            public static void Initialize()
            {
                var anchorButtonXYString = $"{Instance.Configuration.TCGuiButtonAnchorX} {Instance.Configuration.TCGuiButtonAnchorY}";
                var offsetButtonMinString = $"{Instance.Configuration.TCGuiButtonOffsetXMin} {Instance.Configuration.TCGuiButtonOffsetYMin}";
                var offsetButtonMaxString = $"{Instance.Configuration.TCGuiButtonOffsetXMax} {Instance.Configuration.TCGuiButtonOffsetYMax}";

                var containerButton = new CuiElementContainer();

                var button = new CuiButton
                {
                    FadeOut = GUI_FADE,
                    RectTransform =
                    {
                        AnchorMin = anchorButtonXYString,
                        AnchorMax = anchorButtonXYString,
                        OffsetMin = offsetButtonMinString,
                        OffsetMax = offsetButtonMaxString
                    },
                    Button =
                    {
                        Sprite = CUI_SPRITE,
                        Command = $"{CUI_NAME_BUTTON_AND_CMD} {ARG_NETID}",
                        FadeIn = GUI_FADE,
                        Color = ColorDefinition.FromHex(Instance.Configuration.TCGuiButtonBgColor).UnityStringWithAlpha(Instance.Configuration.TCGuiButtonBgAlpha),
                    },
                    Text =
                    {
                        Align = Instance.Configuration.TCGuiButtonTextAlign,
                        FontSize = Instance.Configuration.TCGuiButtonTextSize,
                        Color = ColorDefinition.FromHex(Instance.Configuration.TCGuiButtonTextColor).UnityStringWithAlpha(Instance.Configuration.TCGuiButtonTextAlpha),
                        FadeIn = GUI_FADE,
                        Text = ARG_LANG
                    }
                };

                containerButton.Add(button, "Overlay", CUI_NAME_BUTTON_AND_CMD);

                ContainerButtonJSON = containerButton.ToJson();



                //

                var anchorToastXYString = $"{Instance.Configuration.TCGuiToastAnchorX} {Instance.Configuration.TCGuiToastAnchorY}";

                var offsetToastBGMinString = $"{Instance.Configuration.TCGuiToastOffsetXMin} {Instance.Configuration.TCGuiToastOffsetYMin}";
                var offsetToastBGMaxString = $"{Instance.Configuration.TCGuiToastOffsetXMax} {Instance.Configuration.TCGuiToastOffsetYMax}";

                var offsetToastTextMinString = $"{Instance.Configuration.TCGuiToastOffsetXMin + Instance.Configuration.TCGuiToastTextPadding} {Instance.Configuration.TCGuiToastOffsetYMin + Instance.Configuration.TCGuiToastTextPadding}";
                var offsetToastTextMaxString = $"{Instance.Configuration.TCGuiToastOffsetXMax - Instance.Configuration.TCGuiToastTextPadding} {Instance.Configuration.TCGuiToastOffsetYMax - Instance.Configuration.TCGuiToastTextPadding}";

                var containerToast = new CuiElementContainer();

                var background = new CuiElement
                {
                    Name = CUI_NAME_TOAST_BG,
                    FadeOut = GUI_FADE,
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Sprite = CUI_SPRITE,
                            FadeIn = GUI_FADE,
                            Color = ColorDefinition.FromHex(Instance.Configuration.TCGuiToastBgColor).UnityStringWithAlpha(Instance.Configuration.TCGuiToastBgAlpha),
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchorToastXYString,
                            AnchorMax = anchorToastXYString,
                            OffsetMin = offsetToastBGMinString,
                            OffsetMax = offsetToastBGMaxString
                        }
                    }
                };

                var text = new CuiElement
                {
                    Name = CUI_NAME_TOAST_TEXT,
                    FadeOut = GUI_FADE,
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Align = Instance.Configuration.TCGuiToastTextAlign,
                            FontSize = Instance.Configuration.TCGuiToastTextSize,
                            Color = ColorDefinition.FromHex(Instance.Configuration.TCGuiToastTextColor).UnityStringWithAlpha(Instance.Configuration.TCGuiToastTextAlpha),
                            FadeIn = GUI_FADE,
                            Text = ARG_TOAST
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchorToastXYString,
                            AnchorMax = anchorToastXYString,
                            OffsetMin = offsetToastTextMinString,
                            OffsetMax = offsetToastTextMaxString
                        }
                    }
                };

                containerToast.Add(background);
                containerToast.Add(text);


                ContainerToastJSON = containerToast.ToJson();


                //

                if (Instance.Configuration.ConversionCostCurrencyUsed == ConversionCurrencyType.NoCost)
                {
                    return;
                }

                var anchorCostXYString = $"{Instance.Configuration.TCGuiCostAnchorX} {Instance.Configuration.TCGuiCostAnchorY}";

                var offsetCostMinString = $"{Instance.Configuration.TCGuiCostOffsetXMin} {Instance.Configuration.TCGuiCostOffsetYMin}";
                var offsetCostMaxString = $"{Instance.Configuration.TCGuiCostOffsetXMax} {Instance.Configuration.TCGuiCostOffsetYMax}";

                switch (Instance.Configuration.ConversionCostCurrencyUsed)
                {
                    case ConversionCurrencyType.CustomItemAmount:
                        {
                            FinalCurrencyFormat = MSG(MSG_CONVERT_COST_FORMAT_CUSTOM_ITEM_AMOUNT, null, null);

                            if (!Instance.Configuration.ConversionCostCustomItemOverrideName.IsNullOrEmpty())
                            {
                                FinalCurrencyUnitName = Instance.Configuration.ConversionCostCustomItemOverrideName;
                            }
                            else
                            {
                                FinalCurrencyUnitName = Instance.Configuration.CachedCustomCostItemDef.displayName.translated;
                            }
                            
                        }
                        break;
                    case ConversionCurrencyType.ServerRewards:
                        {
                            FinalCurrencyFormat = MSG(MSG_CONVERT_COST_FORMAT_SERVER_REWARDS, null, null);
                            FinalCurrencyUnitName = MSG(MSG_CONVERT_COST_CURRENCY_NAME_SERVER_REWARDS);
                        }
                        break;
                    case ConversionCurrencyType.Economics:
                        {
                            FinalCurrencyFormat = MSG(MSG_CONVERT_COST_FORMAT_ECONOMICS, null, null);
                            FinalCurrencyUnitName = MSG(MSG_CONVERT_COST_CURRENCY_NAME_ECONOMICS);
                        }
                        break;
                }

                var containerCost = new CuiElementContainer();

                var cost = new CuiElement
                {
                    Name = CUI_NAME_COST,
                    FadeOut = GUI_FADE,
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Align = Instance.Configuration.TCGuiCostTextAlign,
                            FontSize = Instance.Configuration.TCGuiCostTextSize,
                            Color = ColorDefinition.FromHex(Instance.Configuration.TCGuiCostTextColor).UnityStringWithAlpha(Instance.Configuration.TCGuiCostTextAlpha),
                            FadeIn = GUI_FADE,
                            Text = MSG(MSG_CONVERT_COST_LABEL_TEXT_AND_FORMAT, null, string.Format(FinalCurrencyFormat, Instance.Configuration.ConversionCostAmount, FinalCurrencyUnitName)),
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = anchorCostXYString,
                            AnchorMax = anchorCostXYString,
                            OffsetMin = offsetCostMinString,
                            OffsetMax = offsetCostMaxString
                        }
                    }
                };

                containerCost.Add(cost);

                ContainerCostJSON = containerCost.ToJson();

            }

            public static void Cleanup()
            {
                FinalCurrencyFormat = null;
                FinalCurrencyUnitName = null;
                ContainerButtonJSON = null;
                ContainerToastJSON = null;
                ContainerCostJSON = null;
            }

            public static void PlayerShowTCToast(BasePlayer player, string toast)
            {
                PlayerHideTCToast(player);

                player.SetPlayerFlag(FLAG_TOAST_TIMEOUT, true);

                CuiHelper.AddUi(player, ContainerToastJSON.Replace(ARG_TOAST, toast));

                player.Invoke(() =>
                {
                    player.SetPlayerFlag(FLAG_TOAST_TIMEOUT, false);

                    PlayerHideTCToast(player);

                }, Instance.Configuration.TCGuiToastDuration);
            }

            public static void PlayerHideTCToast(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, CUI_NAME_TOAST_BG);
                CuiHelper.DestroyUi(player, CUI_NAME_TOAST_TEXT);
            }

            public static void PlayerOpenTC(BasePlayer player, BuildingPrivlidge tc)
            {
                CuiHelper.AddUi(player, ContainerButtonJSON.Replace(ARG_NETID, tc.net.ID.Value.ToString()).Replace(ARG_LANG, MSG(MSG_CONVERT_BUTTON_CONVERT, player.UserIDString)));

                if (Instance.Configuration.ConversionCostCurrencyUsed == ConversionCurrencyType.NoCost)
                {
                    return;
                }

                CuiHelper.AddUi(player, ContainerCostJSON);
            }

            public static void PlayerCloseTC(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, CUI_NAME_BUTTON_AND_CMD);
                CuiHelper.DestroyUi(player, CUI_NAME_COST);
            }

            public static void PlayerHideAllUI(BasePlayer player)
            {
                PlayerCloseTC(player);
                PlayerHideTCToast(player);
            }

            public static void PlayerPressConvertButton(BasePlayer player, ulong tcNetID)
            {

                if (!HasConvertTCPermission(player))
                {
                    PlayerShowTCToast(player, MSG(MSG_CONVERT_TEST_FAILED, player.UserIDString, MSG(MSG_NO_CONVERT_PERMISSION)));
                    return;
                }

                var netID = new NetworkableId(tcNetID);

                BuildingPrivlidge findTC = BaseNetworkable.serverEntities.Find(netID) as BuildingPrivlidge;

                if (findTC == null)
                {
                    return;
                }

                //check cost here.

                bool couldNotAfford = false;

                int amountNeeded = Instance.Configuration.ConversionCostAmount;

                string missingPlugin = null;

                if (amountNeeded != 0)
                {
                    switch (Instance.Configuration.ConversionCostCurrencyUsed)
                    {
                        case ConversionCurrencyType.CustomItemAmount:
                            {
                                //first check the TC. Then check the player's containers.
                                Item thisItem;

                                var iterateOver = Facepunch.Pool.Get<List<Item>>();

                                ReusableGatherItemAmountArrayCount = 0;

                                iterateOver.AddRange(findTC.inventory.itemList);
                                iterateOver.AddRange(player.inventory.containerMain.itemList);
                                iterateOver.AddRange(player.inventory.containerBelt.itemList);
                                iterateOver.AddRange(player.inventory.containerWear.itemList);

                                bool gotEnough = false;

                                for (var i = 0; i < iterateOver.Count; i++)
                                {
                                    thisItem = iterateOver[i];

                                    if (thisItem == null)
                                    {
                                        continue;
                                    }

                                    if (thisItem.info.shortname != Instance.Configuration.ConversionCostCustomItemShortname)
                                    {
                                        continue;
                                    }

                                    if (Instance.Configuration.ConversionCostCustomItemSkinID != 0)
                                    {
                                        if (thisItem.skin != Instance.Configuration.ConversionCostCustomItemSkinID)
                                        {
                                            continue;
                                        }
                                    }

                                    //ok, it's that item. how much is available, and how much we will take in the end?

                                    int amountAvailable = thisItem.amount;

                                    int amountTaken = Mathf.Min(amountNeeded, amountAvailable);

                                    ReusableGatherItemAmountArray[ReusableGatherItemAmountArrayCount] = new GatherItemAmount { GatherAmount = amountTaken, GatherItem = thisItem };

                                    ReusableGatherItemAmountArrayCount++;

                                    amountNeeded -= amountTaken;

                                    if (amountNeeded <= 0)
                                    {
                                        gotEnough = true;
                                        break;
                                    }
                                }

                                Facepunch.Pool.FreeUnmanaged(ref iterateOver);

                                if (!gotEnough)
                                {
                                    couldNotAfford = true;
                                }                               

                            }
                            break;
                        case ConversionCurrencyType.ServerRewards:
                            {
                                if (Instance.ServerRewards == null)
                                {
                                    couldNotAfford = true;
                                    missingPlugin = nameof(Instance.ServerRewards);
                                    break;
                                }

                                int checkRewardPoints = Convert.ToInt32(Instance.ServerRewards.Call("CheckPoints", player.userID));

                                if (checkRewardPoints < amountNeeded)
                                {
                                    couldNotAfford = true;
                                }

                            }
                            break;
                        case ConversionCurrencyType.Economics:
                            {
                                if (Instance.Economics == null)
                                {
                                    couldNotAfford = true;
                                    missingPlugin = nameof(Instance.Economics);
                                    break;
                                }

                                int checkEconomics = Convert.ToInt32(Instance.Economics.Call("Balance", player.userID.Get()));

                                if (checkEconomics < amountNeeded)
                                {
                                    couldNotAfford = true;
                                }

                            }
                            break;
                        case ConversionCurrencyType.NoCost: break;
                    }
                }

                if (couldNotAfford)
                {
                    if (missingPlugin != null)
                    {
                        PlayerShowTCToast(player, MSG(MSG_CONVERT_COST_MISSING_PLUGIN, player.UserIDString, missingPlugin));
                        return;
                    }

                    PlayerShowTCToast(player, MSG(MSG_CONVERT_COST_CANNOT_AFFORD, player.UserIDString));
                    return;
                }

                BuildingBlock externalDoorway;
                BuildingBlock middleFoundation;

                string log;
                Vector3 debugPosition;

                var test = ShellViabilityCheck(findTC, out middleFoundation, out externalDoorway, out log, out debugPosition, player.UserIDString);

                if (!test)
                {
                    PlayerShowTCToast(player, MSG(MSG_CONVERT_TEST_FAILED, player.UserIDString, log));
                    if (debugPosition != default(Vector3))
                    {
                        DrawText(player, 10F, Color.red, debugPosition, "<size>X</size>");
                    }
                    return;
                }

                var newPocket = ConvertAlreadyViableCheckedIntoPocketDimension(ReusableShellElementList, ReusableExtraBaseCombatEntities, middleFoundation, externalDoorway, true);

                var freshPortalNetID = newPocket.ThisEntityNetID;

                var newItem = MakeEmptyDimensionalBoxItem(1);

                TieItemToPortalNetID(freshPortalNetID, newItem);

                player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);

                //use an actual toast here, since the TC is waay up there.
                ToastPlayer(player, MSG(MSG_CONVERTED_SUCCESSFULLY), GameTip.Styles.Blue_Normal);

                if (Instance.Configuration.ConversionCostCurrencyUsed == ConversionCurrencyType.NoCost)
                {
                    return;
                }

                //now charge the player
                switch (Instance.Configuration.ConversionCostCurrencyUsed)
                {
                    case ConversionCurrencyType.CustomItemAmount:
                        {
                            for (var i = 0; i < ReusableGatherItemAmountArrayCount; i++)
                            {
                                ReusableGatherItemAmountArray[i].GatherItem.UseItem(ReusableGatherItemAmountArray[i].GatherAmount);
                            }
                        }
                        break;
                    case ConversionCurrencyType.ServerRewards:
                        {
                            Instance.ServerRewards.Call("TakePoints", player.userID, amountNeeded);
                        }
                        break;
                    case ConversionCurrencyType.Economics:
                        {
                            Instance.Economics.Call("Withdraw", player.userID.Get(), (double)amountNeeded);
                        }
                        break;
                }
            }


        }
        #endregion

        #region CFG

        public ConfigData Configuration;

        public enum OwnerIDHandling
        {
            AssignDeployingPlayerID,
            AssignZeroID,
            LeaveOriginalID,
        }

        public class CopypasteData
        {
            public string Filename;

            public bool AuthorizePlayerOnTC = true;
            public bool AuthorizePlayerOnLocks = true;
            public bool AuthorizePlayerOnTurrets = true;
            public bool AuthorizationIncludesTeammates = true;

            [JsonConverter(typeof(StringEnumConverter))]
            public OwnerIDHandling AssignOwnerIDs = OwnerIDHandling.AssignDeployingPlayerID;

            [JsonIgnore]
            public ulong SkinID;
        }

        public enum ConversionCurrencyType
        {
            NoCost,
            CustomItemAmount,
            ServerRewards,
            Economics
        }

        public enum GuardOutsideTeleportLocation
        {
            Random,
            Box,
            Portal
        }

        public class ConfigData
        {
            public string Version = VERSION;

            //boxes

            public float DimensionalBoxesSubtractItemConditionWhenPickingUp = 0F;

            public bool DimensionalBoxesAllowLockingWithLocks = false;

            public bool DimensionalBoxesAllowDamage = true;

            public float DimensionaBoxesDamageResistanceDivisor = 50F;

            public bool DimensionalBoxesAlwaysHaveStability = true;

            public bool DimensionalBoxesCheckForPlayersInsideWhenPickingUp = true;

            public bool DimensionalBoxesCheckBuildingBlockOutsideWhenEnteringDimension = false;

            public bool DimensionalBoxesCheckTCAuthInsideWhenEnteringDimension = false;

            public bool DimensionalBoxesCheckTCAuthInsideWhenPickingUp = false;

            public bool DimensionalBoxesItemCheckTCAuthInsideWhenHandlingItem = false;

            //pockets
            //public bool DimensionalPocketsAllowSolarAndWindmill = false;

            //public bool DimensionalPocketsAllowTestGenerators = true;

            public float DimensionalPocketsTrapImmunityAfterTeleport = 1.5F;

            public bool DimensionalPocketsExteriorBlocksAllowNonDecayDamage = true;

            public float DimensionalPocketsActualAltitudeMin = 3200F;

            public float DimensionalPocketsActualAltitudeMax = 3900F;

            public bool DimensionaPocketsAllowVendingMachineBroadcast = false;

            public bool DimensionalPocketsRespawningInsideRequiresDeployment = true;

            public bool DimensionalPocketsTeleportingInsideRequiresDeployment = true;

            public uint DimensionalPocketsMaxInceptionDepth = 10;

            public float DimensionalPocketsLocationMinTemperature = 15F;

            public float DimensionalPocketsLocationMaxTemperature = 32F;
            //new in 1.0.7
            [JsonConverter(typeof(StringEnumConverter))]
            public GuardOutsideTeleportLocation DimensionalPocketsGuardOutsideVolumeTeleportLocation = GuardOutsideTeleportLocation.Box;

            public bool DimensionalPocketsGuardOutsideVolumeKillAfterTeleport = false;
            //

            public string EffectPlayerTeleportAppear = FX_TELEPORT_APPEAR;
            public string EffectPlayerTeleportDisappear = FX_TELEPORT_DISAPPEAR;

            public bool EffectsPlayerTeleportEnable = true;

            public bool CheckEscapeBlockWhenEnteringDimensions = true;
            public bool CheckEscapeBlockWhenExitingDimensions = true;

            public bool CheckEscapeBlockWhenDeployingDimensions = true;
            public bool CheckEscapeBlockWhenPickingUpDimensions = true;

            public List<string> ForbiddenCommandsInsideDimensions = new List<string>();

            public bool ForbiddenCommandsCheckingEnabled = true;

            //conversion pricing
            [JsonConverter(typeof(StringEnumConverter))]
            public ConversionCurrencyType ConversionCostCurrencyUsed = ConversionCurrencyType.CustomItemAmount;

            public int ConversionCostAmount = 500;

            public string ConversionCostCustomItemShortname = "scrap";

            public string ConversionCostCustomItemOverrideName = null;

            public ulong ConversionCostCustomItemSkinID = 0;

            //gui

            public float TCGuiButtonAnchorX = 0.5F;
            public float TCGuiButtonAnchorY = 0F;

            public float TCGuiButtonOffsetXMin = 382F;
            public float TCGuiButtonOffsetYMin = 585F;
            public float TCGuiButtonOffsetXMax = 572F;
            public float TCGuiButtonOffsetYMax = 609F;

            public string TCGuiButtonBgColor = ConversionGuiManager.GUI_COLOR_PURPLISH;
            public float TCGuiButtonBgAlpha = 0.25F;

            public string TCGuiButtonTextColor = ConversionGuiManager.GUI_COLOR_RUSTISH;
            public float TCGuiButtonTextAlpha = 1F;
            public int TCGuiButtonTextSize = 14;

            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor TCGuiButtonTextAlign = TextAnchor.MiddleCenter;

            //toast

            public float TCGuiToastAnchorX = 0.5F;
            public float TCGuiToastAnchorY = 0F;

            public float TCGuiToastOffsetXMin = 192F;
            public float TCGuiToastOffsetYMin = 492F;
            public float TCGuiToastOffsetXMax = 572F;
            public float TCGuiToastOffsetYMax = 556F;

            public string TCGuiToastBgColor = ConversionGuiManager.GUI_COLOR_REDDISH;
            public float TCGuiToastBgAlpha = 1F;

            public float TCGuiToastDuration = 5F;

            public string TCGuiToastTextColor = ConversionGuiManager.GUI_COLOR_RUSTISH;
            public float TCGuiToastTextAlpha = 1F;
            public int TCGuiToastTextSize = 14;

            public float TCGuiToastTextPadding = 4F;

            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor TCGuiToastTextAlign = TextAnchor.UpperLeft;

            //cost


            public float TCGuiCostAnchorX = 0.5F;
            public float TCGuiCostAnchorY = 0F;

            public float TCGuiCostOffsetXMin = 192F;
            public float TCGuiCostOffsetYMin = 560F;
            public float TCGuiCostOffsetXMax = 562F;
            public float TCGuiCostOffsetYMax = 581F;

            public string TCGuiCostTextColor = ConversionGuiManager.GUI_COLOR_RUSTISH;
            public float TCGuiCostTextAlpha = 1F;
            public int TCGuiCostTextSize = 12;



            [JsonConverter(typeof(StringEnumConverter))]
            public TextAnchor TCGuiCostTextAlign = TextAnchor.MiddleRight;

            //various
            public Dictionary<ulong, CopypasteData> SkinIDToCopyPasteData = new Dictionary<ulong, CopypasteData>();

            //added in 1.0.12 to replace solar/windmill bools
            public List<string> DimensionalPocketEntityStickingOutWhitelist = new List<string>();

            //cached, ignored
            [JsonIgnore]
            public ItemDefinition CachedCustomCostItemDef = null;

            [JsonIgnore]
            public Dictionary<string, CopypasteData> FilenameToCopyPasteData = new Dictionary<string, CopypasteData>();


        }

        protected override void LoadDefaultConfig()
        {
            RestoreDefaultConfig();
        }

        private void ProcessConfigData()
        {
            bool needsSave = false;

            if (Configuration.SkinIDToCopyPasteData.IsNullOrEmpty())
            {
                Configuration.SkinIDToCopyPasteData = new Dictionary<ulong, CopypasteData>
                {
                    [810762814] = new CopypasteData
                    {
                        Filename = "twigbasic",
                    },

                    [2678938976] = new CopypasteData
                    {
                        Filename = "boxmas",
                    },

                    [2901076682] = new CopypasteData
                    {
                        Filename = "partycube",
                    },

                    [2455018530] = new CopypasteData
                    {
                        Filename = "hotbox",
                    },

                    [2513758552] = new CopypasteData
                    {
                        Filename = "vip1",
                    },
                    [2513747559] = new CopypasteData
                    {
                        Filename = "vip2",
                    },
                    [2513727958] = new CopypasteData
                    {
                        Filename = "vip3",
                    }
                };

                needsSave = true;
            }

            if (Configuration.ForbiddenCommandsCheckingEnabled)
            {
                if (Configuration.ForbiddenCommandsInsideDimensions.IsNullOrEmpty())
                {
                    Configuration.ForbiddenCommandsInsideDimensions = new List<string>
                    {
                        "tp",
                        "warp",
                        "back",
                        "home",
                        "tpr",
                        "tpa",
                        "kit"
                    };

                    needsSave = true;
                }
            }

            //added in 1.0.12

            if (Configuration.DimensionalPocketEntityStickingOutWhitelist.IsNullOrEmpty())
            {
                Configuration.DimensionalPocketEntityStickingOutWhitelist = new List<string>
                {
                    PREFAB_GENERATOR_WINDMILL,
                    PREFAB_GENERATOR_SOLAR,
                    PREFAB_GENERATOR_FUEL,
                    PREFAB_GENERATOR_TEST_1,
                    PREFAB_GENERATOR_TEST_2,
                    PREFAB_WATER_CATCHER_LARGE,
                    PREFAB_WATER_CATCHER_SMALL,
                    PREFAB_WATER_COMBINER,
                    PREFAB_WATER_SPLITTER,
                    PREFAB_ELECTRICAL_COMBINER,
                    PREFAB_ELECTRICAL_SPLITTER,
                    PREFAB_ELECTRICAL_BRANCH,
                    PREFAB_WATER_PURIFIER_POWERED,
                    PREFAB_WATER_BARREL,
                    PREFAB_WATER_PURIFIER_STORAGE
                };

                needsSave = true;
            }
            //

            Configuration.FilenameToCopyPasteData.Clear();

            foreach (var entry in Configuration.SkinIDToCopyPasteData)
            {
                entry.Value.SkinID = entry.Key;

                if (Configuration.FilenameToCopyPasteData.ContainsKey(entry.Value.Filename))
                {
                    continue;
                }

                Configuration.FilenameToCopyPasteData.Add(entry.Value.Filename, entry.Value);
            }
            

            var oldVersion = Configuration.Version;

            if (oldVersion != VERSION)
            {
                Configuration.Version = VERSION;
                needsSave = true;
            }

            if (Configuration.ConversionCostCurrencyUsed == ConversionCurrencyType.CustomItemAmount)
            {
                Configuration.CachedCustomCostItemDef = ItemManager.FindItemDefinition(Instance.Configuration.ConversionCostCustomItemShortname);

                if (Configuration.CachedCustomCostItemDef == null)
                {
                    Configuration.CachedCustomCostItemDef = ItemManager.FindItemDefinition("scrap");
                }
            }

            if (needsSave)
            {
                SaveConfigData();
            }
        }
        private void RestoreDefaultConfig()
        {
            PrintWarning("Generating default config...");
            Configuration = new ConfigData();

            SaveConfigData();
        }

        private void LoadConfigData()
        {
            bool needsSave = false;

            PrintWarning("Loading configuration file...");
            try
            {
                Configuration = Config.ReadObject<ConfigData>();
                PrintWarning("Success.");
            }
            catch (Exception e)
            {
                Configuration = new ConfigData();
                PrintWarning($"Loading failed: \n{e.Message}\n{e.StackTrace}\nGenerating new config file...");

                needsSave = true;

            }

            if (needsSave)
            {
                SaveConfigData();
            }
            

        }
        private void SaveConfigData()
        {
            PrintWarning("Saving config...");
            Config.WriteObject(Configuration, true);
        }

        #endregion

        #region MONO

        public class InternalBehaviour : MonoBehaviour
        {
            public static void DetachEverythingInternal()
            {
                foreach (var entry in DimensionalBox.ServerCacheBox.Values.ToList())
                {
                    DestroyImmediate(entry);
                }

                foreach (var entry in DimensionalPocket.ServerCachePortal.Values.ToList())
                {
                    DestroyImmediate(entry);
                }

                foreach (var entry in DimensionalPlayer.ServerCachePlayer.Values.ToList())
                {
                    DestroyImmediate(entry);
                }

                //and just in case...

                foreach (var entry in FindObjectsOfType<InternalBehaviour>().ToList())
                {
                    DestroyImmediate(entry);
                }
            }
        }

        public class DimensionalBehaviour : InternalBehaviour
        {
            public BaseEntity ThisEntity;
            public ulong ThisEntityNetID;
            public ulong PairedEntityNetID;

            public void InitEntity()
            {
                ThisEntity = GetComponent<BaseEntity>();
                ThisEntityNetID = ThisEntity.net.ID.Value;

                MarkEntityAsInternal(ThisEntity);
            }

            public virtual void Awake()
            {
                InitEntity();
            }

            public virtual void OnDestroy()
            {

            }



            public virtual bool IsPairedToValidEntity()
            {
                if (PairedEntityNetID == 0)
                {
                    return false;
                }

                return true;
            }
        }

        public class DimensionalBox : DimensionalBehaviour
        {
            public static Dictionary<ulong, DimensionalBox> ServerCacheBox;
            public DimensionalPocket PairedPocket;

            public ItemContainer ThisItemContainer;
            public ulong ThisItemContainerUID;

            public StorageContainer ThisStorageContainer;

            public Vector3 LastCheckedPosition;
            
            public void AssumeContainerUID(ulong forceUID)
            {
                ThisItemContainerUID = forceUID;

                ThisItemContainer.uid.Value = forceUID;
                ThisItemContainer.MarkDirty();

            }


            public bool WasJustPickedUp = false;

            public override void Awake()
            {
                base.Awake();

                ServerCacheBox.Add(ThisEntityNetID, this);

                ThisStorageContainer = ThisEntity as StorageContainer;

                ThisItemContainer = ThisStorageContainer.inventory;
                ThisItemContainerUID = ThisItemContainer.uid.Value;

                ThisStorageContainer.pickup.subtractCondition = Instance.Configuration.DimensionalBoxesSubtractItemConditionWhenPickingUp;

                if (Instance.Configuration.DimensionalBoxesAlwaysHaveStability)
                {
                    MakeStable(ThisStorageContainer, false, false);
                }

                PairToPocket(TryFindMatchingPocketByThisItemContainerUID());
            }

            public void PlayerOpenedBox(BasePlayer player)
            {
                if (!IsPairedToValidEntity())
                {
                    return;
                }

                if (PairedPocket.ThisPortal == null)
                {
                    return;
                }

                DimensionalPocket.TeleportPlayerToPortal(player, PairedPocket.ThisPortal);
            }

            public override void OnDestroy()
            {
                base.OnDestroy();

                if (Unloading)
                {
                    return;
                }

                ServerCacheBox.Remove(ThisEntityNetID);

                bool isPaired = IsPairedToValidEntity();

                if (!isPaired)
                {
                    return;
                }


                if (!WasJustPickedUp)
                {
                    if (!PairedPocket.ThisPortal.IsDestroyed)
                    {
                        PairedPocket.LastKnownBoxPosition = transform.position;

                        PairedPocket.HurtPocketDoDeath(KILL_PORTAL_REASON_BOX_KILLED);
                    }                        
                }
                else
                {
                    PairedPocket.BeUnpairedByBox();   
                }
                
            }

            public override bool IsPairedToValidEntity()
            {
                if (!base.IsPairedToValidEntity())
                {
                    return false;
                }

                if (PairedPocket == null)
                {
                    return false;
                }

                if (PairedPocket.ThisEntity == null)
                {
                    return false;
                }

                return true;
            }

            public void PairToPocket(DimensionalPocket pocket)
            {
                if (pocket == null)
                {
                    UnpairFromPocket();
                    return;
                }

                PairedPocket = pocket;
                PairedEntityNetID = pocket.ThisEntityNetID;

                AssumeContainerUID(PairedEntityNetID);

                PairedPocket.BePairedByBox(this);
                /*
                if (PairedPocket.PairedEntityNetID == 0)
                {

                }*/
            }

            public void UnpairFromPocket()
            {
                PairedPocket = null;
                PairedEntityNetID = 0;
            }

            public DimensionalPocket TryFindMatchingPocketByThisItemContainerUID()
            {
                foreach (var entry in DimensionalPocket.ServerCachePortal)
                {
                    if (entry.Key == ThisItemContainerUID)
                    {
                        return entry.Value;
                    }
                }

                return null;
                
            }

            public bool TryBuildFromItem(Item fromItem)
            {
                var pocket = TryGetDimensionalPocketFromNonNullItem(fromItem);

                if (pocket == null)
                {
                    return false;
                }

                PairToPocket(pocket);

                return true;
            }

            public object CanPickupBox(BasePlayer player)
            {
                if (!IsPairedToValidEntity())
                {
                    return null;
                }

                //first, regular check...

                bool pickupCheckSoFar;

                if (ThisStorageContainer.pickup.enabled)
                {
                    if (!ThisStorageContainer.pickup.requireBuildingPrivilege || player.CanBuild())
                    {
                        if (ThisStorageContainer.pickup.requireHammer)
                        {
                            pickupCheckSoFar = player.IsHoldingEntity<Hammer>();
                        }
                        else
                        {
                            pickupCheckSoFar = true;
                        }
                    }
                    else
                    {
                        pickupCheckSoFar = false;
                    }
                }
                else
                {
                    pickupCheckSoFar = false;
                }

                if (pickupCheckSoFar)
                {
                    if (Instance.Configuration.DimensionalBoxesCheckTCAuthInsideWhenPickingUp)
                    {
                        //check if there's a TC. if there isn't, go on. nothing.

                        if (PairedPocket.IsPlayerBuildingBlockedOnFirstTcInsideIfThereIsAnyButOnlyIfAtLeast1Auth(player))
                        {
                            ToastPlayer(player, MSG(MSG_CANT_PICK_UP_WITHOUT_TC_AUTH2, player.UserIDString));
                            return false;
                        }
                    }

                    if (Instance.Configuration.DimensionalBoxesCheckForPlayersInsideWhenPickingUp)
                    {
                        if (PairedPocket.AnyPlayersInside())
                        {
                            ToastPlayer(player, MSG(MSG_CANT_PICK_UP_WITH_PLAYERS_INSIDE, player.UserIDString));
                            return false;
                        }
                        else
                        {
                            return true;
                        }
                    }
                    else
                    {
                        return true;
                    }


                }
                else
                {
                    return false;
                }
            }

            public void OnPickedUp(Item newlyGivenItem, BasePlayer player)
            {
                WasJustPickedUp = true; //so the mono OnDestroy knows not to panic, it wasn't killed

                TurnIntoDimensionalBoxItem(newlyGivenItem);

                TieItemToPortalNetID(PairedEntityNetID, newlyGivenItem);
            }           

        }

        public class HollowCubeMonoKiller : HollowCubeMono
        {
            public override void Prepare(DimensionalPocket owner, bool collidersAreTriggers)
            {
                base.Prepare(owner, collidersAreTriggers);
            }

            private void OnTriggerEnter(Collider col)
            {
                BaseEntity entity = col?.ToBaseEntity();

                if (!entity?.IsValid() ?? false)
                {
                    return;
                }

                var maybePlayer = entity as BasePlayer;

                if (maybePlayer == null)
                {
                    return;
                }

                if (IsPlayerNPC(maybePlayer))
                {
                    return;
                }

                if (maybePlayer.IsFlying)
                {
                    return;
                }

                if (maybePlayer.IsGod())
                {
                    return;
                }

                Vector3 teleportPos = default(Vector3);
                Vector3 teleportViewAngles = default(Vector3);

                bool useRandom = false;

                string langMsgForReason = MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_RANDOM;

                switch(Instance.Configuration.DimensionalPocketsGuardOutsideVolumeTeleportLocation)
                {
                    case GuardOutsideTeleportLocation.Portal:
                        {
                            teleportPos = DimensionalPocket.GetPortalTeleportPos(OwnerPocket.ThisPortal);
                            teleportViewAngles = DimensionalPocket.GetPortalTeleportViewAngles(OwnerPocket.ThisPortal);
                            langMsgForReason = MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_PORTAL;
                        }
                        break;
                    case GuardOutsideTeleportLocation.Box:
                        {
                            if (OwnerPocket.PairedBox == null)
                            {
                                useRandom = true;
                            }
                            else
                            {
                                teleportPos = OwnerPocket.GetTeleportPosition(true, true);
                                langMsgForReason = MSG_VOLUME_OUTSIDE_CORPSE_LOCATION_BOX;
                            }
                        }
                        break;
                    default:
                    case GuardOutsideTeleportLocation.Random:
                        {
                            teleportPos = default(Vector3); //will be set
                            useRandom = true;
                        }
                        break;
                }

                if (useRandom)
                {
                    teleportPos = ServerMgr.FindSpawnPoint(maybePlayer).pos; //forces to random, we're lying that the box doesn't exist
                }

                TeleportPlayerTo(maybePlayer, teleportPos, teleportViewAngles);

                var finalMessage = MSG(MSG_VOLUME_OUTSIDE_KILLS_PLAYERS, maybePlayer.UserIDString, MSG(langMsgForReason, maybePlayer.UserIDString));

                if (!Instance.Configuration.DimensionalPocketsGuardOutsideVolumeKillAfterTeleport)
                {
                    return;
                }

                maybePlayer.Invoke(() =>
                {
                    HurtToDeath(maybePlayer);

                    maybePlayer.ChatMessage(finalMessage);
                }, 0.1F);

            }
        }
        

        public class HollowCubeMono : MonoBehaviour
        {
            public DimensionalPocket OwnerPocket;
            public BoxCollider[] HollowCubeSides;

            public virtual void Prepare(DimensionalPocket owner, bool collidersAreTriggers)
            {
                OwnerPocket = owner;

                HollowCubeSides = new BoxCollider[5];

                float thickness = 1F;
                float innerSize = 9.66F;
                float outerSize = innerSize + thickness;

                HollowCubeSides[0] = AddBoxCollider(gameObject, new Vector3(outerSize, thickness, outerSize), new Vector3(0, innerSize / 2 + thickness / 2, 0), collidersAreTriggers); // Top
                //HollowCubeSides[1] = AddBoxCollider(HollowCubeGameobject, new Vector3(outerSize, thickness, outerSize), new Vector3(0, -(innerSize / 2 + thickness / 2), 0)); // Bottom
                HollowCubeSides[1] = AddBoxCollider(gameObject, new Vector3(outerSize, outerSize, thickness), new Vector3(0, 0, innerSize / 2 + thickness / 2), collidersAreTriggers); // Front
                HollowCubeSides[2] = AddBoxCollider(gameObject, new Vector3(outerSize, outerSize, thickness), new Vector3(0, 0, -(innerSize / 2 + thickness / 2)), collidersAreTriggers); // Back
                HollowCubeSides[3] = AddBoxCollider(gameObject, new Vector3(thickness, outerSize, outerSize), new Vector3(innerSize / 2 + thickness / 2, 0, 0), collidersAreTriggers); // Right
                HollowCubeSides[4] = AddBoxCollider(gameObject, new Vector3(thickness, outerSize, outerSize), new Vector3(-(innerSize / 2 + thickness / 2), 0, 0), collidersAreTriggers); // Left
            }
        }

        //attach directly to the portal.


        public class DimensionalPocket : DimensionalBehaviour
        {
            public DimensionalBox PairedBox;

            public static readonly Vector3 COLLIDER_BOX_SIZE = new Vector3(2.4F, 1.7F, 1.2F);

            public static readonly Vector3 COLLIDER_BOX_OFFSET = Vector3.up * 1.365F;

            public static Dictionary<ulong, DimensionalPocket> ServerCachePortal;

            public static Dictionary<ulong, DimensionalPocket> ServerCacheShellBlockToPocket;

            public static Dictionary<uint, DimensionalPocket> ServerCacheBuildingIDToPocket;

            public BasePortal ThisPortal;

            public Vector3 LastKnownBoxPosition = default(Vector3);

            public BuildingBlock MiddleFoundation;

            public BuildingBlock ExitDoorway;

            public Vector3 CenterPoint;
            public Quaternion CenterRot;

            public bool FullyActivated = false;

            public Dictionary<ulong, BuildingBlock> ShellBlocks = new Dictionary<ulong, BuildingBlock>();

            public BuildingManager.Building ThisBuilding;
            public uint ThisBuildingID;


            public GameObject ColliderGameobject;

            public BoxCollider ColliderBox;

            public GameObject HollowCubePreventBuildingGO;
            public GameObject HollowCubeKillPlayersGO;

            public HollowCubeMono HollowMono;

            public DimensionalPocket ParentPocket = null;

            public ListHashSet<DimensionalPocket> ChildrenPockets = new ListHashSet<DimensionalPocket>();

            public static Vector3 GetPortalTeleportPos(BasePortal portal)
            {
                return portal.transform.position - portal.transform.right * TELEPORTATION_PORTAL_OFFSET;
            }

            public static Vector3 GetPortalTeleportViewAngles(BasePortal portal)
            {
                return portal.GetLocalEntryExitRotation() * Vector3.right;
            }

            public static void TeleportPlayerToPortal(BasePlayer player, BasePortal portal)
            {
                Vector3 teleportPos = DimensionalPocket.GetPortalTeleportPos(portal);
                Vector3 viewAngles = DimensionalPocket.GetPortalTeleportViewAngles(portal);

                TeleportPlayerTo(player, teleportPos, viewAngles);

            }

            public override void Awake()
            {
                base.Awake();

                ThisPortal = ThisEntity as BasePortal;

                try
                {
                    ServerCachePortal.Add(ThisEntityNetID, this);
                }
                catch
                {
                    Instance.PrintError("!!! ERROR CASE A");
                }


                ColliderGameobject = new GameObject("PortalPreventBuilding");
                ColliderGameobject.transform.SetPositionAndRotation(transform.position + COLLIDER_BOX_OFFSET, transform.rotation);
                ColliderGameobject.SetLayerRecursive(LAYER_PREVENT_BUILDING);

                ColliderBox = ColliderGameobject.AddComponent<BoxCollider>();
                ColliderBox.isTrigger = false;
                ColliderBox.size = COLLIDER_BOX_SIZE;
                ColliderBox.center = Vector3.zero;
                ColliderBox.contactOffset = 0.01F;

                ColliderGameobject.transform.SetParent(transform, true);
                ColliderGameobject.SetActive(true);

                unchecked
                {
                    ThisBuildingID = (uint)ThisPortal.targetID.Value; //we know this will never exceed uint.maxvalue, but still just in case, unchecked
                }


                ThisBuilding = BuildingManager.server.GetBuilding(ThisBuildingID);

                if (ThisBuilding == null)
                {
                    HurtPocketDoDeath(KILL_PORTAL_REASON_AWAKE_BUILDING_NOT_FOUND);
                    return;
                }


                try
                {
                    ServerCacheBuildingIDToPocket.Add(ThisBuildingID, this);
                }
                catch
                {
                    Instance.PrintError("!!! ERROR CASE B");
                }

                //find middle foundation and doorway

                BuildingBlock checkedBlock;

                var iterateOver = ThisBuilding.buildingBlocks;

                for (var i = 0; i < iterateOver.Count; i++)
                {
                    checkedBlock = iterateOver[i];

                    if (!IsEntityMarkedInternal(checkedBlock))
                    {
                        continue;
                    }

                    try
                    {
                        ShellBlocks.Add(checkedBlock.net.ID.Value, checkedBlock);
                    }
                    catch
                    {
                        Instance.PrintError("!!! ERROR CASE C");
                    }


                    //all internal entities are the shell


                    if (!IsEntityMarkedSpecific(checkedBlock))
                    {
                        continue;
                    }

                    switch (checkedBlock.PrefabName)
                    {
                        case PREFAB_FOUNDATION_SQUARE:
                            {
                                MiddleFoundation = checkedBlock;
                            }
                            break;
                        case PREFAB_WALL_DOORWAY:
                            {
                                ExitDoorway = checkedBlock;
                            }
                            break;
                    }
                }

                if (ShellBlocks.Count != 9 + 9 + 36)
                {
                    HurtPocketDoDeath(KILL_PORTAL_REASON_AWAKE_SHELLBLOCK_MISCOUNT);
                    return;
                }

                if (MiddleFoundation == null)
                {
                    HurtPocketDoDeath(KILL_PORTAL_REASON_AWAKE_MIDDLE_FOUNDATION_NULL);
                    return;
                }

                if (ExitDoorway == null)
                {
                    HurtPocketDoDeath(KILL_PORTAL_REASON_AWAKE_EXIT_DOORWAY_NULL);
                    return;
                }

                CenterPoint = MiddleFoundation.transform.position + Vector3.up * 4.5F;
                CenterRot = MiddleFoundation.transform.rotation;

                foreach (var shellBlock in ShellBlocks)
                {
                    try
                    {
                        ServerCacheShellBlockToPocket.Add(shellBlock.Key, this);
                    }
                    catch
                    {
                        Instance.PrintError("!!! ERROR CASE D");
                    }

                }

                HollowCubePreventBuildingGO = AttachHollowCube<HollowCubeMono>(LAYER_PREVENT_BUILDING, false);
                HollowCubeKillPlayersGO = AttachHollowCube<HollowCubeMonoKiller>((int)Rust.Layer.Reserved1, true);

                //UpdateHierarchyRecursively();

                FullyActivated = true;

            }

            public GameObject AttachHollowCube<T>(int layer, bool collidersAreTriggers) where T: HollowCubeMono, new()
            {
                var newGo = new GameObject("HollowCube");
                newGo.transform.SetPositionAndRotation(CenterPoint, transform.rotation);
                newGo.SetLayerRecursive(layer);

                newGo.transform.SetParent(transform, true);
                newGo.SetActive(true);

                var colliderMono = newGo.AddComponent<T>();
                colliderMono.Prepare(this, collidersAreTriggers);

                return newGo;
            }

            public void UpdateParentHierarchyRecursively(ListHashSet<DimensionalPocket> listToUse = null, int traversedLevel = 0)
            {
                if (listToUse == null)
                {
                    listToUse = new ListHashSet<DimensionalPocket>();
                }
                else
                {
                    if (listToUse.Contains(this))
                    {
                        return;
                    }
                }

                listToUse.Add(this); //so no sloshing

                var oldParent = ParentPocket;
                DimensionalPocket newParent;

                if (!IsPairedToValidEntity())
                {
                    newParent = null;
                }
                else
                {
                    if (PairedBox.ThisStorageContainer.buildingID == 0 || PairedBox.ThisStorageContainer.GetBuilding() == null)
                    {
                        newParent = null;
                    }
                    else
                    {
                        if (!ServerCacheBuildingIDToPocket.ContainsKey(PairedBox.ThisStorageContainer.buildingID))
                        {
                            newParent = null;
                        }
                        else
                        {
                            newParent = ServerCacheBuildingIDToPocket[PairedBox.ThisStorageContainer.buildingID];
                        }
                    }
                }

                //children first...

                var iterateOver = ChildrenPockets.ToArray();

                for (var ch = 0; ch < iterateOver.Length; ch++)
                {
                    iterateOver[ch].UpdateParentHierarchyRecursively(listToUse, traversedLevel + 1);
                }

                //and then parents, they will do their children first

                if (oldParent != newParent)
                {
                    if (oldParent != null)
                    {
                        if (oldParent.ChildrenPockets.Contains(this))
                        {
                            oldParent.ChildrenPockets.Remove(this);
                        }

                        oldParent.UpdateParentHierarchyRecursively(listToUse, traversedLevel-1);
                    }

                    if (newParent != null)
                    {
                        if (!newParent.ChildrenPockets.Contains(this))
                        {
                            newParent.ChildrenPockets.Add(this);
                        }

                        newParent.UpdateParentHierarchyRecursively(listToUse, traversedLevel-1);
                    }
                }





                ParentPocket = newParent;
            }

            public bool AnyPlayersInside()
            {
                var obb = new OBB(CenterPoint, Vector3.one*10F, CenterRot);

                VisEntitiesUniqueInsideOBB<BasePlayer>(obb);

                return ReusableVisibilityList.Any();
            }

            public bool IsPlayerBuildingBlockedOnFirstTcInsideIfThereIsAnyButOnlyIfAtLeast1Auth(BasePlayer player)
            {
                if (!ThisBuilding.HasBuildingPrivileges())
                {
                    return false;
                }

                var tc = ThisBuilding.buildingPrivileges.FirstOrDefault();

                //just in case
                if (tc == null)
                {
                    return false;
                }
                
                if (!tc.AnyAuthed())
                {
                    return false;
                }

                if (tc.IsAuthed(player))
                {
                    return false;
                }

                return true;
            }

            public void PlayerUsePortal(BasePlayer player)
            {
                if (!IsPairedToValidEntity())
                {
                    ToastPlayer(player, MSG(MSG_CANT_GET_OUT_STUCK, player.UserIDString));

                    return;
                }

                var position = GetTeleportPosition(true, true);
                TeleportPlayerTo(player, position, (PairedBox.transform.rotation * Vector3.forward).normalized);
            }

            public object OnShellBlockKilled(BuildingBlock shellBlock)
            {
                HurtPocketDoDeath(KILL_PORTAL_REASON_SHELLBLOCK_KILLED);
                return null;

            }

            public void HurtPocketDoDeath(string reason)
            {
                KillPortalReasonLast = reason;

                //we need to tell the hook: it's okay to have this portal killed
                MarkEntityAsSpecific(ThisPortal, false);

                ThisPortal.Kill();
                //this will induce OnDestroy
            }

            public Vector3 GetTeleportPosition(bool useProvidedBoxExistingCheck, bool boxExists)
            {
                if (!useProvidedBoxExistingCheck)
                {
                    boxExists = IsPairedToValidEntity();
                }

                if (LastKnownBoxPosition != default(Vector3))
                    return LastKnownBoxPosition + Vector3.up * TELEPORTATION_BOX_OFFSET;

                if (boxExists)
                    return PairedBox.transform.position + Vector3.up * TELEPORTATION_BOX_OFFSET;
                
                return ServerMgr.FindSpawnPoint().pos;
            }

            public override bool IsPairedToValidEntity()
            {
                if (!base.IsPairedToValidEntity())
                {
                    return false;
                }

                if (PairedBox == null)
                {
                    return false;
                }

                if (PairedBox.ThisEntity == null)
                {
                    return false;
                }

                return true;
            }

            public void BePairedByBox(DimensionalBox box)
            {
                if (box == null)
                {
                    BeUnpairedByBox();
                    return;
                }

                var currentlyPairedBox = PairedBox;

                //different box
                if (currentlyPairedBox != null && currentlyPairedBox != box)
                {
                    currentlyPairedBox.UnpairFromPocket();
                    currentlyPairedBox.WasJustPickedUp = true;
                    currentlyPairedBox.ThisEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                }

                PairedBox = box;
                PairedEntityNetID = box.ThisEntityNetID;

                //for lost and found
                ThisPortal.skinID = PairedBox.ThisStorageContainer.skinID;

                LastKnownBoxPosition = PairedBox.transform.position;

                UpdateParentHierarchyRecursively();
            }

            public void BeUnpairedByBox()
            {
                PairedBox = null;
                PairedEntityNetID = 0;

                UpdateParentHierarchyRecursively();
            }

            public override void OnDestroy()
            {
                base.OnDestroy();

                if (!Unloading)
                {
                    if (ChildrenPockets.Any())
                    {
                        var iterateOver = ChildrenPockets.ToArray();
                        for (var i = 0; i < iterateOver.Length; i++)
                        {
                            var child = iterateOver[i];

                            if (child == null)
                            {
                                continue;
                            }

                            child.LastKnownBoxPosition = LastKnownBoxPosition;
                            child.HurtPocketDoDeath(KILL_PORTAL_REASON_PARENT_DIMENSION_KILLED);
                        }
                    }

                    ServerCachePortal.Remove(ThisEntityNetID);
                    ServerCacheBuildingIDToPocket.Remove(ThisBuildingID);

                    bool boxStillExists = IsPairedToValidEntity();
                    bool boxStillExistsAndIsNotBeingDestroyed;

                    if (boxStillExists)
                    {
                        boxStillExistsAndIsNotBeingDestroyed = !PairedBox.ThisEntity.IsDestroyed;
                    }
                    else
                    {
                        boxStillExistsAndIsNotBeingDestroyed = false;
                    }

                    if (boxStillExistsAndIsNotBeingDestroyed)
                    {
                        //kill instantly
                        PairedBox.ThisEntity.Kill(BaseNetworkable.DestroyMode.Gib);
                    }

                    if (!FullyActivated)
                    {
                        return;
                    }

                    foreach (var shellBlock in ShellBlocks)
                    {
                        ServerCacheShellBlockToPocket.Remove(shellBlock.Key);
                    }

                    //if not unloading, it means the TC is already dead.

                    Vector3 generalTeleportLocation = GetTeleportPosition(true, boxStillExists);// 

                    var obb = new OBB(CenterPoint, Vector3.one * 10F, CenterRot);

                    VisEntitiesUniqueInsideOBB<BaseEntity>(obb);

                    for (var v = 0; v < ReusableVisibilityList.Count; v++)
                    {
                        BaseEntity thisEntity = ReusableVisibilityList[v];

                        if (thisEntity.IsDestroyed)
                        {
                            continue;
                        }

                        if (!BaseNetworkableEx.IsValid(thisEntity))
                        {
                            continue;
                        }

                        if (thisEntity.net.ID.Equals(ThisEntityNetID))
                        {
                            //that's the BasePortal, e.g. you. ignore. you're dying/dead.

                            continue;
                        }

                        if (thisEntity is BasePlayer)
                        {
                            TeleportPlayerTo(thisEntity as BasePlayer, generalTeleportLocation);

                            continue;
                        }

                        if (thisEntity is DroppedItemContainer || thisEntity is LootableCorpse)
                        {
                            //teleport it with slight delay in a hemisphere

                            TeleportEntity(thisEntity, generalTeleportLocation, false);

                            continue;
                        }


                        //does it have any sort of inventory, but not dropped item container or lootable corpse?

                        //same, teleport with a delay. BUT KILL WITH GIBS THIS TIME.

                        if (thisEntity is IItemContainerEntity || thisEntity is IIdealSlotEntity || thisEntity is LootPanel.IHasLootPanel)
                        {
                            TeleportEntity(thisEntity, generalTeleportLocation, true);

                            continue;
                        }

                        //anything else
                        if (thisEntity != null && !thisEntity.IsDestroyed)
                        {
                            thisEntity.Kill(BaseNetworkable.DestroyMode.None);
                        }
                    }
                }

                if (ColliderGameobject != null)
                {
                    DestroyImmediate(ColliderGameobject);
                }

                if (HollowCubePreventBuildingGO != null)
                {
                    DestroyImmediate(HollowCubePreventBuildingGO);
                }

                if (HollowCubeKillPlayersGO != null)
                {
                    DestroyImmediate(HollowCubeKillPlayersGO);
                }
            }

        }
        #endregion

        #region CMD

        public bool ReplaceOrCheckCommon(IPlayer iplayer, string command, out BasePlayer player, out RaycastHit info)
        {
            info = default(RaycastHit);
            player = iplayer.Object as BasePlayer;

            if (iplayer.IsServer || player == null)
            {
                iplayer.Reply(MSG(MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_REQUIRES_PLAYER, iplayer.Id));
                return false;
            }

            if (player.IsBuildingBlocked())
            {
                iplayer.Reply(MSG(MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_BUILDING_BLOCKED, iplayer.Id));
                return false;
            }

            if (!Physics.Raycast(player.eyes.HeadRay(), out info, 10F, LAYERMASK_INTERESTING_ENTITIES, QueryTriggerInteraction.Ignore))
            {
                iplayer.Reply(MSG(MSG_REPLACE_OR_CHECK_OR_CHECK_COMMON_RAYCAST_FAIL, iplayer.Id));
                return false;
            }

            return true;
        }

        public bool CheckCommon(IPlayer iplayer, string command, out BasePlayer player, out BuildingBlock middleFoundation, out BuildingBlock exitDoorway, out string debugInfo, out Vector3 problematicPosition, string[] args)
        {
            middleFoundation = null;
            exitDoorway = null;
            debugInfo = "";
            problematicPosition = default(Vector3);

            RaycastHit info;

            if (!ReplaceOrCheckCommon(iplayer, command, out player, out info))
            {
                return false;
            }

            var maybeTC = RaycastHitEx.GetEntity(info) as BuildingPrivlidge;

            if (maybeTC == null)
            {
                iplayer.Reply(MSG(MSG_CHECK_COMMON_NOT_LOOKING_AT_TC, player.UserIDString));
                return false;
            }

            bool check = ShellViabilityCheck(maybeTC, out middleFoundation, out exitDoorway, out debugInfo, out problematicPosition, player.UserIDString);

            if (!check)
            {
                iplayer.Reply(debugInfo);
                if (problematicPosition != default(Vector3))
                {
                    DrawText(player, 10F, Color.red, problematicPosition, "<size>X</size>");
                }
                
            }

            return check;
        }

        public static Dictionary<string, string> ExternalShellBlockReplacementsShortnameToFull;

        public static ListHashSet<string>[] ExternalShellBlockReplacementFamilies;

        public static Dictionary<string, ListHashSet<string>> ExternalShellBlockReplacements;

        public static Dictionary<string, ListHashSet<string>> ExternalShellBlockReplacementsSocketsToCheck;

        private void GetValidReplacementsStrings(out string replacementsSides, out string replacementsTops)
        {
            var stringBuilderSides = new StringBuilder();
            var stringBuilderTops = new StringBuilder();

            for (var f = 0; f < ExternalShellBlockReplacementFamilies.Length; f++)
            {
                var family = ExternalShellBlockReplacementFamilies[f];

                for (var m = 0; m < family.Count; m++)
                {
                    var member = family[m];
                    if (f == 0)
                    {
                        stringBuilderSides.Append("• ");
                        stringBuilderSides.AppendLine(Path.GetFileNameWithoutExtension(member));

                    }
                    else if (f == 1)
                    {
                        stringBuilderTops.Append("• ");
                        stringBuilderTops.AppendLine(Path.GetFileNameWithoutExtension(member));
                    }
                }
            }

            replacementsSides = stringBuilderSides.ToString();
            replacementsTops = stringBuilderTops.ToString();
        }

        private void TellPlayerReplacementUsage(IPlayer iplayer, bool wrongPrefabName = false)
        {
            string replacementsSides;
            string replacementsTops;

            GetValidReplacementsStrings(out replacementsSides, out replacementsTops);

            iplayer.Reply(MSG(wrongPrefabName ? MSG_REPLACE_USAGE_PREFAB_NOT_FOUND : MSG_REPLACE_USAGE, iplayer.Id, replacementsSides, replacementsTops));
        }

        private void CommandReplace(IPlayer iplayer, string command, string[] args)
        {
            if (!HasReplacePermission(iplayer))
            {
                iplayer.Reply(MSG(MSG_NO_PERMISSION_REPLACE, iplayer.Id));
                return;
            }

            BasePlayer player;

            RaycastHit info;

            if (!ReplaceOrCheckCommon(iplayer, command, out player, out info))
            {
                return;
            }

            if (args.Length == 0)
            {
                TellPlayerReplacementUsage(iplayer);
                return;
            }

            var replaceWithShortname = args[0];

            string replaceWithFullName; //we're gonna need it later

            if (!ExternalShellBlockReplacementsShortnameToFull.TryGetValue(replaceWithShortname, out replaceWithFullName))
            {
                TellPlayerReplacementUsage(iplayer, true);
                return;
            }

            var maybeBuildingBlock = RaycastHitEx.GetEntity(info) as BuildingBlock;

            if (maybeBuildingBlock == null)
            {
                iplayer.Reply(MSG(MSG_REPLACE_NOT_LOOKING_AT_BUILDING_BLOCK, player.UserIDString));
                return;
            }

            var lookingAtShortName = maybeBuildingBlock.ShortPrefabName;

            string lookingAtFullName;

            if (!ExternalShellBlockReplacementsShortnameToFull.TryGetValue(lookingAtShortName, out lookingAtFullName))
            {
                iplayer.Reply(MSG(MSG_REPLACE_NOT_LOOKING_AT_TOP_OR_SIDE, player.UserIDString));
                return;
            }

            //check if it belongs to a pocket dimension

            DimensionalPocket maybeShellBlockPocket; //gonna need it later

            if (!DimensionalPocket.ServerCacheShellBlockToPocket.TryGetValue(maybeBuildingBlock.net.ID.Value, out maybeShellBlockPocket))
            {
                iplayer.Reply(MSG(MSG_REPLACE_NOT_EXTERNAL_SHELL_BLOCK, player.UserIDString));
                return;
            }

            //confirm it's not already that!

            if (lookingAtShortName == replaceWithShortname)
            {
                iplayer.Reply(MSG(MSG_REPLACE_SHELL_BLOCK_ALREADY_THAT, player.UserIDString, lookingAtShortName));
                return;
            }

            //ensure that the replacement is valid for that!

            if (!ExternalShellBlockReplacements[lookingAtFullName].Contains(replaceWithFullName))
            {
                iplayer.Reply(MSG(MSG_REPLACE_INVALID_REPLACEMENT, player.UserIDString));
                return;
            }

            //make sure there's nothing inside.

            bool foundOccupiedSocket = false;

            var entLinks = maybeBuildingBlock.GetEntityLinks();

            for (var i = 0; i < entLinks.Count; i++)
            {
                if (entLinks[i] == null)
                {
                    continue;
                }

                if (entLinks[i].socket == null)
                {
                    continue;
                }


                var socketName = entLinks[i].socket.socketName;


                if (!ExternalShellBlockReplacementsSocketsToCheck[lookingAtFullName].Contains(socketName))
                {
                    //this socket is not on a list of sockets inside of which it would matter
                    continue;
                }

                if (!entLinks[i].IsFemale())
                {
                    //no boys allowed
                    continue;
                }

                if (!entLinks[i].IsOccupied())
                {
                    continue;
                }

                //socket matters, and it's occupied.
                iplayer.Reply(MSG(MSG_REPLACE_SOMETHING_INSIDE_SOCKET, player.UserIDString, lookingAtShortName, socketName));
                foundOccupiedSocket = true;
                break;
            }

            if (foundOccupiedSocket)
            {
                return;
            }

            //remember important stuff...

            ulong rememberSkin = maybeBuildingBlock.skinID;
            ulong rememberOwner = maybeBuildingBlock.OwnerID;
            uint rememberColour = maybeBuildingBlock.customColour;

            var rememberGrade = maybeBuildingBlock.grade;

            Vector3 rememberPosition = maybeBuildingBlock.transform.position;
            Quaternion rememberRotation = maybeBuildingBlock.transform.rotation;

            BaseEntity.Flags rememberFlags = maybeBuildingBlock.flags;

            float rememberHealthFraction = maybeBuildingBlock.healthFraction;

            //remove from ShellBlocks, individual cache and global, so killing it won't make the pocket panic

            maybeShellBlockPocket.ShellBlocks.Remove(maybeBuildingBlock.net.ID.Value);
            DimensionalPocket.ServerCacheShellBlockToPocket.Remove(maybeBuildingBlock.net.ID.Value);

            maybeBuildingBlock.Kill(BaseNetworkable.DestroyMode.None);

            //look into water bases have things are built.

            var replacementBuildingBlock = GameManager.server.CreateEntity(replaceWithFullName, rememberPosition, rememberRotation, true) as BuildingBlock;
            replacementBuildingBlock.OwnerID = rememberOwner;
            replacementBuildingBlock.skinID = rememberSkin;

            replacementBuildingBlock.AttachToBuilding(maybeShellBlockPocket.ThisBuildingID);

            replacementBuildingBlock.Spawn();

            replacementBuildingBlock.SetHealth(replacementBuildingBlock.MaxHealth() * rememberHealthFraction);
            replacementBuildingBlock.ChangeGradeAndSkin(rememberGrade, rememberSkin, true, true);
            replacementBuildingBlock.SetCustomColour(rememberColour);

            replacementBuildingBlock.flags = rememberFlags;

            MakeStable(replacementBuildingBlock, true, true, false); //don't. use previous health.

            replacementBuildingBlock.StopBeingDemolishable();
            //replacementBuildingBlock.StopBeingRotatable(); ah why the hell not. let them.

            replacementBuildingBlock.SendNetworkUpdate();

            maybeShellBlockPocket.ShellBlocks.Add(replacementBuildingBlock.net.ID.Value, replacementBuildingBlock);
            DimensionalPocket.ServerCacheShellBlockToPocket.Add(replacementBuildingBlock.net.ID.Value, maybeShellBlockPocket);

            Effect.server.Run(PREFAB_BUILD_EFFECT, replacementBuildingBlock, 0u, Vector3.zero, Vector3.zero);
        }

        private void CommandViabilityCheck(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player;
            BuildingBlock middleFoundation;
            BuildingBlock exitDoorway;
            string debugInfo;
            Vector3 problematicPosition;

            if (!CheckCommon(iplayer, command, out player, out middleFoundation, out exitDoorway, out debugInfo, out problematicPosition, args))
            {
                return;
            }

            iplayer.Reply($"Dimensional Pocket Viability Check Result: {debugInfo}");
        }

        [ConsoleCommand(ConversionGuiManager.CUI_NAME_BUTTON_AND_CMD)]
        private void CommandGuiButton(ConsoleSystem.Arg arg)
        {
            if (!arg.HasArgs())
            {
                return;
            }

            var player = arg.Player();

            if (player == null)
            {
                return;
            }

            if (player.HasPlayerFlag(FLAG_TOAST_TIMEOUT))
            {
                return;
            }

            ulong maybeNetID;

            if (!ulong.TryParse(arg.Args[0], out maybeNetID))
            {
                return;
            }

            ConversionGuiManager.PlayerPressConvertButton(player, maybeNetID);
        }

        private void CommandConvert(IPlayer iplayer, string command, string[] args)
        {
            BasePlayer player;
            BuildingBlock middleFoundation;
            BuildingBlock exitDoorway;
            string debugInfo;
            Vector3 problematicPosition;

            if (!CheckCommon(iplayer, command, out player, out middleFoundation, out exitDoorway, out debugInfo, out problematicPosition, args))
            {
                return;
            }

            var newPocket = ConvertAlreadyViableCheckedIntoPocketDimension(ReusableShellElementList, ReusableExtraBaseCombatEntities, middleFoundation, exitDoorway, true);

            var freshPortalNetID = newPocket.ThisEntityNetID;

            var newItem = MakeEmptyDimensionalBoxItem(1);

            TieItemToPortalNetID(freshPortalNetID, newItem);

            player.GiveItem(newItem, BaseEntity.GiveItemReason.PickedUp);

            iplayer.Reply(MSG(MSG_CONVERTED_SUCCESSFULLY));

        }

        private void CommandEmergencyCleanup(IPlayer iplayer, string command, string[] args)
        {
            Unloading = true; //as to not trigger panic mode
            InternalBehaviour.DetachEverythingInternal();
            Unloading = false;

            var findAllDecayables = BaseNetworkable.serverEntities.OfType<DecayEntity>().ToArray();

            int killed = 0;

            DecayEntity candidate;

            for (var e = 0; e < findAllDecayables.Length; e++)
            {
                candidate = findAllDecayables[e];

                if (!BaseNetworkableEx.IsValid(candidate))
                {
                    continue;
                }

                if (candidate.IsDestroyed)
                {
                    continue;
                }

                if (!IsEntityMarkedInternal(candidate))
                {
                    continue;
                }

                candidate.Kill(BaseNetworkable.DestroyMode.None);
                killed++;
            }

            iplayer.Reply($"Found and killed {killed} entities marked as internal");
        }

        private void CommandList(IPlayer iplayer, string command, string[] args)
        {
            if (DimensionalPocket.ServerCachePortal.Count == 0)
            {
                iplayer.Reply("There's currently no Pocket Dimensions on the server.");
                return;
            }


            StringBuilder buildResponse = new StringBuilder();
            DimensionalPocket thisPocket;

            ulong lastFound = 0;

            if (args.Length == 0)
            {
                buildResponse.Append("There's currently ");
                buildResponse.Append(DimensionalPocket.ServerCachePortal.Count);
                buildResponse.Append(" Pocket Dimensions on the server:\n");

                var iterateOver = DimensionalPocket.ServerCachePortal.Values.ToArray();

                for (var i = 0; i < iterateOver.Length; i++)
                {
                    thisPocket = iterateOver[i];

                    buildResponse.Append('•');
                    buildResponse.Append(' ');
                    buildResponse.Append(thisPocket.ThisEntityNetID);

                    lastFound = thisPocket.ThisEntityNetID;

                    buildResponse.Append(" @ pos. ");
                    buildResponse.Append(thisPocket.CenterPoint.ToString());
                    buildResponse.Append(" ; box deployed? ");
                    buildResponse.Append(thisPocket.PairedBox == null ? "no ; " : $"yes, @ pos. {thisPocket.PairedBox.transform.position} ; ");

                    buildResponse.Append("inside another dimension? ");

                    buildResponse.Append(thisPocket.ParentPocket == null ? "no ; " : $"yes, {thisPocket.ParentPocket.ThisEntityNetID} @ pos {thisPocket.ParentPocket.CenterPoint} ; ");

                    buildResponse.Append("\n\n");

                }

                buildResponse.Append($"For more actions on a specific Dimension, add the Dimension number as argument, and then follow it with any of the self-explanatory options below, like...\n /{CMD_LIST} {lastFound} {CMD_LIST_ARG_PLAYER_INFO}\n /{CMD_LIST} {lastFound} {CMD_LIST_ARG_TP_TO_BOX}\n /{CMD_LIST} {lastFound} {CMD_LIST_ARG_TP_TO_PORTAL}\n /{CMD_LIST} {lastFound} {CMD_LIST_ARG_RECLAIM_ITEM}\n /{CMD_LIST} {lastFound} {CMD_LIST_ARG_KILL_DIMENSION}\n");


                iplayer.Reply(buildResponse.ToString());
                return;
            }

            if (args.Length == 1)
            {
                buildResponse.Append(CMD_LIST_USAGE_FULL);
                iplayer.Reply(buildResponse.ToString());
                return;
            }

            ulong portalNetID;

            if (!ulong.TryParse(args[0], out portalNetID))
            {
                buildResponse.Append("This doesn't look like a valid positive integer");
                iplayer.Reply(buildResponse.ToString());
                return;
            }

            if (!DimensionalPocket.ServerCachePortal.TryGetValue(portalNetID, out thisPocket))
            {
                buildResponse.Append($"Could not find a Dimension with that number. Use the command `{CMD_LIST}` with no arguments to get a breakdown.");
                iplayer.Reply(buildResponse.ToString());
                return;
            }

            var player = iplayer.Object as BasePlayer;

            bool isNotPlayer = iplayer.IsServer || player == null;

            switch (args[1].ToLower())
            {
                default:
                    {
                        buildResponse.Append("Invalid option. Try one of the following: ");
                        buildResponse.Append(CMD_LIST_USAGE_PART_2);
                        iplayer.Reply(buildResponse.ToString());
                        return;
                    }
                case CMD_LIST_ARG_RECLAIM_ITEM:
                    {
                        if (isNotPlayer)
                        {
                            buildResponse.Append(CMD_LIST_REQUIRES_PLAYER);
                            iplayer.Reply(buildResponse.ToString());
                            return;
                        }

                        var reclaimedItem = MakeEmptyDimensionalBoxItem();
                        TurnIntoDimensionalBoxItem(reclaimedItem);

                        TieItemToPortalNetID(thisPocket.ThisEntityNetID, reclaimedItem);

                        if (thisPocket.ThisPortal.skinID != 0)
                        {
                            ReskinItem(reclaimedItem, thisPocket.ThisPortal.skinID);
                        }

                        player.GiveItem(reclaimedItem);
                        buildResponse.Append("You have reclaimed a Dimensional Box Item.");
                        iplayer.Reply(buildResponse.ToString());
                        return;
                    }
                case CMD_LIST_ARG_TP_TO_BOX:
                    {
                        if (thisPocket.PairedBox == null)
                        {
                            buildResponse.Append("This Dimension doesn't have a paired Box associated with it, try reclaiming and deploying it first");
                            iplayer.Reply(buildResponse.ToString());
                            return;
                        }

                        if (isNotPlayer)
                        {
                            buildResponse.Append(CMD_LIST_REQUIRES_PLAYER);
                            iplayer.Reply(buildResponse.ToString());
                            return;
                        }

                        var position = thisPocket.GetTeleportPosition(true, true);
                        TeleportPlayerTo(player, position);

                        buildResponse.Append("You have been teleported to the location of the Dimensional Box.");
                        iplayer.Reply(buildResponse.ToString());
                        return;
                    }
                case CMD_LIST_ARG_TP_TO_PORTAL:
                    {
                        if (isNotPlayer)
                        {
                            buildResponse.Append(CMD_LIST_REQUIRES_PLAYER);
                            iplayer.Reply(buildResponse.ToString());
                            return;
                        }

                        DimensionalPocket.TeleportPlayerToPortal(player, thisPocket.ThisPortal);

                        buildResponse.Append("You have been teleported to the location of the Pocket Dimension. IF YOU GET EJECTED BACK, GO INTO `noclip` MODE  BEFORE TELEPORTING!");
                        iplayer.Reply(buildResponse.ToString());
                        return;
                    }
                case CMD_LIST_ARG_KILL_DIMENSION:
                    {
                        thisPocket.LastKnownBoxPosition = player.transform.position + Vector3.up; //just to set in stone the random one in case it were null

                        thisPocket.HurtPocketDoDeath(KILL_PORTAL_REASON_MANUAL_ADMIN_CMD);

                        buildResponse.Append($"You have successfully killed the entire dimension (and any dimensions it might've contained inside). If there were any contents inside to be dropped, look for them around you.");
                        iplayer.Reply(buildResponse.ToString());
                        return;
                    }
                case CMD_LIST_ARG_PLAYER_INFO:
                    {
                        List<ulong> playerIDList = Facepunch.Pool.Get<List<ulong>>();

                        //first, add the exit doorway owner...
                        playerIDList.Add(thisPocket.ExitDoorway.OwnerID);

                        //then, if the box exists, owner of that box...

                        if (thisPocket.PairedBox != null)
                        {
                            var pairedBoxOwnerID = thisPocket.PairedBox.ThisStorageContainer.OwnerID;

                            if (!playerIDList.Contains(pairedBoxOwnerID))
                            {
                                playerIDList.Add(pairedBoxOwnerID);
                            }
                        }

                        //then, if there's a TC inside, all people authed on that TC
                        var maybeTC = thisPocket.ThisBuilding.GetDominatingBuildingPrivilege();

                        if (maybeTC != null)
                        {
                            if (!maybeTC.authorizedPlayers.IsNullOrEmpty())
                            {
                                foreach (var entry in maybeTC.authorizedPlayers)
                                {
                                    var authorisedPlayerID = entry.userid;

                                    if (!playerIDList.Contains(authorisedPlayerID))
                                    {
                                        playerIDList.Add(authorisedPlayerID);
                                    }
                                }
                            }
                        }

                        if (playerIDList.Count == 0)
                        {
                            buildResponse.Append("Could not find any steam IDs associated with that Dimension.");
                            Facepunch.Pool.FreeUnmanaged(ref playerIDList);
                            iplayer.Reply(buildResponse.ToString());
                            return;
                        }

                        buildResponse.Append("The following players were found associated with that Dimension:\n");

                        for (var i = 0; i< playerIDList.Count; i++)
                        {
                            var playerID = playerIDList[i].ToString();

                            var playerName = "[UNKNOWN]";

                            var maybePlayer = Instance.covalence.Players.FindPlayerById(playerID);

                            if (maybePlayer != null)
                            {
                                playerName = maybePlayer.Name;
                            }
                            buildResponse.Append('•');
                            buildResponse.Append(' ');
                            buildResponse.Append(playerName);
                            buildResponse.Append(" (");
                            buildResponse.Append(playerID);
                            buildResponse.AppendLine(") ");
                        }


                        Facepunch.Pool.FreeUnmanaged(ref playerIDList);
                        iplayer.Reply(buildResponse.ToString());
                        return;
                    }


            }
        }

        private void CommandLostFound(IPlayer iplayer, string command, string[] args)
        {
            iplayer.Reply($"WARNING: {CMD_LOST_FOUND} is deprecated and now redirects to {CMD_LIST} - use it instead.");
            CommandList(iplayer, command, args);
        }

        private void CommandGiveBox(IPlayer iplayer, string command, string[] args)
        {
            if (CopyPaste == null)
            {
                iplayer.Reply("CopyPaste is not loaded in.");
                return;
            }

            if (Configuration.SkinIDToCopyPasteData.IsNullOrEmpty())
            {
                iplayer.Reply("There are no custom skin-to-CopyPaste filename entries in your config.");
                return;
            }

            if (args.Length < (iplayer.IsServer ? 2 : 1))
            {
                StringBuilder buildList = new StringBuilder();

                foreach (var entry in Instance.Configuration.SkinIDToCopyPasteData)
                {
                    buildList.Append("    ");
                    buildList.Append(entry.Key);
                    buildList.Append(" : ");
                    buildList.Append(entry.Value.Filename);
                    buildList.Append("\n");
                }

                iplayer.Reply($"USAGE: {CMD_GIVE_BOX} [skinID/CopyPaste filename] [partial player name or full steam ID]. If a player executes it in the chat or console in-game and they don't specify a recipient, it will be given to the player executing this command. Executing from the server console requires specifying the player.\nHere's a list of available skin IDs and their corresponding CopyPaste filenames:\n\n{buildList}");
                return;
            }

            ulong boxSkin;


            string filename;

            if (!ulong.TryParse(args[0], out boxSkin))
            {
                //treat it as a string, then.

                if (!Configuration.FilenameToCopyPasteData.ContainsKey(args[0]))
                {
                    iplayer.Reply($"Your config does not contain a CopyPaste profile with the filename \"{args[0]}\"");

                    return;

                }
                else
                {
                    filename = Configuration.FilenameToCopyPasteData[args[0]].Filename;
                    boxSkin = Configuration.FilenameToCopyPasteData[args[0]].SkinID;
                }
            }
            else
            {
                if (!Configuration.SkinIDToCopyPasteData.ContainsKey(boxSkin))
                {
                    iplayer.Reply($"Your config does not contain a CopyPaste profile for the skin {boxSkin}");

                    return;
                }
                else
                {
                    filename = Configuration.SkinIDToCopyPasteData[boxSkin].Filename;
                }
            }

            if (!SpawnedPasteRequest.CopypasteFileExists(filename))
            {
                iplayer.Reply($"The CopyPaste file \"{filename}\" does not exist! Make sure it's in the /oxide/data/copypaste/ folder!");

                return;
            }

            BasePlayer playerToBequeef;

            if (args.Length > 1)
            {
                playerToBequeef = covalence.Players.FindPlayer(args[1])?.Object as BasePlayer ?? null;
            }
            else
            {
                playerToBequeef = iplayer.Object as BasePlayer;
            }

            if (playerToBequeef == null)
            {
                iplayer.Reply($"No player matching \"{args[1]}\" was found");
                return;
            }

            var newItem = ItemManager.Create(ItemDefinitionWoodStorageBox, 1, boxSkin);

            newItem.name = $"{MSG(MSG_POCKET_DIMENSION_ITEM_NAME)} \"{filename}\"";

            newItem.MarkDirty();

            playerToBequeef.GiveItem(newItem);

            iplayer.Reply($"{playerToBequeef.displayName} ({playerToBequeef.userID}) was given {newItem.name}");

        }

        #endregion

        #region DRAWING

        public const int CUBE_CORNER_LEFT_DOWN_BACK = 0;
        public const int CUBE_CORNER_LEFT_DOWN_FORWARD = 1;
        public const int CUBE_CORNER_LEFT_UP_BACK = 2;
        public const int CUBE_CORNER_LEFT_UP_FORWARD = 3;

        public const int CUBE_CORNER_RIGHT_DOWN_BACK = 4;
        public const int CUBE_CORNER_RIGHT_DOWN_FORWARD = 5;
        public const int CUBE_CORNER_RIGHT_UP_BACK = 6;
        public const int CUBE_CORNER_RIGHT_UP_FORWARD = 7;


        public const string TEXT_BULLET_SMALL = "<size=20>X</size>";
        public const string TEXT_BULLET_BIG = "<size=40>X</size>";

        public static bool PrePlayerDraw(BasePlayer player)
        {
            if (!player.HasPlayerFlag(BasePlayer.PlayerFlags.IsAdmin))
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, true);
                player.SendNetworkUpdateImmediate();

                return true;
            }

            return false;
        }

        public static void PostPlayerDraw(BasePlayer player, bool setAdmin)
        {
            if (setAdmin)
            {
                player.SetPlayerFlag(BasePlayer.PlayerFlags.IsAdmin, false);
                player.SendNetworkUpdateImmediate();
            }
        }
        public static void DrawText(BasePlayer player, float duration, Color color, Vector3 position, string text)
        {
            bool setAdmin = PrePlayerDraw(player);

            DrawTextCommon(player, duration, color, position, text);

            PostPlayerDraw(player, setAdmin);
        }

        public static void DrawTextCommon(BasePlayer player, float duration, Color color, Vector3 position, string text)
        {
            player.SendConsoleCommand("ddraw.text", duration, color, position, text);
        }

        public static Vector3[] GetObbCoordinates(OBB obb)
        {
            var result = new Vector3[8];

            result[0] = obb.GetPoint(-1F, -1F, -1F);
            result[1] = obb.GetPoint(-1F, -1F, 1F);
            result[2] = obb.GetPoint(-1F, 1F, -1F);
            result[3] = obb.GetPoint(-1F, 1F, 1F);

            result[4] = obb.GetPoint(1F, -1F, -1F);
            result[5] = obb.GetPoint(1F, -1F, 1F);
            result[6] = obb.GetPoint(1F, 1F, -1F);
            result[7] = obb.GetPoint(1F, 1F, 1F);

            return result;
        }

        public static void DrawCube(BasePlayer player, float duration, Color color, Vector3 center, Vector3[] eightPoints, bool alsoDrawCenterDot = true, bool centerDotIsBig = false)
        {
            bool setAdmin = PrePlayerDraw(player);

            DrawCubeCommon(player, duration, color, center, eightPoints, alsoDrawCenterDot, centerDotIsBig);

            PostPlayerDraw(player, setAdmin);
        }


        public static void DrawCubeCommon(BasePlayer player, float duration, Color color, Vector3 center, Vector3[] eightPoints, bool alsoDrawCenterDot = true, bool centerDotIsBig = false)
        {
            if (color.Equals(Color.clear))
            {
                return;
            }

            if (eightPoints == null)
            {
                return;
            }

            if (eightPoints.Length != 8)
            {
                return;
            }

            //draw 4 floor lines
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_LEFT_DOWN_BACK], eightPoints[CUBE_CORNER_RIGHT_DOWN_BACK]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_LEFT_DOWN_BACK], eightPoints[CUBE_CORNER_LEFT_DOWN_FORWARD]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_RIGHT_DOWN_FORWARD], eightPoints[CUBE_CORNER_RIGHT_DOWN_BACK]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_RIGHT_DOWN_FORWARD], eightPoints[CUBE_CORNER_LEFT_DOWN_FORWARD]);

            //draw 4 ceiling lines

            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_LEFT_UP_BACK], eightPoints[CUBE_CORNER_RIGHT_UP_BACK]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_LEFT_UP_BACK], eightPoints[CUBE_CORNER_LEFT_UP_FORWARD]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_RIGHT_UP_FORWARD], eightPoints[CUBE_CORNER_RIGHT_UP_BACK]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_RIGHT_UP_FORWARD], eightPoints[CUBE_CORNER_LEFT_UP_FORWARD]);

            //draw 4 columns

            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_LEFT_DOWN_BACK], eightPoints[CUBE_CORNER_LEFT_UP_BACK]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_RIGHT_DOWN_BACK], eightPoints[CUBE_CORNER_RIGHT_UP_BACK]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_LEFT_DOWN_FORWARD], eightPoints[CUBE_CORNER_LEFT_UP_FORWARD]);
            DrawLineCommon(player, duration, color, eightPoints[CUBE_CORNER_RIGHT_DOWN_FORWARD], eightPoints[CUBE_CORNER_RIGHT_UP_FORWARD]);

            if (alsoDrawCenterDot)
            {
                DrawTextCommon(player, duration, color, center, centerDotIsBig ? TEXT_BULLET_BIG : TEXT_BULLET_SMALL);
            }
        }


        public static void DrawLineCommon(BasePlayer player, float duration, Color color, Vector3 point1, Vector3 point2)
        {
            if (color.Equals(Color.clear))
            {
                return;
            }
            player.SendConsoleCommand("ddraw.line", duration, color, point1, point2);
        }

        #endregion

        #region COPYPASTE
        void OnEntitySpawned(StorageContainer entity)
        {
            if (Instance == null)
            {
                return;
            }

            if (entity == null)
            {
                return;
            }

            if (CopyPaste == null)
            {
                return;
            }

            if (entity.PrefabName != PREFAB_WOOD_STORAGE_BOX)
            {
                return;
            }

            if (entity.skinID == 0)
            {
                return;
            }

            if (!Configuration.SkinIDToCopyPasteData.ContainsKey(entity.skinID))
            {
                return;
            }

            Instance.timer.Once(0.1F, () =>
            {
                if (entity == null)
                {
                    return;
                }

                //if marked as internal, ignore - it's already been dealt with!

                if (IsEntityMarkedInternal(entity))
                {
                    return;
                }

                if (CopyPaste == null)
                {
                    return;
                }

                if (!Configuration.SkinIDToCopyPasteData.ContainsKey(entity.skinID))
                {
                    return;
                }

                var filename = Configuration.SkinIDToCopyPasteData[entity.skinID].Filename;

                if (!SpawnedPasteRequest.CopypasteFileExists(filename))
                {
                    return;
                }

                SpawnedPasteRequest.OnSkinnedRelevantNonInternalBoxSpawned(entity, filename, entity.skinID);

            });

        }
        public class SpawnedPasteRequest
        {
            public StorageContainer DeployedContainer;

            public string CopypasteFilename;

            public Vector3 PastedPosition;
            public float RotationCorrection;

            public List<BaseEntity> PastedEntities = null;

            public BasePlayer RequestingPlayer = null;

            public static Dictionary<Vector3, SpawnedPasteRequest> RequestByPosition;

            public const string COPYPASTE_PATH_FORMAT = "copypaste/{0}";

            public string FinishedReason = MSG(MSG_REQUEST_DEPLOY_SUCCESS);

            public GameTip.Styles FinishedToastStyle = GameTip.Styles.Blue_Long;

            public bool ShouldKillJustPastedEntities = false;

            public ulong SkinID;

            public static bool CopypasteFileExists(string filename)
            {
                if (Instance.CopyPaste == null)
                {
                    return false;
                }

                return Interface.Oxide.DataFileSystem.ExistsDatafile(string.Format(COPYPASTE_PATH_FORMAT, filename));
            }

            public static void OnSkinnedRelevantNonInternalBoxSpawned(StorageContainer freshlySpawnedSkinnedWoodenBox, string copypasteFilename, ulong skinID)
            {
                var yRot = 0F; // freshlySpawnedSkinnedWoodenBox.transform.eulerAngles.y;
                var flatRot = Quaternion.Euler(0F, yRot, 0F);
                var rndPos = GetRandomMiddleFoundationPosition(flatRot);

                if (RequestByPosition.ContainsKey(rndPos))
                {
                    return;
                }

                BasePlayer requestingPlayer = null;

                if (freshlySpawnedSkinnedWoodenBox.OwnerID != 0)
                {
                    var iplayer = Instance.covalence.Players.FindPlayerById(freshlySpawnedSkinnedWoodenBox.OwnerID.ToString());
                    if (iplayer != null)
                    {
                        requestingPlayer = iplayer.Object as BasePlayer;
                    }
                    
                }

                var newRequest = new SpawnedPasteRequest
                {
                    CopypasteFilename = copypasteFilename,
                    DeployedContainer = freshlySpawnedSkinnedWoodenBox,
                    RotationCorrection = yRot,
                    PastedPosition = rndPos,
                    RequestingPlayer = requestingPlayer,
                    SkinID = skinID
                };

                Interface.Call("TryPasteFromVector3", rndPos, yRot, copypasteFilename, ReusableCopypasteArgs, null, null);

                RequestByPosition.Add(rndPos, newRequest);

                //give it 10 seconds

                Instance.timer.Once(10F, () =>
                {
                    if (newRequest != null)
                    {
                        newRequest.FinishedReason = MSG(MSG_REQUEST_TIMEOUT);
                        newRequest.FinishedToastStyle = GameTip.Styles.Red_Normal;
                        newRequest.RequestFinished();
                    }
                });
                
            }

            public void OnRequestedSpecialPasteFinished(List<BaseEntity> pastedEntities)
            {
                if (DeployedContainer == null)
                {
                    FinishedReason = MSG(MSG_REQUEST_CONTAINER_NULL);
                    FinishedToastStyle = GameTip.Styles.Red_Normal;
                    ShouldKillJustPastedEntities = true;
                    RequestFinished();
                }

                PastedEntities = pastedEntities;

                if (RequestingPlayer == null)
                {
                    FinishedReason = MSG(MSG_REQUEST_PLAYER_NULL);
                    FinishedToastStyle = GameTip.Styles.Red_Normal;
                    ShouldKillJustPastedEntities = true;
                    RequestFinished();
                }

                if (!Instance.Configuration.SkinIDToCopyPasteData.ContainsKey(SkinID))
                {
                    FinishedReason = MSG(MSG_REQUEST_MISSING_ENTRY, null, SkinID);
                    FinishedToastStyle = GameTip.Styles.Red_Normal;
                    ShouldKillJustPastedEntities = true;
                    RequestFinished();
                }

                var skinData = Instance.Configuration.SkinIDToCopyPasteData[SkinID];

                bool authOnTC = skinData.AuthorizePlayerOnTC;
                bool authOnTurrets = skinData.AuthorizePlayerOnTurrets;
                bool authOnLocks = skinData.AuthorizePlayerOnLocks;

                OwnerIDHandling ownerIdHandling = skinData.AssignOwnerIDs;

                var steamIDList = Facepunch.Pool.Get<List<ulong>>();

                steamIDList.Add(RequestingPlayer.userID);

                if (skinData.AuthorizationIncludesTeammates)
                {
                    if (RequestingPlayer.Team != null)
                    {
                        if (!RequestingPlayer.Team.members.IsNullOrEmpty())
                        {
                            for (var p = 0; p < RequestingPlayer.Team.members.Count; p++)
                            {
                                ulong steamID = RequestingPlayer.Team.members[p];

                                if (steamID == RequestingPlayer.userID)
                                {
                                    continue; //because already added
                                }
                                steamIDList.Add(steamID);
                            }
                        }

                    }
                }

                BaseEntity asBaseEntity;

                BuildingPrivlidge asTC;
                BaseLock asBaseLock;
                KeyLock asKeyLock;
                CodeLock asCodeLock;

                AutoTurret asTurret;

                BuildingPrivlidge findTC = null;

                for (var i = 0; i < PastedEntities.Count; i++)
                {
                    asBaseEntity = PastedEntities[i];

                    switch (ownerIdHandling)
                    {
                        case OwnerIDHandling.AssignZeroID:
                            {
                                asBaseEntity.OwnerID = 0;
                            }
                            break;
                        case OwnerIDHandling.AssignDeployingPlayerID:
                            {
                                asBaseEntity.OwnerID = RequestingPlayer.userID;
                            }
                            break;
                    }

                    asTC = asBaseEntity as BuildingPrivlidge;

                    if (asTC != null)
                    {
                        if (findTC == null)
                        {
                            findTC = asTC;

                            //clear all original auths

                            if (authOnTC)
                            {
                                findTC.authorizedPlayers.Clear();

                                for (var p = 0; p < steamIDList.Count; p++)
                                {
                                    findTC.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                                    {
                                        ShouldPool = false,
                                        userid = steamIDList[p],
                                        username = "",
                                    });
                                }
                            }
                        }

                        continue;
                    }

                    if (authOnLocks)
                    {
                        asBaseLock = asBaseEntity as BaseLock;

                        if (asBaseLock != null)
                        {
                            asKeyLock = asBaseLock as KeyLock;

                            if (asKeyLock != null)
                            {
                                //only authorise the first entry on the list.
                                //because keylocks work by owner ID

                                asKeyLock.OwnerID = RequestingPlayer.userID;

                                continue;
                            }

                            asCodeLock = asBaseLock as CodeLock;

                            if (asCodeLock != null)
                            {

                                asCodeLock.whitelistPlayers.Clear();

                                for (var p = 0; p < steamIDList.Count; p++)
                                {
                                    asCodeLock.whitelistPlayers.Add(steamIDList[p]);
                                }

                            }

                            continue;
                        }
                    }

                    if (authOnTurrets)
                    {
                        asTurret = asBaseEntity as AutoTurret;

                        if (asTurret != null)
                        {
                            asTurret.authorizedPlayers.Clear();

                            for (var p = 0; p < steamIDList.Count; p++)
                            {
                                asTurret.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                                {
                                    ShouldPool = false,
                                    userid = steamIDList[p],
                                    username = "",
                                });
                            }
                        }
                    }

                    UpdateEntityNetworkStuff(asBaseEntity);
                }

                Facepunch.Pool.FreeUnmanaged(ref steamIDList);

                if (findTC == null)
                {
                    FinishedReason = MSG(MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE, null, CopypasteFilename, MSG(MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE_TC_MISSING)); 
                    FinishedToastStyle = GameTip.Styles.Red_Normal;
                    ShouldKillJustPastedEntities = true;
                    RequestFinished();
                    return;
                }

                BuildingBlock middleFoundation;
                BuildingBlock exitDoorway;
                string debugInfo;
                Vector3 problematicPosition;

                var viabilityTest = ShellViabilityCheck(findTC, out middleFoundation, out exitDoorway, out debugInfo, out problematicPosition, RequestingPlayer.UserIDString);

                if (!viabilityTest)
                {
                    FinishedReason = MSG(MSG_REQUEST_INVALID_COPYPASTE_STRUCTURE, RequestingPlayer.UserIDString, CopypasteFilename, debugInfo);
                    FinishedToastStyle = GameTip.Styles.Red_Normal;
                    ShouldKillJustPastedEntities = true;
                    RequestFinished();
                    return;
                }

                var newPocket = ConvertAlreadyViableCheckedIntoPocketDimension(ReusableShellElementList, ReusableExtraBaseCombatEntities, middleFoundation, exitDoorway, false);

                MarkEntityAsIndestructible(DeployedContainer);
                MarkEntityAsInternal(DeployedContainer);

                var newBox = DeployedContainer.gameObject.AddComponent<DimensionalBox>();

                newBox.PairToPocket(newPocket);

                RequestFinished();                
            }

            public void RequestFinished()
            {
                if (RequestByPosition.ContainsKey(PastedPosition))
                {
                    if (RequestingPlayer != null)
                    {
                        ToastPlayer(RequestingPlayer, FinishedReason, FinishedToastStyle);
                    }

                    RequestByPosition.Remove(PastedPosition);
                }

                if (PastedEntities != null)
                {
                    if (ShouldKillJustPastedEntities)
                    {
                        BaseEntity justPasted;

                        for (var i = 0; i < PastedEntities.Count; i++)
                        {
                            justPasted = PastedEntities[i];

                            if (justPasted == null)
                            {
                                continue;
                            }

                            if (justPasted.IsDestroyed)
                            {
                                continue;
                            }

                            justPasted.Kill(BaseNetworkable.DestroyMode.None);
                        }
                    }

                    PastedEntities = null;
                }

            }
        }

        void OnPasteFinished(List<BaseEntity> pastedEntities, string pastedFilename, IPlayer iplayer, Vector3 pastingPosition)
        {
            if (Instance == null)
            {
                return;
            }

            if (!SpawnedPasteRequest.RequestByPosition.ContainsKey(pastingPosition))
            {
                return;
            }

            var player = iplayer.Object as BasePlayer;

            SpawnedPasteRequest.RequestByPosition[pastingPosition].OnRequestedSpecialPasteFinished(pastedEntities);
        }
        #endregion

        #region STATIC HELPERS

        public static BoxCollider AddBoxCollider(GameObject parent, Vector3 size, Vector3 center, bool isTrigger)
        {
            BoxCollider collider = parent.AddComponent<BoxCollider>();
            collider.size = size;
            collider.center = center;
            collider.isTrigger = isTrigger;
            collider.contactOffset = 0.01F;

            return collider;
        }

        public static void TieItemToPortalNetID(ulong portalNetID, Item item)
        {
            item.text = portalNetID.ToString();
        }

        public static DimensionalPocket TryGetDimensionalPocketFromNonNullItem(Item item)
        {
            if (item.text.IsNullOrEmpty())
            {
                return null;
            }

            ulong netIDofPairedPortal;

            if (!ulong.TryParse(item.text, out netIDofPairedPortal))
            {
                return null;
            }

            if (netIDofPairedPortal == 0)
            {
                return null;
            }

            DimensionalPocket result;

            if (!DimensionalPocket.ServerCachePortal.TryGetValue(netIDofPairedPortal, out result))
            {
                result = null;
            }

            return result;
        }

        /*
        public static ulong GetUlongFromTwoInts(int higherBits, int lowerBits)
        {
            ulong higher = (ulong)higherBits << 32;
            ulong lower = (uint)lowerBits; // uint is used to avoid sign extension
            ulong result = higher | lower;
            return result;
        }

        public static void SetTwoIntsBackFromUlong(ulong value, ref int higherBits, ref int lowerBits)
        {
            higherBits = (int)(value >> 32);
            lowerBits = (int)(value & 0xFFFFFFFF);
        }*/

        public static bool PositionWithinDimensionLayer(Vector3 position)
        {
            if (position.y < Instance.Configuration.DimensionalPocketsActualAltitudeMin)
            {
                return false;
            }

            if (position.y > Instance.Configuration.DimensionalPocketsActualAltitudeMax)
            {
                return false;
            }

            return true;
        }

        public static class PositionCheckResult
        {
            public static DimensionalPocket BottomPocketFound;
            public static uint LevelsTraversed;
            public static bool ShortCircuitDetected;
            public static bool NoMoreDepthAllowed;
            public static bool PocketIsDeployed;
            public static bool NothingAround;
            public static bool BuildingFoundIsNotPocketDimension;

            public static void Reset()
            {
                BottomPocketFound = null;
                LevelsTraversed = 0;
                ShortCircuitDetected = false;
                NoMoreDepthAllowed = false;
                PocketIsDeployed = false;
                NothingAround = false;
                BuildingFoundIsNotPocketDimension = false;
            }
        }


        public static void GetBottomMostPocketRecursively(DimensionalPocket startedFrom, DimensionalPocket currentPocket, bool justStarting, DimensionalPocket cannotLeadBackTo = null)
        {
            if (justStarting)
            {
                PositionCheckResult.LevelsTraversed = 1;
                PositionCheckResult.ShortCircuitDetected = false;

                currentPocket = startedFrom;
            }
            else
            {
                PositionCheckResult.LevelsTraversed++;

                PositionCheckResult.NoMoreDepthAllowed = PositionCheckResult.LevelsTraversed + 1 > Instance.Configuration.DimensionalPocketsMaxInceptionDepth;

                if (currentPocket == startedFrom || (cannotLeadBackTo != null && currentPocket == cannotLeadBackTo))
                {
                    PositionCheckResult.ShortCircuitDetected = true;
                }

                if (PositionCheckResult.ShortCircuitDetected)
                {
                    PositionCheckResult.BottomPocketFound = currentPocket;
                    return;
                }
            }

            if (currentPocket.ParentPocket == null)
            {
                PositionCheckResult.BottomPocketFound = currentPocket;
                return;
            }

            GetBottomMostPocketRecursively(startedFrom, currentPocket.ParentPocket, false, cannotLeadBackTo);
            
        }

        public static void CheckPositionResultDeep(Vector3 position, DimensionalPocket cannotShortCircuitTo = null)
        {
            PositionCheckResult.Reset();

            var obb = new OBB(position, Vector3.one * 20F, Quaternion.identity);

            VisEntitiesUniqueInsideOBB<BuildingBlock>(obb);

            if (ReusableVisibilityList.Any())
            {
                //something found. Is it a pocket dimension, or something yet different...

                var firstBuildingBlock = ReusableVisibilityList.First() as BuildingBlock;

                if (DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(firstBuildingBlock.buildingID))
                {
                    //yes it's a pocket dimension. GetBottomRecursively will set the LastCheckPositionPocketFound - either to itself or its parent.
                    //it will detect too deep / short circuits
                    //if it's not deployed, you need to go back and inform the player.

                    var originalStartingFrom = DimensionalPocket.ServerCacheBuildingIDToPocket[firstBuildingBlock.buildingID];

                    GetBottomMostPocketRecursively(originalStartingFrom, originalStartingFrom, true, cannotShortCircuitTo);

                    if (PositionCheckResult.BottomPocketFound != null)
                    {
                        PositionCheckResult.PocketIsDeployed = PositionCheckResult.BottomPocketFound.IsPairedToValidEntity();
                    }
                }
                else
                {
                    PositionCheckResult.BuildingFoundIsNotPocketDimension = true;
                }
            }
            else
            {
                PositionCheckResult.NothingAround = true;
            }
        }
        
        public static bool IsPlayerNPC(BasePlayer player)
        {
            if (player.IsNpc)
            {
                return true;
            }

            return !(player.userID >= 76560000000000000L || player.userID <= 0L);
        }

        public static Vector3 AlignWorldPosToMapCells(Vector3 vectorToQuantize)
        {
            return new Vector3(AlignCoordinateToMapCells(vectorToQuantize.x, 80F, true), AlignCoordinateToMapCells(vectorToQuantize.y, 100F, true), AlignCoordinateToMapCells(vectorToQuantize.z, 80F, true));
        }

        public static float AlignCoordinateToMapCells(float valueToQuantize, float quantification, bool addHalfQuantificationAfterwards)
        {
            return Mathf.Floor(valueToQuantize / quantification) * quantification + (addHalfQuantificationAfterwards ? quantification/2F : 0F);
        }

        public static bool IsTCWaterBaseTC(BuildingPrivlidge tc)
        {
            return tc.skinID == SKIN_WATERBASE_TC;
        }

        public static void ReskinItem(Item item, ulong wantedSkinID)
        {
            item.skin = wantedSkinID;
            item.MarkDirty();

            var heldEntity = item.GetHeldEntity();

            if (heldEntity != null)
            {
                heldEntity.skinID = wantedSkinID;
                heldEntity.SendNetworkUpdate();
            }
        }

        public static bool IsEscapeBlocked(BasePlayer player)
        {
            if (Instance.NoEscape == null)
            {
                return false;
            }

            var tryHook = Interface.Call("IsEscapeBlocked", player.UserIDString);

            if (tryHook is bool)
            {
                return tryHook.Equals(true);
            }

            return false;
        }

        public static bool HasAdminPermission(IPlayer player) => HasPermission(player, PERM_ADMIN);
        public static bool HasAdminPermission(BasePlayer player) => HasPermission(player, PERM_ADMIN);

        public static bool HasReplacePermission(IPlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_REPLACE);
        public static bool HasReplacePermission(BasePlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_REPLACE);

        public static bool HasRespawnInPocketPermission(IPlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_RESPAWN_IN_POCKET);
        public static bool HasRespawnInPocketPermission(BasePlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_RESPAWN_IN_POCKET);

        public static bool HasDeployPickupPermission(IPlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_DEPLOY_PICKUP);
        public static bool HasDeployPickupPermission(BasePlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_DEPLOY_PICKUP);

        public static bool HasEnterExitPermission(IPlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_ENTER_EXIT);
        public static bool HasEnterExitPermission(BasePlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_ENTER_EXIT);

        public static bool HasConvertTCPermission(IPlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_CONVERT_TC);
        public static bool HasConvertTCPermission(BasePlayer player) => HasPermission(player, PERM_ALL) || HasPermission(player, PERM_CONVERT_TC);


        public static bool HasPermission(IPlayer player, string perm)
        {
            if (Instance.permission.UserHasPermission(player.Id, PERM_ADMIN))
            {
                return true;
            }

            return Instance.permission.UserHasPermission(player.Id, perm);
        }

        public static bool HasPermission(BasePlayer player, string perm)
        {
            /*
            if (Instance.permission.UserHasPermission(player.UserIDString, PERM_ADMIN))
            {
                return true;
            }*/

            return Instance.permission.UserHasPermission(player.UserIDString, perm);
        }

        private static void BufferForVisEntitiesUnique(OBB bounds, int layerMask = -1, QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Collide)
        {
            layerMask = GamePhysics.HandleIgnoreCollision(bounds.position, layerMask);
            int num = ReusableColCount;

            ReusableColCount = Physics.OverlapBoxNonAlloc(bounds.position, bounds.extents, ReusableColBuffer, bounds.rotation, layerMask, triggerInteraction);
            for (int i = ReusableColCount; i < num; i++)
            {
                ReusableColBuffer[i] = null;
            }

            if (ReusableColCount >= ReusableColBuffer.Length)
            {
                UnityEngine.Debug.LogWarning("Vis query is exceeding collider buffer length.");
                ReusableColCount = ReusableColBuffer.Length;
            }
        }

        public static void VisEntitiesUnique<T>(OBB obb, ListHashSet<BaseEntity> list, int layerMask, QueryTriggerInteraction interaction = QueryTriggerInteraction.Ignore) where T : BaseEntity
        {
            BufferForVisEntitiesUnique(obb, layerMask, interaction);
            for (int i = 0; i < ReusableColCount; i++)
            {
                Collider collider = ReusableColBuffer[i];
                if (!(collider == null) && collider.enabled)
                {
                    T val = GameObjectEx.ToBaseEntity(collider) as T;
                    if (val != null)
                    {
                        if (!list.Contains(val))
                        {
                            list.Add(val);
                        }
                    }
                }
            }
        }

        public static void VisEntitiesUniqueInsideOBB<T>(OBB obb) where T: BaseEntity
        {
            ReusableVisibilityList.Clear();

            //ReusableOBB.position = position;
            //ReusableOBB.rotation = rotation;

            VisEntitiesUnique<T>(obb, ReusableVisibilityList, LAYERMASK_INTERESTING_ENTITIES, QueryTriggerInteraction.Ignore);
        }

        private static readonly Vector3 westbound = new Vector3(-4.5F, 0F, 0F);
        private static readonly Vector3 northbound = new Vector3(0F, 0F, 4.5F);
        private static readonly Vector3 eastbound = new Vector3(4.5F, 0F, 0F);
        private static readonly Vector3 southbound = new Vector3(0F, 0F, -4.5F);

        //2 bottom neighbours on level 1...
        private static readonly Vector3 nNorthSouth0 = new Vector3(-3F, 0F, 0F);
        private static readonly Vector3 nNorthSouth1 = new Vector3(3F, 0F, 0F);

        //3 neighbours on level 2...
        private static readonly Vector3 nNorthSouth2 = new Vector3(-3F, 3F, 0F);
        private static readonly Vector3 nNorthSouth3 = new Vector3(0F, 3F, 0F);
        private static readonly Vector3 nNorthSouth4 = new Vector3(3F, 3F, 0F);

        //3 neighbours on level 3...
        private static readonly Vector3 nNorthSouth5 = new Vector3(-3F, 6F, 0F);
        private static readonly Vector3 nNorthSouth6 = new Vector3(0F, 6F, 0F);
        private static readonly Vector3 nNorthSouth7 = new Vector3(3F, 6F, 0F);


        //2 bottom neighbours on level 1...
        private static readonly Vector3 nWestEast0 = new Vector3(0F, 0F, -3F);
        private static readonly Vector3 nWestEast1 = new Vector3(0F, 0F, 3F);

        //3 neighbours on level 2...
        private static readonly Vector3 nWestEast2 = new Vector3(0F, 3F, -3F);
        private static readonly Vector3 nWestEast3 = new Vector3(0F, 3F, 0F);
        private static readonly Vector3 nWestEast4 = new Vector3(0F, 3F, 3F);

        //3 neighbours on level 3...
        private static readonly Vector3 nWestEast5 = new Vector3(0F, 6F, -3F);
        private static readonly Vector3 nWestEast6 = new Vector3(0F, 6F, 0F);
        private static readonly Vector3 nWestEast7 = new Vector3(0F, 6F, 3F);

        public static Vector3[] ReusableCardinalDirectionPositions;

        public static Vector3[][] ReusableNeighbourPosTable;

        public static List<BuildingBlock> ReusableFoundationList;
        public static List<BuildingBlock> ReusableFloorList;
        public static List<BuildingBlock> ReusableCombinedWallAndDoorwayList;

        public static List<BuildingBlock> ReusableShellElementList;

        public static List<BaseCombatEntity> ReusableExtraBaseCombatEntities;

        public static Dictionary<BuildingBlock, BuildingBlock> ReusableMatchFoundationToCeiling;

        public static GatherItemAmount[] ReusableGatherItemAmountArray;

        public static int ReusableGatherItemAmountArrayCount;

        //public static OBB ReusableOBB;

        public static ListHashSet<BaseEntity> ReusableVisibilityList;
        public static Collider[] ReusableColBuffer;
        public static int ReusableColCount = 0;

        public static string[] ReusableCopypasteArgs;

        public struct GatherItemAmount
        {
            public int GatherAmount;
            public Item GatherItem;
        }

        public static void PopulateTransformedReusables(BuildingBlock middleFoundation)
        {
            ReusableCardinalDirectionPositions[0] = middleFoundation.transform.TransformPoint(westbound);
            ReusableCardinalDirectionPositions[1] = middleFoundation.transform.TransformPoint(northbound);
            ReusableCardinalDirectionPositions[2] = middleFoundation.transform.TransformPoint(eastbound);
            ReusableCardinalDirectionPositions[3] = middleFoundation.transform.TransformPoint(southbound);

            ReusableNeighbourPosTable[0][0] = middleFoundation.transform.TransformPoint(westbound + nWestEast0);
            ReusableNeighbourPosTable[0][1] = middleFoundation.transform.TransformPoint(westbound + nWestEast1);
            ReusableNeighbourPosTable[0][2] = middleFoundation.transform.TransformPoint(westbound + nWestEast2);
            ReusableNeighbourPosTable[0][3] = middleFoundation.transform.TransformPoint(westbound + nWestEast3);
            ReusableNeighbourPosTable[0][4] = middleFoundation.transform.TransformPoint(westbound + nWestEast4);
            ReusableNeighbourPosTable[0][5] = middleFoundation.transform.TransformPoint(westbound + nWestEast5);
            ReusableNeighbourPosTable[0][6] = middleFoundation.transform.TransformPoint(westbound + nWestEast6);
            ReusableNeighbourPosTable[0][7] = middleFoundation.transform.TransformPoint(westbound + nWestEast7);

            ReusableNeighbourPosTable[1][0] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth0);
            ReusableNeighbourPosTable[1][1] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth1);
            ReusableNeighbourPosTable[1][2] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth2);
            ReusableNeighbourPosTable[1][3] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth3);
            ReusableNeighbourPosTable[1][4] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth4);
            ReusableNeighbourPosTable[1][5] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth5);
            ReusableNeighbourPosTable[1][6] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth6);
            ReusableNeighbourPosTable[1][7] = middleFoundation.transform.TransformPoint(northbound + nNorthSouth7);

            ReusableNeighbourPosTable[2][0] = middleFoundation.transform.TransformPoint(eastbound + nWestEast0);
            ReusableNeighbourPosTable[2][1] = middleFoundation.transform.TransformPoint(eastbound + nWestEast1);
            ReusableNeighbourPosTable[2][2] = middleFoundation.transform.TransformPoint(eastbound + nWestEast2);
            ReusableNeighbourPosTable[2][3] = middleFoundation.transform.TransformPoint(eastbound + nWestEast3);
            ReusableNeighbourPosTable[2][4] = middleFoundation.transform.TransformPoint(eastbound + nWestEast4);
            ReusableNeighbourPosTable[2][5] = middleFoundation.transform.TransformPoint(eastbound + nWestEast5);
            ReusableNeighbourPosTable[2][6] = middleFoundation.transform.TransformPoint(eastbound + nWestEast6);
            ReusableNeighbourPosTable[2][7] = middleFoundation.transform.TransformPoint(eastbound + nWestEast7);

            ReusableNeighbourPosTable[3][0] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth0);
            ReusableNeighbourPosTable[3][1] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth1);
            ReusableNeighbourPosTable[3][2] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth2);
            ReusableNeighbourPosTable[3][3] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth3);
            ReusableNeighbourPosTable[3][4] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth4);
            ReusableNeighbourPosTable[3][5] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth5);
            ReusableNeighbourPosTable[3][6] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth6);
            ReusableNeighbourPosTable[3][7] = middleFoundation.transform.TransformPoint(southbound + nNorthSouth7);
        }

        public static bool ShellViabilityCheck(BuildingPrivlidge tc, out BuildingBlock middleFoundation, out BuildingBlock theExternalDoorway, out string log, out Vector3 debugPosition, string involvedPlayerUserID = null)
        {
            log = MSG(MSG_VIABILITY_OK);
            debugPosition = default(Vector3);
            theExternalDoorway = null;
            middleFoundation = null;

            if (IsTCWaterBaseTC(tc))
            {
                debugPosition = tc.transform.position;
                log = MSG(MSG_VIABILITY_WATERBASES_NOT_SUPPORTED_YET, involvedPlayerUserID);
                return false;
            }

            var building = tc.GetBuilding();

            if (building.buildingPrivileges.Count > 1)
            {
                log = MSG(MSG_VIABILITY_TOO_MANY_TCS, involvedPlayerUserID);
                return false;
            }

            if (building.buildingBlocks.Count == 0)
            {
                log = MSG(MSG_VIABILITY_NO_BUILDING_BLOCKS, involvedPlayerUserID);
                return false;
            }


            BuildingBlock thisBuildingBlock;

            bool abortedEarly = false;

            Vector3 averageFoundationPosition = Vector3.zero;

            float minY = float.MaxValue;
            float maxY = float.MinValue;

            int countDoorways = 0;
            int countWalls = 0;

            ReusableFoundationList.Clear();
            ReusableFloorList.Clear();
            ReusableCombinedWallAndDoorwayList.Clear();
            ReusableShellElementList.Clear();

            for (var b = 0; b < building.buildingBlocks.Count; b++)
            {
                thisBuildingBlock = building.buildingBlocks[b];

                switch (thisBuildingBlock.PrefabName)
                {
                    case PREFAB_FOUNDATION_TRIANGLE:
                        {
                            log = MSG(MSG_VIABILITY_ONLY_SQUARE_FOUNDATIONS, involvedPlayerUserID);
                            debugPosition = thisBuildingBlock.transform.position;
                            abortedEarly = true;
                        }
                        break;
                    case PREFAB_FOUNDATION_SQUARE:
                        {
                            ReusableFoundationList.Add(thisBuildingBlock);

                            averageFoundationPosition += thisBuildingBlock.transform.position;

                            if (thisBuildingBlock.transform.position.y > maxY)
                            {
                                maxY = thisBuildingBlock.transform.position.y;
                            }

                            if (thisBuildingBlock.transform.position.y < minY)
                            {
                                minY = thisBuildingBlock.transform.position.y;
                            }
                        }
                        break;
                    case PREFAB_WALL_DOORWAY:
                        {
                            ReusableCombinedWallAndDoorwayList.Add(thisBuildingBlock);
                            countDoorways++;
                        }
                        break;
                    case PREFAB_WALL_WINDOW:
                    case PREFAB_WALL_FRAME:
                    case PREFAB_WALL:
                        {
                            ReusableCombinedWallAndDoorwayList.Add(thisBuildingBlock);
                            countWalls++;
                        }
                        break;
                    case PREFAB_FLOOR_FRAME:
                    case PREFAB_FLOOR_SQUARE:
                        {
                            ReusableFloorList.Add(thisBuildingBlock);
                        }
                        break;

                }

                if (abortedEarly)
                {
                    break;
                }

            }

            if (abortedEarly)
            {
                return false;
            }

            if (ReusableFoundationList.Count != 9)
            {
                log = MSG(MSG_VIABILITY_MUST_HAVE_9_FOUNDATIONS, involvedPlayerUserID, ReusableFoundationList.Count);
                return false;
            }

            if (ReusableFloorList.Count < 9)
            {
                log = MSG(MSG_VIABILITY_MUST_HAVE_AT_LEAST_9_FLOORS, involvedPlayerUserID, ReusableFloorList.Count);
                return false;
            }

            if (countDoorways < 1)
            {
                log = MSG(MSG_VIABILITY_MUST_HAVE_1_DOORWAY, involvedPlayerUserID);
                return false;
            }

            if (countWalls < 35)
            {
                log = MSG(MSG_VIABILITY_MUST_HAVE_AT_LEAST_35_WALLS, involvedPlayerUserID, countWalls);
                return false;
            }

            if (Mathf.Abs(maxY - minY) > 0.005F)
            {
                log = MSG(MSG_VIABILITY_FOUNDATIONS_MUST_BE_SAME_LEVEL, involvedPlayerUserID);
                return false;
            }

            averageFoundationPosition /= 9F;

            //find the foundation closest to the averageFoundation pos...


            middleFoundation = null;
            BuildingBlock middleCeiling = null;

            //if not found: it means the shape is invalid!

            ReusableMatchFoundationToCeiling.Clear();
            

            BuildingBlock consideredFloor;

            BuildingBlock foundMatchingFloor;

            abortedEarly = false;

            for (var f = 0; f < 9; f++)
            {
                bool isMiddle = false;
                //for each foundation:

                thisBuildingBlock = ReusableFoundationList[f];

                
                if (Vector3.Distance(thisBuildingBlock.transform.position, averageFoundationPosition) < 0.01F)
                {
                    middleFoundation = thisBuildingBlock;
                    isMiddle = true;
                }


                //1) try to match to a floor nearly exactly 9 meters above

                //no need for vis.entities, you already got the floors

                foundMatchingFloor = null;

                Vector3 expectedFloorPosition = thisBuildingBlock.transform.position + Vector3.up * 9F;

                for (var c = 0; c < ReusableFloorList.Count; c++)
                {
                    consideredFloor = ReusableFloorList[c];

                    if (Vector3.Distance(consideredFloor.transform.position, expectedFloorPosition) < 0.01F)
                    {
                        foundMatchingFloor = consideredFloor;

                        if (isMiddle)
                        {
                            middleCeiling = consideredFloor;
                        }

                        break;
                    }
                }

                if (foundMatchingFloor == null)
                {
                    log = MSG(MSG_VIABILITY_CEILING_MISSING, involvedPlayerUserID);
                    debugPosition = expectedFloorPosition;
                    abortedEarly = true;
                    break;
                }
                else
                {
                    //remove for further iteration
                    ReusableFloorList.Remove(foundMatchingFloor);

                    ReusableShellElementList.Add(thisBuildingBlock);
                    ReusableShellElementList.Add(foundMatchingFloor);                    

                    ReusableMatchFoundationToCeiling.Add(thisBuildingBlock, foundMatchingFloor);
                }

            }

            if (abortedEarly)
            {
                return false;
            }

            if (middleFoundation == null || middleCeiling == null)
            {
                log = MSG(MSG_VIABILITY_FOUNDATIONS_MUST_MATCH_CEILINGS, involvedPlayerUserID);
                return false;
            }

            /*
            if (IsEntityMarkedSpecific(middleFoundation))
            {
                log = MSG(MSG_VIABILITY_ALREADY_POCKET_DIMENSION, involvedPlayerUserID);
                return false;
            }*/
            //no. instead, check the cache.

            if (DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(middleFoundation.buildingID))
            {
                log = MSG(MSG_VIABILITY_ALREADY_POCKET_DIMENSION, involvedPlayerUserID);
                return false;
            }

            //any entities belonging to the building outside of the cube?


            PopulateTransformedReusables(middleFoundation);

            Vector3 expectedWallPosition;

            BuildingBlock blockConsidered;

            BuildingBlock neighbourConsidered;

            BuildingBlock blockMatched;

            BuildingBlock neighbourMatched;

            int doorwaysFound = 0;

            for (var p = 0; p < 4; p++)
            {
                expectedWallPosition = ReusableCardinalDirectionPositions[p];
                blockMatched = null;

                for (var c = 0; c < ReusableCombinedWallAndDoorwayList.Count; c++)
                {

                    blockConsidered = ReusableCombinedWallAndDoorwayList[c];

                    if (Vector3.Distance(blockConsidered.transform.position, expectedWallPosition) < 0.025F)
                    {
                        blockMatched = blockConsidered;
                        break;
                    }
                }

                if (blockMatched == null)
                {
                    log = MSG(MSG_VIABILITY_WALL_MISSING_AT_LEVEL, involvedPlayerUserID, 0);
                    debugPosition = expectedWallPosition;
                    abortedEarly = true;
                    break;
                }
                else
                {
                    //remove from list to make it easier later
                    ReusableCombinedWallAndDoorwayList.Remove(blockMatched);

                    ReusableShellElementList.Add(blockMatched);

                    if (blockMatched.PrefabName == PREFAB_WALL_DOORWAY)
                    {
                        doorwaysFound++;

                        if (doorwaysFound != 1)
                        {
                            log = MSG(MSG_VIABILITY_TOO_MANY_DOORWAYS, involvedPlayerUserID, doorwaysFound);
                            debugPosition = blockMatched.transform.position;
                            abortedEarly = true;
                            break;
                        }
                        else
                        {
                            theExternalDoorway = blockMatched;
                        }
                    }

                    //and now it's time for 8 neighbours!

                    //again, you don't know the orientation of the wall - it can be soft or hard sided.

                    Vector3 expectedNeighbourPos;

                    var iterateOver = ReusableNeighbourPosTable[p];

                    abortedEarly = false;

                    for (var n = 0; n < 8; n++)
                    {
                        int currentLevel = n == 0 || n == 1 ? 1 : (n == 2 || n == 3 || n == 4 ? 2 : 3);

                        neighbourMatched = null;

                        expectedNeighbourPos = iterateOver[n];                            

                        for (var c = 0; c < ReusableCombinedWallAndDoorwayList.Count; c++)
                        {
                            neighbourConsidered = ReusableCombinedWallAndDoorwayList[c];

                            var distance = Vector3.Distance(neighbourConsidered.transform.position, expectedNeighbourPos);             

                            if (distance < 0.025F)
                            {
                                neighbourMatched = neighbourConsidered;
                                break;
                            }
                        }

                        if (neighbourMatched == null)
                        {
                            log = MSG(MSG_VIABILITY_WALL_MISSING_AT_LEVEL, involvedPlayerUserID, currentLevel);
                            debugPosition = expectedNeighbourPos;
                            abortedEarly = true;
                            break;
                        }
                        else
                        {
                            //remove from list to make it easier later
                            ReusableCombinedWallAndDoorwayList.Remove(neighbourMatched);

                            ReusableShellElementList.Add(neighbourMatched);

                            if (neighbourMatched.PrefabName == PREFAB_WALL_DOORWAY)
                            {
                                doorwaysFound++;

                                if (doorwaysFound > 1)
                                {
                                    log = MSG(MSG_VIABILITY_TOO_MANY_DOORWAYS, involvedPlayerUserID, doorwaysFound);
                                    debugPosition = neighbourMatched.transform.position;
                                    abortedEarly = true;
                                    break;
                                }
                                else
                                {
                                    theExternalDoorway = neighbourMatched;
                                }
                            }

                        }

                        if (abortedEarly)
                        {
                            break;
                        }
                    }

                    if (abortedEarly)
                    {
                        break;
                    }
                }

                if (abortedEarly)
                {
                    break;
                }
            }

            if (abortedEarly)
            {
                return false;
            }

            if (theExternalDoorway == null)
            {
                log = MSG(MSG_VIABILITY_NO_EXTERNAL_DOORWAYS_FOUND, involvedPlayerUserID);
                return false;
            }

            //make sure there's no doors/vending machines on the external doorway!

            //check entityLinks.

            var entLinks = theExternalDoorway.GetEntityLinks();

            abortedEarly = false;

            for (var i = 0; i < entLinks.Count; i++)
            {
                if (entLinks[i] == null)
                {
                    continue;
                }

                if (entLinks[i].socket == null)
                {
                    continue;
                }

                if (!entLinks[i].IsFemale()) //no boys allowed
                {
                    continue;
                }

                if (!entLinks[i].IsOccupied())
                {
                    continue;
                }


                if (!(entLinks[i].socket.socketName == SOCKET_NAME_WALL_DOORWAY_DOORWAY_FEMALE_1 || entLinks[i].socket.socketName == SOCKET_NAME_WALL_DOORWAY_DOORWAY_FEMALE_2))
                {
                    continue;
                }

                abortedEarly = true;
                debugPosition = theExternalDoorway.transform.position;
                log = MSG(MSG_VIABILITY_DOOR_FOUND_IN_EXTERNAL_DOORWAY, involvedPlayerUserID);

                break;
            }

            if (abortedEarly)
            {
                return false;
            }

            BaseCombatEntity asBaseCombat;

            //check if there's anything tall, obstructing the space in front of the external doorway...

            var doorOBB = new OBB(theExternalDoorway.transform.position + DimensionalPocket.COLLIDER_BOX_OFFSET, DimensionalPocket.COLLIDER_BOX_SIZE, theExternalDoorway.transform.rotation);

            VisEntitiesUniqueInsideOBB<BaseEntity>(doorOBB);

            abortedEarly = false;

            BaseEntity currentlyConsideredEntity;
            
            //var debugPlayer = Instance.covalence.Players.FindPlayerById(involvedPlayerUserID).Object as BasePlayer;
            
            for (var i = 0; i < ReusableVisibilityList.Count; i++)
            {
                currentlyConsideredEntity = ReusableVisibilityList[i];

                if (currentlyConsideredEntity.EqualNetID(theExternalDoorway))
                {
                    continue;
                }

                asBaseCombat = ReusableVisibilityList[i] as BaseCombatEntity;

                if (asBaseCombat == null)
                {
                    continue;
                }

                //sofas are BaseVehicles. Who knew.

                if (asBaseCombat is BasePlayer || (asBaseCombat is BaseVehicle && !asBaseCombat.PrefabName.Contains("sofa")) || asBaseCombat is BaseNpc || asBaseCombat is BasePet)
                {
                    continue;
                }

                var entityOBB = currentlyConsideredEntity.WorldSpaceBounds();

                if (!doorOBB.Intersects(entityOBB))
                {
                    continue;
                }

                /*
                if (debugPlayer != null)
                {
                    DrawCube(debugPlayer, 20F, Color.green, doorOBB.position, GetObbCoordinates(doorOBB), true, true);

                    DrawCube(debugPlayer, 20F, Color.red, entityOBB.position, GetObbCoordinates(entityOBB), true, true);
                }*/

                log = $"{MSG(MSG_VIABILITY_SOMETHING_IN_FRONT_OF_DOORWAY, involvedPlayerUserID)}... {asBaseCombat.ShortPrefabName}";
                debugPosition = currentlyConsideredEntity.transform.position;
                abortedEarly = true;
                break;
            }

            if (abortedEarly)
            {
                return false;
            }

            //everything went better than expected? vis check!

            var obb9x9x9padded = new OBB(middleFoundation.transform.position + Vector3.up * 4.5F, Vector3.one * 9.33F, middleFoundation.transform.rotation);

            /*
            var debugPlayer = Instance.covalence.Players.FindPlayerById(involvedPlayerUserID).Object as BasePlayer;

            if (debugPlayer != null)
            {
                DrawCube(debugPlayer, 10F, Color.yellow, obb9x9x9padded.position, GetObbCoordinates(obb9x9x9padded), true, true);
            }*/

            VisEntitiesUniqueInsideOBB<BaseCombatEntity>(obb9x9x9padded);

            ReusableExtraBaseCombatEntities.Clear();

            int everythingInBuildingCount = building.decayEntities.Count;

            List<DecayEntity> everythingThatMattersInsideOBB = Facepunch.Pool.Get<List<DecayEntity>>();

            DecayEntity asDecayEntity;
            IOEntity asIoEntity;

            abortedEarly = false;

            bool containedInWhiteListCurrently;

            bool shouldCheckWhitelist = !Instance.Configuration.DimensionalPocketEntityStickingOutWhitelist.IsNullOrEmpty();

            for (var i = 0; i < ReusableVisibilityList.Count; i++)
            {
                //check if contained in the whitelist...
                var entity = ReusableVisibilityList[i];
                asDecayEntity = entity as DecayEntity;

                if (asDecayEntity == null)
                {
                    if (entity is BasePlayer || (entity is BaseVehicle && !entity.PrefabName.Contains("sofa")) || entity is BaseNpc || entity is BasePet)
                    {
                        continue;
                    }

                    //add to some basecombat extras

                    ReusableExtraBaseCombatEntities.Add(entity as BaseCombatEntity);

                    continue;
                }

                //special check for purified water storage
                //needs to be ignored here as it doesn't belong to the building

                if (entity.PrefabName == PREFAB_WATER_PURIFIER_STORAGE)
                {
                    ReusableExtraBaseCombatEntities.Add(entity as BaseCombatEntity);
                    continue;
                }

                asIoEntity = asDecayEntity as IOEntity;

                containedInWhiteListCurrently = false;

                if (shouldCheckWhitelist)
                {
                    containedInWhiteListCurrently = Instance.Configuration.DimensionalPocketEntityStickingOutWhitelist.Contains(entity.PrefabName);
                }

                bool ignoreAxisDistanceCheck = containedInWhiteListCurrently;

                if (asIoEntity != null)
                {
                    /*
                    if (asIoEntity is SolarPanel || asIoEntity is ElectricWindmill)
                    {
                        if (!Instance.Configuration.DimensionalPocketsAllowSolarAndWindmill)
                        {
                            abortedEarly = true;
                            debugPosition = asIoEntity.transform.position;
                            log = MSG(MSG_VIABILITY_NO_SOLAR_OR_WINDMILL_ALLOWED, involvedPlayerUserID);
                            break;
                        }
                        else
                        {
                            ignoreAxisDistanceCheck = true;
                        }
                    }
                    else
                    {
                        if (asIoEntity.ShortPrefabName.Contains("generator.s"))
                        {
                            if (!Instance.Configuration.DimensionalPocketsAllowTestGenerators)
                            {
                                abortedEarly = true;
                                debugPosition = asIoEntity.transform.position;
                                log = MSG(MSG_VIABILITY_NO_TEST_GENERATORS_ALLOWED, involvedPlayerUserID);
                                break;
                            }
                            else
                            {
                                ignoreAxisDistanceCheck = true;
                            }
                        }
                    }*/

                    IOEntity maybeAnotherIoEntity;

                    var iterateOver = asIoEntity.inputs.Concat(asIoEntity.outputs).ToArray();

                    for (var io = 0; io < iterateOver.Length; io++)
                    {
                        maybeAnotherIoEntity = iterateOver[io].connectedTo?.Get(true);
                        if (maybeAnotherIoEntity != null)
                        {
                            //verify that it exists inside reusable vis list
                            if (!ReusableVisibilityList.Contains(maybeAnotherIoEntity))
                            {
                                abortedEarly = true;
                                debugPosition = maybeAnotherIoEntity.transform.position;
                                log = MSG(MSG_VIABILITY_IO_CONNECTION_OUTSIDE_CUBE, involvedPlayerUserID);
                                break;
                            }
                        }
                    }

                    if (abortedEarly)
                    {
                        break;
                    }
                }

                everythingThatMattersInsideOBB.Add(asDecayEntity);

                if (ignoreAxisDistanceCheck)
                {
                    continue;
                }

                var decayEntityOBB = asDecayEntity.WorldSpaceBounds();

                var checkedPositionOnOBB = asDecayEntity.PrefabName.Contains("foundation") ? decayEntityOBB.GetPoint(0F, 1F, 0F) : decayEntityOBB.position;

                if (!obb9x9x9padded.Contains(checkedPositionOnOBB))
                {
                    /*
                    if (debugPlayer != null)
                    {
                        DrawText(debugPlayer, 10F, Color.red, checkedPositionOnOBB, "X");
                    }*/

                    abortedEarly = true;
                    debugPosition = checkedPositionOnOBB;
                    log = $"{MSG(MSG_VIABILITY_SOMETHING_STICKING_OUT, involvedPlayerUserID)}... {asDecayEntity.ShortPrefabName}";
                    break;
                }
                else
                {
                    /*
                    if (debugPlayer != null)
                    {
                        DrawText(debugPlayer, 10F, Color.green, checkedPositionOnOBB, "V");
                    }*/
                }

                

                if (asDecayEntity.buildingID != building.ID)
                {
                    asDecayEntity.AttachToBuilding(building.ID);

                    continue;
                }

                
            }

            if (abortedEarly)
            {
                //did you catch any io entities outside the cube?

                Facepunch.Pool.FreeUnmanaged(ref everythingThatMattersInsideOBB);
                return false;
            }

            var everythingThatMattersInsideOBBCount = everythingThatMattersInsideOBB.Count;

            if (everythingThatMattersInsideOBBCount != everythingInBuildingCount)
            {
                StringBuilder builder = new StringBuilder();

                //do we have extra entities in vis?
                if (everythingThatMattersInsideOBBCount > everythingInBuildingCount)
                {
                    foreach (var entry in everythingThatMattersInsideOBB)
                    {
                        if (!building.decayEntities.Contains(entry))
                        {
                            builder.Append(entry.ShortPrefabName);
                            builder.Append(',');
                            builder.Append(' ');
                        }
                    }

                    log = MSG(MSG_VIABILITY_MORE_ENTITIES_IN_VOLUME_THAT_BUILDING2, involvedPlayerUserID, everythingThatMattersInsideOBBCount, everythingInBuildingCount, builder.ToString());
                }
                else
                {
                    foreach (var entry in building.decayEntities)
                    {
                        if (!everythingThatMattersInsideOBB.Contains(entry))
                        {
                            builder.Append(entry.ShortPrefabName);
                            builder.Append(',');
                            builder.Append(' ');
                        }
                    }

                    log = MSG(MSG_VIABILITY_LESS_ENTITIES_IN_VOLUME_THAT_BUILDING2, involvedPlayerUserID, everythingThatMattersInsideOBBCount, everythingInBuildingCount, builder.ToString());
                }
                Facepunch.Pool.FreeUnmanaged(ref everythingThatMattersInsideOBB);
                return false;
            }

            Facepunch.Pool.FreeUnmanaged(ref everythingThatMattersInsideOBB);
            return true;
        }

        public const float SPECIAL_DAMAGE_AMOUNT = 123456F;

        public static void HurtToDeath(BaseCombatEntity asBaseCombat)
        {
            asBaseCombat.Hurt(SPECIAL_DAMAGE_AMOUNT, Rust.DamageType.Generic, null, false);
        }

        public static void PrintToDebug(string msg)
        {
            Instance.PrintError(msg);
        }

        public static void ToastPlayer(BasePlayer player, string toast, GameTip.Styles style = GameTip.Styles.Blue_Normal)
        {
            string toastKeyForPlayer = string.Format("{0}.{1}", toast, player.UserIDString);

            if (!ToastsCached.TryGetValue(toastKeyForPlayer, out var translatePhrase))
            {
                if (toast.Length > BREAK_TOASTS_EVERY_N_CHARACTERS)
                {
                    toast = SpliceText(toast, BREAK_TOASTS_EVERY_N_CHARACTERS);
                }

                translatePhrase = new Translate.Phrase(toastKeyForPlayer, toast);

                ToastsCached.Add(toastKeyForPlayer, translatePhrase);
            }

            player.ShowToast(style, translatePhrase, true);
        }

        public static string SpliceText(string inputText, int lineLength)
        {
            StringBuilder builder = new StringBuilder();

            string[] stringSplit = inputText.Split(' ');
            int charCounter = 0;

            for (int i = 0; i < stringSplit.Length; i++)
            {
                builder.Append(stringSplit[i]);
                builder.Append(' ');
                charCounter += stringSplit[i].Length;

                if (charCounter > lineLength)
                {
                    builder.Append('\n');
                    charCounter = 0;
                }
            }
            return builder.ToString();
        }

        public static float GetSlightDelay()
        {
            return UnityEngine.Random.Range(0.01F, 0.1F);
        }

        public static bool IsPositionWithinTemperatureRange(Vector3 position)
        {
            bool result;

            float restoreHour = TOD_Sky.Instance.Cycle.Hour;
            TOD_Sky.Instance.Cycle.Hour = 12F;

            float temperature = Climate.GetTemperature(position);

            if (temperature < Instance.Configuration.DimensionalPocketsLocationMinTemperature)
            {
                result = false;
            }
            else
            {
                if (temperature > Instance.Configuration.DimensionalPocketsLocationMaxTemperature)
                {
                    result = false;
                }
                else
                {
                    result = true;
                }
            }

            TOD_Sky.Instance.Cycle.Hour = restoreHour;
            return result;
        }

        public static Vector3 GetRandomMiddleFoundationPosition(Quaternion obbRotation)
        {
            Vector3 randomPos;

            bool obstacleFound;

            int failedTemperatureAttempts = 0;

            do
            {

                randomPos = Vector3Ex.Range(-TerrainMeta.Size.x / 2, TerrainMeta.Size.x / 2).WithY(UnityEngine.Random.Range(Instance.Configuration.DimensionalPocketsActualAltitudeMin, Instance.Configuration.DimensionalPocketsActualAltitudeMax));

                //make sure the random pos coords are rounded down to nearest hundred, with 50 added

                randomPos = AlignWorldPosToMapCells(randomPos); //to make sure it doesn't cross network group/layer bounds


                if (failedTemperatureAttempts < 64 && !IsPositionWithinTemperatureRange(randomPos))
                {
                    failedTemperatureAttempts++;
                    obstacleFound = true;
                }
                else
                {
                    var obb = new OBB(randomPos + Vector3.up * 4.5F, Vector3.one * 100F, obbRotation);

                    VisEntitiesUniqueInsideOBB<BaseEntity>(obb);

                    obstacleFound = ReusableVisibilityList.Count > 0;
                }

            }
            while (obstacleFound);

            return randomPos;
        }

        public static void SendClientSidedEntKillToEveryone(BaseEntity entity)
        {
            foreach (var entry in Network.Net.sv.connections)
            {
                //1.0.2

                /*
                if (Network.Net.sv.write.Start())
                {
                    Network.Net.sv.write.PacketID(Network.Message.Type.EntityDestroy);
                    Network.Net.sv.write.EntityID(entity.net.ID);
                    Network.Net.sv.write.Send(new Network.SendInfo(entry));
                }
                */

                //1.0.3

                /*
                if (Network.Net.sv.IsConnected())
                {
                    NetWrite netWrite = Network.Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.EntityDestroy);
                    netWrite.UInt32(entity.net.ID);
                    netWrite.Send(new SendInfo(entry));
                }*/

                //1.0.4
                /*
                if (entity.children != null)
                {
                    foreach (BaseEntity child in entity.children)
                    {
                        child.DestroyOnClient(entry);
                    }
                }*/
                if (Network.Net.sv.IsConnected())
                {
                    NetWrite netWrite = Network.Net.sv.StartWrite();
                    netWrite.PacketID(Message.Type.EntityDestroy);
                    netWrite.EntityID(entity.net.ID);
                    netWrite.UInt8(0);
                    netWrite.Send(new SendInfo(entry));
                }
            }
        }

        public static void UpdateEntityNetworkStuff(BaseEntity entity)
        {
            entity.transform.hasChanged = true;

            entity.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);

            SendClientSidedEntKillToEveryone(entity);


            entity.SendNetworkUpdateImmediate();
            entity.UpdateNetworkGroup();

            //this should recursively update hierarchy

            UpdateChildrenIfAny(entity);
        }

        public static bool StopHereIfNotInvokedByParentAndHasParentButOtherwiseChangePos(BaseCombatEntity entity, Vector3 newPosition, bool invokedByParent)
        {
            if (!invokedByParent)
            {
                if (entity.HasParent())
                {
                    return true;
                }

                //mount check
                var asMount = entity as BaseMountable;

                if (asMount != null)
                {
                    asMount.DismountAllPlayers();
                }

                //only change position to new if not invoked by the parent.
                //if invoked by parent, we're interested in the update stuff and nothing else
                entity.transform.position = newPosition;
            }

            return false;
        }
        public static void TeleportBaseCombatEnityDuringConversion(BaseCombatEntity entity, Vector3 newPosition, bool invokedByParentSoNoPosChangeJustUpdate = false)
        {
            if (StopHereIfNotInvokedByParentAndHasParentButOtherwiseChangePos(entity, newPosition, invokedByParentSoNoPosChangeJustUpdate))
            {
                return;
            }

            UpdateEntityNetworkStuff(entity);
        }
        public static void TeleportDecayEntityDuringConversion(DecayEntity entity, Vector3 newPosition, bool invokedByParent = false)
        {
            if (StopHereIfNotInvokedByParentAndHasParentButOtherwiseChangePos(entity, newPosition, invokedByParent))
            {
                return;
            }

            var buildingBlock = entity as BuildingBlock;

            if (buildingBlock != null)
            {
                buildingBlock.SetGrade(buildingBlock.grade);
                buildingBlock.SetHealth(buildingBlock.health);
            }

            entity.ClientRPC(RpcTarget.NetworkGroup("RefreshSkin", entity));

            if (buildingBlock != null)
            {
                buildingBlock.UpdateSkin(true);
            }

            if (!Instance.Configuration.DimensionaPocketsAllowVendingMachineBroadcast)
            {
                var asVendingMachine = entity as VendingMachine;

                if (asVendingMachine != null)
                {
                    Instance.OnEntitySpawned(asVendingMachine);
                }
            }

            UpdateEntityNetworkStuff(entity);



        }

        public static void UpdateChildrenIfAny(BaseEntity entity)
        {
            if (entity.children.IsNullOrEmpty())
            {
                return;
            }

            for (var i = 0; i < entity.children.Count; i++)
            {
                var child = entity.children[i];

                var childAsDecayEntity = child as DecayEntity;

                if (childAsDecayEntity != null)
                {
                    TeleportDecayEntityDuringConversion(childAsDecayEntity, Vector3.zero, true); //position doesn't matter here
                }
                else
                {
                    var childAsBaseCombat = child as BaseCombatEntity;

                    if (childAsBaseCombat != null)
                    {
                        TeleportBaseCombatEnityDuringConversion(childAsBaseCombat, Vector3.zero, true); //neither does it here
                    }
                }

            }
            
        }

        public static void TeleportEntity(BaseEntity entity, Vector3 pos, bool makeItDieAfterwards)
        {
            //make it stable so it doesn't freak out when stuff under it goes missing first
            MakeStable(entity, false, false);

            //and then do THIS with slight delay...

            entity.Invoke(() =>
            {
                if (entity != null && !entity.IsDestroyed)
                {

                    var rnd = UnityEngine.Random.insideUnitSphere;

                    entity.transform.position = pos + rnd.WithY(Mathf.Abs(rnd.y));
                    entity.transform.hasChanged = true;
                    entity.SendNetworkUpdateImmediate();

                    if (makeItDieAfterwards)
                    {
                        var asBaseCombat = entity as BaseCombatEntity;

                        if (asBaseCombat != null)
                        {
                            //hurt it to death so it drops whatever it needs to drop at the proper location
                            HurtToDeath(asBaseCombat);
                        }
                    }
                }

            }, GetSlightDelay());
        }

        public static void TeleportPlayerTo(BasePlayer player, Vector3 position, Vector3 viewAngles = default(Vector3))
        {
            if (Instance.Configuration.EffectsPlayerTeleportEnable)
            {
                Effect.server.Run(Instance.Configuration.EffectPlayerTeleportDisappear, player.transform.position, Vector3.up);
            }

            player.EnsureDismounted();
            player.EndLooting();
            player.Server_CancelGesture();

            player.PauseFlyHackDetection();
            player.PauseSpeedHackDetection();

            if (player.HasParent())
            {
                player.SetParent(null, true, true);
            }

            player.RemoveFromTriggers();

            player.SetPlayerFlag(FLAG_IMMUNE_AFTER_TELEPORT, true);
            player.Invoke(() => player.SetPlayerFlag(FLAG_IMMUNE_AFTER_TELEPORT, false), Mathf.Max(0.5F, Instance.Configuration.DimensionalPocketsTrapImmunityAfterTeleport));
            player.ClientRPC(RpcTarget.Player("StartLoading_Quick", player), true);

            player.Teleport(position);
            player.ForceUpdateTriggers();

            if (Instance.Configuration.EffectsPlayerTeleportEnable)
            {
                Effect.server.Run(Instance.Configuration.EffectPlayerTeleportAppear, position, Vector3.up);
            }

            if (viewAngles != default(Vector3))
            {
                player.ClientRPC(RpcTarget.Player("ForceViewAnglesTo", player), viewAngles);
            }

            player.UpdateNetworkGroup();
            player.SetPlayerFlag(BasePlayer.PlayerFlags.ReceivingSnapshot, true);
            player.SendNetworkUpdateImmediate();
        }

        public static BaseEntity SpawnStableAt(string prefabName, Vector3 position, bool markInternal, bool markIndestructible, ulong withOwnerID = 0, Quaternion rotation = default(Quaternion))
        {
            var newEntity = GameManager.server.CreateEntity(prefabName, position, rotation, true);
            newEntity.OwnerID = withOwnerID;
            newEntity.Spawn();
           
            MakeStable(newEntity, markInternal, markIndestructible);

            return newEntity;
        }

        //returns the net ID of the fresh portal - it goes in the cache
        public static DimensionalPocket ConvertAlreadyViableCheckedIntoPocketDimension(List<BuildingBlock> allShellElements, List<BaseCombatEntity> extraCombatEntities, BuildingBlock middleFoundation, BuildingBlock exitDoorway, bool teleportEntitiesUp)
        {
            //MakeStable(tc, true, false);

            MarkEntityAsSpecific(middleFoundation);
            MarkEntityAsSpecific(exitDoorway);

            for (var i = 0; i< allShellElements.Count; i++)
            {                
                BuildingBlock thisShellElement = allShellElements[i];
                MakeStable(thisShellElement, true, false); //or just mark as internal?

                thisShellElement.StopBeingDemolishable();
            }

            var buildingTouse = middleFoundation.GetBuilding();

            var delta = teleportEntitiesUp ? (GetRandomMiddleFoundationPosition(middleFoundation.transform.rotation) - middleFoundation.transform.position) : Vector3.zero;

            //for debug purposes
            //var delta = teleportEntitiesUp ? Vector3.up * 20F : Vector3.zero;

            DecayEntity curDecay;

            for (var i = 0; i < buildingTouse.decayEntities.Count; i++)
            {
                curDecay = buildingTouse.decayEntities[i];
                TeleportDecayEntityDuringConversion(curDecay, curDecay.transform.position + delta);
            }

            BaseCombatEntity curBaseCombat;

            for (var i = 0; i < extraCombatEntities.Count; i++)
            {
                curBaseCombat = extraCombatEntities[i];
                TeleportBaseCombatEnityDuringConversion(curBaseCombat, curBaseCombat.transform.position + delta);
            }
            

            Vector3 flatDirectionFromMiddleToDoorway = (exitDoorway.transform.position - middleFoundation.transform.position).WithY(0F).normalized;

            Vector3 flatForwardOfDoorway = exitDoorway.transform.right.WithY(0F).normalized;

            float dotOfForward = Vector3.Dot(flatDirectionFromMiddleToDoorway, flatForwardOfDoorway);
            float dotOfBackward = Vector3.Dot(flatDirectionFromMiddleToDoorway, -flatForwardOfDoorway);

            Vector3 applyEulers = exitDoorway.transform.eulerAngles + (dotOfForward > dotOfBackward ? Vector3.zero : Vector3.up * 180F);

            var freshPortal = SpawnStableAt(PREFAB_BUNKER_PORTAL, exitDoorway.transform.position, true, true, 0, Quaternion.Euler(applyEulers)) as BasePortal;    

            freshPortal.targetID.Value = buildingTouse.ID;

            var newPocket = freshPortal.gameObject.AddComponent<DimensionalPocket>();

            return newPocket;
        }

        public static void MakeStable(BaseEntity entity, bool markInternal, bool markIndestructible, bool setHealthToMax = true)
        {
            var groundWatch = entity.gameObject.GetComponent<GroundWatch>();

            if (groundWatch != null)
            {
                UnityEngine.Object.DestroyImmediate(groundWatch);
            }

            var groundMissing = entity.gameObject.GetComponent<DestroyOnGroundMissing>();

            if (groundMissing != null)
            {
                UnityEngine.Object.DestroyImmediate(groundMissing);
            }

            if (setHealthToMax)
            {
                var asBaseCombat = entity as BaseCombatEntity;
                if (asBaseCombat != null)
                {
                    var maybeStability = entity as StabilityEntity;
                    if (maybeStability != null)
                    {
                        maybeStability.grounded = true;

                        var maybeBuildingBlock = maybeStability as BuildingBlock;

                        if (maybeBuildingBlock != null)
                        {
                            maybeBuildingBlock.SetHealthToMax();
                            maybeBuildingBlock.StopBeingDemolishable();
                            maybeBuildingBlock.StopBeingRotatable();
                        }
                    }
                }
            }


            if (markInternal)
            {
                MarkEntityAsInternal(entity);
            }

            if (markIndestructible)
            {
                MarkEntityAsIndestructible(entity);
            }
        }

        public static void MarkEntityAsSpecific(BaseEntity entity, bool sendNetworkUpdate = true)
        {
            entity.SetFlag(FLAG_SPECIFIC, true, false, sendNetworkUpdate);
        }

        public static void MarkEntityAsInternal(BaseEntity entity)
        {
            entity.SetFlag(FLAG_INTERNAL, true, false, true);
        }

        public static void MarkEntityAsIndestructible(BaseEntity entity)
        {
            entity.SetFlag(FLAG_INDESTRUCTIBLE, true, false, true);
        }
        public static bool IsEntityMarkedIndestructible(BaseEntity entity)
        {
            return entity.HasFlag(FLAG_INDESTRUCTIBLE);
        }


        public static bool IsEntityMarkedSpecific(BaseEntity entity)
        {
            return entity.HasFlag(FLAG_SPECIFIC);
        }

        public static bool IsEntityMarkedInternal(BaseEntity entity)
        {
            return entity.HasFlag(FLAG_INTERNAL);
        }


        public static bool IsDimensionalBoxItem(Item item, bool lookingForFresh = false)
        {
            if (item == null)
            {
                return false;
            }

            if (item.info.shortname != ITEM_SHORTNAME_WOOD_STORAGE_BOX)
            {
                return false;
            }

            if (!lookingForFresh)
            {
                if (!item.HasFlag(global::Item.Flag.Cooking))
                {
                    return false;
                }

                return true;
            }
            else
            {
                if (item.skin == 0)
                {
                    return false;
                }

                if (item.HasFlag(global::Item.Flag.Cooking))
                {
                    return false;
                }

                return Instance.Configuration.SkinIDToCopyPasteData.ContainsKey(item.skin);
            }
        }

        public static Item MakeEmptyDimensionalBoxItem(int amount = 1)
        {
            var newItem = ItemManager.Create(ItemDefinitionWoodStorageBox, amount);
            TurnIntoDimensionalBoxItem(newItem);

            return newItem;
        }

        public static void TurnIntoDimensionalBoxItem(Item itemThatYouAreSureIsWoodStorageBox)
        {
            itemThatYouAreSureIsWoodStorageBox.SetFlag(global::Item.Flag.Cooking, true);

            EnsureDimensionalBoxItemName(itemThatYouAreSureIsWoodStorageBox);
        }

        public static void EnsureDimensionalBoxItemName(Item item)
        {
            item.name = MSG(MSG_POCKET_DIMENSION_ITEM_NAME);
            item.MarkDirty();
        }

        #endregion

        #region API

        [HookMethod(nameof(GetDimensionalPortals))]
        public BasePortal[] GetDimensionalPortals()
        {
            var portalCount = DimensionalPocket.ServerCachePortal.Count;

            if (portalCount == 0)
            {
                return null;
            }

            var resultArray = new BasePortal[portalCount];

            int idx = 0;

            foreach (var entry in DimensionalPocket.ServerCachePortal)
            {
                resultArray[idx] = entry.Value.ThisPortal;
                idx++;
            }

            return resultArray;
        }

        [HookMethod(nameof(GetDimensionalPockets))]
        public BuildingManager.Building[] GetDimensionalPockets()
        {
            var pocketCount = DimensionalPocket.ServerCacheBuildingIDToPocket.Count;

            if (pocketCount == 0)
            {
                return null;
            }

            var resultArray = new BuildingManager.Building[pocketCount];

            int idx = 0;

            foreach (var entry in DimensionalPocket.ServerCacheBuildingIDToPocket)
            {
                resultArray[idx] = entry.Value.ThisBuilding;
                idx++;
            }

            return resultArray;
        }

        [HookMethod(nameof(GetDimensionalBoxes))]
        public StorageContainer[] GetDimensionalBoxes()
        {
            var boxCount = DimensionalBox.ServerCacheBox.Count;

            if (boxCount == 0)
            {
                return null;
            }

            var resultArray = new StorageContainer[boxCount];

            int idx = 0;

            foreach (var entry in DimensionalBox.ServerCacheBox)
            {
                resultArray[idx] = entry.Value.ThisStorageContainer;
                idx++;
            }

            return resultArray;
        }

        [HookMethod(nameof(CheckIsDimensionalItem))]
        public bool CheckIsDimensionalItem(Item item, bool lookingForFresh = false) => IsDimensionalBoxItem(item, lookingForFresh);

        [HookMethod(nameof(CheckIsDimensionalPocket))]
        public bool CheckIsDimensionalPocket(BuildingManager.Building building) => DimensionalPocket.ServerCacheBuildingIDToPocket.ContainsKey(building.ID);

        [HookMethod(nameof(CheckIsDimensionalBox))]
        public bool CheckIsDimensionalBox(StorageContainer box)
        {
            if (box == null)
            {
                return false;
            }

            if (box.net == null)
            {
                return false;
            }

            return DimensionalBox.ServerCacheBox.ContainsKey(box.net.ID.Value);
        }


        [HookMethod(nameof(CheckIsDimensionalPortal))]
        public bool CheckIsDimensionalPortal(BasePortal portal)
        {
            if (portal == null)
            {
                return false;
            }

            if (portal.net == null)
            {
                return false;
            }

            return DimensionalPocket.ServerCachePortal.ContainsKey(portal.net.ID.Value);
        }

        [HookMethod(nameof(CheckIsDimensionalShellBlock))]
        public bool CheckIsDimensionalShellBlock(BuildingBlock block)
        {
            if (block == null)
            {
                return false;
            }

            if (block.net == null)
            {
                return false;
            }

            return DimensionalPocket.ServerCacheShellBlockToPocket.ContainsKey(block.net.ID.Value);
        }

        [HookMethod(nameof(CheckBelongsToDimensionalPocket))]
        public bool CheckBelongsToDimensionalPocket(DecayEntity entity)
        {
            if (entity == null)
            {
                return false;
            }

            var building = entity.GetBuilding();

            if (building == null)
            {
                return false;
            }

            return CheckIsDimensionalPocket(building);
        }

        #endregion
    }
}
