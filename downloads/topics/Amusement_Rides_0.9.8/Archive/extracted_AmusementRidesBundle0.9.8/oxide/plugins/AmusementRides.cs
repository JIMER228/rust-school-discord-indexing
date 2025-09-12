// Requires: TapeLibrary

using Network;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Plugins;
using Oxide.Game.Rust;
using Oxide.Game.Rust.Cui;
using ProtoBuf;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using WebSocketSharp;
using static Construction;

namespace Oxide.Plugins
{
    [Info("AmusementRides", "Nikedemos", VERSION)]
    [Description("I want to go on something more thrilling than MR BONES WILD RIDE")]

    public class AmusementRides : RustPlugin
    {
        [PluginReference]
        private Plugin SignArtist, TapeLibrary;

        public class AmusementBehaviour : MonoBehaviour
        {
            //TODO: Common updateRate / currentTime based updates
        }

        public class AmusementRidesPlugin : RustPlugin
        {
            public const string PREFAB_TC = "assets/prefabs/deployable/tool cupboard/cupboard.tool.deployed.prefab";
            public const string PREFAB_SMALL_BOX = "assets/prefabs/deployable/woodenbox/woodbox_deployed.prefab";
            public const string PREFAB_LARGE_BOX = "assets/prefabs/deployable/large wood storage/box.wooden.large.prefab";
            public const string PREFAB_LOCK_KEY = "assets/prefabs/locks/keylock/lock.key.prefab";
            public const string PREFAB_LOCK_CODE = "assets/prefabs/locks/keypad/lock.code.prefab";

            public const string PREFAB_VENDING_MACHINE = "assets/prefabs/deployable/vendingmachine/vendingmachine.deployed.prefab";
            public const string PREFAB_BOOMBOX = "assets/prefabs/voiceaudio/boombox/boombox.deployed.prefab";
            public const string PREFAB_SPEAKER = "assets/prefabs/voiceaudio/hornspeaker/connectedspeaker.deployed.static.prefab";
            public const string PREFAB_GENERATOR = "assets/prefabs/deployable/playerioents/generators/generator.small.prefab";
            public const string PREFAB_SPLITTER = "assets/prefabs/io/electric/switches/splitter.prefab";

            public const string PREFAB_BUTTON_CABLE = "assets/prefabs/io/electric/switches/pressbutton/pressbutton.prefab";
            public const string PREFAB_BUTTON = "assets/prefabs/deployable/playerioents/button/button.prefab";



            public const string PREFAB_LADDER = "assets/prefabs/building/ladder.wall.wood/ladder.wooden.wall.prefab";
            public const string PREFAB_MAILBOX = "assets/prefabs/deployable/mailbox/mailbox.deployed.prefab";
            public const string PREFAB_RUG = "assets/prefabs/deployable/rug/rug.deployed.prefab";

            public const string PREFAB_FLOOR = "assets/prefabs/building core/floor/floor.prefab";
            public const string PREFAB_FLOOR_FRAME = "assets/prefabs/building core/floor.frame/floor.frame.prefab";
            public const string PREFAB_FLOOR_TRIANGLE = "assets/prefabs/building core/floor.triangle/floor.triangle.prefab";
            public const string PREFAB_WALL_FRAME = "assets/prefabs/building core/wall.frame/wall.frame.prefab";
            public const string PREFAB_WALL_WINDOW = "assets/prefabs/building core/wall.window/wall.window.prefab";
            public const string PREFAB_ROOF_TRIANGLE = "assets/prefabs/building core/roof.triangle/roof.triangle.prefab";
            public const string PREFAB_ROOF = "assets/prefabs/building core/roof/roof.prefab";
            public const string PREFAB_DOORWAY = "assets/prefabs/building core/wall.doorway/wall.doorway.prefab";
            public const string PREFAB_STEPS = "assets/prefabs/building core/foundation.steps/foundation.steps.prefab";

            public const string PREFAB_FOUNDATION = "assets/prefabs/building core/foundation/foundation.prefab";
            public const string PREFAB_FOUNDATION_TRIANGLE = "assets/prefabs/building core/foundation.triangle/foundation.triangle.prefab";

            public const string PREFAB_WALL_LOW = "assets/prefabs/building core/wall.low/wall.low.prefab";
            public const string PREFAB_RAMP = "assets/prefabs/building core/ramp/ramp.prefab";
            public const string PREFAB_WALL = "assets/prefabs/building core/wall/wall.prefab";

            public const string PREFAB_SHOP_FRONT = "assets/prefabs/building/wall.frame.shopfront/wall.frame.shopfront.prefab";
            public const string PREFAB_GRILL_FLOOR = "assets/prefabs/building/floor.grill/floor.grill.prefab";
            public const string PREFAB_DOOR_WOODEN_SINGLE = "assets/prefabs/building/door.hinged/door.hinged.wood.prefab";
            public const string PREFAB_DOOR_METAL_DOUBLE = "assets/prefabs/building/door.double.hinged/door.double.hinged.metal.prefab";
            public const string PREFAB_DOOR_METAL_SINGLE = "assets/prefabs/building/door.hinged/door.hinged.metal.prefab";
            public const string PREFAB_WINDOW_SHUTTERS = "assets/prefabs/building/wall.window.shutter/shutter.wood.a.prefab";

            public const string PREFAB_XMAS_LIGHTS = "assets/prefabs/misc/xmas/christmas_lights/xmas.lightstring.deployed.prefab";

            public const string PREFAB_REFINERY = "assets/prefabs/deployable/oil refinery/refinery_small_deployed.prefab";
            public const string PREFAB_SIRENLIGHT = "assets/prefabs/deployable/playerioents/lights/sirenlight/electric.sirenlight.deployed.prefab";
            public const string PREFAB_FIREWORK = "assets/prefabs/deployable/fireworks/volcanofirework.prefab";

            public const string PREFAB_CHAIR = "assets/prefabs/deployable/chair/chair.deployed.prefab";

            public const string PREFAB_DIESEL_BARREL = "assets/prefabs/resource/diesel barrel/diesel_barrel_world.prefab";
            public const string PREFAB_OIL_BARREL = "assets/bundled/prefabs/radtown/oil_barrel.prefab";
            public const string PREFAB_METAL_BARRICADE = "assets/prefabs/deployable/barricades/barricade.metal.prefab";

            public const string PREFAB_PICTUREFRAME_XXL = "assets/prefabs/deployable/signs/sign.pictureframe.xxl.prefab";
            public const string PREFAB_PICTUREFRAME_XL = "assets/prefabs/deployable/signs/sign.pictureframe.xl.prefab";
            public const string PREFAB_SIGN_TALL = "assets/prefabs/deployable/signs/sign.pictureframe.tall.prefab";
            public const string PREFAB_SIGN_WOODEN_MEDIUM = "assets/prefabs/deployable/signs/sign.medium.wood.prefab";
            public const string PREFAB_SIGN_WOODEN_LARGE = "assets/prefabs/deployable/signs/sign.large.wood.prefab";
            public const string PREFAB_SIGN_WOODEN_HUGE = "assets/prefabs/deployable/signs/sign.huge.wood.prefab";
            public const string PREFAB_SIGN_WOODEN_SMALL= "assets/prefabs/deployable/signs/sign.small.wood.prefab";
            public const string PREFAB_FOG_MACHINE = "assets/content/props/fog machine/fogmachine.prefab";

            public const string PREFAB_SHOTGUN_TRAP = "assets/prefabs/deployable/single shot trap/guntrap.deployed.prefab";

            public const string PREFAB_MINICOPTER = "assets/content/vehicles/minicopter/minicopter.entity.prefab";

            public const string PREFAB_PLAYER = "assets/prefabs/player/player.prefab";

            public const string PREFAB_HORSE = "assets/rust.ai/nextai/testridablehorse.prefab";
            public const string PREFAB_HORSE_CORPSE = "assets/rust.ai/agents/horse/horse.corpse.prefab";

            public const string PREFAB_WORKBENCH_LEVEL1_DEPLOYED = "assets/prefabs/deployable/tier 1 workbench/workbench1.deployed.prefab";
            public const string PREFAB_WORKBENCH_LEVEL2_DEPLOYED = "assets/prefabs/deployable/tier 2 workbench/workbench2.deployed.prefab";
            public const string PREFAB_WORKBENCH_LEVEL3_DEPLOYED = "assets/prefabs/deployable/tier 3 workbench/workbench3.deployed.prefab";

            public const string PREFAB_WORKBENCH_LEVEL1_STATIC = "assets/bundled/prefabs/static/workbench1.static.prefab";
            public const string PREFAB_WORKBENCH_LEVEL2_STATIC = "assets/bundled/prefabs/static/workbench2.static.prefab";
            public const string PREFAB_WORKBENCH_LEVEL3_STATIC = "assets/bundled/prefabs/static/workbench3.static.prefab";

            public const string PREFAB_SCRAPHELI_GIBS = "assets/content/vehicles/scrap heli carrier/servergibs_scraptransport.prefab";

            public const string PREFAB_SCRAPHELI_GIBS_PART_38 = "ScrapHeliGibs_Part038";

            public const int ITEM_LOCK_KEY = -850982208;
            public const int ITEM_REFINERY = -1293296287;
            public const int ITEM_TC = -97956382;
            public const int ITEM_LARGE_BOX = 833533164;
            public const int ITEM_SMALL_BOX = -180129657;
            public const int ITEM_PITCHFORK = 1090916276;
            public const int ITEM_SPRING = -1021495308;
            public const int ITEM_SALVAGED_HAMMER = -1506397857;
            public const int ITEM_METAL_PIPE = 95950017;

            public const int ITEM_WOOD = -151838493;
            public const int ITEM_FRAGS = 69511070;
            public const int ITEM_STONES = 2099697608;
            public const int ITEM_CLOTH = 858312878;
            public const int ITEM_SCRAP = -932201673;
            public const int ITEM_LOWGRADE = -946369541;

            public const int ITEM_SEWING_KIT = 1234880403;
            public const int ITEM_SICKLE = -1368584029;
            public const int ITEM_SNOWBALL = -363689972;

            public const ulong SKIN_CHAIR_RED = 2243574331;
            public const ulong SKIN_CHAIR_YELLOW = 2243575528;
            public const ulong SKIN_CHAIR_GREEN = 2243575827;
            public const ulong SKIN_CHAIR_CYAN = 2243576358;
            public const ulong SKIN_CHAIR_BLUE = 2243576743;
            public const ulong SKIN_CHAIR_MAGENTA = 2243577262;

            public Dictionary<string, RideDefinition> rideDefinitions = new Dictionary<string, RideDefinition>();
            public Dictionary<string, RideHandler> rideHandlers = new Dictionary<string, RideHandler>();

            public static AmusementRides AmusementInstance;

            public const string MERRY_RADIO_URL = "http://80.2.179.204:1337/stream";

            public static readonly string[] BUNDLED_OGG_URLS = new string[]
            {
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119417238736917/babys1stsynthwave.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119417637187604/bluegras.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119417930780702/brokeback.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119418325061724/denseintenseloop16.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119418744483891/ferriswheel.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119419084214272/greengras.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119419411386498/mutkanto.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119419704979507/psyloop.ogg",
                "https://cdn.discordapp.com/attachments/771376962643034143/1080119420069875793/slowspyk.ogg",
            };

            [PluginReference]
            private AmusementRides AmusementRides; //has to be private

            public string AddRideDefinition(RideDefinition definition)
            {
                rideDefinitions.Add(definition.rideName, definition);

                return definition.rideName;
            }
            public string AddRideHandler(RideHandler handler)
            {
                rideHandlers.Add(handler.name, handler);

                return handler.name;
            }

            void Init()
            {
                InitStuff();
            }

            void Unload()
            {
                UnloadStuff();
            }

            void OnServerInitialized()
            {
                InitializedStuff();
            }

            public virtual void InitializedStuff()
            {
                AddRideHandlers();
                RegisterHandlers();

                AddRideMusics();

                AddRideDefinitions();

                RegisterDefinitions();

            }

            public virtual void InitStuff()
            {
                AmusementInstance = Instance;
            }

            public virtual void UnloadStuff()
            {
                foreach (var definition in rideDefinitions)
                {
                    Instance.DefinitionUnregister(this, definition.Key);
                }

                foreach (var handler in rideHandlers)
                {
                    Instance.HandlerUnregister(this, handler.Key);
                }
            }

            public virtual void AddRideHandlers()
            {

            }

            public virtual void AddRideDefinitions()
            {

            }

            public virtual void AddRideMusics()
            {

            }

            public void RegisterDefinitions()
            {
                foreach (var definition in rideDefinitions)
                {
                    AmusementInstance.DefinitionRegister(this, definition.Value);
                }
            }

            public void RegisterHandlers()
            {
                foreach (var handler in rideHandlers)
                {
                    AmusementInstance.HandlerRegister(this, handler.Value);
                }
            }
        }

        public static readonly ulong SteamIconID = 0;
        public static AmusementRides Instance;
        public static LayerMask CollisionLayer = LayerMask.GetMask("Tree", "Debris", "Clutter", "Default", "Construction", "Deployed");

        public static readonly string PERMISSION_ADMIN = "amusementrides.admin";
        public static readonly string PERMISSION_VIP = "amusementrides.vip";
        public static readonly string PERMISSION_RIDE = "amusementrides.ride";
        public static readonly string PERMISSION_OPERATE = "amusementrides.operate";
        public static readonly string PERMISSION_CRAFT_DEPLOY_PICKUP = "amusementrides.craft.deploy.pickup";

        public static readonly int FLAG_INTERNAL = 0x200000;

        public static int layerMaskPlayer = LayerMask.GetMask("Player (Server)");

        /*
        public static readonly Dictionary<string, string> ThirdPersonToFirst = new Dictionary<string, string>
        {
            ["was"] = "were",
            ["has"] = "have",
            ["is"] = "are",
            ["does"] = "do",

            ["wasn't"] = "weren't",
            ["hasn't"] = "haven't",
            ["isn't"] = "aren't",
            ["doesn't"] = "don't",
        };*/

        public static Dictionary<string, string> Fx = new Dictionary<string, string>
        {
            ["pumpUp"] = "assets/bundled/prefabs/fx/oiljack/pump_up.prefab",
            ["pumpDown"] = "assets/bundled/prefabs/fx/oiljack/pump_down.prefab",

            ["pumpUp2"] = "assets/bundled/prefabs/fx/well/pump_up.prefab",
            ["pumpDown2"] = "assets/bundled/prefabs/fx/well/pump_down.prefab",
            ["purchase"] = "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab",
            ["vomit"] = "assets/bundled/prefabs/fx/gestures/drink_vomit.prefab",

            ["flesh"] = "assets/bundled/prefabs/fx/impacts/blunt/flesh/fleshbloodimpact.prefab",
            ["fall"] = "assets/bundled/prefabs/fx/player/fall-damage.prefab",
            ["ground"] = "assets/bundled/prefabs/fx/player/groundfall.prefab",
            ["gutshot"] = "assets/bundled/prefabs/fx/player/gutshot_scream.prefab",
            ["scream"] = "assets/bundled/prefabs/fx/player/beartrap_scream.prefab"

        };


        public static readonly List<string> deployTargetNames = new List<string>
        {
            "box.wooden",
            "box.wooden.large",
            "cupboard.tool",
            "door.double.hinged.metal",
            "door.double.hinged.toptier",
            "door.double.hinged.wood",
            "door.hinged.metal",
            "door.hinged.toptier",
            "door.hinged.wood",
            "floor.ladder.hatch",
            "fridge",
            "gates.external.high.stone",
            "gates.external.high.wood",
            "locker",
            "wall.frame.garagedoor",
            "wall.frame.cell.gate",
            "wall.frame.fence.gate",
            "wall.frame.shopfront",
        };

        public const string VERSION = "0.9.8";
        #region CONFIG
        public ConfigData configData;

        protected override void LoadDefaultConfig()
        {
            PrintWarning("Generating default config...");
            configData = new ConfigData();
            SaveConfigData();
        }
        private void ProcessConfigData()
        {
            if (configData.version != VERSION)
            {
                Tuple<int, int, int> tuplePrevious;
                Tuple<int, int, int> tupleCurrent;

                var versionFromConfig = configData.version;

                //is the version older than 0.9.3?
                if (CompareVersions(versionFromConfig, "0.9.8", out tuplePrevious, out tupleCurrent) == VersionComparisonResult.PreviousIsOlder)
                {
                    Instance.PrintError($"\n\nMusical Update: Backed up your previous config as /oxide/config/AmusementRides.json.OLD and generated default config.\n\n");

                    SaveConfigData($"{Config.Filename}.OLD");

                    storedData = new StoredData();

                    configData.version = VERSION;
                    Instance.PrintWarning($"\n\nYou have succesfully updated from {versionFromConfig} to {VERSION}\n");
                    SaveConfigData();
                }


            }
        }

        private void LoadConfigData()
        {
            PrintWarning("Loading configuration file...");
            try
            {
                configData = Config.ReadObject<ConfigData>();
            }
            catch
            {
                configData = new ConfigData();
            }


            //SaveConfigData();
        }

        private void SaveConfigData(string customFilename = null)
        {
            Config.WriteObject(configData, true, customFilename);
        }

        public class DefinitionOverride
        {
            public Dictionary<int, Dictionary<int, ulong>> specialGroupSkins;

            public Dictionary<int, Dictionary<int, string>> specialSignData;
            public float minWaitTime = 0F;

            public string rideDescription;
            public string rideImage;

            public Dictionary<int, int> rideCost;
            
            public int worbenchLevel;

            public List<string> assignedMusic;
        }

        public class ConfigData
        {
            public string version = VERSION;

            public float minimumRideSpacing = 20F;
            public bool allowMusic = true;

            public bool enableCrafting = true;
            public bool removeRidesOnMapWipe = true;

            public bool addMerryGoRadioToBoomboxes = true;

            public Dictionary<string, DefinitionOverride> definitionCustomizations = new Dictionary<string, DefinitionOverride>();
        }

        #endregion

        #region LANG
        //pre 0.9.3 messages
        public const string MSG_LANDING_SURVIVED_5 = "LandingMessageSurvived5";
        public const string MSG_LANDING_SURVIVED_10 = "LandingMessageSurvived10";
        public const string MSG_LANDING_SURVIVED_25 = "LandingMessageSurvived25";
        public const string MSG_LANDING_SURVIVED_50 = "LandingMessageSurvived50";
        public const string MSG_LANDING_SURVIVED_90 = "LandingMessageSurvived90";
        public const string MSG_LANDING_SURVIVED_100 = "LandingMessageSurvived100";
        public const string MSG_LANDING_SURVIVED_MAX = "LandingMessageSurvivedMax";

        public const string MSG_LANDING_DIED_5 = "LandingMessageDied5";
        public const string MSG_LANDING_DIED_10 = "LandingMessageDied10";
        public const string MSG_LANDING_DIED_25 = "LandingMessageDied25";
        public const string MSG_LANDING_DIED_50 = "LandingMessageDied50";
        public const string MSG_LANDING_DIED_90 = "LandingMessageDied90";
        public const string MSG_LANDING_DIED_100 = "LandingMessageDied100";
        public const string MSG_LANDING_DIED_150 = "LandingMessageDied150";
        public const string MSG_LANDING_DIED_250 = "LandingMessageDied250";
        public const string MSG_LANDING_DIED_MAX = "LandingMessageDiedMax";

        //post 0.9.3 messages
        public const string MSG_LANDING_FULL_STATS = nameof(MSG_LANDING_FULL_STATS);

        public const string MSG_MESSAGE_FORMAT = nameof(MSG_MESSAGE_FORMAT);
        public const string MSG_CANT_CONNECT_DISCONNECT_WIRES = nameof(MSG_CANT_CONNECT_DISCONNECT_WIRES);
        public const string MSG_CAN_ONLY_PLACE_ON_SINGLE_FOUNDATION = nameof(MSG_CAN_ONLY_PLACE_ON_SINGLE_FOUNDATION);
        public const string MSG_TOO_CLOSE_TO_ANOTHER_RIDE = nameof(MSG_TOO_CLOSE_TO_ANOTHER_RIDE);

        public const string MSG_CANT_DEMOLISH_RIDE_ENTITY = nameof(MSG_CANT_DEMOLISH_RIDE_ENTITY);
        public const string MSG_CANT_ROTATE_RIDE_ENTITY = nameof(MSG_CANT_ROTATE_RIDE_ENTITY);
        public const string MSG_CANT_DEPLOY_RIDES_IN_THE_WAY = nameof(MSG_CANT_DEPLOY_RIDES_IN_THE_WAY);
        public const string MSG_CANT_DEPLOY_THINGS_ON_RIDE = nameof(MSG_CANT_DEPLOY_THINGS_ON_RIDE);
        public const string MSG_CANT_BUILD_OR_DEPLOY_ON_RIDE = nameof(MSG_CANT_BUILD_OR_DEPLOY_ON_RIDE);
        public const string MSG_CANT_UPGRADE_RIDE_STRUCTURES = nameof(MSG_CANT_UPGRADE_RIDE_STRUCTURES);

        public const string MSG_ONLY_AUTHORISED_CAN_USE_RIDE = nameof(MSG_ONLY_AUTHORISED_CAN_USE_RIDE);
        public const string MSG_ONLY_AUTHORISED_CAN_CONTROL = nameof(MSG_ONLY_AUTHORISED_CAN_CONTROL);

        public const string MSG_CANT_PICKUP_ADMIN_RIDE = nameof(MSG_CANT_PICKUP_ADMIN_RIDE);
        public const string MSG_PICKUP_CONTAINER_NEEDS_TO_BE_EMPTY = nameof(MSG_PICKUP_CONTAINER_NEEDS_TO_BE_EMPTY);
        public const string MSG_PICKUP_LOCK_NEEDS_TO_BE_REMOVED = nameof(MSG_PICKUP_LOCK_NEEDS_TO_BE_REMOVED);
        public const string MSG_PICKUP_CONTAINER_NEEDS_REPAIR = nameof(MSG_PICKUP_CONTAINER_NEEDS_REPAIR);
        public const string MSG_CANT_GET_ON_RIDE_NOT_OVER = nameof(MSG_CANT_GET_ON_RIDE_NOT_OVER);
        public const string MSG_ADMISSION_FEE_FREE_OF_CHARGE_FORMATTED = nameof(MSG_ADMISSION_FEE_FREE_OF_CHARGE_FORMATTED);
        public const string MSG_ADMISSION_FEE_CURRENCY_FORMATTED = nameof(MSG_ADMISSION_FEE_CURRENCY_FORMATTED);
        public const string MSG_ADMISSION_FEE_FREE_OF_CHARGE_UNFORMATTED = nameof(MSG_ADMISSION_FEE_FREE_OF_CHARGE_UNFORMATTED);
        public const string MSG_ADMISSION_FEE_CURRENCY_UNFORMATTED = nameof(MSG_ADMISSION_FEE_CURRENCY_UNFORMATTED);
        public const string MSG_CANT_GET_ON_NEED_CURRENCY = nameof(MSG_CANT_GET_ON_NEED_CURRENCY);

        public const string MSG_BROADCAST_PLAYER_BACK_ON_RIDE = nameof(MSG_BROADCAST_PLAYER_BACK_ON_RIDE);
        public const string MSG_BROADCAST_RIDE_NOT_STARTING = nameof(MSG_BROADCAST_RIDE_NOT_STARTING);
        public const string MSG_BROADCAST_RIDE_HAS_STARTED = nameof(MSG_BROADCAST_RIDE_HAS_STARTED);
        public const string MSG_BROADCAST_RIDE_IS_OVER = nameof(MSG_BROADCAST_RIDE_IS_OVER);
        public const string MSG_BROADCAST_RIDE_STARTING_SOON = nameof(MSG_BROADCAST_RIDE_STARTING_SOON);

        public const string MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_CANT_JOIN_LATE = nameof(MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_CANT_JOIN_LATE);
        public const string MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_AND_CAN_JOIN_LATE = nameof(MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_AND_CAN_JOIN_LATE);
        public const string MSG_BROADCAST_NOBODY_LEFT_STOPPING_NOW = nameof(MSG_BROADCAST_NOBODY_LEFT_STOPPING_NOW);
        public const string MSG_BROADCAST_NOBODY_LEFT_STOPPING_SOON = nameof(MSG_BROADCAST_NOBODY_LEFT_STOPPING_SOON);
        public const string MSG_BROADCAST_PLAYER_GOT_OFF_DISMOUNTED = nameof(MSG_BROADCAST_PLAYER_GOT_OFF_DISMOUNTED);

        public const string MSG_BROADCAST_PLAYER_GOT_OFF_DIED = nameof(MSG_BROADCAST_PLAYER_GOT_OFF_DIED);
        public const string MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED = nameof(MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED);
        public const string MSG_BROADCAST_PLAYER_GOT_OFF_COLD_FEET = nameof(MSG_BROADCAST_PLAYER_GOT_OFF_COLD_FEET);
        public const string MSG_BROADCAST_PLAYER_GOT_OFF_DIED_BEFORE_RIDE_BEGAN = nameof(MSG_BROADCAST_PLAYER_GOT_OFF_DIED_BEFORE_RIDE_BEGAN);
        public const string MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED_BEFORE_RIDE_BEGAN = nameof(MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED_BEFORE_RIDE_BEGAN);
        public const string MSG_BROADCAST_RIDE_REMOVED_BY_ADMIN = nameof(MSG_BROADCAST_RIDE_REMOVED_BY_ADMIN);
        public const string MSG_BROADCAST_RIDE_REMOVED_BY_RAIDING = nameof(MSG_BROADCAST_RIDE_REMOVED_BY_RAIDING);
        public const string MSG_BROADCAST_RIDE_LOCK_DESTROYED = nameof(MSG_BROADCAST_RIDE_LOCK_DESTROYED);
        public const string MSG_ADMISSION_RIDE_FREE_OF_CHARGE_PLAYER_AUTHORISED = nameof(MSG_ADMISSION_RIDE_FREE_OF_CHARGE_PLAYER_AUTHORISED);
        public const string MSG_ADMISSION_RIDE_FREE_OF_CHARGE_NO_FEE = nameof(MSG_ADMISSION_RIDE_FREE_OF_CHARGE_NO_FEE);

        public const string MSG_ADMISSION_PLAYER_PAID_CURRENCY = nameof(MSG_ADMISSION_PLAYER_PAID_CURRENCY);

        public const string MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME = nameof(MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME);
        public const string MSG_COMMAND_GIVE_GAVE_RIDE_TO_YOURSELF = nameof(MSG_COMMAND_GIVE_GAVE_RIDE_TO_YOURSELF);
        public const string MSG_COMMAND_GAVE_RIDE_TO_PLAYER = nameof(MSG_COMMAND_GAVE_RIDE_TO_PLAYER);
        public const string MSG_COMMAND_GIVE_GAVE_RIDE_RECEIVED_FROM_ADMIN = nameof(MSG_COMMAND_GIVE_GAVE_RIDE_RECEIVED_FROM_ADMIN);

        public const string MSG_COMMAND_SPAWN_NEW_RIDE_CREATED = nameof(MSG_COMMAND_SPAWN_NEW_RIDE_CREATED);
        public const string MSG_ONLY_ADMINS_CAN_MANAGE_THIS_RIDE = nameof(MSG_ONLY_ADMINS_CAN_MANAGE_THIS_RIDE);

        public const string MSG_ONLY_ADMINS_CAN_CHANGE_THIS_SETTING = nameof(MSG_ONLY_ADMINS_CAN_CHANGE_THIS_SETTING);
        public const string MSG_UI_RIDE_NAME = nameof(MSG_UI_RIDE_NAME);
        public const string MSG_UI_ONLY_AUTHORISED_CAN_RIDE = nameof(MSG_UI_ONLY_AUTHORISED_CAN_RIDE);
        public const string MSG_UI_EVERYONE_CAN_RIDE = nameof(MSG_UI_EVERYONE_CAN_RIDE);
        public const string MSG_UI_ONLY_AUTHORISED_CAN_CONTROL = nameof(MSG_UI_ONLY_AUTHORISED_CAN_CONTROL);
        public const string MSG_UI_EVERYONE_CAN_CONTROL = nameof(MSG_UI_EVERYONE_CAN_CONTROL);
        public const string MSG_UI_AUTHORISED_FREE = nameof(MSG_UI_AUTHORISED_FREE);
        public const string MSG_UI_AUTHORISED_CHARGE = nameof(MSG_UI_AUTHORISED_CHARGE);
        public const string MSG_UI_BROADCAST_POSITION = nameof(MSG_UI_BROADCAST_POSITION);
        public const string MSG_UI_DONT_BROADCAST_POSITION = nameof(MSG_UI_DONT_BROADCAST_POSITION);

        public const string MSG_UI_ADMIN_RIDE = nameof(MSG_UI_ADMIN_RIDE);
        public const string MSG_UI_NORMAL_RIDE = nameof(MSG_UI_NORMAL_RIDE);
        public const string MSG_UI_ADMISSION_ITEM = nameof(MSG_UI_ADMISSION_ITEM);
        public const string MSG_UI_ADMISSION_AMOUNT = nameof(MSG_UI_ADMISSION_AMOUNT);
        public const string MSG_UI_MUSIC_AUTO = nameof(MSG_UI_MUSIC_AUTO);
        public const string MSG_UI_MUSIC_MANUAL = nameof(MSG_UI_MUSIC_MANUAL);
        public const string MSG_UI_BROADCAST_CHAT_MESSAGES = nameof(MSG_UI_BROADCAST_CHAT_MESSAGES);
        public const string MSG_UI_DONT_BROADCAST_CHAT_MESSAGES = nameof(MSG_UI_DONT_BROADCAST_CHAT_MESSAGES);

        public const string MSG_UI_WB_PLAYS_MUSIC = nameof(MSG_UI_WB_PLAYS_MUSIC);
        public const string MSG_UI_WB_HAS_TC = nameof(MSG_UI_WB_HAS_TC);
        public const string MSG_UI_WB_LEVEL_REQUIRED = nameof(MSG_UI_WB_LEVEL_REQUIRED);
        public const string MSG_UI_WB_CANT_AFFORD_TO_CRAFT = nameof(MSG_UI_WB_CANT_AFFORD_TO_CRAFT);
        public const string MSG_UI_WB_READY_TO_CRAFT = nameof(MSG_UI_WB_READY_TO_CRAFT);

        public const string MSG_PLACEHOLDER_74 = nameof(MSG_PLACEHOLDER_74);
        public const string MSG_PLACEHOLDER_75 = nameof(MSG_PLACEHOLDER_75);
        public const string MSG_PLACEHOLDER_76 = nameof(MSG_PLACEHOLDER_76);
        public const string MSG_PLACEHOLDER_77 = nameof(MSG_PLACEHOLDER_77);
        public const string MSG_PLACEHOLDER_78 = nameof(MSG_PLACEHOLDER_78);
        public const string MSG_PLACEHOLDER_79 = nameof(MSG_PLACEHOLDER_79);
        public const string MSG_PLACEHOLDER_80 = nameof(MSG_PLACEHOLDER_80);

