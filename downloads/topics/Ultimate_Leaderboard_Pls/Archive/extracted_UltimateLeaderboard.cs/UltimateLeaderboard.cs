// #define TESTING

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Oxide.Core;
using Oxide.Core.Database;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Core.SQLite.Libraries;
using Oxide.Game.Rust.Cui;
using Oxide.Plugins.UltimateLeaderboardExtensionMethods;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using Pool = Facepunch.Pool;
using Debug = UnityEngine.Debug;
using Global = Rust.Global;
using Image = UnityEngine.UI.Image;

#if CARBON
using Carbon.Base;
using Carbon.Modules;
#endif

namespace Oxide.Plugins
{
    [Info("Ultimate Leaderboard", "Mevent", "1.2.8")]
    public class UltimateLeaderboard : RustPlugin
    {
        #region Fields

        [PluginReference] private Plugin ImageLibrary = null, ServerPanel = null, Notify = null, UINotify = null, LangAPI = null, BetterChat = null, Clans = null;

        private static UltimateLeaderboard Instance;

#if CARBON
		private ImageDatabaseModule imageDatabase;
#endif
        private bool _isLangAPIReady;

        private bool _enabledImageLibrary;

        private IPlayerDataStorage dataStorage;
        private Dictionary<ulong, PlayerStats> playerStats = new();

        private MemoryLeaderboard Leaderboard;
        private bool isLeaderboard;
        private Coroutine cacheCoroutine;

        private const string
            PERM_Profile = "ultimateleaderboard.profile",
            Layer = "UI.Ultimate.Leaderboard",
            LayerContent = "UI.Ultimate.Leaderboard.Section.Content";

        private (bool spStatus, int categoryID) _serverPanelCategory = (false, -1); // key - use serverPanel, value - category id

        private Dictionary<ulong, float> profileRequestCooldown = new Dictionary<ulong, float>();
        private const int ProfileCooldownDuration = 5;
        private HashSet<ulong> loadingProfiles = new HashSet<ulong>();

        #endregion

        #region Config

        public static Configuration _config;

        public class Configuration
        {
            #region Fields

