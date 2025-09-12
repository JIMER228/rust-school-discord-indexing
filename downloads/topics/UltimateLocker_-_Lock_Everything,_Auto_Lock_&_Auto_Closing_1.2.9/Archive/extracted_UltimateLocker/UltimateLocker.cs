using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch.Extend;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Oxide.Core;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Rust;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;
using static UnityEngine.Object;
using Quaternion = UnityEngine.Quaternion;
using Vector3 = UnityEngine.Vector3;

//==================================================
//CHANGELOG
//==================================================

//1.0.6     =>      05/11/2024
// - Fixed: Duplicated message in chat with auto lock pin code

//1.0.7     =>      08/11/2024
// - Fixed: Double printing of pin messages in chat

//1.0.8     =>      10/11/2024
// - Added: Auto closing system, Door Closer
// - Fixed: Default player setting data

//1.0.9     =>      11/11/2024
// - Added: AutoDoors plugin integration: Prevents automatic door closing if a Door Closer is present
// - Fixed: Code Lock unlock fix

//1.1.0     =>      14/11/2024
// - Fixed: Vehicle auto lock

//1.1.1     =>      22/11/2024
// - Fixed: Invalid code with leading 0s
// - Fixed: Modular car guest code lock

//1.1.2     =>      23/11/2024
// - Added: Support for Car Chassis

//1.1.3     =>      24/11/2024
// - Fixed: Key Lock Clan/Team sharing

//1.1.4     =>      30/11/2024
// - Added: New command /loker and /closer to add lock and door closer manually

//1.1.5     =>      15/12/2024
// - Updated: Configuration file for default player settings
// - Added: Added command to change the closing time of the individual Door Closer you are looking at
// - Improved: Code refactoring
// - Fixed: Lock position for players who have the [ultimatelocker.autolock.nolockrequired] role

//1.1.6     =>      15/12/2024
// - Fixed: bug fix

//1.1.7     =>      16/12/2024
// - Fixed: Shared lock for offline or death players

//1.1.8     =>      17/12/2024
// - Added: Message when placing a door closer via the command
// - Fixed: Loss of setted custom time for single door closer after server restart
// - Fixed: Issue where door closers were always placed if added via command, even if a door closer was already present

//1.1.9     =>      22/12/2024
// - Fixed: Vanish (CanUseLockedEntity) hook conflict
// - Update: disable lock/unlock effect if player is invisible or flying

//1.2.0     =>      30/01/2025
// - Added: Lock and auto closer support for windows: Wood Shutters
// - Improved:  Door closer positioning
// - Added:  Automatic lock closing when dismount of the vehicle

//1.2.1     =>      07/02/2025
// - Fixed: Horse lock after Rust update (06/02/2025)

//1.2.2     =>      09/02/2025
// - Fixed: Horse prefab name

//1.2.3     =>      10/02/2025
// - Added: Door Closer for Medieval door
// - Added: Code Lock support for Medieval entities: Mounted Ballista, Battering Ram, Catapult, Siege Tower, Ballista
// - Added: Medieval entities block usage: opening/closing doors, reloading/firing ammo, mounting, driving, pushing, pulling, etc...

//1.2.4     =>      13/02/2025
// - Added: Codelock support for Medieval Large Wood Box

//1.2.5     =>      27/02/2025
// - Fix: Integration with plugins [Backpack] and [Bank]

//1.2.6     =>      03/03/2025
// - Added: Player Auto Closing default settings
// - Added: New configuration option to resync all player settings to the default settings configured
// - Added: New configuration option to Force share locks with clan/team members.

//1.2.7     =>      08/03/2025
// - Added: New lockable entities [Triangle Planter Box, Triangle Rail Road Planter, Single Plant Pot, Beehive, Chicken Coop, Cooking Workbench, Engineering Workbench, Hopper]

//1.2.8     =>      12/03/2025
// - Added: New translations [es, es-ES, fr, de, nl, tr, ru, uk, zh-CN]

//1.2.9     =>      19/03/2025
// - Fix: Russian translation
// - Added: Added property to enable only use of key locks

//1.3.0     =>      22/03/2025
// - Added: New translation [zh-TW] Traditional Chinese

//1.3.1     =>      16/04/2025
// - Added: Configuration to disable door closer removal
// - Fixed: bug fix

//1.3.2     =>      01/05/2025
// - Fixed: Rust update (01/05/2025)

//1.3.3     =>      02/05/2025
// - Added: Codelock support for [Bamboo Barrel, Wicker Barrel]

//1.3.4     =>      06/05/2025
// - Added: Show Box and Storage Container lock status in the UI

//1.3.5     =>      13/05/2025
// - Added: New permission to bypass some controls during the deployment of the lock in vehicles

//1.3.6     =>      27/05/2025
// - Fixed: Fixed issue where CodeLocks were shared even in "Use key lock only" mode

//1.3.7     =>      04/07/2025
// - Added: Codelock support for Abyss Horizontal Storage Tank, Abyss Vertical Storage Tank

//1.3.8     =>      07/07/2025
// - Fixed: RemoverTool bug fix

//1.3.9     =>      08/07/2025
// - Improved: Delay while positioning the lock/Codelock

//1.4.0     =>      08/07/2025
// - Improved: Delay while positioning the lock/Codelock (Second improvement)

//1.4.1     =>      08/07/2025
// - Improved: Performance improved
// - Improved: Code refactoring

//1.4.2     =>      08/07/2025
// - Fixed: Bug fix

//1.4.3     =>      08/07/2025
// - Fixed: NRE Bug fix

namespace Oxide.Plugins
{
    [Info("UltimateLocker", "Scalbox", "1.4.3")]
    [Description("Place Code Locks everywhere & Auto Lock / Auto Closing. Vehicles, Furnaces, Weapon Racks, Turrets, Deployable Items and much more.")]
    public class UltimateLocker : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin? ImageLibrary;
        [PluginReference] private Plugin? Clans;
        [PluginReference] private Plugin? Vanish;
        [PluginReference] private Plugin? RemoverTool;
        [PluginReference] private Plugin? GunShip;
        private static UltimateLocker? self;

        private enum EntityType
        {
            Vehicle,
            VehicleBike,
            VehicleMedieval,
            DeployableConstruction,
            DeployableMedieval,
            DeployableStorage,
            DeployableWorkbench,
            DeployableElectrical,
            DeployableElectricalButton,
            DeployableElectricSwitch,
            DeployableElectricGenerator,
            DeployableLiquidContainer,
            DeployableTurret,
            DeployableTraps,
            DeployableModularCarLift,
            DeployableSnowFogMachine,
            DeployableElevator,
            DeployableElectricalNeonSign,
            DeployableMiningQuarry,
            DeployableMiningQuarryStatic,
            DeployableMiningPumpJack,
            DeployableMiningPumpJackStatic,
            DeployableWeaponRack,
            DeployableStash,
            DeployableItems,
            DeployableResources,
            DeployableIndustrial,
            DeployableMisc,
            DeployableFun,
        }

        private enum LockCategory
        {
            None,
            Door,
            Window,
            Box,
            StorageContainer,
            Locker,
            Cupboard,
            Vehicle,
            Medieval,
            Furnace,
            VendingMachine,
            Composter,
            MixingTable,
            Planter,
            AutoTurret,
            SamSite,
            Trap,
            WeaponRack,
            Stash,
            NeonSign,
            OtherLockableEntities,
            OtherCustomEntities
        }

        private enum AutoClosingType
        {
            None,
            Door,
            DoubleDoor,
            Window,
            Garage,
            LadderHatch,
            ExternalGate,
            FenceGate,
            LegacyWoodShelterDoor
        }

        private readonly List<ItemLockData> _allowedItemsLock = new();
        private readonly List<string> _entityRequiredPermission = new();

        //PrefabName
        private readonly List<string> _vehiclesCockpitPrefabName = new()
        {
            "assets/content/vehicles/modularcar/module_entities/1module_cockpit.prefab",
            "assets/content/vehicles/modularcar/module_entities/1module_cockpit_armored.prefab",
            "assets/content/vehicles/modularcar/module_entities/1module_cockpit_with_engine.prefab"
        };

        //PrefabName
        private readonly List<string> _boxesPrefabName = new()
        {
            "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab",
            "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab",
            "assets/prefabs/deployable/large wood storage/skins/medieval_large_wood_box/medieval.box.wooden.large.prefab", //Medieval Large Wood Box
            "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_a.prefab",
            "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_b.prefab",
            "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
            "assets/prefabs/deployable/large wood storage/skins/abyss_dlc_large_wood_box/abyss_dlc_storage_horizontal/abyss_barrel_horizontal.prefab",
            "assets/prefabs/deployable/large wood storage/skins/abyss_dlc_large_wood_box/abyss_dlc_storage_vertical/abyss_barrel_vertical.prefab",
            "assets/prefabs/deployable/large wood storage/skins/jungle_dlc_large_wood_box/jungle_dlc_storage_vertical/bamboo_barrel.prefab",
            "assets/prefabs/deployable/large wood storage/skins/jungle_dlc_large_wood_box/jungle_dlc_storage_horizontal/wicker_barrel.prefab",
            "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab",
            "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab"
        };

        //PrefabName
        private readonly List<string> _windowsPrefabName = new()
        {
            "assets/prefabs/building/wall.window.shutter/shutter.wood.a.prefab"
        };

        //PrefabName
        private readonly List<string> _medievalPrefabName = new()
        {
            "assets/content/vehicles/siegeweapons/ballista/ballista.entity.prefab", //Mounted Ballista
            "assets/content/vehicles/siegeweapons/batteringram/batteringram.entity.prefab", //Battering Ram
            "assets/content/vehicles/siegeweapons/catapult/catapult.entity.prefab", //Catapult
            "assets/content/vehicles/siegeweapons/siegetower/siegetower.entity.prefab", //Siege Tower
            "assets/content/vehicles/siegeweapons/ballista/ballistagun.static.entity.prefab" //Ballista
        };

        private readonly Dictionary<AutoClosingType, List<string>> _autoClosingEntities = new()
        {
            {
                AutoClosingType.Door, new List<string>
                {
                    "assets/prefabs/building/door.hinged/door.hinged.wood.prefab",
                    "assets/prefabs/building/door.hinged/door.hinged.metal.prefab",
                    "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab",
                    "assets/prefabs/misc/permstore/factorydoor/door.hinged.industrial.d.prefab",
                    "assets/prefabs/misc/medieval door skin/medieval.door.hinged.metal.prefab"
                }
            },
            {
                AutoClosingType.DoubleDoor, new List<string>
                {
                    "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab",
                    "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab",
                    "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab",
                    "assets/prefabs/building/wall.frame.cell/wall.frame.cell.gate.prefab",
                    "assets/prefabs/misc/medieval door skin/medieval.door.double.hinged.metal.prefab"
                }
            },
            {
                AutoClosingType.Window, new List<string> { "assets/prefabs/building/wall.window.shutter/shutter.wood.a.prefab" }
            },
            {
                AutoClosingType.Garage, new List<string> { "assets/prefabs/building/wall.frame.garagedoor/wall.frame.garagedoor.prefab" }
            },
            {
                AutoClosingType.LadderHatch, new List<string>
                {
                    "assets/prefabs/building/floor.ladder.hatch/floor.ladder.hatch.prefab",
                    "assets/prefabs/building/floor.triangle.ladder.hatch/floor.triangle.ladder.hatch.prefab"
                }
            },
            {
                AutoClosingType.ExternalGate, new List<string>
                {
                    "assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab",
                    "assets/prefabs/building/gates.external.high/gates.external.high.stone/gates.external.high.stone.prefab"
                }
            },
            {
                AutoClosingType.FenceGate, new List<string> { "assets/prefabs/building/wall.frame.fence/wall.frame.fence.gate.prefab" }
            },
            {
                AutoClosingType.LegacyWoodShelterDoor, new List<string>
                {
                    "assets/prefabs/building/legacy.shelter.wood/legacy.shelter.wood.deployed.prefab",
                    "assets/prefabs/building/legacy.shelter.wood/legacy.shelter.wood.door.prefab"
                }
            }
        };

        //Vehicle name - Component Name
        private readonly Dictionary<string, string> _gunShipVehiclesComponentName = new()
        {
            { "Apache", "Oxide.Plugins.GunShip/Apache" },
            { "ArmedSedan", "Oxide.Plugins.GunShip/ArmedSedan" },
            { "AttackRhib", "Oxide.Plugins.GunShip/AttackRhib" },
            { "CargoTruck", "Oxide.Plugins.GunShip/CargoTruck" },
            { "Cougar", "Oxide.Plugins.GunShip/Cougar" },
            { "Enforcer", "Oxide.Plugins.GunShip/Enforcer" },
            { "Guardian", "Oxide.Plugins.GunShip/Guardian" },
            { "HeavyTechnical", "Oxide.Plugins.GunShip/HeavyTechnical" },
            { "MiniFighter", "Oxide.Plugins.GunShip/MiniFighter" },
            { "Reaper", "Oxide.Plugins.GunShip/Reaper" },
            { "Stallion", "Oxide.Plugins.GunShip/Stallion" },
            { "Stinger", "Oxide.Plugins.GunShip/Stinger" },
            { "TCOPCobra", "Oxide.Plugins.GunShip/TCOPCobra" },
            { "TCOPHuey", "Oxide.Plugins.GunShip/TCOPHuey" },
            { "Technical", "Oxide.Plugins.GunShip/Technical" },
            { "Viper", "Oxide.Plugins.GunShip/Viper" }
        };

        private const string UILocker = "UI.UltimateLocker.Locker";
        private const string UIAutoLock = "UI.UltimateLocker.AutoLock";
        private const string UIAutoClosing = "UI.UltimateLocker.AutoClosing";

        private const string PermissionUse = "ultimatelocker.use";
        private const string PermissionAdmin = "ultimatelocker.admin";
        private const string PermissionBypassForce = "ultimatelocker.bypass.force";
        private const string PermissionBypassLockDeployVehicleCheck = "ultimatelocker.bypass.lock_deploy_vehicle_check";
        private const string PermissionAutoLockEnabled = "ultimatelocker.autolock.enabled";
        private const string PermissionAutoLockNoLockRequired = "ultimatelocker.autolock.nolockrequired";
        private const string PermissionAutoClosingEnabled = "ultimatelocker.autoclosing.enabled";
        private const string PermissionAutoClosingNoDoorCloserRequired = "ultimatelocker.autoclosing.nodoorcloserrequired";

        private const string DefaultTimeZone = "Europe/London";
        private const int KeyLockItemID = -850982208;
        private const int CodeLockItemID = 1159991980;
        private const int DoorCloserItemID = 1409529282;
        private const string CodeLockOnDeployImageName = "UltimateLocker::CodeLockOnDeploy";
        private const string KeyLockOnDeployImageName = "UltimateLocker::KeyLockOnDeploy";
        private const string CodeLockPrefab = "assets/prefabs/locks/keypad/lock.code.prefab";
        private const string KeyLockPrefab = "assets/prefabs/locks/keylock/lock.key.prefab";
        private const string DoorCloserPrefabName = "assets/prefabs/misc/doorcloser/doorcloser.prefab";
        private const string LegacyShelterWoodDoorPrefabName = "assets/prefabs/building/legacy.shelter.wood/legacy.shelter.wood.door.prefab";
        private const string KeyLockShortPrefabName = "lock.key";
        private const string CodeLockShortPrefabName = "lock.code";
        private const string DoorCloserShortPrefabName = "door.closer";
        private const string DefaultModularCarCodeLockCode = "0000";
        private const int GarageOpenDuration = 1;

        private const string ModuleCarPartialShortPrefabName = "module_car_spawned.entity";
        private const string CarChassisModuleCarPartialPrefabName = "assets/content/vehicles/modularcar/car_chassis_";

        private const int CoroutineIteratorsPerFrame = 5;
        private const float CoroutineCodeLocksWait = 0.5F;

        private const string CodeLockDeployEffect = "assets/prefabs/locks/keypad/effects/lock-code-deploy.prefab";
        private const string CodeLockDeniedEffect = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string CodeLockLockEffect = "assets/prefabs/locks/keypad/effects/lock.code.lock.prefab";
        private const string CodeLockUnlockEffect = "assets/prefabs/locks/keypad/effects/lock.code.unlock.prefab";
        private const string CodeLockCodeUpdatedEffect = "assets/prefabs/locks/keypad/effects/lock.code.updated.prefab";
        private const string CodeLockShockEffect = "assets/prefabs/locks/keypad/effects/lock.code.shock.prefab";

        private const string CodeLockOnDeployImageUrl = "https://dl.scalbox.com/Rust/Plugins/UltimateLocker/CodeLock_OnDeploy.jpg";
        private const string KeyLockOnDeployImageUrl = "https://dl.scalbox.com/Rust/Plugins/UltimateLocker/KeyLock_OnDeploy.jpg";

        private static readonly Regex AlphanumericRegex = new Regex("[^a-zA-Z0-9\\._-]", RegexOptions.Compiled);
        // private static readonly Regex AlphanumericRegex = new Regex("alphabet\\.", RegexOptions.Compiled);

        private readonly object _true = true;
        private readonly DateTime _epochDateTime = DateTime.Parse("1970-01-01");


        private Harmony _harmony;
        private List<Coroutine> _coroutines = new();
        private Configuration _config;
        private Data _data;

        #endregion

        #region Hook

        private void Init()
        {
            if (_config.ChatCommand.IsNullOrEmpty())
            {
                Puts("Command missing in configuration");
                Interface.Oxide.UnloadPlugin(Name);
                return;
            }

            foreach (var command in _config.AutoLockConfiguration.ChatCommand)
            {
                AddCovalenceCommand(command, nameof(AutoLockCmdHandler));
            }

            foreach (var command in _config.ChatCommand)
            {
                AddCovalenceCommand(command, nameof(CmdHandler));
            }

            AddCovalenceCommand(new[]
                {
                    _config.AutoLockConfiguration.AddLockerChatCommand, _config.AutoClosingConfiguration.AddCloserChatCommand,
                    _config.AutoClosingConfiguration.RemoveCloserChatCommand
                },
                nameof(LockerCloserCmdHandler));

            _allowedItemsLock.AddRange(_config.LockConfiguration.Vehicles.Where(v => v.EnableLock).ToList());
            _allowedItemsLock.AddRange(_config.LockConfiguration.Deployables.Where(d => d.EnableLock).ToList());

            self = this;
            _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionBypassForce, this);
            permission.RegisterPermission(PermissionBypassLockDeployVehicleCheck, this);
            permission.RegisterPermission(PermissionAutoLockEnabled, this);
            permission.RegisterPermission(PermissionAutoLockNoLockRequired, this);
            permission.RegisterPermission(PermissionAutoClosingEnabled, this);
            permission.RegisterPermission(PermissionAutoClosingNoDoorCloserRequired, this);

            //Register all entity required permissions
            _entityRequiredPermission.AddRange(EntityRequiredPermission());
            if (!_entityRequiredPermission.IsNullOrEmpty())
            {
                foreach (var perm in _entityRequiredPermission)
                {
                    permission.RegisterPermission(perm, this);
                }
            }
        }

        private void OnServerInitialized()
        {
            if (_config.AutoLockConfiguration.ResyncPlayersDefaultSettings)
            {
                PrintWarning("Resyncing players default settings, STARTED...");

                if (!_data.AutoLockPlayerSettingsData.IsNullOrEmpty())
                {
                    foreach (var steamID in _data.AutoLockPlayerSettingsData.Keys.ToList())
                    {
                        var currentAutoLockPlayerSettings = _data.AutoLockPlayerSettingsData[steamID];

                        var defaultAutoLockPlayerSettingsData = GetDefaultAutoLockPlayerSettingsData(steamID);
                        defaultAutoLockPlayerSettingsData.CodeLock = currentAutoLockPlayerSettings.CodeLock;
                        defaultAutoLockPlayerSettingsData.GuestCodeLock = currentAutoLockPlayerSettings.GuestCodeLock;

                        _data.AutoLockPlayerSettingsData[steamID] = defaultAutoLockPlayerSettingsData;
                    }

                    SaveData();
                }

                if (!_data.AutoClosingPlayerSettingsData.IsNullOrEmpty())
                {
                    foreach (var autoClosingPlayerSettingsDataKey in _data.AutoClosingPlayerSettingsData.Keys.ToList())
                    {
                        _data.AutoClosingPlayerSettingsData[autoClosingPlayerSettingsDataKey] = GetDefaultAutoClosingPlayerSettingsData(autoClosingPlayerSettingsDataKey);
                    }

                    SaveData();
                }

                _config.AutoLockConfiguration.ResyncPlayersDefaultSettings = false;
                SaveConfig();

                PrintWarning("Resyncing players default settings, DONE!");
            }

            _harmony = new Harmony($"{Name}_Patch_{Version.Major}_{Version.Minor}_{Version.Patch}");

            _harmony.Patch(
                original: AccessTools.Method(typeof(ModularCarCodeLock), nameof(ModularCarCodeLock.TryAddALock)),
                postfix: new HarmonyMethod(typeof(ModularCarCodeLock_Settings),
                    nameof(ModularCarCodeLock_Settings.Postfix_TryAddALock)));
            _harmony.Patch(
                original: AccessTools.Method(typeof(ModularCarCodeLock), nameof(ModularCarCodeLock.RemoveLock)),
                postfix: new HarmonyMethod(typeof(ModularCarCodeLock_Settings),
                    nameof(ModularCarCodeLock_Settings.Postfix_RemoveLock)));

            _harmony.Patch(original: AccessTools.Method(typeof(Workbench), nameof(Workbench.RPC_TechTreeUnlock)),
                prefix: new HarmonyMethod(typeof(Workbench_Settings),
                    nameof(Workbench_Settings.Prefix_RPC_TechTreeUnlock)));

            _harmony.Patch(original: AccessTools.Method(typeof(SmartSwitch), nameof(SmartSwitch.ToggleSwitch)),
                prefix: new HarmonyMethod(typeof(SmartSwitch_ToggleSwitch), nameof(SmartSwitch_ToggleSwitch.Prefix)));
            _harmony.Patch(original: AccessTools.Method(typeof(TimerSwitch), nameof(TimerSwitch.SVSwitch)),
                prefix: new HarmonyMethod(typeof(TimerSwitch_SVSwitch), nameof(TimerSwitch_SVSwitch.Prefix)));
            _harmony.Patch(
                original: AccessTools.Method(typeof(CustomTimerSwitch), nameof(CustomTimerSwitch.SERVER_SetTime)),
                prefix: new HarmonyMethod(typeof(CustomTimerSwitch_SERVER_SetTime),
                    nameof(CustomTimerSwitch_SERVER_SetTime.Prefix)));

            _harmony.Patch(original: AccessTools.Method(typeof(FogMachine), nameof(FogMachine.SetFogOn)),
                prefix: new HarmonyMethod(typeof(FogMachine_Toggle), nameof(FogMachine_Toggle.Prefix_SetFogOn)));
            _harmony.Patch(original: AccessTools.Method(typeof(FogMachine), nameof(FogMachine.SetFogOff)),
                prefix: new HarmonyMethod(typeof(FogMachine_Toggle), nameof(FogMachine_Toggle.Prefix_SetFogOff)));

            _harmony.Patch(
                original: AccessTools.Method(typeof(StrobeLight), nameof(StrobeLight.SetStrobe),
                    new[] { typeof(BaseEntity.RPCMessage) }),
                prefix: new HarmonyMethod(typeof(StrobeLight_Settings), nameof(StrobeLight_Settings.Prefix_SetStrobe)));
            _harmony.Patch(original: AccessTools.Method(typeof(StrobeLight), nameof(StrobeLight.SetStrobeSpeed)),
                prefix: new HarmonyMethod(typeof(StrobeLight_Settings),
                    nameof(StrobeLight_Settings.Prefix_SetStrobeSpeed)));

            _harmony.Patch(
                original: AccessTools.Method(typeof(AudioVisualisationEntity),
                    nameof(AudioVisualisationEntity.ServerUpdateSettings)),
                prefix: new HarmonyMethod(typeof(AudioVisualisationEntity_Settings),
                    nameof(AudioVisualisationEntity_Settings.Prefix_ServerUpdateSettings)));

            // _harmony.Patch(original: AccessTools.Method(typeof(Signage), nameof(Signage.CanUpdateSign)),
            // prefix: new HarmonyMethod(typeof(Signage_Settings), nameof(Signage_Settings.Prefix_CanUpdateSign)));
            _harmony.Patch(original: AccessTools.Method(typeof(Signage), nameof(Signage.LockSign)),
                prefix: new HarmonyMethod(typeof(Signage_Settings), nameof(Signage_Settings.Prefix_LockSign)));
            _harmony.Patch(original: AccessTools.Method(typeof(Signage), nameof(Signage.UnLockSign)),
                prefix: new HarmonyMethod(typeof(Signage_Settings), nameof(Signage_Settings.Prefix_UnLockSign)));

            _harmony.Patch(original: AccessTools.Method(typeof(NeonSign), nameof(NeonSign.SetAnimationSpeed)),
                prefix: new HarmonyMethod(typeof(NeonSign_Settings),
                    nameof(NeonSign_Settings.Prefix_SetAnimationSpeed)));
            _harmony.Patch(original: AccessTools.Method(typeof(NeonSign), nameof(NeonSign.UpdateNeonColors)),
                prefix: new HarmonyMethod(typeof(NeonSign_Settings),
                    nameof(NeonSign_Settings.Prefix_UpdateNeonColors)));

            _harmony.Patch(original: AccessTools.Method(typeof(SearchLight), nameof(SearchLight.RPC_UseLight)),
                prefix: new HarmonyMethod(typeof(SearchLight_Settings), nameof(SearchLight_Settings.Prefix_UseLight)));

            _harmony.Patch(
                original: AccessTools.Method(typeof(PoweredRemoteControlEntity),
                    nameof(PoweredRemoteControlEntity.CanChangeID)),
                prefix: new HarmonyMethod(typeof(PoweredRemoteControlEntity_Settings),
                    nameof(PoweredRemoteControlEntity_Settings.Prefix_CanChangeID)));
            _harmony.Patch(
                original: AccessTools.Method(typeof(PoweredRemoteControlEntity),
                    nameof(PoweredRemoteControlEntity.CanControl)),
                prefix: new HarmonyMethod(typeof(PoweredRemoteControlEntity_Settings),
                    nameof(PoweredRemoteControlEntity_Settings.Prefix_CanControl)));


            _harmony.Patch(
                original: AccessTools.Method(typeof(RFBroadcaster), nameof(RFBroadcaster.ServerSetFrequency)),
                prefix: new HarmonyMethod(typeof(RFBroadcaster_Settings),
                    nameof(RFBroadcaster_Settings.Prefix_ServerSetFrequency)));
            _harmony.Patch(original: AccessTools.Method(typeof(RFReceiver), nameof(RFReceiver.ServerSetFrequency)),
                prefix: new HarmonyMethod(typeof(RFReceiver_Settings),
                    nameof(RFReceiver_Settings.Prefix_ServerSetFrequency)));

            _harmony.Patch(original: AccessTools.Method(typeof(AutoTurret), nameof(AutoTurret.CanControl)),
                prefix: new HarmonyMethod(typeof(AutoTurret_Settings), nameof(AutoTurret_Settings.Prefix_CanControl)));

            // _harmony.Patch(original: AccessTools.Method(typeof(GrowableEntity), nameof(GrowableEntity.PickFruit)),
            //     prefix: new HarmonyMethod(typeof(GrowableEntity_Settings),
            //         nameof(GrowableEntity_Settings.Prefix_PickFruit)));

            _harmony.Patch(original: AccessTools.Method(typeof(IndustrialCrafter), "SvSwitch"),
                prefix: new HarmonyMethod(typeof(IndustrialCrafter_Settings),
                    nameof(IndustrialCrafter_Settings.Prefix_SvSwitch)));

            //######################################## MEDIEVAL ENTITIES ########################################
            _harmony.Patch(original: AccessTools.Method(typeof(BatteringRam), "RPC_OpenDoor"),
                prefix: new HarmonyMethod(typeof(MedievalEntity_Settings),
                    nameof(MedievalEntity_Settings.BatteringRam_Prefix_RPC_OpenDoor)));

            _harmony.Patch(original: AccessTools.Method(typeof(BatteringRam), "RPC_CloseDoor"),
                prefix: new HarmonyMethod(typeof(MedievalEntity_Settings),
                    nameof(MedievalEntity_Settings.BatteringRam_Prefix_RPC_CloseDoor)));

            _harmony.Patch(original: AccessTools.Method(typeof(Door), "RPC_OpenDoor"),
                prefix: new HarmonyMethod(typeof(MedievalEntity_Settings),
                    nameof(MedievalEntity_Settings.SiegeTower_Prefix_RPC_OpenDoor)));

            _harmony.Patch(original: AccessTools.Method(typeof(Door), "RPC_CloseDoor"),
                prefix: new HarmonyMethod(typeof(MedievalEntity_Settings),
                    nameof(MedievalEntity_Settings.SiegeTower_Prefix_RPC_CloseDoor)));

            _harmony.Patch(original: AccessTools.Method(typeof(Catapult), "SERVER_ReloadStart"),
                prefix: new HarmonyMethod(typeof(MedievalEntity_Settings),
                    nameof(MedievalEntity_Settings.Catapult_Prefix_SERVER_ReloadStart)));

            _harmony.Patch(original: AccessTools.Method(typeof(BaseSiegeWeapon), nameof(BaseSiegeWeapon.SERVER_StartPulling)),
                prefix: new HarmonyMethod(typeof(MedievalEntity_Settings),
                    nameof(MedievalEntity_Settings.BaseSiegeWeapon_Prefix_SERVER_StartPulling)));
            //###################################################################################################

            if (RemoverTool != null && !_config.AutoClosingConfiguration.CanRemoveDoorCloser)
            {
                var targetType = AccessTools.TypeByName("Oxide.Plugins.RemoverTool");
                _harmony.Patch(original: AccessTools.Method(targetType, "HasAccess"),
                    prefix: new HarmonyMethod(typeof(RemoverTool_Patch), nameof(RemoverTool_Patch.Prefix_HasAccess)));
            }

            ImageLibrary?.Call("AddImage", CodeLockOnDeployImageUrl, CodeLockOnDeployImageName);
            ImageLibrary?.Call("AddImage", KeyLockOnDeployImageUrl, KeyLockOnDeployImageName);

            //TODO da testare e poi rimuovere
            // NextFrame(() => _coroutines.Add(ServerMgr.Instance.StartCoroutine(HandleStorageContainerLockableOnStartup())));

            if (!_config.AutoLockConfiguration.UseOnlyKeyLock)
                NextFrame(() => _coroutines.Add(ServerMgr.Instance.StartCoroutine(HandleUpdateCodeLockAuthOnStartup())));
            NextFrame(() => _coroutines.Add(ServerMgr.Instance.StartCoroutine(HandleUpdateDoorCloserDelayTimeOnStartup())));
        }

        private List<string> EntityRequiredPermission()
        {
            var itemPermissions = _allowedItemsLock
                .Where(i => !IsNullOrEmpty(i.RequiredPermission))
                .Select(i => i.RequiredPermission)
                .ToList();

            if (IsNullOrEmpty(itemPermissions)) return Array.Empty<string>().ToList();

            var validEntityPermissionsList = new List<string?>();

            foreach (var itemPermission in itemPermissions
                         .Select(GetValidPermission)
                         .Where(permissions => !IsNullOrEmpty(permissions)))
            {
                validEntityPermissionsList.AddRange(itemPermission);
            }

            if (IsNullOrEmpty(validEntityPermissionsList)) return Array.Empty<string>().ToList();

            //Remove empty and duplicated permissions
            validEntityPermissionsList = validEntityPermissionsList
                .Where(s => s != null && !string.IsNullOrWhiteSpace(s))
                .Distinct()
                .ToList();

            return validEntityPermissionsList!;
        }

        private List<string> GetValidPermission(ICollection<string> permissions)
        {
            if (IsNullOrEmpty(permissions)) return Array.Empty<string>().ToList();
            return permissions
                .Select(GetValidPermission)
                .Where(perm => perm != null)
                .ToList()!;
        }

        private string? GetValidPermission(string perm)
        {
            if (string.IsNullOrWhiteSpace(perm)) return null;

            perm = perm.Replace(" ", "_");
            perm = AlphanumericRegex.Replace(perm, "");

            var permissionPrefix = Name.ToLower();

            return string.IsNullOrWhiteSpace(perm)
                ? null
                : $"{permissionPrefix}.{perm.ToLower()}";
        }

        private bool HasValidPermission(BasePlayer? player, ICollection<string> permissions)
        {
            if (player == null || IsNullOrEmpty(permissions)) return false;

            var validPermission = GetValidPermission(permissions);
            if (IsNullOrEmpty(validPermission)) return true;

            return validPermission.Any(perm =>
                _entityRequiredPermission.Contains(perm) && permission.UserHasPermission(player.UserIDString, perm));
        }

        private void Unload()
        {
            foreach (var player in BasePlayer.activePlayerList)
            {
                CuiHelper.DestroyUi(player, UILocker);
                CuiHelper.DestroyUi(player, UIAutoLock);
                CuiHelper.DestroyUi(player, UIAutoClosing);

                DestroyMonoComponent(player);
            }

            if (!_coroutines.IsNullOrEmpty())
            {
                foreach (var coroutine in _coroutines.Where(c => c != null))
                {
                    ServerMgr.Instance.StopCoroutine(coroutine);
                }
            }

            //TODO DA RIMUOVERE
            ServerMgr.Instance.StartCoroutine(HandleStorageContainerLockableOnunload());

            _coroutines.Clear();
            _entityRequiredPermission.Clear();
            _vehiclesCockpitPrefabName.Clear();

            _harmony.UnpatchAll($"{Name}_Patch_{Version.Major}_{Version.Minor}_{Version.Patch}");
            self = null;
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            DestroyMonoComponent(player);
        }

        private void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            DestroyMonoComponent(player);
        }

        private void OnNewSave(string filename)
        {
            _data.AutoLockPlayerSettingsData.Clear();
            _data.AutoClosingPlayerSettingsData.Clear();
            _data.CodeLockItemsData.Clear();
            _data.DoorCloserCustomTimeItemsData.Clear();
            SaveData();
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();
            MergeConfiguration();

            if (!Equals(_config.VersionNumber, Version))
            {
                UpdateConfigValues();
                _config.VersionNumber = Version;
            }

            SaveConfig();

            // // For debug for new entities
            // _config.LockConfiguration.Vehicles.Clear();
            // _config.LockConfiguration.Deployables.Clear();
            //
            // PrintWarning("FORCE CONFIG FOR DEBUG: Started updating the configuration file...");
            //
            // var confDebug = Configuration.CreateConfig();
            // if (!confDebug.LockConfiguration.Vehicles.IsNullOrEmpty())
            // {
            //     foreach (var vehicle in confDebug.LockConfiguration.Vehicles)
            //     {
            //         if (!_config.LockConfiguration.Vehicles.Any(i => i.PrefabName.Equals(vehicle.PrefabName, StringComparison.OrdinalIgnoreCase)))
            //         {
            //             vehicle.EnableLock = true;
            //             _config.LockConfiguration.Vehicles.Add(vehicle);
            //         }
            //     }
            //
            //     SaveConfig();
            // }
            //
            // if (!confDebug.LockConfiguration.Deployables.IsNullOrEmpty())
            // {
            //     foreach (var deployable in confDebug.LockConfiguration.Deployables)
            //     {
            //         if (!_config.LockConfiguration.Deployables.Any(i => i.PrefabName.Equals(deployable.PrefabName, StringComparison.OrdinalIgnoreCase)))
            //         {
            //             deployable.EnableLock = true;
            //             _config.LockConfiguration.Deployables.Add(deployable);
            //         }
            //     }
            //
            //     SaveConfig();
            // }
            //
            // PrintWarning("FORCE CONFIG FOR DEBUG: Config update completed!");
            // //-------------------- End debug --------------------
        }

        //Merge Json Ignored value to the config
        private void MergeConfiguration()
        {
            var tmpConf = Configuration.CreateConfig();

            foreach (var tmpVehicle in tmpConf.LockConfiguration.Vehicles)
            {
                var vehicle = _config.LockConfiguration.Vehicles.FirstOrDefault(v =>
                    v.PrefabName.ToLower().Equals(tmpVehicle.PrefabName.ToLower()));
                if (vehicle == null)
                    continue;

                vehicle.LockCategory = tmpVehicle.LockCategory;
                vehicle.EntityType = tmpVehicle.EntityType;
                vehicle.CodeLockPosition = tmpVehicle.CodeLockPosition;
                vehicle.CodeLockRotation = tmpVehicle.CodeLockRotation;
            }

            foreach (var tmpDeployable in tmpConf.LockConfiguration.Deployables)
            {
                var deployable = _config.LockConfiguration.Deployables.FirstOrDefault(v =>
                    v.PrefabName.ToLower().Equals(tmpDeployable.PrefabName.ToLower()));
                if (deployable == null)
                    continue;

                deployable.LockCategory = tmpDeployable.LockCategory;
                deployable.EntityType = tmpDeployable.EntityType;
                deployable.CodeLockPosition = tmpDeployable.CodeLockPosition;
                deployable.CodeLockRotation = tmpDeployable.CodeLockRotation;
            }
        }

        private void DeleteLangFiles()
        {
            var langDir = Interface.Oxide.LangDirectory;
            string[] languages = lang.GetLanguages(this);
            foreach (var language in languages)
            {
                var langFile = Path.Combine(langDir, language, $"{Name}.json");
                if (!File.Exists(langFile)) continue;
                File.Delete(langFile);
                Puts($"########## Delete old lang file: {langFile} ##########");
            }
        }

        private void UpdateConfigValues()
        {
            UpdateDataValues();

            if (_config.VersionNumber < new VersionNumber(0, 9, 84))
            {
                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/bikes/motorbike.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Motorbike",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/bikes/motorbike.prefab",

                        EntityType = EntityType.VehicleBike,
                        CodeLockPosition = new Vector3(0.074f, 0.8f, 0.14f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/bikes/motorbike_sidecar.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Motorbike With Sidecar",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/bikes/motorbike_sidecar.prefab",

                        EntityType = EntityType.VehicleBike,
                        CodeLockPosition = new Vector3(0.72f, 0.52f, -0.65f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/bikes/pedalbike.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Pedal Bike",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/bikes/pedalbike.prefab",

                        EntityType = EntityType.VehicleBike,
                        CodeLockPosition = new Vector3(0.0f, 0.6f, -0.07f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/bikes/pedaltrike.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Pedal Trike",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/bikes/pedaltrike.prefab",

                        EntityType = EntityType.VehicleBike,
                        CodeLockPosition = new Vector3(0.0f, 0.7f, -0.5f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }


                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals(
                            "assets/prefabs/deployable/weaponracks/weaponrack_single1.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Frontier Bolts Single Item Rack",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_single1.deployed.prefab",

                        EntityType = EntityType.DeployableWeaponRack,
                        CodeLockPosition = new Vector3(-0.5f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals(
                            "assets/prefabs/deployable/weaponracks/weaponrack_single2.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Frontier Horseshoe Single Item Rack",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_single2.deployed.prefab",

                        EntityType = EntityType.DeployableWeaponRack,
                        CodeLockPosition = new Vector3(-0.5f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals(
                            "assets/prefabs/deployable/weaponracks/weaponrack_single3.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Frontier Horns Single Item Rack",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_single3.deployed.prefab",

                        EntityType = EntityType.DeployableWeaponRack,
                        CodeLockPosition = new Vector3(-0.5f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }
            }


            if (_config.VersionNumber < new VersionNumber(0, 9, 85))
            {
                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/bundled/prefabs/static/miningquarry_static.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Mining Quarry Static",
                        EnableLock = false,
                        PrefabName = "assets/bundled/prefabs/static/miningquarry_static.prefab",

                        EntityType = EntityType.DeployableMiningQuarryStatic,
                        CodeLockPosition = new Vector3(-2.36f, 4.5f, 1.66f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }
            }


            if (_config.VersionNumber < new VersionNumber(0, 9, 86))
            {
                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/deployable/oil jack/mining.pumpjack.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Pump Jack",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/oil jack/mining.pumpjack.prefab",

                        EntityType = EntityType.DeployableMiningPumpJack,
                        CodeLockPosition = new Vector3(3.26f, 4.5f, -1.36f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/bundled/prefabs/static/pumpjack-static.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Pump Jack Static",
                        EnableLock = false,
                        PrefabName = "assets/bundled/prefabs/static/pumpjack-static.prefab",

                        EntityType = EntityType.DeployableMiningPumpJackStatic,
                        CodeLockPosition = new Vector3(3.26f, 4.5f, -1.36f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }
            }

            if (_config.VersionNumber < new VersionNumber(0, 9, 902))
            {
                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "RHIB Storage",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.38f, 0.30f),
                        CodeLockRotation = Quaternion.Euler(0, 90, 0),

                        RequiredPermission = new[] { "" }
                    });
                }
            }

            if (_config.VersionNumber < new VersionNumber(0, 9, 910))
            {
                Puts("Configuration file is outdated. Updating...");
                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Chinese Lantern",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern.deployed.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, -0.4f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern_white.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Chinese Lantern White",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern_white.deployed.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, -0.4f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/deployable/tuna can wall lamp/tunalight.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Tuna Can Lamp",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/tuna can wall lamp/tunalight.deployed.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/deployable/lantern/lantern.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Lantern",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/lantern/lantern.deployed.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/deployable/campfire/campfire.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Camp Fire",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/campfire/campfire.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Cursed Cauldron",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Skull Fire Pit",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Jack O Lantern Angry",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.2f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Jack O Lantern Happy",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab",

                        EntityType = EntityType.DeployableStorage,
                        CodeLockPosition = new Vector3(0.0f, 0.2f, 0.0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 0, 0))
            {
                Puts("Configuration file is outdated. Updating...");

                _config.ChatPrefix = "UltimateLocker";

                if (_config.AutoLockConfiguration == null)
                {
                    _config.AutoLockConfiguration = new AutoLockConfiguration();
                }

                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 0, 8))
            {
                Puts("Configuration file is outdated. Updating...");

                if (_config.AutoClosingConfiguration == null)
                {
                    _config.AutoClosingConfiguration = new AutoClosingConfiguration();
                }

                Puts("Configuration file updated.");

                DeleteLangFiles();
            }

            if (_config.VersionNumber < new VersionNumber(1, 1, 2))
            {
                Puts("Configuration file is outdated. Updating...");

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Car Chassis 2 Module Car",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab",

                        LockCategory = LockCategory.Vehicle,
                        EntityType = EntityType.Vehicle,
                        CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Car Chassis 3 Module Car",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab",

                        LockCategory = LockCategory.Vehicle,
                        EntityType = EntityType.Vehicle,
                        CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Car Chassis 4 Module Car",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab",

                        LockCategory = LockCategory.Vehicle,
                        EntityType = EntityType.Vehicle,
                        CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                        RequiredPermission = new[] { "" }
                    });
                }

                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 1, 4))
            {
                DeleteLangFiles();
            }

            if (_config.VersionNumber < new VersionNumber(1, 1, 5))
            {
                DeleteLangFiles();

                Puts("Configuration file is outdated. Updating...");
                _config.AutoLockConfiguration.AutoLockPlayerConfiguration = new AutoLockPlayerConfiguration();
                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 1, 8))
            {
                DeleteLangFiles();
                SaveConfig();
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 0))
            {
                DeleteLangFiles();

                Puts("Configuration file is outdated. Updating...");

                if (!_config.LockConfiguration.Deployables.Any(dep =>
                        dep.PrefabName.Equals("assets/prefabs/building/wall.window.shutter/shutter.wood.a.prefab")))
                {
                    _config.LockConfiguration.Deployables.Add(new ItemLockData
                    {
                        ItemName = "Wood Shutters",
                        EnableLock = false,
                        PrefabName = "assets/prefabs/building/wall.window.shutter/shutter.wood.a.prefab",

                        LockCategory = LockCategory.OtherCustomEntities,
                        EntityType = EntityType.DeployableConstruction,
                        CodeLockPosition = new Vector3(0.18f, 0.26f, 0.96f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 1))
            {
                DeleteLangFiles();

                Puts("Configuration file is outdated. Updating...");

                var horsePrefab = _config.LockConfiguration.Vehicles
                    .FirstOrDefault(lc => lc.PrefabName.Equals("assets/rust.ai/nextai/testridablehorse.prefab", StringComparison.OrdinalIgnoreCase));
                if (horsePrefab != null)
                {
                    horsePrefab.PrefabName = "assets/content/vehicles/horse/_old/testridablehorse.prefab";
                }

                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 2))
            {
                DeleteLangFiles();

                Puts("Configuration file is outdated. Updating...");

                var horsePrefab = _config.LockConfiguration.Vehicles
                    .FirstOrDefault(lc => lc.PrefabName.Equals("assets/content/vehicles/horse/_old/testridablehorse.prefab", StringComparison.OrdinalIgnoreCase));
                if (horsePrefab != null)
                {
                    horsePrefab.PrefabName = "assets/content/vehicles/horse/ridablehorse2.prefab";
                }

                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 3))
            {
                DeleteLangFiles();

                Puts("Configuration file is outdated. Updating...");

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/siegeweapons/ballista/ballista.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Mounted Ballista",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/siegeweapons/ballista/ballista.entity.prefab",

                        LockCategory = LockCategory.Medieval,
                        EntityType = EntityType.VehicleMedieval,
                        CodeLockPosition = new Vector3(0.24f, 0.3f, 0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/siegeweapons/batteringram/batteringram.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Battering Ram",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/siegeweapons/batteringram/batteringram.entity.prefab",

                        LockCategory = LockCategory.Medieval,
                        EntityType = EntityType.VehicleMedieval,
                        CodeLockPosition = new Vector3(2.4f, 2.0f, 1.64f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/siegeweapons/catapult/catapult.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Catapult",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/siegeweapons/catapult/catapult.entity.prefab",

                        LockCategory = LockCategory.Medieval,
                        EntityType = EntityType.VehicleMedieval,
                        CodeLockPosition = new Vector3(1.124f, 2.74f, 0.9f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/siegeweapons/siegetower/siegetower.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Siege Tower",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/siegeweapons/siegetower/siegetower.entity.prefab",

                        LockCategory = LockCategory.Medieval,
                        EntityType = EntityType.VehicleMedieval,
                        CodeLockPosition = new Vector3(1.27f, 2.4f, -0.4f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                if (!_config.LockConfiguration.Vehicles.Any(dep =>
                        dep.PrefabName.Equals("assets/content/vehicles/siegeweapons/ballista/ballistagun.static.entity.prefab")))
                {
                    _config.LockConfiguration.Vehicles.Add(new ItemLockData
                    {
                        ItemName = "Ballista",
                        EnableLock = false,
                        PrefabName = "assets/content/vehicles/siegeweapons/ballista/ballistagun.static.entity.prefab",

                        LockCategory = LockCategory.Medieval,
                        EntityType = EntityType.DeployableMedieval,
                        CodeLockPosition = new Vector3(0.0f, 0.6f, 0f),
                        CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                        RequiredPermission = new[] { "" }
                    });
                }

                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 6))
            {
                DeleteLangFiles();

                Puts("Configuration file is outdated. Updating...");
                _config.AutoLockConfiguration.AutoClosingPlayerConfiguration = new AutoClosingPlayerConfiguration();
                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 7))
            {
                PrintWarning("Configuration file is outdated. Updating...");

                AddNewItemFromConfiguration();

                SaveConfig();
                PrintWarning("Config update completed!");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 8))
            {
                DeleteLangFiles();
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 9))
            {
                DeleteLangFiles();
            }

            if (_config.VersionNumber < new VersionNumber(1, 3, 0))
            {
                DeleteLangFiles();
            }

            if (_config.VersionNumber < new VersionNumber(1, 3, 2))
            {
                Puts("Configuration file is outdated. Updating...");

                var horsePrefab = _config.LockConfiguration.Vehicles
                    .FirstOrDefault(lc => lc.PrefabName.Equals("assets/content/vehicles/horse/ridablehorse2.prefab", StringComparison.OrdinalIgnoreCase));
                if (horsePrefab != null)
                {
                    horsePrefab.PrefabName = "assets/content/vehicles/horse/ridablehorse.prefab";
                    horsePrefab.CodeLockPosition = new Vector3(0.0f, 1.24f, 0.0f);
                }

                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 3, 3))
            {
                PrintWarning("Configuration file is outdated. Updating...");

                AddNewItemFromConfiguration();

                SaveConfig();
                PrintWarning("Config update completed!");
            }

            if (_config.VersionNumber < new VersionNumber(1, 3, 7))
            {
                PrintWarning("Configuration file is outdated. Updating...");

                AddNewItemFromConfiguration();

                SaveConfig();
                PrintWarning("Config update completed!");
            }
        }

        private void AddNewItemFromConfiguration()
        {
            var conf = Configuration.CreateConfig();
            if (!conf.LockConfiguration.Vehicles.IsNullOrEmpty())
            {
                foreach (var vehicle in conf.LockConfiguration.Vehicles)
                {
                    if (!_config.LockConfiguration.Vehicles.Any(i => i.PrefabName.Equals(vehicle.PrefabName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _config.LockConfiguration.Vehicles.Add(vehicle);
                    }
                }
            }

            if (!conf.LockConfiguration.Deployables.IsNullOrEmpty())
            {
                foreach (var deployable in conf.LockConfiguration.Deployables)
                {
                    if (!_config.LockConfiguration.Deployables.Any(i => i.PrefabName.Equals(deployable.PrefabName, StringComparison.OrdinalIgnoreCase)))
                    {
                        _config.LockConfiguration.Deployables.Add(deployable);
                    }
                }
            }
        }

        private void UpdateDataValues()
        {
            if (_config.VersionNumber < new VersionNumber(1, 2, 0))
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
                Puts("Updating Data file...");

                if (_data.AutoLockPlayerSettingsData.IsNullOrEmpty())
                    return;

                foreach (var autoLockPlayerSettingsData in _data.AutoLockPlayerSettingsData.Select(psd => psd.Value))
                {
                    autoLockPlayerSettingsData.VehicleLockAutoClosingOnDismountEnabled =
                        _config.AutoLockConfiguration.AutoLockPlayerConfiguration.EnableVehicleLockAutoClosingOnDismountByDefault;
                }

                if (_data.AutoClosingPlayerSettingsData.IsNullOrEmpty())
                    return;

                foreach (var autoClosingPlayerSettingsData in _data.AutoClosingPlayerSettingsData.Select(psd => psd.Value))
                {
                    autoClosingPlayerSettingsData.WindowClosingDelay = _config.AutoClosingConfiguration.DefaultClosingDelayTime;
                }

                SaveData();
                Puts("Data file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 2, 3))
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
                Puts("Updating Data file...");

                if (_data.AutoLockPlayerSettingsData.IsNullOrEmpty())
                    return;

                foreach (var autoLockPlayerSettingsData in _data.AutoLockPlayerSettingsData.Select(psd => psd.Value))
                {
                    autoLockPlayerSettingsData.MedievalEntityLockEnabled = _config.AutoLockConfiguration.AutoLockPlayerConfiguration.EnableMedievalEntityLockByDefault;
                }

                SaveData();
                Puts("Data file updated.");
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.CreateConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(_config);

        private void SaveData() => Interface.Oxide.DataFileSystem.WriteObject(Name, _data);


        #region Clans

        private void OnClanDestroy(string clanName)
        {
            timer.Once(5f, () => { UpdateAllCodeLockAuthShareByClanName(clanName); });
        }

        private void OnClanMemberGone(string userID, string tag)
        {
            timer.Once(5f, () => { UpdateAllCodeLockAuthShareByTeamMembers(new List<ulong> { Convert.ToUInt64(userID) }); });
        }

        private void OnClanUpdate(string clanName)
        {
            timer.Once(5f, () => { UpdateAllCodeLockAuthShareByClanName(clanName); });
        }

        #endregion

        #region Clans Reborn

        private void OnClanMemberGone(string playerId, List<string> memberUserIDs)
        {
            if (memberUserIDs == null || memberUserIDs.IsNullOrEmpty()) return;
            UpdateAllCodeLockAuthShareByTeamMembers(memberUserIDs.Select(ulong.Parse).ToList());
        }

        #endregion Clans Reborn

        #region Rust Team

        private object? OnTeamUpdated(ulong currentTeam, RelationshipManager.PlayerTeam playerTeam, BasePlayer player)
        {
            if (playerTeam.members.IsNullOrEmpty()) return null;

            var members = new List<ulong>();
            members.AddRange(playerTeam.members);
            members.Add(player.userID);

            NextTick(() => { UpdateAllCodeLockAuthShareByTeamMembers(members); });
            return null;
        }

        private void OnTeamAcceptInvite(RelationshipManager.PlayerTeam playerTeam, BasePlayer player)
        {
            if (playerTeam.members.IsNullOrEmpty()) return;

            var members = new List<ulong>();
            members.AddRange(playerTeam.members);
            members.Add(player.userID);

            NextTick(() => { UpdateAllCodeLockAuthShareByTeamMembers(members); });
        }

        private object? OnTeamLeave(RelationshipManager.PlayerTeam playerTeam, BasePlayer player)
        {
            if (playerTeam.members.IsNullOrEmpty()) return null;

            var members = new List<ulong>();
            members.AddRange(playerTeam.members);
            members.Add(player.userID);

            NextTick(() => { UpdateAllCodeLockAuthShareByTeamMembers(members); });

            return null;
        }

        private object? OnTeamKick(RelationshipManager.PlayerTeam playerTeam, BasePlayer leader, ulong target)
        {
            if (playerTeam.members.IsNullOrEmpty()) return null;

            var members = new List<ulong>();
            members.AddRange(playerTeam.members);
            members.Add(target);

            NextTick(() => { UpdateAllCodeLockAuthShareByTeamMembers(members); });

            return null;
        }

        private void OnTeamDisbanded(RelationshipManager.PlayerTeam playerTeam)
        {
            if (playerTeam.members.IsNullOrEmpty()) return;
            UpdateAllCodeLockAuthShareByTeamMembers(playerTeam.members);
        }

        #endregion

        #region AutoDoors Integration

        //AutoDoors plugin integration: Prevents automatic door closing if a Door Closer is present
        private object? OnDoorAutoClose(BasePlayer player, Door door)
        {
            if (door == null) return null;

            var doorCloser = door.GetSlot(BaseEntity.Slot.UpperModifier) as DoorCloser;
            if (doorCloser != null && doorCloser.delay > 0)
            {
                return false;
            }

            return null;
        }

        #endregion

        private void OnItemDeployed(Deployer deployer, BaseEntity entity, DoorCloser doorCloser)
        {
            if (doorCloser == null || !doorCloser.OwnerID.IsSteamId()) return;

            var player = deployer.GetOwnerPlayer();
            if (player == null || player.IsNpc) return;

            var autoClosingType = AutoClosingTypeByEntity(entity, true);
            if (autoClosingType == AutoClosingType.None) return;

            AdjustDoorCloserPosition(entity, doorCloser, autoClosingType);
            doorCloser.SendNetworkUpdate_Position();
            doorCloser.SendNetworkUpdateImmediate();

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) || !IsAutoClosingEnabled(player)) return;

            var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(player);
            if (autoClosingPlayerSettings.AutoClosingEnabled)
            {
                _coroutines.Add(ServerMgr.Instance.StartCoroutine(UpdateDoorCloserDelayTime(player, autoClosingType, autoClosingPlayerSettings,
                    new List<DoorCloser> { doorCloser })));
            }
        }

        private void OnEntitySpawned(BaseEntity entity)
        {
            //TODO da testare e poi rimuovere
            // if (entity == null || !entity.OwnerID.IsSteamId())
            //     return;
            //
            // if (!_config.LockConfiguration.Deployables.IsNullOrEmpty() && !_allowedItemsLock.IsNullOrEmpty())
            // {
            //     if (entity != null && entity is StorageContainer storageContainer && storageContainer.OwnerID.IsSteamId())
            //     {
            //         if (_config.LockConfiguration.Deployables.Any(d =>
            //                 d.PrefabName.Equals(storageContainer.PrefabName, StringComparison.OrdinalIgnoreCase)))
            //         {
            //             storageContainer.isLockable = _allowedItemsLock.Any(ai =>
            //                 ai.PrefabName.Equals(storageContainer.PrefabName, StringComparison.OrdinalIgnoreCase));
            //
            //             storageContainer.SendNetworkUpdateImmediate(true);
            //         }
            //     }
            // }

            if (entity == null || !entity.OwnerID.IsSteamId())
                return;

            // // For debug for new entities
            // Puts($"########## ENTITY NAME: {entity.PrefabName} - ENTITYID: {entity.net.ID.Value} ##########");

            if (IsAnEntityToIgnore(entity)) return;

            var player = BasePlayer.FindByID(entity!.OwnerID);
            if (player == null || player.IsNpc) return;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse)) return;

            //#################### Auto Closing ####################
            NextFrame(() => { DeployDoorCloser(player, entity, false); });

            //#################### Auto lock ####################
            NextFrame(() => { DeployLocker(player, entity, false); });

            //####################################################################################################
        }

        private void OnEntityBuilt(Planner planner, GameObject go)
        {
            if (go == null || go.ToBaseEntity() == null || go.ToBaseEntity().GetEntity() == null)
                return;

            var baseEntity = go.ToBaseEntity().GetEntity();
            if (baseEntity == null)
                return;

            var plannerPlayer = planner.GetOwnerPlayer();
            if (plannerPlayer != null && _config.SetPlayerAsOwnerWhenPlacedQuarry &&
                baseEntity is MiningQuarry miningQuarry && plannerPlayer.userID.IsSteamId())
            {
                miningQuarry.OwnerID = plannerPlayer.userID;
            }

            if (baseEntity is not CodeLock && baseEntity is not KeyLock)
                return;

            var baseLock = baseEntity as BaseLock;
            if (baseLock == null || !baseLock.OwnerID.IsSteamId()) return;

            var player = BasePlayer.FindByID(baseLock.OwnerID);
            if (player == null) return;

            if (CanDeployLock(player, baseLock))
                EntityDeployed(baseLock, null, player);
        }

        private object? OnEntityKill(BaseLock baseLock)
        {
            if (baseLock == null || !baseLock.OwnerID.IsSteamId()) return null;
            var player = BasePlayer.FindByID(baseLock.OwnerID);
            if (player == null) return null;

            EntityRemoved(baseLock, null, player);

            return null;
        }

        private object? OnEntityKill(ModularCar modularCar)
        {
            if (modularCar == null || !modularCar.OwnerID.IsSteamId()) return null;
            var player = BasePlayer.FindByID(modularCar.OwnerID);
            if (player == null) return null;

            EntityRemoved(null, modularCar, player);

            return null;
        }

        private object? OnEntityKill(DoorCloser doorCloser)
        {
            if (doorCloser == null || !doorCloser.OwnerID.IsSteamId()) return null;
            var player = BasePlayer.FindByID(doorCloser.OwnerID);
            if (player == null) return null;

            DoorCloserRemoved(player, doorCloser);

            return null;
        }

        private void OnActiveItemChanged(BasePlayer player, Item oldItem, Item newItem)
        {
            if (newItem == null || player == null) return;

            if (permission.UserHasPermission(player.UserIDString, PermissionUse) &&
                newItem.info.itemid is KeyLockItemID or CodeLockItemID)
            {
                var lockerBehaviour = player.GetComponent<LockerBehaviour>();
                if (lockerBehaviour == null)
                    player.gameObject.AddComponent<LockerBehaviour>();
            }
            else if (player.HasComponent<LockerBehaviour>())
                Destroy(player.GetComponent<LockerBehaviour>());
        }

        private void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null) return;

            if (input == null || !input.WasJustPressed(BUTTON.FIRE_PRIMARY))
            {
                return; // Esegue il deploy della serratura solo se è stato premuto il tasto di fuoco
            }

            var behaviour = player.GetComponent<LockerBehaviour>();
            if (behaviour != null)
            {
                behaviour.HandleInput(input);
            }
        }

        //TODO DA RIMUOVERE
        //Prevents Rust from managing Key Lock placement
        // private object? CanDeployItem(BasePlayer player, Deployer deployer, NetworkableId entityId)
        // {
        //     if (player == null || !entityId.IsValid) return null;
        //
        //     var activeItem = player.GetActiveItem();
        //     if (activeItem == null || (!activeItem.info.itemid.Equals(CodeLockItemID) &&
        //                                !activeItem.info.itemid.Equals(KeyLockItemID))) return null;
        //
        //     var entity = BaseNetworkable.serverEntities.Find(entityId);
        //     if (entity == null || entity is not BaseEntity targetEntity) return null;
        //
        //     if (IsAllowedEntity(targetEntity))
        //     {
        //         if (activeItem.info.itemid.Equals(KeyLockItemID)) return false;
        //         if (!CanDeployLock(player, targetEntity)) return false;
        //     }
        //
        //     return null;
        // }

//TODO da ricontrollare nel prossimo update di Carbon
#if CARBON
#else
        private void CanLock(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null) return;
            if (baseLock is CodeLock codeLock && !codeLock.whitelistPlayers.Contains(player.userID.Get()))
                UpdateCodeLockAuthShareOrSettings(codeLock, null, player);

            var isAllowed = IsEntityOwner(baseLock, player) || (baseLock is CodeLock cl && cl.whitelistPlayers.Contains(player.userID.Get()));
            if (isAllowed) baseLock.SetFlag(BaseEntity.Flags.Locked, true);
        }
#endif

        private object? CanUnlock(BasePlayer player, BaseLock baseLock)
        {
            var isAllowed = IsEntityOwner(baseLock, player) || (baseLock is CodeLock codeLock && codeLock.whitelistPlayers.Contains(player.userID.Get()));
            if (isAllowed) baseLock.SetFlag(BaseEntity.Flags.Locked, false);
            return isAllowed;
        }

        private void CanChangeCode(BasePlayer player, CodeLock codeLock, string code, bool isGuest)
        {
            NextFrame(() =>
            {
                if (!isGuest ? codeLock.code != code : codeLock.guestCode != code)
                {
                    return;
                }

                UpdateCodeLockAuthShareOrSettings(codeLock, null, player);
            });
        }

        // private void CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        // {
        //     if (player == null || baseLock == null || !baseLock.IsLocked()) return;
        //
        //     Puts($"########## CanUseLockedEntity: {player.displayName} - {baseLock.ShortPrefabName}");
        //     
        //     if (baseLock.IsLocked() && baseLock is CodeLock codeLock && !codeLock.whitelistPlayers.Contains(player.userID.Get()))
        //         UpdateCodeLockAuthList(codeLock, null, player);
        // }

        private object? CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (player == null || baseLock == null || !baseLock.IsLocked()) return null;
            if (baseLock is CodeLock codeLock && !codeLock.whitelistPlayers.Contains(player.userID.Get()) && IsEntityOwner(baseLock, player))
                UpdateCodeLockAuthShareOrSettings(codeLock, null, player);

            return baseLock switch
            {
                CodeLock cl when cl.whitelistPlayers.Contains(player.userID.Get()) => _true,
                KeyLock kl when IsEntityOwner(kl, player) => _true,
                _ => null
            };
        }

        private object? CanPickupEntity(BasePlayer player, DoorCloser doorCloser)
        {
            if (player == null || doorCloser == null || player.IsNpc) return null;

            if (!_config.AutoClosingConfiguration.CanRemoveDoorCloser)
            {
                return false;
            }

            if (!_config.AutoClosingConfiguration.CanPickupDoorCloser)
            {
                doorCloser.Kill();
            }

            return null;
        }

        //TODO DA RIMUOVERE
        // private bool CanPickupEntity(BasePlayer player, BaseEntity entity)
        // {
        //     if (!CanUseEntity(player, entity)) return false;
        //     return true;
        // }

        private object? CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (player == null || entity == null) return null;

            if (entity is BaseCombatEntity baseCombatEntity && !baseCombatEntity.pickup.enabled)
                return null;

            if (!CanUseEntity(player, entity)) return false;
            return null;
        }

        private object? CanLootEntity(BasePlayer player, BaseEntity baseEntity)
        {
            if (player == null || baseEntity == null) return null;

            if (baseEntity is RidableHorse or ShopFront or StorageContainer)
            {
                return baseEntity switch
                {
                    RidableHorse ridableHorse => CanLootEntity(player, ridableHorse),
                    ShopFront shopFront => CanLootEntity(player, shopFront),
                    StorageContainer storageContainer => CanLootEntity(player, storageContainer),
                    _ => null
                };
            }

            if (!CanUseEntity(player, baseEntity)) return false;
            return null;
        }

        //TODO testare se puo essere rimosso
        private object? CanLootEntity(BasePlayer player, RidableHorse animal)
        {
            if (player == null || animal == null) return null;
            if (!CanUseEntity(player, animal)) return false;
            return null;
        }

        private object? CanLootEntity(BasePlayer player, ContainerIOEntity container)
        {
            if (player == null || container == null) return null;

            if (container != null && container is VendingMachine)
            {
                if (IsVendingMachineOpen(player, container)) return null;
            }

            if (!CanUseEntity(player, container)) return false;
            return null;
        }

        private object? CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;

            if (container != null && container is DropBox)
            {
                if (IsDropBoxOpen(player, container)) return null;
            }

            if (!CanUseEntity(player, container)) return false;
            return null;
        }

        private object? CanLootEntity(BasePlayer player, ShopFront shopFront)
        {
            if (player == null || shopFront == null) return null;
            if (!CanUseEntity(player, shopFront)) return false;
            return null;
        }

        private void OnEntityDismounted(BaseMountable baseMountable, BasePlayer player)
        {
            if (baseMountable == null || player == null || player.IsNpc) return;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse)) return;

            var autoLockPlayerSettings = GetAutoLockPlayerSettingsData(player.userID.Get());
            if (!autoLockPlayerSettings.VehicleLockAutoClosingOnDismountEnabled) return;

            var canUseEntity = CanUseEntity(player, baseMountable);
            if (!canUseEntity) return;

            NextFrame(() =>
            {
                var codeLockData = GetCodeLockDataByEntity(baseMountable);
                if (codeLockData == null) return;

                if (!codeLockData.IsLocked())
                {
                    codeLockData.Lock();

                    if (!IsInvisible(player))
                        RunEffect(CodeLockLockEffect, player);

                    SendMessage(player, Lang("LockClosedAutomaticallyMessage", player.UserIDString));
                }
            });
        }

        private void OnModularCarLockAdded(ModularCarCodeLock? modularCarCodeLock, string code, ulong userID)
        {
            if (modularCarCodeLock == null || modularCarCodeLock.owner == null || !userID.IsSteamId())
                return;

            var modularCar = modularCarCodeLock.owner;
            if (modularCar == null)
                return;

            modularCar.OwnerID = userID;
            EntityDeployed(null, modularCar, BasePlayer.FindByID(userID));
        }

        private void OnModularCarLockRemoved(ModularCarCodeLock? modularCarCodeLock)
        {
            if (modularCarCodeLock == null || modularCarCodeLock.owner == null ||
                !modularCarCodeLock.owner.OwnerID.IsSteamId())
                return;

            var modularCar = modularCarCodeLock.owner;
            if (modularCar == null)
                return;

            EntityRemoved(null, modularCar, BasePlayer.FindByID(modularCar.OwnerID));
            modularCar.OwnerID = 0;
        }

        private object? OnOvenToggle(BaseOven oven, BasePlayer player)
        {
            if (player == null || oven == null) return null;
            if (!CanUseEntity(player, oven)) return false;
            return null;
        }

        private object? OnMixingTableToggle(MixingTable mixingTable, BasePlayer player)
        {
            if (player == null || mixingTable == null) return null;
            if (!CanUseEntity(player, mixingTable)) return false;
            return null;
        }

        private object? CanAdministerVending(BasePlayer player, VendingMachine machine)
        {
            if (player == null || machine == null) return null;
            if (!CanUseEntity(player, machine)) return false;
            return null;
        }

        private object? OnRotateVendingMachine(VendingMachine machine, BasePlayer player)
        {
            if (player == null || machine == null) return null;
            if (!CanUseEntity(player, machine)) return false;
            return null;
        }

        private object? CanMountEntity(BasePlayer player, BaseMountable entity)
        {
            if (player == null || entity == null) return null;
            if (!CanUseEntity(player, entity)) return false;
            return null;
        }

        private object? OnPlayerWantsMount(BasePlayer player, BaseMountable entity)
        {
            if (player == null || entity == null) return null;
            if (!CanUseEntity(player, entity)) return false;
            return null;
        }

        private object? OnEngineStart(BaseVehicle vehicle, BasePlayer driver)
        {
            if (driver == null || vehicle == null) return null;
            if (!CanUseEntity(driver, vehicle)) return false;
            return null;
        }

        private object? OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
            if (_config.AllowPushVehiclesBlockedByCodeLock)
                return null;

            if (player == null || vehicle == null) return null;
            if (!CanUseEntity(player, vehicle)) return false;
            return null;
        }

        //Conduct Horse
        private object? OnHorseLead(RidableHorse animal, BasePlayer player)
        {
            if (_config.AllowPushVehiclesBlockedByCodeLock)
                return null;

            if (player == null || animal == null) return null;
            if (!CanUseEntity(player, animal)) return false;
            return null;
        }

        private object? OnHotAirBalloonToggle(HotAirBalloon balloon, BasePlayer player)
        {
            if (player == null || balloon == null) return null;
            if (!CanUseEntity(player, balloon)) return false;
            return null;
        }

        private object? CanBuild(Planner planner, Construction prefab, Construction.Target target)
        {
            if (planner == null || planner.GetOwnerPlayer() == null || target.entity == null) return null;
            if (!CanUseEntity(planner.GetOwnerPlayer(), target.entity)) return false;
            return null;
        }

        private object? OnWorkbenchTechTreeUnlock(Workbench workbench, BasePlayer player)
        {
            if (player == null || workbench == null) return null;
            if (!CanUseEntity(player, workbench)) return false;
            return null;
        }

        private object? OnExperimentStart(Workbench workbench, BasePlayer player)
        {
            if (player == null || workbench == null) return null;
            if (!CanUseEntity(player, workbench)) return false;
            return null;
        }

        private object? OnPlayerDrink(BasePlayer player, LiquidContainer liquidContainer)
        {
            if (player == null || liquidContainer == null) return null;
            if (!CanUseEntity(player, liquidContainer)) return false;
            return null;
        }

        private object? OnSwitchToggle(IOEntity entity, BasePlayer player)
        {
            if (player == null || entity == null) return null;
            if (!CanUseEntity(player, entity)) return false;
            return null;
        }

        private object? OnSmartSwitchToggle(SmartSwitch smartSwitch, BasePlayer player)
        {
            if (player == null || smartSwitch == null) return null;
            if (!CanUseEntity(player, smartSwitch)) return false;
            return null;
        }

        private object? OnTimerSwitchToggle(TimerSwitch timerSwitch, BasePlayer player)
        {
            if (player == null || timerSwitch == null) return null;
            if (!CanUseEntity(player, timerSwitch)) return false;
            return null;
        }

        private object? CanTimerSwitchSetTime(CustomTimerSwitch customTimerSwitch, BasePlayer player)
        {
            if (player == null || customTimerSwitch == null) return null;
            if (!CanUseEntity(player, customTimerSwitch)) return false;
            return null;
        }

        private object? OnButtonPress(PressButton button, BasePlayer player)
        {
            if (player == null || button == null) return null;
            if (!CanUseEntity(player, button)) return false;
            return null;
        }

        private object? OnFogMachineToggle(FogMachine fogMachine, BasePlayer player)
        {
            if (player == null || fogMachine == null) return null;
            if (!CanUseEntity(player, fogMachine)) return false;
            return null;
        }

        private object? OnStrobeLightToggle(StrobeLight strobeLight, BasePlayer player)
        {
            if (player == null || strobeLight == null) return null;
            if (!CanUseEntity(player, strobeLight)) return false;
            return null;
        }

        private object? OnStrobeSpeedChange(StrobeLight strobeLight, BasePlayer player)
        {
            if (player == null || strobeLight == null) return null;
            if (!CanUseEntity(player, strobeLight)) return false;
            return null;
        }

        private object? OnAudioVisualisationServerUpdateSettings(AudioVisualisationEntity audioVisualisationEntity,
            BasePlayer player)
        {
            if (player == null || audioVisualisationEntity == null) return null;
            if (!CanUseEntity(player, audioVisualisationEntity)) return false;
            return null;
        }

        //TODO DA RIMUOVERE
        // private bool CanUpdateSign(BasePlayer player, Signage sign)
        // {
        //     if (player == null || sign == null) return true;
        //     return CanUseEntity(player, sign);
        // }

        private object? CanUpdateSign(BasePlayer player, Signage sign)
        {
            if (player == null || sign == null) return null;
            if (!CanUseEntity(player, sign)) return false;
            return null;
        }

        //TODO DA RIMUOVERE
        // private object? OnSignageCanUpdateSign(Signage signage, BasePlayer player)
        // {
        //     Puts("########## OnSignageCanUpdateSign ##########");
        //     if (player == null || signage == null) return null;
        //     if (!CanUseEntity(player, signage)) return false;
        //     return null;
        // }

        private object? OnSignageLockUnlockSign(Signage signage, BasePlayer player)
        {
            if (player == null || signage == null) return null;
            if (!CanUseEntity(player, signage)) return false;
            return null;
        }

        private object? OnNeonSignSetAnimationSpeed(NeonSign neonSign, BasePlayer player)
        {
            if (player == null || neonSign == null) return null;
            if (!CanUseEntity(player, neonSign)) return false;
            return null;
        }

        private object? OnUpdateNeonColors(NeonSign neonSign, BasePlayer player)
        {
            if (player == null || neonSign == null) return null;
            if (!CanUseEntity(player, neonSign)) return false;
            return null;
        }

        private object? OnSearchLightUseLight(SearchLight searchLight, BasePlayer player)
        {
            if (player == null || searchLight == null) return null;
            if (!CanUseEntity(player, searchLight)) return false;
            return null;
        }

        private object? OnPoweredRemoteControlEntityCanChangeID(BasePlayer player,
            PoweredRemoteControlEntity poweredRemoteControlEntity)
        {
            if (player == null || poweredRemoteControlEntity.GetEnt() == null) return null;
            if (!CanUseEntity(player, poweredRemoteControlEntity.GetEnt())) return false;
            return null;
        }

        private object? OnPoweredRemoteControlEntityCanControl(ulong playerID,
            PoweredRemoteControlEntity poweredRemoteControlEntity)
        {
            if (!playerID.IsSteamId() || poweredRemoteControlEntity == null ||
                poweredRemoteControlEntity.GetEnt() == null) return null;
            if (!CanUseEntity(BasePlayer.Find(playerID.ToString()), poweredRemoteControlEntity.GetEnt())) return false;
            return null;
        }

        private object? OnRFBroadcasterCanChangeFrequency(RFBroadcaster rfBroadcaster, BasePlayer player)
        {
            if (player == null || rfBroadcaster == null) return null;
            if (!CanUseEntity(player, rfBroadcaster)) return false;
            return null;
        }

        private object? OnRFReceiverCanChangeFrequency(RFReceiver rfBroadcaster, BasePlayer player)
        {
            if (player == null || rfBroadcaster == null) return null;
            if (!CanUseEntity(player, rfBroadcaster)) return false;
            return null;
        }

        // private object OnRfListenerAdd(IRFObject obj, int frequency)
        // {
        //     Puts("OnRfListenerAdd works!");
        //     return null;
        // }
        //
        // private object OnRfBroadcasterAdd(IRFObject obj, int frequency)
        // {
        //     Puts("OnRfBroadcasterAdd works!");
        //     return null;
        // }

        //FARM: Called when a player is trying to take a cutting (clone) of a GrowableEntity
        private object? CanTakeCutting(BasePlayer player, GrowableEntity entity)
        {
            if (player == null || entity == null) return null;
            if (!CanUseEntity(player, entity)) return false;
            return null;
        }

        //FARM: Called when a player is trying to harvest a dying growable entity
        private object? OnRemoveDying(GrowableEntity plant, BasePlayer player)
        {
            if (player == null || plant == null) return null;
            if (!CanUseEntity(player, plant)) return false;
            return null;
        }

        //FARM: Called when the player gathers a growable entity
        private object? OnGrowableGather(GrowableEntity plant, BasePlayer player)
        {
            if (player == null || plant == null) return null;
            if (!CanUseEntity(player, plant)) return true;
            return null;
        }

        //TODO DA RIMUOVERE
        //FARM: Called when the player gathers a growable entity
        // private object? CanPickFruit(GrowableEntity plant, BasePlayer player, bool eat)
        // {
        //     if (player == null || plant == null) return null;
        //     if (!CanUseEntity(player, plant)) return false;
        //     return null;
        // }

        private object? OnSamSiteModeToggle(SamSite samSite, BasePlayer player, bool defenderMode)
        {
            if (player == null || samSite == null) return null;
            if (!CanUseEntity(player, samSite)) return false;
            return null;
        }

        private object? OnTurretAuthorize(AutoTurret autoTurret, BasePlayer player)
        {
            if (player == null || autoTurret == null) return null;
            if (!CanUseEntity(player, autoTurret)) return false;
            return null;
        }

        private object? OnTurretRotate(AutoTurret autoTurret, BasePlayer player)
        {
            if (player == null || autoTurret == null) return null;
            if (!CanUseEntity(player, autoTurret)) return false;
            return null;
        }

        private object? OnAutoTurretCanControl(ulong playerID, AutoTurret autoTurret)
        {
            if (!playerID.IsSteamId() || autoTurret == null || autoTurret.GetEnt() == null) return null;
            if (!CanUseEntity(BasePlayer.Find(playerID.ToString()), autoTurret.GetEnt())) return false;
            return null;
        }

        //Electricity and Industrial
        private object? OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            if (player == null || (entity1 == null && entity2 == null)) return null;
            if (!CanUseEntity(player, entity1, entity2)) return false;
            return null;
        }

        //Electricity and Industrial
        private object? OnWireClear(BasePlayer player, IOEntity entity1, int connected, IOEntity entity2, bool flag)
        {
            if (player == null || (entity1 == null && entity2 == null)) return null;
            if (!CanUseEntity(player, entity1, entity2)) return false;
            return null;
        }

        private object? OnConveyorFiltersChange(IndustrialConveyor industrialConveyor, BasePlayer player,
            ProtoBuf.IndustrialConveyor.ItemFilterList itemFilterList)
        {
            if (player == null || industrialConveyor == null) return null;
            if (!CanUseEntity(player, industrialConveyor)) return false;
            return null;
        }

        private object? OnIndustrialCrafterSwitchToggle(IndustrialCrafter industrialCrafter, BasePlayer player)
        {
            if (player == null || industrialCrafter == null) return null;
            if (!CanUseEntity(player, industrialCrafter)) return false;
            return null;
        }

        private object? OnBoomboxToggle(BoomBox boomBox, BasePlayer player)
        {
            if (player == null || boomBox == null || boomBox.BaseEntity == null) return null;
            if (!CanUseEntity(player, boomBox.BaseEntity)) return false;
            return null;
        }

        private object? OnBoomboxStationUpdate(BoomBox boomBox, string url, BasePlayer player)
        {
            if (player == null || boomBox == null || boomBox.BaseEntity == null) return null;
            if (!CanUseEntity(player, boomBox.BaseEntity)) return false;
            return null;
        }

        private object? OnElevatorButtonPress(ElevatorLift lift, BasePlayer player, Elevator.Direction direction,
            bool toTopOrBottom)
        {
            if (player == null || lift == null) return null;
            if (!CanUseEntity(player, lift)) return false;
            return null;
        }

        private void OnQuarryToggled(MiningQuarry quarry, BasePlayer player)
        {
            if (player == null || quarry == null || quarry.GetEntity() == null ||
                !quarry.GetEntity().OwnerID.IsSteamId()) return;

            if (!CanUseEntity(player, quarry))
                quarry.EngineSwitch(!quarry.IsEngineOn());
        }

        private object? OnRackedWeaponMount(Item slot, BasePlayer player, WeaponRack weaponRack)
        {
            if (!CanUseEntity(player, weaponRack)) return false;
            return null;
        }

        private object? OnRackedWeaponTake(Item slot, BasePlayer player, WeaponRack weaponRack)
        {
            if (!CanUseEntity(player, weaponRack)) return true;
            return null;
        }

        private object? OnRackedWeaponSwap(Item item, WeaponRackSlot weaponAtIndex, BasePlayer player,
            WeaponRack weaponRack)
        {
            if (!CanUseEntity(player, weaponRack)) return false;
            return null;
        }

        private object? OnRackedWeaponLoad(Item slot, ItemDefinition itemdef, BasePlayer player, WeaponRack weaponRack)
        {
            if (!CanUseEntity(player, weaponRack)) return false;
            return null;
        }

        private object? OnRackedWeaponUnload(Item slot, BasePlayer player, WeaponRack weaponRack)
        {
            if (!CanUseEntity(player, weaponRack)) return false;
            return null;
        }

        //######################################## MEDIEVAL ENTITIES ########################################
        private object? CanBatteringRamOpenDoor(BatteringRam batteringRam, BasePlayer player)
        {
            if (player == null || batteringRam == null) return null;
            if (!CanUseEntity(player, batteringRam)) return false;
            return null;
        }

        private object? CanBatteringRamCloseDoor(BatteringRam batteringRam, BasePlayer player)
        {
            if (player == null || batteringRam == null) return null;
            if (!CanUseEntity(player, batteringRam)) return false;
            return null;
        }

        private object? CanCatapultReloadStart(Catapult catapult, BasePlayer player)
        {
            if (player == null || catapult == null) return null;
            if (!CanUseEntity(player, catapult)) return false;
            return null;
        }

        private object? CanSiegeTowerOpenDoor(Door door, BasePlayer player)
        {
            if (player == null || door == null) return null;
            if (!CanUseEntity(player, door)) return false;
            return null;
        }

        private object? CanSiegeTowerCloseDoor(Door door, BasePlayer player)
        {
            if (player == null || door == null) return null;
            if (!CanUseEntity(player, door)) return false;
            return null;
        }

        private object? OnSiegeWeaponFire(BaseSiegeWeapon baseSiegeWeapon, BasePlayer player)
        {
            if (player == null || baseSiegeWeapon == null) return null;
            if (!CanUseEntity(player, baseSiegeWeapon)) return false;
            return null;
        }

        private object? CanBaseSiegeWeaponStartPulling(BaseSiegeWeapon baseSiegeWeapon, BasePlayer player)
        {
            if (player == null || baseSiegeWeapon == null) return null;
            if (!CanUseEntity(player, baseSiegeWeapon)) return false;
            return null;
        }
        //###################################################################################################

        #endregion

        #region Core

        private void SendMessage(BasePlayer player, string message)
        {
            var prefix = !string.IsNullOrWhiteSpace(_config.ChatPrefix) ? _config.ChatPrefix : Name;
            prefix = $"<color=#62de32>{prefix}\n</color>";
            player.ChatMessage($"{prefix}{message}");
        }

        private void DestroyMonoComponent(BasePlayer player)
        {
            if (player == null) return;
            if (player.HasComponent<LockerBehaviour>())
                Destroy(player.GetComponent<LockerBehaviour>());
        }

        private static bool IsNullOrEmpty<T>(ICollection<T>? collection)
        {
            return collection == null || collection.Count == 0;
        }

        //TODO DA RIMUOVERE
        // private IEnumerator HandleStorageContainerLockableOnStartup()
        // {
        //     if (_config.LockConfiguration.Deployables.IsNullOrEmpty()) yield break;
        //
        //     var allConfiguredStorageContainer = BaseNetworkable.serverEntities
        //         .Where(e => e != null && !string.IsNullOrEmpty(e.PrefabName) &&
        //                     _config.LockConfiguration.Deployables.Any(d =>
        //                         e.PrefabName.Equals(d.PrefabName, StringComparison.OrdinalIgnoreCase)))
        //         .Where(e => e is StorageContainer storage && storage.OwnerID.IsSteamId())
        //         .Cast<StorageContainer>()
        //         .ToList();
        //
        //     var count = 0;
        //     foreach (var storageContainer in allConfiguredStorageContainer)
        //     {
        //         if (_allowedItemsLock.Any(ai =>
        //                 ai.PrefabName.Equals(storageContainer.PrefabName, StringComparison.OrdinalIgnoreCase)))
        //         {
        //             storageContainer.isLockable = true;
        //         }
        //         else
        //         {
        //             storageContainer.isLockable = false;
        //         }
        //
        //         storageContainer.SendNetworkUpdate();
        //         storageContainer.SendNetworkUpdateImmediate(true);
        //
        //         if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
        //         count++;
        //     }
        // }

        //TODO DA RIMUOVERE
        private IEnumerator HandleStorageContainerLockableOnunload()
        {
            if (_config.LockConfiguration.Deployables.IsNullOrEmpty()) yield break;

            var allConfiguredStorageContainer = BaseNetworkable.serverEntities
                .Where(e => e != null && !string.IsNullOrEmpty(e.PrefabName) &&
                            _config.LockConfiguration.Deployables.Any(d =>
                                e.PrefabName.Equals(d.PrefabName, StringComparison.OrdinalIgnoreCase)))
                .Where(e => e is StorageContainer storage && storage.OwnerID.IsSteamId())
                .Cast<StorageContainer>()
                .ToList();

            var count = 0;
            foreach (var storageContainer in allConfiguredStorageContainer)
            {
                if (_allowedItemsLock.Any(ai =>
                        ai.PrefabName.Equals(storageContainer.PrefabName, StringComparison.OrdinalIgnoreCase)))
                {
                    storageContainer.isLockable = false;
                }

                storageContainer.SendNetworkUpdate();
                storageContainer.SendNetworkUpdateImmediate(true);

                if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
                count++;
            }
        }

        private IEnumerator HandleUpdateCodeLockAuthOnStartup()
        {
            var count = 0;
            var playerList = BasePlayer.activePlayerList;
            foreach (var player in playerList)
            {
                if (player == null || player.IsNpc) continue;

                var lockEntityList = BaseNetworkable.serverEntities
                    .Where(se =>
                        se is CodeLock cl &&
                        cl.OwnerID.IsSteamId() &&
                        cl.OwnerID.Equals(player.userID))
                    .Cast<BaseEntity>()
                    .ToList();

                lockEntityList.AddRange(BaseNetworkable.serverEntities
                    .Where(se =>
                        se is ModularCar be && (
                            be.ShortPrefabName.Contains(ModuleCarPartialShortPrefabName) ||
                            be.PrefabName.StartsWith(CarChassisModuleCarPartialPrefabName, StringComparison.OrdinalIgnoreCase)
                        ) &&
                        be.OwnerID.IsSteamId() &&
                        be.OwnerID.Equals(player.userID))
                    .Cast<BaseEntity>()
                    .ToList());

                if (count % CoroutineIteratorsPerFrame == 0)
                    yield return ServerMgr.Instance.StartCoroutine(UpdateAllCodeLockAuthShareOrSettings(lockEntityList, player));
                count++;
            }
        }

        private IEnumerator HandleUpdateDoorCloserDelayTimeOnStartup()
        {
            var count = 0;
            var doorCloserList = BaseNetworkable.serverEntities
                .Where(se =>
                    se is DoorCloser dc && dc.GetParentEntity() != null && AutoClosingTypeByEntity(dc.GetParentEntity()) != AutoClosingType.None &&
                    dc.OwnerID.IsSteamId())
                .Cast<DoorCloser>();

            var doorCloserGroupedByPlayerId = doorCloserList.GroupBy(dc => dc.OwnerID).ToList();
            foreach (var group in doorCloserGroupedByPlayerId)
            {
                var player = group.Key;
                var doorCloserListByPlayer = group.ToList();
                var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(player);

                if (count % CoroutineIteratorsPerFrame == 0)
                    yield return ServerMgr.Instance.StartCoroutine(UpdateDoorCloserDelayTime(null, null, autoClosingPlayerSettings, doorCloserListByPlayer));
                count++;
            }
        }

        private static CodeLockData? GetCodeLockDataByLookingAt(BasePlayer player)
        {
            if (player == null) return null;

            var baseEntity = GetEntityByRayCast(player);
            if (baseEntity == null) return null;

            return GetCodeLockDataByEntity(baseEntity);
        }

        private static BaseEntity? GetEntityByRayCast(BasePlayer player)
        {
            if (player == null) return null;
            Physics.Raycast(player.eyes.HeadRay(), out var raycastHit, 3f,
                Layers.Mask.Vehicle_Detailed | Layers.Mask.Vehicle_Large | Layers.Mask.AI | Layers.Mask.Deployed | Layers.Deploy);
            return raycastHit.GetEntity() ?? null;
        }

        //Blocca l'auto lock sui veicoli GunShip, in quanto il codelock viene rimosso in automatico dal plugin al riavvio e i veicoli cambialo il loro NetId ad ogni riavvio del plugin
        private bool IsGunShipVehicle(BaseEntity entity)
        {
            if (GunShip == null) return false;

            if (entity.IsDestroyed) return false;
            if (entity.GetParentEntity() is not null)
                entity = entity.GetParentEntity();

            var components = entity.GetComponents<Component>();
            foreach (var component in components)
            {
                var fullName = component.GetType().FullName;
                if (string.IsNullOrWhiteSpace(fullName)) continue;

                var normalizedName = fullName.Replace("+", "/");
                if (_gunShipVehiclesComponentName.ContainsValue(normalizedName))
                    return true;
            }

            return false;
        }

        private static CodeLockData? GetCodeLockDataByEntity(BaseEntity baseEntity)
        {
            if (baseEntity == null) return null;
            var entityToCheck = baseEntity;

            var parentModularCar = entityToCheck.GetComponentInParent<ModularCar>();
            if (parentModularCar != null)
            {
                entityToCheck = parentModularCar;
            }

            if (entityToCheck is ModularCar modularCar)
            {
                if (!modularCar.CarLock.HasALock)
                    return null;

                return new CodeLockData
                {
                    ModularCarCodeLock = modularCar.CarLock,
                    BaseEntity = modularCar
                };
            }

            var baseLock = entityToCheck.GetSlot(BaseEntity.Slot.Lock) as BaseLock;

            if (baseLock == null && entityToCheck.GetParentEntity() == null)
                return null;

            if (baseLock == null && entityToCheck.GetParentEntity() != null)
            {
                baseLock = entityToCheck.GetParentEntity().GetSlot(BaseEntity.Slot.Lock) as BaseLock;
                entityToCheck = entityToCheck.GetParentEntity();
            }

            if (baseLock == null || entityToCheck == null) return null;

            return new CodeLockData
            {
                BaseLock = baseLock,
                BaseEntity = entityToCheck
            };
        }

        private static bool IsAnEntityToIgnore(BaseEntity entity)
        {
            if (entity == null) return false;

            if (entity is StorageContainer storageContainer)
            {
                //Ignore storage container with BackpackBox component (Plugin Backpack)
                if (storageContainer.GetComponent("Oxide.Plugins.Backpack/BackpackBox") != null)
                    return true;

                //Ignore storage container created by the plugin Bank
                var groundWatchComponent = storageContainer.GetComponent<GroundWatch>();
                if (groundWatchComponent != null && !groundWatchComponent.enabled && !storageContainer.enableSaving)
                    return true;
            }

            return false;
        }

        private bool CanUseEntity(BasePlayer player, BaseEntity? baseEntity1, BaseEntity? baseEntity2)
        {
            if (player == null || (baseEntity1 == null && baseEntity2 == null)) return true;

            var targetEntity = null as BaseEntity;
            if (baseEntity1 != null && IsAllowedEntity(baseEntity1))
                targetEntity = baseEntity1;
            else if (baseEntity2 != null && IsAllowedEntity(baseEntity2))
                targetEntity = baseEntity2;

            if (targetEntity == null) return true;

            var targetIndustrialStorageAdaptor =
                baseEntity1 as IndustrialStorageAdaptor ?? baseEntity2 as IndustrialStorageAdaptor;
            if (targetIndustrialStorageAdaptor != null && targetIndustrialStorageAdaptor.GetParentEntity() != null &&
                IsAllowedEntity(targetIndustrialStorageAdaptor.GetParentEntity()))
            {
                return CanUseEntity(player, targetEntity) &&
                       CanUseEntity(player, targetIndustrialStorageAdaptor.GetParentEntity());
            }

            if (baseEntity1 != null && baseEntity2 != null && IsAllowedEntity(baseEntity1) &&
                IsAllowedEntity(baseEntity2))
            {
                return CanUseEntity(player, baseEntity1) && CanUseEntity(player, baseEntity2);
            }

            return CanUseEntity(player, targetEntity);
        }

        private bool CanUseEntity(BasePlayer player, BaseEntity? baseEntity)
        {
            if (player == null || baseEntity == null || string.IsNullOrEmpty(baseEntity.PrefabName)) return true;

            var entityToCheck = baseEntity;

            if (baseEntity.GetParentEntity() != null && IsAllowedEntity(baseEntity.GetParentEntity().PrefabName))
            {
                if (entityToCheck is LiquidContainer)
                {
                    entityToCheck = baseEntity.GetParentEntity();
                }

                if (entityToCheck is GrowableEntity)
                {
                    entityToCheck = baseEntity.GetParentEntity();
                }
            }

            var codeLockData = GetCodeLockDataByEntity(entityToCheck);

            if (codeLockData == null || (codeLockData.ModularCarCodeLock == null && codeLockData.BaseLock == null) ||
                codeLockData.BaseEntity == null) return true;

            if (!codeLockData.IsLocked()) return true;

            //TODO da testare e poi rimuovere
            // if (!permission.UserHasPermission(player.UserIDString, PermissionUse) ||
            //     // !IsAllowedEntity(entityToCheck)) return true;
            //     !IsAllowedEntity(codeLockData.BaseEntity)) return true;

            if (CanByPassCodeLock(player))
            {
                codeLockData.Unlock();
                NextFrame(() => codeLockData.Lock());
                return true;
            }

            if (codeLockData.GetOwnerID() == null)
            {
                return true;
            }

            //If the player is authorized to the turret, it bypasses the Code Lock
            if (entityToCheck is AutoTurret turret)
            {
                if (turret.IsAuthed(player))
                    return true;
            }

            if (entityToCheck is ShopFront shopFront)
            {
                if (IsShopFrontCustomerPosOpen(player, shopFront))
                {
                    codeLockData.Unlock();
                    NextFrame(() => codeLockData.Lock());
                    return true;
                }
            }

            if (codeLockData.IsLocked() && codeLockData.GetCodeLockEntity() != null &&
                (codeLockData.GetCodeLockEntity() is not CodeLock || !codeLockData.GetCodeLockWhitelistPlayers().Contains(player.userID.Get())) &&
                !IsEntityOwner(codeLockData.GetCodeLockEntity(), player))
            {
                //CodeLock usage blocked
                if (!IsInvisible(player))
                {
                    RunEffect(CodeLockDeniedEffect, player);
                    PrintToChat(player, Lang("NoCodeLockAuth", player.UserIDString));
                }

                return false;
            }

            //CodeLock usage allowed
            if (!IsInvisible(player))
                RunEffect(CodeLockUnlockEffect, player);
            return true;
        }

        private ItemLockData? AllowedEntityGetLockData(BaseEntity baseEntity)
        {
            if (baseEntity == null || string.IsNullOrEmpty(baseEntity.PrefabName)) return null;
            return _allowedItemsLock.FirstOrDefault(a =>
                !string.IsNullOrEmpty(a.PrefabName) && a.PrefabName.ToLower().Equals(baseEntity.PrefabName.ToLower()));
        }

        private List<ItemLockData> PlayerGetAllowedItemLockDataList(BasePlayer player)
        {
            var playerAllowedItemLockList = new List<ItemLockData>();
            if (player == null || player.IsNpc) return playerAllowedItemLockList;

            playerAllowedItemLockList.AddRange(_allowedItemsLock
                .Where(ai => ai.RequiredPermission.IsNullOrEmpty())
                .ToList());

            var allowedItemLockRequirePermission = _allowedItemsLock
                .Where(ai => !ai.RequiredPermission.IsNullOrEmpty())
                .ToList();

            if (!allowedItemLockRequirePermission.IsNullOrEmpty())
            {
                foreach (var itemLockRequirePermission in allowedItemLockRequirePermission)
                {
                    foreach (var requirePermission in itemLockRequirePermission.RequiredPermission)
                    {
                        var perm = GetValidPermission(requirePermission);
                        if (string.IsNullOrWhiteSpace(perm) || permission.UserHasPermission(player.UserIDString, perm))
                        {
                            if (playerAllowedItemLockList.Any(pai => pai.PrefabName.ToLower().Equals(itemLockRequirePermission.PrefabName.ToLower())))
                                continue;
                            playerAllowedItemLockList.Add(itemLockRequirePermission);
                        }
                    }
                }
            }

            return playerAllowedItemLockList;
        }

        private List<LockCategory> PlayerGetAllowedLockCategory(BasePlayer player)
        {
            var lockCategoryAllowedList = new List<LockCategory>();
            if (player == null || player.IsNpc) return lockCategoryAllowedList;

            var itemLockAllowed = PlayerGetAllowedItemLockDataList(player);
            if (itemLockAllowed.IsNullOrEmpty()) return lockCategoryAllowedList;

            lockCategoryAllowedList.AddRange(itemLockAllowed
                .GroupBy(p => p.LockCategory)
                .Select(g => g.First().LockCategory)
                .ToList());

            return lockCategoryAllowedList;
        }

        private bool IsAllowedEntity(BaseEntity baseEntity)
        {
            if (baseEntity == null || string.IsNullOrEmpty(baseEntity.PrefabName)) return false;
            return _allowedItemsLock.Any(a =>
                !string.IsNullOrEmpty(a.PrefabName) && a.PrefabName.ToLower().Equals(baseEntity.PrefabName.ToLower()));
        }

        private bool IsAllowedEntity(string prefabName)
        {
            if (string.IsNullOrEmpty(prefabName)) return false;
            return _allowedItemsLock.Any(a =>
                !string.IsNullOrEmpty(a.PrefabName) && a.PrefabName.ToLower().Equals(prefabName.ToLower()));
        }

        private bool CanByPassCodeLock(BasePlayer player)
        {
            if (player == null) return false;

            return (_config.AllowAdminToBypassCodeLock &&
                    permission.UserHasPermission(player.UserIDString, PermissionAdmin)) ||
                   permission.UserHasPermission(player.UserIDString, PermissionBypassForce);
        }

        private static bool PlayerHasAKeyLock(BasePlayer player) => RetrievePlayerKeyLock(player) != null;

        private static bool PlayerHasACodeLock(BasePlayer player) => RetrievePlayerCodeLock(player) != null;

        private static Item? RetrievePlayerKeyLock(BasePlayer player)
        {
            if (player == null || player.IsNpc) return null;

            var playerInventoryAllItems = new List<Item>();
            player.inventory.GetAllItems(playerInventoryAllItems);
            return playerInventoryAllItems.FirstOrDefault(item => item.info.shortname.Equals(KeyLockShortPrefabName));
        }

        private static Item? RetrievePlayerCodeLock(BasePlayer player)
        {
            if (player == null || player.IsNpc) return null;

            var playerInventoryAllItems = new List<Item>();
            player.inventory.GetAllItems(playerInventoryAllItems);
            return playerInventoryAllItems.FirstOrDefault(item => item.info.shortname.Equals(CodeLockShortPrefabName));
        }

        private static Item? RetrievePlayerDoorCloser(BasePlayer player)
        {
            if (player == null || player.IsNpc) return null;

            var playerInventoryAllItems = new List<Item>();
            player.inventory.GetAllItems(playerInventoryAllItems);
            return playerInventoryAllItems.FirstOrDefault(item => item.info.shortname.Equals(DoorCloserShortPrefabName));
        }

        private LockCategory LockCategoryByEntity(BaseEntity entity)
        {
            if (entity == null) return LockCategory.None;

            if (_windowsPrefabName.Contains(entity.PrefabName, StringComparer.OrdinalIgnoreCase))
                return LockCategory.Window;

            if (_medievalPrefabName.Contains(entity.PrefabName, StringComparer.OrdinalIgnoreCase))
                return LockCategory.Medieval;

            if (entity is Door) return LockCategory.Door;

            if (_boxesPrefabName.Contains(entity.PrefabName, StringComparer.OrdinalIgnoreCase))
                return LockCategory.Box;

            switch (entity)
            {
                case BoxStorage:
                    return LockCategory.StorageContainer;
                case Locker:
                    return LockCategory.Locker;
                case BuildingPrivlidge:
                    return LockCategory.Cupboard;
                case BaseVehicle:
                    return LockCategory.Vehicle;
                case BaseOven:
                    return LockCategory.Furnace;
                case VendingMachine:
                    return LockCategory.VendingMachine;
                case Composter:
                    return LockCategory.Composter;
                case MixingTable:
                    return LockCategory.MixingTable;
                case PlanterBox:
                    return LockCategory.Planter;
                case AutoTurret:
                    return LockCategory.AutoTurret;
                case SamSite:
                    return LockCategory.SamSite;
                case FlameTurret or GunTrap:
                    return LockCategory.Trap;
                case WeaponRack:
                    return LockCategory.WeaponRack;
                case StashContainer:
                    return LockCategory.Stash;
                case NeonSign:
                    return LockCategory.NeonSign;
            }

            if (IsAllowedEntity(entity)) return LockCategory.OtherCustomEntities;

            if (entity.HasSlot(BaseEntity.Slot.Lock)) return LockCategory.OtherLockableEntities;

            return LockCategory.None;
        }

        private AutoClosingType AutoClosingTypeByEntity(BaseEntity entity, bool bypassOnlyEnabledCondition = false)
        {
            if (entity == null) return AutoClosingType.None;

            var (autoClosingType, value) = _autoClosingEntities.FirstOrDefault(ent => ent.Value.Contains(entity.PrefabName));
            if (value == null || value.IsNullOrEmpty()) return AutoClosingType.None;

            var autoClosingConfiguration = _config.AutoClosingConfiguration;
            return autoClosingType switch
            {
                AutoClosingType.Door when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableDoorAutoClosing => AutoClosingType.Door,
                AutoClosingType.DoubleDoor when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableDoubleDoorAutoClosing => AutoClosingType.DoubleDoor,
                AutoClosingType.Window when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableWindowAutoClosing => AutoClosingType.Window,
                AutoClosingType.Garage when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableGarageAutoClosing => AutoClosingType.Garage,
                AutoClosingType.LadderHatch when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableLadderHatchAutoClosing => AutoClosingType.LadderHatch,
                AutoClosingType.ExternalGate when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableExternalGateAutoClosing => AutoClosingType.ExternalGate,
                AutoClosingType.FenceGate when bypassOnlyEnabledCondition || autoClosingConfiguration.EnableFenceGateAutoClosing => AutoClosingType.FenceGate,
                AutoClosingType.LegacyWoodShelterDoor when (bypassOnlyEnabledCondition || autoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosing) &&
                                                           entity is LegacyShelter or LegacyShelterDoor => AutoClosingType.LegacyWoodShelterDoor,
                _ => AutoClosingType.None
            };
        }

        private DoorCloser? GetDoorCloserByEntity(BaseEntity entity)
        {
            if (entity == null) return null;

            if (entity is LegacyShelter legacyShelter && legacyShelter.GetChildDoor() is { } legacyShelterDoor)
                return legacyShelterDoor.GetSlot(BaseEntity.Slot.UpperModifier) as DoorCloser;

            var doorCloser = entity.GetSlot(BaseEntity.Slot.UpperModifier) as DoorCloser;
            if (doorCloser == null && entity.GetParentEntity() != null)
                doorCloser = entity.GetParentEntity().GetSlot(BaseEntity.Slot.UpperModifier) as DoorCloser;

            return doorCloser;
        }

        private static void AdjustBaseLockPosition(BaseEntity entity, BaseLock baseLock, ItemLockData itemLockData)
        {
            if (entity == null || baseLock == null) return;

            baseLock.transform.localPosition = itemLockData.CodeLockPosition;
            baseLock.transform.localRotation = itemLockData.CodeLockRotation;
        }

        private static void AdjustDoorCloserPosition(BaseEntity entity, DoorCloser doorCloser, AutoClosingType autoClosingType)
        {
            if (entity == null || doorCloser == null) return;

            switch (autoClosingType)
            {
                case AutoClosingType.Door:
                    break;
                case AutoClosingType.DoubleDoor:
                case AutoClosingType.Garage:
                    doorCloser.transform.localPosition = new Vector3(0.0f, entity.bounds.size.y, 0f);
                    break;
                case AutoClosingType.Window:
                    doorCloser.transform.localPosition = new Vector3(0.0f, 1.3f, 0f);
                    break;
                case AutoClosingType.LadderHatch:
                    doorCloser.transform.localPosition = new Vector3(-1.0f, -0.1f, 0f);
                    break;
                case AutoClosingType.ExternalGate:
                    if (entity.PrefabName.Equals("assets/prefabs/building/gates.external.high/gates.external.high.wood/gates.external.high.wood.prefab"))
                    {
                        doorCloser.transform.localPosition = new Vector3(3.5f, 2.0f, -0.6f);
                        doorCloser.transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f);
                        return;
                    }

                    doorCloser.transform.localPosition = new Vector3(3.57f, 2.0f, -1.05f);
                    doorCloser.transform.localRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f);

                    break;
                case AutoClosingType.FenceGate:
                    doorCloser.transform.localPosition = new Vector3(0.0f, entity.bounds.size.y, 0f);
                    break;
                case AutoClosingType.LegacyWoodShelterDoor:
                    break;
                default:
                    return;
            }
        }

        private bool IsAutoLockerEnabled(BasePlayer player, AutoLockPlayerSettingsData settings)
        {
            if (player == null || settings == null) return false;

            return permission.UserHasPermission(player.UserIDString, PermissionAutoLockEnabled) && settings.AutoLockOnPlacementEnabled;
        }

        private bool IsAutoClosingEnabled(BasePlayer player)
        {
            if (player == null || _config.AutoClosingConfiguration == null) return false;

            var autoClosingConfiguration = _config.AutoClosingConfiguration;

            return permission.UserHasPermission(player.UserIDString, PermissionAutoClosingEnabled) && (
                autoClosingConfiguration.EnableDoorAutoClosing || autoClosingConfiguration.EnableDoubleDoorAutoClosing ||
                autoClosingConfiguration.EnableWindowAutoClosing || autoClosingConfiguration.EnableGarageAutoClosing ||
                autoClosingConfiguration.EnableLadderHatchAutoClosing || autoClosingConfiguration.EnableExternalGateAutoClosing ||
                autoClosingConfiguration.EnableFenceGateAutoClosing || autoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosing
            );
        }

        private void AutoLockApplySettings(BasePlayer player, BaseLock baseLock, AutoLockPlayerSettingsData settings)
        {
            baseLock.OwnerID = player.userID;

            var isAutoLockerEnabled = IsAutoLockerEnabled(player, settings);
            if (isAutoLockerEnabled)
                baseLock.SetFlag(BaseEntity.Flags.Locked, true);

            if (baseLock is not CodeLock codeLock) return;

            if (isAutoLockerEnabled)
            {
                codeLock.code = settings.CodeLock;
                codeLock.hasCode = true;

                if (settings.GuestCodeEnabled)
                {
                    codeLock.guestCode = settings.GuestCodeLock;
                    codeLock.hasGuestCode = true;
                }
            }

            codeLock.whitelistPlayers.Clear();
            codeLock.guestPlayers.Clear();

            if (!codeLock.whitelistPlayers.Contains(player.userID.Get()))
            {
                codeLock.whitelistPlayers.Add(player.userID);
            }

            if (CheckPlayerHasShareLocksWithClanTeamEnabled(settings.SteamID))
            {
                var clanInfo = GetClanInfo(player);
                if (clanInfo == null || clanInfo.ClanMemberUserIdList.IsNullOrEmpty())
                    return;

                codeLock.whitelistPlayers.AddRange(clanInfo.ClanMemberUserIdList.Select(ulong.Parse).ToList());
            }
        }

        private void AutoLockSendMessage(BasePlayer player, BaseLock baseLock, AutoLockPlayerSettingsData settings)
        {
            if (player == null || baseLock == null || settings == null) return;

            switch (baseLock)
            {
                case CodeLock when settings.GuestCodeEnabled:
                    SendMessage(player,
                        Lang("AutoLockCodeLockAndGuestCodeDeployedMessage", player.UserIDString, settings.StreamerModeEnabled ? "****" : settings.CodeLock,
                            settings.StreamerModeEnabled ? "****" : settings.GuestCodeLock));
                    break;
                case CodeLock:
                    SendMessage(player,
                        Lang("AutoLockCodeLockDeployedMessage", player.UserIDString, settings.StreamerModeEnabled ? "****" : settings.CodeLock));
                    break;
                case KeyLock:
                    SendMessage(player, Lang("AutoLockKeyLockDeployedMessage", player.UserIDString));
                    break;
            }
        }

        private bool CanDeployLock(BasePlayer player, BaseEntity baseEntity, bool ignoreExistingLock = false)
        {
            if (string.IsNullOrEmpty(baseEntity.PrefabName)) return false;

            var entityToCheck = GetMainEntity(baseEntity);
            if (entityToCheck is null) return false;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) || !IsAllowedEntity(entityToCheck)) return false;

            if (IsGunShipVehicle(baseEntity)) return false;

            //START - Required Permission check
            var itemLockData = _allowedItemsLock.FirstOrDefault(i => !string.IsNullOrEmpty(i.PrefabName) && i.PrefabName.ToLower().Equals(entityToCheck.PrefabName.ToLower()));

            if (itemLockData != null && !HasValidPermission(player, itemLockData.RequiredPermission)) return false;
            //END - Required Permission check


            if (_config.RequiresBuildingPrivilege && !permission.UserHasPermission(player.UserIDString, PermissionBypassLockDeployVehicleCheck) && player.IsBuildingBlocked())
                return false;


            if (entityToCheck is MiningQuarry miningQuarry)
            {
                if (miningQuarry.isStatic && !miningQuarry.OwnerID.IsSteamId())
                    return false;
            }

            if (entityToCheck is ModularCar modularCar)
            {
                if (modularCar.CarLock.HasALock)
                    return false;

                if (ignoreExistingLock && !modularCar.CarLock.HasALock)
                    return false;

                if (player.GetActiveItem() == null || player.GetActiveItem().info.itemid.Equals(KeyLockItemID))
                    return false;
            }

            if (entityToCheck is BaseVehicle baseVehicle)
            {
                if (!ignoreExistingLock && baseVehicle.GetSlot(BaseEntity.Slot.Lock) is not null)
                    return false;

                if (_config.RequiresBuildingPrivilegeToDeployCodeLockInUnownedVehicles && !baseVehicle.OwnerID.IsSteamId() && player.IsBuildingBlocked())
                    return permission.UserHasPermission(player.UserIDString, PermissionBypassLockDeployVehicleCheck) || player.IsBuildingAuthed();

                if (_config.AllowDeployCodeLockInOwnedPlayersVehicles && baseVehicle.OwnerID.IsSteamId())
                    return true;

                if (!baseVehicle.OwnerID.IsSteamId() &&
                    (_config.AllowDeployCodeLockInUnownedVehicles || permission.UserHasPermission(player.UserIDString, PermissionBypassLockDeployVehicleCheck)))
                    return true;
            }

            if ((!ignoreExistingLock && entityToCheck.GetSlot(BaseEntity.Slot.Lock) is not null) || !IsEntityOwner(entityToCheck, player))
                return false;

            return true;
        }

        private bool DeployLock(BasePlayer player, BaseEntity baseEntity, bool byPassActiveItem = false)
        {
            try
            {
                if (baseEntity.GetSlot(BaseEntity.Slot.Lock) is not null)
                    return false;

                var entityToCheck = GetMainEntity(baseEntity);
                if (entityToCheck is null) return false;

                if (!permission.UserHasPermission(player.UserIDString, PermissionUse) || !IsAllowedEntity(entityToCheck)) return false;

                if (IsGunShipVehicle(baseEntity)) return false;

                var codeLockPosition = _allowedItemsLock.FirstOrDefault(i => !string.IsNullOrEmpty(i.PrefabName) && i.PrefabName.ToLower().Equals(entityToCheck.PrefabName.ToLower()));

                if (codeLockPosition == null)
                {
                    RunEffect(CodeLockDeniedEffect, player);
                    return false;
                }

                BaseLock? locker;
                if (!byPassActiveItem)
                {
                    if (player.GetActiveItem().info.itemid.Equals(KeyLockItemID))
                        locker = GameManager.server.CreateEntity(KeyLockPrefab, codeLockPosition.CodeLockPosition,
                            codeLockPosition.CodeLockRotation) as KeyLock;
                    else if (!_config.AutoLockConfiguration.UseOnlyKeyLock && player.GetActiveItem().info.itemid.Equals(CodeLockItemID))
                        locker = GameManager.server.CreateEntity(CodeLockPrefab, codeLockPosition.CodeLockPosition,
                            codeLockPosition.CodeLockRotation) as CodeLock;
                    else
                        return false;
                }
                else
                {
                    locker = GameManager.server.CreateEntity(CodeLockPrefab, codeLockPosition.CodeLockPosition,
                        codeLockPosition.CodeLockRotation) as CodeLock;
                }

                if (locker is null)
                {
                    RunEffect(CodeLockDeniedEffect, player);
                    return false;
                }

                var autoLockPlayerSettings = GetAutoLockPlayerSettingsData(player.userID.Get());

                if (entityToCheck is ModularCar modularCar)
                {
                    if (modularCar.CarLock.HasALock)
                        return false;

                    var codeLockPin = DefaultModularCarCodeLockCode;
                    if (autoLockPlayerSettings is { AutoLockOnPlacementEnabled: true, VehicleLockEnabled: true })
                        codeLockPin = autoLockPlayerSettings.CodeLock;

                    var codeLockAdded = modularCar.CarLock.TryAddALock(codeLockPin, player.userID);
                    if (!codeLockAdded)
                        return false;

                    // modularCar.OwnerID = player.userID; => Handled by OnModularCarLockAdded Hook
                    modularCar.CarLock.TryAddPlayer(player.userID);


                    if (codeLockPin.Equals(DefaultModularCarCodeLockCode))
                        SendMessage(player, Lang("ModularCarNewCodeLockMessage", player.UserIDString, modularCar.CarLock.Code));
                    else
                        SendMessage(player, Lang("AutoLockCodeLockDeployedMessage", player.UserIDString,
                            autoLockPlayerSettings.StreamerModeEnabled ? "****" : autoLockPlayerSettings.CodeLock));


                    return true;
                }

                if (locker is KeyLock keyLock)
                {
                    keyLock.SetFlag(BaseEntity.Flags.Locked, true);
                }

                locker.Spawn();
                // codeLock.OwnerID = baseEntity.OwnerID;
                locker.OwnerID = player.userID;
                locker.SetParent(entityToCheck, false, false);

                entityToCheck.SetSlot(BaseEntity.Slot.Lock, locker);
                locker.SendNetworkUpdateImmediate(true);

                EntityDeployed(locker, null, player);

                if (autoLockPlayerSettings.AutoLockOnPlacementEnabled)
                    Interface.CallHook("OnEntitySpawned", locker);

                return true;
            }
            catch
            {
                return false;
            }
        }

        private void DeployLocker(BasePlayer player, BaseEntity entity, bool sendMessage)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (sendMessage)
                    SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            if (IsAnEntityToIgnore(entity)) return;

            var autoLockPlayerSettings = GetAutoLockPlayerSettingsData(player.userID.Get());

            var targetEntity = entity;
            if (entity is BaseLock baseLock && entity.GetParentEntity() != null)
            {
                targetEntity = baseLock.GetParentEntity();
            }

            var lockCategory = LockCategoryByEntity(targetEntity);
            switch (lockCategory)
            {
                case LockCategory.None:
                    if (sendMessage)
                        SendMessage(player, Lang("LockCategoryNotValid", player.UserIDString));
                    return;
                case LockCategory.Door when autoLockPlayerSettings.DoorsLockEnabled:
                case LockCategory.Window when autoLockPlayerSettings.WindowsLockEnabled:
                case LockCategory.Box when autoLockPlayerSettings.BoxesLockEnabled:
                case LockCategory.StorageContainer when autoLockPlayerSettings.StorageContainerLockEnabled:
                case LockCategory.Locker when autoLockPlayerSettings.LockersLockEnabled:
                case LockCategory.Cupboard when autoLockPlayerSettings.CupboardsLockEnabled:
                case LockCategory.Vehicle when autoLockPlayerSettings.VehicleLockEnabled:
                case LockCategory.Medieval when autoLockPlayerSettings.MedievalEntityLockEnabled:
                case LockCategory.Furnace when autoLockPlayerSettings.FurnaceLockEnabled:
                case LockCategory.VendingMachine when autoLockPlayerSettings.VendingMachineLockEnabled:
                case LockCategory.Composter when autoLockPlayerSettings.ComposterLockEnabled:
                case LockCategory.MixingTable when autoLockPlayerSettings.MixingTableLockEnabled:
                case LockCategory.Planter when autoLockPlayerSettings.PlanterLockEnabled:
                case LockCategory.AutoTurret when autoLockPlayerSettings.AutoTurretLockEnabled:
                case LockCategory.SamSite when autoLockPlayerSettings.SamSiteLockEnabled:
                case LockCategory.Trap when autoLockPlayerSettings.TrapsLockEnabled:
                case LockCategory.WeaponRack when autoLockPlayerSettings.WeaponRackLockEnabled:
                case LockCategory.Stash when autoLockPlayerSettings.StashLockEnabled:
                case LockCategory.NeonSign when autoLockPlayerSettings.NeonSignLockEnabled:
                case LockCategory.OtherCustomEntities when autoLockPlayerSettings.OtherCustomEntitiesLockEnabled:
                case LockCategory.OtherLockableEntities when autoLockPlayerSettings.OtherLockableEntitiesLockEnabled:
                    PlaceLocker(player, targetEntity, autoLockPlayerSettings, sendMessage);
                    return;
                default:
                    if (sendMessage)
                        SendMessage(player, Lang("LockCategoryNotValid", player.UserIDString));
                    return;
            }
        }

        private void DeployDoorCloser(BasePlayer player, BaseEntity entity, bool sendMessage)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (sendMessage)
                    SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            if (!IsAutoClosingEnabled(player))
            {
                if (sendMessage)
                    SendMessage(player, Lang("AutoClosingNotEnabled", player.UserIDString));
                return;
            }

            var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(player);
            if (!autoClosingPlayerSettings.AutoClosingEnabled)
            {
                if (sendMessage)
                    SendMessage(player, Lang("PlayerAutoClosingNotEnabled", player.UserIDString));
                return;
            }

            var autoClosingType = AutoClosingTypeByEntity(entity);
            if (autoClosingType == AutoClosingType.None)
            {
                if (sendMessage)
                    SendMessage(player, Lang("AutoClosingTypeNotValid", player.UserIDString));
                return;
            }

            switch (autoClosingType)
            {
                case AutoClosingType.Door when autoClosingPlayerSettings.DoorAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.Door, autoClosingPlayerSettings.DoorClosingDelay, autoClosingPlayerSettings, sendMessage);
                    break;
                case AutoClosingType.DoubleDoor when autoClosingPlayerSettings.DoubleDoorAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.DoubleDoor, autoClosingPlayerSettings.DoubleDoorClosingDelay,
                        autoClosingPlayerSettings, sendMessage);
                    break;
                case AutoClosingType.Window when autoClosingPlayerSettings.WindowAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.Window, autoClosingPlayerSettings.WindowClosingDelay,
                        autoClosingPlayerSettings, sendMessage);
                    break;
                case AutoClosingType.Garage when autoClosingPlayerSettings.GarageAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.Garage, autoClosingPlayerSettings.GarageClosingDelay + GarageOpenDuration,
                        autoClosingPlayerSettings, sendMessage);
                    break;
                case AutoClosingType.LadderHatch when autoClosingPlayerSettings.LadderHatchAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.LadderHatch, autoClosingPlayerSettings.LadderHatchClosingDelay,
                        autoClosingPlayerSettings, sendMessage);
                    break;
                case AutoClosingType.ExternalGate when autoClosingPlayerSettings.ExternalGateAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.ExternalGate, autoClosingPlayerSettings.ExternalGateClosingDelay,
                        autoClosingPlayerSettings, sendMessage);
                    break;
                case AutoClosingType.FenceGate when autoClosingPlayerSettings.FenceGateAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.FenceGate, autoClosingPlayerSettings.FenceGateClosingDelay, autoClosingPlayerSettings,
                        sendMessage);
                    break;
                case AutoClosingType.LegacyWoodShelterDoor when autoClosingPlayerSettings.LegacyWoodShelterDoorAutoClosingEnabled:
                    PlaceDoorCloser(player, entity, AutoClosingType.LegacyWoodShelterDoor, autoClosingPlayerSettings.LegacyWoodShelterDoorClosingDelay,
                        autoClosingPlayerSettings, sendMessage);
                    break;
                default:
                    if (sendMessage)
                        SendMessage(player, Lang("AutoClosingTypeNotValid", player.UserIDString));
                    return;
            }
        }

        private void PlaceLocker(BasePlayer player, BaseEntity entity, AutoLockPlayerSettingsData settings, bool sendMessage)
        {
            if (player == null || entity == null || settings == null)
            {
                if (sendMessage && player != null)
                    SendMessage(player, Lang("MissingParametersError", player.UserIDString, "player or entity or settings"));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (sendMessage)
                    SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            var isAutoLockerEnabled = IsAutoLockerEnabled(player, settings);
            if (isAutoLockerEnabled)
            {
                if (string.IsNullOrWhiteSpace(settings.CodeLock) || !IsValidLockCode(settings.CodeLock))
                {
                    settings.CodeLock = GenerateRandomCode();
                    SaveData();
                }

                if (settings.GuestCodeEnabled && (string.IsNullOrWhiteSpace(settings.GuestCodeLock) || !IsValidLockCode(settings.GuestCodeLock)))
                {
                    settings.GuestCodeLock = GenerateRandomCode();
                    SaveData();
                }
            }

            //Check if it is a custom entity and if it can be locked
            var customLockableItemData = AllowedEntityGetLockData(entity);
            if (customLockableItemData != null && !CanDeployLock(player, entity, true))
            {
                if (sendMessage)
                    SendMessage(player, Lang("EntityNotAllowed", player.UserIDString));
                return;
            }

            if (IsGunShipVehicle(entity)) return;

            //If it is not a custom entity, verify that it is a lockable entity
            if (customLockableItemData == null && !entity.HasSlot(BaseEntity.Slot.Lock))
            {
                if (sendMessage)
                    SendMessage(player, Lang("EntityNotAllowed", player.UserIDString));
                return;
            }

            //If the lock is positioned manually, apply the various settings
            var currentLock = entity.GetSlot(BaseEntity.Slot.Lock) as BaseLock;
            if (currentLock != null)
            {
                if (!isAutoLockerEnabled) return;

                if (currentLock is CodeLock cl && !_config.AutoLockConfiguration.UseOnlyKeyLock)
                {
                    var oldCode = cl.code;
                    AutoLockApplySettings(player, currentLock, settings);
                    var newCode = cl.code;

                    if (oldCode.Equals(newCode))
                        return;
                }

                AutoLockSendMessage(player, currentLock, settings);

                return;
            }

            if (!isAutoLockerEnabled)
            {
                if (sendMessage)
                    SendMessage(player, Lang("AutoLockerNotEnabled", player.UserIDString));
                return;
            }

            Item? lockerItem = null;
            BaseLock? locker = null;
            if (permission.UserHasPermission(player.UserIDString, PermissionAutoLockNoLockRequired))
            {
                locker = GameManager.server.CreateEntity(_config.AutoLockConfiguration.UseOnlyKeyLock ? KeyLockPrefab : CodeLockPrefab,
                        customLockableItemData?.CodeLockPosition ?? default,
                        customLockableItemData?.CodeLockRotation ?? default)
                    as BaseLock;
            }

            if (locker == null)
            {
                if (!_config.AutoLockConfiguration.UseOnlyKeyLock)
                    lockerItem = RetrievePlayerCodeLock(player);

                if (lockerItem != null)
                {
                    locker = GameManager.server.CreateEntity(CodeLockPrefab,
                            customLockableItemData?.CodeLockPosition ?? default,
                            customLockableItemData?.CodeLockRotation ?? default)
                        as CodeLock;
                }

                if (locker == null && (settings.AlsoUseKeyLockEnabled || _config.AutoLockConfiguration.UseOnlyKeyLock))
                {
                    lockerItem = RetrievePlayerKeyLock(player);
                    if (lockerItem != null)
                    {
                        locker = GameManager.server.CreateEntity(KeyLockPrefab,
                                customLockableItemData?.CodeLockPosition ?? default,
                                customLockableItemData?.CodeLockRotation ?? default)
                            as KeyLock;
                    }
                }

                if ((locker == null || locker is CodeLock) && _config.AutoLockConfiguration.UseOnlyKeyLock)
                {
                    if (sendMessage)
                        SendMessage(player, Lang("UseOnlyKeyLock_KeyLockNotFound", player.UserIDString));
                    return;
                }
            }

            if (locker == null)
            {
                if (sendMessage)
                    SendMessage(player, Lang("LockItemNotFound", player.UserIDString));
                return;
            }

            AutoLockApplySettings(player, locker, settings);
            locker.Spawn();

            //If it's a custom entity, use the custom position for the lock
            if (customLockableItemData != null)
                locker.SetParent(entity);
            else
                locker.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.Lock));

            entity.SetSlot(BaseEntity.Slot.Lock, locker);

            lockerItem?.UseItem();


            if (isAutoLockerEnabled && locker is CodeLock cLock)
            {
                AutoLockSendMessage(player, locker, settings);

                if (cLock.IsLocked())
                {
                    RunEffect(CodeLockLockEffect, entity);
                    return;
                }
            }

            RunEffect(CodeLockDeployEffect, entity);
        }

        private void PlaceDoorCloser(BasePlayer player, BaseEntity entity, AutoClosingType autoClosingType, int autoClosingDelayTime,
            AutoClosingPlayerSettingsData settings, bool sendMessage)
        {
            if (player == null || entity == null || settings == null)
            {
                if (sendMessage && player != null)
                    SendMessage(player, Lang("MissingParametersError", player.UserIDString, "player or entity or settings"));
                return;
            }

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (sendMessage)
                    SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            if (!IsAutoClosingEnabled(player))
            {
                if (sendMessage)
                    SendMessage(player, Lang("AutoClosingNotEnabled", player.UserIDString));
                return;
            }

            if (GetDoorCloserByEntity(entity) != null)
            {
                if (sendMessage)
                    SendMessage(player, Lang("DoorCloserAlreadyExists", player.UserIDString));
                return;
            }

            if (autoClosingDelayTime <= 0)
                autoClosingDelayTime = _config.AutoClosingConfiguration.DefaultClosingDelayTime;

            Item? doorCloserItem = null;
            DoorCloser? doorCloser = null;
            if (permission.UserHasPermission(player.UserIDString, PermissionAutoClosingNoDoorCloserRequired))
            {
                doorCloser = GameManager.server.CreateEntity(DoorCloserPrefabName) as DoorCloser;
            }

            if (doorCloser == null)
            {
                doorCloserItem = RetrievePlayerDoorCloser(player);
                if (doorCloserItem != null)
                {
                    doorCloser = GameManager.server.CreateEntity(DoorCloserPrefabName) as DoorCloser;
                }
            }

            if (doorCloser == null)
            {
                if (sendMessage)
                    SendMessage(player, Lang("DoorCloserItemNotFound", player.UserIDString));
                return;
            }

            doorCloser.gameObject.Identity();
            doorCloser.OwnerID = player.userID;
            doorCloser.delay = autoClosingDelayTime;


            if (AutoClosingType.LegacyWoodShelterDoor == autoClosingType &&
                entity is LegacyShelter legacyShelter && legacyShelter.GetChildDoor() is { } legacyShelterDoor)
                doorCloser.SetParent(legacyShelterDoor, legacyShelterDoor.GetSlotAnchorName(BaseEntity.Slot.UpperModifier));
            else
                doorCloser.SetParent(entity, entity.GetSlotAnchorName(BaseEntity.Slot.UpperModifier));

            doorCloser.OnDeployed(entity, null, null);

            AdjustDoorCloserPosition(entity, doorCloser, autoClosingType);

            doorCloser.Spawn();


            if (AutoClosingType.LegacyWoodShelterDoor == autoClosingType &&
                entity is LegacyShelter legacyShelter2 && legacyShelter2.GetChildDoor() is { } legacyShelterDoor2)
                legacyShelterDoor2.SetSlot(BaseEntity.Slot.UpperModifier, doorCloser);
            else
                entity.SetSlot(BaseEntity.Slot.UpperModifier, doorCloser);


            doorCloserItem?.UseItem();

            if (sendMessage)
                SendMessage(player, Lang("DoorCloserDeployed", player.UserIDString, autoClosingDelayTime));
        }

        private BaseEntity? GetMainEntity(BaseEntity baseEntity)
        {
            if (baseEntity == null || string.IsNullOrEmpty(baseEntity.PrefabName)) return null;

            if (baseEntity is ModularCar ||
                (baseEntity.GetParentEntity() != null &&
                 baseEntity.GetParentEntity() is ModularCar))
            {
                return IsAllowedEntity(baseEntity) ? baseEntity :
                    IsAllowedEntity(baseEntity.GetParentEntity()) ? baseEntity.GetParentEntity() : baseEntity;
            }

            return baseEntity;
        }

        //TODO DA RIMUOVERE
        // private BaseEntity? GetVehicleCockpit(ModularCar modularCar)
        // {
        //     if (modularCar == null || string.IsNullOrEmpty(modularCar.PrefabName)) return null;
        //     var vehicleCockpit =
        //         modularCar.children.FirstOrDefault(ch => _vehiclesCockpitPrefabName.Contains(ch.PrefabName));
        //     return vehicleCockpit == null ? null : vehicleCockpit;
        // }

        private void UpdateAllCodeLockAuthShareByClanName(string clanName)
        {
            var clanMembers = GetClanMembers(clanName);
            if (clanMembers == null || clanMembers.Count == 0) return;

            var clanMembersSteamIdList = clanMembers.Select(member => Convert.ToUInt64((string)member)).ToList();
            if (clanMembersSteamIdList.IsNullOrEmpty()) return;

            ClanTeamUpdateAllCodeLockAuthShare(clanMembersSteamIdList);
        }

        private void UpdateAllCodeLockAuthShareByTeamMembers(List<ulong>? memberList)
        {
            ClanTeamUpdateAllCodeLockAuthShare(memberList);
        }

        private void ClanTeamUpdateAllCodeLockAuthShare(List<ulong>? memberList)
        {
            if (memberList == null || memberList.IsNullOrEmpty()) return;

            var allCodeLockList = BaseNetworkable.serverEntities
                .Where(se =>
                    se is CodeLock cl &&
                    cl.OwnerID.IsSteamId() &&
                    memberList.Contains(cl.OwnerID))
                .Cast<BaseEntity>()
                .ToList();

            allCodeLockList.AddRange(BaseNetworkable.serverEntities
                .Where(se =>
                    se is ModularCar be && (
                        be.ShortPrefabName.Contains(ModuleCarPartialShortPrefabName) ||
                        be.PrefabName.StartsWith(CarChassisModuleCarPartialPrefabName, StringComparison.OrdinalIgnoreCase)
                    ) &&
                    be.OwnerID.IsSteamId() &&
                    memberList.Contains(be.OwnerID))
                .Cast<BaseEntity>()
                .ToList());

            foreach (var playerSteamId in memberList)
            {
                if (!playerSteamId.IsSteamId())
                    return;

                var player = BasePlayer.FindAwakeOrSleeping(playerSteamId.ToString());
                if (player == null || player.IsNpc) return;

                _coroutines.Add(ServerMgr.Instance.StartCoroutine(UpdateAllCodeLockAuthShareOrSettings(allCodeLockList, player)));
            }
        }

        private IEnumerator UpdateAllCodeLockAuthShareOrSettings(List<BaseEntity> baseEntityList, BasePlayer basePlayer,
            string newLockCode = "", bool isGuestCode = false, bool removeGuestCode = false, bool sendMessage = false, bool sendSharedAuthMessage = false)
        {
            var count = 0;
            foreach (var baseEntity in baseEntityList)
            {
                if (baseEntity == null || !IsEntityOwner(baseEntity, basePlayer)) continue;

                switch (baseEntity)
                {
                    case ModularCar modularCar:
                        if (!modularCar.CarLock.HasALock) continue;
                        UpdateCodeLockAuthShareOrSettings(null, modularCar, basePlayer, newLockCode, isGuestCode, removeGuestCode);
                        break;
                    case CodeLock codeLock:
                        if (!codeLock.OwnerID.IsSteamId()) continue;
                        UpdateCodeLockAuthShareOrSettings(codeLock, null, basePlayer, newLockCode, isGuestCode, removeGuestCode);
                        break;
                    default:
                        continue;
                }

                if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
                count++;
            }

            if (sendMessage)
            {
                SendMessage(basePlayer,
                    removeGuestCode
                        ? Lang("AllGuestCodeLockPinRemovedMessage", basePlayer.UserIDString)
                        : isGuestCode
                            ? Lang("AllGuestCodeLockPinChangedMessage", basePlayer.UserIDString)
                            : Lang("AllCodeLockPinChangedMessage", basePlayer.UserIDString));
            }

            if (sendSharedAuthMessage)
            {
                SendMessage(basePlayer, Lang("AllCodeLockSharedAuthPinChangedMessage", basePlayer.UserIDString));
            }
        }

        private void UpdateCodeLockAuthShareOrSettings(CodeLock? codeLock, ModularCar? modularCar, BasePlayer basePlayer,
            string newLockCode = "", bool isGuestCode = false, bool removeGuestCode = false, bool sendMessage = false)
        {
            if (codeLock == null && modularCar == null)
                return;

            BaseEntity baseCodeLock = codeLock != null ? codeLock : modularCar!;
            if (baseCodeLock == null || !IsEntityOwner(baseCodeLock, basePlayer)) return;

            if (_config.AutoLockConfiguration.UseOnlyKeyLock)
                return;

            NextFrame(() =>
            {
                AutoLockPlayerSettingsData? autoLockPlayerSettings = null;
                switch (baseCodeLock)
                {
                    case ModularCar _modularCar when !isGuestCode:

                        var modularCarWhitelistPlayers = _modularCar.CarLock.WhitelistPlayers.ToList();
                        foreach (var modularCarWhitelistPlayer in modularCarWhitelistPlayers)
                        {
                            _modularCar.CarLock.TryRemovePlayer(modularCarWhitelistPlayer);
                        }

                        if (!string.IsNullOrWhiteSpace(newLockCode))
                        {
                            _modularCar.CarLock.TrySetNewCode(newLockCode, basePlayer.userID);
                        }

                        _modularCar.CarLock.TryAddPlayer(basePlayer.userID);

                        _modularCar.SendNetworkUpdateImmediate();

                        if (sendMessage)
                        {
                            SendMessage(basePlayer, Lang("CodeLockPinChangedMessage", basePlayer.UserIDString));
                        }

                        autoLockPlayerSettings = GetAutoLockPlayerSettingsData(basePlayer.userID.Get());
                        if (CheckPlayerHasShareLocksWithClanTeamEnabled(autoLockPlayerSettings.SteamID))
                        {
                            var clanInfo = GetClanInfo(basePlayer);
                            if (clanInfo == null) return;

                            if (!clanInfo.ClanMemberUserIdList.IsNullOrEmpty())
                            {
                                var clanMemberUserIdList = clanInfo.ClanMemberUserIdList.Select(ulong.Parse).ToList();
                                foreach (var clanMember in clanMemberUserIdList)
                                {
                                    _modularCar.CarLock.TryAddPlayer(clanMember);
                                }
                            }
                        }

                        _modularCar.SendNetworkUpdateImmediate();

                        break;

                    case CodeLock _codeLock:

                        _codeLock.guestPlayers.Clear();
                        _codeLock.whitelistPlayers.Clear();

                        if (!string.IsNullOrWhiteSpace(newLockCode) && !isGuestCode)
                        {
                            _codeLock.whitelistPlayers.Clear();
                            _codeLock.whitelistPlayers.Add(basePlayer.userID);
                            _codeLock.code = newLockCode;
                        }

                        if (!string.IsNullOrWhiteSpace(newLockCode) && isGuestCode)
                        {
                            _codeLock.guestCode = newLockCode;
                            _codeLock.hasGuestCode = isGuestCode;
                        }

                        if (removeGuestCode)
                        {
                            _codeLock.guestCode = string.Empty;
                            _codeLock.hasGuestCode = false;
                        }

                        if (!_codeLock.whitelistPlayers.Contains(basePlayer.userID.Get()))
                        {
                            _codeLock.whitelistPlayers.Add(basePlayer.userID);
                        }

                        _codeLock.SendNetworkUpdateImmediate();

                        if (sendMessage)
                        {
                            SendMessage(basePlayer, Lang("CodeLockPinChangedMessage", basePlayer.UserIDString));
                        }


                        autoLockPlayerSettings = GetAutoLockPlayerSettingsData(basePlayer.userID.Get());
                        if (CheckPlayerHasShareLocksWithClanTeamEnabled(autoLockPlayerSettings.SteamID))
                        {
                            var clanInfo = GetClanInfo(basePlayer);
                            if (clanInfo == null) return;

                            if (!clanInfo.ClanMemberUserIdList.IsNullOrEmpty())
                            {
                                _codeLock.whitelistPlayers.AddRange(clanInfo.ClanMemberUserIdList.Select(ulong.Parse).ToList());
                            }
                        }

                        _codeLock.SendNetworkUpdateImmediate();

                        break;
                }
            });
        }

        //TODO DA RIMUOVERE
        // private static bool IsPlayerEntity(BaseLock baseLock)
        // {
        //     if (baseLock == null)
        //         return false;
        //     return baseLock != null && baseLock.OwnerID.IsSteamId();
        // }

        private bool IsInvisible(BasePlayer player)
        {
            if (player == null) return false;
            return player.IsFlying || (Vanish?.Call<bool>("IsInvisible", player) ?? false);
        }

        private static bool IsVendingMachineOpen(BasePlayer player, BaseEntity entity)
        {
            if (entity is VendingMachine vendingMachine)
            {
                return vendingMachine.PlayerInfront(player);
            }

            return false;
        }

        private static bool IsDropBoxOpen(BasePlayer player, BaseEntity entity)
        {
            if (entity is DropBox dropBox)
            {
                return dropBox.PlayerInfront(player);
            }

            return false;
        }

        private static bool IsShopFrontCustomerPosOpen(BasePlayer player, BaseEntity entity)
        {
            if (entity is ShopFront shopFront)
            {
                return shopFront.PlayerInCustomerPos(player);
            }

            return false;
        }

        private void EntityDeployed(BaseLock? baseLock, ModularCar? modularCar, BasePlayer ownerPlayer)
        {
            if ((baseLock != null || modularCar != null) && ownerPlayer == null ||
                !ownerPlayer.userID.IsSteamId()) return;

            BaseEntity codeLockEntity = baseLock != null ? baseLock : modularCar!;

            _data.CodeLockItemsData.TryGetValue(ownerPlayer.userID, out var codeLockItemsDataList);
            if (codeLockItemsDataList == null)
            {
                _data.CodeLockItemsData.Add(ownerPlayer.userID, new List<CodeLockItemsData>());
            }

            var teleportCoordinate = CoordinateForTeleport(codeLockEntity.transform.position);
            var currentDateTime = CurrentDateTime();
            var parentEntity = codeLockEntity.GetParentEntity() != null ? codeLockEntity.GetParentEntity() : null;

            _data.CodeLockItemsData[ownerPlayer.userID].Add(new CodeLockItemsData
            {
                OwnerId = ownerPlayer.userID,
                PlayerName = ownerPlayer.displayName,
                NetId = codeLockEntity.net.ID.Value,
                ShortPrefabName = codeLockEntity.ShortPrefabName,
                ParentEntityNetId = parentEntity != null && parentEntity.net != null ? parentEntity.net.ID.Value : null,
                ParentEntity = parentEntity != null ? parentEntity.ShortPrefabName : "",
                DeployCoordinate = CoordinateFromPosition(codeLockEntity.transform.position),
                TeleportCoordinate = $"teleportpos {teleportCoordinate}",
                DeployDate = currentDateTime,
                DeployTimestamp = (ulong)currentDateTime.Subtract(_epochDateTime).TotalMilliseconds
            });
            SaveData();
        }

        private void EntityRemoved(BaseLock? baseLock, ModularCar? modularCar, BasePlayer ownerPlayer)
        {
            if ((baseLock != null || modularCar != null) && ownerPlayer == null ||
                !ownerPlayer.userID.IsSteamId()) return;

            BaseEntity codeLockEntity = baseLock != null ? baseLock : modularCar!;

            if (!_data.CodeLockItemsData.TryGetValue(ownerPlayer.userID, out var codeLockItemsDataList))
                return;

            if (!codeLockItemsDataList.Any(cl => cl.NetId.Equals(codeLockEntity.net.ID.Value)))
                return;

            codeLockItemsDataList.RemoveAll(cl => cl.NetId.Equals(codeLockEntity.net.ID.Value));
            SaveData();
        }

        private void DoorCloserCustomDelayTimeChanged(BasePlayer ownerPlayer, DoorCloser doorCloser)
        {
            if (doorCloser == null || !ownerPlayer.userID.IsSteamId()) return;

            _data.DoorCloserCustomTimeItemsData.TryGetValue(ownerPlayer.userID, out var doorCloserItemData);
            if (doorCloserItemData == null)
            {
                _data.DoorCloserCustomTimeItemsData.Add(ownerPlayer.userID, new List<DoorCloserItemData>());
            }

            //Update existing Door Closer
            var doorCloserItem = _data.DoorCloserCustomTimeItemsData[ownerPlayer.userID].FirstOrDefault(dc => dc.NetId.Equals(doorCloser.net.ID.Value));
            if (doorCloserItem != null)
            {
                doorCloserItem.PlayerName = ownerPlayer.displayName;
                doorCloserItem.DelayTime = (int)doorCloser.delay;

                SaveData();
                return;
            }

            //Add new Door Closer
            var teleportCoordinate = CoordinateForTeleport(doorCloser.transform.position);
            var currentDateTime = CurrentDateTime();
            _data.DoorCloserCustomTimeItemsData[ownerPlayer.userID].Add(new DoorCloserItemData
            {
                OwnerId = ownerPlayer.userID,
                PlayerName = ownerPlayer.displayName,
                NetId = doorCloser.net.ID.Value,
                DelayTime = (int)doorCloser.delay,
                ParentEntity = doorCloser.GetParentEntity() != null
                    ? doorCloser.GetParentEntity().ShortPrefabName
                    : "",
                DeployCoordinate = CoordinateFromPosition(doorCloser.transform.position),
                TeleportCoordinate = $"teleportpos {teleportCoordinate}",
                DeployDate = currentDateTime,
                DeployTimestamp = (ulong)currentDateTime.Subtract(_epochDateTime).TotalMilliseconds
            });
            SaveData();
        }

        private void DoorCloserRemoved(BasePlayer ownerPlayer, DoorCloser doorCloser)
        {
            if (doorCloser == null || !ownerPlayer.userID.IsSteamId()) return;

            if (!_data.DoorCloserCustomTimeItemsData.TryGetValue(ownerPlayer.userID, out var doorCloserItemDataList))
                return;

            if (!doorCloserItemDataList.Any(cl => cl.NetId.Equals(doorCloser.net.ID.Value)))
                return;

            doorCloserItemDataList.RemoveAll(cl => cl.NetId.Equals(doorCloser.net.ID.Value));
            SaveData();
        }

        private DateTime CurrentDateTime()
        {
            try
            {
                return TimeZoneInfo
                    .ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow,
                        string.IsNullOrEmpty(_config.TimeZone) ? DefaultTimeZone : _config.TimeZone).DateTime;
            }
            catch
            {
                //return DateTime.Now;
                return DateTime.UtcNow;
            }
        }

        private static string CoordinateFromPosition(Vector3 pos)
        {
            return
                $"X: {pos.x.ToString(CultureInfo.InvariantCulture)} - Y: {pos.y.ToString(CultureInfo.InvariantCulture)} - Z:{pos.z.ToString(CultureInfo.InvariantCulture)}";
        }

        private static string CoordinateForTeleport(Vector3 pos)
        {
            return
                $" {pos.x.ToString(CultureInfo.InvariantCulture)},{pos.y.ToString(CultureInfo.InvariantCulture)},{pos.z.ToString(CultureInfo.InvariantCulture)}";
        }

        private void CmdHandler(IPlayer ipPlayer, string command, string[] arg)
        {
            var playerCmdCaller = ipPlayer?.Object as BasePlayer;
            if (playerCmdCaller == null) return;

            if (!permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionUse))
            {
                SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                return;
            }

            if (arg.Length == 0) return;

            var action = arg[0].ToLower();
            var isAdmin = permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionAdmin);

            var inputCodeLock = 0UL;
            var targetSteamID = 0UL;
            // var isSteamID = arg.Length > 1 && ulong.TryParse(arg[1], out targetSteamID);

            bool isSteamID;
            PlayerInfo? playerInfo;
            BasePlayer? targetBasePlayer;
            List<BaseLock> lockList;

            switch (action)
            {
                case "lockall":
                case "unlockall":

                    //TODO #################### unificare questi controlli ####################
                    if (!isAdmin)
                    {
                        SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!CanByPassCodeLock(playerCmdCaller))
                    {
                        SendMessage(playerCmdCaller, Lang("NoBypassPermissions", playerCmdCaller.UserIDString));
                        return;
                    }
                    //#########################################################################

                    isSteamID = arg.Length >= 2 && ulong.TryParse(arg[1], out targetSteamID);
                    if (!isSteamID || !targetSteamID.IsSteamId())
                    {
                        SendMessage(playerCmdCaller, Lang("InvalidSteamID", playerCmdCaller.UserIDString));
                        return;
                    }

                    lockList = BaseNetworkable.serverEntities
                        .Where(e => e is BaseLock)
                        .Cast<BaseLock>()
                        .Where(cl => cl.OwnerID.Equals(targetSteamID))
                        .ToList();

                    playerInfo = ConvertToPlayerInfo(playerCmdCaller);
                    _coroutines.Add(ServerMgr.Instance.StartCoroutine(LockSetLocked(playerInfo, lockList, action.Equals("lockall"))));

                    break;

                case "removeall":

                    //TODO #################### unificare questi controlli ####################
                    if (!isAdmin)
                    {
                        SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!CanByPassCodeLock(playerCmdCaller))
                    {
                        SendMessage(playerCmdCaller, Lang("NoBypassPermissions", playerCmdCaller.UserIDString));
                        return;
                    }
                    //#########################################################################

                    isSteamID = arg.Length >= 2 && ulong.TryParse(arg[1], out targetSteamID);
                    if (!isSteamID || !targetSteamID.IsSteamId())
                    {
                        SendMessage(playerCmdCaller, Lang("InvalidSteamID", playerCmdCaller.UserIDString));
                        return;
                    }

                    lockList = BaseNetworkable.serverEntities
                        .Where(e => e is BaseLock)
                        .Cast<BaseLock>()
                        .Where(cl => cl.OwnerID.Equals(targetSteamID))
                        .ToList();

                    playerInfo = ConvertToPlayerInfo(playerCmdCaller);
                    _coroutines.Add(ServerMgr.Instance.StartCoroutine(RemoveLock(playerInfo, lockList)));

                    break;

                case "lock":
                case "unlock":
                case "remove":

                    //TODO #################### unificare questi controlli ####################
                    if (!isAdmin)
                    {
                        SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!CanByPassCodeLock(playerCmdCaller))
                    {
                        SendMessage(playerCmdCaller, Lang("NoBypassPermissions", playerCmdCaller.UserIDString));
                        return;
                    }
                    //#########################################################################

                    var codeLockData = GetCodeLockDataByLookingAt(playerCmdCaller);
                    if (codeLockData == null || codeLockData.GetCodeLockEntity() == null ||
                        codeLockData.BaseEntity == null)
                    {
                        SendMessage(playerCmdCaller, Lang("CodeLockNotFound", playerCmdCaller.UserIDString));
                        return;
                    }

                    switch (action)
                    {
                        case "lock":
                        {
                            codeLockData.Lock();
                            PlayerShowToast(playerCmdCaller,
                                Lang("CodeLockMessage", playerCmdCaller.UserIDString, Lang("LockerStatus_Locked", playerCmdCaller.UserIDString)));
                            break;
                        }
                        case "unlock":
                        {
                            codeLockData.Unlock();
                            PlayerShowToast(playerCmdCaller,
                                Lang("CodeLockMessage", playerCmdCaller.UserIDString, Lang("LockerStatus_Unlocked", playerCmdCaller.UserIDString)));
                            break;
                        }
                        case "remove":
                        {
                            codeLockData.RemoveLock(playerCmdCaller);
                            PlayerShowToast(playerCmdCaller,
                                Lang("CodeLockMessage", playerCmdCaller.UserIDString, Lang("LockerStatus_Removed", playerCmdCaller.UserIDString)), true);
                            break;
                        }
                    }

                    break;

                case "code":
                case "codeall":
                case "guestcodeall":

                    var tryParseNewCode = arg.Length > 1 && ulong.TryParse(arg[1], out inputCodeLock);
                    //TODO #################### unificare questi controlli ####################
                    isSteamID = arg.Length > 2 && ulong.TryParse(arg[2], out targetSteamID);
                    if (isSteamID && !targetSteamID.IsSteamId())
                    {
                        SendMessage(playerCmdCaller, Lang("InvalidSteamID", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!IsValidLockCode(inputCodeLock.ToString()))
                    {
                        SendMessage(playerCmdCaller, Lang("LockCodeNotValid", playerCmdCaller.UserIDString));
                        return;
                    }

                    var newLockCode = PadWithZeros(inputCodeLock.ToString());

                    if (isAdmin && isSteamID)
                    {
                        targetBasePlayer = BasePlayer.FindAwakeOrSleepingByID(targetSteamID);
                        if (targetBasePlayer == null)
                        {
                            SendMessage(playerCmdCaller, Lang("PlayerNotFound", playerCmdCaller.UserIDString));
                            return;
                        }
                    }
                    else if ((isAdmin && !isSteamID) || !isAdmin)
                    {
                        targetBasePlayer = playerCmdCaller;
                    }
                    else
                    {
                        targetBasePlayer = null;
                    }

                    if (!isAdmin && arg.Length > 2 && arg[2] != null)
                    {
                        SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                        return;
                    }
                    //#########################################################################

                    switch (action)
                    {
                        case "code":
                        {
                            codeLockData = GetCodeLockDataByLookingAt(playerCmdCaller);
                            if (codeLockData == null || codeLockData.GetCodeLockEntity() == null)
                            {
                                SendMessage(playerCmdCaller, Lang("CodeLockNotFound", playerCmdCaller.UserIDString));
                                return;
                            }

                            targetBasePlayer = codeLockData.GetOwnerPlayer();
                            if (targetBasePlayer == null)
                            {
                                SendMessage(playerCmdCaller, Lang("PlayerNotFound", playerCmdCaller.UserIDString));
                                return;
                            }

                            if (!isAdmin && !IsEntityOwner(codeLockData.GetCodeLockEntity(), playerCmdCaller))
                            {
                                SendMessage(playerCmdCaller, Lang("NoEntityOwner", playerCmdCaller.UserIDString));
                                return;
                            }


                            //If the admin is not the owner of the entity, send the message, to change the pin code, only to the admin
                            var sendMessage = true;
                            if (isAdmin && !playerCmdCaller.userID.Get().Equals(targetBasePlayer.userID))
                            {
                                if (!CanByPassCodeLock(playerCmdCaller))
                                {
                                    SendMessage(playerCmdCaller, Lang("NoBypassPermissions", playerCmdCaller.UserIDString));
                                    return;
                                }

                                sendMessage = false;
                            }


                            switch (codeLockData.GetCodeLockEntity())
                            {
                                case ModularCar _modularCar:
                                    if (!_modularCar.CarLock.HasALock) return;
                                    UpdateCodeLockAuthShareOrSettings(null, _modularCar, targetBasePlayer, newLockCode, sendMessage: sendMessage);
                                    break;
                                case CodeLock _codeLock:
                                    if (!_codeLock.OwnerID.IsSteamId()) return;
                                    UpdateCodeLockAuthShareOrSettings(_codeLock, null, targetBasePlayer, newLockCode, sendMessage: sendMessage);
                                    break;
                            }

                            //If the admin is not the owner of the entity, send the message, to change the pin code, only to the admin
                            if (isAdmin && !sendMessage)
                                SendMessage(playerCmdCaller, Lang("CodeLockPinChangedMessage", playerCmdCaller.UserIDString));

                            break;
                        }
                        case "codeall":
                        case "guestcodeall":
                        {
                            if (isAdmin)
                            {
                                if (isSteamID && !CanByPassCodeLock(playerCmdCaller))
                                {
                                    SendMessage(playerCmdCaller, Lang("NoBypassPermissions", playerCmdCaller.UserIDString));
                                    return;
                                }
                            }

                            if (targetBasePlayer == null)
                            {
                                SendMessage(playerCmdCaller, Lang("PlayerNotFound", playerCmdCaller.UserIDString));
                                return;
                            }

                            var lockEntityList = BaseNetworkable.serverEntities
                                .Where(se =>
                                    se is CodeLock cl &&
                                    cl.OwnerID.IsSteamId() &&
                                    cl.OwnerID.Equals(targetBasePlayer.userID))
                                .Cast<BaseEntity>()
                                .ToList();

                            lockEntityList.AddRange(BaseNetworkable.serverEntities
                                .Where(se =>
                                    se is ModularCar be && (
                                        be.ShortPrefabName.Contains(ModuleCarPartialShortPrefabName) ||
                                        be.PrefabName.StartsWith(CarChassisModuleCarPartialPrefabName, StringComparison.OrdinalIgnoreCase)
                                    ) &&
                                    be.OwnerID.IsSteamId() &&
                                    be.OwnerID.Equals(targetBasePlayer.userID))
                                .Cast<BaseEntity>()
                                .ToList());

                            var isGuestCode = action.Equals("guestcodeall");

                            _coroutines.Add(
                                ServerMgr.Instance.StartCoroutine(UpdateAllCodeLockAuthShareOrSettings(lockEntityList, targetBasePlayer,
                                    newLockCode, isGuestCode: isGuestCode, sendMessage: true)));

                            break;
                        }
                    }

                    break;

                case "show":

                    //TODO #################### unificare questi controlli ####################
                    if (!isAdmin)
                    {
                        SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!CanByPassCodeLock(playerCmdCaller))
                    {
                        SendMessage(playerCmdCaller, Lang("NoBypassPermissions", playerCmdCaller.UserIDString));
                        return;
                    }
                    //#########################################################################

                    codeLockData = GetCodeLockDataByLookingAt(playerCmdCaller);
                    if (codeLockData == null || codeLockData.GetCodeLockEntity() == null)
                    {
                        SendMessage(playerCmdCaller, Lang("CodeLockNotFound", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (codeLockData.BaseEntity == null)
                    {
                        SendMessage(playerCmdCaller, Lang("EntityNotAllowed", playerCmdCaller.UserIDString));
                        return;
                    }

                    var codeLockCode = codeLockData.GetCodeLockCode();
                    if (string.IsNullOrWhiteSpace(codeLockCode))
                    {
                        SendMessage(playerCmdCaller, Lang("CodeLockCodeNotFound", playerCmdCaller.UserIDString));
                        return;
                    }

                    var autoLockPlayerSettings = GetAutoLockPlayerSettingsData(playerCmdCaller.userID);
                    if (autoLockPlayerSettings.StreamerModeEnabled)
                    {
                        SendMessage(playerCmdCaller, Lang("ShowCodeLockPinStreamerModeMessage", playerCmdCaller.UserIDString));
                        return;
                    }

                    var guestCodeLock = codeLockData.GetCodeLockGuestCode();

                    SendMessage(playerCmdCaller,
                        string.IsNullOrWhiteSpace(guestCodeLock)
                            ? Lang("ShowCodeLockCodeMessage", playerCmdCaller.UserIDString, codeLockCode)
                            : Lang("ShowCodeLockAndGuestCodeMessage", playerCmdCaller.UserIDString, codeLockCode, guestCodeLock));

                    break;

                case "ctime":
                    var delayTimeString = string.Join(" ", arg.Skip(1));

                    if (!permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionUse))
                    {
                        SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!IsValidDelayTime(delayTimeString))
                    {
                        SendMessage(playerCmdCaller, Lang("DelayTimeNotValid", playerCmdCaller.UserIDString,
                            _config.AutoClosingConfiguration.MinimumClosingDelayTime, _config.AutoClosingConfiguration.MaximumClosingDelayTime));
                        return;
                    }

                    if (!IsAutoClosingEnabled(playerCmdCaller))
                    {
                        SendMessage(playerCmdCaller, Lang("AutoClosingNotEnabled", playerCmdCaller.UserIDString));
                        return;
                    }

                    var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(playerCmdCaller);
                    if (!autoClosingPlayerSettings.AutoClosingEnabled)
                    {
                        SendMessage(playerCmdCaller, Lang("PlayerAutoClosingNotEnabled", playerCmdCaller.UserIDString));
                        return;
                    }

                    var entityLookedAt = GetEntityByRayCast(playerCmdCaller);
                    if (entityLookedAt == null)
                    {
                        SendMessage(playerCmdCaller, Lang("EntityNotFound", playerCmdCaller.UserIDString));
                        return;
                    }

                    var doorCloser = GetDoorCloserByEntity(entityLookedAt);
                    if (doorCloser == null)
                    {
                        SendMessage(playerCmdCaller, Lang("DoorCloserNotFound", playerCmdCaller.UserIDString));
                        return;
                    }

                    if (!isAdmin && !IsEntityOwner(doorCloser, playerCmdCaller))
                    {
                        SendMessage(playerCmdCaller, Lang("NoEntityOwner", playerCmdCaller.UserIDString));
                        return;
                    }

                    var autoClosingType = AutoClosingTypeByEntity(entityLookedAt);
                    if (autoClosingType == AutoClosingType.None)
                    {
                        SendMessage(playerCmdCaller, Lang("AutoClosingTypeNotValid", playerCmdCaller.UserIDString));
                        return;
                    }

                    var delayTime = GetValidDelayTime(delayTimeString);

                    if (autoClosingType == AutoClosingType.Garage)
                        doorCloser.delay = delayTime + GarageOpenDuration;
                    else
                        doorCloser.delay = delayTime;

                    doorCloser.SendNetworkUpdate();

                    DoorCloserCustomDelayTimeChanged(playerCmdCaller, doorCloser);

                    PlayerShowToast(playerCmdCaller, Lang("SingleDoorCloserCustomTime", playerCmdCaller.UserIDString, delayTime));

                    break;
            }
        }

        private void AutoLockCmdHandler(IPlayer ipPlayer, string command, string[] arg)
        {
            var player = ipPlayer?.Object as BasePlayer;
            if (player == null) return;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) ||
                !permission.UserHasPermission(player.UserIDString, PermissionAutoLockEnabled))
            {
                SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            if (arg.Length == 0)
            {
                OpenAutoLockSettingsUI(player);
                return;
            }
        }

        private void LockerCloserCmdHandler(IPlayer ipPlayer, string command, string[] arg)
        {
            var playerCmdCaller = ipPlayer?.Object as BasePlayer;
            if (playerCmdCaller == null) return;

            var isAdmin = permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionAdmin);

            if (!permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionUse))
            {
                SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                return;
            }

            if (command.Equals(_config.AutoLockConfiguration.AddLockerChatCommand, StringComparison.OrdinalIgnoreCase))
            {
                if (!permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionAutoLockEnabled))
                {
                    SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                    return;
                }

                var entityLookedAt = GetEntityByRayCast(playerCmdCaller);
                if (entityLookedAt == null)
                {
                    SendMessage(playerCmdCaller, Lang("EntityNotFound", playerCmdCaller.UserIDString));
                    return;
                }

                DeployLocker(playerCmdCaller, entityLookedAt, true);
                return;
            }

            if (command.Equals(_config.AutoClosingConfiguration.AddCloserChatCommand, StringComparison.OrdinalIgnoreCase))
            {
                if (!permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionAutoClosingEnabled))
                {
                    SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                    return;
                }

                var entityLookedAt = GetEntityByRayCast(playerCmdCaller);
                if (entityLookedAt == null)
                {
                    SendMessage(playerCmdCaller, Lang("EntityNotFound", playerCmdCaller.UserIDString));
                    return;
                }

                DeployDoorCloser(playerCmdCaller, entityLookedAt, true);
                return;
            }

            if (command.Equals(_config.AutoClosingConfiguration.RemoveCloserChatCommand, StringComparison.OrdinalIgnoreCase))
            {
                if (!permission.UserHasPermission(playerCmdCaller.UserIDString, PermissionAutoClosingEnabled))
                {
                    SendMessage(playerCmdCaller, Lang("NoPermissions", playerCmdCaller.UserIDString));
                    return;
                }

                var entityLookedAt = GetEntityByRayCast(playerCmdCaller);
                if (entityLookedAt == null)
                {
                    SendMessage(playerCmdCaller, Lang("EntityNotFound", playerCmdCaller.UserIDString));
                    return;
                }

                var doorCloser = GetDoorCloserByEntity(entityLookedAt);
                if (doorCloser == null)
                {
                    SendMessage(playerCmdCaller, Lang("DoorCloserNotFound", playerCmdCaller.UserIDString));
                    return;
                }

                if (!isAdmin && !IsEntityOwner(doorCloser, playerCmdCaller))
                {
                    SendMessage(playerCmdCaller, Lang("NoEntityOwner", playerCmdCaller.UserIDString));
                    return;
                }

                DoorCloserRemoved(playerCmdCaller, doorCloser);
                doorCloser.Kill();
                SendMessage(playerCmdCaller, Lang("DoorCloserListMessage", playerCmdCaller.UserIDString,
                    Lang("LockerStatus_Removed", playerCmdCaller.UserIDString), "1"));

                return;
            }
        }

        [ConsoleCommand("ultimatelocker.autolock.cmd")]
        private void UIAutoLockCommandHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var action = arg.Args?.Length > 0 ? arg.Args[0] : null;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) ||
                !permission.UserHasPermission(player.UserIDString, PermissionAutoLockEnabled))
            {
                SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            bool? toggleStatus;
            List<BaseEntity> lockEntityList;

            var autoLockPlayerSettings = GetAutoLockPlayerSettingsData(player.userID);

            switch (action)
            {
                case "toggleShareLocksWithClan":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.ShareLocksWithClanTeamEnabled == toggleStatus) return;

                    autoLockPlayerSettings.ShareLocksWithClanTeamEnabled = arg.Args[1].ToBool();

                    if (!_config.AutoLockConfiguration.UseOnlyKeyLock)
                        player.SendConsoleCommand("ultimatelocker.autolock.cmd updateAllCodeLockAuth");

                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleAutoLockOnPlacement":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.AutoLockOnPlacementEnabled == toggleStatus) return;

                    autoLockPlayerSettings.AutoLockOnPlacementEnabled = arg.Args[1].ToBool();

                    if (!_config.AutoLockConfiguration.UseOnlyKeyLock && !autoLockPlayerSettings.AutoLockOnPlacementEnabled)
                        player.SendConsoleCommand("ultimatelocker.autolock.cmd removeAllGuestCodeLockPin");

                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleGuestCodeEnable":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.GuestCodeEnabled == toggleStatus) return;

                    autoLockPlayerSettings.GuestCodeEnabled = arg.Args[1].ToBool();

                    if (!autoLockPlayerSettings.GuestCodeEnabled)
                        player.SendConsoleCommand("ultimatelocker.autolock.cmd removeAllGuestCodeLockPin");

                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleAlsoUseKeyLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.AlsoUseKeyLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.AlsoUseKeyLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleStreamerMode":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.StreamerModeEnabled == toggleStatus) return;

                    autoLockPlayerSettings.StreamerModeEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleDoorsLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.DoorsLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.DoorsLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleWindowsLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.WindowsLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.WindowsLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleBoxesLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.BoxesLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.BoxesLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleStorageContainerLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.StorageContainerLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.StorageContainerLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleLockersLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.LockersLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.LockersLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleCupboardsLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.CupboardsLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.CupboardsLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleVehicleLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.VehicleLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.VehicleLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleVehicleLockAutoClosingOnDismount":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.VehicleLockAutoClosingOnDismountEnabled == toggleStatus) return;

                    autoLockPlayerSettings.VehicleLockAutoClosingOnDismountEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleMedievalEntityLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.MedievalEntityLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.MedievalEntityLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleFurnaceLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.FurnaceLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.FurnaceLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleVendingMachineLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.VendingMachineLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.VendingMachineLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleComposterLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.ComposterLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.ComposterLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleMixingTableLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.MixingTableLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.MixingTableLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "togglePlanterLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.PlanterLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.PlanterLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleAutoTurretLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.AutoTurretLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.AutoTurretLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleSamSiteLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.SamSiteLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.SamSiteLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleTrapsLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.TrapsLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.TrapsLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleWeaponRackLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.WeaponRackLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.WeaponRackLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleStashLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.StashLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.StashLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleNeonSignLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.NeonSignLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.NeonSignLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleOtherLockableEntitiesLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.OtherLockableEntitiesLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.OtherLockableEntitiesLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleOtherCustomEntitiesLock":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.OtherCustomEntitiesLockEnabled == toggleStatus) return;

                    autoLockPlayerSettings.OtherCustomEntitiesLockEnabled = arg.Args[1].ToBool();
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "saveNewCodeLockPin":
                    if (arg.Args == null || arg.Args.Length < 2) return;

                    var newCodeLockPin = string.Join(" ", arg.Args.Skip(1));
                    newCodeLockPin = PadWithZeros(newCodeLockPin);

                    if (string.IsNullOrWhiteSpace(newCodeLockPin) || !IsValidLockCode(newCodeLockPin))
                    {
                        SendMessage(player, Lang("LockCodeNotValid", player.UserIDString));
                        return;
                    }

                    if (autoLockPlayerSettings.CodeLock.Equals(newCodeLockPin)) return;

                    autoLockPlayerSettings.CodeLock = newCodeLockPin;
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "saveNewGuestCodeLockPin":
                    if (arg.Args == null || arg.Args.Length < 2) return;

                    var newGuestCodeLockPin = string.Join(" ", arg.Args.Skip(1));
                    newGuestCodeLockPin = PadWithZeros(newGuestCodeLockPin);

                    if (string.IsNullOrWhiteSpace(newGuestCodeLockPin) || !IsValidLockCode(newGuestCodeLockPin))
                    {
                        SendMessage(player, Lang("LockCodeNotValid", player.UserIDString));
                        return;
                    }

                    if (autoLockPlayerSettings.GuestCodeLock.Equals(newGuestCodeLockPin)) return;

                    autoLockPlayerSettings.GuestCodeLock = newGuestCodeLockPin;
                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "updateAllCodeLockPin":
                    if (!autoLockPlayerSettings.AutoLockOnPlacementEnabled) return;

                    if (!IsValidLockCode(autoLockPlayerSettings.CodeLock)) return;

                    CmdHandler(player.IPlayer, "", new[] { "codeall", autoLockPlayerSettings.CodeLock });
                    break;

                case "updateAllGuestCodeLockPin":
                    if (!autoLockPlayerSettings.AutoLockOnPlacementEnabled ||
                        !autoLockPlayerSettings.GuestCodeEnabled) return;

                    if (!IsValidLockCode(autoLockPlayerSettings.GuestCodeLock)) return;

                    CmdHandler(player.IPlayer, "", new[] { "guestcodeall", autoLockPlayerSettings.GuestCodeLock });
                    break;

                case "updateAllCodeLockAuth":
                    lockEntityList = BaseNetworkable.serverEntities
                        .Where(se =>
                            se is CodeLock cl &&
                            cl.OwnerID.IsSteamId() &&
                            cl.OwnerID.Equals(player.userID))
                        .Cast<BaseEntity>()
                        .ToList();

                    lockEntityList.AddRange(BaseNetworkable.serverEntities
                        .Where(se =>
                            se is ModularCar be && (
                                be.ShortPrefabName.Contains(ModuleCarPartialShortPrefabName) ||
                                be.PrefabName.StartsWith(CarChassisModuleCarPartialPrefabName, StringComparison.OrdinalIgnoreCase)
                            ) &&
                            be.OwnerID.IsSteamId() &&
                            be.OwnerID.Equals(player.userID))
                        .Cast<BaseEntity>()
                        .ToList());

                    _coroutines.Add(
                        ServerMgr.Instance.StartCoroutine(UpdateAllCodeLockAuthShareOrSettings(lockEntityList, player, sendSharedAuthMessage: true)));

                    break;

                case "removeAllGuestCodeLockPin":
                    lockEntityList = BaseNetworkable.serverEntities
                        .Where(se =>
                            se is CodeLock cl &&
                            cl.OwnerID.IsSteamId() &&
                            cl.OwnerID.Equals(player.userID))
                        .Cast<BaseEntity>()
                        .ToList();

                    _coroutines.Add(
                        ServerMgr.Instance.StartCoroutine(
                            UpdateAllCodeLockAuthShareOrSettings(lockEntityList, player, removeGuestCode: true, sendMessage: true)));

                    break;
            }
        }

        [ConsoleCommand("ultimatelocker.autoclosing.cmd")]
        private void UIAutoClosingCommandHandler(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            var action = arg.Args?.Length > 0 ? arg.Args[0] : null;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) || !IsAutoClosingEnabled(player))
            {
                SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                OpenAutoClosingSettingsUI(player);
                return;
            }

            bool? toggleStatus;
            string? autoClosingTypeString;
            AutoClosingType autoClosingType;

            var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(player);

            switch (action)
            {
                case "toggleAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.AutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.AutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleDoorAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.DoorAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.DoorAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleDoubleDoorAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.DoubleDoorAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.DoubleDoorAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleWindowAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.WindowAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.WindowAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleGarageAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.GarageAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.GarageAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleLadderHatchAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.LadderHatchAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.LadderHatchAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleExternalGateAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.ExternalGateAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.ExternalGateAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleFenceGateAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.FenceGateAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.FenceGateAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "toggleLegacyWoodShelterDoorAutoClosingEnabled":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoClosingPlayerSettings.LegacyWoodShelterDoorAutoClosingEnabled == toggleStatus) return;

                    autoClosingPlayerSettings.LegacyWoodShelterDoorAutoClosingEnabled = arg.Args[1].ToBool();

                    SaveData();
                    OpenAutoClosingSettingsUI(player);
                    break;

                case "saveNewDelayTime":

                    if (arg.Args is not { Length: > 2 } || string.IsNullOrWhiteSpace(arg.Args[1]) || string.IsNullOrWhiteSpace(arg.Args[2])) return;

                    autoClosingTypeString = arg.Args[1];
                    var delayTimeString = string.Join(" ", arg.Args.Skip(2));

                    if (string.IsNullOrWhiteSpace(autoClosingTypeString) || string.IsNullOrWhiteSpace(delayTimeString))
                    {
                        SendMessage(player, Lang("MissingParametersError", player.UserIDString, "autoClosingTypeString or delayTimeString"));
                        return;
                    }

                    if (!IsValidDelayTime(delayTimeString))
                    {
                        SendMessage(player, Lang("DelayTimeNotValid", player.UserIDString, _config.AutoClosingConfiguration.MinimumClosingDelayTime,
                            _config.AutoClosingConfiguration.MaximumClosingDelayTime));
                        return;
                    }

                    if (!Enum.IsDefined(typeof(AutoClosingType), autoClosingTypeString))
                    {
                        SendMessage(player, Lang("GenericError", player.UserIDString, "autoClosingTypeString"));
                        return;
                    }

                    if (!Enum.TryParse(autoClosingTypeString, out autoClosingType))
                    {
                        SendMessage(player, Lang("GenericError", player.UserIDString, "autoClosingTypeString conversion"));
                        return;
                    }

                    var delayTime = GetValidDelayTime(delayTimeString);
                    switch (autoClosingType)
                    {
                        case AutoClosingType.Door:
                            autoClosingPlayerSettings.DoorClosingDelay = delayTime;
                            break;
                        case AutoClosingType.DoubleDoor:
                            autoClosingPlayerSettings.DoubleDoorClosingDelay = delayTime;
                            break;
                        case AutoClosingType.Window:
                            autoClosingPlayerSettings.WindowClosingDelay = delayTime;
                            break;
                        case AutoClosingType.Garage:
                            autoClosingPlayerSettings.GarageClosingDelay = delayTime;
                            break;
                        case AutoClosingType.LadderHatch:
                            autoClosingPlayerSettings.LadderHatchClosingDelay = delayTime;
                            break;
                        case AutoClosingType.ExternalGate:
                            autoClosingPlayerSettings.ExternalGateClosingDelay = delayTime;
                            break;
                        case AutoClosingType.FenceGate:
                            autoClosingPlayerSettings.FenceGateClosingDelay = delayTime;
                            break;
                        case AutoClosingType.LegacyWoodShelterDoor:
                            autoClosingPlayerSettings.LegacyWoodShelterDoorClosingDelay = delayTime;
                            break;
                        default:
                            SendMessage(player, Lang("GenericError", player.UserIDString, "autoClosingType case"));
                            break;
                    }

                    SaveData();
                    break;

                case "removeAllDoorCloser":
                case "updateAllDoorCloserDelayTime":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;

                    autoClosingTypeString = arg.Args[1];

                    if (string.IsNullOrWhiteSpace(autoClosingTypeString))
                    {
                        SendMessage(player, Lang("MissingParametersError", player.UserIDString, "autoClosingTypeString"));
                        return;
                    }

                    if (!Enum.IsDefined(typeof(AutoClosingType), autoClosingTypeString))
                    {
                        SendMessage(player, Lang("GenericError", player.UserIDString, "autoClosingTypeString"));
                        return;
                    }

                    if (!Enum.TryParse(autoClosingTypeString, out autoClosingType))
                    {
                        SendMessage(player, Lang("GenericError", player.UserIDString, "autoClosingTypeString conversion"));
                        return;
                    }


                    var doorCloserList = new List<DoorCloser>();
                    var enumerableEntities = BaseNetworkable.serverEntities
                        .Where(se =>
                            se is DoorCloser dc && dc.GetParentEntity() != null && AutoClosingTypeByEntity(dc.GetParentEntity()) != AutoClosingType.None &&
                            dc.OwnerID.IsSteamId() && dc.OwnerID.Equals(player.userID))
                        .Cast<DoorCloser>();

                    switch (autoClosingType)
                    {
                        case AutoClosingType.Door:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.Door].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.DoubleDoor:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.DoubleDoor].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.Window:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.Window].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.Garage:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.Garage].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.LadderHatch:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.LadderHatch].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.ExternalGate:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.ExternalGate].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.FenceGate:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => _autoClosingEntities[AutoClosingType.FenceGate].Contains(dc.GetParentEntity().PrefabName)).ToList());
                            break;
                        case AutoClosingType.LegacyWoodShelterDoor:
                            doorCloserList.AddRange(enumerableEntities
                                .Where(dc => dc.GetParentEntity().PrefabName.Equals(LegacyShelterWoodDoorPrefabName)).ToList());
                            break;
                        default:
                            SendMessage(player, Lang("GenericError", player.UserIDString, "autoClosingType case 2"));
                            break;
                    }

                    if (doorCloserList.IsNullOrEmpty()) return;

                    switch (action)
                    {
                        case "removeAllDoorCloser":
                            _coroutines.Add(ServerMgr.Instance.StartCoroutine(RemoveDoorCloser(player, doorCloserList)));
                            break;

                        case "updateAllDoorCloserDelayTime":
                            _coroutines.Add(ServerMgr.Instance.StartCoroutine(UpdateDoorCloserDelayTime(player, autoClosingType, autoClosingPlayerSettings,
                                doorCloserList)));
                            break;
                    }

                    break;
            }
        }

        private void OpenAutoLockSettingsUI(BasePlayer player)
        {
            var mainContainer = UIHelper.MainContainer(UIAutoLock, "0.35 0.20", "0.65 0.80", requireCursor: true);
            UIHelper.Header(mainContainer, UIAutoLock, UIAutoLock + ".Header", Lang("UiHeaderTitle", player.UserIDString), closeUI: UIAutoLock);

            var contentPanel = UIHelper.ParentPanel(mainContainer, UIAutoLock, UIAutoLock + ".Container",
                "0 0", "0.997 0.938");

            var autoLockPlayerSettings = GetAutoLockPlayerSettingsData(player.userID);

            const float rowHeight = 0.0620f;
            const float rowHeightMargin = 0.0166f;

            var rowOffsetY = 0.9245;

            if (_config.AutoLockConfiguration.ForceShareLocksWithClanTeam == null)
            {
                //#################### ShareLocksWithClanTeam ####################
                var rowShareLocksWithClan = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.ShareLocksWithClan",
                    $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
                UIHelper.Text(mainContainer, rowShareLocksWithClan, Lang("UiShareLocksWithClanTeamEnabled", player.UserIDString), "0.02 0.2", "0.78 0.7");
                UIHelper.ToggleButton(mainContainer, rowShareLocksWithClan, autoLockPlayerSettings.ShareLocksWithClanTeamEnabled,
                    "ultimatelocker.autolock.cmd toggleShareLocksWithClan", "0.8 0.1", "0.99 0.85",
                    Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));

                rowOffsetY -= rowHeight + rowHeightMargin;
                //####################################################################################################
            }

            //#################### AutoLockOnPlacement ####################
            var rowAutoLockOnPlacement = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.AutoLockOnPlacement",
                $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
            UIHelper.Text(mainContainer, rowAutoLockOnPlacement, Lang("UiAutoLockOnPlacementEnabled", player.UserIDString), "0.02 0.2", "0.78 0.7");
            UIHelper.ToggleButton(mainContainer, rowAutoLockOnPlacement, autoLockPlayerSettings.AutoLockOnPlacementEnabled,
                "ultimatelocker.autolock.cmd toggleAutoLockOnPlacement", "0.8 0.1", "0.99 0.85",
                Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
            //####################################################################################################

            if (!_config.AutoLockConfiguration.UseOnlyKeyLock && autoLockPlayerSettings.AutoLockOnPlacementEnabled)
            {
                //#################### CodeLock Pin ####################
                rowOffsetY -= rowHeight + rowHeightMargin;
                var rowCodeLockPin = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.CodeLockPin",
                    $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
                UIHelper.Text(mainContainer, rowCodeLockPin, Lang("UiCodeLockPin", player.UserIDString), "0.02 0.2", "0.78 0.7");
                UIHelper.Input(mainContainer, rowCodeLockPin, "ultimatelocker.autolock.cmd saveNewCodeLockPin",
                    autoLockPlayerSettings.CodeLock, "0.46 0.1", "0.68 0.85", textAlign: TextAnchor.MiddleCenter, maxLength: 4,
                    isPassword: autoLockPlayerSettings.StreamerModeEnabled, autofocus: autoLockPlayerSettings.StreamerModeEnabled);
                UIHelper.UpdateButton(mainContainer, rowCodeLockPin, "ultimatelocker.autolock.cmd updateAllCodeLockPin",
                    Lang("UiUpdateAll", player.UserIDString), "0.72 0.1", "0.99 0.85", fadeIn: 0.5f);
                //####################################################################################################

                //#################### GuestCodeEnable ####################
                rowOffsetY -= rowHeight + rowHeightMargin;
                var rowGuestCodeEnable = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.GuestCodeEnable",
                    $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
                UIHelper.Text(mainContainer, rowGuestCodeEnable, Lang("UiGuestCodeEnabled", player.UserIDString), "0.02 0.2", "0.78 0.7");
                UIHelper.ToggleButton(mainContainer, rowGuestCodeEnable, autoLockPlayerSettings.GuestCodeEnabled,
                    "ultimatelocker.autolock.cmd toggleGuestCodeEnable", "0.8 0.1", "0.99 0.85",
                    Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
                //####################################################################################################

                if (autoLockPlayerSettings.GuestCodeEnabled)
                {
                    //#################### GuestCodeLock Pin ####################
                    rowOffsetY -= rowHeight + rowHeightMargin;
                    var rowGuestCodeLockPin = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.GuestCodeLockPin",
                        $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
                    UIHelper.Text(mainContainer, rowGuestCodeLockPin, Lang("UiGuestCodeLockPin", player.UserIDString), "0.02 0.2", "0.78 0.7");
                    UIHelper.Input(mainContainer, rowGuestCodeLockPin, "ultimatelocker.autolock.cmd saveNewGuestCodeLockPin",
                        autoLockPlayerSettings.GuestCodeLock, "0.46 0.1", "0.68 0.85", textAlign: TextAnchor.MiddleCenter, maxLength: 4,
                        isPassword: autoLockPlayerSettings.StreamerModeEnabled, autofocus: autoLockPlayerSettings.StreamerModeEnabled);
                    UIHelper.UpdateButton(mainContainer, rowGuestCodeLockPin, "ultimatelocker.autolock.cmd updateAllGuestCodeLockPin",
                        Lang("UiUpdateAll", player.UserIDString), "0.72 0.1", "0.99 0.85", fadeIn: 0.5f);
                    //####################################################################################################
                }

                //#################### AlsoUseKeyLock ####################
                rowOffsetY -= rowHeight + rowHeightMargin;
                var rowAlsoUseKeyLock = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.AlsoUseKeyLock",
                    $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
                UIHelper.Text(mainContainer, rowAlsoUseKeyLock, Lang("UiAlsoUseKeyLockEnabled", player.UserIDString), "0.02 0.2", "0.78 0.7");
                UIHelper.ToggleButton(mainContainer, rowAlsoUseKeyLock, autoLockPlayerSettings.AlsoUseKeyLockEnabled,
                    "ultimatelocker.autolock.cmd toggleAlsoUseKeyLock", "0.8 0.1", "0.99 0.85",
                    Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
                //####################################################################################################
            }

            var allowedLockCategory = PlayerGetAllowedLockCategory(player);

            //#################### Other Settings ####################
            const float componentLeftMargin = 10f;
            const float componentRightMargin = -20f;
            const float componentHeight = 26f;
            const float componentHeightMargin = 6f;

            var offsetY = componentHeightMargin * -1; //Add top margin
            var rowCuiPanels = new List<UIHelper.RowCuiPanel>();

            const string basicCmd = "ultimatelocker.autolock.cmd";

            //#################### StreamerMode ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.StreamerMode", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiStreamerModeEnabled", player.UserIDString), $"{basicCmd} toggleStreamerMode",
                autoLockPlayerSettings.StreamerModeEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################

            //#################### DoorsLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.DoorsLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiDoorsLockEnabled", player.UserIDString), $"{basicCmd} toggleDoorsLock",
                autoLockPlayerSettings.DoorsLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################

            //#################### WindowsLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.WindowsLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiWindowsLockEnabled", player.UserIDString), $"{basicCmd} toggleWindowsLock",
                autoLockPlayerSettings.WindowsLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################

            // if (allowedLockCategory.Contains(LockCategory.Box))
            // {
            //#################### BoxesLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.BoxesLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiBoxesLockEnabled", player.UserIDString), $"{basicCmd} toggleBoxesLock",
                autoLockPlayerSettings.BoxesLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################
            // }

            // if (allowedLockCategory.Contains(LockCategory.StorageContainer))
            // {
            //#################### StorageContainerLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.StorageContainerLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiStorageContainerLockEnabled", player.UserIDString), $"{basicCmd} toggleStorageContainerLock",
                autoLockPlayerSettings.StorageContainerLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################
            // }

            //#################### LockersLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.LockersLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiLockersLockEnabled", player.UserIDString), $"{basicCmd} toggleLockersLock",
                autoLockPlayerSettings.LockersLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################

            //#################### CupboardsLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.CupboardsLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiCupboardsLockEnabled", player.UserIDString), $"{basicCmd} toggleCupboardsLock",
                autoLockPlayerSettings.CupboardsLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################

            if (allowedLockCategory.Contains(LockCategory.Vehicle))
            {
                //#################### VehicleLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.VehicleLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiVehicleLockEnabled", player.UserIDString), $"{basicCmd} toggleVehicleLock",
                    autoLockPlayerSettings.VehicleLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################

                //#################### VehicleLockAutoClosingOnDismount ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.VehicleLockAutoClosingOnDismount", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiVehicleLockAutoClosingOnDismountEnabled", player.UserIDString), $"{basicCmd} toggleVehicleLockAutoClosingOnDismount",
                    autoLockPlayerSettings.VehicleLockAutoClosingOnDismountEnabled, player, textFontSize: 9));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.Medieval))
            {
                //#################### MedievalEntity ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.MedievalEntity", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiMedievalEntityLockEnabled", player.UserIDString), $"{basicCmd} toggleMedievalEntityLock",
                    autoLockPlayerSettings.MedievalEntityLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.Furnace))
            {
                //#################### FurnaceLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.FurnaceLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiFurnaceLockEnabled", player.UserIDString), $"{basicCmd} toggleFurnaceLock",
                    autoLockPlayerSettings.FurnaceLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.VendingMachine))
            {
                //#################### VendingMachineLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.VendingMachineLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiVendingMachineLockEnabled", player.UserIDString), $"{basicCmd} toggleVendingMachineLock",
                    autoLockPlayerSettings.VendingMachineLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.Composter))
            {
                //#################### ComposterLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.ComposterLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiComposterLockEnabled", player.UserIDString), $"{basicCmd} toggleComposterLock",
                    autoLockPlayerSettings.ComposterLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.MixingTable))
            {
                //#################### MixingTableLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.MixingTableLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiMixingTableLockEnabled", player.UserIDString), $"{basicCmd} toggleMixingTableLock",
                    autoLockPlayerSettings.MixingTableLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.Planter))
            {
                //#################### PlanterLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.PlanterLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiPlanterLockEnabled", player.UserIDString), $"{basicCmd} togglePlanterLock",
                    autoLockPlayerSettings.PlanterLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.AutoTurret))
            {
                //#################### AutoTurretLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.AutoTurretLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiAutoTurretLockEnabled", player.UserIDString), $"{basicCmd} toggleAutoTurretLock",
                    autoLockPlayerSettings.AutoTurretLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.SamSite))
            {
                //#################### SamSiteLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.SamSiteLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiSamSiteLockEnabled", player.UserIDString), $"{basicCmd} toggleSamSiteLock",
                    autoLockPlayerSettings.SamSiteLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.Trap))
            {
                //#################### TrapsLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.TrapsLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiTrapsLockEnabled", player.UserIDString), $"{basicCmd} toggleTrapsLock",
                    autoLockPlayerSettings.TrapsLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.WeaponRack))
            {
                //#################### WeaponRackLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.WeaponRackLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiWeaponRackLockEnabled", player.UserIDString), $"{basicCmd} toggleWeaponRackLock",
                    autoLockPlayerSettings.WeaponRackLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.Stash))
            {
                //#################### StashLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.StashLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiStashLockEnabled", player.UserIDString), $"{basicCmd} toggleStashLock",
                    autoLockPlayerSettings.StashLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.NeonSign))
            {
                //#################### NeonSignLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.NeonSignLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiNeonSignLockEnabled", player.UserIDString), $"{basicCmd} toggleNeonSignLock",
                    autoLockPlayerSettings.NeonSignLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            //#################### OtherLockableEntitiesLock ####################
            rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.OtherLockableEntitiesLock", offsetY, componentLeftMargin, componentHeight,
                componentRightMargin, Lang("UiOtherLockableEntitiesLockEnabled", player.UserIDString), $"{basicCmd} toggleOtherLockableEntitiesLock",
                autoLockPlayerSettings.OtherLockableEntitiesLockEnabled, player));
            offsetY = offsetY - componentHeight - componentHeightMargin;
            //############################################################

            if (allowedLockCategory.Contains(LockCategory.OtherCustomEntities))
            {
                //#################### OtherCustomEntitiesLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.OtherCustomEntitiesLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiOtherCustomEntitiesLockEnabled", player.UserIDString), $"{basicCmd} toggleOtherCustomEntitiesLock",
                    autoLockPlayerSettings.OtherCustomEntitiesLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            var isAutoClosingEnabled = IsAutoClosingEnabled(player);
            var scrollAnchorMinY = "0.014";
            if (isAutoClosingEnabled)
                scrollAnchorMinY = "0.07";

            var scrollViewHeight = rowCuiPanels.Count * componentHeight + rowCuiPanels.Count * componentHeightMargin;
            scrollViewHeight += componentHeightMargin; //Add bottom margin
            var scrollView = UIHelper.ScrollView(mainContainer, contentPanel, UIAutoLock + ".ScrollView", scrollViewHeight,
                $"0 {scrollAnchorMinY}", $"0.997 {rowOffsetY - .01}");

            foreach (var rowCuiPanel in rowCuiPanels)
            {
                mainContainer.Add(rowCuiPanel.CuiPanel, scrollView, rowCuiPanel.PanelName);
                rowCuiPanel.Render?.Invoke();
            }

            //####################################################################################################

            if (isAutoClosingEnabled)
                UIHelper.Button(mainContainer, contentPanel, "ultimatelocker.autoclosing.cmd",
                    Lang("UiOpenAutoClosingButtonText", player.UserIDString), "0.027 0.011", "0.50 0.06", fadeIn: 0.5f);

            CuiHelper.AddUi(player, mainContainer);
        }

        private void OpenAutoClosingSettingsUI(BasePlayer player)
        {
            var mainContainer = UIHelper.MainContainer(UIAutoClosing, "0.35 0.20", "0.65 0.80", requireCursor: true);
            UIHelper.Header(mainContainer, UIAutoClosing, UIAutoClosing + ".Header", Lang("UiAutoClosingHeaderTitle", player.UserIDString),
                closeUI: UIAutoClosing);

            var contentPanel = UIHelper.ParentPanel(mainContainer, UIAutoClosing, UIAutoClosing + ".Container",
                "0 0", "0.997 0.938");

            var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(player);

            const float rowHeight = 0.0620f;
            const float rowHeightMargin = 0.0166f;

            //#################### AutoClosingEnabled ####################
            var rowOffsetY = 0.9245;
            var rowAutoClosingEnabled = UIHelper.Row(mainContainer, contentPanel, UIAutoClosing + ".Row.AutoClosingEnabled",
                $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
            UIHelper.Text(mainContainer, rowAutoClosingEnabled, Lang("UiAutoClosingEnabledTitle", player.UserIDString), "0.02 0.2", "0.78 0.7");
            UIHelper.ToggleButton(mainContainer, rowAutoClosingEnabled, autoClosingPlayerSettings.AutoClosingEnabled,
                "ultimatelocker.autoclosing.cmd toggleAutoClosingEnabled", "0.8 0.1", "0.99 0.85",
                Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
            //####################################################################################################

            if (!autoClosingPlayerSettings.AutoClosingEnabled)
            {
                CuiHelper.AddUi(player, mainContainer);
                return;
            }

            //########################################## Delay Settings ##########################################
            const float componentLeftMargin = 10f;
            const float componentRightMargin = -20f;
            const float componentHeight = 26f;
            const float componentHeightMargin = 6f;

            var offsetY = componentHeightMargin * -1; //Add top margin
            var rowCuiPanels = new List<UIHelper.RowCuiPanel>();

            const string basicCmd = "ultimatelocker.autoclosing.cmd";

            const int fontSize = 8;
            var autoClosingConfiguration = _config.AutoClosingConfiguration;
            if (autoClosingConfiguration.EnableDoorAutoClosing)
            {
                //#################### DoorAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.DoorAutoClosingEnabled", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiDoorAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleDoorAutoClosingEnabled",
                    autoClosingPlayerSettings.DoorAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.DoorAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.DoorAutoClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.DoorClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.Door}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.Door}", $"{basicCmd} removeAllDoorCloser {AutoClosingType.Door}",
                        fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableDoubleDoorAutoClosing)
            {
                //#################### DoubleDoorAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.DoubleDoorAutoClosingEnabled", offsetY, componentLeftMargin,
                    componentHeight,
                    componentRightMargin, Lang("UiDoubleDoorAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleDoubleDoorAutoClosingEnabled",
                    autoClosingPlayerSettings.DoubleDoorAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.DoubleDoorAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.DoubleDoorClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.DoubleDoorClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.DoubleDoor}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.DoubleDoor}", $"{basicCmd} removeAllDoorCloser {AutoClosingType.DoubleDoor}",
                        fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableWindowAutoClosing)
            {
                //#################### WindowAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.WindowAutoClosingEnabled", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiWindowsAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleWindowAutoClosingEnabled",
                    autoClosingPlayerSettings.WindowAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.WindowAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.WindowClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.WindowClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.Window}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.Window}", $"{basicCmd} removeAllDoorCloser {AutoClosingType.Window}",
                        fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableGarageAutoClosing)
            {
                //#################### GarageAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.GarageAutoClosingEnabled", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiGarageAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleGarageAutoClosingEnabled",
                    autoClosingPlayerSettings.GarageAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.GarageAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.GarageClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.GarageClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.Garage}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.Garage}", $"{basicCmd} removeAllDoorCloser {AutoClosingType.Garage}",
                        fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableLadderHatchAutoClosing)
            {
                //#################### LadderHatchAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.LadderHatchAutoClosingEnabled", offsetY, componentLeftMargin,
                    componentHeight,
                    componentRightMargin, Lang("UiLadderHatchAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleLadderHatchAutoClosingEnabled",
                    autoClosingPlayerSettings.LadderHatchAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.LadderHatchAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.LadderHatchClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.LadderHatchClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.LadderHatch}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.LadderHatch}",
                        $"{basicCmd} removeAllDoorCloser {AutoClosingType.LadderHatch}", fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableExternalGateAutoClosing)
            {
                //#################### ExternalGateAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.ExternalGateAutoClosingEnabled", offsetY, componentLeftMargin,
                    componentHeight,
                    componentRightMargin, Lang("UiExternalGateAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleExternalGateAutoClosingEnabled",
                    autoClosingPlayerSettings.ExternalGateAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.ExternalGateAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.ExternalGateClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.ExternalGateClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.ExternalGate}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.ExternalGate}",
                        $"{basicCmd} removeAllDoorCloser {AutoClosingType.ExternalGate}", fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableFenceGateAutoClosing)
            {
                //#################### FenceGateAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.FenceGateAutoClosingEnabled", offsetY, componentLeftMargin,
                    componentHeight,
                    componentRightMargin, Lang("UiFenceGateAutoClosingEnabled", player.UserIDString), $"{basicCmd} toggleFenceGateAutoClosingEnabled",
                    autoClosingPlayerSettings.FenceGateAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.FenceGateAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.FenceGateClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.FenceGateClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.FenceGate}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.FenceGate}", $"{basicCmd} removeAllDoorCloser {AutoClosingType.FenceGate}",
                        fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            if (autoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosing)
            {
                //#################### LegacyWoodShelterDoorAutoClosingEnabled ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoClosing}.Row.LegacyWoodShelterDoorAutoClosingEnabled", offsetY, componentLeftMargin,
                    componentHeight,
                    componentRightMargin, Lang("UiLegacyWoodShelterDoorAutoClosingEnabled", player.UserIDString),
                    $"{basicCmd} toggleLegacyWoodShelterDoorAutoClosingEnabled",
                    autoClosingPlayerSettings.LegacyWoodShelterDoorAutoClosingEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;

                if (autoClosingPlayerSettings.LegacyWoodShelterDoorAutoClosingEnabled)
                {
                    rowCuiPanels.Add(ScrollViewAddAutoClosingSettingRow(mainContainer, $"{UIAutoClosing}.Row.LegacyWoodShelterDoorClosingDelay", offsetY,
                        componentLeftMargin, componentHeight, componentRightMargin, Lang("UiClosingDelay", player.UserIDString),
                        autoClosingPlayerSettings.LegacyWoodShelterDoorClosingDelay, $"{basicCmd} saveNewDelayTime {AutoClosingType.LegacyWoodShelterDoor}",
                        $"{basicCmd} updateAllDoorCloserDelayTime {AutoClosingType.LegacyWoodShelterDoor}",
                        $"{basicCmd} removeAllDoorCloser {AutoClosingType.LegacyWoodShelterDoor}", fontSize, player));
                    offsetY = offsetY - componentHeight - componentHeightMargin;
                }
                //############################################################
            }

            var scrollViewHeight = rowCuiPanels.Count * componentHeight + rowCuiPanels.Count * componentHeightMargin;
            scrollViewHeight += componentHeightMargin; //Add bottom margin
            var scrollView = UIHelper.ScrollView(mainContainer, contentPanel, UIAutoClosing + ".ScrollView", scrollViewHeight,
                "0 0.014", $"0.997 {rowOffsetY - .01}");

            foreach (var rowCuiPanel in rowCuiPanels)
            {
                mainContainer.Add(rowCuiPanel.CuiPanel, scrollView, rowCuiPanel.PanelName);
                rowCuiPanel.Render?.Invoke();
            }
            //####################################################################################################

            CuiHelper.AddUi(player, mainContainer);
        }

        private UIHelper.RowCuiPanel ScrollViewAddRow(CuiElementContainer container, string panelName, float offsetY,
            float componentLeftMargin, float componentHeight, float componentRightMargin, string text, string commandAction, bool toggleState,
            BasePlayer? player = null, int textFontSize = 11)
        {
            var rowLockersLock = UIHelper.Row($"{panelName}", "0 1", "1 1",
                $"{componentLeftMargin} {offsetY - componentHeight}", $"{componentRightMargin} {offsetY}");
            rowLockersLock.Render = () =>
            {
                UIHelper.Text(container, rowLockersLock.PanelName, text, "0.02 0.2", "0.78 0.7", textFontSize);

                if (player != null)
                    UIHelper.ToggleButton(container, rowLockersLock.PanelName, toggleState, $"{commandAction}", "0.8 0.1", "0.99 0.85",
                        Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
                else
                    UIHelper.ToggleButton(container, rowLockersLock.PanelName, toggleState, $"{commandAction}", "0.8 0.1", "0.99 0.85");
            };

            return rowLockersLock;
        }

        private UIHelper.RowCuiPanel ScrollViewAddAutoClosingSettingRow(CuiElementContainer container, string panelName, float offsetY,
            float componentLeftMargin, float componentHeight, float componentRightMargin, string text, int delayTime, string commandDelayTimeUpdate,
            string commandUpdate, string commandRemove, int fontSize = 10, BasePlayer? player = null)
        {
            var rowLockersLock = UIHelper.Row($"{panelName}", "0 1", "1 1",
                $"{componentLeftMargin} {offsetY - componentHeight}", $"{componentRightMargin} {offsetY}");
            rowLockersLock.Render = () =>
            {
                UIHelper.Text(container, rowLockersLock.PanelName, text, "0.02 0.2", "0.30 0.7", fontSize);
                UIHelper.Input(container, rowLockersLock.PanelName, commandDelayTimeUpdate, delayTime.ToString(), "0.30 0.1", "0.45 0.85",
                    textAlign: TextAnchor.MiddleCenter, maxLength: 4);
                UIHelper.UpdateButton(container, rowLockersLock.PanelName, commandUpdate, Lang("UiUpdateAll", player.UserIDString),
                    "0.47 0.1", "0.72 0.85", fontSize: 10, fadeIn: 0.5f);
                UIHelper.DeleteButton(container, rowLockersLock.PanelName, commandRemove, Lang("UiRemoveAll", player.UserIDString),
                    "0.74 0.1", "0.99 0.85", fontSize: 10, fadeIn: 0.5f);
            };

            return rowLockersLock;
        }

        private bool CheckPlayerHasShareLocksWithClanTeamEnabled(ulong steamID)
        {
            if (!steamID.IsSteamId()) return false;
            //Se ForceShareLocksWithClanTeam è diverso da null allora utilizza il valore forzato, altrimenti utilizza il valore scelto dal player
            return _config.AutoLockConfiguration.ForceShareLocksWithClanTeam ?? GetAutoLockPlayerSettingsData(steamID).ShareLocksWithClanTeamEnabled;
        }

        private AutoLockPlayerSettingsData GetAutoLockPlayerSettingsData(ulong steamID)
        {
            if (_data.AutoLockPlayerSettingsData.TryGetValue(steamID, out var autoLockPlayerSettingsData))
                return autoLockPlayerSettingsData;

            //Create default Auto Lock Player Settings Data
            autoLockPlayerSettingsData = GetDefaultAutoLockPlayerSettingsData(steamID);

            _data.AutoLockPlayerSettingsData.Add(steamID, autoLockPlayerSettingsData);
            SaveData();
            return autoLockPlayerSettingsData;
        }

        private AutoLockPlayerSettingsData GetDefaultAutoLockPlayerSettingsData(ulong steamID)
        {
            //Create default Auto Lock Player Settings Data
            var defaultAutoLockConfiguration = _config.AutoLockConfiguration;
            return new AutoLockPlayerSettingsData
            {
                SteamID = steamID,
                CodeLock = GenerateRandomCode(),
                GuestCodeLock = GenerateRandomCode(),

                AutoLockOnPlacementEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableAutoLockOnPlacementByDefault,
                AlsoUseKeyLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableAlsoUseKeyLockByDefault,
                GuestCodeEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableGuestCodeByDefault,
                ShareLocksWithClanTeamEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableAutoShareLocksWithClanTeamByDefault,
                StreamerModeEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableStreamerModeByDefault,
                DoorsLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableDoorsLockByDefault,
                WindowsLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableWindowsLockByDefault,
                BoxesLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableBoxesLockByDefault,
                StorageContainerLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableStorageContainerLockByDefault,
                LockersLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableLockersLockByDefault,
                CupboardsLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableCupboardsLockByDefault,
                VehicleLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableVehicleLockByDefault,
                VehicleLockAutoClosingOnDismountEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableVehicleLockAutoClosingOnDismountByDefault,
                MedievalEntityLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableMedievalEntityLockByDefault,
                FurnaceLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableFurnaceLockByDefault,
                VendingMachineLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableVendingMachineLockByDefault,
                ComposterLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableComposterLockByDefault,
                MixingTableLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableMixingTableLockByDefault,
                PlanterLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnablePlanterLockByDefault,
                AutoTurretLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableAutoTurretLockByDefault,
                SamSiteLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableSamSiteLockByDefault,
                TrapsLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableTrapsLockByDefault,
                WeaponRackLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableWeaponRackLockByDefault,
                StashLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableStashLockByDefault,
                NeonSignLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableNeonSignLockByDefault,
                OtherLockableEntitiesLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableOtherLockableEntitiesLockByDefault,
                OtherCustomEntitiesLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableOtherCustomEntitiesLockByDefault,
            };
        }

        private AutoClosingPlayerSettingsData GetAutoClosingPlayerSettingsData(BasePlayer player) => GetAutoClosingPlayerSettingsData(player.userID);

        private AutoClosingPlayerSettingsData GetAutoClosingPlayerSettingsData(ulong userID)
        {
            if (_data.AutoClosingPlayerSettingsData.TryGetValue(userID, out var autoClosingPlayerSettingsData))
                return autoClosingPlayerSettingsData;

            //Create default Auto Closing Player Settings Data
            autoClosingPlayerSettingsData = GetDefaultAutoClosingPlayerSettingsData(userID);

            _data.AutoClosingPlayerSettingsData.Add(userID, autoClosingPlayerSettingsData);
            SaveData();
            return autoClosingPlayerSettingsData;
        }

        private AutoClosingPlayerSettingsData GetDefaultAutoClosingPlayerSettingsData(ulong userID)
        {
            var defaultAutoClosingConfiguration = _config.AutoClosingConfiguration;
            //Create default Auto Closing Player Settings Data
            var defaultPlayerAutoClosingConfiguration = _config.AutoLockConfiguration.AutoClosingPlayerConfiguration;
            return new AutoClosingPlayerSettingsData
            {
                SteamID = userID,

                AutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableAutoClosingByDefault,
                DoorAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableDoorAutoClosingByDefault,
                DoubleDoorAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableDoubleDoorAutoClosingByDefault,
                WindowAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableWindowAutoClosingByDefault,
                GarageAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableGarageAutoClosingByDefault,
                LadderHatchAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableLadderHatchAutoClosingByDefault,
                ExternalGateAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableExternalGateAutoClosingByDefault,
                FenceGateAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableFenceGateAutoClosingByDefault,
                LegacyWoodShelterDoorAutoClosingEnabled = defaultPlayerAutoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosingByDefault,

                DoorClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                DoubleDoorClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                GarageClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                LadderHatchClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                ExternalGateClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                FenceGateClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                LegacyWoodShelterDoorClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime
            };
        }

        private static string GenerateRandomCode()
        {
            return Random.Range(1000, 9999).ToString();
        }

        private static string PadWithZeros(string input)
        {
            return string.IsNullOrWhiteSpace(input) ? input : input.PadLeft(4, '0');
        }

        private static bool IsValidLockCode(string? code)
        {
            if (string.IsNullOrEmpty(code)) return false;
            if (code.Length > 4) return false;
            return int.TryParse(code, out var number) && number >= 0;
        }

        private bool IsValidDelayTime(string? delayTimeString)
        {
            if (string.IsNullOrWhiteSpace(delayTimeString) || !delayTimeString.IsNumeric()) return false;
            if (!int.TryParse(delayTimeString, out var delayTime)) return false;
            return delayTime >= _config.AutoClosingConfiguration.MinimumClosingDelayTime &&
                   delayTime <= _config.AutoClosingConfiguration.MaximumClosingDelayTime;
        }

        private int GetValidDelayTime(string? delayTimeString)
        {
            if (string.IsNullOrWhiteSpace(delayTimeString) || !IsValidDelayTime(delayTimeString))
                return _config.AutoClosingConfiguration.DefaultClosingDelayTime;
            var delayTime = int.Parse(delayTimeString);
            return Mathf.Clamp(delayTime, _config.AutoClosingConfiguration.MinimumClosingDelayTime, _config.AutoClosingConfiguration.MaximumClosingDelayTime);
        }

        private static PlayerInfo? FindPlayerInfoBySteamID(ulong steamID)
        {
            var basePlayer = BasePlayer.allPlayerList.FirstOrDefault(p => p.userID == steamID);
            var serverUser = ServerUsers.Get(steamID);
            return ConvertToPlayerInfo(basePlayer, serverUser);
        }

        private static PlayerInfo? ConvertToPlayerInfo(BasePlayer? basePlayer, ServerUsers.User? serverUser = null)
        {
            if (basePlayer == null && serverUser == null)
                return null;

            var playerInfo = new PlayerInfo
            {
                Steamid = basePlayer != null ? basePlayer.userID : serverUser!.steamid,
                DisplayName = basePlayer != null ? basePlayer.displayName : serverUser!.username,
                Transform = basePlayer != null ? basePlayer.transform : null,
                IsBasePlayer = basePlayer != null,
                PlayerObject = basePlayer != null ? basePlayer : serverUser
            };

            return playerInfo;
        }

        private IEnumerator LockSetLocked(PlayerInfo? player, List<BaseLock>? lockList, bool isLocked)

        {
            if (lockList == null)
                yield break;

            var count = 0;
            foreach (var locker in lockList)
            {
                locker.SetFlag(BaseEntity.Flags.Locked, isLocked);

                if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
                count++;
            }

            if (player is { PlayerObject: BasePlayer basePlayer })
            {
                timer.Once(1f,
                    () =>
                    {
                        PlayerShowToast(basePlayer,
                            Lang("CodeLockListMessage", basePlayer.UserIDString,
                                isLocked
                                    ? Lang("LockerStatus_Locked", basePlayer.UserIDString)
                                    : Lang("LockerStatus_Unlocked", basePlayer.UserIDString),
                                lockList.Count.ToString()));
                    });
            }
        }

        private IEnumerator RemoveLock(PlayerInfo? player, List<BaseLock>? lockList)
        {
            if (lockList == null)
                yield break;

            var count = 0;
            foreach (var locker in lockList)
            {
                locker.Kill();

                if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
                count++;
            }

            if (player is { PlayerObject: BasePlayer basePlayer })
            {
                timer.Once(1f,
                    () =>
                    {
                        PlayerShowToast(basePlayer,
                            Lang("CodeLockListMessage", basePlayer.UserIDString, Lang("LockerStatus_Removed", basePlayer.UserIDString),
                                lockList.Count.ToString()));
                    });
            }
        }

        private IEnumerator UpdateDoorCloserDelayTime(BasePlayer? player, AutoClosingType? autoClosingType, AutoClosingPlayerSettingsData settingsData,
            List<DoorCloser> doorCloserList)
        {
            if (doorCloserList.IsNullOrEmpty())
                yield break;

            var count = 0;
            var changeCount = 0;
            var countExcluded = 0;
            foreach (var doorCloser in doorCloserList)
            {
                //Door closers that have custom delay time changed manually
                var userID = player != null ? player.userID.Get() : doorCloser.OwnerID;
                if (userID.IsSteamId() && _data.DoorCloserCustomTimeItemsData.TryGetValue(userID, out var doorCloserItemDataList))
                {
                    var doorCloserItemData = doorCloserItemDataList.FirstOrDefault(dc => dc.NetId.Equals(doorCloser.net.ID.Value));
                    if (doorCloserItemData != null)
                    {
                        doorCloser.delay = doorCloserItemData.DelayTime;
                        doorCloser.SendNetworkUpdate();
                        countExcluded++;
                        continue;
                    }
                }

                if (autoClosingType == null)
                {
                    if (doorCloser.GetParentEntity() == null) continue;
                    var parentEntity = doorCloser.GetParentEntity();

                    autoClosingType = AutoClosingTypeByEntity(parentEntity);
                    if (autoClosingType == AutoClosingType.None) continue;
                }

                switch (autoClosingType)
                {
                    case AutoClosingType.Door when settingsData.DoorAutoClosingEnabled:
                        doorCloser.delay = settingsData.DoorClosingDelay;
                        changeCount++;
                        break;
                    case AutoClosingType.DoubleDoor when settingsData.DoubleDoorAutoClosingEnabled:
                        doorCloser.delay = settingsData.DoubleDoorClosingDelay;
                        changeCount++;
                        break;
                    case AutoClosingType.Window when settingsData.WindowAutoClosingEnabled:
                        doorCloser.delay = settingsData.WindowClosingDelay;
                        changeCount++;
                        break;
                    case AutoClosingType.Garage when settingsData.GarageAutoClosingEnabled:
                        doorCloser.delay = settingsData.GarageClosingDelay + GarageOpenDuration;
                        changeCount++;
                        break;
                    case AutoClosingType.LadderHatch when settingsData.LadderHatchAutoClosingEnabled:
                        doorCloser.delay = settingsData.LadderHatchClosingDelay;
                        changeCount++;
                        break;
                    case AutoClosingType.ExternalGate when settingsData.ExternalGateAutoClosingEnabled:
                        doorCloser.delay = settingsData.ExternalGateClosingDelay;
                        changeCount++;
                        break;
                    case AutoClosingType.FenceGate when settingsData.FenceGateAutoClosingEnabled:
                        doorCloser.delay = settingsData.FenceGateClosingDelay;
                        changeCount++;
                        break;
                    case AutoClosingType.LegacyWoodShelterDoor when settingsData.LegacyWoodShelterDoorAutoClosingEnabled:
                        doorCloser.delay = settingsData.LegacyWoodShelterDoorClosingDelay;
                        changeCount++;
                        break;
                }

                doorCloser.SendNetworkUpdate();

                if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
                count++;
            }

            if (player != null && changeCount > 0)
            {
                timer.Once(1f,
                    () =>
                    {
                        PlayerShowToast(player,
                            Lang("DoorCloserListMessage", player.UserIDString, Lang("LockerStatus_Updated", player.UserIDString),
                                (doorCloserList.Count - countExcluded).ToString()));
                    });
            }
        }

        private IEnumerator RemoveDoorCloser(BasePlayer player, List<DoorCloser>? doorCloserList)
        {
            if (doorCloserList.IsNullOrEmpty())
                yield break;

            var count = 0;
            foreach (var doorCloser in doorCloserList!)
            {
                DoorCloserRemoved(player, doorCloser);
                doorCloser.Kill();

                if (count % CoroutineIteratorsPerFrame == 0) yield return CoroutineEx.waitForEndOfFrame;
                count++;
            }

            if (player != null)
            {
                timer.Once(1f,
                    () =>
                    {
                        PlayerShowToast(player,
                            Lang("DoorCloserListMessage", player.UserIDString, Lang("LockerStatus_Removed", player.UserIDString),
                                doorCloserList.Count.ToString()));
                    });
            }
        }

        private void PlayerShowToast(BasePlayer player, string message, bool isError = false)
        {
            if (player == null)
                return;

            NextTick(() => player.ShowToast(isError ? GameTip.Styles.Red_Normal : GameTip.Styles.Blue_Long, message, false));
        }

        private static void ShowLockerUI(BasePlayer player, bool hasAlpha = false)
        {
            if (player == null) return;

            DestroyLockerUI(player);
            var container =
                GuiHelper.ContainerWithOffset(UILocker, "0 0 0 0", "0.5 0.5", "0.5 0.5", "-20 -25", "20 25");

            var isKeyLock = false;
            var image = null as string;
            if (player.GetActiveItem().info.itemid.Equals(KeyLockItemID))
            {
                image = GetImage(KeyLockOnDeployImageName);
                isKeyLock = true;
            }
            else if (player.GetActiveItem().info.itemid.Equals(CodeLockItemID))
                image = GetImage(CodeLockOnDeployImageName);

            var ancorMax = isKeyLock ? "1 0.8" : "1 1";
            if (!string.IsNullOrEmpty(image))
                GuiHelper.ImageUrl(container, UILocker, image, "0 0", ancorMax,
                    hasAlpha ? "1 1 1 0.5" : "1 1 1 1");
            else
                GuiHelper.Image(container, UILocker, CodeLockItemID, 0, "0 0", ancorMax,
                    hasAlpha ? "1 1 1 0.5" : "1 1 1 1");

            CuiHelper.AddUi(player, container);
        }

        private static void DestroyLockerUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, UILocker);
        }

        private static void DestroyAutoLockUI(BasePlayer player)
        {
            if (player == null) return;
            CuiHelper.DestroyUi(player, UIAutoLock);
        }

        private static string? GetImage(string? imageName, ulong skin = 0, bool returnUrl = false)
        {
            return self == null || string.IsNullOrEmpty(imageName)
                ? null
                : (string)self.ImageLibrary?.Call("GetImage", imageName, skin, returnUrl)!;
        }

        private void RunEffect(string effect, BaseEntity baseEntity)
        {
            if (string.IsNullOrEmpty(effect) || baseEntity == null) return;

            // if (_config.DisableCodeLockEffects) return;
            // if (_config.DisableCodeLockEffectsIfInNoClipOrVanish && IsInvisible(player)) return;

            Effect.server.Run(effect, baseEntity.transform.position);
        }

        private void RunEffect(string effect, BasePlayer player)
        {
            if (string.IsNullOrEmpty(effect) || player == null) return;

            // if (_config.DisableCodeLockEffects) return;
            // if (_config.DisableCodeLockEffectsIfInNoClipOrVanish && IsInvisible(player)) return;

            Effect.server.Run(effect, player.transform.position);
        }

        private bool IsEntityOwner(BaseEntity? baseEntity, BasePlayer basePlayer)
        {
            if (baseEntity == null || !basePlayer.userID.IsSteamId())
                return false;

            if (!baseEntity.OwnerID.IsSteamId())
                return false;

            if (baseEntity.OwnerID.IsSteamId() && baseEntity.OwnerID.Equals(basePlayer.userID))
                return true;

            var clanInfo = GetClanInfo(basePlayer);
            if (clanInfo == null || clanInfo.ClanMemberUserIdList.IsNullOrEmpty()) return false;

            var memberSteamIdList = clanInfo.ClanMemberUserIdList
                .Select(ulong.Parse)
                .Where(CheckPlayerHasShareLocksWithClanTeamEnabled)
                .ToList();

            return memberSteamIdList.Contains(baseEntity.OwnerID);
        }

        private IPlayer? GetIPlayer(string partialNameOrId)
        {
            if (string.IsNullOrWhiteSpace(partialNameOrId)) return null;
            var player = covalence.Players.FindPlayer(partialNameOrId);
            return player;
        }

        private ClanInfo? GetClanInfoBySteamID(ulong userID)
        {
            return GetClanInfo(BasePlayer.FindByID(userID));
        }

        private ClanInfo? GetClanInfo(BasePlayer player)
        {
            if (player == null)
                return null;

            var clanInfo = new ClanInfo();
            //Native Rust Team
            if (Clans == null && player.Team != null)
            {
                clanInfo.ClanName = player.Team.teamName;
                clanInfo.ClanMemberUserIdList = player.Team.members.Select(m => m.ToString()).ToList();
                return clanInfo;
            }

            //Clans plugin
            if (Clans != null)
            {
                // var player = BasePlayer.FindByID(userID);
                if (GetClanOf(player.userID) == null)
                {
                    return null;
                }

                // var clanInfo = new ClanInfo();
                var clanName = GetClanOf(player.userID);

                clanInfo.ClanName = clanName;

                var clanMembers = GetClanMembers(clanName);
                foreach (var member in clanMembers)
                {
                    clanInfo.ClanMemberUserIdList.Add((string)member);
                }

                return clanInfo;
            }

            return null;
        }

        private string? GetClanOf(ulong playerID) => Clans?.Call<string>("GetClanOf", playerID);
        private JObject GetClan(string tag) => Clans?.Call<JObject>("GetClan", tag);
        private JArray GetClanMembers(string tag) => (JArray)GetClan(tag)?.SelectToken("members");


        private static class RemoverTool_Patch
        {
            public static bool Prefix_HasAccess(BasePlayer player, BaseEntity targetEntity, ref bool __result)
            {
                if (targetEntity == null || targetEntity is not DoorCloser doorCloser)
                    return true; //Run Normal code

                __result = false;
                return false; //Block Original code
            }
        }

        private static class ModularCarCodeLock_Settings
        {
            public static void Postfix_TryAddALock(ModularCarCodeLock __instance, string code, ulong userID)
            {
                Interface.CallHook("OnModularCarLockAdded", (object)__instance, code, userID);
            }

            public static void Postfix_RemoveLock(ModularCarCodeLock __instance)
            {
                Interface.CallHook("OnModularCarLockRemoved", (object)__instance);
            }
        }

        private static class Workbench_Settings
        {
            public static bool Prefix_RPC_TechTreeUnlock(BaseEntity.RPCMessage msg, Workbench __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnWorkbenchTechTreeUnlock", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class SmartSwitch_ToggleSwitch
        {
            public static bool Prefix(BaseEntity.RPCMessage msg, SmartSwitch __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnSmartSwitchToggle", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class TimerSwitch_SVSwitch
        {
            public static bool Prefix(BaseEntity.RPCMessage msg, TimerSwitch __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnTimerSwitchToggle", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class CustomTimerSwitch_SERVER_SetTime
        {
            public static bool Prefix(BaseEntity.RPCMessage msg, CustomTimerSwitch __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("CanTimerSwitchSetTime", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class FogMachine_Toggle
        {
            public static bool Prefix_SetFogOn(BaseEntity.RPCMessage msg, FogMachine __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnFogMachineToggle", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_SetFogOff(BaseEntity.RPCMessage msg, FogMachine __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnFogMachineToggle", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class StrobeLight_Settings
        {
            public static bool Prefix_SetStrobe(BaseEntity.RPCMessage msg, StrobeLight __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnStrobeLightToggle", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_SetStrobeSpeed(BaseEntity.RPCMessage msg, StrobeLight __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnStrobeSpeedChange", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class AudioVisualisationEntity_Settings
        {
            public static bool Prefix_ServerUpdateSettings(BaseEntity.RPCMessage msg,
                AudioVisualisationEntity __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnAudioVisualisationServerUpdateSettings", (object)__instance,
                            (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class Signage_Settings
        {
            // public static bool Prefix_CanUpdateSign(BasePlayer player, Signage __instance)
            // {
            //     try
            //     {
            //         if (player == null || __instance == null) return true; //Run Normal code
            //
            //         if (Interface.CallHook("OnSignageCanUpdateSign", (object)__instance, player) != null)
            //             return false; //Block Original code
            //         
            //         // if (Interface.CallHook("OnSignageUpdateSign", (object)__instance, (object)msg.player) != null)
            //         //     return false; //Block Original code
            //     }
            //     catch (Exception e)
            //     {
            //         self?.Puts($"Exception: {e.Message}");
            //     }
            //
            //     return true; //Run Normal code
            // }

            public static bool Prefix_LockSign(BaseEntity.RPCMessage msg, Signage __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnSignageLockUnlockSign", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_UnLockSign(BaseEntity.RPCMessage msg, Signage __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnSignageLockUnlockSign", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class NeonSign_Settings
        {
            public static bool Prefix_SetAnimationSpeed(BaseEntity.RPCMessage msg, NeonSign __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnNeonSignSetAnimationSpeed", (object)__instance, (object)msg.player) !=
                        null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_UpdateNeonColors(BaseEntity.RPCMessage msg, NeonSign __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnUpdateNeonColors", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class SearchLight_Settings
        {
            public static bool Prefix_UseLight(BaseEntity.RPCMessage msg, SearchLight __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnSearchLightUseLight", (object)__instance, (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class PoweredRemoteControlEntity_Settings
        {
            public static bool Prefix_CanChangeID(BasePlayer player, PoweredRemoteControlEntity __instance)
            {
                try
                {
                    if (player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnPoweredRemoteControlEntityCanChangeID", (object)player,
                            (object)__instance) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_CanControl(ulong playerID, PoweredRemoteControlEntity __instance)
            {
                try
                {
                    if (!playerID.IsSteamId() || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnPoweredRemoteControlEntityCanControl", playerID, (object)__instance) !=
                        null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class RFBroadcaster_Settings
        {
            public static bool Prefix_ServerSetFrequency(BaseEntity.RPCMessage msg, RFBroadcaster __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnRFBroadcasterCanChangeFrequency", (object)__instance,
                            (object)msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class RFReceiver_Settings
        {
            public static bool Prefix_ServerSetFrequency(BaseEntity.RPCMessage msg, RFReceiver __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnRFReceiverCanChangeFrequency", (object)__instance, (object)msg.player) !=
                        null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class AutoTurret_Settings
        {
            public static bool Prefix_CanControl(ulong playerID, AutoTurret __instance)
            {
                try
                {
                    if (!playerID.IsSteamId() || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnAutoTurretCanControl", playerID, (object)__instance) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        // private static class GrowableEntity_Settings
        // {
        //     public static bool Prefix_PickFruit(BasePlayer player, bool eat, GrowableEntity __instance)
        //     {
        //         try
        //         {
        //             if (player == null || __instance == null) return true; //Run Normal code
        //
        //             if (Interface.CallHook("CanPickFruit", (object)__instance, player, eat) != null)
        //                 return false; //Block Original code
        //         }
        //         catch (Exception e)
        //         {
        //             self?.Puts($"Exception: {e.Message}");
        //         }
        //
        //         return true; //Run Normal code
        //     }
        // }

        private static class IndustrialCrafter_Settings
        {
            public static bool Prefix_SvSwitch(BaseEntity.RPCMessage msg, IndustrialCrafter __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnIndustrialCrafterSwitchToggle", (object)__instance, (object)msg.player) !=
                        null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        private static class MedievalEntity_Settings
        {
            public static bool BatteringRam_Prefix_RPC_OpenDoor(BaseEntity.RPCMessage rpc, BatteringRam __instance)
            {
                try
                {
                    if (rpc.player == null || __instance == null) return true; //Run Normal code
                    if (Interface.CallHook("CanBatteringRamOpenDoor", (object)__instance, (object)rpc.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool BatteringRam_Prefix_RPC_CloseDoor(BaseEntity.RPCMessage rpc, BatteringRam __instance)
            {
                try
                {
                    if (rpc.player == null || __instance == null) return true; //Run Normal code
                    if (Interface.CallHook("CanBatteringRamCloseDoor", __instance, rpc.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Catapult_Prefix_SERVER_ReloadStart(BaseEntity.RPCMessage msg, Catapult __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code
                    if (Interface.CallHook("CanCatapultReloadStart", __instance, msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool SiegeTower_Prefix_RPC_OpenDoor(BaseEntity.RPCMessage rpc, Door __instance)
            {
                try
                {
                    if (rpc.player == null || __instance == null || __instance is not SiegeTowerDoor) return true; //Run Normal code
                    if (Interface.CallHook("CanSiegeTowerOpenDoor", __instance, rpc.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool SiegeTower_Prefix_RPC_CloseDoor(BaseEntity.RPCMessage rpc, Door __instance)
            {
                try
                {
                    if (rpc.player == null || __instance == null || __instance is not SiegeTowerDoor) return true; //Run Normal code
                    if (Interface.CallHook("CanSiegeTowerCloseDoor", __instance, rpc.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool BaseSiegeWeapon_Prefix_SERVER_StartPulling(BaseEntity.RPCMessage msg, BaseSiegeWeapon __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code
                    if (Interface.CallHook("CanBaseSiegeWeaponStartPulling", __instance, msg.player) != null)
                        return false; //Block Original code
                }
                catch (Exception e)
                {
                    self?.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        public class LockerBehaviour : MonoBehaviour
        {
            private BasePlayer? _player;
            private Coroutine? _raycastCoroutine;

            private void Awake()
            {
                _player = GetComponent<BasePlayer>();
                if (self == null || _player == null || _player.IsNpc || _player.IsDestroyed || _player.GetActiveItem() == null)
                {
                    Destroy(this);
                    return;
                }

                if (!self.permission.UserHasPermission(_player.UserIDString, PermissionUse))
                {
                    Destroy(this);
                    return;
                }

                // Avvia la coroutine per il controllo visivo (Mostrare icona della serratura)
                _raycastCoroutine = ServerMgr.Instance.StartCoroutine(RaycastLoop());
            }

            public void OnDestroy()
            {
                // Ferma la coroutine per evitare che continui a girare in background
                if (_raycastCoroutine != null) ServerMgr.Instance.StopCoroutine(_raycastCoroutine);

                // Distrugge l'UI quando il componente viene rimosso
                if (_player != null) DestroyLockerUI(_player);
            }

            private IEnumerator RaycastLoop()
            {
                // Il loop continua finché il componente esiste e il giocatore è valido
                while (_player is not null && !_player.IsDestroyed && self != null)
                {
                    var entityLookedAt = GetEntityByRayCast(_player);
                    if (entityLookedAt is not null && self.CanDeployLock(_player, entityLookedAt))
                    {
                        ShowLockerUI(_player);
                    }
                    else
                    {
                        DestroyLockerUI(_player);
                    }

                    // Attendi 0.2 secondi prima del prossimo controllo.
                    yield return new WaitForSeconds(0.2f);
                }
            }

            public void HandleInput(InputState input)
            {
                if (self == null || _player == null || _player.IsDestroyed)
                {
                    Destroy(this);
                    return;
                }

                var activeItem = _player.GetActiveItem();
                if (activeItem == null)
                {
                    Destroy(this);
                    return;
                }

                if (!activeItem.info.itemid.Equals(KeyLockItemID) && !activeItem.info.itemid.Equals(CodeLockItemID))
                {
                    Destroy(this);
                    return;
                }

                if (self!._config.AutoLockConfiguration.UseOnlyKeyLock && activeItem.info.itemid.Equals(CodeLockItemID))
                {
                    Destroy(this);
                    return;
                }

                if (!self.permission.UserHasPermission(_player!.UserIDString, PermissionUse))
                {
                    Destroy(this);
                    return;
                }

                if (input == null || !input.WasJustPressed(BUTTON.FIRE_PRIMARY))
                {
                    return; // Esegue il deploy della serratura solo se è stato premuto il tasto di fuoco
                }

                var entityLookedAt = GetEntityByRayCast(_player);
                if (entityLookedAt == null || !self.CanDeployLock(_player, entityLookedAt))
                {
                    return;
                }

                // Se il deploy della serratura va a buon fine, consuma l'oggetto
                if (self.DeployLock(_player, entityLookedAt))
                {
                    activeItem.UseItem(1);
                    self.RunEffect(CodeLockDeployEffect, _player);

                    // Il giocatore non può più piazzare serrature se sono finite, quindi il componente non serve più e viene distrutto.
                    if (activeItem.amount <= 0)
                    {
                        Destroy(this);
                    }
                }
            }
        }

        //TODO OLD LockerBehaviour da rimuovere
        // public class LockerBehaviour : MonoBehaviour
        // {
        //     public BasePlayer? player;
        //     private float _nextUpdateCheckTime;
        //     private float _nextUiUpdateCheckTime;
        //     private const float UpdateInterval = 0.01f;
        //     private const float UiUpdateInterval = 0.5f;
        //
        //     private void Awake()
        //     {
        //         _nextUiUpdateCheckTime = Time.time + UpdateInterval;
        //     }
        //
        //     public void OnDestroy()
        //     {
        //         if (player != null) DestroyLockerUI(player);
        //     }
        //
        //     public void Update()
        //     {
        //         if (Time.time < _nextUpdateCheckTime) return;
        //         _nextUpdateCheckTime = Time.time + UpdateInterval;
        //
        //         var playerInventoryAllItems = Pool.Get<List<Item>>();
        //         try
        //         {
        //             if (self == null || player is null || player.GetActiveItem() == null &&
        //                 !player.GetActiveItem().info.itemid.Equals(KeyLockItemID) &&
        //                 !player.GetActiveItem().info.itemid.Equals(CodeLockItemID))
        //                 Destroy(this);
        //
        //             if (self!._config.AutoLockConfiguration.UseOnlyKeyLock && player!.GetActiveItem().info.itemid.Equals(CodeLockItemID))
        //             {
        //                 Destroy(this);
        //             }
        //
        //             if (!self.permission.UserHasPermission(player!.UserIDString, PermissionUse)) Destroy(this);
        //
        //             if (Time.time >= _nextUiUpdateCheckTime)
        //             {
        //                 DestroyLockerUI(player);
        //
        //                 var entityLookedAt = GetEntityByRayCast(player);
        //                 if (entityLookedAt == null) return;
        //
        //                 if (!self.CanDeployLock(player, entityLookedAt)) return;
        //                 ShowLockerUI(player);
        //                 _nextUiUpdateCheckTime = Time.time + UiUpdateInterval;
        //             }
        //
        //             if (player.serverInput.WasJustReleased(BUTTON.FIRE_PRIMARY))
        //             {
        //                 var entityLookedAt = GetEntityByRayCast(player);
        //                 if (entityLookedAt is null) return;
        //
        //                 if (!self.CanDeployLock(player, entityLookedAt)) return;
        //                 if (!self.DeployLock(player, entityLookedAt)) return;
        //
        //                 var isEmpty = false;
        //
        //                 player.inventory.GetAllItems(playerInventoryAllItems);
        //                 var activeItemId = player.GetActiveItem()?.info.itemid;
        //                 if (activeItemId == null) return;
        //
        //                 var targetItem = playerInventoryAllItems.FirstOrDefault(item => item.info.itemid.Equals(activeItemId));
        //                 if (targetItem != null)
        //                 {
        //                     targetItem.amount--;
        //                     targetItem.MarkDirty();
        //                     if (targetItem.amount <= 0)
        //                     {
        //                         targetItem.Remove();
        //                         isEmpty = true;
        //                     }
        //                 }
        //
        //                 self.RunEffect(CodeLockDeployEffect, player);
        //
        //                 if (isEmpty)
        //                     Destroy(this);
        //             }
        //         }
        //         catch
        //         {
        //             Destroy(this);
        //         }
        //         finally
        //         {
        //             Pool.FreeUnmanaged(ref playerInventoryAllItems);
        //         }
        //     }
        // }

        #endregion

        #region Class

        private static class UIHelper
        {
            public static CuiElementContainer MainContainer(string panelName, string anchorMin, string anchorMax,
                string backgroundColor = "0.133 0.133 0.133 1", bool requireCursor = false, string parent = "Overlay")
            {
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image =
                            {
                                Color = backgroundColor,
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                            CursorEnabled = requireCursor
                        },
                        new CuiElement().Parent = parent, panelName, panelName
                    }
                };
            }

            public static string Header(CuiElementContainer container, string parentName, string panelName, string text,
                string backgroundColor = "0 0 0 0.6", string? closeCommand = null, string? closeUI = null)
            {
                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = backgroundColor,
                        Sprite = "assets/content/ui/ui.background.tile.psd",
                        Material = "assets/content/ui/namefontmaterial.mat"
                    },
                    RectTransform = { AnchorMin = "0 0.94", AnchorMax = "0.997 1" }
                }, parentName, panelName);

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Sprite = "assets/icons/icon-unlocked.png",
                        Material = "assets/icons/iconmaterial.mat",
                        ImageType = Image.Type.Simple
                    },
                    RectTransform =
                    {
                        AnchorMin = "0.02 0.1", AnchorMax = "0.07 0.8"
                    },
                }, panelName);

                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = text.ToUpper(), FontSize = 20, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleLeft,
                        Color = "0.729 0.733 0.729 1", VerticalOverflow = VerticalWrapMode.Truncate
                    },
                    RectTransform = { AnchorMin = "0.09 0", AnchorMax = "0.8 0.9" }
                }, panelName);

                CloseButton(container, panelName, closeCommand, closeUI, "0.92 0", "0.998 0.96", 5);

                return panelName;
            }

            public static string ParentPanel(CuiElementContainer container, string parentName, string panelName,
                string anchorMin, string anchorMax, string backgroundColor = "0 0 0 0", bool cursorEnabled = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = backgroundColor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    CursorEnabled = cursorEnabled,
                }, parentName, panelName);
                return panelName;
            }

            public static void CloseButton(CuiElementContainer container, string panelName, string? closeCommand = null, string? closeUI = null,
                string anchorMin = "0.95 0", string anchorMax = "1.0 0.95", int iconPadding = 4)
            {
                container.Add(new CuiButton
                {
                    Button =
                    {
                        Close = closeUI,
                        Command = closeCommand, Color = "1 0.243 0.173 1",
                        ImageType = Image.Type.Simple
                    },
                    RectTransform =
                    {
                        AnchorMin = anchorMin, AnchorMax = anchorMax
                    }
                }, panelName, panelName + ".CloseButton");

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0.8509805 0.854902 0.8509805 1",
                        Sprite = "assets/icons/close.png",
                        Material = "assets/icons/iconmaterial.mat",
                        ImageType = Image.Type.Simple
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 0", AnchorMax = "1 1",
                        OffsetMin = $"{iconPadding} {iconPadding}", OffsetMax = $"{-iconPadding} {-iconPadding}"
                    },
                }, panelName + ".CloseButton");
            }

            public static void ToggleButton(CuiElementContainer container, string parentName, bool toggleState, string command,
                string anchorMin, string anchorMax, string textYes = "YES", string textNo = "NO")
            {
                const string activatedColor = "0.235 0.294 0.149 1";
                const string deactivatedColor = "0.122 0.125 0.114 1";

                const string activatedTextColor = "0.675 0.875 0.286 1";
                const string deactivatedTextColor = "0.243 0.263 0.2 1";

                const int fontSize = 14;

                container.Add(new CuiPanel
                {
                    Image = { Color = "0.122 0.125 0.114 1" },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
                }, parentName, parentName + ".ToggleButton.Panel");

                container.Add(new CuiButton
                {
                    Button = { Command = command + " True", Color = toggleState ? activatedColor : deactivatedColor },
                    RectTransform = { AnchorMin = "0 0", AnchorMax = "0.5 1" },
                    Text =
                    {
                        Text = textYes, FontSize = fontSize, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter,
                        Color = toggleState ? activatedTextColor : deactivatedTextColor, VerticalOverflow = VerticalWrapMode.Truncate
                    }
                }, parentName + ".ToggleButton.Panel");

                container.Add(new CuiButton
                {
                    Button = { Command = command + " False", Color = toggleState ? deactivatedColor : activatedColor },
                    RectTransform = { AnchorMin = "0.5 0", AnchorMax = "1 1" },
                    Text =
                    {
                        Text = textNo, FontSize = fontSize, Font = "robotocondensed-bold.ttf", Align = TextAnchor.MiddleCenter,
                        Color = toggleState ? deactivatedTextColor : activatedTextColor, VerticalOverflow = VerticalWrapMode.Truncate
                    }
                }, parentName + ".ToggleButton.Panel");
            }

            public static void DeleteButton(CuiElementContainer container, string parentName, string command, string text,
                string anchorMin, string anchorMax, string textColor = "1 1 1 1", string backgroundColor = "1 0.243 0.173 0.5",
                int fontSize = 12, TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0.0f)
            {
                Button(container, parentName, command, text, anchorMin, anchorMax, textColor, backgroundColor, fontSize, align, fadeIn);
            }

            public static void UpdateButton(CuiElementContainer container, string parentName, string command, string text,
                string anchorMin, string anchorMax, string textColor = "1 1 1 1", string backgroundColor = "1 0.471 0.078 0.5",
                int fontSize = 12, TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0.0f)
            {
                Button(container, parentName, command, text, anchorMin, anchorMax, textColor, backgroundColor, fontSize, align, fadeIn);
            }

            public static void Button(CuiElementContainer container, string parentName, string command, string text,
                string anchorMin, string anchorMax, string textColor = "0.675 0.875 0.286 1", string backgroundColor = "0.235 0.294 0.149 1",
                int fontSize = 12, TextAnchor align = TextAnchor.MiddleCenter, float fadeIn = 0.0f)
            {
                container.Add(new CuiButton
                {
                    Button = { Command = command, Color = backgroundColor, FadeIn = fadeIn },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    Text =
                    {
                        Text = text, FontSize = fontSize, Font = "robotocondensed-bold.ttf", Align = align,
                        Color = textColor, VerticalOverflow = VerticalWrapMode.Truncate
                    }
                }, parentName);
            }

            public static string ScrollView(CuiElementContainer container, string parentName, string panelName, float scrollViewHeight,
                string anchorMin = "0 0", string anchorMax = "0.995 0.98", string backgroundColor = "0 0 0 0")
            {
                var scrollView = new CuiElement
                {
                    Name = panelName,
                    Parent = parentName,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            FadeIn = 0.2f,
                            Color = backgroundColor
                        },
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Elastic,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = false,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = new CuiRectTransform
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "1 1",
                                OffsetMin = $"0 {-scrollViewHeight}",
                                OffsetMax = "0 0"
                            },
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Invert = false,
                                AutoHide = false,

                                HandleColor = "1 0.184 0.082 1",
                                PressedColor = "1 0.184 0.082 0.2",
                                HighlightColor = "1 0.184 0.082 0.2", //Color when hovering over the scrollbar

                                HandleSprite = "assets/content/ui/ui.rounded.tga",
                                TrackSprite = "assets/content/ui/ui.background.tile.psd",

                                Size = 10f
                            }
                        },
                        new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                    }
                };
                container.Add(scrollView);

                return panelName;
            }

            public static string Row(CuiElementContainer container, string parentName, string panelName,
                string anchorMin, string anchorMax, string offsetMin = "0 0", string offsetMax = "1 1", string backgroundColor = "0 0 0 1",
                bool cursorEnabled = false)
            {
                container.Add(Row(panelName, anchorMin, anchorMax, offsetMin, offsetMax, backgroundColor, cursorEnabled).CuiPanel, parentName, panelName);
                return panelName;
            }

            public static RowCuiPanel Row(string panelName, string anchorMin, string anchorMax, string offsetMin = "0 0", string offsetMax = "1 1",
                string backgroundColor = "0 0 0 1", bool cursorEnabled = false)
            {
                var cuiPanel = new CuiPanel
                {
                    Image = { Color = backgroundColor },
                    RectTransform =
                    {
                        AnchorMin = anchorMin,
                        AnchorMax = anchorMax,
                        OffsetMin = offsetMin,
                        OffsetMax = offsetMax
                    },
                    CursorEnabled = cursorEnabled,
                };

                return new RowCuiPanel
                {
                    CuiPanel = cuiPanel,
                    PanelName = panelName
                };
            }

            public static void Text(CuiElementContainer container, string parentName, string text, string anchorMin,
                string anchorMax, int fontSize = 11, TextAnchor align = TextAnchor.MiddleLeft,
                string textColor = "1 1 1 1", string font = "robotocondensed-bold.ttf")
            {
                container.Add(new CuiLabel
                {
                    Text =
                    {
                        Text = text.ToUpper(), FontSize = fontSize, Font = font, Align = align, Color = textColor, VerticalOverflow = VerticalWrapMode.Truncate
                    },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
                }, parentName);
            }

            public static void Input(CuiElementContainer container, string parentName, string command, string text, string anchorMin, string anchorMax,
                int fontSize = 12, TextAnchor textAlign = TextAnchor.MiddleLeft, string textColor = "1 1 1 1", string backgroundColor = "0.133 0.133 0.133 0.8",
                int maxLength = 255, bool isPassword = false, bool autofocus = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = backgroundColor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
                }, parentName, parentName + ".Input.Panel");

                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = parentName + ".Input.Panel",
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Align = textAlign,
                            CharsLimit = maxLength,
                            Command = command,
                            Color = textColor,
                            FontSize = fontSize,
                            IsPassword = isPassword,
                            Autofocus = autofocus,
                            Text = text
                        },
                        new CuiRectTransformComponent { AnchorMin = "0 0", AnchorMax = "1 1" },
                        new CuiNeedsKeyboardComponent()
                    }
                });
            }

            public class RowCuiPanel
            {
                public CuiPanel CuiPanel;
                public string PanelName;
                public Action? Render;
            }
        }

        private static class GuiHelper
        {
            public static CuiElementContainer Container(string panelName, string backgroundColor, string anchorMin,
                string anchorMax, bool requireCursor = false, string parent = "Overlay")
            {
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = backgroundColor },
                            RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                            CursorEnabled = requireCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
            }

            public static CuiElementContainer ContainerWithOffset(string panelName, string backgroundColor,
                string anchorMin,
                string anchorMax, string offsetMin = "", string offsetMax = "", bool requireCursor = false,
                string parent = "Hud")
            {
                return new CuiElementContainer
                {
                    {
                        new CuiPanel
                        {
                            Image = { Color = backgroundColor },
                            RectTransform =
                            {
                                AnchorMin = anchorMin, AnchorMax = anchorMax, OffsetMin = offsetMin,
                                OffsetMax = offsetMax
                            },
                            CursorEnabled = requireCursor
                        },
                        new CuiElement().Parent = parent,
                        panelName
                    }
                };
            }

            public static string ParentPanel(CuiElementContainer container, string parentName, string panelName,
                string backgroundColor, string anchorMin, string anchorMax, bool cursorEnabled = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = backgroundColor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    CursorEnabled = cursorEnabled,
                }, parentName, panelName);
                return panelName;
            }

            public static string ParentPanelCircle(CuiElementContainer container, string parentName, string panelName,
                string backgroundColor, string anchorMin, string anchorMax, bool cursorEnabled = false)
            {
                container.Add(new CuiPanel
                {
                    Image = { Color = backgroundColor, Sprite = "assets/icons/circle_closed.png" },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    CursorEnabled = cursorEnabled,
                }, parentName, panelName);
                return panelName;
            }

            public static void Image(CuiElementContainer container, string parentName, int itemId, ulong skinId,
                string anchorMin, string anchorMax, string color = "1 1 1 1")
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Parent = parentName,
                    Components =
                    {
                        new CuiImageComponent { ItemId = itemId, SkinId = skinId, Color = color },
                        new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                    }
                });
            }

            public static void ImageUrl(CuiElementContainer container, string parentName, string image,
                string anchorMin, string anchorMax, string color = "1 1 1 1")
            {
                container.Add(new CuiElement
                {
                    Name = CuiHelper.GetGuid(),
                    Components =
                    {
                        new CuiRawImageComponent { Color = color, Png = image },
                        new CuiRectTransformComponent { AnchorMin = anchorMin, AnchorMax = anchorMax }
                    },
                    Parent = parentName
                });
            }

            public static void CloseButton(CuiElementContainer container, string parentName, string closePanelName,
                string buttonText, string color, string anchorMin, string anchorMax, int fontSize = 14,
                string font = "robotocondensed-bold.ttf", string textColor = "1 1 1 1", string command = "")
            {
                container.Add(new CuiButton
                {
                    Button = { Command = command, Close = closePanelName, Color = color },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    Text = { Text = buttonText, FontSize = fontSize, Font = font, Align = TextAnchor.MiddleCenter, Color = textColor }
                }, parentName);
            }

            public static void Button(CuiElementContainer container, string parentName, string command,
                string buttonText, string buttonColor, string anchorMin, string anchorMax, int fontSize = 14,
                string textColor = "1 1 1 1", TextAnchor align = TextAnchor.MiddleCenter,
                string font = "robotocondensed-bold.ttf")
            {
                container.Add(new CuiButton
                {
                    Button = { Command = command, Color = buttonColor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    Text = { Text = buttonText, FontSize = fontSize, Font = font, Align = align, Color = textColor }
                }, parentName);
            }

            public static void Button(CuiElementContainer container, string parentName, string command,
                string buttonText, string buttonColor, string anchorMin, string anchorMax, string sprite, string material, int fontSize = 14,
                string textColor = "1 1 1 1", TextAnchor align = TextAnchor.MiddleCenter, string font = "robotocondensed-bold.ttf")
            {
                container.Add(new CuiButton
                {
                    Button = { Command = command, Color = buttonColor, Sprite = sprite, Material = material },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    Text = { Text = buttonText, FontSize = fontSize, Font = font, Align = align, Color = textColor }
                }, parentName);
            }

            public static void ButtonCircle(CuiElementContainer container, string parentName, string command,
                string buttonText, string buttonColor, string anchorMin, string anchorMax, int fontSize = 14,
                string textColor = "1 1 1 1", TextAnchor align = TextAnchor.MiddleCenter,
                string font = "robotocondensed-bold.ttf")
            {
                container.Add(new CuiButton
                {
                    Button = { Command = command, Color = buttonColor, Sprite = "assets/icons/circle_closed.png" },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax },
                    Text = { Text = buttonText, FontSize = fontSize, Font = font, Align = align, Color = textColor }
                }, parentName);
            }

            public static void Text(CuiElementContainer container, string parentName, string text, string anchorMin,
                string anchorMax, int fontSize = 14, TextAnchor align = TextAnchor.LowerLeft,
                string textColor = "1 1 1 1", string font = "robotocondensed-bold.ttf")
            {
                container.Add(new CuiLabel
                {
                    Text = { Text = text, FontSize = fontSize, Font = font, Align = align, Color = textColor },
                    RectTransform = { AnchorMin = anchorMin, AnchorMax = anchorMax }
                }, parentName);
            }
        }

        private class Configuration
        {
            [JsonProperty("TimeZone")] public string TimeZone;

            [JsonProperty("ChatPrefix")] public string ChatPrefix;

            [JsonProperty("AllowAdminToBypassCodeLock (Allows admin to bypass Code Lock without using commands). Default False.)")]
            public bool AllowAdminToBypassCodeLock;

            // [JsonProperty("Disable Code Lock sound effects")]
            // public bool DisableCodeLockEffects;
            //
            // [JsonProperty("Disable effects if in NoClip or Vanish mode")]
            // public bool DisableCodeLockEffectsIfInNoClipOrVanish = true;

            [JsonProperty("Chat Command")] public HashSet<string> ChatCommand;

            [JsonProperty("Auto Lock Configuration")]
            public AutoLockConfiguration AutoLockConfiguration;

            [JsonProperty("Auto Closing Configuration")]
            public AutoClosingConfiguration AutoClosingConfiguration;

            [JsonProperty("Requires Building Privilege to place Code Locks. (Default: TRUE)")]
            public bool RequiresBuildingPrivilege = true;

            [JsonProperty("Requires Building Privilege to place Code Locks in unowned vehicles. (Default: FALSE)")]
            public bool RequiresBuildingPrivilegeToDeployCodeLockInUnownedVehicles;

            [JsonProperty("Allow deployment of Code Lock in vehicles owned by other players. (Default: FALSE)")]
            public bool AllowDeployCodeLockInOwnedPlayersVehicles;

            [JsonProperty("Allow deployment of Code Lock in unowned vehicles. (Default: TRUE)")]
            public bool AllowDeployCodeLockInUnownedVehicles = true;

            [JsonProperty("Allow pushing vehicles blocked by the Code Lock (Default: TRUE)")]
            public bool AllowPushVehiclesBlockedByCodeLock = true;

            [JsonProperty("Set player as owner when placing a Mining Quarry or Pump Jack (also static). (Default: TRUE)")]
            public bool SetPlayerAsOwnerWhenPlacedQuarry = true;


            [JsonProperty("Enable Lock")] public LockConfiguration LockConfiguration;

            public VersionNumber VersionNumber;

            public static Configuration CreateConfig()
            {
                return new Configuration
                {
                    TimeZone = DefaultTimeZone,
                    ChatPrefix = "UltimateLocker",
                    AllowAdminToBypassCodeLock = false,
                    // DisableCodeLockEffects = false,
                    // DisableCodeLockEffectsIfInNoClipOrVanish = true,

                    ChatCommand = new HashSet<string> { "ul", "ultimatelocker" },

                    AutoLockConfiguration = new AutoLockConfiguration
                    {
                        UseOnlyKeyLock = false,
                        ResyncPlayersDefaultSettings = false,
                        AutoLockPlayerConfiguration = new AutoLockPlayerConfiguration(),
                        AutoClosingPlayerConfiguration = new AutoClosingPlayerConfiguration()
                    },
                    AutoClosingConfiguration = new AutoClosingConfiguration(),

                    RequiresBuildingPrivilege = true,
                    RequiresBuildingPrivilegeToDeployCodeLockInUnownedVehicles = false,
                    AllowDeployCodeLockInOwnedPlayersVehicles = false,
                    AllowDeployCodeLockInUnownedVehicles = true,
                    AllowPushVehiclesBlockedByCodeLock = true,
                    SetPlayerAsOwnerWhenPlacedQuarry = true,

                    LockConfiguration = new LockConfiguration
                    {
                        Vehicles = new List<ItemLockData>
                        {
                            new()
                            {
                                ItemName = "Minicopter",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/minicopter/minicopter.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.15f, 0.7f, -0.1f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Scrap Transport Helicopter",
                                EnableLock = false,
                                PrefabName =
                                    "assets/content/vehicles/scrap heli carrier/scraptransporthelicopter.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-1.25f, 1.22f, 1.99f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Attack Helicopter",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.6f, 1.08f, 1.01f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Armored / Hot Air Balloon",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(1.45f, 0.9f, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Kayak",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/boats/kayak/kayak.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(0.0f, 0.16f, -1.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Row Boat",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/boats/rowboat/rowboat.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.83f, 0.51f, -0.57f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "RHIB",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/boats/rhib/rhib.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.68f, 2.00f, 0.7f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Tugboat",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/boats/tugboat/tugboat.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(0.065f, 6.8f, 4.12f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 60),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Submarinesolo",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/submarine/submarinesolo.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(0f, 1.85f, 0f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Submarine Duo",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/submarine/submarineduo.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.455f, 1.29f, 0.75f),
                                CodeLockRotation = Quaternion.Euler(0, 180, 10),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Horse",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/horse/ridablehorse.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(0.0f, 1.24f, 0.0f),
                                // CodeLockPosition = new Vector3(-0.22f, 1.05f, 0.1f), //OLD

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Tomaha Snowmobile",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.37f, 0.4f, 0.125f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Snowmobile",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/snowmobiles/snowmobile.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.205f, 0.59f, 0.4f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Sedan",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-1.09f, 0.79f, 0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "2 Module Car",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "3 Module Car",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "4 Module Car",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Car Chassis 2 Module Car",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Car Chassis 3 Module Car",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Car Chassis 4 Module Car",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Motorbike",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/bikes/motorbike.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.074f, 0.8f, 0.14f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Motorbike With Sidecar",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/bikes/motorbike_sidecar.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.72f, 0.52f, -0.65f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Pedal Bike",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/bikes/pedalbike.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.0f, 0.6f, -0.07f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Pedal Trike",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/bikes/pedaltrike.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.0f, 0.7f, -0.5f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Mounted Ballista",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/siegeweapons/ballista/ballista.entity.prefab",

                                LockCategory = LockCategory.Medieval,
                                EntityType = EntityType.VehicleMedieval,
                                CodeLockPosition = new Vector3(0.24f, 0.3f, 0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Battering Ram",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/siegeweapons/batteringram/batteringram.entity.prefab",

                                LockCategory = LockCategory.Medieval,
                                EntityType = EntityType.VehicleMedieval,
                                CodeLockPosition = new Vector3(2.4f, 2.0f, 1.64f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Catapult",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/siegeweapons/catapult/catapult.entity.prefab",

                                LockCategory = LockCategory.Medieval,
                                EntityType = EntityType.VehicleMedieval,
                                CodeLockPosition = new Vector3(1.124f, 2.74f, 0.9f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Siege Tower",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/siegeweapons/siegetower/siegetower.entity.prefab",

                                LockCategory = LockCategory.Medieval,
                                EntityType = EntityType.VehicleMedieval,
                                CodeLockPosition = new Vector3(1.27f, 2.4f, -0.4f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Ballista",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/siegeweapons/ballista/ballistagun.static.entity.prefab",

                                LockCategory = LockCategory.Medieval,
                                EntityType = EntityType.DeployableMedieval,
                                CodeLockPosition = new Vector3(0.0f, 0.6f, 0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            }
                        },
                        Deployables = new List<ItemLockData>
                        {
                            //TODO Sistemare il fatto che possa essere acceso con un Accensore
                            new()
                            {
                                ItemName = "Large Furnace",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/furnace.large/furnace.large.prefab",

                                LockCategory = LockCategory.Furnace,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.65f, 1.25f, -0.65f),
                                CodeLockRotation = Quaternion.Euler(0, 45, 0),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO Sistemare il fatto che possa essere acceso con un Accensore
                            new()
                            {
                                ItemName = "Furnace",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/furnace/furnace.prefab",

                                LockCategory = LockCategory.Furnace,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.02f, 0.3f, 0.5f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO Sistemare il fatto che possa essere acceso con un Accensore
                            new()
                            {
                                ItemName = "Legacy Furnace",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/legacyfurnace/legacy_furnace.prefab",

                                LockCategory = LockCategory.Furnace,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.02f, 1.2f, 0.32f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Refinery",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab",

                                LockCategory = LockCategory.Furnace,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.01f, 1.25f, -0.6f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Electric Furnace",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/electricfurnace/electricfurnace.deployed.prefab",

                                LockCategory = LockCategory.Furnace,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.02f, 0.2f, 0.32f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Stone Fireplace",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/fireplace/fireplace.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                CodeLockPosition = new Vector3(-0.02f, 0.2f, 0.46f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "BBQ",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/bbq/bbq.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.3f, 0.75f, 0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Hobo Barrel",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/twitch/hobobarrel/hobobarrel.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.75f, 0.34f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Storage Barrel B",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_b.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.85f, 0.42f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Storage Barrel C",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.60f, 0.45f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 350),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Abyss Horizontal Storage Tank",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/large wood storage/skins/abyss_dlc_large_wood_box/abyss_dlc_storage_horizontal/abyss_barrel_horizontal.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.60f, 0.42f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 340),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Abyss Vertical Storage Tank",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/large wood storage/skins/abyss_dlc_large_wood_box/abyss_dlc_storage_vertical/abyss_barrel_vertical.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.58f, 0.42f),
                                CodeLockRotation = Quaternion.Euler(0, 92, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Bamboo Barrel",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/large wood storage/skins/jungle_dlc_large_wood_box/jungle_dlc_storage_vertical/bamboo_barrel.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.8f, 0.38f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 346),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Wicker Barrel",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/large wood storage/skins/jungle_dlc_large_wood_box/jungle_dlc_storage_horizontal/wicker_barrel.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.62f, 0.44f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 354),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "RHIB Storage",
                                EnableLock = false,
                                PrefabName = "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.38f, 0.30f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Metal Shop Front",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.metal.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.0f, 0.8f, -0.6f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Dropbox",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/dropbox/dropbox.deployed.prefab",

                                LockCategory = LockCategory.StorageContainer,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.18f, 0.23f, -0.3f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Mail Box",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab",

                                LockCategory = LockCategory.StorageContainer,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.08f, 1.17f, 0.20f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Vending Machine",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab",

                                LockCategory = LockCategory.VendingMachine,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0, 1.0f, -0.22f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Computer Station",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/computerstation/computerstation.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.61f, 0.54f, 0.42f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Twitch Rivals Desk",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/misc/twitch/twitch_rivals_2023_desk/twitchrivals2023_desk.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.74f, 0.56f, 0.5f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Mixing Table",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab",

                                LockCategory = LockCategory.MixingTable,
                                CodeLockPosition = new Vector3(-0.50f, 0.62f, 0.432f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Composter",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/composter/composter.prefab",

                                LockCategory = LockCategory.Composter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0f, 1.3f, 0.6f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Small Planter Box",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/planters/planter.small.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(1.22f, 0.45f, 0.28f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Large Planter Box",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/planters/planter.large.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(1.22f, 0.45f, 1.21f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Triangle Planter Box",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/planters/planter.triangle.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.38f, 0.45f, -0.9f),
                                CodeLockRotation = Quaternion.Euler(0, 0, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Minecart Planter",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/misc/decor_dlc/minecart planter/minecart.planter.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.74f, 0.65f, 0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Bath Tub Planter",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/misc/decor_dlc/bath tub planter/bathtub.planter.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.68f, 0.60f, 0.436f),
                                CodeLockRotation = Quaternion.Euler(0, 180, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Rail Road Planter",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/misc/decor_dlc/rail road planter/railroadplanter.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(1.22f, 0.45f, 1.26f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Triangle Rail Road Planter",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/decor_dlc/rail road planter/triangle_railroad_planter.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.34f, 0.32f, -0.9f),
                                CodeLockRotation = Quaternion.Euler(0, 0, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Single Plant Pot",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/plant pots/plantpot.single.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.2f, 0.16f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 8),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Beehive",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/beehive/beehive.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.8f, 0.24f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Chicken Coop",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/chickencoop/chickencoop.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.2f, 1.3f, -0.62f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO Sistemare che il cavallo non possa mangiare
                            new()
                            {
                                ItemName = "Hitch & Trough",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/hitch & trough/hitchtrough.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0f, 0.4f, 0.3f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Small Water Catcher",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/water catcher/water_catcher_small.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableLiquidContainer,
                                CodeLockPosition = new Vector3(0.0f, 0.8f, 0.36f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Large Water Catcher",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/water catcher/water_catcher_large.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableLiquidContainer,
                                CodeLockPosition = new Vector3(0.0f, 1.5f, 0.84f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Water Barrel",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/liquidbarrel/waterbarrel.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableLiquidContainer,
                                CodeLockPosition = new Vector3(0.0f, 1.5f, 0.40f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Powered Water Purifier",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/poweredwaterpurifier/poweredwaterpurifier.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableLiquidContainer,
                                CodeLockPosition = new Vector3(0.0f, 1.5f, -0.04f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Fluid Switch & Pump",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/playerioents/fluidswitch/fluidswitch.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectricSwitch,
                                CodeLockPosition = new Vector3(0.2f, -0.1f, -0.12f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Repair Bench",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/repair bench/repairbench_deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(-0.80f, 1.0f, -0.36f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Research Table",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/research table/researchtable_deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.6f, 0.96f, 0.24f),
                                CodeLockRotation = Quaternion.Euler(0, 270, 50),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Workbench Level 1",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableWorkbench,
                                CodeLockPosition = new Vector3(0.56f, 0.82f, 0.26f),
                                CodeLockRotation = Quaternion.Euler(0, 270, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Workbench Level 2",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableWorkbench,
                                CodeLockPosition = new Vector3(-0.28f, 0.82f, 0.26f),
                                CodeLockRotation = Quaternion.Euler(0, 270, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Workbench Level 3",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableWorkbench,
                                CodeLockPosition = new Vector3(-0.98f, 0.74f, 0.38f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Cooking Workbench",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/cookingworkbench/cookingworkbench.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,

                                CodeLockPosition = new Vector3(0.0f, 0.54f, 0.26f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),
                                // CodeLockPosition = new Vector3(-0.61f, 1.28f, 0.0f),
                                // CodeLockRotation = Quaternion.Euler(0, 270, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Engineering Workbench",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/io research table/io.table.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.5f, 0.8f, 0.24f),
                                CodeLockRotation = Quaternion.Euler(0, 270, 45),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Hopper",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/hopper/hopper.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.48f, 0.16f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Button",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/playerioents/button/button.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectricalButton,
                                CodeLockPosition = new Vector3(0.12f, -0.24f, -0.03f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Switch",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/playerioents/simpleswitch/switch.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectricSwitch,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, -0.03f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Smart Switch",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/app/smartswitch/smartswitch.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectricSwitch,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, -0.03f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Timer",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/playerioents/timers/timer.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectricSwitch,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, -0.03f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Small Generator",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/generators/fuel generator/small_fuel_generator.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectricGenerator,
                                CodeLockPosition = new Vector3(0.09f, 0.54f, 0.1f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "SAM Site",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab",

                                LockCategory = LockCategory.SamSite,
                                EntityType = EntityType.DeployableTurret,
                                CodeLockPosition = new Vector3(-0.4f, 0.8f, -0.30f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Auto Turret",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab",

                                LockCategory = LockCategory.AutoTurret,
                                EntityType = EntityType.DeployableTurret,
                                CodeLockPosition = new Vector3(-0.29f, 0.38f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Flame Turret",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/npc/flame turret/flameturret.deployed.prefab",

                                LockCategory = LockCategory.Trap,
                                EntityType = EntityType.DeployableTraps,
                                CodeLockPosition = new Vector3(0.0f, 0.16f, -0.10f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Shotgun Trap",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab",

                                LockCategory = LockCategory.Trap,
                                EntityType = EntityType.DeployableTraps,
                                CodeLockPosition = new Vector3(0.0f, 0.1f, -0.3f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Modular Car Lift",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/modular car lift/electrical.modularcarlift.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableModularCarLift,
                                CodeLockPosition = new Vector3(0.0f, 0.9f, -2.0f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Snow Machine",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/xmas/snow_machine/models/snowmachine.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableSnowFogMachine,
                                CodeLockPosition = new Vector3(0.0f, 0.5f, -0.18f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 70),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Fogger-3000",
                                EnableLock = false,
                                PrefabName = "assets/content/props/fog machine/fogmachine.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableSnowFogMachine,
                                CodeLockPosition = new Vector3(-0.01f, 0.14f, 0.04f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Elevator",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/elevator/elevator_lift.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElevator,
                                CodeLockPosition = new Vector3(-1.2f, -0.1f, -0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Mining Quarry",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/quarry/mining_quarry.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableMiningQuarry,
                                CodeLockPosition = new Vector3(-2.36f, 4.5f, 1.66f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Mining Quarry Static",
                                EnableLock = false,
                                PrefabName = "assets/bundled/prefabs/static/miningquarry_static.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableMiningQuarryStatic,
                                CodeLockPosition = new Vector3(-2.36f, 4.5f, 1.66f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Pump Jack",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/oil jack/mining.pumpjack.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableMiningPumpJack,
                                CodeLockPosition = new Vector3(3.26f, 4.5f, -1.36f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Pump Jack Static",
                                EnableLock = false,
                                PrefabName = "assets/bundled/prefabs/static/pumpjack-static.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableMiningPumpJackStatic,
                                CodeLockPosition = new Vector3(3.26f, 4.5f, -1.36f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Tall Weapon Rack",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_tall.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-0.61f, 0.96f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Horizontal Weapon Rack",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/weaponracks/weaponrack_horizontal.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-0.60f, 0.58f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Wide Weapon Rack",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_wide.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-1.14f, 0.58f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Weapon Rack Stand",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_stand.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-0.46f, 1.03f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Frontier Bolts Single Item Rack",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_single1.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-0.5f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Frontier Horseshoe Single Item Rack",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_single2.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-0.5f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Frontier Horns Single Item Rack",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/weaponracks/weaponrack_single3.deployed.prefab",

                                LockCategory = LockCategory.WeaponRack,
                                EntityType = EntityType.DeployableWeaponRack,
                                CodeLockPosition = new Vector3(-0.5f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Small Stash",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/small stash/small_stash_deployed.prefab",

                                LockCategory = LockCategory.Stash,
                                EntityType = EntityType.DeployableStash,
                                CodeLockPosition = new Vector3(0.0f, 0.08f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Chippy Arcade Game",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/chippy arcade/chippyarcademachine.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableItems,
                                CodeLockPosition = new Vector3(0.0f, 0.48f, 0.26f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Strobe Light",
                                EnableLock = false,
                                PrefabName = "assets/content/props/strobe light/strobelight.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableMisc,
                                CodeLockPosition = new Vector3(-0.0f, -0.05f, -0.1f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 270.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Laser Light",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/voiceaudio/laserlight/laserlight.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableFun,
                                CodeLockPosition = new Vector3(0.1f, -0.1f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 180.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Sound Light",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/voiceaudio/soundlight/soundlight.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableMisc,
                                CodeLockPosition = new Vector3(0.0f, -0.1f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 180.0f),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO DA SISTEMARE, NON BLOCCA LE MODIFICHE
                            new()
                            {
                                ItemName = "Small Neon Sign",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/xmas/neon_sign/sign.neon.125x125.prefab",

                                LockCategory = LockCategory.NeonSign,
                                EntityType = EntityType.DeployableElectricalNeonSign,
                                CodeLockPosition = new Vector3(-0.34f, -0.06f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO DA SISTEMARE, NON BLOCCA LE MODIFICHE
                            new()
                            {
                                ItemName = "Medium Neon Sign",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/xmas/neon_sign/sign.neon.125x215.prefab",

                                LockCategory = LockCategory.NeonSign,
                                EntityType = EntityType.DeployableElectricalNeonSign,
                                CodeLockPosition = new Vector3(-0.34f, -0.06f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO DA SISTEMARE, NON BLOCCA LE MODIFICHE
                            new()
                            {
                                ItemName = "Large Neon Sign",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.prefab",

                                LockCategory = LockCategory.NeonSign,
                                EntityType = EntityType.DeployableElectricalNeonSign,
                                CodeLockPosition = new Vector3(-0.34f, -0.06f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO DA SISTEMARE, NON BLOCCA LE MODIFICHE
                            new()
                            {
                                ItemName = "Medium Animated Neon Sign",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/xmas/neon_sign/sign.neon.125x215.animated.prefab",

                                LockCategory = LockCategory.NeonSign,
                                EntityType = EntityType.DeployableElectricalNeonSign,
                                CodeLockPosition = new Vector3(-0.34f, -0.06f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO DA SISTEMARE, NON BLOCCA LE MODIFICHE
                            new()
                            {
                                ItemName = "Large Animated Neon Sign",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/xmas/neon_sign/sign.neon.xl.animated.prefab",

                                LockCategory = LockCategory.NeonSign,
                                EntityType = EntityType.DeployableElectricalNeonSign,
                                CodeLockPosition = new Vector3(-0.34f, -0.06f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Search Light",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/search light/searchlight.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectrical,
                                CodeLockPosition = new Vector3(0.0f, 0.1f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "CCTV Camera",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/cctvcamera/cctv_deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableResources,
                                CodeLockPosition = new Vector3(0.0f, 0.16f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "PTZ CCTV Camera",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/ptz security camera/ptz_cctv_deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectrical,
                                CodeLockPosition = new Vector3(0.08f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 180.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "RF Broadcaster",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/gates/rfbroadcaster/rfbroadcaster.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectrical,
                                CodeLockPosition = new Vector3(0.05f, 0.25f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "RF Receiver",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/gates/rfreceiver/rfreceiver.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElectrical,
                                CodeLockPosition = new Vector3(0.0f, -0.06f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Industrial Conveyor",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/industrialconveyor/industrialconveyor.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableIndustrial,
                                CodeLockPosition = new Vector3(0.0f, -0.16f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 180.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Industrial Crafter",
                                EnableLock = false,
                                PrefabName =
                                    "assets/prefabs/deployable/playerioents/industrialcrafter/industrialcrafter.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableIndustrial,
                                CodeLockPosition = new Vector3(-0.1f, 0.16f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Wheelbarrow Piano",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/instruments/piano/piano.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableFun,
                                CodeLockPosition = new Vector3(0.582f, 0.82f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Junkyard Drum Kit",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/instruments/drumkit/drumkit.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableFun,
                                CodeLockPosition = new Vector3(0.5f, 0.46f, 0.26f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Boom Box",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/voiceaudio/boombox/boombox.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableFun,
                                CodeLockPosition = new Vector3(0.0f, 0.66f, -0.01f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 270.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Chinese Lantern",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, -0.4f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Chinese Lantern White",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/chinesenewyear/chineselantern/chineselantern_white.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, -0.4f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Tuna Can Lamp",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/tuna can wall lamp/tunalight.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 90.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Lantern",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/lantern/lantern.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Camp Fire",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/campfire/campfire.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Cursed Cauldron",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/halloween/cursed_cauldron/cursedcauldron.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Skull Fire Pit",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/misc/halloween/skull_fire_pit/skull_fire_pit.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.0f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Jack O Lantern Angry",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/jack o lantern/jackolantern.angry.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.2f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Jack O Lantern Happy",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.2f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Wood Shutters",
                                EnableLock = false,
                                PrefabName = "assets/prefabs/building/wall.window.shutter/shutter.wood.a.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableConstruction,
                                CodeLockPosition = new Vector3(0.18f, 0.26f, 0.96f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 0.0f, 0.0f),

                                RequiredPermission = new[] { "" }
                            }
                        }
                    },
                    VersionNumber = OxideMod.Version
                };
            }
        }

        private class AutoLockConfiguration
        {
            [JsonProperty("Chat Command")] public HashSet<string> ChatCommand = new() { "autolock", "codelock" };

            [JsonProperty("Add Lock manually - Chat Command")]
            public string AddLockerChatCommand = "locker";

            [JsonProperty("Use only Key Lock. [true] to allow only Key Lock. [false] to allow both Key Lock and Code Lock. (Default: FALSE)")]
            public bool UseOnlyKeyLock = false;

            [JsonProperty(
                "RESYNC DEFAULT SETTINGS for all players. If set to [true], resets all player settings to the default settings configured. Once the settings are reset, the parameter will automatically be reset to false. (Default: FALSE)")]
            public bool ResyncPlayersDefaultSettings = false;

            [JsonProperty(
                "FORCE SHARE LOCKS WITH CLAN/TEAM MEMBERS. [true] to force all players to share locks with clan/team members. [false] to force all players to NOT share locks with clan/team members. [null] to use the player's default settings by UI. (Default: null)")]
            public bool? ForceShareLocksWithClanTeam = null;

            [JsonProperty("Player default settings")] //TODO da rinominare in: "Player Auto Lock default settings"
            public AutoLockPlayerConfiguration AutoLockPlayerConfiguration;

            [JsonProperty("Player Auto Closing default settings")]
            public AutoClosingPlayerConfiguration AutoClosingPlayerConfiguration;
        }

        private class AutoLockPlayerConfiguration
        {
            [JsonProperty("Allow automatic lock placement by default. (Default: FALSE)")]
            public bool EnableAutoLockOnPlacementByDefault = false;

            [JsonProperty("Allow automatic placement of Key Locks if there are no Code Locks in the inventory. (Default: TRUE)")]
            public bool EnableAlsoUseKeyLockByDefault = true;

            [JsonProperty("Allow Guest Code by default. (Default: FALSE)")]
            public bool EnableGuestCodeByDefault;

            [JsonProperty("Allow automatic Share locks with clan/team members by default. (Default: TRUE)")]
            public bool EnableAutoShareLocksWithClanTeamByDefault = true;

            [JsonProperty("Enable streamer mode by default. (Default: FALSE)")]
            public bool EnableStreamerModeByDefault;

            [JsonProperty("Allow automatic lock on Doors by default. (Default: TRUE)")]
            public bool EnableDoorsLockByDefault = true;

            [JsonProperty("Allow automatic lock on Windows by default. (Default: TRUE)")]
            public bool EnableWindowsLockByDefault = true;

            [JsonProperty("Allow automatic lock on Boxes by default. (Default: FALSE)")]
            public bool EnableBoxesLockByDefault;

            [JsonProperty("Allow automatic lock on Storage Container by default. (Default: FALSE)")]
            public bool EnableStorageContainerLockByDefault;

            [JsonProperty("Allow automatic lock on Lockers by default. (Default: TRUE)")]
            public bool EnableLockersLockByDefault = true;

            [JsonProperty("Allow automatic lock on TC (Cupboards) by default. (Default: TRUE)")]
            public bool EnableCupboardsLockByDefault = true;

            [JsonProperty("Allow automatic lock on Vehicle by default. (Default: TRUE)")]
            public bool EnableVehicleLockByDefault = true;

            [JsonProperty("Allow automatic lock closing when dismount of the vehicle by default. (Default: TRUE)")]
            public bool EnableVehicleLockAutoClosingOnDismountByDefault = true;

            [JsonProperty("Allow automatic lock on Medieval entity by default. (Default: FALSE)")]
            public bool EnableMedievalEntityLockByDefault;

            [JsonProperty("Allow automatic lock on Furnace by default. (Default: FALSE)")]
            public bool EnableFurnaceLockByDefault;

            [JsonProperty("Allow automatic lock on Vending Machine by default. (Default: FALSE)")]
            public bool EnableVendingMachineLockByDefault;

            [JsonProperty("Allow automatic lock on Composter by default. (Default: FALSE)")]
            public bool EnableComposterLockByDefault;

            [JsonProperty("Allow automatic lock on Mixing Table by default. (Default: FALSE)")]
            public bool EnableMixingTableLockByDefault;

            [JsonProperty("Allow automatic lock on Planter by default. (Default: FALSE)")]
            public bool EnablePlanterLockByDefault;

            [JsonProperty("Allow automatic lock on Auto Turret by default. (Default: FALSE)")]
            public bool EnableAutoTurretLockByDefault;

            [JsonProperty("Allow automatic lock on SAM Site by default. (Default: FALSE)")]
            public bool EnableSamSiteLockByDefault;

            [JsonProperty("Allow automatic lock on Traps by default. (Default: FALSE)")]
            public bool EnableTrapsLockByDefault;

            [JsonProperty("Allow automatic lock on Weapon Rack by default. (Default: TRUE)")]
            public bool EnableWeaponRackLockByDefault = true;

            [JsonProperty("Allow automatic lock on Stash by default. (Default: FALSE)")]
            public bool EnableStashLockByDefault;

            [JsonProperty("Allow automatic lock on Neon Sign by default. (Default: FALSE)")]
            public bool EnableNeonSignLockByDefault;

            [JsonProperty("Allow automatic lock on Other Lockable Entities by default. (Default: FALSE)")]
            public bool EnableOtherLockableEntitiesLockByDefault;

            [JsonProperty("Allow automatic lock on Other Custom Entities by default. (Default: FALSE)")]
            public bool EnableOtherCustomEntitiesLockByDefault;
        }

        private class AutoClosingPlayerConfiguration
        {
            [JsonProperty("Allow automatic closing by default. (Default: FALSE)")]
            public bool EnableAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Door by default. (Default: FALSE)")]
            public bool EnableDoorAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Double Door by default. (Default: FALSE)")]
            public bool EnableDoubleDoorAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Window by default. (Default: FALSE)")]
            public bool EnableWindowAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Garage by default. (Default: FALSE)")]
            public bool EnableGarageAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Ladder Hatch by default. (Default: FALSE)")]
            public bool EnableLadderHatchAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of External Gate by default. (Default: FALSE)")]
            public bool EnableExternalGateAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Fence Gate by default. (Default: FALSE)")]
            public bool EnableFenceGateAutoClosingByDefault = false;

            [JsonProperty("Allow automatic closing of Legacy Wood Shelter Door by default. (Default: FALSE)")]
            public bool EnableLegacyWoodShelterDoorAutoClosingByDefault = false;
        }

        private class AutoClosingConfiguration
        {
            [JsonProperty("Add Door Closer manually - Chat Command")]
            public string AddCloserChatCommand = "closer";

            [JsonProperty("Remove Door Closer manually - Chat Command")]
            public string RemoveCloserChatCommand = "uncloser";

            [JsonProperty("Player Can Pickup Door Closer. (Default: TRUE)")]
            public bool CanPickupDoorCloser = true;

            [JsonProperty("Player Can Remove Door Closer? (If set to FALSE, only allows removal via command /uncloser). (Default: TRUE)")]
            public bool CanRemoveDoorCloser = true;

            [JsonProperty("Enable automatic closing of Door. (Default: FALSE)")]
            public bool EnableDoorAutoClosing = false;

            [JsonProperty("Enable automatic closing of Double Door. (Default: FALSE)")]
            public bool EnableDoubleDoorAutoClosing = false;

            [JsonProperty("Enable automatic closing of Window. (Default: FALSE)")]
            public bool EnableWindowAutoClosing = false;

            [JsonProperty("Enable automatic closing of Garage. (Default: FALSE)")]
            public bool EnableGarageAutoClosing = false;

            [JsonProperty("Enable automatic closing of Ladder Hatch. (Default: FALSE)")]
            public bool EnableLadderHatchAutoClosing = false;

            [JsonProperty("Enable automatic closing of External Gate. (Default: FALSE)")]
            public bool EnableExternalGateAutoClosing = false;

            [JsonProperty("Enable automatic closing of Fence Gate. (Default: FALSE)")]
            public bool EnableFenceGateAutoClosing = false;

            [JsonProperty("Enable automatic closing of Legacy Wood Shelter Door. (Default: FALSE)")]
            public bool EnableLegacyWoodShelterDoorAutoClosing = false;


            [JsonProperty("Minimum Closing Delay Time. (Default: 10 seconds)")]
            public int MinimumClosingDelayTime = 10;

            [JsonProperty("Maximum Closing Delay Time. (Default: 60 seconds)")]
            public int MaximumClosingDelayTime = 60;

            [JsonProperty("Default Closing Delay Time. (Default: 30 seconds)")]
            public int DefaultClosingDelayTime = 30;
        }

        private class Data
        {
            [JsonProperty("Auto Lock Player Settings")]
            //SteamID : AutoLockPlayerSettingsData
            public readonly Dictionary<ulong, AutoLockPlayerSettingsData> AutoLockPlayerSettingsData = new();

            [JsonProperty("Auto Closing Player Settings")]
            //SteamID : AutoClosingPlayerSettingsData
            public readonly Dictionary<ulong, AutoClosingPlayerSettingsData> AutoClosingPlayerSettingsData = new();

            [JsonProperty("CodeLockItems")]
            //SteamID : List<CodeLockItemsData>>
            public readonly Dictionary<ulong, List<CodeLockItemsData>> CodeLockItemsData = new();

            [JsonProperty("DoorCloserCustomTimeItemsData")]
            //SteamID : List<DoorCloserItemData>>
            public readonly Dictionary<ulong, List<DoorCloserItemData>> DoorCloserCustomTimeItemsData = new();
        }

        private class AutoLockPlayerSettingsData
        {
            [JsonProperty("SteamID")] public ulong SteamID;

            [JsonProperty("Code Lock")] public string CodeLock;

            [JsonProperty("Guest Code Lock")] public string GuestCodeLock;

            [JsonProperty("Is automatic lock placement enabled.")]
            public bool AutoLockOnPlacementEnabled;

            [JsonProperty("Use Key Locks if there are no Code Locks in inventory.")]
            public bool AlsoUseKeyLockEnabled;

            [JsonProperty("Is automatic Guest Code enabled.")]
            public bool GuestCodeEnabled;

            [JsonProperty("Is automatic Share locks with clan/team members enabled.")]
            public bool ShareLocksWithClanTeamEnabled = true;

            [JsonProperty("Is streamer mode enabled.")]
            public bool StreamerModeEnabled;

            [JsonProperty("Is automatic lock on Doors enabled.")]
            public bool DoorsLockEnabled;

            [JsonProperty("Is automatic lock on Windows enabled.")]
            public bool WindowsLockEnabled;

            [JsonProperty("Is automatic lock on Boxes enabled.")]
            public bool BoxesLockEnabled;

            [JsonProperty("Is automatic lock on Storage Container enabled.")]
            public bool StorageContainerLockEnabled;

            [JsonProperty("Is automatic lock on Lockers enabled.")]
            public bool LockersLockEnabled;

            [JsonProperty("Is automatic lock on TC (Cupboards) enabled.")]
            public bool CupboardsLockEnabled;

            [JsonProperty("Is automatic lock on Vehicles enabled.")]
            public bool VehicleLockEnabled;

            [JsonProperty("Is automatic lock closing when dismount of the vehicle enabled.")]
            public bool VehicleLockAutoClosingOnDismountEnabled;

            [JsonProperty("Is automatic lock on Medieval entity enabled.")]
            public bool MedievalEntityLockEnabled;

            [JsonProperty("Is automatic lock on Furnace enabled.")]
            public bool FurnaceLockEnabled;

            [JsonProperty("Is automatic lock on Vending Machine enabled.")]
            public bool VendingMachineLockEnabled;

            [JsonProperty("Is automatic lock on Composter enabled.")]
            public bool ComposterLockEnabled;

            [JsonProperty("Is automatic lock on Mixing Table enabled.")]
            public bool MixingTableLockEnabled;

            [JsonProperty("Is automatic lock on Planter enabled.")]
            public bool PlanterLockEnabled;

            [JsonProperty("Is automatic lock on Auto Turret enabled.")]
            public bool AutoTurretLockEnabled;

            [JsonProperty("Is automatic lock on SAM Site enabled.")]
            public bool SamSiteLockEnabled;

            [JsonProperty("Is automatic lock on Traps enabled.")]
            public bool TrapsLockEnabled;

            [JsonProperty("Is automatic lock on Weapon Rack enabled.")]
            public bool WeaponRackLockEnabled;

            [JsonProperty("Is automatic lock on Stash enabled.")]
            public bool StashLockEnabled;

            [JsonProperty("Is automatic lock on Neon Sign enabled.")]
            public bool NeonSignLockEnabled;

            [JsonProperty("Is automatic lock on Other Lockable Entities enabled.")]
            public bool OtherLockableEntitiesLockEnabled;

            [JsonProperty("Is automatic lock on Other Custom Entities enabled.")]
            public bool OtherCustomEntitiesLockEnabled;
        }

        private class AutoClosingPlayerSettingsData
        {
            [JsonProperty("SteamID")] public ulong SteamID;

            [JsonProperty("Is automatic closing enabled.")]
            public bool AutoClosingEnabled;

            [JsonProperty("Is automatic closing of Door enabled.")]
            public bool DoorAutoClosingEnabled;

            [JsonProperty("Is automatic closing of Double Door enabled.")]
            public bool DoubleDoorAutoClosingEnabled;

            [JsonProperty("Is automatic closing of Windows enabled.")]
            public bool WindowAutoClosingEnabled;

            [JsonProperty("Is automatic closing of Garage enabled.")]
            public bool GarageAutoClosingEnabled;

            [JsonProperty("Is automatic closing of Ladder Hatch enabled.")]
            public bool LadderHatchAutoClosingEnabled;

            [JsonProperty("Is automatic closing of External Gate enabled.")]
            public bool ExternalGateAutoClosingEnabled;

            [JsonProperty("Is automatic closing of Fence Gate enabled.")]
            public bool FenceGateAutoClosingEnabled;

            [JsonProperty("Is automatic closing of Legacy Wood Shelter Door enabled.")]
            public bool LegacyWoodShelterDoorAutoClosingEnabled;


            [JsonProperty("Door Closing Delay.")] public int DoorClosingDelay;

            [JsonProperty("Double Door Closing Delay.")]
            public int DoubleDoorClosingDelay;

            [JsonProperty("Window Closing Delay.")]
            public int WindowClosingDelay;

            [JsonProperty("Garage Closing Delay.")]
            public int GarageClosingDelay;

            [JsonProperty("Ladder Hatch Closing Delay.")]
            public int LadderHatchClosingDelay;

            [JsonProperty("External Gate Closing Delay.")]
            public int ExternalGateClosingDelay;

            [JsonProperty("Fence Gate Closing Delay.")]
            public int FenceGateClosingDelay;

            [JsonProperty("Legacy Wood Shelter Door Closing Delay.")]
            public int LegacyWoodShelterDoorClosingDelay;
        }

        private class CodeLockItemsData
        {
            [JsonProperty("owner_id")] public ulong OwnerId;
            [JsonProperty("player_name")] public string PlayerName;
            [JsonProperty("network_Id")] public ulong NetId;
            [JsonProperty("shortPrefabName")] public string ShortPrefabName;

            [JsonProperty("ParentEntity_Network_Id")]
            public ulong? ParentEntityNetId;

            [JsonProperty("ParentEntity")] public string ParentEntity;
            [JsonProperty("deploy_coordinate")] public string DeployCoordinate;
            [JsonProperty("teleport_coordinate")] public string TeleportCoordinate;
            [JsonProperty("deploy_date")] public DateTime DeployDate;
            [JsonProperty("deploy_timestamp")] public ulong DeployTimestamp;
        }

        private class LockConfiguration
        {
            [JsonProperty("Vehicles")] public List<ItemLockData> Vehicles;
            [JsonProperty("Deployables")] public List<ItemLockData> Deployables;
        }

        private class DoorCloserItemData
        {
            [JsonProperty("owner_id")] public ulong OwnerId;
            [JsonProperty("player_name")] public string PlayerName;
            [JsonProperty("network_Id")] public ulong NetId;
            [JsonProperty("delay_time")] public int DelayTime;
            [JsonProperty("ParentEntity")] public string ParentEntity;
            [JsonProperty("deploy_coordinate")] public string DeployCoordinate;
            [JsonProperty("teleport_coordinate")] public string TeleportCoordinate;
            [JsonProperty("deploy_date")] public DateTime DeployDate;
            [JsonProperty("deploy_timestamp")] public ulong DeployTimestamp;
        }

        private class ItemLockData
        {
            [JsonProperty("ItemName")] public string ItemName;
            [JsonProperty("EnableLock")] public bool EnableLock;
            [JsonProperty("PrefabName")] public string PrefabName;

            [JsonIgnore] public LockCategory LockCategory;

            //TODO DA RIMUOVERE
            [JsonIgnore] public EntityType EntityType;
            [JsonIgnore] public Vector3 CodeLockPosition;
            [JsonIgnore] public Quaternion CodeLockRotation;

            [JsonProperty("RequiredPermission")] public string[] RequiredPermission = { "" };
        }

        private class CodeLockData
        {
            public ModularCarCodeLock? ModularCarCodeLock;
            public BaseLock? BaseLock;
            public BaseEntity? BaseEntity;

            public bool IsModularCar()
            {
                if (ModularCarCodeLock == null && BaseLock == null) return false;
                return ModularCarCodeLock != null;
            }

            public BasePlayer? GetOwnerPlayer()
            {
                var userID = GetOwnerID();
                if (!userID.HasValue || !userID.Value.IsSteamId()) return null;

                return BasePlayer.FindAwakeOrSleeping(userID.ToString());
            }

            public ulong? GetOwnerID()
            {
                var baseEntity = GetCodeLockEntity();
                if (baseEntity == null || !baseEntity.OwnerID.IsSteamId()) return null;

                return baseEntity.OwnerID;
            }

            public BaseEntity? GetCodeLockEntity()
            {
                if (ModularCarCodeLock == null && BaseLock == null) return null;

                if (BaseLock != null)
                    return BaseLock;

                if (BaseEntity != null && BaseEntity is ModularCar modularCar)
                    return modularCar;

                return null;
            }

            public string? GetCodeLockCode()
            {
                if (IsModularCar() && ModularCarCodeLock is { HasALock: true })
                {
                    return ModularCarCodeLock.Code;
                }

                if (!IsModularCar() && BaseLock != null && BaseLock is CodeLock { hasCode: true } codeLock)
                {
                    return codeLock.code;
                }

                return null;
            }

            public string? GetCodeLockGuestCode()
            {
                if (!IsModularCar() && BaseLock != null && BaseLock is CodeLock { hasGuestCode: true } codeLock)
                {
                    return codeLock.guestCode;
                }

                return null;
            }

            public bool IsLocked()
            {
                if (ModularCarCodeLock == null && BaseLock == null) return false;

                if (BaseLock != null)
                    return BaseLock.IsLocked();

                if (BaseEntity != null && BaseEntity is ModularCar modularCar)
                    return modularCar.HasFlag(ModularCarCodeLock.FLAG_CENTRAL_LOCKING);

                return false;
            }

            public void Lock()
            {
                LockUnlock(true);
            }

            public void Unlock()
            {
                LockUnlock(false);
            }

            private void LockUnlock(bool setLocked)
            {
                if (IsModularCar() && BaseEntity is ModularCar modularCar)
                {
                    modularCar.SetFlag(ModularCarCodeLock.FLAG_CENTRAL_LOCKING, setLocked);
                    return;
                }

                if (BaseLock != null)
                {
                    BaseLock.SetFlag(BaseEntity.Flags.Locked, setLocked);
                    return;
                }
            }

            public void RemoveLock(BasePlayer? player)
            {
                if (player == null) return;
                if (IsModularCar() && ModularCarCodeLock is { HasALock: true })
                {
                    self?.EntityRemoved(null, ModularCarCodeLock.owner, player);
                    ModularCarCodeLock.RemoveLock();
                }
                else if (BaseLock != null)
                {
                    self?.EntityRemoved(BaseLock, null, player);
                    BaseLock.Kill();
                }
            }

            public List<ulong> GetCodeLockWhitelistPlayers()
            {
                if (ModularCarCodeLock == null && BaseLock == null) return Array.Empty<ulong>().ToList();

                if (BaseLock is KeyLock)
                    return Array.Empty<ulong>().ToList();

                if (BaseLock != null && BaseLock is CodeLock codeLock)
                    return codeLock.whitelistPlayers;

                if (BaseEntity != null && BaseEntity is ModularCar modularCar)
                    return modularCar.CarLock.WhitelistPlayers.ToList();

                return Array.Empty<ulong>().ToList();
            }
        }

        private class ClanInfo
        {
            public string ClanName;
            public List<string> ClanMemberUserIdList = new();
        }

        private class PlayerInfo
        {
            public ulong Steamid;
            public string DisplayName;
            public Transform? Transform;
            public bool IsBasePlayer;
            public object? PlayerObject;
        }

        #endregion

        #region TODO

        // 1) I cavalli non possono mangiare
        // 2) Sistemare il fatto che possano essere accese con un Accensore: Fornaci, raffinerie, ecc...
        // 3) Disabilitare craftintg se ci sono delle workbench e sono tutte bloccate con il lucchetto
        // 4) Aggiungere supporto per il drone
        // 5) Signage e NeonSign: ogni tanto non blocca la modifica
        // 6) RF: bloccare uso delle frequenze se bloccato con code lock
        // 7) Bloccare effetti sonori se si è in modalità Vanish o noclip

        #endregion

        #region Localization

        private string Lang(string key, string playerID = null, params object[] args)
        {
            return string.Format(lang.GetMessage(key, this, playerID), args);
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "LOCKED",
                ["LockerStatus_Unlocked"] = "UNLOCKED",
                ["LockerStatus_Removed"] = "REMOVED",
                ["LockerStatus_Updated"] = "UPDATED",

                ["NoCodeLockAuth"] = "It is blocked...",
                ["NoPermissions"] = "<color=red>You don't have permission to use this command.</color>",
                ["NoBypassPermissions"] = "<color=red>You don't have permission to bypass code.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID is not the owner of the entity.</color>",
                ["NoEntityOwner"] = "<color=red>You do not own this entity.</color>",
                ["PlayerNotFound"] = "<color=red>The player could not be found.</color>",
                ["InvalidSteamID"] = "<color=red>Invalid SteamID.</color>",
                ["CodeLockNotFound"] = "<color=red>CodeLock or Key Lock could not be found.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Code Lock does not have a code.</color>",
                ["EntityNotAllowed"] = "<color=red>Not allowed on this entity.</color>",
                ["LockCodeNotValid"] = "<color=red>Lock code is not valid (Requires 4 numbers).</color>",
                ["MissingParametersError"] = "<color=red>An error occurred: Missing parameters - [{0}].</color>",
                ["GenericError"] = "<color=red>An error occurred: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Delay Time not valid: Please enter a value between {0} and {1}.</color>",
                ["EntityNotFound"] = "<color=red>You have to look at an entity.</color>",

                ["DoorCloserNotFound"] = "<color=red>The entity does not have a Door Closer.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>You do not have permission or Door Closer is not enabled.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>You don't have Door Closer enabled.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>You don't have the Door Closer in your inventory.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Invalid Door Closer category.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>A Door Closer is already placed on this entity.</color>",

                ["LockCategoryNotValid"] = "<color=red>Invalid lock category.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>You don't have permission or auto lock isn't enabled.</color>",
                ["LockItemNotFound"] = "<color=red>You don't have a lock in your inventory.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>You don't have a lock in your inventory.\nYou can only use locks without a code</color>",

                ["ShowCodeLockCodeMessage"] = "Code Lock code is:\nCode: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Code Lock code is:\nCode: <color=#faa511>{0}</color>\nGuest Code: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Streamer mode active:</color> code cannot be viewed",

                ["CodeLockPinChangedMessage"] = "Code Lock Pin changed.",
                ["AllCodeLockPinChangedMessage"] = "All Code Lock Pins have been changed.",
                ["AllGuestCodeLockPinChangedMessage"] = "All Guest Code Lock Pins have been changed.",
                ["AllGuestCodeLockPinRemovedMessage"] = "All Guest Code Lock Pins have been removed.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "All permissions for sharing Code Locks with your clan/team have been changed.",

                ["AutoLockKeyLockDeployedMessage"] = "Key Lock deployed and locked.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock deployed and locked\nCode: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Code Lock deployed and locked\nCode: <color=#faa511>{0}</color>\nGuest Code: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] =
                    "Pin Code Lock: <color=red>{0}</color>. You can <color=red>change the pin</color> with the Modular Car Lift.",

                ["CodeLockListMessage"] = "You have {0} {1} Code Locks / key Locks.",
                ["CodeLockMessage"] = "You have {0} a Code Lock / Key Locks.",
                ["LockClosedAutomaticallyMessage"] = "The lock was closed automatically.",

                ["SingleDoorCloserCustomTime"] = "You have set the time for this Door Closer to <color=#faa511>{0}s</color>.",
                ["DoorCloserListMessage"] = "You have {0} {1} Door Closer.",
                ["DoorCloserDeployed"] = "Door closer deployed. Delayed closing time set: <color=#faa511>{0}s</color>",

                ["UiButton_Yes"] = "YES",
                ["UiButton_No"] = "NO",

                ["UiHeaderTitle"] = "Auto Lock Settings",
                ["UiShareLocksWithClanTeamEnabled"] = "Share Locks with Clan/Team",
                ["UiAutoLockOnPlacementEnabled"] = "Auto lock",
                ["UiCodeLockPin"] = "Code Lock Pin",
                ["UiGuestCodeEnabled"] = "Guest Code Lock",
                ["UiGuestCodeLockPin"] = "Guest Code Lock Pin",
                ["UiUpdateAll"] = "UPDATE ALL",
                ["UiRemoveAll"] = "REMOVE ALL",
                ["UiAlsoUseKeyLockEnabled"] = "Also Use Key Lock",
                ["UiStreamerModeEnabled"] = "Streamer Mode",
                ["UiDoorsLockEnabled"] = "Auto Lock: Doors",
                ["UiWindowsLockEnabled"] = "Auto Lock: Windows",
                ["UiBoxesLockEnabled"] = "Auto Lock: Boxes",
                ["UiStorageContainerLockEnabled"] = "Auto Lock: All Storage Container",
                ["UiLockersLockEnabled"] = "Auto Lock: Lockers",
                ["UiCupboardsLockEnabled"] = "Auto Lock: Cupboards (TC)",
                ["UiVehicleLockEnabled"] = "Auto Lock: Vehicle",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Automatic lock closing when dismount of the vehicle",
                ["UiMedievalEntityLockEnabled"] = "Auto Lock: Medieval Entities",
                ["UiFurnaceLockEnabled"] = "Auto Lock: Furnace / Refineries",
                ["UiVendingMachineLockEnabled"] = "Auto Lock: Vending Machine",
                ["UiComposterLockEnabled"] = "Auto Lock: Composter",
                ["UiMixingTableLockEnabled"] = "Auto Lock: Mixing Table",
                ["UiPlanterLockEnabled"] = "Auto Lock: Planter",
                ["UiAutoTurretLockEnabled"] = "Auto Lock: Auto Turret",
                ["UiSamSiteLockEnabled"] = "Auto Lock: SAM Site",
                ["UiTrapsLockEnabled"] = "Auto Lock: Traps",
                ["UiWeaponRackLockEnabled"] = "Auto Lock: Weapon Rack",
                ["UiStashLockEnabled"] = "Auto Lock: Stash",
                ["UiNeonSignLockEnabled"] = "Auto Lock: Neon Sign",
                ["UiOtherLockableEntitiesLockEnabled"] = "Auto Lock: Other Lockable Entities",
                ["UiOtherCustomEntitiesLockEnabled"] = "Auto Lock: Other Custom Entities",

                ["UiOpenAutoClosingButtonText"] = "Auto Closing Settings",
                ["UiAutoClosingHeaderTitle"] = "Auto Closing Settings",
                ["UiAutoClosingEnabledTitle"] = "Auto Closing Enabled",
                ["UiClosingDelay"] = "Closing Delay (s)",

                ["UiDoorAutoClosingEnabled"] = "Auto Closing: Door",
                ["UiDoubleDoorAutoClosingEnabled"] = "Auto Closing: Double Door",
                ["UiWindowsAutoClosingEnabled"] = "Auto Closing: Windows",
                ["UiGarageAutoClosingEnabled"] = "Auto Closing: Garage",
                ["UiLadderHatchAutoClosingEnabled"] = "Auto Closing: Ladder Hatch",
                ["UiExternalGateAutoClosingEnabled"] = "Auto Closing: External Gate",
                ["UiFenceGateAutoClosingEnabled"] = "Auto Closing: Fence Gate",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Auto Closing: Legacy Wood Shelter Door"
            }, this);

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "BLOCCATO",
                ["LockerStatus_Unlocked"] = "SBLOCCATO",
                ["LockerStatus_Removed"] = "RIMOSSO",
                ["LockerStatus_Updated"] = "AGGIORNATO",

                ["NoCodeLockAuth"] = "È bloccato...",
                ["NoPermissions"] = "<color=red>Non hai l'autorizzazione per usare questo comando.</color>",
                ["NoBypassPermissions"] = "<color=red>Non hai l'autorizzazione per bypassare il codice.</color>",
                ["NoSteamIdOwner"] = "<color=red>Questo SteamID non è il proprietario dell'entità.</color>",
                ["NoEntityOwner"] = "<color=red>Non sei proprietario di questa entità.</color>",
                ["PlayerNotFound"] = "<color=red>Impossibile trovare il giocatore.</color>",
                ["InvalidSteamID"] = "<color=red>SteamID non valido.</color>",
                ["CodeLockNotFound"] = "<color=red>Impossibile trovare la Serratura a codice o la Serratura.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>La Serratura a codice non ha un codice.</color>",
                ["EntityNotAllowed"] = "<color=red>Non consentito su questa entità.</color>",
                ["LockCodeNotValid"] = "<color=red>Il codice di blocco non è valido (richiede 4 numeri).</color>",
                ["MissingParametersError"] = "<color=red>Si è verificato un errore: Parametri mancanti - [{0}].</color>",
                ["GenericError"] = "<color=red>Si è verificato un errore: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Tempo chiusura ritardata non valido: inserisci un valore compreso tra {0} e {1}.</color>",
                ["EntityNotFound"] = "<color=red>Devi guardare un entità.</color>",

                ["DoorCloserNotFound"] = "<color=red>L'entità non ha un Chiudiporta.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>Non hai i permessi o la chiusura ritardata non è abilitata.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>Non hai la chiusura ritardata abilitata.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>Non hai il Door Closer nell'inventario.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Categoria chiusura automatica non valida.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Un Chiudiporta è gia posizionato su questa entità.</color>",

                ["LockCategoryNotValid"] = "<color=red>Categoria di blocco non valida.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>Non hai i permessi o il blocco automatico non è attivato.</color>",
                ["LockItemNotFound"] = "<color=red>Non hai una serratura nell'inventario.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>Non hai una serratura nell'inventario.\nPuoi usare solo serrature senza codice</color>",

                ["ShowCodeLockCodeMessage"] = "Il codice di blocco della Serratura a codice è:\nCodice: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] =
                    "Il codice di blocco della Serratura a codice è:\nCodice: <color=#faa511>{0}</color>\nCodice ospite: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Modalità streamer attiva:</color> il codice non può essere visualizzato",

                ["CodeLockPinChangedMessage"] = "Il codice PIN della Serratura a codice è stato modificato.",
                ["AllCodeLockPinChangedMessage"] = "Tutti i PIN delle Serrature a Codice sono stati modificati.",
                ["AllGuestCodeLockPinChangedMessage"] = "Tutti i codici PIN per gli ospiti sono stati modificati.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Tutti i codici PIN per gli ospiti sono stati rimossi.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Sono state modificate tutte le autorizzazioni di condivisione delle Serrature a codice con il clan/team.",

                ["AutoLockKeyLockDeployedMessage"] = "Serratura applicata e bloccata.",
                ["AutoLockCodeLockDeployedMessage"] = "Serratura a codice applicata e bloccata\nCodice: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Serratura a codice applicata e bloccata\nCodice: <color=#faa511>{0}</color>\nCodice ospite: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] =
                    "Codice PIN: <color=red>{0}</color>. È possibile <color=red>cambiare il PIN</color> con l'ascensore per auto.",

                ["CodeLockListMessage"] = "Hai {0} {1} Serrature a codice / Serrature.",
                ["CodeLockMessage"] = "Hai {0} la Serratura a codice / Serratura.",
                ["LockClosedAutomaticallyMessage"] = "La serratura è stata chiusa in automatico.",

                ["SingleDoorCloserCustomTime"] = "Hai impostato a <color=#faa511>{0}s</color> il tempo per questa chiusura ritardata.",
                ["DoorCloserListMessage"] = "Hai {0} {1} Chiudiporta.",
                ["DoorCloserDeployed"] = "Chiudiporta posizionato. Tempo impostato per la chiusura ritardata: <color=#faa511>{0}s</color>",

                ["UiButton_Yes"] = "SI",
                ["UiButton_No"] = "NO",

                ["UiHeaderTitle"] = "Impostazioni blocco automatico",
                ["UiShareLocksWithClanTeamEnabled"] = "Condividi le Serrature con il clan/team",
                ["UiAutoLockOnPlacementEnabled"] = "Abilita il blocco automatico",
                ["UiCodeLockPin"] = "Pin Serratura a codice",
                ["UiGuestCodeEnabled"] = "Abilita Codice di blocco per ospiti",
                ["UiGuestCodeLockPin"] = "Codice PIN per gli ospiti",
                ["UiUpdateAll"] = "AGGIORNA TUTTO",
                ["UiRemoveAll"] = "RIMUOVI TUTTO",
                ["UiAlsoUseKeyLockEnabled"] = "Usa anche le Serrature senza codice",
                ["UiStreamerModeEnabled"] = "Modalità Streamer",
                ["UiDoorsLockEnabled"] = "Blocco automatico: Porte",
                ["UiWindowsLockEnabled"] = "Blocco automatico: Finestre",
                ["UiBoxesLockEnabled"] = "Blocco automatico: Contenitori / Barili",
                ["UiStorageContainerLockEnabled"] = "Blocco automatico: Tutti i tipi di contenitore",
                ["UiLockersLockEnabled"] = "Blocco automatico: Armadietti",
                ["UiCupboardsLockEnabled"] = "Blocco automatico: Armadio degli attrezzi (TC)",
                ["UiVehicleLockEnabled"] = "Blocco automatico: Veicoli",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Chiusura automatica serratura quando scendi dal veicolo",
                ["UiMedievalEntityLockEnabled"] = "Blocco automatico: Entità Medievali",
                ["UiFurnaceLockEnabled"] = "Blocco automatico: Fornaci / Raffinerie",
                ["UiVendingMachineLockEnabled"] = "Blocco automatico: Distributore automatico",
                ["UiComposterLockEnabled"] = "Blocco automatico: Compostiera",
                ["UiMixingTableLockEnabled"] = "Blocco automatico: Tavolo di miscelazione",
                ["UiPlanterLockEnabled"] = "Blocco automatico: Fioriera",
                ["UiAutoTurretLockEnabled"] = "Blocco automatico: Torrette automatiche",
                ["UiSamSiteLockEnabled"] = "Blocco automatico: Torrette SAM",
                ["UiTrapsLockEnabled"] = "Blocco automatico: Trappole",
                ["UiWeaponRackLockEnabled"] = "Blocco automatico: Rastrelliera per armi",
                ["UiStashLockEnabled"] = "Blocco automatico: Piccola Scorta",
                ["UiNeonSignLockEnabled"] = "Blocco automatico: Insegna al neon",
                ["UiOtherLockableEntitiesLockEnabled"] = "Blocco automatico: Altre entità bloccabili",
                ["UiOtherCustomEntitiesLockEnabled"] = "Blocco automatico: Altre entità personalizzate",

                ["UiOpenAutoClosingButtonText"] = "Impostazioni chiusura automatica",
                ["UiAutoClosingHeaderTitle"] = "Impostazioni chiusura automatica",
                ["UiAutoClosingEnabledTitle"] = "Chiusura automatica abilitata",
                ["UiClosingDelay"] = "Tempo di chiusura (s)",

                ["UiDoorAutoClosingEnabled"] = "Chiusura automatica: Porte",
                ["UiDoubleDoorAutoClosingEnabled"] = "Chiusura automatica: Porte doppie",
                ["UiWindowsAutoClosingEnabled"] = "Chiusura automatica: Finestre",
                ["UiGarageAutoClosingEnabled"] = "Chiusura automatica: Garage",
                ["UiLadderHatchAutoClosingEnabled"] = "Chiusura automatica: Scala a scomparsa",
                ["UiExternalGateAutoClosingEnabled"] = "Chiusura automatica: Portone mura",
                ["UiFenceGateAutoClosingEnabled"] = "Chiusura automatica: Cancello del recinto",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Chiusura automatica: porta del rifugio"
            }, this, "it");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "BLOQUEADO",
                ["LockerStatus_Unlocked"] = "DESABLOQUEADO",
                ["LockerStatus_Removed"] = "REMOVIDO",
                ["LockerStatus_Updated"] = "ACTUALIZADO",
                ["NoCodeLockAuth"] = "Está bloqueado...",
                ["NoPermissions"] = "<color=red>No tienes permiso para usar este comando.</color>",
                ["NoBypassPermissions"] = "<color=red>No tienes permiso para omitir el código.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID no es el propietario de la entidad.</color>",
                ["NoEntityOwner"] = "<color=red>No eres el dueño de esta entidad.</color>",
                ["PlayerNotFound"] = "<color=red>No se pudo encontrar al jugador.</color>",
                ["InvalidSteamID"] = "<color=red>SteamID inválido.</color>",
                ["CodeLockNotFound"] = "<color=red>No se pudo encontrar el CodeLock o Key Lock.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>El Code Lock no tiene un código.</color>",
                ["EntityNotAllowed"] = "<color=red>No permitido en esta entidad.</color>",
                ["LockCodeNotValid"] = "<color=red>El código de bloqueo no es válido (Requiere 4 números).</color>",
                ["MissingParametersError"] = "<color=red>Ocurrió un error: Parámetros faltantes - [{0}].</color>",
                ["GenericError"] = "<color=red>Ocurrió un error: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Tiempo de retraso no válido: Por favor ingrese un valor entre {0} y {1}.</color>",
                ["EntityNotFound"] = "<color=red>Tienes que mirar una entidad.</color>",
                ["DoorCloserNotFound"] = "<color=red>La entidad no tiene un Cierre de Puerta.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>No tienes permiso o el Cierre de Puerta no está habilitado.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>No tienes el Cierre de Puerta habilitado.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>No tienes el Cierre de Puerta en tu inventario.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Categoría de Cierre de Puerta inválida.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Ya hay un Cierre de Puerta colocado en esta entidad.</color>",
                ["LockCategoryNotValid"] = "<color=red>Categoría de bloqueo inválida.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>No tienes permiso o el bloqueo automático no está habilitado.</color>",
                ["LockItemNotFound"] = "<color=red>No tienes un candado en tu inventario.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>No tienes un candado en tu inventario.\nSolo puedes usar candados sin código</color>",
                ["ShowCodeLockCodeMessage"] = "El código del Code Lock es:\nCódigo: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "El código del Code Lock es:\nCódigo: <color=#faa511>{0}</color>\nCódigo de Invitado: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Modo Streamer activo:</color> código no puede ser visto",
                ["CodeLockPinChangedMessage"] = "Pin del Code Lock cambiado.",
                ["AllCodeLockPinChangedMessage"] = "Todos los Pines del Code Lock han sido cambiados.",
                ["AllGuestCodeLockPinChangedMessage"] = "Todos los Pines de Invitado del Code Lock han sido cambiados.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Todos los Pines de Invitado del Code Lock han sido removidos.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Todos los permisos para compartir Code Locks con tu clan/equipo han sido cambiados.",
                ["AutoLockKeyLockDeployedMessage"] = "Candado de llave desplegado y bloqueado.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock desplegado y bloqueado\nCódigo: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Code Lock desplegado y bloqueado\nCódigo: <color=#faa511>{0}</color>\nCódigo de Invitado: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Pin Code Lock: <color=red>{0}</color>. Puedes <color=red>cambiar el pin</color> con el Elevador de Coches Modular.",
                ["CodeLockListMessage"] = "Tienes {0} Code Locks / Key Locks {1}.",
                ["CodeLockMessage"] = "Tienes {0} un Code Lock / Key Locks.",
                ["LockClosedAutomaticallyMessage"] = "El candado se cerró automáticamente.",
                ["SingleDoorCloserCustomTime"] = "Has establecido el tiempo para este Cierre de Puerta en <color=#faa511>{0}s</color>.",
                ["DoorCloserListMessage"] = "Tienes {0} Cierre de Puerta {1}.",
                ["DoorCloserDeployed"] = "Cierre de Puerta desplegado. Tiempo de cierre diferido establecido: <color=#faa511>{0}s</color>",
                ["UiButton_Yes"] = "SÍ",
                ["UiButton_No"] = "NO",
                ["UiHeaderTitle"] = "Configuraciones de Bloqueo Automático",
                ["UiShareLocksWithClanTeamEnabled"] = "Compartir Bloqueos con Clan/Equipo",
                ["UiAutoLockOnPlacementEnabled"] = "Bloqueo automático",
                ["UiCodeLockPin"] = "Pin del Code Lock",
                ["UiGuestCodeEnabled"] = "Código de Invitado del Code Lock",
                ["UiGuestCodeLockPin"] = "Pin del Código de Invitado del Code Lock",
                ["UiUpdateAll"] = "ACTUALIZAR TODO",
                ["UiRemoveAll"] = "REMOVER TODO",
                ["UiAlsoUseKeyLockEnabled"] = "También Usar Candado de Llave",
                ["UiStreamerModeEnabled"] = "Modo Streamer",
                ["UiDoorsLockEnabled"] = "Bloqueo Automático: Puertas",
                ["UiWindowsLockEnabled"] = "Bloqueo Automático: Ventanas",
                ["UiBoxesLockEnabled"] = "Bloqueo Automático: Cajas",
                ["UiStorageContainerLockEnabled"] = "Bloqueo Automático: Todos los Contenedores de Almacenamiento",
                ["UiLockersLockEnabled"] = "Bloqueo Automático: Lockers",
                ["UiCupboardsLockEnabled"] = "Bloqueo Automático: Armarios (TC)",
                ["UiVehicleLockEnabled"] = "Bloqueo Automático: Vehículo",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Cierre automático al desmontar del vehículo",
                ["UiMedievalEntityLockEnabled"] = "Bloqueo Automático: Entidades Medievales",
                ["UiFurnaceLockEnabled"] = "Bloqueo Automático: Horno / Refinerías",
                ["UiVendingMachineLockEnabled"] = "Bloqueo Automático: Máquina Expendedora",
                ["UiComposterLockEnabled"] = "Bloqueo Automático: Compostador",
                ["UiMixingTableLockEnabled"] = "Bloqueo Automático: Mesa de Mezclas",
                ["UiPlanterLockEnabled"] = "Bloqueo Automático: Plantador",
                ["UiAutoTurretLockEnabled"] = "Bloqueo Automático: Torreta Automática",
                ["UiSamSiteLockEnabled"] = "Bloqueo Automático: Sitio SAM",
                ["UiTrapsLockEnabled"] = "Bloqueo Automático: Trampas",
                ["UiWeaponRackLockEnabled"] = "Bloqueo Automático: Soporte de Armas",
                ["UiStashLockEnabled"] = "Bloqueo Automático: Escondite",
                ["UiNeonSignLockEnabled"] = "Bloqueo Automático: Letrero de Neón",
                ["UiOtherLockableEntitiesLockEnabled"] = "Bloqueo Automático: Otras Entidades Bloqueables",
                ["UiOtherCustomEntitiesLockEnabled"] = "Bloqueo Automático: Otras Entidades Personalizadas",
                ["UiOpenAutoClosingButtonText"] = "Configuraciones de Cierre Automático",
                ["UiAutoClosingHeaderTitle"] = "Configuraciones de Cierre Automático",
                ["UiAutoClosingEnabledTitle"] = "Cierre Automático Habilitado",
                ["UiClosingDelay"] = "Retraso de Cierre (s)",
                ["UiDoorAutoClosingEnabled"] = "Cierre Automático: Puerta",
                ["UiDoubleDoorAutoClosingEnabled"] = "Cierre Automático: Puerta Doble",
                ["UiWindowsAutoClosingEnabled"] = "Cierre Automático: Ventanas",
                ["UiGarageAutoClosingEnabled"] = "Cierre Automático: Garaje",
                ["UiLadderHatchAutoClosingEnabled"] = "Cierre Automático: Escotilla de Escalera",
                ["UiExternalGateAutoClosingEnabled"] = "Cierre Automático: Puerta Externa",
                ["UiFenceGateAutoClosingEnabled"] = "Cierre Automático: Puerta de Cerca",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Cierre Automático: Puerta de Refugio de Madera Antiguo"
            }, this, "es");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "BLOQUEADO",
                ["LockerStatus_Unlocked"] = "DESABLOQUEADO",
                ["LockerStatus_Removed"] = "REMOVIDO",
                ["LockerStatus_Updated"] = "ACTUALIZADO",
                ["NoCodeLockAuth"] = "Está bloqueado...",
                ["NoPermissions"] = "<color=red>No tienes permiso para usar este comando.</color>",
                ["NoBypassPermissions"] = "<color=red>No tienes permiso para omitir el código.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID no es el propietario de la entidad.</color>",
                ["NoEntityOwner"] = "<color=red>No eres el dueño de esta entidad.</color>",
                ["PlayerNotFound"] = "<color=red>No se pudo encontrar al jugador.</color>",
                ["InvalidSteamID"] = "<color=red>SteamID inválido.</color>",
                ["CodeLockNotFound"] = "<color=red>No se pudo encontrar el CodeLock o Key Lock.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>El Code Lock no tiene un código.</color>",
                ["EntityNotAllowed"] = "<color=red>No permitido en esta entidad.</color>",
                ["LockCodeNotValid"] = "<color=red>El código de bloqueo no es válido (Requiere 4 números).</color>",
                ["MissingParametersError"] = "<color=red>Ocurrió un error: Parámetros faltantes - [{0}].</color>",
                ["GenericError"] = "<color=red>Ocurrió un error: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Tiempo de retraso no válido: Por favor ingrese un valor entre {0} y {1}.</color>",
                ["EntityNotFound"] = "<color=red>Tienes que mirar una entidad.</color>",
                ["DoorCloserNotFound"] = "<color=red>La entidad no tiene un Cierre de Puerta.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>No tienes permiso o el Cierre de Puerta no está habilitado.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>No tienes el Cierre de Puerta habilitado.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>No tienes el Cierre de Puerta en tu inventario.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Categoría de Cierre de Puerta inválida.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Ya hay un Cierre de Puerta colocado en esta entidad.</color>",
                ["LockCategoryNotValid"] = "<color=red>Categoría de bloqueo inválida.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>No tienes permiso o el bloqueo automático no está habilitado.</color>",
                ["LockItemNotFound"] = "<color=red>No tienes un candado en tu inventario.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>No tienes un candado en tu inventario.\nSolo puedes usar candados sin código</color>",
                ["ShowCodeLockCodeMessage"] = "El código del Code Lock es:\nCódigo: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "El código del Code Lock es:\nCódigo: <color=#faa511>{0}</color>\nCódigo de Invitado: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Modo Streamer activo:</color> código no puede ser visto",
                ["CodeLockPinChangedMessage"] = "Pin del Code Lock cambiado.",
                ["AllCodeLockPinChangedMessage"] = "Todos los Pines del Code Lock han sido cambiados.",
                ["AllGuestCodeLockPinChangedMessage"] = "Todos los Pines de Invitado del Code Lock han sido cambiados.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Todos los Pines de Invitado del Code Lock han sido removidos.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Todos los permisos para compartir Code Locks con tu clan/equipo han sido cambiados.",
                ["AutoLockKeyLockDeployedMessage"] = "Candado de llave desplegado y bloqueado.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock desplegado y bloqueado\nCódigo: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Code Lock desplegado y bloqueado\nCódigo: <color=#faa511>{0}</color>\nCódigo de Invitado: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Pin Code Lock: <color=red>{0}</color>. Puedes <color=red>cambiar el pin</color> con el Elevador de Coches Modular.",
                ["CodeLockListMessage"] = "Tienes {0} Code Locks / Key Locks {1}.",
                ["CodeLockMessage"] = "Tienes {0} un Code Lock / Key Locks.",
                ["LockClosedAutomaticallyMessage"] = "El candado se cerró automáticamente.",
                ["SingleDoorCloserCustomTime"] = "Has establecido el tiempo para este Cierre de Puerta en <color=#faa511>{0}s</color>.",
                ["DoorCloserListMessage"] = "Tienes {0} Cierre de Puerta {1}.",
                ["DoorCloserDeployed"] = "Cierre de Puerta desplegado. Tiempo de cierre diferido establecido: <color=#faa511>{0}s</color>",
                ["UiButton_Yes"] = "SÍ",
                ["UiButton_No"] = "NO",
                ["UiHeaderTitle"] = "Configuraciones de Bloqueo Automático",
                ["UiShareLocksWithClanTeamEnabled"] = "Compartir Bloqueos con Clan/Equipo",
                ["UiAutoLockOnPlacementEnabled"] = "Bloqueo automático",
                ["UiCodeLockPin"] = "Pin del Code Lock",
                ["UiGuestCodeEnabled"] = "Código de Invitado del Code Lock",
                ["UiGuestCodeLockPin"] = "Pin del Código de Invitado del Code Lock",
                ["UiUpdateAll"] = "ACTUALIZAR TODO",
                ["UiRemoveAll"] = "REMOVER TODO",
                ["UiAlsoUseKeyLockEnabled"] = "También Usar Candado de Llave",
                ["UiStreamerModeEnabled"] = "Modo Streamer",
                ["UiDoorsLockEnabled"] = "Bloqueo Automático: Puertas",
                ["UiWindowsLockEnabled"] = "Bloqueo Automático: Ventanas",
                ["UiBoxesLockEnabled"] = "Bloqueo Automático: Cajas",
                ["UiStorageContainerLockEnabled"] = "Bloqueo Automático: Todos los Contenedores de Almacenamiento",
                ["UiLockersLockEnabled"] = "Bloqueo Automático: Lockers",
                ["UiCupboardsLockEnabled"] = "Bloqueo Automático: Armarios (TC)",
                ["UiVehicleLockEnabled"] = "Bloqueo Automático: Vehículo",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Cierre automático al desmontar del vehículo",
                ["UiMedievalEntityLockEnabled"] = "Bloqueo Automático: Entidades Medievales",
                ["UiFurnaceLockEnabled"] = "Bloqueo Automático: Horno / Refinerías",
                ["UiVendingMachineLockEnabled"] = "Bloqueo Automático: Máquina Expendedora",
                ["UiComposterLockEnabled"] = "Bloqueo Automático: Compostador",
                ["UiMixingTableLockEnabled"] = "Bloqueo Automático: Mesa de Mezclas",
                ["UiPlanterLockEnabled"] = "Bloqueo Automático: Plantador",
                ["UiAutoTurretLockEnabled"] = "Bloqueo Automático: Torreta Automática",
                ["UiSamSiteLockEnabled"] = "Bloqueo Automático: Sitio SAM",
                ["UiTrapsLockEnabled"] = "Bloqueo Automático: Trampas",
                ["UiWeaponRackLockEnabled"] = "Bloqueo Automático: Soporte de Armas",
                ["UiStashLockEnabled"] = "Bloqueo Automático: Escondite",
                ["UiNeonSignLockEnabled"] = "Bloqueo Automático: Letrero de Neón",
                ["UiOtherLockableEntitiesLockEnabled"] = "Bloqueo Automático: Otras Entidades Bloqueables",
                ["UiOtherCustomEntitiesLockEnabled"] = "Bloqueo Automático: Otras Entidades Personalizadas",
                ["UiOpenAutoClosingButtonText"] = "Configuraciones de Cierre Automático",
                ["UiAutoClosingHeaderTitle"] = "Configuraciones de Cierre Automático",
                ["UiAutoClosingEnabledTitle"] = "Cierre Automático Habilitado",
                ["UiClosingDelay"] = "Retraso de Cierre (s)",
                ["UiDoorAutoClosingEnabled"] = "Cierre Automático: Puerta",
                ["UiDoubleDoorAutoClosingEnabled"] = "Cierre Automático: Puerta Doble",
                ["UiWindowsAutoClosingEnabled"] = "Cierre Automático: Ventanas",
                ["UiGarageAutoClosingEnabled"] = "Cierre Automático: Garaje",
                ["UiLadderHatchAutoClosingEnabled"] = "Cierre Automático: Escotilla de Escalera",
                ["UiExternalGateAutoClosingEnabled"] = "Cierre Automático: Puerta Externa",
                ["UiFenceGateAutoClosingEnabled"] = "Cierre Automático: Puerta de Cerca",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Cierre Automático: Puerta de Refugio de Madera Antiguo"
            }, this, "es-ES");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "VERROUILLÉ",
                ["LockerStatus_Unlocked"] = "DÉVERROUILLÉ",
                ["LockerStatus_Removed"] = "ENLEVÉ",
                ["LockerStatus_Updated"] = "MIS À JOUR",
                ["NoCodeLockAuth"] = "Il est bloqué...",
                ["NoPermissions"] = "<color=red>Vous n'avez pas la permission d'utiliser cette commande.</color>",
                ["NoBypassPermissions"] = "<color=red>Vous n'avez pas la permission de contourner le code.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID n'est pas le propriétaire de l'entité.</color>",
                ["NoEntityOwner"] = "<color=red>Vous ne possédez pas cette entité.</color>",
                ["PlayerNotFound"] = "<color=red>Le joueur n'a pas pu être trouvé.</color>",
                ["InvalidSteamID"] = "<color=red>SteamID invalide.</color>",
                ["CodeLockNotFound"] = "<color=red>CodeLock ou Key Lock n'a pas pu être trouvé.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Le Code Lock n'a pas de code.</color>",
                ["EntityNotAllowed"] = "<color=red>Non autorisé sur cette entité.</color>",
                ["LockCodeNotValid"] = "<color=red>Le code de verrouillage n'est pas valide (nécessite 4 chiffres).</color>",
                ["MissingParametersError"] = "<color=red>Une erreur s'est produite: Paramètres manquants - [{0}].</color>",
                ["GenericError"] = "<color=red>Une erreur s'est produite: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Temps de retard non valide: Veuillez entrer une valeur entre {0} et {1}.</color>",
                ["EntityNotFound"] = "<color=red>Vous devez regarder une entité.</color>",
                ["DoorCloserNotFound"] = "<color=red>L'entité n'a pas de ferme-porte.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>Vous n'avez pas la permission ou le ferme-porte n'est pas activé.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>Vous n'avez pas activé le ferme-porte.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>Vous n'avez pas le ferme-porte dans votre inventaire.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Catégorie de ferme-porte non valide.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Un ferme-porte est déjà placé sur cette entité.</color>",
                ["LockCategoryNotValid"] = "<color=red>Catégorie de verrouillage non valide.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>Vous n'avez pas la permission ou le verrouillage automatique n'est pas activé.</color>",
                ["LockItemNotFound"] = "<color=red>Vous n'avez pas de verrou dans votre inventaire.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>Vous n'avez pas de cadenas dans votre inventaire.\nVous ne pouvez utiliser que des cadenas sans code</color>",
                ["ShowCodeLockCodeMessage"] = "Le code du Code Lock est:\nCode: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Le code du Code Lock est:\nCode: <color=#faa511>{0}</color>\nCode invité: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Mode Streamer actif:</color> le code ne peut pas être visualisé",
                ["CodeLockPinChangedMessage"] = "Le code du Code Lock a été changé.",
                ["AllCodeLockPinChangedMessage"] = "Tous les codes du Code Lock ont été changés.",
                ["AllGuestCodeLockPinChangedMessage"] = "Tous les codes invités du Code Lock ont été changés.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Tous les codes invités du Code Lock ont été supprimés.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Toutes les autorisations de partage des Code Locks avec votre clan/équipe ont été modifiées.",
                ["AutoLockKeyLockDeployedMessage"] = "Verrou à clé déployé et verrouillé.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock déployé et verrouillé\nCode: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] = "Code Lock déployé et verrouillé\nCode: <color=#faa511>{0}</color>\nCode invité: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Code Pin Lock: <color=red>{0}</color>. Vous pouvez <color=red>changer le code</color> avec le Ascenseur de Voiture Modulaire.",
                ["CodeLockListMessage"] = "Vous avez {0} {1} Code Locks / verrous à clé.",
                ["CodeLockMessage"] = "Vous avez {0} un Code Lock / verrous à clé.",
                ["LockClosedAutomaticallyMessage"] = "Le verrou a été fermé automatiquement.",
                ["SingleDoorCloserCustomTime"] = "Vous avez défini le temps pour ce ferme-porte à <color=#faa511>{0}s</color>.",
                ["DoorCloserListMessage"] = "Vous avez {0} {1} ferme-porte.",
                ["DoorCloserDeployed"] = "Ferme-porte déployé. Temps de fermeture différée défini : <color=#faa511>{0}s</color>",
                ["UiButton_Yes"] = "OUI",
                ["UiButton_No"] = "NON",
                ["UiHeaderTitle"] = "Paramètres de Verrouillage Automatique",
                ["UiShareLocksWithClanTeamEnabled"] = "Partager les verrous avec le Clan/Équipe",
                ["UiAutoLockOnPlacementEnabled"] = "Verrouillage automatique",
                ["UiCodeLockPin"] = "Code Pin du Code Lock",
                ["UiGuestCodeEnabled"] = "Verrouillage du Code invité",
                ["UiGuestCodeLockPin"] = "Code Pin du Code invité",
                ["UiUpdateAll"] = "TOUT METTRE À JOUR",
                ["UiRemoveAll"] = "TOUT SUPPRIMER",
                ["UiAlsoUseKeyLockEnabled"] = "Utiliser également le verrou à clé",
                ["UiStreamerModeEnabled"] = "Mode Streamer",
                ["UiDoorsLockEnabled"] = "Verrouillage automatique : Portes",
                ["UiWindowsLockEnabled"] = "Verrouillage automatique : Fenêtres",
                ["UiBoxesLockEnabled"] = "Verrouillage automatique : Boîtes",
                ["UiStorageContainerLockEnabled"] = "Verrouillage automatique : Tous les Conteneurs de Stockage",
                ["UiLockersLockEnabled"] = "Verrouillage automatique : Casiers",
                ["UiCupboardsLockEnabled"] = "Verrouillage automatique : Armoires (TC)",
                ["UiVehicleLockEnabled"] = "Verrouillage automatique : Véhicule",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Fermeture automatique du verrou lors de la descente du véhicule",
                ["UiMedievalEntityLockEnabled"] = "Verrouillage automatique : Entités Médiévales",
                ["UiFurnaceLockEnabled"] = "Verrouillage automatique : Four / Raffineries",
                ["UiVendingMachineLockEnabled"] = "Verrouillage automatique : Distributeur Automatique",
                ["UiComposterLockEnabled"] = "Verrouillage automatique : Composteur",
                ["UiMixingTableLockEnabled"] = "Verrouillage automatique : Table de Mélange",
                ["UiPlanterLockEnabled"] = "Verrouillage automatique : Jardinière",
                ["UiAutoTurretLockEnabled"] = "Verrouillage automatique : Tourelle Automatique",
                ["UiSamSiteLockEnabled"] = "Verrouillage automatique : Site SAM",
                ["UiTrapsLockEnabled"] = "Verrouillage automatique : Pièges",
                ["UiWeaponRackLockEnabled"] = "Verrouillage automatique : Râtelier d'Armes",
                ["UiStashLockEnabled"] = "Verrouillage automatique : Cachette",
                ["UiNeonSignLockEnabled"] = "Verrouillage automatique : Enseigne Lumineuse",
                ["UiOtherLockableEntitiesLockEnabled"] = "Verrouillage automatique : Autres Entités Verrouillables",
                ["UiOtherCustomEntitiesLockEnabled"] = "Verrouillage automatique : Autres Entités Personnalisées",
                ["UiOpenAutoClosingButtonText"] = "Paramètres de Fermeture Automatique",
                ["UiAutoClosingHeaderTitle"] = "Paramètres de Fermeture Automatique",
                ["UiAutoClosingEnabledTitle"] = "Fermeture Automatique Activée",
                ["UiClosingDelay"] = "Délai de Fermeture (s)",
                ["UiDoorAutoClosingEnabled"] = "Fermeture Automatique : Porte",
                ["UiDoubleDoorAutoClosingEnabled"] = "Fermeture Automatique : Double Porte",
                ["UiWindowsAutoClosingEnabled"] = "Fermeture Automatique : Fenêtres",
                ["UiGarageAutoClosingEnabled"] = "Fermeture Automatique : Garage",
                ["UiLadderHatchAutoClosingEnabled"] = "Fermeture Automatique : Trappe d'Échelle",
                ["UiExternalGateAutoClosingEnabled"] = "Fermeture Automatique : Portail Externe",
                ["UiFenceGateAutoClosingEnabled"] = "Fermeture Automatique : Portail de Clôture",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Fermeture Automatique : Porte Abri en Bois Ancien"
            }, this, "fr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "GESCHLOSSEN",
                ["LockerStatus_Unlocked"] = "ENTRIEGELT",
                ["LockerStatus_Removed"] = "ENTFERNT",
                ["LockerStatus_Updated"] = "AKTUALISIERT",
                ["NoCodeLockAuth"] = "Es ist blockiert...",
                ["NoPermissions"] = "<color=red>Sie haben keine Berechtigung, diesen Befehl zu verwenden.</color>",
                ["NoBypassPermissions"] = "<color=red>Sie haben keine Berechtigung, den Code zu umgehen.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID ist nicht der Besitzer der Entität.</color>",
                ["NoEntityOwner"] = "<color=red>Sie besitzen diese Entität nicht.</color>",
                ["PlayerNotFound"] = "<color=red>Der Spieler konnte nicht gefunden werden.</color>",
                ["InvalidSteamID"] = "<color=red>Ungültige SteamID.</color>",
                ["CodeLockNotFound"] = "<color=red>CodeLock oder Key Lock konnte nicht gefunden werden.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Der Code Lock hat keinen Code.</color>",
                ["EntityNotAllowed"] = "<color=red>Nicht erlaubt auf dieser Entität.</color>",
                ["LockCodeNotValid"] = "<color=red>Schlosscode ist ungültig (erfordert 4 Zahlen).</color>",
                ["MissingParametersError"] = "<color=red>Es ist ein Fehler aufgetreten: Fehlende Parameter - [{0}].</color>",
                ["GenericError"] = "<color=red>Es ist ein Fehler aufgetreten: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Verzögerungszeit ungültig: Bitte geben Sie einen Wert zwischen {0} und {1} ein.</color>",
                ["EntityNotFound"] = "<color=red>Sie müssen eine Entität ansehen.</color>",
                ["DoorCloserNotFound"] = "<color=red>Die Entität hat keinen Türschließer.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>Sie haben keine Berechtigung oder der Türschließer ist nicht aktiviert.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>Sie haben den Türschließer nicht aktiviert.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>Sie haben den Türschließer nicht in Ihrem Inventar.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Ungültige Türschließer-Kategorie.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Ein Türschließer ist bereits auf dieser Entität platziert.</color>",
                ["LockCategoryNotValid"] = "<color=red>Ungültige Schlosskategorie.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>Sie haben keine Berechtigung oder die automatische Verriegelung ist nicht aktiviert.</color>",
                ["LockItemNotFound"] = "<color=red>Sie haben kein Schloss in Ihrem Inventar.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>Du hast kein Schloss in deinem Inventar.\nDu kannst nur Schlösser ohne Code verwenden</color>",
                ["ShowCodeLockCodeMessage"] = "Der Code des Code Locks lautet:\nCode: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Der Code des Code Locks lautet:\nCode: <color=#faa511>{0}</color>\nGast-Code: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Streamer-Modus aktiviert:</color> Code kann nicht angezeigt werden",
                ["CodeLockPinChangedMessage"] = "Code Lock Pin geändert.",
                ["AllCodeLockPinChangedMessage"] = "Alle Code Lock Pins wurden geändert.",
                ["AllGuestCodeLockPinChangedMessage"] = "Alle Gast-Code Lock Pins wurden geändert.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Alle Gast-Code Lock Pins wurden entfernt.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Alle Berechtigungen zum Teilen von Code Locks mit Ihrem Clan/Team wurden geändert.",
                ["AutoLockKeyLockDeployedMessage"] = "Key Lock bereitgestellt und verriegelt.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock bereitgestellt und verriegelt\nCode: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Code Lock bereitgestellt und verriegelt\nCode: <color=#faa511>{0}</color>\nGast-Code: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Pin Code Lock: <color=red>{0}</color>. Sie können <color=red>den Pin ändern</color> mit dem Modularen Autoaufzug.",
                ["CodeLockListMessage"] = "Sie haben {0} {1} Code Locks / Schlösser.",
                ["CodeLockMessage"] = "Sie haben {0} ein Code Lock / Schlösser.",
                ["LockClosedAutomaticallyMessage"] = "Das Schloss wurde automatisch geschlossen.",
                ["SingleDoorCloserCustomTime"] = "Sie haben die Zeit für diesen Türschließer auf <color=#faa511>{0}s</color> eingestellt.",
                ["DoorCloserListMessage"] = "Sie haben {0} {1} Türschließer.",
                ["DoorCloserDeployed"] = "Türschließer bereitgestellt. Verzögerte Schließzeit festgelegt: <color=#faa511>{0}s</color>",
                ["UiButton_Yes"] = "JA",
                ["UiButton_No"] = "NEIN",
                ["UiHeaderTitle"] = "Automatische Verriegelungseinstellungen",
                ["UiShareLocksWithClanTeamEnabled"] = "Schlösser mit Clan/Team teilen",
                ["UiAutoLockOnPlacementEnabled"] = "Automatische Verriegelung",
                ["UiCodeLockPin"] = "Code Lock Pin",
                ["UiGuestCodeEnabled"] = "Gast-Code Lock",
                ["UiGuestCodeLockPin"] = "Gast-Code Lock Pin",
                ["UiUpdateAll"] = "ALLE AKTUALISIEREN",
                ["UiRemoveAll"] = "ALLE ENTFERNEN",
                ["UiAlsoUseKeyLockEnabled"] = "Auch Key Lock verwenden",
                ["UiStreamerModeEnabled"] = "Streamer-Modus",
                ["UiDoorsLockEnabled"] = "Automatische Verriegelung: Türen",
                ["UiWindowsLockEnabled"] = "Automatische Verriegelung: Fenster",
                ["UiBoxesLockEnabled"] = "Automatische Verriegelung: Kisten",
                ["UiStorageContainerLockEnabled"] = "Automatische Verriegelung: Alle Lagerbehälter",
                ["UiLockersLockEnabled"] = "Automatische Verriegelung: Schließfächer",
                ["UiCupboardsLockEnabled"] = "Automatische Verriegelung: Schränke (TC)",
                ["UiVehicleLockEnabled"] = "Automatische Verriegelung: Fahrzeug",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Automatisches Schließen beim Absteigen vom Fahrzeug",
                ["UiMedievalEntityLockEnabled"] = "Automatische Verriegelung: Mittelalterliche Entitäten",
                ["UiFurnaceLockEnabled"] = "Automatische Verriegelung: Ofen / Raffinerien",
                ["UiVendingMachineLockEnabled"] = "Automatische Verriegelung: Verkaufsautomat",
                ["UiComposterLockEnabled"] = "Automatische Verriegelung: Komposter",
                ["UiMixingTableLockEnabled"] = "Automatische Verriegelung: Mischpult",
                ["UiPlanterLockEnabled"] = "Automatische Verriegelung: Pflanzgefäß",
                ["UiAutoTurretLockEnabled"] = "Automatische Verriegelung: Auto-Geschütz",
                ["UiSamSiteLockEnabled"] = "Automatische Verriegelung: SAM-Site",
                ["UiTrapsLockEnabled"] = "Automatische Verriegelung: Fallen",
                ["UiWeaponRackLockEnabled"] = "Automatische Verriegelung: Waffenständer",
                ["UiStashLockEnabled"] = "Automatische Verriegelung: Versteck",
                ["UiNeonSignLockEnabled"] = "Automatische Verriegelung: Neon-Schild",
                ["UiOtherLockableEntitiesLockEnabled"] = "Automatische Verriegelung: Andere verriegelbare Entitäten",
                ["UiOtherCustomEntitiesLockEnabled"] = "Automatische Verriegelung: Andere benutzerdefinierte Entitäten",
                ["UiOpenAutoClosingButtonText"] = "Auto-Schließ-Einstellungen",
                ["UiAutoClosingHeaderTitle"] = "Auto-Schließ-Einstellungen",
                ["UiAutoClosingEnabledTitle"] = "Auto-Schließung aktiviert",
                ["UiClosingDelay"] = "Schließungsverzögerung (s)",
                ["UiDoorAutoClosingEnabled"] = "Automatische Schließung: Tür",
                ["UiDoubleDoorAutoClosingEnabled"] = "Automatische Schließung: Doppeltür",
                ["UiWindowsAutoClosingEnabled"] = "Automatische Schließung: Fenster",
                ["UiGarageAutoClosingEnabled"] = "Automatische Schließung: Garage",
                ["UiLadderHatchAutoClosingEnabled"] = "Automatische Schließung: Leiterklappe",
                ["UiExternalGateAutoClosingEnabled"] = "Automatische Schließung: Externe Tür",
                ["UiFenceGateAutoClosingEnabled"] = "Automatische Schließung: Zauntor",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Automatische Schließung: Tür des alten Holzunterstands"
            }, this, "de");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "GESLOTEN",
                ["LockerStatus_Unlocked"] = "ONTGRENDELD",
                ["LockerStatus_Removed"] = "VERWIJDERD",
                ["LockerStatus_Updated"] = "BIJGEWERKT",
                ["NoCodeLockAuth"] = "Het is geblokkeerd...",
                ["NoPermissions"] = "<color=red>Je hebt geen toestemming om deze opdracht te gebruiken.</color>",
                ["NoBypassPermissions"] = "<color=red>Je hebt geen toestemming om de code te omzeilen.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID is niet de eigenaar van het object.</color>",
                ["NoEntityOwner"] = "<color=red>Je bent niet de eigenaar van dit object.</color>",
                ["PlayerNotFound"] = "<color=red>De speler kon niet worden gevonden.</color>",
                ["InvalidSteamID"] = "<color=red>Ongeldige SteamID.</color>",
                ["CodeLockNotFound"] = "<color=red>CodeLock of Key Lock kon niet worden gevonden.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Code Lock heeft geen code.</color>",
                ["EntityNotAllowed"] = "<color=red>Niet toegestaan op dit object.</color>",
                ["LockCodeNotValid"] = "<color=red>Slotcode is niet geldig (Vereist 4 cijfers).</color>",
                ["MissingParametersError"] = "<color=red>Er is een fout opgetreden: Ontbrekende parameters - [{0}].</color>",
                ["GenericError"] = "<color=red>Er is een fout opgetreden: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Vertragingstijd niet geldig: Voer een waarde in tussen {0} en {1}.</color>",
                ["EntityNotFound"] = "<color=red>Je moet naar een object kijken.</color>",
                ["DoorCloserNotFound"] = "<color=red>Het object heeft geen Deursluiter.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>Je hebt geen toestemming of Deursluiter is niet ingeschakeld.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>Je hebt deursluiter niet ingeschakeld.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>Je hebt de Deursluiter niet in je inventaris.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Ongeldige categorie Deursluiter.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Er is al een Deursluiter geplaatst op dit object.</color>",
                ["LockCategoryNotValid"] = "<color=red>Ongeldige slotcategorie.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>Je hebt geen toestemming of automatisch slot is niet ingeschakeld.</color>",
                ["LockItemNotFound"] = "<color=red>Je hebt geen slot in je inventaris.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>Je hebt geen slot in je inventaris.\nJe kunt alleen sloten zonder code gebruiken</color>",
                ["ShowCodeLockCodeMessage"] = "Code Lock code is:\nCode: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Code Lock code is:\nCode: <color=#faa511>{0}</color>\nGastcode: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Streamermodus actief:</color> code kan niet worden bekeken",
                ["CodeLockPinChangedMessage"] = "Code Lock Pin gewijzigd.",
                ["AllCodeLockPinChangedMessage"] = "Alle Code Lock Pins zijn gewijzigd.",
                ["AllGuestCodeLockPinChangedMessage"] = "Alle Gast Code Lock Pins zijn gewijzigd.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Alle Gast Code Lock Pins zijn verwijderd.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Alle rechten voor het delen van Code Locks met je clan/team zijn gewijzigd.",
                ["AutoLockKeyLockDeployedMessage"] = "Sleutelslot geplaatst en vergrendeld.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock geplaatst en vergrendeld\nCode: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] = "Code Lock geplaatst en vergrendeld\nCode: <color=#faa511>{0}</color>\nGastcode: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Pincode slot: <color=red>{0}</color>. Je kunt <color=red>de pincode wijzigen</color> met de Modulaire Autolift.",
                ["CodeLockListMessage"] = "Je hebt {0} {1} Code Locks / sleutelsloten.",
                ["CodeLockMessage"] = "Je hebt {0} een Code Lock / sleutelsloten.",
                ["LockClosedAutomaticallyMessage"] = "Het slot werd automatisch gesloten.",
                ["SingleDoorCloserCustomTime"] = "Je hebt de tijd voor deze Deursluiter ingesteld op <color=#faa511>{0}s</color>.",
                ["DoorCloserListMessage"] = "Je hebt {0} {1} Deursluiter.",
                ["DoorCloserDeployed"] = "Deursluiter geplaatst. Vertraagde sluitingstijd ingesteld: <color=#faa511>{0}s</color>",
                ["UiButton_Yes"] = "JA",
                ["UiButton_No"] = "NEE",
                ["UiHeaderTitle"] = "Automatische slotinstellingen",
                ["UiShareLocksWithClanTeamEnabled"] = "Deel sloten met Clan/Team",
                ["UiAutoLockOnPlacementEnabled"] = "Automatisch slot",
                ["UiCodeLockPin"] = "Code Lock Pin",
                ["UiGuestCodeEnabled"] = "Gastcode slot",
                ["UiGuestCodeLockPin"] = "Gastcode slot Pin",
                ["UiUpdateAll"] = "ALLE BIJWERKEN",
                ["UiRemoveAll"] = "ALLE VERWIJDEREN",
                ["UiAlsoUseKeyLockEnabled"] = "Ook Sleutelslot gebruiken",
                ["UiStreamerModeEnabled"] = "Streamermodus",
                ["UiDoorsLockEnabled"] = "Automatisch slot: Deuren",
                ["UiWindowsLockEnabled"] = "Automatisch slot: Ramen",
                ["UiBoxesLockEnabled"] = "Automatisch slot: Dozen",
                ["UiStorageContainerLockEnabled"] = "Automatisch slot: Alle Opslagcontainers",
                ["UiLockersLockEnabled"] = "Automatisch slot: Kluisjes",
                ["UiCupboardsLockEnabled"] = "Automatisch slot: Kasten (TC)",
                ["UiVehicleLockEnabled"] = "Automatisch slot: Voertuig",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Automatische slotvergrendeling bij afstappen van het voertuig",
                ["UiMedievalEntityLockEnabled"] = "Automatisch slot: Middeleeuwse objecten",
                ["UiFurnaceLockEnabled"] = "Automatisch slot: Oven / Raffinaderijen",
                ["UiVendingMachineLockEnabled"] = "Automatisch slot: Verkoopautomaat",
                ["UiComposterLockEnabled"] = "Automatisch slot: Composter",
                ["UiMixingTableLockEnabled"] = "Automatisch slot: Mengtafel",
                ["UiPlanterLockEnabled"] = "Automatisch slot: Planter",
                ["UiAutoTurretLockEnabled"] = "Automatisch slot: Autoturret",
                ["UiSamSiteLockEnabled"] = "Automatisch slot: SAM-site",
                ["UiTrapsLockEnabled"] = "Automatisch slot: Vallen",
                ["UiWeaponRackLockEnabled"] = "Automatisch slot: Wapenrek",
                ["UiStashLockEnabled"] = "Automatisch slot: Verstopplaats",
                ["UiNeonSignLockEnabled"] = "Automatisch slot: Neonbord",
                ["UiOtherLockableEntitiesLockEnabled"] = "Automatisch slot: Andere vergrendelbare objecten",
                ["UiOtherCustomEntitiesLockEnabled"] = "Automatisch slot: Andere aangepaste objecten",
                ["UiOpenAutoClosingButtonText"] = "Automatische sluitingsinstellingen",
                ["UiAutoClosingHeaderTitle"] = "Automatische sluitingsinstellingen",
                ["UiAutoClosingEnabledTitle"] = "Automatische sluiting ingeschakeld",
                ["UiClosingDelay"] = "Sluitingsvertraging (s)",
                ["UiDoorAutoClosingEnabled"] = "Automatische sluiting: Deur",
                ["UiDoubleDoorAutoClosingEnabled"] = "Automatische sluiting: Dubbele deur",
                ["UiWindowsAutoClosingEnabled"] = "Automatische sluiting: Ramen",
                ["UiGarageAutoClosingEnabled"] = "Automatische sluiting: Garage",
                ["UiLadderHatchAutoClosingEnabled"] = "Automatische sluiting: Ladderluik",
                ["UiExternalGateAutoClosingEnabled"] = "Automatische sluiting: Externe poort",
                ["UiFenceGateAutoClosingEnabled"] = "Automatische sluiting: Hekpoort",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Automatische sluiting: Deur van houten schuilplaats uit het verleden"
            }, this, "nl");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "KİLİTLİ",
                ["LockerStatus_Unlocked"] = "KİLİTSİZ",
                ["LockerStatus_Removed"] = "KALDIRILMIŞ",
                ["LockerStatus_Updated"] = "GÜNCELLENDİ",
                ["NoCodeLockAuth"] = "Engellendi...",
                ["NoPermissions"] = "<color=red>Bu komutu kullanma izniniz yok.</color>",
                ["NoBypassPermissions"] = "<color=red>Kodu atlayacak izniniz yok.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID varlığın sahibi değil.</color>",
                ["NoEntityOwner"] = "<color=red>Bu varlığın sahibi değilsiniz.</color>",
                ["PlayerNotFound"] = "<color=red>Oyuncu bulunamadı.</color>",
                ["InvalidSteamID"] = "<color=red>Geçersiz SteamID.</color>",
                ["CodeLockNotFound"] = "<color=red>Kod Kilidi veya Anahtar Kilidi bulunamadı.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Kod Kilidinde kod bulunmuyor.</color>",
                ["EntityNotAllowed"] = "<color=red>Bu varlıkta izin verilmiyor.</color>",
                ["LockCodeNotValid"] = "<color=red>Kilit kodu geçerli değil (4 rakam gerektirir).</color>",
                ["MissingParametersError"] = "<color=red>Bir hata oluştu: Eksik parametreler - [{0}].</color>",
                ["GenericError"] = "<color=red>Bir hata oluştu: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Gecikme süresi geçerli değil: Lütfen {0} ve {1} arasında bir değer girin.</color>",
                ["EntityNotFound"] = "<color=red>Bir varlığa bakmalısınız.</color>",
                ["DoorCloserNotFound"] = "<color=red>Varlıkta Kapı Kapatıcı bulunamadı.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>İzin yok veya Kapı Kapatıcı etkin değil.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>Kapı Kapatıcı etkin değil.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>Envanterinizde Kapı Kapatıcı yok.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Geçersiz Kapı Kapatıcı kategorisi.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Bu varlıkta zaten bir Kapı Kapatıcı bulunuyor.</color>",
                ["LockCategoryNotValid"] = "<color=red>Geçersiz kilit kategorisi.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>İzin yok veya otomatik kilitleme etkin değil.</color>",
                ["LockItemNotFound"] = "<color=red>Envanterinizde kilit yok.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>Envanterinizde kilit yok.\nSadece kodsuz kilitleri kullanabilirsiniz</color>",
                ["ShowCodeLockCodeMessage"] = "Kod Kilidi kodu:\nKod: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Kod Kilidi kodu:\nKod: <color=#faa511>{0}</color>\nMisafir Kodu: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Yayıncı modu etkin:</color> kod görüntülenemez",
                ["CodeLockPinChangedMessage"] = "Kod Kilidi Pini değiştirildi.",
                ["AllCodeLockPinChangedMessage"] = "Tüm Kod Kilidi Pinleri değiştirildi.",
                ["AllGuestCodeLockPinChangedMessage"] = "Tüm Misafir Kod Kilidi Pinleri değiştirildi.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Tüm Misafir Kod Kilidi Pinleri kaldırıldı.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Klan/takımınızla Kod Kilidini paylaşma izinleri değiştirildi.",
                ["AutoLockKeyLockDeployedMessage"] = "Anahtar Kilidi yerleştirildi ve kilitlendi.",
                ["AutoLockCodeLockDeployedMessage"] = "Kod Kilidi yerleştirildi ve kilitlendi\nKod: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Kod Kilidi yerleştirildi ve kilitlendi\nKod: <color=#faa511>{0}</color>\nMisafir Kodu: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Pin Kod Kilidi: <color=red>{0}</color>. Modüler Araç Kaldırıcı ile <color=red>pini değiştirebilirsiniz</color>.",
                ["CodeLockListMessage"] = "Toplam {0} {1} Kod Kilidi / Anahtar Kilidi var.",
                ["CodeLockMessage"] = "Toplam {0} Kod Kilidi / Anahtar Kilidi var.",
                ["LockClosedAutomaticallyMessage"] = "Kilit otomatik olarak kapatıldı.",
                ["SingleDoorCloserCustomTime"] = "Bu Kapı Kapatıcı için zamanı <color=#faa511>{0}s</color> olarak ayarladınız.",
                ["DoorCloserListMessage"] = "Toplam {0} {1} Kapı Kapatıcı var.",
                ["DoorCloserDeployed"] = "Kapı kapatıcı yerleştirildi. Gecikmeli kapanma süresi ayarlandı: <color=#faa511>{0}s</color>",
                ["UiButton_Yes"] = "EVET",
                ["UiButton_No"] = "HAYIR",
                ["UiHeaderTitle"] = "Otomatik Kilit Ayarları",
                ["UiShareLocksWithClanTeamEnabled"] = "Klan/Takım ile Kilidi Paylaş",
                ["UiAutoLockOnPlacementEnabled"] = "Otomatik kilit",
                ["UiCodeLockPin"] = "Kod Kilidi Pini",
                ["UiGuestCodeEnabled"] = "Misafir Kod Kilidi",
                ["UiGuestCodeLockPin"] = "Misafir Kod Kilidi Pini",
                ["UiUpdateAll"] = "TÜMÜNÜ GÜNCELLE",
                ["UiRemoveAll"] = "TÜMÜNÜ KALDIR",
                ["UiAlsoUseKeyLockEnabled"] = "Ayrıca Anahtar Kilidi Kullan",
                ["UiStreamerModeEnabled"] = "Yayıncı Modu",
                ["UiDoorsLockEnabled"] = "Otomatik Kilit: Kapılar",
                ["UiWindowsLockEnabled"] = "Otomatik Kilit: Pencereler",
                ["UiBoxesLockEnabled"] = "Otomatik Kilit: Kutular",
                ["UiStorageContainerLockEnabled"] = "Otomatik Kilit: Tüm Depolama Kapları",
                ["UiLockersLockEnabled"] = "Otomatik Kilit: Dolaplar",
                ["UiCupboardsLockEnabled"] = "Otomatik Kilit: Dolaplar (TC)",
                ["UiVehicleLockEnabled"] = "Otomatik Kilit: Araç",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Araçtan İndiğinizde Otomatik Kilit Kapanması",
                ["UiMedievalEntityLockEnabled"] = "Otomatik Kilit: Ortaçağ Varlıkları",
                ["UiFurnaceLockEnabled"] = "Otomatik Kilit: Ocaklar / Rafineriler",
                ["UiVendingMachineLockEnabled"] = "Otomatik Kilit: Satış Makinesi",
                ["UiComposterLockEnabled"] = "Otomatik Kilit: Kompostlama Makinesi",
                ["UiMixingTableLockEnabled"] = "Otomatik Kilit: Karıştırma Masası",
                ["UiPlanterLockEnabled"] = "Otomatik Kilit: Saksı",
                ["UiAutoTurretLockEnabled"] = "Otomatik Kilit: Otomatik Taret",
                ["UiSamSiteLockEnabled"] = "Otomatik Kilit: SAM Site",
                ["UiTrapsLockEnabled"] = "Otomatik Kilit: Tuzaklar",
                ["UiWeaponRackLockEnabled"] = "Otomatik Kilit: Silah Askısı",
                ["UiStashLockEnabled"] = "Otomatik Kilit: Saklama Yeri",
                ["UiNeonSignLockEnabled"] = "Otomatik Kilit: Neon Işığı",
                ["UiOtherLockableEntitiesLockEnabled"] = "Otomatik Kilit: Diğer Kilitlenebilir Varlıklar",
                ["UiOtherCustomEntitiesLockEnabled"] = "Otomatik Kilit: Diğer Özel Varlıklar",
                ["UiOpenAutoClosingButtonText"] = "Otomatik Kapanma Ayarları",
                ["UiAutoClosingHeaderTitle"] = "Otomatik Kapanma Ayarları",
                ["UiAutoClosingEnabledTitle"] = "Otomatik Kapanma Etkin",
                ["UiClosingDelay"] = "Kapanma Gecikmesi (s)",
                ["UiDoorAutoClosingEnabled"] = "Otomatik Kapanma: Kapı",
                ["UiDoubleDoorAutoClosingEnabled"] = "Otomatik Kapanma: Çift Kapı",
                ["UiWindowsAutoClosingEnabled"] = "Otomatik Kapanma: Pencereler",
                ["UiGarageAutoClosingEnabled"] = "Otomatik Kapanma: Garaj",
                ["UiLadderHatchAutoClosingEnabled"] = "Otomatik Kapanma: Merdiven Kapısı",
                ["UiExternalGateAutoClosingEnabled"] = "Otomatik Kapanma: Harici Kapı",
                ["UiFenceGateAutoClosingEnabled"] = "Otomatik Kapanma: Çit Kapısı",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Otomatik Kapanma: Eski Ahşap Barınak Kapısı"
            }, this, "tr");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "ЗАБЛОКИРОВАН",
                ["LockerStatus_Unlocked"] = "РАЗБЛОКИРОВАН",
                ["LockerStatus_Removed"] = "УДАЛЕН",
                ["LockerStatus_Updated"] = "ОБНОВЛЕН",
                ["NoCodeLockAuth"] = "Он заблокирован...",
                ["NoPermissions"] = "<color=red>У вас нет разрешения на использование этой команды.</color>",
                ["NoBypassPermissions"] = "<color=red>У вас нет разрешения на обход кода.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID не является владельцем объекта.</color>",
                ["NoEntityOwner"] = "<color=red>Вы не являетесь владельцем этого объекта.</color>",
                ["PlayerNotFound"] = "<color=red>Игрок не может быть найден.</color>",
                ["InvalidSteamID"] = "<color=red>Недопустимый SteamID.</color>",
                ["CodeLockNotFound"] = "<color=red>Кодовый замок или ключевой замок не может быть найден.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Кодовый замок не имеет кода.</color>",
                ["EntityNotAllowed"] = "<color=red>Не разрешено для этого объекта.</color>",
                ["LockCodeNotValid"] = "<color=red>Код замка недействителен (Требуется 4 цифры).</color>",
                ["MissingParametersError"] = "<color=red>Произошла ошибка: Недостающие параметры - [{0}].</color>",
                ["GenericError"] = "<color=red>Произошла ошибка: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Недопустимое время задержки: Пожалуйста, введите значение между {0} и {1}.</color>",
                ["EntityNotFound"] = "<color=red>Вы должны посмотреть на объект.</color>",
                ["DoorCloserNotFound"] = "<color=red>Объект не имеет дверной доводчик.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>У вас нет разрешения или дверной доводчик не включен.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>У вас не включен дверной доводчик.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>У вас нет дверного доводчика в вашем инвентаре.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Недопустимая категория дверного доводчика.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Дверной доводчик уже установлен на этом объекте.</color>",
                ["LockCategoryNotValid"] = "<color=red>Недопустимая категория замка.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>У вас нет разрешения или автозамок не включен.</color>",
                ["LockItemNotFound"] = "<color=red>У вас нет замка в вашем инвентаре.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>В вашем инвентаре нет замка.\nВы можете использовать только замки без кода</color>",
                ["ShowCodeLockCodeMessage"] = "Код замка:\nКод: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Код замка:\nКод: <color=#faa511>{0}</color>\nГостевой код: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Режим стримера активен:</color> код не может быть просмотрен",
                ["CodeLockPinChangedMessage"] = "Пин-код замка изменен.",
                ["AllCodeLockPinChangedMessage"] = "Все пин-коды замков изменены.",
                ["AllGuestCodeLockPinChangedMessage"] = "Все гостевые пин-коды замков изменены.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Все гостевые пин-коды замков удалены.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Все разрешения на обмен кодовыми замками с вашим кланом/командой изменены.",
                ["AutoLockKeyLockDeployedMessage"] = "Установлен и заблокирован ключевой замок.",
                ["AutoLockCodeLockDeployedMessage"] = "Установлен и заблокирован кодовый замок\nКод: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Установлен и заблокирован кодовый замок\nКод: <color=#faa511>{0}</color>\nГостевой код: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] =
                    "Пин-код замка: <color=red>{0}</color>. Вы можете <color=red>изменить пин</color> с помощью модульного автомобильного подъемника.",
                ["CodeLockListMessage"] = "У вас {0} {1} кодовых замков / ключевых замков.",
                ["CodeLockMessage"] = "У вас {0} кодовый замок / ключевые замки.",
                ["LockClosedAutomaticallyMessage"] = "Замок был закрыт автоматически.",
                ["SingleDoorCloserCustomTime"] = "Вы установили время для этого дверного доводчика на <color=#faa511>{0}с</color>.",
                ["DoorCloserListMessage"] = "У вас {0} {1} дверных доводчиков.",
                ["DoorCloserDeployed"] = "Дверной доводчик установлен. Установлено время задержки закрытия: <color=#faa511>{0}с</color>",
                ["UiButton_Yes"] = "ДА",
                ["UiButton_No"] = "НЕТ",
                ["UiHeaderTitle"] = "Настройки автозамка",
                ["UiShareLocksWithClanTeamEnabled"] = "Поделиться замками с кланом/командой",
                ["UiAutoLockOnPlacementEnabled"] = "Автозамок",
                ["UiCodeLockPin"] = "Пин-код замка",
                ["UiGuestCodeEnabled"] = "Гостевой код замка",
                ["UiGuestCodeLockPin"] = "Гостевой пин-код замка",
                ["UiUpdateAll"] = "ОБНОВИТЬ ВСЕ",
                ["UiRemoveAll"] = "УДАЛИТЬ ВСЕ",
                ["UiAlsoUseKeyLockEnabled"] = "Также используйте ключевой замок",
                ["UiStreamerModeEnabled"] = "Режим стримера",
                ["UiDoorsLockEnabled"] = "Автозамок: Двери",
                ["UiWindowsLockEnabled"] = "Автозамок: Окна",
                ["UiBoxesLockEnabled"] = "Автозамок: Ящики",
                ["UiStorageContainerLockEnabled"] = "Автозамок: Все контейнеры для хранения",
                ["UiLockersLockEnabled"] = "Автозамок: Шкафы",
                ["UiCupboardsLockEnabled"] = "Автозамок: Шкафы (ТС)",
                ["UiVehicleLockEnabled"] = "Автозамок: Транспортное средство",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Автоматическое закрытие замка при спуске с транспортного средства",
                ["UiMedievalEntityLockEnabled"] = "Автозамок: Средневековые объекты",
                ["UiFurnaceLockEnabled"] = "Автозамок: Печь / Рефайнеры",
                ["UiVendingMachineLockEnabled"] = "Автозамок: Торговый автомат",
                ["UiComposterLockEnabled"] = "Автозамок: Компостер",
                ["UiMixingTableLockEnabled"] = "Автозамок: Смесительный стол",
                ["UiPlanterLockEnabled"] = "Автозамок: Горшок",
                ["UiAutoTurretLockEnabled"] = "Автозамок: Авто турель",
                ["UiSamSiteLockEnabled"] = "Автозамок: Станция ПВО",
                ["UiTrapsLockEnabled"] = "Автозамок: Ловушки",
                ["UiWeaponRackLockEnabled"] = "Автозамок: Стойка для оружия",
                ["UiStashLockEnabled"] = "Автозамок: Укрытие",
                ["UiNeonSignLockEnabled"] = "Автозамок: Неоновый знак",
                ["UiOtherLockableEntitiesLockEnabled"] = "Автозамок: Другие объекты с замками",
                ["UiOtherCustomEntitiesLockEnabled"] = "Автозамок: Другие пользовательские объекты",
                ["UiOpenAutoClosingButtonText"] = "Настройки автоматического закрытия",
                ["UiAutoClosingHeaderTitle"] = "Настройки автоматического закрытия",
                ["UiAutoClosingEnabledTitle"] = "Автоматическое закрытие включено",
                ["UiClosingDelay"] = "Задержка закрытия (с)",
                ["UiDoorAutoClosingEnabled"] = "Автоматическое закрытие: Дверь",
                ["UiDoubleDoorAutoClosingEnabled"] = "Автоматическое закрытие: Двойная дверь",
                ["UiWindowsAutoClosingEnabled"] = "Автоматическое закрытие: Окна",
                ["UiGarageAutoClosingEnabled"] = "Автоматическое закрытие: Гараж",
                ["UiLadderHatchAutoClosingEnabled"] = "Автоматическое закрытие: Лестничная люк",
                ["UiExternalGateAutoClosingEnabled"] = "Автоматическое закрытие: Внешние ворота",
                ["UiFenceGateAutoClosingEnabled"] = "Автоматическое закрытие: Ворота из забора",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Автоматическое закрытие: Дверь старого деревянного укрытия"
            }, this, "ru");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "ЗАБЛОКОВАНО",
                ["LockerStatus_Unlocked"] = "РОЗБЛОКОВАНО",
                ["LockerStatus_Removed"] = "ВИДАЛЕНО",
                ["LockerStatus_Updated"] = "ОНОВЛЕНО",
                ["NoCodeLockAuth"] = "Це заблоковано...",
                ["NoPermissions"] = "<color=red>У вас немає дозволу використовувати цю команду.</color>",
                ["NoBypassPermissions"] = "<color=red>У вас немає дозволу обійти код.</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID не є власником об'єкту.</color>",
                ["NoEntityOwner"] = "<color=red>Ви не володієте цим об'єктом.</color>",
                ["PlayerNotFound"] = "<color=red>Гравця не вдалося знайти.</color>",
                ["InvalidSteamID"] = "<color=red>Недійсний SteamID.</color>",
                ["CodeLockNotFound"] = "<color=red>Кодовий замок або ключовий замок не вдалося знайти.</color>",
                ["CodeLockCodeNotFound"] = "<color=red>Кодовий замок не має коду.</color>",
                ["EntityNotAllowed"] = "<color=red>Не дозволяється на цьому об'єкті.</color>",
                ["LockCodeNotValid"] = "<color=red>Код замка недійсний (потрібно 4 цифри).</color>",
                ["MissingParametersError"] = "<color=red>Виникла помилка: Відсутні параметри - [{0}].</color>",
                ["GenericError"] = "<color=red>Виникла помилка: [{0}].</color>",
                ["DelayTimeNotValid"] = "<color=red>Недійсний час затримки: Будь ласка, введіть значення між {0} та {1}.</color>",
                ["EntityNotFound"] = "<color=red>Вам потрібно подивитися на об'єкт.</color>",
                ["DoorCloserNotFound"] = "<color=red>У об'єкта немає дверного захисника.</color>",
                ["AutoClosingNotEnabled"] = "<color=red>У вас немає дозволу або дверний захисник не увімкнено.</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>У вас немає дозволу на дверний захисник.</color>",
                ["DoorCloserItemNotFound"] = "<color=red>У вас немає дверного захисника в інвентарі.</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>Недійсна категорія дверного захисника.</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>Дверний захисник вже встановлено на цьому об'єкті.</color>",
                ["LockCategoryNotValid"] = "<color=red>Недійсна категорія замка.</color>",
                ["AutoLockerNotEnabled"] = "<color=red>У вас немає дозволу або автоматичне блокування не увімкнено.</color>",
                ["LockItemNotFound"] = "<color=red>У вас немає замка в інвентарі.</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>У вашому інвентарі немає замка.\nВи можете використовувати замки лише без коду</color>",
                ["ShowCodeLockCodeMessage"] = "Код замка: \nКод: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Код замка: \nКод: <color=#faa511>{0}</color>\nГостьовий код: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Активований режим стрімера:</color> код не може бути переглянутий",
                ["CodeLockPinChangedMessage"] = "Змінено код PIN замка.",
                ["AllCodeLockPinChangedMessage"] = "Всі коди PIN замків було змінено.",
                ["AllGuestCodeLockPinChangedMessage"] = "Всі гостьові коди PIN замків було змінено.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Всі гостьові коди PIN замків було видалено.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Всі дозволи для спільного використання замків були змінені.",
                ["AutoLockKeyLockDeployedMessage"] = "Ключовий замок встановлено і заблоковано.",
                ["AutoLockCodeLockDeployedMessage"] = "Кодовий замок встановлено і заблоковано\nКод: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Кодовий замок встановлено і заблоковано\nКод: <color=#faa511>{0}</color>\nГостьовий код: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] =
                    "Кодовий замок: <color=red>{0}</color>. Ви можете <color=red>змінити код</color> з допомогою модульного автомобільного підйомника.",
                ["CodeLockListMessage"] = "У вас є {0} {1} кодових замків / ключових замків.",
                ["CodeLockMessage"] = "У вас є {0} кодовий замок / ключові замки.",
                ["LockClosedAutomaticallyMessage"] = "Замок автоматично закрито.",
                ["SingleDoorCloserCustomTime"] = "Ви встановили час для цього дверного захисника на <color=#faa511>{0}с</color>.",
                ["DoorCloserListMessage"] = "У вас є {0} {1} дверних захисників.",
                ["DoorCloserDeployed"] = "Дверний захисник встановлено. Встановлено затримку закриття: <color=#faa511>{0}с</color>",
                ["UiButton_Yes"] = "ТАК",
                ["UiButton_No"] = "НІ",
                ["UiHeaderTitle"] = "Налаштування автоблокування",
                ["UiShareLocksWithClanTeamEnabled"] = "Поділитися замками з кланом/командою",
                ["UiAutoLockOnPlacementEnabled"] = "Автоматичне блокування",
                ["UiCodeLockPin"] = "PIN-код кодового замка",
                ["UiGuestCodeEnabled"] = "Гостьовий кодовий замок",
                ["UiGuestCodeLockPin"] = "PIN-код гостьового кодового замка",
                ["UiUpdateAll"] = "ОНОВИТИ ВСЕ",
                ["UiRemoveAll"] = "ВИДАЛИТИ ВСЕ",
                ["UiAlsoUseKeyLockEnabled"] = "Також використовувати ключовий замок",
                ["UiStreamerModeEnabled"] = "Режим стрімера",
                ["UiDoorsLockEnabled"] = "Автоблокування: Двері",
                ["UiWindowsLockEnabled"] = "Автоблокування: Вікна",
                ["UiBoxesLockEnabled"] = "Автоблокування: Ящики",
                ["UiStorageContainerLockEnabled"] = "Автоблокування: Всі контейнери для зберігання",
                ["UiLockersLockEnabled"] = "Автоблокування: Шафи",
                ["UiCupboardsLockEnabled"] = "Автоблокування: Шафа (TC)",
                ["UiVehicleLockEnabled"] = "Автоблокування: Транспортний засіб",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "Автоматичне закриття блокування при зліті з транспортного засобу",
                ["UiMedievalEntityLockEnabled"] = "Автоблокування: Середньовічні об'єкти",
                ["UiFurnaceLockEnabled"] = "Автоблокування: Піч / Рафінерії",
                ["UiVendingMachineLockEnabled"] = "Автоблокування: Торговий автомат",
                ["UiComposterLockEnabled"] = "Автоблокування: Компостер",
                ["UiMixingTableLockEnabled"] = "Автоблокування: Стіл для змішування",
                ["UiPlanterLockEnabled"] = "Автоблокування: Горщик",
                ["UiAutoTurretLockEnabled"] = "Автоблокування: Авто турель",
                ["UiSamSiteLockEnabled"] = "Автоблокування: SAM-станція",
                ["UiTrapsLockEnabled"] = "Автоблокування: Пастки",
                ["UiWeaponRackLockEnabled"] = "Автоблокування: Стійка для зброї",
                ["UiStashLockEnabled"] = "Автоблокування: Сховище",
                ["UiNeonSignLockEnabled"] = "Автоблокування: Неоновий знак",
                ["UiOtherLockableEntitiesLockEnabled"] = "Автоблокування: Інші об'єкти, які можна заблокувати",
                ["UiOtherCustomEntitiesLockEnabled"] = "Автоблокування: Інші користувацькі об'єкти",
                ["UiOpenAutoClosingButtonText"] = "Налаштування автоматичного закриття",
                ["UiAutoClosingHeaderTitle"] = "Налаштування автоматичного закриття",
                ["UiAutoClosingEnabledTitle"] = "Автоматичне закриття увімкнено",
                ["UiClosingDelay"] = "Затримка закриття (с)",
                ["UiDoorAutoClosingEnabled"] = "Автозакривання: Двері",
                ["UiDoubleDoorAutoClosingEnabled"] = "Автозакривання: Подвійні двері",
                ["UiWindowsAutoClosingEnabled"] = "Автозакривання: Вікна",
                ["UiGarageAutoClosingEnabled"] = "Автозакривання: Гараж",
                ["UiLadderHatchAutoClosingEnabled"] = "Автозакривання: Драбина люка",
                ["UiExternalGateAutoClosingEnabled"] = "Автозакривання: Зовнішні ворота",
                ["UiFenceGateAutoClosingEnabled"] = "Автозакривання: Ворота огорожі",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Автозакривання: Двері легасі дерев'яної будки"
            }, this, "uk");

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "已锁定",
                ["LockerStatus_Unlocked"] = "未锁定",
                ["LockerStatus_Removed"] = "已移除",
                ["LockerStatus_Updated"] = "已更新",
                ["NoCodeLockAuth"] = "已阻止...",
                ["NoPermissions"] = "<color=red>您没有使用此命令的权限。</color>",
                ["NoBypassPermissions"] = "<color=red>您没有绕过密码的权限。</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID不是实体的所有者。</color>",
                ["NoEntityOwner"] = "<color=red>您不拥有此实体。</color>",
                ["PlayerNotFound"] = "<color=red>找不到玩家。</color>",
                ["InvalidSteamID"] = "<color=red>无效的SteamID。</color>",
                ["CodeLockNotFound"] = "<color=red>找不到密码锁或钥匙锁。</color>",
                ["CodeLockCodeNotFound"] = "<color=red>密码锁没有密码。</color>",
                ["EntityNotAllowed"] = "<color=red>不允许在此实体上执行此操作。</color>",
                ["LockCodeNotValid"] = "<color=red>锁定代码无效（需要4位数字）。</color>",
                ["MissingParametersError"] = "<color=red>发生错误：缺少参数 - [{0}]。</color>",
                ["GenericError"] = "<color=red>发生错误：[{0}]。</color>",
                ["DelayTimeNotValid"] = "<color=red>延迟时间无效：请输入{0}和{1}之间的值。</color>",
                ["EntityNotFound"] = "<color=red>您必须查看一个实体。</color>",
                ["DoorCloserNotFound"] = "<color=red>实体没有门闭器。</color>",
                ["AutoClosingNotEnabled"] = "<color=red>您没有权限或门闭器未启用。</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>您未启用门闭器。</color>",
                ["DoorCloserItemNotFound"] = "<color=red>您的库存中没有门闭器。</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>无效的门闭器类别。</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>此实体上已放置了门闭器。</color>",
                ["LockCategoryNotValid"] = "<color=red>无效的锁定类别。</color>",
                ["AutoLockerNotEnabled"] = "<color=red>您没有权限或自动锁定未启用。</color>",
                ["LockItemNotFound"] = "<color=red>您的库存中没有锁。</color>",
                ["UseOnlyKeyLock_KeyLockNotFound"] = "<color=red>您的库存中没有锁。\n您只能使用没有密码的锁</color>",
                ["ShowCodeLockCodeMessage"] = "密码锁代码是：\n代码：<color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "密码锁代码是：\n代码：<color=#faa511>{0}</color>\n访客代码：<color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>流媒体模式激活：</color>无法查看代码",
                ["CodeLockPinChangedMessage"] = "密码锁代码已更改。",
                ["AllCodeLockPinChangedMessage"] = "所有密码锁代码已更改。",
                ["AllGuestCodeLockPinChangedMessage"] = "所有访客密码锁代码已更改。",
                ["AllGuestCodeLockPinRemovedMessage"] = "所有访客密码锁代码已移除。",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "与您的氏族/团队共享密码锁的所有权限已更改。",
                ["AutoLockKeyLockDeployedMessage"] = "钥匙锁已部署并锁定。",
                ["AutoLockCodeLockDeployedMessage"] = "密码锁已部署并锁定\n代码：<color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] = "密码锁已部署并锁定\n代码：<color=#faa511>{0}</color>\n访客代码：<color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "Pin密码锁：<color=red>{0}</color>。您可以使用模块化汽车升降机<color=red>更改密码</color>。",
                ["CodeLockListMessage"] = "您有{0}个{1}密码锁/钥匙锁。",
                ["CodeLockMessage"] = "您有{0}个密码锁/钥匙锁。",
                ["LockClosedAutomaticallyMessage"] = "锁已自动关闭。",
                ["SingleDoorCloserCustomTime"] = "您已将此门闭器的时间设置为<color=#faa511>{0}秒</color>。",
                ["DoorCloserListMessage"] = "您有{0}个{1}门闭器。",
                ["DoorCloserDeployed"] = "门闭器已部署。延迟关闭时间设置为：<color=#faa511>{0}秒</color>",
                ["UiButton_Yes"] = "是",
                ["UiButton_No"] = "否",
                ["UiHeaderTitle"] = "自动锁定设置",
                ["UiShareLocksWithClanTeamEnabled"] = "与氏族/团队共享锁定",
                ["UiAutoLockOnPlacementEnabled"] = "自动锁定",
                ["UiCodeLockPin"] = "密码锁代码",
                ["UiGuestCodeEnabled"] = "访客密码锁",
                ["UiGuestCodeLockPin"] = "访客密码锁代码",
                ["UiUpdateAll"] = "全部更新",
                ["UiRemoveAll"] = "全部移除",
                ["UiAlsoUseKeyLockEnabled"] = "同时使用钥匙锁",
                ["UiStreamerModeEnabled"] = "流媒体模式",
                ["UiDoorsLockEnabled"] = "自动锁定：门",
                ["UiWindowsLockEnabled"] = "自动锁定：窗户",
                ["UiBoxesLockEnabled"] = "自动锁定：箱子",
                ["UiStorageContainerLockEnabled"] = "自动锁定：所有储物容器",
                ["UiLockersLockEnabled"] = "自动锁定：储物柜",
                ["UiCupboardsLockEnabled"] = "自动锁定：碗柜（TC）",
                ["UiVehicleLockEnabled"] = "自动锁定：车辆",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "下车时自动锁定关闭",
                ["UiMedievalEntityLockEnabled"] = "自动锁定：中世纪实体",
                ["UiFurnaceLockEnabled"] = "自动锁定：熔炉/精炼炉",
                ["UiVendingMachineLockEnabled"] = "自动锁定：自动贩卖机",
                ["UiComposterLockEnabled"] = "自动锁定：堆肥桶",
                ["UiMixingTableLockEnabled"] = "自动锁定：混合桌",
                ["UiPlanterLockEnabled"] = "自动锁定：种植者",
                ["UiAutoTurretLockEnabled"] = "自动锁定：自动炮塔",
                ["UiSamSiteLockEnabled"] = "自动锁定：SAM站点",
                ["UiTrapsLockEnabled"] = "自动锁定：陷阱",
                ["UiWeaponRackLockEnabled"] = "自动锁定：武器架",
                ["UiStashLockEnabled"] = "自动锁定：藏身处",
                ["UiNeonSignLockEnabled"] = "自动锁定：霓虹灯牌",
                ["UiOtherLockableEntitiesLockEnabled"] = "自动锁定：其他可锁定实体",
                ["UiOtherCustomEntitiesLockEnabled"] = "自动锁定：其他自定义实体",
                ["UiOpenAutoClosingButtonText"] = "自动关闭设置",
                ["UiAutoClosingHeaderTitle"] = "自动关闭设置",
                ["UiAutoClosingEnabledTitle"] = "启用自动关闭",
                ["UiClosingDelay"] = "关闭延迟（秒）",
                ["UiDoorAutoClosingEnabled"] = "自动关闭：门",
                ["UiDoubleDoorAutoClosingEnabled"] = "自动关闭：双门",
                ["UiWindowsAutoClosingEnabled"] = "自动关闭：窗户",
                ["UiGarageAutoClosingEnabled"] = "自动关闭：车库",
                ["UiLadderHatchAutoClosingEnabled"] = "自动关闭：梯子舱口",
                ["UiExternalGateAutoClosingEnabled"] = "自动关闭：外部门",
                ["UiFenceGateAutoClosingEnabled"] = "自动关闭：围栏门",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "自动关闭：传统木制庇护所门"
            }, this, "zh-CN"); //Cinese semplificato

            lang.RegisterMessages(new Dictionary<string, string>
            {
                ["LockerStatus_Locked"] = "已鎖定",
                ["LockerStatus_Unlocked"] = "未鎖定",
                ["LockerStatus_Removed"] = "已移除",
                ["LockerStatus_Updated"] = "已更新",
                ["NoCodeLockAuth"] = "已阻止...",
                ["NoPermissions"] = "<color=red>您沒有使用此指令的權限。</color>",
                ["NoBypassPermissions"] = "<color=red>您沒有繞過密碼的權限。</color>",
                ["NoSteamIdOwner"] = "<color=red>SteamID 並非此實體的擁有者。</color>",
                ["NoEntityOwner"] = "<color=red>您不擁有此實體。</color>",
                ["PlayerNotFound"] = "<color=red>找不到玩家。</color>",
                ["InvalidSteamID"] = "<color=red>無效的 SteamID。</color>",
                ["CodeLockNotFound"] = "<color=red>找不到密碼鎖或鑰匙鎖。</color>",
                ["CodeLockCodeNotFound"] = "<color=red>密碼鎖沒有設定密碼。</color>",
                ["EntityNotAllowed"] = "<color=red>不允許在此實體上執行該操作。</color>",
                ["LockCodeNotValid"] = "<color=red>無效的鎖定密碼（需要 4 位數字）。</color>",
                ["MissingParametersError"] = "<color=red>發生錯誤：缺少參數 - [{0}]。</color>",
                ["GenericError"] = "<color=red>發生錯誤：[{0}]。</color>",
                ["DelayTimeNotValid"] = "<color=red>無效的延遲時間：請輸入 {0} 至 {1} 之間的數值。</color>",
                ["EntityNotFound"] = "<color=red>您必須查看一個實體。</color>",
                ["DoorCloserNotFound"] = "<color=red>此實體沒有門閉器。</color>",
                ["AutoClosingNotEnabled"] = "<color=red>您沒有權限或門閉器未啟用。</color>",
                ["PlayerAutoClosingNotEnabled"] = "<color=red>您尚未啟用門閉器。</color>",
                ["DoorCloserItemNotFound"] = "<color=red>您的庫存中沒有門閉器。</color>",
                ["AutoClosingTypeNotValid"] = "<color=red>無效的門閉器類型。</color>",
                ["DoorCloserAlreadyExists"] = "<color=red>此實體上已經放置了門閉器。</color>",
                ["LockCategoryNotValid"] = "<color=red>無效的鎖定類別。</color>",
                ["AutoLockerNotEnabled"] = "<color=red>您沒有權限或自動鎖定未啟用。</color>",
                ["LockItemNotFound"] = "<color=red>您的庫存中沒有鎖。</color>",
                ["ShowCodeLockCodeMessage"] = "密碼鎖的密碼是：\n密碼：<color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "密碼鎖的密碼是：\n密碼：<color=#faa511>{0}</color>\n訪客密碼：<color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>串流模式已啟用：</color>無法查看密碼",
                ["CodeLockPinChangedMessage"] = "密碼鎖密碼已更改。",
                ["AllCodeLockPinChangedMessage"] = "所有密碼鎖的密碼已更改。",
                ["AllGuestCodeLockPinChangedMessage"] = "所有訪客密碼鎖的密碼已更改。",
                ["AllGuestCodeLockPinRemovedMessage"] = "所有訪客密碼鎖的密碼已移除。",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "與您的公會/隊伍共享的所有密碼鎖權限已更改。",
                ["AutoLockKeyLockDeployedMessage"] = "鑰匙鎖已部署並鎖定。",
                ["AutoLockCodeLockDeployedMessage"] = "密碼鎖已部署並鎖定\n密碼：<color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] = "密碼鎖已部署並鎖定\n密碼：<color=#faa511>{0}</color>\n訪客密碼：<color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] = "密碼鎖 PIN 碼：<color=red>{0}</color>。您可以使用模組化汽車升降機<color=red>更改密碼</color>。",
                ["CodeLockListMessage"] = "您擁有 {0} 個 {1} 密碼鎖/鑰匙鎖。",
                ["CodeLockMessage"] = "您擁有 {0} 個密碼鎖/鑰匙鎖。",
                ["LockClosedAutomaticallyMessage"] = "鎖已自動關閉。",
                ["SingleDoorCloserCustomTime"] = "您已將此門閉器的時間設置為 <color=#faa511>{0} 秒</color>。",
                ["DoorCloserListMessage"] = "您擁有 {0} 個 {1} 門閉器。",
                ["DoorCloserDeployed"] = "門閉器已部署，延遲關閉時間設置為：<color=#faa511>{0} 秒</color>",
                ["UiButton_Yes"] = "是",
                ["UiButton_No"] = "否",
                ["UiHeaderTitle"] = "自動鎖定設置",
                ["UiShareLocksWithClanTeamEnabled"] = "與公會/隊伍共享鎖定",
                ["UiAutoLockOnPlacementEnabled"] = "自動鎖定",
                ["UiCodeLockPin"] = "密碼鎖密碼",
                ["UiGuestCodeEnabled"] = "訪客密碼鎖",
                ["UiGuestCodeLockPin"] = "訪客密碼鎖密碼",
                ["UiUpdateAll"] = "全部更新",
                ["UiRemoveAll"] = "全部移除",
                ["UiAlsoUseKeyLockEnabled"] = "同時使用鑰匙鎖",
                ["UiStreamerModeEnabled"] = "串流模式",
                ["UiDoorsLockEnabled"] = "自動鎖定：門",
                ["UiWindowsLockEnabled"] = "自動鎖定：窗戶",
                ["UiBoxesLockEnabled"] = "自動鎖定：箱子",
                ["UiStorageContainerLockEnabled"] = "自動鎖定：所有儲物容器",
                ["UiLockersLockEnabled"] = "自動鎖定：儲物櫃",
                ["UiCupboardsLockEnabled"] = "自動鎖定：工具櫃（TC）",
                ["UiVehicleLockEnabled"] = "自動鎖定：載具",
                ["UiVehicleLockAutoClosingOnDismountEnabled"] = "下車時自動鎖定",
                ["UiMedievalEntityLockEnabled"] = "自動鎖定：中世紀實體",
                ["UiFurnaceLockEnabled"] = "自動鎖定：熔爐/精煉爐",
                ["UiVendingMachineLockEnabled"] = "自動鎖定：自動販賣機",
                ["UiComposterLockEnabled"] = "自動鎖定：堆肥桶",
                ["UiMixingTableLockEnabled"] = "自動鎖定：混合桌",
                ["UiPlanterLockEnabled"] = "自動鎖定：種植槽",
                ["UiAutoTurretLockEnabled"] = "自動鎖定：自動砲塔",
                ["UiSamSiteLockEnabled"] = "自動鎖定：防空砲塔（SAM 站）",
                ["UiTrapsLockEnabled"] = "自動鎖定：陷阱",
                ["UiWeaponRackLockEnabled"] = "自動鎖定：武器架",
                ["UiStashLockEnabled"] = "自動鎖定：藏匿處",
                ["UiNeonSignLockEnabled"] = "自動鎖定：霓虹燈牌",
                ["UiOtherLockableEntitiesLockEnabled"] = "自動鎖定：其他可鎖定實體",
                ["UiOtherCustomEntitiesLockEnabled"] = "自動鎖定：其他自訂實體",
                ["UiOpenAutoClosingButtonText"] = "自動關閉設置",
                ["UiAutoClosingHeaderTitle"] = "自動關閉設置",
                ["UiAutoClosingEnabledTitle"] = "啟用自動關閉",
                ["UiClosingDelay"] = "關閉延遲（秒）",
                ["UiDoorAutoClosingEnabled"] = "自動關閉：門",
                ["UiDoubleDoorAutoClosingEnabled"] = "自動關閉：雙門",
                ["UiWindowsAutoClosingEnabled"] = "自動關閉：窗戶",
                ["UiGarageAutoClosingEnabled"] = "自動關閉：車庫門",
                ["UiLadderHatchAutoClosingEnabled"] = "自動關閉：梯子艙口",
                ["UiExternalGateAutoClosingEnabled"] = "自動關閉：外圍大門",
                ["UiFenceGateAutoClosingEnabled"] = "自動關閉：圍欄門",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "自動關閉：傳統木製庇護所門"
            }, this, "zh-TW"); //Cinese tradizionale
        }

        #endregion
    }
} 