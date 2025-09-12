using Oxide.Core;
using Oxide.Game.Rust.Cui;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Oxide.Plugins
{
    [Info("FuelManager", "Nikedemos", VERSION)]
    [Description("Propel your vehicles with anything you want")]
    public class FuelManager : RustPlugin
    {
        /*
        [PluginReference]
        Plugin ImageLibrary;
        */
        public static FuelManager Instance;
        public static bool Unloading = false;

        #region META
        public const string VERSION = "1.0.13";
        #endregion

        #region CONST
        public const ulong SteamIconID = 0;
        public static readonly string ChatPrefix = $"<color={ColorPalette.RustyYellow.hexValue}>[Fuel Manager]</color> ";

        public const string PREFAB_COCKPIT_NORMAL = "assets/content/vehicles/modularcar/module_entities/1module_cockpit.prefab";
        public const string PREFAB_COCKPIT_ARMORED = "assets/content/vehicles/modularcar/module_entities/1module_cockpit_armored.prefab";
        public const string PREFAB_COCKPIT_ENGINE = "assets/content/vehicles/modularcar/module_entities/1module_cockpit_with_engine.prefab";

        public const string PREFAB_COUNTER = "assets/prefabs/deployable/playerioents/counter/counter.prefab";

        public const int ITEM_BATTERY_SMALL = -692338819;
        public const int ITEM_BATTERY_MEDIUM = 2023888403;
        public const int ITEM_BATTERY_LARGE = 553270375;

        public const int ITEM_SULFUR = -1581843485;
        public const int ITEM_GUNPOWDER = -265876753;

        public const int ITEM_FERTILIZER = -930193596;

        public const int ITEM_DUNG = -1579932985;
        public const int ITEM_LOWGRADE = -946369541;
        public const int ITEM_CRUDE = -321733511;
        public const int ITEM_DIESEL = 1568388703;

        public const int ITEM_WATER_BOTTLE = -1039528932;
        public const int ITEM_BOTA_BAG = 613961768;
        public const int ITEM_WATER_JUG = -119235651;

        public const int ITEM_TORCH = 795236088;

        public const int ITEM_JACKHAMMER = 1488979457;
        public const int ITEM_DIVING_TANK = -2022172587;

        public const int ITEM_MINERS_HAT = -1539025626;
        public const int ITEM_LANTERN = 1658229558;
        public const int ITEM_TUNA_LAMP = -1478445584;
        public const int ITEM_CANDLE_HAT = 1714496074;

        public static readonly ReadOnlyDictionary<VehicleType, Vector3> COUNTER_LOCAL_POSITION = new ReadOnlyDictionary<VehicleType, Vector3>(new Dictionary<VehicleType, Vector3>
        {
            [VehicleType.Car] = new Vector3(0.2F, 0.6F, 0.168F),
            [VehicleType.Minicopter] = new Vector3(0, 0.8F, 0.95F),
            [VehicleType.Scrapheli] = new Vector3(-0.1F, 1.47F, 3.3F),
            [VehicleType.Rowboat] = new Vector3(0F, 0.7F, 1.85F),
            [VehicleType.Rhib] = new Vector3(0F, 2.06F, 0.57F),
            [VehicleType.HotAirBalloon] = new Vector3(-0.32F, 2.8F, 0.1F),
            [VehicleType.Train] = new Vector3(0.537F, 2.391F, -0.064F),
            [VehicleType.Crane] = new Vector3(-0.961F, 4.056F, 1.397F),
            [VehicleType.SubSolo] = new Vector3(0F, 1.148F, 0.343F),
            [VehicleType.SubDuo] = new Vector3(-0.435F, 1.34F, 0.84F),
            [VehicleType.SnowmobileRed] = new Vector3(0.091F, 0.663F, 0.289F),
            [VehicleType.SnowmobileTomaha] = new Vector3(0.052F, 0.476F, -0.370F),
            [VehicleType.Tugboat] = new Vector3(-0.713F, 6.552F, 3.741F),
            [VehicleType.AttackHelicopter] = new Vector3(0F, 1.47F, 0.406F),
            [VehicleType.Motorbike] = new Vector3(0F, 1.17F, 0.47F),
            [VehicleType.Motortrike] = new Vector3(0F, 1.17F, 0.47F),
        });

        public static readonly ReadOnlyDictionary<VehicleType, Vector3> COUNTER_LOCAL_ROTATION = new ReadOnlyDictionary<VehicleType, Vector3>(new Dictionary<VehicleType, Vector3>
        {
            [VehicleType.Car] = new Vector3(340F, 190F, 0F),
            [VehicleType.Minicopter] = new Vector3(0, 180F, 0),
            [VehicleType.Scrapheli] = new Vector3(333.333F, 180F, 0F),
            [VehicleType.Rowboat] = new Vector3(330F, 180F, 0F),
            [VehicleType.Rhib] = new Vector3(331.333F, 180F, 0F),
            [VehicleType.HotAirBalloon] = new Vector3(90F, 180F, 0F),
            [VehicleType.Train] = new Vector3(0F, 180F, 0F),
            [VehicleType.Crane] = new Vector3(23.646F, 180F, 0F),
            [VehicleType.SubSolo] = new Vector3(350F, 180F, 0F),
            [VehicleType.SubDuo] = new Vector3(15F, 110F, 0F),
            [VehicleType.SnowmobileRed] = new Vector3(343.844F, 180F, 0F),
            [VehicleType.SnowmobileTomaha] = new Vector3(300F, 180F, 0F),
            [VehicleType.Tugboat] = new Vector3(320F, 180F, 0f),
            [VehicleType.AttackHelicopter] = new Vector3(340F, 180F, 0F),
            [VehicleType.Motorbike] = new Vector3(310F, 180F, 0F),
            [VehicleType.Motortrike] = new Vector3(310F, 180F, 0F),

        });

        #endregion

        #region LANG
        public const string MSG_GENERAL = "MSG_GENERAL";

        public const string MSG_VEHICLE = "MSG_VEHICLE";
        public const string MSG_VEHICLES = "MSG_VEHICLES";

        public const string MSG_VEHICLE_NAME_CAR = "MSG_VEHICLE_NAME_CAR";
        public const string MSG_VEHICLE_NAME_CARS = "MSG_VEHICLE_NAME_CARS";

        public const string MSG_VEHICLE_NAME_MINICOPTER = "MSG_VEHICLE_NAME_MINICOPTER";
        public const string MSG_VEHICLE_NAME_MINICOPTERS = "MSG_VEHICLE_NAME_MINICOPTERS";

        public const string MSG_VEHICLE_NAME_SCRAPHELI = "MSG_VEHICLE_NAME_SCRAPHELI";
        public const string MSG_VEHICLE_NAME_SCRAPHELIS = "MSG_VEHICLE_NAME_SCRAPHELIS";

        public const string MSG_VEHICLE_NAME_HAB = "MSG_VEHICLE_NAME_HAB";
        public const string MSG_VEHICLE_NAME_HABS = "MSG_VEHICLE_NAME_HABS";

        public const string MSG_VEHICLE_NAME_ROWBOAT = "MSG_VEHICLE_NAME_ROWBOAT";
        public const string MSG_VEHICLE_NAME_ROWBOATS = "MSG_VEHICLE_NAME_ROWBOATS";

        public const string MSG_VEHICLE_NAME_RHIB = "MSG_VEHICLE_NAME_RHIB";
        public const string MSG_VEHICLE_NAME_RHIBS = "MSG_VEHICLE_NAME_RHIBS";

        public const string MSG_VEHICLE_NAME_TRAIN = "MSG_VEHICLE_NAME_TRAIN";
        public const string MSG_VEHICLE_NAME_TRAINS = "MSG_VEHICLE_NAME_TRAINS";

        public const string MSG_VEHICLE_NAME_CRANE = "MSG_VEHICLE_NAME_CRANE";
        public const string MSG_VEHICLE_NAME_CRANES = "MSG_VEHICLE_NAME_CRANES";

        public const string MSG_VEHICLE_NAME_SUBSOLO = "MSG_VEHICLE_NAME_SUBSOLO";
        public const string MSG_VEHICLE_NAME_SUBDUO = "MSG_VEHICLE_NAME_SUBDUO";

        public const string MSG_VEHICLE_NAME_SUBSOLOS = "MSG_VEHICLE_NAME_SUBSOLOS";
        public const string MSG_VEHICLE_NAME_SUBDUOS = "MSG_VEHICLE_NAME_SUBDUOS";

        public const string MSG_VEHICLE_NAME_SNOWMOBILE_RED = "MSG_VEHICLE_NAME_SNOWMOBILE_RED";
        public const string MSG_VEHICLE_NAME_SNOWMOBILE_REDS = "MSG_VEHICLE_NAME_SNOWMOBILE_REDS";

        public const string MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHA = "MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHA";
        public const string MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHAS = "MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHAS";

        public const string MSG_VEHICLE_NAME_TUGBOAT = "MSG_VEHICLE_NAME_TUGBOAT";
        public const string MSG_VEHICLE_NAME_TUGBOATS = "MSG_VEHICLE_NAME_TUGBOATS";

        public const string MSG_VEHICLE_NAME_ATTACKHELICOPTER = "MSG_VEHICLE_NAME_ATTACKHELICOPTER";
        public const string MSG_VEHICLE_NAME_ATTACKHELICOPTERS = "MSG_VEHICLE_NAME_ATTACKHELICOPTERS";

        public const string MSG_VEHICLE_NAME_MOTORBIKE = "MSG_VEHICLE_NAME_MOTORBIKE";
        public const string MSG_VEHICLE_NAME_MOTORBIKES = "MSG_VEHICLE_NAME_MOTORBIKES";

        public const string MSG_VEHICLE_NAME_MOTORTRIKE = "MSG_VEHICLE_NAME_MOTORTRIKE";
        public const string MSG_VEHICLE_NAME_MOTORTRIKES = "MSG_VEHICLE_NAME_MOTORTRIKES";

        public const string MSG_AVAILABLE_FUELS = "MSG_AVAILABLE_FUELS";
        public const string MSG_VEHICLES_DONT_NEED_FUEL = "MSG_VEHICLES_DONT_NEED_FUEL";
        public const string MSG_THESE_VEHICLES_DONT_NEED_FUEL = "MSG_THESE_VEHICLES_DONT_NEED_FUEL";

        public const string MSG_REASON_OK = "MSG_REASON_OK";

        public const string MSG_REASON_FUEL_NOT_DEFINED = "MSG_REASON_FUEL_NOT_DEFINED";
        public const string MSG_REASON_FUEL_DISABLED = "MSG_REASON_FUEL_DISABLED";
        public const string MSG_REASON_VEHICLE_DISABLED = "MSG_REASON_VEHICLE_DISABLED";

        public const string MSG_REASON_NO_PERMISSION_FUEL = "MSG_REASON_NO_PERMISSION_FUEL";
        public const string MSG_REASON_NO_PERMISSION_VEHICLE = "MSG_REASON_NO_PERMISSION_VEHICLE";
        public const string MSG_GUI_NO_PERMISSION_SET = "MSG_NO_PERMISSION_SET";

        public const string MSG_CANT_USE_THIS_FUEL = "MSG_CANT_USE_THIS_FUEL";

        public const string MSG_GUI_PAGE = "MSG_GUI_PAGE";
        public const string MSG_GUI_CONSUMPTION_AMOUNT = "MSG_GUI_CONSUMPTION_AMOUNT";
        public const string MSG_GUI_MULTIPLIER = "MSG_GUI_MULTIPLIER";

        public const string MSG_GUI_CURRENT_VALUE = "MSG_GUI_CURRENT_VALUE";

        public const string MSG_GUI_COUNTERS_ENABLE = "MSG_GUI_COUNTERS_ENABLE";
        public const string MSG_GUI_COUNTERS_DISABLE = "MSG_GUI_COUNTERS_DISABLE";
        public const string MSG_GUI_DONT_NEED_FUEL = "MSG_GUI_DONT_NEED_FUEL";
        public const string MSG_GUI_NEED_FUEL = "MSG_GUI_NEED_FUEL";

        public const string MSG_GUI_ENABLED = "MSG_GUI_ENABLED";
        public const string MSG_GUI_DISABLED = "MSG_GUI_DISABLED";

        public const string MSG_GUI_RESTORE_DEFAULTS = "MSG_GUI_RESTORE_DEFAULTS";

        public const string MSG_GUI_PAGE_NEXT = "MSG_GUI_PAGE_NEXT";
        public const string MSG_GUI_PAGE_PREV = "MSG_GUI_PAGE_PREV";

        public const string MSG_GUI_CLEAR_PERM = "MSG_GUI_CLEAR_PERM";
        public const string MSG_GUI_REMOVE_ITEM = "MSG_GUI_REMOVE_ITEM";
        public const string MSG_GUI_FUEL_ENABLED = "MSG_GUI_FUEL_ENABLED";
        public const string MSG_GUI_FUEL_DISABLED = "MSG_GUI_FUEL_DISABLED";
        public const string MSG_GUI_PERM_NEEDED = "MSG_GUI_PERM_NEEDED";

        public const string MSG_GUI_ADD_NEW_ITEM = "MSG_GUI_ADD_NEW_ITEM";
        public const string MSG_GUI_CONSUMPTION_CONDITION = "MSG_GUI_CONSUMPTION_CONDITION";
        public const string MSG_GUI_CONSUMPTION_ITEM = "MSG_GUI_CONSUMPTION_ITEM";
        public const string MSG_GUI_CONSUMPTION_DATA = "MSG_GUI_CONSUMPTION_DATA";
        public const string MSG_GUI_CONSUMPTION_NONE = "MSG_GUI_CONSUMPTION_NONE";
        public const string MSG_GUI_CONSUMPTION_CONTENTS = "MSG_GUI_CONSUMPTION_CONTENTS";


        public static Dictionary<byte, string> ReasonStrings = new Dictionary<byte, string>
        {
            [REASON_OK] = MSG_REASON_OK,
            [REASON_FUEL_NOT_DEFINED] = MSG_REASON_FUEL_NOT_DEFINED,
            [REASON_FUEL_DISABLED] = MSG_REASON_FUEL_DISABLED,
            [REASON_FUEL_VEHICLE_DISABLED] = MSG_REASON_VEHICLE_DISABLED,
            [REASON_NO_PERMISSION_FUEL] = MSG_REASON_NO_PERMISSION_FUEL,
            [REASON_NO_PERMISSION_VEHICLE] = MSG_REASON_NO_PERMISSION_VEHICLE,
        };

        public static Dictionary<VehicleType, string> LangVehicleNameSingular = new Dictionary<VehicleType, string>
        {
            [VehicleType.Car] = MSG_VEHICLE_NAME_CAR,
            [VehicleType.Minicopter] = MSG_VEHICLE_NAME_MINICOPTER,
            [VehicleType.Rhib] = MSG_VEHICLE_NAME_RHIB,
            [VehicleType.Rowboat] = MSG_VEHICLE_NAME_ROWBOAT,
            [VehicleType.Scrapheli] = MSG_VEHICLE_NAME_SCRAPHELI,
            [VehicleType.HotAirBalloon] = MSG_VEHICLE_NAME_HAB,
            [VehicleType.Train] = MSG_VEHICLE_NAME_TRAIN,
            [VehicleType.Crane] = MSG_VEHICLE_NAME_CRANE,
            [VehicleType.SubSolo] = MSG_VEHICLE_NAME_SUBSOLO,
            [VehicleType.SubDuo] = MSG_VEHICLE_NAME_SUBDUO,
            [VehicleType.SnowmobileRed] = MSG_VEHICLE_NAME_SNOWMOBILE_RED,
            [VehicleType.SnowmobileTomaha] = MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHA,
            [VehicleType.Tugboat] = MSG_VEHICLE_NAME_TUGBOAT,
            [VehicleType.AttackHelicopter] = MSG_VEHICLE_NAME_ATTACKHELICOPTER,
            [VehicleType.Motorbike] = MSG_VEHICLE_NAME_MOTORBIKE,
            [VehicleType.Motortrike] = MSG_VEHICLE_NAME_MOTORTRIKE
        };

        public static Dictionary<VehicleType, string> LangVehicleNamePlural = new Dictionary<VehicleType, string>
        {
            [VehicleType.Car] = MSG_VEHICLE_NAME_CARS,
            [VehicleType.Minicopter] = MSG_VEHICLE_NAME_MINICOPTERS,
            [VehicleType.Rhib] = MSG_VEHICLE_NAME_RHIBS,
            [VehicleType.Rowboat] = MSG_VEHICLE_NAME_ROWBOATS,
            [VehicleType.Scrapheli] = MSG_VEHICLE_NAME_SCRAPHELIS,
            [VehicleType.HotAirBalloon] = MSG_VEHICLE_NAME_HABS,
            [VehicleType.Train] = MSG_VEHICLE_NAME_TRAINS,
            [VehicleType.Crane] = MSG_VEHICLE_NAME_CRANES,
            [VehicleType.SubSolo] = MSG_VEHICLE_NAME_SUBSOLOS,
            [VehicleType.SubDuo] = MSG_VEHICLE_NAME_SUBDUOS,
            [VehicleType.SnowmobileRed] = MSG_VEHICLE_NAME_SNOWMOBILE_REDS,
            [VehicleType.SnowmobileTomaha] = MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHAS,
            [VehicleType.Tugboat] = MSG_VEHICLE_NAME_TUGBOATS,
            [VehicleType.AttackHelicopter] = MSG_VEHICLE_NAME_ATTACKHELICOPTERS,
            [VehicleType.Motorbike] = MSG_VEHICLE_NAME_MOTORBIKES,
            [VehicleType.Motortrike] = MSG_VEHICLE_NAME_MOTORTRIKES
        };

        public static Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            [MSG_GENERAL] = "General",

            [MSG_VEHICLE] = "Vehicle",
            [MSG_VEHICLES] = "Vehicles",

            [MSG_VEHICLE_NAME_CAR] = "Modular Car",
            [MSG_VEHICLE_NAME_CARS] = "Modular Cars",

            [MSG_VEHICLE_NAME_MINICOPTER] = "Minicopter",
            [MSG_VEHICLE_NAME_MINICOPTERS] = "Minicopters",

            [MSG_VEHICLE_NAME_SCRAPHELI] = "Scrapheli",
            [MSG_VEHICLE_NAME_SCRAPHELIS] = "Scraphelis",

            [MSG_VEHICLE_NAME_HAB] = "HAB",
            [MSG_VEHICLE_NAME_HABS] = "HABs",

            [MSG_VEHICLE_NAME_ROWBOAT] = "Rowboat",
            [MSG_VEHICLE_NAME_ROWBOATS] = "Rowboats",

            [MSG_VEHICLE_NAME_RHIB] = "RHIB",
            [MSG_VEHICLE_NAME_RHIBS] = "RHIBs",

            [MSG_VEHICLE_NAME_TRAIN] = "Workcart",
            [MSG_VEHICLE_NAME_TRAINS] = "Workcarts",

            [MSG_VEHICLE_NAME_CRANE] = "Crane",
            [MSG_VEHICLE_NAME_CRANES] = "Cranes",

            [MSG_VEHICLE_NAME_SUBSOLO] = "Solo Sub",
            [MSG_VEHICLE_NAME_SUBSOLOS] = "Solo Subs",

            [MSG_VEHICLE_NAME_SUBDUO] = "Duo Sub",
            [MSG_VEHICLE_NAME_SUBDUOS] = "Duo Subs",

            [MSG_VEHICLE_NAME_SNOWMOBILE_RED] = "Red Snowmobile",
            [MSG_VEHICLE_NAME_SNOWMOBILE_REDS] = "Red Snowmobiles",

            [MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHA] = "Tomaha Snowmobile",
            [MSG_VEHICLE_NAME_SNOWMOBILE_TOMAHAS] = "Tomaha Snowmobiles",

            [MSG_VEHICLE_NAME_TUGBOAT] = "Tugboat",
            [MSG_VEHICLE_NAME_TUGBOATS] = "Tugboats",

            [MSG_VEHICLE_NAME_ATTACKHELICOPTER] = "Attack Helicopter",
            [MSG_VEHICLE_NAME_ATTACKHELICOPTERS] = "Attack Helicopters",

            [MSG_VEHICLE_NAME_MOTORBIKE] = "Motor Bike",
            [MSG_VEHICLE_NAME_MOTORBIKES] = "Motor Bikes",
            [MSG_VEHICLE_NAME_MOTORTRIKE] = "Motor Trike",
            [MSG_VEHICLE_NAME_MOTORTRIKES] = "Motor Trikes",

            [MSG_AVAILABLE_FUELS] = "{0} will accept the following items as fuel:",
            [MSG_VEHICLES_DONT_NEED_FUEL] = "Vehicles don't need any fuel to run. Enjoy!",
            [MSG_THESE_VEHICLES_DONT_NEED_FUEL] = "{0} don't need any fuel to run. Enjoy!",

            [MSG_CANT_USE_THIS_FUEL] = "You cannot drive a {0} that runs on {1}.\nReason: ",

            [MSG_REASON_OK] = "Everything went okay.", //currently not used

            [MSG_REASON_FUEL_NOT_DEFINED] = "This fuel has no defined properties.",
            [MSG_REASON_FUEL_DISABLED] = "This fuel type is disabled for all vehicles.",
            [MSG_REASON_VEHICLE_DISABLED] = "This fuel type is disabled for this vehicle.",
            [MSG_REASON_NO_PERMISSION_FUEL] = "You don't have permission to use this fuel on any vehicles.",
            [MSG_REASON_NO_PERMISSION_VEHICLE] = "You don't have permission to use this fuel on this particular vehicle.",

            [MSG_GUI_PAGE] = "Page",
            [MSG_GUI_CONSUMPTION_AMOUNT] = "{0} consumption",
            [MSG_GUI_MULTIPLIER] = "{0} usage multiplier",

            [MSG_GUI_ENABLED] = "GUI enabled for {0}",
            [MSG_GUI_DISABLED] = "GUI disabled for {0}",

            [MSG_GUI_PAGE_NEXT] = "<< PREVIOUS PAGE",
            [MSG_GUI_PAGE_PREV] = "NEXT PAGE >>",

            [MSG_GUI_CURRENT_VALUE] = "CURRENT VALUE",

            [MSG_GUI_COUNTERS_ENABLE] = "Enable fuel Counters for {0}",
            [MSG_GUI_COUNTERS_DISABLE] = "Disable fuel Counters for {0}",

            [MSG_GUI_DONT_NEED_FUEL] = "{0} don't need fuel to run",
            [MSG_GUI_NEED_FUEL] = "{0} need fuel to run",

            [MSG_GUI_RESTORE_DEFAULTS] = "RESTORE TO DEFAULT",

            [MSG_GUI_CLEAR_PERM] = "CLEAR PERM",
            [MSG_GUI_REMOVE_ITEM] = "X",
            [MSG_GUI_FUEL_ENABLED] = "Enabled for {0}",
            [MSG_GUI_FUEL_DISABLED] = "Disabled for {0}",

            [MSG_GUI_NO_PERMISSION_SET] = "NO PERMISSION SET",

            [MSG_GUI_PERM_NEEDED] = "{0} fuel perm needed",

            [MSG_GUI_ADD_NEW_ITEM] = "Add new item (name or ID)",

            [MSG_GUI_CONSUMPTION_CONDITION] = "Damage condition (like diving tank)",
            [MSG_GUI_CONSUMPTION_ITEM] = "Consume item (like normal fuel)",
            [MSG_GUI_CONSUMPTION_DATA] = "Take from item data (like battery)",
            [MSG_GUI_CONSUMPTION_NONE] = "Unlimited (don't consume)",
            [MSG_GUI_CONSUMPTION_CONTENTS] = "Take from contents (like water/fuel)",
        };

        public static string VEH(VehicleType type, string userID = null, bool plural = false)
        {
            return plural ? MSG(LangVehicleNamePlural[type], userID) : MSG(LangVehicleNameSingular[type], userID);
        }

        public static string REASON(byte reason, string userID = null)
        {
            return MSG(ReasonStrings[reason], userID);
        }

        public static string MSG(string msg, string userID = null, params object[] args)
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

        #region PERMISSIONS
        public const string PERMISSION_VIP_1 = "fuelmanager.vip1";
        public const string PERMISSION_VIP_2 = "fuelmanager.vip2";
        public const string PERMISSION_VIP_3 = "fuelmanager.vip3";
        public const string PERMISSION_VIP_4 = "fuelmanager.vip4";
        public const string PERMISSION_VIP_5 = "fuelmanager.vip5";

        public const string PERMISSION_ADMIN = "fuelmanager.admin";

        public static bool HasPermission(BasePlayer player, string perm)
        {
            return Instance.permission.UserHasPermission(player.UserIDString, perm);
        }

        #endregion

        #region CONFIG
        public ConfigData configData;

        protected override void LoadDefaultConfig()
        {
            RestoreDefaultConfig();
        }
        private void ProcessConfigData()
        {
            if (configData.fuelDefinitions == null)
            {
                GenerateDefaultFuelDefinitions();
            }


            if (configData.version != VERSION)
            {
                var presentVersion = configData.version;

                if (presentVersion != VERSION && VERSION == "1.0.1")
                {
                    TrainUpdateMigration();
                }

                if (presentVersion != VERSION && VERSION == "1.0.2")
                {
                    CraneUpdateMigration();
                }

                if (presentVersion != VERSION && VERSION == "1.0.3")
                {
                    SubmarineUpdateMigration();
                }

                if (presentVersion == "1.0.7" || presentVersion == "1.0.6" || presentVersion == "1.0.5" || presentVersion == "1.0.4")
                {
                    SnowmobileUpdateMigration();
                }

                if (presentVersion == "1.0.12" || presentVersion == "1.0.11" || presentVersion == "1.0.10")
                {
                    AttackHeliTugboatAndBikeMigration();
                }

                configData.version = VERSION;
                Instance.PrintWarning($"\n\nYou have succesfully updated from {presentVersion} to {VERSION}\n");
                SaveConfigData();
            }
        }

        private void TrainUpdateMigration()
        {
            //first: specific for global config, add missing train entries...
            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.Train))
            {
                configData.specificCountersEnabled.Add(VehicleType.Train, true);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.Train))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.Train, false);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.Train))
            {
                configData.specificEnableGUI.Add(VehicleType.Train, true);
            }

            //second: missing fuel definitions

            foreach (var entry in configData.fuelDefinitions)
            {
                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.Train))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.Train, new VehicleFuelSpecifics());
                }
            }

        }

        private void CraneUpdateMigration()
        {
            //first: specific for global config, add missing train entries...
            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.Crane))
            {
                configData.specificCountersEnabled.Add(VehicleType.Crane, true);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.Crane))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.Crane, false);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.Crane))
            {
                configData.specificEnableGUI.Add(VehicleType.Crane, true);
            }

            //second: missing fuel definitions

            foreach (var entry in configData.fuelDefinitions)
            {
                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.Crane))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.Crane, new VehicleFuelSpecifics());
                }
            }

        }

        private void SubmarineUpdateMigration()
        {
            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.SubSolo))
            {
                configData.specificCountersEnabled.Add(VehicleType.SubSolo, true);
            }

            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.SubDuo))
            {
                configData.specificCountersEnabled.Add(VehicleType.SubDuo, true);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.SubSolo))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.SubSolo, false);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.SubDuo))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.SubDuo, false);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.SubSolo))
            {
                configData.specificEnableGUI.Add(VehicleType.SubSolo, true);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.SubDuo))
            {
                configData.specificEnableGUI.Add(VehicleType.SubDuo, true);
            }

            //second: missing fuel definitions

            foreach (var entry in configData.fuelDefinitions)
            {
                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.SubSolo))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.SubSolo, new VehicleFuelSpecifics());
                }

                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.SubDuo))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.SubSolo, new VehicleFuelSpecifics());
                }
            }

        }

        private void SnowmobileUpdateMigration()
        {
            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.SnowmobileRed))
            {
                configData.specificCountersEnabled.Add(VehicleType.SnowmobileRed, true);
            }

            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.SnowmobileTomaha))
            {
                configData.specificCountersEnabled.Add(VehicleType.SnowmobileTomaha, true);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.SnowmobileRed))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.SnowmobileRed, false);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.SnowmobileTomaha))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.SnowmobileTomaha, false);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.SnowmobileRed))
            {
                configData.specificEnableGUI.Add(VehicleType.SnowmobileRed, true);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.SnowmobileTomaha))
            {
                configData.specificEnableGUI.Add(VehicleType.SnowmobileTomaha, true);
            }

            //second: missing fuel definitions

            foreach (var entry in configData.fuelDefinitions)
            {
                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.SnowmobileRed))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.SnowmobileRed, new VehicleFuelSpecifics());
                }

                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.SnowmobileTomaha))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.SnowmobileTomaha, new VehicleFuelSpecifics());
                }
            }
        }

        private void AttackHeliTugboatAndBikeMigration()
        {
            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.AttackHelicopter))
            {
                configData.specificCountersEnabled.Add(VehicleType.AttackHelicopter, true);
            }

            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.Tugboat))
            {
                configData.specificCountersEnabled.Add(VehicleType.Tugboat, true);
            }

            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.Motorbike))
            {
                configData.specificCountersEnabled.Add(VehicleType.Motorbike, true);
            }

            if (!configData.specificCountersEnabled.ContainsKey(VehicleType.Motortrike))
            {
                configData.specificCountersEnabled.Add(VehicleType.Motortrike, true);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.AttackHelicopter))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.AttackHelicopter, false);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.Tugboat))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.Tugboat, false);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.Motorbike))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.Motorbike, false);
            }

            if (!configData.specificNoNeedForFuel.ContainsKey(VehicleType.Motortrike))
            {
                configData.specificNoNeedForFuel.Add(VehicleType.Motortrike, false);
            }


            if (!configData.specificEnableGUI.ContainsKey(VehicleType.AttackHelicopter))
            {
                configData.specificEnableGUI.Add(VehicleType.AttackHelicopter, true);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.Tugboat))
            {
                configData.specificEnableGUI.Add(VehicleType.Tugboat, true);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.Motorbike))
            {
                configData.specificEnableGUI.Add(VehicleType.Motorbike, true);
            }

            if (!configData.specificEnableGUI.ContainsKey(VehicleType.Motortrike))
            {
                configData.specificEnableGUI.Add(VehicleType.Motortrike, true);
            }

            foreach (var entry in configData.fuelDefinitions)
            {
                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.AttackHelicopter))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.AttackHelicopter, new VehicleFuelSpecifics());
                }

                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.Tugboat))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.Tugboat, new VehicleFuelSpecifics());
                }

                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.Motorbike))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.Motorbike, new VehicleFuelSpecifics());
                }

                if (!entry.Value.specificVehicleSettings.ContainsKey(VehicleType.Motortrike))
                {
                    entry.Value.specificVehicleSettings.Add(VehicleType.Motortrike, new VehicleFuelSpecifics());
                }
            }



        }

        private void RestoreDefaultConfig()
        {
            PrintWarning("Generating default config...");

            configData = new ConfigData();

            GenerateDefaultFuelDefinitions();

            if (Instance != null)
            {
                GuiManagerPlayer.GenerateAllVehicleGUIs();
            }

            SaveConfigData();
        }

        private void LoadConfigData()
        {
            PrintWarning("Loading configuration file...");
            try
            {
                configData = Config.ReadObject<ConfigData>();
                PrintWarning("Success.");
            }
            catch
            {
                configData = new ConfigData();
                PrintWarning("Loading failed, generating new...");

            }
            SaveConfigData();

        }
        private void SaveConfigData()
        {
            PrintWarning("Saving config...");
            Config.WriteObject(configData, true);
        }

        public static FuelDefinition GetDefaultFuelDefinitionForItem(int itemID)
        {
            switch (itemID)
            {
                case 0:
                    {
                        return null;
                    }
                case ITEM_FERTILIZER:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                    };
                case ITEM_LOWGRADE:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                    };
                case ITEM_DUNG:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        globalUsageMultiplier = 4.0F,
                    };
                case ITEM_SULFUR:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        globalUsageMultiplier = 15.0F,
                    };
                case ITEM_GUNPOWDER:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        globalUsageMultiplier = 5.0F,
                    };
                case ITEM_CRUDE:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        globalUsageMultiplier = 0.5F,
                    };
                case ITEM_DIESEL:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        globalUsageMultiplier = 0.1F
                    };
                case ITEM_BATTERY_SMALL:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.DataInt,
                        globalUsageMultiplier = 4.0F,
                        globalConsumptionAmount = 60,
                    };
                case ITEM_BATTERY_MEDIUM:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.DataInt,
                        globalUsageMultiplier = 4.0F,
                        globalConsumptionAmount = 60,
                    };
                case ITEM_BATTERY_LARGE:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.DataInt,
                        globalUsageMultiplier = 4.0F,
                        globalConsumptionAmount = 60,
                    };
                case ITEM_WATER_BOTTLE:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.Contents,
                        globalUsageMultiplier = 8.0F,
                        globalConsumptionAmount = 50,
                        globalEnabled = false,
                    };
                case ITEM_BOTA_BAG:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.Contents,
                        globalUsageMultiplier = 8.0F,
                        globalConsumptionAmount = 50,
                        globalEnabled = false,
                    };
                case ITEM_WATER_JUG:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.Contents,
                        globalUsageMultiplier = 8.0F,
                        globalConsumptionAmount = 50,
                        globalEnabled = false,
                    };
                case ITEM_TORCH:
                    return new FuelDefinition
                    {
                        itemID = itemID,
                        consumptionType = ConsumptionType.Condition,
                        globalUsageMultiplier = 1F,
                        globalConsumptionAmount = 5,
                        globalEnabled = false,
                    };
                case ITEM_DIVING_TANK:
                case ITEM_JACKHAMMER:
                    {
                        return new FuelDefinition
                        {
                            itemID = itemID,
                            consumptionType = ConsumptionType.Condition,
                            globalUsageMultiplier = 1F,
                            globalConsumptionAmount = 20,
                        };
                    }
                case ITEM_TUNA_LAMP:
                case ITEM_LANTERN:
                case ITEM_MINERS_HAT:
                case ITEM_CANDLE_HAT:
                    {
                        return new FuelDefinition
                        {
                            itemID = itemID,
                            consumptionType = ConsumptionType.Contents,
                            globalUsageMultiplier = 1F,
                            globalConsumptionAmount = 1,
                        };
                    }
                default:
                    {
                        var itemDef = ItemManager.FindItemDefinition(itemID);
                        var consumptionType = ConsumptionType.Item;

                        if (itemDef != null)
                        {
                            if (itemDef.condition.enabled || itemDef.condition.repairable)
                            {
                                consumptionType = ConsumptionType.Condition;
                            }
                        }

                        return new FuelDefinition
                        {
                            itemID = itemID,
                            consumptionType = consumptionType,
                            globalUsageMultiplier = 1F,
                            globalConsumptionAmount = 1,
                        };
                    }

            }
        }

        private void GenerateDefaultFuelDefinitions()
        {
            if (configData != null)
            {
                PrintWarning("Generating default fuel definitions...");
                configData.fuelDefinitions = new Dictionary<int, FuelDefinition>
                {
                    [ITEM_LOWGRADE] = GetDefaultFuelDefinitionForItem(ITEM_LOWGRADE),
                    [ITEM_BATTERY_SMALL] = GetDefaultFuelDefinitionForItem(ITEM_BATTERY_SMALL),
                    [ITEM_TUNA_LAMP] = GetDefaultFuelDefinitionForItem(ITEM_TUNA_LAMP),
                    [ITEM_DIVING_TANK] = GetDefaultFuelDefinitionForItem(ITEM_DIVING_TANK),
                    [ITEM_LANTERN] = GetDefaultFuelDefinitionForItem(ITEM_LANTERN),
                    [ITEM_BATTERY_MEDIUM] = GetDefaultFuelDefinitionForItem(ITEM_BATTERY_MEDIUM),
                    [ITEM_BATTERY_LARGE] = GetDefaultFuelDefinitionForItem(ITEM_BATTERY_LARGE),
                    [ITEM_JACKHAMMER] = GetDefaultFuelDefinitionForItem(ITEM_JACKHAMMER),
                    [ITEM_FERTILIZER] = GetDefaultFuelDefinitionForItem(ITEM_FERTILIZER),
                    [ITEM_DUNG] = GetDefaultFuelDefinitionForItem(ITEM_DUNG),
                    [ITEM_SULFUR] = GetDefaultFuelDefinitionForItem(ITEM_SULFUR),
                    [ITEM_GUNPOWDER] = GetDefaultFuelDefinitionForItem(ITEM_GUNPOWDER),
                    [ITEM_CRUDE] = GetDefaultFuelDefinitionForItem(ITEM_CRUDE),
                    [ITEM_DIESEL] = GetDefaultFuelDefinitionForItem(ITEM_DIESEL),
                    [ITEM_BOTA_BAG] = GetDefaultFuelDefinitionForItem(ITEM_BOTA_BAG),
                    [ITEM_WATER_JUG] = GetDefaultFuelDefinitionForItem(ITEM_WATER_JUG),
                    [ITEM_WATER_BOTTLE] = GetDefaultFuelDefinitionForItem(ITEM_WATER_BOTTLE),
                    [ITEM_TORCH] = GetDefaultFuelDefinitionForItem(ITEM_TORCH),
                    [ITEM_CANDLE_HAT] = GetDefaultFuelDefinitionForItem(ITEM_CANDLE_HAT),
                    [ITEM_MINERS_HAT] = GetDefaultFuelDefinitionForItem(ITEM_MINERS_HAT),
                };
                SaveConfigData();
            }
        }


        public enum SkinMode
        {
            AcceptAll,
            Whitelist,
            Blacklist
        }

        public enum ConsumptionType
        {
            None, //don't consume anything, just pretend as if you have had
            Item, //consume the item in the tank (like lowgrade)
            DataInt, //consume from data int (like electricity)
            Condition, //consume from item condition (like jackhammer/diving tank)
            Contents //consume from item's contents (like water jug)
        }

        public enum VehicleType
        {
            Car,
            Minicopter,
            Scrapheli,
            Rowboat,
            Rhib,
            HotAirBalloon,
            Train,
            Crane,
            SubDuo,
            SubSolo,
            SnowmobileRed,
            SnowmobileTomaha,
            Tugboat,
            AttackHelicopter,
            Motorbike,
            Motortrike
        }

        public class VehicleFuelSpecifics
        {
            public string specificPermissionNeeded = null;
            public bool specificEnabled = true;

            public int specificConsumptionAmount = 1; //consume 1 item, 1 from data int, 1 from condition, or 1 from contents every time 1 lowgrade would've been consumed
            public float specificUsageMultiplier = 1.0F; //1.0F = default for lowgrade. Set it to 2.0F to use the fuel twice as often. Combine with consumptionAmount for different usage intervals.
        }

        public class FuelDefinition
        {
            public int itemID = 0;

            public SkinMode skinMode = SkinMode.AcceptAll;
            public List<ulong> skinIDs = new List<ulong>();

            public ConsumptionType consumptionType = ConsumptionType.Item;

            //both of those will be multiplied / logical AND-ed by their vehicle specific
            public string globalPermissionNeeded = null;
            public bool globalEnabled = true;

            public int globalConsumptionAmount = 1; //consume 1 item, 1 from data int, 1 from condition, or 1 from contents every time 1 lowgrade would've been consumed
            public float globalUsageMultiplier = 1.0F; //1.0F = default for lowgrade. Set it to 2.0F to use the fuel twice as often. Combine with consumptionAmount for different usage intervals.

            public Dictionary<VehicleType, VehicleFuelSpecifics> specificVehicleSettings = new Dictionary<VehicleType, VehicleFuelSpecifics>
            {
                [VehicleType.Car] = new VehicleFuelSpecifics(),
                [VehicleType.Minicopter] = new VehicleFuelSpecifics(),
                [VehicleType.Scrapheli] = new VehicleFuelSpecifics(),
                [VehicleType.Rowboat] = new VehicleFuelSpecifics(),
                [VehicleType.Rhib] = new VehicleFuelSpecifics(),
                [VehicleType.HotAirBalloon] = new VehicleFuelSpecifics(),
                [VehicleType.Train] = new VehicleFuelSpecifics(),
                [VehicleType.Crane] = new VehicleFuelSpecifics(),
                [VehicleType.SubSolo] = new VehicleFuelSpecifics(),
                [VehicleType.SubDuo] = new VehicleFuelSpecifics(),
                [VehicleType.SnowmobileRed] = new VehicleFuelSpecifics(),
                [VehicleType.SnowmobileTomaha] = new VehicleFuelSpecifics(),
                [VehicleType.Tugboat] = new VehicleFuelSpecifics(),
                [VehicleType.AttackHelicopter] = new VehicleFuelSpecifics(),
                [VehicleType.Motorbike] = new VehicleFuelSpecifics(),
                [VehicleType.Motortrike] = new VehicleFuelSpecifics(),
            };
        }


        public class ConfigData
        {
            public string version = VERSION;

            //if this is set to true, vehicles will always think they have fuel and won't consume anything
            public bool globalCountersEnabled = true;

            public bool globalNoNeedForFuel = false;

            public bool globalEnableGUI = true;

            public Dictionary<VehicleType, bool> specificCountersEnabled = new Dictionary<VehicleType, bool>
            {
                [VehicleType.Car] = true,
                [VehicleType.Minicopter] = true,
                [VehicleType.Scrapheli] = true,
                [VehicleType.Rowboat] = true,
                [VehicleType.Rhib] = true,
                [VehicleType.HotAirBalloon] = true,
                [VehicleType.Train] = true,
                [VehicleType.Crane] = true,
                [VehicleType.SubSolo] = true,
                [VehicleType.SubDuo] = true,
                [VehicleType.SnowmobileRed] = true,
                [VehicleType.SnowmobileTomaha] = true,
                [VehicleType.Tugboat] = true,
                [VehicleType.AttackHelicopter] = true,
                [VehicleType.Motorbike] = true,
                [VehicleType.Motortrike] = true,

            };

            public Dictionary<VehicleType, bool> specificNoNeedForFuel = new Dictionary<VehicleType, bool>
            {
                [VehicleType.Car] = false,
                [VehicleType.Minicopter] = false,
                [VehicleType.Scrapheli] = false,
                [VehicleType.Rowboat] = false,
                [VehicleType.Rhib] = false,
                [VehicleType.HotAirBalloon] = false,
                [VehicleType.Train] = false,
                [VehicleType.Crane] = false,
                [VehicleType.SubSolo] = false,
                [VehicleType.SubDuo] = false,
                [VehicleType.SnowmobileRed] = false,
                [VehicleType.SnowmobileTomaha] = false,
                [VehicleType.Tugboat] = false,
                [VehicleType.AttackHelicopter] = false,
                [VehicleType.Motorbike] = false,
                [VehicleType.Motortrike] = false,
            };

            public Dictionary<VehicleType, bool> specificEnableGUI = new Dictionary<VehicleType, bool>
            {
                [VehicleType.Car] = true,
                [VehicleType.Minicopter] = true,
                [VehicleType.Scrapheli] = true,
                [VehicleType.Rowboat] = true,
                [VehicleType.Rhib] = true,
                [VehicleType.HotAirBalloon] = true,
                [VehicleType.Train] = true,
                [VehicleType.Crane] = true,
                [VehicleType.SubSolo] = true,
                [VehicleType.SubDuo] = true,
                [VehicleType.SnowmobileRed] = true,
                [VehicleType.SnowmobileTomaha] = true,
                [VehicleType.Tugboat] = true,
                [VehicleType.AttackHelicopter] = true,
                [VehicleType.Motorbike] = true,
                [VehicleType.Motortrike] = true,
            };

            public Dictionary<int, FuelDefinition> fuelDefinitions = null;
        }
        #endregion

        #region COLORS
        public class ColorCode
        {
            public string hexValue;
            public UnityEngine.Color rustValue;
            public string rustString;
            public ColorCode(string hex)
            {
                hex = hex.ToUpper();

                hexValue = "#" + hex;

                //extract the R, G, B
                var r = (float)short.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var g = (float)short.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var b = (float)short.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255;

                rustValue = new UnityEngine.Color(r, g, b);
                rustString = $"{r} {g} {b}";
            }
        }

        public static class ColorPalette
        {
            public static ColorCode Black = new ColorCode("000000");
            public static ColorCode White = new ColorCode("ffffff");
            
            public static ColorCode RustyRed = new ColorCode("ce422b");
            public static ColorCode RustyYellow = new ColorCode("cebd2b");
            public static ColorCode RustyGreen = new ColorCode("8fba45");
            public static ColorCode RustyAqua = new ColorCode("45b9ba");
            public static ColorCode RustyBlue = new ColorCode("4897ce");



            //player GUI colours
            public static ColorCode DirtyPink = new ColorCode("e8dcd3");
            public static ColorCode GreyDark = new ColorCode("3f3d36");
            public static ColorCode GreyLight = new ColorCode("9c9c9c");

            //menu colours
            public static ColorCode RedLight = new ColorCode("ff6b6b");
            public static ColorCode YellowLight = new ColorCode("ffd36b");
            public static ColorCode LimeLight = new ColorCode("d4ff6b");
            public static ColorCode GreenLight = new ColorCode("76ff6b");
            public static ColorCode AquaLight = new ColorCode("6bffef");
            public static ColorCode BlueLight = new ColorCode("6bbbff");
            public static ColorCode PurpleLight = new ColorCode("ad6bff");
            public static ColorCode PinkLight = new ColorCode("ff6be8");

            public static ColorCode OrangeLight = new ColorCode("fa5800");
            public static ColorCode PissYellowLight = new ColorCode("e5fa00");

            public static ColorCode OrangeDark = new ColorCode("802d00");
            public static ColorCode PissYellowDark = new ColorCode("758000");//

            public static ColorCode BurgundyDark = new ColorCode("7a2a2f");
            public static ColorCode BurgundyLight = new ColorCode("c66f74");

            public static ColorCode CaramelDark = new ColorCode("73581f");
            public static ColorCode CaramelLight = new ColorCode("bc9c59");

            public static ColorCode SandyDark = new ColorCode("bac195");
            public static ColorCode SandyLight = new ColorCode("f3ffae");

            public static ColorCode CeladonDark = new ColorCode("96a92d");
            public static ColorCode CeladonLight = new ColorCode("ccf100");


        }
        #endregion

        #region MONO
        //attach to the vehicle on spawn
        public class VehicleHelper : MonoBehaviour
        {
            public BaseVehicle vehicle;
            //OR, if not vehicle...
            public HotAirBalloon balloon;
            //in any case...
            public BaseCombatEntity mainEntity;

            //they both share this tho
            public ulong vehicleNetID = 0;
            public VehicleType vehicleType;

            public StorageContainer fuelContainer;

            public EntityFuelSystem fuelSystem;

            public Dictionary<ulong, PowerCounter> cockpitCounters = new Dictionary<ulong, PowerCounter>();

            public static Dictionary<ulong, VehicleHelper> VehicleToHelper = new Dictionary<ulong, VehicleHelper>();
            public static Dictionary<ulong, VehicleHelper> StorageToHelper = new Dictionary<ulong, VehicleHelper>();

            //cache this for reasons
            public int lastFuelItemID = 0;

            public ulong fuelContainerNetID = 0;

            public System.Func<Item, int, bool> canAcceptItemToRestore;
            public ItemDefinition[] onlyAllowedItemsToRestore;

            //this will also call some hooks

            //hot air ballons

            //non-hot air ballons (vehicles)
            public void Prepare(BaseCombatEntity newlySpawnedVehicle)
            {

                mainEntity = newlySpawnedVehicle;

                vehicle = newlySpawnedVehicle as BaseVehicle;

                if (vehicle != null)
                {
                    //try various fuel systems
                    var maybeCar = vehicle as ModularCar;

                    if (maybeCar != null)
                    {
                        vehicleType = VehicleType.Car;
                        fuelSystem = maybeCar.engineController.FuelSystem as EntityFuelSystem;
                    }
                    else
                    {
                        var maybeCopter = vehicle as Minicopter;

                        if (maybeCopter != null)
                        {
                            vehicleType = VehicleType.Minicopter;
                            fuelSystem = maybeCopter.GetFuelSystem() as EntityFuelSystem;
                        }
                        else
                        {
                            var maybeScrapHeli = vehicle as ScrapTransportHelicopter;
                            if (maybeScrapHeli != null)
                            {
                                vehicleType = VehicleType.Scrapheli;
                                fuelSystem = maybeScrapHeli.GetFuelSystem() as EntityFuelSystem;
                            }
                            else
                            {
                                var maybeBoat = vehicle as MotorRowboat;

                                if (maybeBoat != null)
                                {
                                    var maybeRhib = vehicle as RHIB;
                                    if (maybeRhib != null)
                                    {
                                        vehicleType = VehicleType.Rhib;
                                    }
                                    else
                                    {
                                        var maybeTugboat = vehicle as Tugboat;
                                        if (maybeTugboat != null)
                                        {
                                            vehicleType = VehicleType.Tugboat;
                                        }
                                        else
                                        {
                                            vehicleType = VehicleType.Rowboat;
                                        }
                                    }

                                    fuelSystem = maybeBoat.fuelSystem;
                                }
                                else
                                {
                                    var maybeTrain = vehicle as TrainEngine;

                                    if (maybeTrain != null)
                                    {
                                        vehicleType = VehicleType.Train;
                                        fuelSystem = maybeTrain.engineController.FuelSystem as EntityFuelSystem;
                                    }
                                    else
                                    {
                                        var maybeCrane = vehicle as MagnetCrane;

                                        if (maybeCrane != null)
                                        {
                                            vehicleType = VehicleType.Crane;
                                            fuelSystem = maybeCrane.GetFuelSystem() as EntityFuelSystem;
                                        }
                                        else
                                        {
                                            var maybeSubDuo = vehicle as SubmarineDuo;

                                            if (maybeSubDuo != null)
                                            {
                                                vehicleType = VehicleType.SubDuo;
                                                fuelSystem = maybeSubDuo.GetFuelSystem() as EntityFuelSystem;
                                            }
                                            else
                                            {
                                                var maybeSubSolo = vehicle as BaseSubmarine;

                                                if (maybeSubSolo != null)
                                                {
                                                    vehicleType = VehicleType.SubSolo;
                                                    fuelSystem = maybeSubSolo.GetFuelSystem() as EntityFuelSystem;
                                                }
                                                else
                                                {
                                                    var maybeSnowmobile = vehicle as Snowmobile;

                                                    if (maybeSnowmobile != null)
                                                    {
                                                        if (maybeSnowmobile.PrefabName.Contains("tomaha"))
                                                        {
                                                            vehicleType = VehicleType.SnowmobileTomaha;
                                                        }
                                                        else
                                                        {
                                                            vehicleType = VehicleType.SnowmobileRed;
                                                        }

                                                        fuelSystem = maybeSnowmobile.GetFuelSystem() as EntityFuelSystem;
                                                    }
                                                    else
                                                    {
                                                        var maybeAttackHelicopter = vehicle as AttackHelicopter;
                                                        if (maybeAttackHelicopter != null)
                                                        {
                                                            vehicleType = VehicleType.AttackHelicopter;
                                                            fuelSystem = maybeAttackHelicopter.GetFuelSystem() as EntityFuelSystem;
                                                        }
                                                        else
                                                        {
                                                            var maybeBike = vehicle as Bike;

                                                            if (maybeBike != null)
                                                            {
                                                                fuelSystem = maybeBike.GetFuelSystem() as EntityFuelSystem;
                                                                //vehicle type based on prefab name
                                                                if (maybeBike.PrefabName.Contains("motor"))
                                                                {
                                                                    if (maybeBike.PrefabName.Contains("side"))
                                                                    {
                                                                        vehicleType = VehicleType.Motortrike;
                                                                    }
                                                                    else
                                                                    {
                                                                        vehicleType = VehicleType.Motorbike;
                                                                    }
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }

                    vehicleNetID = vehicle.net.ID.Value;
                }
                else
                {
                    balloon = newlySpawnedVehicle as HotAirBalloon;
                    fuelSystem = balloon.fuelSystem;

                    vehicleType = VehicleType.HotAirBalloon;
                    vehicleNetID = balloon.net.ID.Value;
                }

                VehicleHelper.VehicleToHelper.Add(vehicleNetID, this);
                
                Instance.timer.Once(0.1F, () =>
                {
                    PrepareFuelSystem();

                    //cars have counters spawned per-module
                    if (vehicleType != VehicleType.Car)
                    {
                        if (vehicleType == VehicleType.HotAirBalloon)
                        {
                            CheckCounterSituation(balloon);
                        }
                        else
                        {
                            CheckCounterSituation(vehicle);
                        }
                    }

                });

            }

            void OnDestroy()
            {
                VehicleHelper.VehicleToHelper.Remove(vehicleNetID);

                if (fuelContainerNetID != 0)

                    VehicleHelper.StorageToHelper.Remove(fuelContainerNetID);

                if (!Unloading)
                {
                    if (fuelContainer!=null)
                    {
                        if (fuelContainer.inventory!=null)
                        {
                            if (onlyAllowedItemsToRestore == null)
                            {
                                Instance.PrintError("ERROR: Problem restoring original fuel storage prefab's \"onlyAllowedItemToRestore\" ItemDefinition. If you see this message and your server is not unloading, that's bad.");
                            }
                            else
                            {
                                fuelContainer.inventory.onlyAllowedItems = onlyAllowedItemsToRestore;
                            }

                            if (canAcceptItemToRestore == null)
                            {
                                Instance.PrintError("ERROR: Problem restoring original fuel storage prefab's \"canAcceptItemToRestore\" Func<Item, int, bool>. If you see this message and your server is not unloading, that's bad.");
                            }
                            else
                            {
                                fuelContainer.inventory.canAcceptItem = canAcceptItemToRestore;
                            }
                        }
                    }
                }
            }

            public void PrepareFuelSystem()
            {
                if (fuelSystem == null)
                {
                    UnityEngine.Object.DestroyImmediate(this);

                    return;
                }

                fuelContainer = fuelSystem.GetFuelContainer();

                if (fuelContainer == null)
                {
                    UnityEngine.Object.DestroyImmediate(this);

                    return;
                }
                else
                {
                    fuelContainerNetID = fuelContainer.net.ID.Value;

                    StorageToHelper.Add(fuelContainer.net.ID.Value, this);

                    //if you skipped this step, RPC errors are gonna appear as soon as you unload the plugin
                    //and try to put fuel in a tank!

                    //so on plugin unload, those need to be restored.
                    canAcceptItemToRestore = fuelContainer.inventory.canAcceptItem;
                    onlyAllowedItemsToRestore = fuelContainer.inventory.onlyAllowedItems;

                    fuelContainer.inventory.canAcceptItem = CanAcceptItemIntoFuelContainer;
                    fuelContainer.inventory.onlyAllowedItems = null;
                }

                var maybeCar = vehicle as ModularCar;

                if (maybeCar != null)
                {
                    foreach (var module in maybeCar.AttachedModuleEntities)
                {
                        Instance.OnEntitySpawned(module);
                    }
                }

                UpdateCockpitCounters();
            }

            public bool CanBeUsedAsFuel(Item item)
            {
                if (item == null) return false;

                if (!Instance.configData.fuelDefinitions.ContainsKey(item.info.itemid))
                {
                    return false;
                }

                //check if that fuel is enabled at all...
                if (!Instance.configData.fuelDefinitions[item.info.itemid].globalEnabled) return false;

                //check if there's a skin requirement and if so - check if there's a match
                if (!Instance.configData.fuelDefinitions[item.info.itemid].skinMode.Equals(SkinMode.AcceptAll))
                {
                    if (Instance.configData.fuelDefinitions[item.info.itemid].skinIDs != null)
                    {
                        if (Instance.configData.fuelDefinitions[item.info.itemid].skinMode.Equals(SkinMode.Whitelist))
                        {
                            //make sure the skin id exists
                            if (!Instance.configData.fuelDefinitions[item.info.itemid].skinIDs.Contains(item.skin))
                            {
                                return false;
                            }
                        }
                        else if (Instance.configData.fuelDefinitions[item.info.itemid].skinMode.Equals(SkinMode.Blacklist))
                        {
                            //make sure the skin id doesn't exist
                            if (Instance.configData.fuelDefinitions[item.info.itemid].skinIDs.Contains(item.skin))
                            {
                                return false;
                            }
                        }
                    }
                }


                if (!Instance.configData.fuelDefinitions[item.info.itemid].specificVehicleSettings.ContainsKey(vehicleType))
                {
                    Instance.PrintError($"CAN BE USED AS FUEL RETURNS FALSE: specific vehicle settings doesn't contain an entry for {vehicleType}");
                    return false;
                }


                //and now check if you can use it with that vehicle...
                if (!Instance.configData.fuelDefinitions[item.info.itemid].specificVehicleSettings[vehicleType].specificEnabled) return false;

                return true;
            }

            //this will return current fuel usage based on what's inside the container and also vehicle type
            public float GetFuelUsageBasedOnVehicleAndItem()
            {
                var item = fuelSystem.GetFuelItem();

                if (!CanBeUsedAsFuel(item)) return 0F;

                return Instance.configData.fuelDefinitions[item.info.itemid].globalUsageMultiplier * Instance.configData.fuelDefinitions[item.info.itemid].specificVehicleSettings[vehicleType].specificUsageMultiplier;
            }


            #region HOOK RESPONSES
            public void OnVehicleModuleKill(BaseVehicleModule vehicleModule)
            {
                cockpitCounters.Remove(vehicleModule.net.ID.Value);
            }


            public void OnItemAddedToFuelContainer(Item item)
            {
                //update cached item id
                if (item.info.itemid != lastFuelItemID)
                {
                    lastFuelItemID = item.info.itemid;
                }

                UpdateCockpitCounters();
            }

            public void OnItemRemovedFromFuelContainer(Item item)
            {
                lastFuelItemID = 0;

                UpdateCockpitCounters();
            }

            public void CheckCounterSituation(BaseEntity thatSomething)
            {
                //is there supposed to be a counter, but there isn't one?
                
                bool countersEnabled = Instance.configData.globalCountersEnabled && Instance.configData.specificCountersEnabled[vehicleType];
                var maybeCounter = thatSomething.gameObject.GetComponentInChildren<PowerCounter>();
                bool countersPresent;
                bool killedCounter = false;
                if (maybeCounter != null)
                {
                    countersPresent = true;
                }
                else
                {
                    countersPresent = false;
                }
                if (countersPresent && countersEnabled)
                {
                    //do nothing, as nothing needs to be done

                }
                else
                {
                    if (!countersPresent && countersEnabled)
                    {
                        maybeCounter = AttachANewCounterToSomething(thatSomething);
                    }
                    else //must be the last case. Get rid of the counter. Don't add it to cockpit counters.
                    {
                        if (countersEnabled)
                        {
                            maybeCounter.Kill(BaseNetworkable.DestroyMode.None);
                            killedCounter = true;
                        }
                    }
                }

                if (maybeCounter != null && !killedCounter)
                {
                    MakeStable(maybeCounter, false);
                    cockpitCounters.Add(thatSomething.net.ID.Value, maybeCounter);
                }
            }

            public PowerCounter AttachANewCounterToSomething(BaseEntity thatSomething)
            {
                PowerCounter maybeCounter = GameManager.server.CreateEntity(PREFAB_COUNTER, thatSomething.transform.position, Quaternion.Euler(thatSomething.transform.eulerAngles), true) as PowerCounter;
                maybeCounter.Spawn();

                maybeCounter.SetParent(thatSomething, false, false);

                //give it owner id of the vehicle
                maybeCounter.OwnerID = vehicleNetID;

                maybeCounter.transform.localPosition = COUNTER_LOCAL_POSITION[vehicleType];
                maybeCounter.transform.localEulerAngles = COUNTER_LOCAL_ROTATION[vehicleType];
                maybeCounter.transform.hasChanged = true;

                maybeCounter.SendNetworkUpdateImmediate();

                return maybeCounter;
            }

            public void OnVehicleModuleSpawned(BaseVehicleModule vehicleModule)
            {
                switch (vehicleModule.PrefabName)
                {
                    case PREFAB_COCKPIT_ARMORED:
                    case PREFAB_COCKPIT_ENGINE:
                    case PREFAB_COCKPIT_NORMAL:
                        {
                            var moduleAsSeating = vehicleModule as VehicleModuleSeating;

                            if (moduleAsSeating != null)
                            {
                                CheckCounterSituation(moduleAsSeating);
                            }
                        }
                        break;
                }

            }

            public float GetMaxFuelAmount()
            {
                var item = fuelSystem.GetFuelItem();

                if (!CanBeUsedAsFuel(item)) return 0F;

                switch (Instance.configData.fuelDefinitions[item.info.itemid].consumptionType)
                {
                    case ConsumptionType.Condition:
                        {
                            return item.maxCondition;
                        }
                    case ConsumptionType.DataInt:
                        {
                            switch (item.info.itemid)
                            {
                                case ITEM_BATTERY_LARGE: return 1440000F;
                                case ITEM_BATTERY_MEDIUM: return 540000F;
                                case ITEM_BATTERY_SMALL: return 9000F;
                                default: return 0F;
                            }
                        }
                    case ConsumptionType.Contents:
                        {
                            switch (item.info.itemid)
                            {
                                case ITEM_WATER_JUG: return 5000F;
                                case ITEM_BOTA_BAG: return 500F;
                                case ITEM_WATER_BOTTLE: return 250F;
                                default: return 0F;
                            }
                        }
                    default:
                    case ConsumptionType.None:
                    case ConsumptionType.Item:
                        {
                            return (float)item.MaxStackable();
                        }

                }
            }

            public int GetUnitINT(Item item)
            {
                if (!CanBeUsedAsFuel(item)) return 0;
                return Mathf.FloorToInt(GetUnitFLOAT(item));
            }

            public float GetUnitFLOAT(Item item)
            {
                return Instance.configData.fuelDefinitions[item.info.itemid].globalConsumptionAmount * Instance.configData.fuelDefinitions[item.info.itemid].specificVehicleSettings[vehicleType].specificConsumptionAmount;
            }

            public object OnGetFuelAmount()
            {
                if (vehicle != null)
                {
                    if (vehicle.skinID == 2847372289)
                    {
                        return 1337;
                    }
                }

                int result = 0;

                if (fuelSystem == null)
                {
                    return 0;
                }

                if (fuelContainer == null)
                {
                    return 0;
                }

                var item = fuelSystem.GetFuelItem();
                if (!CanBeUsedAsFuel(item))
                {
                    return 0;
                }
                else
                {
                    var wholeUnit = GetUnitINT(item);

                    switch (Instance.configData.fuelDefinitions[item.info.itemid].consumptionType)
                    {
                        case ConsumptionType.None:
                            {
                                result = item.MaxStackable();
                            }
                            break;
                        case ConsumptionType.Condition:
                            {
                                var floatUnit = GetUnitFLOAT(item);

                                result = item.condition >= floatUnit ? Mathf.FloorToInt(item.condition/floatUnit) : 0;
                            }
                            break;
                        case ConsumptionType.DataInt:
                            {

                                if (item.instanceData == null)
                                {
                                    item.instanceData = new ProtoBuf.Item.InstanceData();
                                    item.instanceData.ShouldPool = false;

                                    switch (item.info.itemid)
                                    {
                                        case ITEM_BATTERY_LARGE:
                                            {
                                                item.instanceData.dataInt = 200 * 60;
                                            }
                                            break;
                                        case ITEM_BATTERY_MEDIUM:
                                            {
                                                item.instanceData.dataInt = 100 * 60;
                                            }
                                            break;
                                        case ITEM_BATTERY_SMALL:
                                            {
                                                item.instanceData.dataInt = 20 * 60;
                                            }
                                            break;
                                    }
                                }

                                result = item.instanceData.dataInt >= wholeUnit ? item.instanceData.dataInt : 0; //todo: default based on item id, sometimes items come with null instance data

                            }
                            break;
                        case ConsumptionType.Contents:
                            {
                                if (item.contents != null)
                                {
                                    var itemInside = item.contents.GetSlot(0);
                                    if (itemInside != null)
                                    {
                                        result = itemInside.amount >= wholeUnit ? itemInside.amount : 0;
                                    }
                                    else
                                    {
                                        result = 0;
                                    }
                                }
                                else
                                {
                                    result = 0;
                                }
                            }
                            break;
                        default:
                        case ConsumptionType.Item:
                            {
                                result = item.amount >= wholeUnit ? item.amount : 0;
                            }
                            break;
                    }
                }
                return result;
            }

            public object HasFuel(bool forceCheck = false)
            {
                if (vehicle != null)
                {
                    if (vehicle.skinID == 2847372289) return true;
                }

                return (Instance.configData.globalNoNeedForFuel || Instance.configData.specificNoNeedForFuel[vehicleType]) ? (object)true : null;
            }

            public object TryUseFuel(float seconds, float fuelUsedPerSecond)
            {
                if (fuelSystem == null) return null;

                if ((Object)fuelContainer == (Object)null)
                    return 0;
                Item slot = fuelContainer.inventory.GetSlot(0);
                if (slot == null || slot.amount < 1)
                    return 0;
                fuelSystem.pendingFuel += seconds * fuelUsedPerSecond * GetFuelUsageBasedOnVehicleAndItem();
                if ((double)fuelSystem.pendingFuel >= 1.0)
                {
                    int amountToConsume = Mathf.FloorToInt(fuelSystem.pendingFuel);

                    //this will be invoked afterwards
                    UpdateCockpitCounters();

                    if (!CanBeUsedAsFuel(slot)) return 0;

                    //first, check if you should even consume that
                    var wholeUnit = GetUnitINT(slot);

                    //if fuel is not needed globally/for the vehicle, don't consume anything!

                    if (Instance.configData.globalNoNeedForFuel || Instance.configData.specificNoNeedForFuel[vehicleType] || vehicle?.skinID == 2847372289)
                    {
                        //do not consume anything
                        fuelSystem.pendingFuel -= (float)amountToConsume;
                        return 0;
                    }
                    else
                    {
                        switch (Instance.configData.fuelDefinitions[slot.info.itemid].consumptionType)
                        {
                            case ConsumptionType.Condition:
                                {
                                    if (slot.hasCondition)
                                    {
                                        var floatUnit = GetUnitFLOAT(slot);

                                        if (slot.condition - floatUnit >= 0)
                                        {
                                            slot.LoseCondition(floatUnit);
                                        }

                                        if (slot.condition >= 0F)
                                        {
                                            fuelSystem.pendingFuel -= (float)amountToConsume;
                                        }

                                        return 0;
                                    }
                                    else
                                    {
                                        return 0;
                                    }
                                }

                            case ConsumptionType.None:
                                {
                                    //don't use up the item
                                    fuelSystem.pendingFuel -= (float)amountToConsume;
                                    return 0;
                                }
                            case ConsumptionType.DataInt:
                                {
                                    if (slot.instanceData.dataInt - wholeUnit >= 0)
                                    {
                                        slot.instanceData.dataInt -= wholeUnit;
                                        slot.MarkDirty();
                                        fuelSystem.pendingFuel -= (float)amountToConsume;
                                        return 0;
                                    }
                                    else
                                    {
                                        return 0;
                                    }
                                }
                            case ConsumptionType.Contents:
                                {
                                    if (slot.contents == null)
                                    {
                                        return 0;
                                    }
                                    else
                                    {
                                        var itemInside = slot.contents.GetSlot(0);
                                        if (itemInside == null)
                                        {
                                            return 0;
                                        }
                                        else
                                        {
                                            if (itemInside.amount - wholeUnit >= 0)
                                            {
                                                itemInside.amount -= wholeUnit;
                                                itemInside.MarkDirty();
                                            }
                                            else
                                            {
                                                return 0;
                                            }

                                        }
                                    }
                                }
                                break;
                            case ConsumptionType.Item:
                                {
                                    if (slot.amount >= wholeUnit)
                                    {
                                        slot.UseItem(wholeUnit);
                                        fuelSystem.pendingFuel -= (float)amountToConsume;
                                    }
                                    else
                                    {
                                        return 1;
                                    }

                                    return 0;
                                }
                        }
                    }


                }
                return 0;
            }

            #endregion

            public void UpdateCockpitCounters()
            {
                if (cockpitCounters == null) return;
                if (!cockpitCounters.Any()) return;

                mainEntity.Invoke(() =>
                {
                    var on = true;

                    var max = GetMaxFuelAmount();

                    int amountToDisplay = max == 0 ? 0 : Mathf.FloorToInt(Mathf.Clamp01((int)OnGetFuelAmount()/max) *100F);

                    foreach (var counter in cockpitCounters)
                    {
                        counter.Value.UpdateHasPower(on ? 1 : 0, 0);// ? charge : 0, 0);

                        if (on)
                        {
                            counter.Value.targetCounterNumber = 1;
                            counter.Value.SetCounterNumber(amountToDisplay);
                        }

                        counter.Value.SetFlag(BaseEntity.Flags.On, on, false, true);
                        counter.Value.SendNetworkUpdate(BasePlayer.NetworkQueue.Update);
                    }
                }, 0.1F);


            }

            public bool CanAcceptItemIntoFuelContainer(Item item, int slot)
            {
                return CanBeUsedAsFuel(item);
            }

            public List<BasePlayer> GetPassengers()
            {
                List<BasePlayer> passengers = new List<BasePlayer>();

                if (vehicle != null)
                {
                    for (int index = 0; index < vehicle.mountPoints.Count; ++index)
                    {
                        BaseVehicle.MountPointInfo mountPoint = vehicle.mountPoints[index];
                        if (mountPoint.mountable != null && mountPoint.mountable.GetMounted() != null)
                        {
                            passengers.Add(mountPoint.mountable.GetMounted());
                        }
                    }

                }
                else
                {
                    if (balloon != null)
                    {
                        foreach (var child in balloon.children.Where(ch => ch is BasePlayer))
                        {
                            passengers.Add(child as BasePlayer);
                        }
                    }
                }

                return passengers;
            }
        }

        #endregion

        #region DEBUG & BUMFUCKERY

        #endregion

        #region CHAT

        #endregion

        #region NEWLY ADDED FUEL HOOKS
        private static BaseEntity _checkedEntity1;
        private static BaseEntity _checkedEntity2;
        private static BaseEntity _checkedEntity3;

        object OnFuelCheck(EntityFuelSystem fuelSystem)
        {
            if (Instance == null) return null;
            _checkedEntity1 = fuelSystem.GetFuelContainer();

            if (_checkedEntity1 == null)
            {
                return null;
            }

            if (_checkedEntity1.net == null) return null;

            if (VehicleHelper.VehicleToHelper.ContainsKey(_checkedEntity1.net.ID.Value))
            {
                return VehicleHelper.VehicleToHelper[_checkedEntity1.net.ID.Value].HasFuel();
            }
            return null;
        }

        object CanUseFuel(EntityFuelSystem fuelSystem, StorageContainer container, float seconds, float fuelUsedPerSecond)
        {
            if (Instance == null) return null;
            _checkedEntity2 = container.GetParentEntity();

            if (_checkedEntity2 == null)
            {
                return null;
            }

            if (_checkedEntity2.net == null) return null;

            if (VehicleHelper.VehicleToHelper.ContainsKey(_checkedEntity2.net.ID.Value))
            {
                return VehicleHelper.VehicleToHelper[_checkedEntity2.net.ID.Value].TryUseFuel(seconds, fuelUsedPerSecond);
            }
            return null;
        }

        object OnFuelAmountCheck(EntityFuelSystem fuelSystem, Item item)
        {
            if (Instance == null) return null;
            _checkedEntity3 = fuelSystem.GetFuelContainer()?.parentEntity.Get(true);

            if (_checkedEntity3 == null)
            {
                return null;
            }
            if (_checkedEntity3.net == null) return null;

            if (VehicleHelper.VehicleToHelper.ContainsKey(_checkedEntity3.net.ID.Value))
            {
                return VehicleHelper.VehicleToHelper[_checkedEntity3.net.ID.Value].OnGetFuelAmount();
            }
            return null;
        }
        #endregion

        #region API HOOKS
        void Init()
        {
            Instance = null;

            Unloading = false;

            lang.RegisterMessages(LangMessages, this);
        }

        void Unload()
        {
            SaveConfigData();

            Unloading = true;

            foreach (var carCompo in UnityEngine.Object.FindObjectsOfType<VehicleHelper>())
            {
                UnityEngine.Object.DestroyImmediate(carCompo);
            }

            foreach (var player in BasePlayer.activePlayerList)
            {
                GuiManagerPlayer.HideGUIFuelStorage(player);
            }

            Unloading = false;

            VehicleHelper.VehicleToHelper = null;
            VehicleHelper.StorageToHelper = null;

            GuiManagerAdmin.Cleanup();

            Instance = null;

            Interface.Call("FuelManagerCheck");
        }

        void OnServerInitialized(bool serverInitialized)
        {
            VehicleHelper.VehicleToHelper = new Dictionary<ulong, VehicleHelper>();
            VehicleHelper.StorageToHelper = new Dictionary<ulong, VehicleHelper>();


            permission.RegisterPermission(PERMISSION_VIP_1, this);
            permission.RegisterPermission(PERMISSION_VIP_2, this);
            permission.RegisterPermission(PERMISSION_VIP_3, this);
            permission.RegisterPermission(PERMISSION_VIP_4, this);
            permission.RegisterPermission(PERMISSION_VIP_5, this);
            
            permission.RegisterPermission(PERMISSION_ADMIN, this);

            Instance = this;

            LoadConfigData();
            ProcessConfigData();

            foreach (BaseVehicle vehicle in BaseNetworkable.serverEntities.OfType<BaseVehicle>())
            {
                OnEntitySpawned(vehicle);
            }

            foreach (var balloon in BaseNetworkable.serverEntities.OfType<HotAirBalloon>())
            {
                OnEntitySpawned(balloon);
            }

            GuiManagerAdmin.Prepare();

            DoneLoadingImagesSoGenerateGUINow();

            Interface.Call("FuelManagerCheck");
            /*
            NextTick(() =>
            {
                if (ImageLibrary)
                {
                    AddItemImages();
                }
                else
                {
                    PrintError("WARNING: ImageLibrary not loaded. You will still see the item icons, but every time you open a fuel tank, it will take a few seconds for them to load (instead of it happening only the first time). It's recommended you load in ImageLibrary and reload FuelManager.");
                    DoneLoadingImagesSoGenerateGUINow();
                }
            });*/
        }
        void OnLootEntity(BasePlayer player, StorageContainer container)
        {
            if (Instance == null) return;

            if (container == null) return;

            if (VehicleHelper.StorageToHelper.ContainsKey(container.net.ID.Value))
            {
                if (Instance.configData.globalEnableGUI && Instance.configData.specificEnableGUI[VehicleHelper.StorageToHelper[container.net.ID.Value].vehicleType])
                {
                    GuiManagerPlayer.ShowGUIFuelStorage(player, VehicleHelper.StorageToHelper[container.net.ID.Value].vehicleType);
                }
            }
        }

        void OnLootEntityEnd(BasePlayer player, StorageContainer container)
        {
            if (Instance == null) return;

            if (container == null) return;

            if (VehicleHelper.StorageToHelper.ContainsKey(container.net.ID.Value))
            {
                GuiManagerPlayer.HideGUIFuelStorage(player);
            }
        }

        void OnItemAddedToContainer(ItemContainer container, Item item)
        {
            if (Instance == null) return;

            if (container == null || item == null) return;
            if (container.entityOwner == null) return;
            if (container.entityOwner.net == null) return;

            if (VehicleHelper.StorageToHelper.ContainsKey(container.entityOwner.net.ID.Value))
            {
                VehicleHelper.StorageToHelper[container.entityOwner.net.ID.Value].OnItemAddedToFuelContainer(item);
            }
        }

        void OnItemRemovedFromContainer(ItemContainer container, Item item)
        {
            if (Instance == null) return;

            if (container == null || item == null) return;
            if (container.entityOwner == null) return;
            if (container.entityOwner.net == null) return;

            if (VehicleHelper.StorageToHelper.ContainsKey(container.entityOwner.net.ID.Value))
            {
                VehicleHelper.StorageToHelper[container.entityOwner.net.ID.Value].OnItemRemovedFromFuelContainer(item);
            }
        }

        object OnEngineStart(BaseVehicle vehicle, BasePlayer player)
        {
            if (Instance == null) return null;

            if (vehicle == null) return null;
            if (vehicle.net == null) return null;

            if (VehicleHelper.VehicleToHelper.ContainsKey(vehicle.net.ID.Value))
            {
                //is fuel even needed for this?

                int itemID = -1;

                if (Instance.configData.globalNoNeedForFuel || Instance.configData.specificNoNeedForFuel[VehicleHelper.VehicleToHelper[vehicle.net.ID.Value].vehicleType])
                {
                    itemID = 0;
                }
                else
                {
                    var item = VehicleHelper.VehicleToHelper[vehicle.net.ID.Value].fuelSystem.GetFuelItem();

                    if (item != null)
                    {
                        itemID = item.info.itemid;
                    }
                }

                if (itemID != -1)
                {
                    byte reason;

                    if (CanPlayerDriveVehicleWithFuel(player, VehicleHelper.VehicleToHelper[vehicle.net.ID.Value].vehicleType, itemID, out reason))
                    {
                        VehicleHelper.VehicleToHelper[vehicle.net.ID.Value].UpdateCockpitCounters();
                        return null;
                    }
                    else
                    {
                        player.ChatMessage($"{ChatPrefix}{MSG(MSG_CANT_USE_THIS_FUEL, player.UserIDString, $"{VEH(VehicleHelper.VehicleToHelper[vehicle.net.ID.Value].vehicleType, player.UserIDString)}", $"{ItemManager.FindItemDefinition(itemID).displayName.translated}")} {REASON(reason, player.UserIDString)}");
                        return false;
                    }
                }
            }

            return null;
        }
        void OnEntitySpawned(HotAirBalloon balloon)
        {
            NextTick(() =>
            {
                if (Instance == null) return;
                if (balloon == null) return;

                if (balloon.gameObject.GetComponent<VehicleHelper>() == null)
                {
                    balloon.gameObject.AddComponent<VehicleHelper>().Prepare(balloon);
                }
            });
        }
        
        void OnEntitySpawned(BaseVehicle vehicle)
        {
            NextTick(() =>
            {
                if (Instance == null) return;
                if (vehicle == null) return;

                if (vehicle.PrefabName.Contains("pedal"))
                {
                    return;
                }

                if (vehicle.gameObject.GetComponent<VehicleHelper>() == null)
                {
                    vehicle.gameObject.AddComponent<VehicleHelper>().Prepare(vehicle);
                }
            });
        }

        void OnEntitySpawned(BaseVehicleModule vehicleModule)
        {
            //make sure you delay it, otherwise Vehicle.net will be null
            NextTick(() =>
            {
                if (Instance == null) return;
                if (vehicleModule == null) return;
                if (vehicleModule.Vehicle == null) return;
                if (vehicleModule.Vehicle.net == null) return;
                if (VehicleHelper.VehicleToHelper.ContainsKey(vehicleModule.Vehicle.net.ID.Value))
                {
                    VehicleHelper.VehicleToHelper[vehicleModule.Vehicle.net.ID.Value].OnVehicleModuleSpawned(vehicleModule);
                }
            });
        }

        void OnEntityKill(BaseVehicleModule vehicleModule)
        {
            if (Instance == null) return;
            if (vehicleModule == null) return;
            if (vehicleModule.Vehicle == null) return;
            if (vehicleModule.Vehicle.net == null) return;

            if (VehicleHelper.VehicleToHelper.ContainsKey(vehicleModule.Vehicle.net.ID.Value))
            {
                VehicleHelper.VehicleToHelper[vehicleModule.Vehicle.net.ID.Value].OnVehicleModuleKill(vehicleModule);
            }
        }

        object OnCounterModeToggle(PowerCounter counter, BasePlayer player, bool wants)
        {
            if (Instance == null) return null;

            if (VehicleHelper.VehicleToHelper.ContainsKey((uint)counter.OwnerID))
            {
                return false;
            }
            return null;

        }
        #endregion

        #region IMAGE LIBRARY
        /*
        public void AddItemImages()
        {
            var loadOrder = new Dictionary<string, string>();

            foreach (var fuelDef in configData.fuelDefinitions)
            {
                var itemDef = ItemManager.FindItemDefinition(fuelDef.Key);

                if (itemDef == null) continue;

                //any particular skins we should be looking out for?

                if (!loadOrder.ContainsKey(itemDef.shortname))
                {
                    PrintWarning($"Adding {itemDef.displayName.translated} icon to load order...");
                    loadOrder.Add(itemDef.shortname, MakeURL(itemDef.shortname));
                }
            }

            PrintWarning($"Importing {loadOrder.Count} item icons into Image Library...");
            //pass the load order into ImageLibrary
            ImageLibrary.Call("ImportImageList", Title, loadOrder, (ulong)1337, false, new Action(() => { DoneLoadingImagesSoGenerateGUINow(); }));//, new Action(DoneLoadingImagesSoGenerateGUINow));//, 1337, false, new Action(DoneLoadingImagesSoGenerateGUINow));
            //ImportImageList(string title, Dictionary<string, string> imageList, ulong imageId = 0, bool replace = false, Action callback = null)
        }
        */

        public void DoneLoadingImagesSoGenerateGUINow()
        {
            PrintWarning("Done importing icons. Preparing GUI...");

            GuiManagerPlayer.GenerateContentAnchors();
            GuiManagerPlayer.GenerateAllVehicleGUIs();
        }
        /*
        public string GetIconImage(string itemShortname, ulong skinID = 0)
        {
            if (ImageLibrary == null)
            {
                var makeUrl = MakeURL(itemShortname);
                PrintWarning($"ImageLibrary not loaded, falling back on URL: {makeUrl}");
                return makeUrl;
            }
            else
            {
                string img = ImageLibrary.Call("GetImage", itemShortname, skinID).ToString();
                if (string.IsNullOrEmpty(img))
                {
                    return String.Empty;
                }
                else
                {
                    return img;
                }
            }
        }
        */
        public static string MakeURL(string itemShortname)
        {
            return $"https://www.rustedit.io/images/imagelibrary/{itemShortname}.png";
        }
        #endregion

        #region HELPERS
        public static Dictionary<VehicleType, Dictionary<int, float>> GetAvailableFuelTypes()
        {
            var result = new Dictionary<VehicleType, Dictionary<int, float>>();

            foreach (var fuelDef in Instance.configData.fuelDefinitions)
            {
                if (fuelDef.Value.globalEnabled)
                {
                    foreach (var vehicleDef in fuelDef.Value.specificVehicleSettings)
                    {
                        if (!result.ContainsKey(vehicleDef.Key))
                        {
                            result.Add(vehicleDef.Key, new Dictionary<int, float>());
                        }

                        if (vehicleDef.Value.specificEnabled)
                        {
                            result[vehicleDef.Key].Add(fuelDef.Key, fuelDef.Value.globalUsageMultiplier * vehicleDef.Value.specificUsageMultiplier);
                        }
                    }
                }
            }

            return result;
        }

        public const byte REASON_OK = 0;

        public const byte REASON_FUEL_NOT_DEFINED = 1;
        public const byte REASON_FUEL_DISABLED = 2;
        public const byte REASON_FUEL_VEHICLE_DISABLED = 3;

        public const byte REASON_NO_PERMISSION_FUEL = 4;
        public const byte REASON_NO_PERMISSION_VEHICLE = 5;
        public const byte REASON_NO_PERMISSION_FUEL_VEHICLE = 6;

        public static bool CanPlayerDriveVehicleWithFuel(BasePlayer player, VehicleType vehicleType, int fuelItemID, out byte reason)
        {
            reason = REASON_OK;

            bool fuelNeeded = true;

            if (fuelItemID == 0 || Instance.configData.globalNoNeedForFuel) //means we assume the item is there and it's valid
            {
                fuelNeeded = false;
            }

            if (Instance.configData.specificNoNeedForFuel[vehicleType] == true)
            {
                fuelNeeded = false;
            }

            if (fuelNeeded)
            {

                bool hasFuelPermission = true;
                bool hasFuelVehiclePermission = true;

                bool finalHasPermission;

                if (!Instance.configData.fuelDefinitions.ContainsKey(fuelItemID))
                {
                    reason = REASON_FUEL_NOT_DEFINED;
                    return false;
                }

                if (!Instance.configData.fuelDefinitions[fuelItemID].globalEnabled)
                {
                    reason = REASON_FUEL_DISABLED;
                    return false;
                }

                if (!Instance.configData.fuelDefinitions[fuelItemID].specificVehicleSettings[vehicleType].specificEnabled)
                {
                    reason = REASON_FUEL_VEHICLE_DISABLED;
                    return false;
                }

                bool fuelPermissionNeeded = Instance.configData.fuelDefinitions[fuelItemID].globalPermissionNeeded != null;
                bool fuelVehiclePermissionNeeded = Instance.configData.fuelDefinitions[fuelItemID].specificVehicleSettings[vehicleType].specificPermissionNeeded != null;

                if (fuelPermissionNeeded)
                {
                    hasFuelPermission = HasPermission(player, Instance.configData.fuelDefinitions[fuelItemID].globalPermissionNeeded);
                }

                if (fuelVehiclePermissionNeeded)
                {
                    hasFuelVehiclePermission = HasPermission(player, Instance.configData.fuelDefinitions[fuelItemID].specificVehicleSettings[vehicleType].specificPermissionNeeded);
                }

                finalHasPermission = hasFuelPermission && hasFuelVehiclePermission;

                if (finalHasPermission)
                {
                    reason = REASON_OK;
                    return true;
                }
                else
                {
                    if (!hasFuelPermission)
                    {
                        if (!hasFuelVehiclePermission)
                        {
                            reason = REASON_NO_PERMISSION_FUEL_VEHICLE;
                            return false;
                        }
                        else
                        {
                            reason = REASON_NO_PERMISSION_FUEL;
                            return false;
                        }
                    }
                    else
                    {
                        reason = REASON_NO_PERMISSION_VEHICLE;
                        return false;
                    }
                }
            }
            else
            {
                return true;
            }
        }

        public static void PlayEffect(string effect, Vector3 position)
        {
            EffectNetwork.Send(new Effect(effect, position, Vector3.up));
        }

        public static void MakeStable(BaseCombatEntity entity, bool pickupEnabled = true)
        {
            var groundWatch = entity.gameObject.GetComponent<GroundWatch>();

            if (groundWatch != null)
            {
                UnityEngine.Object.Destroy(groundWatch);
            }

            entity.pickup.enabled = pickupEnabled;

            var destroyOnGroundMissing = entity.gameObject.GetComponent<DestroyOnGroundMissing>();

            if (destroyOnGroundMissing != null)
            {
                UnityEngine.Object.Destroy(destroyOnGroundMissing);
            }
            entity.enableSaving = false;

            foreach (var collider in entity.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.Destroy(collider);
            }

        }
        #endregion

        #region GUI_PLAYER
        public const float GUI_LEFT = 0.6510416F;
        public const float GUI_BOTTOM = 0.5F;
        public const float GUI_RIGHT = 0.946875F;
        public const float GUI_TOP = 0.9333334F;

        public const float GUI_TITLE_HEIGHT = 0.0296296F;
        public const float GUI_DESC_HEIGHT = 0.055555F;

        public const float GUI_ITEM_WIDTH = 0.046875F;
        public const float GUI_ITEM_HEIGHT = 0.0833333F;

        public const float GUI_GAP_WIDTH = 0.003125F;
        public const float GUI_GAP_HEIGHT = 0.0055555F;

        public const float GUI_PAD_MIN_X = 0.0036458333F;
        public const float GUI_PAD_MIN_Y = 0.01111111F;

        public const float GUI_PAD_MAX_X = 0.004166666F;
        public const float GUI_PAD_MAX_Y = 0.002777777F;

        public const float GUI_PAD_MAX_X_AMOUNT = 0.0026041666F;
        public const float GUI_PAD_MIN_Y_AMOUNT = 0.0055555555F;

        public const float GUI_FADE = 0.125F;
        public const int GUI_FONT_SIZE_TITLE = 16;
        public const int GUI_FONT_SIZE_DESCRIPTION = 12;


        public static readonly CuiRectTransformComponent AnchorTitle = new CuiRectTransformComponent
        {
            AnchorMin = $"{GUI_LEFT} {GUI_TOP - GUI_TITLE_HEIGHT}",
            AnchorMax = $"{GUI_RIGHT} {GUI_TOP}"
        };

        public static readonly CuiRectTransformComponent AnchorDesc = new CuiRectTransformComponent
        {
            AnchorMin = $"{GUI_LEFT} {GUI_TOP - GUI_TITLE_HEIGHT - GUI_DESC_HEIGHT}",
            AnchorMax = $"{GUI_RIGHT} {GUI_TOP - GUI_TITLE_HEIGHT}"
        };

        public static readonly CuiRectTransformComponent AnchorContent = new CuiRectTransformComponent
        {
            AnchorMin = $"{GUI_LEFT} {GUI_BOTTOM}",
            AnchorMax = $"{GUI_RIGHT} {GUI_TOP - GUI_TITLE_HEIGHT - GUI_DESC_HEIGHT}"
        };

        public class GuiFuelStorage
        {
            public CuiElementContainer containerMain = new CuiElementContainer();

            public VehicleType type;

            public bool noFuelRequiredForVehicle = false;

            public Dictionary<int, float> fuelItems = new Dictionary<int, float>();

            public CuiTextComponent elementDescription;

            public GuiFuelStorage(VehicleType type)
            {
                this.type = type;
            }

            public void ShowGUI(BasePlayer player)
            {
                elementDescription.Text = GetDescription(player);
                CuiHelper.AddUi(player, containerMain);
            }

            public string GetDescription(BasePlayer player)
            {
                string description;

                var userId = player == null ? null : player.UserIDString;

                if (Instance.configData.globalNoNeedForFuel == true)
                {
                    description = MSG(MSG_VEHICLES_DONT_NEED_FUEL, userId);
                }
                else if (Instance.configData.specificNoNeedForFuel[type])
                {
                    description = MSG(MSG_THESE_VEHICLES_DONT_NEED_FUEL, userId, VEH(type, userId, true));
                }
                else
                {
                    description = MSG(MSG_AVAILABLE_FUELS, userId, VEH(type, userId, true));
                }

                return description;
            }

            public string GetDescription(BasePlayer player, out bool doFuel)
            {
                string description;

                var userId = player == null ? null : player.UserIDString;

                if (Instance.configData.globalNoNeedForFuel == true)
                {
                    description = MSG(MSG_VEHICLES_DONT_NEED_FUEL, userId);
                    doFuel = false;
                }
                else if (this.noFuelRequiredForVehicle)
                {
                    description = MSG(MSG_THESE_VEHICLES_DONT_NEED_FUEL, userId, VEH(type, userId, true));
                    doFuel = false;
                }
                else
                {
                    description = MSG(MSG_AVAILABLE_FUELS, userId, VEH(type, userId, true));
                    doFuel = true;
                }

                return description;
            }

            public void RegeneratePlayerGUI()
            {
                //we're assuming the fuelItems dictionary has been populated already

                //if there's no fuel required, EZ-PZ.
                string description;

                bool doFuel;

                containerMain = new CuiElementContainer();

                description = GetDescription(null, out doFuel);

                containerMain.Add(new CuiElement
                {
                    Name = "fmgui.title.text",
                    FadeOut = GUI_FADE,
                    Parent = "Overlay",
                    Components =
                    {
                       new CuiTextComponent
                       {
                           Align = TextAnchor.MiddleLeft,
                           Color = ColorPalette.DirtyPink.rustString,
                           FadeIn = GUI_FADE,
                           FontSize = GUI_FONT_SIZE_TITLE,
                           Text = $"FUEL MANAGER {VERSION}"
                       },
                       AnchorTitle
                    }
                });

                containerMain.Add(new CuiElement
                {
                    Name = "fmgui.desc",
                    FadeOut = GUI_FADE,
                    Parent = "Overlay",
                    Components =
                    {
                       new CuiTextComponent
                       {
                           Align = TextAnchor.MiddleCenter,
                           Color = ColorPalette.DirtyPink.rustString,
                           FadeIn = GUI_FADE,
                           FontSize = GUI_FONT_SIZE_DESCRIPTION,
                           Text = description
                       },
                       AnchorDesc
                    }
                });

                elementDescription = containerMain[1].Components[0] as CuiTextComponent;

                if (doFuel)
                {
                    //we got the content anchors. so let's dump shit.
                    var index = 0;
                    foreach (var fuel in fuelItems)
                    {
                        containerMain.Add(new CuiElement
                        {
                            Name = $"fmgui.content.{index}.bg",
                            FadeOut = GUI_FADE,
                            Parent = "Overlay",
                            Components =
                            {
                               new CuiImageComponent
                               {
                                   Color = ColorPalette.GreyDark.rustString+" 0.8",
                                   FadeIn = GUI_FADE
                               },
                               GuiManagerPlayer.ContentAnchorsFull[index]
                            }
                        });

                        var itemDef = ItemManager.FindItemDefinition(fuel.Key);

                        containerMain.Add(new CuiElement
                        {
                            Name = $"fmgui.content.{index}.img",
                            FadeOut = GUI_FADE,
                            Parent = "Overlay",
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = $"https://www.rustedit.io/images/imagelibrary/{itemDef.shortname}.png",
                                    //Png = Instance.GetIconImage(itemDef.shortname),//$"https://www.rustedit.io/images/imagelibrary/{itemDef.shortname}.png",
                                    FadeIn = GUI_FADE,
                                },
                               GuiManagerPlayer.ContentAnchorsImage[index]
                            }
                        });

                        containerMain.Add(new CuiElement
                        {
                            Name = $"fmgui.content.{index}.amount",
                            FadeOut = GUI_FADE,
                            Parent = "Overlay",
                            Components =
                            {
                               new CuiTextComponent
                               {
                                   Align = TextAnchor.LowerRight,
                                   Color = ColorPalette.GreyLight.rustString,
                                   FadeIn = GUI_FADE,
                                   FontSize = 14,
                                   Text = $"x{fuel.Value}",
                               },
                               GuiManagerPlayer.ContentAnchorsAmount[index]
                            }
                        });

                        index ++;
                    }
                }
            }
        }

        public static class GuiManagerPlayer
        {
            public static Dictionary<VehicleType, GuiFuelStorage> VehicleGUIs;

            public static Dictionary<int, CuiRectTransformComponent> ContentAnchorsFull;
            public static Dictionary<int, CuiRectTransformComponent> ContentAnchorsImage;
            public static Dictionary<int, CuiRectTransformComponent> ContentAnchorsAmount;

            public const int GUI_COLS = 6;
            public const int GUI_ROWS = 4;

            //no need to run this more than once
            public static void GenerateContentAnchors()
            {
                ContentAnchorsFull = new Dictionary<int, CuiRectTransformComponent>();
                ContentAnchorsImage = new Dictionary<int, CuiRectTransformComponent>();
                ContentAnchorsAmount = new Dictionary<int, CuiRectTransformComponent>();

                var currentRow = 0;
                var currentCol = 0;

                var index = 0;
                //we're going in the opposite direction
                for (currentRow = GUI_ROWS-1; currentRow>=0; currentRow--)
                {
                    for (currentCol = 0; currentCol<GUI_COLS; currentCol++)
                    {
                        ContentAnchorsFull.Add(index, new CuiRectTransformComponent
                        {
                            AnchorMin = $"{GUI_LEFT + ((GUI_ITEM_WIDTH + GUI_GAP_WIDTH) * currentCol)} {GUI_BOTTOM + ((GUI_ITEM_HEIGHT + GUI_GAP_HEIGHT) * currentRow)}",
                            AnchorMax = $"{GUI_LEFT + ((GUI_ITEM_WIDTH + GUI_GAP_WIDTH) * currentCol) + GUI_ITEM_WIDTH} {GUI_BOTTOM + ((GUI_ITEM_HEIGHT + GUI_GAP_HEIGHT) * currentRow) + GUI_ITEM_HEIGHT}",
                        });

                        ContentAnchorsImage.Add(index, new CuiRectTransformComponent
                        {
                            AnchorMin = $"{GUI_LEFT + ((GUI_ITEM_WIDTH + GUI_GAP_WIDTH) * currentCol) + GUI_PAD_MIN_X} {GUI_BOTTOM + ((GUI_ITEM_HEIGHT + GUI_GAP_HEIGHT) * currentRow) + GUI_PAD_MIN_Y}",
                            AnchorMax = $"{GUI_LEFT + ((GUI_ITEM_WIDTH + GUI_GAP_WIDTH) * currentCol) + GUI_ITEM_WIDTH - GUI_PAD_MAX_X} {GUI_BOTTOM + ((GUI_ITEM_HEIGHT + GUI_GAP_HEIGHT) * currentRow) + GUI_ITEM_HEIGHT - GUI_PAD_MAX_Y}",
                        });

                        ContentAnchorsAmount.Add(index, new CuiRectTransformComponent
                        {
                            AnchorMin = $"{GUI_LEFT + ((GUI_ITEM_WIDTH + GUI_GAP_WIDTH) * currentCol) + GUI_PAD_MIN_X} {GUI_BOTTOM + ((GUI_ITEM_HEIGHT + GUI_GAP_HEIGHT) * currentRow) + GUI_PAD_MIN_Y_AMOUNT/3F}",
                            AnchorMax = $"{GUI_LEFT + ((GUI_ITEM_WIDTH + GUI_GAP_WIDTH) * currentCol) + GUI_ITEM_WIDTH - GUI_PAD_MAX_X_AMOUNT} {GUI_BOTTOM + ((GUI_ITEM_HEIGHT + GUI_GAP_HEIGHT) * currentRow) + GUI_ITEM_HEIGHT - GUI_PAD_MAX_Y}",
                        });

                        index++;
                    }
                }
            }

            public static void GenerateAllVehicleGUIs()
            {
                //pre-create...

                VehicleGUIs = new Dictionary<VehicleType, GuiFuelStorage>
                {
                    [VehicleType.Car] = new GuiFuelStorage(VehicleType.Car),
                    [VehicleType.Minicopter] = new GuiFuelStorage(VehicleType.Minicopter),
                    [VehicleType.Rhib] = new GuiFuelStorage(VehicleType.Rhib),
                    [VehicleType.Rowboat] = new GuiFuelStorage(VehicleType.Rowboat),
                    [VehicleType.Scrapheli] = new GuiFuelStorage(VehicleType.Scrapheli),
                    [VehicleType.HotAirBalloon] = new GuiFuelStorage(VehicleType.HotAirBalloon),
                    [VehicleType.Train] = new GuiFuelStorage(VehicleType.Train),
                    [VehicleType.Crane] = new GuiFuelStorage(VehicleType.Crane),
                    [VehicleType.SubSolo] = new GuiFuelStorage(VehicleType.SubSolo),
                    [VehicleType.SubDuo] = new GuiFuelStorage(VehicleType.SubDuo),
                    [VehicleType.SnowmobileRed] = new GuiFuelStorage(VehicleType.SnowmobileRed),
                    [VehicleType.SnowmobileTomaha] = new GuiFuelStorage(VehicleType.SnowmobileTomaha),
                    [VehicleType.Tugboat] = new GuiFuelStorage(VehicleType.Tugboat),
                    [VehicleType.AttackHelicopter] = new GuiFuelStorage(VehicleType.AttackHelicopter),
                    [VehicleType.Motorbike] = new GuiFuelStorage(VehicleType.Motorbike),
                    [VehicleType.Motortrike] = new GuiFuelStorage(VehicleType.Motortrike),

                };

                //populate...
                //first, check if no fuel is required globally!
                //we're gonna skip this step if the fuel is not globally required.

                if (Instance.configData.specificNoNeedForFuel != null)
                {
                    foreach (var noFuelVehicle in Instance.configData.specificNoNeedForFuel)
                    {
                        VehicleGUIs[noFuelVehicle.Key].noFuelRequiredForVehicle = noFuelVehicle.Value;
                    }
                }

                if (Instance.configData.globalNoNeedForFuel == false)
                {
                    //limit to cols * rows items, so it doesn't try any more than the number of anchors we've generated
                    foreach (var fuelDef in Instance.configData.fuelDefinitions.Take(GUI_COLS * GUI_ROWS))
                    {
                        //is this fuel enabled?
                        if (fuelDef.Value.globalEnabled)
                        {
                            foreach (var vehicleDef in fuelDef.Value.specificVehicleSettings)
                            {
                                if (vehicleDef.Value.specificEnabled)
                                {
                                    if (!VehicleGUIs[vehicleDef.Key].noFuelRequiredForVehicle)
                                    {
                                        float usage = fuelDef.Value.consumptionType == ConsumptionType.None ? 0F : fuelDef.Value.globalUsageMultiplier * fuelDef.Value.globalConsumptionAmount * vehicleDef.Value.specificConsumptionAmount * vehicleDef.Value.specificUsageMultiplier;

                                        VehicleGUIs[vehicleDef.Key].fuelItems.Add(fuelDef.Key, usage);
                                    }
                                }
                            }
                        }
                    }
                }

                //and once everything's populated, generate

                RegeneratePlayerGuiPages();

            }

            public static void RegeneratePlayerGuiPages()
            {
                foreach (var gui in VehicleGUIs)
                {
                    gui.Value.RegeneratePlayerGUI();
                }
            }

            public static void ShowGUIFuelStorage(BasePlayer player, VehicleType vehicleType)
            {
                VehicleGUIs[vehicleType].ShowGUI(player);
            }

            public static void HideGUIFuelStorage(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "fmgui.title.text");
                CuiHelper.DestroyUi(player, "fmgui.desc");

                for (var i=0; i<GuiManagerPlayer.GUI_COLS * GuiManagerPlayer.GUI_ROWS; i++)
                {
                    CuiHelper.DestroyUi(player, $"fmgui.content.{i}.bg");
                    CuiHelper.DestroyUi(player, $"fmgui.content.{i}.name");
                    CuiHelper.DestroyUi(player, $"fmgui.content.{i}.img");
                    CuiHelper.DestroyUi(player, $"fmgui.content.{i}.amount");
                }
            }
        }
        #endregion

        #region GUI_ADMIN
        public static class GuiManagerAdmin
        {
            public static Dictionary<int, GuiPage> GuiPages;
            public static Dictionary<BasePlayer, PlayerGuiState> PlayerGuiStates;

            public const int PAGE_GENERAL = 0;
            public const int PAGE_MINICOPTER = 1;
            public const int PAGE_SCRAPHELI = 2;
            public const int PAGE_HAB = 3;
            public const int PAGE_CAR = 4;
            public const int PAGE_ROWBOAT = 5;
            public const int PAGE_RHIB = 6;
            public const int PAGE_TRAIN = 7;
            public const int PAGE_CRANE = 8;
            public const int PAGE_SUBSOLO = 9;
            public const int PAGE_SUBDUO = 10;
            public const int PAGE_SNOWMOBILE_RED = 11;
            public const int PAGE_SNOWMOBILE_TOMAHA = 12;
            public const int PAGE_TUGBOAT = 13;
            public const int PAGE_ATTACKHELICOPTER = 14;
            public const int PAGE_MOTORBIKE = 15;
            public const int PAGE_MOTORTRIKE = 16;

            public const int SUBANCHOR_FULL = 0;

            public const int SUBANCHOR_HALF_LEFT = 1;
            public const int SUBANCHOR_HALF_RIGHT = 2;
            public const int SUBANCHOR_HALF_TOP = 3;
            public const int SUBANCHOR_HALF_BOTTOM = 4;

            public const int SUBANCHOR_CORNER_LEFT_BOTTOM = 5;
            public const int SUBANCHOR_CORNER_RIGHT_BOTTOM = 6;

            public const int SUBANCHOR_CORNER_LEFT_TOP = 7;
            public const int SUBANCHOR_CORNER_RIGHT_TOP = 8;

            public const int SCREEN_WIDTH_IN_PIXELS = 1920;
            public const int SCREEN_HEIGHT_IN_PIXELS = 1080;

            public const float SCREEN_RATIO = (float)SCREEN_WIDTH_IN_PIXELS / (float)SCREEN_HEIGHT_IN_PIXELS;

            public const int MAIN_LEFTMOST_X = 192;
            public const int MAIN_BOTTOMMOST_Y = 898;

            public const int MAIN_RIGHTMOST_X = 1727;
            public const int MAIN_TOPMOST_Y = 100;

            public const int MAIN_PADDING = 12;

            public const int BUTTON_LEFTMOST_X = SCREEN_WIDTH_IN_PIXELS - 308;
            public const int BUTTON_BOTTOMMOST_Y = (SCREEN_HEIGHT_IN_PIXELS / 2) + 60;

            public const int BUTTON_RIGHTMOST_X = SCREEN_WIDTH_IN_PIXELS;
            public const int BUTTON_TOPMOST_Y = (SCREEN_HEIGHT_IN_PIXELS / 2) - 60;

            //padded with border thickness
            public const int MAIN_LEFTMOST_X_P = MAIN_LEFTMOST_X + MAIN_PADDING;
            public const int MAIN_BOTTOMMOST_Y_P = MAIN_BOTTOMMOST_Y - MAIN_PADDING;

            public const int MAIN_RIGHTMOST_X_P = MAIN_RIGHTMOST_X - MAIN_PADDING;
            public const int MAIN_TOPMOST_Y_P = MAIN_TOPMOST_Y + MAIN_PADDING;

            //and same for button
            public const int BUTTON_LEFTMOST_X_P = SCREEN_WIDTH_IN_PIXELS - 308 + MAIN_PADDING;
            public const int BUTTON_BOTTOMMOST_Y_P = (SCREEN_HEIGHT_IN_PIXELS / 2) + 60 - MAIN_PADDING;

            public const int BUTTON_RIGHTMOST_X_P = SCREEN_WIDTH_IN_PIXELS - MAIN_PADDING;
            public const int BUTTON_TOPMOST_Y_P = (SCREEN_HEIGHT_IN_PIXELS / 2) - 60 + MAIN_PADDING;

            //common stuff
            public const int CLOSE_WIDTH = 64;
            public const int CLOSE_HEIGHT = 64;

            public const int CLOSE_LEFTMOST_X = MAIN_RIGHTMOST_X_P - CLOSE_WIDTH;
            public const int CLOSE_BOTTOMMOST_Y = MAIN_TOPMOST_Y_P + CLOSE_HEIGHT;

            public const int CLOSE_RIGHTMOST_X = MAIN_RIGHTMOST_X_P;
            public const int CLOSE_TOPMOST_Y = MAIN_TOPMOST_Y_P;

            //title

            public const int TITLE_HEIGHT = 64;

            public const int TITLE_LEFTMOST_X = MAIN_LEFTMOST_X_P;
            public const int TITLE_BOTTOMMOST_Y = MAIN_TOPMOST_Y_P + TITLE_HEIGHT;

            public const int TITLE_RIGHTMOST_X = MAIN_RIGHTMOST_X_P;
            public const int TITLE_TOPMOST_Y = MAIN_TOPMOST_Y_P;

            //menu
            public const int MENU_HEIGHT = 64;

            public const int MENU_LEFTMOST_X = MAIN_LEFTMOST_X_P;
            public const int MENU_BOTTOMMOST_Y = TITLE_BOTTOMMOST_Y + MAIN_PADDING + MENU_HEIGHT;

            public const int MENU_RIGHTMOST_X = MAIN_RIGHTMOST_X_P;
            public const int MENU_TOPMOST_Y = TITLE_BOTTOMMOST_Y + MAIN_PADDING;

            //backdrop

            public const int BACKDROP_LEFTMOST_X = MAIN_LEFTMOST_X_P;
            public const int BACKDROP_BOTTOMMOST_Y = MAIN_BOTTOMMOST_Y_P;

            public const int BACKDROP_RIGHTMOST_X = MAIN_RIGHTMOST_X_P;
            public const int BACKDROP_TOPMOST_Y = TITLE_BOTTOMMOST_Y + MAIN_PADDING;

            //FONT SIZES
            public const int FONT_SIZE_CLOSE = 36;
            public const int FONT_SIZE_BIG = 32;
            public const int FONT_SIZE_MEDIUM = 24;
            public const int FONT_SIZE_SMALL = 14;

            public static CuiRectTransformComponent GetAnchorLeftSquare(CuiRectTransformComponent originalAnchor, out float newWidth, float paddingInPixels = 0)
            {
                var paddingH = ScreenToRustX(paddingInPixels);
                var paddingV = 1F-ScreenToRustY(paddingInPixels);

                var anchorMinSplit = originalAnchor.AnchorMin.Split(' ');
                var anchorMaxSplit = originalAnchor.AnchorMax.Split(' ');

                float anchorMinX = float.Parse(anchorMinSplit[0]);
                float anchorMinY = float.Parse(anchorMinSplit[1]);

                float anchorMaxX = float.Parse(anchorMaxSplit[0]);
                float anchorMaxY = float.Parse(anchorMaxSplit[1]);

                float width = anchorMaxX - anchorMinX;
                float height = anchorMaxY - anchorMinY;

                newWidth = height / SCREEN_RATIO;

                return new CuiRectTransformComponent { AnchorMin = $"{anchorMinX+ paddingH} {anchorMinY+ paddingV}", AnchorMax = $"{anchorMinX + newWidth - paddingH} {anchorMaxY- paddingV}" };
            }

            public static Dictionary<object, object> GetConsumptionValues()
            {
                return new Dictionary<object, object>
                {
                    [ConsumptionType.Condition] = ConsumptionType.Contents,
                    [ConsumptionType.Contents] = ConsumptionType.DataInt,
                    [ConsumptionType.DataInt] = ConsumptionType.Item,
                    [ConsumptionType.Item] = ConsumptionType.None,
                    [ConsumptionType.None] = ConsumptionType.Condition
                };
            }
            public static Dictionary<object, ColorCode> GetConsumptionColors()
            {
                return new Dictionary<object, ColorCode>
                {
                    [ConsumptionType.Condition] = ColorPalette.RustyGreen,
                    [ConsumptionType.Contents] = ColorPalette.RustyYellow,
                    [ConsumptionType.DataInt] = ColorPalette.RustyAqua,
                    [ConsumptionType.Item] = ColorPalette.RustyBlue,
                    [ConsumptionType.None] = ColorPalette.RustyRed
                };
            }

            public static Dictionary<object, string> GetConsumptionHints()
            {
                return new Dictionary<object, string>
                {
                    [ConsumptionType.Condition] = MSG(MSG_GUI_CONSUMPTION_CONDITION),
                    [ConsumptionType.Contents] = MSG(MSG_GUI_CONSUMPTION_CONTENTS),
                    [ConsumptionType.DataInt] = MSG(MSG_GUI_CONSUMPTION_DATA),
                    [ConsumptionType.Item] = MSG(MSG_GUI_CONSUMPTION_ITEM),
                    [ConsumptionType.None] = MSG(MSG_GUI_CONSUMPTION_NONE),
                };
            }

            public static CuiRectTransformComponent GetAnchorResizedRelative(CuiRectTransformComponent originalAnchor, float xLeft, float yBottom, float xRight, float yTop, float paddingInPixels = 0)
            {
                var paddingH = ScreenToRustX(paddingInPixels);
                var paddingV = 1F-ScreenToRustY(paddingInPixels);

                //let's split parse some strings, shall we.
                var anchorMinSplit = originalAnchor.AnchorMin.Split(' ');
                var anchorMaxSplit = originalAnchor.AnchorMax.Split(' ');

                float anchorMinX = float.Parse(anchorMinSplit[0]);
                float anchorMinY = float.Parse(anchorMinSplit[1]);

                float anchorMaxX = float.Parse(anchorMaxSplit[0]);
                float anchorMaxY = float.Parse(anchorMaxSplit[1]);

                float width = anchorMaxX - anchorMinX;
                float height = anchorMaxY - anchorMinY;

                float xLeftNew = anchorMinX + xLeft * width;
                float yBottomNew = anchorMinY + yBottom * height;

                float xRightNew = anchorMaxX - (1 - xRight) * width;
                float yTopNew = anchorMaxY - (1 - yTop) * height;

                return new CuiRectTransformComponent { AnchorMin = $"{xLeftNew+paddingH} {yBottomNew+paddingV}", AnchorMax = $"{xRightNew-paddingH} {yTopNew-paddingV}" };
            }

            //close button
            public static readonly CuiRectTransformComponent AnchorCloseButton = GetAnchorFromScreenBox(CLOSE_LEFTMOST_X, CLOSE_BOTTOMMOST_Y, CLOSE_RIGHTMOST_X, CLOSE_TOPMOST_Y);

            //title
            public static readonly CuiRectTransformComponent AnchorTitle = GetAnchorFromScreenBox(TITLE_LEFTMOST_X, TITLE_BOTTOMMOST_Y, TITLE_RIGHTMOST_X, TITLE_TOPMOST_Y);

            //backdrop
            public static readonly CuiRectTransformComponent AnchorBackdrop = GetAnchorFromScreenBox(BACKDROP_LEFTMOST_X, BACKDROP_BOTTOMMOST_Y, BACKDROP_RIGHTMOST_X, BACKDROP_TOPMOST_Y);

            public static CuiRectTransformComponent GetAnchorFromScreenBox(float leftmostX, float bottommostY, float rightmostX, float topmostY)
            {
                return new CuiRectTransformComponent { AnchorMin = $"{ScreenToRustX(leftmostX).ToString()} {ScreenToRustY(bottommostY).ToString()}", AnchorMax = $"{ScreenToRustX(rightmostX).ToString()} {ScreenToRustY(topmostY).ToString()}" };
            }


            //main anchor to cover the entire screen
            //should just return "0 0 1 1" if everything's okay
            public static readonly CuiRectTransformComponent AnchorFull = GetAnchorFromScreenBox(0, SCREEN_HEIGHT_IN_PIXELS, SCREEN_WIDTH_IN_PIXELS, 0);

            //bordered anchor
            public static readonly CuiRectTransformComponent AnchorBordered = GetAnchorFromScreenBox(MAIN_LEFTMOST_X, MAIN_BOTTOMMOST_Y, MAIN_RIGHTMOST_X, MAIN_TOPMOST_Y);

            //usable space anchor (padded with border)
            public static readonly CuiRectTransformComponent AnchorUsable = GetAnchorFromScreenBox(MAIN_LEFTMOST_X_P, MAIN_BOTTOMMOST_Y_P, MAIN_RIGHTMOST_X_P, MAIN_TOPMOST_Y_P);

            //page anchor
            public static readonly CuiRectTransformComponent AnchorPage = GetAnchorFromScreenBox(MAIN_LEFTMOST_X_P, MAIN_BOTTOMMOST_Y_P, MAIN_RIGHTMOST_X_P, MAIN_TOPMOST_Y_P + MENU_HEIGHT + TITLE_HEIGHT + MAIN_PADDING);

            //containers

            //static always contains the same elements: blurry window, title, backdrop, X-button.

            public static CuiElementContainer ContainerStatic;

            public static CuiElementContainer[] ContainersMenu;

            public static CuiElement ContainerStaticWindow;
            public static CuiElement ContainerStaticTitle;
            //public static CuiElement ContainerStaticBackdrop;
            public static CuiButton ContainerStaticX;

            public static object InputActionFloat(params object[] args)
            {
                if (args == null) return null;
                if (args.Length == 0) return null;

                if (args[0] == null) return null;

                var arg = args[0].ToString().Replace("<SPACE>", " ");
                if (arg == "" || arg == null)
                {
                    return null;
                }

                float maybeFloat;

                if (float.TryParse(arg, out maybeFloat))
                {
                    if (maybeFloat > 0F)
                    {
                        return maybeFloat;
                    }
                    else return null;

                }
                else
                {
                    return null;
                }
            }

            public static object InputActionInt(params object[] args)
            {
                if (args == null) return null;
                if (args.Length == 0) return null;

                if (args[0] == null) return null;

                var arg = args[0].ToString().Replace("<SPACE>", " ");
                if (arg == "" || arg == null)
                {
                    return null;
                }

                int maybeInt;

                if (int.TryParse(arg, out maybeInt))
                {
                    if (maybeInt >= 1)
                    {
                        return maybeInt;
                    }
                    else return null;

                }
                else
                {
                    return null;
                }
            }

            public static string InputActionNormal(params object[] args)
            {
                if (args == null) return null;
                if (args.Length == 0) return null;

                if (args[0] == null) return null;

                var arg = args[0].ToString().Replace("<SPACE>", " ");
                if (arg == "" || arg == null)
                {
                    return null;
                }
                else return arg;
            }

            public static void Cleanup()
            {
                foreach (var entry in PlayerGuiStates.ToDictionary(k => k.Key, v => v.Value))
                {
                    if (entry.Key != null)
                    {
                        PlayerGuiClose(entry.Key);
                    }
                }

                //null out:

                ContainerStatic = null;
                ContainersMenu = null;

                ContainerStaticWindow = null;
                ContainerStaticTitle = null;
                //ContainerStaticBackdrop = null;
                ContainerStaticX = null;



                GuiPages = null;



                PlayerGuiStates = null;
            }

            public static void Prepare()
            {
                PlayerGuiStates = new Dictionary<BasePlayer, PlayerGuiState>();
                ContainerStatic = new CuiElementContainer();

                GuiPages = new Dictionary<int, GuiPage>
                {
                    //this will use the default language

                    [PAGE_GENERAL] = new GuiPageGeneral(PAGE_GENERAL, MSG(MSG_GENERAL), ColorPalette.GreyLight),
                    [PAGE_MINICOPTER] = new GuiPageVehicleSpecific(PAGE_MINICOPTER, VEH(VehicleType.Minicopter, null, true), ColorPalette.RedLight, VehicleType.Minicopter),
                    [PAGE_SCRAPHELI] = new GuiPageVehicleSpecific(PAGE_SCRAPHELI, VEH(VehicleType.Scrapheli, null, true), ColorPalette.YellowLight, VehicleType.Scrapheli),
                    [PAGE_HAB] = new GuiPageVehicleSpecific(PAGE_HAB, VEH(VehicleType.HotAirBalloon, null, true), ColorPalette.LimeLight, VehicleType.HotAirBalloon),
                    [PAGE_CAR] = new GuiPageVehicleSpecific(PAGE_CAR, VEH(VehicleType.Car, null, true), ColorPalette.GreenLight, VehicleType.Car),
                    [PAGE_ROWBOAT] = new GuiPageVehicleSpecific(PAGE_ROWBOAT, VEH(VehicleType.Rowboat, null, true), ColorPalette.AquaLight, VehicleType.Rowboat),
                    [PAGE_RHIB] = new GuiPageVehicleSpecific(PAGE_RHIB, VEH(VehicleType.Rhib, null, true), ColorPalette.BlueLight, VehicleType.Rhib),
                    [PAGE_TRAIN] = new GuiPageVehicleSpecific(PAGE_TRAIN, VEH(VehicleType.Train, null, true), ColorPalette.PurpleLight, VehicleType.Train),
                    [PAGE_CRANE] = new GuiPageVehicleSpecific(PAGE_CRANE, VEH(VehicleType.Crane, null, true), ColorPalette.PinkLight, VehicleType.Crane),
                    [PAGE_SUBSOLO] = new GuiPageVehicleSpecific(PAGE_SUBSOLO, VEH(VehicleType.SubSolo, null, true), ColorPalette.OrangeLight, VehicleType.SubSolo),
                    [PAGE_SUBDUO] = new GuiPageVehicleSpecific(PAGE_SUBDUO, VEH(VehicleType.SubDuo, null, true), ColorPalette.PissYellowLight, VehicleType.SubDuo),
                    [PAGE_SNOWMOBILE_RED] = new GuiPageVehicleSpecific(PAGE_SNOWMOBILE_RED, VEH(VehicleType.SnowmobileRed, null, true), ColorPalette.BurgundyLight, VehicleType.SnowmobileRed),
                    [PAGE_SNOWMOBILE_TOMAHA] = new GuiPageVehicleSpecific(PAGE_SNOWMOBILE_TOMAHA, VEH(VehicleType.SnowmobileTomaha, null, true), ColorPalette.CaramelLight, VehicleType.SnowmobileRed),
                    [PAGE_TUGBOAT] = new GuiPageVehicleSpecific(PAGE_TUGBOAT, VEH(VehicleType.Tugboat, null, true), ColorPalette.RustyYellow, VehicleType.Tugboat),
                    [PAGE_ATTACKHELICOPTER] = new GuiPageVehicleSpecific(PAGE_ATTACKHELICOPTER, VEH(VehicleType.AttackHelicopter, null, true), ColorPalette.RustyRed, VehicleType.AttackHelicopter),
                    [PAGE_MOTORBIKE] = new GuiPageVehicleSpecific(PAGE_MOTORBIKE, VEH(VehicleType.Motorbike, null, true), ColorPalette.CeladonLight, VehicleType.Motorbike),
                    [PAGE_MOTORTRIKE] = new GuiPageVehicleSpecific(PAGE_MOTORTRIKE, VEH(VehicleType.Motortrike, null, true), ColorPalette.CeladonLight, VehicleType.Motortrike),
                };

                //for every active page, there's one corresponding menu, no need to re-create those on the fly.
                //just pre-generate all menu containers and display the one that's needed, based on what page the player's on.

                //these will be pretty much copies of one thing.
                PrepareMenus();

                ContainerStaticWindow = new CuiElement
                {
                    Name = "fmgui.static.window",
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = ColorPalette.Black.rustString+" 0.97",
                            ImageType = UnityEngine.UI.Image.Type.Simple,
                            Material = "assets/content/ui/uibackgroundblur.mat",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = AnchorBordered.AnchorMin,
                            AnchorMax = AnchorBordered.AnchorMax
                        },

                        new CuiNeedsCursorComponent()
                    }
                };

                ContainerStatic.Add(ContainerStaticWindow);

                ContainerStaticTitle = new CuiElement
                {
                    Name = "fmgui.static.title",
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            FontSize = FONT_SIZE_BIG,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            Color = ColorPalette.White.rustString,
                            Text = $"FUEL MANAGER {VERSION}"
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = AnchorTitle.AnchorMin,
                            AnchorMax = AnchorTitle.AnchorMax
                        }
                    }
                };

                ContainerStatic.Add(ContainerStaticTitle);

                ContainerStaticX = new CuiButton
                {
                    Button =
                    {
                        Color = ColorPalette.RustyRed.rustString,
                        Command = "fm_gui close",
                    },

                    Text =
                    {
                        Color = ColorPalette.White.rustString,
                        Align = UnityEngine.TextAnchor.MiddleCenter,
                        FontSize = FONT_SIZE_CLOSE,
                        Text = "X"
                    },
                    RectTransform = { AnchorMin = AnchorCloseButton.AnchorMin, AnchorMax = AnchorCloseButton.AnchorMax },
                };

                ContainerStatic.Add(ContainerStaticX, "Overlay", "fmgui.static.x");

                /*
                ContainerStaticBackdrop = new CuiElement
                {
                    Name = "fmgui.static.backdrop",
                    Parent = "Overlay",
                    Components =
                    {
                        new CuiRawImageComponent
                        {
                            Url = "ENTER URL HERE",
                        },

                        new CuiRectTransformComponent
                        {
                            AnchorMin = AnchorBackdrop.AnchorMin,
                            AnchorMax = AnchorBackdrop.AnchorMax
                        }
                    }
                };
                */

                //ContainerStatic.Add(ContainerStaticBackdrop);
            }

            public const float MENU_OPACITY_BG_ACTIVE = 0.3F;
            public const float MENU_OPACITY_BG_INACTIVE = 0.15F;
            public const float MENU_OPACITY_TEXT_INACTIVE = 0.5F;

            public static void PrepareMenus()
            {
                ContainersMenu = new CuiElementContainer[GuiPages.Count];

                float menuWidthTotal = MENU_RIGHTMOST_X - MENU_LEFTMOST_X;
                float menuWidthItem = menuWidthTotal / GuiPages.Count;

                CuiRectTransformComponent[] anchorsEnabled = new CuiRectTransformComponent[GuiPages.Count];
                CuiRectTransformComponent[] anchorsDisabled = new CuiRectTransformComponent[GuiPages.Count];

                float correctionH = 2F;
                float correctionV = 2F;

                //menu magic

                for (var i = 0; i < GuiPages.Count; i++)
                {
                    anchorsEnabled[i] = GetAnchorFromScreenBox(MENU_LEFTMOST_X + i * (menuWidthItem), MENU_BOTTOMMOST_Y - correctionV, MENU_LEFTMOST_X + ((i + 1) * (menuWidthItem) - correctionH), MENU_TOPMOST_Y);

                    //same, just +12px from top
                    anchorsDisabled[i] = GetAnchorFromScreenBox(MENU_LEFTMOST_X + i * (menuWidthItem), MENU_BOTTOMMOST_Y - correctionV, MENU_LEFTMOST_X + ((i + 1) * (menuWidthItem)) - correctionH, MENU_TOPMOST_Y + MAIN_PADDING);
                }

                var index = 0;

                Dictionary<int, CuiButton> buttonsEnabled = new Dictionary<int, CuiButton>();
                Dictionary<int, CuiButton> buttonsDisabled = new Dictionary<int, CuiButton>();

                foreach (var page in GuiPages)
                {
                    //we need one "enabled" and one "disabled" button per page
                    //enabled buttons don't have any commands, they're radio buttons kinda

                    var newButtonDisabled = new CuiButton
                    {
                        Button =
                        {
                            Color = page.Value.colorCode.rustString+$" {MENU_OPACITY_BG_INACTIVE}",
                            Command = $"fm_gui menu {page.Value.pageID}",
                        },

                        Text =
                        {
                            Color = ColorPalette.White.rustString+$" {MENU_OPACITY_TEXT_INACTIVE}",
                            Align = UnityEngine.TextAnchor.LowerCenter,
                            FontSize = FONT_SIZE_MEDIUM-10,
                            Text = page.Value.title
                        },
                        RectTransform = { AnchorMin = anchorsDisabled[index].AnchorMin, AnchorMax = anchorsDisabled[index].AnchorMax },
                    };

                    //the key also doubles as the Cui id for Destroying purposes.
                    //the enabled/disabled version can share the same ids

                    buttonsDisabled.Add(index, newButtonDisabled);

                    var newButtonEnabled = new CuiButton
                    {
                        Button =
                        {
                            Color = page.Value.colorCode.rustString+$" {MENU_OPACITY_BG_ACTIVE}",
                            Command = $"",
                        },

                        Text =
                        {
                            Color = ColorPalette.White.rustString,
                            Align = UnityEngine.TextAnchor.MiddleCenter,
                            FontSize = FONT_SIZE_MEDIUM-10,
                            Text = page.Value.title
                        },
                        RectTransform = { AnchorMin = anchorsEnabled[index].AnchorMin, AnchorMax = anchorsEnabled[index].AnchorMax },
                    };

                    buttonsEnabled.Add(index, newButtonEnabled);

                    index++;
                }

                //now all the buttons are ready, they just need to be put in the final containers.
                //iterate over pages once again.

                index = 0;

                foreach (var page in GuiPages)
                {
                    CuiElementContainer pageMenuContainer = new CuiElementContainer();

                    var enabledPage = page.Value.pageID;

                    //add the proper buttons
                    for (var i = 0; i < GuiPages.Count; i++)
                    {
                        if (i == enabledPage)
                        {
                            pageMenuContainer.Add(buttonsEnabled[i], "Overlay", $"fmgui.menu.{i}");
                        }
                        else
                        {
                            pageMenuContainer.Add(buttonsDisabled[i], "Overlay", $"fmgui.menu.{i}");
                        }
                    }

                    ContainersMenu[index] = pageMenuContainer;

                    index++;
                }
            }


            public static void MenuShow(BasePlayer player, int menuID)
            {
                MenuHide(player);
                //which container to show?
                CuiHelper.AddUi(player, ContainersMenu[menuID]);
            }

            public static void MenuHide(BasePlayer player)
            {
                for (var i = 0; i < GuiPages.Count; i++)
                {
                    CuiHelper.DestroyUi(player, $"fmgui.menu.{i}");
                }
            }

            public static void SetPage(BasePlayer player, int page)
            {
                PlayerGuiStates[player].page = page;
            }

            public static int GetPage(BasePlayer player)
            {
                if (!PlayerGuiStates.ContainsKey(player)) return 0;
                return PlayerGuiStates[player].page;
            }

            public static void PageShow(BasePlayer player, int pageID)
            {
                PageHide(player);

                SetPage(player, pageID);

                GuiPages[pageID].PageShow(player);
            }

            public static void PageHide(BasePlayer player)
            {
                GuiPages[GetPage(player)].PageHide(player);
            }

            public static void PageView(BasePlayer player, int pageID)
            {
                PageShow(player, pageID);
                MenuShow(player, pageID);
            }

            public static void PlayerGuiOpen(BasePlayer player)
            {
                if (!PlayerGuiStates.ContainsKey(player))
                {
                    GuiStaticOpen(player);

                    PlayerGuiStates.Add(player, new PlayerGuiState(player));

                    PageView(player, 0);
                }
            }

            public static void PlayerGuiClose(BasePlayer player)
            {
                GuiStaticClose(player);

                PageHide(player);
                MenuHide(player);

                if (PlayerGuiStates.ContainsKey(player))
                {
                    PlayerGuiStates.Remove(player);
                }
            }

            public static void GuiStaticOpen(BasePlayer player)
            {
                CuiHelper.AddUi(player, ContainerStatic);
            }

            public static void GuiStaticClose(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "fmgui.static.window");
                CuiHelper.DestroyUi(player, "fmgui.static.x");
                CuiHelper.DestroyUi(player, "fmgui.static.title");
                CuiHelper.DestroyUi(player, "fmgui.static.backdrop");
            }


            //simple stuff, like navigation, buttons etc will also derive from it.
            //a page cell has width, height (base

            public class GuiPageCell<T>
            {
                //by default cells take 1 full row
                public int width; //0 means take full row
                public int height; //0 means take full column
                public int x;
                public int y;

                public CuiRectTransformComponent anchorFull;

                public int optionID;

                public int orderID;

                public GuiPage guiPage;

                public int fontSize;

                //public List<string> elementNameList = new List<string>();

                public GuiPageCell(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize)
                {
                    guiPage = page;
                    optionID = id;

                    orderID = order;
                    this.width = width;
                    this.height = height;

                    this.x = x;
                    this.y = y;

                    this.fontSize = fontSize;

                    GenerateCellAnchors();

                }

                //call manually after adding the cell to the dictionary

                public virtual void GenerateCellAnchors()
                {
                    anchorFull = GetAnchorCellFull();
                }

                public virtual CuiElementContainer GetFinalContainerForPlayer(BasePlayer player)
                {
                    var container = new CuiElementContainer();

                    return container;
                }

                public virtual void PopulateContainerWithResultsStuff(BasePlayer player, ref CuiElementContainer container, ref List<string> elementNames)
                {

                }
                public virtual Dictionary<int, GuiPageCell<object>> GetDynamicCells(BasePlayer player, GuiPage currentPage)
                {
                    return new Dictionary<int, GuiPageCell<object>>();
                }

                public virtual List<CuiPanel> GetDynamicPanelsForPlayer(BasePlayer player)
                {
                    return new List<CuiPanel>();
                }

                public virtual List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    return new List<CuiElement>();
                }

                public virtual List<CuiButton> GetDynamicButtonsForPlayer(BasePlayer player)
                {
                    return new List<CuiButton>();
                }

                public static void GetPageDimensions(int padding, out int leftMost, out int bottomMost, out int rightMost, out int topMost, out int width, out int height)
                {
                    leftMost = (MAIN_LEFTMOST_X_P + padding);
                    rightMost = (MAIN_RIGHTMOST_X_P - padding);
                    bottomMost = (MAIN_BOTTOMMOST_Y_P - padding);
                    topMost = (MAIN_TOPMOST_Y_P + MENU_HEIGHT + TITLE_HEIGHT + MAIN_PADDING + padding);
                    width = rightMost - leftMost;
                    height = bottomMost - topMost;
                }

                public CuiRectTransformComponent GenerateAnchorCellArbitrary(int _x, int _y, int _cellsX, int _cellsY, int padding = MAIN_PADDING/2)
                {
                    //based on the x, y, width and height
                    //how many rows/columns does it have?

                    //based on page width divided over the number of columns
                    //and on page height divided over the number of rows,

                    //calculate
                    var cellsX = _cellsX;
                    var cellsY = _cellsY;

                    if (cellsX == 0) cellsX = guiPage.cols;
                    if (cellsY == 0) cellsY = guiPage.rows;

                    int leftmostX;
                    int bottommostY;
                    int rightmostX;
                    int topmostY;

                    int pageWidth;
                    int pageHeight;

                    GetPageDimensions(padding, out leftmostX, out bottommostY, out rightmostX, out topmostY, out pageWidth, out pageHeight);

                    var singleCellWidth = (float)pageWidth / (float)guiPage.cols;
                    var singleCellHeight = (float)pageHeight / (float)guiPage.rows;

                    var thisCellWidth = singleCellWidth * cellsX;
                    var thisCellHeight = singleCellHeight * cellsY;

                    //take padding into account between cells maybe? or not. your call. padding can be always sorted out with more cells.

                    return GetAnchorFromScreenBox(leftmostX + _x * singleCellWidth + padding, topmostY + _y * singleCellHeight + thisCellHeight - padding, leftmostX + _x * singleCellWidth + thisCellWidth - padding, topmostY + _y * singleCellHeight + padding);
                }

                public CuiRectTransformComponent GetAnchorCellFull()
                {
                    return GenerateAnchorCellArbitrary(this.x, this.y, this.width, this.height);
                }

            }

            public class GuiBackdropColor<T> : GuiPageCell<T>
            {
                public CuiElement image;
                public string url;

                public GuiBackdropColor(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize, ColorCode color, float alpha, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, fontSize)
                {
                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                    }

                    image = new CuiElement
                    {
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = color.rustString +$" {alpha.ToString()}",
                            },
                            anchorFull
                        },
                        Name = $"fmgui.backdrop.{guiPage.pageID}.{optionID}",
                        Parent = "Overlay"
                    };
                }
                public override List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    return new List<CuiElement> { image };
                }
            }

            public class GuiLabelStatic<T> : GuiPageCell<T>
            {
                public CuiElement text;

                public GuiLabelStatic(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize, TextAnchor textAlign, ColorCode color, string text, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, fontSize)
                {
                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                    }

                    this.text = new CuiElement
                    {
                        Name = $"fmgui.labelstatic.{guiPage.pageID}.{optionID}",
                        Parent = "Overlay",
                        Components =
                            {
                                new CuiTextComponent
                                {
                                    Align = textAlign,
                                    Color = color.rustString,
                                    FontSize = fontSize,
                                    Text = text
                                },
                               anchorFull
                            }
                    };
                }

                public override List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    return new List<CuiElement> { text };
                }
            }

            public class GuiCellImage<T> : GuiPageCell<T>
            {
                public CuiElement image;
                public string url;

                public GuiCellImage(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize, string url, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, fontSize)
                {
                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                    }

                    image = new CuiElement
                    {
                        Name = $"fmgui.image.{guiPage.pageID}.{optionID}",
                        Parent = "Overlay",
                        Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Url = url,
                                },
                               anchorFull
                            }
                    };
                }

                public override List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    return new List<CuiElement> { image };
                }
            }

            public class GuiOption<T> : GuiPageCell<T>
            {
                public Func<T> getter;
                public Action<T> setter;

                public GuiOption(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize, Func<T> getter, Action<T> setter) : base(page, id, order, x, y, width, height, fontSize)
                {
                    //optionID = id;
                    InitializeGetterAndSetter(getter, setter);
                }

                public void InitializeGetterAndSetter(Func<T> getter, Action<T> setter)
                {
                    this.getter = getter;
                    this.setter = setter;
                }

                public virtual T GetterWrapper(T getter)
                {
                    return getter;
                }

                public virtual void SetterWrapper(Action<T> action, T value)
                {
                    action(value);
                }

                public T Value
                {
                    get
                    {
                        return getter(); //GetterWrapper(getter());
                    }
                    set
                    {
                        setter(value);
                        Instance.SaveConfigData();

                        GuiManagerPlayer.GenerateAllVehicleGUIs();
                    }
                }
            }

            public class GuiRecordGeneral<T> : GuiPageCell<T>
            {
                public GuiRecordGeneral(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize) : base(page, id, order, x, y, width, height, fontSize)
                {

                }
            }

            public class GuiOptionInputBox<T> : GuiOption<T>
            {
                public string valueHint;
                public string valueFormat; //0 is valueHint, 1 is Value
                public string treatNullAs = "";

                public int charLimit = 256;

                public int widthHint;

                public CuiRectTransformComponent anchorHint;
                public CuiRectTransformComponent anchorInput; //and panel

                public CuiPanel panel;
                public CuiElement input;
                public CuiElement hint;

                //input action must return true, otherwise no setting the value for real
                public Func<object[], object> inputAction;

                public GuiOptionInputBox(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize, Func<T> getter, Action<T> setter, Func<object[], object> inputAction, string valueHint, int charLimit, int widthHint, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, fontSize, getter, setter)
                {
                    var colorPrefix = $"<size=12><color={ColorPalette.White.hexValue}>";
                    valueFormat = colorPrefix+ "{0}:</color> \n<i>{1}</i></size> ";

                    this.valueHint = valueHint;
                    this.charLimit = charLimit;
                    this.widthHint = widthHint;
                    this.inputAction = inputAction;

                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                        anchorHint = GetAnchorResizedRelative(anchorFull, 0, 0, 0.68F, 1);
                        anchorInput = GetAnchorResizedRelative(anchorFull, 0.68F, 0, 1F, 1F);


                    }



                    panel = new CuiPanel
                    {
                        CursorEnabled = true,
                        Image = new CuiImageComponent
                        {
                            Color = ColorPalette.White.rustString,
                        },
                        RectTransform =
                        {
                            AnchorMin = anchorInput.AnchorMin,
                            AnchorMax = anchorInput.AnchorMax
                        }
                    };

                    input = new CuiElement
                    {
                        Components =
                        {
                            new CuiInputFieldComponent
                            {
                                Align = TextAnchor.MiddleCenter,
                                CharsLimit = charLimit,
                                Color = ColorPalette.GreyDark.rustString,
                                FontSize = fontSize,
                                Command = $"fm_gui input {page.pageID} {optionID}"
                            },
                            GuiManagerAdmin.AnchorFull //full anchor because we're parenting shit
                        },
                        Name = $"fmgui.input.dynamic.{page.pageID}.{optionID}",
                        Parent = $"fmgui.panel.dynamic.{page.pageID}.{optionID}",
                        
                    };

                    hint = new CuiElement
                    {
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Align = TextAnchor.MiddleRight,
                                Text = ValueFormat(),
                                FontSize = fontSize,
                                Color = ColorPalette.RustyYellow.rustString
                            },
                            anchorHint
                        },
                        Name = $"fmgui.input.dynamic.{page.pageID}.{optionID}",
                        Parent = "Overlay"
                    };
                }

                public string ValueFormat()
                {
                    return string.Format(valueFormat, valueHint.ToString(), (object)Value);
                }

                //returning non null will not apply
                public object InputAction(params object[] args)
                {
                    var result = inputAction(args);

                    //returning non-null means everything went cool, here's the value
                    if (result != null)
                    {
                        Value = (T)result;
                    }

                    return result;
                }

                public override void GenerateCellAnchors()
                {
                    base.GenerateCellAnchors();

                    anchorHint = GenerateAnchorCellArbitrary(this.x, this.y, this.widthHint, this.height);
                    anchorInput = GenerateAnchorCellArbitrary(this.x + widthHint, this.y, this.width - widthHint, this.height);
                  
                }

                public override List<CuiPanel> GetDynamicPanelsForPlayer(BasePlayer player)
                {
                    return new List<CuiPanel> { panel };
                }

                public override List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    var maybeText = (hint.Components[0] as CuiTextComponent);
                    var maybeAnchor = (hint.Components[0] as CuiRectTransformComponent);

                    maybeText.Text = ValueFormat();
                    maybeAnchor = anchorFull;

                    return new List<CuiElement> { hint, input };
                }
            }

            public class GuiOptionArbitrary<T> : GuiPageCell<T>
            {
                public GuiOptionArbitrary(GuiPage page, int id, int order, int x, int y, int width, int height, string hint, int fontSize, ColorCode bgColorCode, ColorCode textColorCode) : base(page, id, order, x, y, width, height, fontSize)
                {
                }
            }

            //for displaying results and stuff.
            //it will

            public class GuiOptionResultsGeneral<T> : GuiOptionResults<T>
            {
                public GuiOptionResultsGeneral(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize) : base(page, id, order, x, y, width, height, fontSize)
                {

                }
                public override Dictionary<int, GuiPageCell<object>> GetDynamicCells(BasePlayer player, GuiPage currentPage)
                {
                    var cells = base.GetDynamicCells(player, currentPage);

                    var resultsPerPage = guiPage.resultsPerPage;

                    var elementStart = PlayerGuiStates[player].subpage * guiPage.resultsPerPage;

                    var index = 0;

                    ColorCode[] colorPattern = new ColorCode[2] { ColorPalette.GreyLight, ColorPalette.GreyDark };

                    var general = MSG(MSG_GENERAL);
                    var vehicles = MSG(MSG_VEHICLES);

                    foreach (var type in currentPage.GetSubpageResults(player).Skip(elementStart).Take(resultsPerPage))
                    {
                        var fuelDef = type as FuelDefinition;

                        var itemDef = ItemManager.FindItemDefinition(fuelDef.itemID);

                        float imageWidth;

                        guiPage.AddPageBackdrop(ref cells, index + 10000, x, y, width, height, FONT_SIZE_SMALL, colorPattern[index % 2], 0.5F, anchors[index][SUBANCHOR_FULL]);

                        guiPage.AddPageImage(ref cells, index + 20000, x, y, width, height, FONT_SIZE_SMALL, MakeURL(itemDef.shortname), GetAnchorLeftSquare(anchors[index][SUBANCHOR_FULL], out imageWidth, MAIN_PADDING / 2F));

                        guiPage.AddPageLabelStatic(ref cells, index + 30000, x, y, width, height, FONT_SIZE_MEDIUM, TextAnchor.MiddleLeft, ColorPalette.White, $"{itemDef.displayName.translated}", GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.08F, 0.5F, 0.5F, 1F, MAIN_PADDING / 2F));


                        guiPage.AddPageOptionInput(ref cells, index + 40000, x, y, width, height, FONT_SIZE_SMALL, () => $"<color={ColorPalette.White.hexValue}>{MSG(MSG_GUI_CURRENT_VALUE)}:</color> " + fuelDef.globalConsumptionAmount.ToString(), val => { fuelDef.globalConsumptionAmount = (int)val; }, GuiManagerAdmin.InputActionInt, MSG(MSG_GUI_CONSUMPTION_AMOUNT, null, general), 32, 0, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.05F, 0, 0.275F, 0.5F, MAIN_PADDING / 2F));

                        guiPage.AddPageOptionInput(ref cells, index + 50000, x, y, width, height, FONT_SIZE_SMALL, () => $"<color={ColorPalette.White.hexValue}>{MSG(MSG_GUI_CURRENT_VALUE)}:</color> " + fuelDef.globalUsageMultiplier.ToString("0.000"), val => { fuelDef.globalUsageMultiplier = (float)val; }, GuiManagerAdmin.InputActionFloat, MSG(MSG_GUI_MULTIPLIER, null, general), 32, 0, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.275F, 0, 0.5F, 0.5F, MAIN_PADDING / 2F));


                        guiPage.AddPageOptionMultiple(ref cells, index + 60000, x, y, width, height, () => fuelDef.consumptionType, val => { fuelDef.consumptionType = (ConsumptionType)val; }, FONT_SIZE_SMALL,
                            GetConsumptionValues(),
                            GetConsumptionColors(),
                            GetConsumptionHints(),
                            GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.5F, 0.5F, 3/4F, 1, MAIN_PADDING / 2F)
                        );

                        guiPage.AddPageArbitraryCommandButton(ref cells, index + 70000, x, y, width, height, MSG(MSG_GUI_CLEAR_PERM), FONT_SIZE_SMALL, ColorPalette.RustyBlue, ColorPalette.White, $"fm_gui clear_perm_global {fuelDef.itemID}", GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 7F / 8F, 0, 1F, 0.5F, MAIN_PADDING / 2F));

                        guiPage.AddPageOptionToggle(ref cells, index+80000, x, y, width, height, () => fuelDef.globalEnabled, val => { fuelDef.globalEnabled = (bool)val; }, MSG(MSG_GUI_FUEL_ENABLED, null, vehicles), MSG(MSG_GUI_FUEL_DISABLED, null, vehicles), FONT_SIZE_SMALL, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 3/4F, 0.5F, 15/16F, 1, MAIN_PADDING / 2F)); //0 is full anchor, for now

                        guiPage.AddPageArbitraryCommandButton(ref cells, index + 90000, x, y, width, height, MSG(MSG_GUI_REMOVE_ITEM), FONT_SIZE_SMALL, ColorPalette.RustyRed, ColorPalette.White, $"fm_gui remove {fuelDef.itemID}", GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 15F/16F, 0.5F, 1F, 1, MAIN_PADDING / 2F));

                        guiPage.AddPageOptionInput(ref cells, index + 100000, x, y, width, height, FONT_SIZE_SMALL, () => (fuelDef.globalPermissionNeeded == null ? MSG(MSG_GUI_NO_PERMISSION_SET) : fuelDef.globalPermissionNeeded), val => { fuelDef.globalPermissionNeeded = (string)val; }, GuiManagerAdmin.InputActionNormal, MSG(MSG_GUI_PERM_NEEDED, null, general), 256, 0, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.5F, 0F, 7/8F, 0.5F, MAIN_PADDING / 2F));



                        index++;
                    }

                    guiPage.AddPageOptionInput(ref cells, index + 110000, x, y, width, height, FONT_SIZE_SMALL, () => PlayerGuiStates[player].addNewItem, val => { PlayerGuiStates[player].addNewItem = (string)val; }, GuiManagerAdmin.InputActionNormal, MSG(MSG_GUI_ADD_NEW_ITEM), 256, 0, GetAnchorResizedRelative(AnchorPage, 2.05F/12F, 1 - ScreenToRustY(MAIN_PADDING / 2F), 5/12F, 1/12F, MAIN_PADDING / 2F));



                    return cells;
                }
                public override void PopulateContainerWithResultsStuff(BasePlayer player, ref CuiElementContainer container, ref List<string> elementNames)
                {

                }
            }

            public class GuiOptionResultsVehicleSpecific<T> : GuiOptionResults<T>
            {
                public VehicleType vehicleType;

                public GuiOptionResultsVehicleSpecific(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize, VehicleType vehicleType) : base(page, id, order, x, y, width, height, fontSize)
                {
                    this.vehicleType = vehicleType;
                }

                public override Dictionary<int, GuiPageCell<object>> GetDynamicCells(BasePlayer player, GuiPage currentPage)
                {
                    var cells = base.GetDynamicCells(player, currentPage);

                    var resultsPerPage = guiPage.resultsPerPage;

                    var elementStart = PlayerGuiStates[player].subpage * guiPage.resultsPerPage;

                    var index = 0;

                    ColorCode[] colorPattern = new ColorCode[2] { ColorPalette.GreyLight, ColorPalette.GreyDark };

                    var singular = VEH(vehicleType);
                    var plural = VEH(vehicleType, null, true);

                    foreach (var type in currentPage.GetSubpageResults(player).Skip(elementStart).Take(resultsPerPage))
                    {
                        var fuelDef = type as FuelDefinition;

                        var itemDef = ItemManager.FindItemDefinition(fuelDef.itemID);

                        float imageWidth;

                        //stay the same
                        guiPage.AddPageBackdrop(ref cells, index + 10000, x, y, width, height, FONT_SIZE_SMALL, colorPattern[index % 2], 0.5F, anchors[index][SUBANCHOR_FULL]);

                        guiPage.AddPageImage(ref cells, index + 20000, x, y, width, height, FONT_SIZE_SMALL, MakeURL(itemDef.shortname), GetAnchorLeftSquare(anchors[index][SUBANCHOR_FULL], out imageWidth, MAIN_PADDING / 2F));

                        guiPage.AddPageLabelStatic(ref cells, index + 30000, x, y, width, height, FONT_SIZE_MEDIUM, TextAnchor.MiddleLeft, ColorPalette.White, $"{itemDef.displayName.translated}", GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.08F, 0.5F, 0.5F, 1F, MAIN_PADDING / 2F));
                        //up until this point

                        guiPage.AddPageOptionInput(ref cells, index + 40000, x, y, width, height, FONT_SIZE_SMALL, () => $"<color={ColorPalette.White.hexValue}>{MSG(MSG_GUI_CURRENT_VALUE)}:</color> " + fuelDef.specificVehicleSettings[vehicleType].specificConsumptionAmount.ToString(), val => { fuelDef.specificVehicleSettings[vehicleType].specificConsumptionAmount = (int)val; }, GuiManagerAdmin.InputActionInt, MSG(MSG_GUI_CONSUMPTION_AMOUNT, null, singular), 32, 0, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.05F, 0, 0.275F, 0.5F, MAIN_PADDING / 2F));

                        guiPage.AddPageOptionInput(ref cells, index + 50000, x, y, width, height, FONT_SIZE_SMALL, () => $"<color={ColorPalette.White.hexValue}>{MSG(MSG_GUI_CURRENT_VALUE)}:</color> " + fuelDef.specificVehicleSettings[vehicleType].specificUsageMultiplier.ToString("0.000"), val => { fuelDef.specificVehicleSettings[vehicleType].specificUsageMultiplier = (float)val; }, GuiManagerAdmin.InputActionFloat, MSG(MSG_GUI_MULTIPLIER, null, singular), 32, 0, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.275F, 0, 0.5F, 0.5F, MAIN_PADDING / 2F));

                        //stays the same
                        guiPage.AddPageOptionMultiple(ref cells, index + 60000, x, y, width, height, () => fuelDef.consumptionType, val => { fuelDef.consumptionType = (ConsumptionType)val; }, FONT_SIZE_SMALL,
                            GetConsumptionValues(),
                            GetConsumptionColors(),
                            GetConsumptionHints(),
                            GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.5F, 0.5F, 3 / 4F, 1, MAIN_PADDING / 2F)
                        );

                        //this needs to be changed to local perm
                        guiPage.AddPageArbitraryCommandButton(ref cells, index + 70000, x, y, width, height, MSG(MSG_GUI_CLEAR_PERM), FONT_SIZE_SMALL, ColorPalette.RustyBlue, ColorPalette.White, $"fm_gui clear_perm_specific {fuelDef.itemID} {singular}", GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 7F / 8F, 0, 1F, 0.5F, MAIN_PADDING / 2F));

                        //changed
                        guiPage.AddPageOptionToggle(ref cells, index + 80000, x, y, width, height, () => fuelDef.specificVehicleSettings[vehicleType].specificEnabled, val => { fuelDef.specificVehicleSettings[vehicleType].specificEnabled = (bool)val; }, MSG(MSG_GUI_FUEL_ENABLED, null, plural), MSG(MSG_GUI_FUEL_DISABLED, null, plural), FONT_SIZE_SMALL, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 3 / 4F, 0.5F, 15F / 16F, 1, MAIN_PADDING / 2F)); //0 is full anchor, for now

                        //stays the same
                        guiPage.AddPageArbitraryCommandButton(ref cells, index + 90000, x, y, width, height, MSG(MSG_GUI_REMOVE_ITEM), FONT_SIZE_SMALL, ColorPalette.RustyRed, ColorPalette.White, $"fm_gui remove {fuelDef.itemID}", GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 15F / 16F, 0.5F, 1F, 1, MAIN_PADDING / 2F));


                        //changed
                        guiPage.AddPageOptionInput(ref cells, index + 100000, x, y, width, height, FONT_SIZE_SMALL, () => (fuelDef.specificVehicleSettings[vehicleType].specificPermissionNeeded == null ? MSG(MSG_GUI_NO_PERMISSION_SET): fuelDef.specificVehicleSettings[vehicleType].specificPermissionNeeded), val => { fuelDef.specificVehicleSettings[vehicleType].specificPermissionNeeded = (string)val; }, GuiManagerAdmin.InputActionNormal, MSG(MSG_GUI_PERM_NEEDED, null, singular), 256, 0, GetAnchorResizedRelative(anchors[index][SUBANCHOR_FULL], 0.5F, 0F, 7 / 8F, 0.5F, MAIN_PADDING / 2F));
                        index++;
                    }

                    guiPage.AddPageOptionInput(ref cells, index + 110000, x, y, width, height, FONT_SIZE_SMALL, () => PlayerGuiStates[player].addNewItem, val => { PlayerGuiStates[player].addNewItem = (string)val; }, GuiManagerAdmin.InputActionNormal, MSG(MSG_GUI_ADD_NEW_ITEM), 256, 0, GetAnchorResizedRelative(AnchorPage, 2.05F / 12F, 1 - ScreenToRustY(MAIN_PADDING / 2F), 5 / 12F, 1 / 12F, MAIN_PADDING / 2F));



                    return cells;
                }

                public override void PopulateContainerWithResultsStuff(BasePlayer player, ref CuiElementContainer container, ref List<string> elementNames)
                {
                    //if there's no need for fuel globally/specifically for this vehicle, no need to display everything!
                    string maybeNoNeedReason;

                    //only used here
                    var elementsList = new List<CuiElement>();

                    if (GuiPage.VehicleNeedsFuel((guiPage as GuiPageVehicleSpecific).vehicleType, out maybeNoNeedReason))
                    {
                        var resultIndex = 0;

                        var resultsPerPage = guiPage.resultsPerPage;

                        var elementStart = PlayerGuiStates[player].subpage * guiPage.resultsPerPage;

                        //guiPage will now give stuff

                        foreach (var entry in guiPage.GetSubpageResults(player).Skip(elementStart).Take(resultsPerPage))//  .GetRange(elementStart, resultsPerPage))
                        {

                            var fuelDef = entry as FuelDefinition;

                            var newElement = new CuiElement
                            {
                                Components =
                            {
                                new CuiTextComponent
                                {
                                    Align = TextAnchor.MiddleCenter,
                                    Text = $"{fuelDef.itemID}", //dynamic, run it through value format
                                    FontSize = fontSize,
                                    Color = ColorPalette.White.rustString
                                },
                                anchors[resultIndex][SUBANCHOR_FULL]
                            },
                                Name = $"fmgui.{guiPage.pageID}.{optionID}.result.{resultIndex}",
                                Parent = "Overlay",
                            };

                            elementsList.Add(newElement);

                            resultIndex++;
                        }
                    }
                    else
                    {
                        var type = (guiPage as GuiPageVehicleSpecific).vehicleType;
                        //if there's no need, just display a full notice on the page

                        string noticeString = maybeNoNeedReason == "global" ? $"Enable fuel for all vehicles\n(global, in the General tab)\nto see specific fuel settings" : $"Enable fuel for {VEH(type, null, true)} (button in the upper left corner of this page)\nto see specific fuel settings";
                        var newElement = new CuiElement
                        {
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Align = TextAnchor.MiddleCenter,
                                    Text = noticeString,
                                    FontSize = FONT_SIZE_BIG,
                                    Color = ColorPalette.GreyLight.rustString
                                },
                                anchorFull
                            },
                            Name = $"fmgui.notice.{guiPage.pageID}.{optionID}",
                            Parent = "Overlay",
                        };

                        elementsList.Add(newElement);
                    }

                    //now for every element in the list...
                    foreach (var element in elementsList)
                    {
                        container.Add(element);
                        elementNames.Add(element.Name);
                    }
                }


                
            }

            public class GuiOptionResults<T> : GuiPageCell<T>
            {
                public List<Dictionary<int, CuiRectTransformComponent>> anchors;

                public GuiOptionResults(GuiPage page, int id, int order, int x, int y, int width, int height, int fontSize) : base(page, id, order, x, y, width, height, fontSize)
                {
                    
                }

                //don't use those. instead, override page's Populate method.

                public override void PopulateContainerWithResultsStuff(BasePlayer player, ref CuiElementContainer container, ref List<string> elementNames)
                {

                }

                public override List<CuiButton> GetDynamicButtonsForPlayer(BasePlayer player)
                {
                    return new List<CuiButton>();
                }

                public override List<CuiPanel> GetDynamicPanelsForPlayer(BasePlayer player)
                {
                    return new List<CuiPanel>();
                }

                public override List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    return new List<CuiElement>();
                }

                public override void GenerateCellAnchors()
                {
                    base.GenerateCellAnchors();

                    anchors = new List<Dictionary<int, CuiRectTransformComponent>>();

                    int leftmostX;
                    int bottommostY;
                    int rightmostX;
                    int topmostY;

                    int pageWidth;
                    int pageHeight;

                    var padding = MAIN_PADDING / 2;

                    GetPageDimensions(padding, out leftmostX, out bottommostY, out rightmostX, out topmostY, out pageWidth, out pageHeight);

                    var singleCellWidth = Mathf.FloorToInt((float)pageWidth / (float)guiPage.cols);
                    var singleCellHeight = Mathf.FloorToInt((float)pageHeight / (float)guiPage.rows);

                    //now adjust the dimensions based on how many reserved rows

                    topmostY += singleCellHeight * guiPage.rowPaddingTop;
                    bottommostY -= singleCellHeight * guiPage.rowPaddingBottom;

                    pageWidth = rightmostX - leftmostX;
                    pageHeight = bottommostY - topmostY;

                    //how many rows and cols for results?

                    float resultWidthPx = (1F / guiPage.colsResult) * pageWidth;
                    float resultHeightPx = (1F / guiPage.rowsResult) * pageHeight;

                    var index = 0;

                    for (int y= 0; y < guiPage.rowsResult; y++)
                    {
                        for (int x = 0; x < guiPage.colsResult; x++)
                        {
                            //generate anchors based on the full page anchor.
                            //take row padding (top and bottom) into account.
                            //that padding uses the page's row height.
                            anchors.Add(new Dictionary<int, CuiRectTransformComponent>
                            {
                                //full anchor that takes the whole record
                                [SUBANCHOR_FULL] = GetAnchorFromScreenBox(leftmostX + x * resultWidthPx + padding, topmostY + y * resultHeightPx + resultHeightPx - padding, leftmostX + x * resultWidthPx + resultWidthPx - padding, topmostY + y * resultHeightPx + padding),
                                [SUBANCHOR_HALF_BOTTOM] = GetAnchorFromScreenBox(leftmostX + x * resultWidthPx + padding, topmostY + y * resultHeightPx + resultHeightPx - padding, leftmostX + x * resultWidthPx + resultWidthPx - padding, topmostY + y * resultHeightPx + padding + resultHeightPx/2),
                                [SUBANCHOR_HALF_TOP] = GetAnchorFromScreenBox(leftmostX + x * resultWidthPx + padding, topmostY + y * resultHeightPx + resultHeightPx - padding - resultHeightPx/2, leftmostX + x * resultWidthPx + resultWidthPx - padding, topmostY + y * resultHeightPx + padding),
                                [SUBANCHOR_HALF_RIGHT] = GetAnchorFromScreenBox(leftmostX + x * resultWidthPx + padding + resultWidthPx / 2, topmostY + y * resultHeightPx + resultHeightPx - padding, leftmostX + x * resultWidthPx + resultWidthPx - padding, topmostY + y * resultHeightPx + padding),
                                [SUBANCHOR_HALF_LEFT] = GetAnchorFromScreenBox(leftmostX + x * resultWidthPx + padding, topmostY + y * resultHeightPx + resultHeightPx - padding, leftmostX + x * resultWidthPx + resultWidthPx - padding - resultWidthPx / 2, topmostY + y * resultHeightPx + padding),
                            });
                            //anchors are indexed by records, each is a dictionary

                            index++;
                        }
                    }


                }
            }

            public class GuiOptionInteractiveLabelSubpage<T> : GuiOptionInteractiveLabel<T>
            {
                public GuiOptionInteractiveLabelSubpage(GuiPage page, int id, int order, int x, int y, int width, int height, string hint, int fontSize, ColorCode bgColorCode, ColorCode textColorCode, string hintFormat) : base(page, id, order, x, y, width, height, hint, fontSize, bgColorCode, textColorCode, hintFormat)
                {
                    
                }

                public override List<CuiElement> GetDynamicElementsForPlayer(BasePlayer player)
                {
                    var maxSubpages = guiPage.GetNumberOfSubpages(player);

                    (text.Components[0] as CuiTextComponent).Text = ValueFormat((PlayerGuiStates[player].subpage +1).ToString(), maxSubpages.ToString());

                    //time to get a list from the page, results based on subpage....

                    return new List<CuiElement> { text };
                }
            }
            public class GuiOptionInteractiveLabel<T> : GuiOptionArbitrary<T>
            {
                public string hint;
                public string hintFormat;

                public CuiElement text;

                public GuiOptionInteractiveLabel(GuiPage page, int id, int order, int x, int y, int width, int height, string hint, int fontSize, ColorCode bgColorCode, ColorCode textColorCode, string hintFormat) : base(page, id, order, x, y, width, height, hint, fontSize, bgColorCode, textColorCode)
                {
                    this.hint = hint;
                    this.hintFormat = hintFormat;

                    text = new CuiElement
                    {
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Align = TextAnchor.MiddleCenter,
                                Text = "THIS WILL CHANGE", //dynamic, run it through value format
                                FontSize = FONT_SIZE_SMALL,
                                Color = ColorPalette.White.rustString
                            },
                            anchorFull
                        },
                        Name = $"fmgui.label.{page.pageID}.{optionID}",
                        Parent = "Overlay",
                    };
                }

                public string ValueFormat(params string[] args)
                {
                    return string.Format(hintFormat, hint, args[0], args[1]);
                }
            }

            public class GuiOptionArbitraryCommandButton<T> : GuiOptionArbitrary<T>
            {
                public CuiButton button;

                public GuiOptionArbitraryCommandButton(GuiPage page, int id, int order, int x, int y, int width, int height, string hint, int fontSize, ColorCode bgColorCode, ColorCode textColorCode, string command, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, hint, fontSize, bgColorCode, textColorCode)
                {
                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                    }

                    button = new CuiButton
                    {
                        Button =
                        {
                            Color = bgColorCode.rustString, //will change on the fly
                            Command = command,
                        },
                        Text =
                        {
                            FontSize = fontSize,
                            Align = TextAnchor.MiddleCenter,
                            Color = textColorCode.rustString,
                            Text = hint
                        },
                        RectTransform =
                        {
                            AnchorMin = this.anchorFull.AnchorMin,
                            AnchorMax = this.anchorFull.AnchorMax
                        }
                    };
                }
                public override List<CuiButton> GetDynamicButtonsForPlayer(BasePlayer player)
                {
                    return new List<CuiButton> { button };
                }
            }

            public class GuiOptionToggleMultiple<T> : GuiOption<T>
            {
                public Dictionary<object, object> valueDictionary;
                public Dictionary<object, ColorCode> colorCurrentDictionary;
                public Dictionary<object, string> hintCurrentDictionary;
                public CuiButton multipleButton;

                public int currentValue;

                public GuiOptionToggleMultiple(GuiPage page, int id, int order, int x, int y, int width, int height, Func<T> getter, Action<T> setter, int fontSize, Dictionary<object, object> valueNextDictionary, Dictionary<object, ColorCode> colorCurrentDictionary, Dictionary<object, string> hintCurrentDictionary, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, fontSize, getter, setter)
                {
                    this.valueDictionary = valueNextDictionary; //key is the current value, Action is action for that value
                    this.colorCurrentDictionary = colorCurrentDictionary;
                    this.hintCurrentDictionary = hintCurrentDictionary;

                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                    }

                    multipleButton = new CuiButton
                    {
                        Button =
                        {
                            Color = ColorPalette.RustyGreen.rustString, //will change on the fly
                            Command = $"fm_gui toggle_multiple {guiPage.pageID} {optionID}",
                        },
                        Text =
                        {
                            FontSize = this.fontSize,
                            Align = TextAnchor.MiddleCenter,
                            Color = ColorPalette.White.rustString,
                            Text = "THIS WILL CHANGE DYNAMICALLY"
                        },
                        RectTransform =
                        {
                            AnchorMin = this.anchorFull.AnchorMin,
                            AnchorMax = this.anchorFull.AnchorMax
                        }
                    };

                }

                public object ValueGet()
                {
                    return Value;
                }

                public void ValueSetNext(BasePlayer player)
                {
                    Value = (T)valueDictionary[ValueGet()];

                    GuiManagerPlayer.GenerateAllVehicleGUIs();

                    PageView(player, PlayerGuiStates[player].page);
                }

                public override List<CuiButton> GetDynamicButtonsForPlayer(BasePlayer player)
                {

                    multipleButton.Button.Color = colorCurrentDictionary[ValueGet()].rustString;
                    multipleButton.Text.Text = hintCurrentDictionary[ValueGet()];

                    return new List<CuiButton> { multipleButton };
                }
            }

            public class GuiOptionToggle<T> : GuiOption<T>
            {
                public string valueTrue;
                public string valueFalse;

                public CuiButton toggleButton;

                //add one button. the command is based on a prefix, page and id.

                public GuiOptionToggle(GuiPage page, int id, int order, int x, int y, int width, int height, Func<T> getter, Action<T> setter, string valueTrue, string valueFalse, int fontSize, CuiRectTransformComponent forceAnchor = null) : base(page, id, order, x, y, width, height, fontSize, getter, setter)
                {
                    this.valueTrue = valueTrue;
                    this.valueFalse = valueFalse;
                    this.fontSize = fontSize;
                    //InitializeGetterAndSetter(getter, setter);

                    if (forceAnchor != null)
                    {
                        anchorFull = forceAnchor;
                    }

                    toggleButton = new CuiButton
                    {
                        Button =
                        {
                            Color = ColorPalette.RustyGreen.rustString, //will change on the fly
                            Command = $"fm_gui toggle {guiPage.pageID} {optionID}",
                        },
                        Text =
                        {
                            FontSize = this.fontSize,
                            Align = TextAnchor.MiddleCenter,
                            Color = ColorPalette.White.rustString,
                            Text = "THIS WILL CHANGE DYNAMICALLY"
                        },
                        RectTransform =
                        {
                            AnchorMin = this.anchorFull.AnchorMin,
                            AnchorMax = this.anchorFull.AnchorMax
                        }
                };

                    ///this.Value.Equals(true);
                }

                public void BoolToggleValue(BasePlayer player)
                {
                    Value = (T)(object)(!BoolGet());

                    GuiManagerPlayer.GenerateAllVehicleGUIs();
                    //refresh the page for player
                    PageView(player, PlayerGuiStates[player].page);
                }

                public bool BoolGet()
                {
                    return Value.Equals(true);
                }

                public override void GenerateCellAnchors()
                {
                    base.GenerateCellAnchors();
                }


                public override List<CuiButton> GetDynamicButtonsForPlayer(BasePlayer player)
                {

                    toggleButton.Button.Color = BoolGet() ? ColorPalette.RustyGreen.rustString : ColorPalette.RustyRed.rustString;
                    toggleButton.Text.Text = BoolGet() ? valueTrue : valueFalse;

                    return new List<CuiButton> { toggleButton };
                }


            }

            public class PlayerGuiState
            {
                public BasePlayer player;

                public PlayerGuiState(BasePlayer player)
                {
                    this.player = player;
                }

                public int page = 0;
                public int subpage = 0;

                public string addNewItem
                {
                    get
                    {
                        return null;
                    }
                    set
                    {
                        int maybeID = 0;

                        var maybeFind = ItemManager.GetItemDefinitions().Where(d => !Instance.configData.fuelDefinitions.ContainsKey(d.itemid) && (d.displayName.translated.Contains(value.ToLower()) || d.shortname.Contains(value.ToLower()))).FirstOrDefault();

                        if (maybeFind == null)
                        {

                            if (int.TryParse(value, out maybeID))
                            {
                                maybeFind = ItemManager.FindItemDefinition(maybeID);

                                if (maybeFind != null)
                                {
                                    maybeID = maybeFind.itemid;
                                }
                            }
                        }
                        else
                        {
                            maybeID = maybeFind.itemid;
                        }

                        if (maybeID != 0)
                        {
                            //check if it's not already in definitions!

                            if (!Instance.configData.fuelDefinitions.ContainsKey(maybeID))
                            {
                                var newDefinition = GetDefaultFuelDefinitionForItem(maybeID);

                                if (newDefinition != null)
                                {
                                    Instance.configData.fuelDefinitions.Add(newDefinition.itemID, newDefinition);

                                    Instance.SaveConfigData();

                                    //go to the last page

                                    subpage = GuiPages[page].GetNumberOfSubpages(player) - 1;

                                    GuiManagerPlayer.GenerateAllVehicleGUIs();


                                }
                            }


                        }
                    }
                }
            

                public List<string> elementsToClear = new List<string>();

                public Dictionary<int, GuiPageCell<object>> dynamicCells = new Dictionary<int, GuiPageCell<object>>();
            };

            public const int OPTION_COUNTERS_ENABLED = 0;
            public const int OPTION_NO_NEED_FOR_FUEL = 1;
            public const int OPTION_GUI_ENABLED = 2;

            public class GuiPageGeneral : GuiPage
            {
                public GuiPageGeneral(int page, string title, ColorCode colorCode) : base(page, title, colorCode)
                {

                }

                public override void InitializePageOptions()
                {
                    AddPageOptionToggle(ref pageCells, OPTION_COUNTERS_ENABLED, 0, 0, 4, 1, () => Instance.configData.globalCountersEnabled, val => { Instance.configData.globalCountersEnabled = (bool)val; }, MSG(MSG_GUI_COUNTERS_ENABLE, null, MSG(MSG_VEHICLES)), MSG(MSG_GUI_COUNTERS_DISABLE, null, MSG(MSG_VEHICLES)), 20);

                    AddPageOptionToggle(ref pageCells, OPTION_GUI_ENABLED, 4, 0, 4, 1, () => Instance.configData.globalEnableGUI, val => { Instance.configData.globalEnableGUI = (bool)val; }, MSG(MSG_GUI_ENABLED, null, MSG(MSG_VEHICLES)), MSG(MSG_GUI_DISABLED, null, MSG(MSG_VEHICLES)), 20);

                    AddPageOptionToggle(ref pageCells, OPTION_NO_NEED_FOR_FUEL, 8, 0, 4, 1, () => Instance.configData.globalNoNeedForFuel, val => { Instance.configData.globalNoNeedForFuel = (bool)val; }, MSG(MSG_GUI_DONT_NEED_FUEL, null, MSG(MSG_VEHICLES)), MSG(MSG_GUI_NEED_FUEL, null, MSG(MSG_VEHICLES)), 20);


                    AddPageArbitraryCommandButton(ref pageCells, OPTION_RESTORE_DEFAULTS, 7, 11, 2, 1, MSG(MSG_GUI_RESTORE_DEFAULTS), FONT_SIZE_SMALL, ColorPalette.RustyRed, ColorPalette.White, "fm_gui restore_default");


                    AddPageArbitraryCommandButton(ref pageCells, OPTION_SUBPAGE_PREV, 0, 11, 2, 1, MSG(MSG_GUI_PAGE_NEXT), FONT_SIZE_SMALL, ColorPalette.RustyBlue, ColorPalette.White, "fm_gui nav prev");
                    AddPageArbitraryCommandButton(ref pageCells, OPTION_SUBPAGE_NEXT, 10, 11, 2, 1, MSG(MSG_GUI_PAGE_PREV), FONT_SIZE_SMALL, ColorPalette.RustyBlue, ColorPalette.White, "fm_gui nav next");

                    AddPageArbitraryInteractiveLabelSubpage(ref pageCells, OPTION_SUBPAGE_LABEL, 5, 11, 2, 1, MSG(MSG_GUI_PAGE, null), FONT_SIZE_SMALL, ColorPalette.White, ColorPalette.White, "{0} {1}/{2}");

                    AddPageResultsGeneral(ref pageCells, OPTION_SUBPAGE_RESULTS, 0, 2, 0, 9, FONT_SIZE_MEDIUM);
                }
            }

            public const int OPTION_SPECIFIC_COUNTERS_ENABLED = 0;
            public const int OPTION_SPECIFIC_NO_NEED_FOR_FUEL = 1;

            public const int OPTION_SUBPAGE_PREV = -1000;
            public const int OPTION_SUBPAGE_NEXT = 1000;

            public const int OPTION_SUBPAGE_LABEL = 258924358;

            public const int OPTION_SUBPAGE_RESULTS = 342589;

            public const int OPTION_RESTORE_DEFAULTS = 1337;

            public const int OPTION_ADD_NEW = 420;

            public class GuiPageVehicleSpecific : GuiPage
            {
                public VehicleType vehicleType;
                public string vehicleNameSingular;
                public string vehicleNamePlural;
                public GuiPageVehicleSpecific(int page, string title, ColorCode colorCode, VehicleType vehicleType) : base(page, title, colorCode)
                {
                    this.vehicleType = vehicleType;

                    vehicleNameSingular = VEH(vehicleType, null, true);
                    vehicleNamePlural = VEH(vehicleType, null, true);

                    RegeneratePageContents();
                }


                //you want to renerate the page contents in this case when you add/remove new type of fuel.
                //adding/removing/enabling/disabling will call this on all pages.
                public void RegeneratePageContents()
                {
                    pageCells.Clear();


                    AddPageOptionToggle(ref pageCells, OPTION_COUNTERS_ENABLED, 0, 0, 4, 1, () => Instance.configData.specificCountersEnabled[vehicleType], val => { Instance.configData.specificCountersEnabled[vehicleType] = (bool)val; }, MSG(MSG_GUI_COUNTERS_ENABLE, null, vehicleNamePlural), MSG(MSG_GUI_COUNTERS_DISABLE, null, vehicleNamePlural), 20);


                    AddPageOptionToggle(ref pageCells, OPTION_GUI_ENABLED, 4, 0, 4, 1, () => Instance.configData.specificEnableGUI[vehicleType], val => { Instance.configData.specificEnableGUI[vehicleType] = (bool)val; }, MSG(MSG_GUI_ENABLED, null, vehicleNamePlural), MSG(MSG_GUI_DISABLED, null, vehicleNamePlural), 20);

                    AddPageOptionToggle(ref pageCells, OPTION_NO_NEED_FOR_FUEL, 8, 0, 4, 1, () => Instance.configData.specificNoNeedForFuel[vehicleType], val => { Instance.configData.specificNoNeedForFuel[vehicleType] = (bool)val; }, MSG(MSG_GUI_DONT_NEED_FUEL, null, vehicleNamePlural), MSG(MSG_GUI_NEED_FUEL, null, vehicleNamePlural), 20);

                    /*
                    AddPageOptionToggle(ref pageCells, OPTION_COUNTERS_ENABLED, 0, 0, 6, 1, () => Instance.configData.specificCountersEnabled[vehicleType], val => { Instance.configData.specificCountersEnabled[vehicleType] = (bool)val; }, MSG(MSG_GUI_COUNTERS_ENABLE, null, vehicleNamePlural), MSG(MSG_GUI_COUNTERS_DISABLE, null, vehicleNamePlural));

                    AddPageOptionToggle(ref pageCells, OPTION_NO_NEED_FOR_FUEL, 6, 0, 6, 1, () => Instance.configData.specificNoNeedForFuel[vehicleType], val => { Instance.configData.specificNoNeedForFuel[vehicleType] = (bool)val; }, MSG(MSG_GUI_DONT_NEED_FUEL, null, vehicleNamePlural), MSG(MSG_GUI_NEED_FUEL, null, vehicleNamePlural));
                    */

                    AddPageArbitraryCommandButton(ref pageCells, OPTION_RESTORE_DEFAULTS, 7, 11, 2, 1, MSG(MSG_GUI_RESTORE_DEFAULTS), FONT_SIZE_SMALL, ColorPalette.RustyRed, ColorPalette.White, "fm_gui restore_default");



                    AddPageArbitraryCommandButton(ref pageCells, OPTION_SUBPAGE_PREV, 0, 11, 2, 1, MSG(MSG_GUI_PAGE_NEXT), FONT_SIZE_SMALL, ColorPalette.RustyBlue, ColorPalette.White, "fm_gui nav prev");
                    AddPageArbitraryCommandButton(ref pageCells, OPTION_SUBPAGE_NEXT, 10, 11, 2, 1, MSG(MSG_GUI_PAGE_PREV), FONT_SIZE_SMALL, ColorPalette.RustyBlue, ColorPalette.White, "fm_gui nav next");

                    AddPageArbitraryInteractiveLabelSubpage(ref pageCells, OPTION_SUBPAGE_LABEL, 5, 11, 2, 1, MSG(MSG_GUI_PAGE), FONT_SIZE_SMALL, ColorPalette.White, ColorPalette.White, "{0} {1}/{2}");

                    AddPageResultsVehicleSpecific(ref pageCells, OPTION_SUBPAGE_RESULTS, 0, 2, 0, 9, FONT_SIZE_MEDIUM, vehicleType);
                }
            }

            public class GuiPage
            {
                public int pageID;
                public string title;
                public ColorCode colorCode;

                public int cols;
                public int rows;

                public int rowPaddingTop;
                public int rowPaddingBottom;

                public int colsResult; //whatever's left of the usable area
                public int rowsResult;

                public int resultsPerPage; //colsResult * rowsResult

                public Dictionary<int, GuiPageCell<object>> pageCells;

                public CuiElementContainer containerContent = new CuiElementContainer();

                public CuiElementContainer containerDynamic;

                public CuiElement pageBackdrop;

                public GuiPage(int page, string title, ColorCode colorCode)
                {
                    this.pageID = page;
                    this.title = title;
                    this.colorCode = colorCode;

                    pageCells = new Dictionary<int, GuiPageCell<object>>();

                    ColRowLayout();

                    InitializePageOptions();

                    InitializePage();

                }

                public virtual List<object> GetSubpageResults(BasePlayer player)
                {
                    var results = new List<object>();

                    foreach (var fuelDef in Instance.configData.fuelDefinitions)
                    {
                        results.Add(fuelDef.Value);
                    }

                    //what subpage is the player on?

                    //how many results per subpage?


                    //non-applicable in this case, but what's the filter?


                    return results;
                }

                public virtual void PopulateDynamicContainer(BasePlayer player, ref CuiElementContainer container, out List<string> elementNames)
                {
                    var currentPageID = PlayerGuiStates[player].page;
                    var currentPage = GuiPages[currentPageID];

                    elementNames = new List<string>();


                    foreach (var option in currentPage.pageCells)
                    {
                        if (option.Value is GuiOptionResults<object>)
                        {
                            PlayerGuiStates[player].dynamicCells = option.Value.GetDynamicCells(player, currentPage);

                            foreach (var dynamicOption in PlayerGuiStates[player].dynamicCells)
                            {
                                foreach (var panel in dynamicOption.Value.GetDynamicPanelsForPlayer(player))
                                {
                                    //add the cuiElement to the result container, add the name generated on the fly too
                                    var dynamicName = $"fmgui.panel.dynamic.{currentPageID}.{dynamicOption.Key}";
                                    container.Add(panel, "Overlay", dynamicName);
                                    elementNames.Add(dynamicName);
                                }

                                int index = 0;
                                //normal cuiElements...
                                foreach (var element in dynamicOption.Value.GetDynamicElementsForPlayer(player))
                                {
                                    var dynamicName = $"fmgui.element.dynamic.{currentPageID}.{dynamicOption.Key}.{index}";
                                    element.Name = dynamicName;
                                    //add the cuiElement to the result container, add the name generated on the fly too
                                    container.Add(element);
                                    elementNames.Add(element.Name);

                                    index++;
                                }
                                //buttons...
                                //normal cuiElements...
                                foreach (var button in dynamicOption.Value.GetDynamicButtonsForPlayer(player))
                                {
                                    //add the cuiElement to the result container, add the name generated on the fly too
                                    var dynamicName = $"fmgui.button.dynamic.{currentPageID}.{dynamicOption.Key}";
                                    container.Add(button, "Overlay", dynamicName);
                                    elementNames.Add(dynamicName);
                                }
                            }
                        }
                        else
                        {
                            foreach (var panel in option.Value.GetDynamicPanelsForPlayer(player))
                            {
                                //add the cuiElement to the result container, add the name generated on the fly too
                                var dynamicName = $"fmgui.panel.dynamic.{currentPageID}.{option.Key}";
                                container.Add(panel, "Overlay", dynamicName);
                                elementNames.Add(dynamicName);
                            }
                            //normal cuiElements...
                            foreach (var element in option.Value.GetDynamicElementsForPlayer(player))
                            {
                                //add the cuiElement to the result container, add the name generated on the fly too
                                container.Add(element);
                                elementNames.Add(element.Name);
                            }
                            //buttons...
                            //normal cuiElements...
                            foreach (var button in option.Value.GetDynamicButtonsForPlayer(player))
                            {
                                //add the cuiElement to the result container, add the name generated on the fly too
                                var dynamicName = $"fmgui.button.dynamic.{currentPageID}.{option.Key}";
                                container.Add(button, "Overlay", dynamicName);
                                elementNames.Add(dynamicName);
                            }
                        }
                    }
                }

                public static bool VehicleNeedsFuel(VehicleType type, out string reason)
                {
                    bool globalNoNeed = Instance.configData.globalNoNeedForFuel;
                    bool specificNoNeed = Instance.configData.specificNoNeedForFuel[type];

                    if (!(globalNoNeed || specificNoNeed))
                    {
                        reason = "needed";
                    }
                    else
                    {
                        if (globalNoNeed)
                        {
                            reason = "global";
                        }
                        else
                        {
                            reason = "specific";
                        }
                    }

                    return !(globalNoNeed || specificNoNeed);
                }

                public int GetNumberOfSubpages(BasePlayer player)
                {
                    return Mathf.Max(1, Mathf.CeilToInt((float)GetSubpageResults(player).Count / (float)resultsPerPage));
                }

                public virtual void InitializePageOptions()
                {

                }


                public virtual void ColRowLayout()
                {
                    cols = 12;
                    rows = 12;

                    //for results, it's different though

                    colsResult = 1;
                    rowsResult = 4;

                    rowPaddingBottom = 1;
                    rowPaddingTop = 1;

                    resultsPerPage = colsResult * rowsResult;
                }


                //not used under normal circumstances
                public void AddPageCell(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize)
                {
                    cellDictionary.Add(optionID, new GuiPageCell<object>
                        (
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize
                        ));
                }

                public void AddPageResultsVehicleSpecific(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize, VehicleType vehicleType)
                {
                    cellDictionary.Add(optionID, new GuiOptionResultsVehicleSpecific<object>
                        (
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize,
                        vehicleType
                        ));
                }

                public void AddPageResultsGeneral(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize)
                {
                    cellDictionary.Add(optionID, new GuiOptionResultsGeneral<object>
                        (
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize
                        ));
                }

                public void AddPageArbitraryInteractiveLabelSubpage(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, string hint, int fontSize, ColorCode buttonColorCode, ColorCode textColorCode, string hintFormat)
                {
                    cellDictionary.Add(optionID, new GuiOptionInteractiveLabelSubpage<object>
                        (
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        hint,
                        fontSize,
                        buttonColorCode,
                        textColorCode,
                        hintFormat
                        ));
                }

                public void AddPageArbitraryCommandButton(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, string hint, int fontSize, ColorCode buttonColorCode, ColorCode textColorCode, string command, CuiRectTransformComponent forceAnchor = null)
                {
                    cellDictionary.Add(optionID, new GuiOptionArbitraryCommandButton<object>
                        (
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        hint,
                        fontSize,
                        buttonColorCode,
                        textColorCode,
                        command,
                        forceAnchor
                        ));
                }

                public void AddPageLabelStatic(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize, TextAnchor textAlign, ColorCode color, string text, CuiRectTransformComponent forceAnchor = null)
                {
                    var newOption = new GuiLabelStatic<object>(
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize,
                        textAlign,
                        color,
                        text,
                        forceAnchor
                        );

                    /*
                    if (forceAnchor != null)
                    {
                        newOption.anchorFull = forceAnchor;
                    }
                    */

                    cellDictionary.Add(optionID, newOption);
                }

                public void AddPageBackdrop(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize, ColorCode color, float alpha, CuiRectTransformComponent forceAnchor = null)
                {
                    var newOption = new GuiBackdropColor<object>(
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize,
                        color,
                        alpha,
                        forceAnchor
                        );

                    /*
                    if (forceAnchor != null)
                    {
                        newOption.anchorFull = forceAnchor;
                    }
                    */

                    cellDictionary.Add(optionID, newOption);
                }

                public void AddPageImage(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize, string url, CuiRectTransformComponent forceAnchor = null)
                {
                    var newOption = new GuiCellImage<object>(
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize,
                        url,
                        forceAnchor
                        );

                    /*
                    if (forceAnchor != null)
                    {
                        newOption.anchorFull = forceAnchor;
                    }
                    */

                    cellDictionary.Add(optionID, newOption);
                }

                public void AddPageOptionMultiple(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, Func<object> getter, Action<object> setter, int fontSize, Dictionary<object, object> valueNextDictionary, Dictionary<object, ColorCode> colorCurrentDictionary, Dictionary<object, string> hintCurrentDictionary, CuiRectTransformComponent forceAnchor = null)
                {
                    var newOption = new GuiOptionToggleMultiple<object>(
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        getter,
                        setter,
                        fontSize,
                        valueNextDictionary,
                        colorCurrentDictionary,
                        hintCurrentDictionary,
                        forceAnchor
                        );

                    cellDictionary.Add(optionID, newOption);
                }

                public void AddPageOptionToggle(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, Func<object> getter, Action<object> setter, string valueTrue, string valueFalse, int fontSize = FONT_SIZE_MEDIUM, CuiRectTransformComponent forceAnchor = null)
                {
                    var newOption = new GuiOptionToggle<object>(
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        getter,
                        setter,
                        valueTrue,
                        valueFalse,
                        fontSize,
                        forceAnchor
                        );

                    /*
                    if (forceAnchor != null)
                    {
                        newOption.anchorFull = forceAnchor;
                    }
                    */

                    cellDictionary.Add(optionID, newOption);
                }
                public void AddPageOptionInput(ref Dictionary<int, GuiPageCell<object>> cellDictionary, int optionID, int x, int y, int width, int height, int fontSize, Func<object> getter, Action<object> setter, Func<object[], object> resultAction, string valueHint, int charLimit, int widthHint, CuiRectTransformComponent forceAnchor = null)
                {
                    cellDictionary.Add(optionID, new GuiOptionInputBox<object>
                        (
                        this,
                        optionID,
                        cellDictionary.Count,
                        x,
                        y,
                        width,
                        height,
                        fontSize,
                        getter,
                        setter,
                        resultAction,
                        valueHint,
                        charLimit,
                        widthHint,
                        forceAnchor
                        ));
                }


                public virtual CuiElementContainer GetDynamicContent(BasePlayer player, out List<string> elementNames)
                {
                    CuiElementContainer resultContainer = new CuiElementContainer();

                    //go through the current player's page GUI options.


                    //elementNames = new List<string>();

                    PopulateDynamicContainer(player, ref resultContainer, out elementNames);

                    return resultContainer;
                }

                public virtual void InitializePage()
                {
                    pageBackdrop = new CuiElement
                    {
                        Components =
                        {
                            new CuiImageComponent
                            {
                                Color = colorCode.rustString +$" {MENU_OPACITY_BG_ACTIVE}",
                            },
                            AnchorPage
                        },
                        Name = $"fmgui.page.backdrop",
                        Parent = "Overlay"
                    };

                    containerContent.Add(pageBackdrop);
                }

                public virtual void PageShow(BasePlayer player)
                {
                    CuiHelper.AddUi(player, containerContent);
                    PageDynamicShow(player);
                }

                public virtual void PageHide(BasePlayer player)
                {
                    //based on the container
                    CuiHelper.DestroyUi(player, $"fmgui.page.backdrop");
                    PageDynamicHide(player);
                }

                public void PageDynamicPrepare(BasePlayer player)
                {
                    containerDynamic = GetDynamicContent(player, out PlayerGuiStates[player].elementsToClear);
                }

                public void PageDynamicShow(BasePlayer player)
                {
                    PageDynamicPrepare(player);

                    CuiHelper.AddUi(player, containerDynamic);

                }
                public void PageDynamicHide(BasePlayer player)
                {
                    foreach (var elementName in PlayerGuiStates[player].elementsToClear)
                    {
                        CuiHelper.DestroyUi(player, elementName);
                    }
                }
            }

            public static float ScreenToRustX(float x)
            {
                return (float)x / SCREEN_WIDTH_IN_PIXELS;
            }
            public static float ScreenToRustY(float y)
            {
                return 1 - ((float)y / SCREEN_HEIGHT_IN_PIXELS);
            }
        }
        [ChatCommand("fm_gui")]
        private void cmdChatAnnounce(BasePlayer player, string command, string[] args)
        {
            if (!(player.IsAdmin || player.IsDeveloper))
            {
                if (!HasPermission(player, PERMISSION_ADMIN))
                {
                    return;
                }
            }

            GuiManagerAdmin.PlayerGuiOpen(player);
        }

        [ConsoleCommand("fm_gui")]
        private void cmdConsolefmgui(ConsoleSystem.Arg arg)
        {
            if (arg.IsRcon)
            {
                return;
            }

            if (arg.Connection.player == null)
            {
                return;
            }

            //admin only. Check for permission here
            if (!(arg.IsAdmin || arg.IsConnectionAdmin))
            {
                if (!HasPermission(arg.Connection.player as BasePlayer, PERMISSION_ADMIN))
                {
                    return;
                }
            }
                                 
            if (!arg.HasArgs())
            {
                GuiManagerAdmin.PlayerGuiOpen(arg.Connection.player as BasePlayer);
            }
            else
            {
                //arg 0 is always a string

                bool arg1exists = arg.Args.Length > 1;
                bool arg1isValidInt = false;
                int arg1 = 0;

                bool arg2exists = arg.Args.Length > 2;
                bool arg2isValidInt = false;
                int arg2 = 0;

                bool arg3exists = arg.Args.Length > 3;
                string remainingArgs = null;

                if (arg1exists)
                {
                    if (int.TryParse(arg.Args[1], out arg1))
                    {
                        arg1isValidInt = true;
                    }

                    if (arg2exists)
                    {
                        if (int.TryParse(arg.Args[2], out arg2))
                        {
                            arg2isValidInt = true;
                        }

                        if (arg3exists)
                        {
                            remainingArgs = String.Join("<SPACE>", arg.Args.Skip(3));
                        }
                    }

                }
                

                switch (arg.Args[0])
                {
                    case "close":
                        {
                            GuiManagerAdmin.PlayerGuiClose(arg.Connection.player as BasePlayer);
                        }
                        break;
                    case "menu":
                        {
                            if (arg1isValidInt)
                            {
                                if (GuiManagerAdmin.GuiPages.ContainsKey(arg1))
                                {
                                    GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, arg1);
                                }
                            }
                        }
                        break;
                    case "toggle": //
                        {
                            if (arg1isValidInt)
                            {
                                if (arg2isValidInt)
                                {
                                    if (GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer].dynamicCells.ContainsKey(arg2))
                                    {
                                        var maybeHyperToggle = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer].dynamicCells[arg2] as GuiManagerAdmin.GuiOptionToggle<object>;

                                        if (maybeHyperToggle != null)
                                        {
                                            maybeHyperToggle.BoolToggleValue(arg.Connection.player as BasePlayer);
                                        }
                                    }
                                    else
                                    {
                                        if (GuiManagerAdmin.GuiPages.ContainsKey(arg1))
                                        {
                                            if (GuiManagerAdmin.GuiPages[arg1].pageCells.ContainsKey(arg2))
                                            {
                                                var maybeToggle = GuiManagerAdmin.GuiPages[arg1].pageCells[arg2] as GuiManagerAdmin.GuiOptionToggle<object>;

                                                if (maybeToggle != null)
                                                {
                                                    maybeToggle.BoolToggleValue(arg.Connection.player as BasePlayer);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "toggle_multiple":
                        {
                            if (arg1isValidInt)
                            {
                                if (arg2isValidInt)
                                {
                                    if (GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer].dynamicCells.ContainsKey(arg2))
                                    {
                                        var maybeHyperToggle = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer].dynamicCells[arg2] as GuiManagerAdmin.GuiOptionToggleMultiple<object>;

                                        if (maybeHyperToggle != null)
                                        {
                                            maybeHyperToggle.ValueSetNext(arg.Connection.player as BasePlayer);
                                        }
                                    }
                                    else
                                    {
                                        if (GuiManagerAdmin.GuiPages.ContainsKey(arg1))
                                        {
                                            if (GuiManagerAdmin.GuiPages[arg1].pageCells.ContainsKey(arg2))
                                            {
                                                var maybeToggle = GuiManagerAdmin.GuiPages[arg1].pageCells[arg2] as GuiManagerAdmin.GuiOptionToggleMultiple<object>;

                                                if (maybeToggle != null)
                                                {
                                                    maybeToggle.ValueSetNext(arg.Connection.player as BasePlayer);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "clear_perm_global":
                        {
                            if (arg1isValidInt)
                            {
                                //arg1 is the item id that needs to have its global fuel perms cleared
                                var fuelDef = configData.fuelDefinitions[arg1];

                                var state = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer];

                                fuelDef.globalPermissionNeeded = null;

                                Instance.SaveConfigData();

                                GuiManagerPlayer.GenerateAllVehicleGUIs();

                                GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);
                            }
                        }
                        break;
                    case "clear_perm_specific":
                        {
                            //fm_gui [0]clear_perm_specific [1]itemID [2][3][4 etc etc]vehicleNameSingular

                            if (arg1isValidInt)
                            {
                                if (configData.fuelDefinitions.ContainsKey(arg1))
                                {
                                    var fuelDef = configData.fuelDefinitions[arg1];

                                    var maybeVehicleNameSingular = string.Join(" ", arg.Args.Skip(2));

                                    var state = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer];

                                    foreach (var vehSpec in fuelDef.specificVehicleSettings)
                                    {
                                        //check if the name matches...?
                                        if (VEH(vehSpec.Key) == maybeVehicleNameSingular)
                                        {
                                            fuelDef.specificVehicleSettings[vehSpec.Key].specificPermissionNeeded = null;

                                            Instance.SaveConfigData();

                                            GuiManagerPlayer.GenerateAllVehicleGUIs();

                                            GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);

                                            break;
                                        }
                                    }
                                }
                            }
                        }
                        break;
                    case "remove":
                        {
                            if (arg1isValidInt)
                            {
                                //arg1 is the item id that needs to be removed from fuel definitions
                                if (configData.fuelDefinitions.ContainsKey(arg1))
                                {
                                    var state = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer];

                                    configData.fuelDefinitions.Remove(arg1);
                                    Instance.SaveConfigData();

                                    var subpageMax = GuiManagerAdmin.GuiPages[state.page].GetNumberOfSubpages(arg.Connection.player as BasePlayer)-1;

                                    if (state.subpage > subpageMax)
                                    {
                                        state.subpage = subpageMax;
                                    }

                                    GuiManagerPlayer.GenerateAllVehicleGUIs();

                                    GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);

                                }
                            }
                        }
                        break;
                    case "input":
                        {
                            var state = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer];

                            if (arg1isValidInt)
                            {
                                if (arg2isValidInt)
                                {
                                    if (GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer].dynamicCells.ContainsKey(arg2))
                                    {
                                        var maybeHyperInput = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer].dynamicCells[arg2] as GuiManagerAdmin.GuiOptionInputBox<object>;

                                        if (maybeHyperInput != null)
                                        {
                                            if (maybeHyperInput.InputAction(remainingArgs) != null)
                                            GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);

                                            /*
                                            if (maybeHyperInput.InputAction(remainingArgs) != null)
                                            {
                                                GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);
                                            }*/
                                        }
                                    }
                                    else
                                    {
                                        if (GuiManagerAdmin.GuiPages.ContainsKey(arg1))
                                        {
                                            if (GuiManagerAdmin.GuiPages[arg1].pageCells.ContainsKey(arg2))
                                            {
                                                var maybeInput = GuiManagerAdmin.GuiPages[arg1].pageCells[arg2] as GuiManagerAdmin.GuiOptionInputBox<object>;

                                                if (maybeInput != null)
                                                {
                                                    if (maybeInput.InputAction(remainingArgs) != null)
                                                    {
                                                        GuiManagerPlayer.GenerateAllVehicleGUIs();
                                                        GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);
                                                    }
                                                }
                                            }
                                        }
                                    }

                                }
                            }
                        }
                        break;
                    case "nav":
                        {
                            //fm_gui nav prev, fm_gui nav next
                            bool success = false;
                            var state = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer];
                            //check what page the player is on
                            switch (arg.Args[1])
                            {
                                case "prev":
                                    {
                                        if (state.subpage>0)
                                        {
                                            state.subpage--;
                                            success = true;
                                        }
                                    }
                                    break;
                                case "next":
                                    {
                                        if (state.subpage+1 < GuiManagerAdmin.GuiPages[state.subpage+1].GetNumberOfSubpages(arg.Connection.player as BasePlayer))
                                        {
                                            state.subpage++;
                                            success = true;
                                        }
                                    }
                                    break;
                            }

                            if (success)
                            {
                                GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);
                            }

                        }
                        break;
                    case "restore_default":
                        {
                            var state = GuiManagerAdmin.PlayerGuiStates[arg.Connection.player as BasePlayer];

                            RestoreDefaultConfig();

                            var subpageMax = GuiManagerAdmin.GuiPages[state.page].GetNumberOfSubpages(arg.Connection.player as BasePlayer) - 1;

                            if (state.subpage > subpageMax)
                            {
                                state.subpage = subpageMax;
                            }

                            GuiManagerAdmin.PageView(arg.Connection.player as BasePlayer, state.page);
                        }
                        break;
                }
            }
        }
        #endregion
    }
}