            [JsonProperty(PropertyName = "Permission to use plugin (ex: ultimateleaderboard.use)")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Work with Notify?")]
            public bool UseNotify = true;

            [JsonProperty(PropertyName = "Commands",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string[] Commands = {"leaderboard", "stats"};

            [JsonProperty(PropertyName = "ServerPanel Template (V1, V2)")]
            public PatternServerMenu Pattern;

            [JsonProperty(PropertyName = "Leaderboard Cache Interval (seconds)")]
            public int LeaderboardCacheInterval = 600;

            [JsonProperty(PropertyName = "Wipe data on new save")]
            public bool WipeDataOnNewSave = true;

            [JsonProperty(PropertyName = "Count NPC kills as player kills")]
            public bool CountNPCKillsAsPlayerKills = false;

            [JsonProperty(PropertyName = "Data Storage Settings")]
            public StorageInfo Storage = new()
            {
                StorageType = StorageType.SQLite,
                SQLite = new StorageInfo.SQLiteSettings
                {
                    DatabaseName = "players.db"
                },
                MySQL = new StorageInfo.MySQLSettings
                {
                    Host = "localhost",
                    Port = 3306,
                    DatabaseName = "rust",
                    Username = "root",
                    Password = ""
                }
            };

            [JsonProperty(PropertyName = "Loot Settings")]
            public LootSettings Loot = new()
            {
                Enabled = true,
                Loots = new List<LootSettings.LootEntry>
                {
                    LootSettings.LootEntry.Create("kills", LootSettings.LootType.Kill),
                    LootSettings.LootEntry.Create("deaths", LootSettings.LootType.Kill, -1f),
                    LootSettings.LootEntry.Create("stones", LootSettings.LootType.Gather, 0.1f),
                    LootSettings.LootEntry.Create("supply_drop", LootSettings.LootType.LootItems, 3),
                    LootSettings.LootEntry.Create("crate_normal", LootSettings.LootType.LootItems, 0.3f),
                    LootSettings.LootEntry.Create("crate_elite", LootSettings.LootType.LootItems, 0.5f),
                    LootSettings.LootEntry.Create("bradley_crate", LootSettings.LootType.LootItems, 5f),
                    LootSettings.LootEntry.Create("heli_crate", LootSettings.LootType.LootItems, 5f),
                    LootSettings.LootEntry.Create("bradleyapc", LootSettings.LootType.Kill, 10f),
                    LootSettings.LootEntry.Create("patrolhelicopter", LootSettings.LootType.Kill, 15f),
                    LootSettings.LootEntry.Create("barrel", LootSettings.LootType.Kill, 0.1f),
                    LootSettings.LootEntry.Create("npc_tunneldweller|npc_tunneldwellerspawned|npc_underwaterdweller|scientistnpc_arena|scientistnpc_bradley|scientistnpc_bradley_heavy|scientistnpc_cargo|scientistnpc_cargo_turret_any|scientistnpc_cargo_turret_lr300|scientistnpc_ch47_gunner|scientistnpc_excavator|scientistnpc_full_any|scientistnpc_full_lr300|scientistnpc_full_mp5|scientistnpc_full_pistol|scientistnpc_full_shotgun|scientistnpc_junkpile_pistol|scientistnpc_oilrig|scientistnpc_patrol|scientistnpc_patrol_arctic|scientistnpc_peacekeeper|scientistnpc_roam|scientistnpc_roam_nvg_variant|scientistnpc_roamtethered", LootSettings.LootType.Kill, 0.5f),
                    LootSettings.LootEntry.Create("scientistnpc_heavy", LootSettings.LootType.Kill, 2f),
                    LootSettings.LootEntry.Create("sulfur.ore", LootSettings.LootType.Gather, 0.5f),
                    LootSettings.LootEntry.Create("metal.ore", LootSettings.LootType.Gather, 0.5f),
                    LootSettings.LootEntry.Create("hq.metal.ore", LootSettings.LootType.Gather, 0.5f),
                    LootSettings.LootEntry.Create("stones", LootSettings.LootType.Gather, 0.5f),
                    LootSettings.LootEntry.Create("cupboard.tool.deployed", LootSettings.LootType.Kill)
                }
            };

            [JsonProperty(PropertyName = "Tabs", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<TabConfig> Tabs = new()
            {
                new TabConfig
                {
                    Name = "GENERAL",
                    Blocks = new List<BlockConfig>
                    {
                        new()
                        {
                            BlockType = "Profile",
                            StatFields = new List<StatFieldConfig>
                            {
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "name",
                                    Icon = "avatar",
                                    Title = "Profile"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "kd",
                                    Icon = "https://i.ibb.co/TcWCQjd/icon-kd.png",
                                    Title = "K/D"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "current_playtime",
                                    SecondPrefab = "formatеed_total_playtime",
                                    Icon = "https://i.ibb.co/LrYg0tM/icon-time.png",
                                    Title = "Session duration"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "longest_kill_distance",
                                    Icon = "https://i.ibb.co/6RZcHWrz/Frame-Icon-1-1.png",
                                    Title = "Longest Kill Distance"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "total_resources",
                                    Icon = "https://i.ibb.co/0jd7Kcyv/Frame-Icon-1-2.png",
                                    Title = "Total Resources Gathered"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "favorite_resource",
                                    Icon = "https://i.ibb.co/GfpgX0Rm/Frame-Icon-1-3.png",
                                    Title = "Favorite Resource"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "total_items_crafted",
                                    Icon = "https://i.ibb.co/3YLFnH9s/Frame-Icon-1-4.png",
                                    Title = "Total Items Crafted"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "structures_built",
                                    Icon = "https://i.ibb.co/qLf15T0F/Frame-Icon-1-5.png",
                                    Title = "Structures Built"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "upgrades_performed",
                                    Icon = "https://i.ibb.co/wr77GyGX/Frame-Icon-1-6.png",
                                    Title = "Upgrades Performed"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "favorite_building_material",
                                    Icon = "https://i.ibb.co/Zphz6ggv/Frame-Icon-1-7.png",
                                    Title = "Favorite Building Material"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Icon = "https://i.ibb.co/4RLGH90w/Frame-Icon-1-8.png",
                                    Prefab = "events_won",
                                    Title = "Events Won"
                                },
                                new()
                                {
                                    Type = LootSettings.LootType.Custom,
                                    Prefab = "favorite_event",
                                    Icon = "https://i.ibb.co/wFCXGgLn/Frame-Icon-1-9.png",
                                    Title = "Favorite Event"
                                }
                            }
                        },
                        new()
                        {
                            BlockType = "Statistics",
                            Columns = new List<ColumnConfig>
                            {
                                new()
                                {
                                    Title = "EVENTS",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "Convoy",
                                            Title = "Convoy",
                                            Icon = "https://i.ibb.co/Jzf9bq6/icon-convoy.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "Sputnik",
                                            Title = "Sputnik",
                                            Icon = "https://i.ibb.co/DgQRGH6/icon-sputnik.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "Caravan",
                                            Title = "Caravan",
                                            Icon = "https://i.ibb.co/TqHMK1k/icon-caravan-png.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "GasStationEvent",
                                            Title = "GasStation",
                                            Icon = "https://i.ibb.co/mDHn5FL/icon-gas-station.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "AirEvent",
                                            Title = "Air",
                                            Icon = "https://i.ibb.co/0M2TxGw/icon-air-event.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "SatDishEvent",
                                            Title = "SatDish",
                                            Icon = "https://i.ibb.co/1qVQLwK/icon-sat-dish.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "Triangulation",
                                            Title = "Triangulation",
                                            Icon = "https://i.ibb.co/wYPd8Md/icon-triangulation.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "WaterEvent",
                                            Title = "Water",
                                            Icon = "https://i.ibb.co/0Fj08WP/icon-Water-Treatment.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "HarborEvent",
                                            Title = "Harbor",
                                            Icon = "https://i.ibb.co/WF8ZnDH/icon-harbor.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "ArcticBaseEvent",
                                            Title = "ArcticBase",
                                            Icon = "https://i.ibb.co/9GrVv3Z/icon-arctic-base.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "JunkyardEvent",
                                            Title = "Junkyard",
                                            Icon = "https://i.ibb.co/D4cZ1n2/icon-Junk-Yard.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "SupermarketEvent",
                                            Title = "Supermarket",
                                            Icon = "https://i.ibb.co/mNR56hB/icon-supermarket.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Event,
                                            Prefab = "PowerPlantEvent",
                                            Title = "PowerPlant",
                                            Icon = "https://i.ibb.co/VC15pr1/icon-power-plant.png"
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                new TabConfig
                {
                    Name = "RESOURCES",
                    Blocks = new List<BlockConfig>
                    {
                        new()
                        {
                            BlockType = "Statistics",
                            Columns = new List<ColumnConfig>
                            {
                                new()
                                {
                                    Title = "RESOURCES",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "stones",
                                            Title = "Stone"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "wood",
                                            Title = "Wood"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "sulfur.ore",
                                            Title = "Sulfur Ore"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "metal.ore",
                                            Title = "Metal Ore"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "hq.metal.ore",
                                            Title = "High Quality Metal Ore"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "leather",
                                            Title = "Leather"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "bone.fragments",
                                            Title = "Bone Fragments"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "fat.animal",
                                            Title = "Animal Fat"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.LootItems,
                                            Prefab = "scrap",
                                            Title = "Scrap"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "FARMING",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "hemp-collectable",
                                            Icon = "assets/prefabs/plants/hemp/hemp_clone.icon.png",
                                            Title = "Hemp"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "blue.berry",
                                            Title = "Blue Berry"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "red.berry",
                                            Title = "Red Berry"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "yellow.berry",
                                            Title = "Yellow Berry"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "black.berry",
                                            Title = "Black Berry"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "green.berry",
                                            Title = "Green Berry"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "white.berry",
                                            Title = "White Berry"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "potato",
                                            Title = "Potato"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "cloth",
                                            Title = "Cloth"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "mushroom",
                                            Title = "Mushroom"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "corn",
                                            Title = "Corn"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "pumpkin",
                                            Title = "Pumpkin"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "orchid",
                                            Title = "Orchid"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "rose",
                                            Title = "Rose"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "sunflower",
                                            Title = "Sunflower"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Gather,
                                            Prefab = "wheat",
                                            Title = "Wheat"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "MISC",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "bear",
                                            Icon = "assets/rust.ai/agents/bear/bearavatar.png",
                                            Title = "Bears"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "polarbear",
                                            Icon = "assets/rust.ai/agents/bear/polarbearavatar.png",
                                            Title = "Polar Bears"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "boar",
                                            Icon = "assets/rust.ai/agents/boar/boaravatar.png",
                                            Title = "Boars"
                                        },
                                        // new()
                                        // {
                                        //     Type = LootSettings.LootType.Kill,
                                        //     Prefab = "chicken",
                                        //     Icon = "assets/rust.ai/agents/chicken/chickenavatar.png",
                                        //     Title = "Chicken"
                                        // },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "stag",
                                            Icon = "assets/rust.ai/agents/stag/stagavatar.png",
                                            Title = "Stag"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "wolf2",
                                            Icon = "assets/rust.ai/agents/wolf/wolfavatar.png",
                                            Title = "Wolf"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "simpleshark",
                                            Icon = "assets/rust.ai/agents/fish/shark/sharkavatar.png",
                                            Title = "Shark"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "bradleyapc",
                                            Icon = "assets/prefabs/npc/m2bradley/bradleyavatar.png",
                                            Title = "Bradley"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "patrolhelicopter",
                                            Icon = "assets/prefabs/npc/patrol helicopter/patrolhelicopteravatar.png",
                                            Title = "Helicopter"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab =
                                                "npc_underwaterdweller|scientistnpc_arena|scientistnpc_bradley|scientistnpc_bradley_heavy|scientistnpc_cargo|scientistnpc_cargo_turret_any|scientistnpc_cargo_turret_lr300|scientistnpc_ch47_gunner|scientistnpc_excavator|scientistnpc_full_any|scientistnpc_full_lr300|scientistnpc_full_mp5|scientistnpc_full_pistol|scientistnpc_full_shotgun|scientistnpc_junkpile_pistol|scientistnpc_oilrig|scientistnpc_patrol|scientistnpc_patrol_arctic|scientistnpc_peacekeeper|scientistnpc_roam|scientistnpc_roam_nvg_variant|scientistnpc_roamtethered",
                                            Icon =
                                                "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistavatar.png",
                                            Title = "Scientist"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "scientistnpc_heavy",
                                            Icon =
                                                "assets/rust.ai/agents/npcplayer/humannpc/scientist/heavyscientistavatar.png",
                                            Title = "Heavy Scientist"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "npc_tunneldweller|npc_tunneldwellerspawned",
                                            Icon =
                                                "https://i.ibb.co/JFS7L9CT/image.png",
                                            Title = "Tunnel Dweller"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Kill,
                                            Prefab = "npc_underwaterdweller",
                                            Icon =
                                                "https://i.ibb.co/7t9z9fPQ/image.png",
                                            Title = "Underwater Lab Dweller"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "RAID",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "explosive.satchel",
                                            Title = "Satchel"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "grenade.molotov",
                                            Title = "Molotov"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "grenade.flashbang",
                                            Title = "Flashbang"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "surveycharge",
                                            Title = "Survey Charge"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "grenade.f1",
                                            Title = "Grenade"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "grenade.beancan",
                                            Title = "Beancan"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "ammo.rocket.basic",
                                            Title = "Rocket"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "ammo.rocket.fire",
                                            Title = "Incendiary Rocket"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "ammo.rocket.hv",
                                            Title = "Rocket HV"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.rifle.explosive",
                                            Title = "Explosive 5.56 Rifle Ammo"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "ammo.grenadelauncher.he",
                                            Title = "GL HE"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ExplosiveUsed,
                                            Prefab = "explosive.timed",
                                            Title = "C4"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.rocket.mlrs",
                                            Title = "MLRS Rocket"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "RECYCLED",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "propanetank",
                                            Title = "Propane Tanks"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "gears",
                                            Title = "Gears"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "metalpipe",
                                            Title = "Metal Pipe"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "riflebody",
                                            Title = "Rifle Body"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "semibody",
                                            Title = "Semi Body"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "metalspring",
                                            Title = "Metal Springs"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "roadsigns",
                                            Title = "Road Signs"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "sewingkit",
                                            Title = "Sewing Kits"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "tarp",
                                            Title = "Tarp"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "rope",
                                            Title = "Rope"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "sheetmetal",
                                            Title = "Sheet Metal"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "fuse",
                                            Title = "Fuse"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "metalblade",
                                            Title = "Metal Blade"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "smgbody",
                                            Title = "SMG Body"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "techparts",
                                            Title = "Tech Parts"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "targeting.computer",
                                            Title = "Targeting Computer"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.RecycleItem,
                                            Prefab = "cctv.camera",
                                            Title = "CCTV Camera"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "FIRED",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.pistol",
                                            Title = "Pistol"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.pistol.fire",
                                            Title = "Pistol Incendiary"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.pistol.hv",
                                            Title = "Pistol HV"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.nailgun.nails",
                                            Title = "Nailguns"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.rifle",
                                            Title = "Rifle"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.rifle.hv",
                                            Title = "Rifle HV"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.rifle.incendiary",
                                            Title = "Rifle Incendiary"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.handmade.shell",
                                            Title = "HandMades"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.grenadelauncher.buckshot",
                                            Title = "GL Buckshot"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.shotgun.slug",
                                            Title = "Shotgun Slug"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.shotgun.fire",
                                            Title = "Shotgun Fire"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "ammo.shotgun",
                                            Title = "Shotgun"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "snowball",
                                            Title = "SnowBall"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "arrow.wooden",
                                            Title = "Wooden Arrow"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "arrow.fire",
                                            Title = "Fire Arrow"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "arrow.bone",
                                            Title = "Bone Arrow"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.ShotFired,
                                            Prefab = "arrow.hv",
                                            Title = "High Velocity Arrow"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "FISHING",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.anchovy",
                                            Title = "Anchovy"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.catfish",
                                            Title = "Catfish"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.herring",
                                            Title = "Herring"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.orangeroughy",
                                            Title = "Orange Roughy"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.salmon",
                                            Title = "Salmon"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.sardine",
                                            Title = "Sardine"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.smallshark",
                                            Title = "Small Shark"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.troutsmall",
                                            Title = "Small Trout"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.yellowperch",
                                            Title = "Yellow Perch"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Fishing,
                                            Prefab = "fish.minnows",
                                            Title = "Minnows"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "CRAFTED",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "wall.frame.garagedoor",
                                            Title = "Garage Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "door.double.hinged.metal",
                                            Title = "Sheet Metal Double Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "door.double.hinged.toptier",
                                            Title = "Armored Double Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "door.double.hinged.wood",
                                            Title = "Wood Double Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "door.hinged.metal",
                                            Title = "Sheet Metal Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "door.hinged.toptier",
                                            Title = "Armored Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "door.hinged.wood",
                                            Title = "Wooden Door"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "box.wooden.large",
                                            Title = "Large Wood Box"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "box.wooden",
                                            Title = "Wood Storage Box"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "flameturret",
                                            Title = "Flame Turret"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "guntrap",
                                            Title = "Shotgun Trap"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "autoturret",
                                            Title = "Auto Turret"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "lock.code",
                                            Title = "Code Lock"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "lock.key",
                                            Title = "Key Lock"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "ladder.wooden.wall",
                                            Title = "Wooden Ladder"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "syringe.medical",
                                            Title = "Medical Syringe"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "bandage",
                                            Title = "Bandage"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "largemedkit",
                                            Title = "Large Medkit"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "rocket.launcher",
                                            Title = "Rocket Launcher"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "ammo.rocket.basic",
                                            Title = "Rocket"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "ammo.rocket.fire",
                                            Title = "Incendiary Rocket"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "ammo.rocket.hv",
                                            Title = "HV Rocket"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "explosive.timed",
                                            Title = "C4"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "explosive.satchel",
                                            Title = "Satchel"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "grenade.beancan",
                                            Title = "Beancan"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "grenade.f1",
                                            Title = "Grenade"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "ammo.rifle.explosive",
                                            Title = "Explosive 5.56 Rifle Ammo"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "explosives",
                                            Title = "Explosives"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "gunpowder",
                                            Title = "Gun Powder"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Craft,
                                            Prefab = "lowgradefuel",
                                            Title = "Low Grade Fuel"
                                        }
                                    }
                                },
                                new()
                                {
                                    Title = "LOOTED",
                                    StatFields = new List<StatFieldConfig>
                                    {
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "crate_normal_2",
                                            Title = "Normal Crate",
                                            Icon = "https://i.ibb.co/tsgZ5Nr/radtown-crate-normal-2.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "crate_basic",
                                            Title = "Basic Crate",
                                            Icon = "https://i.ibb.co/C2gJtPN/radtown-crate-basic.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "crate_elite",
                                            Title = "Elite Crate",
                                            Icon = "https://i.ibb.co/x7cygHS/radtown-crate-elite.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "crate_normal",
                                            Title = "Military Crate",
                                            Icon = "https://i.ibb.co/YTCh1Ns/radtown-crate-normal.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "crate_tools",
                                            Title = "Tools Crate",
                                            Icon = "https://i.ibb.co/7gMXbJh/radtown-crate-tools.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "supply_drop",
                                            Title = "Supply Drop",
                                            Icon = "https://i.ibb.co/YD9P1HP/supplydrop-supply-drop.png"
                                        },
                                        new()
                                        {
                                            Type = LootSettings.LootType.Crate,
                                            Prefab = "codelockedhackablecrate|codelockedhackablecrate_oilrig",
                                            Title = "Locked Crate",
                                            Icon =
                                                "https://i.ibb.co/ryBz30Q/deployable-chinooklockedcrate-codelockedhackablecrate.png"
                                        }
                                    }
                                }
                            },
                            StatFields = new List<StatFieldConfig>(),
                            HitRateImages = new List<HitRateConfig>()
                        }
                    }
                },
                new TabConfig
                {
                    Name = "BUILDING",
                    Blocks = new List<BlockConfig>
                    {
                        new()
                        {
                            BlockType = "Building",
                            GradeBanners = new List<GradeBannerConfig>
                            {
                                new()
                                {
                                    Grade = BuildingGrade.Enum.Twigs.ToString().ToLower(),
                                    Image = "https://i.ibb.co/Cs9kRnth/Building-Banner-Twigs.png"
                                },
                                new()
                                {
                                    Grade = BuildingGrade.Enum.Wood.ToString().ToLower(),
                                    Image = "https://i.ibb.co/V0kqGfcj/Building-Banner-Wood.png"
                                },
                                new()
                                {
                                    Grade = BuildingGrade.Enum.Stone.ToString().ToLower(),
                                    Image = "https://i.ibb.co/ccDCwSs2/Building-Banner-Stones.png"
                                },
                                new()
                                {
                                    Grade = BuildingGrade.Enum.Metal.ToString().ToLower(),
                                    Image = "https://i.ibb.co/F43JWdVw/Building-Banner-Metal.png"
                                },
                                new()
                                {
                                    Grade = BuildingGrade.Enum.TopTier.ToString().ToLower(),
                                    Image = "https://i.ibb.co/FL5Bqb2V/Building-Banner-HQM.png"
                                }
                            }
                        }
                    }
                },
                new TabConfig
                {
                    Name = "HITRATE",
                    Blocks = new List<BlockConfig>
                    {
                        new()
                        {
                            BlockType = "HitRate",
                            StatFields = new List<StatFieldConfig>
                            {
                                new()
                                {
                                    Type = LootSettings.LootType.Kill,
                                    Prefab = "HEAD",
                                    Icon = null,
                                    Title = "Head kills"
                                }
                            },
                            HitRateImages = new List<HitRateConfig>
                            {
                                new()
                                {
                                    MinHits = 0,
                                    MaxHits = 99,
                                    Image = "https://i.ibb.co/GvZW4wD/233671bb100d.png"
                                },
                                new()
                                {
                                    MinHits = 100,
                                    MaxHits = 299,
                                    Image = "https://i.ibb.co/k5brMhL/d1f5b7f08571.png"
                                },
                                new()
                                {
                                    MinHits = 300,
                                    MaxHits = 999999999,
                                    Image = "https://i.ibb.co/WB0yB0H/585b8ee89a0e.png"
                                }
                            }
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<CategoryConfig> Categories = new()
            {
                new CategoryConfig {Title = "MY STATISTICS", Type = "default", Enabled = true},
                new CategoryConfig {Title = "TOP 10 PLAYERS", Type = "top", Enabled = true},
                new CategoryConfig {Title = "SEARCH", Type = "search", Enabled = true}
            };

            [JsonProperty(PropertyName = "Leaderboard Columns",
                ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LeaderboardColumn> LeaderboardColumns = new()
            {
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Custom,
                    Prefab = "nickname",
                    Name = "Nickname",
                    Width = 210
                },
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Kill,
                    Prefab = "kills",
                    Name = "Kills",
                    Width = 100
                },
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Death,
                    Prefab = "deaths",
                    Name = "Deaths",
                    Width = 100
                },
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Custom,
                    Prefab = "kdr",
                    Name = "KDR",
                    Width = 100
                },
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Kill,
                    Prefab = "npc_tunneldweller|npc_tunneldwellerspawned|npc_underwaterdweller|scientistnpc_arena|scientistnpc_bradley|scientistnpc_bradley_heavy|scientistnpc_cargo|scientistnpc_cargo_turret_any|scientistnpc_cargo_turret_lr300|scientistnpc_ch47_gunner|scientistnpc_excavator|scientistnpc_full_any|scientistnpc_full_lr300|scientistnpc_full_mp5|scientistnpc_full_pistol|scientistnpc_full_shotgun|scientistnpc_heavy|scientistnpc_junkpile_pistol|scientistnpc_oilrig|scientistnpc_patrol|scientistnpc_patrol_arctic|scientistnpc_peacekeeper|scientistnpc_roam|scientistnpc_roam_nvg_variant|scientistnpc_roamtethered",
                    Name = "NPC Kills",
                    Width = 100
                },
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Custom,
                    Prefab = "formatеed_total_playtime",
                    Name = "Total online",
                    Width = 120
                },
                new LeaderboardColumn
                {
                    Type = LootSettings.LootType.Custom,
                    Prefab = "points",
                    Name = "Scores",
                    Width = 100,
                    IsDefault = true
                }
            };

            [JsonProperty(PropertyName = "Awards")]
            public AwardsConfig Awards = new()
            {
                OnWipeAward = true,
                Categories = new List<AwardCategory>
                {
                    new()
                    {
                        Enabled = true,
                        Title = "TOP mushroomers",
                        Type = LootSettings.LootType.Gather,
                        Prefab = "mushroom",
                        Places = new List<AwardConfig>
                        {
                            new()
                            {
                                Place = 1,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Frontiersman Pack",
                                Image = "https://i.ibb.co/BnxYzfC/image.png"
                            },
                            new()
                            {
                                Place = 2,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Lumberjack Pack",
                                Image = "https://i.ibb.co/hL4wPtM/image.png"
                            },
                            new()
                            {
                                Place = 3,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Artic Pack",
                                Image = "https://i.ibb.co/SXvwsX9/image.png"
                            },
                            new()
                            {
                                Place = 4,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Abyss Pack",
                                Image = "https://i.ibb.co/HnG6Mhx/image.png"
                            },
                            new()
                            {
                                Place = 5,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Retro Tool Cupboard",
                                Image = "https://i.ibb.co/cNpxwLs/image.png"
                            }
                        }
                    },
                    new()
                    {
                        Enabled = true,
                        Title = "TOP killers",
                        Type = LootSettings.LootType.Kill,
                        Prefab = "kills",
                        Places = new List<AwardConfig>
                        {
                            new()
                            {
                                Place = 1,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Frontiersman Pack",
                                Image = "https://i.ibb.co/BnxYzfC/image.png"
                            },
                            new()
                            {
                                Place = 2,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Lumberjack Pack",
                                Image = "https://i.ibb.co/hL4wPtM/image.png"
                            },
                            new()
                            {
                                Place = 3,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Artic Pack",
                                Image = "https://i.ibb.co/SXvwsX9/image.png"
                            },
                            new()
                            {
                                Place = 4,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Abyss Pack",
                                Image = "https://i.ibb.co/HnG6Mhx/image.png"
                            },
                            new()
                            {
                                Place = 5,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Retro Tool Cupboard",
                                Image = "https://i.ibb.co/cNpxwLs/image.png"
                            }
                        }
                    },
                    new()
                    {
                        Enabled = true,
                        Title = "TOP hunters",
                        Type = LootSettings.LootType.Kill,
                        Prefab = "bear",
                        Places = new List<AwardConfig>
                        {
                            new()
                            {
                                Place = 1,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Frontiersman Pack",
                                Image = "https://i.ibb.co/BnxYzfC/image.png"
                            },
                            new()
                            {
                                Place = 2,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Lumberjack Pack",
                                Image = "https://i.ibb.co/hL4wPtM/image.png"
                            },
                            new()
                            {
                                Place = 3,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Artic Pack",
                                Image = "https://i.ibb.co/SXvwsX9/image.png"
                            },
                            new()
                            {
                                Place = 4,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Abyss Pack",
                                Image = "https://i.ibb.co/HnG6Mhx/image.png"
                            },
                            new()
                            {
                                Place = 5,
                                Type = AwardConfig.AwardType.Command,
                                Title = "Retro Tool Cupboard",
                                Image = "https://i.ibb.co/cNpxwLs/image.png"
                            }
                        }
                    }
                }
            };

            [JsonProperty(PropertyName = "Auto Messages")]
            public AutoMessagesConfig AutoMessages = new()
            {
                Chat = new ChatMessagesConfig
                {
                    Enabled = false,
                    MessageInterval = 3600,
                    Messages = new List<MessageConfig>
                    {
                        new MessageConfig { Header = "Top 5 Points:", LootType = LootSettings.LootType.Custom, Prefab = "points", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Most Time Played:", LootType = LootSettings.LootType.Custom, Prefab = "formatеed_total_playtime", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Killers:", LootType = LootSettings.LootType.Kill, Prefab = "kills", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Explosive:", LootType = LootSettings.LootType.ExplosiveUsed, Prefab = "explosive.satchel|grenade.molotov|grenade.flashbang|surveycharge|grenade.f1|grenade.beancan|ammo.rocket.basic|ammo.rocket.fire|ammo.rocket.hv|ammo.grenadelauncher.he|explosive.timed|", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Npc kills:", LootType = LootSettings.LootType.Kill, Prefab = "npc_tunneldweller|npc_tunneldwellerspawned|npc_underwaterdweller|scientistnpc_arena|scientistnpc_bradley|scientistnpc_bradley_heavy|scientistnpc_cargo|scientistnpc_cargo_turret_any|scientistnpc_cargo_turret_lr300|scientistnpc_ch47_gunner|scientistnpc_excavator|scientistnpc_full_any|scientistnpc_full_lr300|scientistnpc_full_mp5|scientistnpc_full_pistol|scientistnpc_full_shotgun|scientistnpc_heavy|scientistnpc_junkpile_pistol|scientistnpc_oilrig|scientistnpc_patrol|scientistnpc_patrol_arctic|scientistnpc_peacekeeper|scientistnpc_roam|scientistnpc_roam_nvg_variant|scientistnpc_roamtethered", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Resources Gathered:", LootType = LootSettings.LootType.Custom, Prefab = "total_resources", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Animal killer:", LootType = LootSettings.LootType.Kill, Prefab = "bear|polarbear|boar|stag|wolf2|simpleshark", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Builder:", LootType = LootSettings.LootType.Custom, Prefab = "structures_built", Limit = 5 }
                    }
                },
                Discord = new DiscordMessagesConfig
                {
                    Enabled = false,
                    MessageInterval = 3600,
                    Webhook = "",
                    Messages = new List<MessageConfig>
                    {
                        new MessageConfig { Header = "Top 5 Points:", LootType = LootSettings.LootType.Custom, Prefab = "points", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Most Time Played:", LootType = LootSettings.LootType.Custom, Prefab = "formatеed_total_playtime", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Killers:", LootType = LootSettings.LootType.Kill, Prefab = "kills", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Explosive:", LootType = LootSettings.LootType.ExplosiveUsed, Prefab = "explosive.satchel|grenade.molotov|grenade.flashbang|surveycharge|grenade.f1|grenade.beancan|ammo.rocket.basic|ammo.rocket.fire|ammo.rocket.hv|ammo.grenadelauncher.he|explosive.timed|", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Npc kills:", LootType = LootSettings.LootType.Kill, Prefab = "npc_tunneldweller|npc_tunneldwellerspawned|npc_underwaterdweller|scientistnpc_arena|scientistnpc_bradley|scientistnpc_bradley_heavy|scientistnpc_cargo|scientistnpc_cargo_turret_any|scientistnpc_cargo_turret_lr300|scientistnpc_ch47_gunner|scientistnpc_excavator|scientistnpc_full_any|scientistnpc_full_lr300|scientistnpc_full_mp5|scientistnpc_full_pistol|scientistnpc_full_shotgun|scientistnpc_heavy|scientistnpc_junkpile_pistol|scientistnpc_oilrig|scientistnpc_patrol|scientistnpc_patrol_arctic|scientistnpc_peacekeeper|scientistnpc_roam|scientistnpc_roam_nvg_variant|scientistnpc_roamtethered", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Resources Gathered:", LootType = LootSettings.LootType.Custom, Prefab = "total_resources", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Animal killer:", LootType = LootSettings.LootType.Kill, Prefab = "bear|polarbear|boar|stag|wolf2|simpleshark", Limit = 5 },
                        new MessageConfig { Header = "Top 5 Builder:", LootType = LootSettings.LootType.Custom, Prefab = "structures_built", Limit = 5 }
                    },
                    LineColors = new[] {0xFFFFFF}
                },
                SendSameMessageToBoth = false
            };

            [JsonProperty(PropertyName = "Custom Titles")]
            public CustomTitlesConfig CustomTitles = new()
            {
                Enabled = false,
                MaxTitles = 1,
                Titles = new List<CustomTitleConfig>
                {
                    new CustomTitleConfig { Title = "[Hunter]", LootType = LootSettings.LootType.Kill, Prefab = "bear|polarbear|boar|stag|wolf2|simpleshark", Limit = 1},
                    new CustomTitleConfig { Title = "[Builder]", LootType = LootSettings.LootType.Custom, Prefab = "structures_built", Limit = 1},
                    new CustomTitleConfig { Title = "[Gatherer]", LootType = LootSettings.LootType.Custom, Prefab = "total_resources", Limit = 1},
                    new CustomTitleConfig { Title = "[Killer]", LootType = LootSettings.LootType.Kill, Prefab = "kills", Limit = 1},
                    new CustomTitleConfig { Title = "[Mushroomer]", LootType = LootSettings.LootType.Gather, Prefab = "mushroom", Limit = 1}
                }
            };

            public VersionNumber Version;

            #endregion

            #region Classes

            public class CustomTitlesConfig
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool Enabled;

                [JsonProperty(PropertyName = "Max Titles")]
                public int MaxTitles;

                [JsonProperty(PropertyName = "Titles", ObjectCreationHandling = ObjectCreationHandling.Replace) ]
                public List<CustomTitleConfig> Titles = new();
            }

            public class CustomTitleConfig
            {
                [JsonProperty(PropertyName = "Title")]
                public string Title;

                [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
                public LootSettings.LootType LootType;

                [JsonProperty(PropertyName = "Prefab")]
                public string Prefab;
                
                [JsonProperty(PropertyName = "Limit")]
                public int Limit;
            }

            public class AutoMessagesConfig
            {
                [JsonProperty(PropertyName = "Chat")] public ChatMessagesConfig Chat;

                [JsonProperty(PropertyName = "Discord")]
                public DiscordMessagesConfig Discord;

                [JsonProperty(PropertyName = "Send Same Message to Both")]
                public bool SendSameMessageToBoth = true;
            }

            public class ChatMessagesConfig
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool Enabled;

                [JsonProperty(PropertyName = "Message Interval (seconds)")]
                public int MessageInterval;

                [JsonProperty(PropertyName = "Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<MessageConfig> Messages;
            }

            public class DiscordMessagesConfig
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool Enabled;

                [JsonProperty(PropertyName = "Message Interval (seconds)")]
                public int MessageInterval;

                [JsonProperty(PropertyName = "Webhook")]
                public string Webhook;

                [JsonProperty(PropertyName = "Messages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public List<MessageConfig> Messages = new();

                [JsonProperty(PropertyName = "Line Colors", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public int[] LineColors = {0xFFFFFF};
            }

            public class MessageConfig
            {
                [JsonProperty(PropertyName = "Header")]
                public string Header;

                [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
                public LootSettings.LootType LootType;

                [JsonProperty(PropertyName = "Prefab")]
                public string Prefab;

                [JsonProperty(PropertyName = "Limit")] public int Limit;
            }

            public enum StorageType
            {
                JSON,
                SQLite,
                MySQL
            }

            public class StorageInfo
            {
                #region Fields

                [JsonProperty(PropertyName = "Data Type")] [JsonConverter(typeof(StringEnumConverter))]
                public StorageType StorageType = StorageType.MySQL;

                [JsonProperty(PropertyName = "SQLite Settings")]
                public SQLiteSettings SQLite = new();

                [JsonProperty(PropertyName = "MySQL Settings")]
                public MySQLSettings MySQL = new();

                #endregion

                #region Classes

                public class SQLiteSettings
                {
                    [JsonProperty(PropertyName = "Database Name")]
                    public string DatabaseName = string.Empty;
                }

                public class MySQLSettings
                {
                    [JsonProperty(PropertyName = "Host")] public string Host = string.Empty;

                    [JsonProperty(PropertyName = "Port")] public int Port;

                    [JsonProperty(PropertyName = "Database Name")]
                    public string DatabaseName = string.Empty;

                    [JsonProperty(PropertyName = "Username")]
                    public string Username = string.Empty;

                    [JsonProperty(PropertyName = "Password")]
                    public string Password = string.Empty;

                    [JsonProperty(PropertyName = "Table Prefix")]
                    public string TablePrefix = string.Empty;
                }

                #endregion
            }

            public class LeaderboardColumn
            {
                [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
                public LootSettings.LootType Type;

                [JsonProperty(PropertyName = "Prefab")]
                public string Prefab;

                [JsonProperty(PropertyName = "Name")] public string Name;

                [JsonProperty(PropertyName = "Width")] public float Width;

                [JsonProperty(PropertyName = "Is Default?")]
                public bool IsDefault;
            }

            #endregion
        }

        [JsonConverter(typeof(StringEnumConverter))]
        public enum PatternServerMenu
        {
            V1,
            V2
        }

        public class CategoryConfig
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "Type")] public string Type = string.Empty;

            [JsonProperty(PropertyName = "Title")] public string Title = string.Empty;

            [JsonProperty(PropertyName = "Permission")]
            public string Permission = string.Empty;
        }

        public class TabConfig
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled = true;

            [JsonProperty(PropertyName = "Name")] public string Name;

            [JsonProperty(PropertyName = "Permission")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Blocks", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<BlockConfig> Blocks = new();
        }

        public class BlockConfig
        {
            [JsonProperty(PropertyName = "BlockType", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public string BlockType;

            [JsonProperty(PropertyName = "Columns", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<ColumnConfig> Columns = new();

            [JsonProperty(PropertyName = "StatFields", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<StatFieldConfig> StatFields = new();

            [JsonProperty(PropertyName = "HitRateImages", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<HitRateConfig> HitRateImages = new();

            [JsonProperty(PropertyName = "Grade Banners", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<GradeBannerConfig> GradeBanners = new();
        }

        public class ColumnConfig
        {
            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Fields", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<StatFieldConfig> StatFields = new();
        }

        public class StatFieldConfig
        {
            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public LootSettings.LootType Type;

            [JsonProperty(PropertyName = "Image URL")]
            public string Icon;

            [JsonProperty(PropertyName = "Title")] public string Title;

            [JsonProperty(PropertyName = "Prefab")]
            public string Prefab;

            [JsonProperty(PropertyName = "Second Prefab")]
            public string SecondPrefab;
        }

        public class GradeBannerConfig
        {
            [JsonProperty(PropertyName = "Grade")] public string Grade;

            [JsonProperty(PropertyName = "Banner image")]
            public string Image;
        }

        public class HitRateConfig
        {
            [JsonProperty(PropertyName = "Minimum hits count")]
            public int MinHits;

            [JsonProperty(PropertyName = "Maximum hits count")]
            public int MaxHits;

            [JsonProperty(PropertyName = "Image")] public string Image;
        }

        public class AwardsConfig
        {
            [JsonProperty(PropertyName = "Automatically give rewards after the wipe")]
            public bool OnWipeAward;

            [JsonProperty(PropertyName = "Display Awards in UI")]
            public bool DisplayAwardsUI = true;

            [JsonProperty(PropertyName = "Categories", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AwardCategory> Categories = new();
        }

        public class AwardCategory
        {
            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Title")] public string Title = string.Empty;

            [JsonProperty(PropertyName = "Permission")]
            public string Permission = string.Empty;

            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public LootSettings.LootType Type;

            [JsonProperty(PropertyName = "Prefab")]
            public string Prefab;

            [JsonProperty(PropertyName = "Places", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<AwardConfig> Places = new();
        }

        public class AwardConfig
        {
            #region Fields

            [JsonProperty(PropertyName = "Place")] public int Place;

            [JsonProperty(PropertyName = "Type")] [JsonConverter(typeof(StringEnumConverter))]
            public AwardType Type;

            [JsonProperty(PropertyName = "Title")] public string Title = string.Empty;

            [JsonProperty(PropertyName = "Image")] public string Image = string.Empty;

            [JsonProperty(PropertyName = "Command (%steamid%)")]
            public string Command = string.Empty;

            [JsonProperty(PropertyName = "Kit Name")]
            public string Kit = string.Empty;

            [JsonProperty(PropertyName = "Amount")]
            public int Amount = 1;

            #endregion

            #region Pubic Methods

            public enum AwardType
            {
                Command,
                ServerRewards,
                Economics,
                BankSystem,
                GameStores,
                MoscowOVH,
                Plugin,
                Kit
            }

            #endregion
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            try
            {
                _config = Config.ReadObject<Configuration>();
                if (_config == null) throw new Exception();
                
				if (_config.Version < Version)
					UpdateConfigValues();

                SaveConfig();
            }
            catch
            {
                PrintError("Your configuration file contains an error. Using default configuration values.");
                LoadDefaultConfig();
            }
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        protected override void LoadDefaultConfig()
        {
            _config = new Configuration();
        }

        
		private void UpdateConfigValues()
		{
			PrintWarning("Config update detected! Updating config values...");

			_config.Version = Version;
			PrintWarning("Config update completed!");
		}

        #endregion

        #region Hooks

        private void Init()
        {
            Instance = this;

            _config?.Loot?.LoadLootScoreCache();

            LoadUISettings();

            Reward.LoadDataAwards();

            Leaderboard = new MemoryLeaderboard();
            Leaderboard.CreateOrUpdateDatabase();

            LoadDataStorage();
        }

        private void OnServerInitialized()
        {
            UnsubscribeUnusedHooks();

            LoadServerPanel();

            dataStorage?.CreateTables();

            LoadImages();

            LoadMessages();

            LoadCustomTitles();

            RegisterPermissions();

            RegisterCommands();

            LoadAutoMessages();

            LoadLangAPI();

            timer.In(1, LoadActivePlayers);
        }

        private void OnPlayerConnected(BasePlayer player)
        {
            if (player == null || !player.userID.IsSteamId()) return;

            PlayerStats.LoadPlayerData(player.userID, stats =>
            {
                stats.LastIP = player.net.connection.ipaddress;
                stats.LastName = player.displayName;
            });

            Reward.PlayerConnectedCheck(player);
        }

        private void OnPlayerDisconnected(BasePlayer player)
        {
            if (!PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.DisconnectTime = DateTime.UtcNow;
            stats.LastIP = player.net.connection.ipaddress;
            stats.LastName = player.displayName;
            stats.TotalPlayTime += (ulong) DateTime.UtcNow.Subtract(stats.ConnectTime).TotalSeconds;

            dataStorage.SavePlayerStats(stats);
            PlayerStats.Remove(player.userID);
        }

        private void OnNewSave()
        {
            if (_config.Awards.OnWipeAward)
                Reward.StoreTopPlayersAwards();

            if (_config.WipeDataOnNewSave)
            {
                Puts("Wiping leaderboard data on new save...");
                Leaderboard?.CreateOrUpdateDatabase();
                dataStorage?.DoWipeData();
            }
        }

        private void Unload()
        {
            if (playerStats != null)
            {
                var playerIDs = playerStats.Keys.ToArray();
                for (var i = playerIDs.Length - 1; i >= 0; i--)
                    dataStorage?.SavePlayerStats(playerStats[playerIDs[i]]);
            }

            if (cacheCoroutine != null)
                Global.Runner.StopCoroutine(cacheCoroutine);

            Instance = null;
            _config = null;
            _uiSettings = null;
            Reward._rewardPlayerData = null;
        }

        private void OnServerShutdown()
        {
            Unload();
        }

        #region Stats

        #region Death

        private void CanBeWounded(BasePlayer player, HitInfo info)
        {
            if (!PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.Death, "wounded", 1);
        }

        #endregion Death

        #region Craft

        private void OnItemCraftFinished(ItemCraftTask task, Item item, ItemCrafter crafter)
        {
            if (task == null || crafter == null || item == null || crafter.owner == null ||
                !PlayerStats.TryGet(crafter.owner.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.Craft, item.info.shortname, item.amount);
        }

        #endregion Craft

        #region Construction

        private void OnEntityBuilt(Planner plan, GameObject go)
        {
            var player = plan.GetOwnerPlayer();
            if (player == null || !PlayerStats.TryGet(player.userID, out var stats)) return;

            var deployable = go.ToBaseEntity();
            if (deployable == null || deployable is not BuildingBlock buildingBlock) return;

            stats.AddStats(LootSettings.LootType.Construction, buildingBlock.ShortPrefabName, 1);
        }

        #endregion Construction

        #region Kill | Distance | Raid

        private readonly Dictionary<ulong, BasePlayer> _lastHeli = new();

        private void OnEntityDeath(BaseCombatEntity entity, HitInfo info)
        {
            if (info == null || entity == null || entity is BasePlayer) return;

            var attacker = info.InitiatorPlayer;
            if (attacker == null || !PlayerStats.TryGet(attacker.userID, out var stats)) return;

            switch (entity)
            {
                case BaseHelicopter when !_lastHeli.TryGetValue(entity.net.ID.Value, out attacker) || attacker == null:
                    return;
                case BuildingBlock buildingBlock:
                {
                    stats.AddStats(LootSettings.LootType.Raid, buildingBlock.ShortPrefabName, 1);
                    return;
                }
            }

            stats.AddStats(LootSettings.LootType.Kill, entity.ShortPrefabName, 1);
        }

        private void OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
        {
            if (entity == null) return;

            if (entity is BaseHelicopter helicopter && helicopter.net != null && info.InitiatorPlayer != null)
                _lastHeli[helicopter.net.ID.Value] = info.InitiatorPlayer;

            if (entity is BasePlayer victim)
            {
                var attacker = info.InitiatorPlayer;
                if (attacker != null && PlayerStats.TryGet(attacker.userID, out var attackerStats) &&
                    (!victim.IsNpc || _config.CountNPCKillsAsPlayerKills))
                {
                    var bodyPart = GetBodypartName(victim, info);
                    if (!string.IsNullOrEmpty(bodyPart))
                        attackerStats.AddStats(LootSettings.LootType.BodyHits, bodyPart, 1);
                }
            }
        }

        #endregion Kill | Distance | Raid

        #region Death | Kill | Distance

        private void OnPlayerDeath(BasePlayer victim, HitInfo info)
        {
            if (victim == null) return;

            var killer = info?.InitiatorPlayer;
            if (killer == victim)
                return;

            if (PlayerStats.TryGet(victim.userID, out var stats) && !victim.IsNpc)
            {
                if (killer == null)
                {
                    stats.AddStats(LootSettings.LootType.Death, victim.lastDamage.ToString(), 1);
                    return;
                }

                stats.AddStats(LootSettings.LootType.Death, "deaths", 1);
            }

            if (killer != null && PlayerStats.TryGet(killer.userID, out var attackerStats))
            {
                if (!victim.IsNpc || _config.CountNPCKillsAsPlayerKills)
                {
                    if (victim.IsSleeping())
                        attackerStats.AddStats(LootSettings.LootType.Kill, "kill_sleepers", 1);

                    attackerStats.AddStats(LootSettings.LootType.Kill, "kills", 1);

                    var weaponName = GetWeaponName(killer);
                    if (!string.IsNullOrEmpty(weaponName))
                        attackerStats.AddStats(LootSettings.LootType.WeaponUsed, weaponName, 1);

                    #region Distance

                    attackerStats.StatsStorage.TryGetItem(LootSettings.LootType.Kill, "max_distance",
                        out var oldDistance);
                    var distance = Mathf.Max(info.ProjectileDistance, oldDistance);
                    attackerStats.SetStats(LootSettings.LootType.Kill, "max_distance",
                        (float) Math.Round(distance, 2));

                    #endregion
                }
                
                if (victim.IsNpc)
                {
                    attackerStats.AddStats(LootSettings.LootType.Kill, victim.ShortPrefabName, 1);
                }
            }
        }

        #endregion Death | Kill | Distance

        #region Medical | Consume

        private void OnItemAction(Item item, string action, BasePlayer player)
        {
            if (item == null || player == null || !PlayerStats.TryGet(player.userID, out var stats)) return;

            if (action == "consume")
                stats.AddStats(
                    item.info.shortname == "largemedkit"
                        ? LootSettings.LootType.Medical
                        : LootSettings.LootType.Consume, item.info.shortname, 1);
        }

        #endregion Medical | Consume

        #region Medical

        private void OnHealingItemUse(MedicalTool tool, BasePlayer player)
        {
            if (tool == null || player == null || !PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.Medical, tool.ShortPrefabName, 1);
        }

        #endregion Medical

        #region Crate | LootItems

        private readonly HashSet<ulong> _lootedContainers = new();

        private void OnLootEntity(BasePlayer player, LootContainer lootContainer)
        {
            if (player == null || lootContainer == null || lootContainer.net == null ||
                !lootContainer.net.ID.IsValid || !PlayerStats.TryGet(player.userID, out var stats)) return;

            var netID = lootContainer.net.ID.Value;

            if (_lootedContainers.Contains(netID)) return;

            _lootedContainers.Add(netID);

            stats.AddStats(LootSettings.LootType.Crate, lootContainer.ShortPrefabName, 1);

            if (lootContainer.inventory?.itemList != null)
                foreach (var item in lootContainer.inventory.itemList)
                    stats.AddStats(LootSettings.LootType.LootItems, item.info.shortname, item.amount);
        }

        private void OnContainerDropItems(ItemContainer container)
        {
            if (container == null || container.entityOwner == null || !container.entityOwner.ShortPrefabName.Contains("barrel") || container.entityOwner is not LootContainer lootContainer) return;

            var player = lootContainer.lastAttacker as BasePlayer;
            if (player == null || player.IsNpc || !PlayerStats.TryGet(player.userID, out var stats)) return;

            foreach (var item in container.itemList) stats.AddStats(LootSettings.LootType.LootItems, item.info.shortname, item.amount);
        }

        private void OnDeathDropPickup(BasePlayer player)
        {
            if (player == null || !PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.LootItems, "death_drop", 1);
        }

        #endregion Crate | LootItems

        #region Fishing

        private void OnFishCatch(Item fish, BasePlayer player)
        {
            if (fish == null || fish.info == null || player == null ||
                !PlayerStats.TryGet(player.userID, out var stats))
                return;

            stats.AddStats(LootSettings.LootType.Fishing, fish.info.shortname, 1);
        }

        #endregion Fishing

        #region Puzzle

        private void OnCardSwipe(CardReader cardReader, Keycard card, BasePlayer player)
        {
            if (card == null || cardReader == null || player == null || !PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.Puzzle, card.ShortPrefabName, 1);
        }

        #endregion Puzzle

        #region Upgrade

        private void OnStructureUpgrade(BuildingBlock block, BasePlayer player, BuildingGrade.Enum grade, ulong skin)
        {
            if (block == null || player == null || grade == BuildingGrade.Enum.None || !PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.Upgrade, $"{block.ShortPrefabName} {grade.ToString().ToLower()}", 1);
        }

        #endregion Upgrade

        #region Gather

        private void OnCollectiblePickedup(CollectibleEntity collectible, BasePlayer player, Item item)
        {
            if (player == null || item == null) return;

            API_OnItemGather(player.userID, item.info.shortname, item.amount);
        }

        private void OnGrowableGathered(GrowableEntity plant, Item item, BasePlayer player)
        {
            if (item == null || player == null) return;

            API_OnItemGather(player.userID, item.info.shortname, item.amount);
        }

        private void OnDispenserBonusReceived(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item == null || player == null) return;
            
            API_OnItemGather(player.userID, item.info.shortname, item.amount);
        }

        private void OnDispenserGathered(ResourceDispenser dispenser, BasePlayer player, Item item)
        {
            if (item == null || player == null) return;

            API_OnItemGather(player.userID, item.info.shortname, item.amount);
        }

        #endregion

        #region CustomEvents

        private void OnConvoyEventWin(ulong userID)
        {
            API_OnEventWin(userID, "Convoy");
        }

        private void OnSputnikEventWin(ulong userID)
        {
            API_OnEventWin(userID, "Sputnik");
        }

        private void OnCaravanEventWin(ulong userID)
        {
            API_OnEventWin(userID, "Caravan");
        }

        private void OnGasStationEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "GasStationEvent");
        }

        private void OnAirEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "AirEvent");
        }

        private void OnSatDishEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "SatDishEvent");
        }

        private void OnTriangulationWinner(ulong userID)
        {
            API_OnEventWin(userID, "Triangulation");
        }

        private void OnWaterEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "WaterEvent");
        }

        private void OnHarborEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "HarborEvent");
        }

        private void OnFerryTerminalEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "FerryTerminalEvent");
        }

        private void OnArcticBaseEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "ArcticBaseEvent");
        }

        private void OnJunkyardEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "JunkyardEvent");
        }

        private void OnSupermarketEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "SupermarketEvent");
        }

        private void OnPowerPlantEventWinner(ulong userID)
        {
            API_OnEventWin(userID, "PowerPlantEvent");
        }

        private void OnArmoredTrainEventWin(ulong userID)
        {
            API_OnEventWin(userID, "ArmoredTrainEvent");
        }

        private void OnSurvivalArenaWin(BasePlayer player)
        {
            if (player == null) return;

            API_OnEventWin(player.userID, "SurvivalArena");
        }

        private void OnBossKilled(ScientistNPC boss, BasePlayer attacker)
        {
            if (boss == null || attacker == null) return;

            API_OnEventWin(attacker.userID, "KillBoss");
        }

        #region Raidable Bases

        private enum RaidableMode
        {
            Disabled = -1,
            Easy = 0,
            Medium = 1,
            Hard = 2,
            Expert = 3,
            Nightmare = 4,
            Points = 8888,
            Random = 9999
        }

        private void OnRaidableBaseCompleted(Vector3 raidPos, int mode, bool allowPVP, string id, float spawnTime, float despawnTime, float loadTime, ulong ownerId, BasePlayer owner, List<BasePlayer> raiders, List<BasePlayer> intruders, List<BaseEntity> entities)
        {
            if (owner == null || !PlayerStats.TryGet(owner.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.RaidableBases, ((RaidableMode)mode).ToString().ToLower(), 1);
        }

        #endregion Raidable Bases

        #endregion

        #region ShotFired

        private void OnWeaponFired(BaseProjectile projectile, BasePlayer player)
        {
            if (projectile == null || player == null || projectile.primaryMagazine == null ||
                projectile.primaryMagazine.ammoType == null ||
                !PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.ShotFired, projectile.primaryMagazine.ammoType.shortname, 1);
        }

        private void OnMlrsFired(MLRS entity, BasePlayer player)
        {
            if (entity == null || player == null || !PlayerStats.TryGet(player.userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.ShotFired, "ammo.rocket.mlrs", entity.RocketAmmoCount);
        }

        #endregion ShotFired

        #region ExplosiveUsed

        private void OnTimedExplosiveExplode(TimedExplosive explosive, Vector3 explosionFxPos)
        {
            if (explosive == null || explosive.creatorEntity is not BasePlayer player ||
                !PlayerStats.TryGet(player.userID, out var stats)) return;

            var entity = explosive.LookupPrefab();
            if (entity == null) return;

            stats.AddStats(LootSettings.LootType.ExplosiveUsed, GetShortnameFromPrefab(entity.ShortPrefabName), 1);
        }

        #endregion ExplosiveUsed

        #region Recycle Item

        private readonly Dictionary<ulong, ulong> _recyclerToPlayer = new();

        private void OnRecyclerToggle(Recycler recycler, BasePlayer player)
        {
            if (recycler == null || recycler.net == null || !recycler.net.ID.IsValid) return;

            NextTick(() =>
            {
                if (recycler.IsOn())
                    _recyclerToPlayer[recycler.net.ID.Value] = player.userID;
                else
                    _recyclerToPlayer.Remove(recycler.net.ID.Value);
            });
        }

        private void OnItemRecycle(Item item, Recycler recycler)
        {
            if (!_recyclerToPlayer.TryGetValue(recycler.net.ID.Value, out var playerID) ||
                !PlayerStats.TryGet(playerID, out var stats)) return;

            var itemAmount = 1;
            if (item.amount > 1)
                itemAmount = Mathf.CeilToInt(Mathf.Min(item.amount, item.info.stackable * 0.1f));

            stats.AddStats(LootSettings.LootType.RecycleItem, item.info.shortname, itemAmount);
        }

        #endregion

        #region Economy

        private void OnEconomicsDeposit(string playerId, double amount)
        {
            if (!playerId.IsSteamId() || !PlayerStats.TryGet(Convert.ToUInt64(playerId), out var stats)) return;

            stats.AddStats(LootSettings.LootType.Economy, "Economics", (float)amount);
        }

        private void OnAddedBalance(ulong userID, int amount, BasePlayer player, int balancePlayer)
        {
            if (!userID.IsSteamId() || !PlayerStats.TryGet(userID, out var stats)) return;

            stats.AddStats(LootSettings.LootType.Economy, "IQEconomic", amount);
        }

        #endregion Economy

        #endregion Stats

        #region Server Panel

        private void OnServerPanelCategoryPage(BasePlayer player, int category, int page)
        {
            RemoveOpenedLeaderboard(player);
        }

        private void OnServerPanelClosed(BasePlayer player)
        {
            RemoveOpenedLeaderboard(player);
        }

        private void OnReceiveCategoryInfo(int categoryID)
        {
            _serverPanelCategory.categoryID = categoryID;

            UpdateTemplateRenderer();
        }

        #endregion

        #region Image Library

        private void OnPluginLoaded(Plugin plugin)
        {
            switch (plugin.Name)
            {
                case nameof(ImageLibrary):
                    timer.In(1, LoadImages);
                    break;
                case nameof(ServerPanel):
                    timer.In(1, LoadServerPanel);
                    break;
                case nameof(BetterChat):
                    timer.In(1, LoadCustomTitles);
                    break;
            }
        }

        private void OnPluginUnloaded(Plugin plugin)
        {
            switch (plugin.Name)
            {
                case nameof(ImageLibrary):
                    _enabledImageLibrary = false;
                    break;
                case nameof(ServerPanel):
                    _serverPanelCategory.spStatus = false;
                    UpdateTemplateRenderer();
                    break;
                case nameof(LangAPI):
                    _isLangAPIReady = false;
                    break;
            }
        }

        private void OnLangAPIFinished()
        {
            _isLangAPIReady = true;
        }

        #endregion

        #endregion

        #region Commands

        private void CmdOpenLeaderboard(IPlayer cov, string command, string[] args)
        {
            if (cov?.Object is not BasePlayer player) return;

            if (_enabledImageLibrary == false)
            {
                SendNotify(player, NoILError, 1);

                BroadcastILNotInstalled();
                return;
            }

            if (args.Length > 0 && permission.UserHasPermission(player.UserIDString, PERM_Profile))
            {
                void OpenLeaderboardForPlayer(BasePlayer viewer, PlayerStats targetStats)
                {
                    if (!_openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                         _openedLeaderboards[player.userID] = openedLeaderboard = new OpenedLeaderboard(player);

                    if (_serverPanelCategory.spStatus && _serverPanelCategory.categoryID != -1)
                    {
                        ServerPanel?.Call("API_OnServerPanelOpenCategoryByID", player, _serverPanelCategory.categoryID);
                    }
                    else
                    {
                        UpdateUI(player, container => { templateRenderer.Render(player, container); });
                    }

                    openedLeaderboard.OnPageSelected(_config.Categories.FindLastIndex(x => x.Type == "search"));

                    UpdateUI(viewer, container =>
                    {
                        templateRenderer.CategoriesSection(player, container);
                        templateRenderer.UserProfilePage(player, targetStats, container);
                    });
                }

                if (ulong.TryParse(args[0], out var targetID))
                {
                    if (!PlayerStats.TryGet(targetID, out var stats))
                    {
                        if (loadingProfiles.Contains(targetID)) 
                        {
                            SendNotify(player, MsgProfileAlreadyLoading, 1);
                            return;
                        }

                        loadingProfiles.Add(targetID);
                        
                        dataStorage.LoadPlayerStats(targetID, (status, loadedStats) =>
                        {
                            loadingProfiles.Remove(targetID);
                            if (status == 1 && loadedStats != null)
                            {
                                playerStats[targetID] = loadedStats;

                                OpenLeaderboardForPlayer(player, loadedStats);
                            }
                            else
                            {
                                SendNotify(player, MsgPlayerNotFound, 1);
                            }
                        });
                        return;
                    }
                    OpenLeaderboardForPlayer(player, stats);
                    return;
                }
                
                SendNotify(player, MsgInvalidSteamID, 1);
                return;
            }
            
            if (_serverPanelCategory.spStatus && _serverPanelCategory.categoryID != -1)
            {
                ServerPanel?.Call("API_OnServerPanelOpenCategoryByID", player, _serverPanelCategory.categoryID);
            }
            else
            {
                if (!_openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    _openedLeaderboards[player.userID] = openedLeaderboard = new OpenedLeaderboard(player);

                UpdateUI(player, container => { templateRenderer.Render(player, container); });
            }
        }

        [ConsoleCommand("UI_UltimateLeaderboard")]
        private void CmdOpenLeaderboard(ConsoleSystem.Arg arg)
        {
            var player = arg.Player();
            if (player == null) return;

            if (!_openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;
            
            switch (arg.GetString(0))
            {
                case "close":
                {
                    RemoveOpenedLeaderboard(player);
                    break;
                }

                case "page":
                {
                    openedLeaderboard.OnPageSelected(arg.GetInt(1));

                    UpdateUI(player, container =>
                    {
                        templateRenderer.CategoriesSection(player, container);
                        templateRenderer.ContentSection(player, container);
                    });
                    break;
                }

                case "search":
                {
                    string BuildSearchInput(int startIndex)
                    {
                        var inputBuilder = Pool.Get<StringBuilder>();
                        try
                        {
                            for (int i = startIndex; i < arg.Args.Length; i++)
                            {
                                if (i > startIndex) inputBuilder.Append(' ');
                                inputBuilder.Append(arg.Args[i]);
                            }
                            return inputBuilder.ToString();
                        }
                        finally
                        {
                            Pool.FreeUnmanaged(ref inputBuilder);
                        }
                    }

                    var input = BuildSearchInput(1);

                    openedLeaderboard.OnInputSearch(input);

                    UpdateUI(player, container =>
                    {
                        templateRenderer.SearchInputField(player, container, true);
                        templateRenderer.SearchPlayersSection(player, container);
                    });
                    break;
                }

                case "search_open_profile":
                {
                    if (profileRequestCooldown.TryGetValue(player.userID, out var lastRequest))
                    {
                        if (UnityEngine.Time.time - lastRequest < ProfileCooldownDuration)
                        {
                            SendNotify(player, MsgProfileWaitBeforeAnother, 1);
                            break;
                        }
                    }
                    profileRequestCooldown[player.userID] = UnityEngine.Time.time;

                    var targetID = arg.GetUInt64(1);
                    var action = arg.GetString(2);
                    var tabIndex = arg.GetInt(3);

                    if (!PlayerStats.TryGet(targetID, out var stats))
                    {
                        if (loadingProfiles.Contains(targetID)) 
                        {
                            SendNotify(player, MsgProfileAlreadyLoading, 1);
                            break;
                        }

                        loadingProfiles.Add(targetID);
                        
                        dataStorage.LoadPlayerStats(targetID, (status, loadedStats) =>
                        {
                            loadingProfiles.Remove(targetID);
                            if (status == 1 && loadedStats != null)
                            {
                                playerStats[targetID] = loadedStats;
                                
                                HandleOpenProfile(action, tabIndex, loadedStats);
                            }
                        });
                    }
                    else
                    {
                        HandleOpenProfile(action, tabIndex, stats);
                    }
                    break;
                }

                case "open_profile":
                {
                    var targetID = arg.GetUInt64(1);
                    var action = arg.GetString(2);
                    var tabIndex = arg.GetInt(3);

                    var stats = PlayerStats.Get(targetID);
                    if (stats == null)
                    {
                        return;
                    }
                    
                    HandleOpenProfile(action, tabIndex, stats);
                    break;
                }

                case "leaderboard":
                {
                    switch (arg.GetString(1))
                    {
                        case "award_tab":
                        {
                            openedLeaderboard.OnAwardTabSelected(arg.GetInt(2));

                            UpdateUI(player,
                                container => { templateRenderer.LeaderboardAwardsSection(player, container); });
                            break;
                        }

                        case "select_column":
                        {
                            openedLeaderboard.OnColumnSelected(arg.GetInt(2));

                            UpdateUI(player, container => { templateRenderer.LeaderboardPage(player, container); });
                            break;
                        }

                        case "page":
                        {
                            switch (arg.GetString(2))
                            {
                                case "back":
                                {
                                    if (openedLeaderboard.LeaderboardPage > 0)
                                        openedLeaderboard.LeaderboardPage--;
                                    break;
                                }

                                case "next":
                                {
                                    if (openedLeaderboard.LeaderboardPage < openedLeaderboard.GetMaxLeaderboardPage())
                                        openedLeaderboard.LeaderboardPage++;
                                    break;
                                }

                                case "start":
                                {
                                    openedLeaderboard.LeaderboardPage = 0;
                                    break;
                                }
                                case "end":
                                {
                                    openedLeaderboard.LeaderboardPage = openedLeaderboard.GetMaxLeaderboardPage() - 1;
                                    break;
                                }
                            }

                            UpdateUI(player, container =>
                            { 
                                    templateRenderer.ShowLeaderboardTableUI(player, container);
                                    templateRenderer.ShowLeaderboardPaginationUI(player, container);
                            });
                            break;
                        }
                    }

                    break;
                }
            }

            
            void HandleOpenProfile(string action, int tabIndex, PlayerStats targetStats)
            {
                if (action == "tab")
                {
                    openedLeaderboard.OnTabSelected(tabIndex);
                }
                UpdateUI(player, container =>
                {
                    templateRenderer.UserProfilePage(player, targetStats, container);
                });
            }
        }

        [ConsoleCommand("leaderboard.hide")]
        private void CmdHidePlayer(ConsoleSystem.Arg arg)
        { 
            if (!arg.IsServerside) return;

            var steamID = arg.GetULong(0);
            if (!steamID.IsSteamId())
            {
                SendReply(arg, "Usage: leaderboard.hide <steamid>");
                return;
            }

            TogglePlayerVisibility(steamID, (wasHidden, stats) =>
            {
                SendReply(arg, $"Player {steamID} is now {(wasHidden ? "visible" : "hidden")} in leaderboard");

                if (wasHidden)
                {
                    if (stats != null)
                        Leaderboard?.AddUserToLeaderboard(stats);
                        Leaderboard?.CacheDatabase();
                }
                else
                {
                    Leaderboard?.RemovePlayer(steamID);
                }
            });

            void TogglePlayerVisibility(ulong playerId, Action<bool, PlayerStats> callback)
            {
                if (!PlayerStats.TryGet(playerId, out var stats))
                {
                    dataStorage.LoadPlayerStats(playerId, (status, loadedStats) =>
                    {
                        if (status == 1 && loadedStats != null)
                        {
                            PerformVisibilityToggle(loadedStats, callback);
                        }
                        else
                        {
                            callback?.Invoke(false, loadedStats);
                        }
                    });
                    return;
                }

                PerformVisibilityToggle(stats, callback);
            }

            void PerformVisibilityToggle(PlayerStats stats, Action<bool, PlayerStats> callback)
            {
                stats.HiddenFromLeaderboard = !stats.HiddenFromLeaderboard;

                dataStorage?.SavePlayerStats(stats);

                callback?.Invoke(!stats.HiddenFromLeaderboard, stats);
            }
        }

        [ConsoleCommand("leaderboard.wipe")]
        private void CmdConsoleWipeData(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            try
            {
                Leaderboard?.CreateOrUpdateDatabase();

                dataStorage?.DoWipeData();

                playerStats?.Clear();

                dataStorage?.StartCache();

                timer.In(1, LoadActivePlayers);
            }
            finally
            {
                SendReply(arg, "Data wiped successfully");
            }
        }

        #endregion

        #region Interface

        private CuiElementContainer API_OpenPlugin(BasePlayer player)
        {
            if (!_openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                _openedLeaderboards[player.userID] = openedLeaderboard = new OpenedLeaderboard(player);

            var container = new CuiElementContainer();

            #region Background

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, "UI.Server.Panel.Content", "UI.Server.Panel.Content.Plugin", "UI.Server.Panel.Content.Plugin");

            container.Add(new CuiPanel
            {
                RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"},
                Image = {Color = "0 0 0 0"}
            }, "UI.Server.Panel.Content.Plugin", Layer + ".Background", Layer + ".Background");

            #endregion Background

            templateRenderer?.Render(player, container);

            return container;
        }

        private static string GetKeyWithMaxValue(Dictionary<string, float> stats, BasePlayer player)
        {
            if (stats.Count == 0)
                return null;

            string maxKey = null;
            var maxValue = 0f;
            foreach (var kv in stats)
                if (kv.Value > maxValue)
                {
                    maxValue = kv.Value;
                    maxKey = kv.Key;
                }

            return maxKey;
        }

        private string GetPlaytimeLocalized(BasePlayer player, double seconds)
        {
            var time = TimeSpan.FromSeconds(seconds);
            var result = Pool.Get<List<string>>();
            try
            {
                if (time.Days != 0)
                    result.Add(Msg(player, "UI.Time.Days", time.Days));
                if (time.Hours != 0)
                    result.Add(Msg(player, "UI.Time.Hours", time.Hours));
                if (time.Minutes != 0)
                    result.Add(Msg(player, "UI.Time.Minutes", time.Minutes));
                if (time.Seconds != 0 || result.Count == 0)
                    result.Add(Msg(player, "UI.Time.Seconds", time.Seconds));

                return string.Join(" ", result.GetRange(0, Mathf.Min(result.Count, 2)));
            }
            finally
            {
                Pool.FreeUnmanaged(ref result);
            }
        }

        private static void UpdateUI(BasePlayer player, Action<CuiElementContainer> callback)
        {
            var container = Pool.Get<CuiElementContainer>();
            try
            {
                callback?.Invoke(container);

                CuiHelper.AddUi(player, container);
            }
            finally
            {
                container?.Clear();
                Pool.FreeUnsafe(ref container);
            }
        }

        #endregion

        #region Data

        public interface IPlayerDataStorage
        {
            void CreateTables();
            void SavePlayerStats(PlayerStats stats);
            void LoadPlayerStats(ulong userId, Action<int, PlayerStats> callback);
            void StartCache();
            void LoadPlayersToLeaderboard(int offset);

            void GetTopPlayers(LootSettings.LootType type, string prefab, int limit,
                Action<List<PlayerStats>> callback);

            void LoadGroupPlayerStats(ulong[] userIDs, Action<Dictionary<ulong, PlayerStats>> callback);

            void DoWipeData();
        }

        public class JsonPlayerDataStorage : IPlayerDataStorage
        {
            private List<string> files = new();

            public void CreateTables()
            {
                StartCache();
            }

            public void StartCache()
            {
                files = GetFiles().ToList();
                if (files.Count < 0)
                    return;

                Instance.PrintWarning("Start caching leaderboard");
                Instance.cacheCoroutine = Global.Runner.StartCoroutine(Instance.LoadPlayersCache(100));
            }

            public void LoadPlayersToLeaderboard(int offset)
            {
                var max = Math.Min(offset, files.Count);
                var min = offset > files.Count ? files.Count - 100 : offset - 100;
                min = min % 100 > 0 ? files.Count - min % 100 : min;

                for (var i = min; i < max; i++)
                {
                    var cachedStat = Interface.Oxide.DataFileSystem.ReadObject<PlayerStats>(BaseFolder() + files[i]);
                    if (cachedStat == null) continue;

                    Instance.Leaderboard.AddUserToLeaderboard(cachedStat);
                }

                if (max < files.Count)
                {
                    Instance.cacheCoroutine =
                        Global.Runner.StartCoroutine(Instance.LoadPlayersCache(offset + 100));
                }
                else
                {
                    files.Clear();

                    Instance.Leaderboard.CacheDatabase();
                }
            }

            public void SavePlayerStats(PlayerStats stats)
            {
                Interface.Oxide.DataFileSystem.WriteObject(BaseFolder() + stats.UserId, stats);
            }

            public void LoadPlayerStats(ulong userId, Action<int, PlayerStats> callback)
            {
                PlayerStats data = null;

                try
                {
                    data = ReadOnlyObject(BaseFolder() + userId);
                }
                catch (Exception e)
                {
                    Interface.Oxide.LogError(e.ToString());
                }

                if (data != null)
                    callback?.Invoke(1, data);
                else
                    callback?.Invoke(0, null);
            }

            private static string BaseFolder()
            {
                return nameof(UltimateLeaderboard) + Path.DirectorySeparatorChar + "Players" +
                       Path.DirectorySeparatorChar;
            }

            private static PlayerStats ReadOnlyObject(string userId)
            {
                return Interface.Oxide.DataFileSystem.ExistsDatafile(userId)
                    ? Interface.Oxide.DataFileSystem.GetFile(userId).ReadObject<PlayerStats>()
                    : null;
            }

            public static string[] GetFiles()
            {
                var baseFolder = BaseFolder();
                try
                {
                    var json = ".json".Length;
                    var paths = Interface.Oxide.DataFileSystem.GetFiles(baseFolder);
                    for (var i = 0; i < paths.Length; i++)
                    {
                        var path = paths[i];
                        var separatorIndex = path.LastIndexOf(Path.DirectorySeparatorChar);

                        // We have to do this since GetFiles returns paths instead of filenames
                        // And other methods require filenames
                        paths[i] = path.Substring(separatorIndex + 1, path.Length - separatorIndex - 1 - json);
                    }

                    return paths;
                }
                catch
                {
                    return Array.Empty<string>();
                }
            }

            public void GetTopPlayers(LootSettings.LootType type, string prefab, int limit,
                Action<List<PlayerStats>> callback)
            {
                var allStats = Pool.Get<List<PlayerStats>>();
                try
                {
                    var files = GetFiles();
                    foreach (var file in files)
                    {
                        var stats = Interface.Oxide.DataFileSystem.ReadObject<PlayerStats>(BaseFolder() + file);
                        if (stats != null && stats.StatsStorage.TryGetItem(type, prefab, out var value) && value > 0)
                            allStats.Add(stats);
                    }

                    var filteredStats = allStats
                        .OrderByDescending(s => s.StatsStorage.LootStorage[type].Items[prefab])
                        .Take(limit)
                        .ToList();
                    callback?.Invoke(filteredStats);
                }
                finally
                {
                    Pool.FreeUnmanaged(ref allStats);
                }
            }

            public void LoadGroupPlayerStats(ulong[] userIDs, Action<Dictionary<ulong, PlayerStats>> callback)
            {
                if (userIDs == null || userIDs.Length == 0) return;

                var players = Pool.Get<Dictionary<ulong, PlayerStats>>();

                try
                {
                    var files = Array.FindAll(GetFiles(), f => userIDs.Contains(Convert.ToUInt64(f)));
                    foreach (var file in files)
                    {
                        var stats = Interface.Oxide.DataFileSystem.ReadObject<PlayerStats>(BaseFolder() + file);
                        if (stats != null)
                            players.TryAdd(Convert.ToUInt64(file), stats);
                    }
                }
                finally
                {
                    callback?.Invoke(players);
                }
            }

            public void DoWipeData()
            {
                var path = Path.Combine(Interface.Oxide.DataDirectory, BaseFolder());
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, true);
                    Directory.CreateDirectory(path);
                }
            }
        }

        public class SqlitePlayerDataStorage : IPlayerDataStorage
        {
            private bool needWipe;
            private SQLite Sqlite = Interface.GetMod().GetLibrary<SQLite>();
            private Connection Sqlite_conn;

            private bool dbLoaded = false;

            public void StartCache()
            {
                Instance.PrintWarning("Start caching leaderboard");
                Instance.cacheCoroutine = Global.Runner.StartCoroutine(Instance.LoadPlayersCache(0));
            }

            public void LoadPlayersToLeaderboard(int offset)
            {
                var sqlStats = Sql.Builder.Append(@"SELECT UserId, LastIP, LastName, ConnectTime, DisconnectTime, TotalPlayTime, Points
                          FROM PlayerStats
                          LIMIT @0, @1", offset, 100);

                Sqlite?.Query(sqlStats, Sqlite_conn, results =>
                {
                    if (results == null) return;

                    if (results.Count <= 0)
                    {
                        Instance.Leaderboard.CacheDatabase();
                        return;
                    }

                    var players = new Dictionary<ulong, PlayerStats>();

                    foreach (var row in results)
                    {
                        if (row["UserId"] is DBNull) continue;
                        var userID = Convert.ToUInt64(row["UserId"]);

                        var stats = new PlayerStats
                        {
                            UserId = userID,
                            LastIP = row.TryGetValue("LastIP", out var lastIP) ? lastIP.ToString() : null,
                            LastName = row.TryGetValue("LastName", out var lastName) ? lastName.ToString() : null,
                            ConnectTime = row.TryGetValue("ConnectTime", out var connectTime) ? DateTime.Parse((string)connectTime) : DateTime.UtcNow,
                            DisconnectTime = row.TryGetValue("DisconnectTime", out var disconnectTime) ? DateTime.Parse((string)disconnectTime) : default,
                            TotalPlayTime = row.TryGetValue("TotalPlayTime", out var playTime) ? Convert.ToDouble(playTime) : 0,
                            Points = row.TryGetValue("Points", out var points) ? row.GetFloat("Points") : 0
                        };

                        players.TryAdd(userID, stats);
                    }

                    var userIds = players.Keys.ToArray();

                    var sqlLoot = Sql.Builder.Append(@"SELECT UserId, LootType, ShortName, ItemValue 
                                                       FROM StatsStorage 
                                                       WHERE UserId IN (@0)", userIds);

                    Sqlite?.Query(sqlLoot, Sqlite_conn, lootResults =>
                    {
                        if (lootResults != null)
                        {
                            foreach (var lootRow in lootResults)
                            {
                                if (lootRow["UserId"] is DBNull) continue;
                                var userID = Convert.ToUInt64(lootRow["UserId"]);

                                if (players.TryGetValue(userID, out var stats))
                                {
                                    if (lootRow["LootType"] is not DBNull)
                                    {
                                        stats.AddStats(
                                            (LootSettings.LootType)lootRow.GetInt("LootType"),
                                            string.Intern(lootRow.GetString("ShortName")),
                                            lootRow.GetFloat("ItemValue"));
                                    }
                                }
                            }
                        }

                        foreach (var (_, stats) in players)
                        {
                            Instance.Leaderboard.AddUserToLeaderboard(stats);
                        }

                        Instance.cacheCoroutine =
                            Global.Runner.StartCoroutine(Instance.LoadPlayersCache(offset + 100));
                    });
                });
            }

            public void LoadDatabase()
            {
                var databaseDirectory =
                    Path.Combine(Interface.Oxide.DataFileSystem.Directory, nameof(UltimateLeaderboard));

                if (!Directory.Exists(databaseDirectory)) Directory.CreateDirectory(databaseDirectory);

                Sqlite_conn =
                    Sqlite.OpenDb(
                        nameof(UltimateLeaderboard) + Path.DirectorySeparatorChar + _config.Storage.SQLite.DatabaseName,
                        Instance);
                if (Sqlite_conn?.Con == null)
                {
                    Instance?.PrintError("Couldn't open the SQLite Database: " + Sqlite_conn?.Con?.State);
                    return;
                }

                Sqlite.Insert(Sql.Builder.Append("PRAGMA foreign_keys = ON"), Sqlite_conn);
            }

            public void CreateTables()
            {
                #region Migrations

                var checkColumnSql = Sql.Builder.Append(@"
                    SELECT COUNT(*) as count 
                    FROM pragma_table_info('PlayerStats') 
                    WHERE name='HiddenFromLeaderboard'");

                Sqlite?.Query(checkColumnSql, Sqlite_conn, results =>
                {
                    if (results != null && results.Count > 0)
                    {
                        var columnExists = results[0].GetInt("count") > 0;
                        if (!columnExists)
                        {
                            var alterTableSql = Sql.Builder.Append(
                                "ALTER TABLE PlayerStats ADD COLUMN HiddenFromLeaderboard INTEGER DEFAULT 0");
                            Sqlite?.Insert(alterTableSql, Sqlite_conn);
                        }
                    }
                });

                #endregion Migrations

                if (needWipe)
                    Sqlite.Insert(Sql.Builder.Append("DELETE FROM PlayerStats; DELETE FROM StatsStorage;"),
                        Sqlite_conn);

                Sqlite.Insert(
                    Sql.Builder.Append(
                        @"CREATE TABLE IF NOT EXISTS PlayerStats (UserId INTEGER PRIMARY KEY, LastIP TEXT, LastName TEXT, ConnectTime TEXT, DisconnectTime TEXT, TotalPlayTime TEXT, Points FLOAT, HiddenFromLeaderboard INTEGER DEFAULT 0); " +
                        @"CREATE TABLE IF NOT EXISTS StatsStorage (UserId INTEGER, LootType INTEGER, ShortName TEXT, ItemValue FLOAT, UNIQUE(UserId, LootType, ShortName), FOREIGN KEY (UserID) REFERENCES PlayerStats(UserId));"
                    ),
                    Sqlite_conn);

                dbLoaded = true;

                //START CACHING
                StartCache();
            }

            public void SavePlayerStats(PlayerStats stats)
            {
                var sqlBuilder = Sql.Builder.Append(@"INSERT OR REPLACE INTO PlayerStats (UserId, LastIP, LastName, ConnectTime, DisconnectTime, TotalPlayTime, Points, HiddenFromLeaderboard) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7 ); ", 
                    stats.UserId, stats.LastIP, stats.LastName, stats.ConnectTime.ToString(), stats.DisconnectTime.ToString(), stats.TotalPlayTime.ToString("N"), stats.Points, Convert.ToInt32(stats.HiddenFromLeaderboard));

                foreach (var (lootType, lootItems) in stats.StatsStorage.LootStorage)
                {
                    foreach (var (shortName, amount) in lootItems.Items)
                    {
                        sqlBuilder.Append($"INSERT OR REPLACE INTO StatsStorage (UserId, LootType, ShortName, ItemValue) VALUES ('{stats.UserId}', '{(int) lootType}', '{shortName}', '{amount}'); ");
                    }
                }

                Sqlite.Insert(sqlBuilder, Sqlite_conn);
            }

            public void LoadPlayerStats(ulong userId, Action<int, PlayerStats> callback)
            {
                var sqlStats = Sql.Builder.Append(
                    @"SELECT p.UserId, p.LastIP, p.LastName, p.ConnectTime, p.DisconnectTime, p.TotalPlayTime, p.Points, ss.LootType, ss.ShortName, ss.ItemValue " +
                    "FROM PlayerStats p " +
                    "LEFT JOIN StatsStorage ss on p.UserId = ss.UserId " +
                    $"WHERE p.UserId = {(long) userId}");

                Sqlite?.Query(sqlStats, Sqlite_conn, results =>
                {
                    if (results == null || results.Count == 0) //not found
                    {
                        callback(0, null);
                        return;
                    }

                    PlayerStats stats = null;
                    foreach (var row in results)
                    {
                        if (stats == null)
                        {
                            stats = new PlayerStats();
                            stats.UserId = row.TryGetValue("UserId", out var UserId)
                                ? Convert.ToUInt64(UserId)
                                : stats.UserId;

                            stats.LastIP = row.TryGetValue("LastIP", out var lastIP) ? lastIP.ToString() : stats.LastIP;
                            stats.LastName = row.TryGetValue("LastName", out var lastName)
                                ? lastName.ToString()
                                : stats.LastName;
                            stats.ConnectTime = DateTime.UtcNow;
                            stats.DisconnectTime = row.TryGetValue("DisconnectTime", out var disconnectTime)
                                ? DateTime.Parse((string) disconnectTime)
                                : stats.DisconnectTime;
                            stats.TotalPlayTime = row.TryGetValue("TotalPlayTime", out var playTime)
                                ? Convert.ToDouble(playTime)
                                : stats.TotalPlayTime;
                            stats.Points = row.TryGetValue("Points", out var points)
                                ? row.GetFloat("Points")
                                : stats.Points;
                            stats.HiddenFromLeaderboard = row.TryGetValue("HiddenFromLeaderboard", out var hidden) ? Convert.ToBoolean((int) hidden) : stats.HiddenFromLeaderboard;
                        }

                        if (row["LootType"] is not DBNull)
                            stats.AddStats((LootSettings.LootType) row.GetInt("LootType"), row.GetString("ShortName"),
                                row.GetFloat("ItemValue"));
                    }

                    callback(1, stats); // Статус 1 - данные успешно загружены
                });
            }

            public void GetTopPlayers(LootSettings.LootType type, string prefab, int limit,
                Action<List<PlayerStats>> callback)
            {
                var sql = Sql.Builder.Append(@"SELECT s.UserId, p.LastName, s.ItemValue " +
                                             "FROM StatsStorage s " +
                                             $"INNER JOIN PlayerStats p ON s.UserId = p.UserId WHERE s.LootType = {(int) type} AND s.ShortName = '{prefab}' ORDER BY s.ItemValue DESC LIMIT {limit}");

                Sqlite?.Query(sql, Sqlite_conn, results =>
                {
                    var topPlayers = new List<PlayerStats>();
                    foreach (var row in results)
                    {
                        var stats = new PlayerStats
                        {
                            UserId = Convert.ToUInt64(row["UserId"]),
                            LastName = row["LastName"].ToString()
                        };
                        stats.AddStats(type, prefab, Convert.ToSingle(row["ItemValue"]));
                        topPlayers.Add(stats);
                    }

                    callback?.Invoke(topPlayers);
                });
            }

            public void LoadGroupPlayerStats(ulong[] userIDs, Action<Dictionary<ulong, PlayerStats>> callback)
            {
                if (userIDs == null || userIDs.Length == 0) return;

                var sqlStats = Sql.Builder.Append(
                    @"SELECT p.UserId, p.LastIP, p.LastName, p.ConnectTime, p.DisconnectTime, p.TotalPlayTime, p.Points, ss.LootType, ss.ShortName, ss.ItemValue " +
                    "FROM PlayerStats p " +
                    "LEFT JOIN StatsStorage ss on p.UserId = ss.UserId " +
                    "WHERE p.UserId IN (" + string.Join(",", userIDs) + ");");

                Sqlite?.Query(sqlStats, Sqlite_conn, results =>
                {
                    var players = Pool.Get<Dictionary<ulong, PlayerStats>>();

                    try
                    {
                        if (results == null || results.Count == 0) return;

                        foreach (var row in results)
                        {
                            if (row["UserId"] is DBNull) continue;

                            var userID = Convert.ToUInt64(row["UserId"]);

                            if (!players.TryGetValue(userID, out var stats))
                            {
                                stats = new PlayerStats
                                {
                                    UserId = userID,
                                    LastIP = row.TryGetValue("LastIP", out var lastIP) ? lastIP.ToString() : null,
                                    LastName = row.TryGetValue("LastName", out var lastName)
                                        ? lastName.ToString()
                                        : null,
                                    ConnectTime = row.TryGetValue("ConnectTime", out var connectTime)
                                        ? DateTime.Parse((string) connectTime)
                                        : DateTime.UtcNow,
                                    DisconnectTime = row.TryGetValue("DisconnectTime", out var disconnectTime)
                                        ? DateTime.Parse((string) disconnectTime)
                                        : default,
                                    TotalPlayTime = row.TryGetValue("TotalPlayTime", out var playTime)
                                        ? Convert.ToDouble(playTime)
                                        : 0,
                                    Points = row.TryGetValue("Points", out var points) ? row.GetFloat("Points") : 0
                                };

                                players.TryAdd(userID, stats);
                            }

                            if (row["LootType"] is not DBNull)
                                stats.AddStats((LootSettings.LootType) row.GetInt("LootType"),
                                    string.Intern(row.GetString("ShortName")), row.GetFloat("ItemValue"));
                        }
                    }
                    finally
                    {
                        callback?.Invoke(players);
                    }
                });
            }

            public void DoWipeData()
            {
                needWipe = true;

                if (dbLoaded)
                {
                    Sqlite.Insert(Sql.Builder.Append($"DELETE FROM PlayerStats; DELETE FROM StatsStorage;"), Sqlite_conn);
                }
            }
        }

        public class MySqlPlayerDataStorage : IPlayerDataStorage
        {
            private string PlayerStatsTable => $"{_config.Storage.MySQL.TablePrefix}PlayerStats";
            private string StatsStorageTable => $"{_config.Storage.MySQL.TablePrefix}StatsStorage";

            private bool needWipe;
            private Core.MySql.Libraries.MySql Sql = Interface.GetMod().GetLibrary<Core.MySql.Libraries.MySql>();
            private Connection Sql_conn;

            private bool dbLoaded = false;

            public void LoadDatabase()
            {
                try
                {
                    Sql_conn = Sql.OpenDb(
                        _config.Storage.MySQL.Host,
                        _config.Storage.MySQL.Port,
                        _config.Storage.MySQL.DatabaseName,
                        _config.Storage.MySQL.Username,
                        _config.Storage.MySQL.Password,
                        Instance);
                }
                catch (Exception ex)
                {
                    Instance.Puts(ex.Message);
                }

                Sql.Insert(Core.Database.Sql.Builder.Append("SET NAMES utf8mb4"), Sql_conn);
            }

            public void LoadPlayersToLeaderboard(int offset)
            {
                var sqlStats = Core.Database.Sql.Builder
                    .Append(@$"SELECT UserId, LastIP, LastName, ConnectTime, DisconnectTime, TotalPlayTime, Points
                              FROM {PlayerStatsTable}
                              LIMIT @0, @1", offset, 100);

                Sql?.Query(sqlStats, Sql_conn, results =>
                {
                    if (results == null) return;

                    if (results.Count <= 0)
                    {
                        Instance.Leaderboard.CacheDatabase();
                        return;
                    }

                    var players = new Dictionary<ulong, PlayerStats>();

                    foreach (var row in results)
                    {
                        if (row["UserId"] is DBNull) continue;
                        var userID = Convert.ToUInt64(row["UserId"]);

                        var stats = new PlayerStats
                        {
                            UserId = userID,
                            LastIP = row.TryGetValue("LastIP", out var lastIP) ? lastIP.ToString() : null,
                            LastName = row.TryGetValue("LastName", out var lastName) ? lastName.ToString() : null,
                            ConnectTime = row.TryGetValue("ConnectTime", out var connectTime) ? DateTime.Parse((string)connectTime) : DateTime.UtcNow,
                            DisconnectTime = row.TryGetValue("DisconnectTime", out var disconnectTime) ? DateTime.Parse((string)disconnectTime) : default,
                            TotalPlayTime = row.TryGetValue("TotalPlayTime", out var playTime) ? Convert.ToDouble(playTime) : 0,
                            Points = row.TryGetValue("Points", out var points) ? row.GetFloat("Points") : 0
                        };

                        players.TryAdd(userID, stats);
                    }

                    var userIds = players.Keys.ToArray();

                    var sqlLoot = Core.Database.Sql.Builder.Append(@$"SELECT UserId, LootType, ShortName, ItemValue 
                                                                      FROM {StatsStorageTable} 
                                                                      WHERE UserId IN (@0)", userIds);

                    Sql?.Query(sqlLoot, Sql_conn, lootResults =>
                    {
                        if (lootResults != null)
                        {
                            foreach (var lootRow in lootResults)
                            {
                                if (lootRow["UserId"] is DBNull) continue;
                                var userID = Convert.ToUInt64(lootRow["UserId"]);

                                if (players.TryGetValue(userID, out var stats))
                                {
                                    if (lootRow["LootType"] is not DBNull)
                                    {
                                        stats.AddStats(
                                            (LootSettings.LootType)lootRow.GetInt("LootType"),
                                            string.Intern(lootRow.GetString("ShortName")),
                                            lootRow.GetFloat("ItemValue"));
                                    }
                                }
                            }
                        }

                        foreach (var (_, stats) in players)
                            Instance.Leaderboard.AddUserToLeaderboard(stats);

                        Instance.cacheCoroutine =
                            Global.Runner.StartCoroutine(Instance.LoadPlayersCache(offset + 100));
                    });
                });
            }

            public void SavePlayerStats(PlayerStats stats)
            {
                var sqlBuilder = Core.Database.Sql.Builder.Append(
                    $"INSERT INTO {PlayerStatsTable} (UserId, LastIP, LastName, ConnectTime, DisconnectTime, TotalPlayTime, Points, HiddenFromLeaderboard) VALUES ( @0, @1, @2, @3, @4, @5, @6, @7 ) ON DUPLICATE KEY UPDATE LastIP = @1, LastName = @2, ConnectTime = @3, DisconnectTime = @4, TotalPlayTime = @5, Points = @6, HiddenFromLeaderboard = @7; ",
                    stats.UserId,
                    stats.LastIP,
                    stats.LastName,
                    stats.ConnectTime.ToString(),
                    stats.DisconnectTime.ToString(),
                    stats.TotalPlayTime.ToString("N"),
                    stats.Points, 
                    Convert.ToInt32(stats.HiddenFromLeaderboard)
                );
                
                foreach (var (lootType, lootItems) in stats.StatsStorage.LootStorage)
                foreach (var (shortName, amount) in lootItems.Items)
                    sqlBuilder.Append(
                        $"INSERT INTO {StatsStorageTable} (UserId, LootType, ShortName, ItemValue) VALUES ('{stats.UserId}', '{(int) lootType}', '{shortName}', '{amount}') ON DUPLICATE KEY UPDATE ItemValue = {amount};");
                
                Sql.Insert(sqlBuilder, Sql_conn);
            }

            public void LoadPlayerStats(ulong userId, Action<int, PlayerStats> callback)
            {
                var sqlStats = Core.Database.Sql.Builder.Append(
                    @$"SELECT p.UserId, p.LastIP, p.LastName, p.ConnectTime, p.DisconnectTime, p.TotalPlayTime, p.Points, ss.LootType, ss.ShortName, ss.ItemValue 
                       FROM {PlayerStatsTable} p 
                       LEFT JOIN {StatsStorageTable} ss on p.UserId = ss.UserId 
                       WHERE p.UserId = {(long) userId}");
                Sql?.Query(sqlStats, Sql_conn, results =>
                {
                    if (results == null || results.Count == 0) //not found
                    {
                        callback(0, null);
                        return;
                    }

                    PlayerStats stats = null;
                    foreach (var row in results)
                    {
                        if (stats == null)
                        {
                            stats = new PlayerStats();
                            stats.UserId = row.TryGetValue("UserId", out var UserId)
                                ? Convert.ToUInt64(UserId)
                                : stats.UserId;

                            stats.LastIP = row.TryGetValue("LastIP", out var lastIP) ? lastIP.ToString() : stats.LastIP;
                            stats.LastName = row.TryGetValue("LastName", out var lastName)
                                ? lastName.ToString()
                                : stats.LastName;
                            stats.ConnectTime = DateTime.UtcNow;
                            stats.DisconnectTime = row.TryGetValue("DisconnectTime", out var disconnectTime)
                                ? DateTime.Parse((string) disconnectTime)
                                : stats.DisconnectTime;
                            stats.TotalPlayTime = row.TryGetValue("TotalPlayTime", out var playTime)
                                ? Convert.ToDouble(playTime)
                                : stats.TotalPlayTime;
                            stats.Points = row.TryGetValue("Points", out var points)
                                ? row.GetFloat("Points")
                                : stats.Points;
                            stats.HiddenFromLeaderboard = row.TryGetValue("HiddenFromLeaderboard", out var hidden) ? Convert.ToBoolean((int) hidden) : stats.HiddenFromLeaderboard;
                        }

                        if (row["LootType"] is not DBNull)
                            stats.AddStats((LootSettings.LootType) row.GetInt("LootType"), row.GetString("ShortName"), row.GetFloat("ItemValue"));
                    }

                    callback(1, stats); // Статус 1 - данные успешно загружены
                });
            }

            public void CreateTables()
            {
                #region Migrations

                var checkColumnSql = Core.Database.Sql.Builder.Append(@$"
                    SELECT COUNT(*) as count 
                    FROM information_schema.COLUMNS 
                    WHERE TABLE_NAME = '{PlayerStatsTable}' 
                    AND COLUMN_NAME = 'HiddenFromLeaderboard'
                    AND TABLE_SCHEMA = @0", _config.Storage.MySQL.DatabaseName);

                Sql?.Query(checkColumnSql, Sql_conn, results =>
                {
                    if (results != null && results.Count > 0)
                    {
                        var columnExists = results[0].GetInt("count") > 0;
                        if (!columnExists)
                        {
                            var alterTableSql = Core.Database.Sql.Builder.Append(
                                $"ALTER TABLE {PlayerStatsTable} ADD COLUMN HiddenFromLeaderboard INT DEFAULT 0");
                            Sql?.Insert(alterTableSql, Sql_conn);
                        }
                    }
                });

                #endregion Migrations

                if (needWipe)
                    Sql?.Insert(Core.Database.Sql.Builder.Append(@$"DELETE FROM {PlayerStatsTable}; DELETE FROM {StatsStorageTable};"), Sql_conn);

                Sql.Insert(
                    Core.Database.Sql.Builder.Append(
                        @$"CREATE TABLE IF NOT EXISTS {PlayerStatsTable} (UserId BIGINT PRIMARY KEY, LastIP varchar(255), LastName varchar(255), ConnectTime varchar(255), DisconnectTime varchar(255), TotalPlayTime varchar(255), Points FLOAT, HiddenFromLeaderboard INT DEFAULT 0); 
                           CREATE TABLE IF NOT EXISTS {StatsStorageTable} (UserId BIGINT, LootType INT, ShortName VARCHAR(255), ItemValue FLOAT, UNIQUE(UserId, LootType, ShortName), FOREIGN KEY (UserID) REFERENCES {PlayerStatsTable}(UserId));"
                    ),
                    Sql_conn);

                dbLoaded = true;

                //START CACHING
                StartCache();
            }

            public void StartCache()
            {
                Instance.PrintWarning("Start caching leaderboard");
                Instance.cacheCoroutine = Global.Runner.StartCoroutine(Instance.LoadPlayersCache(0));
            }

            public void GetTopPlayers(LootSettings.LootType type, string prefab, int limit,
                Action<List<PlayerStats>> callback)
            {
                var sql = Core.Database.Sql.Builder.Append(@$"SELECT s.UserId, p.LastName, s.ItemValue 
                                                              FROM {StatsStorageTable} s 
                                                              INNER JOIN {PlayerStatsTable} p ON s.UserId = p.UserId 
                                                              WHERE s.LootType = {(int) type} AND s.ShortName = '{prefab}' 
                                                              ORDER BY s.ItemValue DESC LIMIT {limit}");

                Sql?.Query(sql, Sql_conn, results =>
                {
                    var topPlayers = new List<PlayerStats>();
                    foreach (var row in results)
                    {
                        var stats = new PlayerStats
                        {
                            UserId = Convert.ToUInt64(row["UserId"]),
                            LastName = row["LastName"].ToString()
                        };
                        stats.AddStats(type, prefab, Convert.ToSingle(row["ItemValue"]));
                        topPlayers.Add(stats);
                    }

                    callback?.Invoke(topPlayers);
                });
            }

            public void LoadGroupPlayerStats(ulong[] userIDs, Action<Dictionary<ulong, PlayerStats>> callback)
            {
                if (userIDs == null || userIDs.Length == 0) return;

                var sqlStats = Core.Database.Sql.Builder.Append(
                    @$"SELECT p.UserId, p.LastIP, p.LastName, p.ConnectTime, p.DisconnectTime, p.TotalPlayTime, p.Points, ss.LootType, ss.ShortName, ss.ItemValue
                       FROM {PlayerStatsTable} p 
                       LEFT JOIN {StatsStorageTable} ss on p.UserId = ss.UserId 
                       WHERE p.UserId IN (" + string.Join(",", userIDs) + ");");

                Sql?.Query(sqlStats, Sql_conn, results =>
                {
                    var players = Pool.Get<Dictionary<ulong, PlayerStats>>();

                    try
                    {
                        if (results == null || results.Count == 0) return;

                        foreach (var row in results)
                        {
                            if (row["UserId"] is DBNull) continue;

                            var userID = Convert.ToUInt64(row["UserId"]);

                            if (!players.TryGetValue(userID, out var stats))
                            {
                                stats = new PlayerStats
                                {
                                    UserId = userID,
                                    LastIP = row.TryGetValue("LastIP", out var lastIP) ? lastIP.ToString() : null,
                                    LastName = row.TryGetValue("LastName", out var lastName)
                                        ? lastName.ToString()
                                        : null,
                                    ConnectTime = row.TryGetValue("ConnectTime", out var connectTime)
                                        ? DateTime.Parse((string) connectTime)
                                        : DateTime.UtcNow,
                                    DisconnectTime = row.TryGetValue("DisconnectTime", out var disconnectTime)
                                        ? DateTime.Parse((string) disconnectTime)
                                        : default,
                                    TotalPlayTime = row.TryGetValue("TotalPlayTime", out var playTime)
                                        ? Convert.ToDouble(playTime)
                                        : 0,
                                    Points = row.TryGetValue("Points", out var points) ? row.GetFloat("Points") : 0
                                };

                                players.TryAdd(userID, stats);
                            }

                            if (row["LootType"] is not DBNull)
                                stats.AddStats((LootSettings.LootType) row.GetInt("LootType"),
                                    string.Intern(row.GetString("ShortName")), row.GetFloat("ItemValue"));
                        }
                    }
                    finally
                    {
                        callback?.Invoke(players);
                    }
                });
            }

            public void DoWipeData()
            {
                needWipe = true;

                if (dbLoaded)
                {
                    Sql?.Insert(Core.Database.Sql.Builder.Append(@$"DELETE FROM {PlayerStatsTable}; DELETE FROM {StatsStorageTable};"), Sql_conn);
                }
            }
        }

        public class PlayerStats : IDisposable
        {
            #region Fields

            public ulong UserId;
            public string LastIP;
            public string LastName;
            public DateTime ConnectTime;
            public DateTime DisconnectTime;
            public double TotalPlayTime;
            public float Points;
            public bool HiddenFromLeaderboard;

            public StatsStorage StatsStorage = new();

            #endregion

            #region Constructors

            public PlayerStats()
            {
            }

            public PlayerStats(ulong userId)
            {
                UserId = userId;
                ConnectTime = DateTime.UtcNow;
                TotalPlayTime = 0.0;
            }

            public void Dispose()
            {
                // StatsStorage.Dispose();
            }

            #endregion

            #region Public Methods

            /************* FOR TESTING **************************/
            public static void ConsoleTestInfo(List<Dictionary<string, object>> rows, PlayerStats stats)
            {
                var i = 1;
                Instance.Puts("________________________________________________");
                foreach (var row in rows)
                {
                    Instance.Puts($"Request #{i} ROWS =>");
                    foreach (var r in row)
                        Instance.Puts($"Name : {r.Key}\t------>\tValue : {r.Value}");


                    i++;
                }

                Instance.Puts("________________________________________________");

                Instance.Puts("________________________________________________");
                foreach (var loot in stats.StatsStorage.LootStorage)
                foreach (var items in loot.Value.Items)
                    Instance.Puts($"ItemName {items.Key}\t--->\tItemValue {items.Value}\t-->\tLootType {loot.Key}");

                Instance.Puts("_________________________________________________");
                Instance.Puts($"{stats.LastName} | {stats.UserId}");
                Instance.Puts("________________________________________________");
            }
            /**********************************************************/


            public static PlayerStats Get(ulong userID)
            {
                return Instance?.playerStats?.TryGetValue(userID, out var stats) == true ? stats : null;
            }

            public static bool TryGet(ulong userID, out PlayerStats stats)
            {
                if (Instance?.playerStats?.TryGetValue(userID, out stats) == true)
                    return true;

                stats = null;
                return false;
            }

            public static bool Remove(ulong userID, bool needSave = false)
            {
                if (needSave && TryGet(userID, out var stats))
                    Instance?.dataStorage?.SavePlayerStats(stats);

                return Instance?.playerStats?.Remove(userID) == true;
            }

            public static void LoadPlayerData(ulong userID, Action<PlayerStats> onComplete = null)
            {
                Instance?.dataStorage?.LoadPlayerStats(userID, (status, stats) =>
                {
                    if (status == 0 || stats == null) stats = new PlayerStats(userID); // Create a new object if there is no data
                    
                    Instance.playerStats[userID] = stats;

                    onComplete?.Invoke(stats);

                    Instance?.dataStorage?.SavePlayerStats(stats);
                });
            }

            #endregion

            #region Stats

            public void AddStats(LootSettings.LootType type, string key, float value)
            {
                if (Instance._needToCollect.TryGetValue(type, out var collect) && !collect.Contains(key) && !collect.Contains("*")) return;

                StatsStorage?.Add(type, key, value);

                if (_config.Loot.Enabled)
                    if (_config.Loot.GetLootScore(type, key) is float scores)
                        Points += scores * value;
            }

            public void SetStats(LootSettings.LootType type, string key, float value)
            {
                if (Instance._needToCollect.TryGetValue(type, out var collect) && !collect.Contains(key) && !collect.Contains("*")) return;

                var hasOldValue = StatsStorage.TryGetItem(type, key, out var oldValue);

                StatsStorage?.Set(type, key, value);

                if (_config.Loot.Enabled)
                    if (_config.Loot.GetLootScore(type, key) is float scores)
                    {
                        if (hasOldValue)
                            Points += (value - oldValue) * scores;
                        else
                            Points += scores * value;
                    }
            }

            public float GetRawStatValue(LootSettings.LootType type, string key)
            {
                switch (type)
                {
                    case LootSettings.LootType.Custom:
                        {
                            switch (key)
                            {
                                case "total_play_time":
                                    return (float)(TotalPlayTime + GetRawStatValue(LootSettings.LootType.Custom, "current_playtime"));
                                case "current_playtime":
                                    return (float)(DateTime.UtcNow - ConnectTime).TotalSeconds;
                                case "kdr":
                                    var kills = StatsStorage.GetKills();
                                    if (kills == 0)
                                        return 0f;

                                    var deaths = Mathf.Max(StatsStorage.GetDeaths(), 1);

                                    return (float)Math.Round((double)kills / deaths, 2);
                                case "points":
                                    return Points;
                                case "longest_kill_distance":
                                    return StatsStorage.TryGetItem(LootSettings.LootType.Kill, "max_distance", out var maxDistance) ? maxDistance : 0f;
                                case "total_hits":
                                    return StatsStorage.GetTotal(LootSettings.LootType.BodyHits);
                                case "total_resources":
                                    return StatsStorage.GetTotal(LootSettings.LootType.Gather);
                                case "total_items_crafted":
                                    return StatsStorage.GetTotal(LootSettings.LootType.Craft);
                                case "events_won":
                                    return StatsStorage.GetTotal(LootSettings.LootType.Event);
                                case "structures_built":
                                    return StatsStorage.GetTotal(LootSettings.LootType.Construction);
                                case "upgrades_performed":
                                    return StatsStorage.GetTotal(LootSettings.LootType.Upgrade);
                                default:
                                    return 0f;
                            }
                        }
                    default:
                        {
                            var result = 0f;

                            foreach (var statKey in key.Split('|'))
                                if (StatsStorage.TryGetItem(type, statKey, out var val))
                                    result += val;

                            return result;
                        }
                }
            }

            public string GetFormattedStatValue(BasePlayer player, LootSettings.LootType type, string prefab)
            {
                switch (type)
                {
                    case LootSettings.LootType.Custom:
                        {
                            switch (prefab)
                            {
                                case "name":
                                case "nickname":
                                    return LastName;

                                case "formatеed_total_playtime":
                                    {
                                        var totalPlaytime = GetRawStatValue(LootSettings.LootType.Custom, "total_play_time");
                                        return Instance.GetPlaytimeLocalized(player, Convert.ToDouble(totalPlaytime));
                                    }

                                case "current_playtime":
                                    {
                                        var currentPlaytime = GetRawStatValue(type, prefab);
                                        return Msg(player, UIStatValueToday, Instance.GetPlaytimeLocalized(player, currentPlaytime));
                                    }

                                case "kd":
                                    return Msg(player, UIStatValueKD, StatsStorage.GetKills(), StatsStorage.GetDeaths());

                                case "favorite_resource":
                                    {
                                        var gathers = StatsStorage.GetAll(LootSettings.LootType.Gather);
                                        var resourceShortname = GetKeyWithMaxValue(gathers, player);
                                        if (resourceShortname != null)
                                        {
                                            var itemDef = ItemManager.FindItemDefinition(resourceShortname);
                                            if (itemDef != null)
                                            {
                                                if (Instance._isLangAPIReady)
                                                    return Convert.ToString(Instance.LangAPI?.Call("GetItemDisplayName", itemDef.shortname, itemDef.displayName.english, player.UserIDString));
                                                else
                                                    return itemDef.displayName.english;
                                            }

                                            return resourceShortname;
                                        }

                                        return Msg(player, UIStatValueNone);
                                    }

                                // Crafts
                                case "favorite_crafted_item":
                                    {
                                        var crafts = StatsStorage.GetAll(LootSettings.LootType.Craft);
                                        var craftName = GetKeyWithMaxValue(crafts, player);
                                        if (craftName != null)
                                        {
                                            var itemDef = ItemManager.FindItemDefinition(craftName);
                                            if (itemDef != null)
                                            {
                                                if (Instance._isLangAPIReady)
                                                    return Convert.ToString(Instance.LangAPI?.Call("GetItemDisplayName", itemDef.shortname, itemDef.displayName.english, player.UserIDString));
                                                else
                                                    return itemDef.displayName.english;
                                            }

                                            return craftName;
                                        }

                                        return Msg(player, UIStatValueNone);
                                    }

                                // Events
                                case "favorite_event":
                                    {
                                        var events = StatsStorage.GetAll(LootSettings.LootType.Event);
                                        var eventName = GetKeyWithMaxValue(events, player);
                                        if (eventName != null) return Msg(player, "UI.Field." + eventName);
                                        return Msg(player, UIStatValueNone);
                                    }

                                // Building
                                case "favorite_building_material":
                                    {
                                        var materials = StatsStorage.GetUpgrades();
                                        try
                                        {
                                            if (materials != null && materials.Count > 0)
                                            {
                                                var topGradeName = materials[0].shortname.Split(' ')[1];
                                                if (!string.IsNullOrEmpty(topGradeName))
                                                    return Msg(player, "UI.Grade." + topGradeName);
                                            }

                                            return Msg(player, UIStatValueNone);
                                        }
                                        finally
                                        {
                                            Pool.FreeUnmanaged(ref materials);
                                        }
                                    }

                                case "favorite_weapon":
                                    {
                                        var weapons = StatsStorage.GetAll(LootSettings.LootType.WeaponUsed);
                                        var weaponName = GetKeyWithMaxValue(weapons, player);
                                        if (weaponName != null)
                                        {
                                            var itemDef = ItemManager.FindItemDefinition(weaponName);
                                            if (itemDef != null)
                                            {
                                                if (Instance._isLangAPIReady)
                                                    return Convert.ToString(Instance.LangAPI?.Call("GetItemDisplayName", itemDef.shortname, itemDef.displayName.english, player.UserIDString));
                                                else
                                                    return itemDef.displayName.english;
                                            }

                                            return weaponName;
                                        }

                                        return Msg(player, UIStatValueNone);
                                    }

                                case "clan_tag":
                                {
                                    return Convert.ToString(Instance?.Clans?.Call("GetClanOf", player.UserIDString)) ?? string.Empty;
                                }

                                default:
                                    return GetRawStatValue(type, prefab).ToString();
                            }
                        }

                    default:
                        {
                            return GetRawStatValue(type, prefab).ToString();
                        }
                }
            }

            #endregion Stats
        }

        public class StatsStorage
        {
            #region Fields

            [JsonProperty(PropertyName = "Loot Storage", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public Dictionary<LootSettings.LootType, LootStorageItems> LootStorage = new();

            public class LootStorageItems
            {
                [JsonProperty(PropertyName = "Items", ObjectCreationHandling = ObjectCreationHandling.Replace)]
                public Dictionary<string, float> Items = new();
            }

            #endregion

            #region Public Methods

            public void Add(LootSettings.LootType type, string key, float value)
            {
                if (string.IsNullOrEmpty(key)) return;

                if (!LootStorage.TryGetValue(type, out var storage))
                    LootStorage.TryAdd(type, storage = new LootStorageItems());

                if (storage.Items.ContainsKey(key))
                    storage.Items[key] += value;
                else
                    storage.Items.TryAdd(key, value);
            }

            public void Set(LootSettings.LootType type, string key, float value)
            {
                if (string.IsNullOrEmpty(key)) return;
                if (!LootStorage.TryGetValue(type, out var storage))
                    LootStorage.TryAdd(type, storage = new LootStorageItems());

                storage.Items[key] = value;
            }

            public bool TryGetItem(LootSettings.LootType type, string key, out float value)
            {
                value = 0;
                return !string.IsNullOrEmpty(key) && LootStorage.TryGetValue(type, out var storage) &&
                       storage.Items.TryGetValue(key, out value);
            }

            #endregion

            #region Public Helpers

            public int GetKills()
            {
                return TryGetItem(LootSettings.LootType.Kill, "kills", out var value) ? (int) value : 0;
            }

            public int GetDeaths()
            {
                return TryGetItem(LootSettings.LootType.Death, "deaths", out var value) ? (int) value : 0;
            }

            public List<(string shortname, float amount)> GetUpgrades()
            {
                var list = Pool.Get<List<(string shortname, float amount)>>();
                if (LootStorage.TryGetValue(LootSettings.LootType.Upgrade, out var upgradeStorage))
                {
                    foreach (var (shortname, val) in upgradeStorage.Items) list.Add((shortname, val));

                    list.Sort((x, y) => y.amount.CompareTo(x.amount));
                }
                
                return list;
            }

            public float GetTotal(LootSettings.LootType type)
            {
                return LootStorage.TryGetValue(type, out var storage) ? storage.Items.Values.Sum() : 0f;
            }

            public Dictionary<string, float> GetAll(LootSettings.LootType type)
            {
                return LootStorage.TryGetValue(type, out var storage) ? storage.Items : new Dictionary<string, float>();
            }

            #endregion
        }

        public class LootSettings
        {
            #region Fields

            [JsonProperty(PropertyName = "Enabled")]
            public bool Enabled;

            [JsonProperty(PropertyName = "Loot", ObjectCreationHandling = ObjectCreationHandling.Replace)]
            public List<LootEntry> Loots = new();

            #endregion

            #region Public Methods

            [JsonIgnore] private Dictionary<(LootType, string), float> lootScoreCache = new();

            public void LoadLootScoreCache()
            {
                lootScoreCache.Clear();
                Loots?.ForEach(entry => 
                { 
                    if (entry.Enabled) lootScoreCache[(entry.LootType, entry.Prefab)] = entry.Score; 
                });
            }

            public float? GetLootScore(LootType type, string shortName)
            {
                return lootScoreCache.GetValueOrDefault((type, shortName), 0f);
            }

            #endregion Public Methods

            public class LootEntry
            {
                [JsonProperty(PropertyName = "Enabled")]
                public bool Enabled;

                [JsonProperty(PropertyName = "Short Name")]
                public string Prefab;

                [JsonProperty(PropertyName = "Loot Type")] [JsonConverter(typeof(StringEnumConverter))]
                public LootType LootType;

                [JsonProperty(PropertyName = "Score")] public float Score;

                public static LootEntry Create(string shortName, LootType type, float score = 1f)
                {
                    return new LootEntry
                    {
                        Enabled = true,
                        Prefab = shortName,
                        LootType = type,
                        Score = score
                    };
                }
            }

            public enum LootType
            {
                None = 0,
                Construction = 1,
                Medical = 2,
                Event = 3,
                Farm = 4,
                Gather = 5,
                Kill = 6,
                Consume = 7,
                Raid = 8,
                Death = 9,
                Craft = 10,
                Crate = 11,
                LootItems = 12,
                Fishing = 13,
                Puzzle = 14,
                Custom = 15,
                Upgrade = 16,
                ShotFired = 17,
                ExplosiveUsed = 18,
                RecycleItem = 19,
                BodyHits = 20,
                WeaponUsed = 21,
                RaidableBases = 22,
                Economy = 23,
            }
        }

        #endregion

        #region Opened Leaderboard

        private Dictionary<ulong, OpenedLeaderboard> _openedLeaderboards = new();

        private class OpenedLeaderboard
        {
            public int Page, SelectedTab, LeaderboardPage;

            public OpenedLeaderboard(BasePlayer player)
            {
                Page = SelectedTab = LeaderboardPage = 0;
                selectedColumnIndex = Instance.Leaderboard.defaultColumnIndex;
            }

            #region User Profile

            public void OnPageSelected(int page)
            {
                Page = page;
                SelectedTab = LeaderboardPage = 0;
            }

            public void OnTabSelected(int selectedTab)
            {
                SelectedTab = selectedTab;
            }

            #endregion User Profile

            #region Search

            public string search;

            public void OnInputSearch(string input)
            {
                search = input;
            }

            #endregion

            #region Leaderboard

            private int selectedColumnIndex;

            public int awardTabSelected;

            public void OnColumnSelected(int column)
            {
                selectedColumnIndex = column;
            }

            public void OnAwardTabSelected(int newIndex)
            {
                awardTabSelected = newIndex;
            }

            public int GetSelectedColumn()
            {
                return selectedColumnIndex;
            }

            public int GetMaxLeaderboardPage()
            {
                if (Instance.Leaderboard == null || Instance.Leaderboard.leaderboardData.Count == 0)
                    return 1;

                return Mathf.CeilToInt((float) Instance.Leaderboard.leaderboardData.Count / Instance.templateRenderer.GetLeaderboardPlayersToShow());
            }

            #endregion
        }

        #endregion Opened Leaderboard

        #region Leaderboard

        private class MemoryLeaderboard
        {
            private List<LeaderboardTop> workingLeaderboardData = new();

            public List<LeaderboardTop> leaderboardData = new();
            public Dictionary<int, int[]> preSortedColumns = new();

            public Dictionary<(LootSettings.LootType, string), int> messageColumns = new();
            public Dictionary<(LootSettings.LootType, string), int[]> sortedIndexes = new();

            private Timer _cacheUpdateTimer;

            public int defaultColumnIndex;

            public MemoryLeaderboard()
            {
                for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                    if (_config.LeaderboardColumns[colIndex].IsDefault)
                        defaultColumnIndex = colIndex;

                CacheDatabase(false);

                if (_config.AutoMessages?.Chat?.Messages != null)
                    foreach (var msg in _config.AutoMessages.Chat.Messages)
                        messageColumns.TryAdd((msg.LootType, msg.Prefab), msg.Limit);
                if (_config.AutoMessages?.Discord?.Messages != null)
                    foreach (var msg in _config.AutoMessages.Discord.Messages)
                        messageColumns.TryAdd((msg.LootType, msg.Prefab), msg.Limit);

                if (_config.CustomTitles.Enabled)
                {
                    foreach (var title in _config.CustomTitles.Titles)
                    {
                        if (messageColumns.TryGetValue((title.LootType, title.Prefab), out var limit))
                        {
                            messageColumns[(title.LootType, title.Prefab)] = Mathf.Max(limit, title.Limit);
                        }
                        else
                        {
                            messageColumns.TryAdd((title.LootType, title.Prefab), title.Limit);
                        }
                    }
                }
            }

            public void CreateOrUpdateDatabase()
            {
                workingLeaderboardData.Clear();
            }

            public void CacheDatabase(bool notifyOnComplete = true)
            {
                if (Instance.cacheCoroutine != null)
                    Global.Runner.StopCoroutine(Instance.cacheCoroutine);

                Instance.cacheCoroutine = Global.Runner.StartCoroutine(CacheRoutine(notifyOnComplete));
            }

            private IEnumerator CacheRoutine(bool notifyOnComplete = true)
            {
                #region Initialize Leaderboard

                leaderboardData.Clear();
                leaderboardData.AddRange(workingLeaderboardData);
                workingLeaderboardData.Clear();

                var count = leaderboardData.Count;
                
                var baseIndices = new int[count];
                for (var i = 0; i < count; i++)
                    baseIndices[i] = i;

                #endregion Initialize Leaderboard

                #region Sort Leaderboard

                var newActivePreSortedColumns = new Dictionary<int, int[]>();

                for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                {
                    var sortedIndices = baseIndices.ToArray();
                    Array.Sort(sortedIndices, (a, b) =>
                    {
                        return leaderboardData[b].leaderboardItems[colIndex].CompareTo(leaderboardData[a].leaderboardItems[colIndex]);
                    });
                    newActivePreSortedColumns[colIndex] = sortedIndices;
                }
                
                preSortedColumns = newActivePreSortedColumns;

                #endregion Sort Leaderboard

                yield return CoroutineEx.waitForFixedUpdate;

                #region Auto Messages

                if (_config?.AutoMessages?.Chat?.Enabled == true || _config?.AutoMessages?.Discord?.Enabled == true || _config?.CustomTitles?.Enabled == true)
                {
                    var newSortedIndexes = new Dictionary<(LootSettings.LootType, string), int[]>();
                    foreach (var (colPrefab, colLimit) in messageColumns)
                    {
                        var sortedIndices = baseIndices.ToArray();

                        Array.Sort(sortedIndices, (a, b) =>
                        {
                            if (!leaderboardData[a].messageSortValues.TryGetValue(colPrefab, out var valA))
                                valA = 0f;

                            if (!leaderboardData[b].messageSortValues.TryGetValue(colPrefab, out var valB))
                                valB = 0f;

                            return valB.CompareTo(valA);
                        });

                        var limit = Mathf.Min(sortedIndices.Length, colLimit);

                        var limitedIndices = new int[limit];
                        Array.Copy(sortedIndices, limitedIndices, limit);

                        newSortedIndexes[colPrefab] = limitedIndices;
                    }

                    sortedIndexes = newSortedIndexes;

                    UpdateCustomTitles();
                }

                #endregion Auto Messages

                yield return CoroutineEx.waitForFixedUpdate;

                #region Notify

                if (notifyOnComplete)
                    Instance.PrintWarning($"Leaderboard successfully cached: {this.leaderboardData.Count} players");

                #endregion Notify

                Instance.isLeaderboard = true;

                timerUpdateCache();
            }

            public void AddUserToLeaderboard(PlayerStats stats)
            {
                if (stats.HiddenFromLeaderboard) return;

                if (PlayerStats.TryGet(stats.UserId, out var liveStats)) stats = liveStats;

                var topPlayer = new LeaderboardTop
                {
                    UserId = stats.UserId,
                    LastName = stats.LastName,
                    leaderboardItems = new float[_config.LeaderboardColumns.Count]
                };

                for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                {
                    var col = _config.LeaderboardColumns[colIndex];

                    topPlayer.leaderboardItems[colIndex] = GetLeaderboardStatValue(stats, col.Type, col.Prefab);
                }

                if (messageColumns.Count > 0)
                {
                    topPlayer.messageSortValues = new Dictionary<(LootSettings.LootType, string), float>();

                    foreach (var col in messageColumns.Keys)
                        topPlayer.messageSortValues[col] = GetLeaderboardStatValue(stats, col.Item1, col.Item2);
                }

                workingLeaderboardData.Add(topPlayer);

                float GetLeaderboardStatValue(PlayerStats stats, LootSettings.LootType type, string prefab)
                {
                    return type == LootSettings.LootType.Custom && prefab == "formatеed_total_playtime" ?
                        stats.GetRawStatValue(LootSettings.LootType.Custom, "total_play_time") :
                        stats.GetRawStatValue(type, prefab);
                }
            }

            public void timerUpdateCache()
            {
                _cacheUpdateTimer?.Destroy();

                _cacheUpdateTimer = Instance.timer.In(_config.LeaderboardCacheInterval, () =>
                {
                    CreateOrUpdateDatabase();
                    Instance.dataStorage.StartCache();
                });
            }

            public List<LeaderboardTop> GetLeaderboardUsers(int offset, int amount, int? columnIndex = null)
            {
                if (!columnIndex.HasValue) columnIndex = defaultColumnIndex;

                var sortedIndices = preSortedColumns[columnIndex.Value];
                var end = Math.Min(offset + amount, sortedIndices.Length);
                var result = Pool.Get<List<LeaderboardTop>>();

                for (var i = offset; i < end; i++) result.Add(leaderboardData[sortedIndices[i]]);

                return result;
            }

            public List<LeaderboardTop> GetLeaderboardMessagesUsers(LootSettings.LootType type, string prefab,
                int offset, int amount)
            {
                if (!sortedIndexes.TryGetValue((type, prefab), out var sortedIndices))
                    return new List<LeaderboardTop>();

                var end = Math.Min(offset + amount, sortedIndices.Length);
                var result = Pool.Get<List<LeaderboardTop>>();
                for (var i = offset; i < end; i++)
                    result.Add(leaderboardData[sortedIndices[i]]);
                return result;
            }

            public void RemovePlayer(ulong steamID)
            {
                var working = workingLeaderboardData.RemoveAll(x => x.UserId == steamID);
                var main = leaderboardData.RemoveAll(x => x.UserId == steamID);

                if (working > 0 || main > 0)
                    CacheDatabase(false);
            }
        
            public Dictionary<ulong, PlayerTitlesInfo> customTitles = new();

            public struct PlayerTitlesInfo
            {
                public string[] Titles;
            }

            public void UpdateCustomTitles()
            {
                if (!_config.CustomTitles.Enabled) return;

                var dict = Pool.Get<Dictionary<ulong, List<string>>>();
                try
                {
                    foreach (var title in _config.CustomTitles.Titles)
                    {
                        var topPlayers = GetLeaderboardMessagesUsers(title.LootType, title.Prefab, 0, title.Limit);
                        try
                        {
                            foreach (var player in topPlayers)
                            {
                                if (!dict.TryGetValue(player.UserId, out var titles))
                                    dict[player.UserId] = new List<string>();

                                if (dict[player.UserId].Count >= _config.CustomTitles.MaxTitles) continue;

                                if (!dict[player.UserId].Contains(title.Title))
                                    dict[player.UserId].Add(title.Title);
                            }
                        }
                        finally
                        {
                            Pool.FreeUnmanaged(ref topPlayers);
                        }
                    }

                    foreach (var (playerID, titles) in dict)
                    {
                        customTitles[playerID] = new PlayerTitlesInfo
                        {
                            Titles = titles.ToArray()
                        };
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref dict);
                }
            }
        }

        public class LeaderboardTop
        {
            public ulong UserId;
            public string LastName;
            public float[] leaderboardItems;
            public Dictionary<(LootSettings.LootType, string), float> messageSortValues;
        }

        #endregion

        #region Reward

        public static class Reward
        {
            #region Data

            public static Dictionary<ulong, List<PlayerAward>> _rewardPlayerData = null;

            public class PlayerAward
            {
                public int CategoryIndex;
                public int Place;
            }

            private static string GetAwardsPath()
            {
                return nameof(UltimateLeaderboard) + Path.DirectorySeparatorChar + "Awards";
            }

            public static void LoadDataAwards()
            {
                _rewardPlayerData =
                    Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, List<PlayerAward>>>(
                        GetAwardsPath()) ??
                    new Dictionary<ulong, List<PlayerAward>>();
            }

            public static void SaveDataAwards()
            {
                Interface.Oxide.DataFileSystem.WriteObject(GetAwardsPath(), _rewardPlayerData);
            }

            #endregion

            public static void StoreTopPlayersAwards()
            {
                _rewardPlayerData.Clear();
                var categories = _config.Awards.Categories.FindAll(c => c.Enabled);
                if (categories.Count == 0)
                {
                    SaveDataAwards();
                    return;
                }

                var completedCount = 0;
                for (var categoryIndex = 0; categoryIndex < categories.Count; categoryIndex++)
                {
                    var localCategoryIndex = categoryIndex;

                    var category = categories[localCategoryIndex];
                    var limit = category.Places.Max(x => x.Place);

                    Instance.dataStorage.GetTopPlayers(category.Type, category.Prefab, limit, topPlayers =>
                    {
                        for (var i = 0; i < topPlayers.Count; i++)
                        {
                            var playerId = topPlayers[i].UserId;
                            if (!_rewardPlayerData.ContainsKey(playerId))
                                _rewardPlayerData[playerId] = new List<PlayerAward>();

                            _rewardPlayerData[playerId].Add(new PlayerAward
                                {CategoryIndex = localCategoryIndex, Place = i + 1});
                        }

                        completedCount++;
                        if (completedCount == categories.Count) SaveDataAwards();
                    });
                }
            }

            public static void PlayerConnectedCheck(BasePlayer player)
            {
                if (_rewardPlayerData.TryGetValue(player.userID, out var awards))
                {
                    var categories = _config.Awards.Categories.FindAll(c => c.Enabled);

                    foreach (var award in awards)
                    {
                        var category = categories[award.CategoryIndex];

                        var awardConfig = category.Places.Find(p => p.Place == award.Place);
                        if (awardConfig != null) GiveAward(player, awardConfig);
                    }

                    _rewardPlayerData.Remove(player.userID);
                    SaveDataAwards();
                }
            }

            private static void GiveAward(BasePlayer player, AwardConfig awardConfig)
            {
                switch (awardConfig.Type)
                {
                    case AwardConfig.AwardType.Command:
                        var command = awardConfig.Command.Replace("%steamid%", player.UserIDString);
                        ConsoleSystem.Run(ConsoleSystem.Option.Server, command);
                        Instance.Puts($"Awarded {player.displayName} ({player.UserIDString}): {awardConfig.Title}");
                        break;

                    default:
                        Instance.PrintWarning($"Unsupported award type: {awardConfig.Type}");
                        break;
                }
            }
        }

        #endregion

        #region Auto Messages

        private void LoadAutoMessages()
        {
            if (_config.AutoMessages.Chat.Enabled) timer.Every(_config.AutoMessages.Chat.MessageInterval, SendChatMessages);

            if (_config.AutoMessages.Discord.Enabled && !_config.AutoMessages.SendSameMessageToBoth)
                timer.Every(_config.AutoMessages.Discord.MessageInterval, SendDiscordMessages);
        }

        private void SendChatMessages()
        {
            if (!_config.AutoMessages.Chat.Enabled || !isLeaderboard)
                return;

            var message = _config.AutoMessages.Chat.Messages.GetRandom();
            if (message == null) return;

            var topPlayers = Leaderboard.GetLeaderboardMessagesUsers(message.LootType, message.Prefab, 0, message.Limit);

            var sb = Pool.Get<StringBuilder>();
            try
            {
                sb.AppendLine(message.Header);

                for (var i = 0; i < topPlayers.Count; i++)
                {
                    var player = topPlayers[i];
                    var value = GetFormattedValue(message.LootType, message.Prefab, player.messageSortValues[(message.LootType, message.Prefab)]);

                    sb.AppendLine($"{i + 1}. {player.LastName} - {value}");
                }

                var chatMessage = sb.ToString();

                foreach (var player in BasePlayer.activePlayerList) SendReply(player, chatMessage);

                if (_config.AutoMessages.SendSameMessageToBoth && _config.AutoMessages.Discord.Enabled)
                    SendDiscordMessage(_config.AutoMessages.Discord.Webhook, chatMessage);
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        private void SendDiscordMessages()
        {
            if (!_config.AutoMessages.Discord.Enabled || string.IsNullOrEmpty(_config.AutoMessages.Discord.Webhook) || !isLeaderboard)
                return;

            var message = _config.AutoMessages.Discord.Messages.GetRandom();
            if (message == null) return;

            var topPlayers =
                Leaderboard.GetLeaderboardMessagesUsers(message.LootType, message.Prefab, 0, message.Limit);

            var sb = Pool.Get<StringBuilder>();
            try
            {
                sb.AppendLine(message.Header);

                for (var i = 0; i < topPlayers.Count; i++)
                {
                    var player = topPlayers[i];
                    var value = GetFormattedValue(message.LootType, message.Prefab, player.messageSortValues[(message.LootType, message.Prefab)]);

                    sb.AppendLine($"{i + 1}. {player.LastName} - {value}");
                }

                var discordMessage = sb.ToString();

                SendDiscordMessage(_config.AutoMessages.Discord.Webhook, discordMessage);
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        private void SendDiscordMessage(string webhook, string message)
        {
            var embed = new Embed();
            embed.AddField("Ultimate Leaderboard", message, false, _config.AutoMessages.Discord.LineColors.GetRandom());

            var discordMessageObj = new DiscordMessage("", embed);

            webrequest.Enqueue(webhook, discordMessageObj.ToJson(), (code, response) => { },
                this,
                RequestMethod.POST,
                new Dictionary<string, string>
                {
                    {"Content-Type", "application/json"}
                });
        }


        public static string GetFormattedValue(LootSettings.LootType type, string prefab, float val)
        {
            if (type == LootSettings.LootType.Custom)
            {
                switch (prefab)
                {
                    case "formatеed_total_playtime":
                        {
                            return Instance.GetPlaytimeLocalized(null, Convert.ToDouble(val));
                        }

                    case "current_playtime":
                        {
                            return Msg(UIStatValueToday, null, Instance.GetPlaytimeLocalized(null, val));
                        }
                }
            }

            return FormatLargeNumber(val);
        }

        #region Discord Classes

        public class Embed
        {
            public int color { get; set; }

            [JsonProperty("fields")] public List<Field> Fields { get; set; } = new();

            public Embed AddField(string name, string value, bool inline, int colors)
            {
                Fields.Add(new Field(name, Regex.Replace(value, "<.*?>", string.Empty), inline));
                color = colors;
                return this;
            }
        }

        public class Field
        {
            public Field(string name, string value, bool inline)
            {
                Name = name;
                Value = value;
                Inline = inline;
            }

            [JsonProperty("name")] public string Name { get; set; }

            [JsonProperty("value")] public string Value { get; set; }

            [JsonProperty("inline")] public bool Inline { get; set; }
        }

        public class DiscordMessage
        {
            [JsonProperty("content")] public string Content { get; set; }

            [JsonProperty("embeds")] public List<Embed> Embeds { get; set; } = new();

            public DiscordMessage(string content, Embed embed)
            {
                Content = content;
                Embeds.Add(embed);
            }

            public string ToJson()
            {
                return JsonConvert.SerializeObject(this);
            }
        }

        #endregion Discord Classes

        #endregion Auto Messages

        #region UI Settings

        private static UISettings _uiSettings;

        public class UISettings
        {
            [JsonProperty(PropertyName = "Pagination Button Next")]
            public string PaginationButtonNext;

            [JsonProperty(PropertyName = "Pagination Button Back")]
            public string PaginationButtonBack;

            [JsonProperty(PropertyName = "Pagination Button End")]
            public string PaginationButtonEnd;

            [JsonProperty(PropertyName = "Pagination Button Start")]
            public string PaginationButtonStart;

            [JsonProperty(PropertyName = "Awards Icon")]
            public string AwardsIcon;
        }

        private void LoadUISettings()
        {
            try
            {
                _uiSettings =
                    Interface.Oxide.DataFileSystem.ExistsDatafile(nameof(UltimateLeaderboard) +
                                                                  Path.DirectorySeparatorChar + "UI" +
                                                                  Path.DirectorySeparatorChar + "Settings")
                        ? Interface.Oxide.DataFileSystem.ReadObject<UISettings>(nameof(UltimateLeaderboard) +
                            Path.DirectorySeparatorChar + "UI" + Path.DirectorySeparatorChar + "Settings")
                        : null;
            }
            catch (Exception ex)
            {
                PrintError("Error loading UI Settings: " + ex.Message);
            }

            if (_uiSettings == null)
            {
                _uiSettings = GetDefaultUISettings();

                Interface.Oxide.DataFileSystem.WriteObject(
                    nameof(UltimateLeaderboard) + Path.DirectorySeparatorChar + "UI" + Path.DirectorySeparatorChar +
                    "Settings", _uiSettings);
            }
        }

        private UISettings GetDefaultUISettings()
        {
            return new UISettings
            {
                PaginationButtonNext = "https://i.ibb.co/YBgPCJLz/icon-next.png",
                PaginationButtonEnd = "https://i.ibb.co/qYps6DqN/icon-end.png",
                PaginationButtonStart = "https://i.ibb.co/5hx7pDJC/icon-start.png",
                PaginationButtonBack = "https://i.ibb.co/WvdXNVxX/icon-back.png",
                AwardsIcon = "https://i.ibb.co/67LK9gS9/image-1740214539457-432.png"
            };
        }

        #endregion UI Settings

        #region Templates

        private ITemplateRenderer templateRenderer;

        private interface ITemplateRenderer
        {
            void Render(BasePlayer player, CuiElementContainer container);

            void CategoriesSection(BasePlayer player, CuiElementContainer container);

            void ContentSection(BasePlayer player, CuiElementContainer container);

            void UserProfilePage(BasePlayer player, PlayerStats stats, CuiElementContainer container);

            void LeaderboardPage(BasePlayer player, CuiElementContainer container);
            void LeaderboardAwardsSection(BasePlayer player, CuiElementContainer container);
            void ShowLeaderboardTableUI(BasePlayer player, CuiElementContainer container);
            void ShowLeaderboardPaginationUI(BasePlayer player, CuiElementContainer container);
            int GetLeaderboardPlayersToShow();

            void SearchPage(BasePlayer player, CuiElementContainer container);
            void SearchInputField(BasePlayer player, CuiElementContainer container, bool update = false);
            void SearchPlayersSection(BasePlayer player, CuiElementContainer container);

            void DrawProfileBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight);

            void DrawStatisticsBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight);

            void DrawHitRateBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight);

            void DrawBuildingBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight);
        }

        private class TemplateV1Renderer : ITemplateRenderer
        {
            public void Render(BasePlayer player, CuiElementContainer container)
            {
                CategoriesSection(player, container);

                ContentSection(player, container);
            }

            public void CategoriesSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                var categories = Instance.GetAvailableCategories(player.UserIDString);
                try
                {
                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-600 -35",
                            OffsetMax = "600 0"
                        }
                    }, Layer + ".Background", Layer + ".Categories", Layer + ".Categories");

                    var categoriesOffsetX = 0f;
                    var categoryWidth = 168f;
                    var categoryMarginX = 4f;

                    for (var i = 0; i < categories.Count; i++)
                    {
                        var category = categories[i];

                        container.Add(new CuiButton
                        {
                            Text =
                            {
                                Text = Msg(player, "UI.Category." + category.Title), Font = "robotocondensed-bold.ttf",
                                FontSize = 17,
                                Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            Button =
                            {
                                Command = $"UI_UltimateLeaderboard page {i}",
                                Color = openedLeaderboard.Page == i
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{categoriesOffsetX} -40",
                                OffsetMax = $"{categoriesOffsetX + categoryWidth} 0"
                            }
                        }, Layer + ".Categories");

                        categoriesOffsetX += categoryWidth + categoryMarginX;
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref categories);
                }
            }

            public void ContentSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                    {
                        AnchorMin = "0.5 1",
                        AnchorMax = "0.5 1",
                        OffsetMin = "-640 -550",
                        OffsetMax = "640 -40"
                    }
                }, Layer + ".Background", Layer + ".Main", Layer + ".Main");

                switch (openedLeaderboard.Page)
                {
                    case 0:
                    {
                        UserProfilePage(player, PlayerStats.Get(player.userID), container);
                        break;
                    }

                    case 1: // leaderboard
                    {
                        LeaderboardPage(player, container);
                        break;
                    }

                    case 2: // search
                    {
                        SearchPage(player, container);
                        break;
                    }
                }
            }

            public void UserProfilePage(BasePlayer player, PlayerStats stats, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                #region Statistics Section

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #endregion Statistics Section

                #region Tabs

                var tabs = Instance?.GetAvailableTabs(stats.UserId.ToString());
                try
                {
                    var tabOffsetX = 0;

                    // Tab buttons
                    var tabButtonWidth = 130;
                    var tabButtonHeight = 25;
                    var tabButtonMarginX = 5;

                    #region Tabs.Scroll

                    var tabsScrollWidth = tabs.Count * tabButtonHeight + (tabs.Count - 1) * tabButtonMarginX;

                    tabsScrollWidth = Mathf.Max(tabsScrollWidth, 1200);

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Tabs",
                        Parent = LayerContent,
                        Components =
                        {
                            // new CuiImageComponent() { Color = "0 0 0 0" },
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = false,
                                Inertia = true,
                                Horizontal = true,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = new CuiRectTransform
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = $"{tabsScrollWidth} 0"
                                },
                                HorizontalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    AutoHide = true,
                                    HighlightColor = HexToCuiColor("#D74933"),
                                    HandleColor = HexToCuiColor("#D74933"),
                                    PressedColor = HexToCuiColor("#D74933"),
                                    TrackColor = HexToCuiColor("#373737")
                                }
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 1",
                                AnchorMax = "0.5 1",
                                OffsetMin = "-600 -40",
                                OffsetMax = "600 -10"
                            }
                        }
                    });

                    for (var tabIndex = 0; tabIndex < tabs.Count; tabIndex++)
                    {
                        var tab = tabs[tabIndex];

                        var leftTabOffset = tabOffsetX + tabIndex * (tabButtonWidth + tabButtonMarginX);

                        container.Add(new CuiButton
                        {
                            Button =
                            {
                                Command = $"UI_UltimateLeaderboard open_profile {stats.UserId} tab {tabIndex}",
                                Color = openedLeaderboard.SelectedTab == tabIndex
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            Text =
                            {
                                Text = Msg(player, "UI.Tabs." + tab.Name),
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 16,
                                Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{leftTabOffset} {-tabButtonHeight}",
                                OffsetMax = $"{leftTabOffset + tabButtonWidth} {0}"
                            }
                        }, LayerContent + ".Tabs");
                    }

                    #endregion Tabs.Scroll

                    #region Tabs.Blocks

                    #region Tabs.Blocks.Scroll

                    var scrollRect = new CuiRectTransform
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"0 {-4000}",
                        OffsetMax = "0 0"
                    };

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Scroll",
                        Parent = LayerContent,
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = true,
                                Inertia = true,
                                Horizontal = false,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = scrollRect,
                                VerticalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    AutoHide = true,
                                    HighlightColor = HexToCuiColor("#D74933"),
                                    HandleColor = HexToCuiColor("#D74933"),
                                    PressedColor = HexToCuiColor("#D74933"),
                                    TrackColor = HexToCuiColor("#373737")
                                }
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 1",
                                AnchorMax = "0.5 1",
                                OffsetMin = "-600 -515",
                                OffsetMax = "605 -40"
                            }
                        }
                    });

                    #endregion Tabs.Blocks.Scroll

                    #region Tabs.Blocks.Content

                    var targetTab = tabs[openedLeaderboard.SelectedTab];

                    var offsetY = 0f;
                    var blockMarginY = 20f;

                    for (var k = 0; k < targetTab.Blocks.Count; k++)
                    {
                        var block = targetTab.Blocks[k];

                        float totalHeight;
                        switch (block.BlockType)
                        {
                            case "Profile":
                                DrawProfileBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "Statistics":
                                DrawStatisticsBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "HitRate":
                                DrawHitRateBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "Building":
                                DrawBuildingBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            default:
                                totalHeight = 0f;
                                break;
                        }

                        offsetY = offsetY - totalHeight;

                        if (k != targetTab.Blocks.Count - 1)
                            offsetY = offsetY - blockMarginY;
                    }

                    offsetY = Mathf.Min(-435, offsetY);

                    scrollRect.OffsetMin = $"0 {offsetY}";

                    #endregion Tabs.Blocks.Content

                    #endregion Tabs.Blocks
                }
                finally
                {
                    Pool.FreeUnmanaged(ref tabs);
                }

                #endregion Tabs
            }

            private float leaderboardLeftIndent = 20f;

            public void LeaderboardPage(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                #region Statistics Section

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #endregion Statistics Section

                #region Awards

                var tableTopOffset = -100;

                if (_config.Awards.DisplayAwardsUI)
                    LeaderboardAwardsSection(player, container);
                else
                    tableTopOffset = -10;

                #endregion Awards

                #region Leaderboard

                #region Header

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                        {AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-600 {tableTopOffset - 32}", OffsetMax = $"600 {tableTopOffset}"}
                }, LayerContent, LayerContent + ".Leaderboard.Header");

                tableTopOffset = tableTopOffset - 32;

                var offsetX = leaderboardLeftIndent;

                for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                {
                    var leaderboardColumn = _config.LeaderboardColumns[colIndex];

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} 0", OffsetMax = $"{offsetX + leaderboardColumn.Width} 0"
                        },
                        Text =
                        {
                            Text = Msg(player, "UI.Leaderboard.Column." + leaderboardColumn.Name),
                            Font = "robotocondensed-bold.ttf", FontSize = 12,
                            Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_UltimateLeaderboard leaderboard select_column " + colIndex
                        }
                    }, LayerContent + ".Leaderboard.Header");

                    if (colIndex + 1 != _config.LeaderboardColumns.Count)
                        container.Add(new CuiPanel
                        {
                            Image = {Color = HexToCuiColor("#E2DBD3", 20)},
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0 1",
                                OffsetMin = $"{offsetX + leaderboardColumn.Width - 1f} 0",
                                OffsetMax = $"{offsetX + leaderboardColumn.Width + 1f} 0"
                            }
                        }, LayerContent + ".Leaderboard.Header");

                    offsetX += leaderboardColumn.Width;
                }