        private static Dictionary<string, string> LangMessages = new Dictionary<string, string>
        {
            [MSG_LANDING_SURVIVED_5] = "{0} came out virtually unscathed!",
            [MSG_LANDING_SURVIVED_10] = "{0} ended up with a few bruises.",
            [MSG_LANDING_SURVIVED_25] = "{0} suffered some minor injuries.",
            [MSG_LANDING_SURVIVED_50] = "{0} suffered severe injuries.",
            [MSG_LANDING_SURVIVED_90] = "{0} suffered fatal injuries.",
            [MSG_LANDING_SURVIVED_100] = "{0} nearly died from the injuries.",
            [MSG_LANDING_SURVIVED_MAX] = "{0} survived a literally IMPOSSIBLE amount of damage.",

            [MSG_LANDING_DIED_5] = "{0} died from stupidity!",
            [MSG_LANDING_DIED_10] = "{0} died from a boobo or and/or an ouchie.",
            [MSG_LANDING_DIED_25] = "{0} died from some minor injuries.",
            [MSG_LANDING_DIED_50] = "{0} died from severe injuries.",
            [MSG_LANDING_DIED_90] = "{0} died from predictably fatal injuries.",
            [MSG_LANDING_DIED_100] = "{0} died from practically unsurvivable injuries.",
            [MSG_LANDING_DIED_150] = "{0} died from humorously over-the-top injuries.",
            [MSG_LANDING_DIED_250] = "{0} died from absolutely hilarious injuries.",
            [MSG_LANDING_DIED_MAX] = "{0} died from shockingly grotesque injuries.",

            [MSG_LANDING_FULL_STATS] = "Launch velocity: {0} m/s ({1} MPH)\nLaunch height: {2} m\nFirst impact velocity: {3} m/s ({4} MPH)\nAir time: {5} s\nAir distance: {6} m\nDamage taken: {7} HP (= {8} J)\nTotal travel time: {9} s\nTotal travel distance: {10} m",

            [MSG_MESSAGE_FORMAT] = "<color=yellow>[{0}]</color> {1}",
            [MSG_CANT_CONNECT_DISCONNECT_WIRES] = "You can't connect/disconnect wires on entities belonging to Rides",
            [MSG_CAN_ONLY_PLACE_ON_SINGLE_FOUNDATION] = "You can only place {0} on a single foundation with nothing else around it.",
            [MSG_TOO_CLOSE_TO_ANOTHER_RIDE] = "You are too close to another ride, <color=yellow>{0}</color> ({1} m. away).\nTry at least {2} meters away.",

            [MSG_CANT_DEMOLISH_RIDE_ENTITY] = "You cannot demolish structures that belong to the ride.",
            [MSG_CANT_ROTATE_RIDE_ENTITY] = "You cannot rotate structures that belong to the ride.",

            [MSG_CANT_DEPLOY_RIDES_IN_THE_WAY] = "You cannot deploy the ride here, there's some entities in the way: ",
            [MSG_CANT_DEPLOY_THINGS_ON_RIDE] = "You cannot build or deploy things on this ride.",
            [MSG_CANT_BUILD_OR_DEPLOY_ON_RIDE] = "You cannot build or deploy things on this ride.",
            [MSG_CANT_UPGRADE_RIDE_STRUCTURES] = "You cannot upgrade structures that belong to the ride.",

            [MSG_ONLY_AUTHORISED_CAN_USE_RIDE] = "Only authorized players can use this ride.",
            [MSG_ONLY_AUTHORISED_CAN_CONTROL] = "Only authorized players can control this ride.",

            [MSG_CANT_PICKUP_ADMIN_RIDE] = "This is an admin ride, you cannot pick it up.",
            [MSG_PICKUP_CONTAINER_NEEDS_TO_BE_EMPTY] = "You have to take everything out of the ride's Container before picking the ride up.",
            [MSG_PICKUP_LOCK_NEEDS_TO_BE_REMOVED] = "You have to take off the lock on the ride's Container before picking the ride up.",
            [MSG_PICKUP_CONTAINER_NEEDS_REPAIR] = "You have to repair the ride's Container to full health before picking the ride up.",
            [MSG_CANT_GET_ON_RIDE_NOT_OVER] = "Please wait till the ride's over.",
            [MSG_ADMISSION_FEE_FREE_OF_CHARGE_FORMATTED] = "<color=green>FREE OF CHARGE</color>",
            [MSG_ADMISSION_FEE_CURRENCY_FORMATTED] = "<color=white>{0}</color> <color=orange>{1}</color>",
            [MSG_ADMISSION_FEE_FREE_OF_CHARGE_UNFORMATTED] = "FREE OF CHARGE",
            [MSG_ADMISSION_FEE_CURRENCY_UNFORMATTED] = "{0} {1}",
            [MSG_CANT_GET_ON_NEED_CURRENCY] = "Sorry, you need {0} to get on!",

            [MSG_BROADCAST_PLAYER_BACK_ON_RIDE] = "{0} is back on the ride? Gotta make your mind up!",
            [MSG_BROADCAST_RIDE_NOT_STARTING] = "The ride is not starting, everyone got off before the countdown.",
            [MSG_BROADCAST_RIDE_HAS_STARTED] = "The ride has started!",
            [MSG_BROADCAST_RIDE_IS_OVER] = "The ride is over!",
            [MSG_BROADCAST_RIDE_STARTING_SOON] = "{0} hopped on. Ride starting in {1} seconds!",

            [MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_CANT_JOIN_LATE] = "{0} managed to hop on in time, there's {1} seats left.",
            [MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_AND_CAN_JOIN_LATE] = "{0} got on the ride! Come and join them!",
            [MSG_BROADCAST_NOBODY_LEFT_STOPPING_NOW] = "Nobody left on the ride, coming to a stop now.",
            [MSG_BROADCAST_NOBODY_LEFT_STOPPING_SOON] = "Nobody left on the ride, coming to a stop soon.",
            [MSG_BROADCAST_PLAYER_GOT_OFF_DISMOUNTED] = "{0} couldn't take it any more and got off!",

            [MSG_BROADCAST_PLAYER_GOT_OFF_DIED] = "{0} couldn't take it anymore and died on the ride!",
            [MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED] = "{0} couldn't take it anymore and disconnected!",
            [MSG_BROADCAST_PLAYER_GOT_OFF_COLD_FEET] = "{0} had cold feet and got off!",
            [MSG_BROADCAST_PLAYER_GOT_OFF_DIED_BEFORE_RIDE_BEGAN] = "{0} died before the ride even began!",
            [MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED_BEFORE_RIDE_BEGAN] = "{0} had cold feet and disconnected before the ride even began!",
            [MSG_BROADCAST_RIDE_REMOVED_BY_ADMIN] = "{0} has been removed by an admin.",
            [MSG_BROADCAST_RIDE_REMOVED_BY_RAIDING] = "Oh no. {0} has been removed (most likely because it was raided)",
            [MSG_BROADCAST_RIDE_LOCK_DESTROYED] = "The lock on the ride's container has been destroyed!",
            [MSG_ADMISSION_RIDE_FREE_OF_CHARGE_PLAYER_AUTHORISED] = "You're authorized, so for you, the ride is free of charge!",
            [MSG_ADMISSION_RIDE_FREE_OF_CHARGE_NO_FEE] = "The ride is free of charge!",

            [MSG_ADMISSION_PLAYER_PAID_CURRENCY] = "You paid {0} to ride.",

            [MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME] = "Please provide a valid name of the ride. Try {0}.\nIf you don't see the ride listed, reload the missing ride plugins in the console.",

            [MSG_COMMAND_GIVE_GAVE_RIDE_TO_YOURSELF] = "You have given yourself a <color=yellow>{0}</color> item.",
            [MSG_COMMAND_GAVE_RIDE_TO_PLAYER] = "You have given {0} a <color=yellow>{1}</color> item.",
            [MSG_COMMAND_GIVE_GAVE_RIDE_RECEIVED_FROM_ADMIN] = "You have received a <color=yellow>{0}</color> item from an admin.",

            [MSG_COMMAND_SPAWN_NEW_RIDE_CREATED] = "New ride {0} created successfully!",
            [MSG_ONLY_ADMINS_CAN_MANAGE_THIS_RIDE] = "Only admins can manage this ride.",

            [MSG_ONLY_ADMINS_CAN_CHANGE_THIS_SETTING] = "Only admins can change this setting.",

            [MSG_UI_RIDE_NAME] = "Ride name (or DEFAULT to restore):",
            [MSG_UI_ONLY_AUTHORISED_CAN_RIDE] = "Only authorized can use this ride",
            [MSG_UI_EVERYONE_CAN_RIDE] = "Everyone can use this ride",

            [MSG_UI_ONLY_AUTHORISED_CAN_CONTROL] = "Only authorized can control this ride",
            [MSG_UI_EVERYONE_CAN_CONTROL] = "Everyone can control this ride",
            [MSG_UI_AUTHORISED_FREE] = "Authorized ride for free",
            [MSG_UI_AUTHORISED_CHARGE] = "Charge authorized too",
            [MSG_UI_BROADCAST_POSITION] = "Broadcast ride position",
            [MSG_UI_DONT_BROADCAST_POSITION] = "Don't broadcast position",

            [MSG_UI_ADMIN_RIDE] = "Admin ride (no damage, no decay, only admins can pick up/manage)",
            [MSG_UI_NORMAL_RIDE] = "Normal ride (takes damage, decays, authorized can pick up/manage)",
            [MSG_UI_ADMISSION_ITEM] = "Admission item, enter ID/name: ",
            [MSG_UI_ADMISSION_AMOUNT] = "Admission amount, 0 = free: ",
            [MSG_UI_MUSIC_AUTO] = "Music: auto start/stop from ride playlist",
            [MSG_UI_MUSIC_MANUAL] = "Music: manual start/stop with custom tapes/stations",
            [MSG_UI_BROADCAST_CHAT_MESSAGES] = "Broadcast global chat messages",
            [MSG_UI_DONT_BROADCAST_CHAT_MESSAGES] = "Don't broadcast global chat messages",

            [MSG_UI_WB_PLAYS_MUSIC] = "• plays {0} music tracks",
            [MSG_UI_WB_HAS_TC] = "• comes with a TC",

            [MSG_UI_WB_LEVEL_REQUIRED] = "WORKBENCH LV {0} REQUIRED",
            [MSG_UI_WB_CANT_AFFORD_TO_CRAFT] = "CAN'T AFFORD TO CRAFT",
            [MSG_UI_WB_READY_TO_CRAFT] = "READY TO CRAFT! CLICK HERE",
            [MSG_PLACEHOLDER_74] = "",
            [MSG_PLACEHOLDER_75] = "",
            [MSG_PLACEHOLDER_76] = "",
            [MSG_PLACEHOLDER_77] = "",
            [MSG_PLACEHOLDER_78] = "",
            [MSG_PLACEHOLDER_79] = "",
            [MSG_PLACEHOLDER_80] = "",
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


        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(LangMessages, this);
        }

        public static List<string> FxHurt = new List<string>
        {
            "flesh",
            "fall",
            "ground",
            "gutshot",
            "scream"
        };

        public const float AVERAGE_HUMAN_BODY_WEIGHT = 62F;
        public const float METERS_PER_SECOND_TO_MPH_MULTIPLIER = 2.23694F;
        //if the server is US-based, you should change it to your country's average, 80.7F (in kilograms) ;)
        //source: https://bmcpublichealth.biomedcentral.com/articles/10.1186/1471-2458-12-439

        public const float DEATH_AT_VELOCITY = 56.7961835F; //at body weight 62 KG and kinetic energy 50000 JOULES, it means death

        public const float MAX_KINETIC_ENERGY_AT_100_DAMAGE = 50000F; //50 kilojoules means 100HP damage on impact

        public static string RandomHurtFx()
        {
            return FxHurt[UnityEngine.Random.Range(0, FxHurt.Count - 1)];
        }

        public Dictionary<uint, Ride> entityToRide = new Dictionary<uint, Ride>();

        public Dictionary<string, Ride> rides = new Dictionary<string, Ride>();

        public Dictionary<string, RideDefinition> rideDefinitions = new Dictionary<string, RideDefinition>();

        public Dictionary<int, byte[]> textureDictionary = null;

        public Dictionary<ulong, string> skinToDefinition = new Dictionary<ulong, string>();

        public Dictionary<string, RideHandler> rideHandlers = new Dictionary<string, RideHandler>();

        public Dictionary<ulong, Ride> playerIDtoRide = new Dictionary<ulong, Ride>();

        public Dictionary<ulong, RideSegmentCollider> playerIDtoSegmentCollider = new Dictionary<ulong, RideSegmentCollider>();

        public Dictionary<ulong, List<Ride>> playerIDtoRidesNearby = new Dictionary<ulong, List<Ride>>();

        public bool DefinitionRegister(RustPlugin registrant, RideDefinition definition)
        {
            if (rideDefinitions.ContainsKey(definition.rideName))
            {
                Instance.PrintError($"[{registrant.Name}] [DEFINITION] ERROR: {definition.rideName} has already been registered! Skipping.");

                return false;
            }
            else
            {
                //not contains? create some defaults based on the definition.

                if (!Instance.configData.definitionCustomizations.ContainsKey(definition.rideName))
                {
                    Instance.configData.definitionCustomizations.Add(definition.rideName, new DefinitionOverride
                    {
                        rideCost = definition.rideCost,
                        rideDescription = definition.rideDescription,
                        rideImage = definition.rideImage,
                        minWaitTime = definition.minWaitTime,
                        worbenchLevel = definition.workbenchLevel,
                        specialSignData = definition.specialSignData,
                        specialGroupSkins = definition.specialGroupSkins,
                        assignedMusic = definition.assignedMusic
                    });

                    Instance.PrintWarning($"[{registrant.Name}] [DEFINITION] {definition.rideName} {definition.rideVersion} by {definition.rideAuthor} has succesfully registered its default definition customization entry in the config. Any changes made to that file will take effect next time the plugin reloads.");

                    Instance.SaveConfigData();
                }
                else //contains? well then, use those!
                {
                    //1 by 1...

                    var definitionCopy = definition.ShallowCopy();

                    try
                    {

                        var useThisDefinition = Instance.configData.definitionCustomizations[definition.rideName];

                        definition.rideDescription = useThisDefinition.rideDescription;
                        definition.rideCost = useThisDefinition.rideCost.ToDictionary(e => e.Key, e => e.Value);
                        definition.rideImage = useThisDefinition.rideImage;
                        definition.minWaitTime = useThisDefinition.minWaitTime;
                        definition.workbenchLevel = useThisDefinition.worbenchLevel;
                        definition.specialSignData = useThisDefinition.specialSignData;
                        definition.specialGroupSkins = useThisDefinition.specialGroupSkins;
                        definition.assignedMusic = useThisDefinition.assignedMusic;
                    }
                    catch
                    {
                        Instance.PrintError($"[{registrant.Name}] [DEFINITION] {definition.rideName} {definition.rideVersion} by {definition.rideAuthor} failed to apply definition customization settings. Please check your config file - are those null/invalid or are they missing quotes? Sticking with default definition settings for now.");

                        definition = definitionCopy;
                    }
                    finally
                    {
                        Instance.PrintWarning($"[{registrant.Name}] [DEFINITION] {definition.rideName} {definition.rideVersion} by {definition.rideAuthor} has succesfully applied definition customization from the config file.");
                    }

                    //check if the ride item skin is not 0!
                    if (definition.rideItemSkinID == 0)
                    {
                        Instance.PrintError($"[{registrant.Name}] [DEFINITION] ERROR: {definition.rideName} cannot have a Ride Item Skin ID set to 0! Please change it to ANYTHING else. Skipping.");
                        return false;
                    }

                    //check if another ride has already registered the same pickup skin


                    if (rideDefinitions.Where(d => d.Value.rideItemSkinID == definition.rideItemSkinID).Any())
                    {
                        Instance.PrintError($"[{registrant.Name}] [DEFINITION] ERROR: {definition.rideName} is trying to register the same Ride Item Skin ID ({definition.rideItemSkinID}) as another, already registered definition. Please change it to ANYTHING else. Skipping.");
                        return false;
                    }

                    definition.ApplyPickupDefinition();

                    skinToDefinition.Add(definition.rideItemSkinID, definition.rideName);
                    rideDefinitions.Add(definition.rideName, definition);
                    Instance.PrintWarning($"[{registrant.Name}] [DEFINITION] {definition.rideName} {definition.rideVersion} by {definition.rideAuthor} registered successfully.");

                    //create an override entry in the config if it doesn't exist
                    if (Instance.configData.definitionCustomizations == null)
                    {
                        Instance.configData.definitionCustomizations = new Dictionary<string, DefinitionOverride>();
                    }
                }




                return true;
            }
        }

        public bool DefinitionUnregister(RustPlugin registrant, string name)
        {
            if (rideDefinitions.ContainsKey(name))
            {
                Instance.PrintWarning($"[{registrant.Name}] [DEFINITION] {rideDefinitions[name].rideName} unregistered successfully.");

                skinToDefinition.Remove(rideDefinitions[name].rideItemSkinID);
                rideDefinitions.Remove(name);

                return true;
            }
            else
            {
                return false;
            }
        }

        public bool HandlerRegister(RustPlugin registrant, RideHandler handler)
        {
            if (rideHandlers.ContainsKey(handler.name))
            {
                return false;
            }
            else
            {
                rideHandlers.Add(handler.name, handler);
                Instance.PrintWarning($"[{registrant.Name}] [HANDLER] {handler.name} {handler.version} by {handler.author} registered successfully.");
                return true;
            }
        }

        public bool HandlerUnregister(RustPlugin registrant, string name)
        {
            if (rideHandlers.ContainsKey(name))
            {

                Instance.PrintWarning($"[{registrant.Name}] [HANDLER] {rideHandlers[name].name} unregistered successfully.");
                rideHandlers.Remove(name);
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool HandlerExists(string name)
        {
            return rideHandlers.ContainsKey(name);
        }

        public bool isUnloading;
        public bool isEmergencyCleanup;

        void Init()
        {
            LoadData();
            isUnloading = false;
            Instance = this;
            isEmergencyCleanup = false;
        }

        void Unload()
        {


            /*
            var check = true;

            if (rides == null)
            {
                check = false;
            }
            else
            {
                if (storedData == null)
                {
                    check = false;
                }
                else
                {
                    if (storedData.rides == null)
                    {
                        check = false;
                    }
                }
            }*/

            isUnloading = true;

            var iter = rides.Keys.ToList();

            foreach (var entry in iter)
            {
                RideRemove(entry);
            }

            foreach (var ejector in UnityEngine.Object.FindObjectsOfType<PlayerEjector>())
            {
                ejector.Eject(true);
            }

            ItemManager.DoRemoves();

            SaveData();

            if (BundledDownloadTimeoutTimer != null)
            {
                BundledDownloadTimeoutTimer.Destroy();
                BundledDownloadTimeoutTimer = null;
            }

            Instance = null;
            //storedData = null;
            isUnloading = false;
        }

        //
        void OnNewSave(string strFilename)
        {
            var tempConfig = Config.ReadObject<ConfigData>();
            if (tempConfig.removeRidesOnMapWipe)
            {
                Instance.PrintWarning("INFO: The map has been wiped! Removing all ride data. If you don't want this to happen, make an appropriate setting in the config.");
                storedData = new StoredData();
                SaveData();
            }

        }


        void OnServerSave()
        {
            timer.Once(UnityEngine.Random.Range(30F, 60F), () =>
            {
                SaveData();
            });
        }

        void OnServerInitialized(bool serverInitialized)
        {
            Instance = this;

            lang.RegisterMessages(LangMessages, this);

            isEmergencyCleanup = true;

            BaseEntity entityAsBaseEntity;
            var found = 0;

            foreach (var mono in UnityEngine.Object.FindObjectsOfType<AmusementBehaviour>())
            {
                UnityEngine.Object.Destroy(mono.gameObject);
                found++;
            }

            if (found > 0)
            {
                Instance.PrintError($"EMERGENCY CLEANUP: Found and destroyed {found} leftover MonoBehaviour gameobjects");
            }

            found = 0;

            foreach (var entity in BaseNetworkable.serverEntities)
            {
                entityAsBaseEntity = entity as BaseEntity;
                if (entityAsBaseEntity != null)
                {
                    if (/*entityAsBaseEntity.OwnerID == 1337420*/IsInternal(entityAsBaseEntity))
                    {
                        if (!entityAsBaseEntity.IsDestroyed)
                        {

                            entityAsBaseEntity.Kill(BaseNetworkable.DestroyMode.None);
                            found++;
                        }
                    }
                }
            }
            if (found > 0)
            {
                Instance.PrintError($"EMERGENCY CLEANUP: Found and killed {found} leftover entities");
            }

            isEmergencyCleanup = false;

            LoadData();

            LoadConfigData();
            ProcessConfigData();

            if (configData.enableCrafting)
            {
                foreach (var wb in BaseNetworkable.serverEntities.OfType<Workbench>())
                {
                    OnEntitySpawned(wb);
                }
            }

            // Allow other plugins a tick in case oxide calls this method after the rides are registered
            NextTick(() =>
            {
                try
                {
                    PrintWarning($"{SignArtist.Name} {SignArtist.Version} by {SignArtist.Author} is ready!");
                }
                catch
                {
                    PrintError($"\n\n\nWARNING: Sign Artist doesn't appear to be loaded. Without it, rides might appear painfully dull and/or look as if they're missing something. After you load it in, type Oxide.Reload AmusementRides\n\n\n");
                }

                bool tapeLibraryLoaded = false;

                try
                {
                    PrintWarning($"{TapeLibrary.Name} {TapeLibrary.Version} by {TapeLibrary.Author} is ready!");
                    tapeLibraryLoaded = true;
                }
                catch
                {
                    PrintError($"\n\n\nWARNING: Tape Library doesn't appear to be loaded. Without it, ride music will not work correctly. After you load it in, type Oxide.Reload AmusementRides\n\n\n");
                }

                if (!tapeLibraryLoaded)
                {
                    ProcessData();
                }
                else
                {
                    TryEnsuringOGGsDownloadedAndThenProcessData();
                }
            });

            if (!configData.addMerryGoRadioToBoomboxes)
            {
                return;
            }

            bool foundMatchingEntry = false;

            if (!BoomBox.ServerUrlList.IsNullOrEmpty())
            {
                string[] allCurrentStations = BoomBox.ServerUrlList.Split(',');

                if (allCurrentStations.Length % 2 == 0 && allCurrentStations.Length >= 2)
                {
                    for (var s = 0; s <= allCurrentStations.Length - 2; s++)
                    {
                        var stationName = allCurrentStations[s];
                        var stationURL = allCurrentStations[s + 1];

                        if (stationURL == AmusementRidesPlugin.MERRY_RADIO_URL)
                        {
                            foundMatchingEntry = true;
                            break;
                        }
                    }
                }
            }

            if (!foundMatchingEntry)
            {
                if (!BoomBox.ServerUrlList.IsNullOrEmpty())
                {
                    if (!BoomBox.ServerUrlList.EndsWith(","))
                    {
                        BoomBox.ServerUrlList += ",";
                    }
                }
                else
                {
                    BoomBox.ServerUrlList = string.Empty;
                }


                BoomBox.ServerUrlList += $"Merry-Go-Radio,{AmusementRidesPlugin.MERRY_RADIO_URL}";

                Instance.PrintWarning("INFO: Added Merry-Go-Radio URL to the BoomBox server URL list. If you don't want it, set the \"addMerryGoRadioToBoomboxes\" config value to false, reload the plugin, and clear the list by typing boombox.serverurllist \"\" in the console.");
            }
            else
            {
                Instance.PrintWarning("OK: Merry-Go-Radio URL already exists in the BoomBox server URL list. If you don't want it, set the \"addMerryGoRadioToBoomboxes\" config value to false, reload the plugin, and clear the list by typing boombox.serverurllist \"\" in the console.");
            }

        }

        void OnEntityKill(BaseEntity entity)
        {
            if (entity == null) return;
            if (entity.IsDestroyed) return;

            if (entity.net == null) return;

            Ride maybeRide;

            if (entityToRide.TryGetValue(entity.net.ID, out maybeRide))
            {
                maybeRide.handler.WhenEntityKill(maybeRide, entity);
            }
        }

        void OnEntitySpawned(Workbench workbench)
        {
            if (configData == null) return;
            if (!configData.enableCrafting) return;

            if (workbench.gameObject.GetComponent<WorkbenchHelper>() == null)
            {
                var newHelper = workbench.gameObject.AddComponent<WorkbenchHelper>();
                newHelper.Prepare(workbench);
            }
        }

        void OnSignUpdated(Signage sign, BasePlayer player, string text)
        {
            if (entityToRide.ContainsKey(sign.net.ID))
            {
                //update all signs?!
                foreach (var entry in BaseEntity.serverEntities.OfType<Signage>())
                {
                    Instance.timer.Once(0.5F, () =>
                    {
                        entry.SendNetworkUpdateImmediate();
                    });
                }
            }
        }

        void OnItemDeployed(Deployer deployer, BaseEntity entity)
        {
            if (entityToRide.ContainsKey(entity.net.ID))
            {
                entityToRide[entity.net.ID].handler.WhenItemDeployed(entityToRide[entity.net.ID], deployer, entity);
            }
        }

        object OnWireCommon(BasePlayer player, IOEntity entity1, IOEntity entity2)
        {
            if (Instance == null)
            {
                return null;
            }

            if (!BaseNetworkableEx.IsValid(entity1))
            {
                return null;
            }

            if (!BaseNetworkableEx.IsValid(entity2))
            {
                return null;
            }

            object result = null;

            if (entityToRide.ContainsKey(entity1.net.ID))
            {
                result = true;
            }
            else
            {
                if (entityToRide.ContainsKey(entity2.net.ID))
                {
                    result = true;
                }
            }

            if (result != null)
            {
                if (player != null)
                {
                    TellMessage(player, MSG(MSG_CANT_CONNECT_DISCONNECT_WIRES, player.UserIDString));
                }
            }

            return result;
        }

        object OnWireConnect(BasePlayer player, IOEntity entity1, int inputs, IOEntity entity2, int outputs)
        {
            if (Instance == null)
            {
                return null;
            }

            return OnWireCommon(player, entity1, entity2);
        }

        //flag true means inputs, flag false means outputs
        object OnWireClear(BasePlayer player, IOEntity entity1, int connecteds, IOEntity entity2, bool flag)
        {
            if (Instance == null)
            {
                return null;
            }

            return OnWireCommon(player, entity1, entity2);
        }

        void OnEntityBuilt(Planner plan, GameObject go)
        {
            var entity = go.GetComponent<BaseCombatEntity>();
            if (entity == null) return;

            if (skinToDefinition.ContainsKey(entity.skinID))
            {

                //create a ride based on the entity position/rotation. kill the deployed entity
                var ownerID = entity.OwnerID;

                //entity.OwnerID = 1337420; //so it doesn't trigger stuff
                MarkInternal(entity);

                var pos = entity.transform.position;
                var rot = entity.transform.eulerAngles;

                var passedTest = true;

                Ride rideFoundInTheWay = null;

                float distance = 0;

                //first and foremonst: check if the minimum spacing between rides is met.
                foreach (var ride in rides.ToDictionary(c => c.Key, c => c.Value))
                {
                    distance = Vector3.Distance(pos, ride.Value.transform.position);

                    if (distance < configData.minimumRideSpacing)
                    {
                        passedTest = false;
                        rideFoundInTheWay = ride.Value;
                        break;
                    }
                }

                var maybePlayer = RustCore.FindPlayerById(ownerID);

                var def = rideDefinitions[skinToDefinition[entity.skinID]];

                var maybeTC = entity as BuildingPrivlidge;

                var atLeastOneFoundationFound = false;
                var atMostOneFoundationFound = true;
                var somethingElseFound = false;

                BuildingBlock theFoundation = null;

                //no point in going futher if the first test (proximity to other rides) has failed
                if (passedTest)
                {

                    if (maybeTC != null)
                    {
                        var colliders = Physics.OverlapSphere(maybeTC.transform.position, 3F, CollisionLayer);
                        if (colliders.Any())
                        {
                            foreach (var entry in colliders)
                            {
                                var maybeBaseCombatEntity = entry.ToBaseEntity() as BaseCombatEntity;

                                if (maybeBaseCombatEntity != null)
                                {
                                    //ignore yourself
                                    if (maybeBaseCombatEntity == maybeTC) continue;
                                    //and players nearby
                                    if (maybeBaseCombatEntity as BasePlayer != null) continue;

                                    //is it a foundation?
                                    if (maybeBaseCombatEntity.PrefabName == AmusementRidesPlugin.PREFAB_FOUNDATION || maybeBaseCombatEntity.PrefabName == AmusementRidesPlugin.PREFAB_FOUNDATION_TRIANGLE)
                                    {
                                        var maybeFoundation = maybeBaseCombatEntity as BuildingBlock;
                                        if (maybeFoundation != null)
                                        {
                                            theFoundation = maybeFoundation;

                                            if (atLeastOneFoundationFound == false)
                                            {
                                                atLeastOneFoundationFound = true;
                                                atMostOneFoundationFound = true;
                                            }
                                            else
                                            {
                                                atMostOneFoundationFound = false;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        //not a foundation and not the TC. test failed
                                        somethingElseFound = true;
                                    }
                                }
                            }
                        }

                        if (somethingElseFound)
                        {
                            passedTest = false;
                        }
                        else
                        {
                            if (!atLeastOneFoundationFound)
                            {
                                passedTest = false;
                            }
                            else
                            {
                                if (!atMostOneFoundationFound)
                                {
                                    passedTest = false;
                                }
                            }
                        }
                    }
                }

                if (passedTest)
                {
                    if (maybeTC != null  && theFoundation != null)
                    {
                        var correction = new Vector3(def.posFoundationCorrectionX, def.posFoundationCorrectionY, def.posFoundationCorrectionZ);

                        pos = theFoundation.transform.position + correction;
                        NextTick(() =>
                        {
                            theFoundation.Kill(BaseNetworkable.DestroyMode.Gib);
                        });
                    }

                    var newRide = RideCreate(ownerID, pos, rot, def, "", 1, null, false, true);

                    rides[newRide].structure.rideVolume.allowedEntities.Add(entity.net.ID, entity);

                    if (theFoundation != null)
                    {
                        rides[newRide].structure.rideVolume.allowedEntities.Add(theFoundation.net.ID, theFoundation);
                    }
                    //the last volume check will be performed by the ride itself.
                }
                else
                {

                    if (maybePlayer != null)
                    {
                        if (rideFoundInTheWay == null)
                        {
                            TellMessage(maybePlayer, MSG(MSG_CAN_ONLY_PLACE_ON_SINGLE_FOUNDATION, maybePlayer.UserIDString, def.rideNickname));
                        }
                        else
                        {
                            TellMessage(maybePlayer, MSG(MSG_TOO_CLOSE_TO_ANOTHER_RIDE, maybePlayer.UserIDString, rideFoundInTheWay.rideData.nickname, distance.ToString("0.00"), configData.minimumRideSpacing.ToString("0.00")));

                        }
                    }

                    //refund
                    GivePlayerRideItem(def, maybePlayer);
                }


                NextTick(() =>
                {
                    entity.Kill(BaseNetworkable.DestroyMode.None);
                });


            }
        }

        object OnButtonPress(PressButton button, BasePlayer player)
        {
            if (entityToRide.ContainsKey(button.net.ID))
            {
                return entityToRide[button.net.ID].handler.WhenButtonPress(entityToRide[button.net.ID], button, player);
            }
            return null;
        }

        object OnSwitchToggle(ElectricSwitch electricSwitch, BasePlayer player)
        {
            if (entityToRide.ContainsKey(electricSwitch.net.ID))
            {
                return entityToRide[electricSwitch.net.ID].handler.WhenSwitchToggle(entityToRide[electricSwitch.net.ID], electricSwitch, player);
            }
            return null;
        }

        object OnItemRecycle(Item item, Recycler recycler)
        {
            if (skinToDefinition.ContainsKey(item.skin))
            {
                var definition = rideDefinitions[skinToDefinition[item.skin]];

                foreach (var cost in definition.rideCost)
                {
                    recycler.MoveItemToOutput(ItemManager.CreateByItemID(cost.Key, Convert.ToInt32(cost.Value/2)));
                }

                item.Remove();
                item.MarkDirty();

                return true;
            }
            else return null;
        }

        object CanSpawnInZone(BaseEntity entity)
        {
            if (IsInternal(entity)) return true;

            //if (entity.OwnerID == 1337420) return true;
            return null;
        }

        private object CanSpinDrop(Item item, BaseEntity entity)
        {
            if (IsInternal(entity)) return true;

            //if (entity.OwnerID == 1337420) return true;
            return null;
        }

        object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return null;
            if (entity.net == null) return null;
            if (info == null) return null;
            if (entity.IsDestroyed) return null;

            Ride maybeRide;
            if (entityToRide.TryGetValue(entity.net.ID, out maybeRide))
            {
                if (maybeRide == null)
                {
                    return null;
                }

                if (maybeRide.isAlreadyDestroying)
                {
                    return null;
                }
                return maybeRide.handler.WhenEntityTakeDamage(maybeRide, entity, info);
            }

            //not belonging to a ride, but still marked internal? return non-null.

            if (IsInternal(entity))
            {
                return true;
            }

            return null;
        }

        object OnStructureUpgrade(BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
        {
            if (!IsInternal(entity))
            {
                return null;
            }

            if (entityToRide.ContainsKey(entity.net.ID))
            {
                return entityToRide[entity.net.ID].handler.WhenStructureUpgrade(entityToRide[entity.net.ID], entity, player, grade);
            }
            return null;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (playerIDtoRide.ContainsKey(player.userID))
            {
                var ride = playerIDtoRide[player.userID];

                if (ride != null)
                {
                    ride.handler.WhenPlayerDisconnected(ride, player, reason);
                }
            }
            else
            {
                if (playerIDtoRidesNearby.ContainsKey(player.userID))
                {
                    foreach (var ride in playerIDtoRidesNearby[player.userID].Where(e => true).ToList())
                    {
                        if (ride != null)
                        {
                            if (ride.handler != null)
                            {
                                ride.handler.WhenPlayerDisconnected(ride, player, reason);
                            }
                        }
                    }
                }
            }
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            //force yourself onto every ride if you're in radius+1.5Fm
            if (rides != null && rides.Count > 0)
            {
                foreach (var ride in rides)
                {
                    if (Vector3.Distance(ride.Value.transform.position, player.transform.position) <= ride.Value.influenceSphere.radius + 1.5F)
                    {
                        ride.Value.handler.PlayerEnterNearby(ride.Value, player);
                    }
                }

                timer.Once(1F, () =>
                {
                    if (player == null)
                    {
                        return;
                    }

                    if (playerIDtoRidesNearby.ContainsKey(player.userID))
                    {

                        foreach (var ride in playerIDtoRidesNearby[player.userID].ToList())
                        {
                            if (ride != null)
                            {
                                if (ride.handler != null)
                                {
                                    ride.handler.PlayerEnterNearby(ride, player);
                                }
                            }
                        }
                    }
                });
            }
        }

        object OnPlayerDeath(BasePlayer player, HitInfo info)
        {
            if (playerIDtoRide.ContainsKey(player.userID))
            {
                var ride = playerIDtoRide[player.userID];

                if (ride != null)
                {
                    ride.handler.WhenPlayerDeath(ride, player, info);
                }
            }
            else
            {
                if (playerIDtoRidesNearby.ContainsKey(player.userID))
                {
                    foreach (var ride in playerIDtoRidesNearby[player.userID].Where(e => true).ToList())
                    {
                        if (ride != null)
                        {
                            if (ride.handler != null)
                            {
                                ride.handler.WhenPlayerDeath(ride, player, info);
                            }
                        }
                    }
                }
            }

            return null;
        }
        object OnCupboardAuthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (!IsInternal(privilege))
            {
                return null;
            }

            if (entityToRide.ContainsKey(privilege.net.ID))
            {
                return entityToRide[privilege.net.ID].handler.WhenCupboardUpdate(entityToRide[privilege.net.ID], privilege, player);
            }
            else return null;
        }

        object OnCupboardClearList(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (!IsInternal(privilege))
            {
                return null;
            }

            if (entityToRide.ContainsKey(privilege.net.ID))
            {
                return entityToRide[privilege.net.ID].handler.WhenCupboardUpdate(entityToRide[privilege.net.ID], privilege, player);
            }
            else return null;
        }

        object OnCupboardDeauthorize(BuildingPrivlidge privilege, BasePlayer player)
        {
            if (!IsInternal(privilege))
            {
                return null;
            }

            if (entityToRide.ContainsKey(privilege.net.ID))
            {
                return entityToRide[privilege.net.ID].handler.WhenCupboardUpdate(entityToRide[privilege.net.ID], privilege, player);
            }
            else return null;
        }

        object CanMountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (entityToRide.ContainsKey(mountable.net.ID))
            {
                return entityToRide[mountable.net.ID].handler.IfMountEntity(entityToRide[mountable.net.ID], player, mountable);
            }
            else return null;
        }

        
        object CanDismountEntity(BasePlayer player, BaseMountable mountable)
        {
            if (player == null)
            {
                return null;
            }

            Ride ride;

            if (!entityToRide.TryGetValue(mountable.net.ID, out ride))
            {
                return null;
            }

            return ride.handler.IfDismountEntity(entityToRide[mountable.net.ID], player, mountable);
        }

        void OnPlayerInput(BasePlayer player, InputState input)
        {
            if (player == null || input == null) return;

            if (!input.WasJustPressed(BUTTON.JUMP))
            {
                return;
            }

            var mountable = player.GetMounted();

            if (!IsInternal(mountable))
            {
                return;
            }

            Ride ride;

            if (!entityToRide.TryGetValue(mountable.net.ID, out ride))
            {
                return;
            }

            if (!ride.running)
            {
                return;
            }

            ride.handler.ForceDismountEntity(entityToRide[mountable.net.ID], player, mountable);
        }


        object CanUseLockedEntity(BasePlayer player, BaseLock baseLock)
        {
            if (!IsInternal(baseLock))
            {
                return null;
            }

            if (entityToRide.ContainsKey(baseLock.net.ID))
            {
                return entityToRide[baseLock.net.ID].handler.IfUseLockedEntity(entityToRide[baseLock.net.ID], player, baseLock);
            }
            else return null;
        }

        void OnLootEntity(BasePlayer player, BaseEntity entity)
        {
            if (!IsInternal(entity))
            {
                return;
            }

            if (entityToRide.ContainsKey(entity.net.ID))
            {
                if (player != null)
                {
                    entityToRide[entity.net.ID].handler.WhenLootEntity(entityToRide[entity.net.ID], player, entity);
                }
            }
        }

        void OnLootEntityEnd(BasePlayer player, BaseCombatEntity entity)
        {
            if (!IsInternal(entity))
            {
                return;
            }

            if (entityToRide.ContainsKey(entity.net.ID))
            {
                entityToRide[entity.net.ID].handler.WhenLootEntityEnd(entityToRide[entity.net.ID], player, entity);
            }
        }

        void OnLootEntity(BasePlayer player, Workbench workbench)
        {
            workbench?.gameObject.GetComponent<WorkbenchHelper>()?.ui.GuiOpen(player);
        }
        void OnLootEntityEnd(BasePlayer player, Workbench workbench)
        {
            workbench?.gameObject.GetComponent<WorkbenchHelper>()?.ui.GuiClose(player);
        }

        object CanDemolish(BasePlayer player, BuildingBlock block, BuildingGrade.Enum grade)
        {
            if (!IsInternal(block))
            {
                return null;
            }

            if (entityToRide.ContainsKey(block.net.ID))
            {
                TellMessage(player, MSG(MSG_CANT_DEMOLISH_RIDE_ENTITY, player.UserIDString), entityToRide[block.net.ID].rideData.nickname);
                return false;
            }
            else return null;
        }

        object OnStructureRotate(BaseCombatEntity entity, BasePlayer player)
        {
            if (!IsInternal(entity))
            {
                return null;
            }

            if (entityToRide.ContainsKey(entity.net.ID))
            {
                TellMessage(player, MSG(MSG_CANT_ROTATE_RIDE_ENTITY, player.UserIDString), entityToRide[entity.net.ID].rideData.nickname);
                return false;
            }
            else return null;
        }

        object CanPickupEntity(BasePlayer player, BaseEntity entity)
        {
            if (!IsInternal(entity))
            {
                return null;
            }

            if (entityToRide.ContainsKey(entity.net.ID))
            {
                return entityToRide[entity.net.ID].handler.IfPickupEntity(entityToRide[entity.net.ID], player, entity);
            }
            else return null;
        }

        //add the hooks for can use door, can press button etc

        public void EngageIO(IOEntity inputEntity, IOEntity outputEntity, int inputSlot, int outputSlot)
        {
            var maybeCurrentOutputEntity = inputEntity.inputs[inputSlot].connectedTo?.Get() ?? null;

            if (maybeCurrentOutputEntity != null)
            {
                DisengageIO(inputEntity, maybeCurrentOutputEntity, inputSlot, outputSlot);
            }

            inputEntity.inputs[inputSlot].connectedTo.Set(outputEntity);
            inputEntity.inputs[inputSlot].connectedToSlot = outputSlot;
            inputEntity.inputs[inputSlot].connectedTo.Init();

            outputEntity.outputs[outputSlot].connectedTo.Set(inputEntity);
            outputEntity.outputs[outputSlot].connectedToSlot = inputSlot;

            outputEntity.outputs[outputSlot].connectedTo.Init();
            outputEntity.MarkDirtyForceUpdateOutputs();
            outputEntity.SendNetworkUpdateImmediate();
            inputEntity.SendNetworkUpdate();
            outputEntity.SendChangedToRoot(true);
        }


        public void DisengageIO(IOEntity input, IOEntity output, int inputSlot, int outputSlot)
        {
            input.inputs[inputSlot].connectedTo.entityRef.uid = 0;
            input.inputs[inputSlot].connectedTo.ioEnt = null;
            input.inputs[inputSlot].connectedToSlot = 0;

            output.outputs[outputSlot].linePoints = null;

            output.outputs[outputSlot].connectedTo.entityRef.uid = 0;
            output.outputs[outputSlot].connectedTo.ioEnt = null;
            output.outputs[outputSlot].connectedToSlot = 0;

            input.UpdateFromInput(0, 0);

            input.MarkDirtyForceUpdateOutputs();
            input.SendIONetworkUpdate();
            input.SendNetworkUpdateImmediate();
            input.UpdateNetworkGroup();

            output.MarkDirtyForceUpdateOutputs();
            output.SendIONetworkUpdate();
            output.SendNetworkUpdateImmediate();
            output.UpdateNetworkGroup();
        }
        public Tuple<Vector3, Quaternion> GetRelativeTransformTuple(Transform transform, Vector3 localPos, Vector3 localEulers)
        {
            _reusableTransformTuple = new Tuple<Vector3, Quaternion>(transform.TransformPoint(localPos), transform.rotation * Quaternion.Euler(localEulers));
            return _reusableTransformTuple;
        }

        public DroppedItem PivotCreate(DroppedItem parentPivot, Vector3 position, Vector3 rotation, int itemID = 479143914)
        {
            //THANKS KARUZA!
            var newItem = ItemManager.CreateByItemID(itemID);

            Quaternion droppingQuaternionRot;

            if (parentPivot == null)
            {
                droppingQuaternionRot = Quaternion.Euler(rotation);
            }
            else
            {
                var relTransform = GetRelativeTransformTuple(parentPivot.transform, position, rotation);

                position = relTransform.Item1;
                droppingQuaternionRot = relTransform.Item2;
            }

            var dropped = newItem.Drop(position, Vector3.zero, droppingQuaternionRot);


            var droppedItem = dropped.GetComponent<DroppedItem>();

            if (parentPivot != null)
            {
                droppedItem.SetParent(parentPivot, true, true);
                /*
                droppedItem.transform.localPosition = position;
                droppedItem.transform.localEulerAngles = rotation;
                droppedItem.transform.hasChanged = true;
                droppedItem.SendNetworkUpdateImmediate();*/
            }

            var rigid = droppedItem.GetComponent<Rigidbody>();
            if (rigid != null)
            {
                rigid.isKinematic = true;
                rigid.useGravity = false;
            }

            droppedItem.syncPosition = true;
            droppedItem.enableSaving = false;
            droppedItem.EnableGlobalBroadcast(true);

            droppedItem.allowPickup = false;

            // no despawn
            droppedItem.Invoke("IdleDestroy", float.MaxValue);
            droppedItem.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), droppedItem, "IdleDestroy"));

