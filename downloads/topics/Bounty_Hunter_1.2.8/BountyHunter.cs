using System;
using System.Collections.Generic;
using System.Linq;
using Oxide.Core;
using UnityEngine;
using Quaternion = UnityEngine.Quaternion;
using Random = UnityEngine.Random;
using Newtonsoft.Json;
using Oxide.Core.Libraries;
using Oxide.Core.Plugins;
using UnityEngine.AI;
using Time = UnityEngine.Time;
using Facepunch;

/*Developer API
Dictionary<ulong, int> GetBountyStats() */
// Returns a Dictionary<ulong, int> where the key is the player's ID and the value is the number of bounty kills.
// If no data is available, it returns an empty dictionary.

/*
 * Changelog:
 *
 * Version 1.2.8:
 * - Added a Delay on Dependency Check for NPC To Spawn On Initial Load.
 * - Added Kits Support to Bounties
 */

namespace Oxide.Plugins
{
    [Info("Bounty Hunter", "Wrecks", "1.2.8")]
    [Description("Spawns Bounties at Monuments for Customizable Rewards.")]
    public class BountyHunter : RustPlugin
    {
        [PluginReference] private Plugin Economics, ServerRewards, Kits, MarkerManager, SkillTree;

        #region lang

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages
            (new Dictionary<string, string>
            {
                ["NoPerms"] = "[<color=#b5a642>Bounty Hunter</color>] You do not have permission to run this <color=#b54251>Command</color>!",
                ["NoTokens"] = "[<color=#b5a642>Bounty Hunter</color>] :coffeecan: You have no <color=#b5a642>Bounty Tokens</color> on you that I can cash out. \nBe on the look out, see you soon!\n\n:exclamation:",
                ["BountyHunterCooldownMessage"] = "[<color=#b5a642>Bounty Hunter</color>] :angry: Wait <color=#a642b5>{0}</color> second(s)!, I think I spotted a <color=#b5a642>Bounty</color>...",
                ["EconomicsMessage"] = "[<color=#b5a642>Bounty Hunter</color>] :eyes: \n\n I can net you <color=#85bb65>$</color><color=#42b5a6>{0}</color> for your <color=#42b5a6>{1}x</color> <color=#a642b5>{2}</color>(s).",
                ["SrMessage"] = "[<color=#b5a642>Bounty Hunter</color>] :eyes: \n\n I can net you <color=#42b5a6>{0}</color> <color=#cd5c5c>RP</color> for your <color=#42b5a6>{1}x</color> <color=#a642b5>{2}</color>(s).",
                ["SaleDisabled"] = "[<color=#b5a642>Bounty Hunter</color>] <color=#42b5a6>Sales</color> are Disabled in the Config, Enable and Try again.",
                ["NoBountyHunterLicense"] = "[<color=#b5a642>Bounty Hunter</color>] :eyebrow: Are you out there targeting Bounties without a <color=#42b5a6>License</color>? \nUse <color=#42b5a6>/BuyBL</color> to purchase one.",
                ["AlreadyHasLicense"] = "[<color=#b5a642>Bounty Hunter</color>] You already have a <color=#b54251>Bounty Hunter License</color>.",
                ["BuyBountyHunterLicenseSuccess"] = "[<color=#b5a642>Bounty Hunter</color>] <color=#b54251>Bounty Hunter License</color> Obtained. \nYou were charged {0}<color=#3e9c35>{1}</color>, Stay Vigilant, <color=#8bb542>{2}</color>!",
                ["InsufficientFunds"] = "[<color=#b5a642>Bounty Hunter</color>] You don't have enough funds to purchase a <color=#b5a642>Bounty Hunter License</color>. \nIt costs <color=#42b5a6>{0}</color><color=#a642b5>{1}</color>.",
                ["Despawn"] = "[<color=#b5a642>Bounty Hunter</color>] The <color=#42b5a6>{0}</color> has <color=#b54251>Fled</color> the Island.",
                ["BountySpotted"] = "[<color=#b5a642>Bounty Hunter</color>]\n\n A <color=#42b5a6>{0}</color> has been spotted near <color=#b56d42>{1}</color>.",
                ["BountyEliminated"] = "[<color=#b5a642>Bounty Hunter</color>] The <color=#42b5a6>{0}</color> was <color=#b5a642>Eliminated</color> by <color=#8bb542>{1}</color>!",
                ["BountyDrop"] = "[<color=#b5a642>Bounty Hunter</color>] The <color=#42b5a6>{0}</color> dropped a pouch! Open it and deliver the <color=#42b5a6>Bounty Token</color> to the Bounty Hunter for your Reward!",
                ["Revoked"] = "[<color=#b5a642>Bounty Hunter</color>] Your <color=#42b5a6>Bounty License</color> has been <color=#42b5a6>Revoked</color> be wary of Death next time!"
            }, this);
        }

        #endregion

        #region rewardhandling

        private void RewardHandler(BasePlayer player, BountyTiers tier, int quantity, int baseSalePrice)
        {
            double bonusPercentage = Random.Range(tier.MaxDiscount, tier.MaxBonus + 1);
            bonusPercentage = Mathf.Clamp((float)bonusPercentage, -100f, 100f);
            var adjustedSalePrice = baseSalePrice * (1 + bonusPercentage / 100);
            var totalReward = quantity * (int)adjustedSalePrice;
            switch (_config.EconomyPlugin)
            {
                case 1:
                {
                    PayoutEco(player, tier, quantity, totalReward, bonusPercentage);
                    break;
                }
                case 2:
                {
                    PayoutSR(player, tier, quantity, totalReward, bonusPercentage);
                    break;
                }
            }
        }

        private void PayoutSR(BasePlayer player, BountyTiers tier, int quantity, int totalReward, double bonusPercentage)
        {
            ServerRewards.Call("AddPoints", player.UserIDString, totalReward);
            var message = lang.GetMessage("SrMessage", this, player.UserIDString);
            Player.Message(player, string.Format(message, totalReward, quantity, tier.TokenName, bonusPercentage), null, _config.ChatIcon);
            SendSrFx(player);
            SendDiscord(player, tier, quantity, totalReward);
        }

        private void PayoutEco(BasePlayer player, BountyTiers tier, int quantity, int totalReward, double bonusPercentage)
        {
            Economics.Call("Deposit", player.UserIDString, (double)totalReward);
            var message = lang.GetMessage("EconomicsMessage", this, player.UserIDString);
            Player.Message(player, string.Format(message, totalReward, quantity, tier.TokenName, bonusPercentage), null, _config.ChatIcon);
            SendEcoFx(player);
            SendDiscord(player, tier, quantity, totalReward);
            return;
        }

        #endregion

        #region bountytiers

        public class BountyTiers
        {
            [JsonProperty("Tier Name")] public string TierName { get; set; }
            [JsonProperty("Skill Tree Xp Value")] public double Value { get; set; }
            [JsonProperty("Bounty Kit Enabled?")] public bool BountyKitEnabled { get; set; }
            [JsonProperty("Bounty Kit Name?")] public string BountyKitName { get; set; } = "";
            [JsonProperty("Bounty Clothing Options")] public List<BountyClothingOption> BountyClothingOptions { get; set; }
            [JsonProperty("Bounty Total Health")] public int BountyHealth { get; set; }
            [JsonProperty("Bounty Damage Scaling")] public float BountyDamageOutput { get; set; }
            [JsonProperty("Aim Cone Scale")] public float AimConeScale { get; set; }
            [JsonProperty("Weapon to Equip?")] public string WeaponName { get; set; }
            [JsonProperty("Weapon Skin ID")] public ulong WeaponSkin { get; set; }
            [JsonProperty("Bounty Token Name")] public string TokenName { get; set; }
            [JsonProperty("Bounty Token Skin")] public ulong TokenSkinID { get; set; }
            [JsonProperty("Is the Token Marketable to the Bounty NPC?")] public bool Sellable { get; set; }
            [JsonProperty("Sale Price?")] public double SalePrice { get; set; }
            [JsonProperty("Max Discount Buy Variation in %? (Negative Values) (To Simulate Supply & Demand)")] public float MaxDiscount { get; set; }
            [JsonProperty("Maximum Bonus Variation in %? (Positive Values) (To Simulate Supply & Demand)")] public float MaxBonus { get; set; }
            [JsonProperty("Min Item Drop")] public int MinItemDrop { get; set; } = 1;
            [JsonProperty("Max Item Drop")] public int MaxItemDrop { get; set; } = 3;
            [JsonProperty("Bounty Optional Drops")] public List<BountyDrops> BountyDropsList { get; set; }
        }

        #endregion

        #region Declarations

        private int _botCount;
        private Timer spawnTimer;
        private const string AdminPermission = "BountyHunter.Admin";
        private const string LifetimeLicense = "BountyHunter.LifetimeLicense";
        private readonly Dictionary<string, List<Vector3>> _monumentSpawnPoints = new();
        private readonly Dictionary<string, List<Vector3>> _dynamicSpawnPoints = new();
        private Dictionary<string, List<Vector3>> _customSpawnPoints = new();
        private readonly Dictionary<ulong, float> _interactionCooldowns = new();
        private readonly Dictionary<string, BountyTiers> _botTierMap = new();
        private readonly List<ScientistNPC> _spawnedBounties = new();
        private readonly Dictionary<string, Timer> _despawnTimers = new();
        private readonly List<NpcData> _npcDataList = new();
        private readonly List<NPCTalking> _bountyNpCs = new();
        private List<BountyLicense> _playerLicenses = new();
        private Dictionary<ulong, int> _playerData = new();
        private const string CustomSpawn = "CustomSpawn";
        private const string ReloadFx = "assets/prefabs/weapons/grenade launcher/effects/reload_end.prefab";
        private const string DeniedFx = "assets/prefabs/locks/keypad/effects/lock.code.denied.prefab";
        private const string SpawnedFx = "assets/prefabs/deployable/research table/effects/research-success.prefab";
        private const string DespawnedFx = "assets/bundled/prefabs/fx/notice/stack.world.fx.prefab";
        private const string SuccessFx = "assets/prefabs/weapons/spas12/effects/pump_forward.prefab";
        private const string RicochetFx = "assets/bundled/prefabs/fx/ricochet/ricochet2.prefab";
        private const string NoTokensFx = "assets/prefabs/instruments/jerrycanguitar/effects/guitardeploy.prefab";
        private const string Drop = "assets/prefabs/misc/item drop/item_drop_backpack.prefab";
        private const string Npc = "assets/prefabs/npc/bandit/missionproviders/missionprovider_bandit_a.prefab";
        private const string NpcFx = "assets/prefabs/misc/easter/painted eggs/effects/gold_open.prefab";
        private const string RemovalFx = "assets/prefabs/misc/easter/painted eggs/effects/bronze_open.prefab";
        private const string ServerRewardsFx = "assets/prefabs/food/small water bottle/effects/water-bottle-deploy.prefab";
        private const string EconomicsFx = "assets/prefabs/deployable/vendingmachine/effects/vending-machine-purchase-human.prefab";
        private const string DiscordArt = "https://cdn.discordapp.com/attachments/1200248277640163344/1204607519981572126/BOUNTYHUNTERDRAFT.png?ex=65d5592d&is=65c2e42d&hm=1b0dad7de65f6c8bfa798ede2074cdde30572722ba8b1b961d923d16dd6f95f2&";