                #endregion

                #region Scroll

                var leaderboardTotalWidth = leaderboardLeftIndent + _config.LeaderboardColumns.Sum(x => x.Width);

                leaderboardTotalWidth = Mathf.Max(leaderboardTotalWidth, 1200);

                var playerHeight = 40f;
                var playerMarginY = 4f;

                var leaderboardTotalHeight = GetLeaderboardPlayersToShow() * playerHeight +
                                             (GetLeaderboardPlayersToShow() - 1) * playerMarginY;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Scroll",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = true,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = new CuiRectTransform
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"0 -{leaderboardTotalHeight}",
                                OffsetMax = $"{leaderboardTotalWidth} 0"
                            },
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            },
                            HorizontalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 1",
                            OffsetMin = "-600 35", 
                            OffsetMax = $"605 {tableTopOffset - 8}"
                            // OffsetMax = "605 -140"
                        }
                    }
                });

                #endregion Scroll

                ShowLeaderboardTableUI(player, container);

                ShowLeaderboardPaginationUI(player, container);

                #endregion
            }

            public void LeaderboardAwardsSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Leaderboard.Awards",
                    DestroyUi = LayerContent + ".Leaderboard.Awards",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#000000", 0)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-600 -100",
                            OffsetMax = "600 -10"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Leaderboard.Awards",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.AwardsIcon)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -91",
                            OffsetMax = "98 -13"
                        }
                    }
                });

                #region Awards.Scroll

                var awardsTotalWidth = 765f;

                var awardScrollRect = new CuiRectTransform
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0 1",
                    OffsetMin = "0 0",
                    OffsetMax = $"{awardsTotalWidth} 0"
                };

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                    Parent = LayerContent + ".Leaderboard.Awards",
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = false,
                            Inertia = true,
                            Horizontal = true,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = awardScrollRect,
                            HorizontalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                Invert = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "120 0", OffsetMax = "0 0"
                        }
                    }
                });

                #region Title

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UITitleAwards), Font = "robotocondensed-regular.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -23",
                            OffsetMax = "200 5"
                        }
                    }
                });

                #endregion Title

                var awardCategories = Instance.GetAvailableAwardCategories(player.UserIDString);
                try
                {
                    if (awardCategories.Count > 0)
                    {
                        #region Tabs

                        var awardCategoryWidth = 105;
                        var awardCategoryMarginX = 5;

                        var awardCategoriesOffset = 100;

                        for (var awardIndex = 0; awardIndex < awardCategories.Count; awardIndex++)
                        {
                            var awardCategory = awardCategories[awardIndex];

                            var awardOffset = awardCategoriesOffset +
                                              awardIndex * (awardCategoryWidth + awardCategoryMarginX);

                            container.Add(new CuiButton
                            {
                                RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{awardOffset} -20",
                                OffsetMax = $"{awardOffset + awardCategoryWidth} 0"
                            },
                                Text =
                            {
                                Text = Msg(player, "UI.Awards." + awardCategory.Title),
                                Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90)
                            },
                                Button =
                            {
                                Color = openedLeaderboard.awardTabSelected == awardIndex
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Command = $"UI_UltimateLeaderboard leaderboard award_tab {awardIndex}"
                            }
                            }, LayerContent + ".Leaderboard.Awards" + ".Scroll");
                        }

                        #endregion Tabs

                        #region List

                        var awardPlaceWidth = 210;
                        var awardPlaceMarginX = 5;
                        var awardPlacesOffset = 0;

                        var selectedAwardCategory = awardCategories[openedLeaderboard.awardTabSelected];

                        for (var i = 0; i < selectedAwardCategory.Places.Count; i++)
                        {
                            var place = selectedAwardCategory.Places[i];

                            var awardOffset = awardPlacesOffset + i * (awardPlaceWidth + awardPlaceMarginX);

                            var targetAwardLayer = LayerContent + ".Leaderboard.Awards" + ".Award" + i;

                            container.Add(new CuiElement
                            {
                                Name = targetAwardLayer,
                                Parent = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                                Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#696969", 15),
                                    Sprite = "assets/content/ui/ui.background.tile.psd",
                                    Material = "assets/content/ui/namefontmaterial.mat",
                                    ImageType = Image.Type.Tiled
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{awardOffset} -77",
                                    OffsetMax = $"{awardOffset + awardPlaceWidth} -27"
                                }
                            }
                            });
                            container.Add(new CuiElement
                            {
                                Parent = targetAwardLayer,
                                Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = Instance.GetImage(place.Image)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5",
                                    AnchorMax = "0 0.5",
                                    OffsetMin = "5 -20",
                                    OffsetMax = "45 20"
                                }
                            }
                            });

                            #region titles

                            container.Add(new CuiElement
                            {
                                Parent = targetAwardLayer,
                                Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlaceAwards, place.Place),
                                    Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "1 1",
                                    OffsetMin = "50 -19",
                                    OffsetMax = "0 0"
                                }
                            }
                            });
                            container.Add(new CuiElement
                            {
                                Parent = targetAwardLayer,
                                Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, "UI.Awards.Place." + place.Title),
                                    Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.UpperLeft,
                                    Color = HexToCuiColor("#E2DBD3", 70)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "50 0",
                                    OffsetMax = "0 -19"
                                }
                            }
                            });

                            #endregion titles
                        }

                        #region Calculate Scroll

                        var tabsTotalWidth = awardCategoriesOffset + awardCategories.Count * awardCategoryWidth +
                                             (awardCategories.Count - 1) * awardCategoryMarginX;

                        var awardPlacesTotalWidth = awardPlacesOffset +
                                                    selectedAwardCategory.Places.Count * awardPlaceWidth +
                                                    (selectedAwardCategory.Places.Count - 1) * awardPlaceMarginX;

                        awardsTotalWidth = Mathf.Max(awardsTotalWidth, tabsTotalWidth);

                        awardsTotalWidth = Mathf.Max(awardsTotalWidth, awardPlacesTotalWidth);

                        awardScrollRect.OffsetMax = $"{awardsTotalWidth} 0";

                        #endregion Calculate Scroll

                        #endregion List
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref awardCategories);
                }

                #endregion Awards.Scroll
            }

            public void ShowLeaderboardPaginationUI(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;
                
                #region Pagination

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Pages",
                    DestroyUi = LayerContent + ".Pagination.Pages",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#696969", 30)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "-37 0",
                            OffsetMax = "38 30"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Pages",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text =
                                $"{openedLeaderboard.LeaderboardPage + 1} / {openedLeaderboard.GetMaxLeaderboardPage()}",
                            Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 70)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Next",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page next"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "44 0",
                            OffsetMax = "76 30"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Next",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonNext)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.End",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page end"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "82 0",
                            OffsetMax = "114 30"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.End",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonEnd)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Start",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page start"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "-113 0",
                            OffsetMax = "-81 30"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Start",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonStart)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Back",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page back"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "-75 0",
                            OffsetMax = "-43 30"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Back",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonBack)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });

                #endregion Pagination
            }

            public void ShowLeaderboardTableUI(BasePlayer player, CuiElementContainer leaderboardContainer)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                var playerHeight = 40f;
                var playerMarginY = 4f;

                var leaderboardPlayersOffset = openedLeaderboard.LeaderboardPage * GetLeaderboardPlayersToShow();

                var players =
                    Instance.Leaderboard?.GetLeaderboardUsers(leaderboardPlayersOffset, GetLeaderboardPlayersToShow(),
                        openedLeaderboard.GetSelectedColumn());
                try
                {
                    leaderboardContainer.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = "0 0 0 0"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = $"{leaderboardLeftIndent} 0", OffsetMax = "-5 0"
                            }
                        }, LayerContent + ".Scroll", LayerContent + ".Leaderboard.Table",
                        LayerContent + ".Leaderboard.Table");
                    
                    if (!Instance.isLeaderboard)
                    {
                        leaderboardContainer.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text = 
                            {
                                Text = Msg(player, UILeaderboardLoading),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 30,
                                Color = HexToCuiColor("#FFFFFF", 50),
                            }
                        }, LayerContent + ".Leaderboard.Table");
                    }
                    else
                    {
                    var playersOffsetY = 0f;
                    for (var i = 0; i < players.Count; i++)
                    {
                        var topPlayer = players[i];

                        var topNumber = leaderboardPlayersOffset + i + 1;

                        var targetPanelColor = topNumber switch
                        {
                            1 => HexToCuiColor("#71B8ED", 25),
                            2 => HexToCuiColor("#71B8ED", 15),
                            3 => HexToCuiColor("#71B8ED", 10),
                            _ => topNumber % 2 == 0 ? HexToCuiColor("#696969", 10) : HexToCuiColor("#696969", 15)
                        };

                        var targetTopColor = topNumber switch
                        {
                            1 => HexToCuiColor("#FFFFFF"),
                            2 => HexToCuiColor("#FFFFFF", 70),
                            3 => HexToCuiColor("#FFFFFF", 50),
                            _ => HexToCuiColor("#E2DBD3", 20)
                        };

                        var targetTextColor = topNumber switch
                        {
                            1 => HexToCuiColor("#FFFFFF"),
                            _ => HexToCuiColor("#E2DBD3", 70)
                        };

                        #region Player Panel

                        var playerLayer = LayerContent + ".Scroll" + ".Player." + i;

                        leaderboardContainer.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = targetPanelColor,
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat",
                                ImageType = Image.Type.Tiled
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1",
                                OffsetMin = $"0 {playersOffsetY - playerHeight}",
                                OffsetMax = $"0 {playersOffsetY}"
                            }
                        }, LayerContent + ".Leaderboard.Table", playerLayer, playerLayer);

                        leaderboardContainer.Add(new CuiElement
                        {
                            Parent = playerLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = topNumber.ToString(),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleLeft, Color = targetTopColor
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "-20 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        var playersOffsetX = 0f;
                        for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                        {
                            var leaderboardColumn = _config.LeaderboardColumns[colIndex];

                            if (leaderboardColumn.Type == LootSettings.LootType.Custom &&
                                leaderboardColumn.Prefab == "nickname")
                            {
                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiRawImageComponent
                                        {
                                            SteamId = topPlayer.UserId.ToString()
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0.5",
                                            AnchorMax = "0 0.5",
                                            OffsetMin = $"{playersOffsetX + 6} -14",
                                            OffsetMax = $"{playersOffsetX + 34} 14"
                                        }
                                    }
                                });

                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiTextComponent
                                        {
                                            Text = topPlayer.LastName ?? string.Empty,
                                            Font = "robotocondensed-bold.ttf",
                                            FontSize = 12,
                                            Align = TextAnchor.MiddleLeft,
                                            Color = targetTextColor
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 1",
                                            OffsetMin = $"{playersOffsetX + 45} 0",
                                            OffsetMax = $"{playersOffsetX + leaderboardColumn.Width} 0"
                                        }
                                    }
                                });
                            }
                            else if (colIndex < topPlayer.leaderboardItems.Length)
                            {
                                var val = topPlayer.leaderboardItems[colIndex];

                                var statValue = leaderboardColumn.Type == LootSettings.LootType.Custom && leaderboardColumn.Prefab == "formatеed_total_playtime" ? Instance.GetPlaytimeLocalized(player, Convert.ToDouble(val)) : FormatLargeNumber(val);
                                
                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiTextComponent
                                        {
                                            Text = statValue ?? string.Empty,
                                            Font = "robotocondensed-bold.ttf", FontSize = 12,
                                            Align = TextAnchor.MiddleCenter,
                                            Color = targetTextColor
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 1",
                                            OffsetMin = $"{playersOffsetX} 0",
                                            OffsetMax = $"{playersOffsetX + leaderboardColumn.Width} 0"
                                        }
                                    }
                                });
                            }

                            if (colIndex + 1 != _config.LeaderboardColumns.Count)
                            {
                                leaderboardContainer.Add(new CuiPanel
                                {
                                    Image = {Color = HexToCuiColor("#E2DBD3", 10)},
                                    RectTransform =
                                    {
                                        AnchorMin = "0 0", AnchorMax = "0 1",
                                        OffsetMin = $"{playersOffsetX + leaderboardColumn.Width - 1f} 0",
                                        OffsetMax = $"{playersOffsetX + leaderboardColumn.Width + 1f} 0"
                                    }
                                }, playerLayer);

                                playersOffsetX += leaderboardColumn.Width;
                            }
                        }

                        #endregion Player Panel

                        #region Position

                        playersOffsetY = playersOffsetY - playerHeight - playerMarginY;

                        #endregion
                    }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }
            }

            public int GetLeaderboardPlayersToShow()
            {
                return 10;
            }

            public void SearchPage(BasePlayer player, CuiElementContainer container)
            {
                #region Statistics Section

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #endregion Statistics Section

                #region Search

                container.Add(new CuiElement
                {
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UISearchPlayersTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -50", OffsetMax = "0 -10"}
                    }
                });

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = HexToCuiColor("#696969", 30), Sprite = "assets/content/ui/UI.Background.Tile.psd",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "40 -90", OffsetMax = "334 -50"}
                }, LayerContent, LayerContent + ".Panel.Search");

                SearchInputField(player, container);

                #endregion

                SearchPlayersSection(player, container);
            }


            public void SearchInputField(BasePlayer player, CuiElementContainer container, bool update = false)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Panel.Search" + ",Input",
                    Parent = LayerContent + ".Panel.Search",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = !string.IsNullOrWhiteSpace(openedLeaderboard.search)
                                ? openedLeaderboard.search
                                : Msg(player, UISearchInputPlaceholder),
                            Font = "robotocondensed-bold.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 30),
                            NeedsKeyboard = true,
                            Command = "UI_UltimateLeaderboard search"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
                    }
                });
            }

            public void SearchPlayersSection(BasePlayer player, CuiElementContainer container)
            {
                #region Background

                var scrollRect = new CuiRectTransform
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"0 {-1500}",
                    OffsetMax = "0 0"
                };

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Section.Players" + ".Scroll",
                    DestroyUi = LayerContent + ".Section.Players" + ".Scroll",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = false,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = scrollRect,
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-600 -500",
                            OffsetMax = "600 -100"
                        }
                    }
                });

                #endregion

                #region Table

                var playerWidth = 222f;
                var playerHeight = 40f;
                var playersMarginX = 78f;
                var playersMarginY = 0f;

                var defaultIndentX = 0f;
                var defaultIndentY = -40f;

                var offsetX = defaultIndentX;
                var offsetY = defaultIndentY;

                var players = Pool.Get<List<(string displayName, string userID)>>();
                try
                {
                    Instance.PopulatePlayers(player.userID, ref players);

                    var maxColumns = 4;
                    var rows = Mathf.CeilToInt((float) players.Count / maxColumns);

                    var totalScrollHeight =
                        Mathf.Max(rows * playerHeight + (rows - 1) * playersMarginY + Mathf.Abs(defaultIndentY), 400);
                    scrollRect.OffsetMin = $"0 -{totalScrollHeight}";

                    if (players.Count == 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = LayerContent + ".Section.Players" + ".Scroll",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlayersNoPlayers),
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 40,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    VerticalOverflow = VerticalWrapMode.Overflow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1"
                                }
                            }
                        });
                    }
                    else
                    {
                        #region Title

                        container.Add(new CuiElement
                        {
                            Parent = LayerContent + ".Section.Players" + ".Scroll",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlayersListTitle), Font = "robotocondensed-bold.ttf",
                                    FontSize = 20, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -30", OffsetMax = "-40 0"}
                            }
                        });

                        #endregion

                        for (var i = 0; i < players.Count; i++)
                        {
                            var (displayName, userID) = players[i];

                            #region Panel

                            var targetColor =
                                (i + 1) % 2 == 0 ? HexToCuiColor("#696969", 10) : HexToCuiColor("#696969", 15);

                            var playerLayer = CuiHelper.GetGuid();

                            container.Add(new CuiElement
                            {
                                Name = playerLayer,
                                Parent = LayerContent + ".Section.Players" + ".Scroll",
                                Components =
                                {
                                    new CuiButtonComponent
                                    {
                                        Command = "UI_UltimateLeaderboard search_open_profile " + userID,
                                        Color = targetColor,
                                        Sprite = "assets/content/ui/ui.background.tile.psd",
                                        Material = "assets/content/ui/namefontmaterial.mat",
                                        ImageType = Image.Type.Tiled
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 1", AnchorMax = "0 1",
                                        OffsetMin = $"{offsetX} {offsetY - playerHeight}",
                                        OffsetMax = $"{offsetX + playerWidth} {offsetY}"
                                    }
                                }
                            });


                            container.Add(new CuiElement
                            {
                                Parent = playerLayer,
                                Components =
                                {
                                    new CuiRawImageComponent {SteamId = userID},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "6 -14",
                                        OffsetMax = "34 14"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = playerLayer,
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = displayName ?? "Unknown",
                                        Font = "robotocondensed-bold.ttf", FontSize = 12,
                                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 70)
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 0", OffsetMax = "0 0"
                                    }
                                }
                            });

                            #endregion

                            #region Position

                            if ((i + 1) % rows == 0)
                            {
                                offsetY = defaultIndentY;
                                offsetX = offsetX + playerWidth + playersMarginX;
                            }
                            else
                            {
                                offsetY = offsetY - playerHeight - playersMarginY;
                            }

                            #endregion
                        }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }

                #endregion
            }

            #region Blocks

            public void DrawProfileBlock(BasePlayer player,
                BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                #region Fields

                var fieldsOnRow = 4;
                var fieldWidth = 220f;
                var fieldHeight = 48f;
                var fieldMarginX = 100f;
                var fieldMarginY = 30f;
                var leftIndent = 10f;

                var columns = Mathf.Min(fieldsOnRow, block.StatFields.Count);
                var rows = Mathf.CeilToInt((float) block.StatFields.Count / fieldsOnRow);

                var maxWidth = columns * fieldWidth + (columns - 1) * fieldMarginX;
                var maxHeight = rows * fieldHeight + (rows - 1) * fieldMarginY + 20;

                #endregion Fields

                var localOffsetY = offsetY - 10f;

                var currentField = 0;
                for (var row = 0; row < rows; row++)
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 20),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} {localOffsetY - fieldHeight - 10}",
                            OffsetMax = $"{maxWidth} {localOffsetY + 10}"
                        }
                    }, LayerContent + ".Scroll");

                    var offsetX = leftIndent;
                    for (var col = 0; col < fieldsOnRow && currentField < block.StatFields.Count; col++)
                    {
                        DrawStatField(player,
                            block.StatFields[currentField],
                            stats,
                            ref container,
                            $"{offsetX} {localOffsetY - fieldHeight}",
                            $"{offsetX + fieldWidth} {localOffsetY}"
                        );

                        currentField++;

                        offsetX += fieldWidth + fieldMarginX;
                    }

                    localOffsetY = localOffsetY - fieldHeight - fieldMarginY;
                }

                totalHeight = maxHeight;
            }

            public void DrawStatisticsBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                var leftIndent = 0f;

                var columnFieldWidth = 166f;
                var columnFieldHeight = 68f;
                var columnFieldMarginX = 5f;
                var columnFieldMarginY = 5f;

                #region Draw Field resources

                void DrawStatGUI(string ID, string parent,
                    string oMin, string oMax,
                    StatFieldConfig statField)
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 20),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = oMin, OffsetMax = oMax
                        }
                    }, parent, ID);

                    #region Icon

                    var imageLayer = container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 50),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "10 -24", OffsetMax = "58 24"}
                    }, ID);

                    var def = !string.IsNullOrEmpty(statField.Icon)
                        ? null
                        : ItemManager.FindItemDefinition(statField.Prefab);
                    container.Add(new CuiElement
                    {
                        Parent = imageLayer,
                        Components =
                        {
                            def != null
                                ? new CuiImageComponent
                                {
                                    ItemId = def.itemid
                                }
                                : statField.Icon.StartsWith("assets/")
                                    ? new CuiRawImageComponent {Sprite = statField.Icon}
                                    : new CuiRawImageComponent {Png = Instance.GetImage(statField.Icon)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -24", OffsetMax = "24 24"
                            }
                        }
                    });

                    #endregion

                    #region Stats

                    var statSB = Pool.Get<StringBuilder>();
                    try
                    {
                        statSB.Append(Msg(player, "UI.Field." + statField.Title));

                        if (string.IsNullOrEmpty(statField.SecondPrefab))
                        {
                            statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                       stats.GetFormattedStatValue(player, statField.Type, statField.Prefab) +
                                                       "</b></color></size>");
                        }
                        else
                        {
                            statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                       stats.GetFormattedStatValue(player, statField.Type, statField.Prefab) +
                                                       "</b></color></size>");
                            statSB.Append('\n')
                                .Append(stats.GetFormattedStatValue(player, statField.Type, statField.SecondPrefab));
                        }

                        container.Add(new CuiElement
                        {
                            Parent = ID,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = statSB.ToString(),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    VerticalOverflow = VerticalWrapMode.Overflow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "70 0", OffsetMax = "-10 0"
                                }
                            }
                        });
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref statSB);
                    }

                    #endregion
                }

                #endregion

                var columnMarginY = 10f;
                var columnsOnRow = 7;

                var offsetX = leftIndent;

                totalHeight = 0;

                var localOffsetY = offsetY;

                for (var columnIndex = 0; columnIndex < block.Columns.Count; columnIndex++)
                {
                    var column = block.Columns[columnIndex];

                    var columnRows = Mathf.CeilToInt((float) column.StatFields.Count / columnsOnRow);
                    var rowsHeight = columnRows * columnFieldHeight + (columnRows - 1) * columnFieldMarginY;

                    var columnHeight = rowsHeight + 24f;
                    var columnWidth = columnsOnRow * columnFieldWidth + (columnsOnRow - 1) * columnFieldMarginX;

                    var columnLayer = LayerContent + $".Statistics.Section.{columnIndex}";

                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} {localOffsetY - columnHeight}",
                            OffsetMax = $"{offsetX + columnWidth} {localOffsetY}"
                        }
                    }, LayerContent + ".Scroll", columnLayer);

                    container.Add(new CuiElement
                    {
                        Parent = columnLayer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = column.Title, Font = "robotocondensed-bold.ttf", FontSize = 20,
                                Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"}
                        }
                    });

                    var fieldOffsetX = 0f;
                    var fieldOffsetY = -24f;

                    for (var fieldIndex = 0; fieldIndex < column.StatFields.Count; fieldIndex++)
                    {
                        var field = column.StatFields[fieldIndex];

                        DrawStatGUI(
                            $"{columnLayer} ({fieldIndex})",
                            columnLayer,
                            $"{fieldOffsetX} {fieldOffsetY - columnFieldHeight}",
                            $"{fieldOffsetX + columnFieldWidth} {fieldOffsetY}",
                            field);

                        if ((fieldIndex + 1) % columnsOnRow == 0 && fieldIndex + 1 != column.StatFields.Count)
                        {
                            fieldOffsetX = 0f;
                            fieldOffsetY = fieldOffsetY - columnFieldHeight - columnFieldMarginY;
                        }
                        else
                        {
                            fieldOffsetX += columnFieldWidth + columnFieldMarginX;
                        }
                    }

                    localOffsetY = localOffsetY - columnHeight - columnMarginY;

                    totalHeight += columnHeight + columnMarginY;
                }
            }

            public void DrawHitRateBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                var hitPointHeight = 435;

                #region Hit Point

                totalHeight = hitPointHeight;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-600 {offsetY - hitPointHeight}",
                        OffsetMax = $"600 {offsetY}"
                    }
                }, LayerContent + ".Scroll", LayerContent + ".Section.HitPoint");

                #region Fields

                var totalHits = stats.StatsStorage.GetTotal(LootSettings.LootType.BodyHits);

                var hitRateImageUrl = block.HitRateImages
                    .Find(hitRate => totalHits >= hitRate.MinHits && totalHits <= hitRate.MaxHits)?.Image;

                #endregion

                #region Title

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Section.HitPoint",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIHitRateTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#FFFFFF")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "367 0"}
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Section.HitPoint",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIHitRateDescription), Font = "robotocondensed-regular.ttf",
                            FontSize = 14, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -70", OffsetMax = "367 -30"}
                    }
                });

                #endregion

                #region Image

                if (!string.IsNullOrEmpty(hitRateImageUrl))
                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Section.HitPoint" + ".Image",
                        Parent = LayerContent + ".Section.HitPoint",
                        Components =
                        {
                            new CuiRawImageComponent {Png = Instance.GetImage(hitRateImageUrl)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-293 0", OffsetMax = "293 330"}
                        }
                    });

                #endregion

                #region Body Parts

                AddBodyPart("59 109.8", "139 129.8", "head", "HEAD");
                AddBodyPart("127 54", "207 74", "left arm|right arm", "ARM");
                AddBodyPart("80 -37", "160 -17", "chest", "CHEST");
                AddBodyPart("95 -139.5", "175 -119.5", "hip", "HIP");
                AddBodyPart("-237 -23.5", "-157 -3.5", "body", "BODY");
                AddBodyPart("-227 77.5", "-147 97.5", "neck", "NECK");

                void AddBodyPart(string offMin, string offMax, string key, string title = null)
                {
                    if (string.IsNullOrEmpty(title)) title = key;

                    if (!stats.StatsStorage.TryGetItem(LootSettings.LootType.BodyHits, key, out var value)) value = 0f;

                    var rect = new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = offMin, OffsetMax = offMax};

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.HitPoint" + ".Image",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, "UI.BodyParts." + title), Font = "robotocondensed-regular.ttf",
                                FontSize = 10, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF")
                            },
                            rect
                        }
                    });

                    var percent = value > 0 ? Mathf.CeilToInt(value / totalHits * 100f) : 0;

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.HitPoint" + ".Image",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = percent + "%", Font = "robotocondensed-regular.ttf", FontSize = 10,
                                Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#D74933")
                            },
                            rect
                        }
                    });
                }

                #endregion

                #endregion
            }

            public void DrawBuildingBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container,
                ref float offsetY, out float totalHeight)
            {
                totalHeight = 435;

                var topGrades = stats.StatsStorage.GetUpgrades();
                try
                {
                    var amount = topGrades.Sum(grade => grade.amount);

                    var bannerKey = "grade_banner_twigs";

                    if (topGrades.Count > 0)
                    {
                        var topGradeName = topGrades[0].shortname.Split(' ')[1];

                        bannerKey = $"grade_banner_{topGradeName}";
                    }

                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = $"-600 {offsetY - totalHeight}",
                            OffsetMax = $"600 {offsetY}"
                        }
                    }, LayerContent + ".Scroll", LayerContent + ".Section.Building");

                    #region Titles

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, UIBuildingTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                                Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#FFFFFF")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "367 0"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, UIBuildingDescription),
                                Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft,
                                Color = HexToCuiColor("#E2DBD3")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -70", OffsetMax = "367 -30"}
                        }
                    });

                    #endregion Titles

                    #region Top Banner

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Section.Building" + ".Banner",
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiRawImageComponent {Png = Instance.GetImage(bannerKey)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "525 367"}
                        }
                    });

                    #endregion Top Banner

                    #region Items

                    var columnUpIndent = 15f;
                    var columnLeftIndent = -720f; //408f;
                    var columnFieldWidth = 577f;
                    var columnFieldHeight = 54f;
                    var columnFieldMarginY = 22f;

                    for (var fieldIndex = 0; fieldIndex < 5; fieldIndex++)
                    {
                        string blockName;
                        string gradeName;
                        float gradeValue;

                        if (fieldIndex < topGrades.Count)
                        {
                            var grade = topGrades[fieldIndex];
                            var keyParts = grade.shortname.Split(' ');
                            blockName = keyParts[0];
                            gradeName = keyParts[1];
                            gradeValue = grade.amount;
                        }
                        else
                        {
                            blockName = "wall";
                            gradeName = "twigs";
                            gradeValue = 0f;
                        }

                        var offsetUp = columnFieldHeight * fieldIndex + columnFieldMarginY * fieldIndex +
                                       columnFieldMarginY +
                                       columnUpIndent;

                        var gradeLayer = CuiHelper.GetGuid();

                        container.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = HexToCuiColor("#38393F"),
                                Sprite = "assets/content/ui/ui.background.transparent.linearltr.tga",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "1 1", AnchorMax = "1 1",
                                OffsetMin = $"{columnLeftIndent} -{offsetUp + columnFieldHeight}",
                                OffsetMax = $"{columnLeftIndent + columnFieldWidth} -{offsetUp}"
                            }
                        }, LayerContent + ".Section.Building", gradeLayer);

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = (fieldIndex + 1).ToString(), Font = "robotocondensed-bold.ttf",
                                    FontSize = 24,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "15 0", OffsetMax = "45 0"}
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#CF432D"),
                                    Sprite = GetBuildingBlockImage(blockName)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                                    OffsetMin = "46 -34", OffsetMax = "114 34"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, "UI.Building." + blockName + "_" + gradeName),
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 18,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "146 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"{Mathf.CeilToInt(Mathf.Max(gradeValue / amount * 100f, 0))}%",
                                    Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleRight,
                                    Color = HexToCuiColor("#D74933")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "-186 0",
                                    OffsetMax = "-76 0"
                                }
                            }
                        });

                        #region Icon

                        var gradeItem = Instance.GetShortnameFromBuildingGrade(gradeName);
                        if (!string.IsNullOrEmpty(gradeItem))
                        {
                            container.Add(new CuiPanel
                            {
                                Image = {Color = HexToCuiColor("#D74933")},
                                RectTransform =
                                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-54 0", OffsetMax = "0 0"}
                            }, gradeLayer, gradeLayer + ".icon");

                            container.Add(new CuiElement
                            {
                                Parent = gradeLayer + ".icon",
                                Components =
                                {
                                    new CuiImageComponent
                                    {
                                        ItemId = ItemManager.FindItemDefinition(gradeItem)?.itemid ?? 0
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5 0.5",
                                        AnchorMax = "0.5 0.5",
                                        OffsetMin = "-19 -19",
                                        OffsetMax = "19 19"
                                    }
                                }
                            });
                        }

                        #endregion
                    }

                    #endregion Items
                }
                finally
                {
                    Pool.FreeUnmanaged(ref topGrades);
                }
            }

            private void DrawStatField(BasePlayer player, StatFieldConfig fieldConfig, PlayerStats stats,
                ref CuiElementContainer container,
                string oMin, string oMax, string aMin = "0 1", string aMax = "0 1")
            {
                #region Stat Field

                var statFieldLayer = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0"
                    },
                    RectTransform = {AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                }, LayerContent + ".Scroll", statFieldLayer);

                #endregion Stat Field

                #region Stat Field Icon

                if (!string.IsNullOrEmpty(fieldConfig.Icon))
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 50),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -24", OffsetMax = "48 24"}
                    }, statFieldLayer, statFieldLayer + ".Icon.Background");

                    var imageComponent = fieldConfig.Icon == "avatar"
                        ? new CuiRawImageComponent
                        {
                            SteamId = stats.UserId.ToString()
                        }
                        : new CuiRawImageComponent
                        {
                            Png = Instance.GetImage(fieldConfig.Icon)
                        };

                    container.Add(new CuiElement
                    {
                        Parent = statFieldLayer + ".Icon.Background",
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -24", OffsetMax = "24 24"
                            }
                        }
                    });
                }

                #endregion Stat Field Icon

                #region Stats

                var statSB = Pool.Get<StringBuilder>();
                try
                {
                    statSB.Append(Msg(player, "UI.Field." + fieldConfig.Title));

                    if (string.IsNullOrEmpty(fieldConfig.SecondPrefab))
                    {
                        statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                   stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.Prefab) +
                                                   "</b></color></size>");
                    }
                    else
                    {
                        statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                   stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.Prefab) +
                                                   "</b></color></size>");
                        statSB.Append('\n')
                            .Append(stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.SecondPrefab));
                    }

                    container.Add(new CuiElement
                    {
                        Parent = statFieldLayer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = statSB.ToString(),
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Color = HexToCuiColor("#E2DBD3", 50),
                                VerticalOverflow = VerticalWrapMode.Overflow
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "60 0", OffsetMax = "0 0"
                            }
                        }
                    });
                }
                finally
                {
                    Pool.FreeUnmanaged(ref statSB);
                }

                #endregion
            }

            #endregion Blocks
        }

        private class TemplateV2Renderer : ITemplateRenderer
        {
            public void Render(BasePlayer player, CuiElementContainer container)
            {
                #region Leaderboard

                #region Leaderboard Title

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Background",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIPluginTitle), Font = "robotocondensed-regular.ttf", FontSize = 32,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#CF432D", 90)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -70", OffsetMax = "-45 -20"}
                    }
                });

                #endregion Leaderboard Title

                #region Leaderboard line

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#373737", 50)},
                    RectTransform = {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "40 -71", OffsetMax = "-82 -69"}
                }, Layer + ".Background");

                #endregion Leaderboard line

                #endregion Leaderboard

                CategoriesSection(player, container);

                ContentSection(player, container);
            }

            public void CategoriesSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                var categories = Instance.GetAvailableCategories(player.UserIDString);
                try
                {
                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "40 -130", OffsetMax = "664 -90"}
                    }, Layer + ".Background", Layer + ".Categories", Layer + ".Categories");

                    var categoriesOffsetX = 0f;
                    var categoryWidth = 168f;
                    var categoryMarginX = 4f;

                    for (var i = 0; i < categories.Count; i++)
                    {
                        var category = categories[i];

                        container.Add(new CuiButton
                        {
                            Text =
                            {
                                Text = Msg(player, "UI.Category." + category.Title), Font = "robotocondensed-bold.ttf",
                                FontSize = 17,
                                Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            Button =
                            {
                                Command = $"UI_UltimateLeaderboard page {i}",
                                Color = openedLeaderboard.Page == i
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{categoriesOffsetX} -40",
                                OffsetMax = $"{categoriesOffsetX + categoryWidth} 0"
                            }
                        }, Layer + ".Categories");

                        categoriesOffsetX += categoryWidth + categoryMarginX;
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref categories);
                }
            }

            public void ContentSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "0 1",
                        OffsetMin = "40 0",
                        OffsetMax = "926 -140"
                    }
                }, Layer + ".Background", Layer + ".Main", Layer + ".Main");

                switch (openedLeaderboard.Page)
                {
                    case 0:
                    {
                        UserProfilePage(player, PlayerStats.Get(player.userID), container);
                        break;
                    }

                    case 1: // leaderboard
                    {
                        LeaderboardPage(player, container);
                        break;
                    }

                    case 2: // search
                    {
                        SearchPage(player, container);
                        break;
                    }
                }
            }

            public void UserProfilePage(BasePlayer player, PlayerStats stats, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                #region Statistics Section

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #endregion Statistics Section

                #region Tabs

                var tabs = Instance?.GetAvailableTabs(stats.UserId.ToString());
                try
                {
                    var tabOffsetX = 0;

                    // Tab buttons
                    var tabButtonWidth = 130;
                    var tabButtonHeight = 25;
                    var tabButtonMarginX = 5;

                    #region Tabs.Scroll

                    var tabsScrollWidth = tabs.Count * tabButtonHeight + (tabs.Count - 1) * tabButtonMarginX;

                    tabsScrollWidth = Mathf.Max(tabsScrollWidth, 886);

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Tabs",
                        Parent = LayerContent,
                        Components =
                        {
                            // new CuiImageComponent() { Color = "0 0 0 0" },
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = false,
                                Inertia = true,
                                Horizontal = true,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = new CuiRectTransform
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = $"{tabsScrollWidth} 0"
                                },
                                HorizontalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    AutoHide = true,
                                    HighlightColor = HexToCuiColor("#D74933"),
                                    HandleColor = HexToCuiColor("#D74933"),
                                    PressedColor = HexToCuiColor("#D74933"),
                                    TrackColor = HexToCuiColor("#373737")
                                }
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = "0 -35",
                                OffsetMax = "886 -5"
                            }
                        }
                    });

                    for (var tabIndex = 0; tabIndex < tabs.Count; tabIndex++)
                    {
                        var tab = tabs[tabIndex];

                        var leftTabOffset = tabOffsetX + tabIndex * (tabButtonWidth + tabButtonMarginX);

                        container.Add(new CuiButton
                        {
                            Button =
                            {
                                Command = $"UI_UltimateLeaderboard open_profile {stats.UserId} tab {tabIndex}",
                                Color = openedLeaderboard.SelectedTab == tabIndex
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            Text =
                            {
                                Text = Msg(player, "UI.Tabs." + tab.Name),
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 16,
                                Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{leftTabOffset} {-tabButtonHeight}",
                                OffsetMax = $"{leftTabOffset + tabButtonWidth} {0}"
                            }
                        }, LayerContent + ".Tabs");
                    }

                    #endregion Tabs.Scroll

                    #region Tabs.Blocks

                    #region Tabs.Blocks.Scroll

                    var scrollRect = new CuiRectTransform
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"0 {-4000}",
                        OffsetMax = "0 0"
                    };

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Scroll",
                        Parent = LayerContent,
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = true,
                                Inertia = true,
                                Horizontal = false,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = scrollRect,
                                VerticalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    AutoHide = true,
                                    HighlightColor = HexToCuiColor("#D74933"),
                                    HandleColor = HexToCuiColor("#D74933"),
                                    PressedColor = HexToCuiColor("#D74933"),
                                    TrackColor = HexToCuiColor("#373737")
                                }
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1",
                                OffsetMin = "0 -485", OffsetMax = "0 -40"
                            }
                        }
                    });

                    #endregion Tabs.Blocks.Scroll

                    #region Tabs.Blocks.Content

                    var targetTab = tabs[openedLeaderboard.SelectedTab];

                    var offsetY = 0f;
                    var blockMarginY = 20f;

                    for (var k = 0; k < targetTab.Blocks.Count; k++)
                    {
                        var block = targetTab.Blocks[k];

                        float totalHeight;
                        switch (block.BlockType)
                        {
                            case "Profile":
                                DrawProfileBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "Statistics":
                                DrawStatisticsBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "HitRate":
                                DrawHitRateBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "Building":
                                DrawBuildingBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            default:
                                totalHeight = 0f;
                                break;
                        }

                        offsetY = offsetY - totalHeight;

                        if (k != targetTab.Blocks.Count - 1)
                            offsetY = offsetY - blockMarginY;
                    }

                    offsetY = Mathf.Min(-435, offsetY);

                    scrollRect.OffsetMin = $"0 {offsetY}";

                    #endregion Tabs.Blocks.Content

                    #endregion Tabs.Blocks
                }
                finally
                {
                    Pool.FreeUnmanaged(ref tabs);
                }

                #endregion Tabs
            }

            private float leaderboardLeftIndent = 20f;

            public void LeaderboardPage(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                #region Statistics Section

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #endregion Statistics Section

                #region Awards

                var tableTopOffset = -110;

                if (_config.Awards.DisplayAwardsUI)
                    LeaderboardAwardsSection(player, container);
                else
                    tableTopOffset = 0;

                #endregion Awards

                #region Leaderboard

                #region Header

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"0 {tableTopOffset - 32}", OffsetMax = $"546 {tableTopOffset}"}
                }, LayerContent, LayerContent + ".Leaderboard.Header");

                tableTopOffset = tableTopOffset - 32;

                var offsetX = leaderboardLeftIndent;

                for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                {
                    var leaderboardColumn = _config.LeaderboardColumns[colIndex];

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} 0", OffsetMax = $"{offsetX + leaderboardColumn.Width} 0"
                        },
                        Text =
                        {
                            Text = Msg(player, "UI.Leaderboard.Column." + leaderboardColumn.Name),
                            Font = "robotocondensed-bold.ttf", FontSize = 12,
                            Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_UltimateLeaderboard leaderboard select_column " + colIndex
                        }
                    }, LayerContent + ".Leaderboard.Header");

                    if (colIndex + 1 != _config.LeaderboardColumns.Count)
                        container.Add(new CuiPanel
                        {
                            Image = {Color = HexToCuiColor("#E2DBD3", 20)},
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0 1",
                                OffsetMin = $"{offsetX + leaderboardColumn.Width - 1f} 0",
                                OffsetMax = $"{offsetX + leaderboardColumn.Width + 1f} 0"
                            }
                        }, LayerContent + ".Leaderboard.Header");

                    offsetX += leaderboardColumn.Width;
                }

                #endregion

                #region Scroll

                var leaderboardTotalWidth = leaderboardLeftIndent + _config.LeaderboardColumns.Sum(x => x.Width);

                leaderboardTotalWidth = Mathf.Max(leaderboardTotalWidth, 886);

                var playerHeight = 40f;
                var playerMarginY = 4f;


                var leaderboardTotalHeight = GetLeaderboardPlayersToShow() * playerHeight +
                                             (GetLeaderboardPlayersToShow() - 1) * playerMarginY;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Scroll",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = true,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = new CuiRectTransform
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"0 -{leaderboardTotalHeight}",
                                OffsetMax = $"{leaderboardTotalWidth} 0"
                            },
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            },
                            HorizontalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = "0 -445", OffsetMax = $"891 {tableTopOffset - 8}"
                        }
                    }
                });

                #endregion Scroll

                ShowLeaderboardTableUI(player, container);

                ShowLeaderboardPaginationUI(player, container);

                #endregion
            }

            public void LeaderboardAwardsSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Leaderboard.Awards",
                    DestroyUi = LayerContent + ".Leaderboard.Awards",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#000000", 0)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -90",
                            OffsetMax = "0 0"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Leaderboard.Awards",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.AwardsIcon)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -91",
                            OffsetMax = "98 -13"
                        }
                    }
                });

                #region Awards.Scroll

                var awardsTotalWidth = 765f;

                var awardScrollRect = new CuiRectTransform
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0 1",
                    OffsetMin = "0 0",
                    OffsetMax = $"{awardsTotalWidth} 0"
                };

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                    Parent = LayerContent + ".Leaderboard.Awards",
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = false,
                            Inertia = true,
                            Horizontal = true,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = awardScrollRect,
                            HorizontalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                Invert = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "120 0", OffsetMax = "0 0"
                        }
                    }
                });

                #region Title

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UITitleAwards), Font = "robotocondensed-regular.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -23",
                            OffsetMax = "200 5"
                        }
                    }
                });

                #endregion Title

                var awardCategories = Instance.GetAvailableAwardCategories(player.UserIDString);
                try
                {
                    if (awardCategories.Count > 0)
                    {
                    #region Tabs

                    var awardCategoryWidth = 105;
                    var awardCategoryMarginX = 5;

                    var awardCategoriesOffset = 100;

                    for (var awardIndex = 0; awardIndex < awardCategories.Count; awardIndex++)
                    {
                        var awardCategory = awardCategories[awardIndex];

                        var awardOffset = awardCategoriesOffset +
                                          awardIndex * (awardCategoryWidth + awardCategoryMarginX);

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{awardOffset} -20",
                                OffsetMax = $"{awardOffset + awardCategoryWidth} 0"
                            },
                            Text =
                            {
                                Text = Msg(player, "UI.Awards." + awardCategory.Title),
                                Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            Button =
                            {
                                Color = openedLeaderboard.awardTabSelected == awardIndex
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Command = $"UI_UltimateLeaderboard leaderboard award_tab {awardIndex}"
                            }
                        }, LayerContent + ".Leaderboard.Awards" + ".Scroll");
                    }

                    #endregion Tabs

                    #region List

                    var awardPlaceWidth = 210;
                    var awardPlaceMarginX = 5;
                    var awardPlacesOffset = 0;

                    var selectedAwardCategory = awardCategories[openedLeaderboard.awardTabSelected];

                    for (var i = 0; i < selectedAwardCategory.Places.Count; i++)
                    {
                        var place = selectedAwardCategory.Places[i];

                        var awardOffset = awardPlacesOffset + i * (awardPlaceWidth + awardPlaceMarginX);

                        var targetAwardLayer = LayerContent + ".Leaderboard.Awards" + ".Award" + i;

                        container.Add(new CuiElement
                        {
                            Name = targetAwardLayer,
                            Parent = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#696969", 15),
                                    Sprite = "assets/content/ui/ui.background.tile.psd",
                                    Material = "assets/content/ui/namefontmaterial.mat",
                                    ImageType = Image.Type.Tiled
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{awardOffset} -77",
                                    OffsetMax = $"{awardOffset + awardPlaceWidth} -27"
                                }
                            }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = targetAwardLayer,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = Instance.GetImage(place.Image)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5",
                                    AnchorMax = "0 0.5",
                                    OffsetMin = "5 -20",
                                    OffsetMax = "45 20"
                                }
                            }
                        });

                        #region titles

                        container.Add(new CuiElement
                        {
                            Parent = targetAwardLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlaceAwards, place.Place),
                                    Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "1 1",
                                    OffsetMin = "50 -19",
                                    OffsetMax = "0 0"
                                }
                            }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = targetAwardLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, "UI.Awards.Place." + place.Title),
                                    Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.UpperLeft,
                                    Color = HexToCuiColor("#E2DBD3", 70)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "50 0",
                                    OffsetMax = "0 -19"
                                }
                            }
                        });

                        #endregion titles
                    }

                    #region Calculate Scroll

                    var tabsTotalWidth = awardCategoriesOffset + awardCategories.Count * awardCategoryWidth +
                                         (awardCategories.Count - 1) * awardCategoryMarginX;

                    var awardPlacesTotalWidth = awardPlacesOffset +
                                                selectedAwardCategory.Places.Count * awardPlaceWidth +
                                                (selectedAwardCategory.Places.Count - 1) * awardPlaceMarginX;

                    awardsTotalWidth = Mathf.Max(awardsTotalWidth, tabsTotalWidth);

                    awardsTotalWidth = Mathf.Max(awardsTotalWidth, awardPlacesTotalWidth);

                    awardScrollRect.OffsetMax = $"{awardsTotalWidth} 0";

                    #endregion Calculate Scroll

                    #endregion List
                    
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref awardCategories);
                }

                #endregion Awards.Scroll
            }

            
            public void ShowLeaderboardPaginationUI(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;
                    
                #region Pagination

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Pages",
                    DestroyUi = LayerContent + ".Pagination.Pages",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#696969", 30)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-37 -485",
                            OffsetMax = "38 -455"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Pages",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text =
                                $"{openedLeaderboard.LeaderboardPage + 1} / {openedLeaderboard.GetMaxLeaderboardPage()}",
                            Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 70)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Next",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page next"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "44 -485",
                            OffsetMax = "76 -455"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Next",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonNext)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.End",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page end"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "82 -485",
                            OffsetMax = "114 -455"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.End",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonEnd)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Start",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page start"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-113 -485",
                            OffsetMax = "-81 -455"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Start",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonStart)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Back",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page back"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-75 -485",
                            OffsetMax = "-43 -455"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Back",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonBack)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });

                #endregion Pagination

            }
            public void ShowLeaderboardTableUI(BasePlayer player, CuiElementContainer leaderboardContainer)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                var playerHeight = 40f;
                var playerMarginY = 4f;

                var leaderboardPlayersOffset = openedLeaderboard.LeaderboardPage * GetLeaderboardPlayersToShow();

                var players =
                    Instance.Leaderboard?.GetLeaderboardUsers(leaderboardPlayersOffset, GetLeaderboardPlayersToShow(),
                        openedLeaderboard.GetSelectedColumn());
                try
                {
                    leaderboardContainer.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = "0 0 0 0"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = $"{leaderboardLeftIndent} 0", OffsetMax = "-5 0"
                            }
                        }, LayerContent + ".Scroll", LayerContent + ".Leaderboard.Table",
                        LayerContent + ".Leaderboard.Table");

                    if (!Instance.isLeaderboard)
                    {
                        leaderboardContainer.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text = 
                            {
                                Text = Msg(player, UILeaderboardLoading),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 30,
                                Color = HexToCuiColor("#FFFFFF", 50),
                            }
                        }, LayerContent + ".Leaderboard.Table");
                    }
                    else
                    {
                    var playersOffsetY = 0f;
                    for (var i = 0; i < players.Count; i++)
                    {
                        var topPlayer = players[i];

                        var topNumber = leaderboardPlayersOffset + i + 1;

                        var targetPanelColor = topNumber switch
                        {
                            1 => HexToCuiColor("#71B8ED", 25),
                            2 => HexToCuiColor("#71B8ED", 15),
                            3 => HexToCuiColor("#71B8ED", 10),
                            _ => topNumber % 2 == 0 ? HexToCuiColor("#696969", 10) : HexToCuiColor("#696969", 15)
                        };

                        var targetTopColor = topNumber switch
                        {
                            1 => HexToCuiColor("#FFFFFF"),
                            2 => HexToCuiColor("#FFFFFF", 70),
                            3 => HexToCuiColor("#FFFFFF", 50),
                            _ => HexToCuiColor("#E2DBD3", 20)
                        };

                        var targetTextColor = topNumber switch
                        {
                            1 => HexToCuiColor("#FFFFFF"),
                            _ => HexToCuiColor("#E2DBD3", 70)
                        };

                        #region Player Panel

                        var playerLayer = LayerContent + ".Scroll" + ".Player." + i;

                        leaderboardContainer.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = targetPanelColor,
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat",
                                ImageType = Image.Type.Tiled
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1",
                                OffsetMin = $"0 {playersOffsetY - playerHeight}",
                                OffsetMax = $"0 {playersOffsetY}"
                            }
                        }, LayerContent + ".Leaderboard.Table", playerLayer, playerLayer);

                        leaderboardContainer.Add(new CuiElement
                        {
                            Parent = playerLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = topNumber.ToString(),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleLeft, Color = targetTopColor
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "-20 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        var playersOffsetX = 0f;
                        for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                        {
                            var leaderboardColumn = _config.LeaderboardColumns[colIndex];

                            if (leaderboardColumn.Type == LootSettings.LootType.Custom &&
                                leaderboardColumn.Prefab == "nickname")
                            {
                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiRawImageComponent
                                        {
                                            SteamId = topPlayer.UserId.ToString()
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0.5",
                                            AnchorMax = "0 0.5",
                                            OffsetMin = $"{playersOffsetX + 6} -14",
                                            OffsetMax = $"{playersOffsetX + 34} 14"
                                        }
                                    }
                                });

                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiTextComponent
                                        {
                                            Text = topPlayer.LastName ?? string.Empty,
                                            Font = "robotocondensed-bold.ttf",
                                            FontSize = 12,
                                            Align = TextAnchor.MiddleLeft,
                                            Color = targetTextColor
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 1",
                                            OffsetMin = $"{playersOffsetX + 45} 0",
                                            OffsetMax = $"{playersOffsetX + leaderboardColumn.Width} 0"
                                        }
                                    }
                                });
                            }
                            else if (colIndex < topPlayer.leaderboardItems.Length)
                            {
                                var val = topPlayer.leaderboardItems[colIndex];

                                var statValue = leaderboardColumn.Type == LootSettings.LootType.Custom && leaderboardColumn.Prefab == "formatеed_total_playtime" ? Instance.GetPlaytimeLocalized(player, Convert.ToDouble(val)) : FormatLargeNumber(val);
                                
                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiTextComponent
                                        {
                                            Text = statValue ?? string.Empty,
                                            Font = "robotocondensed-bold.ttf", FontSize = 12,
                                            Align = TextAnchor.MiddleCenter,
                                            Color = targetTextColor
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 1",
                                            OffsetMin = $"{playersOffsetX} 0",
                                            OffsetMax = $"{playersOffsetX + leaderboardColumn.Width} 0"
                                        }
                                    }
                                });
                            }

                            if (colIndex + 1 != _config.LeaderboardColumns.Count)
                            {
                                leaderboardContainer.Add(new CuiPanel
                                {
                                    Image = {Color = HexToCuiColor("#E2DBD3", 10)},
                                    RectTransform =
                                    {
                                        AnchorMin = "0 0", AnchorMax = "0 1",
                                        OffsetMin = $"{playersOffsetX + leaderboardColumn.Width - 1f} 0",
                                        OffsetMax = $"{playersOffsetX + leaderboardColumn.Width + 1f} 0"
                                    }
                                }, playerLayer);

                                playersOffsetX += leaderboardColumn.Width;
                            }
                        }

                        #endregion Player Panel

                        #region Position

                        playersOffsetY = playersOffsetY - playerHeight - playerMarginY;

                        #endregion
                    }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }
            }

            public int GetLeaderboardPlayersToShow()
            {
                return 10;
            }

            public void SearchPage(BasePlayer player, CuiElementContainer container)
            {
                #region Statistics Section

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #endregion Statistics Section

                #region Search

                container.Add(new CuiElement
                {
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UISearchPlayersTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -40", OffsetMax = "0 0"}
                    }
                });

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = HexToCuiColor("#696969", 30), Sprite = "assets/content/ui/UI.Background.Tile.psd",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -80", OffsetMax = "294 -40"}
                }, LayerContent, LayerContent + ".Panel.Search");

                SearchInputField(player, container);

                #endregion

                SearchPlayersSection(player, container);
            }

            public void SearchInputField(BasePlayer player, CuiElementContainer container, bool update = false)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Panel.Search" + ",Input",
                    Parent = LayerContent + ".Panel.Search",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = !string.IsNullOrWhiteSpace(openedLeaderboard.search)
                                ? openedLeaderboard.search
                                : Msg(player, UISearchInputPlaceholder),
                            Font = "robotocondensed-bold.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 30),
                            NeedsKeyboard = true,
                            Command = "UI_UltimateLeaderboard search"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
                    }
                });
            }

            public void SearchPlayersSection(BasePlayer player, CuiElementContainer container)
            {
                #region Background

                var scrollRect = new CuiRectTransform
                {
                    AnchorMin = "0 1", AnchorMax = "1 1",
                    OffsetMin = $"0 {-1500}",
                    OffsetMax = "0 0"
                };

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Section.Players" + ".Scroll",
                    DestroyUi = LayerContent + ".Section.Players" + ".Scroll",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = false,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = scrollRect,
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1", AnchorMax = "1 1",
                            OffsetMin = "0 -485", OffsetMax = "0 -100"
                        }
                    }
                });

                #endregion

                #region Table

                var playerWidth = 222f;
                var playerHeight = 40f;
                var playersMarginX = 78f;
                var playersMarginY = 0f;

                var defaultIndentX = 0f;
                var defaultIndentY = -40f;

                var offsetX = defaultIndentX;
                var offsetY = defaultIndentY;

                var players = Pool.Get<List<(string displayName, string userID)>>();
                try
                {
                    Instance.PopulatePlayers(player.userID, ref players);

                    var maxColumns = 4;
                    var rows = Mathf.CeilToInt((float) players.Count / maxColumns);

                    var totalScrollHeight =
                        Mathf.Max(rows * playerHeight + (rows - 1) * playersMarginY + Mathf.Abs(defaultIndentY), 385);
                    scrollRect.OffsetMin = $"0 -{totalScrollHeight}";

                    if (players.Count == 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = LayerContent + ".Section.Players" + ".Scroll",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlayersNoPlayers),
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 40,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    VerticalOverflow = VerticalWrapMode.Overflow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1"
                                }
                            }
                        });
                    }
                    else
                    {
                        #region Title

                        container.Add(new CuiElement
                        {
                            Parent = LayerContent + ".Section.Players" + ".Scroll",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlayersListTitle), Font = "robotocondensed-bold.ttf",
                                    FontSize = 20, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -30", OffsetMax = "-40 0"}
                            }
                        });

                        #endregion

                        for (var i = 0; i < players.Count; i++)
                        {
                            var (displayName, userID) = players[i];

                            #region Panel

                            var targetColor =
                                (i + 1) % 2 == 0 ? HexToCuiColor("#696969", 10) : HexToCuiColor("#696969", 15);

                            var playerLayer = CuiHelper.GetGuid();

                            container.Add(new CuiElement
                            {
                                Name = playerLayer,
                                Parent = LayerContent + ".Section.Players" + ".Scroll",
                                Components =
                                {
                                    new CuiButtonComponent
                                    {
                                        Command = "UI_UltimateLeaderboard search_open_profile " + userID,
                                        Color = targetColor,
                                        Sprite = "assets/content/ui/ui.background.tile.psd",
                                        Material = "assets/content/ui/namefontmaterial.mat",
                                        ImageType = Image.Type.Tiled
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 1", AnchorMax = "0 1",
                                        OffsetMin = $"{offsetX} {offsetY - playerHeight}",
                                        OffsetMax = $"{offsetX + playerWidth} {offsetY}"
                                    }
                                }
                            });


                            container.Add(new CuiElement
                            {
                                Parent = playerLayer,
                                Components =
                                {
                                    new CuiRawImageComponent {SteamId = userID},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "6 -14",
                                        OffsetMax = "34 14"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = playerLayer,
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = displayName ?? "Unknown",
                                        Font = "robotocondensed-bold.ttf", FontSize = 12,
                                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 70)
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 0", OffsetMax = "0 0"
                                    }
                                }
                            });

                            #endregion

                            #region Position

                            if ((i + 1) % rows == 0)
                            {
                                offsetY = defaultIndentY;
                                offsetX = offsetX + playerWidth + playersMarginX;
                            }
                            else
                            {
                                offsetY = offsetY - playerHeight - playersMarginY;
                            }

                            #endregion
                        }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }

                #endregion
            }

            #region Blocks

            public void DrawProfileBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                #region Fields

                var fieldsOnRow = 3;
                var fieldWidth = 220f;
                var fieldHeight = 48f;
                var fieldMarginX = 100f;
                var fieldMarginY = 30f;
                var leftIndent = 10f;

                var columns = Mathf.Min(fieldsOnRow, block.StatFields.Count);
                var rows = Mathf.CeilToInt((float) block.StatFields.Count / fieldsOnRow);

                var maxWidth = columns * fieldWidth + (columns - 1) * fieldMarginX;
                var maxHeight = rows * fieldHeight + (rows - 1) * fieldMarginY + 20;

                #endregion Fields

                var localOffsetY = offsetY - 10f;

                var currentField = 0;
                for (var row = 0; row < rows; row++)
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 20),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} {localOffsetY - fieldHeight - 10}",
                            OffsetMax = $"{maxWidth} {localOffsetY + 10}"
                        }
                    }, LayerContent + ".Scroll");

                    var offsetX = leftIndent;
                    for (var col = 0; col < fieldsOnRow && currentField < block.StatFields.Count; col++)
                    {
                        DrawStatField(player,
                            block.StatFields[currentField],
                            stats,
                            ref container,
                            $"{offsetX} {localOffsetY - fieldHeight}",
                            $"{offsetX + fieldWidth} {localOffsetY}"
                        );

                        currentField++;

                        offsetX += fieldWidth + fieldMarginX;
                    }

                    localOffsetY = localOffsetY - fieldHeight - fieldMarginY;
                }

                totalHeight = maxHeight;
            }

            public void DrawStatisticsBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                var leftIndent = 0f;

                var columnFieldWidth = 168f;
                var columnFieldHeight = 68f;
                var columnFieldMarginX = 5f;
                var columnFieldMarginY = 5f;

                #region Draw Field resources

                void DrawStatGUI(string ID, string parent,
                    string oMin, string oMax,
                    StatFieldConfig statField)
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 20),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = oMin, OffsetMax = oMax
                        }
                    }, parent, ID);

                    #region Icon

                    var imageLayer = container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 50),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "10 -24", OffsetMax = "58 24"}
                    }, ID);

                    var def = !string.IsNullOrEmpty(statField.Icon)
                        ? null
                        : ItemManager.FindItemDefinition(statField.Prefab);
                    container.Add(new CuiElement
                    {
                        Parent = imageLayer,
                        Components =
                        {
                            def != null
                                ? new CuiImageComponent
                                {
                                    ItemId = def.itemid
                                }
                                : statField.Icon.StartsWith("assets/")
                                    ? new CuiRawImageComponent {Sprite = statField.Icon}
                                    : new CuiRawImageComponent {Png = Instance.GetImage(statField.Icon)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -24", OffsetMax = "24 24"
                            }
                        }
                    });

                    #endregion

                    #region Stats

                    var statSB = Pool.Get<StringBuilder>();
                    try
                    {
                        statSB.Append(Msg(player, "UI.Field." + statField.Title));

                        if (string.IsNullOrEmpty(statField.SecondPrefab))
                        {
                            statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                       stats.GetFormattedStatValue(player, statField.Type, statField.Prefab) +
                                                       "</b></color></size>");
                        }
                        else
                        {
                            statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                       stats.GetFormattedStatValue(player, statField.Type, statField.Prefab) +
                                                       "</b></color></size>");
                            statSB.Append('\n')
                                .Append(stats.GetFormattedStatValue(player, statField.Type, statField.SecondPrefab));
                        }

                        container.Add(new CuiElement
                        {
                            Parent = ID,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = statSB.ToString(),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    VerticalOverflow = VerticalWrapMode.Overflow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "70 0", OffsetMax = "-10 0"
                                }
                            }
                        });
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref statSB);
                    }

                    #endregion
                }

                #endregion

                var columnMarginY = 10f;
                var columnsOnRow = 5;

                var offsetX = leftIndent;

                totalHeight = 0;

                var localOffsetY = offsetY;

                for (var columnIndex = 0; columnIndex < block.Columns.Count; columnIndex++)
                {
                    var column = block.Columns[columnIndex];

                    var columnRows = Mathf.CeilToInt((float) column.StatFields.Count / columnsOnRow);
                    var rowsHeight = columnRows * columnFieldHeight + (columnRows - 1) * columnFieldMarginY;

                    var columnHeight = rowsHeight + 24f;
                    var columnWidth = columnsOnRow * columnFieldWidth + (columnsOnRow - 1) * columnFieldMarginX;

                    var columnLayer = LayerContent + $".Statistics.Section.{columnIndex}";

                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} {localOffsetY - columnHeight}",
                            OffsetMax = $"{offsetX + columnWidth} {localOffsetY}"
                        }
                    }, LayerContent + ".Scroll", columnLayer);

                    container.Add(new CuiElement
                    {
                        Parent = columnLayer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = column.Title, Font = "robotocondensed-bold.ttf", FontSize = 20,
                                Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"}
                        }
                    });

                    var fieldOffsetX = 0f;
                    var fieldOffsetY = -24f;

                    for (var fieldIndex = 0; fieldIndex < column.StatFields.Count; fieldIndex++)
                    {
                        var field = column.StatFields[fieldIndex];

                        DrawStatGUI(
                            $"{columnLayer} ({fieldIndex})",
                            columnLayer,
                            $"{fieldOffsetX} {fieldOffsetY - columnFieldHeight}",
                            $"{fieldOffsetX + columnFieldWidth} {fieldOffsetY}",
                            field);

                        if ((fieldIndex + 1) % columnsOnRow == 0 && fieldIndex + 1 != column.StatFields.Count)
                        {
                            fieldOffsetX = 0f;
                            fieldOffsetY = fieldOffsetY - columnFieldHeight - columnFieldMarginY;
                        }
                        else
                        {
                            fieldOffsetX += columnFieldWidth + columnFieldMarginX;
                        }
                    }

                    localOffsetY = localOffsetY - columnHeight - columnMarginY;

                    totalHeight += columnHeight + columnMarginY;
                }
            }

            public void DrawHitRateBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                var hitPointHeight = 435;

                #region Hit Point

                totalHeight = hitPointHeight;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                    {
                        AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"0 {offsetY - hitPointHeight}",
                        OffsetMax = $"886 {offsetY}"
                    }
                }, LayerContent + ".Scroll", LayerContent + ".Section.HitPoint");

                #region Fields

                var totalHits = stats.StatsStorage.GetTotal(LootSettings.LootType.BodyHits);

                var hitRateImageUrl = block.HitRateImages
                    .Find(hitRate => totalHits >= hitRate.MinHits && totalHits <= hitRate.MaxHits)?.Image;

                #endregion

                #region Title

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Section.HitPoint",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIHitRateTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#FFFFFF")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "367 0"}
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Section.HitPoint",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIHitRateDescription), Font = "robotocondensed-regular.ttf",
                            FontSize = 14, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -70", OffsetMax = "367 -30"}
                    }
                });

                #endregion

                #region Image

                if (!string.IsNullOrEmpty(hitRateImageUrl))
                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Section.HitPoint" + ".Image",
                        Parent = LayerContent + ".Section.HitPoint",
                        Components =
                        {
                            new CuiRawImageComponent {Png = Instance.GetImage(hitRateImageUrl)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-293 0", OffsetMax = "293 330"}
                        }
                    });

                #endregion

                #region Body Parts

                AddBodyPart("59 109.8", "139 129.8", "head", "HEAD");
                AddBodyPart("127 54", "207 74", "left arm|right arm", "ARM");
                AddBodyPart("80 -37", "160 -17", "chest", "CHEST");
                AddBodyPart("95 -139.5", "175 -119.5", "hip", "HIP");
                AddBodyPart("-237 -23.5", "-157 -3.5", "body", "BODY");
                AddBodyPart("-227 77.5", "-147 97.5", "neck", "NECK");

                void AddBodyPart(string offMin, string offMax, string key, string title = null)
                {
                    if (string.IsNullOrEmpty(title)) title = key;

                    if (!stats.StatsStorage.TryGetItem(LootSettings.LootType.BodyHits, key, out var value)) value = 0f;

                    var rect = new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = offMin, OffsetMax = offMax};

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.HitPoint" + ".Image",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, "UI.BodyParts." + title), Font = "robotocondensed-regular.ttf",
                                FontSize = 10, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF")
                            },
                            rect
                        }
                    });

                    var percent = value > 0 ? Mathf.CeilToInt(value / totalHits * 100f) : 0;

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.HitPoint" + ".Image",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = percent + "%", Font = "robotocondensed-regular.ttf", FontSize = 10,
                                Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#D74933")
                            },
                            rect
                        }
                    });
                }

                #endregion

                #endregion
            }

            public void DrawBuildingBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container,
                ref float offsetY, out float totalHeight)
            {
                totalHeight = 435;

                var topGrades = stats.StatsStorage.GetUpgrades();
                try
                {
                    var amount = topGrades.Sum(grade => grade.amount);

                    var bannerKey = "grade_banner_twigs";

                    if (topGrades.Count > 0)
                    {
                        var topGradeName = topGrades[0].shortname.Split(' ')[1];

                        bannerKey = $"grade_banner_{topGradeName}";
                    }

                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"0 {offsetY - totalHeight}",
                            OffsetMax = $"886 {offsetY}"
                        }
                    }, LayerContent + ".Scroll", LayerContent + ".Section.Building");

                    #region Titles

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, UIBuildingTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                                Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#FFFFFF")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "367 0"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, UIBuildingDescription),
                                Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft,
                                Color = HexToCuiColor("#E2DBD3")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -70", OffsetMax = "367 -30"}
                        }
                    });

                    #endregion Titles

                    #region Top Banner

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Section.Building" + ".Banner",
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiRawImageComponent {Png = Instance.GetImage(bannerKey)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "525 367"}
                        }
                    });

                    #endregion Top Banner

                    #region Items

                    var columnUpIndent = 15f;
                    var columnLeftIndent = 408f;
                    var columnFieldWidth = 460f;
                    var columnFieldHeight = 54f;
                    var columnFieldMarginY = 22f;

                    for (var fieldIndex = 0; fieldIndex < 5; fieldIndex++)
                    {
                        string blockName;
                        string gradeName;
                        float gradeValue;

                        if (fieldIndex < topGrades.Count)
                        {
                            var grade = topGrades[fieldIndex];
                            var keyParts = grade.shortname.Split(' ');
                            blockName = keyParts[0];
                            gradeName = keyParts[1];
                            gradeValue = grade.amount;
                        }
                        else
                        {
                            blockName = "wall";
                            gradeName = "twigs";
                            gradeValue = 0f;
                        }

                        var offsetUp = columnFieldHeight * fieldIndex + columnFieldMarginY * fieldIndex +
                                       columnFieldMarginY +
                                       columnUpIndent;

                        var gradeLayer = CuiHelper.GetGuid();

                        container.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = HexToCuiColor("#38393F"),
                                Sprite = "assets/content/ui/ui.background.transparent.linearltr.tga",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1",
                                OffsetMin = $"{columnLeftIndent} -{offsetUp + columnFieldHeight}",
                                OffsetMax = $"{columnLeftIndent + columnFieldWidth} -{offsetUp}"
                            }
                        }, LayerContent + ".Section.Building", gradeLayer);

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = (fieldIndex + 1).ToString(), Font = "robotocondensed-bold.ttf",
                                    FontSize = 24,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "15 0", OffsetMax = "45 0"}
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#CF432D"),
                                    Sprite = $"assets/prefabs/building core/{blockName}/{blockName}.png"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                                    OffsetMin = "46 -34", OffsetMax = "114 34"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, "UI.Building." + blockName + "_" + gradeName),
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 18,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "146 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"{Mathf.CeilToInt(Mathf.Max(gradeValue / amount * 100f, 0))}%",
                                    Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.UpperLeft,
                                    Color = HexToCuiColor("#D74933")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "358.7 0.7",
                                    OffsetMax = "398.7 40.7"
                                }
                            }
                        });

                        #region Icon

                        var gradeItem = Instance.GetShortnameFromBuildingGrade(gradeName);
                        if (!string.IsNullOrEmpty(gradeItem))
                        {
                            container.Add(new CuiPanel
                            {
                                Image = {Color = HexToCuiColor("#D74933")},
                                RectTransform =
                                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-54 0", OffsetMax = "0 0"}
                            }, gradeLayer, gradeLayer + ".icon");

                            container.Add(new CuiElement
                            {
                                Parent = gradeLayer + ".icon",
                                Components =
                                {
                                    new CuiImageComponent
                                    {
                                        ItemId = ItemManager.FindItemDefinition(gradeItem)?.itemid ?? 0
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5 0.5",
                                        AnchorMax = "0.5 0.5",
                                        OffsetMin = "-19 -19",
                                        OffsetMax = "19 19"
                                    }
                                }
                            });
                        }

                        #endregion
                    }

                    #endregion Items
                }
                finally
                {
                    Pool.FreeUnmanaged(ref topGrades);
                }
            }

            private void DrawStatField(BasePlayer player, StatFieldConfig fieldConfig, PlayerStats stats,
                ref CuiElementContainer container,
                string oMin, string oMax, string aMin = "0 1", string aMax = "0 1")
            {
                #region Stat Field

                var statFieldLayer = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0"
                    },
                    RectTransform = {AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                }, LayerContent + ".Scroll", statFieldLayer);

                #endregion Stat Field

                #region Stat Field Icon

                if (!string.IsNullOrEmpty(fieldConfig.Icon))
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 50),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -24", OffsetMax = "48 24"}
                    }, statFieldLayer, statFieldLayer + ".Icon.Background");

                    var imageComponent = fieldConfig.Icon == "avatar"
                        ? new CuiRawImageComponent
                        {
                            SteamId = stats.UserId.ToString()
                        }
                        : new CuiRawImageComponent
                        {
                            Png = Instance.GetImage(fieldConfig.Icon)
                        };

                    container.Add(new CuiElement
                    {
                        Parent = statFieldLayer + ".Icon.Background",
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -24", OffsetMax = "24 24"
                            }
                        }
                    });
                }

                #endregion Stat Field Icon

                #region Stats

                var statSB = Pool.Get<StringBuilder>();
                try
                {
                    statSB.Append(Msg(player, "UI.Field." + fieldConfig.Title));

                    if (string.IsNullOrEmpty(fieldConfig.SecondPrefab))
                    {
                        statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                   stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.Prefab) +
                                                   "</b></color></size>");
                    }
                    else
                    {
                        statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                   stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.Prefab) +
                                                   "</b></color></size>");
                        statSB.Append('\n')
                            .Append(stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.SecondPrefab));
                    }

                    container.Add(new CuiElement
                    {
                        Parent = statFieldLayer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = statSB.ToString(),
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Color = HexToCuiColor("#E2DBD3", 50),
                                VerticalOverflow = VerticalWrapMode.Overflow
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "60 0", OffsetMax = "0 0"
                            }
                        }
                    });
                }
                finally
                {
                    Pool.FreeUnmanaged(ref statSB);
                }

                #endregion
            }

            #endregion Blocks
        }

        private class TemplateFullscreenRenderer : ITemplateRenderer
        {
            public void Render(BasePlayer player, CuiElementContainer container)
            {
                BackgroundSection(player, container);

                MainPanel(player, container);

                HeaderSection(player, container);

                CategoriesSection(player, container);

                ContentSection(player, container);
            }

            public void BackgroundSection(BasePlayer player, CuiElementContainer container)
            {
                container.Add(new CuiElement
                {
                    Parent = "Overlay",
                    Name = Layer,
                    DestroyUi = Layer,
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Color = HexToCuiColor("#191919", 90),
                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                            Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat",
                            ImageType = Image.Type.Tiled
                        },
                        new CuiRectTransformComponent(),
                        new CuiNeedsCursorComponent(),
                        new CuiNeedsKeyboardComponent()
                    }
                });
            }

            public void MainPanel(BasePlayer player, CuiElementContainer container)
            {
                container.Add(new CuiPanel
                {
                    RectTransform =
                    {
                        AnchorMin = "0.5 0.5",
                        AnchorMax = "0.5 0.5",
                        OffsetMin = "-600 -300",
                        OffsetMax = "600 300"
                    },
                    Image =
                    {
                        Color = HexToCuiColor("#191919", 50),
                        Material = "assets/content/ui/uibackgroundblur.mat"
                    }
                }, Layer, Layer + ".Background", Layer + ".Background");
            }

            public void HeaderSection(BasePlayer player, CuiElementContainer container)
            {
                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = HexToCuiColor("#494949"),
                        Sprite = "assets/content/ui/UI.Background.Transparent.LinearLTR.tga"
                    },
                    RectTransform =
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = "0 -50",
                        OffsetMax = "-50 0"
                    }
                }, Layer + ".Background", Layer + ".Header", Layer + ".Header");

                container.Add(new CuiLabel
                {
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "20 0",
                        OffsetMax = "0 0"
                    },
                    Text =
                    {
                        Text = Msg(player, UIPluginTitle),
                        Font = "robotocondensed-bold.ttf",
                        FontSize = 18,
                        Align = TextAnchor.MiddleLeft,
                        Color = HexToCuiColor("#E2DBD3")
                    }
                }, Layer + ".Header");

                #region Close Button

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Header",
                    Name = Layer + ".Header" + ".CloseButton",
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#E44028"),
                            Sprite = "assets/content/ui/UI.Background.TileTex.psd",
                            Close = Layer,
                            Command = "UI_UltimateLeaderboard close"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "1 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -50",
                            OffsetMax = "50 0"
                        }
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = Layer + ".Header" + ".CloseButton",
                    Components =
                    {
                        new CuiImageComponent
                        {
                            Sprite = "assets/icons/close.png"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-9 -9",
                            OffsetMax = "9 9"
                        }
                    }
                });

                #endregion Close Button
            }
            
            public void CategoriesSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                var categories = Instance.GetAvailableCategories(player.UserIDString);
                try
                {
                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0.5 1",
                            AnchorMax = "0.5 1",
                            OffsetMin = "-580 -105",
                            OffsetMax = "580 -70"
                        }
                    }, Layer + ".Background", Layer + ".Categories", Layer + ".Categories");

                    var categoriesOffsetX = 0f;
                    var categoryWidth = 168f;
                    var categoryMarginX = 8f;

                    for (var i = 0; i < categories.Count; i++)
                    {
                        var category = categories[i];

                        container.Add(new CuiButton
                        {
                            Text =
                            {
                                Text = category.Title, Font = "robotocondensed-bold.ttf", FontSize = 17,
                                Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            Button =
                            {
                                Command = $"UI_UltimateLeaderboard page {i}",
                                Color = openedLeaderboard.Page == i
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = $"{categoriesOffsetX} -40",
                                OffsetMax = $"{categoriesOffsetX + categoryWidth} 0"
                            }
                        }, Layer + ".Categories");

                        categoriesOffsetX += categoryWidth + categoryMarginX;
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref categories);
                }
            }

            public void ContentSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                    {
                        AnchorMin = "0 0",
                        AnchorMax = "1 1",
                        OffsetMin = "20 0",
                        OffsetMax = "-20 -115"
                    }
                }, Layer + ".Background", Layer + ".Main", Layer + ".Main");

                switch (openedLeaderboard.Page)
                {
                    case 0:
                    {
                        UserProfilePage(player, PlayerStats.Get(player.userID), container);
                        break;
                    }

                    case 1: // leaderboard
                    {
                        LeaderboardPage(player, container);
                        break;
                    }

                    case 2: // search
                    {
                        SearchPage(player, container);
                        break;
                    }
                }
            }

            public void UserProfilePage(BasePlayer player, PlayerStats stats, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #region Tabs

                var tabs = Instance?.GetAvailableTabs(stats.UserId.ToString());
                try
                {
                    var tabOffsetX = 0;

                    // Tab buttons
                    var tabButtonWidth = 130;
                    var tabButtonHeight = 25;
                    var tabButtonMarginX = 5;

                    #region Tabs.Scroll

                    var tabsScrollWidth = tabs.Count * tabButtonHeight + (tabs.Count - 1) * tabButtonMarginX;

                    tabsScrollWidth = Mathf.Max(tabsScrollWidth, 1160);

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Tabs",
                        Parent = LayerContent,
                        Components =
                        {
                            // new CuiImageComponent() { Color = "0 0 0 0" },
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = false,
                                Inertia = true,
                                Horizontal = true,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = new CuiRectTransform
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 1",
                                    OffsetMin = "0 0",
                                    OffsetMax = $"{tabsScrollWidth} 0"
                                },
                                HorizontalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    AutoHide = true,
                                    HighlightColor = HexToCuiColor("#D74933"),
                                    HandleColor = HexToCuiColor("#D74933"),
                                    PressedColor = HexToCuiColor("#D74933"),
                                    TrackColor = HexToCuiColor("#373737")
                                }
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 1",
                                AnchorMax = "0.5 1",
                                OffsetMin = "-580 -35",
                                OffsetMax = "580 -5"
                            }
                        }
                    });

                    for (var tabIndex = 0; tabIndex < tabs.Count; tabIndex++)
                    {
                        var tab = tabs[tabIndex];

                        var leftTabOffset = tabOffsetX + tabIndex * (tabButtonWidth + tabButtonMarginX);

                        container.Add(new CuiButton
                        {
                            Button =
                            {
                                Command = $"UI_UltimateLeaderboard open_profile {stats.UserId} tab {tabIndex}",
                                Color = openedLeaderboard.SelectedTab == tabIndex
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            Text =
                            {
                                Text = Msg(player, "UI.Tabs." + tab.Name),
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 16,
                                Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{leftTabOffset} {-tabButtonHeight}",
                                OffsetMax = $"{leftTabOffset + tabButtonWidth} {0}"
                            }
                        }, LayerContent + ".Tabs");
                    }

                    #endregion Tabs.Scroll

                    #region Tabs.Blocks

                    #region Tabs.Blocks.Scroll

                    var scrollRect = new CuiRectTransform
                    {
                        AnchorMin = "0 1",
                        AnchorMax = "1 1",
                        OffsetMin = $"0 {-4000}",
                        OffsetMax = "0 0"
                    };

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Scroll",
                        Parent = LayerContent,
                        Components =
                        {
                            new CuiImageComponent {Color = "0 0 0 0"},
                            new CuiScrollViewComponent
                            {
                                MovementType = ScrollRect.MovementType.Clamped,
                                Vertical = true,
                                Inertia = true,
                                Horizontal = false,
                                Elasticity = 0.25f,
                                DecelerationRate = 0.3f,
                                ScrollSensitivity = 24f,
                                ContentTransform = scrollRect,
                                VerticalScrollbar = new CuiScrollbar
                                {
                                    Size = 3,
                                    AutoHide = true,
                                    HighlightColor = HexToCuiColor("#D74933"),
                                    HandleColor = HexToCuiColor("#D74933"),
                                    PressedColor = HexToCuiColor("#D74933"),
                                    TrackColor = HexToCuiColor("#373737")
                                }
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0",
                                AnchorMax = "0.5 1",
                                OffsetMin = "-580 0",
                                OffsetMax = "580 -50"
                            }
                        }
                    });

                    #endregion Tabs.Blocks.Scroll

                    #region Tabs.Blocks.Content

                    var targetTab = tabs[openedLeaderboard.SelectedTab];

                    var offsetY = 0f;
                    var blockMarginY = 20f;

                    for (var k = 0; k < targetTab.Blocks.Count; k++)
                    {
                        var block = targetTab.Blocks[k];

                        float totalHeight;
                        switch (block.BlockType)
                        {
                            case "Profile":
                                DrawProfileBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "Statistics":
                                DrawStatisticsBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "HitRate":
                                DrawHitRateBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            case "Building":
                                DrawBuildingBlock(player, block, stats, container, ref offsetY, out totalHeight);
                                break;
                            default:
                                totalHeight = 0f;
                                break;
                        }

                        offsetY = offsetY - totalHeight;

                        if (k != targetTab.Blocks.Count - 1) offsetY = offsetY - blockMarginY;
                    }

                    offsetY = Mathf.Min(-435, offsetY);

                    scrollRect.OffsetMin = $"0 {offsetY}";

                    #endregion Tabs.Blocks.Content

                    #endregion Tabs.Blocks
                }
                finally
                {
                    Pool.FreeUnmanaged(ref tabs);
                }

                #endregion Tabs
            }

            private float leaderboardLeftIndent = 20f;

            public void LeaderboardPage(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #region Awards

                var tableTopOffset = -100;

                if (_config.Awards.DisplayAwardsUI)
                    LeaderboardAwardsSection(player, container);
                else
                    tableTopOffset = -10;

                #endregion Awards

                #region Leaderboard

                #region Header

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                        {AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-580 {tableTopOffset - 32}", OffsetMax = $"580 {tableTopOffset}"}
                }, LayerContent, LayerContent + ".Leaderboard.Header");

                tableTopOffset = tableTopOffset - 32;

                var offsetX = leaderboardLeftIndent;

                for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                {
                    var leaderboardColumn = _config.LeaderboardColumns[colIndex];

                    container.Add(new CuiButton
                    {
                        RectTransform =
                        {
                            AnchorMin = "0 0", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} 0", OffsetMax = $"{offsetX + leaderboardColumn.Width} 0"
                        },
                        Text =
                        {
                            Text = Msg(player, "UI.Leaderboard.Column." + leaderboardColumn.Name),
                            Font = "robotocondensed-bold.ttf", FontSize = 12,
                            Align = TextAnchor.MiddleCenter, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        Button =
                        {
                            Color = "0 0 0 0",
                            Command = "UI_UltimateLeaderboard leaderboard select_column " + colIndex
                        }
                    }, LayerContent + ".Leaderboard.Header");

                    if (colIndex + 1 != _config.LeaderboardColumns.Count)
                        container.Add(new CuiPanel
                        {
                            Image = {Color = HexToCuiColor("#E2DBD3", 20)},
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "0 1",
                                OffsetMin = $"{offsetX + leaderboardColumn.Width - 1f} 0",
                                OffsetMax = $"{offsetX + leaderboardColumn.Width + 1f} 0"
                            }
                        }, LayerContent + ".Leaderboard.Header");

                    offsetX += leaderboardColumn.Width;
                }

                #endregion

                #region Scroll

                var leaderboardTotalWidth = leaderboardLeftIndent + _config.LeaderboardColumns.Sum(x => x.Width);

                leaderboardTotalWidth = Mathf.Max(leaderboardTotalWidth, 1160);

                var playerHeight = 40f;
                var playerMarginY = 4f;

                var leaderboardTotalHeight = GetLeaderboardPlayersToShow() * playerHeight +
                                             (GetLeaderboardPlayersToShow() - 1) * playerMarginY;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Scroll",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = true,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = new CuiRectTransform
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"0 -{leaderboardTotalHeight}",
                                OffsetMax = $"{leaderboardTotalWidth} 0"
                            },
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            },
                            HorizontalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0", AnchorMax = "0.5 1",
                            OffsetMin = "-580 40", OffsetMax = $"585 {tableTopOffset - 8}"
                        }
                    }
                });

                #endregion Scroll

                ShowLeaderboardTableUI(player, container);

                ShowLeaderboardPaginationUI(player, container);

                #endregion
            }

            public void LeaderboardAwardsSection(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Leaderboard.Awards",
                    DestroyUi = LayerContent + ".Leaderboard.Awards",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#000000", 0)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "1 1",
                            OffsetMin = "0 -100",
                            OffsetMax = "0 -10"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Leaderboard.Awards",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.AwardsIcon)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -91",
                            OffsetMax = "98 -13"
                        }
                    }
                });

                #region Awards.Scroll

                var awardsTotalWidth = 765f;

                var awardScrollRect = new CuiRectTransform
                {
                    AnchorMin = "0 0",
                    AnchorMax = "0 1",
                    OffsetMin = "0 0",
                    OffsetMax = $"{awardsTotalWidth} 0"
                };

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                    Parent = LayerContent + ".Leaderboard.Awards",
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = false,
                            Inertia = true,
                            Horizontal = true,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = awardScrollRect,
                            HorizontalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                Invert = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1",
                            OffsetMin = "120 0", OffsetMax = "0 0"
                        }
                    }
                });

                #region Title

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UITitleAwards), Font = "robotocondensed-regular.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 50)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = "0 -23",
                            OffsetMax = "200 5"
                        }
                    }
                });

                #endregion Title

                var awardCategories = Instance.GetAvailableAwardCategories(player.UserIDString);
                try
                {
                    if (awardCategories.Count > 0)
                    {

                    #region Tabs

                    var awardCategoryWidth = 105;
                    var awardCategoryMarginX = 5;

                    var awardCategoriesOffset = 100;

                    for (var awardIndex = 0; awardIndex < awardCategories.Count; awardIndex++)
                    {
                        var awardCategory = awardCategories[awardIndex];

                        var awardOffset = awardCategoriesOffset +
                                          awardIndex * (awardCategoryWidth + awardCategoryMarginX);

                        container.Add(new CuiButton
                        {
                            RectTransform =
                            {
                                AnchorMin = "0 1",
                                AnchorMax = "0 1",
                                OffsetMin = $"{awardOffset} -20",
                                OffsetMax = $"{awardOffset + awardCategoryWidth} 0"
                            },
                            Text =
                            {
                                Text = Msg(player, "UI.Awards." + awardCategory.Title),
                                Font = "robotocondensed-bold.ttf", FontSize = 10, Align = TextAnchor.MiddleCenter,
                                Color = HexToCuiColor("#E2DBD3", 90)
                            },
                            Button =
                            {
                                Color = openedLeaderboard.awardTabSelected == awardIndex
                                    ? HexToCuiColor("#D74933")
                                    : HexToCuiColor("#2F2F2F"),
                                Command = $"UI_UltimateLeaderboard leaderboard award_tab {awardIndex}"
                            }
                        }, LayerContent + ".Leaderboard.Awards" + ".Scroll");
                    }

                    #endregion Tabs

                    #region List

                    var awardPlaceWidth = 210;
                    var awardPlaceMarginX = 5;
                    var awardPlacesOffset = 0;

                    var selectedAwardCategory = awardCategories[openedLeaderboard.awardTabSelected];

                    for (var i = 0; i < selectedAwardCategory.Places.Count; i++)
                    {
                        var place = selectedAwardCategory.Places[i];

                        var awardOffset = awardPlacesOffset + i * (awardPlaceWidth + awardPlaceMarginX);

                        var targetAwardLayer = LayerContent + ".Leaderboard.Awards" + ".Award" + i;

                        container.Add(new CuiElement
                        {
                            Name = targetAwardLayer,
                            Parent = LayerContent + ".Leaderboard.Awards" + ".Scroll",
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#696969", 15),
                                    Sprite = "assets/content/ui/ui.background.tile.psd",
                                    Material = "assets/content/ui/namefontmaterial.mat",
                                    ImageType = Image.Type.Tiled
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "0 1",
                                    OffsetMin = $"{awardOffset} -77",
                                    OffsetMax = $"{awardOffset + awardPlaceWidth} -27"
                                }
                            }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = targetAwardLayer,
                            Components =
                            {
                                new CuiRawImageComponent
                                {
                                    Png = Instance.GetImage(place.Image)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5",
                                    AnchorMax = "0 0.5",
                                    OffsetMin = "5 -20",
                                    OffsetMax = "45 20"
                                }
                            }
                        });

                        #region titles

                        container.Add(new CuiElement
                        {
                            Parent = targetAwardLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlaceAwards, place.Place),
                                    Font = "robotocondensed-regular.ttf", FontSize = 12, Align = TextAnchor.LowerLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 1",
                                    AnchorMax = "1 1",
                                    OffsetMin = "50 -19",
                                    OffsetMax = "0 0"
                                }
                            }
                        });
                        container.Add(new CuiElement
                        {
                            Parent = targetAwardLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, "UI.Awards.Place." + place.Title),
                                    Font = "robotocondensed-bold.ttf", FontSize = 13, Align = TextAnchor.UpperLeft,
                                    Color = HexToCuiColor("#E2DBD3", 70)
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "50 0",
                                    OffsetMax = "0 -19"
                                }
                            }
                        });

                        #endregion titles
                    }

                    #region Calculate Scroll

                    var tabsTotalWidth = awardCategoriesOffset + awardCategories.Count * awardCategoryWidth +
                                         (awardCategories.Count - 1) * awardCategoryMarginX;

                    var awardPlacesTotalWidth = awardPlacesOffset +
                                                selectedAwardCategory.Places.Count * awardPlaceWidth +
                                                (selectedAwardCategory.Places.Count - 1) * awardPlaceMarginX;

                    awardsTotalWidth = Mathf.Max(awardsTotalWidth, tabsTotalWidth);

                    awardsTotalWidth = Mathf.Max(awardsTotalWidth, awardPlacesTotalWidth);

                    awardScrollRect.OffsetMax = $"{awardsTotalWidth} 0";

                    #endregion Calculate Scroll

                    #endregion List
                    
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref awardCategories);
                }

                #endregion Awards.Scroll
            }

            public void ShowLeaderboardPaginationUI(BasePlayer player, CuiElementContainer container)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                #region Pagination

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Pages",
                    DestroyUi = LayerContent + ".Pagination.Pages",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = HexToCuiColor("#696969", 30)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "-37 5",
                            OffsetMax = "38 35"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Pages",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text =
                                $"{openedLeaderboard.LeaderboardPage + 1} / {openedLeaderboard.GetMaxLeaderboardPage()}",
                            Font = "robotocondensed-bold.ttf", FontSize = 14, Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 70)
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0",
                            AnchorMax = "1 1",
                            OffsetMin = "0 0",
                            OffsetMax = "0 0"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Next",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page next"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "44 5",
                            OffsetMax = "76 35"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Next",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonNext)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.End",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page end"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "82 5",
                            OffsetMax = "114 35"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.End",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonEnd)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Start",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page start"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "-113 5",
                            OffsetMax = "-81 35"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Start",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonStart)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Pagination.Back",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiButtonComponent
                        {
                            Color = HexToCuiColor("#696969", 30),
                            Command = "UI_UltimateLeaderboard leaderboard page back"
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0",
                            AnchorMax = "0.5 0",
                            OffsetMin = "-75 5",
                            OffsetMax = "-43 35"
                        }
                    }
                });
                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Pagination.Back",
                    Components =
                    {
                        new CuiRawImageComponent {Png = Instance.GetImage(_uiSettings.PaginationButtonBack)},
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0.5 0.5",
                            AnchorMax = "0.5 0.5",
                            OffsetMin = "-16 -16",
                            OffsetMax = "16 16"
                        }
                    }
                });

                #endregion Pagination
            }

            public void ShowLeaderboardTableUI(BasePlayer player, CuiElementContainer leaderboardContainer)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard))
                    return;

                var playerHeight = 40f;
                var playerMarginY = 4f;

                var leaderboardPlayersOffset = openedLeaderboard.LeaderboardPage * GetLeaderboardPlayersToShow();

                var players =
                    Instance.Leaderboard?.GetLeaderboardUsers(leaderboardPlayersOffset, GetLeaderboardPlayersToShow(),
                        openedLeaderboard.GetSelectedColumn());
                try
                {
                    leaderboardContainer.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = "0 0 0 0"
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = $"{leaderboardLeftIndent} 0", OffsetMax = "-5 0"
                            }
                        }, LayerContent + ".Scroll", LayerContent + ".Leaderboard.Table",
                        LayerContent + ".Leaderboard.Table");
                    
                    if (!Instance.isLeaderboard)
                    {
                        leaderboardContainer.Add(new CuiLabel
                        {
                            RectTransform = { AnchorMin = "0 0", AnchorMax = "1 1" },
                            Text = 
                            {
                                Text = Msg(player, UILeaderboardLoading),
                                Align = TextAnchor.MiddleCenter,
                                Font = "robotocondensed-bold.ttf",
                                FontSize = 30,
                                Color = HexToCuiColor("#FFFFFF", 50),
                            }
                        }, LayerContent + ".Leaderboard.Table");
                    }
                    else
                    {
                    var playersOffsetY = 0f;
                    for (var i = 0; i < players.Count; i++)
                    {
                        var topPlayer = players[i];

                        var topNumber = leaderboardPlayersOffset + i + 1;

                        var targetPanelColor = topNumber switch
                        {
                            1 => HexToCuiColor("#71B8ED", 25),
                            2 => HexToCuiColor("#71B8ED", 15),
                            3 => HexToCuiColor("#71B8ED", 10),
                            _ => topNumber % 2 == 0 ? HexToCuiColor("#696969", 10) : HexToCuiColor("#696969", 15)
                        };

                        var targetTopColor = topNumber switch
                        {
                            1 => HexToCuiColor("#FFFFFF"),
                            2 => HexToCuiColor("#FFFFFF", 70),
                            3 => HexToCuiColor("#FFFFFF", 50),
                            _ => HexToCuiColor("#E2DBD3", 20)
                        };

                        var targetTextColor = topNumber switch
                        {
                            1 => HexToCuiColor("#FFFFFF"),
                            _ => HexToCuiColor("#E2DBD3", 70)
                        };

                        #region Player Panel

                        var playerLayer = LayerContent + ".Scroll" + ".Player." + i;

                        leaderboardContainer.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = targetPanelColor,
                                Sprite = "assets/content/ui/ui.background.tile.psd",
                                Material = "assets/content/ui/namefontmaterial.mat",
                                ImageType = Image.Type.Tiled
                            },
                            RectTransform =
                            {
                                AnchorMin = "0 1", AnchorMax = "1 1",
                                OffsetMin = $"0 {playersOffsetY - playerHeight}",
                                OffsetMax = $"0 {playersOffsetY}"
                            }
                        }, LayerContent + ".Leaderboard.Table", playerLayer, playerLayer);

                        leaderboardContainer.Add(new CuiElement
                        {
                            Parent = playerLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = topNumber.ToString(),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleLeft, Color = targetTopColor
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "-20 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        var playersOffsetX = 0f;
                        for (var colIndex = 0; colIndex < _config.LeaderboardColumns.Count; colIndex++)
                        {
                            var leaderboardColumn = _config.LeaderboardColumns[colIndex];

                            if (leaderboardColumn.Type == LootSettings.LootType.Custom &&
                                leaderboardColumn.Prefab == "nickname")
                            {
                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiRawImageComponent
                                        {
                                            SteamId = topPlayer.UserId.ToString()
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0.5",
                                            AnchorMax = "0 0.5",
                                            OffsetMin = $"{playersOffsetX + 6} -14",
                                            OffsetMax = $"{playersOffsetX + 34} 14"
                                        }
                                    }
                                });

                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiTextComponent
                                        {
                                            Text = topPlayer.LastName ?? string.Empty,
                                            Font = "robotocondensed-bold.ttf",
                                            FontSize = 12,
                                            Align = TextAnchor.MiddleLeft,
                                            Color = targetTextColor
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 1",
                                            OffsetMin = $"{playersOffsetX + 45} 0",
                                            OffsetMax = $"{playersOffsetX + leaderboardColumn.Width} 0"
                                        }
                                    }
                                });
                            }
                            else if (colIndex < topPlayer.leaderboardItems.Length)
                            {
                                var val = topPlayer.leaderboardItems[colIndex];

                                var statValue = leaderboardColumn.Type == LootSettings.LootType.Custom && leaderboardColumn.Prefab == "formatеed_total_playtime" ? Instance.GetPlaytimeLocalized(player, Convert.ToDouble(val)) : FormatLargeNumber(val);
                                
                                leaderboardContainer.Add(new CuiElement
                                {
                                    Parent = playerLayer,
                                    Components =
                                    {
                                        new CuiTextComponent
                                        {
                                            Text = statValue ?? string.Empty,
                                            Font = "robotocondensed-bold.ttf", FontSize = 12,
                                            Align = TextAnchor.MiddleCenter,
                                            Color = targetTextColor
                                        },
                                        new CuiRectTransformComponent
                                        {
                                            AnchorMin = "0 0", AnchorMax = "0 1",
                                            OffsetMin = $"{playersOffsetX} 0",
                                            OffsetMax = $"{playersOffsetX + leaderboardColumn.Width} 0"
                                        }
                                    }
                                });
                            }

                            if (colIndex + 1 != _config.LeaderboardColumns.Count)
                            {
                                leaderboardContainer.Add(new CuiPanel
                                {
                                    Image = {Color = HexToCuiColor("#E2DBD3", 10)},
                                    RectTransform =
                                    {
                                        AnchorMin = "0 0", AnchorMax = "0 1",
                                        OffsetMin = $"{playersOffsetX + leaderboardColumn.Width - 1f} 0",
                                        OffsetMax = $"{playersOffsetX + leaderboardColumn.Width + 1f} 0"
                                    }
                                }, playerLayer);

                                playersOffsetX += leaderboardColumn.Width;
                            }
                        }

                        #endregion Player Panel

                        #region Position

                        playersOffsetY = playersOffsetY - playerHeight - playerMarginY;

                        #endregion
                    }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }
            }

            public int GetLeaderboardPlayersToShow()
            {
                return 10;
            }

            public void SearchPage(BasePlayer player, CuiElementContainer container)
            {
                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform = {AnchorMin = "0 0", AnchorMax = "1 1"}
                }, Layer + ".Main", LayerContent, LayerContent);

                #region Search

                container.Add(new CuiElement
                {
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UISearchPlayersTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -50", OffsetMax = "0 -10"}
                    }
                });

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = HexToCuiColor("#696969", 30), Sprite = "assets/content/ui/UI.Background.Tile.psd",
                        Material = "assets/content/ui/uibackgroundblur-ingamemenu.mat"
                    },
                    RectTransform = {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -90", OffsetMax = "294 -50"}
                }, LayerContent, LayerContent + ".Panel.Search");

                SearchInputField(player, container);

                #endregion

                SearchPlayersSection(player, container);
            }

            public void SearchInputField(BasePlayer player, CuiElementContainer container, bool update = false)
            {
                if (!Instance._openedLeaderboards.TryGetValue(player.userID, out var openedLeaderboard)) return;

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Panel.Search" + ",Input",
                    Parent = LayerContent + ".Panel.Search",
                    Update = update,
                    Components =
                    {
                        new CuiInputFieldComponent
                        {
                            Text = !string.IsNullOrWhiteSpace(openedLeaderboard.search)
                                ? openedLeaderboard.search
                                : Msg(player, UISearchInputPlaceholder),
                            Font = "robotocondensed-bold.ttf", FontSize = 14,
                            Align = TextAnchor.MiddleCenter,
                            Color = HexToCuiColor("#E2DBD3", 30),
                            NeedsKeyboard = true,
                            Command = "UI_UltimateLeaderboard search"
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 0", OffsetMax = "0 0"}
                    }
                });
            }

            public void SearchPlayersSection(BasePlayer player, CuiElementContainer container)
            {
                #region Players

                #region Background

                var scrollRect = new CuiRectTransform
                {
                    AnchorMin = "0 1",
                    AnchorMax = "1 1",
                    OffsetMin = $"0 {-1500}",
                    OffsetMax = "0 0"
                };

                container.Add(new CuiElement
                {
                    Name = LayerContent + ".Section.Players" + ".Scroll",
                    DestroyUi = LayerContent + ".Section.Players" + ".Scroll",
                    Parent = LayerContent,
                    Components =
                    {
                        new CuiImageComponent {Color = "0 0 0 0"},
                        new CuiScrollViewComponent
                        {
                            MovementType = ScrollRect.MovementType.Clamped,
                            Vertical = true,
                            Inertia = true,
                            Horizontal = false,
                            Elasticity = 0.25f,
                            DecelerationRate = 0.3f,
                            ScrollSensitivity = 24f,
                            ContentTransform = scrollRect,
                            VerticalScrollbar = new CuiScrollbar
                            {
                                Size = 3,
                                AutoHide = true,
                                HighlightColor = HexToCuiColor("#D74933"),
                                HandleColor = HexToCuiColor("#D74933"),
                                PressedColor = HexToCuiColor("#D74933"),
                                TrackColor = HexToCuiColor("#373737")
                            }
                        },
                        new CuiRectTransformComponent
                        {
                            AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "0 10", OffsetMax = "0 -100"
                        }
                    }
                });

                #endregion

                #region Table

                var playerWidth = 222f;
                var playerHeight = 40f;
                var playersMarginX = 78f;
                var playersMarginY = 0f;

                var defaultIndentX = 0f;
                var defaultIndentY = -40f;

                var offsetX = defaultIndentX;
                var offsetY = defaultIndentY;

                var players = Pool.Get<List<(string displayName, string userID)>>();
                try
                {
                    Instance.PopulatePlayers(player.userID, ref players);

                    var maxColumns = 4;
                    var rows = Mathf.CeilToInt((float) players.Count / maxColumns);

                    var totalScrollHeight =
                        Mathf.Max(rows * playerHeight + (rows - 1) * playersMarginY + Mathf.Abs(defaultIndentY), 375);
                    scrollRect.OffsetMin = $"0 -{totalScrollHeight}";

                    if (players.Count == 0)
                    {
                        container.Add(new CuiElement
                        {
                            Parent = LayerContent + ".Section.Players" + ".Scroll",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlayersNoPlayers),
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 40,
                                    Align = TextAnchor.MiddleCenter,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    VerticalOverflow = VerticalWrapMode.Overflow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1"
                                }
                            }
                        });
                    }
                    else
                    {
                        #region Title

                        container.Add(new CuiElement
                        {
                            Parent = LayerContent + ".Section.Players" + ".Scroll",
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, UIPlayersListTitle), Font = "robotocondensed-bold.ttf",
                                    FontSize = 20, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3", 90)
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -30", OffsetMax = "-40 0"}
                            }
                        });

                        #endregion

                        for (var i = 0; i < players.Count; i++)
                        {
                            var (displayName, userID) = players[i];

                            #region Panel

                            var targetColor =
                                (i + 1) % 2 == 0 ? HexToCuiColor("#696969", 10) : HexToCuiColor("#696969", 15);

                            var playerLayer = CuiHelper.GetGuid();

                            container.Add(new CuiElement
                            {
                                Name = playerLayer,
                                Parent = LayerContent + ".Section.Players" + ".Scroll",
                                Components =
                                {
                                    new CuiButtonComponent
                                    {
                                        Command = "UI_UltimateLeaderboard search_open_profile " + userID,
                                        Color = targetColor,
                                        Sprite = "assets/content/ui/ui.background.tile.psd",
                                        Material = "assets/content/ui/namefontmaterial.mat",
                                        ImageType = Image.Type.Tiled
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 1", AnchorMax = "0 1",
                                        OffsetMin = $"{offsetX} {offsetY - playerHeight}",
                                        OffsetMax = $"{offsetX + playerWidth} {offsetY}"
                                    }
                                }
                            });


                            container.Add(new CuiElement
                            {
                                Parent = playerLayer,
                                Components =
                                {
                                    new CuiRawImageComponent {SteamId = userID},
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "6 -14",
                                        OffsetMax = "34 14"
                                    }
                                }
                            });

                            container.Add(new CuiElement
                            {
                                Parent = playerLayer,
                                Components =
                                {
                                    new CuiTextComponent
                                    {
                                        Text = displayName ?? "Unknown",
                                        Font = "robotocondensed-bold.ttf", FontSize = 12,
                                        Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3", 70)
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0 0", AnchorMax = "1 1", OffsetMin = "45 0", OffsetMax = "0 0"
                                    }
                                }
                            });

                            #endregion

                            #region Position

                            if ((i + 1) % rows == 0)
                            {
                                offsetY = defaultIndentY;
                                offsetX = offsetX + playerWidth + playersMarginX;
                            }
                            else
                            {
                                offsetY = offsetY - playerHeight - playersMarginY;
                            }

                            #endregion
                        }
                    }
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }

                #endregion

                #endregion
            }

            #region Blocks

            public void DrawProfileBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                #region Fields

                var fieldsOnRow = 4;

                var fieldWidth = 220f;
                var fieldHeight = 48f;
                var fieldMarginX = 90f;
                var fieldMarginY = 30f;
                var leftIndent = 10f;

                var columns = Mathf.Min(fieldsOnRow, block.StatFields.Count);
                var rows = Mathf.CeilToInt((float) block.StatFields.Count / fieldsOnRow);

                var maxWidth = columns * fieldWidth + (columns - 1) * fieldMarginX;
                var maxHeight = rows * fieldHeight + (rows - 1) * fieldMarginY;

                #endregion

                var localOffsetY = offsetY - 10f;

                var currentField = 0;
                for (var row = 0; row < rows; row++)
                {
                    var offsetX = leftIndent;

                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 20),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1",
                            AnchorMax = "0 1",
                            OffsetMin = $"{0} {localOffsetY - fieldHeight - 10}",
                            OffsetMax = $"{maxWidth} {localOffsetY + 10}"
                        }
                    }, LayerContent + ".Scroll");

                    for (var col = 0; col < fieldsOnRow && currentField < block.StatFields.Count; col++)
                    {
                        DrawStatField(player,
                            block.StatFields[currentField],
                            stats,
                            ref container,
                            $"{offsetX} {localOffsetY - fieldHeight}",
                            $"{offsetX + fieldWidth} {localOffsetY}"
                        );

                        currentField++;

                        offsetX += fieldWidth + fieldMarginX;
                    }

                    localOffsetY = localOffsetY - fieldHeight - fieldMarginY;
                }

                totalHeight = maxHeight;
            }

            public void DrawStatisticsBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                var leftIndent = 0f;

                var columnFieldWidth = 160f;
                var columnFieldHeight = 68f;
                var columnFieldMarginX = 5f;
                var columnFieldMarginY = 5f;

                #region Draw Field resources

                void DrawStatGUI(string ID, string parent,
                    string oMin, string oMax,
                    StatFieldConfig statField)
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 20),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = oMin, OffsetMax = oMax
                        }
                    }, parent, ID);

                    #region Icon

                    var imageLayer = container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 50),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "10 -24", OffsetMax = "58 24"}
                    }, ID);

                    var def = !string.IsNullOrEmpty(statField.Icon)
                        ? null
                        : ItemManager.FindItemDefinition(statField.Prefab);
                    container.Add(new CuiElement
                    {
                        Parent = imageLayer,
                        Components =
                        {
                            def != null
                                ? new CuiImageComponent
                                {
                                    ItemId = def.itemid
                                }
                                : statField.Icon.StartsWith("assets/")
                                    ? new CuiRawImageComponent {Sprite = statField.Icon}
                                    : new CuiRawImageComponent {Png = Instance.GetImage(statField.Icon)},
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -24", OffsetMax = "24 24"
                            }
                        }
                    });

                    #endregion

                    #region Stats

                    var statSB = Pool.Get<StringBuilder>();
                    try
                    {
                        statSB.Append(Msg(player, "UI.Field." + statField.Title));

                        if (string.IsNullOrEmpty(statField.SecondPrefab))
                        {
                            statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                       stats.GetFormattedStatValue(player, statField.Type, statField.Prefab) +
                                                       "</b></color></size>");
                        }
                        else
                        {
                            statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                       stats.GetFormattedStatValue(player, statField.Type, statField.Prefab) +
                                                       "</b></color></size>");
                            statSB.Append('\n')
                                .Append(stats.GetFormattedStatValue(player, statField.Type, statField.SecondPrefab));
                        }

                        container.Add(new CuiElement
                        {
                            Parent = ID,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = statSB.ToString(),
                                    Font = "robotocondensed-regular.ttf",
                                    FontSize = 12,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3", 50),
                                    VerticalOverflow = VerticalWrapMode.Overflow
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "70 0", OffsetMax = "-10 0"
                                }
                            }
                        });
                    }
                    finally
                    {
                        Pool.FreeUnmanaged(ref statSB);
                    }

                    #endregion
                }

                #endregion

                var columnMarginY = 10f;
                var columnsOnRow = 7;

                var offsetX = leftIndent;

                totalHeight = 0;

                var localOffsetY = offsetY;

                for (var columnIndex = 0; columnIndex < block.Columns.Count; columnIndex++)
                {
                    var column = block.Columns[columnIndex];

                    var columnRows = Mathf.CeilToInt((float) column.StatFields.Count / columnsOnRow);
                    var rowsHeight = columnRows * columnFieldHeight + (columnRows - 1) * columnFieldMarginY;

                    var columnHeight = rowsHeight + 24f;
                    var columnWidth = columnsOnRow * columnFieldWidth + (columnsOnRow - 1) * columnFieldMarginX;

                    var columnLayer = LayerContent + $".Statistics.Section.{columnIndex}";

                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0 1", AnchorMax = "0 1",
                            OffsetMin = $"{offsetX} {localOffsetY - columnHeight}",
                            OffsetMax = $"{offsetX + columnWidth} {localOffsetY}"
                        }
                    }, LayerContent + ".Scroll", columnLayer);

                    container.Add(new CuiElement
                    {
                        Parent = columnLayer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = column.Title,
                                Font = "robotocondensed-bold.ttf", FontSize = 20,
                                Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "1 1", OffsetMin = "0 -24", OffsetMax = "0 0"}
                        }
                    });

                    var fieldOffsetX = 0f;
                    var fieldOffsetY = -24f;

                    for (var fieldIndex = 0; fieldIndex < column.StatFields.Count; fieldIndex++)
                    {
                        var field = column.StatFields[fieldIndex];

                        DrawStatGUI(
                            $"{columnLayer}.{fieldIndex}",
                            columnLayer,
                            $"{fieldOffsetX} {fieldOffsetY - columnFieldHeight}",
                            $"{fieldOffsetX + columnFieldWidth} {fieldOffsetY}",
                            field);

                        if ((fieldIndex + 1) % columnsOnRow == 0 && fieldIndex + 1 != column.StatFields.Count)
                        {
                            fieldOffsetX = 0f;
                            fieldOffsetY = fieldOffsetY - columnFieldHeight - columnFieldMarginY;
                        }
                        else
                        {
                            fieldOffsetX += columnFieldWidth + columnFieldMarginX;
                        }
                    }

                    localOffsetY = localOffsetY - columnHeight - columnMarginY;

                    totalHeight += columnHeight + columnMarginY;
                }
            }

            public void DrawHitRateBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container, ref float offsetY, out float totalHeight)
            {
                var hitPointHeight = 435;
                // var hitPointHeight = 485;

                #region Hit Point

                totalHeight = hitPointHeight;

                container.Add(new CuiPanel
                {
                    Image = {Color = HexToCuiColor("#000000", 0)},
                    RectTransform =
                    {
                        AnchorMin = "0.5 1", AnchorMax = "0.5 1", OffsetMin = $"-580 {offsetY - hitPointHeight}",
                        OffsetMax = $"580 {offsetY}"
                    }
                }, LayerContent + ".Scroll", LayerContent + ".Section.HitPoint");

                #region Fields

                var totalHits = stats.StatsStorage.GetTotal(LootSettings.LootType.BodyHits);

                var hitRateImageUrl = block.HitRateImages
                    .Find(hitRate => totalHits >= hitRate.MinHits && totalHits <= hitRate.MaxHits)?.Image;

                #endregion

                #region Title

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Section.HitPoint",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIHitRateTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                            Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#FFFFFF")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "367 0"}
                    }
                });

                container.Add(new CuiElement
                {
                    Parent = LayerContent + ".Section.HitPoint",
                    Components =
                    {
                        new CuiTextComponent
                        {
                            Text = Msg(player, UIHitRateDescription), Font = "robotocondensed-regular.ttf",
                            FontSize = 14, Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#E2DBD3")
                        },
                        new CuiRectTransformComponent
                            {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -70", OffsetMax = "367 -30"}
                    }
                });

                #endregion

                #region Image

                if (!string.IsNullOrEmpty(hitRateImageUrl))
                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Section.HitPoint" + ".Image",
                        Parent = LayerContent + ".Section.HitPoint",
                        Components =
                        {
                            new CuiRawImageComponent {Png = Instance.GetImage(hitRateImageUrl)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0.5 0", AnchorMax = "0.5 0", OffsetMin = "-293 0", OffsetMax = "293 330"}
                        }
                    });

                #endregion

                #region Body Parts

                AddBodyPart("59 109.8", "139 129.8", "head", "HEAD");
                AddBodyPart("127 54", "207 74", "left arm|right arm", "ARM");
                AddBodyPart("80 -37", "160 -17", "chest", "CHEST");
                AddBodyPart("95 -139.5", "175 -119.5", "hip", "HIP");
                AddBodyPart("-237 -23.5", "-157 -3.5", "body", "BODY");
                AddBodyPart("-227 77.5", "-147 97.5", "neck", "NECK");

                void AddBodyPart(string offMin, string offMax, string key, string title = null)
                {
                    if (string.IsNullOrEmpty(title)) title = key;

                    if (!stats.StatsStorage.TryGetItem(LootSettings.LootType.BodyHits, key, out var value)) value = 0f;

                    var rect = new CuiRectTransformComponent
                        {AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = offMin, OffsetMax = offMax};

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.HitPoint" + ".Image",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, "UI.BodyParts." + title), Font = "robotocondensed-regular.ttf",
                                FontSize = 10, Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#FFFFFF")
                            },
                            rect
                        }
                    });

                    var percent = value > 0 ? Mathf.CeilToInt(value / totalHits * 100f) : 0;

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.HitPoint" + ".Image",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = percent + "%", Font = "robotocondensed-regular.ttf", FontSize = 10,
                                Align = TextAnchor.MiddleRight, Color = HexToCuiColor("#D74933")
                            },
                            rect
                        }
                    });
                }

                #endregion

                #endregion
            }

            public void DrawBuildingBlock(BasePlayer player, BlockConfig block, PlayerStats stats,
                CuiElementContainer container,
                ref float offsetY, out float totalHeight)
            {
                totalHeight = 435;

                var topGrades = stats.StatsStorage.GetUpgrades();
                try
                {
                    var amount = topGrades.Sum(grade => grade.amount);

                    var bannerKey = "grade_banner_twigs";

                    if (topGrades.Count > 0)
                    {
                        var topGradeName = topGrades[0].shortname.Split(' ')[1];

                        bannerKey = $"grade_banner_{topGradeName}";
                    }

                    container.Add(new CuiPanel
                    {
                        Image = {Color = HexToCuiColor("#000000", 0)},
                        RectTransform =
                        {
                            AnchorMin = "0.5 1", AnchorMax = "0.5 1",
                            OffsetMin = $"-580 {offsetY - totalHeight}",
                            OffsetMax = $"580 {offsetY}"
                        }
                    }, LayerContent + ".Scroll", LayerContent + ".Section.Building");

                    #region Titles

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, UIBuildingTitle), Font = "robotocondensed-bold.ttf", FontSize = 24,
                                Align = TextAnchor.UpperLeft, Color = HexToCuiColor("#FFFFFF")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -30", OffsetMax = "367 0"}
                        }
                    });

                    container.Add(new CuiElement
                    {
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = Msg(player, UIBuildingDescription),
                                Font = "robotocondensed-regular.ttf", FontSize = 14, Align = TextAnchor.UpperLeft,
                                Color = HexToCuiColor("#E2DBD3")
                            },
                            new CuiRectTransformComponent
                                {AnchorMin = "0 1", AnchorMax = "0 1", OffsetMin = "0 -70", OffsetMax = "367 -30"}
                        }
                    });

                    #endregion Titles

                    #region Top Banner

                    container.Add(new CuiElement
                    {
                        Name = LayerContent + ".Section.Building" + ".Banner",
                        Parent = LayerContent + ".Section.Building",
                        Components =
                        {
                            new CuiRawImageComponent {Png = Instance.GetImage(bannerKey)},
                            new CuiRectTransformComponent
                                {AnchorMin = "0 0", AnchorMax = "0 0", OffsetMin = "0 0", OffsetMax = "525 367"}
                        }
                    });

                    #endregion Top Banner

                    #region Items

                    var columnUpIndent = 15f;
                    var columnLeftIndent = -720f; //408f;
                    var columnFieldWidth = 577f;
                    var columnFieldHeight = 54f;
                    var columnFieldMarginY = 22f;

                    for (var fieldIndex = 0; fieldIndex < 5; fieldIndex++)
                    {
                        string blockName;
                        string gradeName;
                        float gradeValue;

                        if (fieldIndex < topGrades.Count)
                        {
                            var grade = topGrades[fieldIndex];
                            var keyParts = grade.shortname.Split(' ');
                            blockName = keyParts[0];
                            gradeName = keyParts[1];
                            gradeValue = grade.amount;
                        }
                        else
                        {
                            blockName = "wall";
                            gradeName = "twigs";
                            gradeValue = 0f;
                        }

                        var offsetUp = columnFieldHeight * fieldIndex + columnFieldMarginY * fieldIndex +
                                       columnFieldMarginY +
                                       columnUpIndent;

                        var gradeLayer = CuiHelper.GetGuid();

                        container.Add(new CuiPanel
                        {
                            Image =
                            {
                                Color = HexToCuiColor("#38393F"),
                                Sprite = "assets/content/ui/ui.background.transparent.linearltr.tga",
                                Material = "assets/content/ui/namefontmaterial.mat"
                            },
                            RectTransform =
                            {
                                AnchorMin = "1 1", AnchorMax = "1 1",
                                OffsetMin = $"{columnLeftIndent} -{offsetUp + columnFieldHeight}",
                                OffsetMax = $"{columnLeftIndent + columnFieldWidth} -{offsetUp}"
                            }
                        }, LayerContent + ".Section.Building", gradeLayer);

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = (fieldIndex + 1).ToString(), Font = "robotocondensed-bold.ttf",
                                    FontSize = 24,
                                    Align = TextAnchor.MiddleLeft, Color = HexToCuiColor("#E2DBD3")
                                },
                                new CuiRectTransformComponent
                                    {AnchorMin = "0 0", AnchorMax = "0 1", OffsetMin = "15 0", OffsetMax = "45 0"}
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiImageComponent
                                {
                                    Color = HexToCuiColor("#CF432D"),
                                    Sprite = $"assets/prefabs/building core/{blockName}/{blockName}.png"
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0.5", AnchorMax = "0 0.5",
                                    OffsetMin = "46 -34", OffsetMax = "114 34"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = Msg(player, "UI.Building." + blockName + "_" + gradeName),
                                    Font = "robotocondensed-bold.ttf",
                                    FontSize = 18,
                                    Align = TextAnchor.MiddleLeft,
                                    Color = HexToCuiColor("#E2DBD3")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "0 0", AnchorMax = "1 1",
                                    OffsetMin = "146 0", OffsetMax = "0 0"
                                }
                            }
                        });

                        container.Add(new CuiElement
                        {
                            Parent = gradeLayer,
                            Components =
                            {
                                new CuiTextComponent
                                {
                                    Text = $"{Mathf.CeilToInt(Mathf.Max(gradeValue / amount * 100f, 0))}%",
                                    Font = "robotocondensed-bold.ttf", FontSize = 24, Align = TextAnchor.MiddleRight,
                                    Color = HexToCuiColor("#D74933")
                                },
                                new CuiRectTransformComponent
                                {
                                    AnchorMin = "1 0",
                                    AnchorMax = "1 1",
                                    OffsetMin = "-186 0",
                                    OffsetMax = "-76 0"
                                }
                            }
                        });

                        #region Icon

                        var gradeItem = Instance.GetShortnameFromBuildingGrade(gradeName);
                        if (!string.IsNullOrEmpty(gradeItem))
                        {
                            container.Add(new CuiPanel
                            {
                                Image = {Color = HexToCuiColor("#D74933")},
                                RectTransform =
                                    {AnchorMin = "1 0", AnchorMax = "1 1", OffsetMin = "-54 0", OffsetMax = "0 0"}
                            }, gradeLayer, gradeLayer + ".icon");

                            container.Add(new CuiElement
                            {
                                Parent = gradeLayer + ".icon",
                                Components =
                                {
                                    new CuiImageComponent
                                    {
                                        ItemId = ItemManager.FindItemDefinition(gradeItem)?.itemid ?? 0
                                    },
                                    new CuiRectTransformComponent
                                    {
                                        AnchorMin = "0.5 0.5",
                                        AnchorMax = "0.5 0.5",
                                        OffsetMin = "-19 -19",
                                        OffsetMax = "19 19"
                                    }
                                }
                            });
                        }

                        #endregion
                    }

                    #endregion Items
                }
                finally
                {
                    Pool.FreeUnmanaged(ref topGrades);
                }
            }

            private void DrawStatField(BasePlayer player, StatFieldConfig fieldConfig, PlayerStats stats,
                ref CuiElementContainer container,
                string oMin, string oMax, string aMin = "0 1", string aMax = "0 1")
            {
                #region Stat Field

                var statFieldLayer = CuiHelper.GetGuid();

                container.Add(new CuiPanel
                {
                    Image =
                    {
                        Color = "0 0 0 0"
                    },
                    RectTransform = {AnchorMin = aMin, AnchorMax = aMax, OffsetMin = oMin, OffsetMax = oMax}
                }, LayerContent + ".Scroll", statFieldLayer);

                #endregion Stat Field

                #region Stat Field Icon

                if (!string.IsNullOrEmpty(fieldConfig.Icon))
                {
                    container.Add(new CuiPanel
                    {
                        Image =
                        {
                            Color = HexToCuiColor("#38393F", 50),
                            Sprite = "assets/content/ui/ui.background.tile.psd",
                            Material = "assets/content/ui/namefontmaterial.mat"
                        },
                        RectTransform =
                            {AnchorMin = "0 0.5", AnchorMax = "0 0.5", OffsetMin = "0 -24", OffsetMax = "48 24"}
                    }, statFieldLayer, statFieldLayer + ".Icon.Background");

                    var imageComponent = fieldConfig.Icon == "avatar"
                        ? new CuiRawImageComponent
                        {
                            SteamId = stats.UserId.ToString()
                        }
                        : new CuiRawImageComponent
                        {
                            Png = Instance.GetImage(fieldConfig.Icon)
                        };

                    container.Add(new CuiElement
                    {
                        Parent = statFieldLayer + ".Icon.Background",
                        Components =
                        {
                            imageComponent,
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0.5 0.5", AnchorMax = "0.5 0.5", OffsetMin = "-24 -24", OffsetMax = "24 24"
                            }
                        }
                    });
                }

                #endregion Stat Field Icon

                #region Stats

                var statSB = Pool.Get<StringBuilder>();
                try
                {
                    statSB.Append(Msg(player, "UI.Field." + fieldConfig.Title));

                    if (string.IsNullOrEmpty(fieldConfig.SecondPrefab))
                    {
                        statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                   stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.Prefab) +
                                                   "</b></color></size>");
                    }
                    else
                    {
                        statSB.Append('\n').Append("<size=13><color=#E2DBD3><b>" +
                                                   stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.Prefab) +
                                                   "</b></color></size>");
                        statSB.Append('\n')
                            .Append(stats.GetFormattedStatValue(player, fieldConfig.Type, fieldConfig.SecondPrefab));
                    }

                    container.Add(new CuiElement
                    {
                        Parent = statFieldLayer,
                        Components =
                        {
                            new CuiTextComponent
                            {
                                Text = statSB.ToString(),
                                Font = "robotocondensed-regular.ttf",
                                FontSize = 12,
                                Align = TextAnchor.MiddleLeft,
                                Color = HexToCuiColor("#E2DBD3", 50),
                                VerticalOverflow = VerticalWrapMode.Overflow
                            },
                            new CuiRectTransformComponent
                            {
                                AnchorMin = "0 0", AnchorMax = "1 1",
                                OffsetMin = "60 0", OffsetMax = "0 0"
                            }
                        }
                    });
                }
                finally
                {
                    Pool.FreeUnmanaged(ref statSB);
                }

                #endregion
            }

            #endregion Blocks
        }

        #endregion Templates

        #region Utils
        
        private static string FormatLargeNumber(float num)
        {
            if (num > 999999999 || num < -999999999)
            {
                return num.ToString("0,,,.###B", CultureInfo.InvariantCulture);
            }
            else if (num > 999999 || num < -999999)
            {
                return num.ToString("0,,.##M", CultureInfo.InvariantCulture);
            }
            else if (num > 999 || num < -999)
            {
                return num.ToString("0,.#K", CultureInfo.InvariantCulture);
            }
            else
            {
                return num.ToString(CultureInfo.InvariantCulture);
            }
        }

        private static string GetShortnameFromPrefab(string prefabName)
        {
            return prefabName switch
            {
                "40mm_grenade_he" => "ammo.grenadelauncher.he",
                "grenade.beancan.deployed" => "grenade.beancan",
                "grenade.f1.deployed" => "grenade.f1",
                "grenade.molotov.deployed" => "grenade.molotov",
                "grenade.flashbang.deployed" => "grenade.flashbang",
                "explosive.satchel.deployed" => "explosive.satchel",
                "explosive.timed.deployed" => "explosive.timed",
                "rocket_basic" => "ammo.rocket.basic",
                "rocket_hv" => "ammo.rocket.hv",
                "rocket_fire" => "ammo.rocket.fire",
                "survey_charge.deployed" => "surveycharge",
                _ => prefabName
            };
        }

        private List<string> FindBuildingBlockPrefabs()
        {
            var list = Pool.Get<List<string>>();

            foreach (var (prefab, obj) in GameManager.server.preProcessed.prefabList)
                if (obj.TryGetComponent<BuildingBlock>(out var block) && block != null)
                    list.Add(block.ShortPrefabName);

            return list;
        }

        private List<AwardCategory> GetAvailableAwardCategories(string player)
        {
            var list = Pool.Get<List<AwardCategory>>();

            foreach (var awardCategory in _config.Awards.Categories)
                if (awardCategory.Enabled && (string.IsNullOrEmpty(awardCategory.Permission) ||
                                              permission.UserHasPermission(player, awardCategory.Permission)))
                    list.Add(awardCategory);

            return list;
        }

        private List<TabConfig> GetAvailableTabs(string player)
        {
            var list = Pool.Get<List<TabConfig>>();

            foreach (var tab in _config.Tabs)
                if (tab.Enabled && (string.IsNullOrEmpty(tab.Permission) ||
                                    permission.UserHasPermission(player, tab.Permission)))
                    list.Add(tab);

            return list;
        }

        private List<CategoryConfig> GetAvailableCategories(string player)
        {
            var list = Pool.Get<List<CategoryConfig>>();

            foreach (var category in _config.Categories)
                if (category.Enabled && (string.IsNullOrEmpty(category.Permission) ||
                                         permission.UserHasPermission(player, category.Permission)))
                    list.Add(category);

            return list;
        }

        private void PopulatePlayers(ulong playerId, ref List<(string displayName, string userID)> list)
        {
            if (!_openedLeaderboards.TryGetValue(playerId, out var openedLeaderboard))
                return;

#if TESTING
            var testPlayers = new List<(string displayName, string userID)>();

            string GenerateNickname()
            {
                string[] Adjectives =
 { "quick", "slow", "sly", "brave", "quiet", "loud", "bright", "dark", "big", "small" };
                string[] Nouns = { "wolf", "fox", "bear", "eagle", "snake", "fish", "tree", "stone", "fire", "water" };

                var adjective = Adjectives.GetRandom();
                var noun = Nouns.GetRandom();
                var number = UnityEngine.Random.Range(0, 1000);
                return $"{adjective}{noun}{number}";
            }

            string GenerateSteamID()
            {
                long steamID64 = (long)(UnityEngine.Random.value * long.MaxValue);
                return steamID64.ToString();
            }

            for (int i = 0; i < 100; i++)
            {
                testPlayers.Add((GenerateNickname(), GenerateSteamID()));
            }

            
            if (string.IsNullOrEmpty(openedLeaderboard.search))
            {
                list.AddRange(testPlayers);
            }
            else
            {
                list.AddRange(testPlayers.FindAll(x => x.displayName.StartsWith(openedLeaderboard.search, StringComparison.CurrentCultureIgnoreCase)));
            }

#else
            if (string.IsNullOrEmpty(openedLeaderboard.search))
            {
                foreach (var player in Leaderboard.leaderboardData)
                    if (player.UserId != playerId)
                        list.Add((player.LastName, player.UserId.ToString()));
            }
            else
            {
                foreach (var player in Leaderboard.leaderboardData)
                    if (player.UserId != playerId &&
                        (player.UserId.ToString() == openedLeaderboard.search ||
                         player.LastName.StartsWith(openedLeaderboard.search,
                             StringComparison.CurrentCultureIgnoreCase) ||
                         player.LastName.Contains(openedLeaderboard.search, CompareOptions.IgnoreCase)))
                        list.Add((player.LastName, player.UserId.ToString()));
            }
#endif
        }

        private IEnumerator LoadPlayersCache(int offset)
        {
            yield return CoroutineEx.waitForFixedUpdate;

            dataStorage.LoadPlayersToLeaderboard(offset);
        }

        private void LoadDataStorage()
        {
            switch (_config.Storage.StorageType)
            {
                case Configuration.StorageType.JSON:
                    dataStorage = new JsonPlayerDataStorage();
                    break;
                case Configuration.StorageType.SQLite:
                {
                    var sqlData = new SqlitePlayerDataStorage();
                    sqlData.LoadDatabase();
                    dataStorage = sqlData;
                    break;
                }
                case Configuration.StorageType.MySQL:
                {
                    var sqlData = new MySqlPlayerDataStorage();
                    sqlData.LoadDatabase();
                    dataStorage = sqlData;
                    break;
                }
                default:
                    PrintError($"Invalid storage method: {_config.Storage.StorageType}");
                    return;
            }
        }

        private void LoadActivePlayers()
        {
            var userIDs = GetActivePlayerIDs();

            dataStorage?.LoadGroupPlayerStats(userIDs, players =>
            {
                try
                {
                    foreach (var (userID, stats) in players) playerStats[userID] = stats;

                    #region Missing Players

                    var missingPlayers = userIDs.Except(players.Keys);
                    foreach (var userID in missingPlayers)
                    {
                        var stats = new PlayerStats(userID);
                        playerStats[userID] = stats;
                    }

                    #endregion Missing Players
                }
                finally
                {
                    Pool.FreeUnmanaged(ref players);
                }

                foreach (var player in BasePlayer.activePlayerList)
                    if (PlayerStats.TryGet(player.userID, out var stats))
                    {
                        stats.LastIP = player.net.connection.ipaddress;
                        stats.LastName = player.displayName;

                        Instance?.dataStorage?.SavePlayerStats(stats);
                    }
            });

            foreach (var player in BasePlayer.activePlayerList) Reward.PlayerConnectedCheck(player);
        }

        private ulong[] GetActivePlayerIDs()
        {
            var list = Pool.Get<List<ulong>>();
            try
            {
                foreach (var player in BasePlayer.activePlayerList)
                    if (player.userID.IsSteamId())
                        list.Add(player.userID);

                return list.ToArray();
            }
            finally
            {
                Pool.FreeUnmanaged(ref list);
            }
        }

        private Dictionary<LootSettings.LootType, HashSet<string>> _needToCollect = new();

        private void UnsubscribeUnusedHooks()
        {
            #region Calculate Used Loot Types

            if (_config.Loot.Enabled)
            {
                foreach (var loot in _config.Loot.Loots)
                {
                    AddCustomLoot(loot.LootType, loot.Prefab);
                }
            }

            foreach (var tab in _config.Tabs)
            {
                foreach (var block in tab.Blocks)
                {
                    switch (block.BlockType)
                    {
                        case "HitRate":
                            AddCustomLoot(LootSettings.LootType.BodyHits, "*");
                            break;
                        case "Building":
                            AddCustomLoot(LootSettings.LootType.Upgrade, "*");
                            break;
                    }

                    foreach (var column in block.Columns)
                    {
                        foreach (var statField in column.StatFields)
                        {
                            AddCustomLoot(statField.Type, statField.Prefab);
                        }
                    }

                    foreach (var statField in block.StatFields)
                    {
                        AddCustomLoot(statField.Type, statField.Prefab);
                    }
                }
            }

            foreach (var category in _config.Awards.Categories)
            {
                AddCustomLoot(category.Type, category.Prefab);
            }

            foreach (var column in _config.LeaderboardColumns)
            {
                AddCustomLoot(column.Type, column.Prefab);
            }

            if (_config.AutoMessages.Chat.Enabled)
            {
                foreach (var message in _config.AutoMessages.Chat.Messages)
                {
                    AddCustomLoot(message.LootType, message.Prefab);
                }
            }

            if (_config.AutoMessages.Discord.Enabled)
            {
                foreach (var message in _config.AutoMessages.Discord.Messages)
                {
                    AddCustomLoot(message.LootType, message.Prefab);
                }
            }

            if (_config.CustomTitles.Enabled)
            {
                foreach (var title in _config.CustomTitles.Titles)
                {
                    AddCustomLoot(title.LootType, title.Prefab);
                }
            }

            if (_needToCollect.TryGetValue(LootSettings.LootType.Custom, out var customLoot))
            {
                foreach (var loot in customLoot)
                {
                    switch (loot)
                    {
                        case "kdr":
                        case "kd":
                            AddCustomLoot(LootSettings.LootType.Kill, "kills");
                            AddCustomLoot(LootSettings.LootType.Death, "deaths");
                            break;

                        case "longest_kill_distance":
                            AddCustomLoot(LootSettings.LootType.Kill, "max_distance");
                            break;

                        case "total_hits":
                            AddCustomLoot(LootSettings.LootType.BodyHits, "*");
                            break;

                        case "total_resources":
                            AddCustomLoot(LootSettings.LootType.Gather, "*");
                            break;

                        case "total_items_crafted":
                            AddCustomLoot(LootSettings.LootType.Craft, "*");
                            break;
                        case "events_won":
                            AddCustomLoot(LootSettings.LootType.Event, "*");
                            break;

                        case "structures_built":
                            AddCustomLoot(LootSettings.LootType.Construction, "*");
                            break;

                        case "upgrades_performed":
                            AddCustomLoot(LootSettings.LootType.Upgrade, "*");
                            break;
                    }
                }
            }

            void AddCustomLoot(LootSettings.LootType type, string prefab = null)
            {
                _needToCollect.TryAdd(type, new HashSet<string>());

                if (prefab == "*")
                    _needToCollect[type] = new HashSet<string> { "*" };
                else if (!string.IsNullOrEmpty(prefab))
                {
                    foreach (var loot in prefab.Split('|'))
                    {
                        _needToCollect[type].Add(loot);
                    }
                }
            }

            #endregion Calculate Used Loot Types

            #region Unsubscribe Unused Hooks

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Kill) && !_needToCollect.ContainsKey(LootSettings.LootType.Raid))
                Unsubscribe(nameof(OnEntityDeath));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Kill) && !_needToCollect.ContainsKey(LootSettings.LootType.BodyHits))
                Unsubscribe(nameof(OnEntityTakeDamage));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Kill) && !_needToCollect.ContainsKey(LootSettings.LootType.Death) && !_needToCollect.ContainsKey(LootSettings.LootType.WeaponUsed))
                Unsubscribe(nameof(OnPlayerDeath));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Medical) && !_needToCollect.ContainsKey(LootSettings.LootType.Consume))
                Unsubscribe(nameof(OnItemAction));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Medical))
                Unsubscribe(nameof(OnHealingItemUse));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Crate) && !_needToCollect.ContainsKey(LootSettings.LootType.LootItems))
                Unsubscribe(nameof(OnLootEntity));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.LootItems))
                Unsubscribe(nameof(OnContainerDropItems));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.LootItems))
                Unsubscribe(nameof(OnDeathDropPickup));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Fishing))
                Unsubscribe(nameof(OnFishCatch));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Puzzle))
                Unsubscribe(nameof(OnCardSwipe));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Upgrade))
                Unsubscribe(nameof(OnStructureUpgrade));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.Gather)) Unsubscribe(nameof(OnCollectiblePickedup));
            if (!_needToCollect.ContainsKey(LootSettings.LootType.Gather)) Unsubscribe(nameof(OnGrowableGathered));
            if (!_needToCollect.ContainsKey(LootSettings.LootType.Gather)) Unsubscribe(nameof(OnDispenserBonusReceived));
            if (!_needToCollect.ContainsKey(LootSettings.LootType.Gather)) Unsubscribe(nameof(OnDispenserGathered));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.ShotFired)) Unsubscribe(nameof(OnWeaponFired));
            if (!_needToCollect.ContainsKey(LootSettings.LootType.ShotFired)) Unsubscribe(nameof(OnMlrsFired));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.ExplosiveUsed)) Unsubscribe(nameof(OnTimedExplosiveExplode));

            if (!_needToCollect.ContainsKey(LootSettings.LootType.RecycleItem)) Unsubscribe(nameof(OnRecyclerToggle));
            if (!_needToCollect.ContainsKey(LootSettings.LootType.RecycleItem)) Unsubscribe(nameof(OnItemRecycle));

            #endregion Unsubscribe Unused Hooks
        }

        private static string HexToCuiColor(string hex, float alpha = 100)
        {
            if (string.IsNullOrEmpty(hex)) hex = "#FFFFFF";

            var str = hex.Trim('#');
            if (str.Length != 6) throw new Exception(hex);
            var r = byte.Parse(str.AsSpan(0, 2), NumberStyles.HexNumber);
            var g = byte.Parse(str.AsSpan(2, 2), NumberStyles.HexNumber);
            var b = byte.Parse(str.AsSpan(4, 2), NumberStyles.HexNumber);

            return $"{(double) r / 255} {(double) g / 255} {(double) b / 255} {alpha / 100f}";
        }

        private static string GetBuildingBlockImage(string blockName)
        {
            return blockName switch
            {
                "block.stair.lshape" => "assets/prefabs/building core/stairs.l/stairs_l.png",
                "block.stair.spiral.triangle" => "assets/prefabs/building core/stairs.spiral.triangle/stairs.triangle.spiral.png",
                "block.stair.spiral" => "assets/prefabs/building core/stairs.spiral/stairs_spiral.png",
                "block.stair.ushape" => "assets/prefabs/building core/stairs.u/stairs_u.png",
                _ => $"assets/prefabs/building core/{blockName}/{blockName}.png",
            };
        }

        private static string GetWeaponName(BasePlayer player)
        {
            var activeItem = player?.GetActiveItem();
            if (activeItem == null) return null;

            var weapon = activeItem.info.shortname;

            weapon = weapon switch
            {
                "rifle.ak.ice" or "rifle.ak.diver" => "rifle.ak",
                _ => weapon 
            };

            return weapon;
        }

        private static string GetBodypartName(BaseCombatEntity entity, HitInfo info)
        {
            return entity?.skeletonProperties?.FindBone(info.HitBone)?.name?.english.ToLower() ?? "";
        }

        private string GetShortnameFromBuildingGrade(string grade)
        {
            switch (grade)
            {
                case "wood":
                    return "wood";
                case "stone":
                    return "stones";
                case "metal":
                    return "metal.fragments";
                case "toptier":
                    return "metal.refined";
                default:
                    return null;
            }
        }

        private void LoadLangAPI()
        {    
            if (LangAPI != null && LangAPI.IsLoaded)
                _isLangAPIReady = Convert.ToBoolean(LangAPI.Call("IsReady"));
        }

        private void LoadCustomTitles()
        {
            if (!_config.CustomTitles.Enabled || BetterChat is not {IsLoaded: true}) return;

            BetterChat?.Call("API_RegisterThirdPartyTitle", new object[] { this, new Func<IPlayer, string>(GetPlayerTitles) });
        }

        private string GetPlayerTitles(IPlayer player)
        {
            if (player is not BasePlayer basePlayer || !Leaderboard.customTitles.TryGetValue(basePlayer.userID, out var titles) || titles.Titles.Length <= 0) return string.Empty;
            
            var sb = Pool.Get<StringBuilder>();
            try
            {
                foreach (var title in titles.Titles)
                {
                    if (sb.Length != 0) sb.Append(' ');

                    sb.Append(title);
                }

                return sb.ToString();
            }
            finally
            {
                Pool.FreeUnmanaged(ref sb);
            }
        }

        private void UpdateTemplateRenderer()
        {
            templateRenderer = new TemplateFullscreenRenderer();

            if (_serverPanelCategory.spStatus && _serverPanelCategory.categoryID != -1)
            {
                var serverPanelTemplate = Convert.ToString(ServerPanel?.Call("API_GetCurrentTemplate"));
                if (!string.IsNullOrEmpty(serverPanelTemplate))
                {
                    switch (serverPanelTemplate)
                    {
                        case "t1":
                        case "t1_1":
                            templateRenderer = new TemplateV1Renderer();
                            return;
                        case "t2":
                            templateRenderer = new TemplateV2Renderer();
                            return;
                    }
                }
                else
                {
                    switch (_config.Pattern)
                    {
                        case PatternServerMenu.V1:
                            templateRenderer = new TemplateV1Renderer();
                            return;
                        case PatternServerMenu.V2:
                            templateRenderer = new TemplateV2Renderer();
                            return;
                    }
                }
            }
        }

        private void LoadServerPanel()
        {
            _serverPanelCategory.spStatus = ServerPanel is {IsLoaded: true};

            ServerPanel?.Call("API_OnServerPanelProcessCategory", Name);

            UpdateTemplateRenderer();
        }

        private void RemoveOpenedLeaderboard(BasePlayer player)
        {
            _openedLeaderboards.Remove(player.userID);
            profileRequestCooldown.Remove(player.userID);
        }

        private void RegisterCommands()
        {
            AddCovalenceCommand(_config.Commands, nameof(CmdOpenLeaderboard));
        }

        private void RegisterPermissions()
        {
            TryRegisterPermission(PERM_Profile);

            TryRegisterPermission(_config.Permission);

            _config.Awards.Categories.ForEach(awardCategory => TryRegisterPermission(awardCategory.Permission));

            _config.Categories.ForEach(category => TryRegisterPermission(category.Permission));

            _config.Tabs.ForEach(tab => TryRegisterPermission(tab.Permission));

            void TryRegisterPermission(string perm)
            {
                if (!string.IsNullOrWhiteSpace(perm) && !permission.PermissionExists(perm))
                    permission.RegisterPermission(perm, this);
            }
        }

        #region Working with Images

        private Dictionary<string, string> _loadedImages = new();

        private void AddImage(string url, string fileName, ulong imageId = 0)
        {
#if CARBON
			imageDatabase.Queue(true, new Dictionary<string, string>
			{
				[fileName] = url
			});
#else
            ImageLibrary?.Call("AddImage", url, fileName, imageId);
#endif
        }

        private string GetImage(string name)
        {
            if (_loadedImages.TryGetValue(name, out var imageID)) return imageID;

#if CARBON
			return imageDatabase.GetImageString(name);
#else
            return Convert.ToString(ImageLibrary?.Call("GetImage", name));
#endif
        }

        private bool HasImage(string name)
        {
#if CARBON
			return Convert.ToBoolean(imageDatabase.HasImage(name));
#else
            return Convert.ToBoolean(ImageLibrary?.Call("HasImage", name));
#endif
        }

        private void LoadImages()
        {
#if CARBON
			imageDatabase = BaseModule.GetModule<ImageDatabaseModule>();
#endif

            _enabledImageLibrary = true;

            var imagesList = new Dictionary<string, string>();

            _config.Tabs.ForEach(tab =>
            {
                if (!tab.Enabled) return;

                tab.Blocks.ForEach(block =>
                {
                    block.Columns?.ForEach(column =>
                    {
                        column.StatFields?.ForEach(stat => { TryLoadImage(stat.Icon, stat.Icon); });
                    });

                    block.StatFields?.ForEach(stat => { TryLoadImage(stat.Icon, stat.Icon); });

                    block.HitRateImages?.ForEach(hitRateImage =>
                    {
                        TryLoadImage(hitRateImage.Image, hitRateImage.Image);
                    });

                    block.GradeBanners?.ForEach(gradeBanner =>
                    {
                        TryLoadImage($"grade_banner_{gradeBanner.Grade}", gradeBanner.Image);
                    });
                });
            });

            foreach (var awardsCategory in _config.Awards.Categories)
            foreach (var award in awardsCategory.Places)
                TryLoadImage(award.Image, award.Image);

            TryLoadImage(_uiSettings?.PaginationButtonNext, _uiSettings?.PaginationButtonNext);
            TryLoadImage(_uiSettings?.PaginationButtonBack, _uiSettings?.PaginationButtonBack);
            TryLoadImage(_uiSettings?.PaginationButtonEnd, _uiSettings?.PaginationButtonEnd);
            TryLoadImage(_uiSettings?.PaginationButtonStart, _uiSettings?.PaginationButtonStart);
            TryLoadImage(_uiSettings?.AwardsIcon, _uiSettings?.AwardsIcon);

            foreach (var (name, url) in imagesList.ToArray())
            {
                if (url.IsURL()) continue;

                imagesList.Remove(name);

                LoadImageFromFS(name, url);
            }

#if CARBON
            imageDatabase.Queue(false, imagesList);
#else
            timer.In(1f, () =>
            {
                if (ImageLibrary is not {IsLoaded: true})
                {
                    _enabledImageLibrary = false;

                    BroadcastILNotInstalled();
                    return;
                }

                ImageLibrary?.Call("ImportImageList", Title, imagesList, 0UL, true);
            });
#endif

            void TryLoadImage(string name, string image)
            {
                if (!string.IsNullOrEmpty(image) && !string.IsNullOrEmpty(name))
                    imagesList.TryAdd(name, image);
            }
        }

        private void BroadcastILNotInstalled()
        {
            for (var i = 0; i < 5; i++) PrintError("IMAGE LIBRARY IS NOT INSTALLED.");
        }

        private void LoadImageFromFS(string name, string path)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(path)) return;

            if (name.StartsWith("assets/") || name == "avatar") return;

            Global.Runner.StartCoroutine(LoadImage(name, path));
        }

        private IEnumerator LoadImage(string name, string path)
        {
            var url = "file://" + Interface.Oxide.DataDirectory + Path.DirectorySeparatorChar + "MeventImages" +
                      Path.DirectorySeparatorChar + path;
            using var www = UnityWebRequestTexture.GetTexture(url);

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"Image not found: {path}");
            }
            else
            {
                var texture = DownloadHandlerTexture.GetContent(www);
                try
                {
                    var image = texture.EncodeToPNG();

                    _loadedImages.TryAdd(name,
                        FileStorage.server.Store(image, FileStorage.Type.png, CommunityEntity.ServerInstance.net.ID)
                            .ToString());
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }

        #endregion

        #endregion

        #region Localization

        private const string
            MsgPlayerNotFound = "Msg.Player.NotFound",
            MsgInvalidSteamID = "Msg.Invalid.SteamID",
            UILeaderboardLoading = "UI.Leaderboard.Loading",
            MsgProfileWaitBeforeAnother = "MSG.Profile.Wait.Before.Another",
            MsgProfileAlreadyLoading = "MSG.Profile.Already.Loading",
            UIPluginTitle = "UI.Plugin.Title",
            UITitleAwards = "UI.Leaderboard.Awards.Title",
            UIPlaceAwards = "UI.Leaderboard.Awards.Place",
            UISearchPlayersTitle = "UI.Search.Players.Title",
            UISearchInputPlaceholder = "UI.Search.Input.Placeholder",
            UIPlayersListTitle = "UI.Players.List.Title",
            UIPlayersNoPlayers = "UI.Players.NoPlayers",
            UIHitRateTitle = "UI.HitRate.Title",
            UIHitRateDescription = "UI.HitRate.Description",
            UIBuildingTitle = "UI.Building.Title",
            UIBuildingDescription = "UI.Building.Description",
            UIStatValueTotal = "UI.Stat.Value.Total",
            UIStatValueToday = "UI.Stat.Value.Today",
            UIStatValueKD = "UI.Stat.Value.KD",
            UIStatValueNone = "UI.Stat.Value.None",
            UIStatValueUnknown = "UI.Stat.Value.Unknown",
            NoILError = "NoILError";

        private void LoadMessages()
        {
            var en = new Dictionary<string, string>
            {
                [NoILError] = "The plugin does not work correctly, contact the administrator!",
                [UIPluginTitle] = "LEADERBOARD",
                [UITitleAwards] = "Top Rewards",
                [UIPlaceAwards] = "{0} place",
                [UISearchPlayersTitle] = "SEARCH PLAYER BY NAME OR STEAMID",
                [UISearchInputPlaceholder] = "ENTER NAME OR STEAMID",
                [UIPlayersListTitle] = "LIST OF PLAYERS ON THE SERVER",
                [UIPlayersNoPlayers] = "There are no available players :(",
                [UIHitRateTitle] = "HIT RATE",
                [UIHitRateDescription] = "Here you can see the total number of hits to body parts",
                [UIBuildingTitle] = "BUILDING",
                [UIBuildingDescription] = "Here you can see building statistics",
                [UIStatValueTotal] = "Total: {0}",
                [UIStatValueToday] = "Today: {0}",
                [UIStatValueKD] = "Kills: {0} / Deaths: {1}",
                [UIStatValueNone] = "None",
                [UIStatValueUnknown] = "Unknown",
                ["UI.Time.Days"] = "{0}d",
                ["UI.Time.Hours"] = "{0}h",
                ["UI.Time.Minutes"] = "{0}m",
                ["UI.Time.Seconds"] = "{0}s",
                [MsgProfileAlreadyLoading] = "This profile is already loading, please wait.",
                [MsgProfileWaitBeforeAnother] = "Please wait a moment before opening another profile.",
                [UILeaderboardLoading] = "Loading...",
                [MsgPlayerNotFound] = "Player statistics not found",
                [MsgInvalidSteamID] = "Invalid Steam ID format",
            };

            #region Categories

            foreach (var category in _config.Categories)
                if (!string.IsNullOrEmpty(category.Title))
                    en.TryAdd("UI.Category." + category.Title, category.Title);

            #endregion Categories

            #region Awards

            foreach (var category in _config.Awards.Categories)
            {
                if (!string.IsNullOrEmpty(category.Title))
                    en.TryAdd("UI.Awards." + category.Title, category.Title);

                foreach (var place in category.Places)
                    if (!string.IsNullOrEmpty(place.Title))
                        en.TryAdd("UI.Awards.Place." + place.Title, place.Title);
            }

            #endregion Awards

            #region Tabs

            foreach (var tab in _config.Tabs)
            {
                en.TryAdd("UI.Tabs." + tab.Name, tab.Name);

                foreach (var block in tab.Blocks)
                {
                    foreach (var blockColumn in block.Columns)
                    {
                        if (!string.IsNullOrEmpty(blockColumn.Title))
                            en.TryAdd("UI.Column." + blockColumn.Title, blockColumn.Title);

                        foreach (var field in blockColumn.StatFields)
                        {
                            if (!string.IsNullOrEmpty(field.Title))
                                en.TryAdd("UI.Field." + field.Title, field.Title);
                        
                            if (!string.IsNullOrEmpty(field.Prefab))
                                en.TryAdd("UI.Field." + field.Prefab, field.Prefab);
                        }
                    }

                    foreach (var field in block.StatFields)
                    {
                        if (!string.IsNullOrEmpty(field.Title))
                            en.TryAdd("UI.Field." + field.Title, field.Title);

                        if (!string.IsNullOrEmpty(field.Prefab))
                            en.TryAdd("UI.Field." + field.Prefab, field.Prefab);

                    }
                }
            }

            #endregion Tabs

            #region Leaderboard

            foreach (var leaderboardColumn in _config.LeaderboardColumns)
                if (!string.IsNullOrEmpty(leaderboardColumn.Name))
                    en.TryAdd("UI.Leaderboard.Column." + leaderboardColumn.Name, leaderboardColumn.Name);

            #endregion Leaderboard

            #region Body Parts

            en.TryAdd("UI.BodyParts." + "HEAD", "HEAD");
            en.TryAdd("UI.BodyParts." + "ARM", "ARM");
            en.TryAdd("UI.BodyParts." + "CHEST", "CHEST");
            en.TryAdd("UI.BodyParts." + "HIP", "HIP");
            en.TryAdd("UI.BodyParts." + "BODY", "BODY");
            en.TryAdd("UI.BodyParts." + "NECK", "NECK");

            #endregion Body Parts

            #region Building

            var buildingBlocks = FindBuildingBlockPrefabs();
            var buildingGrades = (BuildingGrade.Enum[]) Enum.GetValues(typeof(BuildingGrade.Enum));
            try
            {
                foreach (var building in buildingBlocks)
                foreach (var buildingGrade in buildingGrades)
                {
                    var grade = buildingGrade.ToString();
                    if (!string.IsNullOrEmpty(grade))
                    {
                        en.TryAdd("UI.Building." + building + "_" + grade.ToLower(),
                            (building + "- " + grade).ToUpper());

                        en.TryAdd("UI.Grade." + grade.ToLower(), grade.ToUpper());
                    }
                }
            }
            finally
            {
                Pool.FreeUnmanaged(ref buildingBlocks);
            }

            #endregion Building

            lang.RegisterMessages(en, this);
        }

        private static string Msg(string key, string userid = null, params object[] obj)
        {
            return string.Format(Instance.lang.GetMessage(key, Instance, userid), obj);
        }

        private static string Msg(BasePlayer player, string key, params object[] obj)
        {
            return string.Format(Instance.lang.GetMessage(key, Instance, player?.UserIDString), obj);
        }

        private static void Reply(BasePlayer player, string key, params object[] obj)
        {
            Instance.SendReply(player, Msg(key, player.UserIDString, obj));
        }

        private void SendNotify(BasePlayer player, string key, int type, params object[] obj)
        {
            if (_config.UseNotify && (Notify != null || UINotify != null))
                Interface.Oxide.CallHook("SendNotify", player, type, Msg(player, key, obj));
            else
                Reply(player, key, obj);
        }

        #endregion Localization

        #region API

        private void API_OnItemGather(ulong player, string shortname, int amount)
        {
            if (string.IsNullOrEmpty(shortname) || amount <= 0 || !PlayerStats.TryGet(player, out var stats))
                return;

            stats.AddStats(LootSettings.LootType.Gather, shortname, amount);
        }

        private void API_OnEventWin(ulong userID, string eventName, int amount = 1)
        {
            if (!PlayerStats.TryGet(userID, out var stats))
                return;

            stats.AddStats(LootSettings.LootType.Event, eventName, amount);
        }

        private bool API_IsHiddenFromLeaderboard(ulong playerId)
        {
            return PlayerStats.TryGet(playerId, out var stats) && stats.HiddenFromLeaderboard;
        }

        private float API_GetPlayerStat(ulong playerId, string lootType, string shortname)
        {
            if (!PlayerStats.TryGet(playerId, out var stats)) return 0f;

            return stats.GetRawStatValue((LootSettings.LootType)Enum.Parse(typeof(LootSettings.LootType), lootType), shortname);
        }

        #endregion

        #region Testing Functions

#if TESTING

        [ConsoleCommand("ul.lb.show")]
        private void CmdConsoleShowLeadebrboard(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            Interface.Oxide.DataFileSystem.WriteObject(
                    nameof(UltimateLeaderboard) + Path.DirectorySeparatorChar + "Debug_Leaderboard", Leaderboard.leaderboardData);
            
            SendReply(arg, "Leaderboard saved to: Debug_Leaderboard.json");
        }

        [ChatCommand("FakeKillStat")]
        private void FakeKillStatCommand(BasePlayer sender, string command, string[] args)
        {
            if (!sender.IsAdmin)
                return;

            var stats = PlayerStats.Get(sender.userID);

            var weapon = ItemManager.CreateByName("rifle.lr300");

            var hitEnumValues = Enum.GetValues(typeof(HitArea));

            int repeatCount = 1;

            if (args.Length != 0)
                repeatCount = int.Parse(args[0]);

            for (int i = 0; i < repeatCount; i++)
            {
            }

            Debug.Log($"Add fake kill ({repeatCount})");
        }

        [ConsoleCommand("SaveDB")]
        private void SaveDB(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            
            CollectData();

            Debug.Log("DB save!");
        }

        private void CollectData()
        {
            foreach (var stats in playerStats.Values) dataStorage.SavePlayerStats(stats);
        }

        [ConsoleCommand("test")]
        private void testDB(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            var leaderboard = Leaderboard.GetLeaderboardUsers(arg.GetInt(0, 0), arg.GetInt(1, 100));

            foreach (var leader in leaderboard)
            {
                Puts($"{leader.LastName} [ {string.Join(", ", leader.leaderboardItems)} ]");
            }

        }

        [ConsoleCommand("test2")]
        private void searchDublicate(ConsoleSystem.Arg arg)
        {
            OnNewSave();
        }

        [ConsoleCommand("ultimateleaderboard.test.awards")]
        private void TestAwardsConsoleCommand(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;

            if (arg.Connection != null)
            {
                Puts("This command can only be executed from the server console.");
                return;
            }

            try
            {
                Reward.StoreTopPlayersAwards();

                timer.In(2, () => {
                    
                foreach (var player in BasePlayer.activePlayerList)
                {
                    Reward.PlayerConnectedCheck(player);
                }
                });
            }
            catch (Exception ex)
            {
                PrintError("Error while executing test awards command: " + ex.Message);
            }
        }

        [ConsoleCommand("ultimateleaderboard.test.get.building.blocks")]
        private void GetBuildingBlocks(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            var list = new List<(string prefab, string shortName)>();
            foreach (var (prefab, obj) in GameManager.server.preProcessed.prefabList)
                if (obj.TryGetComponent<BuildingBlock>(out var block) && block != null)
                    list.Add((prefab, block.ShortPrefabName));

            var sb = new StringBuilder();
            sb.AppendLine("Building blocks:");
            foreach (var (prefab, shortName) in list)
                sb.AppendLine($"{prefab} | {shortName}");

            Puts(sb.ToString());
        }

        [ConsoleCommand("ultimateleaderboard.test.get.helicopter")]
        private void GetHelicopter(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            var list = new List<(string prefab, string shortName)>();
            foreach (var (prefab, obj) in GameManager.server.preProcessed.prefabList)
                if (obj.TryGetComponent<PatrolHelicopter>(out var helicopter) && helicopter != null)
                    list.Add((prefab, helicopter.ShortPrefabName));

            var sb = new StringBuilder();
            sb.AppendLine("Helicopters:");
            foreach (var (prefab, shortName) in list)
                sb.AppendLine($"{prefab} | {shortName}");

            Puts(sb.ToString());
        }

        [ConsoleCommand("ultimateleaderboard.test.messages.chat")]
        private void TestChatMessages(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            
            var message = _config.AutoMessages.Chat.Messages.Find(x => x.LootType == LootSettings.LootType.Custom && x.Prefab == "formatеed_total_playtime");
            if (message == null)
            {
                Puts("No messages found");
                return;
            }
            
            Puts($"selected message: LootType: {message.LootType} | Prefab: {message.Prefab} | Header: {message.Header} | Limit: {message.Limit}");

            var topPlayers = Leaderboard.GetLeaderboardMessagesUsers(message.LootType, message.Prefab, 0, message.Limit);

            var sb = new StringBuilder();
            sb.AppendLine($"Top {message.Limit} players:");
            foreach (var player in topPlayers)
            {
                sb.AppendLine($"{player.LastName} | {string.Join(", ", player.messageSortValues.Select(x => $"{x.Key}: {x.Value}"))}");
            }

            Puts(sb.ToString());

            sb.Clear();

            sb.AppendLine(message.Header);

            for (var i = 0; i < topPlayers.Count; i++)
            {
                var player = topPlayers[i];
                var value = FormatLargeNumber(player.messageSortValues[(message.LootType, message.Prefab)]);

                sb.AppendLine($"{i + 1}. {player.LastName} - {value}");
            }

            Puts(sb.ToString());
        }

        [ConsoleCommand("ultimateleaderboard.test.messages.data")]
        private void TestChatMessagesRandom(ConsoleSystem.Arg arg)
        {
            if (!arg.IsServerside) return;
            
            
            var topPlayers = Leaderboard.GetLeaderboardMessagesUsers(LootSettings.LootType.Custom, "formatеed_total_playtime", 0, 10);

            foreach (var player in topPlayers)
            {
                Puts($"{player.LastName} | {player.messageSortValues[(LootSettings.LootType.Custom, "formatеed_total_playtime")]}");
            }
        }
#endif

        #endregion
    }
}

#region Extension Methods

namespace Oxide.Plugins.UltimateLeaderboardExtensionMethods
{
    // ReSharper disable ForCanBeConvertedToForeach
    // ReSharper disable LoopCanBeConvertedToQuery
    public static class ExtensionMethods
    {
        public static bool IsURL(this string uriName)
        {
            return Uri.TryCreate(uriName, UriKind.Absolute, out var uriResult) &&
                   (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps);
        }

        public static string GetString(this Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out var val) ? Convert.ToString(val) : string.Empty;
        }

        public static float GetFloat(this Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out var val) ? Convert.ToSingle(val) : 0f;
        }

        public static int GetInt(this Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out var val) ? Convert.ToInt32(val) : 0;
        }
    }
}

#endregion Extension Methods