            //droppedItem.OwnerID = 1337420;
            MarkInternal(droppedItem);

            return droppedItem;
        }

        public BaseEntity SummonEntity(string prefabName, DroppedItem parentPivot, Vector3 position, Vector3 rotation, ulong skin, bool parentToPivotIfNotNull, string gibsName = null)
        {
            BaseEntity entity;

            Quaternion droppingQuaternionRot;

            if (parentPivot == null)
            {
                droppingQuaternionRot = Quaternion.Euler(rotation);
            }
            else
            {
                var relTransform = GetRelativeTransformTuple(parentPivot.transform, position, rotation);

                position = relTransform.Item1;
                droppingQuaternionRot = relTransform.Item2;
            }

            entity = GameManager.server.CreateEntity(prefabName, position, droppingQuaternionRot, true);

            if (entity != null)
            {
                entity.skinID = skin;

                bool isGibs = false;

                var maybeGibs = entity as ServerGib;
                if (maybeGibs != null)
                {
                    var rigidBody = maybeGibs.gameObject.GetComponent<Rigidbody>();

                    if (rigidBody != null)
                    {
                        UnityEngine.Object.DestroyImmediate(rigidBody);
                    }

                    maybeGibs._gibName = gibsName;
                    isGibs = true;
                }

                MakeStableAndSpawn(entity);

                if (isGibs)
                {
                    maybeGibs.CancelInvoke(maybeGibs.RemoveMe);
                }

                if (parentToPivotIfNotNull)
                {
                    if (parentPivot != null)
                    {
                        entity.SetParent(parentPivot, true, false);
                    }

                }

                //entity.limitNetworking = true;

                //destroy on clients nearby already does this
                //entity.SendNetworkUpdateImmediate();
            }

            return entity;
        }

        public static void MarkInternal(BaseEntity entity)
        {
            if (!BaseNetworkableEx.IsValid(entity))
            {
                return;
            }

            entity.flags = (BaseEntity.Flags)((int)entity.flags | FLAG_INTERNAL);
        }

        public static void UnmarkInternal(BaseEntity entity)
        {
            if (!BaseNetworkableEx.IsValid(entity))
            {
                return;
            }

            entity.flags = (BaseEntity.Flags)((int)entity.flags & ~FLAG_INTERNAL);
        }


        public static bool IsInternal(BaseEntity entity)
        {
            if (!BaseNetworkableEx.IsValid(entity))
            {
                return false;
            }

            return ((int)entity.flags & FLAG_INTERNAL) == FLAG_INTERNAL;
        }