        #endregion

        #region Config

        private static Configuration _config;

        public class Configuration
        {
            [JsonProperty("Announce Top Bounty Hunters To Chat Every x Seconds (0 To Disable)")] public int AnnounceInterval { get; set; } = 600;
            [JsonProperty("Bounty Prefab Path")] public string BountyPrefab { get; set; } = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab";
            [JsonProperty("Discord Webhook URL")] public string Webhook { get; set; }
            [JsonProperty("Enable Skill Tree Xp Gain?")] public bool EnabledSkillTreeXp { get; set; }
            [JsonProperty("Require License for Xp Gain?")] public bool XpLicReq { get; set; }
            [JsonProperty("Enable Monument Spawns?")] public bool EnableMonumentSpawns { get; set; }
            [JsonProperty("Enable Custom Spawns?")] public bool EnableCustomSpawns { get; set; }
            [JsonProperty("Clear Custom Spawns File On Wipe?")] public bool ClearCustomSpawns { get; set; }
            [JsonProperty("Chat Icon")] public ulong ChatIcon { get; set; }
            [JsonProperty("Maximum Active Bounties")] public int MaxBounties { get; set; }
            [JsonProperty("Drop Bounty Tokens In Pouch?")] public bool TokensEnabled { get; set; }
            [JsonProperty("Base Item for the Bounty Tokens?")] public string PlaceholderItem { get; set; }
            [JsonProperty("Tokens Marketable to the Bounty NPC?")] public bool TokensMarketable { get; set; }
            [JsonProperty("Console Command to Purchase a Bounty License? (For GUI Shops)")] public string ConsoleCommand { get; set; } = "buybl";
            [JsonProperty("Chat Command to Purchase a Bounty License?")] public string ChatCommand { get; set; } = "buybl";
            [JsonProperty("Require a Bounty License To Cash In Tokens to the Bounty NPC?")] public bool LicenseRequired { get; set; }
            [JsonProperty("Price to Purchase a Bounty License?")] public int LicenseFee { get; set; }
            [JsonProperty("Lose Bounty License on Death?")] public bool RemoveLicense { get; set; }
            [JsonProperty("Clear Licenses on Wipe?")] public bool ClearLicenses { get; set; }
            [JsonProperty("Clear Kill Counters on Wipe?")] public bool ClearKills { get; set; }
            [JsonProperty("Dynamic (Bandit Camp) Spawn Of The NPC Enabled?")] public bool DynamicSpawn { get; set; } = true;
            [JsonProperty("Economy Plugin - 1 Economics - 2 Server Rewards")] public int EconomyPlugin { get; set; }
            [JsonProperty("Maximum Amount of Like Tokens Sold at a Time")] public int MaxTradeQuantity { get; set; }
            [JsonProperty("NPC Interaction Cooldown In Seconds")] public float InteractionCooldown { get; set; }
            [JsonProperty("NPC Kit Enabled?")] public bool NpcKitEnabled { get; set; }
            [JsonProperty("NPC Kit Name?")] public string NpcKitName { get; set; }
            [JsonProperty("Amount of Time In Seconds Between Bounty Spawns")] public int BountySpawnRate { get; set; }
            [JsonProperty("Amount of Time In Seconds for a Bounty to Despawn")] public int BountyDespawnTimer { get; set; }
            [JsonProperty("Enable Spawn SFX?")] public bool SpawnFX { get; set; } = true;
            [JsonProperty("Enable Despawn SFX?")] public bool DespawnFX { get; set; } = true;
            [JsonProperty("Announce to Chat When Bounty is Active?")] public bool AnnounceBountyActive { get; set; }
            [JsonProperty("Announce to Chat When Bounty is Claimed?")] public bool AnnounceBountyClaimed { get; set; }
            [JsonProperty("Enable Marker Manager from UMOD, To Mark Bounties?")] public bool MarkerEnabled { get; set; }
            [JsonProperty("Enable Marker Manager from UMOD, To Mark NPC Location?")] public bool MarkerEnabledForNPC { get; set; } = true;
            [JsonProperty("Bounty Tiers")] public List<BountyTiers> BountyTiers { get; set; }
            [JsonProperty("NPC Weapon")] public string NpcWeapon { get; set; } = "rifle.bolt";
            [JsonProperty("NPC Weapon Skin")] public ulong NpcWeaponSkin { get; set; } = 819149392;
            [JsonProperty("NPC Clothing Options")] public List<ClothingOption> NpcClothingOptions { get; set; }

