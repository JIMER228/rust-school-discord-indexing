using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using Oxide.Core;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Facepunch.Extend;
using HarmonyLib;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Game.Rust.Cui;
using Random = UnityEngine.Random;
using Rust;
using UnityEngine;
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

namespace Oxide.Plugins
{
    [Info("UltimateLocker", "Scalbox", "1.1.9")]
    [Description("Place Code Locks everywhere & Auto Lock / Auto Closing. Vehicles, Furnaces, Weapon Racks, Turrets, Deployable Items and much more.")]
    public class UltimateLocker : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin? ImageLibrary;
        [PluginReference] private Plugin? Clans;
        [PluginReference] private Plugin? Vanish;
        private static UltimateLocker? self;

        private enum EntityType
        {
            Vehicle,
            VehicleBike,
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
            Box,
            StorageContainer,
            Locker,
            Cupboard,
            Vehicle,
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
            Garage,
            LadderHatch,
            ExternalGate,
            FenceGate,
            LegacyWoodShelterDoor
        }

        private readonly List<ItemLockData> _allowedItemsLock = new();
        private readonly List<string> _entityRequiredPermission = new();

        //SteamID - Timer
        private readonly Dictionary<ulong, Timer> _playerTimers = new();

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
            "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_a.prefab",
            "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_b.prefab",
            "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",
            "assets/content/vehicles/boats/rhib/subents/rhib_storage.prefab",
            "assets/prefabs/misc/halloween/coffin/coffinstorage.prefab"
        };

        private readonly Dictionary<AutoClosingType, List<string>> _autoClosingEntities = new()
        {
            {
                AutoClosingType.Door, new List<string>
                {
                    "assets/prefabs/building/door.hinged/door.hinged.wood.prefab",
                    "assets/prefabs/building/door.hinged/door.hinged.metal.prefab",
                    "assets/prefabs/building/door.hinged/door.hinged.toptier.prefab",
                    "assets/prefabs/misc/permstore/factorydoor/door.hinged.industrial.d.prefab"
                }
            },
            {
                AutoClosingType.DoubleDoor, new List<string>
                {
                    "assets/prefabs/building/door.double.hinged/door.double.hinged.wood.prefab",
                    "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab",
                    "assets/prefabs/building/door.double.hinged/door.double.hinged.toptier.prefab",
                    "assets/prefabs/building/wall.frame.cell/wall.frame.cell.gate.prefab"
                }
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

        private const string UILocker = "UI.UltimateLocker.Locker";
        private const string UIAutoLock = "UI.UltimateLocker.AutoLock";
        private const string UIAutoClosing = "UI.UltimateLocker.AutoClosing";

        private const string PermissionUse = "ultimatelocker.use";
        private const string PermissionAdmin = "ultimatelocker.admin";
        private const string PermissionBypassForce = "ultimatelocker.bypass.force";
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

        private void OnServerInitialized()
        {
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

            ImageLibrary?.Call("AddImage", CodeLockOnDeployImageUrl, CodeLockOnDeployImageName);
            ImageLibrary?.Call("AddImage", KeyLockOnDeployImageUrl, KeyLockOnDeployImageName);

            //TODO da testare e poi rimuovere
            // NextFrame(() => _coroutines.Add(ServerMgr.Instance.StartCoroutine(HandleStorageContainerLockableOnStartup())));
            NextFrame(() => _coroutines.Add(ServerMgr.Instance.StartCoroutine(HandleUpdateCodeLockAuthOnStartup())));
            NextFrame(() => _coroutines.Add(ServerMgr.Instance.StartCoroutine(HandleUpdateDoorCloserDelayTimeOnStartup())));
        }

        void Init()
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

            AddCovalenceCommand(new[] { _config.AutoLockConfiguration.AddLockerChatCommand, _config.AutoClosingConfiguration.AddCloserChatCommand },
                nameof(LockerCloserCmdHandler));

            _allowedItemsLock.AddRange(_config.LockConfiguration.Vehicles.Where(v => v.EnableLock).ToList());
            _allowedItemsLock.AddRange(_config.LockConfiguration.Deployables.Where(d => d.EnableLock).ToList());

            self = this;
            _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);

            permission.RegisterPermission(PermissionUse, this);
            permission.RegisterPermission(PermissionAdmin, this);
            permission.RegisterPermission(PermissionBypassForce, this);
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

            foreach (var playerTimer in _playerTimers.Values)
            {
                playerTimer.Destroy();
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
            _playerTimers.Clear();
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

        private void DeleteOldLangFiles(string langCode)
        {
            var langDir = Interface.Oxide.LangDirectory;
            var langFile = Path.Combine(langDir, langCode, $"{Name}.json");
            if (!File.Exists(langFile)) return;
            File.Delete(langFile);
            Puts($"########## Delete old lang file: {langFile} ##########");
        }

        private void UpdateConfigValues()
        {
            // string[] languages = lang.GetLanguages(this);
            // foreach (var language in languages)
            // {
            //     DeleteOldLangFiles(language);
            // }

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

                string[] languages = lang.GetLanguages(this);
                foreach (var language in languages)
                {
                    DeleteOldLangFiles(language);
                }
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
                        EnableLock = true,
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
                        EnableLock = true,
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
                        EnableLock = true,
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
                string[] languages = lang.GetLanguages(this);
                foreach (var language in languages)
                {
                    DeleteOldLangFiles(language);
                }
            }

            if (_config.VersionNumber < new VersionNumber(1, 1, 5))
            {
                string[] languages = lang.GetLanguages(this);
                foreach (var language in languages)
                {
                    DeleteOldLangFiles(language);
                }

                Puts("Configuration file is outdated. Updating...");
                _config.AutoLockConfiguration.AutoLockPlayerConfiguration = new AutoLockPlayerConfiguration();
                SaveConfig();
                Puts("Configuration file updated.");
            }

            if (_config.VersionNumber < new VersionNumber(1, 1, 8))
            {
                string[] languages = lang.GetLanguages(this);
                foreach (var language in languages)
                {
                    DeleteOldLangFiles(language);
                }

                SaveConfig();
            }
        }

        private void UpdateDataValues()
        {
            if (_config.VersionNumber < new VersionNumber(1, 1, 3))
            {
                _data = Interface.Oxide.DataFileSystem.ReadObject<Data>(Name);
                Puts("Updating Data file...");

                if (_data.AutoLockPlayerSettingsData.IsNullOrEmpty())
                    return;

                foreach (var autoLockPlayerSettingsData in _data.AutoLockPlayerSettingsData)
                {
                    autoLockPlayerSettingsData.Value.ShareLocksWithClanTeamEnabled = true;
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

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) || !IsAutoClosingEnabled(player)) return;

            var autoClosingPlayerSettings = GetAutoClosingPlayerSettingsData(player);
            if (!autoClosingPlayerSettings.AutoClosingEnabled) return;

            var autoClosingType = AutoClosingTypeByEntity(entity);
            if (autoClosingType == AutoClosingType.None) return;

            AdjustDoorCloserPosition(entity, doorCloser, autoClosingType);
            doorCloser.SendNetworkUpdate_Position();

            _coroutines.Add(ServerMgr.Instance.StartCoroutine(UpdateDoorCloserDelayTime(player, autoClosingType, autoClosingPlayerSettings,
                new List<DoorCloser> { doorCloser })));
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

            var player = BasePlayer.FindByID(entity!.OwnerID);
            if (player == null || player.IsNpc) return;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse)) return;

            //#################### Auto Closing ####################
            NextFrame(() => { DeployDoorCloser(player, entity, false); });

            //#################### Auto lock ####################
            NextFrame(() => { DeployLocker(player, entity, false); });

            //####################################################################################################
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

        private void DeployLocker(BasePlayer player, BaseEntity entity, bool sendMessage)
        {
            if (!permission.UserHasPermission(player.UserIDString, PermissionUse))
            {
                if (sendMessage)
                    SendMessage(player, Lang("NoPermissions", player.UserIDString));
                return;
            }

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
                case LockCategory.Box when autoLockPlayerSettings.BoxesLockEnabled:
                case LockCategory.StorageContainer when autoLockPlayerSettings.StorageContainerLockEnabled:
                case LockCategory.Locker when autoLockPlayerSettings.LockersLockEnabled:
                case LockCategory.Cupboard when autoLockPlayerSettings.CupboardsLockEnabled:
                case LockCategory.Vehicle when autoLockPlayerSettings.VehicleLockEnabled:
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
                    lockerBehaviour = player.gameObject.AddComponent<LockerBehaviour>();

                lockerBehaviour.player = player;
            }
            else if (player.HasComponent<LockerBehaviour>())
                Destroy(player.GetComponent<LockerBehaviour>());
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

            if ( /*permission.UserHasPermission(player.UserIDString, PermissionAutoClosingNoDoorCloserRequired) ||*/
                !_config.AutoClosingConfiguration.CanPickupDoorCloser)
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

            if (baseEntity is BaseRidableAnimal or ShopFront or StorageContainer)
            {
                return baseEntity switch
                {
                    BaseRidableAnimal baseRidableAnimal => CanLootEntity(player, baseRidableAnimal),
                    ShopFront shopFront => CanLootEntity(player, shopFront),
                    StorageContainer storageContainer => CanLootEntity(player, storageContainer),
                    _ => null
                };
            }

            if (!CanUseEntity(player, baseEntity)) return false;
            return null;
        }

        //TODO testare se puo essere rimosso
        private object? CanLootEntity(BasePlayer player, BaseRidableAnimal animal)
        {
            if (player == null || animal == null) return null;
            if (!CanUseEntity(player, animal)) return false;
            return null;
        }

        private object? CanLootEntity(BasePlayer player, StorageContainer container)
        {
            if (player == null || container == null) return null;

            if (container != null && container is VendingMachine or DropBox)
            {
                if (IsVendingMachineOpen(player, container) || IsDropBoxOpen(player, container)) return null;
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

        private object? OnVehiclePush(BaseVehicle vehicle, BasePlayer player)
        {
            if (_config.AllowPushVehiclesBlockedByCodeLock)
                return null;

            if (player == null || vehicle == null) return null;
            if (!CanUseEntity(player, vehicle)) return false;
            return null;
        }

        //Conduct Horse
        private object? OnHorseLead(BaseRidableAnimal animal, BasePlayer player)
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

        #endregion

        #region Core

        private void SendMessage(BasePlayer player, string message)
        {
            var prefix = !string.IsNullOrWhiteSpace(_config.ChatPrefix) ? _config.ChatPrefix : string.Empty;
            if (!string.IsNullOrWhiteSpace(prefix))
            {
                prefix = $"<color=#62de32>{prefix}\n</color>";
            }

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
                Layers.Mask.Vehicle_Detailed | Layers.Mask.Vehicle_Large | Layers.Mask.AI | Layers.Mask.Deployed |
                Layers.Deploy);
            return raycastHit.GetEntity() ?? null;
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

            if (entity is Door) return LockCategory.Door;

            if (_boxesPrefabName.Contains(entity.PrefabName.ToLower()))
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

        private AutoClosingType AutoClosingTypeByEntity(BaseEntity entity)
        {
            if (entity == null) return AutoClosingType.None;

            var (autoClosingType, value) = _autoClosingEntities.FirstOrDefault(ent => ent.Value.Contains(entity.PrefabName));
            if (value == null || value.IsNullOrEmpty()) return AutoClosingType.None;

            var autoClosingConfiguration = _config.AutoClosingConfiguration;
            return autoClosingType switch
            {
                AutoClosingType.Door when autoClosingConfiguration.EnableDoorAutoClosing => AutoClosingType.Door,
                AutoClosingType.DoubleDoor when autoClosingConfiguration.EnableDoubleDoorAutoClosing => AutoClosingType.DoubleDoor,
                AutoClosingType.Garage when autoClosingConfiguration.EnableGarageAutoClosing => AutoClosingType.Garage,
                AutoClosingType.LadderHatch when autoClosingConfiguration.EnableLadderHatchAutoClosing => AutoClosingType.LadderHatch,
                AutoClosingType.ExternalGate when autoClosingConfiguration.EnableExternalGateAutoClosing => AutoClosingType.ExternalGate,
                AutoClosingType.FenceGate when autoClosingConfiguration.EnableFenceGateAutoClosing => AutoClosingType.FenceGate,
                AutoClosingType.LegacyWoodShelterDoor when autoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosing &&
                                                           entity is LegacyShelter or LegacyShelterDoor =>
                    AutoClosingType.LegacyWoodShelterDoor,
                _ => AutoClosingType.None
            };
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

                var oldCode = currentLock is CodeLock cl ? cl.code : string.Empty;
                AutoLockApplySettings(player, currentLock, settings);
                var newCode = currentLock is CodeLock cl2 ? cl2.code : string.Empty;

                if (oldCode.Equals(newCode))
                    return;
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
                locker = GameManager.server.CreateEntity(CodeLockPrefab,
                        customLockableItemData?.CodeLockPosition ?? default,
                        customLockableItemData?.CodeLockRotation ?? default)
                    as CodeLock;
            }

            if (locker == null)
            {
                lockerItem = RetrievePlayerCodeLock(player);
                if (lockerItem != null)
                {
                    locker = GameManager.server.CreateEntity(CodeLockPrefab,
                            customLockableItemData?.CodeLockPosition ?? default,
                            customLockableItemData?.CodeLockRotation ?? default)
                        as CodeLock;
                }

                if (locker == null && settings.AlsoUseKeyLockEnabled)
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


            if (isAutoLockerEnabled)
            {
                AutoLockSendMessage(player, locker, settings);

                if (locker is CodeLock cl && cl.IsLocked())
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
                autoClosingConfiguration.EnableGarageAutoClosing || autoClosingConfiguration.EnableLadderHatchAutoClosing ||
                autoClosingConfiguration.EnableExternalGateAutoClosing || autoClosingConfiguration.EnableFenceGateAutoClosing ||
                autoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosing
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
            if (!codeLock.whitelistPlayers.Contains(player.userID.Get()))
            {
                codeLock.whitelistPlayers.Add(player.userID);
            }

            if (settings.ShareLocksWithClanTeamEnabled)
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
            if (player == null || baseEntity == null || string.IsNullOrEmpty(baseEntity.PrefabName)) return false;

            var entityToCheck = GetMainEntity(baseEntity);
            if (entityToCheck == null) return false;

            if (!permission.UserHasPermission(player.UserIDString, PermissionUse) || !IsAllowedEntity(entityToCheck)) return false;


            //START - Required Permission check
            var itemLockData = _allowedItemsLock.FirstOrDefault(i =>
                !string.IsNullOrEmpty(i.PrefabName) &&
                i.PrefabName.ToLower().Equals(entityToCheck.PrefabName.ToLower()));

            if (itemLockData != null && !HasValidPermission(player, itemLockData.RequiredPermission)) return false;
            //END - Required Permission check


            if (_config.RequiresBuildingPrivilege && player.IsBuildingBlocked())
                return false;


            if (entityToCheck is MiningQuarry miningQuarry)
            {
                if (miningQuarry.isStatic && !miningQuarry.OwnerID.IsSteamId())
                    return false;
            }

            if (entityToCheck is ModularCar modularCar)
            {
                if (modularCar == null)
                    return false;

                if (modularCar.CarLock.HasALock)
                    return false;

                if (ignoreExistingLock && !modularCar.CarLock.HasALock)
                    return false;

                if (player.GetActiveItem() == null || player.GetActiveItem().info.itemid.Equals(KeyLockItemID))
                    return false;
            }

            if (entityToCheck is BaseVehicle baseVehicle)
            {
                if (baseVehicle == null)
                    return false;

                if (!ignoreExistingLock && baseVehicle.GetSlot(BaseEntity.Slot.Lock) != null)
                    return false;

                if (_config.RequiresBuildingPrivilegeToDeployCodeLockInUnownedVehicles && !baseVehicle.OwnerID.IsSteamId() && player.CanBuild())
                    return true;

                if (_config.AllowDeployCodeLockInOwnedPlayersVehicles && baseVehicle.OwnerID.IsSteamId())
                    return true;

                if (_config.AllowDeployCodeLockInUnownedVehicles && !baseVehicle.OwnerID.IsSteamId())
                    return true;
            }

            if ((!ignoreExistingLock && entityToCheck.GetSlot(BaseEntity.Slot.Lock) != null) || !IsEntityOwner(entityToCheck, player))
                return false;

            return true;
        }

        private bool DeployLock(BasePlayer player, BaseEntity baseEntity, bool byPassActiveItem = false)
        {
            try
            {
                if (player == null || baseEntity == null || baseEntity.GetSlot(BaseEntity.Slot.Lock) != null)
                    return false;

                var entityToCheck = GetMainEntity(baseEntity);
                if (entityToCheck == null) return false;

                if (!permission.UserHasPermission(player.UserIDString, PermissionUse) ||
                    !IsAllowedEntity(entityToCheck)) return false;

                var codeLockPosition = _allowedItemsLock.FirstOrDefault(i =>
                    !string.IsNullOrEmpty(i.PrefabName) &&
                    i.PrefabName.ToLower().Equals(entityToCheck.PrefabName.ToLower()));

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
                    else if (player.GetActiveItem().info.itemid.Equals(CodeLockItemID))
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

                if (locker == null)
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
                        if (autoLockPlayerSettings.ShareLocksWithClanTeamEnabled)
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
                        if (autoLockPlayerSettings.ShareLocksWithClanTeamEnabled)
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
            _data.CodeLockItemsData[ownerPlayer.userID].Add(new CodeLockItemsData
            {
                OwnerId = ownerPlayer.userID,
                PlayerName = ownerPlayer.displayName,
                NetId = codeLockEntity.net.ID.Value,
                ShortPrefabName = codeLockEntity.ShortPrefabName,
                ParentEntity = codeLockEntity.GetParentEntity() != null
                    ? codeLockEntity.GetParentEntity().ShortPrefabName
                    : "",
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

                    player.SendConsoleCommand("ultimatelocker.autolock.cmd updateAllCodeLockAuth");

                    SaveData();
                    OpenAutoLockSettingsUI(player);
                    break;

                case "toggleAutoLockOnPlacement":
                    if (arg.Args is not { Length: > 1 } || string.IsNullOrWhiteSpace(arg.Args[1])) return;
                    toggleStatus = arg.Args[1].ToBool();
                    if (autoLockPlayerSettings.AutoLockOnPlacementEnabled == toggleStatus) return;

                    autoLockPlayerSettings.AutoLockOnPlacementEnabled = arg.Args[1].ToBool();

                    if (!autoLockPlayerSettings.AutoLockOnPlacementEnabled)
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

            //#################### ShareLocksWithClanTeam ####################
            var rowOffsetY = 0.9245;
            var rowShareLocksWithClan = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.ShareLocksWithClan",
                $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
            UIHelper.Text(mainContainer, rowShareLocksWithClan, Lang("UiShareLocksWithClanTeamEnabled", player.UserIDString), "0.02 0.2", "0.78 0.7");
            UIHelper.ToggleButton(mainContainer, rowShareLocksWithClan, autoLockPlayerSettings.ShareLocksWithClanTeamEnabled,
                "ultimatelocker.autolock.cmd toggleShareLocksWithClan", "0.8 0.1", "0.99 0.85",
                Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
            //####################################################################################################

            //#################### AutoLockOnPlacement ####################
            rowOffsetY -= rowHeight + rowHeightMargin;
            var rowAutoLockOnPlacement = UIHelper.Row(mainContainer, contentPanel, UIAutoLock + ".Row.AutoLockOnPlacement",
                $"0.0274 {rowOffsetY}", $"0.949 {rowOffsetY + rowHeight}");
            UIHelper.Text(mainContainer, rowAutoLockOnPlacement, Lang("UiAutoLockOnPlacementEnabled", player.UserIDString), "0.02 0.2", "0.78 0.7");
            UIHelper.ToggleButton(mainContainer, rowAutoLockOnPlacement, autoLockPlayerSettings.AutoLockOnPlacementEnabled,
                "ultimatelocker.autolock.cmd toggleAutoLockOnPlacement", "0.8 0.1", "0.99 0.85",
                Lang("UiButton_Yes", player.UserIDString), Lang("UiButton_No", player.UserIDString));
            //####################################################################################################

            if (autoLockPlayerSettings.AutoLockOnPlacementEnabled)
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

            if (allowedLockCategory.Contains(LockCategory.Box))
            {
                //#################### BoxesLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.BoxesLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiBoxesLockEnabled", player.UserIDString), $"{basicCmd} toggleBoxesLock",
                    autoLockPlayerSettings.BoxesLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

            if (allowedLockCategory.Contains(LockCategory.StorageContainer))
            {
                //#################### StorageContainerLock ####################
                rowCuiPanels.Add(ScrollViewAddRow(mainContainer, $"{UIAutoLock}.Row.StorageContainerLock", offsetY, componentLeftMargin, componentHeight,
                    componentRightMargin, Lang("UiStorageContainerLockEnabled", player.UserIDString), $"{basicCmd} toggleStorageContainerLock",
                    autoLockPlayerSettings.StorageContainerLockEnabled, player));
                offsetY = offsetY - componentHeight - componentHeightMargin;
                //############################################################
            }

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
            BasePlayer? player = null)
        {
            var rowLockersLock = UIHelper.Row($"{panelName}", "0 1", "1 1",
                $"{componentLeftMargin} {offsetY - componentHeight}", $"{componentRightMargin} {offsetY}");
            rowLockersLock.Render = () =>
            {
                UIHelper.Text(container, rowLockersLock.PanelName, text, "0.02 0.2", "0.78 0.7");

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
            return steamID.IsSteamId() && GetAutoLockPlayerSettingsData(steamID).ShareLocksWithClanTeamEnabled;
        }

        private AutoLockPlayerSettingsData GetAutoLockPlayerSettingsData(ulong steamID)
        {
            if (_data.AutoLockPlayerSettingsData.TryGetValue(steamID, out var autoLockPlayerSettingsData))
                return autoLockPlayerSettingsData;

            //Create default Auto Lock Player Settings Data
            var defaultAutoLockConfiguration = _config.AutoLockConfiguration;
            autoLockPlayerSettingsData = new AutoLockPlayerSettingsData
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
                BoxesLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableBoxesLockByDefault,
                StorageContainerLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableStorageContainerLockByDefault,
                LockersLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableLockersLockByDefault,
                CupboardsLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableCupboardsLockByDefault,
                VehicleLockEnabled = defaultAutoLockConfiguration.AutoLockPlayerConfiguration.EnableVehicleLockByDefault,
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

            _data.AutoLockPlayerSettingsData.Add(steamID, autoLockPlayerSettingsData);
            SaveData();
            return autoLockPlayerSettingsData;
        }

        private AutoClosingPlayerSettingsData GetAutoClosingPlayerSettingsData(BasePlayer player) => GetAutoClosingPlayerSettingsData(player.userID);

        private AutoClosingPlayerSettingsData GetAutoClosingPlayerSettingsData(ulong userID)
        {
            if (_data.AutoClosingPlayerSettingsData.TryGetValue(userID, out var autoClosingPlayerSettingsData))
                return autoClosingPlayerSettingsData;

            //Create default Auto Closing Player Settings Data
            var defaultAutoClosingConfiguration = _config.AutoClosingConfiguration;
            autoClosingPlayerSettingsData = new AutoClosingPlayerSettingsData
            {
                SteamID = userID,

                AutoClosingEnabled = false,
                DoorAutoClosingEnabled = defaultAutoClosingConfiguration.EnableDoorAutoClosing,
                DoubleDoorAutoClosingEnabled = defaultAutoClosingConfiguration.EnableDoubleDoorAutoClosing,
                GarageAutoClosingEnabled = defaultAutoClosingConfiguration.EnableGarageAutoClosing,
                LadderHatchAutoClosingEnabled = defaultAutoClosingConfiguration.EnableLadderHatchAutoClosing,
                ExternalGateAutoClosingEnabled = defaultAutoClosingConfiguration.EnableExternalGateAutoClosing,
                FenceGateAutoClosingEnabled = defaultAutoClosingConfiguration.EnableFenceGateAutoClosing,
                LegacyWoodShelterDoorAutoClosingEnabled = defaultAutoClosingConfiguration.EnableLegacyWoodShelterDoorAutoClosing,

                DoorClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                DoubleDoorClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                GarageClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                LadderHatchClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                ExternalGateClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                FenceGateClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime,
                LegacyWoodShelterDoorClosingDelay = defaultAutoClosingConfiguration.DefaultClosingDelayTime
            };

            _data.AutoClosingPlayerSettingsData.Add(userID, autoClosingPlayerSettingsData);
            SaveData();
            return autoClosingPlayerSettingsData;
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_SetFogOff(BaseEntity.RPCMessage msg, FogMachine __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnFogMachineToggle", (object)__instance, (object)msg.player) != null)
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_SetStrobeSpeed(BaseEntity.RPCMessage msg, StrobeLight __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnStrobeSpeedChange", (object)__instance, (object)msg.player) != null)
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
            //             return false; //Block Orignal code
            //         
            //         // if (Interface.CallHook("OnSignageUpdateSign", (object)__instance, (object)msg.player) != null)
            //         //     return false; //Block Orignal code
            //     }
            //     catch (Exception e)
            //     {
            //         self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_UnLockSign(BaseEntity.RPCMessage msg, Signage __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnSignageLockUnlockSign", (object)__instance, (object)msg.player) != null)
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }

            public static bool Prefix_UpdateNeonColors(BaseEntity.RPCMessage msg, NeonSign __instance)
            {
                try
                {
                    if (msg.player == null || __instance == null) return true; //Run Normal code

                    if (Interface.CallHook("OnUpdateNeonColors", (object)__instance, (object)msg.player) != null)
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
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
        //                 return false; //Block Orignal code
        //         }
        //         catch (Exception e)
        //         {
        //             self.Puts($"Exception: {e.Message}");
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
                        return false; //Block Orignal code
                }
                catch (Exception e)
                {
                    self.Puts($"Exception: {e.Message}");
                }

                return true; //Run Normal code
            }
        }

        public class LockerBehaviour : MonoBehaviour
        {
            public BasePlayer? player;

            public void OnDestroy()
            {
                if (player != null) DestroyLockerUI(player);
                if (self != null && self._playerTimers.ContainsKey(player!.userID))
                {
                    self._playerTimers[player.userID].Destroy();
                    self._playerTimers.Remove(player.userID);
                }
            }

            public void FixedUpdate()
            {
                try
                {
                    if (self == null || player == null || player.GetActiveItem() == null &&
                        !player.GetActiveItem().info.itemid.Equals(KeyLockItemID) &&
                        !player.GetActiveItem().info.itemid.Equals(CodeLockItemID))
                        Destroy(this);

                    if (!self!.permission.UserHasPermission(player!.UserIDString, PermissionUse)) Destroy(this);

                    if (!self!._playerTimers.ContainsKey(player!.userID))
                        self._playerTimers.Add(player.userID,
                            self.timer.Every(0.5f, () =>
                            {
                                DestroyLockerUI(player);

                                var entityLookedAt = GetEntityByRayCast(player);
                                if (entityLookedAt == null) return;

                                if (!self.CanDeployLock(player, entityLookedAt)) return;
                                ShowLockerUI(player);
                            }));

                    if (player.serverInput.WasJustReleased(BUTTON.FIRE_PRIMARY))
                    {
                        var entityLookedAt = GetEntityByRayCast(player);
                        if (entityLookedAt == null) return;

                        if (!self.CanDeployLock(player, entityLookedAt)) return;
                        if (!self.DeployLock(player, entityLookedAt)) return;
                        self.RunEffect(CodeLockDeployEffect, player);

                        var isEmpty = false;

                        var playerInventoryAllItems = new List<Item>();
                        player.inventory.GetAllItems(playerInventoryAllItems);
                        foreach (var item in playerInventoryAllItems.Where(item => item.info.itemid.Equals(player.GetActiveItem().info.itemid)))
                        {
                            item.amount--;
                            item.MarkDirty();
                            if (item.amount <= 0)
                            {
                                item.Remove();
                                isEmpty = true;
                            }

                            break;
                        }

                        if (isEmpty)
                            Destroy(this);
                    }
                }
                catch
                {
                    Destroy(this);
                }
            }
        }

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
                        ImageType = UnityEngine.UI.Image.Type.Simple
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
                        ImageType = UnityEngine.UI.Image.Type.Simple
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
                        ImageType = UnityEngine.UI.Image.Type.Simple
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
                            MovementType = UnityEngine.UI.ScrollRect.MovementType.Elastic,
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
                        AutoLockPlayerConfiguration = new AutoLockPlayerConfiguration()
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
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/minicopter/minicopter.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.15f, 0.7f, -0.1f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Scrap Transport Helicopter",
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/attackhelicopter/attackhelicopter.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.6f, 1.08f, 1.01f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Armored / Hot Air Balloon",
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/hot air balloon/hotairballoon.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(1.45f, 0.9f, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Kayak",
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/boats/rowboat/rowboat.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.83f, 0.51f, -0.57f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "RHIB",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/boats/rhib/rhib.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.68f, 2.00f, 0.7f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Tugboat",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/rust.ai/nextai/testridablehorse.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.22f, 1.05f, 0.1f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Tomaha Snowmobile",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/snowmobiles/tomahasnowmobile.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.37f, 0.4f, 0.125f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Snowmobile",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/snowmobiles/snowmobile.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.205f, 0.59f, 0.4f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Sedan",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/sedan_a/sedantest.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-1.09f, 0.79f, 0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "2 Module Car",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/modularcar/2module_car_spawned.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "3 Module Car",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/modularcar/3module_car_spawned.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "4 Module Car",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/modularcar/4module_car_spawned.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Car Chassis 2 Module Car",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/modularcar/car_chassis_2module.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Car Chassis 3 Module Car",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/modularcar/car_chassis_3module.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Car Chassis 4 Module Car",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/modularcar/car_chassis_4module.entity.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.Vehicle,
                                CodeLockPosition = new Vector3(-0.9f, 0.35f, -0.5f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Motorbike",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/bikes/motorbike.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.074f, 0.8f, 0.14f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Motorbike With Sidecar",
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/bikes/pedalbike.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.0f, 0.6f, -0.07f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Pedal Trike",
                                EnableLock = true,
                                PrefabName = "assets/content/vehicles/bikes/pedaltrike.prefab",

                                LockCategory = LockCategory.Vehicle,
                                EntityType = EntityType.VehicleBike,
                                CodeLockPosition = new Vector3(0.0f, 0.7f, -0.5f),
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/fireplace/fireplace.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                CodeLockPosition = new Vector3(-0.02f, 0.2f, 0.46f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "BBQ",
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/bbq/bbq.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.3f, 0.75f, 0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Hobo Barrel",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/misc/decor_dlc/storagebarrel/storage_barrel_c.prefab",

                                LockCategory = LockCategory.Box,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.60f, 0.45f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 350),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "RHIB Storage",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab",

                                LockCategory = LockCategory.StorageContainer,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.08f, 1.17f, 0.20f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Vending Machine",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/mixingtable/mixingtable.deployed.prefab",

                                LockCategory = LockCategory.MixingTable,
                                CodeLockPosition = new Vector3(-0.50f, 0.62f, 0.432f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Composter",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/planters/planter.large.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(1.22f, 0.45f, 1.21f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Minecart Planter",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName =
                                    "assets/prefabs/misc/decor_dlc/rail road planter/railroadplanter.deployed.prefab",

                                LockCategory = LockCategory.Planter,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(1.22f, 0.45f, 1.26f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 90),

                                RequiredPermission = new[] { "" }
                            },
                            //TODO Sistemare che il cavallo non possa mangiare
                            new()
                            {
                                ItemName = "Hitch & Trough",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableWorkbench,
                                CodeLockPosition = new Vector3(-0.98f, 0.74f, 0.38f),
                                CodeLockRotation = Quaternion.Euler(0, 90, 0),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Button",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/npc/sam_site_turret/sam_site_turret_deployed.prefab",

                                LockCategory = LockCategory.SamSite,
                                EntityType = EntityType.DeployableTurret,
                                CodeLockPosition = new Vector3(-0.4f, 0.8f, -0.30f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Auto Turret",
                                EnableLock = true,
                                PrefabName = "assets/prefabs/npc/autoturret/autoturret_deployed.prefab",

                                LockCategory = LockCategory.AutoTurret,
                                EntityType = EntityType.DeployableTurret,
                                CodeLockPosition = new Vector3(-0.29f, 0.38f, 0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Flame Turret",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/elevator/elevator_lift.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableElevator,
                                CodeLockPosition = new Vector3(-1.2f, -0.1f, -0.0f),

                                RequiredPermission = new[] { "" }
                            },
                            new()
                            {
                                ItemName = "Mining Quarry",
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
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
                                EnableLock = true,
                                PrefabName = "assets/prefabs/deployable/jack o lantern/jackolantern.happy.prefab",

                                LockCategory = LockCategory.OtherCustomEntities,
                                EntityType = EntityType.DeployableStorage,
                                CodeLockPosition = new Vector3(0.0f, 0.2f, 0.0f),
                                CodeLockRotation = Quaternion.Euler(0.0f, 270.0f, 90.0f),

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

            [JsonProperty("Player default settings")]
            public AutoLockPlayerConfiguration AutoLockPlayerConfiguration;
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

        private class AutoClosingConfiguration
        {
            [JsonProperty("Add Door Closer manually - Chat Command")]
            public string AddCloserChatCommand = "closer";

            [JsonProperty("Player Can Pickup Door Closer. (Default: TRUE)")]
            public bool CanPickupDoorCloser = true;

            [JsonProperty("Enable automatic closing of Door. (Default: FALSE)")]
            public bool EnableDoorAutoClosing = false;

            [JsonProperty("Enable automatic closing of Double Door. (Default: FALSE)")]
            public bool EnableDoubleDoorAutoClosing = false;

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

                ["ShowCodeLockCodeMessage"] = "Code Lock code is:\nCode: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] = "Code Lock code is:\nCode: <color=#faa511>{0}</color>\nGuest Code: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Streamer mode active:</color> code cannot be viewed",

                ["CodeLockPinChangedMessage"] = "Code Lock Pin changed.",
                ["AllCodeLockPinChangedMessage"] = "All Code Lock Pins have been changed.",
                ["AllGuestCodeLockPinChangedMessage"] = "All Guest Code Lock Pins have been changed.",
                ["AllGuestCodeLockPinRemovedMessage"] = "All Guest Code Lock Pins have been removed.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "All Code Lock Shared Auth have been changed.",

                ["AutoLockKeyLockDeployedMessage"] = "Key Lock deployed and locked.",
                ["AutoLockCodeLockDeployedMessage"] = "Code Lock deployed and locked\nCode: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Code Lock deployed and locked\nCode: <color=#faa511>{0}</color>\nGuest Code: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] =
                    "Pin Code Lock: <color=red>{0}</color>. You can <color=red>change the pin</color> with the Modular Car Lift.",

                ["CodeLockListMessage"] = "You have {0} {1} Code Locks / key Locks.",
                ["CodeLockMessage"] = "You have {0} a Code Lock / Key Locks.",

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
                ["UiBoxesLockEnabled"] = "Auto Lock: Boxes",
                ["UiStorageContainerLockEnabled"] = "Auto Lock: All Storage Container",
                ["UiLockersLockEnabled"] = "Auto Lock: Lockers",
                ["UiCupboardsLockEnabled"] = "Auto Lock: Cupboards (TC)",
                ["UiVehicleLockEnabled"] = "Auto Lock: Vehicle",
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

                ["ShowCodeLockCodeMessage"] = "Il codice di blocco della Serratura a codice è:\nCodice: <color=#faa511>{0}</color>",
                ["ShowCodeLockAndGuestCodeMessage"] =
                    "Il codice di blocco della Serratura a codice è:\nCodice: <color=#faa511>{0}</color>\nCodice ospite: <color=#faa511>{1}</color>",
                ["ShowCodeLockPinStreamerModeMessage"] = "<color=#faa511>Modalità streamer attiva:</color> il codice non può essere visualizzato",

                ["CodeLockPinChangedMessage"] = "Il codice PIN della Serratura a codice è stato modificato.",
                ["AllCodeLockPinChangedMessage"] = "Tutti i PIN delle Serrature a Codice sono stati modificati.",
                ["AllGuestCodeLockPinChangedMessage"] = "Tutti i codici PIN per gli ospiti sono stati modificati.",
                ["AllGuestCodeLockPinRemovedMessage"] = "Tutti i codici PIN per gli ospiti sono stati rimossi.",
                ["AllCodeLockSharedAuthPinChangedMessage"] = "Sono state modificate tutte le autorizzazioni di condivise delle Serrature a codice.",

                ["AutoLockKeyLockDeployedMessage"] = "Serratura applicata e bloccata.",
                ["AutoLockCodeLockDeployedMessage"] = "Serratura a codice applicata e bloccata\nCodice: <color=#faa511>{0}</color>",
                ["AutoLockCodeLockAndGuestCodeDeployedMessage"] =
                    "Serratura a codice applicata e bloccata\nCodice: <color=#faa511>{0}</color>\nCodice ospite: <color=#faa511>{1}</color>",
                ["ModularCarNewCodeLockMessage"] =
                    "Codice PIN: <color=red>{0}</color>. È possibile <color=red>cambiare il PIN</color> con l'ascensore per auto.",

                ["CodeLockListMessage"] = "Hai {0} {1} Serrature a codice / Serrature.",
                ["CodeLockMessage"] = "Hai {0} la Serratura a codice / Serratura.",

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
                ["UiBoxesLockEnabled"] = "Blocco automatico: Contenitori / Barili",
                ["UiStorageContainerLockEnabled"] = "Blocco automatico: Tutti i tipi di contenitore",
                ["UiLockersLockEnabled"] = "Blocco automatico: Armadietti",
                ["UiCupboardsLockEnabled"] = "Blocco automatico: Armadio degli attrezzi (TC)",
                ["UiVehicleLockEnabled"] = "Blocco automatico: Veicoli",
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
                ["UiGarageAutoClosingEnabled"] = "Chiusura automatica: Garage",
                ["UiLadderHatchAutoClosingEnabled"] = "Chiusura automatica: Scala a scomparsa",
                ["UiExternalGateAutoClosingEnabled"] = "Chiusura automatica: Portone mura",
                ["UiFenceGateAutoClosingEnabled"] = "Chiusura automatica: Cancello del recinto",
                ["UiLegacyWoodShelterDoorAutoClosingEnabled"] = "Chiusura automatica: porta del rifugio"
            }, this, "it");

            // lang.RegisterMessages(new Dictionary<string, string>
            // {
            //     
            // }, this, "es");
            //
            // lang.RegisterMessages(new Dictionary<string, string>
            // {
            //     
            // }, this, "es-ES");
            //
            // lang.RegisterMessages(new Dictionary<string, string>
            // {
            //     
            // }, this, "de");
            //
            // lang.RegisterMessages(new Dictionary<string, string>
            // {
            //    
            // }, this, "ru");
        }

        #endregion
    }
}