        public void MakeStableAndSpawn(BaseEntity entity)
        {
            entity.EnableGlobalBroadcast(true);
            entity.syncPosition = true;

            var maybeCombatEntity = entity as BaseCombatEntity;

            if (maybeCombatEntity != null)
            {
                maybeCombatEntity.pickup.enabled = false;

                var maybeChair = maybeCombatEntity as BaseMountable;

                if (maybeChair != null)
                {
                    maybeChair.isMobile = true;
                }

                var maybeFirework = maybeCombatEntity as BaseFirework;
                if (maybeFirework != null)
                {
                    //we do this so you can't fire up the fireworks manually, only with flags!

                    maybeFirework.SetFlag(BaseEntity.Flags.Reserved8, true, false);
                }
            }

            var groundWatch = entity.gameObject.GetComponent<GroundWatch>();

            if (groundWatch != null)
            {
                UnityEngine.Object.Destroy(groundWatch);
            }

            var destroyOnGroundMissing = entity.gameObject.GetComponent<DestroyOnGroundMissing>();

            if (destroyOnGroundMissing != null)
            {
                UnityEngine.Object.Destroy(destroyOnGroundMissing);
            }
            entity.enableSaving = false;

            //entity.RemoveFromTriggers();
            if (entity as BaseRidableAnimal != null)
            {
                entity.Invoke("OnPhysicsNeighbourChanged", float.MaxValue);
                entity.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), entity, "OnPhysicsNeighbourChanged"));
            }

            var meshCollider = entity.gameObject.GetComponent<MeshCollider>();


            if (meshCollider != null)
            {
                UnityEngine.Object.Destroy(meshCollider);
            }

            var rigidBody = entity.gameObject.GetComponent<Rigidbody>();

            if (rigidBody != null)
            {

                rigidBody.isKinematic = true;
                rigidBody.useGravity = false;
            }


            entity.Spawn();

            var maybeStability = entity as StabilityEntity;
            if (maybeStability != null)
            {
                //entity.enableSaving = false;
                maybeStability.grounded = true;
                //newFloor.syncPosition = true;

                /*
                var maybeBuilding = maybeStability as BuildingBlock;

                if (maybeBuilding != null)
                {
                    if (maybeBuilding.grade != BuildingGrade.Enum.None)
                    {
                        maybeBuilding.SetGrade(maybeBuilding.grade);
                    }

                    Instance.NextFrame(() =>
                    {
                        maybeBuilding.SetHealthToMax();
                        maybeBuilding.ClientRPC(null, "RefreshSkin");
                        maybeBuilding.UpdateSkin(true);

                        BuildingManager.server.GetBuilding(maybeStability.buildingID)?.Dirty();

                        maybeStability.SendNetworkUpdateImmediate();
                        maybeStability.UpdateNetworkGroup();
                    });
                    
                }*/
                

            }

            var maybeSign = entity as Signage;

            if (maybeSign != null)
            {
                maybeSign.SetFlag(BaseEntity.Flags.Locked, true, false, true);
            }/*
            else
            {
                entity.OwnerID = 1337420;
            }*/

            MarkInternal(entity);
        }
        public Dictionary<Vector3, Vector3> TheLineMethod(Vector3 lineStart, Vector3 lineEnd, bool alignRotationToLine, Vector3 cloneRotationOrRotationCorrection, uint intermediaryPoints)
        {
            if (intermediaryPoints == 0) intermediaryPoints = 1;

            Dictionary<Vector3, Vector3> result = new Dictionary<Vector3, Vector3>();

            Vector3 rot;

            if (alignRotationToLine)
            {
                rot = Quaternion.LookRotation((lineEnd - lineStart).normalized, Vector3.up).eulerAngles + cloneRotationOrRotationCorrection;
            }
            else
            {
                rot = cloneRotationOrRotationCorrection;
            }


            for (var i = 0; i < intermediaryPoints; i++)
            {
                var currentPos = Vector3.Lerp(lineStart, lineEnd, (float)i / (float)(intermediaryPoints));

                result.Add(currentPos, rot);
            }

            return result;
        }

        public Dictionary<Vector3, Vector3> TheCubeMethod(uint sidesX, uint sidesY, uint sidesZ, float sepX, float sepY, float sepZ, Vector3 centerpointRotation, Vector3 cloneRotation)
        {
            Dictionary<Vector3, Vector3> result = new Dictionary<Vector3, Vector3>();

            //if you wanna go to centerpoint, go to half negative coordinates

            float startX = -(float)sidesX * sepX / 2F;
            float startY = -(float)sidesY * sepY / 2F;
            float startZ = -(float)sidesZ * sepZ / 2F;

            for (var x = 0; x < sidesX; x++)
            {
                var currentX = startX + (float)x * sepX + sepX / 2;
                for (var y = 0; y < sidesY; y++)
                {
                    var currentY = startY + (float)y * sepY + sepY / 2;
                    for (var z = 0; z < sidesZ; z++)
                    {
                        var currentZ = startZ + (float)z * sepZ + sepZ / 2;
                        result.Add(new Vector3(currentX, currentY, currentZ), cloneRotation);
                    }
                }
            }

            return result;
        }

        public Dictionary<Vector3, Vector3> TheCircleMethod(int sides, Vector3 distanceFromCenterpoint, Vector3 upAxis, Vector3 localRotation, bool sameDirection, Vector3 rotationCorrection)
        {
            Dictionary<Vector3, Vector3> result = new Dictionary<Vector3, Vector3>();

            //item 1 position, item 2 rotation
            for (var i = 0; i < sides; i++)
            {
                //don't trust the IDE: you gotta cast these to floats or you're gonna have a bad time

                Vector3 finalRot;
                float facingDir;

                facingDir = ((float)i / (float)sides) * 360F;
                finalRot = upAxis + new Vector3(0F, facingDir, 0F); //final rot

                var centerpoint = Vector3.zero;

                var finalPos = Quaternion.Euler(finalRot) * (distanceFromCenterpoint - centerpoint) + centerpoint;

                var absolutelyFinalRot = sameDirection ? rotationCorrection + localRotation : finalRot + localRotation;

                result.Add(finalPos, absolutelyFinalRot);
            }

            return result;
        }

        public DroppedItem DropItem(Vector3 position, Vector3 rotation, Vector3 velocity, bool kinematic = false, bool gravity = true, int itemID = -363689972)
        {
            var newItem = ItemManager.CreateByItemID(itemID);
            var droppedItem = newItem.Drop(position, velocity, Quaternion.Euler(rotation)) as DroppedItem;

            var rigid = droppedItem.GetComponent<Rigidbody>();
            if (rigid != null)
            {
                rigid.isKinematic = kinematic;
                rigid.useGravity = gravity;
            }

            droppedItem.syncPosition = true;
            droppedItem.enableSaving = false;

            droppedItem.allowPickup = false;

            // no despawn
            droppedItem.Invoke("IdleDestroy", float.MaxValue);
            droppedItem.CancelInvoke((Action)Delegate.CreateDelegate(typeof(Action), droppedItem, "IdleDestroy"));

            return droppedItem;
        }

        Tuple<Vector3, Quaternion> _reusableTransformTuple;


        //must be serializable. definitions have those
        public class RideVolumeColliderData
        {
            public bool isBox = true;

            public float posX = 0F;
            public float posY = 0F;
            public float posZ = 0F;

            public float rotX = 0F;
            public float rotY = 0F;
            public float rotZ = 0F;

            public float sizeX = 10F;
            public float sizeY = 10F;
            public float sizeZ = 10F;

            public float radius = 10F;
        }

        //attach to a new game object.
        //then each ride has one. destroy that gameobject on removal.

        public class RideVolume : AmusementBehaviour
        {
            public Ride ride;

            public List<Collider> colliders = new List<Collider>();

            public Dictionary<uint, BaseCombatEntity> alienEntities = new Dictionary<uint, BaseCombatEntity>();
            public Dictionary<uint, BaseCombatEntity> allowedEntities = new Dictionary<uint, BaseCombatEntity>();

            public bool initialCheckPerformed = false;

            public void Prepare(Ride ride, Vector3 localPosition, Vector3 localEulerAngles, List<RideVolumeColliderData> rideVolumeColliders)
            {
                //iterate through the list. for every entry, create an appropriate collider
                foreach (var col in rideVolumeColliders)
                {
                    var boxCollider = gameObject.AddComponent<BoxCollider>();

                    boxCollider.isTrigger = true;
                    boxCollider.center = new Vector3(col.posX, col.posY, col.posZ);
                    boxCollider.size = new Vector3(col.sizeX, col.sizeY, col.sizeZ);

                    boxCollider.transform.SetParent(ride.transform, false);
                    //boxCollider.transform.localPosition = localPosition;// + boxCollider.center;
                    boxCollider.transform.localEulerAngles = localEulerAngles + new Vector3(col.rotX, col.rotY, col.rotZ);
                    colliders.Add(boxCollider);
                }

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                this.ride = ride;
            }

            public bool InitialCheck(Vector3 position, Vector3 rotation, RideDefinition definition, RideData rideData, ulong playerOwnerID, float rideHealth, string forceName, bool isAdminRide, bool justCreated = false)
            {

                //if there's any alien entities that shouldn't be there,
                //destroy the ride (and refund the player the item)
                var result = false;

                if (isAdminRide)
                {
                    result =  true;
                    initialCheckPerformed = true;
                }
                else
                {
                    if (alienEntities.Any())
                    {

                        var maybePlayer = RustCore.FindPlayerById(playerOwnerID);
                        if (maybePlayer != null)
                        {
                            var msg = MSG(MSG_CANT_DEPLOY_RIDES_IN_THE_WAY, maybePlayer.UserIDString);

                            var count = 0;

                            foreach (var ent in alienEntities)
                            {
                                var endChar = count == alienEntities.Count - 1 ? ". " : ", ";
                                msg += $"<color=red>{ent.Value.ShortPrefabName}</color>{endChar}";

                                count++;
                            }

                            Instance.TellMessage(maybePlayer, msg);
                            Instance.PlayerTryPickupRide(ride, maybePlayer);
                        }
                        else
                        {
                            //no player? remove the ride
                            Instance.RideRemove(ride.name);
                        }
                    }
                    else
                    {
                        result = true;
                    }

                    initialCheckPerformed = true;
                }



                if (result)
                {
                    ride.PostInit(position, rotation, definition, rideData, playerOwnerID, rideHealth, forceName, isAdminRide, justCreated);
                }

                return result;
            }

            void OnTriggerEnter(Collider col)
            {
                if (ride == null) return;
                var entity = col.ToBaseEntity() as BaseCombatEntity;
                if (entity == null) return;
                if (entity as BasePlayer != null) return;
                if (entity as BaseCorpse != null) return;

                var maybeParent = entity.GetParentEntity();

                //ignore entities with a vehicle parent

                if (maybeParent != null)
                {
                    if (maybeParent != entity)
                    {
                        if (maybeParent as BaseVehicle != null || maybeParent as BaseVehicleModule != null || maybeParent as HotAirBalloon != null)
                        {
                            return;
                        }
                    }
                }

                if (ride.structure.allEntities.ContainsKey(entity.net.ID)) return;
                if (alienEntities.ContainsKey(entity.net.ID)) return;

                if (initialCheckPerformed == false)
                {
                    if (!allowedEntities.ContainsKey(entity.net.ID))
                    {
                        alienEntities.Add(entity.net.ID, entity);
                    }
                    //and we'll deal with it soon after
                }
                else
                {
                    if (entity.OwnerID == 0 || entity.OwnerID == 1337 || IsInternal(entity) /* entity.OwnerID == 1337420 */)
                    {
                        return;
                    }

                    if (entity as BaseVehicle != null) return;
                    if (entity as BaseNpc != null) return;

                    if (entity as LootContainer != null)
                    {
                        if (entity as SupplyDrop != null || entity as HackableLockedCrate != null)
                        {
                            return;
                        }
                    }

                    var maybePlayer = RustCore.FindPlayerById(entity.OwnerID);
                    if (maybePlayer != null)
                    {
                        //don't add, kill instantly
                        Instance.TellMessage(maybePlayer, MSG(MSG_CANT_DEPLOY_THINGS_ON_RIDE, maybePlayer.UserIDString), ride.rideData.nickname);
                    }
                    entity.Kill(BaseNetworkable.DestroyMode.Gib);
                }
            }
        }

        //attach to a new game object.
        //then the ride has a cache of those monos

        public class RideSegmentCollider : AmusementBehaviour
        {
            public Ride ride;
            public DroppedItem pivot;

            public string segmentName;

            public SphereCollider sphereCollider;
            public BoxCollider boxCollider;

            public float sphereRadius = 10F;
            public Vector3 boxSize;

            public bool isBox = true;

            public Dictionary<ulong, BasePlayer> playersInside = new Dictionary<ulong, BasePlayer>();

            public List<ulong> temporarilyIgnored = new List<ulong>();

            //temporarily whitelisted players can't enter/exit the collider rapidly
            public void TemporarilyIgnore(ulong userID, float seconds)
            {
                if (!IsTemporarilyIgnored(userID))
                {
                    temporarilyIgnored.Add(userID);
                    Instance.timer.Once(seconds, () =>
                    {
                        if (temporarilyIgnored != null)
                        {
                            if (IsTemporarilyIgnored(userID))
                            {
                                temporarilyIgnored.Remove(userID);
                            }
                        }
                    });
                }
            }

            public bool IsTemporarilyIgnored(ulong userID)
            {
                return temporarilyIgnored.Contains(userID);
            }


            public void PrepareAsBox(Ride ride, DroppedItem pivot, Vector3 localPosition, Vector3 localEulerAngles, string segmentName, Vector3 boxSize)
            {
                boxCollider = gameObject.AddComponent<BoxCollider>();

                boxCollider.isTrigger = true;

                boxCollider.center = Vector3.zero;
                boxCollider.size = boxSize;

                boxCollider.transform.SetParent(pivot.transform, false);
                boxCollider.transform.localPosition = localPosition;
                boxCollider.transform.localEulerAngles = localEulerAngles;

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                this.segmentName = segmentName;
                this.pivot = pivot;
                this.ride = ride;
                this.boxSize = boxSize;
            }

            void OnTriggerEnter(Collider col)
            {
                if (ride == null) return;
                if (pivot == null) return;

                if (ride.handler.OnRideTriggerEnter(ride, col, segmentName))
                {
                    OnTriggerEnterSuccess(col, segmentName);
                }
            }

            void OnTriggerExit(Collider col)
            {
                if (ride == null) return;
                if (pivot == null) return;

                if (ride.handler.OnRideTriggerExit(ride, col, segmentName))
                {
                    OnTriggerExitSuccess(col, segmentName);
                }
            }

            public object OnTriggerEnterSuccess(Collider col, string segmentName)
            {
                var maybePlayer = col.ToBaseEntity() as BasePlayer;
                if (maybePlayer != null)
                {
                    if (ride.handler.IfEnterSegmentCollider(ride, maybePlayer, segmentName, transform) == null)
                    {
                        //if (IsTemporarilyIgnored(maybePlayer.userID)) return false;

                        //TemporarilyIgnore(maybePlayer.userID, 0.5F);

                        playersInside.Add(maybePlayer.userID, maybePlayer);

                        if (!Instance.playerIDtoSegmentCollider.ContainsKey(maybePlayer.userID))
                        {
                            Instance.playerIDtoSegmentCollider.Add(maybePlayer.userID, this);
                        }
                        else
                        {
                            Instance.playerIDtoSegmentCollider[maybePlayer.userID] = this;
                        }

                        maybePlayer.SetParent(pivot, true, true);
                        return true;
                    }
                    else return false;
                }
                else return false;
            }

            public object OnTriggerExitSuccess(Collider col, string segmentName)
            {
                var maybePlayer = col.ToBaseEntity() as BasePlayer;
                if (maybePlayer != null)
                {
                    if (ride.handler.IfExitSegmentCollider(ride, maybePlayer, segmentName) == null)
                    {
                        //if (IsTemporarilyIgnored(maybePlayer.userID)) return false;

                        //TemporarilyIgnore(maybePlayer.userID, 0.5F);

                        if (Instance.playerIDtoSegmentCollider.ContainsKey(maybePlayer.userID))
                        {
                            Instance.playerIDtoSegmentCollider.Remove(maybePlayer.userID);
                        }

                        playersInside.Remove(maybePlayer.userID);
                        maybePlayer.SetParent(null, true, true);
                        return true;
                    }
                    else return false;
                }
                else return false;
            }

            public object RideHandlerManualEjection(BasePlayer player)
            {
                ride.handler.IfExitSegmentCollider(ride, player, segmentName);

                if (Instance.playerIDtoSegmentCollider.ContainsKey(player.userID))
                {
                    Instance.playerIDtoSegmentCollider.Remove(player.userID);
                }

                playersInside.Remove(player.userID);

                if (player != null)
                {
                    player.SetParent(null, true, true);
                }

                return true;
            }
        }

        public class HorseHelper : AmusementBehaviour
        {
            public float currentTime = float.MinValue;
            public float lastUpdate = float.MinValue;

            public Ride ride;
            public RidableHorse horse;

            public float updateRate = float.MinValue; //let's tinker with that

            public float updateRateMin = 3F;
            public float updateRateMax = 6F;

            public bool rideRunningPrevious;
            public bool rideRunningNow;

            public static Vector3 correctionVector = Vector3.zero;


            void OnPhysicsNeighbourChanged()
            { 
                horse.CancelInvoke(horse.DelayedDropToGround);
            }

            public void Init(RidableHorse horse, Ride ride)
            {
                this.ride = ride;
                this.horse = horse;
                rideRunningNow = true;

                this.horse.SetDecayActive(false);
            }

            void LateUpdate()
            {
                rideRunningPrevious = rideRunningNow;
                rideRunningNow = ride.running;

                if (rideRunningPrevious ^ rideRunningNow)
                {
                    currentTime = Time.time;

                    if (currentTime > lastUpdate + updateRate)
                    {
                        horse.transform.localPosition = correctionVector;

                        horse.transform.localPosition = horse.transform.localPosition + Vector3.back / 2F;
                        //horse.transform.hasChanged = true;

                        horse.SendNetworkUpdateImmediate();

                        horse.Invoke(() =>
                        {
                            horse.transform.localPosition = horse.transform.localPosition + Vector3.forward / 2F;
                            horse.transform.hasChanged = true;

                            horse.SendNetworkUpdateImmediate();
                        }, 0.1F);

                        updateRate = UnityEngine.Random.Range(updateRateMin, updateRateMax);

                        lastUpdate = currentTime;
                    }
                }

            }

        }

        public enum RideContainerType
        {
            SmallBox,
            BigBox,
            TC
        }

        public class RideStructure
        {
            public RideStructure(Ride ride)
            {
                this.ride = ride;
                //main pivot is always a gear
                mainPivot = Instance.PivotCreate(null, this.ride.transform.position, this.ride.transform.eulerAngles);

                //mainPivot.limitNetworking = false;

                pivotGetParentPivot.Add(mainPivot, null); //main pivot is not parented to anything
            }

            //non serializable, RUNTIME shit only

            public Ride ride;
            //cache all entities, useful for fast removal
            public Dictionary<uint, BaseEntity> allEntities = new Dictionary<uint, BaseEntity>();
            public Dictionary<uint, StabilityEntity> immobileStabilityEntities = new Dictionary<uint, StabilityEntity>();
            public Dictionary<uint, BaseCombatEntity> damagableEntities = new Dictionary<uint, BaseCombatEntity>();
            
            public DroppedItem mainPivot;

            public VendingMachine vendingMachine;

            //key: full path
            public Dictionary<string, RideSegmentCollider> segmentColliders = new Dictionary<string, RideSegmentCollider>();

            public Dictionary<string, DroppedItem> pivots = new Dictionary<string, DroppedItem>();

            //will tell us what pivot is parented to what pivot
            //1st is parented to 2nd
            public Dictionary<DroppedItem, DroppedItem> pivotGetParentPivot = new Dictionary<DroppedItem, DroppedItem>();

            //will tell us what entity is parented to what pivot
            public Dictionary<BaseEntity, DroppedItem> entityGetPivot = new Dictionary<BaseEntity, DroppedItem>();

            //and this reverse one will give us a list entities per pivot
            public Dictionary<DroppedItem, List<BaseEntity>> pivotGetEntities = new Dictionary<DroppedItem, List<BaseEntity>>();

            public Dictionary<int, Dictionary<uint, BaseEntity>> specialGroups = new Dictionary<int, Dictionary<uint, BaseEntity>>();
            public Dictionary<uint, int> entityToSpecialGroup = new Dictionary<uint, int>();

            public Dictionary<uint, BaseMountable> mountables = new Dictionary<uint, BaseMountable>();
            public Dictionary<BaseMountable, Vector3> mountablePreviousRotation = new Dictionary<BaseMountable, Vector3>();
            public Dictionary<BaseMountable, Vector3> mountablePreviousPosition = new Dictionary<BaseMountable, Vector3>();
            public Dictionary<BaseMountable, Vector3> mountableCurrentVelocity = new Dictionary<BaseMountable, Vector3>();
            public Dictionary<BaseMountable, Vector3> mountableCurrentUp = new Dictionary<BaseMountable, Vector3>();
            public Dictionary<BaseMountable, float> mountableCurrentMagnitude = new Dictionary<BaseMountable, float>();

            public bool hasBuilding = false;
            public uint buildingID = 0; //0 till specified otherwise
            public BuildingManager.Building building = null;

            public RideContainerType containerType = RideContainerType.SmallBox;

            public BuildingPrivlidge containerAsTC = null;
            public StorageContainer container = null;

            public BaseLock baseLock = null;
            public CodeLock baseLockAsCodeLock = null;
            public KeyLock baseLockAsKeyLock = null;

            public RideVolume rideVolume = null;

            public ElectricGenerator boomboxGenerator = null;
            public DeployableBoomBox boombox = null;

            //1 splitter, then 3 splitters, then 9 splitters

            public Splitter splitterHiddenLayer1 = null;

            public Splitter splitterHiddenLayer2_1 = null;
            public Splitter splitterHiddenLayer2_2 = null;
            public Splitter splitterHiddenLayer2_3 = null;

            public Splitter splitterLayer3_1_1 = null;
            public Splitter splitterLayer3_1_2 = null;
            public Splitter splitterLayer3_1_3 = null;

            public Splitter splitterLayer3_2_1 = null;
            public Splitter splitterLayer3_2_2 = null;
            public Splitter splitterLayer3_2_3 = null;

            public Splitter splitterLayer3_3_1 = null;
            public Splitter splitterLayer3_3_2 = null;
            public Splitter splitterLayer3_3_3 = null;

            public List<ConnectedSpeaker> allSpeakers = new List<ConnectedSpeaker>();

            public ConnectedSpeaker[] allSpeakersArray = null;

            public SplitterSlot[] numberedSlots = new SplitterSlot[27];
            public void AssignNumberedSlot(int id, Splitter splitter, int splitterSlot)
            {
                numberedSlots[id] = new SplitterSlot(splitter, splitterSlot);
            }

            public void InitializeSplitters()
            {
                //don't engage it yet.
                splitterHiddenLayer1 = SummonSplitterAndAddToCache<Splitter>(boombox, 0);

                splitterHiddenLayer2_1 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer1, 0);
                splitterHiddenLayer2_2 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer1, 1);
                splitterHiddenLayer2_3 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer1, 2);

                splitterLayer3_1_1 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_1, 0);
                splitterLayer3_1_2 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_1, 1);
                splitterLayer3_1_3 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_1, 2);

                splitterLayer3_2_1 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_2, 0);
                splitterLayer3_2_2 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_2, 1);
                splitterLayer3_2_3 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_2, 2);

                splitterLayer3_3_1 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_3, 0);
                splitterLayer3_3_2 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_3, 1);
                splitterLayer3_3_3 = SummonSplitterAndAddToCache<Splitter>(splitterHiddenLayer2_3, 2);

                AssignNumberedSlot(0, splitterLayer3_1_1, 0);
                AssignNumberedSlot(1, splitterLayer3_1_1, 1);
                AssignNumberedSlot(2, splitterLayer3_1_1, 2);

                AssignNumberedSlot(3, splitterLayer3_1_2, 0);
                AssignNumberedSlot(4, splitterLayer3_1_2, 1);
                AssignNumberedSlot(5, splitterLayer3_1_2, 2);

                AssignNumberedSlot(6, splitterLayer3_1_3, 0);
                AssignNumberedSlot(7, splitterLayer3_1_3, 1);
                AssignNumberedSlot(8, splitterLayer3_1_3, 2);

                AssignNumberedSlot(9, splitterLayer3_2_1, 0);
                AssignNumberedSlot(10, splitterLayer3_2_1, 1);
                AssignNumberedSlot(11, splitterLayer3_2_1, 2);

                AssignNumberedSlot(12, splitterLayer3_2_2, 0);
                AssignNumberedSlot(13, splitterLayer3_2_2, 1);
                AssignNumberedSlot(14, splitterLayer3_2_2, 2);

                AssignNumberedSlot(15, splitterLayer3_2_3, 0);
                AssignNumberedSlot(16, splitterLayer3_2_3, 1);
                AssignNumberedSlot(17, splitterLayer3_2_3, 2);

                AssignNumberedSlot(18, splitterLayer3_3_1, 0);
                AssignNumberedSlot(19, splitterLayer3_3_1, 1);
                AssignNumberedSlot(20, splitterLayer3_3_1, 2);

                AssignNumberedSlot(21, splitterLayer3_3_2, 0);
                AssignNumberedSlot(22, splitterLayer3_3_2, 1);
                AssignNumberedSlot(23, splitterLayer3_3_2, 2);

                AssignNumberedSlot(24, splitterLayer3_3_3, 0);
                AssignNumberedSlot(25, splitterLayer3_3_3, 1);
                AssignNumberedSlot(26, splitterLayer3_3_3, 2);

            }

            public Splitter SummonSplitterAndAddToCache<T>(IOEntity andThenEngageYourZeroethInputToOutputEntity = null, int outputSlotForThatOutputEntity = 0) where T : IOEntity
            {
                Splitter newIOCompo = Instance.SummonEntity(AmusementRidesPlugin.PREFAB_SPLITTER, null, boomboxGenerator.transform.position, boomboxGenerator.transform.eulerAngles, 0, false) as Splitter;

                ride.structure.allEntities.Add(newIOCompo.net.ID, newIOCompo);
                Instance.entityToRide.Add(newIOCompo.net.ID, ride);

                if (andThenEngageYourZeroethInputToOutputEntity != null)
                {
                    Instance.EngageIO(newIOCompo, andThenEngageYourZeroethInputToOutputEntity, 0, outputSlotForThatOutputEntity);
                }

                return newIOCompo;
            }

            public bool EngageIOToSplitterSystemFirstAvailableSlot(IOEntity inputIOEntity)
            {
                for (var i = 0; i < 27; i++)
                {
                    var thisSlot = numberedSlots[i];

                    if (thisSlot.IsFree())
                    {
                        Instance.EngageIO(inputIOEntity, thisSlot.ThisSplitter, 0, thisSlot.ThisSplitterOutputSlot);
                        return true;
                    }
                }

                return false;
            }

            public class SplitterSlot
            {
                public Splitter ThisSplitter;
                public int ThisSplitterOutputSlot;

                public SplitterSlot(Splitter thisSplitter, int thisSplitterSlot)
                {
                    ThisSplitter = thisSplitter;
                    ThisSplitterOutputSlot = thisSplitterSlot;
                }

                public bool IsFree()
                {
                    return GetEntityConnectedToThisSlot() == null;
                }

                public IOEntity GetEntityConnectedToThisSlot()
                {
                    return ThisSplitter.outputs[ThisSplitterOutputSlot].connectedTo.Get();
                }
            }



            public virtual DroppedItem AttachNewPivotToPivot(DroppedItem parentPivot, Vector3 position, Vector3 rotation, int itemID = 479143914)
            {
                var newPivot = Instance.PivotCreate(parentPivot, position, rotation, itemID);

                pivotGetParentPivot.Add(newPivot, parentPivot);
                //newPivot.transform.hasChanged = true;
                //newPivot.SendNetworkUpdateImmediate();

                return newPivot;
            }

            public virtual BaseEntity AttachFromSegmentEntityDefinitionToPivot(RideDefinitionSegmentEntity segEnt, DroppedItem pivot)
            {
                BaseEntity result = null;
                var pos = new Vector3(segEnt.posX, segEnt.posY, segEnt.posZ);
                var rot = new Vector3(segEnt.rotX, segEnt.rotY, segEnt.rotZ);

                if (segEnt.propItemID != 0)
                {
                    result = AttachNewItemToPivot(segEnt.propItemID, pivot, pos, rot, 0, segEnt.isSpecial, segEnt.specialGroup);
                }
                else
                {
                    result = AttachNewEntityToPivot(segEnt.prefabName, pivot, pos, rot, 0, segEnt.isSpecial, segEnt.locked, segEnt.isMobile, segEnt.specialGroup, segEnt.buildingGrade, segEnt.damagable, segEnt.gibsName);
                }

                return result;
            }

            public virtual DroppedItem AttachNewItemToPivot(int itemID, DroppedItem parentPivot, Vector3 position, Vector3 rotation, ulong skin, bool special, int specialGroup)
            {
                if (parentPivot == null)
                {
                    parentPivot = mainPivot;
                }                

                var relTransform = Instance.GetRelativeTransformTuple(parentPivot.transform, position, rotation);

                position = relTransform.Item1;
                rotation = relTransform.Item2.eulerAngles;

                var newItem = Instance.DropItem(position, rotation, Vector3.zero, true, false, itemID);
                if (newItem == null) return null;

                newItem.SetParent(parentPivot, true, true);

                Instance.entityToRide.Add(newItem.net.ID, ride);

                allEntities.Add(newItem.net.ID, newItem);

                entityGetPivot.Add(newItem, parentPivot);

                if (!pivotGetEntities.ContainsKey(parentPivot))
                {
                    pivotGetEntities.Add(parentPivot, new List<BaseEntity>());
                }

                pivotGetEntities[parentPivot].Add(newItem);

                if (special)
                {
                    if (!specialGroups.ContainsKey(specialGroup))
                    {
                        specialGroups.Add(specialGroup, new Dictionary<uint, BaseEntity>());
                    }

                    specialGroups[specialGroup].Add(newItem.net.ID, newItem);

                    entityToSpecialGroup.Add(newItem.net.ID, specialGroup); //reverse lookup
                }

                return newItem;
            }

            public virtual BaseEntity AttachNewEntityToPivot(string prefabName, DroppedItem pivot, Vector3 position, Vector3 rotation, ulong skin, bool special, bool locked, bool mobile, int specialGroup, BuildingGrade.Enum grade = BuildingGrade.Enum.None, bool damagable = false, string gibsName = null)
            {
                //IDEALLY THIS SHOULD BE DONE ASYNCHRONOUSLY, maybe with an effect per-prefab?
                //or manually. then instead of doing all of that here right now, you put it in a dictionary and do them one by one
                //for now maybe stagger it a bit

                if (!mobile)
                {
                    pivot = null;
                }

                bool parentToPivot = true;

                if (pivot == null)
                {
                    pivot = mainPivot;
                    parentToPivot = false;
                }

                var newEntity = Instance.SummonEntity(prefabName, pivot, position, rotation, skin, parentToPivot, gibsName);

                if (locked)
                {
                    newEntity.SetFlag(BaseEntity.Flags.Locked, true, false, true);
                }

                if (newEntity == null)
                {
                    return newEntity;
                }

                newEntity.net.SwitchSecondaryGroup(ride.secondaryVisibilityGroup);

                //it's now parented, all we need to do is add to caches.
                Instance.entityToRide.Add(newEntity.net.ID, ride);

                var maybeHorse = newEntity as RidableHorse;
                if (maybeHorse != null)
                {
                    Instance.timer.Once(2F, () =>
                    {
                        //add the horse saddle to mounts
                        var saddle = maybeHorse.mountPoints[0].mountable;//maybeHorse.GetSaddle();

                        if (saddle == null)
                        {

                        }
                        else
                        {
                            allEntities.Add(saddle.net.ID, saddle);
                            Instance.entityToRide.Add(saddle.net.ID, ride);
                            mountables.Add(saddle.net.ID, saddle);
                            mountablePreviousPosition.Add(saddle, saddle.transform.position);
                            mountablePreviousRotation.Add(saddle, saddle.transform.position);
                            mountableCurrentVelocity.Add(saddle, Vector3.zero);
                            mountableCurrentMagnitude.Add(saddle, 0F);
                            mountableCurrentUp.Add(saddle, saddle.transform.up);
                        }
                    });
                }
                else
                {

                    var maybeDecayEntity = newEntity as DecayEntity;
                    if (maybeDecayEntity != null)
                    {
                        if (hasBuilding)
                        {
                            maybeDecayEntity.AttachToBuilding(buildingID);
                            building = maybeDecayEntity.GetBuilding();
                        }

                        var maybeStabilityEntity = newEntity as StabilityEntity;
                        if (maybeStabilityEntity != null)
                        {
                            //only add non-mobile ones, the mobile ones take care of themselves
                           // maybeStabilityEntity.SetHealth(maybeStabilityEntity.MaxHealth());

                            if (!mobile)
                            {
                                immobileStabilityEntities.Add(maybeStabilityEntity.net.ID, maybeStabilityEntity);
                            }
                            var maybeBuilding = maybeStabilityEntity as BuildingBlock;

                            if (maybeBuilding != null)
                            {
                                maybeBuilding.SetGrade(grade == BuildingGrade.Enum.None ? BuildingGrade.Enum.Twigs : grade);
                                maybeBuilding.SetHealthToMax();
                                maybeBuilding.SendNetworkUpdate();
                                maybeBuilding.UpdateSkin();
                                maybeBuilding.ResetUpkeepTime();
                                
                                BuildingManager.server.GetBuilding(buildingID)?.Dirty();
                            }
                        }
                        else
                        {
                            //connected speakers? or something else?

                            if (prefabName == AmusementRidesPlugin.PREFAB_SPEAKER)
                            {
                                var maybeIOEntity = maybeDecayEntity as IOEntity;

                                if (maybeIOEntity != null)
                                {
                                    //engage, or at least try to

                                    var asSpeaker = maybeIOEntity as ConnectedSpeaker;

                                    if (asSpeaker != null)
                                    {
                                        ride.structure.allSpeakers.Add(asSpeaker);
                                    }

                                    EngageIOToSplitterSystemFirstAvailableSlot(maybeIOEntity);
                                }
                            }
                        }
                    }

                    /*
                    if (isNormal && newEntity as Signage == null)
                    {
                        var newMobileCompo = newEntity.gameObject.AddComponent<RefreshHelperMobile>();
                        newMobileCompo.Init(newEntity, ride);
                    }*/
                }


                allEntities.Add(newEntity.net.ID, newEntity);

                if (damagable)
                {
                    damagableEntities.Add(newEntity.net.ID, newEntity as BaseCombatEntity);
                }

                if (pivot != null)
                {
                    entityGetPivot.Add(newEntity, pivot);

                    if (!pivotGetEntities.ContainsKey(pivot))
                    {
                        pivotGetEntities.Add(pivot, new List<BaseEntity>());
                    }

                    pivotGetEntities[pivot].Add(newEntity);
                }

                var maybeMountable = newEntity as BaseMountable;

                if (maybeMountable != null)
                {
                    mountables.Add(maybeMountable.net.ID, maybeMountable);
                    mountablePreviousPosition.Add(maybeMountable, maybeMountable.transform.position);
                    mountablePreviousRotation.Add(maybeMountable, maybeMountable.transform.position);
                    mountableCurrentVelocity.Add(maybeMountable, Vector3.zero);
                    mountableCurrentMagnitude.Add(maybeMountable, 0F);
                    mountableCurrentUp.Add(maybeMountable, maybeMountable.transform.up);
                }

                if (special)
                {
                    if (!specialGroups.ContainsKey(specialGroup))
                    {
                        specialGroups.Add(specialGroup, new Dictionary<uint, BaseEntity>());
                    }

                    specialGroups[specialGroup].Add(newEntity.net.ID, newEntity);

                    entityToSpecialGroup.Add(newEntity.net.ID, specialGroup); //reverse lookup
                }

                newEntity.transform.hasChanged = true;
                newEntity.SendNetworkUpdateImmediate();

                return newEntity;
            }
        }

        public class RideDefinition
        {
            //must be entirely serializable
            //contains ride segments and other things
            public string rideName = "New Ride";
            public string rideNickname = null; //used for display, not identification purposes. Can change.
            public string rideAuthor = "Nikedemos";
            public string rideVersion = "1.0.0";
            public string rideDescription = "This description should be changed";
            public string rideImage = null;


            public Dictionary<int, int> rideCost = new Dictionary<int, int>
            {
            };

            public int workbenchLevel = 1;

            //register in SkinToRideDefinition on definition register
            //unregister on definition register

            public List<string> assignedMusic = null;

            public float posMusicX = 0F;
            public float posMusicY = 0F;
            public float posMusicZ = 0F;

            public float rotMusicX = 0F;
            public float rotMusicY = 0F;
            public float rotMusicZ = 0F;

            public bool canJoinLate = false;
            public bool reversable = false;

            public List<RideVolumeColliderData> rideVolumes = new List<RideVolumeColliderData> { new RideVolumeColliderData() };

            [JsonConverter(typeof(StringEnumConverter))]
            public RideContainerType containerType = RideContainerType.SmallBox;
            public ulong containerSkinID = 0; //this doesn't have to be set
            public float containerHealth = 1000F;

            public ulong rideItemSkinID = 0; //The definition won't be registered if it's 0. Must be unique.
            public int rideItemPickupID = AmusementRidesPlugin.ITEM_SMALL_BOX;

            //only for rides that need a foundation

            public float posFoundationCorrectionX = 0F;
            public float posFoundationCorrectionY = 0F;
            public float posFoundationCorrectionZ = 0F;

            public float posAntiCorrectionX = 0F;
            public float posAntiCorrectionY = 0F;
            public float posAntiCorrectionZ = 0F;

            public float posContainerX = 0F;
            public float posContainerY = 0F;
            public float posContainerZ = 0F;

            public float rotContainerX = 0F;
            public float rotContainerY = 0F;
            public float rotContainerZ = 0F;

            public int admissionFeePrice = 20;
            public string admissionFeeCurrency = "scrap";

            public bool waitForFullLoad = true;

            public float waitForFullLoadPercentage = 0.5F;

            public float minWaitTime = 10F;
            public float maxWaitTime = 30F;

            public float influenceRadius = 100F;

            public bool hasBuilding = false;

            public Dictionary<int, Dictionary<int, ulong>> specialGroupSkins = null;

            public Dictionary<int, Dictionary<int, string>> specialSignData = null;

            public string handlerName = "Default Handler";

            public bool useDuration = false;

            public float rideDuration = 150F;

            public Dictionary<int, RideDefinitionSegment> definitionSegments = new Dictionary<int, RideDefinitionSegment>();

            public ExtraRideData extraData = null;

            [JsonIgnore]
            public ItemDefinition pickupDefinition;

            public void ApplyPickupDefinition()
            {
                pickupDefinition = ItemManager.FindItemDefinition(rideItemPickupID);
            }

            public RideDefinition ShallowCopy()
            {
                return (RideDefinition) this.MemberwiseClone();
            }
        }

        public class RideDefinitionSegmentEntity
        {
            //Ride segments consist of RideEntities
            public string prefabName = "none";

            public int propItemID = 0;

            public string gibsName = null;

            [JsonConverter(typeof(StringEnumConverter))]
            public BuildingGrade.Enum buildingGrade = BuildingGrade.Enum.Twigs;

            public bool locked = false;
            public bool isMobile = true;
            public bool damagable = false;

            public float posX = 0F;
            public float posY = 0F;
            public float posZ = 0F;
            public float rotX = 0F;
            public float rotY = 0F;
            public float rotZ = 0F;

            public bool isSpecial = false;
            public int specialGroup = 0;
        }

        public class RideDefinitionSegment
        {
            //must be entirely serializable
            //and that includes the movement of pivots
            //so also ride motivators
            public string name = "Segment";

            public int pivotItemID = 479143914;

            public bool clonesFaceSameDirection = false;
            //if true, use cloneRotX, Y, Z to get the facing direction for all

            public float pivotPosX = 0F;
            public float pivotPosY = 0F;
            public float pivotPosZ = 0F;

            public float pivotRotX = 0F;
            public float pivotRotY = 0F;
            public float pivotRotZ = 0F;

            public float clonePosX = 0F;
            public float clonePosY = 0F;
            public float clonePosZ = 0F;

            public float cloneRotX = 0F;
            public float cloneRotY = 0F;
            public float cloneRotZ = 0F;

            public float cloneUpX = 0F;
            public float cloneUpY = 0F;
            public float cloneUpZ = 0F;

            public float sameRotX = 0F;
            public float sameRotY = 0F;
            public float sameRotZ = 0F;

            public bool hasCollider = false;

            public float colliderSizeX = 3F;
            public float colliderSizeY = 3F;
            public float colliderSizeZ = 3F;

            public float colliderRotX = 0F;
            public float colliderRotY = 0F;
            public float colliderRotZ = 0F;

            public float colliderPosX = 0F;
            public float colliderPosY = 0F;
            public float colliderPosZ = 0F;

            //if clones are on, treat the pivot X,Y,Z as the centre
            //of a circle/cube.
            public bool clonesAreRadial = true; //if false, we got cubic clones

            public int cloneCount = 1;

            //entities
            public Dictionary<int, RideDefinitionSegmentEntity> segmentEntities = new Dictionary<int, RideDefinitionSegmentEntity>();

            //sub segments, null by default
            public Dictionary<int, RideDefinitionSegment> childSegments = null;
        }

        public class RideHandlerColliderBased : RideHandler
        {
            public override object Init()
            {
                name = "DefaultColliderBasedHandler";

                return base.Init();
            }

            public override void ManualEjection(Ride ride, BasePlayer player)
            {
                if (Instance.playerIDtoSegmentCollider.ContainsKey(player.userID))
                {
                    var segmentCollider = Instance.playerIDtoSegmentCollider[player.userID];

                    segmentCollider.RideHandlerManualEjection(player);
                }
                else
                {
                    //return;
                }

                base.ManualEjection(ride, player);
            }

            public override bool AnybodyLeftRiding(Ride ride)
            {
                var result = false;

                foreach (var entry in ride.structure.segmentColliders)
                {
                    if (entry.Value.playersInside.Any())
                    {
                        result = true;
                        break;
                    }
                }

                return result;
            }

            public override object IfEnterSegmentCollider(Ride ride, BasePlayer player, string segmentColliderName, Transform boxTransform)
            {
                if (PlayerTryGetOn(ride, player, null, segmentColliderName) == null)
                {
                    return null;
                }
                else
                {
                    //player.SetVelocity(-player.GetWorldVelocity());
                    //player.Teleport(player.transform.position - 5 * player.estimatedVelocity * Time.deltaTime);
                    player.PauseFlyHackDetection(1F);
                    player.PauseSpeedHackDetection(1F);
                    player.PauseVehicleNoClipDetection(1);

                    var minVelocity = player.estimatedVelocity;
                    var minVelocityNorm = -20F*minVelocity.normalized;

                    player.ApplyInheritedVelocity(minVelocityNorm);
                    player.Invoke(() => player.ApplyInheritedVelocity(Vector3.zero), 0.25F);
                    return false;
                }
            }

            public override object IfExitSegmentCollider(Ride ride, BasePlayer player, string segmentColliderName)
            {
                if (PlayerTryGetOff(ride, player, null, "") == null)
                {
                    return null;
                }
                else
                {
                    //no fail to exit state for now
                    return false;
                }
            }
        }

        public class RideHandlerMountBased : RideHandler
        {
            public override object Init()
            {
                name = "DefaultMountBasedHandler";

                return base.Init();
            }

            public override object RideStop(Ride ride, params object[] args)
            {
                base.RideStop(ride, args);
                DismountEveryone(ride, args);
                return null;
            }
            public override void ApplyDismountState(Ride ride, BasePlayer player, params object[] args)
            {
                base.ApplyDismountState(ride, player, args);

                if (ride.running)
                {
                    var usingEntity = args[0] as BaseMountable;
                    if (usingEntity != null)
                    {
                        LaunchPlayer(ride, player, usingEntity);
                    }
                }


            }

            public override object IfMountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                return PlayerTryGetOn(ride, player, mountable);
            }

            public override object IfDismountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                return PlayerTryGetOff(ride, player, mountable);
            }

            public override void ForceDismountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                base.ForceDismountEntity(ride, player, mountable);


                //we don't want the hook response to override this, so...

                mountable.SetFlag(BaseEntity.Flags.Busy, true, false, false);

                //this will call the hook, the hook response will lead back to IfDismount, and that will lead to PlayerTryGetOff(ride, player, mountable);
                DismountFromMountable(ride, mountable);

                mountable.SetFlag(BaseEntity.Flags.Busy, false, false, false);
            }
        }


        public class RideHandler
        {
            public string name = "Default Handler";
            public string author = "Nikedemos";
            public string version = "1.0.0";

            public RideHandler()
            {
                Init();
            }
            public virtual object Init()
            {
                return null;
            }

            public virtual object Prepare(Ride ride, params object[] args)
            {
                PrepareGui(ride);
                Instance.PrintWarning($"{ride.name} (\"{ride.rideData.nickname}\") is ready to use the handler {name}!");
                return null;
            }

            public virtual void PrepareGui(Ride ride)
            {
                ride.ui = new RideUI { ride = ride, colorTitleBG = ColorPalette.RustyOrange.rustString };
                ride.ui.PrepareGui();
            }

            public virtual object Update(Ride ride, params object[] args)
            {
                return null;
            }

            public virtual void ManualEjection(Ride ride, BasePlayer player)
            {
                //call the base afterwards.
                //it exits players nearby.

                PlayerExitNearby(ride, player);
            }

            public virtual void WhenPlayerDisconnected(Ride ride, BasePlayer player, string reason)
            {
                ManualEjection(ride, player);
            }

            public virtual object WhenPlayerDeath(Ride ride, BasePlayer player, HitInfo info)
            {
                ManualEjection(ride, player);
                return null;
            }

            public virtual bool CanPlayerUseRide(Ride ride, BasePlayer player, bool tellPlayer = true)
            {
                //FIRST AND FOREMOST: if the player is an admin or has a proper permission, the answer is yes.
                //if that's not the case...

                if (ride.rideData.onlyAuthorizedCanRide)
                {
                    if (IsPlayerAuthorisedOnRide(ride, player))
                    {
                        return true;
                    }
                    else
                    {
                        if (tellPlayer)
                        {
                            Instance.TellMessage(player, MSG(MSG_ONLY_AUTHORISED_CAN_USE_RIDE, player.UserIDString), ride.rideData.nickname);
                        }
                        return false;
                    }
                }
                else return true;
            }

            public virtual bool CanPlayerOperateRide(Ride ride, BasePlayer player, bool tellPlayer = true)
            {
                //FIRST AND FOREMOST: if the player is an admin or has a proper permission, the answer is yes.
                //if that's not the case...

                if (ride.rideData.onlyAuthorizedCanOperate)
                {
                    if (IsPlayerAuthorisedOnRide(ride, player))
                    {
                        return true;
                    }
                    else
                    {
                        if (tellPlayer)
                        {
                            Instance.TellMessage(player, MSG(MSG_ONLY_AUTHORISED_CAN_CONTROL, player.UserIDString), ride.rideData.nickname);
                        }

                        return false;
                    }
                }
                else return true;
            }

            public virtual bool IsPlayerAuthorisedOnRide(Ride ride, BasePlayer player)
            {
                if (ride.structure.containerAsTC != null)
                {
                    return ride.structure.containerAsTC.IsAuthed(player);
                }
                else
                {
                    if (!ride.rideData.hasLock)
                    {
                        if (player.userID == ride.rideData.OwnerID)
                        {
                            return true;
                        }
                        else
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (player.userID == ride.structure.baseLock.OwnerID)
                            return true;

                        if (ride.rideData.isCodeLock)
                        {
                            if (ride.structure.baseLockAsCodeLock.guestPlayers.Contains(player.userID) || ride.structure.baseLockAsCodeLock.whitelistPlayers.Contains(player.userID))
                            {
                                return true;
                            }
                            else
                            {
                                return false;
                            }
                        }
                        else //must be a keylock
                        {
                            foreach (Item itemId in player.inventory.FindItemIDs(AmusementRidesPlugin.ITEM_LOCK_KEY))
                            {
                                if (itemId.instanceData != null && itemId.instanceData.dataInt == ride.structure.baseLockAsKeyLock.keyCode)
                                    return true;
                            }
                        }
                    }
                }


                return true;
            }

            //to be called more often when stuff is moving

            public void RefreshEntities(Ride ride, BasePlayer player)
            {
                //TEST:
                if (player == null)
                {
                    return;
                }

                //added boombox for tests, seems to be OK?!

                if (ride.RefreshQueue == null)
                {
                    var refreshList = Facepunch.Pool.GetList<BaseEntity>();

                    foreach (var entry in ride.structure.allEntities)
                    {
                        var e = entry.Value;

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_BOOMBOX)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_GENERATOR)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_SPLITTER)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_SIRENLIGHT)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_VENDING_MACHINE)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_OIL_BARREL)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_DIESEL_BARREL)
                        {
                            continue;
                        }

                        if (e.PrefabName == AmusementRidesPlugin.PREFAB_LADDER)
                        {
                            continue;
                        }

                        if (e is ISignage)
                        {
                            continue;
                        }

                        if (e is BaseSaddle)
                        {
                            continue;
                        }

                        if (e is BaseRidableAnimal)
                        {
                            continue;
                        }

                        if (e.GetParentEntity() == null)
                        {
                            continue;
                        }

                        //if (player == null)
                        {
                            if (e.PrefabName == AmusementRidesPlugin.PREFAB_SPEAKER)
                            {
                                continue;
                            }
                        }
                        

                        if (ride.structure.immobileStabilityEntities.ContainsKey(e.net.ID))
                        {
                            continue;
                        }

                        refreshList.Add(e);
                    }

                    ride.RefreshQueue = refreshList.ToArray();

                    Facepunch.Pool.FreeList(ref refreshList);
                }

                ServerMgr.Instance.StartCoroutine(RefreshCoroutine(ride, player));
            }

            public IEnumerator RefreshCoroutine(Ride ride, BasePlayer player)
            {
                if (ride == null)
                {
                    yield break;
                }

                SendInfo sendInfo = player == null ? new SendInfo(ride.playersNearbyConnectionsList) : new SendInfo(player.net.connection);
                BaseEntity entity;

                for (var i = 0; i < ride.RefreshQueue.Length; i++)
                {
                    if (ride == null)
                    {
                        yield break;
                    }

                    entity = ride.RefreshQueue[i];

                    if (!BaseNetworkableEx.IsValid(entity))
                    {
                        continue;
                    }

                    if (entity._limitedNetworking)
                    {
                        continue;
                    }

                    if (Network.Net.sv.IsConnected())
                    {
                        NetWrite netWrite = Network.Net.sv.StartWrite();
                        netWrite.PacketID(Message.Type.EntityDestroy);
                        netWrite.UInt32(entity.net.ID);
                        netWrite.Send(sendInfo);
                    }

                    entity.SendNetworkUpdateImmediate();
                    entity.UpdateNetworkGroup();

                    yield return new WaitForSeconds(0.0001F);

                }

                //and now the connected speakers all at once.
                if (player == null)
                {
                    yield break;
                }

                if (ride.structure.boombox == null)
                {
                    yield break;
                }

                if (!ride.structure.boombox.IsOn())
                {
                    yield break;
                }

                for (var i = 0; i < ride.structure.allSpeakersArray.Length; i++)
                {
                    var speaker = ride.structure.allSpeakersArray[i];

                    speaker.ClientRPCPlayerAndSpectators(null, player, "Client_StopPlayingAudio", ride.structure.boombox.net.ID);
                    speaker.Invoke(() =>
                    {
                        speaker.ClientRPCPlayerAndSpectators(null, player, "Client_PlayAudioFrom", ride.structure.boombox.net.ID);

                    }, 0.25F);
                }

                yield break;
            }

            public virtual bool OnRideTriggerEnter(Ride ride, Collider col, string segmentName = null)
            {
                if (col == null) return false;

                BaseEntity entity = col.ToBaseEntity();
                if (entity == null)
                {
                    return false;
                }
                else
                {
                    BasePlayer player = entity as BasePlayer;
                    if (player != null)
                    {
                        if (!player.IsConnected) return false;
                        if (!player.Connection.isAuthenticated) return false;
                        if (!player.IsAlive()) return false;
                        if (!player.IsFullySpawned() || player.IsSleeping())
                        {
                            return false;
                        }

                        if (segmentName == null)
                        {
                            return PlayerEnterNearby(ride, player);
                        }
                        else return PlayerEnterSegmentTrigger(ride, player, segmentName);
                    }
                    else
                    {
                        if (!Instance.entityToRide.ContainsKey(entity.net.ID))
                        {
                            return EntityEnterNearby(ride, entity);
                        }
                        else return false;
                    }
                }
            }

            public virtual bool OnRideTriggerExit(Ride ride, Collider col, string segmentName = null)
            {
                if (col == null) return false;

                BaseEntity entity = col.ToBaseEntity();
                if (entity == null)
                {
                    return false;
                }
                else
                {
                    BasePlayer player = entity as BasePlayer;
                    if (player != null)
                    {
                        if (player == null) return false;
                        if (!player.IsConnected) return false;
                        if (!player.Connection.isAuthenticated) return false;
                        if (!player.IsAlive()) return false;

                        if (segmentName == null)
                        {
                            return PlayerExitNearby(ride, player);
                        }
                        else return PlayerExitSegmentTrigger(ride, player, segmentName);
                    }
                    else
                    {
                        if (!Instance.entityToRide.ContainsKey(entity.net.ID))
                        {
                            return EntityExitNearby(ride, entity);
                        }
                        else return false;
                    }
                }
            }

            public virtual bool EntityEnterNearby(Ride ride, BaseEntity entity, params object[] args)
            {
                return true;
            }

            public virtual bool EntityExitNearby(Ride ride, BaseEntity entity, params object[] args)
            {
                return true;
            }

            public virtual bool PlayerEnterNearby(Ride ride, BasePlayer player, params object[] args)
            {
                if (!Instance.playerIDtoRidesNearby.ContainsKey(player.userID))
                {
                    Instance.playerIDtoRidesNearby.Add(player.userID, new List<Ride>());
                }

                if (!Instance.playerIDtoRidesNearby[player.userID].Contains(ride))
                {
                    Instance.playerIDtoRidesNearby[player.userID].Add(ride);
                }

                Instance.timer.Once(1F, () =>
                {
                    if (ride != null)
                    {
                        if (player != null)
                        {
                            if (ride.playersNearby.ContainsKey(player.userID))
                            {
                                RefreshEntities(ride, player);
                                //RefreshBuildingBlocks(ride, true);
                            }
                        }
                    }
                });


                if (!ride.playersNearby.ContainsKey(player.userID))
                {
                    ride.playersNearby.Add(player.userID, player);
                    ride.playersNearbyConnections.Add(player.userID, player.net.connection);

                    ride.playersNearbyConnectionsList = GetNearbyConnections(ride);

                    return true;
                }
                else
                {
                    return false;
                }
            }

            public virtual bool PlayerExitNearby(Ride ride, BasePlayer player, params object[] args)
            {
                if (Instance.playerIDtoRidesNearby.ContainsKey(player.userID))
                {
                    if (!player.IsConnected)
                    {
                        Instance.playerIDtoRidesNearby.Remove(player.userID);
                    }
                    else
                    {
                        Instance.playerIDtoRidesNearby[player.userID].Remove(ride);
                    }
                }

                if (ride.playersNearby.ContainsKey(player.userID))
                {
                    ride.playersNearby.Remove(player.userID);
                    ride.playersNearbyConnections.Remove(player.userID);

                    ride.playersNearbyConnectionsList = GetNearbyConnections(ride);
                    return true;
                }
                else
                {
                    return false;
                }
            }

            public virtual bool PlayerEnterSegmentTrigger(Ride ride, BasePlayer player, string segmentName)
            {
                return true;
            }

            public virtual bool PlayerExitSegmentTrigger(Ride ride, BasePlayer player, string segmentName)
            {
                return true;
            }

            public virtual object RideStart(Ride ride, bool forced = false)
            {
                //RefreshEntities(ride);

                if (!forced && !AnybodyLeftRiding(ride))
                {
                    RideStartFailure(ride);
                }
                else
                {
                    RideStartSuccess(ride);
                }

                return null;
            }

            public virtual object RideStartFailure(Ride ride)
            {
                ride.startingSoon = false;
                BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_NOT_STARTING));
                CleanSlate(ride);
                return null;
            }

            public virtual object RideStartSuccess(Ride ride)
            {
                ride.running = true;
                ride.startingSoon = false;

                BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_HAS_STARTED));

                ride.rideStartedAt = DateTime.UtcNow;

                PlayRandomMusicOrContinueExisting(ride);

                return null;
            }

            public virtual void PlayRandomMusicOrContinueExisting(Ride ride)
            {
                if (!Instance.configData.allowMusic)
                {
                    return;
                }

                if (!ride.rideData.hasAnyMusicTracks)
                {
                    return;
                }

                //just to force it through the speakers if already playing?

                if (!ride.rideData.musicAutoMode)
                {
                    return;
                }

                if (!ride.rideData.hasAnyMusicTracks)
                {
                    return;
                }

                if (ride.rideData.playlist == null)
                {
                    return;
                }

                var rndMusic = Instance.RandomValuesFromList(ride.rideData.playlist).Take(1).First();

                ride.BoomboxStartPlaying(rndMusic);
                    
                
            }

            public virtual bool AnybodyLeftRiding(Ride ride)
            {
                var foundOne = false;

                foreach (var chair in ride.structure.mountables)
                {
                    if (chair.Value.GetMounted() != null)
                    {
                        foundOne = true;
                        break;
                    }
                }
                return foundOne;
            }

            public virtual object RideStop(Ride ride, params object[] args)
            {
                ride.running = false;
                ride.rideEndedAt = DateTime.UtcNow;

                ride.startingSoon = false;
                ride.reversing = false;

                CleanSlate(ride);

                BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_IS_OVER));

                ride.BoomboxStopPlaying();

                return null;
            }

            public virtual void CleanSlate(Ride ride, params object[] args)
            {
                ride.mountableToPlayer.Clear();
                ride.playerToMountable.Clear();
                ride.paidAlready.Clear();
            }

            public void RideStartingSoon(Ride ride, BasePlayer player)
            {
                if (ride.running) return;

                //get wait time should be a method, based on settings and min/max
                BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_STARTING_SOON, null, player.displayName, ride.definition.minWaitTime));//, player);

                ride.startingSoon = true;

                Instance.timer.Once(ride.definition.minWaitTime, () => RideStart(ride));
            }

            public virtual void UpdatePivots(Ride ride, params object[] args)
            {

            }

            public virtual void DismountEveryone(Ride ride, params object[] args)
            {
                foreach (var entry in ride.structure.mountables)
                {
                    var maybeMounted = entry.Value.GetMounted();
                    if (maybeMounted != null)
                    {
                        DismountFromMountable(ride, entry.Value);
                    }
                }
            }

            public virtual void DismountFromMountable(Ride ride, BaseMountable chair, bool gracefully = false)
            {
                var maybeMounted = chair.GetMounted();

                if (maybeMounted != null)
                {
                    //this will call the hook
                    chair.DismountPlayer(maybeMounted, true);

                }
            }

            public virtual void ApplyDismountState(Ride ride, BasePlayer player, params object[] args)
            {

                //what happens to the player when they're forcefully dismounted
                //effects and shit, maybe messages
                return;
            }

            public virtual object IfUseLockedEntity(Ride ride, BasePlayer player, BaseEntity entity)
            {
                if (entity == ride.structure.baseLock)
                {
                    return null;
                }

                return false;
            }

            public virtual void WhenLootEntity(Ride ride, BasePlayer player, BaseEntity entity)
            {
                if (entity == ride.structure.container)
                {
                    ride.ui.GuiOpen(player);
                }
                return;
            }

            public virtual void WhenLootEntityEnd(Ride ride, BasePlayer player, BaseEntity entity)
            {
                if (entity == ride.structure.container)
                {
                    ride.ui.GuiClose(player);
                }
                return;
            }

            public virtual object IfPickupEntity(Ride ride, BasePlayer player, BaseEntity entity)
            {
                if (ride.rideData.isAdminRide)
                {
                    //skip all the checks, admins can just pick it up
                    if (IsAdmin(player))
                    {
                        Instance.PlayerTryPickupRide(ride, player);
                    }
                    else
                    {
                        Instance.TellMessage(player, MSG(MSG_CANT_PICKUP_ADMIN_RIDE, player.UserIDString), ride.rideData.nickname);
                    }
                }
                else

                if (ride.structure.container.health == ride.definition.containerHealth)
                {
                    if (!ride.rideData.hasLock)
                    {
                        //at the very end, check the container.
                        var foundItem = false;

                        for (var slot = 0; slot < ride.structure.container.inventory.capacity; slot++)
                        {
                            var currentItem = ride.structure.container.inventory.GetSlot(slot);

                            if (currentItem != null)
                            {
                                foundItem = true;
                                break;
                            }
                        }

                        if (!foundItem)
                        {
                            Instance.PlayerTryPickupRide(ride, player);
                        }
                        else
                        {

                            Instance.TellMessage(player, MSG(MSG_PICKUP_CONTAINER_NEEDS_TO_BE_EMPTY, player.UserIDString), ride.rideData.nickname);
                        }

                    }
                    else
                    {
                        Instance.TellMessage(player, MSG(MSG_PICKUP_LOCK_NEEDS_TO_BE_REMOVED, player.UserIDString), ride.rideData.nickname);
                    }
                }
                else
                {
                    Instance.TellMessage(player, MSG(MSG_PICKUP_CONTAINER_NEEDS_REPAIR, player.UserIDString), ride.rideData.nickname);
                }

                return false;
            }

            public virtual object IfEnterSegmentCollider(Ride ride, BasePlayer player, string segmentColliderName, Transform boxTransform)
            {
                return null;
            }

            public virtual object IfExitSegmentCollider(Ride ride, BasePlayer player, string segmentColliderName)
            {
                return null;
            }

            public object WhenCupboardUpdate(Ride ride, BuildingPrivlidge privilege, BasePlayer player)
            {
                Instance.NextTick(() =>
                {
                    ride.ContainerToData();
                });
                return null;
            }

            public virtual object IfMountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                return null;
            }

            public virtual object IfDismountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                //here you check if you can even dismount while the ride is running.
                return null;
            }


            public virtual void ForceDismountEntity(Ride ride, BasePlayer player, BaseMountable mountable)
            {
                //here you check if you can even dismount while the ride is running.
            }

            public virtual void OnPlayerLaunchLanded(Ride ride, BasePlayer player, bool isPlayerAlive, Vector3 ejectionPosition, float ejectionTime, float ejectionVelocity, float firstImpactVelocity, float airTime, float airDistance, float airHeight, float damageTaken, float joulesTaken, float totalTravelTime, float totalTravelDistance)
            {
                //var aliveStuff = isPlayerAlive ? $"<color=green>{player.displayName} survived the fall!</color>" : $"<color=red>{player.displayName} did not survive the fall.</color>";
                string aliveStuff;
                string msg;

                if (isPlayerAlive)
                {
                    if (damageTaken < 5F)
                    {
                        msg = "LandingMessageSurvived5";
                    }
                    else if (damageTaken < 10F)
                    {
                        msg = "LandingMessageSurvived10";
                    }
                    else if (damageTaken < 25F)
                    {
                        msg = "LandingMessageSurvived25";
                    }
                    else if (damageTaken < 50F)
                    {
                        msg = "LandingMessageSurvived50";
                    }
                    else if (damageTaken < 90F)
                    {
                        msg = "LandingMessageSurvived90";
                    }
                    else if (damageTaken <= 100F)
                    {
                        msg = "LandingMessageSurvived100";
                    }
                    else
                    {
                        msg = "LandingMessageSurvivedMax";
                    }
                }
                else
                {
                    if (damageTaken < 5F)
                    {
                        msg = "LandingMessageDied5";
                    }
                    else if (damageTaken < 10F)
                    {
                        msg = "LandingMessageDied10";
                    }
                    else if (damageTaken < 25F)
                    {
                        msg = "LandingMessageDied25";
                    }
                    else if (damageTaken < 50F)
                    {
                        msg = "LandingMessageDied50";
                    }
                    else if (damageTaken < 90F)
                    {
                        msg = "LandingMessageDied90";
                    }
                    else if (damageTaken < 100F)
                    {
                        msg = "LandingMessageDied100";
                    }
                    else if (damageTaken < 150F)
                    {
                        msg = "LandingMessageDied150";
                    }
                    else if (damageTaken < 250F)
                    {
                        msg = "LandingMessageDied250";
                    }
                    else
                    {
                        msg = "LandingMessageDiedMax";
                    }
                }
                aliveStuff = MSG(msg, null, player.displayName);

                var buildString = $"{aliveStuff}\n {MSG(MSG_LANDING_FULL_STATS, null, ejectionVelocity.ToString("0.00"), (ejectionVelocity * METERS_PER_SECOND_TO_MPH_MULTIPLIER).ToString("0.00"), airHeight.ToString("0.00"), firstImpactVelocity.ToString("0.00"), (firstImpactVelocity * METERS_PER_SECOND_TO_MPH_MULTIPLIER).ToString("0.00"), airTime.ToString("0.00"), airDistance.ToString("0.00"), damageTaken.ToString("0.00"), joulesTaken.ToString("0.00"), totalTravelTime.ToString("0.00"), totalTravelDistance.ToString("0.00"))}";

                BroadcastNearby(ride, buildString);//, player);
            }

            public virtual void LaunchPlayer(Ride ride, BasePlayer player, BaseEntity usingEntity, params object[] args)
            {
                //launch the player off of the chair!
                var mountable = usingEntity as BaseMountable;

                if (mountable != null)
                {

                    var velocity = ride.structure.mountableCurrentVelocity[mountable];

                    var magnitude = ride.structure.mountableCurrentMagnitude[mountable];

                    var correction = ride.structure.mountableCurrentUp[mountable] * 0.8F;

                    var launchedItem = Instance.DropItem(ride.structure.mountablePreviousPosition[mountable] + correction, ride.structure.mountablePreviousRotation[mountable], velocity, false, true, -1579932985); //raw chicken breast cause why not

                    var ejector = launchedItem.gameObject.AddComponent<PlayerEjector>();

                    //spawn a new chair in...

                    var chair = Instance.SummonEntity("assets/bundled/prefabs/static/chair.invisible.static.prefab", launchedItem, new Vector3(0, -0.6F, 0), Vector3.zero.normalized, 0, true) as BaseMountable;

                    ejector.chair = chair;
                    ejector.player = player;
                    ejector.pivot = launchedItem;

                    //next frame because the player is still technically mounted

                    Instance.NextFrame(() =>
                    {
                        player.MountObject(chair);
                        ejector.Prepare(ride);
                    });
                }
            }

            public virtual object PlayerTryGetOn(Ride ride, BasePlayer player, BaseEntity usingEntity = null, string segmentName = "")
            {
                if (!Instance.playerIDtoRide.ContainsKey(player.userID))
                {
                    Instance.playerIDtoRide.Add(player.userID, ride);
                }
                else //when switching from ride to ride directly, can happen
                {
                    Instance.playerIDtoRide[player.userID] = ride;
                }

                object result = null;

                if (CanPlayerUseRide(ride, player, true))
                {
                    if (ride.running && !ride.definition.canJoinLate)
                    {
                        Instance.TellMessage(player, MSG(MSG_CANT_GET_ON_RIDE_NOT_OVER, player.UserIDString), ride.rideData.nickname);
                        return false;
                    }
                    else
                    {
                        if (!HasPaidAlready(ride, player))
                        {
                            string feeFormatted = ride.AdmissionFeeFormatted(player);

                            if (!CanPlayerAfford(ride, player))
                            {
                                Instance.TellMessage(player, MSG(MSG_CANT_GET_ON_NEED_CURRENCY, player.UserIDString, feeFormatted), ride.rideData.nickname);
                                return false;
                            }
                            else
                            {
                                ChargePlayer(ride, player, true);
                                if (usingEntity != null)
                                {
                                    var mountable = usingEntity as BaseMountable;

                                    if (mountable != null)
                                    {
                                        ride.playerToMountable.Add(player, mountable);
                                        ride.mountableToPlayer.Add(mountable, player);
                                    }
                                }
                            }
                        }
                        else
                        {
                            BroadcastNearby(ride, MSG(MSG_BROADCAST_PLAYER_BACK_ON_RIDE, null, player.displayName));//, player);
                            ride.paidAlready[player.userID] = true;
                        }


                        if (ride.startingSoon == false)
                        {
                            RideStartingSoon(ride, player);
                        }
                        else
                        {
                            if (!ride.definition.canJoinLate)
                            {
                                BroadcastNearby(ride, MSG(MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_CANT_JOIN_LATE, null, player.displayName, ride.structure.mountables.Where(m => !ride.mountableToPlayer.ContainsKey(m.Value)).Count()));//, player);
                            }
                            else
                            {
                                BroadcastNearby(ride, MSG(MSG_BROADCAST_PLAYER_HOPPED_IN_TIME_AND_CAN_JOIN_LATE, null, player.displayName));//, player);
                            }
                            //make a case for limited/unlimited seats
                        }

                    }
                }
                else result = false;

                return result;
            }

            public object PlayerTryGetOff(Ride ride, BasePlayer player, BaseEntity usingEntity = null, string segmentName = "")
            {
                //if the player is dead/disconnected
                if (Instance.playerIDtoRide.ContainsKey(player.userID))
                {
                    Instance.playerIDtoRide.Remove(player.userID);
                }

                string msg = "";

                if (!ride.paidAlready.ContainsKey(player.userID))
                {
                    return null;
                }

                if (ride.running)
                {
                    //loser
                    if (player.IsConnected)
                    {
                        if (player.IsAlive())
                        {
                            msg = MSG(MSG_BROADCAST_PLAYER_GOT_OFF_DISMOUNTED, null, player.displayName);
                            ApplyDismountState(ride, player, usingEntity);
                        }
                        else
                        {
                            msg = MSG(MSG_BROADCAST_PLAYER_GOT_OFF_DIED, null, player.displayName);
                        }

                    }
                    else
                    {
                        msg = MSG(MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED, null, player.displayName);
                    }
                }
                else
                {
                    if (DateTime.UtcNow.Subtract(ride.rideEndedAt).TotalSeconds > 0.1F)
                    {
                        //meh.
                        if (player.IsConnected)
                        {
                            if (player.IsAlive())
                            {
                                msg = MSG(MSG_BROADCAST_PLAYER_GOT_OFF_COLD_FEET, null, player.displayName);
                                ApplyDismountState(ride, player, usingEntity);
                            }
                            else
                            {
                                msg = MSG(MSG_BROADCAST_PLAYER_GOT_OFF_DIED_BEFORE_RIDE_BEGAN, null, player.displayName);
                            }
                        }
                        else
                        {
                            msg = MSG(MSG_BROADCAST_PLAYER_GOT_OFF_DISCONNECTED_BEFORE_RIDE_BEGAN, null, player.displayName);
                        }

                    }
                }

                if (ride.running)
                {
                    if (ride.definition.reversable == true)
                    {
                        if (ride.reversing == false)
                        {
                            Instance.NextTick(() =>
                            {

                                if (!AnybodyLeftRiding(ride) && ride.reversing == false)
                                {
                                    ride.reversing = true;
                                    BroadcastNearby(ride, MSG(MSG_BROADCAST_NOBODY_LEFT_STOPPING_SOON));
                                }
                            });
                        }
                    }
                    else
                    {
                        Instance.NextTick(() =>
                        {

                            if (!AnybodyLeftRiding(ride))
                            {
                                BroadcastNearby(ride, MSG(MSG_BROADCAST_NOBODY_LEFT_STOPPING_NOW));
                                RideStop(ride);
                            }
                        });
                    }

                }

                ride.paidAlready[player.userID] = false;

                if (msg != "")
                {
                    BroadcastNearby(ride, msg);//, player);
                }

                //unless we're returning false...
                var mountable = usingEntity as BaseMountable;

                if (mountable != null)
                {
                    ride.playerToMountable.Remove(player);
                    ride.mountableToPlayer.Remove(mountable);
                }

                return null;
            }

            public virtual bool CanPlayerAfford(Ride ride, BasePlayer player)
            {
                if (ride.rideData.admissionFeePrice == 0) return true;
                if (ride.rideData.dontChargeAuthorized)
                {
                    if (IsPlayerAuthorisedOnRide(ride, player))
                    {
                        return true;
                    }
                }

                return player.inventory.containerBelt.GetAmount(ride.rideData.admissionFeeCurrencyID, true) + player.inventory.containerMain.GetAmount(ride.rideData.admissionFeeCurrencyID, true) >= ride.rideData.admissionFeePrice;
            }

            public virtual bool HasPaidAlready(Ride ride, BasePlayer player)
            {
                return ride.paidAlready.ContainsKey(player.userID);
            }

            public virtual object ChargePlayer(Ride ride, BasePlayer player, bool currentlyMounted = true)
            {
                if (HasPaidAlready(ride, player))
                {
                    Instance.PrintError($"ERROR: Trying to charge {player.displayName} twice for some reason on {ride.name} ({ride.rideData.nickname})");
                    return false;
                }

                if (ride.rideData.dontChargeAuthorized)
                {
                    if (IsPlayerAuthorisedOnRide(ride, player))
                    {
                        Instance.TellMessage(player, MSG(MSG_ADMISSION_RIDE_FREE_OF_CHARGE_PLAYER_AUTHORISED, player.UserIDString), ride.rideData.nickname);
                        ride.paidAlready.Add(player.userID, currentlyMounted);
                        return null;
                    }
                }

                if (ride.rideData.admissionFeePrice < 1)
                {
                    Instance.TellMessage(player, MSG(MSG_ADMISSION_RIDE_FREE_OF_CHARGE_NO_FEE, player.UserIDString), ride.rideData.nickname);
                    ride.paidAlready.Add(player.userID, currentlyMounted);
                    return null;
                }

                ride.paidAlready.Add(player.userID, currentlyMounted);
                //first charge from container, then from main
                var amount = player.inventory.Take(null, ride.rideData.admissionFeeCurrencyID, ride.rideData.admissionFeePrice);
                Instance.CreateEffectForAt("purchase", player);

                //oh and maybe take skin and condition number into account...

                //now put those items in the container
                if (ride.structure.container != null)
                {
                    var feeItem = ItemManager.CreateByItemID(ride.rideData.admissionFeeCurrencyID, ride.rideData.admissionFeePrice);
                    if (feeItem.MoveToContainer(ride.structure.container.inventory, -1, true))
                    {
                        //OK, maybe a hook? Maybe notify ride owners?
                    }
                    else
                    {
                        feeItem.Remove();
                    }
                }

                Instance.TellMessage(player, MSG(MSG_ADMISSION_PLAYER_PAID_CURRENCY, player.UserIDString, ride.AdmissionFeeFormatted(player)), ride.rideData.nickname);//, player);

                return null;
            }

            public virtual object RefundPlayer(Ride ride, BasePlayer player)
            {
                return null;
            }

            public virtual object AwardPlayer(Ride ride, BasePlayer player, int amount)
            {
                return null;
            }

            public virtual void WhenItemDeployed(Ride ride, Deployer deployer, BaseEntity entityDeployedOn)
            {
                var deployedThingy = entityDeployedOn.GetSlot(deployer.GetDeployable().slot);
                if (deployedThingy != null)
                {
                    var player = deployer.GetOwnerPlayer();

                    var checkPassed = false;
                    //the only allowed circumstance: put keylock/codelock on container

                    if (entityDeployedOn.PrefabName == AmusementRidesPlugin.PREFAB_TC || entityDeployedOn.PrefabName == AmusementRidesPlugin.PREFAB_SMALL_BOX || entityDeployedOn.PrefabName == AmusementRidesPlugin.PREFAB_LARGE_BOX)
                    {
                        if (!(ride.rideData.isAdminRide && !IsAdmin(player)))
                        {

                            if (deployedThingy.PrefabName == AmusementRidesPlugin.PREFAB_LOCK_KEY || deployedThingy.PrefabName == AmusementRidesPlugin.PREFAB_LOCK_CODE)
                            {
                                checkPassed = true;
                            }
                        }
                    }

                    if (!checkPassed)
                    {
                        Instance.TellMessage(player, MSG(MSG_CANT_BUILD_OR_DEPLOY_ON_RIDE, player.UserIDString), ride.rideData.nickname);
                        deployedThingy.Kill(BaseNetworkable.DestroyMode.Gib);
                    }
                    else
                    {
                        ride.LockToData(deployedThingy as BaseLock);
                    }
                }
            }

            public virtual void WhenEntityKill(Ride ride, BaseEntity entity)
            {
                if (ride == null) return;
                if (ride.isAlreadyDestroying) return;

                if (!ride.structure.damagableEntities.ContainsKey(entity.net.ID))
                {
                    BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_REMOVED_BY_ADMIN, null, ride.rideData.nickname));
                }
                else
                {
                    BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_REMOVED_BY_RAIDING, null, ride.rideData.nickname));
                    ride.destroyMode = BaseNetworkable.DestroyMode.Gib;
                    ride.structure.damagableEntities.Remove(entity.net.ID);
                }

                if (ride.structure.immobileStabilityEntities.ContainsKey(entity.net.ID))
                {
                    ride.structure.immobileStabilityEntities.Remove(entity.net.ID);
                }

                if (Instance.entityToRide.ContainsKey(entity.net.ID))
                {
                    Instance.entityToRide.Remove(entity.net.ID);
                }

                if (ride.structure.allEntities.ContainsKey(entity.net.ID))
                {
                    ride.structure.allEntities.Remove(entity.net.ID);
                }

                //if it's a lock entity, picking up will kill it, right?

                var maybeLock = entity as BaseLock;
                if (maybeLock != null)
                {
                    ride.LockToData(null);


                    return;
                }

                Instance.RideRemove(ride.gameObject.name);
            }

            public virtual object WhenEntityTakeDamage(Ride ride, BaseCombatEntity entity, HitInfo info, bool megaDebug = false)
            {
                if (ride.rideData.isAdminRide)
                {
                    if (megaDebug)
                    {
                        Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: ADMIN RIDE, RETURNING TRUE");
                    }

                    return true;
                }

                if (ride.isAlreadyDestroying)
                {
                    if (megaDebug)
                    {
                        Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: ALREADY DESTROYING, RETURNING TRUE");
                    }
                    return true;
                }
                //by default, only the container (tc, box) is a "damagable" entity. Some rides can have more than 1 container

                if (ride.structure.damagableEntities.ContainsKey(entity.net.ID))
                {
                    if (megaDebug)
                    {
                        Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: IT'S A DAMAGABLE ENTITY...");
                    }

                    //still ignore decay damage on those
                    if (info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Decay)
                    {
                        if (megaDebug)
                        {
                            Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: DECAY, RETURNING TRUE");
                        }
                        return true;
                    }
                    else
                    {
                        var total = info.damageTypes.Total();

                        if (total < entity.Health())
                        {
                            if (megaDebug)
                            {
                                Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: TOTAL DAMAGE TO ENTITY IS LESS THAN ENTITY HEALTH, RETURNING NULL");
                            }
                            return null;
                        }
                        else
                        {
                            Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: TOTAL DAMAGE TO ENTITY IS LESS THAN ENTITY HEALTH, RETURNING NULL");

                            ride.destroyMode = BaseNetworkable.DestroyMode.Gib;
                            ride.structure.allEntities.Remove(entity.net.ID);
                            Instance.RideRemove(ride.name);

                            return null;
                        }
                    }
                }
                else
                {
                    if (megaDebug)
                    {
                        Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: IT'S NOT A DAMAGABLE ENTITY...");
                    }

                    //if it's not damagable and the container is a TC, pass the buck!
                    if (ride.structure.container.net.ID != entity.net.ID)
                    {
                        if (megaDebug)
                        {
                            Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: IT'S NOT THE RIDE CONTAINER.");
                        }

                        //ignore horse damage
                        if (entity.PrefabName == AmusementRidesPlugin.PREFAB_HORSE && info.damageTypes.GetMajorityDamageType() == Rust.DamageType.Decay)
                        {
                            if (megaDebug)
                            {
                                Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: HORSE DECAY DAMAGE, RETURNING TRUE...");
                            }
                            return true;
                        }


                        var total = info.damageTypes.Total();

                        if (total <= ride.structure.container.Health())
                        {
                            ride.structure.container.SetHealth(ride.structure.container.health - total);
                        }
                        else
                        {
                            if (megaDebug)
                            {
                                Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: RIDE CONTAINER DYING, RETURNING TRUE.");
                            }

                            ride.destroyMode = BaseNetworkable.DestroyMode.Gib;
                            Instance.RideRemove(ride.name);

                            return true;
                        }
                    }
                    
                    if (ride.structure.container.Health() < 100F)
                    {
                        if (ride.rideData.hasLock)
                        {
                            //destroy the lock if there is one
                            ride.handler.BroadcastNearby(ride, MSG(MSG_BROADCAST_RIDE_LOCK_DESTROYED));
                            ride.structure.baseLock.Kill(BaseNetworkable.DestroyMode.Gib);
                        }

                    }
                    if (megaDebug)
                    {
                        Instance.PrintError($">>>>> MEGA DEBUG FOR {entity.ShortPrefabName} {entity.net.ID}: WE GOT TO THE END, RETURNING TRUE.");
                    }


                    return true;
                }

            }

            public virtual object WhenButtonPress(Ride ride, PressButton button, BasePlayer player)
            {
                if (CanPlayerOperateRide(ride, player))
                {
                    return null;
                }
                else return false;
            }

            public virtual object WhenSwitchToggle(Ride ride, ElectricSwitch electricSwitch, BasePlayer player)
            {
                if (CanPlayerOperateRide(ride, player))
                {
                    return null;
                }
                else return false;
            }

            public virtual object WhenStructureUpgrade(Ride ride, BaseCombatEntity entity, BasePlayer player, BuildingGrade.Enum grade)
            {
                //yeah but maybe, sometimes, you can
                Instance.TellMessage(player, MSG(MSG_CANT_UPGRADE_RIDE_STRUCTURES, player.UserIDString), ride.rideData.nickname);
                return false;
            }

            public virtual void BroadcastNearby(Ride ride, string message)//, BasePlayer involvedPlayer = null)
            {
                if (!ride.rideData.chatEnabled) return;

                foreach (var playah in GetNearbyPlayers(ride))
                {
                    Instance.TellMessage(playah, message, ride.rideData.nickname);//, involvedPlayer);
                }
            }
            public virtual List<BasePlayer> GetNearbyPlayers(Ride ride)
            {
                return ride.playersNearby.Values.ToList();
            }

            public virtual List<Connection> GetNearbyConnections(Ride ride)
            {
                return ride.playersNearbyConnections.Values.ToList();
            }
        }
        //attach to dropped item!
        public class PlayerEjector : AmusementBehaviour
        {
            public BasePlayer player;
            public DroppedItem pivot;
            public BaseMountable chair;
            public Rigidbody rigidbody;

            public Ride ride;

            public Vector3 ejectionPosition;
            public float ejectionTime;

            public float ejectionVelocity;

            public float joulesTaken = 0F;
            public float damageTaken = 0F;

            public Vector3 firstImpactPosition;
            public float firstImpactVelocity;
            public float firstImpactTime;

            public float maxY = float.MinValue;

            //landing position and time are only needed once at the end

            public Vector3 bumpPosition;

            public bool ready = false;

            public float currentTime = float.MinValue;
            public float lastUpdate = float.MinValue;

            public float updateRate = 0.01F;

            public float shouldntMoveFor = 0.5F;

            public float shouldntMoveEstablishedAt = float.MinValue;

            public bool shouldntMoveNow = false;

            public bool airborne = true;

            void LateUpdate()
            {
                if (!ready) return;
                if (rigidbody == null) return;

                currentTime = UnityEngine.Time.time;

                if (currentTime < lastUpdate + updateRate) return;

                if (pivot.transform.position.y > maxY)
                {
                    maxY = pivot.transform.position.y;
                }

                if (player.WaterFactor()>0F)
                {
                    Eject();
                    return;
                }

                if (!shouldntMoveNow)
                {
                    //check if you stopped moving for 3 seconds
                    if (rigidbody.velocity.magnitude < 0.1F)
                    {
                        shouldntMoveNow = true;
                        shouldntMoveEstablishedAt = currentTime;
                        //player.StartWounded();
                    }
                }
                else
                {
                    if (rigidbody.velocity.magnitude < 0.1F)
                    {
                        if (currentTime >= shouldntMoveEstablishedAt + shouldntMoveFor)
                        {
                            //that's it I guess
                            Eject();
                        }
                        else
                        {
                            if (rigidbody.velocity.magnitude >= 0.1F)
                            {
                                shouldntMoveNow = false;
                                player.StopWounded();
                            }
                            //cancel
                        }


                    }
                }



                lastUpdate = currentTime;
            }

            //collider to check if it should be destroyed too
            //doing the collider, eh?

            //void Awake()

            //do this after telling the player, pivot, original chair
            public void Eject(bool brutally = false)
            {
                var maybeMounted = chair.GetMounted();

                if (maybeMounted != null)
                {
                    //this will call the hook
                    chair.DismountPlayer(maybeMounted, true);

                }

                if (player != null)
                {
                    if (player.IsConnected)
                    {
                        player.Teleport(pivot.transform.position);

                        if (player.IsAlive())
                        {
                            if (player.IsWounded())
                            {
                                player.StopWounded();
                            }
                        }
                        //we got all the info now.
                        //-ejection velocity
                        //-first impact velocity
                        //calculate travel time and distances
                        //var buildString = brutally ? "<color=red>Fatal accident! </color>" : "<color=green>You survived! </color>";

                        if (!Instance.isUnloading)
                        {
                            var totalTravelTime = Time.time - ejectionTime;
                            var totalTravelDistance = Vector3.Distance(ejectionPosition, transform.position);

                            var airTime = firstImpactTime - ejectionTime;
                            var airDistance = Vector3.Distance(ejectionPosition, firstImpactPosition);


                            //now tell all of that to the handler!
                            ride.handler.OnPlayerLaunchLanded(ride, player, !brutally, ejectionPosition, ejectionTime, ejectionVelocity, firstImpactVelocity, airTime, airDistance, maxY - ejectionPosition.y, damageTaken, joulesTaken, totalTravelTime, totalTravelDistance);

                        }

                        //Instance.TellMessage(player, buildString);
                    }

                }

                chair.Kill(ride.destroyMode); pivot.Kill(ride.destroyMode);
            }

            void OnCollisionEnter(Collision collision)
            {
                if (player == null)
                {
                    Eject();
                    return;
                }

                if (!player.IsConnected)
                {
                    Eject();
                    return;
                }

                if (!player.IsAlive())
                {
                    Eject(true);
                    return;
                }

                var velocity = collision.relativeVelocity.magnitude;

                var kineticEnergy = (AVERAGE_HUMAN_BODY_WEIGHT / 2F) * velocity * velocity;

                var damageToBody = kineticEnergy / MAX_KINETIC_ENERGY_AT_100_DAMAGE * 100F;

                damageTaken += damageToBody;
                joulesTaken += kineticEnergy;

                if (airborne)
                {
                    //we only do it once
                    firstImpactPosition = transform.position;
                    firstImpactTime = Time.time;
                    firstImpactVelocity = velocity;
                    chair.transform.localPosition = new Vector3(0, 0, -0.6F);
                    chair.transform.hasChanged = true;
                    //chair.SendNetworkUpdateImmediate();
                    //player.StartWounded();


                    airborne = false;
                }

                Instance.CreateEffectForAt(RandomHurtFx(), player);

                if (!player.net.connection.info.GetBool("global.god", false))
                {
                    if (damageToBody >= player.Health())
                    {
                        //eject the player before hurting them!

                        Eject(true);

                        //don't return. you still wanna hurt them
                    }
                }

                Instance.NextFrame(() =>
                {
                    //player.SetHealth(player.Health() - damageToBody);
                    player.Hurt(damageToBody, Rust.DamageType.Collision, null, false);
                });

            }

            public void Prepare(Ride ride)
            {
                this.ride = ride;

                ejectionPosition = transform.position;
                ejectionTime = Time.time;

                rigidbody = pivot.gameObject.GetComponent<Rigidbody>();

                ejectionVelocity = rigidbody.velocity.magnitude;
                ready = true;
            }
        }

        public class RideStructureFerrisWheelSpecific
        {
            public DroppedItem rotorPivot = null;

            public DroppedItem[] gondolaPivotArray = null;

            public DroppedItem[][] gondolaWallPivotArray = null;
        }

        public class RideStructureMerryGoRoundSpecific
        {
            public DroppedItem rotorPivot = null;

            public DroppedItem[] polePivots = null;
        }

        public class Ride : AmusementBehaviour
        {
            public bool isAlreadyDestroying = false;
            public BaseNetworkable.DestroyMode destroyMode = BaseNetworkable.DestroyMode.None;
            //attach to a new game object
            //has an influence collider radius according to definition

            //this is what gets loaded...
            public RideDefinition definition;

            //and turned into this
            public RideStructure structure;
            public RideStructureFerrisWheelSpecific structureSpecificFerrisWheel = null;
            public RideStructureMerryGoRoundSpecific structureSpecificMerryGoRound = null;

            //state is the last step
            public RideHandler handler;

            public RideUI ui;

            public bool initialized = false;

            //dynamic state stuff
            public DateTime rideStartedAt = DateTime.MinValue;
            public DateTime rideEndedAt = DateTime.MinValue;

            public Dictionary<ulong, BasePlayer> playersNearby = new Dictionary<ulong, BasePlayer>();
            //cache connections at the same time too
            public Dictionary<ulong, Connection> playersNearbyConnections = new Dictionary<ulong, Connection>();

            //cached list every time a player join/leaves nearby
            public List<Connection> playersNearbyConnectionsList = new List<Connection>();

            public Dictionary<BasePlayer, BaseMountable> playerToMountable = new Dictionary<BasePlayer, BaseMountable>();
            public Dictionary<BaseMountable, BasePlayer> mountableToPlayer = new Dictionary<BaseMountable, BasePlayer>();

            public Dictionary<ulong, bool> paidAlready = new Dictionary<ulong, bool>();

            public bool open = true;
            public bool startingSoon = false;
            public bool running = false;
            public bool stopping = false;
            public bool reversing = false;

            public float delta = 0;
            public float deltaPlus = 0.1F;
            public float deltaMinus = -0.1F;
            public float deltaMax = 100;
            public float deltaNormalized = 0;

            public RideColliderSphere influenceSphere;

            public float intensity = 1;

            public RideData rideData;

            public Network.Visibility.Group secondaryVisibilityGroup;

            public BaseEntity[] RefreshQueue = null;

            public virtual void Init(Vector3 position, Vector3 rotation, RideDefinition definition, RideData rideData, ulong playerOwnerID, float rideHealth, string forceName, bool isAdminRide, bool justCreated = false)
            {
                secondaryVisibilityGroup = Net.sv.visibility.GetGroup(transform.position);

                structure = new RideStructure(this);
                this.definition = definition;

                var newGameObject = new GameObject();
                newGameObject.name = $"Volume:{name}";

                newGameObject.layer = (int)Rust.Layer.Reserved1;

                newGameObject.transform.SetParent(transform, false);
                newGameObject.SetActive(true);

                structure.rideVolume = newGameObject.AddComponent<RideVolume>();

                //now before we get to the Post Init, the ride will perform some initial collider checks.

                structure.rideVolume.Prepare(this, Vector3.zero, Vector3.zero, definition.rideVolumes);

                Instance.timer.Once(0.25F, () =>
                {
                    structure.rideVolume.InitialCheck(position, rotation, definition, rideData, playerOwnerID, rideHealth, forceName, isAdminRide, justCreated);
                });
            }

            public virtual void PostInit(Vector3 position, Vector3 rotation, RideDefinition definition, RideData rideData, ulong playerOwnerID, float rideHealth, string forceName, bool isAdminRide, bool justCreated = false)
            {
                if (justCreated)
                {
                    var maybePlayer = RustCore.FindPlayerById(playerOwnerID);
                    if (maybePlayer != null)
                    {
                        //teleport the player to the height of the ride + ride correction
                        maybePlayer.Teleport(maybePlayer.transform.position.WithY(position.y+ definition.posAntiCorrectionY));
                    }
                }

                //this is the time to load your settings. if you can't load, take them from definition and save.
                //next time you load there will be there. You don't alter the definition globally since they might
                //be shared among different ride instances.

                if (rideData != null)
                {
                    this.rideData = rideData;
                }
                else
                {
                    this.rideData = new RideData
                    {
                        admissionFeeCurrencyID = ItemManager.FindItemDefinition(definition.admissionFeeCurrency).itemid,
                        admissionFeePrice = definition.admissionFeePrice,
                        definitionName = definition.rideName,
                        definitionVersion = definition.rideVersion,
                        handlerName = definition.handlerName,
                        isAdminRide = isAdminRide,

                        extraData = definition.extraData,
                        hasLock = false,
                        nickname = definition.rideNickname,
                        posX = position.x,
                        posY = position.y,
                        posZ = position.z,
                        rotX = rotation.x,
                        rotY = rotation.y,
                        rotZ = rotation.z,
                        OwnerID = playerOwnerID,
                        health = rideHealth,
                        playlist = definition.assignedMusic,
                        hasAnyMusicTracks = definition.assignedMusic!=null,

                        name = forceName, //this has to stay!
                        //handlerVersion// when handler is applied for the first time!
                    };
                    Instance.storedData.rides.Add(forceName, this.rideData);

                    //if we're going with the data-derived name in the end, make sure you remove the pre-entered ride with the forced name

                    //Instance.storedData.rides.Add(forceName, this.rideData);

                }

                //nickname = definition.rideNickname != null ? definition.rideNickname : definition.rideName;

                //SetAdmissionFee(definition.admissionFeePrice, definition.admissionFeeCurrency);

                DefinitionToStructure();

                ApplySpecialGroupSkins();

                if (Instance.SignArtist != null)
                {
                    ApplySpecialSignData();
                }

                if (!ApplyHandler())
                {
                    Instance.PrintError("ERROR: Something went wrong while trying to apply the handler.");
                }
                else
                {
                    initialized = true;

                    influenceSphere = gameObject.AddComponent<RideColliderSphere>();

                    influenceSphere.PerformPrepare(this, this.definition.influenceRadius);
                }

                UpdateVendingMachine();

                //refresher = gameObject.AddComponent<RideRefresher>();
                //refresher.Init(this);
            }

            public string AdmissionFeeFormatted(BasePlayer player, bool withFormatting = true)
            {
                string userID = player?.UserIDString ?? null;

                if (withFormatting)
                {
                    return rideData.admissionFeePrice == 0 ? MSG(MSG_ADMISSION_FEE_FREE_OF_CHARGE_FORMATTED, userID) : MSG(MSG_ADMISSION_FEE_CURRENCY_FORMATTED, userID, rideData.admissionFeePrice, ItemManager.FindItemDefinition(rideData.admissionFeeCurrencyID).displayName.translated);
                }
                else
                {
                    return rideData.admissionFeePrice == 0 ? MSG(MSG_ADMISSION_FEE_FREE_OF_CHARGE_UNFORMATTED, userID) : MSG(MSG_ADMISSION_FEE_CURRENCY_UNFORMATTED, userID, rideData.admissionFeePrice, ItemManager.FindItemDefinition(rideData.admissionFeeCurrencyID).displayName.translated);
                }

            }

            public void BoomboxToData()
            {
                rideData.boomboxRadioURL = structure.boombox.BoxController.CurrentRadioIp;
                rideData.boomboxIsOn = structure.boombox.HasFlag(BaseEntity.Flags.On);

                //is there a tape inside?

                bool hasSomethingInside = structure.boombox.HasFlag(BaseEntity.Flags.Reserved1);
                bool somethingInsideIsValidTape = false;

                Item firstItemInside = null;
                Cassette cassette = null;

                if (hasSomethingInside)
                {
                    firstItemInside = structure.boombox.inventory.itemList.FirstOrDefault();

                    if (firstItemInside != null)
                    {
                        if (firstItemInside.info.shortname.Contains("cassette"))
                        {
                            if (firstItemInside.instanceData != null)
                            {
                                cassette = BaseNetworkable.serverEntities.Find(firstItemInside.instanceData.subEntity) as Cassette;

                                if (cassette != null)
                                {
                                    somethingInsideIsValidTape = true;
                                }
                            }
                        }
                    }
                }

                if (somethingInsideIsValidTape)
                {
                    rideData.boomboxTapeAudioID = cassette.AudioId;
                    rideData.boomboxTapeSkin = firstItemInside.skin;
                    rideData.boomboxTapeProducer = cassette.CreatorSteamId;
                    rideData.boomboxTapeText = firstItemInside.text;
                }
                else
                {
                    rideData.boomboxTapeAudioID = 0;
                    rideData.boomboxTapeSkin = 0;
                    rideData.boomboxTapeProducer = 0;
                    rideData.boomboxTapeText = null;
                }
            }

            public void DataToBoombox()
            {
                structure.boombox.BoxController.CurrentRadioIp = rideData.boomboxRadioURL;
                structure.boombox.ClientRPC(null, "OnRadioIPChanged", rideData.boomboxRadioURL);

                if (rideData.boomboxTapeAudioID != 0)
                {
                    var maybeFreshTape = Instance.TapeLibrary?.CallHook("ProduceKnownTape", rideData.boomboxTapeAudioID, rideData.boomboxTapeProducer, rideData.boomboxTapeSkin, rideData.boomboxTapeText) as Item ?? null;

                    if (maybeFreshTape != null)
                    {
                        maybeFreshTape.MoveToContainer(structure.boombox.inventory, 0, true, true);

                        if (rideData.boomboxTapeProducer == 1337420)
                        {
                            CassetteGuard.AssignGuard(maybeFreshTape, BaseNetworkable.serverEntities.Find(maybeFreshTape.instanceData.subEntity) as Cassette, structure.boombox.inventory);
                        }
                    }
                    else
                    {
                        Instance.PrintWarning($"TAPE LIBRARY FAILED TO RESTORE A RIDE TAPE. Is Tape Library loaded in?");
                    }
                }

                if (!rideData.musicAutoMode)
                {
                    if (rideData.boomboxIsOn)
                    {
                        structure.boombox.Invoke(() =>
                        {
                            structure.boombox.BoxController.ServerTogglePlay(true);
                        }, 1F); //wait a bit longer, it's okay, the ride is loading in
                    }
                }

            }

            public void LockToData(BaseLock baseLock)
            {
                if (baseLock == null)
                {
                    rideData.hasLock = false;
                    rideData.isLockLocked = false;
                    rideData.isCodeLock = false;
                    rideData.guestCode = "";
                    rideData.code = "";
                    rideData.keyCode = 1;
                    rideData.hasGuestCode = false;
                    rideData.hasCode = false;
                    rideData.whitelistPlayers = new List<ulong>();
                    rideData.guestPlayers = new List<ulong>();
                    rideData.firstKeyCreated = false;

                    structure.baseLock = null;

                    return;
                }

                structure.baseLock = baseLock;

                rideData.isLockLocked = baseLock.HasFlag(BaseEntity.Flags.Locked);
                rideData.OwnerID = baseLock.OwnerID;
                rideData.hasLock = true;

                if (!structure.allEntities.ContainsKey(baseLock.net.ID))
                {
                    structure.allEntities.Add(baseLock.net.ID, baseLock);
                }

                if (!Instance.entityToRide.ContainsKey(baseLock.net.ID))
                {
                    Instance.entityToRide.Add(baseLock.net.ID, this);
                }

                var maybeCodeLock = structure.baseLock as CodeLock;
                if (maybeCodeLock != null)
                {
                    rideData.isCodeLock = true;
                    structure.baseLockAsCodeLock = maybeCodeLock;

                    rideData.code = maybeCodeLock.code;
                    rideData.guestCode = maybeCodeLock.guestCode;
                    rideData.hasGuestCode = maybeCodeLock.hasGuestCode;
                    rideData.hasCode = maybeCodeLock.hasCode;

                    rideData.whitelistPlayers = maybeCodeLock.whitelistPlayers;
                    rideData.guestPlayers = maybeCodeLock.guestPlayers;

                }
                else
                {
                    var maybeKeyLock = structure.baseLock as KeyLock;
                    if (maybeKeyLock != null)
                    {
                        rideData.isCodeLock = false;
                        structure.baseLockAsKeyLock = maybeKeyLock;

                        //for keylocks, the keycode will have been generated I guess?
                        rideData.keyCode = maybeKeyLock.keyCode;
                        rideData.firstKeyCreated = maybeKeyLock.firstKeyCreated;
                    }
                }
            }

            public void DataToLock()
            {
                Instance.NextFrame(() =>
                {
                    if (rideData.hasLock)
                    {
                        if (rideData.isCodeLock)
                        {
                            var newCodelock = GameManager.server.CreateEntity(AmusementRidesPlugin.PREFAB_LOCK_CODE) as CodeLock;//, structure.container.transform.position, structure.container.transform.rotation, true) as CodeLock;
                            newCodelock.SetParent(structure.container, structure.container.GetSlotAnchorName(BaseEntity.Slot.Lock));

                            newCodelock.OwnerID = rideData.OwnerID;

                            //newCodelock.OnDeployed(buildingPrivlidge, player);

                            newCodelock.Spawn();

                            newCodelock.code = rideData.code;
                            newCodelock.guestCode = rideData.guestCode;
                            newCodelock.whitelistPlayers = rideData.whitelistPlayers;
                            newCodelock.guestPlayers = rideData.guestPlayers;

                            newCodelock.hasGuestCode = rideData.hasGuestCode;
                            newCodelock.hasCode = rideData.hasCode;

                            newCodelock.SetFlag(BaseEntity.Flags.Locked, rideData.isLockLocked);

                            structure.baseLock = newCodelock;
                            structure.baseLockAsCodeLock = newCodelock as CodeLock;

                            structure.container.SetSlot(BaseEntity.Slot.Lock, newCodelock);

                            if (!structure.allEntities.ContainsKey(newCodelock.net.ID))
                            {
                                structure.allEntities.Add(newCodelock.net.ID, newCodelock);
                            }

                            if (!Instance.entityToRide.ContainsKey(newCodelock.net.ID))
                            {
                                Instance.entityToRide.Add(newCodelock.net.ID, this);
                            }

                            newCodelock.SendNetworkUpdateImmediate();

                            //LockToData(newCodelock);
                        }
                        else
                        {
                            var newKeyLock = GameManager.server.CreateEntity(AmusementRidesPlugin.PREFAB_LOCK_KEY) as KeyLock;
                            newKeyLock.SetParent(structure.container, structure.container.GetSlotAnchorName(BaseEntity.Slot.Lock));

                            newKeyLock.OwnerID = rideData.OwnerID;

                            newKeyLock.Spawn();

                            newKeyLock.keyCode = rideData.keyCode;
                            newKeyLock.firstKeyCreated = rideData.firstKeyCreated;

                            newKeyLock.SetFlag(BaseEntity.Flags.Locked, rideData.isLockLocked);

                            structure.baseLock = newKeyLock;
                            structure.baseLockAsKeyLock = newKeyLock as KeyLock;

                            if (!structure.allEntities.ContainsKey(newKeyLock.net.ID))
                            {
                                structure.allEntities.Add(newKeyLock.net.ID, newKeyLock);
                            }

                            if (!Instance.entityToRide.ContainsKey(newKeyLock.net.ID))
                            {
                                Instance.entityToRide.Add(newKeyLock.net.ID, this);
                            }

                            structure.container.SetSlot(BaseEntity.Slot.Lock, newKeyLock);

                            //LockToData(newKeyLock);
                        }
                    }
                });
            }

            public void ContainerToData()
            {
                if (definition.containerType == RideContainerType.TC)
                {
                    rideData.authorizedPlayers = new List<ulong>();
                    //get authed players
                    foreach (var entry in structure.containerAsTC.authorizedPlayers)
                    {
                        rideData.authorizedPlayers.Add(entry.userid);
                    }
                }

                rideData.health = structure.container.Health() / definition.containerHealth;

                rideData.containerContents = new Dictionary<int, KeyValuePair<int, int>>();

                for (var slot = 0; slot < structure.container.inventory.capacity; slot++)
                {
                    var currentItem = structure.container.inventory.GetSlot(slot);

                    if (currentItem != null)
                    {
                        rideData.containerContents.Add(slot, new KeyValuePair<int, int>(currentItem.info.itemid, currentItem.amount));
                    }
                }

                structure.container.SendNetworkUpdateImmediate();
            }

            public void DataToContainer()
            {
                if (definition.containerType == RideContainerType.TC)
                {
                    foreach (var entryuserid in rideData.authorizedPlayers)
                    {
                        structure.containerAsTC.authorizedPlayers.Add(new ProtoBuf.PlayerNameID
                        {
                            userid = entryuserid,
                            username = "Username" //this doesn't matter
                        });
                    }
                }

                if (rideData.containerContents != null)
                {
                    if (rideData.containerContents.Any())
                    {
                        foreach (var slot in rideData.containerContents)
                        {
                            var newItem = ItemManager.CreateByItemID(slot.Value.Key, slot.Value.Value);
                            if (newItem != null)
                            {
                                newItem.MoveToContainer(structure.container.inventory, slot.Key, false);
                            }
                        }
                    }
                }

                structure.container.SetMaxHealth(definition.containerHealth);
                structure.container.SetHealth(rideData.health * definition.containerHealth);
                structure.container.SendNetworkUpdateImmediate();

            }

            public void ApplySpecialGroupSkins()
            {
                //any skins defined?
                if (definition.specialGroupSkins != null)
                {
                    //are there any special groups?
                    if (structure.specialGroups.Any())
                    {
                        foreach (var group in structure.specialGroups)
                        {
                            if (!definition.specialGroupSkins.ContainsKey(group.Key))
                            {
                                continue;
                            }

                            var skinDic = definition.specialGroupSkins[group.Key];

                            var counter = 0;

                            foreach (var entity in group.Value)
                            {
                                entity.Value.skinID = skinDic[counter % skinDic.Count];
                                entity.Value.SendNetworkUpdateImmediate();
                                counter++;
                            }
                        }
                    }
                }
            }

            public void ApplySpecialSignData()
            {
                //any sign URLS defined?
                if (definition.specialSignData != null)
                {
                    //are there any special groups?
                    if (structure.specialGroups.Any())
                    {
                        foreach (var group in structure.specialGroups)
                        {
                            if (!definition.specialSignData.ContainsKey(group.Key))
                            {
                                continue;
                            }

                            var urlDic = definition.specialSignData[group.Key];

                            var delay = 2F;

                            var counter = 0;

                            foreach (var entity in group.Value)
                            {
                                Instance.timer.Once(delay+((float)counter/10F), () =>
                                {

                                    var sign = entity.Value as Signage;
                                    sign.EnsureInitialized();
                                    sign.textureIDs[0] = sign.net.ID;
                                    string str = "";
                                    str = urlDic[counter % urlDic.Count];
                                    Instance.SignArtist.Call("API_SkinSign", null, sign, str, true);
                                });
                                counter++;
                            }
                        }
                    }
                }
            }

            void FixedUpdate()
            {
                if (!initialized) return;

                handler.Update(this);
            }

            public void SetAdmissionFee(int price, string currency)
            {
                //if (!initialized) return;

                rideData.admissionFeePrice = price;
                rideData.admissionFeeCurrencyID = ItemManager.FindItemDefinition(currency).itemid;

                if (rideData.admissionFeeCurrencyID == 0)
                {
                    Instance.PrintError($"ERROR: {currency} is not a valid item name.");
                }
            }

            public bool ApplyHandler()
            {
                //
                if (Instance == null) return false;
                if (rideData == null) return false;
                if (Instance.rideHandlers == null) return false;

                if (Instance.HandlerExists(definition.handlerName))
                {
                    handler = Instance.rideHandlers[definition.handlerName];

                    rideData.handlerName = handler.name;
                    rideData.handlerVersion = handler.version;

                    return (handler.Prepare(this) == null);
                }
                else
                {
                    Instance.PrintError($"ERROR: {definition.handlerName} doesn't exist! Make sure you registered it first!");
                    return false;
                }
            }

            public void BoomboxStartPlaying(string providedPartialNameOrURL)
            {
                //first, remove current tape. Eject if non internal, remove if internal

                bool hasSomethingInside = structure.boombox.HasFlag(BaseEntity.Flags.Reserved1);
                bool somethingInsideIsValidTape = false;
                bool validTapeInsideIsInternal = false;

                Item firstItemInside = null;
                Cassette cassette = null;

                if (hasSomethingInside)
                {
                    firstItemInside = structure.boombox.inventory.itemList.FirstOrDefault();

                    if (firstItemInside != null)
                    {
                        if (firstItemInside.info.shortname.Contains("cassette"))
                        {
                            if (firstItemInside.instanceData != null)
                            {
                                cassette = BaseNetworkable.serverEntities.Find(firstItemInside.instanceData.subEntity) as Cassette;

                                if (cassette != null)
                                {
                                    somethingInsideIsValidTape = true;

                                    validTapeInsideIsInternal = (cassette.CreatorSteamId == 1337420);
                                }
                            }
                        }
                    }
                }

                if (firstItemInside != null)
                {
                    bool doEject = false;

                    if (somethingInsideIsValidTape)
                    {
                        if (validTapeInsideIsInternal)
                        {
                            //remove
                            firstItemInside.Remove();
                            ItemManager.DoRemoves();
                            firstItemInside = null;
                        }
                        else
                        {
                            doEject = true;
                        }
                    }
                    else
                    {
                        doEject = true;
                    }

                    if (doEject)
                    {
                        firstItemInside.DropAndTossUpwards(structure.boombox.transform.position + structure.boombox.transform.transform.up * 0.4F);
                    }
                }


                if (providedPartialNameOrURL.Contains("://"))
                {
                    structure.boombox.BoxController.CurrentRadioIp = providedPartialNameOrURL;
                    structure.boombox.ClientRPC(null, "OnRadioIPChanged", providedPartialNameOrURL);
                }
                else
                {
                    var maybeFreshTape = Instance.TapeLibrary?.CallHook("TryProduceRecordedTapeByName", providedPartialNameOrURL, true, 0UL, 1337420UL) as Item ?? null;

                    if (maybeFreshTape != null)
                    {
                        maybeFreshTape.MoveToContainer(structure.boombox.inventory, 0, true, true);

                        CassetteGuard.AssignGuard(maybeFreshTape, BaseNetworkable.serverEntities.Find(maybeFreshTape.instanceData.subEntity) as Cassette, structure.boombox.inventory);
                    }
                    else
                    {
                        Instance.PrintWarning($"TAPE LIBRARY FAILED TO PRODUCE TAPE: {providedPartialNameOrURL}. Is Tape Library loaded and the file exists inside /oxide/data/ogg?");
                    }
                }

                structure.boombox.Invoke(() =>
                {
                    structure.boombox.BoxController.ServerTogglePlay(true);
                }, 0.2F);
            }

            public void BoomboxStopPlaying()
            {
                if (!Instance.configData.allowMusic)
                {
                    return;
                }

                if (!rideData.hasAnyMusicTracks)
                {
                    return;
                }

                if (!rideData.musicAutoMode)
                {
                    return;
                }

                structure.boombox.BoxController.ServerTogglePlay(false);
            }


            public void UpdateVendingMachine()
            {
                structure.vendingMachine.shopName = $"Amusement Rides\n{rideData.nickname}\n {AdmissionFeeFormatted(null, false)}";
                structure.vendingMachine.SetFlag(VendingMachine.VendingMachineFlags.Broadcasting, rideData.broadcastLocation, false, true);
                structure.vendingMachine.UpdateMapMarker();
            }

            public void DefinitionToStructure()
            {
                structure.vendingMachine = Instance.SummonEntity(AmusementRidesPlugin.PREFAB_VENDING_MACHINE, null, transform.position + Vector3.down * 150F, Vector3.zero, 0, false) as VendingMachine;
                structure.allEntities.Add(structure.vendingMachine.net.ID, structure.vendingMachine);
                Instance.entityToRide.Add(structure.vendingMachine.net.ID, this);

                structure.boomboxGenerator = Instance.SummonEntity(AmusementRidesPlugin.PREFAB_GENERATOR, null, transform.position + Vector3.down * 150F, Vector3.zero, 0, false) as ElectricGenerator;

                structure.boomboxGenerator.electricAmount = 300F;

                structure.allEntities.Add(structure.boomboxGenerator.net.ID, structure.boomboxGenerator);
                Instance.entityToRide.Add(structure.boomboxGenerator.net.ID, this);//

                structure.boombox = Instance.SummonEntity(AmusementRidesPlugin.PREFAB_BOOMBOX, null, transform.TransformPoint(definition.posMusicX, definition.posMusicY, definition.posMusicZ), transform.eulerAngles + new Vector3(definition.rotMusicX, definition.rotMusicY, definition.rotMusicZ), 0, false) as DeployableBoomBox;

                structure.boombox.PowerUsageWhilePlaying = 0;

                structure.boombox.BoxController.CurrentRadioIp = null;

                structure.allEntities.Add(structure.boombox.net.ID, structure.boombox);
                Instance.entityToRide.Add(structure.boombox.net.ID, this);


                //engage

                Instance.EngageIO(structure.boombox, structure.boomboxGenerator, 0, 0);


                //and now splitters

                structure.InitializeSplitters();

                rideData.playlist = definition.assignedMusic.ToList();

                structure.containerType = definition.containerType;

                var prefab = AmusementRidesPlugin.PREFAB_SMALL_BOX;

                if (structure.containerType == RideContainerType.TC)
                {
                    structure.hasBuilding = true;

                    structure.buildingID = BuildingManager.server.NewBuildingID();

                    structure.building = new BuildingManager.Building { ID = structure.buildingID };

                    prefab = AmusementRidesPlugin.PREFAB_TC;
                }
                else
                {
                    if (structure.containerType == RideContainerType.BigBox)
                    {
                        prefab = AmusementRidesPlugin.PREFAB_LARGE_BOX;
                    }
                }

                //don't parent that shit. attach to null pivot and make it damagable.
                var newContainer = structure.AttachNewEntityToPivot(prefab, null, new Vector3(definition.posContainerX, definition.posContainerY, definition.posContainerZ), new Vector3(definition.rotContainerX, definition.rotContainerY, definition.rotContainerZ), definition.containerSkinID, false, false, false, -666, BuildingGrade.Enum.None, true);

                //make it pickupable

                var newContainerAsStorage = newContainer as StorageContainer;

                if (newContainerAsStorage != null)
                {
                    newContainerAsStorage.pickup.enabled = true;
                    newContainerAsStorage.pickup.itemTarget = definition.pickupDefinition;
                    newContainerAsStorage.pickup.requireHammer = true;
                }

                switch (structure.containerType)
                {
                    case RideContainerType.BigBox:
                    case RideContainerType.SmallBox:
                        {
                            structure.container = newContainer as StorageContainer;
                        }
                        break;
                    case RideContainerType.TC:
                        {
                            structure.container = newContainer as StorageContainer;
                            structure.containerAsTC = newContainer as BuildingPrivlidge;
                        }
                        break;
                }

                DataToContainer();
                DataToLock();
                DataToBoombox();

                //now we have the main pivot. everything is parented to it,
                //directly or indirectly
                //make sure you're iterating over a copy
                foreach (var entry in definition.definitionSegments.ToDictionary(e => e.Key, e => e.Value))
                {
                    if (entry.Value.cloneCount > 1)
                    {
                        var transformTupleList = Instance.TheCircleMethod(entry.Value.cloneCount, new Vector3(entry.Value.clonePosX, entry.Value.clonePosY, entry.Value.clonePosZ), new Vector3(entry.Value.cloneUpX, entry.Value.cloneUpY, entry.Value.cloneUpZ), new Vector3(entry.Value.cloneRotX, entry.Value.cloneRotY, entry.Value.cloneRotZ), entry.Value.clonesFaceSameDirection, new Vector3(entry.Value.sameRotX, entry.Value.sameRotY, entry.Value.sameRotZ));

                        var currentClone = 0;

                        foreach (var positionEntry in transformTupleList)
                        {
                            var pos = positionEntry.Key;
                            var rot = positionEntry.Value;

                            var pivot = structure.AttachNewPivotToPivot(structure.mainPivot, pos, rot, entry.Value.pivotItemID);

                            var newName = entry.Value.name + currentClone.ToString();

                            structure.pivots.Add(newName, pivot);

                            if (entry.Value.hasCollider)
                            {
                                //we need a dictionary of monos.
                                //rides will have them.
                                var newGameObject = new GameObject();
                                newGameObject.name = $"Collider:{name}:{newName}";

                                newGameObject.layer = (int)Rust.Layer.Reserved1;

                                //newGameObject.transform.position = pivot.transform.position + entry.V;
                                //newGameObject.transform.eulerAngles = pivot.transform.localEulerAngles;

                                newGameObject.SetActive(true);

                                var newSegmentCollider = newGameObject.AddComponent<RideSegmentCollider>();
                                //Init...
                                newSegmentCollider.PrepareAsBox(this, pivot, new Vector3(entry.Value.colliderPosX, entry.Value.colliderPosY, entry.Value.colliderPosZ), new Vector3(entry.Value.colliderRotX, entry.Value.colliderRotY, entry.Value.colliderRotZ), newName, new Vector3(entry.Value.colliderSizeX, entry.Value.colliderSizeY, entry.Value.colliderSizeZ));

                                //add da mono to dictionary
                                structure.segmentColliders.Add(newName, newSegmentCollider);
                            }


                            foreach (var segEnt in entry.Value.segmentEntities)
                            {
                                structure.AttachFromSegmentEntityDefinitionToPivot(segEnt.Value, pivot);
                            }
                            RecursiveStructureParser(entry.Value, pivot, entry.Value.name);

                            currentClone++;
                        }
                    }
                    else
                    {
                        var pivot = structure.AttachNewPivotToPivot(structure.mainPivot, new Vector3(entry.Value.pivotPosX, entry.Value.pivotPosY, entry.Value.pivotPosZ), new Vector3(entry.Value.pivotRotX, entry.Value.pivotRotY, entry.Value.pivotRotZ), entry.Value.pivotItemID);

                        var newName = entry.Value.name;

                        structure.pivots.Add(newName, pivot);

                        if (entry.Value.hasCollider)
                        {
                            //we need a dictionary of monos.
                            //rides will have them.
                            var newGameObject = new GameObject();
                            newGameObject.name = $"Collider:{name}:{newName}";

                            newGameObject.layer = (int)Rust.Layer.Reserved1;

                            //newGameObject.transform.position = pivot.transform.position + entry.V;
                            //newGameObject.transform.eulerAngles = pivot.transform.localEulerAngles;

                            newGameObject.SetActive(true);

                            var newSegmentCollider = newGameObject.AddComponent<RideSegmentCollider>();
                            //Init...
                            newSegmentCollider.PrepareAsBox(this, pivot, new Vector3(entry.Value.colliderPosX, entry.Value.colliderPosY, entry.Value.colliderPosZ), new Vector3(entry.Value.colliderRotX, entry.Value.colliderRotY, entry.Value.colliderRotZ), newName, new Vector3(entry.Value.colliderSizeX, entry.Value.colliderSizeY, entry.Value.colliderSizeZ));

                            //add da mono to dictionary
                            structure.segmentColliders.Add(newName, newSegmentCollider);
                        }

                        foreach (var segEnt in entry.Value.segmentEntities)
                        {
                            structure.AttachFromSegmentEntityDefinitionToPivot(segEnt.Value, pivot);
                        }
                        RecursiveStructureParser(entry.Value, pivot, entry.Value.name);
                    }
                }

                structure.allSpeakersArray = structure.allSpeakers.ToArray();
            }

            public void RecursiveStructureParser(RideDefinitionSegment currentSegment, DroppedItem parentPivot, string segmentName = null)
            {
                if (parentPivot == null)
                {
                    return;
                }

                if (currentSegment.childSegments == null) return;


                foreach (var entry in currentSegment.childSegments)
                {
                    if (entry.Value.cloneCount > 1)
                    {
                        var transformTupleList = Instance.TheCircleMethod(entry.Value.cloneCount, new Vector3(entry.Value.clonePosX, entry.Value.clonePosY, entry.Value.clonePosZ), new Vector3(entry.Value.cloneUpX, entry.Value.cloneUpY, entry.Value.cloneUpZ), new Vector3(entry.Value.cloneRotX, entry.Value.cloneRotY, entry.Value.cloneRotZ), entry.Value.clonesFaceSameDirection, new Vector3(entry.Value.sameRotX, entry.Value.sameRotY, entry.Value.sameRotZ));

                        var currentClone = 0;

                        foreach (var positionEntry in transformTupleList)
                        {
                            var pos = positionEntry.Key;
                            var rot = positionEntry.Value;

                            var pivot = structure.AttachNewPivotToPivot(parentPivot, pos, rot, entry.Value.pivotItemID);

                            var newName = entry.Value.name + currentClone.ToString();

                            if (segmentName != null)
                            {
                                newName = segmentName + "." + newName;
                            }


                            structure.pivots.Add(newName, pivot);

                            if (entry.Value.hasCollider)
                            {
                                //we need a dictionary of monos.
                                //rides will have them.
                                var newGameObject = new GameObject();
                                newGameObject.name = $"Collider:{name}:{newName}";

                                newGameObject.layer = (int)Rust.Layer.Reserved1;

                                //newGameObject.transform.position = pivot.transform.position + entry.V;
                                //newGameObject.transform.eulerAngles = pivot.transform.localEulerAngles;

                                newGameObject.SetActive(true);

                                var newSegmentCollider = newGameObject.AddComponent<RideSegmentCollider>();
                                //Init...
                                newSegmentCollider.PrepareAsBox(this, pivot, new Vector3(entry.Value.colliderPosX, entry.Value.colliderPosY, entry.Value.colliderPosZ), new Vector3(entry.Value.colliderRotX, entry.Value.colliderRotY, entry.Value.colliderRotZ), newName, new Vector3(entry.Value.colliderSizeX, entry.Value.colliderSizeY, entry.Value.colliderSizeZ));

                                //add da mono to dictionary
                                structure.segmentColliders.Add(newName, newSegmentCollider);
                            }

                            foreach (var segEnt in entry.Value.segmentEntities)
                            {
                                structure.AttachFromSegmentEntityDefinitionToPivot(segEnt.Value, pivot);
                            }
                            RecursiveStructureParser(entry.Value, pivot, newName);

                            currentClone++;
                        }

                    }
                    else
                    {
                        var pivot = structure.AttachNewPivotToPivot(parentPivot, new Vector3(entry.Value.pivotPosX, entry.Value.pivotPosY, entry.Value.pivotPosZ), new Vector3(entry.Value.pivotRotX, entry.Value.pivotRotY, entry.Value.pivotRotZ), entry.Value.pivotItemID);

                        var newName = entry.Value.name;

                        if (segmentName != null)
                        {
                            newName = segmentName + "." + newName;
                        }

                        structure.pivots.Add(newName, pivot);

                        if (entry.Value.hasCollider)
                        {
                            //we need a dictionary of monos.
                            //rides will have them.
                            var newGameObject = new GameObject();
                            newGameObject.name = $"Collider:{name}:{newName}";

                            newGameObject.layer = (int)Rust.Layer.Reserved1;

                            //newGameObject.transform.position = pivot.transform.position + entry.V;
                            //newGameObject.transform.eulerAngles = pivot.transform.localEulerAngles;

                            newGameObject.SetActive(true);

                            var newSegmentCollider = newGameObject.AddComponent<RideSegmentCollider>();
                            //Init...
                            newSegmentCollider.PrepareAsBox(this, pivot, new Vector3(entry.Value.colliderPosX, entry.Value.colliderPosY, entry.Value.colliderPosZ), new Vector3(entry.Value.colliderRotX, entry.Value.colliderRotY, entry.Value.colliderRotZ), newName, new Vector3(entry.Value.colliderSizeX, entry.Value.colliderSizeY, entry.Value.colliderSizeZ));

                            //add da mono to dictionary
                            structure.segmentColliders.Add(newName, newSegmentCollider);
                        }

                        foreach (var segEnt in entry.Value.segmentEntities)
                        {
                            structure.AttachFromSegmentEntityDefinitionToPivot(segEnt.Value, pivot);
                        }
                        RecursiveStructureParser(entry.Value, pivot, newName);
                    }
                }

            }

            void OnDestroy()
            {
                if (!Instance.isEmergencyCleanup)
                {
                    Cleanup();
                }
            }

            public void SyncRideData()
            {
                ContainerToData();
                LockToData(structure.baseLock);
                BoomboxToData();
            }

            public void EraseRideData()
            {
                if (Instance.rides.ContainsKey(name))
                {
                    Instance.rides.Remove(name);
                }
                Instance.storedData.rides.Remove(name);
            }

            public void Cleanup()
            {
                if (ui != null)
                {
                    ui.GuiCloseAll();
                }

                if (structure != null)
                {
                    if (structure.rideVolume != null)
                    {
                        DestroyImmediate(structure.rideVolume.gameObject);
                    }
                }

                if (isAlreadyDestroying)
                {
                    return;
                }

                isAlreadyDestroying = true;

                if (Instance.isUnloading == false)
                {
                    EraseRideData();
                }
                else
                {
                    SyncRideData();
                }

                //destroy all extra colliders
                if (structure != null)
                {
                    foreach (var entry in structure.segmentColliders)
                    {
                        if (entry.Value == null) continue;
                        if (entry.Value.IsUnityNull()) continue;

                        DestroyImmediate(entry.Value.gameObject);
                    }

                    List<BaseEntity> iterateOver = Facepunch.Pool.GetList<BaseEntity>();

                    iterateOver.AddRange(structure.allEntities.Values);
                    iterateOver.AddRange(structure.pivots.Values);

                    for (var i = 0; i < iterateOver.Count; i++)
                    {
                        var ent = iterateOver[i];

                        if (ent == null) continue;
                        if (ent.IsUnityNull()) continue;
                        if (ent.IsDestroyed) continue;

                        if ((ent as BaseCombatEntity)?.IsDead() ?? false) continue;

                        //this seems unneccesary?

                        ent.SetParent(null, true, false);
                        //UnmarkInternal(ent);


                        ent.Kill(destroyMode);

                        /*ent.Invoke(() =>
                        {
                            if (ent == null) return;
                            if (ent.IsUnityNull()) return;
                            if (ent.IsDestroyed) return;
                            if ((ent as BaseCombatEntity)?.IsDead() ?? false) return;

                        }, UnityEngine.Random.Range(0.1F, 1F)); */
                    }

                    //kill the main pivot if it's still alive
                    if (!structure.mainPivot.IsDestroyed)
                    {
                        structure.mainPivot.Kill(destroyMode);
                    }

                    Facepunch.Pool.FreeList(ref iterateOver);
                }
            }
        }
        //attach to any game object
        public class RideColliderSphere : RideCollider
        {
            public float radius;
            public override void PrepareCollider(object sizeParameter)
            {
                try
                {
                    var parameterAsFloat = Convert.ToSingle(sizeParameter);
                    this.radius = parameterAsFloat;
                }
                catch
                {
                    this.radius = 50F;
                }

                collider = gameObject.AddComponent<SphereCollider>();
                colliderAsSphereCollider = collider as SphereCollider;

                colliderAsSphereCollider.isTrigger = true;
                colliderAsSphereCollider.radius = radius;

                var rigidbody = gameObject.AddComponent<Rigidbody>();
                rigidbody.useGravity = false;
                rigidbody.isKinematic = true;
                rigidbody.detectCollisions = true;
                rigidbody.collisionDetectionMode = CollisionDetectionMode.Discrete;

                base.PrepareCollider(sizeParameter);
            }

            public override void PerformOnDestroy()
            {
                base.PerformOnDestroy();
            }
        }

        public class RideCollider : AmusementBehaviour
        {
            public Ride ride;
            public string segmentName;
            public Collider collider;
            public BoxCollider colliderAsBoxCollider;
            public SphereCollider colliderAsSphereCollider;

            public BaseEntity entity; //can be null, in that case it's a generic ride one

            public virtual void PerformAwake()
            {

            }

            public virtual void PerformPrepare(Ride ride, object sizeParameter, string segmentName = null, BaseEntity entity = null)
            {
                this.ride = ride;
                this.segmentName = segmentName;
                this.entity = entity;

                PrepareCollider(sizeParameter);

                if (entity != null)
                {
                    gameObject.transform.SetParent(entity.transform, false);
                }
                else
                {
                    gameObject.transform.SetParent(ride.transform, false);
                }

            }

            public virtual void PrepareCollider(object sizeParameter)
            {

            }

            public virtual void PerformOnDestroy()
            {

            }

            public virtual void PerformOnTriggerEnter(Collider col)
            {
                ride.handler.OnRideTriggerEnter(ride, col);
            }

            public virtual void PerformOnTriggerExit(Collider col)
            {
                ride.handler.OnRideTriggerExit(ride, col);
            }

            void Awake()
            {
                PerformAwake();
            }

            void OnTriggerEnter(Collider col)
            {
                PerformOnTriggerEnter(col);
            }

            void OnTriggerExit(Collider col)
            {
                PerformOnTriggerExit(col);
            }

            void OnDestroy()
            {
                if (!Instance.isEmergencyCleanup)
                {
                    PerformOnDestroy();
                }
            }
        }

        [ChatCommand("ar_give")]
        private void cmdChatAR_give(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return;
            //check permission first
            if (args.Length == 0)
            {
                TellMessage(player, MSG(MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME, player.UserIDString, ListRideDefinitions()));
            }
            else
            {
                var targetPlayer = player;

                if (args.Length > 1)
                {
                    var maybeDifferentPlayer = BasePlayer.activePlayerList.Where(p => p.displayName.ToLower().Contains(args[1].ToLower())).FirstOrDefault();
                    if (maybeDifferentPlayer != null)
                    {
                        targetPlayer = maybeDifferentPlayer;
                    }
                }

                var rideName = args[0];
                if (rideDefinitions.ContainsKey(rideName))
                {
                    GivePlayerRideItem(rideDefinitions[rideName], targetPlayer);

                    if (player == targetPlayer)
                    {
                        TellMessage(player, MSG(MSG_COMMAND_GIVE_GAVE_RIDE_TO_YOURSELF, player.UserIDString, rideName));
                    }
                    else
                    {
                        TellMessage(player, MSG(MSG_COMMAND_GAVE_RIDE_TO_PLAYER, player.UserIDString, targetPlayer.displayName, rideName));
                        TellMessage(player, MSG(MSG_COMMAND_GIVE_GAVE_RIDE_RECEIVED_FROM_ADMIN, targetPlayer.UserIDString, rideName));
                    }
                }
                else
                {
                    TellMessage(player, MSG(MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME, player.UserIDString, ListRideDefinitions()));
                }
            }
        }

        [ChatCommand("ar_spawn")]
        void cmdChatAR_spawn(BasePlayer player, string command, string[] args)
        {
            if (!IsAdmin(player)) return;

            if (args.Length > 0)
            {
                var name = args[0];

                if (rideDefinitions.ContainsKey(name))
                {
                    var lookRot = player.serverInput.current.aimAngles;
                    lookRot = new Vector3(0F, lookRot.y, 0F);

                    var newRideID = RideCreate(player.userID, player.transform.position, lookRot, rideDefinitions[name], "", 1.0F, null, true);
                    TellMessage(player, MSG(MSG_COMMAND_SPAWN_NEW_RIDE_CREATED, player.UserIDString, newRideID));
                }
                else
                {
                    TellMessage(player, MSG(MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME, player.UserIDString, ListRideDefinitions()));
                }
            }
            else
            {
                TellMessage(player, MSG(MSG_COMMAND_GIVE_AND_SPAWN_PROVIDE_VALID_RIDE_NAME, player.UserIDString, ListRideDefinitions()));
            }
        }
        public string ListRideDefinitions()
        {
            var count = 0;

            StringBuilder sb = new StringBuilder();

            foreach (var entry in rideDefinitions)
            {
                sb.Append("<color=yellow>");
                sb.Append(entry.Key);
                sb.Append("</color>");

                sb.Append((count == rideDefinitions.Count - 1) ? '.' : ',');
                sb.Append(' ');
                count++;
            }

            return sb.ToString(); ;
        }

        public class ExtraRideData
        {
            public Dictionary<int, string> memoryString = new Dictionary<int, string>();
            public Dictionary<int, int> memoryInt = new Dictionary<int, int>();
            public Dictionary<int, uint> memoryUint = new Dictionary<int, uint>();
            public Dictionary<int, float> memoryFloat = new Dictionary<int, float>();
            public Dictionary<int, bool> memoryBool = new Dictionary<int, bool>();
        }

        public class RideData
        {
            public string name; //gameobject name, will always be unique
            public string nickname; //apply by definition

            public string definitionName; //apply by definition
            public string handlerName; //apply by handler

            public bool chatEnabled = true;

            public string definitionVersion = "1.0.0";
            public string handlerVersion;  //apply by handler

            public ulong OwnerID = 0; //aply by definition

            public bool isAdminRide = false; //aply by definition
            public bool onlyAuthorizedCanOperate = true;
            public bool onlyAuthorizedCanRide = false;
            public bool dontChargeAuthorized = true;
            public bool broadcastLocation = true;

            public List<ulong> whitelistPlayers = new List<ulong>();
            public List<ulong> guestPlayers = new List<ulong>();

            public List<ulong> authorizedPlayers = new List<ulong>();

            public bool hasLock = false;

            public bool isCodeLock = false;

            public bool isLockLocked = false;

            public int keyCode = UnityEngine.Random.Range(1, 100000);
            public bool firstKeyCreated = false;

            public string code = "";
            public string guestCode = "";

            public bool hasCode = false;
            public bool hasGuestCode = false;

            public uint boomboxTapeAudioID = 0;
            public ulong boomboxTapeProducer = 0;
            public ulong boomboxTapeSkin = 0;
            public string boomboxTapeText = null;

            public string boomboxRadioURL = string.Empty;

            public bool boomboxIsOn = false;

            //normally for key locks it's UnityEngine.Random.Range(1, 100000);

            //public int codeGuest = UnityEngine.Random.Range(0, 9999);

            public float health = 1.0F;

            public int admissionFeeCurrencyID; //from definition

            public int admissionFeePrice; //from definition

            public float posX;
            public float posY;
            public float posZ;

            public float rotX;
            public float rotY;
            public float rotZ;

            public List<string> playlist = null;
            public bool hasAnyMusicTracks = false;

            public bool musicAutoMode = true;


            //first int: slot number
            //second int: item ID
            //third int: item amount

            public Dictionary<int, KeyValuePair<int, int>> containerContents = new Dictionary<int, KeyValuePair<int, int>>();

            public ExtraRideData extraData = null;
        }

        public class StoredData
        {
            public string version = VERSION;
            public Dictionary<string, RideData> rides = new Dictionary<string, RideData>();
        }

        public StoredData storedData;

        public void LoadData()
        {
            PrintWarning("Loading data...");

            try
            {
                storedData = Interface.Oxide.DataFileSystem.ReadObject<StoredData>(Name);
            }
            catch
            {
                PrintWarning("Corrupt data, generating default one.");
                storedData = new StoredData();
                SaveData();
            }

            if (storedData == null)
            {
                PrintWarning("Null data, generating default one.");
                storedData = new StoredData();
                SaveData();
            }
        }

        public enum VersionComparisonResult
        {
            Equal,
            PreviousIsOlder,
            PreviousIsNewer
        }

        public VersionComparisonResult CompareVersions(string previousVersion, string currentVersion, out Tuple<int,int,int> tuplePrevious, out Tuple<int,int,int> tupleCurrent)
        {
            tuplePrevious = null;
            tupleCurrent = null;

            if (previousVersion == currentVersion)
            {
                return VersionComparisonResult.Equal;
            }

            var previousVersionSeparated = previousVersion.Split('.');
            var currentVersionSeparated = currentVersion.Split('.');

            int previousVersionValue = 0;
            int currentVersionValue = 0;

            int multiplier = 1;

            int previousMajor = 0;
            int previousMinor = 0;
            int previousBuild = 0;

            int currentMajor = 0;
            int currentMinor = 0;
            int currentBuild = 0;

            for (var i = 2; i >= 0; i--)
            {
                int previousVersionPart = int.Parse(previousVersionSeparated[i]);
                int currentVersionPart = int.Parse(currentVersionSeparated[i]);

                switch (i)
                {
                    case 2:
                        {
                            previousBuild = previousVersionPart;
                            currentBuild = currentVersionPart;
                        }
                        break;
                    case 1:
                        {
                            previousMinor = previousVersionPart;
                            currentMinor = currentVersionPart;
                        }
                        break;
                    case 0:
                        {
                            previousMajor = previousVersionPart;
                            currentMajor = currentVersionPart;
                        }
                        break;
                }

                previousVersionValue += previousVersionPart * multiplier;
                currentVersionValue += currentVersionPart * multiplier;

                multiplier *= 1024;
            }

            tuplePrevious = new Tuple<int, int, int>(previousMajor, previousMinor, previousBuild);
            tupleCurrent = new Tuple<int, int, int>(currentMajor, currentMinor, currentBuild);

            if (previousVersionValue < currentVersionValue)
            {
                return VersionComparisonResult.PreviousIsOlder;
            }

            return VersionComparisonResult.PreviousIsNewer;
        }

        public ListHashSet<string> BundledDownloadQueue;

        Timer BundledDownloadTimeoutTimer = null;

        public void CheckIfItHasBeen30SecondsOrMoreAndStillStuffInQueue()
        {
            if (BundledDownloadQueue.Count > 0)
            {
                PrintError("ERROR: It's been more than 30 seconds and Tape Library has still not reported all the required files present/downloaded. Check the output above. The plugin will continue loading, but the bundled music might not work correctly. If this problem persists, you can just upload the required files manually to the /oxide/data/ogg/ folder (provided Tape Library is loaded in).");

                ProcessData();
            }

            BundledDownloadQueue.Clear();
            BundledDownloadTimeoutTimer = null;


        }

        public void OggDownloadResultAction(bool boolResult, string url, string msg)
        {
            if (BundledDownloadQueue.Count == 0)
            {
                return;
            }

            if (!boolResult)
            {
                PrintError($"Could not download *.ogg from {url}: {msg}");
            }
            else
            {
                PrintWarning(msg);
            }

            if (BundledDownloadQueue.Contains(url))
            {
                BundledDownloadQueue.Remove(url);
            }

            if (BundledDownloadQueue.Count == 0)
            {
                //that was the last one.
                PrintWarning("All OGG files dealt with (check the output above). The plugin will continue loading.");
                ProcessData();
            }
        }

        public void TryEnsuringOGGsDownloadedAndThenProcessData()
        {
            Puts("OK: Tape Library is loaded in, downloading all default OGGs if missing...");

            BundledDownloadQueue = new ListHashSet<string>();

            //set the timeout timer...
            BundledDownloadTimeoutTimer = Instance.timer.Once(30F, CheckIfItHasBeen30SecondsOrMoreAndStillStuffInQueue);

            for (var i = 0; i < AmusementRidesPlugin.BUNDLED_OGG_URLS.Length; i++)
            {
                BundledDownloadQueue.Add(AmusementRidesPlugin.BUNDLED_OGG_URLS[i]);
            }

            for (var i = 0; i < AmusementRidesPlugin.BUNDLED_OGG_URLS.Length; i++)
            {                
                TapeLibrary.Call("RequestOggDownloadToDirectory", AmusementRidesPlugin.BUNDLED_OGG_URLS[i], 0, Delegate.CreateDelegate(typeof(Action<bool, string, string>), Instance, nameof(OggDownloadResultAction))); 
            }

        }

        public void ProcessData()
        {
            var versionFromData = storedData.version;

            Puts($"Processing data...");

            //check if the data version changed

            bool needsSave = false;

            Tuple<int, int, int> tuplePrevious;
            Tuple<int, int, int> tupleCurrent;

            //is the version older than 0.9.3?
            var comparisonResult = CompareVersions(versionFromData, "0.9.3", out tuplePrevious, out tupleCurrent);

            if (comparisonResult == VersionComparisonResult.PreviousIsOlder)
            {
                Instance.PrintError($"\n\nMusical Update: Backed up your previous data as /oxide/data/AmusementRides.OLD.json and generated fresh data.\n\n");

                SaveData(Name + ".OLD");

                storedData = new StoredData();

                needsSave = true;
            }

            if (storedData.rides.Any())
            {
                foreach (var entry in storedData.rides)
                {
                    Puts($"Processing {entry.Key} ({entry.Value.nickname})...");


                    //check if there's a definition!
                    var defName = entry.Value.definitionName;
                    var defVersion = entry.Value.definitionVersion;


                    if (rideDefinitions.ContainsKey(defName))
                    {
                        var rideDef = rideDefinitions[defName];
                        var newRideID = RideCreate(entry.Value.OwnerID, new Vector3(entry.Value.posX, entry.Value.posY, entry.Value.posZ), new Vector3(entry.Value.rotX, entry.Value.rotY, entry.Value.rotZ), rideDef, "", entry.Value.health, entry.Value, entry.Value.isAdminRide);


                        if (rideDef.rideVersion == entry.Value.definitionVersion)
                        {
                            Puts($"OK: Processing ride {entry.Key} using definition {defName} {defVersion}...");

                        }
                        else
                        {
                            PrintWarning($"INFO: {entry.Key} is using definition {defName} version {defVersion}, but the currently registered definition is version {rideDef.rideVersion}! Migrating to {rideDef.rideVersion}.");
                            //possible migration of definition

                            entry.Value.definitionVersion = rideDef.rideVersion;
                        }
                    }
                    else
                    {
                        PrintError($"ERROR: Trying to process the ride {entry.Key}, but the ride definition \"{defName}\" is not registered. Aborting loading the ride.");
                    }


                }

            }
            else
            {
                PrintWarning("INFO: No rides to process");
            }

            if (needsSave)
            {
                storedData.version = VERSION;
                Instance.PrintWarning($"\n\nYou have succesfully updated from {versionFromData} to {VERSION}\n");
                SaveData();
            }
        }


        public void SaveData(string customFilename = null)
        {
            PrintWarning("Saving data...");

            Interface.Oxide.DataFileSystem.WriteObject(customFilename == null ? Name : customFilename, storedData);
        }

        public string RideCreate(ulong ownerID, Vector3 position, Vector3 rotation, RideDefinition definition, string forceRideID = "", float rideHealth = 1.0F, RideData rideData = null, bool isAdminRide = false, bool justCreated = false)
        {
            Ride newRide = null;

            string newId = definition.rideName;

            if (forceRideID != "")
            {
                newId = forceRideID;
            }
            else
            {

                //take the count

                var ridesToCheck = rides.Where(r => r.Key.Contains(newId));
                //take the highest number found, add 1
                int maxFound = 0;

                foreach (var entry in ridesToCheck)
                {
                    var numberFromKey = int.Parse(entry.Key.Replace(newId, ""));
                    if (numberFromKey > maxFound)
                    {
                        maxFound = numberFromKey;
                    }
                }

                newId = newId + (maxFound + 1).ToString();
            }

            //create a new game object, attach a Ride mono to it, Initialize the mono with definition.
            var newGameObject = new GameObject();
            newGameObject.name = newId;

            newGameObject.layer = (int)Rust.Layer.Reserved1;
            newGameObject.transform.position = position;
            newGameObject.transform.eulerAngles = rotation;

            newGameObject.SetActive(true);

            newRide = newGameObject.AddComponent<Ride>();

            newRide.Init(position, rotation, definition, rideData, ownerID, rideHealth, newId, isAdminRide, justCreated);

            //cache that shit, whether it's a new thing or not
            Instance.rides.Add(newId, newRide);

            return newId;
        }

        public void RideRemove(string rideId)
        {
            //UnityEngine.Object.DestroyImmediate(rides[rideId].gameObject);
            GameManager.DestroyImmediate(rides[rideId].gameObject);
        }
        public void CreateEffectForAt(string fx, BasePlayer player)
        {
            if (!Fx.ContainsKey(fx)) return;

            //CreateEffectAt(fx, player.transform.position);
            CreateEffectFor(fx, player);

        }
        public void CreateEffectAt(string fx, Vector3 position, Connection sourceConn = null)
        {
            if (!Fx.ContainsKey(fx)) return;

            Effect.server.Run(Fx[fx], position, Vector3.forward);
        }

        public void CreateEffectFor(string fx, BasePlayer player)
        {
            if (!Fx.ContainsKey(fx)) return;

            EffectNetwork.Send(new Effect(Fx[fx], player, 0, Vector3.zero, Vector3.forward));
        }

        public static bool IsAdmin(BasePlayer player)
        {
            //return false;
            //check if the player has the admin permission
            return player.IsAdmin || player.IsDeveloper;
        }

        public void GivePlayerRideItem(RideDefinition definition, BasePlayer player, int amount = 1)
        {
            Item createdItem = ItemManager.Create(definition.pickupDefinition, 1, definition.rideItemSkinID);

            createdItem.name = definition.rideNickname;

            //createdItem.conditionNormalized = Mathf.Clamp01(ride.structure.container.health / ride.definition.containerHealth);

            player.GiveItem(createdItem, BaseEntity.GiveItemReason.PickedUp);
        }

        public bool PlayerTryPickupRide(Ride ride, BasePlayer player)
        {
            //replacement pickup function

            GivePlayerRideItem(ride.definition, player);

            RideRemove(ride.name);

            return false;
        }

        public IEnumerable<TValue> RandomValuesFromList<TValue>(IList<TValue> list)
        {
            List<TValue> values = Enumerable.ToList(list);

            int size = list.Count;
            while (true)
            {
                yield return values[UnityEngine.Random.Range(0, size)];
            }
        }

        public void TellMessage(BasePlayer player, string msg, string prefix = "Amusement Rides")//, BasePlayer involvedPlayer = null)
        {

            if (player != null)
            {
                if (player.IsConnected)
                {
                    Player.Message(player, MSG(MSG_MESSAGE_FORMAT, player.UserIDString, prefix, msg), null, SteamIconID);
                }
            }
        }
        public class ColorCode
        {
            public string hexValue;
            public Color rustValue;
            public string rustString;
            public ColorCode(string hex)
            {
                hex = hex.ToUpper();

                hexValue = "#" + hex;

                //extract the R, G, B
                var r = (float)short.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var g = (float)short.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber) / 255;
                var b = (float)short.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber) / 255;

                rustValue = new Color(r, g, b);
                rustString = $"{r} {g} {b}";
            }
        }

        public static class ColorPalette
        {
            public static ColorCode RustyBlack = new ColorCode("1e2020");
            public static ColorCode RustyGrey = new ColorCode("a4a6a7");
            public static ColorCode RustyWhite = new ColorCode("f6eae0");

            public static ColorCode RustyRed = new ColorCode("ce422b");
            public static ColorCode RustyYellow = new ColorCode("baae45");
            public static ColorCode RustyGreen = new ColorCode("8fba45");
            public static ColorCode RustyBlue = new ColorCode("4897ce");

            public static ColorCode RustyOrange = new ColorCode("d76716");

            public static ColorCode RustyRedDark = new ColorCode("662d24");
            public static ColorCode RustyYellowDark = new ColorCode("665f24");
            public static ColorCode RustyGreenDark = new ColorCode("4e6624");
            public static ColorCode RustyBlueDark = new ColorCode("244a66");

            //gradient between red and blue
            public static ColorCode RustyRed075 = new ColorCode("c34939");
            public static ColorCode RustyRed050 = new ColorCode("b3534c");
            public static ColorCode RustyRed025 = new ColorCode("a05f63");
            public static ColorCode RustyBlue025 = new ColorCode("8b6d7d");
            public static ColorCode RustyBlue050 = new ColorCode("767a96");
            public static ColorCode RustyBlue075 = new ColorCode("6386ad");
        }

        [ConsoleCommand("ride_ui")]
        private void cmdConsoleRideUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            //check the 0th argument, that should be the ride name
            if (arg.HasArgs())
            {
                var rideName = arg.Args[0];

                if (rides.ContainsKey(rideName))
                {
                    var ride = rides[rideName];

                    if (ride.ui.subscribers.ContainsKey(player.userID))
                    {
                        //remove the first argument
                        arg.Args = arg.Args.Skip(1).ToArray();

                        ride.ui.ParseCommand(player, arg);
                    }
                }
            }
        }

        public class RideUIConfigurable
        {
            public RideUI ui;

            public string commandName;
            public string hint = "Configuration";

            public string layout = "Ab";

            public string hintTrue = "True";
            public string hintFalse = "False";

            public string colorTrue = ColorPalette.RustyGreenDark.rustString;
            public string colorFalse = ColorPalette.RustyRedDark.rustString;

            public string uiName = "argui.change.me";

            public string uiParent = "Overlay";

            public TextAnchor textAlign = TextAnchor.MiddleCenter;

            //for all
            public string colorText = ColorPalette.RustyWhite.rustString;

            public bool alsoShowCurrentValueWithHint = true;

            public string defaultValue = "DEFAULT";

            public string valueTrue = "DEFAULT";

            public string valueFalse = "DEFAULT";

            public string type = "text";
            //text, //inputFloat, //inputString, //inputInt

            public uint lineNumber = 0;

            public int elementIndexStart;

            public CuiTextComponent componentHintText;

            public CuiButtonComponent componentButton;

            public CuiInputFieldComponent componentInput;

            public CuiRectTransformComponent anchorPrimary = UI_anchorLineFull;
            public CuiRectTransformComponent anchorSecondary;

            public void UpdateConfigurable(string newHintText, string newValue = null)
            {

                if (type == "text")
                {
                    string maybeExtra = "";

                    if (newHintText == null)
                    {
                        newHintText = hint;
                    }

                    if (newValue != null)
                        maybeExtra = $" <i>({newValue})</i>";

                    componentHintText.Text = $"{newHintText}  { maybeExtra}";
                }

                else if (type == "toggle")
                {
                    bool newValueBool = false;

                    if (newValue.ToLower().Contains("t"))
                    {
                        newValueBool = true;
                    }

                    SetConfigurableBoolStuff(newValueBool);
                }
            }

            public void SetConfigurableBoolStuff(bool boolean)
            {
                componentButton.Color = boolean ? colorTrue : colorFalse;
                componentHintText.Text = boolean ? hintTrue : hintFalse;
            }

            public void AddElementsToContainer()
            {
                switch (type)
                {
                    case "text":
                        {
                            //text...
                            componentHintText = new CuiTextComponent
                            {
                                Text = hint,
                                FontSize = UI_fontSize,
                                Color = colorText,
                                FadeIn = UI_fade,
                                Align = textAlign,
                            };

                            var anchorA = UI_GetLineAtHeight(lineNumber, anchorPrimary);
                            var anchorB = UI_GetLineAtHeight(lineNumber, anchorSecondary);

                            ui.containerMain.Add(new CuiElement
                            {
                                Name = uiName,
                                Parent = uiParent,
                                FadeOut = UI_fade,
                                Components =
                                {
                                    componentHintText,

                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = anchorA.AnchorMin,
                                        AnchorMax = anchorA.AnchorMax
                                    }
                                }
                            });

                            ui.elementCount++;

                            componentHintText = ui.containerMain[ui.elementCount - 1].Components[0] as CuiTextComponent;

                            //panel...

                            ui.containerMain.Add(new CuiPanel
                            {
                                CursorEnabled = true,
                                Image = new CuiImageComponent
                                {
                                    Color = ColorPalette.RustyWhite.rustString,
                                },
                                FadeOut = UI_fade,
                                RectTransform = { AnchorMin = anchorB.AnchorMin, AnchorMax = anchorB.AnchorMax }
                            }, uiParent, uiName + ".panel");

                            ui.elementCount++;

                            //input
                            componentInput = new CuiInputFieldComponent
                            {
                                CharsLimit = 32,
                                Align = TextAnchor.MiddleCenter,
                                Color = ColorPalette.RustyBlack.rustString,
                                FontSize = UI_fontSize,
                                Text = "",
                                IsPassword = false,
                                Command = $"ride_ui {ui.ride.name} {commandName} "
                            };

                            ui.containerMain.Add(new CuiElement
                            {
                                Name = uiName+".input",
                                Components =
                                {
                                    componentInput,

                                    new CuiRectTransformComponent{ AnchorMin = "0 0", AnchorMax = "1 1"}
                                },
                                Parent = uiName+".panel"
                            });

                            ui.elementCount++;
                        }
                    break;

                    case "toggle":
                        {
                            var thisAnchor = UI_GetLineAtHeight(lineNumber, anchorPrimary);

                            var buttonDefinition = new CuiButton
                            {
                                Button =
                                {
                                    Color = colorTrue, //will be changed
                                    FadeIn = UI_fade,
                                    Command = $"ride_ui {ui.ride.name} {commandName} toggle",
                                },
                                Text =
                                {
                                    Color = ColorPalette.RustyWhite.rustString,
                                    Align = TextAnchor.MiddleCenter,
                                    FadeIn = UI_fade,
                                    FontSize = UI_fontSize,
                                    Text = hintTrue //will be changed too
                                },
                                FadeOut = UI_fade,
                                RectTransform = { AnchorMin = thisAnchor.AnchorMin, AnchorMax = thisAnchor.AnchorMax },
                            };

                            //now after you add this button, take the last two elements of the container. button goes first
                            ui.containerMain.Add(buttonDefinition, uiParent, uiName);

                            ui.elementCount += 2;

                            componentButton = ui.containerMain[ui.elementCount - 2].Components[0] as CuiButtonComponent;
                            componentHintText = ui.containerMain[ui.elementCount - 1].Components[0] as CuiTextComponent;
                        }
                        break;

                    default:
                        {
                            return;
                        }
                }
            }

            public void RemoveElementFromContainer()
            {

            }
            
        }

        public const float UI_left = 0.34375F;
        public static float UI_bottom = 0.495518F;
        public static float UI_right = 0.6410625F;
        public static float UI_top = 0.916666F;

        public static float UI_paddingPx = 8F;

        public static float UI_imageWidthPx = 256F;
        public static float UI_imageHeightPx = 256F;

        public static float UI_imageWidth = UI_imageWidthPx / 1920F;
        public static float UI_imageHeight = UI_imageHeightPx / 1080F;

        public static float UI_paddingH = UI_paddingPx / 1920F;
        public static float UI_paddingV = UI_paddingPx / 1080F;

        public static float UI_leftP = UI_left + UI_paddingH;
        public static float UI_bottomP = UI_bottom + UI_paddingV;
        public static float UI_rightP = UI_right - UI_paddingH;
        public static float UI_topP = UI_top - UI_paddingV;

        public static float UI_titleHeight = 0.04F;

        public static float UI_lineHeight = 0.025F;

        public static int UI_fontSizeTitle = 20;

        public static int UI_fontSize = 12;

        public static float UI_sideButtonWidth = UI_paddingH * 4F;

        public static CuiRectTransformComponent UI_anchorMain = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_left} {UI_bottom}",
            AnchorMax = $"{UI_right} {UI_top}"
        };

        public static CuiRectTransformComponent UI_anchorTitle = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_left} {UI_top - UI_titleHeight}",
            AnchorMax = $"{UI_right} {UI_top}"
        };

        public static CuiRectTransformComponent UI_anchorRideDescriptionTitle = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_topP - UI_titleHeight - UI_lineHeight}",
            AnchorMax = $"{UI_rightP} {UI_topP - UI_titleHeight}"
        };

        public static CuiRectTransformComponent UI_anchorRideDescriptionImage = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_rightP - UI_imageWidth} {UI_topP - UI_titleHeight - UI_imageHeight - UI_lineHeight}",
            AnchorMax = $"{UI_rightP} {UI_topP - UI_titleHeight - UI_lineHeight}"
        };

        public static CuiRectTransformComponent UI_anchorRideCraftButton = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_topP - UI_titleHeight - UI_imageHeight - UI_lineHeight}",
            AnchorMax = $"{UI_rightP - UI_imageWidth - UI_paddingH} {UI_topP - UI_titleHeight - UI_imageHeight + UI_lineHeight * 3 - UI_lineHeight}"
        };

        public static CuiRectTransformComponent UI_anchorRideDescriptionText = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_topP - UI_titleHeight - UI_imageHeight + UI_lineHeight*2 + UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_rightP - UI_imageWidth - UI_paddingH} {UI_topP - UI_titleHeight - UI_lineHeight}"
        };


        public static CuiRectTransformComponent UI_anchorRideMenuPrevious = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_bottomP}",
            AnchorMax = $"{UI_leftP + UI_sideButtonWidth} {UI_topP - UI_titleHeight - UI_imageHeight - UI_lineHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorRideMenuNext = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_rightP - UI_sideButtonWidth} {UI_bottomP}",
            AnchorMax = $"{UI_rightP} {UI_topP - UI_titleHeight - UI_imageHeight - UI_lineHeight - UI_paddingV}"
        };

        public static float UI_anchorRideButtonMinX = UI_leftP + UI_sideButtonWidth + UI_paddingH;
        public static float UI_anchorRideButtonMaxX = UI_rightP - UI_sideButtonWidth - UI_paddingH;

        public static float UI_anchorRideButtonHeight = (UI_topP - UI_titleHeight - UI_imageHeight - UI_lineHeight - UI_paddingV - UI_bottomP - UI_paddingV * 2F) /3F ;


        public static CuiRectTransformComponent UI_anchorRideMenuButton1 = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_anchorRideButtonMinX} {UI_bottomP}",
            AnchorMax = $"{UI_anchorRideButtonMaxX} {UI_bottomP + UI_anchorRideButtonHeight}"
        };

        public static CuiRectTransformComponent UI_anchorRideMenuButton2 = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_anchorRideButtonMinX} {UI_bottomP + UI_anchorRideButtonHeight + UI_paddingV}",
            AnchorMax = $"{UI_anchorRideButtonMaxX} {UI_bottomP + UI_anchorRideButtonHeight + UI_paddingV + UI_anchorRideButtonHeight}"
        };

        public static CuiRectTransformComponent UI_anchorRideMenuButton3 = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_anchorRideButtonMinX} {UI_bottomP + UI_anchorRideButtonHeight + UI_paddingV + UI_anchorRideButtonHeight + UI_paddingV}",
            AnchorMax = $"{UI_anchorRideButtonMaxX} {UI_bottomP + UI_anchorRideButtonHeight + UI_paddingV + UI_anchorRideButtonHeight + UI_paddingV + UI_anchorRideButtonHeight}"
        };

        public static CuiRectTransformComponent UI_anchorLineFull = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_rightP} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineHalfA = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_leftP + (UI_right - UI_left) / 2F - UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineHalfB = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP + (UI_right - UI_left) / 2F + UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_rightP} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineThirdA = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_leftP + (UI_right - UI_left) / 3F - UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineThirdB = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP + (UI_right - UI_left) / 3F + UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_leftP + (UI_right - UI_left) * (2F / 3F) - UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineThirdAB = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_leftP + (UI_right - UI_left) * (2F / 3F) - UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineThirdBC = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP + (UI_right - UI_left) / 3F + UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_rightP} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static CuiRectTransformComponent UI_anchorLineThirdC = new CuiRectTransformComponent
        {
            AnchorMin = $"{UI_leftP + (UI_right - UI_left) * (2F / 3F) - UI_paddingH / 2F} {UI_top - UI_titleHeight - UI_paddingV - UI_lineHeight}",
            AnchorMax = $"{UI_rightP} {UI_top - UI_titleHeight - UI_paddingV}"
        };

        public static float UI_fade = 0.125F;

        public static CuiRectTransformComponent UI_GetLineAtHeight(uint lineNumber, CuiRectTransformComponent basedOn)
        {
            string newAnchorMin = basedOn.AnchorMin;
            string newAnchorMax = basedOn.AnchorMax;

            //split shit

            float minX, minY, maxX, maxY;

            var splitMin = newAnchorMin.Split(' ');
            var splitMax = newAnchorMax.Split(' ');

            minX = float.Parse(splitMin[0]);
            minY = float.Parse(splitMin[1]) - lineNumber * (UI_lineHeight + UI_paddingV);

            maxX = float.Parse(splitMax[0]);
            maxY = float.Parse(splitMax[1]) - lineNumber * (UI_lineHeight + UI_paddingV);

            return new CuiRectTransformComponent
            {
                AnchorMin = $"{minX} {minY}",
                AnchorMax = $"{maxX} {maxY}"
            };
        }

        [ConsoleCommand("workbench_ui")]
        private void cmdConsoleWorkbenchUI(ConsoleSystem.Arg arg)
        {
            var player = arg.Connection.player as BasePlayer;
            if (player == null) return;

            //check the 0th argument, that should be the workbench net ID
            if (arg.HasArgs())
            {
                uint maybeNetID;

                if (uint.TryParse(arg.Args[0], out maybeNetID))
                {
                    var workbench = BaseNetworkable.serverEntities.Find(maybeNetID) as Workbench;

                    if (workbench != null)
                    {
                        var workbenchHelper = workbench.GetComponent<WorkbenchHelper>();

                        if (workbenchHelper != null)
                        {
                            //check if the player is in GUI subscribers...
                            if (workbenchHelper.ui.subscribers.ContainsKey(player.userID))
                            {
                                //remove the first argument
                                arg.Args = arg.Args.Skip(1).ToArray();

                                workbenchHelper.ui.ParseCommand(player, arg);
                            }
                        } 
                    }
                }
            }
        }

        public class CassetteGuard : MonoBehaviour
        {
            public Cassette cassette;
            public Item item;
            public ItemContainer originalContainer;

            bool stillActive = true;

            public static CassetteGuard AssignGuard(Item item, Cassette cassette, ItemContainer container)
            {
                CassetteGuard result = cassette.gameObject.AddComponent<CassetteGuard>();

                result.item = item;
                result.originalContainer = container;
                result.cassette = cassette;

                result.InvokeRepeating(nameof(CheckIfStillInContainer), 1F, 1F);

                return result;
            }

            public void CheckIfStillInContainer()
            {
                if (Instance == null)
                {
                    return;
                }

                if (!stillActive)
                {
                    return;
                }

                if (item == null)
                {
                    KillCassetteIfNotDestroyed();
                    return;
                }

                if (originalContainer == null || Instance.isUnloading || item.parent != originalContainer)
                {
                    item.Remove();
                    KillCassetteIfNotDestroyed();
                    return;
                }
            }

            void OnDestroy()
            {
                CancelInvoke(nameof(CheckIfStillInContainer));
            }

            public void KillCassetteIfNotDestroyed()
            {
                if (!cassette.IsDestroyed)
                {
                    cassette.Kill(BaseNetworkable.DestroyMode.None);
                }

                stillActive = false;
            }
        }

        public class WorkbenchHelper : MonoBehaviour
        {
            public Workbench workbench;
            public WorkbenchUI ui;
            public int level;
            public int page = 0;
            public string currentDefinition;

            public static readonly int RIDES_PER_PAGE = 3;
            public void Prepare(Workbench workbench)
            {
                string wbColor = ColorPalette.RustyGreenDark.rustString;

                this.workbench = workbench;
                switch (this.workbench.PrefabName)
                {
                    case AmusementRidesPlugin.PREFAB_WORKBENCH_LEVEL1_DEPLOYED:
                    case AmusementRidesPlugin.PREFAB_WORKBENCH_LEVEL1_STATIC:
                        {
                            level = 1;
                        }
                        break;
                    case AmusementRidesPlugin.PREFAB_WORKBENCH_LEVEL2_DEPLOYED:
                    case AmusementRidesPlugin.PREFAB_WORKBENCH_LEVEL2_STATIC:
                        {
                            level = 2;
                            wbColor = ColorPalette.RustyBlueDark.rustString;
                        }
                        break;
                    case AmusementRidesPlugin.PREFAB_WORKBENCH_LEVEL3_DEPLOYED:
                    case AmusementRidesPlugin.PREFAB_WORKBENCH_LEVEL3_STATIC:
                        {
                            level = 3;
                            wbColor = ColorPalette.RustyRedDark.rustString;
                        }
                        break;
                }

                 ui = new WorkbenchUI { workbenchHelper = this, colorTitleBG = wbColor };

                ui.PrepareGui();
            }

            void OnDestroy()
            {
                ui.GuiCloseAll();
            }
        }

        public class WorkbenchUI : GenericUI
        {
            public WorkbenchHelper workbenchHelper;

            public CuiElementContainer containerDescription = new CuiElementContainer();
            public CuiElementContainer containerMenu = new CuiElementContainer();

            public CuiElement elementDescriptionTitle = new CuiElement
            {
                Name = "argui.description.title",
                Parent = "Overlay",
                Components =
                {
                    new CuiTextComponent
                    {
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        Text = "RIDE TITLE",
                        Color = ColorPalette.RustyGrey.rustString, //WILL CHANGE
                        FontSize = UI_fontSize,
                    },
                    UI_anchorRideDescriptionTitle
                }
            };

            public CuiElement elementDescriptionText = new CuiElement
            {
                Name = "argui.description.text",
                Parent = "Overlay",
                Components =
                {
                    new CuiTextComponent
                    {
                        Align = TextAnchor.UpperLeft,
                        FadeIn = UI_fade,
                        Text = "RIDE DESCRIPTION WILL CHANGE",
                        FontSize = UI_fontSize,
                    },
                    UI_anchorRideDescriptionText
                }
            };

            public CuiElement elementDescriptionImage = new CuiElement
            {
                Name = "argui.description.image",
                Parent = "Overlay",
                FadeOut = UI_fade,
                Components =
                {
                    new CuiRawImageComponent
                    {
                        Url = "THIS WILL CHANGE",
                        FadeIn = UI_fade,
                    },
                    UI_anchorRideDescriptionImage
                }
            };

            public CuiButton buttonCraft = new CuiButton
            {
                Button =
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed
                        FadeIn = UI_fade,
                        Command = $"workbench_ui WORKBENCH_NET_ID craft rideDefinitionName", //will be changed too
                    },
                Text =
                    {
                        Color = ColorPalette.RustyWhite.rustString,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        FontSize = UI_fontSize,
                        Text = "Craft for 1000 Wood, 2000 Metal Fragments, 10 Raw Horse Meat" //will be changed too
                    },
                FadeOut = UI_fade,
                RectTransform = { AnchorMin = UI_anchorRideCraftButton.AnchorMin, AnchorMax = UI_anchorRideCraftButton.AnchorMax },
            };

            public CuiButton buttonPrevious = new CuiButton
            {
                Button =
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed
                        FadeIn = UI_fade,
                        Command = $"workbench_ui WORKBENCH_NET_ID page previous",
                    },
                Text =
                    {
                        Color = ColorPalette.RustyWhite.rustString,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        FontSize = UI_fontSize+8,
                        Text = "<\n<\n<"
                    },
                FadeOut = UI_fade,
                RectTransform = { AnchorMin = UI_anchorRideMenuPrevious.AnchorMin, AnchorMax = UI_anchorRideMenuPrevious.AnchorMax },
            };

            public CuiButton buttonNext = new CuiButton
            {
                Button =
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed
                        FadeIn = UI_fade,
                        Command = $"workbench_ui WORKBENCH_NET_ID page next",
                    },
                Text =
                    {
                        Color = ColorPalette.RustyWhite.rustString,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        FontSize = UI_fontSize+8,
                        Text = ">\n>\n>"
                    },
                FadeOut = UI_fade,
                RectTransform = { AnchorMin = UI_anchorRideMenuNext.AnchorMin, AnchorMax = UI_anchorRideMenuNext.AnchorMax },
            };

            public CuiButton buttonRide1 = new CuiButton
            {
                Button =
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed to reflect the level
                        FadeIn = UI_fade,
                        Command = $"workbench_ui WORKBENCH_NET_ID ride rideName",
                    },
                Text =
                    {
                        Color = ColorPalette.RustyWhite.rustString,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        FontSize = UI_fontSize+4,
                        Text = "Ride option 1 will be changed"
                    },
                FadeOut = UI_fade,
                RectTransform = { AnchorMin = UI_anchorRideMenuButton1.AnchorMin, AnchorMax = UI_anchorRideMenuButton1.AnchorMax },
            };

            public CuiButton buttonRide2 = new CuiButton
            {
                Button =
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed to reflect whether you can afford it or not
                        FadeIn = UI_fade,
                        Command = $"workbench_ui WORKBENCH_NET_ID ride rideName",
                    },
                Text =
                    {
                        Color = ColorPalette.RustyWhite.rustString,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        FontSize = UI_fontSize+4,
                        Text = "Ride option 2 will be changed"
                    },
                FadeOut = UI_fade,
                RectTransform = { AnchorMin = UI_anchorRideMenuButton2.AnchorMin, AnchorMax = UI_anchorRideMenuButton2.AnchorMax },
            };

            public CuiButton buttonRide3 = new CuiButton
            {
                Button =
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed to reflect whether you can afford it or not
                        FadeIn = UI_fade,
                        Command = $"workbench_ui WORKBENCH_NET_ID ride rideName",
                    },
                Text =
                    {
                        Color = ColorPalette.RustyWhite.rustString,
                        Align = TextAnchor.MiddleCenter,
                        FadeIn = UI_fade,
                        FontSize = UI_fontSize+4,
                        Text = "Ride option 3 will be changed"
                    },
                FadeOut = UI_fade,
                RectTransform = { AnchorMin = UI_anchorRideMenuButton3.AnchorMin, AnchorMax = UI_anchorRideMenuButton3.AnchorMax },
            };

            public static Dictionary<string, RideDefinition> GetAvailableRideDefinitions(int workbenchLevel)
            {
                return Instance.rideDefinitions.OrderBy(d => d.Value.rideNickname).ToDictionary(k => k.Key, k => k.Value);
            }

            public void LoadDefinitionIntoDescription(string definitionName, BasePlayer player)
            {
                if (Instance.rideDefinitions.ContainsKey(definitionName))
                {
                    var definition = Instance.rideDefinitions[definitionName];

                    //title
                    var title = containerDescription[0].Components[0] as CuiTextComponent;

                    title.Text = definition.rideNickname;

                    string titleColor;

                    switch (definition.workbenchLevel)
                    {
                        case 3:
                            {
                                titleColor = ColorPalette.RustyRed.rustString;
                            }
                            break;
                        case 2:
                            {
                                titleColor = ColorPalette.RustyBlue.rustString;
                            }
                            break;
                        default:
                            {
                                titleColor = ColorPalette.RustyGreen.rustString;
                            }
                            break;
                    }

                    title.Color = titleColor;

                    //description
                    var description = containerDescription[1].Components[0] as CuiTextComponent;

                    var musicalStuff = definition.assignedMusic != null ? $"\n<color={ColorPalette.RustyYellow.hexValue}><i>{MSG(MSG_UI_WB_PLAYS_MUSIC, null, definition.assignedMusic.Count)}</i></color>" : "";
                    var buildingStuff = definition.containerType == RideContainerType.TC ? $"\n<color={ColorPalette.RustyYellow.hexValue}><i>{MSG(MSG_UI_WB_HAS_TC)}</i></color>" : "";

                    description.Text = definition.rideDescription + buildingStuff + musicalStuff;

                    var buildCostString = "";

                    var counter = 0;
                    foreach (var cost in definition.rideCost)
                    {
                        var itemDef = ItemManager.FindItemDefinition(cost.Key);

                        if (itemDef == null)
                        {
                            Instance.PrintWarning($"ERROR: Trying to access item cost for {definition.rideName}, but the definition's cost item ID {cost.Key} doesn't seem to be pointing to any valid item. Let Nikedemos know ASAP!");
                        }
                        else
                        {
                            buildCostString += $"{cost.Value} {itemDef.displayName.translated}";
                            buildCostString += counter < definition.rideCost.Count - 1 ? ", " : "";

                            counter++;
                        }
                    }

                    //image...
                    var image = containerDescription[2].Components[0] as CuiRawImageComponent;
                    image.Url = definition.rideImage;

                    //and button
                    var canAfford = CanPlayerAffordRide(player, definition);
                    var insufficientLevel = definition.workbenchLevel > workbenchHelper.level;


                    var craftButton = containerDescription[3].Components[0] as CuiButtonComponent;

                    string buttonColor;

                    var suffix1 = "";
                    var suffix2 = "";
                    var finalSuffix = "";

                    if (insufficientLevel)
                    {
                        suffix1 = $"<size=9><color={ColorPalette.RustyRed.hexValue}>{MSG(MSG_UI_WB_LEVEL_REQUIRED, null, definition.workbenchLevel)} </color></size>";
                    }

                    if (!canAfford)
                    {
                        suffix2 = $"<size=9><color={ColorPalette.RustyRed.hexValue}>{MSG(MSG_UI_WB_CANT_AFFORD_TO_CRAFT)}</color></size>";
                    }

                    if (suffix1 == "" && suffix2 == "")
                    {
                        buttonColor = ColorPalette.RustyGreenDark.rustString;
                        finalSuffix = $"<size=9><color={ColorPalette.RustyWhite.hexValue}>{MSG(MSG_UI_WB_READY_TO_CRAFT)}</color></size>";
                    }
                    else
                    {
                        buttonColor = suffix1 != "" ? ColorPalette.RustyGrey.rustString : ColorPalette.RustyRedDark.rustString;

                        if (suffix2 == "")
                        {
                            finalSuffix = suffix1;
                        }
                        else if (suffix1 == "")
                        {
                            finalSuffix = suffix2;
                        }
                        else if (suffix1 != "" && suffix2 != "")
                        {
                            finalSuffix = $"{suffix1}, {suffix2}";
                        }
                    }


                    craftButton.Color = buttonColor;
                    var craftButtonText = containerDescription[4].Components[0] as CuiTextComponent;

                    craftButtonText.Text = buildCostString+"\n"+ finalSuffix;

                    //command for the button
                    craftButton.Command = $"workbench_ui {workbenchHelper.workbench.net.ID} craft {definition.rideName}";
                }
            }

            public int GetLastPageIndex()
            {
                var availableRideDefinitions = GetAvailableRideDefinitions(workbenchHelper.level).Count();
                return (availableRideDefinitions / WorkbenchHelper.RIDES_PER_PAGE) + (availableRideDefinitions % WorkbenchHelper.RIDES_PER_PAGE) -1;
            }

            public override object ParseCommand(BasePlayer player, ConsoleSystem.Arg arg)
            { 
                bool playerIsAdmin = IsAdmin(player);
                var argCount = arg.Args.Count();

                if (argCount < 2) return null;

                bool paramIsInt = false;

                int paramAsInt = 0;

                string paramAsString = string.Join(" ", arg.Args.Skip(1).ToArray());

                bool refreshDescription = false;
                bool refreshMenu = false;

                if (int.TryParse(arg.Args[1], out paramAsInt))
                {
                    paramIsInt = true;
                }

                switch (arg.Args[0])
                {
                    case "craft":
                        {
                            //check if workbench level checks out
                            if (Instance.rideDefinitions.ContainsKey(paramAsString))
                            {
                                var def = Instance.rideDefinitions[paramAsString];

                                if (def.workbenchLevel <= workbenchHelper.level)
                                {
                                    if (CanPlayerAffordRide(player, def))
                                    {
                                        PlayerCraftRide(player, def, true);
                                        refreshDescription = true;
                                    }
                                }
                            }
                        }
                        break;

                    case "page":
                        {
                            if (arg.Args[1] == "next")
                            {
                                if (workbenchHelper.page < GetLastPageIndex())
                                {
                                    workbenchHelper.page++;
                                    refreshMenu = true;
                                }
                            }
                            else //must be "previous"
                            {
                                if (workbenchHelper.page > 0)
                                {
                                    workbenchHelper.page--;
                                    refreshMenu = true;
                                }
                            }
                        }
                        break;
                    case "ride":
                        {
                            if (Instance.rideDefinitions.ContainsKey(arg.Args[1]))
                            {
                                workbenchHelper.currentDefinition = arg.Args[1];
                                refreshDescription = true;
                            }
                        }
                        break;
                }

                if (refreshDescription)
                {
                    GuiRefreshWorkbenchDescriptionAll();
                }

                if (refreshMenu)
                {
                    GuiRefreshWorkbenchMenuAll();
                }

                return null;
            }

            public void SetPreviousAndNextCommands()
            {
                var previousButton = containerMenu[0].Components[0] as CuiButtonComponent;
                previousButton.Command = $"workbench_ui {workbenchHelper.workbench.net.ID} page previous";

                var nextButton = containerMenu[2].Components[0] as CuiButtonComponent;
                nextButton.Command = $"workbench_ui {workbenchHelper.workbench.net.ID} page next";

            }

            public void SetRideButtonCommands(BasePlayer player)
            {
                var availableRides = GetAvailableRideDefinitions(workbenchHelper.level).Keys.Skip(workbenchHelper.page * WorkbenchHelper.RIDES_PER_PAGE).Take(3).ToArray();

                if (availableRides.Count() > 0)
                {
                    var definition1 = Instance.rideDefinitions[availableRides[0]];

                    SetRideButtonFromDefinition(4, definition1, player);
                }
                else
                {
                    SetRideButtonBlank(4);
                }

                if (availableRides.Count() > 1)
                {
                    var definition2 = Instance.rideDefinitions[availableRides[1]];

                    SetRideButtonFromDefinition(6, definition2, player);
                }
                else
                {
                    SetRideButtonBlank(6);
                }

                if (availableRides.Count() > 2)
                {
                    var definition3 = Instance.rideDefinitions[availableRides[2]];

                    SetRideButtonFromDefinition(8, definition3, player);
                }
                else
                {
                    SetRideButtonBlank(8);
                }

            }

            public void SetRideButtonFromDefinition(int indexStartAt, RideDefinition definition, BasePlayer player)
            {
                var buttonButton = containerMenu[indexStartAt].Components[0] as CuiButtonComponent;
                buttonButton.Command = $"workbench_ui {workbenchHelper.workbench.net.ID} ride {definition.rideName}";

                var levelMet = definition.workbenchLevel <= workbenchHelper.level;

                var buttonColor = !levelMet ? ColorPalette.RustyGrey.rustString : (CanPlayerAffordRide(player, definition) ? ColorPalette.RustyGreenDark.rustString : ColorPalette.RustyRedDark.rustString);

                buttonButton.Color = buttonColor;

                var buttonText = containerMenu[indexStartAt+1].Components[0] as CuiTextComponent;
                buttonText.Text = $"{definition.rideNickname} {definition.rideVersion} by {definition.rideAuthor}";
            }

            public void SetRideButtonBlank(int indexStartAt)
            {
                var buttonButton = containerMenu[indexStartAt].Components[0] as CuiButtonComponent;
                buttonButton.Command = "";
                buttonButton.Color = ColorPalette.RustyBlack.rustString;

                var buttonText = containerMenu[indexStartAt + 1].Components[0] as CuiTextComponent;
                buttonText.Text = "";
            }

            public string GetFirstOrDefaultDefinitionName()
            {
                return GetAvailableRideDefinitions(workbenchHelper.level).Keys.FirstOrDefault();
            }

            public override void PrepareGuiCommon()
            {
                base.PrepareGuiCommon();
                PrepareGuiWorkbenchDescription();
                PrepareGuiWorkbenchMenu();
                SetPreviousAndNextCommands();
            }

            public void PrepareGuiWorkbenchDescription()
            {
                containerDescription.Add(elementDescriptionTitle); //0
                containerDescription.Add(elementDescriptionText); //1
                containerDescription.Add(elementDescriptionImage); //2
                containerDescription.Add(buttonCraft, "Overlay", "argui.description.button"); //3 (button) and 4 (text)
            }

            public void PrepareGuiWorkbenchMenu()
            {
                containerMenu.Add(buttonPrevious, "Overlay", "argui.menu.previous");//0, 1
                containerMenu.Add(buttonNext, "Overlay", "argui.menu.next");//2, 3
                containerMenu.Add(buttonRide1, "Overlay", "argui.menu.button1"); //4, 5
                containerMenu.Add(buttonRide2, "Overlay", "argui.menu.button2"); //6, 7
                containerMenu.Add(buttonRide3, "Overlay", "argui.menu.button3"); //8, 9

            }

            public override void GuiCloseCommon(BasePlayer player)
            {
                base.GuiCloseCommon(player);
                GuiCloseWorkbenchDescription(player);
                GuiCloseWorkbenchMenu(player);
            }

            public void GuiCloseWorkbenchDescription(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "argui.description.title");
                CuiHelper.DestroyUi(player, "argui.description.image");
                CuiHelper.DestroyUi(player, "argui.description.text");
                CuiHelper.DestroyUi(player, "argui.description.button");
            }
            public void GuiCloseWorkbenchMenu(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "argui.menu.previous");
                CuiHelper.DestroyUi(player, "argui.menu.next");
                CuiHelper.DestroyUi(player, "argui.menu.button1");
                CuiHelper.DestroyUi(player, "argui.menu.button2");
                CuiHelper.DestroyUi(player, "argui.menu.button3");
                CuiHelper.DestroyUi(player, "argui.menu.button4");
                CuiHelper.DestroyUi(player, "argui.menu.button5");
                CuiHelper.DestroyUi(player, "argui.menu.button6");
            }


            public void GuiRefreshWorkbenchDescription(BasePlayer player)
            {
                GuiCloseWorkbenchDescription(player);
                GuiOpenWorkbenchDescription(player);
            }

            public void GuiRefreshWorkbenchMenu(BasePlayer player)
            {
                GuiCloseWorkbenchMenu(player);
                GuiOpenWorkbenchMenu(player);
            }

            public void GuiRefreshWorkbenchDescriptionAll()
            {
                foreach (var player in subscribers)
                {
                    GuiRefreshWorkbenchDescription(player.Value);
                }
            }

            public void GuiRefreshWorkbenchMenuAll()
            {
                foreach (var player in subscribers)
                {
                    GuiRefreshWorkbenchMenu(player.Value);
                }
            }

            public override void GuiOpenCommon(BasePlayer player)
            {
                base.GuiOpenCommon(player);

                GuiOpenWorkbenchDescription(player);
                GuiOpenWorkbenchMenu(player);
            }

            public void GuiOpenWorkbenchDescription(BasePlayer player)
            {
                if (workbenchHelper.currentDefinition == null)
                {
                    workbenchHelper.currentDefinition = GetFirstOrDefaultDefinitionName();
                }

                LoadDefinitionIntoDescription(workbenchHelper.currentDefinition, player);

                CuiHelper.AddUi(player, containerDescription);
            }

            public void GuiOpenWorkbenchMenu(BasePlayer player)
            {
                SetRideButtonCommands(player);

                CuiHelper.AddUi(player, containerMenu);
            }

            public static bool CanPlayerAffordRide(BasePlayer player, RideDefinition definition)
            {
                foreach (var c in definition.rideCost)
                {
                    if (player.inventory.GetAmount(c.Key) < c.Value)
                    {
                        return false;
                    }
                }
                return true;
            }

            public static void PlayerCraftRide(BasePlayer player, RideDefinition definition, bool affordabilityAlreadyChecked = true)
            {
                foreach (var c in definition.rideCost)
                {
                    player.inventory.Take(null, c.Key, c.Value);
                    player.Command("note.inv", c.Key, -c.Value);
                }

                Instance.GivePlayerRideItem(definition, player, 1);
            }

            public override void SetTitle()
            {
                string availableLevels = workbenchHelper.level > 2 ? "1, 2 AND 3" : (workbenchHelper.level > 1 ? "1 AND 2" : "1");
                (elementTitleText.Components[0] as CuiTextComponent).Text = $"LEVEL {availableLevels} RIDES";

                SetTitleBGColor();
            }

        }

        public class GenericUI
        {
            public Dictionary<ulong, BasePlayer> subscribers = new Dictionary<ulong, BasePlayer>();

            public CuiElementContainer containerMain = new CuiElementContainer();

            public string colorTitleBG = ColorPalette.RustyGrey.rustString;

            public virtual object ParseCommand(BasePlayer player, ConsoleSystem.Arg arg)
            {
                return null;
            }
            public virtual void SetTitle()
            {
                (elementTitleText.Components[0] as CuiTextComponent).Text = "Generic UI";
                SetTitleBGColor();
            }

            public virtual void SetTitleBGColor()
            {
                (elementTitleBackdrop.Components[0] as CuiImageComponent).Color = colorTitleBG;
            }

            public virtual void PrepareGui()
            {
                PrepareGuiCommon();

                SetTitle();
            }

            public virtual void PrepareGuiCommon()
            {
                //add things to the main container here and override
                containerMain.Add(elementBackdrop);
                containerMain.Add(elementTitleBackdrop);
                containerMain.Add(elementTitleText);

                elementCount = 3;
            }

            public virtual void GuiOpenCommon(BasePlayer player)
            {
                CuiHelper.AddUi(player, containerMain);
            }

            public virtual void GuiCloseCommon(BasePlayer player)
            {
                CuiHelper.DestroyUi(player, "argui.backdrop");
                CuiHelper.DestroyUi(player, "argui.title.backdrop");
                CuiHelper.DestroyUi(player, "argui.title.text");
            }
            public void GuiOpen(BasePlayer player)
            {
                if (!subscribers.ContainsKey(player.userID))
                {
                    subscribers.Add(player.userID, player);
                }


                GuiOpenCommon(player);
            }

            public void GuiClose(BasePlayer player)
            {
                if (subscribers.ContainsKey(player.userID))
                {
                    subscribers.Remove(player.userID);
                }

                GuiCloseCommon(player);
            }

            public void GuiRefresh(BasePlayer player)
            {
                GuiClose(player);
                GuiOpen(player);
            }

            public void GuiCloseAll()
            {
                foreach (var playah in subscribers.ToList())
                {
                    if (playah.Value != null)
                    {
                        GuiClose(playah.Value);
                    }
                    else
                    {
                        subscribers.Remove(playah.Key);
                    }
                }
            }

            public void GuiRefreshAll()
            {
                foreach (var playah in subscribers.ToList())
                {
                    if (playah.Value != null)
                    {
                        GuiRefresh(playah.Value);
                    }
                    else
                    {
                        subscribers.Remove(playah.Key);
                    }
                }
            }

            public int elementCount = 0;

            public CuiElement elementBackdrop = new CuiElement
            {
                Name = "argui.backdrop",
                Parent = "Overlay",
                FadeOut = UI_fade,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = ColorPalette.RustyBlack.rustString,
                        FadeIn = UI_fade
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = UI_anchorMain.AnchorMin,
                        AnchorMax = UI_anchorMain.AnchorMax
                    }
                }
            };

            public CuiElement elementTitleBackdrop = new CuiElement
            {
                Name = "argui.title.backdrop",
                Parent = "Overlay",
                FadeOut = UI_fade,
                Components =
                {
                    new CuiImageComponent
                    {
                        Color = ColorPalette.RustyGrey.rustString, //will be changed
                        FadeIn = UI_fade
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = UI_anchorTitle.AnchorMin,
                        AnchorMax = UI_anchorTitle.AnchorMax
                    }
                }
            };

            public CuiElement elementTitleText = new CuiElement
            {
                Name = "argui.title.text",
                Parent = "Overlay",
                FadeOut = UI_fade,
                Components =
                {
                    new CuiTextComponent
                    {
                        Text = "RIDE NICKNAME",
                        FontSize = UI_fontSizeTitle,
                        Color = ColorPalette.RustyWhite.rustString,
                        FadeIn = UI_fade,
                        Align = TextAnchor.MiddleCenter,
                    },

                    new CuiRectTransformComponent
                    {
                        AnchorMin = UI_anchorTitle.AnchorMin,
                        AnchorMax = UI_anchorTitle.AnchorMax
                    }
                }
            };
        }

        public class RideUI : GenericUI 
        {
            public Ride ride;

            //indices are also the "ar.x" name
            public Dictionary<string, RideUIConfigurable> configurables = new Dictionary<string, RideUIConfigurable>();

            public static string colorTitleBar = ColorPalette.RustyBlueDark.rustString;

            public override object ParseCommand(BasePlayer player, ConsoleSystem.Arg arg)
            {
                bool playerIsAdmin = IsAdmin(player);
                var argCount = arg.Args.Count();

                if (argCount < 2) return null;

                //use precognition

                bool paramIsBool = false;
                bool paramIsFloat;
                bool paramIsString;
                bool paramIsInt = false;

                bool paramAsBool;
                float paramAsFloat = 0F;
                string paramAsString = string.Join(" ", arg.Args.Skip(1).ToArray());
                int paramAsInt = 0;

                string newHintText = null;
                string newHintValue = null;

                bool updateConfigurable = true;
                bool updateGui = true;

                if (float.TryParse(arg.Args[1], out paramAsFloat))
                {
                    paramIsFloat = true;
                }
                if (int.TryParse(arg.Args[1], out paramAsInt))
                {
                    paramIsInt = true;
                }
                if (arg.Args[1] == "toggle")
                {
                    paramIsBool = true;
                    //paramAsBool = arg.Args[1] == "true" ? true : false;
                }

                paramIsString = true;

                bool goOn = true;

                if (ride.rideData.isAdminRide)
                {
                    if (!playerIsAdmin)
                    {
                        updateGui = false;
                        goOn = false;
                        Instance.TellMessage(player, MSG(MSG_ONLY_ADMINS_CAN_MANAGE_THIS_RIDE, player.UserIDString), ride.rideData.nickname);
                    }
                }

                if (goOn)
                {
                    if (argCount > 0)
                    {
                        switch (arg.Args[0])
                        {
                            case "set_nickname":
                                {
                                    //nickname shows in the title
                                    updateConfigurable = false;

                                    var nickname = paramAsString;

                                    if (nickname == "DEFAULT")
                                    {
                                        nickname = ride.definition.rideNickname;
                                    }

                                    if (nickname.Length > 32)
                                    {
                                        nickname = nickname.Substring(0, 32);
                                    }
                                    ride.rideData.nickname = nickname;
                                    SetTitle();

                                    ride.UpdateVendingMachine();
                                }
                                break;
                            case "toggle_only_authorized_ride":
                                {
                                    if (paramIsBool)
                                    {
                                        //toggle in the data
                                        ride.rideData.onlyAuthorizedCanRide = !ride.rideData.onlyAuthorizedCanRide;
                                        newHintValue = ride.rideData.onlyAuthorizedCanRide.ToString();
                                    }
                                }
                                break;
                            case "toggle_only_authorized_operate":
                                {
                                    if (paramIsBool)
                                    {
                                        //toggle in the data
                                        ride.rideData.onlyAuthorizedCanOperate = !ride.rideData.onlyAuthorizedCanOperate;
                                        newHintValue = ride.rideData.onlyAuthorizedCanOperate.ToString();
                                    }
                                }
                                break;
                            case "toggle_broadcast_position":
                                {
                                    if (paramIsBool)
                                    {
                                        //toggle in the data
                                        ride.rideData.broadcastLocation = !ride.rideData.broadcastLocation;
                                        newHintValue = ride.rideData.broadcastLocation.ToString();

                                        ride.UpdateVendingMachine();
                                    }
                                }
                                break;
                            case "toggle_charge_authorized":
                                {
                                    if (paramIsBool)
                                    {
                                        //toggle in the data
                                        ride.rideData.dontChargeAuthorized = !ride.rideData.dontChargeAuthorized;
                                        newHintValue = ride.rideData.dontChargeAuthorized.ToString();
                                    }
                                }
                                break;
                            case "toggle_is_admin_ride":
                                {
                                    if (playerIsAdmin)
                                    {
                                        if (paramIsBool)
                                        {
                                            //toggle in the data
                                            ride.rideData.isAdminRide = !ride.rideData.isAdminRide;
                                            newHintValue = ride.rideData.isAdminRide.ToString();
                                        }
                                    }
                                    else
                                    {
                                        updateGui = false;
                                        Instance.TellMessage(player, MSG(MSG_ONLY_ADMINS_CAN_CHANGE_THIS_SETTING, player.UserIDString), ride.rideData.nickname);
                                        updateConfigurable = false;
                                    }
                                }
                                break;
                            case "toggle_music_auto":
                                {
                                    if (playerIsAdmin)
                                    {
                                        if (paramIsBool)
                                        {
                                            //toggle in the data
                                            ride.rideData.musicAutoMode = !ride.rideData.musicAutoMode;
                                            newHintValue = ride.rideData.musicAutoMode.ToString();
                                        }
                                    }
                                    else
                                    {
                                        updateGui = false;
                                        Instance.TellMessage(player, MSG(MSG_ONLY_ADMINS_CAN_CHANGE_THIS_SETTING, player.UserIDString), ride.rideData.nickname);
                                        updateConfigurable = false;
                                    }
                                }
                                break;
                            case "toggle_enable_chat":
                                {
                                    if (playerIsAdmin)
                                    {
                                        if (paramIsBool)
                                        {
                                            //toggle in the data
                                            ride.rideData.chatEnabled = !ride.rideData.chatEnabled;
                                            newHintValue = ride.rideData.chatEnabled.ToString();
                                        }
                                    }
                                    else
                                    {
                                        updateGui = false;
                                        Instance.TellMessage(player, MSG(MSG_ONLY_ADMINS_CAN_CHANGE_THIS_SETTING, player.UserIDString), ride.rideData.nickname);
                                        updateConfigurable = false;
                                    }
                                }
                                break;

                            case "set_currency":
                                {
                                    ItemDefinition definition;
                                    int itemId = 0;

                                    if (paramIsInt)
                                    {
                                        definition = ItemManager.FindItemDefinition(paramAsInt);
                                        if (definition != null)
                                        {
                                            itemId = paramAsInt;
                                        }
                                    }
                                    else
                                    {
                                        definition = ItemManager.FindItemDefinition(paramAsString);
                                        if (definition != null)
                                        {
                                            itemId = definition.itemid;
                                        }
                                    }

                                    if (definition == null)
                                    {
                                        definition = ItemManager.FindItemDefinition("scrap");
                                        itemId = definition.itemid;
                                    }

                                    if (itemId != 0)
                                    {
                                        ride.rideData.admissionFeeCurrencyID = itemId;
                                        newHintValue = $"{definition.displayName.translated}";
                                        ride.UpdateVendingMachine();
                                    }
                                    else
                                    {
                                        updateGui = false;
                                        //Instance.TellMessage(player, $"Item <i>{paramAsString}</i> was not found by name/ID!", ride.rideData.nickname);
                                        updateConfigurable = false;
                                    }
                                }
                                break;
                            case "set_price":
                                {
                                    var weGood = false;

                                    if (paramIsInt)
                                    {
                                        if (paramAsInt>=0 && paramAsInt < int.MaxValue)
                                        {
                                            weGood = true;
                                        }
                                    }

                                    if (!weGood)
                                    {
                                        updateGui = false;
                                        updateConfigurable = false;
                                    }
                                    else
                                    {
                                        ride.rideData.admissionFeePrice = paramAsInt;
                                        newHintValue = paramAsInt.ToString();
                                        ride.UpdateVendingMachine();
                                    }
                                }
                                break;

                        }

                        //update hint text...
                        if (updateConfigurable)
                        {
                            configurables[$"argui.{arg.Args[0]}"].UpdateConfigurable(newHintText, newHintValue);
                        }
                    }
                }

                if (updateGui)
                {
                    GuiRefreshAll();
                }


                return null;
            }

            public override void PrepareGui()
            {
                PrepareConfigurables();

                base.PrepareGui();
            }

            public virtual void PrepareConfigurables()
            {
                configurables.Add("argui.set_nickname", new RideUIConfigurable
                {
                    ui = this,
                    uiName = "argui.set_nickname",
                    commandName = "set_nickname",
                    type = "text",
                    hint = MSG(MSG_UI_RIDE_NAME, null),
                    anchorPrimary = UI_anchorLineHalfA,
                    anchorSecondary = UI_anchorLineHalfB,

                    alsoShowCurrentValueWithHint = false,
                    lineNumber = 0,
                });

                configurables.Add("argui.toggle_only_authorized_ride", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineHalfA,
                    uiName = "argui.toggle_only_authorized_ride",
                    commandName = "toggle_only_authorized_ride",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_ONLY_AUTHORISED_CAN_RIDE),
                    hintFalse = MSG(MSG_UI_EVERYONE_CAN_RIDE),

                    //swap colors around
                    colorTrue = ColorPalette.RustyRedDark.rustString,
                    colorFalse = ColorPalette.RustyGreenDark.rustString,

                    lineNumber = 1,
                });

                configurables.Add("argui.toggle_only_authorized_operate", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineHalfB,
                    uiName = "argui.toggle_only_authorized_operate",
                    commandName = "toggle_only_authorized_operate",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_ONLY_AUTHORISED_CAN_CONTROL),
                    hintFalse = MSG(MSG_UI_EVERYONE_CAN_CONTROL),

                    //swap colors around
                    colorTrue = ColorPalette.RustyRedDark.rustString,
                    colorFalse = ColorPalette.RustyGreenDark.rustString,

                    lineNumber = 1,
                });

                configurables.Add("argui.toggle_charge_authorized", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineHalfA,
                    uiName = "argui.toggle_charge_authorized",
                    commandName = "toggle_charge_authorized",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_AUTHORISED_FREE),
                    hintFalse = MSG(MSG_UI_AUTHORISED_CHARGE),

                    lineNumber = 2,
                });

                configurables.Add("argui.toggle_broadcast_position", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineHalfB,
                    uiName = "argui.toggle_broadcast_position",
                    commandName = "toggle_broadcast_position",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_BROADCAST_POSITION),
                    hintFalse = MSG(MSG_UI_DONT_BROADCAST_POSITION),

                    lineNumber = 2,
                });

                configurables.Add("argui.toggle_is_admin_ride", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineFull,
                    uiName = "argui.toggle_is_admin_ride",
                    commandName = "toggle_is_admin_ride",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_ADMIN_RIDE),
                    hintFalse = MSG(MSG_UI_NORMAL_RIDE),

                    colorTrue = ColorPalette.RustyRedDark.rustString,
                    colorFalse = ColorPalette.RustyBlueDark.rustString,

                    lineNumber = 3,
                });

                configurables.Add("argui.set_currency", new RideUIConfigurable
                {
                    ui = this,
                    uiName = "argui.set_currency",
                    commandName = "set_currency",
                    type = "text",
                    hint = MSG(MSG_UI_ADMISSION_ITEM),
                    anchorPrimary = UI_anchorLineThirdAB,
                    anchorSecondary = UI_anchorLineThirdC,

                    alsoShowCurrentValueWithHint = true,
                    lineNumber = 4,
                });

                configurables.Add("argui.set_price", new RideUIConfigurable
                {
                    ui = this,
                    uiName = "argui.set_price",
                    commandName = "set_price",
                    type = "text",
                    hint = MSG(MSG_UI_ADMISSION_AMOUNT),
                    anchorPrimary = UI_anchorLineThirdAB,
                    anchorSecondary = UI_anchorLineThirdC,

                    alsoShowCurrentValueWithHint = true,
                    lineNumber = 5,
                });
                
                configurables.Add("argui.toggle_music_auto", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineFull,
                    uiName = "argui.toggle_music_auto",
                    commandName = "toggle_music_auto",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_MUSIC_AUTO),
                    hintFalse = MSG(MSG_UI_MUSIC_MANUAL),

                    colorTrue = ColorPalette.RustyGreenDark.rustString,
                    colorFalse = ColorPalette.RustyRedDark.rustString,

                    lineNumber = 6,
                });

                configurables.Add("argui.toggle_enable_chat", new RideUIConfigurable
                {
                    ui = this,
                    anchorPrimary = UI_anchorLineFull,
                    uiName = "argui.toggle_enable_chat",
                    commandName = "toggle_enable_chat",
                    type = "toggle",

                    hintTrue = MSG(MSG_UI_BROADCAST_CHAT_MESSAGES),
                    hintFalse = MSG(MSG_UI_DONT_BROADCAST_CHAT_MESSAGES),

                    colorTrue = ColorPalette.RustyGreenDark.rustString,
                    colorFalse = ColorPalette.RustyRedDark.rustString,

                    lineNumber = 7,
                });
            }

            public virtual void AddConfigurableElements()
            {
                foreach (var entry in configurables)
                {
                    entry.Value.elementIndexStart = elementCount;

                    //adding the element(s) to container will decrease the count by 1, 2, 3 or w/e
                    entry.Value.AddElementsToContainer();
                }
            }

            public virtual void InitializeConfigurableElementValues()
            {
                //default value
                configurables["argui.toggle_only_authorized_ride"].UpdateConfigurable(null, ride.rideData.onlyAuthorizedCanRide.ToString());
                configurables["argui.toggle_only_authorized_operate"].UpdateConfigurable(null, ride.rideData.onlyAuthorizedCanOperate.ToString());
                configurables["argui.toggle_is_admin_ride"].UpdateConfigurable(null, ride.rideData.isAdminRide.ToString());
                configurables["argui.toggle_charge_authorized"].UpdateConfigurable(null, ride.rideData.dontChargeAuthorized.ToString());
                configurables["argui.toggle_music_auto"].UpdateConfigurable(null, ride.rideData.musicAutoMode.ToString());
                configurables["argui.toggle_enable_chat"].UpdateConfigurable(null, ride.rideData.chatEnabled.ToString());

                var feeDefinition = ItemManager.FindItemDefinition(ride.rideData.admissionFeeCurrencyID);

                if (feeDefinition == null)
                {
                    feeDefinition = ItemManager.FindItemDefinition("Scrap");
                }

                configurables["argui.set_currency"].UpdateConfigurable(null, $"{feeDefinition.displayName.translated}");
                configurables["argui.set_price"].UpdateConfigurable(null, ride.rideData.admissionFeePrice.ToString());
            }

            public virtual void RemoveConfigurableElements()
            {
                foreach (var entry in configurables)
                {
                    entry.Value.RemoveElementFromContainer();
                }
            }

            public override void PrepareGuiCommon()
            {
                base.PrepareGuiCommon();

                AddConfigurableElements();
                InitializeConfigurableElementValues();
            }

            public override void SetTitle()
            {
                (elementTitleText.Components[0] as CuiTextComponent).Text = ride.rideData.nickname;

                SetTitleBGColor();
            }

            public override void GuiCloseCommon(BasePlayer player)
            {
                base.GuiCloseCommon(player);
                GuiCloseConfigurables(player);
            }

            public void GuiCloseConfigurables(BasePlayer player)
            {
                foreach (var entry in configurables)
                {
                    CuiHelper.DestroyUi(player, $"{entry.Key}");
                    CuiHelper.DestroyUi(player, $"{entry.Key}.input");
                    CuiHelper.DestroyUi(player, $"{entry.Key}.panel");
                    CuiHelper.DestroyUi(player, $"{entry.Key}.button");
                }
            }
        }
    }
}