            public static Configuration DefaultConfig()
            {
                return new Configuration
                {
                    AnnounceInterval = 600,
                    BountyPrefab = "assets/rust.ai/agents/npcplayer/humannpc/scientist/scientistnpc_heavy.prefab",
                    Webhook = "INSERT_WEBHOOK_URL",
                    EnabledSkillTreeXp = false,
                    XpLicReq = true,
                    EnableMonumentSpawns = true,
                    EnableCustomSpawns = false,
                    ClearCustomSpawns = false,
                    ChatIcon = 0,
                    MaxBounties = 3,
                    TokensEnabled = true,
                    PlaceholderItem = "blood",
                    LicenseRequired = true,
                    LicenseFee = 10000,
                    RemoveLicense = true,
                    ClearLicenses = false,
                    ClearKills = false,
                    DynamicSpawn = true,
                    TokensMarketable = true,
                    ConsoleCommand = "buybl",
                    ChatCommand = "buybl",
                    EconomyPlugin = 1,
                    MaxTradeQuantity = 3,
                    InteractionCooldown = 3f,
                    NpcKitEnabled = false,
                    NpcKitName = "",
                    BountySpawnRate = 600,
                    BountyDespawnTimer = 1200,
                    SpawnFX = true,
                    DespawnFX = true,
                    AnnounceBountyActive = true,
                    AnnounceBountyClaimed = true,
                    MarkerEnabled = true,
                    MarkerEnabledForNPC = true,
                    NpcWeapon = "rifle.bolt",
                    NpcWeaponSkin = 819149392,
                    NpcClothingOptions = new List<ClothingOption>
                    {
                        new()
                        {
                            Shortname = "burlap.trousers",
                            Skin = 1760352876
                        },
                        new()
                        {
                            Shortname = "burlap.shirt",
                            Skin = 1760350097
                        },
                        new()
                        {
                            Shortname = "shoes.boots",
                            Skin = 547978997
                        },
                        new()
                        {
                            Shortname = "hat.boonie",
                            Skin = 3077061771
                        },
                        new()
                        {
                            Shortname = "burlap.gloves",
                            Skin = 1338273501
                        }
                    },
                    BountyTiers = new List<BountyTiers>
                    {
                        new()
                        {
                            TierName = "Tier 1 Bounty Target",
                            Value = 100,
                            BountyKitEnabled = false,
                            BountyKitName = "",
                            BountyClothingOptions = new List<BountyClothingOption>
                            {
                                new()
                                {
                                    Shortname = "hazmatsuittwitch",
                                    Skin = 0
                                }
                            },
                            BountyHealth = 150,
                            BountyDamageOutput = 1f,
                            AimConeScale = 2f,
                            WeaponName = "rifle.ak",
                            WeaponSkin = 3140321604,
                            TokenName = "Tier 1 Bounty Token",
                            TokenSkinID = 3155517347,
                            Sellable = true,
                            SalePrice = 1000,
                            MaxDiscount = -10,
                            MaxBonus = 5,
                            MinItemDrop = 1,
                            MaxItemDrop = 3,
                            BountyDropsList = new List<BountyDrops>
                            {
                                new()
                                {
                                    Shortname = "stones",
                                    Skin = 0,
                                    CustomName = "",
                                    AmountMin = 3,
                                    AmountMax = 10,
                                    ChanceToDrop = 20.0f
                                },
                                new()
                                {
                                    Shortname = "scrap",
                                    Skin = 0,
                                    CustomName = "",
                                    AmountMin = 5,
                                    AmountMax = 30,
                                    ChanceToDrop = 30.0f
                                },
                                new()
                                {
                                    Shortname = "paper",
                                    Skin = 3048132587,
                                    CustomName = "Cash",
                                    AmountMin = 1,
                                    AmountMax = 3,
                                    ChanceToDrop = 40.0f
                                }
                            }
                        },
                        new()
                        {
                            TierName = "Tier 2 Bounty Target",
                            Value = 200,
                            BountyKitEnabled = false,
                            BountyKitName = "",
                            BountyClothingOptions = new List<BountyClothingOption>
                            {
                                new()
                                {
                                    Shortname = "hazmatsuit.arcticsuit",
                                    Skin = 0
                                }
                            },
                            BountyHealth = 225,
                            BountyDamageOutput = 1.5f,
                            AimConeScale = 2.5f,
                            WeaponName = "rifle.lr300",
                            WeaponSkin = 2715918380,
                            TokenName = "Tier 2 Bounty Token",
                            TokenSkinID = 3155517539,
                            Sellable = true,
                            SalePrice = 2000,
                            MaxDiscount = -5,
                            MaxBonus = 15,
                            MinItemDrop = 1,
                            MaxItemDrop = 3,
                            BountyDropsList = new List<BountyDrops>
                            {
                                new()
                                {
                                    Shortname = "metal.fragments",
                                    Skin = 0,
                                    CustomName = "",
                                    AmountMin = 200,
                                    AmountMax = 500,
                                    ChanceToDrop = 30.0f
                                },
                                new()
                                {
                                    Shortname = "scrap",
                                    Skin = 0,
                                    CustomName = "",
                                    AmountMin = 240,
                                    AmountMax = 300,
                                    ChanceToDrop = 30.0f
                                },
                                new()
                                {
                                    Shortname = "paper",
                                    Skin = 3048132587,
                                    CustomName = "Cash",
                                    AmountMin = 10,
                                    AmountMax = 15,
                                    ChanceToDrop = 40.0f
                                }
                            }
                        },
                        new()
                        {
                            TierName = "Tier 3 Bounty Target",
                            Value = 300,
                            BountyKitEnabled = false,
                            BountyKitName = "",
                            BountyClothingOptions = new List<BountyClothingOption>
                            {
                                new()
                                {
                                    Shortname = "scientistsuit_heavy",
                                    Skin = 0
                                }
                            },
                            BountyHealth = 325,
                            BountyDamageOutput = 2f,
                            AimConeScale = 3f,
                            WeaponName = "rifle.ak.ice",
                            WeaponSkin = 0,
                            TokenName = "Tier 3 Bounty Token",
                            TokenSkinID = 3155517732,
                            Sellable = true,
                            SalePrice = 3200,
                            MaxDiscount = -3,
                            MaxBonus = 20,
                            MinItemDrop = 1,
                            MaxItemDrop = 3,
                            BountyDropsList = new List<BountyDrops>
                            {
                                new()
                                {
                                    Shortname = "metal.refined",
                                    Skin = 0,
                                    CustomName = "",
                                    AmountMin = 200,
                                    AmountMax = 500,
                                    ChanceToDrop = 30.0f
                                },
                                new()
                                {
                                    Shortname = "scrap",
                                    Skin = 0,
                                    CustomName = "",
                                    AmountMin = 440,
                                    AmountMax = 600,
                                    ChanceToDrop = 30.0f
                                },
                                new()
                                {
                                    Shortname = "paper",
                                    Skin = 3048132587,
                                    CustomName = "Cash",
                                    AmountMin = 20,
                                    AmountMax = 35,
                                    ChanceToDrop = 40.0f
                                }
                            }
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
                _config = Config.ReadObject<Configuration>();
                if (_config == null) LoadDefaultConfig();
                SaveConfig();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                PrintWarning("Creating new config file.");
                LoadDefaultConfig();
            }
        }

        protected override void LoadDefaultConfig()
        {
            _config = Configuration.DefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config);
        }

        #endregion

        #region npcdatastuff

        public class ClothingOption
        {
            [JsonProperty("Shortname")] public string Shortname { get; set; }
            [JsonProperty("Skin")] public ulong Skin { get; set; }
        }

        public class BountyClothingOption
        {
            [JsonProperty("Shortname")] public string Shortname { get; set; }
            [JsonProperty("Skin")] public ulong Skin { get; set; }
        }

        public class BountyDrops
        {
            [JsonProperty("Shortname")] public string Shortname { get; set; }
            [JsonProperty("Skin")] public ulong Skin { get; set; }
            [JsonProperty("Custom Name")] public string CustomName { get; set; }
            [JsonProperty("Amount Min")] public int AmountMin { get; set; }
            [JsonProperty("Amount Max")] public int AmountMax { get; set; }
            [JsonProperty("Chance to Drop")] public float ChanceToDrop { get; set; }
        }

        private class NpcData
        {
            public string NpcName { get; set; }
            public string ShortPrefabName { get; set; }
            public PositionData Position { get; set; }
            public RotationData Rotation { get; set; }
        }

        public class PositionData
        {
            public float X, Y, Z;

            public Vector3 ToVector3()
            {
                return new Vector3(X, Y, Z);
            }
        }

        public class RotationData
        {
            public float X, Y, Z;

            public RotationData(Vector3 rotation)
            {
                X = rotation.x;
                Y = rotation.y;
                Z = rotation.z;
            }

            public Vector3 ToVector3()
            {
                return new Vector3(X, Y, Z);
            }
        }

        #endregion

        #region data

        private void SaveData()
        {
            try
            {
                Interface.Oxide.DataFileSystem.WriteObject("Bounty/Licenses", _playerLicenses);
                Interface.Oxide.DataFileSystem.WriteObject("Bounty/CustomSpawns", _customSpawnPoints);
                Interface.Oxide.DataFileSystem.WriteObject("Bounty/NPCData", new List<NpcData>());
                if (!_config.DynamicSpawn) Interface.Oxide.DataFileSystem.WriteObject("Bounty/NPCData", _npcDataList);
            }
            catch (Exception ex)
            {
                PrintError($"Error saving data: {ex.Message}");
            }
        }

        private void LoadData()
        {
            try
            {
                var loadedNpcData = Interface.Oxide.DataFileSystem.ReadObject<List<NpcData>>("Bounty/NPCData");
                if (loadedNpcData != null) _npcDataList.AddRange(loadedNpcData);
                _playerLicenses = Interface.Oxide.DataFileSystem.ReadObject<List<BountyLicense>>("Bounty/Licenses") ?? new List<BountyLicense>();
                var customSpawns = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<Vector3>>>("Bounty/CustomSpawns") ?? new Dictionary<string, List<Vector3>>();
                foreach (var pair in customSpawns) _customSpawnPoints[pair.Key] = pair.Value;
            }
            catch (Exception ex)
            {
                PrintError($"Error loading data: {ex.Message}");
            }
        }

        private void LoadTopKills()
        {
            var savedData = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<ulong, int>>("Bounty/playerData");
            _playerData = savedData ?? new Dictionary<ulong, int>();
            foreach (var playerId in _playerData.Keys.ToList())
            {
                _playerData.TryAdd(playerId, 0);
            }
        }

        private void ClearKills()
        {
            LoadTopKills();
            _playerData.Clear();
            SaveTopKills();
            Puts("Wipe Detected Clearing Top Kills");
        }

        private void SaveTopKills()
        {
            Interface.Oxide.DataFileSystem.WriteObject("Bounty/playerData", _playerData);
        }

        #endregion

        #region markerRemover

        private static bool InRange(Vector3 a, Vector3 b, float distance)
        {
            return (a - b).sqrMagnitude <= distance * distance;
        }

        private void OnEntitySpawned(MapMarkerMissionProvider marker)
        {
            timer.Once
            (1f, () =>
            {
                if (marker == null || marker.IsDestroyed) return;
                if (_bountyNpCs == null || _bountyNpCs.Count == 0) return;
                foreach (var npc in _bountyNpCs)
                {
                    if (npc == null || npc.transform == null || marker.transform == null) continue;
                    if (InRange(npc.transform.position, marker.transform.position, 0.1f))
                    {
                        NextTick(() => marker?.Kill());
                    }
                }
            });
        }

        #endregion

        #region hooks

        private object OnEntityTakeDamage(BaseEntity entity, HitInfo info)
        {
            if (entity == null || info?.Initiator == null)
            {
                return null;
            }
            if (_bountyNpCs.Contains(entity))
            {
                // Blocks NPCs from being damaged
                return false;
            }
            return null;
        }

        private object OnNpcKits(NPCTalking npc)
        {
            return _bountyNpCs.Contains(npc) ? true : null;
        }

        private void OnPlayerSleepEnded(BasePlayer player)
        {
            if (!permission.UserHasPermission(player.UserIDString, LifetimeLicense)) return;
            if (!HasBountyLicense(player.userID)) GiveLicense(player.userID);
        }

        private void OnServerInitialized(bool initial)
        {
            RegisterCommands();
            RegisterPerms();
            if (_config.EnableMonumentSpawns) LoadMonumentSpawnPoints();
            LoadCustomSpawnPoints();
            LoadDynamicSpawnPoints();
            LoadData();
            CheckDependencies(initial);
            CheckST();
            StartSpawnTimer();
            LoadTopKills();
            CheckAnnounce();
        }

        private void RegisterCommands()
        {
            var consoleCommand = _config.ConsoleCommand;
            var chatCommand = _config.ChatCommand;
            cmd.AddConsoleCommand(consoleCommand, this, nameof(BuyBLCcmd));
            cmd.AddChatCommand(chatCommand, this, nameof(BuyBountyLicCmd));
        }

        private void RegisterPerms()
        {
            permission.RegisterPermission(AdminPermission, this);
            permission.RegisterPermission(LifetimeLicense, this);
        }

        private void CheckDependencies(bool initial)
        {
            if (initial)
            {
                timer.Once(6f, SpawnNpCsFromData);
                timer.Once(7f, CheckPlugin);
            }
            else
            {
                SpawnNpCsFromData();
                CheckPlugin();
            }
        }

        private void CheckAnnounce()
        {
            if (_config.AnnounceInterval > 0)
            {
                timer.Repeat(_config.AnnounceInterval, 0, () => AnnounceTop());
            }
        }

        private void CheckST()
        {
            if (_config.EnabledSkillTreeXp)
            {
                Subscribe("STCanGainXP");
            }
        }

        private void OnNewSave(string filename)
        {
            LoadData();
            ClearNpcData();
            Puts("Wipe Detected, Removing Bounty Hunter NPCs.");
            if (_config.ClearLicenses) ClearPlayerLicenses();
            if (_config.ClearCustomSpawns) ClearCustomSpawnPoints();
            if (_config.ClearKills) ClearKills();
        }

        private void ClearNpcData()
        {
            _npcDataList.Clear();
            SaveData();
        }

        private void ClearPlayerLicenses()
        {
            _playerLicenses.Clear();
            Interface.Oxide.DataFileSystem.WriteObject("Bounty/Licenses", _playerLicenses);
            SaveData();
            Puts("Clearing Bounty Hunter Licenses.");
        }

        private void ClearCustomSpawnPoints()
        {
            _customSpawnPoints.Clear();
            Interface.Oxide.DataFileSystem.WriteObject("Bounty/CustomSpawns", _customSpawnPoints);
            SaveData();
            Puts("Clearing Bounty Hunter Custom Spawns.");
        }

        private void OnPlayerDeath(BasePlayer player, HitInfo info, string tierName, ScientistNPC bounty)
        {
            try
            {
                if (info.Initiator.IsNpc) return;
                var attacker = info.Initiator as BasePlayer;
                if (attacker == null || !player.name.StartsWith("Bounty_")) return;
                RemoveMarker(player.name);
                var pos = player.transform.position + new Vector3(0f, 2f, 0f);
                var container = GameManager.server.CreateEntity(Drop, pos) as DroppedItemContainer;
                if (container == null) return;
                if (container.inventory != null) return;
                if (container.inventory == null)
                {
                    container.inventory = new ItemContainer();
                    container.inventory.ServerInitialize(null, 36);
                    container.inventory.SetFlag(ItemContainer.Flag.NoItemInput, true);
                }
                var playerID = player.UserIDString;
                if (_botTierMap.TryGetValue(playerID, out var selectedBountyTier))
                {
                    var bountyDropsList = selectedBountyTier.BountyDropsList;
                    ShuffleList(bountyDropsList);
                    var lootcount = Random.Range(selectedBountyTier.MinItemDrop, selectedBountyTier.MaxItemDrop);
                    //Puts("lootcount" + lootcount);
                    for (var i = 0; i < lootcount;)
                    {
                        var entry = bountyDropsList[Random.Range(0, bountyDropsList.Count)];
                        var chance = Random.Range(0f, 100f);
                        if (chance > entry.ChanceToDrop)
                        {
                            continue;
                        }
                        var amount = Random.Range(entry.AmountMin, entry.AmountMax);
                        var item = ItemManager.CreateByName(entry.Shortname, amount, entry.Skin);
                        if (!string.IsNullOrEmpty(entry.CustomName))
                        {
                            item.name = entry.CustomName;
                        }
                        container.inventory.GiveItem(item);
                        i++;
                        // Puts("giving item" + item.name);
                    }
                }
                if (selectedBountyTier != null && _config.TokensEnabled)
                {
                    var bountyClaim = ItemManager.CreateByName(_config.PlaceholderItem, 1, selectedBountyTier.TokenSkinID);
                    bountyClaim.name = selectedBountyTier.TokenName;
                    container.inventory.GiveItem(bountyClaim);
                }
                container.inventory.MarkDirty();
                container.Spawn();
                container.name = $"{attacker.displayName} Bounty Drop";
                container.SendNetworkUpdateImmediate();
                _botCount--;
                if (_botCount == 0) Interface.CallHook("OnBountyInactive");
                _spawnedBounties.Remove(bounty);
                foreach (var pair in _despawnTimers.ToList().Where(pair => pair.Key != null && pair.Key == player.UserIDString))
                {
                    pair.Value.Destroy();
                    _despawnTimers.Remove(pair.Key);
                    break;
                }
                _playerData.TryAdd(attacker.userID, 0);
                _playerData[attacker.userID]++;
                SaveTopKills(); //Adding Kills to the tally
                var elimMessage = lang.GetMessage("BountyEliminated", this);
                if (selectedBountyTier == null) return;
                var bountyName = selectedBountyTier.TierName;
                var attackerName = attacker.displayName;
                timer.Repeat(0.3f, 5, () => Effect.server.Run(RicochetFx, pos));
                var message = lang.GetMessage("BountyDrop", this);
                if (_config.TokensEnabled) attacker.SendMessage(message, bountyName);
                if (_config.AnnounceBountyClaimed)
                    Server.Broadcast(string.Format(elimMessage, bountyName, attackerName), null, _config.ChatIcon);
                if (!SkillTree || !_config.EnabledSkillTreeXp) return;
                if (_config.XpLicReq && !HasBountyLicense(attacker.userID) && !permission.UserHasPermission(attacker.UserIDString, LifetimeLicense))
                    return;
                AwardXp(attacker, selectedBountyTier);
            }
            catch (Exception ex)
            {
                //Debug.LogError;
            }
        }

        private void AwardXp(BasePlayer player, BountyTiers tier)
        {
            if (tier.Value == 0) return;
            timer.Once
            (2f, () =>
            {
                SkillTree?.Call("AwardXP", player, tier.Value);
                player.ChatMessage("[<color=#b5a642>Bounty Hunter</color>] You gained <color=#b5a642>" + tier.Value + "</color> <color=#42b5a6>Xp</color> for the <color=#b5a642>Bounty</color> Takedown.");
                CommandSuccess(player);
            });
        }

        private object CanLootEntity(BasePlayer player, DroppedItemContainer container)
        {
            if (!container.name.Contains("Bounty Drop")) return null;
            var attackerName = container.name.Replace(" Bounty Drop", "");
            if (player.displayName == attackerName) return null;
            return false;
        }

        private object OnNpcConversationStart(NPCTalking npcTalking, BasePlayer player, ConversationData conversationData)
        {
            if (!_bountyNpCs.Contains(npcTalking)) return null;
            if (!_config.TokensMarketable)
            {
                var salesdisabled = lang.GetMessage("SaleDisabled", this, player.UserIDString);
                Player.Message(player, string.Format(salesdisabled), null, _config.ChatIcon);
                DeniedFX(player);
                return false; //returning false to prevent any dialogue built into the prefab npc
            }
            if (_config.LicenseRequired && !HasBountyLicense(player.userID))
            {
                var noLicenseMessage = lang.GetMessage("NoBountyHunterLicense", this, player.UserIDString);
                Player.Message(player, string.Format(noLicenseMessage), null, _config.ChatIcon);
                CommandSuccess(player);
                return false;
            }
            if (!_bountyNpCs.Contains(npcTalking)) return null;
            if (_interactionCooldowns.ContainsKey(player.userID) && Time.realtimeSinceStartup < _interactionCooldowns[player.userID])
            {
                var remainingCooldown = _interactionCooldowns[player.userID] - Time.realtimeSinceStartup;
                var cooldownMessage = lang.GetMessage("BountyHunterCooldownMessage", this, player.UserIDString);
                Player.Message(player, string.Format(cooldownMessage, (int)Math.Ceiling(remainingCooldown)), null, _config.ChatIcon);
                DeniedFX(player);
                return true;
            }
            _interactionCooldowns[player.userID] = Time.realtimeSinceStartup + _config.InteractionCooldown;
            HandleBountyTrade(player);
            return true;
        }

        private void Unload()
        {
            ClearTimers();
            ClearBounties();
            ClearBountyNPCs();
            SaveData();
            SaveTopKills();
            Unsubscribe("STCanGainXP");
        }

        private void ClearTimers()
        {
            spawnTimer?.Destroy();
        }

        #endregion

        #region tradehandler

        private void HandleBountyTrade(BasePlayer player)
        {
            foreach (var tokens in _config.BountyTiers)
                if (tokens.Sellable)
                    if (PlayerHasTokens(player, tokens))
                    {
                        var quantity = GetTotalTokenQuantity(player, tokens);
                        if (quantity > _config.MaxTradeQuantity) quantity = _config.MaxTradeQuantity;
                        RemoveTokensFromPlayer(player, tokens, quantity);
                        var salePriceInt = (int)tokens.SalePrice;
                        RewardHandler(player, tokens, quantity, salePriceInt);
                        return;
                    }
            var noTokens = lang.GetMessage("NoTokens", this, player.UserIDString);
            Player.Message(player, string.Format(noTokens), null, _config.ChatIcon);
            EffectNetwork.Send(new Effect(NoTokensFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void RemoveTokensFromPlayer(BasePlayer player, BountyTiers token, int quantity)
        {
            foreach (var item in player.inventory.containerMain.itemList.ToList())
                if (IsToken(item, token))
                {
                    var itemsToRemove = Mathf.Min(item.amount, quantity);
                    if (item.amount > itemsToRemove)
                    {
                        item.amount -= itemsToRemove;
                        item.MarkDirty();
                        player.SendNetworkUpdateImmediate();
                    }
                    else
                    {
                        item.RemoveFromContainer();
                        item.Remove();
                    }
                    quantity -= itemsToRemove;
                    if (quantity <= 0)
                        return;
                }
            foreach (var item in player.inventory.containerBelt.itemList.ToList())
                if (IsToken(item, token))
                {
                    var itemsToRemove = Mathf.Min(item.amount, quantity);
                    if (item.amount > itemsToRemove)
                    {
                        item.amount -= itemsToRemove;
                        item.MarkDirty();
                        player.SendNetworkUpdateImmediate();
                    }
                    else
                    {
                        item.RemoveFromContainer();
                        item.Remove();
                    }
                    quantity -= itemsToRemove;
                    if (quantity <= 0)
                        return;
                }
        }

        private int GetTotalTokenQuantity(BasePlayer player, BountyTiers token)
        {
            return player.inventory.containerMain.itemList.Concat(player.inventory.containerBelt.itemList).Where(item => IsToken(item, token)).Sum(item => item.amount);
        }

        private bool PlayerHasTokens(BasePlayer player, BountyTiers token)
        {
            return player.inventory.containerMain.itemList.Concat(player.inventory.containerBelt.itemList).Any(item => IsToken(item, token));
        }

        private bool IsToken(Item item, BountyTiers token)
        {
            return item.skin == token.TokenSkinID;
        }

        #endregion

        #region helpers

        void ShuffleList<T>(List<T> list)
        {
            for (var i = list.Count - 1; i > 0; i--)
            {
                var randomIndex = Random.Range(0, i + 1);
                (list[i], list[randomIndex]) = (list[randomIndex], list[i]);
            }
        }

        private void SpawnNpCsFromData()
        {
            if (_config.DynamicSpawn)
            {
                SpawnNpcsDynamically();
            }
            else
            {
                var npcDataCopy = Pool.Get<List<NpcData>>();
                npcDataCopy.AddRange(_npcDataList);
                foreach (var npcData in npcDataCopy)
                {
                    var spawnPosition = npcData.Position.ToVector3();
                    var npcRotation = npcData.Rotation.ToVector3();
                    var spawnRotation = Quaternion.Euler(npcRotation);
                    RespawnBountyNpc(ref spawnPosition, ref spawnRotation, npcData.NpcName, _npcDataList);
                }
                Pool.FreeUnmanaged(ref npcDataCopy);
            }
        }

        private void RespawnBountyNpc(ref Vector3 position, ref Quaternion rotation, string oldNpcName, List<NpcData> npcDataList)
        {
            if (!Economics && !ServerRewards) return;
            var oldNpcData = npcDataList.Find(data => data.NpcName == oldNpcName);
            if (oldNpcData == null) return;
            var npc = GameManager.server.CreateEntity(Npc, position, rotation) as NPCTalking;
            if (npc == null) return;
            npc.loadouts = Array.Empty<PlayerInventoryProperties>();
            var colliders = Physics.OverlapSphere(position, 1.0f);
            foreach (var collider in colliders)
            {
                var nearbyNpc = collider.GetComponent<NPCTalking>();
                if (nearbyNpc != null && nearbyNpc.PrefabName == Npc)
                    nearbyNpc.Kill();
            }
            npc.Spawn();
            npc.enableSaving = false;
            npc.gameObject.name = oldNpcName;
            _bountyNpCs.Add(npc);
            if (_config.NpcKitEnabled && !string.IsNullOrEmpty(_config.NpcKitName))
            {
                Kits?.Call("GiveKit", npc, _config.NpcKitName);
            }
            else
            {
                foreach (var clothingOption in _config.NpcClothingOptions)
                {
                    var clothingItemDef = ItemManager.FindItemDefinition(clothingOption.Shortname);
                    if (clothingItemDef == null) continue;
                    var clothingItem = ItemManager.Create(clothingItemDef);
                    if (clothingOption.Skin != 0)
                    {
                        clothingItem.skin = clothingOption.Skin;
                        clothingItem.MarkDirty();
                    }
                    if (clothingItem.MoveToContainer(npc.inventory.containerWear))
                    {
                        // Puts("BountyHunter Outfit Complete");
                    }
                    else
                    {
                        clothingItem.Remove();
                    }
                }
                var npcWeapon = _config.NpcWeapon;
                if (!string.IsNullOrEmpty(npcWeapon))
                {
                    var heldItemDef = ItemManager.CreateByName(npcWeapon, 1, _config.NpcWeaponSkin);
                    {
                        if (heldItemDef == null) return;
                        if (!heldItemDef.MoveToContainer(npc.inventory.containerBelt)) heldItemDef.Remove();
                    }
                }
            }
            var transform = npc.transform;
            var rotationData = new RotationData(transform.rotation.eulerAngles);
            var position1 = transform.position;
            var positionData = new PositionData
            {
                X = position1.x,
                Y = position1.y,
                Z = position1.z
            };
            npcDataList.Remove(oldNpcData);
            npcDataList.Add
            (new NpcData
            {
                ShortPrefabName = Npc,
                Position = positionData,
                Rotation = rotationData,
                NpcName = npc.gameObject.name
            });
            if (_config.MarkerEnabledForNPC) CreateMarkerForNpc(npc, "Bounty Hunter");
            SaveData();
        }

        private void LoadMonumentSpawnPoints()
        {
            var monumentsFound = 0;
            var spawnPointsInjected = 0;
            var monuments = TerrainMeta.Path.Monuments;
            foreach (var monument in monuments)
                if (monument.name.Contains("launch_site_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(245.31f, 66.00f, -20.68f));
                    spawnPoints.Add(new Vector3(-15.04f, 3.00f, -17.79f));
                    spawnPoints.Add(new Vector3(-84.51f, -3.00f, 64.95f));
                    spawnPoints.Add(new Vector3(-20.21f, -0.45f, -24.81f));
                    spawnPoints.Add(new Vector3(-116.16f, 3.00f, -54.54f));
                    spawnPoints.Add(new Vector3(-75.43f, -34.50f, 0.55f));
                    spawnPoints.Add(new Vector3(200.58f, 59.50f, 7.53f));
                    spawnPoints.Add(new Vector3(-13.99f, -2.98f, 10.40f));
                    spawnPoints.Add(new Vector3(-49.71f, 10.38f, -32.58f));
                    spawnPoints.Add(new Vector3(-23.82f, -3.00f, 54.10f));
                    spawnPoints.Add(new Vector3(1.64f, -3.00f, 19.54f));
                    spawnPoints.Add(new Vector3(-58.62f, 34.39f, -33.09f));
                    spawnPoints.Add(new Vector3(-40.69f, 34.40f, -31.59f));
                    _monumentSpawnPoints["launch_site_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["launch_site_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("desert_military_base_b"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(29.92f, -0.01f, -13.67f));
                    spawnPoints.Add(new Vector3(-39.83f, 0.20f, -4.36f));
                    spawnPoints.Add(new Vector3(-34.08f, -0.01f, 4.17f));
                    spawnPoints.Add(new Vector3(-28.30f, 0.14f, 25.48f));
                    spawnPoints.Add(new Vector3(-35.72f, -0.01f, 38.18f));
                    spawnPoints.Add(new Vector3(-0.70f, -0.01f, 18.54f));
                    spawnPoints.Add(new Vector3(33.11f, 0.01f, 4.01f));
                    _monumentSpawnPoints["desert_military_base_b"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["desert_military_base_b"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("excavator_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(17.79f, 15.91f, -6.14f));
                    spawnPoints.Add(new Vector3(35.93f, 12.43f, -27.37f));
                    spawnPoints.Add(new Vector3(63.08f, 10.03f, -39.19f));
                    spawnPoints.Add(new Vector3(66.49f, 3.02f, -40.67f));
                    spawnPoints.Add(new Vector3(41.19f, -4.97f, -24.44f));
                    spawnPoints.Add(new Vector3(4.92f, 5.34f, 19.96f));
                    _monumentSpawnPoints["excavator_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["excavator_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("nuclear_missile_silo"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(29.31f, 0.25f, 4.41f));
                    spawnPoints.Add(new Vector3(4.71f, 0.25f, -40.03f));
                    spawnPoints.Add(new Vector3(0.46f, 0.25f, -38.40f));
                    spawnPoints.Add(new Vector3(-26.74f, 0.25f, -31.12f));
                    spawnPoints.Add(new Vector3(-19.37f, 0.25f, -8.74f));
                    spawnPoints.Add(new Vector3(-31.30f, 0.38f, 12.15f));
                    spawnPoints.Add(new Vector3(11.41f, 0.31f, 20.85f));
                    _monumentSpawnPoints["nuclear_missile_silo"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["nuclear_missile_silo"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("harbor_2"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(14.71f, 4.07f, 21.75f)); //fixed 
                    spawnPoints.Add(new Vector3(27.20f, 4.00f, 24.75f)); //fixed
                    spawnPoints.Add(new Vector3(46.59f, 4.00f, 21.47f)); // fixed
                    spawnPoints.Add(new Vector3(43.01f, 3.89f, 3.73f)); // fixed
                    spawnPoints.Add(new Vector3(91.81f, 4.00f, -20.21f)); // Fixed
                    spawnPoints.Add(new Vector3(46.94f, 1.75f, -67.78f)); //fixed
                    _monumentSpawnPoints["harbor_2"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["harbor_2"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("harbor_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(-45.75f, 1.33f, 11.05f)); //fixed
                    spawnPoints.Add(new Vector3(-38.63f, 1.33f, -4.81f)); //fixed
                    spawnPoints.Add(new Vector3(9.72f, 4.25f, -9.51f));
                    spawnPoints.Add(new Vector3(45.02f, 4.25f, 2.29f)); // fixed
                    spawnPoints.Add(new Vector3(38.55f, 4.25f, 31.92f)); //fixed
                    _monumentSpawnPoints["harbor_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["harbor_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("airfield_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(-73.01f, 0.30f, -11.41f));
                    spawnPoints.Add(new Vector3(-109.45f, 3.22f, 50.90f));
                    spawnPoints.Add(new Vector3(-87.44f, 0.30f, 48.77f));
                    spawnPoints.Add(new Vector3(-27.44f, 0.30f, 33.57f));
                    spawnPoints.Add(new Vector3(16.96f, 0.30f, 32.98f));
                    spawnPoints.Add(new Vector3(47.33f, 0.30f, 40.56f));
                    spawnPoints.Add(new Vector3(112.99f, 3.05f, 10.91f));
                    spawnPoints.Add(new Vector3(97.69f, 3.23f, -91.54f));
                    spawnPoints.Add(new Vector3(47.70f, 0.30f, -83.86f));
                    spawnPoints.Add(new Vector3(4.90f, 3.30f, -84.93f));
                    spawnPoints.Add(new Vector3(-10.20f, 3.30f, -96.38f));
                    spawnPoints.Add(new Vector3(-29.41f, 3.29f, -90.06f));
                    _monumentSpawnPoints["airfield_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["airfield_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("arctic_research_base_a"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(-26.60f, 1.80f, -31.33f));
                    spawnPoints.Add(new Vector3(-32.04f, 4.75f, -19.15f));
                    spawnPoints.Add(new Vector3(-20.25f, 4.75f, -16.90f));
                    spawnPoints.Add(new Vector3(4.61f, 2.78f, 15.45f));
                    spawnPoints.Add(new Vector3(23.71f, 1.43f, 6.84f));
                    spawnPoints.Add(new Vector3(-43.94f, 6.01f, 35.21f));
                    spawnPoints.Add(new Vector3(25.39f, 0.12f, -34.08f));
                    spawnPoints.Add(new Vector3(29.01f, 3.01f, 35.77f));
                    _monumentSpawnPoints["arctic_research_base_a"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["arctic_research_base_a"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("ferry_terminal_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(-27.71f, 5.25f, -6.45f));
                    spawnPoints.Add(new Vector3(-8.30f, 5.14f, -28.74f));
                    spawnPoints.Add(new Vector3(30.58f, 5.25f, -5.24f));
                    spawnPoints.Add(new Vector3(66.24f, 6.38f, 41.49f));
                    spawnPoints.Add(new Vector3(-38.45f, 5.25f, 35.96f));
                    spawnPoints.Add(new Vector3(-57.95f, 0.90f, 11.20f));
                    spawnPoints.Add(new Vector3(-20.83f, 5.16f, -38.52f));
                    _monumentSpawnPoints["ferry_terminal_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["ferry_terminal_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("oilrig_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(20.42f, 45.13f, 2.78f));
                    spawnPoints.Add(new Vector3(-3.64f, 46.42f, 67.32f));
                    spawnPoints.Add(new Vector3(-7.18f, 27.13f, 45.06f));
                    spawnPoints.Add(new Vector3(-14.25f, 22.65f, -13.75f));
                    spawnPoints.Add(new Vector3(-13.80f, 36.40f, -2.28f));
                    spawnPoints.Add(new Vector3(-4.43f, 36.14f, 2.15f));
                    spawnPoints.Add(new Vector3(-15.07f, 1.14f, -2.71f));
                    spawnPoints.Add(new Vector3(8.26f, 9.87f, -28.90f));
                    _monumentSpawnPoints["oilrig_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["oilrig_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("water_treatment_plant_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(42.74f, 6.52f, -41.37f));
                    spawnPoints.Add(new Vector3(35.39f, 0.20f, -32.60f));
                    spawnPoints.Add(new Vector3(70.54f, 0.29f, -4.98f));
                    spawnPoints.Add(new Vector3(83.68f, -5.73f, -13.53f));
                    spawnPoints.Add(new Vector3(-81.93f, -5.73f, -71.98f));
                    spawnPoints.Add(new Vector3(-96.53f, 0.26f, -128.27f));
                    spawnPoints.Add(new Vector3(-69.99f, -5.75f, -133.27f));
                    spawnPoints.Add(new Vector3(-95.67f, 0.67f, -112.74f));
                    spawnPoints.Add(new Vector3(-15.53f, 3.25f, -69.03f));
                    _monumentSpawnPoints["water_treatment_plant_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["water_treatment_plant_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("trainyard_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(43.26f, -5.71f, -55.76f));
                    spawnPoints.Add(new Vector3(-33.29f, 9.01f, -45.82f));
                    spawnPoints.Add(new Vector3(-58.89f, 0.30f, -1.50f));
                    spawnPoints.Add(new Vector3(90.35f, 0.29f, -46.00f));
                    spawnPoints.Add(new Vector3(-29.43f, 0.01f, -99.42f));
                    _monumentSpawnPoints["trainyard_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["trainyard_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("powerplant_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(54.07f, 0.29f, 26.76f));
                    spawnPoints.Add(new Vector3(-18.45f, 0.26f, 62.68f));
                    _monumentSpawnPoints["powerplant_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["powerplant_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("oilrig_2"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(12.01f, 27.03f, -0.13f));
                    spawnPoints.Add(new Vector3(0.92f, 27.03f, -3.80f));
                    spawnPoints.Add(new Vector3(21.63f, 22.52f, -31.88f));
                    spawnPoints.Add(new Vector3(12.12f, 13.52f, -2.64f));
                    spawnPoints.Add(new Vector3(12.43f, 18.02f, -34.92f));
                    spawnPoints.Add(new Vector3(30.90f, 31.52f, -7.93f));
                    _monumentSpawnPoints["oilrig_2"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["oilrig_2"].Add(position);
                        spawnPointsInjected++;
                    }
                }
                else if (monument.name.Contains("military_tunnel_1"))
                {
                    monumentsFound++;
                    var spawnPoints = Pool.Get<List<Vector3>>();
                    var transform = monument.transform;
                    var monumentPosition = transform.position;
                    var monumentRotation = transform.rotation;
                    spawnPoints.Add(new Vector3(-10.81f, 7.56f, -23.63f));
                    spawnPoints.Add(new Vector3(8.54f, 7.77f, -29.30f));
                    spawnPoints.Add(new Vector3(-46.65f, 7.60f, -34.57f));
                    spawnPoints.Add(new Vector3(-62.50f, 19.52f, 43.40f));
                    spawnPoints.Add(new Vector3(-27.07f, 19.48f, -82.29f));
                    _monumentSpawnPoints["military_tunnel_1"] = Pool.Get<List<Vector3>>();
                    foreach (var position in spawnPoints.Select(spawnPoint => monumentPosition + monumentRotation * spawnPoint))
                    {
                        _monumentSpawnPoints["military_tunnel_1"].Add(position);
                        spawnPointsInjected++;
                    }
                }
            Puts("Usable Monuments Found: " + monumentsFound);
            //Puts("Total Possible Spawn Points Injected: " + spawnPointsInjected);
        }

        private void ClearBounties()
        {
            foreach (var bounty in _spawnedBounties.ToList())
            {
                _spawnedBounties.Remove(bounty);
                bounty?.Kill();
            }
            _spawnedBounties.Clear();
            Interface.CallHook("OnBountyInactive");
        }

        private void ClearBountyNPCs()
        {
            foreach (var npc in _bountyNpCs)
                npc?.Kill();
            _bountyNpCs.Clear();
        }

        private class BountyLicense
        {
            public ulong PlayerID { get; set; }
            public bool HasLicense { get; set; }
        }

        [HookMethod("HasBountyLicense")]
        private bool HasBountyLicense(ulong playerID)
        {
            if (permission.UserHasPermission(playerID.ToString(), LifetimeLicense)) return true;
            var existingLicense = _playerLicenses.Find(license => license.PlayerID == playerID);
            return existingLicense is { HasLicense: true };
        }

        private void GiveLicense(ulong playerID)
        {
            var existingLicense = _playerLicenses.Find(license => license.PlayerID == playerID);
            if (existingLicense != null)
            {
                if (existingLicense.HasLicense) return;
                existingLicense.HasLicense = true;
                SaveData();
            }
            else
            {
                _playerLicenses.Add(new BountyLicense { PlayerID = playerID, HasLicense = true });
                SaveData();
            }
        }

        private void OnPlayerRespawned(BasePlayer player)
        {
            if (_config.RemoveLicense && HasBountyLicense(player.userID))
                TakeLicense(player.userID);
        }

        private void TakeLicense(ulong playerID)
        {
            if (permission.UserHasPermission(playerID.ToString(), LifetimeLicense)) return; // do not remove license if it is a lifetime license
            var existingLicense = _playerLicenses.Find(license => license.PlayerID == playerID);
            var player = BasePlayer.FindByID(playerID);
            if (existingLicense == null) return;
            if (!existingLicense.HasLicense) return;
            existingLicense.HasLicense = false;
            timer.Once(3f, () => SendRevokeMessage(player));
            SaveData();
        }

        private void SendRevokeMessage(BasePlayer player)
        {
            var message = lang.GetMessage("Revoked", this, player.UserIDString);
            Player.Message(player, string.Format(message), null, _config.ChatIcon);
            CommandSuccess(player);
        }

        private void CheckPlugin()
        {
            if (!Economics && !ServerRewards)
            {
                Puts("Economics or ServerRewards need to be loaded to have NPCs spawned and handle trades!");
            }
            if (!MarkerManager)
            {
                Puts("MarkerManager needs to be loaded to have the Bounty Location Displayed!");
            }
            if (!SkillTree)
            {
                Puts("Skill Tree can be used to give bonus xp on Bounty Eliminations!");
            }
        }

        #endregion

        #region customspawn

        private string GetNearestMonumentName(Vector3 position)
        {
            return (from monumentEntry in _monumentSpawnPoints where monumentEntry.Value.Any(spawnPoint => Vector3.Distance(position, spawnPoint) < 5f) select monumentEntry.Key).FirstOrDefault();
        }

        private List<Vector3> GrabAllPoints(Dictionary<string, List<Vector3>> spawnPointsDict)
        {
            var spawnPointsList = Pool.Get<List<Vector3>>();
            foreach (var spawnList in spawnPointsDict.Values) spawnPointsList.AddRange(spawnList);
            return spawnPointsList;
        }

        private void LoadCustomSpawnPoints()
        {
            try
            {
                _customSpawnPoints = Interface.Oxide.DataFileSystem.ReadObject<Dictionary<string, List<Vector3>>>("Bounty/CustomSpawns") ?? new Dictionary<string, List<Vector3>>();
            }
            catch (Exception ex)
            {
                PrintError($"Error loading custom spawn points: {ex.Message}");
            }
        }

        #endregion

        #region dynamicspawn

        private void LoadDynamicSpawnPoints()
        {
            var monuments = TerrainMeta.Path.Monuments;
            foreach (var monument in monuments)
            {
                if (!monument.name.Contains("bandit_town")) continue;
                var spawnPoints = Pool.Get<List<Vector3>>();
                spawnPoints.Add(new Vector3(64.86f, 2.00f, -46.20f));
                spawnPoints.Add(new Vector3(-0.58f, 0.83f, -68.93f));
                var transform = monument.transform;
                var monumentPosition = transform.position;
                var monumentRotation = transform.rotation;
                var calculatedSpawnPoints = Pool.Get<List<Vector3>>();
                foreach (var spawnPoint in spawnPoints)
                {
                    calculatedSpawnPoints.Add(monumentPosition + monumentRotation * spawnPoint);
                }
                _dynamicSpawnPoints["bandit_town"] = calculatedSpawnPoints;
            }
        }

        private void SpawnNpcsDynamically()
        {
            foreach (var kvp in _dynamicSpawnPoints)
                if (kvp.Value.Count > 0)
                {
                    var spawnPoint = kvp.Value[Random.Range(0, kvp.Value.Count)];
                    var spawnRotation = Quaternion.Euler(0f, 0, 0f);
                    var npcName = "Bounty Hunter 1";
                    var newNpcData = new NpcData
                    {
                        NpcName = npcName,
                        Position = new PositionData { X = spawnPoint.x, Y = spawnPoint.y, Z = spawnPoint.z },
                        ShortPrefabName = Npc
                    };
                    _npcDataList.Add(newNpcData);
                    RespawnBountyNpc(ref spawnPoint, ref spawnRotation, npcName, _npcDataList);
                    break;
                }
        }

        #endregion

        #region commands

        [ChatCommand("givetokens")]
        private void GiveTokens(BasePlayer player)
        {
            if (player.IsAdmin)
            {
                foreach (var tier in _config.BountyTiers)
                {
                    var tokenSkinID = tier.TokenSkinID;
                    var item = ItemManager.CreateByName(_config.PlaceholderItem, 50, tokenSkinID);
                    if (item == null) continue;
                    item.name = tier.TokenName;
                    player.GiveItem(item);
                }
            }
            else
            {
                player.ChatMessage("You do not have permission to use this command.");
            }
        }

        [ChatCommand("ab")]
        private void AddBountySpawnCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                player.ChatMessage("You do not have permission to use this command.");
                return;
            }
            var playerPosition = player.transform.position;
            SaveCustomSpawnPoint(playerPosition);
            player.ChatMessage("Bounty Spawn Point Saved at Position: " + playerPosition);
        }

        private void SaveCustomSpawnPoint(Vector3 position)
        {
            if (!_customSpawnPoints.ContainsKey(CustomSpawn)) _customSpawnPoints[CustomSpawn] = Pool.Get<List<Vector3>>();
            _customSpawnPoints[CustomSpawn].Add(position);
            SaveData();
        }

        [ChatCommand("spawnBountyHunter")]
        private void SpawnBountyHunterCommand(BasePlayer player, string command)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                NoPermsMessage(player);
                DeniedFX(player);
                return;
            }
            if (!_config.TokensMarketable)
            {
                var salesdisabled = lang.GetMessage("SaleDisabled", this, player.UserIDString);
                Player.Message(player, string.Format(salesdisabled), null, _config.ChatIcon);
                EffectNetwork.Send(new Effect(DeniedFx, player.transform.position, player.transform.position), player.net.connection);
                return;
            }
            var spawnPosition = player.transform.position;
            var spawnRotation = Quaternion.Euler(player.serverInput.current.aimAngles);
            NPCSpawnedFx(player);
            SpawnBountyHunter(spawnPosition, spawnRotation);
        }

        private void SpawnBountyHunter(Vector3 position, Quaternion rotation)
        {
            if (!Economics && !ServerRewards)
                return;
            var npc = GameManager.server.CreateEntity(Npc, position, rotation) as NPCTalking;
            if (npc == null) return;
            npc.loadouts = new PlayerInventoryProperties[0];
            var colliders = Physics.OverlapSphere(position, 1.0f);
            foreach (var collider in colliders)
            {
                var nearbyNpc = collider.GetComponent<NPCTalking>();
                if (nearbyNpc != null && nearbyNpc.PrefabName == Npc) nearbyNpc.Kill();
            }
            npc.Spawn();
            npc.enableSaving = false;
            npc.gameObject.name = $"Bounty Hunter {_bountyNpCs.Count + 1}";
            _bountyNpCs.Add(npc);
            if (_config.NpcKitEnabled && !string.IsNullOrEmpty(_config.NpcKitName))
            {
                Kits?.Call("GiveKit", npc, _config.NpcKitName);
            }
            else
            {
                foreach (var clothingOption in _config.NpcClothingOptions)
                {
                    var clothingItemDef = ItemManager.FindItemDefinition(clothingOption.Shortname);
                    if (clothingItemDef == null) continue;
                    var clothingItem = ItemManager.Create(clothingItemDef);
                    if (clothingOption.Skin != 0)
                    {
                        clothingItem.skin = clothingOption.Skin;
                        clothingItem.MarkDirty();
                    }
                    if (clothingItem.MoveToContainer(npc.inventory.containerWear))
                    {
                        // Puts("Bounty Hunter Outfit Complete");
                    }
                    else
                    {
                        clothingItem.Remove();
                    }
                }
                var npcWeapon = _config.NpcWeapon;
                if (!string.IsNullOrEmpty(npcWeapon))
                {
                    var heldItemDef = ItemManager.CreateByName(npcWeapon, 1, _config.NpcWeaponSkin);
                    {
                        if (heldItemDef == null) return;
                        if (!heldItemDef.MoveToContainer(npc.inventory.containerBelt))
                            heldItemDef.Remove();
                    }
                }
            }
            var transform = npc.transform;
            var rotationData = new RotationData(transform.rotation.eulerAngles);
            var position1 = transform.position;
            var positionData = new PositionData
            {
                X = position1.x,
                Y = position1.y,
                Z = position1.z
            };
            _npcDataList.Add
            (new NpcData
            {
                ShortPrefabName = Npc,
                Position = positionData,
                Rotation = rotationData,
                NpcName = npc.gameObject.name
            });
            if (_config.MarkerEnabledForNPC) CreateMarkerForNpc(npc, "Bounty Hunter");
            SaveData();
        }

        [ChatCommand("removebountyhunter")]
        private void RemoveBountyHunterCommand(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                NoPermsMessage(player);
                DeniedFX(player);
                return;
            }
            if (Physics.Raycast(player.eyes.HeadRay(), out var hit))
            {
                var hitEntity = hit.GetEntity();
                if (hitEntity != null && hitEntity is NPCTalking && hitEntity.PrefabName.Equals(Npc))
                {
                    var npc = (NPCTalking)hitEntity;
                    if (_bountyNpCs.Contains(npc))
                    {
                        _bountyNpCs.Remove(npc);
                        var nameParts = npc.gameObject.name.Split(' ');
                        if (nameParts.Length == 3 && nameParts[0] == "Bounty" && nameParts[1] == "Hunter")
                            if (int.TryParse(nameParts[2], out var npcNumber))
                            {
                                var npcData = _npcDataList.FirstOrDefault(data => data.ShortPrefabName.Equals(Npc) && data.Position != null && data.Position.X == npc.transform.position.x && data.Position.Y == npc.transform.position.y && data.Position.Z == npc.transform.position.z);
                                if (npcData != null)
                                {
                                    _npcDataList.Remove(npcData);
                                    Interface.Oxide.DataFileSystem.WriteObject("Bounty/NPCData", _npcDataList);
                                    SaveData();
                                    Player.Message(player, "Bounty Hunter NPC data removed.", null, _config.ChatIcon);
                                    EffectNetwork.Send(new Effect(RemovalFx, player.transform.position, player.transform.position), player.net.connection);
                                }
                                else
                                {
                                    Player.Message(player, $"NPC for Bounty Hunter #{npcNumber} Not Found.", null, _config.ChatIcon);
                                }
                            }
                        npc.Kill();
                        if (_config.MarkerEnabled) RemoveMarker("BountyHunterMarker_" + _bountyNpCs.Count);
                        return;
                    }
                }
            }
            Player.Message(player, "You are not looking at an Bounty Hunter NPC.", null, _config.ChatIcon);
        }

        private void BuyBountyLicCmd(BasePlayer player)
        {
            if (!_config.LicenseRequired) return;
            if (HasBountyLicense(player.userID))
            {
                var alreadyHasLicense = lang.GetMessage("AlreadyHasLicense", this, player.UserIDString);
                Player.Message(player, alreadyHasLicense, null, _config.ChatIcon);
                DeniedFX(player);
                return;
            }
            double licenseCost = _config.LicenseFee;
            if (HasEnoughFunds(player, licenseCost))
            {
                PayFee(player, licenseCost);
                GiveLicense(player.userID);
                var currencySymbol = _config.EconomyPlugin == 1 ? "<color=#999966>$" : "<color=#d9534f>RP</color>";
                var message = lang.GetMessage("BuyBountyHunterLicenseSuccess", this, player.UserIDString);
                Player.Message(player, string.Format(message, currencySymbol, licenseCost, player.displayName), null, _config.ChatIcon);
                CommandSuccess(player);
            }
            else
            {
                var currencySymbol = _config.EconomyPlugin == 1 ? "<color=#999966>$</color>" : "<color=#d9534f>RP</color>";
                var errorMessage = lang.GetMessage("InsufficientFunds", this, player.UserIDString);
                Player.Message(player, string.Format(errorMessage, currencySymbol, licenseCost), null, _config.ChatIcon);
                DeniedFX(player);
            }
        }

        private void BuyBLCcmd(ConsoleSystem.Arg arg)
        {
            var targetPlayerId = arg.GetString(0);
            if (string.IsNullOrEmpty(targetPlayerId))
            {
                Puts("Usage: " + _config.ConsoleCommand + " <playerID>");
                return;
            }
            var targetPlayer = BasePlayer.Find(targetPlayerId) ?? BasePlayer.FindSleeping(targetPlayerId);
            if (targetPlayer == null)
            {
                Puts($"Player with ID '{targetPlayerId}' not found.");
                return;
            }
            if (!_config.LicenseRequired) return;
            if (HasBountyLicense(targetPlayer.userID))
            {
                var alreadyHasLicense = lang.GetMessage("AlreadyHasLicense", this, targetPlayer.UserIDString);
                Player.Message(targetPlayer, alreadyHasLicense, null, _config.ChatIcon);
                DeniedFX(targetPlayer);
                return;
            }
            double licenseCost = _config.LicenseFee;
            if (HasEnoughFunds(targetPlayer, licenseCost))
            {
                PayFee(targetPlayer, licenseCost);
                GiveLicense(targetPlayer.userID);
                var currencySymbol = _config.EconomyPlugin == 1 ? "<color=#999966>$" : "<color=#d9534f>RP</color>";
                var message = lang.GetMessage("BuyBountyHunterLicenseSuccess", this, targetPlayer.UserIDString);
                Player.Message(targetPlayer, string.Format(message, currencySymbol, licenseCost, targetPlayer.displayName), null, _config.ChatIcon);
                CommandSuccess(targetPlayer);
            }
            else
            {
                var currencySymbol = _config.EconomyPlugin == 1 ? "<color=#999966>$</color>" : "<color=#d9534f>RP</color>";
                var errorMessage = lang.GetMessage("InsufficientFunds", this, targetPlayer.UserIDString);
                Player.Message(targetPlayer, string.Format(errorMessage, currencySymbol, licenseCost), null, _config.ChatIcon);
                DeniedFX(targetPlayer);
            }
        }

        private bool HasEnoughFunds(BasePlayer player, double amount)
        {
            switch (_config.EconomyPlugin)
            {
                case 1:
                {
                    var balance = Economics.Call("Balance", player.UserIDString);
                    return (double)balance >= amount;
                }
                case 2:
                {
                    var points = ServerRewards.Call("CheckPoints", player.userID);
                    return (int)points >= amount;
                }
                default:
                    return false;
            }
        }

        private void PayFee(BasePlayer player, double amount)
        {
            switch (_config.EconomyPlugin)
            {
                case 1:
                    Economics.Call("Withdraw", player.UserIDString, amount);
                    break;
                case 2:
                {
                    var intAmount = (int)amount;
                    ServerRewards.Call("TakePoints", player.userID, intAmount);
                    break;
                }
            }
        }

        [ChatCommand("dp")]
        private void DebugSpawns(BasePlayer player, string command, string[] args)
        {
            if (!permission.UserHasPermission(player.UserIDString, AdminPermission))
            {
                DeniedFX(player);
                return;
            }
            DebugSpawnPoints(player, _monumentSpawnPoints, Color.magenta, "Monument");
            DebugSpawnPoints(player, _customSpawnPoints, Color.green, "Custom");
            Player.Message(player, "[<color=#b5a642>Bounty Hunter</color>] Spheres have been drawn at all Spawn Points.", null, _config.ChatIcon);
        }

        private void DebugSpawnPoints(BasePlayer player, Dictionary<string, List<Vector3>> spawnPoints, Color color, string type)
        {
            foreach (var spawnPoint in spawnPoints.Select(spawnPair => spawnPair.Value).SelectMany(spawnPointsList => spawnPointsList))
                player.SendConsoleCommand("ddraw.sphere", 30f, color, spawnPoint + new Vector3(0f, 1f, 0f), 1f);
        }

        private void SpawnBot()
        {
            if (_botCount == _config.MaxBounties) return;
            for (var i = _config.BountyTiers.Count - 1; i > 0; i--)
            {
                var j = Random.Range(0, i + 1);
                (_config.BountyTiers[i], _config.BountyTiers[j]) = (_config.BountyTiers[j], _config.BountyTiers[i]);
            }
            var tier = _config.BountyTiers[0];
            var allSpawnPoints = Pool.Get<List<Vector3>>();
            allSpawnPoints.AddRange(GrabAllPoints(_monumentSpawnPoints));
            if (_config.EnableCustomSpawns) allSpawnPoints.AddRange(GrabAllPoints(_customSpawnPoints));
            if (allSpawnPoints.Count == 0) return;
            var randomPos = allSpawnPoints[Random.Range(0, allSpawnPoints.Count)];
            var bounty = GameManager.server.CreateEntity(_config.BountyPrefab, randomPos, Quaternion.identity) as ScientistNPC;
            if (bounty != null)
            {
                bounty.name = "Bounty_" + tier.TierName;
                bounty.displayName = "Bounty";
                bounty.startHealth = tier.BountyHealth;
                bounty.damageScale = tier.BountyDamageOutput;
                bounty.aimConeScale = tier.AimConeScale;
                bounty.radioChatterType = ScientistNPC.RadioChatterType.NONE;
                bounty.CancelInvoke(nameof(ScientistNPC.PlayRadioChatter));
                if (NavMesh.SamplePosition(bounty.transform.position, out var navHit, 100f, NavMesh.AllAreas))
                {
                    bounty.Spawn();
                    if (bounty.TryGetComponent(out BaseNavigator navigator))
                    {
                        var isStationary = !navHit.hit;
                        timer.Once(0.1f, () => SetupNavigator(bounty, navigator, navHit, 50f, isStationary));
                    }
                }
                bounty.SendNetworkUpdateImmediate();
                _spawnedBounties.Add(bounty);
                _botCount++;
                if (_botCount > 0) Interface.CallHook("OnBountyActive");
                bounty.inventory.Strip();
                if (tier.BountyKitEnabled && !string.IsNullOrEmpty(tier.BountyKitName))
                {
                    Kits?.Call("GiveKit", bounty, tier.BountyKitName);
                } //ADDED KITS TO BOUNTIES
                else
                {
                    foreach (var clothingOption in tier.BountyClothingOptions)
                    {
                        var attire = ItemManager.CreateByName(clothingOption.Shortname, 1, clothingOption.Skin);
                        bounty.inventory.containerWear.GiveItem(attire);
                    }
                    var weapon = ItemManager.CreateByName(tier.WeaponName, 1, tier.WeaponSkin);
                    bounty.inventory.containerBelt.GiveItem(weapon);
                }
                var despawnTimer = timer.Once(_config.BountyDespawnTimer, () => { DespawnBot(bounty); });
                _despawnTimers.Add(bounty.UserIDString, despawnTimer);
                _botTierMap[bounty.UserIDString] = tier;
                if (_config.MarkerEnabled)
                {
                    CreateMarker(bounty, tier.TierName, displayName: tier.TierName);
                }
            }
            Pool.FreeUnmanaged(ref allSpawnPoints);
            if (!_config.AnnounceBountyActive)
            {
                return;
            }
            var spottedMessage = lang.GetMessage("BountySpotted", this);
            var nearestMonumentName = GetNearestMonumentName(randomPos);
            var locationDescription = nearestMonumentName != null ? ConvertMonPrefabName(nearestMonumentName) : "a Shady Location";
            var message = string.Format(spottedMessage, tier.TierName, locationDescription);
            Server.Broadcast(message, null, _config.ChatIcon);
            if (!_config.SpawnFX) return;
            foreach (var hunter in BasePlayer.activePlayerList)
            {
                BountySpawned(hunter);
            }
        }

        // Cred to Nivex for the nav Help.
        private void SetupNavigator(BaseCombatEntity owner, BaseNavigator navigator, NavMeshHit navHit, float distance, bool isStationary)
        {
            if (navHit.hit)
            {
                var settings = NavMesh.GetSettingsByIndex(navHit.mask);
                navigator.Agent.agentTypeID = settings.agentTypeID;
            }
            else
            {
                navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 0f;
                navigator.DefaultArea = "Not Walkable";
            }
            navigator.CanUseNavMesh = !isStationary && !Rust.Ai.AiManager.nav_disable;
            if (isStationary)
            {
                navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = 0f;
                navigator.DefaultArea = "Not Walkable";
            }
            else
            {
                var roamDistance = distance * 0.85f;
                navigator.MaxRoamDistanceFromHome = navigator.BestMovementPointMaxDistance = navigator.BestRoamPointMaxDistance = roamDistance;
                navigator.DefaultArea = "Walkable";
                navigator.topologyPreference = ((TerrainTopology.Enum)TerrainTopology.EVERYTHING);
            }
            if (navigator.CanUseNavMesh)
            {
                navigator.Init(owner, navigator.Agent);
            }
        }

        private static string ConvertMonPrefabName(string shortName)
        {
            var nameMappings = new Dictionary<string, string>
            {
                { "launch_site_1", "Launch Site" },
                { "military_tunnel_1", "Military Tunnel" },
                { "desert_military_base_b", "Abandoned Military Base" },
                { "excavator_1", "Giant Excavator Pit" },
                { "nuclear_missile_silo", "Missile Silo" },
                { "harbor_2", "Harbor" },
                { "harbor_1", "Harbor" },
                { "airfield_1", "Airfield" },
                { "arctic_research_base_a", "Arctic Research Base" },
                { "ferry_terminal_1", "Ferry Terminal" },
                { "oilrig_1", "Large Oil Rig" },
                { "water_treatment_plant_1", "Water Treatment Plant" },
                { "trainyard_1", "Train Yard" },
                { "powerplant_1", "Power Plant" },
                { "oilrig_2", "Oil Rig" }
            };
            return nameMappings.GetValueOrDefault(shortName, shortName);
        }

        #endregion

        #region marker

        private void CreateMarker(BaseEntity entity, string bounty, int duration = 0, float refreshRate = 3f, float radius = 0.2f, string displayName = "Bounty", string colorMarker = "FF0000", string colorOutline = "606060", float alpha = 0.75f)
        {
            Interface.CallHook("API_CreateMarker", entity, bounty, duration, refreshRate, radius, displayName, colorMarker, colorOutline, alpha);
        }

        private void CreateMarkerForNpc(BaseEntity entity, string bounty, int duration = 0, float refreshRate = 3f, float radius = 0.2f)
        {
            CreateMarker(entity, bounty, duration, refreshRate, radius, "Bounty Hunter Cashout");
        }

        private void RemoveMarker(string bounty)
        {
            Interface.CallHook("API_RemoveMarker", bounty);
        }

        #endregion

        #region discord

        private void SendDiscord(BasePlayer player, BountyTiers targetTokens, int quantity, int totalReward)
        {
            var webhookUrl = _config.Webhook;
            var playerName = $"{player.displayName} ({player.UserIDString})";
            var currency = _config.EconomyPlugin == 1 ? "$" : "RP";
            var content = _discordJson.Replace("{title}", "Bounty Claims").Replace("{description}", $"{playerName} turned in {quantity}x {targetTokens.TokenName}(s) for {currency}{totalReward}.").Replace("{imageUrl}", DiscordArt);
            if (string.IsNullOrEmpty(webhookUrl) || !webhookUrl.Contains("/api/webhooks"))
                return;
            webrequest.Enqueue
            (webhookUrl, content, (code, response) =>
            {
                if (code != 204) Puts($"Discord responded with code {code}. Response: {response}");
            }, this, RequestMethod.POST, new Dictionary<string, string> { ["Content-Type"] = "application/json" });
        }

        private readonly string _discordJson = @"{
    ""embeds"": [
        {
            ""type"": ""rich"",
            ""title"": ""{title}"",
            ""description"": ""{description}"",
            ""color"": 16711680,
            ""thumbnail"": {
                ""url"": ""{imageUrl}"",
                ""proxy_url"": ""{imageUrl}"",
                ""height"": 64,
                ""width"": 64
            }
        }
    ]
}";

        #endregion

        #region fx

        private void BountySpawned(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(SpawnedFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void BountyDespawned(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(DespawnedFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void NPCSpawnedFx(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(NpcFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void CommandSuccess(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(SuccessFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void Notification(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(ReloadFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void DeniedFX(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(DeniedFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void SendEcoFx(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(EconomicsFx, player.transform.position, player.transform.position), player.net.connection);
        }

        private void SendSrFx(BasePlayer player)
        {
            EffectNetwork.Send(new Effect(ServerRewardsFx, player.transform.position, player.transform.position), player.net.connection);
        }

        #endregion

        #region Timers

        private void StartSpawnTimer()
        {
            spawnTimer = timer.Every(_config.BountySpawnRate, SpawnBot);
        }

        private void DespawnBot(ScientistNPC bounty)
        {
            _botCount--;
            if (_botCount == 0) Interface.CallHook("OnBountyInactive");
            if (bounty == null)
            {
                return;
            }
            if (!_botTierMap.TryGetValue(bounty.UserIDString, out var selectedBountyTier)) return;
            bounty.Kill();
            _spawnedBounties.Remove(bounty);
            var bountyName = lang.GetMessage(selectedBountyTier.TierName, this);
            var despawnMessage = lang.GetMessage("Despawn", this);
            if (_config.AnnounceBountyActive)
            {
                Server.Broadcast(string.Format(despawnMessage, bountyName), null, _config.ChatIcon);
                if (_config.DespawnFX)
                {
                    foreach (var player in BasePlayer.activePlayerList)
                    {
                        BountyDespawned(player);
                    }
                }
            }
            if (!_despawnTimers.TryGetValue(bounty.UserIDString, out var despawnTimer)) return;
            despawnTimer.Destroy();
            _despawnTimers.Remove(bounty.UserIDString);
        }

        #endregion

        #region topBountyHunters

        private void AnnounceTop(bool isGlobal = true, BasePlayer targetPlayer = null)
        {
            var topPlayers = _playerData.Where(x => x.Value > 0).OrderByDescending(x => x.Value).Take(5).ToList();
            if (!topPlayers.Any()) return;
            var s = "<size=16><color=#b5a642>Top Bounty Hunters</color></size>\n";
            var rankMessages = Pool.Get<List<string>>();
            var killRank = 1;
            foreach (var playerEntry in topPlayers)
            {
                var playerId = playerEntry.Key;
                var bKills = playerEntry.Value;
                var player = covalence.Players.FindPlayer(playerId.ToString());
                var playerName = player?.Name ?? "Unknown";
                rankMessages.Add($"\n<color=#b5a642>Rank <color=#b56d42>{killRank}</color> <color=#b5a642>{playerName}</color> - <color=#b5a642>Bounty Kills: <color=#b56d42>{bKills}</color></color></color>");
                killRank++;
            }
            var message = s + string.Join("\n", rankMessages);
            if (isGlobal)
            {
                Server.Broadcast(message, null, _config.ChatIcon);
                foreach (var onlinePlayer in BasePlayer.activePlayerList.ToList())
                {
                    Notification(onlinePlayer);
                }
            }
            else if (targetPlayer != null)
            {
                Player.Message(targetPlayer, message, null, _config.ChatIcon);
                Notification(targetPlayer);
            }
            Pool.FreeUnmanaged(ref rankMessages);
        }

        [ChatCommand("btop")]
        private void AnnounceTopCmd(BasePlayer player)
        {
            AnnounceTop(false, player);
        }

        #endregion

        #region noPermsMessage

        private void NoPermsMessage(BasePlayer player)
        {
            var message = lang.GetMessage("NoPerms", this, player.UserIDString);
            Player.Message(player, string.Format(message), null, _config.ChatIcon);
        }

        #endregion

        #region API

        [HookMethod("GetBountyStats")]
        public object GetBountyStats()
        {
            var playerStats = new Dictionary<ulong, int>();
            foreach (var entry in _playerData)
            {
                var playerId = entry.Key;
                var bKills = entry.Value;
                playerStats[playerId] = bKills;
            }
            return playerStats;
        }

        #endregion
    }
}